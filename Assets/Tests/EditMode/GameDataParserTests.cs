using System;
using System.Collections.Generic;
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

        const string EnemiesV3Json = @"{
  ""schemaVersion"": 3,
  ""dropTable"": { ""noDropWeight"": 8 },
  ""enemies"": [
    {
      ""id"": ""dive_enemy"", ""displayName"": ""Dive"", ""hp"": 10,
      ""contactDamage"": 1, ""scoreValue"": 100, ""fireIntervalTicks"": 0,
      ""dropWeight"": 1, ""halfWidth"": 0.5, ""halfHeight"": 0.5,
      ""movement"": {
        ""pattern"": ""dive"", ""speed"": 6,
        ""delayTicks"": 20, ""durationTicks"": 30
      },
      ""midBoss"": {
        ""themeId"": ""hive"", ""weight"": 4,
        ""stageIndexMin"": 2, ""stageIndexMax"": 4,
        ""phases"": [
          {
            ""fireIntervalTicks"": 48, ""ways"": 1,
            ""bulletSpeed"": 8, ""movementPattern"": ""stationary"",
            ""durationTicks"": 120
          },
          {
            ""fireIntervalTicks"": 28, ""ways"": 3,
            ""bulletSpeed"": 10, ""movementPattern"": ""verticalSine"",
            ""movementAmplitude"": 2.5, ""movementPeriodTicks"": 90,
            ""durationTicks"": 105, ""telegraphTicks"": 18
          }
        ]
      }
    },
    {
      ""id"": ""zigzag_enemy"", ""displayName"": ""Zigzag"", ""hp"": 10,
      ""contactDamage"": 1, ""scoreValue"": 100, ""fireIntervalTicks"": 0,
      ""dropWeight"": 1, ""halfWidth"": 0.5, ""halfHeight"": 0.5,
      ""movement"": {
        ""pattern"": ""zigzag"", ""speed"": 4.5,
        ""amplitude"": 2.5, ""periodTicks"": 80
      }
    },
    {
      ""id"": ""dash_enemy"", ""displayName"": ""Dash"", ""hp"": 10,
      ""contactDamage"": 1, ""scoreValue"": 100, ""fireIntervalTicks"": 0,
      ""dropWeight"": 1, ""halfWidth"": 0.5, ""halfHeight"": 0.5,
      ""movement"": {
        ""pattern"": ""dash"", ""speed"": 12,
        ""durationTicks"": 8, ""pauseTicks"": 24
      }
    }
  ]
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

        const string PlayerJson = @"{
  ""schemaVersion"": 1,
  ""player"": {
    ""maxEnemyBullets"": 96
  }
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
            Assert.AreEqual(0, stages.Segments[0].Obstacles.Count);
            Assert.AreEqual(
                StageSegmentTemplate.DefaultWeight,
                stages.Segments[0].Weight);
            Assert.IsNull(stages.Segments[0].ThemeId);
            Assert.IsNull(stages.Bosses[0].ThemeId);
            Assert.AreEqual(0, stages.ThemeIds.Count);
            Assert.IsNull(new SegmentStageGenerator(stages).Generate(1UL, 1, 1).ThemeId);
        }

        [Test]
        public void Parse_BossPartsBuildExactGateSpawnAndSuctionModels()
        {
            string waves = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500,
    ""parts"": [
      {
        ""id"": ""spawn_sac"", ""offsetX"": -2.5, ""offsetY"": 1.25,
        ""halfWidth"": 1.5, ""halfHeight"": 1, ""hp"": 100,
        ""attack"": {
          ""type"": ""spawnEnemy"", ""intervalTicks"": 480,
          ""spawnEnemyId"": ""elite_sine""
        }
      },
      {
        ""id"": ""maw"", ""offsetX"": 0, ""offsetY"": 0,
        ""halfWidth"": 2, ""halfHeight"": 2, ""hp"": 150,
        ""regenerationTicks"": 1200,
        ""attack"": { ""type"": ""suction"", ""effectSpeed"": 3.0 }
      },
      {
        ""id"": ""heart"", ""offsetX"": 2, ""offsetY"": 0,
        ""halfWidth"": 1.5, ""halfHeight"": 1.5, ""hp"": 250,
        ""isCore"": true, ""coreGatePartIds"": [""spawn_sac""],
        ""attack"": {
          ""type"": ""radialSpread"", ""intervalTicks"": 90,
          ""ways"": 8, ""bulletSpeed"": 6.0
        }
      }
    ]");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves);
            StageBossTemplate boss =
                data.StageGeneration.Bosses[0];
            Assert.AreEqual(3, boss.Parts.Count);
            Assert.AreEqual(-640, boss.Parts[0].OffsetX);
            Assert.AreEqual(320, boss.Parts[0].OffsetY);
            Assert.AreEqual(
                BossPartAttackType.SpawnEnemy,
                boss.Parts[0].Attack.Type);
            Assert.AreEqual(
                "elite_sine",
                boss.Parts[0].Attack.SpawnEnemyId);
            Assert.AreEqual(1200, boss.Parts[1].RegenerationTicks);
            Assert.AreEqual(768, boss.Parts[1].Attack.EffectSpeedNumerator);
            Assert.AreEqual(60, boss.Parts[1].Attack.EffectSpeedDenominator);
            CollectionAssert.AreEqual(
                new[] { "spawn_sac" },
                boss.Parts[2].CoreGatePartIds);

            StagePlan plan =
                new SegmentStageGenerator(data.StageGeneration)
                    .Generate(12UL, 1, 1);
            Assert.AreEqual(3, plan.BossParts.Count);
            Assert.IsTrue(plan.BossParts[2].IsCore);
        }

        [Test]
        public void Parse_BossPhasesBuildShootingMovementAndPartStateAxes()
        {
            string waves = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500,
    ""phases"": [
      {
        ""pattern"": ""radial"",
        ""fireIntervalTicks"": 90, ""ways"": 2, ""bulletSpeed"": 6.0,
        ""movementPattern"": ""stationary"",
        ""partVulnerability"": ""coreOnly""
      },
      {
        ""pattern"": ""spiral"",
        ""fireIntervalTicks"": 45, ""ways"": 3, ""bulletSpeed"": 9.0,
        ""movementPattern"": ""verticalSine"",
        ""movementAmplitude"": 1.5, ""movementPeriodTicks"": 120,
        ""partVulnerability"": ""all""
      },
      {
        ""pattern"": ""wall"",
        ""fireIntervalTicks"": 50, ""ways"": 5, ""bulletSpeed"": 7.0
      },
      {
        ""pattern"": ""burst"",
        ""fireIntervalTicks"": 80, ""ways"": 4, ""bulletSpeed"": 8.0,
        ""telegraphTicks"": 15
      }
    ]");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves);
            IReadOnlyList<BossPhase> phases =
                data.StageGeneration.Bosses[0].Phases;

            Assert.AreEqual(4, phases.Count);
            Assert.AreEqual(
                BossMovementPattern.Stationary,
                phases[0].MovementPattern);
            Assert.AreEqual(
                BossFirePattern.Radial,
                phases[0].FirePattern);
            Assert.AreEqual(
                BossPartVulnerability.CoreOnly,
                phases[0].PartVulnerability);
            Assert.AreEqual(
                BossMovementPattern.VerticalSine,
                phases[1].MovementPattern);
            Assert.AreEqual(
                BossFirePattern.Spiral,
                phases[1].FirePattern);
            Assert.AreEqual(384, phases[1].MovementAmplitudeNumerator);
            Assert.AreEqual(1, phases[1].MovementAmplitudeDenominator);
            Assert.AreEqual(120, phases[1].MovementPeriodTicks);
            Assert.AreEqual(
                BossPartVulnerability.All,
                phases[1].PartVulnerability);
            Assert.AreEqual(
                BossFirePattern.Wall,
                phases[2].FirePattern);
            Assert.AreEqual(
                BossFirePattern.Burst,
                phases[3].FirePattern);
        }

        [Test]
        public void Parse_Req087ProjectileAndSignatureAxesAreExactAndOptional()
        {
            const string laser = @"""bossLaser"": {
          ""cycleIntervalTicks"": 8, ""telegraphTicks"": 2,
          ""firingTicks"": 2, ""sustainTicks"": 2,
          ""dissipateTicks"": 2,
          ""startOffsetX"": 0, ""startOffsetY"": -1,
          ""endOffsetX"": 0, ""endOffsetY"": 1,
          ""thinHalfWidth"": 0.125, ""fullHalfWidth"": 0.25,
          ""damage"": 1
        }";
            string waves = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500,
    ""phases"": [
      { ""fireIntervalTicks"": 60, ""ways"": 1, ""bulletSpeed"": 6 },
      { ""fireIntervalTicks"": 60, ""ways"": 1, ""bulletSpeed"": 6,
        ""projectileKind"": ""heavy"",
        ""signaturePatternId"": ""scrapThrow"",
        ""signatureObstacleHp"": 9, ""signatureGravity"": 3600 },
      { ""fireIntervalTicks"": 60, ""ways"": 1, ""bulletSpeed"": 6,
        ""projectileKind"": ""splitter"", ""splitAfterTicks"": 12,
        ""signaturePatternId"": ""brood"",
        ""signatureSpawnEnemyId"": ""elite_sine"",
        ""signatureHomingTurnLutSlotsPerTick"": 1 },
      { ""fireIntervalTicks"": 60, ""ways"": 1, ""bulletSpeed"": 6,
        ""projectileKind"": ""mine"", ""mineTravelTicks"": 10,
        ""mineTelegraphTicks"": 8, ""mineAcceleration"": 3600,
        ""signaturePatternId"": ""laserGrid"", " + laser + @" },
      { ""fireIntervalTicks"": 60, ""ways"": 1, ""bulletSpeed"": 6,
        ""signaturePatternId"": ""lightning"", " + laser + @" },
      { ""fireIntervalTicks"": 60, ""ways"": 4, ""bulletSpeed"": 6,
        ""pattern"": ""radial"", ""projectileKind"": ""bossLaser"",
        ""signaturePatternId"": ""prismCore"", " + laser + @" }
    ]");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves);
            IReadOnlyList<BossPhase> phases =
                data.StageGeneration.Bosses[0].Phases;

            Assert.AreEqual(BossProjectileKind.Normal, phases[0].ProjectileKind);
            Assert.AreEqual(BossSignaturePattern.None, phases[0].SignaturePattern);
            Assert.AreEqual(BossProjectileKind.Heavy, phases[1].ProjectileKind);
            Assert.AreEqual(BossSignaturePattern.ScrapThrow, phases[1].SignaturePattern);
            Assert.AreEqual(256, phases[1].SignatureGravityNumerator);
            Assert.AreEqual(1, phases[1].SignatureGravityDenominator);
            Assert.AreEqual(BossProjectileKind.Splitter, phases[2].ProjectileKind);
            Assert.AreEqual(12, phases[2].SplitAfterTicks);
            Assert.AreEqual(BossSignaturePattern.Brood, phases[2].SignaturePattern);
            Assert.AreEqual("elite_sine", phases[2].SignatureSpawnEnemyId);
            Assert.AreEqual(BossProjectileKind.Mine, phases[3].ProjectileKind);
            Assert.AreEqual(256, phases[3].MineAccelerationNumerator);
            Assert.AreEqual(BossSignaturePattern.LaserGrid, phases[3].SignaturePattern);
            Assert.AreEqual(BossSignaturePattern.Lightning, phases[4].SignaturePattern);
            Assert.AreEqual(BossProjectileKind.BossLaser, phases[5].ProjectileKind);
            Assert.AreEqual(BossSignaturePattern.PrismCore, phases[5].SignaturePattern);
            Assert.IsNotNull(phases[5].LaserAttack);
        }

        [Test]
        public void Parse_LegacyBossPatternNamesMapToAimed()
        {
            string waves = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500,
    ""phases"": [
      { ""pattern"": ""aimed"", ""fireIntervalTicks"": 60,
        ""ways"": 1, ""bulletSpeed"": 6 },
      { ""pattern"": ""spread"", ""fireIntervalTicks"": 45,
        ""ways"": 3, ""bulletSpeed"": 7 },
      { ""pattern"": ""rapid"", ""fireIntervalTicks"": 20,
        ""ways"": 1, ""bulletSpeed"": 8 }
    ]");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves);
            IReadOnlyList<BossPhase> phases =
                data.StageGeneration.Bosses[0].Phases;

            Assert.AreEqual(BossFirePattern.Aimed, phases[0].FirePattern);
            Assert.AreEqual(BossFirePattern.Aimed, phases[1].FirePattern);
            Assert.AreEqual(BossFirePattern.Aimed, phases[2].FirePattern);
        }

        [Test]
        public void Parse_RejectsUnknownBossFirePatternWithFieldPath()
        {
            string waves = WavesJson.Replace(
                @"""entryLaneMask"": 7, ""hp"": 500",
                @"""entryLaneMask"": 7, ""hp"": 500,
    ""phases"": [
      { ""pattern"": ""unknown"", ""fireIntervalTicks"": 60,
        ""ways"": 1, ""bulletSpeed"": 6 }
    ]");

            GameDataParseException error =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        waves));

            StringAssert.Contains(
                "waves.json.bosses[0].phases[0].pattern",
                error.Message);
        }

        [Test]
        public void Parse_WaveObstaclesBuildExactModelsAndGeneratedPlan()
        {
            string waves = WavesJson.Replace(
                @"""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }]",
                @"""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }],
    ""obstacles"": [
      { ""type"": ""solid"", ""x"": 12.5, ""y"": -1.25, ""hp"": 0 },
      { ""type"": ""breakable"", ""x"": 14, ""y"": 2.5, ""hp"": 30 }
    ]");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves);
            StageSegmentTemplate template =
                data.StageGeneration.Segments[0];
            Assert.AreEqual(2, template.Obstacles.Count);
            Assert.AreEqual(
                ObstacleType.Solid,
                template.Obstacles[0].Type);
            Assert.AreEqual(3200, template.Obstacles[0].X);
            Assert.AreEqual(-320, template.Obstacles[0].Y);
            Assert.AreEqual(0, template.Obstacles[0].Hp);
            Assert.AreEqual(
                ObstacleType.Breakable,
                template.Obstacles[1].Type);
            Assert.AreEqual(3584, template.Obstacles[1].X);
            Assert.AreEqual(640, template.Obstacles[1].Y);
            Assert.AreEqual(30, template.Obstacles[1].Hp);

            StagePlan plan = new SegmentStageGenerator(
                data.StageGeneration).Generate(9UL, 1, 1);
            Assert.AreEqual(2, plan.Segments[0].Obstacles.Count);
            Assert.AreEqual(30, plan.Segments[0].Obstacles[1].Hp);
        }

        [Test]
        public void Parse_WaveObstacleRejectsInvalidTypeWithPath()
        {
            string waves = WavesJson.Replace(
                @"""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }]",
                @"""spawns"": [],
    ""obstacles"": [
      { ""type"": ""hazard"", ""x"": 1, ""y"": 2, ""hp"": 0 }
    ]");

            GameDataParseException error =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        waves));
            StringAssert.Contains(
                "waves.json.segments[0].obstacles[0].type",
                error.Message);
        }

        [Test]
        public void Parse_EnemiesV3Movement_BuildsAllNewPatternParameters()
        {
            string waves = WavesJson.Replace("elite_sine", "dive_enemy");
            GameDataSet data = GameDataParser.Parse(
                EnemiesV3Json,
                WeaponsJson,
                waves);

            EnemyDefinition dive = data.BattleContent.FindEnemy("dive_enemy");
            Assert.AreEqual(EnemyMovePattern.Dive, dive.MovePattern);
            Assert.AreEqual(1536, dive.MoveSpeedNumerator);
            Assert.AreEqual(60, dive.MoveSpeedDenominator);
            Assert.AreEqual(20, dive.MovementDelayTicks);
            Assert.AreEqual(30, dive.MovementDurationTicks);
            Assert.IsNotNull(dive.MidBossProfile);
            Assert.AreEqual("hive", dive.MidBossProfile.ThemeId);
            Assert.AreEqual(4, dive.MidBossProfile.Weight);
            Assert.AreEqual(2, dive.MidBossProfile.StageIndexMin);
            Assert.AreEqual(4, dive.MidBossProfile.StageIndexMax);
            Assert.AreEqual(2, dive.MidBossProfile.Phases.Count);
            Assert.AreEqual(
                120,
                dive.MidBossProfile.Phases[0].DurationTicks);
            Assert.AreEqual(
                18,
                dive.MidBossProfile.Phases[1].TelegraphTicks);

            EnemyDefinition zigzag = data.BattleContent.FindEnemy("zigzag_enemy");
            Assert.AreEqual(EnemyMovePattern.Zigzag, zigzag.MovePattern);
            Assert.AreEqual(1152, zigzag.MoveSpeedNumerator);
            Assert.AreEqual(60, zigzag.MoveSpeedDenominator);
            Assert.AreEqual(640, zigzag.MovementAmplitudeNumerator);
            Assert.AreEqual(1, zigzag.MovementAmplitudeDenominator);
            Assert.AreEqual(80, zigzag.MovementPeriodTicks);

            EnemyDefinition dash = data.BattleContent.FindEnemy("dash_enemy");
            Assert.AreEqual(EnemyMovePattern.Dash, dash.MovePattern);
            Assert.AreEqual(3072, dash.MoveSpeedNumerator);
            Assert.AreEqual(60, dash.MoveSpeedDenominator);
            Assert.AreEqual(8, dash.MovementDurationTicks);
            Assert.AreEqual(24, dash.MovementPauseTicks);
        }

        [Test]
        public void Parse_EnemiesV3Movement_RejectsMissingPatternParameterWithPath()
        {
            string invalid = EnemiesV3Json.Replace(@", ""pauseTicks"": 24", "");
            string waves = WavesJson.Replace("elite_sine", "dive_enemy");

            GameDataParseException error = Assert.Throws<GameDataParseException>(
                () => GameDataParser.Parse(invalid, WeaponsJson, waves));

            StringAssert.Contains(
                "enemies.json.enemies[2].movement.pauseTicks",
                error.Message);
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
        public void Parse_RewardsV3SupportsStackProfilesAndArbitraryIds()
        {
            const string rewardsV3 = @"{
  ""schemaVersion"": 3,
  ""optionCount"": 3,
  ""maxCombinedModifierCost"": 7,
  ""rewards"": [
    {
      ""id"": ""pierce_alpha"", ""type"": ""modifier"",
      ""modifierId"": ""alpha"", ""modifierEffect"": ""pierce_shot"",
      ""stackable"": true, ""maxStacks"": 3,
      ""stackStrength"": 2, ""interactionCost"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9
    },
    {
      ""id"": ""ricochet_once"", ""type"": ""modifier"",
      ""modifierId"": ""ricochet_once"", ""modifierEffect"": ""ricochet"",
      ""stackable"": false, ""maxStacks"": 1,
      ""stackStrength"": 1, ""interactionCost"": 2,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9
    },
    {
      ""id"": ""homing_beta"", ""type"": ""modifier"",
      ""modifierId"": ""beta"", ""modifierEffect"": ""homing_missile"",
      ""stackable"": true, ""maxStacks"": 2,
      ""stackStrength"": 1, ""interactionCost"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9
    }
  ]
}";

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                rewardsV3,
                ShipsJson);
            RewardDefinition modifier = data.Rewards.All[0];

            AssertAll(() =>
            {
                Assert.AreEqual(7, data.Rewards.MaxCombinedModifierCost);
                Assert.AreEqual("alpha", modifier.ModifierKey);
                Assert.AreEqual(BattleModifier.PierceShot, modifier.ModifierId);
                Assert.IsTrue(modifier.ModifierStackable);
                Assert.AreEqual(3, modifier.ModifierMaxStacks);
                Assert.AreEqual(2, modifier.ModifierStackStrength);
                Assert.AreEqual(1, modifier.ModifierInteractionCost);
                Assert.IsFalse(data.Rewards.All[1].ModifierStackable);
            });
        }

        [Test]
        public void Req072SchemasExposeTerminalContractsAndRerollCost()
        {
            string waves = WavesJson.Replace(
                @"  ""segments"": [{",
                @"  ""contracts"": {
    ""standardContractId"": ""standard_route"",
    ""minimumOptionCount"": 2,
    ""maximumOptionCount"": 3,
    ""entries"": [
      { ""id"": ""standard_route"", ""weight"": 1, ""riskTier"": ""safe"" },
      { ""id"": ""end_run"", ""weight"": 1, ""riskTier"": ""safe"",
        ""destinationKind"": ""endRun"" },
      { ""id"": ""uncharted"", ""weight"": 1, ""riskTier"": ""high"",
        ""destinationKind"": ""uncharted"",
        ""eligibility"": ""hiddenBiomeUnlocked"" }
    ]
  },
  ""segments"": [{");
            string rewards = RewardsJson
                .Replace(
                    @"""schemaVersion"": 1",
                    @"""schemaVersion"": 5")
                .Replace(
                    @"""optionCount"": 3,",
                    @"""optionCount"": 3,
  ""maxCombinedModifierCost"": 4,
  ""rerollCost"": 5,");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves,
                rewards);

            Assert.AreEqual(5, data.Rewards.RerollCost);
            Assert.AreEqual(
                ContractDestinationKind.EndRun,
                data.Contracts.EndRun.DestinationKind);
            Assert.AreEqual(
                ContractDestinationKind.Uncharted,
                data.Contracts.Uncharted.DestinationKind);
            Assert.AreEqual(
                ContractEligibility.HiddenBiomeUnlocked,
                data.Contracts.Uncharted.Eligibility);
            Assert.IsFalse(
                data.Contracts.Uncharted.IsEligible(2, 1, 0));
            Assert.IsTrue(
                data.Contracts.Uncharted.IsEligible(3, 2, 0));
        }

        [Test]
        public void Parse_ContractsPoolsAndRewardCostsAreDataDriven()
        {
            string waves = WavesJson.Replace(
                @"""segments"": [",
                @"""contracts"": {
    ""standardContractId"": ""standard_route"",
    ""minimumOptionCount"": 2,
    ""maximumOptionCount"": 2,
    ""entries"": [
      { ""id"": ""standard_route"", ""weight"": 1, ""riskTier"": ""safe"" },
      {
        ""id"": ""dense_salvage"", ""weight"": 4, ""riskTier"": ""high"",
        ""enemyDensityMultiplier"": 1.5,
        ""capsuleDropMultiplier"": 1.25,
        ""bombDropMultiplier"": 2,
        ""guaranteedBombDrop"": true,
        ""gimmickIntensityMultiplier"": 1.5,
        ""rewardOptionCountDelta"": 1,
        ""scoreMultiplier"": 1.5
      }
    ]
  },
  ""segments"": [");
            string rewards = RewardsJson
                .Replace(
                    @"""schemaVersion"": 1",
                    @"""schemaVersion"": 4")
                .Replace(
                    @"""optionCount"": 3,",
                    @"""optionCount"": 3,
  ""maxCombinedModifierCost"": 4,")
                .Replace(
                    @"""id"": ""capsules_3"", ""type"": ""capsules"", ""amount"": 3,",
                    @"""id"": ""capsules_3"", ""type"": ""capsules"", ""amount"": 3,
      ""pool"": ""mid"",
      ""costs"": [
        { ""type"": ""shieldMaxDown"", ""amount"": 1 },
        { ""type"": ""moveSpeedDown"", ""amount"": 1 },
        { ""type"": ""capsuleDropWeightDown"", ""amount"": 2 },
        { ""type"": ""bombMaxDown"", ""amount"": 1 }
      ],");

            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                waves,
                rewards);

            Assert.IsNotNull(data.Contracts);
            Assert.AreEqual(
                "standard_route",
                data.Contracts.Standard.Id);
            Assert.AreEqual(2, data.Contracts.All.Count);
            Assert.AreEqual(
                RewardPool.Mid,
                data.Rewards.All[0].Pool);
            Assert.AreEqual(
                4,
                data.Rewards.All[0].Costs.Count);
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
        public void Parse_OptionalPlayerMaxEnemyBulletsCopiesValueToBattleConfig()
        {
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                ShipsJson,
                ScoringJson,
                PlayerJson);

            Assert.AreEqual(96, data.CreateBattleSimConfig().MaxEnemyBullets);
        }

        [Test]
        public void Parse_MissingPlayerMaxEnemyBulletsRetainsDefault()
        {
            const string playerWithoutEnemyCap = @"{
  ""schemaVersion"": 1,
  ""player"": {}
}";
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                ShipsJson,
                ScoringJson,
                playerWithoutEnemyCap);

            Assert.AreEqual(128, data.CreateBattleSimConfig().MaxEnemyBullets);
        }

        [Test]
        public void Parse_PlayerRejectsInvalidSchemaAndEnemyBulletCapWithPaths()
        {
            string invalidSchema = PlayerJson.Replace(
                @"""schemaVersion"": 1",
                @"""schemaVersion"": 2");
            GameDataParseException schemaError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        RewardsJson,
                        ShipsJson,
                        ScoringJson,
                        invalidSchema));
            StringAssert.Contains("player.json.schemaVersion", schemaError.Message);

            string negativeCap = PlayerJson.Replace(
                @"""maxEnemyBullets"": 96",
                @"""maxEnemyBullets"": -1");
            GameDataParseException capError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        RewardsJson,
                        ShipsJson,
                        ScoringJson,
                        negativeCap));
            StringAssert.Contains(
                "player.json.player.maxEnemyBullets",
                capError.Message);
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
            Assert.AreEqual(WeaponType.Vulcan, swift.WeaponType);
            Assert.IsFalse(swift.MaxHp.HasValue);
            CollectionAssert.AreEqual(
                new[] { 1, 0, 1, 0, 0, 0, 0, 0 },
                swift.StartingPowerUpLevels);
            Assert.AreEqual(1000L, swift.UnlockCost);
            Assert.IsFalse(data.Ships is ShipDefinition[]);
        }

        [Test]
        public void Parse_ShipsReadsThreeConceptWeaponsAndHullValues()
        {
            const string concepts = @"{
  ""schemaVersion"": 1,
  ""ships"": [
    {
      ""id"": ""starter"", ""displayName"": ""Starter"",
      ""moveSpeedMultiplierNumerator"": 1,
      ""moveSpeedMultiplierDenominator"": 1,
      ""startingPowerUpLevels"": [0, 0, 0, 0],
      ""unlockCost"": 0, ""weaponType"": ""vulcan"", ""maxHp"": 3
    },
    {
      ""id"": ""interceptor"", ""displayName"": ""Interceptor"",
      ""moveSpeedMultiplierNumerator"": 5,
      ""moveSpeedMultiplierDenominator"": 4,
      ""startingPowerUpLevels"": [0, 0, 0, 0],
      ""unlockCost"": 25000, ""weaponType"": ""laser"", ""maxHp"": 2
    },
    {
      ""id"": ""bulwark"", ""displayName"": ""Bulwark"",
      ""moveSpeedMultiplierNumerator"": 4,
      ""moveSpeedMultiplierDenominator"": 5,
      ""startingPowerUpLevels"": [0, 0, 0, 1],
      ""unlockCost"": 50000, ""weaponType"": ""spread"", ""maxHp"": 5
    }
  ]
}";
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                concepts);

            Assert.AreEqual(
                WeaponType.Vulcan,
                data.FindShip("starter").WeaponType);
            Assert.AreEqual(
                3,
                data.FindShip("starter").StartingShieldStock);
            Assert.AreEqual(3, data.FindShip("starter").MaxHp);
            Assert.AreEqual(
                WeaponType.Laser,
                data.FindShip("interceptor").WeaponType);
            Assert.AreEqual(2, data.FindShip("interceptor").MaxHp);
            Assert.AreEqual(
                WeaponType.Spread,
                data.FindShip("bulwark").WeaponType);
            Assert.AreEqual(5, data.FindShip("bulwark").MaxHp);
        }

        [Test]
        public void Parse_ShipsRejectsUnknownWeaponAndNonPositiveHull()
        {
            string unknownWeapon = ShipsJson.Replace(
                @"""unlockCost"": 1000",
                @"""unlockCost"": 1000, ""weaponType"": ""beam""");
            GameDataParseException weaponError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        RewardsJson,
                        unknownWeapon));
            StringAssert.Contains(
                "ships.json.ships[1].weaponType",
                weaponError.Message);

            string zeroHull = ShipsJson.Replace(
                @"""unlockCost"": 1000",
                @"""unlockCost"": 1000, ""maxHp"": 0");
            GameDataParseException hpError =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        WavesJson,
                        RewardsJson,
                        zeroHull));
            StringAssert.Contains(
                "ships.json.ships[1].maxHp",
                hpError.Message);
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
                ReadUtf8(Path.Combine(root, "GameData", "rewards.json")),
                ReadUtf8(Path.Combine(root, "GameData", "ships.json")),
                ReadUtf8(Path.Combine(root, "GameData", "scoring.json")),
                ReadUtf8(Path.Combine(root, "GameData", "player.json")));

            bool hasHiveTentacle =
                data.BattleContent.FindEnemy("hive_tentacle")
                != null;
            EnemyDefinition miniCore =
                data.BattleContent.FindEnemy("mini_core");
            int laserProfileEnemyCount =
                (data.BattleContent.FindEnemy("laser_sentry") != null
                    ? 1
                    : 0)
                + (data.BattleContent.FindEnemy("prism_beamer") != null
                    ? 1
                    : 0);
            Assert.AreEqual(
                (hasHiveTentacle ? 31 : 30)
                    + (miniCore != null ? 1 : 0)
                    + laserProfileEnemyCount,
                data.BattleContent.Enemies.Count);
            if (miniCore != null)
            {
                Assert.IsNotNull(miniCore.MidBossProfile);
                Assert.AreEqual(
                    "core",
                    miniCore.MidBossProfile.ThemeId);
                Assert.GreaterOrEqual(
                    miniCore.MidBossProfile.StageIndexMin,
                    3);
            }
            Assert.AreEqual(4, data.BattleContent.Weapons.Count);
            // REQ-080/081: five missile families (combat trio + downward_drop + homing).
            Assert.AreEqual(5, data.BattleContent.MissileFamilies.Count);
            Assert.AreEqual(3, data.BattleContent.OptionFormations.Count);
            Assert.AreEqual(
                MissileFamily.Straight,
                data.BattleContent.DefaultMissileFamily);
            Assert.AreEqual(
                OptionFormation.Trail,
                data.BattleContent.DefaultOptionFormation);
            Assert.IsNotNull(
                data.BattleContent.FindMissileFamily(
                    MissileFamily.DownwardDrop));
            Assert.IsNotNull(
                data.BattleContent.FindMissileFamily(
                    MissileFamily.Homing));
            Assert.AreEqual(38, data.StageGeneration.Segments.Count);
            bool hasLeviathan = false;
            bool hasBroodmother = false;
            for (int i = 0;
                i < data.StageGeneration.Bosses.Count;
                i++)
            {
                string bossId =
                    data.StageGeneration.Bosses[i].BossId;
                if (bossId
                    == SegmentStageGenerator.LeviathanBossId)
                    hasLeviathan = true;
                else if (bossId
                    == SegmentStageGenerator.BroodmotherBossId)
                    hasBroodmother = true;
            }
            Assert.AreEqual(hasLeviathan, hasBroodmother);
            Assert.AreEqual(
                hasLeviathan ? 7 : 5,
                data.StageGeneration.Bosses.Count);
            Assert.AreEqual(3, data.Rewards.OptionCount);
            // 13 base + 5 missileFamily + 3 optionFormation (REQ-034/081)
            // + bomb_stock_1 (REQ-067)
            // + 5 costed rewards (REQ-071).
            Assert.AreEqual(27, data.Rewards.All.Count);
            // REQ-073: schema v5 exposes capsule reroll cost (provisional §7 = 5).
            Assert.AreEqual(5, data.Rewards.RerollCost);
            Assert.IsNotNull(data.Contracts);
            Assert.AreEqual("standard_route", data.Contracts.Standard.Id);
            // 1 standard + 8 nextStage specialty + end_run + uncharted (REQ-073).
            Assert.AreEqual(11, data.Contracts.All.Count);
            Assert.IsNotNull(data.Contracts.EndRun);
            Assert.AreEqual(
                ContractDestinationKind.EndRun,
                data.Contracts.EndRun.DestinationKind);
            Assert.IsNotNull(data.Contracts.Uncharted);
            Assert.AreEqual(
                ContractDestinationKind.Uncharted,
                data.Contracts.Uncharted.DestinationKind);
            Assert.AreEqual(
                ContractEligibility.HiddenBiomeUnlocked,
                data.Contracts.Uncharted.Eligibility);
            Assert.AreEqual(3, data.Ships.Count);
            // REQ-081 start line: all ships vulcan L0; identity via gauge + missile.
            Assert.AreEqual(
                WeaponType.Vulcan,
                data.FindShip("starter").WeaponType);
            Assert.AreEqual(
                PrimaryWeaponFamily.Double,
                data.FindShip("starter").GaugeWeaponFamily);
            Assert.AreEqual(
                MissileFamily.DownwardDrop,
                data.FindShip("starter").StartingMissileFamily.Value);
            Assert.AreEqual(
                0,
                data.FindShip("starter")
                    .ExportStartingPowerUpLevels()[0]);
            Assert.AreEqual(
                1,
                data.FindShip("starter").StartingShieldStock.Value);
            Assert.AreEqual(1, data.FindShip("starter").MaxHp.Value);
            Assert.AreEqual(
                WeaponType.Vulcan,
                data.FindShip("interceptor").WeaponType);
            Assert.AreEqual(
                PrimaryWeaponFamily.Spread,
                data.FindShip("interceptor").GaugeWeaponFamily);
            Assert.AreEqual(
                MissileFamily.Straight,
                data.FindShip("interceptor").StartingMissileFamily.Value);
            Assert.AreEqual(0, data.FindShip("interceptor").MaxHp.Value);
            Assert.AreEqual(
                WeaponType.Vulcan,
                data.FindShip("bulwark").WeaponType);
            Assert.AreEqual(
                PrimaryWeaponFamily.Laser,
                data.FindShip("bulwark").GaugeWeaponFamily);
            Assert.AreEqual(
                MissileFamily.Homing,
                data.FindShip("bulwark").StartingMissileFamily.Value);
            Assert.AreEqual(2, data.FindShip("bulwark").MaxHp.Value);
            Assert.IsTrue(data.FindShip("starter").HasCustomPowerUpGauge);
            Assert.AreEqual(
                5,
                data.StageGeneration.ClosingSegmentsPerStage);
            Assert.AreEqual(6, data.CreatePowerUpGauge().GetMaxLevel(PowerUpSlot.Speed));
            Assert.AreEqual(6, data.CreatePowerUpGauge().GetMaxLevel(PowerUpSlot.Missile));
            // Content REQ-084 follow-up: Option maxLevel 6 + fixed offsets 6.
            Assert.AreEqual(6, data.CreatePowerUpGauge().GetMaxLevel(PowerUpSlot.Option));
            Assert.AreEqual(6, data.CreatePowerUpGauge().GetMaxLevel(PowerUpSlot.Shield));
            Assert.AreEqual(128, data.CreateBattleSimConfig().MaxEnemyBullets);

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

            ulong[] reportedDuplicateSeeds = { 42UL, 20260729UL };
            for (int seedIndex = 0;
                seedIndex < reportedDuplicateSeeds.Length;
                seedIndex++)
            {
                StagePlan reported = generator.Generate(
                    reportedDuplicateSeeds[seedIndex],
                    1,
                    1);
                Assert.AreEqual(
                    0,
                    reported.SegmentReuseCount,
                    $"seed {reportedDuplicateSeeds[seedIndex]} reused a segment");
                Assert.IsFalse(reported.SegmentReuseApplied);
            }
        }

        [Test]
        public void Parse_WaveSegmentWeightIsOptionalAndMustBePositive()
        {
            string weighted = WavesJson.Replace(
                @"""id"": ""seg"",",
                @"""id"": ""seg"", ""weight"": 37,");
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                weighted);
            Assert.AreEqual(37, data.StageGeneration.Segments[0].Weight);

            string invalid = WavesJson.Replace(
                @"""id"": ""seg"",",
                @"""id"": ""seg"", ""weight"": 0,");
            GameDataParseException error =
                Assert.Throws<GameDataParseException>(
                    () => GameDataParser.Parse(
                        EnemiesJson,
                        WeaponsJson,
                        invalid));
            StringAssert.Contains(
                "waves.json.segments[0].weight",
                error.Message);
        }

        [Test]
        public void RepositoryWaveCatalogSupportsEveryRouteEncounterType()
        {
            string root = FindRepositoryRoot();
            GameDataSet data = GameDataParser.Parse(
                ReadUtf8(Path.Combine(root, "GameData", "enemies.json")),
                ReadUtf8(Path.Combine(root, "GameData", "weapons.json")),
                ReadUtf8(Path.Combine(root, "GameData", "waves.json")),
                ReadUtf8(Path.Combine(root, "GameData", "rewards.json")),
                ReadUtf8(Path.Combine(root, "GameData", "ships.json")),
                ReadUtf8(Path.Combine(root, "GameData", "scoring.json")),
                ReadUtf8(Path.Combine(root, "GameData", "player.json")));
            var generator =
                new SegmentStageGenerator(data.StageGeneration);

            for (int themeIndex = 0;
                themeIndex < data.StageGeneration.ThemeIds.Count;
                themeIndex++)
            {
                string themeId =
                    data.StageGeneration.ThemeIds[themeIndex];
                for (int difficulty = 2;
                    difficulty <= 5;
                    difficulty++)
                {
                    for (int encounter = 0; encounter < 4; encounter++)
                    {
                        var encounterType = (EncounterType)encounter;
                        Assert.IsTrue(
                            generator.CanGenerateRoute(
                                themeId,
                                difficulty,
                                difficulty,
                                encounterType),
                            themeId + " d" + difficulty
                                + " " + encounterType);
                        StagePlan plan = generator.GenerateRoute(
                            0xC0FFEEUL,
                            difficulty,
                            difficulty,
                            themeId,
                            encounterType);
                        Assert.AreEqual(themeId, plan.ThemeId);
                        Assert.AreEqual(
                            encounterType,
                            plan.EncounterType);
                        Assert.IsTrue(
                            StagePlanClearability.IsClearable(plan));
                    }
                }
            }
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

        [Test]
        public void Parse_ShipOwnedGaugeUsesSixSlotsMainShotAndImmediateWeaponSwitch()
        {
            const string shipsJson = @"{
  ""schemaVersion"": 2,
  ""ships"": [{
    ""id"": ""starter"",
    ""displayName"": ""Starter"",
    ""moveSpeedMultiplierNumerator"": 1,
    ""moveSpeedMultiplierDenominator"": 1,
    ""startingPowerUpLevels"": [0, 0, 0, 0],
    ""unlockCost"": 0,
    ""weaponType"": ""vulcan"",
    ""startingShieldStock"": 0,
    ""gaugeWeaponFamily"": ""double"",
    ""powerUpGaugeSlots"": [
      ""Speed"", ""MainShot"", ""Missile"", ""Weapon"", ""Option"", ""Shield""
    ]
  }]
}";
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                WavesJson,
                RewardsJson,
                shipsJson);
            ShipDefinition ship = data.DefaultShip;
            PowerUpGauge gauge = data.CreatePowerUpGauge(ship);

            Assert.AreEqual(0, ship.StartingShieldStock);
            Assert.AreEqual(PrimaryWeaponFamily.Double, ship.GaugeWeaponFamily);
            Assert.AreEqual(PowerUpGauge.ShipGaugeSlotCount, gauge.GaugeSlotCount);
            Assert.AreEqual(PowerUpSlot.Speed, gauge.GaugeSlots[0].Slot);
            Assert.AreEqual(PowerUpSlot.MainShot, gauge.GaugeSlots[1].Slot);
            Assert.AreEqual(PowerUpSlot.Missile, gauge.GaugeSlots[2].Slot);
            Assert.AreEqual(PowerUpSlot.Double, gauge.GaugeSlots[3].Slot);
            Assert.AreEqual(PowerUpSlot.Option, gauge.GaugeSlots[4].Slot);
            Assert.AreEqual(PowerUpSlot.Shield, gauge.GaugeSlots[5].Slot);

            gauge.Collect();
            gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.MainShot));

            PowerUpGaugeSlotView before = gauge.GetGaugeSlotView(3);
            Assert.AreEqual(0, before.Progress);
            Assert.AreEqual(1, before.RequiredCapsules);
            gauge.Collect();
            gauge.Collect();
            gauge.Collect();
            gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
            Assert.AreEqual(PowerUpWeaponMode.Double, gauge.ActiveWeaponMode);
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Double));
            Assert.AreEqual(0, gauge.GetProgress(PowerUpSlot.Double));
            Assert.AreEqual(0, gauge.GetGaugeSlotView(3).RequiredCapsules);
        }

        [Test]
        public void Parse_MaxLevelSixSurvivesGaugeStateStorage()
        {
            string levelSixWeapons = WeaponsJson.Replace(
                @"""maxLevel"": 5",
                @"""maxLevel"": 6");
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                levelSixWeapons,
                WavesJson);
            PowerUpGauge source = data.CreatePowerUpGauge();

            Assert.AreEqual(6, source.GetMaxLevel(PowerUpSlot.MainShot));
            Assert.AreEqual(
                6,
                source.GrantLevels(PowerUpSlot.MainShot, 6));
            int[] stored = source.ExportLevels();
            PowerUpGauge restored = source.CreateEmptyWithSameRules();
            restored.ImportLevels(stored);
            Assert.AreEqual(6, restored.GetLevel(PowerUpSlot.MainShot));
        }

        [Test]
        public void Parse_ClosingSegmentCountBuildsLongerClosingRoute()
        {
            string longerClosing = WavesWithThemes(
                    @"[""hive""]",
                    "hive",
                    "hive")
                .Replace(
                @"""segmentsPerStage"": 1,",
                @"""segmentsPerStage"": 1, ""closingSegmentsPerStage"": 2,");
            longerClosing = longerClosing.Replace(
                @"    ""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }]
  }],
  ""bosses"":",
                @"    ""spawns"": [{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": -5.5 }]
  },{
    ""id"": ""seg2"", ""theme"": ""hive"",
    ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""lengthTicks"": 60, ""entryLaneMask"": 7, ""exitLaneMask"": 7,
    ""traversableLaneMasks"": [7],
    ""spawns"": []
  }],
  ""bosses"":");
            GameDataSet data = GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                longerClosing);
            var generator = new SegmentStageGenerator(data.StageGeneration);

            Assert.AreEqual(1, data.StageGeneration.SegmentsPerStage);
            Assert.AreEqual(2, data.StageGeneration.ClosingSegmentsPerStage);
            Assert.IsTrue(generator.CanGenerateRouteForSection(
                "hive",
                1,
                1,
                EncounterType.Normal,
                StageRouteSection.Closing));
            StagePlan closing = generator.GenerateRouteForSection(
                123UL,
                1,
                1,
                "hive",
                EncounterType.Normal,
                StageRouteSection.Closing);
            Assert.AreEqual(2, closing.Segments.Count);
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
        static void AssertAll(Action assert) => assert();
    }
}
