using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 (UGUI + 픽셀 폰트). 스타필드가 천천히 흐르고, 시드를 확인/수정한 뒤
    /// Space/Enter/(A)로 출격한다. 시드 편집은 숫자 키 + 백스페이스 직접 처리
    /// (InputField/EventSystem 의존 없이 패드와 공존).
    ///
    /// 시드는 방문할 때마다 새로 뽑는다. 이건 "이번 런을 무엇으로 할지"의 선택일 뿐이고
    /// (Presentation 소관), 같은 시드를 넣으면 같은 런이 나오는 것은 Core가 보장한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] Transform[] _layers;
        [SerializeField] float[] _factors;
        [SerializeField] float _tileWidth = 24f;
        [SerializeField] float _driftSpeed = 1.2f;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        string _seedText;
        Text _promptText, _seedValueText, _continueText;
        string _shownSeed;
        Shmup.Core.Simulation.RunSuspendData _suspended;
        ReplayFileData _replay;
        int _dailyDateInt;

        void Start()
        {
            _seedText = ((uint)System.Environment.TickCount).ToString();

            var canvas = UiKit.CreateCanvas("TitleCanvas", 50);
            canvas.transform.SetParent(transform, false);

            var title1 = UiKit.CreateCornerText(canvas.transform, _fontBold, "ROGUELIKE", 40,
                new Color(0.62f, 0.83f, 1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                TextAnchor.UpperCenter, "Title1");
            var title2 = UiKit.CreateCornerText(canvas.transform, _fontBold, "SCROLLING SHOOTER", 40,
                new Color(0.62f, 0.83f, 1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f),
                TextAnchor.UpperCenter, "Title2");
            UiKit.AddShadow(title1, 3f);
            UiKit.AddShadow(title2, 3f);
            _promptText = UiKit.CreateCornerText(canvas.transform, _font,
                "PRESS SPACE / (A) TO LAUNCH", 14, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), TextAnchor.UpperCenter, "Prompt");
            UiKit.AddShadow(_promptText);
            _seedValueText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 66f),
                TextAnchor.LowerCenter, "Seed");
            UiKit.AddShadow(_seedValueText);

            // 이어하기 (REQ-017): 저장된 런이 있으면 안내 표시
            _suspended = RunSave.TryLoad();
            if (_suspended != null)
            {
                _continueText = UiKit.CreateCornerText(canvas.transform, _font,
                    $"[C]/(X) CONTINUE — stage {_suspended.stageIndex}, score {_suspended.score:N0}",
                    11, UiKit.TextAccent, new Vector2(0f, 0.5f), new Vector2(14f, 34f),
                    TextAnchor.MiddleLeft, "Continue");
                UiKit.AddShadow(_continueText);
            }

            // 데일리 런 (REQ-018): 날짜는 Presentation이 읽고 Core는 순수 해시만
            var todayUtc = System.DateTime.UtcNow;
            _dailyDateInt = todayUtc.Year * 10000 + todayUtc.Month * 100 + todayUtc.Day;
            var daily = UiKit.CreateCornerText(canvas.transform, _font,
                $"[D]/(RB) DAILY RUN {todayUtc:MM-dd}", 11, UiKit.TextMain,
                new Vector2(0f, 0.5f), new Vector2(14f, 12f), TextAnchor.MiddleLeft, "Daily");
            UiKit.AddShadow(daily);

            // 마지막 런 리플레이 (REQ-018/019)
            _replay = ReplaySave.TryLoad();
            if (_replay != null)
            {
                var replayText = UiKit.CreateCornerText(canvas.transform, _font,
                    $"[V]/(LB) REPLAY — {_replay.finalScore:N0}", 11, UiKit.TextMain,
                    new Vector2(0f, 0.5f), new Vector2(14f, -10f), TextAnchor.MiddleLeft, "Replay");
                UiKit.AddShadow(replayText);
            }
        }

        void Update()
        {
            if (_layers != null && _factors != null)
            {
                float scroll = Time.time * _driftSpeed;
                for (int i = 0; i < _layers.Length && i < _factors.Length; i++)
                {
                    if (_layers[i] == null) continue;
                    float offset = Mathf.Repeat(scroll * _factors[i], _tileWidth);
                    _layers[i].localPosition = new Vector3(-offset, 0f, 0f);
                }
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                EditSeed(keyboard);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    StartRun();
                    return;
                }
            }
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                StartRun();
                return;
            }

            // 이어하기
            if (_suspended != null &&
                ((keyboard != null && keyboard.cKey.wasPressedThisFrame)
                 || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame)))
            {
                BattleDirector.PendingResume = _suspended;
                RunSave.Delete();   // 소비형 — 재저장은 다음 중단 시점에
                DevArgs.RuntimeSeed = (long)_suspended.runSeed;   // HUD 시드 표시 일치
                SceneManager.LoadScene("Battle");
                return;
            }

            // 데일리 런: 같은 날짜 → 전 세계 같은 시드 (Core DailySeed)
            if ((keyboard != null && keyboard.dKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame))
            {
                DevArgs.RuntimeSeed = (long)Shmup.Core.DailySeed.FromDate(_dailyDateInt);
                SceneManager.LoadScene("Battle");
                return;
            }

            // 마지막 런 리플레이
            if (_replay != null &&
                ((keyboard != null && keyboard.vKey.wasPressedThisFrame)
                 || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame)))
            {
                BattleDirector.PendingReplay = _replay;
                DevArgs.RuntimeSeed = _replay.seed;
                SceneManager.LoadScene("Battle");
                return;
            }

            // 깜빡이는 출격 안내
            bool promptVisible = Mathf.Repeat(Time.time, 1f) < 0.7f;
            if (_promptText != null && _promptText.enabled != promptVisible)
                _promptText.enabled = promptVisible;

            if (_seedValueText != null && !ReferenceEquals(_shownSeed, _seedText))
            {
                _shownSeed = _seedText;
                _seedValueText.text = $"SEED  {_seedText}_   (숫자 입력/백스페이스로 수정)";
            }
        }

        void EditSeed(Keyboard keyboard)
        {
            if (keyboard.backspaceKey.wasPressedThisFrame && _seedText.Length > 0)
                _seedText = _seedText.Substring(0, _seedText.Length - 1);
            for (Key key = Key.Digit1; key <= Key.Digit0; key++)
            {
                if (!keyboard[key].wasPressedThisFrame || _seedText.Length >= 12) continue;
                int digit = key == Key.Digit0 ? 0 : key - Key.Digit1 + 1;
                _seedText += (char)('0' + digit);
            }
        }

        void StartRun()
        {
            DevArgs.RuntimeSeed = long.TryParse(_seedText, out long seed)
                ? seed
                : (uint)System.Environment.TickCount;
            SceneManager.LoadScene("Battle");
        }
    }
}
