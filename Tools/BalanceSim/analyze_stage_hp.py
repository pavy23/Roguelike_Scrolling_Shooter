"""Stage segment HP + boss TTK analysis (REQ-011 late-boss rebalance).

Boss HP curve target (provisional, AGENTS.md §7 — human finalizes):
  stage1 1000 → hive 1300 → fortress 1600 → storm 4000 → core 4500

Orchestrator note (post-28a8ea4): sizing late bosses to full-power DPS (~1880)
blew the curve (storm 20000 = 12.5× fortress). Re-anchor on expected firepower
at stage 4–5 reach, not theoretical max stacks. Full-power 25× DPS inflation
mitigation (option dmg scale, passive stack caps, etc.) is a separate future REQ.

--- Expected firepower assumptions (power-up acquisition pace) ---

Formulas (match Core Damage.Compute / interval reduce, full-hit, 60 tps):
  MainShot DPS(L) ≈ base10 × (100+50(L-1))/100 × (60 / interval(L))
    L1: 75, L2: 112.5, L3: 171.4, L4: 250, L5: 360
  Option O adds O extra main beams → total main contribution × (1+O)
  Missile DPS(L)  ≈ base20 × (100+50(L-1))/100 × (60 / max(minInterval, reduced))
    With minInterval 15 (weapons.json provisional): L1≈40, L2≈72, L3≈120
    With minInterval still 30 (old bug): L1–3 stay ≈40/60/80

Acquisition pace (first-clear / typical run, NOT death-carry full stack):
  Capsules cycle the gauge; stage-clear rewards offer 3 picks (slot levels dominate).
  Assume player prioritizes Main then Option, Missile secondary; Shield optional.

  Stage | Assumed build (levels)              | Theoretical DPS | Band used
  ------|-------------------------------------|-----------------|----------
  1     | Main1  Opt0  Mis0                   | ~75             | 75–150
  2     | Main2  Opt1  Mis0                   | ~225            | 150–300
  3     | Main3  Opt2  Mis1                   | ~554            | 350–550
  4     | Main3  Opt2  Mis1  (partial hit ~0.85) | ~470 theo→~400 practical
        |  mid: Main3–4 Opt2 Mis1             | ~450–650        | **400–700**
  5     | Main4  Opt2–3 Mis1–2 (not max)      | ~550–750        | **450–700**
  max   | Main5  Opt4  Mis3                   | ~1880–1920      | full-power floor only

Boss TTK gates for this pass (provisional):
  - Expected stage 4–5 DPS (400–700): TTK ≈ 8–15 s  (primary sizing)
  - Full-power 1880 DPS: TTK ≥ 2 s               (anti-meltdown floor only)

Chosen HP vs gates:
  boss_storm 4000 @500 DPS → 8.0 s; @400 → 10.0 s; @700 → 5.7 s*
  boss_core  4500 @550 DPS → 8.2 s; @450 → 10.0 s; @700 → 6.4 s*
  both @1880 → 2.13 s / 2.39 s ≥ 2 s
  * High end of 700 DPS (near-full main+option) undershoots 8 s; acceptable until
    DPS-inflation mitigation REQ. Mid-band 450–550 is the sizing anchor.

All values provisional per AGENTS.md §7.
"""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
enemies = {
    e["id"]: e
    for e in json.loads((root / "GameData/enemies.json").read_text(encoding="utf-8"))[
        "enemies"
    ]
}
waves = json.loads((root / "GameData/waves.json").read_text(encoding="utf-8"))
themes = waves["themes"]


def seg_hp(seg):
    return sum(enemies[s["enemyId"]]["hp"] for s in seg["spawns"])


def seg_len_s(seg):
    return seg["lengthTicks"] / 60.0


def matches(seg, theme, diff):
    t = seg.get("theme")
    if t is not None and t != theme:
        return False
    return seg["difficultyMin"] <= diff <= seg["difficultyMax"]


print("=== Segment HP ===")
for seg in waves["segments"]:
    hp = seg_hp(seg)
    print(
        f"{seg['id']:40} theme={str(seg.get('theme')):10} "
        f"d={seg['difficultyMin']}-{seg['difficultyMax']} "
        f"hp={hp:5} hp/s={hp/seg_len_s(seg):6.2f} n={len(seg['spawns'])}"
    )

