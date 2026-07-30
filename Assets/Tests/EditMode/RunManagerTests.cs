using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class RunManagerTests
    {
        [Test]
        public void PlayerDeathEndsRunAndStopsFurtherSimulation()
        {
            var manager = CreateManager(
                11UL,
                new TestStageGenerator(true, 5),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            Assert.AreEqual(1, manager.RunNumber);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(RunState.Playing, manager.State);

            manager.Step(in none);

            Assert.AreEqual(0, manager.Battle.PlayerHp);
            Assert.AreEqual(RunState.RunOver, manager.State);
            Assert.AreEqual(1, manager.Battle.Tick);

            manager.Step(in none);
            Assert.AreEqual(1, manager.Battle.Tick);
        }

        [Test]
        public void CompletedRoomsKeepDifficultyAtCurrentBiome()
        {
            var generator = new TestStageGenerator(false, 1, 2);
            var curve = new StageDifficultyCurve(2, 2, 5);
            var manager = new RunManager(
                22UL,
                generator,
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1.0),
                curve);
            InputCommand none = InputCommand.None;

            AssertCall(generator.Calls[0], 22UL, 1, 2);
            Step(manager, 3, in none);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(0, manager.Battle.Tick);
            Assert.AreEqual(2, manager.Difficulty);
            Assert.AreEqual(1, generator.Calls[1].StageIndex);
            Assert.AreEqual(2, generator.Calls[1].Difficulty);

            Step(manager, 3, in none);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(3, manager.RoomIndex);
            Assert.AreEqual(2, manager.Difficulty);
            Assert.AreEqual(1, generator.Calls[2].StageIndex);
            Assert.AreEqual(2, generator.Calls[2].Difficulty);

            Step(manager, 3, in none);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(4, manager.RoomIndex);
            Assert.AreEqual(2, manager.Difficulty);
        }

        [Test]
        public void StageTransitionsCarryAndAccumulateBattleScores()
        {
            var manager = new RunManager(
                88UL,
                new ScoreStageGenerator(false),
                CreateConfig(),
                CreateScoringContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 2, in fire);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(75L, manager.TotalScore);
            Assert.AreEqual(0L, manager.Battle.Score);
            Assert.AreEqual(2L, manager.Statistics.ShotsFired);
            Assert.AreEqual(1L, manager.Statistics.ShotsHit);
            Assert.AreEqual(1L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(1, manager.Statistics.RoomsCleared);

            Step(manager, 2, in fire);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(3, manager.RoomIndex);
            Assert.AreEqual(150L, manager.TotalScore);
            Assert.AreEqual(4L, manager.Statistics.ShotsFired);
            Assert.AreEqual(2L, manager.Statistics.ShotsHit);
            Assert.AreEqual(2L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(2, manager.Statistics.RoomsCleared);
        }

        [Test]
        public void StageTransitionCarriesCollectedCapsules()
        {
            BattleSimConfig config = CreateConfig();
            config.CapsuleHalfWidth = 1;
            var manager = new RunManager(
                91UL,
                new ScoreStageGenerator(false),
                config,
                CreateDroppingContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 2, in fire);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(1L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(1, manager.Statistics.RoomsCleared);
        }

        [Test]
        public void RestartAppliesInjectedDeathCarryAndBuildsFreshFirstStage()
        {
            var initialGauge = PowerUpGauge.CreateDefault();
            initialGauge.ImportLevels(new[] { 5, 3, 4, 3 });
            var generator = new TestStageGenerator(true, 5);
            var manager = new RunManager(
                33UL,
                generator,
                CreateConfig(),
                CreateContent(),
                initialGauge,
                new MetaProgression(0.5),
                StageDifficultyCurve.CreateDefault());
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(44UL);

            Assert.AreEqual(2, manager.RunNumber);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(44UL, manager.RunSeed);
            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreNotSame(initialGauge, manager.PowerUpGauge);
            CollectionAssert.AreEqual(
                new[] { 2, 1, 2, 1 },
                manager.PowerUpGauge.ExportLevels());
            AssertCall(generator.Calls[1], 44UL, 1, 1);
        }

        [Test]
        public void RestartResetsTotalScore()
        {
            var manager = new RunManager(
                89UL,
                new ScoreStageGenerator(true),
                CreateConfig(),
                CreateScoringContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 3, in fire);

            Assert.AreEqual(RunState.RunOver, manager.State);
            Assert.AreEqual(75L, manager.TotalScore);
            Assert.AreEqual(3L, manager.Statistics.ShotsFired);
            Assert.AreEqual(1L, manager.Statistics.ShotsHit);
            Assert.AreEqual(1L, manager.Statistics.Kills);

            manager.Restart(90UL);

            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreEqual(0L, manager.TotalScore);
            Assert.AreEqual(0L, manager.Battle.Score);
            Assert.AreEqual(0L, manager.Statistics.ShotsFired);
            Assert.AreEqual(0L, manager.Statistics.ShotsHit);
            Assert.AreEqual(0L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0L, manager.Statistics.GrazeCount);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
        }

        [Test]
        public void DefaultRestartCarryPreservesAllPowerUpLevels()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 2, 1, 2 });
            var manager = CreateManager(
                55UL,
                new TestStageGenerator(true, 5),
                gauge);
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(56UL);

            CollectionAssert.AreEqual(
                new[] { 3, 2, 1, 2 },
                manager.PowerUpGauge.ExportLevels());
        }

        [Test]
        public void RestartingWithSameSeedRebuildsIdenticalFirstStage()
        {
            var manager = CreateManager(
                77UL,
                new TestStageGenerator(true, 5),
                PowerUpGauge.CreateDefault());
            string firstSegmentId = manager.StagePlan.Segments[0].SegmentId;
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(77UL);

            Assert.AreEqual(firstSegmentId, manager.StagePlan.Segments[0].SegmentId);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(1, manager.Difficulty);
            Assert.AreEqual(0, manager.Battle.Tick);
        }

        [Test]
        public void SameSeedAndInputsReproduceStagesAndDelayedOptionTrajectory()
        {
            BattleSimConfig config = CreateConfig();
            config.OptionFollowDelayTicks = 2;
            var firstGauge = PowerUpGauge.CreateDefault();
            var secondGauge = PowerUpGauge.CreateDefault();
            firstGauge.ImportLevels(new[] { 0, 0, 2, 0 });
            secondGauge.ImportLevels(new[] { 0, 0, 2, 0 });
            var first = new RunManager(
                0xC0FFEEUL,
                new TestStageGenerator(false, 2, 2),
                config,
                CreateContent(),
                firstGauge);
            var second = new RunManager(
                0xC0FFEEUL,
                new TestStageGenerator(false, 2, 2),
                config,
                CreateContent(),
                secondGauge);

            for (int tick = 0; tick < 12; tick++)
            {
                var input = new InputCommand(
                    tick % 4 < 2 ? 1 : -1,
                    tick % 3 == 0 ? 1 : 0,
                    false);
                first.Step(in input);
                second.Step(in input);
                AssertManagersEqual(first, second, tick);
            }
        }

        [Test]
        public void ShipAppliesExactMovementMultiplierAndStartingLevels()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedNumerator = 3;
            config.PlayerSpeedDenominator = 2;
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 1, 0, 0, 0 });
            var ship = new ShipDefinition(
                "swift",
                "Swift",
                4,
                3,
                new[] { 2, 1, 0, 0 },
                100);
            var manager = new RunManager(
                91UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                gauge,
                null,
                ship);
            var moveRight = new InputCommand(1, 0, false);

            manager.Step(in moveRight);

            Assert.AreSame(ship, manager.Ship);
            Assert.AreEqual(2, manager.Battle.PlayerX);
            CollectionAssert.AreEqual(
                new[] { 2, 1, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
        }

        [Test]
        public void RestartNeverDropsBelowShipStartingLevels()
        {
            var ship = new ShipDefinition(
                "armed",
                "Armed",
                1,
                1,
                new[] { 2, 1, 0, 0 },
                0);
            var manager = new RunManager(
                92UL,
                new TestStageGenerator(true, 5),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(0.0),
                StageDifficultyCurve.CreateDefault(),
                null,
                ship);
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(93UL);

            CollectionAssert.AreEqual(
                new[] { 2, 1, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
        }

        static RunManager CreateManager(
            ulong seed,
            IStageGenerator generator,
            PowerUpGauge gauge)
        {
            return new RunManager(
                seed,
                generator,
                CreateConfig(),
                CreateContent(),
                gauge);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -100,
                PlayerMaxX = 100,
                PlayerMinY = -100,
                PlayerMaxY = 100,
                BulletDespawnX = 100,
                EnemyDespawnX = -100,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                StartingShieldStock = 0,
                PlayerHitInvulnerabilityTicks = 0,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }

        static BattleContent CreateContent()
        {
            var enemy = new EnemyDefinition(
                "rammer",
                1,
                10,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var weapon = new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0);
            return new BattleContent(new[] { enemy }, new[] { weapon }, weapon.Id);
        }

        static BattleContent CreateScoringContent()
        {
            var scored = new EnemyDefinition(
                "scored", "Scored", 1, 0, 75, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var lethal = new EnemyDefinition(
                "lethal", "Lethal", 10, 1, 0, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var weapon = new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { scored, lethal },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateDroppingContent()
        {
            var scored = new EnemyDefinition(
                "scored", "Scored", 1, 0, 75, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 1, 0, 1, 1);
            var weapon = new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { scored },
                new[] { weapon },
                weapon.Id);
        }

        static void Step(RunManager manager, int count, in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                manager.Step(in input);
        }

        static void AssertCall(
            GenerationCall call,
            ulong seed,
            int stageIndex,
            int difficulty)
        {
            Assert.AreEqual(seed, call.Seed);
            Assert.AreEqual(stageIndex, call.StageIndex);
            Assert.AreEqual(difficulty, call.Difficulty);
        }

        static void AssertManagersEqual(
            RunManager expected,
            RunManager actual,
            int sourceTick)
        {
            Assert.AreEqual(expected.RunNumber, actual.RunNumber, $"source tick {sourceTick}");
            Assert.AreEqual(expected.StageIndex, actual.StageIndex, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Difficulty, actual.Difficulty, $"source tick {sourceTick}");
            Assert.AreEqual(expected.State, actual.State, $"source tick {sourceTick}");
            Assert.AreEqual(expected.TotalScore, actual.TotalScore, $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.ShotsFired,
                actual.Statistics.ShotsFired,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.ShotsHit,
                actual.Statistics.ShotsHit,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.Kills,
                actual.Statistics.Kills,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.CapsulesCollected,
                actual.Statistics.CapsulesCollected,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.GrazeCount,
                actual.Statistics.GrazeCount,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.StagesCleared,
                actual.Statistics.StagesCleared,
                $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.Tick, actual.Battle.Tick, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.PlayerX, actual.Battle.PlayerX, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.PlayerY, actual.Battle.PlayerY, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.Options.Count, actual.Battle.Options.Count);

            for (int i = 0; i < expected.Battle.Options.Count; i++)
            {
                Assert.AreEqual(expected.Battle.Options[i].Index, actual.Battle.Options[i].Index);
                Assert.AreEqual(expected.Battle.Options[i].X, actual.Battle.Options[i].X);
                Assert.AreEqual(expected.Battle.Options[i].Y, actual.Battle.Options[i].Y);
            }
        }

        sealed class ScoreStageGenerator : IStageGenerator
        {
            readonly bool _lethal;

            public ScoreStageGenerator(bool lethal)
            {
                _lethal = lethal;
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                SpawnEvent[] spawns = _lethal
                    ? new[]
                    {
                        new SpawnEvent(0, "scored", 1, 0),
                        new SpawnEvent(3, "lethal", 0, 0)
                    }
                    : new[] { new SpawnEvent(0, "scored", 1, 0) };
                int lengthTicks = _lethal ? 4 : 2;
                var segment = new StageSegment(
                    "score",
                    lengthTicks,
                    spawns,
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(new[] { segment }, "boss", 1, 1, 1);
            }
        }

        sealed class TestStageGenerator : IStageGenerator
        {
            readonly bool _lethal;
            readonly int[] _segmentLengths;

            public TestStageGenerator(bool lethal, params int[] segmentLengths)
            {
                _lethal = lethal;
                _segmentLengths = (int[])segmentLengths.Clone();
            }

            public List<GenerationCall> Calls { get; } = new List<GenerationCall>();

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                Calls.Add(new GenerationCall(seed, stageIndex, difficulty));
                Rng rng = new Rng(seed).Fork(stageIndex).Fork(difficulty);
                var segments = new StageSegment[_segmentLengths.Length];
                for (int i = 0; i < segments.Length; i++)
                {
                    SpawnEvent[] spawns = _lethal && i == 0
                        ? new[]
                        {
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0)
                        }
                        : new SpawnEvent[0];
                    segments[i] = new StageSegment(
                        "segment_" + i + "_" + rng.NextInt(0, 100000),
                        _segmentLengths[i],
                        spawns,
                        1,
                        1,
                        new[] { 1 });
                }
                return new StagePlan(segments, "boss", 1, 1, 1);
            }
        }

        sealed class GenerationCall
        {
            public GenerationCall(ulong seed, int stageIndex, int difficulty)
            {
                Seed = seed;
                StageIndex = stageIndex;
                Difficulty = difficulty;
            }

            public ulong Seed { get; }
            public int StageIndex { get; }
            public int Difficulty { get; }
        }
    }
}
