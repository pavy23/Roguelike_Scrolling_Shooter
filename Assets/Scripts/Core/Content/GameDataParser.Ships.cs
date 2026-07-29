using System;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        const int SupportedShipsSchemaVersion = 1;

        static ShipDefinition[] ParseShips(ShipsDto root, int[] powerUpMaxLevels)
        {
            int schemaVersion = Require(
                root.schemaVersion,
                "ships.json.schemaVersion");
            if (schemaVersion != SupportedShipsSchemaVersion)
                throw Error(
                    "ships.json.schemaVersion",
                    $"must be {SupportedShipsSchemaVersion}, but was {schemaVersion}.");

            ShipDto[] source = RequireArray(root.ships, "ships.json.ships");
            var definitions = new ShipDefinition[source.Length];
            bool hasZeroCostShip = false;
            for (int i = 0; i < source.Length; i++)
            {
                string path = $"ships.json.ships[{i}]";
                ShipDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");

                int[] startingLevels = RequireArray(
                    item.startingPowerUpLevels,
                    path + ".startingPowerUpLevels",
                    allowEmpty: true);
                if (startingLevels.Length != PowerUpGauge.SlotCount)
                    throw Error(
                        path + ".startingPowerUpLevels",
                        $"must contain exactly {PowerUpGauge.SlotCount} entries.");
                var levelCopy = (int[])startingLevels.Clone();
                for (int slot = 0; slot < levelCopy.Length; slot++)
                {
                    if (levelCopy[slot] < 0)
                        throw Error(
                            $"{path}.startingPowerUpLevels[{slot}]",
                            "cannot be negative.");
                    if (levelCopy[slot] > powerUpMaxLevels[slot])
                        throw Error(
                            $"{path}.startingPowerUpLevels[{slot}]",
                            $"cannot exceed the {(PowerUpSlot)slot} max level "
                            + $"{powerUpMaxLevels[slot]}.");
                }

                long unlockCost = Require(item.unlockCost, path + ".unlockCost");
                var definition = new ShipDefinition(
                    RequireText(item.id, path + ".id"),
                    RequireText(item.displayName, path + ".displayName"),
                    Require(
                        item.moveSpeedMultiplierNumerator,
                        path + ".moveSpeedMultiplierNumerator"),
                    Require(
                        item.moveSpeedMultiplierDenominator,
                        path + ".moveSpeedMultiplierDenominator"),
                    levelCopy,
                    unlockCost,
                    ParseWeaponType(item.weaponType, path + ".weaponType"),
                    ParseShipMaxHp(item.maxHp, path + ".maxHp"));
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(
                            definitions[previous].Id,
                            definition.Id,
                            StringComparison.Ordinal))
                        throw Error(
                            path + ".id",
                            $"duplicates id '{definition.Id}'.");
                }

                definitions[i] = definition;
                if (unlockCost == 0)
                    hasZeroCostShip = true;
            }

            if (!hasZeroCostShip)
                throw Error(
                    "ships.json.ships",
                    "must contain at least one zero-cost starting ship.");
            return definitions;
        }

        static WeaponType ParseWeaponType(string value, string path)
        {
            if (value == null)
                return WeaponType.Vulcan;
            switch (RequireText(value, path))
            {
                case "vulcan": return WeaponType.Vulcan;
                case "laser": return WeaponType.Laser;
                case "spread": return WeaponType.Spread;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        static int? ParseShipMaxHp(int? value, string path)
        {
            if (!value.HasValue)
                return null;
            if (value.Value < 1)
                throw Error(path, "must be positive.");
            return value.Value;
        }
    }
}
