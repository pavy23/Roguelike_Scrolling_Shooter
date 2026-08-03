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
        public void OptionFollowDelayDefaultsToTwelveTicks()
        {
            Assert.AreEqual(12, new BattleSimConfig().OptionFollowDelayTicks);
            Assert.AreEqual(
                100,
                new BattleSimConfig().OptionMissileDamagePercent);
        }

        [Test]
        public void ActivateInput_UsesRisingEdgesWithoutRepeatingWhileHeld()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            var sim = CreateSim(
                CreateConfig(),
                gauge,
                EmptyPlan(),
                Content(Weapon()),
                0xAC71UL);
            var held = new InputCommand(0, 0, false, true);
            InputCommand released = InputCommand.None;

            sim.Step(in held);
            gauge.Collect();
            sim.Step(in held);

            Assert.AreEqual(
                1,
                gauge.GetLevel(PowerUpSlot.Speed));
            Assert.AreEqual(0, gauge.Cursor);

            sim.Step(in released);
            sim.Step(in held);

            Assert.AreEqual(
                1,
                gauge.GetLevel(PowerUpSlot.Speed));
            Assert.AreEqual(
                1,
                gauge.GetProgress(PowerUpSlot.Speed));
            Assert.AreEqual(PowerUpGauge.NoSelection, gauge.Cursor);
            Assert.AreEqual(0, sim.EventsThisTick.Length);
        }

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
        public void SpeedGaugeLevelAddsExactMovementCurve()
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.GrantLevels(PowerUpSlot.Speed, 1);
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedNumerator = SimSpace.TicksPerSecond;
            config.PlayerSpeedDenominator = SimSpace.TicksPerSecond;
            BattleSim sim = CreateSim(
                config,
                gauge,
                EmptyPlan(),
                Content(Weapon()),
                0x5EEDUL);
            var right = new InputCommand(1, 0, false);

            for (int tick = 0;
                tick < SimSpace.TicksPerSecond;
                tick++)
                sim.Step(in right);

            Assert.AreEqual(
                SimSpace.TicksPerSecond
                    + SimSpace.SubUnitsPerWorldUnit,
                sim.PlayerX);
        }

        [Test]
        public void GaugeWeaponModesSwitchImmediatelyAndRemainMutuallyExclusive()
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.GrantLevels(PowerUpSlot.MainShot, 2);
            BattleSim sim = CreateSim(
                CreateConfig(),
                gauge,
                EmptyPlan(),
                Content(Weapon(baseDamage: 10)),
                0xD0B1EUL);
            InputCommand none = InputCommand.None;

            gauge.GrantLevels(PowerUpSlot.Double, 1);
            sim.Step(in none);
            Assert.AreEqual(
                PrimaryWeaponFamily.Double,
                sim.EquippedPrimaryWeaponFamily);
            Assert.AreEqual(WeaponType.Spread, sim.PlayerWeaponType);
            Assert.AreEqual(
                PowerUpWeaponMode.Double,
                gauge.ActiveWeaponMode);

            gauge.GrantLevels(PowerUpSlot.Laser, 1);
            sim.Step(in none);
            Assert.AreEqual(
                PrimaryWeaponFamily.Laser,
                sim.EquippedPrimaryWeaponFamily);
            Assert.AreEqual(WeaponType.Laser, sim.PlayerWeaponType);
            Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Double));
            Assert.AreEqual(2, gauge.GetLevel(PowerUpSlot.MainShot));

            gauge.GrantLevels(PowerUpSlot.Triple, 1);
            sim.Step(in none);
            Assert.AreEqual(
                PrimaryWeaponFamily.Spread,
                sim.EquippedPrimaryWeaponFamily);
            Assert.AreEqual(WeaponType.Spread, sim.PlayerWeaponType);
            Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Laser));
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Triple));
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
        public void OptionLevel_FollowsDelayedPlayerHistoryAndMirrorsMainShotVolley()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 2;
            config.OptionFollowDelayTicks = 2;
            var sim = CreateSim(
                config,
                Gauge(0, 0, 2, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 4, speed: 0)),
                4UL);
            IReadOnlyList<OptionState> options = sim.Options;

            Assert.AreEqual(2, options.Count);
            AssertOption(options[0], 1, 0, 0);
            AssertOption(options[1], 2, 0, 0);

            Step(sim, 1, 0, false);
            Step(sim, 1, 0, false);
            Step(sim, 0, 1, false);
            Step(sim, -1, 0, false);
            Step(sim, -1, 0, true);

            Assert.AreSame(options, sim.Options);
            Assert.IsFalse(options is List<OptionState>);
            AssertOption(options[0], 1, 4, 2);
            AssertOption(options[1], 2, 2, 0);
            Assert.AreEqual(3, sim.Bullets.Count);
            AssertBullet(sim.Bullets[0], BulletKind.MainShot, 0, 2);
            AssertBullet(sim.Bullets[1], BulletKind.MainShot, 4, 2);
            AssertBullet(sim.Bullets[2], BulletKind.MainShot, 2, 0);
        }

        [Test]
        public void SixOptionsUseFullTrailHistoryCapacity()
        {
            BattleSimConfig config = CreateConfig();
            config.OptionFollowDelayTicks = 1;
            var sim = CreateSim(
                config,
                Gauge(0, 0, PowerUpGauge.MaximumOptionCount, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 100, speed: 0)),
                84UL);

            for (int tick = 0; tick < PowerUpGauge.MaximumOptionCount; tick++)
                Step(sim, 1, 0, false);

            Assert.AreEqual(PowerUpGauge.MaximumOptionCount, sim.Options.Count);
            for (int i = 0; i < sim.Options.Count; i++)
                AssertOption(
                    sim.Options[i],
                    i + 1,
                    PowerUpGauge.MaximumOptionCount - 1 - i,
                    0);
        }

        [Test]
        public void SixOptionMainVolleyUsesAvailableBudgetInEmitterOrder()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 0;
            config.PlayerBulletSpeedPerTick = 0;
            config.OptionFormation = OptionFormation.Fixed;
            config.OptionFixedOffsetXs = new[] { 10, 20, 30, 40, 50, 60 };
            config.OptionFixedOffsetYs = new[] { 0, 0, 0, 0, 0, 0 };
            config.MaxBullets = 4;
            BattleContent content =
                Content(Weapon(baseDamage: 1, interval: 100, speed: 0));
            BattleSim first = CreateSim(
                config,
                Gauge(0, 0, PowerUpGauge.MaximumOptionCount, 0),
                EmptyPlan(),
                content,
                8400UL);
            BattleSim second = CreateSim(
                config,
                Gauge(0, 0, PowerUpGauge.MaximumOptionCount, 0),
                EmptyPlan(),
                content,
                8400UL);

            Step(first, 0, 0, true);
            Step(second, 0, 0, true);

            AssertOption(first.Options[4], 5, 50, 0);
            AssertOption(first.Options[5], 6, 60, 0);
            Assert.AreEqual(4, first.Bullets.Count);
            Assert.AreEqual(4, second.Bullets.Count);
            for (int i = 0; i < first.Bullets.Count; i++)
            {
                int expectedX = i == 0 ? 0 : i * 10;
                AssertBullet(
                    first.Bullets[i],
                    BulletKind.MainShot,
                    expectedX,
                    0);
                AssertBullet(
                    second.Bullets[i],
                    BulletKind.MainShot,
                    expectedX,
                    0);
                Assert.AreEqual(first.Bullets[i].Id, second.Bullets[i].Id);
            }
        }

        [Test]
        public void OptionMissileVolleyUsesRemainingBudgetInEmitterOrder()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 0;
            config.PlayerBulletSpeedPerTick = 0;
            config.MissileSpeedXNumerator = 0;
            config.MissileFallSpeedYNumerator = 0;
            config.OptionFormation = OptionFormation.Fixed;
            config.OptionFixedOffsetXs = new[] { 10, 20, 30, 40, 50, 60 };
            config.OptionFixedOffsetYs = new[] { 0, 0, 0, 0, 0, 0 };
            config.OptionMissileDamagePercent = 37;
            config.MaxBullets = 10;
            BattleSim sim = CreateSim(
                config,
                Gauge(0, 1, PowerUpGauge.MaximumOptionCount, 0),
                EmptyPlan(),
                Content(Weapon(baseDamage: 0, interval: 100, speed: 0)),
                0x8501UL);

            Step(sim, 0, 0, true);

            Assert.AreEqual(10, sim.Bullets.Count);
            Assert.AreEqual(3, CountBullets(sim.Bullets, BulletKind.Missile));
            for (int i = 0; i < 7; i++)
            {
                AssertBullet(
                    sim.Bullets[i],
                    BulletKind.MainShot,
                    i == 0 ? 0 : i * 10,
                    0);
            }
            for (int i = 0; i < 3; i++)
            {
                AssertBullet(
                    sim.Bullets[7 + i],
                    BulletKind.Missile,
                    i * 10,
                    0);
            }
        }

        [Test]
        public void SixOptionsAtHighestFireRateFillBudgetWithoutSkippingVolley()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 0;
            config.PlayerBulletSpeedPerTick = 0;
            config.OptionFormation = OptionFormation.Fixed;
            config.OptionFixedOffsetXs = new[] { 10, 20, 30, 40, 50, 60 };
            config.OptionFixedOffsetYs = new[] { 0, 0, 0, 0, 0, 0 };
            config.MaxBullets = 64;
            config.MainShotRapidFireStartLevel = 2;
            config.MainShotFireIntervalReductionPerLevel = 1;
            config.MainShotMinimumFireIntervalTicks = 4;
            var gauge = new PowerUpGauge(new[] { 6, 3, 6, 3 });
            gauge.ImportLevels(new[] { 6, 0, 6, 0 });
            BattleSim sim = CreateSim(
                config,
                gauge,
                EmptyPlan(),
                Content(Weapon(baseDamage: 1, interval: 8, speed: 0)),
                0x100UL);
            var fire = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 40; tick++)
                sim.Step(in fire);

            Assert.AreEqual(64, sim.Bullets.Count);
            Assert.AreEqual(64L, sim.Statistics.ShotsFired);
            for (int emitter = 0; emitter <= PowerUpGauge.MaximumOptionCount; emitter++)
            {
                int emitterX = emitter * 10;
                Assert.AreEqual(
                    emitter == 0 ? 10 : 9,
                    CountBulletsAtX(sim.Bullets, BulletKind.MainShot, emitterX),
                    $"emitter {emitter} must follow body-first budget order");
            }
        }

        [Test]
        public void OptionMissileDamagePercentScalesCollisionDamage()
        {
            EnemyDefinition bodyTarget = Enemy("body_target", hp: 30);
            EnemyDefinition optionTarget = Enemy("option_target", hp: 30);
            StagePlan plan = Plan(Segment(
                "targets",
                20,
                new SpawnEvent(1, bodyTarget.Id, 500, 0),
                new SpawnEvent(1, optionTarget.Id, 1500, 0)));
            BattleSimConfig config = CreateConfig();
            config.MissileBaseDamage = 10;
            config.MissileDamageGrowthPercentPerLevel = 0;
            config.MissileSpeedXNumerator = 500;
            config.MissileSpeedXDenominator = 1;
            config.MissileFallSpeedYNumerator = 0;
            config.OptionFormation = OptionFormation.Fixed;
            config.OptionFixedOffsetXs = new[] { 1000, 0, 0, 0, 0, 0 };
            config.OptionFixedOffsetYs = new[] { 0, 0, 0, 0, 0, 0 };
            config.OptionMissileDamagePercent = 50;
            BattleSim sim = CreateSim(
                config,
                Gauge(0, 1, 1, 0),
                plan,
                Content(
                    Weapon(baseDamage: 0, interval: 100, speed: 500),
                    bodyTarget,
                    optionTarget),
                0x8502UL);

            Step(sim, 0, 0, true);
            Step(sim, 0, 0, false);

            Assert.AreEqual(2, sim.Enemies.Count);
            Assert.AreEqual(20, sim.Enemies[0].Hp,
                "the body missile deals the full configured damage");
            Assert.AreEqual(25, sim.Enemies[1].Hp,
                "the option missile deals the configured percentage");
        }

        [Test]
        public void ShieldStock_ConsumesOnePerHitAndShieldUpgradeRestoresOne()
        {
            EnemyDefinition heavy = Enemy("heavy", contactDamage: 3);
            EnemyDefinition light = Enemy("light", contactDamage: 1);
            EnemyDefinition afterUpgrade = Enemy("after_upgrade", contactDamage: 2);
            StagePlan plan = Plan(Segment(
                "contacts",
                10,
                new SpawnEvent(1, heavy.Id, 0, 0),
                new SpawnEvent(2, light.Id, 0, 0),
                new SpawnEvent(4, afterUpgrade.Id, 0, 0)));
            var gauge = Gauge(0, 0, 0, 2);
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 2;
            config.PlayerHitInvulnerabilityTicks = 0;
            var sim = CreateSim(
                config,
                gauge,
                plan,
                Content(Weapon(), heavy, light, afterUpgrade),
                5UL);
            InputCommand none = InputCommand.None;

            Assert.AreEqual(2, sim.ShieldRemaining);
            sim.Step(in none);
            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(1, sim.ShieldRemaining);

            sim.Step(in none);
            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(0, sim.ShieldRemaining);

            gauge.ImportLevels(new[] { 0, 0, 0, 3 });
            sim.Step(in none);
            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(1, sim.ShieldRemaining);

            sim.Step(in none);
            Assert.AreEqual(1, sim.PlayerHp);
            Assert.AreEqual(0, sim.ShieldRemaining);
        }

        [Test]
        public void ShieldStockRecovery_ClampsAtConfiguredCap()
        {
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 4;
            config.MaxShieldStock = 5;
            var sim = CreateSim(
                config,
                Gauge(0, 0, 0, 0),
                Plan(Segment("idle", 10)),
                Content(Weapon()),
                6UL);

            Assert.AreEqual(1, sim.RecoverShieldStock(99));
            Assert.AreEqual(5, sim.ShieldStock);
            Assert.AreEqual(0, sim.RecoverShieldStock(1));
            Assert.AreEqual(5, sim.ShieldStock);
        }

        [Test]
        public void ShieldHitInvulnerability_BlocksHitsUntilConfiguredTick()
        {
            EnemyDefinition first = Enemy("first", contactDamage: 9);
            EnemyDefinition blocked = Enemy("blocked", contactDamage: 9);
            EnemyDefinition afterWindow = Enemy(
                "after_window",
                contactDamage: 9);
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 2;
            config.PlayerHitInvulnerabilityTicks = 2;
            var sim = CreateSim(
                config,
                Gauge(0, 0, 0, 0),
                Plan(Segment(
                    "contacts",
                    10,
                    new SpawnEvent(1, first.Id, 0, 0),
                    new SpawnEvent(2, blocked.Id, 0, 0),
                    new SpawnEvent(3, afterWindow.Id, 0, 0))),
                Content(Weapon(), first, blocked, afterWindow),
                7UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            Assert.AreEqual(1, sim.ShieldStock);
            Assert.AreEqual(2, sim.PlayerInvulnerabilityTicksRemaining);

            sim.Step(in none);
            Assert.AreEqual(1, sim.ShieldStock);
            Assert.AreEqual(1, sim.PlayerInvulnerabilityTicksRemaining);

            sim.Step(in none);
            Assert.AreEqual(0, sim.ShieldStock);
            Assert.AreEqual(1, sim.PlayerHp);
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
            config.OptionFollowDelayTicks = 3;
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
            Assert.Less(first.ShieldRemaining, 5);
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

        static int CountBulletsAtX(
            IReadOnlyList<BulletState> bullets,
            BulletKind kind,
            int x)
        {
            int count = 0;
            for (int i = 0; i < bullets.Count; i++)
                if (bullets[i].Kind == kind && bullets[i].X == x)
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

        static void Step(BattleSim sim, int moveX, int moveY, bool fire)
        {
            var input = new InputCommand(moveX, moveY, fire);
            sim.Step(in input);
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
                Assert.AreEqual(
                    left.DamagePercent,
                    right.DamagePercent,
                    $"tick {tick}, bullet {i}");
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
