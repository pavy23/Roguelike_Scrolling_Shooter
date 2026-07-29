using System;
using System.Globalization;
using Shmup.Core.Simulation;

namespace Shmup.Core
{
    /// <summary>
    /// Serializer-independent migration and integrity boundary for Core save DTOs.
    /// Presentation owns file IO; Core owns canonical field ordering and checksums.
    /// </summary>
    public static class SaveDataIntegrity
    {
        public static RunSuspendData MigrateAndValidate(
            RunSuspendData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.schemaVersion < 1
                || source.schemaVersion > RunSuspendData.CurrentSchemaVersion)
            {
                throw Unsupported(
                    "run suspend",
                    source.schemaVersion);
            }
            if (source.schemaVersion
                    == RunSuspendData.CurrentSchemaVersion
                && !HasValidChecksum(source))
            {
                throw Corrupted("Run suspend checksum is missing or invalid.");
            }
            if (source.schemaVersion
                    < RunSuspendData.CurrentSchemaVersion
                && !string.IsNullOrEmpty(source.checksum)
                && !(source.schemaVersion == 5
                    && HasValidRunSuspendV5Checksum(source))
                && !(source.schemaVersion == 4
                    && HasValidRunSuspendV4Checksum(source)))
            {
                throw Corrupted(
                    "Legacy run suspend contains an unexpected checksum.");
            }

            var migrated = new RunSuspendData
            {
                schemaVersion = RunSuspendData.CurrentSchemaVersion,
                runSeed = source.runSeed,
                runNumber = source.runNumber,
                stageIndex = source.stageIndex,
                score = source.score,
                shotsFired = source.shotsFired,
                shotsHit = source.shotsHit,
                kills = source.kills,
                capsulesCollected = source.capsulesCollected,
                grazeCount = source.grazeCount,
                stagesCleared = source.stagesCleared,
                powerUpLevels = Clone(source.powerUpLevels),
                powerUpCursor = source.powerUpCursor,
                playerHp = source.playerHp,
                shieldRemaining = source.shieldRemaining,
                rewardAcquisitions =
                    Clone(source.rewardAcquisitions),
                activeModifiers = source.activeModifiers,
                shipId = source.shipId,
                fireIntervalTicks = source.fireIntervalTicks,
                mainShotBaseDamage = source.mainShotBaseDamage,
                playerSpeedNumerator = source.playerSpeedNumerator,
                playerSpeedDenominator = source.playerSpeedDenominator,
                difficultyMultiplierNumerator =
                    source.schemaVersion >= 2
                        ? source.difficultyMultiplierNumerator
                        : 1,
                difficultyMultiplierDenominator =
                    source.schemaVersion >= 2
                        ? source.difficultyMultiplierDenominator
                        : 1,
                routeChoices =
                    source.schemaVersion >= 3
                        ? Clone(
                            source.routeChoices,
                            source.schemaVersion >= 5)
                        : Array.Empty<RouteChoiceData>(),
                finalStageIndex =
                    source.schemaVersion >= 4
                        ? source.finalStageIndex
                        : RunProgressionConfig.DefaultFinalStageIndex,
                biomeIndex =
                    source.schemaVersion >= 5
                        ? source.biomeIndex
                        : source.stageIndex,
                roomIndex =
                    source.schemaVersion >= 5
                        ? source.roomIndex
                        : 1,
                isBiomeBoss =
                    source.schemaVersion >= 5
                        && source.isBiomeBoss,
                biomeCount =
                    source.schemaVersion >= 5
                        ? source.biomeCount
                        : source.schemaVersion >= 4
                            ? source.finalStageIndex
                            : RunProgressionConfig.DefaultBiomeCount,
                roomsPerBiome =
                    source.schemaVersion >= 5
                        ? source.roomsPerBiome
                        : 1,
                roomsCleared =
                    source.schemaVersion >= 5
                        ? source.roomsCleared
                        : Math.Max(0, source.stageIndex - 1),
                missileFamily =
                    source.schemaVersion >= 6
                        ? source.missileFamily
                        : (int)MissileFamily.Straight,
                optionFormation =
                    source.schemaVersion >= 6
                        ? source.optionFormation
                        : (int)OptionFormation.Trail
            };
            Seal(migrated);
            return migrated;
        }

