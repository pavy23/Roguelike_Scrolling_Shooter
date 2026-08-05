using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

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
        readonly PowerUpSlot[] _gaugeSlots;
        readonly ReadOnlyCollection<PowerUpSlot> _readOnlyGaugeSlots;

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
                null,
                null,
                null,
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
            : this(
                id,
                displayName,
                moveSpeedMultiplierNumerator,
                moveSpeedMultiplierDenominator,
                startingPowerUpLevels,
                unlockCost,
                weaponType,
                maxHp,
                null,
                null,
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
            int? startingShieldStock,
            PrimaryWeaponFamily? gaugeWeaponFamily,
            IReadOnlyList<PowerUpSlot> gaugeSlots,
            MissileFamily? startingMissileFamily = null,
            OptionFormation? startingOptionFormation = null)
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
            if (startingPowerUpLevels.Length != 4
                && startingPowerUpLevels.Length
                    != PowerUpGauge.SlotCount)
                throw new ArgumentException(
                    $"Starting levels must have exactly 4 or "
                    + $"{PowerUpGauge.SlotCount} entries.",
                    nameof(startingPowerUpLevels));
            if (unlockCost < 0)
                throw new ArgumentOutOfRangeException(nameof(unlockCost));
            if (!Enum.IsDefined(typeof(WeaponType), weaponType))
                throw new ArgumentOutOfRangeException(nameof(weaponType));
            if (startingShieldStock.HasValue
                && startingShieldStock.Value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(startingShieldStock));
            if (startingMissileFamily.HasValue
                && !Enum.IsDefined(
                    typeof(MissileFamily),
                    startingMissileFamily.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(startingMissileFamily));
            if (startingOptionFormation.HasValue
                && !Enum.IsDefined(
                    typeof(OptionFormation),
                    startingOptionFormation.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(startingOptionFormation));
            if (gaugeWeaponFamily.HasValue
                && (gaugeWeaponFamily.Value
                        == PrimaryWeaponFamily.Vulcan
                    || !Enum.IsDefined(
                        typeof(PrimaryWeaponFamily),
                        gaugeWeaponFamily.Value)))
                throw new ArgumentOutOfRangeException(
                    nameof(gaugeWeaponFamily));
            bool hasGaugeSlots = gaugeSlots != null
                && gaugeSlots.Count != 0;
            if (hasGaugeSlots != gaugeWeaponFamily.HasValue)
                throw new ArgumentException(
                    "A custom gauge requires both gaugeWeaponFamily "
                    + "and gaugeSlots.");

            _startingPowerUpLevels =
                new int[PowerUpGauge.SlotCount];
            Array.Copy(
                startingPowerUpLevels,
                _startingPowerUpLevels,
                startingPowerUpLevels.Length);
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
            StartingShieldStock = startingShieldStock;
            GaugeWeaponFamily = gaugeWeaponFamily;
            StartingMissileFamily = startingMissileFamily;
            StartingOptionFormation = startingOptionFormation;
            _gaugeSlots = CopyGaugeSlots(
                gaugeSlots,
                gaugeWeaponFamily);
            _readOnlyGaugeSlots =
                Array.AsReadOnly(_gaugeSlots);
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
        /// Ship-specific starting shield stock. Zero is a valid glass-cannon
        /// start; null preserves BattleSimConfig.StartingShieldStock.
        /// </summary>
        public int? StartingShieldStock { get; }
        /// <summary>Compatibility alias for pre-REQ-040 callers.</summary>
        public int? MaxHp => StartingShieldStock;
        /// <summary>
        /// Family selected by the custom six-slot ship weapon entry.
        /// Null means the backward-compatible seven-slot gauge.
        /// </summary>
        public PrimaryWeaponFamily? GaugeWeaponFamily { get; }
        public IReadOnlyList<PowerUpSlot> GaugeSlots =>
            _readOnlyGaugeSlots;
        public bool HasCustomPowerUpGauge =>
            GaugeWeaponFamily.HasValue;
        /// <summary>
        /// Ship-owned starting family. Null preserves weapons.json's legacy
        /// global default for schema v1/v2 ships.
        /// </summary>
        public MissileFamily? StartingMissileFamily { get; }
        /// <summary>
        /// Ship-owned starting option formation. Null preserves weapons.json's
        /// global default for legacy ships and omitted schema-v4 fields.
        /// </summary>
        public OptionFormation? StartingOptionFormation { get; }

        public int[] ExportStartingPowerUpLevels()
        {
            return (int[])_startingPowerUpLevels.Clone();
        }

        static PowerUpSlot[] CopyGaugeSlots(
            IReadOnlyList<PowerUpSlot> source,
            PrimaryWeaponFamily? family)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<PowerUpSlot>();
            bool migrateLegacyFiveSlot = source.Count
                == PowerUpGauge.ShipGaugeSlotCount - 1;
            if (!migrateLegacyFiveSlot
                && source.Count != PowerUpGauge.ShipGaugeSlotCount)
                throw new ArgumentException(
                    $"A ship gauge requires exactly "
                    + $"{PowerUpGauge.ShipGaugeSlotCount} slots.",
                    nameof(source));

            var copy = new PowerUpSlot[
                PowerUpGauge.ShipGaugeSlotCount];
            int write = 0;
            for (int i = 0; i < source.Count; i++)
            {
                PowerUpSlot slot = source[i];
                copy[write++] = slot;
                if (migrateLegacyFiveSlot
                    && slot == PowerUpSlot.Speed)
                {
                    copy[write++] = PowerUpSlot.MainShot;
                }
            }
            if (write != copy.Length)
                throw new ArgumentException(
                    "A legacy five-slot ship gauge must contain Speed.",
                    nameof(source));

            var seen = new bool[PowerUpGauge.SlotCount];
            int weaponModes = 0;
            PowerUpSlot expectedWeapon =
                GaugeSlotForFamily(family.Value);
            for (int i = 0; i < copy.Length; i++)
            {
                PowerUpSlot slot = copy[i];
                int index = (int)slot;
                if (index < 0
                    || index >= PowerUpGauge.SlotCount)
                    throw new ArgumentOutOfRangeException(
                        nameof(source));
                if (seen[index])
                    throw new ArgumentException(
                        $"Duplicate ship gauge slot '{slot}'.",
                        nameof(source));
                seen[index] = true;
                if (PowerUpSlotDefinition.IsWeaponModeSlot(slot))
                    weaponModes++;
            }
            if (!seen[(int)PowerUpSlot.Speed]
                || !seen[(int)PowerUpSlot.MainShot]
                || !seen[(int)PowerUpSlot.Missile]
                || !seen[(int)PowerUpSlot.Option]
                || !seen[(int)PowerUpSlot.Shield]
                || weaponModes != 1
                || !seen[(int)expectedWeapon])
                throw new ArgumentException(
                    "A ship gauge requires Speed, MainShot, Missile, its "
                    + "designated weapon, Option, and Shield exactly once.",
                    nameof(source));
            return copy;
        }

        public static PowerUpSlot GaugeSlotForFamily(
            PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Double:
                    return PowerUpSlot.Double;
                case PrimaryWeaponFamily.Laser:
                    return PowerUpSlot.Laser;
                case PrimaryWeaponFamily.Spread:
                    return PowerUpSlot.Triple;
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
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
        public const int CurrentSchemaVersion = 3;

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
        public int lastColossalBoss;

        [DataMember(Order = 5)]
        public string checksum;

        [DataMember(Order = 6)]
        public int continueStock;
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
            : this(
                totalCurrency,
                unlockedShipIds,
                selectedShipId,
                ColossalBossKind.None)
        {
        }

        public MetaState(
            long totalCurrency,
            IReadOnlyList<string> unlockedShipIds,
            string selectedShipId,
            ColossalBossKind lastColossalBoss)
            : this(
                totalCurrency,
                unlockedShipIds,
                selectedShipId,
                lastColossalBoss,
                0)
        {
        }

        public MetaState(
            long totalCurrency,
            IReadOnlyList<string> unlockedShipIds,
            string selectedShipId,
            ColossalBossKind lastColossalBoss,
            int continueStock)
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
            if (!Enum.IsDefined(
                    typeof(ColossalBossKind),
                    lastColossalBoss))
                throw new ArgumentOutOfRangeException(
                    nameof(lastColossalBoss));
            if (continueStock < 0
                || continueStock
                    > ContinueEconomyConfig.DefaultMaximumStock)
                throw new ArgumentOutOfRangeException(
                    nameof(continueStock));

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
            LastColossalBoss = lastColossalBoss;
            ContinueStock = continueStock;
            _readOnlyUnlockedShipIds = _unlockedShipIds.AsReadOnly();
        }

        public long TotalCurrency { get; private set; }
        public IReadOnlyList<string> UnlockedShipIds => _readOnlyUnlockedShipIds;
        public string SelectedShipId { get; private set; }
        public ColossalBossKind LastColossalBoss { get; private set; }
        public int ContinueStock { get; private set; }

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
                data.selectedShipId,
                (ColossalBossKind)data.lastColossalBoss,
                data.continueStock);
        }

        public MetaStateData ExportData()
        {
            var data = new MetaStateData
            {
                schemaVersion = MetaStateData.CurrentSchemaVersion,
                totalCurrency = TotalCurrency,
                unlockedShipIds = _unlockedShipIds.ToArray(),
                selectedShipId = SelectedShipId,
                lastColossalBoss = (int)LastColossalBoss,
                continueStock = ContinueStock
            };
            SaveDataIntegrity.Seal(data);
            return data;
        }

        /// <summary>
        /// 완주한 런의 점수를 크레딧으로 바꿔 더한다.
        ///
        /// **점수 전액이 아니라 <see cref="ScoreToCurrencyPermille"/>만큼이다.**
        /// 1:1이던 시절에는 퍼펙트 클리어 한 판(약 440만 점)으로 컨티뉴 8개와 기체
        /// 둘(합 19만 4천)을 스물두 번 살 수 있었다 — 살 것이 남지 않으면 경제가
        /// 아니다 (사람 지시 2026-08-05: "보상은 2.5%로 고치자").
        /// </summary>
        public void CreditScore(long score)
        {
            if (score < 0)
                throw new ArgumentOutOfRangeException(nameof(score));
            long credited = score * ScoreToCurrencyPermille / 1000;
            TotalCurrency = checked(TotalCurrency + credited);
        }

        /// <summary>
        /// 점수의 몇 천분율이 크레딧이 되는가. 25 = 2.5%.
        ///
        /// 이 값에서 나오는 감각: 중간 런(100만 점)이 25,000크레딧이라 컨티뉴를
        /// 가득(44,000) 채우려면 두 판, 기체 해금(50,000)은 클리어 한 판이다.
        /// 초반에는 컨티뉴가 진짜 선택이 되고, 클리어할 실력이 되면 한 판에 하나씩
        /// 열린다.
        /// </summary>
        public const long ScoreToCurrencyPermille = 25;

        public long GetContinuePurchasePrice(
            ContinueEconomyConfig config = null)
        {
            ContinueEconomyConfig resolved =
                config ?? ContinueEconomyConfig.CreateDefault();
            return resolved.GetPurchasePrice(ContinueStock);
        }

        public ContinuePurchaseResult TryPurchaseContinue(
            ContinueEconomyConfig config = null)
        {
            ContinueEconomyConfig resolved =
                config ?? ContinueEconomyConfig.CreateDefault();
            if (ContinueStock >= resolved.MaximumStock)
            {
                return new ContinuePurchaseResult(
                    false,
                    ContinuePurchaseRejectionReason.StockFull,
                    0);
            }

            long price = resolved.GetPurchasePrice(ContinueStock);
            if (TotalCurrency < price)
            {
                return new ContinuePurchaseResult(
                    false,
                    ContinuePurchaseRejectionReason.InsufficientCurrency,
                    price);
            }

            TotalCurrency -= price;
            ContinueStock++;
            return new ContinuePurchaseResult(
                true,
                ContinuePurchaseRejectionReason.None,
                price);
        }

        internal void ConsumeContinues(int amount)
        {
            if (amount < 0 || amount > ContinueStock)
                throw new ArgumentOutOfRangeException(nameof(amount));
            ContinueStock -= amount;
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

        /// <summary>
        /// Selects an unlocked ship. Invalid presentation input is ignored
        /// without changing the current selection.
        /// </summary>
        public bool SelectShip(string shipId)
        {
            if (string.IsNullOrEmpty(shipId)
                || !IsUnlocked(shipId))
                return false;
            SelectedShipId = shipId;
            return true;
        }

        public void RecordColossalBossEncounter(
            ColossalBossKind boss)
        {
            if (boss != ColossalBossKind.Leviathan
                && boss != ColossalBossKind.Broodmother)
                throw new ArgumentOutOfRangeException(nameof(boss));
            LastColossalBoss = boss;
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
