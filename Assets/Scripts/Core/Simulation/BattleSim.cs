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
        PlayerFired = 10
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

    public enum BulletFaction { Player = 0, Enemy = 1 }
    public enum BulletKind { MainShot = 0, Missile = 1 }

    /// <summary>One tick of digital input. Movement is clamped to -1, 0, or 1 per axis.</summary>
    public readonly struct InputCommand
    {
        public InputCommand(int moveX, int moveY, bool fire)
        {
            MoveX = Clamp(moveX);
            MoveY = Clamp(moveY);
            Fire = fire;
        }

        public int MoveX { get; }
        public int MoveY { get; }
        public bool Fire { get; }
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
        public int FireIntervalTicks { get; set; }
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

        // Provisional power-up tuning. These are deliberately configurable until
        // the human balance pass replaces them with approved GameData values.
        public int MainShotRapidFireStartLevel { get; set; } = 3;
        public int MainShotFireIntervalReductionPerLevel { get; set; } = 1;
        public int MainShotMinimumFireIntervalTicks { get; set; } = 4;
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
                FireIntervalTicks = 8,
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
        long ScrollX { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        int PlayerHp { get; }
        int ShieldRemaining { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        IReadOnlyList<OptionState> Options { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        IReadOnlyList<CapsuleState> Capsules { get; }
        /// <summary>Events emitted by the most recent Step. Cleared at the start of each Step.</summary>
        ReadOnlySpan<SimEvent> EventsThisTick { get; }
        void Step(in InputCommand input);
    }

    /// <summary>Deterministic integer-only combat and generated-stage simulation.</summary>
    public sealed class BattleSim : IBattleSim
    {
        const int DropRngStream = 1;
        const int SineScale = 1024;

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
        readonly int _playerBulletDamage, _playerBulletHalfWidth, _playerBulletHalfHeight;
        readonly PowerUpGauge _powerUpGauge;
        readonly Rng _dropRng;
        readonly List<BulletState> _bullets;
        readonly List<int> _bulletXRemainders;
        readonly List<int> _bulletYRemainders;
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
        readonly ReadOnlyCollection<EnemyState> _readOnlyEnemies;
        readonly List<CapsuleState> _capsules;
        readonly ReadOnlyCollection<CapsuleState> _readOnlyCapsules;
        readonly ScheduledSpawn[] _scheduledSpawns;

        SimEvent[] _events = new SimEvent[64];
        int _eventCount;

        int _playerXRemainder, _playerYRemainder, _cooldown, _missileCooldown;
        int _mainShotLevel, _missileLevel, _optionLevel, _shieldGaugeLevel;
        int _nextBulletId = 1;
        int _nextEnemyId = 1;
        int _nextCapsuleId = 1;
        int _nextScheduledSpawn;
        int _playerHistoryHead;
        int _playerHistoryCount;

        /// <summary>Backward-compatible stage-less player movement and basic-shot simulation.</summary>
        public BattleSim(BattleSimConfig config, Rng rng)
            : this(config, rng, null, null, null, false)
        {
        }

        /// <summary>Stage-enabled simulation using immutable Core content definitions.</summary>
        public BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge)
            : this(config, rng, stagePlan, content, powerUpGauge, true)
        {
        }

        BattleSim(
            BattleSimConfig config,
            Rng rng,
            StagePlan stagePlan,
            BattleContent content,
            PowerUpGauge powerUpGauge,
            bool stageEnabled)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (stageEnabled && stagePlan == null) throw new ArgumentNullException(nameof(stagePlan));
            if (stageEnabled && content == null) throw new ArgumentNullException(nameof(content));
            if (stageEnabled && powerUpGauge == null) throw new ArgumentNullException(nameof(powerUpGauge));
            Validate(config);

            _playerSpeedNumerator = config.PlayerSpeedNumerator;
            _playerSpeedDenominator = config.PlayerSpeedDenominator;
            _maxBullets = config.MaxBullets;
            _mainShotRapidFireStartLevel = config.MainShotRapidFireStartLevel;
            _mainShotFireIntervalReductionPerLevel =
                config.MainShotFireIntervalReductionPerLevel;
            _mainShotMinimumFireIntervalTicks = config.MainShotMinimumFireIntervalTicks;
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
            _powerUpGauge = powerUpGauge;
            _dropRng = rng.Fork(DropRngStream);

            if (stageEnabled)
            {
                WeaponDefinition weapon = content.PlayerWeapon;
                _bulletSpeedNumerator = weapon.ProjectileSpeedNumerator;
                _bulletSpeedDenominator = weapon.ProjectileSpeedDenominator;
                _fireIntervalTicks = weapon.FireIntervalTicks;
                _playerBulletDamage = weapon.BaseDamage;
                _playerBulletHalfWidth = weapon.ProjectileHalfWidth;
                _playerBulletHalfHeight = weapon.ProjectileHalfHeight;
                ValidateDropTotals(content, _capsuleNoDropWeight);
                _scheduledSpawns = BuildSchedule(stagePlan, content);
            }
            else
            {
                _bulletSpeedNumerator = config.PlayerBulletSpeedNumerator;
                _bulletSpeedDenominator = config.PlayerBulletSpeedDenominator;
                _fireIntervalTicks = config.FireIntervalTicks;
                _playerBulletDamage = 0;
                _playerBulletHalfWidth = 0;
                _playerBulletHalfHeight = 0;
                _scheduledSpawns = Array.Empty<ScheduledSpawn>();
            }

            _bullets = new List<BulletState>(_maxBullets);
            _bulletXRemainders = new List<int>(_maxBullets);
            _bulletYRemainders = new List<int>(_maxBullets);
            _readOnlyBullets = _bullets.AsReadOnly();
            _options = new List<OptionState>();
            _readOnlyOptions = _options.AsReadOnly();
            int maxOptionLevel = powerUpGauge == null
                ? 0
                : powerUpGauge.GetMaxLevel(PowerUpSlot.Option);
            long historyCapacity = (long)maxOptionLevel * _optionFollowDelayTicks + 1;
            if (historyCapacity > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.OptionFollowDelayTicks),
                    "Option history capacity exceeds the supported range.");
            _playerHistoryX = new int[(int)historyCapacity];
            _playerHistoryY = new int[(int)historyCapacity];
            _enemies = new List<EnemyState>();
            _enemyDefinitions = new List<EnemyDefinition>();
            _enemyXRemainders = new List<int>();
            _enemySpawnYs = new List<int>();
            _enemyAges = new List<int>();
            _readOnlyEnemies = _enemies.AsReadOnly();
            _capsules = new List<CapsuleState>();
            _readOnlyCapsules = _capsules.AsReadOnly();

            PlayerX = config.PlayerSpawnX;
            PlayerY = config.PlayerSpawnY;
            PlayerHp = config.PlayerMaxHp;
            RecordPlayerPosition();
            ReadPowerUpLevels();
            UpdateOptionPositions();
            SpawnScheduledThroughTick(0);
        }

        public int Tick { get; private set; }
        public long ScrollX => GetScrollXAtTick(Tick);
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public int PlayerHp { get; private set; }
        public int ShieldRemaining { get; private set; }
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;
        public IReadOnlyList<OptionState> Options => _readOnlyOptions;
        public IReadOnlyList<EnemyState> Enemies => _readOnlyEnemies;
        public IReadOnlyList<CapsuleState> Capsules => _readOnlyCapsules;
        public ReadOnlySpan<SimEvent> EventsThisTick => new ReadOnlySpan<SimEvent>(_events, 0, _eventCount);

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

            PlayerX = AdvancePlayerAxis(PlayerX, input.MoveX, ref _playerXRemainder, _playerMinX, _playerMaxX);
            PlayerY = AdvancePlayerAxis(PlayerY, input.MoveY, ref _playerYRemainder, _playerMinY, _playerMaxY);
            RecordPlayerPosition();
            ReadPowerUpLevels();
            UpdateOptionPositions();
            AdvanceBullets();
            AdvanceEnemies();
            SpawnScheduledThroughTick(Tick);
            ResolvePlayerBulletEnemyCollisions();
            ResolveEnemyPlayerCollisions();
            ResolveCapsulePlayerCollisions();

            if (_cooldown > 0) _cooldown--;
            if (_missileCooldown > 0) _missileCooldown--;
            if (input.Fire)
            {
                if (_cooldown == 0 && _bullets.Count < _maxBullets)
                    SpawnMainShotVolley();
                if (_missileLevel > 0
                    && _missileCooldown == 0
                    && _bullets.Count < _maxBullets)
                    SpawnMissile();
            }
        }

        void EmitEvent(SimEventType type, int entityId, int x, int y, int arg)
        {
            if (_eventCount == _events.Length)
                Array.Resize(ref _events, _events.Length * 2);
            _events[_eventCount++] = new SimEvent(type, entityId, x, y, arg);
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
            int write = 0;
            for (int read = 0; read < _bullets.Count; read++)
            {
                BulletState bullet = _bullets[read];
                bool isMissile = bullet.Kind == BulletKind.Missile;
                int xNumerator = isMissile ? _missileSpeedXNumerator : _bulletSpeedNumerator;
                int xDenominator = isMissile ? _missileSpeedXDenominator : _bulletSpeedDenominator;
                int yNumerator = isMissile ? -_missileFallSpeedYNumerator : 0;
                int yDenominator = isMissile ? _missileFallSpeedYDenominator : 1;
                long accumulatedX = _bulletXRemainders[read] + (long)xNumerator;
                long accumulatedY = _bulletYRemainders[read] + (long)yNumerator;
                int deltaX = (int)(accumulatedX / xDenominator);
                int deltaY = (int)(accumulatedY / yDenominator);
                int nextXRemainder = (int)(accumulatedX % xDenominator);
                int nextYRemainder = (int)(accumulatedY % yDenominator);
                long nextX = bullet.X + (long)deltaX;
                if (nextX > _bulletDespawnX) continue;
                int nextY = SaturateToInt(bullet.Y + (long)deltaY);
                _bullets[write] = new BulletState(
                    bullet.Id, bullet.Faction, bullet.Kind, (int)nextX, nextY);
                _bulletXRemainders[write] = nextXRemainder;
                _bulletYRemainders[write] = nextYRemainder;
                write++;
            }

            int removed = _bullets.Count - write;
            if (removed > 0)
            {
                _bullets.RemoveRange(write, removed);
                _bulletXRemainders.RemoveRange(write, removed);
                _bulletYRemainders.RemoveRange(write, removed);
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

                if (definition.MovePattern != EnemyMovePattern.Static)
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
                        / definition.SinePeriodTicks) % SineLut.Length);
                    long offset = (long)definition.SineAmplitudeNumerator
                        * SineLut[phase]
                        / ((long)definition.SineAmplitudeDenominator * SineScale);
                    y = SaturateToInt(_enemySpawnYs[index] + offset);
                }

                _enemyAges[index] = age;
                _enemies[index] = new EnemyState(state.Id, state.DefinitionId, x, y, state.Hp);
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
                    _nextEnemyId++, spawn.Definition.Id, spawn.X, spawn.Y, spawn.Definition.MaxHp));
                _enemyDefinitions.Add(spawn.Definition);
                _enemyXRemainders.Add(0);
                _enemySpawnYs.Add(spawn.Y);
                _enemyAges.Add(0);
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

                int enemyIndex = FindBulletHitEnemy(bullet);
                if (enemyIndex < 0)
                {
                    bulletIndex++;
                    continue;
                }

                RemoveBulletAt(bulletIndex);
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
                    continue;
                }

                EnemyDefinition definition = _enemyDefinitions[enemyIndex];
                RemoveEnemyAt(enemyIndex);
                EmitEvent(SimEventType.EnemyKilled, enemy.Id, enemy.X, enemy.Y, damage);
                TryDropCapsule(definition, enemy.X, enemy.Y);
            }
        }

        int FindBulletHitEnemy(BulletState bullet)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState enemy = _enemies[i];
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
            SpawnBullet(BulletKind.MainShot, PlayerX, PlayerY);
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.MainShot);
            for (int i = 0; i < _options.Count && _bullets.Count < _maxBullets; i++)
                SpawnBullet(BulletKind.MainShot, _options[i].X, _options[i].Y);
            _cooldown = ComputeReducedInterval(
                _fireIntervalTicks,
                _mainShotLevel,
                _mainShotRapidFireStartLevel,
                _mainShotFireIntervalReductionPerLevel,
                _mainShotMinimumFireIntervalTicks);
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

        void RemoveBulletAt(int index)
        {
            _bullets.RemoveAt(index);
            _bulletXRemainders.RemoveAt(index);
            _bulletYRemainders.RemoveAt(index);
        }

        void RemoveEnemyAt(int index)
        {
            _enemies.RemoveAt(index);
            _enemyDefinitions.RemoveAt(index);
            _enemyXRemainders.RemoveAt(index);
            _enemySpawnYs.RemoveAt(index);
            _enemyAges.RemoveAt(index);
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

        static int SaturateToInt(long value)
        {
            if (value < int.MinValue) return int.MinValue;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }

        static ScheduledSpawn[] BuildSchedule(StagePlan stagePlan, BattleContent content)
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
            if (config.MaxBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxBullets));
            if (config.MainShotRapidFireStartLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotRapidFireStartLevel));
            if (config.MainShotFireIntervalReductionPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MainShotFireIntervalReductionPerLevel));
            if (config.MainShotMinimumFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotMinimumFireIntervalTicks));
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
            if (config.PlayerMinX > config.PlayerMaxX || config.PlayerMinY > config.PlayerMaxY)
                throw new ArgumentException("Player bounds are reversed.", nameof(config));
            if (config.PlayerSpawnX < config.PlayerMinX || config.PlayerSpawnX > config.PlayerMaxX)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnX));
            if (config.PlayerSpawnY < config.PlayerMinY || config.PlayerSpawnY > config.PlayerMaxY)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnY));
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
