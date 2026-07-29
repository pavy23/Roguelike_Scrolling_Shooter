using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class RunManagerAllocationTests
    {
        const int WarmupTicks = 100;
        const int MeasuredTicks = 600;

        [Test]
        public void StepAllocatesNoManagedMemoryAcrossCombatBossAndReward()
        {
            RunManager run = CreateRun();
            InputCommand fire = new InputCommand(0, 0, true);

            GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < WarmupTicks; i++)
                run.Step(in fire);

            bool sawCombat = false;
            bool sawBoss = false;
            bool sawReward = false;
            var allocationsByTick = new long[MeasuredTicks];
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < MeasuredTicks; i++)
            {
                long tickBefore = GC.GetAllocatedBytesForCurrentThread();
                run.Step(in fire);
                allocationsByTick[i] =
                    GC.GetAllocatedBytesForCurrentThread() - tickBefore;

                BattleSim battle = (BattleSim)run.Battle;
                sawCombat |= battle.Enemies.Count > 0;
                sawBoss |= battle.BossActive;
                sawReward |= run.State == RunState.AwaitingReward;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i < allocationsByTick.Length; i++)
            {
                if (allocationsByTick[i] != 0)
                {
                    TestContext.WriteLine(
                        $"Measured tick {i}: {allocationsByTick[i]} bytes");
                }
            }

            Assert.Multiple(() =>
            {
                Assert.IsTrue(sawCombat, "The measured window did not exercise enemy combat.");
                Assert.IsTrue(sawBoss, "The measured window did not exercise the boss interval.");
                Assert.IsTrue(sawReward, "The measured window did not reach reward selection.");
                Assert.AreEqual(
                    0L,
                    allocated,
                    "RunManager.Step allocated managed heap memory during the measured window.");
            });
        }

        [Test]
        public void StepAllocatesNoManagedMemoryWhenGrazeIsScored()
        {
            InputCommand none = InputCommand.None;
            BattleSim warmup = CreateGrazeBattle();
            warmup.Step(in none);

            BattleSim measured = CreateGrazeBattle();
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            measured.Step(in none);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Multiple(() =>
            {
                Assert.AreEqual(1L, measured.Statistics.GrazeCount);
                Assert.AreEqual(
                    0L,
                    allocated,
                    "Scoring a graze allocated managed heap memory.");
            });
        }

        static RunManager CreateRun()
        {
            EnemyDefinition enemy = new EnemyDefinition(
                "guard_enemy",
                "Guard Enemy",
                20,
                0,
                10,
                EnemyMovePattern.Static,
                0,
                1,
                30,
                64,
                64,
                0,
                0,
                1,
                64);
            WeaponDefinition weapon = new WeaponDefinition(
                "guard_shot",
                10,
                5,
                96,
                1,
                8,
                8);
            BattleContent content = new BattleContent(
                new[] { enemy },
                new[] { weapon },
                weapon.Id);
            StagePlan plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "guard_segment",
                        120,
                        new[]
                        {
                            new SpawnEvent(105, enemy.Id, 600, 300),
                            new SpawnEvent(110, enemy.Id, 700, -300)
                        },
                        1,
                        1,
                        new[] { 1 })
                },
                "guard_boss",
                1,
                1,
                1,
                30,
                256,
                256,
                300,
                new[] { new BossPhase(20, 3, 64, 1) });
            BattleSimConfig config = CreateConfig();

            return new RunManager(
                0xC0DEC0DEUL,
                new FixedStageGenerator(plan),
                config,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleSim CreateGrazeBattle()
        {
            EnemyDefinition turret = new EnemyDefinition(
                "graze_turret",
                "Graze Turret",
                100,
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
            WeaponDefinition weapon = new WeaponDefinition(
                "graze_shot",
                1,
                1,
                1,
                1,
                0,
                0);
            BattleContent content = new BattleContent(
                new[] { turret },
                new[] { weapon },
                weapon.Id);
            StagePlan plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "graze_segment",
                        100,
                        new[] { new SpawnEvent(0, turret.Id, 0, 128) },
                        1,
                        1,
                        new[] { 1 })
                },
                "none",
                1,
                1,
                1);
            BattleSimConfig config = CreateConfig();
            config.EnemyBulletSpeedNumerator = 0;
            config.EnemyBulletHalfWidth = 0;
            config.EnemyBulletHalfHeight = 0;
            config.MaxEnemyBullets = 1;
            config.GrazeExtraRadiusSubUnits = 128;

            return new BattleSim(
                config,
                new Rng(15UL),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 2,
                PlayerBulletSpeedPerTick = 96,
                FireIntervalTicks = 5,
                MaxBullets = 64,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 2000,
                EnemyDespawnX = -2000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 1000,
                PlayerHalfWidth = 16,
                PlayerHalfHeight = 16,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 1,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                EnemyBulletSpeedNumerator = 64,
                EnemyBulletSpeedDenominator = 1,
                EnemyBulletHalfWidth = 8,
                EnemyBulletHalfHeight = 8,
                EnemyBulletDamage = 0,
                MaxEnemyBullets = 32
            };
        }

        sealed class FixedStageGenerator : IStageGenerator
        {
            readonly StagePlan _plan;

            public FixedStageGenerator(StagePlan plan)
            {
                _plan = plan;
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                return _plan;
            }
        }
    }
}
