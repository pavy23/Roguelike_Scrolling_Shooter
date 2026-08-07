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
    /// REQ-176: 전함 2막에서 **이동 중에는 앞쪽 포탑만** 쏘고, 멈추면 전부 쏜다.
    ///
    /// 사람 지시 2026-08-05: "전함 2페이즈 배치는 좋은데 움직일때 아무런 공격이
    /// 없으니 썰렁하네. 움직일땐 앞의 3개 레이저, 멈추면 6개 전부 레이저 쏘자."
    ///
    /// 이 테스트가 왜 있나: 처음 구현은 "뒤쪽 포탑은 이동 중 쉰다"는 억제 규칙만
    /// 넣었고, 그것만으로 끝났다고 보고했다. 그런데 2막 포탑 주기는 560~1060틱
    /// (9~18초)이고 이동은 240틱(4초)이다. 막이 열릴 때 쿨다운을 통째로 깔면
    /// 이동 4초 동안 **애초에 아무도 발사 시점에 도달하지 못한다.** 억제할 발사가
    /// 없으니 화면은 전과 똑같이 썰렁했고, 사람이 "왜 반영이 안된것 같지?"라고
    /// 되물었다.
    ///
    /// 그래서 검사는 억제가 아니라 **관측된 발사**로 한다. 이동 중에 실제로 빔이
    /// 나갔는가, 그것이 전부 앞쪽에서 나갔는가, 멈춘 뒤에는 뒤쪽도 쏘는가.
    /// 주기·개수 같은 숫자는 적지 않는다 — 전부 데이터에서 읽어 관계로만 본다.
    /// </summary>
    [TestFixture]
    public sealed class Req176WarshipMovingFireTests
    {
        const string FortressThemeId = "fortress";
        const int BossReachTickBudget = 60_000;
        const int ActivationTickBudget = 1_000;
        const int PostArrivalTickBudget = 4_000;

        [Test]
        public void MovingWarshipFiresFromItsFrontHardpointsOnly()
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
            Assert.NotNull(
                run.StagePlan.WarshipEncounter,
                "요새 스테이지 보스에 전함 조우가 붙어 있지 않다.");

            AdvanceUntilAnchored(run, battle);
            Assert.AreEqual(0, battle.WarshipActiveGroupIndex);

            // 1막(midbossGate)을 전멸시켜 2막을 연다. 어떤 파츠가 1막인지는
            // 데이터가 정하므로 "지금 때릴 수 있는 것"으로만 표현한다.
            DestroyEveryVulnerablePart(run, battle);
            Assert.AreEqual(
                1,
                battle.WarshipActiveGroupIndex,
                "1막을 전멸시켰는데 2막이 열리지 않았다.");

            // 2막은 이동으로 시작해야 한다 — 이동이 없으면 이 규칙 자체가
            // 관측 대상이 아니다.
            Assert.Less(
                battle.WarshipAnchorTravelPermille,
                1000,
                "2막이 이동 없이 시작했다 — anchorTravelTicks가 0인가?");

            var frontWhileMoving = new HashSet<int>();
            var rearWhileMoving = new HashSet<int>();
            var seen = new HashSet<int>();
            int movingTicks = 0;

            while (battle.WarshipAnchorTravelPermille < 1000
                && movingTicks < ActivationTickBudget)
            {
                Collect(battle, seen, frontWhileMoving, rearWhileMoving);
                run.Step(InputCommand.None);
                movingTicks++;
            }
            Collect(battle, seen, frontWhileMoving, rearWhileMoving);

            Assert.Greater(
                frontWhileMoving.Count + rearWhileMoving.Count,
                0,
                "함체가 이동하는 동안 포탑이 한 발도 쏘지 않았다 — "
                + "이동 구간이 포탑 주기보다 짧아 발사 시점에 도달하지 못한 것이다.");
            Assert.IsEmpty(
                rearWhileMoving,
                "이동 중에 뒤쪽 포탑이 발사했다 — 뒤쪽은 쉬어야 한다: "
                + string.Join(",", rearWhileMoving));
            Assert.Greater(
                frontWhileMoving.Count,
                1,
                "이동 중에 앞쪽 포탑 하나만 쐈다 — 앞쪽 전체가 참여해야 한다.");

            // 멈춘 뒤에는 뒤쪽도 쏴야 한다 ("멈추면 6개 전부").
            var frontAfter = new HashSet<int>();
            var rearAfter = new HashSet<int>();
            for (int tick = 0; tick < PostArrivalTickBudget; tick++)
            {
                Collect(battle, seen, frontAfter, rearAfter);
                if (battle.WarshipActiveGroupIndex != 1)
                    break;
                run.Step(InputCommand.None);
            }
            Assert.Greater(
                rearAfter.Count,
                0,
                "정박한 뒤에도 뒤쪽 포탑이 쏘지 않았다 — 억제가 풀리지 않는다.");
            Assert.GreaterOrEqual(
                frontAfter.Count,
                frontWhileMoving.Count,
                "정박 후에 앞쪽 포탑이 오히려 줄었다.");
            // 2막은 좌우 대칭 배치다 ("왼쪽 3개 / 오른쪽 3개") — 문 개수를
            // 적는 대신 대칭으로 확인한다.
            Assert.AreEqual(
                frontAfter.Count,
                rearAfter.Count,
                $"정박 후 좌우가 비대칭이다 — 앞{frontAfter.Count} 뒤{rearAfter.Count}.");

            TestContext.WriteLine(
                $"이동 {movingTicks}틱: 앞 {frontWhileMoving.Count}문 발사, 뒤 0문. "
                + $"정박 후: 앞 {frontAfter.Count}문 + 뒤 {rearAfter.Count}문.");
        }

        /// <summary>
        /// 아직 못 본 파츠 레이저를 수집해 앞/뒤로 나눠 담는다.
        ///
        /// 앞/뒤 판정을 **발사된 그 틱에** 하는 것이 중요하다. 함체는 스크롤하며
        /// 좌표가 통째로 흐르기 때문에, 막 시작 때 잡아둔 중심선으로 나중에
        /// 분류하면 전부 한쪽으로 몰린다.
        /// </summary>
        static void Collect(
            BattleSim battle,
            HashSet<int> seen,
            HashSet<int> front,
            HashSet<int> rear)
        {
            int centerX = HardpointCenterX(battle);
            for (int i = 0; i < battle.Lasers.Count; i++)
            {
                LaserState laser = battle.Lasers[i];
                if (laser.SourceKind != LaserSourceKind.BossPart)
                    continue;
                if (!seen.Add(laser.Id))
                    continue;
                int partIndex = laser.SourceEntityId;
                if (partIndex < 0 || partIndex >= battle.BossParts.Count)
                    continue;
                if (battle.BossParts[partIndex].X > centerX)
                    rear.Add(partIndex);
                else
                    front.Add(partIndex);
            }
        }

        /// <summary>
        /// 앞/뒤를 가르는 기준선 = **함체 중심**. Core의 억제 규칙이 파츠
        /// 정의의 offsetX 부호로 판단하므로, 세계 좌표에서 같은 기준은 보스
        /// 본체의 X다.
        ///
        /// 두 번 틀렸다. 활성 파츠의 min/max 중점은 포탑 아닌 파츠에 밀려
        /// 오른쪽으로 치우쳤고, 보스 본체 X는 함체 중심과 아예 다른 값이었다
        /// (본체 12유닛 vs 함체 -9유닛). 여섯 문이 전부 "앞"으로 분류돼
        /// 뒤쪽 발사를 하나도 못 셌다.
        /// </summary>
        static int HardpointCenterX(BattleSim battle)
        {
            return battle.WarshipWorldX;
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
            string gameData = Path.Combine(TestKit.FindRepositoryRoot(), "GameData");
            return GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")),
                File.ReadAllText(Path.Combine(gameData, "rewards.json")),
                File.ReadAllText(Path.Combine(gameData, "ships.json")),
                File.ReadAllText(Path.Combine(gameData, "scoring.json")));
        }
    }
}
