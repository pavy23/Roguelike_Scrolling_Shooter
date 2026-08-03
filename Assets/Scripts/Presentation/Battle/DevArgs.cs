using System;
using System.Globalization;
using Shmup.Core.Simulation;
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

        /// <summary>
        /// 시작 직후 해당 구간까지 무입력 워프 (REQ-124). `--warp=boss` 또는 `?dev=1&warp=boss`.
        /// 값: early | midboss | late | boss. 보스룸 도달에만 매 검증 3~5분(F11 40여 번)이
        /// 들던 것을 없앤다.
        /// </summary>
        const string WarpKey = "warp";

        /// <summary>
        /// 미지의 구역에서 바로 시작 (REQ-123). `--uncharted` / `?dev=1&uncharted=1`.
        /// 거대 보스 2종(레비아탄/브루드마더)은 5바이옴 완주 + 히든 조건 2/3을 채운
        /// 최종 항로에서만 열려 사실상 회귀 검증이 불가능했다 — 실제로 두 보스의
        /// 스프라이트가 엉뚱한 그림으로 나오는 버그가 오래 살아남았다.
        /// Core가 이 런에 DevFlagsActive를 세워 점수 제출을 닫는다.
        /// </summary>
        const string UnchartedKey = "uncharted";

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

        /// <summary>
        /// 이번 런의 시드를 타이틀에서 **사람이 직접 쳐 넣었는가** (스코어보드 공정성).
        ///
        /// 시드를 손으로 고정하면 같은 판을 몇 번이고 연습한 뒤 최고 기록만 올릴 수 있다 —
        /// 랜덤 시드로 한 번에 뚫은 기록과 같은 보드에 서면 보드가 의미를 잃는다.
        /// 그래서 치트 런과 같은 취급으로 제출을 닫는다. 리롤(랜덤)·데일리·리플레이·
        /// 이어하기는 여기에 해당하지 않는다.
        ///
        /// 데일리 표식과 마찬가지로 "시드를 무엇으로 골랐나"라는 타이틀의 선택이라
        /// Presentation 소관이고, 시드와 같은 채널로 넘겨야 두 값이 어긋나지 않는다.
        /// </summary>
        public static bool RuntimeSeeded;

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

        static bool _unchartedResolved;
        static bool _uncharted;

        /// <summary>미지의 구역에서 시작. 개발 모드가 아니면 항상 false.</summary>
        public static bool StartInUncharted
        {
            get
            {
                if (_unchartedResolved) return _uncharted;
                _unchartedResolved = true;
                _uncharted = DevMode
                    && TryReadArg(UnchartedKey, out string raw)
                    && IsTruthy(raw);
                return _uncharted;
            }
        }

        static bool _warpResolved;
        static RunStageSection? _warp;

        /// <summary>
        /// 시작 직후 워프할 구간 (REQ-124). 지정이 없거나 개발 모드가 아니면 null.
        /// 오타는 무시하고 null로 흘려보낸다 — 진단이 어려워지지 않게 경고는 남긴다.
        /// </summary>
        public static RunStageSection? WarpSection
        {
            get
            {
                if (_warpResolved) return _warp;
                _warpResolved = true;
                _warp = ResolveWarp();
                return _warp;
            }
        }

        static RunStageSection? ResolveWarp()
        {
            if (!DevMode) return null;
            if (!TryReadArg(WarpKey, out string raw)) return null;
            if (string.IsNullOrEmpty(raw)) return null;
            switch (raw.ToLowerInvariant())
            {
                case "early":
                case "opening": return RunStageSection.Opening;
                case "midboss":
                case "mid": return RunStageSection.MidBoss;
                case "late":
                case "closing":
                    return StartInUncharted
                        ? RunStageSection.HiddenOpening
                        : RunStageSection.Closing;
                // 미지의 구역은 구간 이름이 따로다(HiddenOpening/HiddenBoss). `warp=boss`를
                // 쓰는 쪽에서 그 차이를 알 이유가 없으니 여기서 흡수한다 — 안 그러면
                // `?uncharted=1&warp=boss`가 영영 도달하지 못하고 상한에서 멈춘다.
                case "boss":
                case "stageboss":
                    return StartInUncharted
                        ? RunStageSection.HiddenBoss
                        : RunStageSection.StageBoss;
                case "hiddenboss": return RunStageSection.HiddenBoss;
                default:
                    Debug.LogWarning(
                        $"[dev] warp={raw} 를 모르겠다. early|midboss|late|boss 중 하나여야 한다.");
                    return null;
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
