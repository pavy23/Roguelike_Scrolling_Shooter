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
            int checkpoint)
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
                });
        }

        static void AssertPlansEqual(StagePlan expected, StagePlan actual)
        {
            Assert.AreEqual(expected.BossId, actual.BossId);
            Assert.AreEqual(expected.LaneCount, actual.LaneCount);
            Assert.AreEqual(expected.StartLaneMask, actual.StartLaneMask);
            Assert.AreEqual(expected.BossEntryLaneMask, actual.BossEntryLaneMask);
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
