using System;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        const int SupportedShipsSchemaVersion = 3;

        static ShipDefinition[] ParseShips(ShipsDto root, int[] powerUpMaxLevels)
        {
            int schemaVersion = Require(
                root.schemaVersion,
                "ships.json.schemaVersion");
            if (schemaVersion < 1
                || schemaVersion > SupportedShipsSchemaVersion)
                throw Error(
                    "ships.json.schemaVersion",
                    $"must be between 1 and {SupportedShipsSchemaVersion}, "
                    + $"but was {schemaVersion}.");

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
                if (startingLevels.Length != 4
                    && startingLevels.Length != PowerUpGauge.SlotCount)
                    throw Error(
                        path + ".startingPowerUpLevels",
                        $"must contain exactly 4 or "
                        + $"{PowerUpGauge.SlotCount} entries.");
                var levelCopy =
                    new int[PowerUpGauge.SlotCount];
                Array.Copy(
                    startingLevels,
                    levelCopy,
                    startingLevels.Length);
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
                PrimaryWeaponFamily? gaugeWeaponFamily =
                    ParseGaugeWeaponFamily(
                        item.gaugeWeaponFamily,
                        path + ".gaugeWeaponFamily");
                PowerUpSlot[] gaugeSlots = ParseShipGaugeSlots(
                    item.powerUpGaugeSlots,
                    gaugeWeaponFamily,
                    path + ".powerUpGaugeSlots");
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
                    ParseStartingShieldStock(
                        item.startingShieldStock,
                        path + ".startingShieldStock",
                        allowZero: true)
                        ?? ParseStartingShieldStock(
                            item.maxHp,
                            path + ".maxHp",
                            allowZero: false),
                    gaugeWeaponFamily,
                    gaugeSlots,
                    schemaVersion >= SupportedShipsSchemaVersion
                        ? ParseShipMissileFamily(
                            item.missileFamily,
                            path + ".missileFamily")
                        : item.missileFamily == null
                            ? (MissileFamily?)null
                            : ParseShipMissileFamily(
                                item.missileFamily,
                                path + ".missileFamily"));
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

        static MissileFamily ParseShipMissileFamily(
            string value,
            string path)
        {
            if (value == null)
                throw Error(path, "is required.");
            return ParseMissileFamily(value, path);
        }

        static PrimaryWeaponFamily? ParseGaugeWeaponFamily(
            string value,
            string path)
        {
            if (value == null)
                return null;
            switch (RequireText(value, path))
            {
                case "double": return PrimaryWeaponFamily.Double;
                case "laser": return PrimaryWeaponFamily.Laser;
                case "triple": return PrimaryWeaponFamily.Spread;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        static PowerUpSlot[] ParseShipGaugeSlots(
            string[] values,
            PrimaryWeaponFamily? family,
            string path)
        {
            if (values == null)
            {
                if (!family.HasValue)
                    return null;
                return new[]
                {
                    PowerUpSlot.Speed,
                    PowerUpSlot.Missile,
                    ShipDefinition.GaugeSlotForFamily(family.Value),
                    PowerUpSlot.Option,
                    PowerUpSlot.Shield
                };
            }
            if (!family.HasValue)
                throw Error(
                    path,
                    "requires gaugeWeaponFamily.");
            if (values.Length != PowerUpGauge.ShipGaugeSlotCount)
                throw Error(
                    path,
                    $"must contain exactly "
                    + $"{PowerUpGauge.ShipGaugeSlotCount} entries.");

            var slots = new PowerUpSlot[values.Length];
            PowerUpSlot weaponSlot =
                ShipDefinition.GaugeSlotForFamily(family.Value);
            for (int i = 0; i < slots.Length; i++)
            {
                string itemPath = $"{path}[{i}]";
                switch (RequireText(values[i], itemPath))
                {
                    case "Speed":
                        slots[i] = PowerUpSlot.Speed;
                        break;
                    case "Missile":
                        slots[i] = PowerUpSlot.Missile;
                        break;
                    case "Weapon":
                        slots[i] = weaponSlot;
                        break;
                    case "Option":
                        slots[i] = PowerUpSlot.Option;
                        break;
                    case "Shield":
                        slots[i] = PowerUpSlot.Shield;
                        break;
                    default:
                        throw Error(
                            itemPath,
                            $"has unknown value '{values[i]}'.");
                }
            }
            return slots;
        }

        static int? ParseStartingShieldStock(
            int? value,
            string path,
            bool allowZero)
        {
            if (!value.HasValue)
                return null;
            if (value.Value < (allowZero ? 0 : 1))
                throw Error(
                    path,
                    allowZero
                        ? "cannot be negative."
                        : "must be positive.");
            return value.Value;
        }
    }
}
