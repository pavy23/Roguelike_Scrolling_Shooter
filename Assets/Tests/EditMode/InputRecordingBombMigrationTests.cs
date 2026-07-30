using System.Linq;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class InputRecordingBombMigrationTests
    {
        [Test]
        public void RecorderPersistsBombBitAndKeepsRunsCanonical()
        {
            var recorder = new InputRecorder(4);
            var none = new InputCommand(0, 0, false, false, false);
            var bomb = new InputCommand(0, 0, false, false, true);
            recorder.Record(in none);
            recorder.Record(in bomb);
            recorder.Record(in bomb);

            InputRecordingData data = recorder.Export();
            InputCommand[] playback =
                new InputPlayback(data).ToArray();

            Assert.AreEqual(
                InputRecordingData.CurrentSchemaVersion,
                data.schemaVersion);
            Assert.AreEqual(2, data.runs.Length);
            Assert.IsFalse(playback[0].ActivateBomb);
            Assert.IsTrue(playback[1].ActivateBomb);
            Assert.IsTrue(playback[2].ActivateBomb);
        }

        [Test]
        public void SchemaEightReplayMigratesBombBitToFalse()
        {
            var legacy = new InputRecordingData
            {
                schemaVersion = 8,
                totalTicks = 2,
                runs = new[]
                {
                    new InputRunData
                    {
                        moveX = 0,
                        moveY = 0,
                        fire = true,
                        activate = false,
                        activateBomb = true,
                        tickCount = 2
                    }
                },
                difficultyMultiplierNumerator = 1,
                difficultyMultiplierDenominator = 1,
                routeChoices = new RouteChoiceData[0],
                finalStageIndex =
                    RunProgressionConfig.DefaultBiomeCount,
                biomeCount =
                    RunProgressionConfig.DefaultBiomeCount,
                roomsPerBiome =
                    RunProgressionConfig.DefaultRoomsPerBiome,
                missileFamily = (int)MissileFamily.Straight,
                optionFormation = (int)OptionFormation.Trail,
                lastColossalBossAtRunStart =
                    (int)ColossalBossKind.None,
                checksum = null
            };

            InputCommand[] playback =
                new InputPlayback(legacy).ToArray();

            Assert.AreEqual(2, playback.Length);
            Assert.IsFalse(playback[0].ActivateBomb);
            Assert.IsFalse(playback[1].ActivateBomb);
        }
    }
}
