using System;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        internal readonly struct ExactFraction
        {
            public ExactFraction(int numerator, int denominator)
            {
                Numerator = numerator;
                Denominator = denominator;
            }

            public int Numerator { get; }
            public int Denominator { get; }
        }

        static int Require(int? value, string path)
        {
            if (!value.HasValue)
                throw Error(path, "is required.");
            return value.Value;
        }

        static long Require(long? value, string path)
        {
            if (!value.HasValue)
                throw Error(path, "is required.");
            return value.Value;
        }

        static decimal Require(decimal? value, string path)
        {
            if (!value.HasValue)
                throw Error(path, "is required.");
            return value.Value;
        }

        static string RequireText(string value, string path)
        {
            if (string.IsNullOrEmpty(value))
                throw Error(path, "must be a non-empty string.");
            return value;
        }

        static string OptionalText(string value, string path)
        {
            return value == null ? null : RequireText(value, path);
        }

        static T[] RequireArray<T>(T[] value, string path, bool allowEmpty = false)
        {
            if (value == null)
                throw Error(path, "is required.");
            if (!allowEmpty && value.Length == 0)
                throw Error(path, "must contain at least one item.");
            return value;
        }

        static int ToSubUnits(decimal worldUnits, string path)
        {
            decimal scaled;
            try
            {
                scaled = checked(worldUnits * SimSpace.SubUnitsPerWorldUnit);
            }
            catch (OverflowException ex)
            {
                throw Error(path, "is outside the supported coordinate range.", ex);
            }

            if (decimal.Truncate(scaled) != scaled
                || scaled < int.MinValue
                || scaled > int.MaxValue)
                throw Error(
                    path,
                    $"must resolve to a whole 1/{SimSpace.SubUnitsPerWorldUnit} world-unit subunit.");
            return decimal.ToInt32(scaled);
        }

        static ExactFraction ToSubUnitFraction(decimal worldUnits, string path)
        {
            decimal scaled;
            try
            {
                scaled = checked(worldUnits * SimSpace.SubUnitsPerWorldUnit);
            }
            catch (OverflowException ex)
            {
                throw Error(path, "is outside the supported numeric range.", ex);
            }
            return DecimalToFraction(scaled, path);
        }

        static ExactFraction ToPerTickSpeed(decimal worldUnitsPerSecond, string path)
        {
            if (worldUnitsPerSecond < 0)
                throw Error(path, "cannot be negative.");
            ExactFraction perSecond = ToSubUnitFraction(worldUnitsPerSecond, path);
            long denominator = (long)perSecond.Denominator * SimSpace.TicksPerSecond;
            if (denominator > int.MaxValue)
                throw Error(path, "needs a denominator larger than the simulation supports.");
            return new ExactFraction(perSecond.Numerator, (int)denominator);
        }

        static ExactFraction ToPerTickAcceleration(
            decimal worldUnitsPerSecondSquared,
            string path)
        {
            if (worldUnitsPerSecondSquared < 0)
                throw Error(path, "cannot be negative.");
            ExactFraction perSecondSquared =
                ToSubUnitFraction(worldUnitsPerSecondSquared, path);
            long denominator = (long)perSecondSquared.Denominator
                * SimSpace.TicksPerSecond
                * SimSpace.TicksPerSecond;
            if (denominator > int.MaxValue)
                throw Error(
                    path,
                    "needs a denominator larger than the simulation supports.");
            int divisor = GreatestCommonDivisor(
                Math.Abs(perSecondSquared.Numerator),
                (int)denominator);
            return new ExactFraction(
                perSecondSquared.Numerator / divisor,
                (int)denominator / divisor);
        }

        static ExactFraction DecimalToFraction(decimal value, string path)
        {
            int[] bits = decimal.GetBits(value);
            bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;
            int scale = (bits[3] >> 16) & 0x7f;
            if (bits[2] != 0)
                throw Error(path, "has more precision than the simulation supports.");

            ulong magnitude = ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
            while (scale > 0 && magnitude % 10 == 0)
            {
                magnitude /= 10;
                scale--;
            }

            long denominator = 1;
            for (int i = 0; i < scale; i++)
                denominator = checked(denominator * 10);
            if (magnitude > int.MaxValue || denominator > int.MaxValue)
                throw Error(path, "has more precision than the simulation supports.");

            int numerator = (int)magnitude;
            if (negative) numerator = -numerator;
            int divisor = GreatestCommonDivisor(Math.Abs(numerator), (int)denominator);
            return new ExactFraction(numerator / divisor, (int)denominator / divisor);
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

        static GameDataParseException Error(
            string path,
            string message,
            Exception innerException = null)
        {
            string fullMessage = $"{path} {message}";
            return innerException == null
                ? new GameDataParseException(fullMessage)
                : new GameDataParseException(fullMessage, innerException);
        }
    }
}
