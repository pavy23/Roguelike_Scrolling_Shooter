using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class SegmentStageGeneratorTests
    {
        const int Left = 1 << 0;
        const int Center = 1 << 1;
        const int Right = 1 << 2;

        [Test]
        public void SameInputs_ProduceIdenticalStagePlan()
        {
            var generator = CreateGenerator();

            StagePlan first = generator.Generate(0xC0FFEEUL, 1, 2);
            StagePlan second = generator.Generate(0xC0FFEEUL, 1, 2);

            AssertPlansEqual(first, second);
            Assert.IsNull(first.ThemeId);
        }

        [Test]
        public void ThemeRotation_IsOrdinalDeterministicAndFiltersTemplates()
        {
            var catalog = new StageGenerationCatalog(
                3,
                1,
                Center,
                new[]
                {
                    Segment("nebula_segment", Center, Center, Center, "nebula"),
                    Segment("core_segment", Center, Center, Center, "core"),
                    Segment("hive_segment", Center, Center, Center, "hive")
                },
                new[]
                {
                    Boss("nebula_boss", Center, "nebula"),
                    Boss("core_boss", Center, "core"),
                    Boss("hive_boss", Center, "hive")
                });
            var generator = new SegmentStageGenerator(catalog);

            CollectionAssert.AreEqual(
                new[] { "core", "hive", "nebula" },
                catalog.ThemeIds);
            AssertThemePlan(generator.Generate(77UL, 1, 1), "core");
            AssertThemePlan(generator.Generate(77UL, 2, 1), "hive");
            AssertThemePlan(generator.Generate(77UL, 3, 1), "nebula");
            AssertThemePlan(generator.Generate(77UL, 4, 1), "core");
            AssertPlansEqual(
                generator.Generate(77UL, 2, 1),
                generator.Generate(77UL, 2, 1));
        }

        [Test]
        public void UntaggedTemplates_AreEligibleForEveryCatalogTheme()
        {
            var catalog = new StageGenerationCatalog(
                3,
                1,
                Center,
                new[] { Segment("global_segment", Center, Center, Center) },
                new[]
                {
                    Boss("scrapyard_boss", Center, "scrapyard"),
                    Boss("hive_boss", Center, "hive")
                });
            var generator = new SegmentStageGenerator(catalog);

            StagePlan hive = generator.Generate(1UL, 1, 1);
            StagePlan scrapyard = generator.Generate(1UL, 2, 1);

            Assert.AreEqual("hive", hive.ThemeId);
            Assert.AreEqual("global_segment", hive.Segments[0].SegmentId);
            Assert.AreEqual("hive_boss", hive.BossId);
            Assert.AreEqual("scrapyard", scrapyard.ThemeId);
            Assert.AreEqual("global_segment", scrapyard.Segments[0].SegmentId);
            Assert.AreEqual("scrapyard_boss", scrapyard.BossId);
        }

        [Test]
        public void ThemeFilterWithoutClearableAssembly_ThrowsThemeSpecificError()
        {
            var catalog = new StageGenerationCatalog(
                3,
                1,
                Center,
                new[]
                {
                    Segment("hive_dead_end", Center, Left, Center | Left, "hive"),
                    Segment("scrapyard_route", Center, Right, Center | Right, "scrapyard")
                },
                new[]
                {
                    Boss("hive_boss", Right, "hive"),
                    Boss("scrapyard_boss", Right, "scrapyard")
                });
            var generator = new SegmentStageGenerator(catalog);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => generator.Generate(1UL, 1, 1));

            StringAssert.Contains("theme 'hive'", error.Message);
            StringAssert.Contains("clearable stage", error.Message);
        }

        [Test]
        public void GeneratedPlans_AreClearableAcrossManySeeds()
        {
            var generator = CreateGenerator();

            for (ulong seed = 0; seed < 1000; seed++)
            {
                StagePlan plan = generator.Generate(seed, 1, 2);
                Assert.IsTrue(
                    StagePlanClearability.IsClearable(plan),
                    $"seed {seed} produced an unclearable stage");
            }
        }

        [Test]
        public void Generator_LooksAheadAndRejectsLocallyValidDeadEnd()
        {
            var generator = CreateGenerator();

            for (ulong seed = 0; seed < 100; seed++)
            {
                StagePlan plan = generator.Generate(seed, 1, 2);
                Assert.AreEqual("to_right", plan.Segments[0].SegmentId);
                Assert.AreEqual("hold_right", plan.Segments[1].SegmentId);
            }
        }

        [Test]
        public void NoClearableAssembly_ThrowsInsteadOfReturningImpossiblePlan()
        {
            var catalog = new StageGenerationCatalog(
                3,
                1,
                Center,
                new[]
                {
                    Segment("to_left", Center, Left, Center | Left)
                },
                new[]
                {
                    new StageBossTemplate("right_boss", 1, 1, 1, 5, Right)
                });
            var generator = new SegmentStageGenerator(catalog);

            Assert.Throws<InvalidOperationException>(
                () => generator.Generate(1UL, 1, 2));
        }

        [Test]
        public void InvalidInputs_AreRejected()
        {
            var generator = CreateGenerator();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => generator.Generate(1UL, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => generator.Generate(1UL, 1, 0));
        }

        [Test]
        public void BossTemplate_DefensivelyCopiesPhaseList()
        {
            var original = new BossPhase(30, 2, 256, 60);
            var replacement = new BossPhase(10, 5, 512, 60);
            var source = new[] { original };
            var boss = new StageBossTemplate(
                "boss", 1, 5, 1, 5, Center,
                100, 256, 256, 2048, source);

            source[0] = replacement;

            Assert.AreSame(original, boss.Phases[0]);
            Assert.Throws<ArgumentException>(
                () => new StageBossTemplate(
                    "invalid", 1, 5, 1, 5, Center,
                    100, 256, 256, 2048, new BossPhase[] { null }));
        }

        static SegmentStageGenerator CreateGenerator()
        {
            var catalog = new StageGenerationCatalog(
                3,
                2,
                Center,
                new[]
                {
                    Segment("to_left", Center, Left, Center | Left),
                    Segment("to_right", Center, Right, Center | Right),
                    Segment("hold_left", Left, Left, Left),
                    Segment("hold_right", Right, Right, Right)
                },
                new[]
                {
                    new StageBossTemplate("right_boss_a", 1, 1, 1, 5, Right),
                    new StageBossTemplate("right_boss_b", 1, 1, 1, 5, Right)
                });
            return new SegmentStageGenerator(catalog);
        }

        static StageSegmentTemplate Segment(
            string id,
            int entry,
            int exit,
            int checkpoint,
            string themeId = null)
        {
            return new StageSegmentTemplate(
                id,
                1,
                5,
                600,
                entry,
                exit,
                new[] { checkpoint },
                new[]
                {
                    new SpawnEvent(
                        60,
                        "zako_straight",
                        12 * SimSpace.SubUnitsPerWorldUnit,
                        0)
                },
                themeId);
        }

        static StageBossTemplate Boss(string id, int entry, string themeId)
        {
            return new StageBossTemplate(
                id,
                1,
                10,
                1,
                5,
                entry,
                0,
                0,
                0,
                0,
                Array.Empty<BossPhase>(),
                themeId);
        }

        static void AssertThemePlan(StagePlan plan, string expectedTheme)
        {
            Assert.AreEqual(expectedTheme, plan.ThemeId);
            Assert.AreEqual(expectedTheme + "_segment", plan.Segments[0].SegmentId);
            Assert.AreEqual(expectedTheme + "_boss", plan.BossId);
            Assert.IsTrue(StagePlanClearability.IsClearable(plan));
        }

        static void AssertPlansEqual(StagePlan expected, StagePlan actual)
        {
            Assert.AreEqual(expected.ThemeId, actual.ThemeId);
            Assert.AreEqual(expected.BossId, actual.BossId);
            Assert.AreEqual(expected.LaneCount, actual.LaneCount);
            Assert.AreEqual(expected.StartLaneMask, actual.StartLaneMask);
            Assert.AreEqual(expected.BossEntryLaneMask, actual.BossEntryLaneMask);
            Assert.AreEqual(expected.BossMaxHp, actual.BossMaxHp);
            Assert.AreEqual(expected.BossHalfWidth, actual.BossHalfWidth);
            Assert.AreEqual(expected.BossHalfHeight, actual.BossHalfHeight);
            Assert.AreEqual(expected.BossHoldX, actual.BossHoldX);
            Assert.AreEqual(expected.BossPhases.Count, actual.BossPhases.Count);
            for (int i = 0; i < expected.BossPhases.Count; i++)
            {
                Assert.AreEqual(
                    expected.BossPhases[i].FireIntervalTicks,
                    actual.BossPhases[i].FireIntervalTicks);
                Assert.AreEqual(expected.BossPhases[i].Ways, actual.BossPhases[i].Ways);
                Assert.AreEqual(
                    expected.BossPhases[i].BulletSpeedNumerator,
                    actual.BossPhases[i].BulletSpeedNumerator);
                Assert.AreEqual(
                    expected.BossPhases[i].BulletSpeedDenominator,
                    actual.BossPhases[i].BulletSpeedDenominator);
            }
            Assert.AreEqual(expected.Segments.Count, actual.Segments.Count);

            for (int i = 0; i < expected.Segments.Count; i++)
            {
                StageSegment expectedSegment = expected.Segments[i];
                StageSegment actualSegment = actual.Segments[i];
                Assert.AreEqual(expectedSegment.SegmentId, actualSegment.SegmentId);
                Assert.AreEqual(expectedSegment.LengthTicks, actualSegment.LengthTicks);
                Assert.AreEqual(expectedSegment.EntryLaneMask, actualSegment.EntryLaneMask);
                Assert.AreEqual(expectedSegment.ExitLaneMask, actualSegment.ExitLaneMask);
                CollectionAssert.AreEqual(
                    expectedSegment.TraversableLaneMasks,
                    actualSegment.TraversableLaneMasks);
                Assert.AreEqual(expectedSegment.Spawns.Count, actualSegment.Spawns.Count);

                for (int spawn = 0; spawn < expectedSegment.Spawns.Count; spawn++)
                {
                    SpawnEvent expectedSpawn = expectedSegment.Spawns[spawn];
                    SpawnEvent actualSpawn = actualSegment.Spawns[spawn];
                    Assert.AreEqual(expectedSpawn.Tick, actualSpawn.Tick);
                    Assert.AreEqual(expectedSpawn.EnemyId, actualSpawn.EnemyId);
                    Assert.AreEqual(expectedSpawn.X, actualSpawn.X);
                    Assert.AreEqual(expectedSpawn.Y, actualSpawn.Y);
                }
            }
        }
    }
}
