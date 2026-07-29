using System;

namespace Shmup.Core
{
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
        readonly int[] _maxLevels;

        public PowerUpGauge(int[] maxLevels)
        {
            if (maxLevels == null) throw new ArgumentNullException(nameof(maxLevels));
            if (maxLevels.Length != SlotCount)
                throw new ArgumentException($"maxLevels must have exactly {SlotCount} entries");
            for (int i = 0; i < SlotCount; i++)
                if (maxLevels[i] < 1)
                    throw new ArgumentException("every max level must be at least 1");
            _maxLevels = (int[])maxLevels.Clone();
        }

        /// <summary>Placeholder caps until the human balance pass (AGENTS.md §7) fixes them.</summary>
        public static PowerUpGauge CreateDefault() => new PowerUpGauge(new[] { 5, 3, 4, 3 });

        /// <summary>Index of the highlighted slot, or NoSelection when the gauge is empty.</summary>
        public int Cursor { get; private set; } = NoSelection;

        public int GetLevel(PowerUpSlot slot) => _levels[(int)slot];
        public int GetMaxLevel(PowerUpSlot slot) => _maxLevels[(int)slot];

        /// <summary>Called when the player picks up a power-up capsule.</summary>
        public void Collect()
        {
            Cursor = Cursor == NoSelection ? 0 : (Cursor + 1) % SlotCount;
        }

        public bool CanActivate => Cursor != NoSelection && _levels[Cursor] < _maxLevels[Cursor];

        /// <summary>
        /// Upgrade the highlighted slot. Returns false (and keeps the cursor) when
        /// nothing is highlighted or the slot is already at max level.
        /// </summary>
        public bool Activate()
        {
            if (!CanActivate) return false;
            _levels[Cursor]++;
            Cursor = NoSelection;
            return true;
        }

        /// <summary>Snapshot of current levels, ordered by PowerUpSlot. Safe to mutate.</summary>
        public int[] ExportLevels() => (int[])_levels.Clone();

        /// <summary>Restore levels (e.g. carried over from a previous run), clamped to [0, max].</summary>
        public void ImportLevels(int[] levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Length != SlotCount)
                throw new ArgumentException($"levels must have exactly {SlotCount} entries");
            for (int i = 0; i < SlotCount; i++)
                _levels[i] = Math.Max(0, Math.Min(levels[i], _maxLevels[i]));
        }

        /// <summary>
        /// Exact restore used after RunManager has validated serializer-facing
        /// suspend data. Unlike ImportLevels, invalid values are rejected rather
        /// than clamped so corrupted checkpoints cannot silently change a run.
        /// </summary>
        internal void RestoreState(int[] levels, int cursor)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Length != SlotCount)
                throw new ArgumentException(
                    $"levels must have exactly {SlotCount} entries",
                    nameof(levels));
            if (cursor < NoSelection || cursor >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(cursor));

            for (int i = 0; i < SlotCount; i++)
            {
                if (levels[i] < 0 || levels[i] > _maxLevels[i])
                    throw new ArgumentException(
                        $"level {i} is outside [0, {_maxLevels[i]}]",
                        nameof(levels));
                _levels[i] = levels[i];
            }
            Cursor = cursor;
        }
    }
}
