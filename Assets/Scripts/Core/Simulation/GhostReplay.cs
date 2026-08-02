using System;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Immutable balance and storage limits for the stage-one time-loop ghost.
    /// The ghost always uses a straight primary shot; options, missiles, bombs,
    /// gauge activation, collision, and incoming damage are intentionally absent.
    /// </summary>
    public sealed class GhostReplayConfig
    {
        public const int DefaultFixedWeaponLevel = 1;
        public const int DefaultFireIntervalTicks = 8;
        public const int DefaultMaximumInputRuns =
            10 * 60 * SimSpace.TicksPerSecond;

        public GhostReplayConfig(
            int fixedWeaponLevel = DefaultFixedWeaponLevel,
            int fireIntervalTicks = DefaultFireIntervalTicks,
            int maximumInputRuns = DefaultMaximumInputRuns)
        {
            if (fixedWeaponLevel < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(fixedWeaponLevel));
            if (fireIntervalTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(fireIntervalTicks));
            if (maximumInputRuns < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maximumInputRuns));

            FixedWeaponLevel = fixedWeaponLevel;
            FireIntervalTicks = fireIntervalTicks;
            MaximumInputRuns = maximumInputRuns;
        }

        public int FixedWeaponLevel { get; }
        public int FireIntervalTicks { get; }
        public int MaximumInputRuns { get; }

        public static GhostReplayConfig CreateDefault()
        {
            return new GhostReplayConfig();
        }
    }

    /// <summary>
    /// Stage-one tick-zero state required to turn recorded input into a trajectory.
    /// Every value is integer simulation data and is retained with the recording.
    /// </summary>
    public readonly struct GhostRecordingStartState
    {
        public GhostRecordingStartState(
            int x,
            int y,
            int speedNumerator,
            int speedDenominator,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            if (speedNumerator < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(speedNumerator));
            if (speedDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(speedDenominator));
            if (minimumX > maximumX)
                throw new ArgumentOutOfRangeException(nameof(minimumX));
            if (minimumY > maximumY)
                throw new ArgumentOutOfRangeException(nameof(minimumY));
            if (x < minimumX || x > maximumX)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (y < minimumY || y > maximumY)
                throw new ArgumentOutOfRangeException(nameof(y));

            X = x;
            Y = y;
            SpeedNumerator = speedNumerator;
            SpeedDenominator = speedDenominator;
            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumY = minimumY;
            MaximumY = maximumY;
        }

        public int X { get; }
        public int Y { get; }
        public int SpeedNumerator { get; }
        public int SpeedDenominator { get; }
        public int MinimumX { get; }
        public int MaximumX { get; }
        public int MinimumY { get; }
        public int MaximumY { get; }
    }

    /// <summary>
    /// Complete observable ghost state. Remainders are exposed so determinism
    /// audits include the fractional movement state that affects future ticks.
    /// </summary>
    public readonly struct GhostState
    {
        public GhostState(
            bool active,
            int entityId,
            int x,
            int y,
            bool isFiring,
            int playbackTick,
            int ticksUntilNextShot,
            long movementRemainderX,
            long movementRemainderY)
        {
            Active = active;
            EntityId = entityId;
            X = x;
            Y = y;
            IsFiring = isFiring;
            PlaybackTick = playbackTick;
            TicksUntilNextShot = ticksUntilNextShot;
            MovementRemainderX = movementRemainderX;
            MovementRemainderY = movementRemainderY;
        }

        public bool Active { get; }
        public int EntityId { get; }
        public int X { get; }
        public int Y { get; }
        public bool IsFiring { get; }
        public int PlaybackTick { get; }
        public int TicksUntilNextShot { get; }
        public long MovementRemainderX { get; }
        public long MovementRemainderY { get; }
    }

    /// <summary>
    /// Pure integer movement used by ghost playback. It mirrors BattleSim's
    /// digital diagonal normalization and analog speed clamp, but deliberately
    /// ignores stage-five hazards because the ghost has no collision body.
    /// </summary>
    public static class GhostReplayMotion
    {
        const int DigitalDirectionScale = 65_536;
        const int DigitalDiagonalComponent = 46_340;

        public static GhostState Advance(
            in GhostState state,
            in GhostRecordingStartState start,
            in InputCommand input)
        {
            if (!state.Active)
                return state;

            int x;
            int y;
            long remainderX = state.MovementRemainderX;
            long remainderY = state.MovementRemainderY;
            if (input.UseAnalogMovement)
            {
                ClampAnalogDelta(
                    input.AnalogDeltaXSubUnits,
                    input.AnalogDeltaYSubUnits,
                    start.SpeedNumerator,
                    start.SpeedDenominator,
                    out int deltaX,
                    out int deltaY);
                x = ClampAxis(
                    state.X,
                    deltaX,
                    start.MinimumX,
                    start.MaximumX);
                y = ClampAxis(
                    state.Y,
                    deltaY,
                    start.MinimumY,
                    start.MaximumY);
            }
            else
            {
                int componentScale =
                    input.MoveX != 0 && input.MoveY != 0
                        ? DigitalDiagonalComponent
                        : DigitalDirectionScale;
                x = AdvanceDigitalAxis(
                    state.X,
                    input.MoveX,
                    componentScale,
                    start.SpeedNumerator,
                    start.SpeedDenominator,
                    ref remainderX,
                    start.MinimumX,
                    start.MaximumX);
                y = AdvanceDigitalAxis(
                    state.Y,
                    input.MoveY,
                    componentScale,
                    start.SpeedNumerator,
                    start.SpeedDenominator,
                    ref remainderY,
                    start.MinimumY,
                    start.MaximumY);
            }

            return new GhostState(
                true,
                state.EntityId,
                x,
                y,
                false,
                state.PlaybackTick,
                state.TicksUntilNextShot,
                remainderX,
                remainderY);
        }

        static int AdvanceDigitalAxis(
            int position,
            int direction,
            int componentScale,
            int speedNumerator,
            int speedDenominator,
            ref long remainder,
            int minimum,
            int maximum)
        {
            if (direction == 0)
                return position;
            long divisor =
                (long)speedDenominator * DigitalDirectionScale;
            long accumulated = remainder
                + (long)direction * speedNumerator * componentScale;
            long candidate = position + accumulated / divisor;
            long nextRemainder = accumulated % divisor;
            if (direction < 0 && candidate <= minimum)
            {
                remainder = 0;
                return minimum;
            }
            if (direction > 0 && candidate >= maximum)
            {
                remainder = 0;
                return maximum;
            }
            remainder = nextRemainder;
            return (int)candidate;
        }

        static void ClampAnalogDelta(
            int requestedX,
            int requestedY,
            int speedNumerator,
            int speedDenominator,
            out int deltaX,
            out int deltaY)
        {
            if ((requestedX == 0 && requestedY == 0)
                || speedNumerator == 0)
            {
                deltaX = 0;
                deltaY = 0;
                return;
            }

            ulong absoluteX = AbsoluteAsUnsigned(requestedX);
            ulong absoluteY = AbsoluteAsUnsigned(requestedY);
            ulong lengthSquared =
                absoluteX * absoluteX + absoluteY * absoluteY;
            ulong speed = (ulong)speedNumerator;
            ulong denominator = (ulong)speedDenominator;
            ulong maximumLengthSquared =
                speed * speed / (denominator * denominator);
            if (lengthSquared <= maximumLengthSquared)
            {
                deltaX = requestedX;
                deltaY = requestedY;
                return;
            }

            ulong lengthCeiling = IntegerSquareRoot(lengthSquared);
            if (lengthCeiling * lengthCeiling < lengthSquared)
                lengthCeiling++;
            long divisor = (long)speedDenominator * (long)lengthCeiling;
            deltaX = (int)((long)requestedX * speedNumerator / divisor);
            deltaY = (int)((long)requestedY * speedNumerator / divisor);
        }

        static int ClampAxis(
            int position,
            int delta,
            int minimum,
            int maximum)
        {
            long candidate = (long)position + delta;
            if (candidate <= minimum)
                return minimum;
            if (candidate >= maximum)
                return maximum;
            return (int)candidate;
        }

        static ulong AbsoluteAsUnsigned(int value)
        {
            return value < 0 ? (ulong)(-(long)value) : (ulong)value;
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
    }
}
