# REQ-135 / REQ-136 보고서 (GROK / CONTENT)

- 작업일: 2026-08-03
- 브랜치/worktree: `content` / `wt-content`
- 결과: **REQ-135 적용 PASS** · **REQ-136 개수 감축 PASS** · **크기 절반은 데이터 필드 없음 → Core/Presentation 요청**

---

## REQ-135 — 날아다니는 작은 적 히트박스 ×1.5

### 1. 판단 기준

Presentation이 스프라이트를 `halfWidth`/`halfHeight`에 맞춰 스케일하므로 **히트박스 = 보이는 크기**.

**포함 조건 (소형 비행 잡졸)**

| 조건 | 값 |
|---|---|
| halfWidth | ≤ 0.8 (수정 전) |
| halfHeight | ≤ 0.8 (수정 전) |
| movement.pattern | `static` 제외 |
| hp | < 50 |
| midBoss | 없음 |

**제외 (의도적)**

| id | 이유 |
|---|---|
| `zako_tank` | hw=1.56 / hp=500 — 이미 포탑·중형보다 큼 (예시 목록에 있었으나 크기 기준으로 제외) |
| `zako_sine_slow` | hw=1.17 / hp=100 — 중형 슬로우 사인 |
| `scrap_tumbler` / `echo_wisp` / `void_moth` 등 | hw≥1.09 · hp≥80 중형 |
| 포탑·레이저 센트리·미니보스·엘리트·보스 | 지시 제외 |

### 2. 대상 12종 (적용 후)

| id | before (hw×hh) | after (×1.5, 1/256 양자화) | hp |
|---|---:|---:|---:|
| `lancer_dart` | 0.546875×0.390625 | **0.8203125×0.5859375** | 6 |
| `rift_blade` | 0.625×0.390625 | **0.9375×0.5859375** | 6 |
| `interceptor_rush` | 0.625×0.46875 | **0.9375×0.703125** | 6 |
| `zako_fast` | 0.625×0.46875 | **0.9375×0.703125** | 6–8 |
| `pipe_rat` | 0.625×0.46875 | **0.9375×0.703125** | 10 |
| `wisp_spark` | 0.625×0.546875 | **0.9375×0.8203125** | 8 |
| `zako_straight` | 0.703125×0.546875 | **1.0546875×0.8203125** | 12 |
| `zako_sine` | 0.703125×0.546875 | **1.0546875×0.8203125** | 14 |
| `rust_skimmer` | 0.703125×0.546875 | **1.0546875×0.8203125** | 12 |
| `sting_hornet` | 0.703125×0.546875 | **1.0546875×0.8203125** | 10 |
| `junk_roller` | 0.78125×0.625 | **1.171875×0.9375** | 14 |
| `spore_drifter` | 0.78125×0.703125 | **1.171875×1.0546875** | 14 |

### 3. 난이도 우려 (적용은 1.5배 유지 — 사람 판단 대기)

1. **접촉 판정 면적 ≈ 2.25배** (선형 1.5²). 스테이지1 주력(`rust_skimmer`/`pipe_rat`/`junk_roller`)이 커져 **맞기 쉬워짐**.
2. 1스테이지 전반은 사격 적 거의 없음(REQ-130/132) → 위협 = **접촉 + 고철**. 잡졸 확대가 체감 난이도 주 요인.
3. 대안(미적용, 참고):
   - **×1.25** (이전 REQ-083 배율과 동일 철학) — 면적 ≈1.56배, 가독성 개선 + 난이도 완만
   - **스테이지1 전용 소형 유지 / 후반만 ×1.5** — 스키마에 테마별 크기 없음 → Core 확장 필요
4. 스폰–장애물 **물리 충돌은 Core에 없음**(장애물은 플레이어만 접촉 데미지). 시각 파묻힘 완화는 REQ-136에서 y 너지 재실행.

### 4. 변경 파일

- `GameData/enemies.json`
- `Tools/BalanceSim/_req135_scale_small_enemies.py`

---

## REQ-136 — 스테이지1 파괴 가능 고철: 수↓ · 크기 ½

### 1. 개수 (스크랩야드 breakable)

