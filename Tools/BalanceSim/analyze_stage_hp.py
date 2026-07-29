"""Stage segment HP + boss TTK analysis (REQ-033 boss redesign).

Boss HP curve target (provisional, AGENTS.md §7 — human finalizes):
  stage1 24000 → hive 28000 → fortress 32000 → storm 38000 → core 45000
  3 phases each (aimed → spread → rapid). Equal-split HP thresholds:
  remaining 2/3 → phase1, remaining 1/3 → phase2 (Core equal-N split).

--- Expected firepower (biome: 6 rooms then boss event) ---

Formulas (match Core Damage.Compute / interval reduce, full-hit, 60 tps):
  MainShot DPS(L) ≈ base10 × (100+50(L-1))/100 × (60 / interval(L))
    L1: 75, L2: 112.5, L3: 171.4, L4: 250, L5: 360
  Option O adds O extra main beams → total main contribution × (1+O)
  Missile DPS(L)  ≈ base20 × (100+50(L-1))/100 × (60 / max(minInterval, reduced))
    With minInterval 15 (weapons.json provisional): L1≈40, L2≈72, L3≈120

Acquisition pace after 6 rooms + stage rewards (NOT death-carry full stack):
  Capsules cycle the gauge; room/stage rewards offer slot levels.
  Mid firepower measured ~500 DPS even on early bosses (old HP melted in 2–3s).

  Boss order | Assumed build (levels)           | Theoretical DPS | Band used
  -----------|----------------------------------|-----------------|----------
  stage1     | Main3 Opt1–2 Mis0–1              | ~340–550        | **450–650** mid **550**
  hive       | Main3 Opt2 Mis1                  | ~554            | **550–750** mid **650**
  fortress   | Main4 Opt2 Mis1–2                | ~650–850        | **650–850** mid **750**
  storm      | Main4 Opt3 Mis2                  | ~850–1050       | **800–1000** mid **900**
  core       | Main5 Opt3 Mis2 (not always max) | ~1000–1400      | **950–1200** mid **1050**
  max        | Main5 Opt4 Mis3                  | ~1880–1920      | full-power floor only

Boss TTK gates for REQ-033 (provisional):
  - Expected biome-reach DPS: TTK **35–45 s**  (primary sizing)
  - Full-power ~1880 DPS: TTK **≥ 12 s**       (anti-instant melt floor)

Chosen HP vs gates (mid anchor → TTK; full @1880):
  boss_stage1  24000 @550 → 43.6 s; @1880 → 12.8 s
  boss_hive    28000 @650 → 43.1 s; @1880 → 14.9 s
  boss_fortress 32000 @750 → 42.7 s; @1880 → 17.0 s
  boss_storm   38000 @900 → 42.2 s; @1880 → 20.2 s
  boss_core    45000 @1050 → 42.9 s; @1880 → 23.9 s

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


def phase_threat(phase):
    """Threat proxy: ways * bulletSpeed / fireIntervalTicks (higher = denser/faster)."""
    return phase["ways"] * float(phase["bulletSpeed"]) / phase["fireIntervalTicks"]


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
FULL_POWER_DPS = 1880.0
EXPECTED_DPS = {
    "boss_stage1": 550.0,
    "boss_hive": 650.0,
    "boss_fortress": 750.0,
    "boss_storm": 900.0,
    "boss_core": 1050.0,
}
TTK_EXPECTED_MIN = 35.0
TTK_EXPECTED_MAX = 45.0
TTK_FULL_MIN = 12.0
PHASE_COUNT = 3

print()
print("=== Boss HP curve (REQ-033) ===")
bosses = waves["bosses"]
prev = None
for b in bosses:
    hp = b["hp"]
    delta = "" if prev is None else f"  Δ={hp - prev:+d}  ×{hp / prev:.2f}"
    mono = "" if prev is None else ("  monoOK" if hp > prev else "  MONO FAIL")
    print(f"{b['id']:16} hp={hp:6}{delta}{mono}")
    prev = hp

print()
print(
    f"=== Boss TTK @ expected DPS (biome-reach, full-hit) "
    f"target {TTK_EXPECTED_MIN:.0f}–{TTK_EXPECTED_MAX:.0f}s ==="
)
for b in bosses:
    dps = EXPECTED_DPS.get(b["id"], 500.0)
    ttk = b["hp"] / dps
    mid_ok = TTK_EXPECTED_MIN <= ttk <= TTK_EXPECTED_MAX
    flag = "  midOK" if mid_ok else "  OUT"
    print(f"{b['id']:16} hp={b['hp']:6} @ {dps:6.1f} DPS  TTK={ttk:5.2f}s{flag}")

print()
print(f"=== Boss TTK @ full power DPS {FULL_POWER_DPS:.0f} (floor ≥ {TTK_FULL_MIN:.0f}s) ===")
for b in bosses:
    ttk = b["hp"] / FULL_POWER_DPS
    floor = "OK" if ttk >= TTK_FULL_MIN else "BELOW"
    print(f"{b['id']:16} hp={b['hp']:6} TTK={ttk:.2f}s  [{floor}]")

print()
print("=== Boss phases (pattern / equal-split thresholds) ===")
for b in bosses:
    phases = b["phases"]
    thresholds = b.get("phaseHpThresholds", [])
    print(
        f"{b['id']:16} phases={len(phases)} "
        f"phaseHpThresholds={thresholds} "
        f"(Core equal-split remaining {PHASE_COUNT-1}/{PHASE_COUNT} … 1/{PHASE_COUNT})"
    )
    prev_t = None
    for i, p in enumerate(phases):
        threat = phase_threat(p)
        mono = "" if prev_t is None else (" monoOK" if threat > prev_t else " MONO FAIL")
        print(
            f"  p{i} pattern={p.get('pattern', '?'):8} "
            f"hpEnter={p.get('hpEnterRatio', '?'):} "
            f"int={p['fireIntervalTicks']:3}t ways={p['ways']} "
            f"spd={p['bulletSpeed']}  threat={threat:.3f}{mono}"
        )
        prev_t = threat
