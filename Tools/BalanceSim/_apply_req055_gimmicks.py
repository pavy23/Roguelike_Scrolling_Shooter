#!/usr/bin/env python3
"""REQ-055: fill stage gimmicks into GameData (GROK content ownership).

One-shot applicator. Idempotent on re-run (rebuilds derived fields).
"""
from __future__ import annotations

import json
from copy import deepcopy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ENEMIES = ROOT / "GameData" / "enemies.json"
WAVES = ROOT / "GameData" / "waves.json"

# Playfield half-height ≈ 11.25u; player halfH 0.375u; speed-up 4 ≈ 21.5 u/s.
# Corridors leave telegraph room by starting wide and ending still ≥ ~5u open.

def laser(
    *,
    cycle: int,
    telegraph: int,
    firing: int,
    sustain: int,
    dissipate: int,
    end_x: float = -36.0,
    end_y: float = 0.0,
    start_x: float = 0.0,
    start_y: float = 0.0,
    thin: float = 0.0625,
    full: float = 0.45,
    damage: int = 1,
) -> dict:
    return {
        "cycleIntervalTicks": cycle,
        "telegraphTicks": telegraph,
        "firingTicks": firing,
        "sustainTicks": sustain,
        "dissipateTicks": dissipate,
        "startOffsetX": start_x,
        "startOffsetY": start_y,
        "endOffsetX": end_x,
        "endOffsetY": end_y,
        "thinHalfWidth": thin,
        "fullHalfWidth": full,
        "damage": damage,
    }


def emitter(x: float, y: float, laser_profile: dict) -> dict:
    return {
        "type": "laserEmitter",
        "x": x,
        "y": y,
        "hp": 0,
        "laser": laser_profile,
    }


def brk(x: float, y: float, hp: int) -> dict:
    return {"type": "breakable", "x": x, "y": y, "hp": hp}


def solid(x: float, y: float) -> dict:
    return {"type": "solid", "x": x, "y": y, "hp": 0}


def corridor(
    start_min: float,
    start_max: float,
    end_min: float,
    end_max: float,
    contact: int = 1,
) -> dict:
    return {
        "corridor": {
            "startMinY": start_min,
            "startMaxY": start_max,
            "endMinY": end_min,
            "endMaxY": end_max,
            "contactDamage": contact,
        }
    }


def drift(x_ps: float, y_ps: float) -> dict:
    return {"drift": {"xPerSecond": x_ps, "yPerSecond": y_ps}}


def merge_env(segment: dict, environment: dict) -> None:
    segment["environment"] = environment


def replace_breakables(obstacles: list, new_breakables: list) -> list:
    """Keep solids/lasers; replace breakables with new list."""
    kept = [o for o in obstacles if o.get("type") != "breakable"]
    return kept + new_breakables


def ensure_tentacle_enemy(enemies_doc: dict) -> None:
    enemies = enemies_doc["enemies"]
    if any(e["id"] == "hive_tentacle" for e in enemies):
        # refresh definition in place
        for i, e in enumerate(enemies):
            if e["id"] == "hive_tentacle":
                enemies[i] = tentacle_def()
                return
    # insert after brood-related hive pack (after lancer_dart)
    insert_at = next(
        (i + 1 for i, e in enumerate(enemies) if e["id"] == "lancer_dart"),
        len(enemies),
    )
    enemies.insert(insert_at, tentacle_def())


def tentacle_def() -> dict:
    # Static wall tentacle: tall hitbox, scrapes scroll, drops capsules when cleared.
    return {
        "id": "hive_tentacle",
        "displayName": "Hive Tentacle",
        "hp": 160,
        "contactDamage": 1,
        "scoreValue": 280,
        "fireIntervalTicks": 100,
        "dropWeight": 7,
        "halfWidth": 0.625,
        "halfHeight": 1.25,
        "movement": {
            "pattern": "static",
        },
    }


def spawn(tick: int, enemy_id: str, y: float) -> dict:
    return {"tick": tick, "enemyId": enemy_id, "y": y}


def strip_enemy_spawns(spawns: list, enemy_id: str) -> list:
    return [s for s in spawns if s.get("enemyId") != enemy_id]


