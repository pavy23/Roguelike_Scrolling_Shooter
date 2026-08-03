"""REQ-139: scale boss_fortress + missile/laser + vertical staging anchors.

Phase 2 (anchors): group anchorOffsetY / anchorTravelTicks (Core af27d38).
Robot form still pending schema.
HP total locked at 19600 so combat duration does not balloon with size.
"""
from __future__ import annotations

import json
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATHS = [
    ROOT / "GameData" / "waves.json",
    ROOT / "Assets" / "Resources" / "GameData" / "waves.json",
]


def on_grid(value: float) -> bool:
    d = Decimal(str(value)) * 256
    return d == d.to_integral_value()


# Act staging relative to originY=0. Screen Y ∈ [-11.25, 11.25].
# Act1 sinks the hull so only the upper deck is in frame; act2 rises to centre.
# REQ-142: stern A relaxed −9→−8 (still submerged, y=0 stern hit kept).
GROUP_ANCHORS = {
    "stern": {"anchorOffsetY": -8.0, "anchorTravelTicks": 0},
    "hull": {"anchorOffsetY": 0.0, "anchorTravelTicks": 90},
    "bow": {"anchorOffsetY": 0.0, "anchorTravelTicks": 0},
}

# REQ-142: part bottoms sit on measured warship_hull deck line (offsetY = deck + halfH).
NEW_PARTS = [
    {
        "id": "engine",
        "offsetX": 5.0,
        # Deck@+5 = +5.0625 (superstructure); bottom on deck; A=−8 keeps y≈0 hit.
        "offsetY": 7.5625,
        "halfWidth": 3.5,
        "halfHeight": 2.5,
        "hp": 2200,
        # Phase 1 (stern midbossGate): missile-like slow aimed volleys.
        "attack": {
            "type": "aimedSpread",
            "intervalTicks": 48,
            "ways": 3,
            "bulletSpeed": 5.5,
        },
    },
    {
        "id": "turret_a",
        "offsetX": 4.0,
        "offsetY": 6.1875,
        "halfWidth": 1.5,
        "halfHeight": 1.25,
        "hp": 900,
        # Phase 2 (hull attritionLine): laser-heavy deck battery.
        "attack": {
            "type": "laser",
            "intervalTicks": 200,
            "laser": {
                "cycleIntervalTicks": 200,
                "telegraphTicks": 48,
                "firingTicks": 8,
                "sustainTicks": 56,
                "dissipateTicks": 12,
                "startOffsetX": -1.5,
                "startOffsetY": 0.0,
                "endOffsetX": -28.0,
                "endOffsetY": -3.0,
                "thinHalfWidth": 0.25,
                "fullHalfWidth": 0.75,
                "damage": 1,
            },
        },
    },
    {
        "id": "turret_b",
        "offsetX": 0.0,
        "offsetY": 6.6875,
        "halfWidth": 1.5,
        "halfHeight": 1.25,
        "hp": 900,
        "attack": {
            "type": "laser",
            "intervalTicks": 180,
            "laser": {
                "cycleIntervalTicks": 180,
                "telegraphTicks": 40,
                "firingTicks": 8,
                "sustainTicks": 48,
                "dissipateTicks": 12,
                "startOffsetX": -1.5,
                "startOffsetY": 0.0,
                "endOffsetX": -28.0,
                "endOffsetY": -1.5,
                "thinHalfWidth": 0.25,
                "fullHalfWidth": 0.75,
                "damage": 1,
            },
        },
    },
    {
        "id": "turret_c",
        "offsetX": -4.0,
        "offsetY": 3.875,
        "halfWidth": 1.5,
        "halfHeight": 1.25,
        "hp": 900,
        "attack": {
            "type": "laser",
            "intervalTicks": 160,
            "laser": {
                "cycleIntervalTicks": 160,
                "telegraphTicks": 36,
                "firingTicks": 6,
                "sustainTicks": 40,
                "dissipateTicks": 10,
                "startOffsetX": -1.5,
                "startOffsetY": 0.0,
                "endOffsetX": -28.0,
                "endOffsetY": 1.5,
                "thinHalfWidth": 0.25,
                "fullHalfWidth": 0.75,
                "damage": 1,
            },
        },
    },
    {
        "id": "turret_d",
        "offsetX": -8.0,
        "offsetY": 3.0,
        "halfWidth": 1.5,
        "halfHeight": 1.25,
        "hp": 900,
        "attack": {
            "type": "laser",
            "intervalTicks": 220,
            "laser": {
                "cycleIntervalTicks": 220,
                "telegraphTicks": 50,
                "firingTicks": 8,
                "sustainTicks": 60,
                "dissipateTicks": 14,
                "startOffsetX": -1.5,
                "startOffsetY": 0.0,
                "endOffsetX": -28.0,
                "endOffsetY": 3.0,
                "thinHalfWidth": 0.25,
                "fullHalfWidth": 0.75,
                "damage": 1,
            },
        },
    },
    {
        "id": "core",
        "offsetX": -11.0,
        "offsetY": 0.0,
        "halfWidth": 2.5,
        "halfHeight": 2.5,
        "hp": 13800,
        "isCore": True,
        # Final-core pressure until robot form schema lands.
        "attack": {
            "type": "radialSpread",
            "intervalTicks": 36,
            "ways": 9,
            "bulletSpeed": 11.0,
        },
    },
]


