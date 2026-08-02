using System;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Tunable continue-economy values. Content may construct this from JSON;
    /// Core keeps the default proposal available when no data is supplied.
    /// </summary>
    public sealed class ContinueEconomyConfig
    {
        public const int DefaultMaximumStock = 8;
        public const long DefaultFirstPurchasePrice = 2000;
        public const long DefaultPurchasePriceIncrease = 1000;
        public const int DefaultFinalWagerShieldCap = 5;
        public const long DefaultOverflowScoreBonus = 1000;

        public ContinueEconomyConfig(
            int maximumStock = DefaultMaximumStock,
            long firstPurchasePrice = DefaultFirstPurchasePrice,
            long purchasePriceIncrease = DefaultPurchasePriceIncrease,
            int finalWagerShieldCap = DefaultFinalWagerShieldCap,
            long overflowScoreBonus = DefaultOverflowScoreBonus)
        {
            if (maximumStock < 1 || maximumStock > DefaultMaximumStock)
                throw new ArgumentOutOfRangeException(nameof(maximumStock));
            if (firstPurchasePrice < 0)
                throw new ArgumentOutOfRangeException(nameof(firstPurchasePrice));
            if (purchasePriceIncrease < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(purchasePriceIncrease));
            if (maximumStock > 1
                && purchasePriceIncrease
                    > (long.MaxValue - firstPurchasePrice)
                        / (maximumStock - 1))
                throw new ArgumentOutOfRangeException(
                    nameof(purchasePriceIncrease));
            if (finalWagerShieldCap < 1
                || finalWagerShieldCap > BattleSimConfig.MaximumShieldStock)
                throw new ArgumentOutOfRangeException(
                    nameof(finalWagerShieldCap));
            if (overflowScoreBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(overflowScoreBonus));

            MaximumStock = maximumStock;
            FirstPurchasePrice = firstPurchasePrice;
            PurchasePriceIncrease = purchasePriceIncrease;
            FinalWagerShieldCap = finalWagerShieldCap;
            OverflowScoreBonus = overflowScoreBonus;
        }

        public int MaximumStock { get; }
        public long FirstPurchasePrice { get; }
        public long PurchasePriceIncrease { get; }
        public int FinalWagerShieldCap { get; }
        public long OverflowScoreBonus { get; }

        public static ContinueEconomyConfig CreateDefault() =>
            new ContinueEconomyConfig();

        public long GetPurchasePrice(int currentStock)
        {
            if (currentStock < 0 || currentStock > MaximumStock)
                throw new ArgumentOutOfRangeException(nameof(currentStock));
            if (currentStock == MaximumStock)
                return 0;
            return checked(
                FirstPurchasePrice
                + PurchasePriceIncrease * currentStock);
        }
    }

    public enum ContinuePurchaseRejectionReason
    {
        None = 0,
        StockFull = 1,
        InsufficientCurrency = 2
    }

    public readonly struct ContinuePurchaseResult
    {
        internal ContinuePurchaseResult(
            bool purchased,
            ContinuePurchaseRejectionReason rejectionReason,
            long price)
        {
            Purchased = purchased;
            RejectionReason = rejectionReason;
            Price = price;
        }

        public bool Purchased { get; }
        public ContinuePurchaseRejectionReason RejectionReason { get; }
        public long Price { get; }
    }

    public enum ContinueRejectionReason
    {
        None = 0,
        RunNotOver = 1,
        NoStock = 2,
        DailyRun = 3,
        FinalWagerCommitted = 4
    }

    public readonly struct ContinueAvailability
    {
        internal ContinueAvailability(
            bool canUse,
            ContinueRejectionReason rejectionReason,
            int stock)
        {
            CanUse = canUse;
            RejectionReason = rejectionReason;
            Stock = stock;
        }

        public bool CanUse { get; }
        public ContinueRejectionReason RejectionReason { get; }
        public int Stock { get; }
    }

    /// <summary>
    /// Successful continue decision at a cumulative simulation-tick boundary.
    /// Replay drivers apply the decision when RunManager.SimulationTicksElapsed
    /// reaches this value and the reconstructed run is over.
    /// </summary>
    public readonly struct ContinueDecision
    {
        public ContinueDecision(int simulationTick)
        {
            if (simulationTick < 1)
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            SimulationTick = simulationTick;
        }

        public int SimulationTick { get; }
    }
}
