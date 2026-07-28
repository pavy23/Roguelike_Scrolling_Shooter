using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BattlePowerUpSimTests
    {
        [Test]
        public void MainShotLevel_UsesDamageCurveAndReducesHighLevelFireInterval()
        {
            var gauge = Gauge(3, 0, 0, 0);
            WeaponDefinition weapon = Weapon(baseDamage: 4, interval: 4, speed: 1);
            EnemyDefinition target = Enemy("target", hp: 10);
            StagePlan targetPlan = Plan(Segment(
                "target_segment",
                20,
                new SpawnEvent(1, target.Id, 1, 0)));
            var damageSim = CreateSim(
                CreateConfig(), gauge, targetPlan, Content(weapon, target), 1UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            damageSim.Step(in fire);
            damageSim.Step(in none);

            Assert.AreEqual(1, damageSim.Enemies.Count);
            Assert.AreEqual(2, damageSim.Enemies[0].Hp);

            var rapidSim = CreateSim(
                CreateConfig(),
                Gauge(3, 0, 0, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 4, speed: 0)),
                2UL);
            for (int i = 0; i < 4; i++)
                rapidSim.Step(in fire);

            Assert.AreEqual(2, rapidSim.Bullets.Count);
        }

        [Test]
        public void MissileLevel_FiresDistinguishableForwardFallingProjectilesPeriodically()
        {
            BattleSimConfig config = CreateConfig();
            config.MissileFireIntervalTicks = 2;
            config.MissileMinimumFireIntervalTicks = 0;
            config.MissileFireIntervalReductionPerLevel = 0;
            config.MissileSpeedXNumerator = 2;
            config.MissileSpeedXDenominator = 1;
            config.MissileFallSpeedYNumerator = 1;
            config.MissileFallSpeedYDenominator = 1;
            var sim = CreateSim(
                config,
                Gauge(0, 1, 0, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 100, speed: 0)),
                3UL);
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            Assert.AreEqual(2, sim.Bullets.Count);
            Assert.AreEqual(BulletKind.MainShot, sim.Bullets[0].Kind);
            Assert.AreEqual(BulletKind.Missile, sim.Bullets[1].Kind);

            sim.Step(in fire);
            Assert.AreEqual(2, sim.Bullets[1].X);
            Assert.AreEqual(-1, sim.Bullets[1].Y);

            sim.Step(in fire);
            Assert.AreEqual(2, CountBullets(sim.Bullets, BulletKind.Missile));
            Assert.AreEqual(BulletKind.Missile, sim.Bullets[2].Kind);
            Assert.AreEqual(0, sim.Bullets[2].X);
            Assert.AreEqual(0, sim.Bullets[2].Y);
        }

        [Test]
        public void OptionLevel_ExposesFollowingOffsetsAndMirrorsMainShotVolley()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 2;
            config.OptionOffsetXStep = -10;
            config.OptionOffsetYStep = 3;
            var sim = CreateSim(
                config,
                Gauge(0, 0, 2, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 4, speed: 0)),
                4UL);
            IReadOnlyList<OptionState> options = sim.Options;

            Assert.AreEqual(2, options.Count);
            AssertOption(options[0], 1, -10, 3);
            AssertOption(options[1], 2, -20, 6);

            var moveAndFire = new InputCommand(1, 1, true);
            sim.Step(in moveAndFire);

            Assert.AreSame(options, sim.Options);
            Assert.IsFalse(options is List<OptionState>);
            AssertOption(options[0], 1, -8, 5);
            AssertOption(options[1], 2, -18, 8);
            Assert.AreEqual(3, sim.Bullets.Count);
            AssertBullet(sim.Bullets[0], BulletKind.MainShot, 2, 2);
            AssertBullet(sim.Bullets[1], BulletKind.MainShot, -8, 5);
            AssertBullet(sim.Bullets[2], BulletKind.MainShot, -18, 8);
        }

        [Test]
        public void ShieldLevel_AbsorbsContactDamageUntilSpentAndRefreshesOnUpgrade()
        {
            EnemyDefinition heavy = Enemy("heavy", contactDamage: 3);
            EnemyDefinition light = Enemy("light", contactDamage: 1);
            EnemyDefinition afterUpgrade = Enemy("after_upgrade", contactDamage: 2);
            StagePlan plan = Plan(Segment(
                "contacts",
                10,
                new SpawnEvent(1, heavy.Id, 0, 0),
                new SpawnEvent(2, light.Id, 0, 0),
                new SpawnEvent(3, afterUpgrade.Id, 0, 0)));
            var gauge = Gauge(0, 0, 0, 2);
            var sim = CreateSim(
                CreateConfig(),
                gauge,
                plan,
                Content(Weapon(), heavy, light, afterUpgrade),
                5UL);
            InputCommand none = InputCommand.None;

            Assert.AreEqual(2, sim.ShieldRemaining);
            sim.Step(in none);
            Assert.AreEqual(4, sim.PlayerHp);
            Assert.AreEqual(0, sim.ShieldRemaining);

            sim.Step(in none);
            Assert.AreEqual(3, sim.PlayerHp);
            Assert.AreEqual(0, sim.ShieldRemaining);

            gauge.ImportLevels(new[] { 0, 0, 0, 3 });
            sim.Step(in none);
            Assert.AreEqual(3, sim.PlayerHp);
            Assert.AreEqual(1, sim.ShieldRemaining);
        }

        [Test]
        public void SameLevelsSeedAndInputs_ProduceIdenticalPowerUpStates()
        {
            EnemyDefinition rammer = Enemy("rammer", contactDamage: 2);
            StagePlan plan = Plan(Segment(
                "deterministic_powerups",
                60,
                new SpawnEvent(2, rammer.Id, 0, 0)));
            BattleContent content = Content(
                Weapon(baseDamage: 2, interval: 6, speed: 3), rammer);
            BattleSimConfig config = CreateConfig();
            config.MissileBaseDamage = 3;
            config.MissileFireIntervalTicks = 5;
            config.MissileMinimumFireIntervalTicks = 2;
            config.MissileFireIntervalReductionPerLevel = 1;
            config.MissileSpeedXNumerator = 5;
            config.MissileSpeedXDenominator = 2;
            config.MissileFallSpeedYNumerator = 3;
            config.MissileFallSpeedYDenominator = 2;
            config.OptionOffsetXStep = -7;
            config.OptionOffsetYStep = 2;
            var first = CreateSim(
                config, Gauge(4, 2, 2, 3), plan, content, 0xC0FFEEUL);
            var second = CreateSim(
                config, Gauge(4, 2, 2, 3), plan, content, 0xC0FFEEUL);

            for (int tick = 0; tick < 30; tick++)
            {
                var input = new InputCommand(
                    tick < 3 ? 0 : tick % 7 < 3 ? 1 : -1,
                    tick % 5 == 0 ? 1 : tick % 5 == 1 ? -1 : 0,
                    tick % 4 != 0);
                first.Step(in input);
                second.Step(in input);
                AssertStatesEqual(first, second, tick);
            }

            Assert.Greater(CountBullets(first.Bullets, BulletKind.Missile), 0);
            Assert.AreEqual(2, first.Options.Count);
            Assert.Less(first.ShieldRemaining, 3);
        }

        static BattleSim CreateSim(
            BattleSimConfig config,
            PowerUpGauge gauge,
            StagePlan plan,
            BattleContent content,
            ulong seed)
        {
            return new BattleSim(config, new Rng(seed), plan, content, gauge);
        }

        static PowerUpGauge Gauge(int mainShot, int missile, int option, int shield)
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { mainShot, missile, option, shield });
            return gauge;
        }

        static BattleContent Content(
            WeaponDefinition weapon,
            params EnemyDefinition[] enemies)
        {
            return new BattleContent(enemies, new[] { weapon }, weapon.Id);
        }

        static WeaponDefinition Weapon(
            int baseDamage = 1,
            int interval = 10,
            int speed = 0)
        {
            return new WeaponDefinition("shot", baseDamage, interval, speed, 1, 0, 0);
        }

        static EnemyDefinition Enemy(
            string id,
            int hp = 1,
            int contactDamage = 0)
        {
            return new EnemyDefinition(
                id,
                hp,
                contactDamage,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                64);
        }

        static StagePlan EmptyPlan()
        {
            return Plan(Segment("empty", 100));
        }

        static StageSegment Segment(
            string id,
            int lengthTicks,
            params SpawnEvent[] spawns)
        {
            return new StageSegment(id, lengthTicks, spawns, 1, 1, new[] { 1 });
        }

        static StagePlan Plan(params StageSegment[] segments)
        {
            return new StagePlan(segments, "boss", 1, 1, 1);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 256,
                PlayerMinX = -10000,
                PlayerMaxX = 10000,
                PlayerMinY = -10000,
                PlayerMaxY = 10000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 5,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                MissileHalfWidth = 0,
                MissileHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                MainShotRapidFireStartLevel = 3,
                MainShotFireIntervalReductionPerLevel = 1,
                MainShotMinimumFireIntervalTicks = 0
            };
        }

        static int CountBullets(IReadOnlyList<BulletState> bullets, BulletKind kind)
        {
            int count = 0;
            for (int i = 0; i < bullets.Count; i++)
                if (bullets[i].Kind == kind)
                    count++;
            return count;
        }

        static void AssertOption(OptionState option, int index, int x, int y)
        {
            Assert.AreEqual(index, option.Index);
            Assert.AreEqual(x, option.X);
            Assert.AreEqual(y, option.Y);
        }

        static void AssertBullet(BulletState bullet, BulletKind kind, int x, int y)
        {
            Assert.AreEqual(kind, bullet.Kind);
            Assert.AreEqual(x, bullet.X);
            Assert.AreEqual(y, bullet.Y);
        }

        static void AssertStatesEqual(BattleSim expected, BattleSim actual, int tick)
        {
            Assert.AreEqual(expected.Tick, actual.Tick, $"tick {tick}");
            Assert.AreEqual(expected.PlayerX, actual.PlayerX, $"tick {tick}");
            Assert.AreEqual(expected.PlayerY, actual.PlayerY, $"tick {tick}");
            Assert.AreEqual(expected.PlayerHp, actual.PlayerHp, $"tick {tick}");
            Assert.AreEqual(expected.ShieldRemaining, actual.ShieldRemaining, $"tick {tick}");
            Assert.AreEqual(expected.Bullets.Count, actual.Bullets.Count, $"tick {tick}");
            Assert.AreEqual(expected.Options.Count, actual.Options.Count, $"tick {tick}");
            Assert.AreEqual(expected.Enemies.Count, actual.Enemies.Count, $"tick {tick}");

            for (int i = 0; i < expected.Bullets.Count; i++)
            {
                BulletState left = expected.Bullets[i];
                BulletState right = actual.Bullets[i];
                Assert.AreEqual(left.Id, right.Id, $"tick {tick}, bullet {i}");
                Assert.AreEqual(left.Faction, right.Faction, $"tick {tick}, bullet {i}");
                Assert.AreEqual(left.Kind, right.Kind, $"tick {tick}, bullet {i}");
                Assert.AreEqual(left.X, right.X, $"tick {tick}, bullet {i}");
                Assert.AreEqual(left.Y, right.Y, $"tick {tick}, bullet {i}");
            }

            for (int i = 0; i < expected.Options.Count; i++)
            {
                OptionState left = expected.Options[i];
                OptionState right = actual.Options[i];
                Assert.AreEqual(left.Index, right.Index, $"tick {tick}, option {i}");
                Assert.AreEqual(left.X, right.X, $"tick {tick}, option {i}");
                Assert.AreEqual(left.Y, right.Y, $"tick {tick}, option {i}");
            }

            for (int i = 0; i < expected.Enemies.Count; i++)
            {
                EnemyState left = expected.Enemies[i];
                EnemyState right = actual.Enemies[i];
                Assert.AreEqual(left.Id, right.Id, $"tick {tick}, enemy {i}");
                Assert.AreEqual(left.DefinitionId, right.DefinitionId, $"tick {tick}, enemy {i}");
                Assert.AreEqual(left.X, right.X, $"tick {tick}, enemy {i}");
                Assert.AreEqual(left.Y, right.Y, $"tick {tick}, enemy {i}");
                Assert.AreEqual(left.Hp, right.Hp, $"tick {tick}, enemy {i}");
            }
        }
    }
}
