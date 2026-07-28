using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-005 요청 1: 시뮬 이벤트 버스. Presentation이 상태 차분 대신 구독하는
    /// 이산 사건 스트림이 결정론적이고 순서가 고정임을 증명한다.
    /// </summary>
    [TestFixture]
    public class BattleSimEventTests
    {
        [Test]
        public void EnemyKillAndCapsuleFlow_EmitsOrderedEvents()
        {
            EnemyDefinition enemy = Enemy("dropper", EnemyMovePattern.Static, hp: 10, dropWeight: 1);
            BattleContent content = Content(new WeaponDefinition("shot", 10, 1, 2, 1, 0, 0), enemy);
            StagePlan plan = Plan(Segment("drop", 20, new SpawnEvent(0, enemy.Id, 4, 0)));
            BattleSimConfig config = CreateConfig();
            config.CapsuleNoDropWeight = 0;
            var gauge = PowerUpGauge.CreateDefault();
            var sim = new BattleSim(config, new Rng(3UL), plan, content, gauge);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            SimEvent[] fireTick = sim.EventsThisTick.ToArray();
            Assert.AreEqual(1, fireTick.Length);
            Assert.AreEqual(SimEventType.PlayerFired, fireTick[0].Type);
            Assert.AreEqual((int)BulletKind.MainShot, fireTick[0].Arg);
            sim.Step(in none);
            sim.Step(in none);

            SimEvent[] killTick = sim.EventsThisTick.ToArray();
            Assert.AreEqual(2, killTick.Length);
            Assert.AreEqual(SimEventType.EnemyKilled, killTick[0].Type);
            Assert.AreEqual(4, killTick[0].X);
            Assert.AreEqual(10, killTick[0].Arg);
            Assert.AreEqual(SimEventType.CapsuleDropped, killTick[1].Type);
            Assert.AreEqual(4, killTick[1].X);

            var moveRight = new InputCommand(1, 0, false);
            sim.Step(in moveRight);
            sim.Step(in moveRight);

            SimEvent[] pickTick = sim.EventsThisTick.ToArray();
            Assert.AreEqual(1, pickTick.Length);
            Assert.AreEqual(SimEventType.CapsulePicked, pickTick[0].Type);
            Assert.AreEqual(killTick[1].EntityId, pickTick[0].EntityId);
        }

        [Test]
        public void EnemyHit_WhenSurviving_EmitsHitWithDamage()
        {
            EnemyDefinition enemy = Enemy("tanky", EnemyMovePattern.Static, hp: 100);
            BattleContent content = Content(new WeaponDefinition("shot", 10, 1, 2, 1, 0, 0), enemy);
            StagePlan plan = Plan(Segment("hit", 20, new SpawnEvent(0, enemy.Id, 4, 0)));
            var sim = CreateSim(plan, content, CreateConfig(), 3UL);
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;

            sim.Step(in fire);
            sim.Step(in none);
            sim.Step(in none);

            SimEvent[] events = sim.EventsThisTick.ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(SimEventType.EnemyHit, events[0].Type);
            Assert.AreEqual(10, events[0].Arg);
            Assert.AreEqual(1, sim.Enemies.Count);
        }

        [Test]
        public void PlayerContact_EmitsPlayerHitAndPlayerKilledAtZeroHp()
        {
            EnemyDefinition enemy = Enemy("rammer", EnemyMovePattern.Static, contactDamage: 2);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment("contact", 10, new SpawnEvent(1, enemy.Id, 0, 0)));
            BattleSimConfig config = CreateConfig();
            config.PlayerMaxHp = 2;
            var sim = CreateSim(plan, content, config, 4UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            SimEvent[] events = sim.EventsThisTick.ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(SimEventType.PlayerHit, events[0].Type);
            Assert.AreEqual(2, events[0].Arg);
            Assert.AreEqual(SimEventType.PlayerKilled, events[1].Type);
            Assert.AreEqual(0, sim.PlayerHp);
        }

        [Test]
        public void PowerUpActivation_EmitsLevelChangedNextStep()
        {
            EnemyDefinition enemy = Enemy("bystander", EnemyMovePattern.Static);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment("idle", 100));
            var gauge = PowerUpGauge.CreateDefault();
            var sim = new BattleSim(CreateConfig(), new Rng(5UL), plan, content, gauge);
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            Assert.AreEqual(0, sim.EventsThisTick.Length);

            gauge.Collect();
            Assert.IsTrue(gauge.Activate());
            sim.Step(in none);

            SimEvent[] events = sim.EventsThisTick.ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(SimEventType.PowerUpLevelChanged, events[0].Type);
            Assert.AreEqual((int)PowerUpSlot.MainShot, events[0].EntityId);
            Assert.AreEqual(1, events[0].Arg);
        }

        [Test]
        public void ConstructionWithCarriedLevels_EmitsNothing()
        {
            EnemyDefinition enemy = Enemy("bystander", EnemyMovePattern.Static);
            BattleContent content = Content(enemy);
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 2, 1, 1 });
            var sim = new BattleSim(
                CreateConfig(), new Rng(6UL), Plan(Segment("idle", 100)), content, gauge);

            Assert.AreEqual(0, sim.EventsThisTick.Length);
        }

        [Test]
        public void EventsAreClearedAtEachStep()
        {
            EnemyDefinition enemy = Enemy("rammer", EnemyMovePattern.Static, contactDamage: 1);
            BattleContent content = Content(enemy);
            StagePlan plan = Plan(Segment("contact", 10, new SpawnEvent(1, enemy.Id, 0, 0)));
            BattleSimConfig config = CreateConfig();
            config.PlayerMaxHp = 5;
            var sim = CreateSim(plan, content, config, 4UL);
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            Assert.Greater(sim.EventsThisTick.Length, 0);
            sim.Step(in none);
            Assert.AreEqual(0, sim.EventsThisTick.Length);
        }

        [Test]
        public void SameSeedAndInputs_ProduceIdenticalEventSequences()
        {
            SimEvent[][] first = RunEventScript(seed: 11UL);
            SimEvent[][] second = RunEventScript(seed: 11UL);

            Assert.AreEqual(first.Length, second.Length);
            for (int tick = 0; tick < first.Length; tick++)
            {
                Assert.AreEqual(first[tick].Length, second[tick].Length, $"tick {tick}");
                for (int i = 0; i < first[tick].Length; i++)
                {
                    Assert.AreEqual(first[tick][i].Type, second[tick][i].Type, $"tick {tick} event {i}");
                    Assert.AreEqual(first[tick][i].EntityId, second[tick][i].EntityId, $"tick {tick} event {i}");
                    Assert.AreEqual(first[tick][i].X, second[tick][i].X, $"tick {tick} event {i}");
                    Assert.AreEqual(first[tick][i].Y, second[tick][i].Y, $"tick {tick} event {i}");
                    Assert.AreEqual(first[tick][i].Arg, second[tick][i].Arg, $"tick {tick} event {i}");
                }
            }
        }

        static SimEvent[][] RunEventScript(ulong seed)
        {
            EnemyDefinition zako = Enemy("zako", EnemyMovePattern.Straight, hp: 10, speedNumerator: 2, dropWeight: 3);
            BattleContent content = Content(new WeaponDefinition("shot", 10, 1, 3, 1, 0, 0), zako);
            StagePlan plan = Plan(Segment(
                "script",
                200,
                new SpawnEvent(0, zako.Id, 40, 0),
                new SpawnEvent(10, zako.Id, 60, 0),
                new SpawnEvent(20, zako.Id, 80, 0)));
            BattleSimConfig config = CreateConfig();
            config.CapsuleNoDropWeight = 2;
            var sim = CreateSim(plan, content, config, seed);

            var log = new List<SimEvent[]>();
            for (int tick = 0; tick < 120; tick++)
            {
                var input = new InputCommand(tick % 3 == 0 ? 1 : 0, 0, fire: true);
                sim.Step(in input);
                log.Add(sim.EventsThisTick.ToArray());
            }
            return log.ToArray();
        }

        static BattleSim CreateSim(StagePlan plan, BattleContent content, BattleSimConfig config, ulong seed)
        {
            return new BattleSim(config, new Rng(seed), plan, content, PowerUpGauge.CreateDefault());
        }

        static BattleContent Content(params EnemyDefinition[] enemies)
        {
            return Content(new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0), enemies);
        }

        static BattleContent Content(WeaponDefinition weapon, params EnemyDefinition[] enemies)
        {
            return new BattleContent(enemies, new[] { weapon }, weapon.Id);
        }

        static EnemyDefinition Enemy(
            string id,
            EnemyMovePattern pattern,
            int hp = 1,
            int contactDamage = 0,
            int speedNumerator = 0,
            int speedDenominator = 1,
            int dropWeight = 0,
            int sineAmplitude = 0,
            int sinePeriodTicks = 64)
        {
            return new EnemyDefinition(
                id, hp, contactDamage, pattern, speedNumerator, speedDenominator,
                0, 0, dropWeight, sineAmplitude, sinePeriodTicks);
        }

        static StageSegment Segment(string id, int lengthTicks, params SpawnEvent[] spawns)
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
                PlayerSpeedPerTick = 2,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 1000,
                EnemyDespawnX = -1000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 5,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }
    }
}
