# 밸런스 교차 검산 1회차 리포트

**문서 ID:** QA-REP-20260729-01  
**작성일:** 2026-07-29  
**작성자:** GEMINI (QA/검증 담당)  
**대상 저장소:** `Roguelike_Scrolling_Shooter` (`wt-qa` worktree, `qa` 브랜치)  
**검증 대상 데이터:** `GameData/enemies.json`, `GameData/waves.json`, `GameData/weapons.json`, `GameData/player.json`, `GameData/rewards.json`  

---

## 1. 개요 및 목적

본 보고서는 **Roguelike_Scrolling_Shooter** 프로젝트의 `GameData/*.json` 원본 데이터 수치를 독립적으로 수집·파싱하고, 기획 공식 및 Core 시뮬레이션 규칙에 기반하여 밸런스 수치를 독자적으로 재계산(Cross-Check)한 1회차 결과 보고서이다.  
GROK의 `BalanceSim` 스크립트를 참조하지 않고 순수 파이썬 파싱 및 이산 수학적 계산을 통해 검증을 수행하였다.

---

## 2. 검증 결과 요약

| 검증 항목 | 판정 | 핵심 발견 사항 |
|---|---|---|
| **(1) Stage 1→5 난이도 단조 증가** | **[이상]** | **Stage 3 → Stage 4 구간에서 적 HP 총량(-13.7%) 및 초당 HP 공급량(-10.7%)이 오히려 감소하는 역전 현상 발견.** |
| **(2) 보스 5종 HP 및 페이즈 분할** | **[정상/의심]** | HP 성장 곡선(1000~2400) 및 페이즈 1→2 탄속/탄수 증가 곡선은 타당하나, **JSON 스키마 내 페이즈 전환 HP 임계값(% 기준) 미비.** |
| **(3) 기본 무기 DPS 대비 TTK 곡선** | **[버그/의심]** | **1) `MissileMinimumFireIntervalTicks` 기본값(30t) 오류로 미사일 레벨업 시 쿨다운 감소 불능 버그 발견.**<br>**2) 풀 파워업 시 DPS가 기본 75.0 → 1880.0 (25.1배)으로 폭증하여 최종 보스 TTK가 1.2초로 붕괴.** |
| **(4) 보상 9종 기대 가치 균형** | **[의심]** | `capsules_5` 대비 특정 무작위 슬롯 직가 레벨업의 가치 불균형 및 체력 풀 상태에서의 `repair_hp_1` 낭비 문제. |

---

## 3. 항목별 상세 검산 결과

### (1) 스테이지 1→5 난이도 단조 증가 검산

스테이지별 테마 및 난이도 계수(Stage 1=Diff 1 ~ Stage 5=Diff 5)에 따른 세그먼트 생성 풀과 평균 적 HP, 등장 밀도(초당 HP 및 적 수)를 파싱하여 검산하였다.

#### 스테이지별 세그먼트 평균 집계 데이터
*각 세그먼트 재생 시간 `lengthTicks / 60` 기준*

| 스테이지 | 테마 (Theme) | 매칭 세그먼트 수 | 세그먼트당 평균 HP | 초당 HP 공급량 (HP/s) | 초당 적 등장 수 (Enemies/s) |
|---|---|---|---|---|---|
| **Stage 1** | Scrapyard | 3개 | 140.0 HP | 13.38 HP/s | 1.09 /s |
| **Stage 2** | Hive | 9개 | 178.2 HP | 15.04 HP/s | 1.28 /s |
| **Stage 3** | Fortress | 10개 | 257.2 HP | 19.72 HP/s | 1.31 /s |
| **Stage 4** | Nebula | 9개 | **221.9 HP (-13.7%)** | **17.61 HP/s (-10.7%)** | 1.36 /s |
| **Stage 5** | Core | 8개 | 393.5 HP | 28.35 HP/s | 1.46 /s |

