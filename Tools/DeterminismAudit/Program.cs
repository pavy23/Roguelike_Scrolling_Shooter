using System;
using System.Globalization;
using System.IO;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.DeterminismAudit
{
    static class Program
    {
        const int Success = 0;
        const int InvalidArguments = 2;
        const int AuditFailure = 3;

        static int Main(string[] args)
        {
            if (args.Length != 3
                || !TryParseSeed(args[0], out ulong seed)
                || !TryParsePositiveInt(args[1], out int stageCount)
                || !TryParsePositiveInt(args[2], out int tickCount))
            {
                Console.Error.WriteLine(
                    "Usage: dotnet run --project Tools/DeterminismAudit -- "
                    + "<seed> <stageCount> <tickCount>");
                Console.Error.WriteLine(
                    "Seed accepts unsigned decimal or a 0x-prefixed hexadecimal value.");
                return InvalidArguments;
            }

            try
            {
                string projectRoot = FindProjectRoot();
                string gameData = Path.Combine(projectRoot, "GameData");
                string rewardsPath = Path.Combine(gameData, "rewards.json");
                string shipsPath = Path.Combine(gameData, "ships.json");
                GameDataSet data = GameDataParser.Parse(
                    File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                    File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                    File.ReadAllText(Path.Combine(gameData, "waves.json")),
                    File.Exists(rewardsPath) ? File.ReadAllText(rewardsPath) : null,
                    File.Exists(shipsPath) ? File.ReadAllText(shipsPath) : null);

                BattleSimConfig config = data.CreateBattleSimConfig();
                // The audit must reach stage transitions instead of ending on
                // balance-dependent player survivability. Hit events still fold
                // into the event count, while this large finite HP avoids death.
                config.PlayerMaxHp = 1_000_000;
                var run = new RunManager(
                    seed,
                    new SegmentStageGenerator(data.StageGeneration),
                    config,
                    data.BattleContent,
                    data.CreatePowerUpGauge(),
                    data.Rewards,
                    data.DefaultShip);
                var hasher = new DeterminismAuditHasher();

                int executedTicks = 0;
                while (executedTicks < tickCount
                    && run.StageIndex <= stageCount
                    && run.State != RunState.RunOver)
                {
                    if (run.State == RunState.AwaitingReward)
                    {
                        int option = (run.StageIndex + executedTicks)
                            % run.RewardOptions.Count;
                        run.ChooseReward(option);
                        if (run.StageIndex > stageCount)
                            break;
                    }

                    InputCommand input = CreateInput(
                        seed,
                        executedTicks,
                        run.Battle.PlayerX);
                    int steppedRunNumber = run.RunNumber;
                    int steppedStageIndex = run.StageIndex;
                    IBattleSim steppedBattle = run.Battle;
                    run.Step(in input);
                    hasher.FoldTick(
                        steppedRunNumber,
                        steppedStageIndex,
                        run.State,
                        steppedBattle,
                        run.TotalScore);
                    executedTicks++;
                }

                int completedStages = Math.Min(
                    stageCount,
                    Math.Max(0, run.StageIndex - 1));
                Console.WriteLine(
                    $"hash={hasher.HexHash} seed={seed} "
                    + $"requestedStages={stageCount} completedStages={completedStages} "
                    + $"requestedTicks={tickCount} executedTicks={executedTicks} "
                    + $"stage={run.StageIndex} state={run.State} ship={run.Ship.Id}");
                return Success;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Determinism audit failed: {ex.GetType().Name}: {ex.Message}");
                return AuditFailure;
            }
        }

        static InputCommand CreateInput(
            ulong seed,
            int tick,
            int playerX)
        {
            int phaseOffset = (int)(seed % 360UL);
            int verticalPhase = (tick + phaseOffset * 3) % 360;
            int leftAuditLane = -18 * SimSpace.SubUnitsPerWorldUnit;
            int moveX = playerX > leftAuditLane ? -1 : 0;
            int moveY = verticalPhase < 180 ? 1 : -1;
            bool fire = (tick + phaseOffset) % 5 != 4;
            return new InputCommand(moveX, moveY, fire);
        }

        static bool TryParseSeed(string value, out ulong seed)
        {
            if (value != null && value.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ulong.TryParse(
                    value.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out seed);
            }
            return ulong.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out seed);
        }

        static bool TryParsePositiveInt(string value, out int result)
        {
            return int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out result)
                && result > 0;
        }

        static string FindProjectRoot()
        {
            string root = FindProjectRootFrom(Directory.GetCurrentDirectory());
            if (root != null)
                return root;

            root = FindProjectRootFrom(AppContext.BaseDirectory);
            if (root != null)
                return root;

            throw new DirectoryNotFoundException(
                "Could not find the project root containing GameData.");
        }

        static string FindProjectRootFrom(string startPath)
        {
            var directory = new DirectoryInfo(startPath);
            while (directory != null)
            {
                string dataPath = Path.Combine(directory.FullName, "GameData");
                if (File.Exists(Path.Combine(dataPath, "enemies.json"))
                    && File.Exists(Path.Combine(dataPath, "weapons.json"))
                    && File.Exists(Path.Combine(dataPath, "waves.json")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }
    }
}
