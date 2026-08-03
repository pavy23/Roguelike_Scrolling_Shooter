using Shmup.Core;
using Shmup.Core.Content;
using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 격납고 (UGUI, 함선 해금형 메타 — 2026-07-29 사람 확정).
    /// ←/→ 또는 패드 dpad 함선 순환, U/(Y) 해금, 선택은 즉시 저장 —
    /// 전투 씬이 저장을 읽어 함선을 적용한다. 해금/선택 규칙은 전부 Core MetaState 소관.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HangarScreen : MonoBehaviour
    {
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;
        [SerializeField] string[] _shipIds;
        [SerializeField] Sprite[] _shipSprites;

        GameDataSet _data;
        MetaState _meta;
        int _cursor;
        Text _headerText, _bodyText;
        Image _preview;
        Button _unlockButton;
        int _shownCursor = -1;
        long _shownCurrency = -1;
        string _shownSelected;

        // 컨티뉴 재고 (REQ-104). 가격 사다리·상한·거절 사유는 전부 Core 판정이고,
        // 여기서는 물어본 값을 그리고 구매 요청만 넘긴다.
        Text _continueText;
        Button _continueButton;
        int _shownContinueStock = -1;

        /// <summary>구매 실패 문면을 잠깐 띄우는 타이머 (크레딧 부족 등).</summary>
        float _continueNoticeTimer;
        string _continueNotice;

        /// <summary>같은 GameObject의 타이틀 화면 (랭킹 모달 상태를 물어본다).</summary>
        TitleScreen _title;

        void Start()
        {
            _data = GameDataParser.Parse(
                LoadText("enemies"), LoadText("weapons"), LoadText("waves"),
                TryLoadText("rewards"), TryLoadText("ships"), TryLoadText("scoring"));
            _meta = MetaSave.Load(_data);
            for (int i = 0; i < _data.Ships.Count; i++)
                if (_data.Ships[i].Id == _meta.SelectedShipId)
                    _cursor = i;

            var canvas = UiKit.CreateCanvas("HangarCanvas", 55);
            canvas.transform.SetParent(transform, false);
            _headerText = UiKit.CreateCornerText(canvas.transform, _fontBold, "", 12,
                UiKit.TextAccent, new Vector2(0.5f, 0f), new Vector2(0f, 46f),
                TextAnchor.LowerCenter, "Header");
            _bodyText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextMain, new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                TextAnchor.LowerCenter, "Body");

            // 기체 미리보기 (픽셀 ×2 확대)
            var previewGo = new GameObject("ShipPreview");
            previewGo.transform.SetParent(canvas.transform, false);
            _preview = previewGo.AddComponent<Image>();
            _preview.raycastTarget = false;
            var rect = _preview.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 74f);
            _preview.enabled = false;

            // 컨티뉴 재고 — 격납고 왼쪽 아래. 함선 정보(가운데)와 겹치지 않고,
            // "출격 전에 사 두는 물건"이라는 점에서 해금과 같은 층위에 둔다.
            _continueText = UiKit.CreateCornerText(canvas.transform, _font, "", 10,
                // 62 = BUY 버튼(y14, 높이34) 위로 확실히 띄운다. 52였을 때는 버튼
                // 테두리에 글자가 걸쳐 겹쳐 보였다 (사람 지적 2026-08-03).
                UiKit.TextDim, new Vector2(0f, 0f), new Vector2(10f, 62f),
                TextAnchor.LowerLeft, "ContinueStock");
            _continueText.rectTransform.sizeDelta = new Vector2(220f, 28f);

            if (UiPlatform.TouchMode)
            {
                UiKit.CreateTouchButton(canvas.transform, _fontBold, "◄", 16,
                    new Vector2(0.5f, 0f), new Vector2(-146f, 36f), new Vector2(36f, 36f),
                    () => MoveCursor(-1), "HangarPrev");
                UiKit.CreateTouchButton(canvas.transform, _fontBold, "►", 16,
                    new Vector2(0.5f, 0f), new Vector2(146f, 36f), new Vector2(36f, 36f),
                    () => MoveCursor(1), "HangarNext");
                _unlockButton = UiKit.CreateTouchButton(canvas.transform, _font, "UNLOCK", 10,
                    new Vector2(1f, 0f), new Vector2(-10f, 14f), new Vector2(104f, 34f),
                    TryUnlockCurrent, "HangarUnlock");
                _continueButton = UiKit.CreateTouchButton(canvas.transform, _font, "BUY", 10,
                    new Vector2(0f, 0f), new Vector2(10f, 14f), new Vector2(104f, 34f),
                    TryBuyContinue, "HangarBuyContinue");
            }
        }

        /// <summary>
        /// 컨티뉴 한 개 구매. 가격 사다리(2,000 + 1,000 × 보유)와 상한 8, 크레딧 검사는
        /// 전부 Core(TryPurchaseContinue)가 한다 — 여기서 값을 다시 계산하지 않는다.
        /// 거절 사유는 짧게 띄운다: 눌렀는데 아무 일도 안 일어나면 고장으로 읽힌다.
        /// </summary>
        void TryBuyContinue()
        {
            if (_meta == null) return;
            var result = _meta.TryPurchaseContinue();
            if (result.Purchased)
            {
                MetaSave.Save(_meta);
                _continueNotice = null;
                _continueNoticeTimer = 0f;
            }
            else
            {
                _continueNotice =
                    result.RejectionReason
                        == ContinuePurchaseRejectionReason.InsufficientCurrency
                        ? $"NEED {result.Price:N0} cr"
                        : UiText.HangarContinueFull;
                _continueNoticeTimer = 2f;
            }
            _shownContinueStock = -1;   // 표시 갱신
            _shownCurrency = -1;
        }

        void MoveCursor(int delta)
        {
            if (_data == null || _data.Ships.Count == 0) return;
            _cursor = (_cursor + delta + _data.Ships.Count) % _data.Ships.Count;
        }

        /// <summary>커서의 함선을 해금한다 (크레딧이 모자라면 Core가 거부한다).</summary>
        void TryUnlockCurrent()
        {
            if (_data == null || _meta == null || _data.Ships.Count == 0) return;
            var ship = _data.Ships[_cursor];
            if (_meta.IsUnlocked(ship.Id)) return;
            if (_meta.TryUnlock(ship))
                MetaSave.Save(_meta);
            _shownCursor = -1;   // 표시 갱신
        }

        Sprite SpriteForShip(string shipId)
        {
            if (_shipIds == null || _shipSprites == null) return null;
            int count = Mathf.Min(_shipIds.Length, _shipSprites.Length);
            for (int i = 0; i < count; i++)
                if (string.Equals(_shipIds[i], shipId, System.StringComparison.Ordinal))
                    return _shipSprites[i];
            return null;
        }

        void Update()
        {
            if (_data == null || _meta == null || _data.Ships.Count == 0) return;

            // 랭킹 모달이 떠 있으면 격납고는 입력을 받지 않는다 — 보드를 읽는 동안
            // 화살표가 함선을 넘기거나 [B]가 크레딧을 쓰면 안 된다(타이틀도 같은 규칙).
            if (_title == null) _title = GetComponent<TitleScreen>();
            if (_title != null && _title.RankingOpen)
            {
                RefreshTexts(_data.Ships[_cursor]);
                RefreshContinue();
                return;
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) move = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame) move = 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) move = -1;
                if (gamepad.dpad.right.wasPressedThisFrame) move = 1;
            }
            _cursor = (_cursor + move + _data.Ships.Count) % _data.Ships.Count;

            var ship = _data.Ships[_cursor];
            bool unlockPressed = (keyboard != null && keyboard.uKey.wasPressedThisFrame)
                              || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);
            if (unlockPressed)
                TryUnlockCurrent();
            // 컨티뉴 구매는 [B]. 패드는 dpad 아래로 — 함선 순환(좌/우)과 축이 갈려
            // 기체를 넘기다 실수로 크레딧을 쓰는 일이 없다.
            bool buyPressed = (keyboard != null && keyboard.bKey.wasPressedThisFrame)
                           || (gamepad != null && gamepad.dpad.down.wasPressedThisFrame);
            if (buyPressed)
                TryBuyContinue();

            if (_continueNoticeTimer > 0f)
            {
                _continueNoticeTimer -= Time.unscaledDeltaTime;
                if (_continueNoticeTimer <= 0f)
                {
                    _continueNotice = null;
                    _shownContinueStock = -1;
                }
            }
            if (_meta.IsUnlocked(ship.Id) && _meta.SelectedShipId != ship.Id)
            {
                _meta.SelectShip(ship.Id);
                MetaSave.Save(_meta);
            }

            RefreshTexts(ship);
            RefreshContinue();
        }

        /// <summary>
        /// 컨티뉴 재고 줄 + 구매 버튼. 값이 바뀐 프레임에만 문자열을 만든다
        /// (타이틀은 계속 떠 있는 화면이라 매 프레임 할당이 그대로 쓰레기가 된다).
        /// </summary>
        void RefreshContinue()
        {
            if (_continueText == null || _meta == null) return;
            int stock = _meta.ContinueStock;
            // 재고 말고는 이 줄을 바꾸는 게 없다. 구매 실패 문면은 스스로 -1을 심어
            // 다음 프레임에 한 번 더 그린다.
            if (stock == _shownContinueStock) return;
            _shownContinueStock = stock;

            int max = ContinueEconomyConfig.DefaultMaximumStock;
            long price = _meta.GetContinuePurchasePrice();
            bool full = stock >= max;
            var sb = new System.Text.StringBuilder(64);
            sb.Append(string.Format(UiText.HangarContinueStockFormat, stock, max));
            if (_continueNotice != null)
                sb.Append("   ").Append(_continueNotice);
            else if (full)
                sb.Append("   ").Append(UiText.HangarContinueFull);
            else if (!UiPlatform.TouchMode)
                sb.Append("   ").Append(
                    string.Format(UiText.HangarContinueHint, price.ToString("N0")));
            _continueText.text = sb.ToString();
            // 재고가 있으면 "죽어도 이어서 갈 수 있다"는 사실 자체가 정보다 — 앰버로 켠다.
            _continueText.color = stock > 0 ? UiKit.TextAccent : UiKit.TextDim;

            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(!full);
                if (!full)
                {
                    var label = _continueButton.GetComponentInChildren<Text>();
                    if (label != null)
                        label.text = string.Format(
                            UiText.HangarContinueBuyFormat, price.ToString("N0"));
                }
            }
        }

        void RefreshTexts(Core.ShipDefinition ship)
        {
            if (_headerText == null || _bodyText == null) return;
            if (_shownCursor == _cursor
                && _shownCurrency == _meta.TotalCurrency
                && _shownSelected == _meta.SelectedShipId) return;
            _shownCursor = _cursor;
            _shownCurrency = _meta.TotalCurrency;
            _shownSelected = _meta.SelectedShipId;

            _headerText.text =
                $"HANGAR  ◄ {_cursor + 1}/{_data.Ships.Count} ►      CREDIT {_meta.TotalCurrency:N0}";

            var previewSprite = SpriteForShip(ship.Id);
            if (_preview != null)
            {
                _preview.enabled = previewSprite != null;
                if (previewSprite != null)
                {
                    _preview.sprite = previewSprite;
                    _preview.rectTransform.sizeDelta = previewSprite.rect.size * 2f;
                    // 미해금 함선은 실루엣으로
                    _preview.color = _meta.IsUnlocked(ship.Id) ? Color.white : new Color(0.1f, 0.12f, 0.2f, 0.9f);
                }
            }
            bool unlocked = _meta.IsUnlocked(ship.Id);
            // 해금 버튼은 잠긴 함선에서만 의미가 있다 — 값까지 라벨에 실어 준다.
            if (_unlockButton != null)
            {
                _unlockButton.gameObject.SetActive(!unlocked);
                if (!unlocked)
                {
                    var label = _unlockButton.GetComponentInChildren<Text>();
                    if (label != null) label.text = $"UNLOCK\n{ship.UnlockCost:N0} cr";
                }
            }
            string status = unlocked
                ? (_meta.SelectedShipId == ship.Id ? "[SELECTED]" : "[OWNED]")
                : (UiPlatform.TouchMode
                    ? $"[LOCKED — {ship.UnlockCost:N0} cr]"
                    : $"[LOCKED — {ship.UnlockCost:N0} cr, U/(Y) to unlock]");
            // 기체를 가르는 수치는 **이동 속도와 실드 재고** 둘뿐이다. 시작 파워업
            // 레벨은 세 기체 모두 전부 0이라 "start S0 M0 O0 B0"은 아무 정보도 주지
            // 않으면서 고르는 데 방해만 됐다 (사람 지시 2026-08-03).
            _bodyText.text =
                $"{ship.DisplayName}  {status}\n" +
                $"speed x{(float)ship.MoveSpeedMultiplierNumerator / ship.MoveSpeedMultiplierDenominator:0.##}   " +
                $"shield x{ship.StartingShieldStock ?? 0}";
        }

        static string LoadText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            if (asset == null)
                throw new System.InvalidOperationException($"Resources/GameData/{name} 없음 — 씬 재생성 필요.");
            return asset.text;
        }

        static string TryLoadText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            return asset != null ? asset.text : null;
        }
    }
}
