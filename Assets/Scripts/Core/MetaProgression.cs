using System;

namespace Shmup.Core
{
    /// <summary>
    /// Roguelike meta progression: how much of each power-up level survives death.
    /// CarryFraction is a HUMAN balance decision (AGENTS.md §7) — agents must not
    /// change the value used by the game without an explicit human sign-off.
    /// 1.0 reproduces the design doc as written (levels fully persist across runs);
    /// lower values trade persistence for tension.
    /// </summary>
    public sealed class MetaProgression
    {
        public double CarryFraction { get; }

        public MetaProgression(double carryFraction)
        {
            if (carryFraction < 0.0 || carryFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(carryFraction), "must be in [0, 1]");
            CarryFraction = carryFraction;
        }

        /// <summary>Levels carried into the next run after death: floor(level × CarryFraction) per slot.</summary>
        public int[] ApplyDeathCarry(int[] levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            var result = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
                result[i] = (int)Math.Floor(levels[i] * CarryFraction);
            return result;
        }
    }
}
