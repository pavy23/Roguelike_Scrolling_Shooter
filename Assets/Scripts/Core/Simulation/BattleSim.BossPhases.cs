using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public sealed partial class BattleSim
    {
        void EnterBossPhase(
            int phaseIndex,
            bool emitChanged)
        {
            _bossPhase = phaseIndex;
            _bossPhaseAge = 0;
            Generation.BossPhase phase =
                _bossPhases[phaseIndex];
            ConfigureBossMovementPhase(phase);
            _bossFireCooldown = phase.TelegraphTicks > 0
                ? phase.TelegraphTicks
                : Math.Max(
                    1,
                    Math.Min(
                        _bossFireCooldown,
                        phase.FireIntervalTicks));
            _bossPhaseTelegraphPending =
                phase.TelegraphTicks > 0;
            _bossBurstAwaitingVolley =
                phase.FirePattern == BossFirePattern.Burst
                && phase.TelegraphTicks > 0;
            _bossPatternVolleyIndex = 0;
            RefreshBossPartPositions();
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartAttackProfile attack = GetBossPartAttack(i);
                _bossPartFireCooldowns[i] = attack.IntervalTicks;
                _bossPartSecondaryLaserCooldowns[i] =
                    attack.SecondaryLaser == null
                        ? 0
                        : attack.SecondaryLaser.CycleIntervalTicks;
                _bossPartSecondaryBurstCooldowns[i] =
                    attack.SecondaryBurst == null
                        ? 0
                        : attack.SecondaryBurst.CycleIntervalTicks;
            }
            ConfigureSegmentChainSchedule(phase);
            if (emitChanged)
            {
                EmitEvent(
                    SimEventType.BossPhaseChanged,
                    _bossId,
                    _bossX,
                    _bossY,
                    phaseIndex);
            }
        }

        void EmitPendingBossTelegraph()
        {
            if (!_bossPhaseTelegraphPending)
                return;
            _bossPhaseTelegraphPending = false;
            EmitBossAttackTelegraph(_bossPhases[_bossPhase]);
        }

        void UpdateBossPhaseFire(Generation.BossPhase phase)
        {
            if (_bossFireCooldown > 0)
                _bossFireCooldown--;
            if (_bossFireCooldown != 0)
                return;

            if (phase.FirePattern == BossFirePattern.Burst)
            {
                if (_bossBurstAwaitingVolley)
                {
                    if (phase.ProjectileKind == BossProjectileKind.BossLaser)
                        TryStartLaser(
                            LaserSourceKind.Boss,
                            _bossId,
                            phase.LaserAttack,
                            _bossX,
                            _bossY);
                    else
                        FireAimedBossVolley(phase);
                    FireBossSignature(phase);
                    _bossPatternVolleyIndex++;
                    _bossBurstAwaitingVolley = false;
                    _bossFireCooldown = phase.FireIntervalTicks;
                    return;
                }

                EmitBossAttackTelegraph(phase);
                _bossBurstAwaitingVolley = true;
                _bossFireCooldown = phase.TelegraphTicks;
                return;
            }

            if (phase.ProjectileKind == BossProjectileKind.BossLaser)
            {
                TryStartLaser(
                    LaserSourceKind.Boss,
                    _bossId,
                    phase.LaserAttack,
                    _bossX,
                    _bossY);
            }
            else switch (phase.FirePattern)
            {
                case BossFirePattern.Aimed:
                    FireAimedBossVolley(phase);
                    break;
                case BossFirePattern.Radial:
                    FireRadialBossVolley(phase, 0);
                    break;
                case BossFirePattern.Spiral:
                    FireRadialBossVolley(
                        phase,
                        (_bossPatternVolleyIndex
                            * SpiralStepLutSlots)
                            % SineLut.Length);
                    break;
                case BossFirePattern.Wall:
                    FireWallBossVolley(phase);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss fire pattern.");
            }
            FireBossSignature(phase);
            _bossPatternVolleyIndex++;
            _bossFireCooldown = phase.FireIntervalTicks;
        }

        void FireAimedBossVolley(Generation.BossPhase phase)
        {
            int shots = GetBossVolleyShotCount(phase.Ways);
            for (int i = 0; i < shots; i++)
            {
                long centeredIndex =
                    2L * i - (phase.Ways - 1L);
                int rotation = (int)(
                    (centeredIndex * SpreadStepLutSlots / 2)
                    % SineLut.Length);
                SpawnEnemyAimedBullet(
                    _bossX,
                    _bossY,
                    PlayerX,
                    PlayerY,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    rotation,
                    phase);
            }
        }

        void FireRadialBossVolley(
            Generation.BossPhase phase,
            int baseRotation)
        {
            int shots = GetBossVolleyShotCount(phase.Ways);
            for (int i = 0; i < shots; i++)
            {
                int rotation =
                    (baseRotation
                        + (int)((long)i
                            * SineLut.Length
                            / phase.Ways))
                    % SineLut.Length;
                SpawnEnemyAimedBullet(
                    _bossX,
                    _bossY,
                    _bossX - SineScale,
                    _bossY,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    rotation,
                    phase);
            }
        }

        void FireWallBossVolley(Generation.BossPhase phase)
        {
            int gap = _bossPatternRng.NextInt(0, phase.Ways);
            int requested = phase.Ways - 1;
            int shots = GetBossVolleyShotCount(requested);
            int fired = 0;
            long height = (long)_playerMaxY - _playerMinY;
            for (int lane = 0;
                lane < phase.Ways && fired < shots;
                lane++)
            {
                if (lane == gap)
                    continue;
                int y = (int)(_playerMinY
                    + height * lane / (phase.Ways - 1));
                SpawnEnemyAimedBullet(
                    _bossX,
                    y,
                    _playerMinX,
                    y,
                    phase.BulletSpeedNumerator,
                    phase.BulletSpeedDenominator,
                    0,
                    phase);
                fired++;
            }
        }

        void FireBossSignature(Generation.BossPhase phase)
        {
            switch (phase.SignaturePattern)
            {
                case BossSignaturePattern.None:
                    return;
                case BossSignaturePattern.ScrapThrow:
                    TrySpawnBossScrap(phase);
                    return;
                case BossSignaturePattern.Brood:
                    SpawnBossEnemy(
                        _battleContent.FindEnemy(phase.SignatureSpawnEnemyId),
                        _bossX,
                        _bossY);
                    return;
                case BossSignaturePattern.LaserGrid:
                    TryStartLaser(
                        LaserSourceKind.Boss,
                        _bossId,
                        phase.LaserAttack,
                        _bossX,
                        _bossY);
                    TryStartLaser(
                        LaserSourceKind.Boss,
                        _bossId,
                        MirrorLaserVertically(phase.LaserAttack),
                        _bossX,
                        _bossY);
                    return;
                case BossSignaturePattern.Lightning:
                    TryStartLaser(
                        LaserSourceKind.Boss,
                        _bossId,
                        OffsetLaserX(
                            phase.LaserAttack,
                            PlayerX - _bossX),
                        _bossX,
                        _bossY);
                    return;
                case BossSignaturePattern.PrismCore:
                    FirePrismLasers(phase.LaserAttack);
                    return;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss signature pattern.");
            }
        }

        void FirePrismLasers(LaserAttackDefinition source)
        {
            int rotation = (_bossPatternVolleyIndex * SpiralStepLutSlots)
                % SineLut.Length;
            RotateVector(
                source.EndOffsetX - source.StartOffsetX,
                source.EndOffsetY - source.StartOffsetY,
                rotation,
                out long endX,
                out long endY);
            LaserAttackDefinition first = CloneLaser(
                source,
                source.StartOffsetX,
                source.StartOffsetY,
                SaturateToInt((long)source.StartOffsetX + endX),
                SaturateToInt((long)source.StartOffsetY + endY));
            LaserAttackDefinition second = CloneLaser(
                source,
                source.StartOffsetX,
                source.StartOffsetY,
                SaturateToInt((long)source.StartOffsetX - endX),
                SaturateToInt((long)source.StartOffsetY - endY));
            TryStartLaser(
                LaserSourceKind.Boss,
                _bossId,
                first,
                _bossX,
                _bossY);
            TryStartLaser(
                LaserSourceKind.Boss,
                _bossId,
                second,
                _bossX,
                _bossY);
        }

        /// <summary>
        /// 발사구에서 플레이어를 향하도록 끝 오프셋을 돌린다 (사람 지시 2026-08-04:
        /// "처음 주인공 기체 위치를 서칭한번 하고 그 쪽으로 발사").
        ///
        /// 길이는 의미가 없다 — BattleSim이 어차피 화면 끝까지 늘린다. 여기서
        /// 정하는 것은 **방향**뿐이므로, 원래 길이를 그대로 쓰되 방향만 바꾼다.
        /// 플레이어가 발사구와 같은 자리면 돌릴 방향이 없으니 원본을 둔다.
        /// </summary>
        LaserAttackDefinition AimLaserAtPlayer(
            LaserAttackDefinition definition,
            int sourceX,
            int sourceY)
        {
            long muzzleX = (long)sourceX + definition.StartOffsetX;
            long muzzleY = (long)sourceY + definition.StartOffsetY;
            long toPlayerX = PlayerX - muzzleX;
            long toPlayerY = PlayerY - muzzleY;
            if (toPlayerX == 0 && toPlayerY == 0)
                return definition;

            long spanX = (long)definition.EndOffsetX - definition.StartOffsetX;
            long spanY = (long)definition.EndOffsetY - definition.StartOffsetY;
            long reach = IntegerLength(spanX, spanY);
            if (reach <= 0)
                return definition;

            long distance = IntegerLength(toPlayerX, toPlayerY);
            if (distance <= 0)
                return definition;

            // 방향 단위벡터를 원래 사거리로 되돌린다 (정수 나눗셈 = 절삭, 결정론).
            long endX = definition.StartOffsetX + toPlayerX * reach / distance;
            long endY = definition.StartOffsetY + toPlayerY * reach / distance;
            if (endX == definition.StartOffsetX && endY == definition.StartOffsetY)
                return definition;   // 생성자가 같은 끝점을 거부한다

            return CloneLaser(
                definition,
                definition.StartOffsetX,
                definition.StartOffsetY,
                SaturateToInt(endX),
                SaturateToInt(endY));
        }

        /// <summary>정수 벡터 길이 (뉴턴법 제곱근). float를 쓰지 않는다 — AGENTS.md §4.</summary>
        static long IntegerLength(long x, long y)
        {
            long squared = x * x + y * y;
            if (squared <= 0) return 0;
            long guess = squared;
            long next = (guess + 1) / 2;
            while (next < guess)
            {
                guess = next;
                next = (guess + squared / guess) / 2;
            }
            return guess;
        }

        static LaserAttackDefinition MirrorLaserVertically(
            LaserAttackDefinition source)
        {
            return CloneLaser(
                source,
                source.StartOffsetX,
                -source.StartOffsetY,
                source.EndOffsetX,
                -source.EndOffsetY);
        }

        static LaserAttackDefinition OffsetLaserX(
            LaserAttackDefinition source,
            int offsetX)
        {
            return CloneLaser(
                source,
                SaturateToInt((long)source.StartOffsetX + offsetX),
                source.StartOffsetY,
                SaturateToInt((long)source.EndOffsetX + offsetX),
                source.EndOffsetY);
        }

        static LaserAttackDefinition CloneLaser(
            LaserAttackDefinition source,
            int startX,
            int startY,
            int endX,
            int endY)
        {
            return new LaserAttackDefinition(
                source.CycleIntervalTicks,
                source.TelegraphTicks,
                source.FiringTicks,
                source.SustainTicks,
                source.DissipateTicks,
                startX,
                startY,
                endX,
                endY,
                source.ThinHalfWidth,
                source.FullHalfWidth,
                source.Damage,
                source.AimsAtPlayer);
        }

        void TrySpawnBossScrap(Generation.BossPhase phase)
        {
            if (_obstacles.Count + _pendingObstacleRegens.Count
                >= _maxObstacles)
            {
                EmitEvent(
                    SimEventType.ObstacleCapacityExceeded,
                    _bossId,
                    _bossX,
                    _bossY,
                    _maxObstacles);
                return;
            }
            if (_nextObstacleId == int.MaxValue)
                throw new InvalidOperationException(
                    "The obstacle id counter is exhausted.");
            _obstacles.Add(new ObstacleState(
                _nextObstacleId++,
                ObstacleType.Breakable,
                _bossX,
                _bossY,
                phase.SignatureObstacleHp));
            _obstacleAges.Add(0);
            _obstacleLaserAttacks.Add(null);
            _obstacleBlocksEnemyBullets.Add(false);
            _obstacleRegenDelayTicks.Add(0);
            _obstacleMaxHps.Add(phase.SignatureObstacleHp);
            _obstacleMotionXRemainders.Add(0);
            _obstacleMotionYRemainders.Add(0);
            _obstacleVelocityXNumerators.Add(
                -phase.BulletSpeedNumerator);
            _obstacleVelocityYNumerators.Add(
                phase.BulletSpeedNumerator);
            _obstacleVelocityDenominators.Add(
                phase.BulletSpeedDenominator);
            _obstacleGravityNumerators.Add(
                phase.SignatureGravityNumerator);
            _obstacleGravityDenominators.Add(
                phase.SignatureGravityDenominator);
        }

        int GetBossVolleyShotCount(int requested)
        {
            int available = Math.Max(
                0,
                _maxEnemyBullets - CountEnemyBullets());
            if (requested > available)
            {
                EmitEvent(
                    SimEventType.EnemyBulletCapacityExceeded,
                    _bossId,
                    _bossX,
                    _bossY,
                    _maxEnemyBullets);
            }
            return Math.Min(requested, available);
        }

        void ApplyBossPhaseMovement(
            Generation.BossPhase phase,
            bool legacyVerticalMovementActive)
        {
            int previousX = _bossX;
            int previousY = _bossY;
            int transitionOffsetX =
                GetBossMovementTransitionOffset(
                    _bossMovementTransitionOffsetX);
            int transitionOffsetY =
                GetBossMovementTransitionOffset(
                    _bossMovementTransitionOffsetY);
            int tick = _bossPhaseAge
                + _bossMovementPhaseOffsetTicks;
            if (phase.MovementPattern == BossMovementPattern.LungeReturn
                && PositiveModulo(tick, phase.MovementPeriodTicks) == 0)
            {
                EmitEvent(
                    SimEventType.BossMovementTelegraphed,
                    _bossId,
                    _bossHoldX,
                    _bossMovementAnchorY,
                    phase.MovementTelegraphTicks);
            }
            _bossX = SaturateToInt(
                (long)_bossHoldX
                + ComputeMovementOffsetX(phase, tick)
                + transitionOffsetX);
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.LegacyHover:
                {
                    if (_bossPartDefinitions.Count > 0
                        && !legacyVerticalMovementActive)
                    {
                        _bossY = SaturateToInt(
                            (long)_bossMovementAnchorY
                            + transitionOffsetY);
                        break;
                    }
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + ComputeLegacyHoverOffset(tick)
                        + transitionOffsetY);
                    break;
                }
                case BossMovementPattern.Stationary:
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + transitionOffsetY);
                    break;
                case BossMovementPattern.VerticalSine:
                case BossMovementPattern.FigureEight:
                {
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + ComputeMovementOffsetY(phase, tick)
                        + transitionOffsetY);
                    break;
                }
                case BossMovementPattern.LungeReturn:
                {
                    _bossY = SaturateToInt(
                        (long)_bossMovementAnchorY
                        + transitionOffsetY);
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unknown boss movement pattern "
                        + $"{phase.MovementPattern}.");
            }
            _bossVelocityX = SaturateToInt(
                (long)_bossX - previousX);
            _bossVelocityY = SaturateToInt(
                (long)_bossY - previousY);
        }

        void ConfigureBossMovementPhase(
            Generation.BossPhase phase)
        {
            int currentOffsetX = SaturateToInt(
                (long)_bossX - _bossHoldX);
            int currentOffsetY = SaturateToInt(
                (long)_bossY - _bossMovementAnchorY);
            int phaseOffset = FindClosestMovementPhase(
                phase,
                SaturateToInt(
                    (long)currentOffsetX + _bossVelocityX),
                SaturateToInt(
                    (long)currentOffsetY + _bossVelocityY),
                _bossVelocityX,
                _bossVelocityY);
            _bossMovementPhaseOffsetTicks = phaseOffset;
            _bossMovementTransitionOffsetX = SaturateToInt(
                (long)currentOffsetX
                + _bossVelocityX
                - ComputeMovementOffsetX(
                    phase,
                    phaseOffset));
            _bossMovementTransitionOffsetY = SaturateToInt(
                (long)currentOffsetY
                + _bossVelocityY
                - ComputeMovementOffsetY(
                    phase,
                    phaseOffset));
        }

        static int FindClosestMovementPhase(
            Generation.BossPhase phase,
            int targetOffsetX,
            int targetOffsetY,
            int velocityX,
            int velocityY)
        {
            int period;
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                    return 0;
                case BossMovementPattern.LegacyHover:
                    period =
                        SineLut.Length << BossHoverPeriodShift;
                    break;
                case BossMovementPattern.VerticalSine:
                case BossMovementPattern.LungeReturn:
                case BossMovementPattern.FigureEight:
                    period = phase.MovementPeriodTicks;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }

            int bestTick = 0;
            long bestPositionError = long.MaxValue;
            long bestVelocityError = long.MaxValue;
            for (int tick = 0; tick < period; tick++)
            {
                int offsetX = ComputeMovementOffsetX(phase, tick);
                int offsetY = ComputeMovementOffsetY(phase, tick);
                long candidateVelocityX =
                    (long)ComputeMovementOffsetX(phase, tick + 1)
                    - offsetX;
                long candidateVelocityY =
                    (long)ComputeMovementOffsetY(phase, tick + 1)
                    - offsetY;
                long positionError =
                    Math.Abs((long)offsetX - targetOffsetX)
                    + Math.Abs((long)offsetY - targetOffsetY);
                long velocityError =
                    Math.Abs(candidateVelocityX - velocityX)
                    + Math.Abs(candidateVelocityY - velocityY);
                if (velocityError < bestVelocityError
                    || (velocityError == bestVelocityError
                        && positionError < bestPositionError))
                {
                    bestPositionError = positionError;
                    bestVelocityError = velocityError;
                    bestTick = tick;
                }
            }
            return bestTick;
        }

        int GetBossMovementTransitionOffset(int transitionOffset)
        {
            if (transitionOffset == 0
                || _bossPhaseAge >= BossMovementRecenterTicks)
                return 0;
            int remaining =
                BossMovementRecenterTicks - _bossPhaseAge;
            return SaturateToInt(
                (long)transitionOffset
                * remaining
                / BossMovementRecenterTicks);
        }

        static int ComputeMovementOffsetX(
            Generation.BossPhase phase,
            int tick)
        {
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                case BossMovementPattern.LegacyHover:
                case BossMovementPattern.VerticalSine:
                    return 0;
                case BossMovementPattern.LungeReturn:
                    return ComputeLungeReturnOffsetX(phase, tick);
                case BossMovementPattern.FigureEight:
                    return ComputeFigureEightOffsetX(phase, tick);
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }
        }

        static int ComputeMovementOffsetY(
            Generation.BossPhase phase,
            int tick)
        {
            switch (phase.MovementPattern)
            {
                case BossMovementPattern.Stationary:
                case BossMovementPattern.LungeReturn:
                    return 0;
                case BossMovementPattern.LegacyHover:
                    return ComputeLegacyHoverOffset(tick);
                case BossMovementPattern.VerticalSine:
                case BossMovementPattern.FigureEight:
                    return ComputeVerticalSineOffset(phase, tick);
                default:
                    throw new InvalidOperationException(
                        "Unknown boss movement pattern.");
            }
        }

        static int ComputeLungeReturnOffsetX(
            Generation.BossPhase phase,
            int tick)
        {
            int phaseTick = PositiveModulo(
                tick,
                phase.MovementPeriodTicks);
            if (phaseTick < phase.MovementTelegraphTicks)
                return 0;

            int movementTicks = phase.MovementPeriodTicks
                - phase.MovementTelegraphTicks;
            int elapsed = phaseTick - phase.MovementTelegraphTicks;
            int outboundTicks = movementTicks / 2;
            int returnTicks = movementTicks - 1 - outboundTicks;
            long numerator;
            long denominator;
            if (elapsed <= outboundTicks)
            {
                numerator = (long)phase.MovementAmplitudeNumerator
                    * elapsed;
                denominator = (long)phase.MovementAmplitudeDenominator
                    * outboundTicks;
            }
            else
            {
                numerator = (long)phase.MovementAmplitudeNumerator
                    * (movementTicks - 1 - elapsed);
                denominator = (long)phase.MovementAmplitudeDenominator
                    * returnTicks;
            }
            return -SaturateToInt(numerator / denominator);
        }

        static int ComputeFigureEightOffsetX(
            Generation.BossPhase phase,
            int tick)
        {
            int phaseTick = PositiveModulo(
                tick,
                phase.MovementPeriodTicks);
            int lutIndex = (int)(
                (long)phaseTick * SineLut.Length
                / phase.MovementPeriodTicks);
            int doubledIndex = (lutIndex * 2) % SineLut.Length;
            long numerator =
                (long)phase.MovementAmplitudeNumerator
                * SineLut[doubledIndex];
            long denominator =
                (long)phase.MovementAmplitudeDenominator
                * SineScale
                * 2;
            return SaturateToInt(numerator / denominator);
        }

        static int ComputeLegacyHoverOffset(int tick)
        {
            int period =
                SineLut.Length << BossHoverPeriodShift;
            int normalized = PositiveModulo(tick, period);
            int legacyIndex =
                (normalized >> BossHoverPeriodShift)
                % SineLut.Length;
            return SaturateToInt(
                (long)BossHoverAmplitude
                * SineLut[legacyIndex]
                / SineScale);
        }

        static int ComputeVerticalSineOffset(
            Generation.BossPhase phase,
            int tick)
        {
            int phaseTick = PositiveModulo(
                tick,
                phase.MovementPeriodTicks);
            int lutIndex = (int)(
                (long)phaseTick * SineLut.Length
                / phase.MovementPeriodTicks);
            long numerator =
                (long)phase.MovementAmplitudeNumerator
                * SineLut[lutIndex];
            long denominator =
                (long)phase.MovementAmplitudeDenominator
                * SineScale;
            return SaturateToInt(numerator / denominator);
        }

        static int PositiveModulo(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0
                ? remainder + modulus
                : remainder;
        }

        void InitializeBossParts()
        {
            int aggregateHp = 0;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartDefinition definition =
                    _bossPartDefinitions[i];
                int maxHp = ScaleEnemyHp(definition.MaxHp);
                aggregateHp = SaturatingAddDamage(aggregateHp, maxHp);
                BossPartAttackProfile attack = GetBossPartAttack(i);
                _bossPartFireCooldowns[i] = attack.IntervalTicks;
                _bossPartSecondaryLaserCooldowns[i] =
                    attack.SecondaryLaser == null
                        ? 0
                        : attack.SecondaryLaser.CycleIntervalTicks;
                _bossPartSecondaryBurstCooldowns[i] =
                    attack.SecondaryBurst == null
                        ? 0
                        : attack.SecondaryBurst.CycleIntervalTicks;
                _bossPartRegenerationRemaining[i] = 0;
                _bossPartContactHitThisCycle[i] = false;
                _bossPartStates[i] = new BossPartState(
                    definition.PartId,
                    SaturateToInt((long)_bossX + definition.OffsetX),
                    SaturateToInt((long)_bossY + definition.OffsetY),
                    maxHp,
                    maxHp,
                    false,
                    IsBossPartActive(i),
                    definition.IsCore,
                    false);
            }
            if (_bossPartDefinitions.Count > 0)
            {
                _bossHp = aggregateHp;
                RefreshBossPartPositions();
            }
        }

        void UpdateMultipartBoss(Generation.BossPhase phase)
        {
            RegenerateBossParts();

            bool verticalMovementActive = false;
            int chargeOffset = 0;
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                if (_bossPartStates[i].Destroyed
                    || !IsBossPartActive(i))
                    continue;
                BossPartAttackProfile attack =
                    GetBossPartAttack(i);
                if (attack.Type == BossPartAttackType.VerticalMovement)
                    verticalMovementActive = true;
                else if (attack.Type == BossPartAttackType.MeleeCharge)
                {
                    // 예고 → 돌진. 예고 길이가 0이면 예전처럼 곧장 밀고 들어온다.
                    //
                    // 예고 구간에는 **움직이지 않는다.** 그 자리에 서서 번쩍이는
                    // 것이 예고이고, 움직이면서 알리는 것은 예고가 아니라 통보다.
                    int cycle = _bossAge % attack.IntervalTicks;
                    int telegraph = attack.MeleeTelegraphTicks;
                    if (telegraph > 0 && cycle == 0)
                    {
                        BossPartState telegraphing = _bossPartStates[i];
                        EmitBossPartEvent(
                            SimEventType.BossPartMeleeTelegraphed,
                            telegraphing.X,
                            telegraphing.Y,
                            telegraph,
                            i);
                    }
                    int chargeCycle = MeleeChargeCycle(
                        attack, _bossAge, out int chargeTicks);
                    if (chargeCycle >= 0 && chargeCycle < chargeTicks)
                    {
                        chargeOffset = Math.Max(
                            chargeOffset,
                            AdvancePositiveFraction(
                                chargeCycle,
                                attack.EffectSpeedNumerator,
                                attack.EffectSpeedDenominator));
                    }
                }
            }

            _bossX = SaturateToInt(
                (long)_bossHoldX - chargeOffset);
            ApplyBossPhaseMovement(
                phase,
                verticalMovementActive);
            RefreshBossPartPositions();

            UpdateActiveBossPartAttacks();
        }

        void UpdateActiveBossPartAttacks()
        {
            PrimeWarshipMovingFireCooldowns();
            for (int i = 0; i < _bossPartDefinitions.Count; i++)
            {
                BossPartState state = _bossPartStates[i];
                if (state.Destroyed
                    || !state.Active
                    || IsBossPartInvulnerable(i))
                    continue;
                // 이동 중인 전함은 **앞쪽 포탑만** 쏜다 (사람 지시 2026-08-05).
                if (IsWarshipPartHoldingFireWhileMoving(i))
                    continue;
                BossPartAttackProfile attack =
                    GetBossPartAttack(i);
                switch (attack.Type)
                {
                    case BossPartAttackType.None:
                    case BossPartAttackType.VerticalMovement:
                        break;
                    case BossPartAttackType.MeleeCharge:
                        ApplyBossMeleeContact(i, attack);
                        break;
                    case BossPartAttackType.Suction:
                        break;
                    default:
                        if (_bossPartFireCooldowns[i] > 0)
                            _bossPartFireCooldowns[i]--;
                        if (_bossPartFireCooldowns[i] == 0)
                        {
                            FireBossPartAttack(i, attack);
                            _bossPartFireCooldowns[i] =
                                attack.IntervalTicks;
                        }
                        break;
                }
                // REQ-175: secondary laser is independent of primary type/cooldown.
                if (attack.SecondaryLaser != null)
                {
                    if (_bossPartSecondaryLaserCooldowns[i] > 0)
                        _bossPartSecondaryLaserCooldowns[i]--;
                    if (_bossPartSecondaryLaserCooldowns[i] == 0)
                    {
                        TryStartLaser(
                            LaserSourceKind.BossPart,
                            i,
                            attack.SecondaryLaser,
                            state.X,
                            state.Y);
                        _bossPartSecondaryLaserCooldowns[i] =
                            attack.SecondaryLaser.CycleIntervalTicks;
                    }
                }
                // REQ-177: 부무장 탄막도 주 공격과 독립된 주기로 돈다. 이동 중
                // 억제는 위쪽 hold 검사가 이미 걸러 주므로 여기서 다시 보지 않는다
                // — 뒤쪽 포탑은 이동 중 레이저도 탄막도 쉰다.
                if (attack.SecondaryBurst != null)
                {
                    if (_bossPartSecondaryBurstCooldowns[i] > 0)
                        _bossPartSecondaryBurstCooldowns[i]--;
                    if (_bossPartSecondaryBurstCooldowns[i] == 0)
                    {
                        FireBossPartBurst(i, attack.SecondaryBurst);
                        _bossPartSecondaryBurstCooldowns[i] =
                            attack.SecondaryBurst.CycleIntervalTicks;
                    }
                }
            }
        }

        /// <summary>
        /// 부무장 탄막 한 발. 주 공격의 탄막 경로와 같은 규칙(탄 상한, 부채꼴
        /// 각도, 전방위 분할)을 쓰되 발사원은 그 파츠다.
        /// </summary>
        void FireBossPartBurst(
            int partIndex,
            BossPartBurstDefinition burst)
        {
            BossPartState part = _bossPartStates[partIndex];
            int available = Math.Max(
                0,
                _maxEnemyBullets - CountEnemyBullets());
            int shots = Math.Min(burst.Ways, available);
            for (int i = 0; i < shots; i++)
            {
                int targetX = PlayerX;
                int targetY = PlayerY;
                int rotation;
                if (burst.Aimed)
                {
                    long centeredIndex = 2L * i - (burst.Ways - 1L);
                    rotation = (int)(
                        (centeredIndex * SpreadStepLutSlots / 2)
                        % SineLut.Length);
                }
                else
                {
                    rotation = (int)(
                        (long)i * SineLut.Length / burst.Ways);
                    int sin = SineLut[rotation];
                    int cos = SineLut[
                        (rotation + SineLut.Length / 4)
                        % SineLut.Length];
                    targetX = SaturateToInt((long)part.X + cos);
                    targetY = SaturateToInt((long)part.Y + sin);
                    rotation = 0;
                }
                SpawnEnemyAimedBullet(
                    part.X,
                    part.Y,
                    targetX,
                    targetY,
                    burst.BulletSpeedNumerator,
                    burst.BulletSpeedDenominator,
                    rotation);
            }
        }

        void BeginWarshipTick()
        {
            _warshipEncounter.BeginTick();
            _warshipEventCursor = 0;
            SyncWarshipPositionAndVulnerability();
        }
    }
}
