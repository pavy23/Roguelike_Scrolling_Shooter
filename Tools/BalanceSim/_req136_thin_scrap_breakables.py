#!/usr/bin/env python3
"""
REQ-136: thin scrapyard breakable count (~30–40%) and re-scatter remaining.

Size half is NOT in GameData — Core uses global ObstacleHalfWidth/Height
(= 0.5 world units). This script only reduces counts + preserves intentional
walls + re-evens X distribution (REQ-129 scatter retained).

Only mutates GameData/waves.json.
"""
from __future__ import annotations

import json
from collections import Counter, defaultdict
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
ENEMIES = ROOT / "GameData" / "enemies.json"

OBS_HALF = 0.5  # code constant — size change not in JSON
X_MIN, X_MAX = 10.5, 19.5


def is_scrap(seg: dict) -> bool:
    return (
        seg.get("theme") == "scrapyard"
        or "scrap" in seg.get("id", "")
        or "theme=scrapyard" in seg.get("intent", "")
    )


def snap025(v: float) -> float:
    return round(v * 4.0) / 4.0


def breakables(seg: dict) -> list[dict]:
    return [o for o in (seg.get("obstacles") or []) if o.get("type") == "breakable"]


def set_xy(o: dict, x: float, y: float | None = None) -> None:
    o["x"] = snap025(float(x))
    if y is not None:
        o["y"] = snap025(float(y))


def even_xs(n: int, lo: float, hi: float) -> list[float]:
    if n <= 1:
        return [snap025((lo + hi) / 2)]
    return [snap025(lo + i * (hi - lo) / (n - 1)) for i in range(n)]


def x_gaps(xs: list[float]) -> list[float]:
    s = sorted(xs)
    return [s[i + 1] - s[i] for i in range(len(s) - 1)]


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


def summarize(segments: list[dict], label: str) -> dict:
    segs = [s for s in segments if is_scrap(s)]
    all_gaps = []
    walls = Counter()
    segs_walls = 0
    total_b = 0
    spans = []
    cover_obs = 0
    cover_segs = 0
    late_mid = 0
    mid_lane = 0
    print(f"\n=== {label} scrapyard ===")
    per = {}
    for s in segs:
        bs = breakables(s)
        total_b += len(bs)
        cov = sum(1 for o in bs if o.get("blocksEnemyBullets"))
        cover_obs += cov
        if cov:
            cover_segs += 1
        for o in bs:
            if abs(float(o["y"])) <= 4:
                mid_lane += 1
            if (
                s.get("difficultyMin", 1) >= 2
                and o.get("blocksEnemyBullets")
                and abs(float(o["y"])) <= 3.0
            ):
                late_mid += 1
        if not bs:
            print(f"  {s['id']}: n=0")
            per[s["id"]] = 0
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
        per[s["id"]] = len(bs)
        print(
            f"  {s['id']}: n={len(bs)} cover={cov} "
            f"xs={sorted(xs)} span={max(xs)-min(xs):.2f} "
            f"gaps={[round(g,2) for g in gaps]} walls={wc or '-'}"
        )
    stats = {
        "total": total_b,
        "cover_obs": cover_obs,
        "cover_segs": cover_segs,
        "late_mid": late_mid,
        "mid_lane": mid_lane,
        "walls_segs": segs_walls,
        "walls_hist": dict(walls),
        "per": per,
    }
    if all_gaps:
        tiny = sum(1 for g in all_gaps if g <= 1.0)
        stats.update(
            {
                "gap_min": min(all_gaps),
                "gap_med": median(all_gaps),
                "gap_max": max(all_gaps),
                "gaps_le1": tiny,
                "gaps_n": len(all_gaps),
                "span_med": median(spans),
            }
        )
        print(
            f"  TOTAL br={total_b} gap min/med/max="
            f"{min(all_gaps):.2f}/{median(all_gaps):.2f}/{max(all_gaps):.2f} "
            f"gaps≤1={tiny}/{len(all_gaps)} walls_segs={segs_walls} hist={dict(walls)} "
            f"span med={median(spans):.2f} cover={cover_obs}/{cover_segs} "
            f"late_mid={late_mid} mid_lane={mid_lane}"
        )
    return stats


def replace_breakables(seg: dict, new_bs: list[dict]) -> None:
    """Replace breakable entries, keep solids/lasers in order (solids first-ish)."""
    others = [o for o in (seg.get("obstacles") or []) if o.get("type") != "breakable"]
    # Keep solids first then breakables (historical order style)
    solids = [o for o in others if o.get("type") == "solid"]
    rest = [o for o in others if o.get("type") != "solid"]
    seg["obstacles"] = solids + new_bs + rest


