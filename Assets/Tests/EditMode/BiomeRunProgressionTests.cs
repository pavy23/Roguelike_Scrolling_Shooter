using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class BiomeRunProgressionTests
    {
        [Test]
        public void DefaultProgressionUsesThreeBosslessRoomsThenBiomeBoss()
        {
            RunManager run = CreateRun(
                11UL,
                RunProgressionConfig.CreateDefault(),
                false);

            Assert.AreEqual(5, run.BiomeCount);
            Assert.AreEqual(3, run.RoomsPerBiome);
            for (int room = 1; room <= 3; room++)
            {
                Assert.AreEqual(1, run.BiomeIndex);
                Assert.AreEqual(room, run.RoomIndex);
                Assert.IsFalse(run.IsBiomeBoss);
                Assert.AreEqual(1, run.Difficulty);
                Assert.AreEqual(string.Empty, run.StagePlan.BossId);
                Assert.AreEqual(0, run.StagePlan.BossMaxHp);
                Assert.AreEqual(0, run.RewardOptions.Count);

                run.Step(InputCommand.None);
                if (room < 3)
                {
                    Assert.AreEqual(RunState.AwaitingRoute, run.State);
                    Assert.GreaterOrEqual(run.RouteOptions.Count, 2);
                    for (int i = 0; i < run.RouteOptions.Count; i++)
                    {
                        Assert.AreEqual(
                            "biome_1",
                            run.RouteOptions[i].ThemeId);
                    }
                    run.ChooseRoute(FindNonEliteRoute(run));
                }
            }

            Assert.AreEqual(RunState.AwaitingRoute, run.State);
            Assert.GreaterOrEqual(run.RouteOptions.Count, 2);
            for (int i = 0; i < run.RouteOptions.Count; i++)
                Assert.AreEqual("biome_2", run.RouteOptions[i].ThemeId);
            run.ChooseRoute(FindNonEliteRoute(run));
            Assert.AreEqual(3, run.RouteChoiceHistory.Count);

            Assert.AreEqual(RunState.Playing, run.State);
            Assert.IsTrue(run.IsBiomeBoss);
            Assert.AreEqual(3, run.RoomIndex);
            Assert.AreEqual("biome_1_boss", run.StagePlan.BossId);
            Assert.AreEqual(1, run.StagePlan.BossMaxHp);
            Assert.AreEqual(3, run.Statistics.RoomsCleared);
            Assert.AreEqual(0, run.Statistics.BiomesCleared);
        }

        [Test]
        public void RewardsAppearOnlyAfterEliteRoomAndNonFinalBiomeBoss()
        {
            RunManager run = CreateRun(
                22UL,
                new RunProgressionConfig(2, 6),
                true);

            run.Step(InputCommand.None);
            run.ChooseRoute(FindRoute(run, EncounterType.Elite));
            Assert.AreEqual(EncounterType.Elite, run.StagePlan.EncounterType);

            run.Step(InputCommand.None);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);
            run.ChooseReward(0);
            Assert.AreEqual(RunState.AwaitingRoute, run.State);

            while (!run.IsBiomeBoss)
            {
                run.ChooseRoute(FindRoute(run, EncounterType.Normal));
                run.Step(InputCommand.None);
            }

            Assert.AreEqual(0, run.RewardOptions.Count);
            DefeatBoss(run);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);

            run.ChooseReward(0);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(2, run.BiomeIndex);
            Assert.AreEqual(1, run.RoomIndex);
            Assert.IsFalse(run.IsBiomeBoss);
        }

        [Test]
        public void ConfiguredTwoByTwoRunClearsOnlyAfterSecondBiomeBoss()
        {
            RunManager run = CreateRun(
                33UL,
                new RunProgressionConfig(2, 2),
                false);

            for (int guard = 0; guard < 500 && !run.IsFinished; guard++)
            {
                if (run.State == RunState.AwaitingReward)
                    run.ChooseReward(0);
                else if (run.State == RunState.AwaitingRoute)
                    run.ChooseRoute(FindNonEliteRoute(run));
                else
                {
                    InputCommand input = run.IsBiomeBoss
                        ? new InputCommand(0, 0, true)
                        : InputCommand.None;
                    run.Step(in input);
                }
            }

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.AreEqual(2, run.BiomeIndex);
            Assert.IsTrue(run.IsBiomeBoss);
            Assert.AreEqual(2, run.Statistics.BiomesCleared);
            Assert.AreEqual(4, run.Statistics.RoomsCleared);
            Assert.AreEqual(0, run.RewardOptions.Count);
            Assert.AreEqual(0, run.RouteOptions.Count);
        }

        [Test]
        public void RoomBoundarySuspendAndRecordingPreserveHierarchyAndRoute()
        {
            const ulong seed = 44UL;
            RunManager source = CreateRun(
                seed,
                new RunProgressionConfig(3, 3),
                false);
            source.Step(InputCommand.None);
            int selectedIndex = FindNonEliteRoute(source);
            RouteOption selected = source.RouteOptions[selectedIndex];
            source.ChooseRoute(selectedIndex);

            RunSuspendData suspend = source.ExportSuspendData();
            Assert.AreEqual(RunSuspendData.CurrentSchemaVersion, suspend.schemaVersion);
            Assert.AreEqual(1, suspend.biomeIndex);
            Assert.AreEqual(2, suspend.roomIndex);
            Assert.IsFalse(suspend.isBiomeBoss);
            Assert.AreEqual(3, suspend.biomeCount);
            Assert.AreEqual(3, suspend.roomsPerBiome);
            Assert.AreEqual(1, suspend.roomsCleared);
            Assert.IsTrue(SaveDataIntegrity.HasValidChecksum(suspend));

            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new HierarchyGenerator(false),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault());
            Assert.AreEqual(source.BiomeIndex, resumed.BiomeIndex);
            Assert.AreEqual(source.RoomIndex, resumed.RoomIndex);
            Assert.AreEqual(selected.ThemeId, resumed.StagePlan.ThemeId);
            Assert.AreEqual(
                selected.EncounterType,
                resumed.StagePlan.EncounterType);
            AssertRunHashEqual(source, resumed);

            var recorder = new InputRecorder(source);
            InputCommand none = InputCommand.None;
            recorder.Record(in none);
            InputRecordingData recording = recorder.Export();
            Assert.AreEqual(
                InputRecordingData.CurrentSchemaVersion,
                recording.schemaVersion);
            Assert.AreEqual(3, recording.biomeCount);
            Assert.AreEqual(3, recording.roomsPerBiome);
            Assert.AreEqual(1, recording.routeChoices.Length);
            Assert.AreEqual(1, recording.routeChoices[0].biomeIndex);
            Assert.AreEqual(2, recording.routeChoices[0].roomIndex);
            Assert.IsTrue(SaveDataIntegrity.HasValidChecksum(recording));

            var playback = new InputPlayback(recording);
            Assert.AreEqual(3, playback.BiomeCount);
            Assert.AreEqual(3, playback.RoomsPerBiome);
            Assert.AreEqual(1, playback.RouteChoices[0].BiomeIndex);
            Assert.AreEqual(2, playback.RouteChoices[0].RoomIndex);

            suspend.routeChoices[0].roomIndex = 3;
            recording.routeChoices[0].roomIndex = 3;
            Assert.IsFalse(SaveDataIntegrity.HasValidChecksum(suspend));
            Assert.IsFalse(SaveDataIntegrity.HasValidChecksum(recording));
        }

        [Test]
        public void BossBoundarySuspendPreservesPendingNextBiomeRoute()
        {
            RunManager source = CreateRun(
                66UL,
                new RunProgressionConfig(2, 2),
                false);
            source.Step(InputCommand.None);
            source.ChooseRoute(FindNonEliteRoute(source));
            source.Step(InputCommand.None);
            Assert.AreEqual(RunState.AwaitingRoute, source.State);
            source.ChooseRoute(FindNonEliteRoute(source));
            Assert.IsTrue(source.IsBiomeBoss);

            RunSuspendData suspend = source.ExportSuspendData();
            Assert.AreEqual(2, suspend.routeChoices.Length);
            RouteChoiceData pending =
                suspend.routeChoices[suspend.routeChoices.Length - 1];
            Assert.AreEqual(2, pending.biomeIndex);
            Assert.AreEqual(1, pending.roomIndex);

            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new HierarchyGenerator(false),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault());
            Assert.IsTrue(resumed.IsBiomeBoss);
            AssertRunHashEqual(source, resumed);
        }

        [Test]
        public void PreBiomeSchemasMigrateToOneRoomCompatibilityHierarchy()
        {
            RunManager run = CreateRun(
                55UL,
                new RunProgressionConfig(5, 6),
                false);
            RunSuspendData legacySuspend = run.ExportSuspendData();
            legacySuspend.schemaVersion = 4;
            legacySuspend.checksum = null;
            legacySuspend.biomeIndex = 0;
            legacySuspend.roomIndex = 0;
            legacySuspend.biomeCount = 0;
            legacySuspend.roomsPerBiome = 0;

            RunSuspendData migratedSuspend =
                SaveDataIntegrity.MigrateAndValidate(legacySuspend);
            Assert.AreEqual(
                RunSuspendData.CurrentSchemaVersion,
                migratedSuspend.schemaVersion);
            Assert.AreEqual(legacySuspend.stageIndex, migratedSuspend.biomeIndex);
            Assert.AreEqual(1, migratedSuspend.roomIndex);
            Assert.AreEqual(1, migratedSuspend.roomsPerBiome);
            Assert.AreEqual(
                legacySuspend.finalStageIndex,
                migratedSuspend.biomeCount);
            Assert.IsTrue(SaveDataIntegrity.HasValidChecksum(migratedSuspend));

            var recorder = new InputRecorder();
            InputCommand none = InputCommand.None;
            recorder.Record(in none);
            InputRecordingData legacyRecording = recorder.Export();
            legacyRecording.schemaVersion = 5;
            legacyRecording.checksum = null;
            legacyRecording.biomeCount = 0;
            legacyRecording.roomsPerBiome = 0;

            InputRecordingData migratedRecording =
                SaveDataIntegrity.MigrateAndValidate(legacyRecording);
            Assert.AreEqual(
                InputRecordingData.CurrentSchemaVersion,
                migratedRecording.schemaVersion);
            Assert.AreEqual(1, migratedRecording.roomsPerBiome);
            Assert.AreEqual(
                legacyRecording.finalStageIndex,
                migratedRecording.biomeCount);
            Assert.IsTrue(SaveDataIntegrity.HasValidChecksum(migratedRecording));
        }

        [Test]
        public void PreviousChecksummedSchemasRemainAccepted()
        {
            var suspend = new RunSuspendData
            {
                schemaVersion = 4,
                runSeed = 7,
                runNumber = 1,
                stageIndex = 2,
                score = 3,
                shotsFired = 4,
                shotsHit = 5,
                kills = 6,
                capsulesCollected = 7,
                grazeCount = 8,
                stagesCleared = 1,
                powerUpLevels = new[] { 1, 0, 0, 0 },
                powerUpCursor = -1,
                playerHp = 5,
                shieldRemaining = 0,
                rewardAcquisitions =
                    Array.Empty<RewardAcquisitionData>(),
                activeModifiers = 0,
                shipId = "default",
                fireIntervalTicks = 8,
                mainShotBaseDamage = 1,
                playerSpeedNumerator = 10,
                playerSpeedDenominator = 1,
                difficultyMultiplierNumerator = 1,
                difficultyMultiplierDenominator = 1,
                routeChoices = Array.Empty<RouteChoiceData>(),
                finalStageIndex = 5,
                checksum = "ADC976AA191F2AAD"
            };
            RunSuspendData migratedSuspend =
                SaveDataIntegrity.MigrateAndValidate(suspend);
            Assert.AreEqual(5, migratedSuspend.biomeCount);
            Assert.AreEqual(1, migratedSuspend.roomsPerBiome);

            var recording = new InputRecordingData
            {
                schemaVersion = 5,
                totalTicks = 1,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = 0,
                        moveY = 0,
                        fire = false,
                        activate = false,
                        tickCount = 1
                    }
                },
                difficultyMultiplierNumerator = 1,
                difficultyMultiplierDenominator = 1,
                routeChoices = Array.Empty<RouteChoiceData>(),
                finalStageIndex = 5,
                checksum = "F876F0EFB88A821C"
            };
            InputRecordingData migratedRecording =
                SaveDataIntegrity.MigrateAndValidate(recording);
            Assert.AreEqual(5, migratedRecording.biomeCount);
            Assert.AreEqual(1, migratedRecording.roomsPerBiome);
        }

        static RunManager CreateRun(
            ulong seed,
            RunProgressionConfig progression,
            bool eliteRoutesOnly)
        {
            return new RunManager(
                seed,
                new HierarchyGenerator(eliteRoutesOnly),
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
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMinX = -10_000;
            config.PlayerMaxX = 10_000;
            config.PlayerMinY = -10_000;
            config.PlayerMaxY = 10_000;
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
                "hierarchy_shot",
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

        static int FindRoute(RunManager run, EncounterType encounterType)
        {
            for (int i = 0; i < run.RouteOptions.Count; i++)
            {
                if (run.RouteOptions[i].EncounterType == encounterType)
                    return i;
            }
            Assert.Fail($"No {encounterType} route was generated.");
            return 0;
        }

        static int FindNonEliteRoute(RunManager run)
        {
            for (int i = 0; i < run.RouteOptions.Count; i++)
            {
                if (run.RouteOptions[i].EncounterType != EncounterType.Elite)
                    return i;
            }
            Assert.Fail("No non-elite route was generated.");
            return 0;
        }

        static void DefeatBoss(RunManager run)
        {
            InputCommand fire = new InputCommand(0, 0, true);
            for (int guard = 0;
                guard < 100 && run.State == RunState.Playing;
                guard++)
                run.Step(in fire);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
        }

        static void AssertRunHashEqual(RunManager expected, RunManager actual)
        {
            var expectedHash = new DeterminismAuditHasher();
            var actualHash = new DeterminismAuditHasher();
            expectedHash.FoldRunState(expected);
            actualHash.FoldRunState(actual);
            Assert.AreEqual(expectedHash.Hash, actualHash.Hash);
        }

        sealed class HierarchyGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
            {
                "biome_1",
                "biome_2",
                "biome_3",
                "biome_4",
                "biome_5"
            };
            static readonly BossPhase[] Phases =
            {
                new BossPhase(999, 1, 1, 1)
            };

            readonly bool _eliteRoutesOnly;

            public HierarchyGenerator(bool eliteRoutesOnly)
            {
                _eliteRoutesOnly = eliteRoutesOnly;
            }

            public IReadOnlyList<string> ThemeIds => Themes;

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                return Array.AsReadOnly((string[])Themes.Clone());
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                string theme = Themes[(stageIndex - 1) % Themes.Length];
                return Plan(theme, EncounterType.Normal);
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                if (Array.IndexOf(Themes, themeId) < 0)
                    return false;
                return !_eliteRoutesOnly
                    || encounterType == EncounterType.Normal
                    || encounterType == EncounterType.Elite;
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
                var segment = new StageSegment(
                    themeId + "_room",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    themeId + "_boss",
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
                    encounterType);
            }
        }
    }
}
