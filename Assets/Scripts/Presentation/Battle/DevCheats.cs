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
                     ^ (long)(_director.Tick / 30);
            if (key != _overlayKey)
            {
                _overlayKey = key;
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
                _overlayText =
                    $"run {_director.RunNumber}   stage {_director.StageIndex}{theme}   diff {_director.Difficulty}   seed {_director.Seed}   tick {_director.Tick}   shield {_director.ShieldRemaining}   {(_director.PlayerHp > 0 ? "alive" : "dead")}{section}{ghost}{warship}{DevFlagText}\n[F3] hide   [F7] section look   [F9] capsule   [F10] activate   [F11] +10s skip   [ESC] pause   (--seed=N pins the seed, --stage=N starts there, --god survives)";
            }
            GUI.Label(new Rect(8, 4, Screen.width - 16, _style.fontSize * 3), _overlayText, _style);
        }
    }
}
