"""Quantize non-1/256 y values in seg_hive_brood_wave (pre-existing parse blocker)."""
import json
from pathlib import Path

path = Path(__file__).resolve().parents[2] / "GameData" / "waves.json"
w = json.loads(path.read_text(encoding="utf-8"))
# 1.8*256=460.8 → 461; 0.6*256=153.6 → 154
fixes = {
    -1.8: -461 / 256,
    -0.6: -154 / 256,
    0.6: 154 / 256,
    1.8: 461 / 256,
}
for seg in w["segments"]:
    if seg["id"] != "seg_hive_brood_wave":
        continue
    for sp in seg["spawns"]:
        y = sp.get("y")
        if y in fixes:
            new_y = fixes[y]
            print(f"fixed y {y} -> {new_y} (su={new_y * 256})")
            sp["y"] = new_y

path.write_text(
    json.dumps(w, indent=2, ensure_ascii=False) + "\n",
    encoding="utf-8",
)
print("done")
