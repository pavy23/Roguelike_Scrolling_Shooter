# REQ-079 — 기체별 무기·실드·게이지 + maxLevel 6 + Closing 연장 (content)

**작성:** GROK · 2026-07-31  
**상태:** GameData 반영 · 검증 통과 · 커밋  
**선행:** `git merge main` (REQ-078 Core 400/400 + AUDIT PASS)

## 검증

| 항목 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **400/400** |
| BalanceSim (`VerifyThemeAssembly`) | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary) |
| 동일 시드 2회 해시 | suite 시나리오별 2회 일치 강제 |

---

## A. 기체 정체성 (사람 지정 — 변경 없음)

| 기체 | id | 게이지 무기 | 시작 실드 | 이속 배수 | 성격 |
|---|---|---|---:|---|---|
| 1 Starter | `starter` | **DOUBLE** (`gaugeWeaponFamily: double`) | **1** | 1/1 | 밸런스 |
| 2 | `interceptor` | **TRIPLE** (`triple`) | **0** | **5/4** | 빠르지만 약함 |
| 3 | `bulwark` | **LASER** (`laser`) | **2** | **4/5** | 느리지만 탱킹 |

- schemaVersion **2**, 5칸 게이지: `Speed / Missile / Weapon / Option / Shield`
- 기체 무기 슬롯: Core `ActivatesImmediately` + catalog `baseCost: 1` (1회 활성화 즉시 발동)
- baseline `weaponType`: starter=`vulcan` (모드 전), interceptor=`spread`, bulwark=`laser`
- 이속 배수: 기존 5/4·4/5 유지 (이미 사람 성격과 일치)

---

## B. 슬롯 maxLevel 6 + 비용 곡선

| 슬롯 | maxLevel | 곡선 | L0→max 캡슐 | 비고 |
|---|---:|---|---:|---|
| Speed | **6** | 1+L | **21** | 전방 저가, L6은 유의미 투자 |
| Missile | **6** | 1+L² | **61** | 올인 런에서만 L6 |
| Double/Laser/Triple | **1** | flat **1** | **1** | 즉시 전환 |
| Option | **4** | 2+L+L² | **28** | 아래 판단 |
| Shield | **6** | 2+L² | **67** | 올인, softCap 3 |

- exclusive full (모드 1개) ≈ **178** 캡슐
- open+close EV S1≈**22.8** → 5 biome run EV≈**114** → exclusive full ≈ 9.5 stages (한 런 전부 불가)
- Missile/Shield L6 ≈ 61–67 ≈ 올인 시 run EV 상당분

### SHIELD 레벨 vs 실드 상한 (판단)

| 축 | 값 | 출처 |
|---|---|---|
| SHIELD maxLevel | 6 | REQ-079 사람 |
| `BattleSimConfig.MaxShieldStock` | **3** (hard max **5**) | Core §7 잠정 |
| 레벨업 효과 | +1 stock / 레벨 상승 (effective) | Core `RecoverShieldStock` |
| `effectSoftCapLevel` | **3** | content 조정 |

**판단:** maxLevel 6을 유지하되 `effectSoftCapLevel=3`으로 유효 레벨을 실드 상한(3)에 맞춤.  
softCap 이후 raw 4–6은 `floor(sqrt(Δ))` 감쇠만 적용 → stock 상한 3을 넘기지 않음.  
상한 자체를 6으로 올리려면 **§7 사람 결정 + CODEX** (`MaximumShieldStock`).

### OPTION 상한 (판단)

**Option maxLevel = 4 유지** (사람 기본 “각 기술별 6”에서 예외).

근거:
1. Core `optionFormations.fixed.offsets` 스키마가 **정확히 4개** 강제 (`GameDataParser.Weapons`)
2. `BattleSim`은 Fixed 선택 시 `offsets.Length >= GetMaxLevel(Option)` 필요 → maxLevel 6이면 런타임 예외
3. 화면 과밀: 옵션 6기는 640×360에서 탄막·가독성 위험

→ maxLevel 6 원하면 CODEX: Fixed offsets 6 + 파서 허용 범위 확장 후 content 재조정.

---

## C. Closing 연장

| | Opening segs | Closing segs | 배율 | open EV | open+close EV | 룸 시간(가중 평균 seg≈12.2s) |
|---|---:|---:|---:|---:|---:|---:|
| **Before** | 3 | 3 (default=open) | 1.0× | ≈8.6 | ≈17.1 | open+close ≈ **73s** |
| **After** | 3 | **5** | **1.67×** (1.5–2 밴드) | ≈8.6 | ≈**22.8** | open+close ≈ **98s** |

- 목적: 중간 보상 후 고른 파워를 Closing에서 써 볼 시간 (+약 25s/stage)
- EV 증가와 B 비용 곡선 일관: L6 올인 슬롯(61–67)은 run EV≈114 대비 절반 이상 투자 → 여전히 올인 전용

---

## 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/ships.json` | schema v2, 3기체 게이지·실드·무기 배정 |
| `GameData/weapons.json` | maxLevel 6 (Speed/Missile/Shield), 비용, 무기 baseCost 1, Shield softCap 3 |
| `GameData/waves.json` | `closingSegmentsPerStage: 5` |
| `Tools/BalanceSim/Program.cs` | REQ-079 ship/gauge/closing EV 게이트 |
| `Tools/DeterminismAudit/Program.cs` | PreferCapped ST 스톨 방지 (커버리지 모드 자동 활성화 제거) |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | 골든 기대치 REQ-079 동기화 |

---

## 요청

### CLAUDE
- [ ] `Assets/Resources/GameData/{ships,weapons,waves}.json` 동기화
- [ ] 5슬롯 게이지 HUD (기체별 Weapon 슬롯 1개 표시)

### CODEX (선택)
- [ ] Fixed formation offsets 6 + 파서 허용 → Option maxLevel 6 가능
- [ ] `MaxShieldStock` §7 확정 시 6 연동 여부
- [ ] 감사 하니스: 커버리지 모드 활성화 시 보스 Y 추종/직사 폴백 (현재는 ST 유지로 우회)
