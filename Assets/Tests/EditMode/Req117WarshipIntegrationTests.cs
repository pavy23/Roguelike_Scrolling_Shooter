using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req117WarshipIntegrationTests
    {
        const string FortressThemeId = "fortress";
        const string FortressBossId = "boss_fortress";
        const string FortressWarshipId = "fortress_warship";
        const string SternPartId = "engine";
        const int BossReachTickBudget = 60_000;
        const int WarshipActivationTickBudget = 1_000;

        [Test]
        public void RepositoryGameDataRunManagerFortressBossActivatesDamageableStern()
        {
            const ulong seed = 2UL;
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            int fortressStageIndex = FindThemeStage(generator, seed, FortressThemeId);

            Assert.AreEqual(3, fortressStageIndex);
            WarshipRunResult result = ExecuteThroughSternDefeat(
                data,
                generator,
                seed,
                fortressStageIndex);

            TestContext.WriteLine(
                $"seed={seed} fortressStage={fortressStageIndex} "
                + $"hash={result.FinalAuditHash:X16}");

            Assert.AreEqual(FortressThemeId, result.ThemeId);
            Assert.AreEqual(FortressBossId, result.BossId);
            Assert.AreEqual(FortressWarshipId, result.WarshipId);
            Assert.Greater(result.SternHpBeforeDamage, result.SternHpAfterDamage);
            Assert.AreEqual(0, result.SternHpAfterDefeat);
            Assert.IsTrue(result.MidBossDefeatedPublished);
        }

        [Test]
        public void RepositoryGameDataShuffledFortressWarshipIsAttachedDeterministically()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            ulong seed = FindSeedWithFortressOutsideCanonicalStage(generator);
            int fortressStageIndex = FindThemeStage(generator, seed, FortressThemeId);

            Assert.AreNotEqual(3, fortressStageIndex);
            WarshipRunResult first = ExecuteThroughSternDefeat(
                data,
                generator,
                seed,
                fortressStageIndex);
            WarshipRunResult repeated = ExecuteThroughSternDefeat(
                data,
                new SegmentStageGenerator(data.StageGeneration),
                seed,
                fortressStageIndex);

            TestContext.WriteLine(
                $"shuffled seed={seed} fortressStage={fortressStageIndex} "
                + $"hash={first.FinalAuditHash:X16} "
                + $"repeat={repeated.FinalAuditHash:X16}");

            Assert.AreEqual(FortressThemeId, first.ThemeId);
            Assert.AreEqual(FortressBossId, first.BossId);
            Assert.AreEqual(FortressWarshipId, first.WarshipId);
            Assert.IsTrue(first.MidBossDefeatedPublished);
            Assert.AreEqual(first.FinalAuditHash, repeated.FinalAuditHash);
            Assert.AreEqual(first.SternHpBeforeDamage, repeated.SternHpBeforeDamage);
            Assert.AreEqual(first.SternHpAfterDamage, repeated.SternHpAfterDamage);
            Assert.AreEqual(first.SternHpAfterDefeat, repeated.SternHpAfterDefeat);
            Assert.AreEqual(
                first.MidBossDefeatedPublished,
                repeated.MidBossDefeatedPublished);
        }

        static WarshipRunResult ExecuteThroughSternDefeat(
            GameDataSet data,
            SegmentStageGenerator generator,
            ulong seed,
            int fortressStageIndex)
        {
            StageBossTemplate catalogBoss = FindBoss(
                data.StageGeneration,
                FortressBossId);
            Assert.NotNull(catalogBoss.WarshipEncounter);
            Assert.AreEqual(
                FortressWarshipId,
                catalogBoss.WarshipEncounter.EncounterId);
            int difficulty = StageDifficultyCurve.CreateDefault()
                .GetDifficulty(fortressStageIndex);
            StagePlan generated = generator.GenerateRoute(
                seed,
                fortressStageIndex,
                difficulty,
                FortressThemeId,
                EncounterType.Normal);
            Assert.NotNull(generated.WarshipEncounter);
            Assert.AreEqual(
                FortressWarshipId,
                generated.WarshipEncounter.EncounterId);

            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var run = new RunManager(
                seed,
                generator,
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                data.Contracts,
                new RunConfig(fortressStageIndex));

            Assert.AreEqual(fortressStageIndex, run.BiomeIndex);
            Assert.AreEqual(FortressThemeId, run.StagePlan.ThemeId);
            AdvanceToBiomeBoss(run);

            Assert.IsTrue(run.IsBiomeBoss);
            Assert.AreEqual(FortressThemeId, run.StagePlan.ThemeId);
            Assert.AreEqual(FortressBossId, run.StagePlan.BossId);
            Assert.NotNull(run.StagePlan.WarshipEncounter);
            Assert.AreEqual(
                FortressWarshipId,
                run.StagePlan.WarshipEncounter.EncounterId);

            BattleSim battle = (BattleSim)run.Battle;
            bool activated = AdvanceToSternActivation(run, battle);
            Assert.IsTrue(activated);
            Assert.AreEqual(0, battle.WarshipActiveGroupIndex);

            int sternIndex = FindPartIndex(battle, SternPartId);
            BossPartState stern = battle.BossParts[sternIndex];
            Assert.IsFalse(stern.Invulnerable);
            int hpBeforeDamage = stern.Hp;
            int firstDamage = Math.Max(1, hpBeforeDamage / 2);
            Assert.IsTrue(battle.TrySpawnGhostMainShot(
                stern.X,
                stern.Y,
                firstDamage));
            run.Step(InputCommand.None);
            int hpAfterDamage = battle.BossParts[sternIndex].Hp;
            Assert.Less(hpAfterDamage, hpBeforeDamage);
            Assert.Greater(hpAfterDamage, 0);

            stern = battle.BossParts[sternIndex];
            Assert.IsTrue(battle.TrySpawnGhostMainShot(
                stern.X,
                stern.Y,
                stern.Hp));
            run.Step(InputCommand.None);
            bool midBossDefeated = HasEvent(
                battle.EventsThisTick,
                SimEventType.MidBossDefeated);
            Assert.IsTrue(battle.BossParts[sternIndex].Destroyed);
            Assert.AreEqual(1, battle.WarshipActiveGroupIndex);

            var hasher = new DeterminismAuditHasher();
            hasher.FoldRunState(run);
            return new WarshipRunResult(
                run.StagePlan.ThemeId,
                run.StagePlan.BossId,
                run.StagePlan.WarshipEncounter.EncounterId,
                hpBeforeDamage,
                hpAfterDamage,
                battle.BossParts[sternIndex].Hp,
                midBossDefeated,
                hasher.Hash);
        }

        static void AdvanceToBiomeBoss(RunManager run)
        {
            int ticks = 0;
            while (ticks < BossReachTickBudget
                && run.State != RunState.RunOver
                && !(run.IsBiomeBoss && run.State == RunState.Playing))
            {
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

                BattleSim battle = (BattleSim)run.Battle;
                InputCommand input = CreateProgressInput(ticks, battle);
                run.Step(in input);
                Assert.IsTrue(run.Battle.IsPlayerAlive);
                ticks++;
            }

            Assert.Less(ticks, BossReachTickBudget);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.IsTrue(run.IsBiomeBoss);
        }

        static bool AdvanceToSternActivation(
            RunManager run,
            BattleSim battle)
        {
            for (int tick = 0; tick < WarshipActivationTickBudget; tick++)
            {
                if (battle.BossActive
                    && battle.WarshipActiveGroupIndex == 0)
                    return true;
                run.Step(InputCommand.None);
            }
            return battle.BossActive
                && battle.WarshipActiveGroupIndex == 0;
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

        static StageBossTemplate FindBoss(
            StageGenerationCatalog catalog,
            string bossId)
        {
            for (int i = 0; i < catalog.Bosses.Count; i++)
                if (string.Equals(
                        catalog.Bosses[i].BossId,
                        bossId,
                        StringComparison.Ordinal))
                    return catalog.Bosses[i];
            Assert.Fail($"Missing boss '{bossId}' in parsed catalog.");
            return null;
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

        static bool HasEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        static ulong FindSeedWithFortressOutsideCanonicalStage(
            SegmentStageGenerator generator)
        {
            for (ulong seed = 0; seed < 64; seed++)
                if (FindThemeStage(generator, seed, FortressThemeId) != 3)
                    return seed;
            Assert.Fail("No shuffled fortress seed found in the audit range.");
            return 0;
        }

        static int FindThemeStage(
            SegmentStageGenerator generator,
            ulong seed,
            string themeId)
        {
            var order = generator.GetThemeOrder(seed);
            for (int i = 0; i < order.Count; i++)
                if (string.Equals(
                        order[i],
                        themeId,
                        StringComparison.Ordinal))
                    return i + 1;
            Assert.Fail($"Missing theme '{themeId}' in generated order.");
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

        readonly struct WarshipRunResult
        {
            public WarshipRunResult(
                string themeId,
                string bossId,
                string warshipId,
                int sternHpBeforeDamage,
                int sternHpAfterDamage,
                int sternHpAfterDefeat,
                bool midBossDefeatedPublished,
                ulong finalAuditHash)
            {
                ThemeId = themeId;
                BossId = bossId;
                WarshipId = warshipId;
                SternHpBeforeDamage = sternHpBeforeDamage;
                SternHpAfterDamage = sternHpAfterDamage;
                SternHpAfterDefeat = sternHpAfterDefeat;
                MidBossDefeatedPublished = midBossDefeatedPublished;
                FinalAuditHash = finalAuditHash;
            }

            public string ThemeId { get; }
            public string BossId { get; }
            public string WarshipId { get; }
            public int SternHpBeforeDamage { get; }
            public int SternHpAfterDamage { get; }
            public int SternHpAfterDefeat { get; }
            public bool MidBossDefeatedPublished { get; }
            public ulong FinalAuditHash { get; }
        }
    }
}
