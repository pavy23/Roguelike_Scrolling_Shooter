using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 난이도 선택 상태 (REQ-020 Presentation 몫). PlayerPrefs 저장.
    /// 배율 값은 잠정(§7) — GROK/플레이 피드백으로 확정 예정.
    /// </summary>
    public static class DifficultySelect
    {
        public const string PrefKey = "rss.difficulty";

        public static int Index
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 0, 2);
            set => PlayerPrefs.SetInt(PrefKey, Mathf.Clamp(value, 0, 2));
        }

        public static string Label => Index == 0 ? "EASY" : Index == 2 ? "HARD" : "NORMAL";

        /// <summary>적 HP 배율 (잠정 §7): EASY 3/4, NORMAL 1/1, HARD 5/4.</summary>
        public static void GetMultiplier(out int numerator, out int denominator)
        {
            switch (Index)
            {
                case 0: numerator = 3; denominator = 4; break;
                case 2: numerator = 5; denominator = 4; break;
                default: numerator = 1; denominator = 1; break;
            }
        }
    }
}
