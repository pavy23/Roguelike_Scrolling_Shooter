using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class StageGimmickTests
    {
        [Test]
        public void BreakableDebrisUsesExistingHpCollisionAndDestructionEvent()
        {
            var environment = SegmentEnvironmentDefinition.None;
            StagePlan plan = Plan(
                Segment(
                    10,
                    environment,
                    new ObstacleSpawn(
                        ObstacleType.Breakable,
                        1,
                        0,
                        1)));
            BattleSimConfig config = Config();
            config.ObstacleHalfWidth = 0;
            config.ObstacleHalfHeight = 0;
            BattleSim sim = Sim(plan, config);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(0, sim.Obstacles.Count);
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.ObstacleDestroyed));
            });
        }

        [Test]
        public void NarrowingCorridorClampsPlayerAndAppliesWallContact()
        {
            var environment = new SegmentEnvironmentDefinition(
                true,
                -4,
                4,
                -2,
                2,
                1,
                0,
                1,
                0,
                1);
            BattleSimConfig config = Config();
            config.StartingShieldStock = 2;
            config.PlayerHitInvulnerabilityTicks = 0;
            BattleSim sim = Sim(
                Plan(Segment(4, environment)),
                config);
            InputCommand upward =
                InputCommand.Analog(0, 100, false);

            sim.Step(in upward);

            AssertAll(() =>
            {
                Assert.IsTrue(sim.Environment.HasCorridor);
                Assert.AreEqual(-4, sim.Environment.CorridorMinY);
                Assert.AreEqual(4, sim.Environment.CorridorMaxY);
                Assert.AreEqual(4, sim.PlayerY);
                Assert.AreEqual(1, sim.ShieldStock);
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.CorridorContact));
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.PlayerHit));
            });
        }

        [Test]
        public void DriftComposesAfterAnalogClampAndContinuesWithoutInput()
        {
            var environment = new SegmentEnvironmentDefinition(
                false,
                0,
                0,
                0,
                0,
                0,
                1,
                2,
                0,
                1);
            BattleSimConfig config = Config();
            config.PlayerSpeedNumerator = 3;
            config.PlayerSpeedDenominator = 1;
            BattleSim sim = Sim(
                Plan(Segment(10, environment)),
                config);
            InputCommand move =
                InputCommand.Analog(100, 0, false);
            InputCommand released =
                InputCommand.Analog(0, 0, false);

            sim.Step(in move);
            int afterControl = sim.PlayerX;
            sim.Step(in released);

            AssertAll(() =>
            {
                Assert.AreEqual(3, afterControl);
                Assert.AreEqual(4, sim.PlayerX);
                Assert.IsTrue(sim.Environment.HasDrift);
                Assert.AreEqual(1, sim.Environment.DriftXNumerator);
                Assert.AreEqual(2, sim.Environment.DriftXDenominator);
            });
        }

        [Test]
        public void TimeLimitExpiryIsUnshieldableAndObservable()
        {
            StagePlan plan = Plan(
                Segment(10, SegmentEnvironmentDefinition.None),
                new StageGimmickDefinition(
                    "core",
                    false,
                    2));
            BattleSimConfig config = Config();
            config.StartingShieldStock = 3;
            config.MaxShieldStock = 3;
            BattleSim sim = Sim(plan, config);
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            Assert.IsTrue(sim.IsPlayerAlive);
            Assert.AreEqual(1, sim.RemainingTimeTicks);
            sim.Step(in none);

            AssertAll(() =>
            {
                Assert.IsFalse(sim.IsPlayerAlive);
                Assert.AreEqual(0, sim.ShieldStock);
                Assert.AreEqual(0, sim.RemainingTimeTicks);
                Assert.IsTrue(sim.TimeLimitExpired);
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.TimeLimitExpired));
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.PlayerKilled));
            });
        }

        [Test]
        public void VisionObstructionIsExposedWithoutChangingSimulation()
        {
            StageSegment segment =
                Segment(10, SegmentEnvironmentDefinition.None);
            BattleSim obscured = Sim(
                Plan(
                    segment,
                    new StageGimmickDefinition(
                        "core",
                        true,
                        0)),
                Config());
            BattleSim clear = Sim(
                Plan(
                    segment,
                    new StageGimmickDefinition(
                        "core",
                        false,
                        0)),
                Config());
            var move = new InputCommand(1, 1, false);

            for (int i = 0; i < 5; i++)
            {
                obscured.Step(in move);
                clear.Step(in move);
            }

            AssertAll(() =>
            {
                Assert.IsTrue(obscured.VisionObscured);
                Assert.IsFalse(clear.VisionObscured);
                Assert.AreEqual(clear.PlayerX, obscured.PlayerX);
                Assert.AreEqual(clear.PlayerY, obscured.PlayerY);
                Assert.AreEqual(clear.ShieldStock, obscured.ShieldStock);
            });
        }

        [Test]
        public void EnemyAndObstacleCapsEmitRejectionEvents()
        {
            var enemy = new EnemyDefinition(
                "tentacle",
                1,
                1,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var segment = new StageSegment(
                "caps",
                10,
                new[]
                {
                    new SpawnEvent(0, enemy.Id, 5, 0),
                    new SpawnEvent(0, enemy.Id, 6, 0)
                },
                1,
                1,
                new[] { 1 },
                new[]
                {
                    new ObstacleSpawn(
                        ObstacleType.Solid,
                        5,
                        3,
                        0)
                });
            BattleSimConfig config = Config();
            config.MaxEnemies = 1;
            config.MaxObstacles = 0;
            BattleContent content = Content(enemy);
            var sim = new BattleSim(
                config,
                new Rng(55UL),
                Plan(segment),
                content,
                PowerUpGauge.CreateDefault());

            AssertAll(() =>
            {
                Assert.AreEqual(1, sim.Enemies.Count);
                Assert.AreEqual(0, sim.Obstacles.Count);
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.EnemyCapacityExceeded));
                Assert.IsTrue(HasEvent(
                    sim,
                    SimEventType.ObstacleCapacityExceeded));
                Assert.AreEqual(
                    EnemyMovePattern.Static,
                    enemy.MovePattern);
            });
        }

        [Test]
        public void SameSeedAndGimmickInputsProduceIdenticalAuditHash()
        {
            var environment = new SegmentEnvironmentDefinition(
                true,
                -20,
                20,
                -10,
                10,
                1,
                -3,
                2,
                1,
                3);
            StagePlan plan = Plan(
                Segment(20, environment),
                new StageGimmickDefinition(
                    "core",
                    true,
                    15));
            BattleSim first = Sim(plan, Config(), 0x55AAUL);
            BattleSim second = Sim(plan, Config(), 0x55AAUL);
            var input = InputCommand.Analog(2, -1, false);

            for (int i = 0; i < 12; i++)
            {
                first.Step(in input);
                second.Step(in input);
            }

            var firstHash = new DeterminismAuditHasher();
            var secondHash = new DeterminismAuditHasher();
            firstHash.FoldBattleState(first);
            secondHash.FoldBattleState(second);
            Assert.AreEqual(firstHash.Hash, secondHash.Hash);
        }

        static StagePlan Plan(
            StageSegment segment,
            StageGimmickDefinition gimmick = null)
        {
            return new StagePlan(
                new[] { segment },
                string.Empty,
                1,
                1,
                1,
                0,
                0,
                0,
                0,
                Array.Empty<BossPhase>(),
                "core",
                "core",
                EncounterType.Normal,
                Array.Empty<BossPartDefinition>(),
                gimmick);
        }

        static StageSegment Segment(
            int lengthTicks,
            SegmentEnvironmentDefinition environment,
            params ObstacleSpawn[] obstacles)
        {
            return new StageSegment(
                "gimmick",
                lengthTicks,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 },
                obstacles,
                environment);
        }

        static BattleSim Sim(
            StagePlan plan,
            BattleSimConfig config,
            ulong seed = 1UL)
        {
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                Content(),
                PowerUpGauge.CreateDefault());
        }

        static BattleContent Content(
            params EnemyDefinition[] enemies)
        {
            var weapon = new WeaponDefinition(
                "shot",
                1,
                1,
                1,
                1,
                0,
                0);
            return new BattleContent(
                enemies,
                new[] { weapon },
                weapon.Id);
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerSpeedNumerator = 100;
            config.PlayerSpeedDenominator = 1;
            config.PlayerMinX = -1000;
            config.PlayerMaxX = 1000;
            config.PlayerMinY = -1000;
            config.PlayerMaxY = 1000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.StartingShieldStock = 1;
            config.MaxShieldStock = 5;
            config.PlayerBulletSpeedNumerator = 1;
            config.PlayerBulletSpeedDenominator = 1;
            config.MainShotBaseDamage = 1;
            config.FireIntervalTicks = 1;
            config.MainShotHalfWidth = 0;
            config.MainShotHalfHeight = 0;
            config.UseConfiguredMainShotStats = true;
            config.BulletDespawnX = 1000;
            config.EnemyDespawnX = -1000;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            config.CapsuleNoDropWeight = 1;
            config.MaxEnemyBullets = 0;
            return config;
        }

        static bool HasEvent(
            BattleSim sim,
            SimEventType type)
        {
            ReadOnlySpan<SimEvent> events =
                sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        static void AssertAll(Action assert) => assert();
    }
}
