# REQ-054 follow-up: fill boss phase movement/part axes + strip mini_* from segments.
# MidBoss section owns mini_* exclusively (Core Rng.Fork(6)).
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
path = ROOT / "GameData" / "waves.json"
data = json.loads(path.read_text(encoding="utf-8"))

# Structure: p0 basic / p1 movement+parts open / p2 enrage.
# Threat = ways * bulletSpeed / fireIntervalTicks must be strictly mono.
# Density (ways/interval) jumps into p2 for enrage readability.
# HP slightly reduced where pattern tax would stretch real TTK; stage1 stays 9000 (18s @500).

boss_phases = {
    "boss_stage1": {
        # Tutorial: teach phases clearly, gentlest enrage
        "hp": 9000,
        "phases": [
            {
                "pattern": "aimed",
                "hpEnterRatio": 1.0,
                "fireIntervalTicks": 55,
                "ways": 3,
                "bulletSpeed": 9.0,
                "movementPattern": "stationary",
                "movementAmplitude": 0,
                "movementPeriodTicks": 1,
                "partVulnerability": "coreOnly",
            },
            {
                "pattern": "spread",
                "hpEnterRatio": 0.667,
                "fireIntervalTicks": 48,
                "ways": 6,
                "bulletSpeed": 8.0,
                "movementPattern": "verticalSine",
                "movementAmplitude": 1.75,
                "movementPeriodTicks": 150,
                "partVulnerability": "all",
            },
            {
                "pattern": "rapid",
                "hpEnterRatio": 0.333,
                "fireIntervalTicks": 20,
                "ways": 3,
                "bulletSpeed": 14.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 2.0,
                "movementPeriodTicks": 100,
                "partVulnerability": "all",
            },
        ],
    },
    "boss_hive": {
        # Movement-heavy organic: big weave from p1 through enrage
        "hp": 14500,
        "phases": [
            {
                "pattern": "aimed",
                "hpEnterRatio": 1.0,
                "fireIntervalTicks": 50,
                "ways": 3,
                "bulletSpeed": 9.5,
                "movementPattern": "stationary",
                "movementAmplitude": 0,
                "movementPeriodTicks": 1,
                "partVulnerability": "coreOnly",
            },
            {
                "pattern": "spread",
                "hpEnterRatio": 0.667,
                "fireIntervalTicks": 42,
                "ways": 7,
                "bulletSpeed": 8.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 3.25,
                "movementPeriodTicks": 96,
                "partVulnerability": "all",
            },
            {
                "pattern": "rapid",
                "hpEnterRatio": 0.333,
                "fireIntervalTicks": 16,
                "ways": 3,
                "bulletSpeed": 14.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 3.5,
                "movementPeriodTicks": 72,
                "partVulnerability": "all",
            },
        ],
    },
    "boss_fortress": {
        # Fortress: slow heavy sway; density enrage more than mobility
        "hp": 18000,
        "phases": [
            {
                "pattern": "aimed",
                "hpEnterRatio": 1.0,
                "fireIntervalTicks": 46,
                "ways": 4,
                "bulletSpeed": 10.0,
                "movementPattern": "stationary",
                "movementAmplitude": 0,
                "movementPeriodTicks": 1,
                "partVulnerability": "coreOnly",
            },
            {
                "pattern": "spread",
                "hpEnterRatio": 0.667,
                "fireIntervalTicks": 40,
                "ways": 8,
                "bulletSpeed": 9.0,
                "movementPattern": "verticalSine",
                "movementAmplitude": 0.875,
                "movementPeriodTicks": 210,
                "partVulnerability": "all",
            },
            {
                "pattern": "rapid",
                "hpEnterRatio": 0.333,
                "fireIntervalTicks": 14,
                "ways": 3,
                "bulletSpeed": 15.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 1.25,
                "movementPeriodTicks": 150,
                "partVulnerability": "all",
            },
        ],
    },
    "boss_storm": {
        # Nebula storm: chaotic fast weave + extreme p2 density
        "hp": 22500,
        "phases": [
            {
                "pattern": "aimed",
                "hpEnterRatio": 1.0,
                "fireIntervalTicks": 42,
                "ways": 4,
                "bulletSpeed": 10.5,
                "movementPattern": "stationary",
                "movementAmplitude": 0,
                "movementPeriodTicks": 1,
                "partVulnerability": "coreOnly",
            },
            {
                "pattern": "spread",
                "hpEnterRatio": 0.667,
                "fireIntervalTicks": 36,
                "ways": 8,
                "bulletSpeed": 9.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 2.75,
                "movementPeriodTicks": 84,
                "partVulnerability": "all",
            },
            {
                "pattern": "rapid",
                "hpEnterRatio": 0.333,
                "fireIntervalTicks": 12,
                "ways": 3,
                "bulletSpeed": 16.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 3.25,
                "movementPeriodTicks": 60,
                "partVulnerability": "all",
            },
        ],
    },
    "boss_core": {
        # Finale hybrid: dense + mobile enrage
        "hp": 28000,
        "phases": [
            {
                "pattern": "aimed",
                "hpEnterRatio": 1.0,
                "fireIntervalTicks": 40,
                "ways": 4,
                "bulletSpeed": 11.0,
                "movementPattern": "stationary",
                "movementAmplitude": 0,
                "movementPeriodTicks": 1,
                "partVulnerability": "coreOnly",
            },
            {
                "pattern": "spread",
                "hpEnterRatio": 0.667,
                "fireIntervalTicks": 34,
                "ways": 7,
                "bulletSpeed": 10.5,
                "movementPattern": "verticalSine",
                "movementAmplitude": 2.25,
                "movementPeriodTicks": 100,
                "partVulnerability": "all",
            },
            {
                "pattern": "rapid",
                "hpEnterRatio": 0.333,
                "fireIntervalTicks": 12,
                "ways": 3,
                "bulletSpeed": 17.0,
                "movementPattern": "verticalSine",
                "movementAmplitude": 2.75,
                "movementPeriodTicks": 66,
                "partVulnerability": "all",
            },
        ],
    },
}

