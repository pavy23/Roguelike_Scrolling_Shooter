# REQ-111 GROK 구현·검증 보고서 — 대형 기믹 데이터 (St3 전함 + St5 고스트)

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-109 (타임루프 고스트 Core) · REQ-110 (WarshipEncounter Core)
- 결과: **PASS**

## 결론

| 축 | 처리 |
|---|---|
| St3 fortress 전함 | `boss_fortress`에 `parts[]` + `warship` 정의 — 함미 engine / 함체 포탑 4 / 함수 core |
| 일반 스폰 | 보스 룸은 Core `CreateBiomeBossPlan`으로 이미 빈 스폰(전함 단독). 후반 fortress fodder 소폭 thin |
| 전투 시간 | total HP **19600** ≈ mini_walker(1600)+구 boss(18000). pure ST @720 ≈ **27.2s**, wall ≈ **37.2s** |
| St5 고스트 구간 | Closing 밀도 **유지** (고스트는 full-run 보너스만). 고정 화력 L1/8t **기본값 유지** |
| BalanceSim | `CheckReq111WarshipAndGhost` 게이트 추가 · all green |

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **529/529** |
| BalanceSim | **all green** (`CheckReq111WarshipAndGhost` 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary 256) |

---

## 1. St3 전함 (`boss_fortress`)

### warship 계약

| 필드 | 값 | 의도 |
|---|---:|---|
| id | `fortress_warship` | REQ-110 예시와 동일 |
| eventEntityId | 110 | 이벤트 엔티티 |
| warningTicks | 180 | 3s WARNING |
| originX/Y | 24 / 0 | 우측 진입 |
| scrollSpeedPerSecond | 3.0 | 함체 서사 스크롤 |
| baseCoreOpeningWays | 9 | 포탑 0 파괴 시 |
| waysReductionPerTurret | 2 | 포탑당 개막 밀도 감소 |
| minimumCoreOpeningWays | 3 | 바닥 |

### groups

| group | role | parts | advance |
|---|---|---|---:|
| stern | midbossGate | engine | (전 파괴) |
| hull | attritionLine | turret_a..d | **720** (12s) |
| bow | finalCore | core | (전 파괴) |

함미 격파 시 Core가 `MidBossDefeated` 발행 (REQ-110). 함수 개막 ways는 `CoreOpeningWays`로 독립 (midbossOutcome 재사용 없음).

### parts HP · 무장

| part | HP | attack | 비고 |
|---|---:|---|---|
| engine | 2200 | radialSpread 5-way / 64t | midboss 게이트 (walker 1600 근방↑) |
| turret_a..d | 900×4 | **aimedSpread** (터렛/sentry 어휘) | 선택 파괴 → ways 분기 |
| core | 13800 | radialSpread 9-way / 36t | 보스 슬롯 · isCore |
| **합** | **19600** | | MaxHp = sum (Core 불변식) |

halfWidth/Height·offset는 1/256 서브유닛 양자화 (laser_sentry hitbox 1.25×1.09375 재사용).

### 단일체 phase 제거

전함은 part attack이 본전투. 기존 3-phase LaserGrid 사다리는 제거. BalanceSim 표준 보스 phase 게이트는 warship을 skip하고 REQ-111 전용 게이트로 이관.

### 후반 일반 스폰 thin

전함 구간(보스 룸) 스폰은 Core가 이미 비움. 클라이맥스 과밀 방지로 late fortress fodder만 교대 삭제:

| segment | 제거 |
|---|---:|
| seg_fortress_drone_lattice | 5 |
| seg_fortress_armored_gate | 3 |
| seg_fortress_crossfire_alley | 2 |

터렛·laser_sentry·mortar 등 요새 어휘는 유지. cleanKill 분기는 손대지 않음.

---

## 2. 전투 시간 · 클리어 가능성 (BalanceSim)

St3 reach DPS = **720** (기존 boss_fortress 앵커 유지).

| 지표 | 값 | 게이트 |
|---|---:|---|
| pure ST TTK (전 파츠) | 27.2s | 22–36s |
| full-power TTK @1880 | 10.4s | ≥8s |
| wall-clock (warn+engine+attr+core) | 37.2s | 28–55s |
| legacy mid+boss ST ref | 27.2s | 정렬 목표 |

| 포탑 파괴 | CoreOpeningWays |
|---:|---:|
| 0 | 9 |
| 2 | 5 |
| 4 | 3 (min) |

fortress 테마 핀 조립: `GenerateRoute(..., "fortress")` → warship plan clearable.

---

## 3. St5 고스트 구간

### 활성 창

- Core: `BiomeIndex == Final && Section == Closing && HasStageOneGhostRecording`
- Closing segs = **7** · core late (dMin≥3) avgSpawns ≈ **15.2**
- St1 직행이 아닌 full-run에만 고스트 존재

### 고정 화력 검토 (`GhostReplayConfig` 기본값)

| 항목 | 값 | 근거 |
|---|---:|---|
| FixedWeaponLevel | **1** | “과거 자아 저레벨” 판타지 · main_shot base |
| FireIntervalTicks | **8** | main_shot L1 cadence와 동일 |
| 데미지 | 10 | `Damage.Compute(base, 1)` |
| DPS | **75** | 10×60/8 |
| St5 reach(1050) 대비 | **7.1%** | 게이트 ≤12% (carry 금지) |

**유지 결정:** 기본값 변경 없음. 고스트는 보조 화력이지 클리어 조건이 아님.

### 밀도 조정 판단

고스트 DPS를 전제로 closing을 상향하면 **St3 직행·기록 없음 런**이 불이익. BalanceSim 판정: **밀도 유지 (no densify)**. intent에 `REQ111 ghost-window density held` 표기만 추가.

---

## 4. Core 긴급 보정 (소유 경계 메모)

테마 셔플 경로에서 `RunManager.ApplyProgressionBossDifficulty`가 **다른 보스의 MaxHp**를 warship parts에 덮어써 `Multipart boss HP must equal the sum of its part HP`로 크래시.

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Core/Simulation/RunManager.cs` | warship/multipart면 progression MaxHp 치환 **skip** |

CODEX 소유 영역이나 REQ-111 데이터가 테마 셔플에서 로드 불가 → DeterminismAudit 차단. 최소 가드 1곳만 수정. **CODEX 리뷰·인수 요청** (requests.md).

---

## 5. BalanceSim 변경

- `CheckBossRedesign`: warship/multipart phase 게이트 skip · HP mono는 유지
- `CheckBossBulletVocabulary`: warship phase signature skip (fortress LaserGrid 제거 반영)
- **신규** `CheckReq111WarshipAndGhost`: 파츠/그룹/ways/TTK/조립/고스트 화력·밀도 정책

---

## 6. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | boss_fortress warship+parts · fortress late fodder thin · core intent 표기 |
| `Tools/BalanceSim/Program.cs` | REQ-111 게이트 · warship 호환 보스 검사 |
| `Assets/Scripts/Core/Simulation/RunManager.cs` | multipart progression HP 가드 (긴급) |
| `Reviews/from-grok/req111-report.md` | 본 보고 |
| `Reviews/from-grok/requests.md` | 요청 갱신 |
| `Tools/_req111_*.py` | 적용 스크립트 (재현) |

---

## 7. DeterminismAudit 해시 (대표)

| scenario | hash |
|---|---|
| seed-0-first | `EAE8157691E80783` |
| seed-12345-rotating | `33B753E790BAF638` |
| seed-7-hidden | `5B0DDD764FC79904` |

content·warship plan 해시 포함 경로 → GEMINI 베이스라인 갱신 여부 판단 요청.
