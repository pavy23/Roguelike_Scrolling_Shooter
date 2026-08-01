# REQ-103 — Core 신규 필드 요구서 (GROK → CODEX)

- 작성: 2026-08-02
- 근거: 사람 승인 스테이지 대개편 설계안 `Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md`
- 범위: **CODEX / Shmup.Core** 스키마·시뮬 축 (content 데이터 필드는 Core 파서 착수 후 GROK가 채움)
- 병행: REQ-103a = 기존 스키마만으로 가능한 웨이브 재설계 (content, 본 문서와 분리)

---

## 0. 우선순위 요약

| ID | 필드/축 | 담당 시그니처 스테이지 | 긴급도 | 결정론 영향 |
|---|---|---|---|---|
| **C-A** | `Obstacle.blocksEnemyBullets` | St1 고철 방패 | **P0** | 충돌 판정 (순수 시뮬) |
| **C-B** | `Obstacle.regenDelayTicks` | St2 재생 세포벽 | **P0** | 틱 그리드 재생 (순수 시뮬) |
| **C-C** | `midbossOutcome` 분기 | 전 스테이지 (후반 테이블 선택) | **P1** | 시드 분기 스트림 필요 |
| **C-D** | `scrollSpeedMultiplier` (세그먼트) | St1 후반·St5 전반 스파이크 | **P1** | 스크롤=월드 이동이면 시뮬; Presentation 전용이면 무영향 |
| **C-E** | 섹션 마커 이벤트 | Presentation 배경 전환 | **P1** | 이벤트 발행만이면 해시 영향 최소 |

REQ-103a에서 **C-D는 스키마에 없음**을 확인함 (`waves.json` 루트 `scrollSpeed` 단일). 스파이크 데이터는 Core 필드 착수 후 content가 채운다.

---

## 1. C-A — `blocksEnemyBullets` (고철 방패)

### 왜
설계 St1 시그니처: 파괴 가능 고철이 **적탄을 차단**. “쏘면 엄폐물이 사라진다” 튜토리얼.  
현재 obstacle은 `solid` / `breakable` / `laserEmitter` — 플레이어·적 기체 충돌만, **적탄 차단 플래그 없음**.

### 제안 스키마
```json
{
  "type": "breakable",
  "x": 12.0,
  "y": 2.5,
  "hp": 30,
  "blocksEnemyBullets": true
}
```

| 필드 | 타입 | 기본 | 규칙 |
|---|---|---|---|
| `blocksEnemyBullets` | bool | `false` | `true`일 때만 적 탄막 AABB와 장애물 충돌 → 탄 소멸(또는 흡수). 플레이어 탄은 기존처럼 breakable만 데미지 |

### 제안 Core 시그니처
```csharp
// ObstacleSpawn / ObstacleDefinition
public bool BlocksEnemyBullets { get; }

// BattleSim 탄 스텝 (의사코드)
// if (bullet.IsEnemy && obstacle.BlocksEnemyBullets && AabbOverlap(bullet, obstacle))
//     DespawnBullet(bullet); // breakable HP는 플레이어 탄만 감소 (기본)
```

### 결정론
- 동일 틱 처리 순서 유지 (기존 탄/장애물 루프 정렬 규칙 준수).
- `Dictionary` 순회 금지 — 장애물 배열 인덱스 순.

### 수락 기준
1. `blocksEnemyBullets: true` breakable 뒤에 숨으면 적 조준탄이 막힘.
2. 플레이어가 고철을 파괴하면 엄폐 소멸.
3. 기본 `false` → 기존 스테이지 비트 동일 (골든/해시 회귀 없음).
4. EditMode + DeterminismAudit 통과.

### content 후속 (GROK)
St1 scrapyard 후반 세그먼트에 방패 잔해 배치 (REQ-103b 예정).

---

## 2. C-B — `regenDelayTicks` (재생 세포벽)

### 왜
설계 St2 시그니처: 쏘면 파이고 **수 초 후 재생**하는 유기벽. 결정론 틱 그리드와 궁합 최상.

### 제안 스키마
```json
{
  "type": "breakable",
  "x": 14.0,
  "y": -3.0,
  "hp": 40,
  "regenDelayTicks": 180
}
```

| 필드 | 타입 | 기본 | 규칙 |
|---|---|---|---|
| `regenDelayTicks` | int | `0` (=재생 없음) | `>0`이면 파괴 후 N틱 뒤 동일 좌표·최대 HP로 재생성. `0`은 현재 breakable과 동일 |

### 제안 Core 시그니처
```csharp
public int RegenDelayTicks { get; } // 0 = no regen

// 파괴 시: schedule RespawnAt(tickNow + RegenDelayTicks)
// 재생 전 슬롯은 충돌 OFF; 재생 순간 HP=max, active=true
// MaxObstacles 슬롯 점유: 파괴 중에도 슬롯 유지(재생 예약) 권장 — 풀 고갈 방지
```

