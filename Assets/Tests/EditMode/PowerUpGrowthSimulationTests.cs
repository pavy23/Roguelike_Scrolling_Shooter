using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class PowerUpGrowthSimulationTests
    {
        [Test]
        public void ProvisionalCurve_With170Capsules_RoutesAcrossCompleteGauge()
        {
            const int capsuleSupply = 170;
            var gauge = PowerUpGauge.CreateDefault();
            var completed = new bool[gauge.GaugeSlotCount];
            int target = 0;
            int capsulesUsed = 0;
            int completedCount = 0;

            while (capsulesUsed < capsuleSupply
                && completedCount < completed.Length)
            {
                while (completed[target])
                    target = (target + 1) % completed.Length;

                PowerUpSlot slot =
                    gauge.GaugeSlots[target].Slot;
                int routeCost = target + 1;
                if (capsulesUsed + routeCost > capsuleSupply)
                    break;
                for (int step = 0; step < routeCost; step++)
                    gauge.Collect();
                capsulesUsed += routeCost;
                gauge.ActivateDetailed();
                if (gauge.GetLevel(slot)
                    == gauge.GetMaxLevel(slot))
                {
                    completed[target] = true;
                    completedCount++;
                }
                target = (target + 1) % completed.Length;
            }

            Assert.AreEqual(
                PowerUpGauge.DefaultGaugeSlotCount,
                gauge.GaugeSlotCount);
            Assert.AreEqual(PowerUpSlot.Speed, gauge.GaugeSlots[0].Slot);
            Assert.AreEqual(PowerUpSlot.Shield, gauge.GaugeSlots[6].Slot);
            Assert.Greater(capsulesUsed, 0);
            Assert.LessOrEqual(capsulesUsed, capsuleSupply);
            Assert.Greater(gauge.GetLevel(PowerUpSlot.Speed), 0);
            Assert.AreNotEqual(
                PowerUpWeaponMode.None,
                gauge.ActiveWeaponMode);
        }
    }
}
