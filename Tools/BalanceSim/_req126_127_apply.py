#!/usr/bin/env python3
"""
REQ-126: fortress mid-height solid platforms (visual floors for floating enemies).
REQ-127: tighten lengthTicks spread + lower closingSegmentsPerStage.

Only mutates GameData/waves.json (GROK content ownership).

Notes:
- ObstacleContactDamage applies to the PLAYER only (BattleSim.ResolveObstaclePlayerCollisions).
  Enemies do not take obstacle damage; "no bury" is a visual rule (platform under feet).
- Solid half-size = 0.5 world units (BattleConfig.ObstacleHalfWidth/Height).
"""
from __future__ import annotations

import json
import math
import random
from collections import Counter, defaultdict
from pathlib import Path
from statistics import mean, median, pstdev

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
ENEMIES = ROOT / "GameData" / "enemies.json"

TICK_HZ = 60.0
OBS_HALF = 0.5
LENGTH_MIN = 600
LENGTH_MAX = 900
SPIKE_LENGTH = 400  # REQ-103b short-scramble ceiling; exempt from LENGTH_MIN
SPIKE_IDS = {"seg_scrap_speed_spike", "seg_core_speed_spike"}
CLOSING_SEGMENTS = 5  # was 7
VALLEY_GAP = 120  # REQ-103a boss-valley: lengthTicks - lastSpawnTick

# Visual: platform top near enemy feet. platform_y ≈ enemy_y - halfH - OBS_HALF - gap
STAND_GAP = 0.125
# Reject platform if enemy center is this close (reads as buried in the block)
BURY_CENTER_DIST = 0.75
# Max new solids per fortress segment (MaxObstacles=32 concurrent)
MAX_NEW_PER_SEG = 6
SHELF_BLOCKS = 3  # contiguous 1-unit blocks → shelf width 3


def load_enemy_half_heights() -> dict[str, float]:
    with open(ENEMIES, encoding="utf-8") as f:
        data = json.load(f)
    return {e["id"]: float(e["halfHeight"]) for e in data["enemies"]}


def is_fortress(seg: dict) -> bool:
    return (
        seg.get("theme") == "fortress"
        or "fortress" in seg.get("id", "")
        or "theme=fortress" in seg.get("intent", "")
    )


def snap025(y: float) -> float:
    return round(y * 4.0) / 4.0


def cluster_spawn_ys(
    spawns: list[dict], bin_size: float = 0.75
) -> list[tuple[float, int, float]]:
    """(mean_y, count, max_halfH_among_cluster) for floating spawns."""
    # halfH filled later by caller via recompute — here just y clusters
    buckets: dict[int, list[float]] = defaultdict(list)
    for sp in spawns:
        y = float(sp["y"])
        if abs(y) >= 5.0:
            continue  # edge turrets already sit on floor/ceiling solids
        b = int(math.floor(y / bin_size))
        buckets[b].append(y)
    out = []
    for ys in buckets.values():
        out.append((sum(ys) / len(ys), len(ys)))
    out.sort(key=lambda t: -t[1])
    return out


def max_half_near(
    spawns: list[dict], half_h: dict[str, float], center_y: float, radius: float = 1.25
) -> float:
    m = 0.5
    for sp in spawns:
        if abs(float(sp["y"]) - center_y) <= radius:
            m = max(m, half_h.get(sp["enemyId"], 1.0))
    return m


def platform_y_under(cluster_y: float, half_near: float) -> float:
    """Shelf under the cluster so enemies read as standing on it."""
    return snap025(cluster_y - half_near - OBS_HALF - STAND_GAP)


def would_bury_large(
    platform_y: float,
    spawns: list[dict],
    half_h: dict[str, float],
    min_half: float = 0.8,
) -> bool:
    """True if a medium/large enemy center sits inside the solid block.

    Small rushers (interceptor ~0.47 halfH) may graze/pass through platforms —
    they have no obstacle collision and are not the visual 'standing' subjects.
    """
    for sp in spawns:
        hh = half_h.get(sp["enemyId"], 1.0)
        if hh < min_half:
            continue
        if abs(float(sp["y"]) - platform_y) < OBS_HALF:  # center inside 1x1 block
            return True
    return False