def apply_scrapyard(seg: dict) -> None:
    sid = seg["id"]
    # Stage-1 capable scrap segs: floating breakable debris only (no solids).
    if sid == "seg_scrap_debris_line":
        # d1-2: teach cover/clear — 5 mid-field breakables.
        seg["obstacles"] = [
            brk(11.0, 2.5, 25),
            brk(13.5, -1.5, 30),
            brk(16.0, 3.5, 28),
            brk(18.5, -3.0, 32),
            brk(14.0, 0.0, 35),
        ]
        seg["gimmickNote"] = "파괴 가능 잔해 5 — 엄폐·치우기 입문"
    elif sid == "seg_scrap_pipe_dash":
        seg["obstacles"] = [
            brk(12.0, 4.0, 22),
            brk(14.5, -3.5, 26),
            brk(17.0, 1.0, 30),
            brk(19.5, -2.0, 28),
            brk(15.5, 3.0, 24),
            brk(13.0, -0.5, 34),
        ]
        seg["gimmickNote"] = "잔해 6 — 대시 레인 사이에 엄폐 섬"
    elif sid == "seg_scrap_skimmer_weave":
        seg["obstacles"] = [
            brk(11.5, 3.25, 30),
            brk(14.0, -3.25, 30),
            brk(16.5, 0.0, 40),
            brk(19.0, 4.0, 28),
            brk(12.5, -1.5, 32),
            brk(17.5, -4.0, 26),
        ]
        seg["gimmickNote"] = "잔해 6 — 스킴 위빙 경로 강제/엄폐"
    elif sid == "seg_scrap_junk_corridor":
        # d2-4: solids frame + denser breakables
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"]
        if len(solids) < 2:
            solids = [solid(11.0, 5.5), solid(15.0, -5.5)]
        seg["obstacles"] = solids + [
            brk(12.5, 1.5, 35),
            brk(14.0, -2.0, 40),
            brk(16.5, 3.0, 38),
            brk(18.0, -0.5, 45),
            brk(19.5, 2.5, 42),
        ]
        seg["gimmickNote"] = "잔해 5 + 프레임 — 좁은 고철 통로 엄폐 전투"
    elif sid == "seg_scrap_tumbler_pack":
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"]
        if len(solids) < 2:
            solids = [solid(11.0, 5.5), solid(15.0, -5.5)]
        seg["obstacles"] = solids + [
            brk(12.0, 0.0, 45),
            brk(14.5, 3.5, 40),
            brk(14.5, -3.5, 40),
            brk(17.0, 1.5, 50),
            brk(19.0, -2.5, 48),
            brk(16.0, -0.5, 55),
        ]
        seg["gimmickNote"] = "잔해 6 — 텀블러 무리 앞에서 엄폐 파쇄"
    elif sid == "seg_scrap_rust_gauntlet":
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"]
        if len(solids) < 3:
            solids = [solid(10.0, 6.0), solid(14.0, -6.0), solid(18.0, 5.5)]
        seg["obstacles"] = solids + [
            brk(11.5, 2.0, 50),
            brk(13.0, -2.5, 55),
            brk(15.5, 0.5, 60),
            brk(17.0, 3.5, 48),
            brk(17.0, -3.5, 48),
            brk(19.5, -0.5, 55),
            brk(12.5, -4.0, 42),
        ]
        seg["gimmickNote"] = "잔해 7 — 후반 엄폐 밀도, 파쇄 보상"


