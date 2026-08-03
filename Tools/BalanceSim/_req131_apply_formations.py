#!/usr/bin/env python3
"""REQ-131: inject Gradius-style formations into stages 1-3 early/mid segments.

Formations are just same-enemyId spawn rows with short tick spacing + y patterns.
No schema changes. Stage-1 scrapyard reorganizes existing fodder (no density spike).
Hive/fortress reorganize early fodder windows into readable squadrons.
"""
from __future__ import annotations

import json
from collections import Counter
from copy import deepcopy
from pathlib import Path
from typing import Callable, Iterable

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"


# ── formation builders ──────────────────────────────────────────────

def column(enemy: str, y: float, n: int, t0: int, dt: int) -> list[dict]:
    """일렬 종대 — same y, fixed tick gap."""
    return [{"tick": t0 + i * dt, "enemyId": enemy, "y": y} for i in range(n)]


def diagonal(
    enemy: str, y0: float, dy: float, n: int, t0: int, dt: int
) -> list[dict]:
    """사선 편대 — y steps each member."""
    return [
        {"tick": t0 + i * dt, "enemyId": enemy, "y": round(y0 + i * dy, 4)}
        for i in range(n)
    ]


def v_wedge(
    enemy: str, y_center: float, dy: float, n: int, t0: int, dt: int
) -> list[dict]:
    """V자/쐐기 — center leads, then ±1, ±2, ... pairs trail.

    n must be odd for a clean center (if even, trailing member is + only).
    Tick order: center, then upper, lower, upper, lower...
    """
    spawns = [{"tick": t0, "enemyId": enemy, "y": y_center}]
    pairs = (n - 1) // 2
    extra = (n - 1) % 2  # if even n, one unpaired + wing
    k = 0
    for p in range(1, pairs + 1):
        spawns.append(
            {
                "tick": t0 + (2 * p - 1) * dt,
                "enemyId": enemy,
                "y": round(y_center + p * dy, 4),
            }
        )
        spawns.append(
            {
                "tick": t0 + (2 * p) * dt,
                "enemyId": enemy,
                "y": round(y_center - p * dy, 4),
            }
        )
        k = 2 * p
    if extra:
        spawns.append(
            {
                "tick": t0 + (k + 1) * dt,
                "enemyId": enemy,
                "y": round(y_center + (pairs + 1) * dy, 4),
            }
        )
    return spawns


def sort_spawns(spawns: list[dict]) -> list[dict]:
    return sorted(spawns, key=lambda s: (s["tick"], s["y"], s["enemyId"]))


def remove_in_window(
    spawns: list[dict],
    t_lo: int,
    t_hi: int,
    enemy_ids: set[str] | None = None,
    max_remove: int | None = None,
) -> list[dict]:
    """Drop up to max_remove spawns in [t_lo, t_hi] (optionally filtered by id)."""
    kept: list[dict] = []
    removed = 0
    for sp in spawns:
        in_win = t_lo <= sp["tick"] <= t_hi
        id_ok = enemy_ids is None or sp["enemyId"] in enemy_ids
        if in_win and id_ok and (max_remove is None or removed < max_remove):
            removed += 1
            continue
        kept.append(sp)
    return kept


def replace_window_with_formation(
    spawns: list[dict],
    formation: list[dict],
    *,
    remove_ids: set[str] | None = None,
    pad_before: int = 10,
    pad_after: int = 10,
) -> list[dict]:
    """Remove overlapping-window fodder of remove_ids, then insert formation."""
    t0 = min(s["tick"] for s in formation)
    t1 = max(s["tick"] for s in formation)
    cleaned = remove_in_window(
        spawns, t0 - pad_before, t1 + pad_after, remove_ids, max_remove=None
    )
    # also strip any exact (tick, enemyId) collisions with formation members
    form_keys = {(s["tick"], s["enemyId"], s["y"]) for s in formation}
    cleaned = [
        s
        for s in cleaned
        if (s["tick"], s["enemyId"], s["y"]) not in form_keys
    ]
    # avoid same-tick same-y collisions with anything
    form_tick_y = {(s["tick"], s["y"]) for s in formation}
    cleaned = [s for s in cleaned if (s["tick"], s["y"]) not in form_tick_y]
    return sort_spawns(cleaned + formation)


