using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class BossFirePatternTests
    {
        [TestCase(BossFirePattern.Aimed, 3, 3)]
        [TestCase(BossFirePattern.Radial, 4, 4)]
        [TestCase(BossFirePattern.Spiral, 2, 2)]
        [TestCase(BossFirePattern.Wall, 5, 4)]
        public void PatternCreatesItsConfiguredVolley(
            BossFirePattern pattern,
            int ways,
            int expectedBullets)
        {
            BattleSim sim =
                CreateSim(0x6601UL, pattern, ways, 64, 0);

            StepUntilEnemyBullets(sim);

            Assert.AreEqual(expectedBullets, CountEnemyBullets(sim));
        }

        [Test]
        public void RadialCreatesFullCircleDirections()
        {
            BattleSim sim =
                CreateSim(0x6602UL, BossFirePattern.Radial, 4, 64, 0);
            StepUntilEnemyBullets(sim);
            InputCommand none = InputCommand.None;
            sim.Step(in none);

            int bossX = sim.Boss.X;
            int bossY = sim.Boss.Y;
            bool left = false;
            bool right = false;
            bool up = false;
            bool down = false;
            for (int i = 0; i < sim.Bullets.Count; i++)
            {
                BulletState bullet = sim.Bullets[i];
                if (bullet.Faction != BulletFaction.Enemy)
                    continue;
                left |= bullet.X < bossX;
                right |= bullet.X > bossX;
                up |= bullet.Y > bossY;
                down |= bullet.Y < bossY;
            }

            Assert.IsTrue(left);
            Assert.IsTrue(right);
            Assert.IsTrue(up);
            Assert.IsTrue(down);
        }

        [Test]
        public void SpiralRotatesSuccessiveArmVolleys()
        {
            BattleSim sim =
                CreateSim(0x6603UL, BossFirePattern.Spiral, 1, 64, 0);
            StepUntilEnemyBullets(sim);
            InputCommand none = InputCommand.None;
            sim.Step(in none);
            Assert.AreEqual(2, CountEnemyBullets(sim));
            sim.Step(in none);

            BulletState first = FindEnemyBullet(sim, 0);
            BulletState second = FindEnemyBullet(sim, 1);
            Assert.AreNotEqual(
                first.Y - sim.Boss.Y,
                second.Y - sim.Boss.Y);
        }

        [Test]
        public void WallGapIsDeterministicForSameSeed()
        {
            BattleSim first =
                CreateSim(0x6604UL, BossFirePattern.Wall, 6, 64, 0);
            BattleSim second =
                CreateSim(0x6604UL, BossFirePattern.Wall, 6, 64, 0);

            StepUntilEnemyBullets(first);
            StepUntilEnemyBullets(second);

            Assert.AreEqual(
                CountEnemyBullets(first),
                CountEnemyBullets(second));
            for (int i = 0; i < CountEnemyBullets(first); i++)
            {
                Assert.AreEqual(
                    FindEnemyBullet(first, i).Y,
                    FindEnemyBullet(second, i).Y);
            }
        }

        [Test]
        public void BurstTelegraphsBeforeEveryAimedVolley()
        {
            BattleSim sim =
                CreateSim(0x6605UL, BossFirePattern.Burst, 3, 64, 2);
            InputCommand none = InputCommand.None;
            bool telegraphed = false;
            for (int tick = 0; tick < 256; tick++)
            {
                sim.Step(in none);
                if (HasEvent(
                        sim.EventsThisTick,
                        SimEventType.BossAttackTelegraphed))
                {
                    telegraphed = true;
                    Assert.AreEqual(0, CountEnemyBullets(sim));
                    break;
                }
            }

            Assert.IsTrue(telegraphed);
            for (int tick = 0;
                tick < 3 && CountEnemyBullets(sim) == 0;
                tick++)
            {
                sim.Step(in none);
            }
            Assert.AreEqual(3, CountEnemyBullets(sim));
        }

        [Test]
        public void VolleyTruncationEmitsEnemyBulletCapacityEvent()
        {
            BattleSim sim =
                CreateSim(0x6606UL, BossFirePattern.Radial, 8, 3, 0);
            InputCommand none = InputCommand.None;
            bool capacityEvent = false;
            for (int tick = 0; tick < 256; tick++)
            {
                sim.Step(in none);
                capacityEvent |= HasEvent(
                    sim.EventsThisTick,
                    SimEventType.EnemyBulletCapacityExceeded);
                if (CountEnemyBullets(sim) > 0)
                    break;
            }

            Assert.AreEqual(3, CountEnemyBullets(sim));
            Assert.IsTrue(capacityEvent);
        }

        static BattleSim CreateSim(
            ulong seed,
            BossFirePattern pattern,
            int ways,
            int maxEnemyBullets,
            int telegraphTicks)
        {
            BattleSimConfig config = CreateConfig();
            config.MaxEnemyBullets = maxEnemyBullets;
            var phase = new BossPhase(
                1,
                ways,
                16,
                1,
                BossMovementPattern.Stationary,
                0,
                1,
                1,
                BossPartVulnerability.Legacy,
                0,
                telegraphTicks,
                pattern);
            var segment = new StageSegment(
                "entry",
                1,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                "boss",
                1,
                1,
                1,
                9999,
                1,
                1,
                50,
                new[] { phase });
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                CreateContent(),
                PowerUpGauge.CreateDefault());
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 10,
                MaxBullets = 0,
                PlayerMinX = -80,
                PlayerMaxX = 80,
                PlayerMinY = -80,
                PlayerMaxY = 80,
                BulletDespawnX = 100,
                EnemyDespawnX = -100,
                PlayerSpawnX = 70,
                PlayerSpawnY = 70,
                StartingShieldStock = 0,
                PlayerHitInvulnerabilityTicks = 0,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                EnemyBulletHalfWidth = 0,
                EnemyBulletHalfHeight = 0,
                EnemyBulletDamage = 0,
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
                new WeaponDefinition("shot", 1, 10, 0, 1, 0, 0);
            return new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
        }

        static void StepUntilEnemyBullets(BattleSim sim)
        {
            InputCommand none = InputCommand.None;
            for (int tick = 0; tick < 256; tick++)
            {
                sim.Step(in none);
                if (CountEnemyBullets(sim) > 0)
                    return;
            }
            Assert.Fail("Boss did not fire within the test budget.");
        }

        static int CountEnemyBullets(BattleSim sim)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction == BulletFaction.Enemy)
                    count++;
            return count;
        }

        static BulletState FindEnemyBullet(
            BattleSim sim,
            int enemyIndex)
        {
            int found = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
            {
                if (sim.Bullets[i].Faction != BulletFaction.Enemy)
                    continue;
                if (found == enemyIndex)
                    return sim.Bullets[i];
                found++;
            }
            Assert.Fail("Enemy bullet index was not found.");
            return default;
        }

        static bool HasEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }
    }
}
