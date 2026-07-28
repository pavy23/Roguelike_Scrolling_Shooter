using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    /// <summary>
    /// Unity-free parser for enemies.json, weapons.json, waves.json schema v2,
    /// rewards.json schema v1, and optional ships.json schema v1.
    /// Decimal source values are converted with decimal arithmetic only.
    /// </summary>
    public static partial class GameDataParser
    {
        public const int SupportedSchemaVersion = 2;

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
            try
            {
                EnemiesParseResult enemies = ParseEnemies(
                    Deserialize<EnemiesDto>(enemiesJson, "enemies.json"));
                WeaponParseResult weapons = ParseWeapons(
                    Deserialize<WeaponsDto>(weaponsJson, "weapons.json"));
                var content = new BattleContent(
                    enemies.Definitions,
                    weapons.Definitions,
                    weapons.MainShot.Id);
                WavesParseResult waves = ParseWaves(
                    Deserialize<WavesDto>(wavesJson, "waves.json"),
                    content);
                RewardCatalog rewards = rewardsJson == null
                    ? null
                    : ParseRewards(Deserialize<RewardsDto>(rewardsJson, "rewards.json"));
                ShipDefinition[] ships = shipsJson == null
                    ? new[] { ShipDefinition.CreateDefault() }
                    : ParseShips(
                        Deserialize<ShipsDto>(shipsJson, "ships.json"),
                        weapons.MaxLevels);

                return new GameDataSet(
                    content,
                    waves.Catalog,
                    enemies.NoDropWeight,
                    waves.ScrollSpeed.Numerator,
                    waves.ScrollSpeed.Denominator,
                    weapons.MaxLevels,
                    weapons.Missile,
                    rewards,
                    ships);
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
                    "GameData schema v2 validation failed.", ex);
            }
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
            if (schemaVersion != SupportedSchemaVersion)
                throw Error(
                    "enemies.json.schemaVersion",
                    $"must be {SupportedSchemaVersion}, but was {schemaVersion}.");
            if (root.dropTable == null)
                throw Error("enemies.json.dropTable", "is required.");
            int noDropWeight = Require(
                root.dropTable.noDropWeight,
                "enemies.json.dropTable.noDropWeight");
            if (noDropWeight < 0)
                throw Error("enemies.json.dropTable.noDropWeight", "cannot be negative.");

            EnemyDto[] source = RequireArray(root.enemies, "enemies.json.enemies");
            var definitions = new EnemyDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                definitions[i] = ParseEnemy(source[i], i);
                if ((long)noDropWeight + definitions[i].DropWeight > int.MaxValue)
                    throw Error(
                        $"enemies.json.enemies[{i}].dropWeight",
                        "makes the drop-table total exceed the integer range.");
            }
            return new EnemiesParseResult(definitions, noDropWeight);
        }

        static EnemyDefinition ParseEnemy(EnemyDto source, int index)
        {
            string path = $"enemies.json.enemies[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");

            ExactFraction speed = ToPerTickSpeed(
                Require(source.moveSpeed, path + ".moveSpeed"),
                path + ".moveSpeed");
            ExactFraction amplitude = ToSubUnitFraction(
                Require(source.amplitude, path + ".amplitude"),
                path + ".amplitude");
            if (amplitude.Numerator < 0)
                throw Error(path + ".amplitude", "cannot be negative.");

            return new EnemyDefinition(
                RequireText(source.id, path + ".id"),
                RequireText(source.displayName, path + ".displayName"),
                Require(source.hp, path + ".hp"),
                Require(source.contactDamage, path + ".contactDamage"),
                Require(source.scoreValue, path + ".scoreValue"),
                ParseMovePattern(source.movePattern, path + ".movePattern"),
                speed.Numerator,
                speed.Denominator,
                Require(source.fireIntervalTicks, path + ".fireIntervalTicks"),
                ToSubUnits(Require(source.halfWidth, path + ".halfWidth"), path + ".halfWidth"),
                ToSubUnits(Require(source.halfHeight, path + ".halfHeight"), path + ".halfHeight"),
                Require(source.dropWeight, path + ".dropWeight"),
                amplitude.Numerator,
                amplitude.Denominator,
                Require(source.periodTicks, path + ".periodTicks"));
        }

        static EnemyMovePattern ParseMovePattern(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "straight": return EnemyMovePattern.Straight;
                case "sine": return EnemyMovePattern.Sine;
                case "static": return EnemyMovePattern.Static;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        static RewardCatalog ParseRewards(RewardsDto root)
        {
            const int supportedRewardsSchemaVersion = 1;
            int schemaVersion = Require(
                root.schemaVersion,
                "rewards.json.schemaVersion");
            if (schemaVersion != supportedRewardsSchemaVersion)
                throw Error(
                    "rewards.json.schemaVersion",
                    $"must be {supportedRewardsSchemaVersion}, but was {schemaVersion}.");

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
                definitions[i] = ParseReward(source[i], i);
                for (int previous = 0; previous < i; previous++)
                {
                    if (definitions[previous].Id == definitions[i].Id)
                        throw Error(
                            $"rewards.json.rewards[{i}].id",
                            $"duplicates id '{definitions[i].Id}'.");
                }
            }
            return new RewardCatalog(optionCount, definitions);
        }

        static RewardDefinition ParseReward(RewardDto source, int index)
        {
            string path = $"rewards.json.rewards[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");

            RewardType type = ParseRewardType(source.type, path + ".type");
            PowerUpSlot slot = PowerUpSlot.MainShot;
            if (type == RewardType.SlotLevel)
            {
                slot = ParsePowerUpSlot(source.slot, path + ".slot");
            }
            else if (source.slot != null)
            {
                throw Error(path + ".slot", "is only valid for slotLevel rewards.");
            }

            int amount = Require(source.amount, path + ".amount");
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

            return new RewardDefinition(
                RequireText(source.id, path + ".id"),
                type,
                slot,
                amount,
                weight,
                stageIndexMin,
                stageIndexMax);
        }

        static RewardType ParseRewardType(string value, string path)
        {
            switch (RequireText(value, path))
            {
                case "capsules": return RewardType.Capsules;
                case "slotLevel": return RewardType.SlotLevel;
                case "repairHp": return RewardType.RepairHp;
                default: throw Error(path, $"has unknown value '{value}'.");
            }
        }

        internal readonly struct EnemiesParseResult
        {
            public EnemiesParseResult(EnemyDefinition[] definitions, int noDropWeight)
            {
                Definitions = definitions;
                NoDropWeight = noDropWeight;
            }

            public EnemyDefinition[] Definitions { get; }
            public int NoDropWeight { get; }
        }
    }
}
