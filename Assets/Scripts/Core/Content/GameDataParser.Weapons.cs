using System;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        static WeaponParseResult ParseWeapons(WeaponsDto root)
        {
            int schemaVersion = Require(root.schemaVersion, "weapons.json.schemaVersion");
            if (schemaVersion != SupportedSchemaVersion
                && schemaVersion != SupportedWeaponsSchemaVersion)
                throw Error(
                    "weapons.json.schemaVersion",
                    $"must be {SupportedSchemaVersion} or "
                    + $"{SupportedWeaponsSchemaVersion}, but was {schemaVersion}.");

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
                int fireIntervalTicks = Require(
                    item.fireIntervalTicks,
                    path + ".fireIntervalTicks");
                int minimumFireIntervalTicks =
                    item.minimumFireIntervalTicks ?? fireIntervalTicks / 2;
                if (minimumFireIntervalTicks < 0)
                    throw Error(
                        path + ".minimumFireIntervalTicks",
                        "cannot be negative.");
                var definition = new WeaponDefinition(
                    RequireText(item.id, path + ".id"),
                    slot,
                    Require(item.baseDamage, path + ".baseDamage"),
                    fireIntervalTicks,
                    speed.Numerator,
                    speed.Denominator,
                    ToSubUnits(
                        Require(item.projectileHalfWidth, path + ".projectileHalfWidth"),
                        path + ".projectileHalfWidth"),
                    ToSubUnits(
                        Require(item.projectileHalfHeight, path + ".projectileHalfHeight"),
                        path + ".projectileHalfHeight"),
                    Require(item.maxLevel, path + ".maxLevel"),
                    minimumFireIntervalTicks);

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

            MissileFamilyDefinition[] missileFamilies;
            OptionFormationDefinition[] optionFormations;
            MissileFamily defaultMissileFamily;
            OptionFormation defaultOptionFormation;
            if (schemaVersion == SupportedWeaponsSchemaVersion)
            {
                missileFamilies = ParseMissileFamilies(root);
                optionFormations = ParseOptionFormations(root);
                defaultMissileFamily = ParseMissileFamily(
                    root.defaultMissileFamily,
                    "weapons.json.defaultMissileFamily");
                defaultOptionFormation = ParseOptionFormation(
                    root.defaultOptionFormation,
                    "weapons.json.defaultOptionFormation");
            }
            else
            {
                int u = SimSpace.SubUnitsPerWorldUnit;
                missileFamilies = new[]
                {
                    new MissileFamilyDefinition(
                        MissileFamily.Straight,
                        missile.BaseDamage,
                        missile.FireIntervalTicks,
                        missile.MinimumFireIntervalTicks,
                        5,
                        missile.ProjectileSpeedNumerator,
                        missile.ProjectileSpeedDenominator,
                        5 * u,
                        SimSpace.TicksPerSecond,
                        0,
                        0,
                        0,
                        0)
                };
                optionFormations = new[]
                {
                    new OptionFormationDefinition(
                        OptionFormation.Trail,
                        12,
                        Array.Empty<int>(),
                        Array.Empty<int>(),
                        0,
                        0,
                        1)
                };
                defaultMissileFamily = MissileFamily.Straight;
                defaultOptionFormation = OptionFormation.Trail;
            }

            return new WeaponParseResult(
                definitions,
                maxLevels,
                mainShot,
                missile,
                missileFamilies,
                defaultMissileFamily,
                optionFormations,
                defaultOptionFormation);
        }

        static MissileFamilyDefinition[] ParseMissileFamilies(
            WeaponsDto root)
        {
            MissileFamilyDto[] source = RequireArray(
                root.missileFamilies,
                "weapons.json.missileFamilies");
            if (source.Length != 3)
                throw Error(
                    "weapons.json.missileFamilies",
                    "must contain straight, spread_bomb, and piercing_lance.");
            var definitions = new MissileFamilyDefinition[source.Length];
            var seen = new bool[3];
            for (int i = 0; i < source.Length; i++)
            {
                string path = $"weapons.json.missileFamilies[{i}]";
                MissileFamilyDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");
                MissileFamily family = ParseMissileFamily(
                    item.id,
                    path + ".id");
                if (seen[(int)family])
                    throw Error(path + ".id", $"duplicates '{item.id}'.");
                seen[(int)family] = true;
                ExactFraction speed = ToPerTickSpeed(
                    Require(item.projectileSpeed, path + ".projectileSpeed"),
                    path + ".projectileSpeed");
                ExactFraction fall = ToPerTickSpeed(
                    Require(item.fallSpeedY, path + ".fallSpeedY"),
                    path + ".fallSpeedY");
                definitions[i] = new MissileFamilyDefinition(
                    family,
                    Require(item.baseDamage, path + ".baseDamage"),
                    Require(
                        item.fireIntervalTicks,
                        path + ".fireIntervalTicks"),
                    Require(
                        item.minimumFireIntervalTicks,
                        path + ".minimumFireIntervalTicks"),
                    Require(
                        item.fireIntervalReductionPerLevel,
                        path + ".fireIntervalReductionPerLevel"),
                    speed.Numerator,
                    speed.Denominator,
                    fall.Numerator,
                    fall.Denominator,
                    Require(
                        item.pierceEnemyCount,
                        path + ".pierceEnemyCount"),
                    Require(
                        item.explosionDamage,
                        path + ".explosionDamage"),
                    ToSubUnits(
                        Require(
                            item.explosionRadius,
                            path + ".explosionRadius"),
                        path + ".explosionRadius"),
                    Require(
                        item.explosionMaxTargets,
                        path + ".explosionMaxTargets"));
            }
            return definitions;
        }

        static OptionFormationDefinition[] ParseOptionFormations(
            WeaponsDto root)
        {
            OptionFormationDto[] source = RequireArray(
                root.optionFormations,
                "weapons.json.optionFormations");
            if (source.Length != 3)
                throw Error(
                    "weapons.json.optionFormations",
                    "must contain trail, fixed, and orbit.");
            var definitions =
                new OptionFormationDefinition[source.Length];
            var seen = new bool[3];
            for (int i = 0; i < source.Length; i++)
            {
                string path = $"weapons.json.optionFormations[{i}]";
                OptionFormationDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");
                OptionFormation formation = ParseOptionFormation(
                    item.id,
                    path + ".id");
                if (seen[(int)formation])
                    throw Error(path + ".id", $"duplicates '{item.id}'.");
                seen[(int)formation] = true;

                int[] offsetXs = Array.Empty<int>();
                int[] offsetYs = Array.Empty<int>();
                if (formation == OptionFormation.Fixed)
                {
                    OptionOffsetDto[] offsets = RequireArray(
                        item.offsets,
                        path + ".offsets");
                    if (offsets.Length != 4)
                        throw Error(
                            path + ".offsets",
                            "must contain exactly four offsets.");
                    offsetXs = new int[offsets.Length];
                    offsetYs = new int[offsets.Length];
                    for (int offset = 0; offset < offsets.Length; offset++)
                    {
                        string offsetPath =
                            $"{path}.offsets[{offset}]";
                        if (offsets[offset] == null)
                            throw Error(offsetPath, "cannot be null.");
                        offsetXs[offset] = ToSubUnits(
                            Require(
                                offsets[offset].x,
                                offsetPath + ".x"),
                            offsetPath + ".x");
                        offsetYs[offset] = ToSubUnits(
                            Require(
                                offsets[offset].y,
                                offsetPath + ".y"),
                            offsetPath + ".y");
                    }
                }
                else if (item.offsets != null)
                {
                    throw Error(
                        path + ".offsets",
                        "is only valid for fixed formation.");
                }

                int followDelay = formation == OptionFormation.Trail
                    ? Require(
                        item.followDelayTicks,
                        path + ".followDelayTicks")
                    : item.followDelayTicks ?? 0;
                int radius = formation == OptionFormation.Orbit
                    ? ToSubUnits(
                        Require(item.radius, path + ".radius"),
                        path + ".radius")
                    : item.radius.HasValue
                        ? ToSubUnits(item.radius.Value, path + ".radius")
                        : 0;
                int angularNumerator =
                    formation == OptionFormation.Orbit
                        ? Require(
                            item.angularLutSlotsNumerator,
                            path + ".angularLutSlotsNumerator")
                        : item.angularLutSlotsNumerator ?? 0;
                int angularDenominator =
                    formation == OptionFormation.Orbit
                        ? Require(
                            item.angularLutSlotsDenominator,
                            path + ".angularLutSlotsDenominator")
                        : item.angularLutSlotsDenominator ?? 1;
                definitions[i] = new OptionFormationDefinition(
                    formation,
                    followDelay,
                    offsetXs,
                    offsetYs,
                    radius,
                    angularNumerator,
                    angularDenominator);
            }
            return definitions;
        }

        internal static MissileFamily ParseMissileFamily(
            string value,
            string path)
        {
            switch (RequireText(value, path))
            {
                case "straight": return MissileFamily.Straight;
                case "spread_bomb": return MissileFamily.SpreadBomb;
                case "piercing_lance":
                    return MissileFamily.PiercingLance;
                default:
                    throw Error(path, $"has unknown value '{value}'.");
            }
        }

        internal static OptionFormation ParseOptionFormation(
            string value,
            string path)
        {
            switch (RequireText(value, path))
            {
                case "trail": return OptionFormation.Trail;
                case "fixed": return OptionFormation.Fixed;
                case "orbit": return OptionFormation.Orbit;
                default:
                    throw Error(path, $"has unknown value '{value}'.");
            }
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
                WeaponDefinition missile,
                MissileFamilyDefinition[] missileFamilies,
                MissileFamily defaultMissileFamily,
                OptionFormationDefinition[] optionFormations,
                OptionFormation defaultOptionFormation)
            {
                Definitions = definitions;
                MaxLevels = maxLevels;
                MainShot = mainShot;
                Missile = missile;
                MissileFamilies = missileFamilies;
                DefaultMissileFamily = defaultMissileFamily;
                OptionFormations = optionFormations;
                DefaultOptionFormation = defaultOptionFormation;
            }

            public WeaponDefinition[] Definitions { get; }
            public int[] MaxLevels { get; }
            public WeaponDefinition MainShot { get; }
            public WeaponDefinition Missile { get; }
            public MissileFamilyDefinition[] MissileFamilies { get; }
            public MissileFamily DefaultMissileFamily { get; }
            public OptionFormationDefinition[] OptionFormations { get; }
            public OptionFormation DefaultOptionFormation { get; }
        }
    }
}
