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

            Assert.AreEqual(768, data.ScrollSpeedNumerator);
            Assert.AreEqual(60, data.ScrollSpeedDenominator);
            Assert.AreEqual(8, data.CapsuleNoDropWeight);

            StageGenerationCatalog stages = data.StageGeneration;
            SpawnEvent spawn = stages.Segments[0].Spawns[0];
            Assert.AreEqual(13 * SimSpace.SubUnitsPerWorldUnit, spawn.X);
            Assert.AreEqual(-11 * SimSpace.SubUnitsPerWorldUnit / 2, spawn.Y);
            Assert.AreEqual(500, stages.Bosses[0].MaxHp);
        }

        [Test]
        public void Factories_UseSchemaValuesAndReturnIndependentState()
        {
            GameDataSet data = GameDataParser.Parse(EnemiesJson, WeaponsJson, WavesJson);
            BattleSimConfig first = data.CreateBattleSimConfig();
            BattleSimConfig second = data.CreateBattleSimConfig();

            Assert.AreEqual(20, first.MissileBaseDamage);
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
        }

        [Test]
        public void RepositoryApprovedV2Files_ParseCompletely()
        {
            string root = FindRepositoryRoot();
            GameDataSet data = GameDataParser.Parse(
                ReadUtf8(Path.Combine(root, "GameData", "enemies.json")),
                ReadUtf8(Path.Combine(root, "GameData", "weapons.json")),
                ReadUtf8(Path.Combine(root, "GameData", "waves.json")));

            Assert.AreEqual(8, data.BattleContent.Enemies.Count);
            Assert.AreEqual(4, data.BattleContent.Weapons.Count);
            Assert.AreEqual(8, data.StageGeneration.Segments.Count);
            Assert.AreEqual(1, data.StageGeneration.Bosses.Count);

            EnemyDefinition elite = data.BattleContent.FindEnemy("elite_sine");
            Assert.AreEqual(2304, elite.SineAmplitudeNumerator);
            Assert.AreEqual(5, elite.SineAmplitudeDenominator);

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
