using System;

namespace Shmup.Core
{
    /// <summary>
    /// Converts a caller-supplied UTC calendar date into a stable run seed.
    /// This type never reads a clock; Presentation owns UTC date acquisition.
    /// </summary>
    public static class DailySeed
    {
        const uint OffsetBasis = 2166136261U;
        const uint Prime = 16777619U;

        /// <summary>
        /// Hashes a valid Gregorian date encoded as yyyymmdd. Bytes are folded
        /// least-significant first so the result is independent of platform
        /// endianness.
        /// </summary>
        public static ulong FromDate(int yyyymmdd)
        {
            ValidateDate(yyyymmdd);

            unchecked
            {
                uint value = (uint)yyyymmdd;
                uint hash = OffsetBasis;
                for (int byteIndex = 0; byteIndex < 4; byteIndex++)
                {
                    hash ^= (byte)value;
                    hash *= Prime;
                    value >>= 8;
                }
                return hash;
            }
        }

        static void ValidateDate(int yyyymmdd)
        {
            int year = yyyymmdd / 10000;
            int month = yyyymmdd / 100 % 100;
            int day = yyyymmdd % 100;
            if (year < 1 || year > 9999 || month < 1 || month > 12)
                throw new ArgumentOutOfRangeException(nameof(yyyymmdd));

            int daysInMonth;
            switch (month)
            {
                case 2:
                    daysInMonth = IsLeapYear(year) ? 29 : 28;
                    break;
                case 4:
                case 6:
                case 9:
                case 11:
                    daysInMonth = 30;
                    break;
                default:
                    daysInMonth = 31;
                    break;
            }

            if (day < 1 || day > daysInMonth)
                throw new ArgumentOutOfRangeException(nameof(yyyymmdd));
        }

        static bool IsLeapYear(int year)
        {
            return year % 4 == 0
                && (year % 100 != 0 || year % 400 == 0);
        }
    }
}
