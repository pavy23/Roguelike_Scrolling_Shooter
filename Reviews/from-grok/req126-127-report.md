# REQ-126 / REQ-127 — GROK content report (2026-08-03)

## REQ-126 — Fortress solid platforms

### Pre-count (hypothesis check)

| 구분 | 값 |
|---|---|
| fortress 세그먼트 | 10 (`theme=fortress`) |
| fortress solid **합계** | **30** (평균 3.0/seg) |
| non-fortress solid 평균 | 1.98/seg |
| 전체 solid / breakable / laser | 129 / 181 / 26 |

**가설 "포트리스에 solid가 거의 없다"는 개수로는 거짓.**  
다만 기존 solid Y는 전부 천장/바닥 장식대(`|y|≈5.5~6.5`)뿐이라 **중간 고도 발판이 0개**였다. 적이 허공에 떠 보이는 원인이 이쪽.

### Post-count

| 구분 | 값 |
|---|---|
| fortress solid 합계 | **90** (edge 30 + mid-band 60) |
| 전체 solid | **189** (+60) |
| breakable / laser | 181 / 26 (변경 없음) |
| 세그먼트당 최대 obstacles | 15 (MaxObstacles=32 여유) |

### 배치 규칙

각 fortress 세그먼트에 **상·하 데크 2개 × solid 3개(가로 연속)** = +6:

- **Upper deck target** `y = clamp(mean(spawns in (1,5)) − 1.75, 0.75..2.0)` 후 0.25 단위 nudge  
  (중/대형 적 중심이 solid 블록 안에 들어가지 않게)
- **Lower deck target** `y = clamp(mean(spawns in (−5,−1)) − 1.75, −3.5..−0.75)` 동일 nudge
- **X**: 상 데크 `9,10,11` / 하 데크 `13,14,15` (1.0 간격 = 1×1 블록 맞댐)
- `hp: 0`, `type: "solid"` — 기존 `obstacle_armor_block` 배선 그대로
- 적–장애물 충돌 없음(플레이어만 `ObstacleContactDamage`). 매립 검증은 **halfH≥0.8 중형 이상 중심 겹침**만 금지. 소형 interceptor 스침은 허용.

| 세그먼트 | solid 전→후 | platform y | coords (요약) |
|---|---|---|---|
| sentry_grid | 2→8 | 1.0 / −3.5 | (9–11,1.0)+(13–15,−3.5) |
| interceptor_assault | 2→8 | 1.5 / −3.5 | 동일 패턴 |
| mortar_line | 2→8 | 1.0 / −2.5 | … |
| turret_cross | 3→9 | 0.75 / −4.0 | … |
| drone_lattice | 4→10 | 1.0 / −3.5 | … |
| armored_gate | 4→10 | 0.75 / −4.0 | … |
| shield_bastion | 2→8 | 1.25 / −3.5 | … |
| crossfire_alley | 4→10 | 1.0 / −3.0 | … |
| clean_kill_hull | 3→9 | 0.5 / −4.0 | … |
| clean_kill_lattice | 4→10 | 0.5 / −3.5 | … |

### 통로 폭 검증

- solid half = 0.5u → 선반 폭 3.0u
- x-band `[8,21]` 기준 **free_frac ≈ 0.75** (선반 y마다)
- max contiguous run = 3.0 ≤ 4.0 (벽 아님)
- BalanceSim `corridor=ok` 전 fortress 세그먼트 PASS
- 세로로 상·하 데크 사이 간격 ≥1.5u 유지 → 중앙 비행 통로 확보

---

## REQ-127 — Stage interval variance

### 변경

| 항목 | Before | After |
|---|---|---|
| `closingSegmentsPerStage` | 7 | **5** |
| `segmentsPerStage` | 3 | 3 (유지) |
| `lengthTicks` 범위 | 280~970 (비 3.46) | **400~900** (비 **2.25**) |
| mean / stdev | 790.7 / 135.2 | 789.5 / 115.3 |

**length 클램프 규칙**
- 일반 세그먼트: `[600, 900]`
- `seg_scrap_speed_spike` / `seg_core_speed_spike`: REQ-103b 때문에 **400** (short scramble 유지; 600으로 올리면 게이트 FAIL)
- 970→900 클램프 후 valley gap 붕괴 3건: 마지막 스폰 tick `820→780` (gap 120 복구)
  - sentry_grid / armored_gate: `zako_tank`
  - core_guardian_wall: `guardian_sphere`

### 기대 소요 시간 (60 tick/s, 복원추출 8000회)

| | early (n=3) | late (n=closing) | late/early |
|---|---|---|---|
| **Before** | mean **39.5s** stdev 3.9 · range 14~47.5 · run比 3.39 | n=7 mean **92.2s** stdev 6.0 · range 59~107 · run比 1.82 | **2.33** |
| **After** | mean **39.5s** stdev 3.3 · range 20~45 · run比 2.25 | n=5 mean **65.7s** stdev 4.3 · range 47.5~75 · run比 1.58 | **1.66** |

목표 1:1.3~1.5 대비 late/early **1.66** — 5/3 개수 비 한계. 4로 내리면 ~1.33이지만 요청 문구 "5 근처"를 채택.  
잔여 편차는 CODEX `SegmentStageGenerator` 목표시간 채우기로 마감하는 것이 맞음.

### 결정론 / 생성기 중복

- **결정론 해시 변경**: `lengthTicks`, `closingSegmentsPerStage`, obstacle 좌표, (3세그) 스폰 tick이 바뀌므로 **기존 리플레이·시드 해시·저장 호환 깨질 수 있음.**
- **CODEX 겹침**: 생성기가 "목표 시간으로 세그먼트 개수를 채우기"로 바뀌면 이번 데이터 클램프와 **이중 조정이 될 수 있음**. 생성기 착륙 후 length 상·하한 또는 closing 수를 한 쪽으로 재조정할 것.

---

## 검증

```
dotnet run --project Tools/BalanceSim/VerifyThemeAssembly.csproj -c Release
→ PASS: BalanceSim all checks green.
```

(JSON UTF-8 no BOM, indent 2, camelCase 유지)

## 산출물

- `GameData/waves.json`
- `Tools/BalanceSim/_req126_127_analyze.py`
- `Tools/BalanceSim/_req126_127_apply.py`
- `Tools/BalanceSim/_req127_fix_gates.py`
