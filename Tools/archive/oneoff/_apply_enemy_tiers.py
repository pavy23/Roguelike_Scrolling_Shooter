"""Apply 4-tier enemy redesign + wave intent/rhythm (GROK content, provisional §7).

Firepower anchors assume CODEX growth-curve nerf (slower level-ups):
  early main-only ~75–120 DPS; stage mid ~400/500/600/720/880;
  full-power harder to reach ~1400–1600 (not current 1880 god-run).
TTK targets use stage mid, not peak full-option melt.
"""
from __future__ import annotations

import json
from collections import Counter
from copy import deepcopy
from pathlib import Path

root = Path(__file__).resolve().parents[2]

# ---------------------------------------------------------------------------
# Tier catalog — size and HP move together (visual honesty).
# dropWeight: fodder less / big more; total stage EV kept in [10,16] band.
# ---------------------------------------------------------------------------
TIER = {
    # --- 잡몹: flash clear, rhythm + light capsule trickle ---
    # dropWeights: fodder thriftier than pre-19%-cut era, but high-weight
    # fodder segs still need enough EV for stage band [10,16]. Big tiers carry more.
    # half extents MUST be k/256 (Core ToSubUnits). Use dyadic decimals only.
    "lancer_dart": dict(
        tier="fodder", hp=6, scoreValue=50, dropWeight=2,
        halfWidth=0.4375, halfHeight=0.3125, contactDamage=1),
    "interceptor_rush": dict(
        tier="fodder", hp=6, scoreValue=55, dropWeight=2,
        halfWidth=0.5, halfHeight=0.375, contactDamage=1),
    "rift_blade": dict(
        tier="fodder", hp=6, scoreValue=60, dropWeight=2,
        halfWidth=0.5, halfHeight=0.3125, contactDamage=1),
    "zako_fast": dict(
        tier="fodder", hp=8, scoreValue=70, dropWeight=2,
        halfWidth=0.5, halfHeight=0.375, contactDamage=1),
    "wisp_spark": dict(
        tier="fodder", hp=8, scoreValue=75, dropWeight=2,
        halfWidth=0.5, halfHeight=0.4375, contactDamage=1),
    "pipe_rat": dict(
        tier="fodder", hp=10, scoreValue=80, dropWeight=3,
        halfWidth=0.5, halfHeight=0.375, contactDamage=1),
    "sting_hornet": dict(
        tier="fodder", hp=10, scoreValue=90, dropWeight=3,
        halfWidth=0.5625, halfHeight=0.4375, contactDamage=1),
    "rust_skimmer": dict(
        tier="fodder", hp=12, scoreValue=100, dropWeight=3,
        halfWidth=0.5625, halfHeight=0.4375, contactDamage=1),
    "zako_straight": dict(
        tier="fodder", hp=12, scoreValue=100, dropWeight=3,
        halfWidth=0.5625, halfHeight=0.4375, contactDamage=1),
    "junk_roller": dict(
        tier="fodder", hp=14, scoreValue=110, dropWeight=4,
        halfWidth=0.625, halfHeight=0.5, contactDamage=1),
    "zako_sine": dict(
        tier="fodder", hp=14, scoreValue=120, dropWeight=4,
        halfWidth=0.5625, halfHeight=0.4375, contactDamage=1),
    "spore_drifter": dict(
        tier="fodder", hp=14, scoreValue=120, dropWeight=4,
        halfWidth=0.625, halfHeight=0.5625, contactDamage=1),
    # --- 강화형: must aim briefly (early ~0.8–1.4s @100 DPS) ---
    "scrap_tumbler": dict(
        tier="reinforced", hp=80, scoreValue=220, dropWeight=5,
        halfWidth=0.875, halfHeight=0.6875, contactDamage=1),
    "echo_wisp": dict(
        tier="reinforced", hp=90, scoreValue=250, dropWeight=5,
        halfWidth=0.875, halfHeight=0.6875, contactDamage=1),
    "void_moth": dict(
        tier="reinforced", hp=95, scoreValue=260, dropWeight=5,
        halfWidth=0.875, halfHeight=0.6875, contactDamage=1),
    "zako_sine_slow": dict(
        tier="reinforced", hp=100, scoreValue=280, dropWeight=6,
        halfWidth=0.9375, halfHeight=0.75, contactDamage=1),
    "sentry_drone": dict(
        tier="reinforced", hp=110, scoreValue=300, dropWeight=5,
        halfWidth=0.9375, halfHeight=0.75, contactDamage=1),
    "phase_disc": dict(
        tier="reinforced", hp=120, scoreValue=320, dropWeight=5,
        halfWidth=0.9375, halfHeight=0.75, contactDamage=1),
    "brood_spitter": dict(
        tier="reinforced", hp=125, scoreValue=340, dropWeight=6,
        halfWidth=0.9375, halfHeight=0.8125, contactDamage=1),
    "mortar_drone": dict(
        tier="reinforced", hp=135, scoreValue=360, dropWeight=6,
        halfWidth=0.9375, halfHeight=0.8125, contactDamage=1),
    "turret_ground": dict(
        tier="reinforced", hp=140, scoreValue=380, dropWeight=4,
        halfWidth=0.9375, halfHeight=0.875, contactDamage=1),
    "turret_ceiling": dict(
        tier="reinforced", hp=140, scoreValue=380, dropWeight=4,
        halfWidth=0.9375, halfHeight=0.875, contactDamage=1),
    # --- 중형: multi-hit, dangerous if ignored (~4–8s early / ~1–1.5s mid) ---
    "zako_tank": dict(
        tier="mid", hp=500, scoreValue=800, dropWeight=13,
        halfWidth=1.25, halfHeight=1.0, contactDamage=2),
    "elite_sine": dict(
        tier="mid", hp=620, scoreValue=1000, dropWeight=14,
        halfWidth=1.3125, halfHeight=1.0625, contactDamage=2),
    "guardian_sphere": dict(
        tier="mid", hp=780, scoreValue=1200, dropWeight=15,
        halfWidth=1.375, halfHeight=1.25, contactDamage=2),
    "shard_prism": dict(
        tier="mid", hp=850, scoreValue=1300, dropWeight=15,
        halfWidth=1.375, halfHeight=1.25, contactDamage=2),
    # --- 중간보스: section goal; TTK ~4–5s at stage mid DPS ---
    "mini_horror": dict(
        tier="midboss", hp=2400, scoreValue=3000, dropWeight=22,
        halfWidth=2.25, halfHeight=1.75, contactDamage=2),
    "mini_destroyer": dict(
        tier="midboss", hp=3000, scoreValue=3600, dropWeight=24,
        halfWidth=2.375, halfHeight=1.75, contactDamage=2),
    "mini_crystal": dict(
        tier="midboss", hp=3600, scoreValue=4200, dropWeight=24,
        halfWidth=2.375, halfHeight=1.75, contactDamage=2),
    "mini_walker": dict(
        tier="midboss", hp=4500, scoreValue=5000, dropWeight=26,
        halfWidth=2.5, halfHeight=1.875, contactDamage=2),
}

