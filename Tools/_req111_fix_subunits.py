import json
from pathlib import Path
from decimal import Decimal, ROUND_HALF_UP

path = Path("GameData/waves.json")
data = json.loads(path.read_text(encoding="utf-8"))

def q(v):
    """Quantize to 1/256 world unit (subunits)."""
    d = Decimal(str(v)) * 256
    n = int(d.to_integral_value(rounding=ROUND_HALF_UP))
    return float(Decimal(n) / 256)

for boss in data["bosses"]:
    if boss.get("id") != "boss_fortress":
        continue
    boss["halfWidth"] = q(10.0)
    boss["halfHeight"] = q(5.0)
    for p in boss["parts"]:
        for k in ("offsetX", "offsetY", "halfWidth", "halfHeight"):
            if k in p:
                p[k] = q(p[k])
        # laser_sentry-like hitbox: 1.25 x 1.09375
        if p["id"].startswith("turret_"):
            p["halfWidth"] = q(1.25)
            p["halfHeight"] = q(1.09375)
    for k in ("originX", "originY"):
        if k in boss.get("warship", {}):
            boss["warship"][k] = q(boss["warship"][k])
    print("parts:")
    for p in boss["parts"]:
        print(p["id"], p["offsetX"], p["offsetY"], p["halfWidth"], p["halfHeight"], p["hp"])
    print("warship origin", boss["warship"]["originX"], boss["warship"]["originY"])
    break

path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("ok")
