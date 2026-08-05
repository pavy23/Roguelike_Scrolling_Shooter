using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req119WarshipPlayerContinuityTests
    {
        /// <summary>등장(경고 + 화면 진입)에 허용하는 최대 시간.</summary>
        const int EntranceTickBudget = 1_800;

        const ulong FortressSeed = 2UL;
        const int FortressStageIndex = 3;
        const string FortressThemeId = "fortress";
        const string FortressBossId = "boss_fortress";
        const string SternPartId = "engine";
        const int BossReachTickBudget = 60_000;
        const int AutoFireDamageTickBudget = 240;

        [Test]
        public void RepositoryFortressWarshipRoomAndGateKeepPlayerFrameContinuous()
        {
            RunManager run = CreateFortressRun();
            RoomBoundarySample boundary = AdvanceToFortressBoss(run);
            BattleSim battle = (BattleSim)run.Battle;

            Assert.AreEqual(boundary.PlayerX, battle.PlayerX);
            Assert.AreEqual(boundary.PlayerY, battle.PlayerY);
            Assert.AreEqual(boundary.ScrollX, battle.ScrollX);
            Assert.AreEqual(0, battle.Tick);

            int expectedPlayerX = battle.PlayerX;
            int expectedPlayerY = battle.PlayerY;
            long expectedScrollX = battle.ScrollX;
            int warningTicks = run.StagePlan.WarshipEncounter.WarningTicks;

            for (int tick = 1;
                tick <= EntranceTickBudget
                    && battle.WarshipActiveGroupIndex < 0;
                tick++)
            {
                run.Step(InputCommand.None);
                Assert.AreEqual(
                    expectedPlayerX,
                    battle.PlayerX,
                    $"player X discontinuity at warship tick {tick}");
                Assert.AreEqual(
                    expectedPlayerY,
                    battle.PlayerY,
                    $"player Y discontinuity at warship tick {tick}");
                Assert.AreEqual(
                    expectedScrollX,
                    battle.ScrollX,
                    $"scroll reference discontinuity at warship tick {tick}");
            }

            TestContext.WriteLine(
                $"roomBoundary player=({boundary.PlayerX},{boundary.PlayerY}) "
                + $"scroll={boundary.ScrollX}; gate player=({battle.PlayerX},"
                + $"{battle.PlayerY}) scroll={battle.ScrollX} "
                + $"warshipTick={battle.WarshipEncounterTick}");

            Assert.IsTrue(battle.BossActive);
            Assert.AreEqual(0, battle.WarshipActiveGroupIndex);
            Assert.IsTrue(battle.IsPlayerAlive);
        }

        [Test]
        public void RepositoryFortressAutoFireDamagesSternSoonAfterGateActivation()
        {
            RunManager run = CreateFortressRun();
            AdvanceToFortressBoss(run);
            BattleSim battle = (BattleSim)run.Battle;
            int warningTicks = run.StagePlan.WarshipEncounter.WarningTicks;
            long shotsFiredAtRoomEntry = battle.Statistics.ShotsFired;
            long shotsHitAtRoomEntry = battle.Statistics.ShotsHit;

            for (int tick = 0;
                tick < warningTicks
                    || battle.WarshipActiveGroupIndex != 0;
                tick++)
            {
                // 1막은 경고가 끝난 **뒤 함체가 화면에 들어와야** 열린다.
                // 경고 틱 + 2로 잡아 두었더니 등장 거리를 늘린 순간 깨졌다 —
                // 여기서 확인할 것은 "곧 열린다"이지 "몇 틱에 열린다"가 아니다.
                Assert.Less(tick, EntranceTickBudget);
                InputCommand input = FireTowardStern(battle);
                run.Step(in input);
                Assert.IsTrue(battle.IsPlayerAlive);
            }

            int sternIndex = FindPartIndex(battle, SternPartId);
            int hpBefore = battle.BossParts[sternIndex].Hp;
            int damageTick = -1;

            for (int tick = 1;
                tick <= AutoFireDamageTickBudget;
                tick++)
            {
                InputCommand input = FireTowardStern(battle);
                run.Step(in input);
                Assert.IsTrue(battle.IsPlayerAlive);
                if (battle.BossParts[sternIndex].Hp < hpBefore)
                {
                    damageTick = tick;
                    break;
                }
            }

            int hpAfter = battle.BossParts[sternIndex].Hp;
            TestContext.WriteLine(
                $"gate player=({battle.PlayerX},{battle.PlayerY}) "
                + $"sternHp={hpBefore}->{hpAfter} damageTick={damageTick} "
                + $"shots={shotsFiredAtRoomEntry}->{battle.Statistics.ShotsFired} "
                + $"hits={shotsHitAtRoomEntry}->{battle.Statistics.ShotsHit}");

            Assert.Greater(damageTick, 0);
            Assert.LessOrEqual(damageTick, AutoFireDamageTickBudget);
            Assert.Less(hpAfter, hpBefore);
            Assert.Greater(
                battle.Statistics.ShotsFired,
                shotsFiredAtRoomEntry);
            Assert.Greater(
                battle.Statistics.ShotsHit,
                shotsHitAtRoomEntry);
        }

        static RunManager CreateFortressRun()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var run = new RunManager(
                FortressSeed,
                generator,
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                data.Contracts,
                new RunConfig(FortressStageIndex));

            Assert.AreEqual(FortressStageIndex, run.BiomeIndex);
            Assert.AreEqual(FortressThemeId, run.StagePlan.ThemeId);
            return run;
        }

        static RoomBoundarySample AdvanceToFortressBoss(RunManager run)
        {
            for (int tick = 0; tick < BossReachTickBudget; tick++)
            {
                if (run.IsBiomeBoss && run.State == RunState.Playing)
                {
                    BattleSim battle = (BattleSim)run.Battle;
                    Assert.AreEqual(FortressThemeId, run.StagePlan.ThemeId);
                    Assert.AreEqual(FortressBossId, run.StagePlan.BossId);
                    Assert.NotNull(run.StagePlan.WarshipEncounter);
                    return new RoomBoundarySample(
                        battle.PlayerX,
                        battle.PlayerY,
                        battle.ScrollX);
                }
                if (run.State == RunState.AwaitingReward)
                {
                    Assert.IsTrue(run.ChooseReward(0));
                    continue;
                }
                if (run.State == RunState.AwaitingContract)
                {
                    Assert.IsTrue(run.ChooseContract(0));
                    continue;
                }
                if (run.State != RunState.Playing)
                    Assert.Fail(
                        $"Unexpected state before fortress boss: {run.State}.");

                BattleSim previousBattle = (BattleSim)run.Battle;
                InputCommand input = CreateProgressInput(tick, previousBattle);
                run.Step(in input);
                Assert.IsTrue(run.Battle.IsPlayerAlive);
                if (run.IsBiomeBoss && run.State == RunState.Playing)
                {
                    return new RoomBoundarySample(
                        previousBattle.PlayerX,
                        previousBattle.PlayerY,
                        previousBattle.ScrollX);
                }
            }

            Assert.Fail("Fortress boss room was not reached within the tick budget.");
            return default;
        }

        static InputCommand CreateProgressInput(int tick, BattleSim battle)
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
                : battle.PlayerY > targetY ? -1 : 0;
            return new InputCommand(
                0,
                moveY,
                true,
                tick % 120 == 0);
        }

        static InputCommand FireTowardStern(BattleSim battle)
        {
            int moveY = battle.PlayerY < 0
                ? 1
                : battle.PlayerY > 0 ? -1 : 0;
            return new InputCommand(0, moveY, true);
        }

        static int FindPartIndex(BattleSim battle, string partId)
        {
            for (int i = 0; i < battle.BossParts.Count; i++)
                if (string.Equals(
                        battle.BossParts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return i;
            Assert.Fail($"Missing boss part '{partId}'.");
            return -1;
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(
                FindRepositoryRoot(),
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

        static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(
                    Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }

        readonly struct RoomBoundarySample
        {
            public RoomBoundarySample(int playerX, int playerY, long scrollX)
            {
                PlayerX = playerX;
                PlayerY = playerY;
                ScrollX = scrollX;
            }

            public int PlayerX { get; }
            public int PlayerY { get; }
            public long ScrollX { get; }
        }
    }
}