### 결정론
- 재생 시각 = 파괴 틱 + delay (벽시계 금지).
- 시드 지터(REQ-098)는 **최초 배치**에만 적용; 재생 좌표는 파괴 직전 논리 좌표 고정.

### 수락 기준
1. `regenDelayTicks: 180` 벽이 파괴 후 3초(@60tps)에 돌아옴.
2. 재생 전 통과 가능, 재생 후 충돌 재활성.
3. `0`/omit → 기존 동작.
4. 동시 다수 재생 예약이 MaxObstacles를 넘지 않음(슬롯 유지 정책).

### content 후속
hive 후반 `seg_hive_membrane_wall` / `organic_pulse` 계열에 재생벽 배치.

---

## 3. C-C — `midbossOutcome` 분기

### 왜
설계: 중간보스 **처치 방식**에 따라 후반 웨이브 테이블 선택 (예: St3 파괴 포탑 수 → 보스 개막 탄막 밀도).  
현재 스테이지 생성은 시드+difficulty+theme으로 세그먼트만 뽑고, **미드보스 전투 결과 → 후반 재선택** 축이 없음.

### 제안 모델 (최소)
```csharp
public enum MidbossOutcomeKind : byte
{
    Default = 0,
    CleanKill = 1,      // 시간/피해 효율 클리어
    Attrition = 2,      // 장기전·피격 다수
    PartFocus = 3,      // 특정 파츠 우선 파괴 (St3 함미 등)
}

// StagePlan 또는 RunManager 상태
public MidbossOutcomeKind LastMidbossOutcome { get; }

// 생성 API 확장 (제안)
StagePlan GeneratePostMidbossHalf(
    ulong seed,
    int stageIndex,
    int difficulty,
    string themeId,
    MidbossOutcomeKind outcome);
```

### 데이터 측 (content가 채울 스키마 초안)
```json
{
  "id": "seg_fortress_hull_line_clean",
  "postMidbossOutcomes": ["cleanKill"],
  "difficultyMin": 3,
  "difficultyMax": 5,
  "theme": "fortress"
}
```
또는 카탈로그 테이블:
```json
"midbossBranches": [
  {
    "theme": "fortress",
    "outcome": "partFocus",
    "segmentPoolTags": ["hull_line"],
    "bossOpenDensityMultiplier": 0.85
  }
]
```

### 스트림 분기
- 후반 재선택 RNG는 `Rng.Fork(streamId)` — **스테이지 생성 스트림과 분리** (AGENTS.md §4.4).
- 제안 streamId: `PostMidbossSegmentStream = 4` (기존 0..3 사용 중: stage/segment/boss/obstacleJitter).

### 수락 기준
1. 동일 시드·동일 outcome → 동일 후반 세그먼트.
2. outcome만 바꾸면 후반 테이블이 달라질 수 있음 (풀이 분기된 경우).
3. outcome 미실장 빌드는 Default 풀만 (기존과 동일).

### 판단 메모
- **최소 구현**: outcome enum + 후반 세그먼트 가중 필터 1단이면 content가 St3 분기 보상 데이터를 넣을 수 있음.
- 보스 개막 탄막 밀도 배율은 boss phase 또는 contract multiplier 재사용 검토.

---

## 4. C-D — `scrollSpeedMultiplier` (세그먼트)

### 현황 (REQ-103a 조사)
- `waves.json` 루트 `scrollSpeed: 5.0` 만 존재.
- `SegmentDto` / `SegmentEnvironmentDto`에 스크롤 배율 필드 **없음**.
- 기믹(`gimmicks[]`)도 vision/timeLimit만.

### 왜
설계: 구간 경계 3종 세트 중 **스크롤 속도** + St1 후반·St5 전반 **짧은 고속 스파이크 1회**.

### 제안 스키마
```json
{
  "id": "seg_scrap_speed_spike",
  "scrollSpeedMultiplier": 1.5,
  "lengthTicks": 240,
  ...
}
```

| 필드 | 타입 | 기본 | 규칙 |
|---|---|---|---|
| `scrollSpeedMultiplier` | decimal | `1.0` | 세그먼트 구간 동안 루트 scrollSpeed에 곱. 권장 밴드 0.75..2.0 |

### 결정론 분기
설계안 §2: “Core가 소유하는 것은 scrollSpeed 하나 — 결정론 무영향”은 **Presentation 패럴랙스 연출**을 가정한 문구.  
실제 구현 선택:

| 옵션 | 의미 | 권장 |
|---|---|---|
| **D1** 시뮬 스크롤(적·장애물 월드 X 이동)에 배율 적용 | 고속 구간이 진짜로 빨라짐 | **권장** (손맛) |
| **D2** Presentation 패럴랙스만 배율 | 결정론 해시 무영향, 연출만 | 스파이크 “느낌”만 |

