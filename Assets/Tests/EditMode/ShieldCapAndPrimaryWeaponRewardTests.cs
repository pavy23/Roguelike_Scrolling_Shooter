using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class ShieldCapAndPrimaryWeaponRewardTests
    {
        [Test]
        public void ShieldCapDefaultsToThreeAndRuntimeRangeClampsStock()
        {
            BattleSimConfig defaults = BattleSimConfig.CreateDefault();
            Assert.AreEqual(3, defaults.MaxShieldStock);

            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 5;
            config.MaxShieldStock = 5;
            BattleSim battle = CreateBattle(config);

            Assert.AreEqual(5, battle.ShieldStock);
            Assert.AreEqual(3, battle.SetMaxShieldStock(3));
            Assert.AreEqual(3, battle.MaxShieldStock);
            battle.SetMaxShieldStock(5);
            Assert.AreEqual(2, battle.RecoverShieldStock(5));
            Assert.AreEqual(5, battle.ShieldStock);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => battle.SetMaxShieldStock(2));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => battle.SetMaxShieldStock(6));
        }

        [Test]
        public void RuntimeShieldCapRoundTripsThroughChecksumAndResume()
        {
            RunManager source = CreateRun(0x5A1E1DUL);
            source.SetMaxShieldStock(5);
            RunSuspendData data = source.ExportSuspendData();

            Assert.AreEqual(5, data.maxShieldStock);
            Assert.IsTrue(Shmup.Core.SaveDataIntegrity.HasValidChecksum(data));

            RunManager resumed = Resume(data);
            Assert.AreEqual(5, resumed.MaxShieldStock);
            Assert.AreEqual(
                source.Battle.ShieldStock,
                resumed.Battle.ShieldStock);

            data.maxShieldStock = 6;
            Assert.Throws<ArgumentException>(
                () => Shmup.Core.SaveDataIntegrity
                    .MigrateAndValidate(data));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => source.SetMaxShieldStock(6));
        }

        [Test]
        public void PrimaryRewardSwitchesRemainRepeatableAndSuspendDeterministic()
        {
            RunManager source = CreateRun(0xD0B1EUL);

            AdvanceToReward(source);
            ChoosePrimary(source, PrimaryWeaponFamily.Double);
            AssertPrimary(
                source,
                PrimaryWeaponFamily.Double,
                WeaponType.Spread);
            PrimaryWeaponFamilyDefinition doubleShot =
                source.CurrentPrimaryWeaponDefinition;

            AdvanceToReward(source);
            ChoosePrimary(source, PrimaryWeaponFamily.Laser);
            AssertPrimary(
                source,
                PrimaryWeaponFamily.Laser,
                WeaponType.Laser);
            PrimaryWeaponFamilyDefinition laser =
                source.CurrentPrimaryWeaponDefinition;
            Assert.Greater(laser.PierceEnemyCount, 0);
            Assert.Greater(
                laser.FireIntervalTicks,
                doubleShot.FireIntervalTicks);
            Assert.Greater(
                laser.BaseDamage,
                doubleShot.BaseDamage);

            RunSuspendData data = source.ExportSuspendData();
            Assert.AreEqual(
                (int)PrimaryWeaponFamily.Laser,
                data.primaryWeaponFamily);
            RunManager resumed = Resume(data);
            AssertPrimary(
                resumed,
                PrimaryWeaponFamily.Laser,
                WeaponType.Laser);
            AssertRunHashEqual(source, resumed);

            AdvanceToReward(source);
            ChoosePrimary(source, PrimaryWeaponFamily.Double);
            AssertPrimary(
                source,
                PrimaryWeaponFamily.Double,
                WeaponType.Spread);
            Assert.AreEqual(
                2,
                source.CurrentPrimaryWeaponDefinition.SpreadWays);
        }

        static void AdvanceToReward(RunManager run)
        {
            var fire = new InputCommand(0, 0, true);
            for (int guard = 0;
                guard < 1000
                    && run.State != RunState.AwaitingReward;
                guard++)
            {
                if (run.State == RunState.AwaitingRoute)
                {
                    Assert.GreaterOrEqual(
                        run.RouteOptions.Count,
                        RunManager.MinimumRouteOptionCount);
                    run.ChooseRoute(0);
                }
                else
                {
                    Assert.AreEqual(RunState.Playing, run.State);
                    run.Step(in fire);
                }
            }
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RunManager.RewardOptionCount,
                run.RewardOptions.Count);
        }

        static void ChoosePrimary(
            RunManager run,
            PrimaryWeaponFamily family)
        {
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                RewardOption option = run.RewardOptions[i];
                if (option.Type == RewardType.PrimaryWeaponFamily
                    && option.PrimaryWeaponFamily == family)
                {
                    run.ChooseReward(i);
                    return;
                }
            }
            Assert.Fail($"Primary family {family} was not offered.");
        }

        static void AssertPrimary(
            RunManager run,
            PrimaryWeaponFamily family,
            WeaponType weaponType)
        {
            Assert.AreEqual(family, run.CurrentPrimaryWeaponFamily);
            Assert.AreEqual(
                weaponType,
                run.Battle.PlayerWeaponType);
            Assert.IsNotNull(run.CurrentPrimaryWeaponDefinition);
            Assert.IsNotEmpty(
                run.CurrentPrimaryWeaponDefinition.DisplayName);
            Assert.IsNotEmpty(
                run.CurrentPrimaryWeaponDefinition.Description);
        }

        static void AssertRunHashEqual(
            RunManager expected,
            RunManager actual)
        {
            var expectedHash = new DeterminismAuditHasher();
            var actualHash = new DeterminismAuditHasher();
            expectedHash.FoldRunState(expected);
            actualHash.FoldRunState(actual);
            Assert.AreEqual(expectedHash.Hash, actualHash.Hash);
        }

        static RunManager CreateRun(ulong seed)
        {
            return new RunManager(
                seed,
                new RewardRouteGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault(),
                1,
                1,
                new RunProgressionConfig(4, 1));
        }

        static RunManager Resume(RunSuspendData data)
        {
            return RunManager.ResumeFromSuspendData(
                data,
                new RewardRouteGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                CreateRewards(),
                ShipDefinition.CreateDefault());
        }

        static RewardCatalog CreateRewards()
        {
            return new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    PrimaryReward(
                        "primary_vulcan",
                        PrimaryWeaponFamily.Vulcan),
                    PrimaryReward(
                        "primary_double",
                        PrimaryWeaponFamily.Double),
                    PrimaryReward(
                        "primary_laser",
                        PrimaryWeaponFamily.Laser),
                    new RewardDefinition(
                        "capsules",
                        RewardType.Capsules,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        99)
                });
        }

        static RewardDefinition PrimaryReward(
            string id,
            PrimaryWeaponFamily family)
        {
            return new RewardDefinition(
                id,
                RewardType.PrimaryWeaponFamily,
                PowerUpSlot.MainShot,
                1,
                1,
                1,
                99,
                null,
                BattleModifier.None,
                MissileFamily.Straight,
                OptionFormation.Trail,
                family);
        }

        static BattleSim CreateBattle(BattleSimConfig config)
        {
            return new BattleSim(
                config,
                new Rng(1UL),
                RewardRouteGenerator.Plan("a"),
                CreateContent(),
                PowerUpGauge.CreateDefault());
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.StartingShieldStock = 3;
            config.MaxShieldStock = 3;
            config.EnemyBulletDamage = 0;
            config.MaxEnemyBullets = 0;
            config.CapsuleNoDropWeight = 1;
            return config;
        }

        static BattleContent CreateContent()
        {
            var weapon = new WeaponDefinition(
                "primary",
                10,
                2,
                256,
                1,
                0,
                0);
            return new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
        }

        sealed class RewardRouteGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
                { "a", "b", "c", "d" };
            static readonly BossPhase[] Phases =
                { new BossPhase(999, 1, 1, 1) };

            public IReadOnlyList<string> ThemeIds => Themes;

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return Plan(
                    Themes[(stageIndex - 1) % Themes.Length]);
            }

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                return Array.AsReadOnly((string[])Themes.Clone());
            }

            public bool CanGenerateRoute(
                string themeId,
                int stageIndex,
                int difficulty,
                EncounterType encounterType)
            {
                return Array.IndexOf(Themes, themeId) >= 0;
            }

            public StagePlan GenerateRoute(
                ulong seed,
                int stageIndex,
                int difficulty,
                string themeId,
                EncounterType encounterType)
            {
                return Plan(themeId);
            }

            public static StagePlan Plan(string themeId)
            {
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "segment",
                            1,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
                    "boss",
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    512,
                    Phases,
                    themeId,
                    themeId);
            }
        }
    }
}
