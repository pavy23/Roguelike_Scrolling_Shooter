# REQ-181: rebalance hidden bosses (HP + 4-phase monotonic vulnerability).
# Run from repo root: python Tools/BalanceSim/_req181_hidden_boss_rebalance.py
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PATH = ROOT / "GameData" / "waves.json"


def find_boss(data: dict, bid: str) -> tuple[int, dict]:
    for i, b in enumerate(data["bosses"]):
        if b["id"] == bid:
            return i, b
    raise KeyError(bid)


def check_reach(
    parts: dict[str, int],
    phases_vuln: list[set[str]],
    thresholds: list[float],
    total: int,
) -> None:
    ever: set[str] = set()
    for i, vuln in enumerate(phases_vuln):
        ever |= vuln
        removable = sum(parts[p] for p in ever)
        floor = total - removable
        next_t = thresholds[i] if i < len(thresholds) else 0.0
        needed = int(total * next_t) if next_t > 0 else 0
        ok = floor <= needed
        print(
            f"  phase{i}: rem={removable} floor={floor} "
            f"next_needed={needed} ok={ok}"
        )
        assert ok, (i, floor, needed)


def check_mono(phases: list[dict], name: str) -> None:
    ever_vuln: set[str] = set()
    for i, ph in enumerate(phases):
        now: set[str] = set()
        for r in ph["partRules"]:
            if r["active"] and not r["invulnerable"]:
                now.add(r["partId"])
            if r["partId"] in ever_vuln:
                assert r["active"] and not r["invulnerable"], (
                    f"{name} ph{i} re-invuln {r['partId']}"
                )
        ever_vuln |= now
        print(f"  {name} mono ph{i}: vuln={sorted(now)}")
    print(f"  {name} mono OK")


def lev_rule(pid: str, active: bool, invuln: bool, attack=None) -> dict:
    r = {"partId": pid, "active": active, "invulnerable": invuln}
    if attack is not None:
        r["attack"] = attack
    return r


def brood_rule(pid: str, active: bool, invuln: bool, attack=None) -> dict:
    r = {"partId": pid, "active": active, "invulnerable": invuln}
    if attack is not None:
        r["attack"] = attack
    return r


