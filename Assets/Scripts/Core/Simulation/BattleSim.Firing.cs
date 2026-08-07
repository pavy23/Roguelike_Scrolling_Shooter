using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public sealed partial class BattleSim
    {
        int FindEnemyIndexById(int enemyId)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                if (_enemies[i].Id == enemyId)
                    return i;
            }
            return -1;
        }

        void ResolveEnemyPlayerCollisions()
        {
            int index = 0;
            while (index < _enemies.Count)
            {
                if ((_enemyMovementFlags[index]
                        & EnemyMovementBossRetreat) != 0)
                {
                    index++;
                    continue;
                }
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
                RemoveEnemyAt(index);
                ApplyPlayerHit(contactDamage);
            }
        }

        void ResolveSegmentChainPlayerCollisions()
        {
            if (!_playerAlive)
                return;
            for (int chainIndex = 0;
                chainIndex < _segmentChainRuntimes.Count;
                chainIndex++)
            {
                SegmentChainRuntime chain =
                    _segmentChainRuntimes[chainIndex];
                if (chain.Definition.ContactDamage == 0)
                    continue;
                int stateOffset = 0;
                for (int previous = 0;
                    previous < chainIndex;
                    previous++)
                    stateOffset += _segmentChainRuntimes[previous]
                        .Definition.SegmentCount;
                for (int segmentIndex = 0;
                    segmentIndex < chain.Definition.SegmentCount;
                    segmentIndex++)
                {
                    SegmentChainState segment =
                        _segmentChainStates[stateOffset + segmentIndex];
                    if (!Intersects(
                            PlayerX,
                            PlayerY,
                            _playerHalfWidth,
                            _playerHalfHeight,
                            segment.X,
                            segment.Y,
                            chain.Definition.HalfWidth,
                            chain.Definition.HalfHeight))
                        continue;
                    ApplyPlayerHit(chain.Definition.ContactDamage);
                    return;
                }
            }
        }

        void ResolveObstaclePlayerCollisions()
        {
            for (int i = 0; i < _obstacles.Count; i++)
            {
                ObstacleState obstacle = _obstacles[i];
                if (!Intersects(
                        PlayerX,
                        PlayerY,
                        _playerHalfWidth,
                        _playerHalfHeight,
                        obstacle.X,
                        obstacle.Y,
                        _obstacleHalfWidth,
                        _obstacleHalfHeight))
                    continue;

                ApplyPlayerHit(_obstacleContactDamage);
            }
        }

        bool ApplyPlayerHit(int incomingDamage)
        {
            if (incomingDamage <= 0
                || !_playerAlive
                || _playerInvulnerable
                || _playerInvulnerabilityTicksRemaining > 0)
                return false;

            int eventDamage;
            if (ShieldStock > 0)
            {
                ShieldStock--;
                eventDamage = 0;
                _playerInvulnerabilityTicksRemaining =
                    _playerHitInvulnerabilityTicks;
            }
            else
            {
                _playerAlive = false;
                eventDamage = incomingDamage;
            }

            EmitEvent(
                SimEventType.PlayerHit,
                0,
                PlayerX,
                PlayerY,
                eventDamage);
            if (_hitsTaken < long.MaxValue)
                _hitsTaken++;
            if (!_playerAlive)
                EmitEvent(
                    SimEventType.PlayerKilled,
                    0,
                    PlayerX,
                    PlayerY,
                    0);
            return true;
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

                RemoveCapsuleAt(index);
                _powerUpGauge.Collect();
                EmitEvent(SimEventType.CapsulePicked, capsule.Id, capsule.X, capsule.Y, 0);
            }
        }

        void ResolveBombPickupPlayerCollisions()
        {
            int index = 0;
            while (index < _bombPickups.Count)
            {
                BombPickupState pickup = _bombPickups[index];
                if (!Intersects(
                        PlayerX, PlayerY,
                        _playerHalfWidth, _playerHalfHeight,
                        pickup.X, pickup.Y,
                        _capsuleHalfWidth, _capsuleHalfHeight))
                {
                    index++;
                    continue;
                }

                RemoveBombPickupAt(index);
                AcquireBombStock(
                    1,
                    pickup.Id,
                    pickup.X,
                    pickup.Y);
            }
        }

        void TryDropCapsule(EnemyDefinition definition, int x, int y)
        {
            if (IsRoomBoundaryCleanupActive)
                return;
            int baseWeight = Math.Max(
                0,
                definition.DropWeight
                    - _capsuleDropWeightReduction);
            if (baseWeight == 0) return;
            long scaledWeight = ScalePositiveRatioSaturated(
                baseWeight,
                _capsuleDropMultiplierNumerator,
                _capsuleDropMultiplierDenominator,
                false);
            scaledWeight = ScalePositiveRatioSaturated(
                scaledWeight,
                _contractCapsuleDropMultiplierNumerator,
                _contractCapsuleDropMultiplierDenominator,
                false);
            int dropWeight = scaledWeight >= int.MaxValue - _capsuleNoDropWeight
                ? int.MaxValue - _capsuleNoDropWeight
                : (int)scaledWeight;
            int totalWeight = _capsuleNoDropWeight + dropWeight;
            if (_dropRng.NextInt(0, totalWeight) < _capsuleNoDropWeight) return;
            if (_nextCapsuleId == int.MaxValue)
                throw new InvalidOperationException("The capsule id counter is exhausted.");
            int capsuleId = _nextCapsuleId++;
            _capsules.Add(new CapsuleState(capsuleId, x, y));
            _capsuleMagnetXRemainders.Add(0);
            _capsuleMagnetYRemainders.Add(0);
            EmitEvent(SimEventType.CapsuleDropped, capsuleId, x, y, 0);
        }

        void TryDropBomb(EnemyDefinition definition, int x, int y)
        {
            if (IsRoomBoundaryCleanupActive
                || definition.BombDropWeight == 0
                || _bombPickups.Count >= _maxBombPickups)
                return;
            long scaledWeight = ScalePositiveRatioSaturated(
                definition.BombDropWeight,
                _bombDropMultiplierNumerator,
                _bombDropMultiplierDenominator,
                false);
            int dropWeight = scaledWeight >= int.MaxValue
                - _bombNoDropWeight
                    ? int.MaxValue - _bombNoDropWeight
                    : (int)scaledWeight;
            if (_bombNoDropWeight > int.MaxValue
                - dropWeight)
                throw new InvalidOperationException(
                    "The bomb drop-table total exceeds the integer range.");
            int totalWeight =
                _bombNoDropWeight + dropWeight;
            if (!_contractGuaranteesBombDrop
                && (totalWeight == 0
                || _bombDropRng.NextInt(0, totalWeight)
                    < _bombNoDropWeight))
                return;
            if (_nextBombPickupId == int.MaxValue)
                throw new InvalidOperationException(
                    "The bomb pickup id counter is exhausted.");
            _bombPickups.Add(new BombPickupState(
                _nextBombPickupId++,
                x,
                y));
            _bombPickupMagnetXRemainders.Add(0);
            _bombPickupMagnetYRemainders.Add(0);
        }

        void RemoveCapsuleAt(int index)
        {
            _capsules.RemoveAt(index);
            _capsuleMagnetXRemainders.RemoveAt(index);
            _capsuleMagnetYRemainders.RemoveAt(index);
        }

        void RemoveBombPickupAt(int index)
        {
            _bombPickups.RemoveAt(index);
            _bombPickupMagnetXRemainders.RemoveAt(index);
            _bombPickupMagnetYRemainders.RemoveAt(index);
        }

        void SpawnMainShotVolley(bool burstContinuation)
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException("The bullet id counter is exhausted.");
            int remainingBudget = GetRemainingPlayerBulletCapacity();
            SpawnMainShotFrom(PlayerX, PlayerY, ref remainingBudget);
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.MainShot);
            // REQ-128: admission order is part of the deterministic rule.
            // The body fires first, followed by options in their stable index order.
            for (int i = 0; i < _options.Count && remainingBudget > 0; i++)
                SpawnMainShotFrom(
                    _options[i].X,
                    _options[i].Y,
                    ref remainingBudget);
            if (burstContinuation)
                return;
            _burstShotsRemaining = _burstCount - 1;
            _burstCooldownTicks = _burstShotsRemaining > 0
                ? _burstIntervalTicks
                : 0;
            _cooldown = ComputeReducedInterval(
                _fireIntervalTicks,
                _mainShotLevel,
                _mainShotRapidFireStartLevel,
                _mainShotFireIntervalReductionPerLevel,
                _mainShotMinimumFireIntervalTicks);
        }

        /// <summary>
        /// 레이저 3단(프리즘 빔) 중의 옵션 발리 (REQ-184). 빔은 본체에서만 나가는데
        /// 예전에는 빔이 메인샷 발리를 통째로 대체해 옵션 화력이 0이 됐다
        /// (사람 보고 2026-08-07). 옵션은 2단(랜스) 볼트를 그대로 계속 쏜다 —
        /// 볼트 데미지·관통은 3단 프로필이 2단과 같은 값을 물려받는다.
        /// 본체 몫은 쏘지 않으므로 버스트 상태는 건드리지 않는다.
        /// </summary>
        void SpawnBeamOptionVolley()
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException("The bullet id counter is exhausted.");
            int remainingBudget = GetRemainingPlayerBulletCapacity();
            for (int i = 0; i < _options.Count && remainingBudget > 0; i++)
                SpawnMainShotFrom(
                    _options[i].X,
                    _options[i].Y,
                    ref remainingBudget);
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.MainShot);
            _cooldown = ComputeReducedInterval(
                _fireIntervalTicks,
                _mainShotLevel,
                _mainShotRapidFireStartLevel,
                _mainShotFireIntervalReductionPerLevel,
                _mainShotMinimumFireIntervalTicks);
        }

        void SpawnMainShotFrom(int x, int y, ref int remainingBudget)
        {
            if (remainingBudget <= 0)
                return;
            if (_playerWeaponType != WeaponType.Spread)
            {
                SpawnBullet(BulletKind.MainShot, x, y);
                remainingBudget--;
                ApplyMainShotVelocity(
                    _bullets.Count - 1,
                    _bulletSpeedNumerator,
                    0,
                    _bulletSpeedDenominator);
                return;
            }

            int spreadStep = GetCurrentSpreadStepLutSlots();
            for (int i = 0; i < _spreadWays && remainingBudget > 0; i++)
            {
                int rotation;
                if (_mainShotAngleLutSlots.Length != 0)
                {
                    rotation = _mainShotAngleLutSlots[i];
                }
                else
                {
                    long centeredIndex =
                        2L * i - (_spreadWays - 1L);
                    rotation = (int)(
                        (centeredIndex * spreadStep / 2)
                        % SineLut.Length);
                }
                SpawnSpreadBullet(x, y, rotation);
                remainingBudget--;
            }
        }

        void SpawnSpreadBullet(int x, int y, int lutRotation)
        {
            SpawnBullet(BulletKind.MainShot, x, y);
            int index = ((lutRotation % SineLut.Length)
                + SineLut.Length)
                % SineLut.Length;
            int sin = SineLut[index];
            int cos = SineLut[
                (index + SineLut.Length / 4)
                % SineLut.Length];
            long velocityX = (long)_bulletSpeedNumerator * cos;
            long velocityY = (long)_bulletSpeedNumerator * sin;
            long velocityDenominator =
                (long)_bulletSpeedDenominator * SineScale;
            int bulletIndex = _bullets.Count - 1;
            ApplyMainShotVelocity(
                bulletIndex,
                velocityX,
                velocityY,
                velocityDenominator);
        }

        int GetCurrentSpreadStepLutSlots()
        {
            if (_pulsePeriodTicks == 0
                || _pulseMaxStepLutSlots
                    == _pulseMinStepLutSlots)
                return _spreadStepLutSlots;
            int halfPeriod = Math.Max(1, _pulsePeriodTicks / 2);
            int phase = Tick % _pulsePeriodTicks;
            int distance = phase <= halfPeriod
                ? phase
                : _pulsePeriodTicks - phase;
            int range = _pulseMaxStepLutSlots
                - _pulseMinStepLutSlots;
            return _pulseMinStepLutSlots
                + (int)((long)range * distance / halfPeriod);
        }

        void ApplyMainShotVelocity(
            int bulletIndex,
            long baseVelocityX,
            long baseVelocityY,
            long baseDenominator)
        {
            if (_inertiaVelocityPercent == 0)
            {
                if (baseVelocityY == 0
                    && baseVelocityX == _bulletSpeedNumerator
                    && baseDenominator == _bulletSpeedDenominator)
                    return;
                SetBulletVelocity(
                    bulletIndex,
                    baseVelocityX,
                    baseVelocityY,
                    baseDenominator);
                return;
            }
            const int percentDenominator = 100;
            long denominator = baseDenominator * percentDenominator;
            long velocityX = baseVelocityX * percentDenominator
                + (long)_playerVelocityX
                    * _inertiaVelocityPercent
                    * baseDenominator;
            long velocityY = baseVelocityY * percentDenominator
                + (long)_playerVelocityY
                    * _inertiaVelocityPercent
                    * baseDenominator;
            SetBulletVelocity(
                bulletIndex,
                velocityX,
                velocityY,
                denominator);
        }

        void AdvanceMainShotBurst()
        {
            if (_burstShotsRemaining == 0)
                return;
            if (_burstCooldownTicks > 0)
                _burstCooldownTicks--;
            if (_burstCooldownTicks > 0)
                return;
            if (!HasCapacityForMainShotVolley())
            {
                _burstCooldownTicks = 1;
                return;
            }
            SpawnMainShotVolley(true);
            _burstShotsRemaining--;
            _burstCooldownTicks = _burstShotsRemaining > 0
                ? _burstIntervalTicks
                : 0;
        }

        void SpawnMissileVolley()
        {
            int remainingBudget = GetRemainingPlayerBulletCapacity();
            SpawnBullet(BulletKind.Missile, PlayerX, PlayerY);
            remainingBudget--;
            EmitEvent(SimEventType.PlayerFired, 0, PlayerX, PlayerY, (int)BulletKind.Missile);
            // Match main-shot admission: body, then stable option index order.
            for (int i = 0; i < _options.Count && remainingBudget > 0; i++)
            {
                SpawnBullet(
                    BulletKind.Missile,
                    _options[i].X,
                    _options[i].Y,
                    _optionMissileDamagePercent);
                remainingBudget--;
            }
            _missileCooldown = ComputeReducedInterval(
                _missileFireIntervalTicks,
                _missileLevel,
                _missileRapidFireStartLevel,
                _missileFireIntervalReductionPerLevel,
                _missileMinimumFireIntervalTicks);
        }

        bool HasCapacityForMainShotVolley()
        {
            return GetRemainingPlayerBulletCapacity() > 0;
        }

        bool HasCapacityForMissileVolley()
        {
            return GetRemainingPlayerBulletCapacity() > 0;
        }

        int GetRemainingPlayerBulletCapacity()
        {
            return _maxBullets - CountPlayerBullets();
        }

        void SpawnBullet(
            BulletKind kind,
            int x,
            int y,
            int damagePercent = 100)
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException();
            _bullets.Add(new BulletState(
                _nextBulletId++,
                BulletFaction.Player,
                kind,
                x,
                y,
                0,
                damagePercent));
            _bulletAux.Add(new BulletAux
            {
                PiercesRemaining =
                    kind == BulletKind.MainShot
                        ? GetMainShotPierceCount()
                        : kind == BulletKind.Missile
                            ? _missilePierceEnemyCount
                            : 0
            });
            AddDefaultEnemyProjectileBehavior();
            IncrementSaturated(ref _shotsFired);
        }

        int GetMainShotPierceCount()
        {
            long count = _mainShotBasePierceEnemyCount;
            if (HasModifier(BattleModifier.PierceShot))
                count += _pierceShotEnemyCount;
            return count >= int.MaxValue
                ? int.MaxValue
                : (int)count;
        }

        int CountEnemyBullets()
        {
            int count = 0;
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Faction == BulletFaction.Enemy)
                    count++;
            return count;
        }

        int CountHostileLasers()
        {
            int count = 0;
            for (int i = 0; i < _lasers.Count; i++)
                if (_lasers[i].SourceKind != LaserSourceKind.Player)
                    count++;
            return count;
        }

        int CountPlayerBullets()
        {
            int count = 0;
            for (int i = 0; i < _bullets.Count; i++)
                if (_bullets[i].Faction == BulletFaction.Player)
                    count++;
            return count;
        }

        /// <summary>발사 위치에서 (targetX, targetY)를 향해 지정 유리수 속도의 적탄을 스폰한다.</summary>
        void SpawnEnemyAimedBullet(
            int fromX, int fromY, int targetX, int targetY,
            int speedNumerator, int speedDenominator, int lutRotation,
            Generation.BossPhase phase = null)
        {
            if (CountEnemyBullets() >= _maxEnemyBullets) return;
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException();

            long dx = (long)targetX - fromX;
            long dy = (long)targetY - fromY;
            while (dx > MaxAimComponentBeforeRotation
                || dx < -MaxAimComponentBeforeRotation
                || dy > MaxAimComponentBeforeRotation
                || dy < -MaxAimComponentBeforeRotation)
            {
                dx /= 2;
                dy /= 2;
            }
            if (lutRotation != 0)
            {
                int index = ((lutRotation % SineLut.Length) + SineLut.Length) % SineLut.Length;
                int sin = SineLut[index];
                int cos = SineLut[(index + SineLut.Length / 4) % SineLut.Length];
                long rotatedX = (dx * cos - dy * sin) / SineScale;
                long rotatedY = (dx * sin + dy * cos) / SineScale;
                dx = rotatedX;
                dy = rotatedY;
            }
            long length = IntegerSqrt(dx * dx + dy * dy);
            if (length == 0) { dx = -1; dy = 0; length = 1; }

            // 서브유닛/틱 = speedNum/(speedDen) × (dx, dy)/len → 분모 speedDen×len 유리수
            long velDen = speedDenominator * length;
            long velXNum = speedNumerator * dx;
            long velYNum = speedNumerator * dy;
            while (velDen > int.MaxValue || Math.Abs(velXNum) > int.MaxValue || Math.Abs(velYNum) > int.MaxValue)
            {
                velDen >>= 1;
                velXNum >>= 1;
                velYNum >>= 1;
                if (velDen < 1) { velDen = 1; break; }
            }

            BulletKind kind = phase == null
                ? BulletKind.EnemyShot
                : ToBulletKind(phase.ProjectileKind);
            int collisionScale = kind == BulletKind.Heavy
                ? HeavyCollisionScalePercent
                : 100;
            BossSignaturePattern signature = phase == null
                ? BossSignaturePattern.None
                : phase.SignaturePattern;
            _bullets.Add(new BulletState(
                _nextBulletId++,
                BulletFaction.Enemy,
                kind,
                fromX,
                fromY,
                0,
                100,
                collisionScale,
                signature));
            _bulletAux.Add(new BulletAux
            {
                VelXNumerator = (int)velXNum,
                VelYNumerator = (int)velYNum,
                VelDenominator = (int)velDen,
                SplitAfterTicks =
                    phase == null ? 0 : phase.SplitAfterTicks,
                MineTravelTicks =
                    phase == null ? 0 : phase.MineTravelTicks,
                MineTelegraphTicks =
                    phase == null ? 0 : phase.MineTelegraphTicks,
                AccelerationXNumerator =
                    phase == null ? 0 : phase.MineAccelerationNumerator,
                AccelerationYNumerator = 0,
                AccelerationDenominator =
                    phase == null ? 1 : phase.MineAccelerationDenominator,
                HomingTurnLutSlotsPerTick =
                    phase != null
                        && phase.SignaturePattern == BossSignaturePattern.Brood
                            ? phase.SignatureHomingTurnLutSlotsPerTick
                            : 0
            });
        }

        static BulletKind ToBulletKind(BossProjectileKind kind)
        {
            switch (kind)
            {
                case BossProjectileKind.Normal: return BulletKind.EnemyShot;
                case BossProjectileKind.Heavy: return BulletKind.Heavy;
                case BossProjectileKind.Splitter: return BulletKind.Splitter;
                case BossProjectileKind.Mine: return BulletKind.Mine;
                case BossProjectileKind.BossLaser: return BulletKind.BossLaser;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss projectile kind.");
            }
        }

        /// <summary>
        /// 방금 Add된 탄의 적탄 거동 필드를 기본값으로 채운다. 병렬 리스트
        /// 시절에는 거동 리스트 7개에 Add하는 별도 단계였고, 통합 후에도
        /// 호출 구조를 유지한다 — 스폰 지점마다 거동 기본값을 복붙하지 않게.
        /// </summary>
        void AddDefaultEnemyProjectileBehavior()
        {
            ref BulletAux aux = ref _bulletAux[_bulletAux.Count - 1];
            aux.SplitAfterTicks = 0;
            aux.MineTravelTicks = 0;
            aux.MineTelegraphTicks = 0;
            aux.AccelerationXNumerator = 0;
            aux.AccelerationYNumerator = 0;
            aux.AccelerationDenominator = 1;
            aux.HomingTurnLutSlotsPerTick = 0;
        }

        static long IntegerSqrt(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 2) return value;

            // 나눗셈 비교로 mid*mid 오버플로를 피하는 순수 정수 이진 탐색.
            // 상한은 floor(sqrt(long.MaxValue))다.
            long low = 1;
            long high = Math.Min(value, 3037000499L);
            long result = 1;
            while (low <= high)
            {
                long mid = low + ((high - low) >> 1);
                if (mid <= value / mid)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return result;
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

        static int MultiplySaturated(int value, int multiplier)
        {
            long product = (long)value * multiplier;
            return product >= int.MaxValue
                ? int.MaxValue
                : (int)product;
        }

        bool HasModifier(BattleModifier modifier)
        {
            return (_activeModifiers & modifier) != 0;
        }

        bool HasBulletHitEnemy(int bulletId, int enemyId)
        {
            for (int i = 0; i < _bulletHitRecordCount; i++)
            {
                if (_bulletHitRecordBulletIds[i] == bulletId
                    && _bulletHitRecordEnemyIds[i] == enemyId)
                    return true;
            }
            return false;
        }

        void RecordBulletHit(int bulletId, int enemyId)
        {
            if (_bulletHitRecordCount == _bulletHitRecordBulletIds.Length)
                throw new InvalidOperationException(
                    "The preallocated bullet hit history is exhausted.");
            _bulletHitRecordBulletIds[_bulletHitRecordCount] = bulletId;
            _bulletHitRecordEnemyIds[_bulletHitRecordCount] = enemyId;
            _bulletHitRecordCount++;
        }

        void ClearBulletHitRecords(int bulletId)
        {
            int write = 0;
            for (int read = 0; read < _bulletHitRecordCount; read++)
            {
                if (_bulletHitRecordBulletIds[read] == bulletId)
                    continue;
                _bulletHitRecordBulletIds[write] =
                    _bulletHitRecordBulletIds[read];
                _bulletHitRecordEnemyIds[write] =
                    _bulletHitRecordEnemyIds[read];
                write++;
            }
            _bulletHitRecordCount = write;
        }

        void RemoveBulletAt(int index)
        {
            int bulletId = _bullets[index].Id;
            _bullets.RemoveAt(index);
            _bulletAux.RemoveAt(index);
            ClearBulletHitRecords(bulletId);
        }

        void RemoveEnemyAt(int index)
        {
            _enemies.RemoveAt(index);
            _enemyDefinitions.RemoveAt(index);
            _enemyXRemainders.RemoveAt(index);
            _enemySpawnYs.RemoveAt(index);
            _enemyAges.RemoveAt(index);
            _enemyDiveTargetYs.RemoveAt(index);
            _enemyMovementFlags.RemoveAt(index);
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

        int ScaleEnemyHp(int baseHp)
        {
            long scaled = ScalePositiveRatioSaturated(
                baseHp,
                _enemyHpMultiplierNumerator,
                _enemyHpMultiplierDenominator,
                true);
            scaled = ScalePositiveRatioSaturated(
                scaled,
                _encounterEnemyHpMultiplierNumerator,
                _encounterEnemyHpMultiplierDenominator,
                true);
            return scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled;
        }

        static long ScalePositiveRatioSaturated(
            long value,
            int numerator,
            int denominator,
            bool roundUp)
        {
            long quotient = value / denominator;
            long remainder = value % denominator;
            long whole = MultiplySaturated(quotient, numerator);
            long fractionProduct = remainder * numerator;
            long fraction = roundUp
                ? (fractionProduct + denominator - 1) / denominator
                : fractionProduct / denominator;
            return whole > long.MaxValue - fraction
                ? long.MaxValue
                : whole + fraction;
        }

        static int SaturateToInt(long value)
        {
            if (value < int.MinValue) return int.MinValue;
            if (value > int.MaxValue) return int.MaxValue;
            return (int)value;
        }

        static ScheduledSpawn[] BuildSchedule(
            StagePlan stagePlan, BattleContent content, out long totalTicks)
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
            totalTicks = segmentStart;
            return result;
        }

        int ComputeMissileDamage(int baseDamage, int damagePercent)
        {
            int levelDamage = Damage.Compute(
                baseDamage,
                Math.Max(1, _missileLevel),
                _missileDamageGrowthPercentPerLevel);
            long scaled = (long)levelDamage * damagePercent / 100;
            return scaled >= int.MaxValue
                ? int.MaxValue
                : (int)scaled;
        }

        int ComputeMainShotDamage(in BulletState bullet)
        {
            if (bullet.Kind == BulletKind.GhostMainShot)
            {
                if (bullet.FixedDamage < 1)
                    throw new InvalidOperationException(
                        "A ghost projectile is missing fixed damage.");
                return bullet.FixedDamage;
            }
            return Damage.Compute(
                _playerBulletDamage,
                Math.Max(1, _mainShotLevel));
        }

        static int SaturatingAddDamage(int left, int right)
        {
            long sum = (long)left + right;
            return sum >= int.MaxValue
                ? int.MaxValue
                : (int)sum;
        }

        static ScheduledObstacle[] BuildObstacleSchedule(StagePlan stagePlan)
        {
            var schedule = new List<ScheduledObstacle>();
            long segmentStart = 0;
            for (int segmentIndex = 0;
                segmentIndex < stagePlan.Segments.Count;
                segmentIndex++)
            {
                StageSegment segment = stagePlan.Segments[segmentIndex];
                for (int obstacleIndex = 0;
                    obstacleIndex < segment.Obstacles.Count;
                    obstacleIndex++)
                {
                    if (segmentStart > int.MaxValue)
                        throw new ArgumentException(
                            "Stage obstacle timeline exceeds the tick range.",
                            nameof(stagePlan));
                    schedule.Add(new ScheduledObstacle(
                        (int)segmentStart,
                        segment.Obstacles[obstacleIndex]));
                }
                segmentStart += segment.LengthTicks;
            }
            return schedule.ToArray();
        }

        static int[] BuildSegmentStartTicks(StagePlan stagePlan)
        {
            var result = new int[stagePlan.Segments.Count];
            long startTick = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (startTick > int.MaxValue)
                    throw new ArgumentException(
                        "Stage environment timeline exceeds the tick range.",
                        nameof(stagePlan));
                result[i] = (int)startTick;
                startTick += stagePlan.Segments[i].LengthTicks;
            }
            return result;
        }

        static void BuildSegmentScrollProfile(
            StagePlan stagePlan,
            int baseNumerator,
            int baseDenominator,
            out long[] startOffsets,
            out int[] speedNumerators,
            out int[] speedDenominators,
            out int totalTicks,
            out long endOffset)
        {
            int count = stagePlan.Segments.Count;
            startOffsets = new long[count];
            speedNumerators = new int[count];
            speedDenominators = new int[count];
            long offset = 0;
            long ticks = 0;
            for (int i = 0; i < count; i++)
            {
                StageSegment segment = stagePlan.Segments[i];
                long leftNumerator = baseNumerator;
                long leftDenominator = baseDenominator;
                long rightNumerator =
                    segment.ScrollSpeedMultiplierNumerator;
                long rightDenominator =
                    segment.ScrollSpeedMultiplierDenominator;
                long crossA = GreatestCommonDivisor(
                    leftNumerator,
                    rightDenominator);
                long crossB = GreatestCommonDivisor(
                    rightNumerator,
                    leftDenominator);
                leftNumerator /= crossA;
                rightDenominator /= crossA;
                rightNumerator /= crossB;
                leftDenominator /= crossB;
                long numerator = checked(
                    leftNumerator * rightNumerator);
                long denominator = checked(
                    leftDenominator * rightDenominator);
                long divisor = GreatestCommonDivisor(
                    numerator,
                    denominator);
                numerator /= divisor;
                denominator /= divisor;
                if (numerator > int.MaxValue
                    || denominator > int.MaxValue)
                    throw new ArgumentException(
                        "A segment scroll-speed product exceeds the supported rational range.",
                        nameof(stagePlan));

                startOffsets[i] = offset;
                speedNumerators[i] = (int)numerator;
                speedDenominators[i] = (int)denominator;
                offset = checked(
                    offset
                    + ComputeScrollX(
                        segment.LengthTicks,
                        speedNumerators[i],
                        speedDenominators[i]));
                ticks = checked(ticks + segment.LengthTicks);
            }
            if (ticks > int.MaxValue)
                throw new ArgumentException(
                    "Stage scroll timeline exceeds the tick range.",
                    nameof(stagePlan));
            totalTicks = (int)ticks;
            endOffset = offset;
        }

        static bool HasSegmentScrollMultipliers(StagePlan stagePlan)
        {
            for (int i = 0; i < stagePlan.Segments.Count; i++)
            {
                StageSegment segment = stagePlan.Segments[i];
                if (segment.ScrollSpeedMultiplierNumerator
                        != segment.ScrollSpeedMultiplierDenominator)
                    return true;
            }
            return false;
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
            if (!Enum.IsDefined(typeof(WeaponType), config.PlayerWeaponType))
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerWeaponType));
            if (config.PlayerSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpeedNumerator));
            if (config.PlayerSpeedDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpeedDenominator));
            if (config.PlayerWeaponFamily.HasValue
                && !Enum.IsDefined(
                    typeof(PrimaryWeaponFamily),
                    config.PlayerWeaponFamily.Value))
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerWeaponFamily));
            if (config.PlayerBulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerBulletSpeedNumerator));
            if (config.PlayerBulletSpeedDenominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerBulletSpeedDenominator));
            if (config.FireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.FireIntervalTicks));
            if (config.MainShotBaseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotBaseDamage));
            if (config.MainShotHalfWidth < 0
                || config.MainShotHalfHeight < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MainShotHalfWidth));
            if (config.MaxBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxBullets));
            if (config.MaxEnemies < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MaxEnemies));
            if (config.MainShotRapidFireStartLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotRapidFireStartLevel));
            if (config.MainShotFireIntervalReductionPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MainShotFireIntervalReductionPerLevel));
            if (config.MainShotMinimumFireIntervalTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MainShotMinimumFireIntervalTicks));
            ValidateWeaponProfile(
                config.LaserBaseDamage,
                config.LaserFireIntervalTicks,
                config.LaserRapidFireStartLevel,
                config.LaserFireIntervalReductionPerLevel,
                config.LaserMinimumFireIntervalTicks,
                config.LaserSpeedNumerator,
                config.LaserSpeedDenominator,
                config.LaserHalfWidth,
                config.LaserHalfHeight,
                nameof(config.LaserBaseDamage));
            if (config.LaserPierceEnemyCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.LaserPierceEnemyCount));
            ValidateWeaponProfile(
                config.SpreadBaseDamage,
                config.SpreadFireIntervalTicks,
                config.SpreadRapidFireStartLevel,
                config.SpreadFireIntervalReductionPerLevel,
                config.SpreadMinimumFireIntervalTicks,
                config.SpreadSpeedNumerator,
                config.SpreadSpeedDenominator,
                config.SpreadHalfWidth,
                config.SpreadHalfHeight,
                nameof(config.SpreadBaseDamage));
            if (config.SpreadWays < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.SpreadWays));
            if (config.SpreadStepLutSlots < 0
                || config.SpreadStepLutSlots > SineLut.Length / 2)
                throw new ArgumentOutOfRangeException(
                    nameof(config.SpreadStepLutSlots));
            if (config.MainShotAngleLutSlots == null)
                throw new ArgumentNullException(
                    nameof(config.MainShotAngleLutSlots));
            if (config.MainShotAngleLutSlots.Length != 0
                && config.MainShotAngleLutSlots.Length
                    != config.SpreadWays)
                throw new ArgumentException(
                    "Main-shot angle count must match SpreadWays.",
                    nameof(config.MainShotAngleLutSlots));
            for (int i = 0;
                i < config.MainShotAngleLutSlots.Length;
                i++)
            {
                int angle = config.MainShotAngleLutSlots[i];
                if (angle < -SineLut.Length / 2
                    || angle > SineLut.Length / 2)
                    throw new ArgumentOutOfRangeException(
                        nameof(config.MainShotAngleLutSlots));
            }
            if (config.MissileBaseDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MissileBaseDamage));
            if (config.MissileDamageGrowthPercentPerLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MissileDamageGrowthPercentPerLevel));
            if (config.OptionMissileDamagePercent < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.OptionMissileDamagePercent));
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
            if (config.MissileDropDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MissileDropDelayTicks));
            if (config.OptionFollowDelayTicks < 0
                || (config.OptionFormation == OptionFormation.Trail
                    && config.OptionFollowDelayTicks < 1))
                throw new ArgumentOutOfRangeException(nameof(config.OptionFollowDelayTicks));
            if (config.StartingShieldStock < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.StartingShieldStock));
            if (config.MaxShieldStock < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxShieldStock));
            if (config.PlayerHitInvulnerabilityTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.PlayerHitInvulnerabilityTicks));
            if (config.StartingBombStock < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.StartingBombStock));
            if (config.MaxBombStock < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxBombStock));
            if (config.BombInvulnerabilityTicks < 0
                || config.BombEffectRadiusSubUnits < 0
                || config.BombRegularEnemyDamage < 0
                || config.BombBossDamageCap < 0
                || config.BombBossPartDamageCap < 0
                || config.BombNoDropWeight < 0
                || config.MaxBombPickups < 0
                || config.MaxLasers < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.BombInvulnerabilityTicks));
            if (config.PlayerHalfWidth < 0 || config.PlayerHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerHalfWidth));
            if (config.CapsuleHalfWidth < 0 || config.CapsuleHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleHalfWidth));
            if (config.CapsuleNoDropWeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.CapsuleNoDropWeight));
            if (config.CapsuleDropWeightReduction < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleDropWeightReduction));
            if (config.ContractBombDropMultiplierNumerator < 0
                || config.ContractBombDropMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractBombDropMultiplierNumerator));
            if (config.ContractCapsuleDropMultiplierNumerator < 0
                || config.ContractCapsuleDropMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractCapsuleDropMultiplierNumerator));
            if (config.ContractScoreMultiplierNumerator < 0
                || config.ContractScoreMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ContractScoreMultiplierNumerator));
            if (config.ScrollSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedNumerator));
            if (config.ScrollSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.ScrollSpeedDenominator));
            if (config.CapsuleMagnetRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetRadiusSubUnits));
            if (config.CapsuleMagnetSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetSpeedNumerator));
            if (config.CapsuleMagnetSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.CapsuleMagnetSpeedDenominator));
            if (config.RareEncounterChanceNumerator < 0
                || config.RareEncounterChanceDenominator < 1
                || config.RareEncounterChanceNumerator
                    > config.RareEncounterChanceDenominator)
                throw new ArgumentOutOfRangeException(
                    nameof(config.RareEncounterChanceNumerator));
            if (config.RareRewardSelectionCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.RareRewardSelectionCount));
            if (config.MaxObstacles < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxObstacles));
            if (config.ObstacleHalfWidth < 0
                || config.ObstacleHalfHeight < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ObstacleHalfWidth));
            if (config.BossDamageScorePerHundred < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.BossDamageScorePerHundred));
            if (config.ObstacleContactDamage < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ObstacleContactDamage));
            if (config.BreakableObstacleScore < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.BreakableObstacleScore));
            if (config.EnemyHpMultiplierNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.EnemyHpMultiplierNumerator));
            if (config.EnemyHpMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(config.EnemyHpMultiplierDenominator));
            if (config.EnemyBulletSpeedNumerator < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletSpeedNumerator));
            if (config.EnemyBulletSpeedDenominator < 1)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletSpeedDenominator));
            if (config.EnemyBulletHalfWidth < 0 || config.EnemyBulletHalfHeight < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletHalfWidth));
            if (config.EnemyBulletDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.EnemyBulletDamage));
            if (config.MaxEnemyBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(config.MaxEnemyBullets));
            if (config.PierceShotEnemyCount < 0)
                throw new ArgumentOutOfRangeException(nameof(config.PierceShotEnemyCount));
            if (config.RicochetRangeSubUnits < 0)
                throw new ArgumentOutOfRangeException(nameof(config.RicochetRangeSubUnits));
            if (config.HomingMissileTurnLutSlotsPerTick < 0
                || config.HomingMissileTurnLutSlotsPerTick > SineLut.Length / 2)
                throw new ArgumentOutOfRangeException(
                    nameof(config.HomingMissileTurnLutSlotsPerTick));
            if (config.MissileFamily == MissileFamily.Homing
                && config.HomingMissileTurnLutSlotsPerTick < 1)
                throw new ArgumentException(
                    "Homing missile config requires a positive turn rate.",
                    nameof(config.HomingMissileTurnLutSlotsPerTick));
            if (config.KillExplosionRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillExplosionRadiusSubUnits));
            if (config.KillExplosionDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(config.KillExplosionDamage));
            if (config.KillExplosionMaxTargets < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillExplosionMaxTargets));
            if (config.GrazeExtraRadiusSubUnits < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.GrazeExtraRadiusSubUnits));
            if ((long)Math.Max(config.PlayerHalfWidth, config.PlayerHalfHeight)
                    + Math.Max(
                        config.EnemyBulletHalfWidth,
                        config.EnemyBulletHalfHeight)
                    + config.GrazeExtraRadiusSubUnits
                > MaxSquareRoot)
                throw new ArgumentOutOfRangeException(
                    nameof(config.GrazeExtraRadiusSubUnits),
                    "The combined graze radius exceeds the supported integer range.");
            if (config.GrazeScore < 0)
                throw new ArgumentOutOfRangeException(nameof(config.GrazeScore));
            if (config.KillComboGaugeGain < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.KillComboGaugeGain));
            if (config.ComboGaugeRequirements == null
                || config.ComboGaugeRequirements.Length
                    != BattleSimConfig.ComboMultiplierLevelCount - 1)
                throw new ArgumentException(
                    "Combo gauge requirements must contain exactly five entries.",
                    nameof(config.ComboGaugeRequirements));
            for (int i = 0; i < config.ComboGaugeRequirements.Length; i++)
            {
                if (config.ComboGaugeRequirements[i] < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(config.ComboGaugeRequirements));
            }
            if (config.ComboDecayTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(config.ComboDecayTicks));
            if (config.ComboMultipliers == null
                || config.ComboMultipliers.Length
                    != BattleSimConfig.ComboMultiplierLevelCount)
                throw new ArgumentException(
                    "Combo multipliers must contain exactly six entries.",
                    nameof(config.ComboMultipliers));
            for (int i = 0; i < config.ComboMultipliers.Length; i++)
            {
                if (config.ComboMultipliers[i] < 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(config.ComboMultipliers));
            }
            if (config.ShieldBonusScorePerStock < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(config.ShieldBonusScorePerStock));
            if ((long)config.MaxBullets + config.MaxEnemyBullets > int.MaxValue)
                throw new ArgumentOutOfRangeException(
                    nameof(config.MaxEnemyBullets),
                    "Combined bullet capacity exceeds the supported integer range.");
            if (config.BulletDespawnX < 0)
                throw new ArgumentOutOfRangeException(nameof(config.BulletDespawnX));
            if (config.PlayerMinX > config.PlayerMaxX || config.PlayerMinY > config.PlayerMaxY)
                throw new ArgumentException("Player bounds are reversed.", nameof(config));
            if (config.PlayerSpawnX < config.PlayerMinX || config.PlayerSpawnX > config.PlayerMaxX)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnX));
            if (config.PlayerSpawnY < config.PlayerMinY || config.PlayerSpawnY > config.PlayerMaxY)
                throw new ArgumentOutOfRangeException(nameof(config.PlayerSpawnY));
        }

        static void ValidateWeaponProfile(
            int baseDamage,
            int fireIntervalTicks,
            int rapidFireStartLevel,
            int fireIntervalReductionPerLevel,
            int minimumFireIntervalTicks,
            int speedNumerator,
            int speedDenominator,
            int halfWidth,
            int halfHeight,
            string parameterName)
        {
            if (baseDamage < 0
                || fireIntervalTicks < 0
                || rapidFireStartLevel < 1
                || fireIntervalReductionPerLevel < 0
                || minimumFireIntervalTicks < 0
                || speedNumerator < 0
                || speedDenominator < 1
                || halfWidth < 0
                || halfHeight < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
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

        readonly struct ScheduledObstacle
        {
            public ScheduledObstacle(int tick, ObstacleSpawn obstacle)
            {
                Tick = tick;
                Obstacle = obstacle
                    ?? throw new ArgumentNullException(nameof(obstacle));
            }

            public int Tick { get; }
            public ObstacleSpawn Obstacle { get; }
        }
    }
}
