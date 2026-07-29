using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    public sealed class DailySeedTests
    {
        [Test]
        public void FromDate_HasStablePlatformIndependentVector()
        {
            ulong seed = DailySeed.FromDate(20260729);

            Assert.AreEqual(0x712A6B0FUL, seed);
            Assert.AreEqual(seed, DailySeed.FromDate(20260729));
            Assert.AreNotEqual(seed, DailySeed.FromDate(20260730));
        }

        [Test]
        public void FromDate_AcceptsGregorianLeapDays()
        {
            Assert.DoesNotThrow(() => DailySeed.FromDate(20000229));
            Assert.DoesNotThrow(() => DailySeed.FromDate(20240229));
        }

        [Test]
        public void FromDate_RejectsInvalidCalendarDates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DailySeed.FromDate(0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DailySeed.FromDate(20261301));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DailySeed.FromDate(20260229));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DailySeed.FromDate(19000229));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DailySeed.FromDate(20260431));
        }
    }
}
