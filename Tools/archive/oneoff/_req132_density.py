#!/usr/bin/env python3
"""REQ-132: formation-aware bullet density for junk_roller / scrap_tumbler."""
from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
ENEMIES = ROOT / "GameData" / "enemies.json"

# Proposed A-plan intervals (ticks @ 60Hz)
JR_IV = 180  # 3.0s
ST_IV = 150  # 2.5s

# Conservative lifetime on screen (ticks) for fodder — used if unknown
# zigzag ~ speed 3.5-3.75, playfield width ~20u → ~5-7s on screen
DEFAULT_LIFE = 360  # 6s


def load():
    waves = json.loads(WAVES.read_text(encoding="utf-8"))
    enemies = json.loads(ENEMIES.read_text(encoding="utf-8"))
    segs = waves.get("segments") or waves.get("segmentPool") or []
    if isinstance(segs, dict):
        segs = list(segs.values()) if segs else []
    # waves may nest under stages / pool keys
    if not segs:
        # try common keys
        for k, v in waves.items():
            if k in ("schemaVersion", "stages", "bosses", "midBosses"):
                continue
            if isinstance(v, list) and v and isinstance(v[0], dict) and "spawns" in v[0]:
                segs = v
                break
            if isinstance(v, list) and v and isinstance(v[0], dict) and "id" in v[0]:
                # might be segment list without checking spawns
                if any("spawns" in x for x in v if isinstance(x, dict)):
                    segs = v
                    break
    return segs, enemies


def find_segments(waves_raw):
    """Recursively find segment-like dicts with id + spawns."""
    found = []

    def walk(obj):
        if isinstance(obj, dict):
            if "id" in obj and "spawns" in obj and isinstance(obj["spawns"], list):
                found.append(obj)
            for v in obj.values():
                walk(v)
        elif isinstance(obj, list):
            for x in obj:
                walk(x)

    walk(waves_raw)
    return found


