# REQ-129 / REQ-130 보고서 (GROK / CONTENT)

- 작업일: 2026-08-03
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-126/127 (`02d3eab`) main 병합 완료
- 결과: **REQ-129 적용 PASS** · **REQ-130 조사만 (수치 미적용, §7)**

---

## REQ-129 — 스크랩야드 breakable X 분산

### 1. 사전 집계 (수정 전)

대상: `theme=scrapyard` 세그먼트 13개 · breakable **77**개.

| 지표 | 수정 전 |
|---|---|
| 인접 x-gap min / med / max | **0.00 / 1.50 / 4.00** |
| gaps ≤ 1.0 | **27 / 64** (42%) |
| x-span med | 7.50 |
| 세로 벽(x±1, n≥2) 보유 세그 | **11 / 13** |
| 벽 크기 히스토그램 | {2: 21, **3: 1**, **5: 1**} |

**최악 사례**

| 세그먼트 | 문제 |
|---|---|
| `seg_scrap_center_breach` | breakable **5개 전부 x=14.5** 세로 벽 + 2@17.5 · span=**3.0** |
| `seg_scrap_zigzag_posts` | x=11–13 구간에 3개 클러스터 (wall n=3) |
| `seg_scrap_debris_line` 등 전반 | 인접 gap 0.5–1.0 다수 → “한 덩어리 + 긴 공백” 체감 |
| `seg_scrap_rust_gauntlet` / `rail_split` | 같은 x 쌍·삼열 — **의도된 엄폐/레일** (REQ-103b) |

### 2. 타 테마 (참고, 이번 미수정)

| theme | br | gap med | walls_seg | 비고 |
|---|---:|---:|---:|---|
| scrapyard | 77 | 1.50 | 11 | **이번 수정 대상** (밀집 최심) |
| hive | 22 | 3.00 | 2 | 재생 벽 시그니처 위주, 밀도 낮음 |
| fortress | 23 | 4.00 | 3 | solid 플랫폼이 주력 (REQ-126) |
| nebula | 23 | 4.00 | 0 | gap 양호 |
| core | 30 | 2.75 | 1 | 양호 |

스크랩야드만 breakable 절대 수·밀집도가 두드러짐. 다른 테마는 동일 증상 경미.

### 3. 수정 방침

1. **의도 벽 유지** (전부 제거 금지 — 사람 지시)
   - `rail_split` 상하 레일 쌍 (mask=2 중앙 통로 훈련)
   - `rust_gauntlet` / `clean_kill_corridor` 세로 엄폐 기둥 (REQ-103b cover-line)
   - `speed_spike` 짧은 게이트 쌍
   - `center_breach` 는 **5연속 벽 → 3연속 dig-wall @x=14** 로 축소 + 나머지 4개 scatter
2. **전반 티칭 세그** (debris / pipe / skimmer / zigzag / shard / junk): x를 구간 **[10.5–19.5]** 에 균등 배치, 인접 gap **≥ 1.25~2.0**, same-x 벽 제거
3. `tumbler_pack`: 엄폐용 **1쌍만** @15.5 유지, 나머지 scatter
4. 중형 적(`scrap_tumbler` 등) spawn y와 완전 일치하는 breakable y 는 0.75u 오프셋 (시각 파묻힘). 의도 벽·터렛 솔리드 발판은 유지
5. `blocksEnemyBullets` 커버 게이트 유지 (obs≥20 · segs≥6)

### 4. 사후 집계 (수정 후)

| 지표 | 수정 전 | 수정 후 |
|---|---|---|
| gap min / med / max | 0.00 / 1.50 / 4.00 | 0.00 / **1.50** / 4.00 |
| gaps ≤ 1.0 | **27 / 64** | **13 / 64** (의도 벽의 gap=0만 남음) |
| x-span med | 7.50 | **8.00** |
| 벽 보유 세그 | **11** | **6** |
| 벽 hist | {2:21, 3:1, **5:1**} | {2:11, **3:1**} (5-stack 제거) |
| cover | — | obs=**52** segs=**13** (게이트 통과) |

의도 벽 6세그 (`tumbler` 1쌍 · `center_breach` 3 · `gauntlet`/`clean_kill_corridor` 각 3쌍 · `rail_split` 3쌍 · `speed_spike` 1쌍)만 same-x 유지.

