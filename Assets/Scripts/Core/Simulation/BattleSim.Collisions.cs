using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public sealed partial class BattleSim
    {
        void ResolvePlayerLaserEnemyCollisions()
        {
            int beamIndex = FindPlayerBeamIndex();
            if (beamIndex < 0)
                return;
            LaserState laser = _lasers[beamIndex];
            int index = 0;
            while (index < _enemies.Count)
            {
                EnemyState enemy = _enemies[index];
                EnemyDefinition definition =
                    _enemyDefinitions[index];
                int radius = SaturatingAddDamage(
                    Math.Max(
                        definition.HalfWidth,
                        definition.HalfHeight),
                    laser.HalfWidth);
                if (!LaserGeometry.IntersectsSegmentCircle(
                        laser.StartX,
                        laser.StartY,
                        laser.EndX,
                        laser.EndY,
                        enemy.X,
                        enemy.Y,
                        radius))
                {
                    index++;
                    continue;
                }
                int hp = Damage.ApplyToHp(
                    enemy.Hp,
                    laser.Damage);
                if (hp > 0)
                {
                    _enemies[index] = new EnemyState(
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
                        laser.Damage);
                    index++;
                    continue;
                }
                RemoveEnemyAt(index);
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
                if (HasModifier(BattleModifier.KillExplosion))
                    ApplyKillExplosion(enemy.Id, enemy.X, enemy.Y);
            }
        }

        void ResolvePlayerLaserBossCollisions()
        {
            if (!BossActive || BossEntering)
                return;
            int beamIndex = FindPlayerBeamIndex();
            if (beamIndex < 0)
                return;
            LaserState laser = _lasers[beamIndex];
            if (_bossPartDefinitions.Count == 0)
            {
                int radius = SaturatingAddDamage(
                    Math.Max(_bossHalfWidth, _bossHalfHeight),
                    laser.HalfWidth);
                if (LaserGeometry.IntersectsSegmentCircle(
                        laser.StartX,
                        laser.StartY,
                        laser.EndX,
                        laser.EndY,
                        _bossX,
                        _bossY,
                        radius))
                    ApplyDamageToBoss(laser.Damage);
                return;
            }

            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartState part = _bossPartStates[i];
                if (part.Destroyed || !part.Active)
                    continue;
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                int radius = SaturatingAddDamage(
                    Math.Max(
                        definition.HalfWidth,
                        definition.HalfHeight),
                    laser.HalfWidth);
                if (!LaserGeometry.IntersectsSegmentCircle(
                        laser.StartX,
                        laser.StartY,
                        laser.EndX,
                        laser.EndY,
                        part.X,
                        part.Y,
                        radius))
                    continue;
                ApplyDamageToBossPart(i, laser.Damage);
                return;
            }
        }

        void ResolvePlayerLaserSegmentChainCollisions()
        {
            int beamIndex = FindPlayerBeamIndex();
            if (beamIndex < 0)
                return;
            LaserState laser = _lasers[beamIndex];
            for (int i = 0; i < _segmentChainRuntimes.Count; i++)
            {
                SegmentChainRuntime chain = _segmentChainRuntimes[i];
                int radius = SaturatingAddDamage(
                    Math.Max(
                        chain.Definition.HalfWidth,
                        chain.Definition.HalfHeight),
                    laser.HalfWidth);
                if (!LaserGeometry.IntersectsSegmentCircle(
                        laser.StartX,
                        laser.StartY,
                        laser.EndX,
                        laser.EndY,
                        chain.HeadX,
                        chain.HeadY,
                        radius))
                    continue;
                ApplyDamageToSegmentChain(i, laser.Damage);
                return;
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
                            _missileBaseDamage,
                            bullet.DamagePercent)
                        : ComputeMainShotDamage(in bullet);
                    if (bullet.Kind == BulletKind.Missile
                        && _missileFamily
                            == MissileFamily.SpreadBomb)
                    {
                        damage = SaturatingAddDamage(
                            damage,
                            ComputeMissileDamage(
                                _missileExplosionDamage,
                                bullet.DamagePercent));
                    }
                    ApplyDamageToObstacleAt(
                        obstacleIndex,
                        damage,
                        bullet.X,
                        bullet.Y);
                }

                // Terrain blocks every player projectile, including laser pierce.
                if (bullet.Kind == BulletKind.Missile
                    && _missileFamily == MissileFamily.SpreadBomb)
                {
                    ApplyMissileExplosion(
                        bullet.Id,
                        obstacle.X,
                        obstacle.Y,
                        bullet.DamagePercent);
                }
                RemoveBulletAt(bulletIndex);
            }
        }

        void ResolveEnemyBulletObstacleCollisions()
        {
            int bulletIndex = 0;
            while (bulletIndex < _bullets.Count)
            {
                BulletState bullet = _bullets[bulletIndex];
                if (bullet.Faction != BulletFaction.Enemy)
                {
                    bulletIndex++;
                    continue;
                }

                int obstacleIndex = -1;
                int bulletHalfWidth = ScaleProjectileHitbox(
                    _enemyBulletHalfWidth,
                    bullet.CollisionScalePercent);
                int bulletHalfHeight = ScaleProjectileHitbox(
                    _enemyBulletHalfHeight,
                    bullet.CollisionScalePercent);
                for (int i = 0; i < _obstacles.Count; i++)
                {
                    if (!_obstacleBlocksEnemyBullets[i])
                        continue;
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
                    {
                        obstacleIndex = i;
                        break;
                    }
                }
                if (obstacleIndex < 0)
                {
                    bulletIndex++;
                    continue;
                }

                ObstacleState blocker = _obstacles[obstacleIndex];
                RemoveBulletAt(bulletIndex);
                EmitEvent(
                    SimEventType.EnemyBulletBlocked,
                    bullet.Id,
                    bullet.X,
                    bullet.Y,
                    blocker.Id);
            }
        }

        void ApplyDamageToObstacleAt(
            int obstacleIndex,
            int damage,
            int hitX,
            int hitY)
        {
            ObstacleState obstacle = _obstacles[obstacleIndex];
            int hp = Damage.ApplyToHp(obstacle.Hp, damage);
            if (hp > 0)
            {
                _obstacles[obstacleIndex] = new ObstacleState(
                    obstacle.Id,
                    obstacle.Type,
                    obstacle.X,
                    obstacle.Y,
                    hp);
                EmitEvent(
                    SimEventType.ObstacleDamaged,
                    obstacle.Id,
                    hitX,
                    hitY,
                    hp);
                return;
            }

            int regenDelayTicks =
                _obstacleRegenDelayTicks[obstacleIndex];
            if (regenDelayTicks > 0)
            {
                if (regenDelayTicks > int.MaxValue - Tick)
                    throw new InvalidOperationException(
                        "The obstacle regeneration tick exceeds the simulation range.");
                _pendingObstacleRegens.Add(
                    new ObstacleRegenerationState(
                        obstacle.Id,
                        obstacle.Type,
                        obstacle.X,
                        obstacle.Y,
                        _obstacleMaxHps[obstacleIndex],
                        _obstacleBlocksEnemyBullets[obstacleIndex],
                        regenDelayTicks,
                        Tick + regenDelayTicks));
            }
            RemoveObstacleAt(obstacleIndex);
            int awardedScore = AwardScore(_breakableObstacleScore);
            EmitEvent(
                SimEventType.ObstacleDestroyed,
                obstacle.Id,
                obstacle.X,
                obstacle.Y,
                awardedScore);
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
                    ? ComputeMissileDamage(
                        _missileBaseDamage,
                        bullet.DamagePercent)
                    : ComputeMainShotDamage(in bullet);
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
                    if (_bulletAux[bulletIndex].PiercesRemaining > 0)
                    {
                        _bulletAux[bulletIndex].PiercesRemaining--;
                        keepBullet = true;
                    }

                    if (HasModifier(BattleModifier.Ricochet)
                        && _bulletAux[bulletIndex].RicochetUsed
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
                            _bulletAux[bulletIndex].RicochetUsed++;
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
                            enemy.Y,
                            bullet.DamagePercent);
                    }
                    else if (_bulletAux[bulletIndex].PiercesRemaining > 0)
                    {
                        _bulletAux[bulletIndex].PiercesRemaining--;
                        keepBullet = true;
                    }
                }

                if (!keepBullet
                    && bullet.Kind == BulletKind.MainShot
                    && _impactExplosionDamage > 0)
                {
                    ApplyImpactExplosion(
                        bullet.Id,
                        enemy.Id,
                        enemy.X,
                        enemy.Y);
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

        void ApplyImpactExplosion(
            int sourceBulletId,
            int excludedEnemyId,
            int centerX,
            int centerY)
        {
            EmitEvent(
                SimEventType.KillExplosionTriggered,
                sourceBulletId,
                centerX,
                centerY,
                _impactExplosionDamage);
            long radiusSquared =
                SquaredRadiusSaturated(_impactExplosionRadius);
            int index = 0;
            while (index < _enemies.Count)
            {
                EnemyState enemy = _enemies[index];
                if (enemy.Id == excludedEnemyId
                    || SquaredDistanceSaturated(
                        centerX,
                        centerY,
                        enemy.X,
                        enemy.Y) > radiusSquared)
                {
                    index++;
                    continue;
                }
                int hp = Damage.ApplyToHp(
                    enemy.Hp,
                    _impactExplosionDamage);
                if (hp > 0)
                {
                    _enemies[index] = new EnemyState(
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
                        _impactExplosionDamage);
                    index++;
                    continue;
                }
                EnemyDefinition definition =
                    _enemyDefinitions[index];
                RemoveEnemyAt(index);
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
            }
        }

        void ApplyMissileExplosion(
            int sourceBulletId,
            int centerX,
            int centerY,
            int damagePercent)
        {
            int damage = ComputeMissileDamage(
                _missileExplosionDamage,
                damagePercent);
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
                        || !part.Active
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
            long bulletRadius = Math.Max(
                ScaleProjectileHitbox(
                    _enemyBulletHalfWidth,
                    bullet.CollisionScalePercent),
                ScaleProjectileHitbox(
                    _enemyBulletHalfHeight,
                    bullet.CollisionScalePercent));
            long radius = playerRadius + bulletRadius + _grazeExtraRadiusSubUnits;
            long radiusSquared = radius * radius;
            return SquaredDistanceSaturated(
                PlayerX,
                PlayerY,
                bullet.X,
                bullet.Y) <= radiusSquared;
        }

        static int ScaleProjectileHitbox(int halfExtent, int percent)
        {
            return SaturateToInt((long)halfExtent * percent / 100);
        }

        int RecordKillScore(long baseScore)
        {
            int awardedScore = AwardScore(baseScore);
            RecordComboAction();
            return awardedScore;
        }

        void RecordComboAction()
        {
            _comboActionThisTick = true;
            _ticksSinceLastComboAction = 0;
        }

        /// <summary>
        /// REQ-133: boss damage counts as a combo action and pays a small score.
        ///
        /// Called only where damage was actually applied — a shot absorbed by an
        /// invulnerable part must NOT keep the combo alive. That case already tells
        /// the player it did nothing (the view draws a cyan deflect spark), so
        /// rewarding it would make the screen lie in the other direction.
        ///
        /// Score is paid per whole 100 damage with the remainder carried, so a long
        /// fight pays the same total no matter how the damage is chunked. Carrying
        /// the remainder (instead of rounding each hit) keeps it deterministic and
        /// stops rapid weak hits from paying nothing at all.
        /// </summary>
        void RecordBossDamage(int appliedDamage)
        {
            if (appliedDamage <= 0) return;
            RecordComboAction();
            if (_bossDamageScorePerHundred <= 0) return;

            _bossDamageScoreCarry += appliedDamage;
            if (_bossDamageScoreCarry < 100) return;
            long chunks = _bossDamageScoreCarry / 100;
            _bossDamageScoreCarry -= (int)(chunks * 100);
            AwardScore(chunks * _bossDamageScorePerHundred);
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

        /// <summary>
        /// 그레이즈는 **한 번에 한 단계** 올린다 (사람 지시 2026-08-03:
        /// "스치기 한번에 배율 올리기로 하자. 잘 안오르네").
        ///
        /// 게이지 경로(AddComboGauge)를 쓰지 않는 이유: 그 함수는 임계를 넘는 동안
        /// 루프를 돌아, 이득을 키우면 한 번에 여러 단계가 뛴다. "한 번에 한 단계"는
        /// 게이지 수치로는 표현할 수 없어서 규칙으로 옮겼다.
        ///
        /// 킬은 그대로 게이지를 쓴다 — 잡졸을 쓸어 담는 것과 탄에 몸을 붙이는 것은
        /// 위험의 성격이 다르고, 보상 곡선도 달라야 한다.
        ///
        /// 빠르다고 느껴지면 감쇠(ComboDecayTicks)로 조인다. 스치기를 멈추면
        /// 7초마다 한 단계씩 내려가므로, 최대 배율은 계속 위험 속에 있어야 유지된다.
        /// </summary>
        void AdvanceMultiplierFromGraze()
        {
            if (_multiplierLevel >= _comboMultipliers.Length - 1)
                return;
            // 같은 순간에 여러 발이 스치면 각각 한 단계씩 올라 5-way 탄막 하나로 x32가
            // 됐다 (사람 보고 2026-08-03: "스칠 때 배율이 한 번만 올라야 하는데 한방에
            // 32max가 된다"). 스침은 **순간**이지 발수가 아니다 — 쿨다운 안에서는
            // 몇 발이 지나가든 한 단계만 올린다.
            if (_ticksSinceGrazeLevelUp < GrazeLevelUpCooldownTicks)
                return;
            _ticksSinceGrazeLevelUp = 0;
            _comboGauge = 0;
            _multiplierLevel++;
            AppendEvent(
                SimEventType.MultiplierChanged,
                _multiplierLevel,
                PlayerX,
                PlayerY,
                ScoreMultiplier);
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
            if (_comboActionThisTick || _multiplierLevel == 0)
                return;

            // 플레이어가 콤보를 이을 **수단이 하나도 없는 시간**에는 시계를 세운다.
            //
            // 사람 보고 2026-08-03: "중간보스/보스 진입 순간 배율이 리셋된다." 앞서
            // BossEntering만 막았는데 부족했다 — 보스가 **아직 스폰되기 전** 진입
            // 구간이 남아 있었다. 그 구간에는 잡졸도 없고 적탄도 없고 보스도 없어
            // 킬·그레이즈·딜 셋 다 불가능한데 감쇠만 돌았다.
            //
            // 조건은 **보스룸 진입 구간**으로 좁힌다. 처음엔 "적·적탄·보스가 전부 없으면
            // 멈춘다"로 일반화했는데, 그러면 평상시 웨이브 사이 소강에서도 멈춰 감쇠가
            // 사실상 무력해진다(테스트가 정확히 그 지점을 잡았다).
            //
            // 보스룸(_bossMaxHp > 0)인데 보스가 아직 안 온 시간만 세운다 —
            // 그 구간은 잡졸도 이미 끝났고 보스도 없어 손쓸 방법이 정말로 없다.
            if (_bossMaxHp > 0 && !_bossSpawned)
                return;

            // 보스 등장 연출·페이즈 전환 중에도 같은 이유로 멈춘다 (데미지가 거부된다).
            //
            // 이 구간에서는 플레이어가 콤보를 이을 방법이 **하나도 없다**: 보스는
            // ApplyDamageToBoss/Part가 BossEntering을 걸러 데미지를 안 받고, 잡졸은
            // 이미 정리됐으며, 보스는 아직 탄을 뿌리지 않아 그레이즈도 없다.
            // 손쓸 방법이 없는 시간에 벌을 주면 그건 규칙이 아니라 사고다.
            //
            // 감쇠를 아예 없애는 것이 아니라 **멈추는** 것이다 — 연출이 끝나 보스가
            // 자리를 잡으면 그 자리부터 다시 흐른다.
            if (BossEntering || BossTransitioning)
                return;

            if (_ticksSinceLastComboAction < _comboDecayTicks)
                _ticksSinceLastComboAction++;
            if (_ticksSinceLastComboAction < _comboDecayTicks)
                return;

            _ticksSinceLastComboAction = 0;
            // 게이지를 0으로 쓸어버리지 않고 절반만 깎는다 (사람 보고 2026-08-03:
            // "스치기로 배율 올라가는 게 여전히 안 된다").
            //
            // 예전에는 한 단계 떨어질 때 게이지가 통째로 날아갔다. 그러면 다음 단계까지
            // 쌓아 둔 것이 매번 리셋되어, 탄이 얇은 구간에서는 아무리 스쳐도 영원히
            // 첫 배율에 못 닿는다 — 실제로 그레이즈 이득 3, 첫 임계 30이라
            // "감쇠 창 안에 10번 스치기"를 요구하고 있었다.
            //
            // 절반만 깎으면 "밀렸지만 지운 건 아니다"가 되어, 다시 붙으면 금방 회복된다.
            _comboGauge /= 2;
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
            _ticksSinceLastComboAction = 0;
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
    }
}
