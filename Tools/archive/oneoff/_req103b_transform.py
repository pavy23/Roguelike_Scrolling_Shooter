# -*- coding: utf-8 -*-
"""REQ-103b: fill gimmick axes (blocksEnemyBullets, regen, outcomes, scroll spike).

Re-runnable: idempotent where possible (skips already-tagged fields / ids).
"""
from __future__ import annotations

import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WAVES = ROOT / "GameData" / "waves.json"


def load() -> dict:
    with WAVES.open(encoding="utf-8") as f:
        return json.load(f)


def save(data: dict) -> None:
    with WAVES.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def find_seg(data: dict, sid: str) -> dict | None:
    for s in data["segments"]:
        if s["id"] == sid:
            return s
    return None


def mark_cover(obs: dict) -> None:
    if obs.get("type") != "breakable":
        return
    obs["blocksEnemyBullets"] = True


def mark_regen(obs: dict, delay: int) -> None:
    if obs.get("type") != "breakable":
        return
    obs["regenDelayTicks"] = delay


def apply_scrap_cover(data: dict) -> list[str]:
    """St1: cover lines on breakables — 'shooting destroys cover' teaching."""
    notes: list[str] = []

    # Early open: partial cover (roughly half) — teach without total blockade.
    early_partial = {
        "seg_scrap_debris_line": [0, 2, 4],  # mid-ish Y posts
        "seg_scrap_pipe_dash": [0, 2, 4],
        "seg_scrap_zigzag_posts": [0, 2, 4],  # alternate posts form lane cover
        "seg_scrap_shard_field": [0, 2, 4, 6],
        "seg_scrap_center_breach": [1, 3, 5],  # leave a dig gap in the wall
        "seg_scrap_skimmer_weave": [0, 2, 4],
        "seg_scrap_rail_split": [0, 2, 4],
    }
    for sid, idxs in early_partial.items():
        seg = find_seg(data, sid)
        if not seg:
            continue
        obs = seg.get("obstacles", [])
        n = 0
        for i in idxs:
            if i < len(obs) and obs[i].get("type") == "breakable":
                if not obs[i].get("blocksEnemyBullets"):
                    mark_cover(obs[i])
                    n += 1
        if n:
            notes.append(f"{sid}: +{n} cover")
            intent = seg.get("intent", "")
            if "REQ103b cover" not in intent:
                seg["intent"] = (intent + " | REQ103b cover").strip(" |")

    # Mid/late: denser cover lines for turret gauntlet + midboss approach.
    late_cover = {
        # Prefer mid-lane Y cover posts that sit between player path and edge turrets.
        "seg_scrap_junk_corridor": "all_breakable",
        "seg_scrap_tumbler_pack": "all_breakable",
        "seg_scrap_rust_gauntlet": "all_breakable",
    }
    for sid, mode in late_cover.items():
        seg = find_seg(data, sid)
        if not seg:
            continue
        obs = seg.get("obstacles", [])
        n = 0
        for o in obs:
            if o.get("type") == "breakable" and not o.get("blocksEnemyBullets"):
                # Keep extreme edge debris as non-cover so dig/farm still rewards.
                y = abs(float(o.get("y", 0)))
                if mode == "all_breakable" and y <= 4.0:
                    mark_cover(o)
                    n += 1
        if n:
            notes.append(f"{sid}: +{n} cover-line")
            intent = seg.get("intent", "")
            if "REQ103b cover" not in intent:
                seg["intent"] = (intent + " | REQ103b cover-line").strip(" |")

    # Reposition late gauntlet cover into staggered vertical lines (player can tuck
    # behind mid posts when turrets fire from ±5.5).
    gauntlet = find_seg(data, "seg_scrap_rust_gauntlet")
    if gauntlet is not None:
        # Replace breakable layout with explicit cover columns at y≈±2 and 0.
        solids = [o for o in gauntlet.get("obstacles", []) if o.get("type") != "breakable"]
        cover_posts = [
            {"type": "breakable", "x": 11.5, "y": 2.0, "hp": 50, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 11.5, "y": -2.0, "hp": 50, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 14.0, "y": 0.0, "hp": 55, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 16.5, "y": 2.25, "hp": 55, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 16.5, "y": -2.25, "hp": 55, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 19.0, "y": 1.5, "hp": 48, "blocksEnemyBullets": True},
            {"type": "breakable", "x": 19.0, "y": -1.5, "hp": 48, "blocksEnemyBullets": True},
        ]
        gauntlet["obstacles"] = solids + cover_posts
        notes.append("seg_scrap_rust_gauntlet: cover-line layout")

    return notes


