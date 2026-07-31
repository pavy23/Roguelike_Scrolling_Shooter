using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public enum RunState
    {
        Playing = 0,
        RunOver = 1,
        /// <summary>보스 격파 후 보상 선택 대기 (REQ-007 요청 3). ChooseReward로 재개.</summary>
        AwaitingReward = 2,
        /// <summary>
        /// Legacy serialized value only. REQ-054 runtime never enters this state.
        /// </summary>
        [Obsolete(
            "REQ-054 removed runtime route choices. Numeric value is retained "
            + "only for legacy persistence compatibility.")]
        AwaitingRoute = 3,
        /// <summary>The configured final stage was cleared successfully.</summary>
        RunCleared = 4,
        /// <summary>
        /// The next biome's fully disclosed sector contract is waiting for input.
        /// </summary>
        AwaitingContract = 5
    }

    public enum RunStageSection
    {
        Opening = 0,
        MidBoss = 1,
        Closing = 2,
        StageBoss = 3,
        HiddenOpening = 4,
        HiddenBoss = 5
    }

    public enum RewardSelectionKind
    {
        None = 0,
        MidStage = 1,
        Main = 2
    }

    public enum RewardDecisionKind
    {
        Select = 0,
        Reroll = 1
    }

    public readonly struct RewardDecision
    {
        public RewardDecision(
            int rewardSequence,
            RewardSelectionKind selectionKind,
            RewardDecisionKind decisionKind,
            int optionIndex)
        {
            if (rewardSequence < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(rewardSequence));
            if (selectionKind != RewardSelectionKind.MidStage
                && selectionKind != RewardSelectionKind.Main)
                throw new ArgumentOutOfRangeException(
                    nameof(selectionKind));
            if (!Enum.IsDefined(
                    typeof(RewardDecisionKind),
                    decisionKind))
                throw new ArgumentOutOfRangeException(
                    nameof(decisionKind));
            if (decisionKind == RewardDecisionKind.Reroll)
            {
                if (optionIndex != -1)
                    throw new ArgumentOutOfRangeException(
                        nameof(optionIndex));
            }
            else if (optionIndex < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(optionIndex));
            RewardSequence = rewardSequence;
            SelectionKind = selectionKind;
            DecisionKind = decisionKind;
            OptionIndex = optionIndex;
        }

        public int RewardSequence { get; }
        public RewardSelectionKind SelectionKind { get; }
        public RewardDecisionKind DecisionKind { get; }
        public int OptionIndex { get; }
    }

    public enum RunCompletionGrade
    {
        None = 0,
        StandardClear = 1,
        PerfectClear = 2
    }

    public enum RewardType
    {
        /// <summary>캡슐 n개 즉시 수집 (커서 전진).</summary>
        Capsules = 0,
        /// <summary>지정 슬롯 레벨 +1 (최대치 클램프).</summary>
        SlotLevel = 1,
        /// <summary>실드 스톡 회복 — 상한까지 즉시 적용.</summary>
        ShieldStock = 2,
        /// <summary>
        /// Legacy API/JSON compatibility alias. "repairHp" now restores shield
        /// stock and retains numeric value 2.
        /// </summary>
        RepairHp = ShieldStock,
        FireRateUp = 3,
        DamageUp = 4,
        MoveSpeedUp = 5,
        Modifier = 6,
        MissileFamily = 7,
        OptionFormation = 8,
        /// <summary>전멸 폭탄 스톡을 상한까지 즉시 획득.</summary>
        BombStock = 9,
        PrimaryWeaponFamily = 10
    }

    public enum RewardPool
    {
        Both = 0,
        Mid = 1,
        Main = 2
    }

    public enum RewardEffectType
    {
        Capsules = 0,
        SlotLevel = 1,
        ShieldStock = 2,
        FireRateUp = 3,
        DamageUp = 4,
        MoveSpeedUp = 5,
        Modifier = 6,
        MissileFamily = 7,
        OptionFormation = 8,
        BombStock = 9,
        PrimaryWeaponFamily = 10,
        ShieldMaxDown = 11,
        MoveSpeedDown = 12,
        CapsuleDropWeightDown = 13,
        BombMaxDown = 14
    }

    public readonly struct RewardEffectView
    {
        public RewardEffectView(RewardEffectType type, int amount)
        {
            if (!Enum.IsDefined(typeof(RewardEffectType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Type = type;
            Amount = amount;
        }

        public RewardEffectType Type { get; }
        public int Amount { get; }
    }

    public readonly struct RewardCostDefinition
    {
        public RewardCostDefinition(RewardEffectType type, int amount)
        {
            if (type != RewardEffectType.ShieldMaxDown
                && type != RewardEffectType.MoveSpeedDown
                && type != RewardEffectType.CapsuleDropWeightDown
                && type != RewardEffectType.BombMaxDown)
                throw new ArgumentOutOfRangeException(nameof(type));
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount));
            Type = type;
            Amount = amount;
        }

        public RewardEffectType Type { get; }
        public int Amount { get; }
    }

    /// <summary>보상 후보 하나.</summary>
    public readonly struct RewardOption
    {
        public RewardOption(RewardType type, PowerUpSlot slot, int amount)
            : this(null, type, slot, amount)
        {
        }

        public RewardOption(string id, RewardType type, PowerUpSlot slot, int amount)
            : this(id, type, slot, amount, BattleModifier.None)
        {
        }

        public RewardOption(
            string id,
            RewardType type,
            PowerUpSlot slot,
            int amount,
            BattleModifier modifierId)
            : this(
                id,
                type,
                slot,
                amount,
                modifierId,
                MissileFamily.Straight,
                OptionFormation.Trail,
                PrimaryWeaponFamily.Vulcan)
        {
        }

        public RewardOption(
            string id,
            RewardType type,
            PowerUpSlot slot,
            int amount,
            BattleModifier modifierId,
            MissileFamily missileFamily,
            OptionFormation optionFormation,
            PrimaryWeaponFamily primaryWeaponFamily =
                PrimaryWeaponFamily.Vulcan,
            string modifierKey = null,
            IReadOnlyList<RewardCostDefinition> costs = null,
            IReadOnlyList<RewardEffectView> gains = null,
            IReadOnlyList<RewardEffectView> costViews = null)
        {
            Id = id;
            Type = type;
            Slot = slot;
            Amount = amount;
            ModifierId = modifierId;
            ModifierKey = modifierKey;
            MissileFamily = missileFamily;
            OptionFormation = optionFormation;
            PrimaryWeaponFamily = primaryWeaponFamily;
            Gains = gains ?? Array.AsReadOnly(new[]
            {
                new RewardEffectView(
                    (RewardEffectType)type,
                    amount)
            });
            if (costViews != null)
                Costs = costViews;
            else if (costs == null || costs.Count == 0)
                Costs = Array.Empty<RewardEffectView>();
            else
            {
                var copy = new RewardEffectView[costs.Count];
                for (int i = 0; i < copy.Length; i++)
                    copy[i] = new RewardEffectView(
                        costs[i].Type,
                        costs[i].Amount);
                Costs = Array.AsReadOnly(copy);
            }
        }

        public string Id { get; }
        public RewardType Type { get; }
        public PowerUpSlot Slot { get; }
        public int Amount { get; }
        public BattleModifier ModifierId { get; }
        public string ModifierKey { get; }
        public MissileFamily MissileFamily { get; }
        public OptionFormation OptionFormation { get; }
        public PrimaryWeaponFamily PrimaryWeaponFamily { get; }
        public IReadOnlyList<RewardEffectView> Gains { get; }
        public IReadOnlyList<RewardEffectView> Costs { get; }
    }

    public readonly struct RouteOption
    {
        public RouteOption(string themeId, EncounterType encounterType)
        {
            if (string.IsNullOrEmpty(themeId))
                throw new ArgumentException(
                    "Route theme id cannot be empty.",
                    nameof(themeId));
            if (!Enum.IsDefined(typeof(EncounterType), encounterType))
                throw new ArgumentOutOfRangeException(nameof(encounterType));
            ThemeId = themeId;
            EncounterType = encounterType;
        }

        public string ThemeId { get; }
        public EncounterType EncounterType { get; }
    }

    public readonly struct RouteChoice
    {
        public RouteChoice(
            int stageIndex,
            int optionIndex,
            string themeId,
            EncounterType encounterType)
            : this(
                stageIndex,
                1,
                optionIndex,
                themeId,
                encounterType)
        {
        }

        public RouteChoice(
            int biomeIndex,
            int roomIndex,
            int optionIndex,
            string themeId,
            EncounterType encounterType)
        {
            if (biomeIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(biomeIndex));
            if (roomIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(roomIndex));
            if (optionIndex < 0
                || optionIndex >= RunManager.MaximumRouteOptionCount)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (string.IsNullOrEmpty(themeId))
                throw new ArgumentException(
                    "Route theme id cannot be empty.",
                    nameof(themeId));
            if (!Enum.IsDefined(typeof(EncounterType), encounterType))
                throw new ArgumentOutOfRangeException(nameof(encounterType));
            BiomeIndex = biomeIndex;
            RoomIndex = roomIndex;
            OptionIndex = optionIndex;
            ThemeId = themeId;
            EncounterType = encounterType;
        }

        /// <summary>The biome entered by this choice. Legacy alias: StageIndex.</summary>
        public int BiomeIndex { get; }
        public int StageIndex => BiomeIndex;
        /// <summary>The regular room entered by this choice.</summary>
        public int RoomIndex { get; }
        public int OptionIndex { get; }
        public string ThemeId { get; }
        public EncounterType EncounterType { get; }
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
            int stageIndexMax,
            int? maxPerRun = null,
            BattleModifier modifierId = BattleModifier.None,
            MissileFamily missileFamily = MissileFamily.Straight,
            OptionFormation optionFormation = OptionFormation.Trail,
            PrimaryWeaponFamily primaryWeaponFamily =
                PrimaryWeaponFamily.Vulcan,
            string modifierKey = null,
            bool modifierStackable = false,
            int modifierMaxStacks = 1,
            int modifierStackStrength = 1,
            int modifierInteractionCost = 1,
            RewardPool pool = RewardPool.Both,
            IReadOnlyList<RewardCostDefinition> costs = null)
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
            if (!Enum.IsDefined(typeof(RewardPool), pool))
                throw new ArgumentOutOfRangeException(nameof(pool));
            if (maxPerRun.HasValue && maxPerRun.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(maxPerRun));
            if (type == RewardType.Modifier)
            {
                if (!BattleModifierRules.IsSingleKnown(modifierId))
                    throw new ArgumentOutOfRangeException(nameof(modifierId));
                if (string.IsNullOrEmpty(modifierKey))
                    modifierKey = modifierId.ToString();
                if (modifierMaxStacks < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(modifierMaxStacks));
                if (!modifierStackable && modifierMaxStacks != 1)
                    throw new ArgumentException(
                        "One-time modifiers must have maxStacks 1.",
                        nameof(modifierMaxStacks));
                if (modifierStackStrength < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(modifierStackStrength));
                if (modifierInteractionCost < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(modifierInteractionCost));
            }
            else if (modifierId != BattleModifier.None)
            {
                throw new ArgumentException(
                    "Only modifier rewards can specify a modifier id.",
                    nameof(modifierId));
            }
            if (type == RewardType.MissileFamily
                && !Enum.IsDefined(
                    typeof(MissileFamily),
                    missileFamily))
                throw new ArgumentOutOfRangeException(
                    nameof(missileFamily));
            if (type == RewardType.OptionFormation
                && !Enum.IsDefined(
                    typeof(OptionFormation),
                    optionFormation))
                throw new ArgumentOutOfRangeException(
                    nameof(optionFormation));
            if (type == RewardType.PrimaryWeaponFamily
                && !Enum.IsDefined(
                    typeof(PrimaryWeaponFamily),
                    primaryWeaponFamily))
                throw new ArgumentOutOfRangeException(
                    nameof(primaryWeaponFamily));

            Id = id;
            Type = type;
            Slot = slot;
            Amount = amount;
            Weight = weight;
            StageIndexMin = stageIndexMin;
            StageIndexMax = stageIndexMax;
            MaxPerRun = maxPerRun;
            ModifierId = modifierId;
            ModifierKey = modifierKey;
            ModifierStackable = modifierStackable;
            ModifierMaxStacks = modifierMaxStacks;
            ModifierStackStrength = modifierStackStrength;
            ModifierInteractionCost = modifierInteractionCost;
            MissileFamily = missileFamily;
            OptionFormation = optionFormation;
            PrimaryWeaponFamily = primaryWeaponFamily;
            Pool = pool;
            Gains = Array.AsReadOnly(new[]
            {
                new RewardEffectView(
                    (RewardEffectType)type,
                    amount)
            });
            if (costs == null || costs.Count == 0)
            {
                Costs = Array.Empty<RewardCostDefinition>();
                CostViews = Array.Empty<RewardEffectView>();
            }
            else
            {
                var costCopy = new RewardCostDefinition[costs.Count];
                var costViewCopy = new RewardEffectView[costs.Count];
                for (int i = 0; i < costCopy.Length; i++)
                {
                    costCopy[i] = costs[i];
                    costViewCopy[i] = new RewardEffectView(
                        costs[i].Type,
                        costs[i].Amount);
                }
                Costs = Array.AsReadOnly(costCopy);
                CostViews = Array.AsReadOnly(costViewCopy);
            }
        }

        public string Id { get; }
        public RewardType Type { get; }
        public PowerUpSlot Slot { get; }
        public int Amount { get; }
        public int Weight { get; }
        public int StageIndexMin { get; }
        public int StageIndexMax { get; }
        /// <summary>Maximum acquisitions in one run; null means unlimited.</summary>
        public int? MaxPerRun { get; }
        public BattleModifier ModifierId { get; }
        public string ModifierKey { get; }
        public bool ModifierStackable { get; }
        public int ModifierMaxStacks { get; }
        public int ModifierStackStrength { get; }
        public int ModifierInteractionCost { get; }
        public MissileFamily MissileFamily { get; }
        public OptionFormation OptionFormation { get; }
        public PrimaryWeaponFamily PrimaryWeaponFamily { get; }
        public RewardPool Pool { get; }
        public IReadOnlyList<RewardEffectView> Gains { get; }
        public IReadOnlyList<RewardCostDefinition> Costs { get; }
        internal IReadOnlyList<RewardEffectView> CostViews { get; }
    }

    /// <summary>Immutable reward pool parsed from rewards.json.</summary>
    public sealed class RewardCatalog
    {
        readonly IReadOnlyList<RewardDefinition> _all;

        public RewardCatalog(
            int optionCount,
            IReadOnlyList<RewardDefinition> rewards,
            int maxCombinedModifierCost = 4,
            int rerollCost = 5)
        {
            if (optionCount < 1)
                throw new ArgumentOutOfRangeException(nameof(optionCount));
            if (rewards == null)
                throw new ArgumentNullException(nameof(rewards));
            if (rewards.Count < optionCount)
                throw new ArgumentException(
                    "The reward pool cannot be smaller than the option count.",
                    nameof(rewards));
            if (maxCombinedModifierCost < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxCombinedModifierCost));
            if (rerollCost < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(rerollCost));

            var copy = new RewardDefinition[rewards.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = rewards[i];

            OptionCount = optionCount;
            MaxCombinedModifierCost = maxCombinedModifierCost;
            RerollCost = rerollCost;
            _all = Array.AsReadOnly(copy);
        }

        public int OptionCount { get; }
        public int MaxCombinedModifierCost { get; }
        public int RerollCost { get; }
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

    /// <summary>
    /// Configurable integer linear difficulty curve evaluated once per biome.
    /// The Stage name remains for source compatibility with existing consumers.
    /// </summary>
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

    /// <summary>Immutable biome/room hierarchy for one campaign loop.</summary>
    public sealed class RunProgressionConfig
    {
        public const int DefaultBiomeCount = 5;
        public const int DefaultRoomsPerBiome = 3;
        public const int DefaultFinalStageIndex = DefaultBiomeCount;
        public const int HiddenRooms = 2;

        /// <summary>
        /// Legacy constructor: one regular room per biome. Existing callers and
        /// migrated pre-biome recordings preserve their former stage cadence.
        /// New campaigns should use the two-argument constructor.
        /// </summary>
        public RunProgressionConfig(int finalStageIndex)
            : this(finalStageIndex, 1)
        {
        }

        public RunProgressionConfig(int biomeCount, int roomsPerBiome)
        {
            if (biomeCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(biomeCount));
            if (roomsPerBiome < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(roomsPerBiome));
            BiomeCount = biomeCount;
            RoomsPerBiome = roomsPerBiome;
        }

        public int BiomeCount { get; }
        public int RoomsPerBiome { get; }
        public int FinalStageIndex => BiomeCount;

        public static RunProgressionConfig CreateDefault()
        {
            return new RunProgressionConfig(
                DefaultBiomeCount,
                DefaultRoomsPerBiome);
        }

        public bool IsFinalBiome(int biomeIndex)
        {
            return biomeIndex >= BiomeCount;
        }

        public bool IsFinalStage(int stageIndex) => IsFinalBiome(stageIndex);
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
            long grazeCount,
            int stagesCleared,
            int roomsCleared)
        {
            ShotsFired = shotsFired;
            ShotsHit = shotsHit;
            Kills = kills;
            CapsulesCollected = capsulesCollected;
            GrazeCount = grazeCount;
            StagesCleared = stagesCleared;
            RoomsCleared = roomsCleared;
        }

        public long ShotsFired { get; }
        public long ShotsHit { get; }
        public long Kills { get; }
        public long CapsulesCollected { get; }
        public long GrazeCount { get; }
        public int StagesCleared { get; }
        public int BiomesCleared => StagesCleared;
        public int RoomsCleared { get; }
    }

    /// <summary>
    /// Owns one roguelike run: deterministic biome/room creation, battle replacement,
    /// death state, and power-up carry into a restarted run.
    /// </summary>
    public sealed class RunManager
    {
        sealed class PrefixReadOnlyList<T> :
            IReadOnlyList<T>,
            IList<T>
        {
            readonly T[] _items;

            public PrefixReadOnlyList(T[] items)
            {
                _items = items ?? throw new ArgumentNullException(
                    nameof(items));
            }

            public int Count { get; private set; }

            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[index];
                }
                set => throw new NotSupportedException(
                    "The reward option view is read-only.");
            }

            public bool IsReadOnly => true;

            public void SetCount(int count)
            {
                if (count < 0 || count > _items.Length)
                    throw new ArgumentOutOfRangeException(nameof(count));
                Count = count;
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return _items[i];
            }

            public int IndexOf(T item)
            {
                return Array.IndexOf(_items, item, 0, Count);
            }

            public bool Contains(T item) => IndexOf(item) >= 0;

            public void CopyTo(T[] array, int arrayIndex)
            {
                Array.Copy(_items, 0, array, arrayIndex, Count);
            }

            public void Add(T item) => throw new NotSupportedException(
                "The reward option view is read-only.");
            public void Clear() => throw new NotSupportedException(
                "The reward option view is read-only.");
            public void Insert(int index, T item) =>
                throw new NotSupportedException(
                    "The reward option view is read-only.");
            public bool Remove(T item) => throw new NotSupportedException(
                "The reward option view is read-only.");
            public void RemoveAt(int index) =>
                throw new NotSupportedException(
                    "The reward option view is read-only.");

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        const int BattleSimulationStream = 1;
        const int RewardSelectionStream = 2;
        // Stream id 3 is retained so section traits and legacy route migration
        // preserve the pre-REQ-054 deterministic sequence.
        const int RouteSelectionStream = 3;
        const int RoomGenerationStream = 4;
        const int ColossalBossSelectionStream = 5;
        const int MidBossSelectionStream = 6;
        const int ContractSelectionStream = 7;
        const int StageOrderStream = 8;
        public const int MidStageRewardOptionCount = 2;
        public const int MainRewardOptionCount = 3;
        /// <summary>Legacy alias for the main reward card count.</summary>
        public const int RewardOptionCount = MainRewardOptionCount;
        public const int MinimumContractOptionCount = 2;
        public const int MaximumContractOptionCount = 3;
        public const int MinimumRouteOptionCount = 2;
        public const int MaximumRouteOptionCount = 3;

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
                    "repair_hp_1", RewardType.ShieldStock, PowerUpSlot.MainShot,
                    1, 1, 1, int.MaxValue)
            });
        static readonly RewardOption[] FallbackRewardOptions =
        {
            new RewardOption(
                "fallback_capsules_1",
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                1),
            new RewardOption(
                "fallback_shield_1",
                RewardType.ShieldStock,
                PowerUpSlot.Shield,
                1),
            new RewardOption(
                "fallback_capsules_3",
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                3)
        };
        static readonly ContractCatalog BuiltInContracts =
            new ContractCatalog(
                "standard_route",
                MinimumContractOptionCount,
                MinimumContractOptionCount,
                new[]
                {
                    new ContractDefinition(
                        "standard_route",
                        1,
                        ContractRiskTier.Safe),
                    new ContractDefinition(
                        "standard_route_reserve",
                        1,
                        ContractRiskTier.Safe)
                });
        static readonly ContractDefinition BuiltInEndRunContract =
            new ContractDefinition(
                "end_run",
                1,
                ContractRiskTier.Safe,
                destinationKind:
                    ContractDestinationKind.EndRun);
        static readonly ContractDefinition BuiltInUnchartedContract =
            new ContractDefinition(
                "uncharted",
                1,
                ContractRiskTier.High,
                destinationKind:
                    ContractDestinationKind.Uncharted,
                eligibility:
                    ContractEligibility.HiddenBiomeUnlocked);

        readonly IStageGenerator _stageGenerator;
        readonly BattleSimConfig _battleConfig;
        readonly BattleContent _battleContent;
        readonly MetaProgression _metaProgression;
        readonly StageDifficultyCurve _difficultyCurve;
        readonly RunProgressionConfig _progressionConfig;
        readonly RewardCatalog _rewards;
        readonly ContractCatalog _contracts;
        readonly ShipDefinition _ship;
        readonly int _difficultyMultiplierNumerator;
        readonly int _difficultyMultiplierDenominator;
        readonly int[] _powerUpMaxLevels;
        readonly int _initialShieldStock;
        readonly int _initialBombStock;
        readonly int _initialMaxShieldStock;
        readonly int _initialMaxBombStock;
        readonly int _initialCapsuleDropWeightReduction;
        readonly int _initialFireIntervalTicks;
        readonly int _initialMainShotBaseDamage;
        readonly int _initialPlayerSpeedNumerator;
        readonly int _initialPlayerSpeedDenominator;
        readonly RewardDefinition[] _rewardPool;
        readonly int[] _rewardPoolCatalogIndices;
        readonly int[] _rewardWeights;
        readonly RewardOption[] _rewardOptionBuffer;
        readonly int[] _rewardOptionCatalogIndices;
        readonly PrefixReadOnlyList<RewardOption> _rewardOptionView;
        readonly int[] _rewardAcquisitionCounts;
        readonly int[] _stageStartRewardAcquisitionCounts;
        readonly Rng _rewardRng;
        readonly Rng _routeRng;
        readonly Rng _contractRng;
        readonly int[] _stageThemeOrder;
        readonly ReadOnlyCollection<int> _stageThemeOrderView;
        readonly List<RouteChoice> _routeChoiceHistory;
        readonly ReadOnlyCollection<RouteChoice> _routeChoiceHistoryView;
        readonly List<ContractChoice> _contractChoiceHistory;
        readonly ReadOnlyCollection<ContractChoice>
            _contractChoiceHistoryView;
        readonly List<RewardDecision> _rewardDecisionHistory;
        readonly ReadOnlyCollection<RewardDecision>
            _rewardDecisionHistoryView;
        MetaState _metaState;
        ColossalBossKind _lastColossalBossAtRunStart;
        BattleContinuityState _pendingBattleContinuity;

        ulong _runSeed;
        int _stageLengthTicks;
        long _completedStageScore;
        long _completedShotsFired;
        long _completedShotsHit;
        long _completedKills;
        long _completedCapsulesCollected;
        long _completedGrazeCount;
        int _stagesCleared;
        int _roomsCleared;
        bool _activateHeld;
        int[] _stageStartPowerUpLevels;
        int[] _stageStartPowerUpProgress;
        int _stageStartPowerUpCursor;
        long _stageStartScore;
        long _stageStartShotsFired;
        long _stageStartShotsHit;
        long _stageStartKills;
        long _stageStartCapsulesCollected;
        long _stageStartGrazeCount;
        int _stageStartStagesCleared;
        int _stageStartRoomsCleared;
        int _stageStartPlayerLife;
        int _stageStartShieldStock;
        int _stageStartBombStock;
        BattleModifier _stageStartActiveModifiers;
        PrimaryWeaponFamily _stageStartPrimaryWeaponFamily;
        MissileFamily _stageStartMissileFamily;
        OptionFormation _stageStartOptionFormation;
        int _stageStartEliteRoomsCleared;
        int _stageStartNoHitBiomesCleared;
        int _stageStartRareEncountersCleared;
        bool _stageStartCurrentBiomeHit;
        int _stageStartFireIntervalTicks;
        int _stageStartMainShotBaseDamage;
        int _stageStartPlayerSpeedNumerator;
        int _stageStartPlayerSpeedDenominator;
        int _stageStartCapsuleBalance;
        BattleContinuityState _stageStartContinuity;
        int _rewardSelectionsRemaining;
        int _rewardSelectionRound;
        RewardSelectionKind _rewardSelectionKind;
        bool _currentBiomeHit;
        int _capsuleBalance;
        IReadOnlyList<ContractOption> _contractOptions =
            Array.Empty<ContractOption>();

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
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                null,
                null,
                1,
                1)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ContractCatalog contracts)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                1,
                1,
                null,
                true,
                contracts)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ContractCatalog contracts,
            ShipDefinition ship)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ship,
                1,
                1,
                null,
                true,
                contracts)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            MetaState metaState)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge)
        {
            _metaState = metaState
                ?? throw new ArgumentNullException(nameof(metaState));
            _lastColossalBossAtRunStart =
                metaState.LastColossalBoss;
        }

        /// <summary>
        /// Replay constructor: injects the recorded meta input without mutating
        /// or requiring a live MetaState.
        /// </summary>
        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RunProgressionConfig progressionConfig,
            ColossalBossKind lastColossalBossAtRunStart)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                progressionConfig)
        {
            SetLastColossalBossAtRunStart(
                lastColossalBossAtRunStart);
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
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                null,
                1,
                1)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RunProgressionConfig progressionConfig)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                null,
                null,
                1,
                1,
                progressionConfig,
                true)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RunProgressionConfig progressionConfig,
            MetaState metaState)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                progressionConfig)
        {
            _metaState = metaState
                ?? throw new ArgumentNullException(nameof(metaState));
            _lastColossalBossAtRunStart =
                metaState.LastColossalBoss;
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
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ship,
                1,
                1)
        {
        }

        /// <summary>
        /// Creates a run with provisional enemy-HP difficulty scaling.
        /// The fraction is reduced and applied with ceiling; 1/1 preserves
        /// the legacy behavior. Preset values remain a human balance decision.
        /// </summary>
        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ship,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator)
        {
        }

        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            MetaState metaState)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                rewards,
                ship,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator)
        {
            _metaState = metaState
                ?? throw new ArgumentNullException(nameof(metaState));
            SetLastColossalBossAtRunStart(
                metaState.LastColossalBoss);
        }

        /// <summary>
        /// Full replay constructor. lastColossalBossAtRunStart must come from
        /// InputPlayback so hidden-boss weighting remains reproducible.
        /// </summary>
        public RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            ColossalBossKind lastColossalBossAtRunStart)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                rewards,
                ship,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator)
        {
            SetLastColossalBossAtRunStart(
                lastColossalBossAtRunStart);
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
                null,
                1,
                1)
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
                null,
                1,
                1)
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
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                rewards,
                ship,
                1,
                1)
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
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                rewards,
                ship,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                null,
                true)
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
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            RunProgressionConfig progressionConfig)
            : this(
                runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                rewards,
                ship,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                progressionConfig,
                true)
        {
        }

        RunManager(
            ulong runSeed,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            MetaProgression metaProgression,
            StageDifficultyCurve difficultyCurve,
            RewardCatalog rewards,
            ShipDefinition ship,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            RunProgressionConfig progressionConfig,
            bool buildInitialStage,
            ContractCatalog contracts = null)
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
            _progressionConfig =
                progressionConfig ?? RunProgressionConfig.CreateDefault();
            _rewards = rewards ?? BuiltInRewards;
            _contracts = contracts ?? BuiltInContracts;
            ModifierStacks = new BattleModifierStackSet(
                _rewards.MaxCombinedModifierCost);
            _ship = ship ?? ShipDefinition.CreateDefault();
            ValidateShipGauge(_ship, PowerUpGauge);
            if (_ship.StartingMissileFamily.HasValue
                && _battleContent.FindMissileFamily(
                    _ship.StartingMissileFamily.Value) == null)
                throw new ArgumentException(
                    $"Ship '{_ship.Id}' references an unavailable "
                    + "missile family.",
                    nameof(ship));
            NormalizeDifficultyMultiplier(
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                out _difficultyMultiplierNumerator,
                out _difficultyMultiplierDenominator);
            _battleConfig.EnemyHpMultiplierNumerator =
                _difficultyMultiplierNumerator;
            _battleConfig.EnemyHpMultiplierDenominator =
                _difficultyMultiplierDenominator;
            if (_rewards.OptionCount != RewardOptionCount)
                throw new ArgumentException(
                    $"RunManager requires exactly {RewardOptionCount} reward options.",
                    nameof(rewards));
            for (int i = 0; i < _rewards.All.Count; i++)
            {
                RewardDefinition reward = _rewards.All[i];
                if (reward.Type == RewardType.MissileFamily
                    && _battleContent.FindMissileFamily(
                        reward.MissileFamily) == null)
                    throw new ArgumentException(
                        $"Reward '{reward.Id}' references an unavailable "
                        + "missile family.",
                        nameof(rewards));
                if (reward.Type == RewardType.OptionFormation
                    && _battleContent.FindOptionFormation(
                        reward.OptionFormation) == null)
                    throw new ArgumentException(
                        $"Reward '{reward.Id}' references an unavailable "
                        + "option formation.",
                        nameof(rewards));
                if (reward.Type == RewardType.PrimaryWeaponFamily
                    && _battleContent.FindPrimaryWeaponFamily(
                        reward.PrimaryWeaponFamily) == null)
                    throw new ArgumentException(
                        $"Reward '{reward.Id}' references an unavailable "
                        + "primary weapon family.",
                        nameof(rewards));
            }
            _rewardPool = new RewardDefinition[_rewards.All.Count];
            _rewardPoolCatalogIndices = new int[_rewards.All.Count];
            _rewardWeights = new int[_rewards.All.Count];
            _rewardOptionBuffer =
                new RewardOption[MainRewardOptionCount + 1];
            _rewardOptionCatalogIndices =
                new int[MainRewardOptionCount + 1];
            _rewardOptionView =
                new PrefixReadOnlyList<RewardOption>(
                    _rewardOptionBuffer);
            _rewardAcquisitionCounts = new int[_rewards.All.Count];
            _stageStartRewardAcquisitionCounts =
                new int[_rewards.All.Count];
            _stageStartPowerUpLevels =
                new int[PowerUpGauge.SlotCount];
            _stageStartPowerUpProgress =
                new int[PowerUpGauge.SlotCount];
            _rewardRng = new Rng(0UL);
            _routeRng = new Rng(0UL);
            _contractRng = new Rng(0UL);
            _stageThemeOrder = CreateStageThemeOrder(
                runSeed,
                _progressionConfig.BiomeCount,
                _stageGenerator);
            _stageThemeOrderView =
                Array.AsReadOnly(_stageThemeOrder);
            _routeChoiceHistory = new List<RouteChoice>();
            _routeChoiceHistoryView = _routeChoiceHistory.AsReadOnly();
            _contractChoiceHistory = new List<ContractChoice>();
            _contractChoiceHistoryView =
                _contractChoiceHistory.AsReadOnly();
            _rewardDecisionHistory =
                new List<RewardDecision>();
            _rewardDecisionHistoryView =
                _rewardDecisionHistory.AsReadOnly();
            _battleConfig.MainShotBaseDamage =
                _battleContent.PlayerWeapon.BaseDamage;
            _battleConfig.FireIntervalTicks =
                _battleContent.PlayerWeapon.FireIntervalTicks;
            _battleConfig.PlayerBulletSpeedNumerator =
                _battleContent.PlayerWeapon.ProjectileSpeedNumerator;
            _battleConfig.PlayerBulletSpeedDenominator =
                _battleContent.PlayerWeapon.ProjectileSpeedDenominator;
            _battleConfig.MainShotHalfWidth =
                _battleContent.PlayerWeapon.ProjectileHalfWidth;
            _battleConfig.MainShotHalfHeight =
                _battleContent.PlayerWeapon.ProjectileHalfHeight;
            _battleConfig.UseConfiguredMainShotStats = true;

            _powerUpMaxLevels = new int[PowerUpGauge.SlotCount];
            for (int i = 0; i < _powerUpMaxLevels.Length; i++)
                _powerUpMaxLevels[i] = PowerUpGauge.GetMaxLevel((PowerUpSlot)i);
            ApplyShipSpeedMultiplier(_battleConfig, _ship);
            ApplyShipWeaponProfile(_battleConfig, _ship);
            CurrentPrimaryWeaponFamily =
                PrimaryWeaponFamilyFor(_ship.WeaponType);
            if (_ship.StartingShieldStock.HasValue)
            {
                _battleConfig.StartingShieldStock =
                    Math.Min(
                        _ship.StartingShieldStock.Value,
                        _battleConfig.MaxShieldStock);
            }
            _initialShieldStock =
                Math.Min(
                    _battleConfig.StartingShieldStock,
                    _battleConfig.MaxShieldStock);
            _initialBombStock =
                _battleConfig.StartingBombStock;
            _initialMaxShieldStock =
                _battleConfig.MaxShieldStock;
            _initialMaxBombStock =
                _battleConfig.MaxBombStock;
            _initialCapsuleDropWeightReduction =
                _battleConfig.CapsuleDropWeightReduction;
            _initialFireIntervalTicks = _battleConfig.FireIntervalTicks;
            _initialMainShotBaseDamage = _battleConfig.MainShotBaseDamage;
            _initialPlayerSpeedNumerator = _battleConfig.PlayerSpeedNumerator;
            _initialPlayerSpeedDenominator = _battleConfig.PlayerSpeedDenominator;
            ApplyShipStartingLevels(PowerUpGauge);
            ResetShieldStockForNewRun();
            CurrentMissileFamily =
                _ship.StartingMissileFamily
                ?? _battleContent.DefaultMissileFamily;
            CurrentOptionFormation =
                _battleContent.DefaultOptionFormation;
            ApplyCurrentLoadoutProfiles();

            _runSeed = runSeed;
            RunNumber = 1;
            BiomeIndex = 1;
            RoomIndex = 1;
            IsBiomeBoss = false;
            IsHiddenBiome = false;
            State = RunState.Playing;
            CompletionGrade = RunCompletionGrade.None;
            SelectedColossalBoss = ColossalBossKind.None;
            EliteRoomsCleared = 0;
            NoHitBiomesCleared = 0;
            RareEncountersCleared = 0;
            _currentBiomeHit = false;
            ActiveContract = null;
            ResetContractBattleConfig();
            _lastColossalBossAtRunStart =
                ColossalBossKind.None;
            if (buildInitialStage)
                BuildCurrentStage();
        }

        public int RunNumber { get; private set; }
        /// <summary>Current biome. StageIndex remains a compatibility alias.</summary>
        public int BiomeIndex { get; private set; }
        public int StageIndex => BiomeIndex;
        /// <summary>
        /// Theme/content stage currently mapped to this progression position.
        /// Difficulty continues to follow BiomeIndex.
        /// </summary>
        public int ThemeStageIndex =>
            IsHiddenBiome
                ? BiomeCount
                : _stageThemeOrder[BiomeIndex - 1];
        public IReadOnlyList<int> StageThemeOrder =>
            _stageThemeOrderView;
        /// <summary>Current regular-room slot (1..RoomsPerBiome).</summary>
        public int RoomIndex { get; private set; }
        /// <summary>True after the final regular room while fighting the biome boss.</summary>
        public bool IsBiomeBoss { get; private set; }
        public bool IsHiddenBiome { get; private set; }
        public RunStageSection StageSection
        {
            get
            {
                if (IsHiddenBiome)
                    return IsBiomeBoss
                        ? RunStageSection.HiddenBoss
                        : RunStageSection.HiddenOpening;
                if (IsBiomeBoss)
                    return RunStageSection.StageBoss;
                if (IsMidBossSection)
                    return RunStageSection.MidBoss;
                return RoomIndex <= 1
                    ? RunStageSection.Opening
                    : RunStageSection.Closing;
            }
        }
        public bool IsMidBossSection =>
            !IsHiddenBiome
            && !IsBiomeBoss
            && RoomsPerBiome >=
                RunProgressionConfig.DefaultRoomsPerBiome
            && RoomIndex == 2;
        public RunState State { get; private set; }
        public RunCompletionGrade CompletionGrade { get; private set; }
        public ColossalBossKind SelectedColossalBoss { get; private set; }
        public ColossalBossKind LastColossalBossAtRunStart =>
            _lastColossalBossAtRunStart;
        public int EliteRoomsCleared { get; private set; }
        public int NoHitBiomesCleared { get; private set; }
        public int RareEncountersCleared { get; private set; }
        public int HiddenConditionCount =>
            CountHiddenBiomeConditions(
                EliteRoomsCleared,
                NoHitBiomesCleared,
                RareEncountersCleared);
        public bool HiddenBiomeUnlocked =>
            IsHiddenBiome || HiddenConditionCount >= 2;
        public int FinalStageIndex => _progressionConfig.FinalStageIndex;
        public int BiomeCount => _progressionConfig.BiomeCount;
        public int RoomsPerBiome => _progressionConfig.RoomsPerBiome;
        public bool IsFinished =>
            State == RunState.RunOver || State == RunState.RunCleared;
        public ulong RunSeed => _runSeed;
        public ShipDefinition Ship => _ship;
        public int DifficultyMultiplierNumerator =>
            _difficultyMultiplierNumerator;
        public int DifficultyMultiplierDenominator =>
            _difficultyMultiplierDenominator;
        /// <summary>Score earned across completed and current stages of this run.</summary>
        public long TotalScore => AddSaturated(_completedStageScore, Battle.Score);
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
                    AddSaturated(_completedGrazeCount, battle.GrazeCount),
                    _stagesCleared,
                    _roomsCleared);
            }
        }
        public int Difficulty { get; private set; }
        public StagePlan StagePlan { get; private set; }
        public IBattleSim Battle { get; private set; }
        public PowerUpGauge PowerUpGauge { get; private set; }
        /// <summary>
        /// Rule-changing rewards active for this run. They carry through death
        /// restarts, matching power-up carry policy, and start empty on a new manager.
        /// </summary>
        public BattleModifier ActiveModifiers =>
            ModifierStacks.ActiveModifiers;
        public BattleModifierStackSet ModifierStacks
        {
            get;
            private set;
        }
        public PrimaryWeaponFamily CurrentPrimaryWeaponFamily
        {
            get;
            private set;
        }
        public PrimaryWeaponFamilyDefinition CurrentPrimaryWeaponDefinition =>
            _battleContent.FindPrimaryWeaponFamily(
                CurrentPrimaryWeaponFamily);
        public MissileFamily CurrentMissileFamily { get; private set; }
        public OptionFormation CurrentOptionFormation { get; private set; }
        public int MaxShieldStock => _battleConfig.MaxShieldStock;
        public int MaxBombStock => _battleConfig.MaxBombStock;
        public int CapsuleDropWeightReduction =>
            _battleConfig.CapsuleDropWeightReduction;
        public int CapsuleBalance => _capsuleBalance;
        public int RewardRerollCost => _rewards.RerollCost;
        public bool CanRerollRewardOptions =>
            State == RunState.AwaitingReward
            && _capsuleBalance >= _rewards.RerollCost;

        /// <summary>
        /// Runtime integration point for future max-stock rewards/options.
        /// Current stock is clamped immediately when the cap is lowered.
        /// </summary>
        public void SetMaxShieldStock(int maxShieldStock)
        {
            if (maxShieldStock < 1
                || maxShieldStock
                    > BattleSimConfig.MaximumShieldStock)
                throw new ArgumentOutOfRangeException(
                    nameof(maxShieldStock),
                    $"Shield cap must be in "
                    + $"1.."
                    + $"{BattleSimConfig.MaximumShieldStock}.");
            if (!(Battle is BattleSim battle))
                throw new InvalidOperationException(
                    "Runtime shield cap changes require BattleSim.");
            battle.SetMaxShieldStock(maxShieldStock);
            _battleConfig.MaxShieldStock = maxShieldStock;
            _battleConfig.StartingShieldStock =
                Math.Min(
                    _battleConfig.StartingShieldStock,
                    maxShieldStock);
            _stageStartShieldStock =
                Math.Min(_stageStartShieldStock, maxShieldStock);
        }

        /// <summary>
        /// AwaitingReward only: two cards for MidStage, three for Main.
        /// </summary>
        public IReadOnlyList<RewardOption> RewardOptions => _rewardOptions;
        IReadOnlyList<RewardOption> _rewardOptions = Array.Empty<RewardOption>();
        public RewardSelectionKind RewardSelectionKind =>
            State == RunState.AwaitingReward
                ? _rewardSelectionKind
                : RewardSelectionKind.None;
        public IReadOnlyList<ContractOption> ContractOptions =>
            State == RunState.AwaitingContract
                ? _contractOptions
                : Array.Empty<ContractOption>();
        /// <summary>
        /// Contract affecting the current biome. Null in biome 1 and after the run.
        /// </summary>
        public ContractDefinition ActiveContract
        {
            get;
            private set;
        }
        public IReadOnlyList<ContractChoice> ContractChoiceHistory =>
            _contractChoiceHistoryView;
        public IReadOnlyList<RewardDecision> RewardDecisionHistory =>
            _rewardDecisionHistoryView;
        /// <summary>Two or three deterministic map nodes while AwaitingRoute.</summary>
        public IReadOnlyList<RouteOption> RouteOptions => _routeOptions;
        IReadOnlyList<RouteOption> _routeOptions = Array.Empty<RouteOption>();
        IReadOnlyList<RouteOption> _preparedRouteOptions =
            Array.Empty<RouteOption>();
        public IReadOnlyList<RouteChoice> RouteChoiceHistory =>
            _routeChoiceHistoryView;

        /// <summary>
        /// Exports the checkpoint captured immediately before the current room or
        /// biome-boss boundary's tick zero. Calling this during combat therefore
        /// resumes at that boundary, not at the current tick. Choice and terminal
        /// states are rejected because they are not resumable playing boundaries.
        /// </summary>
        public RunSuspendData ExportSuspendData()
        {
            if (State != RunState.Playing)
                throw new InvalidOperationException(
                    "Suspend data can only be exported while a stage is playing.");

            int acquisitionCount = 0;
            for (int i = 0;
                i < _stageStartRewardAcquisitionCounts.Length;
                i++)
            {
                if (_stageStartRewardAcquisitionCounts[i] > 0)
                    acquisitionCount++;
            }

            var acquisitions =
                new RewardAcquisitionData[acquisitionCount];
            int destination = 0;
            for (int i = 0;
                i < _stageStartRewardAcquisitionCounts.Length;
                i++)
            {
                int count = _stageStartRewardAcquisitionCounts[i];
                if (count == 0)
                    continue;
                acquisitions[destination++] = new RewardAcquisitionData
                {
                    rewardId = _rewards.All[i].Id,
                    count = count
                };
            }

            var routeChoices =
                new RouteChoiceData[_routeChoiceHistory.Count];
            for (int i = 0; i < _routeChoiceHistory.Count; i++)
            {
                RouteChoice choice = _routeChoiceHistory[i];
                routeChoices[i] = new RouteChoiceData
                {
                    stageIndex = choice.StageIndex,
                    biomeIndex = choice.BiomeIndex,
                    roomIndex = choice.RoomIndex,
                    optionIndex = choice.OptionIndex,
                    themeId = choice.ThemeId,
                    encounterType = (int)choice.EncounterType
                };
            }
            var contractChoices =
                new ContractChoiceData[
                    _contractChoiceHistory.Count];
            for (int i = 0;
                i < _contractChoiceHistory.Count;
                i++)
            {
                ContractChoice choice =
                    _contractChoiceHistory[i];
                contractChoices[i] =
                    new ContractChoiceData
                    {
                        targetBiomeIndex =
                            choice.TargetBiomeIndex,
                        optionIndex = choice.OptionIndex,
                        contractId = choice.ContractId,
                        destinationKind =
                            (int)choice.DestinationKind,
                        destinationThemeId =
                            choice.DestinationThemeId,
                        destinationThemeStageIndex =
                            choice.DestinationThemeStageIndex
                    };
            }
            var rewardDecisions =
                new RewardDecisionData[
                    _rewardDecisionHistory.Count];
            for (int i = 0;
                i < _rewardDecisionHistory.Count;
                i++)
            {
                RewardDecision decision =
                    _rewardDecisionHistory[i];
                rewardDecisions[i] =
                    new RewardDecisionData
                    {
                        rewardSequence =
                            decision.RewardSequence,
                        selectionKind =
                            (int)decision.SelectionKind,
                        decisionKind =
                            (int)decision.DecisionKind,
                        optionIndex =
                            decision.OptionIndex
                    };
            }

            var data = new RunSuspendData
            {
                schemaVersion = RunSuspendData.CurrentSchemaVersion,
                runSeed = _runSeed,
                runNumber = RunNumber,
                stageIndex = StageIndex,
                biomeIndex = BiomeIndex,
                roomIndex = RoomIndex,
                isBiomeBoss = IsBiomeBoss,
                score = _stageStartScore,
                shotsFired = _stageStartShotsFired,
                shotsHit = _stageStartShotsHit,
                kills = _stageStartKills,
                capsulesCollected = _stageStartCapsulesCollected,
                grazeCount = _stageStartGrazeCount,
                stagesCleared = _stageStartStagesCleared,
                roomsCleared = _stageStartRoomsCleared,
                powerUpLevels =
                    (int[])_stageStartPowerUpLevels.Clone(),
                powerUpProgress =
                    (int[])_stageStartPowerUpProgress.Clone(),
                powerUpCursor = _stageStartPowerUpCursor,
                playerHp = _stageStartPlayerLife,
                shieldRemaining = _stageStartShieldStock,
                shieldStock = _stageStartShieldStock,
                maxShieldStock = _battleConfig.MaxShieldStock,
                bombStock = _stageStartBombStock,
                maxBombStock = _battleConfig.MaxBombStock,
                rewardAcquisitions = acquisitions,
                activeModifiers = (int)_stageStartActiveModifiers,
                primaryWeaponFamily =
                    (int)_stageStartPrimaryWeaponFamily,
                missileFamily = (int)_stageStartMissileFamily,
                optionFormation = (int)_stageStartOptionFormation,
                shipId = _ship.Id,
                fireIntervalTicks = _stageStartFireIntervalTicks,
                mainShotBaseDamage = _stageStartMainShotBaseDamage,
                playerSpeedNumerator =
                    _stageStartPlayerSpeedNumerator,
                playerSpeedDenominator =
                    _stageStartPlayerSpeedDenominator,
                difficultyMultiplierNumerator =
                    _difficultyMultiplierNumerator,
                difficultyMultiplierDenominator =
                    _difficultyMultiplierDenominator,
                routeChoices = routeChoices,
                finalStageIndex = FinalStageIndex,
                biomeCount = BiomeCount,
                roomsPerBiome = RoomsPerBiome,
                isHiddenBiome = IsHiddenBiome,
                eliteRoomsCleared =
                    _stageStartEliteRoomsCleared,
                noHitBiomesCleared =
                    _stageStartNoHitBiomesCleared,
                rareEncountersCleared =
                    _stageStartRareEncountersCleared,
                currentBiomeHit =
                    _stageStartCurrentBiomeHit,
                selectedColossalBoss =
                    (int)SelectedColossalBoss,
                lastColossalBossAtRunStart =
                    (int)_lastColossalBossAtRunStart,
                hasStageStartContinuity = true,
                stageStartPlayerX =
                    _stageStartContinuity.PlayerX,
                stageStartPlayerY =
                    _stageStartContinuity.PlayerY,
                stageStartMultiplierLevel =
                    _stageStartContinuity.MultiplierLevel,
                stageStartComboGauge =
                    _stageStartContinuity.ComboGauge,
                stageStartTicksSinceLastKill =
                    _stageStartContinuity.TicksSinceLastKill,
                activeContractId =
                    ActiveContract?.Id,
                contractChoices = contractChoices,
                capsuleDropWeightReduction =
                    _battleConfig
                        .CapsuleDropWeightReduction,
                capsuleBalance =
                    _stageStartCapsuleBalance,
                rewardDecisions =
                    rewardDecisions
            };
            SaveDataIntegrity.Seal(data);
            return data;
        }

        public static RunManager ResumeFromSuspendData(
            RunSuspendData data,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge)
        {
            return ResumeFromSuspendData(
                data,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                null,
                null);
        }

        public static RunManager ResumeFromSuspendData(
            RunSuspendData data,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            RewardCatalog rewards,
            ShipDefinition ship)
        {
            return ResumeFromSuspendData(
                data,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                new MetaProgression(1, 1),
                StageDifficultyCurve.CreateDefault(),
                rewards,
                ship);
        }

        /// <summary>
        /// Validates serializer-facing data before generating a stage, then
        /// reconstructs the exact persistent state present at that stage boundary.
        /// The supplied ship must match data.shipId.
        /// </summary>
        public static RunManager ResumeFromSuspendData(
            RunSuspendData data,
            IStageGenerator stageGenerator,
            BattleSimConfig battleConfig,
            BattleContent battleContent,
            PowerUpGauge powerUpGauge,
            MetaProgression metaProgression,
            StageDifficultyCurve difficultyCurve,
            RewardCatalog rewards,
            ShipDefinition ship,
            ContractCatalog contracts = null)
        {
            data = SaveDataIntegrity.MigrateAndValidate(data);
            if (stageGenerator == null)
                throw new ArgumentNullException(nameof(stageGenerator));
            if (battleConfig == null)
                throw new ArgumentNullException(nameof(battleConfig));
            if (battleContent == null)
                throw new ArgumentNullException(nameof(battleContent));
            if (powerUpGauge == null)
                throw new ArgumentNullException(nameof(powerUpGauge));
            if (metaProgression == null)
                throw new ArgumentNullException(nameof(metaProgression));
            if (difficultyCurve == null)
                throw new ArgumentNullException(nameof(difficultyCurve));

            RewardCatalog resolvedRewards = rewards ?? BuiltInRewards;
            ContractCatalog resolvedContracts =
                contracts ?? BuiltInContracts;
            ShipDefinition resolvedShip =
                ship ?? ShipDefinition.CreateDefault();
            ValidateSuspendData(
                data,
                powerUpGauge,
                resolvedRewards,
                resolvedShip);
            ResolveSuspendDifficulty(
                data,
                out int difficultyMultiplierNumerator,
                out int difficultyMultiplierDenominator);

            var manager = new RunManager(
                data.runSeed,
                stageGenerator,
                battleConfig,
                battleContent,
                powerUpGauge,
                metaProgression,
                difficultyCurve,
                resolvedRewards,
                resolvedShip,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                new RunProgressionConfig(
                    data.biomeCount,
                    data.roomsPerBiome),
                false,
                resolvedContracts);

            manager._runSeed = data.runSeed;
            manager.RunNumber = data.runNumber;
            manager.BiomeIndex = data.isHiddenBiome
                ? data.biomeCount
                : data.biomeIndex;
            manager.RoomIndex = data.roomIndex;
            manager.IsBiomeBoss = data.isBiomeBoss;
            manager.IsHiddenBiome = data.isHiddenBiome;
            manager.EliteRoomsCleared =
                data.eliteRoomsCleared;
            manager.NoHitBiomesCleared =
                data.noHitBiomesCleared;
            manager.RareEncountersCleared =
                data.rareEncountersCleared;
            manager._currentBiomeHit =
                data.currentBiomeHit;
            manager.SelectedColossalBoss =
                (ColossalBossKind)data.selectedColossalBoss;
            manager._lastColossalBossAtRunStart =
                (ColossalBossKind)data.lastColossalBossAtRunStart;
            manager.CompletionGrade =
                RunCompletionGrade.None;
            manager.State = RunState.Playing;
            manager._rewardOptions = Array.Empty<RewardOption>();
            manager._routeOptions = Array.Empty<RouteOption>();
            manager._completedStageScore = data.score;
            manager._completedShotsFired = data.shotsFired;
            manager._completedShotsHit = data.shotsHit;
            manager._completedKills = data.kills;
            manager._completedCapsulesCollected =
                data.capsulesCollected;
            manager._capsuleBalance = data.capsuleBalance;
            manager._completedGrazeCount = data.grazeCount;
            manager._stagesCleared = data.stagesCleared;
            manager._roomsCleared = data.roomsCleared;
            manager.CurrentPrimaryWeaponFamily =
                data.primaryWeaponFamily < 0
                    ? PrimaryWeaponFamilyFor(
                        resolvedShip.WeaponType)
                    : (PrimaryWeaponFamily)
                        data.primaryWeaponFamily;
            manager.CurrentMissileFamily =
                (MissileFamily)data.missileFamily;
            manager.CurrentOptionFormation =
                (OptionFormation)data.optionFormation;
            manager.ApplyPrimaryWeaponFamilyProfile(
                manager._battleContent.FindPrimaryWeaponFamily(
                    manager.CurrentPrimaryWeaponFamily)
                ?? throw new ArgumentException(
                    "Suspend primary weapon family is unavailable.",
                    nameof(data)));
            manager.ApplyCurrentLoadoutProfiles();
            manager._battleConfig.MaxShieldStock =
                data.maxShieldStock;
            manager._battleConfig.StartingShieldStock =
                data.shieldStock;
            manager._battleConfig.MaxBombStock =
                data.maxBombStock;
            manager._battleConfig.StartingBombStock =
                data.bombStock;
            manager._battleConfig.FireIntervalTicks =
                data.fireIntervalTicks;
            manager._battleConfig.MainShotBaseDamage =
                data.mainShotBaseDamage;
            manager._battleConfig.PlayerSpeedNumerator =
                data.playerSpeedNumerator;
            manager._battleConfig.PlayerSpeedDenominator =
                data.playerSpeedDenominator;
            manager._battleConfig.CapsuleDropWeightReduction =
                data.capsuleDropWeightReduction;
            manager.RestoreContractState(
                data.activeContractId,
                data.contractChoices,
                resolvedContracts);
            manager.RestoreRewardDecisionHistory(
                data.rewardDecisions);

            RestoreRewardAcquisitions(
                data.rewardAcquisitions,
                resolvedRewards,
                manager._rewardAcquisitionCounts);
            manager.RebuildModifierStacksFromAcquisitions(
                (BattleModifier)data.activeModifiers);
            manager.RestoreRouteChoices(
                data.routeChoices,
                false);
            manager.PowerUpGauge.RestoreState(
                data.powerUpLevels,
                data.powerUpCursor,
                data.powerUpProgress);
            if (data.hasStageStartContinuity)
            {
                manager._pendingBattleContinuity =
                    new BattleContinuityState(
                        data.stageStartPlayerX,
                        data.stageStartPlayerY,
                        data.stageStartMultiplierLevel,
                        data.stageStartComboGauge,
                        data.stageStartTicksSinceLastKill);
            }
            manager.BuildCurrentStage();

            if (manager.Battle.PlayerHp != data.playerHp
                || manager.Battle.ShieldStock != data.shieldStock
                || manager.Battle.BombStock != data.bombStock)
            {
                throw new ArgumentException(
                    "Suspend player life, shield stock, or bomb stock does not match "
                    + "the reconstructed stage boundary.",
                    nameof(data));
            }
            return manager;
        }

        public void Step(in InputCommand input)
        {
            if (State != RunState.Playing)
                return;

            bool activatePressed = input.Activate && !_activateHeld;
            _activateHeld = input.Activate;

            InputCommand battleInput =
                input.WithActivate(activatePressed);
            Battle.Step(in battleInput);
            if (Battle is BattleSim weaponBattle
                && weaponBattle.EquippedPrimaryWeaponFamily
                    != CurrentPrimaryWeaponFamily)
            {
                SwitchPrimaryWeaponFamily(
                    weaponBattle.EquippedPrimaryWeaponFamily);
            }
            ObserveBattleEvents();
            // Death is authoritative and wins over every room/boss-clear
            // transition produced by the same battle tick.
            if (!Battle.IsPlayerAlive)
            {
                State = RunState.RunOver;
                return;
            }

            // 보스전이 있는 스테이지는 StageCleared(보스 격파)로 끝나고 보상 선택으로 넘어간다.
            // 보스 데이터가 없는 플랜(레거시/테스트)은 기존 틱 소진 규칙 유지.
            if (IsBiomeBoss)
            {
                bool bossCleared =
                    Battle is BattleSim battleSim
                    && battleSim.HasBossBattle
                        ? battleSim.BossDefeated
                        : Battle.Tick >= _stageLengthTicks;
                if (bossCleared)
                {
                    IncrementStagesCleared();
                    if (IsHiddenBiome)
                        CompleteRun(
                            RunCompletionGrade.PerfectClear);
                    else
                    {
                        RecordNoHitBiomeClear();
                        BeginRewardSelection(
                            RewardSelectionKind.Main);
                    }
                }
                return;
            }

            bool sectionCleared =
                IsMidBossSection
                && Battle is BattleSim midBossBattle
                && midBossBattle.HasBossBattle
                    ? midBossBattle.BossDefeated
                    : Battle.Tick >= _stageLengthTicks;
            if (sectionCleared)
            {
                IncrementRoomsCleared();
                if (IsHiddenBiome)
                {
                    if (RoomIndex
                        >= RunProgressionConfig.HiddenRooms)
                        AdvanceToBiomeBoss();
                    else
                        AdvanceHiddenRoom();
                    return;
                }
                if (!IsMidBossSection
                    && StagePlan.EncounterType == EncounterType.Elite
                    && EliteRoomsCleared < int.MaxValue)
                    EliteRoomsCleared++;
                if (StagePlan.EncounterType == EncounterType.Rare
                    && RareEncountersCleared < int.MaxValue)
                    RareEncountersCleared++;
                if (IsMidBossSection)
                    BeginRewardSelection(
                        RewardSelectionKind.MidStage);
                else
                    AdvanceAfterRegularSection();
            }
        }

        void BeginRewardSelection(RewardSelectionKind kind)
        {
            if (kind != RewardSelectionKind.MidStage
                && kind != RewardSelectionKind.Main)
                throw new ArgumentOutOfRangeException(nameof(kind));
            _rewardSelectionKind = kind;
            _rewardSelectionsRemaining = 1;
            _rewardSelectionRound = 0;
            int optionCount =
                kind == RewardSelectionKind.MidStage
                    ? MidStageRewardOptionCount
                    : MainRewardOptionCount;
            if (ActiveContract != null)
                optionCount = Math.Max(
                    1,
                    Math.Min(
                        _rewardOptionBuffer.Length,
                        optionCount
                            + ActiveContract
                                .RewardOptionCountDelta));
            IReadOnlyList<RewardOption> options =
                GenerateRewardOptions(optionCount);
            if (options.Count != optionCount)
                throw new InvalidOperationException(
                    "Reward selection cannot begin without "
                    + $"{optionCount} choices.");
            _rewardOptions = options;
            State = RunState.AwaitingReward;
        }

        void CompleteRun(RunCompletionGrade grade)
        {
            if (grade != RunCompletionGrade.StandardClear
                && grade != RunCompletionGrade.PerfectClear)
                throw new ArgumentOutOfRangeException(nameof(grade));
            _rewardSelectionsRemaining = 0;
            _rewardSelectionRound = 0;
            _rewardSelectionKind = RewardSelectionKind.None;
            _rewardOptionView.SetCount(0);
            _rewardOptions = Array.Empty<RewardOption>();
            _routeOptions = Array.Empty<RouteOption>();
            _preparedRouteOptions = Array.Empty<RouteOption>();
            _contractOptions =
                Array.Empty<ContractOption>();
            ActiveContract = null;
            CompletionGrade = grade;
            State = RunState.RunCleared;
        }

        void ObserveBattleEvents()
        {
            ReadOnlySpan<SimEvent> events =
                Battle.EventsThisTick;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type == SimEventType.PlayerHit)
                    _currentBiomeHit = true;
                else if (events[i].Type
                    == SimEventType.CapsulePicked)
                    AddCapsuleCurrency(1);
            }
        }

        void AddCapsuleCurrency(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            long total = (long)_capsuleBalance + amount;
            _capsuleBalance = total >= int.MaxValue
                ? int.MaxValue
                : (int)total;
        }

        void RecordNoHitBiomeClear()
        {
            if (!_currentBiomeHit
                && NoHitBiomesCleared < int.MaxValue)
                NoHitBiomesCleared++;
            _currentBiomeHit = false;
        }

        bool TryBeginHiddenBiome()
        {
            ColossalBossKind selected = SelectColossalBoss(
                _runSeed,
                _lastColossalBossAtRunStart);
            if (!(_stageGenerator
                    is IColossalBossStageGenerator colossal)
                || !colossal.CanGenerateColossalBoss(
                    selected))
                return false;

            _pendingBattleContinuity = null;
            AccumulateCompletedBattle();
            SelectedColossalBoss = selected;
            IsHiddenBiome = true;
            // Hidden content extends the final biome; it is not a sixth public
            // campaign biome. Keep the HUD/save-facing progression bounded.
            BiomeIndex = BiomeCount;
            RoomIndex = 1;
            IsBiomeBoss = false;
            State = RunState.Playing;
            _rewardOptions = Array.Empty<RewardOption>();
            _routeOptions = Array.Empty<RouteOption>();
            _preparedRouteOptions = Array.Empty<RouteOption>();
            _metaState?.RecordColossalBossEncounter(
                SelectedColossalBoss);
            BuildCurrentStage();
            return true;
        }

        public static int CountHiddenBiomeConditions(
            int eliteRoomsCleared,
            int noHitBiomesCleared,
            int rareEncountersCleared)
        {
            if (eliteRoomsCleared < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(eliteRoomsCleared));
            if (noHitBiomesCleared < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(noHitBiomesCleared));
            if (rareEncountersCleared < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(rareEncountersCleared));
            return (eliteRoomsCleared >= 3 ? 1 : 0)
                + (noHitBiomesCleared >= 2 ? 1 : 0)
                + (rareEncountersCleared >= 1 ? 1 : 0);
        }

        public static bool MeetsHiddenBiomeConditions(
            int eliteRoomsCleared,
            int noHitBiomesCleared,
            int rareEncountersCleared)
        {
            return CountHiddenBiomeConditions(
                eliteRoomsCleared,
                noHitBiomesCleared,
                rareEncountersCleared) >= 2;
        }

        public static ColossalBossKind SelectColossalBoss(
            ulong runSeed,
            ColossalBossKind lastEncounteredBoss)
        {
            if (!Enum.IsDefined(
                    typeof(ColossalBossKind),
                    lastEncounteredBoss))
                throw new ArgumentOutOfRangeException(
                    nameof(lastEncounteredBoss));
            int leviathanWeight =
                lastEncounteredBoss
                    == ColossalBossKind.Broodmother
                ? 3
                : 1;
            int broodmotherWeight =
                lastEncounteredBoss
                    == ColossalBossKind.Leviathan
                ? 3
                : 1;
            Rng rng = new Rng(runSeed)
                .Fork(ColossalBossSelectionStream);
            int roll = rng.NextInt(
                0,
                leviathanWeight + broodmotherWeight);
            return roll < leviathanWeight
                ? ColossalBossKind.Leviathan
                : ColossalBossKind.Broodmother;
        }

        void SetLastColossalBossAtRunStart(
            ColossalBossKind boss)
        {
            if (!Enum.IsDefined(
                    typeof(ColossalBossKind),
                    boss))
                throw new ArgumentOutOfRangeException(nameof(boss));
            _lastColossalBossAtRunStart = boss;
        }

        /// <summary>
        /// 보상을 확정하고 다음 스테이지를 시작한다. 선택은 플레이어 입력이므로
        /// 리플레이 기록 대상이다 (같은 시드 + 같은 선택 = 같은 런).
        /// </summary>
        public bool ChooseReward(int optionIndex)
        {
            if (State != RunState.AwaitingReward)
                return false;
            if (optionIndex < 0 || optionIndex >= _rewardOptions.Count)
                return false;
            _rewardDecisionHistory.Add(new RewardDecision(
                GetRewardSequence(),
                _rewardSelectionKind,
                RewardDecisionKind.Select,
                optionIndex));
            int catalogIndex = _rewardOptionCatalogIndices[optionIndex];
            ApplyReward(_rewardOptions[optionIndex], catalogIndex);
            if (catalogIndex >= 0
                && _rewardAcquisitionCounts[catalogIndex] < int.MaxValue)
                _rewardAcquisitionCounts[catalogIndex]++;
            _rewardSelectionsRemaining--;
            if (_rewardSelectionsRemaining > 0)
            {
                _rewardSelectionRound++;
                _rewardOptions = GenerateRewardOptions(
                    _rewardSelectionKind
                        == RewardSelectionKind.MidStage
                            ? MidStageRewardOptionCount
                            : MainRewardOptionCount);
                return true;
            }
            _rewardOptions = Array.Empty<RewardOption>();
            RewardSelectionKind completedKind =
                _rewardSelectionKind;
            _rewardSelectionKind = RewardSelectionKind.None;
            _rewardOptionView.SetCount(0);
            if (completedKind == RewardSelectionKind.MidStage)
            {
                AdvanceAfterRegularSection();
                return true;
            }
            if (completedKind != RewardSelectionKind.Main)
                throw new InvalidOperationException(
                    "Reward selection kind was lost.");
            if (_progressionConfig.IsFinalBiome(BiomeIndex))
            {
                BeginFinalContractSelection();
                return true;
            }
            BeginContractSelection();
            return true;
        }

        public bool RerollRewardOptions()
        {
            if (!CanRerollRewardOptions)
                return false;
            int optionCount = _rewardOptions.Count;
            _rewardSelectionRound++;
            IReadOnlyList<RewardOption> options =
                GenerateRewardOptions(optionCount);
            if (options.Count != optionCount)
                throw new InvalidOperationException(
                    "Reward reroll did not reproduce the current card count.");
            _capsuleBalance -= _rewards.RerollCost;
            _rewardOptions = options;
            _rewardDecisionHistory.Add(new RewardDecision(
                GetRewardSequence(),
                _rewardSelectionKind,
                RewardDecisionKind.Reroll,
                -1));
            return true;
        }

        void BeginContractSelection()
        {
            if (BiomeIndex >= BiomeCount)
                throw new InvalidOperationException(
                    "The final biome has no outgoing contract.");
            _contractOptions =
                GenerateContractOptions(BiomeIndex + 1);
            if (_contractOptions.Count == 0)
                _contractOptions = Array.AsReadOnly(new[]
                {
                    CreateContractOption(
                        _contracts.Standard,
                        BiomeIndex + 1,
                        _stageThemeOrder[BiomeIndex])
                });
            State = RunState.AwaitingContract;
        }

        void BeginFinalContractSelection()
        {
            ContractDefinition endRun =
                _contracts.EndRun
                ?? BuiltInEndRunContract;
            ContractDefinition uncharted =
                _contracts.Uncharted
                ?? BuiltInUnchartedContract;
            bool canGenerate =
                _stageGenerator
                    is IColossalBossStageGenerator colossal
                && colossal.CanGenerateColossalBoss(
                    SelectColossalBoss(
                        _runSeed,
                        _lastColossalBossAtRunStart));
            if (canGenerate
                && uncharted.IsEligible(
                    EliteRoomsCleared,
                    NoHitBiomesCleared,
                    RareEncountersCleared))
            {
                _contractOptions = Array.AsReadOnly(new[]
                {
                    new ContractOption(endRun, null),
                    new ContractOption(uncharted, null)
                });
            }
            else
            {
                _contractOptions = Array.AsReadOnly(new[]
                {
                    new ContractOption(endRun, null)
                });
            }
            State = RunState.AwaitingContract;
        }

        public bool ChooseContract(int optionIndex)
        {
            if (State != RunState.AwaitingContract)
                return false;
            if (optionIndex < 0
                || optionIndex >= _contractOptions.Count)
                return false;
            ContractOption selectedOption =
                _contractOptions[optionIndex];
            ContractDefinition selected =
                selectedOption?.Definition
                ?? _contracts.Standard;
            int targetBiome = BiomeIndex + 1;
            if (!selected.IsEligible(
                    EliteRoomsCleared,
                    NoHitBiomesCleared,
                    RareEncountersCleared))
                return false;
            if (selected.DestinationKind
                    == ContractDestinationKind.NextStage
                && BiomeIndex >= BiomeCount)
                return false;
            if (selected.DestinationKind
                    != ContractDestinationKind.NextStage
                && BiomeIndex != BiomeCount)
                return false;
            ActiveContract = selected;
            if (selected.DestinationKind
                == ContractDestinationKind.NextStage)
                ApplyContractDestination(
                    selectedOption,
                    targetBiome);
            _battleConfig.ContractCapsuleDropMultiplierNumerator =
                selected.CapsuleDropNumerator;
            _battleConfig.ContractCapsuleDropMultiplierDenominator =
                selected.CapsuleDropDenominator;
            _battleConfig.ContractBombDropMultiplierNumerator =
                selected.BombDropNumerator;
            _battleConfig.ContractBombDropMultiplierDenominator =
                selected.BombDropDenominator;
            _battleConfig.ContractGuaranteesBombDrop =
                selected.GuaranteedBombDrop;
            _battleConfig.ContractScoreMultiplierNumerator =
                selected.ScoreMultiplierNumerator;
            _battleConfig.ContractScoreMultiplierDenominator =
                selected.ScoreMultiplierDenominator;
            _contractChoiceHistory.Add(new ContractChoice(
                targetBiome,
                optionIndex,
                selected.Id,
                selected.DestinationKind,
                selectedOption?.DestinationThemeId,
                selectedOption?.DestinationThemeStageIndex ?? 0));
            _contractOptions =
                Array.Empty<ContractOption>();
            if (selected.DestinationKind
                == ContractDestinationKind.EndRun)
            {
                CompleteRun(
                    RunCompletionGrade.StandardClear);
                return true;
            }
            if (selected.DestinationKind
                == ContractDestinationKind.Uncharted)
            {
                if (!TryBeginHiddenBiome())
                    throw new InvalidOperationException(
                        "The selected uncharted destination is unavailable.");
                return true;
            }
            AdvanceBiome();
            return true;
        }

        IReadOnlyList<ContractOption> GenerateContractOptions(
            int targetBiomeIndex)
        {
            _contractRng.ResetForked(
                _runSeed,
                ContractSelectionStream,
                targetBiomeIndex);
            int optionCount = _contracts.MinimumOptionCount;
            if (_contracts.MaximumOptionCount
                > _contracts.MinimumOptionCount)
            {
                optionCount = _contractRng.NextInt(
                    _contracts.MinimumOptionCount,
                    _contracts.MaximumOptionCount + 1);
            }

            var pool = new ContractDefinition[
                _contracts.All.Count - 1];
            var weights = new int[pool.Length];
            int poolCount = 0;
            for (int i = 0; i < _contracts.All.Count; i++)
            {
                ContractDefinition contract = _contracts.All[i];
                if (ReferenceEquals(contract, _contracts.Standard))
                    continue;
                if (contract.DestinationKind
                    != ContractDestinationKind.NextStage)
                    continue;
                if (!contract.IsEligible(
                        EliteRoomsCleared,
                        NoHitBiomesCleared,
                        RareEncountersCleared))
                    continue;
                pool[poolCount] = contract;
                weights[poolCount] = contract.Weight;
                poolCount++;
            }
            optionCount = Math.Min(
                optionCount,
                poolCount + 1);
            var options =
                new ContractDefinition[optionCount];
            options[0] = _contracts.Standard;
            for (int option = 1; option < optionCount; option++)
            {
                int pick = _contractRng.PickWeighted(
                    weights,
                    poolCount);
                options[option] = pool[pick];
                int last = --poolCount;
                pool[pick] = pool[last];
                weights[pick] = weights[last];
            }
            return BindContractDestinations(
                options,
                targetBiomeIndex);
        }

        IReadOnlyList<ContractOption> BindContractDestinations(
            ContractDefinition[] definitions,
            int targetBiomeIndex)
        {
            int firstPosition = targetBiomeIndex - 1;
            int lastShuffledPosition = Math.Min(
                3,
                BiomeCount - 2);
            int candidateCount = targetBiomeIndex >= 2
                && targetBiomeIndex <= 4
                && firstPosition <= lastShuffledPosition
                    ? lastShuffledPosition - firstPosition + 1
                    : 1;
            var options = new ContractOption[definitions.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int candidatePosition = candidateCount == 1
                    ? firstPosition
                    : firstPosition + i % candidateCount;
                int themeStageIndex =
                    _stageThemeOrder[candidatePosition];
                options[i] = CreateContractOption(
                    definitions[i],
                    targetBiomeIndex,
                    themeStageIndex);
            }
            return Array.AsReadOnly(options);
        }

        ContractOption CreateContractOption(
            ContractDefinition definition,
            int targetBiomeIndex,
            int themeStageIndex)
        {
            return new ContractOption(
                definition,
                GetDestinationThemeId(
                    themeStageIndex,
                    targetBiomeIndex),
                themeStageIndex);
        }

        string GetDestinationThemeId(
            int themeStageIndex,
            int targetBiomeIndex)
        {
            if (_stageGenerator is IRouteStageGenerator routeGenerator
                && themeStageIndex >= 1
                && themeStageIndex <= routeGenerator.ThemeIds.Count)
                return routeGenerator.ThemeIds[themeStageIndex - 1];
            StagePlan plan = _stageGenerator.Generate(
                _runSeed,
                themeStageIndex,
                _difficultyCurve.GetDifficulty(targetBiomeIndex));
            if (plan == null)
                throw new InvalidOperationException(
                    "The stage generator returned no destination theme.");
            return !string.IsNullOrEmpty(plan.ThemeId)
                ? plan.ThemeId
                : !string.IsNullOrEmpty(plan.RequestedThemeId)
                    ? plan.RequestedThemeId
                    : $"stage_{themeStageIndex}";
        }

        void ApplyContractDestination(
            ContractOption option,
            int targetBiomeIndex)
        {
            if (option == null
                || option.DestinationThemeStageIndex < 1)
                throw new InvalidOperationException(
                    "The selected contract has no destination theme stage.");
            int targetPosition = targetBiomeIndex - 1;
            int sourcePosition = -1;
            int lastShuffledPosition = Math.Min(
                3,
                BiomeCount - 2);
            int searchEnd = targetBiomeIndex >= 2
                && targetBiomeIndex <= 4
                && targetPosition <= lastShuffledPosition
                    ? lastShuffledPosition
                    : targetPosition;
            for (int i = targetPosition; i <= searchEnd; i++)
                if (_stageThemeOrder[i]
                    == option.DestinationThemeStageIndex)
                {
                    sourcePosition = i;
                    break;
                }
            if (sourcePosition < 0)
                throw new InvalidOperationException(
                    "The selected destination is no longer in the remaining theme pool.");
            int displaced = _stageThemeOrder[targetPosition];
            _stageThemeOrder[targetPosition] =
                option.DestinationThemeStageIndex;
            _stageThemeOrder[sourcePosition] = displaced;
        }

        [Obsolete(
            "REQ-054 removed route selection. Legacy route payloads remain "
            + "readable but are not actionable.")]
        public void ChooseRoute(int optionIndex)
        {
            throw new NotSupportedException(
                "Route choices were removed by REQ-054.");
        }

        void AdvanceAfterRegularSection()
        {
            _preparedRouteOptions = Array.Empty<RouteOption>();
            _routeOptions = Array.Empty<RouteOption>();
            if (RoomIndex >= RoomsPerBiome)
                AdvanceToBiomeBoss();
            else
                AdvanceRoom();
        }

        void ApplyReward(
            in RewardOption option,
            int catalogIndex)
        {
            switch (option.Type)
            {
                case RewardType.Capsules:
                    for (int i = 0; i < option.Amount; i++)
                        PowerUpGauge.Collect();
                    AddCapsuleCurrency(option.Amount);
                    break;
                case RewardType.SlotLevel:
                    PowerUpGauge.GrantLevels(
                        option.Slot,
                        option.Amount);
                    break;
                case RewardType.ShieldStock:
                    if (!(Battle is BattleSim battle))
                        throw new InvalidOperationException(
                            "Shield stock recovery requires BattleSim.");
                    battle.RecoverShieldStock(option.Amount);
                    break;
                case RewardType.BombStock:
                    if (!(Battle is BattleSim bombBattle))
                        throw new InvalidOperationException(
                            "Bomb stock acquisition requires BattleSim.");
                    bombBattle.AcquireBombStock(option.Amount);
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
                case RewardType.Modifier:
                {
                    RewardDefinition definition =
                        _rewards.All[catalogIndex];
                    if (!ModifierStacks.TryAdd(
                            definition.ModifierId,
                            definition.ModifierStackStrength,
                            definition.ModifierInteractionCost,
                            definition.ModifierMaxStacks))
                    {
                        throw new InvalidOperationException(
                            $"Modifier '{definition.ModifierKey}' "
                            + "exceeds its stack or combination cap.");
                    }
                    break;
                }
                case RewardType.MissileFamily:
                    CurrentMissileFamily = option.MissileFamily;
                    ApplyCurrentLoadoutProfiles();
                    break;
                case RewardType.OptionFormation:
                    CurrentOptionFormation = option.OptionFormation;
                    ApplyCurrentLoadoutProfiles();
                    break;
                case RewardType.PrimaryWeaponFamily:
                    SwitchPrimaryWeaponFamily(
                        option.PrimaryWeaponFamily);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown reward type {option.Type}.");
            }
            ApplyRewardCosts(option.Costs);
        }

        void ApplyRewardCosts(
            IReadOnlyList<RewardEffectView> costs)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                RewardEffectView cost = costs[i];
                switch (cost.Type)
                {
                    case RewardEffectType.ShieldMaxDown:
                        SetMaxShieldStock(Math.Max(
                            1,
                            MaxShieldStock - cost.Amount));
                        break;
                    case RewardEffectType.MoveSpeedDown:
                        RemoveMoveSpeed(_battleConfig, cost.Amount);
                        break;
                    case RewardEffectType.CapsuleDropWeightDown:
                        _battleConfig.CapsuleDropWeightReduction =
                            SaturatingAdd(
                                _battleConfig.CapsuleDropWeightReduction,
                                cost.Amount);
                        break;
                    case RewardEffectType.BombMaxDown:
                        if (!(Battle is BattleSim bombBattle))
                            throw new InvalidOperationException(
                                "Bomb cap costs require BattleSim.");
                        int bombCap = Math.Max(
                            1,
                            _battleConfig.MaxBombStock - cost.Amount);
                        bombBattle.SetMaxBombStock(bombCap);
                        _battleConfig.MaxBombStock = bombCap;
                        _battleConfig.StartingBombStock = Math.Min(
                            _battleConfig.StartingBombStock,
                            bombCap);
                        _stageStartBombStock = Math.Min(
                            _stageStartBombStock,
                            bombCap);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown reward cost type {cost.Type}.");
                }
            }
        }

        /// <summary>시드·스테이지·주입 카탈로그의 결정론적 가중 비복원 선택.</summary>
        IReadOnlyList<RewardOption> GenerateRewardOptions(
            int optionCount)
        {
            if (optionCount < 1
                || optionCount > _rewardOptionBuffer.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(optionCount));
            IReadOnlyList<RewardDefinition> rewards = _rewards.All;
            int eligibleCount = 0;
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardDefinition reward = rewards[i];
                if (_rewardSelectionKind == RewardSelectionKind.MidStage
                    && reward.Pool == RewardPool.Main)
                    continue;
                if (_rewardSelectionKind == RewardSelectionKind.Main
                    && reward.Pool == RewardPool.Mid)
                    continue;
                if (BiomeIndex < reward.StageIndexMin
                    || BiomeIndex > reward.StageIndexMax)
                    continue;
                if (reward.MaxPerRun.HasValue
                    && _rewardAcquisitionCounts[i] >= reward.MaxPerRun.Value)
                    continue;
                if (reward.Type == RewardType.Modifier
                    && !ModifierStacks.CanAdd(
                        reward.ModifierId,
                        reward.ModifierStackStrength,
                        reward.ModifierInteractionCost,
                        reward.ModifierMaxStacks))
                    continue;
                if (reward.Type == RewardType.Modifier
                    && reward.ModifierId
                        == BattleModifier.HomingMissile
                    && CurrentMissileFamily
                        == MissileFamily.Homing)
                    continue;
                if (reward.Type == RewardType.MissileFamily
                    && reward.MissileFamily
                        == CurrentMissileFamily)
                    continue;
                if (reward.Type == RewardType.OptionFormation
                    && reward.OptionFormation
                        == CurrentOptionFormation)
                    continue;
                if (reward.Type == RewardType.PrimaryWeaponFamily
                    && reward.PrimaryWeaponFamily
                        == CurrentPrimaryWeaponFamily)
                    continue;

                _rewardPool[eligibleCount] = reward;
                _rewardPoolCatalogIndices[eligibleCount] = i;
                _rewardWeights[eligibleCount] = reward.Weight;
                eligibleCount++;
            }

            _rewardRng.ResetForked(
                _runSeed,
                RewardSelectionStream,
                GetRewardSequence());
            for (int i = 0; i < _rewardSelectionRound; i++)
                _rewardRng.NextULong();
            int poolCount = eligibleCount;
            int catalogOptionCount = Math.Min(
                eligibleCount,
                optionCount);
            int optionStart = 0;
            if (catalogOptionCount > 0
                && (StagePlan.EncounterType == EncounterType.Elite
                    || StagePlan.EncounterType == EncounterType.Rare))
            {
                int modifierWeight = 0;
                for (int i = 0; i < poolCount; i++)
                {
                    if (_rewardPool[i].Type == RewardType.Modifier)
                        modifierWeight += _rewardWeights[i];
                }

                if (modifierWeight > 0)
                {
                    int roll = _rewardRng.NextInt(0, modifierWeight);
                    int modifierPick = -1;
                    for (int i = 0; i < poolCount; i++)
                    {
                        if (_rewardPool[i].Type != RewardType.Modifier)
                            continue;
                        if (roll < _rewardWeights[i])
                        {
                            modifierPick = i;
                            break;
                        }
                        roll -= _rewardWeights[i];
                    }

                    RewardDefinition modifier =
                        _rewardPool[modifierPick];
                    _rewardOptionCatalogIndices[0] =
                        _rewardPoolCatalogIndices[modifierPick];
                    _rewardOptionBuffer[0] = new RewardOption(
                        modifier.Id,
                        modifier.Type,
                        modifier.Slot,
                        modifier.Amount,
                        modifier.ModifierId,
                        modifier.MissileFamily,
                        modifier.OptionFormation,
                        modifier.PrimaryWeaponFamily,
                        modifier.ModifierKey,
                        modifier.Costs,
                        modifier.Gains,
                        modifier.CostViews);
                    int last = --poolCount;
                    _rewardPool[modifierPick] = _rewardPool[last];
                    _rewardPoolCatalogIndices[modifierPick] =
                        _rewardPoolCatalogIndices[last];
                    _rewardWeights[modifierPick] =
                        _rewardWeights[last];
                    optionStart = 1;
                }
            }

            for (int i = optionStart;
                i < catalogOptionCount;
                i++)
            {
                int pick = _rewardRng.PickWeighted(_rewardWeights, poolCount);
                RewardDefinition selected = _rewardPool[pick];
                _rewardOptionCatalogIndices[i] =
                    _rewardPoolCatalogIndices[pick];
                _rewardOptionBuffer[i] = new RewardOption(
                    selected.Id,
                    selected.Type,
                    selected.Slot,
                    selected.Amount,
                    selected.ModifierId,
                    selected.MissileFamily,
                    selected.OptionFormation,
                    selected.PrimaryWeaponFamily,
                    selected.ModifierKey,
                    selected.Costs,
                    selected.Gains,
                    selected.CostViews);

                int last = --poolCount;
                _rewardPool[pick] = _rewardPool[last];
                _rewardPoolCatalogIndices[pick] =
                    _rewardPoolCatalogIndices[last];
                _rewardWeights[pick] = _rewardWeights[last];
            }
            for (int i = catalogOptionCount; i < optionCount; i++)
            {
                int fallbackIndex =
                    (i - catalogOptionCount)
                    % FallbackRewardOptions.Length;
                _rewardOptionCatalogIndices[i] = -1;
                _rewardOptionBuffer[i] =
                    FallbackRewardOptions[fallbackIndex];
            }
            _rewardOptionView.SetCount(optionCount);
            return _rewardOptionView;
        }

        IReadOnlyList<RouteOption> GenerateRouteOptions(
            int targetBiomeIndex,
            int targetRoomIndex)
        {
            if (!(_stageGenerator is IRouteStageGenerator routeGenerator)
                || routeGenerator.ThemeIds.Count == 0)
                return Array.Empty<RouteOption>();
            if (targetBiomeIndex < 1
                || targetBiomeIndex > BiomeCount
                || targetRoomIndex < 1
                || targetRoomIndex > RoomsPerBiome)
                return Array.Empty<RouteOption>();

            int targetDifficulty =
                _difficultyCurve.GetDifficulty(targetBiomeIndex);
            string themeId = GetBiomeThemeId(targetBiomeIndex);

            _routeRng.ResetForked(
                _runSeed,
                RouteSelectionStream,
                GetRoomSequence(targetBiomeIndex, targetRoomIndex));
            int desiredCount = MaximumRouteOptionCount;
            if (desiredCount > MinimumRouteOptionCount)
                desiredCount = _routeRng.NextInt(2, desiredCount + 1);
            bool includeRare =
                _battleConfig.RareEncounterChanceNumerator > 0
                && _routeRng.NextInt(
                    0,
                    _battleConfig.RareEncounterChanceDenominator)
                    < _battleConfig.RareEncounterChanceNumerator;
            int rareSlot = includeRare
                ? _routeRng.NextInt(0, desiredCount)
                : -1;

            var options = new RouteOption[desiredCount];
            int optionCount = 0;
            if (rareSlot == 0
                && CanGenerateRouteForRoom(
                    routeGenerator,
                    themeId,
                    targetBiomeIndex,
                    targetDifficulty,
                    EncounterType.Rare,
                    targetRoomIndex))
            {
                options[optionCount++] =
                    new RouteOption(themeId, EncounterType.Rare);
            }

            const int commonEncounterCount = 4;
            int encounterStart =
                _routeRng.NextInt(0, commonEncounterCount);
            for (int encounterOffset = 0;
                encounterOffset < commonEncounterCount
                    && optionCount < desiredCount;
                encounterOffset++)
            {
                var encounterType = (EncounterType)(
                    (encounterStart + encounterOffset)
                    % commonEncounterCount);
                if (!CanGenerateRouteForRoom(
                        routeGenerator,
                        themeId,
                        targetBiomeIndex,
                        targetDifficulty,
                        encounterType,
                        targetRoomIndex))
                    continue;
                options[optionCount++] =
                    new RouteOption(themeId, encounterType);
                if (optionCount == rareSlot
                    && CanGenerateRouteForRoom(
                        routeGenerator,
                        themeId,
                        targetBiomeIndex,
                        targetDifficulty,
                        EncounterType.Rare,
                        targetRoomIndex))
                {
                    options[optionCount++] =
                        new RouteOption(themeId, EncounterType.Rare);
                }
            }

            if (optionCount < MinimumRouteOptionCount)
                return Array.Empty<RouteOption>();
            if (optionCount == options.Length)
                return Array.AsReadOnly(options);

            var trimmed = new RouteOption[optionCount];
            Array.Copy(options, trimmed, optionCount);
            return Array.AsReadOnly(trimmed);
        }

        EncounterType SelectSectionEncounterType(
            int biomeIndex,
            int roomIndex)
        {
            _routeRng.ResetForked(
                _runSeed,
                RouteSelectionStream,
                GetRoomSequence(biomeIndex, roomIndex));
            if (_battleConfig.RareEncounterChanceNumerator > 0
                && _routeRng.NextInt(
                    0,
                    _battleConfig.RareEncounterChanceDenominator)
                    < _battleConfig.RareEncounterChanceNumerator)
                return EncounterType.Rare;
            const int commonEncounterCount = 4;
            return (EncounterType)_routeRng.NextInt(
                0,
                commonEncounterCount);
        }

        bool CanGenerateRouteForRoom(
            IRouteStageGenerator generator,
            string themeId,
            int stageIndex,
            int difficulty,
            EncounterType encounterType,
            int roomIndex)
        {
            if (generator is ISectionRouteStageGenerator sectionGenerator)
            {
                return sectionGenerator.CanGenerateRouteForSection(
                    themeId,
                    stageIndex,
                    difficulty,
                    encounterType,
                    GetRouteSection(roomIndex));
            }
            return generator.CanGenerateRoute(
                themeId,
                stageIndex,
                difficulty,
                encounterType);
        }

        StagePlan GenerateRouteForRoom(
            IRouteStageGenerator generator,
            ulong seed,
            int stageIndex,
            int difficulty,
            string themeId,
            EncounterType encounterType,
            int roomIndex)
        {
            if (generator is ISectionRouteStageGenerator sectionGenerator)
            {
                return sectionGenerator.GenerateRouteForSection(
                    seed,
                    stageIndex,
                    difficulty,
                    themeId,
                    encounterType,
                    GetRouteSection(roomIndex));
            }
            return generator.GenerateRoute(
                seed,
                stageIndex,
                difficulty,
                themeId,
                encounterType);
        }

        StageRouteSection GetRouteSection(int roomIndex)
        {
            bool isMidBossRoom =
                RoomsPerBiome
                    >= RunProgressionConfig.DefaultRoomsPerBiome
                && roomIndex == 2;
            return roomIndex > 1 && !isMidBossRoom
                ? StageRouteSection.Closing
                : StageRouteSection.Default;
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
            var nextGauge = PowerUpGauge.CreateEmptyWithSameRules();
            nextGauge.ImportLevels(carriedLevels);

            _runSeed = newRunSeed;
            ResetStageThemeOrder(newRunSeed);
            RunNumber++;
            BiomeIndex = 1;
            RoomIndex = 1;
            IsBiomeBoss = false;
            IsHiddenBiome = false;
            State = RunState.Playing;
            CompletionGrade = RunCompletionGrade.None;
            SelectedColossalBoss = ColossalBossKind.None;
            _lastColossalBossAtRunStart =
                _metaState == null
                    ? ColossalBossKind.None
                    : _metaState.LastColossalBoss;
            EliteRoomsCleared = 0;
            NoHitBiomesCleared = 0;
            RareEncountersCleared = 0;
            _currentBiomeHit = false;
            _rewardOptions = Array.Empty<RewardOption>();
            _routeOptions = Array.Empty<RouteOption>();
            _preparedRouteOptions = Array.Empty<RouteOption>();
            _contractOptions =
                Array.Empty<ContractOption>();
            _routeChoiceHistory.Clear();
            _contractChoiceHistory.Clear();
            _rewardDecisionHistory.Clear();
            ActiveContract = null;
            ResetContractBattleConfig();
            _completedStageScore = 0;
            _completedShotsFired = 0;
            _completedShotsHit = 0;
            _completedKills = 0;
            _completedCapsulesCollected = 0;
            _completedGrazeCount = 0;
            _stagesCleared = 0;
            _roomsCleared = 0;
            _pendingBattleContinuity = null;
            _capsuleBalance = 0;
            Array.Clear(
                _rewardAcquisitionCounts,
                0,
                _rewardAcquisitionCounts.Length);
            _battleConfig.StartingShieldStock = _initialShieldStock;
            _battleConfig.StartingBombStock = _initialBombStock;
            _battleConfig.MaxShieldStock = _initialMaxShieldStock;
            _battleConfig.MaxBombStock = _initialMaxBombStock;
            _battleConfig.CapsuleDropWeightReduction =
                _initialCapsuleDropWeightReduction;
            _battleConfig.FireIntervalTicks = _initialFireIntervalTicks;
            _battleConfig.MainShotBaseDamage = _initialMainShotBaseDamage;
            ApplyPrimaryWeaponFamilyProfile(
                CurrentPrimaryWeaponDefinition
                ?? throw new InvalidOperationException(
                    "The current primary weapon family is unavailable."));
            _battleConfig.PlayerSpeedNumerator = _initialPlayerSpeedNumerator;
            _battleConfig.PlayerSpeedDenominator = _initialPlayerSpeedDenominator;
            PowerUpGauge = nextGauge;
            ResetShieldStockForNewRun();
            BuildCurrentStage();
        }

        void AdvanceRoom()
        {
            if (RoomIndex >= RoomsPerBiome)
                throw new InvalidOperationException(
                    "The regular-room counter is already at the biome boundary.");
            CaptureBattleContinuity();
            AccumulateCompletedBattle();
            RoomIndex++;
            IsBiomeBoss = false;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        void AdvanceHiddenRoom()
        {
            if (!IsHiddenBiome
                || RoomIndex
                    >= RunProgressionConfig.HiddenRooms)
                throw new InvalidOperationException(
                    "The hidden-room counter is already at its boundary.");
            CaptureBattleContinuity();
            AccumulateCompletedBattle();
            RoomIndex++;
            IsBiomeBoss = false;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        void AdvanceToBiomeBoss()
        {
            CaptureBattleContinuity();
            AccumulateCompletedBattle();
            IsBiomeBoss = true;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        void AdvanceBiome()
        {
            if (BiomeIndex >= BiomeCount)
                throw new InvalidOperationException(
                    "The biome counter is already at the campaign boundary.");
            _pendingBattleContinuity = null;
            AccumulateCompletedBattle();
            BiomeIndex++;
            RoomIndex = 1;
            IsBiomeBoss = false;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        void CaptureBattleContinuity()
        {
            if (!(Battle is BattleSim battle))
                throw new InvalidOperationException(
                    "Room continuity requires BattleSim.");
            _pendingBattleContinuity =
                battle.CaptureContinuityState();
        }

        void AccumulateCompletedBattle()
        {
            _battleConfig.StartingShieldStock = Battle.ShieldStock;
            _battleConfig.StartingBombStock = Battle.BombStock;
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
            _completedGrazeCount = AddSaturated(
                _completedGrazeCount,
                battle.GrazeCount);
        }

        void IncrementStagesCleared()
        {
            if (_stagesCleared < int.MaxValue)
                _stagesCleared++;
        }

        void IncrementRoomsCleared()
        {
            if (_roomsCleared < int.MaxValue)
                _roomsCleared++;
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

        static int SaturatingMultiply(int value, int multiplier)
        {
            long result = (long)value * multiplier;
            return result >= int.MaxValue
                ? int.MaxValue
                : (int)result;
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

        static void RemoveMoveSpeed(
            BattleSimConfig config,
            int amount)
        {
            long reductionNumerator =
                (long)amount * SimSpace.SubUnitsPerWorldUnit;
            long reductionDenominator = SimSpace.TicksPerSecond;
            long denominatorDivisor = GreatestCommonDivisor(
                config.PlayerSpeedDenominator,
                reductionDenominator);
            long leftScale =
                reductionDenominator / denominatorDivisor;
            long rightScale =
                config.PlayerSpeedDenominator / denominatorDivisor;
            long numerator =
                (long)config.PlayerSpeedNumerator * leftScale
                - reductionNumerator * rightScale;
            long denominator =
                (long)config.PlayerSpeedDenominator * leftScale;
            if (numerator < 1)
            {
                config.PlayerSpeedNumerator = 1;
                config.PlayerSpeedDenominator = 1;
                return;
            }
            long divisor = GreatestCommonDivisor(
                numerator,
                denominator);
            numerator /= divisor;
            denominator /= divisor;
            config.PlayerSpeedNumerator =
                numerator > int.MaxValue
                    ? int.MaxValue
                    : (int)numerator;
            config.PlayerSpeedDenominator =
                denominator > int.MaxValue
                    ? int.MaxValue
                    : (int)denominator;
        }

        void ResetContractBattleConfig()
        {
            _battleConfig.ContractCapsuleDropMultiplierNumerator = 1;
            _battleConfig.ContractCapsuleDropMultiplierDenominator = 1;
            _battleConfig.ContractBombDropMultiplierNumerator = 1;
            _battleConfig.ContractBombDropMultiplierDenominator = 1;
            _battleConfig.ContractGuaranteesBombDrop = false;
            _battleConfig.ContractScoreMultiplierNumerator = 1;
            _battleConfig.ContractScoreMultiplierDenominator = 1;
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

        static void ValidateSuspendData(
            RunSuspendData data,
            PowerUpGauge gauge,
            RewardCatalog rewards,
            ShipDefinition ship)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.schemaVersion
                != RunSuspendData.CurrentSchemaVersion)
            {
                throw new ArgumentException(
                    $"Unsupported suspend schema version "
                    + $"{data.schemaVersion}.",
                    nameof(data));
            }
            if (!Enum.IsDefined(
                    typeof(MissileFamily),
                    data.missileFamily)
                || !Enum.IsDefined(
                    typeof(OptionFormation),
                    data.optionFormation)
                || (data.primaryWeaponFamily != -1
                    && !Enum.IsDefined(
                        typeof(PrimaryWeaponFamily),
                        data.primaryWeaponFamily)))
                throw new ArgumentException(
                    "Suspend weapon loadout is invalid.",
                    nameof(data));
            ResolveSuspendDifficulty(
                data,
                out _,
                out _);
            if (data.runNumber < 1)
                throw new ArgumentException(
                    "Suspend runNumber must be positive.",
                    nameof(data));
            if (data.stageIndex < 1)
                throw new ArgumentException(
                    "Suspend stageIndex must be positive.",
                    nameof(data));
            bool hiddenBiomePosition =
                data.isHiddenBiome
                && (data.biomeIndex == data.biomeCount
                    // Accept REQ-035 payloads written before REQ-052.
                    || data.biomeIndex == data.biomeCount + 1);
            if (data.biomeCount < 1
                || data.finalStageIndex != data.biomeCount
                || data.biomeIndex < 1
                || (data.biomeIndex > data.biomeCount
                    && !hiddenBiomePosition)
                || data.stageIndex != data.biomeIndex)
            {
                throw new ArgumentException(
                    "Suspend biome progression is invalid.",
                    nameof(data));
            }
            int roomLimit = hiddenBiomePosition
                ? RunProgressionConfig.HiddenRooms
                : data.roomsPerBiome;
            if (data.roomsPerBiome < 1
                || data.roomIndex < 1
                || data.roomIndex > roomLimit)
                throw new ArgumentException(
                    "Suspend room progression is invalid.",
                    nameof(data));
            int expectedBiomesCleared = hiddenBiomePosition
                ? data.biomeCount
                : data.biomeIndex - 1;
            if (data.stagesCleared != expectedBiomesCleared)
                throw new ArgumentException(
                    "Suspend biomesCleared must match the biome boundary.",
                    nameof(data));
            long expectedRoomsCleared = hiddenBiomePosition
                ? (long)data.biomeCount * data.roomsPerBiome
                    + (data.isBiomeBoss
                        ? RunProgressionConfig.HiddenRooms
                        : data.roomIndex - 1)
                : (long)(data.biomeIndex - 1)
                    * data.roomsPerBiome
                    + (data.isBiomeBoss
                        ? data.roomsPerBiome
                        : data.roomIndex - 1);
            if (expectedRoomsCleared > int.MaxValue
                || data.roomsCleared != (int)expectedRoomsCleared)
                throw new ArgumentException(
                    "Suspend roomsCleared must match the room boundary.",
                    nameof(data));
            if (data.schemaVersion >= 3 && data.routeChoices == null)
                throw new ArgumentException(
                    "Suspend routeChoices cannot be null.",
                    nameof(data));
            if (data.routeChoices != null)
            {
                int previousRouteBiome = 0;
                int previousRouteRoom = 0;
                for (int i = 0; i < data.routeChoices.Length; i++)
                {
                    RouteChoiceData choice = data.routeChoices[i];
                    bool isPendingNextBiomeChoice =
                        data.isBiomeBoss
                        && data.biomeIndex < data.biomeCount
                        && choice != null
                        && choice.biomeIndex == data.biomeIndex + 1
                        && choice.roomIndex == 1;
                    if (choice == null
                        || choice.biomeIndex < 1
                        || choice.biomeIndex > data.biomeCount
                        || (choice.biomeIndex > data.biomeIndex
                            && !isPendingNextBiomeChoice)
                        || choice.roomIndex < 1
                        || choice.roomIndex > data.roomsPerBiome
                        || choice.stageIndex != choice.biomeIndex
                        || choice.biomeIndex < previousRouteBiome
                        || (choice.biomeIndex == previousRouteBiome
                            && choice.roomIndex <= previousRouteRoom)
                        || choice.optionIndex < 0
                        || choice.optionIndex >= MaximumRouteOptionCount
                        || string.IsNullOrEmpty(choice.themeId)
                        || !Enum.IsDefined(
                            typeof(EncounterType),
                            choice.encounterType))
                    {
                        throw new ArgumentException(
                            "Suspend route choice history is invalid.",
                            nameof(data));
                    }
                    previousRouteBiome = choice.biomeIndex;
                    previousRouteRoom = choice.roomIndex;
                }
            }
            if (data.eliteRoomsCleared < 0
                || data.noHitBiomesCleared < 0
                || data.rareEncountersCleared < 0
                || !Enum.IsDefined(
                    typeof(ColossalBossKind),
                    data.selectedColossalBoss)
                || !Enum.IsDefined(
                    typeof(ColossalBossKind),
                    data.lastColossalBossAtRunStart)
                || (hiddenBiomePosition
                    && data.selectedColossalBoss
                        == (int)ColossalBossKind.None)
                || (!hiddenBiomePosition
                    && data.selectedColossalBoss
                        != (int)ColossalBossKind.None))
            {
                throw new ArgumentException(
                    "Suspend hidden-biome progression is invalid.",
                    nameof(data));
            }
            if (data.score < 0
                || data.shotsFired < 0
                || data.shotsHit < 0
                || data.kills < 0
                || data.capsulesCollected < 0
                || data.grazeCount < 0)
            {
                throw new ArgumentException(
                    "Suspend score and statistics cannot be negative.",
                    nameof(data));
            }
            if (data.capsuleBalance < 0)
                throw new ArgumentException(
                    "Suspend capsule balance cannot be negative.",
                    nameof(data));
            if (!string.Equals(
                data.shipId,
                ship.Id,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Suspend shipId does not match the supplied ship.",
                    nameof(data));
            }
            if (data.powerUpLevels == null
                || data.powerUpLevels.Length
                    != PowerUpGauge.SlotCount)
            {
                throw new ArgumentException(
                    $"Suspend powerUpLevels must have exactly "
                    + $"{PowerUpGauge.SlotCount} entries.",
                    nameof(data));
            }
            if (data.powerUpCursor < PowerUpGauge.NoSelection
                || data.powerUpCursor >= gauge.GaugeSlotCount)
            {
                throw new ArgumentException(
                    "Suspend powerUpCursor is outside its valid range.",
                    nameof(data));
            }
            if (data.powerUpProgress == null
                || data.powerUpProgress.Length
                    != PowerUpGauge.SlotCount)
            {
                throw new ArgumentException(
                    $"Suspend powerUpProgress must have exactly "
                    + $"{PowerUpGauge.SlotCount} entries.",
                    nameof(data));
            }

            int[] shipStartingLevels =
                ship.ExportStartingPowerUpLevels();
            for (int i = 0; i < PowerUpGauge.SlotCount; i++)
            {
                int level = data.powerUpLevels[i];
                int maximum =
                    gauge.GetMaxLevel((PowerUpSlot)i);
                if (level < shipStartingLevels[i]
                    || level > maximum)
                {
                    throw new ArgumentException(
                        $"Suspend power-up level {i} is outside "
                        + $"[{shipStartingLevels[i]}, {maximum}].",
                        nameof(data));
                }
                int progress = data.powerUpProgress[i];
                int required = gauge.GetRequiredCapsulesForLevel(
                    (PowerUpSlot)i,
                    level);
                if (progress < 0
                    || (required == 0 && progress != 0)
                    || (required > 0 && progress >= required))
                {
                    throw new ArgumentException(
                        $"Suspend power-up progress {i} is outside "
                        + "the current level cost.",
                        nameof(data));
                }
            }
            if (data.playerHp != 1)
                throw new ArgumentException(
                    "Suspend playerHp compatibility flag must be one.",
                    nameof(data));
            if (data.maxShieldStock
                    < BattleSimConfig.DefaultMaxShieldStock
                || data.maxShieldStock
                    > BattleSimConfig.MaximumShieldStock
                || data.shieldStock < 0
                || data.shieldStock > data.maxShieldStock
                || data.shieldRemaining != data.shieldStock)
                throw new ArgumentException(
                    "Suspend shield stock is outside its cap or its "
                    + "compatibility mirror does not match.",
                    nameof(data));
            if (data.maxBombStock < 1
                || data.bombStock < 0
                || data.bombStock > data.maxBombStock)
                throw new ArgumentException(
                    "Suspend bomb stock is outside its cap.",
                    nameof(data));
            if (data.hasStageStartContinuity
                && (data.stageStartMultiplierLevel < 0
                    || data.stageStartMultiplierLevel > 3
                    || data.stageStartComboGauge < 0
                    || data.stageStartTicksSinceLastKill < 0))
            {
                throw new ArgumentException(
                    "Suspend room-continuity state is invalid.",
                    nameof(data));
            }
            if (data.fireIntervalTicks < 0
                || data.mainShotBaseDamage < 0
                || data.playerSpeedNumerator < 0
                || data.playerSpeedDenominator < 1)
            {
                throw new ArgumentException(
                    "Suspend passive battle tuning is invalid.",
                    nameof(data));
            }
            BattleModifier modifiers =
                (BattleModifier)data.activeModifiers;
            if ((modifiers & ~BattleModifierRules.All) != 0)
                throw new ArgumentException(
                    "Suspend activeModifiers contains unknown flags.",
                    nameof(data));
            if (data.rewardAcquisitions == null)
                throw new ArgumentException(
                    "Suspend rewardAcquisitions cannot be null.",
                    nameof(data));

            int previousCatalogIndex = -1;
            long totalAcquisitions = 0;
            for (int i = 0; i < data.rewardAcquisitions.Length; i++)
            {
                RewardAcquisitionData acquisition =
                    data.rewardAcquisitions[i];
                if (acquisition == null
                    || string.IsNullOrEmpty(acquisition.rewardId)
                    || acquisition.count < 1)
                {
                    throw new ArgumentException(
                        "Suspend reward acquisition entries are invalid.",
                        nameof(data));
                }

                int catalogIndex =
                    FindRewardIndex(rewards, acquisition.rewardId);
                if (catalogIndex < 0)
                    throw new ArgumentException(
                        $"Suspend references unknown reward "
                        + $"'{acquisition.rewardId}'.",
                        nameof(data));
                if (catalogIndex <= previousCatalogIndex)
                    throw new ArgumentException(
                        "Suspend reward acquisitions must be unique "
                        + "and in catalog order.",
                        nameof(data));

                RewardDefinition reward = rewards.All[catalogIndex];
                if (reward.MaxPerRun.HasValue
                    && acquisition.count > reward.MaxPerRun.Value)
                {
                    throw new ArgumentException(
                        $"Suspend reward '{acquisition.rewardId}' "
                        + "exceeds maxPerRun.",
                        nameof(data));
                }
                previousCatalogIndex = catalogIndex;
                totalAcquisitions += acquisition.count;
                if (totalAcquisitions
                    > (long)data.roomsCleared + data.stagesCleared)
                    throw new ArgumentException(
                        "Suspend reward acquisitions exceed cleared "
                        + "reward boundaries.",
                        nameof(data));
            }
        }

        static void RestoreRewardAcquisitions(
            RewardAcquisitionData[] acquisitions,
            RewardCatalog rewards,
            int[] destination)
        {
            Array.Clear(destination, 0, destination.Length);
            for (int i = 0; i < acquisitions.Length; i++)
            {
                RewardAcquisitionData acquisition = acquisitions[i];
                int catalogIndex =
                    FindRewardIndex(rewards, acquisition.rewardId);
                destination[catalogIndex] = acquisition.count;
            }
        }

        void RebuildModifierStacksFromAcquisitions(
            BattleModifier expectedFlags)
        {
            var rebuilt = new BattleModifierStackSet(
                _rewards.MaxCombinedModifierCost);
            for (int i = 0; i < _rewards.All.Count; i++)
            {
                RewardDefinition reward = _rewards.All[i];
                if (reward.Type != RewardType.Modifier)
                    continue;
                int count = _rewardAcquisitionCounts[i];
                for (int stack = 0; stack < count; stack++)
                {
                    if (!rebuilt.TryAdd(
                            reward.ModifierId,
                            reward.ModifierStackStrength,
                            reward.ModifierInteractionCost,
                            reward.ModifierMaxStacks))
                    {
                        throw new ArgumentException(
                            $"Suspend modifier '{reward.ModifierKey}' "
                            + "exceeds its stack or combination cap.");
                    }
                }
            }
            BattleModifier missing =
                expectedFlags & ~rebuilt.ActiveModifiers;
            foreach (BattleModifier effect in BattleModifierRules.Ordered)
            {
                if ((missing & effect) == 0)
                    continue;
                if (!rebuilt.TryAdd(effect, 1, 1, 1))
                    throw new ArgumentException(
                        "Suspend legacy modifier flags exceed the "
                        + "combination cap.");
            }
            if (rebuilt.ActiveModifiers != expectedFlags)
            {
                throw new ArgumentException(
                    "Suspend activeModifiers does not match modifier "
                    + "reward acquisitions.");
            }
            ModifierStacks = rebuilt;
        }

        void RestoreRouteChoices(
            RouteChoiceData[] choices,
            bool requireCompleteHistory)
        {
            _routeChoiceHistory.Clear();
            if (choices == null)
                return;

            for (int i = 0; i < choices.Length; i++)
            {
                RouteChoiceData data = choices[i];
                if (data.roomIndex == 1)
                {
                    _routeChoiceHistory.Add(new RouteChoice(
                        data.biomeIndex,
                        data.roomIndex,
                        data.optionIndex,
                        data.themeId,
                        (EncounterType)data.encounterType));
                    continue;
                }
                IReadOnlyList<RouteOption> options =
                    GenerateRouteOptions(
                        data.biomeIndex,
                        data.roomIndex);
                if (data.optionIndex < 0
                    || data.optionIndex >= options.Count)
                {
                    throw new ArgumentException(
                        "Suspend route option index is invalid.",
                        "data");
                }

                RouteOption option = options[data.optionIndex];
                var encounterType =
                    (EncounterType)data.encounterType;
                if (!string.Equals(
                        data.themeId,
                        option.ThemeId,
                        StringComparison.Ordinal)
                    || encounterType != option.EncounterType)
                {
                    throw new ArgumentException(
                        "Suspend route choice does not match "
                        + "the deterministic options.",
                        "data");
                }

                _routeChoiceHistory.Add(new RouteChoice(
                    data.biomeIndex,
                    data.roomIndex,
                    data.optionIndex,
                    data.themeId,
                    encounterType));
            }

            if (!requireCompleteHistory)
                return;

            int historyIndex = 0;
            int historyBiomeCount =
                IsHiddenBiome ? BiomeCount : BiomeIndex;
            for (int biome = 1;
                biome <= historyBiomeCount;
                biome++)
            {
                int lastRoom =
                    IsHiddenBiome
                    || biome < BiomeIndex
                    || IsBiomeBoss
                    ? RoomsPerBiome
                    : RoomIndex;
                for (int room = 2; room <= lastRoom; room++)
                {
                    IReadOnlyList<RouteOption> options =
                        GenerateRouteOptions(biome, room);
                    if (options.Count < MinimumRouteOptionCount)
                        continue;
                    while (historyIndex < _routeChoiceHistory.Count
                        && _routeChoiceHistory[historyIndex].RoomIndex == 1)
                        historyIndex++;
                    if (historyIndex >= _routeChoiceHistory.Count
                        || _routeChoiceHistory[historyIndex].BiomeIndex
                            != biome
                        || _routeChoiceHistory[historyIndex].RoomIndex
                            != room)
                    {
                        throw new ArgumentException(
                            "Suspend route choice history is incomplete.",
                            "data");
                    }
                    historyIndex++;
                }
            }
            while (historyIndex < _routeChoiceHistory.Count
                && _routeChoiceHistory[historyIndex].RoomIndex == 1)
                historyIndex++;
            if (historyIndex != _routeChoiceHistory.Count)
            {
                throw new ArgumentException(
                    "Suspend route choice history contains "
                    + "unexpected stages.",
                    "data");
            }
        }

        void RestoreContractState(
            string activeContractId,
            ContractChoiceData[] choices,
            ContractCatalog catalog)
        {
            _contractChoiceHistory.Clear();
            ContractChoiceData[] source =
                choices ?? Array.Empty<ContractChoiceData>();
            int previousBiome = 1;
            for (int i = 0; i < source.Length; i++)
            {
                ContractChoiceData data = source[i];
                ContractDefinition definition =
                    data == null
                        ? null
                        : FindContractIncludingTerminal(
                            catalog,
                            data.contractId);
                if (data == null
                    || data.targetBiomeIndex <= previousBiome
                    || data.targetBiomeIndex > BiomeCount + 1
                    || definition == null
                    || !Enum.IsDefined(
                        typeof(ContractDestinationKind),
                        data.destinationKind)
                    || definition.DestinationKind
                        != (ContractDestinationKind)
                            data.destinationKind)
                    throw new ArgumentException(
                        "Suspend contract choice history is invalid.");
                _contractChoiceHistory.Add(
                    new ContractChoice(
                        data.targetBiomeIndex,
                        data.optionIndex,
                        data.contractId,
                        (ContractDestinationKind)
                            data.destinationKind,
                        data.destinationThemeId,
                        data.destinationThemeStageIndex));
                if (definition.DestinationKind
                    == ContractDestinationKind.NextStage)
                    RestoreContractDestination(data);
                previousBiome = data.targetBiomeIndex;
            }

            ActiveContract =
                FindContractIncludingTerminal(
                    catalog,
                    activeContractId);
            if (BiomeIndex == 1)
            {
                if (ActiveContract != null
                    || _contractChoiceHistory.Count != 0)
                    throw new ArgumentException(
                        "Biome one cannot have an active contract.");
                ResetContractBattleConfig();
                return;
            }
            if (ActiveContract == null
                && _contractChoiceHistory.Count == 0)
            {
                ActiveContract = catalog.Standard;
                for (int biome = 2;
                    biome <= BiomeIndex;
                    biome++)
                    _contractChoiceHistory.Add(
                        new ContractChoice(
                            biome,
                            0,
                            catalog.Standard.Id,
                            ContractDestinationKind.NextStage,
                            GetDestinationThemeId(
                                _stageThemeOrder[biome - 1],
                                biome),
                            _stageThemeOrder[biome - 1]));
            }
            if (ActiveContract == null
                || _contractChoiceHistory.Count == 0)
                throw new ArgumentException(
                    "Suspend active contract is missing.");
            ContractChoice latest = _contractChoiceHistory[
                _contractChoiceHistory.Count - 1];
            int expectedTargetBiome =
                IsHiddenBiome
                    ? BiomeCount + 1
                    : BiomeIndex;
            if (latest.TargetBiomeIndex != expectedTargetBiome
                || !string.Equals(
                    latest.ContractId,
                    ActiveContract.Id,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Suspend active contract does not match its history.");
            _battleConfig.ContractCapsuleDropMultiplierNumerator =
                ActiveContract.CapsuleDropNumerator;
            _battleConfig.ContractCapsuleDropMultiplierDenominator =
                ActiveContract.CapsuleDropDenominator;
            _battleConfig.ContractBombDropMultiplierNumerator =
                ActiveContract.BombDropNumerator;
            _battleConfig.ContractBombDropMultiplierDenominator =
                ActiveContract.BombDropDenominator;
            _battleConfig.ContractGuaranteesBombDrop =
                ActiveContract.GuaranteedBombDrop;
            _battleConfig.ContractScoreMultiplierNumerator =
                ActiveContract.ScoreMultiplierNumerator;
            _battleConfig.ContractScoreMultiplierDenominator =
                ActiveContract.ScoreMultiplierDenominator;
        }

        void RestoreContractDestination(ContractChoiceData data)
        {
            int targetPosition = data.targetBiomeIndex - 1;
            int themeStageIndex =
                data.destinationThemeStageIndex;
            if (themeStageIndex == 0
                && data.destinationThemeId == null)
            {
                // Pre-REQ-086 payloads used the already shuffled next theme.
                themeStageIndex = _stageThemeOrder[targetPosition];
            }
            string themeId = GetDestinationThemeId(
                themeStageIndex,
                data.targetBiomeIndex);
            if (data.destinationThemeId != null
                && !string.Equals(
                    data.destinationThemeId,
                    themeId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Suspend contract destination theme is invalid.");
            ApplyContractDestination(
                new ContractOption(
                    FindContractIncludingTerminal(
                        _contracts,
                        data.contractId),
                    themeId,
                    themeStageIndex),
                data.targetBiomeIndex);
        }

        void RestoreRewardDecisionHistory(
            RewardDecisionData[] decisions)
        {
            _rewardDecisionHistory.Clear();
            RewardDecisionData[] source =
                decisions
                ?? Array.Empty<RewardDecisionData>();
            int previousSequence = 0;
            bool selected = false;
            for (int i = 0; i < source.Length; i++)
            {
                RewardDecisionData data = source[i];
                if (data == null
                    || data.rewardSequence < previousSequence)
                    throw new ArgumentException(
                        "Suspend reward decision history is invalid.");
                if (data.rewardSequence != previousSequence)
                {
                    previousSequence =
                        data.rewardSequence;
                    selected = false;
                }
                var decision = new RewardDecision(
                    data.rewardSequence,
                    (RewardSelectionKind)
                        data.selectionKind,
                    (RewardDecisionKind)
                        data.decisionKind,
                    data.optionIndex);
                if (selected)
                    throw new ArgumentException(
                        "Suspend reward decision history occurs after selection.");
                if (decision.DecisionKind
                    == RewardDecisionKind.Select)
                    selected = true;
                _rewardDecisionHistory.Add(decision);
            }
        }

        static ContractDefinition FindContractIncludingTerminal(
            ContractCatalog catalog,
            string id)
        {
            ContractDefinition definition = catalog.Find(id);
            if (definition != null || id == null)
                return definition;
            if (string.Equals(
                    id,
                    BuiltInEndRunContract.Id,
                    StringComparison.Ordinal))
                return BuiltInEndRunContract;
            if (string.Equals(
                    id,
                    BuiltInUnchartedContract.Id,
                    StringComparison.Ordinal))
                return BuiltInUnchartedContract;
            return null;
        }

        static int FindRewardIndex(
            RewardCatalog rewards,
            string rewardId)
        {
            for (int i = 0; i < rewards.All.Count; i++)
            {
                if (string.Equals(
                    rewards.All[i].Id,
                    rewardId,
                    StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        static void ResolveSuspendDifficulty(
            RunSuspendData data,
            out int numerator,
            out int denominator)
        {
            if (data.schemaVersion == 1)
            {
                numerator = 1;
                denominator = 1;
                return;
            }
            if (data.difficultyMultiplierNumerator < 1
                || data.difficultyMultiplierDenominator < 1)
            {
                throw new ArgumentException(
                    "Suspend difficulty multiplier must be positive.",
                    nameof(data));
            }

            int divisor = GreatestCommonDivisor(
                data.difficultyMultiplierNumerator,
                data.difficultyMultiplierDenominator);
            numerator =
                data.difficultyMultiplierNumerator / divisor;
            denominator =
                data.difficultyMultiplierDenominator / divisor;
        }

        void ApplyCurrentLoadoutProfiles()
        {
            MissileFamilyDefinition missile =
                _battleContent.FindMissileFamily(
                    CurrentMissileFamily);
            if (missile == null)
                throw new InvalidOperationException(
                    $"Missile family '{CurrentMissileFamily}' "
                    + "is not present in BattleContent.");
            _battleConfig.MissileFamily = missile.Family;
            _battleConfig.MissileBaseDamage = missile.BaseDamage;
            _battleConfig.MissileDamageGrowthPercentPerLevel =
                missile.DamageGrowthPercentPerLevel;
            _battleConfig.MissileFireIntervalTicks =
                missile.FireIntervalTicks;
            _battleConfig.MissileMinimumFireIntervalTicks =
                missile.MinimumFireIntervalTicks;
            _battleConfig.MissileFireIntervalReductionPerLevel =
                missile.FireIntervalReductionPerLevel;
            _battleConfig.MissileSpeedXNumerator =
                missile.SpeedXNumerator;
            _battleConfig.MissileSpeedXDenominator =
                missile.SpeedXDenominator;
            _battleConfig.MissileFallSpeedYNumerator =
                missile.FallSpeedYNumerator;
            _battleConfig.MissileFallSpeedYDenominator =
                missile.FallSpeedYDenominator;
            _battleConfig.MissilePierceEnemyCount =
                missile.PierceEnemyCount;
            _battleConfig.MissileExplosionDamage =
                missile.ExplosionDamage;
            _battleConfig.MissileExplosionRadiusSubUnits =
                missile.ExplosionRadiusSubUnits;
            _battleConfig.MissileExplosionMaxTargets =
                missile.ExplosionMaxTargets;
            _battleConfig.MissileDropDelayTicks =
                missile.DropDelayTicks;
            _battleConfig.HomingMissileTurnLutSlotsPerTick =
                missile.HomingTurnLutSlotsPerTick;

            OptionFormationDefinition option =
                _battleContent.FindOptionFormation(
                    CurrentOptionFormation);
            if (option == null)
                throw new InvalidOperationException(
                    $"Option formation '{CurrentOptionFormation}' "
                    + "is not present in BattleContent.");
            _battleConfig.OptionFormation = option.Formation;
            _battleConfig.OptionFollowDelayTicks =
                option.FollowDelayTicks;
            _battleConfig.OptionFixedOffsetXs =
                CopyIntegers(option.OffsetXs);
            _battleConfig.OptionFixedOffsetYs =
                CopyIntegers(option.OffsetYs);
            _battleConfig.OptionOrbitRadiusSubUnits =
                option.OrbitRadiusSubUnits;
            _battleConfig.OptionOrbitAngularLutSlotsNumerator =
                option.AngularLutSlotsNumerator;
            _battleConfig.OptionOrbitAngularLutSlotsDenominator =
                option.AngularLutSlotsDenominator;
        }

        void SwitchPrimaryWeaponFamily(
            PrimaryWeaponFamily family)
        {
            PrimaryWeaponFamilyDefinition current =
                _battleContent.FindPrimaryWeaponFamily(
                    CurrentPrimaryWeaponFamily);
            PrimaryWeaponFamilyDefinition next =
                _battleContent.FindPrimaryWeaponFamily(family);
            if (next == null)
                throw new InvalidOperationException(
                    $"Primary weapon family '{family}' "
                    + "is not present in BattleContent.");

            int damageBonus = current == null
                ? 0
                : Math.Max(
                    0,
                    _battleConfig.MainShotBaseDamage
                        - current.BaseDamage);
            int intervalReduction = current == null
                ? 0
                : Math.Max(
                    0,
                    current.FireIntervalTicks
                        - _battleConfig.FireIntervalTicks);
            CurrentPrimaryWeaponFamily = family;
            ApplyPrimaryWeaponFamilyProfile(next);
            _battleConfig.MainShotBaseDamage = SaturatingAdd(
                _battleConfig.MainShotBaseDamage,
                damageBonus);
            _battleConfig.FireIntervalTicks = Math.Max(
                _battleConfig.MainShotMinimumFireIntervalTicks,
                _battleConfig.FireIntervalTicks
                    - intervalReduction);
        }

        void ApplyPrimaryWeaponFamilyProfile(
            PrimaryWeaponFamilyDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            _battleConfig.PlayerWeaponType =
                definition.WeaponType;
            _battleConfig.PlayerWeaponFamily =
                definition.Family;
            _battleConfig.MainShotBaseDamage =
                definition.BaseDamage;
            _battleConfig.FireIntervalTicks =
                definition.FireIntervalTicks;
            _battleConfig.MainShotMinimumFireIntervalTicks =
                definition.MinimumFireIntervalTicks;
            _battleConfig.MainShotRapidFireStartLevel =
                definition.RapidFireStartLevel;
            _battleConfig.MainShotFireIntervalReductionPerLevel =
                definition.FireIntervalReductionPerLevel;
            _battleConfig.PlayerBulletSpeedNumerator =
                definition.SpeedNumerator;
            _battleConfig.PlayerBulletSpeedDenominator =
                definition.SpeedDenominator;
            _battleConfig.MainShotHalfWidth =
                definition.HalfWidth;
            _battleConfig.MainShotHalfHeight =
                definition.HalfHeight;
            _battleConfig.LaserPierceEnemyCount =
                definition.PierceEnemyCount;
            _battleConfig.SpreadWays =
                definition.SpreadWays;
            _battleConfig.SpreadStepLutSlots =
                definition.SpreadStepLutSlots;
            _battleConfig.MainShotAngleLutSlots =
                CopyIntegers(definition.ShotAngleLutSlots);
            _battleConfig.UseConfiguredMainShotStats = true;
        }

        static int[] CopyIntegers(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            return copy;
        }

        void BuildCurrentStage()
        {
            ApplyCurrentLoadoutProfiles();
            int generationBiomeIndex =
                IsHiddenBiome
                    ? BiomeCount
                    : _stageThemeOrder[BiomeIndex - 1];
            int battleSequenceBiomeIndex =
                IsHiddenBiome ? BiomeCount + 1 : BiomeIndex;
            Difficulty = _difficultyCurve.GetDifficulty(
                battleSequenceBiomeIndex);
            ulong generationSeed = GetRoomGenerationSeed(
                battleSequenceBiomeIndex,
                RoomIndex,
                IsBiomeBoss);
            StagePlan generated;
            if (IsHiddenBiome && IsBiomeBoss)
            {
                if (!(_stageGenerator
                        is IColossalBossStageGenerator colossal))
                    throw new InvalidOperationException(
                        "The hidden boss requires a colossal boss generator.");
                generated = colossal.GenerateColossalBoss(
                    generationSeed,
                    generationBiomeIndex,
                    Difficulty,
                    SelectedColossalBoss);
            }
            else if (_stageGenerator is IRouteStageGenerator routeGenerator)
            {
                StagePlan basePlan = _stageGenerator.Generate(
                    _runSeed,
                    battleSequenceBiomeIndex,
                    Difficulty);
                if (basePlan == null)
                    throw new InvalidOperationException(
                        "The stage generator returned no biome base plan.");
                if (generationBiomeIndex >= 1
                    && generationBiomeIndex
                        <= routeGenerator.ThemeIds.Count)
                {
                    string selectedThemeId =
                        routeGenerator.ThemeIds[
                            generationBiomeIndex - 1];
                    if (!string.Equals(
                            basePlan.ThemeId,
                            selectedThemeId,
                            StringComparison.Ordinal))
                    {
                        basePlan = routeGenerator.GenerateRoute(
                            _runSeed,
                            battleSequenceBiomeIndex,
                            Difficulty,
                            selectedThemeId,
                            basePlan.EncounterType);
                    }
                }
                if (TryGetRouteChoice(
                        battleSequenceBiomeIndex,
                        RoomIndex,
                        out RouteChoice routeChoice))
                {
                    generated = GenerateRouteForRoom(
                        routeGenerator,
                        generationSeed,
                        battleSequenceBiomeIndex,
                        Difficulty,
                        routeChoice.ThemeId,
                        IsBiomeBoss
                            ? EncounterType.Normal
                            : routeChoice.EncounterType,
                        IsBiomeBoss ? 1 : RoomIndex);
                }
                else if (!IsBiomeBoss
                    && RoomIndex > 1)
                {
                    EncounterType encounterType =
                        IsMidBossSection
                            ? EncounterType.Elite
                            : SelectSectionEncounterType(
                                battleSequenceBiomeIndex,
                                RoomIndex);
                    if (!CanGenerateRouteForRoom(
                            routeGenerator,
                            basePlan.ThemeId,
                            generationBiomeIndex,
                            Difficulty,
                            encounterType,
                            RoomIndex))
                        encounterType = EncounterType.Normal;
                    generated = GenerateRouteForRoom(
                        routeGenerator,
                        generationSeed,
                        battleSequenceBiomeIndex,
                        Difficulty,
                        basePlan.ThemeId,
                        encounterType,
                        RoomIndex);
                }
                else
                    generated = basePlan;
            }
            else
            {
                generated = _stageGenerator.Generate(
                    generationSeed,
                    generationBiomeIndex,
                    Difficulty);
            }
            if (generated == null)
                throw new InvalidOperationException(
                    "The stage generator returned no plan.");
            if (!IsHiddenBiome
                && generationBiomeIndex != BiomeIndex
                && _stageGenerator
                    is IRouteStageGenerator progressionGenerator
                && BiomeIndex <= progressionGenerator.ThemeIds.Count)
            {
                string progressionThemeId =
                    progressionGenerator.ThemeIds[BiomeIndex - 1];
                StagePlan progressionReference =
                    progressionGenerator.GenerateRoute(
                        _runSeed,
                        BiomeIndex,
                        Difficulty,
                        progressionThemeId,
                        EncounterType.Normal);
                generated = ApplyProgressionBossDifficulty(
                    generated,
                    progressionReference);
            }
            ResetEnemyHpScale();
            StagePlan = IsBiomeBoss
                ? CreateBiomeBossPlan(generated)
                : IsMidBossSection
                    ? CreateMidBossPlan(generated)
                    : CreateRegularRoomPlan(generated);
            StagePlan = ApplyContractToStagePlan(
                StagePlan,
                ActiveContract);
            _stageLengthTicks = GetStageLengthTicks(StagePlan);

            Rng battleRng = new Rng(_runSeed)
                .Fork(BattleSimulationStream)
                .Fork(battleSequenceBiomeIndex)
                .Fork(RoomIndex)
                .Fork(IsBiomeBoss ? 1 : 0);
            Battle = new BattleSim(
                _battleConfig,
                battleRng,
                StagePlan,
                _battleContent,
                PowerUpGauge,
                ModifierStacks,
                _pendingBattleContinuity);
            _pendingBattleContinuity = null;
            _preparedRouteOptions = Array.Empty<RouteOption>();
            CaptureStageStart();
        }

        void ResetEnemyHpScale()
        {
            _battleConfig.EnemyHpMultiplierNumerator =
                _difficultyMultiplierNumerator;
            _battleConfig.EnemyHpMultiplierDenominator =
                _difficultyMultiplierDenominator;
        }

        static StagePlan ApplyProgressionBossDifficulty(
            StagePlan source,
            StagePlan progressionReference)
        {
            var phases =
                new BossPhase[source.BossPhases.Count];
            for (int i = 0; i < phases.Length; i++)
            {
                BossPhase phase = source.BossPhases[i];
                BossPhase reference =
                    progressionReference.BossPhases.Count == 0
                        ? phase
                        : progressionReference.BossPhases[
                            Math.Min(
                                i,
                                progressionReference.BossPhases.Count - 1)];
                phases[i] = new BossPhase(
                    reference.FireIntervalTicks,
                    reference.Ways,
                    reference.BulletSpeedNumerator,
                    reference.BulletSpeedDenominator,
                    phase.MovementPattern,
                    phase.MovementAmplitudeNumerator,
                    phase.MovementAmplitudeDenominator,
                    phase.MovementPeriodTicks,
                    phase.PartVulnerability,
                    phase.DurationTicks,
                    phase.TelegraphTicks,
                    phase.FirePattern);
            }

            return new StagePlan(
                source.Segments,
                source.BossId,
                source.LaneCount,
                source.StartLaneMask,
                source.BossEntryLaneMask,
                progressionReference.BossMaxHp,
                source.BossHalfWidth,
                source.BossHalfHeight,
                source.BossHoldX,
                phases,
                source.ThemeId,
                source.RequestedThemeId,
                source.EncounterType,
                source.BossParts,
                source.Gimmick);
        }

        static StagePlan ApplyContractToStagePlan(
            StagePlan source,
            ContractDefinition contract)
        {
            if (contract == null || contract.IsNeutral)
                return source;
            var segments =
                new StageSegment[source.Segments.Count];
            for (int i = 0; i < segments.Length; i++)
            {
                StageSegment segment = source.Segments[i];
                int spawnCount = ScaleCount(
                    segment.Spawns.Count,
                    contract.EnemyDensityNumerator,
                    contract.EnemyDensityDenominator);
                var spawns = new SpawnEvent[spawnCount];
                for (int spawn = 0; spawn < spawnCount; spawn++)
                {
                    int sourceIndex =
                        spawn * segment.Spawns.Count
                        / Math.Max(1, spawnCount);
                    spawns[spawn] =
                        segment.Spawns[sourceIndex];
                }
                segments[i] = new StageSegment(
                    segment.SegmentId,
                    segment.LengthTicks,
                    spawns,
                    segment.EntryLaneMask,
                    segment.ExitLaneMask,
                    segment.TraversableLaneMasks,
                    segment.Obstacles,
                    segment.Environment);
            }

            StageGimmickDefinition gimmick = source.Gimmick;
            if (gimmick != StageGimmickDefinition.None
                && contract.GimmickIntensityNumerator
                    != contract.GimmickIntensityDenominator)
            {
                if (contract.GimmickIntensityNumerator == 0)
                {
                    gimmick = new StageGimmickDefinition(
                        gimmick.ThemeId,
                        false,
                        0);
                }
                else
                {
                    long scaled = (long)gimmick.TimeLimitTicks
                        * contract.GimmickIntensityDenominator
                        / contract.GimmickIntensityNumerator;
                    gimmick = new StageGimmickDefinition(
                        gimmick.ThemeId,
                        gimmick.VisionObscured,
                        scaled >= int.MaxValue
                            ? int.MaxValue
                            : (int)scaled);
                }
            }
            return new StagePlan(
                segments,
                source.BossId,
                source.LaneCount,
                source.StartLaneMask,
                source.BossEntryLaneMask,
                source.BossMaxHp,
                source.BossHalfWidth,
                source.BossHalfHeight,
                source.BossHoldX,
                source.BossPhases,
                source.ThemeId,
                source.RequestedThemeId,
                source.EncounterType,
                source.BossParts,
                gimmick);
        }

        static int ScaleCount(
            int count,
            int numerator,
            int denominator)
        {
            if (count == 0 || numerator == 0)
                return 0;
            long scaled = (long)count * numerator;
            scaled /= denominator;
            return scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled;
        }

        IReadOnlyList<RouteOption> GenerateExitRouteOptions()
        {
            if (IsBiomeBoss || IsHiddenBiome)
                return Array.Empty<RouteOption>();
            if (RoomIndex < RoomsPerBiome)
            {
                return GenerateRouteOptions(
                    BiomeIndex,
                    RoomIndex + 1);
            }
            if (BiomeIndex < BiomeCount)
                return GenerateRouteOptions(BiomeIndex + 1, 1);
            return Array.Empty<RouteOption>();
        }

        bool TryGetRouteChoice(
            int biomeIndex,
            int roomIndex,
            out RouteChoice routeChoice)
        {
            for (int i = _routeChoiceHistory.Count - 1; i >= 0; i--)
            {
                RouteChoice candidate = _routeChoiceHistory[i];
                if (candidate.BiomeIndex == biomeIndex
                    && candidate.RoomIndex == roomIndex)
                {
                    routeChoice = candidate;
                    return true;
                }
                if (candidate.BiomeIndex < biomeIndex
                    || (candidate.BiomeIndex == biomeIndex
                        && candidate.RoomIndex < roomIndex))
                    break;
            }
            routeChoice = default;
            return false;
        }

        string GetBiomeThemeId(int biomeIndex)
        {
            int themeStageIndex =
                _stageThemeOrder[biomeIndex - 1];
            if (_stageGenerator
                    is IRouteStageGenerator routeGenerator
                && themeStageIndex <= routeGenerator.ThemeIds.Count)
                return routeGenerator.ThemeIds[themeStageIndex - 1];
            StagePlan plan = _stageGenerator.Generate(
                _runSeed,
                themeStageIndex,
                _difficultyCurve.GetDifficulty(biomeIndex));
            if (plan == null)
                throw new InvalidOperationException(
                    "The stage generator returned no biome base plan.");
            return plan.ThemeId;
        }

        static int[] CreateStageThemeOrder(
            ulong runSeed,
            int biomeCount,
            IStageGenerator stageGenerator)
        {
            var order = new int[biomeCount];
            for (int i = 0; i < order.Length; i++)
                order[i] = i + 1;
            if (stageGenerator
                    is IRouteStageGenerator routeGenerator
                && routeGenerator.ThemeIds.Count >= biomeCount)
            {
                IReadOnlyList<string> themeOrder =
                    routeGenerator.GetThemeOrder(runSeed);
                if (themeOrder.Count >= biomeCount)
                {
                    for (int position = 0;
                        position < biomeCount;
                        position++)
                    {
                        int themeIndex = -1;
                        for (int candidate = 0;
                            candidate < routeGenerator.ThemeIds.Count;
                            candidate++)
                        {
                            if (string.Equals(
                                    themeOrder[position],
                                    routeGenerator.ThemeIds[candidate],
                                    StringComparison.Ordinal))
                            {
                                themeIndex = candidate;
                                break;
                            }
                        }
                        if (themeIndex < 0)
                            throw new InvalidOperationException(
                                "The route generator returned an unknown "
                                + $"theme '{themeOrder[position]}'.");
                        order[position] = themeIndex + 1;
                    }
                    return order;
                }
            }
            if (biomeCount < 5)
                return order;

            Rng rng = new Rng(runSeed).Fork(StageOrderStream);
            for (int i = 3; i > 1; i--)
            {
                int swap = rng.NextInt(1, i + 1);
                int value = order[i];
                order[i] = order[swap];
                order[swap] = value;
            }
            return order;
        }

        void ResetStageThemeOrder(ulong runSeed)
        {
            int[] order = CreateStageThemeOrder(
                runSeed,
                BiomeCount,
                _stageGenerator);
            Array.Copy(
                order,
                _stageThemeOrder,
                order.Length);
        }

        ulong GetRoomGenerationSeed(
            int biomeIndex,
            int roomIndex,
            bool isBiomeBoss)
        {
            if (roomIndex == 1 && !isBiomeBoss)
                return _runSeed;
            var rng = new Rng(_runSeed)
                .Fork(RoomGenerationStream)
                .Fork(biomeIndex)
                .Fork(roomIndex)
                .Fork(isBiomeBoss ? 1 : 0);
            return rng.NextULong();
        }

        int GetRoomSequence(int biomeIndex, int roomIndex)
        {
            long sequence =
                (long)(biomeIndex - 1) * RoomsPerBiome + roomIndex;
            if (sequence > int.MaxValue)
                throw new InvalidOperationException(
                    "The room sequence exceeds the supported range.");
            return (int)sequence;
        }

        int GetRewardSequence()
        {
            int roomSequence = GetRoomSequence(BiomeIndex, RoomIndex);
            if (!IsBiomeBoss)
                return roomSequence;
            long bossSequence =
                (long)BiomeCount * RoomsPerBiome + BiomeIndex;
            if (bossSequence > int.MaxValue)
                throw new InvalidOperationException(
                    "The reward sequence exceeds the supported range.");
            return (int)bossSequence;
        }

        StagePlan CreateMidBossPlan(StagePlan source)
        {
            var candidates = new List<EnemyDefinition>();
            for (int i = 0; i < _battleContent.Enemies.Count; i++)
            {
                EnemyDefinition enemy = _battleContent.Enemies[i];
                if (!enemy.Id.StartsWith(
                        "mini_",
                        StringComparison.Ordinal))
                    continue;
                MidBossProfile profile = enemy.MidBossProfile;
                if (profile != null
                    && (BiomeIndex < profile.StageIndexMin
                        || BiomeIndex > profile.StageIndexMax))
                    continue;
                candidates.Add(enemy);
            }
            candidates.Sort(
                (left, right) => string.CompareOrdinal(
                    left.Id,
                    right.Id));
            if (candidates.Count == 0)
            {
                return source.BossMaxHp > 0
                    ? CreateBossOnlyPlan(
                        source,
                        EncounterType.Elite)
                    : CreateRegularRoomPlan(source);
            }

            var selection = new Rng(_runSeed)
                .Fork(MidBossSelectionStream)
                .Fork(BiomeIndex);
            var candidateWeights = new int[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                MidBossProfile profile =
                    candidates[i].MidBossProfile;
                int weight = profile?.Weight ?? 1;
                if (profile != null
                    && string.Equals(
                        profile.ThemeId,
                        source.ThemeId,
                        StringComparison.Ordinal))
                    weight = SaturatingMultiply(weight, 3);
                candidateWeights[i] = weight;
            }
            int selectedIndex = selection.PickWeighted(
                candidateWeights,
                candidateWeights.Length);
            EnemyDefinition midBoss =
                candidates[selectedIndex];
            IReadOnlyList<BossPhase> phases =
                midBoss.MidBossProfile?.Phases
                ?? CreateDefaultMidBossPattern(
                    midBoss,
                    selectedIndex);
            int laneMask = source.BossEntryLaneMask != 0
                ? source.BossEntryLaneMask
                : source.StartLaneMask != 0
                    ? source.StartLaneMask
                    : 1;
            var entry = new StageSegment(
                "__mid_boss_entry__",
                1,
                Array.Empty<SpawnEvent>(),
                laneMask,
                laneMask,
                new[] { laneMask });
            return new StagePlan(
                new[] { entry },
                midBoss.Id,
                source.LaneCount,
                laneMask,
                laneMask,
                midBoss.MaxHp,
                midBoss.HalfWidth,
                midBoss.HalfHeight,
                source.BossHoldX,
                phases,
                source.ThemeId,
                source.RequestedThemeId,
                EncounterType.Elite,
                Array.Empty<BossPartDefinition>(),
                source.Gimmick);
        }

        IReadOnlyList<BossPhase> CreateDefaultMidBossPattern(
            EnemyDefinition midBoss,
            int patternVariant)
        {
            BossMovementPattern movementPattern;
            int movementAmplitudeNumerator = 0;
            int movementAmplitudeDenominator = 1;
            int movementPeriodTicks = 1;
            switch (midBoss.MovePattern)
            {
                case EnemyMovePattern.Static:
                    movementPattern =
                        BossMovementPattern.Stationary;
                    break;
                case EnemyMovePattern.Sine:
                    movementPattern =
                        BossMovementPattern.VerticalSine;
                    movementAmplitudeNumerator =
                        midBoss.MovementAmplitudeNumerator;
                    movementAmplitudeDenominator =
                        midBoss.MovementAmplitudeDenominator;
                    movementPeriodTicks =
                        midBoss.MovementPeriodTicks;
                    break;
                default:
                    movementPattern =
                        BossMovementPattern.LegacyHover;
                    break;
            }

            int baseInterval = Math.Max(
                24,
                midBoss.FireIntervalTicks);
            int fastInterval = Math.Max(
                12,
                baseInterval * 2 / 3);
            int dangerousWays = 3 + 2 * (patternVariant & 1);
            BossMovementPattern alternateMovement =
                movementPattern == BossMovementPattern.Stationary
                    ? BossMovementPattern.VerticalSine
                    : BossMovementPattern.Stationary;
            int alternateAmplitude =
                alternateMovement == BossMovementPattern.VerticalSine
                    ? 2 * SimSpace.SubUnitsPerWorldUnit
                    : 0;
            int alternatePeriod =
                alternateMovement == BossMovementPattern.VerticalSine
                    ? 90 + 15 * (patternVariant % 4)
                    : 1;
            var opening = new BossPhase(
                baseInterval,
                1,
                _battleConfig.EnemyBulletSpeedNumerator,
                _battleConfig.EnemyBulletSpeedDenominator,
                movementPattern,
                movementAmplitudeNumerator,
                movementAmplitudeDenominator,
                movementPeriodTicks,
                BossPartVulnerability.Legacy,
                120 + 15 * (patternVariant % 3),
                0);
            var pressure = new BossPhase(
                fastInterval,
                dangerousWays,
                _battleConfig.EnemyBulletSpeedNumerator,
                _battleConfig.EnemyBulletSpeedDenominator,
                alternateMovement,
                alternateAmplitude,
                1,
                alternatePeriod,
                BossPartVulnerability.Legacy,
                105 + 15 * ((patternVariant + 1) % 3),
                18 + 3 * (patternVariant % 3));
            if ((patternVariant & 1) == 0)
                return Array.AsReadOnly(
                    new[] { opening, pressure });

            var burst = new BossPhase(
                Math.Max(10, fastInterval - 4),
                3,
                SaturatingMultiply(
                    _battleConfig.EnemyBulletSpeedNumerator,
                    5),
                SaturatingMultiply(
                    _battleConfig.EnemyBulletSpeedDenominator,
                    4),
                movementPattern,
                movementAmplitudeNumerator,
                movementAmplitudeDenominator,
                movementPeriodTicks,
                BossPartVulnerability.Legacy,
                90 + 15 * (patternVariant % 3),
                24);
            return Array.AsReadOnly(
                new[] { opening, pressure, burst });
        }

        static StagePlan CreateRegularRoomPlan(StagePlan source)
        {
            return new StagePlan(
                source.Segments,
                string.Empty,
                source.LaneCount,
                source.StartLaneMask,
                source.BossEntryLaneMask,
                0,
                0,
                0,
                0,
                Array.Empty<BossPhase>(),
                source.ThemeId,
                source.RequestedThemeId,
                source.EncounterType,
                Array.Empty<BossPartDefinition>(),
                source.Gimmick);
        }

        static StagePlan CreateBiomeBossPlan(StagePlan source)
        {
            return CreateBossOnlyPlan(
                source,
                EncounterType.Normal);
        }

        static StagePlan CreateBossOnlyPlan(
            StagePlan source,
            EncounterType encounterType)
        {
            int laneMask = source.BossEntryLaneMask != 0
                ? source.BossEntryLaneMask
                : source.StartLaneMask != 0 ? source.StartLaneMask : 1;
            var entry = new StageSegment(
                "__biome_boss_entry__",
                1,
                Array.Empty<SpawnEvent>(),
                laneMask,
                laneMask,
                new[] { laneMask });
            return new StagePlan(
                new[] { entry },
                source.BossId,
                source.LaneCount,
                laneMask,
                laneMask,
                source.BossMaxHp,
                source.BossHalfWidth,
                source.BossHalfHeight,
                source.BossHoldX,
                source.BossPhases,
                source.ThemeId,
                source.RequestedThemeId,
                encounterType,
                source.BossParts,
                source.Gimmick);
        }

        void CaptureStageStart()
        {
            for (int i = 0; i < PowerUpGauge.SlotCount; i++)
            {
                _stageStartPowerUpLevels[i] =
                    PowerUpGauge.GetLevel((PowerUpSlot)i);
                _stageStartPowerUpProgress[i] =
                    PowerUpGauge.GetProgress((PowerUpSlot)i);
            }
            _stageStartPowerUpCursor = PowerUpGauge.Cursor;
            _stageStartScore = _completedStageScore;
            _stageStartShotsFired = _completedShotsFired;
            _stageStartShotsHit = _completedShotsHit;
            _stageStartKills = _completedKills;
            _stageStartCapsulesCollected =
                _completedCapsulesCollected;
            _stageStartCapsuleBalance =
                _capsuleBalance;
            _stageStartGrazeCount = _completedGrazeCount;
            _stageStartStagesCleared = _stagesCleared;
            _stageStartRoomsCleared = _roomsCleared;
            _stageStartPlayerLife = Battle.PlayerHp;
            _stageStartShieldStock = Battle.ShieldStock;
            _stageStartBombStock = Battle.BombStock;
            _stageStartActiveModifiers = ActiveModifiers;
            _stageStartPrimaryWeaponFamily =
                CurrentPrimaryWeaponFamily;
            _stageStartMissileFamily = CurrentMissileFamily;
            _stageStartOptionFormation = CurrentOptionFormation;
            _stageStartEliteRoomsCleared =
                EliteRoomsCleared;
            _stageStartNoHitBiomesCleared =
                NoHitBiomesCleared;
            _stageStartRareEncountersCleared =
                RareEncountersCleared;
            _stageStartCurrentBiomeHit =
                _currentBiomeHit;
            _stageStartFireIntervalTicks =
                _battleConfig.FireIntervalTicks;
            _stageStartMainShotBaseDamage =
                _battleConfig.MainShotBaseDamage;
            _stageStartPlayerSpeedNumerator =
                _battleConfig.PlayerSpeedNumerator;
            _stageStartPlayerSpeedDenominator =
                _battleConfig.PlayerSpeedDenominator;
            _stageStartContinuity =
                ((BattleSim)Battle).CaptureContinuityState();
            Array.Copy(
                _rewardAcquisitionCounts,
                _stageStartRewardAcquisitionCounts,
                _rewardAcquisitionCounts.Length);
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

        static void ValidateShipGauge(
            ShipDefinition ship,
            PowerUpGauge gauge)
        {
            if (!ship.HasCustomPowerUpGauge)
                return;
            if (gauge.GaugeSlotCount != ship.GaugeSlots.Count)
                throw new ArgumentException(
                    $"Ship '{ship.Id}' requires a "
                    + $"{ship.GaugeSlots.Count}-slot gauge.",
                    nameof(gauge));
            for (int i = 0; i < ship.GaugeSlots.Count; i++)
            {
                if (gauge.GaugeSlots[i].Slot != ship.GaugeSlots[i])
                    throw new ArgumentException(
                        $"Ship '{ship.Id}' gauge slot {i} must be "
                        + $"'{ship.GaugeSlots[i]}'.",
                        nameof(gauge));
            }
        }

        void ResetShieldStockForNewRun()
        {
            long stock = (long)_initialShieldStock
                + PowerUpGauge.GetLevel(PowerUpSlot.Shield);
            _battleConfig.StartingShieldStock = (int)Math.Min(
                stock,
                _battleConfig.MaxShieldStock);
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

        static void ApplyShipWeaponProfile(
            BattleSimConfig config,
            ShipDefinition ship)
        {
            config.PlayerWeaponType = ship.WeaponType;
            switch (ship.WeaponType)
            {
                case WeaponType.Vulcan:
                    return;
                case WeaponType.Laser:
                    config.MainShotBaseDamage =
                        config.LaserBaseDamage;
                    config.FireIntervalTicks =
                        config.LaserFireIntervalTicks;
                    config.MainShotRapidFireStartLevel =
                        config.LaserRapidFireStartLevel;
                    config.MainShotFireIntervalReductionPerLevel =
                        config.LaserFireIntervalReductionPerLevel;
                    config.MainShotMinimumFireIntervalTicks =
                        config.LaserMinimumFireIntervalTicks;
                    config.PlayerBulletSpeedNumerator =
                        config.LaserSpeedNumerator;
                    config.PlayerBulletSpeedDenominator =
                        config.LaserSpeedDenominator;
                    config.MainShotHalfWidth =
                        config.LaserHalfWidth;
                    config.MainShotHalfHeight =
                        config.LaserHalfHeight;
                    return;
                case WeaponType.Spread:
                    config.MainShotBaseDamage =
                        config.SpreadBaseDamage;
                    config.FireIntervalTicks =
                        config.SpreadFireIntervalTicks;
                    config.MainShotRapidFireStartLevel =
                        config.SpreadRapidFireStartLevel;
                    config.MainShotFireIntervalReductionPerLevel =
                        config.SpreadFireIntervalReductionPerLevel;
                    config.MainShotMinimumFireIntervalTicks =
                        config.SpreadMinimumFireIntervalTicks;
                    config.PlayerBulletSpeedNumerator =
                        config.SpreadSpeedNumerator;
                    config.PlayerBulletSpeedDenominator =
                        config.SpreadSpeedDenominator;
                    config.MainShotHalfWidth =
                        config.SpreadHalfWidth;
                    config.MainShotHalfHeight =
                        config.SpreadHalfHeight;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(ship),
                        $"Ship '{ship.Id}' has an unsupported weapon type.");
            }
        }

        static PrimaryWeaponFamily PrimaryWeaponFamilyFor(
            WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.Vulcan:
                    return PrimaryWeaponFamily.Vulcan;
                case WeaponType.Laser:
                    return PrimaryWeaponFamily.Laser;
                case WeaponType.Spread:
                    return PrimaryWeaponFamily.Spread;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(weaponType));
            }
        }

        static void NormalizeDifficultyMultiplier(
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            out int reducedNumerator,
            out int reducedDenominator)
        {
            if (difficultyMultiplierNumerator < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(difficultyMultiplierNumerator));
            }
            if (difficultyMultiplierDenominator < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(difficultyMultiplierDenominator));
            }

            int divisor = GreatestCommonDivisor(
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator);
            reducedNumerator =
                difficultyMultiplierNumerator / divisor;
            reducedDenominator =
                difficultyMultiplierDenominator / divisor;
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
