"""REQ-129: scrapyard breakable obstacle distribution analysis."""
from __future__ import annotations

import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from statistics import mean, median, pstdev

ROOT = Path(__file__).resolve().parents[2]
with open(ROOT / "GameData" / "waves.json", "r", encoding="utf-8") as f:
    data = json.load(f)

segments = data["segments"]
themes = list(data.get("themes") or [])


def is_theme(s, theme: str) -> bool:
    return (
        s.get("theme") == theme
        or theme in s.get("id", "")
        or f"theme={theme}" in s.get("intent", "")
    )


def breakables(s):
    return [o for o in (s.get("obstacles") or []) if o.get("type") == "breakable"]


def x_gaps(xs_sorted):
    if len(xs_sorted) < 2:
        return []
    return [xs_sorted[i + 1] - xs_sorted[i] for i in range(len(xs_sorted) - 1)]


def cluster_same_x(breakables_list, tol=1.0):
    """Groups where |x_i - x_j| <= tol. Returns list of cluster sizes (>=2)."""
    xs = sorted((o["x"], o["y"], i) for i, o in enumerate(breakables_list))
    used = set()
    cluster_sizes = []
    for i, (x, y, idx) in enumerate(xs):
        if idx in used:
            continue
        group = [idx]
        used.add(idx)
        for j in range(i + 1, len(xs)):
            x2, y2, idx2 = xs[j]
            if idx2 in used:
                continue
            if abs(x2 - x) <= tol:
                group.append(idx2)
                used.add(idx2)
            elif x2 - x > tol:
                # xs sorted by x; further ones are farther
                # but group seed is x — only consecutive expansion from seed
                pass
        # re-expand: all members within tol of any member (connected component-ish via shared band)
        # simpler: same x-band = all within [minx, maxx] span <= 2*tol with chain
        # recompute properly via union-find on |xi-xj|<=tol
    # redo with union-find for correctness
    n = len(breakables_list)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for i in range(n):
        for j in range(i + 1, n):
            if abs(breakables_list[i]["x"] - breakables_list[j]["x"]) <= tol:
                union(i, j)
    groups = defaultdict(list)
    for i in range(n):
        groups[find(i)].append(i)
    return sorted((len(g) for g in groups.values() if len(g) >= 2), reverse=True)


def vertical_wall_clusters(breakables_list, x_tol=1.0, min_count=2):
    """Clusters with same x±tol and distinct y — vertical stacks."""
    n = len(breakables_list)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for i in range(n):
        for j in range(i + 1, n):
            if abs(breakables_list[i]["x"] - breakables_list[j]["x"]) <= x_tol:
                union(i, j)
    walls = []
    for root in range(n):
        members = [i for i in range(n) if find(i) == root]
        if len(members) < min_count:
            continue
        xs = [breakables_list[i]["x"] for i in members]
        ys = [breakables_list[i]["y"] for i in members]
        walls.append(
            {
                "count": len(members),
                "x_span": (min(xs), max(xs)),
                "ys": sorted(ys),
            }
        )
    walls.sort(key=lambda w: -w["count"])
    return walls


