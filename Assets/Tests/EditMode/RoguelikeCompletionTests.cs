using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class RoguelikeCompletionTests
    {
        [Test]
        public void RewardRerollSpendsCapsulesDeterministicallyAndRecordsReplayEvents()
        {
            RunManager first = CreateRerollRun(0x7201UL);
            RunManager second = CreateRerollRun(0x7201UL);

            DriveToReward(first, RewardSelectionKind.MidStage);
            DriveToReward(second, RewardSelectionKind.MidStage);
            Assert.IsFalse(first.RerollRewardOptions());
            Assert.AreEqual(0, first.CapsuleBalance);
            Assert.IsTrue(first.ChooseReward(0));
            Assert.IsTrue(second.ChooseReward(0));

            DriveToReward(first, RewardSelectionKind.Main);
            DriveToReward(second, RewardSelectionKind.Main);
            Assert.AreEqual(5, first.CapsuleBalance);
            Assert.AreEqual(2, first.RewardRerollCost);
            Assert.IsTrue(first.CanRerollRewardOptions);

            Assert.IsTrue(first.RerollRewardOptions());
            Assert.IsTrue(second.RerollRewardOptions());
            AssertRewardOptionsEqual(first, second);
            Assert.AreEqual(3, first.CapsuleBalance);

            Assert.IsTrue(first.RerollRewardOptions());
            Assert.IsTrue(second.RerollRewardOptions());
            AssertRewardOptionsEqual(first, second);
            Assert.AreEqual(1, first.CapsuleBalance);
            Assert.IsFalse(first.CanRerollRewardOptions);
            Assert.IsFalse(first.RerollRewardOptions());
            Assert.AreEqual(3, first.RewardDecisionHistory.Count);
            Assert.AreEqual(
                RewardDecisionKind.Select,
                first.RewardDecisionHistory[0].DecisionKind);
            Assert.AreEqual(
                RewardDecisionKind.Reroll,
                first.RewardDecisionHistory[1].DecisionKind);
            Assert.AreEqual(
                RewardDecisionKind.Reroll,
                first.RewardDecisionHistory[2].DecisionKind);

            var recorder = new InputRecorder(first);
            InputCommand input = new InputCommand(0, 0, true);
            recorder.Record(in input);
            InputRecordingData recording = recorder.Export();
            var playback = new InputPlayback(recording);
            Assert.AreEqual(3, playback.RewardDecisions.Count);
            Assert.AreEqual(
                RewardDecisionKind.Reroll,
                playback.RewardDecisions[2].DecisionKind);
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(new InputRecordingData
                {
                    schemaVersion = 12
                }));

            AssertRunHashEqual(first, second);
        }

        [Test]
        public void StageOrderKeepsEndpointsAndShufflesThemesTwoThroughFour()
        {
            RunManager first = CreateStageOrderRun(0UL);
            RunManager same = CreateStageOrderRun(0UL);
            AssertStageOrderEqual(first, same);
            Assert.AreEqual(1, first.StageThemeOrder[0]);
            Assert.AreEqual(5, first.StageThemeOrder[4]);

            int[] middle =
            {
                first.StageThemeOrder[1],
                first.StageThemeOrder[2],
                first.StageThemeOrder[3]
            };
            Array.Sort(middle);
            CollectionAssert.AreEqual(
                new[] { 2, 3, 4 },
                middle);

            bool foundDifferent = false;
            for (ulong seed = 1; seed < 64; seed++)
            {
                RunManager candidate =
                    CreateStageOrderRun(seed);
                if (candidate.StageThemeOrder[1]
                        != first.StageThemeOrder[1]
                    || candidate.StageThemeOrder[2]
                        != first.StageThemeOrder[2]
                    || candidate.StageThemeOrder[3]
                        != first.StageThemeOrder[3])
                {
                    foundDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(foundDifferent);

            DriveToReward(first, RewardSelectionKind.Main);
            Assert.IsTrue(first.ChooseReward(0));
            Assert.IsTrue(first.ChooseContract(0));
            Assert.AreEqual(2, first.BiomeIndex);
            Assert.AreEqual(2, first.Difficulty);
            Assert.AreEqual(
                first.StageThemeOrder[1],
                first.ThemeStageIndex);
            Assert.AreEqual(
                "theme_"
                    + first.ThemeStageIndex
                    + "_difficulty_2",
                first.StagePlan.ThemeId);
        }

        [Test]
        public void ContractDestinationsAreDistinctDeterministicAndSuspendable()
        {
            RunManager first = CreateStageOrderRun(0x8601UL);
            RunManager second = CreateStageOrderRun(0x8601UL);
            DriveToReward(first, RewardSelectionKind.Main);
            DriveToReward(second, RewardSelectionKind.Main);
            Assert.IsTrue(first.ChooseReward(0));
            Assert.IsTrue(second.ChooseReward(0));

            Assert.AreEqual(2, first.ContractOptions.Count);
            Assert.AreNotEqual(
                first.ContractOptions[0].DestinationThemeId,
                first.ContractOptions[1].DestinationThemeId);
            for (int i = 0; i < first.ContractOptions.Count; i++)
            {
                Assert.AreEqual(
                    first.ContractOptions[i].DestinationThemeId,
                    second.ContractOptions[i].DestinationThemeId);
                Assert.AreEqual(
                    first.ContractOptions[i]
                        .DestinationThemeStageIndex,
                    second.ContractOptions[i]
                        .DestinationThemeStageIndex);
            }

            ContractOption selected = first.ContractOptions[1];
            Assert.IsTrue(first.ChooseContract(1));
            Assert.IsTrue(second.ChooseContract(1));
            Assert.AreEqual(
                selected.DestinationThemeStageIndex,
                first.ThemeStageIndex);
            Assert.AreEqual(
                selected.DestinationThemeId,
                first.StagePlan.ThemeId);
            AssertRunHashEqual(first, second);

            RunSuspendData suspend = first.ExportSuspendData();
            Assert.AreEqual(
                selected.DestinationThemeId,
                suspend.contractChoices[0]
                    .destinationThemeId);
            Assert.AreEqual(
                selected.DestinationThemeStageIndex,
                suspend.contractChoices[0]
                    .destinationThemeStageIndex);
            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new StageOrderGenerator(),
                CreateStageOrderConfig(),
                CreateStageOrderContent(),
                PowerUpGauge.CreateDefault(),
                CreateStageOrderRewards(),
                null);
            AssertStageOrderEqual(first, resumed);
            AssertRunHashEqual(first, resumed);

            var recorder = new InputRecorder(first);
            recorder.Record(InputCommand.None);
            InputRecordingData recording = recorder.Export();
            var playback = new InputPlayback(recording);
            Assert.AreEqual(
                selected.DestinationThemeId,
                playback.ContractChoices[0]
                    .DestinationThemeId);
        }

        static RunManager CreateRerollRun(ulong seed)
        {
            var rewards = new RewardCatalog(
                3,
                new[]
                {
                    CapsuleReward("capsules_a"),
                    CapsuleReward("capsules_b"),
                    CapsuleReward("capsules_c"),
                    CapsuleReward("capsules_d"),
                    CapsuleReward("capsules_e")
                },
                rerollCost: 2);
            return CreateRun(
                seed,
                rewards,
                new RunProgressionConfig(2, 3));
        }

        static RunManager CreateStageOrderRun(ulong seed)
        {
            return CreateRun(
                seed,
                CreateStageOrderRewards(),
                new RunProgressionConfig(5, 1));
        }

        static RewardCatalog CreateStageOrderRewards()
        {
            return new RewardCatalog(
                3,
                new[]
                {
                    CapsuleReward("capsules_a"),
                    CapsuleReward("capsules_b"),
                    CapsuleReward("capsules_c"),
                    CapsuleReward("capsules_d")
                });
        }

        static RunManager CreateRun(
            ulong seed,
            RewardCatalog rewards,
            RunProgressionConfig progression)
        {
            BattleSimConfig config = CreateStageOrderConfig();
            BattleContent content = CreateStageOrderContent();
            return new RunManager(
                seed,
                new StageOrderGenerator(),
                config,
                content,
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                1,
                1,
                progression);
        }

        static BattleSimConfig CreateStageOrderConfig()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerMinX = -10_000;
            config.PlayerMaxX = 10_000;
            config.PlayerMinY = -10_000;
            config.PlayerMaxY = 10_000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.BulletDespawnX = 20_000;
            config.EnemyDespawnX = -20_000;
            config.StartingShieldStock = 5;
            config.MaxShieldStock = 5;
            return config;
        }

        static BattleContent CreateStageOrderContent()
        {
            var weapon = new WeaponDefinition(
                "req072_shot",
                1,
                1,
                256,
                1,
                0,
                0);
            return new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id);
        }

        static RewardDefinition CapsuleReward(string id)
        {
            return new RewardDefinition(
                id,
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                5,
                1,
                1,
                99);
        }

        static void DriveToReward(
            RunManager run,
            RewardSelectionKind kind)
        {
            InputCommand fire =
                new InputCommand(0, 0, true);
            for (int guard = 0; guard < 5_000; guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    if (run.RewardSelectionKind == kind)
                        return;
                    Assert.IsTrue(run.ChooseReward(0));
                    continue;
                }
                if (run.State == RunState.AwaitingContract)
                {
                    Assert.IsTrue(run.ChooseContract(0));
                    continue;
                }
                Assert.AreEqual(RunState.Playing, run.State);
                run.Step(in fire);
            }
            Assert.Fail("Reward boundary was not reached.");
        }

        static void AssertRewardOptionsEqual(
            RunManager expected,
            RunManager actual)
        {
            Assert.AreEqual(
                expected.RewardOptions.Count,
                actual.RewardOptions.Count);
            for (int i = 0; i < expected.RewardOptions.Count; i++)
                Assert.AreEqual(
                    expected.RewardOptions[i].Id,
                    actual.RewardOptions[i].Id);
        }

        static void AssertStageOrderEqual(
            RunManager expected,
            RunManager actual)
        {
            Assert.AreEqual(
                expected.StageThemeOrder.Count,
                actual.StageThemeOrder.Count);
            for (int i = 0; i < expected.StageThemeOrder.Count; i++)
                Assert.AreEqual(
                    expected.StageThemeOrder[i],
                    actual.StageThemeOrder[i]);
        }

        static void AssertRunHashEqual(
            RunManager expected,
            RunManager actual)
        {
            var expectedHasher =
                new DeterminismAuditHasher();
            var actualHasher =
                new DeterminismAuditHasher();
            expectedHasher.FoldRunState(expected);
            actualHasher.FoldRunState(actual);
            Assert.AreEqual(
                expectedHasher.Hash,
                actualHasher.Hash);
        }

        sealed class StageOrderGenerator : IStageGenerator
        {
            static readonly BossPhase[] Phases =
            {
                new BossPhase(999, 1, 1, 1)
            };

            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "theme_segment_" + stageIndex,
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "theme_boss_" + stageIndex,
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    512,
                    Phases,
                    "theme_"
                        + stageIndex
                        + "_difficulty_"
                        + difficulty);
            }
        }
    }
}
