using System;
using System.Globalization;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 개발용 커맨드라인 인자. 재현 가능한 플레이 테스트를 위해
    /// `RSS.exe --seed=12345` 로 시드를 고정할 수 있다 (에디터 Play에서는 인스펙터 값 사용).
    /// </summary>
    public static class DevArgs
    {
        const string SeedPrefix = "--seed=";

        /// <summary>타이틀 화면에서 고른 이번 런의 시드. 커맨드라인 --seed가 있으면 그쪽이 우선.</summary>
        public static long? RuntimeSeed;

        /// <summary>
        /// 이번 런이 데일리 시드로 시작됐는가 (스코어보드 daily 보드 분리용).
        /// 데일리 여부는 "시드를 무엇으로 골랐나"라는 타이틀의 선택이라 Presentation 소관이다 —
        /// Core는 DailySeed 해시만 순수 함수로 제공하고 런이 데일리인지는 모른다.
        /// 시드와 같은 채널로 넘겨야 두 값이 어긋나지 않는다.
        /// </summary>
        public static bool RuntimeDaily;

        public static long? OverrideSeed
        {
            get
            {
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (!arg.StartsWith(SeedPrefix, StringComparison.Ordinal)) continue;
                    if (long.TryParse(arg.Substring(SeedPrefix.Length), NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out long seed))
                        return seed;
                }
                return null;
            }
        }
    }
}
