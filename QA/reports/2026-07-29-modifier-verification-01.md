# QA Modifier Verification Report: 시너지 모디파이어 (REQ-013/014) 통합 검증 (2026-07-29)

**Evaluator**: GEMINI (QA / VERIFIER)  
**Target Repository**: `D:\Unity_Work\Roguelike_Scrolling_Shooter\wt-qa`  
**Target Branch**: `qa` (main 최종 병합 상태)  
**Execution Date**: 2026-07-29  
**Final Decision**: **PASS (조건부 밸런스 조정 권고 포함 / PASS WITH BALANCING RECOMMENDATION)**  

---

## 1. Executive Summary & Verdict Summary

본 보고서는 main 브랜치에 최종 병합된 **시너지 모디파이어 4종**(`pierce_shot`, `ricochet`, `homing_missile`, `kill_explosion`, `BattleModifier` 플래그) 및 **보상 카탈로그 13종**(`rewards.json`)에 대한 QA 검증 결과를 기록한다.

### 종합 판정: **PASS (조건부 밸런스 조정 권고)**

1. **결정론 감사 (`Tools/DeterminismAudit`)**:
   - 모디파이어 활성화 경로(First, Last, Rotating, PreferCapped)를 포함한 5개 시나리오 및 256개 시드 대상 `cap-boundary` 스윕 100% 동일 해시 일치 (**AUDIT PASS**).
   - `homing_missile` 조향(Sine/Cosine LUT) 및 `ricochet` 타이브레이크(거리 동률 시 lower `candidate.Id` 선호)의 시드 간 결정론적 안정성 확인.
2. **보상 가중치 분포 독립 재계산 (`rewards.json`)**:
   - 총 13종 보상 항목 독립 계산 결과, **Stage 1 기대 모디파이어 수 E[mods] = 1.20**, **Stage 2+ 기대 모디파이어 수 E[mods] = 1.04**로 측정됨.
   - GROK의 주장인 *"3택 카드 제시 시 평균 1개꼴로 모디파이어 등장"*이 수학적으로 정밀하게 입증됨 (디자인 가이드 밴드 [0.5, 1.8] 충족).
3. **`kill_explosion` 벌집 스웜(Swarm) 과강 현상 정량화 및 권고**:
   - `Tools/BalanceSim` 밀집 포격 시뮬레이션(12마리 0.5u 간격 스웜) 결과, `kill_explosion` 단독 적용 시 클리어 시간이 Baseline 대비 **3.15배** (사격 12발 → 3발 감소)로 대폭 단축됨.
   - `pierce_shot` + `kill_explosion` 시너지 결합 시 Baseline 대비 **4.12배** (클리어 시간 107t → 26t) 클리어 속도 향상이 발생하여 GROK의 WARN 임계값(≥ 4.0배)을 초과함. 수치 정량 분석과 함께 밸런스 조정 권고안을 작성함.

---

## 2. Determinism Audit & Steering/Tiebreak Verification (`Tools/DeterminismAudit`)

### 2.1 Audit Suite Execution Results (`dotnet run --project Tools/DeterminismAudit -- --suite`)

5개 대표 시나리오 및 256개 시드 Cap Boundary 스윕 전체에 대한 동일 시드 2회 연속 수행 해시 검증 결과:

| Scenario Name | Seed Value | Strategy | Stages Cleared | Total Ticks | Reward Choices | Capped Choices | State Hash | Result |
|---|---|---|---|---|---|---|---|---|
| `seed-0-first` | `0` | First | 4/4 | 43,848 | 4 | 1 | `35B5A1A8BC5D9C3B` | **PASS** |
| `seed-1-last` | `1` | Last | 6/6 | 56,512 | 6 | 2 | `D136042F2ADC690F` | **PASS** |
| `seed-12345-rotating` | `12,345` | Rotating | 8/8 | 88,172 | 8 | 4 | `BD9DC0FAA106F706` | **PASS** |
| `seed-deadbeef-rotating` | `0xDEADBEEF` | Rotating | 10/10 | 101,836 | 10 | 5 | `64FA82D914DFD127` | **PASS** |
| `seed-max-prefer-capped` | `ulong.MaxValue` | PreferCapped | 14/14 | 166,244 | 14 | 9 | `7A5FD93EAC5B56FA` | **PASS** |

- **Capped Boundary Sweep**:
  - Scanned Seeds: **256** / Qualifying Seeds: **256**
  - Stage 2 Battle Hash: **Matched** / Stage 3 Battle Hash: **Matched** / Stage 3 Reward Options: **Matched**
  - **Verdict**: **AUDIT PASS**

### 2.2 Steering & Tiebreak Mechanism Analysis

