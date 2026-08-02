using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req120PlayerVisibleBoundsTests
    {
        const ulong FortressSeed = 2UL;
        const int FortressStageIndex = 3;
        const int NormalStageIndex = 1;
        const int DirectionHoldTicks = 600;
        const int SectionReachTickBudget = 60_000;
        const int SternDamageTickBudget = 300;
        const string FortressThemeId = "fortress";
        const string SternPartId = "engine";

        [Test]
        public void RepositoryStageWithWideInjectedBoundsCannotLeaveVisiblePlayfield()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            string themeId = generator.GetThemeOrder(FortressSeed)[
                NormalStageIndex - 1];
            StagePlan plan = generator.GenerateRoute(
                FortressSeed,
                NormalStageIndex,
                StageDifficultyCurve.CreateDefault().GetDifficulty(
                    NormalStageIndex),
                themeId,
                EncounterType.Normal);
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            config.PlayerMinY = -20 * SimSpace.SubUnitsPerWorldUnit;
            config.PlayerMaxY = 20 * SimSpace.SubUnitsPerWorldUnit;
            var battle = new BattleSim(
                config,
                new Rng(FortressSeed),
                plan,
                data.BattleContent,
                data.CreatePowerUpGauge());

            AssertSustainedDirectionWithinVisibleBounds(
                battle,
                -1,
                DirectionHoldTicks,
                "wide-config down");
            AssertSustainedDirectionWithinVisibleBounds(
                battle,
                1,
                DirectionHoldTicks * 2,
                "wide-config up");
        }

        [Test]
        public void RepositoryNormalStageAllSectionsKeepSustainedMovementVisible()
        {
            RunManager run = CreateRepositoryRun(NormalStageIndex);
            RunStageSection[] sections =
            {
                RunStageSection.Opening,
                RunStageSection.MidBoss,
                RunStageSection.Closing,
                RunStageSection.StageBoss
            };

            for (int i = 0; i < sections.Length; i++)
            {
                AdvanceToSection(run, sections[i]);
                BattleSim battle = (BattleSim)run.Battle;
                AssertSustainedDirectionWithinVisibleBounds(
                    battle,
                    -1,
                    DirectionHoldTicks,
                    $"{sections[i]} down");
                AssertSustainedDirectionWithinVisibleBounds(
                    battle,
                    1,
                    DirectionHoldTicks * 2,
                    $"{sections[i]} up");
            }
        }

        [Test]
        public void RepositoryFortressDownwardRegressionRecoversAndDamagesStern()
        {
            RunManager run = CreateRepositoryRun(FortressStageIndex);
            AdvanceToSection(run, RunStageSection.StageBoss);
            BattleSim battle = (BattleSim)run.Battle;

            Assert.AreEqual(FortressThemeId, run.StagePlan.ThemeId);
            Assert.NotNull(run.StagePlan.WarshipEncounter);
            AssertSustainedDirectionWithinVisibleBounds(
                battle,
                -1,
                DirectionHoldTicks,
                "warship down");

            while (!battle.BossActive
                || battle.WarshipActiveGroupIndex != 0)
            {
                Assert.Less(
                    battle.WarshipEncounterTick,
                    run.StagePlan.WarshipEncounter.WarningTicks + 2);
                InputCommand activationInput = FireTowardY(battle, 0);
                run.Step(in activationInput);
                AssertPlayerWithinVisibleBounds(battle, "warship activation");
            }

            int sternIndex = FindPartIndex(battle, SternPartId);
            int hpBefore = battle.BossParts[sternIndex].Hp;
            int damageTick = -1;
            for (int tick = 1; tick <= SternDamageTickBudget; tick++)
            {
                int sternY = battle.BossParts[sternIndex].Y;
                InputCommand input = FireTowardY(battle, sternY);
                run.Step(in input);
                AssertPlayerWithinVisibleBounds(battle, $"stern fire tick {tick}");
                if (battle.BossParts[sternIndex].Hp < hpBefore)
                {
                    damageTick = tick;
                    break;
                }
            }

            TestContext.WriteLine(
                $"warship playerY={battle.PlayerY} "
                + $"sternHp={hpBefore}->{battle.BossParts[sternIndex].Hp} "
                + $"damageTick={damageTick}");
            Assert.Greater(damageTick, 0);
            Assert.Less(battle.BossParts[sternIndex].Hp, hpBefore);
        }

        static void AssertSustainedDirectionWithinVisibleBounds(
            BattleSim battle,
            int moveY,
            int ticks,
            string label)
        {
            var input = new InputCommand(0, moveY, true);
            int observedMin = battle.PlayerY;
            int observedMax = battle.PlayerY;
            for (int tick = 1; tick <= ticks; tick++)
            {
                battle.Step(in input);
                observedMin = Math.Min(observedMin, battle.PlayerY);
                observedMax = Math.Max(observedMax, battle.PlayerY);
                AssertPlayerWithinVisibleBounds(
                    battle,
                    $"{label} tick {tick}");
            }
            TestContext.WriteLine(
                $"{label}: y=[{observedMin},{observedMax}] "
                + $"world=[{observedMin / (double)SimSpace.SubUnitsPerWorldUnit:F4},"
                + $"{observedMax / (double)SimSpace.SubUnitsPerWorldUnit:F4}]");
        }

        static void AssertPlayerWithinVisibleBounds(
            BattleSim battle,
            string label)
        {
            Assert.GreaterOrEqual(
                battle.PlayerY,
                SimSpace.GetVisiblePlayerCenterMinY(
                    BattleSimConfig.CreateDefault().PlayerHalfHeight),
                label);
            Assert.LessOrEqual(
                battle.PlayerY,
                SimSpace.GetVisiblePlayerCenterMaxY(
                    BattleSimConfig.CreateDefault().PlayerHalfHeight),
                label);
        }

        static RunManager CreateRepositoryRun(int startStageIndex)
        {
            GameDataSet data = ParseRepositoryGameData();
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            return new RunManager(
                FortressSeed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                data.Contracts,
                new RunConfig(startStageIndex));
        }

        static void AdvanceToSection(
            RunManager run,
            RunStageSection target)
        {
            for (int tick = 0; tick < SectionReachTickBudget; tick++)
            {
                if (run.State == RunState.Playing
                    && run.StageSection == target)
                    return;
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
                        $"Unexpected run state before {target}: {run.State}.");

                BattleSim battle = (BattleSim)run.Battle;
                InputCommand input = CreateProgressInput(tick, battle);
                run.Step(in input);
                Assert.IsTrue(run.Battle.IsPlayerAlive);
                AssertPlayerWithinVisibleBounds(
                    (BattleSim)run.Battle,
                    $"progress to {target} tick {tick}");
            }

            Assert.Fail($"Section {target} was not reached within the tick budget.");
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
            return FireTowardY(battle, targetY);
        }

        static InputCommand FireTowardY(BattleSim battle, int targetY)
        {
            int moveY = battle.PlayerY < targetY
                ? 1
                : battle.PlayerY > targetY ? -1 : 0;
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
    }
}