assert len(TIER) == 30


def _assert_subunit(name: str, value: float) -> None:
    # Must be exact k/256 for Core ToSubUnits.
    scaled = value * 256
    if abs(scaled - round(scaled)) > 1e-9:
        raise SystemExit(f"{name}={value} is not a whole 1/256 subunit ({scaled})")


for _eid, _t in TIER.items():
    _assert_subunit(f"{_eid}.halfWidth", _t["halfWidth"])
    _assert_subunit(f"{_eid}.halfHeight", _t["halfHeight"])

# Segment intents (design notes; parser ignores unknown fields).
INTENT = {
    "seg_intro_line": "잡몹 라인 리듬 — 스치듯 처리하며 레인 감각",
    "seg_sine_pair": "상하 사인 회피 — 중앙 통로 위치 선정",
    "seg_turret_floor": "바닥 강화형 포탑 회피 + 상부 잡몹 정리",
    "seg_swarm_fast": "고속 잡몹 폭주 — 순수 회피 리듬",
    "seg_mixed_mid": "잡몹 리듬 속 중형 앵커 화력 집중",
    "seg_turret_ceiling": "천장 강화형 포탑 회피 + 하부 잡몹",
    "seg_sandwich": "상하 사격 압박 속 중형 격파 (위치+화력)",
    "seg_sine_rush": "사인 잡몹 러시 후 강화형·중형 조준",
    "seg_scrap_debris_line": "스크랩 잡몹 직선 리듬",
    "seg_scrap_pipe_dash": "대시 잡몹 회피 — 반응 속도",
    "seg_scrap_skimmer_weave": "스킴 잡몹 직조 회피 동선",
    "seg_scrap_junk_corridor": "강화형 조준 + 통로 압박",
    "seg_scrap_tumbler_pack": "강화형 텀블러 팩 조준 강요",
    "seg_scrap_rust_gauntlet": "중형 앵커 가틀릿 — 화력 집중",
    "seg_hive_spore_cloud": "잡몹 점유 후 중간보스 피날레",
    "seg_hive_lancer_rush": "랜서 잡몹 러시 순수 회피",
    "seg_hive_brood_wave": "강화형 브루드 사격 조준",
    "seg_hive_hornet_dive": "다이브 잡몹 회피 리듬",
    "seg_hive_organic_pulse": "중형 위협 + 중간보스 화력 집중",
    "seg_hive_nest_choke": "좁은 구간 중간보스 격파 목표",
    "seg_fortress_sentry_grid": "강화형 격자 사격 회피 + 중간보스",
    "seg_fortress_interceptor_assault": "잡몹 러시 폭주 — 회피 우선",
    "seg_fortress_mortar_line": "강화형 박격 라인 조준",
    "seg_fortress_turret_cross": "교차 포탑 위치 선정 + 중간보스",
    "seg_fortress_drone_lattice": "강화형 드론 격자 압박",
    "seg_fortress_armored_gate": "중형·중간보스 화력 집중 관문",
    "seg_nebula_wisp_storm": "잡몹 폭풍 회피 후 중형·중간보스",
    "seg_nebula_wisp_ribbon": "리본 회피 동선 + 중형 앵커",
    "seg_nebula_echo_ribbon": "강화형 에코 조준 리본",
    "seg_nebula_void_moth_swarm": "잡몹·강화 혼합 + 중간보스",
    "seg_nebula_crystal_drift": "중형 드리프트 화력 집중",
    "seg_nebula_prism_haze": "중형 프리즘 + 중간보스 목표",
    "seg_core_guardian_wall": "중형 벽 돌파 + 중간보스",
    "seg_core_final_gauntlet": "잡몹 가틀릿 속 중형 연속 처리",
    "seg_core_rift_blades": "잡몹 칼날 회피 + 강화형 사격",
    "seg_core_phase_discs": "강화형 원반 조준 + 중간보스",
    "seg_core_shard_battery": "중형 배터리 화력 집중",
    "seg_core_void_mix": "전 티어 혼합 최종 가틀릿",
}


