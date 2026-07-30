using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class PrimaryWeaponFamilyTests
    {
        [Test]
        public void DefaultProfilesPreserveFamilyConceptRelationships()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();

            Assert.Greater(
                (long)config.LaserSpeedNumerator
                    * config.PlayerBulletSpeedDenominator,
                (long)config.PlayerBulletSpeedNumerator
                    * config.LaserSpeedDenominator);
            Assert.Greater(
                config.LaserFireIntervalTicks,
                config.FireIntervalTicks);
            Assert.Greater(
                config.LaserBaseDamage,
                config.MainShotBaseDamage);
            Assert.Less(
                config.LaserHalfHeight,
                config.MainShotHalfHeight);
            Assert.AreEqual(2, config.LaserPierceEnemyCount);
            Assert.AreEqual(3, config.SpreadWays);
            Assert.Less(
                config.SpreadBaseDamage,
                config.MainShotBaseDamage);
        }

        [Test]
        public void SelectedShipOverridesStartingHpAndSelectsWeaponProfile()
        {
            BattleSimConfig laserConfig = CreateConfig();
            laserConfig.PlayerMaxHp = 7;
            laserConfig.LaserSpeedNumerator = 80;
            laserConfig.LaserSpeedDenominator = 1;
            ShipDefinition laser = CreateShip(
                "interceptor",
                WeaponType.Laser,
                2);

            RunManager laserRun = CreateRun(
                11UL,
                laser,
                laserConfig,
                Array.Empty<SpawnEvent>());
            InputCommand fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            laserRun.Step(in fire);
            laserRun.Step(in none);

            Assert.AreEqual(2, laserRun.Battle.ShieldStock);
            Assert.AreEqual(
                WeaponType.Laser,
                laserRun.Battle.PlayerWeaponType);
            Assert.AreEqual(80, laserRun.Battle.Bullets[0].X);

            BattleSimConfig fallbackConfig = CreateConfig();
            fallbackConfig.PlayerMaxHp = 7;
            RunManager fallback = CreateRun(
                12UL,
                ShipDefinition.CreateDefault(),
                fallbackConfig,
                Array.Empty<SpawnEvent>());

            Assert.AreEqual(3, fallback.Battle.ShieldStock);
            Assert.AreEqual(
                WeaponType.Vulcan,
                fallback.Battle.PlayerWeaponType);
        }

        [Test]
        public void SpreadFiresDeterministicThreeWayAndCountsProjectiles()
        {
            BattleSim sim = CreateBattle(
                WeaponType.Spread,
                6,
                Array.Empty<SpawnEvent>(),
                PowerUpGauge.CreateDefault(),
                BattleModifier.None);
            InputCommand fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);

            Assert.AreEqual(3, sim.Bullets.Count);
            Assert.AreEqual(3L, sim.Statistics.ShotsFired);
            Assert.AreEqual(0, sim.Bullets[0].Y);
            Assert.AreEqual(0, sim.Bullets[1].Y);
            Assert.AreEqual(0, sim.Bullets[2].Y);

            sim.Step(in none);

            Assert.Less(sim.Bullets[0].Y, 0);
            Assert.AreEqual(0, sim.Bullets[1].Y);
            Assert.Greater(sim.Bullets[2].Y, 0);
            Assert.AreEqual(
                -sim.Bullets[0].Y,
                sim.Bullets[2].Y);
            Assert.AreEqual(
                sim.Bullets[0].X,
                sim.Bullets[2].X);
        }

        [Test]
        public void LaserPierceAndPierceModifierStackAdditively()
        {
            SpawnEvent[] line = CreateEnemyLine(4);
            BattleSim baseLaser = CreateBattle(
                WeaponType.Laser,
                20,
                line,
                PowerUpGauge.CreateDefault(),
                BattleModifier.None);
            BattleSim modifiedLaser = CreateBattle(
                WeaponType.Laser,
                20,
                line,
                PowerUpGauge.CreateDefault(),
                BattleModifier.PierceShot);
            InputCommand fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            baseLaser.Step(in fire);
            modifiedLaser.Step(in fire);
            for (int i = 0; i < 4; i++)
            {
                baseLaser.Step(in none);
                modifiedLaser.Step(in none);
            }

            Assert.AreEqual(3L, baseLaser.Statistics.Kills);
            Assert.AreEqual(1, baseLaser.Enemies.Count);
            Assert.AreEqual(4L, modifiedLaser.Statistics.Kills);
            Assert.AreEqual(0, modifiedLaser.Enemies.Count);
        }

        [TestCase(WeaponType.Vulcan, 10, 80)]
        [TestCase(WeaponType.Laser, 20, 60)]
        [TestCase(WeaponType.Spread, 6, 88)]
        public void MainPowerUpLevelScalesEachFamilyDamage(
            WeaponType weaponType,
            int baseDamage,
            int expectedHp)
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 0, 0, 0 });
            BattleSim sim = CreateBattle(
                weaponType,
                baseDamage,
                new[] { new SpawnEvent(0, "target", 50, 0) },
                gauge,
                BattleModifier.None,
                enemyHp: 100);
            InputCommand fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);

            Assert.AreEqual(1, sim.Enemies.Count);
            Assert.AreEqual(expectedHp, sim.Enemies[0].Hp);
        }

        [TestCase(WeaponType.Vulcan, 3)]
        [TestCase(WeaponType.Laser, 2)]
        [TestCase(WeaponType.Spread, 9)]
        public void MainPowerUpLevelUsesEachFamilyFireRateProfile(
            WeaponType weaponType,
            long expectedProjectiles)
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 0, 0, 0 });
            BattleSim sim = CreateBattle(
                weaponType,
                10,
                Array.Empty<SpawnEvent>(),
                gauge,
                BattleModifier.None,
                fireIntervalTicks: 8);
            var fire = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 20; tick++)
                sim.Step(in fire);

            Assert.AreEqual(
                expectedProjectiles,
                sim.Statistics.ShotsFired);
        }

        [Test]
        public void ReplayShipIdReconstructsSameWeaponTrajectory()
        {
            ShipDefinition ship = CreateShip(
                "bulwark",
                WeaponType.Spread,
                5);
            BattleSimConfig config = CreateConfig();
            RunManager recorded = CreateRun(
                0xB017UL,
                ship,
                config,
                Array.Empty<SpawnEvent>());
            var recorder = new InputRecorder(recorded);
            var recordedHasher = new DeterminismAuditHasher();

            for (int tick = 0; tick < 40; tick++)
            {
                var input = new InputCommand(
                    tick % 3 - 1,
                    tick % 5 == 0 ? 1 : 0,
                    tick % 4 != 0);
                recorder.Record(in input);
                recorded.Step(in input);
                recordedHasher.FoldRunState(recorded);
            }

            InputRecordingData data = recorder.Export();
            var playback = new InputPlayback(data);
            string replayShipId = ship.Id;
            ShipDefinition replayShip = FindShip(
                new[] { ShipDefinition.CreateDefault(), ship },
                replayShipId);
            Assert.IsNotNull(replayShip);
            RunManager replayed = CreateRun(
                0xB017UL,
                replayShip,
                CreateConfig(),
                Array.Empty<SpawnEvent>());
            var replayedHasher = new DeterminismAuditHasher();
            foreach (InputCommand input in playback)
            {
                replayed.Step(in input);
                replayedHasher.FoldRunState(replayed);
            }

            Assert.AreEqual(
                recordedHasher.Hash,
                replayedHasher.Hash);
        }

        [Test]
        public void SuspendShipIdRebuildsWeaponFamilyAndStartingHp()
        {
            ShipDefinition ship = CreateShip(
                "bulwark",
                WeaponType.Spread,
                5);
            BattleSimConfig config = CreateConfig();
            config.PlayerMaxHp = 9;
            RunManager source = CreateRun(
                0x5A5EUL,
                ship,
                config,
                Array.Empty<SpawnEvent>());
            RunSuspendData data = source.ExportSuspendData();

            RunManager resumed = RunManager.ResumeFromSuspendData(
                data,
                new FixedStageGenerator(CreatePlan(
                    Array.Empty<SpawnEvent>())),
                CreateConfig(),
                CreateContent(6),
                PowerUpGauge.CreateDefault(),
                null,
                ship);

            Assert.AreEqual(ship.Id, data.shipId);
            Assert.AreEqual(3, resumed.Battle.ShieldStock);
            Assert.AreEqual(
                WeaponType.Spread,
                resumed.Battle.PlayerWeaponType);

            InputCommand fire = new InputCommand(0, 0, true);
            source.Step(in fire);
            resumed.Step(in fire);
            var expected = new DeterminismAuditHasher();
            var actual = new DeterminismAuditHasher();
            expected.FoldRunState(source);
            actual.FoldRunState(resumed);
            Assert.AreEqual(expected.Hash, actual.Hash);
        }

        static ShipDefinition CreateShip(
            string id,
            WeaponType weaponType,
            int maxHp)
        {
            return new ShipDefinition(
                id,
                id,
                1,
                1,
                new[] { 0, 0, 0, 0 },
                0,
                weaponType,
                maxHp);
        }

        static ShipDefinition FindShip(
            ShipDefinition[] ships,
            string id)
        {
            for (int i = 0; i < ships.Length; i++)
            {
                if (string.Equals(
                        ships[i].Id,
                        id,
                        StringComparison.Ordinal))
                    return ships[i];
            }
            return null;
        }

        static RunManager CreateRun(
            ulong seed,
            ShipDefinition ship,
            BattleSimConfig config,
            SpawnEvent[] spawns)
        {
            return new RunManager(
                seed,
                new FixedStageGenerator(CreatePlan(spawns)),
                config,
                CreateContent(10),
                PowerUpGauge.CreateDefault(),
                null,
                ship);
        }

        static BattleSim CreateBattle(
            WeaponType weaponType,
            int baseDamage,
            SpawnEvent[] spawns,
            PowerUpGauge gauge,
            BattleModifier modifier,
            int enemyHp = 1,
            int fireIntervalTicks = 20)
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerWeaponType = weaponType;
            config.LaserPierceEnemyCount = 2;
            config.PierceShotEnemyCount = 1;
            EnemyDefinition target = CreateEnemy(enemyHp);
            var weapon = new WeaponDefinition(
                "main",
                baseDamage,
                fireIntervalTicks,
                50,
                1,
                0,
                0);
            var content = new BattleContent(
                new[] { target },
                new[] { weapon },
                weapon.Id);
            return new BattleSim(
                config,
                new Rng(99UL),
                CreatePlan(spawns),
                content,
                gauge,
                modifier);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 2,
                PlayerBulletSpeedPerTick = 10,
                MainShotBaseDamage = 10,
                FireIntervalTicks = 8,
                MainShotHalfWidth = 0,
                MainShotHalfHeight = 0,
                MaxBullets = 64,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 7,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 1,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                LaserBaseDamage = 20,
                LaserFireIntervalTicks = 16,
                LaserRapidFireStartLevel = 2,
                LaserFireIntervalReductionPerLevel = 2,
                LaserMinimumFireIntervalTicks = 8,
                LaserSpeedNumerator = 50,
                LaserSpeedDenominator = 1,
                LaserHalfWidth = 0,
                LaserHalfHeight = 0,
                LaserPierceEnemyCount = 2,
                SpreadBaseDamage = 6,
                SpreadFireIntervalTicks = 10,
                SpreadRapidFireStartLevel = 3,
                SpreadFireIntervalReductionPerLevel = 1,
                SpreadMinimumFireIntervalTicks = 6,
                SpreadSpeedNumerator = 50,
                SpreadSpeedDenominator = 1,
                SpreadHalfWidth = 0,
                SpreadHalfHeight = 0,
                SpreadWays = 3,
                SpreadStepLutSlots = 2,
                MaxEnemyBullets = 0
            };
        }

        static BattleContent CreateContent(int baseDamage)
        {
            EnemyDefinition target = CreateEnemy(100);
            var weapon = new WeaponDefinition(
                "main",
                baseDamage,
                8,
                10,
                1,
                0,
                0);
            return new BattleContent(
                new[] { target },
                new[] { weapon },
                weapon.Id);
        }

        static EnemyDefinition CreateEnemy(int hp)
        {
            return new EnemyDefinition(
                "target",
                hp,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
        }

        static SpawnEvent[] CreateEnemyLine(int count)
        {
            var result = new SpawnEvent[count];
            for (int i = 0; i < count; i++)
                result[i] = new SpawnEvent(
                    0,
                    "target",
                    50 * (i + 1),
                    0);
            return result;
        }

        static StagePlan CreatePlan(SpawnEvent[] spawns)
        {
            return new StagePlan(
                new[]
                {
                    new StageSegment(
                        "weapon_test",
                        1000,
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

        sealed class FixedStageGenerator : IStageGenerator
        {
            readonly StagePlan _plan;

            public FixedStageGenerator(StagePlan plan)
            {
                _plan = plan;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return _plan;
            }
        }
    }
}