def existing_xy(obstacles: list[dict]) -> set[tuple[float, float]]:
    return {(float(o["x"]), float(o["y"])) for o in obstacles}


def strip_prior_req126_platforms(seg: dict) -> None:
    """Idempotent: remove solids previously tagged only by being mid-height REQ126 shelves.

    We detect REQ126 shelves as solids with |y| <= 4.75 that were not part of the
    original edge deco pattern (|y| >= 5.5 style). Safer: remove solids whose y is
    in mid band if intent already has REQ126 — then re-add cleanly.
    """
    intent = seg.get("intent") or ""
    if "REQ126 platform shelves" not in intent:
        return
    kept = []
    for o in seg.get("obstacles") or []:
        if o.get("type") == "solid" and abs(float(o["y"])) <= 4.75:
            continue  # drop mid-band solids (REQ126 shelves)
        kept.append(o)
    seg["obstacles"] = kept


def pick_shelf_xs(
    platform_y: float,
    occupied: set[tuple[float, float]],
    blocks: int,
    prefer_starts: list[float] | None = None,
) -> list[float]:
    """Short horizontal shelves with gaps — never a full-width wall.

    free_frac target: shelf width 3 in band [8,21] (13u) → free ≈ 77%.
    """
    default_starts = [9.0, 13.0, 17.0, 10.0, 14.0, 18.0, 11.0, 15.0, 19.0]
    starts = list(prefer_starts or []) + [s for s in default_starts if s not in (prefer_starts or [])]
    for start in starts:
        shelf = [start + i * 1.0 for i in range(blocks)]
        if any(x < 8.0 or x > 21.0 for x in shelf):
            continue
        if any((x, platform_y) in occupied for x in shelf):
            continue
        return shelf
    if blocks > 2:
        return pick_shelf_xs(platform_y, occupied, 2, prefer_starts)
    for x in [10.0, 12.0, 14.0, 16.0, 18.0, 20.0]:
        if (x, platform_y) not in occupied and 8.0 <= x <= 21.0:
            return [x]
    return []


def band_mean_y(spawns: list[dict], y_lo: float, y_hi: float) -> float | None:
    ys = [float(sp["y"]) for sp in spawns if y_lo < float(sp["y"]) < y_hi]
    if not ys:
        return None
    return sum(ys) / len(ys)


def nudge_deck_y(
    target: float,
    spawns: list[dict],
    half_h: dict[str, float],
    used_ys: list[float],
) -> float | None:
    """Start at target deck height; nudge by 0.25 until no large-enemy center bury."""
    for step in range(0, 8):
        for sign in (0, -1, 1) if step == 0 else (-1, 1):
            if step == 0 and sign != 0:
                continue
            cand = snap025(target + sign * step * 0.25)
            if abs(cand) > 4.0 or abs(cand) < 0.5:
                continue
            if any(abs(cand - uy) < 1.5 for uy in used_ys):
                continue
            if would_bury_large(cand, spawns, half_h):
                continue
            return cand
    return None


