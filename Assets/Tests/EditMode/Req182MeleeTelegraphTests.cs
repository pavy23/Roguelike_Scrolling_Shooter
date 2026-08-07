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
    /// REQ-182: 근접 공격은 **먼저 예고하고 나중에 때린다.**
    ///
    /// 사람 지시 2026-08-05: 레비아탄 낫팔 / 브루드마더 촉수 근접 공격에
    /// "번쩍임 등으로 사전 예고 있음".
    ///
    /// 예고를 넣을 때 이동 쪽만 예고만큼 밀고 접촉 판정을 그대로 뒀더니 창이
    /// 정확히 반대가 됐다 — 멈춰서 경고하는 동안 맞고, 정작 밀고 들어올 때는 안
    /// 맞는다. **예고가 곧 처벌이 되면 예고를 넣은 의미가 없다.**
    ///
    /// 그 짝은 테스트가 아니라 **구조**로 막았다. 두 곳이 각자 식을 쓰는 한 언제든
    /// 다시 어긋나므로 BattleSim.MeleeChargeCycle 하나만 쓰게 했다. 여기서 보는
    /// 것은 데이터다 — 근접인데 예고가 없거나, 반응할 수 없을 만큼 짧거나,
    /// 예고+돌진이 주기를 넘어 돌진이 잘리는 경우.
    /// </summary>
    [TestFixture]
    public sealed class Req182MeleeTelegraphTests
    {
        [Test]
        public void MeleePartsTelegraphBeforeTheyReach()
        {
            GameDataSet data = ParseRepositoryGameData();
            var found = new List<string>();
            var failures = new StringBuilder();

            var bosses = data.StageGeneration.Bosses;
            for (int b = 0; b < bosses.Count; b++)
            {
                StageBossTemplate boss = bosses[b];
                for (int p = 0; p < boss.Phases.Count; p++)
                {
                    IReadOnlyList<BossPhasePartRule> rules = boss.Phases[p].PartRules;
                    for (int r = 0; r < rules.Count; r++)
                    {
                        BossPartAttackProfile attack = rules[r].Attack;
                        if (attack == null
                            || attack.Type != BossPartAttackType.MeleeCharge)
                            continue;
                        string where = $"{boss.BossId}.ph{p}.{rules[r].PartId}";
                        found.Add(where);
                        Check(attack, where, failures);
                    }
                }
                for (int i = 0; i < boss.Parts.Count; i++)
                {
                    BossPartAttackProfile attack = boss.Parts[i].Attack;
                    if (attack.Type != BossPartAttackType.MeleeCharge)
                        continue;
                    string where = $"{boss.BossId}.{boss.Parts[i].PartId}(기본)";
                    found.Add(where);
                    Check(attack, where, failures);
                }
            }

            Assert.IsNotEmpty(
                found,
                "근접 공격을 쓰는 파츠가 하나도 없다 — 검사가 아무것도 안 보고 있다.");
            Assert.IsEmpty(failures.ToString(), failures.ToString());
            TestContext.WriteLine("근접 파츠: " + string.Join(", ", found));
        }

        static void Check(
            BossPartAttackProfile attack, string where, StringBuilder failures)
        {
            // 예고가 없으면 그 파츠는 예고 없이 곧장 온다 — 사람이 요구한 것과 다르다.
            if (attack.MeleeTelegraphTicks <= 0)
            {
                failures.AppendLine($"{where}: 근접인데 예고가 없다.");
                return;
            }
            // 반응할 수 없는 예고는 예고가 아니다. 0.5초를 하한으로 둔다.
            if (attack.MeleeTelegraphTicks < SimSpace.TicksPerSecond / 2)
                failures.AppendLine(
                    $"{where}: 예고가 {attack.MeleeTelegraphTicks}틱뿐이라 "
                    + "반응할 수 없다.");
            // 예고 + 돌진이 주기를 넘으면 돌진이 잘린다. 생성자도 막지만, 데이터가
            // 왜 거부되는지 여기서 이름과 함께 드러나는 편이 훨씬 빨리 고쳐진다.
            int charge = System.Math.Max(1, attack.IntervalTicks / 4);
            if (attack.MeleeTelegraphTicks + charge > attack.IntervalTicks)
                failures.AppendLine(
                    $"{where}: 예고 {attack.MeleeTelegraphTicks} + 돌진 {charge}이 "
                    + $"주기 {attack.IntervalTicks}를 넘는다.");
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