> [!WARNING]
> **Stage 3 → Stage 4 난이도 역전 (Monotonicity Violation)**
> - **원인 분석:**
>   1. Stage 3(`fortress`) 전용 세그먼트(`seg_fortress_sentry_grid`, `seg_fortress_interceptor_assault`)에 Sentry Drone(HP 22) 및 고체력 미니보스급 적이 집중 배치되어 세그먼트당 HP가 624, 460으로 매우 높음.
>   2. Stage 4(`nebula`) 전용 세그먼트(`seg_nebula_wisp_storm`, `seg_nebula_wisp_ribbon`)는 체력이 5에 불과한 Wisp 계열 초경량 적들 위주로 배치되어 세그먼트당 HP가 396, 213으로 낮음.
>   3. 초기 공용 세그먼트(`seg_sine_pair`, `seg_turret_floor` 등)의 `difficultyMax`가 `5`로 설정되어 있어 Stage 4~5 세그먼트 풀에 계속 섞이면서 평균 난이도를 하향 평준화함.

---

### (2) 보스 5종 HP와 페이즈 분할의 스테이지별 타당성

5개 보스의 HP 성장 및 페이즈 1/2 발사 패턴(탄속, 탄수, 발사 간격)을 검산하였다.

| 보스 ID | 스테이지 / 테마 | HP | 페이즈 1 패턴 (간격 / 발사수 / 초당 탄수 / 탄속) | 페이즈 2 패턴 (간격 / 발사수 / 초당 탄수 / 탄속) |
|---|---|---|---|---|
| `boss_stage1` | St.1 (Scrapyard) | 1,000 | 55t (0.92s) / 3way / 3.27발/s / v=9.0 | 45t (0.75s) / 5way / 6.67발/s / v=10.0 |
| `boss_hive` | St.2 (Hive) | 1,300 (+30%) | 48t (0.80s) / 4way / 5.00발/s / v=9.5 | 40t (0.67s) / 5way / 7.50발/s / v=10.5 |
| `boss_fortress` | St.3 (Fortress) | 1,600 (+23%) | 42t (0.70s) / 5way / 7.14발/s / v=10.0 | 38t (0.63s) / 6way / 9.47발/s / v=11.0 |
| `boss_storm` | St.4 (Nebula) | 1,900 (+18.8%) | 40t (0.67s) / 5way / 7.50발/s / v=11.0 | 36t (0.60s) / 7way / 11.67발/s / v=11.5 |
| `boss_core` | St.5 (Core) | 2,400 (+26.3%) | 38t (0.63s) / 7way / 11.05발/s / v=12.0 | 34t (0.57s) / 9way / 15.88발/s / v=12.5 |

> [!NOTE]
> - **보스 난이도 성장 곡선:** HP(1000→2400)와 페이즈 1/2 탄속(9.0→12.5), 초당 탄막밀도(3.27→15.88발/s) 모두 단조 증가하며 매개변수 곡선이 매우 우수하게 설계됨.
> - **[의심 사항] 페이즈 체인지 임계값 미비:** `waves.json`의 `bosses` 데이터 구조에 Phase 전환 HP 조건(예: HP 50% 이하 진입 등)이 명시되어 있지 않고 단순 배열로 들어있음. C# 엔진 레벨에서의 하드코딩 여부 확인 권장.

---

### (3) 기본 무기 DPS 대비 적 TTK 곡선과 파워업 레벨별 변화

Core 데미지 공식(`Damage.Compute`: Level $L$일 때 Base Dmg $\times (100 + 50(L-1))/100$) 및 쿨다운 공식(`ComputeReducedInterval`)을 기반으로 파워업 단계별 DPS와 주요 적/보스 TTK(Time To Kill, 초)를 계산하였다.

#### 1) 무기 및 파워업 조합별 DPS

*   **MainShot (단일 발사체):**
    *   Lv1: Dmg 10, Interval 8t (7.5발/s) $\rightarrow$ **75.0 DPS**
    *   Lv2: Dmg 15, Interval 8t (7.5발/s) $\rightarrow$ **112.5 DPS**
    *   Lv3: Dmg 20, Interval 7t (8.57발/s) $\rightarrow$ **171.4 DPS**
    *   Lv4: Dmg 25, Interval 6t (10.0발/s) $\rightarrow$ **250.0 DPS**
    *   Lv5: Dmg 30, Interval 5t (12.0발/s) $\rightarrow$ **360.0 DPS**
