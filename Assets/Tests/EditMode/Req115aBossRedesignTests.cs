using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req115aBossRedesignTests
    {
        [Test]
        public void BattleSimTicksExplicitPhaseGateAndFiresExposedRailgunPart()
        {
            LaserAttackDefinition railBeam = RailBeam();
            var railAttack = new BossPartAttackProfile(
                BossPartAttackType.Laser,
                railBeam.CycleIntervalTicks,
                0,
                0,
                1,
                0,
                1,
                null,
                0,
                railBeam);
            BossPartDefinition[] parts =
            {
                Part("armor", 0, 0, 20, false),
                Part("railgun", 0, -400, 10, false),
                Part("core", 0, 400, 10, true)
            };
            BossPhase[] phases =
            {
                Phase(
                    new[]
                    {
                        new BossPhasePartRule(
                            "railgun", false, true),
                        new BossPhasePartRule(
                            "core", true, true)
                    }),
                Phase(
                    new[]
                    {
                        new BossPhasePartRule(
                            "armor", false, true),
                        new BossPhasePartRule(
                            "railgun", true, false, railAttack),
                        new BossPhasePartRule(
                            "core", true, false)
                    },
                    1,
                    2)
            };
            BattleSim sim = CreateBattle(parts, phases, null, 40, 115UL);
            AdvanceBossEntry(sim);

            Assert.IsFalse(sim.BossParts[1].Active);
            Assert.IsTrue(sim.BossParts[1].Invulnerable);
            bool phaseEvent = FireUntilPhase(sim, 1);

            Assert.IsTrue(phaseEvent);
            Assert.AreEqual(1, sim.Boss.Phase);
            Assert.AreEqual(20, sim.Boss.Hp);
            Assert.IsFalse(sim.BossParts[0].Active);
            Assert.IsTrue(sim.BossParts[1].Active);
            Assert.IsFalse(sim.BossParts[1].Invulnerable);

            InputCommand none = InputCommand.None;
            LaserState observed = default;
            bool sawPartLaser = false;
            bool sawFullWidth = false;
            for (int tick = 0; tick < 40 && !sawFullWidth; tick++)
            {
                sim.Step(in none);
                for (int i = 0; i < sim.Lasers.Count; i++)
                {
                    LaserState laser = sim.Lasers[i];
                    if (laser.SourceKind != LaserSourceKind.BossPart)
                        continue;
                    observed = laser;
                    sawPartLaser = true;
                    if (laser.HalfWidth == railBeam.FullHalfWidth)
                        sawFullWidth = true;
                }
            }

            Assert.IsTrue(sawPartLaser);
            Assert.AreEqual(1, observed.SourceEntityId);
            Assert.IsTrue(sawFullWidth);
            Assert.AreEqual(8 * SimSpace.SubUnitsPerWorldUnit, observed.HalfWidth);
        }

        [Test]
        public void BattleSimTicksFormTransitionScoresEachBodyAndClearsOnlyFinalForm()
        {
            BattleSim sim = CreateTwoFormBattle(0x115AUL);
            AdvanceBossEntry(sim);
            int firstId = sim.Boss.Id;
            bool transitionStarted = FireUntilTransition(sim);

            Assert.IsTrue(transitionStarted);
            Assert.IsTrue(sim.BossTransitioning);
            Assert.AreEqual(4, sim.BossTransitionTicksRemaining);
            Assert.IsFalse(sim.BossActive);
            Assert.IsFalse(sim.BossDefeated);
            Assert.AreEqual(0, sim.BossFormIndex);
            Assert.AreEqual(20L, sim.Score);
            Assert.IsFalse(HasEvent(sim, SimEventType.StageCleared));
            Assert.IsTrue(HasEvent(sim, SimEventType.EnemyKilled));
            Assert.IsTrue(HasEvent(
                sim,
                SimEventType.BossFormTransitionStarted));
            Assert.AreEqual(
                "boss_prism",
                FindEvent(sim, SimEventType.BossFormTransitionStarted).PartId);

            InputCommand none = InputCommand.None;
            for (int remaining = 3; remaining >= 1; remaining--)
            {
                sim.Step(in none);
                Assert.IsTrue(sim.BossTransitioning);
                Assert.AreEqual(
                    remaining,
                    sim.BossTransitionTicksRemaining);
                Assert.IsFalse(sim.BossActive);
            }
            sim.Step(in none);

            Assert.IsFalse(sim.BossTransitioning);
            Assert.AreEqual(1, sim.BossFormIndex);
            Assert.AreEqual(1, sim.Boss.FormIndex);
            Assert.IsTrue(sim.BossActive);
            Assert.AreNotEqual(firstId, sim.Boss.Id);
            Assert.AreEqual(20, sim.Boss.MaxHp);
            Assert.IsTrue(HasEvent(sim, SimEventType.BossFormChanged));
            Assert.IsTrue(HasEvent(sim, SimEventType.BossSpawned));
            Assert.IsFalse(HasEvent(sim, SimEventType.StageCleared));

            bool finalClear = FireUntilFinalClear(sim);
            Assert.IsTrue(finalClear);
            Assert.IsTrue(sim.BossDefeated);
            Assert.AreEqual(60L, sim.Score);
            Assert.IsTrue(HasEvent(sim, SimEventType.EnemyKilled));
            Assert.IsTrue(HasEvent(sim, SimEventType.StageCleared));
        }

        [Test]
        public void RecordedInputsReplayPhaseAndFormIntermediateStateExactly()
        {
            const ulong seed = 0x115A55UL;
            BattleSim recorded = CreateTwoFormBattle(seed);
            var recorder = new InputRecorder(64);
            var recordedHasher = new DeterminismAuditHasher();
            bool sawTransition = false;
            bool sawForm2 = false;

            for (int tick = 0; tick < 180; tick++)
            {
                InputCommand input = recorded.BossActive
                    && !recorded.BossEntering
                    ? new InputCommand(0, 0, true)
                    : InputCommand.None;
                recorder.Record(in input);
                recorded.Step(in input);
                recordedHasher.FoldBattleState(recorded);
                sawTransition |= recorded.BossTransitioning;
                sawForm2 |= recorded.BossFormIndex == 1;
            }

            Assert.IsTrue(sawTransition);
            Assert.IsTrue(sawForm2);
            Assert.IsTrue(recorded.BossDefeated);

            BattleSim replayed = CreateTwoFormBattle(seed);
            var replayedHasher = new DeterminismAuditHasher();
            int replayedTicks = 0;
            foreach (InputCommand input in
                new InputPlayback(recorder.Export()))
            {
                replayed.Step(in input);
                replayedHasher.FoldBattleState(replayed);
                replayedTicks++;
            }

            Assert.AreEqual(180, replayedTicks);
            Assert.AreEqual(recordedHasher.Hash, replayedHasher.Hash);
            Assert.AreEqual(recorded.BossFormIndex, replayed.BossFormIndex);
            Assert.AreEqual(
                recorded.BossTransitionTicksRemaining,
                replayed.BossTransitionTicksRemaining);
            Assert.AreEqual(recorded.BossDefeated, replayed.BossDefeated);
            Assert.AreEqual(recorded.Score, replayed.Score);
        }

        static BattleSim CreateTwoFormBattle(ulong seed)
        {
            BossPhase[] phases = { Phase(null) };
            var form2 = new BossFormDefinition(
                "boss_prism",
                4,
                20,
                300,
                200,
                300,
                new[]
                {
                    new BossPhase(
                        9999,
                        5,
                        1,
                        1,
                        BossMovementPattern.Stationary,
                        0,
                        1,
                        1,
                        BossPartVulnerability.All,
                        firePattern: BossFirePattern.Spiral)
                });
            return CreateBattle(
                Array.Empty<BossPartDefinition>(),
                phases,
                form2,
                10,
                seed);
        }

        static BattleSim CreateBattle(
            BossPartDefinition[] parts,
            BossPhase[] phases,
            BossFormDefinition form2,
            int firstFormHp,
            ulong seed)
        {
            var weapon = new WeaponDefinition(
                "req115a_shot",
                20,
                1,
                100,
                1,
                10,
                10);
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
                "boss_shell",
                1,
                1,
                1,
                firstFormHp,
                200,
                600,
                300,
                phases,
                null,
                null,
                EncounterType.Normal,
                parts,
                null,
                null,
                form2);
            return new BattleSim(
                Config(),
                new Rng(seed),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BossPartDefinition Part(
            string id,
            int offsetX,
            int offsetY,
            int hp,
            bool isCore)
        {
            return new BossPartDefinition(
                id,
                offsetX,
                offsetY,
                100,
                100,
                hp,
                isCore,
                null,
                BossPartAttackProfile.None,
                0);
        }

        static BossPhase Phase(
            BossPhasePartRule[] rules,
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
                partRules: rules);
        }

        static LaserAttackDefinition RailBeam()
        {
            return new LaserAttackDefinition(
                8,
                2,
                1,
                3,
                2,
                0,
                0,
                -40 * SimSpace.SubUnitsPerWorldUnit,
                0,
                32,
                8 * SimSpace.SubUnitsPerWorldUnit,
                1);
        }

        static BattleSimConfig Config()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerMinX = 0;
            config.PlayerMaxX = 0;
            config.PlayerMinY = 0;
            config.PlayerMaxY = 0;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerInvulnerable = true;
            config.FireIntervalTicks = 1;
            config.PlayerBulletSpeedPerTick = 100;
            config.MainShotBaseDamage = 20;
            config.MainShotHalfWidth = 10;
            config.MainShotHalfHeight = 10;
            config.BulletDespawnX = 2000;
            config.EnemyDespawnX = -10000;
            config.MaxBullets = 64;
            config.MaxEnemyBullets = 128;
            config.KillComboGaugeGain = 0;
            return config;
        }

        static void AdvanceBossEntry(BattleSim sim)
        {
            InputCommand none = InputCommand.None;
            for (int tick = 0;
                tick < 200
                    && (!sim.BossActive || sim.BossEntering);
                tick++)
                sim.Step(in none);
            Assert.IsTrue(sim.BossActive);
            Assert.IsFalse(sim.BossEntering);
        }

        static bool FireUntilPhase(BattleSim sim, int expectedPhase)
        {
            var fire = new InputCommand(0, 0, true);
            for (int tick = 0; tick < 40; tick++)
            {
                sim.Step(in fire);
                if (sim.Boss.Phase == expectedPhase)
                    return HasEvent(sim, SimEventType.BossPhaseChanged);
            }
            return false;
        }

        static bool FireUntilTransition(BattleSim sim)
        {
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            for (int tick = 0; tick < 40; tick++)
            {
                if (tick == 0)
                    sim.Step(in fire);
                else
                    sim.Step(in none);
                if (sim.BossTransitioning)
                    return true;
            }
            return false;
        }

        static bool FireUntilFinalClear(BattleSim sim)
        {
            var fire = new InputCommand(0, 0, true);
            for (int tick = 0; tick < 60; tick++)
            {
                sim.Step(in fire);
                if (sim.BossDefeated)
                    return HasEvent(sim, SimEventType.StageCleared);
            }
            return false;
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
