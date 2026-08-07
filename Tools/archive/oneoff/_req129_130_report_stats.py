#!/usr/bin/env python3
"""Post-apply stats for REQ-129 + REQ-130 fire profile."""
from __future__ import annotations

import json
from collections import Counter
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parents[2]
data = json.loads((ROOT / "GameData" / "waves.json").read_text(encoding="utf-8"))
enemies = {
    e["id"]: e
    for e in json.loads((ROOT / "GameData" / "enemies.json").read_text(encoding="utf-8"))[
        "enemies"
    ]
}


def is_scrap(s: dict) -> bool:
    return (
        s.get("theme") == "scrapyard"
        or "scrap" in s.get("id", "")
        or "theme=scrapyard" in s.get("intent", "")
    )


def wall_sizes(bs: list) -> list[int]:
    n = len(bs)
    parent = list(range(n))

    def find(a: int) -> int:
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a: int, b: int) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for i in range(n):
        for j in range(i + 1, n):
            if abs(bs[i]["x"] - bs[j]["x"]) <= 1.0:
                union(i, j)
    sizes = []
    for r in range(n):
        m = [i for i in range(n) if find(i) == r]
        if len(m) >= 2:
            sizes.append(len(m))
    return sorted(sizes, reverse=True)


segs = [s for s in data["segments"] if is_scrap(s)]
all_gaps = []
walls = Counter()
segs_w = 0
total = 0
spans = []
print("=== AFTER scrapyard ===")
for s in segs:
    bs = [o for o in (s.get("obstacles") or []) if o.get("type") == "breakable"]
    total += len(bs)
    if not bs:
        continue
    xs = sorted(float(o["x"]) for o in bs)
    gaps = [xs[i + 1] - xs[i] for i in range(len(xs) - 1)]
    all_gaps.extend(gaps)
    spans.append(xs[-1] - xs[0])
    ws = wall_sizes(bs)
    if ws:
        segs_w += 1
        for c in ws:
            walls[c] += 1
    print(
        f"{s['id']}: xs={xs} gaps={[round(g, 2) for g in gaps]} walls={ws or '-'}"
    )

print(
    f"TOTAL br={total} gap min/med/max="
    f"{min(all_gaps):.2f}/{median(all_gaps):.2f}/{max(all_gaps):.2f}"
)
print(
    f"gaps<=1: {sum(1 for g in all_gaps if g <= 1.0)}/{len(all_gaps)} "
    f"walls_segs={segs_w} hist={dict(walls)} span_med={median(spans):.2f}"
)

co = cs = 0
for s in segs:
    h = False
    for o in s.get("obstacles") or []:
        if o.get("type") == "breakable" and o.get("blocksEnemyBullets"):
            co += 1
            h = True
    if h:
        cs += 1
print(f"cover obs={co} segs={cs}")

print("\n=== SCRAP SPAWN FIRE PROFILE ===")
fire = Counter()
for s in segs:
    for sp in s.get("spawns") or []:
        eid = sp["enemyId"]
        fi = enemies[eid].get("fireIntervalTicks", 0)
        fire[(eid, fi)] += 1
for (eid, fi), c in sorted(fire.items(), key=lambda x: -x[1]):
    print(f"  {eid:20} fire={fi:4} count={c}")

print("\n=== ALL ENEMIES fireIntervalTicks ===")
zero = []
nonzero = []
for e in enemies.values():
    fi = e.get("fireIntervalTicks", 0)
    laser = e.get("laser") is not None
    (zero if fi == 0 else nonzero).append((e["id"], fi, laser))
print(f"zero={len(zero)} nonzero={len(nonzero)} total={len(enemies)}")
for eid, fi, laser in sorted(zero):
    tag = " LASER" if laser else ""
    print(f"  0    {eid}{tag}")
for eid, fi, laser in sorted(nonzero, key=lambda x: x[1]):
    print(f"  {fi:4} {eid}")

# stage1 early (diffMax<=2) scrap spawn shooters
print("\n=== EARLY scrap (diffMax<=2) shooters ===")
early_fire = Counter()
early_total = 0
for s in segs:
    if int(s.get("difficultyMax", 5)) > 2:
        continue
    for sp in s.get("spawns") or []:
        early_total += 1
        fi = enemies[sp["enemyId"]].get("fireIntervalTicks", 0)
        if fi > 0:
            early_fire[sp["enemyId"]] += 1
print(f"early spawns={early_total} shooting={sum(early_fire.values())} ids={dict(early_fire)}")

print("\n=== LATE scrap (diffMin>=2) shooters ===")
late_fire = Counter()
late_total = 0
for s in segs:
    if int(s.get("difficultyMin", 1)) < 2:
        continue
    for sp in s.get("spawns") or []:
        late_total += 1
        fi = enemies[sp["enemyId"]].get("fireIntervalTicks", 0)
        if fi > 0:
            late_fire[sp["enemyId"]] += 1
print(f"late spawns={late_total} shooting={sum(late_fire.values())} ids={dict(late_fire)}")

# graze
scoring = json.loads((ROOT / "GameData" / "scoring.json").read_text(encoding="utf-8"))
print("\n=== SCORING GRAZE ===")
print(json.dumps(scoring, indent=2))