def max_tick(spawns: list[dict]) -> int:
    return max(s["tick"] for s in spawns) if spawns else 0


def validate_segment(seg: dict, label: str) -> list[str]:
    errs: list[str] = []
    L = int(seg["lengthTicks"])
    spawns = seg.get("spawns") or []
    mt = max_tick(spawns)
    if mt >= L:
        errs.append(f"{label}: max spawn tick {mt} >= lengthTicks {L}")
    # formation tail needs room to enter: last spawn should leave ~40+ ticks
    # before segment end so the ship fully appears (soft warning only)
    if mt > L - 40:
        errs.append(
            f"{label}: WARN last spawn tick {mt} close to length {L} (need margin)"
        )
    trav = seg.get("traversableLaneMasks") or [7]
    # if mid-only (2) anywhere, keep |y| <= 2.25 for formations in that seg
    if all(m == 2 for m in trav) or trav == [2]:
        for sp in spawns:
            if abs(float(sp["y"])) > 2.25:
                errs.append(
                    f"{label}: y={sp['y']} outside mid-lane corridor (mask=2)"
                )
    return errs


# ── per-segment transforms ──────────────────────────────────────────

def apply_scrap_debris_line(seg: dict) -> str:
    """종대 junk_roller ×6 — first-impression score line."""
    form = column("junk_roller", y=0.5, n=6, t0=55, dt=8)
    seg["spawns"] = replace_window_with_formation(
        seg["spawns"],
        form,
        remove_ids={"junk_roller", "rust_skimmer", "pipe_rat"},
        pad_before=15,
        pad_after=5,
    )
    # keep late-half rhythm: if we over-cleared, re-seed light trail after form
    late = [s for s in seg["spawns"] if s["tick"] > 160]
    if len(late) < 5:
        trail = [
            {"tick": 200, "enemyId": "rust_skimmer", "y": 2.5},
            {"tick": 250, "enemyId": "pipe_rat", "y": -1.5},
            {"tick": 310, "enemyId": "rust_skimmer", "y": -3.0},
            {"tick": 370, "enemyId": "pipe_rat", "y": 1.0},
            {"tick": 430, "enemyId": "rust_skimmer", "y": 3.5},
            {"tick": 490, "enemyId": "pipe_rat", "y": -2.0},
        ]
        # drop any existing in trail window then add
        base = [s for s in seg["spawns"] if s["tick"] < 180]
        seg["spawns"] = sort_spawns(base + trail)
    note = "종대 junk_roller×6 @y=0.5 dt=8 t=55..95"
    return note


def apply_scrap_pipe_dash(seg: dict) -> str:
    """사선 pipe_rat ×6 — descending diagonal."""
    form = diagonal("pipe_rat", y0=3.0, dy=-1.0, n=6, t0=50, dt=8)
    seg["spawns"] = replace_window_with_formation(
        seg["spawns"],
        form,
        remove_ids={"pipe_rat", "rust_skimmer"},
        pad_before=15,
        pad_after=10,
    )
    late = [s for s in seg["spawns"] if s["tick"] > 160]
    if len(late) < 6:
        trail = [
            {"tick": 180, "enemyId": "rust_skimmer", "y": -2.0},
            {"tick": 230, "enemyId": "rust_skimmer", "y": 3.0},
            {"tick": 280, "enemyId": "pipe_rat", "y": -3.5},
            {"tick": 340, "enemyId": "rust_skimmer", "y": 1.5},
            {"tick": 400, "enemyId": "pipe_rat", "y": 4.0},
            {"tick": 460, "enemyId": "rust_skimmer", "y": -1.0},
            {"tick": 520, "enemyId": "pipe_rat", "y": 0.0},
        ]
        base = [s for s in seg["spawns"] if s["tick"] < 150]
        seg["spawns"] = sort_spawns(base + trail)
    return "사선 pipe_rat×6 y=3→-2 dt=8 t=50..90"


