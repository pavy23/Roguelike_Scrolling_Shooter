using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class RunCompletionTests
    {
        [Test]
        public void FinalBossClear_FreezesRunAndFinalStatistics()
        {
            RunManager run = CreateRun(7UL, 1);

            DrivePlayingTicks(run, 500);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);
            run.ChooseReward(0);
            Assert.AreEqual(
                RunState.AwaitingContract,
                run.State);
            Assert.IsTrue(run.ChooseContract(0));

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.IsTrue(run.IsFinished);
            Assert.AreEqual(1, run.StageIndex);
            Assert.AreEqual(run.BiomeCount, run.BiomeIndex);
            Assert.LessOrEqual(run.BiomeIndex, run.BiomeCount);
            Assert.AreEqual(1, run.Statistics.StagesCleared);
            Assert.AreEqual(0, run.RewardOptions.Count);
            Assert.AreEqual(0, run.RouteOptions.Count);

            int stoppedTick = run.Battle.Tick;
            long stoppedScore = run.TotalScore;
            RunStatistics stoppedStatistics = run.Statistics;
            for (int i = 0; i < 20; i++)
            {
                var input = new InputCommand(1, 1, true, true);
                run.Step(in input);
            }

            Assert.AreEqual(stoppedTick, run.Battle.Tick);
            Assert.AreEqual(stoppedScore, run.TotalScore);
            Assert.AreEqual(
                stoppedStatistics.ShotsFired,
                run.Statistics.ShotsFired);
            Assert.AreEqual(
                stoppedStatistics.ShotsHit,
                run.Statistics.ShotsHit);
            Assert.AreEqual(
                stoppedStatistics.Kills,
                run.Statistics.Kills);
            Assert.AreEqual(
                stoppedStatistics.StagesCleared,
                run.Statistics.StagesCleared);
        }

        [Test]
        public void SuspendAndInputReplay_ReproduceRunClearedBoundary()
        {
            const ulong seed = 0xC1EA4UL;
            RunManager source = CreateRun(seed, 1);
            RunSuspendData suspend = source.ExportSuspendData();
            var recorder = new InputRecorder(source);

            for (int i = 0;
                i < 500 && source.State == RunState.Playing;
                i++)
            {
                var input = new InputCommand(0, 0, true);
                recorder.Record(in input);
                source.Step(in input);
            }
            InputRecordingData recording = recorder.Export();

            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new CompletingRouteGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault());
            var playback = new InputPlayback(recording);
            RunManager replay = CreateRun(
                seed,
                playback.FinalStageIndex);
            foreach (InputCommand input in playback)
            {
                resumed.Step(in input);
                replay.Step(in input);
            }
            source.ChooseReward(0);
            resumed.ChooseReward(0);
            replay.ChooseReward(0);
            source.ChooseContract(0);
            resumed.ChooseContract(0);
            replay.ChooseContract(0);

            Assert.AreEqual(RunState.RunCleared, source.State);
            Assert.AreEqual(RunState.RunCleared, resumed.State);
            Assert.AreEqual(RunState.RunCleared, replay.State);
            Assert.AreEqual(1, suspend.finalStageIndex);
            Assert.AreEqual(1, playback.FinalStageIndex);
            AssertRunHashEqual(source, resumed);
            AssertRunHashEqual(source, replay);
        }

        [Test]
        public void DeterminismAuditSmoke_ConsumesRewardsAndRoutesToVictory()
        {
            AuditTrace first = RunAuditTrace(0x51A7EUL);
            AuditTrace second = RunAuditTrace(0x51A7EUL);

            Assert.AreEqual(RunState.RunCleared, first.FinalState);
            Assert.AreEqual(5, first.StagesCleared);
            Assert.AreEqual(10, first.RewardChoices);
            Assert.AreEqual(0, first.RouteChoices);
            Assert.AreEqual(15, first.RoomsCleared);
            Assert.AreEqual(31, first.ObservedGimmickMask);
            Assert.AreEqual(first.Hash, second.Hash);
            Assert.AreEqual(first.Ticks, second.Ticks);
            Assert.AreEqual(
                first.RewardChoices,
                second.RewardChoices);
            Assert.AreEqual(first.RouteChoices, second.RouteChoices);
            Assert.AreEqual(first.RoomsCleared, second.RoomsCleared);
            Assert.AreEqual(
                first.ObservedGimmickMask,
                second.ObservedGimmickMask);
            TestContext.WriteLine(
                $"determinism hash={first.Hash:X16}, ticks={first.Ticks}");
        }

        [Test]
        public void FullFifteenRoomRun_NeverStallsInAnEmptyChoiceState()
        {
            AuditTrace trace = RunAuditTrace(0x48A15UL);

            Assert.AreEqual(RunState.RunCleared, trace.FinalState);
            Assert.AreEqual(15, trace.RoomsCleared);
            Assert.AreEqual(0, trace.RouteChoices);
            Assert.Less(trace.MaximumChoiceStateIterations, 8);
        }

        [Test]
        public void CurrentMiniBossContent_FullRhythmRunTakesDamageAndCrossesBossPhasesDeterministically()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "rewards.json")));
            RhythmTrace first = RunRhythmTrace(data, 0x48DA7AUL);
            RhythmTrace second = RunRhythmTrace(data, 0x48DA7AUL);
            TestContext.WriteLine(
                $"state={first.FinalState}, rooms={first.RoomsCleared}, "
                + $"damage={first.DamageEvents}, p1={first.PhaseOneEvents}, "
                + $"p2={first.PhaseTwoEvents}, ticks={first.Ticks}");

            AssertAll(() =>
            {
                Assert.AreEqual(RunState.RunCleared, first.FinalState);
                Assert.AreEqual(15, first.RoomsCleared);
                Assert.AreEqual(5, first.MidRewards);
                Assert.AreEqual(5, first.MainRewards);
                Assert.AreEqual(5, first.MidBossEncounters);
                Assert.Greater(first.DamageEvents, 0);
                Assert.GreaterOrEqual(first.PhaseOneEvents, 5);
                Assert.GreaterOrEqual(first.PhaseTwoEvents, 5);
                Assert.AreEqual(0, first.RouteChoices);
                Assert.AreEqual(first.Hash, second.Hash);
                Assert.AreEqual(first.Ticks, second.Ticks);
                Assert.AreEqual(
                    first.DamageEvents,
                    second.DamageEvents);
                Assert.AreEqual(
                    first.PhaseOneEvents,
                    second.PhaseOneEvents);
                Assert.AreEqual(
                    first.PhaseTwoEvents,
                    second.PhaseTwoEvents);
            });
        }

        [Test]
        public void RhythmRun_WithMidStageSpeedSlotReward_CompletesDeterministically()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")),
                File.ReadAllText(
                    Path.Combine(gameData, "rewards.json")));
            RewardCatalog rewards =
                EnsureMidSpeedSlotReward(data.Rewards);

            RhythmTrace first =
                RunRhythmTrace(data, 0x48DA7AUL, rewards);
            RhythmTrace second =
                RunRhythmTrace(data, 0x48DA7AUL, rewards);

            AssertAll(() =>
            {
                Assert.AreEqual(RunState.RunCleared, first.FinalState);
                Assert.AreEqual(15, first.RoomsCleared);
                Assert.AreEqual(first.Hash, second.Hash);
                Assert.AreEqual(first.Ticks, second.Ticks);
            });
        }

        [Test]
        public void ProgressionConfig_DefaultsToFiveAndSupportsOtherCampaignLengths()
        {
            RunManager defaultRun = CreateRun(11UL, null);
            RunManager twoStageRun = CreateRun(11UL, 2);

            Assert.AreEqual(
                RunProgressionConfig.DefaultFinalStageIndex,
                defaultRun.FinalStageIndex);
            Assert.AreEqual(2, twoStageRun.FinalStageIndex);

            DrivePlayingTicks(twoStageRun, 500);
            Assert.AreEqual(
                RunState.AwaitingReward,
                twoStageRun.State);
            Assert.AreEqual(3, twoStageRun.RewardOptions.Count);
            twoStageRun.ChooseReward(0);
            Assert.IsTrue(twoStageRun.ChooseContract(0));
            DrivePlayingTicks(twoStageRun, 500);
            Assert.AreEqual(
                RunState.AwaitingReward,
                twoStageRun.State);
            twoStageRun.ChooseReward(0);
            Assert.IsTrue(twoStageRun.ChooseContract(0));

            Assert.AreEqual(RunState.RunCleared, twoStageRun.State);
            Assert.AreEqual(2, twoStageRun.Statistics.StagesCleared);
        }

        static AuditTrace RunAuditTrace(ulong seed)
        {
            RunManager run = CreateRun(seed, null);
            var hasher = new DeterminismAuditHasher();
            int ticks = 0;
            int rewards = 0;
            int routes = 0;
            RunState previousChoiceState = RunState.Playing;
            int choiceStateIterations = 0;
            int maximumChoiceStateIterations = 0;
            int observedGimmickMask = 0;
            hasher.FoldRunState(run);

            for (int guard = 0; guard < 5_000 && !run.IsFinished; guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    int expectedCount =
                        run.RewardSelectionKind
                            == RewardSelectionKind.MidStage
                                ? RunManager.MidStageRewardOptionCount
                                : RunManager.MainRewardOptionCount;
                    Assert.AreEqual(
                        expectedCount,
                        run.RewardOptions.Count,
                        "AwaitingReward cannot expose an empty card list.");
                    TrackChoiceState(
                        run.State,
                        ref previousChoiceState,
                        ref choiceStateIterations,
                        ref maximumChoiceStateIterations);
                    int optionIndex =
                        (run.StageIndex + rewards)
                        % run.RewardOptions.Count;
                    RewardOption option = run.RewardOptions[optionIndex];
                    hasher.FoldRewardChoice(
                        run.StageIndex,
                        optionIndex,
                        in option);
                    run.ChooseReward(optionIndex);
                    rewards++;
                }
                else if (run.State
                    == RunState.AwaitingContract)
                {
                    Assert.Greater(run.ContractOptions.Count, 0);
                    run.ChooseContract(0);
                }
                else
                {
                    previousChoiceState = RunState.Playing;
                    choiceStateIterations = 0;
                    var input = new InputCommand(0, 0, true);
                    run.Step(in input);
                    ticks++;
                    ObserveGimmicks(
                        run.Battle,
                        ref observedGimmickMask);
                }
                hasher.FoldRunState(run);
            }

            return new AuditTrace(
                hasher.Hash,
                ticks,
                rewards,
                routes,
                run.Statistics.StagesCleared,
                run.Statistics.RoomsCleared,
                maximumChoiceStateIterations,
                observedGimmickMask,
                run.State);
        }

        static void ObserveGimmicks(
            IBattleSim battle,
            ref int observedMask)
        {
            if (battle.Environment.HasCorridor)
                observedMask |= 1 << 1;
            if (battle.Lasers.Count != 0)
                observedMask |= 1 << 2;
            if (battle.VisionObscured
                && battle.Environment.HasDrift)
                observedMask |= 1 << 3;
            if (battle.TimeLimitTicks != 0)
                observedMask |= 1 << 4;
            ReadOnlySpan<SimEvent> events =
                battle.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type
                    == SimEventType.ObstacleDestroyed)
                {
                    observedMask |= 1;
                    break;
                }
        }

        static RhythmTrace RunRhythmTrace(
            GameDataSet data,
            ulong seed,
            RewardCatalog rewards = null)
        {
            BattleContent content = CreateRhythmContent(data);
            BattleSimConfig config = CreateRhythmConfig();
            var run = new RunManager(
                seed,
                new RhythmRunGenerator("damage_probe"),
                config,
                content,
                data.CreatePowerUpGauge(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards ?? data.Rewards,
                null,
                1,
                1,
                RunProgressionConfig.CreateDefault());
            var hasher = new DeterminismAuditHasher();
            int ticks = 0;
            int midRewards = 0;
            int mainRewards = 0;
            int midBossEncounters = 0;
            int damageEvents = 0;
            int phaseOneEvents = 0;
            int phaseTwoEvents = 0;
            int previousMidBossBiome = 0;
            int dodgeDirection = 1;
            hasher.FoldRunState(run);

            for (int guard = 0;
                guard < 50_000 && !run.IsFinished;
                guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    int expectedCount =
                        run.RewardSelectionKind
                            == RewardSelectionKind.MidStage
                                ? RunManager.MidStageRewardOptionCount
                                : RunManager.MainRewardOptionCount;
                    Assert.AreEqual(expectedCount, run.RewardOptions.Count);
                    if (run.RewardSelectionKind
                        == RewardSelectionKind.MidStage)
                        midRewards++;
                    else
                        mainRewards++;
                    RewardOption option = run.RewardOptions[0];
                    hasher.FoldRewardChoice(
                        run.StageIndex,
                        0,
                        in option);
                    run.ChooseReward(0);
                }
                else if (run.State
                    == RunState.AwaitingContract)
                {
                    Assert.Greater(run.ContractOptions.Count, 0);
                    run.ChooseContract(0);
                }
                else
                {
                    Assert.AreEqual(RunState.Playing, run.State);
                    if (run.StageSection == RunStageSection.MidBoss
                        && run.BiomeIndex != previousMidBossBiome)
                    {
                        StringAssert.StartsWith(
                            "mini_",
                            run.StagePlan.BossId);
                        previousMidBossBiome = run.BiomeIndex;
                        midBossEncounters++;
                    }

                    int shieldBefore = run.Battle.ShieldStock;
                    dodgeDirection = run.Battle.BossActive
                        ? GetRhythmBossAimDirection(run.Battle)
                        : GetRhythmDodgeDirection(
                            run.Battle,
                            dodgeDirection);
                    var fire = new InputCommand(
                        0,
                        dodgeDirection,
                        true);
                    run.Step(in fire);
                    ticks++;
                    if (run.Battle.ShieldStock < shieldBefore)
                        damageEvents++;
                    ReadOnlySpan<SimEvent> events =
                        run.Battle.EventsThisTick;
                    for (int i = 0; i < events.Length; i++)
                    {
                        if (events[i].Type
                            != SimEventType.BossPhaseChanged)
                            continue;
                        if (events[i].Arg == 1)
                            phaseOneEvents++;
                        else if (events[i].Arg == 2)
                            phaseTwoEvents++;
                    }
                }
                hasher.FoldRunState(run);
            }

            return new RhythmTrace(
                hasher.Hash,
                ticks,
                midRewards,
                mainRewards,
                midBossEncounters,
                damageEvents,
                phaseOneEvents,
                phaseTwoEvents,
                run.RouteChoiceHistory.Count,
                run.Statistics.RoomsCleared,
                run.State);
        }

        static RewardCatalog EnsureMidSpeedSlotReward(
            RewardCatalog source)
        {
            var replacements =
                new RewardDefinition[source.All.Count];
            bool found = false;
            for (int i = 0; i < replacements.Length; i++)
            {
                RewardDefinition reward = source.All[i];
                if (string.Equals(
                        reward.Id,
                        "slot_speed_1",
                        StringComparison.Ordinal))
                {
                    Assert.AreEqual(RewardType.SlotLevel, reward.Type);
                    Assert.AreEqual(PowerUpSlot.Speed, reward.Slot);
                    replacements[i] = reward;
                    found = true;
                    continue;
                }
                if (!string.Equals(
                        reward.Id,
                        "passive_move_speed_1",
                        StringComparison.Ordinal))
                {
                    replacements[i] = reward;
                    continue;
                }

                replacements[i] = new RewardDefinition(
                    "slot_speed_1",
                    RewardType.SlotLevel,
                    PowerUpSlot.Speed,
                    1,
                    reward.Weight,
                    reward.StageIndexMin,
                    reward.StageIndexMax,
                    reward.MaxPerRun,
                    pool: reward.Pool);
                found = true;
            }
            Assert.IsTrue(
                found,
                "The rhythm regression fixture requires the mid speed reward.");
            return new RewardCatalog(
                source.OptionCount,
                replacements,
                source.MaxCombinedModifierCost,
                source.RerollCost);
        }

        static int GetRhythmDodgeDirection(
            IBattleSim battle,
            int currentDirection)
        {
            int nearestThreatDistance = int.MaxValue;
            int direction = currentDirection;
            for (int i = 0; i < battle.Bullets.Count; i++)
            {
                BulletState bullet = battle.Bullets[i];
                if (bullet.Faction != BulletFaction.Enemy)
                    continue;
                int distanceX = bullet.X - battle.PlayerX;
                int distanceY = Math.Abs(bullet.Y - battle.PlayerY);
                if (distanceX < 0
                    || distanceX > 1024
                    || distanceY > 256
                    || distanceX >= nearestThreatDistance)
                    continue;
                nearestThreatDistance = distanceX;
                direction = bullet.Y >= battle.PlayerY ? -1 : 1;
            }

            if (battle.PlayerY >= 640 && direction > 0)
                return -1;
            if (battle.PlayerY <= -640 && direction < 0)
                return 1;
            return direction;
        }

        static int GetRhythmBossAimDirection(IBattleSim battle)
        {
            int tolerance = SimSpace.SubUnitsPerWorldUnit / 4;
            if (battle.PlayerY < battle.Boss.Y - tolerance)
                return 1;
            if (battle.PlayerY > battle.Boss.Y + tolerance)
                return -1;
            return 0;
        }

        static void TrackChoiceState(
            RunState state,
            ref RunState previous,
            ref int iterations,
            ref int maximum)
        {
            iterations = state == previous
                ? iterations + 1
                : 1;
            previous = state;
            maximum = Math.Max(maximum, iterations);
            Assert.Less(
                iterations,
                8,
                $"Run remained in {state} without returning to Playing.");
        }

        static void DrivePlayingTicks(RunManager run, int maximum)
        {
            var fire = new InputCommand(0, 0, true);
            for (int i = 0;
                i < maximum && run.State == RunState.Playing;
                i++)
            {
                run.Step(in fire);
            }
        }

        static void DriveWholeRun(RunManager run, int maximum)
        {
            var fire = new InputCommand(0, 0, true);
            for (int guard = 0;
                guard < maximum && !run.IsFinished;
                guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    int expectedCount =
                        run.RewardSelectionKind
                            == RewardSelectionKind.MidStage
                                ? RunManager.MidStageRewardOptionCount
                                : RunManager.MainRewardOptionCount;
                    Assert.AreEqual(expectedCount, run.RewardOptions.Count);
                    run.ChooseReward(0);
                }
                else if (run.State
                    == RunState.AwaitingContract)
                {
                    Assert.Greater(run.ContractOptions.Count, 0);
                    run.ChooseContract(0);
                }
                else
                {
                    Assert.AreEqual(RunState.Playing, run.State);
                    run.Step(in fire);
                }
            }
        }

        static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (Directory.Exists(
                    Path.Combine(directory.FullName, "GameData")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the repository GameData directory.");
        }

        static RunManager CreateRun(ulong seed, int? finalStageIndex)
        {
            RunProgressionConfig progression = finalStageIndex.HasValue
                ? new RunProgressionConfig(finalStageIndex.Value)
                : RunProgressionConfig.CreateDefault();
            return new RunManager(
                seed,
                new CompletingRouteGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                null,
                null,
                1,
                1,
                progression);
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerMaxHp = 100;
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
            return config;
        }

        static BattleContent CreateContent()
        {
            var weapon = new WeaponDefinition(
                "completion_shot",
                1,
                1,
                256,
                1,
                0,
                0);
            var tentacle = new EnemyDefinition(
                "completion_tentacle",
                1,
                1,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            return new BattleContent(
                new[] { tentacle },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateRhythmContent(GameDataSet data)
        {
            var damageProbe = new EnemyDefinition(
                "damage_probe",
                "Damage Probe",
                1,
                1,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                128,
                128,
                0,
                1,
                64);
            var enemies =
                new EnemyDefinition[data.BattleContent.Enemies.Count + 1];
            for (int i = 0;
                i < data.BattleContent.Enemies.Count;
                i++)
                enemies[i] = data.BattleContent.Enemies[i];
            enemies[enemies.Length - 1] = damageProbe;
            return new BattleContent(
                enemies,
                data.BattleContent.Weapons,
                data.BattleContent.PlayerWeapon.Id,
                data.BattleContent.PrimaryWeaponFamilies,
                data.BattleContent.MissileFamilies,
                data.BattleContent.DefaultMissileFamily,
                data.BattleContent.OptionFormations,
                data.BattleContent.DefaultOptionFormation);
        }

        static BattleSimConfig CreateRhythmConfig()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerMinX = 0;
            config.PlayerMaxX = 0;
            config.PlayerMinY = -768;
            config.PlayerMaxY = 768;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.UseConfiguredMainShotStats = true;
            config.MainShotBaseDamage = 1;
            config.FireIntervalTicks = 1;
            config.PlayerBulletSpeedPerTick = 100;
            config.MainShotHalfWidth = 20;
            config.MainShotHalfHeight = 768;
            config.StartingShieldStock = 20;
            config.MaxShieldStock = 20;
            config.BulletDespawnX = 2000;
            config.EnemyDespawnX = -2000;
            config.EnemyBulletDamage = 0;
            config.MaxEnemyBullets = 128;
            return config;
        }

        static void AssertAll(Action assert) => assert();

        static void AssertRunHashEqual(
            RunManager expected,
            RunManager actual)
        {
            var expectedHash = new DeterminismAuditHasher();
            var actualHash = new DeterminismAuditHasher();
            expectedHash.FoldRunState(expected);
            actualHash.FoldRunState(actual);
            Assert.AreEqual(expectedHash.Hash, actualHash.Hash);
        }

        readonly struct AuditTrace
        {
            public AuditTrace(
                ulong hash,
                int ticks,
                int rewardChoices,
                int routeChoices,
                int stagesCleared,
                int roomsCleared,
                int maximumChoiceStateIterations,
                int observedGimmickMask,
                RunState finalState)
            {
                Hash = hash;
                Ticks = ticks;
                RewardChoices = rewardChoices;
                RouteChoices = routeChoices;
                StagesCleared = stagesCleared;
                RoomsCleared = roomsCleared;
                MaximumChoiceStateIterations =
                    maximumChoiceStateIterations;
                ObservedGimmickMask = observedGimmickMask;
                FinalState = finalState;
            }

            public ulong Hash { get; }
            public int Ticks { get; }
            public int RewardChoices { get; }
            public int RouteChoices { get; }
            public int StagesCleared { get; }
            public int RoomsCleared { get; }
            public int MaximumChoiceStateIterations { get; }
            public int ObservedGimmickMask { get; }
            public RunState FinalState { get; }
        }

        readonly struct RhythmTrace
        {
            public RhythmTrace(
                ulong hash,
                int ticks,
                int midRewards,
                int mainRewards,
                int midBossEncounters,
                int damageEvents,
                int phaseOneEvents,
                int phaseTwoEvents,
                int routeChoices,
                int roomsCleared,
                RunState finalState)
            {
                Hash = hash;
                Ticks = ticks;
                MidRewards = midRewards;
                MainRewards = mainRewards;
                MidBossEncounters = midBossEncounters;
                DamageEvents = damageEvents;
                PhaseOneEvents = phaseOneEvents;
                PhaseTwoEvents = phaseTwoEvents;
                RouteChoices = routeChoices;
                RoomsCleared = roomsCleared;
                FinalState = finalState;
            }

            public ulong Hash { get; }
            public int Ticks { get; }
            public int MidRewards { get; }
            public int MainRewards { get; }
            public int MidBossEncounters { get; }
            public int DamageEvents { get; }
            public int PhaseOneEvents { get; }
            public int PhaseTwoEvents { get; }
            public int RouteChoices { get; }
            public int RoomsCleared { get; }
            public RunState FinalState { get; }
        }

        sealed class CompletingRouteGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
                {
                    "scrapyard",
                    "bioHive",
                    "fortress",
                    "nebula",
                    "core"
                };
            static readonly BossPhase[] Phases =
                { new BossPhase(999, 1, 1, 1) };

            public IReadOnlyList<string> ThemeIds => Themes;

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return Plan(Themes[(stageIndex - 1) % Themes.Length]);
            }

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                return Array.AsReadOnly((string[])Themes.Clone());
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                return Array.IndexOf(Themes, themeId) >= 0;
            }

            public StagePlan GenerateRoute(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType)
            {
                return Plan(themeId);
            }

            static StagePlan Plan(string themeId)
            {
                SegmentEnvironmentDefinition environment =
                    SegmentEnvironmentDefinition.None;
                SpawnEvent[] spawns =
                    Array.Empty<SpawnEvent>();
                ObstacleSpawn[] obstacles =
                    Array.Empty<ObstacleSpawn>();
                StageGimmickDefinition gimmick =
                    StageGimmickDefinition.None;

                if (string.Equals(
                        themeId,
                        "scrapyard",
                        StringComparison.Ordinal))
                {
                    obstacles = new[]
                    {
                        new ObstacleSpawn(
                            ObstacleType.Breakable,
                            256,
                            0,
                            1)
                    };
                }
                else if (string.Equals(
                    themeId,
                    "bioHive",
                    StringComparison.Ordinal))
                {
                    environment = Corridor();
                    spawns = new[]
                    {
                        new SpawnEvent(
                            0,
                            "completion_tentacle",
                            256,
                            0)
                    };
                }
                else if (string.Equals(
                    themeId,
                    "fortress",
                    StringComparison.Ordinal))
                {
                    obstacles = new[]
                    {
                        new ObstacleSpawn(
                            ObstacleType.LaserEmitter,
                            512,
                            512,
                            0,
                            GateLaser())
                    };
                }
                else if (string.Equals(
                    themeId,
                    "nebula",
                    StringComparison.Ordinal))
                {
                    environment = Drift();
                    gimmick = new StageGimmickDefinition(
                        themeId,
                        true,
                        0);
                }
                else if (string.Equals(
                    themeId,
                    "core",
                    StringComparison.Ordinal))
                {
                    environment = new SegmentEnvironmentDefinition(
                        true,
                        -1024,
                        1024,
                        -512,
                        512,
                        1,
                        1,
                        2,
                        0,
                        1);
                    obstacles = new[]
                    {
                        new ObstacleSpawn(
                            ObstacleType.Breakable,
                            256,
                            0,
                            1),
                        new ObstacleSpawn(
                            ObstacleType.LaserEmitter,
                            512,
                            512,
                            0,
                            GateLaser())
                    };
                    gimmick = new StageGimmickDefinition(
                        themeId,
                        false,
                        120);
                }
                var segment = new StageSegment(
                    themeId + "_segment",
                    12,
                    spawns,
                    1,
                    1,
                    new[] { 1 },
                    obstacles,
                    environment);
                return new StagePlan(
                    new[] { segment },
                    "completion_boss",
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    512,
                    Phases,
                    themeId,
                    themeId,
                    EncounterType.Normal,
                    Array.Empty<BossPartDefinition>(),
                    gimmick);
            }

            static SegmentEnvironmentDefinition Corridor()
            {
                return new SegmentEnvironmentDefinition(
                    true,
                    -1024,
                    1024,
                    -512,
                    512,
                    1,
                    0,
                    1,
                    0,
                    1);
            }

            static SegmentEnvironmentDefinition Drift()
            {
                return new SegmentEnvironmentDefinition(
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1,
                    2,
                    -1,
                    3);
            }

            static LaserAttackDefinition GateLaser()
            {
                return new LaserAttackDefinition(
                    20,
                    5,
                    2,
                    2,
                    1,
                    0,
                    0,
                    0,
                    -256,
                    8,
                    32,
                    1);
            }
        }

        sealed class RhythmRunGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes = { "rhythm" };
            static readonly BossPhase[] BossPhases =
            {
                new BossPhase(
                    60,
                    1,
                    32,
                    1,
                    BossMovementPattern.Stationary,
                    0,
                    1,
                    1,
                    BossPartVulnerability.CoreOnly),
                new BossPhase(
                    30,
                    3,
                    64,
                    1,
                    BossMovementPattern.VerticalSine,
                    128,
                    1,
                    16,
                    BossPartVulnerability.All),
                new BossPhase(
                    15,
                    5,
                    128,
                    1,
                    BossMovementPattern.VerticalSine,
                    256,
                    1,
                    8,
                    BossPartVulnerability.All)
            };
            readonly string _damageEnemyId;

            public RhythmRunGenerator(string damageEnemyId)
            {
                _damageEnemyId = damageEnemyId;
            }

            public IReadOnlyList<string> ThemeIds => Themes;

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return Plan(stageIndex, EncounterType.Normal);
            }

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                return Array.AsReadOnly((string[])Themes.Clone());
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                return string.Equals(
                    themeId,
                    Themes[0],
                    StringComparison.Ordinal);
            }

            public StagePlan GenerateRoute(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType)
            {
                return Plan(stageIndex, encounterType);
            }

            StagePlan Plan(
                int stageIndex,
                EncounterType encounterType)
            {
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "rhythm_section",
                            1,
                            new[]
                            {
                                new SpawnEvent(
                                    0,
                                    _damageEnemyId,
                                    0,
                                    0)
                            },
                            1,
                            1,
                            new[] { 1 })
                    },
                    "rhythm_boss_" + stageIndex,
                    1,
                    1,
                    1,
                    120,
                    128,
                    128,
                    300,
                    BossPhases,
                    Themes[0],
                    Themes[0],
                    encounterType);
            }
        }
    }
}
