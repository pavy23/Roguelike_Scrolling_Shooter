#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
d = json.loads((ROOT / "GameData" / "waves.json").read_text(encoding="utf-8"))

ids = [
    "seg_scrap_debris_line",
    "seg_scrap_pipe_dash",
    "seg_scrap_zigzag_posts",
    "seg_scrap_shard_field",
    "seg_scrap_rail_split",
    "seg_hive_brood_wave",
    "seg_hive_lancer_rush",
    "seg_hive_hornet_dive",
    "seg_hive_spore_cloud",
    "seg_hive_tentacle_posts",
    "seg_fortress_mortar_line",
    "seg_fortress_sentry_grid",
    "seg_fortress_interceptor_assault",
    "seg_fortress_shield_bastion",
]
for sid in ids:
    s = next(x for x in d["segments"] if x["id"] == sid)
    print(
        f"=== {sid} len={s['lengthTicks']} d={s['difficultyMin']}-{s['difficultyMax']} "
        f"spawns={len(s['spawns'])} trav={s.get('traversableLaneMasks')}"
    )
    for sp in s["spawns"]:
        print(f"  t={sp['tick']:4} {sp['enemyId']:20} y={sp['y']}")
    solids = [
        o
        for o in (s.get("obstacles") or [])
        if o.get("type") in ("solid", "breakable")
    ]
    print("  obs:", [(o["type"], o["x"], o["y"]) for o in solids])
    print()

# drop weights of fodder
enemies = {
    e["id"]: e
    for e in json.loads((ROOT / "GameData" / "enemies.json").read_text(encoding="utf-8"))[
        "enemies"
    ]
}
drop = json.loads((ROOT / "GameData" / "enemies.json").read_text(encoding="utf-8"))[
    "dropTable"
]
print("dropTable", drop)
for eid in [
    "junk_roller",
    "rust_skimmer",
    "pipe_rat",
    "spore_drifter",
    "sting_hornet",
    "lancer_dart",
    "interceptor_rush",
    "sentry_drone",
    "zako_straight",
    "zako_fast",
]:
    e = enemies[eid]
    print(
        f"  {eid}: hp={e['hp']} dropW={e.get('dropWeight')} fire={e.get('fireIntervalTicks')} "
        f"move={e.get('movement',{}).get('pattern')} speed={e.get('movement',{}).get('speed')}"
    )