        public static InputRecordingData MigrateAndValidate(
            InputRecordingData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.schemaVersion < 1
                || source.schemaVersion
                    > InputRecordingData.CurrentSchemaVersion)
            {
                throw Unsupported(
                    "input recording",
                    source.schemaVersion);
            }
            if (source.schemaVersion
                    == InputRecordingData.CurrentSchemaVersion
                && !HasValidChecksum(source))
            {
                throw Corrupted(
                    "Input recording checksum is missing or invalid.");
            }
            if (source.schemaVersion
                    < InputRecordingData.CurrentSchemaVersion
                && !string.IsNullOrEmpty(source.checksum)
                && !(source.schemaVersion == 6
                    && HasValidInputRecordingV6Checksum(source))
                && !(source.schemaVersion == 5
                    && HasValidInputRecordingV5Checksum(source)))
            {
                throw Corrupted(
                    "Legacy input recording contains an unexpected checksum.");
            }

            var migrated = new InputRecordingData
            {
                schemaVersion = InputRecordingData.CurrentSchemaVersion,
                totalTicks = source.totalTicks,
                runs = Clone(
                    source.runs,
                    source.schemaVersion >= 2),
                difficultyMultiplierNumerator =
                    source.schemaVersion >= 3
                        ? source.difficultyMultiplierNumerator
                        : 1,
                difficultyMultiplierDenominator =
                    source.schemaVersion >= 3
                        ? source.difficultyMultiplierDenominator
                        : 1,
                routeChoices =
                    source.schemaVersion >= 4
                        ? Clone(
                            source.routeChoices,
                            source.schemaVersion >= 6)
                        : Array.Empty<RouteChoiceData>(),
                finalStageIndex =
                    source.schemaVersion >= 5
                        ? source.finalStageIndex
                        : RunProgressionConfig.DefaultFinalStageIndex,
                biomeCount =
                    source.schemaVersion >= 6
                        ? source.biomeCount
                        : source.schemaVersion >= 5
                            ? source.finalStageIndex
                            : RunProgressionConfig.DefaultBiomeCount,
                roomsPerBiome =
                    source.schemaVersion >= 6
                        ? source.roomsPerBiome
                        : 1,
                missileFamily =
                    source.schemaVersion >= 7
                        ? source.missileFamily
                        : (int)MissileFamily.Straight,
                optionFormation =
                    source.schemaVersion >= 7
                        ? source.optionFormation
                        : (int)OptionFormation.Trail
            };
            Seal(migrated);
            return migrated;
        }

        public static MetaStateData MigrateAndValidate(
            MetaStateData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.schemaVersion != 0
                && source.schemaVersion
                    != MetaStateData.CurrentSchemaVersion)
            {
                throw Unsupported("meta state", source.schemaVersion);
            }
            if (source.schemaVersion
                    == MetaStateData.CurrentSchemaVersion
                && !HasValidChecksum(source))
            {
                throw Corrupted("Meta state checksum is missing or invalid.");
            }
            if (source.schemaVersion == 0
                && !string.IsNullOrEmpty(source.checksum))
            {
                throw Corrupted(
                    "Legacy meta state contains an unexpected checksum.");
            }

            var migrated = new MetaStateData
            {
                schemaVersion = MetaStateData.CurrentSchemaVersion,
                totalCurrency = source.totalCurrency,
                unlockedShipIds = Clone(source.unlockedShipIds),
                selectedShipId = source.selectedShipId
            };
            Seal(migrated);
            return migrated;
        }

        /// <summary>
        /// Marks a populated DTO as current and refreshes its canonical checksum.
        /// Intended for Core exporters and tests constructing serializer payloads.
        /// </summary>
        public static void Seal(RunSuspendData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.schemaVersion = RunSuspendData.CurrentSchemaVersion;
            data.checksum = ComputeChecksum(data);
        }

        public static void Seal(InputRecordingData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.schemaVersion = InputRecordingData.CurrentSchemaVersion;
            data.checksum = ComputeChecksum(data);
        }

        public static void Seal(MetaStateData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data.schemaVersion = MetaStateData.CurrentSchemaVersion;
            data.checksum = ComputeChecksum(data);
        }

        public static bool HasValidChecksum(RunSuspendData data)
        {
            return data != null
                && IsChecksum(data.checksum)
                && string.Equals(
                    data.checksum,
                    ComputeChecksum(data),
                    StringComparison.Ordinal);
        }

        public static bool HasValidChecksum(InputRecordingData data)
        {
            return data != null
                && IsChecksum(data.checksum)
                && string.Equals(
                    data.checksum,
                    ComputeChecksum(data),
                    StringComparison.Ordinal);
        }

        public static bool HasValidChecksum(MetaStateData data)
        {
            return data != null
                && IsChecksum(data.checksum)
                && string.Equals(
                    data.checksum,
                    ComputeChecksum(data),
                    StringComparison.Ordinal);
        }

        static string ComputeChecksum(RunSuspendData data)
        {
            var hash = new CanonicalHash("RunSuspendData");
            hash.Add(data.schemaVersion);
            hash.Add(data.runSeed);
            hash.Add(data.runNumber);
            hash.Add(data.stageIndex);
            hash.Add(data.score);
            hash.Add(data.shotsFired);
            hash.Add(data.shotsHit);
            hash.Add(data.kills);
            hash.Add(data.capsulesCollected);
            hash.Add(data.grazeCount);
            hash.Add(data.stagesCleared);
            hash.Add(data.powerUpLevels);
            hash.Add(data.powerUpCursor);
            hash.Add(data.playerHp);
            hash.Add(data.shieldRemaining);
            Add(ref hash, data.rewardAcquisitions);
            hash.Add(data.activeModifiers);
            hash.Add(data.shipId);
            hash.Add(data.fireIntervalTicks);
            hash.Add(data.mainShotBaseDamage);
            hash.Add(data.playerSpeedNumerator);
            hash.Add(data.playerSpeedDenominator);
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            Add(ref hash, data.routeChoices);
            hash.Add(data.finalStageIndex);
            hash.Add(data.biomeIndex);
            hash.Add(data.roomIndex);
            hash.Add(data.isBiomeBoss);
            hash.Add(data.biomeCount);
            hash.Add(data.roomsPerBiome);
            hash.Add(data.roomsCleared);
            hash.Add(data.missileFamily);
            hash.Add(data.optionFormation);
            return hash.ToString();
        }

        static string ComputeChecksum(InputRecordingData data)
        {
            var hash = new CanonicalHash("InputRecordingData");
            hash.Add(data.schemaVersion);
            hash.Add(data.totalTicks);
            if (data.runs == null)
            {
                hash.Add(-1);
            }
            else
            {
                hash.Add(data.runs.Length);
                for (int i = 0; i < data.runs.Length; i++)
                {
                    InputRunData run = data.runs[i];
                    hash.Add(run != null);
                    if (run == null)
                        continue;
                    hash.Add(run.moveX);
                    hash.Add(run.moveY);
                    hash.Add(run.fire);
                    hash.Add(run.activate);
                    hash.Add(run.tickCount);
                }
            }
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            Add(ref hash, data.routeChoices);
            hash.Add(data.finalStageIndex);
            hash.Add(data.biomeCount);
            hash.Add(data.roomsPerBiome);
            hash.Add(data.missileFamily);
            hash.Add(data.optionFormation);
            return hash.ToString();
        }

        static bool HasValidRunSuspendV5Checksum(
            RunSuspendData data)
        {
            if (!IsChecksum(data.checksum))
                return false;
            var hash = new CanonicalHash("RunSuspendData");
            hash.Add(data.schemaVersion);
            hash.Add(data.runSeed);
            hash.Add(data.runNumber);
            hash.Add(data.stageIndex);
            hash.Add(data.score);
            hash.Add(data.shotsFired);
            hash.Add(data.shotsHit);
            hash.Add(data.kills);
            hash.Add(data.capsulesCollected);
            hash.Add(data.grazeCount);
            hash.Add(data.stagesCleared);
            hash.Add(data.powerUpLevels);
            hash.Add(data.powerUpCursor);
            hash.Add(data.playerHp);
            hash.Add(data.shieldRemaining);
            Add(ref hash, data.rewardAcquisitions);
            hash.Add(data.activeModifiers);
            hash.Add(data.shipId);
            hash.Add(data.fireIntervalTicks);
            hash.Add(data.mainShotBaseDamage);
            hash.Add(data.playerSpeedNumerator);
            hash.Add(data.playerSpeedDenominator);
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            Add(ref hash, data.routeChoices);
            hash.Add(data.finalStageIndex);
            hash.Add(data.biomeIndex);
            hash.Add(data.roomIndex);
            hash.Add(data.isBiomeBoss);
            hash.Add(data.biomeCount);
            hash.Add(data.roomsPerBiome);
            hash.Add(data.roomsCleared);
            return string.Equals(
                data.checksum,
                hash.ToString(),
                StringComparison.Ordinal);
        }

        static bool HasValidInputRecordingV6Checksum(
            InputRecordingData data)
        {
            if (!IsChecksum(data.checksum))
                return false;
            var hash = new CanonicalHash("InputRecordingData");
            hash.Add(data.schemaVersion);
            hash.Add(data.totalTicks);
            AddInputRuns(ref hash, data.runs);
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            Add(ref hash, data.routeChoices);
            hash.Add(data.finalStageIndex);
            hash.Add(data.biomeCount);
            hash.Add(data.roomsPerBiome);
            return string.Equals(
                data.checksum,
                hash.ToString(),
                StringComparison.Ordinal);
        }

        static bool HasValidRunSuspendV4Checksum(RunSuspendData data)
        {
            if (!IsChecksum(data.checksum))
                return false;
            var hash = new CanonicalHash("RunSuspendData");
            hash.Add(data.schemaVersion);
            hash.Add(data.runSeed);
            hash.Add(data.runNumber);
            hash.Add(data.stageIndex);
            hash.Add(data.score);
            hash.Add(data.shotsFired);
            hash.Add(data.shotsHit);
            hash.Add(data.kills);
            hash.Add(data.capsulesCollected);
            hash.Add(data.grazeCount);
            hash.Add(data.stagesCleared);
            hash.Add(data.powerUpLevels);
            hash.Add(data.powerUpCursor);
            hash.Add(data.playerHp);
            hash.Add(data.shieldRemaining);
            hash.Add(data.activeModifiers);
            hash.Add(data.shipId);
            hash.Add(data.fireIntervalTicks);
            hash.Add(data.mainShotBaseDamage);
            hash.Add(data.playerSpeedNumerator);
            hash.Add(data.playerSpeedDenominator);
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            hash.Add(data.finalStageIndex);
            return string.Equals(
                data.checksum,
                hash.ToString(),
                StringComparison.Ordinal);
        }

        static bool HasValidInputRecordingV5Checksum(InputRecordingData data)
        {
            if (!IsChecksum(data.checksum))
                return false;
            var hash = new CanonicalHash("InputRecordingData");
            hash.Add(data.schemaVersion);
            hash.Add(data.totalTicks);
            AddInputRuns(ref hash, data.runs);
            hash.Add(data.difficultyMultiplierNumerator);
            hash.Add(data.difficultyMultiplierDenominator);
            hash.Add(data.finalStageIndex);
            return string.Equals(
                data.checksum,
                hash.ToString(),
                StringComparison.Ordinal);
        }

        static string ComputeChecksum(MetaStateData data)
        {
            var hash = new CanonicalHash("MetaStateData");
            hash.Add(data.schemaVersion);
            hash.Add(data.totalCurrency);
            hash.Add(data.unlockedShipIds);
            hash.Add(data.selectedShipId);
            return hash.ToString();
        }

        static void Add(
            ref CanonicalHash hash,
            RewardAcquisitionData[] acquisitions)
        {
            if (acquisitions == null)
            {
                hash.Add(-1);
                return;
            }
            hash.Add(acquisitions.Length);
            for (int i = 0; i < acquisitions.Length; i++)
            {
                RewardAcquisitionData acquisition = acquisitions[i];
                hash.Add(acquisition != null);
                if (acquisition == null)
                    continue;
                hash.Add(acquisition.rewardId);
                hash.Add(acquisition.count);
            }
        }

        static void Add(
            ref CanonicalHash hash,
            RouteChoiceData[] choices)
        {
            if (choices == null)
            {
                hash.Add(-1);
                return;
            }
            hash.Add(choices.Length);
            for (int i = 0; i < choices.Length; i++)
            {
                RouteChoiceData choice = choices[i];
                hash.Add(choice != null);
                if (choice == null)
                    continue;
                hash.Add(choice.stageIndex);
                hash.Add(choice.optionIndex);
                hash.Add(choice.themeId);
                hash.Add(choice.encounterType);
                hash.Add(choice.biomeIndex);
                hash.Add(choice.roomIndex);
            }
        }

        static void AddInputRuns(
            ref CanonicalHash hash,
            InputRunData[] runs)
        {
            if (runs == null)
            {
                hash.Add(-1);
                return;
            }
            hash.Add(runs.Length);
            for (int i = 0; i < runs.Length; i++)
            {
                InputRunData run = runs[i];
                hash.Add(run != null);
                if (run == null)
                    continue;
                hash.Add(run.moveX);
                hash.Add(run.moveY);
                hash.Add(run.fire);
                hash.Add(run.activate);
                hash.Add(run.tickCount);
            }
        }

        static int[] Clone(int[] source)
        {
            return source == null ? null : (int[])source.Clone();
        }

        static string[] Clone(string[] source)
        {
            return source == null ? null : (string[])source.Clone();
        }

        static RewardAcquisitionData[] Clone(
            RewardAcquisitionData[] source)
        {
            if (source == null)
                return null;
            var copy = new RewardAcquisitionData[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                RewardAcquisitionData item = source[i];
                copy[i] = item == null
                    ? null
                    : new RewardAcquisitionData
                    {
                        rewardId = item.rewardId,
                        count = item.count
                    };
            }
            return copy;
        }

        static RouteChoiceData[] Clone(
            RouteChoiceData[] source,
            bool includeRoomPosition)
        {
            if (source == null)
                return null;
            var copy = new RouteChoiceData[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                RouteChoiceData item = source[i];
                copy[i] = item == null
                    ? null
                    : new RouteChoiceData
                    {
                        stageIndex = item.stageIndex,
                        biomeIndex = includeRoomPosition
                            ? item.biomeIndex
                            : item.stageIndex,
                        roomIndex = includeRoomPosition
                            ? item.roomIndex
                            : 1,
                        optionIndex = item.optionIndex,
                        themeId = item.themeId,
                        encounterType = item.encounterType
                    };
            }
            return copy;
        }

        static InputRunData[] Clone(
            InputRunData[] source,
            bool includeActivate)
        {
            if (source == null)
                return null;
            var copy = new InputRunData[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                InputRunData item = source[i];
                copy[i] = item == null
                    ? null
                    : new InputRunData
                    {
                        moveX = item.moveX,
                        moveY = item.moveY,
                        fire = item.fire,
                        activate = includeActivate && item.activate,
                        tickCount = item.tickCount
                    };
            }
            return copy;
        }

        static ArgumentException Unsupported(
            string payloadName,
            int version)
        {
            return Corrupted(
                $"Unsupported {payloadName} schema version {version}.");
        }

        static ArgumentException Corrupted(string message)
        {
            return new ArgumentException(message, "data");
        }

        static bool IsChecksum(string value)
        {
            if (value == null || value.Length != 16)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if ((character < '0' || character > '9')
                    && (character < 'A' || character > 'F'))
                    return false;
            }
            return true;
        }

        struct CanonicalHash
        {
            const ulong OffsetBasis = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;

            ulong _value;

            public CanonicalHash(string typeTag)
            {
                _value = OffsetBasis;
                Add(typeTag);
            }

            public void Add(bool value)
            {
                AddByte(value ? (byte)1 : (byte)0);
            }

            public void Add(int value)
            {
                Add(unchecked((uint)value));
            }

            public void Add(uint value)
            {
                AddByte((byte)value);
                AddByte((byte)(value >> 8));
                AddByte((byte)(value >> 16));
                AddByte((byte)(value >> 24));
            }

            public void Add(long value)
            {
                Add(unchecked((ulong)value));
            }

            public void Add(ulong value)
            {
                AddByte((byte)value);
                AddByte((byte)(value >> 8));
                AddByte((byte)(value >> 16));
                AddByte((byte)(value >> 24));
                AddByte((byte)(value >> 32));
                AddByte((byte)(value >> 40));
                AddByte((byte)(value >> 48));
                AddByte((byte)(value >> 56));
            }

            public void Add(string value)
            {
                if (value == null)
                {
                    Add(-1);
                    return;
                }
                Add(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    AddByte((byte)character);
                    AddByte((byte)(character >> 8));
                }
            }

            public void Add(int[] values)
            {
                if (values == null)
                {
                    Add(-1);
                    return;
                }
                Add(values.Length);
                for (int i = 0; i < values.Length; i++)
                    Add(values[i]);
            }

            public void Add(string[] values)
            {
                if (values == null)
                {
                    Add(-1);
                    return;
                }
                Add(values.Length);
                for (int i = 0; i < values.Length; i++)
                    Add(values[i]);
            }

            public override string ToString()
            {
                return _value.ToString(
                    "X16",
                    CultureInfo.InvariantCulture);
            }

            void AddByte(byte value)
            {
                _value ^= value;
                _value *= Prime;
            }
        }
    }
}
