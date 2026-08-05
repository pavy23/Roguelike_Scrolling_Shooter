using System;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;

namespace Shmup.Core.Tests
{
    public sealed class ShipMetaTests
    {
        [Test]
        public void ShipDefinitionDefensivelyCopiesStartingLevels()
        {
            var source = new[] { 1, 2, 3, 1 };
            var ship = new ShipDefinition(
                "swift",
                "Swift",
                5,
                4,
                source,
                250);

            source[0] = 99;
            int[] exported = ship.ExportStartingPowerUpLevels();
            exported[1] = 99;

            CollectionAssert.AreEqual(
                new[] { 1, 2, 3, 1, 0, 0, 0, 0 },
                ship.StartingPowerUpLevels);
        }

        [Test]
        public void ScoreCurrencyUnlockSelectionAndSaveRoundTripAreStable()
        {
            ShipDefinition starter = ShipDefinition.CreateDefault();
            var ship = new ShipDefinition(
                "b_ship",
                "B Ship",
                1,
                1,
                new[] { 0, 1, 0, 0 },
                300);
            MetaState state = MetaState.CreateDefault(starter);

            // 점수를 그대로 크레딧으로 주지 않는다(2026-08-05: 2.5%). 환산율은
            // 사람이 화면을 보고 정하는 값이라, 여기서 숫자를 베끼면 비율을 손볼
            // 때마다 관계없는 테스트가 깨진다 — **비율에서 필요한 점수를 거꾸로
            // 구한다.**
            long needed = 500L * 1000L / MetaState.ScoreToCurrencyPermille;
            state.CreditScore(needed);
            long credited = needed * MetaState.ScoreToCurrencyPermille / 1000L;
            Assert.IsTrue(state.TryUnlock(ship));
            state.SelectShip(ship.Id);

            Assert.AreEqual(credited - 300L, state.TotalCurrency);
            CollectionAssert.AreEqual(
                new[] { "b_ship", "default" },
                state.UnlockedShipIds);
            Assert.AreEqual("b_ship", state.SelectedShipId);

            MetaStateData payload = state.ExportData();
            var serializer = new DataContractJsonSerializer(typeof(MetaStateData));
            MetaStateData restoredPayload;
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, payload);
                stream.Position = 0;
                restoredPayload = (MetaStateData)serializer.ReadObject(stream);
            }

            MetaState restored = MetaState.FromData(restoredPayload);
            Assert.AreEqual(state.TotalCurrency, restored.TotalCurrency);
            CollectionAssert.AreEqual(
                state.UnlockedShipIds,
                restored.UnlockedShipIds);
            Assert.AreEqual(state.SelectedShipId, restored.SelectedShipId);
        }

        [Test]
        public void FailedUnlockDoesNotSpendCurrency()
        {
            MetaState state = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            var expensive = new ShipDefinition(
                "expensive",
                "Expensive",
                1,
                1,
                new[] { 0, 0, 0, 0 },
                100);
            long shortfall = 99L * 1000L / MetaState.ScoreToCurrencyPermille;
            state.CreditScore(shortfall);
            long banked = state.TotalCurrency;

            Assert.IsFalse(state.TryUnlock(expensive));
            Assert.AreEqual(
                banked,
                state.TotalCurrency,
                "해금에 실패했는데 크레딧이 줄었다.");
            Assert.IsFalse(state.IsUnlocked(expensive.Id));
        }

        [Test]
        public void LegacyMetaState_MigratesAndReceivesChecksum()
        {
            var legacy = new MetaStateData
            {
                totalCurrency = 25,
                unlockedShipIds = new[] { "default" },
                selectedShipId = "default"
            };

            MetaStateData migrated =
                SaveDataIntegrity.MigrateAndValidate(legacy);
            MetaState state = MetaState.FromData(migrated);

            Assert.AreEqual(
                MetaStateData.CurrentSchemaVersion,
                migrated.schemaVersion);
            Assert.IsTrue(
                SaveDataIntegrity.HasValidChecksum(migrated));
            Assert.AreEqual(25, state.TotalCurrency);
            Assert.AreEqual(0, legacy.schemaVersion);
        }

        [Test]
        public void CurrentMetaChecksumMismatch_IsClearlyRejected()
        {
            MetaStateData corrupted =
                MetaState.CreateDefault(ShipDefinition.CreateDefault())
                    .ExportData();
            corrupted.totalCurrency++;

            ArgumentException error =
                Assert.Throws<ArgumentException>(
                    () => MetaState.FromData(corrupted));

            StringAssert.Contains("checksum", error.Message);
        }

        [Test]
        public void LockedShipCannotBeSelected()
        {
            MetaState state = MetaState.CreateDefault(
                ShipDefinition.CreateDefault());
            string selectedBefore = state.SelectedShipId;

            Assert.IsFalse(state.SelectShip("locked"));
            Assert.IsFalse(state.SelectShip(null));
            Assert.AreEqual(selectedBefore, state.SelectedShipId);
        }
    }
}