def main() -> None:
    data = json.loads(PATH.read_text(encoding="utf-8"))

    # ============================================================
    # LEVIATHAN — form1 50200 + form2 12600 = 62800
    # Req181 harness ≈615 DPS → ~102s (band 90–120).
    # ============================================================
    li, lev = find_boss(data, "boss_leviathan")

    lev_parts = {
        "turret_spine": 6275,
        "head_cowl": 5883,
        "blade_limb_upper": 2941,
        "blade_limb_lower": 2941,
        "rear_engine": 4706,
        "lower_launcher": 6275,
        "shield_emitter": 5491,
        "railgun": 3922,
        "rib_gate": 4902,
        "core": 6864,
    }
    assert sum(lev_parts.values()) == 50200

    # Half of warship core beam fullHalfWidth 5.0 → 2.5
    railgun_laser = {
        "type": "laser",
        "intervalTicks": 420,
        "laser": {
            "cycleIntervalTicks": 420,
            "telegraphTicks": 180,
            "firingTicks": 12,
            "sustainTicks": 72,
            "dissipateTicks": 20,
            "startOffsetX": -3.8984375,
            "startOffsetY": 0.0,
            "endOffsetX": -32.0,
            "endOffsetY": 0.0,
            "thinHalfWidth": 0.5,
            "fullHalfWidth": 2.5,
            "damage": 1,
        },
    }

    for p in lev["parts"]:
        p["hp"] = lev_parts[p["id"]]
        if p["id"] in ("blade_limb_upper", "blade_limb_lower"):
            # No melee this pass — pure HP targets when opened.
            p.pop("attack", None)

    lev["hp"] = 50200

    all_lev_ids = [
        "turret_spine",
        "head_cowl",
        "blade_limb_upper",
        "blade_limb_lower",
        "rear_engine",
        "lower_launcher",
        "shield_emitter",
        "railgun",
        "rib_gate",
        "core",
    ]

    def lev_rules(vuln_set: set[str], attacks: dict | None = None) -> list[dict]:
        attacks = attacks or {}
        out = []
        for pid in all_lev_ids:
            if pid in vuln_set:
                out.append(lev_rule(pid, True, False, attacks.get(pid)))
            else:
                out.append(lev_rule(pid, False, True))
        return out

    ph0_vuln = {
        "turret_spine",
        "head_cowl",
        "rear_engine",
        "lower_launcher",
        "shield_emitter",
    }
    ph1_vuln = ph0_vuln | {
        "railgun",
        "blade_limb_upper",
        "blade_limb_lower",
    }
    ph2_vuln = ph1_vuln | {"rib_gate", "core"}

    print("Leviathan reachability:")
    check_reach(lev_parts, [ph0_vuln, ph1_vuln, ph2_vuln], [0.7, 0.3, 0.0], 50200)

    ph0_attacks = {
        "turret_spine": {
            "type": "aimedSpread",
            "intervalTicks": 64,
            "ways": 4,
            "bulletSpeed": 7.0,
        },
        "head_cowl": {
            "type": "aimedSpread",
            "intervalTicks": 56,
            "ways": 5,
            "bulletSpeed": 7.0,
        },
        "lower_launcher": {
            "type": "radialSpread",
            "intervalTicks": 80,
            "ways": 6,
            "bulletSpeed": 6.5,
        },
    }
    ph1_attacks = {
        "turret_spine": {
            "type": "aimedSpread",
            "intervalTicks": 56,
            "ways": 4,
            "bulletSpeed": 7.0,
        },
        "head_cowl": {
            "type": "aimedSpread",
            "intervalTicks": 48,
            "ways": 5,
            "bulletSpeed": 7.2,
        },
        "lower_launcher": {
            "type": "radialSpread",
            "intervalTicks": 64,
            "ways": 7,
            "bulletSpeed": 6.8,
        },
        "railgun": railgun_laser,
    }
    ph2_attacks = {
        "turret_spine": {
            "type": "aimedSpread",
            "intervalTicks": 44,
            "ways": 5,
            "bulletSpeed": 7.5,
        },
        "head_cowl": {
            "type": "aimedSpread",
            "intervalTicks": 40,
            "ways": 6,
            "bulletSpeed": 7.5,
        },
        "lower_launcher": {
            "type": "radialSpread",
            "intervalTicks": 52,
            "ways": 8,
            "bulletSpeed": 7.0,
        },
        "railgun": railgun_laser,
        "core": {
            "type": "radialSpread",
            "intervalTicks": 48,
            "ways": 8,
            "bulletSpeed": 7.5,
        },
    }

    # Body density: ph0 5.4 → ph1 9.0 → ph2 13.1 → form2 ~16/s
    lev["phases"] = [
        {
            "pattern": "aimed",
            "fireIntervalTicks": 56,
            "ways": 5,
            "bulletSpeed": 7.0,
            "projectileKind": "normal",
            "movementPattern": "stationary",
            "movementAmplitude": 0,
            "movementPeriodTicks": 1,
            "partVulnerability": "legacy",
            "partRules": lev_rules(ph0_vuln, ph0_attacks),
        },
        {
            "pattern": "radial",
            "fireIntervalTicks": 40,
            "ways": 6,
            "bulletSpeed": 7.2,
            "projectileKind": "heavy",
            "movementPattern": "stationary",
            "movementAmplitude": 0,
            "movementPeriodTicks": 1,
            "partVulnerability": "legacy",
            "hpThreshold": 0.7,
            "partRules": lev_rules(ph1_vuln, ph1_attacks),
        },
        {
            "pattern": "aimed",
            "fireIntervalTicks": 32,
            "ways": 7,
            "bulletSpeed": 7.5,
            "projectileKind": "heavy",
            "movementPattern": "verticalSine",
            "movementAmplitude": 1.75,
            "movementPeriodTicks": 96,
            "partVulnerability": "legacy",
            "hpThreshold": 0.3,
            "partRules": lev_rules(ph2_vuln, ph2_attacks),
        },
    ]

    lev["form2"] = {
        "id": "boss_leviathan_drone",
        "transitionTicks": 180,
        "hp": 12600,
        "halfWidth": 2.5,
        "halfHeight": 2.5,
        "holdX": 10.0,
        "phases": [
            {
                "pattern": "aimed",
                "fireIntervalTicks": 22,
                "ways": 6,
                "bulletSpeed": 8.0,
                "projectileKind": "heavy",
                "movementPattern": "lungeReturn",
                "movementAmplitude": 5.0,
                "movementPeriodTicks": 84,
                "movementTelegraphTicks": 16,
                "partVulnerability": "all",
            },
            {
                "pattern": "burst",
                "fireIntervalTicks": 16,
                "ways": 5,
                "bulletSpeed": 8.5,
                "telegraphTicks": 10,
                "projectileKind": "heavy",
                "movementPattern": "lungeReturn",
                "movementAmplitude": 6.0,
                "movementPeriodTicks": 68,
                "movementTelegraphTicks": 12,
                "partVulnerability": "all",
                "hpThreshold": 0.5,
            },
        ],
    }
    data["bosses"][li] = lev

    # ============================================================
    # BROODMOTHER — form1 48500 + form2 12100 = 60600
    # Req181 harness ≈595 DPS → ~102s (band 90–120).
    # ============================================================
    bi, brood = find_boss(data, "boss_broodmother")

    brood_parts = {
        "tentacle_left": 2205,
        "tentacle_right": 2205,
        "sac_left": 7716,
        "sac_right": 7716,
        "sac_lower": 8267,
        "maw": 13227,
        "heart_core": 7164,
    }
    assert sum(brood_parts.values()) == 48500

    for p in brood["parts"]:
        p["hp"] = brood_parts[p["id"]]

    brood["hp"] = 48500

    def make_laser(
        end_y: float,
        thin: float = 0.25,
        full: float = 1.25,
        start_x: float = -1.5,
        cycle: int = 400,
    ) -> dict:
        return {
            "type": "laser",
            "intervalTicks": cycle,
            "laser": {
                "cycleIntervalTicks": cycle,
                "telegraphTicks": 160,
                "firingTicks": 8,
                "sustainTicks": 48,
                "dissipateTicks": 16,
                "startOffsetX": start_x,
                "startOffsetY": 0.0,
                "endOffsetX": -32.0,
                "endOffsetY": end_y,
                "thinHalfWidth": thin,
                "fullHalfWidth": full,
                "damage": 1,
            },
        }

    brood_lasers = {
        "tentacle_left": make_laser(18.4765625),
        "tentacle_right": make_laser(-18.4765625),
        "sac_left": make_laser(8.57421875),
        "sac_right": make_laser(-8.57421875),
        "maw": make_laser(0.0, thin=0.375, full=1.75, start_x=-3.0),
    }

    all_brood_ids = [
        "tentacle_left",
        "tentacle_right",
        "sac_left",
        "sac_right",
        "sac_lower",
        "maw",
        "heart_core",
    ]

    def brood_rules(vuln_set: set[str], attacks: dict | None = None) -> list[dict]:
        attacks = attacks or {}
        out = []
        for pid in all_brood_ids:
            if pid in vuln_set:
                out.append(brood_rule(pid, True, False, attacks.get(pid)))
            else:
                out.append(brood_rule(pid, False, True))
        return out

    # Absolute rule: sacs stay vulnerable once opened (never re-invulnerable).
    b_ph0 = {
        "tentacle_left",
        "tentacle_right",
        "sac_left",
        "sac_right",
        "sac_lower",
    }
    b_ph1 = b_ph0 | {"maw"}
    b_ph2 = b_ph1 | {"heart_core"}

    print("Broodmother reachability:")
    check_reach(brood_parts, [b_ph0, b_ph1, b_ph2], [0.7, 0.3, 0.0], 48500)

    spawn = {
        "type": "spawnEnemy",
        "intervalTicks": 480,
        "spawnEnemyId": "zako_straight",
    }
    spawn_slow = {
        "type": "spawnEnemy",
        "intervalTicks": 600,
        "spawnEnemyId": "zako_straight",
    }

    b_ph0_attacks = {
        "tentacle_left": {
            "type": "aimedSpread",
            "intervalTicks": 72,
            "ways": 4,
            "bulletSpeed": 6.5,
        },
        "tentacle_right": {
            "type": "aimedSpread",
            "intervalTicks": 80,
            "ways": 4,
            "bulletSpeed": 6.5,
        },
        "sac_left": spawn,
        "sac_right": spawn,
        "sac_lower": spawn,
    }
    # ph2 (index 1): forward 5-way lasers; sacs remain vulnerable.
    b_ph1_attacks = {
        "tentacle_left": brood_lasers["tentacle_left"],
        "tentacle_right": brood_lasers["tentacle_right"],
        "sac_left": brood_lasers["sac_left"],
        "sac_right": brood_lasers["sac_right"],
        "sac_lower": spawn_slow,
        "maw": brood_lasers["maw"],
    }
    b_ph2_attacks = {
        "tentacle_left": brood_lasers["tentacle_left"],
        "tentacle_right": brood_lasers["tentacle_right"],
        "sac_left": brood_lasers["sac_left"],
        "sac_right": brood_lasers["sac_right"],
        "sac_lower": spawn_slow,
        # Suction signature retained in ph3 (BalanceSim REQ-116 + feel).
        "maw": {
            "type": "suction",
            "intervalTicks": 1,
            "effectSpeed": 2.0,
            "effectMaxSpeed": 2.5,
            "effectOffsetX": -3.296875,
            "effectOffsetY": -0.3984375,
        },
        "heart_core": {
            "type": "radialSpread",
            "intervalTicks": 40,
            "ways": 8,
            "bulletSpeed": 7.5,
        },
    }

    brood["phases"] = [
        {
            "pattern": "radial",
            "fireIntervalTicks": 56,
            "ways": 5,
            "bulletSpeed": 7.0,
            "projectileKind": "normal",
            "movementPattern": "stationary",
            "movementAmplitude": 0,
            "movementPeriodTicks": 1,
            "partVulnerability": "legacy",
            "partRules": brood_rules(b_ph0, b_ph0_attacks),
        },
        {
            "pattern": "aimed",
            "fireIntervalTicks": 40,
            "ways": 6,
            "bulletSpeed": 7.2,
            "projectileKind": "heavy",
            "movementPattern": "stationary",
            "movementAmplitude": 0,
            "movementPeriodTicks": 1,
            "partVulnerability": "legacy",
            "hpThreshold": 0.7,
            "partRules": brood_rules(b_ph1, b_ph1_attacks),
        },
        {
            "pattern": "radial",
            "fireIntervalTicks": 32,
            "ways": 7,
            "bulletSpeed": 7.5,
            "projectileKind": "heavy",
            "movementPattern": "verticalSine",
            "movementAmplitude": 1.5,
            "movementPeriodTicks": 100,
            "partVulnerability": "legacy",
            "hpThreshold": 0.3,
            "partRules": brood_rules(b_ph2, b_ph2_attacks),
        },
    ]

    brood["form2"] = {
        "id": "boss_broodmother_spawn",
        "transitionTicks": 180,
        "hp": 12100,
        "halfWidth": 2.25,
        "halfHeight": 2.25,
        "holdX": 10.0,
        "phases": [
            {
                "pattern": "radial",
                "fireIntervalTicks": 22,
                "ways": 6,
                "bulletSpeed": 8.0,
                "projectileKind": "normal",
                "movementPattern": "figureEight",
                "movementAmplitude": 2.25,
                "movementPeriodTicks": 78,
                "partVulnerability": "all",
            },
            {
                "pattern": "aimed",
                "fireIntervalTicks": 16,
                "ways": 5,
                "bulletSpeed": 8.5,
                "projectileKind": "heavy",
                "movementPattern": "lungeReturn",
                "movementAmplitude": 5.5,
                "movementPeriodTicks": 68,
                "movementTelegraphTicks": 12,
                "partVulnerability": "all",
                "hpThreshold": 0.5,
            },
        ],
    }
    data["bosses"][bi] = brood

    print("\nBody density (bullets/s = ways * 60 / interval):")
    for name, phases in (
        ("levi", lev["phases"] + lev["form2"]["phases"]),
        ("brood", brood["phases"] + brood["form2"]["phases"]),
    ):
        for i, ph in enumerate(phases):
            dens = ph["ways"] * 60 / ph["fireIntervalTicks"]
            thr = ph.get("hpThreshold", 1.0)
            print(
                f"  {name} phase{i}: interval={ph['fireIntervalTicks']} "
                f"ways={ph['ways']} dens={dens:.2f}/s thr={thr} "
                f"move={ph['movementPattern']}"
            )

    print("\nMonotonic vulnerability:")
    check_mono(lev["phases"], "levi")
    check_mono(brood["phases"], "brood")

    PATH.write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print("\nWrote", PATH)
    print(
        "levi hp",
        lev["hp"],
        "form2",
        lev["form2"]["hp"],
        "parts",
        sum(p["hp"] for p in lev["parts"]),
    )
    print(
        "brood hp",
        brood["hp"],
        "form2",
        brood["form2"]["hp"],
        "parts",
        sum(p["hp"] for p in brood["parts"]),
    )


if __name__ == "__main__":
    main()
