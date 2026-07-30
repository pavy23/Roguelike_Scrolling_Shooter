using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class PowerUpGrowthSimulationTests
    {
        [Test]
        public void ProvisionalCurve_With170Capsules_ReachesFullPowerAtRunEndBand()
        {
            const int capsuleSupply = 170;
            const int stageCount = 15;
            var gauge = new PowerUpGauge(
                new[] { 5, 3, 4, 3 },
                PowerUpCostCurve.CreateProvisional());
            int[] maxStage = { -1, -1, -1, -1 };
            int target = 0;
            int capsulesUsed = 0;

            while (capsulesUsed < capsuleSupply)
            {
                while (gauge.GetLevel((PowerUpSlot)target)
                    >= gauge.GetMaxLevel((PowerUpSlot)target))
                {
                    target = (target + 1) % PowerUpGauge.SlotCount;
                }

                int routeCost = target + 1;
                if (capsulesUsed + routeCost > capsuleSupply)
                    break;
                for (int step = 0; step < routeCost; step++)
                    gauge.Collect();
                capsulesUsed += routeCost;
                gauge.ActivateDetailed();
                PowerUpSlot slot = (PowerUpSlot)target;
                if (gauge.GetLevel(slot) == gauge.GetMaxLevel(slot)
                    && maxStage[target] < 0)
                {
                    maxStage[target] = DivideCeiling(
                        capsulesUsed * stageCount,
                        capsuleSupply);
                }
                target = (target + 1) % PowerUpGauge.SlotCount;
            }

            AssertAll(() =>
            {
                CollectionAssert.AreEqual(
                    new[] { 4, 3, 4, 3 },
                    gauge.ExportLevels());
                CollectionAssert.AreEqual(
                    new[] { 8, 0, 0, 0 },
                    gauge.ExportProgress());
                CollectionAssert.AreEqual(
                    new[] { -1, 10, 15, 10 },
                    maxStage);
                Assert.AreEqual(
                    13,
                    gauge.GetRemainingCapsules(PowerUpSlot.MainShot));
                Assert.AreEqual(183, RoutedCapsulesForFullPower());
                Assert.AreEqual(
                    17,
                    DivideCeiling(
                        RoutedCapsulesForFullPower() * stageCount,
                        capsuleSupply));
            });
        }

        static int DivideCeiling(int numerator, int denominator)
        {
            return (numerator + denominator - 1) / denominator;
        }

        static int RoutedCapsulesForFullPower()
        {
            int[] maximums = { 5, 3, 4, 3 };
            PowerUpCostCurve curve =
                PowerUpCostCurve.CreateProvisional();
            int total = 0;
            for (int slot = 0; slot < maximums.Length; slot++)
            {
                for (int level = 0; level < maximums[slot]; level++)
                    total += curve.GetCostForCurrentLevel(level)
                        * (slot + 1);
            }
            return total;
        }

        static void AssertAll(Action assert) => assert();
    }
}
