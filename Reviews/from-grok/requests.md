# GROK → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고, 완료 항목은 체크한다.

---

## [ ] REQ-G001 → CODEX: `rewards.json` 파서 + RunManager 풀 교체 (REQ-008 후속)

**무엇이 필요한가**

`GameData/rewards.json`(schemaVersion 1)을 Core가 읽어 `RunManager.GenerateRewardOptions`의 내장 잠정 풀을 대체할 것.

**스키마 (GROK 확정 초안, 2026-07-29)**

| 필드 | 타입 | 의미 |
|---|---|---|
| `schemaVersion` | int | 현재 1 |
| `optionCount` | int | 3택 고정 (Core `RewardOptionCount`와 정합) |
| `rewards[]` | array | 후보 풀 |
| `rewards[].id` | string | 고유 id |
| `rewards[].type` | string | `capsules` \| `slotLevel` \| `repairHp` (→ `RewardType`) |
| `rewards[].slot` | string? | `slotLevel`일 때만 필수. `MainShot`/`Missile`/`Option`/`Shield` |
| `rewards[].amount` | int | 캡슐 횟수 / 슬롯 레벨 증가 / max HP 증가 |
| `rewards[].weight` | int | 가중치 (≥1). 현재 풀은 전부 1 = 균등 |
| `rewards[].stageIndexMin/Max` | int | 해당 스테이지 클리어 시에만 후보 포함 |

**선택 알고리즘 제안 (결정론 유지)**

1. `stageIndex`로 풀 필터 → 가중치 합 검증
2. `Rng.Fork(RewardSelectionStream).Fork(stageIndex)`로 **비복원 가중 샘플** `optionCount`회
3. 후보 수 < `optionCount`이면 파서/런타임 에러 (카탈로그 무결성)

현재 JSON 6종·weight 1·stage 1–99는 sim 브랜치 내장 풀과 **결과 분포가 동일**하도록 맞춤 (균등 비복원).

**왜**

REQ-007이 `rewards.json 연동 예정`으로 내장 풀을 남겼다. 원본은 GameData (AGENTS.md §5). Presentation/밸런스 시뮬이 같은 파일을 읽게 하려면 파서가 선행돼야 한다.

**제안 시그니처 (초안 — CODEX 재량)**

```csharp
// GameDataSet 또는 별도 RewardCatalog
public sealed class RewardCatalog
{
    public int OptionCount { get; }
    public IReadOnlyList<RewardDefinition> All { get; }
    public IReadOnlyList<RewardDefinition> EligibleForStage(int stageIndex);
}

public readonly struct RewardDefinition
{
    public string Id { get; }
    public RewardType Type { get; }
    public PowerUpSlot Slot { get; }  // type != SlotLevel 이면 무시
    public int Amount { get; }
    public int Weight { get; }
    public int StageIndexMin { get; }
    public int StageIndexMax { get; }
}
```

`GameDataParser.Parse` 시그니처에 `rewardsJson` 인자 추가, 또는 선택적 오버로드. 기존 3인자 경로를 깨지 않으려면 rewards 미주입 시 내장 폴백을 잠시 유지해도 된다 (제거 시점은 CODEX 판단).

---

## 밸런스 검토 기록 (2026-07-29) — 재스케일 + boss_stage1

**범위:** REQ-006 재스케일 수치 (`player`/`weapons`/`enemies`/`waves` 세그먼트) + REQ-008 part1 `boss_stage1` 페이즈.  
**조치:** 수치 **변경 없음** (AGENTS.md §7). 아래는 사람 밸런스 패스용 **우려·제안만**.

### A. 플레이필드 재스케일 (×5/3 속도·거리, Y×1.6, 히트박스×1.5)