def apply_scrap_zigzag_posts(seg: dict) -> str:
    """V자 rust_skimmer ×7 — center leads."""
    form = v_wedge("rust_skimmer", y_center=0.0, dy=1.5, n=7, t0=70, dt=8)
    # preserve tumblers outside window
    tumblers = [s for s in seg["spawns"] if s["enemyId"] == "scrap_tumbler"]
    others = [s for s in seg["spawns"] if s["enemyId"] != "scrap_tumbler"]
    rebuilt = replace_window_with_formation(
        others,
        form,
        remove_ids={"pipe_rat", "rust_skimmer", "junk_roller"},
        pad_before=20,
        pad_after=15,
    )
    # light trail + restore tumblers if not colliding
    form_ticks = {s["tick"] for s in form}
    trail = [
        {"tick": 220, "enemyId": "pipe_rat", "y": 3.5},
        {"tick": 270, "enemyId": "pipe_rat", "y": -3.5},
        {"tick": 330, "enemyId": "junk_roller", "y": 2.0},
        {"tick": 390, "enemyId": "junk_roller", "y": -2.0},
        {"tick": 450, "enemyId": "pipe_rat", "y": 0.0},
        {"tick": 510, "enemyId": "rust_skimmer", "y": 2.5},
    ]
    keep_t = [
        s
        for s in tumblers
        if s["tick"] not in form_ticks
        and not (60 <= s["tick"] <= 140)
    ]
    # if tumblers fell in form window, re-place after
    if len(keep_t) < len(tumblers):
        keep_t = [
            {"tick": 180, "enemyId": "scrap_tumbler", "y": 2.5},
            {"tick": 360, "enemyId": "scrap_tumbler", "y": -2.5},
        ]
    base = [s for s in rebuilt if s["tick"] < 200 or s in form]
    # cleaner: formation + trail + tumblers
    seg["spawns"] = sort_spawns(form + trail + keep_t)
    return "V자 rust_skimmer×7 y=0±1.5±3±4.5 dt=8 t=70..118"


def apply_scrap_shard_field(seg: dict) -> str:
    """종대 rust_skimmer ×6 @ mid-low lane."""
    form = column("rust_skimmer", y=-1.0, n=6, t0=60, dt=8)
    tumblers = [s for s in seg["spawns"] if s["enemyId"] == "scrap_tumbler"]
    form = form  # noqa
    trail = [
        {"tick": 180, "enemyId": "pipe_rat", "y": 3.0},
        {"tick": 230, "enemyId": "junk_roller", "y": -3.5},
        {"tick": 290, "enemyId": "pipe_rat", "y": 1.5},
        {"tick": 350, "enemyId": "junk_roller", "y": 3.5},
        {"tick": 410, "enemyId": "pipe_rat", "y": -0.5},
        {"tick": 480, "enemyId": "junk_roller", "y": 2.0},
    ]
    keep_t = []
    for s in tumblers:
        if s["tick"] < 55 or s["tick"] > 120:
            keep_t.append(s)
    if len(keep_t) < 2:
        keep_t = [
            {"tick": 170, "enemyId": "scrap_tumbler", "y": -1.5},
            {"tick": 380, "enemyId": "scrap_tumbler", "y": 2.0},
        ]
    seg["spawns"] = sort_spawns(form + trail + keep_t)
    return "종대 rust_skimmer×6 @y=-1 dt=8 t=60..100"


