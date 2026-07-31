using Shmup.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 그라디우스식 파워업 게이지 HUD (REQ-074 7슬롯 대응 재작성).
    ///
    /// 슬롯 수·이름·순서가 GameData 주도가 되면서 씬에 박아 둔 스프라이트 4칸으로는
    /// 감당할 수 없어, 게이지 관측 API(GaugeSlots)를 순회하며 **런타임에 직접 조립**한다.
    /// 데이터가 슬롯을 바꿔도 HUD는 따라온다.
    ///
    /// 알파벳 한 글자와 작은 핍만으로는 레벨 식별이 어렵다는 피드백(2026-07-31)에 따라
    /// 풀네임 + "LV n"/"MAX"를 쓰고, 실드 잔량 숫자를 함께 띄운다.
    /// 여전히 Core 상태를 읽기만 한다 — 게이지 조작은 어디서도 하지 않는다 (CLAUDE.md).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerUpHudView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;

        const float SlotWidth = 76f;
        const float SlotHeight = 34f;
        const float SlotGap = 4f;
        const int MaxPips = 6;

        // UiSkin.Button(헤어라인 셀) 틴트 = 테두리 색, 속은 자동으로 짙어진다.
        // 계기판 언어: 평시는 무채색, 커서만 앰버 — 색이 곧 "지금 여기"다.
        static readonly Color FrameNormal = new Color(0.30f, 0.34f, 0.38f, 0.9f);
        static readonly Color FrameCursor = new Color(1f, 0.70f, 0.11f, 1f);
        static readonly Color FrameMaxed = new Color(0.35f, 0.62f, 0.42f, 0.95f);
        static readonly Color FrameActiveMode = new Color(0.78f, 0.42f, 0.68f, 0.95f);
        static readonly Color32 PipFilled = new Color32(0x9C, 0xD4, 0xFF, 0xFF);
        static readonly Color32 PipEmpty = new Color32(0x22, 0x2C, 0x44, 0xFF);
        static readonly Color32 PipBanking = new Color32(0x4E, 0x7A, 0xB8, 0xFF);
        static readonly Color32 PipBankingNear = new Color32(0x86, 0xB6, 0xF0, 0xFF);

        Canvas _canvas;
        Image[] _frames;
        Text[] _labels;
        Image[][] _pips;
        Text _shieldText;
        int _builtCount = -1;
        int _shownShield = int.MinValue;

        void Start()
        {
            _canvas = UiKit.CreateCanvas("GaugeCanvas", 42);
            _canvas.transform.SetParent(transform, false);

            // 실드 잔량 — 좌하단. "몇 대를 맞아야 죽는지"가 항상 읽혀야 한다.
            _shieldText = UiKit.CreateCornerText(_canvas.transform, _font, "", 12,
                UiKit.TextMain, new Vector2(0f, 0f), new Vector2(8f, 46f),
                TextAnchor.LowerLeft, "ShieldCount");
            UiKit.AddShadow(_shieldText);
        }

        void Build(int count)
        {
            if (_builtCount == count) return;
            _builtCount = count;

            // 재구성 (스테이지 전환 등으로 슬롯 구성이 바뀔 가능성에 대비해 전부 새로)
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = _canvas.transform.GetChild(i);
                if (child.name.StartsWith("GaugeSlot"))
                    Destroy(child.gameObject);
            }

            _frames = new Image[count];
            _labels = new Text[count];
            _pips = new Image[count][];

            float total = count * SlotWidth + (count - 1) * SlotGap;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"GaugeSlot{i}");
                go.transform.SetParent(_canvas.transform, false);
                var frame = go.AddComponent<Image>();
                frame.sprite = UiSkin.Button;
                frame.type = Image.Type.Sliced;
                frame.color = FrameNormal;
                frame.raycastTarget = false;
                var rect = frame.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(
                    -total / 2f + SlotWidth / 2f + i * (SlotWidth + SlotGap), 4f);
                rect.sizeDelta = new Vector2(SlotWidth, SlotHeight);
                _frames[i] = frame;

                var label = UiKit.CreateText(rect, _font, "", 9,
                    UiKit.TextMain, TextAnchor.UpperCenter, "Label");
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.35f);
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = new Vector2(0f, -2f);
                _labels[i] = label;

                _pips[i] = new Image[MaxPips];
                float pipTotal = MaxPips * 9f - 3f;
                for (int p = 0; p < MaxPips; p++)
                {
                    var pipGo = new GameObject($"Pip{p}");
                    pipGo.transform.SetParent(rect, false);
                    var pip = pipGo.AddComponent<Image>();
                    pip.raycastTarget = false;
                    var pipRect = pip.rectTransform;
                    pipRect.anchorMin = pipRect.anchorMax = new Vector2(0.5f, 0f);
                    pipRect.pivot = new Vector2(0.5f, 0f);
                    pipRect.anchoredPosition = new Vector2(
                        -pipTotal / 2f + 3f + p * 9f, 4f);
                    pipRect.sizeDelta = new Vector2(6f, 6f);
                    _pips[i][p] = pip;
                }
            }
        }

        /// <summary>데이터의 nameKey를 HUD 표기로. 모르는 키는 대문자로 그대로 쓴다.</summary>
        static string DisplayName(string nameKey)
        {
            // Core가 함선 게이지의 주무기 축에 붙이는 고정 키 (REQ-082 D).
            // 스위치의 소문자 케이스와 달리 이 키만 혼합 대소문자라 별도 처리한다 —
            // 그대로 두면 "POWERUP.MAINSHOT"이 화면에 노출된다 (로컬 검증에서 발견).
            if (!string.IsNullOrEmpty(nameKey)
                && nameKey.IndexOf("mainShot", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "SHOT";
            switch (nameKey)
            {
                case "speed": return "SPEED";
                case "mainshot":
                case "main shot":
                case "shot": return "SHOT";
                case "missile": return "MISSILE";
                case "double": return "DOUBLE";
                case "laser": return "LASER";
                case "triple": return "TRIPLE";
                case "option": return "OPTION";
                case "shield": return "SHIELD";
                default:
                    return string.IsNullOrEmpty(nameKey)
                        ? "?" : nameKey.ToUpperInvariant();
            }
        }

        /// <summary>
        /// 무기 진화 단계명 (REQ-086, 사람 승인 설계). 모르는 조합은 기본 이름으로.
        /// 실데이터 nameKey는 "Double Shot"/"Triple Shot"/"Laser"처럼 표시용 문자열이라
        /// (REQ-089에서 확인) 소문자 포함 검사로 매칭한다 — 정확 일치는 조용히 폴백돼
        /// 진화명이 아예 안 보였다. 표시명은 슬롯 폭(76px) 안에 들어가게 짧게.
        /// </summary>
        static string EvolutionName(string nameKey, int level)
        {
            string key = (nameKey ?? "").ToLowerInvariant();
            if (key.Contains("double"))
                return level >= 3 ? "CROSS FIRE" : level == 2 ? "TAIL GUARD" : "DOUBLE";
            if (key.Contains("triple"))
                return level >= 3 ? "BURNER" : level == 2 ? "PULSE FAN" : "TRIPLE";
            if (key.Contains("laser"))
                return level >= 3 ? "PRISM BEAM" : level == 2 ? "LANCE" : "LASER";
            return DisplayName(nameKey);
        }

        void LateUpdate()
        {
            var gauge = _director != null ? _director.Gauge : null;
            if (gauge == null || _canvas == null) return;

            UpdateShieldCount();

            int count = gauge.GaugeSlotCount;
            if (count <= 0) return;
            Build(count);

            for (int i = 0; i < count; i++)
            {
                var view = gauge.GetGaugeSlotView(i);
                bool isCursor = gauge.Cursor == i;
                bool maxed = view.Level >= view.MaxLevel;

                _frames[i].color =
                    isCursor ? FrameCursor :
                    view.IsActiveWeaponMode ? FrameActiveMode :
                    maxed ? FrameMaxed : FrameNormal;

                string name = DisplayName(view.NameKey);
                // 무기 모드는 켜짐/진화 단계가 정체다 (REQ-086: maxLevel 3 진화).
                // 활성 상태에서는 현재 진화 단계의 이름을 그대로 보여 준다 —
                // "DOUBLE LV2"보다 "TAIL GUARD"가 무엇이 바뀌었는지 즉시 읽힌다.
                _labels[i].text = view.IsActiveWeaponMode
                    ? $"{EvolutionName(view.NameKey, view.Level)}\n{(view.Level >= view.MaxLevel ? "MAX" : $"MK{view.Level}")}"
                    : view.MaxLevel <= 1 ? name
                    : maxed ? $"{name}\nMAX"
                    : $"{name}\nLV{view.Level}";
                _labels[i].color =
                    isCursor ? new Color(1f, 0.88f, 0.55f, 1f) :
                    view.IsActiveWeaponMode ? new Color(1f, 0.6f, 0.9f, 1f) :
                    maxed ? new Color(0.48f, 0.88f, 0.61f, 1f) : UiKit.TextMain;

                float banked = view.RequiredCapsules > 0
                    ? view.Progress / (float)view.RequiredCapsules : 0f;
                if (view.MaxLevel <= 1)
                {
                    // 무기 모드: 핍 = 필요 캡슐 수, 채움 = 적립량
                    int need = Mathf.Clamp(view.RequiredCapsules, 0, MaxPips);
                    for (int p = 0; p < MaxPips; p++)
                    {
                        bool within = p < need && !view.IsActiveWeaponMode;
                        _pips[i][p].enabled = within;
                        if (!within) continue;
                        _pips[i][p].color = p < view.Progress ? PipBankingNear : PipEmpty;
                    }
                }
                else
                {
                    for (int p = 0; p < MaxPips; p++)
                    {
                        bool within = p < view.MaxLevel;
                        _pips[i][p].enabled = within;
                        if (!within) continue;
                        if (p < view.Level) _pips[i][p].color = PipFilled;
                        else if (p == view.Level && banked > 0.001f)
                            _pips[i][p].color = banked >= 0.5f ? PipBankingNear : PipBanking;
                        else _pips[i][p].color = PipEmpty;
                    }
                }
            }
        }

        void UpdateShieldCount()
        {
            if (_shieldText == null || _director == null) return;
            int shield = _director.ShieldRemaining;
            if (shield == _shownShield) return;
            _shownShield = shield;
            _shieldText.text = $"SHIELD x{shield}";
            // 0 = 다음 피격이 죽음. 숫자만 슬쩍 바꾸면 못 보고 지나간다.
            _shieldText.color = shield <= 0
                ? UiKit.TextDanger
                : shield == 1 ? new Color(1f, 0.75f, 0.35f, 1f) : UiKit.TextMain;
        }
    }
}
