using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Generation
{
    /// <summary>
    /// Contract for procedural stage generation. The implementation is owned by
    /// CODEX (AGENTS.md §2) and must be a pure function of its inputs:
    /// the same (seed, stageIndex, difficulty) must always produce an identical
    /// StagePlan, with all randomness drawn from an Rng forked off the seed.
    /// </summary>
    public interface IStageGenerator
    {
        StagePlan Generate(ulong seed, int stageIndex, int difficulty);
    }

    /// <summary>
    /// 보스 페이즈 하나의 발사 파라미터 (REQ-007). 속도는 서브유닛/틱 유리수.
    /// Ways는 홀짝 모두 조준축을 중심으로 대칭 배치된다.
    /// </summary>
    public sealed class BossPhase
    {
        public BossPhase(int fireIntervalTicks, int ways, int bulletSpeedNumerator, int bulletSpeedDenominator)
        {
            if (fireIntervalTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (ways < 1)
                throw new ArgumentOutOfRangeException(nameof(ways));
            if (bulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedNumerator));
            if (bulletSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedDenominator));
            FireIntervalTicks = fireIntervalTicks;
            Ways = ways;
            BulletSpeedNumerator = bulletSpeedNumerator;
            BulletSpeedDenominator = bulletSpeedDenominator;
        }

        public int FireIntervalTicks { get; }
        public int Ways { get; }
        public int BulletSpeedNumerator { get; }
        public int BulletSpeedDenominator { get; }
    }

    /// <summary>Ordered segments followed by a boss. Pure data — no Unity types.</summary>
    public sealed class StagePlan
    {
        public StagePlan(IReadOnlyList<StageSegment> segments, string bossId)
            : this(segments, bossId, 0, 0, 0)
        {
        }

        public StagePlan(
            IReadOnlyList<StageSegment> segments,
            string bossId,
            int laneCount,
            int startLaneMask,
            int bossEntryLaneMask)
            : this(segments, bossId, laneCount, startLaneMask, bossEntryLaneMask, 0, 0, 0, 0, null)
        {
        }

        public StagePlan(
            IReadOnlyList<StageSegment> segments,
            string bossId,
            int laneCount,
            int startLaneMask,
            int bossEntryLaneMask,
            int bossMaxHp,
            int bossHalfWidth,
            int bossHalfHeight,
            int bossHoldX,
            IReadOnlyList<BossPhase> bossPhases)
            : this(
                segments,
                bossId,
                laneCount,
                startLaneMask,
                bossEntryLaneMask,
                bossMaxHp,
                bossHalfWidth,
                bossHalfHeight,
                bossHoldX,
                bossPhases,
                null,
                null)
        {
        }

        public StagePlan(
            IReadOnlyList<StageSegment> segments,
            string bossId,
            int laneCount,
            int startLaneMask,
            int bossEntryLaneMask,
            int bossMaxHp,
            int bossHalfWidth,
            int bossHalfHeight,
            int bossHoldX,
            IReadOnlyList<BossPhase> bossPhases,
            string themeId)
            : this(
                segments,
                bossId,
                laneCount,
                startLaneMask,
                bossEntryLaneMask,
                bossMaxHp,
                bossHalfWidth,
                bossHalfHeight,
                bossHoldX,
                bossPhases,
                themeId,
                themeId)
        {
        }

        public StagePlan(
            IReadOnlyList<StageSegment> segments,
            string bossId,
            int laneCount,
            int startLaneMask,
            int bossEntryLaneMask,
            int bossMaxHp,
            int bossHalfWidth,
            int bossHalfHeight,
            int bossHoldX,
            IReadOnlyList<BossPhase> bossPhases,
            string themeId,
            string requestedThemeId)
        {
            if (bossMaxHp < 0)
                throw new ArgumentOutOfRangeException(nameof(bossMaxHp));
            if (bossHalfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(bossHalfWidth));
            if (bossHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(bossHalfHeight));
            if (themeId != null && themeId.Length == 0)
                throw new ArgumentException("Theme id cannot be empty.", nameof(themeId));
            if (requestedThemeId != null && requestedThemeId.Length == 0)
                throw new ArgumentException(
                    "Requested theme id cannot be empty.",
                    nameof(requestedThemeId));
            Segments = Copy(segments, nameof(segments));
            BossId = bossId ?? throw new ArgumentNullException(nameof(bossId));
            LaneCount = laneCount;
            StartLaneMask = startLaneMask;
            BossEntryLaneMask = bossEntryLaneMask;
            BossMaxHp = bossMaxHp;
            BossHalfWidth = bossHalfWidth;
            BossHalfHeight = bossHalfHeight;
            BossHoldX = bossHoldX;
            BossPhases = CopyPhases(bossPhases);
            ThemeId = themeId;
            RequestedThemeId = requestedThemeId;
        }

        public IReadOnlyList<StageSegment> Segments { get; }
        public string BossId { get; }
        public int LaneCount { get; }
        public int StartLaneMask { get; }
        public int BossEntryLaneMask { get; }

        /// <summary>0이면 보스전 없음 — 스테이지는 기존처럼 틱 소진으로 끝난다 (레거시/테스트 호환).</summary>
        public int BossMaxHp { get; }
        public int BossHalfWidth { get; }
        public int BossHalfHeight { get; }
        /// <summary>보스가 진입 후 정지하는 x (서브유닛). 0이면 시뮬 기본값.</summary>
        public int BossHoldX { get; }
        /// <summary>HP를 페이즈 수로 균등 분할해 전환한다. 비어 있으면 시뮬 기본 1페이즈.</summary>
        public IReadOnlyList<BossPhase> BossPhases { get; }
        /// <summary>
        /// Deterministically selected stage theme, or null for an unthemed catalog.
        /// Presentation uses this id to select the matching background.
        /// </summary>
        public string ThemeId { get; }
        /// <summary>
        /// Theme selected by the run-seed permutation before catalog fallback.
        /// Equals ThemeId unless incomplete content required a deterministic
        /// replacement. Null for an unthemed catalog.
        /// </summary>
        public string RequestedThemeId { get; }
        /// <summary>True when ThemeId is a deterministic safety fallback.</summary>
        public bool ThemeFallbackApplied =>
            !string.Equals(
                ThemeId,
                RequestedThemeId,
                StringComparison.Ordinal);

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

        static IReadOnlyList<StageSegment> Copy(
            IReadOnlyList<StageSegment> source,
            string parameterName)
        {
            if (source == null) throw new ArgumentNullException(parameterName);
            var copy = new StageSegment[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Segments cannot contain null.", parameterName);
            return new ReadOnlyCollection<StageSegment>(copy);
        }
    }

    public sealed class StageSegment
    {
        public StageSegment(string segmentId, IReadOnlyList<SpawnEvent> spawns)
            : this(segmentId, 0, spawns, 0, 0, Array.Empty<int>())
        {
        }

        public StageSegment(
            string segmentId,
            int lengthTicks,
            IReadOnlyList<SpawnEvent> spawns,
            int entryLaneMask,
            int exitLaneMask,
            IReadOnlyList<int> traversableLaneMasks)
            : this(
                segmentId,
                lengthTicks,
                spawns,
                entryLaneMask,
                exitLaneMask,
                traversableLaneMasks,
                Array.Empty<ObstacleSpawn>())
        {
        }

        public StageSegment(
            string segmentId,
            int lengthTicks,
            IReadOnlyList<SpawnEvent> spawns,
            int entryLaneMask,
            int exitLaneMask,
            IReadOnlyList<int> traversableLaneMasks,
            IReadOnlyList<ObstacleSpawn> obstacles)
        {
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            LengthTicks = lengthTicks;
            Spawns = CopySpawns(spawns);
            EntryLaneMask = entryLaneMask;
            ExitLaneMask = exitLaneMask;
            TraversableLaneMasks = CopyMasks(traversableLaneMasks);
            Obstacles = CopyObstacles(obstacles);
        }

        public string SegmentId { get; }
        public int LengthTicks { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
        public int EntryLaneMask { get; }
        public int ExitLaneMask { get; }
        public IReadOnlyList<int> TraversableLaneMasks { get; }
        public IReadOnlyList<ObstacleSpawn> Obstacles { get; }

        static IReadOnlyList<SpawnEvent> CopySpawns(IReadOnlyList<SpawnEvent> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new SpawnEvent[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Spawns cannot contain null.", nameof(source));
            return new ReadOnlyCollection<SpawnEvent>(copy);
        }

        static IReadOnlyList<int> CopyMasks(IReadOnlyList<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return new ReadOnlyCollection<int>(copy);
        }

        static IReadOnlyList<ObstacleSpawn> CopyObstacles(
            IReadOnlyList<ObstacleSpawn> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new ObstacleSpawn[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Obstacles cannot contain null.", nameof(source));
            return new ReadOnlyCollection<ObstacleSpawn>(copy);
        }
    }

    /// <summary>An enemy spawn with position in integer simulation subunits.</summary>
    public sealed class SpawnEvent
    {
        public SpawnEvent(int tick, string enemyId, int x, int y)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            Tick = tick;
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            X = x;
            Y = y;
        }

        public int Tick { get; }
        public string EnemyId { get; }
        public int X { get; }
        public int Y { get; }
    }

    public enum ObstacleType
    {
        Solid = 0,
        Breakable = 1
    }

    /// <summary>
    /// An obstacle placed when its segment begins. Solid obstacles use Hp == 0;
    /// breakable obstacles require positive HP.
    /// </summary>
    public sealed class ObstacleSpawn
    {
        public ObstacleSpawn(ObstacleType type, int x, int y, int hp)
        {
            if (!Enum.IsDefined(typeof(ObstacleType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (type == ObstacleType.Solid && hp != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(hp), "Solid obstacle HP must be zero.");
            if (type == ObstacleType.Breakable && hp < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(hp), "Breakable obstacle HP must be positive.");

            Type = type;
            X = x;
            Y = y;
            Hp = hp;
        }

        public ObstacleType Type { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
    }
}