def apply_hive_regen(data: dict) -> list[str]:
    """St2: regen cell walls on late diggable corridors (hive max obs ≤5)."""
    notes: list[str] = []
    # delay in 180–300 band; staggered so dig rhythm differs by segment.
    targets = {
        "seg_hive_membrane_wall": 240,  # signature dig-through membrane
        "seg_hive_organic_pulse": 210,
        "seg_hive_nest_choke": 180,
        "seg_hive_hornet_dive": 270,  # mid-late teach
    }
    for sid, delay in targets.items():
        seg = find_seg(data, sid)
        if not seg:
            continue
        n = 0
        for o in seg.get("obstacles", []):
            if o.get("type") == "breakable":
                if o.get("regenDelayTicks") != delay:
                    mark_regen(o, delay)
                    n += 1
        if n:
            notes.append(f"{sid}: regenDelay={delay} ×{n}")
            intent = seg.get("intent", "")
            if "REQ103b regen" not in intent:
                seg["intent"] = (intent + f" | REQ103b regen@{delay}").strip(" |")

    # membrane_wall: ensure dig path is breakable column (not solid full-block).
    # Existing layout already has solid at edges y=±6.5 and breakable column at x=15
    # y=2.5/0/-2.5 — perfect dig-through center. Keep solids edge-only so lane
    # center remains open after dig (no softlock when walls regen).
    membrane = find_seg(data, "seg_hive_membrane_wall")
    if membrane is not None:
        # Slightly lower center HP so dig feels responsive; regen still returns it.
        for o in membrane.get("obstacles", []):
            if o.get("type") == "breakable" and abs(float(o.get("y", 99))) < 0.1:
                o["hp"] = min(int(o.get("hp", 32)), 28)
        notes.append("seg_hive_membrane_wall: dig-center soft HP")

    return notes


def apply_fortress_turret_density(data: dict) -> list[str]:
    """St3: mild hull-turret densify on late fortress segments (+1 pair each).

    Heavy densify shifts midboss/room durations enough to flip contract RNG
    (Rotating strategy uses executedTicks) into core+gimmickIntensity paths
    whose scaled timeLimit cannot cover closingSegmentsPerStage=7.
    """
    notes: list[str] = []

    def add_spawn(seg: dict, tick: int, enemy: str, y: float) -> bool:
        for sp in seg["spawns"]:
            if sp["tick"] == tick and sp["enemyId"] == enemy and abs(sp["y"] - y) < 0.01:
                return False
        if seg["lengthTicks"] - tick < 120:
            return False
        seg["spawns"].append({"tick": tick, "enemyId": enemy, "y": y})
        seg["spawns"].sort(key=lambda s: (s["tick"], s["enemyId"], s["y"]))
        return True

    # One extra turret pair on signature hull-line segs (design: stage=ship).
    packs = {
        "seg_fortress_turret_cross": [
            (260, "turret_ground", -5.5),
            (260, "turret_ceiling", 5.5),
        ],
        "seg_fortress_drone_lattice": [
            (200, "turret_ground", -5.5),
            (200, "turret_ceiling", 5.5),
        ],
        "seg_fortress_armored_gate": [
            (150, "turret_ground", -5.5),
            (150, "turret_ceiling", 5.5),
        ],
        "seg_fortress_crossfire_alley": [
            (200, "turret_ground", -5.5),
            (200, "turret_ceiling", 5.5),
        ],
    }
    for sid, adds in packs.items():
        seg = find_seg(data, sid)
        if not seg:
            continue
        n = 0
        for tick, enemy, y in adds:
            if add_spawn(seg, tick, enemy, y):
                n += 1
        if n:
            notes.append(f"{sid}: +{n} turret spawns")
            intent = seg.get("intent", "")
            if "REQ103b hull-turret" not in intent:
                seg["intent"] = (intent + " | REQ103b hull-turret-mild").strip(" |")
    return notes