def shots_per_enemy(interval: int, life: int) -> int:
    """How many shots one enemy fires while alive, first shot at `interval` after spawn.
    Core typically arms after first interval (or fires at interval). Use floor(life/interval).
    """
    if interval <= 0:
        return 0
    return max(0, life // interval)


def concurrent_fire_rate(spawns, enemy_id, interval, life=DEFAULT_LIFE):
    """Simulate concurrent shooters and bullets/sec peaks within segment local time."""
    events = []  # (tick, delta_active)
    fire_ticks = []
    for sp in spawns:
        if sp.get("enemyId") != enemy_id:
            continue
        t0 = int(sp["tick"])
        t1 = t0 + life
        events.append((t0, +1))
        events.append((t1, -1))
        # fire at t0+interval, t0+2*interval, ... while < t1
        t = t0 + interval
        while t < t1:
            fire_ticks.append(t)
            t += interval

    events.sort()
    active = 0
    peak_active = 0
    for _, d in events:
        active += d
        peak_active = max(peak_active, active)

    # peak bullets in any 60-tick window
    fire_ticks.sort()
    peak_window = 0
    for i, t in enumerate(fire_ticks):
        # count fires in [t, t+60)
        j = i
        while j < len(fire_ticks) and fire_ticks[j] < t + 60:
            j += 1
        peak_window = max(peak_window, j - i)

    total_shots = len(fire_ticks)
    length = max((int(sp["tick"]) for sp in spawns if sp.get("enemyId") == enemy_id), default=0) + life
    avg_bps = total_shots / (length / 60.0) if length > 0 else 0.0
    return {
        "count": sum(1 for sp in spawns if sp.get("enemyId") == enemy_id),
        "total_shots": total_shots,
        "peak_concurrent": peak_active,
        "peak_bps_1s": peak_window,  # shots in busiest 1s
        "avg_bps": round(avg_bps, 3),
        "per_enemy_rate": round(60.0 / interval, 3) if interval else 0,
    }


def main():
    waves_raw = json.loads(WAVES.read_text(encoding="utf-8"))
    segs = find_segments(waves_raw)
    print(f"segments found: {len(segs)}")

    theme_counts = defaultdict(lambda: defaultdict(int))
    early_segs = []
    formation_focus = [
        "seg_scrap_debris_line",
        "seg_scrap_center_breach",
        "seg_scrap_tumbler_pack",
    ]

    print("\n=== ALL segs with JR/ST ===")
    print(f"{'id':42} {'theme':12} dmin-dmax  w  len  JR  ST")
    for seg in sorted(segs, key=lambda s: (s.get("theme", "?"), s.get("difficultyMin", 0), s["id"])):
        jr = sum(1 for sp in seg.get("spawns", []) if sp.get("enemyId") == "junk_roller")
        st = sum(1 for sp in seg.get("spawns", []) if sp.get("enemyId") == "scrap_tumbler")
        if not (jr or st):
            continue
        theme = seg.get("theme", "?")
        dmin, dmax = seg.get("difficultyMin", 0), seg.get("difficultyMax", 0)
        w = seg.get("weight", 0)
        length = seg.get("lengthTicks", 0)
        theme_counts[theme]["JR"] += jr
        theme_counts[theme]["ST"] += st
        theme_counts[theme]["segs"] += 1
        print(f"{seg['id']:42} {theme:12} {dmin}-{dmax:<3} {w:3} {length:4} {jr:3} {st:3}")
        if dmax <= 3 or seg["id"] in formation_focus:
            early_segs.append(seg)

    print("\n=== Theme spawn totals ===")
    for th, c in sorted(theme_counts.items()):
        print(th, dict(c))

    print("\n=== Formation density @ A-plan (JR=180, ST=150, life=360t) ===")
    for sid in formation_focus:
        seg = next((s for s in segs if s["id"] == sid), None)
        if not seg:
            print(sid, "NOT FOUND")
            continue
        spawns = seg["spawns"]
        length = seg.get("lengthTicks", 600)
        jr = concurrent_fire_rate(spawns, "junk_roller", JR_IV)
        st = concurrent_fire_rate(spawns, "scrap_tumbler", ST_IV)
        # combined fire ticks for peak
        fire_ticks = []
        for sp in spawns:
            eid = sp.get("enemyId")
            if eid == "junk_roller":
                iv, life = JR_IV, DEFAULT_LIFE
            elif eid == "scrap_tumbler":
                iv, life = ST_IV, DEFAULT_LIFE
            else:
                continue
            t0 = int(sp["tick"])
            t = t0 + iv
            while t < t0 + life:
                fire_ticks.append(t)
                t += iv
        fire_ticks.sort()
        peak_1s = 0
        for i, t in enumerate(fire_ticks):
            j = i
            while j < len(fire_ticks) and fire_ticks[j] < t + 60:
                j += 1
            peak_1s = max(peak_1s, j - i)
        # peak concurrent shooters (both types)
        events = []
        for sp in spawns:
            eid = sp.get("enemyId")
            if eid not in ("junk_roller", "scrap_tumbler"):
                continue
            t0 = int(sp["tick"])
            events.append((t0, +1))
            events.append((t0 + DEFAULT_LIFE, -1))
        events.sort()
        active = peak_c = 0
        for _, d in events:
            active += d
            peak_c = max(peak_c, active)

        total_shots = len(fire_ticks)
        # segment duration for avg: from first spawn to last spawn+life, capped by length
        times = [int(sp["tick"]) for sp in spawns if sp.get("enemyId") in ("junk_roller", "scrap_tumbler")]
        if times:
            window = min(length, max(times) + DEFAULT_LIFE) - min(times)
            avg = total_shots / (window / 60.0) if window > 0 else 0
        else:
            avg = 0

        print(f"\n{sid}  length={length}t ({length/60:.1f}s)")
        print(f"  JR: n={jr['count']} total_shots={jr['total_shots']} peak_conc={jr['peak_concurrent']} "
              f"peak_bps={jr['peak_bps_1s']} avg_bps={jr['avg_bps']}")
        print(f"  ST: n={st['count']} total_shots={st['total_shots']} peak_conc={st['peak_concurrent']} "
              f"peak_bps={st['peak_bps_1s']} avg_bps={st['avg_bps']}")
        print(f"  COMBINED: total_shots={total_shots} peak_concurrent_shooters={peak_c} "
              f"peak_bps_1s={peak_1s} avg_bps={avg:.3f}")
        # naive if all fire every 3s simultaneously: n * 60/180
        n = jr["count"] + st["count"]
        naive = n * (60.0 / 180.0)
        print(f"  naive all-JR-rate ({n} units @180t): {naive:.2f} bps (upper-ish if simultaneous)")

    # Also check early scrap segs density
    print("\n=== Early scrap (dMax<=2) combined density ===")
    for seg in segs:
        if seg.get("theme") != "scrapyard":
            continue
        if seg.get("difficultyMax", 99) > 2:
            continue
        spawns = seg["spawns"]
        fire_ticks = []
        n_jr = n_st = 0
        for sp in spawns:
            eid = sp.get("enemyId")
            if eid == "junk_roller":
                iv, n_jr = JR_IV, n_jr + 1
            elif eid == "scrap_tumbler":
                iv, n_st = ST_IV, n_st + 1
            else:
                continue
            t0 = int(sp["tick"])
            t = t0 + iv
            while t < t0 + DEFAULT_LIFE:
                fire_ticks.append(t)
                t += iv
        if not fire_ticks:
            continue
        fire_ticks.sort()
        peak_1s = 0
        for i, t in enumerate(fire_ticks):
            j = i
            while j < len(fire_ticks) and fire_ticks[j] < t + 60:
                j += 1
            peak_1s = max(peak_1s, j - i)
        print(f"  {seg['id']:40} JR={n_jr} ST={n_st} total_shots={len(fire_ticks)} peak_bps={peak_1s}")

    # Compare to late turret baseline
    print("\n=== Late scrap turret baseline (for comparison) ===")
    for seg in segs:
        if seg.get("theme") != "scrapyard":
            continue
        if seg.get("difficultyMin", 0) < 2:
            continue
        tur = sum(1 for sp in seg.get("spawns", []) if sp.get("enemyId") in ("turret_ground", "turret_ceiling", "elite_sine"))
        if tur:
            # turret 90, elite 120
            fire_ticks = []
            for sp in seg.get("spawns", []):
                eid = sp.get("enemyId")
                if eid in ("turret_ground", "turret_ceiling"):
                    iv = 90
                elif eid == "elite_sine":
                    iv = 120
                else:
                    continue
                t0 = int(sp["tick"])
                # turrets live longer
                life = 480
                t = t0 + iv
                while t < t0 + life:
                    fire_ticks.append(t)
                    t += iv
            fire_ticks.sort()
            peak_1s = 0
            for i, t in enumerate(fire_ticks):
                j = i
                while j < len(fire_ticks) and fire_ticks[j] < t + 60:
                    j += 1
                peak_1s = max(peak_1s, j - i)
            print(f"  {seg['id']:40} turrets+elite={tur} total_shots={len(fire_ticks)} peak_bps={peak_1s}")


if __name__ == "__main__":
    main()
