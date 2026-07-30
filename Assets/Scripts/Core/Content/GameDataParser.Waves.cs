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
                bosses[i] = ParseBoss(bossSource[i], i, content);
                EnsureUniqueBossId(bosses, i);
            }

            string[] themes = ParseThemes(root.themes, segments, bosses);
            var catalog = new StageGenerationCatalog(
                Require(root.laneCount, "waves.json.laneCount"),
                Require(root.segmentsPerStage, "waves.json.segmentsPerStage"),
                Require(root.startLaneMask, "waves.json.startLaneMask"),
                segments,
                bosses,
                themes);
            return new WavesParseResult(catalog, scrollSpeed);
        }

        static string[] ParseThemes(
            string[] source,
            StageSegmentTemplate[] segments,
            StageBossTemplate[] bosses)
        {
            if (source == null)
                return null;

            var themes = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                string path = $"waves.json.themes[{i}]";
                themes[i] = RequireText(source[i], path);
                for (int earlier = 0; earlier < i; earlier++)
                    if (string.Equals(themes[earlier], themes[i], StringComparison.Ordinal))
                        throw Error(path, $"duplicates theme '{themes[i]}'.");

                if (!CatalogContainsTheme(segments, bosses, themes[i]))
                    throw Error(
                        path,
                        $"references unregistered theme '{themes[i]}'.");
            }

            for (int i = 0; i < segments.Length; i++)
                EnsureThemeIsListed(
                    segments[i].ThemeId,
                    $"waves.json.segments[{i}].theme",
                    themes);
            for (int i = 0; i < bosses.Length; i++)
                EnsureThemeIsListed(
                    bosses[i].ThemeId,
                    $"waves.json.bosses[{i}].theme",
                    themes);
            return themes;
        }

        static bool CatalogContainsTheme(
            StageSegmentTemplate[] segments,
            StageBossTemplate[] bosses,
            string themeId)
        {
            for (int i = 0; i < segments.Length; i++)
                if (string.Equals(
                        segments[i].ThemeId,
                        themeId,
                        StringComparison.Ordinal))
                    return true;
            for (int i = 0; i < bosses.Length; i++)
                if (string.Equals(
                        bosses[i].ThemeId,
                        themeId,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        static void EnsureThemeIsListed(
            string themeId,
            string path,
            string[] themes)
        {
            if (themeId == null)
                return;
            for (int i = 0; i < themes.Length; i++)
                if (string.Equals(themes[i], themeId, StringComparison.Ordinal))
                    return;
            throw Error(
                path,
                $"theme '{themeId}' is missing from waves.json.themes.");
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

            ObstacleDto[] obstacleSource = source.obstacles
                ?? Array.Empty<ObstacleDto>();
            var obstacles = new ObstacleSpawn[obstacleSource.Length];
            for (int i = 0; i < obstacleSource.Length; i++)
            {
                string obstaclePath = $"{path}.obstacles[{i}]";
                ObstacleDto obstacle = obstacleSource[i];
                if (obstacle == null)
                    throw Error(obstaclePath, "cannot be null.");

                string typeText = RequireText(
                    obstacle.type,
                    obstaclePath + ".type");
                ObstacleType type;
                if (string.Equals(typeText, "solid", StringComparison.Ordinal))
                    type = ObstacleType.Solid;
                else if (string.Equals(
                    typeText,
                    "breakable",
                    StringComparison.Ordinal))
                    type = ObstacleType.Breakable;
                else if (string.Equals(
                    typeText,
                    "laserEmitter",
                    StringComparison.Ordinal))
                    type = ObstacleType.LaserEmitter;
                else
                    throw Error(
                        obstaclePath + ".type",
                        "must be 'solid', 'breakable', or 'laserEmitter'.");

                int hp = Require(obstacle.hp, obstaclePath + ".hp");
                if (type == ObstacleType.Solid && hp != 0)
                    throw Error(
                        obstaclePath + ".hp",
                        "must be zero for a solid obstacle.");
                if (type == ObstacleType.Breakable && hp < 1)
                    throw Error(
                        obstaclePath + ".hp",
                        "must be positive for a breakable obstacle.");
                if (type == ObstacleType.LaserEmitter
                    && hp != 0)
                    throw Error(
                        obstaclePath + ".hp",
                        "must be zero for a laser emitter.");

                obstacles[i] = new ObstacleSpawn(
                    type,
                    ToSubUnits(
                        Require(obstacle.x, obstaclePath + ".x"),
                        obstaclePath + ".x"),
                    ToSubUnits(
                        Require(obstacle.y, obstaclePath + ".y"),
                        obstaclePath + ".y"),
                    hp,
                    ParseLaser(
                        obstacle.laser,
                        obstaclePath + ".laser"));
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
                spawns,
                obstacles,
                OptionalText(source.theme, path + ".theme"),
                ParseSegmentWeight(source.weight, path + ".weight"));
        }

        static int ParseSegmentWeight(int? source, string path)
        {
            int weight = source ?? StageSegmentTemplate.DefaultWeight;
            if (weight < 1)
                throw Error(path, "must be positive when present.");
            return weight;
        }

        static StageBossTemplate ParseBoss(
            BossDto source,
            int index,
            BattleContent content)
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
                    BossMovementPattern movementPattern =
                        ParseBossMovementPattern(
                            phase.movementPattern,
                            phasePath + ".movementPattern");
                    ExactFraction movementAmplitude =
                        phase.movementAmplitude.HasValue
                            ? ToSubUnitFraction(
                                phase.movementAmplitude.Value,
                                phasePath + ".movementAmplitude")
                            : new ExactFraction(0, 1);
                    int movementPeriodTicks =
                        phase.movementPeriodTicks ?? 1;
                    if (movementAmplitude.Numerator < 0)
                        throw Error(
                            phasePath + ".movementAmplitude",
                            "must be non-negative.");
                    if (movementPeriodTicks < 1)
                        throw Error(
                            phasePath + ".movementPeriodTicks",
                            "must be positive.");
                    if (movementPattern
                            == BossMovementPattern.VerticalSine
                        && movementAmplitude.Numerator < 1)
                    {
                        throw Error(
                            phasePath + ".movementAmplitude",
                            "must be positive for verticalSine.");
                    }
                    BossPartVulnerability partVulnerability =
                        ParseBossPartVulnerability(
                            phase.partVulnerability,
                            phasePath + ".partVulnerability");
                    phases[i] = new BossPhase(
                        Require(phase.fireIntervalTicks, phasePath + ".fireIntervalTicks"),
                        Require(phase.ways, phasePath + ".ways"),
                        speed.Numerator,
                        speed.Denominator,
                        movementPattern,
                        movementAmplitude.Numerator,
                        movementAmplitude.Denominator,
                        movementPeriodTicks,
                        partVulnerability);
                }
            }

            BossPartDefinition[] parts = Array.Empty<BossPartDefinition>();
            if (source.parts != null && source.parts.Length > 0)
            {
                parts = new BossPartDefinition[source.parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = ParseBossPart(
                        source.parts[i],
                        $"{path}.parts[{i}]",
                        content);
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
                phases,
                OptionalText(source.theme, path + ".theme"),
                parts);
        }

        static BossPartDefinition ParseBossPart(
            BossPartDto source,
            string path,
            BattleContent content)
        {
            if (source == null)
                throw Error(path, "cannot be null.");
            int halfWidth = ToSubUnits(
                Require(source.halfWidth, path + ".halfWidth"),
                path + ".halfWidth");
            int halfHeight = ToSubUnits(
                Require(source.halfHeight, path + ".halfHeight"),
                path + ".halfHeight");
            if (halfWidth < 1)
                throw Error(path + ".halfWidth", "must be positive.");
            if (halfHeight < 1)
                throw Error(path + ".halfHeight", "must be positive.");

            BossPartAttackProfile attack = ParseBossPartAttack(
                source.attack,
                path + ".attack",
                content);
            try
            {
                return new BossPartDefinition(
                    RequireText(source.id, path + ".id"),
                    source.offsetX.HasValue
                        ? ToSubUnits(source.offsetX.Value, path + ".offsetX")
                        : 0,
                    source.offsetY.HasValue
                        ? ToSubUnits(source.offsetY.Value, path + ".offsetY")
                        : 0,
                    halfWidth,
                    halfHeight,
                    Require(source.hp, path + ".hp"),
                    source.isCore ?? false,
                    source.coreGatePartIds ?? Array.Empty<string>(),
                    attack,
                    source.regenerationTicks ?? 0);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static BossPartAttackProfile ParseBossPartAttack(
            BossPartAttackDto source,
            string path,
            BattleContent content)
        {
            if (source == null)
                return BossPartAttackProfile.None;
            BossPartAttackType type = ParseBossPartAttackType(
                RequireText(source.type, path + ".type"),
                path + ".type");
            string spawnEnemyId = OptionalText(
                source.spawnEnemyId,
                path + ".spawnEnemyId");
            if (type == BossPartAttackType.SpawnEnemy
                && content.FindEnemy(spawnEnemyId) == null)
            {
                throw Error(
                    path + ".spawnEnemyId",
                    $"references unknown enemy '{spawnEnemyId}'.");
            }
            ExactFraction bulletSpeed = source.bulletSpeed.HasValue
                ? ToPerTickSpeed(
                    source.bulletSpeed.Value,
                    path + ".bulletSpeed")
                : new ExactFraction(0, 1);
            ExactFraction effectSpeed = source.effectSpeed.HasValue
                ? ToPerTickSpeed(
                    source.effectSpeed.Value,
                    path + ".effectSpeed")
                : new ExactFraction(0, 1);
            try
            {
                return new BossPartAttackProfile(
                    type,
                    source.intervalTicks ?? 0,
                    source.ways ?? 0,
                    bulletSpeed.Numerator,
                    bulletSpeed.Denominator,
                    effectSpeed.Numerator,
                    effectSpeed.Denominator,
                    spawnEnemyId,
                    source.contactDamage ?? 0);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static BossPartAttackType ParseBossPartAttackType(
            string value,
            string path)
        {
            switch (value)
            {
                case "none": return BossPartAttackType.None;
                case "aimedSpread": return BossPartAttackType.AimedSpread;
                case "radialSpread": return BossPartAttackType.RadialSpread;
                case "meleeCharge": return BossPartAttackType.MeleeCharge;
                case "verticalMovement": return BossPartAttackType.VerticalMovement;
                case "spawnEnemy": return BossPartAttackType.SpawnEnemy;
                case "suction": return BossPartAttackType.Suction;
                default:
                    throw Error(path, $"has unknown boss-part attack type '{value}'.");
            }
        }

        static BossMovementPattern ParseBossMovementPattern(
            string value,
            string path)
        {
            if (value == null)
                return BossMovementPattern.LegacyHover;
            switch (RequireText(value, path))
            {
                case "legacyHover":
                    return BossMovementPattern.LegacyHover;
                case "stationary":
                    return BossMovementPattern.Stationary;
                case "verticalSine":
                    return BossMovementPattern.VerticalSine;
                default:
                    throw Error(
                        path,
                        $"has unknown boss movement pattern '{value}'.");
            }
        }

        static BossPartVulnerability ParseBossPartVulnerability(
            string value,
            string path)
        {
            if (value == null)
                return BossPartVulnerability.Legacy;
            switch (RequireText(value, path))
            {
                case "legacy":
                    return BossPartVulnerability.Legacy;
                case "coreOnly":
                    return BossPartVulnerability.CoreOnly;
                case "all":
                    return BossPartVulnerability.All;
                default:
                    throw Error(
                        path,
                        $"has unknown boss part vulnerability '{value}'.");
            }
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
