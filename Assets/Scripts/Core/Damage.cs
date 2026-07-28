using System;

namespace Shmup.Core
{
    /// <summary>
    /// Integer-only damage math. No floats anywhere in the combat path so results
    /// are bit-identical on every platform (AGENTS.md §4).
    /// </summary>
    public static class Damage
    {
        /// <summary>
        /// Weapon damage at a given level: base × (100 + 50 × (level − 1)) / 100.
        /// Level 1 = base damage, each further level adds +50% of base (integer-floored).
        /// </summary>
        public static int Compute(int baseDamage, int weaponLevel)
        {
            if (baseDamage < 0) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (weaponLevel < 1) throw new ArgumentOutOfRangeException(nameof(weaponLevel));
            return baseDamage * (100 + 50 * (weaponLevel - 1)) / 100;
        }

        /// <summary>Remaining HP after taking damage, floored at 0. Negative damage is treated as 0.</summary>
        public static int ApplyToHp(int hp, int damage)
        {
            return Math.Max(0, hp - Math.Max(0, damage));
        }
    }
}
