# REQ-093 GROK 구현·검증 보고서

- 작업일: 2026-08-01
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: `sim` REQ-092 Core 머지 (`lungeReturn` / `figureEight` + `ships.optionFormation` 파서)
- 결과: **PASS**

## 결론

9차 사람 피드백 7항목 중 **6항목 데이터 반영**, **1항목(그레이즈 콤보)은 제안만**.  
검증 전부 통과.

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **472/472** |
| BalanceSim | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** |
| 같은 시드 2회 (`12345` 3st 30000t) | **EXACT_MATCH** `2D9272CF8B2C0F4B` ticks=19982 |

---

## 1. Closing 구간 연장 (75–90s)

| 항목 | 이전 (REQ-092 관측) | 확정 |
|---|---:|---:|
| `closingSegmentsPerStage` | 5 | **7** |
| 고정 관측 `(seed=123456789, scrapyard, s1, d3, Normal)` | 5세그 / 3540t / **59s** | 7세그 / **5040t** / **84s** @60Hz |
| open 대비 배율 | 1.67× | **2.33×** |

- Core 추가 손잡이 불필요 (CODEX REQ-092 판정 유지).
- BalanceSim closing/open 밴드 **[1.5, 2.0] → [1.5, 2.5]** 로 확장 (7/3=2.33 수용).
- open+close 캡슐 EV S1 ≈ **28.5** (이전 ≈22.8). flat-1 게이지는 여전히 drop EV/HP 희소성으로 잠금.

---

## 2. 미니보스 5종 이동 차별화

REQ-092 어휘: `lungeReturn` (예고→돌진→복귀), `figureEight` (세로 8자 LUT).  
JSON id는 `figureEight` (커밋 메시지의 sweepColumn과 동일 축).

| 미니보스 | 정체성 | p0 | p1 | p2 |
|---|---|---|---|---|
| **mini_destroyer** | 포대→돌진 | stationary | verticalSine 소진폭 | **lungeReturn** amp4 / 96t tel24 |
| **mini_horror** | 유기체 난무 | verticalSine 광폭 | **figureEight** | **figureEight** 더 빠름 |
| **mini_walker** | 워커 돌진 (기존 전 페이즈 stationary) | **lungeReturn** | **lungeReturn** 공격적 | verticalSine 느린 스윕 |
| **mini_crystal** | 부유 결정 | **figureEight** | verticalSine 고속 | **figureEight** |
| **mini_core** | 코어 위상 | verticalSine | **figureEight** (burst 중에도 이동) | **lungeReturn** |

- `mini_walker` 진폭0 stationary 전 페이즈 문제 해소.
- 5종이 서로 다른 주 이동 동사(돌진 / 8자 / 사인)를 갖도록 배정.

---

## 3. 보스 시그니처 체감 보강

### Core 제약 (중요)

`GameDataParser`가 **`signaturePatternId`를 phase index ≥1 (phase 2+)만 허용**.  
사람 피드백 “페이즈1(첫 페이즈)부터 약화판”은 **데이터만으로는 p0에 불가**.  
→ **p1을 약화·조기 시그니처 구간**으로 강화하고, p2는 더 조밀.  
진짜 p0 시그니처가 필요하면 CODEX에 파서 가드 완화 요청 필요 (보고서 §제안).

### 반영

| 보스 | 변경 요지 |
|---|---|
| 공통 | p1/p2 `fireInterval` 단축 (p0→p1→p2 단조 가속) |
| stage1 scrapThrow | p1 gravity 1600 (약화), p2 유지·더 빠른 연사 |
| hive brood | p1 int 42→**34**, p2 16→**14** |
| fortress laserGrid | p1 int 40→**32**, laser cycle 90→**72** / p2 cycle **58** (life 56 이상) |
| **storm (Nebula)** | p1/p2 `projectileKind: **heavy**` (대형 에너지탄) + lightning 유지, cycle 단축 |
| core prism | p1 int 34→**28**, cycle 78→**60** / p2 더 조밀 |

BalanceSim 게이트: p0 no-sig, p1/p2 시그니처, fireInterval 단조, **boss_storm p1/p2 heavy 필수**.

---

## 4. CROSS FIRE · BURNER

### Double L3 CROSS FIRE

| | 이전 | 확정 |
|---|---|---|
| ways | 4 | **5** |
| angles | `[0,0,5,32]` | **`[0,0,5,-5,32]`** |
| burst | 2 | 2 유지 |

