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
            var manager = CreateManager(0x6505UL, config);
            InputCommand input =
                InputCommand.Analog(3, -2, false);
            manager.Step(in input);
            manager.Step(in input);
            RunSuspendData data = manager.ExportSuspendData();

            RunManager resumed = RunManager.ResumeFromSuspendData(
                data,
                new ShortStageGenerator(false),
                CreateConfig(),
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
    }
}