1. **`homing_missile` Steering Stability**:
   - `BattleSim.cs` 내 유도 조향은 부동 소수점(float) 연산을 배제하고 정수 베이스 LUT(`SineLut`) 및 내적(Dot product) 정수 비례 조향 알고리즘으로 구현됨.
   - 타겟 추적 시 동일 거리 타겟이 다수 존재할 경우 `candidate.Id`가 가장 작은 대상을 고정 선택하여 시드 및 틱 간 난수(RNG) 개입 없이 100% 결정론적 조향을 유지함.
2. **`ricochet` Tiebreak Stability**:
   - 도탄 타겟 탐색 시 `FindNearestTarget` 메서드를 사용하며, 거리 제곱(`SquaredDistanceSaturated`)이 최소인 적을 탐색함.
   - 동률 거리(`distance == bestDistance`) 발생 시 `bestId != 0 && candidate.Id >= bestId` 조건으로 이전 `bestId`를 유지하여, 적 리스트 순서 및 ID 할당 순서에 따른 안정적인 타이브레이크를 보장함.

---

## 3. Rewards Catalog (13 Items) Independent Weight Distribution Recalculation

`GameData/rewards.json` 13종 보상 카탈로그의 단계별 가중치 분포 및 3택 카드 등장 모디파이어 기대값(\(E[\text{mods}]\)) 독립 계산 결과는 다음과 같다.

### 3.1 Catalog Composition Breakdown (13 Items)

| Item ID | Category / Type | Weight | Stage Min-Max | `maxPerRun` | Description |
|---|---|---|---|---|---|
| `capsules_5` | Base / Capsules | 2 | 1-99 | - | 캡슐 5개 획득 |
| `slot_main_shot_1` | Base / SlotLevel | 2 | 1-99 | - | MainShot 레벨업 |
| `slot_missile_1` | Base / SlotLevel | 2 | 1-99 | - | Missile 레벨업 |
| `slot_option_1` | Base / SlotLevel | 2 | 1-99 | - | Option 레벨업 |
| `slot_shield_1` | Base / SlotLevel | 2 | 1-99 | - | Shield 레벨업 |
| `repair_hp_1` | Base / RepairHP | 2 | 1-99 | - | 체력 1 회복 |
| `passive_fire_rate_1` | Passive / FireRateUp | 1 | 2-99 | 3 | 연사속도 증가 |
| `passive_damage_1` | Passive / DamageUp | 1 | 2-99 | 3 | 데미지 증가 |
| `passive_move_speed_1` | Passive / MoveSpeedUp | 1 | 2-99 | 3 | 이동속도 증가 |
| `mod_pierce_shot` | **Modifier / PierceShot** | **2** | **1-99** | **1** | **관통 탄환 시너지** |
| `mod_ricochet` | **Modifier / Ricochet** | **2** | **1-99** | **1** | **도탄 시너지** |
| `mod_homing_missile` | **Modifier / HomingMissile** | **2** | **1-99** | **1** | **유도 미사일 시너지** |
| `mod_kill_explosion` | **Modifier / KillExplosion** | **2** | **1-99** | **1** | **처치 폭발 시너지** |

### 3.2 Stage-dependent Weight & Expected Value Calculation

#### 1) Stage 1 (패시브 3종 미등장):
- **Base (6종)**: \(6 \times 2 = 12\)
- **Modifiers (4종)**: \(4 \times 2 = 8\)
- **Passives (3종)**: \(0\) (\(\text{stageIndexMin} = 2\))
- **Total Weight**: \(12 + 8 = 20\)
- **Modifier Weight Ratio**: \(8 / 20 = 0.40\) (40.0%)
- **Expected Modifiers in 3-pick offer**:
  \[
  E[\text{mods}] = 3 \times \frac{8}{20} = \mathbf{1.20}
  \]

#### 2) Stage 2+ (초기 미획득 상태):
- **Base (6종)**: \(6 \times 2 = 12\)
- **Modifiers (4종)**: \(4 \times 2 = 8\)
- **Passives (3종)**: \(3 \times 1 = 3\)
- **Total Weight**: \(12 + 8 + 3 = 23\)
- **Modifier Weight Ratio**: \(8 / 23 \approx 0.3478\) (34.78%)
- **Expected Modifiers in 3-pick offer**:
  \[
  E[\text{mods}] = 3 \times \frac{8}{23} \approx \mathbf{1.04}
  \]

#### 3) 모디파이어 획득 개수에 따른 기대값 쇠퇴 표 (Stage 2+ 기준):

| Acquired Modifiers | Remaining Mod Weight | Total Pool Weight | Mod Weight Ratio | \(E[\text{mods in 3-pick}]\) | Status |
|---|---|---|---|---|---|
| **0개** | 8 | 23 | 34.78% | **1.04개** | 가이드 밴드 [0.5, 1.8] 충족 |
| **1개** | 6 | 21 | 28.57% | **0.86개** | 가이드 밴드 [0.5, 1.8] 충족 |
| **2개** | 4 | 19 | 21.05% | **0.63개** | 가이드 밴드 [0.5, 1.8] 충족 |
| **3개** | 2 | 17 | 11.76% | **0.35개** | 선택지 희소화 |
| **4개 (전종)** | 0 | 15 | 0.00% | **0.00개** | `maxPerRun: 1` 캡 적용으로 풀 제외 |

