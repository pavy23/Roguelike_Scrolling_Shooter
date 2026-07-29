# REQ-034 무기 확장 1단계 — 미사일 계열·옵션 포메이션 설계

**작성:** GROK (content) · 2026-07-30  
**상태:** 사전 설계 전용. **GameData 미수정** (CODEX 파서 선행 대기).  
**전부 잠정 (AGENTS.md §7)** — 사람 플레이 피드백 전 최종 확정 금지.  
**원 요청:** `main` `Reviews/from-claude/requests.md` REQ-034  
**후속 적용:** CODEX 스키마/런타임 후 `GameData/weapons.json` · `rewards.json` 반영 + BalanceSim 조합 검산.

---

## 0. 설계 계약 (REQ-034 요약)

| 축 | 내용 |
|---|---|
| 미사일 계열 3 | `straight` / `spread_bomb` / `piercing_lance` |
| 옵션 포메이션 3 | `trail` / `fixed` / `orbit` |
| 직교성 | 계열·포메이션은 **보상 교체**. 슬롯 레벨(Missile 0–3 / Option 0–4)과 독립 |
| 곱셈 관계 | `homing_missile` 모디파이어 = **현재 계열에 유도 부여** (계열 × 호밍) |
| 기본 로드아웃 | 런 시작: Missile=`straight`, Option=`trail` (현행 체감 유지) |
| 범위 밖 | 주무기 wave/burst 및 런 중 주무기 교체 (2단계 예고) |

**기준선 (현 `weapons.json` 미사일, 잠정):**

| 필드 | 값 |
|---|---:|
| `baseDamage` | 20 |
| `fireIntervalTicks` | 30 |
| `minimumFireIntervalTicks` | 15 |
| `projectileSpeed` | 10 |
| maxLevel | 3 |

**공식 (Core 고정):**

- 대미지: `Damage.Compute` = `base × (100 + 50×(L−1)) / 100` → L1=1.0×, L2=1.5×, L3=2.0×  
- 연사: `ComputeReducedInterval` — RapidFireStartLevel=**2**, ReductionPerLevel=**5**(계열별 예외 아래), Min 적용  
- 틱: 60 TPS. ST DPS = `dmg × 60 / interval`

**밸런스 목표:**

1. **L1 단타(ST) 이론 DPS ≈ 40 ± 8** (현 미사일 기준선 40).  
2. **L3 ST 이론 DPS ≈ 100–120** 대역으로 정렬 (총 DPS 비슷).  
3. 계열마다 **최적인 상황**이 갈리게: 잡졸 라인 / 밀집·장애물 / 보스.  
4. 모디파이어 4종과 곱했을 때 **초선형 폭주**를 수치·규칙으로 사전 봉쇄.

---

## 1. 미사일 계열 3종 — 수치 설계

### 1.1 역할 한 줄

| id | 역할 | 최적인 상황 | 약한 상황 |
|---|---|---|---|
| `straight` | 기준 직진. 안정 연사·단타 신뢰 | **잡졸 수평 라인**, 지속 화력, 범용 | 밀집 팩(1기씩), 기동 보스(빗나감) |
| `spread_bomb` | 하강 투하 → 착탄/명중 폭발 | **밀집 팩·breakable 장애물·하방 군집** | 단일 보스 코어(폭발 낭비), 상단 레인만 있는 편성 |
| `piercing_lance` | 저연사·고대미지·관통 | **보스/엘리트 단타**, 종대 정렬 적 | 산개 잡졸(미스 비용 큼), 초근접 스웜 |

id는 REQ-034의 `spread_bomb`을 권위로 둔다 (하강 투하 거동 = 통칭 dive bomb).

### 1.2 계열별 잠정 수치

공통 레벨 규칙 (계열 공통, 명시 예외 없음):

- `rapidFireStartLevel`: **2**  
- `fireIntervalReductionPerLevel`: 계열 표 참고  
- `maxLevel`: **3** (슬롯 상한 불변, §7)

#### A. `straight` — 기준 (현행에 가깝게)

