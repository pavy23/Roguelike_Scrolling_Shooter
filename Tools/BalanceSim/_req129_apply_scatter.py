#!/usr/bin/env python3
"""
REQ-129: scatter scrapyard breakable obstacles along X.

Problem (human 2026-08-03): breakable scrap piles spawn in tight vertical
clusters at the same x, then long empty stretches.

Strategy:
- Keep intentional walls (rail_split rails, rust_gauntlet cover pillars,
  speed_spike gate, one dig-wall column in center_breach).
- Elsewhere: redistribute breakable X across ~[10.5, 19.5] with min adjacent
  gap ≥ 1.5; reduce same-x±1 stacks of 3+ to at most one signature wall.
- Preserve y, hp, blocksEnemyBullets (and any other keys) when only X changes;
  center_breach also reshapes Y for the dig-wall signature.
- Verify: no enemy-center inside obstacle block (visual bury); mid-lane cover
  counts for late scrap; cover flag count preserved.

Only mutates GameData/waves.json (GROK content ownership).
"""
from __future__ import annotations

import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
ENEMIES = ROOT / "GameData" / "enemies.json"

OBS_HALF = 0.5
SPAWN_X = 21.0
SCROLL = 5.0  # world u/s
TICK_HZ = 60.0
SCROLL_PER_TICK = SCROLL / TICK_HZ
X_MIN, X_MAX = 10.5, 19.5
MIN_GAP_TARGET = 1.5
BURY_EPS = 0.05  # center inside block → bury


def is_scrap(seg: dict) -> bool:
    return (
        seg.get("theme") == "scrapyard"
        or "scrap" in seg.get("id", "")
        or "theme=scrapyard" in seg.get("intent", "")
    )


def snap025(v: float) -> float:
    return round(v * 4.0) / 4.0


def load_enemy_sizes() -> dict[str, tuple[float, float]]:
    with open(ENEMIES, encoding="utf-8") as f:
        data = json.load(f)
    return {
        e["id"]: (float(e["halfWidth"]), float(e["halfHeight"]))
        for e in data["enemies"]
    }


def breakables(seg: dict) -> list[dict]:
    return [o for o in (seg.get("obstacles") or []) if o.get("type") == "breakable"]


def set_breakable_xy(obs: dict, x: float, y: float | None = None) -> None:
    obs["x"] = snap025(float(x))
    if y is not None:
        obs["y"] = snap025(float(y))


def wall_clusters(bs: list[dict], x_tol: float = 1.0, min_n: int = 2) -> list[int]:
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
            if abs(bs[i]["x"] - bs[j]["x"]) <= x_tol:
                union(i, j)
    sizes = []
    for root in range(n):
        members = [i for i in range(n) if find(i) == root]
        if len(members) >= min_n:
            sizes.append(len(members))
    return sorted(sizes, reverse=True)


def x_gaps(xs: list[float]) -> list[float]:
    s = sorted(xs)
    return [s[i + 1] - s[i] for i in range(len(s) - 1)]


def summarize_scrap(segments: list[dict], label: str) -> None:
    segs = [s for s in segments if is_scrap(s)]
    all_gaps = []
    walls = Counter()
    segs_walls = 0
    total_b = 0
    spans = []
    print(f"\n=== {label} scrapyard ===")
    for s in segs:
        bs = breakables(s)
        total_b += len(bs)
        if not bs:
            continue
        xs = [float(o["x"]) for o in bs]
        gaps = x_gaps(xs)
        all_gaps.extend(gaps)
        spans.append(max(xs) - min(xs))
        wc = wall_clusters(bs)
        if wc:
            segs_walls += 1
            for c in wc:
                walls[c] += 1
        print(
            f"  {s['id']}: n={len(bs)} xs={sorted(xs)} "
            f"span={max(xs)-min(xs):.2f} gaps={[round(g,2) for g in gaps]} "
            f"walls={wc or '-'}"
        )
    if all_gaps:
        tiny = sum(1 for g in all_gaps if g <= 1.0)
        print(
            f"  TOTAL br={total_b} gap min/med/max="
            f"{min(all_gaps):.2f}/{median(all_gaps):.2f}/{max(all_gaps):.2f} "
            f"gaps≤1={tiny}/{len(all_gaps)} walls_segs={segs_walls} hist={dict(walls)} "
            f"span med={median(spans):.2f}"
        )


def even_xs(n: int, lo: float = X_MIN, hi: float = X_MAX) -> list[float]:
    if n <= 1:
        return [snap025((lo + hi) / 2)]
    return [snap025(lo + i * (hi - lo) / (n - 1)) for i in range(n)]


