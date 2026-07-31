using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class WeaponExpansionTests
    {
        [Test]
        public void MissileFamiliesUseDistinctIntegerTrajectories()
        {
            BulletState straight = FireMissile(
                MissileFamily.Straight,
                100,
                0,
                BattleModifier.None);
            BulletState bomb = FireMissile(
                MissileFamily.SpreadBomb,
                60,
                90,
                BattleModifier.None);
            BulletState lance = FireMissile(
                MissileFamily.PiercingLance,
                160,
                0,
                BattleModifier.None);

            Assert.AreEqual(100, straight.X);
            Assert.AreEqual(0, straight.Y);
            Assert.AreEqual(60, bomb.X);
            Assert.AreEqual(-90, bomb.Y);
            Assert.AreEqual(160, lance.X);
            Assert.AreEqual(0, lance.Y);
        }

        [Test]
        public void HomingModifierSteersTheCurrentlySelectedBombFamily()
        {
            BattleSimConfig plainConfig = Config();
            ConfigureMissile(
                plainConfig,
                MissileFamily.SpreadBomb,
                100,
                100);
            BattleSim plain = CreateSim(
                plainConfig,
                Gauge(missileLevel: 1),
                BattleModifier.None,
                new[] { Enemy("target", 100) },
                new[] { Spawn("target", 600, 600) });
            BattleSim homing = CreateSim(
                plainConfig,
                Gauge(missileLevel: 1),
                BattleModifier.HomingMissile,
                new[] { Enemy("target", 100) },
                new[] { Spawn("target", 600, 600) });

            FireAndAdvance(plain, 1);
            FireAndAdvance(homing, 1);

            BulletState plainMissile =
                FindBullet(plain, BulletKind.Missile);
            BulletState homingMissile =
                FindBullet(homing, BulletKind.Missile);
            Assert.AreEqual(MissileFamily.SpreadBomb, plainConfig.MissileFamily);
            Assert.Greater(
                homingMissile.Y,
                plainMissile.Y,
                "Homing must layer steering on the bomb profile instead of replacing it.");
        }

        [Test]
        public void PiercingLanceHitsThreeAlignedEnemiesOnlyOnceEach()
        {
            BattleSimConfig config = Config();
            ConfigureMissile(
                config,
                MissileFamily.PiercingLance,
                100,
                0);
            config.MissilePierceEnemyCount = 2;
            config.MissileBaseDamage = 1;
            BattleSim sim = CreateSim(
                config,
                Gauge(missileLevel: 1),
                BattleModifier.None,
                new[]
                {
                    Enemy("a", 1),
                    Enemy("b", 1),
                    Enemy("c", 1)
                },
                new[]
                {
                    Spawn("a", 100, 0),
                    Spawn("b", 200, 0),
                    Spawn("c", 300, 0)
                });

            FireAndAdvance(sim, 4);

            Assert.AreEqual(0, sim.Enemies.Count);
            Assert.AreEqual(3L, sim.Statistics.Kills);
            Assert.AreEqual(0, sim.Bullets.Count);
        }

        [Test]
        public void SpreadBombExplosionKillsNeverSeedKillExplosion()
        {
            BattleSimConfig config = Config();
            ConfigureMissile(
                config,
                MissileFamily.SpreadBomb,
                100,
                0);
            config.MissileBaseDamage = 0;
            config.MissileExplosionDamage = 1;
            config.MissileExplosionRadiusSubUnits = 100;
            config.MissileExplosionMaxTargets = 5;
            config.KillExplosionDamage = 99;
            config.KillExplosionRadiusSubUnits = 1000;
            BattleSim sim = CreateSim(
                config,
                Gauge(missileLevel: 1),
                BattleModifier.KillExplosion,
                new[]
                {
                    Enemy("impact", 100),
                    Enemy("splash_kill", 1),
                    Enemy("chain_canary", 50)
                },
                new[]
                {
                    Spawn("impact", 100, 0),
                    Spawn("splash_kill", 110, 0),
                    Spawn("chain_canary", 150, 0)
                });

            var fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            InputCommand none = InputCommand.None;
            sim.Step(in none);

            Assert.AreEqual(
                1,
                CountEvents(sim, SimEventType.MissileExploded));
            Assert.AreEqual(
                0,
                CountEvents(
                    sim,
                    SimEventType.KillExplosionTriggered),
                "AoE final hits must not seed kill_explosion.");
            Assert.AreEqual(2, sim.Enemies.Count);
            Assert.AreEqual("impact", sim.Enemies[0].DefinitionId);
            Assert.Greater(sim.Enemies[0].Hp, 0);
            Assert.AreEqual("chain_canary", sim.Enemies[1].DefinitionId);
            Assert.Greater(sim.Enemies[1].Hp, 0);
        }

        [Test]
        public void OptionFormationsProduceDeterministicPositions()
        {
            BattleSimConfig fixedConfig = Config();
            fixedConfig.OptionFormation = OptionFormation.Fixed;
            fixedConfig.OptionFixedOffsetXs =
                new[] { 192, 192, 192, 192 };
            fixedConfig.OptionFixedOffsetYs =
                new[] { 384, -384, 704, -704 };
            BattleSim fixedSim = CreateSim(
                fixedConfig,
                Gauge(optionLevel: 2),
                BattleModifier.None,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            Assert.AreEqual(192, fixedSim.Options[0].X);
            Assert.AreEqual(384, fixedSim.Options[0].Y);
            Assert.AreEqual(192, fixedSim.Options[1].X);
            Assert.AreEqual(-384, fixedSim.Options[1].Y);

            BattleSimConfig orbitConfig = Config();
            orbitConfig.OptionFormation = OptionFormation.Orbit;
            orbitConfig.OptionOrbitRadiusSubUnits = 448;
            orbitConfig.OptionOrbitAngularLutSlotsNumerator = 1;
            orbitConfig.OptionOrbitAngularLutSlotsDenominator = 2;
            BattleSim first = CreateSim(
                orbitConfig,
                Gauge(optionLevel: 2),
                BattleModifier.None,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            BattleSim second = CreateSim(
                orbitConfig,
                Gauge(optionLevel: 2),
                BattleModifier.None,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            Assert.AreEqual(448, first.Options[0].X);
            Assert.AreEqual(-448, first.Options[1].X);
            InputCommand none = InputCommand.None;
            for (int tick = 0; tick < 130; tick++)
            {
                first.Step(in none);
                second.Step(in none);
                AssertOptionPositionsEqual(first, second);
            }
        }

        [Test]
        public void OrbitFormationStepAllocatesNoManagedMemory()
        {
            BattleSimConfig config = Config();
            config.OptionFormation = OptionFormation.Orbit;
            BattleSim warmup = CreateSim(
                config,
                Gauge(optionLevel: 4),
                BattleModifier.None,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            InputCommand none = InputCommand.None;
            warmup.Step(in none);

            BattleSim measured = CreateSim(
                config,
                Gauge(optionLevel: 4),
                BattleModifier.None,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 100; tick++)
                measured.Step(in none);
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void SpreadBombExplosionScanAllocatesNoManagedMemory()
        {
            BattleSim warmup = CreateExplosionAllocationSim();
            FireAndAdvance(warmup, 1);

            BattleSim measured = CreateExplosionAllocationSim();
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            long before = GC.GetAllocatedBytesForCurrentThread();
            measured.Step(in fire);
            measured.Step(in none);
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void RewardCandidatesExcludeEquippedFamilyAndFormation()
        {
            RunManager run = CreateRewardRun();
            AdvanceToFirstBossReward(run);

            Assert.AreEqual(3, run.RewardOptions.Count);
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                RewardOption option = run.RewardOptions[i];
                Assert.IsFalse(
                    option.Type == RewardType.MissileFamily
                    && option.MissileFamily
                        == MissileFamily.Straight);
                Assert.IsFalse(
                    option.Type == RewardType.OptionFormation
                    && option.OptionFormation
                        == OptionFormation.Trail);
            }
        }

        [Test]
        public void SuspendAndRecordingPreserveLoadoutAndLegacyDefaults()
        {
            RunManager source = CreateRewardRun();
            RunSuspendData selected = source.ExportSuspendData();
            selected.missileFamily =
                (int)MissileFamily.SpreadBomb;
            selected.optionFormation =
                (int)OptionFormation.Orbit;
            Shmup.Core.SaveDataIntegrity.Seal(selected);
            RunManager resumed = ResumeRewardRun(selected);

            Assert.AreEqual(
                MissileFamily.SpreadBomb,
                resumed.CurrentMissileFamily);
            Assert.AreEqual(
                OptionFormation.Orbit,
                resumed.CurrentOptionFormation);

            var recorder = new InputRecorder(resumed);
            InputCommand none = InputCommand.None;
            recorder.Record(in none);
            InputRecordingData recording = recorder.Export();
            var playback = new InputPlayback(recording);
            Assert.AreEqual(
                MissileFamily.SpreadBomb,
                playback.MissileFamily);
            Assert.AreEqual(
                OptionFormation.Orbit,
                playback.OptionFormation);

            RunSuspendData legacySuspend =
                source.ExportSuspendData();
            legacySuspend.schemaVersion = 5;
            legacySuspend.checksum = null;
            RunSuspendData migratedSuspend =
                Shmup.Core.SaveDataIntegrity.MigrateAndValidate(
                    legacySuspend);
            Assert.AreEqual(
                (int)MissileFamily.Straight,
                migratedSuspend.missileFamily);
            Assert.AreEqual(
                (int)OptionFormation.Trail,
                migratedSuspend.optionFormation);

            recording.schemaVersion = 6;
            recording.checksum = null;
            InputRecordingData migratedRecording =
                Shmup.Core.SaveDataIntegrity.MigrateAndValidate(
                    recording);
            Assert.AreEqual(
                (int)MissileFamily.Straight,
                migratedRecording.missileFamily);
            Assert.AreEqual(
                (int)OptionFormation.Trail,
                migratedRecording.optionFormation);
        }

        [Test]
        public void WeaponsV3AndSwitchRewardsParseIntoRuntimeCatalogs()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                WeaponsV3Json,
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")),
                RewardsWithSwitchesJson);

            Assert.AreEqual(
                3,
                data.BattleContent.MissileFamilies.Count);
            Assert.AreEqual(
                3,
                data.BattleContent.OptionFormations.Count);
            MissileFamilyDefinition bomb =
                data.BattleContent.FindMissileFamily(
                    MissileFamily.SpreadBomb);
            Assert.AreEqual(12, bomb.BaseDamage);
            Assert.AreEqual(16, bomb.ExplosionDamage);
            Assert.AreEqual(448, bomb.ExplosionRadiusSubUnits);
            Assert.AreEqual(
                RewardType.MissileFamily,
                data.Rewards.All[0].Type);
            Assert.AreEqual(
                MissileFamily.SpreadBomb,
                data.Rewards.All[0].MissileFamily);
            Assert.AreEqual(
                OptionFormation.Orbit,
                data.Rewards.All[2].OptionFormation);
        }

        [Test]
        public void WeaponsV4PrimaryMetadataAndRewardV2ParseEndToEnd()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            string weapons = WeaponsV3Json.Replace(
                @"""schemaVersion"": 3",
                @"""schemaVersion"": 4");
            int closingBrace = weapons.LastIndexOf('}');
            weapons = weapons.Insert(
                closingBrace,
                @",
  ""primaryWeaponFamilies"": [
    { ""id"": ""double"", ""displayName"": ""Double"",
      ""description"": ""Two-way coverage shot."", ""weaponType"": ""spread"",
      ""baseDamage"": 6, ""fireIntervalTicks"": 10,
      ""minimumFireIntervalTicks"": 6, ""rapidFireStartLevel"": 3,
      ""fireIntervalReductionPerLevel"": 1, ""projectileSpeed"": 20,
      ""projectileHalfWidth"": 0.375, ""projectileHalfHeight"": 0.140625,
      ""pierceEnemyCount"": 0, ""spreadWays"": 2,
      ""spreadStepLutSlots"": 2 },
    { ""id"": ""laser"", ""displayName"": ""Laser"",
      ""description"": ""Pierces three aligned targets."", ""weaponType"": ""laser"",
      ""baseDamage"": 15, ""fireIntervalTicks"": 16,
      ""minimumFireIntervalTicks"": 8, ""rapidFireStartLevel"": 2,
      ""fireIntervalReductionPerLevel"": 2, ""projectileSpeed"": 28,
      ""projectileHalfWidth"": 0.1875, ""projectileHalfHeight"": 0.0703125,
      ""pierceEnemyCount"": 2, ""spreadWays"": 1,
      ""spreadStepLutSlots"": 0 }
  ]
");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                weapons,
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")),
                PrimaryRewardsV2Json);

            PrimaryWeaponFamilyDefinition laser =
                data.BattleContent.FindPrimaryWeaponFamily(
                    PrimaryWeaponFamily.Laser);
            Assert.AreEqual("Laser", laser.DisplayName);
            Assert.AreEqual(
                "Pierces three aligned targets.",
                laser.Description);
            Assert.AreEqual(2, laser.PierceEnemyCount);
            Assert.AreEqual(
                RewardType.PrimaryWeaponFamily,
                data.Rewards.All[0].Type);
            Assert.AreEqual(
                PrimaryWeaponFamily.Double,
                data.Rewards.All[0].PrimaryWeaponFamily);
            Assert.IsNotNull(
                data.BattleContent.FindPrimaryWeaponFamily(
                    PrimaryWeaponFamily.Vulcan),
                "A minimal v4 Double/Laser table must retain the legacy "
                + "Vulcan profile for existing starting ships.");
            Assert.IsNotNull(
                data.BattleContent.FindPrimaryWeaponFamily(
                    PrimaryWeaponFamily.Spread),
                "A minimal v4 Double/Laser table must retain the legacy "
                + "Spread profile for existing starting ships.");
        }

        [Test]
        public void WeaponsV6PowerUpGaugeOrderCostsAndNamesAreDataDriven()
        {
            string root = FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(
                    Path.Combine(gameData, "enemies.json")),
                WeaponsV6Json(),
                File.ReadAllText(
                    Path.Combine(gameData, "waves.json")));
            PowerUpGauge gauge = data.CreatePowerUpGauge();

            Assert.AreEqual(7, gauge.GaugeSlotCount);
            PowerUpGaugeSlotView speed = gauge.GetGaugeSlotView(0);
            Assert.AreEqual(PowerUpSlot.Speed, speed.Slot);
            Assert.AreEqual("powerUp.speed.full", speed.NameKey);
            Assert.AreEqual(6, speed.MaxLevel);
            Assert.AreEqual(2, speed.RequiredCapsules);
            Assert.AreEqual(
                SimSpace.SubUnitsPerWorldUnit * 5 / 4,
                gauge.GaugeSlots[0].SpeedBonusNumerator);
            Assert.AreEqual(
                SimSpace.TicksPerSecond,
                gauge.GaugeSlots[0].SpeedBonusDenominator);
            Assert.AreEqual(
                PowerUpSlot.Missile,
                gauge.GetGaugeSlotView(1).Slot);
            Assert.AreEqual(
                PowerUpSlot.Double,
                gauge.GetGaugeSlotView(2).Slot);
            Assert.AreEqual(
                PowerUpSlot.Laser,
                gauge.GetGaugeSlotView(3).Slot);
            Assert.AreEqual(
                PowerUpSlot.Triple,
                gauge.GetGaugeSlotView(4).Slot);
            Assert.AreEqual(
                PowerUpSlot.Option,
                gauge.GetGaugeSlotView(5).Slot);
            Assert.AreEqual(
                PowerUpSlot.Shield,
                gauge.GetGaugeSlotView(6).Slot);
        }

        static BulletState FireMissile(
            MissileFamily family,
            int speedX,
            int fallSpeedY,
            BattleModifier modifier)
        {
            BattleSimConfig config = Config();
            ConfigureMissile(
                config,
                family,
                speedX,
                fallSpeedY);
            BattleSim sim = CreateSim(
                config,
                Gauge(missileLevel: 1),
                modifier,
                Array.Empty<EnemyDefinition>(),
                Array.Empty<SpawnEvent>());
            FireAndAdvance(sim, 1);
            return FindBullet(sim, BulletKind.Missile);
        }

        static void ConfigureMissile(
            BattleSimConfig config,
            MissileFamily family,
            int speedX,
            int fallSpeedY)
        {
            config.MissileFamily = family;
            config.MissileSpeedXNumerator = speedX;
            config.MissileSpeedXDenominator = 1;
            config.MissileFallSpeedYNumerator = fallSpeedY;
            config.MissileFallSpeedYDenominator = 1;
            config.MissileFireIntervalTicks = 100;
            config.MissileMinimumFireIntervalTicks = 50;
        }

        static BattleSim CreateSim(
            BattleSimConfig config,
            PowerUpGauge gauge,
            BattleModifier modifiers,
            EnemyDefinition[] enemies,
            SpawnEvent[] spawns)
        {
            var weapon = new WeaponDefinition(
                "shot",
                0,
                100,
                100,
                1,
                0,
                0);
            var content = new BattleContent(
                enemies,
                new[] { weapon },
                weapon.Id);
            var segment = new StageSegment(
                "weapon_expansion",
                1000,
                spawns,
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                "legacy",
                1,
                1,
                1);
            return new BattleSim(
                config,
                new Rng(123UL),
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
                MaxBullets = 64,
                PlayerMinX = -10000,
                PlayerMaxX = 10000,
                PlayerMinY = -10000,
                PlayerMaxY = 10000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 100,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 1,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                MissileBaseDamage = 1,
                MissileFireIntervalTicks = 100,
                MissileMinimumFireIntervalTicks = 50,
                MissileSpeedXNumerator = 100,
                MissileSpeedXDenominator = 1,
                MissileFallSpeedYNumerator = 0,
                MissileFallSpeedYDenominator = 1,
                MissileHalfWidth = 0,
                MissileHalfHeight = 0,
                OptionFixedOffsetXs =
                    new[] { 192, 192, 192, 192 },
                OptionFixedOffsetYs =
                    new[] { 384, -384, 704, -704 },
                EnemyBulletDamage = 0,
                MaxEnemyBullets = 0
            };
        }

        static PowerUpGauge Gauge(
            int missileLevel = 0,
            int optionLevel = 0)
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(
                new[] { 0, missileLevel, optionLevel, 0 });
            return gauge;
        }

        static string WeaponsV6Json()
        {
            string weapons = WeaponsV3Json
                .Replace(
                    @"""schemaVersion"": 3",
                    @"""schemaVersion"": 6")
                .Replace(
                    @"""maxLevel"": 5 }",
                    @"""maxLevel"": 5, ""effectSoftCapLevel"": 5 }")
                .Replace(
                    @"""maxLevel"": 4 }",
                    @"""maxLevel"": 4, ""effectSoftCapLevel"": 4 }")
                .Replace(
                    @"""maxLevel"": 3 }",
                    @"""maxLevel"": 3, ""effectSoftCapLevel"": 3 }");
            int closingBrace = weapons.LastIndexOf('}');
            return weapons.Insert(
                closingBrace,
                @",
  ""primaryWeaponFamilies"": [
    { ""id"": ""double"", ""displayName"": ""Double"",
      ""description"": ""Two-way coverage shot."", ""weaponType"": ""spread"",
      ""baseDamage"": 6, ""fireIntervalTicks"": 10,
      ""minimumFireIntervalTicks"": 6, ""rapidFireStartLevel"": 3,
      ""fireIntervalReductionPerLevel"": 1, ""projectileSpeed"": 20,
      ""projectileHalfWidth"": 0.375, ""projectileHalfHeight"": 0.140625,
      ""pierceEnemyCount"": 0, ""spreadWays"": 2,
      ""spreadStepLutSlots"": 2 },
    { ""id"": ""laser"", ""displayName"": ""Laser"",
      ""description"": ""Pierces three aligned targets."", ""weaponType"": ""laser"",
      ""baseDamage"": 15, ""fireIntervalTicks"": 16,
      ""minimumFireIntervalTicks"": 8, ""rapidFireStartLevel"": 2,
      ""fireIntervalReductionPerLevel"": 2, ""projectileSpeed"": 28,
      ""projectileHalfWidth"": 0.1875, ""projectileHalfHeight"": 0.0703125,
      ""pierceEnemyCount"": 2, ""spreadWays"": 1,
      ""spreadStepLutSlots"": 0 }
  ],
  ""powerUpCostCurve"": {
    ""baseCost"": 1, ""linearGrowth"": 1, ""quadraticGrowth"": 0
  },
  ""powerUpGauge"": {
    ""slots"": [
      { ""slot"": ""Speed"", ""nameKey"": ""powerUp.speed.full"",
        ""maxLevel"": 6, ""speedBonusPerLevel"": 1.25,
        ""costCurve"": {
          ""baseCost"": 2, ""linearGrowth"": 1, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Missile"", ""nameKey"": ""powerUp.missile.full"",
        ""maxLevel"": 3, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 1, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Double"", ""nameKey"": ""powerUp.double.full"",
        ""maxLevel"": 1, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 0, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Laser"", ""nameKey"": ""powerUp.laser.full"",
        ""maxLevel"": 1, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 0, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Triple"", ""nameKey"": ""powerUp.triple.full"",
        ""maxLevel"": 1, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 0, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Option"", ""nameKey"": ""powerUp.option.full"",
        ""maxLevel"": 4, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 1, ""quadraticGrowth"": 0
        } },
      { ""slot"": ""Shield"", ""nameKey"": ""powerUp.shield.full"",
        ""maxLevel"": 3, ""costCurve"": {
          ""baseCost"": 1, ""linearGrowth"": 1, ""quadraticGrowth"": 0
        } }
    ]
  }
");
        }

        static EnemyDefinition Enemy(string id, int hp)
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
                0,
                0,
                0,
                0,
                1,
                64);
        }

        static SpawnEvent Spawn(string id, int x, int y)
        {
            return new SpawnEvent(0, id, x, y);
        }

        static void FireAndAdvance(
            BattleSim sim,
            int followingSteps)
        {
            var fire = new InputCommand(0, 0, true);
            sim.Step(in fire);
            InputCommand none = InputCommand.None;
            for (int i = 0; i < followingSteps; i++)
                sim.Step(in none);
        }

        static BulletState FindBullet(
            BattleSim sim,
            BulletKind kind)
        {
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Kind == kind)
                    return sim.Bullets[i];
            Assert.Fail("Expected bullet was not found.");
            return default;
        }

        static int CountEvents(
            BattleSim sim,
            SimEventType type)
        {
            int count = 0;
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    count++;
            return count;
        }

        static void AssertOptionPositionsEqual(
            BattleSim first,
            BattleSim second)
        {
            Assert.AreEqual(first.Options.Count, second.Options.Count);
            for (int i = 0; i < first.Options.Count; i++)
            {
                Assert.AreEqual(
                    first.Options[i].X,
                    second.Options[i].X);
                Assert.AreEqual(
                    first.Options[i].Y,
                    second.Options[i].Y);
            }
        }

        static RunManager CreateRewardRun()
        {
            BattleContent content = ExpandedContent();
            return new RunManager(
                123UL,
                new RewardStageGenerator(),
                Config(),
                content,
                Gauge(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                SwitchRewards(),
                ShipDefinition.CreateDefault(),
                1,
                1,
                new RunProgressionConfig(2, 1));
        }

        static BattleSim CreateExplosionAllocationSim()
        {
            const int count = 16;
            var enemies = new EnemyDefinition[count];
            var spawns = new SpawnEvent[count];
            for (int i = 0; i < count; i++)
            {
                string id = "bomb_target_" + i;
                enemies[i] = Enemy(id, 2);
                spawns[i] = Spawn(id, 100, i);
            }
            BattleSimConfig config = Config();
            ConfigureMissile(
                config,
                MissileFamily.SpreadBomb,
                100,
                0);
            config.MissileBaseDamage = 0;
            config.MissileExplosionDamage = 1;
            config.MissileExplosionRadiusSubUnits = 100;
            config.MissileExplosionMaxTargets = count;
            return CreateSim(
                config,
                Gauge(missileLevel: 1),
                BattleModifier.KillExplosion,
                enemies,
                spawns);
        }

        static void AdvanceToFirstBossReward(RunManager run)
        {
            InputCommand none = InputCommand.None;
            for (int i = 0;
                i < 5000
                    && run.State != RunState.AwaitingReward;
                i++)
            {
                var fire = new InputCommand(0, 0, true);
                InputCommand input = run.IsBiomeBoss
                    ? fire
                    : none;
                run.Step(in input);
            }
            Assert.AreEqual(RunState.AwaitingReward, run.State);
        }

        static RunManager ResumeRewardRun(RunSuspendData data)
        {
            return RunManager.ResumeFromSuspendData(
                data,
                new RewardStageGenerator(),
                Config(),
                ExpandedContent(),
                Gauge(),
                SwitchRewards(),
                ShipDefinition.CreateDefault());
        }

        static RewardCatalog SwitchRewards()
        {
            return new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    SwitchMissile(
                        "straight",
                        MissileFamily.Straight),
                    SwitchMissile(
                        "bomb",
                        MissileFamily.SpreadBomb),
                    SwitchMissile(
                        "lance",
                        MissileFamily.PiercingLance),
                    SwitchOption(
                        "trail",
                        OptionFormation.Trail),
                    SwitchOption(
                        "fixed",
                        OptionFormation.Fixed)
                });
        }

        static RewardDefinition SwitchMissile(
            string id,
            MissileFamily family)
        {
            return new RewardDefinition(
                id,
                RewardType.MissileFamily,
                PowerUpSlot.Missile,
                1,
                1,
                1,
                99,
                null,
                BattleModifier.None,
                family);
        }

        static RewardDefinition SwitchOption(
            string id,
            OptionFormation formation)
        {
            return new RewardDefinition(
                id,
                RewardType.OptionFormation,
                PowerUpSlot.Option,
                1,
                1,
                1,
                99,
                null,
                BattleModifier.None,
                MissileFamily.Straight,
                formation);
        }

        static BattleContent ExpandedContent()
        {
            int u = SimSpace.SubUnitsPerWorldUnit;
            var main = new WeaponDefinition(
                "shot",
                1,
                1,
                100,
                1,
                0,
                0);
            var missileFamilies = new[]
            {
                Missile(
                    MissileFamily.Straight,
                    20,
                    30,
                    10 * u,
                    0,
                    0,
                    0,
                    0),
                Missile(
                    MissileFamily.SpreadBomb,
                    12,
                    42,
                    6 * u,
                    9 * u,
                    0,
                    16,
                    448),
                Missile(
                    MissileFamily.PiercingLance,
                    40,
                    54,
                    16 * u,
                    0,
                    2,
                    0,
                    0)
            };
            var formations = new[]
            {
                new OptionFormationDefinition(
                    OptionFormation.Trail,
                    12,
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    0,
                    0,
                    1),
                new OptionFormationDefinition(
                    OptionFormation.Fixed,
                    0,
                    new[] { 192, 192, 192, 192 },
                    new[] { 384, -384, 704, -704 },
                    0,
                    0,
                    1),
                new OptionFormationDefinition(
                    OptionFormation.Orbit,
                    0,
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    448,
                    1,
                    2)
            };
            return new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { main },
                main.Id,
                missileFamilies,
                MissileFamily.Straight,
                formations,
                OptionFormation.Trail);
        }

        static MissileFamilyDefinition Missile(
            MissileFamily family,
            int damage,
            int interval,
            int speedX,
            int fallY,
            int pierce,
            int explosionDamage,
            int explosionRadius)
        {
            return new MissileFamilyDefinition(
                family,
                damage,
                interval,
                Math.Max(1, interval / 2),
                family == MissileFamily.PiercingLance ? 6 : 5,
                speedX,
                SimSpace.TicksPerSecond,
                fallY,
                SimSpace.TicksPerSecond,
                pierce,
                explosionDamage,
                explosionRadius,
                explosionDamage == 0 ? 0 : 5);
        }

        static string FindRepositoryRoot()
        {
            // Unity EditMode의 WorkDirectory는 프로젝트 밖을 가리킬 수 있어
            // 실행 파일 위치와 현재 디렉토리까지 함께 훑는다 (dotnet에서는 첫 후보로 충분).
            foreach (string start in new[]
            {
                TestContext.CurrentContext.WorkDirectory,
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory
            })
            {
                string path = start;
                while (!string.IsNullOrEmpty(path))
                {
                    if (Directory.Exists(Path.Combine(path, "GameData")))
                        return path;
                    path = Directory.GetParent(path)?.FullName;
                }
            }
            throw new DirectoryNotFoundException(
                "Could not find repository GameData.");
        }

        sealed class RewardStageGenerator : IStageGenerator
        {
            static readonly BossPhase[] Phases =
            {
                new BossPhase(999, 1, 1, 1)
            };

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "reward",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    100,
                    Phases);
            }
        }

        const string RewardsWithSwitchesJson = @"{
  ""schemaVersion"": 1,
  ""optionCount"": 3,
  ""rewards"": [
    { ""id"": ""bomb"", ""type"": ""missileFamily"",
      ""familyId"": ""spread_bomb"", ""weight"": 2,
      ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""lance"", ""type"": ""missileFamily"",
      ""familyId"": ""piercing_lance"", ""weight"": 2,
      ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""orbit"", ""type"": ""optionFormation"",
      ""formationId"": ""orbit"", ""weight"": 2,
      ""stageIndexMin"": 1, ""stageIndexMax"": 99 }
  ]
}";

        const string PrimaryRewardsV2Json = @"{
  ""schemaVersion"": 2,
  ""optionCount"": 3,
  ""rewards"": [
    { ""id"": ""double"", ""type"": ""primaryWeaponFamily"",
      ""primaryFamilyId"": ""double"", ""weight"": 1,
      ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""laser"", ""type"": ""primaryWeaponFamily"",
      ""primaryFamilyId"": ""laser"", ""weight"": 1,
      ""stageIndexMin"": 1, ""stageIndexMax"": 99 },
    { ""id"": ""capsules"", ""type"": ""capsules"", ""amount"": 1,
      ""weight"": 1, ""stageIndexMin"": 1, ""stageIndexMax"": 99 }
  ]
}";

        const string WeaponsV3Json = @"{
  ""schemaVersion"": 3,
  ""weapons"": [
    { ""id"": ""main_shot"", ""slot"": ""MainShot"", ""baseDamage"": 10,
      ""fireIntervalTicks"": 8, ""projectileSpeed"": 20,
      ""projectileHalfWidth"": 0.375, ""projectileHalfHeight"": 0.140625,
      ""maxLevel"": 5 },
    { ""id"": ""missile"", ""slot"": ""Missile"", ""baseDamage"": 20,
      ""fireIntervalTicks"": 30, ""minimumFireIntervalTicks"": 15,
      ""projectileSpeed"": 10, ""projectileHalfWidth"": 0.46875,
      ""projectileHalfHeight"": 0.28125, ""maxLevel"": 3 },
    { ""id"": ""option"", ""slot"": ""Option"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 4 },
    { ""id"": ""shield"", ""slot"": ""Shield"", ""baseDamage"": 0,
      ""fireIntervalTicks"": 0, ""projectileSpeed"": 0,
      ""projectileHalfWidth"": 0, ""projectileHalfHeight"": 0,
      ""maxLevel"": 3 }
  ],
  ""missileFamilies"": [
    { ""id"": ""straight"", ""baseDamage"": 20,
      ""fireIntervalTicks"": 30, ""minimumFireIntervalTicks"": 15,
      ""fireIntervalReductionPerLevel"": 5, ""projectileSpeed"": 10,
      ""fallSpeedY"": 0, ""pierceEnemyCount"": 0,
      ""explosionDamage"": 0, ""explosionRadius"": 0,
      ""explosionMaxTargets"": 0 },
    { ""id"": ""spread_bomb"", ""baseDamage"": 12,
      ""fireIntervalTicks"": 42, ""minimumFireIntervalTicks"": 28,
      ""fireIntervalReductionPerLevel"": 5, ""projectileSpeed"": 6,
      ""fallSpeedY"": 9, ""pierceEnemyCount"": 0,
      ""explosionDamage"": 16, ""explosionRadius"": 1.75,
      ""explosionMaxTargets"": 5 },
    { ""id"": ""piercing_lance"", ""baseDamage"": 40,
      ""fireIntervalTicks"": 54, ""minimumFireIntervalTicks"": 36,
      ""fireIntervalReductionPerLevel"": 6, ""projectileSpeed"": 16,
      ""fallSpeedY"": 0, ""pierceEnemyCount"": 2,
      ""explosionDamage"": 0, ""explosionRadius"": 0,
      ""explosionMaxTargets"": 0 }
  ],
  ""defaultMissileFamily"": ""straight"",
  ""optionFormations"": [
    { ""id"": ""trail"", ""followDelayTicks"": 12 },
    { ""id"": ""fixed"", ""offsets"": [
      { ""x"": 0.75, ""y"": 1.5 },
      { ""x"": 0.75, ""y"": -1.5 },
      { ""x"": 0.75, ""y"": 2.75 },
      { ""x"": 0.75, ""y"": -2.75 }
    ] },
    { ""id"": ""orbit"", ""radius"": 1.75,
      ""angularLutSlotsNumerator"": 1,
      ""angularLutSlotsDenominator"": 2 }
  ],
  ""defaultOptionFormation"": ""trail""
}";
    }
}
