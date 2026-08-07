#!/usr/bin/env python3
"""REQ-103a: stage overhaul pass 1 — existing-schema redesign of waves.json.

Applies:
1. Late-stage traversableLaneMasks staircase 7→3→2 (theme-scaled intensity)
2. Boss-prep valley: end spawn gap clamped to [120, 180] ticks
3. Early/late spawn character split (open swarms vs terrain+static turrets)
4. Intent annotations for the four-beat flow (no schema field for scroll speed)

Does NOT invent Core fields (scrollSpeedMultiplier, blocksEnemyBullets, etc.).
"""
from __future__ import annotations

import json
import copy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WAVES = ROOT / "GameData" / "waves.json"

VALLEY_MIN = 120
VALLEY_TARGET = 150  # midpoint of 120..180
VALLEY_MAX = 180

# Lane bits (laneCount=3): 7=all, 3=mid+bot, 2=center, 6=top+mid
MASK_OPEN = 7
MASK_TWO = 3
MASK_CENTER = 2

# Static / "emplacement" enemies used to re-character late segments.
STATIC_ENEMIES = {
    "turret_ground",
    "turret_ceiling",
    "hive_tentacle",
    "sentry_drone",
    "mortar_drone",
    "laser_sentry",
    "phase_disc",
    "prism_beamer",
}

# Mobile swarm fodder for early open volume.
SWARM_ENEMIES = {
    "zako_straight",
    "zako_sine",
    "zako_fast",
    "rust_skimmer",
    "pipe_rat",
    "junk_roller",
    "lancer_dart",
    "interceptor_rush",
    "spore_drifter",
    "wisp_spark",
    "sting_hornet",
    "rift_blade",
    "void_moth",
    "echo_wisp",
}


def last_spawn_tick(seg: dict) -> int:
    spawns = seg.get("spawns") or []
    if not spawns:
        return 0
    return max(int(s["tick"]) for s in spawns)


def ensure_boss_valley(seg: dict) -> str | None:
    """Guarantee end gap in [VALLEY_MIN, VALLEY_MAX] by extending lengthTicks.

    Prefer extending length over deleting spawns so HP load stays intact.
    If gap already > VALLEY_MAX, leave it (generous valley is fine).
    """
    length = int(seg["lengthTicks"])
    last = last_spawn_tick(seg)
    if last == 0:
        # No spawns: ensure length itself allows a full valley beat.
        if length < VALLEY_MIN:
            seg["lengthTicks"] = VALLEY_TARGET
            return f"empty-seg length {length}->{VALLEY_TARGET}"
        return None

    gap = length - last
    if gap < VALLEY_MIN:
        new_len = last + VALLEY_TARGET
        seg["lengthTicks"] = new_len
        return f"valley pad {gap}->{new_len - last} (len {length}->{new_len})"
    return None


