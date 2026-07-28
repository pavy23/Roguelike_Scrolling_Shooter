using System;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    public static partial class GameDataParser
    {
        static WavesParseResult ParseWaves(WavesDto root, BattleContent content)
        {
            int schemaVersion = Require(root.schemaVersion, "waves.json.schemaVersion");
            if (schemaVersion != SupportedSchemaVersion)
                throw Error(
                    "waves.json.schemaVersion",
                    $"must be {SupportedSchemaVersion}, but was {schemaVersion}.");

            ExactFraction scrollSpeed = ToPerTickSpeed(
                Require(root.scrollSpeed, "waves.json.scrollSpeed"),
                "waves.json.scrollSpeed");
            int spawnX = ToSubUnits(
                Require(root.spawnX, "waves.json.spawnX"),
                "waves.json.spawnX");

            SegmentDto[] segmentSource = RequireArray(
                root.segments,
                "waves.json.segments");
            var segments = new StageSegmentTemplate[segmentSource.Length];
            for (int i = 0; i < segmentSource.Length; i++)
            {
                segments[i] = ParseSegment(segmentSource[i], i, spawnX, content);
                EnsureUniqueSegmentId(segments, i);
            }

            BossDto[] bossSource = RequireArray(root.bosses, "waves.json.bosses");
            var bosses = new StageBossTemplate[bossSource.Length];
            for (int i = 0; i < bossSource.Length; i++)
            {
                bosses[i] = ParseBoss(bossSource[i], i);
                EnsureUniqueBossId(bosses, i);
            }

            var catalog = new StageGenerationCatalog(
                Require(root.laneCount, "waves.json.laneCount"),
                Require(root.segmentsPerStage, "waves.json.segmentsPerStage"),
                Require(root.startLaneMask, "waves.json.startLaneMask"),
                segments,
                bosses);
            return new WavesParseResult(catalog, scrollSpeed);
        }

        static StageSegmentTemplate ParseSegment(
            SegmentDto source,
            int index,
            int spawnX,
            BattleContent content)
        {
            string path = $"waves.json.segments[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");
            SpawnDto[] spawnSource = RequireArray(
                source.spawns,
                path + ".spawns",
                allowEmpty: true);
            var spawns = new SpawnEvent[spawnSource.Length];
            for (int i = 0; i < spawnSource.Length; i++)
            {
                string spawnPath = $"{path}.spawns[{i}]";
                SpawnDto spawn = spawnSource[i];
                if (spawn == null)
                    throw Error(spawnPath, "cannot be null.");
                string enemyId = RequireText(spawn.enemyId, spawnPath + ".enemyId");
                if (content.FindEnemy(enemyId) == null)
                    throw Error(spawnPath + ".enemyId", $"references unknown enemy '{enemyId}'.");
                spawns[i] = new SpawnEvent(
                    Require(spawn.tick, spawnPath + ".tick"),
                    enemyId,
                    spawnX,
                    ToSubUnits(
                        Require(spawn.y, spawnPath + ".y"),
                        spawnPath + ".y"));
            }

            return new StageSegmentTemplate(
                RequireText(source.id, path + ".id"),
                Require(source.difficultyMin, path + ".difficultyMin"),
                Require(source.difficultyMax, path + ".difficultyMax"),
                Require(source.lengthTicks, path + ".lengthTicks"),
                Require(source.entryLaneMask, path + ".entryLaneMask"),
                Require(source.exitLaneMask, path + ".exitLaneMask"),
                RequireArray(
                    source.traversableLaneMasks,
                    path + ".traversableLaneMasks"),
                spawns);
        }

        static StageBossTemplate ParseBoss(BossDto source, int index)
        {
            string path = $"waves.json.bosses[{index}]";
            if (source == null)
                throw Error(path, "cannot be null.");
            int maxHp = Require(source.hp, path + ".hp");
            if (maxHp < 1)
                throw Error(path + ".hp", "must be positive.");

            // 전투 필드는 선택적 (REQ-007/008) — 없으면 0/빈 배열 → 시뮬 기본값 적용.
            int halfWidth = source.halfWidth.HasValue
                ? ToSubUnits(source.halfWidth.Value, path + ".halfWidth") : 0;
            int halfHeight = source.halfHeight.HasValue
                ? ToSubUnits(source.halfHeight.Value, path + ".halfHeight") : 0;
            int holdX = source.holdX.HasValue
                ? ToSubUnits(source.holdX.Value, path + ".holdX") : 0;
            if (source.halfWidth.HasValue && halfWidth <= 0)
                throw Error(path + ".halfWidth", "must be positive when present.");
            if (source.halfHeight.HasValue && halfHeight <= 0)
                throw Error(path + ".halfHeight", "must be positive when present.");

            BossPhase[] phases = Array.Empty<BossPhase>();
            if (source.phases != null && source.phases.Length > 0)
            {
                phases = new BossPhase[source.phases.Length];
                for (int i = 0; i < source.phases.Length; i++)
                {
                    string phasePath = $"{path}.phases[{i}]";
                    BossPhaseDto phase = source.phases[i];
                    if (phase == null)
                        throw Error(phasePath, "cannot be null.");
                    ExactFraction speed = ToPerTickSpeed(
                        Require(phase.bulletSpeed, phasePath + ".bulletSpeed"),
                        phasePath + ".bulletSpeed");
                    phases[i] = new BossPhase(
                        Require(phase.fireIntervalTicks, phasePath + ".fireIntervalTicks"),
                        Require(phase.ways, phasePath + ".ways"),
                        speed.Numerator,
                        speed.Denominator);
                }
            }

            return new StageBossTemplate(
                RequireText(source.id, path + ".id"),
                Require(source.stageIndexMin, path + ".stageIndexMin"),
                Require(source.stageIndexMax, path + ".stageIndexMax"),
                Require(source.difficultyMin, path + ".difficultyMin"),
                Require(source.difficultyMax, path + ".difficultyMax"),
                Require(source.entryLaneMask, path + ".entryLaneMask"),
                maxHp,
                halfWidth,
                halfHeight,
                holdX,
                phases);
        }

        static void EnsureUniqueSegmentId(StageSegmentTemplate[] items, int index)
        {
            for (int i = 0; i < index; i++)
                if (string.Equals(items[i].SegmentId, items[index].SegmentId, StringComparison.Ordinal))
                    throw Error(
                        $"waves.json.segments[{index}].id",
                        $"duplicates id '{items[index].SegmentId}'.");
        }

        static void EnsureUniqueBossId(StageBossTemplate[] items, int index)
        {
            for (int i = 0; i < index; i++)
                if (string.Equals(items[i].BossId, items[index].BossId, StringComparison.Ordinal))
                    throw Error(
                        $"waves.json.bosses[{index}].id",
                        $"duplicates id '{items[index].BossId}'.");
        }

        internal readonly struct WavesParseResult
        {
            public WavesParseResult(
                StageGenerationCatalog catalog,
                ExactFraction scrollSpeed)
            {
                Catalog = catalog;
                ScrollSpeed = scrollSpeed;
            }

            public StageGenerationCatalog Catalog { get; }
            public ExactFraction ScrollSpeed { get; }
        }
    }
}
