using System;
using System.Collections.Generic;
using NUnit.Framework;
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

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.IsTrue(run.IsFinished);
            Assert.AreEqual(1, run.StageIndex);
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
            Assert.AreEqual(4, first.RewardChoices);
            Assert.AreEqual(19, first.RouteChoices);
            Assert.AreEqual(20, first.RoomsCleared);
            Assert.AreEqual(first.Hash, second.Hash);
            Assert.AreEqual(first.Ticks, second.Ticks);
            Assert.AreEqual(
                first.RewardChoices,
                second.RewardChoices);
            Assert.AreEqual(first.RouteChoices, second.RouteChoices);
            Assert.AreEqual(first.RoomsCleared, second.RoomsCleared);
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
                RunState.AwaitingRoute,
                twoStageRun.State);
            twoStageRun.ChooseRoute(0);
            DrivePlayingTicks(twoStageRun, 500);
            Assert.AreEqual(
                RunState.AwaitingReward,
                twoStageRun.State);
            twoStageRun.ChooseReward(0);
            DrivePlayingTicks(twoStageRun, 500);

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
            hasher.FoldRunState(run);

            for (int guard = 0; guard < 5_000 && !run.IsFinished; guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
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
                else if (run.State == RunState.AwaitingRoute)
                {
                    int optionIndex =
                        (run.StageIndex + routes)
                        % run.RouteOptions.Count;
                    RouteOption option = run.RouteOptions[optionIndex];
                    bool nextBiome =
                        run.RoomIndex >= run.RoomsPerBiome;
                    hasher.FoldRouteChoice(
                        nextBiome
                            ? run.BiomeIndex + 1
                            : run.BiomeIndex,
                        nextBiome ? 1 : run.RoomIndex + 1,
                        optionIndex,
                        in option);
                    run.ChooseRoute(optionIndex);
                    routes++;
                }
                else
                {
                    var input = new InputCommand(0, 0, true);
                    run.Step(in input);
                    ticks++;
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
                run.State);
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
            return new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
        }

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
                RunState finalState)
            {
                Hash = hash;
                Ticks = ticks;
                RewardChoices = rewardChoices;
                RouteChoices = routeChoices;
                StagesCleared = stagesCleared;
                RoomsCleared = roomsCleared;
                FinalState = finalState;
            }

            public ulong Hash { get; }
            public int Ticks { get; }
            public int RewardChoices { get; }
            public int RouteChoices { get; }
            public int StagesCleared { get; }
            public int RoomsCleared { get; }
            public RunState FinalState { get; }
        }

        sealed class CompletingRouteGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
                { "a", "b", "c", "d", "e" };
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
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            themeId + "_segment",
                            1,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
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
                    themeId);
            }
        }
    }
}
