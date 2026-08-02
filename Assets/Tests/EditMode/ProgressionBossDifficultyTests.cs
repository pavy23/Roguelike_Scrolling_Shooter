using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class ProgressionBossDifficultyTests
    {
        [Test]
        public void ShuffledWarshipScalesPartsToProgressionHpWithoutBreakingInvariant()
        {
            RunManager run = AdvanceToShuffledBiome(
                new ProgressionGenerator(true));

            Assert.AreEqual(2, run.BiomeIndex);
            Assert.AreEqual(3, run.ThemeStageIndex);
            Assert.AreEqual("theme_3", run.StagePlan.ThemeId);
            Assert.AreEqual(50, run.StagePlan.BossMaxHp);
            Assert.AreEqual(3, run.StagePlan.BossParts.Count);
            Assert.AreEqual(10, run.StagePlan.BossParts[0].MaxHp);
            Assert.AreEqual(15, run.StagePlan.BossParts[1].MaxHp);
            Assert.AreEqual(25, run.StagePlan.BossParts[2].MaxHp);
            Assert.NotNull(run.StagePlan.WarshipEncounter);
        }

        [Test]
        public void ShuffledRegularBossStillUsesProgressionCombatValuesAndThemePattern()
        {
            RunManager run = AdvanceToShuffledBiome(
                new ProgressionGenerator(false));

            Assert.AreEqual(50, run.StagePlan.BossMaxHp);
            Assert.AreEqual(1, run.StagePlan.BossPhases.Count);
            BossPhase phase = run.StagePlan.BossPhases[0];
            Assert.AreEqual(40, phase.FireIntervalTicks);
            Assert.AreEqual(5, phase.Ways);
            Assert.AreEqual(9, phase.BulletSpeedNumerator);
            Assert.AreEqual(2, phase.BulletSpeedDenominator);
            Assert.AreEqual(
                BossMovementPattern.VerticalSine,
                phase.MovementPattern);
            Assert.AreEqual(4, phase.MovementAmplitudeNumerator);
            Assert.AreEqual(120, phase.MovementPeriodTicks);
        }

        static RunManager AdvanceToShuffledBiome(
            IRouteStageGenerator generator)
        {
            RunManager run = CreateRun(generator);
            InputCommand fire = new InputCommand(0, 0, true);
            for (int guard = 0;
                guard < 5_000 && run.BiomeIndex == 1;
                guard++)
            {
                if (run.State == RunState.AwaitingReward)
                    Assert.IsTrue(run.ChooseReward(0));
                else if (run.State == RunState.AwaitingContract)
                    Assert.IsTrue(run.ChooseContract(0));
                else
                    run.Step(in fire);
            }
            Assert.AreEqual(2, run.BiomeIndex);
            for (int guard = 0;
                guard < 100 && !run.IsBiomeBoss;
                guard++)
                run.Step(InputCommand.None);
            Assert.IsTrue(run.IsBiomeBoss);
            return run;
        }

        static RunManager CreateRun(IRouteStageGenerator generator)
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerMinX = -10_000;
            config.PlayerMaxX = 10_000;
            config.PlayerMinY = -10_000;
            config.PlayerMaxY = 10_000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMaxHp = 1_000_000;
            config.BulletDespawnX = 20_000;
            config.EnemyDespawnX = -20_000;
            config.StartingShieldStock = 5;
            config.MaxShieldStock = 5;
            var weapon = new WeaponDefinition(
                "progression_test_shot",
                1,
                1,
                256,
                1,
                0,
                0);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
            var rewards = new RewardCatalog(
                RunManager.MainRewardOptionCount,
                new[]
                {
                    CapsuleReward("capsules_a"),
                    CapsuleReward("capsules_b"),
                    CapsuleReward("capsules_c"),
                    CapsuleReward("capsules_d")
                });
            return new RunManager(
                0x112UL,
                generator,
                config,
                content,
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                1,
                1,
                new RunProgressionConfig(5, 1));
        }

        static RewardDefinition CapsuleReward(string id)
        {
            return new RewardDefinition(
                id,
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                1,
                1,
                1,
                99);
        }

        sealed class ProgressionGenerator : IRouteStageGenerator
        {
            static readonly string[] Themes =
            {
                "theme_1",
                "theme_2",
                "theme_3",
                "theme_4",
                "theme_5"
            };
            static readonly string[] ShuffledThemes =
            {
                "theme_1",
                "theme_3",
                "theme_2",
                "theme_4",
                "theme_5"
            };

            readonly bool _multipart;

            public ProgressionGenerator(bool multipart)
            {
                _multipart = multipart;
            }

            public IReadOnlyList<string> ThemeIds => Themes;

            public IReadOnlyList<string> GetThemeOrder(ulong seed)
            {
                return Array.AsReadOnly((string[])ShuffledThemes.Clone());
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return GenerateRoute(
                    seed,
                    stageIndex,
                    difficulty,
                    Themes[stageIndex - 1],
                    EncounterType.Normal);
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
                if (_multipart
                    && string.Equals(
                        themeId,
                        "theme_3",
                        StringComparison.Ordinal))
                    return MultipartPlan(themeId, encounterType);
                return RegularPlan(themeId, encounterType);
            }

            static StagePlan RegularPlan(
                string themeId,
                EncounterType encounterType)
            {
                bool progressionReference = string.Equals(
                    themeId,
                    "theme_2",
                    StringComparison.Ordinal);
                bool shuffledTheme = string.Equals(
                    themeId,
                    "theme_3",
                    StringComparison.Ordinal);
                int hp = progressionReference
                    ? 50
                    : shuffledTheme ? 100 : 1;
                BossPhase phase = progressionReference
                    ? new BossPhase(40, 5, 9, 2)
                    : new BossPhase(
                        90,
                        3,
                        7,
                        3,
                        shuffledTheme
                            ? BossMovementPattern.VerticalSine
                            : BossMovementPattern.Stationary,
                        shuffledTheme ? 4 : 0,
                        1,
                        shuffledTheme ? 120 : 1,
                        BossPartVulnerability.Legacy,
                        0,
                        0);
                return new StagePlan(
                    new[] { Segment(themeId) },
                    themeId + "_boss",
                    1,
                    1,
                    1,
                    hp,
                    128,
                    128,
                    256,
                    new[] { phase },
                    themeId,
                    themeId,
                    encounterType);
            }

            static StagePlan MultipartPlan(
                string themeId,
                EncounterType encounterType)
            {
                BossPartDefinition[] parts =
                {
                    Part("engine", 20, false),
                    Part("turret", 30, false),
                    Part("core", 50, true)
                };
                var groups = new[]
                {
                    new WarshipPartGroupDefinition(
                        "stern",
                        WarshipGroupRole.MidbossGate,
                        new[] { "engine" },
                        0),
                    new WarshipPartGroupDefinition(
                        "hull",
                        WarshipGroupRole.AttritionLine,
                        new[] { "turret" },
                        60),
                    new WarshipPartGroupDefinition(
                        "bow",
                        WarshipGroupRole.FinalCore,
                        new[] { "core" },
                        0)
                };
                var warship = new WarshipEncounterDefinition(
                    "test_warship",
                    112,
                    0,
                    0,
                    0,
                    1,
                    1,
                    5,
                    1,
                    3,
                    groups,
                    parts);
                return new StagePlan(
                    new[] { Segment(themeId) },
                    themeId + "_warship",
                    1,
                    1,
                    1,
                    100,
                    128,
                    128,
                    256,
                    Array.Empty<BossPhase>(),
                    themeId,
                    themeId,
                    encounterType,
                    parts,
                    null,
                    warship);
            }

            static BossPartDefinition Part(
                string id,
                int hp,
                bool isCore)
            {
                return new BossPartDefinition(
                    id,
                    0,
                    0,
                    1,
                    1,
                    hp,
                    isCore,
                    Array.Empty<string>(),
                    BossPartAttackProfile.None,
                    0);
            }

            static StageSegment Segment(string themeId)
            {
                return new StageSegment(
                    themeId + "_segment",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
            }
        }
    }
}
