using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class RoomContinuityTests
    {
        [Test]
        public void SameBiomeRoomCarriesPlayerPosition()
        {
            BattleSimConfig config = CreateConfig();
            var manager = CreateManager(0x6501UL, config);
            InputCommand input =
                InputCommand.Analog(3, 4, false);

            manager.Step(in input);
            manager.Step(in input);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(1, manager.Battle.PlayerX);
            Assert.AreEqual(8, manager.Battle.PlayerY);
        }

        [Test]
        public void CarriedPositionClampsToBattleBounds()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerMinX = -2;
            config.PlayerMaxX = 2;
            config.PlayerMinY = -3;
            config.PlayerMaxY = 3;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            StagePlan plan =
                new ShortStageGenerator(false)
                    .Generate(0x6502UL, 1, 1);
            var continuity =
                new BattleContinuityState(7, 8, 0, 0, 0);
            var sim = new BattleSim(
                config,
                new Rng(0x6502UL),
                plan,
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                BattleModifierStackSet.FromFlags(
                    BattleModifier.None,
                    4),
                continuity);

            Assert.AreEqual(2, sim.PlayerX);
            Assert.AreEqual(3, sim.PlayerY);
        }

        [Test]
        public void BiomeBoundaryResetsPositionAndCombo()
        {
            BattleSimConfig config = CreateConfig();
            var manager = CreateManager(0x6503UL, config);
            InputCommand move =
                InputCommand.Analog(2, 1, false);

            StepUntilReward(manager, RewardSelectionKind.MidStage, in move);
            Assert.IsTrue(manager.ChooseReward(0));
            StepUntilReward(manager, RewardSelectionKind.Main, in move);
            Assert.IsTrue(manager.ChooseReward(0));
            Assert.IsTrue(manager.ChooseContract(0));

            Assert.AreEqual(2, manager.BiomeIndex);
            Assert.AreEqual(1, manager.RoomIndex);
            Assert.AreEqual(config.PlayerSpawnX, manager.Battle.PlayerX);
            Assert.AreEqual(config.PlayerSpawnY, manager.Battle.PlayerY);
            Assert.AreEqual(0, manager.Battle.MultiplierLevel);
            Assert.AreEqual(0, manager.Battle.ComboGauge);
        }

        [Test]
        public void SameBiomeRoomCarriesComboGaugeAndDecayAge()
        {
            BattleSimConfig config = CreateConfig();
            var manager = new RunManager(
                0x6504UL,
                new ShortStageGenerator(true),
                config,
                CreateScoringContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            manager.Step(in fire);
            manager.Step(in fire);

            Assert.AreEqual(1L, manager.Statistics.Kills);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(config.KillComboGaugeGain, manager.Battle.ComboGauge);
            Assert.AreEqual(0, manager.Battle.TicksSinceLastKill);
        }

        [Test]
        public void SuspendAtCarriedRoomBoundaryRestoresContinuity()
        {
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 3;
            config.ScrollSpeedDenominator = 2;
            var manager = CreateManager(0x6505UL, config);
            InputCommand input =
                InputCommand.Analog(3, -2, false);
            manager.Step(in input);
            manager.Step(in input);
            RunSuspendData data = manager.ExportSuspendData();

            BattleSimConfig resumeConfig = CreateConfig();
            resumeConfig.ScrollSpeedNumerator = 3;
            resumeConfig.ScrollSpeedDenominator = 2;
            RunManager resumed = RunManager.ResumeFromSuspendData(
                data,
                new ShortStageGenerator(false),
                resumeConfig,
                CreateContent(),
                PowerUpGauge.CreateDefault());

            Assert.AreEqual(manager.Battle.PlayerX, resumed.Battle.PlayerX);
            Assert.AreEqual(manager.Battle.PlayerY, resumed.Battle.PlayerY);
            Assert.AreEqual(
                manager.Battle.ComboGauge,
                resumed.Battle.ComboGauge);
            Assert.AreEqual(
                manager.Battle.TicksSinceLastKill,
                resumed.Battle.TicksSinceLastKill);
            Assert.AreEqual(3L, data.stageStartScrollX);
            Assert.AreEqual(manager.Battle.ScrollX, resumed.Battle.ScrollX);
        }

        [Test]
        public void ScrollContinuesAtRoomBoundary()
        {
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 3;
            config.ScrollSpeedDenominator = 2;
            var manager = CreateManager(0x6507UL, config);

            manager.Step(InputCommand.None);
            manager.Step(InputCommand.None);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(3L, manager.Battle.ScrollX);
            Assert.AreEqual(
                3L,
                ((BattleSim)manager.Battle).GetScrollXAtTick(0));
        }

        [Test]
        public void ScrollContinuesAcrossBiomeBoundary()
        {
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 2;
            var manager = CreateManager(0x650AUL, config);

            manager.Step(InputCommand.None);
            manager.Step(InputCommand.None);
            manager.Step(InputCommand.None);
            manager.Step(InputCommand.None);
            Assert.AreEqual(RunState.AwaitingReward, manager.State);
            Assert.IsTrue(manager.ChooseReward(0));

            manager.Step(InputCommand.None);
            manager.Step(InputCommand.None);
            Assert.IsTrue(manager.IsBiomeBoss);
            manager.Step(InputCommand.None);
            Assert.AreEqual(RunState.AwaitingReward, manager.State);
            Assert.IsTrue(manager.ChooseReward(0));
            Assert.IsTrue(manager.ChooseContract(0));

            Assert.AreEqual(2, manager.BiomeIndex);
            Assert.AreEqual(1, manager.RoomIndex);
            Assert.AreEqual(14L, manager.Battle.ScrollX);
            Assert.AreEqual(config.PlayerSpawnX, manager.Battle.PlayerX);
            Assert.AreEqual(config.PlayerSpawnY, manager.Battle.PlayerY);
        }

        [Test]
        public void PreBossRoomSuppressesLateSpawnsAndDrainsBeforeBoundary()
        {
            BattleSimConfig config = CreateConfig();
            config.ScrollSpeedNumerator = 3;
            config.ScrollSpeedDenominator = 2;
            var manager = new RunManager(
                0x6508UL,
                new BoundaryStageGenerator(),
                config,
                CreateBoundaryContent(),
                PowerUpGauge.CreateDefault());
            BattleSim opening = (BattleSim)manager.Battle;

            for (int tick = 0; tick < 20; tick++)
                manager.Step(InputCommand.None);
            Assert.AreEqual(1, opening.Enemies.Count);

            for (int tick = 20; tick < 79; tick++)
            {
                manager.Step(InputCommand.None);
                if (tick == 39)
                    Assert.AreEqual(0, opening.Enemies.Count);
            }

            Assert.AreEqual(79, opening.Tick);
            Assert.AreEqual(118L, opening.ScrollX);
            Assert.AreEqual(0, opening.Enemies.Count);

            manager.Step(InputCommand.None);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(0, opening.Enemies.Count);
            Assert.AreEqual(120L, opening.ScrollX);
            Assert.AreEqual(120L, manager.Battle.ScrollX);
            Assert.AreEqual(0, manager.Battle.Enemies.Count);
        }

        [Test]
        public void CleanupWindowSuppressesCapsuleAndBombDrops()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpawnX = 0;
            config.PlayerBulletSpeedPerTick = 10;
            config.CapsuleNoDropWeight = 0;
            config.BombNoDropWeight = 0;
            config.MaxBombPickups = 4;
            var enemy = new EnemyDefinition(
                "drop_target",
                "Drop Target",
                1,
                0,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                1,
                0,
                1,
                1,
                0,
                1,
                0,
                1,
                null);
            var weapon =
                new WeaponDefinition("drop_shot", 1, 1, 10, 1, 0, 0);
            var content = new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
            var segment = new StageSegment(
                "drop_cleanup",
                80,
                new[]
                {
                    new SpawnEvent(0, enemy.Id, 522, 0)
                },
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                string.Empty,
                1,
                1,
                1);
            var sim = new BattleSim(
                config,
                new Rng(0x6510UL),
                plan,
                content,
                PowerUpGauge.CreateDefault(),
                BattleModifierStackSet.FromFlags(
                    BattleModifier.None,
                    4),
                null,
                true);

            for (int tick = 0; tick < 18; tick++)
                sim.Step(InputCommand.None);
            sim.Step(new InputCommand(0, 0, true));
            sim.Step(InputCommand.None);

            Assert.AreEqual(1L, sim.Statistics.Kills);
            Assert.AreEqual(0, sim.Capsules.Count);
            Assert.AreEqual(0, sim.BombPickups.Count);
        }

        [Test]
        public void BoundaryWaitsForResidualHostileBulletsToExit()
        {
            BattleSimConfig config = CreateConfig();
            config.MaxEnemyBullets = 64;
            config.EnemyBulletSpeedNumerator = 0;
            config.BulletDespawnX = 100000;
            config.PlayerSpawnY = 100;
            var manager = new RunManager(
                0x6511UL,
                new BoundaryStageGenerator(),
                config,
                CreateBoundaryContent(),
                PowerUpGauge.CreateDefault());
            BattleSim opening = (BattleSim)manager.Battle;

            for (int tick = 0; tick < 80; tick++)
                manager.Step(InputCommand.None);

            Assert.AreEqual(1, manager.RoomIndex);
            Assert.AreEqual(80, opening.Tick);
            Assert.Greater(opening.Bullets.Count, 0);

            for (int guard = 0;
                guard < 300 && manager.RoomIndex == 1;
                guard++)
                manager.Step(InputCommand.None);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(0, opening.Enemies.Count);
            Assert.AreEqual(0, opening.Bullets.Count);
        }

        [Test]
        public void BoundaryIgnoresContinuouslyFiringPlayerBeam()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerWeaponType = WeaponType.Laser;
            config.PlayerWeaponFamily = PrimaryWeaponFamily.Laser;
            var gauge = new PowerUpGauge(new[] { 5, 3, 4, 3 });
            gauge.GrantLevels(PowerUpSlot.MainShot, 3);
            gauge.GrantLevels(PowerUpSlot.Laser, 3);
            var manager = new RunManager(
                0UL,
                new BoundaryStageGenerator(),
                config,
                CreateBoundaryLaserContent(),
                gauge);
            BattleSim opening = (BattleSim)manager.Battle;
            var firing = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 80; tick++)
                manager.Step(in firing);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(1, opening.Lasers.Count);
            Assert.AreEqual(
                LaserSourceKind.Player,
                opening.Lasers[0].SourceKind);
        }

        [Test]
        public void BoundaryForcesProgressAtDeterministicWaitLimit()
        {
            BattleSimConfig config = CreateConfig();
            config.EnemyDespawnX = int.MinValue;
            var manager = new RunManager(
                0UL,
                new BoundaryStageGenerator(),
                config,
                CreateBoundaryContent(),
                PowerUpGauge.CreateDefault());
            BattleSim opening = (BattleSim)manager.Battle;
            int transitionTick = 80
                + BattleSim.RoomBoundaryMaximumWaitTicks;

            for (int tick = 0; tick < transitionTick - 1; tick++)
                manager.Step(InputCommand.None);

            Assert.AreEqual(1, manager.RoomIndex);
            Assert.AreEqual(
                BattleSim.RoomBoundaryMaximumWaitTicks - 1,
                opening.RoomBoundaryWaitTicks);
            Assert.IsFalse(opening.RoomBoundaryWaitLimitReached);
            Assert.Greater(opening.Enemies.Count, 0);

            manager.Step(InputCommand.None);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(
                BattleSim.RoomBoundaryMaximumWaitTicks,
                opening.RoomBoundaryWaitTicks);
            Assert.IsTrue(opening.RoomBoundaryWaitLimitReached);
        }

        [Test]
        public void TimedMidBossHoverMeanRemainsNearSpawnAnchor()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerMinY = -4096;
            config.PlayerMaxY = 4096;
            config.EnemyDespawnX = -4096;
            var phases = new[]
            {
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.Stationary,
                    0, 1, 1,
                    BossPartVulnerability.Legacy,
                    120, 0),
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.VerticalSine,
                    256, 1, 90,
                    BossPartVulnerability.Legacy,
                    105, 0)
            };
            StagePlan plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "entry",
                        1,
                        new SpawnEvent[0],
                        1,
                        1,
                        new[] { 1 })
                },
                "mid",
                1,
                1,
                1,
                100000,
                16,
                16,
                0,
                phases);
            var sim = new BattleSim(
                config,
                new Rng(0x6509UL),
                plan,
                CreateContent(),
                PowerUpGauge.CreateDefault());

            for (int tick = 0;
                tick < 512
                    && (!sim.BossActive || sim.BossEntering);
                tick++)
                sim.Step(InputCommand.None);

            const int windowTicks = 450;
            long windowSum = 0;
            int maxAbsWindowMean = 0;
            int minY = sim.Boss.Y;
            int maxY = sim.Boss.Y;
            for (int tick = 0; tick < 4500; tick++)
            {
                sim.Step(InputCommand.None);
                windowSum += sim.Boss.Y;
                minY = System.Math.Min(minY, sim.Boss.Y);
                maxY = System.Math.Max(maxY, sim.Boss.Y);
                if ((tick + 1) % windowTicks == 0)
                {
                    int mean = (int)(windowSum / windowTicks);
                    maxAbsWindowMean = System.Math.Max(
                        maxAbsWindowMean,
                        System.Math.Abs(mean));
                    TestContext.WriteLine(
                        "window={0} meanY={1}",
                        (tick + 1) / windowTicks,
                        mean);
                    windowSum = 0;
                }
            }
            TestContext.WriteLine(
                "minY={0} maxY={1} maxAbsMean={2}",
                minY,
                maxY,
                maxAbsWindowMean);

            Assert.LessOrEqual(maxAbsWindowMean, 256);
            Assert.GreaterOrEqual(minY, -256);
            Assert.LessOrEqual(maxY, 256);
        }

        [Test]
        public void SameSeedInputsAndChoicesMatchHashesAcrossRoomTransitions()
        {
            RunManager first =
                CreateManager(0x6506UL, CreateConfig());
            RunManager second =
                CreateManager(0x6506UL, CreateConfig());
            InputCommand input =
                InputCommand.Analog(2, -1, false);

            for (int tick = 0; tick < 10; tick++)
            {
                first.Step(in input);
                second.Step(in input);
                if (first.State == RunState.AwaitingReward)
                {
                    Assert.AreEqual(
                        first.RewardSelectionKind,
                        second.RewardSelectionKind);
                    Assert.IsTrue(first.ChooseReward(0));
                    Assert.IsTrue(second.ChooseReward(0));
                }
                else if (first.State
                    == RunState.AwaitingContract)
                {
                    Assert.AreEqual(
                        RunState.AwaitingContract,
                        second.State);
                    Assert.IsTrue(first.ChooseContract(0));
                    Assert.IsTrue(second.ChooseContract(0));
                }

                var firstHash = new DeterminismAuditHasher();
                var secondHash = new DeterminismAuditHasher();
                firstHash.FoldRunState(first);
                secondHash.FoldRunState(second);
                Assert.AreEqual(firstHash.Hash, secondHash.Hash);
            }
        }

        static RunManager CreateManager(
            ulong seed,
            BattleSimConfig config)
        {
            return new RunManager(
                seed,
                new ShortStageGenerator(false),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
        }

        static void StepUntilReward(
            RunManager manager,
            RewardSelectionKind kind,
            in InputCommand input)
        {
            for (int tick = 0; tick < 32; tick++)
            {
                if (manager.State == RunState.AwaitingReward)
                {
                    Assert.AreEqual(kind, manager.RewardSelectionKind);
                    return;
                }
                manager.Step(in input);
            }
            Assert.Fail("Expected reward boundary was not reached.");
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 10,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                MaxEnemyBullets = 0,
                PlayerMinX = -100,
                PlayerMaxX = 100,
                PlayerMinY = -100,
                PlayerMaxY = 100,
                BulletDespawnX = 100,
                EnemyDespawnX = -100,
                PlayerSpawnX = -5,
                PlayerSpawnY = 0,
                StartingShieldStock = 0,
                PlayerHitInvulnerabilityTicks = 0,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }

        static BattleContent CreateContent()
        {
            var enemy = new EnemyDefinition(
                "dummy",
                1,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var weapon =
                new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0);
            return new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateScoringContent()
        {
            var enemy = new EnemyDefinition(
                "target",
                "Target",
                1,
                0,
                100,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                0,
                1,
                1);
            var weapon =
                new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateBoundaryContent()
        {
            var regular = new EnemyDefinition(
                "boundary_target",
                "Boundary Target",
                1000,
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
            var phases = new[]
            {
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.Stationary,
                    0, 1, 1,
                    BossPartVulnerability.Legacy,
                    120, 0),
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.VerticalSine,
                    64, 1, 90,
                    BossPartVulnerability.Legacy,
                    105, 0)
            };
            var profile = new MidBossProfile(
                "boundary",
                1,
                1,
                99,
                phases);
            var midBoss = new EnemyDefinition(
                "mini_boundary",
                "Boundary Mid",
                1000,
                0,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                999,
                16,
                16,
                0,
                0,
                1,
                1,
                0,
                1,
                0,
                0,
                null,
                profile);
            var weapon =
                new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0);
            return new BattleContent(
                new[] { regular, midBoss },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateBoundaryLaserContent()
        {
            BattleContent baseContent = CreateBoundaryContent();
            int[] angles = { 0 };
            var levels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 1, 1, 1, 0, angles),
                new PrimaryWeaponLevelDefinition(
                    2, 1, 1, 1, 0, angles),
                new PrimaryWeaponLevelDefinition(
                    3, 1, 1, 1, 0, angles,
                    beamDamagePerTick: 1,
                    beamLength: 2000,
                    beamStartHalfWidth: 1,
                    beamGrowthPerTick: 1,
                    beamMaxHalfWidth: 4)
            };
            var laser = new PrimaryWeaponFamilyDefinition(
                PrimaryWeaponFamily.Laser,
                "Laser",
                "Boundary regression laser.",
                WeaponType.Laser,
                1, 1, 1, 1, 0,
                1, 1, 0, 0, 1, 1, 0,
                angles,
                levels);
            return new BattleContent(
                baseContent.Enemies,
                baseContent.Weapons,
                baseContent.PlayerWeapon.Id,
                new[]
                {
                    baseContent.FindPrimaryWeaponFamily(
                        PrimaryWeaponFamily.Double),
                    laser
                });
        }

        sealed class ShortStageGenerator : IStageGenerator
        {
            readonly bool _spawnTarget;

            public ShortStageGenerator(bool spawnTarget)
            {
                _spawnTarget = spawnTarget;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                SpawnEvent[] spawns = _spawnTarget
                    ? new[]
                    {
                        new SpawnEvent(0, "target", -4, 0)
                    }
                    : new SpawnEvent[0];
                var segment = new StageSegment(
                    "short",
                    2,
                    spawns,
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "none",
                    1,
                    1,
                    1);
            }
        }

        sealed class BoundaryStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "boundary",
                    80,
                    new[]
                    {
                        new SpawnEvent(
                            0,
                            "boundary_target",
                            1000,
                            0),
                        new SpawnEvent(
                            40,
                            "boundary_target",
                            1000,
                            0)
                    },
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "none",
                    1,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    null,
                    "boundary");
            }
        }
    }
}