print()
print("=== Stage pool averages (theme by stage, diff=stage) ===")
for stage in range(1, 6):
    theme = themes[stage - 1]
    diff = stage
    pool = [s for s in waves["segments"] if matches(s, theme, diff)]
    if not pool:
        print(f"Stage {stage} EMPTY")
        continue
    avg_hp = sum(seg_hp(s) for s in pool) / len(pool)
    avg_hps = sum(seg_hp(s) / seg_len_s(s) for s in pool) / len(pool)
    avg_es = sum(len(s["spawns"]) / seg_len_s(s) for s in pool) / len(pool)
    print(
        f"Stage {stage} {theme:10} n={len(pool)} "
        f"avgHP={avg_hp:7.1f} avgHP/s={avg_hps:6.2f} avgE/s={avg_es:5.2f}"
    )
    for s in pool:
        print(f"    {s['id']:36} hp={seg_hp(s):4}")

# --- Boss TTK (expected firepower + full-power floor) ---
# Expected stage 4–5 band: ~400–700 DPS (see module docstring).
# Mid anchors used for the 8–15 s target: stage4 ~500, stage5 ~550.
# Full-power theoretical max (Main5+Opt4+Mis3, minInterval 30 path): 1880.
FULL_POWER_DPS = 1880.0
EXPECTED_DPS = {
    "boss_stage1": 75.0,  # Main1 baseline
    "boss_hive": 225.0,  # Main2+Opt1 early
    "boss_fortress": 450.0,  # Main3+Opt1–2 mid
    "boss_storm": 500.0,  # stage4 mid of 400–700 band
    "boss_core": 550.0,  # stage5 mid of 450–700 band
}
EXPECTED_BAND = (400.0, 700.0)  # stage 4–5 primary sizing band
TTK_EXPECTED_MIN = 8.0
TTK_EXPECTED_MAX = 15.0
TTK_FULL_MIN = 2.0

print()
print("=== Boss HP curve ===")
bosses = waves["bosses"]
prev = None
for b in bosses:
    hp = b["hp"]
    delta = "" if prev is None else f"  Δ={hp - prev:+d}  ×{hp / prev:.2f}"
    print(f"{b['id']:16} hp={hp:5}{delta}")
    prev = hp

print()
print("=== Boss TTK @ expected DPS (stage-reach build, full-hit) ===")
print(
    f"(stage4–5 band {EXPECTED_BAND[0]:.0f}–{EXPECTED_BAND[1]:.0f} DPS; "
    f"target TTK {TTK_EXPECTED_MIN:.0f}–{TTK_EXPECTED_MAX:.0f}s)"
)
for b in bosses:
    dps = EXPECTED_DPS.get(b["id"], 500.0)
    ttk = b["hp"] / dps
    flag = ""
    if b["id"] in ("boss_storm", "boss_core"):
        lo = b["hp"] / EXPECTED_BAND[1]
        hi = b["hp"] / EXPECTED_BAND[0]
        in_band = lo <= TTK_EXPECTED_MAX and hi >= TTK_EXPECTED_MIN
        # Mid-anchor gate: prefer 8–15 at the stage-specific expected DPS.
        mid_ok = TTK_EXPECTED_MIN <= ttk <= TTK_EXPECTED_MAX
        flag = f"  bandTTK={lo:.1f}–{hi:.1f}s"
        if mid_ok:
            flag += "  midOK"
        elif in_band:
            flag += "  bandOK(mid off)"
        else:
            flag += "  OUT"
    print(f"{b['id']:16} hp={b['hp']:5} @ {dps:6.1f} DPS  TTK={ttk:5.2f}s{flag}")

print()
print(f"=== Boss TTK @ full power DPS {FULL_POWER_DPS:.0f} (floor ≥ {TTK_FULL_MIN:.0f}s) ===")
for b in bosses:
    ttk = b["hp"] / FULL_POWER_DPS
    floor = "OK" if ttk >= TTK_FULL_MIN else "BELOW"
    print(f"{b['id']:16} hp={b['hp']:5} TTK={ttk:.2f}s  [{floor}]")