**GROK 권장: D1** — 스파이크가 회피 난이도에 기여해야 시그니처가 성립. ExactFraction 곱으로 플랫폼 분기 금지.

### 수락 기준
1. multiplier 1.5 세그먼트에서 장애물/스크롤 오브젝트 X 속도 1.5×.
2. omit/1.0 → 기존과 비트 동일.
3. 세그먼트 경계에서 배율 전환 시 위치 연속(순간이동 없음).

### content 후속 (필드 착수 후)
- St1: scrap 후반 짧은 세그먼트 1개 `1.5`
- St5: core 전반 짧은 세그먼트 1개 `1.5`~`1.75`

---

## 5. C-E — 섹션 마커 이벤트 (Presentation 배경 전환)

### 왜
설계: 전반→중간보스→후반→보스 4구간을 파티클+틴트+스크롤로 신호. Presentation `SectionTheme` lerp 트리거 필요.

### 세그먼트 인덱스만으로 충분한가?

| 접근 | 장점 | 단점 |
|---|---|---|
| **E1** `StagePlan.Segments[i]` 인덱스만 노출 | Core 변경 최소 | “중간보스 직후”가 세그먼트 수에 묶임. elite/supply 등 encounter별 세그 수 다름 (`segmentsPerStage` vs closing 7) |
| **E2** 명시 `sectionId` / 이벤트 enum | 데이터·연출 분리 명확 | 스키마+생성기 확장 |
| **E3** 기존 런타임 국면 이벤트 재사용 (stage start / midboss spawn-defeat / boss warn) | 이미 있을 가능성 | 세그먼트 내부 “후반 잠식 시작” 세밀 신호 부족 |

**GROK 판단: E1만으로는 부족하다.**  
이유는 (1) 로그라이크 루트가 방마다 세그먼트 수가 달라지고 (2) 중간보스 격파 시점이 “세그먼트 경계”가 아니며 (3) 설계의 세계 전환은 **미드보스 격파 순간**에 묶여 있다.

### 제안 (최소 이벤트 세트)
```csharp
public enum StageSectionMarker : byte
{
    StageIntro = 0,    // 전반 진입
    MidbossStart = 1,
    MidbossDefeat = 2, // 세계 전환 포인트
    LateHalf = 3,      // 후반 첫 세그먼트 시작
    BossWarning = 4,
    BossStart = 5,
}

// 시뮬 이벤트 (REQ-005 계열 확장)
public readonly struct StageSectionEvent
{
    public StageSectionMarker Marker { get; }
    public int StageIndex { get; }
    public string ThemeId { get; }
    public int Tick { get; }
}
```

Presentation은 이벤트 구독만 — 게임플레이 판정 금지 (AGENTS.md).

### 세그먼트 인덱스 보조
이벤트와 **병행**해 `currentSegmentIndex` / `segmentId`를 읽기 API로 노출하면 배경 스크롤 페이즈 보간에 유용.  
**인덱스 단독 트리거는 비권장.**

### 수락 기준
1. 미드보스 격파 틱에 `MidbossDefeat` 1회 발행 (결정론 재현).
2. 후반 첫 스크롤 세그먼트 시작 시 `LateHalf`.
3. Presentation이 동일 시드 리플레이에서 동일 틱에 전환.

---

## 6. 구현 분할 제안 (CODEX)

| REQ | 내용 | 의존 |
|---|---|---|
| **REQ-103-Core-1** | C-A blocksEnemyBullets + C-B regenDelayTicks (Obstacle 축) | 파서·ObstacleSpawn·탄 충돌 |
| **REQ-103-Core-2** | C-D scrollSpeedMultiplier (세그먼트) | StageSegment + 스크롤 적용 지점 |
| **REQ-103-Core-3** | C-E 섹션 마커 이벤트 + (선택) C-C midbossOutcome | RunManager 국면 · RNG fork |

권장 순서: Core-1 → Core-2 → Core-3.  
content REQ-103a는 Core 없이 진행 완료 가능 범위만 처리.

---

## 7. 파서/골든 영향 (예상)

- `GameDataDtos.ObstacleDto` / `SegmentDto` 필드 추가 (optional, 기본값 하위호환).
- `GameDataParserTests` 골든: 신규 필드 omit 시 기존 카탈로그 해시/카운트 유지.
- DeterminismAudit: 신규 플래그 미사용 시드 경로 해시 불변.

---

## 8. GROK 대기 작업 (Core 착수 후)

1. St1 scrap 방패 잔해 배치 (`blocksEnemyBullets: true`)
2. St2 hive 재생벽 (`regenDelayTicks`)
3. St1/St5 고속 스파이크 세그먼트 1개씩 (`scrollSpeedMultiplier`)
4. midbossOutcome 풀 태그 분기 테이블 (St3 우선)
5. BalanceSim 게이트 확장 (방패 엄폐 EV, 재생 주기, 스파이크 길이 상한)
