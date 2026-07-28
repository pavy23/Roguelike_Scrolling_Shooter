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
    }

    public enum BulletFaction { Player = 0, Enemy = 1 }

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
        {
            Id = id;
            Faction = faction;
            X = x;
            Y = y;
        }

        public int Id { get; }
        public BulletFaction Faction { get; }
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

        /// <summary>Defaults sourced from player.json, main_shot, and the 24 by 14 unit view.</summary>
        public static BattleSimConfig CreateDefault()
        {
            const int u = SimSpace.SubUnitsPerWorldUnit;
            return new BattleSimConfig
            {
                PlayerSpeedNumerator = 8 * u,
                PlayerSpeedDenominator = SimSpace.TicksPerSecond,
                PlayerBulletSpeedNumerator = 12 * u,
                PlayerBulletSpeedDenominator = SimSpace.TicksPerSecond,
                FireIntervalTicks = 8,
                MaxBullets = 64,
                PlayerMinX = -23 * u / 2,
                PlayerMaxX = 23 * u / 2,
                PlayerMinY = -13 * u / 2,
                PlayerMaxY = 13 * u / 2,
                BulletDespawnX = 13 * u,
                PlayerSpawnX = -8 * u,
                PlayerSpawnY = 0,
                PlayerMaxHp = 1,
                PlayerHalfWidth = u / 4,
                PlayerHalfHeight = u / 4
            };
        }
    }

    public interface IBattleSim
    {
        int Tick { get; }
        long ScrollX { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        int PlayerHp { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        IReadOnlyList<CapsuleState> Capsules { get; }
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
        readonly List<int> _bulletRemainders;
        readonly ReadOnlyCollection<BulletState> _readOnlyBullets;
        readonly List<EnemyState> _enemies;
        readonly List<EnemyDefinition> _enemyDefinitions;
        readonly List<int> _enemyXRemainders;
        readonly List<int> _enemySpawnYs;
        readonly List<int> _enemyAges;
        readonly ReadOnlyCollection<EnemyState> _readOnlyEnemies;
        readonly List<CapsuleState> _capsules;
        readonly ReadOnlyCollection<CapsuleState> _readOnlyCapsules;
        readonly ScheduledSpawn[] _scheduledSpawns;

        int _playerXRemainder, _playerYRemainder, _cooldown;
        int _nextBulletId = 1;
        int _nextEnemyId = 1;
        int _nextCapsuleId = 1;
        int _nextScheduledSpawn;

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
            _bulletRemainders = new List<int>(_maxBullets);
            _readOnlyBullets = _bullets.AsReadOnly();
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
            SpawnScheduledThroughTick(0);
        }

        public int Tick { get; private set; }
        public long ScrollX => GetScrollXAtTick(Tick);
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public int PlayerHp { get; private set; }
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;
        public IReadOnlyList<EnemyState> Enemies => _readOnlyEnemies;
        public IReadOnlyList<CapsuleState> Capsules => _readOnlyCapsules;

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

            PlayerX = AdvancePlayerAxis(PlayerX, input.MoveX, ref _playerXRemainder, _playerMinX, _playerMaxX);
            PlayerY = AdvancePlayerAxis(PlayerY, input.MoveY, ref _playerYRemainder, _playerMinY, _playerMaxY);
            AdvanceBullets();
            AdvanceEnemies();
            SpawnScheduledThroughTick(Tick);
            ResolvePlayerBulletEnemyCollisions();
            ResolveEnemyPlayerCollisions();
            ResolveCapsulePlayerCollisions();

            if (_cooldown > 0) _cooldown--;
            if (input.Fire && _cooldown == 0 && _bullets.Count < _maxBullets)
                SpawnPlayerBullet();
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
                long accumulated = _bulletRemainders[read] + (long)_bulletSpeedNumerator;
                int delta = (int)(accumulated / _bulletSpeedDenominator);
                int nextRemainder = (int)(accumulated % _bulletSpeedDenominator);
                BulletState bullet = _bullets[read];
                long nextX = bullet.X + (long)delta;
                if (nextX > _bulletDespawnX) continue;
                _bullets[write] = new BulletState(bullet.Id, bullet.Faction, (int)nextX, bullet.Y);
                _bulletRemainders[write] = nextRemainder;
                write++;
            }

            int removed = _bullets.Count - write;
            if (removed > 0)
            {
                _bullets.RemoveRange(write, removed);
                _bulletRemainders.RemoveRange(write, removed);
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
                    long offset = (long)definition.SineAmplitude * SineLut[phase] / SineScale;
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
                int weaponLevel = _powerUpGauge == null
                    ? 1
                    : Math.Max(1, _powerUpGauge.GetLevel(PowerUpSlot.MainShot));
                int damage = Damage.Compute(_playerBulletDamage, weaponLevel);
                int hp = Damage.ApplyToHp(enemy.Hp, damage);
                if (hp > 0)
                {
                    _enemies[enemyIndex] = new EnemyState(
                        enemy.Id, enemy.DefinitionId, enemy.X, enemy.Y, hp);
                    continue;
                }

                EnemyDefinition definition = _enemyDefinitions[enemyIndex];
                RemoveEnemyAt(enemyIndex);
                TryDropCapsule(definition, enemy.X, enemy.Y);
            }
        }

        int FindBulletHitEnemy(BulletState bullet)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                EnemyState enemy = _enemies[i];
                EnemyDefinition definition = _enemyDefinitions[i];
                if (Intersects(
                        bullet.X, bullet.Y, _playerBulletHalfWidth, _playerBulletHalfHeight,
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

                PlayerHp = Damage.ApplyToHp(PlayerHp, definition.ContactDamage);
                RemoveEnemyAt(index);
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
            }
        }

        void TryDropCapsule(EnemyDefinition definition, int x, int y)
        {
            if (definition.DropWeight == 0) return;
            int totalWeight = _capsuleNoDropWeight + definition.DropWeight;
            if (_dropRng.NextInt(0, totalWeight) < _capsuleNoDropWeight) return;
            if (_nextCapsuleId == int.MaxValue)
                throw new InvalidOperationException("The capsule id counter is exhausted.");
            _capsules.Add(new CapsuleState(_nextCapsuleId++, x, y));
        }

        void SpawnPlayerBullet()
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException("The bullet id counter is exhausted.");
            _bullets.Add(new BulletState(_nextBulletId++, BulletFaction.Player, PlayerX, PlayerY));
            _bulletRemainders.Add(0);
            _cooldown = _fireIntervalTicks;
        }

        void RemoveBulletAt(int index)
        {
            _bullets.RemoveAt(index);
            _bulletRemainders.RemoveAt(index);
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
