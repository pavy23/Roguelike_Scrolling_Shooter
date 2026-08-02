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

        static WarshipEncounter EnterAttrition()
        {
            WarshipEncounter encounter = CreateEncounter();
            encounter.Step(Array.Empty<WarshipDamageCommand>());
            encounter.Step(new[] { new WarshipDamageCommand("stern", 2) });
            return encounter;
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
