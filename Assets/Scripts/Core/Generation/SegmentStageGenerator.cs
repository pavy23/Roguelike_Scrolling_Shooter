using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Generation
{
    /// <summary>
    /// Immutable generation data normally populated from GameData/waves.json.
    /// Array order is significant and is preserved during deterministic selection.
    /// </summary>
    public sealed class StageGenerationCatalog
    {
        public StageGenerationCatalog(
            int laneCount,
            int segmentsPerStage,
            int startLaneMask,
            IReadOnlyList<StageSegmentTemplate> segments,
            IReadOnlyList<StageBossTemplate> bosses)
        {
            if (laneCount < 1 || laneCount > 30)
                throw new ArgumentOutOfRangeException(nameof(laneCount));
            if (segmentsPerStage < 1)
                throw new ArgumentOutOfRangeException(nameof(segmentsPerStage));

            int validLanes = StagePlanClearability.GetValidLaneMask(laneCount);
            StagePlanClearability.ValidateLaneMask(
                startLaneMask, validLanes, nameof(startLaneMask));

            LaneCount = laneCount;
            SegmentsPerStage = segmentsPerStage;
            StartLaneMask = startLaneMask;
            Segments = CopySegments(segments, validLanes);
            Bosses = CopyBosses(bosses, validLanes);

            if (Segments.Count == 0)
                throw new ArgumentException("At least one segment is required.", nameof(segments));
            if (Bosses.Count == 0)
                throw new ArgumentException("At least one boss is required.", nameof(bosses));

            ThemeIds = CollectThemeIds(Segments, Bosses);
        }

        public int LaneCount { get; }
        public int SegmentsPerStage { get; }
        public int StartLaneMask { get; }
        public IReadOnlyList<StageSegmentTemplate> Segments { get; }
        public IReadOnlyList<StageBossTemplate> Bosses { get; }
        public IReadOnlyList<string> ThemeIds { get; }

        static IReadOnlyList<string> CollectThemeIds(
            IReadOnlyList<StageSegmentTemplate> segments,
            IReadOnlyList<StageBossTemplate> bosses)
        {
            var themes = new List<string>();
            for (int i = 0; i < segments.Count; i++)
                AddTheme(themes, segments[i].ThemeId);
            for (int i = 0; i < bosses.Count; i++)
                AddTheme(themes, bosses[i].ThemeId);

            themes.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(themes);
        }

        static void AddTheme(List<string> themes, string themeId)
        {
            if (themeId == null)
                return;
            for (int i = 0; i < themes.Count; i++)
                if (string.Equals(themes[i], themeId, StringComparison.Ordinal))
                    return;
            themes.Add(themeId);
        }

        static IReadOnlyList<StageSegmentTemplate> CopySegments(
            IReadOnlyList<StageSegmentTemplate> source,
            int validLanes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new StageSegmentTemplate[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                StageSegmentTemplate segment = source[i] ?? throw new ArgumentException(
                    "Segments cannot contain null.", nameof(source));
                segment.Validate(validLanes);
                copy[i] = segment;
            }
            return new ReadOnlyCollection<StageSegmentTemplate>(copy);
        }

        static IReadOnlyList<StageBossTemplate> CopyBosses(
            IReadOnlyList<StageBossTemplate> source,
            int validLanes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new StageBossTemplate[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                StageBossTemplate boss = source[i] ?? throw new ArgumentException(
                    "Bosses cannot contain null.", nameof(source));
                boss.Validate(validLanes);
                copy[i] = boss;
            }
            return new ReadOnlyCollection<StageBossTemplate>(copy);
        }
    }

    public sealed class StageSegmentTemplate
    {
        public StageSegmentTemplate(
            string segmentId,
            int difficultyMin,
            int difficultyMax,
            int lengthTicks,
            int entryLaneMask,
            int exitLaneMask,
            IReadOnlyList<int> traversableLaneMasks,
            IReadOnlyList<SpawnEvent> spawns)
            : this(
                segmentId,
                difficultyMin,
                difficultyMax,
                lengthTicks,
                entryLaneMask,
                exitLaneMask,
                traversableLaneMasks,
                spawns,
                null)
        {
        }

        public StageSegmentTemplate(
            string segmentId,
            int difficultyMin,
            int difficultyMax,
            int lengthTicks,
            int entryLaneMask,
            int exitLaneMask,
            IReadOnlyList<int> traversableLaneMasks,
            IReadOnlyList<SpawnEvent> spawns,
            string themeId)
        {
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            DifficultyMin = difficultyMin;
            DifficultyMax = difficultyMax;
            LengthTicks = lengthTicks;
            EntryLaneMask = entryLaneMask;
            ExitLaneMask = exitLaneMask;
            TraversableLaneMasks = CopyMasks(traversableLaneMasks);
            Spawns = CopySpawns(spawns);
            ThemeId = themeId;
        }

        public string SegmentId { get; }
        public int DifficultyMin { get; }
        public int DifficultyMax { get; }
        public int LengthTicks { get; }
        public int EntryLaneMask { get; }
        public int ExitLaneMask { get; }
        public IReadOnlyList<int> TraversableLaneMasks { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
        public string ThemeId { get; }

        internal bool SupportsDifficulty(int difficulty)
        {
            return difficulty >= DifficultyMin && difficulty <= DifficultyMax;
        }

        internal bool SupportsTheme(string themeId)
        {
            return ThemeId == null
                || string.Equals(ThemeId, themeId, StringComparison.Ordinal);
        }

        internal void Validate(int validLanes)
        {
            if (SegmentId.Length == 0)
                throw new ArgumentException("Segment id cannot be empty.");
            if (ThemeId != null && ThemeId.Length == 0)
                throw new ArgumentException("Segment theme id cannot be empty.");
            if (DifficultyMin < 1 || DifficultyMax < DifficultyMin)
                throw new ArgumentException("Segment difficulty range is invalid.");
            if (LengthTicks < 1)
                throw new ArgumentException("Segment length must be positive.");

            StagePlanClearability.ValidateLaneMask(
                EntryLaneMask, validLanes, nameof(EntryLaneMask));
            StagePlanClearability.ValidateLaneMask(
                ExitLaneMask, validLanes, nameof(ExitLaneMask));

            if (TraversableLaneMasks.Count == 0)
                throw new ArgumentException("A segment needs at least one traversal checkpoint.");
            for (int i = 0; i < TraversableLaneMasks.Count; i++)
                StagePlanClearability.ValidateLaneMask(
                    TraversableLaneMasks[i], validLanes, nameof(TraversableLaneMasks));

            if (StagePlanClearability.Advance(
                    EntryLaneMask, this, validLanes) == 0)
                throw new ArgumentException(
                    "Segment has no traversable path from its entry to its exit.");

            for (int i = 0; i < Spawns.Count; i++)
            {
                if (Spawns[i].Tick >= LengthTicks)
                    throw new ArgumentException(
                        "Spawn ticks must be earlier than the segment length.");
            }
        }

        internal StageSegment CreateSegment()
        {
            return new StageSegment(
                SegmentId,
                LengthTicks,
                Spawns,
                EntryLaneMask,
                ExitLaneMask,
                TraversableLaneMasks);
        }

        static IReadOnlyList<int> CopyMasks(IReadOnlyList<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return new ReadOnlyCollection<int>(copy);
        }

        static IReadOnlyList<SpawnEvent> CopySpawns(IReadOnlyList<SpawnEvent> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new SpawnEvent[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Spawns cannot contain null.", nameof(source));
            return new ReadOnlyCollection<SpawnEvent>(copy);
        }
    }

    public sealed class StageBossTemplate
    {
        public StageBossTemplate(
            string bossId,
            int stageIndexMin,
            int stageIndexMax,
            int difficultyMin,
            int difficultyMax,
            int entryLaneMask)
            : this(
                bossId, stageIndexMin, stageIndexMax,
                difficultyMin, difficultyMax, entryLaneMask, 0)
        {
        }

        public StageBossTemplate(
            string bossId,
            int stageIndexMin,
            int stageIndexMax,
            int difficultyMin,
            int difficultyMax,
            int entryLaneMask,
            int maxHp)
            : this(
                bossId, stageIndexMin, stageIndexMax, difficultyMin,
                difficultyMax, entryLaneMask, maxHp, 0, 0, 0, null)
        {
        }

        public StageBossTemplate(
            string bossId,
            int stageIndexMin,
            int stageIndexMax,
            int difficultyMin,
            int difficultyMax,
            int entryLaneMask,
            int maxHp,
            int halfWidth,
            int halfHeight,
            int holdX,
            IReadOnlyList<BossPhase> phases)
            : this(
                bossId,
                stageIndexMin,
                stageIndexMax,
                difficultyMin,
                difficultyMax,
                entryLaneMask,
                maxHp,
                halfWidth,
                halfHeight,
                holdX,
                phases,
                null)
        {
        }

        public StageBossTemplate(
            string bossId,
            int stageIndexMin,
            int stageIndexMax,
            int difficultyMin,
            int difficultyMax,
            int entryLaneMask,
            int maxHp,
            int halfWidth,
            int halfHeight,
            int holdX,
            IReadOnlyList<BossPhase> phases,
            string themeId)
        {
            BossId = bossId ?? throw new ArgumentNullException(nameof(bossId));
            StageIndexMin = stageIndexMin;
            StageIndexMax = stageIndexMax;
            DifficultyMin = difficultyMin;
            DifficultyMax = difficultyMax;
            EntryLaneMask = entryLaneMask;
            MaxHp = maxHp;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            HoldX = holdX;
            Phases = CopyPhases(phases);
            ThemeId = themeId;
        }

        public string BossId { get; }
        public int StageIndexMin { get; }
        public int StageIndexMax { get; }
        public int DifficultyMin { get; }
        public int DifficultyMax { get; }
        public int EntryLaneMask { get; }
        public int MaxHp { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int HoldX { get; }
        public IReadOnlyList<BossPhase> Phases { get; }
        public string ThemeId { get; }

        internal bool Supports(int stageIndex, int difficulty)
        {
            return stageIndex >= StageIndexMin
                && stageIndex <= StageIndexMax
                && difficulty >= DifficultyMin
                && difficulty <= DifficultyMax;
        }

        internal bool SupportsTheme(string themeId)
        {
            return ThemeId == null
                || string.Equals(ThemeId, themeId, StringComparison.Ordinal);
        }

        internal void Validate(int validLanes)
        {
            if (BossId.Length == 0)
                throw new ArgumentException("Boss id cannot be empty.");
            if (ThemeId != null && ThemeId.Length == 0)
                throw new ArgumentException("Boss theme id cannot be empty.");
            if (StageIndexMin < 1 || StageIndexMax < StageIndexMin)
                throw new ArgumentException("Boss stage range is invalid.");
            if (DifficultyMin < 1 || DifficultyMax < DifficultyMin)
                throw new ArgumentException("Boss difficulty range is invalid.");
            if (MaxHp < 0)
                throw new ArgumentException("Boss HP cannot be negative.");
            if (HalfWidth < 0 || HalfHeight < 0)
                throw new ArgumentException("Boss hitbox dimensions cannot be negative.");
            StagePlanClearability.ValidateLaneMask(
                EntryLaneMask, validLanes, nameof(EntryLaneMask));
        }

        static IReadOnlyList<BossPhase> CopyPhases(IReadOnlyList<BossPhase> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<BossPhase>();
            var copy = new BossPhase[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Boss phases cannot contain null.", nameof(source));
            return new ReadOnlyCollection<BossPhase>(copy);
        }
    }

    /// <summary>
    /// Deterministically assembles compatible segment templates. Look-ahead removes
    /// choices that would strand the player before a later segment or the boss.
    /// </summary>
    public sealed class SegmentStageGenerator : IStageGenerator
    {
        const int StageGenerationStream = 0;
        const int SegmentSelectionStream = 0;
        const int BossSelectionStream = 1;

        readonly StageGenerationCatalog _catalog;
        readonly int _validLanes;

        public SegmentStageGenerator(StageGenerationCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _validLanes = StagePlanClearability.GetValidLaneMask(catalog.LaneCount);
        }

        public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
        {
            if (stageIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(stageIndex));
            if (difficulty < 1)
                throw new ArgumentOutOfRangeException(nameof(difficulty));

            string themeId = SelectTheme(stageIndex);
            Rng stageRng = new Rng(seed)
                .Fork(StageGenerationStream)
                .Fork(stageIndex)
                .Fork(difficulty);
            Rng segmentRng = stageRng.Fork(SegmentSelectionStream);
            Rng bossRng = stageRng.Fork(BossSelectionStream);

            var assembled = new StageSegment[_catalog.SegmentsPerStage];
            var completionCache = new Dictionary<long, bool>();
            int reachable = _catalog.StartLaneMask;

            for (int position = 0; position < assembled.Length; position++)
            {
                int remaining = assembled.Length - position - 1;
                var viableIndices = new List<int>();
                var viableExits = new List<int>();

                for (int i = 0; i < _catalog.Segments.Count; i++)
                {
                    StageSegmentTemplate candidate = _catalog.Segments[i];
                    if (!candidate.SupportsDifficulty(difficulty)
                        || !candidate.SupportsTheme(themeId))
                        continue;

                    int exit = StagePlanClearability.Advance(
                        reachable, candidate, _validLanes);
                    if (exit == 0)
                        continue;
                    if (!CanComplete(
                            exit,
                            remaining,
                            stageIndex,
                            difficulty,
                            themeId,
                            completionCache))
                        continue;

                    viableIndices.Add(i);
                    viableExits.Add(exit);
                }

                if (viableIndices.Count == 0)
                    throw new InvalidOperationException(
                        CannotAssembleMessage(themeId));

                int pick = segmentRng.NextInt(0, viableIndices.Count);
                StageSegmentTemplate selected = _catalog.Segments[viableIndices[pick]];
                assembled[position] = selected.CreateSegment();
                reachable = viableExits[pick];
            }

            var compatibleBosses = new List<int>();
            for (int i = 0; i < _catalog.Bosses.Count; i++)
            {
                StageBossTemplate boss = _catalog.Bosses[i];
                if (boss.Supports(stageIndex, difficulty)
                    && boss.SupportsTheme(themeId)
                    && (reachable & boss.EntryLaneMask) != 0)
                    compatibleBosses.Add(i);
            }

            if (compatibleBosses.Count == 0)
                throw new InvalidOperationException(
                    themeId == null
                        ? "Catalog has no reachable boss for the requested inputs."
                        : $"Catalog has no reachable boss for theme '{themeId}' and the requested inputs.");

            int bossPick = bossRng.NextInt(0, compatibleBosses.Count);
            StageBossTemplate selectedBoss =
                _catalog.Bosses[compatibleBosses[bossPick]];

            return new StagePlan(
                assembled,
                selectedBoss.BossId,
                _catalog.LaneCount,
                _catalog.StartLaneMask,
                selectedBoss.EntryLaneMask,
                selectedBoss.MaxHp,
                selectedBoss.HalfWidth,
                selectedBoss.HalfHeight,
                selectedBoss.HoldX,
                selectedBoss.Phases,
                themeId);
        }

        string SelectTheme(int stageIndex)
        {
            return _catalog.ThemeIds.Count == 0
                ? null
                : _catalog.ThemeIds[(stageIndex - 1) % _catalog.ThemeIds.Count];
        }

        static string CannotAssembleMessage(string themeId)
        {
            return themeId == null
                ? "Catalog cannot assemble a clearable stage for the requested inputs."
                : $"Catalog cannot assemble a clearable stage for theme '{themeId}' and the requested inputs.";
        }

        bool CanComplete(
            int reachable,
            int segmentsRemaining,
            int stageIndex,
            int difficulty,
            string themeId,
            IDictionary<long, bool> cache)
        {
            long cacheKey = ((long)segmentsRemaining << 32) | (uint)reachable;
            if (cache.TryGetValue(cacheKey, out bool cached))
                return cached;

            if (segmentsRemaining == 0)
            {
                bool bossReachable = HasReachableBoss(
                    reachable, stageIndex, difficulty, themeId);
                cache.Add(cacheKey, bossReachable);
                return bossReachable;
            }

            for (int i = 0; i < _catalog.Segments.Count; i++)
            {
                StageSegmentTemplate candidate = _catalog.Segments[i];
                if (!candidate.SupportsDifficulty(difficulty)
                    || !candidate.SupportsTheme(themeId))
                    continue;

                int exit = StagePlanClearability.Advance(
                    reachable, candidate, _validLanes);
                if (exit != 0
                    && CanComplete(
                        exit,
                        segmentsRemaining - 1,
                        stageIndex,
                        difficulty,
                        themeId,
                        cache))
                {
                    cache.Add(cacheKey, true);
                    return true;
                }
            }
            cache.Add(cacheKey, false);
            return false;
        }

        bool HasReachableBoss(
            int reachable,
            int stageIndex,
            int difficulty,
            string themeId)
        {
            for (int i = 0; i < _catalog.Bosses.Count; i++)
            {
                StageBossTemplate boss = _catalog.Bosses[i];
                if (boss.Supports(stageIndex, difficulty)
                    && boss.SupportsTheme(themeId)
                    && (reachable & boss.EntryLaneMask) != 0)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Integer-only reachability check shared by generation and tests. Each
    /// checkpoint permits staying in a lane or moving to one adjacent lane.
    /// </summary>
    public static class StagePlanClearability
    {
        public static bool IsClearable(StagePlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (plan.LaneCount < 1 || plan.LaneCount > 30)
                return false;

            int validLanes = GetValidLaneMask(plan.LaneCount);
            if (!IsLaneMaskValid(plan.StartLaneMask, validLanes)
                || !IsLaneMaskValid(plan.BossEntryLaneMask, validLanes))
                return false;

            int reachable = plan.StartLaneMask;
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                reachable = Advance(reachable, plan.Segments[i], validLanes);
                if (reachable == 0)
                    return false;
            }
            return (reachable & plan.BossEntryLaneMask) != 0;
        }

        internal static int Advance(
            int reachable,
            StageSegmentTemplate segment,
            int validLanes)
        {
            reachable &= segment.EntryLaneMask;
            for (int i = 0;
                i < segment.TraversableLaneMasks.Count && reachable != 0;
                i++)
            {
                reachable = Expand(reachable, validLanes)
                    & segment.TraversableLaneMasks[i];
            }
            return reachable & segment.ExitLaneMask;
        }

        static int Advance(
            int reachable,
            StageSegment segment,
            int validLanes)
        {
            if (!IsLaneMaskValid(segment.EntryLaneMask, validLanes)
                || !IsLaneMaskValid(segment.ExitLaneMask, validLanes)
                || segment.TraversableLaneMasks.Count == 0)
                return 0;

            reachable &= segment.EntryLaneMask;
            for (int i = 0;
                i < segment.TraversableLaneMasks.Count && reachable != 0;
                i++)
            {
                int checkpoint = segment.TraversableLaneMasks[i];
                if (!IsLaneMaskValid(checkpoint, validLanes))
                    return 0;
                reachable = Expand(reachable, validLanes) & checkpoint;
            }
            return reachable & segment.ExitLaneMask;
        }

        static int Expand(int lanes, int validLanes)
        {
            return (lanes | (lanes << 1) | (lanes >> 1)) & validLanes;
        }

        internal static int GetValidLaneMask(int laneCount)
        {
            return (1 << laneCount) - 1;
        }

        internal static void ValidateLaneMask(
            int laneMask,
            int validLanes,
            string parameterName)
        {
            if (!IsLaneMaskValid(laneMask, validLanes))
                throw new ArgumentOutOfRangeException(
                    parameterName, "Lane mask must contain only configured lanes.");
        }

        static bool IsLaneMaskValid(int laneMask, int validLanes)
        {
            return laneMask != 0 && (laneMask & ~validLanes) == 0;
        }
    }
}
