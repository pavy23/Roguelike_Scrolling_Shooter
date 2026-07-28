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