| 필드 | 잠정 값 | 근거 |
|---|---:|---|
| `baseDamage` | **20** | 현행 유지 |
| `fireIntervalTicks` | **30** | 현행. 2.0발/s |
| `minimumFireIntervalTicks` | **15** | REQ-011 정합. L3 실연사 확보 |
| `fireIntervalReductionPerLevel` | **5** | L1=30, L2=25, L3=20 |
| `projectileSpeed` | **10** | 현행 월드유닛/초 |
| `fallSpeedY` | **1.5** | 아주 약한 하강(그라디우스식 “살짝 처짐”). 순수 수평도 가능 — CODEX 판단 |
| `pierceEnemyCount` | **0** | 1히트 소멸 |
| `explosionDamage` | **0** | 없음 |
| `explosionRadius` | **0** | — |
| `explosionMaxTargets` | **0** | — |
| 히트박스 (참고) | 0.47×0.28 | 현 `projectileHalfWidth/Height` 유지 |

**ST DPS**

| Lv | dmg | interval | ST DPS |
|---:|---:|---:|---:|
| 1 | 20 | 30 | **40.0** |
| 2 | 30 | 25 | **72.0** |
| 3 | 40 | 20 | **120.0** |

#### B. `spread_bomb` — 투하 폭발

| 필드 | 잠정 값 | 근거 |
|---|---:|---|
| `baseDamage` (직격) | **12** | 직격은 보조. 본전은 폭발 |
| `explosionDamage` | **16** | 직격+폭발=28 @L1 → ST≈40 (아래) |
| `explosionRadius` | **1.75** u | 밀집 spacing 0.5–1.0u 팩 3–5기 커버. 전 화면 삭제 방지 |
| `explosionMaxTargets` | **5** | 상한. 폭주 1차 캡 |
| `fireIntervalTicks` | **42** | 직진보다 느린 “투하 리듬” |
| `minimumFireIntervalTicks` | **28** | L3도 투하 무게 유지 |
| `fireIntervalReductionPerLevel` | **5** | L1=42, L2=37, L3=32 |
| `projectileSpeed` (X) | **6** | 느린 수평. 조준 여유 |
| `fallSpeedY` | **9** | 뚜렷한 하강 (투하 정체성) |
| `pierceEnemyCount` | **0** | 첫 적/지면/장애물에서 폭발 후 소멸 |
| 폭발 트리거 | 적 명중 **또는** solid/breakable 장애물 충돌 **또는** 하단 경계 | 장애물 파밍·통로 개척 지원 |
| 연쇄 | **폭발 대미지로 죽은 적은 추가 폭발 없음** (계열 자체 비연쇄). `kill_explosion` 연동은 §3 |

**대미지 스케일:** 직격·폭발 **둘 다** `Damage.Compute(base, missileLevel)` 적용 (동일 레벨 배수).

**ST 유효 DPS** (직격+폭발이 동일 대상에 모두 적용된다고 가정 — 착탄/명중 시 권장 규칙):

| Lv | direct | boom | sum | interval | ST DPS |
|---:|---:|---:|---:|---:|---:|
| 1 | 12 | 16 | 28 | 42 | **40.0** |
| 2 | 18 | 24 | 42 | 37 | **68.1** |
| 3 | 24 | 32 | 56 | 32 | **105.0** |

**밀집 팩 프록시 (HP1×5, 반경 내, L1):**  
직격 1 + 폭발 최대 5타 = 이론 피해 12+16×5=**92** / 발 → 직진 20 대비 **~4.6×** 청산력.  
ST는 동급, **상황 이득은 순수 다수 타깃**에서만 나온다.

**장애물:** breakable에 폭발 대미지 적용(권고). solid는 탄 소멸+폭발만(통과 없음).

#### C. `piercing_lance` — 관통 창

| 필드 | 잠정 값 | 근거 |
|---|---:|---|
| `baseDamage` | **40** | 단타 프리미엄(~20%). 보스 청크 타격감 |
| `fireIntervalTicks` | **54** | 0.9s. “한 방” 리듬 |
| `minimumFireIntervalTicks` | **36** | 연사 캡. 랜스 정체성 유지 |
| `fireIntervalReductionPerLevel` | **6** | L1=54, L2=48, L3=42 |
| `projectileSpeed` | **16** | 빠른 직진. 명중창 확보 |
| `fallSpeedY` | **0** | 순수 수평 관통 |
| `pierceEnemyCount` | **2** | **첫 타 포함 총 3히트** (laser `LaserPierceEnemyCount` 의미와 동일: “첫 히트 후 추가 통과 수”) |
| `explosionDamage` | **0** | 없음 |
| 동일 적 재히트 | **금지** | 관통 탄 공통 규칙 |
| 히트박스 (권고) | 0.35×0.20 | 가늘고 긴 실루엣. CLAUDE 뷰 |