def apply_scrap_rail_split(seg: dict) -> str:
    """종대 pipe_rat ×5 @ y=0 — mid-corridor only (mask=2)."""
    form = column("pipe_rat", y=0.0, n=5, t0=50, dt=8)
    tumblers = [
        s for s in seg["spawns"] if s["enemyId"] == "scrap_tumbler"
    ]
    # rest must stay |y|<=2
    trail = [
        {"tick": 160, "enemyId": "rust_skimmer", "y": 1.5},
        {"tick": 200, "enemyId": "rust_skimmer", "y": -1.5},
        {"tick": 250, "enemyId": "junk_roller", "y": 0.0},
        {"tick": 300, "enemyId": "rust_skimmer", "y": 1.0},
        {"tick": 360, "enemyId": "junk_roller", "y": -1.0},
        {"tick": 430, "enemyId": "pipe_rat", "y": 0.0},
        {"tick": 500, "enemyId": "rust_skimmer", "y": 2.0},
        {"tick": 540, "enemyId": "pipe_rat", "y": -2.0},
    ]
    keep_t = [
        s
        for s in tumblers
        if abs(float(s["y"])) <= 2.0 and not (40 <= s["tick"] <= 110)
    ]
    if len(keep_t) < 2:
        keep_t = [
            {"tick": 220, "enemyId": "scrap_tumbler", "y": 0.0},
            {"tick": 420, "enemyId": "scrap_tumbler", "y": 0.0},
        ]
    seg["spawns"] = sort_spawns(form + trail + keep_t)
    return "종대 pipe_rat×5 @y=0 (mask=2) dt=8 t=50..82"


def apply_scrap_center_breach(seg: dict) -> str:
    """사선 junk_roller ×5 ascending — d1-3 mid-early."""
    form = diagonal("junk_roller", y0=-2.5, dy=1.0, n=5, t0=80, dt=9)
    tumblers = [s for s in seg["spawns"] if s["enemyId"] == "scrap_tumbler"]
    trail = [
        {"tick": 180, "enemyId": "rust_skimmer", "y": 3.0},
        {"tick": 230, "enemyId": "pipe_rat", "y": -2.0},
        {"tick": 290, "enemyId": "rust_skimmer", "y": 1.5},
        {"tick": 350, "enemyId": "pipe_rat", "y": -3.0},
        {"tick": 420, "enemyId": "rust_skimmer", "y": 0.0},
        {"tick": 490, "enemyId": "pipe_rat", "y": 2.5},
        {"tick": 540, "enemyId": "rust_skimmer", "y": -1.5},
    ]
    keep_t = [s for s in tumblers if s["tick"] < 70 or s["tick"] > 140]
    if len(keep_t) < 2:
        keep_t = [
            {"tick": 200, "enemyId": "scrap_tumbler", "y": 2.0},
            {"tick": 400, "enemyId": "scrap_tumbler", "y": -2.0},
        ]
    seg["spawns"] = sort_spawns(form + trail + keep_t)
    return "사선 junk_roller×5 y=-2.5→1.5 dt=9 t=80..116"


# ── Hive ────────────────────────────────────────────────────────────

def apply_hive_lancer_rush(seg: dict) -> str:
    """사선 lancer_dart ×8 early + 종대 ×5 mid — replace dense lancer spam."""
    form = diagonal("lancer_dart", y0=3.5, dy=-1.0, n=8, t0=30, dt=8)
    form2 = column("lancer_dart", y=1.0, n=5, t0=300, dt=8)
    keep = []
    for s in seg["spawns"]:
        if s["enemyId"] == "hive_tentacle":
            keep.append(s)
            continue
        # strip almost all original lancers — formations + short tail carry the rush feel
        if s["enemyId"] == "lancer_dart":
            continue
        if 25 <= s["tick"] <= 100 and s["enemyId"] == "sting_hornet":
            continue
        keep.append(s)
    tail = [
        {"tick": 400, "enemyId": "lancer_dart", "y": -2.5},
        {"tick": 450, "enemyId": "lancer_dart", "y": 3.0},
        {"tick": 500, "enemyId": "lancer_dart", "y": 0.5},
    ]
    seg["spawns"] = sort_spawns(form + form2 + tail + keep)
    return (
        "사선 lancer×8 y=3.5→-3.5 dt=8 t=30..86 + "
        "종대 lancer×5 @y=1 dt=8 t=300..332"
    )


