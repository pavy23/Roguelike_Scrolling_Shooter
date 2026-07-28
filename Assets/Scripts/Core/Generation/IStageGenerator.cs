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
        {
            Segments = Copy(segments, nameof(segments));
            BossId = bossId ?? throw new ArgumentNullException(nameof(bossId));
            LaneCount = laneCount;
            StartLaneMask = startLaneMask;
            BossEntryLaneMask = bossEntryLaneMask;
        }

        public IReadOnlyList<StageSegment> Segments { get; }
        public string BossId { get; }
        public int LaneCount { get; }
        public int StartLaneMask { get; }
        public int BossEntryLaneMask { get; }

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
        {
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            LengthTicks = lengthTicks;
            Spawns = CopySpawns(spawns);
            EntryLaneMask = entryLaneMask;
            ExitLaneMask = exitLaneMask;
            TraversableLaneMasks = CopyMasks(traversableLaneMasks);
        }

        public string SegmentId { get; }
        public int LengthTicks { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
        public int EntryLaneMask { get; }
        public int ExitLaneMask { get; }
        public IReadOnlyList<int> TraversableLaneMasks { get; }

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
}
