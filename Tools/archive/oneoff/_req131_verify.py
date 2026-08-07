#!/usr/bin/env python3
"""Verify REQ-131 formations: length margin, lane masks, solid y overlap, patterns."""
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

TARGET_IDS = [
    "seg_scrap_debris_line",
    "seg_scrap_pipe_dash",
    "seg_scrap_zigzag_posts",
    "seg_scrap_shard_field",
    "seg_scrap_rail_split",
    "seg_scrap_center_breach",
    "seg_hive_lancer_rush",
    "seg_hive_hornet_dive",
    "seg_hive_spore_cloud",
    "seg_hive_brood_wave",
    "seg_hive_tentacle_posts",
    "seg_fortress_interceptor_assault",
    "seg_fortress_mortar_line",
    "seg_fortress_sentry_grid",
    "seg_fortress_shield_bastion",
]


def detect_formations(spawns: list[dict], min_n: int = 5, max_dt: int = 12):
    """Find runs of same enemyId with short consecutive tick gaps."""
    by_eid: dict[str, list[dict]] = defaultdict(list)
    for s in sorted(spawns, key=lambda x: x["tick"]):
        by_eid[s["enemyId"]].append(s)
    found = []
    for eid, rows in by_eid.items():
        if len(rows) < min_n:
            continue
        # greedy: build chains where consecutive gap <= max_dt
        chain = [rows[0]]
        chains = []
        for r in rows[1:]:
            if r["tick"] - chain[-1]["tick"] <= max_dt:
                chain.append(r)
            else:
                if len(chain) >= min_n:
                    chains.append(chain)
                chain = [r]
        if len(chain) >= min_n:
            chains.append(chain)
        for c in chains:
            ys = [x["y"] for x in c]
            dts = [c[i + 1]["tick"] - c[i]["tick"] for i in range(len(c) - 1)]
            # classify shape
            unique_y = len(set(ys))
            if unique_y == 1:
                shape = "column"
            else:
                # check monotonic diagonal
                diffs = [ys[i + 1] - ys[i] for i in range(len(ys) - 1)]
                if all(d > 0 for d in diffs) or all(d < 0 for d in diffs):
                    shape = "diagonal"
                else:
                    # V-like: first is center-ish, then pairs
                    shape = "wedge/other"
            found.append(
                {
                    "enemyId": eid,
                    "n": len(c),
                    "t0": c[0]["tick"],
                    "t1": c[-1]["tick"],
                    "dt": dts,
                    "ys": ys,
                    "shape": shape,
                    "fire": enemies[eid].get("fireIntervalTicks", 0),
                    "hp": enemies[eid].get("hp"),
                }
            )
    return found


errs = []
print("=== FORMATION DETECTION ===")
for sid in TARGET_IDS:
    seg = next(s for s in data["segments"] if s["id"] == sid)
    L = seg["lengthTicks"]
    spawns = seg["spawns"]
    mt = max(s["tick"] for s in spawns)
    forms = detect_formations(spawns)
    print(f"\n{sid} len={L} spawns={len(spawns)} maxTick={mt} margin={L - mt}")
    print(f"  trav={seg.get('traversableLaneMasks')} d={seg['difficultyMin']}-{seg['difficultyMax']}")
    if mt >= L:
        errs.append(f"{sid}: tick overflow {mt}>={L}")
    if mt > L - 40:
        errs.append(f"{sid}: tight margin maxTick={mt} len={L}")
    solids = [
        o
        for o in (seg.get("obstacles") or [])
        if o.get("type") == "solid"
    ]
    solid_ys = {float(o["y"]) for o in solids}
    for f in forms:
        print(
            f"  FORM {f['shape']:12} {f['enemyId']:18} n={f['n']} "
            f"t={f['t0']}..{f['t1']} dt={f['dt'][:6]}... ys={f['ys']} "
            f"fire={f['fire']} hp={f['hp']}"
        )
        # solid y collision soft check
        for y in f["ys"]:
            for sy in solid_ys:
                if abs(float(y) - sy) < 0.35:
                    print(f"    !! near solid shelf y={sy} (spawn y={y})")
        if all(m == 2 for m in (seg.get("traversableLaneMasks") or [7])):
            for y in f["ys"]:
                if abs(float(y)) > 2.25:
                    errs.append(f"{sid}: formation y={y} outside mask=2")
    if not forms:
        errs.append(f"{sid}: NO formation detected (n>=5, dt<=12)")

