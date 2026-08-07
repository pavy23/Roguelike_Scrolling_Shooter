import json
with open("GameData/waves.json", encoding="utf-8") as f:
    w = json.load(f)
for theme in ("fortress", "core"):
    segs = [s for s in w["segments"] if s.get("theme") == theme]
    print(theme, "segs", len(segs))
    for s in segs:
        late = s.get("difficultyMin", 1) >= 3
        n = len(s.get("spawns", []))
        print(f"  {s['id']:42} d={s['difficultyMin']}-{s['difficultyMax']} w={s['weight']:2} len={s['lengthTicks']:4} spawns={n:2} late={late}")
    print()