def apply_nebula_lightning(data: dict) -> list[str]:
    """St4: mild lightning static densify (+1 phase_disc on late segs)."""
    notes: list[str] = []

    packs = {
        "seg_nebula_crystal_drift": (450, 0.0),
        "seg_nebula_prism_haze": (300, 3.0),
        "seg_nebula_drift_lattice": (320, 2.5),
        "seg_nebula_void_moth_swarm": (350, 3.5),
    }
    for sid, (tick, y) in packs.items():
        seg = find_seg(data, sid)
        if not seg:
            continue
        exists = any(
            sp["tick"] == tick
            and sp["enemyId"] == "phase_disc"
            and abs(sp["y"] - y) < 0.01
            for sp in seg["spawns"]
        )
        if exists:
            continue
        if seg["lengthTicks"] - tick < 120:
            continue
        seg["spawns"].append({"tick": tick, "enemyId": "phase_disc", "y": y})
        seg["spawns"].sort(key=lambda s: (s["tick"], s["enemyId"], s["y"]))
        notes.append(f"{sid}: +1 phase_disc")
        intent = seg.get("intent", "")
        if "REQ103b lightning" not in intent:
            seg["intent"] = (intent + " | REQ103b lightning-mild").strip(" |")
    return notes


def apply_core_time_limit_headroom(data: dict) -> list[str]:
    """St5: raise core timeLimit so gimmickIntensity 1.5 still clears 7-seg closing.

    Base 9000 × (2/3) under intensity 1.5 = 6000, but closing 7×~850 ≈ 5950–6800.
    12000 × 2/3 = 8000 provides margin without removing the time-pressure gimmick.
    """
    notes: list[str] = []
    for g in data.get("gimmicks", []):
        if g.get("theme") == "core":
            old = g.get("timeLimitTicks", 0)
            if old < 12000:
                g["timeLimitTicks"] = 12000
                notes.append(f"core timeLimitTicks {old} → 12000")
            else:
                notes.append(f"core timeLimitTicks already {old}")
    return notes


def lighten_spawns(spawns: list[dict], remove_frac: float = 0.18) -> list[dict]:
    """Drop ~18% of spawns (prefer later ticks) for cleanKill reward path."""
    if not spawns:
        return spawns
    ordered = sorted(spawns, key=lambda s: (s["tick"], s["enemyId"], s["y"]))
    n_remove = max(1, int(round(len(ordered) * remove_frac)))
    # Prefer removing low drop-weight fodder from the second half.
    # Heuristic: remove every ~5th from the back half.
    keep = list(ordered)
    removed = 0
    i = len(keep) - 1
    while removed < n_remove and i >= len(keep) // 3:
        # Keep elites / tanks / turrets more often; drop fodder first.
        fodder = {
            "pipe_rat",
            "rust_skimmer",
            "lancer_dart",
            "wisp_spark",
            "interceptor_rush",
            "rift_blade",
            "sting_hornet",
            "zako_fast",
            "zako_straight",
        }
        if keep[i]["enemyId"] in fodder or removed < n_remove:
            if keep[i]["enemyId"] in fodder or (len(keep) - i) % 2 == 0:
                del keep[i]
                removed += 1
        i -= 1
    # Capsule bump: promote a few mid-tier spawns to higher dropWeight enemies.
    promote = {
        "pipe_rat": "junk_roller",  # 3→5
        "rust_skimmer": "scrap_tumbler",  # 3→5
        "lancer_dart": "brood_spitter",  # 2→6
        "sting_hornet": "spore_drifter",  # 3→4
        "wisp_spark": "echo_wisp",  # 2→5
        "interceptor_rush": "sentry_drone",  # 2→5
        "rift_blade": "phase_disc",  # 2→5
        "zako_straight": "zako_sine",  # 4→5
        "zako_fast": "zako_sine_slow",  # 3→6
    }
    bumped = 0
    for sp in keep:
        if bumped >= 3:
            break
        if sp["enemyId"] in promote:
            sp["enemyId"] = promote[sp["enemyId"]]
            bumped += 1
    return keep


