# REQ-174 / REQ-175 — 전함 2·3막 서치 레이저 + 코어 초대형 빔

**브랜치:** `content`  
**날짜:** 2026-08-04  
**담당:** GROK (CONTENT). Core `secondaryLaser` 는 CODEX usage limit으로 **§9-1 GROK 대행**.

main → content fast-forward 반영 후 작업 (`aimsAtPlayer` · 레이저 화면 끝 연장 · 빔 슬롯 24 포함).

---

## REQ-174 — 전함 2막: 함저 4문 전부 서치 레이저

### 변경 (`GameData/waves.json` · `boss_fortress`)

| 파츠 | cycle (구→신) | telegraph | aimsAtPlayer | 비고 |
|---|---:|---:|---|---|
| turret_c | 200 → **400** | 48 | **true** | 함저 |
| turret_d | 220 → **470** | 52 | **true** | 함저 |
| turret_e | 240 → **540** | 56 | **true** | 함저 |
| turret_f | 260 → **610** | 60 | **true** | 함저 |

- 포문 수·HP·오프셋·빔 두께(`fullHalfWidth` 0.875)·damage 2 유지.
- `endOffsetX/Y` 방향 벡터 유지 (길이는 Core가 화면 끝까지 연장).
- `intervalTicks` = `cycleIntervalTicks` (스키마 불변식).

### 주기 겹침 계산 (60fps, active≈118–130t)

| 주기 세트 | 평균 동시 빔 | 3+ 비율 | 비고 |
|---|---:|---:|---|
| 구 200/220/240/260 | **~2.9** | **~38%** | 서치 전환 시 한 점 수렴 위험 |
| 신 **400/470/540/610** | **~0.99** | **~4.2%** | 한 번에 한두 문 목표 |

- 전 구간 LCM(400,470,540,610) ≈ 3.1×10⁷ t — 실전에서 4문 동시 점화 없음.
- lifetime(118–130) ≤ cycle 전부 만족.
- 서치 조준: 예고 시작 순간 방향 고정 → 예고선 보고 피하기.

---

## REQ-175 — 전함 3막: 코어 미사일 + 주기 초대형 레이저

### 데이터 제약 (초기)

- 파츠 `attack` 는 타입 하나뿐.
- `finalCore` 그룹 멤버는 **전부 isCore** 여야 하고, isCore 는 보스당 **정확히 1**.
- → 보조 파츠 `core_beam` 은 파서가 거부 (`Every finalCore group part must be a boss core`).

### Core 대행 (CODEX limit · §9-1)

`secondaryLaser` optional 필드 추가 — primary 가 non-laser 일 때 독립 사이클로 빔을 한 겹 더 쏨.

| 파일 | 내용 |
|---|---|
| `GameDataDtos.BossPartAttackDto` | `secondaryLaser` |
| `GameDataParser.Waves` | 파싱 → `BossPartAttackProfile` |
| `BossPartAttackProfile` | `SecondaryLaser` + primary laser 와 상호 배타 검증 |
| `BattleSim` | `_bossPartSecondaryLaserCooldowns[]` 독립 쿨다운 · `TryStartLaser` |
| `DeterminismAuditHasher` | secondary + `AimsAtPlayer` fold |

**CODEX 복귀 시 리뷰 요청** — 의도·결정론·suspend 경로 확인.

### 코어 데이터

```json
"attack": {
  "type": "radialSpread",
  "intervalTicks": 36,
  "ways": 9,
  "bulletSpeed": 11.0,
  "secondaryLaser": {
    "cycleIntervalTicks": 600,
    "telegraphTicks": 180,
    "firingTicks": 12,
    "sustainTicks": 72,
    "dissipateTicks": 24,
    "startOffsetX": 0.0,
    "startOffsetY": 0.0,
    "endOffsetX": -32.0,
    "endOffsetY": 0.0,
    "thinHalfWidth": 0.75,
    "fullHalfWidth": 2.5,
    "damage": 2,
    "aimsAtPlayer": false
  }
}
```

| 항목 | 값 | 근거 |
|---|---|---|
| fullHalfWidth | **2.5** | 코어 지름 ≈ 5 wu → half 2.5 |
| telegraph | **180t (3.0s)** | 굵은 빔, ≥2.5s 요구 |
| cycle | **600t (10s)** | lifetime 288t → 빔 꺼진 시간 312t > 빔 on 84t |
| aimsAtPlayer | **false** | 굵기 강함 · 조준은 약하게 (수직 회피 통로 유지) |
| radialSpread | **유지** | 미사일/탄 패턴 그대로 (opening ways 9/−2/min3 유지) |

---

## 검증

### `dotnet test` (Tools/CoreStandalone)

```
통과! 실패 0, 통과 578, 전체 578
```

### BalanceSim (`Tools/BalanceSim/VerifyThemeAssembly.csproj`)

**본 작업으로 새로 깨진 하드코딩 게이트 없음.**  
(REQ-174/175 는 HP·partIds 개수를 바꾸지 않음. 주·패턴만 변경.)

#### 기존 FAIL (REQ-168 이후 상수 미갱신 · 호출자 몫)

| 게이트 | 기대(상수) | 실제(데이터) | 왜 |
|---|---|---|---|
| REQ-157 deck turret HP | 700 | **1400** | REQ-168 ×2 |
| REQ-157 keel turret HP | 1200 | **2400** | REQ-168 ×2 |
| REQ-157 hull partIds | c,d only | **c,d,e,f** | REQ-168 4문 |
| REQ-157 travel 90..150 | — | hull=**240** bow=**180** | REQ-168 자연 이동 |

#### 본 작업과 무관한 기존 FAIL

- REQ-073 shuffle S2 fortress 벽 (fortress@S2 TTK 124s)
- REQ-103b hive late softlock (`seg_sandwich_hive`)
- scoring graze smoke / movement enemy count 밴드 (레거시)

#### 유지된 긍정 신호

- warship assemble OK · parts sum 44000 · form2 존재
- core opening ways 9/−2/min3 감소 구조 유지
- pure ST TTK @720 ≈ 61.1s · wall ≈ 60.8s (REQ-168 밴드 안)

---

## CLAUDE / CODEX 후속

### CLAUDE
1. [ ] `Assets/Resources/GameData/waves.json` 동기화
2. [ ] 함저 포탑 서치 예고선 연출 확인 (aimsAtPlayer 고정 방향)
3. [ ] 코어 secondaryLaser 초대형 빔 비주얼 (fullHalfW 2.5)

### CODEX
1. [ ] **리뷰**: content 의 `secondaryLaser` Core 대행분 (결정론·쿨다운·suspend 재동기화)
2. [ ] (선택) `secondaryLaser` 전용 EditMode 테스트 추가

### 사람
1. [ ] 2막 서치 4문 주기 400–610 체감 (너무 느리면 소폭 단축 가능)
2. [ ] 3막 굵은 고정 빔 vs 조준 중 하나만 강하게 — 현재 **굵기 강 / 조준 off**
