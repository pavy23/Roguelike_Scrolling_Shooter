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
            Assert.AreEqual(
                PowerUpGauge.MaximumOptionCount,
                gauge.GetMaxLevel(PowerUpSlot.Option));
            foreach (PowerUpSlot slot in Enum.GetValues(typeof(PowerUpSlot)))
                Assert.AreEqual(0, gauge.GetLevel(slot));
        }

        [Test]
        public void Collect_AdvancesCursorSequentially()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            Assert.AreEqual(0, gauge.Cursor);
            Assert.AreEqual(PowerUpSlot.Speed, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Missile, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Double, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Laser, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Triple, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Option, gauge.SelectedSlot);
            gauge.Collect();
            Assert.AreEqual(PowerUpSlot.Shield, gauge.SelectedSlot);
        }

        [Test]
        public void Collect_WrapsAroundAfterLastSlot()
        {
            var gauge = PowerUpGauge.CreateDefault();
            for (int i = 0; i < gauge.GaugeSlotCount + 1; i++)
                gauge.Collect();
            Assert.AreEqual(0, gauge.Cursor);
            Assert.AreEqual(PowerUpSlot.Speed, gauge.SelectedSlot);
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
            var gauge = new PowerUpGauge(
                new[] { 1, 1, 1, 1, 1, 1, 1, 1 });
            gauge.Collect();
            Assert.IsTrue(gauge.Activate());

            gauge.Collect();
            Assert.IsFalse(gauge.Activate(), "activating a maxed slot must fail");
            Assert.AreEqual(0, gauge.Cursor,
                "cursor must stay so the player can keep collecting to move past a maxed slot");
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Speed));
        }

        [Test]
        public void CanActivate_FalseWhenSlotMaxed()
        {
            var gauge = new PowerUpGauge(
                new[] { 1, 2, 2, 2, 1, 1, 1, 1 });
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
            snapshot[(int)PowerUpSlot.Speed] = 999;
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Speed));
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

            Assert.AreEqual(1, gauge.GetRequiredCapsules(PowerUpSlot.Speed));
            SelectAndActivate(gauge, PowerUpSlot.Speed);
            Assert.AreEqual(3, gauge.GetRequiredCapsules(PowerUpSlot.Speed));

            SelectAndActivate(gauge, PowerUpSlot.Speed);
            AssertAll(() =>
            {
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.Speed));
                Assert.AreEqual(2, gauge.GetRemainingCapsules(PowerUpSlot.Speed));
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
                Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Speed));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.Speed));
                Assert.AreEqual(1, gauge.GetRemainingCapsules(PowerUpSlot.Speed));
                Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);
            });

            gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Speed));
            Assert.AreEqual(0, gauge.GetProgress(PowerUpSlot.Speed));
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
            SelectAndActivate(gauge, PowerUpSlot.Speed);
            SelectAndActivate(gauge, PowerUpSlot.Missile);

            gauge.GrantLevels(PowerUpSlot.Speed, 1);

            AssertAll(() =>
            {
                Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Speed));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.Speed));
                Assert.AreEqual(1, gauge.GetProgress(PowerUpSlot.Missile));
            });
        }

        [Test]
        public void WeaponModes_AreMutuallyExclusiveAndObservable()
        {
            var gauge = PowerUpGauge.CreateDefault();
            SelectAndActivate(gauge, PowerUpSlot.Double);
            Assert.AreEqual(
                PowerUpWeaponMode.Double,
                gauge.ActiveWeaponMode);

            SelectAndActivate(gauge, PowerUpSlot.Laser);

            AssertAll(() =>
            {
                Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Double));
                Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Laser));
                Assert.AreEqual(
                    PowerUpWeaponMode.Laser,
                    gauge.ActiveWeaponMode);
                Assert.IsTrue(
                    gauge.GetGaugeSlotView(3)
                        .IsActiveWeaponMode);
                Assert.AreEqual(
                    "powerUp.laser",
                    gauge.GetGaugeSlotView(3).NameKey);
            });
        }

        [Test]
        public void ContractGaugeBanRejectsWithoutConsumingSelection()
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            int selectedCursor = gauge.Cursor;
            gauge.SetContractActivationBans(true, false, false);

            PowerUpActivationResult result = gauge.ActivateDetailed();

            Assert.AreEqual(
                PowerUpActivationResult.ContractGaugeActivationBanned,
                result);
            Assert.AreEqual(selectedCursor, gauge.Cursor);
            Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Speed));
            Assert.IsFalse(gauge.CanActivate);
            gauge.Collect();
            Assert.AreNotEqual(selectedCursor, gauge.Cursor);
        }

        [Test]
        public void ContractSlotBansOnlyRejectTheirOwnSlots()
        {
            PowerUpGauge optionGauge = PowerUpGauge.CreateDefault();
            Select(optionGauge, PowerUpSlot.Option);
            optionGauge.SetContractActivationBans(false, true, false);
            Assert.AreEqual(
                PowerUpActivationResult.ContractOptionActivationBanned,
                optionGauge.ActivateDetailed());
            Assert.AreEqual(
                PowerUpSlot.Option,
                optionGauge.SelectedSlot);

            PowerUpGauge shieldGauge = PowerUpGauge.CreateDefault();
            Select(shieldGauge, PowerUpSlot.Shield);
            shieldGauge.SetContractActivationBans(false, false, true);
            Assert.AreEqual(
                PowerUpActivationResult.ContractShieldActivationBanned,
                shieldGauge.ActivateDetailed());
            Assert.AreEqual(
                PowerUpSlot.Shield,
                shieldGauge.SelectedSlot);

            PowerUpGauge allowedGauge = PowerUpGauge.CreateDefault();
            allowedGauge.Collect();
            allowedGauge.SetContractActivationBans(false, true, true);
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                allowedGauge.ActivateDetailed());
        }

        static void SelectAndActivate(
            PowerUpGauge gauge,
            PowerUpSlot slot)
        {
            Select(gauge, slot);
            gauge.ActivateDetailed();
        }

        static void Select(
            PowerUpGauge gauge,
            PowerUpSlot slot)
        {
            int gaugeIndex = -1;
            for (int i = 0; i < gauge.GaugeSlots.Count; i++)
                if (gauge.GaugeSlots[i].Slot == slot)
                {
                    gaugeIndex = i;
                    break;
                }
            if (gaugeIndex < 0)
                throw new ArgumentException(
                    "The hidden MainShot axis is not selectable.",
                    nameof(slot));
            for (int i = 0; i <= gaugeIndex; i++)
                gauge.Collect();
        }

        static void AssertAll(Action assert) => assert();
    }
}
