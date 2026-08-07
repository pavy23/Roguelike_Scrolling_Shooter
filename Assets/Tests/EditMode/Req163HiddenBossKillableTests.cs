using System;
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
    /// REQ-163: 히든 보스를 **끝까지 실제로 때려서** 죽는지 본다.
    ///
    /// 사람 보고 2026-08-04: "마지막 페이즈로 안넘어감. 두 보스 다 약점 파괴후
    /// 아무런 반응이 없음."
    ///
    /// REQ-158에서 페이즈 문턱 산수는 고쳤고 그 테스트는 통과한다. 그런데도
    /// 화면에서는 여전히 안 넘어간다 — 그러면 막히는 곳이 산수가 아니라는 뜻이다.
    /// 산수를 검사하는 테스트로는 영영 못 잡는다. 그래서 여기서는 **싸운다**:
    /// 지금 때릴 수 있는 파츠를 계속 부수고, 보스가 죽고 두 번째 폼으로
    /// 넘어가는지를 본다.
    ///
    /// 이런 테스트가 비싼 이유는 명확하다. 하지만 이 프로젝트에서 "수치는 맞는데
    /// 화면에서는 안 된다"가 반복해서 나왔고, 그때마다 발견 경로가 사람의 플레이
    /// 보고였다. 그 왕복을 없애는 것이 이 파일의 값이다.
    /// </summary>
    public sealed class Req163HiddenBossKillableTests
    {
        const int TickBudget = 200_000;

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
        public void EveryHiddenBossDiesWhenItsWeakPointsAreDestroyed()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            var failures = new StringBuilder();
            var seen = new HashSet<ColossalBossKind>();

            // 어느 거대 보스가 나오는지는 시드가 정한다. 두 종류를 다 보려면
            // 시드를 여러 개 돌려야 한다 — 한 시드만 쓰면 나머지 하나가 영영
            // 검사되지 않고, 그게 바로 이번에 놓친 종류의 구멍이다.
            for (ulong seed = 1; seed <= 24 && seen.Count < HiddenBosses.Length; seed++)
            {
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
                    new RunConfig(1, startInHiddenBiome: true));

                if (!seen.Add(run.SelectedColossalBoss))
                    continue;

                string outcome = FightToTheEnd(run);
                if (outcome != null)
                    failures.AppendLine(
                        $"{run.SelectedColossalBoss} (seed {seed}): {outcome}");
            }

            Assert.AreEqual(
                HiddenBosses.Length,
                seen.Count,
                "시드 24개를 돌려도 거대 보스 두 종류를 다 만나지 못했다.");
            Assert.IsEmpty(
                failures.ToString(),
                "히든 보스를 끝까지 못 잡는다:\n" + failures);
        }

        /// <summary>
        /// 때릴 수 있는 파츠를 계속 부순다. 성공하면 null, 막히면 그 이유.
        /// </summary>
        /// <summary>
        /// 접근 구간을 지나 보스방까지 간다. 이걸 빼먹었더니 테스트가 보스를
        /// 만나지도 못한 채 "진행이 멈췄다"를 뱉었다 — 방은 시간만 지나서는
        /// 안 넘어가고 적을 치워야 한다.
        /// </summary>
        static bool AdvanceToHiddenBoss(RunManager run)
        {
            var fire = new InputCommand(0, 0, true);
            for (int tick = 0; tick < 20_000; tick++)
            {
                if (run.IsHiddenBiome && run.IsBiomeBoss)
                    return true;
                if (run.State == RunState.AwaitingReward)
                    run.ChooseReward(0);
                else if (run.State == RunState.AwaitingContract)
                    run.ChooseContract(0);
                else if (run.State == RunState.Playing)
                    run.Step(in fire);
                else
                    return false;
            }
            return false;
        }

        static string FightToTheEnd(RunManager run)
        {
            if (!AdvanceToHiddenBoss(run))
                return "보스방까지 가지 못했다 (접근 구간에서 막혔다).";
            int ticks = 0;
            int lastProgressTick = 0;
            long lastRemaining = long.MaxValue;
            bool sawSecondForm = false;

            while (ticks < TickBudget)
            {
                // 런이 끝났다고 곧바로 성공이라 하면 안 된다. 예전에는 그렇게
                // 적어 두어서, **두 번째 폼을 한 번도 보지 않고 끝난 경우까지
                // 통과**시켰다 — 사람이 "브루드마더는 마지막 페이즈가 없네"라고
                // 보고한 것을 이 테스트가 놓친 이유가 이것이다.
                if (run.State != RunState.Playing)
                    return sawSecondForm
                        ? null
                        : $"{ticks}틱에 런이 끝났는데(상태 {run.State}) 두 번째 "
                          + $"폼을 한 번도 보지 못했다. 격파 {LastDefeated}, "
                          + $"살아있는 파츠 {LastAlive}, 보스HP {LastBossHp}.";
                if (!(run.Battle is BattleSim battle))
                    return "히든 보스 방인데 BattleSim이 아니다.";

                if (battle.BossFormIndex != 0)
                    sawSecondForm = true;
                if (battle.BossDefeated)
                    return sawSecondForm
                        ? null
                        : "보스는 죽었는데 두 번째 폼으로 넘어가지 않았다 "
                          + "(데이터에는 form2가 있다).";

                if (battle.BossActive)
                {
                    int target = FindVulnerablePart(battle);
                    if (target >= 0)
                    {
                        BossPartState part = battle.BossParts[target];
                        battle.TrySpawnGhostMainShot(part.X, part.Y, part.Hp);
                    }
                    else if (CountAlive(battle) == 0)
                    {
                        // 파츠가 없는 폼(두 번째 폼 등)은 본체를 때린다.
                        BossState boss = battle.Boss;
                        battle.TrySpawnGhostMainShot(boss.X, boss.Y, 10_000);
                    }
                }

                LastDefeated = battle.BossDefeated;
                LastAlive = CountAlive(battle);
                LastBossHp = battle.Boss.Hp;
                run.Step(InputCommand.None);
                ticks++;

                long remaining = RemainingHp(battle);
                if (remaining < lastRemaining)
                {
                    lastRemaining = remaining;
                    lastProgressTick = ticks;
                }
                else if (ticks - lastProgressTick > 1800)
                {
                    return $"{ticks}틱에서 진행이 멈췄다 — 남은 HP {remaining}, "
                        + $"폼 {battle.BossFormIndex}, 때릴 수 있는 파츠 "
                        + $"{CountVulnerable(battle)}개 / 살아 있는 파츠 "
                        + $"{CountAlive(battle)}개, 전환중 {battle.BossTransitioning}"
                        + $"(남은 {battle.BossTransitionTicksRemaining}틱), "
                        + $"활성 {battle.BossActive}, 격파 {battle.BossDefeated}, "
                        + $"런 상태 {run.State}.";
                }
            }

            return $"{TickBudget}틱 안에 끝나지 않았다.";
        }

        static bool LastDefeated;
        static int LastAlive;
        static int LastBossHp;

        static long RemainingHp(BattleSim battle)
        {
            long total = battle.Boss.Hp;
            for (int i = 0; i < battle.BossParts.Count; i++)
                total += battle.BossParts[i].Hp;
            return total;
        }

        static int FindVulnerablePart(BattleSim battle)
        {
            for (int i = 0; i < battle.BossParts.Count; i++)
            {
                BossPartState part = battle.BossParts[i];
                if (!part.Destroyed && !part.Invulnerable && part.Hp > 0)
                    return i;
            }
            return -1;
        }

        static int CountVulnerable(BattleSim battle)
        {
            int count = 0;
            for (int i = 0; i < battle.BossParts.Count; i++)
            {
                BossPartState part = battle.BossParts[i];
                if (!part.Destroyed && !part.Invulnerable && part.Hp > 0)
                    count++;
            }
            return count;
        }

        static int CountAlive(BattleSim battle)
        {
            int count = 0;
            for (int i = 0; i < battle.BossParts.Count; i++)
                if (!battle.BossParts[i].Destroyed)
                    count++;
            return count;
        }
    }
}
