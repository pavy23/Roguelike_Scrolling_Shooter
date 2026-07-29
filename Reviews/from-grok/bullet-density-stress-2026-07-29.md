# Bullet density stress — 2026-07-29 (GROK)

Stage 5 (core) worst-case enemy packing + full-power player bullets vs Core
`MaxEnemyBullets` / `MaxBullets`. **No numeric changes applied** (caps are CODEX-owned).

Runner: `Tools/BalanceSim` → `CheckBulletDensityStress`.

## Caps (BattleSimConfig.CreateDefault)

| Cap | Value |
|---|---:|
| MaxEnemyBullets | 32 |
| MaxBullets | 64 |

Headroom guide: +25% above peak for recommend floor.

## Enemy (stage 5 / core)

| Metric | Value |
|---|---:|
| Worst segment | seg_core_final_gauntlet |
| Peak enemies (no-kill) | 17 |
| Peak shooters (no-kill) | 13 |
| boss_core phase2 | 34t / 9-way / 12.5 u/s |
| Boss p2 theo concurrent | 45 |
| Faithful theo (fodder 1-way + boss p2) | 93 |
| Stress theo (all 9-way + boss p2) | 477 |
| Sim gen peak (cap 512) | 31 |
| Sim densest-core peak | 31 |
| Sim boss-only phase2 hold | **41** |

### Headroom vs MaxEnemyBullets=32

| Case | Peak | Headroom |
|---|---:|---:|
| Boss p2 theo | 45 | **-40.6% OVER** |
| Boss-only sim p2 | 41 | **-28.1% OVER** |
| Gen/worst-core sim | 31 | +3.1% OK |
| Faithful packing bound | 93 | **-190.6% OVER** |

### Recommendations (not applied)

1. **CODEX:** raise `MaxEnemyBullets` to **>= 57** (primary: boss p2 floor 45 + 25%).
2. **CODEX (upper):** **>= 117** if residual core turrets can co-fire with phase 2.
3. **GROK waves.json (optional instead of cap raise):**
   - `boss_core` phase2: ways 9→7 and/or interval 34→45
   - thin concurrent shooters in `seg_core_final_gauntlet` / `seg_core_guardian_wall`
4. Extreme 9-way-on-all-fodder bound (>= 597) only if Core ever multi-ways regular enemies.

## Player (full power)

| Metric | Value |
|---|---:|
| Levels | Main5 / Missile3 / Option4 |
| Main interval / beams | 5t / 5 |
| No-mod theo concurrent | 116 |
| Pierce+ricochet soft uplift theo | ~235 |
| Sim peak (cap 512, pierce+ricochet pack) | **106** |

### Headroom vs MaxBullets=64

| Case | Peak | Headroom |
|---|---:|---:|
| No-mod theo | 116 | **-81.2% OVER** |
| Sim peak | 106 | **-65.6% OVER** |
| Pierce+ricochet theo | 235 | **-267.2% OVER** |

### Recommendations (not applied)

1. **CODEX:** raise `MaxBullets` to **>= 145** (primary: max(116, 106) + 25%).
2. Soft uplift budget: **>= 294** if pierce+ricochet lifetime packing is fully budgeted.
3. Alternates: lower option max, raise main/missile intervals, keep deterministic near-cap volley drop.

## Verification

- `Tools/CoreStandalone` `dotnet test` — 167/167
- `Tools/BalanceSim` `dotnet run` — PASS (overflow WARN-only)

Also recorded in `Reviews/from-claude/requests.md` (GROK response section).
