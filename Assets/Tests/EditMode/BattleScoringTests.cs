using System;
using System.Reflection;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class BattleScoringTests
    {
        [Test]
        public void EnemyBulletGrazeScoresOncePerBulletAndPublishesCoordinates()
        {
            BattleSimConfig config = CreateConfig();
            config.GrazeExtraRadiusSubUnits = 128;
            config.GrazeScore = 7;
            config.GrazeComboGaugeGain = 1;
            config.EnemyBulletSpeedNumerator = 0;
            config.EnemyBulletHalfWidth = 0;
            config.EnemyBulletHalfHeight = 0;
            config.MaxEnemyBullets = 1;
            BattleSim sim = CreateTurretSim(config, 0, 128);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(7L, sim.Score);
                Assert.AreEqual(1L, sim.Statistics.GrazeCount);
                Assert.AreEqual(1, sim.ComboGauge);
                AssertEvent(
                    sim.EventsThisTick,
                    SimEventType.GrazeScored,
                    entityId: 1,
                    x: 0,
                    y: 128,
                    arg: 7);
            });

            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(7L, sim.Score);
                Assert.AreEqual(1L, sim.Statistics.GrazeCount);
                Assert.IsFalse(ContainsEvent(
                    sim.EventsThisTick,
                    SimEventType.GrazeScored));
            });
        }

        [Test]
        public void EnemyBulletHitTakesPriorityOverGraze()
        {
            BattleSimConfig config = CreateConfig();
            config.GrazeExtraRadiusSubUnits = 128;
            config.EnemyBulletSpeedNumerator = 0;
            config.EnemyBulletHalfWidth = 1;
            config.EnemyBulletHalfHeight = 0;
            config.MaxEnemyBullets = 1;
            BattleSim sim = CreateTurretSim(config, 1, 0);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(1, sim.PlayerHp);
                Assert.AreEqual(4, sim.ShieldStock);
                Assert.AreEqual(0L, sim.Statistics.GrazeCount);
                Assert.AreEqual(0L, sim.Score);
                Assert.IsTrue(ContainsEvent(
                    sim.EventsThisTick,
                    SimEventType.PlayerHit));
                Assert.IsFalse(ContainsEvent(
                    sim.EventsThisTick,
                    SimEventType.GrazeScored));
            });
        }

        [Test]
        public void KillsAdvanceMultiplierAndEnemyKilledArgUsesAwardedScore()
        {
            BattleSimConfig config = CreateConfig();
            ConfigureOneKillPerLevel(config);
            BattleSim sim = CreateTargetSim(config, targetCount: 4);
            InputCommand fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            sim.Step(in fire);
            AssertMultiplier(sim, level: 1, multiplier: 2, score: 10);

            sim.Step(in fire);
            AssertMultiplier(sim, level: 2, multiplier: 4, score: 30);
            AssertEvent(
                sim.EventsThisTick,
                SimEventType.EnemyKilled,
                entityId: 2,
                x: 1,
                y: 0,
                arg: 20);

            sim.Step(in fire);
            AssertMultiplier(sim, level: 3, multiplier: 8, score: 70);

            sim.Step(in fire);
            AssertAll(() =>
            {
                Assert.AreEqual(150L, sim.Score);
                Assert.AreEqual(4L, sim.Statistics.Kills);
                Assert.AreEqual(0, sim.ComboGauge);
            });
        }

        [Test]
        public void MultiplierDropsOneLevelAfterConfiguredKilllessTicks()
        {
            BattleSimConfig config = CreateConfig();
            ConfigureOneKillPerLevel(config);
            config.ComboDecayTicks = 3;
            BattleSim sim = CreateTargetSim(config, targetCount: 1);
            InputCommand fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in fire);
            Assert.AreEqual(1, sim.MultiplierLevel);

            sim.Step(in none);
            sim.Step(in none);
            Assert.AreEqual(1, sim.MultiplierLevel);

            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(0, sim.MultiplierLevel);
                Assert.AreEqual(1, sim.ScoreMultiplier);
                AssertEvent(
                    sim.EventsThisTick,
                    SimEventType.MultiplierChanged,
                    entityId: 0,
                    x: 0,
                    y: 0,
                    arg: 1);
            });
        }

        [Test]
        public void PlayerHitResetsMultiplierAndGauge()
        {
            BattleSimConfig config = CreateConfig();
            ConfigureOneKillPerLevel(config);
            config.ComboDecayTicks = 100;
            EnemyDefinition target = Target();
            EnemyDefinition collider = new EnemyDefinition(
                "collider", "Collider", 100, 1, 0, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            BattleSim sim = CreateSim(
                config,
                new[] { target, collider },
                new[]
                {
                    new SpawnEvent(0, target.Id, 1, 0),
                    new SpawnEvent(0, target.Id, 1, 0),
                    new SpawnEvent(0, target.Id, 1, 0),
                    new SpawnEvent(5, collider.Id, 0, 0)
                });
            InputCommand fire = new InputCommand(0, 0, true);

            for (int i = 0; i < 4; i++)
                sim.Step(in fire);
            Assert.AreEqual(3, sim.MultiplierLevel);

            sim.Step(in fire);

            AssertAll(() =>
            {
                Assert.AreEqual(1, sim.PlayerHp);
                Assert.AreEqual(4, sim.ShieldStock);
                Assert.AreEqual(0, sim.MultiplierLevel);
                Assert.AreEqual(1, sim.ScoreMultiplier);
                Assert.AreEqual(0, sim.ComboGauge);
                AssertEvent(
                    sim.EventsThisTick,
                    SimEventType.MultiplierChanged,
                    entityId: 0,
                    x: 0,
                    y: 0,
                    arg: 1);
            });
        }

        [Test]
        public void GrazeAndComboStateAreDeterministicForEqualInputs()
        {
            BattleSimConfig config = CreateConfig();
            ConfigureOneKillPerLevel(config);
            BattleSim first = CreateTargetSim(config, targetCount: 4);
            BattleSim second = CreateTargetSim(config, targetCount: 4);
            InputCommand fire = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 12; tick++)
            {
                first.Step(in fire);
                second.Step(in fire);

                AssertAll(() =>
                {
                    Assert.AreEqual(first.Score, second.Score, $"tick {tick}");
                    Assert.AreEqual(
                        first.MultiplierLevel,
                        second.MultiplierLevel,
                        $"tick {tick}");
                    Assert.AreEqual(first.ComboGauge, second.ComboGauge, $"tick {tick}");
                    Assert.AreEqual(
                        first.Statistics.GrazeCount,
                        second.Statistics.GrazeCount,
                        $"tick {tick}");
                    AssertEventsEqual(
                        first.EventsThisTick,
                        second.EventsThisTick,
                        tick);
                });
            }

            var firstHash = new DeterminismAuditHasher();
            var secondHash = new DeterminismAuditHasher();
            firstHash.FoldBattleState(first);
            secondHash.FoldBattleState(second);
            Assert.AreEqual(firstHash.Hash, secondHash.Hash);
        }

        [Test]
        public void RunStatisticsExposeGrazeCountAndTotalScoreSaturates()
        {
            BattleSimConfig config = CreateConfig();
            config.GrazeExtraRadiusSubUnits = 128;
            config.GrazeScore = 7;
            config.EnemyBulletSpeedNumerator = 0;
            config.MaxEnemyBullets = 1;
            EnemyDefinition turret = Turret();
            StagePlan plan = Plan(
                new[] { new SpawnEvent(0, turret.Id, 0, 128) },
                lengthTicks: 2);
            var run = new RunManager(
                91UL,
                new FixedStageGenerator(plan),
                config,
                Content(turret),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            run.Step(in none);
            run.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(1, run.BiomeIndex);
                Assert.AreEqual(2, run.RoomIndex);
                Assert.AreEqual(1L, run.Statistics.GrazeCount);
                Assert.AreEqual(1, run.Statistics.RoomsCleared);
            });

            run.Step(in none);
            Assert.AreEqual(2L, run.Statistics.GrazeCount);
            FieldInfo completedScore = typeof(RunManager).GetField(
                "_completedStageScore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(completedScore);
            completedScore.SetValue(run, long.MaxValue - 1);
            Assert.AreEqual(long.MaxValue, run.TotalScore);
        }

        static void ConfigureOneKillPerLevel(BattleSimConfig config)
        {
            config.KillComboGaugeGain = 1;
            config.GrazeComboGaugeGain = 0;
            config.ComboGaugeRequiredForLevel2 = 1;
            config.ComboGaugeRequiredForLevel3 = 1;
            config.ComboGaugeRequiredForLevel4 = 1;
        }

        static void AssertMultiplier(
            BattleSim sim,
            int level,
            int multiplier,
            long score)
        {
            AssertAll(() =>
            {
                Assert.AreEqual(level, sim.MultiplierLevel);
                Assert.AreEqual(multiplier, sim.ScoreMultiplier);
                Assert.AreEqual(score, sim.Score);
                AssertEvent(
                    sim.EventsThisTick,
                    SimEventType.MultiplierChanged,
                    entityId: level,
                    x: 0,
                    y: 0,
                    arg: multiplier);
            });
        }

        static BattleSim CreateTurretSim(
            BattleSimConfig config,
            int turretX,
            int turretY)
        {
            EnemyDefinition turret = Turret();
            return CreateSim(
                config,
                new[] { turret },
                new[] { new SpawnEvent(0, turret.Id, turretX, turretY) });
        }

        static BattleSim CreateTargetSim(BattleSimConfig config, int targetCount)
        {
            EnemyDefinition target = Target();
            var spawns = new SpawnEvent[targetCount];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new SpawnEvent(0, target.Id, 1, 0);
            return CreateSim(config, new[] { target }, spawns);
        }

        static BattleSim CreateSim(
            BattleSimConfig config,
            EnemyDefinition[] enemies,
            SpawnEvent[] spawns)
        {
            return new BattleSim(
                config,
                new Rng(0x15UL),
                Plan(spawns, lengthTicks: 1000),
                Content(enemies),
                PowerUpGauge.CreateDefault());
        }

        static BattleContent Content(params EnemyDefinition[] enemies)
        {
            var weapon = new WeaponDefinition(
                "shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(enemies, new[] { weapon }, weapon.Id);
        }

        static StagePlan Plan(SpawnEvent[] spawns, int lengthTicks)
        {
            return new StagePlan(
                new[]
                {
                    new StageSegment(
                        "scoring",
                        lengthTicks,
                        spawns,
                        1,
                        1,
                        new[] { 1 })
                },
                "none",
                1,
                1,
                1);
        }

        static EnemyDefinition Target()
        {
            return new EnemyDefinition(
                "target", "Target", 1, 0, 10, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
        }

        static EnemyDefinition Turret()
        {
            return new EnemyDefinition(
                "turret", "Turret", 100, 0, 0, EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0, 0, 1, 1);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 32,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 10,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 1,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                EnemyBulletSpeedNumerator = 1,
                EnemyBulletSpeedDenominator = 1,
                EnemyBulletHalfWidth = 0,
                EnemyBulletHalfHeight = 0,
                EnemyBulletDamage = 1,
                MaxEnemyBullets = 4
            };
        }

        static bool ContainsEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        // Unity 내장 NUnit에는 Assert.Multiple이 없다 (dotnet 쪽 NUnit과 버전 차이) —
        // 즉시 실행 셔플러로 대체. 통합 컴파일 수정 (CLAUDE, CODEX 소유 파일).
        static void AssertAll(Action assert) => assert();

        static void AssertEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type,
            int entityId,
            int x,
            int y,
            int arg)
        {
            for (int i = 0; i < events.Length; i++)
            {
                SimEvent simEvent = events[i];
                if (simEvent.Type != type)
                    continue;
                AssertAll(() =>
                {
                    Assert.AreEqual(entityId, simEvent.EntityId);
                    Assert.AreEqual(x, simEvent.X);
                    Assert.AreEqual(y, simEvent.Y);
                    Assert.AreEqual(arg, simEvent.Arg);
                });
                return;
            }
            Assert.Fail($"Expected event {type} was not emitted.");
        }

        static void AssertEventsEqual(
            ReadOnlySpan<SimEvent> expected,
            ReadOnlySpan<SimEvent> actual,
            int tick)
        {
            Assert.AreEqual(expected.Length, actual.Length, $"tick {tick}");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Type, actual[i].Type, $"tick {tick}, event {i}");
                Assert.AreEqual(
                    expected[i].EntityId,
                    actual[i].EntityId,
                    $"tick {tick}, event {i}");
                Assert.AreEqual(expected[i].X, actual[i].X, $"tick {tick}, event {i}");
                Assert.AreEqual(expected[i].Y, actual[i].Y, $"tick {tick}, event {i}");
                Assert.AreEqual(expected[i].Arg, actual[i].Arg, $"tick {tick}, event {i}");
            }
        }

        sealed class FixedStageGenerator : IStageGenerator
        {
            readonly StagePlan _plan;

            public FixedStageGenerator(StagePlan plan)
            {
                _plan = plan;
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                return _plan;
            }
        }
    }
}