def apply_enemies():
    path = root / "GameData" / "enemies.json"
    doc = json.loads(path.read_text(encoding="utf-8"))
    for e in doc["enemies"]:
        eid = e["id"]
        if eid not in TIER:
            raise SystemExit(f"unknown enemy {eid}")
        t = TIER[eid]
        e["hp"] = t["hp"]
        e["scoreValue"] = t["scoreValue"]
        e["dropWeight"] = t["dropWeight"]
        e["halfWidth"] = t["halfWidth"]
        e["halfHeight"] = t["halfHeight"]
        e["contactDamage"] = t["contactDamage"]
        # fireInterval / movement unchanged
    # noDropWeight stays 16 (prior 19% cut)
    assert doc["dropTable"]["noDropWeight"] == 16
    path.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {path} ({len(doc['enemies'])} enemies)")


def spawn(tick, enemy_id, y):
    return {"tick": tick, "enemyId": enemy_id, "y": y}


def rebuild_spawns(seg_id: str, length: int) -> list:
    """Clear tier rhythm per segment. Spawns stay within lengthTicks."""
    L = length
    # helpers for late midboss tick
    late = max(L - 80, L * 3 // 4)

    # --- generic ---
    if seg_id == "seg_intro_line":
        # pure fodder line
        ys = [3.25, 3.25, 0, -3.25, -3.25, 1.5, -1.5, 4, 0, -4]
        ids = ["zako_straight"] * 5 + ["rust_skimmer", "pipe_rat", "pipe_rat", "rust_skimmer", "zako_straight"]
        ticks = [40, 70, 100, 130, 160, 220, 250, 320, 350, 380]
        return [spawn(t, i, y) for t, i, y in zip(ticks, ids, ys)]

    if seg_id == "seg_sine_pair":
        # fodder sine pairs only
        out = []
        for t, y in [(40, 4.75), (40, -4.75), (120, 4.75), (120, -4.75),
                     (200, 4.0), (200, -4.0), (300, 5.5), (300, -5.5),
                     (420, 4.75), (420, -4.75)]:
            out.append(spawn(t, "zako_sine" if abs(y) > 4.5 else "junk_roller", y))
        return out

    if seg_id == "seg_turret_floor":
        # reinforced turrets + fodder above
        out = [
            spawn(60, "turret_ground", -5.5),
            spawn(180, "turret_ground", -5.5),
            spawn(360, "turret_ground", -5.5),
            spawn(100, "zako_straight", 3.0),
            spawn(160, "zako_straight", 4.0),
            spawn(220, "zako_fast", 2.0),
            spawn(280, "zako_sine", 3.5),
            spawn(340, "zako_fast", 1.0),
            spawn(420, "zako_sine", 4.5),
            spawn(500, "zako_straight", 2.5),
            spawn(600, "zako_tank", 2.0),  # mid finale
        ]
        return out

    if seg_id == "seg_swarm_fast":
        out = []
        ys = [4, 2, 0, -2, -4, 3, 1, -1, -3, 4.5, -4.5, 2.5, -2.5, 0.5, -0.5, 3.5, -3.5, 0]
        for i, y in enumerate(ys):
            out.append(spawn(30 + i * 28, "zako_fast", y))
        return out

    if seg_id == "seg_mixed_mid":
        out = [
            spawn(40, "zako_straight", 2.5),
            spawn(70, "zako_straight", -2.5),
            spawn(100, "zako_sine", 4.0),
            spawn(130, "zako_fast", 0),
            spawn(180, "scrap_tumbler", 3.0),
            spawn(220, "scrap_tumbler", -3.0),
            spawn(280, "zako_fast", 2.0),
            spawn(320, "zako_sine", -4.0),
            spawn(380, "zako_tank", 0),
            spawn(440, "zako_straight", 3.5),
            spawn(480, "zako_straight", -3.5),
            spawn(540, "elite_sine", 0),
            spawn(600, "zako_fast", 1.5),
            spawn(640, "zako_fast", -1.5),
        ]
        return out

    if seg_id == "seg_turret_ceiling":
        out = [
            spawn(60, "turret_ceiling", 5.5),
            spawn(180, "turret_ceiling", 5.5),
            spawn(360, "turret_ceiling", 5.5),
            spawn(100, "zako_straight", -3.0),
            spawn(160, "zako_straight", -4.0),
            spawn(220, "zako_fast", -2.0),
            spawn(280, "zako_sine", -3.5),
            spawn(340, "zako_fast", -1.0),
            spawn(420, "zako_sine", -4.5),
            spawn(500, "zako_straight", -2.5),
            spawn(600, "zako_tank", -2.0),
        ]
        return out

    if seg_id == "seg_sandwich":
        out = [
            spawn(50, "turret_ground", -5.5),
            spawn(50, "turret_ceiling", 5.5),
            spawn(140, "turret_ground", -5.5),
            spawn(140, "turret_ceiling", 5.5),
            spawn(230, "turret_ground", -5.5),
            spawn(230, "turret_ceiling", 5.5),
            spawn(90, "zako_sine", 2.0),
            spawn(90, "zako_sine", -2.0),
            spawn(180, "zako_sine", 3.0),
            spawn(180, "zako_sine", -3.0),
            spawn(280, "zako_fast", 0),
            spawn(340, "zako_sine_slow", 2.5),
            spawn(340, "zako_sine_slow", -2.5),
            spawn(420, "zako_straight", 1.0),
            spawn(420, "zako_straight", -1.0),
            spawn(520, "zako_tank", 0),
            spawn(620, "elite_sine", 0),
        ]
        return out

    if seg_id == "seg_sine_rush":
        out = [
            spawn(40, "zako_sine", 3.5),
            spawn(70, "zako_sine", -3.5),
            spawn(100, "zako_sine", 4.5),
            spawn(130, "zako_sine", -4.5),
            spawn(180, "zako_sine_slow", 2.0),
            spawn(220, "zako_sine_slow", -2.0),
            spawn(260, "junk_roller", 3.0),
            spawn(300, "junk_roller", -3.0),
            spawn(340, "pipe_rat", 1.5),
            spawn(380, "pipe_rat", -1.5),
            spawn(420, "zako_sine_slow", 0),
            spawn(480, "junk_roller", 4.0),
            spawn(520, "junk_roller", -4.0),
            spawn(580, "elite_sine", 0),
        ]
        return out

    # --- scrapyard ---
    if seg_id == "seg_scrap_debris_line":
        ids = ["junk_roller", "rust_skimmer", "pipe_rat"] * 4
        ys = [3, -2, 1, -4, 2.5, -1, 4, -3, 0, 3.5, -2.5, 1.5]
        return [spawn(40 + i * 40, ids[i], ys[i]) for i in range(12)]

    if seg_id == "seg_scrap_pipe_dash":
        out = []
        for i, y in enumerate([2, -2, 4, -4, 0, 3, -3, 1.5, -1.5, 4.5, -4.5, 0, 2.5, -2.5]):
            eid = "pipe_rat" if i % 2 == 0 else "rust_skimmer"
            out.append(spawn(35 + i * 35, eid, y))
        return out

    if seg_id == "seg_scrap_skimmer_weave":
        out = []
        for i, y in enumerate([4, -3, 2, -4, 1, -2, 3.5, -1, 0, 4.5, -3.5, 2.5, -0.5, 3, -4.5]):
            eid = "rust_skimmer" if i % 3 != 2 else "junk_roller"
            out.append(spawn(35 + i * 38, eid, y))
        return out

    if seg_id == "seg_scrap_junk_corridor":
        out = [
            spawn(40, "junk_roller", 2), spawn(70, "junk_roller", -2),
            spawn(100, "scrap_tumbler", 3), spawn(140, "scrap_tumbler", -3),
            spawn(180, "pipe_rat", 0), spawn(210, "rust_skimmer", 4),
            spawn(250, "turret_ground", -5.5),
            spawn(300, "scrap_tumbler", 2.5), spawn(340, "scrap_tumbler", -2.5),
            spawn(380, "junk_roller", 1), spawn(420, "pipe_rat", -4),
            spawn(460, "rust_skimmer", 3),
            spawn(520, "zako_tank", 0),
            spawn(580, "scrap_tumbler", 3.5), spawn(620, "scrap_tumbler", -3.5),
            spawn(660, "junk_roller", 0),
        ]
        return out

    if seg_id == "seg_scrap_tumbler_pack":
        out = []
        for i, y in enumerate([3, -3, 4, -4, 1.5, -1.5, 0, 2.5, -2.5, 3.5]):
            out.append(spawn(50 + i * 50, "scrap_tumbler", y))
        out += [
            spawn(180, "pipe_rat", 4.5), spawn(220, "pipe_rat", -4.5),
            spawn(300, "junk_roller", 2), spawn(340, "junk_roller", -2),
            spawn(400, "turret_ceiling", 5.5),
            spawn(480, "rust_skimmer", 0), spawn(520, "rust_skimmer", 3),
            spawn(580, "zako_tank", 0),
        ]
        return out

    if seg_id == "seg_scrap_rust_gauntlet":
        out = [
            spawn(40, "rust_skimmer", 3), spawn(70, "rust_skimmer", -3),
            spawn(100, "rust_skimmer", 0), spawn(130, "pipe_rat", 4),
            spawn(160, "pipe_rat", -4), spawn(200, "scrap_tumbler", 2),
            spawn(240, "scrap_tumbler", -2),
            spawn(280, "turret_ground", -5.5), spawn(280, "turret_ceiling", 5.5),
            spawn(340, "junk_roller", 1.5), spawn(380, "junk_roller", -1.5),
            spawn(440, "zako_tank", 2.5), spawn(500, "zako_tank", -2.5),
            spawn(560, "rust_skimmer", 3.5), spawn(600, "rust_skimmer", -3.5),
            spawn(660, "elite_sine", 0),
            spawn(720, "rust_skimmer", 0), spawn(740, "pipe_rat", 2),
            spawn(760, "pipe_rat", -2),
        ]
        return out

    # --- hive ---
    if seg_id == "seg_hive_spore_cloud":
        out = []
        for i, y in enumerate([3, -2, 1, -4, 2.5, -1.5, 4, -3.5, 0]):
            out.append(spawn(40 + i * 45, "spore_drifter", y))
        out += [
            spawn(120, "sting_hornet", 4), spawn(180, "sting_hornet", -4),
            spawn(240, "sting_hornet", 2), spawn(300, "sting_hornet", -2),
            spawn(360, "brood_spitter", 3), spawn(420, "brood_spitter", -3),
            spawn(480, "brood_spitter", 0),
            spawn(late, "mini_horror", 0),
        ]
        return out

    if seg_id == "seg_hive_lancer_rush":
        out = []
        ys = [4, -3, 2, -4, 0, 3.5, -1.5, 1, -2.5, 4.5, -4.5, 2.5, -0.5, 3, -3.5, 0.5]
        for i, y in enumerate(ys):
            out.append(spawn(25 + i * 32, "lancer_dart", y))
        out += [
            spawn(200, "sting_hornet", 3), spawn(280, "sting_hornet", -3),
            spawn(360, "sting_hornet", 1), spawn(440, "sting_hornet", -1),
            spawn(520, "brood_spitter", 2), spawn(560, "brood_spitter", -2),
        ]
        return out

    if seg_id == "seg_hive_brood_wave":
        out = []
        for i, y in enumerate([3, -3, 0, 4, -4, 1.5]):
            out.append(spawn(50 + i * 70, "brood_spitter", y))
        out += [
            spawn(80, "spore_drifter", 2), spawn(140, "spore_drifter", -2),
            spawn(200, "spore_drifter", 3.5), spawn(260, "spore_drifter", -3.5),
            spawn(320, "sting_hornet", 0), spawn(380, "sting_hornet", 4),
            spawn(440, "lancer_dart", -2), spawn(480, "lancer_dart", 2),
            spawn(540, "brood_spitter", 0), spawn(600, "brood_spitter", 2.5),
            spawn(640, "spore_drifter", -1), spawn(680, "sting_hornet", -4),
        ]
        return out

    if seg_id == "seg_hive_hornet_dive":
        out = []
        for i, y in enumerate([3, -2, 4, -4, 1, -1, 2.5, -3.5, 0, 3.5, -0.5, 4.5]):
            out.append(spawn(35 + i * 40, "sting_hornet" if i % 2 == 0 else "lancer_dart", y))
        out += [
            spawn(200, "spore_drifter", 2), spawn(280, "spore_drifter", -2),
            spawn(360, "brood_spitter", 0), spawn(440, "brood_spitter", 3),
            spawn(520, "spore_drifter", -3), spawn(580, "sting_hornet", 1),
        ]
        return out

    if seg_id == "seg_hive_organic_pulse":
        out = [
            spawn(40, "spore_drifter", 3), spawn(80, "spore_drifter", -3),
            spawn(120, "brood_spitter", 2), spawn(180, "brood_spitter", -2),
            spawn(240, "lancer_dart", 4), spawn(280, "lancer_dart", -4),
            spawn(320, "sting_hornet", 0), spawn(360, "elite_sine", 0),
            spawn(420, "brood_spitter", 3), spawn(460, "spore_drifter", -1),
            spawn(500, "lancer_dart", 2), spawn(540, "sting_hornet", -3),
            spawn(late, "mini_horror", 0),
        ]
        return out

    if seg_id == "seg_hive_nest_choke":
        out = [
            spawn(50, "brood_spitter", 2.5), spawn(100, "brood_spitter", -2.5),
            spawn(150, "spore_drifter", 0), spawn(200, "sting_hornet", 3.5),
            spawn(250, "sting_hornet", -3.5), spawn(300, "brood_spitter", 0),
            spawn(360, "lancer_dart", 2), spawn(400, "lancer_dart", -2),
            spawn(450, "elite_sine", 0),
            spawn(520, "spore_drifter", 3), spawn(560, "sting_hornet", -3),
            spawn(late, "mini_horror", 0),
        ]
        return out

    # --- fortress ---
    if seg_id == "seg_fortress_sentry_grid":
        out = [
            spawn(40, "sentry_drone", 3), spawn(40, "sentry_drone", -3),
            spawn(120, "sentry_drone", 4.5), spawn(120, "sentry_drone", -4.5),
            spawn(80, "turret_ground", -5.5), spawn(200, "turret_ceiling", 5.5),
            spawn(100, "interceptor_rush", 1), spawn(140, "interceptor_rush", -1),
            spawn(180, "interceptor_rush", 2.5), spawn(220, "interceptor_rush", -2.5),
            spawn(260, "mortar_drone", 2), spawn(320, "mortar_drone", -2),
            spawn(380, "mortar_drone", 0),
            spawn(280, "interceptor_rush", 3.5), spawn(340, "interceptor_rush", -3.5),
            spawn(420, "scrap_tumbler", 1.5), spawn(480, "sentry_drone", 0),
            spawn(540, "elite_sine", 0),
            spawn(late, "mini_destroyer", 0),
        ]
        return out

    if seg_id == "seg_fortress_interceptor_assault":
        out = []
        for i, y in enumerate([4, -3, 2, -4, 0, 3.5, -1.5, 1, -2.5, 4.5, -4.5, 2.5, -0.5, 3, -3.5, 0.5]):
            out.append(spawn(25 + i * 35, "interceptor_rush", y))
        out += [
            spawn(200, "sentry_drone", 3), spawn(280, "sentry_drone", -3),
            spawn(360, "turret_ground", -5.5), spawn(400, "turret_ceiling", 5.5),
            spawn(460, "mortar_drone", 0), spawn(520, "zako_fast", 2),
            spawn(560, "zako_fast", -2), spawn(620, "elite_sine", 0),
        ]
        return out

    if seg_id == "seg_fortress_mortar_line":
        out = []
        for i, y in enumerate([3, -3, 0, 4, -4, 1.5]):
            out.append(spawn(50 + i * 80, "mortar_drone", y))
        out += [
            spawn(80, "sentry_drone", 2), spawn(160, "sentry_drone", -2),
            spawn(240, "sentry_drone", 3.5), spawn(320, "sentry_drone", -3.5),
            spawn(100, "interceptor_rush", 1), spawn(180, "interceptor_rush", -1),
            spawn(260, "interceptor_rush", 0), spawn(340, "interceptor_rush", 4),
            spawn(420, "interceptor_rush", -4),
            spawn(480, "turret_ground", -5.5),
            spawn(560, "elite_sine", 0),
            spawn(640, "sentry_drone", 0), spawn(700, "mortar_drone", 2),
        ]
        return out

    if seg_id == "seg_fortress_turret_cross":
        out = [
            spawn(50, "turret_ground", -5.5), spawn(50, "turret_ceiling", 5.5),
            spawn(180, "turret_ground", -5.5), spawn(180, "turret_ceiling", 5.5),
            spawn(90, "sentry_drone", 2.5), spawn(130, "sentry_drone", -2.5),
            spawn(220, "sentry_drone", 0), spawn(280, "sentry_drone", 3.5),
            spawn(340, "sentry_drone", -3.5),
            spawn(100, "interceptor_rush", 1), spawn(160, "interceptor_rush", -1),
            spawn(240, "interceptor_rush", 2), spawn(300, "interceptor_rush", -2),
            spawn(360, "interceptor_rush", 0),
            spawn(400, "mortar_drone", 2), spawn(460, "mortar_drone", -2),
            spawn(520, "mortar_drone", 0),
            spawn(580, "elite_sine", 0),
            spawn(late, "mini_destroyer", 0),
        ]
        return out

    if seg_id == "seg_fortress_drone_lattice":
        out = []
        for i, y in enumerate([4, -4, 2, -2, 0, 3, -3, 1.5]):
            out.append(spawn(40 + i * 55, "sentry_drone", y))
        out += [
            spawn(70, "interceptor_rush", 1), spawn(110, "interceptor_rush", -1),
            spawn(150, "interceptor_rush", 3), spawn(190, "interceptor_rush", -3),
            spawn(230, "interceptor_rush", 0), spawn(270, "interceptor_rush", 4),
            spawn(310, "interceptor_rush", -4), spawn(350, "interceptor_rush", 2),
            spawn(390, "interceptor_rush", -2),
            spawn(280, "mortar_drone", 2.5), spawn(360, "mortar_drone", -2.5),
            spawn(440, "mortar_drone", 0), spawn(500, "mortar_drone", 3),
            spawn(560, "turret_ground", -5.5), spawn(560, "turret_ceiling", 5.5),
            spawn(620, "elite_sine", 0),
            spawn(late, "mini_destroyer", 0),
        ]
        return out

    if seg_id == "seg_fortress_armored_gate":
        out = [
            spawn(40, "turret_ground", -5.5), spawn(40, "turret_ceiling", 5.5),
            spawn(120, "turret_ground", -5.5), spawn(120, "turret_ceiling", 5.5),
            spawn(80, "sentry_drone", 2), spawn(160, "sentry_drone", -2),
            spawn(200, "sentry_drone", 3.5), spawn(240, "sentry_drone", -3.5),
            spawn(280, "sentry_drone", 0),
            spawn(100, "mortar_drone", 1.5), spawn(180, "mortar_drone", -1.5),
            spawn(260, "mortar_drone", 0), spawn(340, "mortar_drone", 2.5),
            spawn(400, "mortar_drone", -2.5),
            spawn(140, "interceptor_rush", 4), spawn(220, "interceptor_rush", -4),
            spawn(300, "interceptor_rush", 1), spawn(380, "interceptor_rush", -1),
            spawn(450, "zako_tank", 2), spawn(510, "zako_tank", -2),
            spawn(580, "elite_sine", 0),
            spawn(late, "mini_destroyer", 0),
        ]
        return out

    # --- nebula ---
    if seg_id == "seg_nebula_wisp_storm":
        out = []
        for i, y in enumerate([3, -2, 4, -4, 1, -1, 2.5, -3.5, 0, 3.5, -0.5, 4.5]):
            out.append(spawn(30 + i * 40, "void_moth" if i % 3 else "wisp_spark", y))
        out += [
            spawn(150, "echo_wisp", 2), spawn(230, "echo_wisp", -2),
            spawn(310, "zako_sine_slow", 3), spawn(390, "zako_sine_slow", -3),
            spawn(470, "guardian_sphere", 0),
            spawn(550, "elite_sine", 2),
            spawn(late, "mini_crystal", 0),
        ]
        return out

    if seg_id == "seg_nebula_wisp_ribbon":
        out = []
        for i, y in enumerate([2.5, -2.5, 4, -4, 0, 3, -3, 1.5, -1.5]):
            out.append(spawn(40 + i * 45, "echo_wisp" if i % 2 else "void_moth", y))
        out += [
            spawn(100, "zako_sine", 3.5), spawn(180, "zako_sine", -3.5),
            spawn(260, "zako_sine", 0), spawn(340, "zako_tank", 0),
            spawn(420, "zako_sine_slow", 2), spawn(500, "elite_sine", 0),
            spawn(580, "guardian_sphere", 0),
            spawn(late, "mini_crystal", 0),
        ]
        return out

    if seg_id == "seg_nebula_echo_ribbon":
        out = []
        for i, y in enumerate([3, -3, 1.5, -1.5, 4, -4, 0]):
            out.append(spawn(50 + i * 60, "echo_wisp", y))
        out += [
            spawn(80, "wisp_spark", 2), spawn(140, "wisp_spark", -2),
            spawn(200, "wisp_spark", 3.5), spawn(260, "wisp_spark", -3.5),
            spawn(320, "wisp_spark", 0), spawn(380, "wisp_spark", 4),
            spawn(440, "void_moth", 1), spawn(500, "void_moth", -1),
            spawn(560, "void_moth", 2.5), spawn(620, "void_moth", -2.5),
            spawn(680, "elite_sine", 0),
        ]
        return out

    if seg_id == "seg_nebula_void_moth_swarm":
        out = []
        for i, y in enumerate([3, -2, 4, -4, 1, -1, 2.5]):
            out.append(spawn(40 + i * 50, "void_moth", y))
        out += [
            spawn(70, "wisp_spark", 0), spawn(130, "wisp_spark", 3),
            spawn(190, "wisp_spark", -3), spawn(250, "wisp_spark", 1.5),
            spawn(310, "echo_wisp", 2), spawn(370, "echo_wisp", -2),
            spawn(430, "echo_wisp", 0), spawn(490, "elite_sine", 0),
            spawn(late, "mini_crystal", 0),
        ]
        return out

    if seg_id == "seg_nebula_crystal_drift":
        out = [
            spawn(40, "wisp_spark", 3), spawn(80, "wisp_spark", -3),
            spawn(120, "echo_wisp", 2), spawn(180, "void_moth", -2),
            spawn(240, "wisp_spark", 0), spawn(300, "guardian_sphere", 0),
            spawn(360, "echo_wisp", 3.5), spawn(420, "void_moth", -3.5),
            spawn(480, "elite_sine", 0), spawn(540, "wisp_spark", 2),
            spawn(600, "wisp_spark", -2),
            spawn(late, "mini_crystal", 0),
        ]
        return out

    if seg_id == "seg_nebula_prism_haze":
        out = [
            spawn(40, "echo_wisp", 2.5), spawn(90, "void_moth", -2.5),
            spawn(140, "wisp_spark", 4), spawn(190, "wisp_spark", -4),
            spawn(240, "shard_prism", 0), spawn(320, "echo_wisp", 1),
            spawn(380, "void_moth", -1), spawn(440, "shard_prism", 2),
            spawn(500, "elite_sine", 0), spawn(560, "wisp_spark", 3),
            spawn(620, "wisp_spark", -3),
            spawn(late, "mini_crystal", 0),
        ]
        return out

    # --- core ---
    if seg_id == "seg_core_guardian_wall":
        out = [
            spawn(40, "turret_ground", -5.5), spawn(40, "turret_ceiling", 5.5),
            spawn(100, "guardian_sphere", 2), spawn(160, "guardian_sphere", -2),
            spawn(220, "rift_blade", 3), spawn(250, "rift_blade", -3),
            spawn(280, "rift_blade", 1), spawn(310, "rift_blade", -1),
            spawn(340, "phase_disc", 3.5), spawn(400, "phase_disc", -3.5),
            spawn(460, "shard_prism", 0), spawn(520, "guardian_sphere", 0),
            spawn(580, "rift_blade", 2.5), spawn(610, "rift_blade", -2.5),
            spawn(640, "elite_sine", 0),
            spawn(late, "mini_walker", 0),
        ]
        return out

    if seg_id == "seg_core_final_gauntlet":
        out = []
        for i, y in enumerate([4, -3, 2, -4, 0, 3.5, -1.5, 1, -2.5, 4.5, -4.5, 0.5]):
            out.append(spawn(30 + i * 40, "rift_blade", y))
        out += [
            spawn(100, "turret_ground", -5.5), spawn(200, "turret_ceiling", 5.5),
            spawn(280, "shard_prism", 2), spawn(360, "shard_prism", -2),
            spawn(440, "phase_disc", 0), spawn(500, "phase_disc", 3),
            spawn(560, "interceptor_rush", 1), spawn(600, "interceptor_rush", -1),
            spawn(660, "shard_prism", 0), spawn(720, "elite_sine", 0),
        ]
        return out

    if seg_id == "seg_core_rift_blades":
        out = []
        for i, y in enumerate([3, -2, 4, -4, 1, -1, 2.5, -3.5, 0, 3.5, -0.5, 4.5]):
            out.append(spawn(30 + i * 38, "rift_blade", y))
        out += [
            spawn(150, "phase_disc", 2), spawn(250, "phase_disc", -2),
            spawn(350, "phase_disc", 0),
            spawn(450, "guardian_sphere", 0),
            spawn(550, "interceptor_rush", 3), spawn(590, "interceptor_rush", -3),
            spawn(650, "elite_sine", 0),
        ]
        return out

    if seg_id == "seg_core_phase_discs":
        out = []
        for i, y in enumerate([3, -3, 0, 4, -4, 1.5, -1.5]):
            out.append(spawn(50 + i * 65, "phase_disc", y))
        out += [
            spawn(90, "rift_blade", 2), spawn(150, "rift_blade", -2),
            spawn(210, "rift_blade", 0), spawn(270, "rift_blade", 3.5),
            spawn(330, "rift_blade", -3.5), spawn(390, "rift_blade", 1),
            spawn(300, "shard_prism", 0), spawn(420, "guardian_sphere", 2),
            spawn(500, "guardian_sphere", -2),
            spawn(580, "elite_sine", 0),
            spawn(late, "mini_walker", 0),
        ]
        return out

    if seg_id == "seg_core_shard_battery":
        out = [
            spawn(40, "turret_ground", -5.5), spawn(40, "turret_ceiling", 5.5),
            spawn(80, "shard_prism", 2), spawn(160, "shard_prism", -2),
            spawn(240, "shard_prism", 0), spawn(320, "shard_prism", 3),
            spawn(400, "shard_prism", -3),
            spawn(120, "rift_blade", 1), spawn(200, "rift_blade", -1),
            spawn(280, "rift_blade", 4), spawn(360, "rift_blade", -4),
            spawn(440, "phase_disc", 2.5), spawn(500, "phase_disc", -2.5),
            spawn(560, "guardian_sphere", 0), spawn(620, "elite_sine", 0),
            spawn(late, "mini_walker", 0),
        ]
        return out

    if seg_id == "seg_core_void_mix":
        out = [
            spawn(40, "guardian_sphere", 2), spawn(100, "rift_blade", 3),
            spawn(130, "rift_blade", -3), spawn(160, "phase_disc", 0),
            spawn(220, "shard_prism", -2), spawn(280, "void_moth", 3.5),
            spawn(320, "void_moth", -3.5), spawn(380, "interceptor_rush", 1),
            spawn(420, "interceptor_rush", -1), spawn(460, "guardian_sphere", 0),
            spawn(520, "phase_disc", 2.5), spawn(580, "shard_prism", -2.5),
            spawn(640, "elite_sine", 0), spawn(700, "rift_blade", 0),
            spawn(late, "mini_walker", 0),
        ]
        return out

    raise SystemExit(f"no spawn rebuild for {seg_id}")


def apply_waves():
    path = root / "GameData" / "waves.json"
    doc = json.loads(path.read_text(encoding="utf-8"))
    for seg in doc["segments"]:
        sid = seg["id"]
        if sid not in INTENT:
            raise SystemExit(f"missing intent for {sid}")
        seg["intent"] = INTENT[sid]
        length = seg["lengthTicks"]
        new_spawns = rebuild_spawns(sid, length)
        for sp in new_spawns:
            if sp["tick"] >= length:
                raise SystemExit(f"{sid}: spawn tick {sp['tick']} >= length {length}")
            if sp["enemyId"] not in TIER:
                raise SystemExit(f"{sid}: bad enemy {sp['enemyId']}")
        seg["spawns"] = new_spawns
    path.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {path} ({len(doc['segments'])} segments)")


def report():
    enemies_doc = json.loads((root / "GameData/enemies.json").read_text(encoding="utf-8"))
    enemies = {e["id"]: e for e in enemies_doc["enemies"]}
    waves = json.loads((root / "GameData/waves.json").read_text(encoding="utf-8"))
    no_drop = enemies_doc["dropTable"]["noDropWeight"]
    themes = waves["themes"]

    print("\n=== Tier table ===")
    by_tier = Counter()
    for eid, t in TIER.items():
        e = enemies[eid]
        area = e["halfWidth"] * e["halfHeight"] * 4
        by_tier[t["tier"]] += 1
        print(
            f"{t['tier']:10} {eid:20} hp={e['hp']:4} size={e['halfWidth']:.2f}x{e['halfHeight']:.2f} "
            f"area={area:5.2f} score={e['scoreValue']:4} drop={e['dropWeight']:2} "
            f"p={e['dropWeight']/(no_drop+e['dropWeight']):.1%}"
        )
    print("counts", dict(by_tier))

    print("\n=== TTK sketch (provisional CODEX-nerfed mid DPS) ===")
    dps_early, dps_mid, dps_full = 100.0, 550.0, 1500.0
    for tier_name, sample in [
        ("fodder", "zako_straight"),
        ("reinforced", "brood_spitter"),
        ("mid", "elite_sine"),
        ("midboss_s2", "mini_horror"),
        ("midboss_s3", "mini_destroyer"),
        ("midboss_s4", "mini_crystal"),
        ("midboss_s5", "mini_walker"),
    ]:
        hp = enemies[sample]["hp"]
        print(
            f"{tier_name:12} {sample:18} early={hp/dps_early:.2f}s  "
            f"mid={hp/dps_mid:.2f}s  full={hp/dps_full:.2f}s"
        )

    print("\n=== Segment HP + EV ===")
    weight_sum = 0
    weighted_ev = 0.0
    for s in waves["segments"]:
        hp = sum(enemies[sp["enemyId"]]["hp"] for sp in s["spawns"])
        ev = sum(
            enemies[sp["enemyId"]]["dropWeight"]
            / (no_drop + enemies[sp["enemyId"]]["dropWeight"])
            for sp in s["spawns"]
        )
        w = s.get("weight", 10)
        weight_sum += w
        weighted_ev += ev * w
        tiers = Counter(TIER[sp["enemyId"]]["tier"] for sp in s["spawns"])
        print(
            f"{s['id']:36} hp={hp:5} EV={ev:5.2f} w={w:2} "
            f"tiers={dict(tiers)} | {s.get('intent','')[:28]}"
        )
    e_stage = (weighted_ev / weight_sum) * waves["segmentsPerStage"]
    print(f"weight-biased E_stage={e_stage:.2f} (band 10–16)")

    print("\n=== Stage avgHP mono ===")
    prev = None
    for stage in range(1, 6):
        theme = themes[stage - 1]
        diff = stage
        pool = [
            s for s in waves["segments"]
            if s["difficultyMin"] <= diff <= s["difficultyMax"]
            and (s.get("theme") is None or s.get("theme") == theme)
        ]
        avg = sum(
            sum(enemies[sp["enemyId"]]["hp"] for sp in s["spawns"]) for s in pool
        ) / len(pool)
        flag = "" if prev is None else (" monoOK" if avg >= prev else " MONO FAIL")
        print(f"stage {stage} {theme:10} n={len(pool)} avgHP={avg:.1f}{flag}")
        prev = avg


if __name__ == "__main__":
    apply_enemies()
    apply_waves()
    report()
