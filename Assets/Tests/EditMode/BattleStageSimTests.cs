using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BattleStageSimTests
    {
        [Test]
        public void StagePlan_SpawnsAtSegmentRelativeTicks()
        {
            EnemyDefinition enemy = Enemy("static", EnemyMovePattern.Static);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(
                Segment("first", 3, new SpawnEvent(2, enemy.Id, 10, 10)),
                Segment("second", 3, new SpawnEvent(1, enemy.Id, 20, 20)));
            var sim = CreateSim(plan, content, CreateConfig(), 1UL);
            InputCommand none = InputCommand.None;

            Assert.AreEqual(0, sim.Enemies.Count);
            sim.Step(in none);
            Assert.AreEqual(0, sim.Enemies.Count);
            sim.Step(in none);
            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual(10, sim.Enemies[0].X);
            sim.Step(in none);
            Assert.AreEqual(1, sim.Enemies.Count);
            sim.Step(in none);
            Assert.AreEqual(2, sim.Enemies.Count);
            Assert.AreEqual(20, sim.Enemies[1].X);
        }

        [Test]
        public void MovementPatterns_AreIntegerAndDeterministic()
        {
            var straight = Enemy("straight", EnemyMovePattern.Straight, speedNumerator: 3);
            var sine = Enemy(
                "sine",
                EnemyMovePattern.Sine,
                speedNumerator: 2,
                sineAmplitude: 10,
                sinePeriodTicks: 64);
            var stationary = Enemy("static", EnemyMovePattern.Static, speedNumerator: 100);
            BattleContent content = Content(straight, sine, stationary);
            StagePlan plan = Plan(Segment(
                "patterns",
                100,
                new SpawnEvent(0, straight.Id, 100, 10),
                new SpawnEvent(0, sine.Id, 100, 20),
                new SpawnEvent(0, stationary.Id, 100, 30)));
            var sim = CreateSim(plan, content, CreateConfig(), 2UL);
            InputCommand none = InputCommand.None;

            for (int i = 0; i < 16; i++) sim.Step(in none);

            Assert.AreEqual(52, sim.Enemies[0].X);
            Assert.AreEqual(10, sim.Enemies[0].Y);
            Assert.AreEqual(68, sim.Enemies[1].X);
            Assert.AreEqual(30, sim.Enemies[1].Y);
            Assert.AreEqual(100, sim.Enemies[2].X);
            Assert.AreEqual(30, sim.Enemies[2].Y);
        }

        [Test]
        public void DivePattern_LocksPlayerYOnceAndFollowsDeterministicTrajectory()
        {
            EnemyDefinition enemy = Enemy(
                "dive",
                EnemyMovePattern.Dive,
                speedNumerator: 2,
                movementDelayTicks: 2,
                movementDurationTicks: 4);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "dive",
                100,
                new SpawnEvent(0, enemy.Id, 100, 40)));
            BattleSimConfig config = CreateConfig();
            config.PlayerSpawnY = -20;
            var first = CreateSim(plan, content, config, 21UL);
            var second = CreateSim(plan, content, config, 21UL);
            InputCommand none = InputCommand.None;
            int[] expectedY = { 40, 40, 25, 10, -5, -20, -20 };

            for (int tick = 0; tick < expectedY.Length; tick++)
            {
                first.Step(in none);
                second.Step(in none);
                Assert.AreEqual(98 - 2 * tick, first.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(expectedY[tick], first.Enemies[0].Y, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].X, second.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].Y, second.Enemies[0].Y, $"tick {tick + 1}");
            }
        }

        [Test]
        public void ZigzagPattern_UsesDeterministicTriangleWaveTrajectory()
        {
            EnemyDefinition enemy = Enemy(
                "zigzag",
                EnemyMovePattern.Zigzag,
                speedNumerator: 2,
                sineAmplitude: 40,
                sinePeriodTicks: 8);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "zigzag",
                100,
                new SpawnEvent(0, enemy.Id, 100, 0)));
            var first = CreateSim(plan, content, CreateConfig(), 22UL);
            var second = CreateSim(plan, content, CreateConfig(), 22UL);
            InputCommand none = InputCommand.None;
            int[] expectedY = { 20, 40, 20, 0, -20, -40, -20, 0 };

            for (int tick = 0; tick < expectedY.Length; tick++)
            {
                first.Step(in none);
                second.Step(in none);
                Assert.AreEqual(98 - 2 * tick, first.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(expectedY[tick], first.Enemies[0].Y, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].X, second.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].Y, second.Enemies[0].Y, $"tick {tick + 1}");
            }
        }

        [Test]
        public void DashPattern_RepeatsPauseAndBurstWithoutLosingDeterminism()
        {
            EnemyDefinition enemy = Enemy(
                "dash",
                EnemyMovePattern.Dash,
                speedNumerator: 3,
                speedDenominator: 2,
                movementDurationTicks: 2,
                movementPauseTicks: 2);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "dash",
                100,
                new SpawnEvent(0, enemy.Id, 100, 0)));
            var first = CreateSim(plan, content, CreateConfig(), 23UL);
            var second = CreateSim(plan, content, CreateConfig(), 23UL);
            InputCommand none = InputCommand.None;
            int[] expectedX = { 100, 100, 99, 97, 97, 97, 96, 94 };

            for (int tick = 0; tick < expectedX.Length; tick++)
            {
                first.Step(in none);
                second.Step(in none);
                Assert.AreEqual(expectedX[tick], first.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(0, first.Enemies[0].Y, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].X, second.Enemies[0].X, $"tick {tick + 1}");
                Assert.AreEqual(first.Enemies[0].Y, second.Enemies[0].Y, $"tick {tick + 1}");
            }
        }

        [Test]
        public void PlayerBulletKill_DropsAndCollectsCapsuleThroughGauge()
        {
            EnemyDefinition enemy = Enemy("dropper", EnemyMovePattern.Static, hp: 10, dropWeight: 1);
            BattleContent content = Content(
                new WeaponDefinition("shot", 10, 1, 2, 1, 0, 0),
                enemy);
            StagePlan plan = Plan(Segment(
                "drop",
                20,
                new SpawnEvent(0, enemy.Id, 4, 0)));
            BattleSimConfig config = CreateConfig();
            config.CapsuleNoDropWeight = 0;
            var gauge = PowerUpGauge.CreateDefault();
            var sim = new BattleSim(config, new Rng(3UL), plan, content, gauge);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);
            sim.Step(in none);

            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(0, sim.Bullets.Count);
            Assert.AreEqual(1, sim.Capsules.Count);
            Assert.AreEqual(4, sim.Capsules[0].X);
            Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);

            var moveRight = new InputCommand(1, 0, false);
            sim.Step(in moveRight);
            sim.Step(in moveRight);

            Assert.AreEqual(0, sim.Capsules.Count);
            Assert.AreEqual((int)PowerUpSlot.MainShot, gauge.Cursor);
        }

        [Test]
        public void PlayerBulletKills_AccumulateTargetDefinitionScoreValues()
        {
            EnemyDefinition first = Enemy(
                "first",
                EnemyMovePattern.Static,
                scoreValue: 125);
            EnemyDefinition second = Enemy(
                "second",
                EnemyMovePattern.Static,
                scoreValue: 275);
            BattleContent content = Content(
                new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0),
                first,
                second);
            StagePlan plan = Plan(Segment(
                "score",
                10,
                new SpawnEvent(0, first.Id, 1, 0),
                new SpawnEvent(0, second.Id, 1, 0)));
            var sim = CreateSim(plan, content, CreateConfig(), 31UL);
            IBattleSim observable = sim;
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            sim.Step(in fire);
            Assert.AreEqual(125L, observable.Score);

            sim.Step(in fire);
            Assert.AreEqual(400L, observable.Score);
            Assert.AreEqual(0, sim.Enemies.Count);
        }

        [Test]
        public void EnemyPlayerAabbCollision_AppliesContactDamageAndConsumesEnemy()
        {
            EnemyDefinition enemy = Enemy(
                "rammer",
                EnemyMovePattern.Static,
                contactDamage: 2);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "contact",
                10,
                new SpawnEvent(1, enemy.Id, 0, 0)));
            BattleSimConfig config = CreateConfig();
            config.PlayerMaxHp = 5;
            var sim = CreateSim(plan, content, config, 4UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(4, sim.ShieldStock);
            Assert.AreEqual(0, sim.Enemies.Count);
        }

        [Test]
        public void StaticPattern_MovesOnlyByPureScrollDelta()
        {
            EnemyDefinition enemy = Enemy(
                "scrolled_static",
                EnemyMovePattern.Static,
                speedNumerator: 100);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "scroll",
                10,
                new SpawnEvent(0, enemy.Id, 100, 0)));
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 5;
            config.ScrollSpeedDenominator = 2;
            var sim = CreateSim(plan, content, config, 5UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            Assert.AreEqual(98, sim.Enemies[0].X);
            sim.Step(in none);
            Assert.AreEqual(95, sim.Enemies[0].X);
        }

        [Test]
        public void DroppedCapsule_DriftsWithExactScrollDeltaAndDespawnsOffscreen()
        {
            EnemyDefinition enemy = Enemy(
                "dropper",
                EnemyMovePattern.Static,
                hp: 1,
                dropWeight: 1);
            var weapon = new WeaponDefinition("shot", 1, 60, 0, 1, 200, 0);
            BattleContent content = Content(weapon, enemy);
            StagePlan plan = Plan(Segment(
                "capsule_scroll",
                100,
                new SpawnEvent(0, enemy.Id, 10, 0)));
            BattleSimConfig config = CreateConfig();
            config.PlayerSpawnX = -100;
            config.ScrollSpeedNumerator = 5;
            config.ScrollSpeedDenominator = 2;
            config.EnemyDespawnX = -1;
            config.CapsuleNoDropWeight = 0;
            var sim = CreateSim(plan, content, config, 24UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);
            Assert.AreEqual(1, sim.Capsules.Count);
            Assert.AreEqual(5, sim.Capsules[0].X);

            sim.Step(in none);
            Assert.AreEqual(3, sim.Capsules[0].X);
            sim.Step(in none);
            Assert.AreEqual(0, sim.Capsules[0].X);
            sim.Step(in none);
            Assert.AreEqual(0, sim.Capsules.Count);
        }

        [Test]
        public void CapsuleMagnetUsesDeterministicRationalTrajectory()
        {
            EnemyDefinition enemy = Enemy(
                "magnet_dropper",
                EnemyMovePattern.Static,
                hp: 1,
                dropWeight: 1);
            var weapon =
                new WeaponDefinition("shot", 1, 60, 0, 1, 200, 0);
            BattleContent content = Content(weapon, enemy);
            StagePlan plan = Plan(Segment(
                "capsule_magnet",
                100,
                new SpawnEvent(0, enemy.Id, 10, 0)));
            BattleSimConfig config = CreateConfig();
            config.PlayerSpawnX = 0;
            config.CapsuleMagnetRadiusSubUnits = 20;
            config.CapsuleMagnetSpeedNumerator = 3;
            config.CapsuleMagnetSpeedDenominator = 2;
            BattleSim first = CreateSim(plan, content, config, 24UL);
            BattleSim repeated = CreateSim(plan, content, config, 24UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            first.Step(in fire);
            repeated.Step(in fire);
            first.Step(in none);
            repeated.Step(in none);
            Assert.AreEqual(10, first.Capsules[0].X);

            int[] expectedX = { 9, 7, 6, 4 };
            for (int i = 0; i < expectedX.Length; i++)
            {
                first.Step(in none);
                repeated.Step(in none);
                Assert.AreEqual(expectedX[i], first.Capsules[0].X);
                Assert.AreEqual(
                    first.Capsules[0].X,
                    repeated.Capsules[0].X);
                Assert.AreEqual(
                    first.Capsules[0].Y,
                    repeated.Capsules[0].Y);
            }

            BattleSim measured =
                CreateSim(plan, content, config, 24UL);
            measured.Step(in fire);
            measured.Step(in none);
            long before = GC.GetAllocatedBytesForCurrentThread();
            measured.Step(in none);
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(
                0L,
                allocated,
                "Capsule attraction must use preallocated state.");
        }

        [Test]
        public void ScrollX_IsPureIntegerFunctionOfTick()
        {
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 5;
            config.ScrollSpeedDenominator = 2;
            var sim = new BattleSim(config, new Rng(5UL));
            InputCommand none = InputCommand.None;

            Assert.AreEqual(0L, sim.ScrollX);
            Assert.AreEqual(17L, sim.GetScrollXAtTick(7));
            Assert.AreEqual(17L, BattleSim.ComputeScrollX(7, 5, 2));
            sim.Step(in none);
            Assert.AreEqual(2L, sim.ScrollX);
            sim.Step(in none);
            Assert.AreEqual(5L, sim.ScrollX);
        }
        [Test]
        public void SameSeedAndInputs_ProduceSameEnemyAndDropResults()
        {
            EnemyDefinition enemy = Enemy(
                "coin_flip",
                EnemyMovePattern.Static,
                hp: 1,
                dropWeight: 1);
            BattleContent content = Content(
                new WeaponDefinition("shot", 1, 4, 2, 1, 0, 0),
                enemy);
            var spawns = new SpawnEvent[16];
            for (int i = 0; i < spawns.Length; i++)
                spawns[i] = new SpawnEvent(1 + i * 4, enemy.Id, 4, 0);
            StagePlan plan = Plan(Segment("determinism", 70, spawns));
            BattleSimConfig config = CreateConfig();
            config.CapsuleNoDropWeight = 1;

            var firstRoot = new Rng(0xC0FFEEUL);
            var secondRoot = new Rng(0xC0FFEEUL);
            secondRoot.NextULong();
            secondRoot.NextULong();
            var first = new BattleSim(
                config, firstRoot, plan, content, PowerUpGauge.CreateDefault());
            var second = new BattleSim(
                config, secondRoot, plan, content, PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 70; tick++)
            {
                first.Step(in fire);
                second.Step(in fire);
                AssertStatesEqual(first, second, tick);
            }

            Assert.Greater(first.Capsules.Count, 0);
            Assert.AreEqual(0, first.Enemies.Count);
        }

        [Test]
        public void EnemyAndCapsuleViews_AreReadOnlyAndReused()
        {
            EnemyDefinition enemy = Enemy("view", EnemyMovePattern.Static);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment(
                "view",
                10,
                new SpawnEvent(1, enemy.Id, 20, 0)));
            var sim = CreateSim(plan, content, CreateConfig(), 6UL);
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            IReadOnlyList<ObstacleState> obstacles = sim.Obstacles;
            IReadOnlyList<CapsuleState> capsules = sim.Capsules;
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            Assert.AreSame(enemies, sim.Enemies);
            Assert.AreSame(obstacles, sim.Obstacles);
            Assert.AreSame(capsules, sim.Capsules);
            Assert.IsFalse(enemies is List<EnemyState>);
            Assert.IsFalse(obstacles is List<ObstacleState>);
            Assert.IsFalse(capsules is List<CapsuleState>);
        }

        [Test]
        public void ObstacleScroll_UsesExactWorldScrollDelta()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "scroll",
                20,
                new ObstacleSpawn(ObstacleType.Solid, 10, 5, 0)));
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 5;
            config.ScrollSpeedDenominator = 2;
            var sim = CreateSim(plan, Content(), config, 31UL);
            InputCommand none = InputCommand.None;

            Assert.AreEqual(10, sim.Obstacles[0].X);
            sim.Step(in none);
            Assert.AreEqual(8, sim.Obstacles[0].X);
            sim.Step(in none);
            Assert.AreEqual(5, sim.Obstacles[0].X);
            Assert.AreEqual(5, sim.Obstacles[0].Y);
        }

        [Test]
        public void SolidObstacle_BlocksPlayerBulletAndCannotBeDamaged()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "solid",
                20,
                new ObstacleSpawn(ObstacleType.Solid, 1, 0, 0)));
            WeaponDefinition weapon =
                new WeaponDefinition("shot", 10, 1, 1, 1, 0, 0);
            BattleSimConfig config = CreateConfig();
            config.ObstacleHalfWidth = 0;
            config.ObstacleHalfHeight = 0;
            var sim = CreateSim(
                plan,
                Content(weapon),
                config,
                32UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            Assert.AreEqual(1, sim.Bullets.Count);
            sim.Step(in none);

            Assert.AreEqual(0, sim.Bullets.Count);
            Assert.AreEqual(1, sim.Obstacles.Count);
            Assert.AreEqual(ObstacleType.Solid, sim.Obstacles[0].Type);
            Assert.AreEqual(0, sim.Obstacles[0].Hp);
        }

        [Test]
        public void BreakableObstacle_AwardsMultiplierAppliedScoreAndEmitsCoordinates()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "breakable",
                20,
                new ObstacleSpawn(
                    ObstacleType.Breakable,
                    1,
                    0,
                    10)));
            WeaponDefinition weapon =
                new WeaponDefinition("shot", 10, 1, 1, 1, 0, 0);
            BattleSimConfig config = CreateConfig();
            config.ObstacleHalfWidth = 0;
            config.ObstacleHalfHeight = 0;
            config.BreakableObstacleScore = 7;
            config.ComboMultiplierLevel1 = 2;
            var sim = CreateSim(
                plan,
                Content(weapon),
                config,
                33UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);

            Assert.AreEqual(0, sim.Obstacles.Count);
            Assert.AreEqual(0, sim.Bullets.Count);
            Assert.AreEqual(14, sim.Score);
            SimEvent destroyed = FindEvent(
                sim.EventsThisTick,
                SimEventType.ObstacleDestroyed);
            Assert.AreEqual(1, destroyed.EntityId);
            Assert.AreEqual(1, destroyed.X);
            Assert.AreEqual(0, destroyed.Y);
            Assert.AreEqual(14, destroyed.Arg);
        }

        [Test]
        public void ObstaclePlayerContact_AppliesConfiguredDamage()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "contact",
                20,
                new ObstacleSpawn(ObstacleType.Solid, 0, 0, 0)));
            BattleSimConfig config = CreateConfig();
            config.ObstacleHalfWidth = 0;
            config.ObstacleHalfHeight = 0;
            config.ObstacleContactDamage = 2;
            config.PlayerMaxHp = 3;
            var sim = CreateSim(plan, Content(), config, 34UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(2, sim.ShieldStock);
            Assert.AreEqual(1, sim.Obstacles.Count);
            SimEvent hit = FindEvent(
                sim.EventsThisTick,
                SimEventType.PlayerHit);
            Assert.AreEqual(0, hit.EntityId);
            Assert.AreEqual(0, hit.Arg);
        }

        [Test]
        public void EnemyBullets_IntentionallyPassThroughObstacles()
        {
            var shooter = new EnemyDefinition(
                "shooter",
                "Shooter",
                10,
                0,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                1,
                0,
                0,
                0,
                0,
                1,
                1);
            StageSegment segment = new StageSegment(
                "pass",
                20,
                new[] { new SpawnEvent(0, shooter.Id, 10, 0) },
                1,
                1,
                new[] { 1 },
                new[]
                {
                    new ObstacleSpawn(ObstacleType.Solid, 10, 0, 0)
                });
            BattleSimConfig config = CreateConfig();
            config.EnemyBulletSpeedNumerator = 0;
            config.EnemyBulletHalfWidth = 0;
            config.EnemyBulletHalfHeight = 0;
            config.ObstacleHalfWidth = 0;
            config.ObstacleHalfHeight = 0;
            var sim = CreateSim(
                Plan(segment),
                Content(shooter),
                config,
                35UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            Assert.AreEqual(1, sim.Bullets.Count);
            Assert.AreEqual(BulletFaction.Enemy, sim.Bullets[0].Faction);
            Assert.AreEqual(sim.Obstacles[0].X, sim.Bullets[0].X);
            Assert.AreEqual(sim.Obstacles[0].Y, sim.Bullets[0].Y);
        }

        [Test]
        public void MaxObstacles_DeterministicallyCapsActiveList()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "cap",
                20,
                new ObstacleSpawn(ObstacleType.Solid, 10, 0, 0),
                new ObstacleSpawn(
                    ObstacleType.Breakable,
                    20,
                    0,
                    1)));
            BattleSimConfig config = CreateConfig();
            config.MaxObstacles = 1;

            var first = CreateSim(plan, Content(), config, 36UL);
            var second = CreateSim(plan, Content(), config, 36UL);

            Assert.AreEqual(1, first.Obstacles.Count);
            Assert.AreEqual(1, second.Obstacles.Count);
            Assert.AreEqual(ObstacleType.Solid, first.Obstacles[0].Type);
            Assert.AreEqual(
                first.Obstacles[0].X,
                second.Obstacles[0].X);
        }

        [Test]
        public void ObstacleSimulation_IsDeterministicAcrossIdenticalInputs()
        {
            StagePlan plan = Plan(ObstacleSegment(
                "determinism",
                40,
                new ObstacleSpawn(ObstacleType.Solid, 20, -10, 0),
                new ObstacleSpawn(
                    ObstacleType.Breakable,
                    30,
                    10,
                    20)));
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 7;
            config.ScrollSpeedDenominator = 3;
            var first = CreateSim(plan, Content(), config, 37UL);
            var second = CreateSim(plan, Content(), config, 37UL);
            InputCommand none = InputCommand.None;

            for (int tick = 0; tick < 12; tick++)
            {
                first.Step(in none);
                second.Step(in none);
                AssertStatesEqual(first, second, tick);
            }
        }

        [Test]
        public void StageConstructor_RejectsUnknownEnemyReference()
        {
            BattleContent content = Content(Enemy("known", EnemyMovePattern.Static));
            StagePlan plan = Plan(Segment(
                "bad",
                10,
                new SpawnEvent(1, "missing", 0, 0)));

            Assert.Throws<ArgumentException>(() => CreateSim(
                plan, content, CreateConfig(), 7UL));
        }

        static BattleSim CreateSim(
            StagePlan plan,
            BattleContent content,
            BattleSimConfig config,
            ulong seed)
        {
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleContent Content(params EnemyDefinition[] enemies)
        {
            return Content(new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0), enemies);
        }

        static BattleContent Content(
            WeaponDefinition weapon,
            params EnemyDefinition[] enemies)
        {
            return new BattleContent(enemies, new[] { weapon }, weapon.Id);
        }

        static EnemyDefinition Enemy(
            string id,
            EnemyMovePattern pattern,
            int hp = 1,
            int contactDamage = 0,
            int scoreValue = 0,
            int speedNumerator = 0,
            int speedDenominator = 1,
            int dropWeight = 0,
            int sineAmplitude = 0,
            int sinePeriodTicks = 64,
            int movementDelayTicks = 0,
            int movementDurationTicks = 1,
            int movementPauseTicks = 0)
        {
            return new EnemyDefinition(
                id,
                id,
                hp,
                contactDamage,
                scoreValue,
                pattern,
                speedNumerator,
                speedDenominator,
                0,
                0,
                0,
                dropWeight,
                sineAmplitude,
                1,
                sinePeriodTicks,
                movementDelayTicks,
                movementDurationTicks,
                movementPauseTicks);
        }

        static StageSegment Segment(
            string id,
            int lengthTicks,
            params SpawnEvent[] spawns)
        {
            return new StageSegment(id, lengthTicks, spawns, 1, 1, new[] { 1 });
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

        static SimEvent FindEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return events[i];
            Assert.Fail($"Expected event {type}.");
            return default;
        }

        static StagePlan Plan(params StageSegment[] segments)
        {
            return new StagePlan(segments, "boss", 1, 1, 1);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 2,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 1000,
                EnemyDespawnX = -1000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 5,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }

        static void AssertStatesEqual(BattleSim expected, BattleSim actual, int tick)
        {
            Assert.AreEqual(expected.Tick, actual.Tick, $"tick {tick}");
            Assert.AreEqual(expected.ScrollX, actual.ScrollX, $"tick {tick}");
            Assert.AreEqual(expected.PlayerX, actual.PlayerX, $"tick {tick}");
            Assert.AreEqual(expected.PlayerY, actual.PlayerY, $"tick {tick}");
            Assert.AreEqual(expected.PlayerHp, actual.PlayerHp, $"tick {tick}");
            Assert.AreEqual(expected.Score, actual.Score, $"tick {tick}");
            Assert.AreEqual(expected.Bullets.Count, actual.Bullets.Count, $"tick {tick}");
            Assert.AreEqual(expected.Enemies.Count, actual.Enemies.Count, $"tick {tick}");
            Assert.AreEqual(expected.Obstacles.Count, actual.Obstacles.Count, $"tick {tick}");
            Assert.AreEqual(expected.Capsules.Count, actual.Capsules.Count, $"tick {tick}");

            for (int i = 0; i < expected.Bullets.Count; i++)
            {
                Assert.AreEqual(expected.Bullets[i].Id, actual.Bullets[i].Id, $"tick {tick}, bullet {i}");
                Assert.AreEqual(expected.Bullets[i].X, actual.Bullets[i].X, $"tick {tick}, bullet {i}");
                Assert.AreEqual(expected.Bullets[i].Y, actual.Bullets[i].Y, $"tick {tick}, bullet {i}");
            }

            for (int i = 0; i < expected.Obstacles.Count; i++)
            {
                Assert.AreEqual(expected.Obstacles[i].Id, actual.Obstacles[i].Id, $"tick {tick}, obstacle {i}");
                Assert.AreEqual(expected.Obstacles[i].Type, actual.Obstacles[i].Type, $"tick {tick}, obstacle {i}");
                Assert.AreEqual(expected.Obstacles[i].X, actual.Obstacles[i].X, $"tick {tick}, obstacle {i}");
                Assert.AreEqual(expected.Obstacles[i].Y, actual.Obstacles[i].Y, $"tick {tick}, obstacle {i}");
                Assert.AreEqual(expected.Obstacles[i].Hp, actual.Obstacles[i].Hp, $"tick {tick}, obstacle {i}");
            }

            for (int i = 0; i < expected.Enemies.Count; i++)
            {
                Assert.AreEqual(expected.Enemies[i].Id, actual.Enemies[i].Id, $"tick {tick}, enemy {i}");
                Assert.AreEqual(expected.Enemies[i].DefinitionId, actual.Enemies[i].DefinitionId, $"tick {tick}, enemy {i}");
                Assert.AreEqual(expected.Enemies[i].X, actual.Enemies[i].X, $"tick {tick}, enemy {i}");
                Assert.AreEqual(expected.Enemies[i].Y, actual.Enemies[i].Y, $"tick {tick}, enemy {i}");
                Assert.AreEqual(expected.Enemies[i].Hp, actual.Enemies[i].Hp, $"tick {tick}, enemy {i}");
            }

            for (int i = 0; i < expected.Capsules.Count; i++)
            {
                Assert.AreEqual(expected.Capsules[i].Id, actual.Capsules[i].Id, $"tick {tick}, capsule {i}");
                Assert.AreEqual(expected.Capsules[i].X, actual.Capsules[i].X, $"tick {tick}, capsule {i}");
                Assert.AreEqual(expected.Capsules[i].Y, actual.Capsules[i].Y, $"tick {tick}, capsule {i}");
            }
        }
    }
}
