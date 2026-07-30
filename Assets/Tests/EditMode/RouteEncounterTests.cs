using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class RouteEncounterTests
    {
        const int Lane = 1;

        [Test]
        public void StageSectionsAdvanceDeterministicallyWithoutRouteChoice()
        {
            RunManager first = CreateRun(77UL, EncounterType.Supply);
            RunManager second = CreateRun(77UL, EncounterType.Supply);

            first.Step(InputCommand.None);
            second.Step(InputCommand.None);

            Assert.AreEqual(RunState.Playing, first.State);
            Assert.AreEqual(RunStageSection.MidBoss, first.StageSection);
            Assert.AreEqual(2, first.RoomIndex);
            Assert.AreEqual(EncounterType.Elite, first.StagePlan.EncounterType);
            Assert.AreEqual(0, first.RouteOptions.Count);
            Assert.AreEqual(0, first.RouteChoiceHistory.Count);
            Assert.AreEqual(first.StagePlan.ThemeId, second.StagePlan.ThemeId);
            Assert.AreEqual(
                first.StagePlan.EncounterType,
                second.StagePlan.EncounterType);
        }

        [Test]
        public void RouteChoiceFieldsAffectDeterminismAuditHash()
        {
            var baseHasher = new DeterminismAuditHasher();
            var baseOption =
                new RouteOption("alpha", EncounterType.Normal);
            baseHasher.FoldRouteChoice(2, 0, in baseOption);

            var themeHasher = new DeterminismAuditHasher();
            var themeOption =
                new RouteOption("beta", EncounterType.Normal);
            themeHasher.FoldRouteChoice(2, 0, in themeOption);

            var encounterHasher = new DeterminismAuditHasher();
            var encounterOption =
                new RouteOption("alpha", EncounterType.Elite);
            encounterHasher.FoldRouteChoice(2, 0, in encounterOption);

            Assert.AreNotEqual(baseHasher.Hash, themeHasher.Hash);
            Assert.AreNotEqual(baseHasher.Hash, encounterHasher.Hash);
        }

        [Test]
        public void EncounterTypesApplyProvisionalStageMutations()
        {
            SegmentStageGenerator generator = CreateSegmentGenerator();

            StagePlan normal = generator.GenerateRoute(
                123UL, 2, 2, "alpha", EncounterType.Normal);
            StagePlan elite = generator.GenerateRoute(
                123UL, 2, 2, "alpha", EncounterType.Elite);
            StagePlan supply = generator.GenerateRoute(
                123UL, 2, 2, "alpha", EncounterType.Supply);
            StagePlan hazard = generator.GenerateRoute(
                123UL, 2, 2, "alpha", EncounterType.Hazard);
            StagePlan rare = generator.GenerateRoute(
                123UL, 2, 2, "alpha", EncounterType.Rare);

            Assert.AreEqual(3, normal.Segments.Count);
            Assert.AreEqual(1, elite.Segments.Count);
            Assert.AreEqual(3, elite.EncounterEnemyHpMultiplierNumerator);
            Assert.AreEqual(2, elite.EncounterEnemyHpMultiplierDenominator);
            Assert.Greater(elite.BossMaxHp, 0);
            Assert.AreEqual(1, supply.Segments.Count);
            Assert.AreEqual(0, supply.BossMaxHp);
            Assert.AreEqual(string.Empty, supply.BossId);
            Assert.AreEqual(4, supply.CapsuleDropMultiplierNumerator);
            Assert.IsTrue(StagePlanClearability.IsClearable(supply));
            Assert.AreEqual(normal.Segments.Count, hazard.Segments.Count);
            Assert.Greater(
                CountObstacles(hazard),
                CountObstacles(normal));
            Assert.AreEqual(3, hazard.EncounterScoreMultiplierNumerator);
            Assert.AreEqual(2, hazard.EncounterScoreMultiplierDenominator);
            Assert.AreEqual(normal.Segments.Count, rare.Segments.Count);
            Assert.AreEqual(2, rare.EncounterEnemyHpMultiplierNumerator);
            Assert.AreEqual(1, rare.EncounterEnemyHpMultiplierDenominator);
        }

        [Test]
        public void RouteOptionsRemainEmptyForEverySeed()
        {
            for (ulong seed = 0; seed < 100; seed++)
            {
                RunManager run = CreateRun(seed, EncounterType.Supply);
                run.Step(InputCommand.None);
                Assert.AreEqual(0, run.RouteOptions.Count);
                Assert.AreEqual(RunState.Playing, run.State);
            }
        }

        [Test]
        public void RareOpeningBecomesSectionTraitWithoutChoiceScreen()
        {
            RunManager run = CreateRun(91UL, EncounterType.Rare);
            run.Step(InputCommand.None);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(RunStageSection.MidBoss, run.StageSection);
            Assert.AreEqual(1, run.RareEncountersCleared);
            Assert.AreEqual(0, run.RewardOptions.Count);
        }

        [Test]
        public void BattleSimConsumesEliteHpAndHazardScoreMultipliers()
        {
            var elitePlan = PlanWithSpawn(EncounterType.Elite, 5000);
            var eliteBattle = new BattleSim(
                CreateConfig(),
                new Rng(1UL),
                elitePlan,
                CreateContent(),
                CreateGauge());
            eliteBattle.Step(InputCommand.None);

            Assert.AreEqual(1, eliteBattle.Enemies.Count);
            Assert.AreEqual(2, eliteBattle.Enemies[0].Hp);

            BattleSimConfig hazardConfig = CreateConfig();
            hazardConfig.PlayerSpawnX = 0;
            hazardConfig.PlayerBulletSpeedPerTick = 1;
            hazardConfig.PlayerHalfWidth = 0;
            hazardConfig.PlayerHalfHeight = 0;
            hazardConfig.ScrollSpeedNumerator = 0;
            hazardConfig.ScrollSpeedDenominator = 1;
            var hazardBattle = new BattleSim(
                hazardConfig,
                new Rng(1UL),
                PlanWithSpawn(EncounterType.Hazard, 1),
                CreateContent(),
                CreateGauge());
            hazardBattle.Step(new InputCommand(0, 0, true));
            hazardBattle.Step(InputCommand.None);

            Assert.AreEqual(0, hazardBattle.Enemies.Count);
            Assert.AreEqual(15, hazardBattle.Score);
            bool foundKill = false;
            ReadOnlySpan<SimEvent> events = hazardBattle.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type == SimEventType.EnemyKilled)
                {
                    Assert.AreEqual(15, events[i].Arg);
                    foundKill = true;
                }
            }
            Assert.IsTrue(foundKill);
        }

        [Test]
        public void EliteRewardOptionsGuaranteeEligibleModifier()
        {
            var rewards = new RewardCatalog(
                3,
                new[]
                {
                    new RewardDefinition(
                        "modifier",
                        RewardType.Modifier,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        99,
                        null,
                        BattleModifier.PierceShot),
                    new RewardDefinition(
                        "capsules",
                        RewardType.Capsules,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        99),
                    new RewardDefinition(
                        "repair",
                        RewardType.RepairHp,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        99),
                    new RewardDefinition(
                        "damage",
                        RewardType.DamageUp,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        99)
                });
            var run = new RunManager(
                9UL,
                new RouteStageGenerator(EncounterType.Elite),
                CreateConfig(),
                CreateContent(),
                CreateGauge(),
                rewards);

            run.Step(InputCommand.None);
            run.Step(InputCommand.None);

            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RewardSelectionKind.MidStage,
                run.RewardSelectionKind);
            Assert.AreEqual(2, run.RewardOptions.Count);
            Assert.AreEqual(RewardType.Modifier, run.RewardOptions[0].Type);
            Assert.AreEqual(
                BattleModifier.PierceShot,
                run.RewardOptions[0].ModifierId);
        }

        [Test]
        public void RemovedRouteApiNeverInterruptsPlaying()
        {
            RunManager run = CreateRun(88UL, EncounterType.Supply);
            run.Step(InputCommand.None);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(1, run.BiomeIndex);
            Assert.AreEqual(2, run.RoomIndex);
            Assert.AreEqual(0, run.RouteOptions.Count);
#pragma warning disable CS0618
            Assert.Throws<NotSupportedException>(
                () => run.ChooseRoute(0));
#pragma warning restore CS0618
        }

        [Test]
        public void CurrentSuspendAndReplayExportNoRouteChoices()
        {
            RunManager source = CreateRun(20260729UL, EncounterType.Supply);
            var recorder = new InputRecorder(source);
            recorder.Record(InputCommand.None);
            source.Step(InputCommand.None);

            RunSuspendData suspend = source.ExportSuspendData();
            InputRecordingData recording = recorder.Export();
            var playback = new InputPlayback(recording);
            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new RouteStageGenerator(EncounterType.Supply),
                CreateConfig(),
                CreateContent(),
                CreateGauge());

            Assert.AreEqual(0, suspend.routeChoices.Length);
            Assert.AreEqual(0, playback.RouteChoices.Count);
            Assert.AreEqual(RunStageSection.MidBoss, resumed.StageSection);
            Assert.AreEqual(
                resumed.StagePlan.EncounterType,
                source.StagePlan.EncounterType);
        }

        [Test]
        public void LegacyPlansAndPersistenceSchemasRemainCompatible()
        {
            var legacyPlan = new StagePlan(
                new[] { Segment("legacy", null) },
                "boss",
                1,
                1,
                1);
            Assert.AreEqual(
                EncounterType.Normal,
                legacyPlan.EncounterType);

            RunManager run = CreateRun(5UL, EncounterType.Supply);
            RunSuspendData oldSuspend = run.ExportSuspendData();
            oldSuspend.schemaVersion = 2;
            oldSuspend.checksum = null;
            oldSuspend.routeChoices = null;
            RunManager resumed = RunManager.ResumeFromSuspendData(
                oldSuspend,
                new RouteStageGenerator(EncounterType.Supply),
                CreateConfig(),
                CreateContent(),
                CreateGauge());
            Assert.AreEqual(EncounterType.Supply, resumed.StagePlan.EncounterType);

            var recorder = new InputRecorder();
            recorder.Record(InputCommand.None);
            InputRecordingData oldRecording = recorder.Export();
            oldRecording.schemaVersion = 3;
            oldRecording.checksum = null;
            oldRecording.routeChoices = null;
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(oldRecording));
        }

        [Test]
        public void LegacySaveAndReplayWithRoutePayloadStillOpenAfterRouteRemoval()
        {
            var legacyChoice = new RouteChoiceData
            {
                stageIndex = 1,
                biomeIndex = 1,
                roomIndex = 1,
                optionIndex = 0,
                themeId = "beta",
                encounterType = (int)EncounterType.Rare
            };
            RunManager run = CreateRun(54UL, EncounterType.Supply);
            RunSuspendData oldSuspend = run.ExportSuspendData();
            oldSuspend.schemaVersion = 10;
            oldSuspend.checksum = null;
            oldSuspend.powerUpProgress = null;
            oldSuspend.routeChoices = new[] { legacyChoice };

            RunManager resumed = RunManager.ResumeFromSuspendData(
                oldSuspend,
                new RouteStageGenerator(EncounterType.Supply),
                CreateConfig(),
                CreateContent(),
                CreateGauge());

            Assert.AreEqual(1, resumed.RouteChoiceHistory.Count);
            Assert.AreEqual("beta", resumed.StagePlan.ThemeId);
            Assert.AreEqual(
                EncounterType.Rare,
                resumed.StagePlan.EncounterType);
            Assert.AreEqual(RunState.Playing, resumed.State);

            var recorder = new InputRecorder();
            recorder.Record(InputCommand.None);
            InputRecordingData oldRecording = recorder.Export();
            oldRecording.schemaVersion = 9;
            oldRecording.checksum = null;
            oldRecording.routeChoices = new[] { legacyChoice };
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(oldRecording));
        }

        static RunManager CreateRun(
            ulong seed,
            EncounterType initialEncounter)
        {
            return new RunManager(
                seed,
                new RouteStageGenerator(initialEncounter),
                CreateConfig(),
                CreateContent(),
                CreateGauge());
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            return config;
        }

        static BattleContent CreateContent()
        {
            var enemy = new EnemyDefinition(
                "dummy",
                "Dummy",
                1,
                0,
                10,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                0,
                1,
                1);
            var weapon =
                new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
        }

        static PowerUpGauge CreateGauge()
        {
            return new PowerUpGauge(new[] { 5, 3, 4, 3 });
        }

        static SegmentStageGenerator CreateSegmentGenerator()
        {
            var segments = new List<StageSegmentTemplate>();
            string[] themes = { "alpha", "beta" };
            for (int theme = 0; theme < themes.Length; theme++)
            {
                for (int i = 0; i < 4; i++)
                {
                    segments.Add(new StageSegmentTemplate(
                        themes[theme] + "_" + i,
                        1,
                        5,
                        60,
                        Lane,
                        Lane,
                        new[] { Lane },
                        new[]
                        {
                            new SpawnEvent(i, "dummy", 100, i)
                        },
                        new[]
                        {
                            new ObstacleSpawn(
                                ObstacleType.Solid,
                                200 + i,
                                20 + i,
                                0)
                        },
                        themes[theme]));
                }
            }
            var bosses = new[]
            {
                Boss("alpha"),
                Boss("beta")
            };
            return new SegmentStageGenerator(
                new StageGenerationCatalog(
                    1,
                    3,
                    Lane,
                    segments,
                    bosses,
                    themes));
        }

        static StageBossTemplate Boss(string themeId)
        {
            return new StageBossTemplate(
                themeId + "_boss",
                1,
                99,
                1,
                5,
                Lane,
                10,
                1,
                1,
                1,
                Array.Empty<BossPhase>(),
                themeId);
        }

        static int CountObstacles(StagePlan plan)
        {
            int count = 0;
            for (int i = 0; i < plan.Segments.Count; i++)
                count += plan.Segments[i].Obstacles.Count;
            return count;
        }

        static StageSegment Segment(
            string id,
            IReadOnlyList<ObstacleSpawn> obstacles)
        {
            return new StageSegment(
                id,
                1,
                Array.Empty<SpawnEvent>(),
                Lane,
                Lane,
                new[] { Lane },
                obstacles ?? Array.Empty<ObstacleSpawn>());
        }

        static StagePlan PlanWithSpawn(
            EncounterType encounterType,
            int spawnX)
        {
            var segment = new StageSegment(
                "combat",
                10,
                new[] { new SpawnEvent(0, "dummy", spawnX, 0) },
                Lane,
                Lane,
                new[] { Lane });
            return new StagePlan(
                new[] { segment },
                string.Empty,
                1,
                Lane,
                Lane,
                0,
                0,
                0,
                0,
                Array.Empty<BossPhase>(),
                "alpha",
                "alpha",
                encounterType);
        }

        sealed class RouteStageGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
                { "alpha", "beta", "gamma", "delta" };
            readonly EncounterType _initialEncounter;

            public RouteStageGenerator(
                EncounterType initialEncounter)
            {
                _initialEncounter = initialEncounter;
            }

            public IReadOnlyList<string> ThemeIds => Themes;

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return Plan(Themes[0], _initialEncounter);
            }

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                var order = (string[])Themes.Clone();
                Rng rng = new Rng(seed).Fork(17);
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int swap = rng.NextInt(0, i + 1);
                    string held = order[i];
                    order[i] = order[swap];
                    order[swap] = held;
                }
                return Array.AsReadOnly(order);
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                for (int i = 0; i < Themes.Length; i++)
                {
                    if (string.Equals(
                            Themes[i],
                            themeId,
                            StringComparison.Ordinal))
                        return true;
                }
                return false;
            }

            public StagePlan GenerateRoute(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType)
            {
                return Plan(themeId, encounterType);
            }

            static StagePlan Plan(
                string themeId,
                EncounterType encounterType)
            {
                return new StagePlan(
                    new[] { Segment(themeId + "_segment", null) },
                    string.Empty,
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<BossPhase>(),
                    themeId,
                    themeId,
                    encounterType);
            }
        }
    }
}
