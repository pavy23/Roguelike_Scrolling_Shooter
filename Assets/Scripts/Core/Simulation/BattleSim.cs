using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    /// <summary>Deterministic integer-only combat and generated-stage simulation.</summary>
    public sealed partial class BattleSim : IBattleSim
    {
        const int DropRngStream = 1;
        const int BombDropRngStream = 2;
        const int BossPatternRngStream = 3;
        const int SineScale = 1024;
        const int CapsuleMagnetDirectionScale = 1024;
        const long MaxSquareRoot = 3037000499L;
        // 회전 전 조준 벡터를 이 범위로 축소하면 회전 후 제곱합과
        // speedNumerator 곱이 모두 long 범위에 머문다.
        const long MaxAimComponentBeforeRotation = 1L << 29;

        static readonly int[] SineLut =
        {
            0, 100, 200, 297, 392, 483, 569, 650,
            724, 790, 851, 903, 946, 980, 1004, 1019,
            1024, 1019, 1004, 980, 946, 903, 851, 790,
            724, 650, 569, 483, 392, 297, 200, 100,
            0, -100, -200, -297, -392, -483, -569, -650,
            -724, -790, -851, -903, -946, -980, -1004, -1019,
            -1024, -1019, -1004, -980, -946, -903, -851, -790,
            -724, -650, -569, -483, -392, -297, -200, -100
        };
        int _playerSpeedNumerator, _playerSpeedDenominator;
        int _bulletSpeedNumerator, _bulletSpeedDenominator;
        int _fireIntervalTicks;
        readonly int _maxBullets, _maxEnemies;
        readonly BattleContent _battleContent;
        readonly int _mainShotEffectSoftCap;
        readonly int _missileEffectSoftCap;
        readonly int _optionEffectSoftCap;
        readonly int _shieldEffectSoftCap;
        WeaponType _playerWeaponType;
        int _mainShotBasePierceEnemyCount;
        int _spreadWays, _spreadStepLutSlots;
        int[] _mainShotAngleLutSlots;
        int _primaryWeaponEvolutionLevel;
        int _burstCount = 1, _burstIntervalTicks = 1;
        int _burstShotsRemaining, _burstCooldownTicks;
        int _pulseMinStepLutSlots, _pulseMaxStepLutSlots;
        int _pulsePeriodTicks, _inertiaVelocityPercent;
        int _impactExplosionDamage, _impactExplosionRadius;
        int _beamDamagePerTick, _beamLength;
        int _beamStartHalfWidth, _beamGrowthPerTick;
        int _beamMaxHalfWidth, _playerBeamAge;
        int _playerVelocityX, _playerVelocityY;
        int _mainShotRapidFireStartLevel;
        int _mainShotFireIntervalReductionPerLevel;
        int _mainShotMinimumFireIntervalTicks;
        readonly int _missileBaseDamage;
        readonly int _missileDamageGrowthPercentPerLevel;
        readonly int _optionMissileDamagePercent;
        readonly int _missileFireIntervalTicks, _missileRapidFireStartLevel;
        readonly int _missileFireIntervalReductionPerLevel, _missileMinimumFireIntervalTicks;
        readonly int _missileSpeedXNumerator, _missileSpeedXDenominator;
        readonly int _missileFallSpeedYNumerator, _missileFallSpeedYDenominator;
        readonly int _missileHalfWidth, _missileHalfHeight;
        readonly MissileFamily _missileFamily;
        readonly int _missilePierceEnemyCount;
        readonly int _missileExplosionDamage;
        readonly int _missileExplosionRadiusSubUnits;
        readonly int _missileExplosionMaxTargets;
        readonly int _missileDropDelayTicks;
        readonly int _optionFollowDelayTicks;
        readonly OptionFormation _optionFormation;
        readonly int[] _optionFixedOffsetXs, _optionFixedOffsetYs;
        readonly int _optionOrbitRadiusSubUnits;
        readonly int _optionOrbitAngularLutSlotsNumerator;
        readonly int _optionOrbitAngularLutSlotsDenominator;
        readonly int _playerMinX, _playerMaxX, _playerMinY, _playerMaxY;
        readonly int _bulletDespawnX, _enemyDespawnX;
        readonly int _playerHalfWidth, _playerHalfHeight;
        readonly int _capsuleHalfWidth, _capsuleHalfHeight;
        readonly int _capsuleNoDropWeight;
        readonly int _capsuleDropWeightReduction;
        readonly int _capsuleMagnetRadiusSubUnits;
        readonly int _capsuleMagnetSpeedNumerator;
        readonly int _capsuleMagnetSpeedDenominator;
        readonly int _scrollSpeedNumerator, _scrollSpeedDenominator;
        readonly long[] _segmentScrollStartOffsets;
        readonly int[] _segmentScrollSpeedNumerators;
        readonly int[] _segmentScrollSpeedDenominators;
        readonly int _stageScrollTicks;
        readonly long _stageScrollEndOffset;
        readonly bool _usesSegmentScrollMultipliers;
        readonly int _maxObstacles, _obstacleHalfWidth, _obstacleHalfHeight;

        /// <summary>
        /// 이 장애물의 실제 반폭. 장애물이 자기 크기를 들고 있으면 그것을,
        /// 아니면 설정 기본값을 쓴다. **판정과 연출이 같은 값을 봐야 한다** —
        /// 크기를 데이터로 열면서 한쪽만 고치면 "안 맞았는데 맞는" 판정이 생긴다.
        /// </summary>
        int ObstacleHalfWidthOf(in ObstacleState obstacle) =>
            obstacle.HalfWidth > 0 ? obstacle.HalfWidth : _obstacleHalfWidth;

        int ObstacleHalfHeightOf(in ObstacleState obstacle) =>
            obstacle.HalfHeight > 0 ? obstacle.HalfHeight : _obstacleHalfHeight;
        readonly int _obstacleContactDamage, _breakableObstacleScore;
        readonly int _bossDamageScorePerHundred;
        readonly int _enemyHpMultiplierNumerator;
        readonly int _enemyHpMultiplierDenominator;
        readonly int _encounterEnemyHpMultiplierNumerator;
        readonly int _encounterEnemyHpMultiplierDenominator;
        readonly int _capsuleDropMultiplierNumerator;
        readonly int _capsuleDropMultiplierDenominator;
        readonly int _contractCapsuleDropMultiplierNumerator;
        readonly int _contractCapsuleDropMultiplierDenominator;
        readonly int _encounterScoreMultiplierNumerator;
        readonly int _encounterScoreMultiplierDenominator;
        readonly int _contractScoreMultiplierNumerator;
        readonly int _contractScoreMultiplierDenominator;
        int _playerBulletDamage;
        int _playerBulletHalfWidth, _playerBulletHalfHeight;
        readonly PowerUpGauge _powerUpGauge;
        readonly Rng _dropRng;
        readonly Rng _bombDropRng;
        readonly Rng _bossPatternRng;
        readonly List<BulletState> _bullets;
        // 탄별 보조 상태 (잔여분·속도·관통·거동). BulletAux 참조.
        readonly BulletAuxList _bulletAux;
        readonly int[] _bulletHitRecordBulletIds;
        readonly int[] _bulletHitRecordEnemyIds;
        readonly ReadOnlyCollection<BulletState> _readOnlyBullets;
        readonly List<OptionState> _options;
        readonly ReadOnlyCollection<OptionState> _readOnlyOptions;
        readonly int[] _playerHistoryX;
        readonly int[] _playerHistoryY;
        readonly List<EnemyState> _enemies;
        readonly List<EnemyDefinition> _enemyDefinitions;
        readonly List<int> _enemyXRemainders;
        readonly List<int> _enemySpawnYs;
        readonly List<int> _enemyAges;
        readonly List<int> _enemyDiveTargetYs;
        readonly List<byte> _enemyMovementFlags;
        readonly ReadOnlyCollection<EnemyState> _readOnlyEnemies;
        readonly List<SegmentChainRuntime> _segmentChainRuntimes;
        readonly List<SegmentChainState> _segmentChainStates;
        readonly ReadOnlyCollection<SegmentChainState>
            _readOnlySegmentChains;
        readonly List<ObstacleState> _obstacles;
        readonly List<int> _obstacleAges;
        readonly List<LaserAttackDefinition> _obstacleLaserAttacks;
        readonly List<bool> _obstacleBlocksEnemyBullets;
        readonly List<int> _obstacleRegenDelayTicks;
        readonly List<int> _obstacleMaxHps;
        readonly List<ObstacleRegenerationState> _pendingObstacleRegens;
        readonly ReadOnlyCollection<ObstacleRegenerationState>
            _readOnlyPendingObstacleRegens;
        readonly List<long> _obstacleMotionXRemainders;
        readonly List<long> _obstacleMotionYRemainders;
        readonly List<long> _obstacleVelocityXNumerators;
        readonly List<long> _obstacleVelocityYNumerators;
        readonly List<long> _obstacleVelocityDenominators;
        readonly List<long> _obstacleGravityNumerators;
        readonly List<long> _obstacleGravityDenominators;
        readonly ReadOnlyCollection<ObstacleState> _readOnlyObstacles;
        readonly List<CapsuleState> _capsules;
        readonly List<long> _capsuleMagnetXRemainders;
        readonly List<long> _capsuleMagnetYRemainders;
        readonly ReadOnlyCollection<CapsuleState> _readOnlyCapsules;
        readonly List<BombPickupState> _bombPickups;
        readonly List<long> _bombPickupMagnetXRemainders;
        readonly List<long> _bombPickupMagnetYRemainders;
        readonly ReadOnlyCollection<BombPickupState> _readOnlyBombPickups;
        readonly List<LaserState> _lasers;
        readonly List<LaserAttackDefinition> _laserDefinitions;
        readonly List<int> _laserAges;
        readonly ReadOnlyCollection<LaserState> _readOnlyLasers;
        readonly ScheduledSpawn[] _scheduledSpawns;
        readonly ScheduledObstacle[] _scheduledObstacles;
        readonly IReadOnlyList<StageSegment> _stageSegments;
        readonly int[] _segmentStartTicks;
        readonly bool _visionObscured;
        readonly int _timeLimitTicks;

        // 적탄 설정 (config 스냅숏)
        readonly int _enemyBulletSpeedNumerator, _enemyBulletSpeedDenominator;
        readonly int _enemyBulletHalfWidth, _enemyBulletHalfHeight;
        readonly int _enemyBulletDamage, _maxEnemyBullets;
        int _maxShieldStock;
        readonly int _playerHitInvulnerabilityTicks;
        readonly bool _playerInvulnerable;
        int _maxBombStock;
        readonly int _bombInvulnerabilityTicks;
        readonly int _bombEffectRadiusSubUnits;
        readonly int _bombRegularEnemyDamage;
        readonly int _bombBossDamageCap, _bombBossPartDamageCap;
        readonly int _bombNoDropWeight, _maxBombPickups, _maxLasers;
        readonly int _bombDropMultiplierNumerator;
        readonly int _bombDropMultiplierDenominator;
        readonly bool _contractGuaranteesBombDrop;
        readonly BattleModifier _activeModifiers;
        readonly int _pierceShotEnemyCount, _ricochetRangeSubUnits;
        readonly int _ricochetCount;
        readonly int _homingMissileTurnLutSlotsPerTick;
        readonly int _enemyHomingDurationTicks;
        readonly int _killExplosionRadiusSubUnits, _killExplosionDamage;
        readonly int _killExplosionMaxTargets;
        readonly int _grazeExtraRadiusSubUnits, _grazeScore;
        readonly int _killComboGaugeGain;
        readonly int _comboDecayTicks;
        readonly int[] _comboGaugeRequirements;
        readonly int[] _comboMultipliers;
        readonly int _shieldBonusScorePerStock;

        // 보스 (REQ-007). _bossMaxHp == 0 이면 이 스테이지에 보스전 없음.
        int _bossMaxHp, _bossRuntimeMaxHp;
        int _bossHalfWidth, _bossHalfHeight, _bossHoldX;
        int _bossSpawnX;
        readonly int _fieldCleanupStartTick;
        readonly bool _preparesBossRoomBoundary;
        IReadOnlyList<Generation.BossPhase> _bossPhases;
        IReadOnlyList<BossPartDefinition> _bossPartDefinitions;
        BossPartState[] _bossPartStates;
        ReadOnlyCollection<BossPartState> _readOnlyBossParts;
        int[] _bossPartFireCooldowns;
        /// <summary>Independent cycle timer for BossPartAttackProfile.SecondaryLaser.</summary>
        int[] _bossPartSecondaryLaserCooldowns;
        /// <summary>Independent cycle timer for BossPartAttackProfile.SecondaryBurst.</summary>
        int[] _bossPartSecondaryBurstCooldowns;
        int[] _bossPartRegenerationRemaining;
        bool[] _bossPartsEverDestroyed;
        bool[] _bossPartContactHitThisCycle;
        EnemyDefinition[] _bossPartSpawnDefinitions;
        readonly WarshipEncounterDefinition _warshipDefinition;
        readonly IReadOnlyList<BossPartDefinition>
            _warshipRuntimePartDefinitions;
        WarshipEncounter _warshipEncounter;
        int _warshipEventCursor;
        readonly int _stageTotalTicks;
        bool _bossSpawned, _bossDefeated;
        readonly bool _isMidBossBattle;
        int _bossId, _bossX, _bossY, _bossHp, _bossPhase, _bossAge, _bossFireCooldown;
        int _bossPhaseAge;
        /// <summary>때릴 수 있는 파츠가 하나도 없는 상태가 이어진 틱 수.</summary>
        int _bossNothingDamageableTicks;
        int _bossMovementAnchorY;
        int _bossMovementPhaseOffsetTicks;
        int _bossMovementTransitionOffsetX;
        int _bossMovementTransitionOffsetY;
        int _bossVelocityX;
        int _bossVelocityY;
        bool _bossPhaseTelegraphPending;
        bool _bossBurstAwaitingVolley;
        int _bossPatternVolleyIndex;
        bool _bossUsesTimedPattern;
        long _bossSuctionAccelerationXRemainder;
        long _bossSuctionAccelerationYRemainder;
        int _bossSuctionDeltaX, _bossSuctionDeltaY;
        int _bossSuctionPartIndex = -1;
        int _bossSuctionSourceX, _bossSuctionSourceY;
        bool _bossSuctionActive;
        string _bossSuctionPartId;
        int _segmentChainSummonsRemaining;
        int _segmentChainSummonCooldown;
        int _bossFormIndex;
        int _bossTransitionTicksRemaining;
        readonly BossFormDefinition _bossForm2;

        const int BossHoverAmplitude = 3 * SimSpace.SubUnitsPerWorldUnit;
        const int BossGlideSpeedPerTick = 64;
        public const int BossSpawnSuppressionLeadTicks = 40;
        public const int RoomBoundaryCleanupLeadTicks = 60;
        /// <summary>
        /// Deterministic upper bound for draining a regular room before a boss.
        /// A malformed or stationary transient must never stall progression forever.
        /// </summary>
        public const int RoomBoundaryMaximumWaitTicks = 300;
        const int BossMovementRecenterTicks = 30;
        const int BossRetreatSpeedPerTick = 2 * SimSpace.SubUnitsPerWorldUnit;
        const byte EnemyMovementDiveTargetLocked = 1;
        const byte EnemyMovementBossRetreat = 2;
        const int BossHoverPeriodShift = 2;                            // age >> 2 → 약 4.3초 주기
        const int SpreadStepLutSlots = 2;                              // n-way 간격 = 11.25°
        const int SpiralStepLutSlots = 2;
        const int SplitterStepLutSlots = 4;
        const int HeavyCollisionScalePercent = 250;

        readonly SimEvent[] _events;
        readonly int[] _enemyScanIds;
        readonly long[] _enemyScanDistances;
        int _eventCount;
        long _shotsFired, _shotsHit, _kills, _capsulesCollected, _grazeCount;
        long _bombsUsed, _hitsTaken;

        long _playerXRemainder, _playerYRemainder;
        readonly long _scrollBaseOffset;
        long _driftXRemainder, _driftYRemainder;
        StageEnvironmentState _environment;
        int _currentEnvironmentSegmentIndex = -1;
        bool _timeLimitExpired;
        int _cooldown, _missileCooldown;
        int _mainShotLevel, _missileLevel, _optionLevel, _shieldGaugeLevel;
        int _speedGaugeLevel;
        PrimaryWeaponFamily _equippedPrimaryWeaponFamily;
        int _nextBulletId = 1;
        int _nextEnemyId = 1;
        int _nextObstacleId = 1;
        int _nextCapsuleId = 1;
        int _nextBombPickupId = 1;
        int _nextLaserId = 1;
        int _nextScheduledSpawn;
        int _nextScheduledObstacle;
        int _playerHistoryHead;
        int _playerHistoryCount;
        int _bulletHitRecordCount;
        int _multiplierLevel, _comboGauge, _ticksSinceLastComboAction;
        // REQ-133: 100 데미지 단위로 점수를 줄 때 남는 자투리. 방마다 초기화된다.
        int _bossDamageScoreCarry;

        /// <summary>마지막 그레이즈 승급 이후 경과 틱. 동시 다발 스침을 한 단계로 묶는다.</summary>
        int _ticksSinceGrazeLevelUp = int.MaxValue / 2;

        /// <summary>그레이즈 승급 쿨다운(틱). 0.5초 — 탄막 한 겹을 지나가는 시간이다.</summary>
        const int GrazeLevelUpCooldownTicks = 30;
        bool _comboActionThisTick, _activateHeld, _bombHeld, _playerAlive;
        bool _runClearShieldBonusAwarded;
        int _playerInvulnerabilityTicksRemaining;

        /// <summary>Backward-compatible stage-less player movement and basic-shot simulation.</summary>
        public BattleSim(BattleSimConfig config, Rng rng)
            : this(
                config,
                rng,
                null,
                null,
                null,
                BattleModifierStackSet.FromFlags(
                    BattleModifier.None,
                    4),
                false,
                null,
                false,
                false)
        {
        }

        /// <summary>Stage-enabled simulation using immutable Core content definitions.</summary>
        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge)
            : this(
                config,
                rng,
                stagePlan,
                content,
                powerUpGauge,
                BattleModifierStackSet.FromFlags(
                    BattleModifier.None,
                    4),
                true,
                null,
                false,
                false)
        {
        }

        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifier activeModifiers)
            : this(
                config,
                rng,
                stagePlan,
                content,
                powerUpGauge,
                BattleModifierStackSet.FromFlags(
                    activeModifiers,
                    4),
                true,
                null,
                false,
                false)
        {
        }

        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifierStackSet modifierStacks)
            : this(
                config,
                rng,
                stagePlan,
                content,
                powerUpGauge,
                modifierStacks,
                true,
                null,
                false,
                false)
        {
        }

        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifierStackSet modifierStacks,
            BattleContinuityState continuityState,
            bool preparesBossRoomBoundary = false,
            bool isMidBossBattle = false)
            : this(
                config,
                rng,
                stagePlan,
                content,
                powerUpGauge,
                modifierStacks,
                true,
                continuityState,
                preparesBossRoomBoundary,
                isMidBossBattle)
        {
        }

        BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifierStackSet modifierStacks,
            bool stageEnabled,
            BattleContinuityState continuityState,
            bool preparesBossRoomBoundary,
            bool isMidBossBattle)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (stageEnabled && stagePlan == null) throw new ArgumentNullException(nameof(stagePlan));
            if (stageEnabled && content == null) throw new ArgumentNullException(nameof(content));
            if (stageEnabled && powerUpGauge == null) throw new ArgumentNullException(nameof(powerUpGauge));
            if (modifierStacks == null)
                throw new ArgumentNullException(nameof(modifierStacks));
            Validate(config);
            BattleModifier activeModifiers =
                modifierStacks.ActiveModifiers;
            if ((activeModifiers & ~BattleModifierRules.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(activeModifiers));

            _playerSpeedNumerator = config.PlayerSpeedNumerator;
            _playerSpeedDenominator = config.PlayerSpeedDenominator;
            _maxBullets = config.MaxBullets;
            _maxEnemies = config.MaxEnemies;
            _battleContent = content;
            _mainShotEffectSoftCap = ResolveEffectSoftCap(
                content, powerUpGauge, PowerUpSlot.MainShot);
            _missileEffectSoftCap = ResolveEffectSoftCap(
                content, powerUpGauge, PowerUpSlot.Missile);
            _optionEffectSoftCap = ResolveEffectSoftCap(
                content, powerUpGauge, PowerUpSlot.Option);
            _shieldEffectSoftCap = ResolveEffectSoftCap(
                content, powerUpGauge, PowerUpSlot.Shield);
            _playerWeaponType = config.PlayerWeaponType;
            _equippedPrimaryWeaponFamily =
                config.PlayerWeaponFamily
                ?? PrimaryWeaponFamilyFor(config.PlayerWeaponType);
            _mainShotBasePierceEnemyCount =
                _playerWeaponType == WeaponType.Laser
                    ? config.LaserPierceEnemyCount
                    : 0;
            _spreadWays = config.SpreadWays;
            _spreadStepLutSlots = config.SpreadStepLutSlots;
            _mainShotAngleLutSlots =
                config.MainShotAngleLutSlots == null
                    ? Array.Empty<int>()
                    : (int[])config.MainShotAngleLutSlots.Clone();
            bool useLaserProfile =
                !config.UseConfiguredMainShotStats
                && _playerWeaponType == WeaponType.Laser;
            bool useSpreadProfile =
                !config.UseConfiguredMainShotStats
                && _playerWeaponType == WeaponType.Spread;
            _mainShotRapidFireStartLevel = useLaserProfile
                ? config.LaserRapidFireStartLevel
                : useSpreadProfile
                    ? config.SpreadRapidFireStartLevel
                    : config.MainShotRapidFireStartLevel;
            _mainShotFireIntervalReductionPerLevel = useLaserProfile
                ? config.LaserFireIntervalReductionPerLevel
                : useSpreadProfile
                    ? config.SpreadFireIntervalReductionPerLevel
                    : config.MainShotFireIntervalReductionPerLevel;
            _mainShotMinimumFireIntervalTicks = useLaserProfile
                ? config.LaserMinimumFireIntervalTicks
                : useSpreadProfile
                    ? config.SpreadMinimumFireIntervalTicks
                    : config.MainShotMinimumFireIntervalTicks;
            _missileBaseDamage = config.MissileBaseDamage;
            _missileDamageGrowthPercentPerLevel =
                config.MissileDamageGrowthPercentPerLevel;
            _optionMissileDamagePercent =
                config.OptionMissileDamagePercent;
            _missileFireIntervalTicks = config.MissileFireIntervalTicks;
            _missileRapidFireStartLevel = config.MissileRapidFireStartLevel;
            _missileFireIntervalReductionPerLevel =
                config.MissileFireIntervalReductionPerLevel;
            _missileMinimumFireIntervalTicks = config.MissileMinimumFireIntervalTicks;
            _missileSpeedXNumerator = config.MissileSpeedXNumerator;
            _missileSpeedXDenominator = config.MissileSpeedXDenominator;
            _missileFallSpeedYNumerator = config.MissileFallSpeedYNumerator;
            _missileFallSpeedYDenominator = config.MissileFallSpeedYDenominator;
            _missileHalfWidth = config.MissileHalfWidth;
            _missileHalfHeight = config.MissileHalfHeight;
            _missileFamily = config.MissileFamily;
            _missilePierceEnemyCount =
                config.MissilePierceEnemyCount;
            _missileExplosionDamage =
                config.MissileExplosionDamage;
            _missileExplosionRadiusSubUnits =
                config.MissileExplosionRadiusSubUnits;
            _missileExplosionMaxTargets =
                config.MissileExplosionMaxTargets;
            _missileDropDelayTicks =
                config.MissileDropDelayTicks;
            _optionFollowDelayTicks = config.OptionFollowDelayTicks;
            _optionFormation = config.OptionFormation;
            _optionFixedOffsetXs = config.OptionFixedOffsetXs == null
                ? Array.Empty<int>()
                : (int[])config.OptionFixedOffsetXs.Clone();
            _optionFixedOffsetYs = config.OptionFixedOffsetYs == null
                ? Array.Empty<int>()
                : (int[])config.OptionFixedOffsetYs.Clone();
            _optionOrbitRadiusSubUnits =
                config.OptionOrbitRadiusSubUnits;
            _optionOrbitAngularLutSlotsNumerator =
                config.OptionOrbitAngularLutSlotsNumerator;
            _optionOrbitAngularLutSlotsDenominator =
                config.OptionOrbitAngularLutSlotsDenominator;
            ValidateLoadoutConfig();
            _playerMinX = config.PlayerMinX;
            _playerMaxX = config.PlayerMaxX;
            _playerHalfWidth = config.PlayerHalfWidth;
            _playerHalfHeight = config.PlayerHalfHeight;
            if (stageEnabled)
            {
                int visibleMinY = SimSpace.GetVisiblePlayerCenterMinY(
                    _playerHalfHeight);
                int visibleMaxY = SimSpace.GetVisiblePlayerCenterMaxY(
                    _playerHalfHeight);
                _playerMinY = Math.Max(
                    visibleMinY,
                    Math.Min(visibleMaxY, config.PlayerMinY));
                _playerMaxY = Math.Max(
                    visibleMinY,
                    Math.Min(visibleMaxY, config.PlayerMaxY));
            }
            else
            {
                _playerMinY = config.PlayerMinY;
                _playerMaxY = config.PlayerMaxY;
            }
            _bulletDespawnX = config.BulletDespawnX;
            _enemyDespawnX = config.EnemyDespawnX;
            _capsuleHalfWidth = config.CapsuleHalfWidth;
            _capsuleHalfHeight = config.CapsuleHalfHeight;
            _capsuleNoDropWeight = config.CapsuleNoDropWeight;
            _capsuleDropWeightReduction =
                config.CapsuleDropWeightReduction;
            _capsuleMagnetRadiusSubUnits =
                config.CapsuleMagnetRadiusSubUnits;
            _capsuleMagnetSpeedNumerator =
                config.CapsuleMagnetSpeedNumerator;
            _capsuleMagnetSpeedDenominator =
                config.CapsuleMagnetSpeedDenominator;
            _scrollSpeedNumerator = config.ScrollSpeedNumerator;
            _scrollSpeedDenominator = config.ScrollSpeedDenominator;
            _isMidBossBattle = stageEnabled && isMidBossBattle;
            _maxObstacles = config.MaxObstacles;
            _obstacleHalfWidth = config.ObstacleHalfWidth;
            _obstacleHalfHeight = config.ObstacleHalfHeight;
            _obstacleContactDamage = config.ObstacleContactDamage;
            _bossDamageScorePerHundred = config.BossDamageScorePerHundred;
            _breakableObstacleScore = config.BreakableObstacleScore;
            _enemyHpMultiplierNumerator =
                config.EnemyHpMultiplierNumerator;
            _enemyHpMultiplierDenominator =
                config.EnemyHpMultiplierDenominator;
            _encounterEnemyHpMultiplierNumerator = stageEnabled
                ? stagePlan.EncounterEnemyHpMultiplierNumerator
                : 1;
            _encounterEnemyHpMultiplierDenominator = stageEnabled
                ? stagePlan.EncounterEnemyHpMultiplierDenominator
                : 1;
            _capsuleDropMultiplierNumerator = stageEnabled
                ? stagePlan.CapsuleDropMultiplierNumerator
                : 1;
            _capsuleDropMultiplierDenominator = stageEnabled
                ? stagePlan.CapsuleDropMultiplierDenominator
                : 1;
            _contractCapsuleDropMultiplierNumerator =
                config.ContractCapsuleDropMultiplierNumerator;
            _contractCapsuleDropMultiplierDenominator =
                config.ContractCapsuleDropMultiplierDenominator;
            _encounterScoreMultiplierNumerator = stageEnabled
                ? stagePlan.EncounterScoreMultiplierNumerator
                : 1;
            _encounterScoreMultiplierDenominator = stageEnabled
                ? stagePlan.EncounterScoreMultiplierDenominator
                : 1;
            _contractScoreMultiplierNumerator =
                config.ContractScoreMultiplierNumerator;
            _contractScoreMultiplierDenominator =
                config.ContractScoreMultiplierDenominator;
            _enemyBulletSpeedNumerator = config.EnemyBulletSpeedNumerator;
            _enemyBulletSpeedDenominator = config.EnemyBulletSpeedDenominator;
            _enemyBulletHalfWidth = config.EnemyBulletHalfWidth;
            _enemyBulletHalfHeight = config.EnemyBulletHalfHeight;
            _enemyBulletDamage = config.EnemyBulletDamage;
            _maxEnemyBullets = config.MaxEnemyBullets;
            _maxShieldStock = config.MaxShieldStock;
            _playerHitInvulnerabilityTicks =
                config.PlayerHitInvulnerabilityTicks;
            _playerInvulnerable = config.PlayerInvulnerable;
            _maxBombStock = config.MaxBombStock;
            _bombInvulnerabilityTicks =
                config.BombInvulnerabilityTicks;
            _bombEffectRadiusSubUnits =
                config.BombEffectRadiusSubUnits;
            _bombRegularEnemyDamage =
                config.BombRegularEnemyDamage;
            _bombBossDamageCap = config.BombBossDamageCap;
            _bombBossPartDamageCap =
                config.BombBossPartDamageCap;
            _bombNoDropWeight = config.BombNoDropWeight;
            _bombDropMultiplierNumerator =
                config.ContractBombDropMultiplierNumerator;
            _bombDropMultiplierDenominator =
                config.ContractBombDropMultiplierDenominator;
            _contractGuaranteesBombDrop =
                config.ContractGuaranteesBombDrop;
            _maxBombPickups = config.MaxBombPickups;
            _maxLasers = config.MaxLasers;
            _activeModifiers = activeModifiers;
            _pierceShotEnemyCount = MultiplySaturated(
                config.PierceShotEnemyCount,
                modifierStacks.GetStrength(
                    BattleModifier.PierceShot));
            _ricochetRangeSubUnits = config.RicochetRangeSubUnits;
            _ricochetCount = modifierStacks.GetStrength(
                BattleModifier.Ricochet);
            int homingStrength = modifierStacks.GetStrength(
                BattleModifier.HomingMissile);
            if (_missileFamily == MissileFamily.Homing)
                homingStrength = 1;
            _enemyHomingDurationTicks =
                Math.Max(1, config.EnemyHomingDurationTicks);
            _homingMissileTurnLutSlotsPerTick =
                Math.Min(
                    SineLut.Length / 2,
                    MultiplySaturated(
                        config.HomingMissileTurnLutSlotsPerTick,
                        homingStrength));
            _killExplosionRadiusSubUnits = config.KillExplosionRadiusSubUnits;
            _killExplosionDamage = config.KillExplosionDamage;
            _killExplosionMaxTargets = MultiplySaturated(
                config.KillExplosionMaxTargets,
                modifierStacks.GetStrength(
                    BattleModifier.KillExplosion));
            _grazeExtraRadiusSubUnits = config.GrazeExtraRadiusSubUnits;
            _grazeScore = config.GrazeScore;
            _killComboGaugeGain = config.KillComboGaugeGain;
            _comboDecayTicks = config.ComboDecayTicks;
            _comboGaugeRequirements =
                (int[])config.ComboGaugeRequirements.Clone();
            _comboMultipliers =
                (int[])config.ComboMultipliers.Clone();
            _shieldBonusScorePerStock =
                config.ShieldBonusScorePerStock;
            _powerUpGauge = powerUpGauge;
            _shieldGaugeLevel = powerUpGauge == null
                ? 0
                : GetEffectivePowerLevel(PowerUpSlot.Shield);
            _dropRng = rng.Fork(DropRngStream);
            _bombDropRng = rng.Fork(BombDropRngStream);
            _bossPatternRng = rng.Fork(BossPatternRngStream);
            _bossForm2 = stageEnabled ? stagePlan.Form2 : null;

            if (stageEnabled && stagePlan.BossMaxHp > 0)
            {
                const int u = SimSpace.SubUnitsPerWorldUnit;
                _bossMaxHp = ScaleEnemyHp(stagePlan.BossMaxHp);
                _bossHalfWidth = stagePlan.BossHalfWidth > 0 ? stagePlan.BossHalfWidth : 3 * u;
                _bossHalfHeight = stagePlan.BossHalfHeight > 0 ? stagePlan.BossHalfHeight : 2 * u;
                _bossHoldX = stagePlan.BossHoldX != 0 ? stagePlan.BossHoldX : 14 * u;
                _bossPhases = stagePlan.BossPhases != null && stagePlan.BossPhases.Count > 0
                    ? stagePlan.BossPhases
                    : new[]
                    {
                        new Generation.BossPhase(
                            50, 3, _enemyBulletSpeedNumerator, _enemyBulletSpeedDenominator)
                    };
                _bossPartDefinitions = stagePlan.BossParts;
            }
            else
            {
                _bossPhases = Array.Empty<Generation.BossPhase>();
                _bossPartDefinitions = Array.Empty<BossPartDefinition>();
            }
            _bossUsesTimedPattern =
                ResolveTimedBossPattern(_bossPhases);
            ValidateBossPhaseRuntimeData();

            _bossPartStates =
                new BossPartState[_bossPartDefinitions.Count];
            _readOnlyBossParts = Array.AsReadOnly(_bossPartStates);
            _bossPartFireCooldowns =
                new int[_bossPartDefinitions.Count];
            _bossPartSecondaryLaserCooldowns =
                new int[_bossPartDefinitions.Count];
            _bossPartSecondaryBurstCooldowns =
                new int[_bossPartDefinitions.Count];
            _bossPartRegenerationRemaining =
                new int[_bossPartDefinitions.Count];
            _bossPartsEverDestroyed =
                new bool[_bossPartDefinitions.Count];
            _bossPartContactHitThisCycle =
                new bool[_bossPartDefinitions.Count];
            _bossPartSpawnDefinitions =
                new EnemyDefinition[_bossPartDefinitions.Count];
            ResolveBossPartRuntimeData();
            _warshipDefinition = stageEnabled
                ? stagePlan.WarshipEncounter
                : null;
            _warshipRuntimePartDefinitions =
                BuildWarshipRuntimePartDefinitions();
            _bossRuntimeMaxHp = _bossPartStates.Length == 0
                ? _bossMaxHp
                : SumBossPartMaxHp();

            if (stageEnabled)
            {
                _stageSegments = stagePlan.Segments;
                _segmentStartTicks =
                    BuildSegmentStartTicks(stagePlan);
                BuildSegmentScrollProfile(
                    stagePlan,
                    _scrollSpeedNumerator,
                    _scrollSpeedDenominator,
                    out _segmentScrollStartOffsets,
                    out _segmentScrollSpeedNumerators,
                    out _segmentScrollSpeedDenominators,
                    out _stageScrollTicks,
                    out _stageScrollEndOffset);
                _usesSegmentScrollMultipliers =
                    HasSegmentScrollMultipliers(stagePlan);
                _visionObscured =
                    stagePlan.Gimmick.VisionObscured;
                int configuredTimeLimit =
                    stagePlan.Gimmick.TimeLimitTicks;
                _timeLimitTicks = configuredTimeLimit;
                WeaponDefinition weapon = content.PlayerWeapon;
                _bulletSpeedNumerator = config.UseConfiguredMainShotStats
                    ? config.PlayerBulletSpeedNumerator
                    : useLaserProfile
                        ? config.LaserSpeedNumerator
                        : useSpreadProfile
                            ? config.SpreadSpeedNumerator
                            : weapon.ProjectileSpeedNumerator;
                _bulletSpeedDenominator = config.UseConfiguredMainShotStats
                    ? config.PlayerBulletSpeedDenominator
                    : useLaserProfile
                        ? config.LaserSpeedDenominator
                        : useSpreadProfile
                            ? config.SpreadSpeedDenominator
                            : weapon.ProjectileSpeedDenominator;
                _fireIntervalTicks = config.UseConfiguredMainShotStats
                    ? config.FireIntervalTicks
                    : useLaserProfile
                        ? config.LaserFireIntervalTicks
                        : useSpreadProfile
                            ? config.SpreadFireIntervalTicks
                            : weapon.FireIntervalTicks;
                _playerBulletDamage = config.UseConfiguredMainShotStats
                    ? config.MainShotBaseDamage
                    : useLaserProfile
                        ? config.LaserBaseDamage
                        : useSpreadProfile
                            ? config.SpreadBaseDamage
                            : weapon.BaseDamage;
                _playerBulletHalfWidth = config.UseConfiguredMainShotStats
                    ? config.MainShotHalfWidth
                    : useLaserProfile
                        ? config.LaserHalfWidth
                        : useSpreadProfile
                            ? config.SpreadHalfWidth
                            : weapon.ProjectileHalfWidth;
                _playerBulletHalfHeight = config.UseConfiguredMainShotStats
                    ? config.MainShotHalfHeight
                    : useLaserProfile
                        ? config.LaserHalfHeight
                        : useSpreadProfile
                            ? config.SpreadHalfHeight
                            : weapon.ProjectileHalfHeight;
                ValidateDropTotals(content, _capsuleNoDropWeight);
                _scheduledSpawns = BuildSchedule(stagePlan, content, out long totalTicks);
                _scheduledObstacles = BuildObstacleSchedule(stagePlan);
                _stageTotalTicks = (int)Math.Min(totalTicks, int.MaxValue);
            }
            else
            {
                _stageSegments = Array.Empty<StageSegment>();
                _segmentStartTicks = Array.Empty<int>();
                _segmentScrollStartOffsets = Array.Empty<long>();
                _segmentScrollSpeedNumerators = Array.Empty<int>();
                _segmentScrollSpeedDenominators = Array.Empty<int>();
                _stageScrollTicks = 0;
                _stageScrollEndOffset = 0;
                _usesSegmentScrollMultipliers = false;
                _visionObscured = false;
                _timeLimitTicks = 0;
                _bulletSpeedNumerator = useLaserProfile
                    ? config.LaserSpeedNumerator
                    : useSpreadProfile
                        ? config.SpreadSpeedNumerator
                        : config.PlayerBulletSpeedNumerator;
                _bulletSpeedDenominator = useLaserProfile
                    ? config.LaserSpeedDenominator
                    : useSpreadProfile
                        ? config.SpreadSpeedDenominator
                        : config.PlayerBulletSpeedDenominator;
                _fireIntervalTicks = useLaserProfile
                    ? config.LaserFireIntervalTicks
                    : useSpreadProfile
                        ? config.SpreadFireIntervalTicks
                        : config.FireIntervalTicks;
                _playerBulletDamage = 0;
                _playerBulletHalfWidth = useLaserProfile
                    ? config.LaserHalfWidth
                    : useSpreadProfile
                        ? config.SpreadHalfWidth
                        : 0;
                _playerBulletHalfHeight = useLaserProfile
                    ? config.LaserHalfHeight
                    : useSpreadProfile
                        ? config.SpreadHalfHeight
                        : 0;
                _scheduledSpawns = Array.Empty<ScheduledSpawn>();
                _scheduledObstacles = Array.Empty<ScheduledObstacle>();
            }
            _bossSpawnX = Math.Max(
                _bossHoldX,
                SaturateToInt(
                    (long)SimSpace.PlayfieldHalfWidthSubUnits
                    + GetBossLeftExtent()
                    + 1));
            _preparesBossRoomBoundary =
                stageEnabled && preparesBossRoomBoundary;
            int cleanupLeadTicks = _preparesBossRoomBoundary
                ? RoomBoundaryCleanupLeadTicks
                : _bossMaxHp > 0
                    ? BossSpawnSuppressionLeadTicks
                    : 0;
            _fieldCleanupStartTick = cleanupLeadTicks == 0
                ? int.MaxValue
                : _stageTotalTicks > cleanupLeadTicks
                    ? _stageTotalTicks - cleanupLeadTicks
                    : _stageTotalTicks == int.MaxValue
                        ? int.MaxValue
                        : _stageTotalTicks + 1;

            int bulletCapacity = _maxBullets + _maxEnemyBullets;
            _bullets = new List<BulletState>(bulletCapacity);
            _bulletAux = new BulletAuxList(bulletCapacity);
            int maximumPrimaryPierce =
                GetMaximumPrimaryPierce(
                    content,
                    _mainShotBasePierceEnemyCount);
            long hitRecordCapacity =
                (long)_maxBullets
                    * (maximumPrimaryPierce
                        + _pierceShotEnemyCount
                        + _ricochetCount
                        + _missilePierceEnemyCount
                        + 2L);
            if (hitRecordCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.PierceShotEnemyCount),
                    "The no-allocation bullet hit history exceeds the supported range.");
            _bulletHitRecordBulletIds = new int[(int)hitRecordCapacity];
            _bulletHitRecordEnemyIds = new int[(int)hitRecordCapacity];
            _readOnlyBullets = _bullets.AsReadOnly();
            int maxOptionLevel = powerUpGauge == null
                ? 0
                : powerUpGauge.GetMaxLevel(PowerUpSlot.Option);
            if (_optionFormation == OptionFormation.Fixed
                && _optionFixedOffsetXs.Length < maxOptionLevel)
                throw new ArgumentException(
                    "Fixed formation requires one X/Y offset per option.",
                    nameof(config));
            _options = new List<OptionState>(maxOptionLevel);
            _readOnlyOptions = _options.AsReadOnly();
            long historyCapacity = (long)maxOptionLevel * _optionFollowDelayTicks + 1;
            if (historyCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.OptionFollowDelayTicks),
                    "Option history capacity exceeds the supported range.");
            _playerHistoryX = new int[(int)historyCapacity];
            _playerHistoryY = new int[(int)historyCapacity];
            int spawnCapacity = Math.Max(
                _scheduledSpawns.Length,
                _maxEnemies);
            _enemies = new List<EnemyState>(spawnCapacity);
            _enemyDefinitions = new List<EnemyDefinition>(spawnCapacity);
            _enemyXRemainders = new List<int>(spawnCapacity);
            _enemySpawnYs = new List<int>(spawnCapacity);
            _enemyAges = new List<int>(spawnCapacity);
            _enemyDiveTargetYs = new List<int>(spawnCapacity);
            _enemyMovementFlags = new List<byte>(spawnCapacity);
            _readOnlyEnemies = _enemies.AsReadOnly();
            int segmentChainCapacity = GetSegmentChainCapacity(
                _bossPhases,
                _bossForm2);
            _segmentChainRuntimes =
                new List<SegmentChainRuntime>(segmentChainCapacity);
            _segmentChainStates = new List<SegmentChainState>(
                checked(segmentChainCapacity * 8));
            _readOnlySegmentChains =
                _segmentChainStates.AsReadOnly();
            _obstacles = new List<ObstacleState>(_maxObstacles);
            _obstacleAges = new List<int>(_maxObstacles);
            _obstacleLaserAttacks =
                new List<LaserAttackDefinition>(_maxObstacles);
            _obstacleBlocksEnemyBullets =
                new List<bool>(_maxObstacles);
            _obstacleRegenDelayTicks =
                new List<int>(_maxObstacles);
            _obstacleMaxHps =
                new List<int>(_maxObstacles);
            _pendingObstacleRegens =
                new List<ObstacleRegenerationState>(_maxObstacles);
            _readOnlyPendingObstacleRegens =
                _pendingObstacleRegens.AsReadOnly();
            _obstacleMotionXRemainders = new List<long>(_maxObstacles);
            _obstacleMotionYRemainders = new List<long>(_maxObstacles);
            _obstacleVelocityXNumerators = new List<long>(_maxObstacles);
            _obstacleVelocityYNumerators = new List<long>(_maxObstacles);
            _obstacleVelocityDenominators = new List<long>(_maxObstacles);
            _obstacleGravityNumerators = new List<long>(_maxObstacles);
            _obstacleGravityDenominators = new List<long>(_maxObstacles);
            _readOnlyObstacles = _obstacles.AsReadOnly();
            _capsules = new List<CapsuleState>(spawnCapacity);
            _capsuleMagnetXRemainders = new List<long>(spawnCapacity);
            _capsuleMagnetYRemainders = new List<long>(spawnCapacity);
            _readOnlyCapsules = _capsules.AsReadOnly();
            _bombPickups = new List<BombPickupState>(_maxBombPickups);
            _bombPickupMagnetXRemainders =
                new List<long>(_maxBombPickups);
            _bombPickupMagnetYRemainders =
                new List<long>(_maxBombPickups);
            _readOnlyBombPickups = _bombPickups.AsReadOnly();
            _lasers = new List<LaserState>(_maxLasers);
            _laserDefinitions =
                new List<LaserAttackDefinition>(_maxLasers);
            _laserAges = new List<int>(_maxLasers);
            _readOnlyLasers = _lasers.AsReadOnly();
            _enemyScanIds = new int[spawnCapacity];
            _enemyScanDistances = new long[spawnCapacity];
            long eventCapacity = 64L
                + 3L * spawnCapacity
                + 2L * bulletCapacity
                + 2L * Math.Max(
                    _maxObstacles,
                    _scheduledObstacles.Length)
                + 2L * _maxBombPickups
                + 3L * _maxLasers
                + 3L * segmentChainCapacity;
            if (eventCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(stagePlan),
                    "The no-allocation event capacity exceeds the supported range.");
            _events = new SimEvent[(int)eventCapacity];

            if (continuityState != null
                && continuityState.MultiplierLevel
                    >= _comboMultipliers.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(continuityState),
                    "The carried multiplier level is unsupported.");
            }
            if (continuityState != null
                && ((continuityState.MultiplierLevel
                        == _comboMultipliers.Length - 1
                        && continuityState.ComboGauge != 0)
                    || (continuityState.MultiplierLevel
                        < _comboMultipliers.Length - 1
                        && continuityState.ComboGauge
                            >= _comboGaugeRequirements[
                                continuityState.MultiplierLevel])))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(continuityState),
                    "The carried combo gauge is not canonical.");
            }
            PlayerX = continuityState == null
                ? config.PlayerSpawnX
                : Math.Max(
                    _playerMinX,
                    Math.Min(
                        _playerMaxX,
                        continuityState.PlayerX));
            int initialPlayerY = continuityState == null
                ? config.PlayerSpawnY
                : continuityState.PlayerY;
            PlayerY = Math.Max(
                _playerMinY,
                Math.Min(_playerMaxY, initialPlayerY));
            _scrollBaseOffset = continuityState == null
                ? 0L
                : continuityState.ScrollX;
            if (continuityState != null)
            {
                _multiplierLevel =
                    continuityState.MultiplierLevel;
                _comboGauge = continuityState.ComboGauge;
                _ticksSinceLastComboAction =
                    continuityState.TicksSinceLastKill;
            }
            ShieldStock = Math.Min(
                config.StartingShieldStock,
                _maxShieldStock);
            BombStock = Math.Min(
                config.StartingBombStock,
                _maxBombStock);
            _playerAlive = true;
            UpdateEnvironmentState();
            RecordPlayerPosition();
            ReadPowerUpLevels();
            UpdateOptionPositions();
            SpawnScheduledThroughTick(0);
        }

        public int Tick { get; private set; }
        public long Score { get; private set; }
        public int MultiplierLevel => _multiplierLevel;
        public int ScoreMultiplier => _comboMultipliers[_multiplierLevel];
        public int ComboGauge => _comboGauge;
        // Compatibility name retained for suspend/replay consumers. The clock
        // now measures ticks since the last kill or graze combo action.
        public int TicksSinceLastKill => _ticksSinceLastComboAction;

        public BattleContinuityState CaptureContinuityState()
        {
            return new BattleContinuityState(
                PlayerX,
                PlayerY,
                _multiplierLevel,
                _comboGauge,
                _ticksSinceLastComboAction,
                ScrollX);
        }
        public BattleStatistics Statistics => new BattleStatistics(
            _shotsFired,
            _shotsHit,
            _kills,
            _capsulesCollected,
            _grazeCount,
            _bombsUsed,
            _hitsTaken);
        public long ScrollX => GetScrollXAtTick(Tick);
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public bool IsPlayerAlive => _playerAlive;
        public int ShieldStock { get; private set; }
        public int MaxShieldStock => _maxShieldStock;
        public int BombStock { get; private set; }
        public int PlayerInvulnerabilityTicksRemaining =>
            _playerInvulnerabilityTicksRemaining;
        public int PlayerHp => _playerAlive ? 1 : 0;
        public int ShieldRemaining => ShieldStock;

        /// <summary>
        /// Awards the configured bonus once for the shield stock remaining at
        /// run clear. The bonus is not affected by combo, encounter, or contract
        /// multipliers. Repeated calls are idempotent.
        /// </summary>
        public long AwardRunClearShieldBonus()
        {
            if (_runClearShieldBonusAwarded)
                return 0;
            _runClearShieldBonusAwarded = true;
            long requested = (long)ShieldStock * _shieldBonusScorePerStock;
            long awarded = AddScoreSaturated(requested);
            AppendEvent(
                SimEventType.ShieldBonusAwarded,
                ShieldStock,
                PlayerX,
                PlayerY,
                awarded >= int.MaxValue ? int.MaxValue : (int)awarded);
            return awarded;
        }
        public WeaponType PlayerWeaponType => _playerWeaponType;
        public PrimaryWeaponFamily EquippedPrimaryWeaponFamily =>
            _equippedPrimaryWeaponFamily;
        public int PrimaryWeaponEvolutionLevel =>
            _primaryWeaponEvolutionLevel;
        public int BurstShotsRemaining => _burstShotsRemaining;
        public int BurstCooldownTicksRemaining =>
            _burstCooldownTicks;
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;
        public IReadOnlyList<OptionState> Options => _readOnlyOptions;
        public IReadOnlyList<EnemyState> Enemies => _readOnlyEnemies;
        public IReadOnlyList<SegmentChainState> SegmentChains =>
            _readOnlySegmentChains;
        public IReadOnlyList<ObstacleState> Obstacles => _readOnlyObstacles;
        public IReadOnlyList<ObstacleRegenerationState>
            PendingObstacleRegenerations => _readOnlyPendingObstacleRegens;
        public IReadOnlyList<CapsuleState> Capsules => _readOnlyCapsules;
        public IReadOnlyList<BombPickupState> BombPickups =>
            _readOnlyBombPickups;
        public IReadOnlyList<LaserState> Lasers => _readOnlyLasers;
        public StageEnvironmentState Environment => _environment;
        public bool VisionObscured => _visionObscured;
        public int TimeLimitTicks => _timeLimitTicks;
        public int RemainingTimeTicks => _timeLimitTicks == 0
            ? 0
            : Math.Max(0, _timeLimitTicks - Tick);
        public bool TimeLimitExpired => _timeLimitExpired;
        public ReadOnlySpan<SimEvent> EventsThisTick => new ReadOnlySpan<SimEvent>(_events, 0, _eventCount);
        public bool BossActive => _bossSpawned && !_bossDefeated;
        /// <summary>
        /// 아직 들어오는 중인가. 이 동안은 피격 판정이 없다 (ApplyDamageToBoss*가
        /// 거부한다) — 화면 밖에서 들어오는 것을 때리는 것은 사격이 아니다.
        ///
        /// 전함은 경고가 끝나도 **정박점까지 미끄러져 들어온다.** 예전에는 경고만
        /// 보고 판정을 열어서, 아직 오른쪽에서 들어오는 중인 함체가 맞았다
        /// (사람 지시 2026-08-04: "전함은 다 등장하고부터 피격판정 있게").
        /// </summary>
        public bool BossEntering =>
            BossActive
            && (_warshipEncounter != null
                ? _warshipEncounter.WarningActive
                    || _warshipEncounter.WorldX
                        > _warshipEncounter.Definition.HoldX
                : _bossX > _bossHoldX);
        public bool BossTransitioning =>
            _bossTransitionTicksRemaining > 0;
        public int BossTransitionTicksRemaining =>
            _bossTransitionTicksRemaining;
        public int BossFormIndex => _bossFormIndex;
        public BossState Boss => new BossState(
            _bossId,
            _bossX,
            _bossY,
            _bossHp,
            _bossRuntimeMaxHp,
            _bossPhase,
            _bossPhases.Count == 0
                ? BossMovementPattern.LegacyHover
                : _bossPhases[_bossPhase].MovementPattern,
            _bossPhases.Count == 0
                ? BossPartVulnerability.Legacy
                : _bossPhases[_bossPhase].PartVulnerability,
            _bossFormIndex);
        public IReadOnlyList<BossPartState> BossParts =>
            _readOnlyBossParts;
        public bool SuctionActive => _bossSuctionActive;
        public int WarshipActiveGroupIndex =>
            _warshipEncounter == null
                ? -1
                : _warshipEncounter.ActiveGroupIndex;
        public int WarshipDestroyedAttritionParts =>
            _warshipEncounter == null
                ? 0
                : _warshipEncounter.DestroyedAttritionParts;
        /// <summary>
        /// 함체 중심의 세계 X. 파츠 오프셋의 기준선이다 — 보스 본체 좌표(HoldX)와
        /// 전혀 다른 값이라, 앞/뒤를 가르려면 반드시 이쪽을 봐야 한다.
        /// </summary>
        public int WarshipWorldX =>
            _warshipEncounter != null ? _warshipEncounter.WorldX : 0;

        public int WarshipAnchorOffsetY =>
            _warshipEncounter != null ? _warshipEncounter.AnchorOffsetY : 0;

        public int WarshipAnchorTravelPermille =>
            _warshipEncounter != null
                ? _warshipEncounter.AnchorTravelPermille
                : 1000;

        public int WarshipCoreOpeningWays =>
            _warshipEncounter == null
                ? 0
                : _warshipEncounter.CoreOpeningWays;
        public int WarshipEncounterTick =>
            _warshipEncounter == null ? 0 : _warshipEncounter.Tick;
        public int WarshipActiveGroupElapsedTicks =>
            _warshipEncounter == null
                ? 0
                : _warshipEncounter.ActiveGroupElapsedTicks;
        public long WarshipScrollRemainder =>
            _warshipEncounter == null
                ? 0
                : _warshipEncounter.ScrollRemainder;
        public bool WarshipCoreOpeningPending =>
            _warshipEncounter != null
            && _warshipEncounter.CoreOpeningPending;
        public WarshipEncounterSuspendData
            CaptureWarshipEncounterSuspendData()
        {
            if (_warshipEncounter == null)
                throw new InvalidOperationException(
                    "No active warship encounter can be suspended.");
            return _warshipEncounter.CaptureSuspendData();
        }

        /// <summary>
        /// Restores the warship payload into a BattleSim already replayed to the
        /// same encounter tick. General battle state remains owned by the normal
        /// replay/suspend pipeline.
        /// </summary>
        public void RestoreWarshipEncounterSuspendData(
            WarshipEncounterSuspendData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (_warshipEncounter == null
                || _warshipDefinition == null)
                throw new InvalidOperationException(
                    "No active warship encounter can be restored.");
            if (data.tick != _warshipEncounter.Tick)
                throw new ArgumentException(
                    "Warship restore requires the same replayed encounter tick.",
                    nameof(data));
            _warshipEncounter = WarshipEncounter.Restore(
                _warshipDefinition,
                _warshipRuntimePartDefinitions,
                data);
            _warshipEventCursor = 0;
            RestoreBattlePartsFromWarship();
        }
        /// <summary>보스전이 예정된 스테이지인지 (RunManager가 종료 조건 분기에 쓴다).</summary>
        public bool HasBossBattle => _bossMaxHp > 0;
        /// <summary>
        /// True only after the final configured form is defeated. It remains
        /// false throughout the form-transition window.
        /// </summary>
        public bool BossDefeated => _bossDefeated;
        public int BossDefeatElapsedTicks { get; private set; }
        public bool WasBossPartDestroyed(string partId)
        {
            if (partId == null)
                throw new ArgumentNullException(nameof(partId));
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
                if (string.Equals(
                        _bossPartDefinitions[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return _bossPartsEverDestroyed[i];
            return false;
        }
        /// <summary>
        /// True when this regular room must drain before the following boss room.
        /// </summary>
        public bool PreparesBossRoomBoundary =>
            _preparesBossRoomBoundary;
        /// <summary>
        /// True after all hostile and collectible transient state has exited.
        /// Player-owned projectiles do not block a room transition.
        /// </summary>
        public bool IsRoomBoundaryReady =>
            !_preparesBossRoomBoundary
            || RoomBoundaryWaitLimitReached
            || (_enemies.Count == 0
                && CountEnemyBullets() == 0
                && _obstacles.Count == 0
                && _capsules.Count == 0
                && _bombPickups.Count == 0
                && CountHostileLasers() == 0);
        /// <summary>
        /// Ticks spent beyond the scheduled room end while transient state drains.
        /// </summary>
        public int RoomBoundaryWaitTicks =>
            !_preparesBossRoomBoundary || Tick <= _stageTotalTicks
                ? 0
                : Tick - _stageTotalTicks;
        /// <summary>
        /// True when the deterministic drain deadline forces the next boundary.
        /// </summary>
        public bool RoomBoundaryWaitLimitReached =>
            _preparesBossRoomBoundary
            && RoomBoundaryWaitTicks >= RoomBoundaryMaximumWaitTicks;
        bool IsRoomBoundaryCleanupActive =>
            _preparesBossRoomBoundary
            && Tick >= _fieldCleanupStartTick;

        void ResolveBossPartRuntimeData()
        {
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                int scaledMaxHp = ScaleEnemyHp(definition.MaxHp);
                _bossPartStates[i] = new BossPartState(
                    definition.PartId,
                    definition.OffsetX,
                    definition.OffsetY,
                    scaledMaxHp,
                    scaledMaxHp,
                    false,
                    IsBossPartActive(i),
                    definition.IsCore,
                    false);
                BossPartAttackProfile attack = GetBossPartAttack(i);
                _bossPartFireCooldowns[i] = attack.IntervalTicks;
                _bossPartSecondaryLaserCooldowns[i] =
                    attack.SecondaryLaser == null
                        ? 0
                        : attack.SecondaryLaser.CycleIntervalTicks;
                _bossPartSecondaryBurstCooldowns[i] =
                    attack.SecondaryBurst == null
                        ? 0
                        : attack.SecondaryBurst.CycleIntervalTicks;
                if (definition.Attack.Type
                    == BossPartAttackType.SpawnEnemy)
                {
                    EnemyDefinition spawn = _battleContent.FindEnemy(
                        definition.Attack.SpawnEnemyId);
                    if (spawn == null)
                        throw new ArgumentException(
                            $"Boss part '{definition.PartId}' references "
                            + $"unknown enemy '{definition.Attack.SpawnEnemyId}'.",
                            nameof(_battleContent));
                    _bossPartSpawnDefinitions[i] = spawn;
                }
            }
        }

        IReadOnlyList<BossPartDefinition>
            BuildWarshipRuntimePartDefinitions()
        {
            if (_warshipDefinition == null)
                return Array.Empty<BossPartDefinition>();
            var result = new BossPartDefinition[
                _bossPartDefinitions.Count];
            for (int i = 0; i < result.Length; i++)
            {
                BossPartDefinition source = _bossPartDefinitions[i];
                result[i] = new BossPartDefinition(
                    source.PartId,
                    source.OffsetX,
                    source.OffsetY,
                    source.HalfWidth,
                    source.HalfHeight,
                    ScaleEnemyHp(source.MaxHp),
                    source.IsCore,
                    source.CoreGatePartIds,
                    source.Attack,
                    source.RegenerationTicks);
            }
            return Array.AsReadOnly(result);
        }

        void ValidateBossPhaseRuntimeData()
        {
            for (int i = 0; i < _bossPhases.Count; i++)
            {
                Generation.BossPhase phase = _bossPhases[i];
                if (phase.SignaturePattern == BossSignaturePattern.Brood
                    && _battleContent.FindEnemy(
                        phase.SignatureSpawnEnemyId) == null)
                    throw new ArgumentException(
                        $"Boss phase {i} references unknown brood enemy "
                        + $"'{phase.SignatureSpawnEnemyId}'.",
                        nameof(_battleContent));
                for (int rule = 0; rule < phase.PartRules.Count; rule++)
                {
                    BossPartAttackProfile attack =
                        phase.PartRules[rule].Attack;
                    if (attack != null
                        && attack.Type == BossPartAttackType.SpawnEnemy
                        && _battleContent.FindEnemy(
                            attack.SpawnEnemyId) == null)
                        throw new ArgumentException(
                            $"Boss phase {i} part '{phase.PartRules[rule].PartId}' "
                            + $"references unknown enemy '{attack.SpawnEnemyId}'.",
                            nameof(_battleContent));
                }
            }
        }

        int SumBossPartMaxHp()
        {
            int total = 0;
            for (int i = 0; i < _bossPartStates.Length; i++)
                total = SaturatingAddDamage(
                    total,
                    _bossPartStates[i].MaxHp);
            return total;
        }

        /// <summary>
        /// Returns piecewise segment scroll at any tick. Segment rates are exact
        /// rational products and offsets remain continuous at every boundary.
        /// </summary>
        public long GetScrollXAtTick(int tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick));
            // A warship boss replaces forward stage travel with its own
            // deterministic approach/hold movement. Keep the room scroll at the
            // last pre-boss frame so the player/camera reference does not keep
            // advancing when WARNING activates. This is especially important for
            // RunManager's one-tick boss-only room, whose inherited ScrollX must
            // remain continuous with the preceding combat room.
            int scrollTick = _warshipDefinition != null
                && tick >= _stageTotalTicks
                    ? Math.Max(0, _stageTotalTicks - 1)
                    : tick;
            if (!_usesSegmentScrollMultipliers)
                return checked(
                    _scrollBaseOffset
                    + ComputeScrollX(
                        scrollTick,
                        _scrollSpeedNumerator,
                        _scrollSpeedDenominator));
            for (int i = 0; i < _segmentStartTicks.Length; i++)
            {
                int startTick = _segmentStartTicks[i];
                int endTick = checked(
                    startTick + _stageSegments[i].LengthTicks);
                if (scrollTick > endTick)
                    continue;
                return checked(
                    _scrollBaseOffset
                    + _segmentScrollStartOffsets[i]
                    + ComputeScrollX(
                        scrollTick - startTick,
                        _segmentScrollSpeedNumerators[i],
                        _segmentScrollSpeedDenominators[i]));
            }
            return checked(
                _scrollBaseOffset
                + _stageScrollEndOffset
                + ComputeScrollX(
                    scrollTick - _stageScrollTicks,
                    _scrollSpeedNumerator,
                    _scrollSpeedDenominator));
        }

        /// <summary>Pure integer scroll function: floor(tick * numerator / denominator).</summary>
        public static long ComputeScrollX(int tick, int speedNumerator, int speedDenominator)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (speedNumerator < 0) throw new ArgumentOutOfRangeException(nameof(speedNumerator));
            if (speedDenominator < 1) throw new ArgumentOutOfRangeException(nameof(speedDenominator));
            return (long)tick * speedNumerator / speedDenominator;
        }

        public void Step(in InputCommand input)
        {
            if (Tick == int.MaxValue)
                throw new InvalidOperationException("The simulation tick counter is exhausted.");
            Tick++;
            _eventCount = 0;
            _comboActionThisTick = false;
            if (_ticksSinceGrazeLevelUp < int.MaxValue) _ticksSinceGrazeLevelUp++;
            if (_playerInvulnerabilityTicksRemaining > 0)
                _playerInvulnerabilityTicksRemaining--;

            UpdateEnvironmentState();
            ExpireTimeLimitIfNeeded();
            RefreshSuctionLifecycle();
            int previousPlayerX = PlayerX;
            int previousPlayerY = PlayerY;
            AdvancePlayer(in input);
            _playerVelocityX = PlayerX - previousPlayerX;
            _playerVelocityY = PlayerY - previousPlayerY;
            RecordPlayerPosition();
            bool activatePressed = input.Activate && !_activateHeld;
            _activateHeld = input.Activate;
            if (activatePressed && _powerUpGauge != null)
                _powerUpGauge.Activate();
            ReadPowerUpLevels();
            UpdateOptionPositions();
            bool bombPressed =
                input.ActivateBomb && !_bombHeld;
            _bombHeld = input.ActivateBomb;
            AdvanceLasers();
            UpdatePlayerBeam(input.Fire);
            UpdateEnemyProjectileBehaviors();
            AdvanceBullets();
            AdvanceEnemies();
            AdvanceObstacles();
            AdvanceObstacleRegeneration();
            AdvanceCapsules();
            AdvanceBombPickups();
            SpawnScheduledThroughTick(Tick);
            UpdateBoss();
            RefreshSuctionLifecycle();
            UpdateSegmentChains();
            if (bombPressed)
                TryActivateBomb();
            ResolvePlayerBulletObstacleCollisions();
            ResolvePlayerBulletEnemyCollisions();
            ResolvePlayerBulletSegmentChainCollisions();
            ResolvePlayerBulletBossCollisions();
            RefreshLaserSegments();
            ResolvePlayerLaserEnemyCollisions();
            ResolvePlayerLaserSegmentChainCollisions();
            ResolvePlayerLaserBossCollisions();
            RefreshSuctionLifecycle();
            ResolveEnemyBulletObstacleCollisions();
            ResolveEnemyBulletPlayerCollisions();
            ResolveLaserPlayerCollisions();
            ResolveEnemyPlayerCollisions();
            ResolveSegmentChainPlayerCollisions();
            ResolveObstaclePlayerCollisions();
            ResolveCapsulePlayerCollisions();
            ResolveBombPickupPlayerCollisions();
            CompleteWarshipTick();
            AdvanceComboDecay();

            if (_cooldown > 0) _cooldown--;
            if (_missileCooldown > 0) _missileCooldown--;
            AdvanceMainShotBurst();
            if (input.Fire)
            {
                if (_cooldown == 0
                    && _burstShotsRemaining == 0
                    && HasCapacityForMainShotVolley())
                {
                    if (_beamDamagePerTick == 0)
                        SpawnMainShotVolley(false);
                    else if (_options.Count > 0)
                        SpawnBeamOptionVolley();
                }
                if (_missileLevel > 0
                    && _missileCooldown == 0
                    && HasCapacityForMissileVolley())
                    SpawnMissileVolley();
            }
        }

        void EmitEvent(SimEventType type, int entityId, int x, int y, int arg)
        {
            AppendEvent(type, entityId, x, y, arg);

            switch (type)
            {
                case SimEventType.EnemyHit:
                    IncrementSaturated(ref _shotsHit);
                    break;
                case SimEventType.EnemyKilled:
                    IncrementSaturated(ref _shotsHit);
                    IncrementSaturated(ref _kills);
                    break;
                case SimEventType.CapsulePicked:
                    IncrementSaturated(ref _capsulesCollected);
                    break;
                case SimEventType.GrazeScored:
                    IncrementSaturated(ref _grazeCount);
                    break;
                case SimEventType.PlayerHit:
                    ResetCombo();
                    break;
            }
        }

        /// <summary>
        /// Appends the lifecycle event to the current tick's fixed event buffer.
        /// RunManager calls this after Step when deterministic ghost playback ends.
        /// </summary>
        public void EmitGhostEnded(
            int entityId,
            int x,
            int y,
            int replayedTicks)
        {
            if (entityId < 1)
                throw new ArgumentOutOfRangeException(nameof(entityId));
            if (replayedTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(replayedTicks));
            EmitEvent(
                SimEventType.GhostEnded,
                entityId,
                x,
                y,
                replayedTicks);
        }

        /// <summary>
        /// Emits spawn at the new room's tick-zero boundary. The ghost has no
        /// collision body; only its explicit projectiles enter BattleSim.
        /// </summary>
        public void EmitGhostSpawned(
            int entityId,
            int x,
            int y,
            int fixedWeaponLevel)
        {
            if (entityId < 1)
                throw new ArgumentOutOfRangeException(nameof(entityId));
            if (fixedWeaponLevel < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(fixedWeaponLevel));
            EmitEvent(
                SimEventType.GhostSpawned,
                entityId,
                x,
                y,
                fixedWeaponLevel);
        }

        /// <summary>
        /// Spawns one capacity-limited straight allied shot with exact damage.
        /// It participates in the normal deterministic projectile collision and
        /// scoring pipeline, but never inherits options, missiles, burst, beam,
        /// modifiers, or the current player's weapon level.
        /// </summary>
        /// <summary>
        /// **개발·테스트 전용**: 지금 때릴 수 있는 보스 파츠(없으면 본체)를 곧장
        /// 깎는다. 고스트탄을 쓰지 않는 이유는 탄 상한 때문이다 — 파워가 최대인
        /// 상태에서는 플레이어 탄이 이미 상한을 채우고 있어 주입이 전부 거부된다.
        /// 확인용 치트가 정작 확인하고 싶은 상황(맥스 파워 보스전)에서만 안 먹는다.
        /// </summary>
        public bool DevDamageBoss(int percentOfMax)
        {
            if (percentOfMax < 1 || !BossActive) return false;
            bool hit = false;
            for (int i = 0; i < _bossPartStates.Length; i++)
            {
                BossPartState part = _bossPartStates[i];
                if (part.Destroyed || IsBossPartInvulnerable(i) || part.Hp <= 0)
                    continue;
                ApplyDamageToBossPart(
                    i, Math.Max(1, part.MaxHp * percentOfMax / 100));
                hit = true;
            }
            if (hit || _bossHp <= 0) return hit;
            ApplyDamageToBoss(Math.Max(1, _bossMaxHp * percentOfMax / 100));
            return true;
        }

        public bool TrySpawnGhostMainShot(int x, int y, int fixedDamage)
        {
            if (fixedDamage < 1)
                throw new ArgumentOutOfRangeException(nameof(fixedDamage));
            if (CountPlayerBullets() >= _maxBullets)
                return false;
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException(
                    "The bullet id counter is exhausted.");

            _bullets.Add(new BulletState(
                _nextBulletId++,
                BulletFaction.Player,
                BulletKind.GhostMainShot,
                x,
                y,
                0,
                100,
                100,
                BossSignaturePattern.None,
                fixedDamage));
            _bulletAux.Add(default);
            AddDefaultEnemyProjectileBehavior();
            IncrementSaturated(ref _shotsFired);
            return true;
        }

        void EmitBossPartEvent(
            SimEventType type,
            int x,
            int y,
            int partIndex)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            _events[_eventCount++] = new SimEvent(
                type,
                _bossId,
                x,
                y,
                partIndex,
                _bossPartDefinitions[partIndex].PartId);
        }

        /// <summary>
        /// 파츠 이벤트인데 Arg에 파츠 index가 아니라 **다른 값**을 실어야 할 때.
        /// 근접 예고는 연출이 예고 길이만큼 번쩍여야 해서 그 길이를 실어 보낸다 —
        /// 파츠는 PartId로 찾으면 된다.
        /// </summary>
        void EmitBossPartEvent(
            SimEventType type,
            int x,
            int y,
            int arg,
            int partIndex)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            _events[_eventCount++] = new SimEvent(
                type,
                _bossId,
                x,
                y,
                arg,
                _bossPartDefinitions[partIndex].PartId);
        }

        void AppendEvent(SimEventType type, int entityId, int x, int y, int arg)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            _events[_eventCount++] = new SimEvent(type, entityId, x, y, arg);
        }

        void AppendEvent(in SimEvent simEvent)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            _events[_eventCount++] = simEvent;
        }

        void EmitBossAttackTelegraph(Generation.BossPhase phase)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            BulletKind kind = ToBulletKind(phase.ProjectileKind);
            BossTelegraphKind telegraphKind =
                kind == BulletKind.BossLaser
                || phase.SignaturePattern == BossSignaturePattern.LaserGrid
                || phase.SignaturePattern == BossSignaturePattern.Lightning
                || phase.SignaturePattern == BossSignaturePattern.PrismCore
                    ? BossTelegraphKind.Laser
                    : BossTelegraphKind.Barrage;
            _events[_eventCount++] = new SimEvent(
                SimEventType.BossAttackTelegraphed,
                _bossId,
                _bossX,
                _bossY,
                _bossPhase,
                null,
                kind,
                phase.SignaturePattern,
                telegraphKind);
        }

        static void IncrementSaturated(ref long counter)
        {
            if (counter < long.MaxValue)
                counter++;
        }

        /// <summary>Tick > 0 조건: 생성자(재시작 승계 포함) 초기 레벨은 이벤트가 아니다.</summary>
        void EmitLevelChange(PowerUpSlot slot, int previous, int next)
        {
            if (Tick > 0 && next != previous)
                EmitEvent(SimEventType.PowerUpLevelChanged, (int)slot, PlayerX, PlayerY, next);
        }

        void ReadPowerUpLevels()
        {
            if (_powerUpGauge == null)
            {
                _mainShotLevel = 0;
                _missileLevel = 0;
                _optionLevel = 0;
                _shieldGaugeLevel = 0;
                return;
            }

            int previousMainShot = _mainShotLevel;
            int previousMissile = _missileLevel;
            int previousOption = _optionLevel;
            int nextSpeedLevel =
                _powerUpGauge.GetLevel(PowerUpSlot.Speed);
            if (nextSpeedLevel != _speedGaugeLevel)
            {
                ApplySpeedGaugeLevel(nextSpeedLevel);
                EmitLevelChange(
                    PowerUpSlot.Speed,
                    _speedGaugeLevel,
                    nextSpeedLevel);
                _speedGaugeLevel = nextSpeedLevel;
            }
            ApplyGaugeWeaponMode();
            _mainShotLevel =
                GetEffectivePowerLevel(PowerUpSlot.MainShot);
            _missileLevel =
                GetEffectivePowerLevel(PowerUpSlot.Missile);
            _optionLevel =
                GetEffectivePowerLevel(PowerUpSlot.Option);
            EmitLevelChange(PowerUpSlot.MainShot, previousMainShot, _mainShotLevel);
            EmitLevelChange(PowerUpSlot.Missile, previousMissile, _missileLevel);
            EmitLevelChange(PowerUpSlot.Option, previousOption, _optionLevel);
            int nextShieldLevel =
                GetEffectivePowerLevel(PowerUpSlot.Shield);
            EmitLevelChange(PowerUpSlot.Shield, _shieldGaugeLevel, nextShieldLevel);
            if (nextShieldLevel > _shieldGaugeLevel)
                RecoverShieldStock(nextShieldLevel - _shieldGaugeLevel);
            _shieldGaugeLevel = nextShieldLevel;
        }

        void ApplySpeedGaugeLevel(int nextLevel)
        {
            int delta = nextLevel - _speedGaugeLevel;
            if (delta == 0)
                return;
            if (delta < 0)
                throw new InvalidOperationException(
                    "Speed gauge levels cannot decrease inside a battle.");
            AddExactPositiveFraction(
                ref _playerSpeedNumerator,
                ref _playerSpeedDenominator,
                (long)_powerUpGauge.SpeedBonusNumerator * delta,
                _powerUpGauge.SpeedBonusDenominator);
        }

        void ApplyGaugeWeaponMode()
        {
            if (_battleContent == null)
                return;
            PrimaryWeaponFamily family;
            switch (_powerUpGauge.ActiveWeaponMode)
            {
                case PowerUpWeaponMode.None:
                    return;
                case PowerUpWeaponMode.Double:
                    family = PrimaryWeaponFamily.Double;
                    break;
                case PowerUpWeaponMode.Laser:
                    family = PrimaryWeaponFamily.Laser;
                    break;
                case PowerUpWeaponMode.Triple:
                    family = PrimaryWeaponFamily.Spread;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown gauge weapon mode.");
            }
            PowerUpSlot modeSlot = family == PrimaryWeaponFamily.Double
                ? PowerUpSlot.Double
                : family == PrimaryWeaponFamily.Laser
                    ? PowerUpSlot.Laser
                    : PowerUpSlot.Triple;
            int evolutionLevel = _powerUpGauge.GetLevel(modeSlot);
            if (family == _equippedPrimaryWeaponFamily
                && evolutionLevel == _primaryWeaponEvolutionLevel)
                return;
            PrimaryWeaponFamilyDefinition definition =
                _battleContent.FindPrimaryWeaponFamily(family);
            if (definition == null)
                throw new InvalidOperationException(
                    $"Gauge mode '{family}' has no primary weapon profile.");
            PrimaryWeaponFamilyDefinition current =
                _battleContent.FindPrimaryWeaponFamily(
                    _equippedPrimaryWeaponFamily);
            int damageBonus = current == null
                ? 0
                : Math.Max(
                    0,
                    _playerBulletDamage - current.BaseDamage);
            int intervalReduction = current == null
                ? 0
                : Math.Max(
                    0,
                    current.FireIntervalTicks
                        - _fireIntervalTicks);
            ApplyPrimaryWeaponProfile(definition);
            ApplyPrimaryWeaponLevel(
                definition.GetLevel(evolutionLevel));
            _playerBulletDamage = SaturateToInt(
                (long)_playerBulletDamage + damageBonus);
            _fireIntervalTicks = Math.Max(
                _mainShotMinimumFireIntervalTicks,
                _fireIntervalTicks - intervalReduction);
        }

        void ApplyPrimaryWeaponProfile(
            PrimaryWeaponFamilyDefinition definition)
        {
            _equippedPrimaryWeaponFamily = definition.Family;
            _playerWeaponType = definition.WeaponType;
            _playerBulletDamage = definition.BaseDamage;
            _fireIntervalTicks = definition.FireIntervalTicks;
            _mainShotMinimumFireIntervalTicks =
                definition.MinimumFireIntervalTicks;
            _mainShotRapidFireStartLevel =
                definition.RapidFireStartLevel;
            _mainShotFireIntervalReductionPerLevel =
                definition.FireIntervalReductionPerLevel;
            _bulletSpeedNumerator = definition.SpeedNumerator;
            _bulletSpeedDenominator = definition.SpeedDenominator;
            _playerBulletHalfWidth = definition.HalfWidth;
            _playerBulletHalfHeight = definition.HalfHeight;
            _mainShotBasePierceEnemyCount =
                definition.PierceEnemyCount;
            _spreadWays = definition.SpreadWays;
            _spreadStepLutSlots = definition.SpreadStepLutSlots;
            _mainShotAngleLutSlots =
                CopyAngles(definition.ShotAngleLutSlots);
            _primaryWeaponEvolutionLevel = 1;
            _burstShotsRemaining = 0;
            _burstCooldownTicks = 0;
            RemovePlayerBeam();
        }

        void ApplyPrimaryWeaponLevel(
            PrimaryWeaponLevelDefinition level)
        {
            _primaryWeaponEvolutionLevel = level.Level;
            _mainShotMinimumFireIntervalTicks =
                level.MinimumFireIntervalTicks;
            _mainShotBasePierceEnemyCount =
                level.PierceEnemyCount;
            _spreadWays = level.SpreadWays;
            _spreadStepLutSlots = level.SpreadStepLutSlots;
            _mainShotAngleLutSlots =
                CopyAngles(level.ShotAngleLutSlots);
            _burstCount = level.BurstCount;
            _burstIntervalTicks = level.BurstIntervalTicks;
            _pulseMinStepLutSlots =
                level.PulseMinStepLutSlots;
            _pulseMaxStepLutSlots =
                level.PulseMaxStepLutSlots;
            _pulsePeriodTicks = level.PulsePeriodTicks;
            _inertiaVelocityPercent =
                level.InertiaVelocityPercent;
            _impactExplosionDamage =
                level.ImpactExplosionDamage;
            _impactExplosionRadius =
                level.ImpactExplosionRadius;
            _beamDamagePerTick = level.BeamDamagePerTick;
            _beamLength = level.BeamLength;
            _beamStartHalfWidth = level.BeamStartHalfWidth;
            _beamGrowthPerTick = level.BeamGrowthPerTick;
            _beamMaxHalfWidth = level.BeamMaxHalfWidth;
            _burstShotsRemaining = 0;
            _burstCooldownTicks = 0;
            RemovePlayerBeam();
        }

        static int[] CopyAngles(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<int>();
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            return copy;
        }

        static void AddExactPositiveFraction(
            ref int numerator,
            ref int denominator,
            long bonusNumerator,
            int bonusDenominator)
        {
            if (bonusNumerator < 0 || bonusDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(bonusNumerator));
            long commonDivisor = GreatestCommonDivisorLong(
                denominator,
                bonusDenominator);
            long leftScale = bonusDenominator / commonDivisor;
            long rightScale = denominator / commonDivisor;
            long nextDenominator = (long)denominator * leftScale;
            long nextNumerator;
            try
            {
                nextNumerator = checked(
                    (long)numerator * leftScale
                    + bonusNumerator * rightScale);
            }
            catch (OverflowException)
            {
                numerator = int.MaxValue;
                denominator = 1;
                return;
            }
            long divisor = GreatestCommonDivisorLong(
                nextNumerator,
                nextDenominator);
            nextNumerator /= divisor;
            nextDenominator /= divisor;
            if (nextNumerator > int.MaxValue
                || nextDenominator > int.MaxValue)
            {
                numerator = int.MaxValue;
                denominator = 1;
                return;
            }
            numerator = (int)nextNumerator;
            denominator = (int)nextDenominator;
        }

        static long GreatestCommonDivisorLong(long left, long right)
        {
            while (right != 0)
            {
                long remainder = left % right;
                left = right;
                right = remainder;
            }
            return left == 0 ? 1 : left;
        }

        int GetEffectivePowerLevel(PowerUpSlot slot)
        {
            int rawLevel = _powerUpGauge.GetLevel(slot);
            int softCap;
            switch (slot)
            {
                case PowerUpSlot.MainShot:
                    softCap = _mainShotEffectSoftCap;
                    break;
                case PowerUpSlot.Missile:
                    softCap = _missileEffectSoftCap;
                    break;
                case PowerUpSlot.Option:
                    softCap = _optionEffectSoftCap;
                    break;
                case PowerUpSlot.Shield:
                    softCap = _shieldEffectSoftCap;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
            return PowerLevelScaling.GetEffectiveLevel(
                rawLevel,
                softCap);
        }

        static int ResolveEffectSoftCap(
            BattleContent content,
            PowerUpGauge gauge,
            PowerUpSlot slot)
        {
            if (content == null || gauge == null)
                return int.MaxValue;
            WeaponDefinition definition = content.FindWeapon(slot);
            return definition != null
                && definition.MaxLevel == gauge.GetMaxLevel(slot)
                    ? definition.EffectSoftCapLevel
                    : int.MaxValue;
        }

        static int GetMaximumPrimaryPierce(
            BattleContent content,
            int fallback)
        {
            int maximum = fallback;
            if (content == null)
                return maximum;
            for (int i = 0;
                i < content.PrimaryWeaponFamilies.Count;
                i++)
            {
                maximum = Math.Max(
                    maximum,
                    content.PrimaryWeaponFamilies[i]
                        .PierceEnemyCount);
            }
            return maximum;
        }

        static PrimaryWeaponFamily PrimaryWeaponFamilyFor(
            WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.Laser:
                    return PrimaryWeaponFamily.Laser;
                case WeaponType.Spread:
                    return PrimaryWeaponFamily.Spread;
                default:
                    return PrimaryWeaponFamily.Vulcan;
            }
        }

        /// <summary>
        /// Restores the single REQ-040 durability resource without exceeding the
        /// configured provisional cap. Returns the number of stocks restored.
        /// </summary>
        public int RecoverShieldStock(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            int available = _maxShieldStock - ShieldStock;
            int restored = Math.Min(amount, available);
            ShieldStock += restored;
            return restored;
        }

        /// <summary>
        /// Applies the Core-owned runtime shield cap. Lowering the cap clamps
        /// current stock immediately so no save or following stage can carry an
        /// impossible stock value.
        /// </summary>
        public int SetMaxShieldStock(int maxShieldStock)
        {
            if (maxShieldStock < 1
                || maxShieldStock
                    > BattleSimConfig.MaximumShieldStock)
                throw new ArgumentOutOfRangeException(
                    nameof(maxShieldStock),
                    $"Shield cap must be in "
                    + $"1.."
                    + $"{BattleSimConfig.MaximumShieldStock}.");
            _maxShieldStock = maxShieldStock;
            if (ShieldStock > _maxShieldStock)
                ShieldStock = _maxShieldStock;
            return ShieldStock;
        }

        public int SetMaxBombStock(int maxBombStock)
        {
            if (maxBombStock < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxBombStock));
            _maxBombStock = maxBombStock;
            if (BombStock > _maxBombStock)
                BombStock = _maxBombStock;
            return BombStock;
        }

        /// <summary>
        /// Adds bomb stock up to the provisional cap. This is the Core-owned
        /// reward/pickup integration point; Presentation must not mutate stock.
        /// </summary>
        public int AcquireBombStock(int amount)
        {
            return AcquireBombStock(
                amount,
                0,
                PlayerX,
                PlayerY);
        }

        int AcquireBombStock(
            int amount,
            int pickupId,
            int x,
            int y)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            int available = _maxBombStock - BombStock;
            int restored = Math.Min(amount, available);
            BombStock += restored;
            EmitEvent(
                SimEventType.BombAcquired,
                pickupId,
                x,
                y,
                BombStock);
            if (restored > 0)
                EmitEvent(
                    SimEventType.BombStockChanged,
                    0,
                    PlayerX,
                    PlayerY,
                    BombStock);
            return restored;
        }

        void TryActivateBomb()
        {
            if (BombStock == 0)
            {
                EmitEvent(
                    SimEventType.BombActivationRejectedEmpty,
                    0,
                    PlayerX,
                    PlayerY,
                    0);
                return;
            }

            BombStock--;
            IncrementSaturated(ref _bombsUsed);
            EmitEvent(
                SimEventType.BombStockChanged,
                0,
                PlayerX,
                PlayerY,
                BombStock);
            EmitEvent(
                SimEventType.BombActivated,
                0,
                PlayerX,
                PlayerY,
                _bombEffectRadiusSubUnits);
            if (_playerInvulnerabilityTicksRemaining
                < _bombInvulnerabilityTicks)
            {
                _playerInvulnerabilityTicksRemaining =
                    _bombInvulnerabilityTicks;
            }

            for (int i = _bullets.Count - 1; i >= 0; i--)
                if (_bullets[i].Faction == BulletFaction.Enemy
                    && IsOnScreen(_bullets[i].X, _bullets[i].Y))
                    RemoveBulletAt(i);

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                EnemyState enemy = _enemies[i];
                if (!IsOnScreen(enemy.X, enemy.Y))
                    continue;
                int hp = Damage.ApplyToHp(
                    enemy.Hp,
                    _bombRegularEnemyDamage);
                if (hp > 0)
                {
                    _enemies[i] = new EnemyState(
                        enemy.Id,
                        enemy.DefinitionId,
                        enemy.X,
                        enemy.Y,
                        hp);
                    EmitEvent(
                        SimEventType.EnemyHit,
                        enemy.Id,
                        enemy.X,
                        enemy.Y,
                        enemy.Hp - hp);
                    continue;
                }

                EnemyDefinition definition = _enemyDefinitions[i];
                RemoveEnemyAt(i);
                int awardedScore =
                    RecordKillScore(definition.ScoreValue);
                AppendEvent(
                    SimEventType.EnemyKilled,
                    enemy.Id,
                    enemy.X,
                    enemy.Y,
                    awardedScore);
                IncrementSaturated(ref _kills);
                AdvanceKillCombo();
                TryDropCapsule(definition, enemy.X, enemy.Y);
                TryDropBomb(definition, enemy.X, enemy.Y);
            }

            for (int i = _obstacles.Count - 1; i >= 0; i--)
            {
                ObstacleState obstacle = _obstacles[i];
                if (obstacle.Type != ObstacleType.Breakable
                    || !IsOnScreen(obstacle.X, obstacle.Y))
                {
                    continue;
                }
                ApplyDamageToObstacleAt(
                    i,
                    _bombRegularEnemyDamage,
                    obstacle.X,
                    obstacle.Y);
            }

            for (int i = _segmentChainRuntimes.Count - 1; i >= 0; i--)
            {
                SegmentChainRuntime chain = _segmentChainRuntimes[i];
                if (IsOnScreen(chain.HeadX, chain.HeadY))
                    ApplyDamageToSegmentChain(
                        i,
                        _bombRegularEnemyDamage);
            }

            if (BossEntering
                || !BossActive
                || !IsOnScreen(_bossX, _bossY))
                return;
            if (_bossPartStates.Length == 0)
            {
                ApplyDamageToBoss(_bombBossDamageCap);
                return;
            }
            for (int i = 0;
                i < _bossPartStates.Length && BossActive;
                i++)
            {
                ApplyDamageToBossPart(
                    i,
                    _bombBossPartDamageCap);
            }
        }

        static bool IsOnScreen(int x, int y)
        {
            return x >= -SimSpace.PlayfieldHalfWidthSubUnits
                && x <= SimSpace.PlayfieldHalfWidthSubUnits
                && y >= -SimSpace.PlayfieldHalfHeightSubUnits
                && y <= SimSpace.PlayfieldHalfHeightSubUnits;
        }

    }
}
