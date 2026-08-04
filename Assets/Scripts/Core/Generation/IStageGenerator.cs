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

    public enum MidbossOutcomeKind : byte
    {
        Default = 0,
        CleanKill = 1,
        Attrition = 2,
        PartFocus = 3
    }

    public static class MidbossOutcomeEvaluator
    {
        public static MidbossOutcomeKind Evaluate(
            int defeatElapsedTicks,
            int cleanKillMaxTicks,
            bool partFocusDestroyed)
        {
            if (defeatElapsedTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(defeatElapsedTicks));
            if (cleanKillMaxTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(cleanKillMaxTicks));
            if (partFocusDestroyed)
                return MidbossOutcomeKind.PartFocus;
            if (cleanKillMaxTicks == 0)
                return MidbossOutcomeKind.Default;
            return defeatElapsedTicks <= cleanKillMaxTicks
                ? MidbossOutcomeKind.CleanKill
                : MidbossOutcomeKind.Attrition;
        }
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

    /// <summary>
    /// Optional route extension for outcome-filtered post-midboss generation.
    /// Implementations must keep Default bit-identical to section generation.
    /// </summary>
    public interface IMidbossOutcomeRouteStageGenerator
    {
        bool CanGenerateRouteForSection(
            string themeId,
            int stageIndex,
            int difficulty,
            EncounterType encounterType,
            StageRouteSection section,
            MidbossOutcomeKind outcome);
        StagePlan GenerateRouteForSection(
            ulong seed,
            int stageIndex,
            int difficulty,
            string themeId,
            EncounterType encounterType,
            StageRouteSection section,
            MidbossOutcomeKind outcome);
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

        /// <summary>
        /// 이 거대 보스가 자기 스테이지에 입히는 테마. 히든 스테이지가 직전
        /// 바이옴을 그대로 재사용하지 않고 보스에 맞는 분위기를 갖게 하려는 것이다
        /// (사람 보고 2026-08-04: "히든 스테이지가 기존 스테이지 재활용인데
        /// 각 보스 타입에 따라 맞는 분위기로 새로 만들어줘").
        ///
        /// 데이터가 아직 전용 테마를 갖지 않으면 null을 돌려준다 — 그 경우
        /// 호출자는 종전 동작(마지막 바이옴 테마 재사용)으로 남는다.
        /// </summary>
        string GetColossalBossThemeId(ColossalBossKind kind);
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
        VerticalSine = 2,
        /// <summary>
        /// Holds for MovementTelegraphTicks, lunges toward the player's X side,
        /// then returns to BossHoldX within MovementPeriodTicks.
        /// </summary>
        LungeReturn = 3,
        /// <summary>
        /// Vertical figure-eight driven entirely by the integer sine LUT.
        /// </summary>
        FigureEight = 4
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
    /// Per-phase override for one multipart boss part. Parts without a rule keep
    /// their legacy always-active state and the phase-wide vulnerability rule.
    /// </summary>
    public sealed class BossPhasePartRule
    {
        public BossPhasePartRule(
            string partId,
            bool active,
            bool invulnerable,
            BossPartAttackProfile attack = null)
        {
            if (string.IsNullOrEmpty(partId))
                throw new ArgumentException(
                    "Boss phase part id cannot be null or empty.",
                    nameof(partId));
            PartId = partId;
            Active = active;
            Invulnerable = invulnerable;
            Attack = attack;
        }

        public string PartId { get; }
        public bool Active { get; }
        public bool Invulnerable { get; }
        /// <summary>Null preserves the part's base attack profile.</summary>
        public BossPartAttackProfile Attack { get; }
    }

    public enum SegmentChainDamageRule
    {
        /// <summary>
        /// Only segment zero can take damage. Destroying it removes the entire
        /// chain, matching the Gradius fire-dragon vocabulary.
        /// </summary>
        HeadOnly = 0
    }

    /// <summary>
    /// Deterministic phase-owned segmented minion definition. Movement speeds
    /// use exact simulation-subunit fractions per tick; body segments sample
    /// the head's tick history at FollowDelayTicks intervals.
    /// </summary>
    public sealed class SegmentChainDefinition
    {
        public SegmentChainDefinition(
            int segmentCount,
            int summonCount,
            int summonIntervalTicks,
            int headMaxHp,
            int halfWidth,
            int halfHeight,
            int moveSpeedNumerator,
            int moveSpeedDenominator,
            int turnLutSlotsPerTick,
            int followDelayTicks,
            int contactDamage,
            int spawnOffsetX,
            int spawnOffsetY,
            SegmentChainDamageRule damageRule)
        {
            if (segmentCount < 6 || segmentCount > 8)
                throw new ArgumentOutOfRangeException(
                    nameof(segmentCount),
                    "Segment chains require six to eight segments.");
            if (summonCount < 1)
                throw new ArgumentOutOfRangeException(nameof(summonCount));
            if (summonIntervalTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(summonIntervalTicks));
            if (headMaxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(headMaxHp));
            if (halfWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(halfHeight));
            if (moveSpeedNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedNumerator));
            if (moveSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedDenominator));
            if (turnLutSlotsPerTick < 1 || turnLutSlotsPerTick > 32)
                throw new ArgumentOutOfRangeException(
                    nameof(turnLutSlotsPerTick));
            if (followDelayTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(followDelayTicks));
            if (contactDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(contactDamage));
            if (!Enum.IsDefined(typeof(SegmentChainDamageRule), damageRule))
                throw new ArgumentOutOfRangeException(nameof(damageRule));

            SegmentCount = segmentCount;
            SummonCount = summonCount;
            SummonIntervalTicks = summonIntervalTicks;
            HeadMaxHp = headMaxHp;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            MoveSpeedNumerator = moveSpeedNumerator;
            MoveSpeedDenominator = moveSpeedDenominator;
            TurnLutSlotsPerTick = turnLutSlotsPerTick;
            FollowDelayTicks = followDelayTicks;
            ContactDamage = contactDamage;
            SpawnOffsetX = spawnOffsetX;
            SpawnOffsetY = spawnOffsetY;
            DamageRule = damageRule;
        }

        public int SegmentCount { get; }
        public int SummonCount { get; }
        public int SummonIntervalTicks { get; }
        public int HeadMaxHp { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int MoveSpeedNumerator { get; }
        public int MoveSpeedDenominator { get; }
        public int TurnLutSlotsPerTick { get; }
        public int FollowDelayTicks { get; }
        public int ContactDamage { get; }
        public int SpawnOffsetX { get; }
        public int SpawnOffsetY { get; }
        public SegmentChainDamageRule DamageRule { get; }
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
            Simulation.LaserAttackDefinition laserAttack = null,
            int movementTelegraphTicks = 0,
            int hpThresholdNumerator = 0,
            int hpThresholdDenominator = 1,
            IReadOnlyList<BossPhasePartRule> partRules = null,
            SegmentChainDefinition segmentChain = null)
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
            if ((movementPattern == BossMovementPattern.LungeReturn
                    || movementPattern == BossMovementPattern.FigureEight)
                && movementAmplitudeNumerator < 1)
                throw new ArgumentException(
                    "Lunge-return and figure-eight movement require positive amplitude.",
                    nameof(movementAmplitudeNumerator));
            if (movementTelegraphTicks < 0
                || movementTelegraphTicks >= movementPeriodTicks)
                throw new ArgumentOutOfRangeException(
                    nameof(movementTelegraphTicks));
            if (movementPattern == BossMovementPattern.LungeReturn
                && (movementTelegraphTicks < 1
                    || movementPeriodTicks - movementTelegraphTicks < 3))
                throw new ArgumentException(
                    "Lunge-return movement requires a positive telegraph and at least three movement ticks.",
                    nameof(movementTelegraphTicks));
            if (hpThresholdNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(hpThresholdNumerator));
            if (hpThresholdDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(hpThresholdDenominator));
            if (hpThresholdNumerator > hpThresholdDenominator)
                throw new ArgumentOutOfRangeException(
                    nameof(hpThresholdNumerator),
                    "HP threshold cannot exceed one.");
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
            // 유도율 0을 허용한다 (2026-08-04). 예전에는 1 이상을 강제해서,
            // "발사된 다음에는 유도하지 마라"는 요구를 데이터로 표현할 방법이
            // 없었다 — 유도를 끄려면 Brood 시그니처를 통째로 빼는 수밖에 없었고,
            // 그러자 **소환 패턴까지 같이 사라졌다**(사람 보고: "하이브보스 마지막
            // 유도탄 패턴이 아예 없어졌네").
            //
            // 0은 "조준은 발사 순간에만, 그 뒤로는 직진"이라는 뜻이다. 소환은
            // 그대로 일어난다. 소환 대상 id는 여전히 필수다 — 그게 없으면
            // 시그니처가 아무 일도 하지 않는다.
            if (signaturePattern == BossSignaturePattern.Brood
                && string.IsNullOrEmpty(signatureSpawnEnemyId))
                throw new ArgumentException(
                    "Brood signatures require a spawn enemy id.",
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
            MovementTelegraphTicks = movementTelegraphTicks;
            HpThresholdNumerator = hpThresholdNumerator;
            HpThresholdDenominator = hpThresholdDenominator;
            int ruleCount = partRules == null ? 0 : partRules.Count;
            var rules = new BossPhasePartRule[ruleCount];
            for (int i = 0; i < rules.Length; i++)
            {
                BossPhasePartRule rule = partRules[i]
                    ?? throw new ArgumentException(
                        "Boss phase part rules cannot contain null.",
                        nameof(partRules));
                for (int previous = 0; previous < i; previous++)
                    if (string.Equals(
                            rules[previous].PartId,
                            rule.PartId,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate boss phase part rule '{rule.PartId}'.",
                            nameof(partRules));
                rules[i] = rule;
            }
            PartRules = new ReadOnlyCollection<BossPhasePartRule>(rules);
            SegmentChain = segmentChain;
        }

        public int FireIntervalTicks { get; }
        public int Ways { get; }
        public int BulletSpeedNumerator { get; }
        public int BulletSpeedDenominator { get; }
        public BossMovementPattern MovementPattern { get; }
        public int MovementAmplitudeNumerator { get; }
        public int MovementAmplitudeDenominator { get; }
        public int MovementPeriodTicks { get; }
        /// <summary>
        /// Movement-only warning window. LungeReturn remains at its hold
        /// position for this many ticks at the start of every movement cycle.
        /// </summary>
        public int MovementTelegraphTicks { get; }
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
        /// <summary>
        /// Exact remaining-HP threshold for entering this phase. A zero
        /// numerator opts into legacy equal HP partitions.
        /// </summary>
        public int HpThresholdNumerator { get; }
        public int HpThresholdDenominator { get; }
        public bool HasHpThreshold => HpThresholdNumerator > 0;
        public IReadOnlyList<BossPhasePartRule> PartRules { get; }
        /// <summary>Optional chain-minion schedule owned by this phase.</summary>
        public SegmentChainDefinition SegmentChain { get; }
    }

    public enum BossPartAttackType
    {
        None = 0,
        AimedSpread = 1,
        RadialSpread = 2,
        MeleeCharge = 3,
        VerticalMovement = 4,
        SpawnEnemy = 5,
        Suction = 6,
        Laser = 7
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
            int contactDamage,
            Simulation.LaserAttackDefinition laserAttack = null,
            int effectMaxSpeedNumerator = 0,
            int effectMaxSpeedDenominator = 1,
            int effectOffsetX = 0,
            int effectOffsetY = 0)
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
            if (effectMaxSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(effectMaxSpeedNumerator));
            if (effectMaxSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(effectMaxSpeedDenominator));
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
            if (type == BossPartAttackType.Laser
                && (intervalTicks < 1 || laserAttack == null))
                throw new ArgumentException(
                    "Laser attacks require an interval and laser profile.");
            if (type == BossPartAttackType.Laser
                && intervalTicks != laserAttack.CycleIntervalTicks)
                throw new ArgumentException(
                    "Laser attack interval must match its laser cycle interval.",
                    nameof(intervalTicks));
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
            if (type != BossPartAttackType.Laser && laserAttack != null)
                throw new ArgumentException(
                    "Only laser attacks may specify a laser profile.",
                    nameof(laserAttack));
            if (type != BossPartAttackType.Suction
                && effectMaxSpeedNumerator != 0)
                throw new ArgumentException(
                    "Only suction attacks may specify an effect speed cap.",
                    nameof(effectMaxSpeedNumerator));
            if (type != BossPartAttackType.Suction
                && (effectOffsetX != 0 || effectOffsetY != 0))
                throw new ArgumentException(
                    "Only suction attacks may specify an effect offset.",
                    nameof(effectOffsetX));

            Type = type;
            IntervalTicks = intervalTicks;
            Ways = ways;
            BulletSpeedNumerator = bulletSpeedNumerator;
            BulletSpeedDenominator = bulletSpeedDenominator;
            EffectSpeedNumerator = effectSpeedNumerator;
            EffectSpeedDenominator = effectSpeedDenominator;
            EffectMaxSpeedNumerator =
                type == BossPartAttackType.Suction
                    && effectMaxSpeedNumerator == 0
                    ? int.MaxValue
                    : effectMaxSpeedNumerator;
            EffectMaxSpeedDenominator =
                type == BossPartAttackType.Suction
                    && effectMaxSpeedNumerator == 0
                    ? 1
                    : effectMaxSpeedDenominator;
            EffectOffsetX = effectOffsetX;
            EffectOffsetY = effectOffsetY;
            SpawnEnemyId = spawnEnemyId;
            ContactDamage = contactDamage;
            LaserAttack = laserAttack;
        }

        public BossPartAttackType Type { get; }
        public int IntervalTicks { get; }
        public int Ways { get; }
        public int BulletSpeedNumerator { get; }
        public int BulletSpeedDenominator { get; }
        public int EffectSpeedNumerator { get; }
        public int EffectSpeedDenominator { get; }
        /// <summary>
        /// Exact per-tick cap for suction displacement. Missing legacy data
        /// leaves this effectively unbounded.
        /// </summary>
        public int EffectMaxSpeedNumerator { get; }
        public int EffectMaxSpeedDenominator { get; }
        /// <summary>Source offset relative to the owning boss part.</summary>
        public int EffectOffsetX { get; }
        public int EffectOffsetY { get; }
        public string SpawnEnemyId { get; }
        public int ContactDamage { get; }
        public Simulation.LaserAttackDefinition LaserAttack { get; }
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

    /// <summary>
    /// Optional replacement body spawned after the first boss form is defeated.
    /// TransitionTicks is an invulnerable presentation window with no active body.
    /// </summary>
    public sealed class BossFormDefinition
    {
        public BossFormDefinition(
            string formId,
            int transitionTicks,
            int maxHp,
            int halfWidth,
            int halfHeight,
            int holdX,
            IReadOnlyList<BossPhase> phases,
            IReadOnlyList<BossPartDefinition> parts = null)
        {
            if (string.IsNullOrEmpty(formId))
                throw new ArgumentException(
                    "Boss form id cannot be null or empty.", nameof(formId));
            if (transitionTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(transitionTicks));
            if (maxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (halfWidth < 1)
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfHeight < 1)
                throw new ArgumentOutOfRangeException(nameof(halfHeight));

            FormId = formId;
            TransitionTicks = transitionTicks;
            MaxHp = maxHp;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            HoldX = holdX;
            Phases = CopyPhases(phases);
            Parts = CopyParts(parts);
            ValidatePartsAndPhases(Parts, Phases, MaxHp);
        }

        public string FormId { get; }
        public int TransitionTicks { get; }
        public int MaxHp { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int HoldX { get; }
        public IReadOnlyList<BossPhase> Phases { get; }
        public IReadOnlyList<BossPartDefinition> Parts { get; }

        static IReadOnlyList<BossPhase> CopyPhases(
            IReadOnlyList<BossPhase> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException(
                    "A boss form requires at least one phase.", nameof(source));
            var copy = new BossPhase[source.Count];
            for (int i = 0; i < copy.Length; i++)
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

        internal static void ValidatePartsAndPhases(
            IReadOnlyList<BossPartDefinition> parts,
            IReadOnlyList<BossPhase> phases,
            int maxHp)
        {
            int coreCount = 0;
            long totalHp = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                BossPartDefinition part = parts[i];
                totalHp += part.MaxHp;
                if (part.IsCore)
                    coreCount++;
                for (int previous = 0; previous < i; previous++)
                    if (string.Equals(
                            parts[previous].PartId,
                            part.PartId,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate boss part id '{part.PartId}'.");
            }
            if (parts.Count != 0 && coreCount != 1)
                throw new ArgumentException(
                    "A multipart boss requires exactly one core.");
            if (parts.Count != 0 && totalHp != maxHp)
                throw new ArgumentException(
                    "Multipart boss HP must equal the sum of its part HP.");
            for (int i = 0; i < parts.Count; i++)
            {
                BossPartDefinition part = parts[i];
                for (int gate = 0;
                    gate < part.CoreGatePartIds.Count;
                    gate++)
                {
                    bool found = false;
                    for (int candidate = 0;
                        candidate < parts.Count;
                        candidate++)
                        if (candidate != i
                            && string.Equals(
                                parts[candidate].PartId,
                                part.CoreGatePartIds[gate],
                                StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    if (!found)
                        throw new ArgumentException(
                            "Boss core gate references unknown part "
                            + $"'{part.CoreGatePartIds[gate]}'.");
                }
            }

            bool explicitThresholds = phases.Count > 1
                && phases[1].HasHpThreshold;
            bool timed = phases.Count > 0
                && phases[0].DurationTicks > 0;
            for (int phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
            {
                BossPhase phase = phases[phaseIndex];
                if ((phase.DurationTicks > 0) != timed)
                    throw new ArgumentException(
                        "Boss phases cannot mix timed and HP-based progression.");
                if (timed && phase.HasHpThreshold)
                    throw new ArgumentException(
                        "Timed boss phases cannot define HP thresholds.");
                if (phaseIndex == 0
                    && phase.HasHpThreshold
                    && phase.HpThresholdNumerator
                        != phase.HpThresholdDenominator)
                    throw new ArgumentException(
                        "The first boss phase HP threshold must be one when present.");
                if (phaseIndex > 0
                    && phase.HasHpThreshold != explicitThresholds)
                    throw new ArgumentException(
                        "HP-based boss phases cannot mix explicit and equal thresholds.");
                if (explicitThresholds
                    && phaseIndex == 1
                    && phase.HpThresholdNumerator
                        >= phase.HpThresholdDenominator)
                    throw new ArgumentException(
                        "The first boss transition HP threshold must be below one.");
                if (explicitThresholds && phaseIndex > 1)
                {
                    BossPhase previous = phases[phaseIndex - 1];
                    if ((long)phase.HpThresholdNumerator
                            * previous.HpThresholdDenominator
                        >= (long)previous.HpThresholdNumerator
                            * phase.HpThresholdDenominator)
                        throw new ArgumentException(
                            "Boss phase HP thresholds must strictly decrease.");
                }
                for (int ruleIndex = 0;
                    ruleIndex < phase.PartRules.Count;
                    ruleIndex++)
                {
                    string partId = phase.PartRules[ruleIndex].PartId;
                    bool found = false;
                    for (int partIndex = 0;
                        partIndex < parts.Count;
                        partIndex++)
                        if (string.Equals(
                                parts[partIndex].PartId,
                                partId,
                                StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    if (!found)
                        throw new ArgumentException(
                            $"Boss phase references unknown part '{partId}'.");
                }
            }
        }
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
            StageGimmickDefinition gimmick = null,
            WarshipEncounterDefinition warshipEncounter = null,
            BossFormDefinition form2 = null)
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
            BossFormDefinition.ValidatePartsAndPhases(
                BossParts,
                BossPhases,
                BossMaxHp);
            ThemeId = themeId;
            RequestedThemeId = requestedThemeId;
            EncounterType = encounterType;
            Gimmick = gimmick ?? StageGimmickDefinition.None;
            WarshipEncounter = warshipEncounter;
            Form2 = form2;
            if (WarshipEncounter != null && BossParts.Count == 0)
                throw new ArgumentException(
                    "A warship encounter requires multipart boss parts.",
                    nameof(warshipEncounter));
            // Warship + form2 is allowed (REQ-139): hull completion hands off to
            // the robot second form via BeginWarshipFormTransition.
            WarshipEncounter?.ValidateParts(BossParts);
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
        public WarshipEncounterDefinition WarshipEncounter { get; }
        public BossFormDefinition Form2 { get; }
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
            SegmentEnvironmentDefinition environment = null,
            int scrollSpeedMultiplierNumerator = 1,
            int scrollSpeedMultiplierDenominator = 1)
        {
            if (scrollSpeedMultiplierNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(scrollSpeedMultiplierNumerator));
            if (scrollSpeedMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(scrollSpeedMultiplierDenominator));
            SegmentId = segmentId ?? throw new ArgumentNullException(nameof(segmentId));
            LengthTicks = lengthTicks;
            Spawns = CopySpawns(spawns);
            EntryLaneMask = entryLaneMask;
            ExitLaneMask = exitLaneMask;
            TraversableLaneMasks = CopyMasks(traversableLaneMasks);
            Obstacles = CopyObstacles(obstacles);
            Environment =
                environment ?? SegmentEnvironmentDefinition.None;
            ScrollSpeedMultiplierNumerator =
                scrollSpeedMultiplierNumerator;
            ScrollSpeedMultiplierDenominator =
                scrollSpeedMultiplierDenominator;
        }

        public string SegmentId { get; }
        public int LengthTicks { get; }
        public IReadOnlyList<SpawnEvent> Spawns { get; }
        public int EntryLaneMask { get; }
        public int ExitLaneMask { get; }
        public IReadOnlyList<int> TraversableLaneMasks { get; }
        public IReadOnlyList<ObstacleSpawn> Obstacles { get; }
        public SegmentEnvironmentDefinition Environment { get; }
        public int ScrollSpeedMultiplierNumerator { get; }
        public int ScrollSpeedMultiplierDenominator { get; }

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
            : this(type, x, y, hp, null, false, 0)
        {
        }

        public ObstacleSpawn(
            ObstacleType type,
            int x,
            int y,
            int hp,
            Simulation.LaserAttackDefinition laserAttack)
            : this(type, x, y, hp, laserAttack, false, 0)
        {
        }

        public ObstacleSpawn(
            ObstacleType type,
            int x,
            int y,
            int hp,
            Simulation.LaserAttackDefinition laserAttack,
            bool blocksEnemyBullets,
            int regenDelayTicks)
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
            if (regenDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(regenDelayTicks));
            if (regenDelayTicks > 0 && type != ObstacleType.Breakable)
                throw new ArgumentException(
                    "Only breakable obstacles can regenerate.",
                    nameof(regenDelayTicks));

            Type = type;
            X = x;
            Y = y;
            Hp = hp;
            LaserAttack = laserAttack;
            BlocksEnemyBullets = blocksEnemyBullets;
            RegenDelayTicks = regenDelayTicks;
        }

        public ObstacleType Type { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public Simulation.LaserAttackDefinition LaserAttack { get; }
        public bool BlocksEnemyBullets { get; }
        public int RegenDelayTicks { get; }
    }
}
