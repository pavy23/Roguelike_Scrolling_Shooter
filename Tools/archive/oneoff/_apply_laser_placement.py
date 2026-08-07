#!/usr/bin/env python3
"""
REQ-088 follow-up: expand laser_sentry / prism_beamer wave placement.

Rules:
- laser_sentry: fortress theme (4 segs) + core (≥2 segs), 1–2 each
- prism_beamer: nebula theme (4 segs) + core, scrapyard minimal only
- Peak sources (laser enemies + laserEmitters) per segment template ≤ 4
- MaxLasers = 8 hard cap (runtime LaserCapacityExceeded)
- scrapyard: keep at most the existing single prism_beamer (stage-1 soft)
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"

# segment_id -> list of (enemy_id, tick, y) to ADD (not replace existing)
# Counts after apply are designed so fortress has laser_sentry in ≥4 segs,
# nebula has prism_beamer in ≥4 segs, core has both flavours.
ADDITIONS: dict[str, list[tuple[str, int, float]]] = {
    # --- fortress: laser_sentry (4 segments) ---
    # sentry_grid already has 1 @360/y4.5 + 2 emitters → add 1 more (sources 4)
    "seg_fortress_sentry_grid": [
        ("laser_sentry", 520, -4.5),
    ],
    # interceptor: 1 emitter → +2 sentries (sources 3)
    "seg_fortress_interceptor_assault": [
        ("laser_sentry", 240, 4.25),
        ("laser_sentry", 480, -4.25),
    ],
    # mortar_line: 2 emitters → +1 (sources 3)
    "seg_fortress_mortar_line": [
        ("laser_sentry", 400, 4.5),
    ],
    # turret_cross: 2 emitters → +1 (sources 3)
    "seg_fortress_turret_cross": [
        ("laser_sentry", 420, -4.5),
    ],
    # drone_lattice / armored_gate: 3 emitters already — skip (would push ≥4 easily)

    # --- nebula: prism_beamer (4 segments; ribbon already has 1) ---
    "seg_nebula_wisp_storm": [
        ("prism_beamer", 360, 4.25),
    ],
    "seg_nebula_echo_ribbon": [
        ("prism_beamer", 300, -4.25),
    ],
    "seg_nebula_void_moth_swarm": [
        ("prism_beamer", 280, 4.0),
    ],
    # prism_haze already thematic; 1 beamer (ribbon already covered as 4th? wait)
    # Count: ribbon (existing) + storm + echo + void_moth = 4. Add haze as 5th? user asked 4.
    # ribbon is existing #1; storm/echo/void = 3 more → 4 total. haze optional bonus.
    "seg_nebula_prism_haze": [
        ("prism_beamer", 200, 4.25),
        ("prism_beamer", 520, -4.25),
    ],

    # --- core: laser_sentry ≥2 segs + prism_beamer presence ---
    # guardian_wall: no emitters → 2 sentries
    "seg_core_guardian_wall": [
        ("laser_sentry", 200, 4.5),
        ("laser_sentry", 500, -4.5),
    ],
    # void_mix: no emitters → 1 sentry + 1 beamer (mixed core flavour)
    "seg_core_void_mix": [
        ("laser_sentry", 340, 4.25),
        ("prism_beamer", 460, -4.25),
    ],
    # rift_blades: no emitters → 1 beamer (soft laser intro in core rush)
    "seg_core_rift_blades": [
        ("prism_beamer", 320, 4.5),
    ],
    # phase_discs: 1 emitter → +1 sentry only (sources 2)
    "seg_core_phase_discs": [
        ("laser_sentry", 360, -4.5),
    ],
    # scrapyard: leave tumbler_pack single prism_beamer untouched (minimal)
}


def main() -> None:
    with open(WAVES, encoding="utf-8") as f:
        data = json.load(f)

    segs = {s["id"]: s for s in data["segments"]}
    for seg_id, adds in ADDITIONS.items():
        if seg_id not in segs:
            raise SystemExit(f"missing segment {seg_id}")
        spawns = segs[seg_id]["spawns"]
        existing = {
            (s["enemyId"], s["tick"], float(s["y"]))
            for s in spawns
            if s["enemyId"] in ("laser_sentry", "prism_beamer")
        }
        for eid, tick, y in adds:
            key = (eid, tick, float(y))
            if key in existing:
                print(f"  skip dup {seg_id} {key}")
                continue
            spawns.append({"tick": tick, "enemyId": eid, "y": y})
            print(f"  + {seg_id}: {eid} t={tick} y={y}")

    # Audit peak sources
    laser_enemies = {"laser_sentry", "prism_beamer"}
    print("\n=== post-apply audit ===")
    fortress_ls = 0
    nebula_pb = 0
    core_ls = 0
    core_pb = 0
    scrap_laser = 0
    peak = 0
    peak_id = ""
    for s in data["segments"]:
        le = [x for x in s["spawns"] if x["enemyId"] in laser_enemies]
        em = sum(1 for o in s.get("obstacles", []) if o.get("type") == "laserEmitter")
        sources = len(le) + em
        if sources > peak:
            peak = sources
            peak_id = s["id"]
        theme = s.get("theme", "")
        if any(x["enemyId"] == "laser_sentry" for x in le):
            if theme == "fortress":
                fortress_ls += 1
            elif theme == "core":
                core_ls += 1
        if any(x["enemyId"] == "prism_beamer" for x in le):
            if theme == "nebula":
                nebula_pb += 1
            elif theme == "core":
                core_pb += 1
            elif theme == "scrapyard":
                scrap_laser += 1
        if le:
            detail = [(x["enemyId"], x["tick"], x["y"]) for x in le]
            print(
                f"  {s['id']}: {detail} emitters={em} sources={sources}"
            )

    print(
        f"\nfortress laser_sentry segs={fortress_ls} "
        f"nebula prism_beamer segs={nebula_pb} "
        f"core laser_sentry segs={core_ls} core prism_beamer segs={core_pb} "
        f"scrapyard laser segs={scrap_laser}"
    )
    print(f"peak sources={peak} @ {peak_id}")

    assert fortress_ls >= 4, fortress_ls
    assert nebula_pb >= 4, nebula_pb
    assert core_ls >= 2, core_ls
    assert core_pb >= 1, core_pb
    assert scrap_laser <= 1, scrap_laser
    assert peak <= 4, (peak, peak_id)

    with open(WAVES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"\nwrote {WAVES}")


if __name__ == "__main__":
    main()