| # | 우려 | 근거 | 제안 (확정 금지) |
|---|---|---|---|
| A1 | **체감 속도 검증 미완** | 기계적 환산으로 player 8→13, scroll 3→5, main shot 12→20. 화면 횡단 시간은 유지 설계이나 반올림 잔여(4.25/8.25/3.25 등)와 히트박스 확대가 겹치면 "넓어진 화면에서 더 바빠진" 느낌이 날 수 있음. | 플레이 패스 후 속도만 일괄 ±10% 후보를 시뮬로 비교. 개별 적 속도 손대기는 후순위. |
| A2 | **히트박스 ×1.5 vs 플레이필드 비대칭 확대** | 필드 halfW 20u(구 대비 ×5/3≈1.67), halfH 11.25(×1.6). 플레이어 hitbox 0.25→0.375(×1.5). 피격 면적 증가율이 필드 확대율보다 약간 큼 → 탄 회피 여유가 소폭 줄 수 있음. | 보스/터렛 탄 밀도 체감 후 hitbox 0.35 등 미세 하향 후보. |
| A3 | **스폰 X=21 vs 뷰 우측 20** | 스폰이 뷰 밖 +1u. 고속 `zako_fast`(8.25 u/s)는 등장 인지 시간이 짧음. | 스웜 세그먼트만 spawn 틱을 앞당기거나 fast 속도를 7.5 후보로. |
| A4 | **사인 진폭 + 스폰 Y 합** | 예: `zako_sine_slow` y=±5.5 amp 3.25 → 피크 ≈±8.75 (halfH 11.25 안). 당장은 이탈 없음. 추가 진폭/레인 확장 시 클램프·이탈 위험. | 신규 세그먼트 작성 시 `\|y\|+amplitude < halfH − halfHeight` 체크리스트. |

### B. 웨이브 밀도·드롭 (확장 카탈로그 유지)

| # | 우려 | 근거 | 제안 |
|---|---|---|---|
| B1 | **스웜 드롭 과다** | `zako_fast` dropWeight 3, `noDropWeight` 8 → 대략 3/11 ≈ 27%/킬. `seg_swarm_fast` 18기면 기대 캡슐 ≈5. 스테이지 3세그먼트 누적 시 게이지 과공급 가능. | fast `dropWeight` 2 또는 swarm 스폰 수 삭감. 관측 포인트는 기존 from-grok 기록과 동일. |
| B2 | **difficulty 1 풀이 얇음** | intro / sine_pair / sine_rush 3종만으로 `segmentsPerStage=3` → 조합 다양성 낮음, 초반 반복 체감. | diff1 전용 세그먼트 1–2 추가(밀도는 낮게) 또는 intro 변형. |
| B3 | **sandwich + elite (diff 3+)** | 상하 포탑 + elite_sine(hp 50, contact 2, drop 12). 초중반 파워 부족 시 벽. | sandwich `difficultyMin` 4, 또는 elite hp 40 후보. |
| B4 | **contactDamage 2의 의미** | 기본 `PlayerMaxHp=1`(Core/GameData 미기재)이면 contact 1·2 모두 즉사. tank/elite contact 2는 max HP>1(수리 보상·향후 체력 확장) 전에는 차별 신호가 안 됨. | 플레이어 기본 HP를 GameData로 승격·2+로 둘지 사람 결정 후 contact 곡선 재검토. |

### C. boss_stage1 페이즈 전투 (waves.json)

현재 값: `hp 500`, hitbox `4×3u`, `holdX 14`,  
phase0 `{55t, 3-way, 9 u/s}`, phase1 `{35t, 5-way, 11 u/s}` (HP 균등 분할 — Core equal-split).

| # | 우려 | 근거 (대략 계산, 잠정) | 제안 |
|---|---|---|---|
| C1 | **TTK이 짧은 편** | main_shot base 10, interval 8t, level 0도 `Damage.Compute(..., max(1,level))` → 10 dmg. 이론 DPS ≈ 10×(60/8)=75. 풀히트 가정 TTK ≈ 500/75 ≈ **6.7s**. 옵션 레벨·미사일 시 더 짧음. | hp 800–1200 후보, 또는 페이즈별 무적/장갑 구간. "보스전 연출 길이" 목표 초를 사람이 먼저 정할 것. |
| C2 | **페이즈2 탄막 vs HP1** | phase2: 5발/35t ≈ 8.6발/초, 조준 부채꼴(슬롯 간격 11.25°). `PlayerMaxHp=1`이면 실드 없이 한 발 = 사망. 짧은 TTK와 맞물려 "딜레이스 or 즉사" 이분법. | (a) 기본 HP 상향, (b) phase2 interval 45–50, (c) ways 4, (d) 탄속 9 유지 중 택. |
| C3 | **전 스테이지 동일 보스** | stageIndex 1–99·diff 1–5 동일 hp/페이즈. 후반 파워(슬롯 보상·CarryFraction) 누적 시 보스가 허수아비가 됨. | 단기: hp를 stage/diff 배율 테이블로(스키마 확장). 중기: M3 보스 로테이션. |
| C4 | **holdX=14 / 대형 히트박스** | 필드 우측(halfW 20)에서 4u 반폭 → 좌측 끝 10u까지 몸체. 플레이어 스폰 −13에서 사거리·자리잡기는 여유, 회피 코리도(보스 좌측)는 좁아질 수 있음. | holdX 15–16 또는 halfWidth 3.5 후보 — 스프라이트 실측 후. |
| C5 | **페이즈 경계 = HP 50%만** | 2페이즈 equal-split은 구현 단순. "광폭화" 체감이 탄 간격·ways 점프에만 의존. | 추후 hpRatio 배열 스키마(예: [0.6, 0.25])로 전환 여지 — REQ-008 요청1 원문과 정합. 지금은 Core equal-split에 맞춤. |
| C6 | **보스 탄 vs 플레이어 탄속** | 보스 9–11 u/s, 플레이어 본탄 20 u/s. 접근 전투 시 반격 창은 넓음. 난이도는 탄 **밀도·조준**이 지배. | 탄속보다 interval/ways 조정이 우선. |

