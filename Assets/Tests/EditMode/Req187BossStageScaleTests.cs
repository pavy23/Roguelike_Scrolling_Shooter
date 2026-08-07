using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-187 (셔플 1안, 사람 승인 2026-08-07): 테마 셔플로 보스가 홈
    /// 스테이지 밖에 등장하면 HP를 스테이지 파워 커브 비율로 맞춘다.
    ///
    /// 검증하는 계약:
    /// 1. 커브가 없으면(기존 데이터) 아무것도 변하지 않는다.
    /// 2. 홈 스테이지 등장은 정확히 1.0 — 기존 밸런스 그대로.
    /// 3. 홈 밖 등장은 curve[등장]/curve[홈] 비율로 본체·부위가 함께
    ///    스케일되고, "부위 합 = 본체" 불변식(REQ-116)이 유지된다.
    /// </summary>
    public sealed class Req187BossStageScaleTests
    {
        const int Lane = 1 << 1;

        [Test]
        public void WithoutCurveBossHpIsUntouched()
        {
            SegmentStageGenerator generator = CreateGenerator(curve: null);

            StagePlan away = generator.GenerateRoute(
                7UL, 1, 2, "beta", EncounterType.Normal);

            Assert.AreEqual(10_000, away.BossMaxHp);
        }

        [Test]
        public void HomeStageAppearanceKeepsExactHp()
        {
            SegmentStageGenerator generator = CreateGenerator(
                curve: new[] { 1000, 2000 });

            StagePlan home = generator.GenerateRoute(
                7UL, 2, 2, "beta", EncounterType.Normal);

            Assert.AreEqual(10_000, home.BossMaxHp);
        }

        [Test]
        public void AwayStageScalesBodyAndPartsKeepingTheSumInvariant()
        {
            SegmentStageGenerator generator = CreateGenerator(
                curve: new[] { 1000, 2000 });

            // beta의 홈은 2스테이지. 1스테이지에 등장하면 1000/2000 = 절반.
            StagePlan away = generator.GenerateRoute(
                7UL, 1, 2, "beta", EncounterType.Normal);

            Assert.AreEqual(5_000, away.BossMaxHp);
            Assert.AreEqual(2, away.BossParts.Count);
            long sum = 0;
            for (int i = 0; i < away.BossParts.Count; i++)
                sum += away.BossParts[i].MaxHp;
            Assert.AreEqual(away.BossMaxHp, sum,
                "부위 합 = 본체 불변식은 스케일 후에도 유지돼야 한다.");
            Assert.AreEqual(3_000, away.BossParts[0].MaxHp);
            Assert.AreEqual(2_000, away.BossParts[1].MaxHp);
        }

        static SegmentStageGenerator CreateGenerator(int[] curve)
        {
            var phases = new[] { new BossPhase(999, 1, 1, 1) };
            var betaParts = new[]
            {
                new BossPartDefinition(
                    "hull", 0, 0, 16, 16, 6_000, false,
                    Array.Empty<string>(), null, 0),
                new BossPartDefinition(
                    "core", 0, 0, 16, 16, 4_000, true,
                    Array.Empty<string>(), null, 0)
            };
            var catalog = new StageGenerationCatalog(
                3,
                1,
                Lane,
                new[]
                {
                    Segment("alpha_segment", "alpha"),
                    Segment("beta_segment", "beta")
                },
                new[]
                {
                    new StageBossTemplate(
                        "alpha_boss", 1, 10, 1, 5, Lane,
                        8_000, 16, 16, 0, phases, "alpha", null),
                    new StageBossTemplate(
                        "beta_boss", 1, 10, 1, 5, Lane,
                        10_000, 16, 16, 0, phases, "beta", betaParts)
                },
                new[] { "alpha", "beta" },
                stagePowerCurvePermille: curve);
            return new SegmentStageGenerator(catalog);
        }

        static StageSegmentTemplate Segment(string id, string themeId)
        {
            return new StageSegmentTemplate(
                id,
                1,
                5,
                600,
                Lane,
                Lane,
                new[] { Lane },
                Array.Empty<SpawnEvent>(),
                themeId);
        }
    }
}
