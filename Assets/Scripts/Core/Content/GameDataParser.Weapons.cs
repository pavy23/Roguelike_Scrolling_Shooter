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
                && schemaVersion != SupportedWeaponsSchemaVersion
                && schemaVersion != SupportedPrimaryWeaponsSchemaVersion
                && schemaVersion != SupportedPowerUpCurveSchemaVersion
                && schemaVersion != SupportedPowerUpGaugeSchemaVersion
                && schemaVersion != SupportedReq080WeaponsSchemaVersion)
                throw Error(
                    "weapons.json.schemaVersion",
                    $"must be {SupportedSchemaVersion}, "
                    + $"{SupportedWeaponsSchemaVersion}, or "
                    + $"{SupportedPrimaryWeaponsSchemaVersion}, "
                    + $"{SupportedPowerUpCurveSchemaVersion}, or "
                    + $"{SupportedPowerUpGaugeSchemaVersion}, or "
                    + $"{SupportedReq080WeaponsSchemaVersion}, "
                    + $"but was {schemaVersion}.");

            WeaponDto[] source = RequireArray(root.weapons, "weapons.json.weapons");
            var definitions = new WeaponDefinition[source.Length];
            var maxLevels = new int[PowerUpGauge.SlotCount];
            var seenSlots = new bool[4];
            WeaponDefinition mainShot = null;
            WeaponDefinition missile = null;

            for (int i = 0; i < source.Length; i++)
            {
                string path = $"weapons.json.weapons[{i}]";
                WeaponDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");

                PowerUpSlot slot = ParsePowerUpSlot(item.slot, path + ".slot");
                if ((int)slot >= seenSlots.Length)
                    throw Error(
                        path + ".slot",
                        "selectable gauge slots are declared in powerUpGauge.slots, "
                        + "not weapons.");
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
                    minimumFireIntervalTicks,
                    ParseEffectSoftCapLevel(
                        item,
                        slot,
                        schemaVersion,
                        path));

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
            PrimaryWeaponFamilyDefinition[] primaryWeaponFamilies;
            OptionFormationDefinition[] optionFormations;
            MissileFamily defaultMissileFamily;
            OptionFormation defaultOptionFormation;
            if (schemaVersion >= SupportedWeaponsSchemaVersion)
            {
                missileFamilies = ParseMissileFamilies(
                    root,
                    schemaVersion);
                optionFormations = ParseOptionFormations(root);
                defaultMissileFamily = ParseMissileFamily(
                    root.defaultMissileFamily,
                    "weapons.json.defaultMissileFamily");
                defaultOptionFormation = ParseOptionFormation(
                    root.defaultOptionFormation,
                    "weapons.json.defaultOptionFormation");
                primaryWeaponFamilies =
                    schemaVersion
                >= SupportedPrimaryWeaponsSchemaVersion
                        ? CompletePrimaryWeaponFamilies(
                            ParsePrimaryWeaponFamilies(
                                root,
                                schemaVersion),
                            CreateLegacyPrimaryWeaponFamilies(mainShot))
                        : CreateLegacyPrimaryWeaponFamilies(mainShot);
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
                primaryWeaponFamilies =
                    CreateLegacyPrimaryWeaponFamilies(mainShot);
            }

            PowerUpCostCurve costCurve =
                schemaVersion >= SupportedPowerUpCurveSchemaVersion
                    ? ParsePowerUpCostCurve(root.powerUpCostCurve)
                    : PowerUpCostCurve.CreateProvisional();
            PowerUpSlotDefinition[] gaugeSlots =
                schemaVersion >= SupportedPowerUpGaugeSchemaVersion
                    ? ParsePowerUpGauge(root.powerUpGauge)
                    : CreateLegacyPowerUpGauge(
                        maxLevels,
                        costCurve);
            for (int i = 0; i < gaugeSlots.Length; i++)
                maxLevels[(int)gaugeSlots[i].Slot] =
                    gaugeSlots[i].MaxLevel;

            return new WeaponParseResult(
                definitions,
                maxLevels,
                costCurve,
                gaugeSlots,
                mainShot,
                missile,
                primaryWeaponFamilies,
                missileFamilies,
                defaultMissileFamily,
                optionFormations,
                defaultOptionFormation);
        }

        static int ParseEffectSoftCapLevel(
            WeaponDto item,
            PowerUpSlot slot,
            int schemaVersion,
            string path)
        {
            int maxLevel =
                Require(item.maxLevel, path + ".maxLevel");
            if (schemaVersion >= SupportedPowerUpCurveSchemaVersion)
            {
                int value = Require(
                    item.effectSoftCapLevel,
                    path + ".effectSoftCapLevel");
                if (value < 1 || value > maxLevel)
                    throw Error(
                        path + ".effectSoftCapLevel",
                        "must be within 1..maxLevel.");
                return value;
            }

            int legacySoftCap;
            switch (slot)
            {
                case PowerUpSlot.MainShot: legacySoftCap = 5; break;
                case PowerUpSlot.Missile: legacySoftCap = 3; break;
                case PowerUpSlot.Option: legacySoftCap = 4; break;
                case PowerUpSlot.Shield: legacySoftCap = 3; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
            return Math.Min(maxLevel, legacySoftCap);
        }

        static PowerUpCostCurve ParsePowerUpCostCurve(
            PowerUpCostCurveDto source)
        {
            const string path = "weapons.json.powerUpCostCurve";
            if (source == null)
                throw Error(path, "is required.");
            int baseCost = Require(source.baseCost, path + ".baseCost");
            int linearGrowth = Require(
                source.linearGrowth,
                path + ".linearGrowth");
            int quadraticGrowth = Require(
                source.quadraticGrowth,
                path + ".quadraticGrowth");
            if (baseCost < 1)
                throw Error(path + ".baseCost", "must be positive.");
            if (linearGrowth < 0)
                throw Error(path + ".linearGrowth", "cannot be negative.");
            if (quadraticGrowth < 0)
                throw Error(path + ".quadraticGrowth", "cannot be negative.");
            return new PowerUpCostCurve(
                baseCost,
                linearGrowth,
                quadraticGrowth);
        }

        static PowerUpSlotDefinition[] ParsePowerUpGauge(
            PowerUpGaugeDto source)
        {
            const string path = "weapons.json.powerUpGauge";
            if (source == null)
                throw Error(path, "is required.");
            PowerUpGaugeSlotDto[] slots = RequireArray(
                source.slots,
                path + ".slots");
            if (slots.Length != PowerUpGauge.DefaultGaugeSlotCount)
                throw Error(
                    path + ".slots",
                    $"must contain exactly "
                    + $"{PowerUpGauge.DefaultGaugeSlotCount} entries.");

            var definitions =
                new PowerUpSlotDefinition[slots.Length];
            var seen = new bool[PowerUpGauge.SlotCount];
            seen[(int)PowerUpSlot.MainShot] = true;
            for (int i = 0; i < slots.Length; i++)
            {
                string slotPath = $"{path}.slots[{i}]";
                PowerUpGaugeSlotDto item = slots[i];
                if (item == null)
                    throw Error(slotPath, "cannot be null.");
                PowerUpSlot slot = ParsePowerUpSlot(
                    item.slot,
                    slotPath + ".slot");
                if (slot == PowerUpSlot.MainShot)
                    throw Error(
                        slotPath + ".slot",
                        "MainShot is a hidden shared power axis.");
                if (seen[(int)slot])
                    throw Error(
                        slotPath + ".slot",
                        $"duplicates slot '{slot}'.");
                seen[(int)slot] = true;

                ExactFraction speedBonus =
                    item.speedBonusPerLevel.HasValue
                        ? ToPerTickSpeed(
                            item.speedBonusPerLevel.Value,
                            slotPath + ".speedBonusPerLevel")
                        : new ExactFraction(0, 1);
                if (slot == PowerUpSlot.Speed
                    && speedBonus.Numerator < 1)
                    throw Error(
                        slotPath + ".speedBonusPerLevel",
                        "must be positive for Speed.");
                if (slot != PowerUpSlot.Speed
                    && speedBonus.Numerator != 0)
                    throw Error(
                        slotPath + ".speedBonusPerLevel",
                        "is only valid for Speed.");

                definitions[i] = new PowerUpSlotDefinition(
                    slot,
                    RequireText(
                        item.nameKey,
                        slotPath + ".nameKey"),
                    Require(
                        item.maxLevel,
                        slotPath + ".maxLevel"),
                    ParsePowerUpSlotCostCurve(
                        item.costCurve,
                        slotPath + ".costCurve"),
                    speedBonus.Numerator,
                    speedBonus.Denominator);
            }
            for (int i = 0; i < seen.Length; i++)
                if (!seen[i])
                    throw Error(
                        path + ".slots",
                        $"is missing slot '{(PowerUpSlot)i}'.");
            return definitions;
        }

        static PowerUpCostCurve ParsePowerUpSlotCostCurve(
            PowerUpCostCurveDto source,
            string path)
        {
            if (source == null)
                throw Error(path, "is required.");
            int baseCost = Require(
                source.baseCost,
                path + ".baseCost");
            int linearGrowth = Require(
                source.linearGrowth,
                path + ".linearGrowth");
            int quadraticGrowth = Require(
                source.quadraticGrowth,
                path + ".quadraticGrowth");
            if (baseCost < 1)
                throw Error(path + ".baseCost", "must be positive.");
            if (linearGrowth < 0)
                throw Error(
                    path + ".linearGrowth",
                    "cannot be negative.");
            if (quadraticGrowth < 0)
                throw Error(
                    path + ".quadraticGrowth",
                    "cannot be negative.");
            return new PowerUpCostCurve(
                baseCost,
                linearGrowth,
                quadraticGrowth);
        }

        static PowerUpSlotDefinition[] CreateLegacyPowerUpGauge(
            int[] maxLevels,
            PowerUpCostCurve costCurve)
        {
            return new[]
            {
                new PowerUpSlotDefinition(
                    PowerUpSlot.Speed,
                    "powerUp.speed",
                    5,
                    costCurve,
                    SimSpace.SubUnitsPerWorldUnit,
                    SimSpace.TicksPerSecond),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Missile,
                    "powerUp.missile",
                    maxLevels[(int)PowerUpSlot.Missile],
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Double,
                    "powerUp.double",
                    1,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Laser,
                    "powerUp.laser",
                    1,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Triple,
                    "powerUp.triple",
                    1,
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Option,
                    "powerUp.option",
                    maxLevels[(int)PowerUpSlot.Option],
                    costCurve),
                new PowerUpSlotDefinition(
                    PowerUpSlot.Shield,
                    "powerUp.shield",
                    maxLevels[(int)PowerUpSlot.Shield],
                    costCurve)
            };
        }

        static PrimaryWeaponFamilyDefinition[]
            ParsePrimaryWeaponFamilies(
                WeaponsDto root,
                int schemaVersion)
        {
            PrimaryWeaponFamilyDto[] source = RequireArray(
                root.primaryWeaponFamilies,
                "weapons.json.primaryWeaponFamilies");
            if (source.Length < 2 || source.Length > 4)
                throw Error(
                    "weapons.json.primaryWeaponFamilies",
                    "must contain 2 to 4 unique families including double and laser.");
            var definitions =
                new PrimaryWeaponFamilyDefinition[source.Length];
            var seen = new bool[4];
            for (int i = 0; i < source.Length; i++)
            {
                string path =
                    $"weapons.json.primaryWeaponFamilies[{i}]";
                PrimaryWeaponFamilyDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");
                PrimaryWeaponFamily family =
                    ParsePrimaryWeaponFamily(
                        item.id,
                        path + ".id");
                if (seen[(int)family])
                    throw Error(path + ".id", $"duplicates '{item.id}'.");
                seen[(int)family] = true;
                ExactFraction speed = ToPerTickSpeed(
                    Require(
                        item.projectileSpeed,
                        path + ".projectileSpeed"),
                    path + ".projectileSpeed");
                definitions[i] =
                    new PrimaryWeaponFamilyDefinition(
                        family,
                        RequireText(
                            item.displayName,
                            path + ".displayName"),
                        RequireText(
                            item.description,
                            path + ".description"),
                        ParseWeaponType(
                            RequireText(
                                item.weaponType,
                                path + ".weaponType"),
                            path + ".weaponType"),
                        Require(
                            item.baseDamage,
                            path + ".baseDamage"),
                        Require(
                            item.fireIntervalTicks,
                            path + ".fireIntervalTicks"),
                        Require(
                            item.minimumFireIntervalTicks,
                            path + ".minimumFireIntervalTicks"),
                        Require(
                            item.rapidFireStartLevel,
                            path + ".rapidFireStartLevel"),
                        Require(
                            item.fireIntervalReductionPerLevel,
                            path + ".fireIntervalReductionPerLevel"),
                        speed.Numerator,
                        speed.Denominator,
                        ToSubUnits(
                            Require(
                                item.projectileHalfWidth,
                                path + ".projectileHalfWidth"),
                            path + ".projectileHalfWidth"),
                        ToSubUnits(
                            Require(
                                item.projectileHalfHeight,
                                path + ".projectileHalfHeight"),
                            path + ".projectileHalfHeight"),
                        Require(
                            item.pierceEnemyCount,
                            path + ".pierceEnemyCount"),
                        Require(
                            item.spreadWays,
                            path + ".spreadWays"),
                        Require(
                            item.spreadStepLutSlots,
                            path + ".spreadStepLutSlots"),
                        schemaVersion
                            >= SupportedReq080WeaponsSchemaVersion
                            ? RequireArray(
                                item.shotAngleLutSlots,
                                path + ".shotAngleLutSlots",
                                allowEmpty: true)
                            : item.shotAngleLutSlots);
            }
            if (!seen[(int)PrimaryWeaponFamily.Double]
                || !seen[(int)PrimaryWeaponFamily.Laser])
                throw Error(
                    "weapons.json.primaryWeaponFamilies",
                    "must include double and laser.");
            return definitions;
        }

        static PrimaryWeaponFamilyDefinition[]
            CompletePrimaryWeaponFamilies(
                PrimaryWeaponFamilyDefinition[] configured,
                PrimaryWeaponFamilyDefinition[] fallbacks)
        {
            var present = new bool[4];
            for (int i = 0; i < configured.Length; i++)
                present[(int)configured[i].Family] = true;

            int missingCount = 0;
            for (int i = 0; i < fallbacks.Length; i++)
                if (!present[(int)fallbacks[i].Family])
                    missingCount++;
            if (missingCount == 0)
                return configured;

            var complete =
                new PrimaryWeaponFamilyDefinition[
                    configured.Length + missingCount];
            Array.Copy(configured, complete, configured.Length);
            int writeIndex = configured.Length;
            for (int i = 0; i < fallbacks.Length; i++)
            {
                PrimaryWeaponFamilyDefinition fallback = fallbacks[i];
                if (present[(int)fallback.Family])
                    continue;
                complete[writeIndex++] = fallback;
            }
            return complete;
        }

        static PrimaryWeaponFamilyDefinition[]
            CreateLegacyPrimaryWeaponFamilies(WeaponDefinition main)
        {
            int u = SimSpace.SubUnitsPerWorldUnit;
            int baseDamage = main.BaseDamage;
            int fireInterval = Math.Max(
                1,
                main.FireIntervalTicks);
            int minimumInterval =
                Math.Min(
                    fireInterval,
                    Math.Max(
                        1,
                        main.MinimumFireIntervalTicks));
            return new[]
            {
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Vulcan,
                    "Vulcan",
                    "Rapid straight fire.",
                    WeaponType.Vulcan,
                    baseDamage,
                    fireInterval,
                    minimumInterval,
                    2,
                    1,
                    main.ProjectileSpeedNumerator,
                    main.ProjectileSpeedDenominator,
                    main.ProjectileHalfWidth,
                    main.ProjectileHalfHeight,
                    0,
                    1,
                    0),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Double,
                    "Double",
                    "Two-way spread fire for wider coverage.",
                    WeaponType.Spread,
                    Math.Max(1, baseDamage * 3 / 5),
                    fireInterval + 2,
                    minimumInterval,
                    3,
                    1,
                    main.ProjectileSpeedNumerator,
                    main.ProjectileSpeedDenominator,
                    main.ProjectileHalfWidth,
                    main.ProjectileHalfHeight,
                    0,
                    2,
                    2),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Laser,
                    "Laser",
                    "Slower straight fire that pierces up to three enemies.",
                    WeaponType.Laser,
                    Math.Max(1, baseDamage * 3 / 2),
                    Math.Max(fireInterval + 3, fireInterval * 2),
                    Math.Max(fireInterval, minimumInterval),
                    2,
                    2,
                    28 * u,
                    SimSpace.TicksPerSecond,
                    Math.Max(0, main.ProjectileHalfWidth / 2),
                    Math.Max(0, main.ProjectileHalfHeight / 2),
                    2,
                    1,
                    0),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Spread,
                    "Spread",
                    "Three-way coverage fire.",
                    WeaponType.Spread,
                    Math.Max(1, baseDamage * 3 / 5),
                    fireInterval + 2,
                    minimumInterval,
                    3,
                    1,
                    main.ProjectileSpeedNumerator,
                    main.ProjectileSpeedDenominator,
                    main.ProjectileHalfWidth,
                    main.ProjectileHalfHeight,
                    0,
                    3,
                    2)
            };
        }

        static MissileFamilyDefinition[] ParseMissileFamilies(
            WeaponsDto root,
            int schemaVersion)
        {
            MissileFamilyDto[] source = RequireArray(
                root.missileFamilies,
                "weapons.json.missileFamilies");
            int requiredCount =
                schemaVersion >= SupportedReq080WeaponsSchemaVersion
                    ? 5
                    : 3;
            if (source.Length != requiredCount)
                throw Error(
                    "weapons.json.missileFamilies",
                    schemaVersion >= SupportedReq080WeaponsSchemaVersion
                        ? "must contain straight, spread_bomb, "
                            + "piercing_lance, downward_drop, and homing."
                        : "must contain straight, spread_bomb, "
                            + "and piercing_lance.");
            var definitions = new MissileFamilyDefinition[source.Length];
            var seen = new bool[5];
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
                        path + ".explosionMaxTargets"),
                    schemaVersion >= SupportedReq080WeaponsSchemaVersion
                        ? Require(
                            item.damageGrowthPercentPerLevel,
                            path + ".damageGrowthPercentPerLevel")
                        : item.damageGrowthPercentPerLevel ?? 50,
                    schemaVersion >= SupportedReq080WeaponsSchemaVersion
                        ? Require(
                            item.dropDelayTicks,
                            path + ".dropDelayTicks")
                        : item.dropDelayTicks ?? 0,
                    schemaVersion >= SupportedReq080WeaponsSchemaVersion
                        ? Require(
                            item.homingTurnLutSlotsPerTick,
                            path + ".homingTurnLutSlotsPerTick")
                        : item.homingTurnLutSlotsPerTick ?? 1);
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
                case "downward_drop":
                    return MissileFamily.DownwardDrop;
                case "homing": return MissileFamily.Homing;
                default:
                    throw Error(path, $"has unknown value '{value}'.");
            }
        }

        internal static PrimaryWeaponFamily ParsePrimaryWeaponFamily(
            string value,
            string path)
        {
            switch (RequireText(value, path))
            {
                case "vulcan": return PrimaryWeaponFamily.Vulcan;
                case "double": return PrimaryWeaponFamily.Double;
                case "laser": return PrimaryWeaponFamily.Laser;
                case "spread": return PrimaryWeaponFamily.Spread;
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
                case "Speed": return PowerUpSlot.Speed;
                case "Double": return PowerUpSlot.Double;
                case "Laser": return PowerUpSlot.Laser;
                case "Triple": return PowerUpSlot.Triple;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        internal readonly struct WeaponParseResult
        {
            public WeaponParseResult(
                WeaponDefinition[] definitions,
                int[] maxLevels,
                PowerUpCostCurve costCurve,
                PowerUpSlotDefinition[] gaugeSlots,
                WeaponDefinition mainShot,
                WeaponDefinition missile,
                PrimaryWeaponFamilyDefinition[] primaryWeaponFamilies,
                MissileFamilyDefinition[] missileFamilies,
                MissileFamily defaultMissileFamily,
                OptionFormationDefinition[] optionFormations,
                OptionFormation defaultOptionFormation)
            {
                Definitions = definitions;
                MaxLevels = maxLevels;
                CostCurve = costCurve;
                GaugeSlots = gaugeSlots;
                MainShot = mainShot;
                Missile = missile;
                PrimaryWeaponFamilies = primaryWeaponFamilies;
                MissileFamilies = missileFamilies;
                DefaultMissileFamily = defaultMissileFamily;
                OptionFormations = optionFormations;
                DefaultOptionFormation = defaultOptionFormation;
            }

            public WeaponDefinition[] Definitions { get; }
            public int[] MaxLevels { get; }
            public PowerUpCostCurve CostCurve { get; }
            public PowerUpSlotDefinition[] GaugeSlots { get; }
            public WeaponDefinition MainShot { get; }
            public WeaponDefinition Missile { get; }
            public PrimaryWeaponFamilyDefinition[]
                PrimaryWeaponFamilies { get; }
            public MissileFamilyDefinition[] MissileFamilies { get; }
            public MissileFamily DefaultMissileFamily { get; }
            public OptionFormationDefinition[] OptionFormations { get; }
            public OptionFormation DefaultOptionFormation { get; }
        }
    }
}