def apply_hive_hornet_dive(seg: dict) -> str:
    """V자 sting_hornet ×7."""
    form = v_wedge("sting_hornet", y_center=0.0, dy=1.25, n=7, t0=40, dt=8)
    keep = []
    for s in seg["spawns"]:
        if s["enemyId"] == "hive_tentacle":
            keep.append(s)
            continue
        if 30 <= s["tick"] <= 130 and s["enemyId"] in (
            "sting_hornet",
            "lancer_dart",
            "spore_drifter",
        ):
            continue
        keep.append(s)
    # light mid continuation
    mid = [
        {"tick": 200, "enemyId": "lancer_dart", "y": -2.0},
        {"tick": 250, "enemyId": "lancer_dart", "y": 2.5},
        {"tick": 320, "enemyId": "spore_drifter", "y": -1.5},
        {"tick": 380, "enemyId": "lancer_dart", "y": 3.5},
        {"tick": 440, "enemyId": "brood_spitter", "y": 0.0},
        {"tick": 500, "enemyId": "spore_drifter", "y": -3.0},
        {"tick": 560, "enemyId": "sting_hornet", "y": 1.5},
    ]
    # drop keep that overlaps mid clutter for hornets we already formed
    keep = [s for s in keep if not (190 <= s["tick"] <= 580 and s["enemyId"] == "sting_hornet")]
    # keep non-hornet original mid/late
    keep_mid = [
        s
        for s in keep
        if s["tick"] > 130
        and s["enemyId"] not in ("sting_hornet",)
    ]
    # prefer curated mid + tentacles + form
    tents = [s for s in keep if s["enemyId"] == "hive_tentacle"]
    others = [
        s
        for s in keep
        if s["enemyId"] != "hive_tentacle" and s["tick"] > 130
    ]
    # simplify: form + tents + selected mid from original non-form enemies
    selected = []
    for s in others:
        if s["enemyId"] in ("brood_spitter", "spore_drifter", "lancer_dart"):
            selected.append(s)
    # avoid overcrowding: cap selected
    selected = selected[:8]
    seg["spawns"] = sort_spawns(form + tents + selected)
    # ensure density: if too few, add mid
    if len(seg["spawns"]) < 14:
        seg["spawns"] = sort_spawns(form + tents + mid + selected[:4])
    return "V자 sting_hornet×7 y=0±1.25±2.5±3.75 dt=8 t=40..88"


def apply_hive_spore_cloud(seg: dict) -> str:
    """종대 spore_drifter ×7."""
    form = column("spore_drifter", y=1.5, n=7, t0=45, dt=9)
    keep = []
    for s in seg["spawns"]:
        if s["enemyId"] == "hive_tentacle":
            keep.append(s)
            continue
        if 35 <= s["tick"] <= 130 and s["enemyId"] in (
            "spore_drifter",
            "sting_hornet",
        ):
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "종대 spore_drifter×7 @y=1.5 dt=9 t=45..99"


def apply_hive_brood_wave(seg: dict) -> str:
    """사선 sting_hornet ×6 mid-window (after early spitters settle)."""
    form = diagonal("sting_hornet", y0=-3.0, dy=1.2, n=6, t0=300, dt=8)
    keep = []
    for s in seg["spawns"]:
        if 290 <= s["tick"] <= 370 and s["enemyId"] in (
            "sting_hornet",
            "lancer_dart",
            "spore_drifter",
        ):
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "사선 sting_hornet×6 y=-3→3 dt=8 t=300..340"


