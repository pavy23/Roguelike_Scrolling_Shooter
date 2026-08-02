using System;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class WarshipEncounterTests
    {
        [Test]
        public void GroupsActivateInOrderAndSternEmitsExistingMidbossEvent()
        {
            WarshipEncounter encounter = CreateEncounter();
            encounter.Step(new[]
            {
                new WarshipDamageCommand("stern", 2),
                new WarshipDamageCommand("turret_a", 1)
            });
            Assert.AreEqual(-1, encounter.ActiveGroupIndex);
            Assert.IsFalse(encounter.WasPartDestroyed("stern"));
            Assert.IsTrue(HasEvent(encounter, SimEventType.WarshipWarningStarted));

            encounter.Step(new[]
            {
                new WarshipDamageCommand("core", 99),
                new WarshipDamageCommand("stern", 2)
            });
            Assert.AreEqual(1, encounter.ActiveGroupIndex);
            Assert.IsTrue(encounter.MidbossDefeated);
            Assert.IsTrue(encounter.WasPartDestroyed("stern"));
            Assert.IsFalse(encounter.WasPartDestroyed("core"));
            Assert.IsTrue(HasEvent(encounter, SimEventType.MidBossDefeated));
            Assert.IsTrue(HasEventPart(
                encounter, SimEventType.WarshipGroupActivated, "hull"));
            Assert.IsFalse(encounter.Parts[0].Active);
            Assert.IsTrue(encounter.Parts[1].Active);
            Assert.IsFalse(encounter.Parts[4].Active);
        }

        [Test]
        public void AttritionTurretCountBranchesCoreOpeningDensity()
        {
            WarshipEncounter untouched = EnterAttrition();
            WarshipEncounter focused = EnterAttrition();
            StepEmpty(untouched, 3);
            focused.Step(new[]
            {
                new WarshipDamageCommand("turret_a", 1),
                new WarshipDamageCommand("turret_b", 1)
            });
            StepEmpty(focused, 2);

            Assert.IsTrue(untouched.CoreBattleActive);
            Assert.IsTrue(focused.CoreBattleActive);
            Assert.AreEqual(0, untouched.DestroyedAttritionParts);
            Assert.AreEqual(2, focused.DestroyedAttritionParts);
            Assert.AreEqual(7, untouched.CoreOpeningWays);
            Assert.AreEqual(5, focused.CoreOpeningWays);
            Assert.AreEqual(5, EventArg(
                focused, SimEventType.WarshipCoreBattleStarted));
        }

        [Test]
        public void ScrollUsesLocalOffsetsAndExactRemainder()
        {
            WarshipEncounter encounter = CreateEncounter();
            int initialX = encounter.Parts[1].X;
            encounter.Step(Array.Empty<WarshipDamageCommand>());
            Assert.AreEqual(initialX - 1, encounter.Parts[1].X);
            Assert.AreEqual(1, encounter.ScrollOffset);
            Assert.AreEqual(1, encounter.ScrollRemainder);
            encounter.Step(Array.Empty<WarshipDamageCommand>());
            Assert.AreEqual(initialX - 3, encounter.Parts[1].X);
            Assert.AreEqual(3, encounter.ScrollOffset);
            Assert.AreEqual(0, encounter.ScrollRemainder);
        }

        [Test]
        public void SuspendRoundTripRestoresExactMidEncounterState()
        {
            WarshipEncounter source = EnterAttrition();
            source.Step(new[] { new WarshipDamageCommand("turret_b", 1) });
            WarshipEncounterSuspendData captured = source.CaptureSuspendData();
            WarshipEncounter restored = WarshipEncounter.Restore(
                Definition(Parts()), Parts(), JsonRoundTrip(captured));
            captured.partHp[1] = 99;
            Assert.AreEqual(1, source.Parts[1].Hp);
            Assert.AreEqual(source.Tick, restored.Tick);
            Assert.AreEqual(source.ScrollOffset, restored.ScrollOffset);
            Assert.AreEqual(source.ScrollRemainder, restored.ScrollRemainder);
            Assert.AreEqual(
                source.ActiveGroupElapsedTicks,
                restored.ActiveGroupElapsedTicks);
            Assert.AreEqual(
                source.DestroyedAttritionParts,
                restored.DestroyedAttritionParts);

            var damage = new[] { new WarshipDamageCommand("turret_a", 1) };
            source.Step(damage);
            restored.Step(damage);
            source.Step(Array.Empty<WarshipDamageCommand>());
            restored.Step(Array.Empty<WarshipDamageCommand>());
            Assert.AreEqual(Audit(source), Audit(restored));
        }

        [Test]
        public void SameSeedTwiceProducesIdenticalEncounterAudit()
        {
            const ulong seed = 0x110UL;
            WarshipEncounter first = CreateEncounter();
            WarshipEncounter second = CreateEncounter();
            var firstRng = new Rng(seed).Fork(110);
            var secondRng = new Rng(seed).Fork(110);
            for (int tick = 0; tick < 8; tick++)
            {
                first.Step(CommandsFor(first, firstRng.NextInt(0, 4)));
                second.Step(CommandsFor(second, secondRng.NextInt(0, 4)));
                Assert.AreEqual(Audit(first), Audit(second));
            }
        }

        [Test]
        public void CoreCompletionEmitsStageClearAfterFinalGroupOnly()
        {
            WarshipEncounter encounter = EnterAttrition();
            StepEmpty(encounter, 3);
            encounter.Step(new[] { new WarshipDamageCommand("core", 3) });
            Assert.IsTrue(encounter.Completed);
            Assert.IsTrue(encounter.WasPartDestroyed("core"));
            Assert.IsTrue(HasEvent(encounter, SimEventType.StageCleared));
        }

        [Test]
        public void BattleSimTicksGateGroupsAndPublishWarshipLifecycleEvents()
        {
            BattleSim sim = CreateBattle(0x1131UL);
            Step(sim, InputCommand.None);

            Assert.AreEqual(0, sim.WarshipActiveGroupIndex);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.WarshipWarningStarted));
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.WarshipGroupActivated));
            Assert.IsFalse(sim.BossParts[0].Invulnerable);
            Assert.IsTrue(sim.BossParts[1].Invulnerable);
            Assert.IsTrue(sim.BossParts[5].Invulnerable);

            int turretHp = sim.BossParts[1].Hp;
            SpawnPartHit(sim, 1, 99);
            Step(sim, InputCommand.None);
            Assert.AreEqual(turretHp, sim.BossParts[1].Hp);
            Assert.AreEqual(0, sim.WarshipActiveGroupIndex);

            SpawnPartHit(sim, 0, 99);
            Step(sim, InputCommand.None);
            Assert.IsTrue(sim.BossParts[0].Destroyed);
            Assert.AreEqual(1, sim.WarshipActiveGroupIndex);
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.MidBossDefeated));
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.WarshipGroupActivated));
            Assert.IsFalse(sim.BossParts[1].Invulnerable);
            Assert.IsTrue(sim.BossParts[5].Invulnerable);

            Step(sim, InputCommand.None);
            Assert.IsFalse(HasEvent(
                sim.EventsThisTick,
                SimEventType.MidBossDefeated));
        }

        [Test]
        public void BattleSimAttritionTimerAndDestroyedTurretsChangeOpeningVolley()
        {
            BattleSim untouched = CreateBattle(0x1132UL);
            BattleSim focused = CreateBattle(0x1132UL);
            EnterBattleAttrition(untouched);
            EnterBattleAttrition(focused);

            for (int part = 1; part <= 4; part++)
                SpawnPartHit(focused, part, 99);
            Step(focused, InputCommand.None);
            Step(untouched, InputCommand.None);
            Assert.AreEqual(4, focused.WarshipDestroyedAttritionParts);
            Assert.AreEqual(0, untouched.WarshipDestroyedAttritionParts);

            Step(focused, InputCommand.None);
            Step(untouched, InputCommand.None);
            Step(focused, InputCommand.None);
            Step(untouched, InputCommand.None);
            Assert.AreEqual(2, focused.WarshipActiveGroupIndex);
            Assert.AreEqual(2, untouched.WarshipActiveGroupIndex);
            Assert.AreEqual(100, focused.Boss.X);
            Assert.AreEqual(100, untouched.Boss.X);
            Assert.AreEqual(3, focused.WarshipCoreOpeningWays);
            Assert.AreEqual(9, untouched.WarshipCoreOpeningWays);
            Assert.AreEqual(3, EventArg(
                focused.EventsThisTick,
                SimEventType.WarshipCoreBattleStarted));
            Assert.AreEqual(9, EventArg(
                untouched.EventsThisTick,
                SimEventType.WarshipCoreBattleStarted));

            Step(focused, InputCommand.None);
            Step(untouched, InputCommand.None);
            Assert.AreEqual(3, CountEnemyBullets(focused));
            Assert.AreEqual(9, CountEnemyBullets(untouched));
        }

        [Test]
        public void RecordedBombInputsReplayWholeWarshipBattleExactly()
        {
            const ulong seed = 0x1133UL;
            BattleSim source = CreateBattle(seed, bombs: 3);
            var recorder = new InputRecorder(16);
            InputCommand[] commands =
            {
                InputCommand.None,
                BombInput(),
                InputCommand.None,
                BombInput(),
                InputCommand.None,
                BombInput()
            };
            var hashes = new ulong[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                recorder.Record(in commands[i]);
                Step(source, commands[i]);
                hashes[i] = Audit(source);
            }
            Assert.IsTrue(source.BossDefeated);
            Assert.IsTrue(HasEvent(
                source.EventsThisTick,
                SimEventType.StageCleared));

            BattleSim replay = CreateBattle(seed, bombs: 3);
            int tick = 0;
            foreach (InputCommand command in new InputPlayback(
                recorder.Export()))
            {
                Step(replay, command);
                Assert.AreEqual(hashes[tick], Audit(replay));
                tick++;
            }
            Assert.AreEqual(commands.Length, tick);
            Assert.AreEqual(3, replay.WarshipCoreOpeningWays);
            Assert.IsTrue(replay.BossDefeated);
        }

        [Test]
        public void BattleSimWarshipSuspendRestoresMidAttritionAndOpeningConsumption()
        {
            BattleSim source = CreateBattle(0x1134UL);
            BattleSim restored = CreateBattle(0x1134UL);
            EnterBattleAttrition(source);
            EnterBattleAttrition(restored);

            SpawnPartHit(source, 1, 99);
            SpawnPartHit(restored, 2, 99);
            Step(source, InputCommand.None);
            Step(restored, InputCommand.None);
            WarshipEncounterSuspendData data = JsonRoundTrip(
                source.CaptureWarshipEncounterSuspendData());
            restored.RestoreWarshipEncounterSuspendData(data);

            Step(source, InputCommand.None);
            Step(restored, InputCommand.None);
            Assert.AreEqual(Audit(source), Audit(restored));
            Step(source, InputCommand.None);
            Step(restored, InputCommand.None);
            Assert.AreEqual(Audit(source), Audit(restored));
            Step(source, InputCommand.None);
            Step(restored, InputCommand.None);
            Assert.AreEqual(Audit(source), Audit(restored));
            Assert.AreEqual(7, CountEnemyBullets(source));

            WarshipEncounterSuspendData afterOpening = JsonRoundTrip(
                source.CaptureWarshipEncounterSuspendData());
            restored.RestoreWarshipEncounterSuspendData(afterOpening);
            int before = CountEnemyBullets(restored);
            Step(source, InputCommand.None);
            Step(restored, InputCommand.None);
            Assert.AreEqual(Audit(source), Audit(restored));
            Assert.AreEqual(before + 9, CountEnemyBullets(restored));
        }

        static WarshipEncounter EnterAttrition()
        {
            WarshipEncounter encounter = CreateEncounter();
            encounter.Step(Array.Empty<WarshipDamageCommand>());
            encounter.Step(new[] { new WarshipDamageCommand("stern", 2) });
            return encounter;
        }

        static void EnterBattleAttrition(BattleSim sim)
        {
            Step(sim, InputCommand.None);
            SpawnPartHit(sim, 0, 99);
            Step(sim, InputCommand.None);
            Assert.AreEqual(1, sim.WarshipActiveGroupIndex);
        }

        static BattleSim CreateBattle(ulong seed, int bombs = 0)
        {
            BossPartDefinition[] parts = BattleParts();
            var phase = new BossPhase(50, 1, 1, 1);
            var plan = new StagePlan(
                Array.Empty<StageSegment>(),
                "warship",
                1,
                1,
                1,
                9,
                10,
                10,
                100,
                new[] { phase },
                "fortress",
                "fortress",
                EncounterType.Normal,
                parts,
                StageGimmickDefinition.None,
                BattleDefinition(parts));
            var weapon = new WeaponDefinition(
                "shot", 1, 1, 0, 1, 0, 0);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.UseConfiguredMainShotStats = true;
            config.PlayerBulletSpeedPerTick = 0;
            config.MainShotBaseDamage = 1;
            config.MainShotHalfWidth = 1;
            config.MainShotHalfHeight = 1;
            config.PlayerInvulnerable = true;
            config.PlayerSpawnX = -10_000;
            config.PlayerSpawnY = -10_000;
            config.PlayerMinX = -20_000;
            config.PlayerMaxX = 20_000;
            config.PlayerMinY = -20_000;
            config.PlayerMaxY = 20_000;
            config.BulletDespawnX = 30_000;
            config.MaxEnemyBullets = 64;
            config.EnemyBulletDamage = 0;
            config.StartingBombStock = bombs;
            config.MaxBombStock = Math.Max(3, bombs);
            config.BombEffectRadiusSubUnits = 20_000;
            config.BombBossPartDamageCap = 99;
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                content,
                PowerUpGauge.CreateDefault());
        }

        static BossPartDefinition[] BattleParts()
        {
            BossPartAttackProfile none = BossPartAttackProfile.None;
            var coreAttack = new BossPartAttackProfile(
                BossPartAttackType.RadialSpread,
                1,
                9,
                1,
                1,
                0,
                1,
                null);
            return new[]
            {
                BattlePart("engine", -50, 2, false, none),
                BattlePart("turret_a", -30, 1, false, none),
                BattlePart("turret_b", -10, 1, false, none),
                BattlePart("turret_c", 10, 1, false, none),
                BattlePart("turret_d", 30, 1, false, none),
                BattlePart("core", 50, 3, true, coreAttack)
            };
        }

        static BossPartDefinition BattlePart(
            string id,
            int y,
            int hp,
            bool isCore,
            BossPartAttackProfile attack)
        {
            return new BossPartDefinition(
                id,
                0,
                y,
                4,
                4,
                hp,
                isCore,
                Array.Empty<string>(),
                attack,
                0);
        }

        static WarshipEncounterDefinition BattleDefinition(
            BossPartDefinition[] parts)
        {
            return new WarshipEncounterDefinition(
                "battle_warship",
                113,
                1,
                0,
                0,
                0,
                1,
                9,
                2,
                3,
                new[]
                {
                    new WarshipPartGroupDefinition(
                        "stern",
                        WarshipGroupRole.MidbossGate,
                        new[] { "engine" },
                        0),
                    new WarshipPartGroupDefinition(
                        "hull",
                        WarshipGroupRole.AttritionLine,
                        new[]
                        {
                            "turret_a",
                            "turret_b",
                            "turret_c",
                            "turret_d"
                        },
                        3),
                    new WarshipPartGroupDefinition(
                        "bow",
                        WarshipGroupRole.FinalCore,
                        new[] { "core" },
                        0)
                },
                parts);
        }

        static void SpawnPartHit(BattleSim sim, int partIndex, int damage)
        {
            BossPartState part = sim.BossParts[partIndex];
            Assert.IsTrue(sim.TrySpawnGhostMainShot(
                part.X,
                part.Y,
                damage));
        }

        static InputCommand BombInput()
        {
            return new InputCommand(0, 0, false, false, true);
        }

        static void Step(BattleSim sim, InputCommand input)
        {
            sim.Step(in input);
        }

        static int CountEnemyBullets(BattleSim sim)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction == BulletFaction.Enemy)
                    count++;
            return count;
        }

        static ulong Audit(BattleSim sim)
        {
            var hasher = new DeterminismAuditHasher();
            hasher.FoldBattleState(sim);
            return hasher.Hash;
        }

        static void StepEmpty(WarshipEncounter encounter, int count)
        {
            for (int i = 0; i < count; i++)
                encounter.Step(Array.Empty<WarshipDamageCommand>());
        }

        static WarshipEncounter CreateEncounter()
        {
            BossPartDefinition[] parts = Parts();
            return new WarshipEncounter(Definition(parts), parts);
        }

        static BossPartDefinition[] Parts()
        {
            return new[]
            {
                Part("stern", -8, 2, false),
                Part("turret_a", -3, 1, false),
                Part("turret_b", 0, 1, false),
                Part("turret_c", 3, 1, false),
                Part("core", 8, 3, true)
            };
        }

        static BossPartDefinition Part(
            string id, int offsetX, int hp, bool isCore)
        {
            return new BossPartDefinition(
                id, offsetX, 0, 1, 1, hp, isCore,
                Array.Empty<string>(), BossPartAttackProfile.None, 0);
        }

        static WarshipEncounterDefinition Definition(BossPartDefinition[] parts)
        {
            return new WarshipEncounterDefinition(
                "fortress_warship", 110, 2, 100, 10, 3, 2, 7, 1, 3,
                new[]
                {
                    new WarshipPartGroupDefinition(
                        "stern", WarshipGroupRole.MidbossGate,
                        new[] { "stern" }, 0),
                    new WarshipPartGroupDefinition(
                        "hull", WarshipGroupRole.AttritionLine,
                        new[] { "turret_a", "turret_b", "turret_c" }, 3),
                    new WarshipPartGroupDefinition(
                        "bow", WarshipGroupRole.FinalCore,
                        new[] { "core" }, 0)
                },
                parts);
        }

        static WarshipDamageCommand[] CommandsFor(
            WarshipEncounter encounter, int selection)
        {
            if (encounter.ActiveGroupIndex == 0)
                return new[] { new WarshipDamageCommand("stern", 1) };
            if (encounter.ActiveGroupIndex == 1 && selection < 3)
                return new[]
                {
                    new WarshipDamageCommand(
                        selection == 0 ? "turret_a"
                            : selection == 1 ? "turret_b" : "turret_c",
                        1)
                };
            if (encounter.ActiveGroupIndex == 2)
                return new[] { new WarshipDamageCommand("core", 1) };
            return Array.Empty<WarshipDamageCommand>();
        }

        static ulong Audit(WarshipEncounter encounter)
        {
            var hasher = new DeterminismAuditHasher();
            hasher.FoldWarshipEncounterState(encounter);
            return hasher.Hash;
        }

        static bool HasEvent(WarshipEncounter encounter, SimEventType type)
        {
            ArraySegment<SimEvent> events = encounter.EventsThisTick;
            for (int i = 0; i < events.Count; i++)
                if (events.Array[events.Offset + i].Type == type)
                    return true;
            return false;
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

        static bool HasEventPart(
            WarshipEncounter encounter, SimEventType type, string partId)
        {
            ArraySegment<SimEvent> events = encounter.EventsThisTick;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent simEvent = events.Array[events.Offset + i];
                if (simEvent.Type == type
                    && string.Equals(simEvent.PartId, partId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static int EventArg(WarshipEncounter encounter, SimEventType type)
        {
            ArraySegment<SimEvent> events = encounter.EventsThisTick;
            for (int i = 0; i < events.Count; i++)
                if (events.Array[events.Offset + i].Type == type)
                    return events.Array[events.Offset + i].Arg;
            Assert.Fail($"Missing event {type}.");
            return 0;
        }

        static int EventArg(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return events[i].Arg;
            Assert.Fail($"Missing event {type}.");
            return 0;
        }

        static WarshipEncounterSuspendData JsonRoundTrip(
            WarshipEncounterSuspendData source)
        {
            var serializer = new DataContractJsonSerializer(
                typeof(WarshipEncounterSuspendData));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, source);
                stream.Position = 0;
                return (WarshipEncounterSuspendData)serializer.ReadObject(stream);
            }
        }
    }
}
