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

    /// <summary>보상 후보 하나. 수치는 잠정 풀 기반 — 확정은 사람/GROK (AGENTS.md §7, rewards.json 예정).</summary>
    public readonly struct RewardOption
    {
        public RewardOption(RewardType type, PowerUpSlot slot, int amount)
        {
            Type = type;
            Slot = slot;
            Amount = amount;
        }

        public RewardType Type { get; }
        public PowerUpSlot Slot { get; }
        public int Amount { get; }
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
        const int RewardOptionCount = 3;

        readonly IStageGenerator _stageGenerator;
        readonly BattleSimConfig _battleConfig;
        readonly BattleContent _battleContent;
        readonly MetaProgression _metaProgression;
        readonly StageDifficultyCurve _difficultyCurve;
        readonly int[] _powerUpMaxLevels;

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
                StageDifficultyCurve.CreateDefault())
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
        RewardOption[] _rewardOptions = Array.Empty<RewardOption>();

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
            if (optionIndex < 0 || optionIndex >= _rewardOptions.Length)
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

        /// <summary>
        /// 잠정 보상 풀 (rewards.json 이관 예정 — REQ-008). 시드·스테이지의 순수 함수.
        /// </summary>
        RewardOption[] GenerateRewardOptions()
        {
            var pool = new[]
            {
                new RewardOption(RewardType.Capsules, PowerUpSlot.MainShot, 3),
                new RewardOption(RewardType.SlotLevel, PowerUpSlot.MainShot, 1),
                new RewardOption(RewardType.SlotLevel, PowerUpSlot.Missile, 1),
                new RewardOption(RewardType.SlotLevel, PowerUpSlot.Option, 1),
                new RewardOption(RewardType.SlotLevel, PowerUpSlot.Shield, 1),
                new RewardOption(RewardType.RepairHp, PowerUpSlot.MainShot, 1)
            };

            Rng rewardRng = new Rng(_runSeed)
                .Fork(RewardSelectionStream)
                .Fork(StageIndex);
            var options = new RewardOption[RewardOptionCount];
            int remaining = pool.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int pick = rewardRng.NextInt(0, remaining);
                options[i] = pool[pick];
                pool[pick] = pool[remaining - 1];
                remaining--;
            }
            return options;
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
