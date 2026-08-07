#!/usr/bin/env python3
"""Post-pass: nudge scrapyard breakable Y off large-enemy spawn Y (visual bury)."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
ENEMIES = ROOT / "GameData" / "enemies.json"
OBS_HALF = 0.5

# Signature walls / cover pillars keep shared Y by design
KEEP_IDS = {
    "seg_scrap_rail_split",
    "seg_scrap_rust_gauntlet",
    "seg_scrap_clean_kill_corridor",
    "seg_scrap_speed_spike",
    "seg_scrap_center_breach",  # dig wall intentionally crosses mid
}


def is_scrap(s: dict) -> bool:
    return (
        s.get("theme") == "scrapyard"
        or "scrap" in s.get("id", "")
        or "theme=scrapyard" in s.get("intent", "")
    )


def main() -> None:
    with open(ENEMIES, encoding="utf-8") as f:
        sizes = {
            e["id"]: float(e["halfHeight"]) for e in json.load(f)["enemies"]
        }
    with open(WAVES, encoding="utf-8") as f:
        data = json.load(f)

    nudges = 0
    for seg in data["segments"]:
        if not is_scrap(seg) or seg["id"] in KEEP_IDS:
            continue
        bad_ys = []
        for sp in seg.get("spawns") or []:
            hh = sizes.get(sp["enemyId"], 0.5)
            if hh >= 0.7:
                bad_ys.append(float(sp["y"]))
        for o in seg.get("obstacles") or []:
            if o.get("type") != "breakable":
                continue
            oy = float(o["y"])
            for ey in bad_ys:
                if abs(ey - oy) < OBS_HALF:
                    delta = 0.75 if oy >= 0 else -0.75
                    new_y = round((oy + delta) * 4) / 4
                    if abs(new_y) > 5.0:
                        new_y = round((oy - delta) * 4) / 4
                    print(f"{seg['id']}: y {oy} -> {new_y} (near enemy y={ey})")
                    o["y"] = new_y
                    nudges += 1
                    break

    with open(WAVES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"nudges={nudges}")


if __name__ == "__main__":
    main()
