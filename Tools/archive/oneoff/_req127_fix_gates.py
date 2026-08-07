#!/usr/bin/env python3
"""Post-REQ127 gate fixes:
- speed spikes stay short (REQ-103b: lengthTicks <= 400)
- boss-valley gap length - lastSpawn >= 120 (REQ-103a)
"""
from __future__ import annotations

import json
import random
from pathlib import Path
from statistics import mean, pstdev

ROOT = Path(__file__).resolve().parents[2]
WAVES = ROOT / "GameData" / "waves.json"
SPIKE_LEN = 400
VALLEY = 120
TICK = 60.0


def main() -> None:
    data = json.loads(WAVES.read_text(encoding="utf-8"))

    for sid in ("seg_scrap_speed_spike", "seg_core_speed_spike"):
        s = next(x for x in data["segments"] if x["id"] == sid)
        old = s["lengthTicks"]
        s["lengthTicks"] = SPIKE_LEN
        parts = [x.strip() for x in (s.get("intent") or "").split("|")]
        parts = [x for x in parts if x and not x.startswith("REQ127")]
        parts.append("REQ127 length clamp (spike short-band 400)")
        s["intent"] = " | ".join(parts)
        print(f"{sid}: {old} -> {SPIKE_LEN}")

    for s in data["segments"]:
        spawns = s.get("spawns") or []
        if not spawns:
            continue
        last = max(int(sp["tick"]) for sp in spawns)
        length = int(s["lengthTicks"])
        gap = length - last
        if gap >= VALLEY:
            continue
        need_last = length - VALLEY
        shifted = 0
        for sp in spawns:
            if int(sp["tick"]) > need_last:
                print(
                    f"  {s['id']}: tick {sp['tick']} -> {need_last} "
                    f"({sp.get('enemyId')})"
                )
                sp["tick"] = need_last
                shifted += 1
        new_last = max(int(sp["tick"]) for sp in spawns)
        print(
            f"{s['id']}: valley fix shifted={shifted} "
            f"last={new_last} gap={length - new_last}"
        )

    lengths = [int(s["lengthTicks"]) for s in data["segments"]]
    print(
        "length min/max/mean/stdev/ratio",
        min(lengths),
        max(lengths),
        round(mean(lengths), 1),
        round(pstdev(lengths), 1),
        round(max(lengths) / min(lengths), 2),
    )
    random.seed(42)

    def stage_stats(n: int):
        times = [
            sum(random.choice(lengths) for _ in range(n)) / TICK
            for _ in range(8000)
        ]
        return min(times), max(times), mean(times), pstdev(times)

    for label, n in (("early", 3), ("late", 5)):
        mn, mx, mu, sd = stage_stats(n)
        print(
            f"{label} n={n}: mean={mu:.1f}s stdev={sd:.1f} "
            f"range=[{mn:.1f},{mx:.1f}] ratio={mx / mn:.2f}"
        )
    print("late/early mean", stage_stats(5)[2] / stage_stats(3)[2])

    WAVES.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print("wrote", WAVES)


if __name__ == "__main__":
    main()
