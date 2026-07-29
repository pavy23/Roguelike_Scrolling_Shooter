using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 개발용 치트 + 오버레이. 빌드/에디터 공통으로 동작한다.
    ///
    /// - F9: 캡슐 획득(게이지 커서 전진), F10: 활성화 — 캡슐 드롭이 시뮬레이션에
    ///   연결되기 전(REQ-001 이후 교체)까지 HUD를 손으로 시험하기 위한 임시 입력이다.
    ///   이것은 dev 스캐폴딩이지 게임플레이 경로가 아니다.
    /// - 좌상단 오버레이: 현재 시드 / 틱 / 조작 안내. "시드 12345에서 2분 지점" 식의
    ///   재현 가능한 플레이 테스트 리포트를 위해 시드를 항상 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevCheats : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;

        GUIStyle _style;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _director == null || _director.Gauge == null) return;

            if (keyboard.f9Key.wasPressedThisFrame) _director.Gauge.Collect();
            if (keyboard.f10Key.wasPressedThisFrame) _director.Gauge.Activate();
            if (keyboard.f11Key.wasPressedThisFrame) _director.DevFastForward(600);   // 10초 스킵

            if (_director.IsRunOver)
            {
                if (keyboard.enterKey.wasPressedThisFrame) _director.RestartRun();   // 파워업 승계 재출격
                if (keyboard.rKey.wasPressedThisFrame)
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
            }
        }

        GUIStyle _gameOverStyle, _centerBoldStyle, _centerStyle;

        // REQ-009: OnGUI에서 매 프레임 문자열/스타일 할당 금지.
        // 오버레이 문자열은 표시 값이 실제로 바뀐 프레임에만 재조립한다.
        // tick은 60Hz로 변하므로 0.5초(30틱) 단위로 양자화해 재조립 빈도를 낮춘다.
        long _overlayKey = long.MinValue;
        string _overlayText = "";
        int _gameOverRun = int.MinValue;
        string _gameOverScoreText = "", _gameOverStatsText = "";

        void OnGUI()
        {
            if (_director == null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, Screen.height / 40),
                    normal = { textColor = new Color(0.7f, 0.85f, 1f, 0.9f) }
                };
            if (_centerBoldStyle == null)
            {
                _centerBoldStyle = new GUIStyle(_style) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _centerStyle = new GUIStyle(_style) { alignment = TextAnchor.MiddleCenter };
            }

            // 임시 게임오버 표시 — 정식 UI가 생기면 교체
            if (_director.IsRunOver)
            {
                if (_gameOverStyle == null)
                    _gameOverStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.Max(24, Screen.height / 8),
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(1f, 0.3f, 0.3f, 1f) }
                    };
                if (_gameOverRun != _director.RunNumber)
                {
                    _gameOverRun = _director.RunNumber;
                    _gameOverScoreText =
                        $"SCORE  {_director.TotalScore:D8}   (run {_director.RunNumber}, stage {_director.StageIndex})";
                    var stats = _director.RunStats;
                    float accuracy = stats.ShotsFired > 0 ? (float)stats.ShotsHit / stats.ShotsFired * 100f : 0f;
                    _gameOverStatsText =
                        $"KILLS {stats.Kills}   CAPSULES {stats.CapsulesCollected}   ACCURACY {accuracy:0.#}%   SHOTS {stats.ShotsFired}";
                }
                GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "GAME OVER", _gameOverStyle);
                GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, _style.fontSize * 2),
                    _gameOverScoreText, _centerBoldStyle);
                GUI.Label(new Rect(0, Screen.height * 0.655f, Screen.width, _style.fontSize * 2),
                    _gameOverStatsText, _centerStyle);
                GUI.Label(new Rect(0, Screen.height * 0.68f, Screen.width, _style.fontSize * 2),
                    "[Enter] 재출격 (파워업 승계)   [R] 타이틀",
                    _centerStyle);
            }
            else
            {
                _gameOverRun = int.MinValue;
            }

            // 변화 감지 키: hp/shield/스테이지/런 + 0.5초 단위 틱. 시드/난이도는 런 내 고정이라 런 변경에 묻어간다.
            long key = ((long)_director.RunNumber << 48)
                     ^ ((long)_director.StageIndex << 40)
                     ^ ((long)_director.PlayerHp << 32)
                     ^ ((long)_director.ShieldRemaining << 24)
                     ^ (long)(_director.Tick / 30);
            if (key != _overlayKey)
            {
                _overlayKey = key;
                _overlayText =
                    $"run {_director.RunNumber}   stage {_director.StageIndex}   diff {_director.Difficulty}   seed {_director.Seed}   tick {_director.Tick}   hp {_director.PlayerHp}   shield {_director.ShieldRemaining}\n[F9] capsule   [F10] activate   [F11] +10s skip   [ESC] pause   (--seed=N 으로 시드 고정)";
            }
            GUI.Label(new Rect(8, 4, Screen.width - 16, _style.fontSize * 3), _overlayText, _style);
        }
    }
}
