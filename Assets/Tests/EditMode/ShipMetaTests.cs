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
                new[] { 1, 2, 3, 1 },
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

            state.CreditScore(500);
            Assert.IsTrue(state.TryUnlock(ship));
            state.SelectShip(ship.Id);

            Assert.AreEqual(200L, state.TotalCurrency);
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
            state.CreditScore(99);

            Assert.IsFalse(state.TryUnlock(expensive));
            Assert.AreEqual(99L, state.TotalCurrency);
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

            Assert.Throws<System.InvalidOperationException>(
                () => state.SelectShip("locked"));
        }
    }
}
