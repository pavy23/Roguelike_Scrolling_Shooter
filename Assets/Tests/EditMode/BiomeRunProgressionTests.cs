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
        public void DefaultProgressionUsesOpeningMidBossClosingThenBoss()
        {
            RunManager run = CreateRun(
                11UL,
                RunProgressionConfig.CreateDefault(),
                false);

            Assert.AreEqual(5, run.BiomeCount);
            Assert.AreEqual(3, run.RoomsPerBiome);
            Assert.AreEqual(RunStageSection.Opening, run.StageSection);
            run.Step(InputCommand.None);
            Assert.AreEqual(RunStageSection.MidBoss, run.StageSection);
            Assert.AreEqual(2, run.RoomIndex);
            Assert.Greater(run.StagePlan.BossMaxHp, 0);
            DefeatBoss(run);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(2, run.RewardOptions.Count);
            Assert.AreEqual(
                RewardSelectionKind.MidStage,
                run.RewardSelectionKind);
            run.ChooseReward(0);
            Assert.AreEqual(RunStageSection.Closing, run.StageSection);
            Assert.AreEqual(3, run.RoomIndex);
            run.Step(InputCommand.None);

            Assert.AreEqual(RunState.Playing, run.State);
            Assert.IsTrue(run.IsBiomeBoss);
            Assert.AreEqual(3, run.RoomIndex);
            Assert.AreEqual("biome_1_boss", run.StagePlan.BossId);
            Assert.AreEqual(1, run.StagePlan.BossMaxHp);
            Assert.AreEqual(3, run.Statistics.RoomsCleared);
            Assert.AreEqual(0, run.Statistics.BiomesCleared);
            Assert.AreEqual(0, run.RouteOptions.Count);
            Assert.AreEqual(0, run.RouteChoiceHistory.Count);
        }

        [Test]
        public void MidBossAndStageBossExposeTwoAndThreeChoices()
        {
            RunManager run = CreateRun(
                22UL,
                new RunProgressionConfig(2, 6),
                true);

            run.Step(InputCommand.None);
            Assert.AreEqual(RunStageSection.MidBoss, run.StageSection);
            Assert.AreEqual(EncounterType.Elite, run.StagePlan.EncounterType);

            DefeatBoss(run);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(2, run.RewardOptions.Count);
            run.ChooseReward(0);

            while (!run.IsBiomeBoss)
                run.Step(InputCommand.None);

            Assert.AreEqual(0, run.RewardOptions.Count);
            DefeatBoss(run);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);

            run.ChooseReward(0);
            Assert.AreEqual(
                RunState.AwaitingContract,
                run.State);
            Assert.IsTrue(run.ChooseContract(0));
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(2, run.BiomeIndex);
            Assert.AreEqual(1, run.RoomIndex);
            Assert.IsFalse(run.IsBiomeBoss);
        }

        [Test]
        public void MidBossRewardPoolExhaustionStillOffersChoicesAndAdvances()
        {
            RewardCatalog exhaustedAtBiomeOne =
                CreateBiomeTwoOnlyRewards();
            RunManager run = CreateRun(
                0x58UL,
                RunProgressionConfig.CreateDefault(),
                false,
                exhaustedAtBiomeOne);

            run.Step(InputCommand.None);
            Assert.AreEqual(RunStageSection.MidBoss, run.StageSection);
            DefeatBoss(run);

            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RunManager.MidStageRewardOptionCount,
                run.RewardOptions.Count);
            Assert.IsTrue(run.ChooseReward(0));
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(RunStageSection.Closing, run.StageSection);

            run.Step(InputCommand.None);
            Assert.IsTrue(run.IsBiomeBoss);
            DefeatBoss(run);
            Assert.AreEqual(
                RewardSelectionKind.Main,
                run.RewardSelectionKind);
            Assert.AreEqual(
                RunManager.MainRewardOptionCount,
                run.RewardOptions.Count);
            Assert.IsTrue(run.ChooseReward(0));
            Assert.AreEqual(
                RunState.AwaitingContract,
                run.State);
            Assert.IsTrue(run.ChooseContract(0));
            Assert.AreEqual(2, run.BiomeIndex);
            Assert.AreEqual(RunStageSection.Opening, run.StageSection);
        }

        [Test]
        public void RewardChoiceRejectsInvalidInputWithoutThrowingOrAdvancing()
        {
            RunManager run = CreateRun(
                0x5801UL,
                RunProgressionConfig.CreateDefault(),
                false,
                CreateBiomeTwoOnlyRewards());

            Assert.IsFalse(run.ChooseReward(0));
            Assert.AreEqual(RunState.Playing, run.State);

            run.Step(InputCommand.None);
            DefeatBoss(run);
            Assert.IsFalse(run.ChooseReward(-1));
            Assert.IsFalse(run.ChooseReward(run.RewardOptions.Count));
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RunManager.MidStageRewardOptionCount,
                run.RewardOptions.Count);

            Assert.IsTrue(run.ChooseReward(0));
            Assert.AreEqual(RunStageSection.Closing, run.StageSection);
        }

        [Test]
        public void ExhaustedRewardFallbackIsDeterministicForReplay()
        {
            RunManager first = CreateRun(
                0x5802UL,
                RunProgressionConfig.CreateDefault(),
                false,
                CreateBiomeTwoOnlyRewards());
            RunManager second = CreateRun(
                0x5802UL,
                RunProgressionConfig.CreateDefault(),
                false,
                CreateBiomeTwoOnlyRewards());

            first.Step(InputCommand.None);
            second.Step(InputCommand.None);
            DefeatBoss(first);
            DefeatBoss(second);
            Assert.AreEqual(
                first.RewardOptions.Count,
                second.RewardOptions.Count);
            for (int i = 0; i < first.RewardOptions.Count; i++)
            {
                Assert.AreEqual(
                    first.RewardOptions[i].Id,
                    second.RewardOptions[i].Id);
                Assert.AreEqual(
                    first.RewardOptions[i].Type,
                    second.RewardOptions[i].Type);
                Assert.AreEqual(
                    first.RewardOptions[i].Amount,
                    second.RewardOptions[i].Amount);
            }

            Assert.IsTrue(first.ChooseReward(1));
            Assert.IsTrue(second.ChooseReward(1));
            AssertRunHashEqual(first, second);
        }

        [Test]
        public void MidBossesUseDistinctCyclingPatternsWithTelegraphs()
        {
            BattleContent content = CreateMidBossContent();
            var signatures =
                new Dictionary<string, string>(StringComparer.Ordinal);
            int phaseChanges = 0;
            int telegraphs = 0;

            for (ulong seed = 0;
                seed < 256 && signatures.Count < 4;
                seed++)
            {
                RunManager run = CreateRun(
                    seed,
                    RunProgressionConfig.CreateDefault(),
                    false,
                    null,
                    content);
                run.Step(InputCommand.None);
                IReadOnlyList<BossPhase> phases =
                    run.StagePlan.BossPhases;
                Assert.GreaterOrEqual(phases.Count, 2);
                Assert.LessOrEqual(phases.Count, 3);
                string signature = string.Empty;
                for (int i = 0; i < phases.Count; i++)
                {
                    Assert.Greater(phases[i].DurationTicks, 0);
                    signature +=
                        $"{phases[i].FireIntervalTicks}:"
                        + $"{phases[i].Ways}:"
                        + $"{(int)phases[i].MovementPattern}:"
                        + $"{phases[i].DurationTicks};";
                }
                signatures[run.StagePlan.BossId] = signature;

                if (phaseChanges > 0 && telegraphs > 0)
                    continue;
                for (int tick = 0; tick < 5000; tick++)
                {
                    run.Step(InputCommand.None);
                    ReadOnlySpan<SimEvent> events =
                        run.Battle.EventsThisTick;
                    for (int i = 0; i < events.Length; i++)
                    {
                        if (events[i].Type
                            == SimEventType.BossPhaseChanged)
                            phaseChanges++;
                        else if (events[i].Type
                            == SimEventType.BossAttackTelegraphed)
                            telegraphs++;
                    }
                }
            }

            Assert.AreEqual(4, signatures.Count);
            var uniquePatterns = new HashSet<string>(
                signatures.Values,
                StringComparer.Ordinal);
            Assert.AreEqual(4, uniquePatterns.Count);
            Assert.GreaterOrEqual(phaseChanges, 2);
            Assert.Greater(telegraphs, 0);
        }

        [Test]
        public void MidBossSelectionPrefersHomeThemeAndExcludesLateProfiles()
        {
            BattleContent content = CreateProfiledMidBossContent();
            int homeSelections = 0;
            for (ulong seed = 0; seed < 128; seed++)
            {
                RunManager run = CreateRun(
                    seed,
                    RunProgressionConfig.CreateDefault(),
                    false,
                    null,
                    content);
                run.Step(InputCommand.None);

                Assert.AreNotEqual(
                    "mini_late",
                    run.StagePlan.BossId);
                if (run.StagePlan.BossId == "mini_home")
                    homeSelections++;
            }

            Assert.Greater(
                homeSelections,
                64,
                "The matching theme's 3x soft preference should dominate.");
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
                else if (run.State
                    == RunState.AwaitingContract)
                    run.ChooseContract(0);
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
        public void RoomBoundarySuspendAndRecordingPreserveHierarchyWithoutRoutes()
        {
            const ulong seed = 44UL;
            RunManager source = CreateRun(
                seed,
                new RunProgressionConfig(3, 3),
                false);
            source.Step(InputCommand.None);

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
            Assert.AreEqual(
                source.StagePlan.EncounterType,
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
            Assert.AreEqual(0, recording.routeChoices.Length);
            Assert.IsTrue(SaveDataIntegrity.HasValidChecksum(recording));

            var playback = new InputPlayback(recording);
            Assert.AreEqual(3, playback.BiomeCount);
            Assert.AreEqual(3, playback.RoomsPerBiome);
            Assert.AreEqual(0, playback.RouteChoices.Count);

            suspend.roomIndex = 3;
            recording.roomsPerBiome = 4;
            Assert.IsFalse(SaveDataIntegrity.HasValidChecksum(suspend));
            Assert.IsFalse(SaveDataIntegrity.HasValidChecksum(recording));
        }

        [Test]
        public void BossBoundarySuspendHasNoPendingNextBiomeRoute()
        {
            RunManager source = CreateRun(
                66UL,
                new RunProgressionConfig(2, 2),
                false);
            source.Step(InputCommand.None);
            source.Step(InputCommand.None);
            Assert.IsTrue(source.IsBiomeBoss);

            RunSuspendData suspend = source.ExportSuspendData();
            Assert.AreEqual(0, suspend.routeChoices.Length);

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
            bool eliteRoutesOnly,
            RewardCatalog rewards = null,
            BattleContent content = null)
        {
            return new RunManager(
                seed,
                new HierarchyGenerator(eliteRoutesOnly),
                CreateConfig(),
                content ?? CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
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

        static BattleContent CreateMidBossContent()
        {
            var weapon = new WeaponDefinition(
                "mid_boss_test_shot",
                1,
                1,
                256,
                1,
                0,
                0);
            return new BattleContent(
                new[]
                {
                    CreateMini(
                        "mini_alpha",
                        EnemyMovePattern.Static,
                        0,
                        1,
                        1),
                    CreateMini(
                        "mini_beta",
                        EnemyMovePattern.Sine,
                        384,
                        1,
                        90),
                    CreateMini(
                        "mini_delta",
                        EnemyMovePattern.Static,
                        0,
                        1,
                        1),
                    CreateMini(
                        "mini_gamma",
                        EnemyMovePattern.Sine,
                        512,
                        1,
                        120)
                },
                new[] { weapon },
                weapon.Id);
        }

        static EnemyDefinition CreateMini(
            string id,
            EnemyMovePattern pattern,
            int amplitudeNumerator,
            int amplitudeDenominator,
            int periodTicks)
        {
            return new EnemyDefinition(
                id,
                id,
                10_000,
                0,
                1000,
                pattern,
                0,
                1,
                48,
                256,
                192,
                0,
                amplitudeNumerator,
                amplitudeDenominator,
                periodTicks);
        }

        static BattleContent CreateProfiledMidBossContent()
        {
            var weapon = new WeaponDefinition(
                "profiled_mid_boss_test_shot",
                1,
                1,
                256,
                1,
                0,
                0);
            return new BattleContent(
                new[]
                {
                    CreateProfiledMini(
                        "mini_home",
                        "biome_1",
                        1),
                    CreateProfiledMini(
                        "mini_other_a",
                        "biome_2",
                        1),
                    CreateProfiledMini(
                        "mini_other_b",
                        "biome_3",
                        1),
                    CreateProfiledMini(
                        "mini_late",
                        "biome_1",
                        2)
                },
                new[] { weapon },
                weapon.Id);
        }

        static EnemyDefinition CreateProfiledMini(
            string id,
            string themeId,
            int stageIndexMin)
        {
            var phases = new[]
            {
                new BossPhase(
                    48,
                    1,
                    2048,
                    60,
                    BossMovementPattern.Stationary,
                    0,
                    1,
                    1,
                    BossPartVulnerability.Legacy,
                    120,
                    0),
                new BossPhase(
                    30,
                    3,
                    2560,
                    60,
                    BossMovementPattern.VerticalSine,
                    512,
                    1,
                    90,
                    BossPartVulnerability.Legacy,
                    105,
                    18)
            };
            var profile = new MidBossProfile(
                themeId,
                1,
                stageIndexMin,
                99,
                phases);
            return new EnemyDefinition(
                id,
                id,
                10_000,
                0,
                1000,
                EnemyMovePattern.Static,
                0,
                1,
                48,
                256,
                192,
                0,
                0,
                1,
                1,
                0,
                1,
                0,
                0,
                null,
                profile);
        }

        static RewardCatalog CreateBiomeTwoOnlyRewards()
        {
            return new RewardCatalog(
                RunManager.MainRewardOptionCount,
                new[]
                {
                    new RewardDefinition(
                        "late_capsules",
                        RewardType.Capsules,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        2,
                        99),
                    new RewardDefinition(
                        "late_main",
                        RewardType.SlotLevel,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        2,
                        99),
                    new RewardDefinition(
                        "late_shield",
                        RewardType.ShieldStock,
                        PowerUpSlot.Shield,
                        1,
                        1,
                        2,
                        99)
                });
        }

        static void DefeatBoss(RunManager run)
        {
            InputCommand fire = new InputCommand(0, 0, true);
            for (int guard = 0;
                guard < 300 && run.State == RunState.Playing;
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
