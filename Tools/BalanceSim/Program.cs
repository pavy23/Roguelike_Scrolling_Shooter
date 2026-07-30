// Headless balance checks for GameData (GROK).
// 1) Theme assembly: stages 1..10 × difficulty 1..5 must all assemble.
// 2) REQ-026: every theme × difficulty 1..5 assembles (theme-shuffle ready).
// 3) REQ-026: theme segment counts, stage-1 pool size, stage-index HP monotonicity.
// 4) Reward catalog: modifier rewards parse + weight / maxPerRun guide checks.
// 5) Modifier combo: pierce + kill_explosion dense-pack clear-time (DPS runaway).
// 6) Scoring: graze/combo curves from scoring.json (x8 maintain + graze vs kill).
// 7) Bullet density stress: stage-5 core worst-case enemy pool + full-power player
//    vs Core MaxEnemyBullets / MaxBullets (limits are CODEX-owned; report only).
// 8) Obstacles: stage-1 empty + progressive density + solid corridor gaps (REQ-023).
// 9) Ship primary DPS: vulcan/laser/spread single-target balance (REQ-022).
// 10) Segment weights: catalog bias for common vs spectacle segments (REQ-029).
// 11) Encounter types: Normal/Elite/Supply/Hazard/Rare risk-reward sketch (REQ-028/029).
// 12) Capsule drops after magnet: expected recovery band (REQ-029).
// 13) Boss redesign: TTK 22–32s @ 4-room avg biome DPS, full-power ≥6s, 3 phases, threat mono.
// 14) REQ-034: missile families + option formations ST DPS / situation roles / combo gates.
// 15) REQ-035: colossal bosses (parts sum/core TTK 100–120s, full ≥40s, brood spawn cap, parity).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // Obstacle corridor: default half-height 0.5u; need gap ≥ player hitbox + margin.
    const int MinSolidCorridorGapSubUnits = SimSpace.SubUnitsPerWorldUnit; // 1.0u
    // Ship DPS soft band at level 0 main shot only (single target). Provisional §7.
    const double MaxShipSingleTargetDpsRatio = 1.75;
    const int ShipDpsSimTicks = 180;

    // REQ-026 theme coverage (provisional §7).
    const int MinThemeTaggedSegments = 5;
    const int MinStage1CandidateSegments = 6;
    const int ThemeDiffAssemblySeedCount = 8;

    // Segment weights (REQ-029, provisional §7).
    const int ExpectedSegmentCount = 38;
    const int DefaultSegmentWeight = StageSegmentTemplate.DefaultWeight;
    const int MinWeightedLowCount = 4;   // spectacle / maze / dense
    const int MaxWeightedLow = 5;        // weight ≤ this counts as low
    const int MinWeightedHighCount = 4;  // plain workhorse
    const int MinWeightedHigh = 10;      // weight ≥ this counts as high

    // Encounter sketch (REQ-028/029, provisional §7). WARN bands only.
    const int EncounterSampleSeeds = 16;
    // Elite total load includes same boss as Normal; boss-heavy stages push ratio ~0.9.
    const double EliteHpRatioMin = 0.35; // elite load vs normal (short node)
    const double EliteHpRatioMax = 0.95;
    const double SupplyHpRatioMax = 0.40; // supply must stay clearly lighter
    const double RareHpRatioMin = 1.5;    // rare ≈ 2× HP on full 3-seg
    const double RareHpRatioMax = 2.5;
    const double HazardScoreMult = 1.5;   // Core: 3/2
    const double RareEncounterChance = 0.12; // Core default 12/100
    // Nonlinear p=sw/(noDrop+sw): drop×4 yields ~1.3–1.6× EV at noDrop=12, not full 4×.
    const double SupplyCapsuleRatioMin = 1.30; // supply drop boost vs normal 1-seg

    // Capsule magnet recovery band (REQ-029, provisional §7).
    // Magnet makes near-full pickup realistic; stage = 3 segments weight-biased mean.
    const double MinStageCapsuleExpectation = 10.0;
    const double MaxStageCapsuleExpectation = 16.0;
    const double MaxSupplyNodeCapsuleExpectation = 18.0;

    // Boss redesign TTK / phase gates (playtest 2026-07-30: first boss tutorial-short).
    // Biome path: 4 rooms then boss — average build DPS at reach, not full-power max.
    // First boss is a short "learn the boss fight" beat (~18s @ mid DPS);
    // later bosses stay in the 22–32s mid band and lengthen toward the finale.
    const double BossFullPowerDps = 1880.0;
    const double BossTtkExpectedMin = 16.0;
    const double BossTtkExpectedMax = 32.0;
    const double BossTtkFullMin = 4.5;
    const int BossRequiredPhaseCount = 3;
    // Equal-split remaining-HP ratios for phase 1 / phase 2 (Core N-way equal split).
    const double BossPhaseThreshold0 = 2.0 / 3.0; // enter phase 1
    const double BossPhaseThreshold1 = 1.0 / 3.0; // enter phase 2
    // Expected biome-reach DPS anchors (see analyze_stage_hp.py) — 4-room average.
    static readonly (string Id, double ExpectedDps)[] BossExpectedDps =
    {
        ("boss_stage1", 500.0),
        ("boss_hive", 600.0),
        ("boss_fortress", 720.0),
        ("boss_storm", 880.0),
        ("boss_core", 1050.0),
    };

    // REQ-034 missile family / option formation gates (provisional §7).
    // Playtest 2026-07-30: missile fire rate lowered (support weapon, less screen fill).
    // ST DPS bands rebased around longer base intervals (straight 42t / bomb 54t / lance 70t).
    const int MissileRapidFireStartLevel = 2;
    const int MissileFamilyStSimTicks = 300;
    const double MissileFamilyL1StMin = 26.0;
    const double MissileFamilyL1StMax = 40.0;
    const double MissileFamilyL3StMin = 70.0;
    const double MissileFamilyL3StMax = 100.0;
    const double MissileFamilyStMaxMinRatio = 1.25; // three lineages stay in same ST band
    const double LancePierceShotClearRatioMax = 1.05; // missile-only: pierce_shot must not buff lance
    const double BombKillExpClearRatioMax = 1.40; // bomb splash kills never reseed kill_explosion
    const double BombKillExpVsBaselineWarn = 5.0;

    // REQ-035 colossal bosses (provisional §7).
    // Hidden biome after 5 biomes: mid firepower ~560 DPS → total-HP TTK ~110s.
    // Raw full-power 1880 melts total HP in ~33s; multi-part retarget tax keeps
    // effective ST near ~1500 → floor ≥40s. Gate uses effective full DPS.
    const int ColossalTotalHp = 62_000;
    const int ColossalCoreHp = 25_000;
    const double ColossalExpectedDps = 560.0;
    const double ColossalFullPowerEffectiveDps = 1500.0;
    const double ColossalTtkExpectedMin = 100.0;
    const double ColossalTtkExpectedMax = 120.0;
    const double ColossalTtkFullMin = 40.0;
    // Soft parity: min-path (gates+core) TTK ratio between the two bosses.
    const double ColossalMinPathParityMaxRatio = 1.35;
    // Broodmother spawn: 3 sacs × interval 480t (8s) → concurrent peak over fight.
    const int ColossalSpawnFightSeconds = 120;
    const int ColossalMaxEnemiesCap = 128; // BattleSimConfig.MaxEnemies default
    const int ColossalNormalGenSampleSeeds = 48;
    static readonly string[] ColossalBossIds =
    {
        SegmentStageGenerator.LeviathanBossId,
        SegmentStageGenerator.BroodmotherBossId,
    };

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
                $"diff={seg.DifficultyMin}-{seg.DifficultyMax} w={seg.Weight}");

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
        failures += CheckThemeDifficultyCoverage(data);
        Console.WriteLine();
        failures += CheckModifierRewards(data.Rewards);
        Console.WriteLine();
        failures += CheckModifierComboDps();
        Console.WriteLine();
        failures += CheckScoringCurves(data);
        Console.WriteLine();
        failures += CheckBulletDensityStress(data, generator);
        Console.WriteLine();
        failures += CheckEnemyMovementRoster(data);
        Console.WriteLine();
        failures += CheckObstacleLayouts(data, generator);
        Console.WriteLine();
        failures += CheckShipPrimaryDpsBalance(data);
        Console.WriteLine();
        failures += CheckSegmentWeights(data);
        Console.WriteLine();
        failures += CheckEncounterBalance(data);
        Console.WriteLine();
        failures += CheckCapsuleDropAfterMagnet(data);
        Console.WriteLine();
        failures += CheckBossRedesign(data);
        Console.WriteLine();
        failures += CheckWeaponExpansion(data);
        Console.WriteLine();
        failures += CheckColossalBosses(data, generator);

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

    /// <summary>
    /// REQ-026: theme shuffle (REQ-025) can place any theme at stages 2–5, so every
    /// theme × difficulty 1–5 must assemble. Stage 1 is always themes[0]; require a
    /// larger stage-1 candidate pool and ≥5 themed segments per theme. Difficulty
    /// monotonicity is measured on stage-index ordinal pools (theme=stage, diff=stage).
    /// </summary>
    static int CheckThemeDifficultyCoverage(GameDataSet data)
    {
        int failures = 0;
        StageGenerationCatalog catalog = data.StageGeneration;
        BattleContent content = data.BattleContent;

        Console.WriteLine(
            "REQ-026 theme×difficulty coverage (shuffle-ready, provisional §7):");

        if (catalog.ThemeIds.Count == 0)
        {
            Console.WriteLine("FAIL coverage: catalog has no themes.");
            return 1;
        }

        string stage1Theme = catalog.ThemeIds[0];
        int stage1Candidates = 0;
        var themeTagged = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < catalog.ThemeIds.Count; i++)
            themeTagged[catalog.ThemeIds[i]] = 0;

        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            if (seg.DifficultyMin <= 1
                && (seg.ThemeId == null
                    || string.Equals(seg.ThemeId, stage1Theme, StringComparison.Ordinal)))
                stage1Candidates++;

            if (seg.ThemeId != null && themeTagged.ContainsKey(seg.ThemeId))
                themeTagged[seg.ThemeId]++;
        }

        Console.WriteLine(
            $"  stage1 theme='{stage1Theme}' candidates={stage1Candidates} " +
            $"(need ≥{MinStage1CandidateSegments})");
        if (stage1Candidates < MinStage1CandidateSegments)
        {
            Console.WriteLine(
                $"FAIL coverage: stage-1 candidates {stage1Candidates} " +
                $"< {MinStage1CandidateSegments}.");
            failures++;
        }

        foreach (string theme in catalog.ThemeIds)
        {
            int n = themeTagged[theme];
            Console.WriteLine(
                $"  themed segs theme={theme,-10} n={n} " +
                $"(need ≥{MinThemeTaggedSegments})");
            if (n < MinThemeTaggedSegments)
            {
                Console.WriteLine(
                    $"FAIL coverage: theme '{theme}' has {n} tagged segments " +
                    $"< {MinThemeTaggedSegments}.");
                failures++;
            }

            // Difficulty band coverage for themed segments (null-theme fillers allowed,
            // but theme-tagged pool must span 2–5; stage1 theme also needs d1).
            for (int diff = 1; diff <= 5; diff++)
            {
                if (diff == 1
                    && !string.Equals(theme, stage1Theme, StringComparison.Ordinal))
                    continue;

                int matching = 0;
                foreach (StageSegmentTemplate seg in catalog.Segments)
                {
                    if (!seg.SupportsDifficulty(diff) || !seg.SupportsTheme(theme))
                        continue;
                    matching++;
                }

                if (matching < catalog.SegmentsPerStage)
                {
                    Console.WriteLine(
                        $"FAIL coverage: theme={theme} diff={diff} has {matching} " +
                        $"eligible segments < segmentsPerStage={catalog.SegmentsPerStage}.");
                    failures++;
                }
            }
        }

        // Boss stage/diff ranges must accept any stage index 1..5 for every theme.
        foreach (string theme in catalog.ThemeIds)
        {
            for (int stage = 1; stage <= 5; stage++)
            {
                for (int diff = 1; diff <= 5; diff++)
                {
                    if (FindBossForStage(catalog, stage, diff, theme) == null)
                    {
                        Console.WriteLine(
                            $"FAIL coverage: no boss for theme={theme} " +
                            $"stage={stage} diff={diff}.");
                        failures++;
                    }
                }
            }
        }

        // Full assembly: force theme as themes[0], stageIndex=1, each difficulty.
        const ulong baseSeed = 0x7E4E26UL;
        int assemblyFails = 0;
        int assemblyOk = 0;
        foreach (string theme in catalog.ThemeIds)
        {
            StageGenerationCatalog forced = CatalogWithPrimaryTheme(catalog, theme);
            var gen = new SegmentStageGenerator(forced);
            for (int difficulty = 1; difficulty <= 5; difficulty++)
            {
                for (int s = 0; s < ThemeDiffAssemblySeedCount; s++)
                {
                    ulong seed = baseSeed + (ulong)(s * 9973) + (ulong)difficulty * 131UL;
                    try
                    {
                        StagePlan plan = gen.Generate(seed, stageIndex: 1, difficulty);
                        if (!string.Equals(plan.ThemeId, theme, StringComparison.Ordinal))
                        {
                            Console.WriteLine(
                                $"FAIL assembly: forced theme={theme} diff={difficulty} " +
                                $"seed={seed:X} got theme={plan.ThemeId}.");
                            assemblyFails++;
                            continue;
                        }
                        if (!StagePlanClearability.IsClearable(plan))
                        {
                            Console.WriteLine(
                                $"FAIL assembly: theme={theme} diff={difficulty} " +
                                $"seed={seed:X} plan not clearable.");
                            assemblyFails++;
                            continue;
                        }
                        assemblyOk++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FAIL assembly: theme={theme} diff={difficulty} " +
                            $"seed={seed:X}: {ex.Message}");
                        assemblyFails++;
                    }
                }
            }
        }

        failures += assemblyFails;
        Console.WriteLine(
            $"  forced theme×diff assemblies: ok={assemblyOk} fail={assemblyFails} " +
            $"(themes={catalog.ThemeIds.Count} × diff 1–5 × seeds {ThemeDiffAssemblySeedCount})");

        // Stage-index HP monotonicity (ordinal theme, difficulty=stage).
        double prevAvg = -1.0;
        Console.WriteLine("  stage-index pool avgHP (theme=ordinal, diff=stage):");
        for (int stage = 1; stage <= 5; stage++)
        {
            string theme = catalog.ThemeIds[(stage - 1) % catalog.ThemeIds.Count];
            int difficulty = stage;
            int poolHpSum = 0;
            int poolCount = 0;
            foreach (StageSegmentTemplate seg in catalog.Segments)
            {
                if (!seg.SupportsDifficulty(difficulty) || !seg.SupportsTheme(theme))
                    continue;
                poolHpSum += SegmentSpawnHp(seg, content);
                poolCount++;
            }

            if (poolCount == 0)
            {
                Console.WriteLine(
                    $"FAIL mono: stage={stage} theme={theme} empty segment pool.");
                failures++;
                continue;
            }

            double avg = poolHpSum / (double)poolCount;
            Console.WriteLine(
                $"    stage={stage} theme={theme,-10} n={poolCount} avgHP={avg:F1}");
            if (prevAvg >= 0.0 && avg + 0.001 < prevAvg)
            {
                Console.WriteLine(
                    $"FAIL mono: stage {stage} avgHP {avg:F1} < stage {stage - 1} {prevAvg:F1}.");
                failures++;
            }
            prevAvg = avg;
        }

        if (failures == 0)
            Console.WriteLine("PASS: REQ-026 theme×difficulty coverage + stage HP mono.");
        else
            Console.WriteLine($"FAIL: REQ-026 coverage checks ({failures} failure(s)).");
        return failures;
    }

    static StageGenerationCatalog CatalogWithPrimaryTheme(
        StageGenerationCatalog source,
        string primary)
    {
        var themes = new List<string>(source.ThemeIds.Count) { primary };
        for (int i = 0; i < source.ThemeIds.Count; i++)
        {
            string t = source.ThemeIds[i];
            if (!string.Equals(t, primary, StringComparison.Ordinal))
                themes.Add(t);
        }

        return new StageGenerationCatalog(
            source.LaneCount,
            source.SegmentsPerStage,
            source.StartLaneMask,
            source.Segments,
            source.Bosses,
            themes);
    }

    static int SegmentSpawnHp(StageSegmentTemplate seg, BattleContent content)
    {
        int sum = 0;
        for (int i = 0; i < seg.Spawns.Count; i++)
        {
            EnemyDefinition enemy = content.FindEnemy(seg.Spawns[i].EnemyId);
            if (enemy != null)
                sum += enemy.MaxHp;
        }
        return sum;
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
                $"FAIL density: boss '{boss.BossId}' needs ≥2 phases for density stress.");
            return 1;
        }

        // REQ-033: densest packing is usually mid spread (not final rapid).
        // Pick the phase with max concurrent estimate for pool stress.
        BossPhase densestPhase = boss.Phases[0];
        int densestIdx = 0;
        double densestConcurrent = -1;
        for (int pi = 0; pi < boss.Phases.Count; pi++)
        {
            BossPhase cand = boss.Phases[pi];
            double spd = cand.BulletSpeedNumerator / (double)cand.BulletSpeedDenominator;
            // travel filled below; use ways/interval * 1/speed as density proxy pre-travel.
            double proxy = cand.Ways / (double)cand.FireIntervalTicks / Math.Max(1e-9, spd);
            if (proxy > densestConcurrent)
            {
                densestConcurrent = proxy;
                densestPhase = cand;
                densestIdx = pi;
            }
        }

        BossPhase phase2 = densestPhase;
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
            $"  Boss '{boss.BossId}' densest phase p{densestIdx}: " +
            $"interval={phase2.FireIntervalTicks}t " +
            $"ways={phase2.Ways} speed={phase2.BulletSpeedNumerator}/" +
            $"{phase2.BulletSpeedDenominator} su/tick " +
            $"travel≈{travelSubUnits / (double)SimSpace.SubUnitsPerWorldUnit:F1}u " +
            $"life≈{bossBulletLife}t");
        Console.WriteLine(
            $"  Regular enemy bullet life (Core default speed 8u/s, same travel)≈{enemyBulletLife}t");
        Console.WriteLine(
            $"  Enemy n-way: faithful={enemyWaysFaithful} (Core aimed single); " +
            $"stress maxWays={maxWays} (apply densest-phase ways to every concurrent shooter)");

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
    /// Empty short stage + boss only. Fire until densest phase (index ≥1 with
    /// REQ-033 3-phase spread mid), then stop firing so n-way packing can settle
    /// without defeating the boss.
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
            // Wait until boss has finished entry (at holdX) so densest-phase volleys exist.
            // Fire until phase ≥1 (spread mid with 3 phases), then hold so we do not melt.
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

            // Phase index 1 = first transition (spread) under equal-split 3-phase data.
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

    /// <summary>
    /// REQ-021/055: schema v3 roster must include dive/zigzag/dash on 8–12 of 31 enemies.
    /// </summary>
    static int CheckEnemyMovementRoster(GameDataSet data)
    {
        int failures = 0;
        var counts = new Dictionary<EnemyMovePattern, int>();
        int newPatternCount = 0;
        Console.WriteLine("Enemy movement roster (enemies.json schema v3, provisional §7):");

        foreach (EnemyDefinition enemy in data.BattleContent.Enemies)
        {
            if (!counts.ContainsKey(enemy.MovePattern))
                counts[enemy.MovePattern] = 0;
            counts[enemy.MovePattern]++;
            if (enemy.MovePattern == EnemyMovePattern.Dive
                || enemy.MovePattern == EnemyMovePattern.Zigzag
                || enemy.MovePattern == EnemyMovePattern.Dash)
            {
                newPatternCount++;
                Console.WriteLine(
                    $"  {enemy.Id,-22} {enemy.MovePattern,-8} " +
                    $"speed={enemy.MoveSpeedNumerator}/{enemy.MoveSpeedDenominator} " +
                    $"delay={enemy.MovementDelayTicks} dur={enemy.MovementDurationTicks} " +
                    $"pause={enemy.MovementPauseTicks} amp={enemy.MovementAmplitudeNumerator}/" +
                    $"{enemy.MovementAmplitudeDenominator} period={enemy.MovementPeriodTicks}");
            }
        }

        Console.WriteLine(
            "  totals: " + string.Join(
                ", ",
                counts.OrderBy(kv => kv.Key.ToString())
                    .Select(kv => $"{kv.Key}={kv.Value}")));
        Console.WriteLine(
            $"  new patterns (dive|zigzag|dash) = {newPatternCount} / {data.BattleContent.Enemies.Count}");

        // REQ-055 adds hive_tentacle (static wall tentacle) → 31 catalog enemies.
        if (data.BattleContent.Enemies.Count != 31)
        {
            Console.WriteLine(
                $"FAIL movement: expected 31 enemies, got {data.BattleContent.Enemies.Count}.");
            failures++;
        }

        if (newPatternCount < 8 || newPatternCount > 12)
        {
            Console.WriteLine(
                $"FAIL movement: dive/zigzag/dash count {newPatternCount} outside band [8,12].");
            failures++;
        }

        if (!counts.ContainsKey(EnemyMovePattern.Dive) || counts[EnemyMovePattern.Dive] < 1
            || !counts.ContainsKey(EnemyMovePattern.Zigzag) || counts[EnemyMovePattern.Zigzag] < 1
            || !counts.ContainsKey(EnemyMovePattern.Dash) || counts[EnemyMovePattern.Dash] < 1)
        {
            Console.WriteLine("FAIL movement: each of dive/zigzag/dash must appear at least once.");
            failures++;
        }

        if (failures == 0)
            Console.WriteLine("PASS: enemy movement roster band checks.");
        return failures;
    }

    /// <summary>
    /// REQ-023 + REQ-055: obstacle density, solid corridor gaps, scrapyard debris.
    /// Shared intro (theme-null) stage-1 segments stay empty. Themed scrapyard may place
    /// breakable debris on difficultyMin≤1 so stage 1 teaches cover/clear (REQ-055).
    /// Solids remain banned on stage-1-capable rows (unfair walls in the tutorial band).
    /// Laser emitters (enum value 2 on sim Core) use HP 0 and count toward MaxObstacles.
    /// </summary>
    static int CheckObstacleLayouts(GameDataSet data, SegmentStageGenerator generator)
    {
        int failures = 0;
        BattleSimConfig defaults = BattleSimConfig.CreateDefault();
        int halfH = defaults.ObstacleHalfHeight;
        int maxObstacles = defaults.MaxObstacles;
        var catalog = data.StageGeneration;
        // LaserEmitter = 2 when sim REQ-055 Core is present; absent on older content Core.
        const int LaserEmitterTypeValue = 2;
        const int MaxStage1Breakables = 8;

        Console.WriteLine(
            "Obstacle layouts (waves.json segments, provisional §7 + REQ-055):");
        Console.WriteLine(
            $"  config halfH={halfH}su ({halfH / (double)SimSpace.SubUnitsPerWorldUnit:F2}u) " +
            $"MaxObstacles={maxObstacles} minCorridorGap={MinSolidCorridorGapSubUnits}su");

        int stage1WithObstacles = 0;
        int maxPerSegment = 0;
        int totalWithObstacles = 0;

        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            int count = seg.Obstacles.Count;
            if (count > maxPerSegment)
                maxPerSegment = count;
            if (count > 0)
                totalWithObstacles++;

            bool stage1Capable = seg.DifficultyMin <= 1;
            int solids = 0;
            int breakables = 0;
            int lasers = 0;
            foreach (ObstacleSpawn o in seg.Obstacles)
            {
                if (o.Type == ObstacleType.Solid)
                {
                    solids++;
                    if (o.Hp != 0)
                    {
                        Console.WriteLine(
                            $"FAIL obstacles: solid in '{seg.SegmentId}' has hp={o.Hp} (must 0).");
                        failures++;
                    }
                }
                else if ((int)o.Type == LaserEmitterTypeValue)
                {
                    lasers++;
                    if (o.Hp != 0)
                    {
                        Console.WriteLine(
                            $"FAIL obstacles: laserEmitter in '{seg.SegmentId}' has hp={o.Hp} (must 0).");
                        failures++;
                    }
                }
                else
                {
                    breakables++;
                    if (o.Hp < 1)
                    {
                        Console.WriteLine(
                            $"FAIL obstacles: breakable in '{seg.SegmentId}' has hp={o.Hp}.");
                        failures++;
                    }
                }
            }

            if (stage1Capable && count > 0)
            {
                stage1WithObstacles++;
                // Shared intro pool must stay empty; scrapyard debris is intentional.
                if (seg.ThemeId == null)
                {
                    Console.WriteLine(
                        $"FAIL obstacles: shared stage-1 segment '{seg.SegmentId}' " +
                        $"has {count} obstacles (must be empty).");
                    failures++;
                }
                else if (solids > 0 || lasers > 0)
                {
                    Console.WriteLine(
                        $"FAIL obstacles: stage-1-capable '{seg.SegmentId}' may only use " +
                        $"breakable debris (solids={solids} lasers={lasers}).");
                    failures++;
                }
                else if (breakables > MaxStage1Breakables)
                {
                    Console.WriteLine(
                        $"FAIL obstacles: stage-1-capable '{seg.SegmentId}' has " +
                        $"{breakables} breakables > {MaxStage1Breakables}.");
                    failures++;
                }
            }

            // Corridor: group solids by X; ensure a vertical gap ≥ min between extents.
            var solidsByX = new Dictionary<int, List<int>>();
            foreach (ObstacleSpawn o in seg.Obstacles)
            {
                if (o.Type != ObstacleType.Solid)
                    continue;
                if (!solidsByX.TryGetValue(o.X, out List<int> ys))
                {
                    ys = new List<int>();
                    solidsByX[o.X] = ys;
                }
                ys.Add(o.Y);
            }

            bool corridorOk = true;
            foreach (KeyValuePair<int, List<int>> group in solidsByX)
            {
                List<int> ys = group.Value;
                ys.Sort();
                // Full playfield open edges count as infinite free space outside blocks.
                int playMin = -SimSpace.PlayfieldHalfHeightSubUnits;
                int playMax = SimSpace.PlayfieldHalfHeightSubUnits;
                // Build blocked intervals [y-halfH, y+halfH] and find largest free gap.
                var intervals = new List<(int lo, int hi)>();
                foreach (int y in ys)
                    intervals.Add((y - halfH, y + halfH));
                intervals.Sort((a, b) => a.lo.CompareTo(b.lo));

                int cursor = playMin;
                int bestGap = 0;
                foreach (var (lo, hi) in intervals)
                {
                    if (lo > cursor)
                        bestGap = Math.Max(bestGap, lo - cursor);
                    cursor = Math.Max(cursor, hi);
                }
                if (playMax > cursor)
                    bestGap = Math.Max(bestGap, playMax - cursor);

                if (bestGap < MinSolidCorridorGapSubUnits)
                {
                    Console.WriteLine(
                        $"FAIL obstacles: '{seg.SegmentId}' solid column x={group.Key} " +
                        $"bestGap={bestGap}su < {MinSolidCorridorGapSubUnits}su.");
                    failures++;
                    corridorOk = false;
                }
            }

            string theme = NullLabel(seg.ThemeId);
            Console.WriteLine(
                $"  {seg.SegmentId,-36} theme={theme,-10} " +
                $"n={count,2} solid={solids} break={breakables} laser={lasers} " +
                $"stage1={(stage1Capable ? "Y" : "n")} " +
                $"corridor={(count == 0 || corridorOk ? "ok" : "FAIL")}");
        }

        // Progressive density by theme ordinal (stage 2 hive → 5 core).
        int[] themeMax = new int[catalog.ThemeIds.Count];
        for (int i = 0; i < catalog.ThemeIds.Count; i++)
            themeMax[i] = 0;
        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            if (seg.ThemeId == null)
                continue;
            for (int i = 0; i < catalog.ThemeIds.Count; i++)
            {
                if (string.Equals(catalog.ThemeIds[i], seg.ThemeId, StringComparison.Ordinal))
                {
                    if (seg.Obstacles.Count > themeMax[i])
                        themeMax[i] = seg.Obstacles.Count;
                    break;
                }
            }
        }

        Console.WriteLine("  theme max obstacles/segment:");
        for (int i = 0; i < catalog.ThemeIds.Count; i++)
        {
            Console.WriteLine($"    stage~{i + 1} {catalog.ThemeIds[i],-10} max={themeMax[i]}");
        }

        // scrapyard has no themed segs (null pool); hive early ≤4, core late ≥5.
        int hiveIdx = IndexOfTheme(catalog, "hive");
        int coreIdx = IndexOfTheme(catalog, "core");
        if (hiveIdx >= 0 && themeMax[hiveIdx] > 0 && themeMax[hiveIdx] > 5)
        {
            Console.WriteLine(
                $"FAIL obstacles: hive max {themeMax[hiveIdx]} > 5 (early band 2–4 intended).");
            failures++;
        }
        if (coreIdx >= 0 && themeMax[coreIdx] < 5)
        {
            Console.WriteLine(
                $"FAIL obstacles: core max {themeMax[coreIdx]} < 5 (late band 5–7 intended).");
            failures++;
        }
        if (coreIdx >= 0 && hiveIdx >= 0
            && themeMax[coreIdx] > 0 && themeMax[hiveIdx] > 0
            && themeMax[coreIdx] < themeMax[hiveIdx])
        {
            Console.WriteLine(
                $"FAIL obstacles: core max {themeMax[coreIdx]} < hive max {themeMax[hiveIdx]} " +
                "(density should increase by theme).");
            failures++;
        }

        if (maxPerSegment > maxObstacles)
        {
            Console.WriteLine(
                $"FAIL obstacles: max per segment {maxPerSegment} > MaxObstacles {maxObstacles}.");
            failures++;
        }

        // Generated plans: stage 1 must never spawn obstacles; stage 5 should.
        const ulong seed = 0x0B57AC1EUL;
        for (int stage = 1; stage <= 5; stage++)
        {
            StagePlan plan = generator.Generate(seed, stage, Math.Min(stage, 5));
            int planObstacles = 0;
            for (int s = 0; s < plan.Segments.Count; s++)
                planObstacles += plan.Segments[s].Obstacles.Count;

            Console.WriteLine(
                $"  plan stage={stage} theme={plan.ThemeId} obstacles={planObstacles} " +
                $"segs=[{string.Join(",", SegmentIds(plan))}]");

            // REQ-055: stage 1 scrapyard may include breakable debris.
            // Soft bound only — hard zero is no longer required.
            if (stage == 1 && planObstacles > MaxStage1Breakables * 3)
            {
                Console.WriteLine(
                    $"FAIL obstacles: stage 1 plan has {planObstacles} obstacles " +
                    $"(>{MaxStage1Breakables * 3} across 3 segments).");
                failures++;
            }
            else if (stage == 1 && planObstacles > 0)
            {
                Console.WriteLine(
                    $"  note: stage 1 plan obstacles={planObstacles} " +
                    "(REQ-055 scrapyard debris — expected).");
            }
            if (stage >= 4 && planObstacles < 1)
            {
                Console.WriteLine(
                    $"WARN obstacles: stage {stage} plan has 0 obstacles " +
                    "(late themes should usually include themed segs; seed-dependent).");
            }
        }

        if (stage1WithObstacles == 0 && totalWithObstacles == 0)
        {
            Console.WriteLine("FAIL obstacles: no segment has obstacles (content missing).");
            failures++;
        }

        if (failures == 0)
            Console.WriteLine("PASS: obstacle layout / corridor / stage-1 empty checks.");
        return failures;
    }

    static int IndexOfTheme(StageGenerationCatalog catalog, string themeId)
    {
        for (int i = 0; i < catalog.ThemeIds.Count; i++)
            if (string.Equals(catalog.ThemeIds[i], themeId, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>
    /// REQ-022: three ship primaries (vulcan/laser/spread) single-target DPS balance.
    /// Level-0 main shot only; missiles/options off. Soft FAIL if max/min &gt; band.
    /// </summary>
    static int CheckShipPrimaryDpsBalance(GameDataSet data)
    {
        int failures = 0;
        Console.WriteLine(
            "Ship primary DPS balance (single target, level 0, provisional §7):");

        if (data.Ships.Count < 3)
        {
            Console.WriteLine($"FAIL ships: expected ≥3 ships, got {data.Ships.Count}.");
            return 1;
        }

        ShipDefinition starter = data.FindShip("starter");
        ShipDefinition interceptor = data.FindShip("interceptor");
        ShipDefinition bulwark = data.FindShip("bulwark");
        if (starter == null || interceptor == null || bulwark == null)
        {
            Console.WriteLine("FAIL ships: missing starter/interceptor/bulwark.");
            return 1;
        }

        // Concept fields
        if (starter.WeaponType != WeaponType.Vulcan || starter.MaxHp != 3)
        {
            Console.WriteLine(
                $"FAIL ships: starter expected vulcan/HP3 got {starter.WeaponType}/{starter.MaxHp}.");
            failures++;
        }
        if (interceptor.WeaponType != WeaponType.Laser || interceptor.MaxHp != 2)
        {
            Console.WriteLine(
                $"FAIL ships: interceptor expected laser/HP2 got {interceptor.WeaponType}/{interceptor.MaxHp}.");
            failures++;
        }
        if (bulwark.WeaponType != WeaponType.Spread || bulwark.MaxHp != 5)
        {
            Console.WriteLine(
                $"FAIL ships: bulwark expected spread/HP5 got {bulwark.WeaponType}/{bulwark.MaxHp}.");
            failures++;
        }

        var results = new List<(string id, WeaponType weapon, int dmg, double dps)>();
        foreach (ShipDefinition ship in new[] { starter, interceptor, bulwark })
        {
            try
            {
                int damage = SimulateShipSingleTargetDamage(data, ship, ShipDpsSimTicks);
                double dps = damage * (double)SimSpace.TicksPerSecond / ShipDpsSimTicks;
                results.Add((ship.Id, ship.WeaponType, damage, dps));
                Console.WriteLine(
                    $"  {ship.Id,-14} weapon={ship.WeaponType,-7} maxHp={ship.MaxHp} " +
                    $"move={ship.MoveSpeedMultiplierNumerator}/{ship.MoveSpeedMultiplierDenominator} " +
                    $"dmg@{ShipDpsSimTicks}t={damage} dps≈{dps:F1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL ships: {ship.Id} sim threw: {ex.Message}");
                failures++;
            }
        }

        if (results.Count == 3)
        {
            double min = results.Min(r => r.dps);
            double max = results.Max(r => r.dps);
            double ratio = min <= 0 ? double.PositiveInfinity : max / min;
            Console.WriteLine(
                $"  single-target DPS ratio max/min = {ratio:F2} " +
                $"(band ≤{MaxShipSingleTargetDpsRatio:F2})");

            if (ratio > MaxShipSingleTargetDpsRatio)
            {
                Console.WriteLine(
                    $"FAIL ships: DPS ratio {ratio:F2} > {MaxShipSingleTargetDpsRatio:F2} " +
                    "(one primary dominates single-target).");
                failures++;
            }

            // Soft role checks: laser should not be far below vulcan; spread may trail
            // single-target but must deal damage.
            double vulcan = results.First(r => r.weapon == WeaponType.Vulcan).dps;
            double laser = results.First(r => r.weapon == WeaponType.Laser).dps;
            double spread = results.First(r => r.weapon == WeaponType.Spread).dps;
            if (laser < vulcan * 0.5)
            {
                Console.WriteLine(
                    $"FAIL ships: laser dps {laser:F1} < 50% of vulcan {vulcan:F1}.");
                failures++;
            }
            if (spread <= 0)
            {
                Console.WriteLine("FAIL ships: spread dealt no damage.");
                failures++;
            }
            else if (spread > vulcan * 1.5)
            {
                Console.WriteLine(
                    $"WARN ships: spread single-target dps {spread:F1} > 1.5× vulcan " +
                    $"{vulcan:F1} (coverage weapon unexpectedly strong on 1 target, §7).");
            }
        }

        if (failures == 0)
            Console.WriteLine("PASS: ship primary concept + single-target DPS band.");
        return failures;
    }

    static int SimulateShipSingleTargetDamage(
        GameDataSet data,
        ShipDefinition ship,
        int ticks)
    {
        BattleSimConfig config = data.CreateBattleSimConfig();
        if (ship.MaxHp.HasValue)
            config.PlayerMaxHp = ship.MaxHp.Value;
        ApplyShipWeaponProfile(config, ship);
        // Lab: no scroll, fixed aim, huge HP sponge on the shot line.
        config.ScrollSpeedNumerator = 0;
        config.ScrollSpeedDenominator = 1;
        config.PlayerSpawnX = 0;
        config.PlayerSpawnY = 0;
        config.PlayerMinX = -10000;
        config.PlayerMaxX = 10000;
        config.PlayerMinY = -10000;
        config.PlayerMaxY = 10000;
        config.PlayerHalfWidth = 0;
        config.PlayerHalfHeight = 0;
        config.CapsuleNoDropWeight = 1_000_000;
        config.MaxBullets = 128;
        config.UseConfiguredMainShotStats = true;

        const int spongeHp = 1_000_000;
        var enemy = new EnemyDefinition(
            "dps_sponge",
            "dps_sponge",
            spongeHp,
            0,
            0,
            EnemyMovePattern.Static,
            0,
            1,
            0,
            SimSpace.SubUnitsPerWorldUnit / 2,
            SimSpace.SubUnitsPerWorldUnit / 2,
            0,
            0,
            1,
            1);
        // Place sponge slightly in front of player so forward shots land.
        int spongeX = 3 * SimSpace.SubUnitsPerWorldUnit;
        var weapon = data.BattleContent.PlayerWeapon;
        var content = new BattleContent(
            new[] { enemy },
            data.BattleContent.Weapons.ToArray(),
            weapon.Id);
        var segment = new StageSegment(
            "dps_lab",
            ticks + 10,
            new[] { new SpawnEvent(0, enemy.Id, spongeX, 0) },
            1,
            1,
            new[] { 1 });
        var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        gauge.ImportLevels(new[] { 0, 0, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xD15CUL),
            plan,
            content,
            gauge,
            BattleModifier.None);

        InputCommand fire = new InputCommand(0, 0, true);
        for (int t = 0; t < ticks; t++)
            sim.Step(in fire);

        if (sim.Enemies.Count == 0)
            return spongeHp;
        return spongeHp - sim.Enemies[0].Hp;
    }

    /// <summary>Mirrors RunManager.ApplyShipWeaponProfile for lab sims.</summary>
    static void ApplyShipWeaponProfile(BattleSimConfig config, ShipDefinition ship)
    {
        config.PlayerWeaponType = ship.WeaponType;
        switch (ship.WeaponType)
        {
            case WeaponType.Vulcan:
                return;
            case WeaponType.Laser:
                config.MainShotBaseDamage = config.LaserBaseDamage;
                config.FireIntervalTicks = config.LaserFireIntervalTicks;
                config.MainShotRapidFireStartLevel = config.LaserRapidFireStartLevel;
                config.MainShotFireIntervalReductionPerLevel =
                    config.LaserFireIntervalReductionPerLevel;
                config.MainShotMinimumFireIntervalTicks =
                    config.LaserMinimumFireIntervalTicks;
                config.PlayerBulletSpeedNumerator = config.LaserSpeedNumerator;
                config.PlayerBulletSpeedDenominator = config.LaserSpeedDenominator;
                config.MainShotHalfWidth = config.LaserHalfWidth;
                config.MainShotHalfHeight = config.LaserHalfHeight;
                return;
            case WeaponType.Spread:
                config.MainShotBaseDamage = config.SpreadBaseDamage;
                config.FireIntervalTicks = config.SpreadFireIntervalTicks;
                config.MainShotRapidFireStartLevel = config.SpreadRapidFireStartLevel;
                config.MainShotFireIntervalReductionPerLevel =
                    config.SpreadFireIntervalReductionPerLevel;
                config.MainShotMinimumFireIntervalTicks =
                    config.SpreadMinimumFireIntervalTicks;
                config.PlayerBulletSpeedNumerator = config.SpreadSpeedNumerator;
                config.PlayerBulletSpeedDenominator = config.SpreadSpeedDenominator;
                config.MainShotHalfWidth = config.SpreadHalfWidth;
                config.MainShotHalfHeight = config.SpreadHalfHeight;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(ship));
        }
    }

    /// <summary>
    /// REQ-034: missile families + option formations catalog, ST DPS alignment,
    /// situation roles, pierce_shot non-stack on lance, bomb×kill_explosion gate,
    /// and homing layered on each family (provisional §7).
    /// </summary>
    static int CheckWeaponExpansion(GameDataSet data)
    {
        int failures = 0;
        Console.WriteLine(
            "Weapon expansion REQ-034 (missile families + option formations, §7):");

        IReadOnlyList<MissileFamilyDefinition> families =
            data.BattleContent.MissileFamilies;
        IReadOnlyList<OptionFormationDefinition> formations =
            data.BattleContent.OptionFormations;

        if (families.Count != 3)
        {
            Console.WriteLine(
                $"FAIL weapons v3: expected 3 missileFamilies, got {families.Count}.");
            return 1;
        }
        if (formations.Count != 3)
        {
            Console.WriteLine(
                $"FAIL weapons v3: expected 3 optionFormations, got {formations.Count}.");
            return 1;
        }
        if (data.BattleContent.DefaultMissileFamily != MissileFamily.Straight
            || data.BattleContent.DefaultOptionFormation != OptionFormation.Trail)
        {
            Console.WriteLine(
                $"FAIL weapons v3: defaults must be straight/trail " +
                $"(got {data.BattleContent.DefaultMissileFamily}/" +
                $"{data.BattleContent.DefaultOptionFormation}).");
            failures++;
        }

        MissileFamilyDefinition straight =
            data.BattleContent.FindMissileFamily(MissileFamily.Straight);
        MissileFamilyDefinition bomb =
            data.BattleContent.FindMissileFamily(MissileFamily.SpreadBomb);
        MissileFamilyDefinition lance =
            data.BattleContent.FindMissileFamily(MissileFamily.PiercingLance);
        if (straight == null || bomb == null || lance == null)
        {
            Console.WriteLine("FAIL weapons v3: missing straight/spread_bomb/piercing_lance.");
            return failures + 1;
        }

        // Catalog numeric anchors (playtest retune: longer missile intervals).
        if (straight.BaseDamage != 20
            || straight.FireIntervalTicks != 42
            || straight.MinimumFireIntervalTicks != 20
            || straight.FireIntervalReductionPerLevel != 5
            || straight.PierceEnemyCount != 0
            || straight.ExplosionDamage != 0)
        {
            Console.WriteLine("FAIL weapons v3: straight family numbers diverge from design.");
            failures++;
        }
        if (bomb.BaseDamage != 12
            || bomb.ExplosionDamage != 16
            || bomb.FireIntervalTicks != 54
            || bomb.MinimumFireIntervalTicks != 36
            || bomb.FireIntervalReductionPerLevel != 5
            || bomb.ExplosionMaxTargets != 5
            || bomb.ExplosionRadiusSubUnits
                != (int)(1.75m * SimSpace.SubUnitsPerWorldUnit))
        {
            Console.WriteLine(
                $"FAIL weapons v3: spread_bomb numbers diverge " +
                $"(radiusSu={bomb.ExplosionRadiusSubUnits}).");
            failures++;
        }
        if (lance.BaseDamage != 40
            || lance.FireIntervalTicks != 70
            || lance.MinimumFireIntervalTicks != 44
            || lance.FireIntervalReductionPerLevel != 6
            || lance.PierceEnemyCount != 2
            || lance.ExplosionDamage != 0)
        {
            Console.WriteLine("FAIL weapons v3: piercing_lance numbers diverge from design.");
            failures++;
        }

        OptionFormationDefinition trail =
            data.BattleContent.FindOptionFormation(OptionFormation.Trail);
        OptionFormationDefinition fixedForm =
            data.BattleContent.FindOptionFormation(OptionFormation.Fixed);
        OptionFormationDefinition orbit =
            data.BattleContent.FindOptionFormation(OptionFormation.Orbit);
        if (trail == null || fixedForm == null || orbit == null)
        {
            Console.WriteLine("FAIL weapons v3: missing trail/fixed/orbit formations.");
            return failures + 1;
        }
        if (trail.FollowDelayTicks != 12)
        {
            Console.WriteLine(
                $"FAIL weapons v3: trail followDelayTicks={trail.FollowDelayTicks} expected 12.");
            failures++;
        }
        if (fixedForm.OffsetXs.Count != 4 || fixedForm.OffsetYs.Count != 4)
        {
            Console.WriteLine("FAIL weapons v3: fixed formation needs 4 offsets.");
            failures++;
        }
        else
        {
            int[] expectedX =
            {
                (int)(0.75m * SimSpace.SubUnitsPerWorldUnit),
                (int)(0.75m * SimSpace.SubUnitsPerWorldUnit),
                (int)(0.75m * SimSpace.SubUnitsPerWorldUnit),
                (int)(0.75m * SimSpace.SubUnitsPerWorldUnit)
            };
            int[] expectedY =
            {
                (int)(1.5m * SimSpace.SubUnitsPerWorldUnit),
                (int)(-1.5m * SimSpace.SubUnitsPerWorldUnit),
                (int)(2.75m * SimSpace.SubUnitsPerWorldUnit),
                (int)(-2.75m * SimSpace.SubUnitsPerWorldUnit)
            };
            for (int i = 0; i < 4; i++)
            {
                if (fixedForm.OffsetXs[i] != expectedX[i]
                    || fixedForm.OffsetYs[i] != expectedY[i])
                {
                    Console.WriteLine(
                        $"FAIL weapons v3: fixed offset[{i}]=" +
                        $"({fixedForm.OffsetXs[i]},{fixedForm.OffsetYs[i]}) " +
                        $"expected ({expectedX[i]},{expectedY[i]}).");
                    failures++;
                    break;
                }
            }
        }
        if (orbit.OrbitRadiusSubUnits
                != (int)(1.75m * SimSpace.SubUnitsPerWorldUnit)
            || orbit.AngularLutSlotsNumerator != 1
            || orbit.AngularLutSlotsDenominator != 2)
        {
            Console.WriteLine(
                $"FAIL weapons v3: orbit radius/angular " +
                $"{orbit.OrbitRadiusSubUnits}/{orbit.AngularLutSlotsNumerator}/" +
                $"{orbit.AngularLutSlotsDenominator}.");
            failures++;
        }

        Console.WriteLine(
            $"  catalog: families={families.Count} formations={formations.Count} " +
            $"defaults={data.BattleContent.DefaultMissileFamily}/" +
            $"{data.BattleContent.DefaultOptionFormation}");

        // --- Theoretical ST DPS (design formula) ---
        Console.WriteLine("  theoretical ST DPS (direct+explosion when bomb):");
        var theoryL1 = new List<(string id, double dps)>();
        var theoryL3 = new List<(string id, double dps)>();
        foreach (MissileFamilyDefinition fam in new[] { straight, bomb, lance })
        {
            double dps1 = TheoreticalMissileStDps(fam, 1);
            double dps3 = TheoreticalMissileStDps(fam, 3);
            theoryL1.Add((fam.Id, dps1));
            theoryL3.Add((fam.Id, dps3));
            int interval1 = MissileIntervalAtLevel(fam, 1);
            int interval3 = MissileIntervalAtLevel(fam, 3);
            int shot1 = MissileStShotDamage(fam, 1);
            int shot3 = MissileStShotDamage(fam, 3);
            Console.WriteLine(
                $"    {fam.Id,-16} L1 dmg/shot={shot1,3} interval={interval1,2} ST={dps1:F1}  " +
                $"L3 dmg/shot={shot3,3} interval={interval3,2} ST={dps3:F1}");

            if (dps1 < MissileFamilyL1StMin || dps1 > MissileFamilyL1StMax)
            {
                Console.WriteLine(
                    $"FAIL weapons: {fam.Id} L1 ST {dps1:F1} outside " +
                    $"[{MissileFamilyL1StMin},{MissileFamilyL1StMax}].");
                failures++;
            }
            if (dps3 < MissileFamilyL3StMin || dps3 > MissileFamilyL3StMax)
            {
                Console.WriteLine(
                    $"FAIL weapons: {fam.Id} L3 ST {dps3:F1} outside " +
                    $"[{MissileFamilyL3StMin},{MissileFamilyL3StMax}].");
                failures++;
            }
        }

        double theoryL1Ratio = theoryL1.Max(r => r.dps) / theoryL1.Min(r => r.dps);
        double theoryL3Ratio = theoryL3.Max(r => r.dps) / theoryL3.Min(r => r.dps);
        Console.WriteLine(
            $"    ST max/min L1={theoryL1Ratio:F2} L3={theoryL3Ratio:F2} " +
            $"(band ≤{MissileFamilyStMaxMinRatio:F2})");
        if (theoryL1Ratio > MissileFamilyStMaxMinRatio
            || theoryL3Ratio > MissileFamilyStMaxMinRatio)
        {
            Console.WriteLine(
                "FAIL weapons: family ST DPS band too wide (not co-aligned).");
            failures++;
        }

        // --- Simulated ST DPS + homing layer ---
        Console.WriteLine(
            $"  simulated ST DPS ({MissileFamilyStSimTicks}t sponge, missile-only):");
        var simL1 = new Dictionary<MissileFamily, double>();
        foreach (MissileFamilyDefinition fam in new[] { straight, bomb, lance })
        {
            int dmgNone = SimulateMissileSingleTargetDamage(
                data, fam, level: 1, BattleModifier.None, MissileFamilyStSimTicks);
            int dmgHoming = SimulateMissileSingleTargetDamage(
                data, fam, level: 1, BattleModifier.HomingMissile, MissileFamilyStSimTicks);
            double dpsNone = dmgNone * (double)SimSpace.TicksPerSecond / MissileFamilyStSimTicks;
            double dpsHoming =
                dmgHoming * (double)SimSpace.TicksPerSecond / MissileFamilyStSimTicks;
            simL1[fam.Family] = dpsNone;
            double theory = TheoreticalMissileStDps(fam, 1);
            double vsTheory = theory <= 0 ? 0 : dpsNone / theory;
            Console.WriteLine(
                $"    {fam.Id,-16} simST={dpsNone:F1} (×{vsTheory:F2} theory) " +
                $"homingST={dpsHoming:F1}");

            if (dmgNone <= 0)
            {
                Console.WriteLine($"FAIL weapons: {fam.Id} dealt no ST damage.");
                failures++;
            }
            // Homing must not replace family: on a centered sponge ST should stay
            // within a generous band of the plain shot (hit guarantee via large box).
            if (dpsNone > 0)
            {
                double homingRatio = dpsHoming / dpsNone;
                if (homingRatio < 0.85 || homingRatio > 1.15)
                {
                    Console.WriteLine(
                        $"FAIL weapons: {fam.Id}+homing ST ratio {homingRatio:F2} " +
                        "outside [0.85,1.15] on centered sponge " +
                        "(homing should layer steer, not rewrite damage).");
                    failures++;
                }
            }
        }

        // --- Situation roles: dense pack vs column ---
        Console.WriteLine("  situation roles (missile L1 only, clear-time):");
        int denseStraight = SimulateMissilePackClear(
            data, straight, packSize: 5, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit / 2, column: false, BattleModifier.None, 600);
        int denseBomb = SimulateMissilePackClear(
            data, bomb, packSize: 5, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit / 2, column: false, BattleModifier.None, 600);
        int denseLance = SimulateMissilePackClear(
            data, lance, packSize: 5, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit / 2, column: false, BattleModifier.None, 600);
        Console.WriteLine(
            $"    dense HP1×5: straight={denseStraight}t bomb={denseBomb}t lance={denseLance}t");
        if (denseBomb <= 0 || denseStraight <= 0 || denseLance <= 0)
        {
            Console.WriteLine("FAIL weapons: dense pack did not clear for a family.");
            failures++;
        }
        else if (denseBomb > denseStraight)
        {
            Console.WriteLine(
                $"FAIL weapons: bomb dense clear {denseBomb}t should beat " +
                $"straight {denseStraight}t (AoE role).");
            failures++;
        }

        int colStraight = SimulateMissilePackClear(
            data, straight, packSize: 3, enemyHp: 40, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit, column: true, BattleModifier.None, 900);
        int colBomb = SimulateMissilePackClear(
            data, bomb, packSize: 3, enemyHp: 40, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit, column: true, BattleModifier.None, 900);
        int colLance = SimulateMissilePackClear(
            data, lance, packSize: 3, enemyHp: 40, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit, column: true, BattleModifier.None, 900);
        Console.WriteLine(
            $"    column HP40×3: straight={colStraight}t bomb={colBomb}t lance={colLance}t");
        if (colLance <= 0 || colStraight <= 0)
        {
            Console.WriteLine("FAIL weapons: column did not clear for straight/lance.");
            failures++;
        }
        else if (colLance > colStraight)
        {
            Console.WriteLine(
                $"FAIL weapons: lance column clear {colLance}t should beat " +
                $"straight {colStraight}t (pierce role).");
            failures++;
        }

        // --- pierce_shot must not stack onto piercing_lance missiles ---
        int lanceAlone = SimulateMissilePackClear(
            data, lance, packSize: 5, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit, column: true, BattleModifier.None, 600);
        int lancePierce = SimulateMissilePackClear(
            data, lance, packSize: 5, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit, column: true,
            BattleModifier.PierceShot, 600);
        Console.WriteLine(
            $"  lance column×5: alone={lanceAlone}t +pierce_shot={lancePierce}t " +
            "(missile-only; pierce_shot is main-only)");
        if (lanceAlone <= 0 || lancePierce <= 0)
        {
            Console.WriteLine("FAIL weapons: lance pierce gate pack did not clear.");
            failures++;
        }
        else
        {
            double pierceRatio = (double)lanceAlone / lancePierce;
            Console.WriteLine(
                $"    clear-speed ratio alone/pierce_shot = {pierceRatio:F2} " +
                $"(must ≤{LancePierceShotClearRatioMax:F2})");
            if (pierceRatio > LancePierceShotClearRatioMax)
            {
                Console.WriteLine(
                    "FAIL weapons: pierce_shot appears to buff piercing_lance missiles " +
                    "(must remain main-shot only).");
                failures++;
            }
        }

        // --- bomb × kill_explosion: splash kills never reseed (CODEX rule A) ---
        int bombAlone = SimulateMissilePackClear(
            data, bomb, packSize: 12, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit / 2, column: false, BattleModifier.None, 900);
        int bombKillExp = SimulateMissilePackClear(
            data, bomb, packSize: 12, enemyHp: 1, spacingSubUnits:
            SimSpace.SubUnitsPerWorldUnit / 2, column: false,
            BattleModifier.KillExplosion, 900);
        int baselineMain = SimulatePackClear(
            BattleModifier.None, 12, 1, 900).TicksToClear;
        Console.WriteLine(
            $"  bomb×kill_explosion dense HP1×12: bomb={bombAlone}t " +
            $"bomb+kill_exp={bombKillExp}t main-baseline≈{baselineMain}t");
        if (bombAlone <= 0 || bombKillExp <= 0)
        {
            Console.WriteLine("FAIL weapons: bomb kill_exp gate pack did not clear.");
            failures++;
        }
        else
        {
            double bombRatio = (double)bombAlone / bombKillExp;
            Console.WriteLine(
                $"    bomb+kill_exp clear-speed ×{bombRatio:F2} vs bomb alone " +
                $"(hard ≤{BombKillExpClearRatioMax:F2}; splash kills must not reseed)");
            if (bombRatio > BombKillExpClearRatioMax)
            {
                Console.WriteLine(
                    "FAIL weapons: bomb+kill_explosion runaway " +
                    "(explosion kills may be reseeding kill_explosion).");
                failures++;
            }
            if (baselineMain > 0)
            {
                double vsBase = (double)baselineMain / bombKillExp;
                Console.WriteLine(
                    $"    bomb+kill_exp vs main-baseline clear-speed ×{vsBase:F2} " +
                    $"(soft WARN ≥{BombKillExpVsBaselineWarn:F1}×)");
                if (vsBase >= BombKillExpVsBaselineWarn)
                {
                    Console.WriteLine(
                        $"WARN weapons: bomb+kill_exp ≥{BombKillExpVsBaselineWarn:F0}× " +
                        "main baseline (§7 soft).");
                }
            }
        }

        // --- Reward catalog: family/formation switches + weights ---
        failures += CheckWeaponExpansionRewards(data.Rewards);

        if (failures == 0)
            Console.WriteLine("PASS: weapon expansion catalog + DPS / combo gates.");
        return failures;
    }

    static int CheckWeaponExpansionRewards(RewardCatalog rewards)
    {
        int failures = 0;
        if (rewards == null)
        {
            Console.WriteLine("FAIL rewards: catalog null (weapon expansion).");
            return 1;
        }

        var expectedFamily = new Dictionary<string, (MissileFamily fam, int weight, int stageMin)>(
            StringComparer.Ordinal)
        {
            ["missile_family_straight"] = (MissileFamily.Straight, 1, 1),
            ["missile_family_spread_bomb"] = (MissileFamily.SpreadBomb, 2, 1),
            ["missile_family_piercing_lance"] = (MissileFamily.PiercingLance, 2, 2),
        };
        var expectedForm = new Dictionary<string, (OptionFormation form, int weight, int stageMin)>(
            StringComparer.Ordinal)
        {
            ["option_formation_trail"] = (OptionFormation.Trail, 1, 1),
            ["option_formation_fixed"] = (OptionFormation.Fixed, 2, 1),
            ["option_formation_orbit"] = (OptionFormation.Orbit, 2, 2),
        };

        int famFormWeight1 = 0;
        int famFormWeight2 = 0;
        int totalWeight1 = 0;
        int totalWeight2 = 0;
        int foundFamily = 0;
        int foundForm = 0;

        Console.WriteLine("  reward switches (missileFamily / optionFormation):");
        foreach (RewardDefinition def in rewards.All)
        {
            bool stage1 = def.StageIndexMin <= 1 && def.StageIndexMax >= 1;
            bool stage2 = def.StageIndexMin <= 2 && def.StageIndexMax >= 2;
            if (stage1) totalWeight1 += def.Weight;
            if (stage2) totalWeight2 += def.Weight;

            if (def.Type == RewardType.MissileFamily)
            {
                foundFamily++;
                if (stage1) famFormWeight1 += def.Weight;
                if (stage2) famFormWeight2 += def.Weight;
                if (!expectedFamily.TryGetValue(def.Id, out var exp))
                {
                    Console.WriteLine($"FAIL rewards: unexpected missileFamily id '{def.Id}'.");
                    failures++;
                    continue;
                }
                if (def.MissileFamily != exp.fam
                    || def.Weight != exp.weight
                    || def.StageIndexMin != exp.stageMin)
                {
                    Console.WriteLine(
                        $"FAIL rewards: {def.Id} family={def.MissileFamily} " +
                        $"w={def.Weight} stageMin={def.StageIndexMin} " +
                        $"(expected {exp.fam}/{exp.weight}/{exp.stageMin}).");
                    failures++;
                }
                Console.WriteLine(
                    $"    {def.Id,-32} family={def.MissileFamily,-14} " +
                    $"w={def.Weight} stage={def.StageIndexMin}-{def.StageIndexMax}");
                expectedFamily.Remove(def.Id);
            }
            else if (def.Type == RewardType.OptionFormation)
            {
                foundForm++;
                if (stage1) famFormWeight1 += def.Weight;
                if (stage2) famFormWeight2 += def.Weight;
                if (!expectedForm.TryGetValue(def.Id, out var exp))
                {
                    Console.WriteLine(
                        $"FAIL rewards: unexpected optionFormation id '{def.Id}'.");
                    failures++;
                    continue;
                }
                if (def.OptionFormation != exp.form
                    || def.Weight != exp.weight
                    || def.StageIndexMin != exp.stageMin)
                {
                    Console.WriteLine(
                        $"FAIL rewards: {def.Id} form={def.OptionFormation} " +
                        $"w={def.Weight} stageMin={def.StageIndexMin} " +
                        $"(expected {exp.form}/{exp.weight}/{exp.stageMin}).");
                    failures++;
                }
                Console.WriteLine(
                    $"    {def.Id,-32} form={def.OptionFormation,-14} " +
                    $"w={def.Weight} stage={def.StageIndexMin}-{def.StageIndexMax}");
                expectedForm.Remove(def.Id);
            }
        }

        foreach (string missing in expectedFamily.Keys)
        {
            Console.WriteLine($"FAIL rewards: missing missileFamily '{missing}'.");
            failures++;
        }
        foreach (string missing in expectedForm.Keys)
        {
            Console.WriteLine($"FAIL rewards: missing optionFormation '{missing}'.");
            failures++;
        }
        if (foundFamily != 3 || foundForm != 3)
        {
            Console.WriteLine(
                $"FAIL rewards: expected 3 family + 3 formation entries " +
                $"(got {foundFamily}+{foundForm}).");
            failures++;
        }

        double e1 = totalWeight1 == 0 ? 0 : 3.0 * famFormWeight1 / totalWeight1;
        double e2 = totalWeight2 == 0 ? 0 : 3.0 * famFormWeight2 / totalWeight2;
        Console.WriteLine(
            $"    E[family/form in 3-pick] stage1≈{e1:F2} " +
            $"(w={famFormWeight1}/{totalWeight1}) stage2≈{e2:F2} " +
            $"(w={famFormWeight2}/{totalWeight2})");
        // Soft guide from design: stage1 ~0.5, stage2 ~0.8
        if (e1 < 0.25 || e1 > 1.2)
        {
            Console.WriteLine(
                $"WARN rewards: stage1 E[family/form]≈{e1:F2} outside guide [0.25,1.2] (§7).");
        }
        if (e2 < 0.40 || e2 > 1.4)
        {
            Console.WriteLine(
                $"WARN rewards: stage2 E[family/form]≈{e2:F2} outside guide [0.40,1.4] (§7).");
        }

        return failures;
    }

    static int MissileStShotDamage(MissileFamilyDefinition fam, int level)
    {
        int direct = Damage.Compute(fam.BaseDamage, level);
        int boom = fam.ExplosionDamage > 0
            ? Damage.Compute(fam.ExplosionDamage, level)
            : 0;
        return direct + boom;
    }

    static int MissileIntervalAtLevel(MissileFamilyDefinition fam, int level)
    {
        // Mirrors BattleSim.ComputeReducedInterval with RapidFireStartLevel=2.
        int reductions = Math.Max(0, level - MissileRapidFireStartLevel + 1);
        long reduced = fam.FireIntervalTicks
            - (long)reductions * fam.FireIntervalReductionPerLevel;
        int effectiveMin = Math.Min(
            fam.FireIntervalTicks,
            fam.MinimumFireIntervalTicks);
        return (int)Math.Max(effectiveMin, reduced);
    }

    static double TheoreticalMissileStDps(MissileFamilyDefinition fam, int level)
    {
        int interval = MissileIntervalAtLevel(fam, level);
        if (interval < 1)
            return 0;
        return MissileStShotDamage(fam, level)
            * (double)SimSpace.TicksPerSecond
            / interval;
    }

    /// <summary>
    /// Mirrors GameDataSet.ApplyMissileFamily (private) for lab configs.
    /// </summary>
    static void ApplyMissileFamilyToConfig(
        BattleSimConfig config,
        MissileFamilyDefinition definition)
    {
        config.MissileFamily = definition.Family;
        config.MissileBaseDamage = definition.BaseDamage;
        config.MissileFireIntervalTicks = definition.FireIntervalTicks;
        config.MissileMinimumFireIntervalTicks =
            definition.MinimumFireIntervalTicks;
        config.MissileFireIntervalReductionPerLevel =
            definition.FireIntervalReductionPerLevel;
        config.MissileRapidFireStartLevel = MissileRapidFireStartLevel;
        config.MissileSpeedXNumerator = definition.SpeedXNumerator;
        config.MissileSpeedXDenominator = definition.SpeedXDenominator;
        config.MissileFallSpeedYNumerator = definition.FallSpeedYNumerator;
        config.MissileFallSpeedYDenominator = definition.FallSpeedYDenominator;
        config.MissilePierceEnemyCount = definition.PierceEnemyCount;
        config.MissileExplosionDamage = definition.ExplosionDamage;
        config.MissileExplosionRadiusSubUnits =
            definition.ExplosionRadiusSubUnits;
        config.MissileExplosionMaxTargets = definition.ExplosionMaxTargets;
    }

    static int SimulateMissileSingleTargetDamage(
        GameDataSet data,
        MissileFamilyDefinition family,
        int level,
        BattleModifier modifiers,
        int ticks)
    {
        BattleSimConfig config = data.CreateBattleSimConfig();
        ApplyMissileFamilyToConfig(config, family);
        // Core always volleys main on Fire; zero its damage so the lab is missile-only.
        config.MainShotBaseDamage = 0;
        config.UseConfiguredMainShotStats = true;
        config.ScrollSpeedNumerator = 0;
        config.ScrollSpeedDenominator = 1;
        config.PlayerSpawnX = 0;
        config.PlayerSpawnY = 0;
        config.PlayerMinX = -10000;
        config.PlayerMaxX = 10000;
        config.PlayerMinY = -10000;
        config.PlayerMaxY = 10000;
        config.PlayerHalfWidth = 0;
        config.PlayerHalfHeight = 0;
        config.CapsuleNoDropWeight = 1_000_000;
        config.MaxBullets = 128;
        config.EnemyBulletDamage = 0;
        config.MaxEnemyBullets = 0;
        // Tall sponge (not touching player). Bomb fall ~3u over 2u of travel.
        const int spongeHp = 1_000_000;
        int halfW = SimSpace.SubUnitsPerWorldUnit / 2;
        int halfH = 6 * SimSpace.SubUnitsPerWorldUnit;
        var enemy = new EnemyDefinition(
            "missile_sponge",
            "missile_sponge",
            spongeHp,
            0,
            0,
            EnemyMovePattern.Static,
            0,
            1,
            0,
            halfW,
            halfH,
            0,
            0,
            1,
            1);
        // Keep clear of player contact (dx > halfW). Close enough for bomb fall.
        int spongeX = 2 * SimSpace.SubUnitsPerWorldUnit;
        int spongeY = family.Family == MissileFamily.SpreadBomb
            ? -2 * SimSpace.SubUnitsPerWorldUnit
            : 0;
        WeaponDefinition main = data.BattleContent.PlayerWeapon;
        var content = new BattleContent(
            new[] { enemy },
            data.BattleContent.Weapons.ToArray(),
            main.Id,
            data.BattleContent.MissileFamilies.ToArray(),
            data.BattleContent.DefaultMissileFamily,
            data.BattleContent.OptionFormations.ToArray(),
            data.BattleContent.DefaultOptionFormation);
        var segment = new StageSegment(
            "missile_st_lab",
            ticks + 10,
            new[] { new SpawnEvent(0, enemy.Id, spongeX, spongeY) },
            1,
            1,
            new[] { 1 });
        var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        // Main 0 / Missile level / Option 0 / Shield 0
        gauge.ImportLevels(new[] { 0, level, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0x34F1UL),
            plan,
            content,
            gauge,
            modifiers);
        InputCommand fire = new InputCommand(0, 0, true);
        for (int t = 0; t < ticks; t++)
            sim.Step(in fire);

        if (sim.Enemies.Count == 0)
            return spongeHp;
        return spongeHp - sim.Enemies[0].Hp;
    }

    /// <summary>
    /// Missile-only pack clear. column=true places enemies along +X (pierce lane);
    /// column=false places a tight cluster around bomb impact altitude.
    /// Returns ticks-to-clear, or 0 if not cleared.
    /// </summary>
    static int SimulateMissilePackClear(
        GameDataSet data,
        MissileFamilyDefinition family,
        int packSize,
        int enemyHp,
        int spacingSubUnits,
        bool column,
        BattleModifier modifiers,
        int maxTicks)
    {
        BattleSimConfig config = data.CreateBattleSimConfig();
        ApplyMissileFamilyToConfig(config, family);
        // Isolate missile DPS (main always volleys on Fire).
        config.MainShotBaseDamage = 0;
        config.UseConfiguredMainShotStats = true;
        config.ScrollSpeedNumerator = 0;
        config.ScrollSpeedDenominator = 1;
        config.PlayerSpeedNumerator = 0;
        config.PlayerSpeedDenominator = 1;
        config.PlayerSpawnX = 0;
        config.PlayerSpawnY = 0;
        config.PlayerMinX = -10000;
        config.PlayerMaxX = 10000;
        config.PlayerMinY = -10000;
        config.PlayerMaxY = 10000;
        config.PlayerHalfWidth = 0;
        config.PlayerHalfHeight = 0;
        config.CapsuleNoDropWeight = 1_000_000;
        config.MaxBullets = 128;
        config.EnemyBulletDamage = 0;
        config.MaxEnemyBullets = 0;

        int half = SimSpace.SubUnitsPerWorldUnit / 3;
        // Bomb impact altitude at x≈1.5u: fallSpeed 9u/s, speedX 6u/s → y≈-2.25u.
        bool isBomb = family.Family == MissileFamily.SpreadBomb;
        int impactX = (isBomb ? 3 : 2) * SimSpace.SubUnitsPerWorldUnit / (isBomb ? 2 : 1);
        int impactY = isBomb ? -2 * SimSpace.SubUnitsPerWorldUnit : 0;
        var enemies = new EnemyDefinition[packSize];
        var spawns = new SpawnEvent[packSize];
        for (int i = 0; i < packSize; i++)
        {
            string id = "m_fodder_" + i;
            enemies[i] = new EnemyDefinition(
                id,
                id,
                enemyHp,
                0,
                0, // contact damage 0 (still removed on touch — keep clear of player)
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
            int x;
            int y;
            if (column)
            {
                x = impactX + i * spacingSubUnits;
                y = impactY;
            }
            else
            {
                // Tight cluster fully inside explosion radius (~1.75u) around impact.
                int col = i % 4;
                int row = i / 4;
                x = impactX + (col - 1) * spacingSubUnits;
                y = impactY + (row - 1) * spacingSubUnits;
            }
            spawns[i] = new SpawnEvent(0, id, x, y);
        }

        WeaponDefinition main = data.BattleContent.PlayerWeapon;
        var content = new BattleContent(
            enemies,
            data.BattleContent.Weapons.ToArray(),
            main.Id,
            data.BattleContent.MissileFamilies.ToArray(),
            data.BattleContent.DefaultMissileFamily,
            data.BattleContent.OptionFormations.ToArray(),
            data.BattleContent.DefaultOptionFormation);
        var segment = new StageSegment(
            "missile_pack",
            maxTicks + 10,
            spawns,
            1,
            1,
            new[] { 1 });
        var plan = new StagePlan(new[] { segment }, "legacy", 1, 1, 1);
        PowerUpGauge gauge = PowerUpGauge.CreateDefault();
        gauge.ImportLevels(new[] { 0, 1, 0, 0 });

        var sim = new BattleSim(
            config,
            new Rng(0xB034UL),
            plan,
            content,
            gauge,
            modifiers);
        InputCommand fire = new InputCommand(0, 0, true);
        for (int tick = 1; tick <= maxTicks; tick++)
        {
            sim.Step(in fire);
            if (sim.Enemies.Count == 0)
                return tick;
        }
        return 0;
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

    /// <summary>
    /// REQ-029: segment selection weights bias plain workhorses vs spectacle.
    /// Hard FAIL on catalog shape; distribution bands are soft WARN (§7).
    /// </summary>
    static int CheckSegmentWeights(GameDataSet data)
    {
        int failures = 0;
        var catalog = data.StageGeneration;
        Console.WriteLine(
            "Segment weights (waves.json, REQ-029 provisional §7):");

        if (catalog.Segments.Count != ExpectedSegmentCount)
        {
            Console.WriteLine(
                $"FAIL weights: expected {ExpectedSegmentCount} segments, " +
                $"got {catalog.Segments.Count}.");
            failures++;
        }

        int minW = int.MaxValue;
        int maxW = 0;
        long sum = 0;
        int lowCount = 0;
        int highCount = 0;
        int defaultCount = 0;
        var dist = new SortedDictionary<int, int>();

        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            int w = seg.Weight;
            if (w < 1)
            {
                Console.WriteLine(
                    $"FAIL weights: '{seg.SegmentId}' weight {w} < 1.");
                failures++;
            }

            if (w < minW) minW = w;
            if (w > maxW) maxW = w;
            sum += w;
            if (!dist.ContainsKey(w)) dist[w] = 0;
            dist[w]++;
            if (w <= MaxWeightedLow) lowCount++;
            if (w >= MinWeightedHigh) highCount++;
            if (w == DefaultSegmentWeight) defaultCount++;
        }

        double mean = catalog.Segments.Count == 0
            ? 0
            : sum / (double)catalog.Segments.Count;
        Console.WriteLine(
            $"  n={catalog.Segments.Count} min={minW} max={maxW} mean={mean:F2} " +
            $"defaultW={DefaultSegmentWeight} atDefault={defaultCount}");
        Console.WriteLine(
            "  dist: " + string.Join(
                ", ",
                dist.Select(kv => $"{kv.Key}×{kv.Value}")));
        Console.WriteLine(
            $"  low(w≤{MaxWeightedLow})={lowCount} high(w≥{MinWeightedHigh})={highCount}");

        if (minW == maxW && catalog.Segments.Count > 1)
        {
            Console.WriteLine(
                "FAIL weights: all segments share one weight — no rarity bias.");
            failures++;
        }

        if (lowCount < MinWeightedLowCount)
        {
            Console.WriteLine(
                $"FAIL weights: only {lowCount} low-weight spectacle segments " +
                $"(need ≥{MinWeightedLowCount} with w≤{MaxWeightedLow}).");
            failures++;
        }

        if (highCount < MinWeightedHighCount)
        {
            Console.WriteLine(
                $"FAIL weights: only {highCount} high-weight workhorse segments " +
                $"(need ≥{MinWeightedHighCount} with w≥{MinWeightedHigh}).");
            failures++;
        }

        // Soft: top spectacle should be rare relative to plain intro lines.
        StageSegmentTemplate lightest = catalog.Segments
            .OrderBy(s => s.Weight)
            .ThenBy(s => s.SegmentId, StringComparer.Ordinal)
            .First();
        StageSegmentTemplate heaviest = catalog.Segments
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.SegmentId, StringComparer.Ordinal)
            .First();
        Console.WriteLine(
            $"  lightest={lightest.SegmentId} w={lightest.Weight}  " +
            $"heaviest={heaviest.SegmentId} w={heaviest.Weight}");
        if (heaviest.Weight < lightest.Weight * 3)
        {
            Console.WriteLine(
                $"WARN weights: heaviest/lightest ratio " +
                $"{heaviest.Weight / (double)lightest.Weight:F2} < 3 " +
                "(spectacle may not feel rare enough, §7).");
        }

        if (failures == 0)
            Console.WriteLine("PASS: segment weight catalog bias checks.");
        return failures;
    }

    /// <summary>
    /// REQ-028/029: sample generated plans per encounter type and compare
    /// combat HP / capsule EV / score mult. Core-locked knobs are report-only.
    /// </summary>
    static int CheckEncounterBalance(GameDataSet data)
    {
        int failures = 0;
        var catalog = data.StageGeneration;
        var generator = new SegmentStageGenerator(catalog);
        BattleContent content = data.BattleContent;
        BattleSimConfig defaults = BattleSimConfig.CreateDefault();

        Console.WriteLine(
            "Encounter types (Normal/Elite/Supply/Hazard/Rare, provisional §7):");
        Console.WriteLine(
            $"  Core Rare chance={defaults.RareEncounterChanceNumerator}/" +
            $"{defaults.RareEncounterChanceDenominator} " +
            $"(≈{RareEncounterChance:P0}) rewardPicks={defaults.RareRewardSelectionCount}");
        Console.WriteLine(
            "  multipliers: Elite HP 3/2 · Rare HP 2/1 · Supply drop 4/1 · " +
            "Hazard score 3/2");

        var types = new[]
        {
            EncounterType.Normal,
            EncounterType.Elite,
            EncounterType.Supply,
            EncounterType.Hazard,
            EncounterType.Rare
        };

        // Aggregate over theme × stage 1..5 × seeds.
        var sums = new Dictionary<EncounterType, EncounterAgg>();
        foreach (EncounterType t in types)
            sums[t] = new EncounterAgg();

        int assemblyFails = 0;
        const ulong baseSeed = 0xE2C0UL;
        for (int stage = 1; stage <= 5; stage++)
        {
            int difficulty = stage;
            for (int ti = 0; ti < catalog.ThemeIds.Count; ti++)
            {
                string theme = catalog.ThemeIds[ti];
                for (int s = 0; s < EncounterSampleSeeds; s++)
                {
                    ulong seed = baseSeed
                        + (ulong)stage * 10007UL
                        + (ulong)ti * 997UL
                        + (ulong)s * 131UL;
                    foreach (EncounterType encounter in types)
                    {
                        if (!generator.CanGenerateRoute(
                                theme, stage, difficulty, encounter))
                        {
                            Console.WriteLine(
                                $"FAIL encounter: cannot generate " +
                                $"{encounter} theme={theme} stage={stage}.");
                            assemblyFails++;
                            continue;
                        }

                        StagePlan plan;
                        try
                        {
                            plan = generator.GenerateRoute(
                                seed, stage, difficulty, theme, encounter);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"FAIL encounter: {encounter} theme={theme} " +
                                $"stage={stage} seed={seed:X}: {ex.Message}");
                            assemblyFails++;
                            continue;
                        }

                        if (!StagePlanClearability.IsClearable(plan))
                        {
                            Console.WriteLine(
                                $"FAIL encounter: {encounter} theme={theme} " +
                                $"stage={stage} seed={seed:X} not clearable.");
                            assemblyFails++;
                            continue;
                        }

                        int rawHp = PlanSpawnHp(plan, content);
                        int scaledHp = ScaleInt(
                            rawHp,
                            plan.EncounterEnemyHpMultiplierNumerator,
                            plan.EncounterEnemyHpMultiplierDenominator);
                        double capsuleEv = PlanCapsuleExpectation(
                            plan, content, data.CapsuleNoDropWeight);
                        int scoreNum = plan.EncounterScoreMultiplierNumerator;
                        int scoreDen = plan.EncounterScoreMultiplierDenominator;
                        int obstacles = PlanObstacleCount(plan);
                        int segs = plan.Segments.Count;
                        bool hasBoss = plan.BossMaxHp > 0;

                        EncounterAgg agg = sums[encounter];
                        agg.Samples++;
                        agg.HpSum += scaledHp;
                        agg.BossHpSum += hasBoss ? plan.BossMaxHp : 0;
                        agg.CapsuleSum += capsuleEv;
                        agg.ObstacleSum += obstacles;
                        agg.SegmentSum += segs;
                        agg.BossPresent += hasBoss ? 1 : 0;
                        agg.ScoreNum = scoreNum;
                        agg.ScoreDen = scoreDen;
                        agg.HpNum = plan.EncounterEnemyHpMultiplierNumerator;
                        agg.HpDen = plan.EncounterEnemyHpMultiplierDenominator;
                        agg.DropNum = plan.CapsuleDropMultiplierNumerator;
                        agg.DropDen = plan.CapsuleDropMultiplierDenominator;
                    }
                }
            }
        }

        failures += assemblyFails;

        foreach (EncounterType encounter in types)
        {
            EncounterAgg a = sums[encounter];
            if (a.Samples == 0)
            {
                Console.WriteLine($"FAIL encounter: no samples for {encounter}.");
                failures++;
                continue;
            }

            double avgHp = a.HpSum / a.Samples;
            double avgBoss = a.BossHpSum / a.Samples;
            double avgCap = a.CapsuleSum / a.Samples;
            double avgObs = a.ObstacleSum / (double)a.Samples;
            double avgSeg = a.SegmentSum / (double)a.Samples;
            Console.WriteLine(
                $"  {encounter,-7} n={a.Samples} segs={avgSeg:F2} " +
                $"spawnHP={avgHp:F0} bossHP={avgBoss:F0} " +
                $"E_caps={avgCap:F2} obs={avgObs:F1} " +
                $"bossRate={a.BossPresent / (double)a.Samples:P0} " +
                $"hp×{a.HpNum}/{a.HpDen} drop×{a.DropNum}/{a.DropDen} " +
                $"score×{a.ScoreNum}/{a.ScoreDen}");
        }

        EncounterAgg normal = sums[EncounterType.Normal];
        EncounterAgg elite = sums[EncounterType.Elite];
        EncounterAgg supply = sums[EncounterType.Supply];
        EncounterAgg hazard = sums[EncounterType.Hazard];
        EncounterAgg rare = sums[EncounterType.Rare];

        if (normal.Samples > 0 && elite.Samples > 0)
        {
            double nLoad = normal.HpSum / normal.Samples + normal.BossHpSum / normal.Samples;
            double eLoad = elite.HpSum / elite.Samples + elite.BossHpSum / elite.Samples;
            double ratio = eLoad / Math.Max(1.0, nLoad);
            Console.WriteLine(
                $"  elite/normal combat-load ratio={ratio:F2} " +
                $"(band [{EliteHpRatioMin:F2},{EliteHpRatioMax:F2}])");
            // Elite is 1 segment @1.5× HP + boss vs 3 segs + boss → shorter but denser.
            // Reward: forced modifier bias (Core) + same 1 pick — risk premium is length-compressed.
            if (ratio < EliteHpRatioMin || ratio > EliteHpRatioMax)
            {
                Console.WriteLine(
                    $"WARN encounter: elite load ratio {ratio:F2} outside band " +
                    $"[{EliteHpRatioMin:F2},{EliteHpRatioMax:F2}] (§7 / Core HP mult).");
            }

            // Soft EV sketch: elite modifier guarantee is Core-only recommendation.
            Console.WriteLine(
                "  elite reward note: 1 pick with modifier-weight bias (Core RunManager); " +
                "HP denser but node shorter — reward adequacy is playtest §7.");
        }

        if (normal.Samples > 0 && supply.Samples > 0)
        {
            double nHp = normal.HpSum / normal.Samples + normal.BossHpSum / normal.Samples;
            double sHp = supply.HpSum / supply.Samples + supply.BossHpSum / supply.Samples;
            double sRatio = sHp / Math.Max(1.0, nHp);
            double nCap1 = (normal.CapsuleSum / normal.Samples)
                / Math.Max(1.0, normal.SegmentSum / (double)normal.Samples);
            double sCap = supply.CapsuleSum / supply.Samples;
            double capRatio = sCap / Math.Max(0.01, nCap1);
            Console.WriteLine(
                $"  supply/normal combat-load ratio={sRatio:F2} " +
                $"(want ≤{SupplyHpRatioMax:F2}); " +
                $"supply E_caps={sCap:F2} vs normal/seg={nCap1:F2} " +
                $"(boost≈{capRatio:F2}×, want ≥{SupplyCapsuleRatioMin:F2})");

            if (sRatio > SupplyHpRatioMax)
            {
                Console.WriteLine(
                    $"WARN encounter: supply load ratio {sRatio:F2} too high " +
                    $"(should stay light, §7).");
            }

            if (capRatio < SupplyCapsuleRatioMin)
            {
                Console.WriteLine(
                    $"WARN encounter: supply capsule boost {capRatio:F2}× " +
                    $"< {SupplyCapsuleRatioMin:F2}× (§7 / Core drop mult 4).");
            }

            // Optimal-path risk: supply is very safe + high capsules. Frequency is
            // Core route RNG (equal among Normal/Elite/Supply/Hazard when Rare off).
            Console.WriteLine(
                "  supply safety note: no boss + lowest-spawn segment + drop×4. " +
                "If players always pick Supply, Core should lower Supply route weight " +
                "or move drop mult to GameData (recommendation only).");
        }

        if (normal.Samples > 0 && rare.Samples > 0)
        {
            double nHp = normal.HpSum / normal.Samples;
            double rHp = rare.HpSum / rare.Samples;
            double rRatio = rHp / Math.Max(1.0, nHp);
            Console.WriteLine(
                $"  rare/normal spawnHP ratio={rRatio:F2} " +
                $"(band [{RareHpRatioMin:F2},{RareHpRatioMax:F2}]); " +
                $"reward picks={defaults.RareRewardSelectionCount}; " +
                $"appear chance={RareEncounterChance:P0}");
            if (rRatio < RareHpRatioMin || rRatio > RareHpRatioMax)
            {
                Console.WriteLine(
                    $"WARN encounter: rare HP ratio {rRatio:F2} outside band " +
                    $"(Core Rare HP mult 2/1, §7).");
            }

            // 12% of routes include a Rare slot among 2–3 options → ~4–6% of picks
            // if chosen whenever offered. Soft recommendation only.
            double offerRate = RareEncounterChance;
            double assumedPickIfOffered = 0.45;
            Console.WriteLine(
                $"  rare route sketch: offer≈{offerRate:P0} · " +
                $"if pick-when-offered≈{assumedPickIfOffered:P0} → " +
                $"play rate≈{offerRate * assumedPickIfOffered:P1} of stage transitions. " +
                "12% offer feels sparse-special; raise only if Rare nodes feel invisible.");
        }

        if (hazard.Samples > 0)
        {
            double scoreMult = hazard.ScoreNum / (double)Math.Max(1, hazard.ScoreDen);
            double nObs = normal.Samples == 0
                ? 0
                : normal.ObstacleSum / (double)normal.Samples;
            double hObs = hazard.ObstacleSum / (double)hazard.Samples;
            Console.WriteLine(
                $"  hazard score×={scoreMult:F2} (Core {HazardScoreMult:F2}); " +
                $"obs normal={nObs:F1} hazard={hObs:F1}");
            if (Math.Abs(scoreMult - HazardScoreMult) > 0.01)
            {
                Console.WriteLine(
                    $"FAIL encounter: hazard score mult {scoreMult:F2} " +
                    $"!= expected {HazardScoreMult:F2}.");
                failures++;
            }

            if (hObs + 0.01 < nObs)
            {
                Console.WriteLine(
                    "WARN encounter: hazard obstacles not denser than normal (§7).");
            }
            else
            {
                // Score 1.5× for +~50% obstacles on same HP — soft judgment.
                double obsGain = nObs <= 0.01 ? 1.0 : hObs / nObs;
                Console.WriteLine(
                    $"  hazard risk sketch: obstacle gain×{obsGain:F2} vs score×{scoreMult:F2}. " +
                    "If mazes feel brutal, Core score mult 3/2 may be low; " +
                    "if trivial, lower obstacle inject or score (recommendation only).");
            }
        }

        if (failures == 0)
            Console.WriteLine("PASS: encounter assembly + risk-reward sketch.");
        return failures;
    }

    /// <summary>
    /// REQ-029: after capsule magnet, nearly all drops are recovered.
    /// Expectation uses kill drop formula with full pickup assumption.
    /// </summary>
    static int CheckCapsuleDropAfterMagnet(GameDataSet data)
    {
        int failures = 0;
        int noDrop = data.CapsuleNoDropWeight;
        var catalog = data.StageGeneration;
        BattleContent content = data.BattleContent;
        BattleSimConfig defaults = BattleSimConfig.CreateDefault();

        Console.WriteLine(
            "Capsule drops after magnet (enemies.json dropTable, provisional §7):");
        Console.WriteLine(
            $"  noDropWeight={noDrop} magnetRadius={defaults.CapsuleMagnetRadiusSubUnits}su " +
            $"magnetSpeed={defaults.CapsuleMagnetSpeedNumerator}/" +
            $"{defaults.CapsuleMagnetSpeedDenominator} (Core config)");

        long weightSum = 0;
        double weightedCapsule = 0;
        double weightedSupply = 0;
        int zeroDropEnemies = 0;
        foreach (EnemyDefinition enemy in content.Enemies)
        {
            if (enemy.DropWeight <= 0)
                zeroDropEnemies++;
        }

        foreach (StageSegmentTemplate seg in catalog.Segments)
        {
            double eNormal = SegmentCapsuleExpectation(
                seg, content, noDrop, dropNum: 1, dropDen: 1);
            double eSupply = SegmentCapsuleExpectation(
                seg, content, noDrop, dropNum: 4, dropDen: 1);
            weightSum += seg.Weight;
            weightedCapsule += eNormal * seg.Weight;
            weightedSupply += eSupply * seg.Weight;
        }

        double eSeg = weightSum == 0 ? 0 : weightedCapsule / weightSum;
        double eStage = eSeg * catalog.SegmentsPerStage;
        double eSupplyNode = weightSum == 0 ? 0 : weightedSupply / weightSum;

        Console.WriteLine(
            $"  weight-biased E_caps/seg={eSeg:F2} · E_stage({catalog.SegmentsPerStage} segs)={eStage:F2}");
        Console.WriteLine(
            $"  weight-biased E_caps/supply-node(1 seg drop×4)={eSupplyNode:F2}");
        Console.WriteLine(
            $"  band stage [{MinStageCapsuleExpectation:F0},{MaxStageCapsuleExpectation:F0}] · " +
            $"supply node max {MaxSupplyNodeCapsuleExpectation:F0} · " +
            $"zero-drop enemies={zeroDropEnemies}");

        if (noDrop < 1)
        {
            Console.WriteLine("FAIL drops: noDropWeight must be ≥ 1.");
            failures++;
        }

        if (eStage < MinStageCapsuleExpectation || eStage > MaxStageCapsuleExpectation)
        {
            Console.WriteLine(
                $"FAIL drops: stage capsule EV {eStage:F2} outside band " +
                $"[{MinStageCapsuleExpectation:F0},{MaxStageCapsuleExpectation:F0}] " +
                "(magnet ≈ full recovery — retune noDropWeight / dropWeight).");
            failures++;
        }

        if (eSupplyNode > MaxSupplyNodeCapsuleExpectation)
        {
            Console.WriteLine(
                $"FAIL drops: supply node EV {eSupplyNode:F2} > " +
                $"{MaxSupplyNodeCapsuleExpectation:F0} (drop×4 + magnet).");
            failures++;
        }

        // Soft: map per-enemy drop rates for report.
        Console.WriteLine("  sample drop p (dropW/(noDrop+dropW)):");
        foreach (EnemyDefinition enemy in content.Enemies.Take(6))
        {
            int dw = enemy.DropWeight;
            double p = dw / (double)(noDrop + dw);
            Console.WriteLine($"    {enemy.Id,-22} dropW={dw,2} p={p:P1}");
        }

        if (failures == 0)
            Console.WriteLine("PASS: capsule drop EV band after magnet.");
        return failures;
    }

    /// <summary>
    /// REQ-033 boss redesign gates: HP curve mono, TTK 35–45s @ biome DPS,
    /// full-power ≥12s, exactly 3 phases, phase threat mono, equal-split thresholds.
    /// All gates provisional (AGENTS.md §7). Multipart colossal bosses (REQ-035)
    /// are excluded — see CheckColossalBosses.
    /// </summary>
    static int CheckBossRedesign(GameDataSet data)
    {
        int failures = 0;
        IReadOnlyList<StageBossTemplate> allBosses = data.StageGeneration.Bosses;
        var bosses = new List<StageBossTemplate>();
        for (int i = 0; i < allBosses.Count; i++)
            if (allBosses[i].Parts == null || allBosses[i].Parts.Count == 0)
                bosses.Add(allBosses[i]);

        Console.WriteLine(
            "Boss redesign (REQ-033, provisional §7): " +
            $"TTK {BossTtkExpectedMin:F0}–{BossTtkExpectedMax:F0}s @ biome DPS · " +
            $"full-power ≥{BossTtkFullMin:F0}s · phases={BossRequiredPhaseCount} · " +
            "threat mono · equal-split thresholds " +
            $"(standard bosses only; colossal={allBosses.Count - bosses.Count})");

        if (bosses.Count != BossExpectedDps.Length)
        {
            Console.WriteLine(
                $"FAIL boss: expected {BossExpectedDps.Length} standard bosses, " +
                $"got {bosses.Count} (catalog total {allBosses.Count}).");
            return 1;
        }

        var byId = new Dictionary<string, StageBossTemplate>(StringComparer.Ordinal);
        for (int i = 0; i < bosses.Count; i++)
            byId[bosses[i].BossId] = bosses[i];

        int prevHp = 0;
        for (int i = 0; i < BossExpectedDps.Length; i++)
        {
            string id = BossExpectedDps[i].Id;
            double expectedDps = BossExpectedDps[i].ExpectedDps;
            if (!byId.TryGetValue(id, out StageBossTemplate boss))
            {
                Console.WriteLine($"FAIL boss: missing catalog entry '{id}'.");
                failures++;
                continue;
            }

            int hp = boss.MaxHp;
            if (i > 0 && hp <= prevHp)
            {
                Console.WriteLine(
                    $"FAIL boss: HP curve not strictly mono at '{id}' " +
                    $"(hp={hp} ≤ prev={prevHp}).");
                failures++;
            }
            prevHp = hp;

            double ttkExpected = hp / expectedDps;
            double ttkFull = hp / BossFullPowerDps;
            bool midOk = ttkExpected >= BossTtkExpectedMin && ttkExpected <= BossTtkExpectedMax;
            bool fullOk = ttkFull >= BossTtkFullMin;

            Console.WriteLine(
                $"  {id,-16} hp={hp,6} @ {expectedDps,6:F0} DPS → TTK={ttkExpected:F1}s " +
                $"[{(midOk ? "midOK" : "OUT")}]  full@{BossFullPowerDps:F0} → " +
                $"{ttkFull:F1}s [{(fullOk ? "floorOK" : "BELOW")}]");

            if (!midOk)
            {
                Console.WriteLine(
                    $"FAIL boss: '{id}' expected TTK {ttkExpected:F1}s outside " +
                    $"[{BossTtkExpectedMin:F0},{BossTtkExpectedMax:F0}]s.");
                failures++;
            }

            if (!fullOk)
            {
                Console.WriteLine(
                    $"FAIL boss: '{id}' full-power TTK {ttkFull:F1}s < {BossTtkFullMin:F0}s.");
                failures++;
            }

            if (boss.Phases == null || boss.Phases.Count != BossRequiredPhaseCount)
            {
                Console.WriteLine(
                    $"FAIL boss: '{id}' needs exactly {BossRequiredPhaseCount} phases, " +
                    $"got {boss.Phases?.Count ?? 0}.");
                failures++;
                continue;
            }

            // Document Core equal-split thresholds (remaining HP ratios).
            // nextPhase = (maxHp - hp) * N / maxHp → phase1 @ remaining 2/3, phase2 @ 1/3.
            int hpEnterP1 = (int)((long)hp * 2 / 3); // remaining when damage reaches maxHp/3
            int hpEnterP2 = (int)((long)hp / 3);
            Console.WriteLine(
                $"    phase thresholds (Core equal-split remaining): " +
                $"p1≤{hpEnterP1} ({BossPhaseThreshold0:F3})  p2≤{hpEnterP2} ({BossPhaseThreshold1:F3})");

            double prevThreat = -1.0;
            for (int p = 0; p < boss.Phases.Count; p++)
            {
                BossPhase phase = boss.Phases[p];
                double speedWu = phase.BulletSpeedNumerator / (double)phase.BulletSpeedDenominator
                    * SimSpace.TicksPerSecond / SimSpace.SubUnitsPerWorldUnit;
                double threat = phase.Ways * speedWu / phase.FireIntervalTicks;
                string personality = p == 0 ? "aimed" : p == 1 ? "spread" : "rapid";
                Console.WriteLine(
                    $"    p{p} {personality,-6} int={phase.FireIntervalTicks,3}t " +
                    $"ways={phase.Ways} spd≈{speedWu:F1}u/s threat={threat:F3}" +
                    (prevThreat < 0 ? "" : threat > prevThreat ? " monoOK" : " MONO FAIL"));

                // Personality soft check: aimed low ways, spread high ways, rapid high speed.
                if (p == 0 && phase.Ways > 4)
                {
                    Console.WriteLine(
                        $"WARN boss: '{id}' p0 ways={phase.Ways} high for aimed (expected ≤4).");
                }

                if (p == 1)
                {
                    int waysP0 = boss.Phases[0].Ways;
                    if (phase.Ways <= waysP0)
                    {
                        Console.WriteLine(
                            $"FAIL boss: '{id}' spread ways {phase.Ways} must exceed aimed {waysP0}.");
                        failures++;
                    }
                }

                if (p == 2)
                {
                    double speedP1 = boss.Phases[1].BulletSpeedNumerator
                        / (double)boss.Phases[1].BulletSpeedDenominator
                        * SimSpace.TicksPerSecond / SimSpace.SubUnitsPerWorldUnit;
                    if (speedWu <= speedP1)
                    {
                        Console.WriteLine(
                            $"FAIL boss: '{id}' rapid speed {speedWu:F1} must exceed " +
                            $"spread {speedP1:F1}.");
                        failures++;
                    }

                    if (phase.Ways >= boss.Phases[1].Ways)
                    {
                        Console.WriteLine(
                            $"FAIL boss: '{id}' rapid ways {phase.Ways} must be fewer " +
                            $"than spread {boss.Phases[1].Ways}.");
                        failures++;
                    }
                }

                if (prevThreat >= 0 && threat <= prevThreat)
                {
                    Console.WriteLine(
                        $"FAIL boss: '{id}' phase threat not strictly mono " +
                        $"(p{p - 1}={prevThreat:F3} → p{p}={threat:F3}).");
                    failures++;
                }

                prevThreat = threat;
            }
        }

        // REQ-054: phases may declare movementPattern / partVulnerability in JSON.
        // content-branch Core may still ignore unknown DTO fields until sim merge.
        Console.WriteLine(
            "  movement/part axes (REQ-054 JSON): " +
            "movementPattern, movementAmplitude, movementPeriodTicks, " +
            "partVulnerability on bosses[].phases[] — " +
            "consumed by sim-branch BossPhase parser when merged.");

        if (failures == 0)
            Console.WriteLine("PASS: boss redesign TTK / phases / threat mono.");
        return failures;
    }

    /// <summary>
    /// REQ-035 colossal boss gates (provisional §7):
    /// catalog IDs + parts sum/core, TTK 100–120s @ expected DPS, full-power
    /// effective ≥40s, brood spawn peak vs MaxEnemies, min-path parity,
    /// GenerateColossalBoss Supports(stage 5 / diff 5), normal-gen sample.
    /// </summary>
    static int CheckColossalBosses(GameDataSet data, SegmentStageGenerator generator)
    {
        int failures = 0;
        Console.WriteLine(
            "Colossal bosses (REQ-035, provisional §7): " +
            $"totalHp={ColossalTotalHp} core={ColossalCoreHp} · " +
            $"TTK {ColossalTtkExpectedMin:F0}–{ColossalTtkExpectedMax:F0}s " +
            $"@ {ColossalExpectedDps:F0} DPS · full-eff ≥{ColossalTtkFullMin:F0}s " +
            $"@ {ColossalFullPowerEffectiveDps:F0} DPS · spawn peak " +
            $"vs MaxEnemies={ColossalMaxEnemiesCap}");

        var byId = new Dictionary<string, StageBossTemplate>(StringComparer.Ordinal);
        IReadOnlyList<StageBossTemplate> bosses = data.StageGeneration.Bosses;
        for (int i = 0; i < bosses.Count; i++)
            byId[bosses[i].BossId] = bosses[i];

        StageBossTemplate leviathan = null;
        StageBossTemplate broodmother = null;
        for (int i = 0; i < ColossalBossIds.Length; i++)
        {
            string id = ColossalBossIds[i];
            if (!byId.TryGetValue(id, out StageBossTemplate boss))
            {
                Console.WriteLine($"FAIL colossal: missing catalog entry '{id}'.");
                failures++;
                continue;
            }

            if (string.Equals(id, SegmentStageGenerator.LeviathanBossId, StringComparison.Ordinal))
                leviathan = boss;
            else if (string.Equals(id, SegmentStageGenerator.BroodmotherBossId, StringComparison.Ordinal))
                broodmother = boss;

            failures += CheckOneColossalBoss(boss, data);
        }

        if (!generator.CanGenerateColossalBoss(ColossalBossKind.Leviathan)
            || !generator.CanGenerateColossalBoss(ColossalBossKind.Broodmother))
        {
            Console.WriteLine(
                "FAIL colossal: CanGenerateColossalBoss false for Leviathan/Broodmother.");
            failures++;
        }
        else
        {
            // Hidden boss path: generationBiomeIndex = BiomeCount (5), difficulty = 5.
            try
            {
                StagePlan levPlan = generator.GenerateColossalBoss(
                    1UL, 5, 5, ColossalBossKind.Leviathan);
                StagePlan broodPlan = generator.GenerateColossalBoss(
                    2UL, 5, 5, ColossalBossKind.Broodmother);
                if (levPlan.BossParts == null || levPlan.BossParts.Count == 0)
                {
                    Console.WriteLine("FAIL colossal: leviathan plan has no parts.");
                    failures++;
                }
                if (broodPlan.BossParts == null || broodPlan.BossParts.Count == 0)
                {
                    Console.WriteLine("FAIL colossal: broodmother plan has no parts.");
                    failures++;
                }
                Console.WriteLine(
                    $"  GenerateColossalBoss(stage5/diff5): " +
                    $"lev parts={levPlan.BossParts?.Count ?? 0} " +
                    $"brood parts={broodPlan.BossParts?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"FAIL colossal: GenerateColossalBoss threw: {ex.Message}");
                failures++;
            }
        }

        // Parity: same total/core HP; min-path (gates+core) should stay close.
        if (leviathan != null && broodmother != null)
        {
            int levMin = MinPathHp(leviathan);
            int broodMin = MinPathHp(broodmother);
            double levMinTtk = levMin / ColossalExpectedDps;
            double broodMinTtk = broodMin / ColossalExpectedDps;
            double ratio = Math.Max(levMin, broodMin) / (double)Math.Min(levMin, broodMin);
            Console.WriteLine(
                $"  parity min-path: leviathan={levMin}hp TTK≈{levMinTtk:F1}s · " +
                $"broodmother={broodMin}hp TTK≈{broodMinTtk:F1}s · ratio={ratio:F2} " +
                $"(soft max {ColossalMinPathParityMaxRatio:F2})");
            if (ratio > ColossalMinPathParityMaxRatio)
            {
                Console.WriteLine(
                    $"WARN colossal: min-path HP ratio {ratio:F2} > " +
                    $"{ColossalMinPathParityMaxRatio:F2} — broodmother is intentionally " +
                    "harder (3 sac gates + regen); not a hard fail (§7).");
            }
            else
            {
                Console.WriteLine("  parity: min-path within soft band.");
            }

            // Feel-difficulty note (subtractive vs additive) — always printed.
            Console.WriteLine(
                "  feel: leviathan is subtractive (kill shield→core min path); " +
                "broodmother is additive (sac gates + tentacle regen 20s + " +
                "3× spawn). Same total/core HP keeps ST melt parity; " +
                "time-pressure favors leviathan if player stalls on brood.");
        }

        // Normal generation must never surface colossal IDs (hidden-only).
        // Core skips Parts.Count>0 in GenerateCore; content stageMin=5 is belt+suspenders.
        failures += CheckColossalExcludedFromNormalGen(generator);

        if (failures == 0)
            Console.WriteLine("PASS: colossal boss catalog / TTK / spawn / generate.");
        return failures;
    }

    static int CheckOneColossalBoss(StageBossTemplate boss, GameDataSet data)
    {
        int failures = 0;
        string id = boss.BossId;

        if (boss.Parts == null || boss.Parts.Count == 0)
        {
            Console.WriteLine($"FAIL colossal: '{id}' has no parts.");
            return 1;
        }

        if (boss.MaxHp != ColossalTotalHp)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' MaxHp={boss.MaxHp} expected {ColossalTotalHp}.");
            failures++;
        }

        long partSum = 0;
        int coreCount = 0;
        int coreHp = 0;
        int gateHp = 0;
        int spawnSacs = 0;
        int spawnInterval = 0;
        string spawnEnemyId = null;
        for (int i = 0; i < boss.Parts.Count; i++)
        {
            BossPartDefinition part = boss.Parts[i];
            partSum += part.MaxHp;
            if (part.IsCore)
            {
                coreCount++;
                coreHp = part.MaxHp;
                for (int g = 0; g < part.CoreGatePartIds.Count; g++)
                {
                    string gateId = part.CoreGatePartIds[g];
                    for (int j = 0; j < boss.Parts.Count; j++)
                        if (string.Equals(
                                boss.Parts[j].PartId,
                                gateId,
                                StringComparison.Ordinal))
                            gateHp += boss.Parts[j].MaxHp;
                }
            }

            if (part.Attack != null
                && part.Attack.Type == BossPartAttackType.SpawnEnemy)
            {
                spawnSacs++;
                if (spawnInterval == 0)
                    spawnInterval = part.Attack.IntervalTicks;
                else if (spawnInterval != part.Attack.IntervalTicks)
                {
                    Console.WriteLine(
                        $"WARN colossal: '{id}' sac intervals differ " +
                        $"({spawnInterval} vs {part.Attack.IntervalTicks}).");
                }

                spawnEnemyId = part.Attack.SpawnEnemyId;
            }
        }

        if (partSum != boss.MaxHp)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' parts sum {partSum} != MaxHp {boss.MaxHp}.");
            failures++;
        }

        if (coreCount != 1)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' coreCount={coreCount} (need 1).");
            failures++;
        }
        else if (coreHp != ColossalCoreHp)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' coreHp={coreHp} expected {ColossalCoreHp}.");
            failures++;
        }

        double ttkTotal = boss.MaxHp / ColossalExpectedDps;
        double ttkCore = coreHp / ColossalExpectedDps;
        double ttkFullEff = boss.MaxHp / ColossalFullPowerEffectiveDps;
        double ttkFullRaw = boss.MaxHp / BossFullPowerDps;
        bool midOk = ttkTotal >= ColossalTtkExpectedMin
            && ttkTotal <= ColossalTtkExpectedMax;
        bool fullOk = ttkFullEff >= ColossalTtkFullMin;

        Console.WriteLine(
            $"  {id,-20} parts={boss.Parts.Count} sum={partSum} core={coreHp} " +
            $"gatesHp={gateHp} stage={boss.StageIndexMin}-{boss.StageIndexMax} " +
            $"diff={boss.DifficultyMin}-{boss.DifficultyMax} " +
            $"theme={NullLabel(boss.ThemeId)}");
        Console.WriteLine(
            $"    total @ {ColossalExpectedDps:F0} DPS → TTK={ttkTotal:F1}s " +
            $"[{(midOk ? "midOK" : "OUT")}]  core-only TTK={ttkCore:F1}s  " +
            $"full-eff@{ColossalFullPowerEffectiveDps:F0} → {ttkFullEff:F1}s " +
            $"[{(fullOk ? "floorOK" : "BELOW")}]  raw-full@{BossFullPowerDps:F0} → " +
            $"{ttkFullRaw:F1}s (info)");

        if (!midOk)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' total TTK {ttkTotal:F1}s outside " +
                $"[{ColossalTtkExpectedMin:F0},{ColossalTtkExpectedMax:F0}]s.");
            failures++;
        }

        if (!fullOk)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' full-eff TTK {ttkFullEff:F1}s " +
                $"< {ColossalTtkFullMin:F0}s.");
            failures++;
        }

        // Hidden-only content contract: stage range starts at hidden generation index.
        if (boss.StageIndexMin < 5)
        {
            Console.WriteLine(
                $"FAIL colossal: '{id}' stageIndexMin={boss.StageIndexMin} " +
                "must be ≥5 (hidden path uses stage 5; lowers stage collision).");
            failures++;
        }

        if (boss.ThemeId != null)
        {
            Console.WriteLine(
                $"WARN colossal: '{id}' theme={boss.ThemeId} — prefer null theme " +
                "so ThemeIds list stays 5 biomes; exclusivity needs Core filter.");
        }

        // Broodmother-specific spawn pressure.
        if (string.Equals(
                id,
                SegmentStageGenerator.BroodmotherBossId,
                StringComparison.Ordinal))
        {
            if (spawnSacs < 1 || spawnInterval < 1)
            {
                Console.WriteLine(
                    $"FAIL colossal: '{id}' needs spawnEnemy sacs with interval.");
                failures++;
            }
            else
            {
                if (spawnEnemyId == null
                    || data.BattleContent.FindEnemy(spawnEnemyId) == null)
                {
                    Console.WriteLine(
                        $"FAIL colossal: '{id}' spawnEnemyId '{spawnEnemyId}' unknown.");
                    failures++;
                }

                double intervalSec = spawnInterval / (double)SimSpace.TicksPerSecond;
                int peakIfNoKill = (int)Math.Ceiling(
                    spawnSacs * (ColossalSpawnFightSeconds / intervalSec));
                // Continuous accumulation bound (no despawn/kill): still under pool.
                bool capOk = peakIfNoKill <= ColossalMaxEnemiesCap;
                Console.WriteLine(
                    $"    spawn: sacs={spawnSacs} interval={spawnInterval}t " +
                    $"({intervalSec:F1}s) enemy={spawnEnemyId} · " +
                    $"peak@{ColossalSpawnFightSeconds}s no-kill={peakIfNoKill} " +
                    $"vs MaxEnemies={ColossalMaxEnemiesCap} " +
                    $"[{(capOk ? "capOK" : "OVER")}]");
                if (!capOk)
                {
                    Console.WriteLine(
                        $"FAIL colossal: '{id}' spawn peak {peakIfNoKill} " +
                        $"> MaxEnemies {ColossalMaxEnemiesCap} over " +
                        $"{ColossalSpawnFightSeconds}s (stall overflow).");
                    failures++;
                }
            }
        }

        return failures;
    }

    static int MinPathHp(StageBossTemplate boss)
    {
        int coreHp = 0;
        int gateHp = 0;
        for (int i = 0; i < boss.Parts.Count; i++)
        {
            BossPartDefinition part = boss.Parts[i];
            if (!part.IsCore)
                continue;
            coreHp = part.MaxHp;
            for (int g = 0; g < part.CoreGatePartIds.Count; g++)
            {
                string gateId = part.CoreGatePartIds[g];
                for (int j = 0; j < boss.Parts.Count; j++)
                    if (string.Equals(
                            boss.Parts[j].PartId,
                            gateId,
                            StringComparison.Ordinal))
                        gateHp += boss.Parts[j].MaxHp;
            }
        }
        return coreHp + gateHp;
    }

    static int CheckColossalExcludedFromNormalGen(SegmentStageGenerator generator)
    {
        int failures = 0;
        int hits = 0;
        var hitIds = new HashSet<string>(StringComparer.Ordinal);

        // Stages 1–5 × difficulties 1–5 × seeds — colossal must never be BossId.
        for (int stage = 1; stage <= 5; stage++)
        {
            for (int diff = 1; diff <= 5; diff++)
            {
                for (int seed = 0; seed < ColossalNormalGenSampleSeeds; seed++)
                {
                    StagePlan plan;
                    try
                    {
                        plan = generator.Generate((ulong)seed, stage, diff);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"FAIL colossal: normal Generate({seed},{stage},{diff}) " +
                            $"threw: {ex.Message}");
                        return failures + 1;
                    }

                    if (plan == null || string.IsNullOrEmpty(plan.BossId))
                        continue;
                    for (int i = 0; i < ColossalBossIds.Length; i++)
                    {
                        if (string.Equals(
                                plan.BossId,
                                ColossalBossIds[i],
                                StringComparison.Ordinal))
                        {
                            hits++;
                            hitIds.Add(plan.BossId);
                        }
                    }
                }
            }
        }

        if (hits > 0)
        {
            Console.WriteLine(
                $"FAIL colossal: normal Generate selected colossal {hits} time(s) " +
                $"({string.Join(",", hitIds)}). GenerateCore must skip " +
                "LeviathanBossId/BroodmotherBossId.");
            failures++;
        }
        else
        {
            Console.WriteLine(
                $"  normal-gen sample: stages1–5 × diff1–5 × " +
                $"{ColossalNormalGenSampleSeeds} seeds — no colossal BossId.");
        }

        return failures;
    }

    static int PlanSpawnHp(StagePlan plan, BattleContent content)
    {
        int sum = 0;
        for (int i = 0; i < plan.Segments.Count; i++)
            sum += SpawnListHp(plan.Segments[i].Spawns, content);
        return sum;
    }

    static int SpawnListHp(IReadOnlyList<SpawnEvent> spawns, BattleContent content)
    {
        int sum = 0;
        for (int i = 0; i < spawns.Count; i++)
        {
            EnemyDefinition enemy = content.FindEnemy(spawns[i].EnemyId);
            if (enemy != null)
                sum += enemy.MaxHp;
        }
        return sum;
    }

    static int PlanObstacleCount(StagePlan plan)
    {
        int sum = 0;
        for (int i = 0; i < plan.Segments.Count; i++)
            sum += plan.Segments[i].Obstacles.Count;
        return sum;
    }

    static double PlanCapsuleExpectation(
        StagePlan plan,
        BattleContent content,
        int noDropWeight)
    {
        double sum = 0;
        for (int i = 0; i < plan.Segments.Count; i++)
        {
            sum += SpawnListCapsuleExpectation(
                plan.Segments[i].Spawns,
                content,
                noDropWeight,
                plan.CapsuleDropMultiplierNumerator,
                plan.CapsuleDropMultiplierDenominator);
        }
        return sum;
    }

    static double SegmentCapsuleExpectation(
        StageSegmentTemplate seg,
        BattleContent content,
        int noDropWeight,
        int dropNum,
        int dropDen)
    {
        return SpawnListCapsuleExpectation(
            seg.Spawns, content, noDropWeight, dropNum, dropDen);
    }

    static double SpawnListCapsuleExpectation(
        IReadOnlyList<SpawnEvent> spawns,
        BattleContent content,
        int noDropWeight,
        int dropNum,
        int dropDen)
    {
        double sum = 0;
        for (int i = 0; i < spawns.Count; i++)
        {
            EnemyDefinition enemy = content.FindEnemy(spawns[i].EnemyId);
            if (enemy == null || enemy.DropWeight <= 0)
                continue;
            // Match BattleSim.TryDropCapsule scaled weight: p = sw / (noDrop + sw)
            long scaled = ScalePositiveRatio(
                enemy.DropWeight, dropNum, dropDen);
            sum += scaled / (double)(noDropWeight + scaled);
        }
        return sum;
    }

    static long ScalePositiveRatio(int value, int num, int den)
    {
        if (value <= 0 || num <= 0)
            return 0;
        if (den < 1)
            den = 1;
        return (long)value * num / den;
    }

    static int ScaleInt(int value, int num, int den)
    {
        if (value <= 0)
            return 0;
        if (den < 1)
            den = 1;
        long scaled = (long)value * num / den;
        if (scaled > int.MaxValue)
            return int.MaxValue;
        return (int)scaled;
    }

    sealed class EncounterAgg
    {
        public int Samples;
        public double HpSum;
        public double BossHpSum;
        public double CapsuleSum;
        public int ObstacleSum;
        public int SegmentSum;
        public int BossPresent;
        public int ScoreNum = 1;
        public int ScoreDen = 1;
        public int HpNum = 1;
        public int HpDen = 1;
        public int DropNum = 1;
        public int DropDen = 1;
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