def make_clean_kill_variant(base: dict, new_id: str) -> dict:
    seg = copy.deepcopy(base)
    seg["id"] = new_id
    seg["postMidbossOutcomes"] = ["cleanKill"]
    # Late band only: dMin≥3 keeps solids legal (stage-1 debris rule) and
    # avoids inflating early stage HP pools (REQ-073 mono). Core falls back
    # to Default when the tagged pool cannot clear at lower difficulty.
    seg["difficultyMin"] = 3
    seg["difficultyMax"] = 5
    # Slightly lower weight so cleanKill pool is intentional, not flooding Default.
    seg["weight"] = max(3, int(seg.get("weight", 5)) - 1)
    seg["spawns"] = lighten_spawns(seg.get("spawns", []))
    intent = seg.get("intent", "")
    seg["intent"] = (
        intent + " | REQ103b cleanKill (fewer foes + capsule-lean)"
    ).strip(" |")
    # Keep gimmick fields if present; cleanKill may still use cover/regen.
    return seg


def add_outcome_branches(data: dict) -> list[str]:
    """Per-theme cleanKill late variants (Default untagged = normal path)."""
    notes: list[str] = []
    # One strong late seed per theme → cleanKill reward table.
    seeds = [
        ("seg_scrap_rust_gauntlet", "seg_scrap_clean_kill_corridor"),
        ("seg_hive_membrane_wall", "seg_hive_clean_kill_membrane"),
        ("seg_fortress_turret_cross", "seg_fortress_clean_kill_hull"),
        ("seg_nebula_prism_haze", "seg_nebula_clean_kill_haze"),
        ("seg_core_void_mix", "seg_core_clean_kill_void"),
        # Extra breadth so closingSegmentsPerStage=7 assembles under cleanKill
        # without always reusing one template (still OK if it does).
        ("seg_scrap_junk_corridor", "seg_scrap_clean_kill_junk"),
        ("seg_hive_organic_pulse", "seg_hive_clean_kill_pulse"),
        ("seg_fortress_drone_lattice", "seg_fortress_clean_kill_lattice"),
        ("seg_nebula_crystal_drift", "seg_nebula_clean_kill_crystal"),
        ("seg_core_shard_battery", "seg_core_clean_kill_shard"),
    ]
    existing = {s["id"] for s in data["segments"]}
    for base_id, new_id in seeds:
        if new_id in existing:
            notes.append(f"skip exists {new_id}")
            continue
        base = find_seg(data, base_id)
        if base is None:
            notes.append(f"missing base {base_id}")
            continue
        data["segments"].append(make_clean_kill_variant(base, new_id))
        existing.add(new_id)
        notes.append(f"+{new_id} from {base_id}")
    return notes


