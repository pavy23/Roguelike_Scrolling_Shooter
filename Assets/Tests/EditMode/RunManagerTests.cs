using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public class RunManagerTests
    {
        [Test]
        public void PlayerDeathEndsRunAndStopsFurtherSimulation()
        {
            var manager = CreateManager(
                11UL,
                new TestStageGenerator(true, 5),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            Assert.AreEqual(1, manager.RunNumber);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(RunState.Playing, manager.State);

            manager.Step(in none);

            Assert.AreEqual(0, manager.Battle.PlayerHp);
            Assert.AreEqual(RunState.RunOver, manager.State);
            Assert.AreEqual(1, manager.Battle.Tick);

            manager.Step(in none);
            Assert.AreEqual(1, manager.Battle.Tick);
        }

        [Test]
        public void DevRunStartsAtRequestedStageWithNaturalDifficulty()
        {
            var generator = new TestStageGenerator(false, 10);
            var gauge = PowerUpGauge.CreateDefault();
            var manager = new RunManager(
                0x96UL,
                generator,
                CreateConfig(),
                CreateContent(),
                gauge,
                new RunConfig(3));

            Assert.AreEqual(3, manager.StageIndex);
            Assert.AreEqual(3, manager.BiomeIndex);
            Assert.AreEqual(1, manager.RoomIndex);
            Assert.AreEqual(3, manager.Difficulty);
            Assert.IsTrue(manager.DevFlagsActive);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(0, manager.Statistics.RoomsCleared);
            Assert.AreEqual(3, generator.Calls[0].StageIndex);
            Assert.AreEqual(3, generator.Calls[0].Difficulty);
            for (int i = 0; i < PowerUpGauge.SlotCount; i++)
                Assert.AreEqual(
                    0,
                    gauge.GetLevel((PowerUpSlot)i));
        }

        [Test]
        public void PlayerInvulnerableIgnoresHitsAndMarksDevRun()
        {
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 1;
            config.PlayerInvulnerable = true;
            var manager = new RunManager(
                0x9601UL,
                new ShieldHitStageGenerator(),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            Step(manager, 5, in none);

            Assert.IsTrue(manager.DevFlagsActive);
            Assert.IsTrue(manager.Battle.IsPlayerAlive);
            Assert.AreEqual(1, manager.Battle.PlayerHp);
            Assert.AreEqual(1, manager.Battle.ShieldStock);
            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreEqual(5, manager.Battle.Tick);
        }

        [Test]
        public void DefaultDevFlagsPreserveLegacyHashAndSuspendContract()
        {
            var legacy = new RunManager(
                0x9602UL,
                new TestStageGenerator(false, 10),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault());
            var explicitDefault = new RunManager(
                0x9602UL,
                new TestStageGenerator(false, 10),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                RunConfig.CreateDefault());
            var legacyHash = new DeterminismAuditHasher();
            var explicitHash = new DeterminismAuditHasher();

            legacyHash.FoldRunState(legacy);
            explicitHash.FoldRunState(explicitDefault);

            Assert.IsFalse(new BattleSimConfig().PlayerInvulnerable);
            Assert.AreEqual(1, RunConfig.CreateDefault().StartStageIndex);
            Assert.IsFalse(legacy.DevFlagsActive);
            Assert.IsFalse(explicitDefault.DevFlagsActive);
            Assert.AreEqual(legacyHash.Hash, explicitHash.Hash);
            Assert.DoesNotThrow(() => legacy.ExportSuspendData());
            Assert.DoesNotThrow(
                () => explicitDefault.ExportSuspendData());
        }

        [Test]
        public void DevRunsRejectSuspendExportWithoutChangingSchema()
        {
            var manager = new RunManager(
                0x9603UL,
                new TestStageGenerator(false, 10),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new RunConfig(3));

            InvalidOperationException exception = Assert.Throws<
                InvalidOperationException>(
                () => manager.ExportSuspendData());

            StringAssert.Contains(
                "Developer runs cannot be suspended",
                exception.Message);
        }

        [Test]
        public void StartStageMustFitConfiguredProgression()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RunConfig(0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RunManager(
                    0x9604UL,
                    new TestStageGenerator(false, 10),
                    CreateConfig(),
                    CreateContent(),
                    PowerUpGauge.CreateDefault(),
                    new MetaProgression(1, 1),
                    StageDifficultyCurve.CreateDefault(),
                    null,
                    null,
                    1,
                    1,
                    new RunProgressionConfig(2, 1),
                    new RunConfig(3)));
        }

        [Test]
        public void DevRunRestartReturnsToConfiguredStartStage()
        {
            var manager = new RunManager(
                0x9605UL,
                new TestStageGenerator(true, 5),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new RunConfig(3));
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            Assert.AreEqual(RunState.RunOver, manager.State);

            manager.Restart(0x9606UL);

            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreEqual(3, manager.StageIndex);
            Assert.AreEqual(1, manager.RoomIndex);
            Assert.IsTrue(manager.DevFlagsActive);
        }


        /// <summary>
        /// 실드가 1에서 0이 되며 한 대를 막아 내고, **무적이 끝난 뒤** 다음 유효타에
        /// 런이 끝난다.
        ///
        /// 예전에는 "정확히 몇 번째 틱에 죽는다"를 박아 뒀는데, 그 숫자는 램머가
        /// 날아오는 데 걸리는 시간이 그때의 무적 시간(0.3초)과 **우연히** 맞아떨어져
        /// 나온 값이었다. 무적을 2.5초로 늘리자 그 우연이 깨져 테스트가 실패했다 —
        /// 검증하려던 성질은 멀쩡한데도. 그래서 성질만 검증한다.
        /// </summary>
        [Test]
        public void ShieldOneToZeroSurvivesThenNextEffectiveHitEndsRun()
        {
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 1;
            config.PlayerHitInvulnerabilityTicks =
                BattleSimConfig.DefaultPlayerHitInvulnerabilityTicks;
            var manager = new RunManager(
                12UL,
                new ShieldHitStageGenerator(),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            manager.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(0, manager.Battle.ShieldStock);
                Assert.IsTrue(manager.Battle.IsPlayerAlive);
                Assert.AreEqual(1, manager.Battle.PlayerHp);
                Assert.AreEqual(
                    BattleSimConfig.DefaultPlayerHitInvulnerabilityTicks,
                    manager.Battle.PlayerInvulnerabilityTicksRemaining);
                Assert.AreEqual(RunState.Playing, manager.State);
            });

            // 무적이 도는 동안에는 무슨 일이 있어도 죽지 않는다.
            int invulnerableTicksObserved = 0;
            while (manager.Battle.PlayerInvulnerabilityTicksRemaining > 0)
            {
                Assert.IsTrue(
                    manager.Battle.IsPlayerAlive,
                    "무적 중에 죽었다.");
                manager.Step(in none);
                invulnerableTicksObserved++;
                if (invulnerableTicksObserved
                    > BattleSimConfig.DefaultPlayerHitInvulnerabilityTicks + 8)
                    Assert.Fail("무적이 끝나지 않는다.");
            }

            Assert.AreEqual(
                BattleSimConfig.DefaultPlayerHitInvulnerabilityTicks,
                invulnerableTicksObserved,
                "무적은 설정된 틱만큼 정확히 지속되어야 한다.");

            // 무적이 풀린 뒤에는 다음 유효타에 런이 끝난다.
            for (int tick = 0; tick < 240 && manager.State == RunState.Playing; tick++)
                manager.Step(in none);

            AssertAll(() =>
            {
                Assert.IsFalse(manager.Battle.IsPlayerAlive);
                Assert.AreEqual(0, manager.Battle.PlayerHp);
                Assert.AreEqual(RunState.RunOver, manager.State);
            });
        }

        [Test]
        public void PlayerDeathWinsWhenRoomClearOccursOnSameTick()
        {
            var manager = CreateManager(
                13UL,
                new LethalBoundaryStageGenerator(),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            manager.Step(in none);

            AssertAll(() =>
            {
                Assert.AreEqual(1, manager.Battle.Tick);
                Assert.IsFalse(manager.Battle.IsPlayerAlive);
                Assert.AreEqual(RunState.RunOver, manager.State);
                Assert.AreEqual(1, manager.BiomeIndex);
                Assert.AreEqual(1, manager.RoomIndex);
                Assert.IsFalse(manager.IsBiomeBoss);
                Assert.AreEqual(0, manager.Statistics.RoomsCleared);
                Assert.AreEqual(0, manager.Statistics.StagesCleared);
            });
        }

        [Test]
        public void CompletedRoomsKeepDifficultyAtCurrentBiome()
        {
            var generator = new TestStageGenerator(false, 1, 2);
            var curve = new StageDifficultyCurve(2, 2, 5);
            var manager = new RunManager(
                22UL,
                generator,
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(1.0),
                curve);
            InputCommand none = InputCommand.None;

            AssertCall(generator.Calls[0], 22UL, 1, 2);
            Step(manager, 3, in none);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(0, manager.Battle.Tick);
            Assert.AreEqual(2, manager.Difficulty);
            Assert.AreEqual(1, generator.Calls[1].StageIndex);
            Assert.AreEqual(2, generator.Calls[1].Difficulty);

            Step(manager, 3, in none);
            Assert.AreEqual(RunState.AwaitingReward, manager.State);
            Assert.AreEqual(2, manager.RewardOptions.Count);
            manager.ChooseReward(0);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(3, manager.RoomIndex);
            Assert.AreEqual(2, manager.Difficulty);
            Assert.AreEqual(1, generator.Calls[2].StageIndex);
            Assert.AreEqual(2, generator.Calls[2].Difficulty);

            Step(manager, 3, in none);
            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(3, manager.RoomIndex);
            Assert.IsTrue(manager.IsBiomeBoss);
            Assert.AreEqual(2, manager.Difficulty);
        }

        [Test]
        public void StageTransitionsCarryAndAccumulateBattleScores()
        {
            var manager = new RunManager(
                88UL,
                new ScoreStageGenerator(false),
                CreateConfig(),
                CreateScoringContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 2, in fire);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(75L, manager.TotalScore);
            Assert.AreEqual(0L, manager.Battle.Score);
            Assert.AreEqual(2L, manager.Statistics.ShotsFired);
            Assert.AreEqual(1L, manager.Statistics.ShotsHit);
            Assert.AreEqual(1L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(1, manager.Statistics.RoomsCleared);

            Step(manager, 2, in fire);
            Assert.AreEqual(RunState.AwaitingReward, manager.State);
            manager.ChooseReward(0);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(3, manager.RoomIndex);
            Assert.AreEqual(150L, manager.TotalScore);
            Assert.AreEqual(4L, manager.Statistics.ShotsFired);
            Assert.AreEqual(2L, manager.Statistics.ShotsHit);
            Assert.AreEqual(2L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(2, manager.Statistics.RoomsCleared);
        }

        [Test]
        public void BombStatisticsAccumulateAcrossRoomsAndSuspend()
        {
            BattleSimConfig config = CreateConfig();
            config.StartingBombStock = 2;
            var manager = new RunManager(
                0xB094UL,
                new TestStageGenerator(false, 2),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            var bomb = new InputCommand(
                0,
                0,
                false,
                false,
                true);

            manager.Step(in bomb);
            manager.Step(InputCommand.None);

            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(1L, manager.Statistics.BombsUsed);
            RunSuspendData suspend = manager.ExportSuspendData();
            Assert.AreEqual(1L, suspend.bombsUsed);
            BattleSimConfig resumeConfig = CreateConfig();
            resumeConfig.StartingBombStock = 2;
            RunManager resumed = RunManager.ResumeFromSuspendData(
                suspend,
                new TestStageGenerator(false, 2),
                resumeConfig,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            Assert.AreEqual(1L, resumed.Statistics.BombsUsed);

            resumed.Step(in bomb);
            resumed.Step(InputCommand.None);
            Assert.AreEqual(2L, resumed.Statistics.BombsUsed);
        }

        [Test]
        public void StageTransitionCarriesCollectedCapsules()
        {
            BattleSimConfig config = CreateConfig();
            config.CapsuleHalfWidth = 1;
            var manager = new RunManager(
                91UL,
                new ScoreStageGenerator(false),
                config,
                CreateDroppingContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 2, in fire);

            Assert.AreEqual(1, manager.BiomeIndex);
            Assert.AreEqual(2, manager.RoomIndex);
            Assert.AreEqual(1L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
            Assert.AreEqual(1, manager.Statistics.RoomsCleared);
        }

        [Test]
        public void RestartAppliesInjectedDeathCarryAndBuildsFreshFirstStage()
        {
            var initialGauge = PowerUpGauge.CreateDefault();
            initialGauge.ImportLevels(new[] { 5, 3, 4, 3 });
            var generator = new TestStageGenerator(true, 5);
            var manager = new RunManager(
                33UL,
                generator,
                CreateConfig(),
                CreateContent(),
                initialGauge,
                new MetaProgression(0.5),
                StageDifficultyCurve.CreateDefault());
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(44UL);

            Assert.AreEqual(2, manager.RunNumber);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(44UL, manager.RunSeed);
            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreNotSame(initialGauge, manager.PowerUpGauge);
            CollectionAssert.AreEqual(
                new[] { 2, 1, 2, 1, 0, 0, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
            AssertCall(generator.Calls[1], 44UL, 1, 1);
        }

        [Test]
        public void RestartResetsTotalScore()
        {
            var manager = new RunManager(
                89UL,
                new ScoreStageGenerator(true),
                CreateConfig(),
                CreateScoringContent(),
                PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            Step(manager, 3, in fire);

            Assert.AreEqual(RunState.RunOver, manager.State);
            Assert.AreEqual(75L, manager.TotalScore);
            Assert.AreEqual(3L, manager.Statistics.ShotsFired);
            Assert.AreEqual(1L, manager.Statistics.ShotsHit);
            Assert.AreEqual(1L, manager.Statistics.Kills);

            manager.Restart(90UL);

            Assert.AreEqual(RunState.Playing, manager.State);
            Assert.AreEqual(0L, manager.TotalScore);
            Assert.AreEqual(0L, manager.Battle.Score);
            Assert.AreEqual(0L, manager.Statistics.ShotsFired);
            Assert.AreEqual(0L, manager.Statistics.ShotsHit);
            Assert.AreEqual(0L, manager.Statistics.Kills);
            Assert.AreEqual(0L, manager.Statistics.CapsulesCollected);
            Assert.AreEqual(0L, manager.Statistics.GrazeCount);
            Assert.AreEqual(0L, manager.Statistics.BombsUsed);
            Assert.AreEqual(0, manager.Statistics.StagesCleared);
        }

        [Test]
        public void DefaultRestartCarryPreservesAllPowerUpLevels()
        {
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 3, 2, 1, 2 });
            var manager = CreateManager(
                55UL,
                new TestStageGenerator(true, 5),
                gauge);
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(56UL);

            CollectionAssert.AreEqual(
                new[] { 3, 2, 1, 2, 0, 0, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
        }

        [Test]
        public void RestartingWithSameSeedRebuildsIdenticalFirstStage()
        {
            var manager = CreateManager(
                77UL,
                new TestStageGenerator(true, 5),
                PowerUpGauge.CreateDefault());
            string firstSegmentId = manager.StagePlan.Segments[0].SegmentId;
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(77UL);

            Assert.AreEqual(firstSegmentId, manager.StagePlan.Segments[0].SegmentId);
            Assert.AreEqual(1, manager.StageIndex);
            Assert.AreEqual(1, manager.Difficulty);
            Assert.AreEqual(0, manager.Battle.Tick);
        }

        [Test]
        public void SameSeedAndInputsReproduceStagesAndDelayedOptionTrajectory()
        {
            BattleSimConfig config = CreateConfig();
            config.OptionFollowDelayTicks = 2;
            var firstGauge = PowerUpGauge.CreateDefault();
            var secondGauge = PowerUpGauge.CreateDefault();
            firstGauge.ImportLevels(new[] { 0, 0, 2, 0 });
            secondGauge.ImportLevels(new[] { 0, 0, 2, 0 });
            var first = new RunManager(
                0xC0FFEEUL,
                new TestStageGenerator(false, 2, 2),
                config,
                CreateContent(),
                firstGauge);
            var second = new RunManager(
                0xC0FFEEUL,
                new TestStageGenerator(false, 2, 2),
                config,
                CreateContent(),
                secondGauge);

            for (int tick = 0; tick < 12; tick++)
            {
                var input = new InputCommand(
                    tick % 4 < 2 ? 1 : -1,
                    tick % 3 == 0 ? 1 : 0,
                    false);
                first.Step(in input);
                second.Step(in input);
                AssertManagersEqual(first, second, tick);
            }
        }

        [Test]
        public void ShipAppliesExactMovementMultiplierAndStartingLevels()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedNumerator = 3;
            config.PlayerSpeedDenominator = 2;
            var gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 1, 0, 0, 0 });
            var ship = new ShipDefinition(
                "swift",
                "Swift",
                4,
                3,
                new[] { 2, 1, 0, 0 },
                100);
            var manager = new RunManager(
                91UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                gauge,
                null,
                ship);
            var moveRight = new InputCommand(1, 0, false);

            manager.Step(in moveRight);

            Assert.AreSame(ship, manager.Ship);
            Assert.AreEqual(2, manager.Battle.PlayerX);
            CollectionAssert.AreEqual(
                new[] { 2, 1, 0, 0, 0, 0, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
        }

        [Test]
        public void AnalogInputMovesPlayerThroughRunManager()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 10;
            var manager = new RunManager(
                0x4601UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            InputCommand input =
                InputCommand.Analog(3, -4, false);

            manager.Step(in input);

            AssertAll(() =>
            {
                Assert.AreEqual(3, manager.Battle.PlayerX);
                Assert.AreEqual(-4, manager.Battle.PlayerY);
            });
        }

        [Test]
        public void AnalogInputClampsAtPlayerSpeedThroughRunManager()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 5;
            var manager = new RunManager(
                0x4602UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            InputCommand input =
                InputCommand.Analog(30, 40, false);

            manager.Step(in input);

            AssertAll(() =>
            {
                Assert.AreEqual(3, manager.Battle.PlayerX);
                Assert.AreEqual(4, manager.Battle.PlayerY);
                Assert.LessOrEqual(
                    (long)manager.Battle.PlayerX
                        * manager.Battle.PlayerX
                    + (long)manager.Battle.PlayerY
                        * manager.Battle.PlayerY,
                    25L);
            });
        }

        [Test]
        public void AnalogZeroDeltaStopsAndOverridesDigitalThroughRunManager()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 10;
            var manager = new RunManager(
                0x4603UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            var input = new InputCommand(
                1,
                -1,
                false,
                false,
                false,
                0,
                0);

            manager.Step(in input);

            AssertAll(() =>
            {
                Assert.AreEqual(0, manager.Battle.PlayerX);
                Assert.AreEqual(0, manager.Battle.PlayerY);
            });
        }

        [Test]
        public void AnalogInputPreservesBombActivationThroughRunManager()
        {
            BattleSimConfig config = CreateConfig();
            config.PlayerSpeedPerTick = 10;
            config.StartingBombStock = 1;
            var manager = new RunManager(
                0x4604UL,
                new TestStageGenerator(false, 5),
                config,
                CreateContent(),
                PowerUpGauge.CreateDefault());
            InputCommand input =
                InputCommand.Analog(
                    2,
                    1,
                    false,
                    activateBomb: true);

            manager.Step(in input);

            AssertAll(() =>
            {
                Assert.AreEqual(0, manager.Battle.BombStock);
                Assert.AreEqual(1L, manager.Statistics.BombsUsed);
                Assert.AreEqual(2, manager.Battle.PlayerX);
                Assert.AreEqual(1, manager.Battle.PlayerY);
            });
        }

        [Test]
        public void RestartNeverDropsBelowShipStartingLevels()
        {
            var ship = new ShipDefinition(
                "armed",
                "Armed",
                1,
                1,
                new[] { 2, 1, 0, 0 },
                0);
            var manager = new RunManager(
                92UL,
                new TestStageGenerator(true, 5),
                CreateConfig(),
                CreateContent(),
                PowerUpGauge.CreateDefault(),
                new MetaProgression(0.0),
                StageDifficultyCurve.CreateDefault(),
                null,
                ship);
            InputCommand none = InputCommand.None;

            manager.Step(in none);
            manager.Restart(93UL);

            CollectionAssert.AreEqual(
                new[] { 2, 1, 0, 0, 0, 0, 0, 0 },
                manager.PowerUpGauge.ExportLevels());
        }

        static RunManager CreateManager(
            ulong seed,
            IStageGenerator generator,
            PowerUpGauge gauge)
        {
            return new RunManager(
                seed,
                generator,
                CreateConfig(),
                CreateContent(),
                gauge);
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 1,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -100,
                PlayerMaxX = 100,
                PlayerMinY = -100,
                PlayerMaxY = 100,
                BulletDespawnX = 100,
                EnemyDespawnX = -100,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                StartingShieldStock = 0,
                PlayerHitInvulnerabilityTicks = 0,
                PlayerHalfWidth = 0,
                PlayerHalfHeight = 0,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1
            };
        }

        static BattleContent CreateContent()
        {
            var enemy = new EnemyDefinition(
                "rammer",
                1,
                10,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var weapon = new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0);
            return new BattleContent(new[] { enemy }, new[] { weapon }, weapon.Id);
        }

        static BattleContent CreateScoringContent()
        {
            var scored = new EnemyDefinition(
                "scored", "Scored", 1, 0, 75, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var lethal = new EnemyDefinition(
                "lethal", "Lethal", 10, 1, 0, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 0, 1, 1);
            var weapon = new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { scored, lethal },
                new[] { weapon },
                weapon.Id);
        }

        static BattleContent CreateDroppingContent()
        {
            var scored = new EnemyDefinition(
                "scored", "Scored", 1, 0, 75, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 1, 0, 1, 1);
            var weapon = new WeaponDefinition("shot", 1, 1, 1, 1, 0, 0);
            return new BattleContent(
                new[] { scored },
                new[] { weapon },
                weapon.Id);
        }

        static void Step(RunManager manager, int count, in InputCommand input)
        {
            for (int i = 0; i < count; i++)
                manager.Step(in input);
        }

        static void AssertCall(
            GenerationCall call,
            ulong seed,
            int stageIndex,
            int difficulty)
        {
            Assert.AreEqual(seed, call.Seed);
            Assert.AreEqual(stageIndex, call.StageIndex);
            Assert.AreEqual(difficulty, call.Difficulty);
        }

        static void AssertAll(Action assert) => assert();

        static void AssertManagersEqual(
            RunManager expected,
            RunManager actual,
            int sourceTick)
        {
            Assert.AreEqual(expected.RunNumber, actual.RunNumber, $"source tick {sourceTick}");
            Assert.AreEqual(expected.StageIndex, actual.StageIndex, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Difficulty, actual.Difficulty, $"source tick {sourceTick}");
            Assert.AreEqual(expected.State, actual.State, $"source tick {sourceTick}");
            Assert.AreEqual(expected.TotalScore, actual.TotalScore, $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.ShotsFired,
                actual.Statistics.ShotsFired,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.ShotsHit,
                actual.Statistics.ShotsHit,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.Kills,
                actual.Statistics.Kills,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.CapsulesCollected,
                actual.Statistics.CapsulesCollected,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.GrazeCount,
                actual.Statistics.GrazeCount,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.BombsUsed,
                actual.Statistics.BombsUsed,
                $"source tick {sourceTick}");
            Assert.AreEqual(
                expected.Statistics.StagesCleared,
                actual.Statistics.StagesCleared,
                $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.Tick, actual.Battle.Tick, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.PlayerX, actual.Battle.PlayerX, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.PlayerY, actual.Battle.PlayerY, $"source tick {sourceTick}");
            Assert.AreEqual(expected.Battle.Options.Count, actual.Battle.Options.Count);

            for (int i = 0; i < expected.Battle.Options.Count; i++)
            {
                Assert.AreEqual(expected.Battle.Options[i].Index, actual.Battle.Options[i].Index);
                Assert.AreEqual(expected.Battle.Options[i].X, actual.Battle.Options[i].X);
                Assert.AreEqual(expected.Battle.Options[i].Y, actual.Battle.Options[i].Y);
            }
        }

        sealed class ScoreStageGenerator : IStageGenerator
        {
            readonly bool _lethal;

            public ScoreStageGenerator(bool lethal)
            {
                _lethal = lethal;
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                SpawnEvent[] spawns = _lethal
                    ? new[]
                    {
                        new SpawnEvent(0, "scored", 1, 0),
                        new SpawnEvent(3, "lethal", 0, 0)
                    }
                    : new[] { new SpawnEvent(0, "scored", 1, 0) };
                int lengthTicks = _lethal ? 4 : 2;
                var segment = new StageSegment(
                    "score",
                    lengthTicks,
                    spawns,
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(new[] { segment }, "boss", 1, 1, 1);
            }
        }

        sealed class ShieldHitStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                // 무적 창을 지나서까지 램머를 꾸준히 흘려보낸다.
                //
                // 매 틱 하나씩 깔았더니 무적이 2.5초로 길어지면서 150마리가 동시에
                // 살아 적 개수 상한에 걸렸다. 간격을 두어 상한을 넘지 않게 한다.
                int invulnerableTicks =
                    BattleSimConfig.DefaultPlayerHitInvulnerabilityTicks;
                var spawns = new List<SpawnEvent>
                {
                    new SpawnEvent(1, "rammer", 0, 0)
                };
                for (int tick = 8; tick <= invulnerableTicks + 200; tick += 8)
                    spawns.Add(new SpawnEvent(tick, "rammer", 0, 0));
                var segment = new StageSegment(
                    "shield_hit_regression",
                    invulnerableTicks + 400,
                    spawns.ToArray(),
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1);
            }
        }

        sealed class LethalBoundaryStageGenerator : IStageGenerator
        {
            public StagePlan Generate(
                ulong seed,
                int stageIndex,
                int difficulty)
            {
                var segment = new StageSegment(
                    "lethal_boundary",
                    1,
                    new[]
                    {
                        new SpawnEvent(0, "rammer", 0, 0)
                    },
                    1,
                    1,
                    new[] { 1 });
                return new StagePlan(
                    new[] { segment },
                    "boss",
                    1,
                    1,
                    1);
            }
        }

        sealed class TestStageGenerator : IStageGenerator
        {
            readonly bool _lethal;
            readonly int[] _segmentLengths;

            public TestStageGenerator(bool lethal, params int[] segmentLengths)
            {
                _lethal = lethal;
                _segmentLengths = (int[])segmentLengths.Clone();
            }

            public List<GenerationCall> Calls { get; } = new List<GenerationCall>();

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                Calls.Add(new GenerationCall(seed, stageIndex, difficulty));
                Rng rng = new Rng(seed).Fork(stageIndex).Fork(difficulty);
                var segments = new StageSegment[_segmentLengths.Length];
                for (int i = 0; i < segments.Length; i++)
                {
                    SpawnEvent[] spawns = _lethal && i == 0
                        ? new[]
                        {
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0),
                            new SpawnEvent(1, "rammer", 0, 0)
                        }
                        : new SpawnEvent[0];
                    segments[i] = new StageSegment(
                        "segment_" + i + "_" + rng.NextInt(0, 100000),
                        _segmentLengths[i],
                        spawns,
                        1,
                        1,
                        new[] { 1 });
                }
                return new StagePlan(segments, "boss", 1, 1, 1);
            }
        }

        sealed class GenerationCall
        {
            public GenerationCall(ulong seed, int stageIndex, int difficulty)
            {
                Seed = seed;
                StageIndex = stageIndex;
                Difficulty = difficulty;
            }

            public ulong Seed { get; }
            public int StageIndex { get; }
            public int Difficulty { get; }
        }
    }
}
