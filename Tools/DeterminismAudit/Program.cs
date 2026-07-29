using System;
using System.Globalization;
using System.IO;
using Shmup.Core;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.DeterminismAudit
{
    static class Program
    {
        const int Success = 0;
        const int InvalidArguments = 2;
        const int AuditFailure = 3;
        const string CappedRewardId = "audit_capped";

        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1
                    && string.Equals(
                        args[0],
                        "--suite",
                        StringComparison.OrdinalIgnoreCase))
                    return RunSuite();

                if (args.Length != 3
                    || !TryParseSeed(args[0], out ulong seed)
                    || !TryParsePositiveInt(args[1], out int stageCount)
                    || !TryParsePositiveInt(args[2], out int tickCount))
                {
                    PrintUsage();
                    return InvalidArguments;
                }

                GameDataSet data = LoadGameData();
                ScenarioResult result = RunScenario(
                    data,
                    new AuditScenario(
                        "single",
                        seed,
                        stageCount,
                        tickCount,
                        RewardChoiceStrategy.Rotating));
                Console.WriteLine(result.Format());
                return Success;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Determinism audit failed: {ex.GetType().Name}: {ex.Message}");
                return AuditFailure;
            }
        }

        static int RunSuite()
        {
            GameDataSet data = LoadGameData();
            var scenarios = new[]
            {
                new AuditScenario(
                    "seed-0-first", 0UL, 4, 50_000,
                    RewardChoiceStrategy.First),
                new AuditScenario(
                    "seed-1-last", 1UL, 6, 75_000,
                    RewardChoiceStrategy.Last),
                new AuditScenario(
                    "seed-12345-rotating", 12_345UL, 8, 110_000,
                    RewardChoiceStrategy.Rotating),
                new AuditScenario(
                    "seed-deadbeef-rotating", 0xDEADBEEFUL, 10, 145_000,
                    RewardChoiceStrategy.Rotating),
                new AuditScenario(
                    "seed-max-prefer-capped", ulong.MaxValue, 14, 210_000,
                    RewardChoiceStrategy.PreferCapped)
            };

            Console.WriteLine(
                "suite=determinism-audit-02 "
                + $"scenarios={scenarios.Length} state=full-observable");
            for (int i = 0; i < scenarios.Length; i++)
            {
                ScenarioResult first = RunScenario(data, scenarios[i]);
                ScenarioResult second = RunScenario(data, scenarios[i]);
                if (!first.Matches(second))
                    throw new InvalidOperationException(
                        $"Scenario '{scenarios[i].Name}' diverged between identical runs: "
                        + $"{first.Hash:X16} != {second.Hash:X16}.");
                if (first.CompletedStages < scenarios[i].StageCount)
                    throw new InvalidOperationException(
                        $"Scenario '{scenarios[i].Name}' completed only "
                        + $"{first.CompletedStages}/{scenarios[i].StageCount} stages.");

                Console.WriteLine("PASS " + first.Format());
            }

            CapSweepResult capSweep = RunCapBoundarySweep();
            Console.WriteLine(
                "PASS cap-boundary "
                + $"seedsScanned={capSweep.SeedsScanned} "
                + $"qualifyingSeeds={capSweep.QualifyingSeeds} "
                + $"exampleSeed={capSweep.ExampleSeed} "
                + "stage2Pools=4-vs-5 "
                + "stage2BattleHash=matched "
                + "stage3BattleHash=matched "
                + "stage3RewardOptions=matched");
            Console.WriteLine("AUDIT PASS");
            return Success;
        }

        static ScenarioResult RunScenario(
            GameDataSet data,
            AuditScenario scenario)
        {
            BattleSimConfig config = data.CreateBattleSimConfig();
            // Audit traversal must not depend on current balance survivability.
            // Hit events and HP changes are still folded into the state hash.
            config.PlayerMaxHp = 1_000_000;
            var run = new RunManager(
                scenario.Seed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                data.DefaultShip);
            var hasher = new DeterminismAuditHasher();
            int[] rewardCounts = new int[data.Rewards.All.Count];
            int executedTicks = 0;
            int rewardChoices = 0;
            int cappedChoices = 0;

            hasher.FoldRunState(run);
            while (executedTicks < scenario.TickCount
                && run.StageIndex <= scenario.StageCount
                && run.State != RunState.RunOver)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    int optionIndex = SelectReward(
                        scenario.Strategy,
                        run,
                        data.Rewards,
                        rewardCounts,
                        executedTicks);
                    RewardOption option = run.RewardOptions[optionIndex];
                    hasher.FoldRewardChoice(
                        run.StageIndex,
                        optionIndex,
                        in option);
                    int catalogIndex = FindRewardDefinition(
                        data.Rewards,
                        option.Id);
                    if (catalogIndex >= 0)
                    {
                        if (rewardCounts[catalogIndex] < int.MaxValue)
                            rewardCounts[catalogIndex]++;
                        if (data.Rewards.All[catalogIndex].MaxPerRun.HasValue)
                            cappedChoices++;
                    }

                    run.ChooseReward(optionIndex);
                    rewardChoices++;
                    hasher.FoldRunState(run);
                    continue;
                }

                InputCommand input = CreateInput(
                    scenario.Seed,
                    executedTicks,
                    run.Battle.PlayerX);
                run.Step(in input);
                hasher.FoldRunState(run);
                executedTicks++;
            }

            return new ScenarioResult(
                scenario,
                hasher.Hash,
                executedTicks,
                run.Statistics.StagesCleared,
                rewardChoices,
                cappedChoices,
                run.StageIndex,
                run.State);
        }

        static int SelectReward(
            RewardChoiceStrategy strategy,
            RunManager run,
            RewardCatalog catalog,
            int[] rewardCounts,
            int executedTicks)
        {
            if (strategy == RewardChoiceStrategy.First)
                return 0;
            if (strategy == RewardChoiceStrategy.Last)
                return run.RewardOptions.Count - 1;
            if (strategy == RewardChoiceStrategy.PreferCapped)
            {
                for (int i = 0; i < run.RewardOptions.Count; i++)
                {
                    int catalogIndex = FindRewardDefinition(
                        catalog,
                        run.RewardOptions[i].Id);
                    if (catalogIndex < 0)
                        continue;
                    int? cap = catalog.All[catalogIndex].MaxPerRun;
                    if (cap.HasValue
                        && rewardCounts[catalogIndex] < cap.Value)
                        return i;
                }
            }

            return (run.StageIndex + executedTicks)
                % run.RewardOptions.Count;
        }

        static int FindRewardDefinition(RewardCatalog catalog, string id)
        {
            for (int i = 0; i < catalog.All.Count; i++)
            {
                if (string.Equals(
                        catalog.All[i].Id,
                        id,
                        StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        static CapSweepResult RunCapBoundarySweep()
        {
            const int seedsToScan = 256;
            int qualifying = 0;
            ulong exampleSeed = 0;

            for (ulong seed = 0; seed < seedsToScan; seed++)
            {
                if (!QualifiesForCapBoundary(seed))
                    continue;

                CapTraceResult first = RunCapBoundaryTrace(seed);
                CapTraceResult second = RunCapBoundaryTrace(seed);
                if (!first.Matches(second))
                    throw new InvalidOperationException(
                        $"Capped reward boundary trace diverged for seed {seed}.");
                if (!first.Stage2BattleMatched
                    || !first.Stage3BattleMatched
                    || !first.Stage3OptionsMatched)
                    throw new InvalidOperationException(
                        $"Reward stream isolation failed for seed {seed}.");

                if (qualifying == 0)
                    exampleSeed = seed;
                qualifying++;
            }

            if (qualifying == 0)
                throw new InvalidOperationException(
                    "No seed exercised the capped reward boundary.");
            return new CapSweepResult(seedsToScan, qualifying, exampleSeed);
        }

        static bool QualifiesForCapBoundary(ulong seed)
        {
            RunManager run = CreateBoundaryRun(seed);
            CompleteBoss(run, null);
            int capped = FindOption(run, CappedRewardId);
            int fallback = FindFallbackOption(run);
            if (capped < 0 || fallback < 0)
                return false;

            run.ChooseReward(fallback);
            CompleteBoss(run, null);
            return FindOption(run, CappedRewardId) >= 0;
        }

        static CapTraceResult RunCapBoundaryTrace(ulong seed)
        {
            RunManager cappedPath = CreateBoundaryRun(seed);
            RunManager uncappedPath = CreateBoundaryRun(seed);
            CompleteBoss(cappedPath, null);
            CompleteBoss(uncappedPath, null);

            int cappedIndex = FindOption(cappedPath, CappedRewardId);
            int fallbackIndex = FindFallbackOption(uncappedPath);
            cappedPath.ChooseReward(cappedIndex);
            uncappedPath.ChooseReward(fallbackIndex);

            var cappedStage2 = new DeterminismAuditHasher();
            var uncappedStage2 = new DeterminismAuditHasher();
            CompleteBoss(cappedPath, cappedStage2);
            CompleteBoss(uncappedPath, uncappedStage2);
            bool stage2BattleMatched =
                cappedStage2.Hash == uncappedStage2.Hash;
            if (FindOption(cappedPath, CappedRewardId) >= 0)
                throw new InvalidOperationException(
                    "Capped reward remained eligible after reaching maxPerRun.");
            int uncappedStage2Index =
                FindOption(uncappedPath, CappedRewardId);
            if (uncappedStage2Index < 0)
                throw new InvalidOperationException(
                    "Uncapped comparison path did not offer the capped reward.");

            cappedPath.ChooseReward(FindFallbackOption(cappedPath));
            uncappedPath.ChooseReward(uncappedStage2Index);

            var cappedStage3 = new DeterminismAuditHasher();
            var uncappedStage3 = new DeterminismAuditHasher();
            CompleteBoss(cappedPath, cappedStage3);
            CompleteBoss(uncappedPath, uncappedStage3);
            bool stage3BattleMatched =
                cappedStage3.Hash == uncappedStage3.Hash;
            string cappedOptions = RewardOptionSignature(cappedPath);
            string uncappedOptions = RewardOptionSignature(uncappedPath);
            bool stage3OptionsMatched = string.Equals(
                cappedOptions,
                uncappedOptions,
                StringComparison.Ordinal);

            return new CapTraceResult(
                cappedStage2.Hash,
                uncappedStage2.Hash,
                cappedStage3.Hash,
                uncappedStage3.Hash,
                cappedOptions,
                uncappedOptions,
                stage2BattleMatched,
                stage3BattleMatched,
                stage3OptionsMatched);
        }

        static RunManager CreateBoundaryRun(ulong seed)
        {
            var rewards = new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    BoundaryReward(CappedRewardId, 100, 1),
                    BoundaryReward("audit_fallback_a", 1, null),
                    BoundaryReward("audit_fallback_b", 1, null),
                    BoundaryReward("audit_fallback_c", 1, null),
                    BoundaryReward("audit_fallback_d", 1, null)
                });
            var weapon = new WeaponDefinition(
                "audit_shot", 1, 1, 256, 1, 0, 0);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerMaxHp = 1_000_000;
            config.PlayerMinX = -10_000;
            config.PlayerMaxX = 10_000;
            config.PlayerMinY = -10_000;
            config.PlayerMaxY = 10_000;
            config.BulletDespawnX = 20_000;
            config.EnemyDespawnX = -20_000;
            config.EnemyBulletDamage = 0;
            config.MaxEnemyBullets = 0;
            config.CapsuleNoDropWeight = 1;

            return new RunManager(
                seed,
                new AuditBossEveryStageGenerator(),
                config,
                content,
                PowerUpGauge.CreateDefault(),
                rewards);
        }

        static RewardDefinition BoundaryReward(
            string id,
            int weight,
            int? maxPerRun)
        {
            return new RewardDefinition(
                id,
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                1,
                weight,
                1,
                int.MaxValue,
                maxPerRun);
        }

        static void CompleteBoss(
            RunManager run,
            DeterminismAuditHasher battleHasher)
        {
            if (battleHasher != null)
                battleHasher.FoldBattleState(run.Battle);
            var fire = new InputCommand(0, 0, true);
            for (int i = 0;
                i < 2_000 && run.State == RunState.Playing;
                i++)
            {
                run.Step(in fire);
                if (battleHasher != null)
                    battleHasher.FoldBattleState(run.Battle);
            }
            if (run.State != RunState.AwaitingReward)
                throw new InvalidOperationException(
                    $"Audit boss did not reach reward state at stage {run.StageIndex}.");
        }

        static int FindOption(RunManager run, string id)
        {
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                if (string.Equals(
                        run.RewardOptions[i].Id,
                        id,
                        StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        static int FindFallbackOption(RunManager run)
        {
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                if (!string.Equals(
                        run.RewardOptions[i].Id,
                        CappedRewardId,
                        StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        static string RewardOptionSignature(RunManager run)
        {
            string signature = string.Empty;
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                if (i != 0)
                    signature += "|";
                RewardOption option = run.RewardOptions[i];
                signature += option.Id
                    + ":" + ((int)option.Type).ToString(CultureInfo.InvariantCulture)
                    + ":" + option.Amount.ToString(CultureInfo.InvariantCulture);
            }
            return signature;
        }

        static InputCommand CreateInput(
            ulong seed,
            int tick,
            int playerX)
        {
            int phaseOffset = (int)(seed % 360UL);
            int verticalPhase = (tick + phaseOffset * 3) % 360;
            int leftAuditLane = -18 * SimSpace.SubUnitsPerWorldUnit;
            int moveX = playerX > leftAuditLane ? -1 : 0;
            int moveY = verticalPhase < 180 ? 1 : -1;
            bool fire = (tick + phaseOffset) % 5 != 4;
            return new InputCommand(moveX, moveY, fire);
        }

        static GameDataSet LoadGameData()
        {
            string projectRoot = FindProjectRoot();
            string gameData = Path.Combine(projectRoot, "GameData");
            string rewardsPath = Path.Combine(gameData, "rewards.json");
            string shipsPath = Path.Combine(gameData, "ships.json");
            return GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")),
                File.Exists(rewardsPath)
                    ? File.ReadAllText(rewardsPath)
                    : null,
                File.Exists(shipsPath)
                    ? File.ReadAllText(shipsPath)
                    : null);
        }

        static bool TryParseSeed(string value, out ulong seed)
        {
            if (value != null && value.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    value.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out seed);
            }
            return ulong.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out seed);
        }

        static bool TryParsePositiveInt(string value, out int result)
        {
            return int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out result)
                && result > 0;
        }

        static void PrintUsage()
        {
            Console.Error.WriteLine(
                "Usage: dotnet run --project Tools/DeterminismAudit -- --suite");
            Console.Error.WriteLine(
                "   or: dotnet run --project Tools/DeterminismAudit -- "
                + "<seed> <stageCount> <tickCount>");
            Console.Error.WriteLine(
                "Seed accepts unsigned decimal or a 0x-prefixed hexadecimal value.");
        }

        static string FindProjectRoot()
        {
            string root = FindProjectRootFrom(Directory.GetCurrentDirectory());
            if (root != null)
                return root;

            root = FindProjectRootFrom(AppContext.BaseDirectory);
            if (root != null)
                return root;

            throw new DirectoryNotFoundException(
                "Could not find the project root containing GameData.");
        }

        static string FindProjectRootFrom(string startPath)
        {
            var directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                string dataPath = Path.Combine(directory.FullName, "GameData");
                if (File.Exists(Path.Combine(dataPath, "enemies.json"))
                    && File.Exists(Path.Combine(dataPath, "weapons.json"))
                    && File.Exists(Path.Combine(dataPath, "waves.json")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }

        enum RewardChoiceStrategy
        {
            First,
            Last,
            Rotating,
            PreferCapped
        }

        sealed class AuditScenario
        {
            public AuditScenario(
                string name,
                ulong seed,
                int stageCount,
                int tickCount,
                RewardChoiceStrategy strategy)
            {
                Name = name;
                Seed = seed;
                StageCount = stageCount;
                TickCount = tickCount;
                Strategy = strategy;
            }

            public string Name { get; }
            public ulong Seed { get; }
            public int StageCount { get; }
            public int TickCount { get; }
            public RewardChoiceStrategy Strategy { get; }
        }

        sealed class ScenarioResult
        {
            public ScenarioResult(
                AuditScenario scenario,
                ulong hash,
                int executedTicks,
                int completedStages,
                int rewardChoices,
                int cappedChoices,
                int finalStage,
                RunState finalState)
            {
                Scenario = scenario;
                Hash = hash;
                ExecutedTicks = executedTicks;
                CompletedStages = completedStages;
                RewardChoices = rewardChoices;
                CappedChoices = cappedChoices;
                FinalStage = finalStage;
                FinalState = finalState;
            }

            public AuditScenario Scenario { get; }
            public ulong Hash { get; }
            public int ExecutedTicks { get; }
            public int CompletedStages { get; }
            public int RewardChoices { get; }
            public int CappedChoices { get; }
            public int FinalStage { get; }
            public RunState FinalState { get; }

            public bool Matches(ScenarioResult other)
            {
                return other != null
                    && Hash == other.Hash
                    && ExecutedTicks == other.ExecutedTicks
                    && CompletedStages == other.CompletedStages
                    && RewardChoices == other.RewardChoices
                    && CappedChoices == other.CappedChoices
                    && FinalStage == other.FinalStage
                    && FinalState == other.FinalState;
            }

            public string Format()
            {
                return $"name={Scenario.Name} hash={Hash:X16} "
                    + $"seed={Scenario.Seed} strategy={Scenario.Strategy} "
                    + $"completedStages={CompletedStages}/{Scenario.StageCount} "
                    + $"ticks={ExecutedTicks} rewardChoices={RewardChoices} "
                    + $"cappedChoices={CappedChoices} "
                    + $"finalStage={FinalStage} state={FinalState}";
            }
        }

        sealed class CapTraceResult
        {
            public CapTraceResult(
                ulong cappedStage2Hash,
                ulong uncappedStage2Hash,
                ulong cappedStage3Hash,
                ulong uncappedStage3Hash,
                string cappedOptions,
                string uncappedOptions,
                bool stage2BattleMatched,
                bool stage3BattleMatched,
                bool stage3OptionsMatched)
            {
                CappedStage2Hash = cappedStage2Hash;
                UncappedStage2Hash = uncappedStage2Hash;
                CappedStage3Hash = cappedStage3Hash;
                UncappedStage3Hash = uncappedStage3Hash;
                CappedOptions = cappedOptions;
                UncappedOptions = uncappedOptions;
                Stage2BattleMatched = stage2BattleMatched;
                Stage3BattleMatched = stage3BattleMatched;
                Stage3OptionsMatched = stage3OptionsMatched;
            }

            public ulong CappedStage2Hash { get; }
            public ulong UncappedStage2Hash { get; }
            public ulong CappedStage3Hash { get; }
            public ulong UncappedStage3Hash { get; }
            public string CappedOptions { get; }
            public string UncappedOptions { get; }
            public bool Stage2BattleMatched { get; }
            public bool Stage3BattleMatched { get; }
            public bool Stage3OptionsMatched { get; }

            public bool Matches(CapTraceResult other)
            {
                return other != null
                    && CappedStage2Hash == other.CappedStage2Hash
                    && UncappedStage2Hash == other.UncappedStage2Hash
                    && CappedStage3Hash == other.CappedStage3Hash
                    && UncappedStage3Hash == other.UncappedStage3Hash
                    && string.Equals(
                        CappedOptions,
                        other.CappedOptions,
                        StringComparison.Ordinal)
                    && string.Equals(
                        UncappedOptions,
                        other.UncappedOptions,
                        StringComparison.Ordinal)
                    && Stage2BattleMatched == other.Stage2BattleMatched
                    && Stage3BattleMatched == other.Stage3BattleMatched
                    && Stage3OptionsMatched == other.Stage3OptionsMatched;
            }
        }

        readonly struct CapSweepResult
        {
            public CapSweepResult(
                int seedsScanned,
                int qualifyingSeeds,
                ulong exampleSeed)
            {
                SeedsScanned = seedsScanned;
                QualifyingSeeds = qualifyingSeeds;
                ExampleSeed = exampleSeed;
            }

            public int SeedsScanned { get; }
            public int QualifyingSeeds { get; }
            public ulong ExampleSeed { get; }
        }

        sealed class AuditBossEveryStageGenerator : IStageGenerator
        {
            static readonly BossPhase[] Phases =
            {
                new BossPhase(999, 1, 1, 1)
            };

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "audit_segment",
                            1,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
                    "audit_boss",
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    512,
                    Phases);
            }
        }
    }
}