def collect_floats(obj, acc):
    if isinstance(obj, dict):
        for v in obj.values():
            collect_floats(v, acc)
    elif isinstance(obj, list):
        for v in obj:
            collect_floats(v, acc)
    elif isinstance(obj, float):
        acc.append(obj)
    elif isinstance(obj, int):
        pass
    return acc


def main() -> None:
    body_hw, body_hh = 17.0, 8.5
    coords = collect_floats(
        {"body": [body_hw, body_hh], "parts": NEW_PARTS}, []
    )
    bad = [v for v in coords if not on_grid(v)]
    if bad:
        raise SystemExit(f"off-grid floats: {bad}")

    for path in PATHS:
        data = json.loads(path.read_text(encoding="utf-8"))
        found = False
        for boss in data["bosses"]:
            if boss.get("id") != "boss_fortress":
                continue
            found = True
            boss["halfWidth"] = body_hw
            boss["halfHeight"] = body_hh
            if boss.get("holdX") != 12.0:
                raise SystemExit(f"unexpected holdX {boss.get('holdX')}")
            if boss.get("hp") != 19600:
                raise SystemExit(f"unexpected hp {boss.get('hp')}")
            boss["parts"] = NEW_PARTS
            part_sum = sum(p["hp"] for p in boss["parts"])
            if part_sum != boss["hp"]:
                raise SystemExit(f"parts sum {part_sum} != hp {boss['hp']}")
            warship = boss.get("warship") or {}
            groups = warship.get("groups") or []
            if len(groups) != 3:
                raise SystemExit(f"warship groups {len(groups)} != 3")
            for group in groups:
                gid = group.get("id")
                if gid not in GROUP_ANCHORS:
                    raise SystemExit(f"unknown group id {gid}")
                group.update(GROUP_ANCHORS[gid])
            break
        if not found:
            raise SystemExit(f"boss_fortress not found in {path}")
        path.write_text(
            json.dumps(data, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        print(f"wrote {path.relative_to(ROOT)}")

    print("body halfW/H", body_hw, body_hh)
    hold = 12.0
    for p in NEW_PARTS:
        wx = hold + p["offsetX"]
        wy = p["offsetY"]
        left = wx - p["halfWidth"]
        right = wx + p["halfWidth"]
        bot = wy - p["halfHeight"]
        top = wy + p["halfHeight"]
        at = p.get("attack") or {}
        print(
            f"  {p['id']:10} world=({wx:+.2f},{wy:+.2f}) "
            f"box=[{left:.2f}..{right:.2f}] y=[{bot:.2f}..{top:.2f}] "
            f"hp={p['hp']} type={at.get('type')}"
        )


if __name__ == "__main__":
    main()