def apply_hive_tentacle_posts(seg: dict) -> str:
    """V자 lancer_dart ×5 mid-early."""
    form = v_wedge("lancer_dart", y_center=0.0, dy=1.5, n=5, t0=150, dt=8)
    keep = []
    for s in seg["spawns"]:
        if 140 <= s["tick"] <= 210 and s["enemyId"] in (
            "lancer_dart",
            "sting_hornet",
            "spore_drifter",
        ):
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "V자 lancer_dart×5 y=0±1.5±3 dt=8 t=150..182"


# ── Fortress ────────────────────────────────────────────────────────

# Solid shelf y to avoid for formation flight paths (visual)
FORT_AVOID_Y = {1.0, 1.25, 1.5, -2.5, -3.5}


def apply_fortress_interceptor_assault(seg: dict) -> str:
    """종대 interceptor ×7 + V자 interceptor ×5 — reshape the rush.

    V wing y avoids solid platform shelves (y≈1.0/1.5/-2.5/-3.5).
    """
    form1 = column("interceptor_rush", y=2.0, n=8, t0=30, dt=8)
    # dy=2.0 → y 0,±2,±4 — clears solid shelves at ±1.0/±1.5/±2.5/±3.5
    form2 = v_wedge("interceptor_rush", y_center=0.0, dy=2.0, n=5, t0=300, dt=8)
    keep = []
    for s in seg["spawns"]:
        if s["enemyId"] == "interceptor_rush" and s["tick"] <= 560:
            continue  # full replace interceptor stream with formations + tail
        keep.append(s)
    # small tail of interceptors late (not formation)
    tail = [
        {"tick": 450, "enemyId": "interceptor_rush", "y": 3.5},
        {"tick": 500, "enemyId": "interceptor_rush", "y": -3.0},
        {"tick": 550, "enemyId": "interceptor_rush", "y": 0.5},
    ]
    seg["spawns"] = sort_spawns(form1 + form2 + tail + keep)
    return (
        "종대 interceptor×8 @y=2 dt=8 t=30..86 + "
        "V자 interceptor×5 y=0±2±4 dt=8 t=300..332"
    )


def apply_fortress_mortar_line(seg: dict) -> str:
    """사선 interceptor ×7 early — y steps skip solid shelves."""
    # y: 4, 3, 2, 0, -1, -3, -4 (skip 1 and -2.5 shelf bands)
    form = [
        {"tick": 90 + i * 8, "enemyId": "interceptor_rush", "y": y}
        for i, y in enumerate([4.0, 3.0, 2.0, 0.0, -1.0, -3.0, -4.0])
    ]
    keep = []
    for s in seg["spawns"]:
        if 80 <= s["tick"] <= 160 and s["enemyId"] == "interceptor_rush":
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "사선 interceptor×7 y=4,3,2,0,-1,-3,-4 dt=8 t=90..138"


def apply_fortress_sentry_grid(seg: dict) -> str:
    """V자 interceptor ×7 — dy=2 clears solid shelves at ±1/±1.5/±2.5/±3.5."""
    form = v_wedge("interceptor_rush", y_center=0.0, dy=2.0, n=7, t0=100, dt=8)
    # y: 0, ±2, ±4, ±6
    keep = []
    for s in seg["spawns"]:
        if 90 <= s["tick"] <= 180 and s["enemyId"] == "interceptor_rush":
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "V자 interceptor×7 y=0±2±4±6 dt=8 t=100..148"


def apply_fortress_shield_bastion(seg: dict) -> str:
    """종대 interceptor ×8 @ y=-1 (open corridor, capsule density)."""
    form = column("interceptor_rush", y=-1.0, n=8, t0=170, dt=8)
    keep = []
    for s in seg["spawns"]:
        if 160 <= s["tick"] <= 250 and s["enemyId"] == "interceptor_rush":
            continue
        keep.append(s)
    seg["spawns"] = sort_spawns(form + keep)
    return "종대 interceptor×8 @y=-1 dt=8 t=170..226"


