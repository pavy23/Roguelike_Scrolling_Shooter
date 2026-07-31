using System;

namespace Shmup.Core
{
    /// <summary>
    /// Diminishing integer scaling after a data-defined soft cap. Beyond the
    /// cap, effective gain follows floor(sqrt(rawLevel - softCap)).
    /// </summary>
    public static class PowerLevelScaling
    {
        public static int GetEffectiveLevel(
            int rawLevel,
            int softCapLevel)
        {
            if (rawLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(rawLevel));
            if (softCapLevel < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(softCapLevel));
            if (rawLevel <= softCapLevel)
                return rawLevel;
            return softCapLevel
                + IntegerSqrt(rawLevel - softCapLevel);
        }

        static int IntegerSqrt(int value)
        {
            int low = 0;
            int high = Math.Min(value, 46340);
            int result = 0;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                if (middle == 0 || middle <= value / middle)
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return result;
        }
    }
}
