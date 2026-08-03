"""REQ-126/127 pre-analysis for fortress solids and lengthTicks."""
import json
import random
from collections import Counter, defaultdict
from statistics import median, mean, pstdev
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
with open(ROOT / "GameData" / "waves.json", "r", encoding="utf-8") as f:
    data = json.load(f)

segments = data["segments"]
TICK_HZ = 60.0

print("=== TOP CONFIG ===")
print("segmentsPerStage", data["segmentsPerStage"])
print("closingSegmentsPerStage", data["closingSegmentsPerStage"])
print("scrollSpeed", data["scrollSpeed"])
print("laneCount", data["laneCount"])
print("themes", data["themes"])
print("segment count", len(segments))

lengths = [s["lengthTicks"] for s in segments]
print("\n=== lengthTicks ===")
print(
    "min", min(lengths),
    "max", max(lengths),
    "median", median(lengths),
    "mean", round(mean(lengths), 1),
    "stdev", round(pstdev(lengths), 1) if len(lengths) > 1 else 0,
)
print("ratio max/min", round(max(lengths) / min(lengths), 2))
buckets = Counter((L // 50) * 50 for L in lengths)
print("buckets of 50:", dict(sorted(buckets.items())))

n_early = data["segmentsPerStage"]
n_late = data["closingSegmentsPerStage"]
mean_len = mean(lengths)
med_len = median(lengths)
print("\n=== EXPECTED STAGE TIME (mean length) ===")
print(
    f"early: {n_early} * {mean_len:.0f} = {n_early * mean_len:.0f} ticks "
    f"= {n_early * mean_len / TICK_HZ:.1f}s"
)
print(
    f"late:  {n_late} * {mean_len:.0f} = {n_late * mean_len:.0f} ticks "
    f"= {n_late * mean_len / TICK_HZ:.1f}s"
)
print(f"ratio late/early = {n_late / n_early:.2f}")
print(
    f"using median: early={n_early * med_len / TICK_HZ:.1f}s "
    f"late={n_late * med_len / TICK_HZ:.1f}s"
)

random.seed(0)


def sample_stage_times(n_seg, trials=5000):
    times = []
    for _ in range(trials):
        picks = [random.choice(lengths) for _ in range(n_seg)]
        times.append(sum(picks) / TICK_HZ)
    return min(times), max(times), mean(times), pstdev(times)


for label, n in [("early", n_early), ("late", n_late)]:
    mn, mx, mu, sd = sample_stage_times(n)
    print(
        f"stage duration {label} n={n}: min={mn:.1f}s max={mx:.1f}s "
        f"mean={mu:.1f}s stdev={sd:.1f}s ratio={mx / mn:.2f}"
    )

print("\n=== THEMES ===")
print(Counter(s.get("theme", "(none)") for s in segments))

obs_types = Counter()
for s in segments:
    for o in s.get("obstacles") or []:
        obs_types[o.get("type", "?")] += 1
print("\n=== ALL OBS TYPES ===", dict(obs_types))


def is_fortress(s):
    return (
        s.get("theme") == "fortress"
        or "fortress" in s.get("id", "")
        or "theme=fortress" in s.get("intent", "")
    )


fort = [s for s in segments if is_fortress(s)]
print(f"\n=== FORTRESS SEGMENTS === count={len(fort)}")

fort_solid_total = 0
fort_breakable = 0
fort_laser = 0
for s in fort:
    obs = s.get("obstacles") or []
    by_type = Counter(o.get("type") for o in obs)
    solids = [o for o in obs if o.get("type") == "solid"]
    fort_solid_total += len(solids)
    fort_breakable += by_type.get("breakable", 0)
    fort_laser += by_type.get("laserEmitter", 0)
    spawns = s.get("spawns") or []
    ys = sorted({sp.get("y") for sp in spawns if "y" in sp})
    y_hist = Counter(sp.get("y") for sp in spawns if "y" in sp)
    print(
        f"\n{s['id']} theme={s.get('theme')} len={s['lengthTicks']} "
        f"weight={s.get('weight')}"
    )
    print(f"  difficulty {s.get('difficultyMin')}-{s.get('difficultyMax')}")
    print(
        f"  masks entry={s.get('entryLaneMask')} exit={s.get('exitLaneMask')} "
        f"trav={s.get('traversableLaneMasks')}"
    )
    print(f"  obstacles: {dict(by_type)} solids={len(solids)}")
    for o in solids:
        print(f"    solid x={o['x']} y={o['y']} hp={o.get('hp')}")
    print(f"  spawns={len(spawns)} unique y={ys}")
    print(
        f"  y hist={dict(sorted(y_hist.items(), key=lambda kv: (kv[0] is None, kv[0])))}"
    )
    print(f"  enemies={Counter(sp.get('enemyId') for sp in spawns)}")
    print(f"  total obs count={len(obs)}")

print("\n=== FORTRESS SOLID SUMMARY ===")
print(f"fortress segments: {len(fort)}")
print(f"fortress solid total: {fort_solid_total}")
print(f"fortress breakable: {fort_breakable}")
print(f"fortress laserEmitter: {fort_laser}")
print(
    f"avg solid per fortress seg: "
    f"{fort_solid_total / len(fort) if fort else 0:.2f}"
)

non_fort = [s for s in segments if not is_fortress(s)]
nf_solid = sum(
    1
    for s in non_fort
    for o in (s.get("obstacles") or [])
    if o.get("type") == "solid"
)
print(
    f"non-fortress solid total: {nf_solid} over {len(non_fort)} segs "
    f"avg={nf_solid / len(non_fort):.2f}"
)

print("\n=== SOLID Y DISTRIBUTION BY THEME ===")
for theme in list(data["themes"]) + ["(none)"]:
    segs = [s for s in segments if s.get("theme", "(none)") == theme]
    if not segs:
        continue
    solid_ys = []
    for s in segs:
        for o in s.get("obstacles") or []:
            if o.get("type") == "solid":
                solid_ys.append(o["y"])
    print(
        f"  {theme}: segs={len(segs)} solids={len(solid_ys)} "
        f"y_vals={sorted(Counter(solid_ys).items())}"
    )

print("\n=== HORIZONTAL SOLID ROWS (same y, multi x) examples ===")
examples = 0
for s in segments:
    solids = [o for o in (s.get("obstacles") or []) if o.get("type") == "solid"]
    by_y = defaultdict(list)
    for o in solids:
        by_y[o["y"]].append(o["x"])
    rows = {y: sorted(xs) for y, xs in by_y.items() if len(xs) >= 2}
    if rows:
        print(f"{s['id']} theme={s.get('theme')}: rows={rows}")
        examples += 1
        if examples >= 15:
            break

print("\n=== LANE MASK CHEATSHEET (assumed bits) ===")
print("bit0=1 lane0, bit1=2 lane1, bit2=4 lane2; mask 7=all, 3=0+1, 6=1+2, 2=mid only")

# Obstacle density
print("\n=== OBS COUNT PER SEGMENT (max/min) ===")
obs_counts = [(s["id"], len(s.get("obstacles") or [])) for s in segments]
obs_counts.sort(key=lambda t: -t[1])
print("top 10:", obs_counts[:10])
print("fortress obs counts:", [(s["id"], len(s.get("obstacles") or [])) for s in fort])