def apply_hive(seg: dict) -> None:
    sid = seg["id"]
    # Tentacles = static enemy spawns. Corridors only on choke/pulse (not with mid spam).
    # Keep obstacles ≤5 (BalanceSim hive max gate).

    if sid == "seg_hive_spore_cloud":
        # Open hive — light tentacles on edges only, no corridor (spore cloud fills space).
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        spawns.extend(
            [
                spawn(80, "hive_tentacle", 5.5),
                spawn(80, "hive_tentacle", -5.5),
                spawn(360, "hive_tentacle", 4.5),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        # keep obstacles ≤4
        obs = seg.get("obstacles") or []
        if len(obs) > 4:
            solids = [o for o in obs if o.get("type") == "solid"][:2]
            breaks = [o for o in obs if o.get("type") == "breakable"][:2]
            seg["obstacles"] = solids + breaks
        seg["gimmickNote"] = "벽 촉수 3 — 상하 고정 위협, 포자 사이로 위치 선정"
        if "environment" in seg:
            del seg["environment"]

    elif sid == "seg_hive_lancer_rush":
        # Fast lancers need open space — no corridor, 2 tentacles only.
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        spawns.extend(
            [
                spawn(60, "hive_tentacle", 5.75),
                spawn(60, "hive_tentacle", -5.75),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        if "environment" in seg:
            del seg["environment"]
        seg["gimmickNote"] = "벽 촉수 2 — 랜서 러시 레인 경계"

    elif sid == "seg_hive_brood_wave":
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        spawns.extend(
            [
                spawn(100, "hive_tentacle", 5.25),
                spawn(100, "hive_tentacle", -5.25),
                spawn(400, "hive_tentacle", 0.0),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        if "environment" in seg:
            del seg["environment"]
        seg["gimmickNote"] = "벽 촉수 3 — 브루드 웨이브 사이 앵커"

    elif sid == "seg_hive_hornet_dive":
        # Dive enemies + open field; 2 tentacles as posts.
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        spawns.extend(
            [
                spawn(90, "hive_tentacle", 5.5),
                spawn(90, "hive_tentacle", -5.5),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        if "environment" in seg:
            del seg["environment"]
        seg["gimmickNote"] = "벽 촉수 2 — 호넷 다이브 회피 축"

    elif sid == "seg_hive_organic_pulse":
        # Mild narrowing: start ±9.0 → end ±5.0 (width 18→10u). Telegraph whole segment.
        # Avoid mid-tier overload: keep existing spawns, add 2 tentacles on walls.
        merge_env(
            seg,
            {
                **corridor(-9.0, 9.0, -5.0, 5.0, 1),
            },
        )
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        # Place tentacles early so player sees them before walls close.
        spawns.extend(
            [
                spawn(40, "hive_tentacle", 6.5),
                spawn(40, "hive_tentacle", -6.5),
                spawn(300, "hive_tentacle", 4.0),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        # Cap obstacles at 4 so total terrain pressure stays readable.
        obs = seg.get("obstacles") or []
        solids = [o for o in obs if o.get("type") == "solid"][:2]
        breaks = [o for o in obs if o.get("type") == "breakable"][:2]
        seg["obstacles"] = solids + breaks
        seg["gimmickNote"] = "촉수 3 + 통로 18→10u — 예고 후 완만 수축"

    elif sid == "seg_hive_nest_choke":
        # Stronger choke: start ±8.5 → end ±3.75 (width 17→7.5u).
        # 7.5u >> player 0.75u; at 21.5u/s vertical cross ≈ 0.35s — fair if telegraphed.
        # Light spawns only already (n12); add 2 tentacles, keep obstacles ≤4.
        merge_env(
            seg,
            {
                **corridor(-8.5, 8.5, -3.75, 3.75, 1),
            },
        )
        spawns = strip_enemy_spawns(seg.get("spawns") or [], "hive_tentacle")
        # Remove heavy mid if any concurrent with choke — strip elite-ish during late ticks?
        # Keep catalog spawns; add edge tentacles only.
        spawns.extend(
            [
                spawn(30, "hive_tentacle", 5.75),
                spawn(30, "hive_tentacle", -5.75),
            ]
        )
        spawns.sort(key=lambda s: (s["tick"], s["y"]))
        seg["spawns"] = spawns
        obs = seg.get("obstacles") or []
        solids = [o for o in obs if o.get("type") == "solid"][:2]
        breaks = [o for o in obs if o.get("type") == "breakable"][:2]
        seg["obstacles"] = solids + breaks
        seg["gimmickNote"] = "촉수 2 + 통로 17→7.5u — 네스트 초크, 위치 선정 강제"


def apply_fortress(seg: dict) -> None:
    sid = seg["id"]
    # Reuse laserEmitter gates. Keep solids as wall frame; thin breakables.
    # Timing: telegraph ≥30t (0.5s), open window ≥1s between beams.

    # Horizontal gate lasers: beam shoots left across field.
    gate_hi = laser(cycle=150, telegraph=36, firing=6, sustain=24, dissipate=12, end_x=-38.0)
    gate_lo = laser(cycle=180, telegraph=42, firing=6, sustain=30, dissipate=12, end_x=-38.0)
    gate_mid = laser(cycle=120, telegraph=30, firing=5, sustain=20, dissipate=10, end_x=-36.0)
    # Vertical-ish slant for variety
    gate_slant = laser(
        cycle=160,
        telegraph=40,
        firing=6,
        sustain=28,
        dissipate=12,
        end_x=-30.0,
        end_y=-4.0,
    )

    base_solids = {
        "seg_fortress_sentry_grid": [solid(10.0, 6.5), solid(14.0, -6.5)],
        "seg_fortress_interceptor_assault": [solid(10.0, 6.5), solid(14.0, -6.5)],
        "seg_fortress_mortar_line": [solid(11.0, 6.0), solid(16.0, -6.0)],
        "seg_fortress_turret_cross": [solid(10.0, 6.5), solid(14.0, -6.5), solid(18.0, 5.5)],
        "seg_fortress_drone_lattice": [
            solid(10.0, 6.5),
            solid(14.0, -6.5),
            solid(18.0, 5.5),
            solid(18.0, -5.5),
        ],
        "seg_fortress_armored_gate": [
            solid(10.0, 6.5),
            solid(14.0, -6.5),
            solid(18.0, 5.5),
            solid(18.0, -5.5),
        ],
    }

    if sid == "seg_fortress_sentry_grid":
        # 2 gates: top & bottom rails, desynced cycles.
        seg["obstacles"] = base_solids[sid] + [
            emitter(16.0, 4.0, gate_hi),
            emitter(16.0, -4.0, gate_lo),
            brk(12.5, 0.0, 30),
            brk(18.5, 1.5, 35),
        ]
        seg["gimmickNote"] = "레이저 게이트 2 (주기 150/180) — 상하 타이밍 회피"

    elif sid == "seg_fortress_interceptor_assault":
        # Single mid gate so dive enemies remain dodgeable.
        seg["obstacles"] = base_solids[sid] + [
            emitter(15.0, 0.0, gate_mid),
            brk(12.0, 3.0, 28),
            brk(12.0, -3.0, 28),
            brk(18.0, 0.0, 40),
        ]
        seg["gimmickNote"] = "중앙 레이저 1 (주기 120) — 인터셉터 사이로 통과 타이밍"

    elif sid == "seg_fortress_mortar_line":
        seg["obstacles"] = base_solids[sid] + [
            emitter(17.0, 3.25, gate_hi),
            emitter(17.0, -3.25, gate_lo),
            brk(13.0, 0.0, 35),
            brk(19.0, 2.0, 40),
        ]
        seg["gimmickNote"] = "레이저 2 + 박격 라인 — 이중 타이밍"

    elif sid == "seg_fortress_turret_cross":
        seg["obstacles"] = base_solids[sid] + [
            emitter(15.5, 2.5, gate_mid),
            emitter(15.5, -2.5, gate_hi),
            brk(12.0, 0.0, 35),
            brk(19.0, -1.5, 40),
        ]
        seg["gimmickNote"] = "포탑 벽 + 레이저 2 — 교차 사선 타이밍"

    elif sid == "seg_fortress_drone_lattice":
        # 3 gates max concurrent lasers well under MaxLasers=8.
        seg["obstacles"] = base_solids[sid] + [
            emitter(13.0, 4.5, gate_hi),
            emitter(16.0, 0.0, gate_mid),
            emitter(19.0, -4.5, gate_lo),
            brk(14.5, 2.0, 40),
            brk(17.5, -2.0, 45),
        ]
        seg["gimmickNote"] = "레이저 3 (120/150/180 비동기) — 격자 타이밍 퍼즐"

    elif sid == "seg_fortress_armored_gate":
        seg["obstacles"] = base_solids[sid] + [
            emitter(12.0, 3.5, gate_hi),
            emitter(15.5, -1.0, gate_slant),
            emitter(19.0, -3.5, gate_lo),
            brk(14.0, 1.5, 45),
            brk(17.5, 0.0, 50),
        ]
        seg["gimmickNote"] = "장갑 게이트 레이저 3 — 보스 전 최종 타이밍 시험"


def apply_nebula(seg: dict) -> None:
    sid = seg["id"]
    # visionObscured is theme-wide. Per-segment signed drift alternating directions.
    # Strength ramp: early ~0.45–0.55, mid ~0.6–0.7, late ~0.75–0.9 composite.
    # Keep existing obstacles (terrain crystals).

    profiles = {
        # (x/s, y/s) — composite ≈ hypot
        "seg_nebula_wisp_storm": (0.45, 0.20),  # ~0.49
        "seg_nebula_wisp_ribbon": (-0.35, 0.40),  # ~0.53, opposite X
        "seg_nebula_echo_ribbon": (0.15, -0.55),  # ~0.57, down-biased
        "seg_nebula_void_moth_swarm": (-0.55, -0.25),  # ~0.60
        "seg_nebula_crystal_drift": (0.70, 0.35),  # ~0.78 late
        "seg_nebula_prism_haze": (-0.40, 0.80),  # ~0.89 late peak
    }
    if sid in profiles:
        x_ps, y_ps = profiles[sid]
        merge_env(seg, {**drift(x_ps, y_ps)})
        mag = (x_ps * x_ps + y_ps * y_ps) ** 0.5
        seg["gimmickNote"] = (
            f"시야 제한 + 드리프트 ({x_ps:+.2f},{y_ps:+.2f}) u/s ≈{mag:.2f} — 보정 조준"
        )


def apply_core(seg: dict) -> None:
    sid = seg["id"]
    # Mix of prior gimmicks. Avoid stacking tight corridor + mid-heavy + multi laser.

    gate_a = laser(cycle=150, telegraph=36, firing=6, sustain=24, dissipate=12, end_x=-36.0)
    gate_b = laser(cycle=180, telegraph=42, firing=6, sustain=28, dissipate=12, end_x=-36.0)

    if sid == "seg_core_guardian_wall":
        # Debris cover + mild drift (no corridor, guardians need space).
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:3]
        seg["obstacles"] = solids + [
            brk(12.0, 2.0, 45),
            brk(14.5, -2.5, 50),
            brk(17.0, 0.5, 55),
            brk(19.0, 3.0, 48),
        ]
        merge_env(seg, {**drift(0.30, -0.20)})  # mild ~0.36
        seg["gimmickNote"] = "잔해 4 + 약 드리프트 — 가디언 벽 엄폐"

    elif sid == "seg_core_final_gauntlet":
        # Lasers + breakables, open vertical (no corridor).
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:3]
        seg["obstacles"] = solids + [
            emitter(15.0, 3.5, gate_a),
            emitter(18.0, -3.5, gate_b),
            brk(12.0, 0.0, 50),
            brk(16.5, 1.5, 55),
        ]
        if "environment" in seg:
            del seg["environment"]
        seg["gimmickNote"] = "레이저 2 + 잔해 2 — 최종 게릴라 타이밍"

    elif sid == "seg_core_rift_blades":
        # Fast blades need space — weak drift only, light debris.
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:2]
        seg["obstacles"] = solids + [
            brk(13.0, 2.5, 40),
            brk(16.0, -2.5, 40),
            brk(19.0, 0.0, 45),
        ]
        merge_env(seg, {**drift(-0.35, 0.25)})  # ~0.43
        seg["gimmickNote"] = "잔해 3 + 드리프트 — 리프트 블레이드 보정 회피"

    elif sid == "seg_core_phase_discs":
        # Static discs + mild corridor (not tight) + one laser.
        merge_env(
            seg,
            {
                **corridor(-9.0, 9.0, -5.5, 5.5, 1),
            },
        )
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:2]
        seg["obstacles"] = solids + [
            emitter(17.0, 0.0, gate_a),
            brk(13.0, 2.0, 45),
            brk(13.0, -2.0, 45),
        ]
        seg["gimmickNote"] = "통로 18→11u + 레이저 1 — 페이즈 디스크 위치 선정"

    elif sid == "seg_core_shard_battery":
        # Heavy solids already; add breakable cover + drift, no tight corridor.
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:4]
        seg["obstacles"] = solids + [
            brk(12.5, 1.0, 55),
            brk(15.0, -2.0, 55),
            brk(18.0, 2.5, 60),
            emitter(16.0, -4.0, gate_b),
        ]
        merge_env(seg, {**drift(0.50, 0.30)})  # ~0.58
        seg["gimmickNote"] = "잔해 3 + 레이저 1 + 드리프트 — 샤드 배터리 종합"

    elif sid == "seg_core_void_mix":
        # Mild corridor + debris + light drift. No multi-laser (overload with void mix).
        merge_env(
            seg,
            {
                **corridor(-8.5, 8.5, -4.5, 4.5, 1),
                **drift(-0.40, 0.45),  # ~0.60
            },
        )
        solids = [o for o in (seg.get("obstacles") or []) if o.get("type") == "solid"][:3]
        seg["obstacles"] = solids + [
            brk(12.0, 0.0, 50),
            brk(15.5, 2.5, 55),
            brk(18.5, -2.0, 55),
            brk(14.0, -3.0, 48),
        ]
        seg["gimmickNote"] = "통로 17→9u + 잔해 4 + 드리프트 — 보이드 믹스 종합"


def apply_waves(waves: dict) -> None:
    # Theme-wide gimmicks.
    # Core time limit 9000 ticks = 150s:
    #   segs max 3×900=45s → boss budget 105s
    #   boss_core 28000 HP @ expected 1050 DPS → 26.7s (≈3.9× margin)
    #   struggle 700 DPS → 40s (≈2.6×); 500 DPS → 56s (≈1.9×)
    waves["gimmicks"] = [
        {"theme": "scrapyard", "visionObscured": False, "timeLimitTicks": 0},
        {"theme": "hive", "visionObscured": False, "timeLimitTicks": 0},
        {"theme": "fortress", "visionObscured": False, "timeLimitTicks": 0},
        {"theme": "nebula", "visionObscured": True, "timeLimitTicks": 0},
        {"theme": "core", "visionObscured": False, "timeLimitTicks": 9000},
    ]

    for seg in waves["segments"]:
        theme = seg.get("theme")
        if theme == "scrapyard":
            apply_scrapyard(seg)
        elif theme == "hive":
            apply_hive(seg)
        elif theme == "fortress":
            apply_fortress(seg)
        elif theme == "nebula":
            apply_nebula(seg)
        elif theme == "core":
            apply_core(seg)
        else:
            # Shared intro segments: no stage gimmick (tutorial clarity).
            if "environment" in seg:
                del seg["environment"]
            if "gimmickNote" in seg:
                del seg["gimmickNote"]


def validate_caps(waves: dict) -> None:
    max_obs = 0
    max_laser = 0
    max_tentacle = 0
    hive_max = 0
    for seg in waves["segments"]:
        obs = seg.get("obstacles") or []
        n = len(obs)
        max_obs = max(max_obs, n)
        lasers = sum(1 for o in obs if o.get("type") == "laserEmitter")
        max_laser = max(max_laser, lasers)
        tents = sum(
            1 for s in (seg.get("spawns") or []) if s.get("enemyId") == "hive_tentacle"
        )
        # Concurrent tentacles: those with same/early tick stay until scroll off.
        # Bound by spawn count as upper estimate.
        max_tentacle = max(max_tentacle, tents)
        if seg.get("theme") == "hive":
            # obstacles only (tentacles are enemies)
            hive_max = max(hive_max, n)
        assert n <= 32, f"{seg['id']} obstacles {n} > MaxObstacles 32"
        assert lasers <= 8, f"{seg['id']} lasers {lasers} > MaxLasers 8"
        # corridor width check
        env = seg.get("environment") or {}
        cor = env.get("corridor")
        if cor:
            for key in ("start", "end"):
                mn = cor[f"{key}MinY"]
                mx = cor[f"{key}MaxY"]
                width = mx - mn
                assert width >= 5.0, f"{seg['id']} {key} corridor width {width} < 5u"
                # player halfH 0.375 → needs width > 0.75
                assert width > 0.75 + 1.0, f"{seg['id']} corridor too tight for player"
        # drift magnitude
        dr = env.get("drift")
        if dr:
            mag = (dr["xPerSecond"] ** 2 + dr["yPerSecond"] ** 2) ** 0.5
            assert mag <= 0.95, f"{seg['id']} drift {mag} too strong"
    assert hive_max <= 5, f"hive max obstacles {hive_max} > 5"
    print(
        f"caps ok: maxObs={max_obs} maxLaser/seg={max_laser} "
        f"maxTentacleSpawns={max_tentacle} hiveMaxObs={hive_max}"
    )


def main() -> None:
    enemies = json.loads(ENEMIES.read_text(encoding="utf-8"))
    waves = json.loads(WAVES.read_text(encoding="utf-8"))

    ensure_tentacle_enemy(enemies)
    apply_waves(waves)
    validate_caps(waves)

    ENEMIES.write_text(
        json.dumps(enemies, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    WAVES.write_text(
        json.dumps(waves, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"wrote {ENEMIES.relative_to(ROOT)}")
    print(f"wrote {WAVES.relative_to(ROOT)}")
    print("gimmicks:", json.dumps(waves["gimmicks"], ensure_ascii=False))


if __name__ == "__main__":
    main()