def encroachment_masks(theme: str | None, d_min: int, d_max: int, seg_id: str) -> list[int] | None:
    """Return new multi-checkpoint masks, or None to leave unchanged.

    Design: late band = progressive 7→3→2. Stage intensity:
      scrapyard (St1 feel): mild 7→3
      hive (St2): 7→3 or 7→3→2
      fortress (St3): 7→3→2
      nebula (St4 lightning dodge): 7→3→2
      core (St5 max): 7→3→2→2
      unthemed late: 7→3→2

    Early open volume keeps single open mask; intentional early chokes
    (center-only tutorial segs) are left alone.
    """
    # Tutorial / early open: do not force encroachment.
    early_keep_ids = {
        "seg_intro_line",
        "seg_sine_pair",
        "seg_sine_rush",
        "seg_swarm_fast",
        "seg_scrap_debris_line",
        "seg_scrap_pipe_dash",
        "seg_scrap_zigzag_posts",
        "seg_scrap_shard_field",
        "seg_scrap_center_breach",
        "seg_scrap_skimmer_weave",
        "seg_scrap_rail_split",
    }
    if seg_id in early_keep_ids:
        # Ensure early open volume where design wants open lanes.
        open_early = {
            "seg_intro_line",
            "seg_sine_rush",
            "seg_swarm_fast",
            "seg_scrap_debris_line",
            "seg_scrap_pipe_dash",
            "seg_scrap_zigzag_posts",
            "seg_scrap_shard_field",
            "seg_scrap_center_breach",
        }
        if seg_id in open_early:
            return [MASK_OPEN]
        return None  # keep intentional center chokes (sine_pair, rail_split, weave)

    # Mid "workhorse" that should stay relatively open until true late.
    if d_max <= 2:
        return [MASK_OPEN]

    # Mild mid band (can appear mid-stage): gentle two-step or open.
    if d_min <= 2 and d_max <= 4:
        # Theme mid character
        if theme == "scrapyard":
            return [MASK_OPEN, MASK_OPEN]  # still open canyon intro
        if theme in (None, "hive", "nebula"):
            return [MASK_OPEN]
        if theme == "fortress":
            return [MASK_OPEN, 6]  # slight top-bias toward hull line
        if theme == "core":
            return [MASK_OPEN, MASK_TWO]
        return [MASK_OPEN]

    # True late band: difficultyMin ≥ 3 (or high max with min≥3 already)
    # Also catch d 2-5 segs that are themed "late character" by id.
    late_ids = {
        "seg_sandwich",
        "seg_scrap_rust_gauntlet",
        "seg_scrap_junk_corridor",
        "seg_scrap_tumbler_pack",
        "seg_hive_organic_pulse",
        "seg_hive_nest_choke",
        "seg_hive_membrane_wall",
        "seg_hive_lancer_rush",
        "seg_fortress_armored_gate",
        "seg_fortress_drone_lattice",
        "seg_fortress_turret_cross",
        "seg_fortress_crossfire_alley",
        "seg_fortress_interceptor_assault",
        "seg_nebula_crystal_drift",
        "seg_nebula_prism_haze",
        "seg_nebula_drift_lattice",
        "seg_core_final_gauntlet",
        "seg_core_shard_battery",
        "seg_core_void_mix",
        "seg_core_phase_columns",
        "seg_core_guardian_wall",
    }

    is_late = d_min >= 3 or seg_id in late_ids or d_min >= 4
    if not is_late:
        # residual mid (d2-5 themed early-ish)
        if theme == "scrapyard":
            return [MASK_OPEN]
        if theme == "hive":
            return [MASK_OPEN, MASK_TWO]
        if theme == "fortress":
            return [MASK_OPEN, 6]
        if theme == "nebula":
            return [MASK_OPEN]
        if theme == "core":
            return [MASK_OPEN, MASK_TWO]
        return [MASK_OPEN]

    # --- Late encroachment intensity by theme (St1..St5) ---
    if theme == "scrapyard":
        # St1 late: 7→3 only (design: not full center lock)
        if d_min >= 3 or "rust" in seg_id or "junk" in seg_id:
            return [MASK_OPEN, MASK_TWO, MASK_TWO]
        return [MASK_OPEN, MASK_TWO]

    if theme == "hive":
        # St2: cell-corridor encroachment 7→3→2
        if d_min >= 3 or "membrane" in seg_id or "nest" in seg_id or "organic" in seg_id:
            return [MASK_OPEN, MASK_TWO, MASK_CENTER]
        return [MASK_OPEN, MASK_TWO]

    if theme == "fortress":
        # St3 hull turret line: full staircase
        if d_min >= 3 or "armored" in seg_id or "crossfire" in seg_id or "drone" in seg_id:
            return [MASK_OPEN, MASK_TWO, MASK_CENTER]
        return [MASK_OPEN, 6, MASK_TWO]

    if theme == "nebula":
        # St4 lightning dodge → center choke
        if d_min >= 3 or "crystal" in seg_id or "prism" in seg_id or "lattice" in seg_id:
            return [MASK_OPEN, MASK_TWO, MASK_CENTER]
        return [MASK_OPEN, MASK_TWO]

    if theme == "core":
        # St5 maximum encroachment
        if d_min >= 4 or "final" in seg_id or "void" in seg_id or "phase_columns" in seg_id:
            return [MASK_OPEN, MASK_TWO, MASK_CENTER, MASK_CENTER]
        return [MASK_OPEN, MASK_TWO, MASK_CENTER]

    # unthemed late (sandwich etc.)
    if d_min >= 3:
        return [MASK_OPEN, MASK_TWO, MASK_CENTER]
    return [MASK_OPEN, MASK_TWO]


