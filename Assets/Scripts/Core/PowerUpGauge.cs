using System;

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

        /// <summary>Compatibility curve for direct constructors and old tests.</summary>
        public static PowerUpCostCurve FlatOne { get; } =
            new PowerUpCostCurve(1, 0, 0);

        /// <summary>
        /// Provisional REQ-053 fallback for legacy GameData. GROK must replace
        /// these coefficients with schema-owned balance values.
        /// </summary>
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

    public enum PowerUpSlot
    {
        MainShot = 0,
        Missile = 1,
        Option = 2,
        Shield = 3
    }

    /// <summary>
    /// Gradius-style power-up gauge. Collecting a capsule advances the cursor
    /// through the four slots (wrapping); activating upgrades the highlighted
    /// slot by one level and clears the cursor. Levels are capped per slot.
    /// Max levels are a balance decision owned by a human (AGENTS.md §7).
    /// </summary>
    public sealed class PowerUpGauge
    {
        public const int SlotCount = 4;
        public const int NoSelection = -1;

        readonly int[] _levels = new int[SlotCount];
        readonly int[] _progress = new int[SlotCount];
        readonly int[] _maxLevels;
        readonly PowerUpCostCurve _costCurve;

        public PowerUpGauge(int[] maxLevels)
            : this(maxLevels, PowerUpCostCurve.FlatOne)
        {
        }

        public PowerUpGauge(
            int[] maxLevels,
            PowerUpCostCurve costCurve)
        {
            if (maxLevels == null) throw new ArgumentNullException(nameof(maxLevels));
            if (costCurve == null) throw new ArgumentNullException(nameof(costCurve));
            if (maxLevels.Length != SlotCount)
                throw new ArgumentException($"maxLevels must have exactly {SlotCount} entries");
            for (int i = 0; i < SlotCount; i++)
            {
                if (maxLevels[i] < 1)
                    throw new ArgumentException("every max level must be at least 1");
                costCurve.GetCostForCurrentLevel(maxLevels[i] - 1);
            }
            _maxLevels = (int[])maxLevels.Clone();
            _costCurve = costCurve;
        }

        /// <summary>Placeholder caps until the human balance pass (AGENTS.md §7) fixes them.</summary>
        public static PowerUpGauge CreateDefault() => new PowerUpGauge(new[] { 5, 3, 4, 3 });

        /// <summary>Index of the highlighted slot, or NoSelection when the gauge is empty.</summary>
        public int Cursor { get; private set; } = NoSelection;

        public int GetLevel(PowerUpSlot slot) => _levels[(int)slot];
        public int GetMaxLevel(PowerUpSlot slot) => _maxLevels[(int)slot];
        public int GetProgress(PowerUpSlot slot) => _progress[(int)slot];

        public int GetRequiredCapsules(PowerUpSlot slot)
        {
            int index = (int)slot;
            return _levels[index] >= _maxLevels[index]
                ? 0
                : _costCurve.GetCostForCurrentLevel(_levels[index]);
        }

        public int GetRequiredCapsulesForLevel(
            PowerUpSlot slot,
            int currentLevel)
        {
            int index = (int)slot;
            if (currentLevel < 0 || currentLevel > _maxLevels[index])
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            return currentLevel == _maxLevels[index]
                ? 0
                : _costCurve.GetCostForCurrentLevel(currentLevel);
        }

        public int GetRemainingCapsules(PowerUpSlot slot)
        {
            int required = GetRequiredCapsules(slot);
            return required == 0
                ? 0
                : required - _progress[(int)slot];
        }

        /// <summary>Called when the player picks up a power-up capsule.</summary>
        public void Collect()
        {
            Cursor = Cursor == NoSelection ? 0 : (Cursor + 1) % SlotCount;
        }

        public bool CanActivate => Cursor != NoSelection && _levels[Cursor] < _maxLevels[Cursor];
        public PowerUpActivationResult LastActivationResult { get; private set; } =
            PowerUpActivationResult.NoSelection;

        /// <summary>
        /// Upgrade the highlighted slot. Returns false (and keeps the cursor) when
        /// nothing is highlighted or the slot is already at max level.
        /// Direct calls bypass InputCommand and InputRecorder, so they are intended
        /// only for state setup or explicitly non-replayable development cheats.
        /// </summary>
        public bool Activate()
        {
            return ActivateDetailed() == PowerUpActivationResult.LevelIncreased;
        }

        /// <summary>
        /// Invests the currently highlighted capsule into that slot. Partial
        /// progress is retained per slot; the cursor is consumed even when the
        /// level cost is not yet complete. Capsules passed while seeking a slot
        /// are still lost, preserving the Gradius-style routing tension.
        /// </summary>
        public PowerUpActivationResult ActivateDetailed()
        {
            if (Cursor == NoSelection)
            {
                LastActivationResult = PowerUpActivationResult.NoSelection;
                return LastActivationResult;
            }
            if (_levels[Cursor] >= _maxLevels[Cursor])
            {
                LastActivationResult = PowerUpActivationResult.SlotMaxed;
                return LastActivationResult;
            }

            int activatedSlot = Cursor;
            _progress[activatedSlot]++;
            Cursor = NoSelection;
            int required =
                _costCurve.GetCostForCurrentLevel(_levels[activatedSlot]);
            if (_progress[activatedSlot] < required)
            {
                LastActivationResult = PowerUpActivationResult.ProgressAdded;
                return LastActivationResult;
            }

            _progress[activatedSlot] = 0;
            _levels[activatedSlot]++;
            LastActivationResult = PowerUpActivationResult.LevelIncreased;
            return LastActivationResult;
        }

        /// <summary>Snapshot of current levels, ordered by PowerUpSlot. Safe to mutate.</summary>
        public int[] ExportLevels() => (int[])_levels.Clone();
        public int[] ExportProgress() => (int[])_progress.Clone();

        public int GrantLevels(PowerUpSlot slot, int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            int index = (int)slot;
            int previous = _levels[index];
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
                (int[])_maxLevels.Clone(),
                _costCurve);
        }

        /// <summary>Restore levels (e.g. carried over from a previous run), clamped to [0, max].</summary>
        public void ImportLevels(int[] levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Length != SlotCount)
                throw new ArgumentException($"levels must have exactly {SlotCount} entries");
            for (int i = 0; i < SlotCount; i++)
            {
                _levels[i] = Math.Max(0, Math.Min(levels[i], _maxLevels[i]));
                _progress[i] = 0;
            }
        }

        /// <summary>
        /// Exact restore used after RunManager has validated serializer-facing
        /// suspend data. Unlike ImportLevels, invalid values are rejected rather
        /// than clamped so corrupted checkpoints cannot silently change a run.
        /// </summary>
        internal void RestoreState(
            int[] levels,
            int cursor,
            int[] progress = null)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Length != SlotCount)
                throw new ArgumentException(
                    $"levels must have exactly {SlotCount} entries",
                    nameof(levels));
            if (cursor < NoSelection || cursor >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(cursor));
            if (progress != null && progress.Length != SlotCount)
                throw new ArgumentException(
                    $"progress must have exactly {SlotCount} entries",
                    nameof(progress));

            for (int i = 0; i < SlotCount; i++)
            {
                if (levels[i] < 0 || levels[i] > _maxLevels[i])
                    throw new ArgumentException(
                        $"level {i} is outside [0, {_maxLevels[i]}]",
                        nameof(levels));
                int restoredProgress = progress == null ? 0 : progress[i];
                int required = levels[i] >= _maxLevels[i]
                    ? 0
                    : _costCurve.GetCostForCurrentLevel(levels[i]);
                if (restoredProgress < 0
                    || (required == 0 && restoredProgress != 0)
                    || (required > 0 && restoredProgress >= required))
                {
                    throw new ArgumentException(
                        $"progress {i} is outside the current level cost.",
                        nameof(progress));
                }
                _levels[i] = levels[i];
                _progress[i] = restoredProgress;
            }
            Cursor = cursor;
            LastActivationResult = PowerUpActivationResult.NoSelection;
        }
    }
}