*   **Option (옵션 동시 발사):**
    *   Option 1개당 MainShot DPS가 100% 추가됨 (본체 + Option $O$개 = $(1+O)$배 DPS).
    *   MainShot Lv5 + Option Lv4 (5발 동시 발사) $\rightarrow$ **1,800.0 DPS**
*   **Missile (미사일):**
    *   Lv1: Dmg 20, Interval 30t $\rightarrow$ **40.0 DPS**
    *   Lv2: Dmg 30, Interval 30t $\rightarrow$ **60.0 DPS**
    *   Lv3: Dmg 40, Interval 30t $\rightarrow$ **80.0 DPS**

> [!CAUTION]
> **[버그 발견] Missile 쿨다운 캡핑 버그 (`MissileMinimumFireIntervalTicks = 30`)**
> `BattleSimConfig` 코드 상에서 `MissileMinimumFireIntervalTicks` 기본값이 `30`으로 지정되어 있어, Missile Lv2(계산치 25t), Lv3(계산치 20t)로 감소하더라도 `Math.Max(30, reduced)`에 의해 **발사 간격이 30t(초당 2발) 아래로 줄어들지 않음!**  
> 최소 발사 간격을 `10t` 등으로 수정해야 Missile Lv3 기준 120.0 DPS가 정상 발휘됨.

#### 2) 빌드 시나리오별 주요 적 및 보스 TTK (Time to Kill, 초)

*   **S1 Baseline:** MainShot Lv1 (75.0 DPS)
*   **S2 Early:** MainShot Lv2 + Option Lv1 (225.0 DPS)
*   **S3 Mid:** MainShot Lv3 + Option Lv2 + Missile Lv1 (554.3 DPS)
*   **S4 Late (현재 버그 상태):** MainShot Lv5 + Option Lv4 + Missile Lv3 (1,880.0 DPS)
*   **S5 Max (미사일 버그 수정 시):** MainShot Lv5 + Option Lv4 + Missile Lv3 (1,920.0 DPS)

| 적 종류 / 보스 | HP | S1 Baseline (초) | S2 Early (초) | S3 Mid (초) | S4 Late (초) | S5 Max (초) |
|---|---|---|---|---|---|---|
| `zako_fast` | 6 | 0.080s | 0.027s | 0.011s | 0.003s | 0.003s |
| `zako_straight` | 10 | 0.133s | 0.044s | 0.018s | 0.005s | 0.005s |
| `zako_tank` | 40 | 0.533s | 0.178s | 0.072s | 0.021s | 0.021s |
| `elite_sine` | 50 | 0.667s | 0.222s | 0.090s | 0.027s | 0.026s |
| `mini_destroyer` | 200 | 2.667s | 0.889s | 0.361s | **0.106s** | **0.104s** |
| `mini_walker` | 250 | 3.333s | 1.111s | 0.451s | **0.133s** | **0.130s** |
| `boss_stage1` | 1,000 | 13.333s | 4.444s | 1.804s | **0.532s** | **0.521s** |
| `boss_core` | 2,400 | 32.000s | 10.667s | 4.330s | **1.277s** | **1.250s** |

> [!WARNING]
> **[밸런스 파괴 위험] 파워업 승계 및 풀업 시 TTK 붕괴**
> 플레이어가 무기를 풀업할 경우 DPS가 **75.0 → 1,880.0 (25.1배)** 으로 급증함.
> 이로 인해 Stage 5 최종 보스(`boss_core`, HP 2,400) 조차 **1.27초** 만에 파괴되며, 스테이지 난이도와 슈팅 게임으로서의 긴장감이 완전히 파괴되는 문제가 존재함.

---

### (4) 보상 9종의 기대 가치 균형