TRANSFORMS: dict[str, Callable[[dict], str]] = {
    # scrapyard early
    "seg_scrap_debris_line": apply_scrap_debris_line,
    "seg_scrap_pipe_dash": apply_scrap_pipe_dash,
    "seg_scrap_zigzag_posts": apply_scrap_zigzag_posts,
    "seg_scrap_shard_field": apply_scrap_shard_field,
    "seg_scrap_rail_split": apply_scrap_rail_split,
    "seg_scrap_center_breach": apply_scrap_center_breach,
    # hive mid/early-half
    "seg_hive_lancer_rush": apply_hive_lancer_rush,
    "seg_hive_hornet_dive": apply_hive_hornet_dive,
    "seg_hive_spore_cloud": apply_hive_spore_cloud,
    "seg_hive_brood_wave": apply_hive_brood_wave,
    "seg_hive_tentacle_posts": apply_hive_tentacle_posts,
    # fortress mid/early-half
    "seg_fortress_interceptor_assault": apply_fortress_interceptor_assault,
    "seg_fortress_mortar_line": apply_fortress_mortar_line,
    "seg_fortress_sentry_grid": apply_fortress_sentry_grid,
    "seg_fortress_shield_bastion": apply_fortress_shield_bastion,
}


def count_spawns_by_theme(data: dict, themes: Iterable[str]) -> dict[str, int]:
    out: dict[str, int] = {t: 0 for t in themes}
    for s in data["segments"]:
        t = s.get("theme")
        if t in out:
            out[t] += len(s.get("spawns") or [])
    return out


def main() -> None:
    data = json.loads(WAVES.read_text(encoding="utf-8"))
    before_theme = count_spawns_by_theme(data, ("scrapyard", "hive", "fortress"))
    before_seg: dict[str, int] = {}
    after_notes: dict[str, str] = {}
    errors: list[str] = []

    for seg in data["segments"]:
        sid = seg["id"]
        if sid not in TRANSFORMS:
            continue
        before_seg[sid] = len(seg.get("spawns") or [])
        note = TRANSFORMS[sid](seg)
        # stamp intent
        intent = seg.get("intent") or ""
        if "REQ131" not in intent:
            seg["intent"] = (intent + " | REQ131 formation").strip(" |")
        after_notes[sid] = note
        errors.extend(validate_segment(seg, sid))

    after_theme = count_spawns_by_theme(data, ("scrapyard", "hive", "fortress"))

    print("=== PER-SEGMENT SPAWN COUNTS ===")
    for sid, note in after_notes.items():
        after_n = len(
            next(s for s in data["segments"] if s["id"] == sid)["spawns"]
        )
        print(f"{sid}: {before_seg[sid]} → {after_n} | {note}")

    print("\n=== THEME TOTALS ===")
    for t in ("scrapyard", "hive", "fortress"):
        print(f"  {t}: {before_theme[t]} → {after_theme[t]} (Δ{after_theme[t]-before_theme[t]:+d})")

    if errors:
        print("\n=== VALIDATION ===")
        for e in errors:
            print(" ", e)
        hard = [e for e in errors if not e.startswith("WARN") and ": WARN" not in e]
        # filter soft warnings
        hard = [e for e in errors if "WARN" not in e]
        if hard:
            print("HARD FAIL — not writing")
            for e in hard:
                print(" ", e)
            raise SystemExit(1)

    # JSON write: 2-space indent, no trailing space, UTF-8 no BOM, keep key order
    text = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    WAVES.write_text(text, encoding="utf-8")
    print(f"\nWrote {WAVES}")
    print("OK")


if __name__ == "__main__":
    main()