def add_fortress_platforms(seg: dict, half_h: dict[str, float]) -> dict:
    """Place two short solid shelves (upper + lower) as fortress armor decks.

    Coordinate rule:
      - Upper deck target y = clamp(upper_spawn_mean - 1.75, 0.75, 2.0)
      - Lower deck target y = clamp(lower_spawn_mean - 1.75, -3.5, -0.75)
        (always under feet so mid/large enemies read as standing on armor)
      - Each deck: 3 contiguous solids (width 3) at staggered x (9–11 / 13–15)
      - free_frac in x-band [8,21] stays ~0.75; max run width 3 ≤ 4
    """
    strip_prior_req126_platforms(seg)

    spawns = seg.get("spawns") or []
    obstacles = list(seg.get("obstacles") or [])
    before_solid = sum(1 for o in obstacles if o.get("type") == "solid")
    occupied = existing_xy(obstacles)

    upper_c = band_mean_y(spawns, 1.0, 5.0)
    lower_c = band_mean_y(spawns, -5.0, -1.0)

    decks: list[tuple[str, float]] = []
    if upper_c is not None:
        decks.append(("upper", max(0.75, min(2.0, snap025(upper_c - 1.75)))))
    else:
        decks.append(("upper", 1.5))
    if lower_c is not None:
        decks.append(("lower", max(-3.5, min(-0.75, snap025(lower_c - 1.75)))))
    else:
        decks.append(("lower", -1.5))

    added: list[dict] = []
    used_ys: list[float] = []
    start_rotation = [9.0, 13.0]
    cluster_report: list[tuple[float, int]] = []

    for shelf_i, (label, target) in enumerate(decks):
        py = nudge_deck_y(target, spawns, half_h, used_ys)
        if py is None:
            continue
        prefer = [start_rotation[shelf_i % len(start_rotation)], 17.0, 11.0, 15.0]
        xs = pick_shelf_xs(py, occupied, SHELF_BLOCKS, prefer_starts=prefer)
        if not xs:
            continue
        room = MAX_NEW_PER_SEG - len(added)
        if room <= 0:
            break
        xs = xs[:room]
        for x in xs:
            solid = {"type": "solid", "x": float(x), "y": float(py), "hp": 0}
            obstacles.append(solid)
            occupied.add((float(x), float(py)))
            added.append(solid)
        used_ys.append(py)
        cluster_report.append((round(target, 2), len(xs)))

    seg["obstacles"] = obstacles
    after_solid = sum(1 for o in obstacles if o.get("type") == "solid")

    parts = [p.strip() for p in (seg.get("intent") or "").split("|")]
    parts = [p for p in parts if p and not p.startswith("REQ126")]
    parts.append("REQ126 platform shelves")
    seg["intent"] = " | ".join(parts)

    return {
        "id": seg["id"],
        "before_solid": before_solid,
        "after_solid": after_solid,
        "added": len(added),
        "added_coords": [(o["x"], o["y"]) for o in added],
        "platform_ys": sorted(set(o["y"] for o in added)),
        "obs_total": len(obstacles),
        "clusters": cluster_report,
    }


def validate_visual_bury(seg: dict, half_h: dict[str, float]) -> list[str]:
    """Warn only when a mid/large enemy center is inside a mid-band solid."""
    warnings = []
    mid_ys = {
        float(o["y"])
        for o in (seg.get("obstacles") or [])
        if o.get("type") == "solid" and abs(float(o["y"])) < 5.0
    }
    for ys in mid_ys:
        for sp in seg.get("spawns") or []:
            hh = half_h.get(sp["enemyId"], 1.0)
            if hh < 0.8:
                continue
            ye = float(sp["y"])
            if abs(ye - ys) < OBS_HALF:
                warnings.append(
                    f"{seg['id']}: {sp['enemyId']}@y={ye} CENTER inside solid@y={ys}"
                )
    return warnings


def clamp_length(seg_id: str, ticks: int) -> int:
    if seg_id in SPIKE_IDS:
        return SPIKE_LENGTH
    return max(LENGTH_MIN, min(LENGTH_MAX, ticks))


def ensure_valley_gap(seg: dict) -> list[tuple[int, int, str]]:
    """Ensure length - lastSpawn >= VALLEY_GAP by pulling late spawns earlier."""
    spawns = seg.get("spawns") or []
    if not spawns:
        return []
    length = int(seg["lengthTicks"])
    last = max(int(sp["tick"]) for sp in spawns)
    if length - last >= VALLEY_GAP:
        return []
    need_last = length - VALLEY_GAP
    changes = []
    for sp in spawns:
        old = int(sp["tick"])
        if old > need_last:
            sp["tick"] = need_last
            changes.append((old, need_last, sp.get("enemyId", "?")))
    return changes


