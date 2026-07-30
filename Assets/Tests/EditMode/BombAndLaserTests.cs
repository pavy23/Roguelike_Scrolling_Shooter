using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BombAndLaserTests
    {
        [Test]
        public void BombStockCapsActivatesOnEdgeAndReportsEmptyAttempt()
        {
            BattleSimConfig config = Config();
            config.MaxBombStock = 2;
            var sim = Sim(
                config,
                Segment("empty", 100),
                Array.Empty<EnemyDefinition>());

            Assert.AreEqual(2, sim.AcquireBombStock(3));
            Assert.AreEqual(2, sim.BombStock);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BombAcquired));
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BombStockChanged));

            Step(sim, new InputCommand(0, 0, false, false, true));
            Assert.AreEqual(1, sim.BombStock);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BombActivated));

            Step(sim, InputCommand.None);
            Step(sim, new InputCommand(0, 0, false, false, true));
            Assert.AreEqual(0, sim.BombStock);
            Step(sim, InputCommand.None);
            Step(sim, new InputCommand(0, 0, false, false, true));

            Assert.AreEqual(0, sim.BombStock);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BombActivationRejectedEmpty));
        }

        [Test]
        public void BombDamagesVisibleEnemiesClearsEnemyBulletsAndGrantsInvulnerability()
        {
            LaserAttackDefinition noLaser = null;
            var enemy = new EnemyDefinition(
                "gunner", "Gunner", 50, 0, 10,
                EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0,
                0, 1, 64, 0, 1, 0,
                0, noLaser);
            BattleSimConfig config = Config();
            config.StartingBombStock = 1;
            config.BombRegularEnemyDamage = 100;
            config.BombInvulnerabilityTicks = 45;
            config.EnemyBulletSpeedNumerator = 0;
            config.MaxEnemyBullets = 8;
            var sim = Sim(
                config,
                Segment(
                    "enemy",
                    100,
                    new SpawnEvent(0, enemy.Id, 100, 100)),
                new[] { enemy });

            Step(sim, InputCommand.None);
            Assert.AreEqual(1, CountEnemyBullets(sim));

            Step(sim, new InputCommand(0, 0, false, false, true));

            Assert.AreEqual(0, sim.BombStock);
            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(0, CountEnemyBullets(sim));
            Assert.AreEqual(
                config.BombInvulnerabilityTicks,
                sim.PlayerInvulnerabilityTicksRemaining);
        }

        [Test]
        public void BombBossDamageIsCapped()
        {
            BattleSimConfig config = Config();
            config.StartingBombStock = 1;
            config.BombBossDamageCap = 100;
            StagePlan plan = new StagePlan(
                new[] { Segment("intro", 1) },
                "boss",
                1,
                1,
                1,
                1_000,
                100,
                100,
                100,
                new[] { new BossPhase(999, 1, 1, 1) });
            BattleContent content = Content(
                Array.Empty<EnemyDefinition>());
            var sim = new BattleSim(
                config,
                new Rng(3UL),
                plan,
                content,
                PowerUpGauge.CreateDefault());

            Step(sim, new InputCommand(0, 0, false, false, true));

            Assert.IsTrue(sim.BossActive);
            Assert.AreEqual(900, sim.Boss.Hp);
        }

        [Test]
        public void BombDropUsesIndependentPickupAndAcquisitionFlow()
        {
            var enemy = new EnemyDefinition(
                "bomb_dropper", "Bomb Dropper", 1, 0, 0,
                EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0,
                0, 1, 64, 0, 1, 0,
                1, null);
            BattleSimConfig config = Config();
            config.PlayerBulletSpeedPerTick = 1;
            config.BombNoDropWeight = 0;
            config.CapsuleHalfWidth = 1;
            var sim = Sim(
                config,
                Segment(
                    "drop",
                    100,
                    new SpawnEvent(0, enemy.Id, 1, 0)),
                new[] { enemy });
            var fire = new InputCommand(0, 0, true);

            Step(sim, fire);
            Step(sim, InputCommand.None);

            Assert.AreEqual(1, sim.BombStock);
            Assert.AreEqual(0, sim.BombPickups.Count);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BombAcquired));
        }

        [Test]
        public void TerrainLaserTelegraphsFiresSustainsAndDissipates()
        {
            LaserAttackDefinition laser = Laser(
                cycle: 8,
                telegraph: 2,
                firing: 2,
                sustain: 2,
                dissipate: 1);
            var obstacle = new ObstacleSpawn(
                ObstacleType.LaserEmitter,
                0,
                0,
                0,
                laser);
            BattleSimConfig config = Config();
            config.StartingShieldStock = 2;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            var sim = Sim(
                config,
                ObstacleSegment("laser", 100, obstacle),
                Array.Empty<EnemyDefinition>());

            Assert.AreEqual(1, sim.Lasers.Count);
            Assert.AreEqual(
                LaserPhase.Telegraph,
                sim.Lasers[0].Phase);

            Step(sim, InputCommand.None);
            Assert.AreEqual(
                LaserPhase.Telegraph,
                sim.Lasers[0].Phase);
            Step(sim, InputCommand.None);
            Assert.AreEqual(
                LaserPhase.Firing,
                sim.Lasers[0].Phase);
            Assert.AreEqual(1, sim.ShieldStock);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.LaserFired));

            Step(sim, InputCommand.None);
            Step(sim, InputCommand.None);
            Assert.AreEqual(
                LaserPhase.Sustaining,
                sim.Lasers[0].Phase);
            Assert.AreEqual(
                LaserThicknessStage.Full,
                sim.Lasers[0].ThicknessStage);
            Step(sim, InputCommand.None);
            Step(sim, InputCommand.None);
            Assert.AreEqual(
                LaserPhase.Dissipating,
                sim.Lasers[0].Phase);
            Step(sim, InputCommand.None);
            Assert.AreEqual(0, sim.Lasers.Count);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.LaserEnded));
        }

        [Test]
        public void EnemyLaserUsesItsAttackProfileInsteadOfPointBullets()
        {
            LaserAttackDefinition laser = Laser(
                cycle: 4,
                telegraph: 1,
                firing: 1,
                sustain: 1,
                dissipate: 1);
            var enemy = new EnemyDefinition(
                "laser_enemy", "Laser Enemy", 100, 0, 0,
                EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0,
                0, 1, 64, 0, 1, 0,
                0, laser);
            BattleSimConfig config = Config();
            var sim = Sim(
                config,
                Segment(
                    "enemy_laser",
                    100,
                    new SpawnEvent(0, enemy.Id, 100, 0)),
                new[] { enemy });

            for (int i = 0; i < 4; i++)
                Step(sim, InputCommand.None);

            Assert.AreEqual(1, sim.Lasers.Count);
            Assert.AreEqual(
                LaserSourceKind.Enemy,
                sim.Lasers[0].SourceKind);
            Assert.AreEqual(0, CountEnemyBullets(sim));
            Assert.AreEqual(
                LaserPhase.Telegraph,
                sim.Lasers[0].Phase);
        }

        [Test]
        public void LaserCapacityExceededIsObservable()
        {
            LaserAttackDefinition laser = Laser(
                cycle: 20,
                telegraph: 5,
                firing: 5,
                sustain: 5,
                dissipate: 5);
            BattleSimConfig config = Config();
            config.MaxLasers = 1;
            var sim = Sim(
                config,
                ObstacleSegment(
                    "cap",
                    100,
                    new ObstacleSpawn(
                        ObstacleType.LaserEmitter,
                        0, 0, 0, laser),
                    new ObstacleSpawn(
                        ObstacleType.LaserEmitter,
                        10, 0, 0, laser)),
                Array.Empty<EnemyDefinition>());

            Assert.AreEqual(1, sim.Lasers.Count);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.LaserCapacityExceeded));
        }

        [Test]
        public void SegmentCircleIntersectionUsesEndpointsAndPerpendicularDistance()
        {
            Assert.IsTrue(LaserGeometry.IntersectsSegmentCircle(
                0, 0, 100, 0, 50, 5, 5));
            Assert.IsFalse(LaserGeometry.IntersectsSegmentCircle(
                0, 0, 100, 0, 50, 6, 5));
            Assert.IsTrue(LaserGeometry.IntersectsSegmentCircle(
                0, 0, 100, 0, 103, 4, 5));
            Assert.IsFalse(LaserGeometry.IntersectsSegmentCircle(
                0, 0, 100, 0, 104, 4, 5));
        }

        [Test]
        public void BombAndLaserStateIsDeterministicForSameSeedAndInputs()
        {
            LaserAttackDefinition laser = Laser(
                cycle: 8,
                telegraph: 2,
                firing: 2,
                sustain: 2,
                dissipate: 1);
            StageSegment segment = ObstacleSegment(
                "deterministic",
                100,
                new ObstacleSpawn(
                    ObstacleType.LaserEmitter,
                    0,
                    0,
                    0,
                    laser));
            BattleSimConfig config = Config();
            config.StartingBombStock = 1;
            BattleSim first = Sim(
                config,
                segment,
                Array.Empty<EnemyDefinition>());
            BattleSim second = Sim(
                config,
                segment,
                Array.Empty<EnemyDefinition>());
            var firstHash = new DeterminismAuditHasher();
            var secondHash = new DeterminismAuditHasher();

            for (int tick = 0; tick < 12; tick++)
            {
                InputCommand input = tick == 3
                    ? new InputCommand(
                        0, 0, false, false, true)
                    : InputCommand.None;
                Step(first, input);
                Step(second, input);
                firstHash.FoldBattleState(first);
                secondHash.FoldBattleState(second);
                Assert.AreEqual(
                    firstHash.Hash,
                    secondHash.Hash,
                    $"diverged at tick {tick + 1}");
            }
        }

        static LaserAttackDefinition Laser(
            int cycle,
            int telegraph,
            int firing,
            int sustain,
            int dissipate)
        {
            return new LaserAttackDefinition(
                cycle,
                telegraph,
                firing,
                sustain,
                dissipate,
                -100,
                0,
                100,
                0,
                1,
                4,
                1);
        }

        static BattleSim Sim(
            BattleSimConfig config,
            StageSegment segment,
            EnemyDefinition[] enemies)
        {
            return new BattleSim(
                config,
                new Rng(1UL),
                new StagePlan(
                    new[] { segment },
                    "none",
                    1,
                    1,
                    1),
                Content(enemies),
                PowerUpGauge.CreateDefault());
        }

        static BattleContent Content(EnemyDefinition[] enemies)
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

        static StageSegment Segment(
            string id,
            int lengthTicks,
            params SpawnEvent[] spawns)
        {
            return new StageSegment(
                id,
                lengthTicks,
                spawns,
                1,
                1,
                new[] { 1 });
        }

        static StageSegment ObstacleSegment(
            string id,
            int lengthTicks,
            params ObstacleSpawn[] obstacles)
        {
            return new StageSegment(
                id,
                lengthTicks,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 },
                obstacles);
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerSpeedPerTick = 0;
            config.PlayerBulletSpeedPerTick = 0;
            config.MainShotBaseDamage = 1;
            config.FireIntervalTicks = 1;
            config.MaxBullets = 16;
            config.MaxEnemies = 16;
            config.PlayerMinX = -1_000;
            config.PlayerMaxX = 1_000;
            config.PlayerMinY = -1_000;
            config.PlayerMaxY = 1_000;
            config.BulletDespawnX = 2_000;
            config.EnemyDespawnX = -2_000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.StartingShieldStock = 1;
            config.MaxShieldStock = 5;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.CapsuleHalfWidth = 0;
            config.CapsuleHalfHeight = 0;
            config.CapsuleNoDropWeight = 1;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            config.EnemyBulletDamage = 0;
            return config;
        }

        static int CountEnemyBullets(BattleSim sim)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction
                    == BulletFaction.Enemy)
                    count++;
            return count;
        }

        static bool HasEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        static void Step(
            BattleSim sim,
            InputCommand command)
        {
            sim.Step(in command);
        }
    }
}
