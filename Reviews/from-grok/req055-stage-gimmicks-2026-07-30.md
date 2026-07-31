# REQ-055 스테이지 기믹 데이터 채움 (GROK / 2026-07-30)

한 문장 요약: **스테이지마다 “무엇이 다른가”가 데이터로 고정**되었다. 배경색이 아니라 플레이 규칙을 바꾼다.

| 스테이지 | 한 문장 |
|---|---|
| **스크랩야드** | 떠다니는 **파괴 가능 잔해**로 엄폐하거나 치운다. |
| **바이오 하이브** | **벽 촉수**(Static)와 **좁아지는 통로**로 위치를 고른다. |
| **포트리스** | **포탑 벽 + 레이저 게이트** 타이밍을 탄다. |
| **네뷸라** | **시야 제한** 아래 **드리프트**를 기억·보정한다. |
| **코어** | 앞 기믹 혼합 + **하드 제한 시간(150s)** 으로 종합한다. |

---

## Theme-wide `gimmicks[]`

| theme | visionObscured | timeLimitTicks | 의미 |
|---|---|---|---|
| scrapyard | false | 0 | 잔해만 (세그먼트 obstacles) |
| hive | false | 0 | 촉수 + corridor |
| fortress | false | 0 | laserEmitter 게이트 |
| nebula | **true** | 0 | 시야 제한(Presentation) + drift |
| core | false | **9000** (150s) | 혼합 + 즉사 타임리밋 |

---

## 세그먼트별 배치표

체류 시간 = `lengthTicks / 60` (세그먼트 고정 길이, @60 tps).

### 스크랩야드 — 파괴 가능 잔해 (Breakable)

| 세그먼트 | 체류 | solid / breakable | 요구 플레이 |
|---|---:|---|---|
| `seg_scrap_debris_line` | 10.0s | 0 / **5** (HP 25–35) | 엄폐·치우기 입문 |
| `seg_scrap_pipe_dash` | 10.0s | 0 / **6** (HP 22–34) | 대시 레인 사이 엄폐 섬 |
| `seg_scrap_skimmer_weave` | 11.0s | 0 / **6** (HP 26–40) | 위빙 경로 강제 |
| `seg_scrap_junk_corridor` | 12.0s | 2 / **5** (HP 35–45) | 프레임 + 고철 통로 |
| `seg_scrap_tumbler_pack` | 12.0s | 2 / **6** (HP 40–55) | 텀블러 앞 파쇄 엄폐 |
| `seg_scrap_rust_gauntlet` | 13.0s | 3 / **7** (HP 42–60) | 후반 밀도 최대 |

- d1 가능 세그먼트는 **breakable only** (solid 금지) — 튜토리얼 벽 차단 방지.
- 잔해 Y는 `|y| ≥ 1.5` (중앙 기본 레인 개방; 엄폐는 상하 오프셋).
- 한 화면 목표 4–8개, HP 20–60 가이드 준수.

### 바이오 하이브 — 촉수 + 통로

| 세그먼트 | 체류 | 촉수 spawn | corridor | 요구 플레이 |
|---|---:|---:|---|---|
| `seg_hive_spore_cloud` | 12.0s | **3** | — | 상하 고정 위협 사이로 포자 처리 |
| `seg_hive_lancer_rush` | 11.0s | **2** | — | 랜서 러시 레인 경계 (통로 없음 — 과부하 방지) |
| `seg_hive_brood_wave` | 12.0s | **3** | — | 브루드 사이 앵커 |
| `seg_hive_hornet_dive` | 11.0s | **2** | — | 다이브 회피 축 |
| `seg_hive_organic_pulse` | 13.0s | **3** | **18→10 u** (start ±9 → end ±5) | 예고 후 완만 수축 |
| `seg_hive_nest_choke` | 13.0s | **2** | **17→7.5 u** (start ±8.5 → end ±3.75) | 네스트 초크, 위치 강제 |

- 신규 적 `hive_tentacle`: Static, HP 160, half 0.625×1.25, dropWeight 7.
- 통로 최소 폭 7.5u ≫ 기체 hitbox 0.75u; 스피드업 4스택 21.5 u/s 기준 세로 횡단 ~0.35s.
- 중형·랜서 구간에는 통로를 넣지 않음 (회피 공간 보존).
- hive obstacles/세그 ≤ 4 (게이트 max 5).

### 포트리스 — 레이저 게이트

| 세그먼트 | 체류 | solid / break / **laser** | 주기(t) | 요구 플레이 |
|---|---:|---|---|---|
| `seg_fortress_sentry_grid` | 15.0s | 2 / 2 / **2** | 150 / 180 | 상하 비동기 타이밍 |
| `seg_fortress_interceptor_assault` | 13.0s | 2 / 3 / **1** | 120 | 상단 게이트 + 인터셉터 |
| `seg_fortress_mortar_line` | 13.0s | 2 / 2 / **2** | 150 / 180 | 박격 + 이중 빔 |
| `seg_fortress_turret_cross` | 14.0s | 3 / 2 / **2** | 120 / 150 | 포탑 교차 |
| `seg_fortress_drone_lattice` | 14.0s | 4 / 2 / **3** | 120 / 150 / 180 | 3중 격자 퍼즐 |
| `seg_fortress_armored_gate` | 15.0s | 4 / 2 / **3** | 150 / 160 / 180 | 보스 전 최종 게이트 |

공통 레이저 프로필:
- telegraph 30–42 t (0.5–0.7s) → 예고 확보
- damaging ≈ firing+sustain ~25–36 t
- cycle 120–180 t → 열린 창 ≥ ~1s
- 빔 `|y| ≥ 2.5` (중앙 기본 레인 개방; 플레이어가 타이밍으로 상하 통과)
- 세그먼트당 레이저 ≤ 3 ≪ MaxLasers 8

