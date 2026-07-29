using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public static class SimSpace
    {
        public const int SubUnitsPerWorldUnit = 256;
        public const int TicksPerSecond = 60;

        // 640×360 뷰(월드 40×22.5u) 기준 플레이필드 (ROADMAP M0, REQ-005 요청 2).
        // 스폰/컬링 경계는 이 상수를 기준으로 파생시킨다.
        public const int PlayfieldHalfWidthSubUnits = 20 * SubUnitsPerWorldUnit;
        public const int PlayfieldHalfHeightSubUnits = 45 * SubUnitsPerWorldUnit / 4;
        public const int DespawnMarginSubUnits = 2 * SubUnitsPerWorldUnit;
    }

    /// <summary>
    /// 틱 처리 중 발생한 이산 사건 (REQ-005 요청 1). Presentation이 애니메이션/SFX로
    /// 번역한다 — 상태 차분으로 추측하지 말 것. 보스 계열 값은 보스 시스템용 예약.
    /// </summary>
    public enum SimEventType
    {
        EnemyHit = 0,
        EnemyKilled = 1,
        PlayerHit = 2,
        PlayerKilled = 3,
        CapsuleDropped = 4,
        CapsulePicked = 5,
        PowerUpLevelChanged = 6,
        BossSpawned = 7,
        BossPhaseChanged = 8,
        StageCleared = 9,
        /// <summary>Arg = (int)BulletKind. 발사음 SFX용 — 볼리당 1회.</summary>
        PlayerFired = 10,
        /// <summary>EntityId = bullet id, Arg = target enemy id.</summary>
        BulletRicocheted = 11,
        /// <summary>EntityId = killed enemy id, X/Y = explosion center, Arg = damage.</summary>
        KillExplosionTriggered = 12,
        /// <summary>EntityId = enemy bullet id, X/Y = bullet position, Arg = fixed graze score.</summary>
        GrazeScored = 13,
        /// <summary>EntityId = zero-based multiplier level, Arg = score multiplier.</summary>
        MultiplierChanged = 14
    }

    /// <summary>One event that happened during the last Step. Coordinates are subunits.</summary>
    public readonly struct SimEvent
    {
        public SimEvent(SimEventType type, int entityId, int x, int y, int arg)
        {
            Type = type;
            EntityId = entityId;
            X = x;
            Y = y;
            Arg = arg;
        }

        public SimEventType Type { get; }
        public int EntityId { get; }
        public int X { get; }
        public int Y { get; }
        /// <summary>타입별 부가값 — EnemyHit/Killed·PlayerHit: 데미지, PowerUpLevelChanged: 새 레벨(EntityId=슬롯).</summary>
        public int Arg { get; }
    }

    /// <summary>
    /// Read-only counters observed from one BattleSim instance.
    /// Accuracy is intentionally left to consumers as ShotsHit / ShotsFired.
    /// </summary>
    public readonly struct BattleStatistics
    {
        internal BattleStatistics(
            long shotsFired,
            long shotsHit,
            long kills,
            long capsulesCollected,
            long grazeCount)
        {
            ShotsFired = shotsFired;
            ShotsHit = shotsHit;
            Kills = kills;
            CapsulesCollected = capsulesCollected;
            GrazeCount = grazeCount;
        }

        public long ShotsFired { get; }
        public long ShotsHit { get; }
        public long Kills { get; }
        public long CapsulesCollected { get; }
        public long GrazeCount { get; }
    }

    public enum BulletFaction { Player = 0, Enemy = 1 }
    public enum BulletKind { MainShot = 0, Missile = 1, EnemyShot = 2 }

    [Flags]
    public enum BattleModifier
    {
        None = 0,
        PierceShot = 1 << 0,
        Ricochet = 1 << 1,
        HomingMissile = 1 << 2,
        KillExplosion = 1 << 3
    }

    internal static class BattleModifierRules
    {
        internal const BattleModifier All =
            BattleModifier.PierceShot
            | BattleModifier.Ricochet
            | BattleModifier.HomingMissile
            | BattleModifier.KillExplosion;

        internal static bool IsSingleKnown(BattleModifier modifier)
        {
            int value = (int)modifier;
            return modifier != BattleModifier.None
                && (modifier & ~All) == 0
                && (value & (value - 1)) == 0;
        }
    }

    /// <summary>Observable boss state (REQ-007). Valid only while IBattleSim.BossActive.</summary>
    public readonly struct BossState
    {
        public BossState(int id, int x, int y, int hp, int maxHp, int phase)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = hp;
            MaxHp = maxHp;
            Phase = phase;
        }

        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Phase { get; }
    }

    /// <summary>One tick of digital input. Movement is clamped to -1, 0, or 1 per axis.</summary>
    public readonly struct InputCommand
    {
        public InputCommand(int moveX, int moveY, bool fire)
            : this(moveX, moveY, fire, false)
        {
        }

        public InputCommand(
            int moveX,
            int moveY,
            bool fire,
            bool activate)
        {
            MoveX = Clamp(moveX);
            MoveY = Clamp(moveY);
            Fire = fire;
            Activate = activate;
        }

        public int MoveX { get; }
        public int MoveY { get; }
        public bool Fire { get; }
        public bool Activate { get; }
        public static InputCommand None => default;
        static int Clamp(int value) => value < 0 ? -1 : value > 0 ? 1 : 0;
    }

    /// <summary>Observable bullet state in integer simulation subunits.</summary>
    public readonly struct BulletState
    {
        public BulletState(int id, BulletFaction faction, int x, int y)
            : this(id, faction, BulletKind.MainShot, x, y)
        {
        }

        public BulletState(int id, BulletFaction faction, BulletKind kind, int x, int y)
        {
            Id = id;
            Faction = faction;
            Kind = kind;
            X = x;
            Y = y;
        }

        public int Id { get; }
        public BulletFaction Faction { get; }
        public BulletKind Kind { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>Observable option position in integer simulation subunits.</summary>
    public readonly struct OptionState
    {
        public OptionState(int index, int x, int y)
        {
            Index = index;
            X = x;
            Y = y;
        }

        /// <summary>Stable one-based index matching the option's gauge level.</summary>
        public int Index { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>Observable enemy state in integer simulation subunits.</summary>
    public readonly struct EnemyState
    {
        public EnemyState(int id, string definitionId, int x, int y, int hp)
        {
            Id = id;
            DefinitionId = definitionId;
            X = x;
            Y = y;
            Hp = hp;
        }

        public int Id { get; }
        public string DefinitionId { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
    }

    /// <summary>Observable capsule state in integer simulation subunits.</summary>
    public readonly struct CapsuleState
    {
        public CapsuleState(int id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
        }

        public int Id { get; }
        public int X { get; }
        public int Y { get; }
    }

    /// <summary>Integer-only tuning. Fractional speeds use numerator/denominator pairs.</summary>
    public sealed class BattleSimConfig
    {
        internal const int DefaultMaxEnemyBullets = 128;

        int _playerSpeedNumerator, _bulletSpeedNumerator;
        int _playerSpeedDenominator = 1, _bulletSpeedDenominator = 1;

        /// <summary>Whole subunits/tick shorthand. Setting it resets the denominator to 1.</summary>
        public int PlayerSpeedPerTick
        {
            get => _playerSpeedNumerator / _playerSpeedDenominator;
            set { _playerSpeedNumerator = value; _playerSpeedDenominator = 1; }
        }

        public int PlayerSpeedNumerator { get => _playerSpeedNumerator; set => _playerSpeedNumerator = value; }
        public int PlayerSpeedDenominator { get => _playerSpeedDenominator; set => _playerSpeedDenominator = value; }

        /// <summary>Legacy whole subunits/tick shorthand for the stage-less constructor.</summary>
        public int PlayerBulletSpeedPerTick
        {
            get => _bulletSpeedNumerator / _bulletSpeedDenominator;
            set { _bulletSpeedNumerator = value; _bulletSpeedDenominator = 1; }
        }

        public int PlayerBulletSpeedNumerator { get => _bulletSpeedNumerator; set => _bulletSpeedNumerator = value; }
        public int PlayerBulletSpeedDenominator { get => _bulletSpeedDenominator; set => _bulletSpeedDenominator = value; }
        public WeaponType PlayerWeaponType { get; set; } = WeaponType.Vulcan;
        public int MainShotBaseDamage { get; set; }
        public int FireIntervalTicks { get; set; }
        public int MainShotHalfWidth { get; set; }
        public int MainShotHalfHeight { get; set; }
        internal bool UseConfiguredMainShotStats { get; set; }
        public int MaxBullets { get; set; }
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int EnemyDespawnX { get; set; } = int.MinValue;
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }
        public int PlayerMaxHp { get; set; } = 1;
        public int PlayerHalfWidth { get; set; }
        public int PlayerHalfHeight { get; set; }
        public int CapsuleHalfWidth { get; set; }
        public int CapsuleHalfHeight { get; set; }
        public int CapsuleNoDropWeight { get; set; }
        public int ScrollSpeedNumerator { get; set; }
        public int ScrollSpeedDenominator { get; set; } = 1;
        /// <summary>
        /// Provisional run difficulty tuning (REQ-020, AGENTS.md section 7).
        /// Applied with deterministic ceiling to regular-enemy and boss HP only.
        /// </summary>
        public int EnemyHpMultiplierNumerator { get; set; } = 1;
        public int EnemyHpMultiplierDenominator { get; set; } = 1;

        // Provisional power-up tuning. These are deliberately configurable until
        // the human balance pass replaces them with approved GameData values.
        public int MainShotRapidFireStartLevel { get; set; } = 3;
        public int MainShotFireIntervalReductionPerLevel { get; set; } = 1;
        public int MainShotMinimumFireIntervalTicks { get; set; } = 4;

        // Provisional primary-family profiles (REQ-022, AGENTS.md section 7).
        // RunManager copies the selected profile into the resolved main-shot
        // fields above, so passive rewards and suspend checkpoints keep using
        // one stable set of integers.
        public int LaserBaseDamage { get; set; } = 20;
        public int LaserFireIntervalTicks { get; set; } = 16;
        public int LaserRapidFireStartLevel { get; set; } = 2;
        public int LaserFireIntervalReductionPerLevel { get; set; } = 2;
        public int LaserMinimumFireIntervalTicks { get; set; } = 10;
        public int LaserSpeedNumerator { get; set; } =
            32 * SimSpace.SubUnitsPerWorldUnit;
        public int LaserSpeedDenominator { get; set; } =
            SimSpace.TicksPerSecond;
        public int LaserHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int LaserHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 16;
        /// <summary>Enemies passed after the first laser hit.</summary>
        public int LaserPierceEnemyCount { get; set; } = 2;

        public int SpreadBaseDamage { get; set; } = 6;
        public int SpreadFireIntervalTicks { get; set; } = 10;
        public int SpreadRapidFireStartLevel { get; set; } = 3;
        public int SpreadFireIntervalReductionPerLevel { get; set; } = 1;
        public int SpreadMinimumFireIntervalTicks { get; set; } = 6;
        public int SpreadSpeedNumerator { get; set; } =
            18 * SimSpace.SubUnitsPerWorldUnit;
        public int SpreadSpeedDenominator { get; set; } =
            SimSpace.TicksPerSecond;
        public int SpreadHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 4;
        public int SpreadHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 8;
        public int SpreadWays { get; set; } = 3;
        /// <summary>Angular spacing in 1/64-turn SineLut slots.</summary>
        public int SpreadStepLutSlots { get; set; } = 2;
        public int MissileBaseDamage { get; set; } = 2;
        public int MissileFireIntervalTicks { get; set; } = 45;
        public int MissileRapidFireStartLevel { get; set; } = 2;
        public int MissileFireIntervalReductionPerLevel { get; set; } = 5;
        public int MissileMinimumFireIntervalTicks { get; set; } = 30;
        public int MissileSpeedXNumerator { get; set; } = 13 * SimSpace.SubUnitsPerWorldUnit;
        public int MissileSpeedXDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int MissileFallSpeedYNumerator { get; set; } = 5 * SimSpace.SubUnitsPerWorldUnit;
        public int MissileFallSpeedYDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int MissileHalfWidth { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 8;
        public int MissileHalfHeight { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        /// <summary>
        /// Player-position history distance between consecutive options.
        /// Option N follows the position from N * OptionFollowDelayTicks ago.
        /// </summary>
        public int OptionFollowDelayTicks { get; set; } = 12;

        // 적탄 잠정값 (REQ-007) — GameData 이관 전까지 여기서 조절.
        public int EnemyBulletSpeedNumerator { get; set; } = 8 * SimSpace.SubUnitsPerWorldUnit;
        public int EnemyBulletSpeedDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int EnemyBulletHalfWidth { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        public int EnemyBulletHalfHeight { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        public int EnemyBulletDamage { get; set; } = 1;
        /// <summary>적탄 전용 예산 — 플레이어 탄 풀(MaxBullets)을 잠식하지 않는다.</summary>
        public int MaxEnemyBullets { get; set; } = DefaultMaxEnemyBullets;

        // Provisional synergy tuning (REQ-013, AGENTS.md §7). These stay
        // configurable until the human/GROK balance pass approves authoritative
        // GameData values.
        public int PierceShotEnemyCount { get; set; } = 1;
        public int RicochetRangeSubUnits { get; set; } =
            8 * SimSpace.SubUnitsPerWorldUnit;
        /// <summary>Maximum homing turn per tick in 1/64-turn SineLut slots.</summary>
        public int HomingMissileTurnLutSlotsPerTick { get; set; } = 1;
        public int KillExplosionRadiusSubUnits { get; set; } =
            3 * SimSpace.SubUnitsPerWorldUnit / 2;
        public int KillExplosionDamage { get; set; } = 1;
        public int KillExplosionMaxTargets { get; set; } = 4;

        // Fallback graze/combo scoring tuning (REQ-015/016, AGENTS.md §7).
        // Optional scoring.json values replace these through GameDataSet.
        public int GrazeExtraRadiusSubUnits { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int GrazeScore { get; set; } = 10;
        public int GrazeComboGaugeGain { get; set; } = 1;
        public int KillComboGaugeGain { get; set; } = 10;
        public int ComboGaugeRequiredForLevel2 { get; set; } = 30;
        public int ComboGaugeRequiredForLevel3 { get; set; } = 50;
        public int ComboGaugeRequiredForLevel4 { get; set; } = 80;
        public int ComboDecayTicks { get; set; } = 300;
        public int ComboMultiplierLevel1 { get; set; } = 1;
        public int ComboMultiplierLevel2 { get; set; } = 2;
        public int ComboMultiplierLevel3 { get; set; } = 4;
        public int ComboMultiplierLevel4 { get; set; } = 8;

        /// <summary>
        /// Defaults sourced from player.json, main_shot, and the 40 by 22.5 unit view
        /// (640×360, ROADMAP M0). Spatial values scale the 24×14 originals by ×5/3
        /// (hitboxes ×1.5 to follow the sprite upsize). Power-up values remain
        /// provisional pending the human balance pass.
        /// </summary>
        public static BattleSimConfig CreateDefault()
        {
            const int u = SimSpace.SubUnitsPerWorldUnit;
            return new BattleSimConfig
            {
                PlayerSpeedNumerator = 13 * u,
                PlayerSpeedDenominator = SimSpace.TicksPerSecond,
                PlayerBulletSpeedNumerator = 20 * u,
                PlayerBulletSpeedDenominator = SimSpace.TicksPerSecond,
                MainShotBaseDamage = 10,
                FireIntervalTicks = 8,
                MainShotHalfWidth = 3 * u / 8,
                MainShotHalfHeight = 9 * u / 64,
                MaxBullets = 64,
                PlayerMinX = -39 * u / 2,
                PlayerMaxX = 39 * u / 2,
                PlayerMinY = -43 * u / 4,
                PlayerMaxY = 43 * u / 4,
                BulletDespawnX = SimSpace.PlayfieldHalfWidthSubUnits + u,
                EnemyDespawnX = -(SimSpace.PlayfieldHalfWidthSubUnits + SimSpace.DespawnMarginSubUnits),
                PlayerSpawnX = -13 * u,
                PlayerSpawnY = 0,
                PlayerMaxHp = 1,
                PlayerHalfWidth = 3 * u / 8,
                PlayerHalfHeight = 3 * u / 8
            };
        }

        internal BattleSimConfig Copy()
        {
            return (BattleSimConfig)MemberwiseClone();
        }
    }

    public interface IBattleSim
    {
        int Tick { get; }
        /// <summary>Score earned in this battle instance.</summary>
        long Score { get; }
        /// <summary>Zero-based combo level: 0, 1, 2, or 3.</summary>
        int MultiplierLevel { get; }
        int ScoreMultiplier { get; }
        int ComboGauge { get; }
        BattleStatistics Statistics { get; }
        long ScrollX { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        int PlayerHp { get; }
        int ShieldRemaining { get; }
        WeaponType PlayerWeaponType { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        IReadOnlyList<OptionState> Options { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        IReadOnlyList<CapsuleState> Capsules { get; }
        /// <summary>Events emitted by the most recent Step. Cleared at the start of each Step.</summary>
        ReadOnlySpan<SimEvent> EventsThisTick { get; }
        /// <summary>보스전 진행 중 여부. false면 Boss 값은 무의미하다.</summary>
        bool BossActive { get; }
        BossState Boss { get; }
        void Step(in InputCommand input);
    }

    /// <summary>Deterministic integer-only combat and generated-stage simulation.</summary>
    public sealed class BattleSim : IBattleSim
    {
        const int DropRngStream = 1;
        const int SineScale = 1024;
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
        readonly int _playerSpeedNumerator, _playerSpeedDenominator;
        readonly int _bulletSpeedNumerator, _bulletSpeedDenominator;
        readonly int _fireIntervalTicks, _maxBullets;
        readonly WeaponType _playerWeaponType;
        readonly int _mainShotBasePierceEnemyCount;
        readonly int _spreadWays, _spreadStepLutSlots;
        readonly int _mainShotRapidFireStartLevel;
        readonly int _mainShotFireIntervalReductionPerLevel;
        readonly int _mainShotMinimumFireIntervalTicks;
        readonly int _missileBaseDamage, _missileFireIntervalTicks, _missileRapidFireStartLevel;
        readonly int _missileFireIntervalReductionPerLevel, _missileMinimumFireIntervalTicks;
        readonly int _missileSpeedXNumerator, _missileSpeedXDenominator;
        readonly int _missileFallSpeedYNumerator, _missileFallSpeedYDenominator;
        readonly int _missileHalfWidth, _missileHalfHeight;
        readonly int _optionFollowDelayTicks;
        readonly int _playerMinX, _playerMaxX, _playerMinY, _playerMaxY;
        readonly int _bulletDespawnX, _enemyDespawnX;
        readonly int _playerHalfWidth, _playerHalfHeight;
        readonly int _capsuleHalfWidth, _capsuleHalfHeight;
        readonly int _capsuleNoDropWeight;
        readonly int _scrollSpeedNumerator, _scrollSpeedDenominator;
        readonly int _enemyHpMultiplierNumerator;
        readonly int _enemyHpMultiplierDenominator;
        readonly int _playerBulletDamage, _playerBulletHalfWidth, _playerBulletHalfHeight;
        readonly PowerUpGauge _powerUpGauge;
        readonly Rng _dropRng;
        readonly List<BulletState> _bullets;
        readonly List<int> _bulletXRemainders;
        readonly List<int> _bulletYRemainders;
        // 적탄 조준 벡터: 서브유닛/틱 = (numX, numY) / den. 플레이어 탄은 den 0 (kind 기반 속도).
        readonly List<int> _bulletVelXNumerators;
        readonly List<int> _bulletVelYNumerators;
        readonly List<int> _bulletVelDenominators;
        readonly List<int> _bulletPiercesRemaining;
        readonly List<int> _bulletRicochetUsed;
        readonly List<int> _bulletHomingTargetIds;
        readonly List<byte> _bulletGrazeScored;
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
        readonly List<CapsuleState> _capsules;
        readonly ReadOnlyCollection<CapsuleState> _readOnlyCapsules;
        readonly ScheduledSpawn[] _scheduledSpawns;

        // 적탄 설정 (config 스냅숏)
        readonly int _enemyBulletSpeedNumerator, _enemyBulletSpeedDenominator;
        readonly int _enemyBulletHalfWidth, _enemyBulletHalfHeight;
        readonly int _enemyBulletDamage, _maxEnemyBullets;
        readonly BattleModifier _activeModifiers;
        readonly int _pierceShotEnemyCount, _ricochetRangeSubUnits;
        readonly int _homingMissileTurnLutSlotsPerTick;
        readonly int _killExplosionRadiusSubUnits, _killExplosionDamage;
        readonly int _killExplosionMaxTargets;
        readonly int _grazeExtraRadiusSubUnits, _grazeScore;
        readonly int _grazeComboGaugeGain, _killComboGaugeGain;
        readonly int _comboDecayTicks;
        readonly int[] _comboGaugeRequirements;
        readonly int[] _comboMultipliers;

        // 보스 (REQ-007). _bossMaxHp == 0 이면 이 스테이지에 보스전 없음.
        readonly int _bossMaxHp, _bossHalfWidth, _bossHalfHeight, _bossHoldX;
        readonly IReadOnlyList<Generation.BossPhase> _bossPhases;
        readonly int _stageTotalTicks;
        bool _bossSpawned, _bossDefeated;
        int _bossId, _bossX, _bossY, _bossHp, _bossPhase, _bossAge, _bossFireCooldown;

        const int BossEntrySpeedPerTick = 16;                          // 서브유닛/틱
        const int BossHoverAmplitude = 3 * SimSpace.SubUnitsPerWorldUnit;
        const int BossHoverPeriodShift = 2;                            // age >> 2 → 약 4.3초 주기
        const int SpreadStepLutSlots = 2;                              // n-way 간격 = 11.25°

        readonly SimEvent[] _events;
        readonly int[] _enemyScanIds;
        readonly long[] _enemyScanDistances;
        int _eventCount;
        long _shotsFired, _shotsHit, _kills, _capsulesCollected, _grazeCount;

        int _playerXRemainder, _playerYRemainder, _cooldown, _missileCooldown;
        int _mainShotLevel, _missileLevel, _optionLevel, _shieldGaugeLevel;
        int _nextBulletId = 1;
        int _nextEnemyId = 1;
        int _nextCapsuleId = 1;
        int _nextScheduledSpawn;
        int _playerHistoryHead;
        int _playerHistoryCount;
        int _bulletHitRecordCount;
        int _multiplierLevel, _comboGauge, _ticksSinceLastKill;
        bool _killScoredThisTick, _activateHeld;

        /// <summary>Backward-compatible stage-less player movement and basic-shot simulation.</summary>
        public BattleSim(BattleSimConfig config, Rng rng)
            : this(
                config,
                rng,
                null,
                null,
                null,
                BattleModifier.None,
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
                BattleModifier.None,
                true)
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
                activeModifiers,
                true)
        {
        }

        BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifier activeModifiers,
            bool stageEnabled)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (stageEnabled && stagePlan == null) throw new ArgumentNullException(nameof(stagePlan));
            if (stageEnabled && content == null) throw new ArgumentNullException(nameof(content));
            if (stageEnabled && powerUpGauge == null) throw new ArgumentNullException(nameof(powerUpGauge));
            Validate(config);
            if ((activeModifiers & ~BattleModifierRules.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(activeModifiers));

            _playerSpeedNumerator = config.PlayerSpeedNumerator;
            _playerSpeedDenominator = config.PlayerSpeedDenominator;
            _maxBullets = config.MaxBullets;
            _playerWeaponType = config.PlayerWeaponType;
            _mainShotBasePierceEnemyCount =
                _playerWeaponType == WeaponType.Laser
                    ? config.LaserPierceEnemyCount
                    : 0;
            _spreadWays = config.SpreadWays;
            _spreadStepLutSlots = config.SpreadStepLutSlots;
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
            _optionFollowDelayTicks = config.OptionFollowDelayTicks;
            _playerMinX = config.PlayerMinX;
            _playerMaxX = config.PlayerMaxX;
            _playerMinY = config.PlayerMinY;
            _playerMaxY = config.PlayerMaxY;
            _bulletDespawnX = config.BulletDespawnX;
            _enemyDespawnX = config.EnemyDespawnX;
            _playerHalfWidth = config.PlayerHalfWidth;
            _playerHalfHeight = config.PlayerHalfHeight;
            _capsuleHalfWidth = config.CapsuleHalfWidth;
            _capsuleHalfHeight = config.CapsuleHalfHeight;
            _capsuleNoDropWeight = config.CapsuleNoDropWeight;
            _scrollSpeedNumerator = config.ScrollSpeedNumerator;
            _scrollSpeedDenominator = config.ScrollSpeedDenominator;
            _enemyHpMultiplierNumerator =
                config.EnemyHpMultiplierNumerator;
            _enemyHpMultiplierDenominator =
                config.EnemyHpMultiplierDenominator;
            _enemyBulletSpeedNumerator = config.EnemyBulletSpeedNumerator;
            _enemyBulletSpeedDenominator = config.EnemyBulletSpeedDenominator;
            _enemyBulletHalfWidth = config.EnemyBulletHalfWidth;
            _enemyBulletHalfHeight = config.EnemyBulletHalfHeight;
            _enemyBulletDamage = config.EnemyBulletDamage;
            _maxEnemyBullets = config.MaxEnemyBullets;
            _activeModifiers = activeModifiers;
            _pierceShotEnemyCount = config.PierceShotEnemyCount;
            _ricochetRangeSubUnits = config.RicochetRangeSubUnits;
            _homingMissileTurnLutSlotsPerTick =
                config.HomingMissileTurnLutSlotsPerTick;
            _killExplosionRadiusSubUnits = config.KillExplosionRadiusSubUnits;
            _killExplosionDamage = config.KillExplosionDamage;
            _killExplosionMaxTargets = config.KillExplosionMaxTargets;
            _grazeExtraRadiusSubUnits = config.GrazeExtraRadiusSubUnits;
            _grazeScore = config.GrazeScore;
            _grazeComboGaugeGain = config.GrazeComboGaugeGain;
            _killComboGaugeGain = config.KillComboGaugeGain;
            _comboDecayTicks = config.ComboDecayTicks;
            _comboGaugeRequirements = new[]
            {
                config.ComboGaugeRequiredForLevel2,
                config.ComboGaugeRequiredForLevel3,
                config.ComboGaugeRequiredForLevel4
            };
            _comboMultipliers = new[]
            {
                config.ComboMultiplierLevel1,
                config.ComboMultiplierLevel2,
                config.ComboMultiplierLevel3,
                config.ComboMultiplierLevel4
            };
            _powerUpGauge = powerUpGauge;
            _dropRng = rng.Fork(DropRngStream);

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
            }
            else
            {
                _bossPhases = Array.Empty<Generation.BossPhase>();
            }

            if (stageEnabled)
            {
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
                _stageTotalTicks = (int)Math.Min(totalTicks, int.MaxValue);
            }
            else
            {
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
            }

            int bulletCapacity = _maxBullets + _maxEnemyBullets;
            _bullets = new List<BulletState>(bulletCapacity);
            _bulletXRemainders = new List<int>(bulletCapacity);
            _bulletYRemainders = new List<int>(bulletCapacity);
            _bulletVelXNumerators = new List<int>(bulletCapacity);
            _bulletVelYNumerators = new List<int>(bulletCapacity);
            _bulletVelDenominators = new List<int>(bulletCapacity);
            _bulletPiercesRemaining = new List<int>(bulletCapacity);
            _bulletRicochetUsed = new List<int>(bulletCapacity);
            _bulletHomingTargetIds = new List<int>(bulletCapacity);
            _bulletGrazeScored = new List<byte>(bulletCapacity);
            long hitRecordCapacity =
                (long)_maxBullets
                * (_mainShotBasePierceEnemyCount
                    + _pierceShotEnemyCount
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
            _options = new List<OptionState>(maxOptionLevel);
            _readOnlyOptions = _options.AsReadOnly();
            long historyCapacity = (long)maxOptionLevel * _optionFollowDelayTicks + 1;
            if (historyCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.OptionFollowDelayTicks),
                    "Option history capacity exceeds the supported range.");
            _playerHistoryX = new int[(int)historyCapacity];
            _playerHistoryY = new int[(int)historyCapacity];
            int spawnCapacity = _scheduledSpawns.Length;
            _enemies = new List<EnemyState>(spawnCapacity);
            _enemyDefinitions = new List<EnemyDefinition>(spawnCapacity);
            _enemyXRemainders = new List<int>(spawnCapacity);
            _enemySpawnYs = new List<int>(spawnCapacity);
            _enemyAges = new List<int>(spawnCapacity);
            _enemyDiveTargetYs = new List<int>(spawnCapacity);
            _enemyMovementFlags = new List<byte>(spawnCapacity);
            _readOnlyEnemies = _enemies.AsReadOnly();
            _capsules = new List<CapsuleState>(spawnCapacity);
            _readOnlyCapsules = _capsules.AsReadOnly();
            _enemyScanIds = new int[spawnCapacity];
            _enemyScanDistances = new long[spawnCapacity];
            long eventCapacity = 64L
                + 3L * spawnCapacity
                + 2L * bulletCapacity;
            if (eventCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(stagePlan),
                    "The no-allocation event capacity exceeds the supported range.");
            _events = new SimEvent[(int)eventCapacity];

            PlayerX = config.PlayerSpawnX;
            PlayerY = config.PlayerSpawnY;
            PlayerHp = config.PlayerMaxHp;
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
        public BattleStatistics Statistics => new BattleStatistics(
            _shotsFired,
            _shotsHit,
            _kills,
            _capsulesCollected,
            _grazeCount);
        public long ScrollX => GetScrollXAtTick(Tick);
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public int PlayerHp { get; private set; }
        public int ShieldRemaining { get; private set; }
        public WeaponType PlayerWeaponType => _playerWeaponType;
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;
        public IReadOnlyList<OptionState> Options => _readOnlyOptions;
        public IReadOnlyList<EnemyState> Enemies => _readOnlyEnemies;
        public IReadOnlyList<CapsuleState> Capsules => _readOnlyCapsules;
        public ReadOnlySpan<SimEvent> EventsThisTick => new ReadOnlySpan<SimEvent>(_events, 0, _eventCount);
        public bool BossActive => _bossSpawned && !_bossDefeated;
        public BossState Boss => new BossState(_bossId, _bossX, _bossY, _bossHp, _bossMaxHp, _bossPhase);
        /// <summary>보스전이 예정된 스테이지인지 (RunManager가 종료 조건 분기에 쓴다).</summary>
        public bool HasBossBattle => _bossMaxHp > 0;
        public bool BossDefeated => _bossDefeated;

        /// <summary>Returns scroll at any tick using only immutable speed and the tick argument.</summary>
        public long GetScrollXAtTick(int tick)
        {
            return ComputeScrollX(tick, _scrollSpeedNumerator, _scrollSpeedDenominator);
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
            _killScoredThisTick = false;

            PlayerX = AdvancePlayerAxis(PlayerX, input.MoveX, ref _playerXRemainder, _playerMinX, _playerMaxX);
            PlayerY = AdvancePlayerAxis(PlayerY, input.MoveY, ref _playerYRemainder, _playerMinY, _playerMaxY);
            RecordPlayerPosition();
            bool activatePressed = input.Activate && !_activateHeld;
            _activateHeld = input.Activate;
            if (activatePressed && _powerUpGauge != null)
                _powerUpGauge.Activate();
            ReadPowerUpLevels();
            UpdateOptionPositions();
            AdvanceBullets();
            AdvanceEnemies();
            AdvanceCapsules();
            SpawnScheduledThroughTick(Tick);
            UpdateBoss();
            ResolvePlayerBulletEnemyCollisions();
            ResolvePlayerBulletBossCollisions();
            ResolveEnemyBulletPlayerCollisions();
            ResolveEnemyPlayerCollisions();
            ResolveCapsulePlayerCollisions();
            AdvanceComboDecay();

            if (_cooldown > 0) _cooldown--;
            if (_missileCooldown > 0) _missileCooldown--;
            if (input.Fire)
            {
                if (_cooldown == 0 && CountPlayerBullets() < _maxBullets)
                    SpawnMainShotVolley();
                if (_missileLevel > 0
                    && _missileCooldown == 0
                    && CountPlayerBullets() < _maxBullets)
                    SpawnMissile();
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

        void AppendEvent(SimEventType type, int entityId, int x, int y, int arg)
        {
            if (_eventCount == _events.Length)
                throw new InvalidOperationException(
                    "The preallocated simulation event buffer is exhausted.");
            _events[_eventCount++] = new SimEvent(type, entityId, x, y, arg);
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
                ShieldRemaining = 0;
                return;
            }

            int previousMainShot = _mainShotLevel;
            int previousMissile = _missileLevel;
            int previousOption = _optionLevel;
            _mainShotLevel = _powerUpGauge.GetLevel(PowerUpSlot.MainShot);
            _missileLevel = _powerUpGauge.GetLevel(PowerUpSlot.Missile);
            _optionLevel = _powerUpGauge.GetLevel(PowerUpSlot.Option);
            EmitLevelChange(PowerUpSlot.MainShot, previousMainShot, _mainShotLevel);
            EmitLevelChange(PowerUpSlot.Missile, previousMissile, _missileLevel);
            EmitLevelChange(PowerUpSlot.Option, previousOption, _optionLevel);
            int nextShieldLevel = _powerUpGauge.GetLevel(PowerUpSlot.Shield);
            EmitLevelChange(PowerUpSlot.Shield, _shieldGaugeLevel, nextShieldLevel);
            if (nextShieldLevel > _shieldGaugeLevel)
                ShieldRemaining = nextShieldLevel;
            else if (ShieldRemaining > nextShieldLevel)
                ShieldRemaining = nextShieldLevel;
            _shieldGaugeLevel = nextShieldLevel;
        }

        void UpdateOptionPositions()
        {
            while (_options.Count > _optionLevel)
                _options.RemoveAt(_options.Count - 1);
            while (_options.Count < _optionLevel)
                _options.Add(default);

            for (int i = 0; i < _options.Count; i++)
            {
                int index = i + 1;
                GetPlayerPositionAgo(
                    checked(index * _optionFollowDelayTicks),
                    out int x,
                    out int y);
                _options[i] = new OptionState(index, x, y);
            }
        }

        void RecordPlayerPosition()
        {
            if (_playerHistoryCount > 0)
                _playerHistoryHead = (_playerHistoryHead + 1) % _playerHistoryX.Length;
            _playerHistoryX[_playerHistoryHead] = PlayerX;
            _playerHistoryY[_playerHistoryHead] = PlayerY;
            if (_playerHistoryCount < _playerHistoryX.Length)
                _playerHistoryCount++;
        }

        void GetPlayerPositionAgo(int ticksAgo, out int x, out int y)
        {
            int availableTicksAgo = Math.Min(ticksAgo, _playerHistoryCount - 1);
            int historyIndex = _playerHistoryHead - availableTicksAgo;
            if (historyIndex < 0)
                historyIndex += _playerHistoryX.Length;
            x = _playerHistoryX[historyIndex];
            y = _playerHistoryY[historyIndex];
        }

        int AdvancePlayerAxis(int position, int direction, ref int remainder, int min, int max)
        {
            if (direction == 0) return position;
            long accumulated = remainder + (long)direction * _playerSpeedNumerator;
            long candidate = position + accumulated / _playerSpeedDenominator;
            int nextRemainder = (int)(accumulated % _playerSpeedDenominator);
            if (direction < 0 && candidate <= min) { remainder = 0; return min; }
            if (direction > 0 && candidate >= max) { remainder = 0; return max; }
            remainder = nextRemainder;
            return (int)candidate;
        }

        void AdvanceBullets()
        {
            int despawnY = SimSpace.PlayfieldHalfHeightSubUnits + SimSpace.DespawnMarginSubUnits;
            int write = 0;
            for (int read = 0; read < _bullets.Count; read++)
            {
                BulletState bullet = _bullets[read];
                if (bullet.Kind == BulletKind.Missile
                    && HasModifier(BattleModifier.HomingMissile))
                    UpdateHomingMissile(read, in bullet);
                int xNumerator, xDenominator, yNumerator, yDenominator;
                if (_bulletVelDenominators[read] > 0)
                {
                    // 적탄: 스폰 시 계산된 조준 벡터(REQ-007)
                    xNumerator = _bulletVelXNumerators[read];
                    yNumerator = _bulletVelYNumerators[read];
                    xDenominator = _bulletVelDenominators[read];
                    yDenominator = _bulletVelDenominators[read];
                }
                else
                {
                    bool isMissile = bullet.Kind == BulletKind.Missile;
                    xNumerator = isMissile ? _missileSpeedXNumerator : _bulletSpeedNumerator;
                    xDenominator = isMissile ? _missileSpeedXDenominator : _bulletSpeedDenominator;
                    yNumerator = isMissile ? -_missileFallSpeedYNumerator : 0;
                    yDenominator = isMissile ? _missileFallSpeedYDenominator : 1;
                }

                long accumulatedX = _bulletXRemainders[read] + (long)xNumerator;
                long accumulatedY = _bulletYRemainders[read] + (long)yNumerator;
                int deltaX = (int)(accumulatedX / xDenominator);
                int deltaY = (int)(accumulatedY / yDenominator);
                int nextXRemainder = (int)(accumulatedX % xDenominator);
                int nextYRemainder = (int)(accumulatedY % yDenominator);
                long nextX = bullet.X + (long)deltaX;
                long nextYLong = bullet.Y + (long)deltaY;
                if (nextX > _bulletDespawnX || nextX < -(long)_bulletDespawnX
                    || nextYLong > despawnY || nextYLong < -(long)despawnY)
                {
                    ClearBulletHitRecords(bullet.Id);
                    continue;
                }
                int nextY = SaturateToInt(nextYLong);
                _bullets[write] = new BulletState(
                    bullet.Id, bullet.Faction, bullet.Kind, (int)nextX, nextY);
                _bulletXRemainders[write] = nextXRemainder;
                _bulletYRemainders[write] = nextYRemainder;
                _bulletVelXNumerators[write] = _bulletVelXNumerators[read];
                _bulletVelYNumerators[write] = _bulletVelYNumerators[read];
                _bulletVelDenominators[write] = _bulletVelDenominators[read];
                _bulletPiercesRemaining[write] = _bulletPiercesRemaining[read];
                _bulletRicochetUsed[write] = _bulletRicochetUsed[read];
                _bulletHomingTargetIds[write] = _bulletHomingTargetIds[read];
                _bulletGrazeScored[write] = _bulletGrazeScored[read];
                write++;
            }

            int removed = _bullets.Count - write;
            if (removed > 0)
            {
                _bullets.RemoveRange(write, removed);
                _bulletXRemainders.RemoveRange(write, removed);
                _bulletYRemainders.RemoveRange(write, removed);
                _bulletVelXNumerators.RemoveRange(write, removed);
                _bulletVelYNumerators.RemoveRange(write, removed);
                _bulletVelDenominators.RemoveRange(write, removed);
                _bulletPiercesRemaining.RemoveRange(write, removed);
                _bulletRicochetUsed.RemoveRange(write, removed);
                _bulletHomingTargetIds.RemoveRange(write, removed);
                _bulletGrazeScored.RemoveRange(write, removed);
            }
        }

        void UpdateHomingMissile(int bulletIndex, in BulletState bullet)
        {
            int targetId = _bulletHomingTargetIds[bulletIndex];
            int targetX;
            int targetY;
            if (targetId == 0)
            {
                targetId = FindNearestTarget(
                    bullet.X,
                    bullet.Y,
                    0,
                    long.MaxValue,
                    out targetX,
                    out targetY);
                if (targetId == 0)
                    return;
                _bulletHomingTargetIds[bulletIndex] = targetId;
            }
            else if (targetId < 0
                || !TryGetTargetPosition(targetId, out targetX, out targetY))
            {
                // A lost lock is final: the missile continues on its current vector.
                _bulletHomingTargetIds[bulletIndex] = -1;
                return;
            }

            if (_bulletVelDenominators[bulletIndex] == 0)
                InitializeMissileVelocity(bulletIndex);
            if (_homingMissileTurnLutSlotsPerTick == 0)
                return;

            long desiredX = (long)targetX - bullet.X;
            long desiredY = (long)targetY - bullet.Y;
            ScaleVectorForProducts(ref desiredX, ref desiredY);
            if (desiredX == 0 && desiredY == 0)
                return;

            long velocityX = _bulletVelXNumerators[bulletIndex];
            long velocityY = _bulletVelYNumerators[bulletIndex];
            long cross = velocityX * desiredY - velocityY * desiredX;
            if (cross == 0)
                return;

            int rotation = cross > 0
                ? _homingMissileTurnLutSlotsPerTick
                : -_homingMissileTurnLutSlotsPerTick;
            RotateVector(velocityX, velocityY, rotation, out long turnedX, out long turnedY);

            // Do not overshoot a target already inside the turn step. Keeping the
            // closer of the current and candidate directions avoids float angles.
            long currentDot = velocityX * desiredX + velocityY * desiredY;
            long turnedDot = turnedX * desiredX + turnedY * desiredY;
            if (turnedDot <= currentDot)
                return;

            SetBulletVelocity(
                bulletIndex,
                turnedX,
                turnedY,
                _bulletVelDenominators[bulletIndex]);
        }

        void InitializeMissileVelocity(int bulletIndex)
        {
            long velocityX =
                (long)_missileSpeedXNumerator * _missileFallSpeedYDenominator;
            long velocityY =
                -(long)_missileFallSpeedYNumerator * _missileSpeedXDenominator;
            long denominator =
                (long)_missileSpeedXDenominator * _missileFallSpeedYDenominator;
            SetBulletVelocity(bulletIndex, velocityX, velocityY, denominator);
        }

        static void RotateVector(
            long x,
            long y,
            int lutRotation,
            out long rotatedX,
            out long rotatedY)
        {
            int index = ((lutRotation % SineLut.Length) + SineLut.Length)
                % SineLut.Length;
            int sin = SineLut[index];
            int cos = SineLut[(index + SineLut.Length / 4) % SineLut.Length];
            rotatedX = (x * cos - y * sin) / SineScale;
            rotatedY = (x * sin + y * cos) / SineScale;
        }

        void SetBulletVelocity(
            int bulletIndex,
            long velocityX,
            long velocityY,
            long denominator)
        {
            while (denominator > int.MaxValue
                || Math.Abs(velocityX) > int.MaxValue
                || Math.Abs(velocityY) > int.MaxValue)
            {
                denominator >>= 1;
                velocityX >>= 1;
                velocityY >>= 1;
            }
            if (denominator < 1)
                denominator = 1;

            _bulletVelXNumerators[bulletIndex] = (int)velocityX;
            _bulletVelYNumerators[bulletIndex] = (int)velocityY;
            _bulletVelDenominators[bulletIndex] = (int)denominator;
            _bulletXRemainders[bulletIndex] = 0;
            _bulletYRemainders[bulletIndex] = 0;
        }

        static void ScaleVectorForProducts(ref long x, ref long y)
        {
            while (x > MaxAimComponentBeforeRotation
                || x < -MaxAimComponentBeforeRotation
                || y > MaxAimComponentBeforeRotation
                || y < -MaxAimComponentBeforeRotation)
            {
                x /= 2;
                y /= 2;
            }
        }

        void AdvanceEnemies()
        {
            long scrollDelta = GetScrollXAtTick(Tick) - GetScrollXAtTick(Tick - 1);
            int index = 0;
            while (index < _enemies.Count)
            {
                EnemyState state = _enemies[index];
                EnemyDefinition definition = _enemyDefinitions[index];
                int age = _enemyAges[index] + 1;
                long nextX = state.X - scrollDelta;
                int y = state.Y;

                if (ShouldAdvanceEnemyX(definition, age))
                {
                    long accumulated = _enemyXRemainders[index] + (long)definition.MoveSpeedNumerator;
                    int delta = (int)(accumulated / definition.MoveSpeedDenominator);
                    _enemyXRemainders[index] = (int)(accumulated % definition.MoveSpeedDenominator);
                    nextX -= delta;
                }

                if (nextX < _enemyDespawnX)
                {
                    RemoveEnemyAt(index);
                    continue;
                }
                int x = SaturateToInt(nextX);

                if (definition.MovePattern == EnemyMovePattern.Sine)
                {
                    int phase = (int)(((long)age * SineLut.Length
                        / definition.MovementPeriodTicks) % SineLut.Length);
                    long offset = (long)definition.MovementAmplitudeNumerator
                        * SineLut[phase]
                        / ((long)definition.MovementAmplitudeDenominator * SineScale);
                    y = SaturateToInt(_enemySpawnYs[index] + offset);
                }
                else if (definition.MovePattern == EnemyMovePattern.Zigzag)
                {
                    int triangle = ComputeTriangleLutValue(
                        age,
                        definition.MovementPeriodTicks);
                    long offset = (long)definition.MovementAmplitudeNumerator
                        * triangle
                        / ((long)definition.MovementAmplitudeDenominator * SineScale);
                    y = SaturateToInt(_enemySpawnYs[index] + offset);
                }
                else if (definition.MovePattern == EnemyMovePattern.Dive)
                {
                    y = AdvanceDiveY(index, definition, age);
                }

                _enemyAges[index] = age;
                _enemies[index] = new EnemyState(state.Id, state.DefinitionId, x, y, state.Hp);

                // 터렛류 조준 사격 (REQ-007 요청 1): fireIntervalTicks > 0 인 정의만.
                if (definition.FireIntervalTicks > 0 && age % definition.FireIntervalTicks == 0)
                    SpawnEnemyAimedBullet(
                        x, y, PlayerX, PlayerY,
                        _enemyBulletSpeedNumerator, _enemyBulletSpeedDenominator, 0);
                index++;
            }
        }

        void SpawnScheduledThroughTick(int tick)
        {
            while (_nextScheduledSpawn < _scheduledSpawns.Length
                && _scheduledSpawns[_nextScheduledSpawn].Tick <= tick)
            {
                ScheduledSpawn spawn = _scheduledSpawns[_nextScheduledSpawn++];
                if (_nextEnemyId == int.MaxValue)
                    throw new InvalidOperationException("The enemy id counter is exhausted.");

                _enemies.Add(new EnemyState(
                    _nextEnemyId++,
                    spawn.Definition.Id,
                    spawn.X,
                    spawn.Y,
                    ScaleEnemyHp(spawn.Definition.MaxHp)));
                _enemyDefinitions.Add(spawn.Definition);
                _enemyXRemainders.Add(0);
                _enemySpawnYs.Add(spawn.Y);
                _enemyAges.Add(0);
                _enemyDiveTargetYs.Add(spawn.Y);
                _enemyMovementFlags.Add(0);
            }
        }

        /// <summary>
        /// 보스 수명주기 (REQ-007 요청 2): 세그먼트 소진 → 우측 진입 → holdX 정지 후 사인 호버.
        /// 페이즈는 HP 균등 분할, 발사는 페이즈 파라미터의 n-way 조준 부채꼴.
        /// </summary>
        void UpdateBoss()
        {
            if (_bossMaxHp == 0 || _bossDefeated) return;

            if (!_bossSpawned)
            {
                if (Tick < _stageTotalTicks) return;
                if (_nextEnemyId == int.MaxValue)
                    throw new InvalidOperationException("The enemy id counter is exhausted.");
                _bossSpawned = true;
                _bossId = _nextEnemyId++;
                _bossX = Math.Max(
                    _bossHoldX,
                    SaturateToInt(
                        (long)_bulletDespawnX + 2 * SimSpace.SubUnitsPerWorldUnit));
                _bossY = 0;
                _bossHp = _bossMaxHp;
                _bossPhase = 0;
                _bossAge = 0;
                _bossFireCooldown = _bossPhases[0].FireIntervalTicks;
                EmitEvent(SimEventType.BossSpawned, _bossId, _bossX, _bossY, 0);
                return;
            }

            _bossAge++;
            if (_bossX > _bossHoldX)
            {
                _bossX = Math.Max(_bossHoldX, _bossX - BossEntrySpeedPerTick);
                return;   // 진입 중에는 사격하지 않는다 (등장 연출 여유)
            }

            int lutIndex = (_bossAge >> BossHoverPeriodShift) % SineLut.Length;
            _bossY = (int)((long)BossHoverAmplitude * SineLut[lutIndex] / SineScale);

            Generation.BossPhase phase = _bossPhases[_bossPhase];
            if (_bossFireCooldown > 0) _bossFireCooldown--;
            if (_bossFireCooldown == 0)
            {
                int ways = phase.Ways;
                int available = Math.Max(0, _maxEnemyBullets - CountEnemyBullets());
                int shots = Math.Min(ways, available);
                for (int i = 0; i < shots; i++)
                {
                    long centeredIndex = 2L * i - (ways - 1L);
                    int rotation = (int)(
                        (centeredIndex * SpreadStepLutSlots / 2) % SineLut.Length);
                    SpawnEnemyAimedBullet(
                        _bossX, _bossY, PlayerX, PlayerY,
                        phase.BulletSpeedNumerator, phase.BulletSpeedDenominator, rotation);
                }
                _bossFireCooldown = phase.FireIntervalTicks;
            }
        }

        void ResolvePlayerBulletBossCollisions()
        {
            if (!BossActive) return;

            int bulletIndex = 0;
            while (bulletIndex < _bullets.Count)
            {
                BulletState bullet = _bullets[bulletIndex];
                if (bullet.Faction != BulletFaction.Player)
                {
                    bulletIndex++;
                    continue;
                }

                int bulletHalfWidth = bullet.Kind == BulletKind.Missile
                    ? _missileHalfWidth : _playerBulletHalfWidth;
                int bulletHalfHeight = bullet.Kind == BulletKind.Missile
                    ? _missileHalfHeight : _playerBulletHalfHeight;
                if (!Intersects(
                        bullet.X, bullet.Y, bulletHalfWidth, bulletHalfHeight,
                        _bossX, _bossY, _bossHalfWidth, _bossHalfHeight))
                {
                    bulletIndex++;
                    continue;
                }

                RemoveBulletAt(bulletIndex);
                int damage = bullet.Kind == BulletKind.Missile
                    ? Damage.Compute(_missileBaseDamage, Math.Max(1, _missileLevel))
                    : Damage.Compute(_playerBulletDamage, Math.Max(1, _mainShotLevel));
                _bossHp = Damage.ApplyToHp(_bossHp, damage);

                if (_bossHp > 0)
                {
                    EmitEvent(SimEventType.EnemyHit, _bossId, _bossX, _bossY, damage);
                    int phaseCount = _bossPhases.Count;
                    int nextPhase = Math.Min(
                        phaseCount - 1,
                        (int)((long)(_bossMaxHp - _bossHp) * phaseCount / _bossMaxHp));
                    if (nextPhase != _bossPhase)
                    {
                        _bossPhase = nextPhase;
                        _bossFireCooldown = _bossPhases[_bossPhase].FireIntervalTicks;
                        EmitEvent(SimEventType.BossPhaseChanged, _bossId, _bossX, _bossY, nextPhase);
                    }
                    continue;
                }

                _bossDefeated = true;
                EmitEvent(SimEventType.EnemyKilled, _bossId, _bossX, _bossY, damage);
                RecordKillAndScore((long)_bossMaxHp * 2);
                EmitEvent(SimEventType.StageCleared, _bossId, _bossX, _bossY, 0);
                return;
            }
        }

        void ResolveEnemyBulletPlayerCollisions()
        {
            int index = 0;
            while (index < _bullets.Count)
            {
                BulletState bullet = _bullets[index];
                if (bullet.Faction != BulletFaction.Enemy)
                {
                    index++;
                    continue;
                }

                // A hit always wins over graze on the same tick.
                if (Intersects(
                        PlayerX, PlayerY, _playerHalfWidth, _playerHalfHeight,
                        bullet.X, bullet.Y, _enemyBulletHalfWidth, _enemyBulletHalfHeight))
                {
                    RemoveBulletAt(index);
                    int absorbed = Math.Min(ShieldRemaining, _enemyBulletDamage);
                    ShieldRemaining -= absorbed;
                    PlayerHp = Damage.ApplyToHp(PlayerHp, _enemyBulletDamage - absorbed);
                    EmitEvent(
                        SimEventType.PlayerHit,
                        0,
                        PlayerX,
                        PlayerY,
                        _enemyBulletDamage - absorbed);
                    if (PlayerHp == 0)
                    {
                        EmitEvent(SimEventType.PlayerKilled, 0, PlayerX, PlayerY, 0);
                        return;
                    }
                    continue;
                }

                if (_bulletGrazeScored[index] == 0 && IsWithinGrazeRadius(in bullet))
                {
                    _bulletGrazeScored[index] = 1;
                    AddScoreSaturated(_grazeScore);
                    EmitEvent(
                        SimEventType.GrazeScored,
                        bullet.Id,
                        bullet.X,
                        bullet.Y,
                        _grazeScore);
                    AddComboGauge(_grazeComboGaugeGain);
                }
                index++;
            }
        }

        static bool ShouldAdvanceEnemyX(EnemyDefinition definition, int age)
        {
            if (definition.MovePattern == EnemyMovePattern.Static)
                return false;
            if (definition.MovePattern != EnemyMovePattern.Dash)
                return true;

            long cycleTicks =
                (long)definition.MovementPauseTicks + definition.MovementDurationTicks;
            long phase = (age - 1L) % cycleTicks;
            return phase >= definition.MovementPauseTicks;
        }

        static int ComputeTriangleLutValue(int age, int periodTicks)
        {
            const int cycleScale = 4 * SineScale;
            int phase = (int)(((long)age * cycleScale / periodTicks) % cycleScale);
            if (phase < SineScale)
                return phase;
            if (phase < 3 * SineScale)
                return 2 * SineScale - phase;
            return phase - cycleScale;
        }

        int AdvanceDiveY(int index, EnemyDefinition definition, int age)
        {
            int spawnY = _enemySpawnYs[index];
            if (age <= definition.MovementDelayTicks)
                return spawnY;

            if (_enemyMovementFlags[index] == 0)
            {
                _enemyDiveTargetYs[index] = PlayerY;
                _enemyMovementFlags[index] = 1;
            }

            int elapsed = age - definition.MovementDelayTicks;
            if (elapsed > definition.MovementDurationTicks)
                elapsed = definition.MovementDurationTicks;
            long delta = (long)_enemyDiveTargetYs[index] - spawnY;
            return SaturateToInt(
                spawnY + delta * elapsed / definition.MovementDurationTicks);
        }

        void AdvanceCapsules()
        {
            long scrollDelta = GetScrollXAtTick(Tick) - GetScrollXAtTick(Tick - 1);
            int index = 0;
            while (index < _capsules.Count)
            {
                CapsuleState capsule = _capsules[index];
                long nextX = capsule.X - scrollDelta;
                if (nextX < _enemyDespawnX)
                {
                    _capsules.RemoveAt(index);
                    continue;
                }

                _capsules[index] = new CapsuleState(
                    capsule.Id,
                    SaturateToInt(nextX),
                    capsule.Y);
                index++;
            }
        }

        void ResolvePlayerBulletEnemyCollisions()
        {
            int bulletIndex = 0;
            while (bulletIndex < _bullets.Count)
            {
                BulletState bullet = _bullets[bulletIndex];
                if (bullet.Faction != BulletFaction.Player)
                {
                    bulletIndex++;
                    continue;
                }

                int enemyIndex = FindBulletHitEnemy(bulletIndex, bullet);
                if (enemyIndex < 0)
                {
                    bulletIndex++;
                    continue;
                }

                EnemyState enemy = _enemies[enemyIndex];
                int damage = bullet.Kind == BulletKind.Missile
                    ? Damage.Compute(_missileBaseDamage, Math.Max(1, _missileLevel))
                    : Damage.Compute(_playerBulletDamage, Math.Max(1, _mainShotLevel));
                int hp = Damage.ApplyToHp(enemy.Hp, damage);
                if (hp > 0)
                {
                    _enemies[enemyIndex] = new EnemyState(
                        enemy.Id, enemy.DefinitionId, enemy.X, enemy.Y, hp);
                    EmitEvent(SimEventType.EnemyHit, enemy.Id, enemy.X, enemy.Y, damage);
                }
                else
                {
                    EnemyDefinition definition = _enemyDefinitions[enemyIndex];
                    RemoveEnemyAt(enemyIndex);
                    EmitEvent(SimEventType.EnemyKilled, enemy.Id, enemy.X, enemy.Y, damage);
                    RecordKillAndScore(definition.ScoreValue);
                    TryDropCapsule(definition, enemy.X, enemy.Y);
                    if (HasModifier(BattleModifier.KillExplosion))
                        ApplyKillExplosion(enemy.Id, enemy.X, enemy.Y);
                }

                RecordBulletHit(bullet.Id, enemy.Id);
                bool keepBullet = false;
                if (bullet.Kind == BulletKind.MainShot)
                {
                    if (_bulletPiercesRemaining[bulletIndex] > 0)
                    {
                        _bulletPiercesRemaining[bulletIndex]--;
                        keepBullet = true;
                    }

                    if (HasModifier(BattleModifier.Ricochet)
                        && _bulletRicochetUsed[bulletIndex] == 0)
                    {
                        int targetId = FindNearestTarget(
                            enemy.X,
                            enemy.Y,
                            enemy.Id,
                            SquaredRadiusSaturated(_ricochetRangeSubUnits),
                            out int targetX,
                            out int targetY);
                        if (targetId != 0)
                        {
                            SetBulletVelocityToward(
                                bulletIndex,
                                bullet.X,
                                bullet.Y,
                                targetX,
                                targetY,
                                _bulletSpeedNumerator,
                                _bulletSpeedDenominator);
                            _bulletRicochetUsed[bulletIndex] = 1;
                            keepBullet = true;
                            EmitEvent(
                                SimEventType.BulletRicocheted,
                                bullet.Id,
                                enemy.X,
                                enemy.Y,
                                targetId);
                        }
                    }
                }

                if (keepBullet)
                    bulletIndex++;
                else
                    RemoveBulletAt(bulletIndex);
            }
        }

        int FindBulletHitEnemy(int bulletIndex, BulletState bullet)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState enemy = _enemies[i];
                if (HasBulletHitEnemy(bullet.Id, enemy.Id))
                    continue;
                EnemyDefinition definition = _enemyDefinitions[i];
                int bulletHalfWidth = bullet.Kind == BulletKind.Missile
                    ? _missileHalfWidth
                    : _playerBulletHalfWidth;
                int bulletHalfHeight = bullet.Kind == BulletKind.Missile
                    ? _missileHalfHeight
                    : _playerBulletHalfHeight;
                if (Intersects(
                        bullet.X, bullet.Y, bulletHalfWidth, bulletHalfHeight,
                        enemy.X, enemy.Y, definition.HalfWidth, definition.HalfHeight))
                    return i;
            }
            return -1;
        }

        int FindNearestTarget(
            int originX,
            int originY,
            int excludedId,
            long maximumDistanceSquared,
            out int targetX,
            out int targetY)
        {
            int bestId = 0;
            long bestDistance = maximumDistanceSquared;
            targetX = 0;
            targetY = 0;

            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState candidate = _enemies[i];
                if (candidate.Id == excludedId)
                    continue;
                long distance = SquaredDistanceSaturated(
                    originX,
                    originY,
                    candidate.X,
                    candidate.Y);
                if (distance > bestDistance
                    || (distance == bestDistance
                        && bestId != 0
                        && candidate.Id >= bestId))
                    continue;
                bestId = candidate.Id;
                bestDistance = distance;
                targetX = candidate.X;
                targetY = candidate.Y;
            }

            if (BossActive && _bossId != excludedId)
            {
                long distance = SquaredDistanceSaturated(
                    originX,
                    originY,
                    _bossX,
                    _bossY);
                if (distance <= bestDistance
                    && (distance < bestDistance
                        || bestId == 0
                        || _bossId < bestId))
                {
                    bestId = _bossId;
                    targetX = _bossX;
                    targetY = _bossY;
                }
            }

            return bestId;
        }

        bool TryGetTargetPosition(int targetId, out int x, out int y)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id != targetId)
                    continue;
                x = _enemies[i].X;
                y = _enemies[i].Y;
                return true;
            }
            if (BossActive && _bossId == targetId)
            {
                x = _bossX;
                y = _bossY;
                return true;
            }
            x = 0;
            y = 0;
            return false;
        }

        static long SquaredDistanceSaturated(
            int leftX,
            int leftY,
            int rightX,
            int rightY)
        {
            long dx = Math.Abs((long)leftX - rightX);
            long dy = Math.Abs((long)leftY - rightY);
            if (dx > MaxSquareRoot || dy > MaxSquareRoot)
                return long.MaxValue;
            long dxSquared = dx * dx;
            long dySquared = dy * dy;
            return dxSquared > long.MaxValue - dySquared
                ? long.MaxValue
                : dxSquared + dySquared;
        }

        static long SquaredRadiusSaturated(int radius)
        {
            return (long)radius * radius;
        }

        void SetBulletVelocityToward(
            int bulletIndex,
            int fromX,
            int fromY,
            int targetX,
            int targetY,
            int speedNumerator,
            int speedDenominator)
        {
            long dx = (long)targetX - fromX;
            long dy = (long)targetY - fromY;
            ScaleVectorForProducts(ref dx, ref dy);
            long length = IntegerSqrt(dx * dx + dy * dy);
            if (length == 0)
            {
                dx = 1;
                dy = 0;
                length = 1;
            }
            SetBulletVelocity(
                bulletIndex,
                (long)speedNumerator * dx,
                (long)speedNumerator * dy,
                (long)speedDenominator * length);
        }

        void ApplyKillExplosion(int sourceEnemyId, int centerX, int centerY)
        {
            EmitEvent(
                SimEventType.KillExplosionTriggered,
                sourceEnemyId,
                centerX,
                centerY,
                _killExplosionDamage);
            if (_killExplosionDamage == 0
                || _killExplosionRadiusSubUnits == 0
                || _killExplosionMaxTargets == 0)
                return;

            long radiusSquared =
                SquaredRadiusSaturated(_killExplosionRadiusSubUnits);
            int scanCount = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState enemy = _enemies[i];
                long distanceSquared = SquaredDistanceSaturated(
                    centerX,
                    centerY,
                    enemy.X,
                    enemy.Y);
                if (distanceSquared > radiusSquared)
                    continue;

                int insertIndex = scanCount;
                while (insertIndex > 0
                    && (distanceSquared < _enemyScanDistances[insertIndex - 1]
                        || (distanceSquared == _enemyScanDistances[insertIndex - 1]
                            && enemy.Id < _enemyScanIds[insertIndex - 1])))
                {
                    insertIndex--;
                }
                if (insertIndex >= _killExplosionMaxTargets)
                    continue;

                int nextCount = Math.Min(scanCount + 1, _killExplosionMaxTargets);
                for (int shift = nextCount - 1; shift > insertIndex; shift--)
                {
                    _enemyScanIds[shift] = _enemyScanIds[shift - 1];
                    _enemyScanDistances[shift] = _enemyScanDistances[shift - 1];
                }
                _enemyScanIds[insertIndex] = enemy.Id;
                _enemyScanDistances[insertIndex] = distanceSquared;
                scanCount = nextCount;
            }

            // IDs were captured nearest-first, breaking distance ties by lower id.
            // Explosion kills intentionally call no explosion method themselves.
            for (int scan = 0; scan < scanCount; scan++)
            {
                int enemyIndex = FindEnemyIndexById(_enemyScanIds[scan]);
                if (enemyIndex < 0)
                    continue;
                EnemyState enemy = _enemies[enemyIndex];
                int hp = Damage.ApplyToHp(enemy.Hp, _killExplosionDamage);
                if (hp > 0)
                {
                    _enemies[enemyIndex] = new EnemyState(
                        enemy.Id,
                        enemy.DefinitionId,
                        enemy.X,
                        enemy.Y,
                        hp);
                    continue;
                }

                EnemyDefinition definition = _enemyDefinitions[enemyIndex];
                RemoveEnemyAt(enemyIndex);
                AppendEvent(
                    SimEventType.EnemyKilled,
                    enemy.Id,
                    enemy.X,
                    enemy.Y,
                    _killExplosionDamage);
                IncrementSaturated(ref _kills);
                RecordKillAndScore(definition.ScoreValue);
                TryDropCapsule(definition, enemy.X, enemy.Y);
            }
        }

        bool IsWithinGrazeRadius(in BulletState bullet)
        {
            long playerRadius = Math.Max(_playerHalfWidth, _playerHalfHeight);
            long bulletRadius = Math.Max(_enemyBulletHalfWidth, _enemyBulletHalfHeight);
            long radius = playerRadius + bulletRadius + _grazeExtraRadiusSubUnits;
            long radiusSquared = radius * radius;
            return SquaredDistanceSaturated(
                PlayerX,
                PlayerY,
                bullet.X,
                bullet.Y) <= radiusSquared;
        }

        void RecordKillAndScore(long baseScore)
        {
            AddScoreSaturated(MultiplySaturated(baseScore, ScoreMultiplier));
            _killScoredThisTick = true;
            _ticksSinceLastKill = 0;
            AddComboGauge(_killComboGaugeGain);
        }

        void AddComboGauge(int amount)
        {
            if (amount == 0 || _multiplierLevel >= _comboMultipliers.Length - 1)
                return;

            long nextGauge = (long)_comboGauge + amount;
            _comboGauge = nextGauge >= int.MaxValue
                ? int.MaxValue
                : (int)nextGauge;

            while (_multiplierLevel < _comboMultipliers.Length - 1
                && _comboGauge >= _comboGaugeRequirements[_multiplierLevel])
            {
                _comboGauge -= _comboGaugeRequirements[_multiplierLevel];
                _multiplierLevel++;
                AppendEvent(
                    SimEventType.MultiplierChanged,
                    _multiplierLevel,
                    PlayerX,
                    PlayerY,
                    ScoreMultiplier);
            }

            if (_multiplierLevel == _comboMultipliers.Length - 1)
                _comboGauge = 0;
        }

        void AdvanceComboDecay()
        {
            if (_killScoredThisTick || _multiplierLevel == 0)
                return;

            if (_ticksSinceLastKill < _comboDecayTicks)
                _ticksSinceLastKill++;
            if (_ticksSinceLastKill < _comboDecayTicks)
                return;

            _ticksSinceLastKill = 0;
            _comboGauge = 0;
            _multiplierLevel--;
            AppendEvent(
                SimEventType.MultiplierChanged,
                _multiplierLevel,
                PlayerX,
                PlayerY,
                ScoreMultiplier);
        }

        void ResetCombo()
        {
            _ticksSinceLastKill = 0;
            _comboGauge = 0;
            if (_multiplierLevel == 0)
                return;

            _multiplierLevel = 0;
            AppendEvent(
                SimEventType.MultiplierChanged,
                _multiplierLevel,
                PlayerX,
                PlayerY,
                ScoreMultiplier);
        }

        void AddScoreSaturated(long amount)
        {
            Score = Score > long.MaxValue - amount
                ? long.MaxValue
                : Score + amount;
        }

        static long MultiplySaturated(long value, int multiplier)
        {
            return value != 0 && multiplier > long.MaxValue / value
                ? long.MaxValue
                : value * multiplier;
        }

        int FindEnemyIndexById(int enemyId)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id == enemyId)
                    return i;
            }
            return -1;
        }

        void ResolveEnemyPlayerCollisions()
        {
            int index = 0;
            while (index < _enemies.Count)
            {
                EnemyState enemy = _enemies[index];
                EnemyDefinition definition = _enemyDefinitions[index];
                if (!Intersects(
                        PlayerX, PlayerY, _playerHalfWidth, _playerHalfHeight,
                        enemy.X, enemy.Y, definition.HalfWidth, definition.HalfHeight))
                {
                    index++;
                    continue;
                }

                int contactDamage = definition.ContactDamage;
                int absorbed = Math.Min(ShieldRemaining, contactDamage);
                ShieldRemaining -= absorbed;
                PlayerHp = Damage.ApplyToHp(PlayerHp, contactDamage - absorbed);
                RemoveEnemyAt(index);
                // Arg = 실드를 뚫고 선체에 닿은 데미지. 0이면 실드가 전부 흡수한 것.
                EmitEvent(SimEventType.PlayerHit, 0, PlayerX, PlayerY, contactDamage - absorbed);
                if (PlayerHp == 0)
                    EmitEvent(SimEventType.PlayerKilled, 0, PlayerX, PlayerY, 0);
            }
        }

        void ResolveCapsulePlayerCollisions()
        {
            int index = 0;
            while (index < _capsules.Count)
            {
                CapsuleState capsule = _capsules[index];
                if (!Intersects(
                        PlayerX, PlayerY, _playerHalfWidth, _playerHalfHeight,
                        capsule.X, capsule.Y, _capsuleHalfWidth, _capsuleHalfHeight))
                {
                    index++;
                    continue;
                }

                _capsules.RemoveAt(index);
                _powerUpGauge.Collect();
                EmitEvent(SimEventType.CapsulePicked, capsule.Id, capsule.X, capsule.Y, 0);
            }
        }

        void TryDropCapsule(EnemyDefinition definition, int x, int y)
        {
            if (definition.DropWeight == 0) return;
            int totalWeight = _capsuleNoDropWeight + definition.DropWeight;
            if (_dropRng.NextInt(0, totalWeight) < _capsuleNoDropWeight) return;
            if (_nextCapsuleId == int.MaxValue)
                throw new InvalidOperationException("The capsule id counter is exhausted.");
            int capsuleId = _nextCapsuleId++;
            _capsules.Add(new CapsuleState(capsuleId, x, y));
            EmitEvent(SimEventType.CapsuleDropped, capsuleId, x, y, 0);
        }

        void SpawnMainShotVolley()
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException("The bullet id counter is exhausted.");
            SpawnMainShotFrom(PlayerX, PlayerY);
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.MainShot);
            for (int i = 0; i < _options.Count && CountPlayerBullets() < _maxBullets; i++)
                SpawnMainShotFrom(_options[i].X, _options[i].Y);
            _cooldown = ComputeReducedInterval(
                _fireIntervalTicks,
                _mainShotLevel,
                _mainShotRapidFireStartLevel,
                _mainShotFireIntervalReductionPerLevel,
                _mainShotMinimumFireIntervalTicks);
        }

        void SpawnMainShotFrom(int x, int y)
        {
            if (_playerWeaponType != WeaponType.Spread)
            {
                if (CountPlayerBullets() < _maxBullets)
                    SpawnBullet(BulletKind.MainShot, x, y);
                return;
            }

            int available = Math.Max(0, _maxBullets - CountPlayerBullets());
            int shots = Math.Min(_spreadWays, available);
            for (int i = 0; i < shots; i++)
            {
                long centeredIndex = 2L * i - (_spreadWays - 1L);
                int rotation = (int)(
                    (centeredIndex * _spreadStepLutSlots / 2)
                    % SineLut.Length);
                SpawnSpreadBullet(x, y, rotation);
            }
        }

        void SpawnSpreadBullet(int x, int y, int lutRotation)
        {
            SpawnBullet(BulletKind.MainShot, x, y);
            if (lutRotation == 0)
                return;

            int index = ((lutRotation % SineLut.Length)
                + SineLut.Length)
                % SineLut.Length;
            int sin = SineLut[index];
            int cos = SineLut[
                (index + SineLut.Length / 4)
                % SineLut.Length];
            long velocityX = (long)_bulletSpeedNumerator * cos;
            long velocityY = (long)_bulletSpeedNumerator * sin;
            long velocityDenominator =
                (long)_bulletSpeedDenominator * SineScale;
            while (Math.Abs(velocityX) > int.MaxValue
                || Math.Abs(velocityY) > int.MaxValue
                || velocityDenominator > int.MaxValue)
            {
                velocityX >>= 1;
                velocityY >>= 1;
                velocityDenominator >>= 1;
                if (velocityDenominator < 1)
                    velocityDenominator = 1;
            }

            int bulletIndex = _bullets.Count - 1;
            _bulletVelXNumerators[bulletIndex] = (int)velocityX;
            _bulletVelYNumerators[bulletIndex] = (int)velocityY;
            _bulletVelDenominators[bulletIndex] =
                (int)velocityDenominator;
        }

        void SpawnMissile()
        {
            SpawnBullet(BulletKind.Missile, PlayerX, PlayerY);
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.Missile);
            _missileCooldown = ComputeReducedInterval(
                _missileFireIntervalTicks,
                _missileLevel,
                _missileRapidFireStartLevel,
                _missileFireIntervalReductionPerLevel,
                _missileMinimumFireIntervalTicks);
        }

        void SpawnBullet(BulletKind kind, int x, int y)
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException();
            _bullets.Add(new BulletState(_nextBulletId++, BulletFaction.Player, kind, x, y));
            _bulletXRemainders.Add(0);
            _bulletYRemainders.Add(0);
            _bulletVelXNumerators.Add(0);
            _bulletVelYNumerators.Add(0);
            _bulletVelDenominators.Add(0);
            _bulletPiercesRemaining.Add(
                kind == BulletKind.MainShot
                    ? GetMainShotPierceCount()
                    : 0);
            _bulletRicochetUsed.Add(0);
            _bulletHomingTargetIds.Add(0);
            _bulletGrazeScored.Add(0);
            IncrementSaturated(ref _shotsFired);
        }

        int GetMainShotPierceCount()
        {
            long count = _mainShotBasePierceEnemyCount;
            if (HasModifier(BattleModifier.PierceShot))
                count += _pierceShotEnemyCount;
            return count >= int.MaxValue
                ? int.MaxValue
                : (int)count;
        }

        int CountEnemyBullets()
        {
            int count = 0;
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Faction == BulletFaction.Enemy)
                    count++;
            return count;
        }

        int CountPlayerBullets()
        {
            int count = 0;
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Faction == BulletFaction.Player)
                    count++;
            return count;
        }

        /// <summary>발사 위치에서 (targetX, targetY)를 향해 지정 유리수 속도의 적탄을 스폰한다.</summary>
        void SpawnEnemyAimedBullet(
            int fromX, int fromY, int targetX, int targetY,
            int speedNumerator, int speedDenominator, int lutRotation)
        {
            if (CountEnemyBullets() >= _maxEnemyBullets) return;
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException();

            long dx = (long)targetX - fromX;
            long dy = (long)targetY - fromY;
            while (dx > MaxAimComponentBeforeRotation
                || dx < -MaxAimComponentBeforeRotation
                || dy > MaxAimComponentBeforeRotation
                || dy < -MaxAimComponentBeforeRotation)
            {
                dx /= 2;
                dy /= 2;
            }
            if (lutRotation != 0)
            {
                int index = ((lutRotation % SineLut.Length) + SineLut.Length) % SineLut.Length;
                int sin = SineLut[index];
                int cos = SineLut[(index + SineLut.Length / 4) % SineLut.Length];
                long rotatedX = (dx * cos - dy * sin) / SineScale;
                long rotatedY = (dx * sin + dy * cos) / SineScale;
                dx = rotatedX;
                dy = rotatedY;
            }
            long length = IntegerSqrt(dx * dx + dy * dy);
            if (length == 0) { dx = -1; dy = 0; length = 1; }

            // 서브유닛/틱 = speedNum/(speedDen) × (dx, dy)/len → 분모 speedDen×len 유리수
            long velDen = speedDenominator * length;
            long velXNum = speedNumerator * dx;
            long velYNum = speedNumerator * dy;
            while (velDen > int.MaxValue || Math.Abs(velXNum) > int.MaxValue || Math.Abs(velYNum) > int.MaxValue)
            {
                velDen >>= 1;
                velXNum >>= 1;
                velYNum >>= 1;
                if (velDen < 1) { velDen = 1; break; }
            }

            _bullets.Add(new BulletState(
                _nextBulletId++, BulletFaction.Enemy, BulletKind.EnemyShot, fromX, fromY));
            _bulletXRemainders.Add(0);
            _bulletYRemainders.Add(0);
            _bulletVelXNumerators.Add((int)velXNum);
            _bulletVelYNumerators.Add((int)velYNum);
            _bulletVelDenominators.Add((int)velDen);
            _bulletPiercesRemaining.Add(0);
            _bulletRicochetUsed.Add(0);
            _bulletHomingTargetIds.Add(0);
            _bulletGrazeScored.Add(0);
        }

        static long IntegerSqrt(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 2) return value;

            // 나눗셈 비교로 mid*mid 오버플로를 피하는 순수 정수 이진 탐색.
            // 상한은 floor(sqrt(long.MaxValue))다.
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            long result = 1;
            while (low <= high)
            {
                long mid = low + ((high - low) >> 1);
                if (mid <= value / mid)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return result;
        }

        static int ComputeReducedInterval(
            int baseInterval,
            int level,
            int reductionStartLevel,
            int reductionPerLevel,
            int minimumInterval)
        {
            int reductions = Math.Max(0, level - reductionStartLevel + 1);
            long reduced = baseInterval - (long)reductions * reductionPerLevel;
            int effectiveMinimum = Math.Min(baseInterval, minimumInterval);
            return (int)Math.Max(effectiveMinimum, reduced);
        }

        bool HasModifier(BattleModifier modifier)
        {
            return (_activeModifiers & modifier) != 0;
        }

        bool HasBulletHitEnemy(int bulletId, int enemyId)
        {
            for (int i = 0; i < _bulletHitRecordCount; i++)
            {
                if (_bulletHitRecordBulletIds[i] == bulletId
                    && _bulletHitRecordEnemyIds[i] == enemyId)
                    return true;
            }
            return false;
        }

        void RecordBulletHit(int bulletId, int enemyId)
        {
            if (_bulletHitRecordCount == _bulletHitRecordBulletIds.Length)
                throw new InvalidOperationException(
                    "The preallocated bullet hit history is exhausted.");
            _bulletHitRecordBulletIds[_bulletHitRecordCount] = bulletId;
            _bulletHitRecordEnemyIds[_bulletHitRecordCount] = enemyId;
            _bulletHitRecordCount++;
        }

        void ClearBulletHitRecords(int bulletId)
        {
            int write = 0;
            for (int read = 0; read < _bulletHitRecordCount; read++)
            {
                if (_bulletHitRecordBulletIds[read] == bulletId)
                    continue;
                _bulletHitRecordBulletIds[write] =
                    _bulletHitRecordBulletIds[read];
                _bulletHitRecordEnemyIds[write] =
                    _bulletHitRecordEnemyIds[read];
                write++;
            }
            _bulletHitRecordCount = write;
        }

        void RemoveBulletAt(int index)
        {
            int bulletId = _bullets[index].Id;
            _bullets.RemoveAt(index);
            _bulletXRemainders.RemoveAt(index);
            _bulletYRemainders.RemoveAt(index);
            _bulletVelXNumerators.RemoveAt(index);
            _bulletVelYNumerators.RemoveAt(index);
            _bulletVelDenominators.RemoveAt(index);
            _bulletPiercesRemaining.RemoveAt(index);
            _bulletRicochetUsed.RemoveAt(index);
            _bulletHomingTargetIds.RemoveAt(index);
            _bulletGrazeScored.RemoveAt(index);
            ClearBulletHitRecords(bulletId);
        }

        void RemoveEnemyAt(int index)
        {
            _enemies.RemoveAt(index);
            _enemyDefinitions.RemoveAt(index);
            _enemyXRemainders.RemoveAt(index);
            _enemySpawnYs.RemoveAt(index);
            _enemyAges.RemoveAt(index);
            _enemyDiveTargetYs.RemoveAt(index);
            _enemyMovementFlags.RemoveAt(index);
        }

        static bool Intersects(
            int leftX, int leftY, int leftHalfWidth, int leftHalfHeight,
            int rightX, int rightY, int rightHalfWidth, int rightHalfHeight)
        {
            long xDistance = Math.Abs((long)leftX - rightX);
            long yDistance = Math.Abs((long)leftY - rightY);
            return xDistance <= (long)leftHalfWidth + rightHalfWidth
                && yDistance <= (long)leftHalfHeight + rightHalfHeight;
        }

        int ScaleEnemyHp(int baseHp)
        {
            long scaled =
                ((long)baseHp * _enemyHpMultiplierNumerator
                    + _enemyHpMultiplierDenominator - 1)
                / _enemyHpMultiplierDenominator;
            return scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled;
        }

        static int SaturateToInt(long value)
        {
            if (value < int.MinValue) return int.MinValue;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }

        static ScheduledSpawn[] BuildSchedule(
            StagePlan stagePlan, BattleContent content, out long totalTicks)
        {
            var schedule = new List<ScheduledSpawn>();
            long segmentStart = 0;
            int sequence = 0;

            for (int segmentIndex = 0; segmentIndex < stagePlan.Segments.Count; segmentIndex++)
            {
                StageSegment segment = stagePlan.Segments[segmentIndex];
                if (segment.LengthTicks < 1)
                    throw new ArgumentException(
                        "Stage execution requires positive segment lengths.", nameof(stagePlan));

                for (int spawnIndex = 0; spawnIndex < segment.Spawns.Count; spawnIndex++)
                {
                    SpawnEvent spawn = segment.Spawns[spawnIndex];
                    if (spawn.Tick >= segment.LengthTicks)
                        throw new ArgumentException(
                            "Spawn ticks must be earlier than their segment length.", nameof(stagePlan));

                    EnemyDefinition definition = content.FindEnemy(spawn.EnemyId);
                    if (definition == null)
                        throw new ArgumentException(
                            $"Stage references unknown enemy id '{spawn.EnemyId}'.", nameof(stagePlan));

                    long absoluteTick = segmentStart + spawn.Tick;
                    if (absoluteTick > int.MaxValue)
                        throw new ArgumentException(
                            "Stage spawn timeline exceeds the tick range.", nameof(stagePlan));
                    schedule.Add(new ScheduledSpawn(
                        (int)absoluteTick, sequence++, definition, spawn.X, spawn.Y));
                }

                segmentStart += segment.LengthTicks;
                if (segmentStart > int.MaxValue)
                    throw new ArgumentException(
                        "Stage timeline exceeds the tick range.", nameof(stagePlan));
            }

            ScheduledSpawn[] result = schedule.ToArray();
            Array.Sort(result, CompareScheduledSpawns);
            totalTicks = segmentStart;
            return result;
        }

        static int CompareScheduledSpawns(ScheduledSpawn left, ScheduledSpawn right)
        {
            int tickComparison = left.Tick.CompareTo(right.Tick);
            return tickComparison != 0
                ? tickComparison
                : left.Sequence.CompareTo(right.Sequence);
        }

        static void ValidateDropTotals(BattleContent content, int noDropWeight)
        {
            for (int i = 0; i < content.Enemies.Count; i++)
            {
                long total = (long)noDropWeight + content.Enemies[i].DropWeight;
                if (total > int.MaxValue)
                    throw new ArgumentException(
                        "Capsule drop weights exceed the supported integer range.", nameof(content));
            }
        }

        static void Validate(BattleSimConfig config)
        {
            if (!Enum.IsDefined(typeof(WeaponType), config.PlayerWeaponType))
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerWeaponType));
            if (config.PlayerSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpeedNumerator));
            if (config.PlayerSpeedDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpeedDenominator));
            if (config.PlayerBulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerBulletSpeedNumerator));
            if (config.PlayerBulletSpeedDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerBulletSpeedDenominator));
            if (config.FireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.FireIntervalTicks));
            if (config.MainShotBaseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotBaseDamage));
            if (config.MainShotHalfWidth < 0
                || config.MainShotHalfHeight < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MainShotHalfWidth));
            if (config.MaxBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxBullets));
            if (config.MainShotRapidFireStartLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotRapidFireStartLevel));
            if (config.MainShotFireIntervalReductionPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MainShotFireIntervalReductionPerLevel));
            if (config.MainShotMinimumFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotMinimumFireIntervalTicks));
            ValidateWeaponProfile(
                config.LaserBaseDamage,
                config.LaserFireIntervalTicks,
                config.LaserRapidFireStartLevel,
                config.LaserFireIntervalReductionPerLevel,
                config.LaserMinimumFireIntervalTicks,
                config.LaserSpeedNumerator,
                config.LaserSpeedDenominator,
                config.LaserHalfWidth,
                config.LaserHalfHeight,
                nameof(config.LaserBaseDamage));
            if (config.LaserPierceEnemyCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.LaserPierceEnemyCount));
            ValidateWeaponProfile(
                config.SpreadBaseDamage,
                config.SpreadFireIntervalTicks,
                config.SpreadRapidFireStartLevel,
                config.SpreadFireIntervalReductionPerLevel,
                config.SpreadMinimumFireIntervalTicks,
                config.SpreadSpeedNumerator,
                config.SpreadSpeedDenominator,
                config.SpreadHalfWidth,
                config.SpreadHalfHeight,
                nameof(config.SpreadBaseDamage));
            if (config.SpreadWays < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.SpreadWays));
            if (config.SpreadStepLutSlots < 0
                || config.SpreadStepLutSlots > SineLut.Length / 2)
                throw new ArgumentOutOfRangeException(
                    nameof(config.SpreadStepLutSlots));
            if (config.MissileBaseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileBaseDamage));
            if (config.MissileFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileFireIntervalTicks));
            if (config.MissileRapidFireStartLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MissileRapidFireStartLevel));
            if (config.MissileFireIntervalReductionPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MissileFireIntervalReductionPerLevel));
            if (config.MissileMinimumFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileMinimumFireIntervalTicks));
            if (config.MissileSpeedXNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileSpeedXNumerator));
            if (config.MissileSpeedXDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MissileSpeedXDenominator));
            if (config.MissileFallSpeedYNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileFallSpeedYNumerator));
            if (config.MissileFallSpeedYDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MissileFallSpeedYDenominator));
            if (config.MissileHalfWidth < 0 || config.MissileHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileHalfWidth));
            if (config.OptionFollowDelayTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(config.OptionFollowDelayTicks));
            if (config.PlayerMaxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerMaxHp));
            if (config.PlayerHalfWidth < 0 || config.PlayerHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerHalfWidth));
            if (config.CapsuleHalfWidth < 0 || config.CapsuleHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleHalfWidth));
            if (config.CapsuleNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleNoDropWeight));
            if (config.ScrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedNumerator));
            if (config.ScrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedDenominator));
            if (config.EnemyHpMultiplierNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.EnemyHpMultiplierNumerator));
            if (config.EnemyHpMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.EnemyHpMultiplierDenominator));
            if (config.EnemyBulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletSpeedNumerator));
            if (config.EnemyBulletSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletSpeedDenominator));
            if (config.EnemyBulletHalfWidth < 0 || config.EnemyBulletHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletHalfWidth));
            if (config.EnemyBulletDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletDamage));
            if (config.MaxEnemyBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxEnemyBullets));
            if (config.PierceShotEnemyCount < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PierceShotEnemyCount));
            if (config.RicochetRangeSubUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(config.RicochetRangeSubUnits));
            if (config.HomingMissileTurnLutSlotsPerTick < 0
                || config.HomingMissileTurnLutSlotsPerTick > SineLut.Length / 2)
                throw new ArgumentOutOfRangeException(
                    nameof(config.HomingMissileTurnLutSlotsPerTick));
            if (config.KillExplosionRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillExplosionRadiusSubUnits));
            if (config.KillExplosionDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.KillExplosionDamage));
            if (config.KillExplosionMaxTargets < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillExplosionMaxTargets));
            if (config.GrazeExtraRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.GrazeExtraRadiusSubUnits));
            if ((long)Math.Max(config.PlayerHalfWidth, config.PlayerHalfHeight)
                    + Math.Max(
                        config.EnemyBulletHalfWidth,
                        config.EnemyBulletHalfHeight)
                    + config.GrazeExtraRadiusSubUnits
                > MaxSquareRoot)
                throw new ArgumentOutOfRangeException(
                    nameof(config.GrazeExtraRadiusSubUnits),
                    "The combined graze radius exceeds the supported integer range.");
            if (config.GrazeScore < 0)
                throw new ArgumentOutOfRangeException(nameof(config.GrazeScore));
            if (config.GrazeComboGaugeGain < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.GrazeComboGaugeGain));
            if (config.KillComboGaugeGain < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillComboGaugeGain));
            if (config.ComboGaugeRequiredForLevel2 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboGaugeRequiredForLevel2));
            if (config.ComboGaugeRequiredForLevel3 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboGaugeRequiredForLevel3));
            if (config.ComboGaugeRequiredForLevel4 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboGaugeRequiredForLevel4));
            if (config.ComboDecayTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(config.ComboDecayTicks));
            if (config.ComboMultiplierLevel1 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboMultiplierLevel1));
            if (config.ComboMultiplierLevel2 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboMultiplierLevel2));
            if (config.ComboMultiplierLevel3 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboMultiplierLevel3));
            if (config.ComboMultiplierLevel4 < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ComboMultiplierLevel4));
            if ((long)config.MaxBullets + config.MaxEnemyBullets > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxEnemyBullets),
                    "Combined bullet capacity exceeds the supported integer range.");
            if (config.BulletDespawnX < 0)
                throw new ArgumentOutOfRangeException(nameof(config.BulletDespawnX));
            if (config.PlayerMinX > config.PlayerMaxX || config.PlayerMinY > config.PlayerMaxY)
                throw new ArgumentException("Player bounds are reversed.", nameof(config));
            if (config.PlayerSpawnX < config.PlayerMinX || config.PlayerSpawnX > config.PlayerMaxX)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnX));
            if (config.PlayerSpawnY < config.PlayerMinY || config.PlayerSpawnY > config.PlayerMaxY)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnY));
        }

        static void ValidateWeaponProfile(
            int baseDamage,
            int fireIntervalTicks,
            int rapidFireStartLevel,
            int fireIntervalReductionPerLevel,
            int minimumFireIntervalTicks,
            int speedNumerator,
            int speedDenominator,
            int halfWidth,
            int halfHeight,
            string parameterName)
        {
            if (baseDamage < 0
                || fireIntervalTicks < 0
                || rapidFireStartLevel < 1
                || fireIntervalReductionPerLevel < 0
                || minimumFireIntervalTicks < 0
                || speedNumerator < 0
                || speedDenominator < 1
                || halfWidth < 0
                || halfHeight < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        sealed class ScheduledSpawn
        {
            public ScheduledSpawn(
                int tick, int sequence, EnemyDefinition definition, int x, int y)
            {
                Tick = tick;
                Sequence = sequence;
                Definition = definition;
                X = x;
                Y = y;
            }

            public int Tick { get; }
            public int Sequence { get; }
            public EnemyDefinition Definition { get; }
            public int X { get; }
            public int Y { get; }
        }
    }
}
