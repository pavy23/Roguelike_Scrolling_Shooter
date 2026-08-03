# REQ-131 보고서 (GROK / CONTENT)

- 작업일: 2026-08-03
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-129/130 (`9dae5b9`) main 병합 완료
- 결과: **REQ-131 적용 PASS** (스키마 변경 없음)

---

## 1. 요약

스테이지 1~3 테마(스크랩야드·하이브·포트리스)의 **중간보스 이전(early/mid) 세그먼트**에 그라디우스식 편대 스폰을 넣었다.

- Core/스키마 변경 **없음** — `spawns: [{tick, enemyId, y}]` 만으로 동일 기종·짧은 틱 간격·y 패턴 = 편대.
- 대상 세그먼트 **15개**, 편대 **17회** (일부 세그먼트에 2회).
- 편대 기종은 전부 **fireIntervalTicks=0 잡졸** (사격 위협 증가 없음).
- 스테이지 1은 산발 스폰을 편대로 **재편성** 위주 (테마 총 스폰 +10, ≈+5%).

**결정론 해시 변경**: `waves.json` 스폰 스케줄 변경 → 스테이지 조립·스폰 감사 해시 **전부 변동**. GEMINI DeterminismAudit 베이스라인 갱신 필요.

---

## 2. 편대 카탈로그 (세그먼트별)

### 2.1 스크랩야드 (스테이지 1 전반) — 6세그 · 6편대

| 세그먼트 | 형태 | 기종 | 기수 | tick | y 패턴 | 비고 |
|---|---|---|---:|---|---|---|
| `seg_scrap_debris_line` | 종대 | `junk_roller` | 6 | 55..95 dt=8 | y=0.5 고정 | 첫인상 점수 라인 |
| `seg_scrap_pipe_dash` | 사선 | `pipe_rat` | 6 | 50..90 dt=8 | 3→-2 (dy=-1) | 하강 대각 |
| `seg_scrap_zigzag_posts` | V자 | `rust_skimmer` | 7 | 70..118 dt=8 | 0±1.5±3±4.5 | 중앙 선두 |
| `seg_scrap_shard_field` | 종대 | `rust_skimmer` | 6 | 60..100 dt=8 | y=-1 | |
| `seg_scrap_rail_split` | 종대 | `pipe_rat` | 5 | 50..82 dt=8 | y=0 | **mask=2** 중앙 통로만 |
| `seg_scrap_center_breach` | 사선 | `junk_roller` | 5 | 80..116 dt=9 | -2.5→1.5 | d=1-3 |

- `seg_scrap_skimmer_weave` 는 편대 미적용 (풀 다양성 유지, 기대 편대 횟수 조절).
- **기대 편대/스테이지(early 3세그)**: weight 기준 ≈ **2.58** (목표 2~3).

### 2.2 하이브 (스테이지 2 전반·중반 풀) — 5세그 · 6편대

| 세그먼트 | 형태 | 기종 | 기수 | tick | y 패턴 |
|---|---|---|---:|---|---|
| `seg_hive_lancer_rush` | 사선 + 종대 | `lancer_dart` | 8 + 5 | 30..86 / 300..332 dt=8 | 3.5→-3.5 / y=1 |
| `seg_hive_hornet_dive` | V자 | `sting_hornet` | 7 | 40..88 dt=8 | 0±1.25±2.5±3.75 |
| `seg_hive_spore_cloud` | 종대 | `spore_drifter` | 7 | 45..99 dt=9 | y=1.5 |
| `seg_hive_brood_wave` | 사선 | `sting_hornet` | 6 | 300..340 dt=8 | -3→3 (dy=1.2) |
| `seg_hive_tentacle_posts` | V자 | `lancer_dart` | 5 | 150..182 dt=8 | 0±1.5±3 |

- 촉수(`hive_tentacle`)·중형 앵커는 유지. 잡졸 스트림만 편대로 재편.
- late-encroach 세그먼트(막·초크 등)는 **미터치**.
- **기대 편대/스테이지**: mid 풀 weight상 ≈ **3.0**.

### 2.3 포트리스 (스테이지 3 전반·중반 풀) — 4세그 · 5편대

| 세그먼트 | 형태 | 기종 | 기수 | tick | y 패턴 |
|---|---|---|---:|---|---|
| `seg_fortress_interceptor_assault` | 종대 + V자 | `interceptor_rush` | 8 + 5 | 30..86 / 300..332 dt=8 | y=2 / 0±2±4 |
| `seg_fortress_mortar_line` | 사선 | `interceptor_rush` | 7 | 90..138 dt=8 | 4,3,2,0,-1,-3,-4 |
| `seg_fortress_sentry_grid` | V자 | `interceptor_rush` | 7 | 100..148 dt=8 | 0±2±4±6 |
| `seg_fortress_shield_bastion` | 종대 | `interceptor_rush` | 8 | 170..226 dt=8 | y=-1 |

