using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class LaserGameDataParserTests
    {
        [Test]
        public void ParserBuildsEnemyAndTerrainLaserProfilesAndBombWeights()
        {
            const string enemies = @"{
  ""schemaVersion"": 2,
  ""dropTable"": { ""noDropWeight"": 8, ""bombNoDropWeight"": 97 },
  ""enemies"": [{
    ""id"": ""laser_enemy"", ""displayName"": ""Laser Enemy"", ""hp"": 50,
    ""contactDamage"": 1, ""scoreValue"": 100, ""movePattern"": ""static"",
    ""moveSpeed"": 0, ""fireIntervalTicks"": 0, ""dropWeight"": 1,
    ""bombDropWeight"": 3,
    ""halfWidth"": 0.5, ""halfHeight"": 0.5,
    ""amplitude"": 0, ""periodTicks"": 60,
    ""laser"": {
      ""cycleIntervalTicks"": 60, ""telegraphTicks"": 20,
      ""firingTicks"": 5, ""sustainTicks"": 30, ""dissipateTicks"": 5,
      ""startOffsetX"": 0, ""startOffsetY"": 0,
      ""endOffsetX"": -40, ""endOffsetY"": 0,
      ""thinHalfWidth"": 0.0625, ""fullHalfWidth"": 0.5,
      ""damage"": 1
    }
  }]
}";
            const string weapons = @"{
  ""schemaVersion"": 2,
  ""weapons"": [
    { ""id"": ""main"", ""slot"": ""MainShot"", ""baseDamage"": 1,
      ""fireIntervalTicks"": 8, ""projectileSpeed"": 12,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 5 },
    { ""id"": ""missile"", ""slot"": ""Missile"", ""baseDamage"": 1,
      ""fireIntervalTicks"": 30, ""projectileSpeed"": 6,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 3 },
    { ""id"": ""option"", ""slot"": ""Option"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 4 },
    { ""id"": ""shield"", ""slot"": ""Shield"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 3 }
  ]
}";
            const string waves = @"{
  ""schemaVersion"": 2, ""scrollSpeed"": 3, ""spawnX"": 13,
  ""laneCount"": 3, ""segmentsPerStage"": 1, ""startLaneMask"": 2,
  ""segments"": [{
    ""id"": ""laser_room"", ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""lengthTicks"": 120, ""entryLaneMask"": 7, ""exitLaneMask"": 7,
    ""traversableLaneMasks"": [7],
    ""spawns"": [{ ""tick"": 1, ""enemyId"": ""laser_enemy"", ""y"": 0 }],
    ""obstacles"": [{
      ""type"": ""laserEmitter"", ""x"": 10, ""y"": 5, ""hp"": 0,
      ""laser"": {
        ""cycleIntervalTicks"": 60, ""telegraphTicks"": 20,
        ""firingTicks"": 5, ""sustainTicks"": 30, ""dissipateTicks"": 5,
        ""startOffsetX"": 0, ""startOffsetY"": 0,
        ""endOffsetX"": -40, ""endOffsetY"": 0,
        ""thinHalfWidth"": 0.0625, ""fullHalfWidth"": 0.5,
        ""damage"": 1
      }
    }]
  }],
  ""bosses"": [{
    ""id"": ""boss"", ""stageIndexMin"": 1, ""stageIndexMax"": 1,
    ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""entryLaneMask"": 7, ""hp"": 1
  }]
}";
            const string rewards = @"{
  ""schemaVersion"": 1,
  ""optionCount"": 3,
  ""rewards"": [
    { ""id"": ""bomb_1"", ""type"": ""bombStock"", ""amount"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9 },
    { ""id"": ""capsules_1"", ""type"": ""capsules"", ""amount"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9 },
    { ""id"": ""shield_1"", ""type"": ""shieldStock"", ""amount"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 9 }
  ]
}";

            GameDataSet data =
                GameDataParser.Parse(enemies, weapons, waves, rewards);
            EnemyDefinition enemy =
                data.BattleContent.FindEnemy("laser_enemy");
            StagePlan plan =
                new SegmentStageGenerator(
                    data.StageGeneration).Generate(1UL, 1, 1);

            Assert.AreEqual(97, data.BombNoDropWeight);
            Assert.AreEqual(3, enemy.BombDropWeight);
            Assert.IsNotNull(enemy.LaserAttack);
            Assert.AreEqual(20, enemy.LaserAttack.TelegraphTicks);
            Assert.AreEqual(
                ObstacleType.LaserEmitter,
                plan.Segments[0].Obstacles[0].Type);
            Assert.IsNotNull(
                plan.Segments[0].Obstacles[0].LaserAttack);
            Assert.AreEqual(
                RewardType.BombStock,
                data.Rewards.All[0].Type);
        }
    }
}
