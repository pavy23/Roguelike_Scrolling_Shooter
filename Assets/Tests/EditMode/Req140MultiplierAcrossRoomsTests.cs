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
    /// REQ-140: 배율이 방을 넘어가며 살아남는가.
    ///
    /// 사람이 같은 문제를 네 번 보고했다 — "중간보스 진입 순간 배율이 리셋된다",
    /// "중간보스 이후에는 배수가 아예 동작하지 않는다". 그때마다 규칙을 고쳤지만
    /// 재발했다. 규칙을 또 고치기 전에 **실제 데이터로 돌려서 어디서 떨어지는지
    /// 찍는다.**
    /// </summary>
    public sealed class Req140MultiplierAcrossRoomsTests
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

        static RunManager CreateRun(ulong seed)
        {
            GameDataSet data = ParseRepositoryGameData();
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            return new RunManager(
                seed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                data.CreatePowerUpGauge());
        }

        /// <summary>
        /// 실제 데이터로 1스테이지를 돌며 배율이 언제 오르고 언제 떨어지는지 찍는다.
        /// 판정은 하지 않는다 — 이건 계측이다. 실패 조건은 아래 테스트가 맡는다.
        /// </summary>
        [Test]
        public void TraceMultiplierThroughTheFirstStage()
        {
            RunManager run = CreateRun(20260803UL);
            var log = new StringBuilder();
            var fire = new InputCommand(0, 0, true, false, false);

            RunStageSection section = run.StageSection;
            int level = run.Battle.MultiplierLevel;
            log.AppendLine($"start section={section} level={level}");

            for (int tick = 0; tick < 40000; tick++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    log.AppendLine(
                        $"tick={tick} 보상 선택 (level={run.Battle.MultiplierLevel})");
                    run.ChooseReward(0);
                    continue;
                }
                if (run.State != RunState.Playing)
                {
                    log.AppendLine($"tick={tick} state={run.State} 중단");
                    break;
                }
                run.Step(fire);

                if (run.StageSection != section)
                {
                    section = run.StageSection;
                    log.AppendLine(
                        $"tick={tick} → {section} level={run.Battle.MultiplierLevel} "
                        + $"gauge={run.Battle.ComboGauge}");
                }
                if (run.Battle.MultiplierLevel != level)
                {
                    level = run.Battle.MultiplierLevel;
                    log.AppendLine(
                        $"tick={tick} level={level} (x{run.Battle.ScoreMultiplier}) "
                        + $"section={section}");
                }
                if (section == RunStageSection.Closing && run.BiomeIndex > 0)
                    break;
            }

            TestContext.WriteLine(log.ToString());
            Console.WriteLine(log.ToString());
        }

        /// <summary>
        /// 방이 바뀌어도 배율은 이어진다. 계측으로 확인한 성질을 못 박아 둔다 —
        /// 여기가 깨지면 사람이 보고한 그 증상이 진짜로 Core에서 난 것이다.
        /// </summary>
        [Test]
        public void TheMultiplierSurvivesTheRoomBoundaryIntoTheMidBoss()
        {
            RunManager run = CreateRun(20260803UL);
            var fire = new InputCommand(0, 0, true, false, false);

            int levelBeforeBoundary = 0;
            for (int tick = 0; tick < 40000; tick++)
            {
                if (run.State != RunState.Playing) break;
                int before = run.Battle.MultiplierLevel;
                run.Step(fire);
                if (run.StageSection == RunStageSection.MidBoss)
                {
                    levelBeforeBoundary = before;
                    Assert.AreEqual(
                        before,
                        run.Battle.MultiplierLevel,
                        "중간보스로 넘어가며 배율이 떨어졌다.");
                    break;
                }
            }

            Assert.Greater(
                levelBeforeBoundary,
                0,
                "경계 전에 배율이 올라 있어야 의미 있는 검증이다.");
        }
    }
}
