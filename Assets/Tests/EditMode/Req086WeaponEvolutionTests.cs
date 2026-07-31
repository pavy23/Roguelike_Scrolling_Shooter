using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class Req086WeaponEvolutionTests
    {
        [Test]
        public void WeaponModeReactivationEvolvesToThreeAndViewExposesLevel()
        {
            PowerUpGauge gauge = CreateEvolutionGauge();

            Activate(gauge, PowerUpSlot.Double);
            Activate(gauge, PowerUpSlot.Double);
            Activate(gauge, PowerUpSlot.Double);

            Assert.AreEqual(3, gauge.GetLevel(PowerUpSlot.Double));
            Assert.AreEqual(3, gauge.GetMaxLevel(PowerUpSlot.Double));
            PowerUpGaugeSlotView view = FindView(
                gauge,
                PowerUpSlot.Double);
            Assert.AreEqual(3, view.Level);
            Assert.AreEqual(3, view.MaxLevel);
            Assert.IsTrue(view.IsActiveWeaponMode);

            Activate(gauge, PowerUpSlot.Laser);
            Assert.AreEqual(0, gauge.GetLevel(PowerUpSlot.Double));
            Assert.AreEqual(1, gauge.GetLevel(PowerUpSlot.Laser));
        }

        [Test]
        public void DoubleLevelThreeUsesCrossAnglesAndStaggeredBurst()
        {
            PowerUpGauge gauge = CreateEvolutionGauge();
            Activate(gauge, PowerUpSlot.Double);
            Activate(gauge, PowerUpSlot.Double);
            Activate(gauge, PowerUpSlot.Double);
            BattleSim sim = CreateBattle(
                gauge,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());

            InputCommand fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            Assert.AreEqual(3, sim.PrimaryWeaponEvolutionLevel);
            Assert.AreEqual(4, sim.Bullets.Count);
            Assert.AreEqual(1, sim.BurstShotsRemaining);

            sim.Step(InputCommand.None);
            Assert.AreEqual(8, sim.Bullets.Count);
            Assert.AreEqual(0, sim.BurstShotsRemaining);
            sim.Step(InputCommand.None);
            bool foundRearShot = false;
            for (int i = 0; i < sim.Bullets.Count; i++)
                foundRearShot |= sim.Bullets[i].X < 0;
            Assert.IsTrue(foundRearShot);
        }

        [Test]
        public void LaserLevelThreeCreatesImmediateGrowingPlayerBeam()
        {
            PowerUpGauge gauge = CreateEvolutionGauge();
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            EnemyDefinition target = Enemy("beam_target", 5);
            BattleSim sim = CreateBattle(
                gauge,
                new[] { target },
                new[] { new SpawnEvent(0, target.Id, 100, 0) });
            InputCommand fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            Assert.AreEqual(0, sim.Bullets.Count);
            Assert.AreEqual(1, sim.Lasers.Count);
            Assert.AreEqual(
                LaserSourceKind.Player,
                sim.Lasers[0].SourceKind);
            Assert.AreEqual(1, sim.Lasers[0].HalfWidth);
            Assert.AreEqual(3, sim.Enemies[0].Hp);

            sim.Step(in fire);
            Assert.AreEqual(3, sim.Lasers[0].HalfWidth);
            Assert.AreEqual(1, sim.Enemies[0].Hp);
            sim.Step(in fire);
            Assert.AreEqual(0, sim.Enemies.Count);

            sim.Step(InputCommand.None);
            Assert.AreEqual(0, sim.Lasers.Count);
        }

        [Test]
        public void LaserLevelThreeDamagesBossWithSamePlayerBeam()
        {
            PowerUpGauge gauge = CreateEvolutionGauge();
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            var weapon = new WeaponDefinition(
                "shot", 1, 10, 20, 1, 0, 0);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id,
                CreateFamilies());
            var plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "beam_boss", 5,
                        Array.Empty<SpawnEvent>(),
                        1, 1, new[] { 1 })
                },
                "boss", 1, 1, 1,
                10, 10, 10, 100,
                new[] { new BossPhase(999, 1, 1, 1) });
            var sim = new BattleSim(
                CreateConfig(),
                new Rng(0x8603UL),
                plan,
                content,
                gauge,
                BattleModifier.None);
            for (int i = 0; i < 1000 &&
                (!sim.BossActive || sim.BossEntering); i++)
                sim.Step(InputCommand.None);
            Assert.IsTrue(sim.BossActive);
            Assert.IsFalse(sim.BossEntering);
            int hpBefore = sim.Boss.Hp;

            InputCommand fire = new InputCommand(0, 0, true);
            sim.Step(in fire);

            Assert.AreEqual(hpBefore - 2, sim.Boss.Hp);
        }

        [Test]
        public void TriplePulseAndInertiaAreIntegerTickDeterministic()
        {
            PowerUpGauge earlyGauge = CreateEvolutionGauge();
            Activate(earlyGauge, PowerUpSlot.Triple);
            Activate(earlyGauge, PowerUpSlot.Triple);
            BattleSim early = CreateBattle(
                earlyGauge,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            InputCommand fire = new InputCommand(0, 0, true);
            early.Step(in fire);
            early.Step(InputCommand.None);

            PowerUpGauge wideGauge = CreateEvolutionGauge();
            Activate(wideGauge, PowerUpSlot.Triple);
            Activate(wideGauge, PowerUpSlot.Triple);
            BattleSim wide = CreateBattle(
                wideGauge,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            for (int i = 0; i < 5; i++)
                wide.Step(InputCommand.None);
            wide.Step(in fire);
            wide.Step(InputCommand.None);
            Assert.Greater(
                Math.Abs(wide.Bullets[0].Y),
                Math.Abs(early.Bullets[0].Y));

            PowerUpGauge inertiaGauge = CreateEvolutionGauge();
            Activate(inertiaGauge, PowerUpSlot.Triple);
            Activate(inertiaGauge, PowerUpSlot.Triple);
            Activate(inertiaGauge, PowerUpSlot.Triple);
            BattleSim inertia = CreateBattle(
                inertiaGauge,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            InputCommand movingFire = InputCommand.Analog(
                10,
                0,
                true);
            inertia.Step(in movingFire);
            inertia.Step(InputCommand.None);
            Assert.Greater(
                inertia.Bullets[2].X - inertia.PlayerX,
                early.Bullets[2].X - early.PlayerX);
        }

        [Test]
        public void LaserLevelTwoExplodesWhenItsPierceBudgetIsExhausted()
        {
            PowerUpGauge gauge = CreateEvolutionGauge();
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            EnemyDefinition target = Enemy("lance_target", 1);
            var spawns = new[]
            {
                new SpawnEvent(0, target.Id, 20, 0),
                new SpawnEvent(0, target.Id, 40, 0),
                new SpawnEvent(0, target.Id, 60, 0),
                new SpawnEvent(0, target.Id, 80, 0),
                new SpawnEvent(0, target.Id, 100, 0),
                new SpawnEvent(0, target.Id, 105, 0)
            };
            BattleSim sim = CreateBattle(
                gauge,
                new[] { target },
                spawns);
            InputCommand fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            for (int i = 0; i < 5; i++)
                sim.Step(InputCommand.None);

            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(6L, sim.Statistics.Kills);
        }

        [Test]
        public void OptionalLevelAxesParseWithoutWeaponsSchemaBump()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            string weapons = File.ReadAllText(
                Path.Combine(gameData, "weapons.json"))
                .Replace("\r\n", "\n");
            weapons = weapons.Replace(
                @"""spreadStepLutSlots"": 4,
      ""shotAngleLutSlots"": []",
                @"""spreadStepLutSlots"": 4,
      ""shotAngleLutSlots"": [],
      ""levels"": [
        {
          ""level"": 2,
          ""spreadWays"": 5,
          ""shotAngleLutSlots"": [],
          ""pulseMinStepLutSlots"": 1,
          ""pulseMaxStepLutSlots"": 5,
          ""pulsePeriodTicks"": 12
        },
        {
          ""level"": 3,
          ""spreadWays"": 5,
          ""shotAngleLutSlots"": [],
          ""burstCount"": 2,
          ""burstIntervalTicks"": 1,
          ""inertiaVelocityPercent"": 50,
          ""minimumFireIntervalTicks"": 4
        }
      ]");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                weapons,
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")));
            PrimaryWeaponFamilyDefinition spread =
                data.BattleContent.FindPrimaryWeaponFamily(
                    PrimaryWeaponFamily.Spread);

            Assert.AreEqual(7, GameDataParser.SupportedReq080WeaponsSchemaVersion);
            Assert.AreEqual(3, spread.Levels.Count);
            Assert.AreEqual(5, spread.GetLevel(2).SpreadWays);
            Assert.AreEqual(12, spread.GetLevel(2).PulsePeriodTicks);
            Assert.AreEqual(50, spread.GetLevel(3).InertiaVelocityPercent);
            Assert.AreEqual(2, spread.GetLevel(3).BurstCount);
            Assert.AreEqual(4, spread.GetLevel(3).MinimumFireIntervalTicks);
        }

        static BattleSim CreateBattle(
            PowerUpGauge gauge,
            EnemyDefinition[] enemies,
            SpawnEvent[] spawns)
        {
            BattleSimConfig config = CreateConfig();
            var weapon = new WeaponDefinition(
                "shot",
                1,
                10,
                20,
                1,
                0,
                0);
            var content = new BattleContent(
                enemies,
                new[] { weapon },
                weapon.Id,
                CreateFamilies());
            var segment = new StageSegment(
                "req086",
                100,
                spawns,
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                "boss",
                1,
                1,
                1);
            return new BattleSim(
                config,
                new Rng(0x8602UL),
                plan,
                content,
                gauge,
                BattleModifier.None);
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerSpeedPerTick = 10;
            config.PlayerBulletSpeedPerTick = 20;
            config.PlayerMinX = -1000;
            config.PlayerMaxX = 1000;
            config.PlayerMinY = -1000;
            config.PlayerMaxY = 1000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.BulletDespawnX = 10000;
            config.EnemyDespawnX = -10000;
            config.MaxBullets = 64;
            config.MaxEnemyBullets = 0;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.CapsuleHalfWidth = 0;
            config.CapsuleHalfHeight = 0;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            return config;
        }

        static PrimaryWeaponFamilyDefinition[] CreateFamilies()
        {
            int[] doubleBaseAngles = { 0, 5 };
            var doubleLevels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 5, 0, 2, 5, doubleBaseAngles),
                new PrimaryWeaponLevelDefinition(
                    2, 5, 0, 3, 5, new[] { 0, 8, 32 }),
                new PrimaryWeaponLevelDefinition(
                    3, 5, 0, 4, 5, new[] { 0, 0, 8, 32 },
                    burstCount: 2,
                    burstIntervalTicks: 1)
            };
            int[] laserAngles = { 0 };
            var laserLevels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 8, 2, 1, 0, laserAngles),
                new PrimaryWeaponLevelDefinition(
                    2, 8, 4, 1, 0, laserAngles,
                    impactExplosionDamage: 2,
                    impactExplosionRadius: 64),
                new PrimaryWeaponLevelDefinition(
                    3, 8, 4, 1, 0, laserAngles,
                    beamDamagePerTick: 2,
                    beamLength: 500,
                    beamStartHalfWidth: 1,
                    beamGrowthPerTick: 2,
                    beamMaxHalfWidth: 5)
            };
            var spreadLevels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 5, 0, 3, 4, Array.Empty<int>()),
                new PrimaryWeaponLevelDefinition(
                    2, 5, 0, 5, 4, Array.Empty<int>(),
                    pulseMinStepLutSlots: 1,
                    pulseMaxStepLutSlots: 5,
                    pulsePeriodTicks: 12),
                new PrimaryWeaponLevelDefinition(
                    3, 4, 0, 5, 4, Array.Empty<int>(),
                    pulseMinStepLutSlots: 1,
                    pulseMaxStepLutSlots: 5,
                    pulsePeriodTicks: 12,
                    inertiaVelocityPercent: 50)
            };
            return new[]
            {
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Double,
                    "Double",
                    "Double evolution.",
                    WeaponType.Spread,
                    1, 10, 5, 3, 1,
                    20, 1, 0, 0, 0, 2, 5,
                    doubleBaseAngles,
                    doubleLevels),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Laser,
                    "Laser",
                    "Laser evolution.",
                    WeaponType.Laser,
                    1, 10, 8, 2, 1,
                    20, 1, 0, 0, 2, 1, 0,
                    laserAngles,
                    laserLevels),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Spread,
                    "Triple",
                    "Triple evolution.",
                    WeaponType.Spread,
                    1, 10, 5, 3, 1,
                    20, 1, 0, 0, 0, 3, 4,
                    Array.Empty<int>(),
                    spreadLevels)
            };
        }

        static EnemyDefinition Enemy(string id, int hp)
        {
            return new EnemyDefinition(
                id,
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

        static PowerUpGauge CreateEvolutionGauge()
        {
            return new PowerUpGauge(new[]
            {
                5, 3, 6, 3, 6, 3, 3, 3
            });
        }

        static void Activate(PowerUpGauge gauge, PowerUpSlot slot)
        {
            int gaugeIndex = -1;
            for (int i = 0; i < gauge.GaugeSlots.Count; i++)
                if (gauge.GaugeSlots[i].Slot == slot)
                {
                    gaugeIndex = i;
                    break;
                }
            for (int i = 0; i <= gaugeIndex; i++)
                gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
        }

        static PowerUpGaugeSlotView FindView(
            PowerUpGauge gauge,
            PowerUpSlot slot)
        {
            for (int i = 0; i < gauge.GaugeSlotCount; i++)
                if (gauge.GetGaugeSlotView(i).Slot == slot)
                    return gauge.GetGaugeSlotView(i);
            throw new InvalidOperationException();
        }

        static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(
                    Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }
    }
}