### 네뷸라 — 시야 + 드리프트

| 세그먼트 | 체류 | drift (x, y) u/s | 합성 | 방향 의도 |
|---|---:|---|---:|---|
| `seg_nebula_wisp_storm` | 13.0s | (+0.45, +0.20) | 0.49 | 입문 약 우상 |
| `seg_nebula_wisp_ribbon` | 12.0s | (−0.35, +0.40) | 0.53 | X 반전 |
| `seg_nebula_echo_ribbon` | 12.0s | (+0.15, −0.55) | 0.57 | 하방 편향 |
| `seg_nebula_void_moth_swarm` | 12.0s | (−0.55, −0.25) | 0.60 | 좌하 |
| `seg_nebula_crystal_drift` | 13.0s | (+0.70, +0.35) | **0.78** | 후반 강화 |
| `seg_nebula_prism_haze` | 13.0s | (−0.40, +0.80) | **0.89** | 후반 피크 (≤0.95) |

- 손을 떼면 흐름; 조작 속도 상한은 유지 (Core 계약).
- 연속 동방향 없음 — 세그먼트마다 부호/축 교대.

### 코어 — 총집합 + 제한 시간

| 세그먼트 | 체류 | 혼합 기믹 | 요구 플레이 |
|---|---:|---|---|
| `seg_core_guardian_wall` | 15.0s | 잔해 4 + 약 drift 0.36 | 가디언 엄폐 |
| `seg_core_final_gauntlet` | 14.0s | 레이저 2 + 잔해 2 | 최종 타이밍 |
| `seg_core_rift_blades` | 12.0s | 잔해 3 + drift 0.43 | 고속 회피 보정 |
| `seg_core_phase_discs` | 13.0s | corridor 18→11 + 레이저 1 | 위치 + 타이밍 |
| `seg_core_shard_battery` | 14.0s | 잔해 3 + 레이저 1 + drift 0.58 | 종합 중압 |
| `seg_core_void_mix` | 14.0s | corridor 17→9 + 잔해 4 + drift 0.60 | 최종 종합 |

중형 과부하 방지: 리프트 블레이드·가디언에는 좁은 통로 없음. 레이저 다발 + 협로 동시 배치 없음.

---

## 코어 제한 시간 여유 계산 (필수)

| 항목 | 값 |
|---|---|
| `boss_core` HP | **28_000** |
| 도달 예상 DPS (BalanceSim 앵커) | **1050** |
| 풀파워 DPS | **1880** |
| 보스 TTK @1050 | **26.7 s** |
| 보스 TTK @1880 | **14.9 s** |
| 보스 TTK @700 (고전) | **40.0 s** |
| 보스 TTK @500 (저화력) | **56.0 s** |
| 세그먼트 합 (평균×3) | ~**41 s** (2460 t) |
| 세그먼트 합 (최악 3×900) | **45 s** (2700 t) |
| **timeLimitTicks** | **9000** (= **150 s**) |
| 보스 잔여 예산 (최악 세그 후) | 150 − 45 = **105 s** |
| 여유 @1050 DPS | 105 / 26.7 ≈ **3.9×** |
| 여유 @700 DPS | 105 / 40 ≈ **2.6×** |
| 여유 @500 DPS | 105 / 56 ≈ **1.9×** |

→ 즉사 타이머이지만 **저화력 클리어에도 약 2배 여유**. 인심 박하지 않음.  
(REQ-055 연결 검증용 12000t 잠정값보다 타이트하지만, 실제 TTK 역산 기준 충분.)

---

## 상한 준수

| 캡 | 값 | 본 데이터 peak |
|---|---:|---:|
| MaxObstacles | 32 | **10** /세그 |
| MaxLasers | 8 | **3** /세그 |
| hive obs max (게이트) | 5 | **4** |
| drift 합성 | ≤0.95 | **0.89** |
| corridor min width | ≥5 u | **7.5 u** |

---

## 검증

| 항목 | 결과 |
|---|---|
| sim Core `dotnet test` (REQ-055 Core) | **351 / 353** — StageGimmick·DeterminismAuditSmoke 전부 통과 |
| 잔여 2 실패 | (1) `RepositoryApprovedV2Files` 적 수 30→31 기대값 (CODEX 테스트 갱신 요청) (2) `CurrentMiniBossContent` 리듬 런 — **content HEAD GameData도 sim Core에서 동일 실패** (기믹 도입 전 데이터 괴리, 기믹 단독 원인 아님) |
| content Core `dotnet test` | `laserEmitter` 미지원 → 파서 거부 (sim 병합 후 해소) |
| BalanceSim @ sim Core | 조립 50/50, 장애물/테마/보스 TTK/캡슐 **PASS**. 무기 v3·colossal Generate 실패는 **기존 sim/content 괴리** (본 작업 범위 밖) |
| 결정론 | `DeterminismAuditSmoke` 동일 시드 해시 일치 통과 |

---

## 산출물

- `GameData/enemies.json` — `hive_tentacle` 추가
- `GameData/waves.json` — `gimmicks[]`, `environment`, breakable/laser 배치, 촉수 spawn
- `Tools/BalanceSim/Program.cs` — stage-1 잔해 허용, laser 타입, 적 수 31
- `Tools/BalanceSim/_apply_req055_gimmicks.py` — 재현 스크립트

## 사람 결정 사항 (§7)

- 코어 `timeLimitTicks: 9000` — **제안**. 더 여유(10800/12000) 원하면 지시 바람.
- 잔해 HP·레이저 주기·드리프트 세기 — 잠정. 플레이테스트 후 확정.
- 네뷸라 `visionObscured` 비주얼은 CLAUDE Presentation 계약 (REQ-055).
