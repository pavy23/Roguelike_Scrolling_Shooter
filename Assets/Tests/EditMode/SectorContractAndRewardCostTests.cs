using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class SectorContractAndRewardCostTests
    {
        [Test]
        public void ContractCandidatesAreDeterministicAndAlwaysContainStandard()
        {
            ContractCatalog contracts = CreateContracts();
            RunManager first = CreateRun(
                0x700AUL,
                CreateBothPoolRewards(),
                contracts);
            RunManager second = CreateRun(
                0x700AUL,
                CreateBothPoolRewards(),
                contracts);

            DriveToMainReward(first);
            DriveToMainReward(second);
            Assert.IsTrue(first.ChooseReward(0));
            Assert.IsTrue(second.ChooseReward(0));

            Assert.AreEqual(
                RunState.AwaitingContract,
                first.State);
            Assert.AreEqual(
                first.ContractOptions.Count,
                second.ContractOptions.Count);
            Assert.GreaterOrEqual(
                first.ContractOptions.Count,
                RunManager.MinimumContractOptionCount);
            Assert.LessOrEqual(
                first.ContractOptions.Count,
                RunManager.MaximumContractOptionCount);
            Assert.AreEqual(
                "standard_route",
                first.ContractOptions[0].Id);
            Assert.IsTrue(first.ContractOptions[0].IsNeutral);
            for (int i = 0; i < first.ContractOptions.Count; i++)
                Assert.AreEqual(
                    first.ContractOptions[i].Id,
                    second.ContractOptions[i].Id);

            Assert.IsFalse(first.ChooseContract(-1));
            Assert.IsFalse(first.ChooseContract(
                first.ContractOptions.Count));
            Assert.AreEqual(
                RunState.AwaitingContract,
                first.State);
            Assert.IsTrue(first.ChooseContract(1));
            Assert.IsTrue(second.ChooseContract(1));
            Assert.AreEqual(
                first.ActiveContract.Id,
                second.ActiveContract.Id);
            Assert.AreEqual(2, first.BiomeIndex);
            Assert.AreEqual(1, first.ContractChoiceHistory.Count);
            Assert.AreEqual(
                first.ActiveContract.Id,
                first.ContractChoiceHistory[0].ContractId);
            AssertRunHashEqual(first, second);
        }

        [Test]
        public void MidAndMainRewardPoolsDoNotCross()
        {
            RunManager run = CreateRun(
                0x700BUL,
                CreateSeparatedRewards(),
                CreateContracts());

            DriveToReward(run, RewardSelectionKind.MidStage);
            for (int i = 0; i < run.RewardOptions.Count; i++)
                StringAssert.DoesNotStartWith(
                    "main_",
                    run.RewardOptions[i].Id);
            Assert.IsTrue(run.ChooseReward(0));

            DriveToReward(run, RewardSelectionKind.Main);
            for (int i = 0; i < run.RewardOptions.Count; i++)
                StringAssert.DoesNotStartWith(
                    "mid_",
                    run.RewardOptions[i].Id);
        }

        [Test]
        public void RewardCostsApplyAndClampCurrentStocks()
        {
            RunManager run = CreateRun(
                0x700CUL,
                CreateCostRewards(),
                CreateContracts());
            DriveToMainReward(run);
            int costlyIndex = FindReward(run, "costly");
            Assert.GreaterOrEqual(costlyIndex, 0);

            RewardOption option = run.RewardOptions[costlyIndex];
            Assert.AreEqual(1, option.Gains.Count);
            Assert.AreEqual(4, option.Costs.Count);
            Assert.IsTrue(run.ChooseReward(costlyIndex));

            Assert.AreEqual(3, run.MaxShieldStock);
            Assert.AreEqual(3, run.Battle.ShieldStock);
            Assert.AreEqual(1, run.MaxBombStock);
            Assert.AreEqual(1, run.Battle.BombStock);
            Assert.AreEqual(
                7,
                run.CapsuleDropWeightReduction);
            Assert.AreEqual(
                RunState.AwaitingContract,
                run.State);
            Assert.IsTrue(run.ChooseContract(0));
            int before = run.Battle.PlayerX;
            run.Step(new InputCommand(1, 0, false));
            Assert.Less(
                run.Battle.PlayerX - before,
                13 * SimSpace.SubUnitsPerWorldUnit
                    / SimSpace.TicksPerSecond);
        }

        [Test]
        public void ReplayVersionElevenIsExplicitlyRejected()
        {
            var old = new InputRecordingData
            {
                schemaVersion = 11
            };
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(old));
        }

        [Test]
        public void ContractActivationBansSurviveSuspendAndReplay()
        {
            ContractCatalog contracts = CreateRestrictionContracts();
            RewardCatalog rewards = CreateBothPoolRewards();
            RunManager source = CreateRun(0x7094UL, rewards, contracts);
            DriveToMainReward(source);
            Assert.IsTrue(source.ChooseReward(0));
            Assert.IsTrue(source.ChooseContract(1));

            Assert.IsTrue(source.ActiveContract.GaugeActivationBanned);
            Assert.IsTrue(source.ActiveContract.OptionActivationBanned);
            Assert.IsTrue(source.ActiveContract.ShieldActivationBanned);
            Assert.IsFalse(source.ActiveContract.IsNeutral);
            Assert.IsTrue(HasEffect(
                source.ActiveContract,
                ContractEffectType.GaugeActivationBanned));
            Assert.IsTrue(HasEffect(
                source.ActiveContract,
                ContractEffectType.OptionActivationBanned));
            Assert.IsTrue(HasEffect(
                source.ActiveContract,
                ContractEffectType.ShieldActivationBanned));

            RunSuspendData suspend = source.ExportSuspendData();
            RunManager resumed = ResumeRun(
                suspend,
                rewards,
                contracts);
            Assert.IsTrue(resumed.PowerUpGauge.GaugeActivationBanned);
            Assert.IsTrue(resumed.PowerUpGauge.OptionActivationBanned);
            Assert.IsTrue(resumed.PowerUpGauge.ShieldActivationBanned);

            var recorder = new InputRecorder();
            var activate = new InputCommand(0, 0, false, true);
            recorder.Record(in activate);
            source.Step(in activate);
            foreach (InputCommand input in new InputPlayback(recorder.Export()))
                resumed.Step(in input);

            Assert.AreEqual(
                PowerUpActivationResult.ContractGaugeActivationBanned,
                source.PowerUpGauge.LastActivationResult);
            Assert.AreEqual(
                source.PowerUpGauge.LastActivationResult,
                resumed.PowerUpGauge.LastActivationResult);
            AssertRunHashEqual(source, resumed);
        }

        static RunManager CreateRun(
            ulong seed,
            RewardCatalog rewards,
            ContractCatalog contracts)
        {
            BattleSimConfig config = CreateConfig();
            BattleContent content = CreateContent();
            return new RunManager(
                seed,
                new ContractTestGenerator(),
                config,
                content,
                PowerUpGauge.CreateDefault(),
                rewards,
                contracts);
        }

        static RunManager ResumeRun(
            RunSuspendData data,
            RewardCatalog rewards,
            ContractCatalog contracts)
        {
            return RunManager.ResumeFromSuspendData(
                data,
                new ContractTestGenerator(),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                contracts);
        }

        static BattleSimConfig CreateConfig()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMinX = -10_000;
            config.PlayerMaxX = 10_000;
            config.PlayerMinY = -10_000;
            config.PlayerMaxY = 10_000;
            config.BulletDespawnX = 20_000;
            config.EnemyDespawnX = -20_000;
            config.StartingShieldStock = 5;
            config.MaxShieldStock = 5;
            config.StartingBombStock = 3;
            config.MaxBombStock = 3;
            return config;
        }

        static BattleContent CreateContent()
        {
            var weapon = new WeaponDefinition(
                "contract_test_shot",
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
            return content;
        }

        static void DriveToMainReward(RunManager run)
        {
            DriveToReward(run, RewardSelectionKind.MidStage);
            Assert.IsTrue(run.ChooseReward(0));
            DriveToReward(run, RewardSelectionKind.Main);
        }

        static void DriveToReward(
            RunManager run,
            RewardSelectionKind kind)
        {
            var fire = new InputCommand(0, 0, true);
            for (int guard = 0; guard < 2000; guard++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    if (run.RewardSelectionKind == kind)
                        return;
                    Assert.IsTrue(run.ChooseReward(0));
                    continue;
                }
                Assert.AreEqual(RunState.Playing, run.State);
                run.Step(in fire);
            }
            Assert.Fail($"Did not reach {kind} reward.");
        }

        static int FindReward(RunManager run, string id)
        {
            for (int i = 0; i < run.RewardOptions.Count; i++)
                if (run.RewardOptions[i].Id == id)
                    return i;
            return -1;
        }

        static RewardCatalog CreateBothPoolRewards()
        {
            return new RewardCatalog(
                3,
                new[]
                {
                    Reward("both_a", RewardPool.Both),
                    Reward("both_b", RewardPool.Both),
                    Reward("both_c", RewardPool.Both),
                    Reward("both_d", RewardPool.Both)
                });
        }

        static RewardCatalog CreateSeparatedRewards()
        {
            return new RewardCatalog(
                3,
                new[]
                {
                    Reward("mid_a", RewardPool.Mid),
                    Reward("mid_b", RewardPool.Mid),
                    Reward("mid_c", RewardPool.Mid),
                    Reward("main_a", RewardPool.Main),
                    Reward("main_b", RewardPool.Main),
                    Reward("main_c", RewardPool.Main),
                    Reward("both_a", RewardPool.Both)
                });
        }

        static RewardCatalog CreateCostRewards()
        {
            var costs = new[]
            {
                new RewardCostDefinition(
                    RewardEffectType.ShieldMaxDown,
                    2),
                new RewardCostDefinition(
                    RewardEffectType.MoveSpeedDown,
                    1),
                new RewardCostDefinition(
                    RewardEffectType.CapsuleDropWeightDown,
                    7),
                new RewardCostDefinition(
                    RewardEffectType.BombMaxDown,
                    2)
            };
            return new RewardCatalog(
                3,
                new[]
                {
                    new RewardDefinition(
                        "costly",
                        RewardType.Capsules,
                        PowerUpSlot.MainShot,
                        1,
                        1,
                        1,
                        5,
                        pool: RewardPool.Main,
                        costs: costs),
                    Reward("main_a", RewardPool.Main),
                    Reward("main_b", RewardPool.Main),
                    Reward("mid_a", RewardPool.Mid),
                    Reward("mid_b", RewardPool.Mid)
                });
        }

        static RewardDefinition Reward(
            string id,
            RewardPool pool)
        {
            return new RewardDefinition(
                id,
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                1,
                1,
                1,
                5,
                pool: pool);
        }

        static ContractCatalog CreateContracts()
        {
            return new ContractCatalog(
                "standard_route",
                2,
                3,
                new[]
                {
                    new ContractDefinition(
                        "standard_route",
                        1,
                        ContractRiskTier.Safe),
                    new ContractDefinition(
                        "dense_salvage",
                        5,
                        ContractRiskTier.Low,
                        enemyDensityNumerator: 3,
                        enemyDensityDenominator: 2,
                        capsuleDropNumerator: 5,
                        capsuleDropDenominator: 4,
                        scoreMultiplierNumerator: 5,
                        scoreMultiplierDenominator: 4),
                    new ContractDefinition(
                        "bomb_run",
                        3,
                        ContractRiskTier.High,
                        bombDropNumerator: 2,
                        guaranteedBombDrop: true,
                        rewardOptionCountDelta: 1),
                    new ContractDefinition(
                        "hazard_pay",
                        2,
                        ContractRiskTier.Extreme,
                        gimmickIntensityNumerator: 3,
                        gimmickIntensityDenominator: 2,
                        scoreMultiplierNumerator: 3,
                        scoreMultiplierDenominator: 2)
                });
        }

        static ContractCatalog CreateRestrictionContracts()
        {
            return new ContractCatalog(
                "standard_route",
                2,
                2,
                new[]
                {
                    new ContractDefinition(
                        "standard_route",
                        1,
                        ContractRiskTier.Safe),
                    new ContractDefinition(
                        "spartan_lock",
                        1,
                        ContractRiskTier.Extreme,
                        scoreMultiplierNumerator: 2,
                        gaugeActivationBanned: true,
                        optionActivationBanned: true,
                        shieldActivationBanned: true)
                });
        }

        static bool HasEffect(
            ContractDefinition contract,
            ContractEffectType effectType)
        {
            for (int i = 0; i < contract.Effects.Count; i++)
                if (contract.Effects[i].Type == effectType)
                    return true;
            return false;
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

        sealed class ContractTestGenerator : IStageGenerator
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
                    "contract_room",
                    1,
                    Array.Empty<SpawnEvent>(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "contract_boss",
                    1,
                    1,
                    1,
                    1,
                    0,
                    0,
                    512,
                    Phases);
            }
        }
    }
}
