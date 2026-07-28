using System;
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

        internal GameDataSet(
            BattleContent battleContent,
            StageGenerationCatalog stageGeneration,
            int capsuleNoDropWeight,
            int scrollSpeedNumerator,
            int scrollSpeedDenominator,
            int[] powerUpMaxLevels,
            WeaponDefinition missile)
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
        }

        public BattleContent BattleContent { get; }
        public StageGenerationCatalog StageGeneration { get; }
        public int CapsuleNoDropWeight { get; }
        public int ScrollSpeedNumerator { get; }
        public int ScrollSpeedDenominator { get; }

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
