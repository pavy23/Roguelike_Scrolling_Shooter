using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req118WarshipPositionTests
    {
        const string FortressThemeId = "fortress";
        const string FortressBossId = "boss_fortress";
        const string SternPartId = "engine";
        const string ForwardTurretPartId = "turret_c";
        const string CorePartId = "core";
        const ulong FortressSeed = 2UL;
        const int FortressStageIndex = 3;
        const int LongGateObservationTick = 3_000;
        const int BossReachTickBudget = 60_000;

        [Test]
        public void RepositoryFortressWarshipKeepsDamageablePartsInPlayfieldForEntireEncounter()
        {
            StageBossTemplate boss = RepositoryFortressBoss();
            var encounter = new WarshipEncounter(
                boss.WarshipEncounter,
                boss.Parts);

            for (int tick = 1;
                tick <= boss.WarshipEncounter.WarningTicks;
                tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            Assert.AreEqual(0, encounter.ActiveGroupIndex);
            AssertPartWorldX(encounter, SternPartId, 19);
            AssertPartWorldX(encounter, ForwardTurretPartId, 15);
            AssertPartWorldX(encounter, CorePartId, 9);

            while (encounter.Tick < 240)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            Assert.AreEqual(
                12 * SimSpace.SubUnitsPerWorldUnit,
                encounter.WorldX);
            AssertPartWorldX(encounter, SternPartId, 16);
            AssertPartWorldX(encounter, ForwardTurretPartId, 12);
            AssertPartWorldX(encounter, CorePartId, 6);

            while (encounter.Tick < LongGateObservationTick)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            AssertPartWorldX(encounter, SternPartId, 16);
            AssertPartWorldX(encounter, ForwardTurretPartId, 12);
            AssertPartWorldX(encounter, CorePartId, 6);

            encounter.Step(new[]
            {
                new WarshipDamageCommand(
                    SternPartId,
                    FindPart(encounter, SternPartId).MaxHp)
            });
            Assert.AreEqual(1, encounter.ActiveGroupIndex);
            AssertDamageablePartsInsidePlayfield(encounter);

            for (int elapsed = 1; elapsed < 720; elapsed++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(1, encounter.ActiveGroupIndex);
                AssertDamageablePartsInsidePlayfield(encounter);
                if (elapsed == 640 || elapsed == 719)
                    AssertPartWorldX(
                        encounter,
                        ForwardTurretPartId,
                        -20);
            }

            encounter.Step(Array.Empty<WarshipDamageCommand>());
            Assert.AreEqual(2, encounter.ActiveGroupIndex);
            Assert.AreEqual(
                12 * SimSpace.SubUnitsPerWorldUnit,
                encounter.WorldX);
            AssertPartWorldX(encounter, CorePartId, 6);
            AssertDamageablePartsInsidePlayfield(encounter);

            for (int tick = 0; tick < LongGateObservationTick; tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(2, encounter.ActiveGroupIndex);
                AssertPartWorldX(encounter, CorePartId, 6);
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            encounter.Step(new[]
            {
                new WarshipDamageCommand(
                    CorePartId,
                    FindPart(encounter, CorePartId).Hp)
            });
            Assert.IsTrue(encounter.Completed);
        }

        [Test]
        public void RepositoryFortressBattleSimProjectileDamagesHeldSternAfterLegacyEscapeTick()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            int difficulty = StageDifficultyCurve.CreateDefault()
                .GetDifficulty(FortressStageIndex);
            StagePlan plan = generator.GenerateRoute(
                FortressSeed,
                FortressStageIndex,
                difficulty,
                FortressThemeId,
                EncounterType.Normal);
            Assert.AreEqual(FortressBossId, plan.BossId);
            Assert.NotNull(plan.WarshipEncounter);

            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var battle = new BattleSim(
                config,
                new Rng(FortressSeed),
                plan,
                data.BattleContent,
                data.CreatePowerUpGauge());

            int ticks = 0;
            while (ticks < BossReachTickBudget
                && !(battle.BossActive
                    && battle.WarshipActiveGroupIndex == 0))
            {
                Step(battle);
                Assert.IsTrue(battle.IsPlayerAlive);
                ticks++;
            }

            Assert.Less(ticks, BossReachTickBudget);
            while (battle.WarshipEncounterTick
                < LongGateObservationTick)
                Step(battle);

            Assert.AreEqual(0, battle.WarshipActiveGroupIndex);
            int sternIndex = FindBattlePartIndex(battle, SternPartId);
            BossPartState stern = battle.BossParts[sternIndex];
            Assert.GreaterOrEqual(
                stern.X,
                -SimSpace.PlayfieldHalfWidthSubUnits);
            Assert.LessOrEqual(
                stern.X,
                SimSpace.PlayfieldHalfWidthSubUnits);
            Assert.AreEqual(
                16 * SimSpace.SubUnitsPerWorldUnit,
                stern.X);

            int hpBefore = stern.Hp;
            Assert.IsTrue(battle.TrySpawnGhostMainShot(
                stern.X,
                stern.Y,
                1));
            Step(battle);

            Assert.Less(battle.BossParts[sternIndex].Hp, hpBefore);
            Assert.AreEqual(hpBefore - 1, battle.BossParts[sternIndex].Hp);
        }

        static void AssertDamageablePartsInsidePlayfield(
            WarshipEncounter encounter)
        {
            for (int i = 0; i < encounter.Parts.Count; i++)
            {
                WarshipPartState part = encounter.Parts[i];
                if (!part.Active || part.Invulnerable)
                    continue;
                Assert.GreaterOrEqual(
                    part.X,
                    -SimSpace.PlayfieldHalfWidthSubUnits,
                    $"tick={encounter.Tick} part={part.PartId}");
                Assert.LessOrEqual(
                    part.X,
                    SimSpace.PlayfieldHalfWidthSubUnits,
                    $"tick={encounter.Tick} part={part.PartId}");
            }
        }

        static void AssertPartWorldX(
            WarshipEncounter encounter,
            string partId,
            int expectedWorldUnits)
        {
            Assert.AreEqual(
                expectedWorldUnits * SimSpace.SubUnitsPerWorldUnit,
                FindPart(encounter, partId).X,
                $"tick={encounter.Tick} part={partId}");
        }

        static WarshipPartState FindPart(
            WarshipEncounter encounter,
            string partId)
        {
            for (int i = 0; i < encounter.Parts.Count; i++)
                if (string.Equals(
                        encounter.Parts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return encounter.Parts[i];
            Assert.Fail($"Missing warship part '{partId}'.");
            return default;
        }

        static int FindBattlePartIndex(BattleSim battle, string partId)
        {
            for (int i = 0; i < battle.BossParts.Count; i++)
                if (string.Equals(
                        battle.BossParts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return i;
            Assert.Fail($"Missing battle boss part '{partId}'.");
            return -1;
        }

        static void Step(BattleSim battle)
        {
            InputCommand input = InputCommand.None;
            battle.Step(in input);
        }

        static StageBossTemplate RepositoryFortressBoss()
        {
            StageGenerationCatalog catalog = ParseRepositoryGameData()
                .StageGeneration;
            for (int i = 0; i < catalog.Bosses.Count; i++)
                if (string.Equals(
                        catalog.Bosses[i].BossId,
                        FortressBossId,
                        StringComparison.Ordinal))
                    return catalog.Bosses[i];
            Assert.Fail($"Missing boss '{FortressBossId}'.");
            return null;
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(
                FindRepositoryRoot(),
                "GameData");
            return GameDataParser.Parse(
                Read(gameData, "enemies.json"),
                Read(gameData, "weapons.json"),
                Read(gameData, "waves.json"),
                Read(gameData, "rewards.json"),
                Read(gameData, "ships.json"),
                Read(gameData, "scoring.json"));
        }

        static string Read(string directory, string fileName)
        {
            return File.ReadAllText(Path.Combine(directory, fileName));
        }

        static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(
                    Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }
    }
}