**ST DPS**

| Lv | dmg | interval | ST DPS |
|---:|---:|---:|---:|
| 1 | 40 | 54 | **44.4** |
| 2 | 60 | 48 | **75.0** |
| 3 | 80 | 42 | **114.3** |

**정렬 3기 관통 시 유효 (L1):** 40×3 ×60/54 = **133.3** — 직진 40 대비 라인 클리어 우세.  
**보스 1기:** ST 44–114로 직진(40–120)과 유사 대역, **히트당 2×**라 페이즈 경계·고HP에서 체감 우세. 빗나가면 공백이 길다 → 포지셔닝/호밍 보상.

### 1.3 삼계열 ST DPS 정렬 요약

| 계열 | L1 ST | L3 ST | 특수 배수 (이상적 상황) |
|---|---:|---:|---|
| `straight` | 40.0 | 120.0 | ≈1× (신뢰 연사) |
| `spread_bomb` | 40.0 | 105.0 | 밀집 5팩 L1 ≈ **~4–5×** 청산 |
| `piercing_lance` | 44.4 | 114.3 | 3줄 관통 L1 ≈ **3×** |

총 ST DPS는 의도적으로 비슷하다. **빌드 가치 = 상황 계수**이지 절대 화력 인플레이션이 아니다.

### 1.4 스키마 제안 (CODEX / 파서 후 GROK 적용)

`weapons.json` 확장 초안 (적용 금지 — 문서 전용):

```json
{
  "schemaVersion": 3,
  "weapons": [ "... 기존 main/missile/option/shield 슬롯 메타 유지 ..." ],
  "missileFamilies": [
    {
      "id": "straight",
      "baseDamage": 20,
      "fireIntervalTicks": 30,
      "minimumFireIntervalTicks": 15,
      "fireIntervalReductionPerLevel": 5,
      "projectileSpeed": 10,
      "fallSpeedY": 1.5,
      "pierceEnemyCount": 0,
      "explosionDamage": 0,
      "explosionRadius": 0,
      "explosionMaxTargets": 0
    },
    {
      "id": "spread_bomb",
      "baseDamage": 12,
      "fireIntervalTicks": 42,
      "minimumFireIntervalTicks": 28,
      "fireIntervalReductionPerLevel": 5,
      "projectileSpeed": 6,
      "fallSpeedY": 9,
      "pierceEnemyCount": 0,
      "explosionDamage": 16,
      "explosionRadius": 1.75,
      "explosionMaxTargets": 5
    },
    {
      "id": "piercing_lance",
      "baseDamage": 40,
      "fireIntervalTicks": 54,
      "minimumFireIntervalTicks": 36,
      "fireIntervalReductionPerLevel": 6,
      "projectileSpeed": 16,
      "fallSpeedY": 0,
      "pierceEnemyCount": 2,
      "explosionDamage": 0,
      "explosionRadius": 0,
      "explosionMaxTargets": 0
    }
  ],
  "defaultMissileFamily": "straight"
}
```

- 기존 `weapons[]`의 `id: missile` 슬롯 엔트리는 **maxLevel / 공통 메타** 유지.  
- 계열 수치는 `missileFamilies[]`가 권위.  
- 하위 호환: families 부재 시 현행 단일 미사일 = `straight`.

---

## 2. 옵션 포메이션 3종 — 성격·수치

옵션은 계속 **메인샷 미러 발사** (대미지 = 메인 계열·레벨). 포메이션은 **위치만** 바꾼다.  
개수 = Option 슬롯 레벨 (1–4). 포메이션 교체는 개수와 직교.

### 2.1 `trail` — 추종 (기본, 현행)

| 필드 | 잠정 값 |
|---|---:|
| `followDelayTicks` | **12** (현 `OptionFollowDelayTicks`) |
| 위치 | Option N = 플레이어 위치 히스토리 `N × 12`틱 전 |
| 히스토리 부족 시 | 스폰 위치 고정 (현행) |

**유효 상황**