def add_scroll_spikes(data: dict) -> list[str]:
    """St1 late + St5 early short 3/2 scroll spikes."""
    notes: list[str] = []
    existing = {s["id"] for s in data["segments"]}

    scrap_spike = {
        "id": "seg_scrap_speed_spike",
        "difficultyMin": 3,
        "difficultyMax": 5,
        "weight": 5,
        "lengthTicks": 280,
        "entryLaneMask": 7,
        "exitLaneMask": 7,
        "traversableLaneMasks": [7, 3],
        "theme": "scrapyard",
        "scrollSpeedMultiplier": 1.5,
        "spawns": [
            {"tick": 20, "enemyId": "rust_skimmer", "y": 2.5},
            {"tick": 40, "enemyId": "rust_skimmer", "y": -2.5},
            {"tick": 60, "enemyId": "pipe_rat", "y": 0.0},
            {"tick": 90, "enemyId": "junk_roller", "y": 1.5},
            {"tick": 110, "enemyId": "scrap_tumbler", "y": -1.5},
            {"tick": 140, "enemyId": "rust_skimmer", "y": 3.0},
        ],
        "obstacles": [
            {
                "type": "breakable",
                "x": 12.0,
                "y": 2.0,
                "hp": 30,
                "blocksEnemyBullets": True,
            },
            {
                "type": "breakable",
                "x": 12.0,
                "y": -2.0,
                "hp": 30,
                "blocksEnemyBullets": True,
            },
            {
                "type": "breakable",
                "x": 16.0,
                "y": 0.0,
                "hp": 28,
                "blocksEnemyBullets": True,
            },
        ],
        "intent": "REQ103b speed-spike 3/2 | theme=scrapyard | late short scramble",
    }

    core_spike = {
        "id": "seg_core_speed_spike",
        "difficultyMin": 2,
        "difficultyMax": 4,
        "weight": 6,
        "lengthTicks": 280,
        "entryLaneMask": 7,
        "exitLaneMask": 7,
        "traversableLaneMasks": [7, 3],
        "theme": "core",
        "scrollSpeedMultiplier": 1.5,
        "spawns": [
            {"tick": 20, "enemyId": "rift_blade", "y": 3.0},
            {"tick": 40, "enemyId": "rift_blade", "y": -3.0},
            {"tick": 60, "enemyId": "phase_disc", "y": 0.0},
            {"tick": 90, "enemyId": "rift_blade", "y": 1.5},
            {"tick": 110, "enemyId": "rift_blade", "y": -1.5},
            {"tick": 140, "enemyId": "phase_disc", "y": 2.5},
        ],
        "obstacles": [
            {"type": "solid", "x": 11.0, "y": 6.0, "hp": 0},
            {"type": "solid", "x": 15.0, "y": -6.0, "hp": 0},
            {"type": "breakable", "x": 13.5, "y": 1.5, "hp": 35},
            {"type": "breakable", "x": 17.0, "y": -1.5, "hp": 35},
        ],
        "intent": "REQ103b speed-spike 3/2 | theme=core | early short scramble",
    }

    for seg in (scrap_spike, core_spike):
        if seg["id"] in existing:
            notes.append(f"skip exists {seg['id']}")
            continue
        data["segments"].append(seg)
        notes.append(f"+{seg['id']} mult=1.5 len={seg['lengthTicks']}")
    return notes


def main() -> None:
    data = load()
    all_notes: list[str] = []
    print("=== REQ-103b transform ===")
    for label, fn in [
        ("core timeLimit headroom", apply_core_time_limit_headroom),
        ("scrap cover", apply_scrap_cover),
        ("hive regen", apply_hive_regen),
        ("fortress turrets", apply_fortress_turret_density),
        ("nebula lightning", apply_nebula_lightning),
        ("cleanKill branches", add_outcome_branches),
        ("scroll spikes", add_scroll_spikes),
    ]:
        notes = fn(data)
        print(f"\n[{label}]")
        for n in notes:
            print(" ", n)
        all_notes.extend(notes)

    n = len(data["segments"])
    print(f"\nsegments total: {n}")
    save(data)
    print(f"wrote {WAVES}")

    # Quick field audit
    cover = regen = spikes = ck = 0
    for s in data["segments"]:
        if s.get("scrollSpeedMultiplier"):
            spikes += 1
        if s.get("postMidbossOutcomes"):
            ck += 1
        for o in s.get("obstacles", []):
            if o.get("blocksEnemyBullets"):
                cover += 1
            if o.get("regenDelayTicks"):
                regen += 1
    print(f"audit cover-obs={cover} regen-obs={regen} spike-segs={spikes} cleanKill-segs={ck}")


if __name__ == "__main__":
    main()
