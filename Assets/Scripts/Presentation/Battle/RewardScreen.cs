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
        readonly Image[] _boxBorders = new Image[MaxOptions];
        readonly Text[] _boxTexts = new Text[MaxOptions];
        bool _labelsBuilt;
        int _cursor;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("RewardCanvas", 70);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.55f));
            UiKit.CreateCornerText(canvas.transform, _fontBold, UiText.RewardTitle, 16,
                UiKit.TextAccent, new Vector2(0.5f, 1f), new Vector2(0f, -86f),
                TextAnchor.UpperCenter, "Title");

            const float boxWidth = 150f, boxHeight = 64f, gap = 14f;
            float totalWidth = MaxOptions * boxWidth + (MaxOptions - 1) * gap;
            for (int i = 0; i < MaxOptions; i++)
            {
                var panel = UiKit.CreatePanel(canvas.transform, new Vector2(boxWidth, boxHeight), $"Option{i}");
                panel.anchoredPosition = new Vector2(
                    -totalWidth / 2f + boxWidth / 2f + i * (boxWidth + gap), -10f);
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
                for (int i = 0; i < MaxOptions; i++)
                {
                    bool used = i < options.Count;
                    _boxBorders[i].gameObject.SetActive(used);
                    if (used)
                        _boxTexts[i].text = $"[{i + 1}]\n{Describe(options[i])}";
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

        static string Describe(in RewardOption option)
        {
            switch (option.Type)
            {
                case RewardType.Capsules:
                    return $"CAPSULE x{option.Amount}";
                case RewardType.SlotLevel:
                    return $"{SlotName(option.Slot)} +{option.Amount}";
                case RewardType.RepairHp:
                    return $"HULL +{option.Amount}";
                case RewardType.FireRateUp:
                    return $"FIRE RATE +{option.Amount}";
                case RewardType.DamageUp:
                    return $"DAMAGE +{option.Amount}";
                case RewardType.MoveSpeedUp:
                    return $"ENGINE +{option.Amount}";
                case RewardType.Modifier:
                    return ModifierName(option.ModifierId);
                default:
                    return option.Type.ToString();
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