def assign_by_old_x_order(bs: list[dict], new_xs: list[float]) -> None:
    """Map breakables sorted by old x (stable y tiebreak) onto new_xs sorted."""
    assert len(bs) == len(new_xs)
    ordered = sorted(bs, key=lambda o: (float(o["x"]), float(o["y"])))
    targets = sorted(new_xs)
    for o, x in zip(ordered, targets):
        set_breakable_xy(o, x)


def apply_layouts(segments: list[dict]) -> list[str]:
    """Hand layouts per segment id. Returns list of changed ids."""
    by_id = {s["id"]: s for s in segments}
    changed: list[str] = []

    def touch(sid: str, note: str) -> None:
        changed.append(f"{sid}: {note}")

    # --- Early teaching: pure scatter, no vertical walls ---

    # debris_line n=5: was [11,13.5,14,16,18.5] cluster 13.5/14
    if "seg_scrap_debris_line" in by_id:
        bs = breakables(by_id["seg_scrap_debris_line"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.0, 19.0))
        touch("seg_scrap_debris_line", "even x 11→19")

    # pipe_dash n=6: was paired clusters
    if "seg_scrap_pipe_dash" in by_id:
        bs = breakables(by_id["seg_scrap_pipe_dash"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.5, 19.5))
        touch("seg_scrap_pipe_dash", "even x 11.5→19.5")

    # skimmer_weave n=6 mid-mask=2 — keep y, spread x
    if "seg_scrap_skimmer_weave" in by_id:
        bs = breakables(by_id["seg_scrap_skimmer_weave"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.5, 19.0))
        touch("seg_scrap_skimmer_weave", "even x 11.5→19.0")

    # zigzag_posts n=6: wall of 3 at 11–13 → full even
    if "seg_scrap_zigzag_posts" in by_id:
        bs = breakables(by_id["seg_scrap_zigzag_posts"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.0, 19.0))
        touch("seg_scrap_zigzag_posts", "even x 11→19 (drop open-cluster wall)")

    # shard_field n=8: already decent; enforce min gap ~1.3+
    if "seg_scrap_shard_field" in by_id:
        bs = breakables(by_id["seg_scrap_shard_field"])
        assign_by_old_x_order(bs, even_xs(len(bs), 10.5, 19.5))
        touch("seg_scrap_shard_field", "even x 10.5→19.5")

    # junk_corridor n=5: already good gaps — slight widen only
    if "seg_scrap_junk_corridor" in by_id:
        bs = breakables(by_id["seg_scrap_junk_corridor"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.5, 19.5))
        touch("seg_scrap_junk_corridor", "even x 11.5→19.5")

    if "seg_scrap_clean_kill_junk" in by_id:
        bs = breakables(by_id["seg_scrap_clean_kill_junk"])
        assign_by_old_x_order(bs, even_xs(len(bs), 11.5, 19.5))
        touch("seg_scrap_clean_kill_junk", "even x 11.5→19.5 (mirror junk)")

    # --- Signature walls kept (partial) ---

    # center_breach: was 5@14.5 + 2@17.5. Keep ONE dig wall of 3 + scatter 4.
    if "seg_scrap_center_breach" in by_id:
        seg = by_id["seg_scrap_center_breach"]
        bs = breakables(seg)
        assert len(bs) == 7
        # Sort old by y descending for wall members, rest by x
        # Layout:
        # dig wall column x=14.0: y = 2.0, 0.0, -2.0 (3)
        # scatter: (11.0, 3.25), (12.5, -3.25), (16.5, 3.0), (19.0, -2.5)
        layout = [
            (11.0, 3.25),
            (12.5, -3.25),
            (14.0, 2.0),
            (14.0, 0.0),
            (14.0, -2.0),
            (16.5, 3.0),
            (19.0, -2.5),
        ]
        # Preserve cover flags: old cover at y≈±1.5@14.5 and y=2@17.5
        # Reassign cover to dig wall mid± and one scatter
        # First clear then set
        ordered_old = sorted(bs, key=lambda o: (float(o["x"]), -float(o["y"])))
        # Use existing objects; rewrite x/y; set blocksEnemyBullets on wall edges + one scatter
        for o in ordered_old:
            o.pop("blocksEnemyBullets", None)
        for o, (x, y) in zip(ordered_old, layout):
            set_breakable_xy(o, x, y)
        # cover: wall at ±2 (blocks mid-high / mid-low) + scatter at 16.5
        for o in ordered_old:
            y = float(o["y"])
            x = float(o["x"])
            if (x == 14.0 and abs(y) == 2.0) or (x == 16.5 and y == 3.0):
                o["blocksEnemyBullets"] = True
        touch(
            "seg_scrap_center_breach",
            "dig-wall 3@x=14 + 4 scatter (was 5@14.5 wall)",
        )

    # tumbler_pack: keep ONE vertical pair as cover post, spread rest
    if "seg_scrap_tumbler_pack" in by_id:
        seg = by_id["seg_scrap_tumbler_pack"]
        bs = breakables(seg)
        assert len(bs) == 6
        # Preserve relative y order of old; assign xs
        # layout xs: 11.5, 13.5, 15.5, 15.5 (pair), 17.5, 19.5
        # pair the two with most extreme ±y among mid-lane
        by_abs_y = sorted(bs, key=lambda o: -abs(float(o["y"])))
        pair = by_abs_y[:2]  # two farthest from center → vertical feel
        rest = by_abs_y[2:]
        for o in bs:
            o.pop("blocksEnemyBullets", None)
        set_breakable_xy(pair[0], 15.5)  # keep their y
        set_breakable_xy(pair[1], 15.5)
        rest_xs = [11.5, 13.5, 17.5, 19.5]
        rest_sorted = sorted(rest, key=lambda o: float(o["y"]), reverse=True)
        for o, x in zip(rest_sorted, rest_xs):
            set_breakable_xy(o, x)
        # cover on pair + one high
        for o in pair:
            o["blocksEnemyBullets"] = True
        # one more cover on highest |y| rest if mid-lane
        for o in rest_sorted:
            if abs(float(o["y"])) <= 4.0:
                o["blocksEnemyBullets"] = True
                break
        touch("seg_scrap_tumbler_pack", "1 cover pair @15.5 + 4 scatter")

    # rust_gauntlet: intentional cover pillars — keep pairs but SPREAD columns
    # was 11.5/11.5, 14, 16.5/16.5, 19/19 → already spaced; ensure gaps ≥2.5
    # widen slightly: 11.0 pair, 13.5 single, 16.0 pair, 19.0 pair
    if "seg_scrap_rust_gauntlet" in by_id:
        bs = breakables(by_id["seg_scrap_rust_gauntlet"])
        # group by current x
        by_x: dict[float, list] = defaultdict(list)
        for o in bs:
            by_x[float(o["x"])].append(o)
        # map old columns sorted → new x anchors
        cols = sorted(by_x.keys())
        # 7 breakables: three pairs + one mid → 4 columns
        # expected: 11.5 pair, 14 mid, 16.5 pair, 19 pair
        new_anchors = [11.0, 14.0, 16.5, 19.5]
        for old_x, new_x in zip(cols, new_anchors):
            for o in by_x[old_x]:
                set_breakable_xy(o, new_x)
        touch("seg_scrap_rust_gauntlet", "widen pillar columns 11/14/16.5/19.5")

    if "seg_scrap_clean_kill_corridor" in by_id:
        bs = breakables(by_id["seg_scrap_clean_kill_corridor"])
        by_x = defaultdict(list)
        for o in bs:
            by_x[float(o["x"])].append(o)
        cols = sorted(by_x.keys())
        new_anchors = [11.0, 14.0, 16.5, 19.5]
        for old_x, new_x in zip(cols, new_anchors):
            for o in by_x[old_x]:
                set_breakable_xy(o, new_x)
        touch("seg_scrap_clean_kill_corridor", "mirror gauntlet pillar columns")

    # rail_split: KEEP vertical pairs (top/bottom rail) — intentional mask=2 training
    # but stagger columns a bit more if needed: 12,15,18 already good (gap 3)
    # no change — note only
    # speed_spike: KEEP gate pair — no change

    # Optional light nudge for rail: shift middle column? leave as-is.
    return changed


