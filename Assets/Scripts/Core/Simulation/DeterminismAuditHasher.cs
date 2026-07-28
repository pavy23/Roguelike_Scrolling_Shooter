using System;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Stable FNV-1a fold for the per-tick state used by determinism audits.
    /// Values are folded byte-by-byte in an explicit little-endian order.
    /// </summary>
    public sealed class DeterminismAuditHasher
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        ulong _hash = OffsetBasis;

        public ulong Hash => _hash;
        public string HexHash => _hash.ToString("X16");

        public void FoldTick(
            int runNumber,
            int stageIndex,
            RunState runState,
            IBattleSim steppedBattle,
            long totalScore)
        {
            if (steppedBattle == null)
                throw new ArgumentNullException(nameof(steppedBattle));
            FoldTick(
                runNumber,
                stageIndex,
                (int)runState,
                steppedBattle.Tick,
                steppedBattle.PlayerX,
                steppedBattle.PlayerY,
                steppedBattle.Bullets.Count,
                steppedBattle.Enemies.Count,
                totalScore,
                steppedBattle.EventsThisTick.Length);
        }

        public void FoldTick(
            int runNumber,
            int stageIndex,
            int runState,
            int battleTick,
            int playerX,
            int playerY,
            int bulletCount,
            int enemyCount,
            long totalScore,
            int eventCount)
        {
            FoldInt32(runNumber);
            FoldInt32(stageIndex);
            FoldInt32(runState);
            FoldInt32(battleTick);
            FoldInt32(playerX);
            FoldInt32(playerY);
            FoldInt32(bulletCount);
            FoldInt32(enemyCount);
            FoldInt64(totalScore);
            FoldInt32(eventCount);
        }

        void FoldInt32(int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                for (int shift = 0; shift < 32; shift += 8)
                    FoldByte((byte)(bits >> shift));
            }
        }

        void FoldInt64(long value)
        {
            unchecked
            {
                ulong bits = (ulong)value;
                for (int shift = 0; shift < 64; shift += 8)
                    FoldByte((byte)(bits >> shift));
            }
        }

        void FoldByte(byte value)
        {
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }
    }
}
