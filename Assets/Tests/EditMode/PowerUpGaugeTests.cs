using System;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class PowerUpGaugeTests
    {
        [Test]
        public void NewGauge_HasNoSelection_AndAllLevelsZero()
        {
            var gauge = PowerUpGauge.CreateDefault();
            Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);
            foreach (PowerUpSlot slot in Enum.GetValues(typeof(PowerUpSlot)))
                Assert.AreEqual(0, gauge.GetLevel(slot));
        }

        [Test]
        public void Collect_AdvancesCursorSequentially()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            Assert.AreEqual((int)PowerUpSlot.MainShot, gauge.Cursor);
            gauge.Collect();
            Assert.AreEqual((int)PowerUpSlot.Missile, gauge.Cursor);
            gauge.Collect();
            Assert.AreEqual((int)PowerUpSlot.Option, gauge.Cursor);
            gauge.Collect();
            Assert.AreEqual((int)PowerUpSlot.Shield, gauge.Cursor);
        }

        [Test]
        public void Collect_WrapsAroundAfterLastSlot()
        {
            var gauge = PowerUpGauge.CreateDefault();
            for (int i = 0; i < PowerUpGauge.SlotCount + 1; i++) gauge.Collect();
            Assert.AreEqual((int)PowerUpSlot.MainShot, gauge.Cursor);
        }

        [Test]
        public void Activate_WithoutSelection_ReturnsFalse()
        {
            var gauge = PowerUpGauge.CreateDefault();
            Assert.IsFalse(gauge.Activate());
        }

        [Test]
        public void Activate_IncrementsLevel_AndResetsCursor()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            gauge.Collect();
            Assert.IsTrue(gauge.Activate());
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Missile));
            Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);
        }

        [Test]
        public void Activate_AtMaxLevel_ReturnsFalse_AndKeepsCursor()
        {
            var gauge = new PowerUpGauge(new[] { 1, 1, 1, 1 });
            gauge.Collect();
            Assert.IsTrue(gauge.Activate());

            gauge.Collect();
            Assert.IsFalse(gauge.Activate(), "activating a maxed slot must fail");
            Assert.AreEqual((int)PowerUpSlot.MainShot, gauge.Cursor,
                "cursor must stay so the player can keep collecting to move past a maxed slot");
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.MainShot));
        }

        [Test]
        public void CanActivate_FalseWhenSlotMaxed()
        {
            var gauge = new PowerUpGauge(new[] { 1, 2, 2, 2 });
            gauge.Collect();
            gauge.Activate();
            gauge.Collect();
            Assert.IsFalse(gauge.CanActivate);
        }

        [Test]
        public void ImportLevels_ClampsToBounds()
        {
            var gauge = new PowerUpGauge(new[] { 5, 3, 4, 3 });
            gauge.ImportLevels(new[] { 99, -1, 2, 3 });
            Assert.AreEqual(5, gauge.GetLevel(PowerUpSlot.MainShot));
            Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Missile));
            Assert.AreEqual(2, gauge.GetLevel(PowerUpSlot.Option));
            Assert.AreEqual(3, gauge.GetLevel(PowerUpSlot.Shield));
        }

        [Test]
        public void ExportLevels_ReturnsIndependentCopy()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            gauge.Activate();
            var snapshot = gauge.ExportLevels();
            snapshot[0] = 999;
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.MainShot));
        }

        [Test]
        public void Constructor_InvalidMaxLevels_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PowerUpGauge(null));
            Assert.Throws<ArgumentException>(() => new PowerUpGauge(new[] { 1, 1 }));
            Assert.Throws<ArgumentException>(() => new PowerUpGauge(new[] { 1, 0, 1, 1 }));
        }

        [Test]
        public void LevelCostCurve_UsesCurrentLevelAndExposesRemaining()
        {
            var gauge = new PowerUpGauge(
                new[] { 3, 3, 3, 3 },
                new PowerUpCostCurve(1, 1, 1));

            Assert.AreEqual(1, gauge.GetRequiredCapsules(PowerUpSlot.MainShot));
            SelectAndActivate(gauge, PowerUpSlot.MainShot);
            Assert.AreEqual(3, gauge.GetRequiredCapsules(PowerUpSlot.MainShot));

            SelectAndActivate(gauge, PowerUpSlot.MainShot);
            AssertAll(() =>
            {
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.MainShot));
                Assert.AreEqual(2, gauge.GetRemainingCapsules(PowerUpSlot.MainShot));
                Assert.AreEqual(
                    PowerUpActivationResult.ProgressAdded,
                    gauge.LastActivationResult);
            });
        }

        [Test]
        public void InsufficientActivation_PartiallyInvestsAndConsumesCursor()
        {
            var gauge = new PowerUpGauge(
                new[] { 2, 2, 2, 2 },
                new PowerUpCostCurve(2, 0, 0));
            gauge.Collect();

            PowerUpActivationResult first = gauge.ActivateDetailed();

            AssertAll(() =>
            {
                Assert.AreEqual(PowerUpActivationResult.ProgressAdded, first);
                Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.MainShot));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.MainShot));
                Assert.AreEqual(1, gauge.GetRemainingCapsules(PowerUpSlot.MainShot));
                Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);
            });

            gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.MainShot));
            Assert.AreEqual(0, gauge.GetProgress(PowerUpSlot.MainShot));
        }

        [Test]
        public void LargeDataCap_DoesNotChangeGaugeArrayShape()
        {
            var gauge = new PowerUpGauge(
                new[] { 64, 48, 32, 24 },
                new PowerUpCostCurve(1, 0, 0));
            gauge.ImportLevels(new[] { 63, 47, 31, 23 });

            AssertAll(() =>
            {
                Assert.AreEqual(PowerUpGauge.SlotCount, gauge.ExportLevels().Length);
                Assert.AreEqual(PowerUpGauge.SlotCount, gauge.ExportProgress().Length);
                Assert.AreEqual(64, gauge.GetMaxLevel(PowerUpSlot.MainShot));
                Assert.AreEqual(48, gauge.GetMaxLevel(PowerUpSlot.Missile));
                Assert.AreEqual(32, gauge.GetMaxLevel(PowerUpSlot.Option));
                Assert.AreEqual(24, gauge.GetMaxLevel(PowerUpSlot.Shield));
            });
        }

        [Test]
        public void EffectScaling_IsLinearThroughSoftCapThenDiminishes()
        {
            AssertAll(() =>
            {
                Assert.AreEqual(4, PowerLevelScaling.GetEffectiveLevel(4, 4));
                Assert.AreEqual(5, PowerLevelScaling.GetEffectiveLevel(5, 4));
                Assert.AreEqual(5, PowerLevelScaling.GetEffectiveLevel(6, 4));
                Assert.AreEqual(6, PowerLevelScaling.GetEffectiveLevel(8, 4));
                Assert.AreEqual(7, PowerLevelScaling.GetEffectiveLevel(13, 4));
            });
        }

        [Test]
        public void DirectLevelGrant_PreservesUnrelatedPartialProgress()
        {
            var gauge = new PowerUpGauge(
                new[] { 3, 3, 3, 3 },
                new PowerUpCostCurve(2, 1, 0));
            SelectAndActivate(gauge, PowerUpSlot.MainShot);
            for (int i = 0; i <= (int)PowerUpSlot.Missile; i++)
                gauge.Collect();
            gauge.ActivateDetailed();

            gauge.GrantLevels(PowerUpSlot.MainShot, 1);

            AssertAll(() =>
            {
                Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.MainShot));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.MainShot));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.Missile));
            });
        }

        static void SelectAndActivate(
            PowerUpGauge gauge,
            PowerUpSlot slot)
        {
            for (int i = 0; i <= (int)slot; i++)
                gauge.Collect();
            gauge.ActivateDetailed();
        }

        static void AssertAll(Action assert) => assert();
    }
}
