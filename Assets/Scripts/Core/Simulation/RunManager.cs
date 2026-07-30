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
        RunCleared = 4
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
            string modifierKey = null)
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
            int modifierInteractionCost = 1)
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
    }

    /// <summary>Immutable reward pool parsed from rewards.json.</summary>
    public sealed class RewardCatalog
    {
        readonly IReadOnlyList<RewardDefinition> _all;

        public RewardCatalog(
            int optionCount,
            IReadOnlyList<RewardDefinition> rewards,
            int maxCombinedModifierCost = 4)
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

            var copy = new RewardDefinition[rewards.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = rewards[i];

            OptionCount = optionCount;
            MaxCombinedModifierCost = maxCombinedModifierCost;
            _all = Array.AsReadOnly(copy);
        }

        public int OptionCount { get; }
        public int MaxCombinedModifierCost { get; }
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
        public const int MidStageRewardOptionCount = 2;
        public const int MainRewardOptionCount = 3;
        /// <summary>Legacy alias for the main reward card count.</summary>
        public const int RewardOptionCount = MainRewardOptionCount;
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

        readonly IStageGenerator _stageGenerator;
        readonly BattleSimConfig _battleConfig;
        readonly BattleContent _battleContent;
        readonly MetaProgression _metaProgression;
        readonly StageDifficultyCurve _difficultyCurve;
        readonly RunProgressionConfig _progressionConfig;
        readonly RewardCatalog _rewards;
        readonly ShipDefinition _ship;
        readonly int _difficultyMultiplierNumerator;
        readonly int _difficultyMultiplierDenominator;
        readonly int[] _powerUpMaxLevels;
        readonly int _initialShieldStock;
        readonly int _initialBombStock;
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
        readonly List<RouteChoice> _routeChoiceHistory;
        readonly ReadOnlyCollection<RouteChoice> _routeChoiceHistoryView;
        MetaState _metaState;
        ColossalBossKind _lastColossalBossAtRunStart;

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
        int _rewardSelectionsRemaining;
        int _rewardSelectionRound;
        RewardSelectionKind _rewardSelectionKind;
        bool _currentBiomeHit;

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
            bool buildInitialStage)
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
            ModifierStacks = new BattleModifierStackSet(
                _rewards.MaxCombinedModifierCost);
            _ship = ship ?? ShipDefinition.CreateDefault();
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
            _rewardOptionBuffer = new RewardOption[RewardOptionCount];
            _rewardOptionCatalogIndices = new int[RewardOptionCount];
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
            _routeChoiceHistory = new List<RouteChoice>();
            _routeChoiceHistoryView = _routeChoiceHistory.AsReadOnly();
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
            _initialFireIntervalTicks = _battleConfig.FireIntervalTicks;
            _initialMainShotBaseDamage = _battleConfig.MainShotBaseDamage;
            _initialPlayerSpeedNumerator = _battleConfig.PlayerSpeedNumerator;
            _initialPlayerSpeedDenominator = _battleConfig.PlayerSpeedDenominator;
            ApplyShipStartingLevels(PowerUpGauge);
            ResetShieldStockForNewRun();
            CurrentMissileFamily =
                _battleContent.DefaultMissileFamily;
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
            _lastColossalBossAtRunStart =
                ColossalBossKind.None;
            if (buildInitialStage)
                BuildCurrentStage();
        }

        public int RunNumber { get; private set; }
        /// <summary>Current biome. StageIndex remains a compatibility alias.</summary>
        public int BiomeIndex { get; private set; }
        public int StageIndex => BiomeIndex;
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

        /// <summary>
        /// Runtime integration point for future max-stock rewards/options.
        /// Current stock is clamped immediately when the cap is lowered.
        /// </summary>
        public void SetMaxShieldStock(int maxShieldStock)
        {
            if (maxShieldStock < BattleSimConfig.DefaultMaxShieldStock
                || maxShieldStock
                    > BattleSimConfig.MaximumShieldStock)
                throw new ArgumentOutOfRangeException(
                    nameof(maxShieldStock),
                    $"Shield cap must be in "
                    + $"{BattleSimConfig.DefaultMaxShieldStock}.."
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
                    (int)_lastColossalBossAtRunStart
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
            ShipDefinition ship)
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
                false);

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
                {
                    _currentBiomeHit = true;
                    return;
                }
            }
        }

        void RecordNoHitBiomeClear()
        {
            if (!_currentBiomeHit
                && NoHitBiomesCleared < int.MaxValue)
                NoHitBiomesCleared++;
            _currentBiomeHit = false;
        }

        void BeginHiddenBiome()
        {
            SelectedColossalBoss = SelectColossalBoss(
                _runSeed,
                _lastColossalBossAtRunStart);
            if (!(_stageGenerator
                    is IColossalBossStageGenerator colossal)
                || !colossal.CanGenerateColossalBoss(
                    SelectedColossalBoss))
            {
                throw new InvalidOperationException(
                    $"Hidden biome selected {SelectedColossalBoss}, "
                    + "but the stage catalog cannot generate it.");
            }

            AccumulateCompletedBattle();
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
        public void ChooseReward(int optionIndex)
        {
            if (State != RunState.AwaitingReward)
                throw new InvalidOperationException("No reward is awaiting selection.");
            if (optionIndex < 0 || optionIndex >= _rewardOptions.Count)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            int catalogIndex = _rewardOptionCatalogIndices[optionIndex];
            ApplyReward(_rewardOptions[optionIndex], catalogIndex);
            if (_rewardAcquisitionCounts[catalogIndex] < int.MaxValue)
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
                return;
            }
            _rewardOptions = Array.Empty<RewardOption>();
            RewardSelectionKind completedKind =
                _rewardSelectionKind;
            _rewardSelectionKind = RewardSelectionKind.None;
            _rewardOptionView.SetCount(0);
            if (completedKind == RewardSelectionKind.MidStage)
            {
                AdvanceAfterRegularSection();
                return;
            }
            if (completedKind != RewardSelectionKind.Main)
                throw new InvalidOperationException(
                    "Reward selection kind was lost.");
            if (_progressionConfig.IsFinalBiome(BiomeIndex))
            {
                if (HiddenConditionCount >= 2)
                    BeginHiddenBiome();
                else
                    CompleteRun(
                        RunCompletionGrade.StandardClear);
                return;
            }
            AdvanceBiome();
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

            if (eligibleCount < optionCount)
                throw new InvalidOperationException(
                    $"Biome {BiomeIndex} has {eligibleCount} eligible rewards; "
                    + $"{optionCount} are required.");

            _rewardRng.ResetForked(
                _runSeed,
                RewardSelectionStream,
                GetRewardSequence());
            for (int i = 0; i < _rewardSelectionRound; i++)
                _rewardRng.NextULong();
            int poolCount = eligibleCount;
            int optionStart = 0;
            if (StagePlan.EncounterType == EncounterType.Elite
                || StagePlan.EncounterType == EncounterType.Rare)
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
                        modifier.ModifierKey);
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
                i < optionCount;
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
                    selected.ModifierKey);

                int last = --poolCount;
                _rewardPool[pick] = _rewardPool[last];
                _rewardPoolCatalogIndices[pick] =
                    _rewardPoolCatalogIndices[last];
                _rewardWeights[pick] = _rewardWeights[last];
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
                && routeGenerator.CanGenerateRoute(
                    themeId,
                    targetBiomeIndex,
                    targetDifficulty,
                    EncounterType.Rare))
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
                if (!routeGenerator.CanGenerateRoute(
                        themeId,
                        targetBiomeIndex,
                        targetDifficulty,
                        encounterType))
                    continue;
                options[optionCount++] =
                    new RouteOption(themeId, encounterType);
                if (optionCount == rareSlot
                    && routeGenerator.CanGenerateRoute(
                        themeId,
                        targetBiomeIndex,
                        targetDifficulty,
                        EncounterType.Rare))
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
            _routeChoiceHistory.Clear();
            _completedStageScore = 0;
            _completedShotsFired = 0;
            _completedShotsHit = 0;
            _completedKills = 0;
            _completedCapsulesCollected = 0;
            _completedGrazeCount = 0;
            _stagesCleared = 0;
            _roomsCleared = 0;
            Array.Clear(
                _rewardAcquisitionCounts,
                0,
                _rewardAcquisitionCounts.Length);
            _battleConfig.StartingShieldStock = _initialShieldStock;
            _battleConfig.StartingBombStock = _initialBombStock;
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
            AccumulateCompletedBattle();
            RoomIndex++;
            IsBiomeBoss = false;
            State = RunState.Playing;
            BuildCurrentStage();
        }

        void AdvanceToBiomeBoss()
        {
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
            AccumulateCompletedBattle();
            BiomeIndex++;
            RoomIndex = 1;
            IsBiomeBoss = false;
            State = RunState.Playing;
            BuildCurrentStage();
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
                || data.powerUpCursor >= PowerUpGauge.SlotCount)
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
                IsHiddenBiome ? BiomeCount : BiomeIndex;
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
                    generationBiomeIndex,
                    Difficulty);
                if (basePlan == null)
                    throw new InvalidOperationException(
                        "The stage generator returned no biome base plan.");
                if (TryGetRouteChoice(
                        battleSequenceBiomeIndex,
                        RoomIndex,
                        out RouteChoice routeChoice))
                {
                    generated = routeGenerator.GenerateRoute(
                        generationSeed,
                        generationBiomeIndex,
                        Difficulty,
                        routeChoice.ThemeId,
                        IsBiomeBoss
                            ? EncounterType.Normal
                            : routeChoice.EncounterType);
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
                    if (!routeGenerator.CanGenerateRoute(
                            basePlan.ThemeId,
                            generationBiomeIndex,
                            Difficulty,
                            encounterType))
                        encounterType = EncounterType.Normal;
                    generated = routeGenerator.GenerateRoute(
                        generationSeed,
                        generationBiomeIndex,
                        Difficulty,
                        basePlan.ThemeId,
                        encounterType);
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
            StagePlan = IsBiomeBoss
                ? CreateBiomeBossPlan(generated)
                : IsMidBossSection
                    ? CreateMidBossPlan(generated)
                    : CreateRegularRoomPlan(generated);
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
                ModifierStacks);
            _preparedRouteOptions = Array.Empty<RouteOption>();
            CaptureStageStart();
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
            StagePlan basePlan = _stageGenerator.Generate(
                _runSeed,
                biomeIndex,
                _difficultyCurve.GetDifficulty(biomeIndex));
            if (basePlan == null)
                throw new InvalidOperationException(
                    "The stage generator returned no biome base plan.");
            return basePlan.ThemeId;
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
                if (enemy.Id.StartsWith(
                        "mini_",
                        StringComparison.Ordinal))
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
            EnemyDefinition midBoss =
                candidates[selection.NextInt(0, candidates.Count)];
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

            var phase = new BossPhase(
                Math.Max(1, midBoss.FireIntervalTicks),
                1,
                _battleConfig.EnemyBulletSpeedNumerator,
                _battleConfig.EnemyBulletSpeedDenominator,
                movementPattern,
                movementAmplitudeNumerator,
                movementAmplitudeDenominator,
                movementPeriodTicks,
                BossPartVulnerability.Legacy);
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
                new[] { phase },
                source.ThemeId,
                source.RequestedThemeId,
                EncounterType.Elite,
                Array.Empty<BossPartDefinition>());
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
                Array.Empty<BossPartDefinition>());
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
                source.BossParts);
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
