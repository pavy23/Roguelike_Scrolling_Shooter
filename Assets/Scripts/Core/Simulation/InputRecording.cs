using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    /// <summary>Serializer-facing run-length encoded input command.</summary>
    [Serializable]
    [DataContract]
    public sealed class InputRunData
    {
        [DataMember(Order = 0)]
        public int moveX;

        [DataMember(Order = 1)]
        public int moveY;

        [DataMember(Order = 2)]
        public bool fire;

        [DataMember(Order = 3)]
        public bool activate;

        [DataMember(Order = 4)]
        public int tickCount;
    }

    /// <summary>
    /// Serializer-facing input recording. Presentation owns persistence.
    /// </summary>
    [Serializable]
    [DataContract]
    public sealed class InputRecordingData
    {
        public const int CurrentSchemaVersion = 7;

        [DataMember(Order = 0)]
        public int schemaVersion;

        [DataMember(Order = 1)]
        public int totalTicks;

        [DataMember(Order = 2)]
        public InputRunData[] runs;

        [DataMember(Order = 3)]
        public int difficultyMultiplierNumerator = 1;

        [DataMember(Order = 4)]
        public int difficultyMultiplierDenominator = 1;

        [DataMember(Order = 5)]
        public RouteChoiceData[] routeChoices;

        [DataMember(Order = 6)]
        public int finalStageIndex;

        [DataMember(Order = 7)]
        public string checksum;

        [DataMember(Order = 8)]
        public int biomeCount;

        [DataMember(Order = 9)]
        public int roomsPerBiome;

        [DataMember(Order = 10)]
        public int missileFamily;

        [DataMember(Order = 11)]
        public int optionFormation;
    }

    /// <summary>
    /// Run-length recorder for commands supplied to RunManager.Step.
    /// Its value-type run buffer is fully reserved at construction, so every
    /// successful Record call is allocation-free. Export is the allocation
    /// boundary that creates serializer-facing reference types.
    /// </summary>
    public sealed class InputRecorder
    {
        const int DefaultRunCapacity = 4096;

        readonly InputRun[] _runs;
        readonly int _difficultyMultiplierNumerator;
        readonly int _difficultyMultiplierDenominator;
        readonly int _finalStageIndex;
        readonly int _roomsPerBiome;
        readonly MissileFamily _missileFamily;
        readonly OptionFormation _optionFormation;
        readonly List<RouteChoice> _recordedRouteChoices;
        readonly RunManager _routeSource;
        int _runCount;
        int _totalTicks;

        public InputRecorder()
            : this(
                DefaultRunCapacity,
                1,
                1,
                RunProgressionConfig.DefaultBiomeCount,
                RunProgressionConfig.DefaultRoomsPerBiome)
        {
        }

        public InputRecorder(int runCapacity)
            : this(
                runCapacity,
                1,
                1,
                RunProgressionConfig.DefaultBiomeCount,
                RunProgressionConfig.DefaultRoomsPerBiome)
        {
        }

        public InputRecorder(RunManager run)
            : this(
                DefaultRunCapacity,
                GetDifficultyNumerator(run),
                GetDifficultyDenominator(run),
                GetFinalStageIndex(run),
                GetRoomsPerBiome(run))
        {
            _routeSource = run;
            _missileFamily = run.CurrentMissileFamily;
            _optionFormation = run.CurrentOptionFormation;
        }

        public InputRecorder(
            int runCapacity,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator)
            : this(
                runCapacity,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                RunProgressionConfig.DefaultBiomeCount,
                RunProgressionConfig.DefaultRoomsPerBiome)
        {
        }

        public InputRecorder(
            int runCapacity,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            int finalStageIndex)
            : this(
                runCapacity,
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator,
                finalStageIndex,
                1)
        {
        }

        public InputRecorder(
            int runCapacity,
            int difficultyMultiplierNumerator,
            int difficultyMultiplierDenominator,
            int biomeCount,
            int roomsPerBiome)
        {
            if (runCapacity < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(runCapacity));
            if (difficultyMultiplierNumerator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(difficultyMultiplierNumerator));
            if (difficultyMultiplierDenominator < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(difficultyMultiplierDenominator));
            if (biomeCount < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(biomeCount));
            if (roomsPerBiome < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(roomsPerBiome));

            int divisor = GreatestCommonDivisor(
                difficultyMultiplierNumerator,
                difficultyMultiplierDenominator);
            _difficultyMultiplierNumerator =
                difficultyMultiplierNumerator / divisor;
            _difficultyMultiplierDenominator =
                difficultyMultiplierDenominator / divisor;
            _finalStageIndex = biomeCount;
            _roomsPerBiome = roomsPerBiome;
            _missileFamily = MissileFamily.Straight;
            _optionFormation = OptionFormation.Trail;
            _runs = new InputRun[runCapacity];
            _recordedRouteChoices = new List<RouteChoice>();
        }

        public int Capacity => _runs.Length;
        public int RunCount => _runCount;
        public int TotalTicks => _totalTicks;
        public int DifficultyMultiplierNumerator =>
            _difficultyMultiplierNumerator;
        public int DifficultyMultiplierDenominator =>
            _difficultyMultiplierDenominator;
        public int FinalStageIndex => _finalStageIndex;
        public int BiomeCount => _finalStageIndex;
        public int RoomsPerBiome => _roomsPerBiome;

        public void Record(in InputCommand input)
        {
            if (_totalTicks == int.MaxValue)
                throw new InvalidOperationException(
                    "An input recording cannot exceed Int32.MaxValue ticks.");

            if (_runCount > 0
                && HasSameCommand(_runs[_runCount - 1], in input))
            {
                _runs[_runCount - 1].TickCount++;
                _totalTicks++;
                return;
            }

            if (_runCount == _runs.Length)
            {
                throw new InvalidOperationException(
                    "The input recording run capacity has been exhausted.");
            }
            _runs[_runCount++] = new InputRun(in input, 1);
            _totalTicks++;
        }

        public void Reset()
        {
            _runCount = 0;
            _totalTicks = 0;
            _recordedRouteChoices.Clear();
        }

        public void RecordRouteChoice(
            int stageIndex,
            int optionIndex,
            in RouteOption option)
        {
            RecordRouteChoice(stageIndex, 1, optionIndex, in option);
        }

        public void RecordRouteChoice(
            int biomeIndex,
            int roomIndex,
            int optionIndex,
            in RouteOption option)
        {
            if (_routeSource != null)
                throw new InvalidOperationException(
                    "A run-bound recorder reads route choices from its run.");
            if (_recordedRouteChoices.Count > 0
                && !IsAfter(
                    biomeIndex,
                    roomIndex,
                    _recordedRouteChoices[
                        _recordedRouteChoices.Count - 1]))
            {
                throw new ArgumentException(
                    "Route choices must be recorded in increasing stage order.",
                    nameof(biomeIndex));
            }
            _recordedRouteChoices.Add(new RouteChoice(
                biomeIndex,
                roomIndex,
                optionIndex,
                option.ThemeId,
                option.EncounterType));
        }

        /// <summary>
        /// Creates an independent serializer-facing copy. Empty recordings are
        /// rejected because they cannot reproduce a run.
        /// </summary>
        public InputRecordingData Export()
        {
            if (_totalTicks == 0)
                throw new InvalidOperationException(
                    "An empty input recording cannot be exported.");

            var exportedRuns = new InputRunData[_runCount];
            for (int i = 0; i < _runCount; i++)
            {
                InputRun run = _runs[i];
                exportedRuns[i] = new InputRunData
                {
                    moveX = run.MoveX,
                    moveY = run.MoveY,
                    fire = run.Fire,
                    activate = run.Activate,
                    tickCount = run.TickCount
                };
            }

            IReadOnlyList<RouteChoice> routeChoices =
                _routeSource != null
                    ? _routeSource.RouteChoiceHistory
                    : _recordedRouteChoices;
            var exportedRouteChoices =
                new RouteChoiceData[routeChoices.Count];
            for (int i = 0; i < routeChoices.Count; i++)
            {
                RouteChoice choice = routeChoices[i];
                exportedRouteChoices[i] = new RouteChoiceData
                {
                    stageIndex = choice.StageIndex,
                    biomeIndex = choice.BiomeIndex,
                    roomIndex = choice.RoomIndex,
                    optionIndex = choice.OptionIndex,
                    themeId = choice.ThemeId,
                    encounterType = (int)choice.EncounterType
                };
            }

            var data = new InputRecordingData
            {
                schemaVersion = InputRecordingData.CurrentSchemaVersion,
                totalTicks = _totalTicks,
                runs = exportedRuns,
                difficultyMultiplierNumerator =
                    _difficultyMultiplierNumerator,
                difficultyMultiplierDenominator =
                    _difficultyMultiplierDenominator,
                routeChoices = exportedRouteChoices,
                finalStageIndex = _finalStageIndex,
                biomeCount = _finalStageIndex,
                roomsPerBiome = _roomsPerBiome,
                missileFamily = (int)_missileFamily,
                optionFormation = (int)_optionFormation
            };
            SaveDataIntegrity.Seal(data);
            return data;
        }

        static int GetDifficultyNumerator(RunManager run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            return run.DifficultyMultiplierNumerator;
        }

        static int GetDifficultyDenominator(RunManager run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            return run.DifficultyMultiplierDenominator;
        }

        static int GetFinalStageIndex(RunManager run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            return run.FinalStageIndex;
        }

        static int GetRoomsPerBiome(RunManager run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            return run.RoomsPerBiome;
        }

        static bool IsAfter(
            int biomeIndex,
            int roomIndex,
            in RouteChoice previous)
        {
            return biomeIndex > previous.BiomeIndex
                || (biomeIndex == previous.BiomeIndex
                    && roomIndex > previous.RoomIndex);
        }

        static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }
            return left;
        }

        static bool HasSameCommand(
            in InputRun run,
            in InputCommand command)
        {
            return run.MoveX == command.MoveX
                && run.MoveY == command.MoveY
                && run.Fire == command.Fire
                && run.Activate == command.Activate;
        }

        struct InputRun
        {
            public InputRun(in InputCommand command, int tickCount)
            {
                MoveX = command.MoveX;
                MoveY = command.MoveY;
                Fire = command.Fire;
                Activate = command.Activate;
                TickCount = tickCount;
            }

            public int MoveX;
            public int MoveY;
            public bool Fire;
            public bool Activate;
            public int TickCount;
        }
    }

    /// <summary>
    /// Validates and snapshots an input recording, then enumerates exactly one
    /// InputCommand per recorded tick.
    /// </summary>
    public sealed class InputPlayback : IEnumerable<InputCommand>
    {
        readonly PlaybackRun[] _runs;
        readonly RouteChoice[] _routeChoices;
        readonly IReadOnlyList<RouteChoice> _routeChoiceView;

        public InputPlayback(InputRecordingData data)
        {
            data = SaveDataIntegrity.MigrateAndValidate(data);
            Validate(data);

            TotalTicks = data.totalTicks;
            int divisor = GreatestCommonDivisor(
                data.difficultyMultiplierNumerator,
                data.difficultyMultiplierDenominator);
            DifficultyMultiplierNumerator =
                data.difficultyMultiplierNumerator / divisor;
            DifficultyMultiplierDenominator =
                data.difficultyMultiplierDenominator / divisor;
            BiomeCount = data.biomeCount;
            RoomsPerBiome = data.roomsPerBiome;
            MissileFamily =
                (MissileFamily)data.missileFamily;
            OptionFormation =
                (OptionFormation)data.optionFormation;
            _runs = new PlaybackRun[data.runs.Length];
            for (int i = 0; i < data.runs.Length; i++)
            {
                InputRunData run = data.runs[i];
                _runs[i] = new PlaybackRun(
                    new InputCommand(
                        run.moveX,
                        run.moveY,
                        run.fire,
                        run.activate),
                    run.tickCount);
            }
            RouteChoiceData[] serializedChoices =
                data.routeChoices ?? Array.Empty<RouteChoiceData>();
            _routeChoices = new RouteChoice[serializedChoices.Length];
            for (int i = 0; i < serializedChoices.Length; i++)
            {
                RouteChoiceData choice = serializedChoices[i];
                _routeChoices[i] = new RouteChoice(
                    choice.biomeIndex,
                    choice.roomIndex,
                    choice.optionIndex,
                    choice.themeId,
                    (EncounterType)choice.encounterType);
            }
            _routeChoiceView = Array.AsReadOnly(_routeChoices);
        }

        public int TotalTicks { get; }
        public int RunCount => _runs.Length;
        public int DifficultyMultiplierNumerator { get; }
        public int DifficultyMultiplierDenominator { get; }
        public int FinalStageIndex => BiomeCount;
        public int BiomeCount { get; }
        public int RoomsPerBiome { get; }
        public MissileFamily MissileFamily { get; }
        public OptionFormation OptionFormation { get; }
        public IReadOnlyList<RouteChoice> RouteChoices =>
            _routeChoiceView;

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_runs);
        }

        IEnumerator<InputCommand>
            IEnumerable<InputCommand>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        static void Validate(InputRecordingData data)
        {
            if (data.schemaVersion
                != InputRecordingData.CurrentSchemaVersion)
            {
                throw Corrupted(
                    "The input recording schema version is unsupported.");
            }
            if (data.difficultyMultiplierNumerator < 1
                || data.difficultyMultiplierDenominator < 1)
            {
                throw Corrupted(
                    "The input recording difficulty multiplier "
                    + "must be positive.");
            }
            if (data.biomeCount < 1
                || data.finalStageIndex != data.biomeCount)
                throw Corrupted(
                    "The input recording biome count is invalid.");
            if (data.roomsPerBiome < 1)
                throw Corrupted(
                    "The input recording roomsPerBiome must be positive.");
            if (!Enum.IsDefined(
                    typeof(MissileFamily),
                    data.missileFamily)
                || !Enum.IsDefined(
                    typeof(OptionFormation),
                    data.optionFormation))
                throw Corrupted(
                    "The input recording weapon loadout is invalid.");
            if (data.totalTicks < 1)
                throw Corrupted(
                    "The input recording must contain at least one tick.");
            if (data.runs == null || data.runs.Length == 0)
                throw Corrupted(
                    "The input recording must contain at least one run.");
            if (data.runs.Length > data.totalTicks)
                throw Corrupted(
                    "The input recording has more runs than ticks.");

            long tickSum = 0;
            InputRunData previous = null;
            for (int i = 0; i < data.runs.Length; i++)
            {
                InputRunData run = data.runs[i];
                if (run == null)
                    throw Corrupted(
                        "Input recording runs cannot contain null.");
                if (run.moveX < -1 || run.moveX > 1
                    || run.moveY < -1 || run.moveY > 1)
                {
                    throw Corrupted(
                        "Input recording movement must be digital.");
                }
                if (run.tickCount < 1)
                    throw Corrupted(
                        "Input recording run lengths must be positive.");
                if (previous != null
                    && previous.moveX == run.moveX
                    && previous.moveY == run.moveY
                    && previous.fire == run.fire
                    && previous.activate == run.activate)
                {
                    throw Corrupted(
                        "Adjacent identical input runs are not canonical.");
                }

                tickSum += run.tickCount;
                if (tickSum > int.MaxValue)
                    throw Corrupted(
                        "The input recording tick count overflowed.");
                previous = run;
            }

            if (tickSum != data.totalTicks)
                throw Corrupted(
                    "Input recording run lengths do not match totalTicks.");

            if (data.routeChoices == null)
            {
                throw Corrupted(
                    "Input recording routeChoices cannot be null.");
            }
            RouteChoiceData[] choices =
                data.routeChoices ?? Array.Empty<RouteChoiceData>();
            int previousBiomeIndex = 0;
            int previousRoomIndex = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                RouteChoiceData choice = choices[i];
                if (choice == null
                    || choice.biomeIndex < 1
                    || choice.biomeIndex > data.biomeCount
                    || choice.roomIndex < 1
                    || choice.roomIndex > data.roomsPerBiome
                    || (choice.biomeIndex < previousBiomeIndex)
                    || (choice.biomeIndex == previousBiomeIndex
                        && choice.roomIndex <= previousRoomIndex)
                    || choice.stageIndex != choice.biomeIndex
                    || choice.optionIndex < 0
                    || choice.optionIndex
                        >= RunManager.MaximumRouteOptionCount
                    || string.IsNullOrEmpty(choice.themeId)
                    || !Enum.IsDefined(
                        typeof(EncounterType),
                        choice.encounterType))
                {
                    throw Corrupted(
                        "Input recording route choice history is invalid.");
                }
                previousBiomeIndex = choice.biomeIndex;
                previousRoomIndex = choice.roomIndex;
            }
        }

        static ArgumentException Corrupted(string message)
        {
            return new ArgumentException(message, "data");
        }

        static int GreatestCommonDivisor(int left, int right)
        {
            while (right != 0)
            {
                int remainder = left % right;
                left = right;
                right = remainder;
            }
            return left;
        }

        internal readonly struct PlaybackRun
        {
            public PlaybackRun(
                InputCommand command,
                int tickCount)
            {
                Command = command;
                TickCount = tickCount;
            }

            public InputCommand Command { get; }
            public int TickCount { get; }
        }

        public struct Enumerator : IEnumerator<InputCommand>
        {
            readonly PlaybackRun[] _runs;
            int _runIndex;
            int _remainingTicks;
            InputCommand _current;

            internal Enumerator(PlaybackRun[] runs)
            {
                _runs = runs;
                _runIndex = -1;
                _remainingTicks = 0;
                _current = default;
            }

            public InputCommand Current => _current;
            object IEnumerator.Current => _current;

            public bool MoveNext()
            {
                if (_remainingTicks > 0)
                {
                    _remainingTicks--;
                    return true;
                }

                int nextRun = _runIndex + 1;
                if (nextRun >= _runs.Length)
                    return false;

                _runIndex = nextRun;
                PlaybackRun run = _runs[nextRun];
                _current = run.Command;
                _remainingTicks = run.TickCount - 1;
                return true;
            }

            public void Reset()
            {
                _runIndex = -1;
                _remainingTicks = 0;
                _current = default;
            }

            public void Dispose()
            {
            }
        }
    }
}
