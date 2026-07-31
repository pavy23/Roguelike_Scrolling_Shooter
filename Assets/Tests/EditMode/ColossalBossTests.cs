using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class ColossalBossTests
    {
        [Test]
        public void PartsTakeIndependentDamageAndCoreGateRequiresPredecessor()
        {
            BossPartDefinition gate = Part(
                "shield", 0, 400, 10, false, null);
            BossPartDefinition core = Part(
                "core", 0, 0, 20, true, new[] { "shield" });
            BattleSim sim = CreateBattle(
                new[] { gate, core },
                Array.Empty<EnemyDefinition>());
            AdvanceBossEntry(sim);

            FireUntilPartChanges(sim, 1);
            Assert.AreEqual(20, sim.BossParts[1].Hp);
            Assert.IsTrue(sim.BossParts[1].CoreGated);

            MovePlayerTo(sim, 400);
            FireUntilDestroyed(sim, 0);
            Assert.IsTrue(sim.BossParts[0].Destroyed);
            Assert.IsFalse(sim.BossParts[1].CoreGated);
            Assert.AreEqual(
                1,
                CountEvents(
                    sim,
                    SimEventType.BossPartDestroyed,
                    "shield"));

            MovePlayerTo(sim, 0);
            FireUntilDestroyed(sim, 1);
            Assert.AreEqual(
                1,
                CountEvents(
                    sim,
                    SimEventType.BossPartDestroyed,
                    "core"));
            Assert.IsTrue(sim.BossDefeated);
        }

        [Test]
        public void PhaseCanOpenPartsAndMultipartDamageEmitsBothHpPhaseEvents()
        {
            BossPartDefinition wing = Part(
                "wing", 0, 400, 10, false, null);
            BossPartDefinition core = Part(
                "core", 0, 0, 20, true, null);
            var phases = new[]
            {
                new BossPhase(
                    9999,
                    1,
                    1,
                    1,
                    BossMovementPattern.Stationary,
                    0,
                    1,
                    1,
                    BossPartVulnerability.CoreOnly),
                new BossPhase(
                    9999,
                    2,
                    2,
                    1,
                    BossMovementPattern.VerticalSine,
                    100,
                    1,
                    8,
                    BossPartVulnerability.All),
                new BossPhase(
                    9999,
                    4,
                    4,
                    1,
                    BossMovementPattern.VerticalSine,
                    200,
                    1,
                    4,
                    BossPartVulnerability.All)
            };
            BattleSim sim = CreateBattleWithPhases(
                new[] { wing, core },
                phases,
                10);
            AdvanceBossEntry(sim);

            Assert.IsTrue(sim.BossParts[0].Invulnerable);
            FireUntilPartChanges(sim, 1);
            Assert.AreEqual(1, FindPhaseChangedArg(sim));
            Assert.AreEqual(1, sim.Boss.Phase);
            Assert.AreEqual(
                BossMovementPattern.VerticalSine,
                sim.Boss.MovementPattern);
            Assert.IsFalse(sim.BossParts[0].Invulnerable);

            FireUntilDestroyed(sim, 1);
            Assert.AreEqual(2, FindPhaseChangedArg(sim));
            Assert.AreEqual(2, sim.Boss.Phase);
            Assert.IsTrue(sim.BossDefeated);
        }

        [Test]
        public void DestroyedPartStopsAttackAndRegeneratesAtExactTick()
        {
            BossPartAttackProfile attack =
                new BossPartAttackProfile(
                    BossPartAttackType.AimedSpread,
                    2,
                    1,
                    30,
                    1,
                    0,
                    1,
                    null);
            BossPartDefinition tentacle = new BossPartDefinition(
                "tentacle_left",
                0,
                0,
                100,
                100,
                10,
                false,
                null,
                attack,
                5);
            BossPartDefinition core = Part(
                "heart", 0, 500, 20, true, null);
            BattleSim sim = CreateBattle(
                new[] { tentacle, core },
                Array.Empty<EnemyDefinition>());
            AdvanceBossEntry(sim);

            InputCommand none = InputCommand.None;
            sim.Step(in none);
            sim.Step(in none);
            int bulletsWhileAlive = CountEnemyBullets(sim);
            Assert.Greater(bulletsWhileAlive, 0);

            FireUntilDestroyed(sim, 0);
            int bulletsAtDestruction = CountEnemyBullets(sim);
            for (int tick = 0; tick < 4; tick++)
            {
                sim.Step(in none);
                Assert.IsTrue(sim.BossParts[0].Destroyed);
                Assert.LessOrEqual(
                    CountEnemyBullets(sim),
                    bulletsAtDestruction);
            }

            sim.Step(in none);
            Assert.IsFalse(sim.BossParts[0].Destroyed);
            Assert.AreEqual(10, sim.BossParts[0].Hp);
            Assert.AreEqual(
                1,
                CountEvents(
                    sim,
                    SimEventType.BossPartRegenerated,
                    "tentacle_left"));
        }

        [Test]
        public void BroodSpawnUsesEnemyPathAndHonorsSharedCap()
        {
            EnemyDefinition minion = Enemy("brood_minion");
            BossPartAttackProfile spawn =
                new BossPartAttackProfile(
                    BossPartAttackType.SpawnEnemy,
                    1,
                    0,
                    0,
                    1,
                    0,
                    1,
                    minion.Id);
            BossPartDefinition sac = new BossPartDefinition(
                "spawn_sac",
                0,
                0,
                100,
                100,
                10,
                false,
                null,
                spawn,
                0);
            BossPartDefinition core = Part(
                "heart", 0, 500, 20, true, null);
            BattleSimConfig config = Config();
            config.MaxEnemies = 2;
            config.BulletDespawnX = 0;
            BattleSim sim = CreateBattle(
                new[] { sac, core },
                new[] { minion },
                config);
            InputCommand none = InputCommand.None;

            for (int tick = 0;
                tick < 200 && (!sim.BossActive || sim.BossEntering);
                tick++)
                sim.Step(in none);
            for (int tick = 0; tick < 20; tick++)
                sim.Step(in none);

            Assert.AreEqual(2, sim.Enemies.Count);
            Assert.AreEqual("brood_minion", sim.Enemies[0].DefinitionId);
            Assert.AreEqual("brood_minion", sim.Enemies[1].DefinitionId);
        }

        [Test]
        public void SuctionUsesExactRationalSpeedAndIsDeterministic()
        {
            BossPartAttackProfile suction =
                new BossPartAttackProfile(
                    BossPartAttackType.Suction,
                    0,
                    0,
                    0,
                    1,
                    3,
                    2,
                    null);
            BossPartDefinition maw = new BossPartDefinition(
                "maw",
                0,
                0,
                100,
                100,
                10,
                false,
                null,
                suction,
                0);
            BossPartDefinition core = Part(
                "heart", 0, 500, 20, true, null);
            BattleSimConfig config = Config();
            config.BulletDespawnX = 0;
            config.PlayerMinX = -1000;
            config.PlayerMaxX = 1000;
            BattleSim first = CreateBattle(
                new[] { maw, core },
                Array.Empty<EnemyDefinition>(),
                config);
            BattleSim second = CreateBattle(
                new[] { maw, core },
                Array.Empty<EnemyDefinition>(),
                config);
            InputCommand none = InputCommand.None;

            AdvanceBossEntry(first);
            AdvanceBossEntry(second);
            Assert.AreEqual(0, first.PlayerX);
            first.Step(in none);
            second.Step(in none);
            Assert.AreEqual(1, first.PlayerX);
            first.Step(in none);
            second.Step(in none);
            Assert.AreEqual(3, first.PlayerX);
            Assert.AreEqual(first.PlayerX, second.PlayerX);
            Assert.AreEqual(first.PlayerY, second.PlayerY);
        }

        [Test]
        public void MeleeChargeHitsOncePerCycleAndStopsWhenDestroyed()
        {
            var melee = new BossPartAttackProfile(
                BossPartAttackType.MeleeCharge,
                8,
                0,
                0,
                1,
                1,
                1,
                null,
                3);
            var claw = new BossPartDefinition(
                "claw",
                -300,
                0,
                100,
                100,
                10,
                false,
                null,
                melee,
                0);
            BossPartDefinition core = Part(
                "core", 0, 500, 20, true, null);
            BattleSim sim = CreateBattle(
                new[] { claw, core },
                Array.Empty<EnemyDefinition>());
            AdvanceBossEntry(sim);
            InputCommand none = InputCommand.None;

            int startingStock = sim.ShieldStock;
            for (int tick = 0; tick < 8; tick++)
                sim.Step(in none);
            Assert.AreEqual(startingStock - 1, sim.ShieldStock);

            FireUntilDestroyed(sim, 0);
            int stockAfterDestruction = sim.ShieldStock;
            for (int tick = 0; tick < 16; tick++)
                sim.Step(in none);
            Assert.AreEqual(stockAfterDestruction, sim.ShieldStock);
        }

        [Test]
        public void MultipartBossStepAllocatesNoManagedMemory()
        {
            EnemyDefinition minion = Enemy("allocation_minion");
            BossPartAttackProfile spawn =
                new BossPartAttackProfile(
                    BossPartAttackType.SpawnEnemy,
                    1,
                    0,
                    0,
                    1,
                    0,
                    1,
                    minion.Id);
            BossPartDefinition sac = new BossPartDefinition(
                "sac",
                0,
                0,
                100,
                100,
                10,
                false,
                null,
                spawn,
                0);
            BossPartDefinition core = Part(
                "core", 0, 500, 20, true, new[] { "sac" });
            BattleSimConfig config = Config();
            config.BulletDespawnX = 0;
            config.MaxEnemies = 2;
            BattleSim warmup = CreateBattle(
                new[] { sac, core },
                new[] { minion },
                config);
            InputCommand none = InputCommand.None;
            for (int tick = 0; tick < 10; tick++)
                warmup.Step(in none);

            BattleSim measured = CreateBattle(
                new[] { sac, core },
                new[] { minion },
                config);
            measured.Step(in none);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 100; tick++)
                measured.Step(in none);
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        static BossPartDefinition Part(
            string id,
            int offsetX,
            int offsetY,
            int hp,
            bool core,
            IReadOnlyList<string> gates)
        {
            return new BossPartDefinition(
                id,
                offsetX,
                offsetY,
                100,
                100,
                hp,
                core,
                gates,
                BossPartAttackProfile.None,
                0);
        }

        static EnemyDefinition Enemy(string id)
        {
            return new EnemyDefinition(
                id,
                id,
                10,
                0,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                20,
                20,
                0,
                0,
                1,
                60);
        }

        static BattleSim CreateBattle(
            BossPartDefinition[] parts,
            EnemyDefinition[] enemies,
            BattleSimConfig config = null)
        {
            config = config ?? Config();
            var weapon = new WeaponDefinition(
                "part_test_shot",
                10,
                1,
                100,
                1,
                10,
                10);
            var content = new BattleContent(
                enemies,
                new[] { weapon },
                weapon.Id);
            int totalHp = 0;
            for (int i = 0; i < parts.Length; i++)
                totalHp += parts[i].MaxHp;
            var plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "entry",
                        1,
                        Array.Empty<SpawnEvent>(),
                        1,
                        1,
                        new[] { 1 })
                },
                "multipart_test",
                1,
                1,
                1,
                totalHp,
                200,
                600,
                300,
                new[] { new BossPhase(9999, 1, 1, 1) },
                null,
                null,
                EncounterType.Normal,
                parts);
            return new BattleSim(
                config,
                new Rng(123UL),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleSim CreateBattleWithPhases(
            BossPartDefinition[] parts,
            IReadOnlyList<BossPhase> phases,
            int weaponDamage)
        {
            BattleSimConfig config = Config();
            var weapon = new WeaponDefinition(
                "phase_part_test_shot",
                weaponDamage,
                1,
                100,
                1,
                10,
                10);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
            int totalHp = 0;
            for (int i = 0; i < parts.Length; i++)
                totalHp += parts[i].MaxHp;
            var plan = new StagePlan(
                new[]
                {
                    new StageSegment(
                        "entry",
                        1,
                        Array.Empty<SpawnEvent>(),
                        1,
                        1,
                        new[] { 1 })
                },
                "phase_multipart_test",
                1,
                1,
                1,
                totalHp,
                200,
                600,
                300,
                phases,
                null,
                null,
                EncounterType.Normal,
                parts);
            return new BattleSim(
                config,
                new Rng(124UL),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerSpeedPerTick = 100;
            config.PlayerMinX = 0;
            config.PlayerMaxX = 0;
            config.PlayerMinY = -1000;
            config.PlayerMaxY = 1000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMaxHp = 999;
            config.MainShotBaseDamage = 10;
            config.FireIntervalTicks = 1;
            config.PlayerBulletSpeedPerTick = 100;
            config.MainShotHalfWidth = 10;
            config.MainShotHalfHeight = 10;
            config.BulletDespawnX = 1000;
            config.EnemyDespawnX = -10000;
            config.MaxBullets = 64;
            config.MaxEnemyBullets = 128;
            return config;
        }

        static void AdvanceBossEntry(BattleSim sim)
        {
            InputCommand none = InputCommand.None;
            for (int tick = 0;
                tick < 200
                    && (!sim.BossActive || sim.Boss.X != 300);
                tick++)
                sim.Step(in none);
            Assert.IsTrue(sim.BossActive);
            Assert.AreEqual(300, sim.Boss.X);
        }

        static void MovePlayerTo(BattleSim sim, int y)
        {
            int direction = y.CompareTo(sim.PlayerY);
            var move = new InputCommand(0, direction, false);
            for (int tick = 0;
                tick < 30 && sim.PlayerY != y;
                tick++)
                sim.Step(in move);
            Assert.AreEqual(y, sim.PlayerY);
        }

        static void FireUntilPartChanges(
            BattleSim sim,
            int partIndex)
        {
            int hp = sim.BossParts[partIndex].Hp;
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            for (int tick = 0;
                tick < 30
                    && sim.BossParts[partIndex].Hp == hp;
                tick++)
            {
                if (tick == 0)
                    sim.Step(in fire);
                else
                    sim.Step(in none);
            }
        }

        static void FireUntilDestroyed(
            BattleSim sim,
            int partIndex)
        {
            var fire = new InputCommand(0, 0, true);
            for (int tick = 0;
                tick < 30
                    && !sim.BossParts[partIndex].Destroyed;
                tick++)
                sim.Step(in fire);
            Assert.IsTrue(sim.BossParts[partIndex].Destroyed);
        }

        static int CountEnemyBullets(BattleSim sim)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction
                    == BulletFaction.Enemy)
                    count++;
            return count;
        }

        static int CountEvents(
            BattleSim sim,
            SimEventType type,
            string partId)
        {
            int count = 0;
            ReadOnlySpan<SimEvent> events =
                sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type
                    && events[i].PartId == partId)
                    count++;
            return count;
        }

        static int FindPhaseChangedArg(BattleSim sim)
        {
            ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == SimEventType.BossPhaseChanged)
                    return events[i].Arg;
            Assert.Fail("Expected BossPhaseChanged in the current tick.");
            return -1;
        }
    }
}
