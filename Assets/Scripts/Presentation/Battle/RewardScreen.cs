using Shmup.Core;
using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 스테이지 클리어 보상 3택 (UGUI + 픽셀 폰트, REQ-007).
    /// 키보드 1/2/3 즉시 선택, 패드/방향키 좌우 이동 + South(A)/Enter 확정.
    /// RunManager가 AwaitingReward로 멈춰 있는 동안만 표시 — 선택만 Core에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardScreen : MonoBehaviour
    {
        const int MaxOptions = 3;

        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        GameObject _root;
        Text _titleText;
        readonly Image[] _boxBorders = new Image[MaxOptions];
        readonly Text[] _boxTexts = new Text[MaxOptions];
        readonly RectTransform[] _boxRects = new RectTransform[MaxOptions];
        bool _labelsBuilt;
        int _shownOptionCount;
        int _cursor;
        float _emptyOptionsAge;
        const float EmptyOptionsGrace = 2.5f;
        UnityEngine.UI.Button _rerollButton;
        Text _rerollLabel;
        UnityEngine.UI.Image _rerollBg;

        const float BoxWidth = 150f, BoxHeight = 84f, BoxGap = 14f;

        /// <summary>
        /// 후보 수에 맞춰 카드를 가운데 정렬한다. 중간보스 뒤에는 2택, 스테이지 보스
        /// 뒤에는 3택이 오므로(REQ-054) "항상 3개"를 가정하면 2택이 한쪽으로 치우친다.
        /// </summary>
        void LayoutBoxes(int count)
        {
            if (_shownOptionCount == count) return;
            _shownOptionCount = count;
            float total = count * BoxWidth + (count - 1) * BoxGap;
            for (int i = 0; i < MaxOptions; i++)
            {
                if (_boxRects[i] == null) continue;
                _boxRects[i].anchoredPosition = new Vector2(
                    -total / 2f + BoxWidth / 2f + i * (BoxWidth + BoxGap), -10f);
            }
        }

        void Start()
        {
            var canvas = UiKit.CreateCanvas("RewardCanvas", 70);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.55f));
            _titleText = UiKit.CreateCornerText(canvas.transform, _fontBold, UiText.RewardTitle, 16,
                UiKit.TextAccent, new Vector2(0.5f, 1f), new Vector2(0f, -86f),
                TextAnchor.UpperCenter, "Title");

            // 효과 설명이 한 줄 늘어나 카드를 높였다 (150×3 + 간격 = 478 < 640이라 폭은 그대로).
            // 실제 배치는 후보 수를 아는 시점에 LayoutBoxes가 다시 잡는다.
            for (int i = 0; i < MaxOptions; i++)
            {
                var panel = UiKit.CreatePanel(canvas.transform, new Vector2(BoxWidth, BoxHeight), $"Option{i}");
                _boxRects[i] = panel;
                _boxBorders[i] = panel.GetComponent<Image>();
                _boxTexts[i] = UiKit.CreateTextStretch(panel, _font, "", 10,
                    UiKit.TextMain, TextAnchor.MiddleCenter, 4f, "Label");
                // 카드를 그대로 탭 대상으로 쓴다 — 별도 버튼을 얹는 것보다 손이 가는 곳이 명확하다.
                int index = i;   // 클로저가 루프 변수를 잡지 않도록 복사
                UiKit.MakeTappable(_boxBorders[i], () => Choose(index));
            }
            UiKit.CreateCornerText(canvas.transform, _font,
                UiPlatform.TouchMode ? UiText.ChoiceHintsTouch : UiText.ChoiceHints,
                10, UiKit.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), TextAnchor.MiddleCenter, "Hints");

            // 리롤 (REQ-072): 캡슐을 지불하고 후보를 다시 뽑는다 — 성장이냐 선택권이냐.
            // 카드 아래 중앙. 잔고 부족이면 흐리게 두되 계속 보인다 — 기능의 존재를
            // 숨기면 캡슐을 아껴 둘 이유도 배울 수 없다.
            _rerollButton = UiKit.CreateTouchButton(
                canvas.transform, _font, "", 10,
                new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(170f, 34f),
                OnReroll, "Reroll");
            _rerollBg = _rerollButton.targetGraphic as UnityEngine.UI.Image;
            _rerollLabel = _rerollButton.GetComponentInChildren<Text>();

            _root.SetActive(false);
        }

        void OnReroll()
        {
            if (_director == null || !_director.RerollRewards()) return;
            _labelsBuilt = false;   // 새 후보로 카드 재구성
        }

        void UpdateRerollButton()
        {
            if (_rerollButton == null || _director == null) return;
            int cost = _director.RewardRerollCost;
            int balance = _director.CapsuleBalance;
            bool can = _director.CanRerollRewards;
            if (_rerollLabel != null)
                _rerollLabel.text = $"REROLL  {cost} CAPS  (HAVE {balance})";
            if (_rerollLabel != null)
                _rerollLabel.color = can ? UiKit.TextAccent : UiKit.TextDim;
            if (_rerollBg != null)
            {
                var c = _rerollBg.color;
                c.a = can ? 1f : 0.45f;
                _rerollBg.color = c;
            }
        }

        /// <summary>탭/키 공용 선택. 열려 있지 않거나 범위를 벗어난 탭은 무시한다.</summary>
        void Choose(int index)
        {
            if (_director == null || !_director.AwaitingReward) return;
            var options = _director.RewardOptions;
            if (options == null || index < 0 || index >= options.Count) return;
            _director.ChooseReward(index);
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            bool awaiting = _director.AwaitingReward;
            if (_root.activeSelf != awaiting)
                _root.SetActive(awaiting);
            if (!awaiting)
            {
                _labelsBuilt = false;
                return;
            }

            var options = _director.RewardOptions;

            // 안전 장치: 보상 대기 상태인데 후보가 비어 있으면 탭할 카드가 없어
            // **영구히 갇힌다** (화면은 떠 있으니 멈춘 것처럼 보인다). 사람이 폰에서
            // "중간보스 직후 게임이 멈춘다"고 보고한 정지의 후보 경로다.
            //
            // 준비 중인 한 프레임 동안 비어 있을 수 있으므로 즉시 개입하지 않고,
            // 이 상태가 계속되면 로그를 남기고 빠져나간다. 근본 원인은 Core에 있고
            // CODEX가 REQ-058로 다루지만, 그때까지 사람이 런을 버리게 두지 않는다.
            if (options == null || options.Count == 0)
            {
                _emptyOptionsAge += Time.unscaledDeltaTime;
                if (_emptyOptionsAge > EmptyOptionsGrace)
                {
                    _emptyOptionsAge = 0f;
                    // Core의 ChooseReward는 범위를 벗어나면 예외를 던지므로 빈 목록에서는
                    // 빠져나갈 방법이 없다. 조용히 갇히는 것보다 무엇이 막혔는지 알리는
                    // 편이 낫다 — 사람이 스크린샷으로 원인을 넘겨줄 수 있다.
                    Debug.LogError(
                        "[RewardScreen] 보상 후보가 비어 있어 진행이 막혔다 " +
                        $"(kind={_director.RewardKind}). Core가 후보를 만들지 못했다.");
                    if (_titleText != null)
                        _titleText.text = "REWARD ERROR - EMPTY OPTIONS";
                }
                return;
            }
            _emptyOptionsAge = 0f;
            UpdateRerollButton();

            if (!_labelsBuilt)
            {
                _labelsBuilt = true;
                _cursor = 0;

                // 중간보스 뒤의 2택과 스테이지 보스 뒤의 3택을 문면과 배치로 구분한다.
                bool midStage = _director.RewardKind == RewardSelectionKind.MidStage;
                if (_titleText != null)
                    _titleText.text = midStage
                        ? UiText.MidRewardTitle : UiText.RewardTitle;
                LayoutBoxes(Mathf.Clamp(options.Count, 1, MaxOptions));

                for (int i = 0; i < MaxOptions; i++)
                {
                    bool used = i < options.Count;
                    _boxBorders[i].gameObject.SetActive(used);
                    if (!used) continue;
                    // 번호는 키를 눌러 고를 때만 쓸모가 있다 — 탭으로 고르는 폰에서는
                    // 카드 공간을 설명에 쓰는 게 낫다.
                    _boxTexts[i].text = UiPlatform.TouchMode
                        ? DescribeWithCosts(options[i])
                        : $"[{i + 1}]\n{DescribeWithCosts(options[i])}";
                }
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            // 즉시 선택 (1/2/3)
            if (keyboard != null)
            {
                // Choose를 거쳐야 한다 — 직접 ChooseReward를 부르면 2택에서 3을 눌렀을 때
                // 범위를 벗어난 인덱스가 Core로 넘어간다 (중간 보상은 2택이다).
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { Choose(0); return; }
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { Choose(1); return; }
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { Choose(2); return; }
            }

            // 커서 이동 + 확정
            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) move = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame) move = 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame) move = -1;
                if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame) move = 1;
            }
            if (move != 0)
                _cursor = Mathf.Clamp(_cursor + move, 0, options.Count - 1);

            bool confirm = (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            if (confirm)
            {
                _director.ChooseReward(_cursor);
                return;
            }

            for (int i = 0; i < MaxOptions; i++)
                if (_boxBorders[i].gameObject.activeSelf)
                    _boxBorders[i].color = i == _cursor ? UiKit.TextAccent : UiKit.PanelBorder;
        }

        /// <summary>
        /// 보상 카드 문면. 이름만으로는 무엇을 고르는지 알 수 없다는 지적이 있어
        /// ("중간 빌드 선택시 옵션이 어떤건지 잘 모르겠어", 2026-07-30)
        /// 모든 항목에 **효과를 평이한 말로 한 줄** 붙인다. 숫자만 보여 주고 해석을
        /// 플레이어에게 떠넘기지 않는 것이 목적이다.
        /// </summary>
        /// <summary>
        /// 대가(cost) 한 줄. 붉은 색으로 분리해 "이 카드는 거래"임을 명확히 한다 —
        /// 대가가 본문에 섞여 있으면 집고 나서야 알게 되고, 그건 함정이지 결단이 아니다.
        /// </summary>
        static string DescribeCost(in RewardEffectView cost)
        {
            switch (cost.Type)
            {
                case RewardEffectType.ShieldMaxDown:
                    return $"SHIELD CAP -{cost.Amount}";
                case RewardEffectType.MoveSpeedDown:
                    return $"SPEED -{cost.Amount}";
                case RewardEffectType.CapsuleDropWeightDown:
                    return $"CAPSULE DROPS -{cost.Amount}";
                case RewardEffectType.BombMaxDown:
                    return $"BOMB CAP -{cost.Amount}";
                default:
                    return cost.Type.ToString().ToUpperInvariant() + $" -{cost.Amount}";
            }
        }

        /// <summary>본문 + 대가 목록. 대가는 리치 텍스트로 붉게 칠한다.</summary>
        static string DescribeWithCosts(in RewardOption option)
        {
            string body = Describe(option);
            if (option.Costs == null || option.Costs.Count == 0) return body;

            var sb = new System.Text.StringBuilder(body.Length + 48);
            sb.Append(body);
            for (int i = 0; i < option.Costs.Count; i++)
            {
                sb.Append("\n<color=#ff5f52>");
                sb.Append(DescribeCost(option.Costs[i]));
                sb.Append("</color>");
            }
            return sb.ToString();
        }

        static string Describe(in RewardOption option)
        {
            switch (option.Type)
            {
                case RewardType.Capsules:
                    return $"CAPSULE x{option.Amount}\nfills the gauge below";
                case RewardType.SlotLevel:
                    return $"{SlotName(option.Slot)} +{option.Amount}\n{SlotEffect(option.Slot)}";
                case RewardType.RepairHp:
                    // HP가 사라지고 실드 스톡이 유일한 내구도가 됐다 (REQ-040).
                    return $"SHIELD +{option.Amount}\nrestores a shield stock";
                case RewardType.FireRateUp:
                    return $"RAPID FIRE +{option.Amount}\nshoot more often";
                case RewardType.DamageUp:
                    return $"FIREPOWER +{option.Amount}\nmore damage per shot";
                case RewardType.MoveSpeedUp:
                    return $"ENGINE +{option.Amount}\nmove faster, dodge easier";
                case RewardType.Modifier:
                    return ModifierName(option.ModifierId);
                case RewardType.BombStock:
                    return $"BOMB +{option.Amount}\nscreen-clearing charge";
                case RewardType.MissileFamily:
                    return "MISSILE SWAP\nchanges missile behavior";
                case RewardType.OptionFormation:
                    return "FORMATION SWAP\nchanges drone positions";
                case RewardType.PrimaryWeaponFamily:
                    return "WEAPON SWAP\nchanges your main gun";
                default:
                    return option.Type.ToString();
            }
        }

        static string SlotEffect(PowerUpSlot slot)
        {
            switch (slot)
            {
                case PowerUpSlot.MainShot: return "stronger front gun";
                case PowerUpSlot.Missile: return "more missiles";
                case PowerUpSlot.Option: return "another drone follows you";
                case PowerUpSlot.Shield: return "raises shield capacity";
                default: return "";
            }
        }

        static string ModifierName(BattleModifier modifier)
        {
            switch (modifier)
            {
                case BattleModifier.PierceShot: return "PIERCE SHOT\nshots pierce +1 enemy";
                case BattleModifier.Ricochet: return "RICOCHET\nshots bounce to nearby foe";
                case BattleModifier.HomingMissile: return "HOMING MISSILE\nmissiles seek enemies";
                case BattleModifier.KillExplosion: return "KILL EXPLOSION\nkills damage nearby foes";
                default: return modifier.ToString();
            }
        }

        static string SlotName(PowerUpSlot slot)
        {
            switch (slot)
            {
                case PowerUpSlot.MainShot: return "SHOT";
                case PowerUpSlot.Missile: return "MISSILE";
                case PowerUpSlot.Option: return "OPTION";
                case PowerUpSlot.Shield: return "SHIELD";
                default: return slot.ToString();
            }
        }
    }
}
