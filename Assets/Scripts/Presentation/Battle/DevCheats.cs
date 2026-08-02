using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 개발용 치트 + 오버레이. **<see cref="DevArgs.DevMode"/>일 때만 존재한다.**
    ///
    /// - F9: 캡슐 획득(게이지 커서 전진), F10: 활성화 — 캡슐 드롭이 시뮬레이션에
    ///   연결되기 전(REQ-001 이후 교체)까지 HUD를 손으로 시험하기 위한 임시 입력이다.
    ///   이것은 dev 스캐폴딩이지 게임플레이 경로가 아니다.
    /// - 좌상단 오버레이: 현재 시드 / 틱 / 조작 안내. "시드 12345에서 2분 지점" 식의
    ///   재현 가능한 플레이 테스트 리포트를 위해 시드를 항상 표시한다.
    ///
    /// 릴리스에서는 컴포넌트 자체를 꺼 버린다 — 글로벌 스코어보드가 생긴 뒤로 F9/F10/F11이
    /// 그대로 살아 있으면 보드를 오염시키는 문이 된다. 개발 모드에서 치트를 실제로 쓰면
    /// <see cref="BattleDirector.CheatUsed"/>를 세워 그 런의 제출을 스스로 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevCheats : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] SectionThemeDirector _sectionThemes;

        /// <summary>St3 거대 전함(REQ-110/111) 파츠 그룹 상태 — 없으면 표시하지 않는다.</summary>
        [SerializeField] WarshipView _warship;

        /// <summary>
        /// St4 번개룡 체인(REQ-115b). 이 미니언은 Core가 Enemies가 아니라 별도 관측으로
        /// 노출해서 뷰가 없던 동안 **화면에 없는데 데미지는 주는** 상태였다. 테스터가
        /// "스프라이트를 못 찾겠다"고 두 번 보고한 그 상태를 다시 오진하지 않도록,
        /// Core가 체인을 몇 기 굴리고 뷰가 몇 절을 그리는지를 나란히 적는다 —
        /// 두 숫자가 어긋나면 그때가 뷰 문제고, 둘 다 0이면 Core가 안 소환한 것이다.
        /// </summary>
        [SerializeField] SegmentChainView _chains;

        /// <summary>F3으로 오버레이 표시 전환. 개발 모드에서만 의미가 있고 기본 표시다.</summary>
        static bool _overlayVisible;
        static bool _overlayDefaultResolved;

        GUIStyle _style;

        void Awake()
        {
            // 릴리스: 치트도 오버레이도 없다. 컴포넌트를 끄면 Update/OnGUI가 아예 돌지 않는다.
            if (!DevArgs.DevMode)
            {
                enabled = false;
                return;
            }
            // Debug.isDebugBuild는 필드 초기화 시점에 호출할 수 없다 (Unity 제약)
            if (_overlayDefaultResolved) return;
            _overlayDefaultResolved = true;
            _overlayVisible = true;
        }

        void Update()
        {
            if (!DevArgs.DevMode) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F3은 표시 전환일 뿐이라 치트가 아니다 — 제출을 막지 않는다.
            if (keyboard.f3Key.wasPressedThisFrame) _overlayVisible = !_overlayVisible;

            // F7: 구간 배경 룩(전반/중간보스/후반/보스) 순환 미리보기. 순수 표현이라
            // 역시 치트가 아니다 — 20룩을 실제로 진행하지 않고 눈으로 확인하려면 필요하다.
            if (keyboard.f7Key.wasPressedThisFrame && _sectionThemes != null)
                _sectionThemes.DevCycleSectionPreview();

            if (_director == null || _director.Gauge == null) return;

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                _director.Gauge.Collect();
                _director.MarkCheatUsed();
            }
            if (keyboard.f10Key.wasPressedThisFrame)
            {
                _director.Gauge.Activate();
                _director.MarkCheatUsed();
            }
            if (keyboard.f11Key.wasPressedThisFrame)
            {
                _director.DevFastForward(600);   // 10초 스킵
                _director.MarkCheatUsed();
            }
            // 게임오버 입력/표시는 GameOverScreen(UGUI)으로 이관됨
        }

        // REQ-009: OnGUI에서 매 프레임 문자열/스타일 할당 금지.
        // 오버레이 문자열은 표시 값이 실제로 바뀐 프레임에만 재조립한다.
        // tick은 60Hz로 변하므로 0.5초(30틱) 단위로 양자화해 재조립 빈도를 낮춘다.
        long _overlayKey = long.MinValue;

        // St4 체인 관측값 (REQ-115b). XOR 키와 달리 정확 비교라 상쇄되지 않는다.
        int _lastChainSegments = -1;
        int _lastChainDrawn = -1;

        // 플레이어 뷰 진단 (build25~29 "기체 소실"). 0.1u 단위로 양자화해 들고 있고
        // 표시도 같은 양자화 값이라, 정지 중에는 문자열을 다시 만들지 않는다.
        int _lastPlayerCoreY10 = int.MinValue;
        int _lastPlayerViewY10 = int.MinValue;
        bool _lastPlayerRendererOn;
        int _lastPlayerSortingOrder = int.MinValue;
        string _overlayText = "";

        /// <summary>
        /// 켜져 있는 개발 플래그 표시 (REQ-096). 런 도중 바뀌지 않는 값이라 한 번만
        /// 만든다. 무적으로 죽지 않는 것을 "적 탄이 안 맞네?" 같은 버그로 오독하는
        /// 사고가 실제로 나므로, 켜져 있으면 화면이 그렇다고 말해 줘야 한다.
        /// </summary>
        string _devFlagText;

        string DevFlagText
        {
            get
            {
                if (_devFlagText != null) return _devFlagText;
                var sb = new System.Text.StringBuilder(32);
                if (DevArgs.GodMode) sb.Append("   [GOD]");
                if (DevArgs.OverrideStartStage.HasValue)
                    sb.Append($"   [START {DevArgs.OverrideStartStage.Value}]");
                if (sb.Length > 0) sb.Append("   NO SUBMIT");
                _devFlagText = sb.ToString();
                return _devFlagText;
            }
        }

        void OnGUI()
        {
            if (_director == null || !_overlayVisible || !DevArgs.DevMode) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, Screen.height / 40),
                    normal = { textColor = new Color(0.7f, 0.85f, 1f, 0.9f) }
                };

            // 변화 감지 키: hp/shield/스테이지/런 + 0.5초 단위 틱. 시드/난이도는 런 내 고정이라 런 변경에 묻어간다.
            // 고스트 두 상태(기록 보유 / 활성)도 키에 넣는다 — 다른 값이 그대로면
            // 합류 순간에 문자열이 재조립되지 않아 표시가 최대 0.5초 늦는다.
            bool ghostLive = _director.GhostActive;
            bool ghostRec = _director.HasGhostRecording;
            // 전함(REQ-110/111): 그룹이 넘어가는 프레임과 포탑 잔량은 0.5초 양자화에
            // 묻히면 안 된다 — 함수 개막 밀도가 이 숫자로 정해지므로 실측이 필요하다.
            bool warshipOn = _warship != null && _warship.Active;
            int warshipGroup = warshipOn ? _warship.FocusGroupIndex : 0;
            int warshipTurrets = warshipOn ? _warship.AttritionAlive : 0;
            // 체인은 소환/격파가 몇 초 단위로 오가므로 0.5초 양자화에 묻히면 안 된다.
            // **Core 쪽 숫자는 뷰를 거치지 않고 director에서 직접 읽는다** — 뷰가 죽어
            // 있어도 Core가 굴리는 절 수는 그대로 나와야 오진이 안 난다.
            var coreChains = _director.SegmentChains;
            int chainSegments = coreChains != null ? coreChains.Count : 0;
            int chainDrawn = _chains != null ? _chains.VisibleSegmentCount : 0;
            long key = ((long)_director.RunNumber << 48)
                     ^ ((long)_director.StageIndex << 40)
                     ^ ((long)_director.PlayerHp << 32)
                     ^ ((long)_director.ShieldRemaining << 24)
                     ^ ((long)(int)_director.StageSection << 20)
                     ^ ((ghostLive ? 1L : 0L) << 19)
                     ^ ((ghostRec ? 1L : 0L) << 18)
                     ^ ((warshipOn ? 1L : 0L) << 17)
                     ^ ((long)warshipGroup << 15)
                     ^ ((long)warshipTurrets << 12)
                     // 상위 비트는 비어 있다 (run 번호는 48~53만 쓴다) — 체인 두 값이
                     // 다른 필드와 XOR로 상쇄돼 갱신을 놓치지 않게 여기에 얹는다.
                     ^ (long)(_director.Tick / 30);
            // 플레이어 뷰 판별식 (build25~29 "기체가 사라진다"). 소실 프레임에서
            // py 정상 + ty 이탈 → 뷰 동기 경로, ren false → 렌더러를 끈 범인,
            // 넷 다 정상 → 기체는 거기 있고 무언가가 덮고 있는 것이다.
            int playerCoreY10 = Mathf.RoundToInt(_director.PlayerCoreY * 10f);
            int playerViewY10 = Mathf.RoundToInt(_director.PlayerViewY * 10f);
            bool playerRendererOn = _director.PlayerRendererEnabled;
            int playerSortingOrder = _director.PlayerSortingOrder;

            // 체인 두 값은 XOR 키에 접지 않는다 — 서로 같은 값일 때가 대부분이라
            // 인접 비트에 얹으면 상쇄돼 변화가 통째로 사라진다. 따로 비교한다.
            // 플레이어 뷰 네 값도 같은 이유로 따로 비교한다.
            if (key != _overlayKey
                || chainSegments != _lastChainSegments
                || chainDrawn != _lastChainDrawn
                || playerCoreY10 != _lastPlayerCoreY10
                || playerViewY10 != _lastPlayerViewY10
                || playerRendererOn != _lastPlayerRendererOn
                || playerSortingOrder != _lastPlayerSortingOrder)
            {
                _overlayKey = key;
                _lastChainSegments = chainSegments;
                _lastChainDrawn = chainDrawn;
                _lastPlayerCoreY10 = playerCoreY10;
                _lastPlayerViewY10 = playerViewY10;
                _lastPlayerRendererOn = playerRendererOn;
                _lastPlayerSortingOrder = playerSortingOrder;
                string section = _sectionThemes != null
                    ? $"   sect {_sectionThemes.CurrentSection}/{_sectionThemes.DevPreviewLabel}"
                    : "";
                // 타임루프 고스트 (REQ-109). F7 순환으로는 미리 볼 수 없다 — 재생에
                // St1 입력 기록과 최종 구간 진입이 둘 다 필요해서 런 상태가 없으면
                // 만들 수가 없다. 그래서 프리뷰 대신 **지금 자격이 있는지**를 적는다:
                // rec = St1 기록 보유(최종 구간에 닿으면 뜬다), live = 지금 재생 중.
                string ghost = ghostLive ? "   ghost:live" : ghostRec ? "   ghost:rec" : "";
                // 테마 id. **스테이지 번호로는 테마를 알 수 없다** — Core가 런 시드로
                // St2~St4 테마를 셔플하므로(SegmentStageGenerator.BuildThemeOrder) St2가
                // hive라는 보장이 없다. 이걸 안 적어서 build25 QA가 "hive에 nebula 아트가
                // 뜬다"고 오진했다. 배경/파티클/구간 룩이 전부 이 값으로 갈리니 반드시 보인다.
                string theme = string.IsNullOrEmpty(_director.CurrentThemeId)
                    ? "   theme -"
                    : $"   theme {_director.CurrentThemeId}";
                // 전함 한 조각: 열린 그룹(1=함미 2=함체 3=함수)과 남은 포탑 수.
                // 포탑을 몇 개 남겼는지가 함수 개막 탄막 밀도를 정한다 (REQ-111).
                string warship = warshipOn
                    ? $"   warship {_warship.FocusGroupId} {warshipGroup + 1}/{_warship.GroupCount}   turret {warshipTurrets}/{_warship.AttritionTotal}"
                    : "";
                // St4 번개룡: Core 체인 기수 / 뷰가 그린 절 수. 0/0이면 아직 안 나왔고,
                // n/0이면 뷰가 죽은 것이다 (build26/27의 "안 보인다"가 바로 이 상태였다).
                string chains = chainSegments > 0 || chainDrawn > 0
                    ? $"   chain {chainSegments}seg/{chainDrawn}drawn"
                    : "";
                // 기체 한 조각: Core Y / 뷰 Y / 렌더러 on / 정렬 순서.
                string player =
                    $"   py {playerCoreY10 / 10f:0.0} ty {playerViewY10 / 10f:0.0}"
                    + $" ren {(playerRendererOn ? 1 : 0)} sc {playerSortingOrder}";
                _overlayText =
                    $"run {_director.RunNumber}   stage {_director.StageIndex}{theme}   diff {_director.Difficulty}   seed {_director.Seed}   tick {_director.Tick}   shield {_director.ShieldRemaining}   {(_director.PlayerHp > 0 ? "alive" : "dead")}{player}{section}{ghost}{warship}{chains}{DevFlagText}\n[F3] hide   [F7] section look   [F9] capsule   [F10] activate   [F11] +10s skip   [ESC] pause   (--seed=N pins the seed, --stage=N starts there, --warp=boss jumps, --god survives)";
            }
            GUI.Label(new Rect(8, 4, Screen.width - 16, _style.fontSize * 3), _overlayText, _style);
        }
    }
}
