using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class RngTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new Rng(12345UL);
            var b = new Rng(12345UL);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong(), $"diverged at draw {i}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new Rng(1UL);
            var b = new Rng(2UL);
            bool anyDifferent = false;
            for (int i = 0; i < 100; i++)
                if (a.NextULong() != b.NextULong()) { anyDifferent = true; break; }
            Assert.IsTrue(anyDifferent, "seeds 1 and 2 produced identical 100-draw prefixes");
        }

        [Test]
        public void NextInt_StaysWithinRange()
        {
            var rng = new Rng(42UL);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(-3, 7);
                Assert.IsTrue(v >= -3 && v < 7, $"value {v} out of [-3, 7)");
            }
        }

        [Test]
        public void NextInt_ReachesBothEndpoints()
        {
            var rng = new Rng(7UL);
            bool sawMin = false, sawMax = false;
            for (int i = 0; i < 2000; i++)
            {
                int v = rng.NextInt(0, 8);
                if (v == 0) sawMin = true;
                if (v == 7) sawMax = true;
            }
            Assert.IsTrue(sawMin, "never drew the minimum value");
            Assert.IsTrue(sawMax, "never drew the maximum value");
        }

        [Test]
        public void NextInt_InvalidRange_Throws()
        {
            var rng = new Rng(1UL);
            Assert.Throws<ArgumentException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentException>(() => rng.NextInt(5, 4));
        }

        [Test]
        public void NextDouble_InUnitInterval()
        {
            var rng = new Rng(99UL);
            for (int i = 0; i < 10000; i++)
            {
                double d = rng.NextDouble();
                Assert.IsTrue(d >= 0.0 && d < 1.0, $"value {d} out of [0, 1)");
            }
        }

        [Test]
        public void NextDouble_MeanIsHalf()
        {
            var rng = new Rng(2024UL);
            const int n = 100000;
            double sum = 0;
            for (int i = 0; i < n; i++) sum += rng.NextDouble();
            double mean = sum / n;
            Assert.AreEqual(0.5, mean, 0.005, "mean of uniform [0,1) draws too far from 0.5");
        }

        [Test]
        public void NextInt_IsUniformAcrossBuckets()
        {
            var rng = new Rng(555UL);
            const int buckets = 8;
            const int n = 80000;
            var counts = new int[buckets];
            for (int i = 0; i < n; i++) counts[rng.NextInt(0, buckets)]++;
            const int expected = n / buckets;
            for (int b = 0; b < buckets; b++)
                Assert.AreEqual(expected, counts[b], 500, $"bucket {b} count {counts[b]} deviates from {expected}");
        }

        [Test]
        public void Fork_SameStreamId_IsReproducible()
        {
            var f1 = new Rng(31337UL).Fork(3);
            var f2 = new Rng(31337UL).Fork(3);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(f1.NextULong(), f2.NextULong());
        }

        [Test]
        public void Fork_DifferentStreamIds_AreIndependent()
        {
            var parent = new Rng(31337UL);
            var stage = parent.Fork(0);
            var drops = parent.Fork(1);
            bool anyDifferent = false;
            for (int i = 0; i < 100; i++)
                if (stage.NextULong() != drops.NextULong()) { anyDifferent = true; break; }
            Assert.IsTrue(anyDifferent, "streams 0 and 1 produced identical sequences");
        }

        [Test]
        public void Fork_UnaffectedByParentConsumption()
        {
            var untouched = new Rng(777UL);
            var consumed = new Rng(777UL);
            for (int i = 0; i < 500; i++) consumed.NextULong();

            var a = untouched.Fork(2);
            var b = consumed.Fork(2);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong(),
                    "fork depends on parent consumption — stream isolation broken");
        }

        [Test]
        public void PickWeighted_Respects3To1Ratio()
        {
            var rng = new Rng(4242UL);
            var weights = new[] { 3, 1 };
            const int n = 100000;
            int first = 0;
            for (int i = 0; i < n; i++)
                if (rng.PickWeighted(weights) == 0) first++;
            double ratio = (double)first / n;
            Assert.AreEqual(0.75, ratio, 0.02, $"3:1 weighted draw measured {ratio}");
        }

        [Test]
        public void PickWeighted_ZeroTotal_Throws()
        {
            var rng = new Rng(1UL);
            Assert.Throws<ArgumentException>(() => rng.PickWeighted(new[] { 0, 0, 0 }));
        }

        [Test]
        public void PickWeighted_NegativeWeight_Throws()
        {
            var rng = new Rng(1UL);
            Assert.Throws<ArgumentException>(() => rng.PickWeighted(new[] { 2, -1 }));
        }
    }
}
