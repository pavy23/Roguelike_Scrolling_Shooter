using System;
using System.Collections.Generic;

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
        {
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            BossId = bossId ?? throw new ArgumentNullException(nameof(bossId));
        }

        public IReadOnlyList<StageSegment> Segments { get; }
        public string BossId { get; }
    }

    public sealed class StageSegment
    {
        public StageSegment(string segmentId, IReadOnlyList<SpawnEvent> spawns)
        {
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
        }

        public string SegmentId { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
    }

    /// <summary>An enemy spawn: simulation tick from segment start plus world-space position.</summary>
    public sealed class SpawnEvent
    {
        public SpawnEvent(int tick, string enemyId, float x, float y)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            Tick = tick;
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            X = x;
            Y = y;
        }

        public int Tick { get; }
        public string EnemyId { get; }
        public float X { get; }
        public float Y { get; }
    }
}
