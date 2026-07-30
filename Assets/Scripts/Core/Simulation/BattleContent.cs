using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Simulation
{
    public enum PrimaryWeaponFamily
    {
        Vulcan = 0,
        Double = 1,
        Laser = 2,
        Spread = 3
    }

    public enum MissileFamily
    {
        Straight = 0,
        SpreadBomb = 1,
        PiercingLance = 2
    }

    public enum OptionFormation
    {
        Trail = 0,
        Fixed = 1,
        Orbit = 2
    }

    /// <summary>
    /// Immutable, data-owned primary weapon profile. Display text is carried
    /// with the deterministic profile so Presentation can explain both the
    /// equipped family and reward choices without hard-coded labels.
    /// </summary>
    public sealed class PrimaryWeaponFamilyDefinition
    {
        public PrimaryWeaponFamilyDefinition(
            PrimaryWeaponFamily family,
            string displayName,
            string description,
            WeaponType weaponType,
            int baseDamage,
            int fireIntervalTicks,
            int minimumFireIntervalTicks,
            int rapidFireStartLevel,
            int fireIntervalReductionPerLevel,
            int speedNumerator,
            int speedDenominator,
            int halfWidth,
            int halfHeight,
            int pierceEnemyCount,
            int spreadWays,
            int spreadStepLutSlots)
        {
            if (!Enum.IsDefined(typeof(PrimaryWeaponFamily), family))
                throw new ArgumentOutOfRangeException(nameof(family));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "Display name cannot be null or blank.",
                    nameof(displayName));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException(
                    "Description cannot be null or blank.",
                    nameof(description));
            if (!Enum.IsDefined(typeof(WeaponType), weaponType))
                throw new ArgumentOutOfRangeException(nameof(weaponType));
            if (baseDamage < 0
                || fireIntervalTicks < 1
                || minimumFireIntervalTicks < 1
                || minimumFireIntervalTicks > fireIntervalTicks
                || rapidFireStartLevel < 0
                || fireIntervalReductionPerLevel < 0
                || speedNumerator < 0
                || speedDenominator < 1
                || halfWidth < 0
                || halfHeight < 0
                || pierceEnemyCount < 0
                || spreadWays < 1
                || spreadStepLutSlots < 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (family == PrimaryWeaponFamily.Double
                && (weaponType != WeaponType.Spread || spreadWays != 2))
                throw new ArgumentException(
                    "Double must use the two-way spread simulation profile.",
                    nameof(weaponType));
            if (family == PrimaryWeaponFamily.Laser
                && (weaponType != WeaponType.Laser
                    || pierceEnemyCount < 1))
                throw new ArgumentException(
                    "Laser must use a piercing laser simulation profile.",
                    nameof(weaponType));

            Family = family;
            DisplayName = displayName;
            Description = description;
            WeaponType = weaponType;
            BaseDamage = baseDamage;
            FireIntervalTicks = fireIntervalTicks;
            MinimumFireIntervalTicks = minimumFireIntervalTicks;
            RapidFireStartLevel = rapidFireStartLevel;
            FireIntervalReductionPerLevel =
                fireIntervalReductionPerLevel;
            SpeedNumerator = speedNumerator;
            SpeedDenominator = speedDenominator;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            PierceEnemyCount = pierceEnemyCount;
            SpreadWays = spreadWays;
            SpreadStepLutSlots = spreadStepLutSlots;
        }

        public PrimaryWeaponFamily Family { get; }
        public string Id => PrimaryWeaponFamilyIds.ToId(Family);
        public string DisplayName { get; }
        public string Description { get; }
        public WeaponType WeaponType { get; }
        public int BaseDamage { get; }
        public int FireIntervalTicks { get; }
        public int MinimumFireIntervalTicks { get; }
        public int RapidFireStartLevel { get; }
        public int FireIntervalReductionPerLevel { get; }
        public int SpeedNumerator { get; }
        public int SpeedDenominator { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int PierceEnemyCount { get; }
        public int SpreadWays { get; }
        public int SpreadStepLutSlots { get; }
    }

    public static class PrimaryWeaponFamilyIds
    {
        public static string ToId(PrimaryWeaponFamily family)
        {
            switch (family)
            {
                case PrimaryWeaponFamily.Vulcan: return "vulcan";
                case PrimaryWeaponFamily.Double: return "double";
                case PrimaryWeaponFamily.Laser: return "laser";
                case PrimaryWeaponFamily.Spread: return "spread";
                default:
                    throw new ArgumentOutOfRangeException(nameof(family));
            }
        }
    }

    public sealed class MissileFamilyDefinition
    {
        public MissileFamilyDefinition(
            MissileFamily family,
            int baseDamage,
            int fireIntervalTicks,
            int minimumFireIntervalTicks,
            int fireIntervalReductionPerLevel,
            int speedXNumerator,
            int speedXDenominator,
            int fallSpeedYNumerator,
            int fallSpeedYDenominator,
            int pierceEnemyCount,
            int explosionDamage,
            int explosionRadiusSubUnits,
            int explosionMaxTargets)
        {
            if (!Enum.IsDefined(typeof(MissileFamily), family))
                throw new ArgumentOutOfRangeException(nameof(family));
            if (baseDamage < 0
                || fireIntervalTicks < 1
                || minimumFireIntervalTicks < 1
                || fireIntervalReductionPerLevel < 0
                || speedXNumerator < 0
                || speedXDenominator < 1
                || fallSpeedYNumerator < 0
                || fallSpeedYDenominator < 1
                || pierceEnemyCount < 0
                || explosionDamage < 0
                || explosionRadiusSubUnits < 0
                || explosionMaxTargets < 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            Family = family;
            BaseDamage = baseDamage;
            FireIntervalTicks = fireIntervalTicks;
            MinimumFireIntervalTicks = minimumFireIntervalTicks;
            FireIntervalReductionPerLevel =
                fireIntervalReductionPerLevel;
            SpeedXNumerator = speedXNumerator;
            SpeedXDenominator = speedXDenominator;
            FallSpeedYNumerator = fallSpeedYNumerator;
            FallSpeedYDenominator = fallSpeedYDenominator;
            PierceEnemyCount = pierceEnemyCount;
            ExplosionDamage = explosionDamage;
            ExplosionRadiusSubUnits = explosionRadiusSubUnits;
            ExplosionMaxTargets = explosionMaxTargets;
        }

        public MissileFamily Family { get; }
        public string Id => MissileFamilyIds.ToId(Family);
        public int BaseDamage { get; }
        public int FireIntervalTicks { get; }
        public int MinimumFireIntervalTicks { get; }
        public int FireIntervalReductionPerLevel { get; }
        public int SpeedXNumerator { get; }
        public int SpeedXDenominator { get; }
        public int FallSpeedYNumerator { get; }
        public int FallSpeedYDenominator { get; }
        public int PierceEnemyCount { get; }
        public int ExplosionDamage { get; }
        public int ExplosionRadiusSubUnits { get; }
        public int ExplosionMaxTargets { get; }
    }

    public sealed class OptionFormationDefinition
    {
        readonly ReadOnlyCollection<int> _offsetXs;
        readonly ReadOnlyCollection<int> _offsetYs;

        public OptionFormationDefinition(
            OptionFormation formation,
            int followDelayTicks,
            int[] offsetXs,
            int[] offsetYs,
            int orbitRadiusSubUnits,
            int angularLutSlotsNumerator,
            int angularLutSlotsDenominator)
        {
            if (!Enum.IsDefined(typeof(OptionFormation), formation))
                throw new ArgumentOutOfRangeException(nameof(formation));
            if (followDelayTicks < 0
                || orbitRadiusSubUnits < 0
                || angularLutSlotsNumerator < 0
                || angularLutSlotsDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(followDelayTicks));
            if (offsetXs == null || offsetYs == null
                || offsetXs.Length != offsetYs.Length)
                throw new ArgumentException(
                    "Option formation offsets must have matching arrays.");
            if (formation == OptionFormation.Trail
                && followDelayTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(followDelayTicks));
            if (formation == OptionFormation.Fixed
                && offsetXs.Length == 0)
                throw new ArgumentException(
                    "Fixed formation requires offsets.",
                    nameof(offsetXs));
            if (formation == OptionFormation.Orbit
                && (orbitRadiusSubUnits < 1
                    || angularLutSlotsNumerator < 1))
                throw new ArgumentOutOfRangeException(
                    nameof(orbitRadiusSubUnits));
            Formation = formation;
            FollowDelayTicks = followDelayTicks;
            _offsetXs = Array.AsReadOnly(
                (int[])offsetXs.Clone());
            _offsetYs = Array.AsReadOnly(
                (int[])offsetYs.Clone());
            OrbitRadiusSubUnits = orbitRadiusSubUnits;
            AngularLutSlotsNumerator = angularLutSlotsNumerator;
            AngularLutSlotsDenominator = angularLutSlotsDenominator;
        }

        public OptionFormation Formation { get; }
        public string Id => OptionFormationIds.ToId(Formation);
        public int FollowDelayTicks { get; }
        public IReadOnlyList<int> OffsetXs => _offsetXs;
        public IReadOnlyList<int> OffsetYs => _offsetYs;
        public int OrbitRadiusSubUnits { get; }
        public int AngularLutSlotsNumerator { get; }
        public int AngularLutSlotsDenominator { get; }
    }

    public static class MissileFamilyIds
    {
        public static string ToId(MissileFamily family)
        {
            switch (family)
            {
                case MissileFamily.Straight: return "straight";
                case MissileFamily.SpreadBomb: return "spread_bomb";
                case MissileFamily.PiercingLance: return "piercing_lance";
                default: throw new ArgumentOutOfRangeException(nameof(family));
            }
        }
    }

    public static class OptionFormationIds
    {
        public static string ToId(OptionFormation formation)
        {
            switch (formation)
            {
                case OptionFormation.Trail: return "trail";
                case OptionFormation.Fixed: return "fixed";
                case OptionFormation.Orbit: return "orbit";
                default: throw new ArgumentOutOfRangeException(nameof(formation));
            }
        }
    }

    public enum EnemyMovePattern
    {
        Straight = 0,
        Sine = 1,
        Static = 2,
        Dive = 3,
        Zigzag = 4,
        Dash = 5
    }

    /// <summary>
    /// Immutable four-phase hostile laser profile. Segment offsets are relative
    /// to the source entity and remain in integer simulation subunits.
    /// </summary>
    public sealed class LaserAttackDefinition
    {
        public LaserAttackDefinition(
            int cycleIntervalTicks,
            int telegraphTicks,
            int firingTicks,
            int sustainTicks,
            int dissipateTicks,
            int startOffsetX,
            int startOffsetY,
            int endOffsetX,
            int endOffsetY,
            int thinHalfWidth,
            int fullHalfWidth,
            int damage)
        {
            if (cycleIntervalTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(cycleIntervalTicks));
            if (telegraphTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(telegraphTicks));
            if (firingTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(firingTicks));
            if (sustainTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(sustainTicks));
            if (dissipateTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(dissipateTicks));
            if (startOffsetX == endOffsetX && startOffsetY == endOffsetY)
                throw new ArgumentException(
                    "A laser segment must have distinct endpoints.");
            if (thinHalfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(thinHalfWidth));
            if (fullHalfWidth < thinHalfWidth)
                throw new ArgumentOutOfRangeException(
                    nameof(fullHalfWidth),
                    "Full laser width cannot be smaller than its thin width.");
            if (damage < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(damage),
                    "A hostile laser must deal positive damage.");

            long lifetime = (long)telegraphTicks
                + firingTicks
                + sustainTicks
                + dissipateTicks;
            if (lifetime > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(dissipateTicks),
                    "Laser lifetime exceeds the supported tick range.");
            if (cycleIntervalTicks < lifetime)
                throw new ArgumentOutOfRangeException(
                    nameof(cycleIntervalTicks),
                    "Laser cycles cannot overlap on the same source.");

            CycleIntervalTicks = cycleIntervalTicks;
            TelegraphTicks = telegraphTicks;
            FiringTicks = firingTicks;
            SustainTicks = sustainTicks;
            DissipateTicks = dissipateTicks;
            StartOffsetX = startOffsetX;
            StartOffsetY = startOffsetY;
            EndOffsetX = endOffsetX;
            EndOffsetY = endOffsetY;
            ThinHalfWidth = thinHalfWidth;
            FullHalfWidth = fullHalfWidth;
            Damage = damage;
        }

        public int CycleIntervalTicks { get; }
        public int TelegraphTicks { get; }
        public int FiringTicks { get; }
        public int SustainTicks { get; }
        public int DissipateTicks { get; }
        public int StartOffsetX { get; }
        public int StartOffsetY { get; }
        public int EndOffsetX { get; }
        public int EndOffsetY { get; }
        public int ThinHalfWidth { get; }
        public int FullHalfWidth { get; }
        public int Damage { get; }
        public int LifetimeTicks =>
            TelegraphTicks + FiringTicks + SustainTicks + DissipateTicks;
    }

    /// <summary>
    /// Immutable, Unity-free enemy data. Speeds are simulation subunits per tick
    /// represented as an exact fraction.
    /// </summary>
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(
            string id,
            int maxHp,
            int contactDamage,
            EnemyMovePattern movePattern,
            int moveSpeedNumerator,
            int moveSpeedDenominator,
            int halfWidth,
            int halfHeight,
            int dropWeight,
            int sineAmplitude,
            int sinePeriodTicks)
            : this(
                id, id, maxHp, contactDamage, 0, movePattern,
                moveSpeedNumerator, moveSpeedDenominator, 0,
                halfWidth, halfHeight, dropWeight,
                sineAmplitude, 1, sinePeriodTicks,
                0, 1, 0)
        {
        }

        public EnemyDefinition(
            string id,
            string displayName,
            int maxHp,
            int contactDamage,
            int scoreValue,
            EnemyMovePattern movePattern,
            int moveSpeedNumerator,
            int moveSpeedDenominator,
            int fireIntervalTicks,
            int halfWidth,
            int halfHeight,
            int dropWeight,
            int sineAmplitudeNumerator,
            int sineAmplitudeDenominator,
            int sinePeriodTicks)
            : this(
                id,
                displayName,
                maxHp,
                contactDamage,
                scoreValue,
                movePattern,
                moveSpeedNumerator,
                moveSpeedDenominator,
                fireIntervalTicks,
                halfWidth,
                halfHeight,
                dropWeight,
                sineAmplitudeNumerator,
                sineAmplitudeDenominator,
                sinePeriodTicks,
                0,
                1,
                0)
        {
        }

        public EnemyDefinition(
            string id,
            string displayName,
            int maxHp,
            int contactDamage,
            int scoreValue,
            EnemyMovePattern movePattern,
            int moveSpeedNumerator,
            int moveSpeedDenominator,
            int fireIntervalTicks,
            int halfWidth,
            int halfHeight,
            int dropWeight,
            int movementAmplitudeNumerator,
            int movementAmplitudeDenominator,
            int movementPeriodTicks,
            int movementDelayTicks,
            int movementDurationTicks,
            int movementPauseTicks)
            : this(
                id,
                displayName,
                maxHp,
                contactDamage,
                scoreValue,
                movePattern,
                moveSpeedNumerator,
                moveSpeedDenominator,
                fireIntervalTicks,
                halfWidth,
                halfHeight,
                dropWeight,
                movementAmplitudeNumerator,
                movementAmplitudeDenominator,
                movementPeriodTicks,
                movementDelayTicks,
                movementDurationTicks,
                movementPauseTicks,
                0,
                null)
        {
        }

        public EnemyDefinition(
            string id,
            string displayName,
            int maxHp,
            int contactDamage,
            int scoreValue,
            EnemyMovePattern movePattern,
            int moveSpeedNumerator,
            int moveSpeedDenominator,
            int fireIntervalTicks,
            int halfWidth,
            int halfHeight,
            int dropWeight,
            int movementAmplitudeNumerator,
            int movementAmplitudeDenominator,
            int movementPeriodTicks,
            int movementDelayTicks,
            int movementDurationTicks,
            int movementPauseTicks,
            int bombDropWeight,
            LaserAttackDefinition laserAttack)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Enemy id cannot be null or empty.", nameof(id));
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException(
                    "Enemy display name cannot be null or empty.",
                    nameof(displayName));
            if (maxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (contactDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(contactDamage));
            if (scoreValue < 0)
                throw new ArgumentOutOfRangeException(nameof(scoreValue));
            if (!Enum.IsDefined(typeof(EnemyMovePattern), movePattern))
                throw new ArgumentOutOfRangeException(nameof(movePattern));
            if (moveSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedNumerator));
            if (moveSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedDenominator));
            if (fireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (halfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(halfHeight));
            if (dropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(dropWeight));
            if (bombDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(bombDropWeight));
            if (movementAmplitudeNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(movementAmplitudeNumerator));
            if (movementAmplitudeDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(movementAmplitudeDenominator));
            if (movementPeriodTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(movementPeriodTicks));
            if (movementDelayTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(movementDelayTicks));
            if (movementDurationTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(movementDurationTicks));
            if (movementPauseTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(movementPauseTicks));
            if (movePattern == EnemyMovePattern.Dash && movementPauseTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(movementPauseTicks),
                    "Dash movement requires at least one pause tick.");

            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            ContactDamage = contactDamage;
            ScoreValue = scoreValue;
            MovePattern = movePattern;
            MoveSpeedNumerator = moveSpeedNumerator;
            MoveSpeedDenominator = moveSpeedDenominator;
            FireIntervalTicks = fireIntervalTicks;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            DropWeight = dropWeight;
            BombDropWeight = bombDropWeight;
            LaserAttack = laserAttack;
            MovementAmplitudeNumerator = movementAmplitudeNumerator;
            MovementAmplitudeDenominator = movementAmplitudeDenominator;
            MovementPeriodTicks = movementPeriodTicks;
            MovementDelayTicks = movementDelayTicks;
            MovementDurationTicks = movementDurationTicks;
            MovementPauseTicks = movementPauseTicks;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public int ContactDamage { get; }
        public int ScoreValue { get; }
        public EnemyMovePattern MovePattern { get; }
        public int MoveSpeedNumerator { get; }
        public int MoveSpeedDenominator { get; }
        public int FireIntervalTicks { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int DropWeight { get; }
        public int BombDropWeight { get; }
        public LaserAttackDefinition LaserAttack { get; }
        public int MovementAmplitudeNumerator { get; }
        public int MovementAmplitudeDenominator { get; }
        public int MovementAmplitude =>
            MovementAmplitudeNumerator / MovementAmplitudeDenominator;
        public int MovementPeriodTicks { get; }
        public int MovementDelayTicks { get; }
        public int MovementDurationTicks { get; }
        public int MovementPauseTicks { get; }

        // Legacy names remain source-compatible with Presentation and existing tests.
        public int SineAmplitudeNumerator => MovementAmplitudeNumerator;
        public int SineAmplitudeDenominator => MovementAmplitudeDenominator;
        public int SineAmplitude =>
            MovementAmplitudeNumerator / MovementAmplitudeDenominator;
        public int SinePeriodTicks => MovementPeriodTicks;
    }

    /// <summary>
    /// Immutable, Unity-free weapon data. Projectile speed is an exact fraction
    /// of simulation subunits per tick.
    /// </summary>
    public sealed class WeaponDefinition
    {
        public WeaponDefinition(
            string id,
            int baseDamage,
            int fireIntervalTicks,
            int projectileSpeedNumerator,
            int projectileSpeedDenominator,
            int projectileHalfWidth,
            int projectileHalfHeight)
            : this(
                id, PowerUpSlot.MainShot, baseDamage, fireIntervalTicks,
                projectileSpeedNumerator, projectileSpeedDenominator,
                projectileHalfWidth, projectileHalfHeight, 1,
                fireIntervalTicks / 2)
        {
        }

        public WeaponDefinition(
            string id,
            PowerUpSlot slot,
            int baseDamage,
            int fireIntervalTicks,
            int projectileSpeedNumerator,
            int projectileSpeedDenominator,
            int projectileHalfWidth,
            int projectileHalfHeight,
            int maxLevel)
            : this(
                id, slot, baseDamage, fireIntervalTicks,
                projectileSpeedNumerator, projectileSpeedDenominator,
                projectileHalfWidth, projectileHalfHeight, maxLevel,
                fireIntervalTicks / 2)
        {
        }

        public WeaponDefinition(
            string id,
            PowerUpSlot slot,
            int baseDamage,
            int fireIntervalTicks,
            int projectileSpeedNumerator,
            int projectileSpeedDenominator,
            int projectileHalfWidth,
            int projectileHalfHeight,
            int maxLevel,
            int minimumFireIntervalTicks)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Weapon id cannot be null or empty.", nameof(id));
            if (!Enum.IsDefined(typeof(PowerUpSlot), slot))
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (baseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (fireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (minimumFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumFireIntervalTicks));
            if (projectileSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileSpeedNumerator));
            if (projectileSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(projectileSpeedDenominator));
            if (projectileHalfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileHalfWidth));
            if (projectileHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileHalfHeight));
            if (maxLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(maxLevel));

            Id = id;
            Slot = slot;
            BaseDamage = baseDamage;
            FireIntervalTicks = fireIntervalTicks;
            MinimumFireIntervalTicks = minimumFireIntervalTicks;
            ProjectileSpeedNumerator = projectileSpeedNumerator;
            ProjectileSpeedDenominator = projectileSpeedDenominator;
            ProjectileHalfWidth = projectileHalfWidth;
            ProjectileHalfHeight = projectileHalfHeight;
            MaxLevel = maxLevel;
        }

        public string Id { get; }
        public PowerUpSlot Slot { get; }
        public int BaseDamage { get; }
        public int FireIntervalTicks { get; }
        public int MinimumFireIntervalTicks { get; }
        public int ProjectileSpeedNumerator { get; }
        public int ProjectileSpeedDenominator { get; }
        public int ProjectileHalfWidth { get; }
        public int ProjectileHalfHeight { get; }
        public int MaxLevel { get; }
    }

    /// <summary>
    /// Immutable battle catalog. Source order is retained; lookups never depend
    /// on Dictionary or HashSet enumeration order.
    /// </summary>
    public sealed class BattleContent
    {
        readonly ReadOnlyCollection<EnemyDefinition> _enemies;
        readonly ReadOnlyCollection<WeaponDefinition> _weapons;
        readonly ReadOnlyCollection<PrimaryWeaponFamilyDefinition>
            _primaryWeaponFamilies;
        readonly ReadOnlyCollection<MissileFamilyDefinition> _missileFamilies;
        readonly ReadOnlyCollection<OptionFormationDefinition> _optionFormations;

        public BattleContent(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<WeaponDefinition> weapons,
            string playerWeaponId)
            : this(
                enemies,
                weapons,
                playerWeaponId,
                CreateLegacyPrimaryWeaponFamilies(weapons, playerWeaponId),
                CreateLegacyMissileFamilies(weapons),
                MissileFamily.Straight,
                CreateLegacyOptionFormations(),
                OptionFormation.Trail)
        {
        }

        public BattleContent(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<WeaponDefinition> weapons,
            string playerWeaponId,
            IReadOnlyList<MissileFamilyDefinition> missileFamilies,
            MissileFamily defaultMissileFamily,
            IReadOnlyList<OptionFormationDefinition> optionFormations,
            OptionFormation defaultOptionFormation)
            : this(
                enemies,
                weapons,
                playerWeaponId,
                CreateLegacyPrimaryWeaponFamilies(
                    weapons,
                    playerWeaponId),
                missileFamilies,
                defaultMissileFamily,
                optionFormations,
                defaultOptionFormation)
        {
        }

        public BattleContent(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<WeaponDefinition> weapons,
            string playerWeaponId,
            IReadOnlyList<PrimaryWeaponFamilyDefinition>
                primaryWeaponFamilies,
            IReadOnlyList<MissileFamilyDefinition> missileFamilies,
            MissileFamily defaultMissileFamily,
            IReadOnlyList<OptionFormationDefinition> optionFormations,
            OptionFormation defaultOptionFormation)
        {
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));
            if (weapons == null) throw new ArgumentNullException(nameof(weapons));
            if (string.IsNullOrEmpty(playerWeaponId))
                throw new ArgumentException(
                    "Player weapon id cannot be null or empty.",
                    nameof(playerWeaponId));

            var enemyCopy = new EnemyDefinition[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyDefinition definition = enemies[i] ?? throw new ArgumentException(
                    "Enemy definitions cannot contain null.", nameof(enemies));
                EnsureUniqueEnemyId(enemyCopy, i, definition.Id);
                enemyCopy[i] = definition;
            }

            var weaponCopy = new WeaponDefinition[weapons.Count];
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDefinition definition = weapons[i] ?? throw new ArgumentException(
                    "Weapon definitions cannot contain null.", nameof(weapons));
                EnsureUniqueWeaponId(weaponCopy, i, definition.Id);
                weaponCopy[i] = definition;
            }

            _enemies = new ReadOnlyCollection<EnemyDefinition>(enemyCopy);
            _weapons = new ReadOnlyCollection<WeaponDefinition>(weaponCopy);
            PlayerWeapon = FindWeapon(playerWeaponId) ?? throw new ArgumentException(
                "The player weapon id is not present in the weapon definitions.",
                nameof(playerWeaponId));
            _primaryWeaponFamilies =
                CopyPrimaryWeaponFamilies(primaryWeaponFamilies);
            _missileFamilies = CopyMissileFamilies(missileFamilies);
            _optionFormations = CopyOptionFormations(optionFormations);
            DefaultMissileFamily = defaultMissileFamily;
            DefaultOptionFormation = defaultOptionFormation;
            if (FindMissileFamily(defaultMissileFamily) == null)
                throw new ArgumentException(
                    "The default missile family is not present.",
                    nameof(defaultMissileFamily));
            if (FindOptionFormation(defaultOptionFormation) == null)
                throw new ArgumentException(
                    "The default option formation is not present.",
                    nameof(defaultOptionFormation));
        }

        public IReadOnlyList<EnemyDefinition> Enemies => _enemies;
        public IReadOnlyList<WeaponDefinition> Weapons => _weapons;
        public IReadOnlyList<PrimaryWeaponFamilyDefinition>
            PrimaryWeaponFamilies => _primaryWeaponFamilies;
        public IReadOnlyList<MissileFamilyDefinition> MissileFamilies =>
            _missileFamilies;
        public IReadOnlyList<OptionFormationDefinition> OptionFormations =>
            _optionFormations;
        public WeaponDefinition PlayerWeapon { get; }
        public MissileFamily DefaultMissileFamily { get; }
        public OptionFormation DefaultOptionFormation { get; }

        public PrimaryWeaponFamilyDefinition FindPrimaryWeaponFamily(
            PrimaryWeaponFamily family)
        {
            for (int i = 0; i < _primaryWeaponFamilies.Count; i++)
                if (_primaryWeaponFamilies[i].Family == family)
                    return _primaryWeaponFamilies[i];
            return null;
        }

        public MissileFamilyDefinition FindMissileFamily(
            MissileFamily family)
        {
            for (int i = 0; i < _missileFamilies.Count; i++)
                if (_missileFamilies[i].Family == family)
                    return _missileFamilies[i];
            return null;
        }

        public OptionFormationDefinition FindOptionFormation(
            OptionFormation formation)
        {
            for (int i = 0; i < _optionFormations.Count; i++)
                if (_optionFormations[i].Formation == formation)
                    return _optionFormations[i];
            return null;
        }

        public EnemyDefinition FindEnemy(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            for (int i = 0; i < _enemies.Count; i++)
                if (string.Equals(_enemies[i].Id, id, StringComparison.Ordinal))
                    return _enemies[i];
            return null;
        }

        public WeaponDefinition FindWeapon(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            for (int i = 0; i < _weapons.Count; i++)
                if (string.Equals(_weapons[i].Id, id, StringComparison.Ordinal))
                    return _weapons[i];
            return null;
        }

        public WeaponDefinition FindWeapon(PowerUpSlot slot)
        {
            if (!Enum.IsDefined(typeof(PowerUpSlot), slot))
                throw new ArgumentOutOfRangeException(nameof(slot));
            for (int i = 0; i < _weapons.Count; i++)
                if (_weapons[i].Slot == slot)
                    return _weapons[i];
            return null;
        }

        static void EnsureUniqueEnemyId(
            EnemyDefinition[] definitions,
            int count,
            string id)
        {
            for (int i = 0; i < count; i++)
                if (string.Equals(definitions[i].Id, id, StringComparison.Ordinal))
                    throw new ArgumentException($"Duplicate enemy id '{id}'.");
        }

        static void EnsureUniqueWeaponId(
            WeaponDefinition[] definitions,
            int count,
            string id)
        {
            for (int i = 0; i < count; i++)
                if (string.Equals(definitions[i].Id, id, StringComparison.Ordinal))
                    throw new ArgumentException($"Duplicate weapon id '{id}'.");
        }

        static ReadOnlyCollection<MissileFamilyDefinition>
            CopyMissileFamilies(
                IReadOnlyList<MissileFamilyDefinition> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException(
                    "At least one missile family is required.",
                    nameof(source));
            var copy = new MissileFamilyDefinition[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Missile families cannot contain null.",
                    nameof(source));
                for (int previous = 0; previous < i; previous++)
                    if (copy[previous].Family == copy[i].Family)
                        throw new ArgumentException(
                            "Missile families cannot be duplicated.",
                            nameof(source));
            }
            return new ReadOnlyCollection<MissileFamilyDefinition>(copy);
        }

        static ReadOnlyCollection<PrimaryWeaponFamilyDefinition>
            CopyPrimaryWeaponFamilies(
                IReadOnlyList<PrimaryWeaponFamilyDefinition> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException(
                    "At least one primary weapon family is required.",
                    nameof(source));
            var copy =
                new PrimaryWeaponFamilyDefinition[source.Count];
            bool hasDouble = false;
            bool hasLaser = false;
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Primary weapon families cannot contain null.",
                    nameof(source));
                for (int previous = 0; previous < i; previous++)
                    if (copy[previous].Family == copy[i].Family)
                        throw new ArgumentException(
                            "Primary weapon families cannot be duplicated.",
                            nameof(source));
                hasDouble |= copy[i].Family
                    == PrimaryWeaponFamily.Double;
                hasLaser |= copy[i].Family
                    == PrimaryWeaponFamily.Laser;
            }
            if (!hasDouble || !hasLaser)
                throw new ArgumentException(
                    "Double and laser primary weapon families are required.",
                    nameof(source));
            return new ReadOnlyCollection<PrimaryWeaponFamilyDefinition>(
                copy);
        }

        static ReadOnlyCollection<OptionFormationDefinition>
            CopyOptionFormations(
                IReadOnlyList<OptionFormationDefinition> source)
        {
            if (source == null || source.Count == 0)
                throw new ArgumentException(
                    "At least one option formation is required.",
                    nameof(source));
            var copy = new OptionFormationDefinition[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i] ?? throw new ArgumentException(
                    "Option formations cannot contain null.",
                    nameof(source));
                for (int previous = 0; previous < i; previous++)
                    if (copy[previous].Formation == copy[i].Formation)
                        throw new ArgumentException(
                            "Option formations cannot be duplicated.",
                            nameof(source));
            }
            return new ReadOnlyCollection<OptionFormationDefinition>(copy);
        }

        static IReadOnlyList<MissileFamilyDefinition>
            CreateLegacyMissileFamilies(
                IReadOnlyList<WeaponDefinition> weapons)
        {
            WeaponDefinition missile = null;
            if (weapons != null)
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i] != null
                        && weapons[i].Slot == PowerUpSlot.Missile)
                    {
                        missile = weapons[i];
                        break;
                    }
            int u = SimSpace.SubUnitsPerWorldUnit;
            return new[]
            {
                new MissileFamilyDefinition(
                    MissileFamily.Straight,
                    missile == null ? 2 : missile.BaseDamage,
                    missile == null ? 45 : missile.FireIntervalTicks,
                    missile == null ? 30 : missile.MinimumFireIntervalTicks,
                    5,
                    missile == null
                        ? 13 * u
                        : missile.ProjectileSpeedNumerator,
                    missile == null
                        ? SimSpace.TicksPerSecond
                        : missile.ProjectileSpeedDenominator,
                    5 * u,
                    SimSpace.TicksPerSecond,
                    0,
                    0,
                    0,
                    0)
            };
        }

        static IReadOnlyList<PrimaryWeaponFamilyDefinition>
            CreateLegacyPrimaryWeaponFamilies(
                IReadOnlyList<WeaponDefinition> weapons,
                string playerWeaponId)
        {
            WeaponDefinition main = null;
            if (weapons != null && playerWeaponId != null)
                for (int i = 0; i < weapons.Count; i++)
                    if (weapons[i] != null
                        && string.Equals(
                            weapons[i].Id,
                            playerWeaponId,
                            StringComparison.Ordinal))
                    {
                        main = weapons[i];
                        break;
                    }
            int u = SimSpace.SubUnitsPerWorldUnit;
            int baseDamage = main == null ? 10 : main.BaseDamage;
            int fireInterval =
                main == null
                    ? 8
                    : Math.Max(1, main.FireIntervalTicks);
            int minimumInterval =
                main == null
                    ? 4
                    : Math.Min(
                        fireInterval,
                        Math.Max(
                            1,
                            main.MinimumFireIntervalTicks));
            int speedNumerator =
                main == null
                    ? 20 * u
                    : main.ProjectileSpeedNumerator;
            int speedDenominator =
                main == null
                    ? SimSpace.TicksPerSecond
                    : main.ProjectileSpeedDenominator;
            int halfWidth =
                main == null ? 3 * u / 8 : main.ProjectileHalfWidth;
            int halfHeight =
                main == null ? 9 * u / 64 : main.ProjectileHalfHeight;
            return new[]
            {
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Vulcan,
                    "Vulcan",
                    "Rapid straight fire.",
                    WeaponType.Vulcan,
                    baseDamage,
                    fireInterval,
                    minimumInterval,
                    2,
                    1,
                    speedNumerator,
                    speedDenominator,
                    halfWidth,
                    halfHeight,
                    0,
                    1,
                    0),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Double,
                    "Double",
                    "Two-way spread fire for wider coverage.",
                    WeaponType.Spread,
                    Math.Max(1, baseDamage * 3 / 5),
                    Math.Max(1, fireInterval + 2),
                    Math.Max(1, minimumInterval),
                    3,
                    1,
                    speedNumerator,
                    speedDenominator,
                    halfWidth,
                    halfHeight,
                    0,
                    2,
                    2),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Laser,
                    "Laser",
                    "Slower straight fire that pierces up to three enemies.",
                    WeaponType.Laser,
                    Math.Max(1, baseDamage * 3 / 2),
                    Math.Max(fireInterval + 3, fireInterval * 2),
                    Math.Max(1, fireInterval),
                    2,
                    2,
                    speedNumerator,
                    speedDenominator,
                    Math.Max(0, halfWidth / 2),
                    Math.Max(0, halfHeight / 2),
                    2,
                    1,
                    0),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Spread,
                    "Spread",
                    "Three-way coverage fire.",
                    WeaponType.Spread,
                    Math.Max(1, baseDamage * 3 / 5),
                    Math.Max(1, fireInterval + 2),
                    Math.Max(1, minimumInterval),
                    3,
                    1,
                    speedNumerator,
                    speedDenominator,
                    halfWidth,
                    halfHeight,
                    0,
                    3,
                    2)
            };
        }

        static IReadOnlyList<OptionFormationDefinition>
            CreateLegacyOptionFormations()
        {
            return new[]
            {
                new OptionFormationDefinition(
                    OptionFormation.Trail,
                    12,
                    Array.Empty<int>(),
                    Array.Empty<int>(),
                    0,
                    0,
                    1)
            };
        }
    }
}