def make_br(x: float, y: float, hp: int, cover: bool = False) -> dict:
    o: dict = {"type": "breakable", "x": snap025(x), "y": snap025(y), "hp": int(hp)}
    if cover:
        o["blocksEnemyBullets"] = True
    return o


def apply_thinned(segments: list[dict]) -> list[str]:
    """Hand layouts: ~35% fewer breakables, keep intentional walls, re-scatter."""
    by_id = {s["id"]: s for s in segments}
    notes: list[str] = []

    def set_seg(sid: str, layout: list[dict], note: str) -> None:
        replace_breakables(by_id[sid], layout)
        notes.append(f"{sid}: {note} n={len(layout)}")

    # --- Early teaching: pure scatter, fewer props, mid-lane cover preserved ---

    # debris_line 5→3: keep mid-path cover teaching
    set_seg(
        "seg_scrap_debris_line",
        [
            make_br(11.5, 2.5, 25, cover=True),
            make_br(15.0, -1.75, 30, cover=True),
            make_br(18.5, 1.75, 35, cover=True),
        ],
        "5→3 even scatter + cover on player line",
    )

    # pipe_dash 6→3
    set_seg(
        "seg_scrap_pipe_dash",
        [
            make_br(11.5, 4.0, 22, cover=True),
            make_br(15.5, -1.75, 34),
            make_br(19.0, 3.0, 24, cover=True),
        ],
        "6→3 even scatter",
    )

    # skimmer_weave 6→3 (mask=2 center path free — y away from 0)
    set_seg(
        "seg_scrap_skimmer_weave",
        [
            make_br(11.5, 3.25, 30, cover=True),
            make_br(15.5, -3.25, 30),
            make_br(19.0, 1.75, 40, cover=True),
        ],
        "6→3 even (mask=2 safe)",
    )

    # zigzag_posts 6→3
    set_seg(
        "seg_scrap_zigzag_posts",
        [
            make_br(11.0, 3.75, 24, cover=True),
            make_br(15.0, -1.75, 22),
            make_br(19.0, 3.75, 28, cover=True),
        ],
        "6→3 even scatter",
    )

    # shard_field 8→4 (early dense → thin)
    set_seg(
        "seg_scrap_shard_field",
        [
            make_br(10.5, 4.25, 22, cover=True),
            make_br(13.5, -3.5, 24),
            make_br(16.5, 0.75, 28, cover=True),
            make_br(19.5, -2.0, 32, cover=True),
        ],
        "8→4 even scatter",
    )

    # center_breach 7→5: KEEP dig-wall 3@x=14 + 2 scatter
    set_seg(
        "seg_scrap_center_breach",
        [
            make_br(11.0, 3.25, 30),
            make_br(14.0, 2.0, 35, cover=True),
            make_br(14.0, 0.0, 32),
            make_br(14.0, -2.0, 30, cover=True),
            make_br(18.5, -2.5, 25, cover=True),
        ],
        "7→5 dig-wall3@14 + 2 scatter",
    )

    # rail_split: KEEP intentional top/bottom rails (3 pairs) — no count cut
    # (already correct; rewrite same layout to mark intent)
    set_seg(
        "seg_scrap_rail_split",
        [
            make_br(12.0, 3.5, 26, cover=True),
            make_br(12.0, -3.5, 26),
            make_br(15.0, 3.5, 28),
            make_br(15.0, -3.5, 28, cover=True),
            make_br(18.0, 3.5, 26, cover=True),
            make_br(18.0, -3.5, 26),
        ],
        "KEEP 6 rail pairs (intent wall)",
    )

    # --- Mid/late cover lines ---

    # junk_corridor 5→3 all cover mid-lane (REQ-103 tutorial props)
    set_seg(
        "seg_scrap_junk_corridor",
        [
            make_br(12.0, 1.75, 35, cover=True),
            make_br(15.5, -2.0, 40, cover=True),
            make_br(19.0, 3.25, 42, cover=True),
        ],
        "5→3 mid-lane cover line",
    )

    # clean_kill_junk 5→3 mirror
    set_seg(
        "seg_scrap_clean_kill_junk",
        [
            make_br(12.0, 1.75, 35, cover=True),
            make_br(15.5, -2.0, 40, cover=True),
            make_br(19.0, 3.25, 42, cover=True),
        ],
        "5→3 mid-lane cover line",
    )

    # tumbler_pack 6→4: keep 1 cover pair @15.5 + 2 scatter
    set_seg(
        "seg_scrap_tumbler_pack",
        [
            make_br(12.0, 2.0, 50, cover=True),
            make_br(15.5, 4.25, 40, cover=True),
            make_br(15.5, -3.5, 40, cover=True),
            make_br(19.0, -2.5, 55),
        ],
        "6→4 pair@15.5 + 2 scatter",
    )

    # rust_gauntlet 7→5: keep cover pillars (2 pairs + mid post)
    set_seg(
        "seg_scrap_rust_gauntlet",
        [
            make_br(11.0, 2.0, 50, cover=True),
            make_br(11.0, -2.0, 50, cover=True),
            make_br(15.0, 0.0, 55, cover=True),
            make_br(19.0, 2.0, 48, cover=True),
            make_br(19.0, -2.0, 48, cover=True),
        ],
        "7→5 pillars 11/15/19 (intent cover)",
    )

    # clean_kill_corridor 7→5 mirror gauntlet
    set_seg(
        "seg_scrap_clean_kill_corridor",
        [
            make_br(11.0, 2.0, 50, cover=True),
            make_br(11.0, -2.0, 50, cover=True),
            make_br(15.0, 0.0, 55, cover=True),
            make_br(19.0, 2.0, 48, cover=True),
            make_br(19.0, -2.0, 48, cover=True),
        ],
        "7→5 pillars mirror",
    )

    # speed_spike: KEEP gate pair + center post (intent wall)
    set_seg(
        "seg_scrap_speed_spike",
        [
            make_br(12.0, 2.0, 30, cover=True),
            make_br(12.0, -2.0, 30, cover=True),
            make_br(16.0, 0.0, 28, cover=True),
        ],
        "KEEP 3 gate (intent wall)",
    )

    return notes


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
    print(f"cover gate: obs={cover_obs} segs={cover_segs} late_mid={late_mid}")
    if cover_obs < 20 or cover_segs < 6:
        fails.append(f"cover too thin obs={cover_obs} segs={cover_segs}")
    if late_mid < 8:
        fails.append(f"late mid-lane cover posts {late_mid} < 8")
    return fails