- 기동 회피 중 **지나온 궤적에 화력 잔존** (꼬리 사격).  
- 세로로 크게 흔드는 탄막에서 “몸만 빼고 옵션 화력 유지”.  
- 클래식 그라디우스 손맛 — 기본값 정당화.

**약한 상황**

- 정지 보스 약점 사격 (옵션이  lagged 위치에 묶임).  
- 급반전 직후 수 틱 공백.

### 2.2 `fixed` — 상하 고정

플레이어 기준 **상대 오프셋** (월드유닛). X는 약간 전방(발사 위치 분리·시인성).

| Option index | offsetX | offsetY |
|---:|---:|---:|
| 1 | **0.75** | **+1.50** |
| 2 | **0.75** | **−1.50** |
| 3 | **0.75** | **+2.75** |
| 4 | **0.75** | **−2.75** |

- 세로 간격: 1.25u (1–3, 2–4 사이) + 최초 ±1.50.  
- 플레이필드 halfH≈11.25u — 가장자리 Y 클램프 필수 (Core).  
- 회전/지연 없음. 매 틱 `player + offset`.

**유효 상황**

- **보스 코어·고정 약점**에 레인 고정 사격.  
- fortress 통로·상하 대칭 스폰.  
- 조준 안정 (trail 지연 없음).  
- bulwark(spread)와 조합 시 세로 커버 극대화.

**약한 상황**

- 몸통이 크게 움직이는 회피 — 옵션이 같이 움직여 **히트박스 확장**처럼 위험해 보일 수 있음(실제 피격은 플레이어만).  
- 후방·근접 포위 (전방 오프셋만).

### 2.3 `orbit` — 원 궤도

| 필드 | 잠정 값 | 근거 |
|---|---:|---|
| `radius` | **1.75** u | 히트박스(0.25) 밖, 근접 잡졸 접촉권. 화면 안 여유 |
| 각속도 | **1 SineLut slot / 2 ticks** | 주기 ≈ `64×2/60 ≈ 2.13s` / 공전 |
| 배치 | Option i 위상 = `baseAngle + i × (64 / N)` (정수 나눗셈·나머지 규칙은 CODEX) | 균등 분산 |
| `baseAngle` 진행 | 매 2틱마다 +1 slot (전 옵션 공통 공전) | 결정론·SineLut |
| 좌표 | `player + (radius·cos θ, radius·sin θ)` 정수 LUT | AGENTS.md §4 |

구현 메모 (CODEX):

- 반경 서브유닛: `1.75 × 256 = 448`.  
- 각속도 유리수: `slotsNumerator=1`, `slotsDenominator=2` (누적 나머지).  
- N이 64의 약수가 아니어도 `i * 64 / N` 정수 나눗셈으로 균등 근사.

**유효 상황**

- **근접 스웜·돌진적** — 옵션이 주변을 훑으며 다방향 미러샷.  
- 플레이어 주위 임시 “화력 버블”.  
- graze 플레이와 시너지(옵션 탄이 주변 정리).

**약한 상황**

- 원거리 보스 코어 집중 (옵션이 원을 돌아 **조준이 분산**).  
- 협로에서 옵션 탄이 벽에 낭비(장애물 정책에 따름).

### 2.4 포메이션 비교

| 포메이션 | 조준 안정 | 기동 시 화력 | 근접 방어 | 보스 단점 |
|---|---|---|---|---|
| `trail` | 중 | **고** | 중 (궤적 의존) | 지연 |
| `fixed` | **고** | 중 | 저 (전방 레인) | **강** |
| `orbit` | 저~중 | 중 | **고** | 분산 |

### 2.5 스키마 제안

```json
{
  "optionFormations": [
    {
      "id": "trail",
      "followDelayTicks": 12
    },
    {
      "id": "fixed",
      "offsets": [
        { "x": 0.75, "y": 1.50 },
        { "x": 0.75, "y": -1.50 },
        { "x": 0.75, "y": 2.75 },
        { "x": 0.75, "y": -2.75 }
      ]
    },
    {
      "id": "orbit",
      "radius": 1.75,
      "angularLutSlotsNumerator": 1,
      "angularLutSlotsDenominator": 2
    }
  ],
  "defaultOptionFormation": "trail"
}
```

---

## 3. 계열 × 모디파이어 4종 — 폭주 사전 검토

모디파이어 현행 (REQ-013/014, Core 기본값 잠정):

