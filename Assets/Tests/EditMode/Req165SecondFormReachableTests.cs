using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-165: 두 번째 형태를 가진 보스는 **보스방 계획에도** 그 형태가 실려야 한다.
    ///
    /// 사람 보고 2026-08-04: "브루드마더는 마지막 페이즈가 없네."
    ///
    /// 원인은 RunManager.CreateBossOnlyPlan이었다. 보스방 계획을 다시 조립하면서
    /// 마지막 인자(form2)를 빠뜨렸고, 빠진 인자는 기본값 null이라 컴파일이 통과했다.
    /// 그래서 **두 번째 형태를 가진 보스가 전부** 첫 형태에서 그냥 죽었다 —
    /// 전함 안에서 나오는 로봇, 최종 보스의 두 번째 형태, 히든 보스 둘의 마지막
    /// 페이즈가 모두 화면에 나온 적이 없다.
    ///
    /// Req163이 히든 둘을 싸워서 확인하지만 그건 비싸고 느리다. 이 테스트는
    /// **모든 보스에 대해** 계획 조립이 form2를 잃지 않는지를 싸우지 않고 본다 —
    /// 같은 실수가 다른 보스에서 나면 여기서 먼저 걸린다.
    /// </summary>
    public sealed class Req165SecondFormReachableTests
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

        [Test]
        public void BossRoomPlanKeepsTheSecondFormForEveryBossThatHasOne()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            var failures = new StringBuilder();

            // 두 번째 형태를 가진 보스가 실제로 있어야 이 검사가 의미를 가진다.
            int withForm2 = 0;
            for (int i = 0; i < data.StageGeneration.Bosses.Count; i++)
                if (data.StageGeneration.Bosses[i].Form2 != null)
                    withForm2++;
            Assert.Greater(
                withForm2,
                0,
                "두 번째 형태를 가진 보스가 하나도 없다 — 검사가 헛돈다.");

            // 히든 보스는 전용 경로(GenerateColossalBoss)로 만들어진다.
            foreach (ColossalBossKind kind in new[]
                { ColossalBossKind.Leviathan, ColossalBossKind.Broodmother })
            {
                StagePlan plan = generator.GenerateColossalBoss(
                    17UL, generator.GetThemeOrder(17UL).Count + 1, 5, kind);
                AssertFormSurvivesBossRoomAssembly(plan, kind.ToString(), failures);
            }

            // 공개 바이옴 보스는 테마 경로로 만들어진다.
            for (int i = 0; i < generator.ThemeIds.Count; i++)
            {
                string themeId = generator.ThemeIds[i];
                if (SegmentStageGenerator.IsHiddenOnlyTheme(themeId)) continue;
                int stageIndex = i + 1;
                int difficulty = StageDifficultyCurve.CreateDefault()
                    .GetDifficulty(stageIndex);
                if (!generator.CanGenerateRoute(
                        themeId, stageIndex, difficulty, EncounterType.Normal))
                    continue;
                StagePlan plan = generator.GenerateRoute(
                    5UL, stageIndex, difficulty, themeId, EncounterType.Normal);
                AssertFormSurvivesBossRoomAssembly(plan, themeId, failures);
            }

            Assert.IsEmpty(
                failures.ToString(),
                "보스방 계획이 두 번째 형태를 잃는다:\n" + failures);
        }

        /// <summary>
        /// 생성기가 준 계획에 form2가 있으면, RunManager가 보스방용으로 다시
        /// 조립한 계획에도 남아 있어야 한다.
        /// </summary>
        static void AssertFormSurvivesBossRoomAssembly(
            StagePlan generated, string label, StringBuilder failures)
        {
            if (generated.Form2 == null) return;   // 이 보스는 두 번째 형태가 없다
            StagePlan bossRoom = RunManager.CreateBiomeBossPlanForTests(generated);
            if (bossRoom.Form2 == null)
                failures.AppendLine(
                    $"{label}: 생성기는 '{generated.Form2.FormId}'를 줬는데 "
                    + "보스방 계획에서 사라졌다.");
            else if (!string.Equals(
                    bossRoom.Form2.FormId,
                    generated.Form2.FormId,
                    StringComparison.Ordinal))
                failures.AppendLine(
                    $"{label}: 두 번째 형태가 바뀌었다 "
                    + $"({generated.Form2.FormId} → {bossRoom.Form2.FormId}).");
        }
    }
}