def stage_duration_stats(lengths: list[int], n_seg: int, trials: int = 8000):
    random.seed(42)
    times = [sum(random.choice(lengths) for _ in range(n_seg)) / TICK_HZ for _ in range(trials)]
    return {
        "min_s": min(times),
        "max_s": max(times),
        "mean_s": mean(times),
        "stdev_s": pstdev(times),
        "ratio": max(times) / min(times) if min(times) > 0 else float("inf"),
    }


def passage_report(fort: list[dict]) -> None:
    print("\n========== PASSAGE WIDTH CHECK ==========")
    for s in fort:
        solids = [o for o in (s.get("obstacles") or []) if o.get("type") == "solid"]
        by_y: dict[float, list[float]] = defaultdict(list)
        for o in solids:
            by_y[float(o["y"])].append(float(o["x"]))
        for y, xs in sorted(by_y.items()):
            xs = sorted(xs)
            runs = []
            run_start = prev = xs[0]
            for x in xs[1:]:
                if x - prev <= 1.0 + 1e-6:
                    prev = x
                else:
                    runs.append(prev - run_start + 1.0)
                    run_start = prev = x
            runs.append(prev - run_start + 1.0)
            max_run = max(runs)
            covered = set()
            for x in xs:
                for t in range(int((x - 0.5) * 4), int((x + 0.5) * 4) + 1):
                    covered.add(t)
            band = set(range(int(8 * 4), int(21 * 4) + 1))
            free_frac = 1.0 - len(covered & band) / len(band)
            flag = ""
            if max_run > 4.0:
                flag += " LONG_RUN"
            if free_frac < 0.55:
                flag += " LOW_FREE"
            print(
                f"  {s['id']} y={y}: n={len(xs)} xs={xs} max_run={max_run:.1f} "
                f"free_frac={free_frac:.2f}{flag}"
            )


