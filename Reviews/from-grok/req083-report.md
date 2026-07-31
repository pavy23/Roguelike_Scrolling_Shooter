# REQ-083 GROK 구현·검증 보고서

작성일: 2026-07-31  
브랜치/worktree: `content` / `wt-content`  
범위: REQ-083 네 항목 + REQ-082 GROK 후속(함선 6칸 데이터·BalanceSim)

## 결론

REQ-083 네 항목과 CODEX REQ-082의 GROK 후속을 모두 반영했다.
`dotnet test` **412/412**, BalanceSim **all green**, DeterminismAudit **AUDIT PASS**,
같은 시드 2회 해시 일치.

---

## 1. 더블 샷 각도 (~30°)

| 항목 | 값 |
|---|---|
| LUT 해상도 | `PrimaryWeaponFamilyDefinition.AngleLutSlotsPerTurn = **64**` |
| 목표 | 상향탄 ≈ 30° |
| 계산 | 30° / 360° × 64 = **5.333…** 슬롯 |
| 채택 | 슬롯 **5** → 5/64 × 360° = **28.125°** (가장 가까운 정수 슬롯; 6은 33.75°) |
| 데이터 | `weapons.json` double `shotAngleLutSlots: [0, 5]` (이전 `[0, 8]` = 45°) |
| 설명 문구 | "upward ~30° diagonal"로 갱신 |

BalanceSim은 `[0, 5]`를 FAIL 게이트로 검사한다 (clearability + primary family).

---

## 2. MainShot 게이지 6칸 (REQ-082 + REQ-083)

### 스키마 메모 (Core 파서)

- **함선 게이지** (`ships.json.powerUpGaugeSlots`): 정확히 **6칸**, `MainShot` 필수.
- **7슬롯 카탈로그** (`weapons.json.powerUpGauge.slots`): Core가 `MainShot`을
  **"hidden shared power axis"** 로 거부한다. 여기에 MainShot을 넣으면 파싱 실패.
- MainShot **maxLevel** = `weapons[].main_shot.maxLevel` (**5**).
- MainShot **costCurve** = 루트 `powerUpCostCurve` (REQ-083에서 평탄 1).
- 표시 키는 Core가 함선 게이지 생성 시 `"powerUp.mainShot"` 고정
  (Presentation HUD SHOT 매핑은 CLAUDE REQ-083 선반영).

### ships.json (전 기체)

| 기체 | 순서 |
|---|---|
| starter | Speed, **MainShot**, Missile, Weapon→Double, Option, Shield |
| interceptor | Speed, **MainShot**, Missile, Weapon→Triple, Option, Shield |
| bulwark | Speed, **MainShot**, Missile, Weapon→Laser, Option, Shield |

`startingPowerUpLevels`는 REQ-081 잠금 유지: 레거시 4슬롯 축 `[0,0,0,0]` (Main 포함 전부 0).

### BalanceSim (REQ-082 후속)

- 기대 배열: `Speed, MainShot, Missile, weapon, Option, Shield`
- 지정 무기 인덱스: **2 → 3**
- 무기 슬롯 도달 `Collect()`: **3회 → 4회**
- MainShot cost flat-1 검사 추가

---

## 3. 레벨업 비용 평탄화 (사람 지정 불가침)

모든 게이지 슬롯 + 공유 MainShot 곡선:

```text
baseCost=1, linearGrowth=0, quadraticGrowth=0  →  캡슐 1 = 레벨 +1
```

| 슬롯 | maxLevel | costToMax (평탄 후) | 이전 costToMax (대략) |
|---|---|---|---|
| Speed | 6 | **6** | 21 (1+L) |
| MainShot (shared) | 5 | **5** | 55 (1+L+L²) |
| Missile | 6 | **6** | 1+L² 계열 |
| Double/Laser/Triple | 1 | **1** | 1 (동일) |
| Option | 4 | **4** | 2+L+L² 계열 |
| Shield | 6 | **6** | 2+L² 계열 (softCap 효과 3 유지) |

### 캡슐 EV vs 총 레벨 비용 (BalanceSim 실측)

| 지표 | 값 |
|---|---|
| Stage1 open capsule EV | ≈ **8.55** |
| Open+close (1 room pair) | ≈ **17.1** |
| Run EV (5 biomes × open+close) | ≈ **85** |
| 7슬롯 exclusive full (한 모드) | ≈ **23** |
| 함선 6칸 full (Main5 포함) | Speed6+Main5+Mis6+Wpn1+Opt4+Shd6 = **28** |

평탄 1 이후 **한 런 EV로 게이지 전부를 채울 여유**가 생긴다. 사람 지정 비용은 유지하고,
BalanceSim의 구 1+L+L² 대역(Speed 15–40 등)과 “exclusive full > 1.2× run EV” 게이트를
**flat-1 계약**으로 교체했다. 시작 레벨/비용은 건드리지 않았다.

### 성장 속도 부작용 (보고 전용)

