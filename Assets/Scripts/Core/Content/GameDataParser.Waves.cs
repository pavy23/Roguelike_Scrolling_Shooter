using System;
using System.Collections.Generic;
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
            StageGimmickDefinition[] gimmicks =
                ParseStageGimmicks(root.gimmicks, themes);
            var catalog = new StageGenerationCatalog(
                Require(root.laneCount, "waves.json.laneCount"),
                Require(root.segmentsPerStage, "waves.json.segmentsPerStage"),
                Require(root.startLaneMask, "waves.json.startLaneMask"),
                segments,
                bosses,
                themes,
                gimmicks,
                root.closingSegmentsPerStage,
                root.targetDurationTicks,
                root.closingTargetDurationTicks);
            ContractCatalog contracts =
                ParseContracts(root.contracts);
            return new WavesParseResult(
                catalog,
                scrollSpeed,
                contracts);
        }

        static ContractCatalog ParseContracts(
            ContractCatalogDto root)
        {
            if (root == null)
                return null;
            ContractDto[] source = RequireArray(
                root.entries,
                "waves.json.contracts.entries");
            var definitions =
                new ContractDefinition[source.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                string path =
                    $"waves.json.contracts.entries[{i}]";
                ContractDto item = source[i];
                if (item == null)
                    throw Error(path, "cannot be null.");
                ExactFraction density = ParseContractMultiplier(
                    item.enemyDensityMultiplier,
                    path + ".enemyDensityMultiplier");
                ExactFraction capsules = ParseContractMultiplier(
                    item.capsuleDropMultiplier,
                    path + ".capsuleDropMultiplier");
                ExactFraction bombs = ParseContractMultiplier(
                    item.bombDropMultiplier,
                    path + ".bombDropMultiplier");
                ExactFraction gimmick = ParseContractMultiplier(
                    item.gimmickIntensityMultiplier,
                    path + ".gimmickIntensityMultiplier");
                ExactFraction score = ParseContractMultiplier(
                    item.scoreMultiplier,
                    path + ".scoreMultiplier");
                definitions[i] = new ContractDefinition(
                    RequireText(item.id, path + ".id"),
                    Require(item.weight, path + ".weight"),
                    ParseContractRisk(
                        item.riskTier,
                        path + ".riskTier"),
                    density.Numerator,
                    density.Denominator,
                    capsules.Numerator,
                    capsules.Denominator,
                    bombs.Numerator,
                    bombs.Denominator,
                    item.guaranteedBombDrop ?? false,
                    gimmick.Numerator,
                    gimmick.Denominator,
                    item.rewardOptionCountDelta ?? 0,
                    score.Numerator,
                    score.Denominator,
                    ParseContractDestination(
                        item.destinationKind,
                        path + ".destinationKind"),
                    ParseContractEligibility(
                        item.eligibility,
                        path + ".eligibility"),
                    item.gaugeActivationBanned ?? false,
                    item.optionActivationBanned ?? false,
                    item.shieldActivationBanned ?? false);
            }
            return new ContractCatalog(
                RequireText(
                    root.standardContractId,
                    "waves.json.contracts.standardContractId"),
                Require(
                    root.minimumOptionCount,
                    "waves.json.contracts.minimumOptionCount"),
                Require(
                    root.maximumOptionCount,
                    "waves.json.contracts.maximumOptionCount"),
                definitions);
        }

        static ExactFraction ParseContractMultiplier(
            decimal? value,
            string path)
        {
            decimal multiplier = value ?? 1m;
            if (multiplier < 0m)
                throw Error(path, "cannot be negative.");
            return DecimalToFraction(multiplier, path);
        }

        static ContractRiskTier ParseContractRisk(
            string value,
            string path)
        {
            switch (RequireText(value, path))
            {
                case "safe": return ContractRiskTier.Safe;
                case "low": return ContractRiskTier.Low;
                case "high": return ContractRiskTier.High;
                case "extreme": return ContractRiskTier.Extreme;
                default:
                    throw Error(
                        path,
                        "must be 'safe', 'low', 'high', or 'extreme'.");
            }
        }

        static ContractDestinationKind ParseContractDestination(
            string value,
            string path)
        {
            if (value == null || value == "nextStage")
                return ContractDestinationKind.NextStage;
            switch (RequireText(value, path))
            {
                case "endRun":
                    return ContractDestinationKind.EndRun;
                case "uncharted":
                    return ContractDestinationKind.Uncharted;
                default:
                    throw Error(
                        path,
                        "must be 'nextStage', 'endRun', or 'uncharted'.");
            }
        }

        static ContractEligibility ParseContractEligibility(
            string value,
            string path)
        {
            if (value == null || value == "always")
                return ContractEligibility.Always;
            switch (RequireText(value, path))
            {
                case "hiddenBiomeUnlocked":
                    return ContractEligibility.HiddenBiomeUnlocked;
                default:
                    throw Error(
                        path,
                        "must be 'always' or 'hiddenBiomeUnlocked'.");
            }
        }

        static StageGimmickDefinition[] ParseStageGimmicks(
            StageGimmickDto[] source,
            string[] themes)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<StageGimmickDefinition>();
            if (themes == null)
                throw Error(
                    "waves.json.gimmicks",
                    "requires waves.json.themes.");
            var result = new StageGimmickDefinition[source.Length];
            for (int i = 0; i < result.Length; i++)
            {
                string path = $"waves.json.gimmicks[{i}]";
                StageGimmickDto dto = source[i];
                if (dto == null)
                    throw Error(path, "cannot be null.");
                string theme = RequireText(dto.theme, path + ".theme");
                EnsureThemeIsListed(theme, path + ".theme", themes);
                for (int earlier = 0; earlier < i; earlier++)
                    if (string.Equals(
                            result[earlier].ThemeId,
                            theme,
                            StringComparison.Ordinal))
                        throw Error(path + ".theme", $"duplicates theme '{theme}'.");
                int timeLimitTicks = dto.timeLimitTicks ?? 0;
                if (timeLimitTicks < 0)
                    throw Error(
                        path + ".timeLimitTicks",
                        "cannot be negative.");
                result[i] = new StageGimmickDefinition(
                    theme,
                    dto.visionObscured ?? false,
                    timeLimitTicks);
            }
            return result;
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
                int regenDelayTicks = obstacle.regenDelayTicks ?? 0;
                if (regenDelayTicks < 0)
                    throw Error(
                        obstaclePath + ".regenDelayTicks",
                        "cannot be negative.");
                if (regenDelayTicks > 0
                    && type != ObstacleType.Breakable)
                    throw Error(
                        obstaclePath + ".regenDelayTicks",
                        "requires a breakable obstacle.");

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
                        obstaclePath + ".laser"),
                    obstacle.blocksEnemyBullets ?? false,
                    regenDelayTicks);
            }

            ExactFraction scrollSpeedMultiplier =
                ParseScrollSpeedMultiplier(
                    source.scrollSpeedMultiplier,
                    path + ".scrollSpeedMultiplier");

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
                ParseSegmentWeight(source.weight, path + ".weight"),
                ParseSegmentEnvironment(
                    source.environment,
                    path + ".environment"),
                ParseMidbossOutcomes(
                    source.postMidbossOutcomes,
                    path + ".postMidbossOutcomes"),
                scrollSpeedMultiplier.Numerator,
                scrollSpeedMultiplier.Denominator);
        }

        static ExactFraction ParseScrollSpeedMultiplier(
            decimal? source,
            string path)
        {
            if (!source.HasValue)
                return new ExactFraction(1, 1);
            if (source.Value <= 0)
                throw Error(path, "must be positive.");
            return DecimalToFraction(source.Value, path);
        }

        static MidbossOutcomeKind[] ParseMidbossOutcomes(
            string[] source,
            string path)
        {
            if (source == null)
                return Array.Empty<MidbossOutcomeKind>();
            if (source.Length == 0)
                throw Error(path, "must contain at least one outcome when present.");

            var outcomes = new MidbossOutcomeKind[source.Length];
            for (int i = 0; i < outcomes.Length; i++)
            {
                string itemPath = $"{path}[{i}]";
                string value = RequireText(source[i], itemPath);
                MidbossOutcomeKind outcome;
                if (string.Equals(value, "default", StringComparison.Ordinal))
                    outcome = MidbossOutcomeKind.Default;
                else if (string.Equals(value, "cleanKill", StringComparison.Ordinal))
                    outcome = MidbossOutcomeKind.CleanKill;
                else if (string.Equals(value, "attrition", StringComparison.Ordinal))
                    outcome = MidbossOutcomeKind.Attrition;
                else if (string.Equals(value, "partFocus", StringComparison.Ordinal))
                    outcome = MidbossOutcomeKind.PartFocus;
                else
                    throw Error(
                        itemPath,
                        "must be 'default', 'cleanKill', 'attrition', or 'partFocus'.");
                for (int earlier = 0; earlier < i; earlier++)
                    if (outcomes[earlier] == outcome)
                        throw Error(itemPath, $"duplicates outcome '{value}'.");
                outcomes[i] = outcome;
            }
            return outcomes;
        }

        static SegmentEnvironmentDefinition ParseSegmentEnvironment(
            SegmentEnvironmentDto source,
            string path)
        {
            if (source == null)
                return SegmentEnvironmentDefinition.None;

            bool hasCorridor = source.corridor != null;
            int startMinY = 0;
            int startMaxY = 0;
            int endMinY = 0;
            int endMaxY = 0;
            int contactDamage = 0;
            if (hasCorridor)
            {
                startMinY = ToSubUnits(
                    Require(
                        source.corridor.startMinY,
                        path + ".corridor.startMinY"),
                    path + ".corridor.startMinY");
                startMaxY = ToSubUnits(
                    Require(
                        source.corridor.startMaxY,
                        path + ".corridor.startMaxY"),
                    path + ".corridor.startMaxY");
                endMinY = ToSubUnits(
                    Require(
                        source.corridor.endMinY,
                        path + ".corridor.endMinY"),
                    path + ".corridor.endMinY");
                endMaxY = ToSubUnits(
                    Require(
                        source.corridor.endMaxY,
                        path + ".corridor.endMaxY"),
                    path + ".corridor.endMaxY");
                contactDamage = Require(
                    source.corridor.contactDamage,
                    path + ".corridor.contactDamage");
                if (contactDamage < 1)
                    throw Error(
                        path + ".corridor.contactDamage",
                        "must be positive.");
            }

            ExactFraction driftX = new ExactFraction(0, 1);
            ExactFraction driftY = new ExactFraction(0, 1);
            if (source.drift != null)
            {
                driftX = ToSignedPerTickVelocity(
                    source.drift.xPerSecond ?? 0m,
                    path + ".drift.xPerSecond");
                driftY = ToSignedPerTickVelocity(
                    source.drift.yPerSecond ?? 0m,
                    path + ".drift.yPerSecond");
            }
            try
            {
                return new SegmentEnvironmentDefinition(
                    hasCorridor,
                    startMinY,
                    startMaxY,
                    endMinY,
                    endMaxY,
                    contactDamage,
                    driftX.Numerator,
                    driftX.Denominator,
                    driftY.Numerator,
                    driftY.Denominator);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static ExactFraction ToSignedPerTickVelocity(
            decimal worldUnitsPerSecond,
            string path)
        {
            ExactFraction perSecond =
                ToSubUnitFraction(worldUnitsPerSecond, path);
            long denominator =
                (long)perSecond.Denominator * SimSpace.TicksPerSecond;
            if (denominator > int.MaxValue)
                throw Error(
                    path,
                    "needs a denominator larger than the simulation supports.");
            return new ExactFraction(
                perSecond.Numerator,
                (int)denominator);
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
                    phases[i] = ParseBossPhase(
                        source.phases[i],
                        phasePath,
                        content);
                    if (i == 0
                        && phases[i].SignaturePattern
                            != BossSignaturePattern.None)
                        throw Error(
                            phasePath + ".signaturePatternId",
                            "is reserved for phase 2 or later.");
                    if (phases[i].SignaturePattern
                            == BossSignaturePattern.Brood
                        && content.FindEnemy(
                            phases[i].SignatureSpawnEnemyId) == null)
                        throw Error(
                            phasePath + ".signatureSpawnEnemyId",
                            $"references unknown enemy '{phases[i].SignatureSpawnEnemyId}'.");
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

            WarshipEncounterDefinition warship = source.warship == null
                ? null
                : ParseWarshipEncounter(
                    source.warship,
                    path + ".warship",
                    parts,
                    holdX);
            BossFormDefinition form2 = source.form2 == null
                ? null
                : ParseBossForm(
                    source.form2,
                    path + ".form2",
                    content);

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
                parts,
                warship,
                form2);
        }

        static BossFormDefinition ParseBossForm(
            BossFormDto source,
            string path,
            BattleContent content)
        {
            BossPhaseDto[] phaseSource = RequireArray(
                source.phases,
                path + ".phases");
            if (phaseSource.Length == 0)
                throw Error(path + ".phases", "cannot be empty.");
            var phases = new BossPhase[phaseSource.Length];
            for (int i = 0; i < phases.Length; i++)
                phases[i] = ParseBossPhase(
                    phaseSource[i],
                    $"{path}.phases[{i}]",
                    content);

            BossPartDto[] partSource = source.parts ?? Array.Empty<BossPartDto>();
            var parts = new BossPartDefinition[partSource.Length];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = ParseBossPart(
                    partSource[i],
                    $"{path}.parts[{i}]",
                    content);

            try
            {
                return new BossFormDefinition(
                    RequireText(source.id, path + ".id"),
                    Require(source.transitionTicks, path + ".transitionTicks"),
                    Require(source.hp, path + ".hp"),
                    ToSubUnits(
                        Require(source.halfWidth, path + ".halfWidth"),
                        path + ".halfWidth"),
                    ToSubUnits(
                        Require(source.halfHeight, path + ".halfHeight"),
                        path + ".halfHeight"),
                    source.holdX.HasValue
                        ? ToSubUnits(source.holdX.Value, path + ".holdX")
                        : 0,
                    phases,
                    parts);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static WarshipEncounterDefinition ParseWarshipEncounter(
            WarshipEncounterDto source,
            string path,
            IReadOnlyList<BossPartDefinition> parts,
            int holdX)
        {
            if (parts == null || parts.Count == 0)
                throw Error(path, "requires a non-empty boss parts array.");
            decimal scrollPerSecond = Require(
                source.scrollSpeedPerSecond,
                path + ".scrollSpeedPerSecond");
            if (scrollPerSecond < 0)
                throw Error(
                    path + ".scrollSpeedPerSecond",
                    "must be non-negative.");
            ExactFraction scrollSpeed = ToPerTickSpeed(
                scrollPerSecond,
                path + ".scrollSpeedPerSecond");
            WarshipPartGroupDto[] groupSource = RequireArray(
                source.groups,
                path + ".groups");
            if (groupSource.Length != 3)
                throw Error(path + ".groups", "must contain exactly three groups.");
            var groups = new WarshipPartGroupDefinition[groupSource.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                string groupPath = $"{path}.groups[{i}]";
                WarshipPartGroupDto group = groupSource[i]
                    ?? throw Error(groupPath, "cannot be null.");
                try
                {
                    groups[i] = new WarshipPartGroupDefinition(
                        RequireText(group.id, groupPath + ".id"),
                        ParseWarshipGroupRole(
                            group.role,
                            groupPath + ".role"),
                        RequireArray(
                            group.partIds,
                            groupPath + ".partIds"),
                        group.advanceAfterTicks ?? 0,
                        group.anchorOffsetY.HasValue
                            ? ToSubUnits(
                                group.anchorOffsetY.Value,
                                groupPath + ".anchorOffsetY")
                            : 0,
                        group.anchorTravelTicks ?? 0);
                }
                catch (ArgumentException exception)
                {
                    throw Error(groupPath, exception.Message);
                }
            }
            try
            {
                return new WarshipEncounterDefinition(
                    RequireText(source.id, path + ".id"),
                    source.eventEntityId ?? 0,
                    Require(source.warningTicks, path + ".warningTicks"),
                    ToSubUnits(
                        Require(source.originX, path + ".originX"),
                        path + ".originX"),
                    ToSubUnits(
                        Require(source.originY, path + ".originY"),
                        path + ".originY"),
                    holdX,
                    scrollSpeed.Numerator,
                    scrollSpeed.Denominator,
                    Require(
                        source.baseCoreOpeningWays,
                        path + ".baseCoreOpeningWays"),
                    Require(
                        source.waysReductionPerTurret,
                        path + ".waysReductionPerTurret"),
                    Require(
                        source.minimumCoreOpeningWays,
                        path + ".minimumCoreOpeningWays"),
                    groups,
                    parts);
            }
            catch (ArgumentException exception)
            {
                throw Error(path, exception.Message);
            }
        }

        static WarshipGroupRole ParseWarshipGroupRole(
            string source,
            string path)
        {
            string value = RequireText(source, path);
            switch (value)
            {
                case "midbossGate":
                    return WarshipGroupRole.MidbossGate;
                case "attritionLine":
                    return WarshipGroupRole.AttritionLine;
                case "finalCore":
                    return WarshipGroupRole.FinalCore;
                default:
                    throw Error(
                        path,
                        "must be 'midbossGate', 'attritionLine', or 'finalCore'.");
            }
        }

        static BossPhase ParseBossPhase(
            BossPhaseDto phase,
            string phasePath,
            BattleContent content)
        {
            if (phase == null)
                throw Error(phasePath, "cannot be null.");
            ExactFraction speed = ToPerTickSpeed(
                Require(
                    phase.bulletSpeed,
                    phasePath + ".bulletSpeed"),
                phasePath + ".bulletSpeed");
            BossMovementPattern movementPattern =
                ParseBossMovementPattern(
                    phase.movementPattern,
                    phasePath + ".movementPattern");
            BossFirePattern firePattern =
                ParseBossFirePattern(
                    phase.pattern,
                    phasePath + ".pattern");
            ExactFraction movementAmplitude =
                phase.movementAmplitude.HasValue
                    ? ToSubUnitFraction(
                        phase.movementAmplitude.Value,
                        phasePath + ".movementAmplitude")
                    : new ExactFraction(0, 1);
            int movementPeriodTicks =
                phase.movementPeriodTicks ?? 1;
            int movementTelegraphTicks =
                phase.movementTelegraphTicks ?? 0;
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
            if ((movementPattern == BossMovementPattern.LungeReturn
                    || movementPattern == BossMovementPattern.FigureEight)
                && movementAmplitude.Numerator < 1)
            {
                throw Error(
                    phasePath + ".movementAmplitude",
                    "must be positive for lungeReturn and figureEight.");
            }
            if (movementTelegraphTicks < 0
                || movementTelegraphTicks >= movementPeriodTicks)
            {
                throw Error(
                    phasePath + ".movementTelegraphTicks",
                    "must be non-negative and shorter than movementPeriodTicks.");
            }
            if (movementPattern == BossMovementPattern.LungeReturn
                && (movementTelegraphTicks < 1
                    || movementPeriodTicks - movementTelegraphTicks < 3))
            {
                throw Error(
                    phasePath + ".movementTelegraphTicks",
                    "lungeReturn requires a positive telegraph and at least three movement ticks.");
            }
            int durationTicks = phase.durationTicks ?? 0;
            int telegraphTicks = phase.telegraphTicks ?? 0;
            if (durationTicks < 0)
                throw Error(
                    phasePath + ".durationTicks",
                    "must be non-negative.");
            if (telegraphTicks < 0
                || (durationTicks > 0
                    && telegraphTicks >= durationTicks))
            {
                throw Error(
                    phasePath + ".telegraphTicks",
                    "must be non-negative and shorter than durationTicks.");
            }
            BossPartVulnerability partVulnerability =
                ParseBossPartVulnerability(
                    phase.partVulnerability,
                    phasePath + ".partVulnerability");
            int fireIntervalTicks = Require(
                phase.fireIntervalTicks,
                phasePath + ".fireIntervalTicks");
            int ways = Require(
                phase.ways,
                phasePath + ".ways");
            BossProjectileKind projectileKind = ParseBossProjectileKind(
                phase.projectileKind,
                phasePath + ".projectileKind");
            int splitAfterTicks = phase.splitAfterTicks ?? 0;
            int mineTravelTicks = phase.mineTravelTicks ?? 0;
            int mineTelegraphTicks = phase.mineTelegraphTicks ?? 0;
            ExactFraction mineAcceleration = phase.mineAcceleration.HasValue
                ? ToPerTickAcceleration(
                    phase.mineAcceleration.Value,
                    phasePath + ".mineAcceleration")
                : new ExactFraction(0, 1);
            BossSignaturePattern signaturePattern =
                ParseBossSignaturePattern(
                    phase.signaturePatternId,
                    phasePath + ".signaturePatternId");
            int signatureObstacleHp = phase.signatureObstacleHp ?? 0;
            ExactFraction signatureGravity = phase.signatureGravity.HasValue
                ? ToPerTickAcceleration(
                    phase.signatureGravity.Value,
                    phasePath + ".signatureGravity")
                : new ExactFraction(0, 1);
            int signatureHomingTurn =
                phase.signatureHomingTurnLutSlotsPerTick ?? 0;
            LaserAttackDefinition bossLaser = phase.bossLaser == null
                ? null
                : ParseLaser(
                    phase.bossLaser,
                    phasePath + ".bossLaser");
            ExactFraction hpThreshold = phase.hpThreshold.HasValue
                ? DecimalToFraction(
                    phase.hpThreshold.Value,
                    phasePath + ".hpThreshold")
                : new ExactFraction(0, 1);
            if ((phase.hpThreshold.HasValue
                    && hpThreshold.Numerator <= 0)
                || hpThreshold.Numerator > hpThreshold.Denominator)
                throw Error(
                    phasePath + ".hpThreshold",
                    "must be greater than zero and at most one when present.");
            BossPhasePartRuleDto[] ruleSource =
                phase.partRules ?? Array.Empty<BossPhasePartRuleDto>();
            var partRules = new BossPhasePartRule[ruleSource.Length];
            for (int i = 0; i < partRules.Length; i++)
            {
                string rulePath = $"{phasePath}.partRules[{i}]";
                BossPhasePartRuleDto rule = ruleSource[i]
                    ?? throw Error(rulePath, "cannot be null.");
                try
                {
                    partRules[i] = new BossPhasePartRule(
                        RequireText(rule.partId, rulePath + ".partId"),
                        rule.active ?? throw Error(
                            rulePath + ".active",
                            "is required."),
                        rule.invulnerable ?? throw Error(
                            rulePath + ".invulnerable",
                            "is required."),
                        rule.attack == null
                            ? null
                            : ParseBossPartAttack(
                                rule.attack,
                                rulePath + ".attack",
                                content));
                }
                catch (ArgumentException error)
                {
                    throw Error(rulePath, error.Message);
                }
            }
            if (firePattern == BossFirePattern.Wall && ways < 2)
                throw Error(
                    phasePath + ".ways",
                    "must be at least two for wall.");
            if (firePattern == BossFirePattern.Burst
                && telegraphTicks < 1)
            {
                throw Error(
                    phasePath + ".telegraphTicks",
                    "must be positive for burst.");
            }
            return new BossPhase(
                fireIntervalTicks,
                ways,
                speed.Numerator,
                speed.Denominator,
                movementPattern,
                movementAmplitude.Numerator,
                movementAmplitude.Denominator,
                movementPeriodTicks,
                partVulnerability,
                durationTicks,
                telegraphTicks,
                firePattern,
                projectileKind,
                splitAfterTicks,
                mineTravelTicks,
                mineTelegraphTicks,
                mineAcceleration.Numerator,
                mineAcceleration.Denominator,
                signaturePattern,
                OptionalText(
                    phase.signatureSpawnEnemyId,
                    phasePath + ".signatureSpawnEnemyId"),
                signatureObstacleHp,
                signatureGravity.Numerator,
                signatureGravity.Denominator,
                signatureHomingTurn,
                bossLaser,
                movementTelegraphTicks,
                hpThreshold.Numerator,
                hpThreshold.Denominator,
                partRules,
                ParseSegmentChain(
                    phase.segmentChain,
                    phasePath + ".segmentChain"));
        }

        static SegmentChainDefinition ParseSegmentChain(
            SegmentChainDto source,
            string path)
        {
            if (source == null)
                return null;
            ExactFraction moveSpeed = ToPerTickSpeed(
                Require(source.moveSpeed, path + ".moveSpeed"),
                path + ".moveSpeed");
            string hitRule = RequireText(
                source.hitRule,
                path + ".hitRule");
            SegmentChainDamageRule damageRule;
            if (string.Equals(
                    hitRule,
                    "headOnly",
                    StringComparison.Ordinal))
                damageRule = SegmentChainDamageRule.HeadOnly;
            else
                throw Error(
                    path + ".hitRule",
                    "must be 'headOnly'.");
            try
            {
                return new SegmentChainDefinition(
                    Require(source.segmentCount, path + ".segmentCount"),
                    Require(source.summonCount, path + ".summonCount"),
                    Require(
                        source.summonIntervalTicks,
                        path + ".summonIntervalTicks"),
                    Require(source.headHp, path + ".headHp"),
                    ToSubUnits(
                        Require(source.halfWidth, path + ".halfWidth"),
                        path + ".halfWidth"),
                    ToSubUnits(
                        Require(source.halfHeight, path + ".halfHeight"),
                        path + ".halfHeight"),
                    moveSpeed.Numerator,
                    moveSpeed.Denominator,
                    Require(
                        source.turnLutSlotsPerTick,
                        path + ".turnLutSlotsPerTick"),
                    Require(
                        source.followDelayTicks,
                        path + ".followDelayTicks"),
                    source.contactDamage ?? 0,
                    source.spawnOffsetX.HasValue
                        ? ToSubUnits(
                            source.spawnOffsetX.Value,
                            path + ".spawnOffsetX")
                        : 0,
                    source.spawnOffsetY.HasValue
                        ? ToSubUnits(
                            source.spawnOffsetY.Value,
                            path + ".spawnOffsetY")
                        : 0,
                    damageRule);
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        static BossProjectileKind ParseBossProjectileKind(
            string value,
            string path)
        {
            if (value == null)
                return BossProjectileKind.Normal;
            switch (RequireText(value, path))
            {
                case "normal": return BossProjectileKind.Normal;
                case "heavy": return BossProjectileKind.Heavy;
                case "splitter": return BossProjectileKind.Splitter;
                case "mine": return BossProjectileKind.Mine;
                case "bossLaser": return BossProjectileKind.BossLaser;
                default:
                    throw Error(path, $"has unknown projectile kind '{value}'.");
            }
        }

        static BossSignaturePattern ParseBossSignaturePattern(
            string value,
            string path)
        {
            if (value == null)
                return BossSignaturePattern.None;
            switch (RequireText(value, path))
            {
                case "none": return BossSignaturePattern.None;
                case "scrapThrow": return BossSignaturePattern.ScrapThrow;
                case "brood": return BossSignaturePattern.Brood;
                case "laserGrid": return BossSignaturePattern.LaserGrid;
                case "lightning": return BossSignaturePattern.Lightning;
                case "prismCore": return BossSignaturePattern.PrismCore;
                default:
                    throw Error(path, $"has unknown signature pattern id '{value}'.");
            }
        }

        static BossFirePattern ParseBossFirePattern(
            string value,
            string path)
        {
            if (value == null)
                return BossFirePattern.Aimed;
            switch (RequireText(value, path))
            {
                case "aimed":
                case "spread":
                case "rapid":
                    return BossFirePattern.Aimed;
                case "radial":
                    return BossFirePattern.Radial;
                case "spiral":
                    return BossFirePattern.Spiral;
                case "wall":
                    return BossFirePattern.Wall;
                case "burst":
                    return BossFirePattern.Burst;
                default:
                    throw Error(
                        path,
                        $"has unknown boss fire pattern '{value}'.");
            }
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
                && content != null
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
            ExactFraction effectMaxSpeed = source.effectMaxSpeed.HasValue
                ? ToPerTickSpeed(
                    source.effectMaxSpeed.Value,
                    path + ".effectMaxSpeed")
                : new ExactFraction(0, 1);
            try
            {
                Simulation.LaserAttackDefinition primaryLaser =
                    source.laser == null
                        ? null
                        : ParseLaser(source.laser, path + ".laser");
                Simulation.LaserAttackDefinition secondaryLaser =
                    source.secondaryLaser == null
                        ? null
                        : ParseLaser(
                            source.secondaryLaser,
                            path + ".secondaryLaser");
                return new BossPartAttackProfile(
                    type,
                    source.intervalTicks ?? 0,
                    source.ways ?? 0,
                    bulletSpeed.Numerator,
                    bulletSpeed.Denominator,
                    effectSpeed.Numerator,
                    effectSpeed.Denominator,
                    spawnEnemyId,
                    source.contactDamage ?? 0,
                    primaryLaser,
                    effectMaxSpeed.Numerator,
                    effectMaxSpeed.Denominator,
                    source.effectOffsetX.HasValue
                        ? ToSubUnits(
                            source.effectOffsetX.Value,
                            path + ".effectOffsetX")
                        : 0,
                    source.effectOffsetY.HasValue
                        ? ToSubUnits(
                            source.effectOffsetY.Value,
                            path + ".effectOffsetY")
                        : 0,
                    secondaryLaser,
                    ParseBossPartBurst(
                        source.secondaryBurst,
                        path + ".secondaryBurst"));
            }
            catch (ArgumentException error)
            {
                throw Error(path, error.Message);
            }
        }

        /// <summary>
        /// 부무장 탄막(REQ-177). 필수 값이 빠지면 조용히 0으로 두지 않고
        /// 그 자리에서 실패시킨다 — 탄막이 안 나가는 이유를 화면에서 찾게 되면
        /// 원인에 도달하는 데 몇 배가 든다.
        /// </summary>
        static BossPartBurstDefinition ParseBossPartBurst(
            BossPartBurstDto source,
            string path)
        {
            if (source == null)
                return null;
            ExactFraction speed = ToPerTickSpeed(
                Require(source.bulletSpeed, path + ".bulletSpeed"),
                path + ".bulletSpeed");
            try
            {
                return new BossPartBurstDefinition(
                    Require(
                        source.cycleIntervalTicks,
                        path + ".cycleIntervalTicks"),
                    Require(source.ways, path + ".ways"),
                    speed.Numerator,
                    speed.Denominator,
                    source.aimed ?? true);
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
                case "laser": return BossPartAttackType.Laser;
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
                case "lungeReturn":
                    return BossMovementPattern.LungeReturn;
                case "figureEight":
                    return BossMovementPattern.FigureEight;
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
                ExactFraction scrollSpeed,
                ContractCatalog contracts)
            {
                Catalog = catalog;
                ScrollSpeed = scrollSpeed;
                Contracts = contracts;
            }

            public StageGenerationCatalog Catalog { get; }
            public ExactFraction ScrollSpeed { get; }
            public ContractCatalog Contracts { get; }
        }
    }
}
