using Shmup.Core;
using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 스테이지 클리어 보상 3택 오버레이 (REQ-007). RunManager가 AwaitingReward로
    /// 멈춰 있는 동안만 그려지고, 1/2/3 키로 선택을 Core에 전달할 뿐 결정은 내리지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardScreen : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;

        GUIStyle _titleStyle, _optionStyle;

        void Update()
        {
            if (_director == null || !_director.AwaitingReward) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                _director.ChooseReward(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                _director.ChooseReward(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                _director.ChooseReward(2);
        }

        void OnGUI()
        {
            if (_director == null || !_director.AwaitingReward) return;
            var options = _director.RewardOptions;
            if (options == null) return;

            EnsureStyles();
            float width = Screen.width, height = Screen.height;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(
                new Rect(0, height * 0.24f, width, 40f),
                "STAGE CLEAR — CHOOSE REWARD",
                _titleStyle);

            float boxWidth = Mathf.Min(320f, width * 0.26f);
            float boxHeight = 84f;
            float gap = 24f;
            float totalWidth = options.Count * boxWidth + (options.Count - 1) * gap;
            float startX = (width - totalWidth) / 2f;
            float y = height * 0.4f;

            for (int i = 0; i < options.Count; i++)
            {
                var rect = new Rect(startX + i * (boxWidth + gap), y, boxWidth, boxHeight);
                GUI.color = new Color(0.08f, 0.12f, 0.22f, 0.92f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(rect, $"[{i + 1}]\n{Describe(options[i])}", _optionStyle);
            }
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
                default:
                    return option.Type.ToString();
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

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            _optionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
            };
        }
    }
}