- solid 플랫폼 선반 y(±1.0/±1.5/±2.5/±3.5)를 **편대 y가 피하도록** 설계 (시각적 벽 박힘 완화).
- 포탑·박격·레이저 센트리 스케줄은 유지.
- **기대 편대/스테이지**: ≈ **3.0** (interceptor_assault는 late-encroach 라벨이지만 dMin=2로 early에도 등장 가능·편대 포함).

---

## 3. 스폰 수 전후

| 테마 | 수정 전 | 수정 후 | Δ |
|---|---:|---:|---:|
| scrapyard | 186 | **196** | **+10** (+5.4%) |
| hive | 167 | **175** | **+8** (+4.8%) |
| fortress | 197 | **213** | **+16** (+8.1%) |

세그먼트 단위(발췌):

| 세그먼트 | 전 | 후 |
|---|---:|---:|
| scrap debris / pipe / zigzag / shard / rail / breach | 12/14/14/14/14/14 | 16/18/15/14/15/14 |
| hive lancer / hornet / spore / brood / tentacle | 24/20/20/21/14 | 24/17/23/26/17 |
| fort interceptor / mortar / sentry / shield | 26/20/21/14 | 26/26/25/20 |

`lengthTicks` **미변경**. 모든 대상 세그먼트 `max(spawn.tick) + 40 < lengthTicks` (여유 ≥120틱).

---

## 4. 캡슐(보상) 밀도

드롭 테이블 미변경 (`noDropWeight=13`). 편대 전멸 시 **≥1 캡슐** 확률(독립 가정):

| 기종 (dropWeight) | 기수 | P(≥1) |
|---|---:|---:|
| junk_roller (5) | 6 | **0.86** |
| spore_drifter (4) | 7 | **0.85** |
| rust_skimmer (3) / sting_hornet (3) | 7 | **0.77** |
| pipe_rat (3) | 6 | **0.71** |
| lancer_dart (2) / interceptor×8 | 8 | **0.68** |

→ 전멸 시 캡슐이 **자주** 나오는 밀도. 그라디우스 편대 클리어 보상 맛에 맞춤 (규칙 변경 없이 기수만으로).

---

## 5. 난이도 영향 평가

| 항목 | 평가 |
|---|---|
| 스테이지 1 첫인상 | **위협↑ 없음**. 편대 전원 fire=0 저HP 잡졸. 산발→정렬로 **읽기 쉬워짐**. 총 스폰 +5% 수준. |
| 하이브 | 랜서/호넷/스포어 편대 = 회피 리듬 강화, 사격 압박 증가 없음. lancer_rush는 산발 16기 → 편대 13+꼬리 3으로 **재배치**(총수 동일). |
| 포트리스 | 인터셉터 러시를 종대/V/사선으로 **가독성↑**. 총 +16은 fire=0 고속 잡졸. 포탑·박격 타이밍 유지 → 화력 난이도 동등. |
| 레인/장애물 | scrap `rail_split` 편대 y=0 only (mask=2). fortress 편대 y가 solid 선반 y와 **불일치**. 장애물 좌표·mask 자체 미변경. |
| 세그먼트 길이 | 전부 여유 마진 충족 — 편대 출현 중 세그 종료 없음. |

**종합**: 스테이지 1은 “볼거리 + 점수/캡슐 기회”. 2~3은 기존 잡졸 밀도를 편대 형태로 재구성해 **위협 패턴의 가독성**을 올린 수준. 중간보스·후반 late-encroach 가틀릿은 손대지 않음.

---

## 6. 검증

| 항목 | 결과 |
|---|---|
| JSON 파싱 | PASS |
| 편대 자동 검출 (동일 기종 n≥5, dt≤12) | 15/15 세그먼트, 17 편대 |
| maxTick < lengthTicks | PASS (margin ≥120) |
| mask=2 통로 y 범위 | PASS (`rail_split` \|y\|≤0) |
| solid 선반 y 근접 | PASS (fortress dy=2 / 수동 y 리스트) |
| Core 스키마 신규 필드 | 없음 |

도구 (재현용):

- `Tools/BalanceSim/_req131_apply_formations.py` — 적용
- `Tools/BalanceSim/_req131_verify.py` — 검출·마진·캡슐 기대값
- `Tools/BalanceSim/_req131_analyze.py` / `_req131_dump_spawns.py` — 사전 분석

---

## 7. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | 15 세그먼트 spawns 재편 + intent `REQ131 formation` |
| `Tools/BalanceSim/_req131_*.py` | 분석·적용·검증 스크립트 |
| `Reviews/from-grok/req131-report.md` | 본 보고 |

---

## 8. 후속 / 타 에이전트

- **GEMINI**: DeterminismAudit 베이스라인 갱신 (스폰 스케줄 변경).
- **사람**: 스테이지 1 체감이 “너무 쉬운 점수 농장”이면 기수 5~6으로 하향, 또는 `debris_line`/`pipe_dash` 편대만 유지하고 나머지는 되돌리는 식으로 조절 가능 (스크립트 재실행).
- Core 요청 없음.
