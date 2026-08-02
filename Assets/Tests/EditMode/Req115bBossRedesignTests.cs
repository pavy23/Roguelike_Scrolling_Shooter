using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req115bBossRedesignTests
    {
        [Test]
        public void BattleTicksSpawnTrackAndDestroyHeadOnlySegmentChain()
        {
            SegmentChainDefinition chain = ChainDefinition();
            BossPhase phase = Phase(null, chain);
            BattleSim sim = CreateBattle(
                Array.Empty<BossPartDefinition>(),
                new[] { phase },
                1000,
                0x115B01UL);

            bool sawSpawn = AdvanceBossEntry(sim);
            Assert.IsTrue(sawSpawn);
            SimEvent spawnEvent = FindEvent(
                sim,
                SimEventType.SegmentChainSpawned);
            Assert.AreEqual(6, spawnEvent.Arg);
            Assert.AreEqual(6, sim.SegmentChains.Count);
            Assert.IsTrue(sim.SegmentChains[0].IsHead);
            Assert.IsTrue(sim.SegmentChains[0].Damageable);
            Assert.IsFalse(sim.SegmentChains[1].Damageable);
            int chainId = sim.SegmentChains[0].ChainId;
            for (int i = 1; i < sim.SegmentChains.Count; i++)
                Assert.AreEqual(chainId, sim.SegmentChains[i].ChainId);

            int firstHeadX = sim.SegmentChains[0].X;
            int firstHeadY = sim.SegmentChains[0].Y;
            InputCommand none = InputCommand.None;
            sim.Step(in none);
            Assert.Less(sim.SegmentChains[0].X, firstHeadX);
            sim.Step(in none);
            Assert.AreEqual(firstHeadX, sim.SegmentChains[1].X);
            Assert.AreEqual(firstHeadY, sim.SegmentChains[1].Y);
            Assert.AreNotEqual(
                sim.SegmentChains[0].X,
                sim.SegmentChains[5].X);

            var fire = new InputCommand(0, 0, true);
            bool destroyed = false;
            for (int tick = 0; tick < 80 && !destroyed; tick++)
            {
                sim.Step(in fire);
                destroyed = HasEvent(
                    sim,
                    SimEventType.SegmentChainDestroyed);
            }

            Assert.IsTrue(destroyed);
            Assert.AreEqual(
                6,
                FindEvent(
                    sim,
                    SimEventType.SegmentChainDestroyed).Arg);
            Assert.AreEqual(0, sim.SegmentChains.Count);
            Assert.IsFalse(sim.BossDefeated);
        }

        [Test]
        public void BattleTicksRespectPhaseChainSummonCountAndInterval()
        {
            SegmentChainDefinition chain = new SegmentChainDefinition(
                6,
                2,
                3,
                20,
                60,
                60,
                60,
                1,
                1,
                2,
                0,
                -200,
                0,
                SegmentChainDamageRule.HeadOnly);
            BattleSim sim = CreateBattle(
                Array.Empty<BossPartDefinition>(),
                new[] { Phase(null, chain) },
                1000,
                0x115B03UL);
            AdvanceBossEntry(sim);

            Assert.AreEqual(6, sim.SegmentChains.Count);
            InputCommand none = InputCommand.None;
            sim.Step(in none);
            Assert.AreEqual(6, sim.SegmentChains.Count);
            sim.Step(in none);
            Assert.AreEqual(6, sim.SegmentChains.Count);
            sim.Step(in none);
            Assert.AreEqual(12, sim.SegmentChains.Count);
            Assert.IsTrue(HasEvent(
                sim,
                SimEventType.SegmentChainSpawned));
        }

        [Test]
        public void BattleTicksSuctionResistsInputAndPhaseGateEndsField()
        {
            var suction = new BossPartAttackProfile(
                BossPartAttackType.Suction,
                0,
                0,
                0,
                1,
                40,
                1,
                null,
                0,
                null,
                20,
                1);
            BossPartDefinition[] parts =
            {
                new BossPartDefinition(
                    "maw",
                    -100,
                    0,
                    40,
                    40,
                    10,
                    false,
                    null,
                    BossPartAttackProfile.None,
                    0),
                new BossPartDefinition(
                    "heart",
                    0,
                    0,
                    40,
                    40,
                    40,
                    true,
                    null,
                    BossPartAttackProfile.None,
                    0)
            };
            BossPhase[] phases =
            {
                Phase(new[]
                {
                    new BossPhasePartRule(
                        "maw", true, false, suction),
                    new BossPhasePartRule(
                        "heart", true, false)
                }),
                Phase(
                    new[]
                    {
                        new BossPhasePartRule(
                            "maw", false, true),
                        new BossPhasePartRule(
                            "heart", true, false)
                    },
                    null,
                    4,
                    5)
            };
            BattleSim sim = CreateBattle(
                parts,
                phases,
                50,
                0x115B02UL);

            bool sawStart = AdvanceBossEntry(sim);
            Assert.IsTrue(sawStart);
            SimEvent startEvent = FindEvent(
                sim,
                SimEventType.SuctionStarted);
            Assert.AreEqual("maw", startEvent.PartId);
            Assert.AreEqual(sim.BossParts[0].X, startEvent.X);
            Assert.IsTrue(sim.SuctionActive);
            Assert.AreEqual(0, sim.PlayerX);

            var oppose = new InputCommand(-1, 0, false);
            sim.Step(in oppose);
            Assert.AreEqual(-80, sim.PlayerX);
            Assert.Greater(sim.PlayerX, -100);

            var fire = new InputCommand(0, 0, true);
            bool sawPhase = false;
            bool sawEnd = false;
            for (int tick = 0; tick < 30 && !sawEnd; tick++)
            {
                sim.Step(in fire);
                sawPhase |= HasEvent(
                    sim,
                    SimEventType.BossPhaseChanged);
                sawEnd |= HasEvent(
                    sim,
                    SimEventType.SuctionEnded);
            }

            Assert.IsTrue(sawPhase);
            Assert.IsTrue(sawEnd);
            Assert.AreEqual(
                "maw",
                FindEvent(sim, SimEventType.SuctionEnded).PartId);
            Assert.AreEqual(1, sim.Boss.Phase);
            Assert.IsFalse(sim.SuctionActive);
            int before = sim.PlayerX;
            sim.Step(in oppose);
            Assert.AreEqual(before - 100, sim.PlayerX);
        }

        [Test]
        public void SameSeedAndInputsReplayChainAndSuctionStateExactly()
        {
            BossPartAttackProfile suction = new BossPartAttackProfile(
                BossPartAttackType.Suction,
                0,
                0,
                0,
                1,
                12,
                1,
                null,
                0,
                null,
                8,
                1);
            BossPartDefinition[] parts =
            {
                new BossPartDefinition(
                    "maw", -100, 0, 40, 40, 20, false, null,
                    suction, 0),
                new BossPartDefinition(
                    "heart", 0, 200, 40, 40, 80, true, null,
                    BossPartAttackProfile.None, 0)
            };
            BossPhase phase = Phase(null, ChainDefinition());
            BattleSim first = CreateBattle(
                parts,
                new[] { phase },
                100,
                0x115B55UL);
            BattleSim second = CreateBattle(
                parts,
                new[] { phase },
                100,
                0x115B55UL);
            var firstHasher = new DeterminismAuditHasher();
            var secondHasher = new DeterminismAuditHasher();

            for (int tick = 0; tick < 180; tick++)
            {
                InputCommand input = tick % 3 == 0
                    ? new InputCommand(-1, 1, tick > 90)
                    : new InputCommand(1, -1, tick > 90);
                first.Step(in input);
                second.Step(in input);
                firstHasher.FoldBattleState(first);
                secondHasher.FoldBattleState(second);
            }

            Assert.AreEqual(firstHasher.Hash, secondHasher.Hash);
            Assert.AreEqual(first.PlayerX, second.PlayerX);
            Assert.AreEqual(first.PlayerY, second.PlayerY);
            Assert.AreEqual(
                first.SegmentChains.Count,
                second.SegmentChains.Count);
            Assert.AreEqual(first.SuctionActive, second.SuctionActive);
        }

        static SegmentChainDefinition ChainDefinition()
        {
            return new SegmentChainDefinition(
                6,
                1,
                12,
                20,
                60,
                60,
                60,
                1,
                1,
                2,
                0,
                -200,
                0,
                SegmentChainDamageRule.HeadOnly);
        }

        static BossPhase Phase(
            BossPhasePartRule[] rules,
            SegmentChainDefinition chain = null,
            int thresholdNumerator = 0,
            int thresholdDenominator = 1)
        {
            return new BossPhase(
                9999,
                1,
                1,
                1,
                BossMovementPattern.Stationary,
                0,
                1,
                1,
                BossPartVulnerability.All,
                hpThresholdNumerator: thresholdNumerator,
                hpThresholdDenominator: thresholdDenominator,
                partRules: rules,
                segmentChain: chain);
        }

        static BattleSim CreateBattle(
            BossPartDefinition[] parts,
            BossPhase[] phases,
            int bossHp,
            ulong seed)
        {
            var weapon = new WeaponDefinition(
                "req115b_shot",
                10,
                1,
                500,
                1,
                24,
                24);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
            var segment = new StageSegment(
                "entry",
                1,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                "boss_req115b",
                1,
                1,
                1,
                bossHp,
                80,
                80,
                1000,
                phases,
                null,
                null,
                EncounterType.Normal,
                parts);
            return new BattleSim(
                Config(),
                new Rng(seed),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerSpeedNumerator = 100;
            config.PlayerSpeedDenominator = 1;
            config.PlayerMinX = -4000;
            config.PlayerMaxX = 4000;
            config.PlayerMinY = -4000;
            config.PlayerMaxY = 4000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerInvulnerable = true;
            config.FireIntervalTicks = 1;
            config.PlayerBulletSpeedNumerator = 500;
            config.PlayerBulletSpeedDenominator = 1;
            config.MainShotBaseDamage = 10;
            config.MainShotHalfWidth = 24;
            config.MainShotHalfHeight = 24;
            config.BulletDespawnX = 8000;
            config.EnemyDespawnX = -8000;
            config.MaxBullets = 128;
            config.MaxEnemyBullets = 32;
            config.KillComboGaugeGain = 0;
            return config;
        }

        static bool AdvanceBossEntry(BattleSim sim)
        {
            InputCommand none = InputCommand.None;
            bool sawLifecycleEvent = false;
            for (int tick = 0;
                tick < 200
                    && (!sim.BossActive || sim.BossEntering);
                tick++)
            {
                sim.Step(in none);
                sawLifecycleEvent |= HasEvent(
                    sim,
                    SimEventType.SegmentChainSpawned);
                sawLifecycleEvent |= HasEvent(
                    sim,
                    SimEventType.SuctionStarted);
            }
            Assert.IsTrue(sim.BossActive);
            Assert.IsFalse(sim.BossEntering);
            return sawLifecycleEvent;
        }

        static bool HasEvent(BattleSim sim, SimEventType type)
        {
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        static SimEvent FindEvent(BattleSim sim, SimEventType type)
        {
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return events[i];
            Assert.Fail($"Expected event {type}.");
            return default;
        }
    }
}
