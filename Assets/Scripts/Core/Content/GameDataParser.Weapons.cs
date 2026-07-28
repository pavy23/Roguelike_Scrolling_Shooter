using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        static WeaponParseResult ParseWeapons(WeaponsDto root)
        {
            int schemaVersion = Require(root.schemaVersion, "weapons.json.schemaVersion");
            if (schemaVersion != SupportedSchemaVersion)
                throw Error(
                    "weapons.json.schemaVersion",
                    $"must be {SupportedSchemaVersion}, but was {schemaVersion}.");

            WeaponDto[] source = RequireArray(root.weapons, "weapons.json.weapons");
            var definitions = new WeaponDefinition[source.Length];
            var maxLevels = new int[PowerUpGauge.SlotCount];
            var seenSlots = new bool[PowerUpGauge.SlotCount];
            WeaponDefinition mainShot = null;
            WeaponDefinition missile = null;

            for (int i = 0; i < source.Length; i++)
            {
                string path = $"weapons.json.weapons[{i}]";
                WeaponDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");

                PowerUpSlot slot = ParsePowerUpSlot(item.slot, path + ".slot");
                if (seenSlots[(int)slot])
                    throw Error(path + ".slot", $"duplicates slot '{slot}'.");

                ExactFraction speed = ToPerTickSpeed(
                    Require(item.projectileSpeed, path + ".projectileSpeed"),
                    path + ".projectileSpeed");
                var definition = new WeaponDefinition(
                    RequireText(item.id, path + ".id"),
                    slot,
                    Require(item.baseDamage, path + ".baseDamage"),
                    Require(item.fireIntervalTicks, path + ".fireIntervalTicks"),
                    speed.Numerator,
                    speed.Denominator,
                    ToSubUnits(
                        Require(item.projectileHalfWidth, path + ".projectileHalfWidth"),
                        path + ".projectileHalfWidth"),
                    ToSubUnits(
                        Require(item.projectileHalfHeight, path + ".projectileHalfHeight"),
                        path + ".projectileHalfHeight"),
                    Require(item.maxLevel, path + ".maxLevel"));

                definitions[i] = definition;
                seenSlots[(int)slot] = true;
                maxLevels[(int)slot] = definition.MaxLevel;
                if (slot == PowerUpSlot.MainShot) mainShot = definition;
                if (slot == PowerUpSlot.Missile) missile = definition;
            }

            for (int i = 0; i < seenSlots.Length; i++)
                if (!seenSlots[i])
                    throw Error(
                        "weapons.json.weapons",
                        $"is missing slot '{(PowerUpSlot)i}'.");

            return new WeaponParseResult(
                definitions,
                maxLevels,
                mainShot,
                missile);
        }

        static PowerUpSlot ParsePowerUpSlot(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "MainShot": return PowerUpSlot.MainShot;
                case "Missile": return PowerUpSlot.Missile;
                case "Option": return PowerUpSlot.Option;
                case "Shield": return PowerUpSlot.Shield;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        internal readonly struct WeaponParseResult
        {
            public WeaponParseResult(
                WeaponDefinition[] definitions,
                int[] maxLevels,
                WeaponDefinition mainShot,
                WeaponDefinition missile)
            {
                Definitions = definitions;
                MaxLevels = maxLevels;
                MainShot = mainShot;
                Missile = missile;
            }

            public WeaponDefinition[] Definitions { get; }
            public int[] MaxLevels { get; }
            public WeaponDefinition MainShot { get; }
            public WeaponDefinition Missile { get; }
        }
    }
}