def check_bury(segments: list[dict], sizes: dict[str, tuple[float, float]]) -> list[str]:
    """When enemy x crosses obstacle x (same scroll), flag y AABB bury."""
    issues = []
    for seg in segments:
        if not is_scrap(seg):
            continue
        bs = breakables(seg)
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"]
        all_obs = bs + solids
        for sp in seg.get("spawns") or []:
            eid = sp["enemyId"]
            ey = float(sp["y"])
            tick = int(sp["tick"])
            hw, hh = sizes.get(eid, (0.75, 0.6))
            # enemy world x at spawn = SPAWN_X; after dt ticks: SPAWN_X - scroll*dt
            # obstacle spawned at seg start at ox, after same absolute time:
            # both scroll, relative x constant after both exist.
            # Obstacle exists from t=0; enemy from t=tick.
            # At time t>=tick: ex = SPAWN_X - SCROLL_PER_TICK*(t-tick)
            #                  ox_t = ox - SCROLL_PER_TICK*t
            # Meet when ex == ox_t => SPAWN_X - scroll*(t-tick) = ox - scroll*t
            # => SPAWN_X + scroll*tick = ox => only if ox ≈ SPAWN_X + ... wait
            # SPAWN_X - s*(t-tick) = ox - s*t
            # SPAWN_X - s*t + s*tick = ox - s*t
            # SPAWN_X + s*tick = ox
            # So they only share x if ox == SPAWN_X + s*tick — that can't be for ox~15.
            # Actually both scroll at same rate so relative velocity is 0 once both exist!
            # Enemy appears at SPAWN_X while obstacle already at ox - s*tick.
            # relative: enemy is to the RIGHT of obstacle by (SPAWN_X - (ox - s*tick)).
            # They never close if both only scroll left at same speed!
            # Enemies have their own movement speed leftward (pattern speed).
            # Approx: enemy moves left at its pattern speed on top of... need check.
            # Simpler visual rule from REQ-126: |ey - oy| < OBS_HALF → center in block.
            for o in all_obs:
                oy = float(o["y"])
                if abs(ey - oy) < OBS_HALF - BURY_EPS:
                    # only flag larger/static-ish
                    if hh >= 0.7 or o.get("type") == "solid":
                        issues.append(
                            f"{seg['id']} spawn {eid} y={ey} vs {o.get('type')} "
                            f"@({o['x']},{oy}) center-in-block"
                        )
                # AABB overlap (full half-sizes)
                if abs(ey - oy) < (hh + OBS_HALF) and abs(
                    # rough: if obstacle x is in [12,18] and enemy will path through
                    float(o["x"]) - SPAWN_X
                ) < 12:
                    # soft note only for medium+ and very tight y
                    if abs(ey - oy) < 0.35 and hh >= 0.8:
                        issues.append(
                            f"SOFT {seg['id']} {eid} y={ey} near obs y={oy} x={o['x']}"
                        )
    return issues


