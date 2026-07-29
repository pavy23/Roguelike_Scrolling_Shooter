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
        RepairHp = 2,
        FireRateUp = 3,
        DamageUp = 4,
        MoveSpeedUp = 5
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
    /// Read-only counters accumulated across the current run.
    /// Accuracy is intentionally left to consumers as ShotsHit / ShotsFired.
    /// </summary>
    public readonly struct RunStatistics
    {
        internal RunStatistics(
            long shotsFired,
            long shotsHit,
            long kills,
            long capsulesCollected,
            int stagesCleared)
        {
            ShotsFired = shotsFired;
            ShotsHit = shotsHit;
            Kills = kills;
            CapsulesCollected = capsulesCollected;
            StagesCleared = stagesCleared;
        }

        public long ShotsFired { get; }
        public long ShotsHit { get; }
        public long Kills { get; }
        public long CapsulesCollected { get; }
        public int StagesCleared { get; }
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
        readonly ShipDefinition _ship;
        readonly int[] _powerUpMaxLevels;
        readonly int _initialPlayerMaxHp;
        readonly int _initialFireIntervalTicks;
        readonly int _initialMainShotBaseDamage;
        readonly int _initialPlayerSpeedNumerator;
        readonly int _initialPlayerSpeedDenominator;
        readonly RewardDefinition[] _rewardPool;
        readonly int[] _rewardWeights;
        readonly RewardOption[] _rewardOptionBuffer;
        readonly IReadOnlyList<RewardOption> _rewardOptionView;
        readonly Rng _rewardRng;

        ulong _runSeed;
        int _stageLengthTicks;
        long _completedStageScore;
        long _completedShotsFired;
        long _completedShotsHit;
        long _completedKills;
        long _completedCapsulesCollected;
        int _stagesCleared;

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
                null,
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
                rewards,
                null)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ShipDefinition ship)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1.0),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ship)
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
                null,
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
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                rewards,
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
            RewardCatalog rewards,
            ShipDefinition ship)
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
            _ship = ship ?? ShipDefinition.CreateDefault();
            if (_rewards.OptionCount != RewardOptionCount)
                throw new ArgumentException(
                    $"RunManager requires exactly {RewardOptionCount} reward options.",
                    nameof(rewards));
            _rewardPool = new RewardDefinition[_rewards.All.Count];
            _rewardWeights = new int[_rewards.All.Count];
            _rewardOptionBuffer = new RewardOption[RewardOptionCount];
            _rewardOptionView = Array.AsReadOnly(_rewardOptionBuffer);
            _rewardRng = new Rng(0UL);
            _battleConfig.MainShotBaseDamage =
                _battleContent.PlayerWeapon.BaseDamage;
            _battleConfig.FireIntervalTicks =
                _battleContent.PlayerWeapon.FireIntervalTicks;
            _battleConfig.UseConfiguredMainShotStats = true;
            _initialPlayerMaxHp = _battleConfig.PlayerMaxHp;

            _powerUpMaxLevels = new int[PowerUpGauge.SlotCount];
            for (int i = 0; i < _powerUpMaxLevels.Length; i++)
                _powerUpMaxLevels[i] = PowerUpGauge.GetMaxLevel((PowerUpSlot)i);
            ApplyShipSpeedMultiplier(_battleConfig, _ship);
            _initialFireIntervalTicks = _battleConfig.FireIntervalTicks;
            _initialMainShotBaseDamage = _battleConfig.MainShotBaseDamage;
            _initialPlayerSpeedNumerator = _battleConfig.PlayerSpeedNumerator;
            _initialPlayerSpeedDenominator = _battleConfig.PlayerSpeedDenominator;
            ApplyShipStartingLevels(PowerUpGauge);

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
        public ShipDefinition Ship => _ship;
        /// <summary>Score earned across completed and current stages of this run.</summary>
        public long TotalScore => checked(_completedStageScore + Battle.Score);
        public RunStatistics Statistics
        {
            get
            {
                BattleStatistics battle = Battle.Statistics;
                return new RunStatistics(
                    AddSaturated(_completedShotsFired, battle.ShotsFired),
                    AddSaturated(_completedShotsHit, battle.ShotsHit),
                    AddSaturated(_completedKills, battle.Kills),
                    AddSaturated(
                        _completedCapsulesCollected,
                        battle.CapsulesCollected),
                    _stagesCleared);
            }
        }
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
                    IncrementStagesCleared();
                    _rewardOptions = GenerateRewardOptions();
                    State = RunState.AwaitingReward;
                }
                return;
            }

            if (Battle.Tick >= _stageLengthTicks)
            {
                IncrementStagesCleared();
                AdvanceStage();
            }
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
                case RewardType.FireRateUp:
                    _battleConfig.FireIntervalTicks = Math.Max(
                        Math.Min(
                            _battleConfig.FireIntervalTicks,
                            _battleConfig.MainShotMinimumFireIntervalTicks),
                        _battleConfig.FireIntervalTicks - option.Amount);
                    break;
                case RewardType.DamageUp:
                    _battleConfig.MainShotBaseDamage = SaturatingAdd(
                        _battleConfig.MainShotBaseDamage,
                        2L * option.Amount);
                    break;
                case RewardType.MoveSpeedUp:
                    AddMoveSpeed(_battleConfig, option.Amount);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown reward type {option.Type}.");
            }
        }

        /// <summary>시드·스테이지·주입 카탈로그의 결정론적 가중 비복원 선택.</summary>
        IReadOnlyList<RewardOption> GenerateRewardOptions()
        {
            IReadOnlyList<RewardDefinition> rewards = _rewards.All;
            int eligibleCount = 0;
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardDefinition reward = rewards[i];
                if (StageIndex < reward.StageIndexMin
                    || StageIndex > reward.StageIndexMax)
                    continue;

                _rewardPool[eligibleCount] = reward;
                _rewardWeights[eligibleCount] = reward.Weight;
                eligibleCount++;
            }

            if (eligibleCount < RewardOptionCount)
                throw new InvalidOperationException(
                    $"Stage {StageIndex} has {eligibleCount} eligible rewards; "
                    + $"{RewardOptionCount} are required.");

            _rewardRng.ResetForked(
                _runSeed,
                RewardSelectionStream,
                StageIndex);
            int poolCount = eligibleCount;
            for (int i = 0; i < _rewardOptionBuffer.Length; i++)
            {
                int pick = _rewardRng.PickWeighted(_rewardWeights, poolCount);
                RewardDefinition selected = _rewardPool[pick];
                _rewardOptionBuffer[i] = new RewardOption(
                    selected.Id,
                    selected.Type,
                    selected.Slot,
                    selected.Amount);

                int last = --poolCount;
                _rewardPool[pick] = _rewardPool[last];
                _rewardWeights[pick] = _rewardWeights[last];
            }
            return _rewardOptionView;
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
            int[] shipStartingLevels = _ship.ExportStartingPowerUpLevels();
            for (int i = 0; i < carriedLevels.Length; i++)
            {
                carriedLevels[i] = Math.Max(
                    carriedLevels[i],
                    shipStartingLevels[i]);
            }
            var nextGauge = new PowerUpGauge(_powerUpMaxLevels);
            nextGauge.ImportLevels(carriedLevels);

            _runSeed = newRunSeed;
            RunNumber++;
            StageIndex = 1;
            State = RunState.Playing;
            _rewardOptions = Array.Empty<RewardOption>();
            _completedStageScore = 0;
            _completedShotsFired = 0;
            _completedShotsHit = 0;
            _completedKills = 0;
            _completedCapsulesCollected = 0;
            _stagesCleared = 0;
            _battleConfig.PlayerMaxHp = _initialPlayerMaxHp;
            _battleConfig.FireIntervalTicks = _initialFireIntervalTicks;
            _battleConfig.MainShotBaseDamage = _initialMainShotBaseDamage;
            _battleConfig.PlayerSpeedNumerator = _initialPlayerSpeedNumerator;
            _battleConfig.PlayerSpeedDenominator = _initialPlayerSpeedDenominator;
            PowerUpGauge = nextGauge;
            BuildCurrentStage();
        }

        void AdvanceStage()
        {
            if (StageIndex == int.MaxValue)
                throw new InvalidOperationException("The stage counter is exhausted.");
            BattleStatistics battle = Battle.Statistics;
            _completedStageScore = TotalScore;
            _completedShotsFired = AddSaturated(
                _completedShotsFired,
                battle.ShotsFired);
            _completedShotsHit = AddSaturated(
                _completedShotsHit,
                battle.ShotsHit);
            _completedKills = AddSaturated(
                _completedKills,
                battle.Kills);
            _completedCapsulesCollected = AddSaturated(
                _completedCapsulesCollected,
                battle.CapsulesCollected);
            StageIndex++;
            BuildCurrentStage();
        }

        void IncrementStagesCleared()
        {
            if (_stagesCleared < int.MaxValue)
                _stagesCleared++;
        }

        static long AddSaturated(long left, long right)
        {
            return left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        static int SaturatingAdd(int value, long amount)
        {
            long result = value + amount;
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        static void AddMoveSpeed(BattleSimConfig config, int amount)
        {
            long bonusNumerator =
                (long)amount * SimSpace.SubUnitsPerWorldUnit;
            long bonusDenominator = SimSpace.TicksPerSecond;
            long bonusDivisor = GreatestCommonDivisor(
                bonusNumerator,
                bonusDenominator);
            bonusNumerator /= bonusDivisor;
            bonusDenominator /= bonusDivisor;

            long denominatorDivisor = GreatestCommonDivisor(
                config.PlayerSpeedDenominator,
                bonusDenominator);
            long leftScale = bonusDenominator / denominatorDivisor;
            long rightScale =
                config.PlayerSpeedDenominator / denominatorDivisor;
            if (!TryMultiply(config.PlayerSpeedNumerator, leftScale, out long left)
                || !TryMultiply(bonusNumerator, rightScale, out long right)
                || left > long.MaxValue - right
                || !TryMultiply(
                    config.PlayerSpeedDenominator,
                    leftScale,
                    out long denominator))
            {
                config.PlayerSpeedNumerator = int.MaxValue;
                return;
            }

            long numerator = left + right;
            long divisor = GreatestCommonDivisor(numerator, denominator);
            numerator /= divisor;
            denominator /= divisor;
            if (denominator > int.MaxValue)
            {
                config.PlayerSpeedNumerator = int.MaxValue;
                config.PlayerSpeedDenominator = 1;
                return;
            }

            config.PlayerSpeedNumerator = numerator > int.MaxValue
                ? int.MaxValue
                : (int)numerator;
            config.PlayerSpeedDenominator = (int)denominator;
        }

        static bool TryMultiply(long left, long right, out long result)
        {
            if (left != 0 && right > long.MaxValue / left)
            {
                result = 0;
                return false;
            }
            result = left * right;
            return true;
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

        void ApplyShipStartingLevels(PowerUpGauge gauge)
        {
            int[] levels = gauge.ExportLevels();
            int[] startingLevels = _ship.ExportStartingPowerUpLevels();
            for (int i = 0; i < levels.Length; i++)
            {
                if (startingLevels[i] > _powerUpMaxLevels[i])
                    throw new ArgumentException(
                        $"Ship '{_ship.Id}' starting level for {(PowerUpSlot)i} "
                        + $"exceeds the gauge maximum {_powerUpMaxLevels[i]}.",
                        nameof(_ship));
                levels[i] = Math.Max(levels[i], startingLevels[i]);
            }
            gauge.ImportLevels(levels);
        }

        static void ApplyShipSpeedMultiplier(
            BattleSimConfig config,
            ShipDefinition ship)
        {
            if (config.PlayerSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerSpeedNumerator));
            if (config.PlayerSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerSpeedDenominator));

            int numeratorCancel = GreatestCommonDivisor(
                config.PlayerSpeedNumerator,
                ship.MoveSpeedMultiplierDenominator);
            int denominatorCancel = GreatestCommonDivisor(
                ship.MoveSpeedMultiplierNumerator,
                config.PlayerSpeedDenominator);

            long numerator =
                (long)(config.PlayerSpeedNumerator / numeratorCancel)
                * (ship.MoveSpeedMultiplierNumerator / denominatorCancel);
            long denominator =
                (long)(config.PlayerSpeedDenominator / denominatorCancel)
                * (ship.MoveSpeedMultiplierDenominator / numeratorCancel);
            if (numerator > int.MaxValue || denominator > int.MaxValue)
                throw new ArgumentException(
                    $"Ship '{ship.Id}' movement multiplier exceeds "
                    + "the supported exact fraction range.",
                    nameof(ship));

            config.PlayerSpeedNumerator = (int)numerator;
            config.PlayerSpeedDenominator = (int)denominator;
        }

        static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }
            return left == 0 ? 1 : left;
        }

        static long GreatestCommonDivisor(long left, long right)
        {
            while (right != 0)
            {
                long remainder = left % right;
                left = right;
                right = remainder;
            }
            return left == 0 ? 1 : left;
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