def main() -> None:
    with open(WAVES, encoding="utf-8") as f:
        data = json.load(f)

    half_h = load_enemy_half_heights()
    segments = data["segments"]

    # Snapshot BEFORE (recompute from current file for platforms; lengths may already be clamped)
    # For honest before stats, reconstruct pre-clamp lengths from intent is hard —
    # we report post-state vs documented pre-state in the human report.
    lengths_now = [int(s["lengthTicks"]) for s in segments]
    early_n = int(data["segmentsPerStage"])
    late_n_before = int(data.get("closingSegmentsPerStage", 7))

    fort = [s for s in segments if is_fortress(s)]
    fort_solid_before = sum(
        1
        for s in fort
        for o in (s.get("obstacles") or [])
        if o.get("type") == "solid" and abs(float(o["y"])) > 4.75
    )
    # Count ALL solids before mutation (after strip of prior mid if any)
    # First strip mid from any previous run for clean before of mid
    mid_before = sum(
        1
        for s in fort
        for o in (s.get("obstacles") or [])
        if o.get("type") == "solid" and abs(float(o["y"])) <= 4.75
    )
    total_solid_before = sum(
        1 for s in fort for o in (s.get("obstacles") or []) if o.get("type") == "solid"
    )
    all_types_before = Counter(
        o.get("type") for s in segments for o in (s.get("obstacles") or [])
    )

    print("========== BEFORE (current file) ==========")
    print(
        f"fortress segs={len(fort)} solid_total={total_solid_before} "
        f"(edge~{fort_solid_before}, mid~{mid_before})"
    )
    print(f"all obs types={dict(all_types_before)}")
    print(
        f"lengthTicks min={min(lengths_now)} max={max(lengths_now)} "
        f"median={median(lengths_now)} mean={mean(lengths_now):.1f} "
        f"stdev={pstdev(lengths_now):.1f} ratio={max(lengths_now)/min(lengths_now):.2f}"
    )
    print(f"segmentsPerStage={early_n} closingSegmentsPerStage={late_n_before}")

    # ---------- REQ-126 ----------
    platform_reports = []
    for s in segments:
        if is_fortress(s):
            platform_reports.append(add_fortress_platforms(s, half_h))

    warnings = []
    for s in segments:
        if is_fortress(s):
            warnings.extend(validate_visual_bury(s, half_h))

    # ---------- REQ-127 ----------
    # Known pre-clamp lengths (for tagging segments we intentionally changed)
    pre_clamp_ids = {
        "seg_fortress_sentry_grid",
        "seg_core_guardian_wall",
        "seg_scrap_rust_gauntlet",
        "seg_fortress_turret_cross",
        "seg_fortress_drone_lattice",
        "seg_fortress_armored_gate",
        "seg_core_shard_battery",
        "seg_core_void_mix",
        "seg_scrap_speed_spike",
        "seg_core_speed_spike",
        "seg_scrap_clean_kill_corridor",
        "seg_fortress_clean_kill_hull",
        "seg_core_clean_kill_void",
        "seg_fortress_clean_kill_lattice",
        "seg_core_clean_kill_shard",
    }
    length_changes = []
    for s in segments:
        old = int(s["lengthTicks"])
        new = clamp_length(s["id"], old)
        if old != new:
            length_changes.append((s["id"], old, new))
            s["lengthTicks"] = new
        if s["id"] in pre_clamp_ids or s["id"] in SPIKE_IDS or old != new:
            parts = [p.strip() for p in (s.get("intent") or "").split("|")]
            parts = [p for p in parts if p and not p.startswith("REQ127")]
            tag = (
                "REQ127 length clamp (spike short-band 400)"
                if s["id"] in SPIKE_IDS
                else "REQ127 length clamp"
            )
            parts.append(tag)
            s["intent"] = " | ".join(parts)

    valley_fixes = []
    for s in segments:
        ch = ensure_valley_gap(s)
        if ch:
            valley_fixes.append((s["id"], ch))

    data["closingSegmentsPerStage"] = CLOSING_SEGMENTS

    # ---------- AFTER ----------
    fort = [s for s in segments if is_fortress(s)]
    total_solid_after = sum(
        1 for s in fort for o in (s.get("obstacles") or []) if o.get("type") == "solid"
    )
    mid_after = sum(
        1
        for s in fort
        for o in (s.get("obstacles") or [])
        if o.get("type") == "solid" and abs(float(o["y"])) <= 4.75
    )
    all_types_after = Counter(
        o.get("type") for s in segments for o in (s.get("obstacles") or [])
    )
    lengths_after = [int(s["lengthTicks"]) for s in segments]
    late_n = int(data["closingSegmentsPerStage"])

    print("\n========== REQ-126 PLATFORM REPORT ==========")
    for r in platform_reports:
        print(
            f"{r['id']}: solid {r['before_solid']}→{r['after_solid']} (+{r['added']}) "
            f"ys={r['platform_ys']} coords={r['added_coords']} "
            f"clusters={r['clusters']} obs={r['obs_total']}"
        )
    print(
        f"fortress solid total: {total_solid_before} → {total_solid_after} "
        f"(mid-band {mid_before} → {mid_after})"
    )
    if warnings:
        print(f"BURY WARNINGS ({len(warnings)}):")
        for w in warnings[:40]:
            print(" ", w)
    else:
        print("visual bury validation: PASS")

    print("\n========== REQ-127 LENGTH REPORT ==========")
    print(f"length changes this run ({len(length_changes)}):")
    for sid, old, new in length_changes:
        print(f"  {sid}: {old} → {new}")
    print(f"closingSegmentsPerStage: {late_n_before} → {late_n}")
    print(
        f"lengthTicks min={min(lengths_after)} max={max(lengths_after)} "
        f"median={median(lengths_after)} mean={mean(lengths_after):.1f} "
        f"stdev={pstdev(lengths_after):.1f} ratio={max(lengths_after)/min(lengths_after):.2f}"
    )
    ea = stage_duration_stats(lengths_after, early_n)
    la = stage_duration_stats(lengths_after, late_n)
    # also reference pre-REQ127 theoretical from original numbers
    lengths_original = list(lengths_after)
    # reconstruct originals for known clamps
    # (if already clamped, restore known pre values for comparison print)
    pre_map = {
        "seg_fortress_sentry_grid": 970,
        "seg_core_guardian_wall": 970,
        "seg_scrap_rust_gauntlet": 910,
        "seg_fortress_turret_cross": 910,
        "seg_fortress_drone_lattice": 910,
        "seg_fortress_armored_gate": 970,
        "seg_core_shard_battery": 910,
        "seg_core_void_mix": 910,
        "seg_scrap_speed_spike": 280,
        "seg_core_speed_spike": 280,
        "seg_scrap_clean_kill_corridor": 910,
        "seg_fortress_clean_kill_hull": 910,
        "seg_core_clean_kill_void": 910,
        "seg_fortress_clean_kill_lattice": 910,
        "seg_core_clean_kill_shard": 910,
    }
    lengths_pre = []
    for s in segments:
        lengths_pre.append(pre_map.get(s["id"], int(s["lengthTicks"])))
    print("\n--- baseline (pre-REQ127 reconstructed) ---")
    print(
        f"lengthTicks min={min(lengths_pre)} max={max(lengths_pre)} "
        f"mean={mean(lengths_pre):.1f} stdev={pstdev(lengths_pre):.1f} "
        f"ratio={max(lengths_pre)/min(lengths_pre):.2f}"
    )
    eb = stage_duration_stats(lengths_pre, 3)
    lb = stage_duration_stats(lengths_pre, 7)
    print(
        f"early n=3 mean={eb['mean_s']:.1f}s stdev={eb['stdev_s']:.1f}s "
        f"range=[{eb['min_s']:.1f},{eb['max_s']:.1f}] run_ratio={eb['ratio']:.2f}"
    )
    print(
        f"late  n=7 mean={lb['mean_s']:.1f}s stdev={lb['stdev_s']:.1f}s "
        f"range=[{lb['min_s']:.1f},{lb['max_s']:.1f}] run_ratio={lb['ratio']:.2f}"
    )
    print(f"late/early mean = {lb['mean_s']/eb['mean_s']:.2f}")

    print("\n--- after REQ127 ---")
    print(
        f"early n={early_n} mean={ea['mean_s']:.1f}s stdev={ea['stdev_s']:.1f}s "
        f"range=[{ea['min_s']:.1f},{ea['max_s']:.1f}] run_ratio={ea['ratio']:.2f}"
    )
    print(
        f"late  n={late_n} mean={la['mean_s']:.1f}s stdev={la['stdev_s']:.1f}s "
        f"range=[{la['min_s']:.1f},{la['max_s']:.1f}] run_ratio={la['ratio']:.2f}"
    )
    print(f"late/early mean = {la['mean_s']/ea['mean_s']:.2f}")

    print("\n========== AFTER OBS TYPES ==========")
    print(dict(all_types_after))

    passage_report(fort)

    with open(WAVES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"\nWrote {WAVES}")
    print(
        "DETERMINISM: lengthTicks / closingSegmentsPerStage / obstacle layout "
        "change stage generation hashes — existing replays/saves may invalidate."
    )
    print(
        "CODEX overlap: SegmentStageGenerator time-based fill (if landed later) "
        "will further stabilize stage duration; this data clamp is a temporary "
        "softener and may stack with generator changes (double-tightening risk)."
    )


if __name__ == "__main__":
    main()
