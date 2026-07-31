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

        [DataMember(Order = 4)]
        public int biomeIndex;

        [DataMember(Order = 5)]
        public int roomIndex;
    }

    [Serializable]
    [DataContract]
    public sealed class ContractChoiceData
    {
        [DataMember(Order = 0)]
        public int targetBiomeIndex;

        [DataMember(Order = 1)]
        public int optionIndex;

        [DataMember(Order = 2)]
        public string contractId;

        [DataMember(Order = 3)]
        public int destinationKind;
    }

    [Serializable]
    [DataContract]
    public sealed class RewardDecisionData
    {
        [DataMember(Order = 0)]
        public int rewardSequence;

        [DataMember(Order = 1)]
        public int selectionKind;

        [DataMember(Order = 2)]
        public int decisionKind;

        [DataMember(Order = 3)]
        public int optionIndex;
    }

    /// <summary>
    /// Serializer-facing checkpoint for the beginning of a room or biome boss.
    /// Presentation owns file persistence. Exporting during a stage deliberately
    /// returns the state captured before tick zero, so resuming restarts that boundary.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class RunSuspendData
    {
        /// <summary>
        /// Schema 16 stores eight stable power axes and a ship-specific gauge
        /// cursor. Schema 15 is rejected because its cursor assumed seven slots.
        /// </summary>
        public const int CurrentSchemaVersion = 16;

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

        [DataMember(Order = 25)]
        public int finalStageIndex;

        [DataMember(Order = 26)]
        public string checksum;

        [DataMember(Order = 27)]
        public int biomeIndex;

        [DataMember(Order = 28)]
        public int roomIndex;

        [DataMember(Order = 29)]
        public bool isBiomeBoss;

        [DataMember(Order = 30)]
        public int biomeCount;

        [DataMember(Order = 31)]
        public int roomsPerBiome;

        [DataMember(Order = 32)]
        public int roomsCleared;

        [DataMember(Order = 33)]
        public int missileFamily;

        [DataMember(Order = 34)]
        public int optionFormation;

        [DataMember(Order = 35)]
        public bool isHiddenBiome;

        [DataMember(Order = 36)]
        public int eliteRoomsCleared;

        [DataMember(Order = 37)]
        public int noHitBiomesCleared;

        [DataMember(Order = 38)]
        public int rareEncountersCleared;

        [DataMember(Order = 39)]
        public bool currentBiomeHit;

        [DataMember(Order = 40)]
        public int selectedColossalBoss;

        [DataMember(Order = 41)]
        public int lastColossalBossAtRunStart;

        /// <summary>
        /// Authoritative REQ-040 durability. playerHp and shieldRemaining remain
        /// serialized compatibility mirrors for older Presentation builds.
        /// </summary>
        [DataMember(Order = 42)]
        public int shieldStock;

        [DataMember(Order = 43)]
        public int maxShieldStock;

        [DataMember(Order = 44)]
        public int bombStock;

        [DataMember(Order = 45)]
        public int maxBombStock;

        /// <summary>
        /// Selected runtime primary family. -1 is accepted only as the v1-v9
        /// migration sentinel and resolves from shipId during resume.
        /// </summary>
        [DataMember(Order = 46)]
        public int primaryWeaponFamily;

        /// <summary>
        /// Partial capsule investments ordered by PowerUpSlot. Schema v1-v10
        /// migrate to zero because those versions had no partial progress.
        /// </summary>
        [DataMember(Order = 47)]
        public int[] powerUpProgress;

        [DataMember(Order = 48)]
        public bool hasStageStartContinuity;

        [DataMember(Order = 49)]
        public int stageStartPlayerX;

        [DataMember(Order = 50)]
        public int stageStartPlayerY;

        [DataMember(Order = 51)]
        public int stageStartMultiplierLevel;

        [DataMember(Order = 52)]
        public int stageStartComboGauge;

        [DataMember(Order = 53)]
        public int stageStartTicksSinceLastKill;

        [DataMember(Order = 54)]
        public string activeContractId;

        [DataMember(Order = 55)]
        public ContractChoiceData[] contractChoices;

        [DataMember(Order = 56)]
        public int capsuleDropWeightReduction;

        [DataMember(Order = 57)]
        public int capsuleBalance;

        [DataMember(Order = 58)]
        public RewardDecisionData[] rewardDecisions;
    }
}