def summarize_theme(theme: str, detail: bool = True):
    segs = [s for s in segments if is_theme(s, theme)]
    all_xs = []
    all_gaps = []
    total_b = 0
    wall_counts = Counter()  # cluster size -> how many such walls
    segs_with_walls = 0
    x_span_per_seg = []
    density = []  # breakables / lengthTicks * 100

    print(f"\n{'='*60}")
    print(f"THEME: {theme}  segments={len(segs)}")
    print(f"{'='*60}")

    for s in segs:
        bs = breakables(s)
        total_b += len(bs)
        if not bs:
            if detail:
                print(f"\n{s['id']}: breakables=0 len={s['lengthTicks']}")
            continue
        xs = sorted(o["x"] for o in bs)
        gaps = x_gaps(xs)
        all_xs.extend(xs)
        all_gaps.extend(gaps)
        walls = vertical_wall_clusters(bs, x_tol=1.0, min_count=2)
        if walls:
            segs_with_walls += 1
            for w in walls:
                wall_counts[w["count"]] += 1
        x_span = max(xs) - min(xs) if len(xs) > 1 else 0.0
        x_span_per_seg.append(x_span)
        density.append(len(bs) / max(1, s["lengthTicks"]) * 100.0)

        if detail:
            print(
                f"\n{s['id']} len={s['lengthTicks']} weight={s.get('weight')} "
                f"diff={s.get('difficultyMin')}-{s.get('difficultyMax')}"
            )
            print(f"  breakables={len(bs)} xs={xs}")
            print(
                f"  x min/max/span={min(xs):.2f}/{max(xs):.2f}/{x_span:.2f} "
                f"gaps={ [round(g, 2) for g in gaps] }"
            )
            if gaps:
                print(
                    f"  gap min/med/max={min(gaps):.2f}/{median(gaps):.2f}/{max(gaps):.2f}"
                )
            # x±1 overlap counts
            clusters = cluster_same_x(bs, tol=1.0)
            print(f"  same-x±1 cluster sizes (≥2): {clusters or 'none'}")
            if walls:
                for w in walls:
                    print(
                        f"  WALL n={w['count']} x≈{w['x_span']} ys={w['ys']}"
                    )
            # also list solid/laser
            by_t = Counter(o.get("type") for o in (s.get("obstacles") or []))
            print(f"  all obs types: {dict(by_t)}")

    print(f"\n--- {theme} SUMMARY ---")
    print(f"segments: {len(segs)}")
    print(f"total breakables: {total_b}")
    print(f"avg breakables/seg: {total_b / len(segs) if segs else 0:.2f}")
    if all_xs:
        print(
            f"x overall: min={min(all_xs):.2f} med={median(all_xs):.2f} "
            f"max={max(all_xs):.2f}"
        )
    if all_gaps:
        print(
            f"adjacent x-gaps (within seg, sorted xs): "
            f"min={min(all_gaps):.2f} med={median(all_gaps):.2f} "
            f"max={max(all_gaps):.2f} mean={mean(all_gaps):.2f} "
            f"n={len(all_gaps)}"
        )
        # how many tiny gaps
        tiny = sum(1 for g in all_gaps if g <= 1.0)
        small = sum(1 for g in all_gaps if g <= 2.0)
        print(f"  gaps≤1.0: {tiny}/{len(all_gaps)}  gaps≤2.0: {small}/{len(all_gaps)}")
    if x_span_per_seg:
        print(
            f"x-span per seg (segs with breakables): "
            f"min={min(x_span_per_seg):.2f} med={median(x_span_per_seg):.2f} "
            f"max={max(x_span_per_seg):.2f} mean={mean(x_span_per_seg):.2f}"
        )
    print(f"segs with vertical walls (x±1, n≥2): {segs_with_walls}")
    print(f"wall size histogram: {dict(sorted(wall_counts.items()))}")
    if density:
        print(
            f"density (br/100ticks): med={median(density):.3f} mean={mean(density):.3f}"
        )
    return {
        "theme": theme,
        "segs": len(segs),
        "total_b": total_b,
        "gaps": all_gaps,
        "wall_counts": dict(wall_counts),
        "segs_with_walls": segs_with_walls,
    }


# Primary: scrapyard
scrap = summarize_theme("scrapyard", detail=True)

# Cross-theme report (summary only)
print("\n\n########## CROSS-THEME BREAKABLE SUMMARY ##########")
results = []
for th in themes:
    r = summarize_theme(th, detail=False)
    results.append(r)

print("\n\n=== CROSS-THEME TABLE ===")
print(f"{'theme':14} {'segs':>5} {'br':>5} {'gap_min':>8} {'gap_med':>8} {'gap_max':>8} {'walls_seg':>9} {'wall_hist'}")
for r in results:
    gaps = r["gaps"]
    if gaps:
        gmin, gmed, gmax = min(gaps), median(gaps), max(gaps)
    else:
        gmin = gmed = gmax = float("nan")
    print(
        f"{r['theme']:14} {r['segs']:5} {r['total_b']:5} "
        f"{gmin:8.2f} {gmed:8.2f} {gmax:8.2f} "
        f"{r['segs_with_walls']:9} {r['wall_counts']}"
    )