### D. 보상 풀 (`rewards.json` 신설분, 수치 잠정)

Core 내장과 동일: 캡슐×3 / 4슬롯 각 +1 / 선체 maxHP +1, weight 균등, 3택.

| # | 우려 | 근거 | 제안 |
|---|---|---|---|
| D1 | **capsules_3 체감 약함** | `Collect()`×3은 커서만 3칸 이동. 스테이지 클리어 보상으로 슬롯 +1·maxHP +1 대비 가치 불균형. | 캡슐 보상 제거, amount 상향+자동 활성화 없음 명시 UI, 또는 "랜덤 슬롯 +1"로 교체. |
| D2 | **repairHp = maxHP 영구 증가** | Core `ApplyReward`가 `_battleConfig.PlayerMaxHp += amount` 후 다음 스테이지부터 적용. 기본 1에서 스테이지마다 +1 가능하면 후반 난이도 붕괴(특히 CarryFraction=1.0과 겹침). | 스테이지 상한·weight 하향·후반 stageIndexMin 제한, 또는 "현재 HP만 회복" 타입 분리. |
| D3 | **슬롯 +1 ×4 비중 2/3** | 6후보 중 4가 슬롯. 3택 비복원 시 슬롯 보상이 거의 항상 1개 이상. 의도적일 수 있으나 빌드 편중(MainShot 선호) 가능. | 슬롯 weight를 후반 차등, 또는 이미 max인 슬롯 제외 로직(CODEX). |
| D4 | **스테이지 제한 미사용** | 전원 1–99. 초반 repair/후반 고티어 보상 곡선 없음. | stage 4+ 전용 보상, stage 1 전용 약한 풀 등 구간 설계는 사람 지시 후. |

### E. 사람 결정 대기 (AGENTS.md §7 — 에이전트 변경 금지)

- `MetaProgression.CarryFraction` (기본 1.0) + 스테이지 보상 슬롯 승급 → 런 간 파워 인플레
- `PowerUpGauge` 슬롯 최대 5/3/4/3
- 적 HP·contact·드롭, 보스 hp/페이즈, 무기 baseDamage·interval
- 플레이어 기본 max HP (현재 Core 기본 1, GameData 미승격)

### 권장 밸런스 시뮬 시나리오 (후속 GROK 작업 후보)

1. seed 고정 × stage 1 보스만: 무파워 / 실드1 / 풀파워 TTK·피격 횟수  
2. stage 1→5 보상 3택 랜덤 선택 정책(항상 슬롯 / 항상 repair) 후 보스 TTK 추이  
3. swarm 세그먼트 단독 기대 캡슐 수 vs noDropWeight 민감도  

(스크립트 추가 시 `Tools/` 아래 content 소유 경로에 두고 CoreStandalone 참조.)

---

## 콘텐츠 확장 기록 (2026-07-28) — 스테이지 썰렁 피드백

플레이 피드백: 스테이지가 썰렁하다. `enemies.json` / `waves.json` 카탈로그 확장. **스키마 형식 변경 없음.** 아래 수치는 전부 **잠정값**이며 손맛·밸런스 최종 확정은 사람 결정 (AGENTS.md §7).

### enemies.json — 추가 5종 + dropWeight 정비

