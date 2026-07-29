using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class MetaProgressionTests
    {
        [Test]
        public void FullCarry_PreservesAllLevels()
        {
            var meta = new MetaProgression(1.0);
            var carried = meta.ApplyDeathCarry(new[] { 5, 3, 4, 1 });
            CollectionAssert.AreEqual(new[] { 5, 3, 4, 1 }, carried);
        }

        [Test]
        public void ZeroCarry_ResetsAllLevels()
        {
            var meta = new MetaProgression(0.0);
            var carried = meta.ApplyDeathCarry(new[] { 5, 3, 4, 1 });
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, carried);
        }

        [Test]
        public void HalfCarry_FloorsEachLevel()
        {
            var meta = new MetaProgression(0.5);
            var carried = meta.ApplyDeathCarry(new[] { 5, 3, 4, 1 });
            CollectionAssert.AreEqual(new[] { 2, 1, 2, 0 }, carried);
            Assert.AreEqual(1, meta.CarryNumerator);
            Assert.AreEqual(2, meta.CarryDenominator);
            Assert.AreEqual(0.5, meta.CarryFraction);
        }

        [Test]
        public void IntegerFraction_ReducesAndPreservesLegacyResult()
        {
            var meta = new MetaProgression(2, 4);
            var carried = meta.ApplyDeathCarry(new[] { 5, 3, 4, 1 });

            CollectionAssert.AreEqual(new[] { 2, 1, 2, 0 }, carried);
            Assert.AreEqual(1, meta.CarryNumerator);
            Assert.AreEqual(2, meta.CarryDenominator);
        }

        [Test]
        public void CarryFraction_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaProgression(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MetaProgression(1.1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MetaProgression(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MetaProgression(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new MetaProgression(3, 2));
        }

        [Test]
        public void ApplyDeathCarry_NullLevels_Throws()
        {
            var meta = new MetaProgression(1.0);
            Assert.Throws<ArgumentNullException>(() => meta.ApplyDeathCarry(null));
        }

        [Test]
        public void ApplyDeathCarry_DoesNotMutateInput()
        {
            var meta = new MetaProgression(0.5);
            var input = new[] { 5, 3, 4, 1 };
            meta.ApplyDeathCarry(input);
            CollectionAssert.AreEqual(new[] { 5, 3, 4, 1 }, input);
        }
    }
}
