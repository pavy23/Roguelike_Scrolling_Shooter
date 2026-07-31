using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    /// <summary>
    /// Unity-free parser for enemies.json schema v2/v3, weapons.json schema v2-v6,
    /// waves.json schema v2,
    /// rewards.json schema v1, optional ships.json schema v1, optional
    /// scoring.json schema v1, and optional player.json schema v1 tuning.
    /// Decimal source values are converted with decimal arithmetic only.
    /// </summary>
    public static partial class GameDataParser
    {
        public const int SupportedSchemaVersion = 2;
        public const int SupportedEnemiesSchemaVersion = 3;
        public const int SupportedWeaponsSchemaVersion = 3;
        public const int SupportedPrimaryWeaponsSchemaVersion = 4;
        public const int SupportedPowerUpCurveSchemaVersion = 5;
        public const int SupportedPowerUpGaugeSchemaVersion = 6;

        public static GameDataSet Parse(
            string enemiesJson,
            string weaponsJson,
            string wavesJson)
        {
            return Parse(enemiesJson, weaponsJson, wavesJson, null, null);
        }

        public static GameDataSet Parse(
            string enemiesJson,
            string weaponsJson,
            string wavesJson,
            string rewardsJson)
        {
            return Parse(
                enemiesJson,
                weaponsJson,
                wavesJson,
                rewardsJson,
                null);
        }

        public static GameDataSet Parse(
            string enemiesJson,
            string weaponsJson,
            string wavesJson,
            string rewardsJson,
            string shipsJson)
        {
            return Parse(
                enemiesJson,
                weaponsJson,
                wavesJson,
                rewardsJson,
                shipsJson,
                null);
        }

        public static GameDataSet Parse(
            string enemiesJson,
            string weaponsJson,
            string wavesJson,
            string rewardsJson,
            string shipsJson,
            string scoringJson = null)
        {
            return Parse(
                enemiesJson,
                weaponsJson,
                wavesJson,
                rewardsJson,
                shipsJson,
                scoringJson,
                null);
        }

        public static GameDataSet Parse(
            string enemiesJson,
            string weaponsJson,
            string wavesJson,
            string rewardsJson,
            string shipsJson,
            string scoringJson,
            string playerJson)
        {
            try
            {
                EnemiesParseResult enemies = ParseEnemies(
                    Deserialize<EnemiesDto>(enemiesJson, "enemies.json"));
                WeaponParseResult weapons = ParseWeapons(
                    Deserialize<WeaponsDto>(weaponsJson, "weapons.json"));
                var content = new BattleContent(
                    enemies.Definitions,
                    weapons.Definitions,
                    weapons.MainShot.Id,
                    weapons.PrimaryWeaponFamilies,
                    weapons.MissileFamilies,
                    weapons.DefaultMissileFamily,
                    weapons.OptionFormations,
                    weapons.DefaultOptionFormation);
                WavesParseResult waves = ParseWaves(
                    Deserialize<WavesDto>(wavesJson, "waves.json"),
                    content);
                RewardCatalog rewards = rewardsJson == null
                    ? null
                    : ParseRewards(
                        Deserialize<RewardsDto>(
                            rewardsJson,
                            "rewards.json"),
                        content);
                ShipDefinition[] ships = shipsJson == null
                    ? new[] { ShipDefinition.CreateDefault() }
                    : ParseShips(
                        Deserialize<ShipsDto>(shipsJson, "ships.json"),
                        weapons.MaxLevels);
                ScoringDefinition scoring = scoringJson == null
                    ? null
                    : ParseScoring(
                        Deserialize<ScoringDto>(scoringJson, "scoring.json"));
                int maxEnemyBullets = playerJson == null
                    ? BattleSimConfig.DefaultMaxEnemyBullets
                    : ParsePlayer(
                        Deserialize<PlayerRootDto>(playerJson, "player.json"));

                return new GameDataSet(
                    content,
                    waves.Catalog,
                    enemies.NoDropWeight,
                    enemies.BombNoDropWeight,
                    waves.ScrollSpeed.Numerator,
                    waves.ScrollSpeed.Denominator,
                maxEnemyBullets,
                    weapons.MaxLevels,
                    weapons.CostCurve,
                    weapons.GaugeSlots,
                    weapons.Missile,
                    rewards,
                    waves.Contracts,
                    ships,
                    scoring);
            }
            catch (GameDataParseException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is ArgumentException
                || ex is OverflowException
                || ex is InvalidOperationException)
            {
                throw new GameDataParseException(
                    "GameData schema validation failed.", ex);
            }
        }

        static int ParsePlayer(PlayerRootDto root)
        {
            const int supportedSchemaVersion = 1;
            int schemaVersion = Require(
                root.schemaVersion,
                "player.json.schemaVersion");
            if (schemaVersion != supportedSchemaVersion)
                throw Error(
                    "player.json.schemaVersion",
                    $"must be {supportedSchemaVersion}, but was {schemaVersion}.");
            if (root.player == null)
                throw Error("player.json.player", "is required.");

            int maxEnemyBullets = root.player.maxEnemyBullets
                ?? BattleSimConfig.DefaultMaxEnemyBullets;
            if (maxEnemyBullets < 0)
                throw Error(
                    "player.json.player.maxEnemyBullets",
                    "cannot be negative.");
            return maxEnemyBullets;
        }

        static T Deserialize<T>(string json, string fileName)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw Error(fileName, "cannot be null, empty, or whitespace.");

            try
            {
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
                using (var stream = new MemoryStream(bytes, false))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    object value = serializer.ReadObject(stream);
                    if (value == null)
                        throw Error(fileName, "must contain a JSON object.");
                    return (T)value;
                }
            }
            catch (GameDataParseException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is SerializationException
                || ex is InvalidDataContractException
                || ex is XmlException
                || ex is DecoderFallbackException)
            {
                throw Error(fileName, "is not valid schema JSON.", ex);
            }
        }

        static EnemiesParseResult ParseEnemies(EnemiesDto root)
        {
            int schemaVersion = Require(root.schemaVersion, "enemies.json.schemaVersion");
            if (schemaVersion != SupportedSchemaVersion
                && schemaVersion != SupportedEnemiesSchemaVersion)
                throw Error(
                    "enemies.json.schemaVersion",
                    $"must be {SupportedSchemaVersion} or {SupportedEnemiesSchemaVersion}, but was {schemaVersion}.");
            if (root.dropTable == null)
                throw Error("enemies.json.dropTable", "is required.");
            int noDropWeight = Require(
                root.dropTable.noDropWeight,
                "enemies.json.dropTable.noDropWeight");
            if (noDropWeight < 0)
                throw Error("enemies.json.dropTable.noDropWeight", "cannot be negative.");
            int bombNoDropWeight =
                root.dropTable.bombNoDropWeight ?? 100;
            if (bombNoDropWeight < 0)
                throw Error(
                    "enemies.json.dropTable.bombNoDropWeight",
                    "cannot be negative.");

            EnemyDto[] source = RequireArray(root.enemies, "enemies.json.enemies");
            var definitions = new EnemyDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                definitions[i] = ParseEnemy(source[i], i, schemaVersion);
                if ((long)noDropWeight + definitions[i].DropWeight > int.MaxValue)
                    throw Error(
                        $"enemies.json.enemies[{i}].dropWeight",
                        "makes the drop-table total exceed the integer range.");
                if ((long)bombNoDropWeight
                    + definitions[i].BombDropWeight > int.MaxValue)
                    throw Error(
                        $"enemies.json.enemies[{i}].bombDropWeight",
                        "makes the bomb drop-table total exceed the integer range.");
            }
            return new EnemiesParseResult(
                definitions,
                noDropWeight,
                bombNoDropWeight);
        }

        static EnemyDefinition ParseEnemy(EnemyDto source, int index, int schemaVersion)
        {
            string path = $"enemies.json.enemies[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");

            EnemyMovementParseResult movement = schemaVersion == SupportedSchemaVersion
                ? ParseLegacyMovement(source, path)
                : ParseMovement(source.movement, path + ".movement");

            return new EnemyDefinition(
                RequireText(source.id, path + ".id"),
                RequireText(source.displayName, path + ".displayName"),
                Require(source.hp, path + ".hp"),
                Require(source.contactDamage, path + ".contactDamage"),
                Require(source.scoreValue, path + ".scoreValue"),
                movement.Pattern,
                movement.Speed.Numerator,
                movement.Speed.Denominator,
                Require(source.fireIntervalTicks, path + ".fireIntervalTicks"),
                ToSubUnits(Require(source.halfWidth, path + ".halfWidth"), path + ".halfWidth"),
                ToSubUnits(Require(source.halfHeight, path + ".halfHeight"), path + ".halfHeight"),
                Require(source.dropWeight, path + ".dropWeight"),
                movement.Amplitude.Numerator,
                movement.Amplitude.Denominator,
                movement.PeriodTicks,
                movement.DelayTicks,
                movement.DurationTicks,
                movement.PauseTicks,
                source.bombDropWeight ?? 0,
                ParseLaser(
                    source.laser,
                    path + ".laser"),
                ParseMidBossProfile(
                    source.midBoss,
                    path + ".midBoss"));
        }

        static MidBossProfile ParseMidBossProfile(
            MidBossProfileDto source,
            string path)
        {
            if (source == null)
                return null;
            if (source.phases == null
                || source.phases.Length < 2
                || source.phases.Length > 3)
            {
                throw Error(
                    path + ".phases",
                    "must contain two or three phases.");
            }
            var phases = new BossPhase[source.phases.Length];
            for (int i = 0; i < phases.Length; i++)
            {
                string phasePath = $"{path}.phases[{i}]";
                phases[i] = ParseBossPhase(
                    source.phases[i],
                    phasePath);
                if (phases[i].DurationTicks < 1)
                    throw Error(
                        phasePath + ".durationTicks",
                        "must be positive for a mid-boss pattern.");
            }
            try
            {
                return new MidBossProfile(
                    RequireText(source.themeId, path + ".themeId"),
                    source.weight ?? 1,
                    source.stageIndexMin ?? 1,
                    source.stageIndexMax ?? int.MaxValue,
                    phases);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static LaserAttackDefinition ParseLaser(
            LaserAttackDto source,
            string path)
        {
            if (source == null)
                return null;
            return new LaserAttackDefinition(
                Require(
                    source.cycleIntervalTicks,
                    path + ".cycleIntervalTicks"),
                Require(
                    source.telegraphTicks,
                    path + ".telegraphTicks"),
                Require(
                    source.firingTicks,
                    path + ".firingTicks"),
                Require(
                    source.sustainTicks,
                    path + ".sustainTicks"),
                Require(
                    source.dissipateTicks,
                    path + ".dissipateTicks"),
                ToSubUnits(
                    Require(source.startOffsetX, path + ".startOffsetX"),
                    path + ".startOffsetX"),
                ToSubUnits(
                    Require(source.startOffsetY, path + ".startOffsetY"),
                    path + ".startOffsetY"),
                ToSubUnits(
                    Require(source.endOffsetX, path + ".endOffsetX"),
                    path + ".endOffsetX"),
                ToSubUnits(
                    Require(source.endOffsetY, path + ".endOffsetY"),
                    path + ".endOffsetY"),
                ToSubUnits(
                    Require(source.thinHalfWidth, path + ".thinHalfWidth"),
                    path + ".thinHalfWidth"),
                ToSubUnits(
                    Require(source.fullHalfWidth, path + ".fullHalfWidth"),
                    path + ".fullHalfWidth"),
                Require(source.damage, path + ".damage"));
        }

        static EnemyMovementParseResult ParseLegacyMovement(EnemyDto source, string path)
        {
            if (source.movement != null)
                throw Error(
                    path + ".movement",
                    $"requires enemies.json.schemaVersion {SupportedEnemiesSchemaVersion}.");

            ExactFraction speed = ToPerTickSpeed(
                Require(source.moveSpeed, path + ".moveSpeed"),
                path + ".moveSpeed");
            ExactFraction amplitude = ToSubUnitFraction(
                Require(source.amplitude, path + ".amplitude"),
                path + ".amplitude");
            if (amplitude.Numerator < 0)
                throw Error(path + ".amplitude", "cannot be negative.");

            return new EnemyMovementParseResult(
                ParseMovePattern(source.movePattern, path + ".movePattern"),
                speed,
                amplitude,
                Require(source.periodTicks, path + ".periodTicks"),
                0,
                1,
                0);
        }

        static EnemyMovementParseResult ParseMovement(
            EnemyMovementDto source,
            string path)
        {
            if (source == null)
                throw Error(path, "is required.");

            EnemyMovePattern pattern = ParseMovePattern(
                source.pattern,
                path + ".pattern");
            decimal speedValue = pattern == EnemyMovePattern.Static
                ? source.speed ?? 0m
                : Require(source.speed, path + ".speed");
            ExactFraction speed = ToPerTickSpeed(speedValue, path + ".speed");

            bool usesWave = pattern == EnemyMovePattern.Sine
                || pattern == EnemyMovePattern.Zigzag;
            decimal amplitudeValue = usesWave
                ? Require(source.amplitude, path + ".amplitude")
                : source.amplitude ?? 0m;
            ExactFraction amplitude = ToSubUnitFraction(
                amplitudeValue,
                path + ".amplitude");
            if (amplitude.Numerator < 0)
                throw Error(path + ".amplitude", "cannot be negative.");

            int periodTicks = usesWave
                ? Require(source.periodTicks, path + ".periodTicks")
                : source.periodTicks ?? 1;
            if (periodTicks < 1)
                throw Error(path + ".periodTicks", "must be at least 1.");

            int delayTicks = pattern == EnemyMovePattern.Dive
                ? Require(source.delayTicks, path + ".delayTicks")
                : source.delayTicks ?? 0;
            if (delayTicks < 0)
                throw Error(path + ".delayTicks", "cannot be negative.");

            bool usesDuration = pattern == EnemyMovePattern.Dive
                || pattern == EnemyMovePattern.Dash;
            int durationTicks = usesDuration
                ? Require(source.durationTicks, path + ".durationTicks")
                : source.durationTicks ?? 1;
            if (durationTicks < 1)
                throw Error(path + ".durationTicks", "must be at least 1.");

            int pauseTicks = pattern == EnemyMovePattern.Dash
                ? Require(source.pauseTicks, path + ".pauseTicks")
                : source.pauseTicks ?? 0;
            if (pauseTicks < 0)
                throw Error(path + ".pauseTicks", "cannot be negative.");
            if (pattern == EnemyMovePattern.Dash && pauseTicks < 1)
                throw Error(path + ".pauseTicks", "must be at least 1.");

            return new EnemyMovementParseResult(
                pattern,
                speed,
                amplitude,
                periodTicks,
                delayTicks,
                durationTicks,
                pauseTicks);
        }

        static EnemyMovePattern ParseMovePattern(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "straight": return EnemyMovePattern.Straight;
                case "sine": return EnemyMovePattern.Sine;
                case "static": return EnemyMovePattern.Static;
                case "dive": return EnemyMovePattern.Dive;
                case "zigzag": return EnemyMovePattern.Zigzag;
                case "dash": return EnemyMovePattern.Dash;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        static RewardCatalog ParseRewards(
            RewardsDto root,
            BattleContent content)
        {
            const int supportedRewardsSchemaVersion = 5;
            int schemaVersion = Require(
                root.schemaVersion,
                "rewards.json.schemaVersion");
            if (schemaVersion < 1
                || schemaVersion > supportedRewardsSchemaVersion)
                throw Error(
                    "rewards.json.schemaVersion",
                    $"must be 1..{supportedRewardsSchemaVersion}, "
                    + $"but was {schemaVersion}.");

            int optionCount = Require(root.optionCount, "rewards.json.optionCount");
            if (optionCount != RunManager.RewardOptionCount)
                throw Error(
                    "rewards.json.optionCount",
                    $"must be {RunManager.RewardOptionCount}, but was {optionCount}.");

            RewardDto[] source = RequireArray(root.rewards, "rewards.json.rewards");
            if (source.Length < optionCount)
                throw Error(
                    "rewards.json.rewards",
                    $"must contain at least {optionCount} rewards.");

            var definitions = new RewardDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                definitions[i] = ParseReward(
                    source[i],
                    i,
                    schemaVersion);
                if (definitions[i].Type
                        == RewardType.MissileFamily
                    && content.FindMissileFamily(
                        definitions[i].MissileFamily) == null)
                {
                    throw Error(
                        $"rewards.json.rewards[{i}].familyId",
                        "references a missile family missing from weapons.json.");
                }
                if (definitions[i].Type
                        == RewardType.OptionFormation
                    && content.FindOptionFormation(
                        definitions[i].OptionFormation) == null)
                {
                    throw Error(
                        $"rewards.json.rewards[{i}].formationId",
                        "references an option formation missing from weapons.json.");
                }
                if (definitions[i].Type
                        == RewardType.PrimaryWeaponFamily
                    && content.FindPrimaryWeaponFamily(
                        definitions[i].PrimaryWeaponFamily) == null)
                {
                    throw Error(
                        $"rewards.json.rewards[{i}].primaryFamilyId",
                        "references a primary weapon family missing from weapons.json.");
                }
                for (int previous = 0; previous < i; previous++)
                {
                    if (definitions[previous].Id == definitions[i].Id)
                        throw Error(
                            $"rewards.json.rewards[{i}].id",
                            $"duplicates id '{definitions[i].Id}'.");
                }
            }
            int maxCombinedModifierCost =
                schemaVersion >= 3
                    ? Require(
                        root.maxCombinedModifierCost,
                        "rewards.json.maxCombinedModifierCost")
                    : 4;
            if (maxCombinedModifierCost < 1)
                throw Error(
                    "rewards.json.maxCombinedModifierCost",
                    "must be positive.");
            int rerollCost =
                schemaVersion >= 5
                    ? Require(
                        root.rerollCost,
                        "rewards.json.rerollCost")
                    : 5;
            if (rerollCost < 1)
                throw Error(
                    "rewards.json.rerollCost",
                    "must be positive.");
            return new RewardCatalog(
                optionCount,
                definitions,
                maxCombinedModifierCost,
                rerollCost);
        }

        static ScoringDefinition ParseScoring(ScoringDto root)
        {
            const int supportedScoringSchemaVersion = 1;
            int schemaVersion = Require(
                root.schemaVersion,
                "scoring.json.schemaVersion");
            if (schemaVersion != supportedScoringSchemaVersion)
                throw Error(
                    "scoring.json.schemaVersion",
                    $"must be {supportedScoringSchemaVersion}, but was {schemaVersion}.");

            int grazeRadiusSubUnits = Require(
                root.grazeRadiusSubUnits,
                "scoring.json.grazeRadiusSubUnits");
            if (grazeRadiusSubUnits < 0)
                throw Error(
                    "scoring.json.grazeRadiusSubUnits",
                    "cannot be negative.");

            int grazeScore = Require(root.grazeScore, "scoring.json.grazeScore");
            if (grazeScore < 0)
                throw Error("scoring.json.grazeScore", "cannot be negative.");

            int grazeGaugeCharge = Require(
                root.grazeGaugeCharge,
                "scoring.json.grazeGaugeCharge");
            if (grazeGaugeCharge < 0)
                throw Error(
                    "scoring.json.grazeGaugeCharge",
                    "cannot be negative.");

            int[] requirements = RequireArray(
                root.multiplierGaugeRequirements,
                "scoring.json.multiplierGaugeRequirements",
                allowEmpty: true);
            if (requirements.Length != ScoringDefinition.MultiplierRequirementCount)
                throw Error(
                    "scoring.json.multiplierGaugeRequirements",
                    $"must contain exactly {ScoringDefinition.MultiplierRequirementCount} entries.");
            var requirementCopy = (int[])requirements.Clone();
            for (int i = 0; i < requirementCopy.Length; i++)
            {
                if (requirementCopy[i] < 1)
                    throw Error(
                        $"scoring.json.multiplierGaugeRequirements[{i}]",
                        "must be positive.");
            }

            int multiplierDecayTicks = Require(
                root.multiplierDecayTicks,
                "scoring.json.multiplierDecayTicks");
            if (multiplierDecayTicks < 1)
                throw Error(
                    "scoring.json.multiplierDecayTicks",
                    "must be positive.");

            return new ScoringDefinition(
                grazeRadiusSubUnits,
                grazeScore,
                grazeGaugeCharge,
                requirementCopy,
                multiplierDecayTicks);
        }

        static RewardDefinition ParseReward(
            RewardDto source,
            int index,
            int schemaVersion)
        {
            string path = $"rewards.json.rewards[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");

            RewardType type = ParseRewardType(source.type, path + ".type");
            PowerUpSlot slot = PowerUpSlot.MainShot;
            BattleModifier modifierId = BattleModifier.None;
            string modifierKey = null;
            bool modifierStackable = false;
            int modifierMaxStacks = 1;
            int modifierStackStrength = 1;
            int modifierInteractionCost = 1;
            MissileFamily missileFamily =
                MissileFamily.Straight;
            OptionFormation optionFormation =
                OptionFormation.Trail;
            PrimaryWeaponFamily primaryWeaponFamily =
                PrimaryWeaponFamily.Vulcan;
            RewardPool pool = schemaVersion >= 4
                ? ParseRewardPool(source.pool, path + ".pool")
                : RewardPool.Both;
            RewardCostDefinition[] costs = schemaVersion >= 4
                ? ParseRewardCosts(source.costs, path + ".costs")
                : Array.Empty<RewardCostDefinition>();
            if (type == RewardType.SlotLevel)
            {
                slot = ParsePowerUpSlot(source.slot, path + ".slot");
            }
            else if (source.slot != null)
            {
                throw Error(path + ".slot", "is only valid for slotLevel rewards.");
            }

            if (type == RewardType.Modifier)
            {
                modifierKey = RequireText(
                    source.modifierId,
                    path + ".modifierId");
                modifierId = schemaVersion >= 3
                    && source.modifierEffect != null
                        ? ParseModifierId(
                            source.modifierEffect,
                            path + ".modifierEffect")
                        : ParseModifierId(
                            modifierKey,
                            path + ".modifierId");
                if (schemaVersion >= 3)
                {
                    if (!source.stackable.HasValue)
                        throw Error(
                            path + ".stackable",
                            "is required.");
                    modifierStackable = source.stackable.Value;
                    modifierMaxStacks = Require(
                        source.maxStacks,
                        path + ".maxStacks");
                    modifierStackStrength = Require(
                        source.stackStrength,
                        path + ".stackStrength");
                    modifierInteractionCost = Require(
                        source.interactionCost,
                        path + ".interactionCost");
                    if (modifierMaxStacks < 1)
                        throw Error(
                            path + ".maxStacks",
                            "must be positive.");
                    if (!modifierStackable && modifierMaxStacks != 1)
                        throw Error(
                            path + ".maxStacks",
                            "must be 1 when stackable is false.");
                    if (modifierStackStrength < 1)
                        throw Error(
                            path + ".stackStrength",
                            "must be positive.");
                    if (modifierInteractionCost < 1)
                        throw Error(
                            path + ".interactionCost",
                            "must be positive.");
                }
            }
            else if (source.modifierId != null)
            {
                throw Error(
                    path + ".modifierId",
                    "is only valid for modifier rewards.");
            }

            if (type == RewardType.MissileFamily)
            {
                missileFamily = ParseMissileFamily(
                    source.familyId,
                    path + ".familyId");
            }
            else if (source.familyId != null)
            {
                throw Error(
                    path + ".familyId",
                    "is only valid for missileFamily rewards.");
            }
            if (type == RewardType.OptionFormation)
            {
                optionFormation = ParseOptionFormation(
                    source.formationId,
                    path + ".formationId");
            }
            else if (source.formationId != null)
            {
                throw Error(
                    path + ".formationId",
                    "is only valid for optionFormation rewards.");
            }
            if (type == RewardType.PrimaryWeaponFamily)
            {
                primaryWeaponFamily = ParsePrimaryWeaponFamily(
                    source.primaryFamilyId,
                    path + ".primaryFamilyId");
            }
            else if (source.primaryFamilyId != null)
            {
                throw Error(
                    path + ".primaryFamilyId",
                    "is only valid for primaryWeaponFamily rewards.");
            }

            bool amountOptional =
                type == RewardType.Modifier
                || type == RewardType.MissileFamily
                || type == RewardType.OptionFormation
                || type == RewardType.PrimaryWeaponFamily;
            int amount = amountOptional && !source.amount.HasValue
                ? 1
                : Require(source.amount, path + ".amount");
            if (amount < 1)
                throw Error(path + ".amount", "must be positive.");
            int weight = Require(source.weight, path + ".weight");
            if (weight < 1)
                throw Error(path + ".weight", "must be positive.");
            int stageIndexMin = Require(
                source.stageIndexMin,
                path + ".stageIndexMin");
            int stageIndexMax = Require(
                source.stageIndexMax,
                path + ".stageIndexMax");
            if (stageIndexMin < 1)
                throw Error(path + ".stageIndexMin", "must be positive.");
            if (stageIndexMax < stageIndexMin)
                throw Error(
                    path + ".stageIndexMax",
                    "cannot be less than stageIndexMin.");
            if (source.maxPerRun.HasValue && source.maxPerRun.Value < 1)
                throw Error(path + ".maxPerRun", "must be positive when present.");

            return new RewardDefinition(
                RequireText(source.id, path + ".id"),
                type,
                slot,
                amount,
                weight,
                stageIndexMin,
                stageIndexMax,
                source.maxPerRun,
                modifierId,
                missileFamily,
                optionFormation,
                primaryWeaponFamily,
                modifierKey,
                modifierStackable,
                modifierMaxStacks,
                modifierStackStrength,
                modifierInteractionCost,
                pool,
                costs);
        }

        static RewardPool ParseRewardPool(string value, string path)
        {
            if (value == null || value == "both")
                return RewardPool.Both;
            switch (RequireText(value, path))
            {
                case "mid": return RewardPool.Mid;
                case "main": return RewardPool.Main;
                default:
                    throw Error(
                        path,
                        "must be 'mid', 'main', or 'both'.");
            }
        }

        static RewardCostDefinition[] ParseRewardCosts(
            RewardCostDto[] source,
            string path)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<RewardCostDefinition>();
            var result = new RewardCostDefinition[source.Length];
            for (int i = 0; i < result.Length; i++)
            {
                string itemPath = $"{path}[{i}]";
                RewardCostDto item = source[i];
                if (item == null)
                    throw Error(itemPath, "cannot be null.");
                RewardEffectType type;
                switch (RequireText(item.type, itemPath + ".type"))
                {
                    case "shieldMaxDown":
                        type = RewardEffectType.ShieldMaxDown;
                        break;
                    case "moveSpeedDown":
                        type = RewardEffectType.MoveSpeedDown;
                        break;
                    case "capsuleDropWeightDown":
                        type = RewardEffectType.CapsuleDropWeightDown;
                        break;
                    case "bombMaxDown":
                        type = RewardEffectType.BombMaxDown;
                        break;
                    default:
                        throw Error(
                            itemPath + ".type",
                            "has an unknown reward cost type.");
                }
                int amount = Require(item.amount, itemPath + ".amount");
                if (amount < 1)
                    throw Error(
                        itemPath + ".amount",
                        "must be positive.");
                result[i] = new RewardCostDefinition(type, amount);
            }
            return result;
        }

        static RewardType ParseRewardType(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "capsules": return RewardType.Capsules;
                case "slotLevel": return RewardType.SlotLevel;
                case "repairHp":
                case "shieldStock":
                    return RewardType.ShieldStock;
                case "fireRateUp": return RewardType.FireRateUp;
                case "damageUp": return RewardType.DamageUp;
                case "moveSpeedUp": return RewardType.MoveSpeedUp;
                case "modifier": return RewardType.Modifier;
                case "missileFamily":
                    return RewardType.MissileFamily;
                case "optionFormation":
                    return RewardType.OptionFormation;
                case "bombStock": return RewardType.BombStock;
                case "primaryWeaponFamily":
                    return RewardType.PrimaryWeaponFamily;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        static BattleModifier ParseModifierId(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "pierce_shot": return BattleModifier.PierceShot;
                case "ricochet": return BattleModifier.Ricochet;
                case "homing_missile": return BattleModifier.HomingMissile;
                case "kill_explosion": return BattleModifier.KillExplosion;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        readonly struct EnemyMovementParseResult
        {
            public EnemyMovementParseResult(
                EnemyMovePattern pattern,
                ExactFraction speed,
                ExactFraction amplitude,
                int periodTicks,
                int delayTicks,
                int durationTicks,
                int pauseTicks)
            {
                Pattern = pattern;
                Speed = speed;
                Amplitude = amplitude;
                PeriodTicks = periodTicks;
                DelayTicks = delayTicks;
                DurationTicks = durationTicks;
                PauseTicks = pauseTicks;
            }

            public EnemyMovePattern Pattern { get; }
            public ExactFraction Speed { get; }
            public ExactFraction Amplitude { get; }
            public int PeriodTicks { get; }
            public int DelayTicks { get; }
            public int DurationTicks { get; }
            public int PauseTicks { get; }
        }

        internal readonly struct EnemiesParseResult
        {
            public EnemiesParseResult(
                EnemyDefinition[] definitions,
                int noDropWeight,
                int bombNoDropWeight)
            {
                Definitions = definitions;
                NoDropWeight = noDropWeight;
                BombNoDropWeight = bombNoDropWeight;
            }

            public EnemyDefinition[] Definitions { get; }
            public int NoDropWeight { get; }
            public int BombNoDropWeight { get; }
        }
    }
}
