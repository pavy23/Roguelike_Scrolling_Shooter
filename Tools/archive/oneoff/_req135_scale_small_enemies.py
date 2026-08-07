#!/usr/bin/env python3
"""
REQ-135: scale small flying enemy hitboxes ×1.5.

Presentation scales sprites to halfWidth/halfHeight, so visual size == hitbox.
Only mutates GameData/enemies.json (GROK content ownership).

Selection criterion (size-based):
  - halfWidth <= 0.8 AND halfHeight <= 0.8
  - movement.pattern != static
  - hp < 50, not midBoss
  => pure small flying fodder (zako-class + theme skimmers)

Excluded on purpose (even if named in the human example list):
  - zako_tank / zako_sine_slow: already mid/large hitboxes
  - mid enemies, turrets, mini-bosses, bosses
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENEMIES = ROOT / "GameData" / "enemies.json"
QUANT = 256  # 1/256 world-unit (Core sub-unit)


def q(v: float) -> float:
    return round(v * QUANT) / QUANT


def is_small_flying(e: dict) -> bool:
    if "midBoss" in e:
        return False
    hw = float(e["halfWidth"])
    hh = float(e["halfHeight"])
    hp = int(e["hp"])
    pat = (e.get("movement") or {}).get("pattern", "")
    return hw <= 0.8 and hh <= 0.8 and pat != "static" and hp < 50


def main() -> None:
    with open(ENEMIES, encoding="utf-8") as f:
        data = json.load(f)

    changed = []
    for e in data["enemies"]:
        if not is_small_flying(e):
            continue
        old_w, old_h = float(e["halfWidth"]), float(e["halfHeight"])
        new_w, new_h = q(old_w * 1.5), q(old_h * 1.5)
        e["halfWidth"] = new_w
        e["halfHeight"] = new_h
        changed.append((e["id"], old_w, old_h, new_w, new_h, e["hp"]))

    with open(ENEMIES, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"REQ-135 scaled {len(changed)} small flying enemies ×1.5")
    for row in changed:
        print(
            f"  {row[0]:20s}  {row[1]:.6f}x{row[2]:.6f} -> "
            f"{row[3]:.6f}x{row[4]:.6f}  hp={row[5]}"
        )


if __name__ == "__main__":
    main()
