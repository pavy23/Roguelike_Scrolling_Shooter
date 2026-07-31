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
    /// Optional extension implemented by generators that can build an explicitly
    /// chosen map route. Legacy generators only need IStageGenerator.
    /// </summary>
    public interface IRouteStageGenerator : IStageGenerator
    {
        IReadOnlyList<string> ThemeIds { get; }
        IReadOnlyList<string> GetThemeOrder(ulong seed);
        bool CanGenerateRoute(
            string themeId,
            int stageIndex,
            int difficulty,
            EncounterType encounterType);
        StagePlan GenerateRoute(
            ulong seed,
            int stageIndex,
            int difficulty,
            string themeId,
            EncounterType encounterType);
    }

    public enum StageRouteSection
    {
        Default = 0,
        Closing = 1
    }

    /// <summary>
    /// Optional route extension for section-specific data knobs. Legacy route
    /// generators keep using IRouteStageGenerator unchanged.
    /// </summary>
    public interface ISectionRouteStageGenerator
    {
        bool CanGenerateRouteForSection(
            string themeId,
            int stageIndex,
            int difficulty,
            EncounterType encounterType,
            StageRouteSection section);
        StagePlan GenerateRouteForSection(
            ulong seed,
            int stageIndex,
            int difficulty,
            string themeId,
            EncounterType encounterType,
            StageRouteSection section);
    }

    public enum ColossalBossKind
    {
        None = 0,
        Leviathan = 1,
        Broodmother = 2
    }

    /// <summary>
    /// Optional content-backed extension used by the hidden biome to request the
    /// exact colossal boss chosen by RunManager.
    /// </summary>
    public interface IColossalBossStageGenerator
    {
        bool CanGenerateColossalBoss(ColossalBossKind kind);
        StagePlan GenerateColossalBoss(
            ulong seed,
            int stageIndex,
            int difficulty,
            ColossalBossKind kind);
    }

    public enum EncounterType
    {
        Normal = 0,
        Elite = 1,
        Supply = 2,
        Hazard = 3,
        Rare = 4
    }

    public enum BossMovementPattern
    {
        /// <summary>Preserves the pre-REQ-054 fixed hover behavior.</summary>
        LegacyHover = 0,
        Stationary = 1,
        VerticalSine = 2
    }

    public enum BossFirePattern
    {
        /// <summary>Player-aimed N-way volley. Legacy default.</summary>
        Aimed = 0,
        /// <summary>Evenly spaced full-circle ring.</summary>
        Radial = 1,
        /// <summary>Rotating arms advanced once per volley.</summary>
        Spiral = 2,
        /// <summary>Vertical wall with one deterministically selected gap.</summary>
        Wall = 3,
        /// <summary>Telegraphed player-aimed N-way volley.</summary>
        Burst = 4
    }

    /// <summary>Projectile vocabulary selectable by every boss phase (REQ-087).</summary>
    public enum BossProjectileKind
    {
        Normal = 0,
        Heavy = 1,
        Splitter = 2,
        Mine = 3,
        BossLaser = 4
    }

    /// <summary>Stable, content-facing ids for the five approved boss signatures.</summary>
    public enum BossSignaturePattern
    {
        None = 0,
        ScrapThrow = 1,
        Brood = 2,
        LaserGrid = 3,
        Lightning = 4,
        PrismCore = 5
    }

    public enum BossPartVulnerability
    {
        /// <summary>Preserves core-gate behavior from older boss data.</summary>
        Legacy = 0,
        CoreOnly = 1,
        All = 2
    }

    /// <summary>
    /// 보스 페이즈 하나의 발사 파라미터 (REQ-007). 속도는 서브유닛/틱 유리수.
    /// Ways는 홀짝 모두 조준축을 중심으로 대칭 배치된다.
    /// </summary>
    public sealed class BossPhase
    {
        public BossPhase(int fireIntervalTicks, int ways, int bulletSpeedNumerator, int bulletSpeedDenominator)
            : this(
                fireIntervalTicks,
                ways,
                bulletSpeedNumerator,
                bulletSpeedDenominator,
                BossMovementPattern.LegacyHover,
                0,
                1,
                1,
                BossPartVulnerability.Legacy)
        {
        }

        public BossPhase(
            int fireIntervalTicks,
            int ways,
            int bulletSpeedNumerator,
            int bulletSpeedDenominator,
            BossMovementPattern movementPattern,
            int movementAmplitudeNumerator,
            int movementAmplitudeDenominator,
            int movementPeriodTicks,
            BossPartVulnerability partVulnerability,
            int durationTicks = 0,
            int telegraphTicks = 0,
            BossFirePattern firePattern = BossFirePattern.Aimed,
            BossProjectileKind projectileKind = BossProjectileKind.Normal,
            int splitAfterTicks = 0,
            int mineTravelTicks = 0,
            int mineTelegraphTicks = 0,
            int mineAccelerationNumerator = 0,
            int mineAccelerationDenominator = 1,
            BossSignaturePattern signaturePattern = BossSignaturePattern.None,
            string signatureSpawnEnemyId = null,
            int signatureObstacleHp = 0,
            int signatureGravityNumerator = 0,
            int signatureGravityDenominator = 1,
            int signatureHomingTurnLutSlotsPerTick = 0,
            Simulation.LaserAttackDefinition laserAttack = null)
        {
            if (fireIntervalTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (ways < 1)
                throw new ArgumentOutOfRangeException(nameof(ways));
            if (bulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedNumerator));
            if (bulletSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedDenominator));
            if (!Enum.IsDefined(
                    typeof(BossMovementPattern),
                    movementPattern))
                throw new ArgumentOutOfRangeException(
                    nameof(movementPattern));
            if (movementAmplitudeNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(movementAmplitudeNumerator));
            if (movementAmplitudeDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(movementAmplitudeDenominator));
            if (movementPeriodTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(movementPeriodTicks));
            if (movementPattern == BossMovementPattern.VerticalSine
                && movementAmplitudeNumerator < 1)
                throw new ArgumentException(
                    "Vertical sine movement requires positive amplitude.",
                    nameof(movementAmplitudeNumerator));
            if (!Enum.IsDefined(
                    typeof(BossPartVulnerability),
                    partVulnerability))
                throw new ArgumentOutOfRangeException(
                    nameof(partVulnerability));
            if (durationTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks));
            if (telegraphTicks < 0
                || (durationTicks > 0
                    && telegraphTicks >= durationTicks))
                throw new ArgumentOutOfRangeException(
                    nameof(telegraphTicks));
            if (!Enum.IsDefined(
                    typeof(BossFirePattern),
                    firePattern))
                throw new ArgumentOutOfRangeException(
                    nameof(firePattern));
            if (firePattern == BossFirePattern.Wall && ways < 2)
                throw new ArgumentException(
                    "Wall fire patterns require at least two ways.",
                    nameof(ways));
            if (firePattern == BossFirePattern.Burst
                && telegraphTicks < 1)
                throw new ArgumentException(
                    "Burst fire patterns require positive telegraphTicks.",
                    nameof(telegraphTicks));
            if (!Enum.IsDefined(typeof(BossProjectileKind), projectileKind))
                throw new ArgumentOutOfRangeException(nameof(projectileKind));
            if (splitAfterTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(splitAfterTicks));
            if (mineTravelTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(mineTravelTicks));
            if (mineTelegraphTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(mineTelegraphTicks));
            if (mineAccelerationNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(mineAccelerationNumerator));
            if (mineAccelerationDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(mineAccelerationDenominator));
            if (projectileKind == BossProjectileKind.Splitter
                && splitAfterTicks < 1)
                throw new ArgumentException(
                    "Splitter projectiles require positive splitAfterTicks.",
                    nameof(splitAfterTicks));
            if (projectileKind == BossProjectileKind.Mine
                && (mineTravelTicks < 1
                    || mineTelegraphTicks < 1
                    || mineAccelerationNumerator < 1))
                throw new ArgumentException(
                    "Mine projectiles require positive travel, telegraph, and acceleration values.",
                    nameof(projectileKind));
            if (projectileKind == BossProjectileKind.BossLaser
                && laserAttack == null)
                throw new ArgumentException(
                    "Boss-laser phases require a laser profile.",
                    nameof(laserAttack));
            if (!Enum.IsDefined(typeof(BossSignaturePattern), signaturePattern))
                throw new ArgumentOutOfRangeException(nameof(signaturePattern));
            if (signatureObstacleHp < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(signatureObstacleHp));
            if (signatureGravityNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(signatureGravityNumerator));
            if (signatureGravityDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(signatureGravityDenominator));
            if (signatureHomingTurnLutSlotsPerTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(signatureHomingTurnLutSlotsPerTick));
            if (signaturePattern == BossSignaturePattern.ScrapThrow
                && (signatureObstacleHp < 1
                    || signatureGravityNumerator < 1))
                throw new ArgumentException(
                    "Scrap-throw signatures require positive obstacle HP and gravity.",
                    nameof(signaturePattern));
            if (signaturePattern == BossSignaturePattern.Brood
                && (string.IsNullOrEmpty(signatureSpawnEnemyId)
                    || signatureHomingTurnLutSlotsPerTick < 1))
                throw new ArgumentException(
                    "Brood signatures require a spawn enemy id and positive homing turn rate.",
                    nameof(signaturePattern));
            if ((signaturePattern == BossSignaturePattern.LaserGrid
                    || signaturePattern == BossSignaturePattern.Lightning
                    || signaturePattern == BossSignaturePattern.PrismCore)
                && laserAttack == null)
                throw new ArgumentException(
                    "Laser signatures require a laser profile.",
                    nameof(laserAttack));
            FireIntervalTicks = fireIntervalTicks;
            Ways = ways;
            BulletSpeedNumerator = bulletSpeedNumerator;
            BulletSpeedDenominator = bulletSpeedDenominator;
            MovementPattern = movementPattern;
            MovementAmplitudeNumerator = movementAmplitudeNumerator;
            MovementAmplitudeDenominator = movementAmplitudeDenominator;
            MovementPeriodTicks = movementPeriodTicks;
            PartVulnerability = partVulnerability;
            DurationTicks = durationTicks;
            TelegraphTicks = telegraphTicks;
            FirePattern = firePattern;
            ProjectileKind = projectileKind;
            SplitAfterTicks = splitAfterTicks;
            MineTravelTicks = mineTravelTicks;
            MineTelegraphTicks = mineTelegraphTicks;
            MineAccelerationNumerator = mineAccelerationNumerator;
            MineAccelerationDenominator = mineAccelerationDenominator;
            SignaturePattern = signaturePattern;
            SignatureSpawnEnemyId = signatureSpawnEnemyId;
            SignatureObstacleHp = signatureObstacleHp;
            SignatureGravityNumerator = signatureGravityNumerator;
            SignatureGravityDenominator = signatureGravityDenominator;
            SignatureHomingTurnLutSlotsPerTick =
                signatureHomingTurnLutSlotsPerTick;
            LaserAttack = laserAttack;
        }

        public int FireIntervalTicks { get; }
        public int Ways { get; }
        public int BulletSpeedNumerator { get; }
        public int BulletSpeedDenominator { get; }
        public BossMovementPattern MovementPattern { get; }
        public int MovementAmplitudeNumerator { get; }
        public int MovementAmplitudeDenominator { get; }
        public int MovementPeriodTicks { get; }
        public BossPartVulnerability PartVulnerability { get; }
        /// <summary>
        /// Positive values opt the whole phase list into deterministic time
        /// cycling. Zero preserves legacy HP-threshold phase progression.
        /// </summary>
        public int DurationTicks { get; }
        /// <summary>
        /// Delay before this phase's first volley. A telegraph event is emitted
        /// before the delay starts.
        /// </summary>
        public int TelegraphTicks { get; }
        public BossFirePattern FirePattern { get; }
        public BossProjectileKind ProjectileKind { get; }
        public int SplitAfterTicks { get; }
        public int MineTravelTicks { get; }
        public int MineTelegraphTicks { get; }
        public int MineAccelerationNumerator { get; }
        public int MineAccelerationDenominator { get; }
        public BossSignaturePattern SignaturePattern { get; }
        public string SignatureSpawnEnemyId { get; }
        public int SignatureObstacleHp { get; }
        public int SignatureGravityNumerator { get; }
        public int SignatureGravityDenominator { get; }
        public int SignatureHomingTurnLutSlotsPerTick { get; }
        public Simulation.LaserAttackDefinition LaserAttack { get; }
    }

    public enum BossPartAttackType
    {
        None = 0,
        AimedSpread = 1,
        RadialSpread = 2,
        MeleeCharge = 3,
        VerticalMovement = 4,
        SpawnEnemy = 5,
        Suction = 6
    }

    /// <summary>
    /// Allocation-free runtime parameters for one independently destructible
    /// boss-part attack. Speeds are exact simulation-subunit fractions per tick.
    /// </summary>
    public sealed class BossPartAttackProfile
    {
        public static readonly BossPartAttackProfile None =
            new BossPartAttackProfile(
                BossPartAttackType.None, 0, 0, 0, 1, 0, 1, null);

        public BossPartAttackProfile(
            BossPartAttackType type,
            int intervalTicks,
            int ways,
            int bulletSpeedNumerator,
            int bulletSpeedDenominator,
            int effectSpeedNumerator,
            int effectSpeedDenominator,
            string spawnEnemyId)
            : this(
                type,
                intervalTicks,
                ways,
                bulletSpeedNumerator,
                bulletSpeedDenominator,
                effectSpeedNumerator,
                effectSpeedDenominator,
                spawnEnemyId,
                0)
        {
        }

        public BossPartAttackProfile(
            BossPartAttackType type,
            int intervalTicks,
            int ways,
            int bulletSpeedNumerator,
            int bulletSpeedDenominator,
            int effectSpeedNumerator,
            int effectSpeedDenominator,
            string spawnEnemyId,
            int contactDamage)
        {
            if (!Enum.IsDefined(typeof(BossPartAttackType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (intervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            if (ways < 0)
                throw new ArgumentOutOfRangeException(nameof(ways));
            if (bulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedNumerator));
            if (bulletSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(bulletSpeedDenominator));
            if (effectSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(effectSpeedNumerator));
            if (effectSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(effectSpeedDenominator));
            if (contactDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(contactDamage));
            if ((type == BossPartAttackType.AimedSpread
                    || type == BossPartAttackType.RadialSpread)
                && (intervalTicks < 1 || ways < 1
                    || bulletSpeedNumerator < 1))
                throw new ArgumentException(
                    "Projectile part attacks require interval, ways, and speed.");
            if ((type == BossPartAttackType.MeleeCharge
                    || type == BossPartAttackType.VerticalMovement)
                && (intervalTicks < 1 || effectSpeedNumerator < 1))
                throw new ArgumentException(
                    "Movement part attacks require interval and effect speed.");
            if (type == BossPartAttackType.SpawnEnemy
                && (intervalTicks < 1 || string.IsNullOrEmpty(spawnEnemyId)))
                throw new ArgumentException(
                    "Spawn attacks require interval and enemy id.");
            if (type == BossPartAttackType.Suction
                && effectSpeedNumerator < 1)
                throw new ArgumentException(
                    "Suction requires a positive effect speed.");
            if (type != BossPartAttackType.SpawnEnemy
                && spawnEnemyId != null)
                throw new ArgumentException(
                    "Only spawn attacks may specify an enemy id.",
                    nameof(spawnEnemyId));
            if (type != BossPartAttackType.MeleeCharge
                && contactDamage != 0)
                throw new ArgumentException(
                    "Only melee-charge attacks may specify contact damage.",
                    nameof(contactDamage));

            Type = type;
            IntervalTicks = intervalTicks;
            Ways = ways;
            BulletSpeedNumerator = bulletSpeedNumerator;
            BulletSpeedDenominator = bulletSpeedDenominator;
            EffectSpeedNumerator = effectSpeedNumerator;
            EffectSpeedDenominator = effectSpeedDenominator;
            SpawnEnemyId = spawnEnemyId;
            ContactDamage = contactDamage;
        }

        public BossPartAttackType Type { get; }
        public int IntervalTicks { get; }
        public int Ways { get; }
        public int BulletSpeedNumerator { get; }
        public int BulletSpeedDenominator { get; }
        public int EffectSpeedNumerator { get; }
        public int EffectSpeedDenominator { get; }
        public string SpawnEnemyId { get; }
        public int ContactDamage { get; }
    }

    /// <summary>
    /// Immutable hitbox and behavior definition relative to the boss body.
    /// Core gate ids are copied in declared order and must refer to sibling parts.
    /// </summary>
    public sealed class BossPartDefinition
    {
        readonly ReadOnlyCollection<string> _coreGatePartIds;

        public BossPartDefinition(
            string partId,
            int offsetX,
            int offsetY,
            int halfWidth,
            int halfHeight,
            int maxHp,
            bool isCore,
            IReadOnlyList<string> coreGatePartIds,
            BossPartAttackProfile attack,
            int regenerationTicks)
        {
            if (string.IsNullOrEmpty(partId))
                throw new ArgumentException(
                    "Boss part id cannot be null or empty.", nameof(partId));
            if (halfWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(halfHeight));
            if (maxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (regenerationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(regenerationTicks));

            PartId = partId;
            OffsetX = offsetX;
            OffsetY = offsetY;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            MaxHp = maxHp;
            IsCore = isCore;
            Attack = attack ?? BossPartAttackProfile.None;
            RegenerationTicks = regenerationTicks;

            int count = coreGatePartIds == null ? 0 : coreGatePartIds.Count;
            var gates = new string[count];
            for (int i = 0; i < gates.Length; i++)
            {
                string gate = coreGatePartIds[i];
                if (string.IsNullOrEmpty(gate))
                    throw new ArgumentException(
                        "Core gate part ids cannot be null or empty.",
                        nameof(coreGatePartIds));
                for (int previous = 0; previous < i; previous++)
                    if (string.Equals(
                            gates[previous], gate, StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate core gate part id '{gate}'.",
                            nameof(coreGatePartIds));
                gates[i] = gate;
            }
            if (!isCore && gates.Length != 0)
                throw new ArgumentException(
                    "Only a core part may specify gate ids.",
                    nameof(coreGatePartIds));
            _coreGatePartIds = new ReadOnlyCollection<string>(gates);
        }

        public string PartId { get; }
        public int OffsetX { get; }
        public int OffsetY { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int MaxHp { get; }
        public bool IsCore { get; }
        public IReadOnlyList<string> CoreGatePartIds => _coreGatePartIds;
        public BossPartAttackProfile Attack { get; }
        public int RegenerationTicks { get; }
    }

    /// <summary>Ordered segments followed by a boss. Pure data — no Unity types.</summary>
    /// <summary>
    /// Theme-wide stage gimmicks. Vision obstruction is presentation-only.
    /// A positive time limit is a hard, unshieldable deadline in BattleSim.
    /// </summary>
    public sealed class StageGimmickDefinition
    {
        public static readonly StageGimmickDefinition None =
            new StageGimmickDefinition(null, false, 0, true);

        public StageGimmickDefinition(
            string themeId,
            bool visionObscured,
            int timeLimitTicks)
            : this(themeId, visionObscured, timeLimitTicks, false)
        {
        }

        StageGimmickDefinition(
            string themeId,
            bool visionObscured,
            int timeLimitTicks,
            bool allowNullTheme)
        {
            if (!allowNullTheme && string.IsNullOrEmpty(themeId))
                throw new ArgumentException(
                    "A stage gimmick requires a theme id.",
                    nameof(themeId));
            if (timeLimitTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(timeLimitTicks));
            ThemeId = themeId;
            VisionObscured = visionObscured;
            TimeLimitTicks = timeLimitTicks;
        }

        public string ThemeId { get; }
        public bool VisionObscured { get; }
        /// <summary>Zero disables the deadline.</summary>
        public int TimeLimitTicks { get; }
    }

    /// <summary>
    /// Segment-local deterministic environment. Corridor bounds interpolate
    /// linearly over the segment. Drift is an exact subunit fraction per tick.
    /// </summary>
    public sealed class SegmentEnvironmentDefinition
    {
        public static readonly SegmentEnvironmentDefinition None =
            new SegmentEnvironmentDefinition(
                false, 0, 0, 0, 0, 0, 0, 1, 0, 1);

        public SegmentEnvironmentDefinition(
            bool hasCorridor,
            int startMinY,
            int startMaxY,
            int endMinY,
            int endMaxY,
            int corridorContactDamage,
            int driftXNumerator,
            int driftXDenominator,
            int driftYNumerator,
            int driftYDenominator)
        {
            if (driftXDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(driftXDenominator));
            if (driftYDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(driftYDenominator));
            if (corridorContactDamage < 0
                || (hasCorridor && corridorContactDamage < 1))
                throw new ArgumentOutOfRangeException(
                    nameof(corridorContactDamage));
            if (hasCorridor
                && (startMinY >= startMaxY || endMinY >= endMaxY))
                throw new ArgumentException(
                    "Corridor minimum Y must remain below maximum Y.");
            if (!hasCorridor
                && (startMinY != 0
                    || startMaxY != 0
                    || endMinY != 0
                    || endMaxY != 0
                    || corridorContactDamage != 0))
                throw new ArgumentException(
                    "A disabled corridor cannot carry bounds or damage.");

            HasCorridor = hasCorridor;
            StartMinY = startMinY;
            StartMaxY = startMaxY;
            EndMinY = endMinY;
            EndMaxY = endMaxY;
            CorridorContactDamage = corridorContactDamage;
            DriftXNumerator = driftXNumerator;
            DriftXDenominator = driftXDenominator;
            DriftYNumerator = driftYNumerator;
            DriftYDenominator = driftYDenominator;
        }

        public bool HasCorridor { get; }
        public int StartMinY { get; }
        public int StartMaxY { get; }
        public int EndMinY { get; }
        public int EndMaxY { get; }
        public int CorridorContactDamage { get; }
        public int DriftXNumerator { get; }
        public int DriftXDenominator { get; }
        public int DriftYNumerator { get; }
        public int DriftYDenominator { get; }
        public bool HasDrift =>
            DriftXNumerator != 0 || DriftYNumerator != 0;
    }

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
                requestedThemeId,
                EncounterType.Normal)
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
            string requestedThemeId,
            EncounterType encounterType)
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
                requestedThemeId,
                encounterType,
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
            string themeId,
            string requestedThemeId,
            EncounterType encounterType,
            IReadOnlyList<BossPartDefinition> bossParts,
            StageGimmickDefinition gimmick = null)
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
            if (!Enum.IsDefined(typeof(EncounterType), encounterType))
                throw new ArgumentOutOfRangeException(nameof(encounterType));
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
            BossParts = CopyParts(bossParts);
            ValidateParts(BossParts, BossMaxHp);
            ThemeId = themeId;
            RequestedThemeId = requestedThemeId;
            EncounterType = encounterType;
            Gimmick = gimmick ?? StageGimmickDefinition.None;
            if (Gimmick.ThemeId != null
                && !string.Equals(
                    Gimmick.ThemeId,
                    ThemeId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "Stage gimmick theme must match the generated theme.",
                    nameof(gimmick));
            SegmentReuseCount = CountSegmentReuses(Segments);
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
        public IReadOnlyList<BossPartDefinition> BossParts { get; }
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
        /// <summary>The route encounter rules applied to this generated plan.</summary>
        public EncounterType EncounterType { get; }
        public StageGimmickDefinition Gimmick { get; }
        /// <summary>Provisional per-encounter enemy HP scaling.</summary>
        public int EncounterEnemyHpMultiplierNumerator =>
            EncounterType == EncounterType.Rare
                ? 2
                : EncounterType == EncounterType.Elite ? 3 : 1;
        public int EncounterEnemyHpMultiplierDenominator =>
            EncounterType == EncounterType.Elite ? 2 : 1;
        /// <summary>Provisional per-encounter capsule drop-weight scaling.</summary>
        public int CapsuleDropMultiplierNumerator =>
            EncounterType == EncounterType.Supply ? 4 : 1;
        public int CapsuleDropMultiplierDenominator => 1;
        /// <summary>Provisional per-encounter score scaling.</summary>
        public int EncounterScoreMultiplierNumerator =>
            EncounterType == EncounterType.Hazard ? 3 : 1;
        public int EncounterScoreMultiplierDenominator =>
            EncounterType == EncounterType.Hazard ? 2 : 1;
        /// <summary>True when ThemeId is a deterministic safety fallback.</summary>
        public bool ThemeFallbackApplied =>
            !string.Equals(
                ThemeId,
                RequestedThemeId,
                StringComparison.Ordinal);
        /// <summary>
        /// Number of segment positions that reuse an id selected earlier in this
        /// stage. Zero means the generator assembled a fully unique sequence.
        /// </summary>
        public int SegmentReuseCount { get; }
        /// <summary>
        /// True when the catalog or clearability constraints required reuse.
        /// </summary>
        public bool SegmentReuseApplied => SegmentReuseCount != 0;

        static int CountSegmentReuses(IReadOnlyList<StageSegment> segments)
        {
            int count = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                for (int earlier = 0; earlier < i; earlier++)
                {
                    if (!string.Equals(
                            segments[i].SegmentId,
                            segments[earlier].SegmentId,
                            StringComparison.Ordinal))
                        continue;

                    count++;
                    break;
                }
            }
            return count;
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

        static IReadOnlyList<BossPartDefinition> CopyParts(
            IReadOnlyList<BossPartDefinition> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<BossPartDefinition>();
            var copy = new BossPartDefinition[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Boss parts cannot contain null.", nameof(source));
            return new ReadOnlyCollection<BossPartDefinition>(copy);
        }

        static void ValidateParts(
            IReadOnlyList<BossPartDefinition> parts,
            int bossMaxHp)
        {
            int coreCount = 0;
            long totalHp = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                BossPartDefinition part = parts[i];
                totalHp += part.MaxHp;
                for (int previous = 0; previous < i; previous++)
                    if (string.Equals(
                            parts[previous].PartId,
                            part.PartId,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate boss part id '{part.PartId}'.",
                            nameof(parts));
                if (part.IsCore)
                    coreCount++;
            }
            if (parts.Count != 0 && coreCount != 1)
                throw new ArgumentException(
                    "A multipart boss requires exactly one core.",
                    nameof(parts));
            if (parts.Count != 0 && totalHp != bossMaxHp)
                throw new ArgumentException(
                    "Multipart boss HP must equal the sum of its part HP.",
                    nameof(parts));

            for (int i = 0; i < parts.Count; i++)
            {
                BossPartDefinition part = parts[i];
                for (int gate = 0; gate < part.CoreGatePartIds.Count; gate++)
                {
                    string gateId = part.CoreGatePartIds[gate];
                    bool found = false;
                    for (int candidate = 0; candidate < parts.Count; candidate++)
                    {
                        if (candidate == i)
                            continue;
                        if (string.Equals(
                                parts[candidate].PartId,
                                gateId,
                                StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        throw new ArgumentException(
                            $"Boss core gate references unknown part '{gateId}'.",
                            nameof(parts));
                }
            }
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
            IReadOnlyList<ObstacleSpawn> obstacles,
            SegmentEnvironmentDefinition environment = null)
        {
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            LengthTicks = lengthTicks;
            Spawns = CopySpawns(spawns);
            EntryLaneMask = entryLaneMask;
            ExitLaneMask = exitLaneMask;
            TraversableLaneMasks = CopyMasks(traversableLaneMasks);
            Obstacles = CopyObstacles(obstacles);
            Environment =
                environment ?? SegmentEnvironmentDefinition.None;
        }

        public string SegmentId { get; }
        public int LengthTicks { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
        public int EntryLaneMask { get; }
        public int ExitLaneMask { get; }
        public IReadOnlyList<int> TraversableLaneMasks { get; }
        public IReadOnlyList<ObstacleSpawn> Obstacles { get; }
        public SegmentEnvironmentDefinition Environment { get; }

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
        Breakable = 1,
        LaserEmitter = 2
    }

    /// <summary>
    /// An obstacle placed when its segment begins. Solid obstacles use Hp == 0;
    /// breakable obstacles require positive HP.
    /// </summary>
    public sealed class ObstacleSpawn
    {
        public ObstacleSpawn(ObstacleType type, int x, int y, int hp)
            : this(type, x, y, hp, null)
        {
        }

        public ObstacleSpawn(
            ObstacleType type,
            int x,
            int y,
            int hp,
            Simulation.LaserAttackDefinition laserAttack)
        {
            if (!Enum.IsDefined(typeof(ObstacleType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (type == ObstacleType.Solid && hp != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(hp), "Solid obstacle HP must be zero.");
            if (type == ObstacleType.Breakable && hp < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(hp), "Breakable obstacle HP must be positive.");
            if (type == ObstacleType.LaserEmitter
                && (hp != 0 || laserAttack == null))
                throw new ArgumentException(
                    "Laser emitters require zero HP and a laser profile.");
            if (type != ObstacleType.LaserEmitter && laserAttack != null)
                throw new ArgumentException(
                    "Only laser emitters can carry a laser profile.",
                    nameof(laserAttack));

            Type = type;
            X = x;
            Y = y;
            Hp = hp;
            LaserAttack = laserAttack;
        }

        public ObstacleType Type { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public Simulation.LaserAttackDefinition LaserAttack { get; }
    }
}
