using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-177: 전함 2막 포탑은 레이저와 **별개 주기로** 일반 탄막도 쏜다.
    ///
    /// 사람 지시 2026-08-05: "전함 페이즈 2에서 레이저와 별개로 일반 탄막도
    /// 발사하게 해줘. 너무 빡빡하진 않게."
    ///
    /// "너무 빡빡하진 않게"가 요구의 절반이다. 그래서 이 테스트는 "탄이 나온다"만
    /// 보지 않는다 — 여섯 문이 한 박자에 몰리지 않는가, 합산 발사율이 회피
    /// 가능한 범위인가까지 본다. 개별 수치는 GROK이 정하므로 여기서는 값을
    /// 베끼지 않고 **관계와 상한**만 못 박는다.
    /// </summary>
    [TestFixture]
    public sealed class Req177WarshipPhase2BurstTests
    {
        const string FortressThemeId = "fortress";
        const int BossReachTickBudget = 60_000;
        const int ActivationTickBudget = 1_000;
        const int ObservationTicks = 1_800;

        /// <summary>
        /// 2막 여섯 문이 합쳐서 초당 낼 수 있는 탄 수의 상한.
        ///
        /// 근거는 밸런스 수치가 아니라 **설계 의도**다. 그 여섯 문은 이미 화면
        /// 폭을 가로지르는 굵은 빔을 쏘고, 플레이어는 빔 사이 틈으로 피해야 한다.
        /// 그 틈을 탄으로 다시 메우면 회피 경로가 사라진다. 초당 8발이면 6문이
        /// 평균 0.75초에 한 번씩 내는 셈으로, 빔 회피 중에도 반응할 여지가 남는다.
        /// GROK이 이 선을 넘기면 값이 아니라 의도가 어긋난 것이다.
        /// </summary>
        const double MaxCombinedBulletsPerSecond = 8.0;

        [Test]
        public void EveryPhaseTwoTurretCarriesAnUnsynchronizedBurst()
        {
            GameDataSet data = ParseRepositoryGameData();
            StageBossTemplate warship = FindWarshipBoss(data);
            IReadOnlyList<string> phaseTwo = FindAttritionPartIds(warship);
            Assert.Greater(phaseTwo.Count, 1, "2막 파츠를 찾지 못했다.");

            var missing = new StringBuilder();
            var cycles = new List<int>();
            double bulletsPerSecond = 0.0;
            for (int i = 0; i < phaseTwo.Count; i++)
            {
                BossPartDefinition part = FindPart(warship, phaseTwo[i]);
                BossPartBurstDefinition burst = part.Attack.SecondaryBurst;
                if (burst == null)
                {
                    missing.AppendLine($"{part.PartId}: secondaryBurst가 없다.");
                    continue;
                }
                cycles.Add(burst.CycleIntervalTicks);
                bulletsPerSecond += burst.Ways
                    * (double)SimSpace.TicksPerSecond
                    / burst.CycleIntervalTicks;

                // 한 문이 1초 안에 두 번 쏘면 레이저 예고를 읽을 틈이 없다.
                Assert.GreaterOrEqual(
                    burst.CycleIntervalTicks,
                    SimSpace.TicksPerSecond,
                    $"{part.PartId}의 탄막 주기가 1초 미만이다.");
            }
            Assert.IsEmpty(
                missing.ToString(),
                "2막 포탑에 탄막이 빠졌다:\n" + missing);

            // 여섯 문이 같은 주기면 한 박자에 몰려 벽이 된다.
            var distinct = new HashSet<int>(cycles);
            Assert.AreEqual(
                cycles.Count,
                distinct.Count,
                "2막 포탑들의 탄막 주기가 겹친다 — 동시에 쏘면 벽이 된다: "
                + string.Join(",", cycles));

            Assert.LessOrEqual(
                bulletsPerSecond,
                MaxCombinedBulletsPerSecond,
                $"2막 합산 탄막이 초당 {bulletsPerSecond:F2}발이다 — "
                + "빔 회피 중에 피할 수 없다.");

            TestContext.WriteLine(
                $"2막 {phaseTwo.Count}문 합산 초당 {bulletsPerSecond:F2}발, "
                + $"주기 {string.Join("/", cycles)}틱.");
        }

        [Test]
        public void PhaseTwoActuallyFiresBulletsAlongsideItsLasers()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            int stageIndex = FindThemeStage(generator, 2UL, FortressThemeId);

            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var run = new RunManager(
                2UL,
                generator,
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                data.Contracts,
                new RunConfig(stageIndex));

            AdvanceToBiomeBoss(run);
            var battle = (BattleSim)run.Battle;
            AdvanceUntilAnchored(run, battle);
            DestroyEveryVulnerablePart(run, battle);
            Assert.AreEqual(
                1,
                battle.WarshipActiveGroupIndex,
                "2막이 열리지 않았다.");

            var seenBullets = new HashSet<int>();
            var seenLasers = new HashSet<int>();
            int spawnedBehindPlayer = 0;
            int observed = 0;
            for (; observed < ObservationTicks; observed++)
            {
                if (battle.WarshipActiveGroupIndex != 1)
                    break;
                for (int i = 0; i < battle.Bullets.Count; i++)
                {
                    BulletState bullet = battle.Bullets[i];
                    if (bullet.Faction != BulletFaction.Enemy)
                        continue;
                    if (!seenBullets.Add(bullet.Id))
                        continue;
                    // 플레이어보다 왼쪽에서 생긴 탄은 플레이어를 향해 날아갈 수
                    // 없다 — 뒤에서 생겨 그대로 화면 밖으로 빠진다.
                    if (bullet.X < battle.PlayerX)
                        spawnedBehindPlayer++;
                }
                for (int i = 0; i < battle.Lasers.Count; i++)
                {
                    if (battle.Lasers[i].SourceKind
                        == LaserSourceKind.BossPart)
                        seenLasers.Add(battle.Lasers[i].Id);
                }
                run.Step(InputCommand.None);
            }

            Assert.Greater(
                seenLasers.Count,
                0,
                "2막에서 레이저가 나오지 않았다 — 관측 자체가 틀렸다.");
            Assert.Greater(
                seenBullets.Count,
                0,
                "2막에서 탄막이 한 발도 나오지 않았다.");

            // "탄이 생성됐다"와 "탄이 보인다"는 다르다. 함체가 왼쪽으로 밀려
            // 서면 앞쪽 포탑이 플레이어보다 왼쪽에 놓이고, 거기서 나간 겨냥탄은
            // 플레이어 뒤에서 생겨 화면 밖으로 빠진다. 실제로 69발 중 24발이
            // 그랬고 사람에게는 "탄막이 전혀 안 보인다"로 보였다.
            Assert.AreEqual(
                0,
                spawnedBehindPlayer,
                $"{seenBullets.Count}발 중 {spawnedBehindPlayer}발이 플레이어 "
                + "뒤에서 생겼다 — 함체가 왼쪽으로 너무 밀려 섰다.");

            // 2막 정지 위치가 화면 중앙 근처여야 한다 (사람 지시 2026-08-05:
            // "좀더 가운데쪽으로"). 절대 좌표 대신 화면 반폭 대비로 본다.
            int restX = battle.WarshipWorldX;
            Assert.LessOrEqual(
                System.Math.Abs(restX),
                SimSpace.PlayfieldHalfWidthSubUnits / 3,
                $"2막 함체가 x={restX / (double)SimSpace.SubUnitsPerWorldUnit:F1}"
                + "유닛에 섰다 — 화면 중앙에서 너무 벗어났다.");

            // 관측 창에서 실측한 발사율도 상한 안이어야 한다. 데이터 검사만으로는
            // 파서가 값을 흘렸는지 알 수 없다.
            double seconds = observed / (double)SimSpace.TicksPerSecond;
            double perSecond = seenBullets.Count / seconds;
            Assert.LessOrEqual(
                perSecond,
                MaxCombinedBulletsPerSecond,
                $"실측 {perSecond:F2}발/초로 상한을 넘었다.");

            TestContext.WriteLine(
                $"2막 {observed}틱 관측: 탄 {seenBullets.Count}발"
                + $"({perSecond:F2}/초), 레이저 {seenLasers.Count}줄.");
        }

        static StageBossTemplate FindWarshipBoss(GameDataSet data)
        {
            var bosses = data.StageGeneration.Bosses;
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].WarshipEncounter != null)
                    return bosses[i];
            Assert.Fail("전함 보스를 찾지 못했다.");
            return null;
        }

        /// <summary>
        /// 2막(소모전) 그룹의 파츠 id. 포탑 이름을 적어 두면 배치가 바뀔 때마다
        /// 테스트가 깨지므로 역할로 찾는다.
        /// </summary>
        static IReadOnlyList<string> FindAttritionPartIds(
            StageBossTemplate warship)
        {
            var groups = warship.WarshipEncounter.Groups;
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].Role == WarshipGroupRole.AttritionLine)
                    return groups[i].PartIds;
            Assert.Fail("소모전 그룹이 없다.");
            return null;
        }

        static BossPartDefinition FindPart(
            StageBossTemplate warship,
            string partId)
        {
            for (int i = 0; i < warship.Parts.Count; i++)
                if (warship.Parts[i].PartId == partId)
                    return warship.Parts[i];
            Assert.Fail($"파츠 '{partId}'를 찾지 못했다.");
            return null;
        }

        static void DestroyEveryVulnerablePart(RunManager run, BattleSim battle)
        {
            int startGroup = battle.WarshipActiveGroupIndex;
            for (int guard = 0; guard < battle.BossParts.Count + 1; guard++)
            {
                if (battle.WarshipActiveGroupIndex != startGroup)
                    return;
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
                    return;
                BossPartState victim = battle.BossParts[target];
                Assert.IsTrue(battle.TrySpawnGhostMainShot(
                    victim.X,
                    victim.Y,
                    victim.Hp));
                run.Step(InputCommand.None);
            }
            Assert.Fail("때릴 수 있는 파츠가 끝나지 않았다.");
        }

        static void AdvanceUntilAnchored(RunManager run, BattleSim battle)
        {
            for (int tick = 0; tick < ActivationTickBudget; tick++)
            {
                if (battle.BossActive
                    && !battle.BossEntering
                    && battle.WarshipActiveGroupIndex == 0)
                    return;
                run.Step(InputCommand.None);
            }
            Assert.Fail("전함이 정박하지 않았다.");
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
                Assert.AreEqual(RunState.Playing, run.State);
                var battle = (BattleSim)run.Battle;
                int targetY;
                if (battle.BossActive)
                    targetY = battle.Boss.Y;
                else if (battle.Enemies.Count > 0)
                    targetY = battle.Enemies[0].Y;
                else
                    targetY = ticks % 240 < 120
                        ? SimSpace.PlayfieldHalfHeightSubUnits / 2
                        : -SimSpace.PlayfieldHalfHeightSubUnits / 2;
                int moveY = battle.PlayerY < targetY
                    ? 1
                    : battle.PlayerY > targetY ? -1 : 0;
                var input = new InputCommand(0, moveY, true, ticks % 120 == 0);
                run.Step(in input);
                ticks++;
            }
            Assert.Less(ticks, BossReachTickBudget);
            Assert.IsTrue(run.IsBiomeBoss);
        }

        static int FindThemeStage(
            SegmentStageGenerator generator,
            ulong seed,
            string themeId)
        {
            var order = generator.GetThemeOrder(seed);
            for (int i = 0; i < order.Count; i++)
                if (string.Equals(
                        order[i], themeId, System.StringComparison.Ordinal))
                    return i + 1;
            Assert.Fail($"'{themeId}' 스테이지를 찾지 못했다.");
            return -1;
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(FindRepositoryRoot(), "GameData");
            return GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")),
                File.ReadAllText(Path.Combine(gameData, "rewards.json")),
                File.ReadAllText(Path.Combine(gameData, "ships.json")),
                File.ReadAllText(Path.Combine(gameData, "scoring.json")));
        }

        static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }
    }
}
