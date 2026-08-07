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
        const int ExpectedRunMinutes = 22;
        const int TickBudgetMarginPercent = 25;
        const int AuditBossHitRatePercent = 50;
        const int ColossalBossMaxHp = 62_000;

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
                    $"Determinism audit failed: {ex}");
                return AuditFailure;
            }
        }

        static int RunSuite()
        {
            GameDataSet data = LoadGameData();
            int tickBudget = ComputeSuiteTickBudget(data);
            var scenarios = new[]
            {
                new AuditScenario(
                    "seed-0-first", 0UL, 5, tickBudget,
                    RewardChoiceStrategy.First),
                new AuditScenario(
                    "seed-1-last", 1UL, 5, tickBudget,
                    RewardChoiceStrategy.Last),
                new AuditScenario(
                    "seed-12345-rotating", 12_345UL, 5, tickBudget,
                    RewardChoiceStrategy.Rotating),
                new AuditScenario(
                    "seed-deadbeef-rotating", 0xDEADBEEFUL, 5, tickBudget,
                    RewardChoiceStrategy.Rotating),
                new AuditScenario(
                    "seed-max-prefer-capped", ulong.MaxValue, 5, tickBudget,
                    RewardChoiceStrategy.PreferCapped),
                new AuditScenario(
                    "seed-7-hidden", 7UL, 5, tickBudget,
                    RewardChoiceStrategy.Rotating)
            };

            Console.WriteLine(
                "suite=determinism-audit-06 "
                + $"scenarios={scenarios.Length} state=full-observable "
                + "hiddenPath=required "
                + $"tickBudget={tickBudget} "
                + $"expectedRunTicks={ExpectedRunTicks()} "
                + $"marginPercent={TickBudgetMarginPercent} "
                + $"bossHitRatePercent={AuditBossHitRatePercent}");
            bool hiddenPathCompleted = false;
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
                        + $"{first.CompletedStages}/{scenarios[i].StageCount} stages. "
                        + "The data-derived tick budget was exhausted or progression "
                        + $"stalled. {first.Format()}");
                if (first.FinalState != RunState.RunCleared)
                    throw new InvalidOperationException(
                        $"Scenario '{scenarios[i].Name}' did not reach "
                        + $"RunCleared (state={first.FinalState}).");
                int expectedRooms =
                    scenarios[i].StageCount
                    * RunProgressionConfig.DefaultRoomsPerBiome;
                if (first.CompletionGrade == RunCompletionGrade.PerfectClear)
                {
                    expectedRooms += RunProgressionConfig.HiddenRooms;
                    hiddenPathCompleted = true;
                }
                if (first.CompletedRooms != expectedRooms)
                    throw new InvalidOperationException(
                        $"Scenario '{scenarios[i].Name}' completed only "
                        + $"{first.CompletedRooms}/{expectedRooms} rooms.");

                Console.WriteLine("PASS " + first.Format());
            }
            if (!hiddenPathCompleted)
                throw new InvalidOperationException(
                    "The required hidden-biome audit path was not completed.");

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
            PowerUpGauge legacyGauge = data.CreatePowerUpGauge();
            ShipDefinition auditShip = CreateAuditShip(
                data.DefaultShip,
                legacyGauge.GetMaxLevel(PowerUpSlot.MainShot));
            PowerUpGauge gauge =
                data.CreatePowerUpGauge(auditShip);
            var run = new RunManager(
                scenario.Seed,
                new AuditStageGenerator(data),
                config,
                data.BattleContent,
                gauge,
                data.Rewards,
                data.Contracts,
                auditShip);
            var hasher = new DeterminismAuditHasher();
            int[] rewardCounts = new int[data.Rewards.All.Count];
            int executedTicks = 0;
            int rewardChoices = 0;
            int routeChoices = 0;
            int cappedChoices = 0;
            int previousBossTargetY = 0;
            bool hasPreviousBossTarget = false;

            hasher.FoldRunState(run);
            while (executedTicks < scenario.TickCount
                && (run.StageIndex <= scenario.StageCount
                    || run.IsHiddenBiome)
                && run.State != RunState.RunOver
                && run.State != RunState.RunCleared)
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

                if (run.State == RunState.AwaitingContract)
                {
                    int optionIndex = SelectContract(
                        scenario.Strategy,
                        run,
                        executedTicks);
                    if (!run.ChooseContract(optionIndex))
                        throw new InvalidOperationException(
                            "Audit contract choice was rejected.");
                    hasher.FoldRunState(run);
                    continue;
                }

                RefillAuditShield(run);
                InputCommand input = CreateInput(
                    scenario.Seed,
                    executedTicks,
                    run,
                    ref previousBossTargetY,
                    ref hasPreviousBossTarget);
                run.Step(in input);
                hasher.FoldRunState(run);
                executedTicks++;
            }

            return new ScenarioResult(
                scenario,
                hasher.Hash,
                executedTicks,
                run.Statistics.StagesCleared,
                run.Statistics.RoomsCleared,
                rewardChoices,
                routeChoices,
                cappedChoices,
                run.StageIndex,
                run.State,
                run.BiomeIndex,
                run.RoomIndex,
                run.Battle.Tick,
                run.Battle.Boss.Hp,
                run.Battle.Boss.MaxHp,
                run.CompletionGrade,
                RunManager.CountHiddenBiomeConditions(
                    run.EliteRoomsCleared,
                    run.NoHitBiomesCleared,
                    run.RareEncountersCleared));
        }

        static int ComputeSuiteTickBudget(GameDataSet data)
        {
            WeaponDefinition main = data.BattleContent.PlayerWeapon;
            if (main.BaseDamage < 1 || main.FireIntervalTicks < 1)
                throw new InvalidOperationException(
                    "Cannot derive audit tick budget: the default main weapon "
                    + $"'{main.Id}' has baseDamage={main.BaseDamage} and "
                    + $"fireIntervalTicks={main.FireIntervalTicks}.");

            int maximumBossHp = 0;
            for (int i = 0; i < data.StageGeneration.Bosses.Count; i++)
                if (data.StageGeneration.Bosses[i].MaxHp > maximumBossHp)
                    maximumBossHp = data.StageGeneration.Bosses[i].MaxHp;

            long expectedWithMargin =
                (long)ExpectedRunTicks()
                * (100 + TickBudgetMarginPercent)
                / 100;
            long bossDamageTicks =
                (long)maximumBossHp
                * RunProgressionConfig.DefaultBiomeCount
                * main.FireIntervalTicks
                * 100
                / (main.BaseDamage * AuditBossHitRatePercent);
            long colossalDamageTicks =
                (long)ColossalBossMaxHp
                * main.FireIntervalTicks
                * 100
                / (main.BaseDamage * AuditBossHitRatePercent);
            long hiddenRoomTicks =
                (long)ExpectedRunTicks()
                * RunProgressionConfig.HiddenRooms
                / (RunProgressionConfig.DefaultBiomeCount
                    * RunProgressionConfig.DefaultRoomsPerBiome);
            long derived = expectedWithMargin
                + bossDamageTicks
                + colossalDamageTicks
                + hiddenRoomTicks;
            if (derived > int.MaxValue)
                throw new InvalidOperationException(
                    "Cannot derive audit tick budget: GameData boss HP and weapon "
                    + $"throughput require {derived} ticks, above Int32 capacity.");
            return (int)derived;
        }

        static int ExpectedRunTicks()
        {
            return ExpectedRunMinutes
                * 60
                * SimSpace.TicksPerSecond;
        }

        static int SelectRoute(
            RewardChoiceStrategy strategy,
            RunManager run,
            int executedTicks)
        {
            if (strategy == RewardChoiceStrategy.First)
                return 0;
            if (strategy == RewardChoiceStrategy.Last)
                return run.RouteOptions.Count - 1;
            return (run.StageIndex + executedTicks)
                % run.RouteOptions.Count;
        }

        static int SelectContract(
            RewardChoiceStrategy strategy,
            RunManager run,
            int executedTicks)
        {
            for (int i = 0; i < run.ContractOptions.Count; i++)
                if (run.ContractOptions[i].DestinationKind
                    == ContractDestinationKind.Uncharted)
                    return i;
            if (strategy == RewardChoiceStrategy.Last)
                return run.ContractOptions.Count - 1;
            if (strategy == RewardChoiceStrategy.Rotating
                || strategy == RewardChoiceStrategy.PreferCapped)
                return (run.BiomeIndex + executedTicks)
                    % run.ContractOptions.Count;
            return 0;
        }

        static void RefillAuditShield(RunManager run)
        {
            if (!(run.Battle is BattleSim battle))
                throw new InvalidOperationException(
                    "Audit survival compensation requires BattleSim.");
            battle.RecoverShieldStock(
                run.MaxShieldStock);
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
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
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
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ShipDefinition.CreateDefault(),
                1,
                1,
                new RunProgressionConfig(4, 1));
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
            if (run.State == RunState.AwaitingContract
                && !run.ChooseContract(0))
                throw new InvalidOperationException(
                    "Audit boundary contract choice was rejected.");
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
            RunManager run,
            ref int previousBossTargetY,
            ref bool hasPreviousBossTarget)
        {
            IBattleSim battle = run.Battle;
            int phaseOffset = (int)(seed % 360UL);
            int verticalPhase = (tick + phaseOffset * 3) % 360;
            int leftAuditLane = -6 * SimSpace.SubUnitsPerWorldUnit;
            int moveX = battle.PlayerX < leftAuditLane
                ? 1
                : battle.PlayerX > leftAuditLane
                    ? -1
                    : 0;
            int moveY;
            BossState boss = battle.Boss;
            if (battle.BossActive)
            {
                int rawTargetY = SelectBossTargetY(
                    run.StagePlan,
                    battle);
                int targetY = rawTargetY;
                if (hasPreviousBossTarget)
                {
                    long predicted =
                        (long)rawTargetY
                        + (rawTargetY - previousBossTargetY)
                            * 60L;
                    int leadLimit =
                        4 * SimSpace.SubUnitsPerWorldUnit;
                    predicted = Math.Max(
                        (long)rawTargetY - leadLimit,
                        Math.Min(
                            (long)rawTargetY + leadLimit,
                            predicted));
                    targetY = predicted < int.MinValue
                        ? int.MinValue
                        : predicted > int.MaxValue
                            ? int.MaxValue
                            : (int)predicted;
                }
                previousBossTargetY = rawTargetY;
                hasPreviousBossTarget = true;
                int aimTolerance = SimSpace.SubUnitsPerWorldUnit / 8;
                if (battle.PlayerY < targetY - aimTolerance)
                    moveY = 1;
                else if (battle.PlayerY > targetY + aimTolerance)
                    moveY = -1;
                else
                    moveY = 0;
            }
            else
            {
                hasPreviousBossTarget = false;
                if (TrySelectPickupTargetY(
                        battle,
                        out int pickupTargetY))
                {
                    int pickupTolerance =
                        SimSpace.SubUnitsPerWorldUnit / 4;
                    moveY = battle.PlayerY
                        < pickupTargetY - pickupTolerance
                            ? 1
                            : battle.PlayerY
                                > pickupTargetY + pickupTolerance
                                    ? -1
                                    : 0;
                }
                else
                    moveY = verticalPhase < 180 ? 1 : -1;
            }
            bool fire = true;
            bool activate =
                ShouldActivatePowerUp(run.PowerUpGauge);
            bool activateBomb =
                battle.BossActive
                && battle.BombStock > 0
                && battle.Tick % 600 == 1;
            return new InputCommand(
                moveX,
                moveY,
                fire,
                activate,
                activateBomb);
        }

        static bool ShouldActivatePowerUp(PowerUpGauge gauge)
        {
            if (gauge == null
                || gauge.Cursor == PowerUpGauge.NoSelection
                || !gauge.CanActivate)
                return false;
            PowerUpGaugeSlotView selected =
                gauge.GetGaugeSlotView(gauge.Cursor);
            if (selected.GaugeIndex != gauge.Cursor)
                throw new InvalidOperationException(
                    "Gauge observation index does not match its cursor.");
            if (selected.Level >= selected.MaxLevel)
                return false;

            // Invest in Missile/Option only. Coverage weapon modes (Double/Triple)
            // fire off the boss centerline and stall PreferCapped ST melts after
            // ClosingSegmentsPerStage lengthens capsule-rich routes (REQ-079).
            // Ship weapon-switch coverage lives in unit tests + BalanceSim.
            return selected.Slot == PowerUpSlot.Missile
                || selected.Slot == PowerUpSlot.Option;
        }

        static bool TrySelectPickupTargetY(
            IBattleSim battle,
            out int targetY)
        {
            long bestDistance = long.MaxValue;
            targetY = 0;
            for (int i = 0; i < battle.Capsules.Count; i++)
            {
                CapsuleState pickup = battle.Capsules[i];
                long distance = Math.Abs(
                    (long)pickup.X - battle.PlayerX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    targetY = pickup.Y;
                }
            }
            for (int i = 0; i < battle.BombPickups.Count; i++)
            {
                BombPickupState pickup = battle.BombPickups[i];
                long distance = Math.Abs(
                    (long)pickup.X - battle.PlayerX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    targetY = pickup.Y;
                }
            }
            return bestDistance != long.MaxValue;
        }

        static int SelectBossTargetY(
            StagePlan stage,
            IBattleSim battle)
        {
            BossState boss = battle.Boss;
            for (int i = 0; i < battle.BossParts.Count; i++)
            {
                BossPartState part = battle.BossParts[i];
                if (!part.Destroyed
                    && part.IsCore
                    && !part.Invulnerable)
                    return part.Y;
            }

            for (int definitionIndex = 0;
                definitionIndex < stage.BossParts.Count;
                definitionIndex++)
            {
                BossPartDefinition definition =
                    stage.BossParts[definitionIndex];
                if (!definition.IsCore)
                    continue;
                for (int gate = 0;
                    gate < definition.CoreGatePartIds.Count;
                    gate++)
                {
                    string gateId =
                        definition.CoreGatePartIds[gate];
                    for (int stateIndex = 0;
                        stateIndex < battle.BossParts.Count;
                        stateIndex++)
                    {
                        BossPartState state =
                            battle.BossParts[stateIndex];
                        if (!state.Destroyed
                            && !state.Invulnerable
                            && string.Equals(
                                state.PartId,
                                gateId,
                                StringComparison.Ordinal))
                            return state.Y;
                    }
                }
            }

            for (int definitionIndex = 0;
                definitionIndex < stage.BossParts.Count;
                definitionIndex++)
            {
                BossPartDefinition definition =
                    stage.BossParts[definitionIndex];
                if (definition.RegenerationTicks != 0)
                    continue;
                for (int stateIndex = 0;
                    stateIndex < battle.BossParts.Count;
                    stateIndex++)
                {
                    BossPartState state =
                        battle.BossParts[stateIndex];
                    if (!state.Destroyed
                        && !state.Invulnerable
                        && string.Equals(
                            state.PartId,
                            definition.PartId,
                            StringComparison.Ordinal))
                        return state.Y;
                }
            }

            for (int i = 0; i < battle.BossParts.Count; i++)
            {
                BossPartState part = battle.BossParts[i];
                if (!part.Destroyed && !part.Invulnerable)
                    return part.Y;
            }
            return boss.Y;
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

        static ShipDefinition CreateAuditShip(
            ShipDefinition source,
            int mainShotLevel)
        {
            if (source == null)
                source = ShipDefinition.CreateDefault();
            int[] startingLevels =
                source.ExportStartingPowerUpLevels();
            startingLevels[(int)PowerUpSlot.MainShot] =
                mainShotLevel;
            PrimaryWeaponFamily family =
                source.GaugeWeaponFamily
                ?? PrimaryWeaponFamily.Double;
            PowerUpSlot weaponSlot =
                ShipDefinition.GaugeSlotForFamily(family);
            return new ShipDefinition(
                source.Id,
                source.DisplayName,
                source.MoveSpeedMultiplierNumerator,
                source.MoveSpeedMultiplierDenominator,
                startingLevels,
                source.UnlockCost,
                source.WeaponType,
                null,
                family,
                new[]
                {
                    PowerUpSlot.Speed,
                    PowerUpSlot.Missile,
                    weaponSlot,
                    PowerUpSlot.Option,
                    PowerUpSlot.Shield
                });
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
                int completedRooms,
                int rewardChoices,
                int routeChoices,
                int cappedChoices,
                int finalStage,
                RunState finalState,
                int biomeIndex,
                int roomIndex,
                int battleTick,
                int bossHp,
                int bossMaxHp,
                RunCompletionGrade completionGrade,
                int hiddenConditions)
            {
                Scenario = scenario;
                Hash = hash;
                ExecutedTicks = executedTicks;
                CompletedStages = completedStages;
                CompletedRooms = completedRooms;
                RewardChoices = rewardChoices;
                RouteChoices = routeChoices;
                CappedChoices = cappedChoices;
                FinalStage = finalStage;
                FinalState = finalState;
                BiomeIndex = biomeIndex;
                RoomIndex = roomIndex;
                BattleTick = battleTick;
                BossHp = bossHp;
                BossMaxHp = bossMaxHp;
                CompletionGrade = completionGrade;
                HiddenConditions = hiddenConditions;
            }

            public AuditScenario Scenario { get; }
            public ulong Hash { get; }
            public int ExecutedTicks { get; }
            public int CompletedStages { get; }
            public int CompletedRooms { get; }
            public int RewardChoices { get; }
            public int RouteChoices { get; }
            public int CappedChoices { get; }
            public int FinalStage { get; }
            public RunState FinalState { get; }
            public int BiomeIndex { get; }
            public int RoomIndex { get; }
            public int BattleTick { get; }
            public int BossHp { get; }
            public int BossMaxHp { get; }
            public RunCompletionGrade CompletionGrade { get; }
            public int HiddenConditions { get; }

            public bool Matches(ScenarioResult other)
            {
                return other != null
                    && Hash == other.Hash
                    && ExecutedTicks == other.ExecutedTicks
                    && CompletedStages == other.CompletedStages
                    && CompletedRooms == other.CompletedRooms
                    && RewardChoices == other.RewardChoices
                    && RouteChoices == other.RouteChoices
                    && CappedChoices == other.CappedChoices
                    && FinalStage == other.FinalStage
                    && FinalState == other.FinalState
                    && BiomeIndex == other.BiomeIndex
                    && RoomIndex == other.RoomIndex
                    && BattleTick == other.BattleTick
                    && BossHp == other.BossHp
                    && BossMaxHp == other.BossMaxHp
                    && CompletionGrade == other.CompletionGrade
                    && HiddenConditions == other.HiddenConditions;
            }

            public string Format()
            {
                return $"name={Scenario.Name} hash={Hash:X16} "
                    + $"seed={Scenario.Seed} strategy={Scenario.Strategy} "
                    + $"completedStages={CompletedStages}/{Scenario.StageCount} "
                    + $"completedRooms={CompletedRooms}/"
                    + $"{Scenario.StageCount * RunProgressionConfig.DefaultRoomsPerBiome} "
                    + $"ticks={ExecutedTicks} rewardChoices={RewardChoices} "
                    + $"routeChoices={RouteChoices} "
                    + $"cappedChoices={CappedChoices} "
                    + $"finalStage={FinalStage} state={FinalState} "
                    + $"biome={BiomeIndex} room={RoomIndex} "
                    + $"battleTick={BattleTick} "
                    + $"bossHp={BossHp}/{BossMaxHp} "
                    + $"grade={CompletionGrade} "
                    + $"hiddenConditions={HiddenConditions}";
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

        /// <summary>
        /// Keeps the audit runnable before the content-owned colossal boss rows land.
        /// Normal route generation always delegates to parsed GameData. Colossal rows
        /// also delegate as soon as both approved boss ids exist in that catalog.
        /// </summary>
        sealed class AuditStageGenerator :
            IRouteStageGenerator,
            ISectionRouteStageGenerator,
            IColossalBossStageGenerator
        {
            const int U = SimSpace.SubUnitsPerWorldUnit;
            static readonly BossPhase[] DormantLegacyPhases =
            {
                new BossPhase(3_600, 1, U, 60)
            };

            readonly SegmentStageGenerator _inner;
            readonly string _spawnEnemyId;

            public AuditStageGenerator(GameDataSet data)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));
                _inner = new SegmentStageGenerator(data.StageGeneration);
                if (data.BattleContent.Enemies.Count == 0)
                    throw new InvalidOperationException(
                        "The colossal audit requires at least one enemy definition.");
                _spawnEnemyId = data.BattleContent.Enemies[0].Id;
            }

            public System.Collections.Generic.IReadOnlyList<string> ThemeIds
                => _inner.ThemeIds;

            public System.Collections.Generic.IReadOnlyList<string> GetThemeOrder(
                ulong seed)
            {
                return _inner.GetThemeOrder(seed);
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                return _inner.Generate(seed, stageIndex, difficulty);
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                return _inner.CanGenerateRoute(
                    themeId,
                    stageIndex,
                    difficulty,
                    encounterType);
            }

            public StagePlan GenerateRoute(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType)
            {
                return _inner.GenerateRoute(
                    seed,
                    stageIndex,
                    difficulty,
                    themeId,
                    encounterType);
            }

            public bool CanGenerateRouteForSection(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType,
                StageRouteSection section)
            {
                return _inner.CanGenerateRouteForSection(
                    themeId,
                    stageIndex,
                    difficulty,
                    encounterType,
                    section);
            }

            public StagePlan GenerateRouteForSection(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType,
                StageRouteSection section)
            {
                return _inner.GenerateRouteForSection(
                    seed,
                    stageIndex,
                    difficulty,
                    themeId,
                    encounterType,
                    section);
            }

            public bool CanGenerateColossalBoss(ColossalBossKind kind)
            {
                return kind == ColossalBossKind.Leviathan
                    || kind == ColossalBossKind.Broodmother;
            }

            public string GetColossalBossThemeId(ColossalBossKind kind)
            {
                // 카탈로그가 답을 알면 그대로, 폴백 플랜에는 전용 테마가 없다.
                return _inner.CanGenerateColossalBoss(kind)
                    ? _inner.GetColossalBossThemeId(kind)
                    : null;
            }

            public StagePlan GenerateColossalBoss(
                ulong seed,
                int stageIndex,
                int difficulty,
                ColossalBossKind kind)
            {
                if (_inner.CanGenerateColossalBoss(kind))
                {
                    return _inner.GenerateColossalBoss(
                        seed,
                        stageIndex,
                        difficulty,
                        kind);
                }

                if (!CanGenerateColossalBoss(kind))
                    throw new ArgumentOutOfRangeException(nameof(kind));
                return CreateFallbackColossalPlan(kind);
            }

            StagePlan CreateFallbackColossalPlan(ColossalBossKind kind)
            {
                BossPartDefinition[] parts = kind == ColossalBossKind.Leviathan
                    ? CreateLeviathanParts()
                    : CreateBroodmotherParts();
                string bossId = kind == ColossalBossKind.Leviathan
                    ? SegmentStageGenerator.LeviathanBossId
                    : SegmentStageGenerator.BroodmotherBossId;
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "audit_hidden_approach",
                            1,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
                    bossId,
                    1,
                    1,
                    1,
                    62_000,
                    5 * U,
                    5 * U,
                    14 * U,
                    DormantLegacyPhases,
                    null,
                    null,
                    EncounterType.Normal,
                    parts);
            }

            BossPartDefinition[] CreateLeviathanParts()
            {
                return new[]
                {
                    Part(
                        "shield_generator", 0, 0, 2, 2, 10_000,
                        false, null, BossPartAttackProfile.None, 0),
                    Part(
                        "upper_turret", 0, 3, 2, 1, 6_000,
                        false, null, Projectile(
                            BossPartAttackType.AimedSpread, 72, 3, 7), 0),
                    Part(
                        "lower_launcher", 0, -3, 2, 1, 6_000,
                        false, null, Projectile(
                            BossPartAttackType.AimedSpread, 90, 5, 6), 0),
                    Part(
                        "front_claw", -3, 0, 2, 2, 8_000,
                        false, null, Movement(
                            BossPartAttackType.MeleeCharge, 240, 10), 0),
                    Part(
                        "engine", 3, 0, 2, 2, 7_000,
                        false, null, Movement(
                            BossPartAttackType.VerticalMovement, 180, 3), 0),
                    Part(
                        "core", 0, 0, 2, 2, 25_000,
                        true, new[] { "shield_generator" }, Projectile(
                            BossPartAttackType.RadialSpread, 96, 8, 6), 0)
                };
            }

            BossPartDefinition[] CreateBroodmotherParts()
            {
                var spawn = new BossPartAttackProfile(
                    BossPartAttackType.SpawnEnemy,
                    8 * SimSpace.TicksPerSecond,
                    0,
                    0,
                    1,
                    0,
                    1,
                    _spawnEnemyId);
                return new[]
                {
                    Part(
                        "spawn_sac_left", 0, 3, 2, 1, 6_000,
                        false, null, spawn, 0),
                    Part(
                        "spawn_sac_center", 0, 0, 2, 1, 6_000,
                        false, null, spawn, 0),
                    Part(
                        "spawn_sac_right", 0, -3, 2, 1, 6_000,
                        false, null, spawn, 0),
                    Part(
                        "tentacle_left", -3, 3, 1, 2, 5_000,
                        false, null, Movement(
                            BossPartAttackType.MeleeCharge, 210, 8),
                        20 * SimSpace.TicksPerSecond),
                    Part(
                        "tentacle_right", -3, -3, 1, 2, 5_000,
                        false, null, Projectile(
                            BossPartAttackType.AimedSpread, 84, 5, 6),
                        20 * SimSpace.TicksPerSecond),
                    Part(
                        "maw", 2, 0, 2, 2, 9_000,
                        false, null, new BossPartAttackProfile(
                            BossPartAttackType.Suction,
                            1,
                            0,
                            0,
                            1,
                            3 * U,
                            SimSpace.TicksPerSecond,
                            null),
                        0),
                    Part(
                        "heart", 0, 0, 2, 2, 25_000,
                        true,
                        new[]
                        {
                            "spawn_sac_left",
                            "spawn_sac_center",
                            "spawn_sac_right"
                        },
                        Projectile(
                            BossPartAttackType.RadialSpread, 90, 8, 6),
                        0)
                };
            }

            static BossPartDefinition Part(
                string id,
                int offsetX,
                int offsetY,
                int halfWidth,
                int halfHeight,
                int hp,
                bool isCore,
                string[] gates,
                BossPartAttackProfile attack,
                int regenerationTicks)
            {
                return new BossPartDefinition(
                    id,
                    offsetX * U,
                    offsetY * U,
                    halfWidth * U,
                    halfHeight * U,
                    hp,
                    isCore,
                    gates,
                    attack,
                    regenerationTicks);
            }

            static BossPartAttackProfile Projectile(
                BossPartAttackType type,
                int intervalTicks,
                int ways,
                int worldUnitsPerSecond)
            {
                return new BossPartAttackProfile(
                    type,
                    intervalTicks,
                    ways,
                    worldUnitsPerSecond * U,
                    SimSpace.TicksPerSecond,
                    0,
                    1,
                    null);
            }

            static BossPartAttackProfile Movement(
                BossPartAttackType type,
                int intervalTicks,
                int worldUnitsPerSecond)
            {
                return new BossPartAttackProfile(
                    type,
                    intervalTicks,
                    0,
                    0,
                    1,
                    worldUnitsPerSecond * U,
                    SimSpace.TicksPerSecond,
                    null,
                    type == BossPartAttackType.MeleeCharge ? 1 : 0);
            }
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
