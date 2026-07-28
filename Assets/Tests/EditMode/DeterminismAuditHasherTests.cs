using NUnit.Framework;
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
    }
}
