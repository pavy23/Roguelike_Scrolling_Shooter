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
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Enemy id cannot be null or empty.", nameof(id));
            if (maxHp < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (contactDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(contactDamage));
            if (!Enum.IsDefined(typeof(EnemyMovePattern), movePattern))
                throw new ArgumentOutOfRangeException(nameof(movePattern));
            if (moveSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedNumerator));
            if (moveSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(moveSpeedDenominator));
            if (halfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(halfWidth));
            if (halfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(halfHeight));
            if (dropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(dropWeight));
            if (sineAmplitude < 0)
                throw new ArgumentOutOfRangeException(nameof(sineAmplitude));
            if (sinePeriodTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(sinePeriodTicks));

            Id = id;
            MaxHp = maxHp;
            ContactDamage = contactDamage;
            MovePattern = movePattern;
            MoveSpeedNumerator = moveSpeedNumerator;
            MoveSpeedDenominator = moveSpeedDenominator;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            DropWeight = dropWeight;
            SineAmplitude = sineAmplitude;
            SinePeriodTicks = sinePeriodTicks;
        }

        public string Id { get; }
        public int MaxHp { get; }
        public int ContactDamage { get; }
        public EnemyMovePattern MovePattern { get; }
        public int MoveSpeedNumerator { get; }
        public int MoveSpeedDenominator { get; }
        public int HalfWidth { get; }
        public int HalfHeight { get; }
        public int DropWeight { get; }
        public int SineAmplitude { get; }
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
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Weapon id cannot be null or empty.", nameof(id));
            if (baseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (fireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (projectileSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileSpeedNumerator));
            if (projectileSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(projectileSpeedDenominator));
            if (projectileHalfWidth < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileHalfWidth));
            if (projectileHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(projectileHalfHeight));

            Id = id;
            BaseDamage = baseDamage;
            FireIntervalTicks = fireIntervalTicks;
            ProjectileSpeedNumerator = projectileSpeedNumerator;
            ProjectileSpeedDenominator = projectileSpeedDenominator;
            ProjectileHalfWidth = projectileHalfWidth;
            ProjectileHalfHeight = projectileHalfHeight;
        }

        public string Id { get; }
        public int BaseDamage { get; }
        public int FireIntervalTicks { get; }
        public int ProjectileSpeedNumerator { get; }
        public int ProjectileSpeedDenominator { get; }
        public int ProjectileHalfWidth { get; }
        public int ProjectileHalfHeight { get; }
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