| id | movePattern | hp | moveSpeed | dropWeight | 의도 |
|---|---|---|---|---|---|
| `zako_straight` (기존) | straight | 10 | 3.0 | **4** (was 3) | 기본 잡졸. 드롭 체감 소폭 상향. |
| `zako_sine` (기존) | sine | 10 | 2.5 | **5** (was 3) | 사인 잡졸. 회피 부담 대비 드롭 우대. |
| `turret_ground` (기존) | static | 30 | 0 | **2** (was 1) | 지상 포탑. 저드롭 유지하되 0에 가깝지 않게. |
| `zako_fast` **NEW** | straight | 6 | 5.0 | 3 | 고속 저체력 스웜. 밀도 담당, 개체당 드롭은 낮음. |
| `zako_tank` **NEW** | straight | 40 | 1.5 | 7 | 저속 고기동 탱커. 킬 보상형 드롭. |
| `zako_sine_slow` **NEW** | sine | 18 | 1.8 | 6 | 느린 사인. 화면 점유·압박. |
| `turret_ceiling` **NEW** | static | 30 | 0 | 2 | 천장 포탑. ground 대칭. |
| `elite_sine` **NEW** | sine | 50 | 2.0 | 12 | 엘리트. 고 dropWeight로 캡슐 하이라이트. fireInterval 120 잠정. |

**dropWeight 설계 메모 (잠정):** 상대 가중치만 의미 있음. 잡졸 4–5 / 스웜 3 / 포탑 2 / 탱커·슬로사인 6–7 / 엘리트 12. 절대 드롭 확률 공식은 Core 드롭 구현에 따름 — 체감 과다/과소 시 스케일 일괄 조정 권장.

`contactDamage` / `scoreValue` / `fireIntervalTicks` 도 잠정. 엘리트·탱커 contactDamage=2는 위험 신호용 플레이스홀더.

### waves.json — 세그먼트 3 → 8종, 밀도 상향

`laneCount=3`, `segmentsPerStage=3`, `startLaneMask=2`, 보스 메타 유지. **모든 세그먼트 `entryLaneMask=7`, `exitLaneMask=7`** → difficulty 1–5에서 `segmentsPerStage=3` 조립·보스 진입 가능 (기존 클리어 가능성 전략 유지).

| 세그먼트 | diff | lengthTicks | traversable | 밀도 성격 | 의도 |
|---|---|---|---|---|---|
| `seg_intro_line` | 1–3 | 600 | `[7]` | 중 (10 spawns) | 입문 직선. y 분산으로 전 레인 사용감. |
| `seg_sine_pair` | 1–5 | 600 | `[2]` | 중–고 (10) | 상하 사인 + slow. center 코리도. |
| `seg_turret_floor` | 2–5 | 900 | `[6]` | 중 (11) | 바닥 포탑 + 상부 잡졸/탱커. top\|center. |
| `seg_swarm_fast` **NEW** | 2–5 | 600 | `[7]` | **고** (18) | 고속 스웜 폭주. 전 레인 개방. |
| `seg_mixed_mid` **NEW** | 2–5 | 720 | `[7]` | 중–고 (14) | straight/sine/fast/tank 혼합 샘플. |
| `seg_turret_ceiling` **NEW** | 2–5 | 900 | `[3]` | 중 (11) | 천장 포탑. bottom\|center. floor 대칭. |
| `seg_sandwich` **NEW** | 3–5 | 840 | `[2]` | 고 (17) | 상하 포탑 + 중앙 압박 + elite 피날레. |
| `seg_sine_rush` **NEW** | 1–4 | 660 | `[6]` | 중–고 (14) | 사인 연속. floor 회피 메타(bottom 제외). |

**difficulty 1 풀:** intro / sine_pair / sine_rush 만 → 3세그먼트 조립 가능.  
**difficulty 2:** sandwich 제외 대부분.  
**difficulty 3–5:** sandwich 포함 풀 카탈로그.

### 잠정값 일람 (확정 금지 — 사람 지시 전 유지)

- 신규 적 HP / speed / dropWeight / contactDamage / score / fireInterval
- 기존 적 dropWeight 변경 (3→4, 3→5, 1→2)
- 전 세그먼트 spawn tick·y·lengthTicks·밀도
- 보스 `hp: 500` 미변경 (기존 플레이스홀더)

### 후속 관찰 포인트 (밸런스 시뮬 / 플레이)

1. 스웜 세그먼트에서 드롭이 과다해지면 `zako_fast.dropWeight` 또는 스폰 수를 먼저 깎을 것.
2. sandwich + elite가 difficulty 3+에서 과도하면 `difficultyMin` 4로 올리거나 elite HP 하향.
3. `segmentsPerStage`는 3 유지 — 카탈로그 다양성으로 반복 체감만 완화. 스테이지 절대 길이가 짧으면 상수 상향은 별도 결정.
4. Core/Presentation이 `movePattern` 문자열을 아직 전부 소비하지 않을 수 있음 — 데이터는 스키마 그대로 준비. 미구현 패턴 시 CLAUDE/CODEX 연동 필요.

### 다른 에이전트 요청

(2026-07-29 갱신: 상단 REQ-G001 참고.)
