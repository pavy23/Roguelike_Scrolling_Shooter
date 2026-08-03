# REQ-132 보고서 (GROK / CONTENT)

- 작업일: 2026-08-03
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-131 (`6a70139`) main 병합 · 사람 승인 **안 A** 채택
- 결과: **REQ-132 적용 PASS** (스키마 버전 변경 없음)

---

## 1. 적용 값

### 1.1 `GameData/enemies.json` (안 A 그대로)

| 적 | 변경 전 | 변경 후 | 의미 (60Hz) |
|---|---:|---:|---|
| `junk_roller` | 0 | **180** | 3.0s 주기 조준 1-way |
| `scrap_tumbler` | 0 | **150** | 2.5s 주기 조준 1-way |
| `rust_skimmer` / `pipe_rat` 외 잡졸 | 0 | **0 유지** | — |

- 탄 속도·데미지·ways **미변경** (발사 간격만).
- Core 발화 조건: `age % fireIntervalTicks == 0` (age는 스폰 다음 틱부터 1…) → **첫 탄 = 스폰 후 interval 틱**.
- `schemaVersion` **3 유지** — 기존 필드 값 변경만이며 스키마 확장 아님.

### 1.2 편대 밀도 과다 조정 (`GameData/waves.json`)

안 A를 그대로 전 기종에 적용하면 REQ-131 편대가 **동시 다발 탄벽**이 된다 (아래 §2).  
상위 원칙 **「1면 첫인상 난이도는 올리지 않는다」**에 따라 **발사 간격은 안 A 유지**, 편대 구성원만 비사격 기종으로 교체했다.

| 세그먼트 | 편대 (REQ-131) | 조정 |
|---|---|---|
| `seg_scrap_debris_line` | `junk_roller`×6 종대 (tick 55..95 dt=8) | → **`rust_skimmer`×6** (fire=0). 산발 `junk_roller`×3 유지 → 저빈도 사격 |
| `seg_scrap_center_breach` | `junk_roller`×5 사선 (tick 80..116 dt=9) | → **`pipe_rat`×5** (fire=0). `scrap_tumbler`×2 유지 → 커버 티칭용 사격 |
| `seg_scrap_tumbler_pack` | ST×10 팩 (dMin=2, mid) | **미변경**. peak1s=3으로 mid 허용 범위 |

---

## 2. 편대 포함 탄 밀도 계산

### 2.0 가정

- 틱 60Hz. 조준 1-way (Core `SpawnEnemyAimedBullet`).
- 체공: spawnX≈21 → despawn≈−22 (거리 43u), scroll 5 + move →  
  `junk_roller` life ≈ **303t** (≈5.1s), `scrap_tumbler` ≈ **294t** (≈4.9s).
- 체공 중 발사 횟수: JR 180 → **1발/기**, ST 150 → **1발/기** (2발 전에 despawn).
- `peak1s` = 임의의 60틱 창에서 발사 횟수 최대.

### 2.1 안 A만 적용·편대 무조정 (기각)

| 세그먼트 | JR | ST | total shots | **peak1s** | 판정 |
|---|---:|---:|---:|---:|---|
| `seg_scrap_debris_line` | 9 | 0 | 9 | **6** | 편대 일제(±40t) = late `rust_gauntlet` 터렛 수준 |
| `seg_scrap_center_breach` | 5 | 2 | 7 | **5** | 사선 5연발 탄벽 |
| `seg_scrap_tumbler_pack` | 2 | 10 | 12 | 3 | mid 허용 후보 |
| early 기타 (zigzag/shard/rail) | 2–3 | 2 | 4–5 | 2 | OK |

`debris_line` 편대 첫 탄 시각(스폰+180):  
`235,243,251,259,267,275` → **0.67초 안에 6발**.  
1면 오프닝 weight=12 세그먼트에서 late 가틀릿급 탄밀 → **첫인상 난이도 상승 = 상위 원칙 위반**.

### 2.2 조정 후 (채택)

