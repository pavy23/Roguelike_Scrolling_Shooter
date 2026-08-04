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

            // 1막이 열리는 조건은 **midbossGate 그룹 전체 파괴**다. 어떤 파츠가
            // 그 그룹에 속하는지는 데이터가 정하므로, 이름을 적어두는 대신
            // "지금 때릴 수 있는 것을 다 부순다"로 표현한다.
            bool midBossDefeated = DestroyEveryVulnerablePart(run, battle);
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

        /// <summary>
        /// 지금 때릴 수 있는 파츠를 전부 파괴한다. 반환값은 그 과정에서
        /// MidBossDefeated가 발생했는지 여부.
        /// </summary>
        static bool DestroyEveryVulnerablePart(RunManager run, BattleSim battle)
        {
            bool midBossDefeated = false;
            // **막이 넘어가면 멈춘다.** 소모전도 이제 전멸하면 즉시 다음 막으로
            // 가므로(2026-08-04), 계속 부수면 3막까지 지나가 버린다. 이 헬퍼가
            // 확인하려는 것은 "1막을 다 부수면 2막이 열린다"이다.
            int startGroup = battle.WarshipActiveGroupIndex;
            for (int guard = 0; guard < battle.BossParts.Count + 1; guard++)
            {
                if (battle.WarshipActiveGroupIndex != startGroup)
                    return midBossDefeated;
                int target = -1;
                for (int i = 0; i < battle.BossParts.Count; i++)
                {
                    BossPartState part = battle.BossParts[i];
                    if (!part.Destroyed && !part.Invulnerable && part.Hp > 0)
                    {
                        target = i;
                        break;
                    }
                }
                if (target < 0)
                    return midBossDefeated;

                BossPartState victim = battle.BossParts[target];
                Assert.IsTrue(battle.TrySpawnGhostMainShot(
                    victim.X,
                    victim.Y,
                    victim.Hp));
                run.Step(InputCommand.None);
                midBossDefeated |= HasEvent(
                    battle.EventsThisTick,
                    SimEventType.MidBossDefeated);
            }

            Assert.Fail("Vulnerable warship parts never ran out.");
            return false;
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
            // **정박까지 기다린다.** 그룹이 열렸다고 곧바로 때릴 수 있는 것이
            // 아니다 — 전함은 경고가 끝나도 정박점까지 미끄러져 들어오고, 그
            // 동안은 판정이 없다 (사람 지시 2026-08-04: "전함은 다 등장하고부터
            // 피격판정 있게"). 예전에는 그룹 활성만 보고 곧장 쏴서, 아직 들어오는
            // 중인 함체에 0 데미지가 들어갔다.
            for (int tick = 0; tick < WarshipActivationTickBudget; tick++)
            {
                if (IsSternReady(battle)) return true;
                run.Step(InputCommand.None);
            }
            return IsSternReady(battle);
        }

        static bool IsSternReady(BattleSim battle)
        {
            return battle.BossActive
                && !battle.BossEntering
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
