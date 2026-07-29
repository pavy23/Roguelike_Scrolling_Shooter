// Headless balance checks for GameData (GROK).
// 1) Theme assembly: stages 1..10 × difficulty 1..5 must all assemble.
// 2) Reward catalog: modifier rewards parse + weight / maxPerRun guide checks.
// 3) Modifier combo: pierce + kill_explosion dense-pack clear-time (DPS runaway).
// 4) Scoring: graze/combo curves from scoring.json (x8 maintain + graze vs kill).
// 5) Bullet density stress: stage-5 core worst-case enemy pool + full-power player
//    vs Core MaxEnemyBullets / MaxBullets (limits are CODEX-owned; report only).
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

    // Soft headroom guide for pool caps (WARN only — does not fail).
    // Recommended cap ~= ceil(theoreticalPeak * (1 + BulletPoolHeadroomFraction)).
    const double BulletPoolHeadroomFraction = 0.25;
    const int Stage5Index = 5;
    const int Stage5Difficulty = 5;
    const int DensitySimSeedCount = 24;
    const int DensityElevatedEnemyCap = 512;
    const int DensityElevatedPlayerCap = 512;

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
        failures += CheckBulletDensityStress(data, generator);

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

    /// <summary>
    /// Stage-5 core max enemy-bullet density + full-power player bullets vs Core caps.
    /// WARN-only on overflow; limits are CODEX-owned (do not mutate Max* here).
    /// </summary>
    static int CheckBulletDensityStress(GameDataSet data, SegmentStageGenerator generator)
    {
        int failures = 0;
        BattleSimConfig defaults = BattleSimConfig.CreateDefault();
        int maxEnemyBullets = defaults.MaxEnemyBullets;
        int maxPlayerBullets = defaults.MaxBullets;

        Console.WriteLine(
            "Bullet density stress (stage 5 core worst-case + full-power player):");
        Console.WriteLine(
            $"  Core caps (BattleSimConfig.CreateDefault): " +
            $"MaxEnemyBullets={maxEnemyBullets} MaxBullets={maxPlayerBullets}");
        Console.WriteLine(
            $"  Headroom guide: {(BulletPoolHeadroomFraction * 100):F0}% above theoretical peak " +
            "(WARN only; cap changes are CODEX-owned).");

        try
        {
            failures += CheckEnemyBulletDensity(
                data, generator, maxEnemyBullets);
            Console.WriteLine();
            failures += CheckPlayerBulletDensity(data, maxPlayerBullets);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL density: unhandled {ex.GetType().Name}: {ex.Message}");
            failures++;
        }

        if (failures == 0)
            Console.WriteLine("PASS: bullet density stress checks (overflow is WARN-only).");
        return failures;
    }

    static int CheckEnemyBulletDensity(
        GameDataSet data,
        SegmentStageGenerator generator,
        int maxEnemyBullets)
    {
        int failures = 0;
        var catalog = data.StageGeneration;
        if (catalog.ThemeIds.Count < Stage5Index)
        {
            Console.WriteLine(
                $"FAIL density: theme ordinal list has {catalog.ThemeIds.Count} entries; " +
                $"need stage {Stage5Index}.");
            return 1;
        }

        string coreTheme = catalog.ThemeIds[Stage5Index - 1];
        Console.WriteLine(
            $"  Stage {Stage5Index} theme ordinal → '{coreTheme}' " +
            $"(diff={Stage5Difficulty})");

        StageBossTemplate boss = FindBossForStage(catalog, Stage5Index, Stage5Difficulty, coreTheme);
        if (boss == null)
        {
            Console.WriteLine(
                $"FAIL density: no boss matches stage={Stage5Index} " +
                $"diff={Stage5Difficulty} theme={coreTheme}.");
            return 1;
        }

        if (boss.Phases == null || boss.Phases.Count < 2)
        {
            Console.WriteLine(
                $"FAIL density: boss '{boss.BossId}' needs ≥2 phases for phase-2 stress.");
            return 1;
        }

        BossPhase phase2 = boss.Phases[boss.Phases.Count - 1];
        int enemyWaysFaithful = 1; // Core: non-boss shooters fire 1 aimed shot.
        int maxWays = phase2.Ways;

        int spawnX = data.BattleContent != null
            ? InferSpawnXFromCatalog(catalog)
            : 21 * SimSpace.SubUnitsPerWorldUnit;
        // waves.json spawnX is applied at parse; reconstruct from first spawn if present.
        spawnX = InferSpawnXFromSegments(catalog);

        int enemyDespawnX = -(
            SimSpace.PlayfieldHalfWidthSubUnits + SimSpace.DespawnMarginSubUnits);
        int bulletDespawnX = defaults_BulletDespawnX();
        int travelSubUnits = Math.Max(1, boss.HoldX - (-bulletDespawnX));
        if (boss.HoldX <= 0)
            travelSubUnits = Math.Max(1, spawnX - (-bulletDespawnX));

        int enemyBulletLife = LifetimeTicks(
            travelSubUnits,
            defaults_EnemyBulletSpeedNumerator(),
            defaults_EnemyBulletSpeedDenominator());
        int bossBulletLife = LifetimeTicks(
            travelSubUnits,
            phase2.BulletSpeedNumerator,
            phase2.BulletSpeedDenominator);

        Console.WriteLine(
            $"  Boss '{boss.BossId}' phase2: interval={phase2.FireIntervalTicks}t " +
            $"ways={phase2.Ways} speed={phase2.BulletSpeedNumerator}/" +
            $"{phase2.BulletSpeedDenominator} su/tick " +
            $"travel≈{travelSubUnits / (double)SimSpace.SubUnitsPerWorldUnit:F1}u " +
            $"life≈{bossBulletLife}t");
        Console.WriteLine(
            $"  Regular enemy bullet life (Core default speed 8u/s, same travel)≈{enemyBulletLife}t");
        Console.WriteLine(
            $"  Enemy n-way: faithful={enemyWaysFaithful} (Core aimed single); " +
            $"stress maxWays={maxWays} (apply boss phase2 ways to every concurrent shooter)");

        int peakEnemies = 0;
        int peakShooters = 0;
        string peakSegId = "<none>";
        var coreSegs = new List<StageSegmentTemplate>();
        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            if (!seg.SupportsTheme(coreTheme) || !seg.SupportsDifficulty(Stage5Difficulty))
                continue;
            coreSegs.Add(seg);
            ConcurrentPeak peak = EstimateSegmentConcurrentPeak(
                seg,
                data.BattleContent,
                spawnX,
                enemyDespawnX,
                data.ScrollSpeedNumerator,
                data.ScrollSpeedDenominator);
            Console.WriteLine(
                $"  seg {seg.SegmentId,-36} peakEnemies={peak.Enemies,2} " +
                $"peakShooters={peak.Shooters,2} " +
                $"shooterRateSum={peak.ShooterFireRateSum:F3}/t");
            if (peak.Shooters > peakShooters
                || (peak.Shooters == peakShooters && peak.Enemies > peakEnemies))
            {
                peakShooters = peak.Shooters;
                peakEnemies = peak.Enemies;
                peakSegId = seg.SegmentId;
            }
        }

        if (coreSegs.Count == 0)
        {
            Console.WriteLine(
                $"FAIL density: no segments for theme={coreTheme} " +
                $"diff={Stage5Difficulty}.");
            return 1;
        }

        Console.WriteLine(
            $"  Worst concurrent (no-kill model): enemies={peakEnemies} " +
            $"shooters={peakShooters} @ {peakSegId}");

        // Steady-state concurrent bullets from peak shooters (each fires every interval).
        // Faithful uses per-enemy intervals; stress applies maxWays to every shooter.
        int enemyBulletsFaithful = EstimateShooterConcurrentBullets(
            FindSegment(catalog, peakSegId),
            data.BattleContent,
            spawnX,
            enemyDespawnX,
            data.ScrollSpeedNumerator,
            data.ScrollSpeedDenominator,
            enemyBulletLife,
            enemyWaysFaithful);
        int enemyBulletsStressNWay = EstimateShooterConcurrentBullets(
            FindSegment(catalog, peakSegId),
            data.BattleContent,
            spawnX,
            enemyDespawnX,
            data.ScrollSpeedNumerator,
            data.ScrollSpeedDenominator,
            enemyBulletLife,
            maxWays);

        int bossVolleys = AliveVolleys(bossBulletLife, phase2.FireIntervalTicks);
        int bossConcurrent = bossVolleys * phase2.Ways;

        // Boss phase 2 + residual densest-segment shooters (worst packing assumption).
        int theoFaithful = enemyBulletsFaithful + bossConcurrent;
        int theoStress = enemyBulletsStressNWay + bossConcurrent;

        Console.WriteLine(
            $"  Theoretical enemy bullets (densest seg shooters + boss p2 simultaneous):");
        Console.WriteLine(
            $"    faithful (1-way fodder): fodder={enemyBulletsFaithful} " +
            $"+ boss={bossConcurrent} (volleys={bossVolleys}×ways={phase2.Ways}) " +
            $"= {theoFaithful}");
        Console.WriteLine(
            $"    stress (all shooters {maxWays}-way): fodder={enemyBulletsStressNWay} " +
            $"+ boss={bossConcurrent} = {theoStress}");

        ReportCapComparison(
            "MaxEnemyBullets faithful theo",
            maxEnemyBullets,
            theoFaithful);
        ReportCapComparison(
            "MaxEnemyBullets stress n-way theo",
            maxEnemyBullets,
            theoStress);
        ReportCapComparison(
            "MaxEnemyBullets boss p2 alone",
            maxEnemyBullets,
            bossConcurrent);

        // Headless: (A) generated stage-5 plans across seeds, (B) forced densest core×N + boss.
        int genPeakEnemyBullets = 0;
        int genPeakEnemies = 0;
        int genPeakTick = 0;
        string genPeakSegs = "";
        ulong genPeakSeed = 0;
        for (int i = 0; i < DensitySimSeedCount; i++)
        {
            ulong seed = 0xD5E5UL + (ulong)(i * 9973);
            StagePlan plan = generator.Generate(seed, Stage5Index, Stage5Difficulty);
            DensityProbeResult probe = ProbeEnemyBulletPeak(
                data, plan, DensityElevatedEnemyCap, fireAtBoss: true);
            if (probe.PeakEnemyBullets > genPeakEnemyBullets)
            {
                genPeakEnemyBullets = probe.PeakEnemyBullets;
                genPeakEnemies = probe.PeakEnemies;
                genPeakTick = probe.PeakBulletTick;
                genPeakSegs = string.Join(",", SegmentIds(plan));
                genPeakSeed = seed;
            }
        }

        StageSegmentTemplate densestSeg = FindSegment(catalog, peakSegId);
        StagePlan worstPlan = BuildWorstCaseCorePlan(
            data, densestSeg, boss, catalog.SegmentsPerStage);
        DensityProbeResult worstProbe = ProbeEnemyBulletPeak(
            data, worstPlan, DensityElevatedEnemyCap, fireAtBoss: true);
        // Boss-only lab: short empty segment, no fodder — pure phase packing.
        DensityProbeResult bossOnlyProbe = ProbeBossOnlyBulletPeak(
            data, boss, DensityElevatedEnemyCap);

        Console.WriteLine(
            $"  Headless probe A ({DensitySimSeedCount} generated stage-{Stage5Index} seeds, " +
            $"MaxEnemyBullets={DensityElevatedEnemyCap}, fire→boss for phase2):");
        Console.WriteLine(
            $"    peakEnemyBullets={genPeakEnemyBullets} peakEnemies={genPeakEnemies} " +
            $"@tick={genPeakTick} seed=0x{genPeakSeed:X} segs=[{genPeakSegs}]");
        ReportCapComparison(
            "MaxEnemyBullets sim gen",
            maxEnemyBullets,
            genPeakEnemyBullets);

        Console.WriteLine(
            $"  Headless probe B (forced densest core×{catalog.SegmentsPerStage} + boss, " +
            $"MaxEnemyBullets={DensityElevatedEnemyCap}):");
        Console.WriteLine(
            $"    peakEnemyBullets={worstProbe.PeakEnemyBullets} " +
            $"peakEnemies={worstProbe.PeakEnemies} " +
            $"peakDuringPhase2={worstProbe.PeakEnemyBulletsInPhase2} " +
            $"@tick={worstProbe.PeakBulletTick} segs=[{string.Join(",", SegmentIds(worstPlan))}]");
        ReportCapComparison(
            "MaxEnemyBullets sim worst-core",
            maxEnemyBullets,
            worstProbe.PeakEnemyBullets);
        ReportCapComparison(
            "MaxEnemyBullets sim worst p2 window",
            maxEnemyBullets,
            worstProbe.PeakEnemyBulletsInPhase2);

        Console.WriteLine(
            $"  Headless probe C (boss-only lab, elevated cap, force phase2 then hold):");
        Console.WriteLine(
            $"    peakEnemyBullets={bossOnlyProbe.PeakEnemyBullets} " +
            $"peakDuringPhase2={bossOnlyProbe.PeakEnemyBulletsInPhase2} " +
            $"@tick={bossOnlyProbe.PeakBulletTick}");
        ReportCapComparison(
            "MaxEnemyBullets sim boss-only p2",
            maxEnemyBullets,
            Math.Max(bossOnlyProbe.PeakEnemyBullets, bossOnlyProbe.PeakEnemyBulletsInPhase2));

        // Primary = max of observed sim peaks and boss-p2-alone theoretical floor.
        // Full faithful (peak shooters + boss) is an upper packing bound if residuals
        // and boss p2 ever co-exist without player culling; report separately.
        int observedPeak = Math.Max(
            genPeakEnemyBullets,
            Math.Max(
                worstProbe.PeakEnemyBullets,
                Math.Max(
                    bossOnlyProbe.PeakEnemyBullets,
                    bossOnlyProbe.PeakEnemyBulletsInPhase2)));
        int primaryPeak = Math.Max(bossConcurrent, observedPeak);
        if (primaryPeak > maxEnemyBullets
            || theoFaithful > maxEnemyBullets
            || theoStress > maxEnemyBullets)
        {
            int recommendPrimary = RecommendCap(primaryPeak);
            int recommendFaithful = RecommendCap(theoFaithful);
            int recommendStress = RecommendCap(theoStress);
            Console.WriteLine(
                "  RECOMMEND (CODEX MaxEnemyBullets / GROK waves — values NOT changed):");
            Console.WriteLine(
                $"    Primary (boss p2 theo floor + sim peaks): MaxEnemyBullets >={recommendPrimary} " +
                $"(peak~{primaryPeak}, +{BulletPoolHeadroomFraction * 100:F0}% headroom).");
            Console.WriteLine(
                $"    Upper packing bound (densest shooters + boss p2, 1-way fodder): >={recommendFaithful} " +
                $"(theo={theoFaithful}) if residual turrets ever co-fire with p2.");
            Console.WriteLine(
                $"    Extreme stress (every shooter {maxWays}-way): >={recommendStress} " +
                $"(theo={theoStress}) - only if Core adds multi-way to fodder.");
            Console.WriteLine(
                $"    waves.json levers: boss_core phase2 ways {phase2.Ways}->7 or " +
                $"interval {phase2.FireIntervalTicks}->45 (boss p2 alone theo={bossConcurrent} " +
                $"vs cap {maxEnemyBullets}); thin {peakSegId} concurrent shooters (peak {peakShooters}).");
            Console.WriteLine(
                "    Note: Core clamps enemy spawns at MaxEnemyBullets (silent drop) — " +
                "overflow = missing threat, not a crash. Sim peaks sit near the current " +
                "cap when residual enemies are killed; theoretical boss p2 alone already exceeds.");
        }

        return failures;
    }

    static int CheckPlayerBulletDensity(GameDataSet data, int maxPlayerBullets)
    {
        int failures = 0;
        BattleSimConfig config = data.CreateBattleSimConfig();
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        int mainMax = gauge.GetMaxLevel(PowerUpSlot.MainShot);
        int missileMax = gauge.GetMaxLevel(PowerUpSlot.Missile);
        int optionMax = gauge.GetMaxLevel(PowerUpSlot.Option);
        gauge.ImportLevels(new[] { mainMax, missileMax, optionMax, 0 });

        int mainInterval = ReducedInterval(
            config.FireIntervalTicks,
            mainMax,
            config.MainShotRapidFireStartLevel,
            config.MainShotFireIntervalReductionPerLevel,
            config.MainShotMinimumFireIntervalTicks);
        int missileInterval = ReducedInterval(
            config.MissileFireIntervalTicks,
            missileMax,
            config.MissileRapidFireStartLevel,
            config.MissileFireIntervalReductionPerLevel,
            config.MissileMinimumFireIntervalTicks);

        int beamsPerVolley = 1 + optionMax; // main + each option
        int bulletDespawnX = config.BulletDespawnX > 0
            ? config.BulletDespawnX
            : defaults_BulletDespawnX();
        int playerSpawnX = config.PlayerSpawnX;
        int mainTravel = Math.Max(1, bulletDespawnX - playerSpawnX);
        int mainLife = LifetimeTicks(
            mainTravel,
            config.PlayerBulletSpeedNumerator,
            config.PlayerBulletSpeedDenominator);
        // Missile falls; use horizontal component only as a lower-bound lifetime.
        int missileLife = LifetimeTicks(
            mainTravel,
            config.MissileSpeedXNumerator,
            config.MissileSpeedXDenominator);

        int mainVolleys = AliveVolleys(mainLife, mainInterval);
        int mainConcurrent = mainVolleys * beamsPerVolley;
        int missileConcurrent = AliveVolleys(missileLife, missileInterval);

        // Pierce keeps a bullet alive after first hit; ricochet once can reverse/redirect
        // and roughly double on-screen time in the worst packing case.
        int pierceExtraHits = config.PierceShotEnemyCount; // default 1 → 2 enemies total
        double pierceLifeFactor = 1.0 + 0.15 * pierceExtraHits; // modest extension before despawn
        double ricochetLifeFactor = 1.85; // one reverse trip across most of the field
        int mainConcurrentPierceRicochet = (int)Math.Ceiling(
            mainConcurrent * pierceLifeFactor * ricochetLifeFactor);
        int theoPlayer = mainConcurrentPierceRicochet + missileConcurrent;

        Console.WriteLine("  Player full-power analytical (levels Main/Mis/Opt max):");
        Console.WriteLine(
            $"    levels Main={mainMax} Missile={missileMax} Option={optionMax} " +
            $"(shield ignored for pool)");
        Console.WriteLine(
            $"    mainInterval={mainInterval}t beams/volley={beamsPerVolley} " +
            $"life≈{mainLife}t volleys={mainVolleys} → mainConcurrent={mainConcurrent}");
        Console.WriteLine(
            $"    missileInterval={missileInterval}t life≈{missileLife}t " +
            $"→ missileConcurrent={missileConcurrent}");
        Console.WriteLine(
            $"    pierce×ricochet lifetime uplift: ×{pierceLifeFactor:F2}×{ricochetLifeFactor:F2} " +
            $"→ main'={mainConcurrentPierceRicochet}");
        Console.WriteLine(
            $"    theoretical MaxBullets demand ≈ {theoPlayer} " +
            $"(main'+missile, no hit despawn floor)");

        int theoNoMod = mainConcurrent + missileConcurrent;
        ReportCapComparison("MaxBullets no-mod theo", maxPlayerBullets, theoNoMod);
        ReportCapComparison("MaxBullets pierce+ricochet theo", maxPlayerBullets, theoPlayer);

        // Headless: elevated MaxBullets, full power, pierce+ricochet, dense invincible pack.
        DensityProbeResult playerProbe = ProbePlayerBulletPeak(
            data, DensityElevatedPlayerCap, mainMax, missileMax, optionMax);
        Console.WriteLine(
            $"  Headless probe (MaxBullets={DensityElevatedPlayerCap}, " +
            $"pierce+ricochet, dense high-HP pack, fire always):");
        Console.WriteLine(
            $"    peakPlayerBullets={playerProbe.PeakPlayerBullets} " +
            $"@tick={playerProbe.PeakBulletTick} " +
            $"(main+missile entities; cap was elevated so silent drop is off)");
        ReportCapComparison(
            "MaxBullets sim peak",
            maxPlayerBullets,
            playerProbe.PeakPlayerBullets);

        // Primary demand: no-mod floor + observed sim (pierce/ricochet uplift is softer).
        int primaryPlayer = Math.Max(theoNoMod, playerProbe.PeakPlayerBullets);
        if (primaryPlayer > maxPlayerBullets || theoPlayer > maxPlayerBullets)
        {
            int recommendPrimary = RecommendCap(primaryPlayer);
            int recommendUplift = RecommendCap(Math.Max(theoPlayer, primaryPlayer));
            Console.WriteLine(
                "  RECOMMEND (CODEX MaxBullets — value NOT changed):");
            Console.WriteLine(
                $"    Primary (full-power no-mod theo + sim): >={recommendPrimary} " +
                $"(peak~{primaryPlayer}, +{BulletPoolHeadroomFraction * 100:F0}% headroom).");
            Console.WriteLine(
                $"    With pierce+ricochet lifetime uplift: >={recommendUplift} " +
                $"(theo uplift~{theoPlayer}).");
            Console.WriteLine(
                "    Alternate levers: lower option max, raise main/missile intervals, " +
                "or rely on existing deterministic volley drop near cap.");
            Console.WriteLine(
                "    Pierce/ricochet do not spawn extra entities but extend lifetime; " +
                "pool pressure is mostly option multi-beam + fire rate.");
        }

        return failures;
    }

    static StagePlan BuildWorstCaseCorePlan(
        GameDataSet data,
        StageSegmentTemplate densest,
        StageBossTemplate boss,
        int segmentsPerStage)
    {
        var segments = new StageSegment[segmentsPerStage];
        for (int i = 0; i < segmentsPerStage; i++)
            segments[i] = densest.CreateSegment();

        return new StagePlan(
            segments,
            boss.BossId,
            data.StageGeneration.LaneCount,
            data.StageGeneration.StartLaneMask,
            boss.EntryLaneMask,
            boss.MaxHp,
            boss.HalfWidth,
            boss.HalfHeight,
            boss.HoldX,
            boss.Phases,
            boss.ThemeId);
    }

    /// <summary>
    /// Empty short stage + boss only. Fire until phase 2, then stop firing so
    /// phase-2 n-way can reach steady packing without defeating the boss.
    /// </summary>
    static DensityProbeResult ProbeBossOnlyBulletPeak(
        GameDataSet data,
        StageBossTemplate boss,
        int elevatedEnemyCap)
    {
        var empty = new StageSegment(
            "density_empty",
            30,
            Array.Empty<SpawnEvent>(),
            boss.EntryLaneMask,
            boss.EntryLaneMask,
            new[] { boss.EntryLaneMask });
        var plan = new StagePlan(
            new[] { empty },
            boss.BossId,
            data.StageGeneration.LaneCount,
            data.StageGeneration.StartLaneMask,
            boss.EntryLaneMask,
            boss.MaxHp,
            boss.HalfWidth,
            boss.HalfHeight,
            boss.HoldX,
            boss.Phases,
            boss.ThemeId);

        BattleSimConfig config = data.CreateBattleSimConfig();
        config.MaxEnemyBullets = elevatedEnemyCap;
        config.MaxBullets = 256;
        config.PlayerMaxHp = 99999;
        config.EnemyBulletDamage = 0;
        config.PlayerHalfWidth = 0;
        config.PlayerHalfHeight = 0;
        config.EnemyBulletHalfWidth = 0;
        config.EnemyBulletHalfHeight = 0;
        config.CapsuleNoDropWeight = 1_000_000;
        // Modest DPS so the boss survives entry and reaches holdX before phase push.
        config.MainShotBaseDamage = 15;
        config.UseConfiguredMainShotStats = true;
        config.FireIntervalTicks = 6;
        config.MainShotMinimumFireIntervalTicks = 6;

        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        gauge.ImportLevels(new[] { 1, 0, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xB055UL),
            plan,
            data.BattleContent,
            gauge,
            BattleModifier.None);

        int peak = 0;
        int peakP2 = 0;
        int peakTick = 0;
        bool inPhase2 = false;
        int holdTicks = 0;
        const int holdAfterPhase2 = 20 * SimSpace.TicksPerSecond;
        int holdX = boss.HoldX != 0
            ? boss.HoldX
            : 14 * SimSpace.SubUnitsPerWorldUnit;
        InputCommand fire = new InputCommand(0, 0, true);
        InputCommand none = InputCommand.None;

        for (int t = 0; t < 180 * SimSpace.TicksPerSecond; t++)
        {
            // Wait until boss has finished entry (at holdX) so phase-2 volleys exist.
            // Fire only until phase 2, then hold so we do not melt the boss.
            bool atHold = sim.BossActive && sim.Boss.X <= holdX;
            bool shouldFire = atHold && !inPhase2;
            InputCommand input = shouldFire ? fire : none;
            sim.Step(in input);

            if (!sim.BossActive)
            {
                if (sim.Tick > 90 * SimSpace.TicksPerSecond)
                    break;
                continue;
            }

            if (sim.Boss.Phase >= 1)
                inPhase2 = true;

            int enemyBullets = 0;
            IReadOnlyList<BulletState> bullets = sim.Bullets;
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i].Faction == BulletFaction.Enemy)
                    enemyBullets++;
            }
            if (enemyBullets > peak)
            {
                peak = enemyBullets;
                peakTick = sim.Tick;
            }
            if (inPhase2 && enemyBullets > peakP2)
                peakP2 = enemyBullets;

            if (inPhase2)
            {
                holdTicks++;
                if (holdTicks >= holdAfterPhase2)
                    break;
            }
        }

        return new DensityProbeResult(peak, 0, 1, peakTick, peakP2);
    }

    static DensityProbeResult ProbeEnemyBulletPeak(
        GameDataSet data,
        StagePlan plan,
        int elevatedEnemyCap,
        bool fireAtBoss)
    {
        BattleSimConfig config = data.CreateBattleSimConfig();
        config.MaxEnemyBullets = elevatedEnemyCap;
        config.MaxBullets = fireAtBoss ? 128 : 16;
        config.PlayerMaxHp = 99999;
        config.EnemyBulletDamage = 0;
        config.CapsuleNoDropWeight = 1_000_000;
        // Zero hitboxes so aimed enemy bullets are not culled on player contact —
        // we want pure on-screen packing vs MaxEnemyBullets, not graze/hit rates.
        config.PlayerHalfWidth = 0;
        config.PlayerHalfHeight = 0;
        config.EnemyBulletHalfWidth = 0;
        config.EnemyBulletHalfHeight = 0;
        // Keep contact from wiping the probe when we fly into residual enemies.
        config.MainShotBaseDamage = fireAtBoss ? 40 : 0;
        config.UseConfiguredMainShotStats = true;
        config.FireIntervalTicks = 4;
        config.MainShotMinimumFireIntervalTicks = 4;

        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        // Mild option for boss DPS without filling the elevated player pool.
        gauge.ImportLevels(fireAtBoss ? new[] { 3, 0, 1, 0 } : new[] { 0, 0, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xEBUL),
            plan,
            data.BattleContent,
            gauge,
            BattleModifier.None);

        int stageTicks = 0;
        for (int i = 0; i < plan.Segments.Count; i++)
            stageTicks += plan.Segments[i].LengthTicks;
        int maxTicks = stageTicks + 120 * SimSpace.TicksPerSecond;

        int peakEnemyBullets = 0;
        int peakEnemyBulletsPhase2 = 0;
        int peakEnemies = 0;
        int peakTick = 0;
        InputCommand none = InputCommand.None;
        InputCommand fire = new InputCommand(0, 0, true);
        bool sawPhase2 = false;

        for (int t = 0; t < maxTicks; t++)
        {
            bool shouldFire = fireAtBoss && sim.BossActive;
            InputCommand input = shouldFire ? fire : none;
            sim.Step(in input);

            int enemyBullets = 0;
            IReadOnlyList<BulletState> bullets = sim.Bullets;
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i].Faction == BulletFaction.Enemy)
                    enemyBullets++;
            }
            int enemies = sim.Enemies.Count + (sim.BossActive ? 1 : 0);
            if (enemyBullets > peakEnemyBullets)
            {
                peakEnemyBullets = enemyBullets;
                peakTick = sim.Tick;
            }
            if (enemies > peakEnemies)
                peakEnemies = enemies;

            if (sim.BossActive && sim.Boss.Phase >= 1)
            {
                sawPhase2 = true;
                if (enemyBullets > peakEnemyBulletsPhase2)
                    peakEnemyBulletsPhase2 = enemyBullets;
            }

            if (sim.BossActive && sawPhase2
                && sim.Tick > stageTicks + 45 * SimSpace.TicksPerSecond)
                break;
            if (!sim.BossActive && sim.Tick > stageTicks + 10 * SimSpace.TicksPerSecond)
                break;
        }

        return new DensityProbeResult(
            peakEnemyBullets,
            0,
            peakEnemies,
            peakTick,
            peakEnemyBulletsPhase2);
    }

    static DensityProbeResult ProbePlayerBulletPeak(
        GameDataSet data,
        int elevatedPlayerCap,
        int mainLevel,
        int missileLevel,
        int optionLevel)
    {
        BattleSimConfig config = data.CreateBattleSimConfig();
        config.MaxBullets = elevatedPlayerCap;
        config.MaxEnemyBullets = 0;
        config.PlayerMaxHp = 99999;
        config.PlayerSpeedNumerator = 0;
        config.PlayerSpeedDenominator = 1;
        config.ScrollSpeedNumerator = 0;
        config.ScrollSpeedDenominator = 1;
        config.CapsuleNoDropWeight = 1_000_000;
        config.EnemyDespawnX = int.MinValue / 4;

        const int packSize = 16;
        const int enemyHp = 50_000;
        var enemies = new EnemyDefinition[packSize];
        var spawns = new SpawnEvent[packSize];
        int half = SimSpace.SubUnitsPerWorldUnit / 2;
        int spacing = SimSpace.SubUnitsPerWorldUnit;
        for (int i = 0; i < packSize; i++)
        {
            string id = "dense_" + i;
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
            int x = config.PlayerSpawnX
                + 2 * SimSpace.SubUnitsPerWorldUnit
                + (i % 8) * spacing;
            int y = ((i / 8) * 2 - 1) * 2 * SimSpace.SubUnitsPerWorldUnit;
            spawns[i] = new SpawnEvent(0, id, x, y);
        }

        WeaponDefinition main = data.BattleContent.PlayerWeapon;
        var content = new BattleContent(enemies, new[] { main }, main.Id);
        var segment = new StageSegment(
            "player_pool_pack",
            900,
            spawns,
            1,
            1,
            new[] { 1 });
        var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        gauge.ImportLevels(new[] { mainLevel, missileLevel, optionLevel, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xB01UL),
            plan,
            content,
            gauge,
            BattleModifier.PierceShot | BattleModifier.Ricochet);

        int peakPlayer = 0;
        int peakTick = 0;
        var fire = new InputCommand(0, 0, true);
        const int maxTicks = 600;
        for (int t = 0; t < maxTicks; t++)
        {
            sim.Step(in fire);
            int playerBullets = 0;
            IReadOnlyList<BulletState> bullets = sim.Bullets;
            for (int i = 0; i < bullets.Count; i++)
            {
                if (bullets[i].Faction == BulletFaction.Player)
                    playerBullets++;
            }
            if (playerBullets > peakPlayer)
            {
                peakPlayer = playerBullets;
                peakTick = sim.Tick;
            }
        }

        return new DensityProbeResult(0, peakPlayer, 0, peakTick, 0);
    }

    static void ReportCapComparison(string label, int cap, int peak)
    {
        double headroom = cap <= 0
            ? double.NegativeInfinity
            : (cap - peak) / (double)cap;
        string status = peak <= cap ? "OK" : "OVER";
        Console.WriteLine(
            $"  [{status}] {label}: peak={peak} cap={cap} " +
            $"headroom={(headroom * 100):F1}% " +
            (peak <= cap
                ? $"(spare {cap - peak})"
                : $"(overflow {peak - cap}; recommend >={RecommendCap(peak)})"));
    }

    static int RecommendCap(int peak) =>
        Math.Max(peak, (int)Math.Ceiling(peak * (1.0 + BulletPoolHeadroomFraction)));

    static int AliveVolleys(int lifetimeTicks, int intervalTicks)
    {
        if (lifetimeTicks <= 0 || intervalTicks <= 0)
            return 0;
        return ((lifetimeTicks - 1) / intervalTicks) + 1;
    }

    static int LifetimeTicks(int travelSubUnits, int speedNumerator, int speedDenominator)
    {
        if (speedNumerator <= 0 || speedDenominator <= 0)
            return 0;
        // ticks ≈ travel * den / num  (ceil)
        long ticks = ((long)travelSubUnits * speedDenominator + speedNumerator - 1)
            / speedNumerator;
        if (ticks > int.MaxValue)
            return int.MaxValue;
        return (int)ticks;
    }

    static int ReducedInterval(
        int baseInterval,
        int level,
        int reductionStartLevel,
        int reductionPerLevel,
        int minimumInterval)
    {
        int reductions = Math.Max(0, level - reductionStartLevel + 1);
        long reduced = baseInterval - (long)reductions * reductionPerLevel;
        int effectiveMinimum = Math.Min(baseInterval, minimumInterval);
        return (int)Math.Max(effectiveMinimum, reduced);
    }

    static int defaults_BulletDespawnX()
    {
        return SimSpace.PlayfieldHalfWidthSubUnits + SimSpace.SubUnitsPerWorldUnit;
    }

    static int defaults_EnemyBulletSpeedNumerator() =>
        8 * SimSpace.SubUnitsPerWorldUnit;

    static int defaults_EnemyBulletSpeedDenominator() => SimSpace.TicksPerSecond;

    static StageBossTemplate FindBossForStage(
        StageGenerationCatalog catalog,
        int stageIndex,
        int difficulty,
        string themeId)
    {
        StageBossTemplate best = null;
        for (int i = 0; i < catalog.Bosses.Count; i++)
        {
            StageBossTemplate b = catalog.Bosses[i];
            if (!b.Supports(stageIndex, difficulty))
                continue;
            if (!b.SupportsTheme(themeId))
                continue;
            // Prefer theme-tagged boss when multiple match.
            if (best == null
                || (b.ThemeId != null && best.ThemeId == null)
                || (b.ThemeId != null
                    && string.Equals(b.ThemeId, themeId, StringComparison.Ordinal)
                    && best.ThemeId != null
                    && !string.Equals(best.ThemeId, themeId, StringComparison.Ordinal)))
            {
                best = b;
            }
        }
        return best;
    }

    static StageSegmentTemplate FindSegment(StageGenerationCatalog catalog, string id)
    {
        for (int i = 0; i < catalog.Segments.Count; i++)
        {
            if (string.Equals(catalog.Segments[i].SegmentId, id, StringComparison.Ordinal))
                return catalog.Segments[i];
        }
        return null;
    }

    static int InferSpawnXFromCatalog(StageGenerationCatalog catalog) =>
        InferSpawnXFromSegments(catalog);

    static int InferSpawnXFromSegments(StageGenerationCatalog catalog)
    {
        for (int i = 0; i < catalog.Segments.Count; i++)
        {
            IReadOnlyList<SpawnEvent> spawns = catalog.Segments[i].Spawns;
            if (spawns != null && spawns.Count > 0)
                return spawns[0].X;
        }
        return 21 * SimSpace.SubUnitsPerWorldUnit;
    }

    static ConcurrentPeak EstimateSegmentConcurrentPeak(
        StageSegmentTemplate seg,
        BattleContent content,
        int spawnX,
        int enemyDespawnX,
        int scrollNum,
        int scrollDen)
    {
        if (seg == null)
            return default;

        var events = new List<PeakEvent>(seg.Spawns.Count * 2);
        for (int i = 0; i < seg.Spawns.Count; i++)
        {
            SpawnEvent sp = seg.Spawns[i];
            EnemyDefinition def = content.FindEnemy(sp.EnemyId);
            if (def == null)
                continue;
            int life = EnemyLifeTicks(def, spawnX, enemyDespawnX, scrollNum, scrollDen);
            int death = sp.Tick + life;
            bool shooter = def.FireIntervalTicks > 0;
            events.Add(new PeakEvent(sp.Tick, 1, shooter, def.FireIntervalTicks));
            events.Add(new PeakEvent(death, -1, shooter, def.FireIntervalTicks));
        }

        events.Sort((a, b) =>
        {
            int c = a.Tick.CompareTo(b.Tick);
            if (c != 0) return c;
            // Process despawns before spawns at the same tick.
            return a.Delta.CompareTo(b.Delta);
        });

        int curE = 0, curS = 0, peakE = 0, peakS = 0;
        double rateSum = 0, peakRate = 0;
        for (int i = 0; i < events.Count; i++)
        {
            PeakEvent e = events[i];
            curE += e.Delta;
            if (e.IsShooter)
            {
                curS += e.Delta;
                if (e.FireInterval > 0)
                {
                    double rate = 1.0 / e.FireInterval;
                    rateSum += e.Delta * rate;
                }
            }
            if (curE > peakE) peakE = curE;
            if (curS > peakS)
            {
                peakS = curS;
                peakRate = rateSum;
            }
            else if (curS == peakS && rateSum > peakRate)
            {
                peakRate = rateSum;
            }
        }

        return new ConcurrentPeak(peakE, peakS, peakRate);
    }

    static int EstimateShooterConcurrentBullets(
        StageSegmentTemplate seg,
        BattleContent content,
        int spawnX,
        int enemyDespawnX,
        int scrollNum,
        int scrollDen,
        int bulletLifeTicks,
        int waysPerShot)
    {
        if (seg == null || waysPerShot < 1)
            return 0;

        // Sweep discrete spawn/despawn of each shooter's steady-state bullet budget.
        // At any time a living shooter contributes AliveVolleys(bulletLife, interval)*ways.
        var events = new List<PeakEvent>(seg.Spawns.Count * 2);
        for (int i = 0; i < seg.Spawns.Count; i++)
        {
            SpawnEvent sp = seg.Spawns[i];
            EnemyDefinition def = content.FindEnemy(sp.EnemyId);
            if (def == null || def.FireIntervalTicks <= 0)
                continue;
            int life = EnemyLifeTicks(def, spawnX, enemyDespawnX, scrollNum, scrollDen);
            int contrib = AliveVolleys(bulletLifeTicks, def.FireIntervalTicks) * waysPerShot;
            events.Add(new PeakEvent(sp.Tick, contrib, true, def.FireIntervalTicks));
            events.Add(new PeakEvent(sp.Tick + life, -contrib, true, def.FireIntervalTicks));
        }

        events.Sort((a, b) =>
        {
            int c = a.Tick.CompareTo(b.Tick);
            if (c != 0) return c;
            return a.Delta.CompareTo(b.Delta);
        });

        int cur = 0, peak = 0;
        for (int i = 0; i < events.Count; i++)
        {
            cur += events[i].Delta;
            if (cur > peak) peak = cur;
        }
        return peak;
    }

    static int EnemyLifeTicks(
        EnemyDefinition def,
        int spawnX,
        int enemyDespawnX,
        int scrollNum,
        int scrollDen)
    {
        // Per-tick leftward motion ≈ scroll + self (non-static).
        long scrollPerTickNum = scrollNum;
        long scrollPerTickDen = scrollDen;
        long selfNum = 0;
        long selfDen = 1;
        if (def.MovePattern != EnemyMovePattern.Static)
        {
            selfNum = def.MoveSpeedNumerator;
            selfDen = def.MoveSpeedDenominator;
        }

        // Combined rational speed: n1/d1 + n2/d2 = (n1*d2 + n2*d1)/(d1*d2)
        long speedNum = scrollPerTickNum * selfDen + selfNum * scrollPerTickDen;
        long speedDen = scrollPerTickDen * selfDen;
        if (speedNum <= 0)
            return int.MaxValue / 4;

        long travel = (long)spawnX - enemyDespawnX;
        if (travel < 0) travel = 0;
        long ticks = (travel * speedDen + speedNum - 1) / speedNum;
        if (ticks > int.MaxValue) return int.MaxValue;
        return Math.Max(1, (int)ticks);
    }

    readonly struct ConcurrentPeak
    {
        public ConcurrentPeak(int enemies, int shooters, double shooterFireRateSum)
        {
            Enemies = enemies;
            Shooters = shooters;
            ShooterFireRateSum = shooterFireRateSum;
        }

        public int Enemies { get; }
        public int Shooters { get; }
        public double ShooterFireRateSum { get; }
    }

    readonly struct PeakEvent
    {
        public PeakEvent(int tick, int delta, bool isShooter, int fireInterval)
        {
            Tick = tick;
            Delta = delta;
            IsShooter = isShooter;
            FireInterval = fireInterval;
        }

        public int Tick { get; }
        public int Delta { get; }
        public bool IsShooter { get; }
        public int FireInterval { get; }
    }

    readonly struct DensityProbeResult
    {
        public DensityProbeResult(
            int peakEnemyBullets,
            int peakPlayerBullets,
            int peakEnemies,
            int peakBulletTick,
            int peakEnemyBulletsInPhase2)
        {
            PeakEnemyBullets = peakEnemyBullets;
            PeakPlayerBullets = peakPlayerBullets;
            PeakEnemies = peakEnemies;
            PeakBulletTick = peakBulletTick;
            PeakEnemyBulletsInPhase2 = peakEnemyBulletsInPhase2;
        }

        public int PeakEnemyBullets { get; }
        public int PeakPlayerBullets { get; }
        public int PeakEnemies { get; }
        public int PeakBulletTick { get; }
        public int PeakEnemyBulletsInPhase2 { get; }
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