### 5. 검증

- 적-블록 **물리 충돌 없음** (Core: 장애물은 플레이어만 접촉 데미지 — REQ-126 노트 동일). 시각 파묻힘은 y 오프셋으로 완화.
- `traversableLaneMasks` 미변경. mask=2 세그(`skimmer_weave`, `rail_split`) 중앙 통로 유지.
- cover 게이트: scrap cover segs/obs 하한 충족.
- **결정론 해시 변경**: `waves.json` 장애물 좌표 변경 → 스테이지 조립·장애물 스케줄·감사 해시 **전부 변동**. GEMINI DeterminismAudit 베이스라인 갱신 필요.

### 6. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | scrapyard breakable x(/y) 재배치 · intent `REQ129 scatter-x` |
| `Tools/BalanceSim/_req129_analyze_breakables.py` | 분포 집계 |
| `Tools/BalanceSim/_req129_apply_scatter.py` | 재배치 적용 |
| `Tools/BalanceSim/_req129_nudge_y.py` | 중형 적 y 이격 |
| `Tools/BalanceSim/_req129_130_report_stats.py` | 사후 통계 |
| `Reviews/from-grok/req129-130-report.md` | 본 보고서 |

---

## REQ-130 — 스테이지1 잡졸 무발사: 의도 여부 (조사 only)

### 1. 데이터 사실

`enemies.json` 34종 중 **17종 `fireIntervalTicks: 0`** (탄 발사 없음).  
레이저 전용(`prism_beamer`, `laser_sentry`)도 탄 interval=0.

**스크랩야드 스폰 집계**

| 적 | fireInterval | scrap 스폰 수 |
|---|---:|---:|
| rust_skimmer | 0 | 51 |
| pipe_rat | 0 | 38 |
| scrap_tumbler | 0 | 37 |
| junk_roller | 0 | 36 |
| zako_tank | 0 | 6 |
| **turret_ground** | **90** | 13 |
| **turret_ceiling** | **90** | 3 |
| **elite_sine** | **120** | 1 |
| prism_beamer | 0 (laser) | 1 |

- **early scrap** (`difficultyMax≤2`): 스폰 68 · **사격 0**
- **late scrap** (`difficultyMin≥2`): 스폰 89 · 사격 **17** (터렛·엘리트만)

→ 사람이 본 “스테이지1 졸개가 탄을 안 쏜다”는 **데이터와 일치**. 전반은 완전 무탄, 후반도 터렛 위주.

### 2. 의도 여부 판정

**판정: 초반 난이도·역할 분담으로서의 의도(소프트 설계)에 가깝고, 단순 누락 버그는 아니다.  
다만 점수/그레이즈 루프와의 교차 설계는 비어 있어, 보완 후보(§7 사람 확정)다.**

#### 의도 쪽 근거

1. **REQ-060** (`Reviews/from-grok/req060-stage1-difficulty-2026-07-30.md`): stage1을 관대·튜토리얼 밴드로 두고 HP·중간보스만 깎음. 잔해는 “막힘보다 파밍”. 잡졸 탄막 추가는 없었음.
2. **역할 분담 패턴이 일관됨**: 전 테마 잡졸(zako_*, skimmer, dart, hornet, roller…)이 interval=0, **포탑·중형·엘리트·미드보스만 사격**. stage1만의 실수가 아니라 전역 fodder 규칙.
3. **REQ-055/103a**: stage1 solid 금지·breakable 파밍/엄폐 티칭. 위협은 **접촉 + 지형 + (후반) 터렛**.
4. **테스터 실측 일치**: `Reviews/from-tester/build22-gimmick-axes-2026-08-02.md` — “전반 세그먼트 전용 적은 전부 `fireIntervalTicks: 0` … 실제로 적탄을 쏘는 건 Late 터렛류뿐”을 **정상 상호작용**으로 기술.

#### 긴장/갭 (의도 완전 방어는 어려움)

1. **REQ-103 St1 시그니처** (`req103-core-requests.md`): 고철이 **적탄을 차단** → “쏘면 엄폐물이 사라진다” 튜토리얼.  
   그런데 **early scrap 사격 스폰=0** 이라 전반 cover 플래그는 탄 없이 존재. 후반 터렛 구간에서야 성립.