| 세그먼트 | JR | ST | 편대 기종 | total shots | **peak1s** |
|---|---:|---:|---|---:|---:|
| `seg_scrap_debris_line` | **3** | 0 | skimmer×6 | **3** | **1** |
| `seg_scrap_center_breach` | 0 | **2** | pipe_rat×5 | **2** | **1** |
| `seg_scrap_tumbler_pack` | 2 | 10 | (팩 유지) | 12 | 3 |
| `seg_scrap_zigzag_posts` | 2 | 2 | — | 4 | 2 |
| `seg_scrap_shard_field` | 3 | 2 | — | 5 | 2 |
| `seg_scrap_rail_split` | 2 | 2 | — | 4 | 2 |
| `seg_scrap_skimmer_weave` | 5 | 0 | — | 5 | 1 |

- early scrap (dMax≤2) **weight 평균 사격 기수 ≈ 3.1/세그**.
- 산발 사격 간격 2.5–3.0s · peak1s ≤ 2 → 그레이즈·커버 학습용 **간헐 1–2발**.
- mid `tumbler_pack` peak1s=3 ≈ 터렛 1–2기 수준. 전반 첫인상 풀(dMax≤2) 밖.

**조정 방식 선택 근거**: 간격을 더 늘리면 산발 JR/ST의 그레이즈 기회도 같이 희석된다.  
편대만 비사격 기종으로 바꾸면 **안 A 수치·산발 사격 밀도**를 지키면서 탄벽만 제거한다.

---

## 3. 타 테마 영향 범위

JR/ST 스폰이 있는 세그먼트 전수:

| 테마 | 세그 수 | JR 스폰 | ST 스폰 | 비고 |
|---|---:|---:|---:|---|
| **scrapyard** | 12 | 35 (−6 편대 교체 후) | 37 | 주 영향. early+mid+late |
| **fortress** | 1 | 0 | 1 | `seg_fortress_sentry_grid` ST×1 → +1발/출현 |
| **hive** | 0 | 0 | 0 | **영향 없음** |
| (theme 미표기 legacy) | 3 | 6 | 2 | `sine_pair`/`sine_rush`/`mixed_mid` — 풀 잔존 시 peak1s≤2 |

→ 스테이지 2(하이브) 무영향. 스테이지 3은 ST 1기 수준.  
탄 밀도 상승의 실질 범위는 **스테이지 1 스크랩야드**.

---

## 4. 결정론 영향

| 항목 | 영향 |
|---|---|
| `enemies.json` fireInterval 0→양수 | 적 AI 발화 분기 활성 → **전투 이벤트·상태 해시 변동** |
| `waves.json` 편대 enemyId 교체 | 스폰 스케줄/기종 해시 변동 → **스테이지 조립·스폰 감사 해시 변동** |
| schemaVersion | **불변** (enemies 3 / waves 2) |
| GEMINI | DeterminismAudit 베이스라인 **재취득 필요** |

RNG 스트림 자체(시드 분기)는 변경하지 않음. 같은 시드라도 **사격 유무·스폰 기종**이 달라 상태 해시는 반드시 갈린다.

---

## 5. 목표 달성 체크

| 목표 | 결과 |
|---|---|
| 전반 그레이즈 기회 0 → 간헐 | early scrap 사격 기 등장 (weight-avg ≈3.1) |
| REQ-103 고철이 적탄 차단 티칭 | center_breach 등 cover 플래그 + ST/JR 사격 동시 존재 |
| 1면 첫인상 난이도 미상승 | 편대 탄벽 제거, early peak1s ≤ 2 |
| 탄 속도·데미지 범위 외 | 미터치 |

---

## 6. 검증

| 항목 | 결과 |
|---|---|
| JSON parse | enemies + waves OK |
| fireInterval 값 | JR=180, ST=150, skimmer/rat=0 |
| 편대 구성 | debris=skimmer×6+JR×3, breach=pipe_rat×5+ST×2 |
| 밀도 스크립트 | `Tools/BalanceSim/_req132_density.py`, `_req132_analyze_early.py` |

---

## 7. 타 에이전트 후속

### CLAUDE
1. [ ] Resources `GameData/enemies.json` · `waves.json` 동기화
2. [ ] (선택) stage1 early 그레이즈·cover 체감 캡처

### GEMINI
1. [ ] DeterminismAudit 베이스라인 갱신 (fire + spawn 기종)
2. [ ] early scrap peak 탄밀도 교차 (이론 peak1s≤2)
