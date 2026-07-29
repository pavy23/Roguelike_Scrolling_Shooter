using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Shmup.Core
{
    /// <summary>Primary weapon family selected by ships.json.</summary>
    public enum WeaponType
    {
        Vulcan = 0,
        Laser = 1,
        Spread = 2
    }

    /// <summary>
    /// Immutable ship tuning sourced from GameData/ships.json.
    /// Movement is an exact multiplier and starting levels use PowerUpSlot order.
    /// </summary>
    public sealed class ShipDefinition
    {
        readonly int[] _startingPowerUpLevels;
        readonly ReadOnlyCollection<int> _readOnlyStartingPowerUpLevels;

        public ShipDefinition(
            string id,
            string displayName,
            int moveSpeedMultiplierNumerator,
            int moveSpeedMultiplierDenominator,
            int[] startingPowerUpLevels,
            long unlockCost)
            : this(
                id,
                displayName,
                moveSpeedMultiplierNumerator,
                moveSpeedMultiplierDenominator,
                startingPowerUpLevels,
                unlockCost,
                WeaponType.Vulcan,
                null)
        {
        }

        public ShipDefinition(
            string id,
            string displayName,
            int moveSpeedMultiplierNumerator,
            int moveSpeedMultiplierDenominator,
            int[] startingPowerUpLevels,
            long unlockCost,
            WeaponType weaponType,
            int? maxHp)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Ship id cannot be null or empty.", nameof(id));
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException(
                    "Ship display name cannot be null or empty.",
                    nameof(displayName));
            if (moveSpeedMultiplierNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedMultiplierNumerator));
            if (moveSpeedMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedMultiplierDenominator));
            if (startingPowerUpLevels == null)
                throw new ArgumentNullException(nameof(startingPowerUpLevels));
            if (startingPowerUpLevels.Length != PowerUpGauge.SlotCount)
                throw new ArgumentException(
                    $"Starting levels must have exactly {PowerUpGauge.SlotCount} entries.",
                    nameof(startingPowerUpLevels));
            if (unlockCost < 0)
                throw new ArgumentOutOfRangeException(nameof(unlockCost));
            if (!Enum.IsDefined(typeof(WeaponType), weaponType))
                throw new ArgumentOutOfRangeException(nameof(weaponType));
            if (maxHp.HasValue && maxHp.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHp));

            _startingPowerUpLevels = (int[])startingPowerUpLevels.Clone();
            for (int i = 0; i < _startingPowerUpLevels.Length; i++)
            {
                if (_startingPowerUpLevels[i] < 0)
                    throw new ArgumentException(
                        "Starting power-up levels cannot be negative.",
                        nameof(startingPowerUpLevels));
            }

            Id = id;
            DisplayName = displayName;
            MoveSpeedMultiplierNumerator = moveSpeedMultiplierNumerator;
            MoveSpeedMultiplierDenominator = moveSpeedMultiplierDenominator;
            UnlockCost = unlockCost;
            WeaponType = weaponType;
            MaxHp = maxHp;
            _readOnlyStartingPowerUpLevels =
                Array.AsReadOnly(_startingPowerUpLevels);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MoveSpeedMultiplierNumerator { get; }
        public int MoveSpeedMultiplierDenominator { get; }
        public IReadOnlyList<int> StartingPowerUpLevels =>
            _readOnlyStartingPowerUpLevels;
        public long UnlockCost { get; }
        public WeaponType WeaponType { get; }
        /// <summary>
        /// Ship-specific starting hull. Null preserves BattleSimConfig.PlayerMaxHp.
        /// </summary>
        public int? MaxHp { get; }

        public int[] ExportStartingPowerUpLevels()
        {
            return (int[])_startingPowerUpLevels.Clone();
        }

        /// <summary>Neutral fallback used when ships.json is absent.</summary>
        public static ShipDefinition CreateDefault()
        {
            return new ShipDefinition(
                "default",
                "Default Ship",
                1,
                1,
                new int[PowerUpGauge.SlotCount],
                0);
        }
    }

    /// <summary>
    /// Serializer-facing save payload. Presentation owns persistence and may
    /// serialize these camelCase fields with its chosen storage mechanism.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class MetaStateData
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>Zero denotes the legacy schema that had no version field.</summary>
        [DataMember(Order = 0)]
        public int schemaVersion;

        [DataMember(Order = 1)]
        public long totalCurrency;

        [DataMember(Order = 2)]
        public string[] unlockedShipIds;

        [DataMember(Order = 3)]
        public string selectedShipId;

        [DataMember(Order = 4)]
        public string checksum;
    }

    /// <summary>
    /// Deterministic ship-unlock progression. Ship ids are retained in ordinal
    /// order so exported save data never depends on set enumeration order.
    /// </summary>
    public sealed class MetaState
    {
        readonly List<string> _unlockedShipIds;
        readonly ReadOnlyCollection<string> _readOnlyUnlockedShipIds;

        public MetaState(
            long totalCurrency,
            IReadOnlyList<string> unlockedShipIds,
            string selectedShipId)
        {
            if (totalCurrency < 0)
                throw new ArgumentOutOfRangeException(nameof(totalCurrency));
            if (unlockedShipIds == null)
                throw new ArgumentNullException(nameof(unlockedShipIds));
            if (unlockedShipIds.Count == 0)
                throw new ArgumentException(
                    "At least one ship must be unlocked.",
                    nameof(unlockedShipIds));
            if (string.IsNullOrEmpty(selectedShipId))
                throw new ArgumentException(
                    "Selected ship id cannot be null or empty.",
                    nameof(selectedShipId));

            _unlockedShipIds = new List<string>(unlockedShipIds.Count);
            for (int i = 0; i < unlockedShipIds.Count; i++)
            {
                string id = unlockedShipIds[i];
                if (string.IsNullOrEmpty(id))
                    throw new ArgumentException(
                        "Unlocked ship ids cannot be null or empty.",
                        nameof(unlockedShipIds));
                if (ContainsOrdinal(_unlockedShipIds, id))
                    throw new ArgumentException(
                        $"Duplicate unlocked ship id '{id}'.",
                        nameof(unlockedShipIds));
                _unlockedShipIds.Add(id);
            }
            _unlockedShipIds.Sort(StringComparer.Ordinal);

            if (!ContainsOrdinal(_unlockedShipIds, selectedShipId))
                throw new ArgumentException(
                    "The selected ship must be unlocked.",
                    nameof(selectedShipId));

            TotalCurrency = totalCurrency;
            SelectedShipId = selectedShipId;
            _readOnlyUnlockedShipIds = _unlockedShipIds.AsReadOnly();
        }

        public long TotalCurrency { get; private set; }
        public IReadOnlyList<string> UnlockedShipIds => _readOnlyUnlockedShipIds;
        public string SelectedShipId { get; private set; }

        public static MetaState CreateDefault(ShipDefinition startingShip)
        {
            if (startingShip == null)
                throw new ArgumentNullException(nameof(startingShip));
            return new MetaState(
                0,
                new[] { startingShip.Id },
                startingShip.Id);
        }

        public static MetaState FromData(MetaStateData data)
        {
            data = SaveDataIntegrity.MigrateAndValidate(data);
            return new MetaState(
                data.totalCurrency,
                data.unlockedShipIds,
                data.selectedShipId);
        }

        public MetaStateData ExportData()
        {
            var data = new MetaStateData
            {
                schemaVersion = MetaStateData.CurrentSchemaVersion,
                totalCurrency = TotalCurrency,
                unlockedShipIds = _unlockedShipIds.ToArray(),
                selectedShipId = SelectedShipId
            };
            SaveDataIntegrity.Seal(data);
            return data;
        }

        /// <summary>Adds a completed run's non-negative score to currency.</summary>
        public void CreditScore(long score)
        {
            if (score < 0)
                throw new ArgumentOutOfRangeException(nameof(score));
            TotalCurrency = checked(TotalCurrency + score);
        }

        public bool IsUnlocked(string shipId)
        {
            if (shipId == null) throw new ArgumentNullException(nameof(shipId));
            return ContainsOrdinal(_unlockedShipIds, shipId);
        }

        /// <summary>
        /// Spends currency and unlocks once. Returns false for an existing unlock
        /// or insufficient currency, without changing state.
        /// </summary>
        public bool TryUnlock(ShipDefinition ship)
        {
            if (ship == null) throw new ArgumentNullException(nameof(ship));
            if (IsUnlocked(ship.Id) || TotalCurrency < ship.UnlockCost)
                return false;

            TotalCurrency -= ship.UnlockCost;
            int insertIndex = _unlockedShipIds.BinarySearch(
                ship.Id,
                StringComparer.Ordinal);
            _unlockedShipIds.Insert(~insertIndex, ship.Id);
            return true;
        }

        public void SelectShip(string shipId)
        {
            if (shipId == null) throw new ArgumentNullException(nameof(shipId));
            if (!IsUnlocked(shipId))
                throw new InvalidOperationException(
                    $"Ship '{shipId}' is not unlocked.");
            SelectedShipId = shipId;
        }

        static bool ContainsOrdinal(List<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