| modifierId | 대상 | Core 기본 (참고) |
|---|---|---|
| `pierce_shot` | **메인샷** +1 관통 | `PierceShotEnemyCount = 1` |
| `ricochet` | **메인샷** 1회 도탄 | range 8u |
| `homing_missile` | **미사일** 유도 | turn 1 LUT slot/tick |
| `kill_explosion` | **처치 시** 범위 고정 피해 | dmg **1**, radius **1.5u**, maxTargets **4** |

> 주: REQ-014 BalanceSim에서 밀집 HP1 팩 기준 pierce+kill_explosion ≈ baseline **4.12×**, 주원인은 폭발 파라미터. 미사일 계열 추가는 이 축을 키울 수 있다.

### 3.1 조합 매트릭스 (위험도)

범례: **OK** 의도된 시너지 / **WATCH** 강하지만 수용 / **RISK** 수치·규칙 개입 권고 / **N/A** 서로 다른 서브시스템

| 계열 ↓ \ 모디 → | pierce_shot | ricochet | homing_missile | kill_explosion |
|---|---|---|---|---|
| `straight` | OK (직교) | OK (직교) | **WATCH** 유도 직진 = 안정 픽 | WATCH 연사로 킬 시드 보통 |
| `spread_bomb` | OK (직교) | OK (직교) | **RISK** 유도+AoE | **RISK** 다킬→다폭발 시드 |
| `piercing_lance` | **OK*** | OK (직교) | **WATCH** 보스 완성 빌드 | WATCH 저연사·고킬값 |

\* `pierce_shot`은 **메인 전용** 유지가 전제. 랜스 고유 `pierceEnemyCount`와 **합산 금지**.

### 3.2 중점 1 — `piercing_lance` + `pierce_shot`

| 항목 | 판정 |
|---|---|
| 위험 | **낮음~중** (규칙만 지키면) |
| 이유 | 관통이 두 축(메인/미사일)에 나뉘면 “화면 전체 관통 지옥”이 아니라 **이중 레인 클리어**. 랜스 자체 3히트 + 메인+1은 풀파워 옵션 미러와 겹쳐도 기존 laser 함선 빌드와 동급 설계 공간 |
| 폭주 조건 | Core가 `pierce_shot`을 미사일/랜스에도 가산 → 랜스 3+1=4 관통 등 |
| **규칙 권고 (CODEX)** | (1) `pierce_shot` = MainShot 계열 탄에만. (2) `piercing_lance.pierceEnemyCount`는 패밀리 필드만. (3) 합산 API 금지 |
| **수치 권고** | 랜스 pierce 2 유지. 상향 필요 시 대미지 먼저, 관통 수 나중 |
| BalanceSim | 동일 시드 종대 5줄 잡졸: lance only vs lance+pierce_shot(main) clear time 비 ≤ **1.35×** 이면 통과 |

### 3.3 중점 2 — `spread_bomb` + `kill_explosion`

| 항목 | 판정 |
|---|---|
| 위험 | **높음 (1순위 폭주 후보)** |
| 메커니즘 | 폭탄 1발 다수 처치 → 처치마다 `kill_explosion` 발화. Core는 “폭발 대미지 킬의 재폭발”만 금지하므로 **1차 폭탄 킬 N개가 각각 시드**가 됨 |
| 최악 스케치 (L1, HP1 팩) | 폭탄이 5킬 → kill_explosion ×5 (각 dmg1, r=1.5, max4) → 인접 팩 연쇄 청산. 직진 대비 클리어 속도 **수 배** 가능 |
| **규칙 권고 (우선순)** | **A (권장):** `kill_explosion` 시드는 **비-AoE 최종타**만 (직격·비폭발 탄). 폭탄 폭발 킬·kill_explosion 스플래시 킬은 시드 금지. **B:** 폭발 킬도 시드하되 폭탄 1발당 kill_explosion **최대 1회**. **C (수치만):** 규칙 불변 시 아래 캡 |
| **수치 캡 (C, 규칙 미적용 시)** | bomb `explosionMaxTargets` **5→3**, `explosionRadius` **1.75→1.5**, `KillExplosionDamage` **1 유지**, `KillExplosionMaxTargets` **4→3** |
| BalanceSim 게이트 | 밀집 HP1×12: bomb+kill_exp clearSpeed / bomb alone ≤ **1.40×**; baseline 대비 ≤ **5.0×** soft WARN (REQ-014 4.12× 교훈) |