- **검증 결론**: GROK의 *"3택 제시 카드 중 평균 1개꼴로 모디파이어가 등장한다"*는 주장이 독립 수학 검산으로 100% 입증됨.

---

## 4. Quantified Analysis of `kill_explosion` Swarm DPS & GROK WARN

### 4.1 Test Environment & Simulation Data (`Tools/BalanceSim`)

바이오 하이브(Hive) 테마의 밀집 스웜 적(`seg_hive_spore_cloud` 및 스웜 파동 패턴)을 모사하기 위해, 12마리의 low-HP (HP=1) 적이 0.5 world-unit (128 sub-units) 간격으로 밀집 배치된 환경에서 모디파이어별 처치 시간(Ticks-to-clear)을 측정함.

- **기본 사양**: `packSize = 12`, `enemyHp = 1`, `spacing = 0.5u`
- **Core 기본 파라미터**: `KillExplosionRadius = 2.0u`, `KillExplosionDamage = 2`

| Scenario / Modifier | Clear Ticks | Total Kills | Shots Hit | Kills / Sec | Clear Speed Ratio vs Baseline | Status / GROK Flag |
|---|---|---|---|---|---|---|
| **Baseline (`None`)** | 107 t | 12 | 12 | ~6.7 | **1.00×** (기준) | Normal |
| **`pierce_shot` 단독** | 59 t | 12 | 12 | ~12.2 | **1.81×** | Normal |
| **`kill_explosion` 단독** | **34 t** | 12 | **3** | **~21.2** | **3.15×** | **Swarm Ultra-Efficient** |
| **`pierce` + `kill_explosion`** | **26 t** | 12 | **3** | **~27.7** | **4.12×** | **WARN (Runaway DPS Triggered)** |

### 4.2 Numerical Quantification of GROK's WARN

1. **사격 효율성 폭발적 증가 (75% 사격 감소)**:
   - Baseline 사격 시 12발의 피격(Shots Hit)이 필요했으나, `kill_explosion` 단독 적용 시 **단 3발의 사격**으로 12마리 밀집 스웜 전체가 소멸함.
2. **단독 모디파이어 3.15배 스피드업**:
   - `kill_explosion` 단독으로도 클리어 시간이 107t에서 34t로 감소하여 **3.15배(215% DPS 향상 효과)**의 과도한 성능을 발휘함. 이는 스웜 적(HP 1~2)이 폭발 데미지(Damage = 2, Radius = 2.0u) 1회에 즉사하면서 인접 4~5마리 적에게 체인 스플래시를 일으키기 때문임.
3. **시너지 결합 시 4.12배 Runaway 달성**:
   - `pierce_shot`과 결합 시 관통 탄환이 적 대열의 전반부와 후반부를 동시에 타격하면서 복수의 폭발이 한 틱에 동시 발생함.
   - 클리어 시간이 26t로 줄어들며 Baseline 대비 **4.12배**의 속도 향상을 기록하여, GROK WARN 임계치(Ratio \(\ge 4.0\))를 초과함.

### 4.3 QA Balance Recommendations (권고안)

`kill_explosion`의 스웜 씬 무력화 방지 및 적정 밸런스 유지를 위한 QA 권고 조치:

1. **기본 폭발 데미지 조정 (Damage 2 → 1)**:
   - `KillExplosionDamage` 기본값을 2에서 1로 하향 조정하여, 2 HP 스웜 적이 폭발 1회에 즉사하지 않고 체력 감쇄(Softening) 효과를 받도록 유도.
2. **폭발 반경 미세 조정 (Radius 2.0u → 1.2u~1.5u)**:
   - `KillExplosionRadiusSubUnits`를 2.0 world units에서 1.2u ~ 1.5u 범위로 축소하여 좁은 밀집 대형에만 스플래시가 미치도록 제한.
3. **최대 타겟 수 제한 (Target Cap)**:
   - 1회 폭발 시 피해를 입히는 최대 적 수(예: 최대 4마리)를 지정하여, 극단적인 밀집 스웜에서 화면 전체가 1발로 지워지는 폭주 현상 방지.

---

## 5. Conclusion & Commit Instructions

- **Final Verdict**: **PASS (조건부 밸런스 조정 권고)**
- 본 검증을 통해 결정론 100% PASS, 보상 가중치 분포 1.04~1.20개 입증, `kill_explosion` 수치 정량화 및 권고 제시가 모두 완수되었습니다.
- 완료 기준에 따라 본 보고서를 `QA/reports/2026-07-29-modifier-verification-01.md` 경로에 작성하고 커밋을 진행합니다.
