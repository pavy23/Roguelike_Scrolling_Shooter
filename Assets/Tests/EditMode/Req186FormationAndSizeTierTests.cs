using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-186: waves.json의 두 가지 축약 축 (정리 6번, 사람 승인 2026-08-07).
    ///
    /// 1. spawn formation 매크로 — "같은 간격 N마리 줄"을 N줄로 손 전개하지
    ///    않는다. 전개 결과는 손으로 쓴 원자 스폰과 파싱 결과가 동일해야 한다.
    /// 2. obstacleSizeTiers — 장애물 타입별 기본 크기. per-obstacle 크기
    ///    전수 기입(413개 동일값 복붙) 대신 예외만 장애물에 싣는다.
    ///
    /// 두 축 모두 옵셔널이라 기존 데이터 파싱은 변하지 않는다 (본 게임 영향 0
    /// — DeterminismAudit 해시로 별도 확인).
    /// </summary>
    public sealed class Req186FormationAndSizeTierTests
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

        /// <summary>{0} = spawns 배열, {1} = 헤더 추가분, {2} = obstacles 필드.</summary>
        const string WavesTemplate = @"{{
  ""schemaVersion"": 2, ""scrollSpeed"": 3.0, ""spawnX"": 13.0,
  ""laneCount"": 3, ""segmentsPerStage"": 1, ""startLaneMask"": 2,{1}
  ""segments"": [{{
    ""id"": ""seg"", ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""lengthTicks"": 300, ""entryLaneMask"": 7, ""exitLaneMask"": 7,
    ""traversableLaneMasks"": [7],
    ""spawns"": [{0}]{2}
  }}],
  ""bosses"": [{{
    ""id"": ""boss"", ""stageIndexMin"": 1, ""stageIndexMax"": 1,
    ""difficultyMin"": 1, ""difficultyMax"": 5,
    ""entryLaneMask"": 7, ""hp"": 500
  }}]
}}";

        static GameDataSet ParseWaves(
            string spawns, string header = "", string obstacles = "")
        {
            return GameDataParser.Parse(
                EnemiesJson,
                WeaponsJson,
                string.Format(WavesTemplate, spawns, header, obstacles));
        }

        [Test]
        public void LineFormationExpandsExactlyLikeHandWrittenSpawns()
        {
            GameDataSet expanded = ParseWaves(
                @"{ ""formation"": ""line"", ""count"": 3,
                    ""tickStart"": 40, ""tickStep"": 30,
                    ""enemyId"": ""elite_sine"", ""y"": -2.0, ""yStep"": 1.0 }");
            GameDataSet handWritten = ParseWaves(
                @"{ ""tick"": 40, ""enemyId"": ""elite_sine"", ""y"": -2.0 },
                  { ""tick"": 70, ""enemyId"": ""elite_sine"", ""y"": -1.0 },
                  { ""tick"": 100, ""enemyId"": ""elite_sine"", ""y"": 0.0 }");

            var actual = expanded.StageGeneration.Segments[0].Spawns;
            var wanted = handWritten.StageGeneration.Segments[0].Spawns;
            Assert.AreEqual(wanted.Count, actual.Count);
            for (int i = 0; i < wanted.Count; i++)
            {
                Assert.AreEqual(wanted[i].Tick, actual[i].Tick);
                Assert.AreEqual(wanted[i].EnemyId, actual[i].EnemyId);
                Assert.AreEqual(wanted[i].X, actual[i].X);
                Assert.AreEqual(wanted[i].Y, actual[i].Y);
            }
        }

        [Test]
        public void FormationFieldsWithoutFormationKeyAreRejected()
        {
            // count 같은 formation 전용 필드가 원자 스폰에 섞여 있으면 오타다 —
            // 조용히 무시하면 "넣었는데 안 먹힘" 함정이 된다.
            Assert.Throws<GameDataParseException>(() => ParseWaves(
                @"{ ""tick"": 40, ""enemyId"": ""elite_sine"", ""y"": -2.0,
                    ""count"": 3 }"));
        }

        [Test]
        public void FormationRejectsAtomicTickAndUnknownShape()
        {
            Assert.Throws<GameDataParseException>(() => ParseWaves(
                @"{ ""formation"": ""line"", ""count"": 2, ""tick"": 5,
                    ""tickStart"": 40, ""tickStep"": 30,
                    ""enemyId"": ""elite_sine"", ""y"": 0.0 }"));
            Assert.Throws<GameDataParseException>(() => ParseWaves(
                @"{ ""formation"": ""wedge"", ""count"": 2,
                    ""tickStart"": 40, ""tickStep"": 30,
                    ""enemyId"": ""elite_sine"", ""y"": 0.0 }"));
        }

        [Test]
        public void ObstacleSizeTierFillsOnlyUnsizedObstacles()
        {
            GameDataSet data = ParseWaves(
                @"{ ""tick"": 10, ""enemyId"": ""elite_sine"", ""y"": 0.0 }",
                header: @"
  ""obstacleSizeTiers"": [
    { ""type"": ""breakable"", ""halfWidth"": 0.75, ""halfHeight"": 0.5 }
  ],",
                obstacles: @",
    ""obstacles"": [
      { ""type"": ""breakable"", ""x"": 10.0, ""y"": 1.0, ""hp"": 3 },
      { ""type"": ""breakable"", ""x"": 12.0, ""y"": -1.0, ""hp"": 3,
        ""halfWidth"": 1.25, ""halfHeight"": 1.0 },
      { ""type"": ""solid"", ""x"": 14.0, ""y"": 0.0, ""hp"": 0 }
    ]");

            var obstacles = data.StageGeneration.Segments[0].Obstacles;
            int unit = Simulation.SimSpace.SubUnitsPerWorldUnit;
            // 크기 없는 breakable → 티어 기본값.
            Assert.AreEqual(unit * 3 / 4, obstacles[0].HalfWidth);
            Assert.AreEqual(unit / 2, obstacles[0].HalfHeight);
            // 명시한 장애물은 티어보다 우선한다.
            Assert.AreEqual(unit * 5 / 4, obstacles[1].HalfWidth);
            Assert.AreEqual(unit, obstacles[1].HalfHeight);
            // 티어에 없는 타입은 기존 의미 그대로 0 (시뮬 설정 기본값).
            Assert.AreEqual(0, obstacles[2].HalfWidth);
            Assert.AreEqual(0, obstacles[2].HalfHeight);
        }
    }
}
