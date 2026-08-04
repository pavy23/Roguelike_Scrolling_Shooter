using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-158: 멀티파트 보스의 **모든 페이즈에 도달할 수 있는가**.
    ///
    /// 사람 보고 2026-08-04: "히든 보스도 페이즈4를 구경할 수가 없게 되어있네.
    /// 더 이상 공격할 약점이 없는데 HP는 남아있어."
    ///
    /// 원인은 산수였다. 페이즈는 보스 잔여 HP 비율(hpThreshold)로 넘어가는데,
    /// 각 페이즈에서 **때릴 수 있는 파츠**의 HP 합이 다음 문턱까지 깎기에 모자라면
    /// 그 페이즈에서 영원히 멈춘다. 레비아탄이 그랬다:
    ///
    ///     페이즈 0에서 깰 수 있는 것을 다 깨도 잔여 39,000
    ///     페이즈 1 문턱은            31,000   → 시작조차 안 됨
    ///     남은 부위는 전부 무적       → 보스를 죽일 방법이 없음
    ///
    /// 이건 플레이로 찾기 어려운 종류다 — 몇 분을 때린 뒤에야 "안 죽네"로 나타나고,
    /// 그때도 원인이 데이터 산수라는 것은 보이지 않는다. 그래서 수치로 못 박는다.
    /// </summary>
    public sealed class Req158BossPhaseReachabilityTests
    {
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
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }

        [Test]
        public void EveryMultipartBossCanReachEveryPhaseAndDie()
        {
            GameDataSet data = ParseRepositoryGameData();
            var failures = new StringBuilder();

            foreach (StageBossTemplate boss in data.StageGeneration.Bosses)
            {
                if (boss.Parts == null || boss.Parts.Count == 0) continue;
                // 전함 조우는 페이즈가 아니라 **그룹**으로 진행한다 (파츠를 다 부수면
                // 다음 막이 열린다) — hpThreshold 산수의 대상이 아니다.
                if (boss.WarshipEncounter != null) continue;
                if (boss.Phases == null || boss.Phases.Count <= 1) continue;

                var partHp = new Dictionary<string, int>();
                for (int i = 0; i < boss.Parts.Count; i++)
                    partHp[boss.Parts[i].PartId] = boss.Parts[i].MaxHp;

                // 지금까지 어느 페이즈에서든 한 번이라도 때릴 수 있었던 파츠들.
                // 그 합이 곧 "여기까지 깎을 수 있는 최대치"다.
                var everVulnerable = new HashSet<string>();
                for (int phase = 0; phase < boss.Phases.Count; phase++)
                {
                    foreach (string partId in VulnerableParts(boss, phase))
                        everVulnerable.Add(partId);

                    long removable = 0;
                    foreach (string partId in everVulnerable)
                        removable += partHp.TryGetValue(partId, out int hp) ? hp : 0;
                    long floor = boss.MaxHp - removable;

                    // 다음 문턱 — 마지막 페이즈라면 0(격파)까지 가야 한다.
                    double nextThreshold = phase + 1 < boss.Phases.Count
                        ? boss.Phases[phase + 1].HpThresholdNumerator
                          / (double)Math.Max(1, boss.Phases[phase + 1].HpThresholdDenominator)
                        : 0.0;
                    long needed = (long)Math.Floor(boss.MaxHp * nextThreshold);

                    if (floor > needed)
                    {
                        failures.AppendLine(
                            $"{boss.BossId} 페이즈 {phase}: 여기까지 깎을 수 있는 최소 잔여 HP "
                            + $"{floor} > 다음 문턱 {needed} — 다음 페이즈가 시작되지 않는다."
                            + (nextThreshold <= 0.0
                                ? " (마지막 페이즈라 보스를 죽일 수 없다는 뜻이다.)"
                                : string.Empty));
                    }
                }
            }

            Assert.IsEmpty(
                failures.ToString(),
                "도달할 수 없는 보스 페이즈가 있다:\n" + failures);
        }

        /// <summary>이 페이즈에서 실제로 때릴 수 있는 파츠 id.</summary>
        static IEnumerable<string> VulnerableParts(StageBossTemplate boss, int phase)
        {
            BossPhase definition = boss.Phases[phase];
            var rules = definition.PartRules;
            if (rules == null || rules.Count == 0)
            {
                // 규칙이 없으면 전부 때릴 수 있다 (코어 게이트는 별도로 열린다).
                for (int i = 0; i < boss.Parts.Count; i++)
                    yield return boss.Parts[i].PartId;
                yield break;
            }
            for (int i = 0; i < rules.Count; i++)
                if (rules[i].Active && !rules[i].Invulnerable)
                    yield return rules[i].PartId;
        }
    }
}
