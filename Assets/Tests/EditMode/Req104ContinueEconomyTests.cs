using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class Req104ContinueEconomyTests
    {
        [Test]
        public void ContinuePurchasesUseConfigurableLadderAndPersist()
        {
            MetaState meta = CreateMeta(20_000, 0);

            ContinuePurchaseResult first =
                meta.TryPurchaseContinue();
            ContinuePurchaseResult second =
                meta.TryPurchaseContinue();

            Assert.IsTrue(first.Purchased);
            Assert.AreEqual(2_000L, first.Price);
            Assert.IsTrue(second.Purchased);
            Assert.AreEqual(3_000L, second.Price);
            Assert.AreEqual(2, meta.ContinueStock);
            Assert.AreEqual(15_000L, meta.TotalCurrency);
            MetaState restored = MetaState.FromData(meta.ExportData());
            Assert.AreEqual(2, restored.ContinueStock);
            Assert.AreEqual(15_000L, restored.TotalCurrency);
        }

        [Test]
        public void ContinueUseConsumesStockResetsScoreAndBasicPower()
        {
            MetaState meta = CreateMeta(0, 2);
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 2, 1, 1, 0, 0, 0, 0 });
            RunManager run = CreateLethalRun(0x104UL, gauge, meta);
            InputCommand fire = new InputCommand(0, 0, true);

            Step(run, 3, in fire);

            Assert.AreEqual(RunState.RunOver, run.State);
            Assert.Greater(run.TotalScore, 0L);
            Assert.IsTrue(run.ContinueAvailability.CanUse);
            Assert.IsTrue(run.TryUseContinue(out ContinueRejectionReason reason));
            Assert.AreEqual(ContinueRejectionReason.None, reason);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(0L, run.TotalScore);
            Assert.AreEqual(0, run.Battle.Tick);
            Assert.IsTrue(run.Battle.IsPlayerAlive);
            Assert.AreEqual(1, run.ContinueStock);
            Assert.AreEqual(1, meta.ContinueStock);
            Assert.AreEqual(1, run.Statistics.ContinuesUsed);
            Assert.AreEqual(1, run.ContinueDecisionHistory.Count);
            Assert.AreEqual(3, run.ContinueDecisionHistory[0].SimulationTick);
            CollectionAssert.AreEqual(
                new int[PowerUpGauge.SlotCount],
                run.PowerUpGauge.ExportLevels());
        }

        [Test]
        public void DailyRunRejectsContinueAndDoesNotConsumeMetaStock()
        {
            MetaState meta = CreateMeta(0, 2);
            RunManager run = new RunManager(
                0xDA11UL,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                meta,
                new RunConfig(isDailyRun: true));
            InputCommand fire = new InputCommand(0, 0, true);

            Step(run, 3, in fire);

            Assert.AreEqual(RunState.RunOver, run.State);
            Assert.IsFalse(run.ContinueAvailability.CanUse);
            Assert.AreEqual(
                ContinueRejectionReason.DailyRun,
                run.ContinueAvailability.RejectionReason);
            Assert.IsFalse(
                run.TryUseContinue(
                    out ContinueRejectionReason rejection));
            Assert.AreEqual(ContinueRejectionReason.DailyRun, rejection);
            Assert.AreEqual(0, run.ContinueStock);
            Assert.AreEqual(2, meta.ContinueStock);
            Assert.AreEqual(0, run.Statistics.ContinuesUsed);
        }

        [Test]
        public void FinalBossEntryWagersAllStockAndConvertsOverflowToScore()
        {
            MetaState meta = CreateMeta(0, 8);
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 1;
            config.MaxShieldStock = 3;
            var run = new RunManager(
                0xF1A1UL,
                new EmptyStageGenerator(),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new RunProgressionConfig(1, 1),
                meta);
            InputCommand none = InputCommand.None;

            run.Step(in none);

            Assert.IsTrue(run.IsBiomeBoss);
            Assert.IsTrue(run.FinalWagerCommitted);
            Assert.AreEqual(0, run.ContinueStock);
            Assert.AreEqual(0, meta.ContinueStock);
            Assert.AreEqual(5, run.MaxShieldStock);
            Assert.AreEqual(5, run.Battle.ShieldStock);
            Assert.AreEqual(4, run.FinalWagerShieldGranted);
            Assert.AreEqual(4, run.FinalWagerOverflowConverted);
            Assert.AreEqual(4_000L, run.FinalWagerScoreBonus);
            Assert.AreEqual(4_000L, run.TotalScore);
        }

        [Test]
        public void ReplayAndSuspendRoundTripContinueDecisionDeterministically()
        {
            MetaState meta = CreateMeta(0, 2);
            RunManager source = CreateLethalRun(
                0xA104UL,
                PowerUpGauge.CreateDefault(),
                meta);
            var recorder = new InputRecorder(source);
            InputCommand fire = new InputCommand(0, 0, true);
            for (int i = 0; i < 3; i++)
            {
                recorder.Record(in fire);
                source.Step(in fire);
            }
            Assert.IsTrue(source.TryUseContinue());
            RunSuspendData suspend = source.ExportSuspendData();
            var boundaryHash = new DeterminismAuditHasher();
            boundaryHash.FoldRunState(source);
            recorder.Record(in fire);
            source.Step(in fire);

            InputRecordingData recording = recorder.Export();
            var playback = new InputPlayback(recording);
            RunManager replay = new RunManager(
                0xA104UL,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                playback.CreateRunConfig());
            int tick = 0;
            int decisionIndex = 0;
            foreach (InputCommand input in playback)
            {
                replay.Step(in input);
                tick++;
                if (decisionIndex < playback.ContinueDecisions.Count
                    && playback.ContinueDecisions[decisionIndex]
                        .SimulationTick == tick)
                {
                    Assert.IsTrue(replay.TryUseContinue());
                    decisionIndex++;
                }
            }

            var sourceHash = new DeterminismAuditHasher();
            var replayHash = new DeterminismAuditHasher();
            sourceHash.FoldRunState(source);
            replayHash.FoldRunState(replay);
            Assert.AreEqual(1, recording.continueDecisions.Length);
            Assert.AreEqual(3, recording.continueDecisions[0].simulationTick);
            Assert.AreEqual(sourceHash.HexHash, replayHash.HexHash);

            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault());
            var resumedHash = new DeterminismAuditHasher();
            resumedHash.FoldRunState(resumed);
            Assert.AreEqual(1, resumed.Statistics.ContinuesUsed);
            Assert.AreEqual(1, resumed.ContinueStock);
            Assert.AreEqual(boundaryHash.HexHash, resumedHash.HexHash);
        }

        [Test]
        public void MetaResumeAcrossTwoSuspendsChargesExactlyOncePerContinue()
        {
            MetaState meta = CreateMeta(0, 2);
            RunManager source = CreateLethalRun(
                0x107UL,
                PowerUpGauge.CreateDefault(),
                meta);
            RunSuspendData firstSuspend = source.ExportSuspendData();

            RunManager firstResume = RunManager.ResumeFromSuspendData(
                firstSuspend,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                null,
                null,
                meta);
            InputCommand fire = new InputCommand(0, 0, true);
            Step(firstResume, 3, in fire);
            Assert.AreEqual(RunState.RunOver, firstResume.State);
            Assert.IsTrue(firstResume.TryUseContinue());
            Assert.AreEqual(1, firstResume.ContinueStock);
            Assert.AreEqual(1, meta.ContinueStock);

            RunSuspendData secondSuspend = firstResume.ExportSuspendData();
            RunManager secondResume = RunManager.ResumeFromSuspendData(
                secondSuspend,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                meta);

            Assert.AreEqual(1, secondResume.ContinueStock);
            Assert.AreEqual(1, secondResume.Statistics.ContinuesUsed);
            Assert.AreEqual(1, meta.ContinueStock);

            Step(secondResume, 3, in fire);
            Assert.AreEqual(RunState.RunOver, secondResume.State);
            Assert.IsTrue(secondResume.TryUseContinue());
            Assert.AreEqual(0, secondResume.ContinueStock);
            Assert.AreEqual(0, meta.ContinueStock);
        }

        [Test]
        public void MetaResumeRejectsMismatchedContinueInventoryWithoutMutation()
        {
            MetaState sourceMeta = CreateMeta(0, 2);
            RunSuspendData suspend = CreateLethalRun(
                    0xB107UL,
                    PowerUpGauge.CreateDefault(),
                    sourceMeta)
                .ExportSuspendData();
            MetaState mismatchedMeta = CreateMeta(0, 1);

            ArgumentException error = Assert.Throws<ArgumentException>(
                () => RunManager.ResumeFromSuspendData(
                    suspend,
                    new ScoreThenLethalGenerator(),
                    CreateConfig(),
                    CreateContent(),
                    PowerUpGauge.CreateDefault(),
                    mismatchedMeta));

            StringAssert.Contains("must match", error.Message);
            Assert.AreEqual(1, mismatchedMeta.ContinueStock);
        }

        [Test]
        public void DailyMetaResumeKeepsRunStockZeroAndLeavesMetaStockUntouched()
        {
            MetaState meta = CreateMeta(0, 2);
            var source = new RunManager(
                0xDA107UL,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                meta,
                new RunConfig(isDailyRun: true));
            RunSuspendData suspend = source.ExportSuspendData();

            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                meta);
            InputCommand fire = new InputCommand(0, 0, true);
            Step(resumed, 3, in fire);

            Assert.AreEqual(0, resumed.ContinueStock);
            Assert.AreEqual(2, meta.ContinueStock);
            Assert.IsFalse(resumed.ContinueAvailability.CanUse);
            Assert.AreEqual(
                ContinueRejectionReason.DailyRun,
                resumed.ContinueAvailability.RejectionReason);
        }

        [Test]
        public void MetaResumeFinalWagerConsumesRunAndMetaStockTogether()
        {
            MetaState meta = CreateMeta(0, 2);
            var source = new RunManager(
                0xF107UL,
                new EmptyStageGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new RunProgressionConfig(1, 1),
                meta);
            RunSuspendData suspend = source.ExportSuspendData();
            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new EmptyStageGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                meta);
            InputCommand none = InputCommand.None;

            resumed.Step(in none);

            Assert.IsTrue(resumed.FinalWagerCommitted);
            Assert.AreEqual(0, resumed.ContinueStock);
            Assert.AreEqual(0, meta.ContinueStock);
        }

        [Test]
        public void SameSeedAndContinueDecisionProduceSameAuditHash()
        {
            string first = ContinueTraceHash(0xD37104UL);
            string second = ContinueTraceHash(0xD37104UL);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void PreReq104ReplayAndSuspendSchemasAreRejected()
        {
            Assert.Throws<ArgumentException>(
                () => SaveDataIntegrity.MigrateAndValidate(
                    new InputRecordingData
                    {
                        schemaVersion = 22
                    }));
            Assert.Throws<ArgumentException>(
                () => SaveDataIntegrity.MigrateAndValidate(
                    new RunSuspendData
                    {
                        schemaVersion = 24
                    }));
        }

        static string ContinueTraceHash(ulong seed)
        {
            RunManager run = CreateLethalRun(
                seed,
                PowerUpGauge.CreateDefault(),
                CreateMeta(0, 1));
            InputCommand fire = new InputCommand(0, 0, true);
            Step(run, 3, in fire);
            Assert.IsTrue(run.TryUseContinue());
            run.Step(in fire);
            var hash = new DeterminismAuditHasher();
            hash.FoldRunState(run);
            return hash.HexHash;
        }

        static MetaState CreateMeta(long currency, int continueStock)
        {
            return new MetaState(
                currency,
                new[] { "default" },
                "default",
                ColossalBossKind.None,
                continueStock);
        }

        static RunManager CreateLethalRun(
            ulong seed,
            PowerUpGauge gauge,
            MetaState meta)
        {
            return new RunManager(
                seed,
                new ScoreThenLethalGenerator(),
                CreateConfig(),
                CreateContent(),
                gauge,
                meta);
        }

        static void Step(
            RunManager run,
            int count,
            in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                run.Step(in input);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -100,
                PlayerMaxX = 100,
                PlayerMinY = -100,
                PlayerMaxY = 100,
                BulletDespawnX = 100,
                EnemyDespawnX = -100,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                StartingShieldStock = 0,
                MaxShieldStock = 3,
                PlayerHitInvulnerabilityTicks = 0,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }

        static BattleContent CreateContent()
        {
            var scored = new EnemyDefinition(
                "scored", "Scored", 1, 0, 75,
                EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var lethal = new EnemyDefinition(
                "lethal", "Lethal", 10, 1, 0,
                EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var weapon = new WeaponDefinition(
                "shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { scored, lethal },
                new[] { weapon },
                weapon.Id);
        }

        sealed class ScoreThenLethalGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "continue_probe",
                    4,
                    new[]
                    {
                        new SpawnEvent(0, "scored", 1, 0),
                        new SpawnEvent(3, "lethal", 0, 0),
                        new SpawnEvent(3, "lethal", 0, 0)
                    },
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1);
            }
        }

        sealed class EmptyStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "wager_probe",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1);
            }
        }
    }
}
