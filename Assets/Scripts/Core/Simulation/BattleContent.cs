using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Simulation
{
    public enum EnemyMovePattern
    {
        Straight = 0,
        Sine = 1,
        Static = 2
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
                sineAmplitude, 1, sinePeriodTicks)
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
            if (sineAmplitudeNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(sineAmplitudeNumerator));
            if (sineAmplitudeDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(sineAmplitudeDenominator));
            if (sinePeriodTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(sinePeriodTicks));

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
            SineAmplitudeNumerator = sineAmplitudeNumerator;
            SineAmplitudeDenominator = sineAmplitudeDenominator;
            SinePeriodTicks = sinePeriodTicks;
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
        public int SineAmplitudeNumerator { get; }
        public int SineAmplitudeDenominator { get; }
        public int SineAmplitude =>
            SineAmplitudeNumerator / SineAmplitudeDenominator;
        public int SinePeriodTicks { get; }
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

        public BattleContent(
            IReadOnlyList<EnemyDefinition> enemies,
            IReadOnlyList<WeaponDefinition> weapons,
            string playerWeaponId)
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
        }

        public IReadOnlyList<EnemyDefinition> Enemies => _enemies;
        public IReadOnlyList<WeaponDefinition> Weapons => _weapons;
        public WeaponDefinition PlayerWeapon { get; }

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
    }
}
