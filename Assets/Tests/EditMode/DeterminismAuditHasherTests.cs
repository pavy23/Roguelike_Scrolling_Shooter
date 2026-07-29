using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class DeterminismAuditHasherTests
    {
        [Test]
        public void IdenticalTickSequencesProduceIdenticalHash()
        {
            var first = new DeterminismAuditHasher();
            var second = new DeterminismAuditHasher();

            for (int tick = 0; tick < 100; tick++)
            {
                first.FoldTick(
                    1, 1 + tick / 30, 0, tick,
                    tick * 3, -tick, tick % 7, tick % 5,
                    tick * 100L, tick % 4);
                second.FoldTick(
                    1, 1 + tick / 30, 0, tick,
                    tick * 3, -tick, tick % 7, tick % 5,
                    tick * 100L, tick % 4);
            }

            Assert.AreEqual(first.Hash, second.Hash);
            Assert.AreEqual(16, first.HexHash.Length);
        }

        [Test]
        public void EveryAuditedFieldAffectsHash()
        {
            ulong baseline = Hash(1, 2, 0, 3, 4, 5, 6, 7, 8L, 9);
            Assert.AreNotEqual(baseline, Hash(2, 2, 0, 3, 4, 5, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 3, 0, 3, 4, 5, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 1, 3, 4, 5, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 4, 4, 5, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 5, 5, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 4, 6, 6, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 4, 5, 7, 7, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 4, 5, 6, 8, 8L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 4, 5, 6, 7, 9L, 9));
            Assert.AreNotEqual(baseline, Hash(1, 2, 0, 3, 4, 5, 6, 7, 8L, 10));
        }

        [Test]
        public void FullObservableRunStateMatchesForIdenticalRunsAndChangesAfterStep()
        {
            RunManager first = CreateRun(42UL);
            RunManager second = CreateRun(42UL);
            var firstHasher = new DeterminismAuditHasher();
            var secondHasher = new DeterminismAuditHasher();
            firstHasher.FoldRunState(first);
            secondHasher.FoldRunState(second);
            Assert.AreEqual(firstHasher.Hash, secondHasher.Hash);

            var fire = new InputCommand(0, 0, true);
            first.Step(in fire);
            var changedHasher = new DeterminismAuditHasher();
            changedHasher.FoldRunState(first);
            Assert.AreNotEqual(firstHasher.Hash, changedHasher.Hash);
        }

        [Test]
        public void RewardChoiceFieldsAffectHash()
        {
            ulong baseline = RewardChoiceHash(
                1,
                0,
                new RewardOption(
                    "reward",
                    RewardType.Capsules,
                    PowerUpSlot.MainShot,
                    1));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    2, 0,
                    new RewardOption(
                        "reward", RewardType.Capsules,
                        PowerUpSlot.MainShot, 1)));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    1, 1,
                    new RewardOption(
                        "reward", RewardType.Capsules,
                        PowerUpSlot.MainShot, 1)));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    1, 0,
                    new RewardOption(
                        "other", RewardType.Capsules,
                        PowerUpSlot.MainShot, 1)));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    1, 0,
                    new RewardOption(
                        "reward", RewardType.SlotLevel,
                        PowerUpSlot.MainShot, 1)));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    1, 0,
                    new RewardOption(
                        "reward", RewardType.Capsules,
                        PowerUpSlot.Missile, 1)));
            Assert.AreNotEqual(
                baseline,
                RewardChoiceHash(
                    1, 0,
                    new RewardOption(
                        "reward", RewardType.Capsules,
                        PowerUpSlot.MainShot, 2)));
        }

        static ulong Hash(
            int runNumber,
            int stageIndex,
            int runState,
            int battleTick,
            int playerX,
            int playerY,
            int bulletCount,
            int enemyCount,
            long totalScore,
            int eventCount)
        {
            var hasher = new DeterminismAuditHasher();
            hasher.FoldTick(
                runNumber,
                stageIndex,
                runState,
                battleTick,
                playerX,
                playerY,
                bulletCount,
                enemyCount,
                totalScore,
                eventCount);
            return hasher.Hash;
        }

        static ulong RewardChoiceHash(
            int stageIndex,
            int optionIndex,
            RewardOption option)
        {
            var hasher = new DeterminismAuditHasher();
            hasher.FoldRewardChoice(
                stageIndex,
                optionIndex,
                in option);
            return hasher.Hash;
        }

        static RunManager CreateRun(ulong seed)
        {
            var weapon = new WeaponDefinition(
                "shot", 1, 1, 100, 1, 0, 0);
            return new RunManager(
                seed,
                new FixedStageGenerator(),
                BattleSimConfig.CreateDefault(),
                new BattleContent(
                    Array.Empty<EnemyDefinition>(),
                    new[] { weapon },
                    weapon.Id),
                PowerUpGauge.CreateDefault());
        }

        sealed class FixedStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "segment",
                            100,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
                    "legacy",
                    1,
                    1,
                    1);
            }
        }
    }
}
