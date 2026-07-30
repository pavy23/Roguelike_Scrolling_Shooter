using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shmup.Core.Simulation
{
    public enum ContractRiskTier
    {
        Safe = 0,
        Low = 1,
        High = 2,
        Extreme = 3
    }

    public enum ContractEffectType
    {
        EnemyDensityMultiplier = 0,
        CapsuleDropMultiplier = 1,
        BombDropMultiplier = 2,
        GuaranteedBombDrop = 3,
        GimmickIntensityMultiplier = 4,
        RewardOptionCountDelta = 5,
        ScoreMultiplier = 6
    }

    public readonly struct ContractEffectView
    {
        public ContractEffectView(
            ContractEffectType type,
            int numerator,
            int denominator = 1)
        {
            if (!Enum.IsDefined(typeof(ContractEffectType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (denominator < 1)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            Type = type;
            Numerator = numerator;
            Denominator = denominator;
        }

        public ContractEffectType Type { get; }
        public int Numerator { get; }
        public int Denominator { get; }
    }

    /// <summary>
    /// Immutable, integer-rational knobs applied to the next biome.
    /// A neutral definition is the mandatory standard route.
    /// </summary>
    public sealed class ContractDefinition
    {
        readonly ReadOnlyCollection<ContractEffectView> _effects;

        public ContractDefinition(
            string id,
            int weight,
            ContractRiskTier riskTier,
            int enemyDensityNumerator = 1,
            int enemyDensityDenominator = 1,
            int capsuleDropNumerator = 1,
            int capsuleDropDenominator = 1,
            int bombDropNumerator = 1,
            int bombDropDenominator = 1,
            bool guaranteedBombDrop = false,
            int gimmickIntensityNumerator = 1,
            int gimmickIntensityDenominator = 1,
            int rewardOptionCountDelta = 0,
            int scoreMultiplierNumerator = 1,
            int scoreMultiplierDenominator = 1)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException(
                    "Contract id cannot be empty.",
                    nameof(id));
            if (weight < 1)
                throw new ArgumentOutOfRangeException(nameof(weight));
            if (!Enum.IsDefined(typeof(ContractRiskTier), riskTier))
                throw new ArgumentOutOfRangeException(nameof(riskTier));
            ValidateMultiplier(
                enemyDensityNumerator,
                enemyDensityDenominator,
                nameof(enemyDensityNumerator));
            ValidateMultiplier(
                capsuleDropNumerator,
                capsuleDropDenominator,
                nameof(capsuleDropNumerator));
            ValidateMultiplier(
                bombDropNumerator,
                bombDropDenominator,
                nameof(bombDropNumerator));
            ValidateMultiplier(
                gimmickIntensityNumerator,
                gimmickIntensityDenominator,
                nameof(gimmickIntensityNumerator));
            ValidateMultiplier(
                scoreMultiplierNumerator,
                scoreMultiplierDenominator,
                nameof(scoreMultiplierNumerator));
            if (rewardOptionCountDelta < -1
                || rewardOptionCountDelta > 1)
                throw new ArgumentOutOfRangeException(
                    nameof(rewardOptionCountDelta));

            Id = id;
            Weight = weight;
            RiskTier = riskTier;
            EnemyDensityNumerator = enemyDensityNumerator;
            EnemyDensityDenominator = enemyDensityDenominator;
            CapsuleDropNumerator = capsuleDropNumerator;
            CapsuleDropDenominator = capsuleDropDenominator;
            BombDropNumerator = bombDropNumerator;
            BombDropDenominator = bombDropDenominator;
            GuaranteedBombDrop = guaranteedBombDrop;
            GimmickIntensityNumerator = gimmickIntensityNumerator;
            GimmickIntensityDenominator = gimmickIntensityDenominator;
            RewardOptionCountDelta = rewardOptionCountDelta;
            ScoreMultiplierNumerator = scoreMultiplierNumerator;
            ScoreMultiplierDenominator = scoreMultiplierDenominator;
            _effects = Array.AsReadOnly(BuildEffects());
        }

        public string Id { get; }
        public int Weight { get; }
        public ContractRiskTier RiskTier { get; }
        public int EnemyDensityNumerator { get; }
        public int EnemyDensityDenominator { get; }
        public int CapsuleDropNumerator { get; }
        public int CapsuleDropDenominator { get; }
        public int BombDropNumerator { get; }
        public int BombDropDenominator { get; }
        public bool GuaranteedBombDrop { get; }
        public int GimmickIntensityNumerator { get; }
        public int GimmickIntensityDenominator { get; }
        public int RewardOptionCountDelta { get; }
        public int ScoreMultiplierNumerator { get; }
        public int ScoreMultiplierDenominator { get; }
        public IReadOnlyList<ContractEffectView> Effects => _effects;

        public bool IsNeutral =>
            EnemyDensityNumerator == EnemyDensityDenominator
            && CapsuleDropNumerator == CapsuleDropDenominator
            && BombDropNumerator == BombDropDenominator
            && !GuaranteedBombDrop
            && GimmickIntensityNumerator
                == GimmickIntensityDenominator
            && RewardOptionCountDelta == 0
            && ScoreMultiplierNumerator
                == ScoreMultiplierDenominator;

        ContractEffectView[] BuildEffects()
        {
            var effects = new List<ContractEffectView>(7);
            AddMultiplier(
                effects,
                ContractEffectType.EnemyDensityMultiplier,
                EnemyDensityNumerator,
                EnemyDensityDenominator);
            AddMultiplier(
                effects,
                ContractEffectType.CapsuleDropMultiplier,
                CapsuleDropNumerator,
                CapsuleDropDenominator);
            AddMultiplier(
                effects,
                ContractEffectType.BombDropMultiplier,
                BombDropNumerator,
                BombDropDenominator);
            if (GuaranteedBombDrop)
                effects.Add(new ContractEffectView(
                    ContractEffectType.GuaranteedBombDrop,
                    1));
            AddMultiplier(
                effects,
                ContractEffectType.GimmickIntensityMultiplier,
                GimmickIntensityNumerator,
                GimmickIntensityDenominator);
            if (RewardOptionCountDelta != 0)
                effects.Add(new ContractEffectView(
                    ContractEffectType.RewardOptionCountDelta,
                    RewardOptionCountDelta));
            AddMultiplier(
                effects,
                ContractEffectType.ScoreMultiplier,
                ScoreMultiplierNumerator,
                ScoreMultiplierDenominator);
            return effects.ToArray();
        }

        static void AddMultiplier(
            List<ContractEffectView> destination,
            ContractEffectType type,
            int numerator,
            int denominator)
        {
            if (numerator != denominator)
                destination.Add(new ContractEffectView(
                    type,
                    numerator,
                    denominator));
        }

        static void ValidateMultiplier(
            int numerator,
            int denominator,
            string parameterName)
        {
            if (numerator < 0)
                throw new ArgumentOutOfRangeException(parameterName);
            if (denominator < 1)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class ContractCatalog
    {
        readonly ReadOnlyCollection<ContractDefinition> _all;

        public ContractCatalog(
            string standardContractId,
            int minimumOptionCount,
            int maximumOptionCount,
            IReadOnlyList<ContractDefinition> contracts)
        {
            if (string.IsNullOrEmpty(standardContractId))
                throw new ArgumentException(
                    "Standard contract id cannot be empty.",
                    nameof(standardContractId));
            if (minimumOptionCount < 2
                || maximumOptionCount < minimumOptionCount
                || maximumOptionCount > 3)
                throw new ArgumentOutOfRangeException(
                    nameof(minimumOptionCount),
                    "Contract option counts must stay within 2..3.");
            if (contracts == null)
                throw new ArgumentNullException(nameof(contracts));
            if (contracts.Count < minimumOptionCount)
                throw new ArgumentException(
                    "The contract pool is smaller than its minimum option count.",
                    nameof(contracts));

            var copy = new ContractDefinition[contracts.Count];
            ContractDefinition standard = null;
            long nonStandardWeight = 0;
            for (int i = 0; i < copy.Length; i++)
            {
                ContractDefinition item = contracts[i]
                    ?? throw new ArgumentException(
                        "Contracts cannot contain null.",
                        nameof(contracts));
                for (int earlier = 0; earlier < i; earlier++)
                    if (string.Equals(
                            copy[earlier].Id,
                            item.Id,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            $"Duplicate contract id '{item.Id}'.",
                            nameof(contracts));
                copy[i] = item;
                if (string.Equals(
                        item.Id,
                        standardContractId,
                        StringComparison.Ordinal))
                    standard = item;
                else
                    nonStandardWeight += item.Weight;
            }
            if (standard == null)
                throw new ArgumentException(
                    "The standard contract id is missing from the catalog.",
                    nameof(standardContractId));
            if (!standard.IsNeutral
                || standard.RiskTier != ContractRiskTier.Safe)
                throw new ArgumentException(
                    "The standard contract must be safe and neutral.",
                    nameof(contracts));
            if (nonStandardWeight > int.MaxValue)
                throw new ArgumentException(
                    "The contract weight total exceeds Int32.",
                    nameof(contracts));

            Standard = standard;
            MinimumOptionCount = minimumOptionCount;
            MaximumOptionCount = maximumOptionCount;
            _all = Array.AsReadOnly(copy);
        }

        public ContractDefinition Standard { get; }
        public int MinimumOptionCount { get; }
        public int MaximumOptionCount { get; }
        public IReadOnlyList<ContractDefinition> All => _all;

        public ContractDefinition Find(string id)
        {
            if (id == null)
                return null;
            for (int i = 0; i < _all.Count; i++)
                if (string.Equals(
                        _all[i].Id,
                        id,
                        StringComparison.Ordinal))
                    return _all[i];
            return null;
        }
    }

    public readonly struct ContractChoice
    {
        public ContractChoice(
            int targetBiomeIndex,
            int optionIndex,
            string contractId)
        {
            if (targetBiomeIndex < 2)
                throw new ArgumentOutOfRangeException(
                    nameof(targetBiomeIndex));
            if (optionIndex < 0
                || optionIndex >= RunManager.MaximumContractOptionCount)
                throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (string.IsNullOrEmpty(contractId))
                throw new ArgumentException(
                    "Contract id cannot be empty.",
                    nameof(contractId));
            TargetBiomeIndex = targetBiomeIndex;
            OptionIndex = optionIndex;
            ContractId = contractId;
        }

        public int TargetBiomeIndex { get; }
        public int OptionIndex { get; }
        public string ContractId { get; }
    }
}
