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
        /// <summary>
        /// EntityId = killed enemy id, X/Y = death point,
        /// Arg = multiplier-applied score actually awarded, saturated to int.MaxValue.
        /// </summary>
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
        MultiplierChanged = 14,
        /// <summary>
        /// EntityId = obstacle id, X/Y = destruction point,
        /// Arg = multiplier-applied score actually awarded, saturated to int.MaxValue.
        /// </summary>
        ObstacleDestroyed = 15,
        /// <summary>EntityId = missile id, X/Y = impact point, Arg = explosion damage.</summary>
        MissileExploded = 16,
        /// <summary>EntityId = boss id, PartId = destroyed part id.</summary>
        BossPartDestroyed = 17,
        /// <summary>EntityId = boss id, PartId = regenerated part id.</summary>
        BossPartRegenerated = 18,
        /// <summary>EntityId = pickup id, Arg = stock after acquisition.</summary>
        BombAcquired = 19,
        /// <summary>EntityId = 0, Arg = stock after the change.</summary>
        BombStockChanged = 20,
        /// <summary>X/Y = activation center, Arg = visual effect radius.</summary>
        BombActivated = 21,
        /// <summary>X/Y = attempted activation position.</summary>
        BombActivationRejectedEmpty = 22,
        /// <summary>EntityId = laser id, Arg = (int)LaserSourceKind.</summary>
        LaserTelegraphStarted = 23,
        /// <summary>EntityId = laser id, Arg = full beam half-width.</summary>
        LaserFired = 24,
        /// <summary>EntityId = laser id.</summary>
        LaserEnded = 25,
        /// <summary>
        /// EntityId = rejected source entity id, Arg = configured laser cap.
        /// </summary>
        LaserCapacityExceeded = 26,
        /// <summary>X/Y = rejected spawn point, Arg = configured enemy cap.</summary>
        EnemyCapacityExceeded = 27,
        /// <summary>X/Y = rejected spawn point, Arg = configured obstacle cap.</summary>
        ObstacleCapacityExceeded = 28,
        /// <summary>EntityId = segment index, X/Y = clamped player point, Arg = damage.</summary>
        CorridorContact = 29,
        /// <summary>Arg = configured hard deadline tick.</summary>
        TimeLimitExpired = 30,
        /// <summary>
        /// EntityId = boss id, Arg = zero-based action phase. The phase's first
        /// volley is delayed by its configured telegraphTicks.
        /// </summary>
        BossAttackTelegraphed = 31,
        /// <summary>
        /// EntityId = boss id, Arg = configured enemy-bullet capacity.
        /// One event is emitted for a boss volley truncated by the hard cap.
        /// </summary>
        EnemyBulletCapacityExceeded = 32
    }

    /// <summary>One event that happened during the last Step. Coordinates are subunits.</summary>
    public readonly struct SimEvent
    {
        public SimEvent(SimEventType type, int entityId, int x, int y, int arg)
            : this(type, entityId, x, y, arg, null)
        {
        }

        public SimEvent(
            SimEventType type,
            int entityId,
            int x,
            int y,
            int arg,
            string partId)
        {
            Type = type;
            EntityId = entityId;
            X = x;
            Y = y;
            Arg = arg;
            PartId = partId;
        }

        public SimEventType Type { get; }
        public int EntityId { get; }
        public int X { get; }
        public int Y { get; }
        /// <summary>
        /// Event-specific value. EnemyHit/PlayerHit use damage;
        /// EnemyKilled/ObstacleDestroyed use the multiplier-applied score actually awarded
        /// (saturated to int.MaxValue); other meanings are documented on SimEventType.
        /// </summary>
        public int Arg { get; }
        /// <summary>
        /// Stable boss-part id for BossPartDestroyed/BossPartRegenerated;
        /// null for all legacy events.
        /// </summary>
        public string PartId { get; }
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

        internal static readonly BattleModifier[] Ordered =
        {
            BattleModifier.PierceShot,
            BattleModifier.Ricochet,
            BattleModifier.HomingMissile,
            BattleModifier.KillExplosion
        };

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
            : this(
                id,
                x,
                y,
                hp,
                maxHp,
                phase,
                BossMovementPattern.LegacyHover,
                BossPartVulnerability.Legacy)
        {
        }

        public BossState(
            int id,
            int x,
            int y,
            int hp,
            int maxHp,
            int phase,
            BossMovementPattern movementPattern,
            BossPartVulnerability partVulnerability)
        {
            Id = id;
            X = x;
            Y = y;
            Hp = hp;
            MaxHp = maxHp;
            Phase = phase;
            MovementPattern = movementPattern;
            PartVulnerability = partVulnerability;
        }

        public int Id { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int Phase { get; }
        public BossMovementPattern MovementPattern { get; }
        public BossPartVulnerability PartVulnerability { get; }
    }

    /// <summary>
    /// One tick of player input. The legacy digital axes are clamped to
    /// -1, 0, or 1. Commands created with the analog overload or
    /// <see cref="Analog"/> use integer SimSpace subunit deltas instead;
    /// analog movement wins over digital movement even when both deltas are zero.
    /// </summary>
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
            : this(moveX, moveY, fire, activate, false)
        {
        }

        public InputCommand(
            int moveX,
            int moveY,
            bool fire,
            bool activate,
            bool activateBomb)
            : this(
                moveX,
                moveY,
                fire,
                activate,
                activateBomb,
                0,
                0,
                false)
        {
        }

        /// <summary>
        /// Creates a command carrying both legacy digital axes and an analog
        /// movement delta. The analog delta is expressed in SimSpace subunits
        /// for this simulation tick and takes precedence over the digital axes.
        /// </summary>
        public InputCommand(
            int moveX,
            int moveY,
            bool fire,
            bool activate,
            bool activateBomb,
            int analogDeltaXSubUnits,
            int analogDeltaYSubUnits)
            : this(
                moveX,
                moveY,
                fire,
                activate,
                activateBomb,
                analogDeltaXSubUnits,
                analogDeltaYSubUnits,
                true)
        {
        }

        InputCommand(
            int moveX,
            int moveY,
            bool fire,
            bool activate,
            bool activateBomb,
            int analogDeltaXSubUnits,
            int analogDeltaYSubUnits,
            bool useAnalogMovement)
        {
            MoveX = Clamp(moveX);
            MoveY = Clamp(moveY);
            Fire = fire;
            Activate = activate;
            ActivateBomb = activateBomb;
            AnalogDeltaXSubUnits = analogDeltaXSubUnits;
            AnalogDeltaYSubUnits = analogDeltaYSubUnits;
            UseAnalogMovement = useAnalogMovement;
        }

        public int MoveX { get; }
        public int MoveY { get; }
        public bool Fire { get; }
        public bool Activate { get; }
        public bool ActivateBomb { get; }
        public int AnalogDeltaXSubUnits { get; }
        public int AnalogDeltaYSubUnits { get; }
        public bool UseAnalogMovement { get; }
        public static InputCommand None => default;

        /// <summary>
        /// Creates an analog-only movement command. Deltas use
        /// SimSpace.SubUnitsPerWorldUnit and represent movement during one tick.
        /// </summary>
        public static InputCommand Analog(
            int deltaXSubUnits,
            int deltaYSubUnits,
            bool fire,
            bool activate = false,
            bool activateBomb = false)
        {
            return new InputCommand(
                0,
                0,
                fire,
                activate,
                activateBomb,
                deltaXSubUnits,
                deltaYSubUnits,
                true);
        }

        /// <summary>
        /// Returns this command with only the activate bit replaced.
        /// All movement modes and payload fields are preserved.
        /// </summary>
        public InputCommand WithActivate(bool activate)
        {
            return new InputCommand(
                MoveX,
                MoveY,
                Fire,
                activate,
                ActivateBomb,
                AnalogDeltaXSubUnits,
                AnalogDeltaYSubUnits,
                UseAnalogMovement);
        }

        static int Clamp(int value) => value < 0 ? -1 : value > 0 ? 1 : 0;
    }

    public readonly struct BossPartState
    {
        internal BossPartState(
            string partId,
            int x,
            int y,
            int hp,
            int maxHp,
            bool destroyed,
            bool isCore,
            bool coreGated)
        {
            PartId = partId;
            X = x;
            Y = y;
            Hp = hp;
            MaxHp = maxHp;
            Destroyed = destroyed;
            IsCore = isCore;
            Invulnerable = coreGated;
        }

        public string PartId { get; }
        public int X { get; }
        public int Y { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public bool Destroyed { get; }
        public bool IsCore { get; }
        /// <summary>
        /// True while the current boss phase prevents damage to this part.
        /// </summary>
        public bool Invulnerable { get; }
        /// <summary>
        /// Legacy compatibility alias. Phase rules may now cause invulnerability
        /// even when no predecessor core gate exists.
        /// </summary>
        public bool CoreGated => Invulnerable;
    }

    /// <summary>Observable bullet state in integer simulation subunits.</summary>
    public readonly struct BulletState
    {
        public BulletState(int id, BulletFaction faction, int x, int y)
            : this(id, faction, BulletKind.MainShot, x, y, 0)
        {
        }

        public BulletState(int id, BulletFaction faction, BulletKind kind, int x, int y)
            : this(id, faction, kind, x, y, 0)
        {
        }

        public BulletState(
            int id,
            BulletFaction faction,
            BulletKind kind,
            int x,
            int y,
            int ageTicks)
        {
            if (ageTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(ageTicks));
            Id = id;
            Faction = faction;
            Kind = kind;
            X = x;
            Y = y;
            AgeTicks = ageTicks;
        }

        public int Id { get; }
        public BulletFaction Faction { get; }
        public BulletKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int AgeTicks { get; }
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

    /// <summary>Observable stage obstacle state in integer simulation subunits.</summary>
    public readonly struct ObstacleState
    {
        public ObstacleState(int id, ObstacleType type, int x, int y, int hp)
        {
            Id = id;
            Type = type;
            X = x;
            Y = y;
            Hp = hp;
        }

        public int Id { get; }
        public ObstacleType Type { get; }
        public int X { get; }
        public int Y { get; }
        /// <summary>Remaining HP for breakable obstacles; zero for solid obstacles.</summary>
        public int Hp { get; }
    }

    /// <summary>
    /// Observable segment environment in integer simulation subunits.
    /// SegmentIndex is -1 outside a stage segment (for example, in a boss arena).
    /// </summary>
    public readonly struct StageEnvironmentState
    {
        public StageEnvironmentState(
            int segmentIndex,
            string segmentId,
            bool hasCorridor,
            int corridorMinY,
            int corridorMaxY,
            int corridorContactDamage,
            int driftXNumerator,
            int driftXDenominator,
            int driftYNumerator,
            int driftYDenominator)
        {
            SegmentIndex = segmentIndex;
            SegmentId = segmentId;
            HasCorridor = hasCorridor;
            CorridorMinY = corridorMinY;
            CorridorMaxY = corridorMaxY;
            CorridorContactDamage = corridorContactDamage;
            DriftXNumerator = driftXNumerator;
            DriftXDenominator = driftXDenominator;
            DriftYNumerator = driftYNumerator;
            DriftYDenominator = driftYDenominator;
        }

        public int SegmentIndex { get; }
        public string SegmentId { get; }
        public bool HasCorridor { get; }
        public int CorridorMinY { get; }
        public int CorridorMaxY { get; }
        public int CorridorContactDamage { get; }
        public int DriftXNumerator { get; }
        public int DriftXDenominator { get; }
        public int DriftYNumerator { get; }
        public int DriftYDenominator { get; }
        public bool HasDrift =>
            DriftXNumerator != 0 || DriftYNumerator != 0;
    }

    public enum LaserSourceKind
    {
        Enemy = 0,
        Terrain = 1
    }

    public enum LaserPhase
    {
        Telegraph = 0,
        Firing = 1,
        Sustaining = 2,
        Dissipating = 3
    }

    public enum LaserThicknessStage
    {
        Telegraph = 0,
        Thin = 1,
        Full = 2
    }

    /// <summary>
    /// Observable hostile laser segment. Firing and Sustaining phases damage;
    /// Telegraph and Dissipating phases are presentation-only warnings/fades.
    /// </summary>
    public readonly struct LaserState
    {
        public LaserState(
            int id,
            LaserSourceKind sourceKind,
            int sourceEntityId,
            int startX,
            int startY,
            int endX,
            int endY,
            LaserPhase phase,
            LaserThicknessStage thicknessStage,
            int halfWidth,
            int phaseTicksRemaining,
            int damage)
        {
            Id = id;
            SourceKind = sourceKind;
            SourceEntityId = sourceEntityId;
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            Phase = phase;
            ThicknessStage = thicknessStage;
            HalfWidth = halfWidth;
            PhaseTicksRemaining = phaseTicksRemaining;
            Damage = damage;
        }

        public int Id { get; }
        public LaserSourceKind SourceKind { get; }
        public int SourceEntityId { get; }
        public int StartX { get; }
        public int StartY { get; }
        public int EndX { get; }
        public int EndY { get; }
        public LaserPhase Phase { get; }
        public LaserThicknessStage ThicknessStage { get; }
        public int HalfWidth { get; }
        public int PhaseTicksRemaining { get; }
        public int Damage { get; }
        public bool IsDamaging =>
            Phase == LaserPhase.Firing
            || Phase == LaserPhase.Sustaining;
    }

    public readonly struct BombPickupState
    {
        public BombPickupState(int id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
        }

        public int Id { get; }
        public int X { get; }
        public int Y { get; }
    }

    public static class LaserGeometry
    {
        /// <summary>
        /// Division-free segment-versus-circle test. Very large inputs are
        /// deterministically scaled by powers of two before squared products;
        /// the radius rounds outward so overflow protection never shrinks the
        /// hazardous beam.
        /// </summary>
        public static bool IntersectsSegmentCircle(
            int startX,
            int startY,
            int endX,
            int endY,
            int circleX,
            int circleY,
            int radius)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            long vx = (long)endX - startX;
            long vy = (long)endY - startY;
            long wx = (long)circleX - startX;
            long wy = (long)circleY - startY;
            long scaledRadius = radius;
            while (MaxAbs(vx, vy, wx, wy, scaledRadius) > 16_384)
            {
                vx /= 2;
                vy /= 2;
                wx /= 2;
                wy /= 2;
                scaledRadius = (scaledRadius + 1) / 2;
            }

            long radiusSquared =
                scaledRadius * scaledRadius;
            long segmentLengthSquared =
                vx * vx + vy * vy;
            if (segmentLengthSquared == 0)
                return wx * wx + wy * wy <= radiusSquared;

            long projection = wx * vx + wy * vy;
            if (projection <= 0)
                return wx * wx + wy * wy <= radiusSquared;
            if (projection >= segmentLengthSquared)
            {
                long ex = wx - vx;
                long ey = wy - vy;
                return ex * ex + ey * ey <= radiusSquared;
            }

            long cross = vx * wy - vy * wx;
            return cross * cross
                <= radiusSquared * segmentLengthSquared;
        }

        static long MaxAbs(
            long a,
            long b,
            long c,
            long d,
            long e)
        {
            long max = Abs(a);
            max = Math.Max(max, Abs(b));
            max = Math.Max(max, Abs(c));
            max = Math.Max(max, Abs(d));
            return Math.Max(max, e);
        }

        static long Abs(long value)
        {
            return value < 0 ? -value : value;
        }
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
        /// <summary>
        /// Human-approved REQ-049 default and upgrade ceiling.
        /// </summary>
        public const int DefaultMaxShieldStock = 3;
        public const int MaximumShieldStock = 5;
        public const int ProvisionalMaxShieldStock = DefaultMaxShieldStock;
        /// <summary>
        /// Provisional REQ-041 cap pending explicit human balance approval.
        /// </summary>
        public const int ProvisionalMaxBombStock = 3;
        /// <summary>
        /// Matches Presentation's existing 0.3 second damage flash at 60 Hz.
        /// </summary>
        public const int DefaultPlayerHitInvulnerabilityTicks =
            3 * SimSpace.TicksPerSecond / 10;
        public const int DefaultBombInvulnerabilityTicks =
            3 * SimSpace.TicksPerSecond / 4;

        int _playerSpeedNumerator, _bulletSpeedNumerator;
        int _playerSpeedDenominator = 1, _bulletSpeedDenominator = 1;
        int _startingShieldStock = 1;

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
        /// <summary>
        /// Optional exact family identity. This distinguishes Double from
        /// Triple/Spread even though both use WeaponType.Spread.
        /// </summary>
        public PrimaryWeaponFamily? PlayerWeaponFamily { get; set; }
        public int MainShotBaseDamage { get; set; }
        public int FireIntervalTicks { get; set; }
        public int MainShotHalfWidth { get; set; }
        public int MainShotHalfHeight { get; set; }
        public bool UseConfiguredMainShotStats { get; set; }
        public int MaxBullets { get; set; }
        /// <summary>
        /// Shared regular-enemy population cap. Scheduled and boss-spawned enemies
        /// both use this budget.
        /// </summary>
        public int MaxEnemies { get; set; } = 128;
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int EnemyDespawnX { get; set; } = int.MinValue;
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }
        /// <summary>Shield stocks available at battle tick zero.</summary>
        public int StartingShieldStock
        {
            get => _startingShieldStock;
            set => _startingShieldStock = value;
        }
        /// <summary>
        /// Compatibility alias for callers that still populate ship HP. REQ-040
        /// interprets the old value as starting shield stock; it is not hull HP.
        /// </summary>
        public int PlayerMaxHp
        {
            get => _startingShieldStock;
            set => _startingShieldStock = value;
        }
        /// <summary>
        /// Provisional cap pending the human balance decision requested by REQ-040.
        /// </summary>
        public int MaxShieldStock { get; set; } =
            ProvisionalMaxShieldStock;
        public int PlayerHitInvulnerabilityTicks { get; set; } =
            DefaultPlayerHitInvulnerabilityTicks;
        public int StartingBombStock { get; set; }
        public int MaxBombStock { get; set; } = ProvisionalMaxBombStock;
        public int BombInvulnerabilityTicks { get; set; } =
            DefaultBombInvulnerabilityTicks;
        public int BombEffectRadiusSubUnits { get; set; } =
            48 * SimSpace.SubUnitsPerWorldUnit;
        public int BombRegularEnemyDamage { get; set; } = 1_000;
        public int BombBossDamageCap { get; set; } = 250;
        public int BombBossPartDamageCap { get; set; } = 250;
        public int BombNoDropWeight { get; set; } = 100;
        public int MaxBombPickups { get; set; } = 16;
        public int MaxLasers { get; set; } = 8;
        public int PlayerHalfWidth { get; set; }
        public int PlayerHalfHeight { get; set; }
        public int CapsuleHalfWidth { get; set; }
        public int CapsuleHalfHeight { get; set; }
        public int CapsuleNoDropWeight { get; set; }
        /// <summary>
        /// Persistent reward cost subtracted from each enemy capsule weight.
        /// </summary>
        public int CapsuleDropWeightReduction { get; set; }
        public int ContractBombDropMultiplierNumerator { get; set; } = 1;
        public int ContractBombDropMultiplierDenominator { get; set; } = 1;
        public bool ContractGuaranteesBombDrop { get; set; }
        public int ContractCapsuleDropMultiplierNumerator { get; set; } = 1;
        public int ContractCapsuleDropMultiplierDenominator { get; set; } = 1;
        public int ContractScoreMultiplierNumerator { get; set; } = 1;
        public int ContractScoreMultiplierDenominator { get; set; } = 1;
        public int ScrollSpeedNumerator { get; set; }
        public int ScrollSpeedDenominator { get; set; } = 1;
        /// <summary>
        /// Attraction radius in simulation subunits. Zero disables capsule magnetism.
        /// </summary>
        public int CapsuleMagnetRadiusSubUnits { get; set; }
        /// <summary>Capsule attraction speed numerator in subunits per tick.</summary>
        public int CapsuleMagnetSpeedNumerator { get; set; }
        public int CapsuleMagnetSpeedDenominator { get; set; } = 1;

        // Provisional route tuning (REQ-029, AGENTS.md section 7).
        public int RareEncounterChanceNumerator { get; set; } = 12;
        public int RareEncounterChanceDenominator { get; set; } = 100;
        /// <summary>Number of reward choices earned after clearing a Rare node.</summary>
        public int RareRewardSelectionCount { get; set; } = 2;

        // Provisional obstacle tuning (REQ-023, AGENTS.md section 7).
        // Shape and rewards remain configurable until the human balance pass.
        public int MaxObstacles { get; set; } = 32;
        public int ObstacleHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int ObstacleHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int ObstacleContactDamage { get; set; } = 1;
        public int BreakableObstacleScore { get; set; } = 25;
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
        public int[] MainShotAngleLutSlots { get; set; } =
            Array.Empty<int>();
        public int MissileBaseDamage { get; set; } = 2;
        public int MissileDamageGrowthPercentPerLevel { get; set; } = 50;
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
        public MissileFamily MissileFamily { get; set; } =
            MissileFamily.Straight;
        public int MissilePierceEnemyCount { get; set; }
        public int MissileExplosionDamage { get; set; }
        public int MissileExplosionRadiusSubUnits { get; set; }
        public int MissileExplosionMaxTargets { get; set; }
        public int MissileDropDelayTicks { get; set; }
        /// <summary>
        /// Player-position history distance between consecutive options.
        /// Option N follows the position from N * OptionFollowDelayTicks ago.
        /// </summary>
        public int OptionFollowDelayTicks { get; set; } = 12;
        public OptionFormation OptionFormation { get; set; } =
            OptionFormation.Trail;
        public int[] OptionFixedOffsetXs { get; set; } =
            Array.Empty<int>();
        public int[] OptionFixedOffsetYs { get; set; } =
            Array.Empty<int>();
        public int OptionOrbitRadiusSubUnits { get; set; } =
            7 * SimSpace.SubUnitsPerWorldUnit / 4;
        public int OptionOrbitAngularLutSlotsNumerator { get; set; } = 1;
        public int OptionOrbitAngularLutSlotsDenominator { get; set; } = 2;

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
                StartingShieldStock = 1,
                MaxShieldStock = ProvisionalMaxShieldStock,
                PlayerHitInvulnerabilityTicks =
                    DefaultPlayerHitInvulnerabilityTicks,
                StartingBombStock = 0,
                MaxBombStock = ProvisionalMaxBombStock,
                BombInvulnerabilityTicks =
                    DefaultBombInvulnerabilityTicks,
                PlayerHalfWidth = 3 * u / 8,
                PlayerHalfHeight = 3 * u / 8,
                CapsuleMagnetRadiusSubUnits = 3 * u,
                CapsuleMagnetSpeedNumerator = 8 * u,
                CapsuleMagnetSpeedDenominator = SimSpace.TicksPerSecond
            };
        }

        internal BattleSimConfig Copy()
        {
            var copy = (BattleSimConfig)MemberwiseClone();
            copy.OptionFixedOffsetXs = OptionFixedOffsetXs == null
                ? null
                : (int[])OptionFixedOffsetXs.Clone();
            copy.OptionFixedOffsetYs = OptionFixedOffsetYs == null
                ? null
                : (int[])OptionFixedOffsetYs.Clone();
            copy.MainShotAngleLutSlots =
                MainShotAngleLutSlots == null
                    ? null
                    : (int[])MainShotAngleLutSlots.Clone();
            return copy;
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
        int TicksSinceLastKill { get; }
        BattleStatistics Statistics { get; }
        long ScrollX { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        bool IsPlayerAlive { get; }
        int ShieldStock { get; }
        int BombStock { get; }
        int PlayerInvulnerabilityTicksRemaining { get; }
        /// <summary>
        /// Compatibility health flag: one while alive, zero after the lethal
        /// unshielded hit. It is no longer a multi-point hull resource.
        /// </summary>
        int PlayerHp { get; }
        /// <summary>Compatibility alias for ShieldStock.</summary>
        int ShieldRemaining { get; }
        WeaponType PlayerWeaponType { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        IReadOnlyList<OptionState> Options { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        /// <summary>
        /// Stable read-only obstacle view. Enemy bullets intentionally pass through
        /// obstacles so terrain cannot erase hostile fire or create safe zones.
        /// </summary>
        IReadOnlyList<ObstacleState> Obstacles { get; }
        IReadOnlyList<CapsuleState> Capsules { get; }
        IReadOnlyList<BombPickupState> BombPickups { get; }
        IReadOnlyList<LaserState> Lasers { get; }
        StageEnvironmentState Environment { get; }
        bool VisionObscured { get; }
        int TimeLimitTicks { get; }
        int RemainingTimeTicks { get; }
        bool TimeLimitExpired { get; }
        /// <summary>Events emitted by the most recent Step. Cleared at the start of each Step.</summary>
        ReadOnlySpan<SimEvent> EventsThisTick { get; }
        /// <summary>보스전 진행 중 여부. false면 Boss 값은 무의미하다.</summary>
        bool BossActive { get; }
        /// <summary>
        /// True while the boss is gliding from fully off-screen to its combat
        /// hold point. Entry is non-firing and invulnerable.
        /// </summary>
        bool BossEntering { get; }
        BossState Boss { get; }
        /// <summary>Stable allocation-free view of multipart boss state.</summary>
        IReadOnlyList<BossPartState> BossParts { get; }
        void Step(in InputCommand input);
    }

    /// <summary>
    /// Deterministic state intentionally carried across combat-room boundaries
    /// within one biome. Transient entities and attack cooldowns are excluded.
    /// </summary>
    public sealed class BattleContinuityState
    {
        public BattleContinuityState(
            int playerX,
            int playerY,
            int multiplierLevel,
            int comboGauge,
            int ticksSinceLastKill)
        {
            if (multiplierLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(multiplierLevel));
            if (comboGauge < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(comboGauge));
            if (ticksSinceLastKill < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ticksSinceLastKill));
            PlayerX = playerX;
            PlayerY = playerY;
            MultiplierLevel = multiplierLevel;
            ComboGauge = comboGauge;
            TicksSinceLastKill = ticksSinceLastKill;
        }

        public int PlayerX { get; }
        public int PlayerY { get; }
        public int MultiplierLevel { get; }
        public int ComboGauge { get; }
        public int TicksSinceLastKill { get; }
    }

    /// <summary>Deterministic integer-only combat and generated-stage simulation.</summary>
    public sealed class BattleSim : IBattleSim
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
        int _mainShotRapidFireStartLevel;
        int _mainShotFireIntervalReductionPerLevel;
        int _mainShotMinimumFireIntervalTicks;
        readonly int _missileBaseDamage;
        readonly int _missileDamageGrowthPercentPerLevel;
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
        readonly int _maxObstacles, _obstacleHalfWidth, _obstacleHalfHeight;
        readonly int _obstacleContactDamage, _breakableObstacleScore;
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
        readonly List<ObstacleState> _obstacles;
        readonly List<int> _obstacleAges;
        readonly List<LaserAttackDefinition> _obstacleLaserAttacks;
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
        readonly int _killExplosionRadiusSubUnits, _killExplosionDamage;
        readonly int _killExplosionMaxTargets;
        readonly int _grazeExtraRadiusSubUnits, _grazeScore;
        readonly int _grazeComboGaugeGain, _killComboGaugeGain;
        readonly int _comboDecayTicks;
        readonly int[] _comboGaugeRequirements;
        readonly int[] _comboMultipliers;

        // 보스 (REQ-007). _bossMaxHp == 0 이면 이 스테이지에 보스전 없음.
        readonly int _bossMaxHp, _bossRuntimeMaxHp;
        readonly int _bossHalfWidth, _bossHalfHeight, _bossHoldX;
        readonly int _bossSpawnX, _bossEntryStartTick;
        readonly IReadOnlyList<Generation.BossPhase> _bossPhases;
        readonly IReadOnlyList<BossPartDefinition> _bossPartDefinitions;
        readonly BossPartState[] _bossPartStates;
        readonly ReadOnlyCollection<BossPartState> _readOnlyBossParts;
        readonly int[] _bossPartFireCooldowns;
        readonly int[] _bossPartRegenerationRemaining;
        readonly bool[] _bossPartContactHitThisCycle;
        readonly EnemyDefinition[] _bossPartSpawnDefinitions;
        readonly int _stageTotalTicks;
        bool _bossSpawned, _bossDefeated;
        int _bossId, _bossX, _bossY, _bossHp, _bossPhase, _bossAge, _bossFireCooldown;
        int _bossPhaseAge;
        int _bossMovementAnchorY;
        int _bossMovementPhaseOffsetTicks;
        int _bossVelocityY;
        bool _bossPhaseTelegraphPending;
        bool _bossBurstAwaitingVolley;
        int _bossPatternVolleyIndex;
        readonly bool _bossUsesTimedPattern;
        int _bossSuctionXRemainder, _bossSuctionYRemainder;

        const int BossHoverAmplitude = 3 * SimSpace.SubUnitsPerWorldUnit;
        const int BossGlideSpeedPerTick = 64;
        const int BossHoverPeriodShift = 2;                            // age >> 2 → 약 4.3초 주기
        const int SpreadStepLutSlots = 2;                              // n-way 간격 = 11.25°
        const int SpiralStepLutSlots = 2;

        readonly SimEvent[] _events;
        readonly int[] _enemyScanIds;
        readonly long[] _enemyScanDistances;
        int _eventCount;
        long _shotsFired, _shotsHit, _kills, _capsulesCollected, _grazeCount;

        long _playerXRemainder, _playerYRemainder;
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
        int _multiplierLevel, _comboGauge, _ticksSinceLastKill;
        bool _killScoredThisTick, _activateHeld, _bombHeld, _playerAlive;
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
                null)
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
                null)
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
                null)
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
                null)
        {
        }

        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            BattleModifierStackSet modifierStacks,
            BattleContinuityState continuityState)
            : this(
                config,
                rng,
                stagePlan,
                content,
                powerUpGauge,
                modifierStacks,
                true,
                continuityState)
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
            BattleContinuityState continuityState)
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
            _playerMinY = config.PlayerMinY;
            _playerMaxY = config.PlayerMaxY;
            _bulletDespawnX = config.BulletDespawnX;
            _enemyDespawnX = config.EnemyDespawnX;
            _playerHalfWidth = config.PlayerHalfWidth;
            _playerHalfHeight = config.PlayerHalfHeight;
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
            _maxObstacles = config.MaxObstacles;
            _obstacleHalfWidth = config.ObstacleHalfWidth;
            _obstacleHalfHeight = config.ObstacleHalfHeight;
            _obstacleContactDamage = config.ObstacleContactDamage;
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
            _shieldGaugeLevel = powerUpGauge == null
                ? 0
                : GetEffectivePowerLevel(PowerUpSlot.Shield);
            _dropRng = rng.Fork(DropRngStream);
            _bombDropRng = rng.Fork(BombDropRngStream);
            _bossPatternRng = rng.Fork(BossPatternRngStream);

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

            _bossPartStates =
                new BossPartState[_bossPartDefinitions.Count];
            _readOnlyBossParts = Array.AsReadOnly(_bossPartStates);
            _bossPartFireCooldowns =
                new int[_bossPartDefinitions.Count];
            _bossPartRegenerationRemaining =
                new int[_bossPartDefinitions.Count];
            _bossPartContactHitThisCycle =
                new bool[_bossPartDefinitions.Count];
            _bossPartSpawnDefinitions =
                new EnemyDefinition[_bossPartDefinitions.Count];
            ResolveBossPartRuntimeData();
            _bossRuntimeMaxHp = _bossPartStates.Length == 0
                ? _bossMaxHp
                : SumBossPartMaxHp();

            if (stageEnabled)
            {
                _stageSegments = stagePlan.Segments;
                _segmentStartTicks =
                    BuildSegmentStartTicks(stagePlan);
                _visionObscured =
                    stagePlan.Gimmick.VisionObscured;
                _timeLimitTicks =
                    stagePlan.Gimmick.TimeLimitTicks;
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
            int bossEntryDistance =
                Math.Max(0, _bossSpawnX - _bossHoldX);
            int bossEntryTicks =
                (bossEntryDistance + BossGlideSpeedPerTick - 1)
                / BossGlideSpeedPerTick;
            _bossEntryStartTick =
                Math.Max(0, _stageTotalTicks - bossEntryTicks);

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
            _obstacles = new List<ObstacleState>(_maxObstacles);
            _obstacleAges = new List<int>(_maxObstacles);
            _obstacleLaserAttacks =
                new List<LaserAttackDefinition>(_maxObstacles);
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
                + 3L * _maxLasers;
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
            PlayerY = continuityState == null
                ? config.PlayerSpawnY
                : Math.Max(
                    _playerMinY,
                    Math.Min(
                        _playerMaxY,
                        continuityState.PlayerY));
            if (continuityState != null)
            {
                _multiplierLevel =
                    continuityState.MultiplierLevel;
                _comboGauge = continuityState.ComboGauge;
                _ticksSinceLastKill =
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
        public int TicksSinceLastKill => _ticksSinceLastKill;

        public BattleContinuityState CaptureContinuityState()
        {
            return new BattleContinuityState(
                PlayerX,
                PlayerY,
                _multiplierLevel,
                _comboGauge,
                _ticksSinceLastKill);
        }
        public BattleStatistics Statistics => new BattleStatistics(
            _shotsFired,
            _shotsHit,
            _kills,
            _capsulesCollected,
            _grazeCount);
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
        public WeaponType PlayerWeaponType => _playerWeaponType;
        public PrimaryWeaponFamily EquippedPrimaryWeaponFamily =>
            _equippedPrimaryWeaponFamily;
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;
        public IReadOnlyList<OptionState> Options => _readOnlyOptions;
        public IReadOnlyList<EnemyState> Enemies => _readOnlyEnemies;
        public IReadOnlyList<ObstacleState> Obstacles => _readOnlyObstacles;
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
        public bool BossEntering =>
            BossActive && _bossX > _bossHoldX;
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
                : _bossPhases[_bossPhase].PartVulnerability);
        public IReadOnlyList<BossPartState> BossParts =>
            _readOnlyBossParts;
        /// <summary>보스전이 예정된 스테이지인지 (RunManager가 종료 조건 분기에 쓴다).</summary>
        public bool HasBossBattle => _bossMaxHp > 0;
        public bool BossDefeated => _bossDefeated;

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
                    definition.IsCore,
                    false);
                _bossPartFireCooldowns[i] =
                    definition.Attack.IntervalTicks;
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

        int SumBossPartMaxHp()
        {
            int total = 0;
            for (int i = 0; i < _bossPartStates.Length; i++)
                total = SaturatingAddDamage(
                    total,
                    _bossPartStates[i].MaxHp);
            return total;
        }

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
            if (_playerInvulnerabilityTicksRemaining > 0)
                _playerInvulnerabilityTicksRemaining--;

            UpdateEnvironmentState();
            ExpireTimeLimitIfNeeded();
            AdvancePlayer(in input);
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
            AdvanceBullets();
            AdvanceEnemies();
            AdvanceObstacles();
            AdvanceCapsules();
            AdvanceBombPickups();
            SpawnScheduledThroughTick(Tick);
            UpdateBoss();
            if (bombPressed)
                TryActivateBomb();
            ResolvePlayerBulletObstacleCollisions();
            ResolvePlayerBulletEnemyCollisions();
            ResolvePlayerBulletBossCollisions();
            RefreshLaserSegments();
            ResolveEnemyBulletPlayerCollisions();
            ResolveLaserPlayerCollisions();
            ResolveEnemyPlayerCollisions();
            ResolveObstaclePlayerCollisions();
            ResolveCapsulePlayerCollisions();
            ResolveBombPickupPlayerCollisions();
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
            if (family == _equippedPrimaryWeaponFamily)
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

        void UpdateOptionPositions()
        {
            while (_options.Count > _optionLevel)
                _options.RemoveAt(_options.Count - 1);
            while (_options.Count < _optionLevel)
                _options.Add(default);

            for (int i = 0; i < _options.Count; i++)
            {
                int index = i + 1;
                int x;
                int y;
                if (_optionFormation == OptionFormation.Fixed)
                {
                    x = SaturateToInt(
                        (long)PlayerX + _optionFixedOffsetXs[i]);
                    y = Math.Max(
                        _playerMinY,
                        Math.Min(
                            _playerMaxY,
                            SaturateToInt(
                                (long)PlayerY
                                + _optionFixedOffsetYs[i])));
                }
                else if (_optionFormation == OptionFormation.Orbit)
                {
                    int baseSlot = (int)(
                        (long)Tick
                        * _optionOrbitAngularLutSlotsNumerator
                        / _optionOrbitAngularLutSlotsDenominator);
                    int slot = (
                        baseSlot
                        + i * SineLut.Length / _options.Count)
                        % SineLut.Length;
                    int sin = SineLut[slot];
                    int cos = SineLut[
                        (slot + SineLut.Length / 4)
                        % SineLut.Length];
                    x = SaturateToInt(
                        (long)PlayerX
                        + (long)_optionOrbitRadiusSubUnits
                            * cos / SineScale);
                    y = SaturateToInt(
                        (long)PlayerY
                        + (long)_optionOrbitRadiusSubUnits
                            * sin / SineScale);
                }
                else
                {
                    GetPlayerPositionAgo(
                        checked(index * _optionFollowDelayTicks),
                        out x,
                        out y);
                }
                _options[i] = new OptionState(index, x, y);
            }
        }

        void ValidateLoadoutConfig()
        {
            if (!Enum.IsDefined(typeof(MissileFamily), _missileFamily))
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.MissileFamily));
            if (!Enum.IsDefined(typeof(OptionFormation), _optionFormation))
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.OptionFormation));
            if (_missilePierceEnemyCount < 0
                || _missileExplosionDamage < 0
                || _missileExplosionRadiusSubUnits < 0
                || _missileExplosionMaxTargets < 0
                || _missileDamageGrowthPercentPerLevel < 0
                || _missileDropDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.MissilePierceEnemyCount));
            if (_missileFamily == MissileFamily.DownwardDrop
                && (_missileFallSpeedYNumerator < 1
                    || _missileDropDelayTicks < 1))
                throw new ArgumentException(
                    "Downward-drop missile config requires fall speed "
                    + "and drop delay.");
            if (_optionOrbitRadiusSubUnits < 0
                || _optionOrbitAngularLutSlotsNumerator < 0
                || _optionOrbitAngularLutSlotsDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.OptionOrbitRadiusSubUnits));
            if (_optionFormation == OptionFormation.Fixed
                && _optionFixedOffsetXs.Length
                    != _optionFixedOffsetYs.Length)
                throw new ArgumentException(
                    "Fixed formation requires one X/Y offset per option.");
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

        // 46340 / 65536 is the greatest 16-bit fixed-point diagonal
        // component whose two-dimensional magnitude does not exceed one.
        const int DigitalDirectionScale = 65536;
        const int DigitalDiagonalComponent = 46340;

        void UpdateEnvironmentState()
        {
            int segmentIndex = -1;
            for (int i = 0; i < _stageSegments.Count; i++)
            {
                long endTick =
                    (long)_segmentStartTicks[i]
                    + _stageSegments[i].LengthTicks;
                if (Tick >= _segmentStartTicks[i] && Tick < endTick)
                {
                    segmentIndex = i;
                    break;
                }
            }

            if (segmentIndex != _currentEnvironmentSegmentIndex)
            {
                _driftXRemainder = 0;
                _driftYRemainder = 0;
                _currentEnvironmentSegmentIndex = segmentIndex;
            }
            if (segmentIndex < 0)
            {
                _environment = new StageEnvironmentState(
                    -1,
                    null,
                    false,
                    0,
                    0,
                    0,
                    0,
                    1,
                    0,
                    1);
                return;
            }

            StageSegment segment = _stageSegments[segmentIndex];
            SegmentEnvironmentDefinition definition =
                segment.Environment;
            int localTick = Tick - _segmentStartTicks[segmentIndex];
            int corridorMinY = definition.HasCorridor
                ? InterpolateSegmentValue(
                    definition.StartMinY,
                    definition.EndMinY,
                    localTick,
                    segment.LengthTicks)
                : 0;
            int corridorMaxY = definition.HasCorridor
                ? InterpolateSegmentValue(
                    definition.StartMaxY,
                    definition.EndMaxY,
                    localTick,
                    segment.LengthTicks)
                : 0;
            _environment = new StageEnvironmentState(
                segmentIndex,
                segment.SegmentId,
                definition.HasCorridor,
                corridorMinY,
                corridorMaxY,
                definition.CorridorContactDamage,
                definition.DriftXNumerator,
                definition.DriftXDenominator,
                definition.DriftYNumerator,
                definition.DriftYDenominator);
        }

        static int InterpolateSegmentValue(
            int start,
            int end,
            int elapsedTicks,
            int durationTicks)
        {
            return SaturateToInt(
                (long)start
                + ((long)end - start) * elapsedTicks / durationTicks);
        }

        void ExpireTimeLimitIfNeeded()
        {
            if (_timeLimitExpired
                || _timeLimitTicks == 0
                || Tick < _timeLimitTicks
                || _bossDefeated)
                return;

            _timeLimitExpired = true;
            EmitEvent(
                SimEventType.TimeLimitExpired,
                0,
                PlayerX,
                PlayerY,
                _timeLimitTicks);
            if (!_playerAlive)
                return;
            ShieldStock = 0;
            _playerAlive = false;
            EmitEvent(
                SimEventType.PlayerKilled,
                0,
                PlayerX,
                PlayerY,
                0);
        }

        void AdvancePlayer(in InputCommand input)
        {
            int controlledX;
            int controlledY;
            if (input.UseAnalogMovement)
            {
                _playerXRemainder = 0;
                _playerYRemainder = 0;
                ClampAnalogDelta(
                    input.AnalogDeltaXSubUnits,
                    input.AnalogDeltaYSubUnits,
                    out int deltaX,
                    out int deltaY);
                controlledX = ClampPlayerPosition(
                    PlayerX,
                    deltaX,
                    _playerMinX,
                    _playerMaxX);
                controlledY = ClampPlayerPosition(
                    PlayerY,
                    deltaY,
                    _playerMinY,
                    _playerMaxY);
            }
            else
            {
                int componentScale =
                    input.MoveX != 0 && input.MoveY != 0
                        ? DigitalDiagonalComponent
                        : DigitalDirectionScale;
                controlledX = AdvanceDigitalPlayerAxis(
                    PlayerX,
                    input.MoveX,
                    componentScale,
                    ref _playerXRemainder,
                    _playerMinX,
                    _playerMaxX);
                controlledY = AdvanceDigitalPlayerAxis(
                    PlayerY,
                    input.MoveY,
                    componentScale,
                    ref _playerYRemainder,
                    _playerMinY,
                    _playerMaxY);
            }

            int driftX = AdvanceSignedFraction(
                _environment.DriftXNumerator,
                _environment.DriftXDenominator,
                ref _driftXRemainder);
            int driftY = AdvanceSignedFraction(
                _environment.DriftYNumerator,
                _environment.DriftYDenominator,
                ref _driftYRemainder);
            PlayerX = ClampPlayerPosition(
                controlledX,
                driftX,
                _playerMinX,
                _playerMaxX);

            long candidateY = (long)controlledY + driftY;
            int minimumY = _playerMinY;
            int maximumY = _playerMaxY;
            bool corridorContact = false;
            if (_environment.HasCorridor)
            {
                minimumY = Math.Max(
                    minimumY,
                    SaturateToInt(
                        (long)_environment.CorridorMinY
                        + _playerHalfHeight));
                maximumY = Math.Min(
                    maximumY,
                    SaturateToInt(
                        (long)_environment.CorridorMaxY
                        - _playerHalfHeight));
                if (minimumY > maximumY)
                    throw new InvalidOperationException(
                        "The active corridor is narrower than the player hitbox.");
                corridorContact =
                    candidateY < minimumY || candidateY > maximumY;
            }
            PlayerY = candidateY <= minimumY
                ? minimumY
                : candidateY >= maximumY
                    ? maximumY
                    : (int)candidateY;
            if (corridorContact)
            {
                EmitEvent(
                    SimEventType.CorridorContact,
                    _environment.SegmentIndex,
                    PlayerX,
                    PlayerY,
                    _environment.CorridorContactDamage);
                ApplyPlayerHit(
                    _environment.CorridorContactDamage);
            }
        }

        static int AdvanceSignedFraction(
            int numerator,
            int denominator,
            ref long remainder)
        {
            long accumulated = remainder + numerator;
            int delta = (int)(accumulated / denominator);
            remainder = accumulated % denominator;
            return delta;
        }

        void ClampAnalogDelta(
            int requestedX,
            int requestedY,
            out int deltaX,
            out int deltaY)
        {
            if ((requestedX == 0 && requestedY == 0)
                || _playerSpeedNumerator == 0)
            {
                deltaX = 0;
                deltaY = 0;
                return;
            }

            ulong absoluteX = AbsoluteAsUnsigned(requestedX);
            ulong absoluteY = AbsoluteAsUnsigned(requestedY);
            ulong lengthSquared =
                absoluteX * absoluteX + absoluteY * absoluteY;
            ulong speedNumerator = (ulong)_playerSpeedNumerator;
            ulong speedDenominator = (ulong)_playerSpeedDenominator;
            ulong maximumLengthSquared =
                speedNumerator * speedNumerator
                / (speedDenominator * speedDenominator);

            if (lengthSquared <= maximumLengthSquared)
            {
                deltaX = requestedX;
                deltaY = requestedY;
                return;
            }

            ulong lengthCeiling = IntegerSquareRoot(lengthSquared);
            if (lengthCeiling * lengthCeiling < lengthSquared)
                lengthCeiling++;
            long divisor =
                (long)speedDenominator * (long)lengthCeiling;
            deltaX = (int)(
                (long)requestedX * _playerSpeedNumerator / divisor);
            deltaY = (int)(
                (long)requestedY * _playerSpeedNumerator / divisor);
        }

        int AdvanceDigitalPlayerAxis(
            int position,
            int direction,
            int componentScale,
            ref long remainder,
            int min,
            int max)
        {
            if (direction == 0) return position;
            long divisor =
                (long)_playerSpeedDenominator
                * DigitalDirectionScale;
            long accumulated =
                remainder
                + (long)direction
                * _playerSpeedNumerator
                * componentScale;
            long candidate = position + accumulated / divisor;
            long nextRemainder = accumulated % divisor;
            if (direction < 0 && candidate <= min) { remainder = 0; return min; }
            if (direction > 0 && candidate >= max) { remainder = 0; return max; }
            remainder = nextRemainder;
            return (int)candidate;
        }

        static int ClampPlayerPosition(
            int position,
            int delta,
            int min,
            int max)
        {
            long candidate = (long)position + delta;
            if (candidate <= min)
                return min;
            if (candidate >= max)
                return max;
            return (int)candidate;
        }

        static ulong AbsoluteAsUnsigned(int value)
        {
            return value < 0
                ? (ulong)(-(long)value)
                : (ulong)value;
        }

        static ulong IntegerSquareRoot(ulong value)
        {
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value)
                bit >>= 2;
            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return result;
        }

        void AdvanceBullets()
        {
            int despawnY = SimSpace.PlayfieldHalfHeightSubUnits + SimSpace.DespawnMarginSubUnits;
            int write = 0;
            for (int read = 0; read < _bullets.Count; read++)
            {
                BulletState bullet = _bullets[read];
                if (bullet.Kind == BulletKind.Missile
                    && (_missileFamily == MissileFamily.Homing
                        || HasModifier(BattleModifier.HomingMissile)))
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
                    bool waitingToDrop =
                        isMissile
                        && _missileFamily == MissileFamily.DownwardDrop
                        && bullet.AgeTicks < _missileDropDelayTicks;
                    yNumerator = isMissile && !waitingToDrop
                        ? -_missileFallSpeedYNumerator
                        : 0;
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
                if (bullet.Kind == BulletKind.Missile
                    && _missileFamily == MissileFamily.SpreadBomb
                    && nextYLong
                        <= -SimSpace.PlayfieldHalfHeightSubUnits)
                {
                    ApplyMissileExplosion(
                        bullet.Id,
                        SaturateToInt(nextX),
                        -SimSpace.PlayfieldHalfHeightSubUnits);
                    ClearBulletHitRecords(bullet.Id);
                    continue;
                }
                if (nextX > _bulletDespawnX || nextX < -(long)_bulletDespawnX
                    || nextYLong > despawnY || nextYLong < -(long)despawnY)
                {
                    ClearBulletHitRecords(bullet.Id);
                    continue;
                }
                int nextY = SaturateToInt(nextYLong);
                int nextAge = bullet.AgeTicks == int.MaxValue
                    ? int.MaxValue
                    : bullet.AgeTicks + 1;
                _bullets[write] = new BulletState(
                    bullet.Id,
                    bullet.Faction,
                    bullet.Kind,
                    (int)nextX,
                    nextY,
                    nextAge);
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
            if (_missileFamily == MissileFamily.DownwardDrop
                && bullet.AgeTicks < _missileDropDelayTicks)
                return;
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
                if (definition.LaserAttack != null
                    && age % definition.LaserAttack.CycleIntervalTicks
                        == 0)
                {
                    TryStartLaser(
                        LaserSourceKind.Enemy,
                        state.Id,
                        definition.LaserAttack,
                        x,
                        y);
                }
                else if (definition.LaserAttack == null
                    && definition.FireIntervalTicks > 0
                    && age % definition.FireIntervalTicks == 0)
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
                TrySpawnEnemy(spawn.Definition, spawn.X, spawn.Y);
            }

            while (_nextScheduledObstacle < _scheduledObstacles.Length
                && _scheduledObstacles[_nextScheduledObstacle].Tick <= tick)
            {
                ScheduledObstacle scheduled =
                    _scheduledObstacles[_nextScheduledObstacle++];
                if (_obstacles.Count >= _maxObstacles)
                {
                    EmitEvent(
                        SimEventType.ObstacleCapacityExceeded,
                        0,
                        scheduled.Obstacle.X,
                        scheduled.Obstacle.Y,
                        _maxObstacles);
                    continue;
                }
                if (_nextObstacleId == int.MaxValue)
                    throw new InvalidOperationException(
                        "The obstacle id counter is exhausted.");

                ObstacleSpawn obstacle = scheduled.Obstacle;
                int obstacleId = _nextObstacleId++;
                _obstacles.Add(new ObstacleState(
                    obstacleId,
                    obstacle.Type,
                    obstacle.X,
                    obstacle.Y,
                    obstacle.Hp));
                _obstacleAges.Add(0);
                _obstacleLaserAttacks.Add(obstacle.LaserAttack);
                if (obstacle.LaserAttack != null)
                    TryStartLaser(
                        LaserSourceKind.Terrain,
                        obstacleId,
                        obstacle.LaserAttack,
                        obstacle.X,
                        obstacle.Y);
            }
        }

        void SpawnBossEnemy(
            EnemyDefinition definition,
            int x,
            int y)
        {
            if (definition == null)
                return;
            TrySpawnEnemy(definition, x, y);
        }

        bool TrySpawnEnemy(
            EnemyDefinition definition,
            int x,
            int y)
        {
            if (_enemies.Count >= _maxEnemies)
            {
                EmitEvent(
                    SimEventType.EnemyCapacityExceeded,
                    0,
                    x,
                    y,
                    _maxEnemies);
                return false;
            }
            if (_nextEnemyId == int.MaxValue)
                throw new InvalidOperationException(
                    "The enemy id counter is exhausted.");

            _enemies.Add(new EnemyState(
                _nextEnemyId++,
                definition.Id,
                x,
                y,
                ScaleEnemyHp(definition.MaxHp)));
            _enemyDefinitions.Add(definition);
            _enemyXRemainders.Add(0);
            _enemySpawnYs.Add(y);
            _enemyAges.Add(0);
            _enemyDiveTargetYs.Add(y);
            _enemyMovementFlags.Add(0);
            return true;
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
                if (Tick < _bossEntryStartTick) return;
                if (_nextEnemyId == int.MaxValue)
                    throw new InvalidOperationException("The enemy id counter is exhausted.");
                _bossSpawned = true;
                _bossId = _nextEnemyId++;
                _bossX = _bossSpawnX;
                _bossY = 0;
                _bossHp = _bossMaxHp;
                _bossPhase = 0;
                _bossAge = 0;
                _bossPhaseAge = 0;
                _bossMovementAnchorY = _bossY;
                _bossMovementPhaseOffsetTicks = 0;
                _bossVelocityY = 0;
                Generation.BossPhase initialPhase =
                    _bossPhases[0];
                _bossFireCooldown =
                    initialPhase.TelegraphTicks > 0
                        ? initialPhase.TelegraphTicks
                        : initialPhase.FireIntervalTicks;
                _bossPhaseTelegraphPending =
                    initialPhase.TelegraphTicks > 0;
                _bossBurstAwaitingVolley =
                    initialPhase.FirePattern == BossFirePattern.Burst
                    && initialPhase.TelegraphTicks > 0;
                _bossPatternVolleyIndex = 0;
                InitializeBossParts();
                EmitEvent(SimEventType.BossSpawned, _bossId, _bossX, _bossY, 0);
                return;
            }

            if (_bossX > _bossHoldX)
            {
                _bossX = Math.Max(
                    _bossHoldX,
                    _bossX - BossGlideSpeedPerTick);
                RefreshBossPartPositions();
                return;   // 진입 중에는 사격하지 않는다 (등장 연출 여유)
            }

            _bossAge++;
            AdvanceTimedBossPhase();
            EmitPendingBossTelegraph();
            Generation.BossPhase phase = _bossPhases[_bossPhase];
            if (_bossPartDefinitions.Count > 0)
            {
                UpdateMultipartBoss(phase);
                UpdateBossPhaseFire(phase);
                if (_bossUsesTimedPattern)
                    _bossPhaseAge++;
                return;
            }

            ApplyBossPhaseMovement(phase, false);
            UpdateBossPhaseFire(phase);
            if (_bossUsesTimedPattern)
                _bossPhaseAge++;
        }

        static bool ResolveTimedBossPattern(
            IReadOnlyList<Generation.BossPhase> phases)
        {
            if (phases.Count == 0)
                return false;
            bool timed = phases[0].DurationTicks > 0;
            for (int i = 1; i < phases.Count; i++)
            {
                if ((phases[i].DurationTicks > 0) != timed)
                    throw new ArgumentException(
                        "Boss phases cannot mix timed and HP-based progression.",
                        nameof(phases));
            }
            return timed;
        }

        int GetBossLeftExtent()
        {
            int extent = _bossHalfWidth;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition part =
                    _bossPartDefinitions[i];
                extent = Math.Max(
                    extent,
                    SaturateToInt(
                        (long)part.HalfWidth - part.OffsetX));
            }
            return Math.Max(0, extent);
        }

        void AdvanceTimedBossPhase()
        {
            if (!_bossUsesTimedPattern)
                return;
            Generation.BossPhase phase =
                _bossPhases[_bossPhase];
            if (_bossPhaseAge < phase.DurationTicks)
                return;
            int nextPhase = (_bossPhase + 1) % _bossPhases.Count;
            EnterBossPhase(nextPhase, true);
        }

        void EnterBossPhase(
            int phaseIndex,
            bool emitChanged)
        {
            _bossPhase = phaseIndex;
            _bossPhaseAge = 0;
            Generation.BossPhase phase =
                _bossPhases[phaseIndex];
            ConfigureBossMovementPhase(phase);
            _bossFireCooldown = phase.TelegraphTicks > 0
                ? phase.TelegraphTicks
                : Math.Max(
                    1,
                    Math.Min(
                        _bossFireCooldown,
                        phase.FireIntervalTicks));
            _bossPhaseTelegraphPending =
                phase.TelegraphTicks > 0;
            _bossBurstAwaitingVolley =
                phase.FirePattern == BossFirePattern.Burst
                && phase.TelegraphTicks > 0;
            _bossPatternVolleyIndex = 0;
            RefreshBossPartPositions();
            if (emitChanged)
            {
                EmitEvent(
                    SimEventType.BossPhaseChanged,
                    _bossId,
                    _bossX,
                    _bossY,
                    phaseIndex);
            }
        }

        void EmitPendingBossTelegraph()
        {
            if (!_bossPhaseTelegraphPending)
                return;
            _bossPhaseTelegraphPending = false;
            EmitEvent(
                SimEventType.BossAttackTelegraphed,
                _bossId,
                _bossX,
                _bossY,
                _bossPhase);
        }

        void UpdateBossPhaseFire(Generation.BossPhase phase)
        {
            if (_bossFireCooldown > 0)
                _bossFireCooldown--;
            if (_bossFireCooldown != 0)
                return;

            if (phase.FirePattern == BossFirePattern.Burst)
            {
                if (_bossBurstAwaitingVolley)
                {
                    FireAimedBossVolley(phase);
                    _bossPatternVolleyIndex++;
                    _bossBurstAwaitingVolley = false;
                    _bossFireCooldown = phase.FireIntervalTicks;
                    return;
                }

                EmitEvent(
                    SimEventType.BossAttackTelegraphed,
                    _bossId,
                    _bossX,
                    _bossY,
                    _bossPhase);
                _bossBurstAwaitingVolley = true;
                _bossFireCooldown = phase.TelegraphTicks;
                return;
            }

            switch (phase.FirePattern)
            {
                case BossFirePattern.Aimed:
                    FireAimedBossVolley(phase);
                    break;
                case BossFirePattern.Radial:
                    FireRadialBossVolley(phase, 0);
                    break;
                case BossFirePattern.Spiral:
                    FireRadialBossVolley(
                        phase,
                        (_bossPatternVolleyIndex
                            * SpiralStepLutSlots)
                            % SineLut.Length);
                    break;
                case BossFirePattern.Wall:
                    FireWallBossVolley(phase);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss fire pattern.");
            }
            _bossPatternVolleyIndex++;
            _bossFireCooldown = phase.FireIntervalTicks;
        }

        void FireAimedBossVolley(Generation.BossPhase phase)
        {
            int shots = GetBossVolleyShotCount(phase.Ways);
            for (int i = 0; i < shots; i++)
            {
                long centeredIndex =
                    2L * i - (phase.Ways - 1L);
                int rotation = (int)(
                    (centeredIndex * SpreadStepLutSlots / 2)
                    % SineLut.Length);
                SpawnEnemyAimedBullet(
                    _bossX,
                    _bossY,
                    PlayerX,
                    PlayerY,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    rotation);
            }
        }

        void FireRadialBossVolley(
            Generation.BossPhase phase,
            int baseRotation)
        {
            int shots = GetBossVolleyShotCount(phase.Ways);
            for (int i = 0; i < shots; i++)
            {
                int rotation =
                    (baseRotation
                        + (int)((long)i
                            * SineLut.Length
                            / phase.Ways))
                    % SineLut.Length;
                SpawnEnemyAimedBullet(
                    _bossX,
                    _bossY,
                    _bossX - SineScale,
                    _bossY,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    rotation);
            }
        }

        void FireWallBossVolley(Generation.BossPhase phase)
        {
            int gap = _bossPatternRng.NextInt(0, phase.Ways);
            int requested = phase.Ways - 1;
            int shots = GetBossVolleyShotCount(requested);
            int fired = 0;
            long height = (long)_playerMaxY - _playerMinY;
            for (int lane = 0;
                lane < phase.Ways && fired < shots;
                lane++)
            {
                if (lane == gap)
                    continue;
                int y = (int)(_playerMinY
                    + height * lane / (phase.Ways - 1));
                SpawnEnemyAimedBullet(
                    _bossX,
                    y,
                    _playerMinX,
                    y,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    0);
                fired++;
            }
        }

        int GetBossVolleyShotCount(int requested)
        {
            int available = Math.Max(
                0,
                _maxEnemyBullets - CountEnemyBullets());
            if (requested > available)
            {
                EmitEvent(
                    SimEventType.EnemyBulletCapacityExceeded,
                    _bossId,
                    _bossX,
                    _bossY,
                    _maxEnemyBullets);
            }
            return Math.Min(requested, available);
        }

        void ApplyBossPhaseMovement(
            Generation.BossPhase phase,
            bool legacyVerticalMovementActive)
        {
            int previousY = _bossY;
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.LegacyHover:
                {
                    if (_bossPartDefinitions.Count > 0
                        && !legacyVerticalMovementActive)
                    {
                        _bossY = _bossMovementAnchorY;
                        break;
                    }
                    int tick = _bossPhaseAge
                        + _bossMovementPhaseOffsetTicks;
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + ComputeLegacyHoverOffset(tick));
                    break;
                }
                case BossMovementPattern.Stationary:
                    _bossY = _bossMovementAnchorY;
                    break;
                case BossMovementPattern.VerticalSine:
                {
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + ComputeVerticalSineOffset(
                            phase,
                            _bossPhaseAge
                                + _bossMovementPhaseOffsetTicks));
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unknown boss movement pattern "
                        + $"{phase.MovementPattern}.");
            }
            _bossVelocityY = SaturateToInt(
                (long)_bossY - previousY);
        }

        void ConfigureBossMovementPhase(
            Generation.BossPhase phase)
        {
            int carriedVelocity = _bossVelocityY;
            int phaseOffset = FindClosestMovementPhase(
                phase,
                carriedVelocity,
                SaturateToInt(
                    (long)_bossY + carriedVelocity));
            int firstOffset = ComputeMovementOffset(
                phase,
                phaseOffset);
            _bossMovementPhaseOffsetTicks = phaseOffset;
            _bossMovementAnchorY = SaturateToInt(
                (long)_bossY
                + carriedVelocity
                - firstOffset);
            int amplitude = GetMovementAmplitude(phase);
            int minimumAnchor = SaturateToInt(
                (long)_playerMinY
                + _bossHalfHeight
                + amplitude);
            int maximumAnchor = SaturateToInt(
                (long)_playerMaxY
                - _bossHalfHeight
                - amplitude);
            if (minimumAnchor <= maximumAnchor)
            {
                _bossMovementAnchorY = Math.Max(
                    minimumAnchor,
                    Math.Min(
                        maximumAnchor,
                        _bossMovementAnchorY));
            }
        }

        static int FindClosestMovementPhase(
            Generation.BossPhase phase,
            int velocity,
            int desiredPosition)
        {
            int period;
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                    return 0;
                case BossMovementPattern.LegacyHover:
                    period =
                        SineLut.Length << BossHoverPeriodShift;
                    break;
                case BossMovementPattern.VerticalSine:
                    period = phase.MovementPeriodTicks;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }

            int bestTick = 0;
            long bestPositionError = long.MaxValue;
            long bestError = long.MaxValue;
            for (int tick = 0; tick < period; tick++)
            {
                int offset =
                    ComputeMovementOffset(phase, tick);
                long positionError = Math.Abs(
                    (long)offset - desiredPosition);
                long candidateVelocity =
                    (long)ComputeMovementOffset(phase, tick + 1)
                    - offset;
                long error = Math.Abs(
                    candidateVelocity - velocity);
                if (positionError < bestPositionError
                    || (positionError == bestPositionError
                        && error < bestError))
                {
                    bestPositionError = positionError;
                    bestError = error;
                    bestTick = tick;
                }
            }
            return bestTick;
        }

        static int ComputeMovementOffset(
            Generation.BossPhase phase,
            int tick)
        {
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                    return 0;
                case BossMovementPattern.LegacyHover:
                    return ComputeLegacyHoverOffset(tick);
                case BossMovementPattern.VerticalSine:
                    return ComputeVerticalSineOffset(phase, tick);
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }
        }

        static int GetMovementAmplitude(
            Generation.BossPhase phase)
        {
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                    return 0;
                case BossMovementPattern.LegacyHover:
                    return BossHoverAmplitude;
                case BossMovementPattern.VerticalSine:
                    return SaturateToInt(
                        (long)phase.MovementAmplitudeNumerator
                        / phase.MovementAmplitudeDenominator);
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }
        }

        static int ComputeLegacyHoverOffset(int tick)
        {
            int period =
                SineLut.Length << BossHoverPeriodShift;
            int normalized = PositiveModulo(tick, period);
            int legacyIndex =
                (normalized >> BossHoverPeriodShift)
                % SineLut.Length;
            return SaturateToInt(
                (long)BossHoverAmplitude
                * SineLut[legacyIndex]
                / SineScale);
        }

        static int ComputeVerticalSineOffset(
            Generation.BossPhase phase,
            int tick)
        {
            int phaseTick = PositiveModulo(
                tick,
                phase.MovementPeriodTicks);
            int lutIndex = (int)(
                (long)phaseTick * SineLut.Length
                / phase.MovementPeriodTicks);
            long numerator =
                (long)phase.MovementAmplitudeNumerator
                * SineLut[lutIndex];
            long denominator =
                (long)phase.MovementAmplitudeDenominator
                * SineScale;
            return SaturateToInt(numerator / denominator);
        }

        static int PositiveModulo(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0
                ? remainder + modulus
                : remainder;
        }

        void InitializeBossParts()
        {
            int aggregateHp = 0;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                int maxHp = ScaleEnemyHp(definition.MaxHp);
                aggregateHp = SaturatingAddDamage(aggregateHp, maxHp);
                _bossPartFireCooldowns[i] =
                    definition.Attack.IntervalTicks;
                _bossPartRegenerationRemaining[i] = 0;
                _bossPartContactHitThisCycle[i] = false;
                _bossPartStates[i] = new BossPartState(
                    definition.PartId,
                    SaturateToInt((long)_bossX + definition.OffsetX),
                    SaturateToInt((long)_bossY + definition.OffsetY),
                    maxHp,
                    maxHp,
                    false,
                    definition.IsCore,
                    false);
            }
            if (_bossPartDefinitions.Count > 0)
            {
                _bossHp = aggregateHp;
                RefreshBossPartPositions();
            }
        }

        void UpdateMultipartBoss(Generation.BossPhase phase)
        {
            RegenerateBossParts();

            bool verticalMovementActive = false;
            int chargeOffset = 0;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                if (_bossPartStates[i].Destroyed)
                    continue;
                BossPartAttackProfile attack =
                    _bossPartDefinitions[i].Attack;
                if (attack.Type == BossPartAttackType.VerticalMovement)
                    verticalMovementActive = true;
                else if (attack.Type == BossPartAttackType.MeleeCharge)
                {
                    int cycle = _bossAge % attack.IntervalTicks;
                    int chargeTicks = Math.Max(
                        1,
                        attack.IntervalTicks / 4);
                    if (cycle < chargeTicks)
                    {
                        chargeOffset = Math.Max(
                            chargeOffset,
                            AdvancePositiveFraction(
                                cycle,
                                attack.EffectSpeedNumerator,
                                attack.EffectSpeedDenominator));
                    }
                }
            }

            _bossX = SaturateToInt(
                (long)_bossHoldX - chargeOffset);
            ApplyBossPhaseMovement(
                phase,
                verticalMovementActive);
            RefreshBossPartPositions();

            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartState state = _bossPartStates[i];
                if (state.Destroyed || IsBossCoreGated(i))
                    continue;
                BossPartAttackProfile attack =
                    _bossPartDefinitions[i].Attack;
                switch (attack.Type)
                {
                    case BossPartAttackType.None:
                    case BossPartAttackType.VerticalMovement:
                        break;
                    case BossPartAttackType.MeleeCharge:
                        ApplyBossMeleeContact(i, attack);
                        break;
                    case BossPartAttackType.Suction:
                        ApplyBossSuction(attack);
                        break;
                    default:
                        if (_bossPartFireCooldowns[i] > 0)
                            _bossPartFireCooldowns[i]--;
                        if (_bossPartFireCooldowns[i] == 0)
                        {
                            FireBossPartAttack(i, attack);
                            _bossPartFireCooldowns[i] =
                                attack.IntervalTicks;
                        }
                        break;
                }
            }
        }

        static int AdvancePositiveFraction(
            int ticks,
            int numerator,
            int denominator)
        {
            long value = (long)ticks * numerator / denominator;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        void RegenerateBossParts()
        {
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                if (!_bossPartStates[i].Destroyed
                    || _bossPartRegenerationRemaining[i] <= 0)
                    continue;
                _bossPartRegenerationRemaining[i]--;
                if (_bossPartRegenerationRemaining[i] != 0)
                    continue;

                BossPartState previous = _bossPartStates[i];
                _bossPartStates[i] = new BossPartState(
                    previous.PartId,
                    previous.X,
                    previous.Y,
                    previous.MaxHp,
                    previous.MaxHp,
                    false,
                    previous.IsCore,
                    false);
                _bossHp = SaturatingAddDamage(
                    _bossHp,
                    previous.MaxHp);
                _bossPartFireCooldowns[i] =
                    _bossPartDefinitions[i].Attack.IntervalTicks;
                _bossPartContactHitThisCycle[i] = false;
                EmitBossPartEvent(
                    SimEventType.BossPartRegenerated,
                    previous.X,
                    previous.Y,
                    i);
            }
        }

        void RefreshBossPartPositions()
        {
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                BossPartState state = _bossPartStates[i];
                _bossPartStates[i] = new BossPartState(
                    state.PartId,
                    SaturateToInt((long)_bossX + definition.OffsetX),
                    SaturateToInt((long)_bossY + definition.OffsetY),
                    state.Hp,
                    state.MaxHp,
                    state.Destroyed,
                    state.IsCore,
                    IsBossPartInvulnerable(i));
            }
        }

        bool IsBossPartInvulnerable(int partIndex)
        {
            if (BossEntering)
                return true;
            BossPartVulnerability vulnerability =
                _bossPhases[_bossPhase].PartVulnerability;
            switch (vulnerability)
            {
                case BossPartVulnerability.Legacy:
                    return IsBossCoreGated(partIndex);
                case BossPartVulnerability.CoreOnly:
                    return !_bossPartDefinitions[partIndex].IsCore
                        || IsBossCoreGated(partIndex);
                case BossPartVulnerability.All:
                    return false;
                default:
                    throw new InvalidOperationException(
                        $"Unknown boss part vulnerability "
                        + $"{vulnerability}.");
            }
        }

        bool IsBossCoreGated(int partIndex)
        {
            BossPartDefinition definition =
                _bossPartDefinitions[partIndex];
            if (!definition.IsCore)
                return false;
            for (int gate = 0;
                gate < definition.CoreGatePartIds.Count;
                gate++)
            {
                string gateId =
                    definition.CoreGatePartIds[gate];
                for (int i = 0;
                    i < _bossPartDefinitions.Count;
                    i++)
                {
                    if (string.Equals(
                            _bossPartDefinitions[i].PartId,
                            gateId,
                            StringComparison.Ordinal)
                        && !_bossPartStates[i].Destroyed)
                        return true;
                }
            }
            return false;
        }

        void FireBossPartAttack(
            int partIndex,
            BossPartAttackProfile attack)
        {
            BossPartState part = _bossPartStates[partIndex];
            if (attack.Type == BossPartAttackType.SpawnEnemy)
            {
                SpawnBossEnemy(
                    _bossPartSpawnDefinitions[partIndex],
                    part.X,
                    part.Y);
                return;
            }

            int available = Math.Max(
                0,
                _maxEnemyBullets - CountEnemyBullets());
            int shots = Math.Min(attack.Ways, available);
            for (int i = 0; i < shots; i++)
            {
                int targetX = PlayerX;
                int targetY = PlayerY;
                int rotation;
                if (attack.Type == BossPartAttackType.RadialSpread)
                {
                    rotation = (int)(
                        (long)i * SineLut.Length
                        / attack.Ways);
                    int sin = SineLut[rotation];
                    int cos = SineLut[
                        (rotation + SineLut.Length / 4)
                        % SineLut.Length];
                    targetX = SaturateToInt((long)part.X + cos);
                    targetY = SaturateToInt((long)part.Y + sin);
                    rotation = 0;
                }
                else
                {
                    long centeredIndex =
                        2L * i - (attack.Ways - 1L);
                    rotation = (int)(
                        (centeredIndex * SpreadStepLutSlots / 2)
                        % SineLut.Length);
                }
                SpawnEnemyAimedBullet(
                    part.X,
                    part.Y,
                    targetX,
                    targetY,
                    attack.BulletSpeedNumerator,
                    attack.BulletSpeedDenominator,
                    rotation);
            }
        }

        void ApplyBossSuction(BossPartAttackProfile attack)
        {
            PlayerX = PullAxis(
                PlayerX,
                _bossX,
                attack.EffectSpeedNumerator,
                attack.EffectSpeedDenominator,
                ref _bossSuctionXRemainder,
                _playerMinX,
                _playerMaxX);
            PlayerY = PullAxis(
                PlayerY,
                _bossY,
                attack.EffectSpeedNumerator,
                attack.EffectSpeedDenominator,
                ref _bossSuctionYRemainder,
                _playerMinY,
                _playerMaxY);
        }

        void ApplyBossMeleeContact(
            int partIndex,
            BossPartAttackProfile attack)
        {
            int cycle = _bossAge % attack.IntervalTicks;
            if (cycle == 0)
                _bossPartContactHitThisCycle[partIndex] = false;
            int chargeTicks = Math.Max(
                1,
                attack.IntervalTicks / 4);
            if (cycle >= chargeTicks
                || _bossPartContactHitThisCycle[partIndex]
                || attack.ContactDamage == 0)
                return;

            BossPartState part = _bossPartStates[partIndex];
            BossPartDefinition definition =
                _bossPartDefinitions[partIndex];
            if (!Intersects(
                    PlayerX,
                    PlayerY,
                    _playerHalfWidth,
                    _playerHalfHeight,
                    part.X,
                    part.Y,
                    definition.HalfWidth,
                    definition.HalfHeight))
                return;

            _bossPartContactHitThisCycle[partIndex] = true;
            ApplyPlayerHit(attack.ContactDamage);
        }

        static int PullAxis(
            int position,
            int target,
            int speedNumerator,
            int speedDenominator,
            ref int remainder,
            int minimum,
            int maximum)
        {
            int direction = target.CompareTo(position);
            if (direction == 0)
            {
                remainder = 0;
                return position;
            }
            long accumulated =
                remainder + (long)direction * speedNumerator;
            long delta = accumulated / speedDenominator;
            remainder = (int)(accumulated % speedDenominator);
            long candidate = position + delta;
            if ((direction > 0 && candidate >= target)
                || (direction < 0 && candidate <= target))
            {
                remainder = 0;
                candidate = target;
            }
            if (candidate < minimum)
            {
                remainder = 0;
                return minimum;
            }
            if (candidate > maximum)
            {
                remainder = 0;
                return maximum;
            }
            return (int)candidate;
        }

        void ResolvePlayerBulletBossCollisions()
        {
            if (!BossActive || BossEntering) return;

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
                int partIndex = _bossPartDefinitions.Count == 0
                    ? -1
                    : FindBossPartHit(
                        bullet.X,
                        bullet.Y,
                        bulletHalfWidth,
                        bulletHalfHeight);
                bool legacyHit = _bossPartDefinitions.Count == 0
                    && Intersects(
                        bullet.X,
                        bullet.Y,
                        bulletHalfWidth,
                        bulletHalfHeight,
                        _bossX,
                        _bossY,
                        _bossHalfWidth,
                        _bossHalfHeight);
                if (partIndex < 0 && !legacyHit)
                {
                    bulletIndex++;
                    continue;
                }

                RemoveBulletAt(bulletIndex);
                int damage = bullet.Kind == BulletKind.Missile
                    ? ComputeMissileDamage(_missileBaseDamage)
                    : Damage.Compute(_playerBulletDamage, Math.Max(1, _mainShotLevel));
                bool defeated = partIndex >= 0
                    ? ApplyDamageToBossPart(partIndex, damage)
                    : ApplyDamageToBoss(damage);
                if (!defeated
                    && bullet.Kind == BulletKind.Missile
                    && _missileFamily == MissileFamily.SpreadBomb)
                {
                    ApplyMissileExplosion(
                        bullet.Id,
                        bullet.X,
                        bullet.Y);
                    defeated = _bossDefeated;
                }
                if (defeated)
                    return;
            }
        }

        int FindBossPartHit(
            int x,
            int y,
            int halfWidth,
            int halfHeight)
        {
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartState part = _bossPartStates[i];
                if (part.Destroyed)
                    continue;
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                if (Intersects(
                        x,
                        y,
                        halfWidth,
                        halfHeight,
                        part.X,
                        part.Y,
                        definition.HalfWidth,
                        definition.HalfHeight))
                    return i;
            }
            return -1;
        }

        bool ApplyDamageToBossPart(int partIndex, int damage)
        {
            if (!BossActive || BossEntering || damage <= 0
                || partIndex < 0
                || partIndex >= _bossPartStates.Length)
                return false;
            BossPartState part = _bossPartStates[partIndex];
            if (part.Destroyed || IsBossPartInvulnerable(partIndex))
                return false;

            int hp = Damage.ApplyToHp(part.Hp, damage);
            int appliedDamage = part.Hp - hp;
            _bossHp = Damage.ApplyToHp(
                _bossHp,
                appliedDamage);
            _bossPartStates[partIndex] = new BossPartState(
                part.PartId,
                part.X,
                part.Y,
                hp,
                part.MaxHp,
                hp == 0,
                part.IsCore,
                false);
            if (_bossHp > 0)
                UpdateBossPhaseFromHp();
            if (hp > 0)
            {
                EmitEvent(
                    SimEventType.EnemyHit,
                    _bossId,
                    part.X,
                    part.Y,
                    appliedDamage);
                return false;
            }

            BossPartDefinition definition =
                _bossPartDefinitions[partIndex];
            EmitEvent(
                SimEventType.EnemyHit,
                _bossId,
                part.X,
                part.Y,
                appliedDamage);
            _bossPartRegenerationRemaining[partIndex] =
                definition.RegenerationTicks;
            _bossPartFireCooldowns[partIndex] =
                definition.Attack.IntervalTicks;
            _bossPartContactHitThisCycle[partIndex] = false;
            EmitBossPartEvent(
                SimEventType.BossPartDestroyed,
                part.X,
                part.Y,
                partIndex);
            RefreshBossPartPositions();
            if (definition.IsCore)
                return DefeatBoss(part.X, part.Y);
            return false;
        }

        bool ApplyDamageToBoss(int damage)
        {
            if (!BossActive || BossEntering || damage <= 0)
                return false;
            _bossHp = Damage.ApplyToHp(_bossHp, damage);
            if (_bossHp > 0)
            {
                EmitEvent(
                    SimEventType.EnemyHit,
                    _bossId,
                    _bossX,
                    _bossY,
                    damage);
                UpdateBossPhaseFromHp();
                return false;
            }

            return DefeatBoss(_bossX, _bossY);
        }

        void UpdateBossPhaseFromHp()
        {
            if (_bossUsesTimedPattern)
                return;
            int phaseCount = _bossPhases.Count;
            int nextPhase = Math.Min(
                phaseCount - 1,
                (int)((long)(_bossRuntimeMaxHp - _bossHp)
                    * phaseCount / _bossRuntimeMaxHp));
            if (nextPhase <= _bossPhase)
                return;
            EnterBossPhase(nextPhase, true);
        }

        bool DefeatBoss(int x, int y)
        {
            _bossDefeated = true;
            _bossHp = 0;
            int awardedScore =
                RecordKillScore((long)_bossRuntimeMaxHp * 2);
            EmitEvent(
                SimEventType.EnemyKilled,
                _bossId,
                x,
                y,
                awardedScore);
            AdvanceKillCombo();
            EmitEvent(
                SimEventType.StageCleared,
                _bossId,
                x,
                y,
                0);
            return true;
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
                    ApplyPlayerHit(_enemyBulletDamage);
                    if (!_playerAlive)
                    {
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
                int nextX = SaturateToInt(capsule.X - scrollDelta);
                int nextY = capsule.Y;
                if (_capsuleMagnetRadiusSubUnits > 0
                    && _capsuleMagnetSpeedNumerator > 0
                    && SquaredDistanceSaturated(
                        nextX,
                        nextY,
                        PlayerX,
                        PlayerY)
                        <= SquaredRadiusSaturated(
                            _capsuleMagnetRadiusSubUnits))
                {
                    long dx = (long)PlayerX - nextX;
                    long dy = (long)PlayerY - nextY;
                    long length = IntegerSqrt(dx * dx + dy * dy);
                    if (length > 0)
                    {
                        long directionX =
                            dx * CapsuleMagnetDirectionScale / length;
                        long directionY =
                            dy * CapsuleMagnetDirectionScale / length;
                        long denominator =
                            (long)_capsuleMagnetSpeedDenominator
                            * CapsuleMagnetDirectionScale;
                        long xRemainder =
                            _capsuleMagnetXRemainders[index];
                        long yRemainder =
                            _capsuleMagnetYRemainders[index];
                        nextX = AdvanceCapsuleMagnetAxis(
                            nextX,
                            PlayerX,
                            (long)_capsuleMagnetSpeedNumerator
                                * directionX,
                            denominator,
                            ref xRemainder);
                        nextY = AdvanceCapsuleMagnetAxis(
                            nextY,
                            PlayerY,
                            (long)_capsuleMagnetSpeedNumerator
                                * directionY,
                            denominator,
                            ref yRemainder);
                        _capsuleMagnetXRemainders[index] = xRemainder;
                        _capsuleMagnetYRemainders[index] = yRemainder;
                    }
                }
                else
                {
                    _capsuleMagnetXRemainders[index] = 0;
                    _capsuleMagnetYRemainders[index] = 0;
                }

                if (nextX < _enemyDespawnX)
                {
                    RemoveCapsuleAt(index);
                    continue;
                }

                _capsules[index] = new CapsuleState(
                    capsule.Id,
                    nextX,
                    nextY);
                index++;
            }
        }

        void AdvanceBombPickups()
        {
            long scrollDelta =
                GetScrollXAtTick(Tick)
                - GetScrollXAtTick(Tick - 1);
            int index = 0;
            while (index < _bombPickups.Count)
            {
                BombPickupState pickup = _bombPickups[index];
                int nextX = SaturateToInt(pickup.X - scrollDelta);
                int nextY = pickup.Y;
                if (_capsuleMagnetRadiusSubUnits > 0
                    && _capsuleMagnetSpeedNumerator > 0
                    && SquaredDistanceSaturated(
                        nextX,
                        nextY,
                        PlayerX,
                        PlayerY)
                        <= SquaredRadiusSaturated(
                            _capsuleMagnetRadiusSubUnits))
                {
                    long dx = (long)PlayerX - nextX;
                    long dy = (long)PlayerY - nextY;
                    long length = IntegerSqrt(dx * dx + dy * dy);
                    if (length > 0)
                    {
                        long directionX =
                            dx * CapsuleMagnetDirectionScale / length;
                        long directionY =
                            dy * CapsuleMagnetDirectionScale / length;
                        long denominator =
                            (long)_capsuleMagnetSpeedDenominator
                            * CapsuleMagnetDirectionScale;
                        long xRemainder =
                            _bombPickupMagnetXRemainders[index];
                        long yRemainder =
                            _bombPickupMagnetYRemainders[index];
                        nextX = AdvanceCapsuleMagnetAxis(
                            nextX,
                            PlayerX,
                            (long)_capsuleMagnetSpeedNumerator
                                * directionX,
                            denominator,
                            ref xRemainder);
                        nextY = AdvanceCapsuleMagnetAxis(
                            nextY,
                            PlayerY,
                            (long)_capsuleMagnetSpeedNumerator
                                * directionY,
                            denominator,
                            ref yRemainder);
                        _bombPickupMagnetXRemainders[index] =
                            xRemainder;
                        _bombPickupMagnetYRemainders[index] =
                            yRemainder;
                    }
                }
                else
                {
                    _bombPickupMagnetXRemainders[index] = 0;
                    _bombPickupMagnetYRemainders[index] = 0;
                }

                if (nextX < _enemyDespawnX)
                {
                    RemoveBombPickupAt(index);
                    continue;
                }
                _bombPickups[index] = new BombPickupState(
                    pickup.Id,
                    nextX,
                    nextY);
                index++;
            }
        }

        static int AdvanceCapsuleMagnetAxis(
            int position,
            int target,
            long velocityNumerator,
            long velocityDenominator,
            ref long remainder)
        {
            long accumulated = remainder + velocityNumerator;
            long delta = accumulated / velocityDenominator;
            long next = (long)position + delta;
            if ((target >= position && next >= target)
                || (target <= position && next <= target))
            {
                remainder = 0;
                return target;
            }
            remainder = accumulated % velocityDenominator;
            return SaturateToInt(next);
        }

        void AdvanceObstacles()
        {
            long scrollDelta =
                GetScrollXAtTick(Tick) - GetScrollXAtTick(Tick - 1);
            int index = 0;
            while (index < _obstacles.Count)
            {
                ObstacleState obstacle = _obstacles[index];
                long nextX = obstacle.X - scrollDelta;
                if (nextX < _enemyDespawnX)
                {
                    RemoveObstacleAt(index);
                    continue;
                }

                int age = _obstacleAges[index] + 1;
                _obstacleAges[index] = age;
                _obstacles[index] = new ObstacleState(
                    obstacle.Id,
                    obstacle.Type,
                    SaturateToInt(nextX),
                    obstacle.Y,
                    obstacle.Hp);
                LaserAttackDefinition laser =
                    _obstacleLaserAttacks[index];
                if (laser != null
                    && age % laser.CycleIntervalTicks == 0)
                {
                    TryStartLaser(
                        LaserSourceKind.Terrain,
                        obstacle.Id,
                        laser,
                        SaturateToInt(nextX),
                        obstacle.Y);
                }
                index++;
            }
        }

        void RemoveObstacleAt(int index)
        {
            _obstacles.RemoveAt(index);
            _obstacleAges.RemoveAt(index);
            _obstacleLaserAttacks.RemoveAt(index);
        }

        void TryStartLaser(
            LaserSourceKind sourceKind,
            int sourceEntityId,
            LaserAttackDefinition definition,
            int sourceX,
            int sourceY)
        {
            if (_lasers.Count >= _maxLasers)
            {
                EmitEvent(
                    SimEventType.LaserCapacityExceeded,
                    sourceEntityId,
                    sourceX,
                    sourceY,
                    _maxLasers);
                return;
            }
            if (_nextLaserId == int.MaxValue)
                throw new InvalidOperationException(
                    "The laser id counter is exhausted.");
            int id = _nextLaserId++;
            _laserDefinitions.Add(definition);
            _laserAges.Add(0);
            _lasers.Add(CreateLaserState(
                id,
                sourceKind,
                sourceEntityId,
                sourceX,
                sourceY,
                definition,
                0));
            EmitEvent(
                SimEventType.LaserTelegraphStarted,
                id,
                sourceX,
                sourceY,
                (int)sourceKind);
        }

        void AdvanceLasers()
        {
            int index = 0;
            while (index < _lasers.Count)
            {
                LaserAttackDefinition definition =
                    _laserDefinitions[index];
                LaserPhase previousPhase =
                    _lasers[index].Phase;
                int age = _laserAges[index] + 1;
                if (age >= definition.LifetimeTicks)
                {
                    int id = _lasers[index].Id;
                    int x = _lasers[index].StartX;
                    int y = _lasers[index].StartY;
                    RemoveLaserAt(index);
                    EmitEvent(
                        SimEventType.LaserEnded,
                        id,
                        x,
                        y,
                        0);
                    continue;
                }
                _laserAges[index] = age;
                LaserState current = _lasers[index];
                _lasers[index] = CreateLaserState(
                    current.Id,
                    current.SourceKind,
                    current.SourceEntityId,
                    current.StartX
                        - definition.StartOffsetX,
                    current.StartY
                        - definition.StartOffsetY,
                    definition,
                    age);
                if (previousPhase == LaserPhase.Telegraph
                    && _lasers[index].Phase == LaserPhase.Firing)
                {
                    EmitEvent(
                        SimEventType.LaserFired,
                        current.Id,
                        _lasers[index].StartX,
                        _lasers[index].StartY,
                        definition.FullHalfWidth);
                }
                index++;
            }
        }

        void RefreshLaserSegments()
        {
            int index = 0;
            while (index < _lasers.Count)
            {
                LaserState laser = _lasers[index];
                int sourceX;
                int sourceY;
                if (!TryGetLaserSourcePosition(
                        laser.SourceKind,
                        laser.SourceEntityId,
                        out sourceX,
                        out sourceY))
                {
                    RemoveLaserAt(index);
                    EmitEvent(
                        SimEventType.LaserEnded,
                        laser.Id,
                        laser.StartX,
                        laser.StartY,
                        0);
                    continue;
                }
                _lasers[index] = CreateLaserState(
                    laser.Id,
                    laser.SourceKind,
                    laser.SourceEntityId,
                    sourceX,
                    sourceY,
                    _laserDefinitions[index],
                    _laserAges[index]);
                index++;
            }
        }

        bool TryGetLaserSourcePosition(
            LaserSourceKind kind,
            int sourceEntityId,
            out int x,
            out int y)
        {
            if (kind == LaserSourceKind.Enemy)
            {
                int enemyIndex =
                    FindEnemyIndexById(sourceEntityId);
                if (enemyIndex >= 0)
                {
                    x = _enemies[enemyIndex].X;
                    y = _enemies[enemyIndex].Y;
                    return true;
                }
            }
            else
            {
                for (int i = 0; i < _obstacles.Count; i++)
                    if (_obstacles[i].Id == sourceEntityId)
                    {
                        x = _obstacles[i].X;
                        y = _obstacles[i].Y;
                        return true;
                    }
            }
            x = 0;
            y = 0;
            return false;
        }

        static LaserState CreateLaserState(
            int id,
            LaserSourceKind sourceKind,
            int sourceEntityId,
            int sourceX,
            int sourceY,
            LaserAttackDefinition definition,
            int age)
        {
            int telegraphEnd = definition.TelegraphTicks;
            int firingEnd =
                telegraphEnd + definition.FiringTicks;
            int sustainEnd =
                firingEnd + definition.SustainTicks;
            LaserPhase phase;
            LaserThicknessStage thickness;
            int phaseEnd;
            int halfWidth;
            if (age < telegraphEnd)
            {
                phase = LaserPhase.Telegraph;
                thickness = LaserThicknessStage.Telegraph;
                phaseEnd = telegraphEnd;
                halfWidth = definition.ThinHalfWidth;
            }
            else if (age < firingEnd)
            {
                phase = LaserPhase.Firing;
                thickness = LaserThicknessStage.Thin;
                phaseEnd = firingEnd;
                halfWidth = definition.ThinHalfWidth;
            }
            else if (age < sustainEnd)
            {
                phase = LaserPhase.Sustaining;
                thickness = LaserThicknessStage.Full;
                phaseEnd = sustainEnd;
                halfWidth = definition.FullHalfWidth;
            }
            else
            {
                phase = LaserPhase.Dissipating;
                thickness = LaserThicknessStage.Thin;
                phaseEnd = definition.LifetimeTicks;
                halfWidth = definition.ThinHalfWidth;
            }
            return new LaserState(
                id,
                sourceKind,
                sourceEntityId,
                SaturateToInt(
                    (long)sourceX + definition.StartOffsetX),
                SaturateToInt(
                    (long)sourceY + definition.StartOffsetY),
                SaturateToInt(
                    (long)sourceX + definition.EndOffsetX),
                SaturateToInt(
                    (long)sourceY + definition.EndOffsetY),
                phase,
                thickness,
                halfWidth,
                phaseEnd - age,
                definition.Damage);
        }

        void RemoveLaserAt(int index)
        {
            _lasers.RemoveAt(index);
            _laserDefinitions.RemoveAt(index);
            _laserAges.RemoveAt(index);
        }

        void ResolveLaserPlayerCollisions()
        {
            int playerRadius =
                Math.Max(_playerHalfWidth, _playerHalfHeight);
            for (int i = 0; i < _lasers.Count; i++)
            {
                LaserState laser = _lasers[i];
                if (!laser.IsDamaging)
                    continue;
                int radius = SaturatingAddDamage(
                    playerRadius,
                    laser.HalfWidth);
                if (LaserGeometry.IntersectsSegmentCircle(
                        laser.StartX,
                        laser.StartY,
                        laser.EndX,
                        laser.EndY,
                        PlayerX,
                        PlayerY,
                        radius))
                {
                    ApplyPlayerHit(laser.Damage);
                    if (!_playerAlive)
                        return;
                }
            }
        }

        void ResolvePlayerBulletObstacleCollisions()
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

                int obstacleIndex = FindBulletHitObstacle(in bullet);
                if (obstacleIndex < 0)
                {
                    bulletIndex++;
                    continue;
                }

                ObstacleState obstacle = _obstacles[obstacleIndex];
                if (obstacle.Type == ObstacleType.Breakable)
                {
                    int damage = bullet.Kind == BulletKind.Missile
                        ? ComputeMissileDamage(
                            _missileBaseDamage)
                        : Damage.Compute(
                            _playerBulletDamage,
                            Math.Max(1, _mainShotLevel));
                    if (bullet.Kind == BulletKind.Missile
                        && _missileFamily
                            == MissileFamily.SpreadBomb)
                    {
                        damage = SaturatingAddDamage(
                            damage,
                            ComputeMissileDamage(
                                _missileExplosionDamage));
                    }
                    int hp = Damage.ApplyToHp(obstacle.Hp, damage);
                    if (hp > 0)
                    {
                        _obstacles[obstacleIndex] = new ObstacleState(
                            obstacle.Id,
                            obstacle.Type,
                            obstacle.X,
                            obstacle.Y,
                            hp);
                    }
                    else
                    {
                        RemoveObstacleAt(obstacleIndex);
                        int awardedScore = AwardScore(_breakableObstacleScore);
                        EmitEvent(
                            SimEventType.ObstacleDestroyed,
                            obstacle.Id,
                            obstacle.X,
                            obstacle.Y,
                            awardedScore);
                    }
                }

                // Terrain blocks every player projectile, including laser pierce.
                if (bullet.Kind == BulletKind.Missile
                    && _missileFamily == MissileFamily.SpreadBomb)
                {
                    ApplyMissileExplosion(
                        bullet.Id,
                        obstacle.X,
                        obstacle.Y);
                }
                RemoveBulletAt(bulletIndex);
            }
        }

        int FindBulletHitObstacle(in BulletState bullet)
        {
            int bulletHalfWidth = bullet.Kind == BulletKind.Missile
                ? _missileHalfWidth
                : _playerBulletHalfWidth;
            int bulletHalfHeight = bullet.Kind == BulletKind.Missile
                ? _missileHalfHeight
                : _playerBulletHalfHeight;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ObstacleState obstacle = _obstacles[i];
                if (Intersects(
                        bullet.X,
                        bullet.Y,
                        bulletHalfWidth,
                        bulletHalfHeight,
                        obstacle.X,
                        obstacle.Y,
                        _obstacleHalfWidth,
                        _obstacleHalfHeight))
                    return i;
            }
            return -1;
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
                    ? ComputeMissileDamage(_missileBaseDamage)
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
                    int awardedScore = RecordKillScore(definition.ScoreValue);
                    EmitEvent(
                        SimEventType.EnemyKilled,
                        enemy.Id,
                        enemy.X,
                        enemy.Y,
                        awardedScore);
                    AdvanceKillCombo();
                    TryDropCapsule(definition, enemy.X, enemy.Y);
                    TryDropBomb(definition, enemy.X, enemy.Y);
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
                        && _bulletRicochetUsed[bulletIndex]
                            < _ricochetCount)
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
                            _bulletRicochetUsed[bulletIndex]++;
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
                else if (bullet.Kind == BulletKind.Missile)
                {
                    if (_missileFamily == MissileFamily.SpreadBomb)
                    {
                        ApplyMissileExplosion(
                            bullet.Id,
                            enemy.X,
                            enemy.Y);
                    }
                    else if (_bulletPiercesRemaining[bulletIndex] > 0)
                    {
                        _bulletPiercesRemaining[bulletIndex]--;
                        keepBullet = true;
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

            if (BossActive
                && !BossEntering
                && _bossId != excludedId)
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
            if (BossActive
                && !BossEntering
                && _bossId == targetId)
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
                int awardedScore = RecordKillScore(definition.ScoreValue);
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
        }

        void ApplyMissileExplosion(
            int sourceBulletId,
            int centerX,
            int centerY)
        {
            int damage = ComputeMissileDamage(
                _missileExplosionDamage);
            EmitEvent(
                SimEventType.MissileExploded,
                sourceBulletId,
                centerX,
                centerY,
                damage);
            if (damage == 0
                || _missileExplosionRadiusSubUnits == 0
                || _missileExplosionMaxTargets == 0)
                return;

            long radiusSquared = SquaredRadiusSaturated(
                _missileExplosionRadiusSubUnits);
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
                    && (distanceSquared
                            < _enemyScanDistances[insertIndex - 1]
                        || (distanceSquared
                                == _enemyScanDistances[insertIndex - 1]
                            && enemy.Id
                                < _enemyScanIds[insertIndex - 1])))
                {
                    insertIndex--;
                }
                if (insertIndex >= _missileExplosionMaxTargets)
                    continue;
                int nextCount = Math.Min(
                    scanCount + 1,
                    _missileExplosionMaxTargets);
                for (int shift = nextCount - 1;
                    shift > insertIndex;
                    shift--)
                {
                    _enemyScanIds[shift] =
                        _enemyScanIds[shift - 1];
                    _enemyScanDistances[shift] =
                        _enemyScanDistances[shift - 1];
                }
                _enemyScanIds[insertIndex] = enemy.Id;
                _enemyScanDistances[insertIndex] =
                    distanceSquared;
                scanCount = nextCount;
            }

            for (int scan = 0; scan < scanCount; scan++)
            {
                int enemyIndex =
                    FindEnemyIndexById(_enemyScanIds[scan]);
                if (enemyIndex < 0)
                    continue;
                EnemyState enemy = _enemies[enemyIndex];
                int hp = Damage.ApplyToHp(enemy.Hp, damage);
                if (hp > 0)
                {
                    _enemies[enemyIndex] = new EnemyState(
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
                        damage);
                    continue;
                }

                EnemyDefinition definition =
                    _enemyDefinitions[enemyIndex];
                RemoveEnemyAt(enemyIndex);
                int awardedScore =
                    RecordKillScore(definition.ScoreValue);
                EmitEvent(
                    SimEventType.EnemyKilled,
                    enemy.Id,
                    enemy.X,
                    enemy.Y,
                    awardedScore);
                AdvanceKillCombo();
                TryDropCapsule(definition, enemy.X, enemy.Y);
                TryDropBomb(definition, enemy.X, enemy.Y);
                // Deliberately no ApplyKillExplosion here: AoE final hits
                // cannot seed kill_explosion chains (REQ-034).
            }

            if (BossActive
                && !BossEntering
                && _bossPartDefinitions.Count == 0
                && SquaredDistanceSaturated(
                    centerX,
                    centerY,
                    _bossX,
                    _bossY) <= radiusSquared)
            {
                ApplyDamageToBoss(damage);
            }
            else if (BossActive && !BossEntering)
            {
                for (int i = 0;
                    i < _bossPartStates.Length
                        && !_bossDefeated;
                    i++)
                {
                    BossPartState part = _bossPartStates[i];
                    if (part.Destroyed
                        || SquaredDistanceSaturated(
                            centerX,
                            centerY,
                            part.X,
                            part.Y) > radiusSquared)
                        continue;
                    ApplyDamageToBossPart(i, damage);
                }
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

        int RecordKillScore(long baseScore)
        {
            int awardedScore = AwardScore(baseScore);
            _killScoredThisTick = true;
            _ticksSinceLastKill = 0;
            return awardedScore;
        }

        void AdvanceKillCombo()
        {
            AddComboGauge(_killComboGaugeGain);
        }

        int AwardScore(long baseScore)
        {
            long multipliedScore = MultiplySaturated(baseScore, ScoreMultiplier);
            multipliedScore = ScalePositiveRatioSaturated(
                multipliedScore,
                _encounterScoreMultiplierNumerator,
                _encounterScoreMultiplierDenominator,
                false);
            multipliedScore = ScalePositiveRatioSaturated(
                multipliedScore,
                _contractScoreMultiplierNumerator,
                _contractScoreMultiplierDenominator,
                false);
            long awardedScore = AddScoreSaturated(multipliedScore);
            return awardedScore >= int.MaxValue
                ? int.MaxValue
                : (int)awardedScore;
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

        long AddScoreSaturated(long amount)
        {
            long previousScore = Score;
            Score = Score > long.MaxValue - amount
                ? long.MaxValue
                : Score + amount;
            return Score - previousScore;
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
                RemoveEnemyAt(index);
                ApplyPlayerHit(contactDamage);
            }
        }

        void ResolveObstaclePlayerCollisions()
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ObstacleState obstacle = _obstacles[i];
                if (!Intersects(
                        PlayerX,
                        PlayerY,
                        _playerHalfWidth,
                        _playerHalfHeight,
                        obstacle.X,
                        obstacle.Y,
                        _obstacleHalfWidth,
                        _obstacleHalfHeight))
                    continue;

                ApplyPlayerHit(_obstacleContactDamage);
            }
        }

        bool ApplyPlayerHit(int incomingDamage)
        {
            if (incomingDamage <= 0
                || !_playerAlive
                || _playerInvulnerabilityTicksRemaining > 0)
                return false;

            int eventDamage;
            if (ShieldStock > 0)
            {
                ShieldStock--;
                eventDamage = 0;
                _playerInvulnerabilityTicksRemaining =
                    _playerHitInvulnerabilityTicks;
            }
            else
            {
                _playerAlive = false;
                eventDamage = incomingDamage;
            }

            EmitEvent(
                SimEventType.PlayerHit,
                0,
                PlayerX,
                PlayerY,
                eventDamage);
            if (!_playerAlive)
                EmitEvent(
                    SimEventType.PlayerKilled,
                    0,
                    PlayerX,
                    PlayerY,
                    0);
            return true;
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

                RemoveCapsuleAt(index);
                _powerUpGauge.Collect();
                EmitEvent(SimEventType.CapsulePicked, capsule.Id, capsule.X, capsule.Y, 0);
            }
        }

        void ResolveBombPickupPlayerCollisions()
        {
            int index = 0;
            while (index < _bombPickups.Count)
            {
                BombPickupState pickup = _bombPickups[index];
                if (!Intersects(
                        PlayerX, PlayerY,
                        _playerHalfWidth, _playerHalfHeight,
                        pickup.X, pickup.Y,
                        _capsuleHalfWidth, _capsuleHalfHeight))
                {
                    index++;
                    continue;
                }

                RemoveBombPickupAt(index);
                AcquireBombStock(
                    1,
                    pickup.Id,
                    pickup.X,
                    pickup.Y);
            }
        }

        void TryDropCapsule(EnemyDefinition definition, int x, int y)
        {
            int baseWeight = Math.Max(
                0,
                definition.DropWeight
                    - _capsuleDropWeightReduction);
            if (baseWeight == 0) return;
            long scaledWeight = ScalePositiveRatioSaturated(
                baseWeight,
                _capsuleDropMultiplierNumerator,
                _capsuleDropMultiplierDenominator,
                false);
            scaledWeight = ScalePositiveRatioSaturated(
                scaledWeight,
                _contractCapsuleDropMultiplierNumerator,
                _contractCapsuleDropMultiplierDenominator,
                false);
            int dropWeight = scaledWeight >= int.MaxValue - _capsuleNoDropWeight
                ? int.MaxValue - _capsuleNoDropWeight
                : (int)scaledWeight;
            int totalWeight = _capsuleNoDropWeight + dropWeight;
            if (_dropRng.NextInt(0, totalWeight) < _capsuleNoDropWeight) return;
            if (_nextCapsuleId == int.MaxValue)
                throw new InvalidOperationException("The capsule id counter is exhausted.");
            int capsuleId = _nextCapsuleId++;
            _capsules.Add(new CapsuleState(capsuleId, x, y));
            _capsuleMagnetXRemainders.Add(0);
            _capsuleMagnetYRemainders.Add(0);
            EmitEvent(SimEventType.CapsuleDropped, capsuleId, x, y, 0);
        }

        void TryDropBomb(EnemyDefinition definition, int x, int y)
        {
            if (definition.BombDropWeight == 0
                || _bombPickups.Count >= _maxBombPickups)
                return;
            long scaledWeight = ScalePositiveRatioSaturated(
                definition.BombDropWeight,
                _bombDropMultiplierNumerator,
                _bombDropMultiplierDenominator,
                false);
            int dropWeight = scaledWeight >= int.MaxValue
                - _bombNoDropWeight
                    ? int.MaxValue - _bombNoDropWeight
                    : (int)scaledWeight;
            if (_bombNoDropWeight > int.MaxValue
                - dropWeight)
                throw new InvalidOperationException(
                    "The bomb drop-table total exceeds the integer range.");
            int totalWeight =
                _bombNoDropWeight + dropWeight;
            if (!_contractGuaranteesBombDrop
                && (totalWeight == 0
                || _bombDropRng.NextInt(0, totalWeight)
                    < _bombNoDropWeight))
                return;
            if (_nextBombPickupId == int.MaxValue)
                throw new InvalidOperationException(
                    "The bomb pickup id counter is exhausted.");
            _bombPickups.Add(new BombPickupState(
                _nextBombPickupId++,
                x,
                y));
            _bombPickupMagnetXRemainders.Add(0);
            _bombPickupMagnetYRemainders.Add(0);
        }

        void RemoveCapsuleAt(int index)
        {
            _capsules.RemoveAt(index);
            _capsuleMagnetXRemainders.RemoveAt(index);
            _capsuleMagnetYRemainders.RemoveAt(index);
        }

        void RemoveBombPickupAt(int index)
        {
            _bombPickups.RemoveAt(index);
            _bombPickupMagnetXRemainders.RemoveAt(index);
            _bombPickupMagnetYRemainders.RemoveAt(index);
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
                int rotation;
                if (_mainShotAngleLutSlots.Length != 0)
                {
                    rotation = _mainShotAngleLutSlots[i];
                }
                else
                {
                    long centeredIndex =
                        2L * i - (_spreadWays - 1L);
                    rotation = (int)(
                        (centeredIndex * _spreadStepLutSlots / 2)
                        % SineLut.Length);
                }
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
                    : kind == BulletKind.Missile
                        ? _missilePierceEnemyCount
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

        static int MultiplySaturated(int value, int multiplier)
        {
            long product = (long)value * multiplier;
            return product >= int.MaxValue
                ? int.MaxValue
                : (int)product;
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
            long scaled = ScalePositiveRatioSaturated(
                baseHp,
                _enemyHpMultiplierNumerator,
                _enemyHpMultiplierDenominator,
                true);
            scaled = ScalePositiveRatioSaturated(
                scaled,
                _encounterEnemyHpMultiplierNumerator,
                _encounterEnemyHpMultiplierDenominator,
                true);
            return scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled;
        }

        static long ScalePositiveRatioSaturated(
            long value,
            int numerator,
            int denominator,
            bool roundUp)
        {
            long quotient = value / denominator;
            long remainder = value % denominator;
            long whole = MultiplySaturated(quotient, numerator);
            long fractionProduct = remainder * numerator;
            long fraction = roundUp
                ? (fractionProduct + denominator - 1) / denominator
                : fractionProduct / denominator;
            return whole > long.MaxValue - fraction
                ? long.MaxValue
                : whole + fraction;
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

        int ComputeMissileDamage(int baseDamage)
        {
            return Damage.Compute(
                baseDamage,
                Math.Max(1, _missileLevel),
                _missileDamageGrowthPercentPerLevel);
        }

        static int SaturatingAddDamage(int left, int right)
        {
            long sum = (long)left + right;
            return sum >= int.MaxValue
                ? int.MaxValue
                : (int)sum;
        }

        static ScheduledObstacle[] BuildObstacleSchedule(StagePlan stagePlan)
        {
            var schedule = new List<ScheduledObstacle>();
            long segmentStart = 0;
            for (int segmentIndex = 0;
                segmentIndex < stagePlan.Segments.Count;
                segmentIndex++)
            {
                StageSegment segment = stagePlan.Segments[segmentIndex];
                for (int obstacleIndex = 0;
                    obstacleIndex < segment.Obstacles.Count;
                    obstacleIndex++)
                {
                    if (segmentStart > int.MaxValue)
                        throw new ArgumentException(
                            "Stage obstacle timeline exceeds the tick range.",
                            nameof(stagePlan));
                    schedule.Add(new ScheduledObstacle(
                        (int)segmentStart,
                        segment.Obstacles[obstacleIndex]));
                }
                segmentStart += segment.LengthTicks;
            }
            return schedule.ToArray();
        }

        static int[] BuildSegmentStartTicks(StagePlan stagePlan)
        {
            var result = new int[stagePlan.Segments.Count];
            long startTick = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (startTick > int.MaxValue)
                    throw new ArgumentException(
                        "Stage environment timeline exceeds the tick range.",
                        nameof(stagePlan));
                result[i] = (int)startTick;
                startTick += stagePlan.Segments[i].LengthTicks;
            }
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
            if (config.PlayerWeaponFamily.HasValue
                && !Enum.IsDefined(
                    typeof(PrimaryWeaponFamily),
                    config.PlayerWeaponFamily.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerWeaponFamily));
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
            if (config.MaxEnemies < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MaxEnemies));
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
            if (config.MainShotAngleLutSlots == null)
                throw new ArgumentNullException(
                    nameof(config.MainShotAngleLutSlots));
            if (config.MainShotAngleLutSlots.Length != 0
                && config.MainShotAngleLutSlots.Length
                    != config.SpreadWays)
                throw new ArgumentException(
                    "Main-shot angle count must match SpreadWays.",
                    nameof(config.MainShotAngleLutSlots));
            for (int i = 0;
                i < config.MainShotAngleLutSlots.Length;
                i++)
            {
                int angle = config.MainShotAngleLutSlots[i];
                if (angle < -SineLut.Length / 2
                    || angle > SineLut.Length / 2)
                    throw new ArgumentOutOfRangeException(
                        nameof(config.MainShotAngleLutSlots));
            }
            if (config.MissileBaseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileBaseDamage));
            if (config.MissileDamageGrowthPercentPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MissileDamageGrowthPercentPerLevel));
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
            if (config.MissileDropDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MissileDropDelayTicks));
            if (config.OptionFollowDelayTicks < 0
                || (config.OptionFormation == OptionFormation.Trail
                    && config.OptionFollowDelayTicks < 1))
                throw new ArgumentOutOfRangeException(nameof(config.OptionFollowDelayTicks));
            if (config.StartingShieldStock < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.StartingShieldStock));
            if (config.MaxShieldStock < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxShieldStock));
            if (config.PlayerHitInvulnerabilityTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerHitInvulnerabilityTicks));
            if (config.StartingBombStock < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.StartingBombStock));
            if (config.MaxBombStock < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxBombStock));
            if (config.BombInvulnerabilityTicks < 0
                || config.BombEffectRadiusSubUnits < 0
                || config.BombRegularEnemyDamage < 0
                || config.BombBossDamageCap < 0
                || config.BombBossPartDamageCap < 0
                || config.BombNoDropWeight < 0
                || config.MaxBombPickups < 0
                || config.MaxLasers < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.BombInvulnerabilityTicks));
            if (config.PlayerHalfWidth < 0 || config.PlayerHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerHalfWidth));
            if (config.CapsuleHalfWidth < 0 || config.CapsuleHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleHalfWidth));
            if (config.CapsuleNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleNoDropWeight));
            if (config.CapsuleDropWeightReduction < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleDropWeightReduction));
            if (config.ContractBombDropMultiplierNumerator < 0
                || config.ContractBombDropMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractBombDropMultiplierNumerator));
            if (config.ContractCapsuleDropMultiplierNumerator < 0
                || config.ContractCapsuleDropMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractCapsuleDropMultiplierNumerator));
            if (config.ContractScoreMultiplierNumerator < 0
                || config.ContractScoreMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractScoreMultiplierNumerator));
            if (config.ScrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedNumerator));
            if (config.ScrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedDenominator));
            if (config.CapsuleMagnetRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetRadiusSubUnits));
            if (config.CapsuleMagnetSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetSpeedNumerator));
            if (config.CapsuleMagnetSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetSpeedDenominator));
            if (config.RareEncounterChanceNumerator < 0
                || config.RareEncounterChanceDenominator < 1
                || config.RareEncounterChanceNumerator
                    > config.RareEncounterChanceDenominator)
                throw new ArgumentOutOfRangeException(
                    nameof(config.RareEncounterChanceNumerator));
            if (config.RareRewardSelectionCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.RareRewardSelectionCount));
            if (config.MaxObstacles < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxObstacles));
            if (config.ObstacleHalfWidth < 0
                || config.ObstacleHalfHeight < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ObstacleHalfWidth));
            if (config.ObstacleContactDamage < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ObstacleContactDamage));
            if (config.BreakableObstacleScore < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.BreakableObstacleScore));
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
            if (config.MissileFamily == MissileFamily.Homing
                && config.HomingMissileTurnLutSlotsPerTick < 1)
                throw new ArgumentException(
                    "Homing missile config requires a positive turn rate.",
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

        readonly struct ScheduledObstacle
        {
            public ScheduledObstacle(int tick, ObstacleSpawn obstacle)
            {
                Tick = tick;
                Obstacle = obstacle
                    ?? throw new ArgumentNullException(nameof(obstacle));
            }

            public int Tick { get; }
            public ObstacleSpawn Obstacle { get; }
        }
    }
}
