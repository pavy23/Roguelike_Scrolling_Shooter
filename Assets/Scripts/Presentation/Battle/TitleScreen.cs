using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 (UGUI + 픽셀 폰트). 스타필드가 천천히 흐르고, Space/Enter/(A)로 출격한다.
    ///
    /// 시드는 방문할 때마다 새로 뽑는다. 이건 "이번 런을 무엇으로 할지"의 선택일 뿐이고
    /// (Presentation 소관), 같은 시드를 넣으면 같은 런이 나오는 것은 Core가 보장한다.
    ///
    /// 시드를 **보고 고치는** 수단(표시 줄·숫자 입력·리롤 버튼)은 개발용 재현 도구라
    /// <see cref="DevArgs.DevMode"/>에서만 나온다 — <see cref="_seedUi"/> 참고.
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

        /// <summary>
        /// 시드 UI(표시 줄·숫자 입력·NEW SEED 버튼)를 내보내는가 = <see cref="DevArgs.DevMode"/>.
        ///
        /// 이 셋은 "같은 판을 다시 돌린다"는 개발/디버깅 도구다. 릴리스에서 플레이어에게
        /// 시드 칸을 보여 주면 (a) 무슨 숫자인지 설명할 자리가 없고 (b) 손으로 고정한 런은
        /// 어차피 스코어보드 제출이 막혀 있어 눌러 봐야 손해만 본다. 그래서 통째로 감춘다 —
        /// 시드 값 자체는 그대로 <see cref="NewRandomSeed"/>로 뽑으므로 런의 동작은 같다.
        /// Start에서 한 번 읽어 캐시한다: 화면이 사는 동안 값이 바뀔 일이 없고, 생성 시점과
        /// Update의 판정이 어긋나면 없는 텍스트를 만지게 된다.
        /// </summary>
        bool _seedUi;

        /// <summary>
        /// 지금 칸에 있는 시드를 사람이 직접 쳐 넣었는가 (스코어보드 공정성).
        /// 같은 시드를 손으로 넣으면 같은 판을 몇 번이고 연습할 수 있고, 그렇게 만든
        /// 점수가 랜덤 시드 기록과 같은 보드에 서면 보드가 무의미해진다.
        /// 리롤(랜덤)로 되돌리면 다시 꺼진다 — 낙인은 출격 시점의 값으로 굳는다.
        /// </summary>
        bool _seedManual;
        bool _shownSeedManual;
        Shmup.Core.Simulation.RunSuspendData _suspended;
        ReplayFileData _replay;
        int _dailyDateInt;
        /// <summary>오늘(UTC)의 MM-dd. 데일리 안내/버튼이 같은 문자열을 쓰도록 한 번만 만든다.</summary>
        string _dailyDateLabel = "";
        Text _difficultyText;
        Text _difficultyButtonLabel;

        // 글로벌 랭킹 패널 (P1). 처음 눌렀을 때 한 번만 조립한다 — 타이틀에 온 사람 중
        // 보드를 여는 쪽이 소수라 로드 시점에 미리 만들 이유가 없다.
        GameObject _rankingRoot;
        Text _rankingBody;

        /// <summary>보드 표시 줄 수. 100줄을 다 받아도 화면에는 상위 10줄만 올린다.</summary>
        const int RankingRows = 10;

        // ── 보드 컬럼 폭 ──────────────────────────────────────────────────────
        //
        // "1 PAVY 123,450 ST 3-2 NB" 한 줄로는 뭐가 뭔지 알 수 없다는 지적(2026-08-01).
        // 컬럼 폭을 상수 한 벌로 뽑아 **헤더와 본문이 같은 자릿수**를 쓰게 한다 —
        // 둘을 따로 적어 두면 언젠가 반드시 어긋난다.
        const int ColRank = 2;
        const int ColPilot = 10;   // ScoreboardClient.NameMaxLength와 같다
        const int ColScore = 10;
        const int ColStage = 5;    // "10-4" / "CLR" / "PFT"
        const int ColShip = 4;
        const int ColBomb = 4;

        /// <summary>
        /// 컬럼 라벨 줄. 계기판 라벨 관례대로 전부 대문자이고, 본문보다 어두운 색으로
        /// 그려 기록보다 먼저 읽히지 않게 한다 (색은 BuildRankingPanel이 준다).
        /// </summary>
        static readonly string RankingHeader =
            "#".PadLeft(ColRank) + "  "
            + "PILOT".PadRight(ColPilot) + " "
            + "SCORE".PadLeft(ColScore) + "  "
            + "STG".PadRight(ColStage) + " "
            + "SHIP".PadRight(ColShip) + " "
            + "BOMB".PadLeft(ColBomb);

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
            // 스코어보드가 데일리 보드로 가르는 유일한 근거 — 시드와 같은 채널로 넘긴다.
            DevArgs.RuntimeDaily = true;
            // 데일리는 전원이 같은 시드로 겨루는 판이다 — 손으로 친 시드와 성격이 정반대다.
            DevArgs.RuntimeSeeded = false;
            SceneManager.LoadScene("Battle");
        }

        void ContinueRun()
        {
            if (_suspended == null) return;
            // 저장 파일은 삭제하지 않는다 — 복원이 성공한 뒤 BattleDirector가 지운다
            BattleDirector.PendingResume = _suspended;
            DevArgs.RuntimeSeed = (long)_suspended.runSeed;
            DevArgs.RuntimeDaily = false;
            // 이어하기의 시드는 그 런이 시작될 때 이미 정해진 값이다 — 지금 칸에 뭐가
            // 적혀 있든 상관없다 (BattleDirector도 이어하기 런은 낙인에서 뺀다).
            DevArgs.RuntimeSeeded = false;
            SceneManager.LoadScene("Battle");
        }

        void PlayReplay()
        {
            if (_replay == null) return;
            BattleDirector.PendingReplay = _replay;
            DevArgs.RuntimeSeed = _replay.seed;
            DevArgs.RuntimeDaily = false;
            DevArgs.RuntimeSeeded = false;   // 재생은 기록의 재현 — 제출 경로가 원래 닫혀 있다
            SceneManager.LoadScene("Battle");
        }

        /// <summary>
        /// 새 시드 생성. TickCount 단독은 부팅 후 시간이 길수록 상위 자릿수가 고정돼
        /// (예: 전부 4294xxxxxx) "맨날 같은 시드"로 체감된다 — 사람 지적 2026-08-01.
        /// 시계·GUID를 곱셈 해시로 섞어 자릿수 전체가 움직이게 한다. 시드 '선택'은
        /// Presentation 소관이라 여기서 섞어도 결정론(같은 시드 = 같은 런)과 무관하다.
        /// </summary>
        internal static uint NewRandomSeed()
        {
            unchecked
            {
                uint mixed = (uint)System.Environment.TickCount * 2654435761u;
                mixed ^= (uint)System.DateTime.Now.Ticks;
                mixed ^= (uint)System.Guid.NewGuid().GetHashCode() * 2246822519u;
                return mixed;
            }
        }

        void RerollSeed()
        {
            _seedText = NewRandomSeed().ToString();
            // 랜덤으로 다시 뽑았으면 손으로 친 흔적은 사라진다 — 제출 자격도 돌아온다.
            _seedManual = false;
        }

        // ── 글로벌 랭킹 (P1 스코어보드) ────────────────────────────────────────

        void ToggleRanking()
        {
            if (_rankingRoot == null) BuildRankingPanel();
            if (_rankingRoot == null) return;
            bool open = !_rankingRoot.activeSelf;
            _rankingRoot.SetActive(open);
            // 열 때마다 새로 받는다 — 데일리 보드는 하루 종일 움직인다.
            if (open) RequestRanking();
        }

        void CloseRanking()
        {
            if (_rankingRoot != null) _rankingRoot.SetActive(false);
        }

        /// <summary>
        /// 계기판 언어 그대로: 딤 + 헤어라인 패널 + 앰버 룰 한 줄. 이 모달 안에서
        /// 유일한 동작이 CLOSE라 여기서는 그쪽이 주 동작(챔퍼 블록)이다.
        /// </summary>
        void BuildRankingPanel()
        {
            var canvas = UiKit.CreateCanvas("RankingCanvas", 60);
            canvas.transform.SetParent(transform, false);
            _rankingRoot = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.72f));
            // 컬럼 6개(순위·파일럿·점수·스테이지·기체·봄)를 고정폭으로 세우려면 380px로는
            // 모자란다. 헤어라인 패널 언어는 그대로 두고 폭·높이만 키운다.
            var panel = UiKit.CreatePanel(canvas.transform, new Vector2(440f, 288f));

            UiKit.CreateCornerText(panel, _fontBold, "DAILY RANKING", 14, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -12f), TextAnchor.UpperCenter, "RankTitle");
            UiKit.CreateRule(panel, new Vector2(0.5f, 1f), new Vector2(0f, -34f), 380f,
                UiKit.TextAccent, "RankRule");

            // 컬럼 라벨은 본문과 **같은 폰트·크기·좌표·폭**이어야 자릿수가 맞는다.
            var header = UiKit.CreateCornerText(panel, _font, RankingHeader, 10, UiKit.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0f, -44f), TextAnchor.UpperLeft, "RankHeader");
            header.rectTransform.sizeDelta = new Vector2(400f, 14f);
            // 라벨과 기록을 가르는 헤어라인. 앰버는 위 룰 하나로 족하다 —
            // 액센트는 화면당 하나라는 계기판 원칙을 여기서도 지킨다.
            UiKit.CreateRule(panel, new Vector2(0.5f, 1f), new Vector2(0f, -58f), 400f,
                UiKit.PanelBorder, "RankHeaderRule");

            _rankingBody = UiKit.CreateCornerText(panel, _font, "", 10, UiKit.TextDim,
                new Vector2(0.5f, 1f), new Vector2(0f, -64f), TextAnchor.UpperLeft, "RankBody");
            _rankingBody.rectTransform.sizeDelta = new Vector2(400f, 168f);

            UiKit.CreateTouchButton(panel, _font, "CLOSE", 11,
                new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(140f, 34f),
                CloseRanking, "RankClose", accent: true);

            _rankingRoot.SetActive(false);
        }

        void RequestRanking()
        {
            if (_rankingBody == null) return;
            _rankingBody.text = "LOADING...";
            _rankingBody.color = UiKit.TextDim;
            ScoreboardClient.FetchBoard(true, OnRankingLoaded);
        }

        /// <summary>
        /// 서버가 없거나 회선이 끊겨도 타이틀은 멀쩡해야 한다 — 실패는 OFFLINE 한 단어로
        /// 끝내고 이유는 개발 빌드 로그(ScoreboardClient)에만 남긴다.
        /// </summary>
        void OnRankingLoaded(ScoreboardEntry[] entries, string error)
        {
            // 응답이 늦게 오면 이미 Battle 씬으로 넘어가 이 화면이 없을 수 있다.
            if (this == null || _rankingBody == null) return;

            if (error != null || entries == null)
            {
                _rankingBody.text = "OFFLINE";
                _rankingBody.color = UiKit.TextDim;
                return;
            }
            if (entries.Length == 0)
            {
                _rankingBody.text = "NO ENTRIES YET";
                _rankingBody.color = UiKit.TextDim;
                return;
            }

            var sb = new System.Text.StringBuilder(512);
            int count = Mathf.Min(RankingRows, entries.Length);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                AppendRow(sb, i + 1, entry);
            }
            _rankingBody.text = sb.ToString();
            _rankingBody.color = UiKit.TextMain;
        }

        /// <summary>앰버 뱃지 색 = UiKit.TextAccent. 리치 텍스트라 문자열로 박아 둔다.</summary>
        const string BadgeOpen = "<color=#FFB31C>";
        const string BadgeClose = "</color>";

        /// <summary>
        /// 보드 한 줄: 순위 · 파일럿 · 점수 · 달성 스테이지 · 기체 · 봄 사용 횟수.
        /// 헤더(<see cref="RankingHeader"/>)와 같은 컬럼 상수를 쓰고, 값이 없는 칸은
        /// '-'로 채워 자릿수를 지킨다 — 칸을 비우면 다음 컬럼이 밀려 헤더와 어긋난다.
        /// </summary>
        static void AppendRow(System.Text.StringBuilder sb, int rank, ScoreboardEntry entry)
        {
            // P1.5 이전 기록에는 상세 통계가 아예 없다 (서버가 키를 뺀다 → 전부 0).
            // 스테이지 번호는 1부터라 st <= 0이 곧 구 항목이고, 그때 0을 그리면
            // "1스테이지에서 봄 0개로 죽었다"는 거짓말이 된다.
            bool detailed = entry.st > 0;

            sb.Append(rank.ToString().PadLeft(ColRank));
            sb.Append("  ");
            sb.Append(Clip(entry.n, ColPilot).PadRight(ColPilot));
            sb.Append(' ');
            sb.Append(entry.s.ToString("N0").PadLeft(ColScore));
            sb.Append("  ");
            sb.Append(StageCell(entry, detailed).PadRight(ColStage));
            sb.Append(' ');
            sb.Append(ShipCell(entry).PadRight(ColShip));
            sb.Append(' ');
            AppendBombCell(sb, entry, detailed);
        }

        /// <summary>
        /// 달성 지점. 완주는 도달 좌표보다 등급이 정보다 — "5-4"보다 "CLR"이 크고,
        /// 무피격 완주(PFT)는 그보다 더 크다.
        /// </summary>
        static string StageCell(ScoreboardEntry entry, bool detailed)
        {
            if (!detailed) return "-";
            if (entry.g == "PERFECT") return "PFT";
            if (entry.g == "CLEAR") return "CLR";
            return entry.st.ToString() + "-" + (entry.rm > 0 ? entry.rm : 1).ToString();
        }

        /// <summary>기체 약칭 (ST/IC/BW). id가 비면 공백보다 '-'가 "없다"로 읽힌다.</summary>
        static string ShipCell(ScoreboardEntry entry)
        {
            string ship = ScoreboardClient.ShipAbbrev(entry.sh);
            return string.IsNullOrEmpty(ship) || ship.Trim().Length == 0 ? "-" : ship;
        }

        /// <summary>
        /// 봄 사용 횟수. 0은 "봄을 한 번도 안 쓴 주행"이라 같은 점수라도 다른 기록이다 —
        /// 예전 NB 뱃지를 대신해 숫자 0 자체를 앰버로 강조한다.
        /// 색 태그는 폭에 잡히지 않으므로 패딩을 **태그 안쪽**에 넣어야 정렬이 유지된다.
        /// </summary>
        static void AppendBombCell(
            System.Text.StringBuilder sb, ScoreboardEntry entry, bool detailed)
        {
            if (!detailed)
            {
                sb.Append("-".PadLeft(ColBomb));
                return;
            }
            string cell = entry.bb.ToString().PadLeft(ColBomb);
            if (entry.bb != 0)
            {
                sb.Append(cell);
                return;
            }
            sb.Append(BadgeOpen);
            sb.Append(cell);
            sb.Append(BadgeClose);
        }

        static string Clip(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "?";
            return value.Length <= max ? value : value.Substring(0, max);
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

            // 데일리는 "다른 시드로 한 판"이 아니라 모두가 같은 시드로 겨루는 스코어링
            // 챌린지다 — 두 줄로 성격(GLOBAL SEED)과 오늘 날짜를 함께 읽히게 한다.
            const float dailyH = 40f;
            UiKit.CreateTouchButton(parent, _font,
                string.Format(UiText.DailyButtonTouch, _dailyDateLabel), 9,
                new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(w, dailyH),
                StartDailyRun, "DailyButton");
            y -= step + (dailyH - h);

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

            // 오른쪽 열. 시드 블록은 개발 모드에서만 나오므로 좌표를 박아 두지 않고
            // 커서로 쌓는다 — 릴리스에서 시드가 빠진 자리에 버튼 두 칸짜리 구멍이
            // 남으면 랭킹 버튼만 허공에 떠 보인다.
            float rightY = -150f;

            if (_seedUi)
            {
                // 시드는 폰에서 숫자 입력이 번거로우므로 다시 뽑기만 제공한다.
                UiKit.CreateTouchButton(parent, _font, "NEW SEED", 10,
                    new Vector2(1f, 1f), new Vector2(-10f, rightY), new Vector2(112f, h),
                    RerollSeed, "SeedButton");
                rightY -= h + 4f;

                // 시드 값은 그 버튼 바로 아래로 — 원래 자리(하단 중앙)는 LAUNCH와 격납고가 쓴다.
                if (_seedValueText != null)
                {
                    var rect = _seedValueText.rectTransform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = new Vector2(-10f, rightY);
                    rect.sizeDelta = new Vector2(112f, 20f);
                    _seedValueText.alignment = TextAnchor.UpperRight;
                    _seedValueText.fontSize = 9;
                }
                rightY -= 26f;
            }

            // 글로벌 랭킹 (P1): 오늘의 데일리 보드 top 10.
            UiKit.CreateTouchButton(parent, _font, "RANKING", 10,
                new Vector2(1f, 1f), new Vector2(-10f, rightY), new Vector2(112f, h),
                ToggleRanking, "RankingButton");

            // 출격은 가장 크고 눈에 띄게 — 이 화면의 유일한 주 동작이다.
            UiKit.CreateTouchButton(parent, _fontBold, "LAUNCH", 20,
                new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(200f, 50f),
                StartRun, "LaunchButton", accent: true);
        }

        void Start()
        {
            _seedText = NewRandomSeed().ToString();
            _seedUi = DevArgs.DevMode;

            var canvas = UiKit.CreateCanvas("TitleCanvas", 50);
            canvas.transform.SetParent(transform, false);

            // CODEX 계기판 시그니처: 상단 상태 스트립 — 장식이 아니라 "시스템이 켜져
            // 있다"는 세계관 소품이다 (SYSTEM // READY).
            var strip = new GameObject("StatusStrip");
            strip.transform.SetParent(canvas.transform, false);
            var stripImage = strip.AddComponent<Image>();
            stripImage.sprite = UiSkin.Button;
            stripImage.type = Image.Type.Sliced;
            stripImage.color = new Color(0.275f, 0.315f, 0.360f, 0.9f);
            stripImage.raycastTarget = false;
            var stripRect = stripImage.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.sizeDelta = new Vector2(0f, 18f);
            var stripLeft = UiKit.CreateText(stripRect, _font, "SYSTEM // READY", 8,
                UiKit.TextAccent, TextAnchor.MiddleLeft, "StripLeft");
            var stripLeftRect = stripLeft.rectTransform;
            stripLeftRect.anchorMin = Vector2.zero;
            stripLeftRect.anchorMax = Vector2.one;
            stripLeftRect.offsetMin = new Vector2(10f, 0f);
            stripLeftRect.offsetMax = new Vector2(-10f, 0f);
            var stripRight = UiKit.CreateText(stripRect, _font, "RSS-01 // PILOT LINK", 8,
                UiKit.TextDim, TextAnchor.MiddleRight, "StripRight");
            var stripRightRect = stripRight.rectTransform;
            stripRightRect.anchorMin = Vector2.zero;
            stripRightRect.anchorMax = Vector2.one;
            stripRightRect.offsetMin = new Vector2(10f, 0f);
            stripRightRect.offsetMax = new Vector2(-10f, 0f);

            // 아이브로: 로고 위 작은 앰버 라벨 — 큰 타이포의 서열을 만들어 준다
            var eyebrow = UiKit.CreateCornerText(canvas.transform, _font, "- RUN PROTOCOL -", 9,
                UiKit.TextAccent, new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                TextAnchor.UpperCenter, "Eyebrow");
            UiKit.AddShadow(eyebrow, 1f);

            var title1 = UiKit.CreateCornerText(canvas.transform, _fontBold, "ROGUELIKE", 40,
                UiKit.TextMain, new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                TextAnchor.UpperCenter, "Title1");
            var title2 = UiKit.CreateCornerText(canvas.transform, _fontBold, "SCROLLING SHOOTER", 40,
                UiKit.TextMain, new Vector2(0.5f, 1f), new Vector2(0f, -102f),
                TextAnchor.UpperCenter, "Title2");
            UiKit.AddShadow(title1, 3f);
            UiKit.AddShadow(title2, 3f);
            // 로고 밑줄 — 양끝이 사그라드는 앰버 라인이 로고와 메뉴 영역을 나눈다
            UiKit.CreateRule(canvas.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -148f), 300f, UiKit.TextAccent, "TitleRule");
            _promptText = UiKit.CreateCornerText(canvas.transform, _font,
                UiText.LaunchPrompt, 14, UiKit.TextAccent,
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), TextAnchor.UpperCenter, "Prompt");
            UiKit.AddShadow(_promptText);
            if (_seedUi)
            {
                _seedValueText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                    UiKit.TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 66f),
                    TextAnchor.LowerCenter, "Seed");
                UiKit.AddShadow(_seedValueText);
            }

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
            _dailyDateLabel = todayUtc.ToString(
                "MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var daily = UiKit.CreateCornerText(canvas.transform, _font,
                string.Format(UiText.DailyFormat, _dailyDateLabel), 11, UiKit.TextMain,
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

            // 랭킹이 열려 있는 동안에는 출격/시드 편집 입력을 받지 않는다 —
            // 모달 위에서 스페이스가 그대로 출격으로 새면 보드를 읽다가 런이 시작된다.
            if (_rankingRoot != null && _rankingRoot.activeSelf) return;

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                // 시드 편집도 개발 모드 전용이다. 릴리스에서 숫자 키를 살려 두면 화면에
                // 아무것도 안 보이는 채로 시드가 바뀌고, 그 런은 수동 낙인이 찍혀 조용히
                // 제출이 막힌다 — 보이지 않는 UI에 붙은 입력은 없는 편이 낫다.
                if (_seedUi) EditSeed(keyboard);
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

            if (_seedValueText != null
                && (!ReferenceEquals(_shownSeed, _seedText) || _shownSeedManual != _seedManual))
            {
                _shownSeed = _seedText;
                _shownSeedManual = _seedManual;
                string line = string.Format(
                    UiPlatform.TouchMode ? UiText.SeedFormatTouch : UiText.SeedFormat, _seedText);
                // 제출이 막힌 사실은 런이 끝난 뒤가 아니라 **출격 전에** 알려야 한다.
                _seedValueText.text = _seedManual ? line + UiText.SeedManualSuffix : line;
                _seedValueText.color = _seedManual ? UiKit.TextAccent : UiKit.TextDim;
            }
        }

        void EditSeed(Keyboard keyboard)
        {
            if (keyboard.backspaceKey.wasPressedThisFrame && _seedText.Length > 0)
            {
                _seedText = _seedText.Substring(0, _seedText.Length - 1);
                _seedManual = true;
            }
            for (Key key = Key.Digit1; key <= Key.Digit0; key++)
            {
                if (!keyboard[key].wasPressedThisFrame || _seedText.Length >= 12) continue;
                int digit = key == Key.Digit0 ? 0 : key - Key.Digit1 + 1;
                _seedText += (char)('0' + digit);
                _seedManual = true;
            }
        }

        void StartRun()
        {
            bool parsed = long.TryParse(_seedText, out long seed);
            DevArgs.RuntimeSeed = parsed ? seed : NewRandomSeed();
            DevArgs.RuntimeDaily = false;
            // 수동 시드 낙인은 **출격 시점**에 굳는다: 만졌다가 리롤로 되돌렸으면 랜덤이고,
            // 파싱이 깨진 문자열이면 어차피 새로 뽑은 랜덤이라 수동이 아니다.
            DevArgs.RuntimeSeeded = _seedManual && parsed;
            SceneManager.LoadScene("Battle");
        }
    }
}
