using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class PassiveRewardTests
    {
        [Test]
        public void FireRateUpStacksAndClampsAtConfiguredMinimum()
        {
            BattleSimConfig config = Config();
            config.MainShotMinimumFireIntervalTicks = 4;
            RunManager run = CreateRun(
                RewardType.FireRateUp,
                amount: 1,
                config,
                new WeaponDefinition("shot", 1, 6, 100, 1, 0, 0),
                new BossEveryStageGenerator(_ => 1));
            var fire = new InputCommand(0, 0, true);

            CompleteBoss(run);
            run.ChooseReward(0);
            Step(run, 11, in fire);
            Assert.AreEqual(3L, run.Battle.Statistics.ShotsFired);

            CompleteBoss(run);
            run.ChooseReward(0);
            Step(run, 9, in fire);
            Assert.AreEqual(3L, run.Battle.Statistics.ShotsFired);

            CompleteBoss(run);
            run.ChooseReward(0);
            Step(run, 9, in fire);
            Assert.AreEqual(
                3L,
                run.Battle.Statistics.ShotsFired,
                "Further stacks must not reduce the interval below four ticks.");
        }

        [Test]
        public void DamageUpStacksByTwoAndSaturatesAtIntMaximum()
        {
            RunManager stacked = CreateRun(
                RewardType.DamageUp,
                amount: 1,
                Config(),
                new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0),
                new BossEveryStageGenerator(stageIndex =>
                    stageIndex == 1 ? 1 : stageIndex == 2 ? 4 : 5));

            CompleteBoss(stacked);
            stacked.ChooseReward(0);
            CompleteBoss(stacked);
            Assert.AreEqual(2L, stacked.Battle.Statistics.ShotsHit);
            stacked.ChooseReward(0);
            CompleteBoss(stacked);
            Assert.AreEqual(
                1L,
                stacked.Battle.Statistics.ShotsHit,
                "Two stacks must raise base damage from one to five.");

            RunManager saturated = CreateRun(
                RewardType.DamageUp,
                amount: 1,
                Config(),
                new WeaponDefinition(
                    "shot", int.MaxValue - 1, 1, 100, 1, 0, 0),
                new BossEveryStageGenerator(stageIndex =>
                    stageIndex == 1 ? 1 : int.MaxValue));
            CompleteBoss(saturated);
            saturated.ChooseReward(0);
            CompleteBoss(saturated);
            Assert.AreEqual(
                1L,
                saturated.Battle.Statistics.ShotsHit,
                "Damage must saturate instead of overflowing.");
        }

        [Test]
        public void MoveSpeedUpStacksAsExactWorldUnitsPerSecondAndClampsNumerator()
        {
            BattleSimConfig config = Config();
            config.PlayerSpeedNumerator = 0;
            config.PlayerSpeedDenominator = SimSpace.TicksPerSecond;
            RunManager stacked = CreateRun(
                RewardType.MoveSpeedUp,
                amount: 1,
                config,
                new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0),
                new BossEveryStageGenerator(_ => 1));
            var moveRight = new InputCommand(1, 0, false);

            CompleteBoss(stacked);
            stacked.ChooseReward(0);
            Step(stacked, SimSpace.TicksPerSecond, in moveRight);
            Assert.AreEqual(SimSpace.SubUnitsPerWorldUnit, stacked.Battle.PlayerX);

            CompleteBoss(stacked);
            stacked.ChooseReward(0);
            Step(stacked, SimSpace.TicksPerSecond, in moveRight);
            Assert.AreEqual(
                2 * SimSpace.SubUnitsPerWorldUnit,
                stacked.Battle.PlayerX);

            BattleSimConfig nearMaximum = Config();
            nearMaximum.PlayerSpeedNumerator = int.MaxValue - 100;
            nearMaximum.PlayerSpeedDenominator = SimSpace.TicksPerSecond;
            RunManager saturated = CreateRun(
                RewardType.MoveSpeedUp,
                amount: 1,
                nearMaximum,
                new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0),
                new BossEveryStageGenerator(_ => 1));
            CompleteBoss(saturated);
            saturated.ChooseReward(0);
            saturated.Step(in moveRight);
            Assert.AreEqual(
                int.MaxValue / SimSpace.TicksPerSecond,
                saturated.Battle.PlayerX,
                "The movement numerator must saturate instead of overflowing.");
        }

        [Test]
        public void PassiveRewardMutationsExpireWhenRunRestarts()
        {
            AssertPassiveExpires(RewardType.FireRateUp);
            AssertPassiveExpires(RewardType.DamageUp);
            AssertPassiveExpires(RewardType.MoveSpeedUp);
        }

        [Test]
        public void MaxPerRunExcludesAcquiredRewardBeforeDeterministicDraw()
        {
            RewardCatalog rewards = CappedCatalog();
            IStageGenerator generator = new BossEveryStageGenerator(_ => 1);
            ulong seed = FindSeedOfferingCappedReward(rewards, generator);
            RunManager first = CreateRun(seed, rewards, generator);
            RunManager second = CreateRun(seed, rewards, generator);

            CompleteBoss(first);
            CompleteBoss(second);
            int firstCappedIndex = FindRewardOption(first, "capped");
            int secondCappedIndex = FindRewardOption(second, "capped");
            Assert.AreEqual(firstCappedIndex, secondCappedIndex);

            first.ChooseReward(firstCappedIndex);
            second.ChooseReward(secondCappedIndex);
            CompleteBoss(first);
            CompleteBoss(second);

            for (int i = 0; i < first.RewardOptions.Count; i++)
            {
                Assert.AreNotEqual("capped", first.RewardOptions[i].Id);
                Assert.AreEqual(
                    first.RewardOptions[i].Id,
                    second.RewardOptions[i].Id,
                    "The same seed and acquisition history must produce the same filtered draw.");
            }
        }

        [Test]
        public void MissingMaxPerRunRemainsUnlimited()
        {
            var rewards = new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    Reward("unlimited", RewardType.Capsules, 1),
                    Reward("fallback_a", RewardType.Capsules, 1),
                    Reward("fallback_b", RewardType.Capsules, 1)
                });
            RunManager run = CreateRun(
                123UL,
                rewards,
                new BossEveryStageGenerator(_ => 1));

            CompleteBoss(run);
            run.ChooseReward(FindRewardOption(run, "unlimited"));
            CompleteBoss(run);

            Assert.GreaterOrEqual(FindRewardOption(run, "unlimited"), 0);
        }

        [Test]
        public void RestartClearsMaxPerRunAcquisitionCounts()
        {
            RewardCatalog rewards = CappedCatalog();
            var generator = new RewardThenLethalGenerator("lethal");
            ulong seed = FindSeedOfferingCappedReward(rewards, generator);
            BattleSimConfig config = Config();
            config.StartingShieldStock = 0;
            var lethal = new EnemyDefinition(
                "lethal", 1, 100, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 1);
            BattleContent content = Content(
                new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0),
                lethal);
            RunManager run = new RunManager(
                seed,
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
                new RunProgressionConfig(
                    RunProgressionConfig.DefaultBiomeCount,
                    1));

            CompleteBoss(run);
            run.ChooseReward(FindRewardOption(run, "capped"));
            InputCommand none = InputCommand.None;
            Step(run, 12, in none);
            Assert.AreEqual(RunState.RunOver, run.State);

            run.Restart(seed);
            CompleteBoss(run);

            Assert.GreaterOrEqual(
                FindRewardOption(run, "capped"),
                0,
                "Restart must restore the initial per-run reward eligibility.");
        }

        static void AssertPassiveExpires(RewardType type)
        {
            BattleSimConfig config = Config();
            config.StartingShieldStock = 0;
            config.PlayerSpeedNumerator = 0;
            config.PlayerSpeedDenominator = SimSpace.TicksPerSecond;
            config.MainShotMinimumFireIntervalTicks = 1;
            var lethal = new EnemyDefinition(
                "lethal", 1, 100, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 1);
            var weapon = new WeaponDefinition("shot", 1, 6, 100, 1, 0, 0);
            BattleContent content = Content(weapon, lethal);
            RunManager run = CreateRun(
                type,
                amount: 1,
                config,
                content,
                new RewardThenLethalGenerator(lethal.Id));

            CompleteBoss(run);
            run.ChooseReward(0);
            InputCommand none = InputCommand.None;
            Step(run, 12, in none);
            Assert.AreEqual(RunState.RunOver, run.State);

            run.Restart(999UL);
            if (type == RewardType.FireRateUp)
            {
                var fire = new InputCommand(0, 0, true);
                Step(run, 11, in fire);
                Assert.AreEqual(2L, run.Battle.Statistics.ShotsFired);
            }
            else if (type == RewardType.DamageUp)
            {
                CompleteBoss(run);
                Assert.AreEqual(2L, run.Battle.Statistics.ShotsHit);
            }
            else
            {
                var moveRight = new InputCommand(1, 0, false);
                Step(run, SimSpace.TicksPerSecond, in moveRight);
                Assert.AreEqual(0, run.Battle.PlayerX);
            }
        }

        static RunManager CreateRun(
            RewardType type,
            int amount,
            BattleSimConfig config,
            WeaponDefinition weapon,
            IStageGenerator generator)
        {
            return CreateRun(
                type, amount, config, Content(weapon), generator);
        }

        static RunManager CreateRun(
            RewardType type,
            int amount,
            BattleSimConfig config,
            BattleContent content,
            IStageGenerator generator)
        {
            var rewards = new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    Reward("passive_a", type, amount),
                    Reward("passive_b", type, amount),
                    Reward("passive_c", type, amount)
                });
            return new RunManager(
                123UL,
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
                new RunProgressionConfig(
                    RunProgressionConfig.DefaultBiomeCount,
                    1));
        }

        static RunManager CreateRun(
            ulong seed,
            RewardCatalog rewards,
            IStageGenerator generator)
        {
            return new RunManager(
                seed,
                generator,
                Config(),
                Content(new WeaponDefinition("shot", 1, 1, 100, 1, 0, 0)),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                1,
                1,
                new RunProgressionConfig(
                    RunProgressionConfig.DefaultBiomeCount,
                    1));
        }

        static RewardCatalog CappedCatalog()
        {
            return new RewardCatalog(
                RunManager.RewardOptionCount,
                new[]
                {
                    Reward(
                        "capped",
                        RewardType.Capsules,
                        1,
                        maxPerRun: 1,
                        weight: 100),
                    Reward("fallback_a", RewardType.Capsules, 1),
                    Reward("fallback_b", RewardType.Capsules, 1),
                    Reward("fallback_c", RewardType.Capsules, 1)
                });
        }

        static ulong FindSeedOfferingCappedReward(
            RewardCatalog rewards,
            IStageGenerator generator)
        {
            for (ulong seed = 0; seed < 1024; seed++)
            {
                RunManager run = CreateRun(seed, rewards, generator);
                CompleteBoss(run);
                if (FindRewardOption(run, "capped") >= 0)
                    return seed;
            }

            Assert.Fail("No deterministic test seed offered the capped reward.");
            return 0;
        }

        static int FindRewardOption(RunManager run, string id)
        {
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                if (run.RewardOptions[i].Id == id)
                    return i;
            }

            return -1;
        }

        static RewardDefinition Reward(
            string id,
            RewardType type,
            int amount,
            int? maxPerRun = null,
            int weight = 1)
        {
            return new RewardDefinition(
                id,
                type,
                PowerUpSlot.MainShot,
                amount,
                weight,
                1,
                int.MaxValue,
                maxPerRun);
        }

        static BattleContent Content(
            WeaponDefinition weapon,
            params EnemyDefinition[] enemies)
        {
            return new BattleContent(
                enemies,
                new[] { weapon },
                weapon.Id);
        }

        static BattleSimConfig Config()
        {
            return new BattleSimConfig
            {
                PlayerSpeedNumerator = 0,
                PlayerSpeedDenominator = SimSpace.TicksPerSecond,
                PlayerBulletSpeedNumerator = 100,
                PlayerBulletSpeedDenominator = 1,
                FireIntervalTicks = 6,
                MaxBullets = 512,
                PlayerMinX = int.MinValue,
                PlayerMaxX = int.MaxValue,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 10000,
                EnemyDespawnX = -10000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 50,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                MainShotRapidFireStartLevel = 3,
                MainShotFireIntervalReductionPerLevel = 1,
                MainShotMinimumFireIntervalTicks = 1,
                EnemyBulletDamage = 0,
                MaxEnemyBullets = 0
            };
        }

        static void CompleteBoss(RunManager run)
        {
            var fire = new InputCommand(0, 0, true);
            for (int i = 0; i < 4000; i++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    if (run.RewardSelectionKind
                        == RewardSelectionKind.Main)
                        break;
                    run.ChooseReward(0);
                    continue;
                }
                run.Step(in fire);
            }
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RewardSelectionKind.Main,
                run.RewardSelectionKind);
        }

        static void Step(
            RunManager run,
            int count,
            in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                run.Step(in input);
        }

        static StageSegment Segment(
            string id,
            int lengthTicks,
            params SpawnEvent[] spawns)
        {
            return new StageSegment(
                id, lengthTicks, spawns, 1, 1, new[] { 1 });
        }

        static BossPhase[] BossPhases()
        {
            return new[] { new BossPhase(999, 1, 1, 1) };
        }

        sealed class BossEveryStageGenerator : IStageGenerator
        {
            readonly Func<int, int> _hpForStage;

            public BossEveryStageGenerator(Func<int, int> hpForStage)
            {
                _hpForStage = hpForStage;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return new StagePlan(
                    new[] { Segment("intro", 100) },
                    "boss",
                    1,
                    1,
                    1,
                    _hpForStage(stageIndex),
                    0,
                    0,
                    5000,
                    BossPhases());
            }
        }

        sealed class RewardThenLethalGenerator : IStageGenerator
        {
            readonly string _lethalEnemyId;

            public RewardThenLethalGenerator(string lethalEnemyId)
            {
                _lethalEnemyId = lethalEnemyId;
            }

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                if (stageIndex == 1)
                {
                    return new StagePlan(
                        new[] { Segment("intro", 100) },
                        "boss",
                        1,
                        1,
                        1,
                        2,
                        0,
                        0,
                        5000,
                        BossPhases());
                }

                return new StagePlan(
                    new[]
                    {
                        Segment(
                            "lethal",
                            20,
                            new SpawnEvent(12, _lethalEnemyId, 0, 0))
                    },
                    "legacy",
                    1,
                    1,
                    1);
            }
        }
    }
}
