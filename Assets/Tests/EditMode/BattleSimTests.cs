using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BattleSimTests
    {
        [Test]
        public void InputCommand_ClampsMovementToDigitalDirections()
        {
            var input = new InputCommand(-12, 5, true);

            Assert.AreEqual(-1, input.MoveX);
            Assert.AreEqual(1, input.MoveY);
            Assert.IsTrue(input.Fire);
            Assert.AreEqual(0, InputCommand.None.MoveX);
            Assert.AreEqual(0, InputCommand.None.MoveY);
            Assert.IsFalse(InputCommand.None.Fire);
        }

        [Test]
        public void DefaultConfig_PreservesGameDataSpeedsAsExactFractions()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            int units = SimSpace.SubUnitsPerWorldUnit;

            // 640×360 전환(ROADMAP M0): 24×14 시절 값의 ×5/3 스케일.
            Assert.AreEqual(13 * units, config.PlayerSpeedNumerator);
            Assert.AreEqual(SimSpace.TicksPerSecond, config.PlayerSpeedDenominator);
            Assert.AreEqual(55, config.PlayerSpeedPerTick);
            Assert.AreEqual(20 * units, config.PlayerBulletSpeedNumerator);
            Assert.AreEqual(SimSpace.TicksPerSecond, config.PlayerBulletSpeedDenominator);
            Assert.AreEqual(85, config.PlayerBulletSpeedPerTick);
            Assert.AreEqual(8, config.FireIntervalTicks);
            Assert.AreEqual(64, config.MaxBullets);
            Assert.AreEqual(-13 * units, config.PlayerSpawnX);
            Assert.AreEqual(0, config.PlayerSpawnY);
            Assert.AreEqual(SimSpace.PlayfieldHalfWidthSubUnits + units, config.BulletDespawnX);
            Assert.AreEqual(
                -(SimSpace.PlayfieldHalfWidthSubUnits + SimSpace.DespawnMarginSubUnits),
                config.EnemyDespawnX);
        }

        [Test]
        public void ContinuousDefaultMovement_TravelsExactSourceDistance()
        {
            var sim = new BattleSim(BattleSimConfig.CreateDefault(), new Rng(1UL));
            var moveRight = new InputCommand(1, 0, false);

            for (int i = 0; i < SimSpace.TicksPerSecond; i++)
                sim.Step(in moveRight);

            Assert.AreEqual(0, sim.PlayerX);
        }

        [Test]
        public void PlayerMovement_AccumulatesFractionAndClampsToBounds()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedNumerator = 8;
            config.PlayerSpeedDenominator = 3;
            config.PlayerMinX = -20;
            config.PlayerMaxX = 10;
            var sim = new BattleSim(config, new Rng(2UL));
            var right = new InputCommand(1, 0, false);

            sim.Step(in right);
            Assert.AreEqual(2, sim.PlayerX);
            sim.Step(in right);
            Assert.AreEqual(5, sim.PlayerX);
            sim.Step(in right);
            Assert.AreEqual(8, sim.PlayerX);
            sim.Step(in right);
            Assert.AreEqual(10, sim.PlayerX);
            sim.Step(in right);
            Assert.AreEqual(10, sim.PlayerX);

            var left = new InputCommand(-1, 0, false);
            sim.Step(in left);
            Assert.AreEqual(8, sim.PlayerX);
        }

        [Test]
        public void BulletMovement_PreservesExactWorldUnitsPerSecond()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerBulletSpeedNumerator = 12 * SimSpace.SubUnitsPerWorldUnit;
            config.PlayerBulletSpeedDenominator = SimSpace.TicksPerSecond;
            var sim = new BattleSim(config, new Rng(3UL));
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            InputCommand none = InputCommand.None;
            for (int i = 0; i < 5; i++)
                sim.Step(in none);

            Assert.AreEqual(1, sim.Bullets.Count);
            Assert.AreEqual(SimSpace.SubUnitsPerWorldUnit, sim.Bullets[0].X);
        }

        [Test]
        public void FireCooldown_SpawnsAtConfiguredTickInterval()
        {
            BattleSimConfig config = CreateConfig();
            config.FireIntervalTicks = 8;
            config.PlayerBulletSpeedPerTick = 0;
            var sim = new BattleSim(config, new Rng(30UL));
            var fire = new InputCommand(0, 0, true);

            for (int i = 0; i < 8; i++)
                sim.Step(in fire);
            Assert.AreEqual(1, sim.Bullets.Count);

            sim.Step(in fire);
            Assert.AreEqual(2, sim.Bullets.Count);
            Assert.AreEqual(1, sim.Bullets[0].Id);
            Assert.AreEqual(2, sim.Bullets[1].Id);
        }

        [Test]
        public void Culling_PreservesMonotonicIdsAndSpawnOrder()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerBulletSpeedPerTick = 10;
            config.FireIntervalTicks = 0;
            config.BulletDespawnX = 15;
            var sim = new BattleSim(config, new Rng(4UL));
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            sim.Step(in fire);
            sim.Step(in fire);

            Assert.AreEqual(2, sim.Bullets.Count);
            Assert.AreEqual(2, sim.Bullets[0].Id);
            Assert.AreEqual(10, sim.Bullets[0].X);
            Assert.AreEqual(3, sim.Bullets[1].Id);
            Assert.AreEqual(0, sim.Bullets[1].X);
        }

        [Test]
        public void Bullets_ReadOnlyViewInstanceIsReusedAcrossTicks()
        {
            var sim = new BattleSim(CreateConfig(), new Rng(5UL));
            IReadOnlyList<BulletState> initial = sim.Bullets;
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);
            IReadOnlyList<BulletState> afterSpawn = sim.Bullets;
            sim.Step(in fire);

            Assert.AreSame(initial, afterSpawn);
            Assert.AreSame(initial, sim.Bullets);
            Assert.IsFalse(initial is List<BulletState>);
        }

        [Test]
        public void SameInputSequence_ProducesIdenticalBulletTrajectories()
        {
            var first = new BattleSim(BattleSimConfig.CreateDefault(), new Rng(0xC0FFEEUL));
            var second = new BattleSim(BattleSimConfig.CreateDefault(), new Rng(0xC0FFEEUL));

            for (int tick = 0; tick < 180; tick++)
            {
                var input = new InputCommand(
                    tick % 19 < 7 ? 1 : tick % 19 < 12 ? -1 : 0,
                    tick % 23 < 4 ? 1 : tick % 23 < 8 ? -1 : 0,
                    tick % 3 != 0);

                first.Step(in input);
                second.Step(in input);
                AssertSimStatesEqual(first, second, tick);
            }
        }

        [Test]
        public void Simulation_SnapshotsMutableConfigAtConstruction()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 3;
            var sim = new BattleSim(config, new Rng(6UL));

            config.PlayerSpeedPerTick = 100;
            var right = new InputCommand(1, 0, false);
            sim.Step(in right);

            Assert.AreEqual(3, sim.PlayerX);
        }

        [Test]
        public void Constructor_RejectsInvalidDependenciesAndConfig()
        {
            BattleSimConfig config = CreateConfig();

            Assert.Throws<ArgumentNullException>(() => new BattleSim(null, new Rng(1UL)));
            Assert.Throws<ArgumentNullException>(() => new BattleSim(config, null));
            config.PlayerSpeedDenominator = 0;
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BattleSim(config, new Rng(1UL)));
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -10000,
                PlayerMaxX = 10000,
                PlayerMinY = -10000,
                PlayerMaxY = 10000,
                BulletDespawnX = 10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0
            };
        }

        static void AssertSimStatesEqual(IBattleSim expected, IBattleSim actual, int tick)
        {
            Assert.AreEqual(expected.Tick, actual.Tick, $"input {tick}");
            Assert.AreEqual(expected.PlayerX, actual.PlayerX, $"input {tick}");
            Assert.AreEqual(expected.PlayerY, actual.PlayerY, $"input {tick}");
            Assert.AreEqual(expected.Bullets.Count, actual.Bullets.Count, $"input {tick}");

            for (int i = 0; i < expected.Bullets.Count; i++)
            {
                BulletState left = expected.Bullets[i];
                BulletState right = actual.Bullets[i];
                Assert.AreEqual(left.Id, right.Id, $"input {tick}, bullet {i}");
                Assert.AreEqual(left.Faction, right.Faction, $"input {tick}, bullet {i}");
                Assert.AreEqual(left.Kind, right.Kind, $"input {tick}, bullet {i}");
                Assert.AreEqual(left.X, right.X, $"input {tick}, bullet {i}");
                Assert.AreEqual(left.Y, right.Y, $"input {tick}, bullet {i}");
            }
        }
    }
}
