using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    public interface IBattleSim
    {
        int Tick { get; }
        /// <summary>Score earned in this battle instance.</summary>
        long Score { get; }
        /// <summary>Zero-based combo level: 0, 1, 2, or 3.</summary>
        int MultiplierLevel { get; }
        int ScoreMultiplier { get; }
        int ComboGauge { get; }
        int TicksSinceLastKill { get; }
        BattleStatistics Statistics { get; }
        long ScrollX { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        bool IsPlayerAlive { get; }
        int ShieldStock { get; }
        int BombStock { get; }
        int PlayerInvulnerabilityTicksRemaining { get; }
        /// <summary>
        /// Compatibility health flag: one while alive, zero after the lethal
        /// unshielded hit. It is no longer a multi-point hull resource.
        /// </summary>
        int PlayerHp { get; }
        /// <summary>Compatibility alias for ShieldStock.</summary>
        int ShieldRemaining { get; }
        WeaponType PlayerWeaponType { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        IReadOnlyList<OptionState> Options { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        IReadOnlyList<SegmentChainState> SegmentChains { get; }
        /// <summary>
        /// Stable read-only active-obstacle view. Only obstacles explicitly marked
        /// BlocksEnemyBullets erase hostile projectiles.
        /// </summary>
        IReadOnlyList<ObstacleState> Obstacles { get; }
        IReadOnlyList<CapsuleState> Capsules { get; }
        IReadOnlyList<BombPickupState> BombPickups { get; }
        IReadOnlyList<LaserState> Lasers { get; }
        StageEnvironmentState Environment { get; }
        bool VisionObscured { get; }
        int TimeLimitTicks { get; }
        int RemainingTimeTicks { get; }
        bool TimeLimitExpired { get; }
        /// <summary>Events emitted by the most recent Step. Cleared at the start of each Step.</summary>
        ReadOnlySpan<SimEvent> EventsThisTick { get; }
        /// <summary>보스전 진행 중 여부. false면 Boss 값은 무의미하다.</summary>
        bool BossActive { get; }
        /// <summary>
        /// True while the boss is gliding from fully off-screen to its combat
        /// hold point. Entry is non-firing and invulnerable.
        /// </summary>
        bool BossEntering { get; }
        bool BossTransitioning { get; }
        int BossTransitionTicksRemaining { get; }
        /// <summary>
        /// Zero for the original body, one for form2. RunSuspendData remains a
        /// room-boundary restart; deterministic input replay reconstructs this
        /// value, the phase index, and the transition countdown at any tick.
        /// </summary>
        int BossFormIndex { get; }
        BossState Boss { get; }
        /// <summary>Stable allocation-free view of multipart boss state.</summary>
        IReadOnlyList<BossPartState> BossParts { get; }
        bool SuctionActive { get; }
        /// <summary>-1 during WARNING or when this is not a warship battle.</summary>
        int WarshipActiveGroupIndex { get; }
        int WarshipDestroyedAttritionParts { get; }
        int WarshipCoreOpeningWays { get; }
        /// <summary>Vertical staging offset of the hull, in sub-units. 0 off-warship.</summary>
        int WarshipAnchorOffsetY { get; }
        /// <summary>How far this act's vertical move has run, in thousandths.</summary>
        int WarshipAnchorTravelPermille { get; }
        void Step(in InputCommand input);
    }

    /// <summary>
    /// Deterministic state intentionally carried across combat-room boundaries
    /// within one biome. Transient entities and attack cooldowns are excluded.
    /// </summary>
    public sealed class BattleContinuityState
    {
        public BattleContinuityState(
            int playerX,
            int playerY,
            int multiplierLevel,
            int comboGauge,
            int ticksSinceLastKill)
            : this(
                playerX,
                playerY,
                multiplierLevel,
                comboGauge,
                ticksSinceLastKill,
                0L)
        {
        }

        public BattleContinuityState(
            int playerX,
            int playerY,
            int multiplierLevel,
            int comboGauge,
            int ticksSinceLastKill,
            long scrollX)
        {
            if (multiplierLevel < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(multiplierLevel));
            if (comboGauge < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(comboGauge));
            if (ticksSinceLastKill < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(ticksSinceLastKill));
            if (scrollX < 0)
                throw new ArgumentOutOfRangeException(nameof(scrollX));
            PlayerX = playerX;
            PlayerY = playerY;
            MultiplierLevel = multiplierLevel;
            ComboGauge = comboGauge;
            TicksSinceLastKill = ticksSinceLastKill;
            ScrollX = scrollX;
        }

        public int PlayerX { get; }
        public int PlayerY { get; }
        public int MultiplierLevel { get; }
        public int ComboGauge { get; }
        public int TicksSinceLastKill { get; }
        /// <summary>Absolute parallax scroll at the captured room boundary.</summary>
        public long ScrollX { get; }
    }
}
