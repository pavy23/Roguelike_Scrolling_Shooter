#!/usr/bin/env python3
"""REQ-131: inspect early segments for stages 1-3 (scrapyard/hive/fortress)."""
from __future__ import annotations

import json
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
data = json.loads((ROOT / "GameData" / "waves.json").read_text(encoding="utf-8"))
enemies = {
    e["id"]: e
    for e in json.loads((ROOT / "GameData" / "enemies.json").read_text(encoding="utf-8"))[
        "enemies"
    ]
}

print("schemaVersion", data["schemaVersion"])
print("themes", data["themes"])
print("segmentsPerStage", data["segmentsPerStage"])
print("closingSegmentsPerStage", data["closingSegmentsPerStage"])
print("startLaneMask", data["startLaneMask"])
print("scrollSpeed", data["scrollSpeed"], "spawnX", data["spawnX"])
print("laneCount", data["laneCount"])
print()

TARGET = ("scrapyard", "hive", "fortress")


def is_theme(s, theme):
    return (
        s.get("theme") == theme
        or theme in s.get("id", "")
        or f"theme={theme}" in s.get("intent", "")
    )


for theme in TARGET:
    segs = [s for s in data["segments"] if is_theme(s, theme)]
    print(f"=== {theme} ({len(segs)} segs) ===")
    for s in sorted(segs, key=lambda x: (x.get("difficultyMin", 0), x["id"])):
        nsp = len(s.get("spawns") or [])
        nob = len(s.get("obstacles") or [])
        eids = Counter(sp["enemyId"] for sp in (s.get("spawns") or []))
        ticks = [sp["tick"] for sp in (s.get("spawns") or [])]
        ys = sorted({sp["y"] for sp in (s.get("spawns") or [])})
        tmin = min(ticks) if ticks else None
        tmax = max(ticks) if ticks else None
        print(
            f"  {s['id']}\n"
            f"    d={s.get('difficultyMin')}-{s.get('difficultyMax')} "
            f"len={s.get('lengthTicks')} weight={s.get('weight')} "
            f"spawns={nsp} obs={nob}"
        )
        print(
            f"    masks entry={s.get('entryLaneMask')} exit={s.get('exitLaneMask')} "
            f"trav={s.get('traversableLaneMasks')}"
        )
        print(f"    ticks {tmin}..{tmax} ys={ys}")
        print(f"    enemies={dict(eids)}")
        print(f"    intent={s.get('intent', '')[:100]}")
    print()

# Early candidates: difficultyMax <= 2 (first-half friendly) or difficultyMin <= 1
print("=== EARLY CANDIDATES (dMax<=2 OR intent early-open, theme in 1-3) ===")
for s in data["segments"]:
    theme = s.get("theme")
    if theme not in TARGET:
        continue
    early = (
        int(s.get("difficultyMax", 5)) <= 2
        or "early-open" in s.get("intent", "")
        or int(s.get("difficultyMin", 5)) <= 1
    )
    if not early:
        continue
    nsp = len(s.get("spawns") or [])
    eids = Counter(sp["enemyId"] for sp in (s.get("spawns") or []))
    ticks = [sp["tick"] for sp in (s.get("spawns") or [])]
    print(
        f"{s['id']:40} theme={theme} d={s.get('difficultyMin')}-{s.get('difficultyMax')} "
        f"len={s.get('lengthTicks')} spawns={nsp} t={min(ticks) if ticks else '-'}"
        f"..{max(ticks) if ticks else '-'} {dict(eids)}"
    )

# Fodder enemy list suitable for formations
print("\n=== FODDER-LIKE ENEMIES (hp low / no laser / no boss) ===")
for e in sorted(enemies.values(), key=lambda x: x["id"]):
    eid = e["id"]
    hp = e.get("hp", "?")
    fi = e.get("fireIntervalTicks", 0)
    laser = e.get("laser") is not None
    role = e.get("role") or e.get("tier") or ""
    # skip obvious bosses/mids
    if any(k in eid for k in ("boss", "mid", "core", "capital")):
        continue
    if laser:
        continue
    print(f"  {eid:24} hp={hp} fire={fi} role={role} size={e.get('size')} move={e.get('movePattern') or e.get('path')}")