def check_bury(segments: list[dict]) -> list[str]:
    with open(ENEMIES, encoding="utf-8") as f:
        edata = json.load(f)
    sizes = {
        e["id"]: (float(e["halfWidth"]), float(e["halfHeight"]))
        for e in edata["enemies"]
    }
    issues = []
    for seg in segments:
        if not is_scrap(seg):
            continue
        all_obs = [
            o
            for o in (seg.get("obstacles") or [])
            if o.get("type") in ("breakable", "solid")
        ]
        for sp in seg.get("spawns") or []:
            eid = sp["enemyId"]
            ey = float(sp["y"])
            hw, hh = sizes.get(eid, (0.75, 0.6))
            for o in all_obs:
                oy = float(o["y"])
                # center-in-block
                if abs(ey - oy) < OBS_HALF - 0.05 and hh >= 0.7:
                    issues.append(
                        f"{seg['id']} {eid} y={ey} center-in {o['type']} "
                        f"@({o['x']},{oy})"
                    )
                # AABB soft (visual bury risk)
                if abs(ey - oy) < 0.35 and hh >= 0.9:
                    issues.append(
                        f"SOFT {seg['id']} {eid} y={ey} near {o['type']} y={oy}"
                    )
    return issues


def tag_intent(segments: list[dict]) -> None:
    for s in segments:
        if not is_scrap(s):
            continue
        intent = s.get("intent") or ""
        if "REQ136" not in intent:
            s["intent"] = (intent + " | REQ136 thin-breakables").strip(" |")


def main() -> None:
    with open(WAVES, encoding="utf-8") as f:
        data = json.load(f)

    segments = data["segments"]
    before = summarize(segments, "BEFORE")
    notes = apply_thinned(segments)
    tag_intent(segments)
    after = summarize(segments, "AFTER")

    print("\n--- layout notes ---")
    for n in notes:
        print(" ", n)

    fails = check_cover_gates(segments)
    bury = check_bury(segments)
    if bury:
        print(f"\nBury notes ({len(bury)}):")
        for b in bury[:40]:
            print(" ", b)
    else:
        print("\nBury notes: none")

    if fails:
        print("FAIL gates:", fails)
        raise SystemExit(1)

    reduction = 1.0 - after["total"] / before["total"] if before["total"] else 0
    print(
        f"\nCOUNT: {before['total']} → {after['total']} "
        f"({reduction*100:.1f}% reduction)"
    )
    print(
        "SIZE: obstacle half extents are Core config constants "
        f"(ObstacleHalfWidth/Height = {OBS_HALF}u) — NOT in GameData. "
        "Half-size visual must be handled by Presentation/Core (request)."
    )

    with open(WAVES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"Wrote {WAVES}")


if __name__ == "__main__":
    main()
