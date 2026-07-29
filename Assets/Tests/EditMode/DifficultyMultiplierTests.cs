using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class DifficultyMultiplierTests
    {
        [TestCase(3, 4, 8)]
        [TestCase(1, 1, 10)]
        [TestCase(3, 2, 15)]
        public void MultiplierVariants_AreDeterministicAndScaleEnemyHp(
            int numerator,
            int denominator,
            int expectedHp)
        {
            RunManager first = CreateRun(
                0xD1FF1C017UL,
                numerator,
                denominator,
                false);
            RunManager second = CreateRun(
                0xD1FF1C017UL,
                numerator,
                denominator,
                false);
            InputCommand none = InputCommand.None;

            for (int tick = 0; tick < 40; tick++)
            {
                first.Step(in none);
                second.Step(in none);

                var firstHash = new DeterminismAuditHasher();
                var secondHash = new DeterminismAuditHasher();
                firstHash.FoldRunState(first);
                secondHash.FoldRunState(second);
                Assert.AreEqual(
                    firstHash.Hash,
                    secondHash.Hash,
                    $"difficulty determinism diverged at tick {tick + 1}");
            }

            Assert.AreEqual(1, first.Battle.Enemies.Count);
            Assert.AreEqual(expectedHp, first.Battle.Enemies[0].Hp);
        }

        [Test]
        public void RewardsAndShipConstructor_ReducesMultiplierAndScalesBossHp()
        {
            RunManager run = CreateRun(
                0xB055UL,
                6,
                4,
                true);
            InputCommand none = InputCommand.None;
            for (int guard = 0;
                guard < 100
                    && !(run.IsBiomeBoss
                        && run.Battle is BattleSim bossBattle
                        && bossBattle.BossActive);
                guard++)
                run.Step(in none);

            Assert.AreEqual(3, run.DifficultyMultiplierNumerator);
            Assert.AreEqual(2, run.DifficultyMultiplierDenominator);
            Assert.IsTrue(run.Battle.BossActive);
            Assert.AreEqual(17, run.Battle.Boss.MaxHp);
            Assert.AreEqual(17, run.Battle.Boss.Hp);
        }

        [Test]
        public void ExistingRewardsAndShipConstructor_DefaultsToNormal()
        {
            RunManager run = new RunManager(
                7UL,
                new DifficultyStageGenerator(false),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault());
            InputCommand none = InputCommand.None;
            run.Step(in none);

            Assert.AreEqual(1, run.DifficultyMultiplierNumerator);
            Assert.AreEqual(1, run.DifficultyMultiplierDenominator);
            Assert.AreEqual(10, run.Battle.Enemies[0].Hp);
        }

        [Test]
        public void NonPositiveMultiplier_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateRun(1UL, 0, 1, false));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateRun(1UL, 1, 0, false));
        }

        [Test]
        public void SuspendResume_PreservesDifficultyAndTrajectory()
        {
            RunManager source = CreateRun(
                0x5A5EEDUL,
                3,
                2,
                false);
            InputCommand none = InputCommand.None;
            source.Step(in none);
            RunSuspendData data = source.ExportSuspendData();

            RunManager continuous = RunManager.ResumeFromSuspendData(
                data,
                new DifficultyStageGenerator(false),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault());
            RunManager resumed = RunManager.ResumeFromSuspendData(
                data,
                new DifficultyStageGenerator(false),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault());

            Assert.AreEqual(3, data.difficultyMultiplierNumerator);
            Assert.AreEqual(2, data.difficultyMultiplierDenominator);
            Assert.AreEqual(3, resumed.DifficultyMultiplierNumerator);
            Assert.AreEqual(2, resumed.DifficultyMultiplierDenominator);

            for (int tick = 0; tick < 60; tick++)
            {
                InputCommand input = InputForTick(tick);
                continuous.Step(in input);
                resumed.Step(in input);

                var continuousHash = new DeterminismAuditHasher();
                var resumedHash = new DeterminismAuditHasher();
                continuousHash.FoldRunState(continuous);
                resumedHash.FoldRunState(resumed);
                Assert.AreEqual(
                    continuousHash.Hash,
                    resumedHash.Hash,
                    $"resume diverged at tick {tick + 1}");
            }

            Assert.AreEqual(15, resumed.Battle.Enemies[0].Hp);
        }

        [Test]
        public void RecordingPlayback_PreservesDifficultyForReplayConstruction()
        {
            const ulong seed = 0x7E91A7UL;
            RunManager recorded = CreateRun(seed, 3, 2, false);
            var recorder = new InputRecorder(recorded);
            var recordedHasher = new DeterminismAuditHasher();

            for (int tick = 0; tick < 90; tick++)
            {
                InputCommand input = InputForTick(tick);
                recorder.Record(in input);
                recorded.Step(in input);
                recordedHasher.FoldRunState(recorded);
            }

            InputRecordingData data = recorder.Export();
            var playback = new InputPlayback(data);
            RunManager replayed = CreateRun(
                seed,
                playback.DifficultyMultiplierNumerator,
                playback.DifficultyMultiplierDenominator,
                false);
            var replayedHasher = new DeterminismAuditHasher();
            foreach (InputCommand input in playback)
            {
                replayed.Step(in input);
                replayedHasher.FoldRunState(replayed);
            }

            Assert.AreEqual(3, data.difficultyMultiplierNumerator);
            Assert.AreEqual(2, data.difficultyMultiplierDenominator);
            Assert.AreEqual(3, playback.DifficultyMultiplierNumerator);
            Assert.AreEqual(2, playback.DifficultyMultiplierDenominator);
            Assert.AreEqual(recordedHasher.Hash, replayedHasher.Hash);
            Assert.AreEqual(
                recorded.Battle.Enemies[0].Hp,
                replayed.Battle.Enemies[0].Hp);
        }

        static RunManager CreateRun(
            ulong seed,
            int numerator,
            int denominator,
            bool includeBoss)
        {
            return new RunManager(
                seed,
                new DifficultyStageGenerator(includeBoss),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault(),
                numerator,
                denominator);
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerMaxHp = 50;
            config.PlayerSpawnX = -1000;
            config.PlayerSpawnY = -1000;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            return config;
        }

        static BattleContent CreateContent()
        {
            var enemy = new EnemyDefinition(
                "target",
                10,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var weapon = new WeaponDefinition(
                "shot",
                1,
                8,
                0,
                1,
                0,
                0);
            return new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
        }

        static RewardCatalog CreateRewards()
        {
            return new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    new RewardDefinition(
                        "capsule",
                        RewardType.Capsules,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        int.MaxValue),
                    new RewardDefinition(
                        "main",
                        RewardType.SlotLevel,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        int.MaxValue),
                    new RewardDefinition(
                        "repair",
                        RewardType.RepairHp,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        int.MaxValue)
                });
        }

        static InputCommand InputForTick(int tick)
        {
            return new InputCommand(
                tick % 5 < 2 ? 1 : -1,
                tick % 7 < 3 ? 1 : -1,
                tick % 3 == 0);
        }

        sealed class DifficultyStageGenerator : IStageGenerator
        {
            readonly bool _includeBoss;

            public DifficultyStageGenerator(bool includeBoss)
            {
                _includeBoss = includeBoss;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                int length = _includeBoss ? 1 : 1000;
                var segment = new StageSegment(
                    "difficulty",
                    length,
                    new[]
                    {
                        new SpawnEvent(0, "target", 1000, 1000)
                    },
                    1,
                    1,
                    new[] { 1 });
                if (!_includeBoss)
                {
                    return new StagePlan(
                        new[] { segment },
                        "none",
                        1,
                        1,
                        1);
                }

                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1,
                    11,
                    1,
                    1,
                    1000,
                    new[] { new BossPhase(999, 1, 0, 1) });
            }
        }
    }
}
