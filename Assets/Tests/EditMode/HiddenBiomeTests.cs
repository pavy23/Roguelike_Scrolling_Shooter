using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class HiddenBiomeTests
    {
        [Test]
        public void HiddenConditionBoundaryRequiresTwoOfThree()
        {
            Assert.IsFalse(
                RunManager.MeetsHiddenBiomeConditions(2, 1, 0));
            Assert.IsFalse(
                RunManager.MeetsHiddenBiomeConditions(3, 1, 0));
            Assert.IsFalse(
                RunManager.MeetsHiddenBiomeConditions(2, 2, 0));
            Assert.IsFalse(
                RunManager.MeetsHiddenBiomeConditions(2, 1, 1));

            Assert.IsTrue(
                RunManager.MeetsHiddenBiomeConditions(3, 2, 0));
            Assert.IsTrue(
                RunManager.MeetsHiddenBiomeConditions(3, 1, 1));
            Assert.IsTrue(
                RunManager.MeetsHiddenBiomeConditions(2, 2, 1));
            Assert.AreEqual(
                3,
                RunManager.CountHiddenBiomeConditions(3, 2, 1));
        }

        [Test]
        public void SelectionIsSeedDeterministicAndWeightsAwayFromLastBoss()
        {
            for (ulong seed = 0; seed < 64; seed++)
            {
                ColossalBossKind first =
                    RunManager.SelectColossalBoss(
                        seed,
                        ColossalBossKind.Leviathan);
                ColossalBossKind second =
                    RunManager.SelectColossalBoss(
                        seed,
                        ColossalBossKind.Leviathan);
                Assert.AreEqual(first, second);
            }

            int broodmother = 0;
            for (ulong seed = 0; seed < 256; seed++)
                if (RunManager.SelectColossalBoss(
                        seed,
                        ColossalBossKind.Leviathan)
                    == ColossalBossKind.Broodmother)
                    broodmother++;
            Assert.Greater(
                broodmother,
                160,
                "The boss not seen last must receive the 3:1 weight.");
        }

        [Test]
        public void QualifiedRunAddsTwoRoomsAndProducesPerfectClear()
        {
            MetaState meta = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            RunManager run = CreateRun(
                41UL,
                new RunProgressionConfig(2, 2),
                meta,
                EncounterType.Elite);

            AdvanceToHiddenBoss(run);

            Assert.IsTrue(run.IsHiddenBiome);
            Assert.IsTrue(run.IsBiomeBoss);
            Assert.AreEqual(run.BiomeCount, run.BiomeIndex);
            Assert.AreEqual(2, run.RoomIndex);
            Assert.AreEqual(4, run.EliteRoomsCleared);
            Assert.AreEqual(2, run.NoHitBiomesCleared);
            Assert.AreEqual(0, run.RareEncountersCleared);
            Assert.AreEqual(2, run.HiddenConditionCount);
            Assert.AreEqual(
                run.SelectedColossalBoss,
                meta.LastColossalBoss);

            CompleteCurrentBattle(run);

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.AreEqual(
                RunCompletionGrade.PerfectClear,
                run.CompletionGrade);
        }

        [Test]
        public void UnqualifiedRunUsesStandardClearGrade()
        {
            RunManager run = CreateRun(
                7UL,
                new RunProgressionConfig(1, 1),
                null,
                EncounterType.Normal);

            AdvanceUntilFinished(run);

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.IsFalse(run.IsHiddenBiome);
            Assert.AreEqual(
                RunCompletionGrade.StandardClear,
                run.CompletionGrade);
        }

        [Test]
        public void MissingColossalContentFinishesQualifiedRunWithoutStalling()
        {
            MetaState meta = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            var unsupported =
                new HiddenGenerator(EncounterType.Elite, false);
            RunManager run = CreateRun(
                0x5803UL,
                new RunProgressionConfig(2, 2),
                meta,
                EncounterType.Elite,
                unsupported);

            AdvanceUntilFinished(run);

            Assert.AreEqual(RunState.RunCleared, run.State);
            Assert.AreEqual(
                RunCompletionGrade.StandardClear,
                run.CompletionGrade);
            Assert.IsFalse(run.IsHiddenBiome);
            Assert.AreEqual(
                ColossalBossKind.None,
                run.SelectedColossalBoss);
            Assert.AreEqual(
                ColossalBossKind.None,
                meta.LastColossalBoss);
        }

        [Test]
        public void HiddenBossBoundaryResumesAndRecordingPreservesMetaInput()
        {
            MetaState meta = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            meta.RecordColossalBossEncounter(
                ColossalBossKind.Leviathan);
            var progression = new RunProgressionConfig(2, 2);
            var generator = new HiddenGenerator(EncounterType.Elite);
            RunManager original = CreateRun(
                91UL,
                progression,
                meta,
                EncounterType.Elite,
                generator);
            AdvanceToHiddenBoss(original);
            RunSuspendData suspend = original.ExportSuspendData();

            RunManager resumed =
                RunManager.ResumeFromSuspendData(
                    suspend,
                    generator,
                    Config(),
                    Content(),
                    PowerUpGauge.CreateDefault());

            Assert.IsTrue(resumed.IsHiddenBiome);
            Assert.IsTrue(resumed.IsBiomeBoss);
            Assert.AreEqual(
                original.SelectedColossalBoss,
                resumed.SelectedColossalBoss);
            Assert.AreEqual(
                original.LastColossalBossAtRunStart,
                resumed.LastColossalBossAtRunStart);
            Assert.AreEqual(
                original.EliteRoomsCleared,
                resumed.EliteRoomsCleared);
            Assert.AreEqual(
                original.NoHitBiomesCleared,
                resumed.NoHitBiomesCleared);

            var recorder = new InputRecorder(original);
            InputCommand fire = new InputCommand(0, 0, true);
            recorder.Record(in fire);
            InputPlayback playback =
                new InputPlayback(recorder.Export());
            Assert.AreEqual(
                ColossalBossKind.Leviathan,
                playback.LastColossalBossAtRunStart);
            var replayRun = new RunManager(
                91UL,
                generator,
                Config(),
                Content(),
                PowerUpGauge.CreateDefault(),
                progression,
                playback.LastColossalBossAtRunStart);
            AdvanceToHiddenBoss(replayRun);
            Assert.AreEqual(
                original.SelectedColossalBoss,
                replayRun.SelectedColossalBoss);

            for (int tick = 0;
                tick < 500
                    && (!original.IsFinished
                        || !resumed.IsFinished);
                tick++)
            {
                original.Step(in fire);
                resumed.Step(in fire);
                Assert.AreEqual(original.State, resumed.State);
                Assert.AreEqual(
                    original.CompletionGrade,
                    resumed.CompletionGrade);
            }
            Assert.AreEqual(
                RunCompletionGrade.PerfectClear,
                original.CompletionGrade);
            Assert.AreEqual(
                RunCompletionGrade.PerfectClear,
                resumed.CompletionGrade);
        }

        [Test]
        public void MetaRoundTripPersistsLastColossalBoss()
        {
            MetaState source = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            source.RecordColossalBossEncounter(
                ColossalBossKind.Broodmother);

            MetaState restored =
                MetaState.FromData(source.ExportData());

            Assert.AreEqual(
                ColossalBossKind.Broodmother,
                restored.LastColossalBoss);
        }

        [Test]
        public void PreviousRunAndRecordingSchemasDefaultHiddenFields()
        {
            RunManager run = CreateRun(
                17UL,
                new RunProgressionConfig(1, 1),
                null,
                EncounterType.Normal);
            RunSuspendData legacySuspend = run.ExportSuspendData();
            legacySuspend.schemaVersion = 6;
            legacySuspend.checksum = null;
            legacySuspend.isHiddenBiome = true;
            legacySuspend.eliteRoomsCleared = 99;
            legacySuspend.noHitBiomesCleared = 99;
            legacySuspend.rareEncountersCleared = 99;
            legacySuspend.currentBiomeHit = true;
            legacySuspend.selectedColossalBoss =
                (int)ColossalBossKind.Broodmother;
            legacySuspend.lastColossalBossAtRunStart =
                (int)ColossalBossKind.Leviathan;

            RunSuspendData migratedSuspend =
                SaveDataIntegrity.MigrateAndValidate(legacySuspend);

            Assert.IsFalse(migratedSuspend.isHiddenBiome);
            Assert.AreEqual(0, migratedSuspend.eliteRoomsCleared);
            Assert.AreEqual(0, migratedSuspend.noHitBiomesCleared);
            Assert.AreEqual(0, migratedSuspend.rareEncountersCleared);
            Assert.IsFalse(migratedSuspend.currentBiomeHit);
            Assert.AreEqual(0, migratedSuspend.selectedColossalBoss);
            Assert.AreEqual(
                0,
                migratedSuspend.lastColossalBossAtRunStart);

            var legacyRecording = new InputRecordingData
            {
                schemaVersion = 7,
                totalTicks = 0,
                runs = Array.Empty<InputRunData>(),
                routeChoices = Array.Empty<RouteChoiceData>(),
                difficultyMultiplierNumerator = 1,
                difficultyMultiplierDenominator = 1,
                finalStageIndex = 1,
                biomeCount = 1,
                roomsPerBiome = 1,
                lastColossalBossAtRunStart =
                    (int)ColossalBossKind.Broodmother
            };

            InputRecordingData migratedRecording =
                SaveDataIntegrity.MigrateAndValidate(legacyRecording);

            Assert.AreEqual(
                0,
                migratedRecording.lastColossalBossAtRunStart);
            Assert.IsTrue(
                SaveDataIntegrity.HasValidChecksum(migratedRecording));
        }

        [Test]
        public void PreviousChecksummedMetaSchemaDefaultsLastBoss()
        {
            var legacy = new MetaStateData
            {
                schemaVersion = 1,
                totalCurrency = 25,
                unlockedShipIds = new[] { "default" },
                selectedShipId = "default",
                lastColossalBoss =
                    (int)ColossalBossKind.Broodmother,
                checksum = "7740EF5C938115AB"
            };

            MetaStateData migrated =
                SaveDataIntegrity.MigrateAndValidate(legacy);

            Assert.AreEqual(0, migrated.lastColossalBoss);
            Assert.IsTrue(
                SaveDataIntegrity.HasValidChecksum(migrated));
        }

        static RunManager CreateRun(
            ulong seed,
            RunProgressionConfig progression,
            MetaState meta,
            EncounterType encounterType,
            HiddenGenerator generator = null)
        {
            generator = generator
                ?? new HiddenGenerator(encounterType);
            return meta == null
                ? new RunManager(
                    seed,
                    generator,
                    Config(),
                    Content(),
                    PowerUpGauge.CreateDefault(),
                    progression)
                : new RunManager(
                    seed,
                    generator,
                    Config(),
                    Content(),
                    PowerUpGauge.CreateDefault(),
                    progression,
                    meta);
        }

        static void AdvanceToHiddenBoss(RunManager run)
        {
            InputCommand fire = new InputCommand(0, 0, true);
            for (int tick = 0;
                tick < 2000
                    && !(run.IsHiddenBiome
                        && run.IsBiomeBoss);
                tick++)
            {
                if (run.State == RunState.AwaitingReward)
                    run.ChooseReward(0);
                else
                    run.Step(in fire);
                Assert.LessOrEqual(
                    run.BiomeIndex,
                    run.BiomeCount,
                    "Public biome progression must never expose BIOME 6/5.");
            }
            Assert.IsTrue(run.IsHiddenBiome);
            Assert.IsTrue(run.IsBiomeBoss);
        }

        static void AdvanceUntilFinished(RunManager run)
        {
            InputCommand fire = new InputCommand(0, 0, true);
            for (int tick = 0;
                tick < 2000 && !run.IsFinished;
                tick++)
            {
                if (run.State == RunState.AwaitingReward)
                    run.ChooseReward(0);
                else
                    run.Step(in fire);
            }
            Assert.IsTrue(run.IsFinished);
        }

        static void CompleteCurrentBattle(RunManager run)
        {
            InputCommand fire = new InputCommand(0, 0, true);
            for (int tick = 0;
                tick < 500 && !run.IsFinished;
                tick++)
                run.Step(in fire);
            Assert.IsTrue(run.IsFinished);
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerMinX = 0;
            config.PlayerMaxX = 0;
            config.PlayerMinY = 0;
            config.PlayerMaxY = 0;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMaxHp = 999;
            config.MainShotBaseDamage = 1;
            config.FireIntervalTicks = 1;
            config.PlayerBulletSpeedPerTick = 100;
            config.MainShotHalfWidth = 20;
            config.MainShotHalfHeight = 20;
            config.BulletDespawnX = 1000;
            config.EnemyDespawnX = -1000;
            config.MaxBullets = 64;
            return config;
        }

        static BattleContent Content()
        {
            var weapon = new WeaponDefinition(
                "hidden_test_shot",
                1,
                1,
                100,
                1,
                20,
                20);
            return new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
        }

        sealed class HiddenGenerator :
            IStageGenerator,
            IColossalBossStageGenerator
        {
            readonly EncounterType _encounterType;
            readonly bool _supportsColossalBoss;

            public HiddenGenerator(
                EncounterType encounterType,
                bool supportsColossalBoss = true)
            {
                _encounterType = encounterType;
                _supportsColossalBoss = supportsColossalBoss;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "tiny_room",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "legacy_boss",
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<BossPhase>(),
                    null,
                    null,
                    _encounterType);
            }

            public bool CanGenerateColossalBoss(
                ColossalBossKind kind)
            {
                return _supportsColossalBoss
                    && (kind == ColossalBossKind.Leviathan
                        || kind == ColossalBossKind.Broodmother);
            }

            public StagePlan GenerateColossalBoss(
                ulong seed,
                int stageIndex,
                int difficulty,
                ColossalBossKind kind)
            {
                string id = kind == ColossalBossKind.Leviathan
                    ? SegmentStageGenerator.LeviathanBossId
                    : SegmentStageGenerator.BroodmotherBossId;
                var core = new BossPartDefinition(
                    "core",
                    0,
                    0,
                    200,
                    200,
                    1,
                    true,
                    null,
                    BossPartAttackProfile.None,
                    0);
                return new StagePlan(
                    Array.Empty<StageSegment>(),
                    id,
                    1,
                    1,
                    1,
                    1,
                    200,
                    200,
                    300,
                    new[] { new BossPhase(9999, 1, 1, 1) },
                    null,
                    null,
                    EncounterType.Normal,
                    new[] { core });
            }
        }
    }
}
