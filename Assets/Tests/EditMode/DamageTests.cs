using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class DamageTests
    {
        [Test]
        public void Level1_ReturnsBaseDamage()
        {
            Assert.AreEqual(10, Damage.Compute(10, 1));
        }

        [Test]
        public void LevelCurve_AddsHalfBasePerLevel()
        {
            Assert.AreEqual(15, Damage.Compute(10, 2));
            Assert.AreEqual(20, Damage.Compute(10, 3));
            Assert.AreEqual(30, Damage.Compute(10, 5));
        }

        [Test]
        public void IntegerDivision_FloorsResult()
        {
            Assert.AreEqual(7, Damage.Compute(5, 2));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Damage.Compute(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Damage.Compute(10, 0));
        }

        [Test]
        public void ApplyToHp_ReducesHp()
        {
            Assert.AreEqual(70, Damage.ApplyToHp(100, 30));
        }

        [Test]
        public void ApplyToHp_FloorsAtZero_AndIgnoresNegativeDamage()
        {
            Assert.AreEqual(0, Damage.ApplyToHp(10, 50));
            Assert.AreEqual(10, Damage.ApplyToHp(10, -5));
        }
    }
}
