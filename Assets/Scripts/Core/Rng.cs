using System;
using System.Collections.Generic;

namespace Shmup.Core
{
    /// <summary>
    /// Deterministic PRNG (xoshiro256**) seeded via SplitMix64.
    /// Same seed produces the same sequence on every platform and runtime —
    /// this is the roguelike's reproducibility guarantee (AGENTS.md §4).
    /// All randomness in Shmup.Core must flow through an injected Rng instance;
    /// UnityEngine.Random and System.Random are forbidden in this assembly.
    /// </summary>
    public sealed class Rng
    {
        readonly ulong _seed;
        ulong _s0, _s1, _s2, _s3;

        public Rng(ulong seed)
        {
            _seed = seed;
            ulong sm = seed;
            _s0 = SplitMix64(ref sm);
            _s1 = SplitMix64(ref sm);
            _s2 = SplitMix64(ref sm);
            _s3 = SplitMix64(ref sm);
            if ((_s0 | _s1 | _s2 | _s3) == 0UL)
                _s0 = 0x9E3779B97F4A7C15UL;
        }

        /// <summary>The seed this instance was constructed with. Forks derive from this, not from consumed state.</summary>
        public ulong Seed => _seed;

        static ulong SplitMix64(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            return Mix(x);
        }

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        public ulong NextULong()
        {
            ulong result = Rotl(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive). Rejection sampling — no modulo bias.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException("maxExclusive must be greater than minInclusive");
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)((long)minInclusive + (long)NextBelow(range));
        }

        /// <summary>Uniform double in [0, 1) with 53 bits of precision.</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public bool NextBool(double probability) => NextDouble() < probability;

        /// <summary>
        /// Pick an index with probability proportional to its weight.
        /// Weights must be non-negative and sum to a positive value.
        /// </summary>
        public int PickWeighted(IReadOnlyList<int> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            long total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0) throw new ArgumentException("weights must be non-negative");
                total += weights[i];
            }
            if (total <= 0) throw new ArgumentException("weights must sum to a positive value");

            long roll = (long)NextBelow((ulong)total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>
        /// Derive an independent stream from this Rng's original seed.
        /// Same (seed, streamId) always yields the same stream, regardless of how much
        /// the parent has been consumed — so fixing stage-generation logic never
        /// shifts drop-roll results, and vice versa (AGENTS.md §4).
        /// </summary>
        public Rng Fork(int streamId)
        {
            ulong childSeed = Mix(_seed + 0x9E3779B97F4A7C15UL * ((ulong)(uint)streamId + 1UL));
            return new Rng(childSeed);
        }

        ulong NextBelow(ulong bound)
        {
            ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
            ulong v;
            do { v = NextULong(); } while (v >= limit);
            return v % bound;
        }
    }
}
