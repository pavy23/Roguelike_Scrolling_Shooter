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
        Text _difficultyText;
        Text _difficultyButtonLabel;

        void RefreshDifficultyText()
        {
            if (_difficultyText != null)
                _difficultyText.text = $"[T] DIFFICULTY ◄ {DifficultySelect.Label} ►";
            if (_difficultyButtonLabel != null)
                _difficultyButtonLabel.text = $"DIFFICULTY\n{DifficultySelect.Label}";
        }

        void CycleDifficulty()
        {
            DifficultySelect.Index = (DifficultySelect.Index + 1) % 3;
            RefreshDifficultyText();
        }

        void StartDailyRun()
        {
            DevArgs.RuntimeSeed = (long)Shmup.Core.DailySeed.FromDate(_dailyDateInt);
            SceneManager.LoadScene("Battle");
        }

        void ContinueRun()
        {
            if (_suspended == null) return;
            // 저장 파일은 삭제하지 않는다 — 복원이 성공한 뒤 BattleDirector가 지운다
            BattleDirector.PendingResume = _suspended;
            DevArgs.RuntimeSeed = (long)_suspended.runSeed;
            SceneManager.LoadScene("Battle");
        }

        void PlayReplay()
        {
            if (_replay == null) return;
            BattleDirector.PendingReplay = _replay;
            DevArgs.RuntimeSeed = _replay.seed;
            SceneManager.LoadScene("Battle");
        }

        void RerollSeed()
        {
            _seedText = ((uint)System.Environment.TickCount).ToString();
        }

        /// <summary>
        /// 터치 전용 버튼 열. 폰에서는 키보드 단축키 안내가 아무 의미가 없으므로, 안내 텍스트는
        /// 감추고 같은 동작을 하는 버튼으로 바꿔 놓는다.
        /// </summary>
        void BuildTouchButtons(Transform parent)
        {
            const float w = 132f, h = 34f, step = 38f;
            float y = -150f;

            var difficulty = UiKit.CreateTouchButton(parent, _font, "", 10,
                new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, h),
                CycleDifficulty, "DifficultyButton");
            _difficultyButtonLabel = difficulty.GetComponentInChildren<Text>();
            y -= step;

            UiKit.CreateTouchButton(parent, _font, "DAILY RUN", 10,
                new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, h),
                StartDailyRun, "DailyButton");
            y -= step;

            if (_suspended != null)
            {
                UiKit.CreateTouchButton(parent, _font,
                    $"CONTINUE\nstage {_suspended.stageIndex}", 10,
                    new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, h),
                    ContinueRun, "ContinueButton", accent: true);
                y -= step;
            }

            if (_replay != null)
            {
                UiKit.CreateTouchButton(parent, _font, "REPLAY", 10,
                    new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, h),
                    PlayReplay, "ReplayButton");
            }

            // 시드는 폰에서 숫자 입력이 번거로우므로 다시 뽑기만 제공한다.
            UiKit.CreateTouchButton(parent, _font, "NEW SEED", 10,
                new Vector2(1f, 1f), new Vector2(-10f, -150f), new Vector2(112f, h),
                RerollSeed, "SeedButton");

            // 시드 값은 그 버튼 바로 아래로 옮긴다 — 원래 자리(하단 중앙)는 LAUNCH와 격납고가 쓴다.
            if (_seedValueText != null)
            {
                var rect = _seedValueText.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-10f, -150f - h - 4f);
                rect.sizeDelta = new Vector2(112f, 20f);
                _seedValueText.alignment = TextAnchor.UpperRight;
                _seedValueText.fontSize = 9;
            }

            // 출격은 가장 크고 눈에 띄게 — 이 화면의 유일한 주 동작이다.
            UiKit.CreateTouchButton(parent, _fontBold, "LAUNCH", 20,
                new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(200f, 50f),
                StartRun, "LaunchButton", accent: true);
        }

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
            // 로고 밑줄 — 양끝이 사그라드는 금색 라인이 로고와 메뉴 영역을 나눈다
            UiKit.CreateRule(canvas.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -148f), 300f, UiKit.TextAccent, "TitleRule");
            _promptText = UiKit.CreateCornerText(canvas.transform, _font,
                UiText.LaunchPrompt, 14, UiKit.TextAccent,
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

            // 난이도 선택 (REQ-020): [T]/(dpad↑) 순환, 잠정 배율 §7
            _difficultyText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextMain, new Vector2(0f, 0.5f), new Vector2(14f, 56f),
                TextAnchor.MiddleLeft, "Difficulty");
            UiKit.AddShadow(_difficultyText);
            RefreshDifficultyText();

            // 데일리 런 (REQ-018): 날짜는 Presentation이 읽고 Core는 순수 해시만
            var todayUtc = System.DateTime.UtcNow;
            _dailyDateInt = todayUtc.Year * 10000 + todayUtc.Month * 100 + todayUtc.Day;
            var daily = UiKit.CreateCornerText(canvas.transform, _font,
                $"[D]/(RB) DAILY RUN {todayUtc:MM-dd}", 11, UiKit.TextMain,
                new Vector2(0f, 0.5f), new Vector2(14f, 12f), TextAnchor.MiddleLeft, "Daily");
            UiKit.AddShadow(daily);

            // 마지막 런 리플레이 (REQ-018/019)
            _replay = ReplaySave.TryLoad();
            Text replayText = null;
            if (_replay != null)
            {
                replayText = UiKit.CreateCornerText(canvas.transform, _font,
                    $"[V]/(LB) REPLAY — {_replay.finalScore:N0}", 11, UiKit.TextMain,
                    new Vector2(0f, 0.5f), new Vector2(14f, -10f), TextAnchor.MiddleLeft, "Replay");
                UiKit.AddShadow(replayText);
            }

            if (UiPlatform.TouchMode)
            {
                // 단축키 안내는 폰에서 읽을 이유가 없다 — 버튼이 같은 일을 한다.
                Hide(_promptText);
                Hide(_difficultyText);
                Hide(daily);
                Hide(_continueText);
                Hide(replayText);
                BuildTouchButtons(canvas.transform);
            }
            RefreshDifficultyText();
        }

        static void Hide(Text text)
        {
            if (text != null) text.gameObject.SetActive(false);
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
                ContinueRun();
                return;
            }

            // 난이도 순환
            if ((keyboard != null && keyboard.tKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.dpad.up.wasPressedThisFrame))
                CycleDifficulty();

            // 데일리 런: 같은 날짜 → 전 세계 같은 시드 (Core DailySeed)
            if ((keyboard != null && keyboard.dKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame))
            {
                StartDailyRun();
                return;
            }

            // 마지막 런 리플레이
            if (_replay != null &&
                ((keyboard != null && keyboard.vKey.wasPressedThisFrame)
                 || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame)))
            {
                PlayReplay();
                return;
            }

            // 깜빡이는 출격 안내 (터치 모드에서는 LAUNCH 버튼이 대신하므로 꺼져 있다)
            bool promptVisible = Mathf.Repeat(Time.time, 1f) < 0.7f;
            if (_promptText != null && _promptText.enabled != promptVisible)
                _promptText.enabled = promptVisible;

            if (_seedValueText != null && !ReferenceEquals(_shownSeed, _seedText))
            {
                _shownSeed = _seedText;
                _seedValueText.text = string.Format(
                    UiPlatform.TouchMode ? UiText.SeedFormatTouch : UiText.SeedFormat, _seedText);
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
