using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 정식 게임오버 화면 (UGUI, DevCheats 임시 표시 대체).
    /// [Enter]/패드 South = 재출격(파워업 승계), [R]/패드 East = 타이틀.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverScreen : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        GameObject _root;
        Text _titleText, _scoreText, _statsText, _extraText, _modifierText, _hintsText;
        Button _retryButton;
        Image _dim;
        int _shownRun = int.MinValue;
        bool _shownCleared;

        /// <summary>스코어보드 제출 상태. Sending/Done에서는 버튼이 다시 눌리지 않는다.</summary>
        enum SubmitState { Idle, Sending, Done }

        const string SubmitIdleLabel = "SUBMIT SCORE";

        /// <summary>데일리는 전원이 같은 시드로 겨루는 별도 보드다 — 어디에 올리는지 라벨로 밝힌다.</summary>
        const string SubmitDailyLabel = "SUBMIT DAILY SCORE";

        /// <summary>치트를 쓴 런. 개발 검증 주행이 보드에 섞이지 않게 제출 자체를 닫는다.</summary>
        const string SubmitCheatLabel = "DEV RUN - NO SUBMIT";

        Button _submitButton;
        Text _submitLabel;
        SubmitState _submitState;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("GameOverCanvas", 90);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            // 제출 버튼 한 줄이 늘어난 만큼 패널을 키운다 (요약 텍스트는 전부 상단 앵커라
            // 위치가 그대로고, 힌트는 하단 앵커라 바닥에 붙어 따라 내려간다).
            var panel = UiKit.CreatePanel(canvas.transform, new Vector2(400f, 224f));

            _titleText = UiKit.CreateCornerText(panel, _fontBold, UiText.GameOverTitle, 22, UiKit.TextDanger,
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), TextAnchor.UpperCenter, "Title");
            _scoreText = UiKit.CreateCornerText(panel, _fontBold, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), TextAnchor.UpperCenter, "Score");
            _statsText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -74f), TextAnchor.UpperCenter, "Stats");
            _extraText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), TextAnchor.UpperCenter, "Extra");
            _modifierText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), TextAnchor.UpperCenter, "Modifiers");
            _hintsText = UiKit.CreateCornerText(panel, _font,
                UiText.GameOverHints, 11, UiKit.TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 16f), TextAnchor.LowerCenter, "Hints");

            bool touch = UiPlatform.TouchMode;
            if (touch)
            {
                _hintsText.gameObject.SetActive(false);
                _retryButton = UiKit.CreateTouchButton(panel, _font, "REDEPLOY", 11,
                    new Vector2(0.5f, 0f), new Vector2(-66f, 58f), new Vector2(124f, 36f),
                    Retry, "RetryButton", accent: true);
                UiKit.CreateTouchButton(panel, _font, "TITLE", 11,
                    new Vector2(0.5f, 0f), new Vector2(66f, 58f), new Vector2(124f, 36f),
                    ToTitle, "TitleButton");
            }

            // 글로벌 스코어보드 제출 (P1). 앰버 CTA는 화면당 하나(REDEPLOY)라는 원칙을
            // 지켜 헤어라인 셀로 둔다 — 제출은 선택지지 이 화면의 주 동작이 아니다.
            _submitButton = UiKit.CreateTouchButton(panel, _font, SubmitIdleLabel, 11,
                new Vector2(0.5f, 0f),
                touch ? new Vector2(0f, 14f) : new Vector2(0f, 48f),
                touch ? new Vector2(256f, 36f) : new Vector2(160f, 30f),
                SubmitScore, "SubmitButton");
            _submitLabel = _submitButton.GetComponentInChildren<Text>();

            _dim = UiKit.CreateDim(canvas.transform, Color.clear, "Tint");
            _dim.transform.SetAsFirstSibling();

            _root.SetActive(false);
        }

        void Retry()
        {
            if (_director != null) _director.RestartRun();
        }

        static void ToTitle()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
        }

        /// <summary>
        /// 스코어보드 제출. 이름이 없으면 먼저 받고(폰 브라우저는 window.prompt), 그다음
        /// 비동기 제출이다. 실패해도 화면은 그대로 살아 있고 버튼만 재시도로 바뀐다 —
        /// 네트워크 사정이 재출격을 막으면 안 된다.
        /// </summary>
        void SubmitScore()
        {
            if (_submitState != SubmitState.Idle) return;
            if (_director == null || !_director.IsRunFinished) return;

            // 치트를 쓴 런은 기록이 아니다 (Update가 이미 버튼을 닫지만, 제출 경로 자체를 막는다).
            if (_director.CheatUsed)
            {
                _submitState = SubmitState.Done;
                SetSubmitLabel(SubmitCheatLabel);
                if (_submitButton != null) _submitButton.interactable = false;
                return;
            }

            // 리플레이 재생은 기록의 재현일 뿐 새 기록이 아니다 — 보드에 올리지 않는다.
            if (_director.ReplayMode)
            {
                _submitState = SubmitState.Done;
                SetSubmitLabel("REPLAY — NO SUBMIT");
                if (_submitButton != null) _submitButton.interactable = false;
                return;
            }

            string playerName = ScoreboardClient.PlayerName;
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = ScoreboardClient.SanitizeName(NamePrompt.Ask(playerName));
                if (string.IsNullOrEmpty(playerName))
                {
                    // 취소했거나 2자 미만이다. 상태는 Idle로 두어 다시 누를 수 있게 한다.
                    SetSubmitLabel("NAME 2-10 CHARS");
                    return;
                }
                ScoreboardClient.PlayerName = playerName;
            }

            _submitState = SubmitState.Sending;
            SetSubmitLabel("SENDING...");
            var runStats = _director.RunStats;
            var gauge = _director.Gauge;
            ScoreboardClient.Submit(new ScoreSubmission
            {
                Score = _director.TotalScore,
                Seed = _director.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Ship = _director.ShipId,
                Difficulty = _director.DifficultyLabel,
                Grade = GradeLabel(_director.CompletionGrade),
                // TODO: 리플레이 검증 해시. Core에 "이 런의 결과 해시"를 내주는 API가 없어
                // 비워 둔다 (Reviews/from-claude/requests.md REQ-093 — CODEX 요청).
                ReplayHash = "",
                Daily = _director.IsDailyRun,

                // P1.5 상세 통계 (REQ-094 관측 소비). 전부 사망 시점의 Core 권위 값이다 —
                // 화면에 이미 그려 둔 숫자를 다시 계산하지 않고 같은 출처에서 읽는다.
                Stage = _director.StageIndex,
                Room = _director.RoomIndex,
                // 옵션은 게이지 Option 레벨이 곧 개수다 (Core가 이 값으로 옵션을 띄운다).
                // 사망 시점의 IBattleSim.Options는 이미 정리됐을 수 있어 게이지를 읽는다.
                Options = gauge != null
                    ? gauge.GetLevel(Shmup.Core.PowerUpSlot.Option) : 0,
                Levels = GaugeLevelTotal(gauge),
                Bombs = ToInt(runStats.BombsUsed),
                Graze = ToInt(runStats.GrazeCount),
                MaxCombo = _director.BestMultiplier
            }, OnSubmitDone);
        }

        /// <summary>
        /// 게이지 전 슬롯 레벨의 합 (SHOT 포함). "빌드를 얼마나 키웠나"를 한 숫자로
        /// 압축한 값이라 슬롯 구성이 기체마다 달라도 비교가 된다.
        /// </summary>
        static int GaugeLevelTotal(Shmup.Core.PowerUpGauge gauge)
        {
            if (gauge == null) return 0;
            int total = 0;
            for (int i = 0; i < gauge.GaugeSlotCount; i++)
                total += gauge.GetGaugeSlotView(i).Level;
            return total;
        }

        /// <summary>Core 통계는 포화 누계(long)다 — 제출 필드 폭에 맞춰 자른다.</summary>
        static int ToInt(long value)
        {
            if (value <= 0) return 0;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        void OnSubmitDone(int rank, string error)
        {
            // 응답이 늦게 오면 화면이 이미 파괴됐을 수 있다 (재출격/타이틀 복귀).
            if (this == null || _submitButton == null) return;
            if (error != null)
            {
                _submitState = SubmitState.Idle;
                SetSubmitLabel("RETRY SUBMIT");
                return;
            }
            _submitState = SubmitState.Done;
            SetSubmitLabel(rank > 0 ? $"RANK #{rank}" : "SUBMITTED");
            _submitButton.interactable = false;
        }

        void SetSubmitLabel(string text)
        {
            if (_submitLabel != null) _submitLabel.text = text;
        }

        static string GradeLabel(Shmup.Core.Simulation.RunCompletionGrade grade)
        {
            switch (grade)
            {
                case Shmup.Core.Simulation.RunCompletionGrade.PerfectClear: return "PERFECT";
                case Shmup.Core.Simulation.RunCompletionGrade.StandardClear: return "CLEAR";
                default: return "KIA";
            }
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            // 사망(RunOver)과 완주(RunCleared)를 같은 패널로 처리하되 문면을 바꾼다 (REQ-031)
            bool finished = _director.IsRunFinished;
            if (_root.activeSelf != finished)
                _root.SetActive(finished);
            if (!finished) return;

            if (_shownRun != _director.RunNumber || _shownCleared != _director.IsRunCleared)
            {
                _shownRun = _director.RunNumber;
                _shownCleared = _director.IsRunCleared;
                bool cleared = _shownCleared;
                _titleText.text = cleared ? UiText.RunClearedTitle : UiText.GameOverTitle;
                _titleText.color = cleared ? UiKit.TextAccent : UiKit.TextDanger;
                _hintsText.text = cleared ? UiText.RunClearedHints : UiText.GameOverHints;
                // 완주 뒤에는 파워업을 승계하지 않으므로 "재출격"이 아니라 새 런이다.
                if (_retryButton != null)
                {
                    var label = _retryButton.GetComponentInChildren<Text>();
                    if (label != null) label.text = cleared ? "NEW RUN" : "REDEPLOY";
                }
                // 새 런의 결과다 — 지난 런의 제출 결과(RANK #n)를 그대로 두면 오독된다.
                if (_submitButton != null)
                {
                    _submitState = SubmitState.Idle;
                    _submitButton.interactable = true;
                    SetSubmitLabel(_director.IsDailyRun ? SubmitDailyLabel : SubmitIdleLabel);
                }
                if (_dim != null)
                    _dim.color = cleared
                        ? new Color(0.06f, 0.22f, 0.12f, 0.45f)   // 승리: 청록 틴트
                        : new Color(0.35f, 0.02f, 0.05f, 0.45f);  // 패배: 적색 틴트
                var stats = _director.RunStats;
                float accuracy = stats.ShotsFired > 0
                    ? (float)stats.ShotsHit / stats.ShotsFired * 100f : 0f;
                _scoreText.text =
                    $"SCORE  {_director.TotalScore:D8}   (run {_director.RunNumber}, stage {_director.StageIndex})";
                _statsText.text =
                    $"KILLS {stats.Kills}   CAPSULES {stats.CapsulesCollected}   ACC {accuracy:0.#}%   SHOTS {stats.ShotsFired}";
                // BOMBS는 보드의 NB(노봄) 뱃지와 같은 값이다 — 여기서 0을 확인할 수
                // 있어야 뱃지가 왜 붙었는지/안 붙었는지 납득이 된다.
                _extraText.text =
                    $"BEST COMBO x{_director.BestMultiplier}   GRAZE {stats.GrazeCount}"
                    + $"   BOMBS {stats.BombsUsed}";
                _modifierText.text = DescribeModifiers(_director.ActiveModifiers);
            }

            // 치트는 게임오버 화면이 떠 있는 동안에도 눌릴 수 있으므로 런 전환 블록 밖에서
            // 매 프레임 확인한다 (bool 비교 한 번 — 할당 없음).
            if (_director.CheatUsed && _submitState != SubmitState.Done)
            {
                _submitState = SubmitState.Done;
                if (_submitButton != null) _submitButton.interactable = false;
                SetSubmitLabel(SubmitCheatLabel);
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool restart = (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            bool toTitle = (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
            if (restart) Retry();
            else if (toTitle) ToTitle();
        }

        static string DescribeModifiers(Shmup.Core.Simulation.BattleModifier modifiers)
        {
            if (modifiers == Shmup.Core.Simulation.BattleModifier.None) return "";
            var sb = new System.Text.StringBuilder(64);
            sb.Append("BUILD: ");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.PierceShot, "PIERCE");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.Ricochet, "RICOCHET");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.HomingMissile, "HOMING");
            AppendModifier(sb, modifiers, Shmup.Core.Simulation.BattleModifier.KillExplosion, "BLAST");
            return sb.ToString();
        }

        static void AppendModifier(
            System.Text.StringBuilder sb,
            Shmup.Core.Simulation.BattleModifier modifiers,
            Shmup.Core.Simulation.BattleModifier flag,
            string label)
        {
            if ((modifiers & flag) == 0) return;
            if (sb.Length > 7) sb.Append(" + ");
            sb.Append(label);
        }
    }
}