def check_cover_gates(segments: list[dict]) -> list[str]:
    fails = []
    cover_obs = 0
    cover_segs = 0
    late_mid = 0
    for seg in segments:
        if not is_scrap(seg):
            continue
        has = False
        for o in breakables(seg):
            if o.get("blocksEnemyBullets"):
                cover_obs += 1
                has = True
                if seg.get("difficultyMin", 1) >= 2 and abs(float(o["y"])) <= 3.0:
                    late_mid += 1
        if has:
            cover_segs += 1
    if cover_obs < 20 or cover_segs < 6:
        fails.append(f"cover too thin obs={cover_obs} segs={cover_segs}")
    if late_mid < 8:
        fails.append(f"late mid-lane cover posts {late_mid} < 8")
    print(f"cover: obs={cover_obs} segs={cover_segs} late_mid_posts={late_mid}")
    return fails


def check_lane_masks(segments: list[dict]) -> list[str]:
    """Soft: for center-only mask=2 segs, don't stack breakables on y≈0 at many x."""
    notes = []
    for seg in segments:
        if not is_scrap(seg):
            continue
        masks = seg.get("traversableLaneMasks") or []
        if masks and all(m == 2 for m in masks):
            mid_blockers = [
                o
                for o in breakables(seg)
                if abs(float(o["y"])) < 1.0
            ]
            if len(mid_blockers) >= 3:
                notes.append(
                    f"{seg['id']} mask=2 has {len(mid_blockers)} mid-y breakables "
                    f"(diggable OK, but dense)"
                )
    return notes


def main() -> None:
    with open(WAVES, encoding="utf-8") as f:
        data = json.load(f)
    segments = data["segments"]
    sizes = load_enemy_sizes()

    summarize_scrap(segments, "BEFORE")

    changed = apply_layouts(segments)
    print("\n=== CHANGES ===")
    for c in changed:
        print(" ", c)

    summarize_scrap(segments, "AFTER")

    bury = check_bury(segments, sizes)
    print("\n=== BURY CHECK ===")
    if bury:
        for b in bury:
            print(" ", b)
    else:
        print("  none")

    cover_fails = check_cover_gates(segments)
    lane_notes = check_lane_masks(segments)
    print("\n=== COVER / LANE ===")
    if cover_fails:
        for f in cover_fails:
            print(" FAIL", f)
    else:
        print(" cover gates OK")
    for n in lane_notes:
        print(" note", n)

    # intent tag
    for seg in segments:
        if not is_scrap(seg):
            continue
        if any(seg["id"] in c for c in changed):
            intent = seg.get("intent") or ""
            if "REQ129" not in intent:
                seg["intent"] = intent + " | REQ129 scatter-x"

    # write
    with open(WAVES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"\nWrote {WAVES}")

    if cover_fails:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
