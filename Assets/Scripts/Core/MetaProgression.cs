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
        public int CarryNumerator { get; }
        public int CarryDenominator { get; }
        /// <summary>
        /// Legacy read-only view retained for source compatibility. Simulation
        /// logic uses the integer fraction properties instead.
        /// </summary>
        public double CarryFraction =>
            (double)CarryNumerator / CarryDenominator;

        /// <summary>
        /// Backward-compatible construction boundary. Gameplay calculations use
        /// the reduced integer fraction exposed by CarryNumerator/CarryDenominator.
        /// </summary>
        public MetaProgression(double carryFraction)
            : this(ConvertLegacyFraction(carryFraction))
        {
        }

        public MetaProgression(int carryNumerator, int carryDenominator)
        {
            if (carryNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(carryNumerator));
            if (carryDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(carryDenominator));
            if (carryNumerator > carryDenominator)
                throw new ArgumentOutOfRangeException(
                    nameof(carryNumerator),
                    "must not exceed the denominator");

            int divisor = GreatestCommonDivisor(
                carryNumerator,
                carryDenominator);
            CarryNumerator = carryNumerator / divisor;
            CarryDenominator = carryDenominator / divisor;
        }

        MetaProgression(LegacyFraction fraction)
            : this(fraction.Numerator, fraction.Denominator)
        {
        }

        /// <summary>
        /// Levels carried into the next run after death:
        /// floor(level * numerator / denominator) per slot.
        /// </summary>
        public int[] ApplyDeathCarry(int[] levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            var result = new int[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                result[i] = (int)(
                    (long)levels[i] * CarryNumerator
                    / CarryDenominator);
            }
            return result;
        }

        static LegacyFraction ConvertLegacyFraction(double value)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0.0
                || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    "carryFraction",
                    "must be finite and in [0, 1]");
            }
            if (value == 0.0)
                return new LegacyFraction(0, 1);
            if (value == 1.0)
                return new LegacyFraction(1, 1);

            // Continued fractions recover simple legacy values such as 0.5
            // exactly while keeping both terms in the required Int32 range.
            const int maximumDenominator = 1000000000;
            double remaining = value;
            long previousNumerator = 0;
            long numerator = 1;
            long previousDenominator = 1;
            long denominator = 0;

            for (int iteration = 0; iteration < 32; iteration++)
            {
                long whole = (long)Math.Floor(remaining);
                if (whole > (long.MaxValue - previousNumerator)
                        / Math.Max(1L, numerator)
                    || whole > (long.MaxValue - previousDenominator)
                        / Math.Max(1L, denominator))
                    break;

                long nextNumerator =
                    whole * numerator + previousNumerator;
                long nextDenominator =
                    whole * denominator + previousDenominator;
                if (nextNumerator > int.MaxValue
                    || nextDenominator > maximumDenominator)
                    break;

                previousNumerator = numerator;
                numerator = nextNumerator;
                previousDenominator = denominator;
                denominator = nextDenominator;

                double approximation =
                    (double)numerator / denominator;
                if (Math.Abs(approximation - value)
                    <= 1e-12)
                    break;

                double fraction = remaining - whole;
                if (fraction <= 0.0)
                    break;
                remaining = 1.0 / fraction;
            }

            if (denominator < 1)
            {
                denominator = maximumDenominator;
                numerator = (long)Math.Round(
                    value * denominator,
                    MidpointRounding.AwayFromZero);
            }
            return new LegacyFraction(
                (int)numerator,
                (int)denominator);
        }

        static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }
            return left == 0 ? 1 : left;
        }

        readonly struct LegacyFraction
        {
            public LegacyFraction(int numerator, int denominator)
            {
                Numerator = numerator;
                Denominator = denominator;
            }

            public int Numerator { get; }
            public int Denominator { get; }
        }
    }
}
