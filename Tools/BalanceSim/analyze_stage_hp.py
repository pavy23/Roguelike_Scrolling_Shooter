"""One-shot stage segment HP analysis for REQ-011 balance fix."""
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

print()
bosses = waves["bosses"]
print("=== Boss TTK at full power DPS 1880 ===")
dps = 1880.0
for b in bosses:
    print(f"{b['id']:16} hp={b['hp']:5} TTK={b['hp']/dps:.2f}s")
