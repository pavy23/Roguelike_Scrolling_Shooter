using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req091BoundaryWatchdogTests
    {
        const int StageOneTickBudget = 12_000;

        [Test]
        public void ReproductionSeedZeroLaserRunCompletesDeterministically()
        {
            GameDataSet data = ParseRepositoryGameData();
            ScanResult first = CompleteStageOne(data, 0UL);
            ScanResult second = CompleteStageOne(data, 0UL);

            TestContext.WriteLine(first.Format());
            Assert.IsTrue(first.Completed, first.Format());
            Assert.IsTrue(second.Completed, second.Format());
            Assert.AreEqual(first.Ticks, second.Ticks);
            Assert.AreEqual(first.Hash, second.Hash);
        }

        [Test]
        public void SeedsZeroThroughTwoHundredFiftyFiveCompleteStageOne()
        {
            GameDataSet data = ParseRepositoryGameData();
            const int bucketSize = 32;
            int bucketMinimum = int.MaxValue;
            int bucketMaximum = 0;

            for (ulong seed = 0; seed < 256; seed++)
            {
                ScanResult result = CompleteStageOne(data, seed);
                Assert.IsTrue(result.Completed, result.Format());
                bucketMinimum = Math.Min(bucketMinimum, result.Ticks);
                bucketMaximum = Math.Max(bucketMaximum, result.Ticks);

                if ((seed + 1) % bucketSize == 0)
                {
                    TestContext.WriteLine(
                        "seeds={0}-{1} pass=32 minTicks={2} maxTicks={3}",
                        seed + 1 - bucketSize,
                        seed,
                        bucketMinimum,
                        bucketMaximum);
                    bucketMinimum = int.MaxValue;
                    bucketMaximum = 0;
                }
            }
        }

        static ScanResult CompleteStageOne(GameDataSet data, ulong seed)
        {
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.GrantLevels(PowerUpSlot.MainShot, 5);
            gauge.GrantLevels(PowerUpSlot.Laser, 3);
            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerMaxHp = 1_000_000;
            var run = new RunManager(
                seed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                gauge,
                data.Rewards,
                data.Contracts);
            var hasher = new DeterminismAuditHasher();
            int ticks = 0;

            hasher.FoldRunState(run);
            while (ticks < StageOneTickBudget
                && run.StageIndex == 1
                && run.State != RunState.RunOver
                && run.State != RunState.RunCleared)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    if (!run.ChooseReward(0))
                        throw new InvalidOperationException(
                            "Seed scan reward choice was rejected.");
                    hasher.FoldRunState(run);
                    continue;
                }
                if (run.State == RunState.AwaitingContract)
                {
                    if (!run.ChooseContract(0))
                        throw new InvalidOperationException(
                            "Seed scan contract choice was rejected.");
                    hasher.FoldRunState(run);
                    continue;
                }

                if (run.RoomIndex > 1
                    && gauge.ActiveWeaponMode == PowerUpWeaponMode.Laser)
                    gauge.GrantLevels(PowerUpSlot.Double, 3);
                BattleSim battle = (BattleSim)run.Battle;
                battle.RecoverShieldStock(run.MaxShieldStock);
                InputCommand input = CreateInput(seed, ticks, battle);
                run.Step(in input);
                hasher.FoldRunState(run);
                ticks++;
            }

            return new ScanResult(
                seed,
                ticks,
                run.StageIndex > 1 || run.State == RunState.RunCleared,
                run.State,
                run.BiomeIndex,
                run.RoomIndex,
                run.Battle.Tick,
                hasher.Hash);
        }

        static InputCommand CreateInput(
            ulong seed,
            int tick,
            BattleSim battle)
        {
            int targetY = battle.BossActive
                ? battle.Boss.Y
                : ((tick + (int)(seed % 240UL)) % 240 < 120
                    ? SimSpace.PlayfieldHalfHeightSubUnits / 2
                    : -SimSpace.PlayfieldHalfHeightSubUnits / 2);
            int moveY = battle.PlayerY < targetY
                ? 1
                : battle.PlayerY > targetY
                    ? -1
                    : 0;
            return new InputCommand(0, moveY, true);
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(
                FindRepositoryRoot(),
                "GameData");
            return GameDataParser.Parse(
                Read(gameData, "enemies.json"),
                Read(gameData, "weapons.json"),
                Read(gameData, "waves.json"),
                Read(gameData, "rewards.json"),
                Read(gameData, "ships.json"),
                Read(gameData, "scoring.json"));
        }

        static string Read(string directory, string fileName)
        {
            return File.ReadAllText(Path.Combine(directory, fileName));
        }

        static string FindRepositoryRoot()
        {
            DirectoryInfo current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(
                    Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }

        readonly struct ScanResult
        {
            public ScanResult(
                ulong seed,
                int ticks,
                bool completed,
                RunState state,
                int biome,
                int room,
                int battleTick,
                ulong hash)
            {
                Seed = seed;
                Ticks = ticks;
                Completed = completed;
                State = state;
                Biome = biome;
                Room = room;
                BattleTick = battleTick;
                Hash = hash;
            }

            public ulong Seed { get; }
            public int Ticks { get; }
            public bool Completed { get; }
            public RunState State { get; }
            public int Biome { get; }
            public int Room { get; }
            public int BattleTick { get; }
            public ulong Hash { get; }

            public string Format()
            {
                return $"seed={Seed} completed={Completed} ticks={Ticks}/"
                    + $"{StageOneTickBudget} state={State} biome={Biome} "
                    + $"room={Room} battleTick={BattleTick} hash={Hash:X16}";
            }
        }
    }
}
