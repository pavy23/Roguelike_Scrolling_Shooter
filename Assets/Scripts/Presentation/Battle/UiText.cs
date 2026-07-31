namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// UI 표시 문자열 단일 출처. 출시 요건상 언어를 영어로 통일했고(퍼블리셔 심사 지적:
    /// 한/영 혼재), 로컬라이제이션은 이 클래스만 언어별 테이블로 바꾸면 된다.
    /// 게임플레이 로직은 이 문자열에 의존하지 않는다 — 표시 전용.
    /// </summary>
    public static class UiText
    {
        // 온보딩 (첫 런 3단계)
        public const string Onboarding1 =
            "MOVE  WASD / LEFT STICK      FIRE  SPACE / (A)      PAUSE  ESC / (START)";
        public const string Onboarding2 =
            "Destroy enemies to drop capsules - each one advances the gauge below";
        public const string Onboarding3 =
            "Press X / (Y) to spend the gauge. WHERE you spend it is your build.";

        // 일시정지
        public const string PauseTitle = "PAUSED";
        public const string PauseHints =
            "ESC / (START) RESUME      O / (SELECT) OPTIONS      Q QUIT TO TITLE";
        public const string VolumeFormat = "VOLUME  {0}%   (LEFT / RIGHT)";

        // 게임오버 / 완주
        public const string GameOverTitle = "GAME OVER";
        public const string RunClearedTitle = "RUN COMPLETE";
        public const string GameOverHints =
            "[ENTER] / (A) REDEPLOY - KEEP POWER-UPS      [R] / (B) TITLE";
        public const string RunClearedHints =
            "[ENTER] / (A) NEW RUN      [R] / (B) TITLE";

        // 보상 / 경로
        public const string RewardTitle = "STAGE CLEAR - CHOOSE REWARD";

        /// <summary>중간보스 직후의 짧은 2택 (REQ-054). 주 보상과 무게가 달라야 한다.</summary>
        public const string MidRewardTitle = "MID-BOSS DOWN - QUICK PICK";
        public const string RouteTitle = "CHOOSE YOUR ROUTE";

        /// <summary>섹터 계약 (REQ-070) — 다음 스테이지의 조건을 보고 고른다.</summary>
        public const string ContractTitle = "NEXT SECTOR - CHOOSE YOUR CONTRACT";
        public const string ChoiceHints =
            "[1]-[3] QUICK PICK      LEFT / RIGHT MOVE   (A) / [ENTER] CONFIRM";

        // 타이틀
        public const string LaunchPrompt = "PRESS SPACE / (A) TO LAUNCH";
        public const string SeedFormat = "SEED  {0}_   (type digits, backspace to edit)";
        public const string ContinueFormat = "[C]/(X) CONTINUE - stage {0}, score {1}";
        public const string DailyFormat = "[D]/(RB) DAILY RUN {0}";
        public const string ReplayFormat = "[V]/(LB) REPLAY - {0}";
        public const string DifficultyFormat = "[T] DIFFICULTY  < {0} >";

        // 행거
        public const string HangarFormat = "HANGAR  < {0}/{1} >      CREDIT {2}";
        public const string ShipSelected = "[SELECTED]";
        public const string ShipOwned = "[OWNED]";
        public const string ShipLockedFormat = "[LOCKED - {0} cr, U/(Y) to unlock]";

        // 옵션
        public const string OptionsTitle = "OPTIONS";
        public const string RebindPrompt = "PRESS ANY KEY\n\n(ESC cancel)";
        public const string OptionClose = "CLOSE  [O]/(SELECT)";

        // 조우 타입 (EncounterType 순서와 정렬)
        public static readonly string[] EncounterNames =
        {
            "BATTLE",
            "ELITE  (modifier guaranteed)",
            "SUPPLY  (resupply)",
            "HAZARD  (score x1.5)",
            "RARE  (double reward)"
        };

        // 테마 표시명 (themeIds 순서와 정렬)
        public static readonly string[] ThemeNames =
        {
            "SCRAPYARD", "BIO HIVE", "FORTRESS", "NEBULA", "CORE"
        };

        // 보스 등장
        public const string BossWarning = "!! WARNING !!";

        // 터치 기기 전용 문면. 폰에서는 키·패드 단축키 안내가 읽을 이유가 없어서,
        // 같은 자리에 터치 조작을 설명하거나 버튼이 대신하도록 비워 둔다.
        public const string Onboarding1Touch =
            "TOUCH AND DRAG - YOUR SHIP FOLLOWS YOUR FINGER      AUTO FIRE IS ON";
        public const string Onboarding3Touch =
            "Tap the X button to spend the gauge. WHERE you spend it is your build.";
        public const string SeedFormatTouch = "SEED  {0}";
        public const string ChoiceHintsTouch = "TAP A CARD TO CHOOSE";
        public const string VolumeFormatTouch = "VOLUME  {0}%";
    }
}