`rewards.json`의 9개 보상 항목 및 확률 분포(Stage 1: 총 가중치 12, Stage 2+: 총 가중치 15)를 분석하였다.

| 보상 ID | 타입 (`type`) | 수량 (`amount`) | 가중치 (`weight`) | Stage 1 출현율 | Stage 2+ 출현율 |
|---|---|---|---|---|---|
| `capsules_5` | capsules | 5 | 2 | 16.67% | 13.33% |
| `slot_main_shot_1` | slotLevel (MainShot) | 1 | 2 | 16.67% | 13.33% |
| `slot_missile_1` | slotLevel (Missile) | 1 | 2 | 16.67% | 13.33% |
| `slot_option_1` | slotLevel (Option) | 1 | 2 | 16.67% | 13.33% |
| `slot_shield_1` | slotLevel (Shield) | 1 | 2 | 16.67% | 13.33% |
| `repair_hp_1` | repairHp | 1 | 2 | 16.67% | 13.33% |
| `passive_fire_rate_1` | fireRateUp | 1 | 1 | - | 6.67% |
| `passive_damage_1` | damageUp | 1 | 1 | - | 6.67% |
| `passive_move_speed_1` | moveSpeedUp | 1 | 1 | - | 6.67% |

> [!NOTE]
> **보상 가치 불균형 분석:**
> 1. **`capsules_5` vs 특정 슬롯 직가 레벨업 (`slot_*_1`):**
>    - `capsules_5`는 원하는 슬롯을 선택하여 1단계 즉시 업그레이드할 수 있는 유연성을 지님.
>    - 무작위 특정 슬롯 1레벨업 보상과 동일한 가중치(Weight 2)를 가지지만, 플레이어 선택 자유도 면에서 `capsules_5`가 월등히 우월함.
> 2. **`repair_hp_1` 체력 풀 상태에서의 낭비:**
>    - 플레이어가 체력 100% 상태일 때 선택지에 `repair_hp_1`이 포함되면 유효 선택지가 2개로 줄어드는 손해 발생.
> 3. **패시브 3종 배율 명시성 미비:**
>    - `passive_fire_rate_1`, `passive_damage_1`, `passive_move_speed_1`의 실제 적용 수치가 JSON 상에서 단순 `amount: 1`로 명시되어 있어, 시뮬레이션 적용 로직(퍼센트 증가 등)과의 명확한 데이터 규격화가 필요함.

---

## 4. 권고사항 및 조치 제안

1. **[수정 필요] Stage 4 (`nebula`) 세그먼트 밸런스 조정:**
   - Stage 4 전용 세그먼트의 적 배치 조율 또는 Wisp 적의 HP/등장 수 조정을 통해 세그먼트당 HP 및 HP/s 공급량이 Stage 3보다 높도록 조정 (Stage 3 257 HP $\rightarrow$ Stage 4 300+ HP 권장).
   - 공용 초기 세그먼트의 `difficultyMax`를 제한(예: Diff 1~3으로 제한)하여 Stage 4~5 세그먼트 풀 하향 평준화 방지.
2. **[버그 수정] `MissileMinimumFireIntervalTicks` 기본값 변경:**
   - `BattleSimConfig` 내 default 값을 `30`에서 `10` (또는 이하)으로 수정하여 미사일 레벨업에 따른 연사 속도 증가 혜택이 정상 작동하도록 개선.
3. **[밸런스 제안] 무기 풀업 DPS 상한선 및 보스 HP 재조정:**
   - Option 동시 발사에 따른 DPS 폭발(최대 25.1배)을 완화하기 위해 Option 당 데미지 감쇄 비율(예: Option 발사체 데미지 50~70% 적용) 적용 제안.
   - Stage 4~5 보스 HP 상향 또는 플레이어 파워업 승계 비율(`CarryFraction`) 재검토.
4. **[데이터 규격] Boss Phase Threshold 및 Passive Amount 표준화:**
   - `waves.json` 내 보스 페이즈 전환 HP 기준 명시 및 `rewards.json` 내 패시브 효과의 명확한 수치 규격화.
