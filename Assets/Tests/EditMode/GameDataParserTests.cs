using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class GameDataParserTests
    {
        const string EnemiesJson = @"{
  ""schemaVersion"": 2,
  ""dropTable"": { ""noDropWeight"": 8 },
  ""enemies"": [{
    ""id"": ""elite_sine"", ""displayName"": ""Elite"", ""hp"": 50,
    ""contactDamage"": 2, ""scoreValue"": 600, ""movePattern"": ""sine"",
    ""moveSpeed"": 1.8, ""fireIntervalTicks"": 120, ""dropWeight"": 12,
    ""halfWidth"": 0.625, ""halfHeight"": 0.5,
    ""amplitude"": 1.8, ""periodTicks"": 90
  }]
}";

        const string WeaponsJson = @"{
  ""schemaVersion"": 2,
  ""weapons"": [
    { ""id"": ""main_shot"", ""slot"": ""MainShot"", ""baseDamage"": 10,
      ""fireIntervalTicks"": 8, ""projectileSpeed"": 12.0,
      ""projectileHalfWidth"": 0.25, ""projectileHalfHeight"": 0.09375,
      ""maxLevel"": 5 },
    { ""id"": ""missile"", ""slot"": ""Missile"", ""baseDamage"": 20,
      ""fireIntervalTicks"": 30, ""projectileSpeed"": 6.0,
      ""projectileHalfWidth"": 0.3125, ""projectileHalfHeight"": 0.1875,
      ""maxLevel"": 3 },
    { ""id"": ""option"", ""slot"": ""Option"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0.0,
      ""projectileHalfWidth"": 0.0, ""projectileHalfHeight"": 0.0,
      ""maxLevel"": 4 },
    { ""id"": ""shield"", ""slot"": ""Shield"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0.0,
      ""projectileHalfWidth"": 0.0, ""projectileHalfHeight"": 0.0,
      ""maxLevel"": 3 }
  ]
}";

        const string WavesJson = @"{
  ""schemaVersion"": 2, ""scrollSpeed"": 3.0, ""spawnX"": 13.0,
  ""laneCount"": 3, ""segmentsPerStage"": 1, ""startLaneMask"": 2,
  ""segments"": [{
    ""id"": ""seg"", ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""lengthTicks"": 60, ""entryLaneMask"": 7, ""exitLaneMask"": 7,
    ""traversableLaneMasks"": [7],
    ""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }]
  }],
  ""bosses"": [{
    ""id"": ""boss"", ""stageIndexMin"": 1, ""stageIndexMax"": 1,
    ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""entryLaneMask"": 7, ""hp"": 500
  }]
}";

        const string RewardsJson = @"{
  ""schemaVersion"": 1,
  ""optionCount"": 3,
  ""rewards"": [
    { ""id"": ""capsules_3"", ""type"": ""capsules"", ""amount"": 3,
      ""weight"": 2, ""stageIndexMin"": 1, ""stageIndexMax"": 9, ""maxPerRun"": 2 },
    { ""id"": ""main_1"", ""type"": ""slotLevel"", ""slot"": ""MainShot"",
      ""amount"": 1, ""weight"": 4, ""stageIndexMin"": 1, ""stageIndexMax"": 9 },
    { ""id"": ""missile_1"", ""type"": ""slotLevel"", ""slot"": ""Missile"",
      ""amount"": 1, ""weight"": 3, ""stageIndexMin"": 2, ""stageIndexMax"": 8 },
    { ""id"": ""repair_1"", ""type"": ""repairHp"", ""amount"": 1,
      ""weight"": 1, ""stageIndexMin"": 3, ""stageIndexMax"": 7 }
  ]
}";

        const string ShipsJson = @"{
  ""schemaVersion"": 1,
  ""ships"": [
    {
      ""id"": ""starter"", ""displayName"": ""Starter"",
      ""moveSpeedMultiplierNumerator"": 1,
      ""moveSpeedMultiplierDenominator"": 1,
      ""startingPowerUpLevels"": [0, 0, 0, 0],
      ""unlockCost"": 0
    },
    {
      ""id"": ""swift"", ""displayName"": ""Swift"",
      ""moveSpeedMultiplierNumerator"": 5,
      ""moveSpeedMultiplierDenominator"": 4,
      ""startingPowerUpLevels"": [1, 0, 1, 0],
      ""unlockCost"": 1000
    }
  ]
}";

        const string ScoringJson = @"{
  ""schemaVersion"": 1,
  ""grazeRadiusSubUnits"": 192,
  ""grazeScore"": 25,
  ""grazeGaugeCharge"": 4,
  ""multiplierGaugeRequirements"": [12, 34, 56],
  ""multiplierDecayTicks"": 240
}";

        [Test]
        public void Parse_ApprovedV2_BuildsExactRuntimeModels()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson);

            EnemyDefinition enemy = data.BattleContent.Enemies[0];
            Assert.AreEqual("elite_sine", enemy.Id);
            Assert.AreEqual("Elite", enemy.DisplayName);
            Assert.AreEqual(600, enemy.ScoreValue);
            Assert.AreEqual(2304, enemy.MoveSpeedNumerator);
            Assert.AreEqual(300, enemy.MoveSpeedDenominator);
            Assert.AreEqual(160, enemy.HalfWidth);
            Assert.AreEqual(128, enemy.HalfHeight);
            Assert.AreEqual(2304, enemy.SineAmplitudeNumerator);
            Assert.AreEqual(5, enemy.SineAmplitudeDenominator);

            WeaponDefinition main = data.BattleContent.PlayerWeapon;
            Assert.AreEqual(PowerUpSlot.MainShot, main.Slot);
            Assert.AreEqual(3072, main.ProjectileSpeedNumerator);
            Assert.AreEqual(60, main.ProjectileSpeedDenominator);
            Assert.AreEqual(24, main.ProjectileHalfHeight);
            Assert.AreEqual(4, main.MinimumFireIntervalTicks);
            Assert.AreEqual(
                15,
                data.BattleContent.FindWeapon(PowerUpSlot.Missile)
                    .MinimumFireIntervalTicks);

            Assert.AreEqual(768, data.ScrollSpeedNumerator);
            Assert.AreEqual(60, data.ScrollSpeedDenominator);
            Assert.AreEqual(8, data.CapsuleNoDropWeight);

            StageGenerationCatalog stages = data.StageGeneration;
            SpawnEvent spawn = stages.Segments[0].Spawns[0];
            Assert.AreEqual(13 * SimSpace.SubUnitsPerWorldUnit, spawn.X);
            Assert.AreEqual(-11 * SimSpace.SubUnitsPerWorldUnit / 2, spawn.Y);
            Assert.AreEqual(500, stages.Bosses[0].MaxHp);
            Assert.IsNull(stages.Segments[0].ThemeId);
            Assert.IsNull(stages.Bosses[0].ThemeId);
            Assert.AreEqual(0, stages.ThemeIds.Count);
            Assert.IsNull(new SegmentStageGenerator(stages).Generate(1UL, 1, 1).ThemeId);
        }

        [Test]
        public void Parse_FiveArgumentsRetainsDefaultScoringTuning()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                ShipsJson);
            BattleSimConfig config = data.CreateBattleSimConfig();

            Assert.AreEqual(128, config.GrazeExtraRadiusSubUnits);
            Assert.AreEqual(10, config.GrazeScore);
            Assert.AreEqual(1, config.GrazeComboGaugeGain);
            Assert.AreEqual(30, config.ComboGaugeRequiredForLevel2);
            Assert.AreEqual(50, config.ComboGaugeRequiredForLevel3);
            Assert.AreEqual(80, config.ComboGaugeRequiredForLevel4);
            Assert.AreEqual(300, config.ComboDecayTicks);
        }

        [Test]
        public void Parse_OptionalScoringV1CopiesValuesToBattleConfig()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                ShipsJson,
                ScoringJson);
            BattleSimConfig config = data.CreateBattleSimConfig();

            Assert.AreEqual(192, config.GrazeExtraRadiusSubUnits);
            Assert.AreEqual(25, config.GrazeScore);
            Assert.AreEqual(4, config.GrazeComboGaugeGain);
            Assert.AreEqual(12, config.ComboGaugeRequiredForLevel2);
            Assert.AreEqual(34, config.ComboGaugeRequiredForLevel3);
            Assert.AreEqual(56, config.ComboGaugeRequiredForLevel4);
            Assert.AreEqual(240, config.ComboDecayTicks);
        }

        [Test]
        public void Parse_ScoringRejectsInvalidValuesWithPaths()
        {
            string[] invalidJson =
            {
                ScoringJson.Replace(
                    @"""schemaVersion"": 1",
                    @"""schemaVersion"": 2"),
                ScoringJson.Replace(
                    @"""grazeRadiusSubUnits"": 192",
                    @"""grazeRadiusSubUnits"": -1"),
                ScoringJson.Replace(
                    @"""grazeScore"": 25",
                    @"""grazeScore"": -1"),
                ScoringJson.Replace(
                    @"  ""grazeScore"": 25,",
                    ""),
                ScoringJson.Replace(
                    @"""grazeGaugeCharge"": 4",
                    @"""grazeGaugeCharge"": -1"),
                ScoringJson.Replace(
                    @"[12, 34, 56]",
                    @"[12, 34]"),
                ScoringJson.Replace(
                    @"[12, 34, 56]",
                    @"[12, 0, 56]"),
                ScoringJson.Replace(
                    @"""multiplierDecayTicks"": 240",
                    @"""multiplierDecayTicks"": 0")
            };
            string[] expectedPaths =
            {
                "scoring.json.schemaVersion",
                "scoring.json.grazeRadiusSubUnits",
                "scoring.json.grazeScore",
                "scoring.json.grazeScore",
                "scoring.json.grazeGaugeCharge",
                "scoring.json.multiplierGaugeRequirements",
                "scoring.json.multiplierGaugeRequirements[1]",
                "scoring.json.multiplierDecayTicks"
            };

            for (int i = 0; i < invalidJson.Length; i++)
            {
                string json = invalidJson[i];
                GameDataParseException error =
                    Assert.Throws<GameDataParseException>(
                        () => GameDataParser.Parse(
                            EnemiesJson,
                            WeaponsJson,
                            WavesJson,
                            RewardsJson,
                            ShipsJson,
                            json));
                StringAssert.Contains(expectedPaths[i], error.Message);
            }
        }

        [Test]
        public void Parse_OptionalWaveThemesPopulateTemplatesAndStagePlan()
        {
            string themedWaves = WavesJson
                .Replace(@"""id"": ""seg""", @"""id"": ""seg"", ""theme"": ""hive""")
                .Replace(@"""id"": ""boss""", @"""id"": ""boss"", ""theme"": ""hive""");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                themedWaves);
            StagePlan plan = new SegmentStageGenerator(data.StageGeneration)
                .Generate(123UL, 1, 1);

            Assert.AreEqual("hive", data.StageGeneration.Segments[0].ThemeId);
            Assert.AreEqual("hive", data.StageGeneration.Bosses[0].ThemeId);
            CollectionAssert.AreEqual(
                new[] { "hive" },
                data.StageGeneration.ThemeIds);
            Assert.AreEqual("hive", plan.ThemeId);
        }

        [Test]
        public void Parse_ExplicitThemeOrderIsPreserved()
        {
            string themedWaves = WavesWithThemes(
                @"[""core"", ""hive""]",
                "hive",
                "core");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                themedWaves);

            CollectionAssert.AreEqual(
                new[] { "core", "hive" },
                data.StageGeneration.ThemeIds);
        }

        [Test]
        public void Parse_RejectsUnregisteredExplicitTheme()
        {
            string themedWaves = WavesWithThemes(
                @"[""hive"", ""missing""]",
                "hive",
                "hive");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson,
                    WeaponsJson,
                    themedWaves));

            StringAssert.Contains("waves.json.themes[1]", error.Message);
            StringAssert.Contains("unregistered theme 'missing'", error.Message);
        }

        [Test]
        public void Parse_RejectsTaggedThemeMissingFromExplicitOrder()
        {
            string themedWaves = WavesWithThemes(
                @"[""core""]",
                "hive",
                "core");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson,
                    WeaponsJson,
                    themedWaves));

            StringAssert.Contains("waves.json.segments[0].theme", error.Message);
            StringAssert.Contains(
                "theme 'hive' is missing from waves.json.themes",
                error.Message);
        }

        [Test]
        public void Factories_UseSchemaValuesAndReturnIndependentState()
        {
            GameDataSet data = GameDataParser.Parse(EnemiesJson, WeaponsJson, WavesJson);
            BattleSimConfig first = data.CreateBattleSimConfig();
            BattleSimConfig second = data.CreateBattleSimConfig();

            Assert.AreEqual(20, first.MissileBaseDamage);
            Assert.AreEqual(4, first.MainShotMinimumFireIntervalTicks);
            Assert.AreEqual(15, first.MissileMinimumFireIntervalTicks);
            Assert.AreEqual(1536, first.MissileSpeedXNumerator);
            Assert.AreEqual(60, first.MissileSpeedXDenominator);
            Assert.AreEqual(80, first.MissileHalfWidth);
            Assert.AreEqual(48, first.MissileHalfHeight);
            first.CapsuleNoDropWeight = 999;
            Assert.AreEqual(8, second.CapsuleNoDropWeight);

            PowerUpGauge gauge = data.CreatePowerUpGauge();
            Assert.AreEqual(5, gauge.GetMaxLevel(PowerUpSlot.MainShot));
            Assert.AreEqual(3, gauge.GetMaxLevel(PowerUpSlot.Missile));
            Assert.AreEqual(4, gauge.GetMaxLevel(PowerUpSlot.Option));
            Assert.AreEqual(3, gauge.GetMaxLevel(PowerUpSlot.Shield));
            Assert.AreEqual(1, data.Ships.Count);
            Assert.AreEqual("default", data.DefaultShip.Id);
            Assert.AreEqual("default", data.CreateMetaState().SelectedShipId);
        }

        [Test]
        public void Parse_ExplicitWeaponMinimumIntervals_AreAppliedToBothFiringWeapons()
        {
            string explicitMinimums = WeaponsJson
                .Replace(
                    @"""fireIntervalTicks"": 8,",
                    @"""fireIntervalTicks"": 8, ""minimumFireIntervalTicks"": 3,")
                .Replace(
                    @"""fireIntervalTicks"": 30,",
                    @"""fireIntervalTicks"": 30, ""minimumFireIntervalTicks"": 11,");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                explicitMinimums,
                WavesJson);
            BattleSimConfig config = data.CreateBattleSimConfig();

            Assert.AreEqual(
                3,
                data.BattleContent.FindWeapon(PowerUpSlot.MainShot)
                    .MinimumFireIntervalTicks);
            Assert.AreEqual(
                11,
                data.BattleContent.FindWeapon(PowerUpSlot.Missile)
                    .MinimumFireIntervalTicks);
            Assert.AreEqual(3, config.MainShotMinimumFireIntervalTicks);
            Assert.AreEqual(11, config.MissileMinimumFireIntervalTicks);
        }

        [Test]
        public void Parse_MissingWeaponMinimumIntervals_FallBackToHalfRoundedDown()
        {
            string oddIntervals = WeaponsJson
                .Replace(
                    @"""fireIntervalTicks"": 8,",
                    @"""fireIntervalTicks"": 9,")
                .Replace(
                    @"""fireIntervalTicks"": 30,",
                    @"""fireIntervalTicks"": 31,");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                oddIntervals,
                WavesJson);
            BattleSimConfig config = data.CreateBattleSimConfig();

            Assert.AreEqual(4, config.MainShotMinimumFireIntervalTicks);
            Assert.AreEqual(15, config.MissileMinimumFireIntervalTicks);
        }

        [Test]
        public void Parse_RewardsV1_ExposesImmutableCatalog()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson);

            Assert.IsNotNull(data.Rewards);
            Assert.AreEqual(3, data.Rewards.OptionCount);
            Assert.AreEqual(4, data.Rewards.All.Count);
            Assert.AreEqual("capsules_3", data.Rewards.All[0].Id);
            Assert.AreEqual(RewardType.Capsules, data.Rewards.All[0].Type);
            Assert.AreEqual(2, data.Rewards.All[0].Weight);
            Assert.AreEqual(2, data.Rewards.All[0].MaxPerRun);
            Assert.IsNull(data.Rewards.All[1].MaxPerRun);
            Assert.AreEqual(BattleModifier.None, data.Rewards.All[1].ModifierId);
            Assert.AreEqual(PowerUpSlot.Missile, data.Rewards.All[2].Slot);
            Assert.AreEqual(2, data.Rewards.All[2].StageIndexMin);
            Assert.AreEqual(8, data.Rewards.All[2].StageIndexMax);
            Assert.AreEqual(2, data.Rewards.EligibleForStage(1).Count);
            Assert.AreEqual(4, data.Rewards.EligibleForStage(3).Count);
            Assert.IsFalse(data.Rewards.All is RewardDefinition[]);
        }

        [Test]
        public void Parse_ThreeInputs_LeavesRewardsNullForBuiltInFallback()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson);

            Assert.IsNull(data.Rewards);
        }

        [Test]
        public void Parse_OptionalShipsV1_BuildsShipCatalog()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                ShipsJson);

            Assert.AreEqual(2, data.Ships.Count);
            Assert.AreEqual("starter", data.DefaultShip.Id);
            ShipDefinition swift = data.FindShip("swift");
            Assert.IsNotNull(swift);
            Assert.AreEqual("Swift", swift.DisplayName);
            Assert.AreEqual(5, swift.MoveSpeedMultiplierNumerator);
            Assert.AreEqual(4, swift.MoveSpeedMultiplierDenominator);
            CollectionAssert.AreEqual(
                new[] { 1, 0, 1, 0 },
                swift.StartingPowerUpLevels);
            Assert.AreEqual(1000L, swift.UnlockCost);
            Assert.IsFalse(data.Ships is ShipDefinition[]);
        }

        [Test]
        public void Parse_ShipsRejectsLevelAboveWeaponMaximumWithPath()
        {
            string invalid = ShipsJson.Replace(
                @"""startingPowerUpLevels"": [1, 0, 1, 0]",
                @"""startingPowerUpLevels"": [6, 0, 1, 0]");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson,
                    WeaponsJson,
                    WavesJson,
                    RewardsJson,
                    invalid));

            StringAssert.Contains(
                "ships.json.ships[1].startingPowerUpLevels[0]",
                error.Message);
        }

        [Test]
        public void Parse_ShipsRequiresZeroCostStartingShip()
        {
            string invalid = ShipsJson.Replace(
                @"""unlockCost"": 0",
                @"""unlockCost"": 10");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson,
                    WeaponsJson,
                    WavesJson,
                    RewardsJson,
                    invalid));

            StringAssert.Contains("zero-cost", error.Message);
        }

        [Test]
        public void RepositoryApprovedV2Files_ParseCompletely()
        {
            string root = FindRepositoryRoot();
            GameDataSet data = GameDataParser.Parse(
                ReadUtf8(Path.Combine(root, "GameData", "enemies.json")),
                ReadUtf8(Path.Combine(root, "GameData", "weapons.json")),
                ReadUtf8(Path.Combine(root, "GameData", "waves.json")),
                ReadUtf8(Path.Combine(root, "GameData", "rewards.json")));

            Assert.AreEqual(30, data.BattleContent.Enemies.Count);
            Assert.AreEqual(4, data.BattleContent.Weapons.Count);
            Assert.AreEqual(16, data.StageGeneration.Segments.Count);
            Assert.AreEqual(5, data.StageGeneration.Bosses.Count);
            Assert.AreEqual(3, data.Rewards.OptionCount);
            Assert.AreEqual(13, data.Rewards.All.Count);

            // 640×360 재스케일(REQ-006) 후 elite_sine 진폭 = 3.0u = 768 서브유닛.
            EnemyDefinition elite = data.BattleContent.FindEnemy("elite_sine");
            Assert.AreEqual(768, elite.SineAmplitudeNumerator);
            Assert.AreEqual(1, elite.SineAmplitudeDenominator);

            var generator = new SegmentStageGenerator(data.StageGeneration);
            StagePlan first = generator.Generate(123456789UL, 1, 3);
            StagePlan second = generator.Generate(123456789UL, 1, 3);
            Assert.IsTrue(StagePlanClearability.IsClearable(first));
            Assert.AreEqual(first.BossId, second.BossId);
            for (int i = 0; i < first.Segments.Count; i++)
                Assert.AreEqual(first.Segments[i].SegmentId, second.Segments[i].SegmentId);
        }

        [Test]
        public void Parse_RejectsUnsupportedSchemaVersionWithPath()
        {
            string invalid = EnemiesJson.Replace('2', '1');

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(invalid, WeaponsJson, WavesJson));
            StringAssert.Contains("enemies.json.schemaVersion", error.Message);
        }

        [Test]
        public void Parse_RejectsUnknownEnumWithPath()
        {
            string invalid = EnemiesJson.Replace("sine", "random");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(invalid, WeaponsJson, WavesJson));
            StringAssert.Contains("movePattern", error.Message);
        }

        [Test]
        public void Parse_RejectsUnknownSpawnEnemyWithPath()
        {
            string invalid = WavesJson.Replace("elite_sine", "missing_enemy");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(EnemiesJson, WeaponsJson, invalid));
            StringAssert.Contains("enemyId", error.Message);
        }

        [Test]
        public void Parse_RejectsCoordinateOutsideIntegerSubunitGrid()
        {
            string invalid = WavesJson.Replace("-5.5", "-5.5001");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(EnemiesJson, WeaponsJson, invalid));
            StringAssert.Contains("whole 1/256", error.Message);
        }

        [Test]
        public void Parse_RejectsNonPositiveExplicitBossHitbox()
        {
            string invalid = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500, ""halfWidth"": -1.0");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(EnemiesJson, WeaponsJson, invalid));
            StringAssert.Contains("bosses[0].halfWidth", error.Message);
        }

        static string WavesWithThemes(
            string themesJson,
            string segmentTheme,
            string bossTheme)
        {
            return WavesJson
                .Replace(
                    @"""startLaneMask"": 2,",
                    @"""startLaneMask"": 2, ""themes"": " + themesJson + ",")
                .Replace(
                    @"""id"": ""seg""",
                    @"""id"": ""seg"", ""theme"": """ + segmentTheme + @"""")
                .Replace(
                    @"""id"": ""boss""",
                    @"""id"": ""boss"", ""theme"": """ + bossTheme + @"""");
        }

        [Test]
        public void Parse_RejectsInvalidRewardFieldsWithPath()
        {
            string invalidType = RewardsJson.Replace(
                @"""type"": ""capsules""",
                @"""type"": ""mystery""");
            GameDataParseException typeError = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson, WeaponsJson, WavesJson, invalidType));
            StringAssert.Contains("rewards[0].type", typeError.Message);

            string missingSlot = RewardsJson.Replace(
                @"""slot"": ""Missile"",",
                "");
            GameDataParseException slotError = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson, WeaponsJson, WavesJson, missingSlot));
            StringAssert.Contains("rewards[2].slot", slotError.Message);

            string zeroWeight = RewardsJson.Replace(
                @"""weight"": 2",
                @"""weight"": 0");
            GameDataParseException weightError = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson, WeaponsJson, WavesJson, zeroWeight));
            StringAssert.Contains("rewards[0].weight", weightError.Message);

            string reversedRange = RewardsJson.Replace(
                @"""stageIndexMin"": 3, ""stageIndexMax"": 7",
                @"""stageIndexMin"": 8, ""stageIndexMax"": 7");
            GameDataParseException rangeError = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson, WeaponsJson, WavesJson, reversedRange));
            StringAssert.Contains("rewards[3].stageIndexMax", rangeError.Message);

            string zeroMaxPerRun = RewardsJson.Replace(
                @"""maxPerRun"": 2",
                @"""maxPerRun"": 0");
            GameDataParseException maxPerRunError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        zeroMaxPerRun));
            StringAssert.Contains(
                "rewards[0].maxPerRun",
                maxPerRunError.Message);
        }

        [Test]
        public void Parse_AcceptsRunPassiveRewardTypes()
        {
            string[] names =
            {
                "fireRateUp",
                "damageUp",
                "moveSpeedUp"
            };
            RewardType[] expected =
            {
                RewardType.FireRateUp,
                RewardType.DamageUp,
                RewardType.MoveSpeedUp
            };

            for (int i = 0; i < names.Length; i++)
            {
                string json = RewardsJson.Replace(
                    @"""type"": ""capsules""",
                    @"""type"": """ + names[i] + @"""");
                GameDataSet data = GameDataParser.Parse(
                    EnemiesJson,
                    WeaponsJson,
                    WavesJson,
                    json);
                Assert.AreEqual(expected[i], data.Rewards.All[0].Type);
            }
        }

        [Test]
        public void Parse_ModifierRewardsMapsAllIdsAndKeepsAmountOptional()
        {
            const string modifierRewards = @"{
  ""schemaVersion"": 1,
  ""optionCount"": 3,
  ""rewards"": [
    { ""id"": ""pierce"", ""type"": ""modifier"", ""modifierId"": ""pierce_shot"",
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""bounce"", ""type"": ""modifier"", ""modifierId"": ""ricochet"",
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""homing"", ""type"": ""modifier"", ""modifierId"": ""homing_missile"",
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""explosion"", ""type"": ""modifier"", ""modifierId"": ""kill_explosion"",
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 99 }
  ]
}";

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                modifierRewards);

            Assert.AreEqual(RewardType.Modifier, data.Rewards.All[0].Type);
            Assert.AreEqual(BattleModifier.PierceShot, data.Rewards.All[0].ModifierId);
            Assert.AreEqual(BattleModifier.Ricochet, data.Rewards.All[1].ModifierId);
            Assert.AreEqual(BattleModifier.HomingMissile, data.Rewards.All[2].ModifierId);
            Assert.AreEqual(BattleModifier.KillExplosion, data.Rewards.All[3].ModifierId);
            Assert.AreEqual(1, data.Rewards.All[0].Amount);
        }

        [Test]
        public void Parse_ModifierIdIsRequiredOnlyForModifierRewards()
        {
            string missing = RewardsJson.Replace(
                @"""type"": ""capsules""",
                @"""type"": ""modifier""");
            GameDataParseException missingError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        missing));
            StringAssert.Contains("rewards[0].modifierId", missingError.Message);

            string misplaced = RewardsJson.Replace(
                @"""type"": ""capsules"", ""amount"": 3",
                @"""type"": ""capsules"", ""modifierId"": ""ricochet"", ""amount"": 3");
            GameDataParseException misplacedError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        misplaced));
            StringAssert.Contains("rewards[0].modifierId", misplacedError.Message);
        }

        [Test]
        public void Parse_RejectsRewardOptionCountOtherThanThree()
        {
            string invalid = RewardsJson.Replace(
                @"""optionCount"": 3",
                @"""optionCount"": 2");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(
                    EnemiesJson, WeaponsJson, WavesJson, invalid));
            StringAssert.Contains("rewards.json.optionCount", error.Message);
        }

        static string FindRepositoryRoot()
        {
            // Unity 내장 NUnit은 WorkDirectory를 채우지 않는다 — 그 경우 프로젝트 루트인
            // CurrentDirectory에서 출발한다 (양쪽 러너 모두 상향 탐색으로 GameData를 찾는다).
            string start = TestContext.CurrentContext?.WorkDirectory;
            if (string.IsNullOrEmpty(start))
                start = Environment.CurrentDirectory;

            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "GameData", "waves.json")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repository GameData folder.");
        }

        static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
    }
}
