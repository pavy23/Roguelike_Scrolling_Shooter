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

            _root.SetActive(false);
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
            if (options == null) return;
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
                        ? Describe(options[i])
                        : $"[{i + 1}]\n{Describe(options[i])}";
                }
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            // 즉시 선택 (1/2/3)
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) { _director.ChooseReward(0); return; }
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) { _director.ChooseReward(1); return; }
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) { _director.ChooseReward(2); return; }
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
                case PowerUpSlot.MainShot: return "MAIN SHOT";
                case PowerUpSlot.Missile: return "MISSILE";
                case PowerUpSlot.Option: return "OPTION";
                case PowerUpSlot.Shield: return "SHIELD";
                default: return slot.ToString();
            }
        }
    }
}