| 지표 | 수정 전 (REQ-129 이후) | 수정 후 |
|---|---:|---:|
| **총 breakable** | **77** | **50** |
| 감축률 | — | **35.1%** (목표 30–40%) |
| cover (`blocksEnemyBullets`) | 52 obs / 13 segs | **40** / **13** (≥20/6 게이트 OK) |
| late mid-lane cover posts | 24 | **18** (≥8 게이트 OK) |
| mid-lane (`\|y\|≤4`) | 74 | 48 |
| gap min / med / max | 0.00 / 1.50 / 4.00 | 0.00 / **3.50** / 4.50 |
| gaps ≤ 1.0 | 13/64 | 11/37 (의도 벽 gap=0만) |
| 벽 보유 세그 | 6 | **6** (의도 벽 유지) |
| 벽 hist | {2:11, 3:1} | {2:9, 3:1} |
| x-span med | 8.00 | 7.50 |

### 2. 세그먼트별 감축

| 세그먼트 | before | after | 비고 |
|---|---:|---:|---|
| `debris_line` | 5 | **3** | 전반 티칭, 진행선 cover 3 |
| `pipe_dash` | 6 | **3** | |
| `skimmer_weave` | 6 | **3** | mask=2 중앙 통로 유지 |
| `zigzag_posts` | 6 | **3** | |
| `shard_field` | 8 | **4** | 전반 최다 밀도 완화 |
| `center_breach` | 7 | **5** | dig-wall **3@x=14** 유지 + scatter 2 |
| **`rail_split`** | 6 | **6** | **의도 레일 유지** |
| **`speed_spike`** | 3 | **3** | **의도 게이트 유지** |
| `junk_corridor` | 5 | **3** | 후반 cover-line (전부 cover) |
| `tumbler_pack` | 6 | **4** | pair@15.5 + scatter |
| `rust_gauntlet` | 7 | **5** | 엄폐 기둥 11/15/19 |
| `clean_kill_corridor` | 7 | **5** | gauntlet 미러 |
| `clean_kill_junk` | 5 | **3** | junk 미러 |

### 3. 분산 유지 (REQ-129)

- 비의도 세그: x를 [10.5–19.5] 구간에 **균등 재배치**, 인접 gap 중앙값 **3.5** (이전 1.5)
- 의도 벽: `rail_split` 3쌍 · `speed_spike` 게이트 · `center_breach` dig-wall · gauntlet/corridor 기둥 — same-x 유지
- 중형 적 spawn y 일치 breakable → `_req129_nudge_y` 재실행 (21 nudges, 의도 벽 세그 제외)

### 4. 튜토리얼 소품 (REQ-103 고철 방패) 보존

| 검사 | 결과 |
|---|---|
| 전반 세그 cover 존재 | debris/pipe/skimmer/zigzag/shard/rail 전부 cover ≥2 |
| 후반 mid-lane cover | junk/tumbler/gauntlet/clean_kill **전부** mid-lane cover 유지 |
| 게이트 | cover obs=40 segs=13 · late_mid=18 — **PASS** |

### 5. 크기 절반 — 데이터에 필드 없음

Obstacle AABB는 Core 전역 상수:

```
BattleSimConfig.ObstacleHalfWidth  = SubUnitsPerWorldUnit / 2  // 0.5 world u
BattleSimConfig.ObstacleHalfHeight = SubUnitsPerWorldUnit / 2  // 0.5 world u
```

- `waves.json` obstacle DTO: `type, x, y, hp, blocksEnemyBullets, regenDelayTicks` 만 — **halfWidth/Height 없음**
- 파서도 장애물별 크기를 읽지 않음
- **GROK 단독으로 크기 절반 불가** → CODEX(Core 상수 또는 per-obstacle 필드) + CLAUDE(스프라이트 스케일) 요청

권장: `ObstacleHalfWidth/Height` 를 **0.25** (절반)로 낮추거나, scrapyard breakable만 데이터 필드로 분리.

### 6. 결정론

- `enemies.json` half extents 변경 → 충돌 AABB·스프라이트 스케일 입력 변동
- `waves.json` obstacle 개수·좌표 변경 → 스테이지 조립·장애물 스케줄·감사 해시 **전부 변동**
- **DeterminismAudit 베이스라인 갱신 필요** (GEMINI)

### 7. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/enemies.json` | 소형 비행 12종 half ×1.5 |
| `GameData/waves.json` | scrapyard breakable 77→50 + scatter + y-nudge · intent `REQ136` |
| `Tools/BalanceSim/_req135_scale_small_enemies.py` | 적용 스크립트 |
| `Tools/BalanceSim/_req136_thin_scrap_breakables.py` | 감축·재분산 스크립트 |
| `Reviews/from-grok/req135-136-report.md` | 본 보고서 |