def is_static(enemy_id: str) -> bool:
    return enemy_id in STATIC_ENEMIES


def is_swarm(enemy_id: str) -> bool:
    return enemy_id in SWARM_ENEMIES


def recharacter_spawns(seg: dict) -> str | None:
    """Bias spawn tables: early = open volume, late = static emplacements.

    Keeps spawn count and tick schedule; only swaps enemyIds where it strengthens
    the early/late beat without inventing new spawn rows.
    """
    theme = seg.get("theme")
    d_min = int(seg["difficultyMin"])
    d_max = int(seg["difficultyMax"])
    seg_id = seg["id"]
    spawns = seg.get("spawns") or []
    if not spawns:
        return None

    notes = []

    # --- Early open volume: ensure swarm fodder on stage-1 / open segs ---
    early = d_max <= 2 or seg_id in {
        "seg_intro_line",
        "seg_sine_pair",
        "seg_sine_rush",
        "seg_swarm_fast",
        "seg_scrap_debris_line",
        "seg_scrap_pipe_dash",
        "seg_scrap_zigzag_posts",
        "seg_scrap_shard_field",
    }
    if early:
        # Replace any accidental static on pure early open with swarm.
        swapped = 0
        for s in spawns:
            if is_static(s["enemyId"]) and s["enemyId"] not in ("turret_ground",):
                # keep turrets off early open; only scrap may teach breakables, not emplacements
                pass
        # No aggressive rewrite for early — already swarm-heavy.
        return None

    # --- Late character: inject static presence on high-d themed segs ---
    late = d_min >= 3 or seg_id in {
        "seg_scrap_rust_gauntlet",
        "seg_hive_organic_pulse",
        "seg_hive_nest_choke",
        "seg_hive_membrane_wall",
        "seg_fortress_armored_gate",
        "seg_fortress_drone_lattice",
        "seg_fortress_turret_cross",
        "seg_fortress_crossfire_alley",
        "seg_nebula_crystal_drift",
        "seg_nebula_prism_haze",
        "seg_core_final_gauntlet",
        "seg_core_void_mix",
        "seg_core_phase_columns",
        "seg_core_shard_battery",
        "seg_sandwich",
    }
    if not late:
        return None

    static_count = sum(1 for s in spawns if is_static(s["enemyId"]))
    swarm_count = sum(1 for s in spawns if is_swarm(s["enemyId"]))
    total = len(spawns)
    if total == 0:
        return None

    # Theme-specific static replacement target.
    theme_static = {
        "scrapyard": "turret_ground",  # proxy until scrap-shield Core exists
        "hive": "hive_tentacle",
        "fortress": "mortar_drone",  # avoid laser peak vs design≤4
        "nebula": "phase_disc",
        "core": "phase_disc",  # not prism_beamer — laser peak gate ≤4
        None: "turret_ground",
    }
    target_static = theme_static.get(theme, "turret_ground")

    # Want at least ~30% static on late terrain segs (already many fortress segs exceed this).
    want_static = max(2, (total + 2) // 3)
    if static_count >= want_static:
        return None

    need = want_static - static_count
    # Convert late-half swarm fodder into static emplacements (terrain fight).
    mid_tick = last_spawn_tick(seg) // 2
    candidates = [
        s
        for s in spawns
        if is_swarm(s["enemyId"]) and int(s["tick"]) >= mid_tick
    ]
    # Prefer outer Y (edge emplacements)
    candidates.sort(key=lambda s: (-abs(float(s["y"])), int(s["tick"])))
    for s in candidates[:need]:
        old = s["enemyId"]
        s["enemyId"] = target_static
        # Emplacements hug edges: clamp |y| to ≥3.5 if too center
        y = float(s["y"])
        if abs(y) < 3.5:
            s["y"] = 4.5 if y >= 0 else -4.5
        notes.append(f"{old}->{target_static}@{s['tick']}")

    if notes:
        return f"late static x{len(notes)}: " + ", ".join(notes[:4])
    return None


def annotate_intent(seg: dict, masks: list[int], valley_note: str | None, char_note: str | None) -> None:
    """Refresh intent string so data authors can see the 4-beat role."""
    theme = seg.get("theme") or "shared"
    d_min = seg["difficultyMin"]
    d_max = seg["difficultyMax"]
    mask_s = "→".join(str(m) for m in masks)
    role = "early-open" if d_max <= 2 else ("late-encroach" if d_min >= 3 or len(masks) >= 3 else "mid")
    bits = [f"REQ103a {role}", f"masks[{mask_s}]", f"theme={theme}"]
    if valley_note:
        bits.append("valley")
    if char_note:
        bits.append("static+")
    # Preserve human flavor if present, but always stamp the REQ tag.
    prev = seg.get("intent") or ""
    if "REQ103a" in prev:
        # rewrite cleanly
        flavor = prev.split("|")[-1].strip() if "|" in prev else ""
        seg["intent"] = " | ".join(bits) + (f" | {flavor}" if flavor else "")
    else:
        flavor = prev.strip()
        seg["intent"] = " | ".join(bits) + (f" | {flavor}" if flavor else "")


def main() -> None:
    with WAVES.open(encoding="utf-8") as f:
        data = json.load(f)

    segs = data["segments"]
    assert len(segs) == 48, f"expected 48 segments, got {len(segs)}"

    report = {
        "valley": [],
        "masks": [],
        "character": [],
        "summary": {},
    }

    for seg in segs:
        seg_id = seg["id"]
        theme = seg.get("theme")
        d_min = int(seg["difficultyMin"])
        d_max = int(seg["difficultyMax"])

        # 1) Boss valley
        vnote = ensure_boss_valley(seg)
        if vnote:
            report["valley"].append(f"{seg_id}: {vnote}")

        # 2) Encroachment masks
        new_masks = encroachment_masks(theme, d_min, d_max, seg_id)
        if new_masks is not None:
            old = list(seg.get("traversableLaneMasks") or [])
            if old != new_masks:
                seg["traversableLaneMasks"] = new_masks
                report["masks"].append(f"{seg_id}: {old} -> {new_masks}")
            masks = new_masks
        else:
            masks = list(seg.get("traversableLaneMasks") or [7])

        # entry/exit stay 7 for clearability with multi-step narrowing
        # (unless already specialized — keep)
        if len(masks) >= 2:
            # Ensure entry is open enough to start the staircase
            if int(seg.get("entryLaneMask", 7)) != 7 and d_min >= 3:
                seg["entryLaneMask"] = 7
            if int(seg.get("exitLaneMask", 7)) & MASK_CENTER == 0 and MASK_CENTER in masks:
                # exit must include final traversable lane
                seg["exitLaneMask"] = int(seg.get("exitLaneMask", 7)) | MASK_CENTER

        # 3) Spawn character
        cnote = recharacter_spawns(seg)
        if cnote:
            report["character"].append(f"{seg_id}: {cnote}")

        annotate_intent(seg, masks, vnote, cnote)

    # Final valley audit
    short_gap = []
    multi_late = 0
    for seg in segs:
        last = last_spawn_tick(seg)
        gap = int(seg["lengthTicks"]) - last if last else int(seg["lengthTicks"])
        if last and gap < VALLEY_MIN:
            short_gap.append((seg["id"], gap))
        masks = seg.get("traversableLaneMasks") or []
        if int(seg["difficultyMin"]) >= 3 and len(masks) >= 2:
            multi_late += 1

    report["summary"] = {
        "segments": len(segs),
        "valley_padded": len(report["valley"]),
        "masks_changed": len(report["masks"]),
        "character_changed": len(report["character"]),
        "late_multi_mask": multi_late,
        "remaining_short_gaps": short_gap,
    }

    with WAVES.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(json.dumps(report["summary"], indent=2))
    print(f"\nvalley changes: {len(report['valley'])}")
    for line in report["valley"][:20]:
        print(" ", line)
    if len(report["valley"]) > 20:
        print(f"  ... +{len(report['valley'])-20} more")
    print(f"\nmask changes: {len(report['masks'])}")
    for line in report["masks"]:
        print(" ", line)
    print(f"\ncharacter changes: {len(report['character'])}")
    for line in report["character"]:
        print(" ", line)
    if short_gap:
        print("WARNING short gaps remain:", short_gap)


if __name__ == "__main__":
    main()