### 3.4 중점 3 — `homing_missile` 곱셈 (전 계열)

| 계열 | 효과 | 판정 | 조치 |
|---|---|---|---|
| `straight` | 빗나감↓, 잡졸·중형 안정 | WATCH 양호 | 현 turn rate 유지 후보 |
| `spread_bomb` | 유도 후 폭발 = 자동 밀집 추적 | **RISK** | (1) 호밍 중에는 수평 유도만, 하강 페이즈 고정 또는 (2) bomb 장비 시 turn rate **절반** (config 분기) 또는 (3) radius 1.5 고정 |
| `piercing_lance` | 저연사 미스 보정 + 보스 추적 | WATCH (의도된 강픽) | ST 프리미엄 이미 +10%. 추가 대미지 상향 금지. turn rate 현행 1 slot/tick 유지; 과하면 lance만 0.5 |

**곱셈 관계 명시 (데이터/코드 주석용):**

```
effective_missile = FamilyBehavior(straight|bomb|lance) ⊗ Homing?(steer)
```

호밍은 계열을 **교체하지 않고** 조향만 얹는다. 계열 보상과 모디파이어 보상은 풀에서 독립.

### 3.5 기타 조합 (요약)

| 조합 | 판정 | 메모 |
|---|---|---|
| 임의의 계열 + ricochet | OK | 메인 전용 |
| bomb + pierce_shot | OK | 직교 |
| lance + kill_explosion | WATCH | 저연사·고대미지로 킬 시드 적음. 보스에선 폭발 이득 소 |
| 전 계열 + damage_up×3 | WATCH | 기존 패시브 상한. 계열 ST 정렬 유지되면 상대 비율 동일 |
| orbit + kill_explosion | WATCH | 옵션 미러 킬 증가 → 시드↑. 메인 쪽 이슈에 가까움 |
| fixed + lance | OK 강픽 | 보스 레인정 + 고청크 — **의도된 빌드 판** |

### 3.6 CODEX 구현 체크리스트 (폭주 방지)

1. `BattleModifier.PierceShot` → MainShot(및 option mirror main)만. Missile kind 제외.  
2. `spread_bomb` 폭발 킬이 `kill_explosion`을 재시드할지 **명시적 정책** (권장: 시드 안 함).  
3. `homing_missile` + family 분기 turn rate (선택, bomb 절반 권고).  
4. 리플레이/서스펜드에 `MissileFamilyId` / `OptionFormationId` 포함.  
5. 회귀: 결정론 해시, 계열별 궤적, bomb 비연쇄, lance 동일적 재히트 금지.

---

## 4. 보상 등장 가중치 제안

### 4.1 보상 타입 (REQ-034)

| type (제안) | payload | 중복 규칙 |
|---|---|---|
| `missileFamily` | `familyId` | **현재 장착 계열과 동일하면 후보 제외** |
| `optionFormation` | `formationId` | **현재 포메이션과 동일하면 후보 제외** |

- `maxPerRun` 불필요 (교체형; 여러 번 갈아탈 수 있음).  
- 제외는 가중 추첨 **전** 결정론 필터 (기존 maxPerRun 패턴).

### 4.2 카탈로그 가중치 (잠정)

| id | type | target | weight | stageMin–Max | 비고 |
|---|---|---|---:|---|---|
| `missile_family_straight` | missileFamily | straight | **1** | 1–99 | 재장착용. 기본값이라 저가중 |
| `missile_family_spread_bomb` | missileFamily | spread_bomb | **2** | 1–99 | 전기부터 정체성 |
| `missile_family_piercing_lance` | missileFamily | piercing_lance | **2** | **2**–99 | 보스 무게 구간부터 |
| `option_formation_trail` | optionFormation | trail | **1** | 1–99 | 재장착용 |
| `option_formation_fixed` | optionFormation | fixed | **2** | 1–99 | 조준 빌드 |
| `option_formation_orbit` | optionFormation | orbit | **2** | **2**–99 | 방어/스웜 빌드 |

기존 모디파이어 weight **2** · 슬롯/캡슐 **2** · 패시브 **1** 유지 전제.

### 4.3 3택 기대값 (복원 근사, 현재 장착=straight+trail → 해당 2장 제외)

