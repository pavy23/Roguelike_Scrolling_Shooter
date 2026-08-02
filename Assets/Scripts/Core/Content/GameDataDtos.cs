using System.Runtime.Serialization;

#pragma warning disable CS0649 // Fields are assigned through reflection by the JSON serializer.

namespace Shmup.Core.Content
{
    // DataContractJsonSerializer maps these field names directly to camelCase JSON.
    // Nullable value types let validation distinguish a missing field from zero.
    [DataContract]
    internal sealed class EnemiesDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public DropTableDto dropTable;
        [DataMember]
        public EnemyDto[] enemies;
    }

    [DataContract]
    internal sealed class DropTableDto
    {
        [DataMember]
        public int? noDropWeight;
        [DataMember]
        public int? bombNoDropWeight;
    }

    [DataContract]
    internal sealed class EnemyDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string displayName;
        [DataMember]
        public int? hp;
        [DataMember]
        public int? contactDamage;
        [DataMember]
        public int? scoreValue;
        [DataMember]
        public string movePattern;
        [DataMember]
        public decimal? moveSpeed;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? dropWeight;
        [DataMember]
        public int? bombDropWeight;
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public decimal? amplitude;
        [DataMember]
        public int? periodTicks;
        [DataMember]
        public EnemyMovementDto movement;
        [DataMember]
        public LaserAttackDto laser;
        [DataMember]
        public MidBossProfileDto midBoss;
    }

    [DataContract]
    internal sealed class MidBossProfileDto
    {
        [DataMember]
        public string themeId;
        [DataMember]
        public int? weight;
        [DataMember]
        public int? stageIndexMin;
        [DataMember]
        public int? stageIndexMax;
        [DataMember]
        public BossPhaseDto[] phases;
    }

    [DataContract]
    internal sealed class EnemyMovementDto
    {
        [DataMember]
        public string pattern;
        [DataMember]
        public decimal? speed;
        [DataMember]
        public decimal? amplitude;
        [DataMember]
        public int? periodTicks;
        [DataMember]
        public int? delayTicks;
        [DataMember]
        public int? durationTicks;
        [DataMember]
        public int? pauseTicks;
    }

    [DataContract]
    internal sealed class LaserAttackDto
    {
        [DataMember]
        public int? cycleIntervalTicks;
        [DataMember]
        public int? telegraphTicks;
        [DataMember]
        public int? firingTicks;
        [DataMember]
        public int? sustainTicks;
        [DataMember]
        public int? dissipateTicks;
        [DataMember]
        public decimal? startOffsetX;
        [DataMember]
        public decimal? startOffsetY;
        [DataMember]
        public decimal? endOffsetX;
        [DataMember]
        public decimal? endOffsetY;
        [DataMember]
        public decimal? thinHalfWidth;
        [DataMember]
        public decimal? fullHalfWidth;
        [DataMember]
        public int? damage;
    }

    [DataContract]
    internal sealed class WeaponsDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public WeaponDto[] weapons;
        [DataMember]
        public MissileFamilyDto[] missileFamilies;
        [DataMember]
        public string defaultMissileFamily;
        [DataMember]
        public OptionFormationDto[] optionFormations;
        [DataMember]
        public string defaultOptionFormation;
        [DataMember]
        public int? optionMissileDamagePercent;
        [DataMember]
        public PrimaryWeaponFamilyDto[] primaryWeaponFamilies;
        [DataMember]
        public PowerUpCostCurveDto powerUpCostCurve;
        [DataMember]
        public PowerUpGaugeDto powerUpGauge;
    }

    [DataContract]
    internal sealed class PowerUpGaugeDto
    {
        [DataMember]
        public PowerUpGaugeSlotDto[] slots;
    }

    [DataContract]
    internal sealed class PowerUpGaugeSlotDto
    {
        [DataMember]
        public string slot;
        [DataMember]
        public string nameKey;
        [DataMember]
        public int? maxLevel;
        [DataMember]
        public PowerUpCostCurveDto costCurve;
        [DataMember]
        public decimal? speedBonusPerLevel;
    }

    [DataContract]
    internal sealed class PowerUpCostCurveDto
    {
        [DataMember]
        public int? baseCost;
        [DataMember]
        public int? linearGrowth;
        [DataMember]
        public int? quadraticGrowth;
    }

    [DataContract]
    internal sealed class WeaponDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string slot;
        [DataMember]
        public int? baseDamage;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? minimumFireIntervalTicks;
        [DataMember]
        public decimal? projectileSpeed;
        [DataMember]
        public decimal? projectileHalfWidth;
        [DataMember]
        public decimal? projectileHalfHeight;
        [DataMember]
        public int? maxLevel;
        [DataMember]
        public int? effectSoftCapLevel;
    }

    [DataContract]
    internal sealed class WavesDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public decimal? scrollSpeed;
        [DataMember]
        public decimal? spawnX;
        [DataMember]
        public int? laneCount;
        [DataMember]
        public int? segmentsPerStage;
        [DataMember]
        public int? closingSegmentsPerStage;
        [DataMember]
        public int? startLaneMask;
        [DataMember]
        public string[] themes;
        [DataMember]
        public StageGimmickDto[] gimmicks;
        [DataMember]
        public ContractCatalogDto contracts;
        [DataMember]
        public SegmentDto[] segments;
        [DataMember]
        public BossDto[] bosses;
    }

    [DataContract]
    internal sealed class SegmentDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? weight;
        [DataMember]
        public string theme;
        [DataMember]
        public int? difficultyMin;
        [DataMember]
        public int? difficultyMax;
        [DataMember]
        public int? lengthTicks;
        [DataMember]
        public decimal? scrollSpeedMultiplier;
        [DataMember]
        public string[] postMidbossOutcomes;
        [DataMember]
        public int? entryLaneMask;
        [DataMember]
        public int? exitLaneMask;
        [DataMember]
        public int[] traversableLaneMasks;
        [DataMember]
        public SpawnDto[] spawns;
        [DataMember]
        public ObstacleDto[] obstacles;
        [DataMember]
        public SegmentEnvironmentDto environment;
    }

    [DataContract]
    internal sealed class ContractCatalogDto
    {
        [DataMember]
        public string standardContractId;
        [DataMember]
        public int? minimumOptionCount;
        [DataMember]
        public int? maximumOptionCount;
        [DataMember]
        public ContractDto[] entries;
    }

    [DataContract]
    internal sealed class ContractDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? weight;
        [DataMember]
        public string riskTier;
        [DataMember]
        public string destinationKind;
        [DataMember]
        public string eligibility;
        [DataMember]
        public decimal? enemyDensityMultiplier;
        [DataMember]
        public decimal? capsuleDropMultiplier;
        [DataMember]
        public decimal? bombDropMultiplier;
        [DataMember]
        public bool? guaranteedBombDrop;
        [DataMember]
        public decimal? gimmickIntensityMultiplier;
        [DataMember]
        public int? rewardOptionCountDelta;
        [DataMember]
        public decimal? scoreMultiplier;
        [DataMember]
        public bool? gaugeActivationBanned;
        [DataMember]
        public bool? optionActivationBanned;
        [DataMember]
        public bool? shieldActivationBanned;
    }

    [DataContract]
    internal sealed class StageGimmickDto
    {
        [DataMember]
        public string theme;
        [DataMember]
        public bool? visionObscured;
        [DataMember]
        public int? timeLimitTicks;
    }

    [DataContract]
    internal sealed class SegmentEnvironmentDto
    {
        [DataMember]
        public CorridorDto corridor;
        [DataMember]
        public DriftDto drift;
    }

    [DataContract]
    internal sealed class CorridorDto
    {
        [DataMember]
        public decimal? startMinY;
        [DataMember]
        public decimal? startMaxY;
        [DataMember]
        public decimal? endMinY;
        [DataMember]
        public decimal? endMaxY;
        [DataMember]
        public int? contactDamage;
    }

    [DataContract]
    internal sealed class DriftDto
    {
        [DataMember]
        public decimal? xPerSecond;
        [DataMember]
        public decimal? yPerSecond;
    }

    [DataContract]
    internal sealed class SpawnDto
    {
        [DataMember]
        public int? tick;
        [DataMember]
        public string enemyId;
        [DataMember]
        public decimal? y;
    }

    [DataContract]
    internal sealed class MissileFamilyDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? baseDamage;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? minimumFireIntervalTicks;
        [DataMember]
        public int? fireIntervalReductionPerLevel;
        [DataMember]
        public decimal? projectileSpeed;
        [DataMember]
        public decimal? fallSpeedY;
        [DataMember]
        public int? pierceEnemyCount;
        [DataMember]
        public int? explosionDamage;
        [DataMember]
        public decimal? explosionRadius;
        [DataMember]
        public int? explosionMaxTargets;
        [DataMember]
        public int? damageGrowthPercentPerLevel;
        [DataMember]
        public int? dropDelayTicks;
        [DataMember]
        public int? homingTurnLutSlotsPerTick;
    }

    [DataContract]
    internal sealed class PrimaryWeaponFamilyDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string displayName;
        [DataMember]
        public string description;
        [DataMember]
        public string weaponType;
        [DataMember]
        public int? baseDamage;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? minimumFireIntervalTicks;
        [DataMember]
        public int? rapidFireStartLevel;
        [DataMember]
        public int? fireIntervalReductionPerLevel;
        [DataMember]
        public decimal? projectileSpeed;
        [DataMember]
        public decimal? projectileHalfWidth;
        [DataMember]
        public decimal? projectileHalfHeight;
        [DataMember]
        public int? pierceEnemyCount;
        [DataMember]
        public int? spreadWays;
        [DataMember]
        public int? spreadStepLutSlots;
        [DataMember]
        public int[] shotAngleLutSlots;
        [DataMember]
        public PrimaryWeaponLevelDto[] levels;
        [DataMember]
        public PrimaryWeaponLevelDto[] evolutionLevels;
    }

    [DataContract]
    internal sealed class PrimaryWeaponLevelDto
    {
        [DataMember]
        public int? level;
        [DataMember]
        public int[] shotAngleLutSlots;
        [DataMember]
        public int? spreadWays;
        [DataMember]
        public int? spreadStepLutSlots;
        [DataMember]
        public int? burstCount;
        [DataMember]
        public int? burstIntervalTicks;
        [DataMember]
        public int? pierceEnemyCount;
        [DataMember]
        public int? pulseMinStepLutSlots;
        [DataMember]
        public int? pulseMaxStepLutSlots;
        [DataMember]
        public int? pulsePeriodTicks;
        [DataMember]
        public int? inertiaVelocityPercent;
        [DataMember]
        public int? impactExplosionDamage;
        [DataMember]
        public decimal? impactExplosionRadius;
        [DataMember]
        public int? minimumFireIntervalTicks;
        [DataMember]
        public int? beamDamagePerTick;
        [DataMember]
        public decimal? beamLength;
        [DataMember]
        public decimal? beamStartHalfWidth;
        [DataMember]
        public decimal? beamGrowthPerTick;
        [DataMember]
        public decimal? beamMaxHalfWidth;
    }

    [DataContract]
    internal sealed class OptionFormationDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? followDelayTicks;
        [DataMember]
        public OptionOffsetDto[] offsets;
        [DataMember]
        public decimal? radius;
        [DataMember]
        public int? angularLutSlotsNumerator;
        [DataMember]
        public int? angularLutSlotsDenominator;
    }

    [DataContract]
    internal sealed class OptionOffsetDto
    {
        [DataMember]
        public decimal? x;
        [DataMember]
        public decimal? y;
    }

    [DataContract]
    internal sealed class ObstacleDto
    {
        [DataMember]
        public string type;
        [DataMember]
        public decimal? x;
        [DataMember]
        public decimal? y;
        [DataMember]
        public int? hp;
        [DataMember]
        public bool? blocksEnemyBullets;
        [DataMember]
        public int? regenDelayTicks;
        [DataMember]
        public LaserAttackDto laser;
    }

    [DataContract]
    internal sealed class BossDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string theme;
        [DataMember]
        public int? stageIndexMin;
        [DataMember]
        public int? stageIndexMax;
        [DataMember]
        public int? difficultyMin;
        [DataMember]
        public int? difficultyMax;
        [DataMember]
        public int? entryLaneMask;
        [DataMember]
        public int? hp;

        // REQ-007/008 보스 전투 필드 — 전부 선택적 (없으면 시뮬 기본값).
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public decimal? holdX;
        [DataMember]
        public BossPhaseDto[] phases;
        [DataMember]
        public BossPartDto[] parts;
        [DataMember]
        public WarshipEncounterDto warship;
        [DataMember]
        public BossFormDto form2;
    }

    [DataContract]
    internal sealed class BossFormDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? transitionTicks;
        [DataMember]
        public int? hp;
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public decimal? holdX;
        [DataMember]
        public BossPhaseDto[] phases;
        [DataMember]
        public BossPartDto[] parts;
    }

    [DataContract]
    internal sealed class WarshipEncounterDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public int? eventEntityId;
        [DataMember]
        public int? warningTicks;
        [DataMember]
        public decimal? originX;
        [DataMember]
        public decimal? originY;
        [DataMember]
        public decimal? scrollSpeedPerSecond;
        [DataMember]
        public int? baseCoreOpeningWays;
        [DataMember]
        public int? waysReductionPerTurret;
        [DataMember]
        public int? minimumCoreOpeningWays;
        [DataMember]
        public WarshipPartGroupDto[] groups;
    }

    [DataContract]
    internal sealed class WarshipPartGroupDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string role;
        [DataMember]
        public string[] partIds;
        [DataMember]
        public int? advanceAfterTicks;
    }

    [DataContract]
    internal sealed class BossPhaseDto
    {
        [DataMember]
        public string pattern;
        [DataMember]
        public int? fireIntervalTicks;
        [DataMember]
        public int? ways;
        [DataMember]
        public decimal? bulletSpeed;
        [DataMember]
        public string movementPattern;
        [DataMember]
        public decimal? movementAmplitude;
        [DataMember]
        public int? movementPeriodTicks;
        [DataMember]
        public int? movementTelegraphTicks;
        [DataMember]
        public string partVulnerability;
        [DataMember]
        public int? durationTicks;
        [DataMember]
        public int? telegraphTicks;
        [DataMember]
        public string projectileKind;
        [DataMember]
        public int? splitAfterTicks;
        [DataMember]
        public int? mineTravelTicks;
        [DataMember]
        public int? mineTelegraphTicks;
        [DataMember]
        public decimal? mineAcceleration;
        [DataMember]
        public string signaturePatternId;
        [DataMember]
        public string signatureSpawnEnemyId;
        [DataMember]
        public int? signatureObstacleHp;
        [DataMember]
        public decimal? signatureGravity;
        [DataMember]
        public int? signatureHomingTurnLutSlotsPerTick;
        [DataMember]
        public LaserAttackDto bossLaser;
        [DataMember]
        public decimal? hpThreshold;
        [DataMember]
        public BossPhasePartRuleDto[] partRules;
        [DataMember]
        public SegmentChainDto segmentChain;
    }

    [DataContract]
    internal sealed class SegmentChainDto
    {
        [DataMember]
        public int? segmentCount;
        [DataMember]
        public int? summonCount;
        [DataMember]
        public int? summonIntervalTicks;
        [DataMember]
        public int? headHp;
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public decimal? moveSpeed;
        [DataMember]
        public int? turnLutSlotsPerTick;
        [DataMember]
        public int? followDelayTicks;
        [DataMember]
        public int? contactDamage;
        [DataMember]
        public decimal? spawnOffsetX;
        [DataMember]
        public decimal? spawnOffsetY;
        [DataMember]
        public string hitRule;
    }

    [DataContract]
    internal sealed class BossPhasePartRuleDto
    {
        [DataMember]
        public string partId;
        [DataMember]
        public bool? active;
        [DataMember]
        public bool? invulnerable;
        [DataMember]
        public BossPartAttackDto attack;
    }

    [DataContract]
    internal sealed class BossPartDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public decimal? offsetX;
        [DataMember]
        public decimal? offsetY;
        [DataMember]
        public decimal? halfWidth;
        [DataMember]
        public decimal? halfHeight;
        [DataMember]
        public int? hp;
        [DataMember]
        public bool? isCore;
        [DataMember]
        public string[] coreGatePartIds;
        [DataMember]
        public int? regenerationTicks;
        [DataMember]
        public BossPartAttackDto attack;
    }

    [DataContract]
    internal sealed class BossPartAttackDto
    {
        [DataMember]
        public string type;
        [DataMember]
        public int? intervalTicks;
        [DataMember]
        public int? ways;
        [DataMember]
        public decimal? bulletSpeed;
        [DataMember]
        public decimal? effectSpeed;
        [DataMember]
        public decimal? effectMaxSpeed;
        [DataMember]
        public decimal? effectOffsetX;
        [DataMember]
        public decimal? effectOffsetY;
        [DataMember]
        public string spawnEnemyId;
        [DataMember]
        public int? contactDamage;
        [DataMember]
        public LaserAttackDto laser;
    }

    [DataContract]
    internal sealed class RewardsDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public int? optionCount;
        [DataMember]
        public RewardDto[] rewards;
        [DataMember]
        public int? maxCombinedModifierCost;
        [DataMember]
        public int? rerollCost;
    }

    [DataContract]
    internal sealed class RewardDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string type;
        [DataMember]
        public string slot;
        [DataMember]
        public int? amount;
        [DataMember]
        public int? weight;
        [DataMember]
        public int? stageIndexMin;
        [DataMember]
        public int? stageIndexMax;
        [DataMember]
        public int? maxPerRun;
        [DataMember]
        public string modifierId;
        [DataMember]
        public string modifierEffect;
        [DataMember]
        public bool? stackable;
        [DataMember]
        public int? maxStacks;
        [DataMember]
        public int? stackStrength;
        [DataMember]
        public int? interactionCost;
        [DataMember]
        public string familyId;
        [DataMember]
        public string formationId;
        [DataMember]
        public string primaryFamilyId;
        [DataMember]
        public string pool;
        [DataMember]
        public RewardCostDto[] costs;
    }

    [DataContract]
    internal sealed class RewardCostDto
    {
        [DataMember]
        public string type;
        [DataMember]
        public int? amount;
    }

    [DataContract]
    internal sealed class ShipsDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public ShipDto[] ships;
    }

    [DataContract]
    internal sealed class ShipDto
    {
        [DataMember]
        public string id;
        [DataMember]
        public string displayName;
        [DataMember]
        public int? moveSpeedMultiplierNumerator;
        [DataMember]
        public int? moveSpeedMultiplierDenominator;
        [DataMember]
        public int[] startingPowerUpLevels;
        [DataMember]
        public long? unlockCost;
        [DataMember]
        public string weaponType;
        [DataMember]
        public int? maxHp;
        [DataMember]
        public int? startingShieldStock;
        [DataMember]
        public string gaugeWeaponFamily;
        [DataMember]
        public string[] powerUpGaugeSlots;
        [DataMember]
        public string missileFamily;
        [DataMember]
        public string optionFormation;
    }

    [DataContract]
    internal sealed class ScoringDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public int? grazeRadiusSubUnits;
        [DataMember]
        public int? grazeScore;
        [DataMember]
        public int? grazeGaugeCharge;
        [DataMember]
        public int[] multiplierGaugeRequirements;
        [DataMember]
        public int? multiplierDecayTicks;
        [DataMember]
        public int? shieldBonusScorePerStock;
    }

    [DataContract]
    internal sealed class PlayerRootDto
    {
        [DataMember]
        public int? schemaVersion;
        [DataMember]
        public PlayerDto player;
    }

    [DataContract]
    internal sealed class PlayerDto
    {
        [DataMember]
        public int? maxEnemyBullets;
    }
}

#pragma warning restore CS0649