- open EV ≈8.55 → mid/boss Main 추정 **L5 포화**
- midboss TTK @ midEff: 2.5–5.1s (이전 곡선 대비 크게 단축)
- stage-1 boss TTK @ reachEff ≈ **24.5s** (클리어 게이트 통과)
- 추가 HP/드롭 보정 없이 게이트 전부 green. 손맛이 과하면 다음 라운드에서
  **적 HP 스케일 또는 캡슐 드롭**으로 재조정 권고 (비용 평탄 1은 유지).

Shield **effectSoftCapLevel = 3** 유지 (maxLevel 6).

---

## 4. 졸개 크기 ×1.25

- 대상: `enemies.json` 중 `midBoss` 없는 **29종** (보스/중간보스 제외).
- 레이저 센트리·프리즘 비머 포함.
- 미니보스 5종: 제외 (이미 대형 히트박스, 사람 표현 “졸개”에 해당하지 않음).
- 필드: `halfWidth` / `halfHeight` × 1.25, 1/256 world-unit 양자화.

| id | before (W×H) | after (W×H) |
|---|---|---|
| zako_straight | 0.5625×0.4375 | 0.703125×0.546875 |
| zako_sine | 0.5625×0.4375 | 0.703125×0.546875 |
| zako_fast | 0.5×0.375 | 0.625×0.46875 |
| zako_tank | 1.25×1.0 | 1.5625×1.25 |
| zako_sine_slow | 0.9375×0.75 | 1.171875×0.9375 |
| turret_ground / turret_ceiling | 0.9375×0.875 | 1.171875×1.09375 |
| elite_sine | 1.3125×1.0625 | 1.640625×1.328125 |
| spore_drifter | 0.625×0.5625 | 0.78125×0.703125 |
| lancer_dart | 0.4375×0.3125 | 0.546875×0.390625 |
| hive_tentacle | 0.625×1.25 | 0.78125×1.5625 |
| sentry_drone | 0.9375×0.75 | 1.171875×0.9375 |
| interceptor_rush | 0.5×0.375 | 0.625×0.46875 |
| wisp_spark | 0.5×0.4375 | 0.625×0.546875 |
| guardian_sphere | 1.375×1.25 | 1.71875×1.5625 |
| scrap_tumbler | 0.875×0.6875 | 1.09375×0.859375 |
| brood_spitter | 0.9375×0.8125 | 1.171875×1.015625 |
| mortar_drone | 0.9375×0.8125 | 1.171875×1.015625 |
| laser_sentry | 1.0×0.875 | 1.25×1.09375 |
| echo_wisp | 0.875×0.6875 | 1.09375×0.859375 |
| rust_skimmer | 0.5625×0.4375 | 0.703125×0.546875 |
| junk_roller | 0.625×0.5 | 0.78125×0.625 |
| void_moth | 0.875×0.6875 | 1.09375×0.859375 |
| shard_prism | 1.375×1.25 | 1.71875×1.5625 |
| sting_hornet | 0.5625×0.4375 | 0.703125×0.546875 |
| pipe_rat | 0.5×0.375 | 0.625×0.46875 |
| phase_disc | 0.9375×0.75 | 1.171875×0.9375 |
| prism_beamer | 1.0×0.875 | 1.25×1.09375 |
| rift_blade | 0.5×0.3125 | 0.625×0.390625 |

밸런스 영향: 명중률·접촉 위험 동시 상승. 시뮬 클리어 게이트는 통과.
(히트박스 확대는 Presentation 스프라이트 스케일과 동일 필드를 공유.)

---

## 변경 파일

| 경로 | 내용 |
|---|---|
| `GameData/weapons.json` | double [0,5], powerUpCostCurve·전 슬롯 flat 1 |
| `GameData/ships.json` | 3기체 MainShot 6칸 |
| `GameData/enemies.json` | 졸개 29종 ×1.25 |
| `Tools/BalanceSim/Program.cs` | 6칸 계약, flat-1 게이트, 30° 검사 |
| `Reviews/from-codex/requests.md` | REQ-082 체크 완료 |
| `Reviews/from-claude/req083-…md` | 체크리스트 완료 |
| `Reviews/from-grok/req083-report.md` | 본 보고서 |

---

## 검증 증거

### CoreStandalone

```text
cd Tools\CoreStandalone && dotnet test --no-restore
통과!  실패: 0, 통과: 412, 전체: 412
```

(작업 전 과도기 410/412 — ships 5칸 파싱 실패 2건이 해소됨.)

### BalanceSim

```text
cd Tools\BalanceSim && dotnet run --project VerifyThemeAssembly.csproj -c Release
PASS: BalanceSim all checks green.
```

주요 출력:
- `double shotAngleLutSlots=[0, 5] ≈ 28.1°`
- `Main costs L0→1=1 L1→2=1 L2→3=1 (flat 1)`
- mid Main L5 / boss Main L5 @ open EV
- stage-1 CLEAR, 전 stage 리포트 green

### DeterminismAudit

```text
cd Tools\DeterminismAudit && dotnet run --no-restore --project . -- --suite
AUDIT PASS
```

