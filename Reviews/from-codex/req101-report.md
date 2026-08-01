# REQ-101 Core 기믹 축 1차 구현 보고서

- 일자: 2026-08-02
- 담당: CODEX / SIMULATION
- 상태: **PASS**
- 커밋: 하지 않음 (오케스트레이터 커밋 대상)

## 1. 사전 설계 확인

다음 문서를 정독하고 구현 기준으로 사용했다.

- `Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md`
- `Reviews/from-grok/req103-core-requests.md`

## 2. 구현 결과

### C-A — 장애물 적탄 차단

- `ObstacleSpawn.BlocksEnemyBullets` public 속성과 JSON optional 필드
  `blocksEnemyBullets`를 추가했다. 생략 기본값은 `false`다.
- 명시적으로 활성화된 장애물과 적탄의 AABB가 겹치면 적탄만 소거한다.
- 차단 시 `SimEventType.EnemyBulletBlocked`를 발행한다.
- 적탄 차단은 장애물 HP를 깎지 않는다. 플레이어 탄의 breakable 장애물 데미지
  경로는 기존대로 유지된다.
- 장애물 배열 인덱스 순서로 판정해 순회 순서가 결정적이다.

### C-B — 장애물 재생

- `ObstacleSpawn.RegenDelayTicks` public 속성과 JSON optional 필드
  `regenDelayTicks`를 추가했다. 생략/0은 재생 없음이다.
- breakable 장애물만 양수 재생 지연을 가질 수 있으며, 파서와 public 생성자에서
  음수 및 비-breakable 조합을 거부한다.
- 파괴 틱 `T`에 `T + regenDelayTicks`를 예약하고, 파괴 직전 논리 좌표와 최초
  최대 HP, 동일 entity ID, 적탄 차단 플래그로 복구한다.
- 대기 중인 재생은 `PendingObstacleRegenerations` public read-only 관측으로
  노출되며 MaxObstacles 슬롯을 계속 점유한다.
- 재생 위치가 플레이어, 일반 적, 활성 보스 또는 활성 보스 파츠와 겹치면
  `RespawnAtTick`을 정확히 1틱 연장한다.
- 재생 성공 시 `SimEventType.ObstacleRegenerated`를 발행한다.

### C-C — midbossOutcome 분기

- public `MidbossOutcomeKind : byte`를 추가했다:
  `Default`, `CleanKill`, `Attrition`, `PartFocus`.
- public `MidbossOutcomeEvaluator.Evaluate(...)`가 특정 파츠 조건을 우선하고,
  그렇지 않으면 처치 소요 틱과 clean-kill 임계 틱으로 outcome을 산출한다.
- `BattleSim.BossDefeatElapsedTicks`와
  `BattleSim.WasBossPartDestroyed(string partId)`를 public 관측으로 추가했다.
- `RunManager.LastMidbossOutcome`을 추가하고 현재 midboss의 phase duration 기반
  clean-kill 임계로 자동 산출한다. 현재 GameData에는 outcome 대상 파츠 ID 설정이
  없으므로 파츠 기반 정책은 위 public 파츠 관측과 evaluator에 대상 ID를 전달하는
  방식으로 사용할 수 있다.
- `SegmentDto.postMidbossOutcomes`와
  `StageSegmentTemplate.PostMidbossOutcomes`를 추가했다. 지원 문자열은
  `default`, `cleanKill`, `attrition`, `partFocus`다.
- `IMidbossOutcomeRouteStageGenerator` 및
  `SegmentStageGenerator.GeneratePostMidbossHalf(...)` public API를 추가했다.
- 후반 선택 RNG는 `PostMidbossSegmentStream = 4`로 기존 생성/보스/지터
  스트림과 분리했다.
- 해당 outcome 풀이 완전한 clearable route를 만들 수 있을 때 tagged 풀을 쓰고,
  그렇지 않으면 Default 풀로 결정적으로 폴백한다. 필드가 없는 기존 세그먼트는
  Default 풀에만 속하며 기존 선택 스트림을 그대로 쓴다.

### C-D — 세그먼트 스크롤 배율

- JSON optional decimal `scrollSpeedMultiplier`를 추가했다. 생략 기본값은 1이다.
- 파서에서 decimal을 축약된 정수 유리수로 바꾸고,
  `StageSegment.ScrollSpeedMultiplierNumerator/Denominator` public 속성으로
  전달한다.
