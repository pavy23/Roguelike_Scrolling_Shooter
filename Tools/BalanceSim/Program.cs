// Headless balance checks for GameData (GROK).
// 1) Theme assembly: stages 1..10 × difficulty 1..5 must all assemble.
// 2) Reward catalog: modifier rewards parse + weight / maxPerRun guide checks.
// 3) Modifier combo: pierce + kill_explosion dense-pack clear-time (DPS runaway).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shmup.Core;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

static class Program
{
    // Soft threshold for pierce+explosion vs baseline clear-speed ratio.
    // Values are provisional (AGENTS.md §7); WARN only — does not fail the run.
    const double ComboRunawayWarnRatio = 4.0;

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
        failures += CheckThemeAssemblies(generator);
        Console.WriteLine();
        failures += CheckModifierRewards(data.Rewards);
        Console.WriteLine();
        failures += CheckModifierComboDps();

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("PASS: BalanceSim all checks green.");
            return 0;
        }

        Console.WriteLine($"FAIL: {failures} check failure(s).");
        return 1;
    }

    static int CheckThemeAssemblies(SegmentStageGenerator generator)
    {
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
            Console.WriteLine("PASS: all 50 stage×difficulty assemblies succeeded.");
        else
            Console.WriteLine($"FAIL: {failures} assembly failures.");
        return failures;
    }

    static int CheckModifierRewards(RewardCatalog rewards)
    {
        int failures = 0;
        if (rewards == null)
        {
            Console.WriteLine("FAIL rewards: catalog is null.");
            return 1;
        }

        var expected = new Dictionary<string, BattleModifier>(StringComparer.Ordinal)
        {
            ["mod_pierce_shot"] = BattleModifier.PierceShot,
            ["mod_ricochet"] = BattleModifier.Ricochet,
            ["mod_homing_missile"] = BattleModifier.HomingMissile,
            ["mod_kill_explosion"] = BattleModifier.KillExplosion
        };

        Console.WriteLine("Reward catalog:");
        Console.WriteLine($"  optionCount={rewards.OptionCount} entries={rewards.All.Count}");

        int modifierWeightStage1 = 0;
        int totalWeightStage1 = 0;
        int modifierWeightStage2 = 0;
        int totalWeightStage2 = 0;
        int foundModifiers = 0;

        foreach (RewardDefinition def in rewards.All)
        {
            bool stage1 = def.StageIndexMin <= 1 && def.StageIndexMax >= 1;
            bool stage2 = def.StageIndexMin <= 2 && def.StageIndexMax >= 2;
            if (stage1) totalWeightStage1 += def.Weight;
            if (stage2) totalWeightStage2 += def.Weight;

            if (def.Type != RewardType.Modifier)
                continue;

            foundModifiers++;
            if (stage1) modifierWeightStage1 += def.Weight;
            if (stage2) modifierWeightStage2 += def.Weight;

            if (!expected.TryGetValue(def.Id, out BattleModifier expectedId))
            {
                Console.WriteLine($"FAIL rewards: unexpected modifier id '{def.Id}'.");
                failures++;
                continue;
            }

            if (def.ModifierId != expectedId)
            {
                Console.WriteLine(
                    $"FAIL rewards: {def.Id} modifierId={def.ModifierId} expected {expectedId}.");
                failures++;
            }

            if (!def.MaxPerRun.HasValue || def.MaxPerRun.Value != 1)
            {
                Console.WriteLine(
                    $"FAIL rewards: {def.Id} maxPerRun must be 1 (got {def.MaxPerRun}).");
                failures++;
            }

            if (def.StageIndexMin > 2)
            {
                Console.WriteLine(
                    $"FAIL rewards: {def.Id} should appear early (stageIndexMin<=2, got {def.StageIndexMin}).");
                failures++;
            }

            Console.WriteLine(
                $"  {def.Id,-22} modifier={def.ModifierId,-14} " +
                $"weight={def.Weight} stage={def.StageIndexMin}-{def.StageIndexMax} " +
                $"maxPerRun={def.MaxPerRun}");
            expected.Remove(def.Id);
        }

        foreach (string missing in expected.Keys)
        {
            Console.WriteLine($"FAIL rewards: missing modifier reward '{missing}'.");
            failures++;
        }

        // With-replacement approximation of expected modifiers in a 3-pick.
        // Guide: ~1 modifier per 3-choice offer (REQ-014).
        double expectedStage1 = totalWeightStage1 == 0
            ? 0
            : 3.0 * modifierWeightStage1 / totalWeightStage1;
        double expectedStage2 = totalWeightStage2 == 0
            ? 0
            : 3.0 * modifierWeightStage2 / totalWeightStage2;

        Console.WriteLine(
            $"  weight stage1: modifiers={modifierWeightStage1}/{totalWeightStage1} " +
            $"E[mods in 3-pick]≈{expectedStage1:F2}");
        Console.WriteLine(
            $"  weight stage2+: modifiers={modifierWeightStage2}/{totalWeightStage2} " +
            $"E[mods in 3-pick]≈{expectedStage2:F2}");

        if (foundModifiers != 4)
        {
            Console.WriteLine($"FAIL rewards: expected 4 modifier entries, found {foundModifiers}.");
            failures++;
        }

        // Soft band around guide (~1). Provisional — warn-only outside band.
        if (expectedStage1 < 0.5 || expectedStage1 > 1.8)
        {
            Console.WriteLine(
                $"WARN rewards: stage1 E[mods]≈{expectedStage1:F2} outside guide band [0.5, 1.8] (§7).");
        }
        if (expectedStage2 < 0.5 || expectedStage2 > 1.8)
        {
            Console.WriteLine(
                $"WARN rewards: stage2 E[mods]≈{expectedStage2:F2} outside guide band [0.5, 1.8] (§7).");
        }

        if (failures == 0)
            Console.WriteLine("PASS: modifier rewards catalog checks.");
        return failures;
    }

    /// <summary>
    /// Dense horizontal pack of low-HP fodder. Measures ticks-to-clear under
    /// None / Pierce / KillExplosion / Pierce|KillExplosion with default
    /// BattleSimConfig synergy tuning (pierce count 1, explosion dmg 2, radius 2u).
    /// </summary>
    static int CheckModifierComboDps()
    {
        int failures = 0;
        const int packSize = 12;
        const int enemyHp = 1;
        const int maxTicks = 600;

        var scenarios = new[]
        {
            ("none", BattleModifier.None),
            ("pierce", BattleModifier.PierceShot),
            ("kill_explosion", BattleModifier.KillExplosion),
            ("pierce+explosion", BattleModifier.PierceShot | BattleModifier.KillExplosion)
        };

        Console.WriteLine(
            "Modifier combo DPS (dense pack clear-time, provisional Core defaults):");
        Console.WriteLine(
            $"  pack={packSize} hp={enemyHp} spacing=0.5u fireInterval=default " +
            $"explosion dmg/radius=config defaults");

        int baselineTicks = 0;
        int bestSingleTicks = int.MaxValue;
        int comboTicks = 0;
        long comboKills = 0;

        foreach (var (label, modifiers) in scenarios)
        {
            try
            {
                ClearResult result = SimulatePackClear(
                    modifiers,
                    packSize,
                    enemyHp,
                    maxTicks);
                double dpsProxy = result.TicksToClear > 0
                    ? (double)result.Kills * SimSpace.TicksPerSecond / result.TicksToClear
                    : 0;

                Console.WriteLine(
                    $"  {label,-18} clearTicks={result.TicksToClear,4} " +
                    $"kills={result.Kills,2} shotsHit={result.ShotsHit,3} " +
                    $"kills/s≈{dpsProxy:F1} cleared={result.Cleared}");

                if (!result.Cleared)
                {
                    Console.WriteLine(
                        $"FAIL combo: scenario '{label}' did not clear pack in {maxTicks} ticks.");
                    failures++;
                }

                if (modifiers == BattleModifier.None)
                    baselineTicks = result.TicksToClear;
                else if (modifiers == (BattleModifier.PierceShot | BattleModifier.KillExplosion))
                {
                    comboTicks = result.TicksToClear;
                    comboKills = result.Kills;
                }
                else if (result.TicksToClear < bestSingleTicks)
                    bestSingleTicks = result.TicksToClear;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL combo: scenario '{label}' threw: {ex.Message}");
                failures++;
            }
        }

        if (baselineTicks > 0 && comboTicks > 0)
        {
            double vsBaseline = (double)baselineTicks / comboTicks;
            Console.WriteLine(
                $"  combo vs baseline clear-speed ×{vsBaseline:F2} " +
                $"(baseline {baselineTicks}t → combo {comboTicks}t, kills={comboKills})");

            if (bestSingleTicks < int.MaxValue && bestSingleTicks > 0)
            {
                double vsBestSingle = (double)bestSingleTicks / comboTicks;
                Console.WriteLine(
                    $"  combo vs best-single clear-speed ×{vsBestSingle:F2} " +
                    $"(best single {bestSingleTicks}t)");
            }

            // Document runaway: pierce kills open more explosions along the pack.
            // Soft warn only — Core config defaults are not GameData-owned.
            if (vsBaseline >= ComboRunawayWarnRatio)
            {
                Console.WriteLine(
                    $"WARN combo: pierce+explosion clear-speed ≥{ComboRunawayWarnRatio:F0}× baseline " +
                    $"(×{vsBaseline:F2}). Review KillExplosionDamage/Radius or PierceShotEnemyCount " +
                    $"(Core config, provisional §7).");
            }
            else
            {
                Console.WriteLine(
                    $"  combo within soft runaway band (<{ComboRunawayWarnRatio:F0}× baseline).");
            }
        }

        if (failures == 0)
            Console.WriteLine("PASS: modifier combo clear-time sim.");
        return failures;
    }

    static ClearResult SimulatePackClear(
        BattleModifier modifiers,
        int packSize,
        int enemyHp,
        int maxTicks)
    {
        BattleSimConfig config = BattleSimConfig.CreateDefault();
        // Stationary player; bullets fly right into a tight pack.
        config.PlayerSpeedNumerator = 0;
        config.PlayerSpeedDenominator = 1;
        config.ScrollSpeedNumerator = 0;
        config.ScrollSpeedDenominator = 1;
        config.EnemyBulletDamage = 0;
        config.MaxEnemyBullets = 0;
        config.CapsuleNoDropWeight = 1;

        var enemies = new EnemyDefinition[packSize];
        var spawns = new SpawnEvent[packSize];
        // 0.5 world-unit spacing (128 sub-units) so default explosion radius (2u)
        // covers several neighbors while pierce walks the line.
        const int spacing = SimSpace.SubUnitsPerWorldUnit / 2;
        const int half = SimSpace.SubUnitsPerWorldUnit / 4;
        for (int i = 0; i < packSize; i++)
        {
            string id = "fodder_" + i;
            enemies[i] = new EnemyDefinition(
                id,
                id,
                enemyHp,
                0,
                1,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                half,
                half,
                0,
                0,
                1,
                64);
            // Pack starts just in front of the default player spawn X.
            int x = config.PlayerSpawnX + SimSpace.SubUnitsPerWorldUnit + i * spacing;
            spawns[i] = new SpawnEvent(0, id, x, config.PlayerSpawnY);
        }

        // weapons.json main_shot half sizes: 0.375×0.140625 world units.
        const int bulletHalfW = 3 * SimSpace.SubUnitsPerWorldUnit / 8;
        const int bulletHalfH = 9 * SimSpace.SubUnitsPerWorldUnit / 64;
        var weapon = new WeaponDefinition(
            "main_shot",
            10,
            config.FireIntervalTicks,
            config.PlayerBulletSpeedNumerator,
            config.PlayerBulletSpeedDenominator,
            bulletHalfW,
            bulletHalfH);
        var content = new BattleContent(enemies, new[] { weapon }, weapon.Id);
        var segment = new StageSegment(
            "combo_pack",
            maxTicks + 10,
            spawns,
            1,
            1,
            new[] { 1 });
        var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        gauge.ImportLevels(new[] { 0, 0, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xBEEFUL),
            plan,
            content,
            gauge,
            modifiers);

        var fire = new InputCommand(0, 0, true);
        for (int tick = 1; tick <= maxTicks; tick++)
        {
            sim.Step(in fire);
            if (sim.Enemies.Count == 0)
            {
                return new ClearResult(
                    true,
                    tick,
                    sim.Statistics.Kills,
                    sim.Statistics.ShotsHit);
            }
        }

        return new ClearResult(
            false,
            maxTicks,
            sim.Statistics.Kills,
            sim.Statistics.ShotsHit);
    }

    readonly struct ClearResult
    {
        public ClearResult(bool cleared, int ticksToClear, long kills, long shotsHit)
        {
            Cleared = cleared;
            TicksToClear = ticksToClear;
            Kills = kills;
            ShotsHit = shotsHit;
        }

        public bool Cleared { get; }
        public int TicksToClear { get; }
        public long Kills { get; }
        public long ShotsHit { get; }
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
