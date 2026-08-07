using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req096DevRunTests
    {
        const int BossReachTickBudget = 60_000;

        [Test]
        public void StageThreeInvulnerableRun_ReachesBossFromDefaultPowerState()
        {
            GameDataSet data = ParseRepositoryGameData();
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            var run = new RunManager(
                0x960096UL,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                gauge,
                data.Rewards,
                data.Contracts,
                new RunConfig(3));

            Assert.AreEqual(3, run.StageIndex);
            Assert.AreEqual(1, run.RoomIndex);
            Assert.IsFalse(run.IsBiomeBoss);
            Assert.IsTrue(run.DevFlagsActive);
            for (int i = 0; i < PowerUpGauge.SlotCount; i++)
                Assert.AreEqual(
                    0,
                    gauge.GetLevel((PowerUpSlot)i));

            int ticks = 0;
            while (ticks < BossReachTickBudget
                && run.State != RunState.RunOver
                && !(run.StageIndex == 3
                    && run.IsBiomeBoss
                    && run.State == RunState.Playing))
            {
                if (run.State == RunState.AwaitingReward)
                {
                    Assert.IsTrue(run.ChooseReward(0));
                    continue;
                }
                if (run.State != RunState.Playing)
                    Assert.Fail(
                        $"Unexpected state before stage 3 boss: {run.State}.");

                BattleSim battle = (BattleSim)run.Battle;
                InputCommand input = CreateInput(ticks, battle);
                run.Step(in input);
                Assert.IsTrue(run.Battle.IsPlayerAlive);
                ticks++;
            }

            TestContext.WriteLine(
                $"stage3 dev smoke ticks={ticks} room={run.RoomIndex} "
                + $"boss={run.IsBiomeBoss} score={run.TotalScore} "
                + $"grazes={run.Statistics.GrazeCount}");
            Assert.Less(ticks, BossReachTickBudget);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(3, run.StageIndex);
            Assert.IsTrue(run.IsBiomeBoss);
            Assert.IsTrue(run.Battle.IsPlayerAlive);
            Assert.Greater(run.Statistics.ShotsFired, 0L);
        }

        static InputCommand CreateInput(int tick, BattleSim battle)
        {
            int targetY;
            if (battle.BossActive)
                targetY = battle.Boss.Y;
            else if (battle.Enemies.Count > 0)
                targetY = battle.Enemies[0].Y;
            else
                targetY = tick % 240 < 120
                    ? SimSpace.PlayfieldHalfHeightSubUnits / 2
                    : -SimSpace.PlayfieldHalfHeightSubUnits / 2;
            int moveY = battle.PlayerY < targetY
                ? 1
                : battle.PlayerY > targetY
                    ? -1
                    : 0;
            bool activate = tick % 120 == 0;
            return new InputCommand(0, moveY, true, activate);
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(
                TestKit.FindRepositoryRoot(),
                "GameData");
            return GameDataParser.Parse(
                Read(gameData, "enemies.json"),
                Read(gameData, "weapons.json"),
                Read(gameData, "waves.json"),
                Read(gameData, "rewards.json"),
                Read(gameData, "ships.json"),
                Read(gameData, "scoring.json"));
        }

        static string Read(string directory, string fileName)
        {
            return File.ReadAllText(Path.Combine(directory, fileName));
        }
    }
}
