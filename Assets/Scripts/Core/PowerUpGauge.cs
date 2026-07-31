using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core
{
    /// <summary>
    /// Integer-only capsule cost curve. The cost of advancing from level L to
    /// L+1 is BaseCost + LinearGrowth*L + QuadraticGrowth*L*L.
    /// </summary>
    public sealed class PowerUpCostCurve
    {
        public PowerUpCostCurve(
            int baseCost,
            int linearGrowth,
            int quadraticGrowth)
        {
            if (baseCost < 1)
                throw new ArgumentOutOfRangeException(nameof(baseCost));
            if (linearGrowth < 0)
                throw new ArgumentOutOfRangeException(nameof(linearGrowth));
            if (quadraticGrowth < 0)
                throw new ArgumentOutOfRangeException(nameof(quadraticGrowth));
            BaseCost = baseCost;
            LinearGrowth = linearGrowth;
            QuadraticGrowth = quadraticGrowth;
        }

        public int BaseCost { get; }
        public int LinearGrowth { get; }
        public int QuadraticGrowth { get; }

        public int GetCostForCurrentLevel(int currentLevel)
        {
            if (currentLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            long level = currentLevel;
            long cost;
            try
            {
                cost = checked(
                    BaseCost
                    + (long)LinearGrowth * level
                    + (long)QuadraticGrowth * level * level);
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException(
                    "The power-up cost curve exceeds Int64 range.");
            }
            if (cost > int.MaxValue)
                throw new InvalidOperationException(
                    "The power-up cost curve exceeds Int32 range.");
            return (int)cost;
        }

        public static PowerUpCostCurve FlatOne { get; } =
            new PowerUpCostCurve(1, 0, 0);

        public static PowerUpCostCurve CreateProvisional() =>
            new PowerUpCostCurve(1, 1, 1);
    }

    public enum PowerUpActivationResult
    {
        NoSelection = 0,
        SlotMaxed = 1,
        ProgressAdded = 2,
        LevelIncreased = 3
    }

    /// <summary>
    /// Stable state-array ids. MainShot is the shared power axis used by every
    /// primary family. It is hidden on the legacy gauge and selectable on the
    /// ship-owned six-slot gauge. Display order is data-owned.
    /// Existing ids 0..3 deliberately remain unchanged.
    /// </summary>
    public enum PowerUpSlot
    {
        MainShot = 0,
        Missile = 1,
        Option = 2,
        Shield = 3,
        Speed = 4,
        Double = 5,
        Laser = 6,
        Triple = 7
    }

    public enum PowerUpWeaponMode
    {
        None = 0,
        Double = 1,
        Laser = 2,
        Triple = 3
    }

    /// <summary>
    /// Immutable GameData-owned rule for one visible gauge slot. Speed bonus is
    /// an exact simulation-subunit-per-tick fraction and must be zero for every
    /// non-Speed slot.
    /// </summary>
    public sealed class PowerUpSlotDefinition
    {
        public PowerUpSlotDefinition(
            PowerUpSlot slot,
            string nameKey,
            int maxLevel,
            PowerUpCostCurve costCurve,
            int speedBonusNumerator = 0,
            int speedBonusDenominator = 1,
            bool activatesImmediately = false)
        {
            if (!Enum.IsDefined(typeof(PowerUpSlot), slot))
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (string.IsNullOrWhiteSpace(nameKey))
                throw new ArgumentException(
                    "Power-up slot nameKey cannot be null or blank.",
                    nameof(nameKey));
            if (maxLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLevel));
            if (costCurve == null)
                throw new ArgumentNullException(nameof(costCurve));
            if (speedBonusNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(speedBonusNumerator));
            if (speedBonusDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(speedBonusDenominator));
            if (slot != PowerUpSlot.Speed
                && speedBonusNumerator != 0)
                throw new ArgumentException(
                    "Only the Speed slot can define a movement bonus.",
                    nameof(speedBonusNumerator));
            if (IsWeaponModeSlot(slot) && maxLevel != 1)
                throw new ArgumentException(
                    "Mutually-exclusive weapon mode slots must have maxLevel 1.",
                    nameof(maxLevel));
            if (activatesImmediately && !IsWeaponModeSlot(slot))
                throw new ArgumentException(
                    "Only a weapon mode slot can activate immediately.",
                    nameof(activatesImmediately));
            costCurve.GetCostForCurrentLevel(maxLevel - 1);

            Slot = slot;
            NameKey = nameKey;
            MaxLevel = maxLevel;
            CostCurve = costCurve;
            SpeedBonusNumerator = speedBonusNumerator;
            SpeedBonusDenominator = speedBonusDenominator;
            ActivatesImmediately = activatesImmediately;
        }

        public PowerUpSlot Slot { get; }
        public string NameKey { get; }
        public int MaxLevel { get; }
        public PowerUpCostCurve CostCurve { get; }
        public int SpeedBonusNumerator { get; }
        public int SpeedBonusDenominator { get; }
        /// <summary>
        /// A ship-owned weapon slot switches family on its first activation.
        /// Legacy seven-slot gauges retain partial-investment behavior.
        /// </summary>
        public bool ActivatesImmediately { get; }
        public bool IsWeaponMode => IsWeaponModeSlot(Slot);

        internal static bool IsWeaponModeSlot(PowerUpSlot slot)
        {
            return slot == PowerUpSlot.Double
                || slot == PowerUpSlot.Laser
                || slot == PowerUpSlot.Triple;
        }
    }

    /// <summary>
    /// Allocation-free Presentation observation for one visible gauge entry.
    /// </summary>
    public readonly struct PowerUpGaugeSlotView
    {
        public PowerUpGaugeSlotView(
            int gaugeIndex,
            PowerUpSlot slot,
            string nameKey,
            int level,
            int maxLevel,
            int progress,
            int requiredCapsules,
            bool isActiveWeaponMode)
        {
            GaugeIndex = gaugeIndex;
            Slot = slot;
            NameKey = nameKey;
            Level = level;
            MaxLevel = maxLevel;
            Progress = progress;
            RequiredCapsules = requiredCapsules;
            IsActiveWeaponMode = isActiveWeaponMode;
        }

        public int GaugeIndex { get; }
        public PowerUpSlot Slot { get; }
        public string NameKey { get; }
        public int Level { get; }
        public int MaxLevel { get; }
        public int Progress { get; }
        public int RequiredCapsules { get; }
        public bool IsActiveWeaponMode { get; }
    }

    /// <summary>
    /// Data-ordered Gradius gauge. MainShot remains hidden on the legacy
    /// seven-slot layout and is selectable on ship-owned six-slot layouts.
    /// </summary>
    public sealed class PowerUpGauge
    {
        public const int SlotCount = 8;
        public const int DefaultGaugeSlotCount = 7;
        public const int ShipGaugeSlotCount = 6;
        public const int NoSelection = -1;

        readonly int[] _levels = new int[SlotCount];
        readonly int[] _progress = new int[SlotCount];
        readonly int[] _maxLevels = new int[SlotCount];
        readonly PowerUpCostCurve[] _costCurves =
            new PowerUpCostCurve[SlotCount];
        readonly PowerUpSlotDefinition[] _gaugeSlots;
        readonly ReadOnlyCollection<PowerUpSlotDefinition>
            _readOnlyGaugeSlots;

        public PowerUpGauge(int[] maxLevels)
            : this(maxLevels, PowerUpCostCurve.FlatOne)
        {
        }

        /// <summary>
        /// Compatibility constructor. Four entries map to the legacy
        /// MainShot/Missile/Option/Shield axes; eight entries address all stable
        /// state ids. Visible order always uses the canonical seven-slot gauge.
        /// </summary>
        public PowerUpGauge(
            int[] maxLevels,
            PowerUpCostCurve costCurve)
            : this(
                GetLegacyMainShotMax(maxLevels),
                CreateCanonicalDefinitions(maxLevels, costCurve),
                costCurve)
        {
        }

        public PowerUpGauge(
            int mainShotMaxLevel,
            IReadOnlyList<PowerUpSlotDefinition> gaugeSlots,
            PowerUpCostCurve mainShotCostCurve = null)
        {
            if (mainShotMaxLevel < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(mainShotMaxLevel));
            if (gaugeSlots == null)
                throw new ArgumentNullException(nameof(gaugeSlots));
            if (gaugeSlots.Count != DefaultGaugeSlotCount
                && gaugeSlots.Count != ShipGaugeSlotCount)
                throw new ArgumentException(
                    $"The gauge requires exactly {ShipGaugeSlotCount} "
                    + $"or {DefaultGaugeSlotCount} visible slots.",
                    nameof(gaugeSlots));

            for (int i = 0; i < SlotCount; i++)
            {
                _maxLevels[i] = 0;
                _costCurves[i] = PowerUpCostCurve.FlatOne;
            }
            _maxLevels[(int)PowerUpSlot.MainShot] =
                mainShotMaxLevel;
            _costCurves[(int)PowerUpSlot.MainShot] =
                mainShotCostCurve ?? PowerUpCostCurve.FlatOne;
            _costCurves[(int)PowerUpSlot.MainShot]
                .GetCostForCurrentLevel(mainShotMaxLevel - 1);

            _gaugeSlots =
                new PowerUpSlotDefinition[gaugeSlots.Count];
            var seen = new bool[SlotCount];
            for (int i = 0; i < gaugeSlots.Count; i++)
            {
                PowerUpSlotDefinition definition =
                    gaugeSlots[i]
                    ?? throw new ArgumentException(
                        "Gauge slots cannot contain null.",
                        nameof(gaugeSlots));
                int stateIndex = (int)definition.Slot;
                if (seen[stateIndex])
                    throw new ArgumentException(
                        $"Gauge slot '{definition.Slot}' is duplicated.",
                        nameof(gaugeSlots));
                seen[stateIndex] = true;
                _gaugeSlots[i] = definition;
                _maxLevels[stateIndex] = definition.MaxLevel;
                _costCurves[stateIndex] = definition.CostCurve;
            }
            ValidateVisibleSlots(seen, gaugeSlots.Count);
            _readOnlyGaugeSlots =
                Array.AsReadOnly(_gaugeSlots);
        }

        public static PowerUpGauge CreateDefault()
        {
            PowerUpCostCurve curve =
                PowerUpCostCurve.CreateProvisional();
            return new PowerUpGauge(
                5,
                CreateCanonicalDefinitions(
                    new[] { 5, 3, 4, 3 },
                    curve),
                curve);
        }

        public int Cursor { get; private set; } = NoSelection;
        public int GaugeSlotCount => _gaugeSlots.Length;
        public IReadOnlyList<PowerUpSlotDefinition> GaugeSlots =>
            _readOnlyGaugeSlots;
        public PowerUpWeaponMode ActiveWeaponMode { get; private set; }
        public bool HasSelection => Cursor != NoSelection;
        public PowerUpSlot SelectedSlot =>
            Cursor == NoSelection
                ? PowerUpSlot.MainShot
                : _gaugeSlots[Cursor].Slot;

        public int GetLevel(PowerUpSlot slot) =>
            _levels[ValidateSlot(slot)];
        public int GetMaxLevel(PowerUpSlot slot) =>
            _maxLevels[ValidateSlot(slot)];
        public int GetProgress(PowerUpSlot slot) =>
            _progress[ValidateSlot(slot)];

        public PowerUpGaugeSlotView GetGaugeSlotView(int gaugeIndex)
        {
            if (gaugeIndex < 0 || gaugeIndex >= _gaugeSlots.Length)
                throw new ArgumentOutOfRangeException(nameof(gaugeIndex));
            PowerUpSlotDefinition definition =
                _gaugeSlots[gaugeIndex];
            PowerUpSlot slot = definition.Slot;
            return new PowerUpGaugeSlotView(
                gaugeIndex,
                slot,
                definition.NameKey,
                GetLevel(slot),
                definition.MaxLevel,
                GetProgress(slot),
                GetRequiredCapsules(slot),
                IsActiveModeSlot(slot));
        }

        public int GetRequiredCapsules(PowerUpSlot slot)
        {
            int index = ValidateSlot(slot);
            PowerUpSlotDefinition definition =
                FindDefinitionOrNull(slot);
            if (definition != null
                && definition.ActivatesImmediately
                && _levels[index] < _maxLevels[index])
                return 1;
            return _levels[index] >= _maxLevels[index]
                ? 0
                : _costCurves[index]
                    .GetCostForCurrentLevel(_levels[index]);
        }

        public int GetRequiredCapsulesForLevel(
            PowerUpSlot slot,
            int currentLevel)
        {
            int index = ValidateSlot(slot);
            if (currentLevel < 0
                || currentLevel > _maxLevels[index])
                throw new ArgumentOutOfRangeException(
                    nameof(currentLevel));
            PowerUpSlotDefinition definition =
                FindDefinitionOrNull(slot);
            if (definition != null
                && definition.ActivatesImmediately
                && currentLevel < _maxLevels[index])
                return 1;
            return currentLevel == _maxLevels[index]
                ? 0
                : _costCurves[index]
                    .GetCostForCurrentLevel(currentLevel);
        }

        public int GetRemainingCapsules(PowerUpSlot slot)
        {
            int required = GetRequiredCapsules(slot);
            return required == 0
                ? 0
                : required - _progress[(int)slot];
        }

        public void Collect()
        {
            Cursor = Cursor == NoSelection
                ? 0
                : (Cursor + 1) % _gaugeSlots.Length;
        }

        public bool CanActivate
        {
            get
            {
                if (Cursor == NoSelection)
                    return false;
                PowerUpSlot slot = _gaugeSlots[Cursor].Slot;
                return _levels[(int)slot]
                    < _maxLevels[(int)slot];
            }
        }

        public PowerUpActivationResult LastActivationResult
        {
            get;
            private set;
        } = PowerUpActivationResult.NoSelection;

        public bool Activate()
        {
            return ActivateDetailed()
                == PowerUpActivationResult.LevelIncreased;
        }

        public PowerUpActivationResult ActivateDetailed()
        {
            if (Cursor == NoSelection)
            {
                LastActivationResult =
                    PowerUpActivationResult.NoSelection;
                return LastActivationResult;
            }

            PowerUpSlot slot = _gaugeSlots[Cursor].Slot;
            int stateIndex = (int)slot;
            if (_levels[stateIndex] >= _maxLevels[stateIndex])
            {
                LastActivationResult =
                    PowerUpActivationResult.SlotMaxed;
                return LastActivationResult;
            }

            PowerUpSlotDefinition definition =
                _gaugeSlots[Cursor];
            Cursor = NoSelection;
            if (definition.ActivatesImmediately)
            {
                ActivateWeaponMode(slot);
                LastActivationResult =
                    PowerUpActivationResult.LevelIncreased;
                return LastActivationResult;
            }

            _progress[stateIndex]++;
            int required = _costCurves[stateIndex]
                .GetCostForCurrentLevel(_levels[stateIndex]);
            if (_progress[stateIndex] < required)
            {
                LastActivationResult =
                    PowerUpActivationResult.ProgressAdded;
                return LastActivationResult;
            }

            _progress[stateIndex] = 0;
            if (PowerUpSlotDefinition.IsWeaponModeSlot(slot))
                ActivateWeaponMode(slot);
            else
                _levels[stateIndex]++;
            LastActivationResult =
                PowerUpActivationResult.LevelIncreased;
            return LastActivationResult;
        }

        public int[] ExportLevels() => (int[])_levels.Clone();
        public int[] ExportProgress() => (int[])_progress.Clone();

        public int GrantLevels(PowerUpSlot slot, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            int index = ValidateSlot(slot);
            int previous = _levels[index];
            if (_maxLevels[index] == 0)
                return 0;
            if (PowerUpSlotDefinition.IsWeaponModeSlot(slot)
                && amount > 0)
            {
                ActivateWeaponMode(slot);
                return _levels[index] - previous;
            }
            long requested = (long)previous + amount;
            _levels[index] = requested >= _maxLevels[index]
                ? _maxLevels[index]
                : (int)requested;
            int required = GetRequiredCapsules(slot);
            if (required == 0)
                _progress[index] = 0;
            else if (_progress[index] >= required)
                _progress[index] = required - 1;
            return _levels[index] - previous;
        }

        public PowerUpGauge CreateEmptyWithSameRules()
        {
            return new PowerUpGauge(
                _maxLevels[(int)PowerUpSlot.MainShot],
                _gaugeSlots,
                _costCurves[(int)PowerUpSlot.MainShot]);
        }

        public void ImportLevels(int[] levels)
        {
            if (levels == null)
                throw new ArgumentNullException(nameof(levels));
            int[] expanded = ExpandLegacyState(levels, nameof(levels));
            for (int i = 0; i < SlotCount; i++)
            {
                _levels[i] = Math.Max(
                    0,
                    Math.Min(expanded[i], _maxLevels[i]));
                _progress[i] = 0;
            }
            NormalizeImportedWeaponModes();
        }

        internal void RestoreState(
            int[] levels,
            int cursor,
            int[] progress = null)
        {
            if (levels == null)
                throw new ArgumentNullException(nameof(levels));
            if (levels.Length != SlotCount)
                throw new ArgumentException(
                    $"levels must have exactly {SlotCount} entries",
                    nameof(levels));
            if (cursor < NoSelection
                || cursor >= _gaugeSlots.Length)
                throw new ArgumentOutOfRangeException(nameof(cursor));
            if (progress != null
                && progress.Length != SlotCount)
                throw new ArgumentException(
                    $"progress must have exactly {SlotCount} entries",
                    nameof(progress));

            int activeModes = 0;
            PowerUpSlot activeSlot = PowerUpSlot.MainShot;
            for (int i = 0; i < SlotCount; i++)
            {
                if (levels[i] < 0 || levels[i] > _maxLevels[i])
                    throw new ArgumentException(
                        $"level {i} is outside [0, {_maxLevels[i]}]",
                        nameof(levels));
                int restoredProgress =
                    progress == null ? 0 : progress[i];
                int required = GetRequiredCapsulesForLevel(
                    (PowerUpSlot)i,
                    levels[i]);
                if (restoredProgress < 0
                    || (required == 0 && restoredProgress != 0)
                    || (required > 0
                        && restoredProgress >= required))
                {
                    throw new ArgumentException(
                        $"progress {i} is outside the current level cost.",
                        nameof(progress));
                }
                if (PowerUpSlotDefinition.IsWeaponModeSlot(
                        (PowerUpSlot)i)
                    && levels[i] > 0)
                {
                    activeModes++;
                    activeSlot = (PowerUpSlot)i;
                }
                _levels[i] = levels[i];
                _progress[i] = restoredProgress;
            }
            if (activeModes > 1)
                throw new ArgumentException(
                    "Only one primary weapon mode can be active.",
                    nameof(levels));
            ActiveWeaponMode = ToWeaponMode(activeSlot);
            Cursor = cursor;
            LastActivationResult =
                PowerUpActivationResult.NoSelection;
        }

        public bool IsActiveModeSlot(PowerUpSlot slot)
        {
            return ToWeaponMode(slot) == ActiveWeaponMode
                && ActiveWeaponMode != PowerUpWeaponMode.None;
        }

        public int SpeedBonusNumerator =>
            FindDefinition(PowerUpSlot.Speed)
                .SpeedBonusNumerator;
        public int SpeedBonusDenominator =>
            FindDefinition(PowerUpSlot.Speed)
                .SpeedBonusDenominator;

        void ActivateWeaponMode(PowerUpSlot slot)
        {
            for (int i = 0; i < _gaugeSlots.Length; i++)
            {
                PowerUpSlot other = _gaugeSlots[i].Slot;
                if (!PowerUpSlotDefinition.IsWeaponModeSlot(other))
                    continue;
                _levels[(int)other] = other == slot ? 1 : 0;
                _progress[(int)other] = 0;
            }
            ActiveWeaponMode = ToWeaponMode(slot);
        }

        void NormalizeImportedWeaponModes()
        {
            PowerUpSlot active = PowerUpSlot.MainShot;
            for (int i = 0; i < _gaugeSlots.Length; i++)
            {
                PowerUpSlot slot = _gaugeSlots[i].Slot;
                if (PowerUpSlotDefinition.IsWeaponModeSlot(slot)
                    && _levels[(int)slot] > 0)
                {
                    if (active == PowerUpSlot.MainShot)
                        active = slot;
                    else
                        _levels[(int)slot] = 0;
                }
            }
            ActiveWeaponMode = ToWeaponMode(active);
        }

        PowerUpSlotDefinition FindDefinition(PowerUpSlot slot)
        {
            PowerUpSlotDefinition definition =
                FindDefinitionOrNull(slot);
            if (definition != null)
                return definition;
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        PowerUpSlotDefinition FindDefinitionOrNull(PowerUpSlot slot)
        {
            for (int i = 0; i < _gaugeSlots.Length; i++)
                if (_gaugeSlots[i].Slot == slot)
                    return _gaugeSlots[i];
            return null;
        }

        static void ValidateVisibleSlots(bool[] seen, int gaugeSlotCount)
        {
            PowerUpSlot[] required =
            {
                PowerUpSlot.Speed,
                PowerUpSlot.Missile,
                PowerUpSlot.Option,
                PowerUpSlot.Shield
            };
            for (int i = 0; i < required.Length; i++)
                if (!seen[(int)required[i]])
                    throw new ArgumentException(
                        $"Gauge slot '{required[i]}' is missing.",
                        "gaugeSlots");

            if (gaugeSlotCount == ShipGaugeSlotCount
                && !seen[(int)PowerUpSlot.MainShot])
            {
                throw new ArgumentException(
                    "The ship gauge requires a MainShot slot.",
                    "gaugeSlots");
            }

            int weaponModes = 0;
            for (int i = (int)PowerUpSlot.Double;
                i <= (int)PowerUpSlot.Triple;
                i++)
            {
                if (seen[i])
                    weaponModes++;
            }
            int expectedModes =
                gaugeSlotCount == DefaultGaugeSlotCount ? 3 : 1;
            if (weaponModes != expectedModes)
                throw new ArgumentException(
                    $"The {gaugeSlotCount}-slot gauge requires exactly "
                    + $"{expectedModes} weapon mode slot(s).",
                    "gaugeSlots");
        }

        static int ValidateSlot(PowerUpSlot slot)
        {
            int index = (int)slot;
            if (index < 0 || index >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return index;
        }

        static int GetLegacyMainShotMax(int[] maxLevels)
        {
            if (maxLevels == null)
                throw new ArgumentNullException(nameof(maxLevels));
            if (maxLevels.Length != 4
                && maxLevels.Length != SlotCount)
                throw new ArgumentException(
                    $"maxLevels must have exactly 4 or {SlotCount} entries",
                    nameof(maxLevels));
            for (int i = 0; i < maxLevels.Length; i++)
                if (maxLevels[i] < 1)
                    throw new ArgumentException(
                        "every max level must be at least 1",
                        nameof(maxLevels));
            return maxLevels[(int)PowerUpSlot.MainShot];
        }

        static PowerUpSlotDefinition[]
            CreateCanonicalDefinitions(
                int[] maxLevels,
                PowerUpCostCurve costCurve)
        {
            if (costCurve == null)
                throw new ArgumentNullException(nameof(costCurve));
            GetLegacyMainShotMax(maxLevels);
            int missile = maxLevels[(int)PowerUpSlot.Missile];
            int option = maxLevels[(int)PowerUpSlot.Option];
            int shield = maxLevels[(int)PowerUpSlot.Shield];
            int speed = maxLevels.Length == SlotCount
                ? maxLevels[(int)PowerUpSlot.Speed]
                : 5;
            int doubleMax = maxLevels.Length == SlotCount
                ? maxLevels[(int)PowerUpSlot.Double]
                : 1;
            int laser = maxLevels.Length == SlotCount
                ? maxLevels[(int)PowerUpSlot.Laser]
                : 1;
            int triple = maxLevels.Length == SlotCount
                ? maxLevels[(int)PowerUpSlot.Triple]
                : 1;
            return new[]
            {
                new PowerUpSlotDefinition(
                    PowerUpSlot.Speed,
                    "powerUp.speed",
                    speed,
                    costCurve,
                    256,
                    60),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Missile,
                    "powerUp.missile",
                    missile,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Double,
                    "powerUp.double",
                    doubleMax,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Laser,
                    "powerUp.laser",
                    laser,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Triple,
                    "powerUp.triple",
                    triple,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Option,
                    "powerUp.option",
                    option,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Shield,
                    "powerUp.shield",
                    shield,
                    costCurve)
            };
        }

        static int[] ExpandLegacyState(
            int[] source,
            string parameterName)
        {
            if (source.Length == SlotCount)
                return source;
            if (source.Length != 4)
                throw new ArgumentException(
                    $"State must have exactly 4 or {SlotCount} entries.",
                    parameterName);
            var expanded = new int[SlotCount];
            Array.Copy(source, expanded, source.Length);
            return expanded;
        }

        static PowerUpWeaponMode ToWeaponMode(PowerUpSlot slot)
        {
            switch (slot)
            {
                case PowerUpSlot.Double:
                    return PowerUpWeaponMode.Double;
                case PowerUpSlot.Laser:
                    return PowerUpWeaponMode.Laser;
                case PowerUpSlot.Triple:
                    return PowerUpWeaponMode.Triple;
                default:
                    return PowerUpWeaponMode.None;
            }
        }
    }
}