# expected formation counts per theme early pool
print("\n=== EXPECTED STAGE FORMATION HITS ===")
print("segmentsPerStage early =", data["segmentsPerStage"])
for theme, ids in [
    (
        "scrapyard",
        [
            "seg_scrap_debris_line",
            "seg_scrap_pipe_dash",
            "seg_scrap_zigzag_posts",
            "seg_scrap_shard_field",
            "seg_scrap_rail_split",
            "seg_scrap_center_breach",
        ],
    ),
    (
        "hive",
        [
            "seg_hive_lancer_rush",
            "seg_hive_hornet_dive",
            "seg_hive_spore_cloud",
            "seg_hive_brood_wave",
            "seg_hive_tentacle_posts",
        ],
    ),
    (
        "fortress",
        [
            "seg_fortress_interceptor_assault",
            "seg_fortress_mortar_line",
            "seg_fortress_sentry_grid",
            "seg_fortress_shield_bastion",
        ],
    ),
]:
    # weight of form segs vs all theme segs eligible for early
    theme_segs = [s for s in data["segments"] if s.get("theme") == theme]
    form_segs = [s for s in theme_segs if s["id"] in ids]
    w_form = sum(s.get("weight", 1) for s in form_segs)
    # early-ish: dMin <= 2 for scrap, dMin <= 2 for hive? hive starts at 2
    if theme == "scrapyard":
        pool = [
            s
            for s in theme_segs
            if s["difficultyMin"] <= 1 or s["difficultyMax"] <= 2 or "early" in s.get("intent", "")
        ]
        # also center_breach d1-3
        pool_ids = {s["id"] for s in pool}
        for s in theme_segs:
            if s["id"] == "seg_scrap_center_breach":
                pool_ids.add(s["id"])
        pool = [s for s in theme_segs if s["id"] in pool_ids]
    else:
        pool = [s for s in theme_segs if s["difficultyMin"] <= 2 and s["difficultyMax"] >= 2]
        # mid segments used in early half of stage
        pool = [s for s in theme_segs if "late-encroach" not in s.get("intent", "") and s["difficultyMin"] <= 2]
    w_pool = sum(s.get("weight", 1) for s in pool) or 1
    w_form_in_pool = sum(s.get("weight", 1) for s in pool if s["id"] in ids)
    p = w_form_in_pool / w_pool
    exp = data["segmentsPerStage"] * p
    print(
        f"  {theme}: form_weight_in_pool={w_form_in_pool}/{w_pool} p={p:.2f} "
        f"E[forms/stage]≈{exp:.2f} (target 2-3)"
    )
    print(f"    pool={[s['id'] for s in pool]}")

# drop chance estimate for a formation of n with dropW
print("\n=== CAPSULE EXPECTATION (formation wipe) ===")
no_drop = 13
for eid, n in [
    ("junk_roller", 6),
    ("pipe_rat", 6),
    ("rust_skimmer", 7),
    ("lancer_dart", 8),
    ("sting_hornet", 7),
    ("spore_drifter", 7),
    ("interceptor_rush", 7),
]:
    dw = enemies[eid].get("dropWeight", 0)
    p1 = dw / (dw + no_drop)
    p_none = (1 - p1) ** n
    print(f"  {eid} n={n} p_drop/kill={p1:.3f} P(>=1 capsule)={1-p_none:.3f}")

print("\n=== ERRORS ===")
if errs:
    for e in errs:
        print(" ", e)
else:
    print("  none")
