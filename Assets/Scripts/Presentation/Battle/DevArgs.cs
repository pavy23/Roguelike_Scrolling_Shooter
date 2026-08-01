using System;
using System.Globalization;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 개발용 커맨드라인 인자. 재현 가능한 플레이 테스트를 위해
    /// `RSS.exe --seed=12345` 로 시드를 고정할 수 있다 (에디터 Play에서는 인스펙터 값 사용).
    /// </summary>
    public static class DevArgs
    {
        const string SeedPrefix = "--seed=";
        const string DevFlag = "--dev";
        const string DevQuery = "dev=1";

        static bool _devMode;
        static bool _devModeResolved;

        /// <summary>
        /// 개발자 도구(치트·오버레이)를 허용할 환경인가.
        ///
        /// 스코어보드가 생긴 뒤로 릴리스 빌드에서 치트가 살아 있는 것은 부정행위의 문이다.
        /// 그렇다고 릴리스에서 진단 수단을 완전히 없애면 원격(폰 WebGL) 검증을 할 수 없어서,
        /// **명시적으로 요청했을 때만** 열리는 문을 하나 남긴다:
        ///   에디터 / 개발 빌드 / WebGL URL에 <c>?dev=1</c> / 실행 인자 <c>--dev</c>.
        ///
        /// Unity 제약상 <c>Debug.isDebugBuild</c>·<c>Application.absoluteURL</c>는 정적 필드
        /// 초기화 시점에 부를 수 없으므로 첫 접근(메인 스레드) 때 계산해 캐시한다.
        /// </summary>
        public static bool DevMode
        {
            get
            {
                if (_devModeResolved) return _devMode;
                _devModeResolved = true;
                _devMode = ResolveDevMode();
                return _devMode;
            }
        }

        static bool ResolveDevMode()
        {
            if (Application.isEditor || Debug.isDebugBuild) return true;

            // WebGL 배포판: 주소에 ?dev=1(또는 &dev=1)이 붙어 있을 때만. 쿼리 파서를 두지 않고
            // Contains로 끝낸다 — 이 판정은 보안 경계가 아니라 "실수로 켜지지 않게"가 목적이다.
            string url = Application.absoluteURL;
            if (!string.IsNullOrEmpty(url)
                && url.IndexOf(DevQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, DevFlag, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

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