2. **점수 시스템** (`scoring.json`): `multiplierDecayTicks: 300` (5초), 그레이즈가 배율 유지 수단.  
   잡졸 무탄 + 터렛 희소 → **그레이즈 가뭄** → 배율 5초마다 하락. 사람 보고 “보스전 배율 유지 안 됨”과 정합 (보스는 쏘지만 진입 전 배율이 이미 1).

### 3. 제안 수치 (**적용 금지** — AGENTS.md §7)

목표: early에도 **느린 탄 1~2발**로 그레이즈·cover 학습 창을 열되, stage1 CLEAR 게이트(접촉 중심)를 깨지 않기.

#### 안 A — 스크랩 잡졸 2종만 저빈도 (추천)

| 적 | 현재 | 제안 fireIntervalTicks | 비고 |
|---|---:|---:|---|
| `junk_roller` | 0 | **180** (3.0s) | zigzag · 체공 길어 그레이즈 창 확보 |
| `scrap_tumbler` | 0 | **150** (2.5s) | 중형 느낌 · 후반 팩에도 자연 |
| `rust_skimmer` | 0 | **0 유지** | dash 짧아 탄 타이밍 불안정 |
| `pipe_rat` | 0 | **0 유지** | 최약 접촉 졸개 |

- 탄 스펙(Core 기본 조준 1-way 가정): bulletSpeed **5.5–6.5** (터렛보다 느림), ways=1.
- 추정 early scrap 사격 스폰: roller+tumbler 비중 ≈ 전체 early의 ~40% → 그레이즈 기회 **0 → 간헐**.
- 초당 탄 밀도 상한(동시 3기 사격 가정): 3 × 60/150 ≈ **1.2발/s** — 터렛 90틱(0.67발/s/기)보다 낮게.

#### 안 B — 전역 zako 일부만 (영향 범위 큼)

| 적 | 제안 interval | 이유 |
|---|---:|---|
| `zako_sine` | 160 | 사인 궤적 + 느린 탄 = 교육용 그레이즈 |
| `zako_sine_slow` | 140 | 중형 슬로우 |
| `zako_straight` / `zako_fast` / `zako_tank` | 0 유지 | 직선·돌진·탱크는 접촉 정체성 |

stage2+ 공용 풀이라 **전 스테이지 탄밀도 상승** — stage1 전용 패치가 필요하면 안 A가 안전.

#### 안 C — 데이터 대신 배치 (enemies 불변)

- early scrap 세그 1–2개에 `turret_ground` 1기 추가 (interval 90 유지).
- 장점: 적 정의 불변. 단점: 터렛 HP 140이 early에 무거울 수 있음 → HP 80 터렛 변형이 아니면 부담.

#### 권장

1. **1차**: 안 A (`junk_roller` 180, `scrap_tumbler` 150)만 사람 플레이 확인.
2. 그레이즈 체감 부족 시 `zako_sine` 160 추가 (안 B 일부).
3. cover 티칭 강화가 목표면 early 1세그에 저HP 터렛(안 C 변형) 병행.

**적용은 사람 확정 후.** 지금 커밋에는 `enemies.json` 변경 없음.

### 4. 점수 교차 메모 (참고)

| 항목 | 값 |
|---|---|
| grazeScore | 10 |
| grazeGaugeCharge | 3 |
| multiplierDecayTicks | 300 (5.0s) |
| multiplier steps | 30/50/80/130/200 gauge |

보스 진입 전 late scrap 터렛 17스폰만으로는 배율 스택이 얇음. 잡졸 저빈도 사격은 **난이도보다 점수 표현** 쪽 효과가 큼.

---

## 결정론 / 후속

| 대상 | 영향 |
|---|---|
| DeterminismAudit 해시 | **변경됨** (REQ-129 좌표) — 베이스라인 재취득 필요 |
| enemies.json | 변경 없음 (REQ-130 미적용) |
| CLAUDE | Resources `GameData/waves.json` 동기화 |
| GEMINI | 해시 베이스라인 · stage1 고철 분산 시각 확인 |
| 사람 | REQ-130 안 A/B/C 채택 여부 (§7) |
