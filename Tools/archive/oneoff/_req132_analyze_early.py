#!/usr/bin/env python3
"""Extra early-seg density checks for REQ-132."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
waves = json.loads((ROOT / "GameData/waves.json").read_text(encoding="utf-8"))

# lifetime estimate: spawnX 21 -> despawn -22 = 43u
# effective speed ~ scroll 5 + move
LIFE = {
    "junk_roller": int(43 / (5 + 3.5) * 60),   # ~303
    "scrap_tumbler": int(43 / (5 + 3.75) * 60),  # ~295
}
JR_IV, ST_IV = 180, 150

def density(seg, jr_iv=JR_IV, st_iv=ST_IV):
    fires = []
    counts = {"junk_roller": 0, "scrap_tumbler": 0}
    for sp in seg.get("spawns", []):
        eid = sp.get("enemyId")
        if eid not in counts:
            continue
        counts[eid] += 1
        iv = jr_iv if eid == "junk_roller" else st_iv
        life = LIFE[eid]
        t0 = int(sp["tick"])
        # Core: age starts 1, fires when age % iv == 0 → first at age==iv
        age = iv
        while age <= life:
            fires.append(t0 + age)
            age += iv
    fires.sort()
    peak = 0
    for i, t in enumerate(fires):
        j = i
        while j < len(fires) and fires[j] < t + 60:
            j += 1
        peak = max(peak, j - i)
    return counts, len(fires), peak, fires


print("life JR", LIFE["junk_roller"], "ST", LIFE["scrap_tumbler"])
print()
for seg in waves["segments"]:
    if "junk_roller" not in str(seg.get("spawns")) and "scrap_tumbler" not in str(seg.get("spawns")):
        # cheap filter
        eids = {sp.get("enemyId") for sp in seg.get("spawns", [])}
        if not eids & {"junk_roller", "scrap_tumbler"}:
            continue
    counts, total, peak, fires = density(seg)
    theme = seg.get("theme", "?")
    d = f"{seg.get('difficultyMin')}-{seg.get('difficultyMax')}"
    print(f"{seg['id']:40} {theme:10} d={d:5} JR={counts['junk_roller']:2} ST={counts['scrap_tumbler']:2} "
          f"shots={total:2} peak1s={peak}")
    if peak >= 4:
        # show fire clusters
        print(f"  fires: {fires[:20]}{'...' if len(fires)>20 else ''}")
