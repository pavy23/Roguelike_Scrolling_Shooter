"""REQ-060 stage clearability table (post-tune)."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
enemies_data = json.loads((root / "GameData/enemies.json").read_text(encoding="utf-8"))
enemies = {e["id"]: e for e in enemies_data["enemies"]}
no_drop = enemies_data["dropTable"]["noDropWeight"]
waves = json.loads((root / "GameData/waves.json").read_text(encoding="utf-8"))
ships = json.loads((root / "GameData/ships.json").read_text(encoding="utf-8"))
starter = next(s for s in ships["ships"] if s["id"] == "starter")
main_l = starter["startingPowerUpLevels"][0]

def main_dps(level):
    wl = max(1, level)
    dmg = 10 * (100 + 50 * (wl - 1)) // 100
    reductions = max(0, level - 1)  # rapidStart=2
    interval = max(4, 8 - reductions)
    return dmg * 60.0 / interval

def matches(seg, theme, diff):
    t = seg.get("theme")
    if t is not None and t != theme:
        return False
    return seg["difficultyMin"] <= diff <= seg["difficultyMax"]

def seg_hp(seg):
    return sum(enemies[s["enemyId"]]["hp"] for s in seg["spawns"])

def seg_n(seg):
    return len(seg["spawns"])

def enemy_drop_p(eid):
    w = enemies[eid]["dropWeight"]
    return w / (no_drop + w)

def seg_cap(seg):
    return sum(enemy_drop_p(s["enemyId"]) for s in seg["spawns"])

starter_dps = main_dps(main_l)
eff = starter_dps * 0.70
print(f"Starter Main L{main_l} DPS={starter_dps:.1f} mid-skill eff={eff:.1f}")
print(f"Shield stocks (maxHp)={starter['maxHp']}  noDrop={no_drop}")
print()
mids = ["mini_horror", "mini_destroyer", "mini_crystal", "mini_walker"]
print("Midboss @ starter eff:")
for m in mids:
    e = enemies[m]
    print(f"  {m:16} hp={e['hp']:4} TTK={e['hp']/eff:5.1f}s")
mid_avg = sum(enemies[m]["hp"] for m in mids) / 4
mid_worst = max(enemies[m]["hp"] for m in mids)
print(f"  avg={mid_avg:.0f} worst={mid_worst}")
print()
bosses = {
    1: "boss_stage1", 2: "boss_hive", 3: "boss_fortress",
    4: "boss_storm", 5: "boss_core",
}
reach = {1: 382, 2: 600, 3: 720, 4: 880, 5: 1050}
themes = waves["themes"]
print(f"{'S':>2} {'theme':10} {'avgSeg':>7} {'OC_HP':>7} {'mid':>5} {'boss':>5} {'total':>7} {'TTK':>5} {'hits':>5} {'capOC':>6}")
for stage in range(1, 6):
    theme = themes[stage - 1]
    pool = [s for s in waves["segments"] if matches(s, theme, stage)]
    tw = sum(s.get("weight", 1) for s in pool)
    avg_hp = sum(seg_hp(s) * s.get("weight", 1) for s in pool) / tw
    avg_n = sum(seg_n(s) * s.get("weight", 1) for s in pool) / tw
    avg_cap = sum(seg_cap(s) * s.get("weight", 1) for s in pool) / tw
    oc = avg_hp * 6
    boss = next(b for b in waves["bosses"] if b["id"] == bosses[stage])
    total = oc + mid_avg + boss["hp"]
    dps = reach[stage]
    full_ttk = total / (dps * 0.70)
    mid_ttk = mid_avg / eff
    boss_ttk = boss["hp"] / (dps * 0.70)
    hits = 0.6 + mid_ttk / 14 + boss_ttk / 20
    print(
        f"{stage:2} {theme:10} {avg_hp:7.0f} {oc:7.0f} {mid_avg:5.0f} "
        f"{boss['hp']:5} {total:7.0f} {full_ttk:5.0f} {hits:5.2f} {avg_cap*6:6.1f}"
    )
print()
print("Stage1 pool:")
pool = [s for s in waves["segments"] if matches(s, "scrapyard", 1)]
for s in pool:
    print(f"  {s['id']:28} w={s.get('weight',1):2} hp={seg_hp(s):4} n={seg_n(s):2} cap={seg_cap(s):.2f}")
