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
    }
}