**Stage 1** (lance/orbit 미등장, straight/trail 제외 시 풀에 bomb·fixed만 계열/포메이션)

| 구간 | 기존 Σw (참고) | 신규 유효 Σw | 비고 |
|---|---:|---:|---|
| stage 1 카탈로그 | 20 (mod 8 포함) | + bomb2 + fixed2 = **+4** → **24** | lance/orbit gate out |
| stage 2+ | 23 | + (bomb2+lance2+fixed2+orbit2) = **+8** → **31** | straight/trail 제외 시 실제 추첨 Σ는 −0 |

**E[3택 중 계열·포메이션 카드 수]** (복원 근사):

| 스테이지 | 가정 | E[family/form] | E[modifier] |
|---|---|---:|---:|
| 1 | Σ=24, fam=4, mod=8 | **0.50** | **1.00** |
| 2+ | Σ=31, fam=8, mod=8 | **0.77** | **0.77** |

의도:

- 초반: 모디파이어가 살짝 더 잘 보여 **규칙 빌드** 입문.  
- 중반: 계열/포메이션 ≈ 모디파이어 — **정체성 교체** 기회 확보.  
- 한 런 보상 ~5회 기준, 계열 1회+포메이션 1회 이상을 기대 가능하게.

### 4.4 등장 연출·카피 (CLAUDE UI 힌트)

| id | 짧은 표시명 (안) |
|---|---|
| straight | 미사일: 직진 |
| spread_bomb | 미사일: 확산폭탄 |
| piercing_lance | 미사일: 관통창 |
| trail | 옵션: 추종 |
| fixed | 옵션: 상하고정 |
| orbit | 옵션: 궤도 |

---

## 5. 상황별 최적 빌드 스케치 (디자인 검증용)

| 상황 | 미사일 | 포메이션 | 모디 우선 | 이유 |
|---|---|---|---|---|
| scrapyard 잡졸 라인 | straight | trail | homing | 연사+추종+유도 |
| hive 포자 밀집 | spread_bomb | orbit | kill_exp* | AoE+근접 버블 (*규칙 A 전제) |
| fortress 통로·터렛 | spread_bomb | fixed | — | 장애물+레인 |
| 보스 코어 | piercing_lance | fixed | homing | 청크+고정 조준+유도 |
| 엘리트 기동 | piercing_lance | trail | homing | 관통+기동 보정 |

\* `kill_explosion`은 §3.3 규칙 A 적용 시에만 밀집 빌드에 안전 추천.

---

## 6. 후속 작업 분배

| 담당 | 작업 |
|---|---|
| **CODEX** | MissileFamily / OptionFormation 상태·발사 분기·SineLut 궤도·보상 타입·서스펜드/리플레이 필드·§3 규칙 |
| **GROK** | 파서 완료 후 `weapons.json`/`rewards.json` 수치 반영 + BalanceSim 조합 게이트(§3.2–3.3) |
| **CLAUDE** | 계열별 미사일 뷰/VFX, 포메이션별 옵션 위치 표시, 보상 카드 문구, Resources 동기화 |
| **사람** | §7 최종 수치·폭주 규칙 A/B/C 선택 |

### GROK 셀프 체크 (파서 이후)

- [ ] 삼계열 L1 ST DPS 32–52, L3 95–130  
- [ ] bomb+kill_exp 게이트 통과  
- [ ] lance pierce와 pierce_shot 비합산 테스트 그린  
- [ ] 보상 제외(현재 계열) 결정론  
- [ ] GameData만 수정, Core 기본값과 ApplyTo 정합  

---

## 7. 한 줄 결론

미사일 3계열은 **ST DPS를 40/≈110 대역에 묶어** 두고 상황 계수(연사 / AoE / 관통 청크)로 차별화한다. 옵션 3포메이션은 **추종·고정 레인·공전 버블**로 조준 성격을 가른다. 폭주 1순위는 **`spread_bomb` × `kill_explosion`**(및 유도 폭탄)이며, 관통 합산 금지 + 폭발 킬 비시드(규칙 A)를 CODEX 선행 조건으로 둔다. 보상 가중은 모디파이어와 비슷한 존재감(중반 E≈0.8)으로 **런 중 정체성 교체**를 목표로 한다.

**— end of design doc —**
