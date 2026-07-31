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
        readonly PowerUpCostCurve _powerUpCostCurve;
        readonly PowerUpSlotDefinition[] _powerUpGaugeSlots;
        readonly WeaponDefinition _missile;
        readonly ReadOnlyCollection<ShipDefinition> _ships;
        readonly ScoringDefinition _scoring;
        readonly int _maxEnemyBullets;

        internal GameDataSet(
            BattleContent battleContent,
            StageGenerationCatalog stageGeneration,
            int capsuleNoDropWeight,
            int bombNoDropWeight,
            int scrollSpeedNumerator,
            int scrollSpeedDenominator,
            int maxEnemyBullets,
            int[] powerUpMaxLevels,
            PowerUpCostCurve powerUpCostCurve,
            IReadOnlyList<PowerUpSlotDefinition> powerUpGaugeSlots,
            WeaponDefinition missile,
            RewardCatalog rewards,
            ContractCatalog contracts,
            IReadOnlyList<ShipDefinition> ships,
            ScoringDefinition scoring)
        {
            BattleContent = battleContent ?? throw new ArgumentNullException(nameof(battleContent));
            StageGeneration = stageGeneration ?? throw new ArgumentNullException(nameof(stageGeneration));
            if (capsuleNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(capsuleNoDropWeight));
            if (bombNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(bombNoDropWeight));
            if (scrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(scrollSpeedNumerator));
            if (scrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(scrollSpeedDenominator));
            if (maxEnemyBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(maxEnemyBullets));
            if (powerUpMaxLevels == null)
                throw new ArgumentNullException(nameof(powerUpMaxLevels));
            if (powerUpMaxLevels.Length != PowerUpGauge.SlotCount)
                throw new ArgumentException("Every power-up slot needs a max level.", nameof(powerUpMaxLevels));

            CapsuleNoDropWeight = capsuleNoDropWeight;
            BombNoDropWeight = bombNoDropWeight;
            ScrollSpeedNumerator = scrollSpeedNumerator;
            ScrollSpeedDenominator = scrollSpeedDenominator;
            _maxEnemyBullets = maxEnemyBullets;
            _powerUpMaxLevels = (int[])powerUpMaxLevels.Clone();
            _powerUpCostCurve = powerUpCostCurve
                ?? throw new ArgumentNullException(nameof(powerUpCostCurve));
            if (powerUpGaugeSlots == null)
                throw new ArgumentNullException(
                    nameof(powerUpGaugeSlots));
            _powerUpGaugeSlots =
                new PowerUpSlotDefinition[powerUpGaugeSlots.Count];
            for (int i = 0; i < _powerUpGaugeSlots.Length; i++)
                _powerUpGaugeSlots[i] =
                    powerUpGaugeSlots[i]
                    ?? throw new ArgumentException(
                        "Gauge slots cannot contain null.",
                        nameof(powerUpGaugeSlots));
            _missile = missile ?? throw new ArgumentNullException(nameof(missile));
            Rewards = rewards;
            Contracts = contracts;
            _scoring = scoring;

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
        public int BombNoDropWeight { get; }
        public int ScrollSpeedNumerator { get; }
        public int ScrollSpeedDenominator { get; }
        /// <summary>
        /// Parsed rewards.json catalog, or null when the backward-compatible
        /// three-input parser overload was used.
        /// </summary>
        public RewardCatalog Rewards { get; }
        /// <summary>
        /// Parsed waves.json sector contracts, or null for legacy data.
        /// </summary>
        public ContractCatalog Contracts { get; }
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
            return new PowerUpGauge(
                _powerUpMaxLevels[
                    (int)PowerUpSlot.MainShot],
                _powerUpGaugeSlots,
                _powerUpCostCurve);
        }

        /// <summary>
        /// Creates the ship-owned six-slot gauge when the ship opts into one;
        /// otherwise returns the backward-compatible seven-slot gauge.
        /// The designated weapon entry is an immediate one-activation switch.
        /// </summary>
        public PowerUpGauge CreatePowerUpGauge(ShipDefinition ship)
        {
            if (ship == null)
                throw new ArgumentNullException(nameof(ship));
            if (!ship.HasCustomPowerUpGauge)
                return CreatePowerUpGauge();

            var slots =
                new PowerUpSlotDefinition[ship.GaugeSlots.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                PowerUpSlot slot = ship.GaugeSlots[i];
                PowerUpSlotDefinition source = slot == PowerUpSlot.MainShot
                    ? new PowerUpSlotDefinition(
                        PowerUpSlot.MainShot,
                        "powerUp.mainShot",
                        _powerUpMaxLevels[(int)PowerUpSlot.MainShot],
                        _powerUpCostCurve)
                    : FindPowerUpGaugeSlot(slot);
                slots[i] = new PowerUpSlotDefinition(
                    source.Slot,
                    source.NameKey,
                    source.MaxLevel,
                    source.CostCurve,
                    source.SpeedBonusNumerator,
                    source.SpeedBonusDenominator,
                    source.IsWeaponMode);
            }
            return new PowerUpGauge(
                _powerUpMaxLevels[
                    (int)PowerUpSlot.MainShot],
                slots,
                _powerUpCostCurve);
        }

        public PowerUpGauge CreatePowerUpGauge(string shipId)
        {
            ShipDefinition ship = FindShip(shipId);
            if (ship == null)
                throw new ArgumentException(
                    $"Unknown ship id '{shipId}'.",
                    nameof(shipId));
            return CreatePowerUpGauge(ship);
        }

        PowerUpSlotDefinition FindPowerUpGaugeSlot(PowerUpSlot slot)
        {
            for (int i = 0; i < _powerUpGaugeSlots.Length; i++)
                if (_powerUpGaugeSlots[i].Slot == slot)
                    return _powerUpGaugeSlots[i];
            throw new InvalidOperationException(
                $"The weapons gauge does not define slot '{slot}'.");
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
            config.MainShotBaseDamage = main.BaseDamage;
            config.FireIntervalTicks = main.FireIntervalTicks;
            config.MainShotMinimumFireIntervalTicks = main.MinimumFireIntervalTicks;
            config.MainShotHalfWidth = main.ProjectileHalfWidth;
            config.MainShotHalfHeight = main.ProjectileHalfHeight;
            config.CapsuleNoDropWeight = CapsuleNoDropWeight;
            config.BombNoDropWeight = BombNoDropWeight;
            config.ScrollSpeedNumerator = ScrollSpeedNumerator;
            config.ScrollSpeedDenominator = ScrollSpeedDenominator;
            config.MaxEnemyBullets = _maxEnemyBullets;
            config.MissileBaseDamage = _missile.BaseDamage;
            config.MissileFireIntervalTicks = _missile.FireIntervalTicks;
            config.MissileMinimumFireIntervalTicks = _missile.MinimumFireIntervalTicks;
            config.MissileSpeedXNumerator = _missile.ProjectileSpeedNumerator;
            config.MissileSpeedXDenominator = _missile.ProjectileSpeedDenominator;
            config.MissileHalfWidth = _missile.ProjectileHalfWidth;
            config.MissileHalfHeight = _missile.ProjectileHalfHeight;
            ApplyMissileFamily(
                config,
                BattleContent.FindMissileFamily(
                    BattleContent.DefaultMissileFamily));
            ApplyOptionFormation(
                config,
                BattleContent.FindOptionFormation(
                    BattleContent.DefaultOptionFormation));
            if (_scoring != null)
            {
                config.GrazeExtraRadiusSubUnits = _scoring.GrazeRadiusSubUnits;
                config.GrazeScore = _scoring.GrazeScore;
                config.GrazeComboGaugeGain = _scoring.GrazeGaugeCharge;
                config.ComboGaugeRequiredForLevel2 =
                    _scoring.MultiplierGaugeRequirements[0];
                config.ComboGaugeRequiredForLevel3 =
                    _scoring.MultiplierGaugeRequirements[1];
                config.ComboGaugeRequiredForLevel4 =
                    _scoring.MultiplierGaugeRequirements[2];
                config.ComboDecayTicks = _scoring.MultiplierDecayTicks;
            }
            return config;
        }

        static void ApplyMissileFamily(
            BattleSimConfig config,
            MissileFamilyDefinition definition)
        {
            config.MissileFamily = definition.Family;
            config.MissileBaseDamage = definition.BaseDamage;
            config.MissileFireIntervalTicks =
                definition.FireIntervalTicks;
            config.MissileMinimumFireIntervalTicks =
                definition.MinimumFireIntervalTicks;
            config.MissileFireIntervalReductionPerLevel =
                definition.FireIntervalReductionPerLevel;
            config.MissileSpeedXNumerator =
                definition.SpeedXNumerator;
            config.MissileSpeedXDenominator =
                definition.SpeedXDenominator;
            config.MissileFallSpeedYNumerator =
                definition.FallSpeedYNumerator;
            config.MissileFallSpeedYDenominator =
                definition.FallSpeedYDenominator;
            config.MissilePierceEnemyCount =
                definition.PierceEnemyCount;
            config.MissileExplosionDamage =
                definition.ExplosionDamage;
            config.MissileExplosionRadiusSubUnits =
                definition.ExplosionRadiusSubUnits;
            config.MissileExplosionMaxTargets =
                definition.ExplosionMaxTargets;
        }

        static void ApplyOptionFormation(
            BattleSimConfig config,
            OptionFormationDefinition definition)
        {
            config.OptionFormation = definition.Formation;
            config.OptionFollowDelayTicks =
                definition.FollowDelayTicks;
            config.OptionFixedOffsetXs =
                Copy(definition.OffsetXs);
            config.OptionFixedOffsetYs =
                Copy(definition.OffsetYs);
            config.OptionOrbitRadiusSubUnits =
                definition.OrbitRadiusSubUnits;
            config.OptionOrbitAngularLutSlotsNumerator =
                definition.AngularLutSlotsNumerator;
            config.OptionOrbitAngularLutSlotsDenominator =
                definition.AngularLutSlotsDenominator;
        }

        static int[] Copy(IReadOnlyList<int> source)
        {
            var copy = new int[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            return copy;
        }
    }

    internal sealed class ScoringDefinition
    {
        public const int MultiplierRequirementCount = 3;

        readonly int[] _multiplierGaugeRequirements;

        public ScoringDefinition(
            int grazeRadiusSubUnits,
            int grazeScore,
            int grazeGaugeCharge,
            int[] multiplierGaugeRequirements,
            int multiplierDecayTicks)
        {
            GrazeRadiusSubUnits = grazeRadiusSubUnits;
            GrazeScore = grazeScore;
            GrazeGaugeCharge = grazeGaugeCharge;
            _multiplierGaugeRequirements =
                (int[])multiplierGaugeRequirements.Clone();
            MultiplierDecayTicks = multiplierDecayTicks;
        }

        public int GrazeRadiusSubUnits { get; }
        public int GrazeScore { get; }
        public int GrazeGaugeCharge { get; }
        public IReadOnlyList<int> MultiplierGaugeRequirements =>
            _multiplierGaugeRequirements;
        public int MultiplierDecayTicks { get; }
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
