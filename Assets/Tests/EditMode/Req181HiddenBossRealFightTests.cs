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
    /// REQ-181: 히든 보스는 **실제 무기로** 끝까지 잡힌다.
    ///
    /// 사람 보고 2026-08-05: "Hp가 0이 되지 않음".
    ///
    /// 이미 Req163이 "약점을 다 부수면 죽는다"를 검사하고 있었고 통과했다. 그런데
    /// 그 테스트는 **고스트 데미지로 원하는 파츠를 골라 부순다.** 실제 플레이는
    /// 그렇지 않다 — 탄이 닿는 것부터 깎이고, 그 순서 때문에 페이즈가 먼저 넘어가
    /// 아직 덜 부순 파츠가 다시 무적이 되어 잠긴다.
    ///
    /// 브루드마더가 정확히 그렇게 멈췄다: ph1이 열리는 순간 주머니가 다시 무적이
    /// 되고, 남은 HP 10,389(잠긴 주머니 2,494 + 무적 코어 7,895)가 ph2 문턱
    /// 10,000에 **389 차이로** 못 닿아 영원히 갇혔다.
    ///
    /// 그래서 이 테스트는 고르지 않는다. 플레이어를 화면 위아래로 훑게 하고
    /// 쏘게만 한 뒤, HP가 0이 되는지만 본다.
    ///
    /// **두 가지 자세로 잰다.** 보스에 바짝 붙어 쏘는 쪽과 제자리에서 쏘는 쪽이다.
    /// 하나만 재면 그 하나에 맞춰 수치를 잡게 되는데, 붙어서 싸우는 봇은 사람보다
    /// 훨씬 효율적이라 거기에 맞추면 **사람에게는 훨씬 긴 싸움**이 된다. 실제로
    /// 2026-08-05에 그렇게 잡을 뻔했다 — 붙은 봇 100초에 맞췄더니 총 HP가 재조정
    /// 전보다 오히려 늘었다. 사람의 실제 시간은 이 두 값 사이 어딘가다.
    /// </summary>
    [TestFixture]
    public sealed class Req181HiddenBossRealFightTests
    {
        /// <summary>한 보스에 허용하는 최대 관측 시간. 이걸 넘기면 멈춘 것으로 친다.</summary>
        const int FightTickBudget = 40_000;      // 약 11분

        /// <summary>데미지가 이만큼 이어지지 않으면 "더 때릴 것이 없다"로 본다.</summary>
        const int StallTicks = 3_000;            // 50초

        [TestCase(true, TestName = "붙어서 싸운다")]
        [TestCase(false, TestName = "떨어져서 싸운다")]
        public void BothHiddenBossesDieUnderOrdinaryFire(bool closeIn)
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            var seen = new HashSet<ColossalBossKind>();
            var failures = new StringBuilder();

            for (ulong seed = 1; seed <= 24 && seen.Count < 2; seed++)
            {
                BattleSimConfig config = data.CreateBattleSimConfig();
                config.PlayerInvulnerable = true;
                var run = new RunManager(
                    seed, generator, config, data.BattleContent,
                    data.CreatePowerUpGauge(), data.Rewards, data.Contracts,
                    new RunConfig(1, startInHiddenBiome: true));
                if (!seen.Add(run.SelectedColossalBoss))
                    continue;

                // 개발 패널의 power=max와 같은 처리 — 화력 부족으로 인한 실패와
                // 구조적 교착을 가르기 위해 화력은 상한으로 고정한다.
                var maxed = new int[PowerUpGauge.SlotCount];
                for (int i = 0; i < maxed.Length; i++) maxed[i] = int.MaxValue;
                run.PowerUpGauge.ImportLevels(maxed);

                string outcome = FightWithOrdinaryFire(run, closeIn, out int ticks);
                double seconds = ticks / (double)SimSpace.TicksPerSecond;
                TestContext.WriteLine(
                    $"{run.SelectedColossalBoss} ({(closeIn ? "붙어서" : "떨어져서")}): "
                    + $"{seconds:F0}초 ({ticks}틱) → " + (outcome ?? "격파"));
                if (outcome != null)
                    failures.AppendLine(
                        $"{run.SelectedColossalBoss}"
                        + $"({(closeIn ? "붙어서" : "떨어져서")}): {outcome}");
            }

            Assert.AreEqual(2, seen.Count, "거대 보스 두 종류를 다 만나지 못했다.");
            Assert.IsEmpty(
                failures.ToString(),
                "히든 보스를 평범한 사격으로 못 잡는다:\n" + failures);
        }

        /// <summary>
        /// 위아래로 훑으며 계속 쏜다. 성공하면 null, 막히면 그 이유.
        ///
        /// **보스 중심만 따라가면 안 된다.** 처음에 그렇게 짰더니 위쪽 아가리와
        /// 양옆 촉수를 영영 못 때려서, 데이터가 멀쩡한 레비아탄까지 "멈췄다"로
        /// 나왔다. 판정이 어디에 있든 몇 초 안에 한 번은 지나가야 한다.
        /// </summary>
        static string FightWithOrdinaryFire(
            RunManager run, bool closeIn, out int ticks)
        {
            ticks = 0;
            // 접근 구간을 먼저 지난다. 방은 시간만 지나서는 안 넘어가고 적을
            // 치워야 하므로, 쏘면서 간다.
            var approach = new InputCommand(0, 0, true);
            for (int tick = 0; tick < 20_000; tick++)
            {
                if (run.IsHiddenBiome && run.IsBiomeBoss) break;
                if (run.State == RunState.AwaitingReward) { run.ChooseReward(0); continue; }
                if (run.State == RunState.AwaitingContract) { run.ChooseContract(0); continue; }
                if (run.State != RunState.Playing) return $"접근 중 런이 {run.State}로 끝났다.";
                run.Step(in approach);
            }
            if (!(run.IsHiddenBiome && run.IsBiomeBoss))
                return "히든 보스방에 도달하지 못했다.";

            var battle = (BattleSim)run.Battle;
            for (int wait = 0; wait < 1_200 && !battle.BossActive; wait++)
                run.Step(new InputCommand(0, 0, true));
            if (!battle.BossActive)
                return "보스가 나타나지 않았다.";

            int lastHp = battle.Boss.Hp;
            int stalled = 0;
            while (ticks < FightTickBudget)
            {
                if (run.State != RunState.Playing)
                    return run.State == RunState.RunCleared || run.State == RunState.AwaitingReward
                        ? null
                        : $"런이 {run.State}로 끝났다.";
                int moveY = (ticks / 90) % 2 == 0 ? 1 : -1;
                run.Step(new InputCommand(closeIn ? 1 : 0, moveY, true, false));
                ticks++;

                int hp = battle.Boss.Hp;
                if (hp == lastHp) stalled++;
                else { stalled = 0; lastHp = hp; }
                if (!battle.BossActive || hp <= 0)
                    return null;
                if (stalled >= StallTicks)
                    return $"HP {hp}에서 {stalled}틱 동안 한 점도 안 깎인다 "
                        + $"(페이즈 {battle.Boss.Phase}).";
            }
            return $"{FightTickBudget}틱 안에 못 잡았다 (HP {battle.Boss.Hp}).";
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