print("Threat check:")
for bid, cfg in boss_phases.items():
    prev = -1.0
    print(f"  {bid} hp={cfg['hp']}")
    for i, p in enumerate(cfg["phases"]):
        thr = p["ways"] * p["bulletSpeed"] / p["fireIntervalTicks"]
        dens = p["ways"] / p["fireIntervalTicks"]
        ok = thr > prev
        print(
            f"    p{i} threat={thr:.3f} dens={dens:.3f} "
            f"move={p['movementPattern']} amp={p['movementAmplitude']} "
            f"vul={p['partVulnerability']} {'OK' if ok else 'FAIL'}"
        )
        if not ok:
            raise SystemExit(f"threat mono fail {bid} p{i}")
        prev = thr

# Mid-tier anchors replace mini_* in segments (MidBoss owns mini_* exclusively)
mini_replace = {
    "mini_horror": "brood_spitter",
    "mini_destroyer": "zako_tank",
    "mini_crystal": "elite_sine",
    "mini_walker": "guardian_sphere",
}

updated = 0
for boss in data["bosses"]:
    bid = boss["id"]
    if bid not in boss_phases:
        continue
    cfg = boss_phases[bid]
    boss["hp"] = cfg["hp"]
    boss["phases"] = cfg["phases"]
    boss["phaseHpThresholds"] = [0.667, 0.333]
    updated += 1

stripped = 0
for seg in data["segments"]:
    spawns = seg.get("spawns") or []
    new_spawns = []
    changed = False
    for sp in spawns:
        eid = sp.get("enemyId", "")
        if eid.startswith("mini_"):
            rep = mini_replace.get(eid, "elite_sine")
            new_spawns.append({**sp, "enemyId": rep})
            stripped += 1
            changed = True
        else:
            new_spawns.append(sp)
    if changed:
        seg["spawns"] = new_spawns
        intent = seg.get("intent", "")
        if "중간보스" in intent:
            seg["intent"] = (
                intent.replace("중간보스 피날레", "중형 앵커 피날레")
                .replace("중간보스", "중형")
            )

path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Updated {updated} standard bosses; replaced {stripped} mini_* segment spawns")
