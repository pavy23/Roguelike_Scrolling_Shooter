"""REQ-137/138 post-apply verification (capsule EV + hive geometry)."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
enemies_data = json.loads((root / "GameData/enemies.json").read_text(encoding="utf-8"))
enemies = {e["id"]: e for e in enemies_data["enemies"]}
no_drop = enemies_data["dropTable"]["noDropWeight"]
waves = json.loads((root / "GameData/waves.json").read_text(encoding="utf-8"))
segs_open = waves["segmentsPerStage"]
segs_close = waves["closingSegmentsPerStage"]
OLD_N = 13


def p(w: int, n: int) -> float:
    return w / (n + w)


def matches(seg, theme, diff) -> bool:
    t = seg.get("theme")
    if t is not None and t != theme:
        return False
    return seg["difficultyMin"] <= diff <= seg["difficultyMax"]


def pool_avg_cap(theme, stage, n) -> float:
    pool = [s for s in waves["segments"] if matches(s, theme, stage)]
    tw = sum(s.get("weight", 1) for s in pool)
    return (
        sum(
            sum(p(enemies[sp["enemyId"]]["dropWeight"], n) for sp in s["spawns"])
            * s.get("weight", 1)
            for s in pool
        )
        / tw
    )


print(f"noDropWeight={no_drop} (was {OLD_N})")
print(f"{'S':>2} {'theme':10} {'avgSeg':>7} {'open':>7} {'o+c':>7} {'ratio':>7}")
total_new = 0.0
total_old = 0.0
themes = waves["themes"]
for stage in range(1, 6):
    theme = themes[stage - 1]
    a = pool_avg_cap(theme, stage, no_drop)
    a0 = pool_avg_cap(theme, stage, OLD_N)
    op = a * segs_open
    oc = a * (segs_open + segs_close)
    oc0 = a0 * (segs_open + segs_close)
    total_new += oc
    total_old += oc0
    print(f"{stage:2} {theme:10} {a:7.3f} {op:7.2f} {oc:7.2f} {oc / oc0:7.3f}")

print(
    f"5-stage open+close EV: {total_old:.1f} -> {total_new:.1f} "
    f"ratio={total_new / total_old:.3f}"
)
excl = 6 * 5 + 3  # Speed/Main/Missile/Option/Shield L6 + one weapon mode L3
print(f"exclusive full cost≈{excl}; run EV={total_new:.0f}")
print(f"stages for exclusive full≈{excl / (total_new / 5):.1f}")

s1_open = pool_avg_cap("scrapyard", 1, no_drop) * segs_open
s1_oc = pool_avg_cap("scrapyard", 1, no_drop) * (segs_open + segs_close)
print(f"S1 open EV={s1_open:.2f} → Main@mid ≈L{min(6, int(s1_open))} (flat-1)")
print(f"S1 open+close EV={s1_oc:.2f} → Main@boss ≈L{min(6, int(s1_oc))}")

# BalanceSim global open-only EV
ws = 0
wc = 0.0
for seg in waves["segments"]:
    w = seg.get("weight", 1)
    cap = sum(p(enemies[s["enemyId"]]["dropWeight"], no_drop) for s in seg["spawns"])
    ws += w
    wc += cap * w
e_seg = wc / ws
print(f"BalanceSim eSeg={e_seg:.3f} eStage_open={e_seg * segs_open:.2f} band=[7,14]")

# Sample drop rates before/after
print("\nsample p = dropW/(noDrop+dropW):")
for w in (2, 4, 5, 7, 15, 26):
    p0 = p(w, OLD_N)
    p1 = p(w, no_drop)
    print(f"  w={w:2} p {p0:.4f} -> {p1:.4f} ratio={p1 / p0:.3f}")

hive = next(b for b in waves["bosses"] if b["id"] == "boss_hive")
print(f"\nhive body half={hive['halfWidth']}×{hive['halfHeight']} hp={hive['hp']}")
psum = sum(part["hp"] for part in hive["parts"])
print(f"parts sum={psum} match_total={psum == hive['hp']}")
for part in hive["parts"]:
    print(
        f"  {part['id']:16} ox={part['offsetX']:+6.2f} oy={part['offsetY']:+6.2f} "
        f"half={part['halfWidth']}×{part['halfHeight']} hp={part['hp']} "
        f"core={part.get('isCore', False)} gate={part.get('coreGatePartIds')}"
    )

# Art alignment check
half_h = hive["halfHeight"]
torso_top = half_h
torso_bottom = torso_top - 10.0
leg = next(p for p in hive["parts"] if p["id"] == "tentacle_left")
leg_cy = leg["offsetY"]
leg_hh = leg["halfHeight"]
print(
    f"\nart check: body y=[{-half_h:.2f},{half_h:.2f}] "
    f"torso=[{torso_bottom:.2f},{torso_top:.2f}] "
    f"leg=[{leg_cy - leg_hh:.2f},{leg_cy + leg_hh:.2f}]"
)
core = next(p for p in hive["parts"] if p.get("isCore"))
print(
    f"core head region y=[{core['offsetY'] - core['halfHeight']:.2f},"
    f"{core['offsetY'] + core['halfHeight']:.2f}] "
    f"(shield dome 6×6 centered ~head)"
)
