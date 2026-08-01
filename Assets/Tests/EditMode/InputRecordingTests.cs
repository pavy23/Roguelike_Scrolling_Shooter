using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class InputRecordingTests
    {
        [Test]
        public void RecordSerializePlayback_RoundTripsRunLengths()
        {
            var recorder = new InputRecorder(3);
            InputCommand none = InputCommand.None;
            var rightFireActivate =
                new InputCommand(1, 0, true, true);
            var down = new InputCommand(0, -1, false);
            Record(recorder, 3, in none);
            Record(recorder, 2, in rightFireActivate);
            Record(recorder, 4, in down);

            InputRecordingData exported = recorder.Export();
            InputRecordingData serialized = JsonRoundTrip(exported);
            var playback = new InputPlayback(serialized);
            var commands = new List<InputCommand>();
            foreach (InputCommand command in playback)
                commands.Add(command);

            Assert.AreEqual(3, recorder.RunCount);
            Assert.AreEqual(9, recorder.TotalTicks);
            Assert.AreEqual(3, exported.runs.Length);
            Assert.AreEqual(3, exported.runs[0].tickCount);
            Assert.AreEqual(2, exported.runs[1].tickCount);
            Assert.IsTrue(exported.runs[1].activate);
            Assert.AreEqual(4, exported.runs[2].tickCount);
            Assert.AreEqual(9, playback.TotalTicks);
            Assert.AreEqual(3, playback.RunCount);
            Assert.AreEqual(1, playback.DifficultyMultiplierNumerator);
            Assert.AreEqual(1, playback.DifficultyMultiplierDenominator);
            Assert.AreEqual(9, commands.Count);
            AssertCommand(commands[0], 0, 0, false, false);
            AssertCommand(commands[2], 0, 0, false, false);
            AssertCommand(commands[3], 1, 0, true, true);
            AssertCommand(commands[4], 1, 0, true, true);
            AssertCommand(commands[5], 0, -1, false, false);
            AssertCommand(commands[8], 0, -1, false, false);
        }

        [Test]
        public void ExportAndPlayback_SnapshotTheirSourceData()
        {
            var recorder = new InputRecorder();
            var left = new InputCommand(-1, 0, true);
            Record(recorder, 2, in left);
            InputRecordingData first = recorder.Export();
            var playback = new InputPlayback(first);

            first.runs[0].moveX = 1;
            first.runs[0].tickCount = 1;
            first.totalTicks = 1;
            InputRecordingData second = recorder.Export();
            var commands = new List<InputCommand>();
            foreach (InputCommand command in playback)
                commands.Add(command);

            Assert.AreEqual(-1, second.runs[0].moveX);
            Assert.AreEqual(2, second.runs[0].tickCount);
            Assert.AreEqual(2, second.totalTicks);
            Assert.AreEqual(2, commands.Count);
            AssertCommand(commands[0], -1, 0, true, false);
            AssertCommand(commands[1], -1, 0, true, false);
        }

        [Test]
        public void RecordedPlayback_ReproducesWholeRunStateTrajectoryHash()
        {
            ulong seed = DailySeed.FromDate(20260729);
            RunManager recordedRun = CreateRun(seed);
            var recorder = new InputRecorder();
            var recordedHasher = new DeterminismAuditHasher();

            for (int tick = 0; tick < 240; tick++)
            {
                InputCommand input = InputForTick(tick);
                recorder.Record(in input);
                recordedRun.Step(in input);
                recordedHasher.FoldRunState(recordedRun);
            }

            var playback = new InputPlayback(recorder.Export());
            RunManager replayedRun = CreateRun(seed);
            var replayedHasher = new DeterminismAuditHasher();
            int replayedTicks = 0;
            foreach (InputCommand input in playback)
            {
                replayedRun.Step(in input);
                replayedHasher.FoldRunState(replayedRun);
                replayedTicks++;
            }

            Assert.AreEqual(240, replayedTicks);
            Assert.AreEqual(recordedRun.Battle.Tick, replayedRun.Battle.Tick);
            Assert.AreEqual(recordedHasher.Hash, replayedHasher.Hash);
            Assert.AreEqual(
                1,
                replayedRun.PowerUpGauge.GetLevel(
                    PowerUpSlot.Speed));
        }

        [Test]
        public void AnalogCommands_RoundTripAndRemainRunLengthEncoded()
        {
            var recorder = new InputRecorder(2);
            InputCommand analog =
                InputCommand.Analog(7, -11, true, true, false);
            recorder.Record(in analog);
            recorder.Record(in analog);

            InputRecordingData data = recorder.Export();
            var commands = new List<InputCommand>();
            foreach (InputCommand command in new InputPlayback(data))
                commands.Add(command);

            AssertAll(() =>
            {
                Assert.AreEqual(
                    InputRecordingData.CurrentSchemaVersion,
                    data.schemaVersion);
                Assert.AreEqual(1, data.runs.Length);
                Assert.AreEqual(2, data.runs[0].tickCount);
                Assert.IsTrue(data.runs[0].useAnalogMovement);
                Assert.AreEqual(
                    7,
                    data.runs[0].analogDeltaXSubUnits);
                Assert.AreEqual(
                    -11,
                    data.runs[0].analogDeltaYSubUnits);
                Assert.AreEqual(2, commands.Count);
                Assert.IsTrue(commands[0].UseAnalogMovement);
                Assert.AreEqual(
                    7,
                    commands[0].AnalogDeltaXSubUnits);
                Assert.AreEqual(
                    -11,
                    commands[0].AnalogDeltaYSubUnits);
            });
        }

        [Test]
        public void DefaultCapacityCoversSixMinuteWorstCaseAnalogStage()
        {
            const int stageTicks =
                6 * 60 * SimSpace.TicksPerSecond;
            var recorder = new InputRecorder();

            for (int tick = 0; tick < stageTicks; tick++)
            {
                InputCommand input = InputCommand.Analog(
                    tick,
                    -tick,
                    false);
                recorder.Record(in input);
            }

            Assert.GreaterOrEqual(
                InputRecorder.DefaultRunCapacity,
                stageTicks);
            Assert.AreEqual(stageTicks, recorder.RunCount);
            Assert.AreEqual(stageTicks, recorder.TotalTicks);
        }

        [Test]
        public void SchemaNineRecording_MigratesAnalogMovementToZero()
        {
            var legacy = new InputRecordingData
            {
                schemaVersion = 9,
                totalTicks = 1,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = 1,
                        moveY = 0,
                        fire = true,
                        activate = false,
                        activateBomb = true,
                        tickCount = 1
                    }
                },
                difficultyMultiplierNumerator = 1,
                difficultyMultiplierDenominator = 1,
                routeChoices = Array.Empty<RouteChoiceData>(),
                finalStageIndex = 5,
                biomeCount = 5,
                roomsPerBiome = 4,
                missileFamily = (int)MissileFamily.Straight,
                optionFormation = (int)OptionFormation.Trail,
                lastColossalBossAtRunStart =
                    (int)ColossalBossKind.None,
                checksum = "EA9C549FF3889CD2"
            };

            InputRecordingData migrated =
                SaveDataIntegrity.MigrateAndValidate(legacy);
            var commands = new List<InputCommand>();
            foreach (InputCommand command in
                new InputPlayback(migrated))
            {
                commands.Add(command);
            }

            AssertAll(() =>
            {
                Assert.AreEqual(
                    InputRecordingData.CurrentSchemaVersion,
                    migrated.schemaVersion);
                Assert.AreEqual(1, commands.Count);
                Assert.IsFalse(commands[0].UseAnalogMovement);
                Assert.AreEqual(
                    0,
                    commands[0].AnalogDeltaXSubUnits);
                Assert.AreEqual(
                    0,
                    commands[0].AnalogDeltaYSubUnits);
                Assert.IsTrue(commands[0].ActivateBomb);
                Assert.IsTrue(
                    SaveDataIntegrity.HasValidChecksum(migrated));
            });
        }

        [Test]
        public void SchemaThirteenRecordingIsRejectedAfterGaugeReorder()
        {
            InputRecordingData legacy = ValidData();
            legacy.schemaVersion = 13;
            legacy.checksum = null;

            Assert.Throws<ArgumentException>(
                () => SaveDataIntegrity.MigrateAndValidate(legacy));
        }

        [Test]
        public void SchemaFourteenRecordingIsRejectedAfterShipGaugeChange()
        {
            InputRecordingData legacy = ValidData();
            legacy.schemaVersion = 14;
            legacy.checksum = null;

            Assert.Throws<ArgumentException>(
                () => SaveDataIntegrity.MigrateAndValidate(legacy));
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(legacy));
        }

        [Test]
        public void AnalogRecordedPlayback_ReproducesDeterminismHash()
        {
            BattleSimConfig config =
                BattleSimConfig.CreateDefault();
            BattleSim recorded =
                new BattleSim(config, new Rng(0x45045UL));
            var recorder = new InputRecorder(128);
            var recordedHasher = new DeterminismAuditHasher();

            for (int tick = 0; tick < 120; tick++)
            {
                InputCommand input = InputCommand.Analog(
                    (tick % 9 - 4) * 17,
                    (tick % 7 - 3) * 13,
                    tick % 5 != 0);
                recorder.Record(in input);
                recorded.Step(in input);
                recordedHasher.FoldBattleState(recorded);
            }

            BattleSim replayed =
                new BattleSim(config, new Rng(0x45045UL));
            var replayedHasher = new DeterminismAuditHasher();
            int replayedTicks = 0;
            foreach (InputCommand input in
                new InputPlayback(recorder.Export()))
            {
                replayed.Step(in input);
                replayedHasher.FoldBattleState(replayed);
                replayedTicks++;
            }

            AssertAll(() =>
            {
                Assert.AreEqual(120, replayedTicks);
                Assert.AreEqual(recorded.PlayerX, replayed.PlayerX);
                Assert.AreEqual(recorded.PlayerY, replayed.PlayerY);
                Assert.AreEqual(
                    recordedHasher.Hash,
                    replayedHasher.Hash);
            });
        }

        [Test]
        public void RecordedActivation_ReplaysPowerUpLevelChange()
        {
            ulong seed = 0xA6710A7EUL;
            RunManager recordedRun = CreateRun(seed);
            var recorder = new InputRecorder(4);
            InputCommand none = InputCommand.None;
            var activate = new InputCommand(0, 0, false, true);

            recorder.Record(in none);
            recorder.Record(in activate);
            recorder.Record(in activate);
            recorder.Record(in none);
            recordedRun.Step(in none);
            recordedRun.Step(in activate);
            recordedRun.Step(in activate);
            recordedRun.Step(in none);

            RunManager replayedRun = CreateRun(seed);
            foreach (InputCommand input in
                new InputPlayback(recorder.Export()))
            {
                replayedRun.Step(in input);
            }

            Assert.AreEqual(
                1,
                recordedRun.PowerUpGauge.GetLevel(
                    PowerUpSlot.Speed));
            Assert.AreEqual(
                recordedRun.PowerUpGauge.GetLevel(
                    PowerUpSlot.Speed),
                replayedRun.PowerUpGauge.GetLevel(
                    PowerUpSlot.Speed));
            Assert.AreEqual(
                recordedRun.PowerUpGauge.Cursor,
                replayedRun.PowerUpGauge.Cursor);
        }

        [Test]
        public void RunManagerActivation_RequiresARisingEdge()
        {
            RunManager run = CreateRun(0xED6EUL);
            var held = new InputCommand(0, 0, false, true);
            InputCommand released = InputCommand.None;

            run.Step(in held);
            run.PowerUpGauge.Collect();
            run.Step(in held);

            Assert.AreEqual(
                1,
                run.PowerUpGauge.GetLevel(PowerUpSlot.Speed));
            Assert.AreEqual(0, run.PowerUpGauge.Cursor);

            run.Step(in released);
            run.Step(in held);

            Assert.AreEqual(
                1,
                run.PowerUpGauge.GetLevel(PowerUpSlot.Speed));
            Assert.AreEqual(
                1,
                run.PowerUpGauge.GetProgress(PowerUpSlot.Speed));
            Assert.AreEqual(
                PowerUpGauge.NoSelection,
                run.PowerUpGauge.Cursor);
        }

        [Test]
        public void EmptyRecording_IsRejected()
        {
            var recorder = new InputRecorder();
            Assert.Throws<InvalidOperationException>(
                () => recorder.Export());

            Assert.Throws<ArgumentException>(
                () => new InputPlayback(new InputRecordingData
                {
                    schemaVersion =
                        InputRecordingData.CurrentSchemaVersion,
                    totalTicks = 0,
                    runs = Array.Empty<InputRunData>()
                }));
        }

        [Test]
        public void CorruptedRecording_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new InputPlayback(null));

            InputRecordingData badSchema = ValidData();
            badSchema.schemaVersion++;
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(badSchema));

            InputRecordingData missingRuns = ValidData();
            missingRuns.runs = null;
            AssertRejected(missingRuns);

            InputRecordingData nullRun = ValidData();
            nullRun.runs[0] = null;
            AssertRejected(nullRun);

            InputRecordingData invalidDirection = ValidData();
            invalidDirection.runs[0].moveX = 2;
            AssertRejected(invalidDirection);

            InputRecordingData zeroLength = ValidData();
            zeroLength.runs[0].tickCount = 0;
            AssertRejected(zeroLength);

            InputRecordingData mismatchedTotal = ValidData();
            mismatchedTotal.totalTicks++;
            AssertRejected(mismatchedTotal);

            InputRecordingData invalidDifficulty = ValidData();
            invalidDifficulty.difficultyMultiplierDenominator = 0;
            AssertRejected(invalidDifficulty);

            InputRecordingData duplicateAdjacentRuns = new InputRecordingData
            {
                schemaVersion =
                    InputRecordingData.CurrentSchemaVersion,
                totalTicks = 2,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = 0,
                        moveY = 0,
                        fire = false,
                        activate = false,
                        tickCount = 1
                    },
                    new InputRunData
                    {
                        moveX = 0,
                        moveY = 0,
                        fire = false,
                        activate = false,
                        tickCount = 1
                    }
                }
            };
            AssertRejected(duplicateAdjacentRuns);

            InputRecordingData overflowedTicks = new InputRecordingData
            {
                schemaVersion =
                    InputRecordingData.CurrentSchemaVersion,
                totalTicks = int.MaxValue,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = -1,
                        moveY = 0,
                        fire = false,
                        activate = false,
                        tickCount = int.MaxValue
                    },
                    new InputRunData
                    {
                        moveX = 1,
                        moveY = 0,
                        fire = false,
                        activate = true,
                        tickCount = 1
                    }
                }
            };
            AssertRejected(overflowedTicks);
        }

        [Test]
        public void SchemaTwoRecordingIsRejectedByPlaybackVersionGate()
        {
            InputRecordingData legacy = ValidData();
            legacy.schemaVersion = 2;
            legacy.checksum = null;
            legacy.difficultyMultiplierNumerator = 0;
            legacy.difficultyMultiplierDenominator = 0;

            Assert.Throws<ArgumentException>(
                () => new InputPlayback(legacy));
        }

        [Test]
        public void SchemaTwentyRecordingIsRejectedAfterObstacleJitterChange()
        {
            InputRecordingData legacy = ValidData();
            legacy.schemaVersion = 20;
            legacy.checksum = null;

            Assert.Throws<ArgumentException>(
                () => SaveDataIntegrity.MigrateAndValidate(legacy));
        }

        [Test]
        public void SchemaOneRecording_MigratesAllLaterFields()
        {
            var recorder = new InputRecorder();
            recorder.Record(new InputCommand(1, 0, true, true));
            InputRecordingData legacy = recorder.Export();
            legacy.schemaVersion = 1;
            legacy.checksum = null;
            legacy.difficultyMultiplierNumerator = 0;
            legacy.difficultyMultiplierDenominator = 0;
            legacy.routeChoices = null;

            InputRecordingData migrated =
                SaveDataIntegrity.MigrateAndValidate(legacy);
            var playback = new InputPlayback(migrated);

            Assert.AreEqual(
                InputRecordingData.CurrentSchemaVersion,
                migrated.schemaVersion);
            Assert.AreEqual(1, playback.DifficultyMultiplierNumerator);
            Assert.AreEqual(1, playback.DifficultyMultiplierDenominator);
            Assert.AreEqual(0, playback.RouteChoices.Count);
            foreach (InputCommand input in playback)
                Assert.IsFalse(input.Activate);
            Assert.IsTrue(
                SaveDataIntegrity.HasValidChecksum(migrated));
        }

        [Test]
        public void CurrentRecordingChecksumMismatch_IsClearlyRejected()
        {
            var recorder = new InputRecorder();
            recorder.Record(InputCommand.None);
            InputRecordingData corrupted = recorder.Export();
            corrupted.totalTicks++;

            ArgumentException error =
                Assert.Throws<ArgumentException>(
                    () => new InputPlayback(corrupted));

            StringAssert.Contains("checksum", error.Message);
        }

        [Test]
        public void Reset_ReusesRecorderForANewRecording()
        {
            var recorder = new InputRecorder(1);
            var first = new InputCommand(-1, 0, false);
            var second = new InputCommand(1, 1, true);
            recorder.Record(in first);
            recorder.Reset();
            Record(recorder, 3, in second);

            InputRecordingData data = recorder.Export();

            Assert.AreEqual(1, recorder.RunCount);
            Assert.AreEqual(3, recorder.TotalTicks);
            Assert.AreEqual(1, data.runs.Length);
            Assert.AreEqual(3, data.runs[0].tickCount);
            Assert.AreEqual(1, data.runs[0].moveX);
            Assert.AreEqual(1, data.runs[0].moveY);
            Assert.IsTrue(data.runs[0].fire);
            Assert.IsFalse(data.runs[0].activate);
        }

        [Test]
        public void RunCapacityExhaustion_DoesNotMutateRecording()
        {
            var recorder = new InputRecorder(1);
            var left = new InputCommand(-1, 0, false);
            var right = new InputCommand(1, 0, false);
            recorder.Record(in left);

            Assert.Throws<InvalidOperationException>(
                () => recorder.Record(in right));

            InputRecordingData data = recorder.Export();
            Assert.AreEqual(1, recorder.Capacity);
            Assert.AreEqual(1, recorder.RunCount);
            Assert.AreEqual(1, recorder.TotalTicks);
            Assert.AreEqual(-1, data.runs[0].moveX);
        }

        static InputRecordingData JsonRoundTrip(InputRecordingData data)
        {
            var serializer =
                new DataContractJsonSerializer(typeof(InputRecordingData));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, data);
                stream.Position = 0;
                return (InputRecordingData)serializer.ReadObject(stream);
            }
        }

        static InputRecordingData ValidData()
        {
            return new InputRecordingData
            {
                schemaVersion =
                    InputRecordingData.CurrentSchemaVersion,
                totalTicks = 1,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = 0,
                        moveY = 0,
                        fire = false,
                        activate = false,
                        tickCount = 1
                    }
                },
                routeChoices = Array.Empty<RouteChoiceData>(),
                finalStageIndex =
                    RunProgressionConfig.DefaultFinalStageIndex
            };
        }

        static void AssertRejected(InputRecordingData data)
        {
            if (data.routeChoices == null)
                data.routeChoices = Array.Empty<RouteChoiceData>();
            if (data.finalStageIndex < 1)
            {
                data.finalStageIndex =
                    RunProgressionConfig.DefaultFinalStageIndex;
            }
            SaveDataIntegrity.Seal(data);
            Assert.Throws<ArgumentException>(
                () => new InputPlayback(data));
        }

        static void AssertCommand(
            InputCommand command,
            int moveX,
            int moveY,
            bool fire,
            bool activate)
        {
            Assert.AreEqual(moveX, command.MoveX);
            Assert.AreEqual(moveY, command.MoveY);
            Assert.AreEqual(fire, command.Fire);
            Assert.AreEqual(activate, command.Activate);
        }

        static void AssertAll(Action assert) => assert();

        static void Record(
            InputRecorder recorder,
            int count,
            in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                recorder.Record(in input);
        }

        static InputCommand InputForTick(int tick)
        {
            int phase = tick / 12;
            return new InputCommand(
                phase % 3 - 1,
                phase % 5 < 2 ? 1 : phase % 5 > 3 ? -1 : 0,
                phase % 4 != 0,
                tick >= 20 && tick < 23);
        }

        static RunManager CreateRun(ulong seed)
        {
            var weapon = new WeaponDefinition(
                "shot",
                1,
                3,
                100,
                1,
                0,
                0);
            var gauge = PowerUpGauge.CreateDefault();
            gauge.Collect();
            return new RunManager(
                seed,
                new ReplayStageGenerator(),
                BattleSimConfig.CreateDefault(),
                new BattleContent(
                    Array.Empty<EnemyDefinition>(),
                    new[] { weapon },
                    weapon.Id),
                gauge);
        }

        sealed class ReplayStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                return new StagePlan(
                    new[]
                    {
                        new StageSegment(
                            "replay",
                            1000,
                            Array.Empty<SpawnEvent>(),
                            1,
                            1,
                            new[] { 1 })
                    },
                    "legacy",
                    1,
                    1,
                    1);
            }
        }
    }
}
