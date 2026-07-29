using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BattleModifierTests
    {
        [Test]
        public void PierceShotHitsTwoEnemiesWithoutRepeatedOverlapDamage()
        {
            BattleSim sim = CreateSim(
                BattleModifier.PierceShot,
                Config(),
                Gauge(),
                Enemy("first", 2, halfWidth: 100),
                Enemy("second", 1),
                Spawn(0, "first", 200, 0),
                Spawn(0, "second", 200, 0));

            FireOnceThenStep(sim, 2);

            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual("first", sim.Enemies[0].DefinitionId);
            Assert.AreEqual(1, sim.Enemies[0].Hp);
            Assert.AreEqual(2L, sim.Statistics.ShotsHit);
            Assert.AreEqual(0, sim.Bullets.Count);
        }

        [Test]
        public void PierceHistoryPreventsAbaRepeatWhenConfiguredAboveDefault()
        {
            BattleSimConfig config = Config();
            config.PierceShotEnemyCount = 2;
            BattleSim sim = CreateSim(
                BattleModifier.PierceShot,
                config,
                Gauge(),
                Enemy("wide", 2, halfWidth: 200),
                Enemy("middle", 1),
                Spawn(0, "wide", 300, 0),
                Spawn(0, "middle", 200, 0));

            FireOnceThenStep(sim, 3);

            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual("wide", sim.Enemies[0].DefinitionId);
            Assert.AreEqual(
                1,
                sim.Enemies[0].Hp,
                "The wide A target must not be damaged again after hitting B.");
            Assert.AreEqual(2L, sim.Statistics.ShotsHit);
            Assert.AreEqual(1, sim.Bullets.Count);
        }

        [Test]
        public void RicochetSelectsNearestOtherEnemyAndEmitsTargetId()
        {
            BattleSimConfig config = Config();
            config.RicochetRangeSubUnits = 500;
            BattleSim sim = CreateSim(
                BattleModifier.Ricochet,
                config,
                Gauge(),
                Enemy("source", 1),
                Enemy("nearest", 1),
                Enemy("farther", 1),
                Spawn(0, "source", 100, 0),
                Spawn(0, "nearest", 100, 200),
                Spawn(0, "farther", 100, 400));

            FireOnceThenStep(sim, 1);

            SimEvent ricochet = FindEvent(sim, SimEventType.BulletRicocheted);
            Assert.AreEqual(1, ricochet.EntityId);
            Assert.AreEqual(2, ricochet.Arg);
            Assert.AreEqual(100, ricochet.X);
            Assert.AreEqual(0, ricochet.Y);

            Step(sim, 2);
            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual("farther", sim.Enemies[0].DefinitionId);
            Assert.AreEqual(
                0,
                CountEvents(sim, SimEventType.BulletRicocheted),
                "A bullet may ricochet only once.");
        }

        [Test]
        public void HomingMissileTurnsTowardNearestAndBreaksDistanceTieByEnemyId()
        {
            BattleSimConfig config = Config();
            config.MissileSpeedXNumerator = 100;
            config.MissileSpeedXDenominator = 1;
            config.MissileFallSpeedYNumerator = 0;
            config.MissileFallSpeedYDenominator = 1;
            config.HomingMissileTurnLutSlotsPerTick = 1;
            BattleSim sim = CreateSim(
                BattleModifier.HomingMissile,
                config,
                Gauge(missileLevel: 1),
                Enemy("upper", 10),
                Enemy("lower", 10),
                Spawn(0, "upper", 500, 500),
                Spawn(0, "lower", 500, -500));

            var fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            InputCommand none = InputCommand.None;
            sim.Step(in none);

            BulletState missile = FindBullet(sim, BulletKind.Missile);
            Assert.Greater(
                missile.Y,
                0,
                "Equal-distance targets must lock the lower id (the upper enemy).");
        }

        [Test]
        public void KillExplosionDamagesRadiusOnceAndDoesNotChain()
        {
            BattleSimConfig config = Config();
            config.KillExplosionRadiusSubUnits = 100;
            config.KillExplosionDamage = 1;
            BattleSim sim = CreateSim(
                BattleModifier.KillExplosion,
                config,
                Gauge(),
                Enemy("source", 1),
                Enemy("survivor", 2),
                Enemy("secondary_kill", 1),
                Spawn(0, "source", 100, 0),
                Spawn(0, "survivor", 150, 0),
                Spawn(0, "secondary_kill", 160, 0));

            FireOnceThenStep(sim, 1);

            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual("survivor", sim.Enemies[0].DefinitionId);
            Assert.AreEqual(1, sim.Enemies[0].Hp);
            Assert.AreEqual(1, CountEvents(sim, SimEventType.KillExplosionTriggered));
            Assert.AreEqual(2, CountEvents(sim, SimEventType.EnemyKilled));
            Assert.AreEqual(2L, sim.Statistics.Kills);
            Assert.AreEqual(
                1L,
                sim.Statistics.ShotsHit,
                "Explosion damage is not an additional projectile hit.");
        }

        [Test]
        public void KillExplosionCapsTargetsByDistanceThenLowerId()
        {
            BattleSimConfig config = Config();
            config.KillExplosionRadiusSubUnits = 100;
            config.KillExplosionDamage = 1;
            config.KillExplosionMaxTargets = 4;
            BattleSim sim = CreateSim(
                BattleModifier.KillExplosion,
                config,
                Gauge(),
                123UL,
                new[]
                {
                    Enemy("source", 1),
                    Enemy("tie_low", 2),
                    Enemy("farther", 2),
                    Enemy("nearest", 2),
                    Enemy("tie_high", 2),
                    Enemy("second_nearest", 2),
                    Enemy("third_nearest", 2)
                },
                new[]
                {
                    Spawn(0, "source", 100, 0),
                    Spawn(0, "tie_low", 100, 30),
                    Spawn(0, "farther", 100, 40),
                    Spawn(0, "nearest", 100, 10),
                    Spawn(0, "tie_high", 100, -30),
                    Spawn(0, "second_nearest", 100, 20),
                    Spawn(0, "third_nearest", 100, 25)
                });

            FireOnceThenStep(sim, 1);

            Assert.AreEqual(6, sim.Enemies.Count);
            Assert.AreEqual(1, sim.Enemies[0].Hp, "Lower id wins the distance tie.");
            Assert.AreEqual(2, sim.Enemies[1].Hp, "A farther target is capped.");
            Assert.AreEqual(1, sim.Enemies[2].Hp);
            Assert.AreEqual(2, sim.Enemies[3].Hp, "Higher id loses the distance tie.");
            Assert.AreEqual(1, sim.Enemies[4].Hp);
            Assert.AreEqual(1, sim.Enemies[5].Hp);
        }

        [Test]
        public void CombinedModifiersRemainDeterministicForSameSeed()
        {
            const BattleModifier all =
                BattleModifier.PierceShot
                | BattleModifier.Ricochet
                | BattleModifier.HomingMissile
                | BattleModifier.KillExplosion;
            BattleSim first = CreateDeterminismSim(all, 918273UL);
            BattleSim second = CreateDeterminismSim(all, 918273UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            for (int tick = 0; tick < 12; tick++)
            {
                InputCommand input = tick == 0 ? fire : none;
                first.Step(in input);
                second.Step(in input);
                AssertStateEqual(first, second);
            }
        }

        [Test]
        public void ModifierCollisionScanDoesNotAllocateAfterConstruction()
        {
            // Warm all code paths before taking a per-thread allocation sample.
            BattleSim warmup = CreateExplosionScanSim();
            FireOnceThenStep(warmup, 1);

            BattleSim measured = CreateExplosionScanSim();
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            long before = GC.GetAllocatedBytesForCurrentThread();
            measured.Step(in fire);
            measured.Step(in none);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(
                0L,
                allocated,
                "Modifier scans and parallel bullet state must use preallocated buffers.");
        }

        [Test]
        public void RunManagerAppliesModifierAcrossStagesAndDeathRestart()
        {
            var modifierRewards = new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    ModifierReward("pierce_a"),
                    ModifierReward("pierce_b"),
                    ModifierReward("pierce_c")
                });
            BattleSimConfig config = Config();
            config.BulletDespawnX = 100;
            var weapon = new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0);
            EnemyDefinition lethal = new EnemyDefinition(
                "lethal",
                "lethal",
                1,
                config.PlayerMaxHp,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                0,
                1,
                64);
            var content = new BattleContent(
                new[] { lethal },
                new[] { weapon },
                weapon.Id);
            var run = new RunManager(
                77UL,
                new ModifierRunGenerator(),
                config,
                content,
                Gauge(),
                modifierRewards);

            var fire = new InputCommand(0, 0, true);
            for (int i = 0; i < 2000 && run.State == RunState.Playing; i++)
                run.Step(in fire);
            Assert.AreEqual(RunState.AwaitingReward, run.State);

            run.ChooseReward(0);
            Assert.AreEqual(BattleModifier.PierceShot, run.ActiveModifiers);
            InputCommand none = InputCommand.None;
            run.Step(in none);
            Assert.AreEqual(RunState.RunOver, run.State);

            run.Restart(78UL);
            Assert.AreEqual(
                BattleModifier.PierceShot,
                run.ActiveModifiers,
                "Death restart follows the power-up carry policy.");

            var fresh = new RunManager(
                78UL,
                new ModifierRunGenerator(),
                config,
                content,
                Gauge(),
                modifierRewards);
            Assert.AreEqual(BattleModifier.None, fresh.ActiveModifiers);
        }

        static BattleSim CreateDeterminismSim(
            BattleModifier modifiers,
            ulong seed)
        {
            BattleSimConfig config = Config();
            config.RicochetRangeSubUnits = 500;
            config.KillExplosionRadiusSubUnits = 80;
            config.KillExplosionDamage = 1;
            config.MissileSpeedXNumerator = 100;
            config.MissileSpeedXDenominator = 1;
            config.MissileFallSpeedYNumerator = 0;
            config.MissileFallSpeedYDenominator = 1;
            return CreateSim(
                modifiers,
                config,
                Gauge(missileLevel: 1),
                seed,
                new[]
                {
                    Enemy("a", 1),
                    Enemy("b", 2),
                    Enemy("c", 1),
                    Enemy("d", 2)
                },
                new[]
                {
                    Spawn(0, "a", 100, 0),
                    Spawn(0, "b", 200, 50),
                    Spawn(0, "c", 300, -50),
                    Spawn(0, "d", 400, 100)
                });
        }

        static RewardDefinition ModifierReward(string id)
        {
            return new RewardDefinition(
                id,
                RewardType.Modifier,
                PowerUpSlot.MainShot,
                1,
                1,
                1,
                99,
                null,
                BattleModifier.PierceShot);
        }

        static BattleSim CreateExplosionScanSim()
        {
            const int count = 24;
            var enemies = new EnemyDefinition[count];
            var spawns = new SpawnEvent[count];
            for (int i = 0; i < count; i++)
            {
                string id = "enemy_" + i;
                enemies[i] = Enemy(id, 1);
                spawns[i] = Spawn(0, id, 100 + i, 0);
            }
            BattleSimConfig config = Config();
            config.KillExplosionRadiusSubUnits = count;
            config.KillExplosionDamage = 1;
            return CreateSim(
                BattleModifier.KillExplosion,
                config,
                Gauge(),
                44UL,
                enemies,
                spawns);
        }

        static BattleSim CreateSim(
            BattleModifier modifiers,
            BattleSimConfig config,
            PowerUpGauge gauge,
            EnemyDefinition first,
            EnemyDefinition second,
            params SpawnEvent[] spawns)
        {
            return CreateSim(
                modifiers,
                config,
                gauge,
                123UL,
                new[] { first, second },
                spawns);
        }

        static BattleSim CreateSim(
            BattleModifier modifiers,
            BattleSimConfig config,
            PowerUpGauge gauge,
            EnemyDefinition first,
            EnemyDefinition second,
            EnemyDefinition third,
            params SpawnEvent[] spawns)
        {
            return CreateSim(
                modifiers,
                config,
                gauge,
                123UL,
                new[] { first, second, third },
                spawns);
        }

        static BattleSim CreateSim(
            BattleModifier modifiers,
            BattleSimConfig config,
            PowerUpGauge gauge,
            ulong seed,
            EnemyDefinition[] enemies,
            SpawnEvent[] spawns)
        {
            var weapon = new WeaponDefinition("shot", 1, 100, 100, 1, 0, 0);
            var content = new BattleContent(enemies, new[] { weapon }, weapon.Id);
            var segment = new StageSegment(
                "modifier_test",
                100,
                spawns,
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                content,
                gauge,
                modifiers);
        }

        static BattleSimConfig Config()
        {
            return new BattleSimConfig
            {
                PlayerSpeedNumerator = 0,
                PlayerSpeedDenominator = 1,
                PlayerBulletSpeedNumerator = 100,
                PlayerBulletSpeedDenominator = 1,
                FireIntervalTicks = 100,
                MaxBullets = 32,
                PlayerMinX = -10000,
                PlayerMaxX = 10000,
                PlayerMinY = -10000,
                PlayerMaxY = 10000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 10,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                MissileBaseDamage = 1,
                MissileFireIntervalTicks = 100,
                MissileSpeedXNumerator = 100,
                MissileSpeedXDenominator = 1,
                MissileFallSpeedYNumerator = 0,
                MissileFallSpeedYDenominator = 1,
                MissileHalfWidth = 0,
                MissileHalfHeight = 0,
                EnemyBulletDamage = 0,
                MaxEnemyBullets = 0
            };
        }

        static PowerUpGauge Gauge(int missileLevel = 0)
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 0, missileLevel, 0, 0 });
            return gauge;
        }

        static EnemyDefinition Enemy(string id, int hp, int halfWidth = 0)
        {
            return new EnemyDefinition(
                id,
                id,
                hp,
                0,
                1,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                halfWidth,
                0,
                0,
                0,
                1,
                64);
        }

        static SpawnEvent Spawn(int tick, string id, int x, int y)
        {
            return new SpawnEvent(tick, id, x, y);
        }

        static void FireOnceThenStep(BattleSim sim, int followingSteps)
        {
            var fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            InputCommand none = InputCommand.None;
            Step(sim, followingSteps, in none);
        }

        static void Step(BattleSim sim, int count)
        {
            InputCommand none = InputCommand.None;
            Step(sim, count, in none);
        }

        static void Step(BattleSim sim, int count, in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                sim.Step(in input);
        }

        static SimEvent FindEvent(BattleSim sim, SimEventType type)
        {
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type == type)
                    return events[i];
            }
            Assert.Fail("Expected event " + type + " was not emitted.");
            return default;
        }

        static int CountEvents(BattleSim sim, SimEventType type)
        {
            int count = 0;
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type == type)
                    count++;
            }
            return count;
        }

        static BulletState FindBullet(BattleSim sim, BulletKind kind)
        {
            for (int i = 0; i < sim.Bullets.Count; i++)
            {
                if (sim.Bullets[i].Kind == kind)
                    return sim.Bullets[i];
            }
            Assert.Fail("Expected bullet " + kind + " was not found.");
            return default;
        }

        static void AssertStateEqual(BattleSim first, BattleSim second)
        {
            Assert.AreEqual(first.Tick, second.Tick);
            Assert.AreEqual(first.Enemies.Count, second.Enemies.Count);
            for (int i = 0; i < first.Enemies.Count; i++)
            {
                Assert.AreEqual(first.Enemies[i].Id, second.Enemies[i].Id);
                Assert.AreEqual(first.Enemies[i].X, second.Enemies[i].X);
                Assert.AreEqual(first.Enemies[i].Y, second.Enemies[i].Y);
                Assert.AreEqual(first.Enemies[i].Hp, second.Enemies[i].Hp);
            }
            Assert.AreEqual(first.Bullets.Count, second.Bullets.Count);
            for (int i = 0; i < first.Bullets.Count; i++)
            {
                Assert.AreEqual(first.Bullets[i].Id, second.Bullets[i].Id);
                Assert.AreEqual(first.Bullets[i].X, second.Bullets[i].X);
                Assert.AreEqual(first.Bullets[i].Y, second.Bullets[i].Y);
                Assert.AreEqual(first.Bullets[i].Kind, second.Bullets[i].Kind);
            }
            ReadOnlySpan<SimEvent> firstEvents = first.EventsThisTick;
            ReadOnlySpan<SimEvent> secondEvents = second.EventsThisTick;
            Assert.AreEqual(firstEvents.Length, secondEvents.Length);
            for (int i = 0; i < firstEvents.Length; i++)
            {
                Assert.AreEqual(firstEvents[i].Type, secondEvents[i].Type);
                Assert.AreEqual(firstEvents[i].EntityId, secondEvents[i].EntityId);
                Assert.AreEqual(firstEvents[i].X, secondEvents[i].X);
                Assert.AreEqual(firstEvents[i].Y, secondEvents[i].Y);
                Assert.AreEqual(firstEvents[i].Arg, secondEvents[i].Arg);
            }
        }

        sealed class ModifierRunGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                if (stageIndex == 1)
                {
                    var intro = new StageSegment(
                        "intro",
                        1,
                        new SpawnEvent[0],
                        1,
                        1,
                        new[] { 1 });
                    return new StagePlan(
                        new[] { intro },
                        "boss",
                        1,
                        1,
                        1,
                        1,
                        0,
                        0,
                        100,
                        new[] { new BossPhase(999, 1, 1, 1) });
                }

                var lethal = new StageSegment(
                    "lethal",
                    10,
                    new[] { Spawn(0, "lethal", 0, 0) },
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { lethal },
                    "legacy",
                    1,
                    1,
                    1);
            }
        }
    }
}
