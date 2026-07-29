using System;
using System.Runtime.Serialization;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Serializer-facing acquisition counter. Entries are exported in reward
    /// catalog order so persistence never depends on a dictionary iteration.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class RewardAcquisitionData
    {
        [DataMember(Order = 0)]
        public string rewardId;

        [DataMember(Order = 1)]
        public int count;
    }

    [Serializable]
    [DataContract]
    public sealed class RouteChoiceData
    {
        [DataMember(Order = 0)]
        public int stageIndex;

        [DataMember(Order = 1)]
        public int optionIndex;

        [DataMember(Order = 2)]
        public string themeId;

        [DataMember(Order = 3)]
        public int encounterType;
    }

    /// <summary>
    /// Serializer-facing checkpoint for the beginning of a stage.
    /// Presentation owns file persistence. Exporting during a stage deliberately
    /// returns the state captured before tick zero, so resuming restarts that stage.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class RunSuspendData
    {
        public const int CurrentSchemaVersion = 3;

        [DataMember(Order = 0)]
        public int schemaVersion;

        [DataMember(Order = 1)]
        public ulong runSeed;

        [DataMember(Order = 2)]
        public int runNumber;

        [DataMember(Order = 3)]
        public int stageIndex;

        [DataMember(Order = 4)]
        public long score;

        [DataMember(Order = 5)]
        public long shotsFired;

        [DataMember(Order = 6)]
        public long shotsHit;

        [DataMember(Order = 7)]
        public long kills;

        [DataMember(Order = 8)]
        public long capsulesCollected;

        [DataMember(Order = 9)]
        public long grazeCount;

        [DataMember(Order = 10)]
        public int stagesCleared;

        [DataMember(Order = 11)]
        public int[] powerUpLevels;

        [DataMember(Order = 12)]
        public int powerUpCursor;

        [DataMember(Order = 13)]
        public int playerHp;

        [DataMember(Order = 14)]
        public int shieldRemaining;

        [DataMember(Order = 15)]
        public RewardAcquisitionData[] rewardAcquisitions;

        [DataMember(Order = 16)]
        public int activeModifiers;

        [DataMember(Order = 17)]
        public string shipId;

        // Passive rewards alter these BattleSimConfig values between stages.
        // Persisting the exact reduced integers avoids replaying reward history
        // with a potentially updated reward catalog.
        [DataMember(Order = 18)]
        public int fireIntervalTicks;

        [DataMember(Order = 19)]
        public int mainShotBaseDamage;

        [DataMember(Order = 20)]
        public int playerSpeedNumerator;

        [DataMember(Order = 21)]
        public int playerSpeedDenominator;

        [DataMember(Order = 22)]
        public int difficultyMultiplierNumerator;

        [DataMember(Order = 23)]
        public int difficultyMultiplierDenominator;

        [DataMember(Order = 24)]
        public RouteChoiceData[] routeChoices;
    }
}
