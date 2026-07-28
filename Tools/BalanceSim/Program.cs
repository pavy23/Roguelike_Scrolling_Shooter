// Headless theme-assembly check for GameData/waves.json.
// GROK balance sim: stages 1..10 × difficulty 1..5 must all assemble.
using System;
using System.IO;
using System.Text;
using Shmup.Core.Content;
using Shmup.Core.Generation;

static class Program
{
    static int Main()
    {
        string root = FindRepoRoot();
        string enemies = File.ReadAllText(Path.Combine(root, "GameData", "enemies.json"), Encoding.UTF8);
        string weapons = File.ReadAllText(Path.Combine(root, "GameData", "weapons.json"), Encoding.UTF8);
        string waves = File.ReadAllText(Path.Combine(root, "GameData", "waves.json"), Encoding.UTF8);
        string rewards = File.ReadAllText(Path.Combine(root, "GameData", "rewards.json"), Encoding.UTF8);

        GameDataSet data = GameDataParser.Parse(enemies, weapons, waves, rewards);
        var catalog = data.StageGeneration;
        var generator = new SegmentStageGenerator(catalog);

        Console.WriteLine("ThemeIds (ordinal): " + string.Join(", ", catalog.ThemeIds));
        Console.WriteLine("SegmentsPerStage: " + catalog.SegmentsPerStage);
        Console.WriteLine();

        Console.WriteLine("Segments:");
        foreach (var seg in catalog.Segments)
            Console.WriteLine(
                $"  {seg.SegmentId,-36} theme={NullLabel(seg.ThemeId),-10} " +
                $"diff={seg.DifficultyMin}-{seg.DifficultyMax}");

        Console.WriteLine("Bosses:");
        foreach (var boss in catalog.Bosses)
            Console.WriteLine(
                $"  {boss.BossId,-20} theme={NullLabel(boss.ThemeId),-10} " +
                $"stage={boss.StageIndexMin}-{boss.StageIndexMax} " +
                $"diff={boss.DifficultyMin}-{boss.DifficultyMax}");
        Console.WriteLine();

        int failures = 0;
        const ulong seed = 0xC0FFEEUL;
        for (int stage = 1; stage <= 10; stage++)
        {
            for (int difficulty = 1; difficulty <= 5; difficulty++)
            {
                try
                {
                    StagePlan plan = generator.Generate(seed, stage, difficulty);
                    if (!StagePlanClearability.IsClearable(plan))
                    {
                        Console.WriteLine(
                            $"FAIL stage={stage} diff={difficulty}: plan not clearable " +
                            $"(theme={plan.ThemeId}, boss={plan.BossId})");
                        failures++;
                        continue;
                    }

                    Console.WriteLine(
                        $"OK   stage={stage,2} diff={difficulty} theme={plan.ThemeId,-10} " +
                        $"boss={plan.BossId,-14} segs=[" +
                        string.Join(",", SegmentIds(plan)) + "]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"FAIL stage={stage} diff={difficulty}: {ex.Message}");
                    failures++;
                }
            }
        }

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("PASS: all 50 stage×difficulty assemblies succeeded.");
            return 0;
        }

        Console.WriteLine($"FAIL: {failures} assembly failures.");
        return 1;
    }

    static string[] SegmentIds(StagePlan plan)
    {
        var ids = new string[plan.Segments.Count];
        for (int i = 0; i < plan.Segments.Count; i++)
            ids[i] = plan.Segments[i].SegmentId;
        return ids;
    }

    static string NullLabel(string value) => value ?? "<null>";

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GameData", "waves.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // Fallback: Tools/BalanceSim -> repo root
        string fromCwd = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "..", ".."));
        if (File.Exists(Path.Combine(fromCwd, "GameData", "waves.json")))
            return fromCwd;

        throw new DirectoryNotFoundException("Could not locate GameData/waves.json");
    }
}
