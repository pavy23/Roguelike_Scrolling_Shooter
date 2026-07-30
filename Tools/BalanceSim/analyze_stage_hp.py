"""Stage segment HP + boss TTK analysis (playtest 2026-07-30 boss HP retune #2).

Boss HP curve (provisional, AGENTS.md §7 — human finalizes):
  stage1 9000 → hive 15000 → fortress 19000 → storm 24000 → core 30000
  3 phases each (aimed → spread → rapid). Equal-split HP thresholds:
  remaining 2/3 → phase1, remaining 1/3 → phase2 (Core equal-N split).

First boss is intentionally short (tutorial boss fight). Later bosses stay
gentle and lengthen; do not slash the back half again this cycle.

--- Expected firepower (biome: 4 rooms then boss; CODEX shortening 6→4) ---

Formulas (match Core Damage.Compute / interval reduce, full-hit, 60 tps):
  Gauge level L → weaponLevel = max(1, L); base10 main_shot.
  MainShot DPS(L) ≈ dmg(L) × (60 / interval(L))
    gauge0–1: 75, 2: 112.5, 3: 171.4, 4: 250, 5: 360
  Option O adds O extra main beams → total main contribution × (1+O)
  Missile DPS(L)  ≈ base20 × mult(L) × (60 / max(minInterval, reduced))
    L1≈28.6, L2≈32.4, L3≈37.5 (interval 42/37/32, minInterval 20) — support weapon

Acquisition pace after 4 rooms (NOT death-carry full stack, NOT max power):
  Capsules + room rewards offer slot levels; shorter path than old 6-room model.

  Boss order | Assumed avg build (gauge)     | Theoretical DPS | Band used
  -----------|-------------------------------|-----------------|----------
  stage1     | Main2–3 Opt1 Mis0–1           | ~225–380        | **400–600** mid **500**
  hive       | Main3 Opt1–2 Mis1             | ~380–550        | **500–700** mid **600**
  fortress   | Main3–4 Opt2 Mis1             | ~550–790        | **620–820** mid **720**
  storm      | Main4 Opt2–3 Mis2             | ~820–1070       | **780–980** mid **880**
  core       | Main4–5 Opt3 Mis2 (not max)   | ~1070–1510      | **950–1200** mid **1050**
  max        | Main5 Opt4 Mis3               | ~1880–1920      | full-power floor only

Why mid > pure theoretical low end:
  Large boss hitbox ≈ full-hit; average successful run focuses combat slots;
  not a noob death-spiral and not full-power god run.

Boss TTK gates (playtest: 12000 still long → −25% to 9000 tutorial short):
  - Expected biome-reach DPS: TTK **16–32 s**  (first boss ~18s; later 22–32)
  - Full-power ~1880 DPS: TTK **≥ 4.5 s**      (first-boss floor; later higher)

Chosen HP vs gates (mid anchor → TTK; full @1880):
  boss_stage1    9000 @500  → 18.0 s; @1880 → 4.8 s   (human: 12000 −25%)
  boss_hive     15000 @600  → 25.0 s; @1880 → 8.0 s
  boss_fortress 19000 @720  → 26.4 s; @1880 → 10.1 s
  boss_storm    24000 @880  → 27.3 s; @1880 → 12.8 s
  boss_core     30000 @1050 → 28.6 s; @1880 → 16.0 s

HP mono: stage1→hive jumps (~1.67×) as tutorial→real; thereafter ≈×1.25.

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
    "boss_stage1": 500.0,
    "boss_hive": 600.0,
    "boss_fortress": 720.0,
    "boss_storm": 880.0,
    "boss_core": 1050.0,
}
TTK_EXPECTED_MIN = 22.0
TTK_EXPECTED_MAX = 32.0
TTK_FULL_MIN = 6.0
PHASE_COUNT = 3

print()
print("=== Boss HP curve (4-room avg firepower) ===")
bosses = [b for b in waves["bosses"] if not b.get("parts")]
prev = None
for b in bosses:
    hp = b["hp"]
    delta = "" if prev is None else f"  Δ={hp - prev:+d}  ×{hp / prev:.2f}"
    mono = "" if prev is None else ("  monoOK" if hp > prev else "  MONO FAIL")
    print(f"{b['id']:16} hp={hp:6}{delta}{mono}")
    prev = hp

print()
print(
    f"=== Boss TTK @ expected DPS (4-room avg, full-hit) "
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
    phases = b.get("phases") or []
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
