using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-159: 히든 스테이지가 직전 바이옴을 재사용하지 않는다.
    ///
    /// 사람 보고 2026-08-04: "히든 스테이지가 기존 스테이지 재활용인데 각 보스
    /// 타입에 따라 맞는 분위기로 새로 만들어줘."
    ///
    /// 재활용은 사실이었다 — 히든 방은 generationBiomeIndex = BiomeCount로
    /// 마지막 바이옴 테마를 그대로 썼다. 이제 거대 보스가 자기 테마를 들고 온다.
    ///
    /// 이 테스트가 지키는 것은 **연결**이다. 데이터에 테마가 있고 코드가 그것을
    /// 물어볼 수 있어야 하며, 그 테마로 실제 구간이 생성되어야 한다. 셋 중
    /// 하나만 빠져도 화면은 조용히 예전 배경을 그린다 — 크래시가 나지 않으니
    /// 눈으로 보기 전에는 모른다.
    /// </summary>
    public sealed class Req159HiddenStageThemeTests
    {
        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(TestKit.FindRepositoryRoot(), "GameData");
            return GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")),
                File.ReadAllText(Path.Combine(gameData, "rewards.json")),
                File.ReadAllText(Path.Combine(gameData, "ships.json")),
                File.ReadAllText(Path.Combine(gameData, "scoring.json")));
        }

        static readonly ColossalBossKind[] HiddenBosses =
        {
            ColossalBossKind.Leviathan,
            ColossalBossKind.Broodmother
        };

        [Test]
        public void EveryHiddenBossCarriesItsOwnStageTheme()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);

            foreach (ColossalBossKind kind in HiddenBosses)
            {
                string themeId = generator.GetColossalBossThemeId(kind);
                Assert.IsFalse(
                    string.IsNullOrEmpty(themeId),
                    $"{kind}에 전용 테마가 없다 — 접근 구간이 직전 바이옴을 "
                    + "그대로 재사용하게 된다.");
            }
        }

        [Test]
        public void HiddenThemesAreDistinctFromEachOtherAndFromPublicBiomes()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);

            string leviathan =
                generator.GetColossalBossThemeId(ColossalBossKind.Leviathan);
            string broodmother =
                generator.GetColossalBossThemeId(ColossalBossKind.Broodmother);

            Assert.AreNotEqual(
                leviathan,
                broodmother,
                "두 히든 보스가 같은 테마를 쓰면 어느 쪽을 뽑았는지가 "
                + "화면에서 구별되지 않는다.");

            // 히든 테마는 공개 캠페인 순서에 끼어들면 안 된다 — 끼어들면
            // 1~5 바이옴 중 하나가 히든 배경으로 바뀐다. (카탈로그에는 있어야
            // 한다. 검사할 것은 **런 순서**다.)
            IReadOnlyList<string> runOrder = generator.GetThemeOrder(3UL);
            foreach (string hidden in new[] { leviathan, broodmother })
                for (int i = 0; i < runOrder.Count; i++)
                    Assert.AreNotEqual(
                        hidden,
                        runOrder[i],
                        "히든 테마가 공개 바이옴 순서에 들어 있다.");
        }

        [Test]
        public void HiddenApproachGeneratesSegmentsInTheBossTheme()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            // 히든 구간은 최종 바이옴 **다음** 순번으로 생성된다
            // (RunManager: battleSequenceBiomeIndex = BiomeCount + 1).
            int hiddenStageIndex = generator.GetThemeOrder(3UL).Count + 1;

            foreach (ColossalBossKind kind in HiddenBosses)
            {
                string themeId = generator.GetColossalBossThemeId(kind);
                Assert.IsTrue(
                    generator.CanGenerateRoute(
                        themeId,
                        hiddenStageIndex,
                        MaxDifficulty(data),
                        EncounterType.Normal),
                    $"{kind}의 테마 '{themeId}'로 구간을 만들 수 없다 — "
                    + "세그먼트가 그 난이도를 지원하지 않는다.");

                StagePlan plan = generator.GenerateRoute(
                    11UL,
                    hiddenStageIndex,
                    MaxDifficulty(data),
                    themeId,
                    EncounterType.Normal);

                Assert.NotNull(plan);
                Assert.AreEqual(themeId, plan.ThemeId);
                Assert.Greater(
                    plan.Segments.Count,
                    0,
                    "히든 접근 구간이 비어 있다 — 보스 앞에 스테이지가 없다.");
            }
        }

        static int MaxDifficulty(GameDataSet data)
        {
            int max = 1;
            var bosses = data.StageGeneration.Bosses;
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].DifficultyMax > max)
                    max = bosses[i].DifficultyMax;
            return max;
        }
    }
}
