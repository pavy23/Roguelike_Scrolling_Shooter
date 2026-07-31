using System;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Deterministic fixed-order modifier stack state. Counts describe reward
    /// acquisitions; strengths are data-provided effect units. A shared
    /// interaction budget bounds multiplicative combinations.
    /// </summary>
    public sealed class BattleModifierStackSet
    {
        const int EffectCount = 4;
        readonly int[] _counts = new int[EffectCount];
        readonly int[] _strengths = new int[EffectCount];

        public BattleModifierStackSet(int combinationLimit)
        {
            if (combinationLimit < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(combinationLimit));
            CombinationLimit = combinationLimit;
        }

        public int CombinationLimit { get; }
        public int CombinationUsed { get; private set; }
        public BattleModifier ActiveModifiers { get; private set; }

        public int GetStackCount(BattleModifier effect) =>
            _counts[GetIndex(effect)];

        public int GetStrength(BattleModifier effect) =>
            _strengths[GetIndex(effect)];

        public bool CanAdd(
            BattleModifier effect,
            int stackStrength,
            int interactionCost,
            int maxStacks)
        {
            int index = GetIndex(effect);
            ValidateAddition(stackStrength, interactionCost, maxStacks);
            return _counts[index] < maxStacks
                && CombinationUsed <= CombinationLimit - interactionCost;
        }

        internal bool TryAdd(
            BattleModifier effect,
            int stackStrength,
            int interactionCost,
            int maxStacks)
        {
            if (!CanAdd(
                    effect,
                    stackStrength,
                    interactionCost,
                    maxStacks))
                return false;
            int index = GetIndex(effect);
            _counts[index]++;
            _strengths[index] = SaturatingAdd(
                _strengths[index],
                stackStrength);
            CombinationUsed += interactionCost;
            ActiveModifiers |= effect;
            return true;
        }

        internal BattleModifierStackSet Clone()
        {
            var clone = new BattleModifierStackSet(CombinationLimit);
            Array.Copy(_counts, clone._counts, EffectCount);
            Array.Copy(_strengths, clone._strengths, EffectCount);
            clone.CombinationUsed = CombinationUsed;
            clone.ActiveModifiers = ActiveModifiers;
            return clone;
        }

        public static BattleModifierStackSet FromFlags(
            BattleModifier flags,
            int combinationLimit)
        {
            if ((flags & ~BattleModifierRules.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(flags));
            var result =
                new BattleModifierStackSet(combinationLimit);
            foreach (BattleModifier effect in BattleModifierRules.Ordered)
            {
                if ((flags & effect) != 0)
                {
                    if (!result.TryAdd(effect, 1, 1, 1))
                        throw new ArgumentException(
                            "Modifier flags exceed the combination limit.",
                            nameof(combinationLimit));
                }
            }
            return result;
        }

        public static BattleModifierStackSet CreateSingle(
            BattleModifier effect,
            int stackCount,
            int stackStrength,
            int interactionCost,
            int combinationLimit)
        {
            if (stackCount < 1)
                throw new ArgumentOutOfRangeException(nameof(stackCount));
            var result =
                new BattleModifierStackSet(combinationLimit);
            for (int i = 0; i < stackCount; i++)
            {
                if (!result.TryAdd(
                        effect,
                        stackStrength,
                        interactionCost,
                        stackCount))
                {
                    throw new ArgumentException(
                        "The requested stacks exceed the combination limit.");
                }
            }
            return result;
        }

        static void ValidateAddition(
            int stackStrength,
            int interactionCost,
            int maxStacks)
        {
            if (stackStrength < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(stackStrength));
            if (interactionCost < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(interactionCost));
            if (maxStacks < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStacks));
        }

        static int GetIndex(BattleModifier effect)
        {
            switch (effect)
            {
                case BattleModifier.PierceShot: return 0;
                case BattleModifier.Ricochet: return 1;
                case BattleModifier.HomingMissile: return 2;
                case BattleModifier.KillExplosion: return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect));
            }
        }

        static int SaturatingAdd(int left, int right)
        {
            long sum = (long)left + right;
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
