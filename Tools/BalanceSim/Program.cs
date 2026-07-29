// Headless balance checks for GameData (GROK).
// 1) Theme assembly: stages 1..10 × difficulty 1..5 must all assemble.
// 2) Reward catalog: modifier rewards parse + weight / maxPerRun guide checks.
// 3) Modifier combo: pierce + kill_explosion dense-pack clear-time (DPS runaway).
// 4) Scoring: graze/combo curves from scoring.json (x8 maintain + graze vs kill).
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

    // Provisional hard gates for scoring.json (AGENTS.md §7).
    // Graze fixed score must stay well below the cheapest kill at x1.
    const double MaxGrazeToMinKillRatio = 0.25;
    // At x8, one min-score kill must still cost many grazes to match.
    const int MinGrazesToMatchX8Kill = 20;
    // Kill-only climb to x8 should feel reachable mid-stage, not trivial/impossible.
    const int MinKillsToX8 = 8;
    const int MaxKillsToX8 = 40;
    // Decay window (ticks @60Hz): hold x8 needs regular combat, not AFK.
    const int MinDecayTicks = 120;  // 2s
    const int MaxDecayTicks = 600;  // 10s
    // Soft WARN if pure-graze climb is too easy relative to kill climb.
    const int MinGrazeToKillClimbRatio = 5;

    static int Main()
    {
        string root = FindRepoRoot();
        string enemies = File.ReadAllText(Path.Combine(root, "GameData", "enemies.json"), Encoding.UTF8);
        string weapons = File.ReadAllText(Path.Combine(root, "GameData", "weapons.json"), Encoding.UTF8);
        string waves = File.ReadAllText(Path.Combine(root, "GameData", "waves.json"), Encoding.UTF8);
        string rewards = File.ReadAllText(Path.Combine(root, "GameData", "rewards.json"), Encoding.UTF8);
        string ships = File.ReadAllText(Path.Combine(root, "GameData", "ships.json"), Encoding.UTF8);
        string scoring = File.ReadAllText(Path.Combine(root, "GameData", "scoring.json"), Encoding.UTF8);

        GameDataSet data = GameDataParser.Parse(
            enemies, weapons, waves, rewards, ships, scoring);
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
        failures += CheckScoringCurves(data);

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

    /// <summary>
    /// Validates scoring.json graze/combo curves (REQ-016).
    /// Checks: (1) values apply to BattleSimConfig, (2) x8 climb/maintain difficulty,
    /// (3) graze fixed score does not outpace kill scores / graze-only climb is slow.
    /// KillComboGaugeGain remains Core default (not in scoring.json).
    /// </summary>
    static int CheckScoringCurves(GameDataSet data)
    {
        int failures = 0;
        BattleSimConfig config = data.CreateBattleSimConfig();

        int grazeRadius = config.GrazeExtraRadiusSubUnits;
        int grazeScore = config.GrazeScore;
        int grazeCharge = config.GrazeComboGaugeGain;
        int req2 = config.ComboGaugeRequiredForLevel2;
        int req3 = config.ComboGaugeRequiredForLevel3;
        int req4 = config.ComboGaugeRequiredForLevel4;
        int decay = config.ComboDecayTicks;
        int killCharge = config.KillComboGaugeGain;
        int mult1 = config.ComboMultiplierLevel1;
        int mult2 = config.ComboMultiplierLevel2;
        int mult3 = config.ComboMultiplierLevel3;
        int mult4 = config.ComboMultiplierLevel4;

        Console.WriteLine("Scoring curves (scoring.json → BattleSimConfig, provisional §7):");
        Console.WriteLine(
            $"  grazeRadius={grazeRadius}su ({grazeRadius / (double)SimSpace.SubUnitsPerWorldUnit:F2}u) " +
            $"grazeScore={grazeScore} grazeGaugeCharge={grazeCharge}");
        Console.WriteLine(
            $"  mult requirements=[{req2},{req3},{req4}] " +
            $"decayTicks={decay} ({decay / (double)SimSpace.TicksPerSecond:F1}s) " +
            $"killGaugeGain={killCharge} (Core default)");
        Console.WriteLine(
            $"  multipliers x{mult1}→x{mult2}→x{mult3}→x{mult4}");

        // Sanity: config received finite positive scoring knobs.
        if (grazeRadius < 0 || grazeScore < 0 || grazeCharge < 0)
        {
            Console.WriteLine("FAIL scoring: negative graze knobs after ApplyTo.");
            failures++;
        }
        if (req2 < 1 || req3 < 1 || req4 < 1 || decay < 1 || killCharge < 1)
        {
            Console.WriteLine("FAIL scoring: non-positive combo knobs after ApplyTo.");
            failures++;
        }

        int killsToX2 = CeilDiv(req2, killCharge);
        int killsToX4 = killsToX2 + CeilDiv(req3, killCharge);
        int killsToX8 = killsToX4 + CeilDiv(req4, killCharge);
        int grazesToX2 = grazeCharge == 0 ? int.MaxValue : CeilDiv(req2, grazeCharge);
        int grazesToX4 = grazeCharge == 0
            ? int.MaxValue
            : grazesToX2 + CeilDiv(req3, grazeCharge);
        int grazesToX8 = grazeCharge == 0
            ? int.MaxValue
            : grazesToX4 + CeilDiv(req4, grazeCharge);

        Console.WriteLine(
            $"  kill climb: x2 in {killsToX2} kills, x4 in {killsToX4}, x8 in {killsToX8}");
        Console.WriteLine(
            $"  graze climb: x2 in {FormatCount(grazesToX2)} grazes, " +
            $"x4 in {FormatCount(grazesToX4)}, x8 in {FormatCount(grazesToX8)} " +
            "(graze does not reset decay — only kills maintain mult)");

        // x8 maintain: kill every decay window.
        double decaySeconds = decay / (double)SimSpace.TicksPerSecond;
        Console.WriteLine(
            $"  x8 hold: need ≥1 kill every {decay} ticks ({decaySeconds:F1}s) " +
            $"or drop one mult level (x8→x4→x2→x1)");

        if (killsToX8 < MinKillsToX8 || killsToX8 > MaxKillsToX8)
        {
            Console.WriteLine(
                $"FAIL scoring: kills-to-x8={killsToX8} outside band " +
                $"[{MinKillsToX8},{MaxKillsToX8}] (x8 too easy/hard).");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  kills-to-x8={killsToX8} within band [{MinKillsToX8},{MaxKillsToX8}].");
        }

        if (decay < MinDecayTicks || decay > MaxDecayTicks)
        {
            Console.WriteLine(
                $"FAIL scoring: decayTicks={decay} outside band " +
                $"[{MinDecayTicks},{MaxDecayTicks}] (x8 hold too harsh/lenient).");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  decayTicks={decay} within band [{MinDecayTicks},{MaxDecayTicks}].");
        }

        // Catalog kill scores for graze-vs-kill pressure.
        int minKillScore = int.MaxValue;
        int maxKillScore = 0;
        long sumKillScore = 0;
        int enemyCount = 0;
        string minKillId = "?";
        foreach (EnemyDefinition enemy in data.BattleContent.Enemies)
        {
            enemyCount++;
            sumKillScore += enemy.ScoreValue;
            if (enemy.ScoreValue < minKillScore)
            {
                minKillScore = enemy.ScoreValue;
                minKillId = enemy.Id;
            }
            if (enemy.ScoreValue > maxKillScore)
                maxKillScore = enemy.ScoreValue;
        }

        if (enemyCount == 0 || minKillScore == int.MaxValue)
        {
            Console.WriteLine("FAIL scoring: no enemies to compare graze vs kill.");
            return failures + 1;
        }

        double avgKillScore = sumKillScore / (double)enemyCount;
        double grazeToMinKill = grazeScore / (double)minKillScore;
        int grazesToMatchX1Min = grazeScore == 0
            ? int.MaxValue
            : CeilDiv(minKillScore, grazeScore);
        int grazesToMatchX8Min = grazeScore == 0
            ? int.MaxValue
            : CeilDiv(minKillScore * mult4, grazeScore);
        int grazesToMatchX1Avg = grazeScore == 0
            ? int.MaxValue
            : CeilDiv((int)Math.Round(avgKillScore), grazeScore);

        Console.WriteLine(
            $"  kill scores: min={minKillScore} ({minKillId}) avg≈{avgKillScore:F0} max={maxKillScore}");
        Console.WriteLine(
            $"  graze vs kill: grazeScore/minKill={grazeToMinKill:F3} " +
            $"(grazes≈1 min-kill@x1: {FormatCount(grazesToMatchX1Min)}, " +
            $"@x{mult4}: {FormatCount(grazesToMatchX8Min)}; " +
            $"avg@x1: {FormatCount(grazesToMatchX1Avg)})");

        if (grazeToMinKill > MaxGrazeToMinKillRatio)
        {
            Console.WriteLine(
                $"FAIL scoring: grazeScore/minKill={grazeToMinKill:F3} > " +
                $"{MaxGrazeToMinKillRatio:F2} — graze farming threatens kill score.");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  graze/minKill ratio OK (≤{MaxGrazeToMinKillRatio:F2}).");
        }

        if (grazesToMatchX8Min < MinGrazesToMatchX8Kill)
        {
            Console.WriteLine(
                $"FAIL scoring: only {grazesToMatchX8Min} grazes match one min-kill at x{mult4} " +
                $"(need ≥{MinGrazesToMatchX8Kill}).");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  x{mult4} kill still dominates graze (≥{MinGrazesToMatchX8Kill} grazes to match).");
        }

        // Sustained combat sketch: 1 kill / 2s of min fodder + 3 grazes/s skill play.
        // Multiplier climbs with kills; graze score stays unmultiplied (Core rule).
        const int simSeconds = 60;
        const int killsPerTwoSeconds = 1;
        const int grazesPerSecond = 3;
        long killScoreAccum = 0;
        long grazeScoreAccum = 0;
        int gauge = 0;
        int level = 0; // 0=x1 .. 3=x8
        int[] reqs = { req2, req3, req4 };
        int[] mults = { mult1, mult2, mult3, mult4 };
        int ticksSinceKill = 0;
        int totalKills = 0;
        int totalGrazes = 0;
        int killIntervalTicks = 2 * SimSpace.TicksPerSecond / killsPerTwoSeconds;

        for (int t = 1; t <= simSeconds * SimSpace.TicksPerSecond; t++)
        {
            bool killed = false;
            if (t % killIntervalTicks == 0)
            {
                killScoreAccum += (long)minKillScore * mults[level];
                totalKills++;
                killed = true;
                ticksSinceKill = 0;
                // Kill gauge gain + level climb (mirrors BattleSim.AddComboGauge).
                if (level < mults.Length - 1 && killCharge > 0)
                {
                    long next = (long)gauge + killCharge;
                    gauge = next >= int.MaxValue ? int.MaxValue : (int)next;
                    while (level < mults.Length - 1 && gauge >= reqs[level])
                    {
                        gauge -= reqs[level];
                        level++;
                    }
                    if (level == mults.Length - 1)
                        gauge = 0;
                }
            }

            // 3 grazes/s ≈ one graze every 20 ticks.
            if (t % (SimSpace.TicksPerSecond / grazesPerSecond) == 0)
            {
                grazeScoreAccum += grazeScore;
                totalGrazes++;
                if (level < mults.Length - 1 && grazeCharge > 0)
                {
                    long next = (long)gauge + grazeCharge;
                    gauge = next >= int.MaxValue ? int.MaxValue : (int)next;
                    while (level < mults.Length - 1 && gauge >= reqs[level])
                    {
                        gauge -= reqs[level];
                        level++;
                    }
                    if (level == mults.Length - 1)
                        gauge = 0;
                }
            }

            if (!killed && level > 0)
            {
                ticksSinceKill++;
                if (ticksSinceKill >= decay)
                {
                    ticksSinceKill = 0;
                    gauge = 0;
                    level--;
                }
            }
        }

        long totalScore = killScoreAccum + grazeScoreAccum;
        double grazeShare = totalScore == 0 ? 0 : grazeScoreAccum / (double)totalScore;
        Console.WriteLine(
            $"  60s sketch (1 kill/2s min-fodder + {grazesPerSecond} graze/s): " +
            $"kills={totalKills} grazes={totalGrazes} endMult=x{mults[level]} " +
            $"killScore={killScoreAccum} grazeScore={grazeScoreAccum} " +
            $"grazeShare={grazeShare:P1}");

        // Hard fail if graze contributes majority under this modest skill sketch.
        if (grazeShare >= 0.40)
        {
            Console.WriteLine(
                $"FAIL scoring: grazeShare={grazeShare:P1} ≥ 40% in 60s sketch — " +
                "graze farming dominates kill score.");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  grazeShare={grazeShare:P1} < 40% under sustained combat sketch.");
        }

        // Soft: graze-only climb should be several× slower than kill climb.
        if (grazesToX8 != int.MaxValue && killsToX8 > 0)
        {
            double climbRatio = grazesToX8 / (double)killsToX8;
            if (climbRatio < MinGrazeToKillClimbRatio)
            {
                Console.WriteLine(
                    $"WARN scoring: graze-to-x8 / kill-to-x8 = {climbRatio:F1} " +
                    $"< {MinGrazeToKillClimbRatio} (graze climb relatively easy, §7).");
            }
            else
            {
                Console.WriteLine(
                    $"  graze climb {climbRatio:F1}× slower than kill climb " +
                    $"(≥{MinGrazeToKillClimbRatio}×).");
            }
        }

        // Headless micro-sim: one graze bullet scores once, kill applies mult.
        failures += SimulateGrazeAndKillSmoke(config, data, minKillScore, minKillId);

        if (failures == 0)
            Console.WriteLine("PASS: scoring graze/combo curve checks.");
        return failures;
    }

    /// <summary>
    /// Smoke: zero-size turret bullet graze once + zero-size fodder kill.
    /// Mirrors BattleScoringTests layout so knobs are exercised without contact/hit overlap.
    /// </summary>
    static int SimulateGrazeAndKillSmoke(
        BattleSimConfig baseConfig,
        GameDataSet data,
        int fodderScore,
        string fodderId)
    {
        int failures = 0;
        try
        {
            // scoring.json values are the under-test knobs; spatial layout is lab-sized.
            BattleSimConfig config = data.CreateBattleSimConfig();
            int grazeScore = config.GrazeScore;
            int grazeCharge = config.GrazeComboGaugeGain;
            int grazeRadius = config.GrazeExtraRadiusSubUnits;

            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.PlayerMinX = -10000;
            config.PlayerMaxX = 10000;
            config.PlayerMinY = -10000;
            config.PlayerMaxY = 10000;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.PlayerSpeedNumerator = 0;
            config.PlayerSpeedDenominator = 1;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            config.EnemyBulletDamage = 0;
            config.EnemyBulletSpeedNumerator = 0;
            config.EnemyBulletSpeedDenominator = 1;
            config.EnemyBulletHalfWidth = 0;
            config.EnemyBulletHalfHeight = 0;
            config.MaxEnemyBullets = 4;
            config.MaxBullets = 16;
            config.CapsuleNoDropWeight = 1;
            config.CapsuleHalfWidth = 0;
            config.CapsuleHalfHeight = 0;
            config.MainShotBaseDamage = 99;
            config.FireIntervalTicks = 1;
            config.MainShotMinimumFireIntervalTicks = 1;
            config.PlayerBulletSpeedNumerator = SimSpace.SubUnitsPerWorldUnit;
            config.PlayerBulletSpeedDenominator = 1;
            config.BulletDespawnX = 100000;
            config.EnemyDespawnX = -100000;

            // Zero half-sizes: bullet at (0, grazeRadius) is inside graze, outside hit.
            // ctor: id, name, hp, contact, score, pattern, speedN/D, fire, halfW/H,
            // drop, sineAmpN/D, sinePeriod
            var enemies = new[]
            {
                new EnemyDefinition(
                    "graze_turret",
                    "graze_turret",
                    9999,
                    0,
                    0,
                    EnemyMovePattern.Static,
                    0,
                    1,
                    1,
                    0,
                    0,
                    0,
                    0,
                    1,
                    1),
                new EnemyDefinition(
                    "fodder_score",
                    "fodder_score",
                    1,
                    0,
                    fodderScore,
                    EnemyMovePattern.Static,
                    0,
                    1,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1,
                    1)
            };

            var spawns = new[]
            {
                new SpawnEvent(0, "graze_turret", 0, grazeRadius),
                new SpawnEvent(0, "fodder_score", SimSpace.SubUnitsPerWorldUnit, 0)
            };

            var weapon = new WeaponDefinition(
                "main_shot",
                config.MainShotBaseDamage,
                config.FireIntervalTicks,
                config.PlayerBulletSpeedNumerator,
                config.PlayerBulletSpeedDenominator,
                0,
                0);
            var content = new BattleContent(enemies, new[] { weapon }, weapon.Id);
            var segment = new StageSegment(
                "scoring_smoke",
                120,
                spawns,
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
            PowerUpGauge gauge = PowerUpGauge.CreateDefault();
            gauge.ImportLevels(new[] { 0, 0, 0, 0 });

            var sim = new BattleSim(
                config,
                new Rng(0x5C0UL),
                plan,
                content,
                gauge,
                BattleModifier.None);

            InputCommand none = InputCommand.None;
            InputCommand fire = new InputCommand(0, 0, true);

            sim.Step(in none);
            long scoreAfterGraze = sim.Score;
            long grazeCount = sim.Statistics.GrazeCount;
            int gaugeAfterGraze = sim.ComboGauge;

            if (grazeCount < 1)
            {
                Console.WriteLine(
                    $"FAIL scoring smoke: expected graze on tick1 " +
                    $"(score={scoreAfterGraze}, gauge={gaugeAfterGraze}, " +
                    $"radius={grazeRadius}).");
                failures++;
            }
            else
            {
                if (scoreAfterGraze != grazeScore)
                {
                    Console.WriteLine(
                        $"FAIL scoring smoke: graze score={scoreAfterGraze} " +
                        $"expected {grazeScore}.");
                    failures++;
                }
                if (gaugeAfterGraze != grazeCharge)
                {
                    Console.WriteLine(
                        $"FAIL scoring smoke: graze gauge={gaugeAfterGraze} " +
                        $"expected {grazeCharge}.");
                    failures++;
                }
                Console.WriteLine(
                    $"  smoke graze: score+{scoreAfterGraze} gauge={gaugeAfterGraze} " +
                    $"(grazes={grazeCount})");
            }

            long scoreBeforeKill = sim.Score;
            int multBefore = sim.ScoreMultiplier;
            bool killed = false;
            for (int i = 0; i < 30; i++)
            {
                sim.Step(in fire);
                if (sim.Statistics.Kills >= 1)
                {
                    killed = true;
                    break;
                }
            }

            if (!killed)
            {
                Console.WriteLine("FAIL scoring smoke: fodder not killed in 30 fire ticks.");
                failures++;
            }
            else
            {
                long killPoints = sim.Score - scoreBeforeKill;
                // Extra grazes may land same tick; kill contribution is mult * score.
                long expectedKill = (long)fodderScore * multBefore;
                if (killPoints < expectedKill)
                {
                    Console.WriteLine(
                        $"FAIL scoring smoke: Δscore={killPoints} < " +
                        $"kill floor {expectedKill} (fodder {fodderScore}×{multBefore}).");
                    failures++;
                }
                else
                {
                    Console.WriteLine(
                        $"  smoke kill: floor={expectedKill} Δscore={killPoints} " +
                        $"mult=x{multBefore} total={sim.Score} " +
                        $"catalogRef={fodderId}:{fodderScore}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL scoring smoke: {ex.Message}");
            failures++;
        }

        return failures;
    }

    static int CeilDiv(int numerator, int denominator)
    {
        if (denominator <= 0)
            return int.MaxValue;
        return (numerator + denominator - 1) / denominator;
    }

    static string FormatCount(int value) =>
        value == int.MaxValue ? "∞" : value.ToString();

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
