import json
from pathlib import Path

path = Path("GameData/waves.json")
data = json.loads(path.read_text(encoding="utf-8"))

# --- St3 fortress warship on boss_fortress ---
parts = [
    {
        "id": "engine",
        "offsetX": 8.0,
        "offsetY": 0.0,
        "halfWidth": 2.5,
        "halfHeight": 2.0,
        "hp": 2200,
        "attack": {
            "type": "radialSpread",
            "intervalTicks": 64,
            "ways": 5,
            "bulletSpeed": 9.0,
        },
    },
    {
        "id": "turret_a",
        "offsetX": 4.0,
        "offsetY": 3.5,
        "halfWidth": 1.25,
        "halfHeight": 1.1,
        "hp": 900,
        "attack": {
            "type": "aimedSpread",
            "intervalTicks": 72,
            "ways": 2,
            "bulletSpeed": 10.0,
        },
    },
    {
        "id": "turret_b",
        "offsetX": 4.0,
        "offsetY": -3.5,
        "halfWidth": 1.25,
        "halfHeight": 1.1,
        "hp": 900,
        "attack": {
            "type": "aimedSpread",
            "intervalTicks": 72,
            "ways": 2,
            "bulletSpeed": 10.0,
        },
    },
    {
        "id": "turret_c",
        "offsetX": 0.0,
        "offsetY": 2.0,
        "halfWidth": 1.25,
        "halfHeight": 1.1,
        "hp": 900,
        "attack": {
            "type": "aimedSpread",
            "intervalTicks": 56,
            "ways": 3,
            "bulletSpeed": 9.0,
        },
    },
    {
        "id": "turret_d",
        "offsetX": 0.0,
        "offsetY": -2.0,
        "halfWidth": 1.25,
        "halfHeight": 1.1,
        "hp": 900,
        "attack": {
            "type": "aimedSpread",
            "intervalTicks": 56,
            "ways": 3,
            "bulletSpeed": 9.0,
        },
    },
    {
        "id": "core",
        "offsetX": -6.0,
        "offsetY": 0.0,
        "halfWidth": 2.0,
        "halfHeight": 2.0,
        "hp": 13800,
        "isCore": True,
        "attack": {
            "type": "radialSpread",
            "intervalTicks": 36,
            "ways": 9,
            "bulletSpeed": 11.0,
        },
    },
]
part_hp = sum(p["hp"] for p in parts)
assert part_hp == 19600, part_hp

warship = {
    "id": "fortress_warship",
    "eventEntityId": 110,
    "warningTicks": 180,
    "originX": 24.0,
    "originY": 0.0,
    "scrollSpeedPerSecond": 3.0,
    "baseCoreOpeningWays": 9,
    "waysReductionPerTurret": 2,
    "minimumCoreOpeningWays": 3,
    "groups": [
        {"id": "stern", "role": "midbossGate", "partIds": ["engine"]},
        {
            "id": "hull",
            "role": "attritionLine",
            "partIds": ["turret_a", "turret_b", "turret_c", "turret_d"],
            "advanceAfterTicks": 720,
        },
        {"id": "bow", "role": "finalCore", "partIds": ["core"]},
    ],
}

found = False
for boss in data["bosses"]:
    if boss.get("id") == "boss_fortress":
        found = True
        # Keep phase vocabulary for non-warship tooling / presentation fallback,
        # but multipart+warship is the live St3 climax contract.
        boss["hp"] = part_hp
        boss["halfWidth"] = 10.0
        boss["halfHeight"] = 5.0
        boss["parts"] = parts
        boss["warship"] = warship
        # Drop single-body phase ladder — warship part attacks carry the fight.
        # BalanceSim treats warship bosses separately from 3-phase standard bosses.
        if "phases" in boss:
            del boss["phases"]
        if "phaseHpThresholds" in boss:
            del boss["phaseHpThresholds"]
        break
assert found, "boss_fortress not found"

# --- Fortress late-half fodder thin (warship is the climax; keep turret vocabulary) ---
# Remove low-priority interceptor/zako fodder from dense late fortress segs only.
# Target ~20-30% spawn cut on late (diffMin>=3) non-cleanKill segs.
fodder_ids = {"interceptor_rush", "zako_tank", "scrap_tumbler", "zako_straight"}
late_fortress = {
    "seg_fortress_drone_lattice",
    "seg_fortress_armored_gate",
    "seg_fortress_crossfire_alley",
}
removed = {}
for seg in data["segments"]:
    sid = seg.get("id")
    if sid not in late_fortress:
        continue
    spawns = seg.get("spawns", [])
    # Drop every other fodder spawn (keep structure, turrets, laser_sentry, mortar, elite)
    keep = []
    fodder_seen = 0
    for sp in spawns:
        eid = sp.get("enemyId", "")
        if eid in fodder_ids:
            fodder_seen += 1
            if fodder_seen % 2 == 0:
                continue  # drop alternate fodder
        keep.append(sp)
    removed[sid] = len(spawns) - len(keep)
    seg["spawns"] = keep
    intent = seg.get("intent", "")
    if "REQ111" not in intent:
        seg["intent"] = (intent + " | REQ111 warship-climax fodder-thin").strip(" |")

# --- St5 core late: slight density UP only on non-cleanKill late segs if headroom ---
# Ghost L1 is ~75 DPS (~7% of St5 reach). Full-run bonus only — do NOT rely on it.
# Decision: keep core density (no uptick). Mark intent for audit report.
for seg in data["segments"]:
    if seg.get("theme") != "core":
        continue
    if seg.get("difficultyMin", 1) < 3:
        continue
    if "clean_kill" in seg.get("id", ""):
        continue
    intent = seg.get("intent", "")
    if "REQ111" not in intent:
        seg["intent"] = (intent + " | REQ111 ghost-window density held (ghost bonus-only)").strip(" |")

path.write_text(
    json.dumps(data, ensure_ascii=False, indent=2) + "\n",
    encoding="utf-8",
)
print("part_hp", part_hp)
print("fodder removed", removed)
print("boss_fortress keys", sorted(next(b for b in data["bosses"] if b["id"]=="boss_fortress").keys()))