| 시나리오 | hash |
|---|---|
| seed-0-first | `0E3683771EB9A5BA` |
| seed-1-last | `BF0DF454B2C09E15` |
| seed-12345-rotating | `21EAE9EC77EE4B8E` |
| seed-deadbeef-rotating | `702B6704D2B0C930` |
| seed-max-prefer-capped | `FDF3FD05E3EC959F` |
| seed-7-hidden | `C395351F2B32BBFA` |

cap-boundary: 256/256 matched.

### 같은 시드 2회

```text
dotnet run --no-restore --project . -- 12345 3 30000
```

| 회차 | hash | ticks | stages | rooms |
|---|---|---|---|---|
| 1 | `4432CEB27060C4D4` | 17803 | 3/3 | 9/9 |
| 2 | `4432CEB27060C4D4` | 17803 | 3/3 | 9/9 |

**SAME-SEED HASH MATCH**

---

## 작업 경계

- 수정: `GameData/*.json`, `Tools/BalanceSim/`, Reviews (from-grok / 체크박스 응답).
- Core / Presentation / QA 소유 파일 미수정.
- 사람 §7 잠금: 시작 레벨 0 유지, Shield softCap 3 유지, 비용 평탄 1 준수.

---

## Option 6 (REQ-084 Content follow-up)

작성일: 2026-07-31  
전제: main에 REQ-084 Core 병합 완료 (`PowerUpGauge.MaximumOptionCount = 6`, fixed 4/6 오프셋 파서 수용).

### 데이터 변경 (`GameData/weapons.json`)

| 항목 | before | after |
|---|---|---|
| `weapons[].option.maxLevel` | 4 | **6** |
| `weapons[].option.effectSoftCapLevel` | 4 | **6** |
| `powerUpGauge.slots[Option].maxLevel` | 4 | **6** |
| Option `costCurve` | flat 1 | **유지** (사람 지정) |

### fixed 편성 오프셋 (Core REQ-084 정합)

World 단위 정의 → subunits = world × 256.

| index | x (world / su) | y (world / su) |
|---|---|---|
| 1 | 0.75 / 192 | +1.5 / +384 |
| 2 | 0.75 / 192 | −1.5 / −384 |
| 3 | 0.75 / 192 | +2.75 / +704 |
| 4 | 0.75 / 192 | −2.75 / −704 |
| 5 (신규) | 0.75 / 192 | **+4.0 / +1024** |
| 6 (신규) | 0.75 / 192 | **−4.0 / −1024** |

`Reviews/from-codex/req084-report.md` 5·6번 오프셋과 일치.

### BalanceSim

- 7슬롯 카탈로그 기대 maxLevel: Option **4 → 6**
- Option `costToMax` 게이트: flat-1 기준 **[4,8]** (maxLevel 6)
- fixed formation 오프셋 검사: **4 → 6** (+ y ±4.0)

실측: Option max=6 costToMax=6 · exclusive full≈25 · run EV≈114

### 콘텐츠-연동 테스트 1줄

`GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely`가 저장소 Option maxLevel을 4로 고정하고 있었음  
(CODEX 주석: content-owned REQ-084 반영 전까지). **6으로 갱신** (데이터 계약 동기화, Core 로직 변경 없음).

### 검증 증거 (Option 6 후)

```text
cd Tools\CoreStandalone && dotnet test --no-restore
통과!  실패: 0, 통과: 415, 전체: 415
```

```text
cd Tools\BalanceSim && dotnet run --project VerifyThemeAssembly.csproj -c Release
PASS: BalanceSim all checks green.
```

```text
cd Tools\DeterminismAudit && dotnet run --no-restore --project . -- --suite
AUDIT PASS
```

| 시나리오 | hash (Option 6 데이터 기준) |
|---|---|
| seed-0-first | `39321721C89A947C` |
| seed-1-last | `23778E265F533C7C` |
| seed-12345-rotating | `8F25A13F2507914E` |
| seed-deadbeef-rotating | `82C4E471CF44ABE3` |
| seed-max-prefer-capped | `2BE53C20A522FDD9` |
| seed-7-hidden | `77E6CFF86E100130` |

cap-boundary: 256/256 matched.

같은 시드 2회 (`12345`, stages=3, ticks=30000):

| 회차 | hash | ticks | stages | rooms |
|---|---|---|---|---|
| 1 | `B51FE840D8DD3011` | 17752 | 3/3 | 9/9 |
| 2 | `B51FE840D8DD3011` | 17752 | 3/3 | 9/9 |

**SAME-SEED HASH MATCH** (Option max 6 반영으로 suite/single 해시 베이스라인 갱신됨 — 결정론 자체는 유지).

### 변경 파일 (Option 6)

| 경로 | 내용 |
|---|---|
| `GameData/weapons.json` | option max/softCap 6, gauge offsets 6, gauge slot max 6 |
| `Tools/BalanceSim/Program.cs` | Option·fixed 기대값 6 |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | RepositoryApproved Option max 6 (1 assert) |
| `Reviews/from-grok/req083-report.md` | 본 절 |