- D1 방식으로 실제 적, 장애물, 캡슐, 폭탄 픽업의 스크롤 이동에 적용한다.
- 구간별 속도 곱과 누적 오프셋은 정수/유리수 연산만 사용하며 경계 좌표가
  연속이다.
- 모든 배율이 1/1인 기존 데이터는 종전의 전역 scroll remainder 계산을 그대로
  사용한다. 따라서 필드 생략 시 세그먼트 경계에서 remainder가 리셋되지 않는다.

### C-E — 미드보스 격파 마커

- `SimEventType.MidBossDefeated`를 추가했다.
- `RunManager`가 midboss 구간에 생성한 BattleSim만 마커를 발행한다.
- 보스 HP가 0이 된 바로 그 Step에 한 번 발행하며, `Arg`에는 처치 소요 틱을
  담는다. 따라서 Presentation은 다음 세그먼트 인덱스를 기다리지 않고 같은 틱의
  이벤트로 배경 전환을 시작할 수 있다.

## 3. 결정론 및 저장 스키마 판단

이번 변경은 월드 이동, 탄 소거, 재생 예약, 후반 route 선택, midboss 상태와
이벤트 열을 바꾼다. 기존 리플레이/서스펜드를 새 규칙으로 재생하면 동일 상태를
보장할 수 없으므로 스키마 상향이 필요하다고 판단했다.

- `InputRecordingData`: schema **21 → 22**
- `RunSuspendData`: schema **23 → 24**
- 직전 버전 21 replay와 23 suspend는 명시적으로 거부한다.
- `lastMidbossOutcome`을 suspend DTO와 canonical checksum에 포함했다.
- outcome, 세그먼트 배율, 장애물 신규 필드, pending regeneration,
  boss defeat elapsed ticks를 `DeterminismAuditHasher`에 포함했다.

## 4. 검증

### 단위/회귀 테스트

명령:

```powershell
cd Tools\CoreStandalone
dotnet test --no-restore
```

결과:

```text
통과 499 / 실패 0 / 건너뜀 0
```

신규 검증 축:

- 적탄 차단 플래그 true/기본 false 및 장애물 HP 보존
- 재생 대기, 플레이어 중첩 1틱 유예, 동일 위치/최대 HP 복구
- outcome별 tagged 후반 풀 선택, Default 폴백, 같은 시드 동일 plan
- PartFocus 우선 및 CleanKill/Attrition tick 경계
- 3/2 세그먼트 배율의 실제 좌표 이동과 경계 연속성
- 배율 생략 시 기존 전역 scroll remainder 보존
- midboss 격파 틱의 마커 정확히 1회
- 신규/직전 replay·suspend schema 거부 정책

`Assert.Multiple`은 사용하지 않았다.

### DeterminismAudit suite

명령:

```powershell
dotnet run --project Tools\DeterminismAudit -- --suite
```

결과: 6개 장기 시나리오와 256-seed cap-boundary sweep 전부 PASS,
최종 출력 **`AUDIT PASS`**.

### 동일 시드 2회

명령을 독립적으로 두 번 실행했다.

```powershell
dotnet run --project Tools\DeterminismAudit -- 12345 3 30000
```

두 실행 결과:

```text
run 1: hash=098B4B52C08FBC16 ticks=20753 completedRooms=9/9
run 2: hash=098B4B52C08FBC16 ticks=20753 completedRooms=9/9
```

동일 시드/입력 해시가 일치한다.

## 5. Content/Presentation 후속 계약

GROK은 기존 JSON을 깨지 않고 필요한 세그먼트/장애물에 다음 optional 필드를
추가할 수 있다.

```json
{
  "scrollSpeedMultiplier": 1.5,
  "postMidbossOutcomes": ["cleanKill"],
  "obstacles": [
    {
      "type": "breakable",
      "x": 12.0,
      "y": 2.5,
      "hp": 30,
      "blocksEnemyBullets": true,
      "regenDelayTicks": 180
    }
  ]
}
```

Presentation은 `MidBossDefeated`, `ObstacleRegenerated`,
`EnemyBulletBlocked` 이벤트를 선택적으로 구독할 수 있다. 이벤트 미구독 시에도
Core 게임플레이 결과에는 영향이 없다.
