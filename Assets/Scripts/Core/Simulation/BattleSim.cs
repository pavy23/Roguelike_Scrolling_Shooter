using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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

        public int PlayerSpeedNumerator
        {
            get => _playerSpeedNumerator;
            set => _playerSpeedNumerator = value;
        }

        public int PlayerSpeedDenominator
        {
            get => _playerSpeedDenominator;
            set => _playerSpeedDenominator = value;
        }

        /// <summary>Whole subunits/tick shorthand. Setting it resets the denominator to 1.</summary>
        public int PlayerBulletSpeedPerTick
        {
            get => _bulletSpeedNumerator / _bulletSpeedDenominator;
            set { _bulletSpeedNumerator = value; _bulletSpeedDenominator = 1; }
        }

        public int PlayerBulletSpeedNumerator
        {
            get => _bulletSpeedNumerator;
            set => _bulletSpeedNumerator = value;
        }

        public int PlayerBulletSpeedDenominator
        {
            get => _bulletSpeedDenominator;
            set => _bulletSpeedDenominator = value;
        }

        public int FireIntervalTicks { get; set; }
        public int MaxBullets { get; set; }
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }

        /// <summary>Defaults from player.json, the main shot, and the 24 by 14 unit view.</summary>
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
                PlayerSpawnY = 0
            };
        }
    }

    public interface IBattleSim
    {
        int Tick { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        void Step(in InputCommand input);
    }

    /// <summary>Deterministic tick simulation for movement and the basic shot.</summary>
    public sealed class BattleSim : IBattleSim
    {
        const int BattleSimulationStream = 2;

        readonly int _playerSpeedNumerator, _playerSpeedDenominator;
        readonly int _bulletSpeedNumerator, _bulletSpeedDenominator;
        readonly int _fireIntervalTicks, _maxBullets;
        readonly int _playerMinX, _playerMaxX, _playerMinY, _playerMaxY;
        readonly int _bulletDespawnX;
        readonly List<BulletState> _bullets;
        readonly List<int> _bulletRemainders;
        readonly ReadOnlyCollection<BulletState> _readOnlyBullets;

#pragma warning disable CS0414
        // Reserved for future battle-specific streams. Current logic consumes no randomness.
        readonly Rng _rng;
#pragma warning restore CS0414

        int _playerXRemainder, _playerYRemainder, _cooldown;
        int _nextBulletId = 1;

        public BattleSim(BattleSimConfig config, Rng rng)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            _rng = rng.Fork(BattleSimulationStream);
            Validate(config);

            // Snapshot mutable config so later edits cannot alter this run.
            _playerSpeedNumerator = config.PlayerSpeedNumerator;
            _playerSpeedDenominator = config.PlayerSpeedDenominator;
            _bulletSpeedNumerator = config.PlayerBulletSpeedNumerator;
            _bulletSpeedDenominator = config.PlayerBulletSpeedDenominator;
            _fireIntervalTicks = config.FireIntervalTicks;
            _maxBullets = config.MaxBullets;
            _playerMinX = config.PlayerMinX;
            _playerMaxX = config.PlayerMaxX;
            _playerMinY = config.PlayerMinY;
            _playerMaxY = config.PlayerMaxY;
            _bulletDespawnX = config.BulletDespawnX;

            _bullets = new List<BulletState>(_maxBullets);
            _bulletRemainders = new List<int>(_maxBullets);
            _readOnlyBullets = _bullets.AsReadOnly();
            PlayerX = config.PlayerSpawnX;
            PlayerY = config.PlayerSpawnY;
        }

        public int Tick { get; private set; }
        public int PlayerX { get; private set; }
        public int PlayerY { get; private set; }
        public IReadOnlyList<BulletState> Bullets => _readOnlyBullets;

        public void Step(in InputCommand input)
        {
            if (Tick == int.MaxValue)
                throw new InvalidOperationException("The simulation tick counter is exhausted.");
            Tick++;

            PlayerX = AdvanceAxis(
                PlayerX, input.MoveX, ref _playerXRemainder, _playerMinX, _playerMaxX);
            PlayerY = AdvanceAxis(
                PlayerY, input.MoveY, ref _playerYRemainder, _playerMinY, _playerMaxY);
            AdvanceBullets();

            if (_cooldown > 0) _cooldown--;
            if (input.Fire && _cooldown == 0 && _bullets.Count < _maxBullets)
                SpawnPlayerBullet();
        }

        int AdvanceAxis(int position, int direction, ref int remainder, int min, int max)
        {
            if (direction == 0) return position;

            long accumulated = remainder + (long)direction * _playerSpeedNumerator;
            long candidate = position + accumulated / _playerSpeedDenominator;
            int nextRemainder = (int)(accumulated % _playerSpeedDenominator);

            if (direction < 0 && candidate <= min)
            {
                remainder = 0;
                return min;
            }
            if (direction > 0 && candidate >= max)
            {
                remainder = 0;
                return max;
            }

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

                _bullets[write] = new BulletState(
                    bullet.Id, bullet.Faction, (int)nextX, bullet.Y);
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

        void SpawnPlayerBullet()
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException("The bullet id counter is exhausted.");

            _bullets.Add(new BulletState(
                _nextBulletId++, BulletFaction.Player, PlayerX, PlayerY));
            _bulletRemainders.Add(0);
            _cooldown = _fireIntervalTicks;
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
            if (config.PlayerMinX > config.PlayerMaxX || config.PlayerMinY > config.PlayerMaxY)
                throw new ArgumentException("Player bounds are reversed.", nameof(config));
            if (config.PlayerSpawnX < config.PlayerMinX || config.PlayerSpawnX > config.PlayerMaxX)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnX));
            if (config.PlayerSpawnY < config.PlayerMinY || config.PlayerSpawnY > config.PlayerMaxY)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnY));
        }
    }
}