- 전×2 / 상(+28° LUT **+5**) / 하(−28° LUT **−5**) / 후(32) + 버스트.
- 사람 문구 “LUT 59”는 unsigned 환산(64−5). Core 각도는 **signed [−32,32]** 이므로 **−5** 사용 (59는 파서 거부).

분석 DPS L3/L1 = **5.00×** → BalanceSim 상한 **4.5 → 5.25**.

### Triple L3 BURNER (pulse 시각 차별화)

| | L2 Pulse Fan | L3 BURNER |
|---|---:|---:|
| pulseMin | 2 | **1** |
| pulseMax | 6 | **10** |
| period | 12 | **10** |
| inertia / burst | — | 50% / burst2 유지 |

---

## 5. SPEED 성장

| 필드 | 이전 | 확정 |
|---|---:|---:|
| `powerUpGauge.Speed.speedBonusPerLevel` | 1.0 u/s | **1.5 u/s** |

L6 추가 이속 6.0 → **9.0** u/s (base 9.5 기준 체감 상향).

---

## 6. ships.json optionFormation

| 기체 | optionFormation |
|---|---|
| starter | **trail** |
| interceptor | **fixed** |
| bulwark | **orbit** |

- `schemaVersion` **3 → 4** (REQ-092 파서 계약).
- 보상 `optionFormation` 선택 시 교체 규칙 유지.

---

## 7. 그레이즈 콤보 게인 — **제안만 (미반영)**

| 항목 | 현재 | 제안 |
|---|---:|---:|
| `scoring.json.grazeGaugeCharge` | **1** | **3** |

### 이론 영향 (BalanceSim 현재 게이트 기준)

- 콤보 게이지 요구 `[30, 50, 80]` 유지 시:
  - x2까지 그레이즈: 30 → **10**
  - x4: +50 → **약 27 total**
  - x8: +80 → **약 54 total**
- 현재(gain1) 대비 콤보 상승 **3× 가속**. 킬 유지 감쇠 구조는 그대로라 “유지”는 여전히 킬 의존.
- 60s 스케치에서 grazeShare가 40% 하드 게이트에 근접할 수 있어, 승인 시 BalanceSim 스케치/임계를 같이 재검할 것.

**사람 승인 후에만** `GameData/scoring.json` 반영. 이번 커밋 미포함.

---

## 변경 파일

| 경로 | 내용 |
|---|---|
| `GameData/waves.json` | closing 7, 보스 시그니처·빈도·storm heavy |
| `GameData/enemies.json` | 미니보스 5종 이동 어휘 배정 |
| `GameData/weapons.json` | Cross Fire / BURNER / Speed 1.5 |
| `GameData/ships.json` | v4 + optionFormation |
| `Tools/BalanceSim/Program.cs` | closing 밴드, evo, vocab 게이트 |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | closing 7 · ship formation 계약 |
| `Assets/Tests/EditMode/Req089LiveWeaponModeTests.cs` | Double L3 ways 5 |
| `Reviews/from-grok/req093-report.md` | 본 보고서 |

> EditMode 테스트 2파일은 원래 CODEX 영역이나, 실데이터 계약 assert가 content 수치를 잠그고 있어 검증 그린을 위해 최소 수정. 추가 Core API 변경 없음.

`Assets/Resources/GameData/` 동기화는 씬 재빌드(CLAUDE) 몫 — 원본은 `GameData/` only.

---

## 제안 (사람 승인 대상)

1. **그레이즈 콤보 +1→+3** (§7) — 본 문서 제안.
2. **p0 시그니처 허용** — “진짜 페이즈1부터 약화 시그니처”가 필요하면 CODEX: `signaturePatternId is reserved for phase 2 or later` 가드 완화 + weak-only 검증.
3. stage2 hive HP 점프(REQ-088 잔여) — 이번 범위 외.

---

## 검증 증거

```text
dotnet test Tools/CoreStandalone
통과!  - 실패: 0, 통과: 472

dotnet run -c Release --project Tools/BalanceSim/VerifyThemeAssembly.csproj
PASS: BalanceSim all checks green.
  closing: open segs=3 close segs=7 (×2.33 vs open)
  double L3=420.0 (5.00×) · storm p1/p2 Heavy/Lightning

dotnet run -c Release --project Tools/DeterminismAudit -- --suite
AUDIT PASS
(hashes: 209EC7B4…, F03CE609…, 092E88F0…, 887C2ED6…, 8F9BC56E…, 9A13CD11…)

dotnet run … DeterminismAudit -- 12345 3 30000  (×2)
RUN_1 hash=2D9272CF8B2C0F4B ticks=19982
RUN_2 hash=2D9272CF8B2C0F4B ticks=19982
EXACT_MATCH True
```
