using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 섹터 계약 선택 화면 (REQ-070) — 다음 스테이지의 조건을 보고 고른다.
    ///
    /// 옛 경로 분기(REQ-053에서 폐지)의 실패 원인은 정보 부재였다 — 카드에 아무것도
    /// 적혀 있지 않아 선택이 주사위였다. 그래서 이 화면의 존재 이유는 단 하나다:
    /// **계약의 모든 효과를 카드에 다 적는 것.** 요약하거나 숨기면 같은 실패를 반복한다.
    ///
    /// RunManager가 AwaitingContract로 멈춰 있는 동안만 표시 — 선택만 Core에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ContractScreen : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        // 목적지 바이옴 프리뷰 (REQ-086 + 사람 지정 "다음 스테이지 모습을 스샷으로"):
        // 런타임 캡처 대신 실제 게임 스프라이트(테마 중경 + 대표 잡몹 + 보스)를
        // 카드에 합성한다 — 픽셀 아트가 그대로 살고 결정론과도 무관하다.
        [SerializeField] string[] _themeIds;
        [SerializeField] Sprite[] _themeBgs;
        [SerializeField] Sprite[] _themeBosses;
        [SerializeField] Sprite[] _themeEnemies;

        const int MaxOptions = 3;
        const float BoxWidth = 168f;
        const float BoxHeight = 216f;
        const float BoxGap = 14f;
        const float PreviewHeight = 54f;

        GameObject _root;
        Text _titleText;
        readonly RectTransform[] _boxRects = new RectTransform[MaxOptions];
        readonly Image[] _boxBorders = new Image[MaxOptions];
        readonly Text[] _boxTitles = new Text[MaxOptions];
        readonly Text[] _boxTexts = new Text[MaxOptions];
        readonly GameObject[] _previewRoots = new GameObject[MaxOptions];
        readonly Image[] _previewBgs = new Image[MaxOptions];
        readonly Image[] _previewBosses = new Image[MaxOptions];
        readonly Text[] _previewLabels = new Text[MaxOptions];
        bool _built;
        int _cursor;
        int _shownCount;

        // 위험 등급 → 카드 테두리 색. 색이 곧 첫인상이라 등급과 1:1로 묶는다.
        static Color TierColor(ContractRiskTier tier)
        {
            switch (tier)
            {
                case ContractRiskTier.Low: return new Color(0.35f, 0.65f, 1f, 1f);
                case ContractRiskTier.High: return new Color(1f, 0.62f, 0.25f, 1f);
                case ContractRiskTier.Extreme: return new Color(1f, 0.32f, 0.28f, 1f);
                default: return UiKit.PanelBorder;   // Safe = 무채색 (표준 항로)
            }
        }

        static string TierLabel(ContractRiskTier tier)
        {
            switch (tier)
            {
                case ContractRiskTier.Low: return "LOW RISK";
                case ContractRiskTier.High: return "HIGH RISK";
                case ContractRiskTier.Extreme: return "!! EXTREME !!";
                default: return "STANDARD";
            }
        }

        void Start()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.CreateCanvas("ContractCanvas", 71);   // RewardCanvas(70) 위
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.6f));
            _titleText = UiKit.CreateCornerText(canvas.transform, _fontBold,
                UiText.ContractTitle, 16, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                TextAnchor.UpperCenter, "Title");

            for (int i = 0; i < MaxOptions; i++)
            {
                var panel = UiKit.CreatePanel(canvas.transform,
                    new Vector2(BoxWidth, BoxHeight), $"Contract{i}");
                _boxRects[i] = panel;
                _boxBorders[i] = panel.GetComponent<Image>();

                // 카드 최상단: 목적지 프리뷰 (테마 배경 + 대표 잡몹 + 보스 실루엣)
                _previewRoots[i] = BuildPreview(panel, i);

                // 프리뷰 아래: 계약 이름 + 위험 등급 (색과 문자 이중 표기 — 색맹 대비)
                _boxTitles[i] = UiKit.CreateText(panel, _fontBold, "", 11,
                    UiKit.TextMain, TextAnchor.UpperCenter, "Name");
                var titleRect = _boxTitles[i].rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -8f - PreviewHeight);
                titleRect.sizeDelta = new Vector2(-12f, 42f);

                // 카드 본문: 효과 전체 목록 (프리뷰·제목 아래 영역)
                _boxTexts[i] = UiKit.CreateTextStretch(panel, _font, "", 10,
                    UiKit.TextMain, TextAnchor.MiddleCenter, 8f, "Effects");
                var effectsRect = _boxTexts[i].rectTransform;
                effectsRect.offsetMax = new Vector2(-8f, -(PreviewHeight + 44f));

                int index = i;   // 클로저가 루프 변수를 잡지 않도록 복사
                UiKit.MakeTappable(_boxBorders[i], () => Choose(index));
            }

            UiKit.CreateCornerText(canvas.transform, _font,
                UiPlatform.TouchMode ? UiText.ChoiceHintsTouch : UiText.ChoiceHints,
                10, UiKit.TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 40f), TextAnchor.MiddleCenter, "Hints");

            _root.SetActive(false);
        }

        void LayoutBoxes(int count)
        {
            if (_shownCount == count) return;
            _shownCount = count;
            float total = count * BoxWidth + (count - 1) * BoxGap;
            for (int i = 0; i < MaxOptions; i++)
            {
                if (_boxRects[i] == null) continue;
                _boxRects[i].anchoredPosition = new Vector2(
                    -total / 2f + BoxWidth / 2f + i * (BoxWidth + BoxGap), -6f);
            }
        }

        /// <summary>
        /// 카드 상단의 목적지 프리뷰 조각. 배경은 테마 중경 스프라이트를 어둡게 깔고,
        /// 왼쪽에 대표 잡몹·오른쪽에 보스를 세운다. 하단 라벨이 "NEXT: 테마"를 박는다.
        /// </summary>
        GameObject BuildPreview(RectTransform panel, int index)
        {
            var root = new GameObject("Preview");
            root.transform.SetParent(panel, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -4f);
            rootRect.sizeDelta = new Vector2(-8f, PreviewHeight);

            // 카드 밖으로 새지 않게 잘라 낸다. 보스를 크게 걸어 **일부만** 보이게
            // 하려면 클리핑이 먼저 있어야 한다 — 없으면 카드 밖으로 삐져나온다.
            root.AddComponent<RectMask2D>();

            var bg = new GameObject("Bg").AddComponent<Image>();
            bg.transform.SetParent(rootRect, false);
            bg.raycastTarget = false;
            // 프리뷰는 창밖 풍경 — 카드 잉크보다 살짝 어둡게 눌러 텍스트와 싸우지 않게
            // 원경 그림을 그대로 보여 준다. 예전에는 0.62까지 눌렀는데, 그건 중경
            // 조각 몇 개를 배경처럼 보이게 하려던 보정이었다. 진짜 장면이 들어온
            // 지금은 살짝만 눌러 라벨 가독성만 지킨다.
            bg.color = new Color(0.86f, 0.88f, 0.92f, 1f);
            bg.preserveAspect = false;
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            _previewBgs[index] = bg;

            // 잡몹 아이콘은 뺐다. 배경·보스·라벨까지 넣으면 54px 높이 안에서
            // 셋이 서로를 밀어낸다 — 사람이 원한 것은 "배경과 보스 일부"다.

            var boss = new GameObject("Boss").AddComponent<Image>();
            boss.transform.SetParent(rootRect, false);
            boss.raycastTarget = false;
            boss.preserveAspect = true;
            var bossRect = boss.rectTransform;
            // 오른쪽 끝에 **크게** 걸어 일부만 보이게 한다. 카드 안에 통째로 넣으면
            // 44px짜리 조그만 도장이 되어 무엇인지 읽히지 않았다. 잘려 있는 편이
            // 크기도 전해지고 "다 보여 주지 않는다"는 인상도 남는다.
            bossRect.anchorMin = bossRect.anchorMax = new Vector2(0.86f, 0.5f);
            bossRect.sizeDelta = new Vector2(96f, 88f);
            _previewBosses[index] = boss;

            var label = UiKit.CreateText(rootRect, _fontBold, "", 9,
                UiKit.TextAccent, TextAnchor.LowerLeft, "Dest");
            UiKit.AddShadow(label, 1f);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(4f, 2f);
            labelRect.offsetMax = new Vector2(-4f, 0f);
            _previewLabels[index] = label;
            return root;
        }

        int ThemeIndex(string themeId)
        {
            if (_themeIds == null || string.IsNullOrEmpty(themeId)) return -1;
            for (int i = 0; i < _themeIds.Length; i++)
                if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        static Sprite At(Sprite[] array, int index)
        {
            return array != null && index >= 0 && index < array.Length ? array[index] : null;
        }

        /// <summary>카드 하나의 프리뷰를 계약 목적지에 맞춘다. 테마가 없으면 통째로 숨긴다.</summary>
        void RefreshPreview(int i, ContractOption contract)
        {
            bool nextStage =
                contract.DestinationKind == ContractDestinationKind.NextStage;
            int theme = nextStage ? ThemeIndex(contract.DestinationThemeId) : -1;
            bool show = theme >= 0 && At(_themeBgs, theme) != null;
            if (_previewRoots[i].activeSelf != show)
                _previewRoots[i].SetActive(show);
            // 프리뷰가 없는 카드(귀환·미지의 구역·미지 테마)는 기존 레이아웃으로 복귀
            _boxTitles[i].rectTransform.anchoredPosition =
                new Vector2(0f, show ? -8f - PreviewHeight : -8f);
            _boxTexts[i].rectTransform.offsetMax =
                new Vector2(-8f, show ? -(PreviewHeight + 44f) : -44f);
            if (!show) return;

            _previewBgs[i].sprite = At(_themeBgs, theme);
            _previewBosses[i].sprite = At(_themeBosses, theme);
            _previewBosses[i].enabled = _previewBosses[i].sprite != null;
            _previewLabels[i].text =
                $"NEXT: {contract.DestinationThemeId.ToUpperInvariant()}";
        }

        void Choose(int index)
        {
            if (_director == null || !_director.AwaitingContract) return;
            var options = _director.ContractOptions;
            if (options == null || index < 0 || index >= options.Count) return;
            _director.ChooseContract(index);
        }

        /// <summary>
        /// 효과 하나를 "무엇이 어떻게"의 한 줄로. 유리수는 ×배율로, 델타는 ±로.
        /// 픽셀 폰트가 ASCII 위주라 라벨은 영문 대문자를 쓴다 (HUD 관례와 동일).
        /// </summary>
        static string DescribeEffect(in ContractEffectView e)
        {
            float ratio = e.Denominator != 0 ? (float)e.Numerator / e.Denominator : 1f;
            switch (e.Type)
            {
                case ContractEffectType.EnemyDensityMultiplier:
                    return $"ENEMIES x{ratio:0.##}";
                case ContractEffectType.CapsuleDropMultiplier:
                    return $"CAPSULES x{ratio:0.##}";
                case ContractEffectType.BombDropMultiplier:
                    return $"BOMB DROPS x{ratio:0.##}";
                case ContractEffectType.GuaranteedBombDrop:
                    return "BOMB GUARANTEED";
                case ContractEffectType.GimmickIntensityMultiplier:
                    return $"HAZARDS x{ratio:0.##}";
                case ContractEffectType.RewardOptionCountDelta:
                    return e.Numerator >= 0
                        ? $"REWARD CARDS +{e.Numerator}"
                        : $"REWARD CARDS {e.Numerator}";
                case ContractEffectType.ScoreMultiplier:
                    return $"SCORE x{ratio:0.##}";
                // 봉인 계약 (REQ-095). 무엇이 막히는지가 카드의 정체다 —
                // "게이지를 못 쓴다"는 다른 어떤 배율보다 먼저 읽혀야 한다.
                case ContractEffectType.GaugeActivationBanned:
                    return "NO GAUGE";
                case ContractEffectType.OptionActivationBanned:
                    return "NO OPTION";
                case ContractEffectType.ShieldActivationBanned:
                    return "NO SHIELD";
                default:
                    // 모르는 효과도 절대 빈 줄로 두지 않는다 — 빈 줄은 "카드가 고장났다"로
                    // 읽히고, 이 화면의 존재 이유(효과를 다 적는다)와 정면으로 어긋난다.
                    return e.Denominator != 1
                        ? $"{e.Type.ToString().ToUpperInvariant()} x{ratio:0.##}"
                        : e.Type.ToString().ToUpperInvariant();
            }
        }

        static bool IsBan(ContractEffectType type)
        {
            return type == ContractEffectType.GaugeActivationBanned
                || type == ContractEffectType.OptionActivationBanned
                || type == ContractEffectType.ShieldActivationBanned;
        }

        /// <summary>
        /// 카드 본문의 효과 줄 전체. 봉인 계약은 배율이 곧 봉인의 대가라 봉인 줄에
        /// 배율을 붙여 한 줄로 읽히게 하고("NO GAUGE x1.6"), 같은 숫자를 반복하는
        /// 별도 SCORE 줄은 접는다 — 168px 카드에서 중복은 소음이다.
        /// 봉인이 없는 계약은 기존 그대로 한 효과 = 한 줄이다.
        /// </summary>
        static void AppendEffectLines(System.Text.StringBuilder sb, ContractOption contract)
        {
            var effects = contract.Effects;
            if (effects == null || effects.Count == 0) return;

            int scoreNumerator = contract.ScoreMultiplierNumerator;
            int scoreDenominator = contract.ScoreMultiplierDenominator;
            bool hasScore = scoreDenominator != 0 && scoreNumerator != scoreDenominator;
            bool scorePending = hasScore && HasBan(effects);
            string scoreSuffix = hasScore
                ? $" x{(float)scoreNumerator / scoreDenominator:0.##}" : "";

            for (int k = 0; k < effects.Count; k++)
            {
                var effect = effects[k];
                // 봉인 줄이 배율을 이미 싣고 있으면 SCORE 줄은 생략한다.
                if (scorePending && effect.Type == ContractEffectType.ScoreMultiplier)
                    continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(DescribeEffect(in effect));
                // 배율은 첫 봉인 줄에만. 봉인이 둘이어도 같은 숫자를 두 번 적지 않는다.
                if (scorePending && IsBan(effect.Type))
                {
                    sb.Append(scoreSuffix);
                    scorePending = false;
                }
            }
        }

        static bool HasBan(System.Collections.Generic.IReadOnlyList<ContractEffectView> effects)
        {
            for (int k = 0; k < effects.Count; k++)
                if (IsBan(effects[k].Type)) return true;
            return false;
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            bool awaiting = _director.AwaitingContract;
            if (_root.activeSelf != awaiting)
                _root.SetActive(awaiting);
            if (!awaiting)
            {
                _built = false;
                return;
            }

            var options = _director.ContractOptions;
            if (options == null || options.Count == 0) return;   // Core가 빈 후보를 보장하지 않으므로 방어만

            if (!_built)
            {
                _built = true;
                _cursor = 0;
                LayoutBoxes(Mathf.Clamp(options.Count, 1, MaxOptions));
                for (int i = 0; i < MaxOptions; i++)
                {
                    bool used = i < options.Count;
                    _boxRects[i].gameObject.SetActive(used);
                    if (!used) continue;

                    var contract = options[i];
                    var tierColor = TierColor(contract.RiskTier);
                    RefreshPreview(i, contract);
                    // 최종 화면(REQ-072): 목적지가 곧 카드의 정체다. 귀환은 "여기서
                    // 끝낸다", 미지의 구역은 "더 간다" — 등급 라벨보다 앞세운다.
                    string headline =
                        contract.DestinationKind == ContractDestinationKind.EndRun
                            ? "RETURN HOME"
                            : contract.DestinationKind == ContractDestinationKind.Uncharted
                                ? "THE UNCHARTED"
                                : ContractName(contract.Id);
                    _boxTitles[i].text = $"{headline}\n{TierLabel(contract.RiskTier)}";
                    _boxTitles[i].color = tierColor;

                    // 효과 전체를 나열한다. 표준 항로(효과 없음)는 그것대로 명시 —
                    // 빈 카드는 "버그인가?"로 읽힌다.
                    if (contract.DestinationKind == ContractDestinationKind.EndRun)
                    {
                        _boxTexts[i].text = "END THE RUN HERE\nBANK YOUR SCORE";
                        _boxTexts[i].color = UiKit.TextDim;
                    }
                    else if (contract.DestinationKind == ContractDestinationKind.Uncharted)
                    {
                        var sbU = new System.Text.StringBuilder(96);
                        sbU.Append("ENTER THE HIDDEN SECTOR\nFACE THE COLOSSUS");
                        AppendEffectLines(sbU, contract);
                        _boxTexts[i].text = sbU.ToString();
                        _boxTexts[i].color = UiKit.TextMain;
                    }
                    else if (contract.Effects == null || contract.Effects.Count == 0)
                    {
                        _boxTexts[i].text = "NO MODIFIERS\nA CLEAN RUN";
                        _boxTexts[i].color = UiKit.TextDim;
                    }
                    else
                    {
                        var sb = new System.Text.StringBuilder(96);
                        AppendEffectLines(sb, contract);
                        _boxTexts[i].text = sb.ToString();
                        _boxTexts[i].color = UiKit.TextMain;
                    }
                }
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { Choose(0); return; }
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { Choose(1); return; }
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { Choose(2); return; }
            }

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) move = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) move = 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) move = -1;
                if (gamepad.dpad.right.wasPressedThisFrame) move = 1;
            }
            if (move != 0)
                _cursor = Mathf.Clamp(_cursor + move, 0, options.Count - 1);

            bool confirm =
                (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            if (confirm) { Choose(_cursor); return; }

            // 커서 강조는 등급 색을 밝혀서 — 커서와 등급이 각각 다른 채널이면 혼란스럽다.
            for (int i = 0; i < options.Count && i < MaxOptions; i++)
            {
                var baseColor = TierColor(options[i].RiskTier);
                _boxBorders[i].color = i == _cursor
                    ? Color.Lerp(baseColor, Color.white, 0.35f)
                    : baseColor;
            }
        }

        /// <summary>
        /// 계약 id → 표시 이름. 데이터에 표시명 필드가 없어 id를 규약으로 변환한다
        /// (contract_danger_run → DANGER RUN). GROK 데이터가 표시명을 갖게 되면 교체.
        /// </summary>
        static string ContractName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "SECTOR";
            string s = id;
            if (s.StartsWith("contract_")) s = s.Substring("contract_".Length);
            return s.Replace('_', ' ').ToUpperInvariant();
        }
    }
}
