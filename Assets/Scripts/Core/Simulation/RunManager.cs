using System;
using System.Collections.Generic;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public enum RunState
    {
        Playing = 0,
        RunOver = 1,
        /// <summary>보스 격파 후 보상 선택 대기 (REQ-007 요청 3). ChooseReward로 재개.</summary>
        AwaitingReward = 2
    }

    public enum RewardType
    {
        /// <summary>캡슐 n개 즉시 수집 (커서 전진).</summary>
        Capsules = 0,
        /// <summary>지정 슬롯 레벨 +1 (최대치 클램프).</summary>
        SlotLevel = 1,
        /// <summary>런 최대 HP +1 — 다음 스테이지부터 적용.</summary>
        RepairHp = 2
    }

    /// <summary>보상 후보 하나.</summary>
    public readonly struct RewardOption
    {
        public RewardOption(RewardType type, PowerUpSlot slot, int amount)
            : this(null, type, slot, amount)
        {
        }

        public RewardOption(string id, RewardType type, PowerUpSlot slot, int amount)
        {
            Id = id;
            Type = type;
            Slot = slot;
            Amount = amount;
        }

        public string Id { get; }
        public RewardType Type { get; }
        public PowerUpSlot Slot { get; }
        public int Amount { get; }
    }

    /// <summary>Immutable rewards.json entry used by deterministic run selection.</summary>
    public readonly struct RewardDefinition
    {
        public RewardDefinition(
            string id,
            RewardType type,
            PowerUpSlot slot,
            int amount,
            int weight,
            int stageIndexMin,
            int stageIndexMax)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Reward id cannot be empty.", nameof(id));
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (weight < 1)
                throw new ArgumentOutOfRangeException(nameof(weight));
            if (stageIndexMin < 1)
                throw new ArgumentOutOfRangeException(nameof(stageIndexMin));
            if (stageIndexMax < stageIndexMin)
                throw new ArgumentOutOfRangeException(nameof(stageIndexMax));

            Id = id;
            Type = type;
            Slot = slot;
            Amount = amount;
            Weight = weight;
            StageIndexMin = stageIndexMin;
            StageIndexMax = stageIndexMax;
        }

        public string Id { get; }
        public RewardType Type { get; }
        public PowerUpSlot Slot { get; }
        public int Amount { get; }
        public int Weight { get; }
        public int StageIndexMin { get; }
        public int StageIndexMax { get; }
    }

    /// <summary>Immutable reward pool parsed from rewards.json.</summary>
    public sealed class RewardCatalog
    {
        readonly IReadOnlyList<RewardDefinition> _all;

        public RewardCatalog(
            int optionCount,
            IReadOnlyList<RewardDefinition> rewards)
        {
            if (optionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(optionCount));
            if (rewards == null)
                throw new ArgumentNullException(nameof(rewards));
            if (rewards.Count < optionCount)
                throw new ArgumentException(
                    "The reward pool cannot be smaller than the option count.",
                    nameof(rewards));

            var copy = new RewardDefinition[rewards.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = rewards[i];

            OptionCount = optionCount;
            _all = Array.AsReadOnly(copy);
        }

        public int OptionCount { get; }
        public IReadOnlyList<RewardDefinition> All => _all;

        public IReadOnlyList<RewardDefinition> EligibleForStage(int stageIndex)
        {
            if (stageIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(stageIndex));

            var eligible = new List<RewardDefinition>();
            for (int i = 0; i < _all.Count; i++)
            {
                RewardDefinition reward = _all[i];
                if (stageIndex >= reward.StageIndexMin
                    && stageIndex <= reward.StageIndexMax)
                    eligible.Add(reward);
            }
            return eligible.AsReadOnly();
        }
    }

    /// <summary>Configurable integer linear difficulty curve for successive stages.</summary>
    public sealed class StageDifficultyCurve
    {
        public StageDifficultyCurve(
            int initialDifficulty,
            int increasePerStage,
            int maximumDifficulty)
        {
            if (initialDifficulty < 1)
                throw new ArgumentOutOfRangeException(nameof(initialDifficulty));
            if (increasePerStage < 0)
                throw new ArgumentOutOfRangeException(nameof(increasePerStage));
            if (maximumDifficulty < initialDifficulty)
                throw new ArgumentOutOfRangeException(nameof(maximumDifficulty));

            InitialDifficulty = initialDifficulty;
            IncreasePerStage = increasePerStage;
            MaximumDifficulty = maximumDifficulty;
        }

        public int InitialDifficulty { get; }
        public int IncreasePerStage { get; }
        public int MaximumDifficulty { get; }

        public static StageDifficultyCurve CreateDefault()
        {
            return new StageDifficultyCurve(1, 1, 5);
        }

        public int GetDifficulty(int stageIndex)
        {
            if (stageIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(stageIndex));

            long difficulty = InitialDifficulty
                + (long)(stageIndex - 1) * IncreasePerStage;
            return difficulty >= MaximumDifficulty
                ? MaximumDifficulty
                : (int)difficulty;
        }
    }

    /// <summary>
    /// Owns one roguelike run: deterministic stage creation, battle replacement,
    /// death state, and power-up carry into a restarted run.
    /// </summary>
    public sealed class RunManager
    {
        const int BattleSimulationStream = 1;
        const int RewardSelectionStream = 2;
        public const int RewardOptionCount = 3;

        static readonly RewardCatalog BuiltInRewards = new RewardCatalog(
            RewardOptionCount,
            new[]
            {
                new RewardDefinition(
                    "capsules_3", RewardType.Capsules, PowerUpSlot.MainShot,
                    3, 1, 1, int.MaxValue),
                new RewardDefinition(
                    "slot_main_shot_1", RewardType.SlotLevel, PowerUpSlot.MainShot,
                    1, 1, 1, int.MaxValue),
                new RewardDefinition(
                    "slot_missile_1", RewardType.SlotLevel, PowerUpSlot.Missile,
                    1, 1, 1, int.MaxValue),
                new RewardDefinition(
                    "slot_option_1", RewardType.SlotLevel, PowerUpSlot.Option,
                    1, 1, 1, int.MaxValue),
                new RewardDefinition(
                    "slot_shield_1", RewardType.SlotLevel, PowerUpSlot.Shield,
                    1, 1, 1, int.MaxValue),
                new RewardDefinition(
                    "repair_hp_1", RewardType.RepairHp, PowerUpSlot.MainShot,
                    1, 1, 1, int.MaxValue)
            });

        readonly IStageGenerator _stageGenerator;
        readonly BattleSimConfig _battleConfig;
        readonly BattleContent _battleContent;
        readonly MetaProgression _metaProgression;
        readonly StageDifficultyCurve _difficultyCurve;
        readonly RewardCatalog _rewards;
        readonly int[] _powerUpMaxLevels;
        readonly int _initialPlayerMaxHp;

        ulong _runSeed;
        int _stageLengthTicks;

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1.0),
                StageDifficultyCurve.CreateDefault(),
                null)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1.0),
                StageDifficultyCurve.CreateDefault(),
                rewards)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            MetaProgression metaProgression,
            StageDifficultyCurve difficultyCurve)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                null)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            MetaProgression metaProgression,
            StageDifficultyCurve difficultyCurve,
            RewardCatalog rewards)
        {
            _stageGenerator = stageGenerator
                ?? throw new ArgumentNullException(nameof(stageGenerator));
            _battleConfig = (battleConfig
                ?? throw new ArgumentNullException(nameof(battleConfig))).Copy();
            _battleContent = battleContent
                ?? throw new ArgumentNullException(nameof(battleContent));
            PowerUpGauge = powerUpGauge
                ?? throw new ArgumentNullException(nameof(powerUpGauge));
            _metaProgression = metaProgression
                ?? throw new ArgumentNullException(nameof(metaProgression));
            _difficultyCurve = difficultyCurve
                ?? throw new ArgumentNullException(nameof(difficultyCurve));
            _rewards = rewards ?? BuiltInRewards;
            if (_rewards.OptionCount != RewardOptionCount)
                throw new ArgumentException(
                    $"RunManager requires exactly {RewardOptionCount} reward options.",
                    nameof(rewards));
            _initialPlayerMaxHp = _battleConfig.PlayerMaxHp;

            _powerUpMaxLevels = new int[PowerUpGauge.SlotCount];
            for (int i = 0; i < _powerUpMaxLevels.Length; i++)
                _powerUpMaxLevels[i] = PowerUpGauge.GetMaxLevel((PowerUpSlot)i);

            _runSeed = runSeed;
            RunNumber = 1;
            StageIndex = 1;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        public int RunNumber { get; private set; }
        public int StageIndex { get; private set; }
        public RunState State { get; private set; }
        public ulong RunSeed => _runSeed;
        public int Difficulty { get; private set; }
        public StagePlan StagePlan { get; private set; }
        public IBattleSim Battle { get; private set; }
        public PowerUpGauge PowerUpGauge { get; private set; }

        /// <summary>AwaitingReward 상태에서만 유효. 항상 RewardOptionCount개.</summary>
        public IReadOnlyList<RewardOption> RewardOptions => _rewardOptions;
        IReadOnlyList<RewardOption> _rewardOptions = Array.Empty<RewardOption>();

        public void Step(in InputCommand input)
        {
            if (State != RunState.Playing)
                return;

            Battle.Step(in input);
            if (Battle.PlayerHp <= 0)
            {
                State = RunState.RunOver;
                return;
            }

            // 보스전이 있는 스테이지는 StageCleared(보스 격파)로 끝나고 보상 선택으로 넘어간다.
            // 보스 데이터가 없는 플랜(레거시/테스트)은 기존 틱 소진 규칙 유지.
            if (Battle is BattleSim battleSim && battleSim.HasBossBattle)
            {
                if (battleSim.BossDefeated)
                {
                    _rewardOptions = GenerateRewardOptions();
                    State = RunState.AwaitingReward;
                }
                return;
            }

            if (Battle.Tick >= _stageLengthTicks)
                AdvanceStage();
        }

        /// <summary>
        /// 보상을 확정하고 다음 스테이지를 시작한다. 선택은 플레이어 입력이므로
        /// 리플레이 기록 대상이다 (같은 시드 + 같은 선택 = 같은 런).
        /// </summary>
        public void ChooseReward(int optionIndex)
        {
            if (State != RunState.AwaitingReward)
                throw new InvalidOperationException("No reward is awaiting selection.");
            if (optionIndex < 0 || optionIndex >= _rewardOptions.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));

            ApplyReward(_rewardOptions[optionIndex]);
            _rewardOptions = Array.Empty<RewardOption>();
            State = RunState.Playing;
            AdvanceStage();
        }

        void ApplyReward(in RewardOption option)
        {
            switch (option.Type)
            {
                case RewardType.Capsules:
                    for (int i = 0; i < option.Amount; i++)
                        PowerUpGauge.Collect();
                    break;
                case RewardType.SlotLevel:
                {
                    int[] levels = PowerUpGauge.ExportLevels();
                    int slot = (int)option.Slot;
                    levels[slot] = Math.Min(
                        levels[slot] + option.Amount, _powerUpMaxLevels[slot]);
                    PowerUpGauge.ImportLevels(levels);
                    break;
                }
                case RewardType.RepairHp:
                    _battleConfig.PlayerMaxHp += option.Amount;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown reward type {option.Type}.");
            }
        }

        /// <summary>시드·스테이지·주입 카탈로그의 결정론적 가중 비복원 선택.</summary>
        IReadOnlyList<RewardOption> GenerateRewardOptions()
        {
            IReadOnlyList<RewardDefinition> eligible =
                _rewards.EligibleForStage(StageIndex);
            if (eligible.Count < RewardOptionCount)
                throw new InvalidOperationException(
                    $"Stage {StageIndex} has {eligible.Count} eligible rewards; "
                    + $"{RewardOptionCount} are required.");

            var pool = new List<RewardDefinition>(eligible);
            var weights = new List<int>(eligible.Count);
            for (int i = 0; i < eligible.Count; i++)
                weights.Add(eligible[i].Weight);

            Rng rewardRng = new Rng(_runSeed)
                .Fork(RewardSelectionStream)
                .Fork(StageIndex);
            var options = new RewardOption[RewardOptionCount];
            for (int i = 0; i < options.Length; i++)
            {
                int pick = rewardRng.PickWeighted(weights);
                RewardDefinition selected = pool[pick];
                options[i] = new RewardOption(
                    selected.Id,
                    selected.Type,
                    selected.Slot,
                    selected.Amount);

                int last = pool.Count - 1;
                pool[pick] = pool[last];
                weights[pick] = weights[last];
                pool.RemoveAt(last);
                weights.RemoveAt(last);
            }
            return Array.AsReadOnly(options);
        }

        public void Restart(ulong newRunSeed)
        {
            if (State != RunState.RunOver)
                throw new InvalidOperationException(
                    "A run can only restart after it is over.");
            if (RunNumber == int.MaxValue)
                throw new InvalidOperationException("The run counter is exhausted.");

            int[] carriedLevels = _metaProgression.ApplyDeathCarry(
                PowerUpGauge.ExportLevels());
            var nextGauge = new PowerUpGauge(_powerUpMaxLevels);
            nextGauge.ImportLevels(carriedLevels);

            _runSeed = newRunSeed;
            RunNumber++;
            StageIndex = 1;
            State = RunState.Playing;
            _rewardOptions = Array.Empty<RewardOption>();
            _battleConfig.PlayerMaxHp = _initialPlayerMaxHp;
            PowerUpGauge = nextGauge;
            BuildCurrentStage();
        }

        void AdvanceStage()
        {
            if (StageIndex == int.MaxValue)
                throw new InvalidOperationException("The stage counter is exhausted.");
            StageIndex++;
            BuildCurrentStage();
        }

        void BuildCurrentStage()
        {
            Difficulty = _difficultyCurve.GetDifficulty(StageIndex);
            StagePlan = _stageGenerator.Generate(
                _runSeed,
                StageIndex,
                Difficulty)
                ?? throw new InvalidOperationException(
                    "The stage generator returned no plan.");
            _stageLengthTicks = GetStageLengthTicks(StagePlan);

            Rng battleRng = new Rng(_runSeed)
                .Fork(BattleSimulationStream)
                .Fork(StageIndex);
            Battle = new BattleSim(
                _battleConfig,
                battleRng,
                StagePlan,
                _battleContent,
                PowerUpGauge);
        }

        static int GetStageLengthTicks(StagePlan stagePlan)
        {
            long total = 0;
            for (int i = 0; i < stagePlan.Segments.Count; i++)
            {
                int length = stagePlan.Segments[i].LengthTicks;
                if (length < 1)
                    throw new InvalidOperationException(
                        "RunManager requires positive segment lengths.");
                total += length;
                if (total > int.MaxValue)
                    throw new InvalidOperationException(
                        "The stage timeline exceeds the tick range.");
            }

            if (total == 0)
                throw new InvalidOperationException(
                    "RunManager requires at least one stage segment.");
            return (int)total;
        }
    }
}
