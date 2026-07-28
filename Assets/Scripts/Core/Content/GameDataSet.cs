using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Content
{
    /// <summary>
    /// Immutable result of parsing the approved GameData schema. Mutable runtime
    /// objects are created on demand so callers cannot change the parsed source.
    /// </summary>
    public sealed class GameDataSet
    {
        readonly int[] _powerUpMaxLevels;
        readonly WeaponDefinition _missile;
        readonly ReadOnlyCollection<ShipDefinition> _ships;

        internal GameDataSet(
            BattleContent battleContent,
            StageGenerationCatalog stageGeneration,
            int capsuleNoDropWeight,
            int scrollSpeedNumerator,
            int scrollSpeedDenominator,
            int[] powerUpMaxLevels,
            WeaponDefinition missile,
            RewardCatalog rewards,
            IReadOnlyList<ShipDefinition> ships)
        {
            BattleContent = battleContent ?? throw new ArgumentNullException(nameof(battleContent));
            StageGeneration = stageGeneration ?? throw new ArgumentNullException(nameof(stageGeneration));
            if (capsuleNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(capsuleNoDropWeight));
            if (scrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(scrollSpeedNumerator));
            if (scrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(scrollSpeedDenominator));
            if (powerUpMaxLevels == null)
                throw new ArgumentNullException(nameof(powerUpMaxLevels));
            if (powerUpMaxLevels.Length != PowerUpGauge.SlotCount)
                throw new ArgumentException("Every power-up slot needs a max level.", nameof(powerUpMaxLevels));

            CapsuleNoDropWeight = capsuleNoDropWeight;
            ScrollSpeedNumerator = scrollSpeedNumerator;
            ScrollSpeedDenominator = scrollSpeedDenominator;
            _powerUpMaxLevels = (int[])powerUpMaxLevels.Clone();
            _missile = missile ?? throw new ArgumentNullException(nameof(missile));
            Rewards = rewards;

            if (ships == null) throw new ArgumentNullException(nameof(ships));
            if (ships.Count == 0)
                throw new ArgumentException(
                    "At least one ship definition is required.",
                    nameof(ships));
            var shipCopy = new ShipDefinition[ships.Count];
            ShipDefinition defaultShip = null;
            for (int i = 0; i < shipCopy.Length; i++)
            {
                ShipDefinition ship = ships[i] ?? throw new ArgumentException(
                    "Ship definitions cannot contain null.",
                    nameof(ships));
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(
                            shipCopy[previous].Id,
                            ship.Id,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate ship id '{ship.Id}'.",
                            nameof(ships));
                }

                shipCopy[i] = ship;
                if (defaultShip == null && ship.UnlockCost == 0)
                    defaultShip = ship;
            }
            DefaultShip = defaultShip ?? throw new ArgumentException(
                "At least one zero-cost ship is required.",
                nameof(ships));
            _ships = Array.AsReadOnly(shipCopy);
        }

        public BattleContent BattleContent { get; }
        public StageGenerationCatalog StageGeneration { get; }
        public int CapsuleNoDropWeight { get; }
        public int ScrollSpeedNumerator { get; }
        public int ScrollSpeedDenominator { get; }
        /// <summary>
        /// Parsed rewards.json catalog, or null when the backward-compatible
        /// three-input parser overload was used.
        /// </summary>
        public RewardCatalog Rewards { get; }
        public IReadOnlyList<ShipDefinition> Ships => _ships;
        public ShipDefinition DefaultShip { get; }

        public ShipDefinition FindShip(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            for (int i = 0; i < _ships.Count; i++)
            {
                if (string.Equals(_ships[i].Id, id, StringComparison.Ordinal))
                    return _ships[i];
            }
            return null;
        }

        public MetaState CreateMetaState()
        {
            return MetaState.CreateDefault(DefaultShip);
        }

        public PowerUpGauge CreatePowerUpGauge()
        {
            return new PowerUpGauge((int[])_powerUpMaxLevels.Clone());
        }

        /// <summary>
        /// Applies only schema-owned values. View bounds and provisional values
        /// without GameData fields retain BattleSimConfig defaults.
        /// </summary>
        public BattleSimConfig CreateBattleSimConfig()
        {
            WeaponDefinition main = BattleContent.PlayerWeapon;
            var config = BattleSimConfig.CreateDefault();
            config.PlayerBulletSpeedNumerator = main.ProjectileSpeedNumerator;
            config.PlayerBulletSpeedDenominator = main.ProjectileSpeedDenominator;
            config.FireIntervalTicks = main.FireIntervalTicks;
            config.CapsuleNoDropWeight = CapsuleNoDropWeight;
            config.ScrollSpeedNumerator = ScrollSpeedNumerator;
            config.ScrollSpeedDenominator = ScrollSpeedDenominator;
            config.MissileBaseDamage = _missile.BaseDamage;
            config.MissileFireIntervalTicks = _missile.FireIntervalTicks;
            config.MissileSpeedXNumerator = _missile.ProjectileSpeedNumerator;
            config.MissileSpeedXDenominator = _missile.ProjectileSpeedDenominator;
            config.MissileHalfWidth = _missile.ProjectileHalfWidth;
            config.MissileHalfHeight = _missile.ProjectileHalfHeight;
            return config;
        }
    }

    public sealed class GameDataParseException : FormatException
    {
        public GameDataParseException(string message)
            : base(message)
        {
        }

        public GameDataParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
