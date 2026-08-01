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

        /// <summary>N스테이지부터 시작 (REQ-096). `--stage=3` 또는 `?dev=1&stage=3`.</summary>
        const string StageKey = "stage";

        /// <summary>피격 무시 (REQ-096). `--god` / `--god=1` 또는 `?dev=1&god=1`.</summary>
        const string GodKey = "god";

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

        // ── 개발용 런 시작 조건 (REQ-096) ──────────────────────────────────────
        //
        // 무적과 스테이지 점프는 시드 고정과 성격이 다르다: 시드는 "같은 런을 다시
        // 본다"지만 이 둘은 런 자체를 바꾼다. 그래서 **DevMode가 아닐 때는 인자를
        // 읽지도 않는다** — 릴리스 빌드에 `?god=1`을 붙여도 아무 일도 일어나지 않는다.
        // (Core는 이 값들이 기본값이 아니면 RunManager.DevFlagsActive를 세우고,
        //  BattleDirector가 그 런의 점수 제출을 치트 런과 똑같이 닫는다.)

        static bool _startStageResolved;
        static int? _startStage;

        /// <summary>시작 스테이지(1부터). 지정이 없거나 개발 모드가 아니면 null.</summary>
        public static int? OverrideStartStage
        {
            get
            {
                if (_startStageResolved) return _startStage;
                _startStageResolved = true;
                _startStage = ResolveStartStage();
                return _startStage;
            }
        }

        static int? ResolveStartStage()
        {
            if (!DevMode) return null;
            if (!TryReadArg(StageKey, out string raw)) return null;
            if (!int.TryParse(raw, NumberStyles.Integer,
                              CultureInfo.InvariantCulture, out int stage))
                return null;
            // 1 미만은 Core가 예외로 거절한다. 오타 하나로 런이 시작조차 못 하면
            // 진단이 어려워지므로 여기서 무시하고 기본값으로 흘려보낸다.
            return stage >= 1 ? stage : (int?)null;
        }

        static bool _godResolved;
        static bool _god;

        /// <summary>피격 무시. 개발 모드가 아니면 항상 false.</summary>
        public static bool GodMode
        {
            get
            {
                if (_godResolved) return _god;
                _godResolved = true;
                _god = DevMode
                    && TryReadArg(GodKey, out string raw)
                    && IsTruthy(raw);
                return _god;
            }
        }

        /// <summary>값 없는 플래그(<c>--god</c>)는 켜짐. 0/false/off/no만 꺼짐으로 읽는다.</summary>
        static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;
            return !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "no", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 실행 인자(<c>--key=value</c> / <c>--key</c>)와 WebGL URL 쿼리(<c>key=value</c>)를
        /// 같은 이름으로 읽는다. 데스크톱은 인자로, 폰 원격 검증은 주소로 — 두 경로를
        /// 하나의 이름으로 묶어야 QA 지시가 플랫폼마다 갈리지 않는다.
        /// </summary>
        static bool TryReadArg(string key, out string value)
        {
            value = null;
            string prefix = "--" + key + "=";
            string flag = "--" + key;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(prefix.Length);
                    return true;
                }
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    value = "1";
                    return true;
                }
            }

            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url)) return false;
            int question = url.IndexOf('?');
            if (question < 0) return false;
            string query = url.Substring(question + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (var pair in query.Split('&'))
            {
                int equals = pair.IndexOf('=');
                string name = equals >= 0 ? pair.Substring(0, equals) : pair;
                if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) continue;
                value = equals >= 0 ? pair.Substring(equals + 1) : "1";
                return true;
            }
            return false;
        }
    }
}
