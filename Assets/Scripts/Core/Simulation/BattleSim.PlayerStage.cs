using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public sealed partial class BattleSim
    {
        void UpdateOptionPositions()
        {
            while (_options.Count > _optionLevel)
                _options.RemoveAt(_options.Count - 1);
            while (_options.Count < _optionLevel)
                _options.Add(default);

            for (int i = 0; i < _options.Count; i++)
            {
                int index = i + 1;
                int x;
                int y;
                if (_optionFormation == OptionFormation.Fixed)
                {
                    x = SaturateToInt(
                        (long)PlayerX + _optionFixedOffsetXs[i]);
                    y = Math.Max(
                        _playerMinY,
                        Math.Min(
                            _playerMaxY,
                            SaturateToInt(
                                (long)PlayerY
                                + _optionFixedOffsetYs[i])));
                }
                else if (_optionFormation == OptionFormation.Orbit)
                {
                    int baseSlot = (int)(
                        (long)Tick
                        * _optionOrbitAngularLutSlotsNumerator
                        / _optionOrbitAngularLutSlotsDenominator);
                    int slot = (
                        baseSlot
                        + i * SineLut.Length / _options.Count)
                        % SineLut.Length;
                    int sin = SineLut[slot];
                    int cos = SineLut[
                        (slot + SineLut.Length / 4)
                        % SineLut.Length];
                    x = SaturateToInt(
                        (long)PlayerX
                        + (long)_optionOrbitRadiusSubUnits
                            * cos / SineScale);
                    y = SaturateToInt(
                        (long)PlayerY
                        + (long)_optionOrbitRadiusSubUnits
                            * sin / SineScale);
                }
                else
                {
                    GetPlayerPositionAgo(
                        checked(index * _optionFollowDelayTicks),
                        out x,
                        out y);
                }
                _options[i] = new OptionState(index, x, y);
            }
        }

        void ValidateLoadoutConfig()
        {
            if (!Enum.IsDefined(typeof(MissileFamily), _missileFamily))
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.MissileFamily));
            if (!Enum.IsDefined(typeof(OptionFormation), _optionFormation))
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.OptionFormation));
            if (_missilePierceEnemyCount < 0
                || _missileExplosionDamage < 0
                || _missileExplosionRadiusSubUnits < 0
                || _missileExplosionMaxTargets < 0
                || _missileDamageGrowthPercentPerLevel < 0
                || _missileDropDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.MissilePierceEnemyCount));
            if (_missileFamily == MissileFamily.DownwardDrop
                && (_missileFallSpeedYNumerator < 1
                    || _missileDropDelayTicks < 1))
                throw new ArgumentException(
                    "Downward-drop missile config requires fall speed "
                    + "and drop delay.");
            if (_optionOrbitRadiusSubUnits < 0
                || _optionOrbitAngularLutSlotsNumerator < 0
                || _optionOrbitAngularLutSlotsDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(BattleSimConfig.OptionOrbitRadiusSubUnits));
            if (_optionFormation == OptionFormation.Fixed
                && _optionFixedOffsetXs.Length
                    != _optionFixedOffsetYs.Length)
                throw new ArgumentException(
                    "Fixed formation requires one X/Y offset per option.");
        }

        void RecordPlayerPosition()
        {
            if (_playerHistoryCount > 0)
                _playerHistoryHead = (_playerHistoryHead + 1) % _playerHistoryX.Length;
            _playerHistoryX[_playerHistoryHead] = PlayerX;
            _playerHistoryY[_playerHistoryHead] = PlayerY;
            if (_playerHistoryCount < _playerHistoryX.Length)
                _playerHistoryCount++;
        }

        void GetPlayerPositionAgo(int ticksAgo, out int x, out int y)
        {
            int availableTicksAgo = Math.Min(ticksAgo, _playerHistoryCount - 1);
            int historyIndex = _playerHistoryHead - availableTicksAgo;
            if (historyIndex < 0)
                historyIndex += _playerHistoryX.Length;
            x = _playerHistoryX[historyIndex];
            y = _playerHistoryY[historyIndex];
        }

        // 46340 / 65536 is the greatest 16-bit fixed-point diagonal
        // component whose two-dimensional magnitude does not exceed one.
        const int DigitalDirectionScale = 65536;
        const int DigitalDiagonalComponent = 46340;

        void UpdateEnvironmentState()
        {
            int segmentIndex = -1;
            for (int i = 0; i < _stageSegments.Count; i++)
            {
                long endTick =
                    (long)_segmentStartTicks[i]
                    + _stageSegments[i].LengthTicks;
                if (Tick >= _segmentStartTicks[i] && Tick < endTick)
                {
                    segmentIndex = i;
                    break;
                }
            }

            if (segmentIndex != _currentEnvironmentSegmentIndex)
            {
                _driftXRemainder = 0;
                _driftYRemainder = 0;
                _currentEnvironmentSegmentIndex = segmentIndex;
            }
            if (segmentIndex < 0)
            {
                _environment = new StageEnvironmentState(
                    -1,
                    null,
                    false,
                    0,
                    0,
                    0,
                    0,
                    1,
                    0,
                    1);
                return;
            }

            StageSegment segment = _stageSegments[segmentIndex];
            SegmentEnvironmentDefinition definition =
                segment.Environment;
            int localTick = Tick - _segmentStartTicks[segmentIndex];
            int corridorMinY = definition.HasCorridor
                ? InterpolateSegmentValue(
                    definition.StartMinY,
                    definition.EndMinY,
                    localTick,
                    segment.LengthTicks)
                : 0;
            int corridorMaxY = definition.HasCorridor
                ? InterpolateSegmentValue(
                    definition.StartMaxY,
                    definition.EndMaxY,
                    localTick,
                    segment.LengthTicks)
                : 0;
            _environment = new StageEnvironmentState(
                segmentIndex,
                segment.SegmentId,
                definition.HasCorridor,
                corridorMinY,
                corridorMaxY,
                definition.CorridorContactDamage,
                definition.DriftXNumerator,
                definition.DriftXDenominator,
                definition.DriftYNumerator,
                definition.DriftYDenominator);
        }

        static int InterpolateSegmentValue(
            int start,
            int end,
            int elapsedTicks,
            int durationTicks)
        {
            return SaturateToInt(
                (long)start
                + ((long)end - start) * elapsedTicks / durationTicks);
        }

        void ExpireTimeLimitIfNeeded()
        {
            if (_timeLimitExpired
                || _timeLimitTicks == 0
                || Tick < _timeLimitTicks
                || _bossDefeated)
                return;

            _timeLimitExpired = true;
            EmitEvent(
                SimEventType.TimeLimitExpired,
                0,
                PlayerX,
                PlayerY,
                _timeLimitTicks);
            if (!_playerAlive || _playerInvulnerable)
                return;
            ShieldStock = 0;
            _playerAlive = false;
            EmitEvent(
                SimEventType.PlayerKilled,
                0,
                PlayerX,
                PlayerY,
                0);
        }

        void AdvancePlayer(in InputCommand input)
        {
            int controlledX;
            int controlledY;
            if (input.UseAnalogMovement)
            {
                _playerXRemainder = 0;
                _playerYRemainder = 0;
                ClampAnalogDelta(
                    input.AnalogDeltaXSubUnits,
                    input.AnalogDeltaYSubUnits,
                    out int deltaX,
                    out int deltaY);
                controlledX = ClampPlayerPosition(
                    PlayerX,
                    deltaX,
                    _playerMinX,
                    _playerMaxX);
                controlledY = ClampPlayerPosition(
                    PlayerY,
                    deltaY,
                    _playerMinY,
                    _playerMaxY);
            }
            else
            {
                int componentScale =
                    input.MoveX != 0 && input.MoveY != 0
                        ? DigitalDiagonalComponent
                        : DigitalDirectionScale;
                controlledX = AdvanceDigitalPlayerAxis(
                    PlayerX,
                    input.MoveX,
                    componentScale,
                    ref _playerXRemainder,
                    _playerMinX,
                    _playerMaxX);
                controlledY = AdvanceDigitalPlayerAxis(
                    PlayerY,
                    input.MoveY,
                    componentScale,
                    ref _playerYRemainder,
                    _playerMinY,
                    _playerMaxY);
            }

            AdvanceSuctionForce(
                out int suctionDeltaX,
                out int suctionDeltaY);

            int driftX = AdvanceSignedFraction(
                _environment.DriftXNumerator,
                _environment.DriftXDenominator,
                ref _driftXRemainder);
            int driftY = AdvanceSignedFraction(
                _environment.DriftYNumerator,
                _environment.DriftYDenominator,
                ref _driftYRemainder);
            PlayerX = ClampPlayerPosition(
                controlledX,
                SaturateToInt((long)driftX + suctionDeltaX),
                _playerMinX,
                _playerMaxX);

            long candidateY =
                (long)controlledY + driftY + suctionDeltaY;
            int minimumY = _playerMinY;
            int maximumY = _playerMaxY;
            bool corridorContact = false;
            if (_environment.HasCorridor)
            {
                minimumY = Math.Max(
                    minimumY,
                    SaturateToInt(
                        (long)_environment.CorridorMinY
                        + _playerHalfHeight));
                maximumY = Math.Min(
                    maximumY,
                    SaturateToInt(
                        (long)_environment.CorridorMaxY
                        - _playerHalfHeight));
                if (minimumY > maximumY)
                    throw new InvalidOperationException(
                        "The active corridor is narrower than the player hitbox.");
                corridorContact =
                    candidateY < minimumY || candidateY > maximumY;
            }
            PlayerY = candidateY <= minimumY
                ? minimumY
                : candidateY >= maximumY
                    ? maximumY
                    : (int)candidateY;
            if (corridorContact)
            {
                EmitEvent(
                    SimEventType.CorridorContact,
                    _environment.SegmentIndex,
                    PlayerX,
                    PlayerY,
                    _environment.CorridorContactDamage);
                ApplyPlayerHit(
                    _environment.CorridorContactDamage);
            }
        }

        static int AdvanceSignedFraction(
            int numerator,
            int denominator,
            ref long remainder)
        {
            long accumulated = remainder + numerator;
            int delta = (int)(accumulated / denominator);
            remainder = accumulated % denominator;
            return delta;
        }

        void AdvanceSuctionForce(out int deltaX, out int deltaY)
        {
            if (!_bossSuctionActive
                || _bossSuctionPartIndex < 0
                || _bossSuctionPartIndex >= _bossPartStates.Length)
            {
                deltaX = 0;
                deltaY = 0;
                return;
            }

            BossPartState source =
                _bossPartStates[_bossSuctionPartIndex];
            BossPartAttackProfile attack =
                GetBossPartAttack(_bossSuctionPartIndex);
            int sourceX = GetSuctionSourceX(source, attack);
            int sourceY = GetSuctionSourceY(source, attack);
            long directionX = (long)sourceX - PlayerX;
            long directionY = (long)sourceY - PlayerY;
            ScaleVectorForProducts(ref directionX, ref directionY);
            ulong absoluteX = directionX < 0
                ? (ulong)(-directionX)
                : (ulong)directionX;
            ulong absoluteY = directionY < 0
                ? (ulong)(-directionY)
                : (ulong)directionY;
            ulong lengthSquared = absoluteX * absoluteX
                + absoluteY * absoluteY;
            if (lengthSquared != 0)
            {
                ulong length = IntegerSquareRoot(lengthSquared);
                if (length * length < lengthSquared)
                    length++;
                long normalizedX = directionX * SineScale
                    / (long)length;
                long normalizedY = directionY * SineScale
                    / (long)length;
                long divisor = checked(
                    (long)attack.EffectSpeedDenominator
                    * SineScale);
                long accumulatedX = checked(
                    _bossSuctionAccelerationXRemainder
                    + normalizedX * attack.EffectSpeedNumerator);
                long accumulatedY = checked(
                    _bossSuctionAccelerationYRemainder
                    + normalizedY * attack.EffectSpeedNumerator);
                _bossSuctionDeltaX = SaturateToInt(
                    accumulatedX / divisor);
                _bossSuctionDeltaY = SaturateToInt(
                    accumulatedY / divisor);
                _bossSuctionAccelerationXRemainder =
                    accumulatedX % divisor;
                _bossSuctionAccelerationYRemainder =
                    accumulatedY % divisor;
                ClampSuctionDelta(attack);
            }
            else
            {
                _bossSuctionDeltaX = 0;
                _bossSuctionDeltaY = 0;
                _bossSuctionAccelerationXRemainder = 0;
                _bossSuctionAccelerationYRemainder = 0;
            }

            deltaX = _bossSuctionDeltaX;
            deltaY = _bossSuctionDeltaY;
        }

        void ClampSuctionDelta(BossPartAttackProfile attack)
        {
            ulong x = AbsoluteAsUnsigned(_bossSuctionDeltaX);
            ulong y = AbsoluteAsUnsigned(_bossSuctionDeltaY);
            ulong lengthSquared = x * x + y * y;
            if (lengthSquared == 0)
                return;
            ulong length = IntegerSquareRoot(lengthSquared);
            if (length * length < lengthSquared)
                length++;
            long scaledLength = checked(
                (long)length * attack.EffectMaxSpeedDenominator);
            if (scaledLength <= attack.EffectMaxSpeedNumerator)
                return;
            long divisor = scaledLength;
            _bossSuctionDeltaX = SaturateToInt(
                (long)_bossSuctionDeltaX
                * attack.EffectMaxSpeedNumerator
                / divisor);
            _bossSuctionDeltaY = SaturateToInt(
                (long)_bossSuctionDeltaY
                * attack.EffectMaxSpeedNumerator
                / divisor);
        }

        void ClampAnalogDelta(
            int requestedX,
            int requestedY,
            out int deltaX,
            out int deltaY)
        {
            if ((requestedX == 0 && requestedY == 0)
                || _playerSpeedNumerator == 0)
            {
                deltaX = 0;
                deltaY = 0;
                return;
            }

            ulong absoluteX = AbsoluteAsUnsigned(requestedX);
            ulong absoluteY = AbsoluteAsUnsigned(requestedY);
            ulong lengthSquared =
                absoluteX * absoluteX + absoluteY * absoluteY;
            ulong speedNumerator = (ulong)_playerSpeedNumerator;
            ulong speedDenominator = (ulong)_playerSpeedDenominator;
            ulong maximumLengthSquared =
                speedNumerator * speedNumerator
                / (speedDenominator * speedDenominator);

            if (lengthSquared <= maximumLengthSquared)
            {
                deltaX = requestedX;
                deltaY = requestedY;
                return;
            }

            ulong lengthCeiling = IntegerSquareRoot(lengthSquared);
            if (lengthCeiling * lengthCeiling < lengthSquared)
                lengthCeiling++;
            long divisor =
                (long)speedDenominator * (long)lengthCeiling;
            deltaX = (int)(
                (long)requestedX * _playerSpeedNumerator / divisor);
            deltaY = (int)(
                (long)requestedY * _playerSpeedNumerator / divisor);
        }

        int AdvanceDigitalPlayerAxis(
            int position,
            int direction,
            int componentScale,
            ref long remainder,
            int min,
            int max)
        {
            if (direction == 0) return position;
            long divisor =
                (long)_playerSpeedDenominator
                * DigitalDirectionScale;
            long accumulated =
                remainder
                + (long)direction
                * _playerSpeedNumerator
                * componentScale;
            long candidate = position + accumulated / divisor;
            long nextRemainder = accumulated % divisor;
            if (direction < 0 && candidate <= min) { remainder = 0; return min; }
            if (direction > 0 && candidate >= max) { remainder = 0; return max; }
            remainder = nextRemainder;
            return (int)candidate;
        }

        static int ClampPlayerPosition(
            int position,
            int delta,
            int min,
            int max)
        {
            long candidate = (long)position + delta;
            if (candidate <= min)
                return min;
            if (candidate >= max)
                return max;
            return (int)candidate;
        }

        static ulong AbsoluteAsUnsigned(int value)
        {
            return value < 0
                ? (ulong)(-(long)value)
                : (ulong)value;
        }

        static ulong IntegerSquareRoot(ulong value)
        {
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value)
                bit >>= 2;
            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return result;
        }

        void UpdateEnemyProjectileBehaviors()
        {
            for (int index = _bullets.Count - 1; index >= 0; index--)
            {
                BulletState bullet = _bullets[index];
                if (bullet.Faction != BulletFaction.Enemy)
                    continue;
                if (bullet.Kind == BulletKind.Splitter
                    && bullet.AgeTicks == _bulletAux[index].SplitAfterTicks)
                {
                    SplitEnemyProjectile(index, in bullet);
                    continue;
                }
                if (bullet.Kind == BulletKind.Mine)
                    UpdateMineProjectile(index, in bullet);
                int turn = _bulletAux[index].HomingTurnLutSlotsPerTick;
                // 유도는 **한시적**이다. 시간이 지나면 마지막 방향으로 흘러간다.
                if (turn > 0 && bullet.AgeTicks < _enemyHomingDurationTicks)
                    TurnEnemyProjectileTowardPlayer(index, in bullet, turn);
            }
        }

        void SplitEnemyProjectile(int index, in BulletState parent)
        {
            long velocityX = _bulletAux[index].VelXNumerator;
            long velocityY = _bulletAux[index].VelYNumerator;
            long denominator = _bulletAux[index].VelDenominator;
            BossSignaturePattern signature = parent.SignaturePattern;
            RemoveBulletAt(index);
            int available = Math.Max(
                0,
                _maxEnemyBullets - CountEnemyBullets());
            int children = Math.Min(3, available);
            if (children < 3)
                EmitEvent(
                    SimEventType.EnemyBulletCapacityExceeded,
                    _bossId,
                    parent.X,
                    parent.Y,
                    _maxEnemyBullets);
            for (int child = 0; child < children; child++)
            {
                int rotation = (child - 1) * SplitterStepLutSlots;
                RotateVector(
                    velocityX,
                    velocityY,
                    rotation,
                    out long childX,
                    out long childY);
                AddExactEnemyBullet(
                    BulletKind.EnemyShot,
                    signature,
                    parent.X,
                    parent.Y,
                    childX,
                    childY,
                    denominator,
                    100);
            }
        }

        void UpdateMineProjectile(int index, in BulletState mine)
        {
            int travelTicks = _bulletAux[index].MineTravelTicks;
            int telegraphTicks = _bulletAux[index].MineTelegraphTicks;
            if (mine.AgeTicks == travelTicks)
            {
                SetBulletVelocity(index, 0, 0, 1);
                return;
            }
            if (mine.AgeTicks < travelTicks + telegraphTicks)
                return;
            if (mine.AgeTicks == travelTicks + telegraphTicks)
            {
                int acceleration = _bulletAux[index].AccelerationXNumerator;
                int accelerationDenominator =
                    _bulletAux[index].AccelerationDenominator;
                long dx = (long)PlayerX - mine.X;
                long dy = (long)PlayerY - mine.Y;
                ScaleVectorForProducts(ref dx, ref dy);
                long length = IntegerSqrt(dx * dx + dy * dy);
                if (length == 0)
                {
                    dx = -1;
                    length = 1;
                }
                long denominator = accelerationDenominator * length;
                SetBulletAcceleration(
                    index,
                    acceleration * dx,
                    acceleration * dy,
                    denominator);
            }
            SetBulletVelocity(
                index,
                (long)_bulletAux[index].VelXNumerator
                    + _bulletAux[index].AccelerationXNumerator,
                (long)_bulletAux[index].VelYNumerator
                    + _bulletAux[index].AccelerationYNumerator,
                _bulletAux[index].VelDenominator);
        }

        void SetBulletAcceleration(
            int index,
            long x,
            long y,
            long denominator)
        {
            while (denominator > int.MaxValue
                || Math.Abs(x) > int.MaxValue
                || Math.Abs(y) > int.MaxValue)
            {
                denominator >>= 1;
                x >>= 1;
                y >>= 1;
            }
            if (denominator < 1)
                denominator = 1;
            _bulletAux[index].AccelerationXNumerator = (int)x;
            _bulletAux[index].AccelerationYNumerator = (int)y;
            _bulletAux[index].AccelerationDenominator = (int)denominator;
            SetBulletVelocity(index, 0, 0, denominator);
        }

        void TurnEnemyProjectileTowardPlayer(
            int index,
            in BulletState bullet,
            int turn)
        {
            long desiredX = (long)PlayerX - bullet.X;
            long desiredY = (long)PlayerY - bullet.Y;
            ScaleVectorForProducts(ref desiredX, ref desiredY);
            long velocityX = _bulletAux[index].VelXNumerator;
            long velocityY = _bulletAux[index].VelYNumerator;
            long cross = velocityX * desiredY - velocityY * desiredX;
            if (cross == 0)
                return;
            int rotation = cross > 0 ? turn : -turn;
            RotateVector(
                velocityX,
                velocityY,
                rotation,
                out long turnedX,
                out long turnedY);
            long currentDot = velocityX * desiredX + velocityY * desiredY;
            long turnedDot = turnedX * desiredX + turnedY * desiredY;
            if (turnedDot > currentDot)
                SetBulletVelocity(
                    index,
                    turnedX,
                    turnedY,
                    _bulletAux[index].VelDenominator);
        }

        void AddExactEnemyBullet(
            BulletKind kind,
            BossSignaturePattern signature,
            int x,
            int y,
            long velocityX,
            long velocityY,
            long denominator,
            int collisionScalePercent)
        {
            if (_nextBulletId == int.MaxValue)
                throw new InvalidOperationException(
                    "The bullet id counter is exhausted.");
            _bullets.Add(new BulletState(
                _nextBulletId++,
                BulletFaction.Enemy,
                kind,
                x,
                y,
                0,
                100,
                collisionScalePercent,
                signature));
            _bulletAux.Add(new BulletAux { VelDenominator = 1 });
            AddDefaultEnemyProjectileBehavior();
            SetBulletVelocity(
                _bullets.Count - 1,
                velocityX,
                velocityY,
                denominator);
        }

        void AdvanceBullets()
        {
            int despawnY = SimSpace.PlayfieldHalfHeightSubUnits + SimSpace.DespawnMarginSubUnits;
            int write = 0;
            for (int read = 0; read < _bullets.Count; read++)
            {
                BulletState bullet = _bullets[read];
                if (bullet.Kind == BulletKind.Missile
                    && (_missileFamily == MissileFamily.Homing
                        || HasModifier(BattleModifier.HomingMissile)))
                    UpdateHomingMissile(read, in bullet);
                int xNumerator, xDenominator, yNumerator, yDenominator;
                if (_bulletAux[read].VelDenominator > 0)
                {
                    // 적탄: 스폰 시 계산된 조준 벡터(REQ-007)
                    xNumerator = _bulletAux[read].VelXNumerator;
                    yNumerator = _bulletAux[read].VelYNumerator;
                    xDenominator = _bulletAux[read].VelDenominator;
                    yDenominator = _bulletAux[read].VelDenominator;
                }
                else
                {
                    bool isMissile = bullet.Kind == BulletKind.Missile;
                    xNumerator = isMissile ? _missileSpeedXNumerator : _bulletSpeedNumerator;
                    xDenominator = isMissile ? _missileSpeedXDenominator : _bulletSpeedDenominator;
                    bool waitingToDrop =
                        isMissile
                        && _missileFamily == MissileFamily.DownwardDrop
                        && bullet.AgeTicks < _missileDropDelayTicks;
                    yNumerator = isMissile && !waitingToDrop
                        ? -_missileFallSpeedYNumerator
                        : 0;
                    yDenominator = isMissile ? _missileFallSpeedYDenominator : 1;
                }

                long accumulatedX = _bulletAux[read].XRemainder + (long)xNumerator;
                long accumulatedY = _bulletAux[read].YRemainder + (long)yNumerator;
                int deltaX = (int)(accumulatedX / xDenominator);
                int deltaY = (int)(accumulatedY / yDenominator);
                int nextXRemainder = (int)(accumulatedX % xDenominator);
                int nextYRemainder = (int)(accumulatedY % yDenominator);
                long nextX = bullet.X + (long)deltaX;
                long nextYLong = bullet.Y + (long)deltaY;
                if (bullet.Faction == BulletFaction.Enemy
                    && IsRoomBoundaryCleanupActive)
                    nextX -= BossRetreatSpeedPerTick;
                if (bullet.Kind == BulletKind.Missile
                    && _missileFamily == MissileFamily.SpreadBomb
                    && nextYLong
                        <= -SimSpace.PlayfieldHalfHeightSubUnits)
                {
                    ApplyMissileExplosion(
                        bullet.Id,
                        SaturateToInt(nextX),
                        -SimSpace.PlayfieldHalfHeightSubUnits,
                        bullet.DamagePercent);
                    ClearBulletHitRecords(bullet.Id);
                    continue;
                }
                if (nextX > _bulletDespawnX || nextX < -(long)_bulletDespawnX
                    || nextYLong > despawnY || nextYLong < -(long)despawnY)
                {
                    ClearBulletHitRecords(bullet.Id);
                    continue;
                }
                int nextY = SaturateToInt(nextYLong);
                int nextAge = bullet.AgeTicks == int.MaxValue
                    ? int.MaxValue
                    : bullet.AgeTicks + 1;
                _bullets[write] = new BulletState(
                    bullet.Id,
                    bullet.Faction,
                    bullet.Kind,
                    (int)nextX,
                    nextY,
                    nextAge,
                    bullet.DamagePercent,
                    bullet.CollisionScalePercent,
                    bullet.SignaturePattern,
                    bullet.FixedDamage);
                _bulletAux[write] = _bulletAux[read];
                _bulletAux[write].XRemainder = nextXRemainder;
                _bulletAux[write].YRemainder = nextYRemainder;
                write++;
            }

            int removed = _bullets.Count - write;
            if (removed > 0)
            {
                _bullets.RemoveRange(write, removed);
                _bulletAux.RemoveRange(write, removed);
            }
        }

        void UpdateHomingMissile(int bulletIndex, in BulletState bullet)
        {
            if (_missileFamily == MissileFamily.DownwardDrop
                && bullet.AgeTicks < _missileDropDelayTicks)
                return;
            int targetId = _bulletAux[bulletIndex].HomingTargetId;
            int targetX;
            int targetY;
            if (targetId == 0)
            {
                targetId = FindNearestTarget(
                    bullet.X,
                    bullet.Y,
                    0,
                    long.MaxValue,
                    out targetX,
                    out targetY);
                if (targetId == 0)
                    return;
                _bulletAux[bulletIndex].HomingTargetId = targetId;
            }
            else if (targetId < 0
                || !TryGetTargetPosition(targetId, out targetX, out targetY))
            {
                // A lost lock is final: the missile continues on its current vector.
                _bulletAux[bulletIndex].HomingTargetId = -1;
                return;
            }

            if (_bulletAux[bulletIndex].VelDenominator == 0)
                InitializeMissileVelocity(bulletIndex);
            if (_homingMissileTurnLutSlotsPerTick == 0)
                return;

            long desiredX = (long)targetX - bullet.X;
            long desiredY = (long)targetY - bullet.Y;
            ScaleVectorForProducts(ref desiredX, ref desiredY);
            if (desiredX == 0 && desiredY == 0)
                return;

            long velocityX = _bulletAux[bulletIndex].VelXNumerator;
            long velocityY = _bulletAux[bulletIndex].VelYNumerator;
            long cross = velocityX * desiredY - velocityY * desiredX;
            if (cross == 0)
                return;

            int rotation = cross > 0
                ? _homingMissileTurnLutSlotsPerTick
                : -_homingMissileTurnLutSlotsPerTick;
            RotateVector(velocityX, velocityY, rotation, out long turnedX, out long turnedY);

            // Do not overshoot a target already inside the turn step. Keeping the
            // closer of the current and candidate directions avoids float angles.
            long currentDot = velocityX * desiredX + velocityY * desiredY;
            long turnedDot = turnedX * desiredX + turnedY * desiredY;
            if (turnedDot <= currentDot)
                return;

            SetBulletVelocity(
                bulletIndex,
                turnedX,
                turnedY,
                _bulletAux[bulletIndex].VelDenominator);
        }

        void InitializeMissileVelocity(int bulletIndex)
        {
            long velocityX =
                (long)_missileSpeedXNumerator * _missileFallSpeedYDenominator;
            long velocityY =
                -(long)_missileFallSpeedYNumerator * _missileSpeedXDenominator;
            long denominator =
                (long)_missileSpeedXDenominator * _missileFallSpeedYDenominator;
            SetBulletVelocity(bulletIndex, velocityX, velocityY, denominator);
        }

        static void RotateVector(
            long x,
            long y,
            int lutRotation,
            out long rotatedX,
            out long rotatedY)
        {
            int index = ((lutRotation % SineLut.Length) + SineLut.Length)
                % SineLut.Length;
            int sin = SineLut[index];
            int cos = SineLut[(index + SineLut.Length / 4) % SineLut.Length];
            rotatedX = (x * cos - y * sin) / SineScale;
            rotatedY = (x * sin + y * cos) / SineScale;
        }

        void SetBulletVelocity(
            int bulletIndex,
            long velocityX,
            long velocityY,
            long denominator)
        {
            while (denominator > int.MaxValue
                || Math.Abs(velocityX) > int.MaxValue
                || Math.Abs(velocityY) > int.MaxValue)
            {
                denominator >>= 1;
                velocityX >>= 1;
                velocityY >>= 1;
            }
            if (denominator < 1)
                denominator = 1;

            _bulletAux[bulletIndex].VelXNumerator = (int)velocityX;
            _bulletAux[bulletIndex].VelYNumerator = (int)velocityY;
            _bulletAux[bulletIndex].VelDenominator = (int)denominator;
            _bulletAux[bulletIndex].XRemainder = 0;
            _bulletAux[bulletIndex].YRemainder = 0;
        }

        static void ScaleVectorForProducts(ref long x, ref long y)
        {
            while (x > MaxAimComponentBeforeRotation
                || x < -MaxAimComponentBeforeRotation
                || y > MaxAimComponentBeforeRotation
                || y < -MaxAimComponentBeforeRotation)
            {
                x /= 2;
                y /= 2;
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
                bool retreatingForBoss =
                    (_enemyMovementFlags[index]
                        & EnemyMovementBossRetreat) != 0;
                if (Tick >= _fieldCleanupStartTick
                    && (!_bossSpawned || retreatingForBoss))
                {
                    _enemyMovementFlags[index] |=
                        EnemyMovementBossRetreat;
                    retreatingForBoss = true;
                    nextX -= BossRetreatSpeedPerTick;
                }
                int y = state.Y;

                if (ShouldAdvanceEnemyX(definition, age))
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
                        / definition.MovementPeriodTicks) % SineLut.Length);
                    long offset = (long)definition.MovementAmplitudeNumerator
                        * SineLut[phase]
                        / ((long)definition.MovementAmplitudeDenominator * SineScale);
                    y = SaturateToInt(_enemySpawnYs[index] + offset);
                }
                else if (definition.MovePattern == EnemyMovePattern.Zigzag)
                {
                    int triangle = ComputeTriangleLutValue(
                        age,
                        definition.MovementPeriodTicks);
                    long offset = (long)definition.MovementAmplitudeNumerator
                        * triangle
                        / ((long)definition.MovementAmplitudeDenominator * SineScale);
                    y = SaturateToInt(_enemySpawnYs[index] + offset);
                }
                else if (definition.MovePattern == EnemyMovePattern.Dive)
                {
                    y = AdvanceDiveY(index, definition, age);
                }

                _enemyAges[index] = age;
                _enemies[index] = new EnemyState(state.Id, state.DefinitionId, x, y, state.Hp);

                // 터렛류 조준 사격 (REQ-007 요청 1): fireIntervalTicks > 0 인 정의만.
                if (!retreatingForBoss
                    && definition.LaserAttack != null
                    && age % definition.LaserAttack.CycleIntervalTicks
                        == 0)
                {
                    TryStartLaser(
                        LaserSourceKind.Enemy,
                        state.Id,
                        definition.LaserAttack,
                        x,
                        y);
                }
                else if (!retreatingForBoss
                    && definition.LaserAttack == null
                    && definition.FireIntervalTicks > 0
                    && age % definition.FireIntervalTicks == 0)
                    SpawnEnemyAimedBullet(
                        x, y, PlayerX, PlayerY,
                        _enemyBulletSpeedNumerator, _enemyBulletSpeedDenominator, 0);
                index++;
            }
        }

        void SpawnScheduledThroughTick(int tick)
        {
            while (_nextScheduledSpawn < _scheduledSpawns.Length
                && _scheduledSpawns[_nextScheduledSpawn].Tick <= tick)
            {
                ScheduledSpawn spawn = _scheduledSpawns[_nextScheduledSpawn++];
                if (spawn.Tick >= _fieldCleanupStartTick)
                {
                    continue;
                }
                TrySpawnEnemy(spawn.Definition, spawn.X, spawn.Y);
            }

            while (_nextScheduledObstacle < _scheduledObstacles.Length
                && _scheduledObstacles[_nextScheduledObstacle].Tick <= tick)
            {
                ScheduledObstacle scheduled =
                    _scheduledObstacles[_nextScheduledObstacle++];
                if (scheduled.Tick >= _fieldCleanupStartTick)
                    continue;
                if (_obstacles.Count + _pendingObstacleRegens.Count
                    >= _maxObstacles)
                {
                    EmitEvent(
                        SimEventType.ObstacleCapacityExceeded,
                        0,
                        scheduled.Obstacle.X,
                        scheduled.Obstacle.Y,
                        _maxObstacles);
                    continue;
                }
                if (_nextObstacleId == int.MaxValue)
                    throw new InvalidOperationException(
                        "The obstacle id counter is exhausted.");

                ObstacleSpawn obstacle = scheduled.Obstacle;
                int obstacleId = _nextObstacleId++;
                AddActiveObstacle(
                    obstacleId,
                    obstacle.Type,
                    obstacle.X,
                    obstacle.Y,
                    obstacle.Hp,
                    obstacle.Hp,
                    obstacle.LaserAttack,
                    obstacle.BlocksEnemyBullets,
                    obstacle.RegenDelayTicks,
                    obstacle.HalfWidth,
                    obstacle.HalfHeight);
                if (obstacle.LaserAttack != null)
                    TryStartLaser(
                        LaserSourceKind.Terrain,
                        obstacleId,
                        obstacle.LaserAttack,
                        obstacle.X,
                        obstacle.Y);
            }
        }

        void AddActiveObstacle(
            int id,
            ObstacleType type,
            int x,
            int y,
            int hp,
            int maxHp,
            LaserAttackDefinition laserAttack,
            bool blocksEnemyBullets,
            int regenDelayTicks,
            int halfWidth = 0,
            int halfHeight = 0)
        {
            _obstacles.Add(new ObstacleState(
                id, type, x, y, hp, halfWidth, halfHeight));
            _obstacleAges.Add(0);
            _obstacleLaserAttacks.Add(laserAttack);
            _obstacleBlocksEnemyBullets.Add(blocksEnemyBullets);
            _obstacleRegenDelayTicks.Add(regenDelayTicks);
            _obstacleMaxHps.Add(maxHp);
            AddDefaultObstacleMotion();
        }

        void SpawnBossEnemy(
            EnemyDefinition definition,
            int x,
            int y)
        {
            if (definition == null)
                return;
            TrySpawnEnemy(definition, x, y);
        }

        bool TrySpawnEnemy(
            EnemyDefinition definition,
            int x,
            int y)
        {
            if (_enemies.Count >= _maxEnemies)
            {
                EmitEvent(
                    SimEventType.EnemyCapacityExceeded,
                    0,
                    x,
                    y,
                    _maxEnemies);
                return false;
            }
            if (_nextEnemyId == int.MaxValue)
                throw new InvalidOperationException(
                    "The enemy id counter is exhausted.");

            _enemies.Add(new EnemyState(
                _nextEnemyId++,
                definition.Id,
                x,
                y,
                ScaleEnemyHp(definition.MaxHp)));
            _enemyDefinitions.Add(definition);
            _enemyXRemainders.Add(0);
            _enemySpawnYs.Add(y);
            _enemyAges.Add(0);
            _enemyDiveTargetYs.Add(y);
            _enemyMovementFlags.Add(0);
            return true;
        }

        /// <summary>
        /// 보스 수명주기 (REQ-007 요청 2): 세그먼트 소진 → 우측 진입 → holdX 정지 후 사인 호버.
        /// 페이즈는 HP 균등 분할, 발사는 페이즈 파라미터의 n-way 조준 부채꼴.
        /// </summary>
        void UpdateBoss()
        {
            if (_bossTransitionTicksRemaining > 0)
            {
                _bossTransitionTicksRemaining--;
                if (_bossTransitionTicksRemaining == 0)
                    SpawnSecondBossForm();
                return;
            }
            if (_bossMaxHp == 0 || _bossDefeated) return;

            if (!_bossSpawned)
            {
                if (Tick < _stageTotalTicks) return;
                if (_nextEnemyId == int.MaxValue)
                    throw new InvalidOperationException("The enemy id counter is exhausted.");
                _bossSpawned = true;
                _bossId = _nextEnemyId++;
                _bossX = _bossSpawnX;
                _bossY = 0;
                _bossHp = _bossMaxHp;
                _bossPhase = 0;
                _bossAge = 0;
                _bossPhaseAge = 0;
                _bossMovementAnchorY = _bossY;
                _bossMovementPhaseOffsetTicks = 0;
                _bossMovementTransitionOffsetX = 0;
                _bossMovementTransitionOffsetY = 0;
                _bossVelocityX = 0;
                _bossVelocityY = 0;
                Generation.BossPhase initialPhase =
                    _bossPhases[0];
                _bossFireCooldown =
                    initialPhase.TelegraphTicks > 0
                        ? initialPhase.TelegraphTicks
                        : initialPhase.FireIntervalTicks;
                _bossPhaseTelegraphPending =
                    initialPhase.TelegraphTicks > 0;
                _bossBurstAwaitingVolley =
                    initialPhase.FirePattern == BossFirePattern.Burst
                    && initialPhase.TelegraphTicks > 0;
                _bossPatternVolleyIndex = 0;
                InitializeBossParts();
                ConfigureSegmentChainSchedule(initialPhase);
                if (_warshipDefinition != null)
                {
                    _warshipEncounter = new WarshipEncounter(
                        _warshipDefinition,
                        _warshipRuntimePartDefinitions);
                    BeginWarshipTick();
                }
                EmitEvent(SimEventType.BossSpawned, _bossId, _bossX, _bossY, 0);
                ForwardWarshipEvents();
                return;
            }

            if (_warshipEncounter != null)
            {
                _bossAge++;
                BeginWarshipTick();
                UpdateActiveBossPartAttacks();
                _bossPhaseAge++;
                return;
            }

            if (_bossAge == 0 && _bossX > _bossHoldX)
            {
                _bossX = Math.Max(
                    _bossHoldX,
                    _bossX - BossGlideSpeedPerTick);
                RefreshBossPartPositions();
                return;   // 진입 중에는 사격하지 않는다 (등장 연출 여유)
            }

            _bossAge++;
            AdvanceTimedBossPhase();
            AdvanceBossPhaseIfNothingIsDamageable();
            EmitPendingBossTelegraph();
            Generation.BossPhase phase = _bossPhases[_bossPhase];
            if (_bossPartDefinitions.Count > 0)
            {
                UpdateMultipartBoss(phase);
                UpdateBossPhaseFire(phase);
                _bossPhaseAge++;
                return;
            }

            ApplyBossPhaseMovement(phase, false);
            UpdateBossPhaseFire(phase);
            _bossPhaseAge++;
        }

        static bool ResolveTimedBossPattern(
            IReadOnlyList<Generation.BossPhase> phases)
        {
            if (phases.Count == 0)
                return false;
            bool timed = phases[0].DurationTicks > 0;
            for (int i = 1; i < phases.Count; i++)
            {
                if ((phases[i].DurationTicks > 0) != timed)
                    throw new ArgumentException(
                        "Boss phases cannot mix timed and HP-based progression.",
                        nameof(phases));
            }
            return timed;
        }

        static int GetSegmentChainCapacity(
            IReadOnlyList<Generation.BossPhase> phases,
            BossFormDefinition form2)
        {
            long capacity = 0;
            for (int i = 0; i < phases.Count; i++)
                if (phases[i].SegmentChain != null)
                    capacity += phases[i].SegmentChain.SummonCount;
            if (form2 != null)
                for (int i = 0; i < form2.Phases.Count; i++)
                    if (form2.Phases[i].SegmentChain != null)
                        capacity +=
                            form2.Phases[i].SegmentChain.SummonCount;
            if (capacity > int.MaxValue / 8)
                throw new ArgumentOutOfRangeException(
                    nameof(phases),
                    "Segment-chain state capacity exceeds the supported range.");
            return (int)capacity;
        }

        void ConfigureSegmentChainSchedule(
            Generation.BossPhase phase)
        {
            _segmentChainSummonsRemaining = phase.SegmentChain == null
                ? 0
                : phase.SegmentChain.SummonCount;
            _segmentChainSummonCooldown = 0;
        }

        void UpdateSegmentChains()
        {
            if (BossActive && !BossEntering)
            {
                SegmentChainDefinition definition =
                    _bossPhases[_bossPhase].SegmentChain;
                if (definition != null
                    && _segmentChainSummonsRemaining > 0)
                {
                    if (_segmentChainSummonCooldown > 0)
                        _segmentChainSummonCooldown--;
                    if (_segmentChainSummonCooldown == 0)
                    {
                        SpawnSegmentChain(definition);
                        _segmentChainSummonsRemaining--;
                        _segmentChainSummonCooldown =
                            definition.SummonIntervalTicks;
                    }
                }
            }

            for (int i = 0; i < _segmentChainRuntimes.Count; i++)
                AdvanceSegmentChain(_segmentChainRuntimes[i]);
            RebuildSegmentChainStates();
        }

        void SpawnSegmentChain(SegmentChainDefinition definition)
        {
            if (_nextEnemyId == int.MaxValue)
                throw new InvalidOperationException(
                    "The enemy id counter is exhausted.");
            int x = SaturateToInt(
                (long)_bossX + definition.SpawnOffsetX);
            int y = SaturateToInt(
                (long)_bossY + definition.SpawnOffsetY);
            int id = _nextEnemyId++;
            var chain = new SegmentChainRuntime(
                id,
                definition,
                ScaleEnemyHp(definition.HeadMaxHp),
                x,
                y);
            _segmentChainRuntimes.Add(chain);
            EmitEvent(
                SimEventType.SegmentChainSpawned,
                id,
                x,
                y,
                definition.SegmentCount);
        }

        void AdvanceSegmentChain(SegmentChainRuntime chain)
        {
            long desiredX = (long)PlayerX - chain.HeadX;
            long desiredY = (long)PlayerY - chain.HeadY;
            ScaleVectorForProducts(ref desiredX, ref desiredY);
            long cross = (long)chain.DirectionX * desiredY
                - (long)chain.DirectionY * desiredX;
            if (cross != 0)
            {
                int rotation = cross > 0
                    ? chain.Definition.TurnLutSlotsPerTick
                    : -chain.Definition.TurnLutSlotsPerTick;
                RotateVector(
                    chain.DirectionX,
                    chain.DirectionY,
                    rotation,
                    out long turnedX,
                    out long turnedY);
                long currentDot = (long)chain.DirectionX * desiredX
                    + (long)chain.DirectionY * desiredY;
                long turnedDot = turnedX * desiredX
                    + turnedY * desiredY;
                if (turnedDot > currentDot)
                    NormalizeChainDirection(
                        chain,
                        turnedX,
                        turnedY);
            }

            long divisor = checked(
                (long)SegmentChainRuntime.SineDirectionScale
                * chain.Definition.MoveSpeedDenominator);
            long accumulatedX = checked(
                chain.MoveRemainderX
                + (long)chain.DirectionX
                    * chain.Definition.MoveSpeedNumerator);
            long accumulatedY = checked(
                chain.MoveRemainderY
                + (long)chain.DirectionY
                    * chain.Definition.MoveSpeedNumerator);
            chain.HeadX = SaturateToInt(
                (long)chain.HeadX + accumulatedX / divisor);
            chain.HeadY = SaturateToInt(
                (long)chain.HeadY + accumulatedY / divisor);
            chain.MoveRemainderX = accumulatedX % divisor;
            chain.MoveRemainderY = accumulatedY % divisor;
            chain.HistoryHead++;
            if (chain.HistoryHead == chain.HistoryX.Length)
                chain.HistoryHead = 0;
            chain.HistoryX[chain.HistoryHead] = chain.HeadX;
            chain.HistoryY[chain.HistoryHead] = chain.HeadY;
        }

        static void NormalizeChainDirection(
            SegmentChainRuntime chain,
            long x,
            long y)
        {
            long squared = x * x + y * y;
            long length = IntegerSqrt(squared);
            if (length < 1)
                return;
            chain.DirectionX = SaturateToInt(
                x * SegmentChainRuntime.SineDirectionScale / length);
            chain.DirectionY = SaturateToInt(
                y * SegmentChainRuntime.SineDirectionScale / length);
        }

        void RebuildSegmentChainStates()
        {
            _segmentChainStates.Clear();
            for (int chainIndex = 0;
                chainIndex < _segmentChainRuntimes.Count;
                chainIndex++)
            {
                SegmentChainRuntime chain =
                    _segmentChainRuntimes[chainIndex];
                for (int segmentIndex = 0;
                    segmentIndex < chain.Definition.SegmentCount;
                    segmentIndex++)
                {
                    int historyIndex = chain.HistoryHead
                        - segmentIndex
                            * chain.Definition.FollowDelayTicks;
                    while (historyIndex < 0)
                        historyIndex += chain.HistoryX.Length;
                    _segmentChainStates.Add(new SegmentChainState(
                        chain.Id,
                        segmentIndex,
                        chain.HistoryX[historyIndex],
                        chain.HistoryY[historyIndex],
                        chain.HeadHp,
                        chain.HeadMaxHp));
                }
            }
        }

        void DestroyAllSegmentChains()
        {
            for (int i = _segmentChainRuntimes.Count - 1; i >= 0; i--)
            {
                SegmentChainRuntime chain = _segmentChainRuntimes[i];
                EmitEvent(
                    SimEventType.SegmentChainDestroyed,
                    chain.Id,
                    chain.HeadX,
                    chain.HeadY,
                    chain.Definition.SegmentCount);
            }
            _segmentChainRuntimes.Clear();
            _segmentChainStates.Clear();
            _segmentChainSummonsRemaining = 0;
            _segmentChainSummonCooldown = 0;
        }

        int GetBossLeftExtent()
        {
            int extent = _bossHalfWidth;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition part =
                    _bossPartDefinitions[i];
                extent = Math.Max(
                    extent,
                    SaturateToInt(
                        (long)part.HalfWidth - part.OffsetX));
            }
            return Math.Max(0, extent);
        }

        void AdvanceTimedBossPhase()
        {
            if (!_bossUsesTimedPattern)
                return;
            Generation.BossPhase phase =
                _bossPhases[_bossPhase];
            if (_bossPhaseAge < phase.DurationTicks)
                return;
            int nextPhase = (_bossPhase + 1) % _bossPhases.Count;
            EnterBossPhase(nextPhase, true);
        }
    }
}
