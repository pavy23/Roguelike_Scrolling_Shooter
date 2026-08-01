# REQ-092 미니보스 이동·기체 옵션 편성·Closing 관측 보고

- 담당: CODEX / SIMULATION
- 작업일: 2026-08-01
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## A. 미니보스 이동 어휘 확장

`BossMovementPattern`과 공용 `BossPhase` 파서에 다음 두 축을 추가했다.

- `lungeReturn`
  - `movementTelegraphTicks` 동안 `BossHoldX`에서 정지한다.
  - 이후 `movementAmplitude`만큼 플레이어가 있는 왼쪽 X 방향으로 정수 삼각 왕복하고
    `movementPeriodTicks` 안에 홀드 위치로 돌아온다.
  - 매 주기 시작에 public `SimEventType.BossMovementTelegraphed`를 발생시킨다.
    `Arg`는 예고 틱, X/Y는 홀드 위치이므로 Presentation이 상태 차분 없이 예고 연출을
    붙일 수 있다.
- `figureEight`
  - 기존 64슬롯 정수 사인 LUT만 사용한다.
  - Y는 기본 주파수, X는 2배 주파수·절반 진폭으로 움직여 세로 8자 궤적을 만든다.

두 패턴 모두 부동소수점·난수·벽시계를 사용하지 않는다. 페이즈 전환 시 X/Y 위치와
속도를 함께 반영하는 정수 재중심 보간도 추가했다. 새 public 심벌은
`BossMovementPattern.LungeReturn`, `BossMovementPattern.FigureEight`,
`BossPhase.MovementTelegraphTicks`, `SimEventType.BossMovementTelegraphed`다.
`DeterminismAuditHasher`의 StagePlan 입력 해시에 이동 예고 틱도 포함했다.

### stationary 원인 확인

정지는 진폭 0인 사인의 부작용이 아니다. `BattleSim`에는
`BossMovementPattern.Stationary` 전용 분기가 있고 X는 `BossHoldX`, Y는 이동 앵커에
고정된다. 현재 `GameData/enemies.json`의 `mini_walker`는 세 페이즈 모두
`movementPattern: "stationary"`, 진폭 0을 명시하므로 전투 내내 가만히 있는 것이
데이터대로의 정상 결과다. 다른 미니보스도 일부 페이즈에 stationary를 의도적으로
사용한다. 새 패턴의 미니보스별 배정과 수치는 GROK 소유 데이터 작업으로 남겼다.

## B. ships.json 기체별 시작 옵션 편성

- `ships.json` 지원 버전을 v4로 올리고 선택 필드 `optionFormation`을 추가했다.
- 허용값은 기존 공용 식별자와 같은 `trail`, `fixed`, `orbit`이다.
- `ShipDefinition.StartingOptionFormation`을 public nullable 속성으로 노출했다.
- `RunManager`는 새 런 생성 시 기체 시작 편성을 우선 적용하고, 필드가 없으면
  `weapons.json.defaultOptionFormation`을 사용한다.
- 기체가 가리킨 편성이 `BattleContent`에 없으면 생성자에서 즉시 거부한다.
- 기존 `RewardType.OptionFormation` 적용 경로는 그대로라 보상을 선택하면 현재 편성이
  교체된다.

### 스키마 판정

신규 의미가 추가되므로 생산 데이터는 `schemaVersion: 4`로 올려야 한다. v1~v3은 해당
필드를 적용하지 않고 nullable `null`로 마이그레이션되어 전역 기본 편성을 유지한다.
v4에서도 필드는 선택이므로 생략 시 같은 폴백을 쓴다. v3에서 도입된 필수
`missileFamily` 규칙은 별도 버전 경계로 유지했다. 따라서 GROK은 기체 1/2/3에 각각
trail/fixed/orbit를 넣으면서 ships 스키마만 v4로 올리면 된다.

서스펜드는 현재 편성을 `RunSuspendData.optionFormation`에, 리플레이는
`InputRecordingData.optionFormation`에 이미 저장한다. 기체 시작 orbit으로 만든 런을
서스펜드→복원하고 InputRecorder→InputPlayback한 테스트에서 모두 orbit을 재현했다.

## C. 미니보스 이후 Closing 길이 관측

main에 이미 병합된 Core 손잡이를 확인했다.

- `waves.json.closingSegmentsPerStage` → `StageGenerationCatalog` →
  `StageRouteSection.Closing` 생성 경로가 연결되어 있다.
- 현재 데이터는 일반 구간 `segmentsPerStage: 3`, Closing
  `closingSegmentsPerStage: 5`다.
- RunManager 구조상 room 2 미니보스 보상 뒤 room 3이 Closing이고, 그 종료 뒤 별도
  StageBoss 전투로 진입한다.

고정 관측 `(seed=123456789, theme=scrapyard, stage=1, difficulty=3,
encounter=Normal)` 결과는 **5세그먼트, 합계 3,540틱 = 59초(60Hz)** 다. 이 값은 선택된
세그먼트의 `lengthTicks` 합이며, 잔여 적대 엔티티 정리 상황에 따라 룸 경계에서
0~300틱의 결정론적 drain 대기가 추가될 수 있다.

따라서 일반 Closing 연장은 **waves.json 수치만으로 가능**하며 REQ-092용 추가 Core
손잡이는 필요하지 않다. 단, 현 정책상 Elite/Supply encounter는 섹션과 무관하게
1세그먼트로 축약된다. 그 두 특수 encounter까지 Closing 길이 값을 강제하려면 별도의
Core 정책 변경이 필요하지만, 현재 기본 Closing 관측과 사람 피드백 해결 범위에는
해당하지 않는다.

## 회귀 테스트

- `LungeReturnHoldsForTelegraphThenLungesAndReturns`
- `FigureEightUsesBothAxesAndIsDeterministic`
- `Parse_BossMovementVocabularyReadsLungeAndFigureEight`
- `Parse_ShipsV4ReadsOptionalStartingOptionFormation`
- `ShipOptionFormationSeedsRunSuspendAndReplay`
- 실데이터 파서 테스트에 Closing 5세그·합산 틱 관측을 추가

모든 신규 테스트는 NUnit 3 API만 사용하며 `Assert.Multiple`을 사용하지 않았다.

## 최종 검증

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS: 실패 0, 통과 472, 전체 472
```

### DeterminismAudit

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
PASS 6 scenarios
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

### 같은 시드 2회

```text
seed=12345 stages=3 tickBudget=30000
RUN_1 hash=C89CE41572D3C5C3 ticks=16979 rooms=9/9
RUN_2 hash=C89CE41572D3C5C3 ticks=16979 rooms=9/9
EXACT_MATCH True
```

### 정적 검사

```text
git diff --check: PASS
Assert.Multiple 신규 사용: 없음
System.Random / UnityEngine.Random / Guid.NewGuid 신규 사용: 없음
DateTime.Now / Environment.TickCount 신규 사용: 없음
```

## 오케스트레이터 주의

감사 실행이 만든 `Tools/DeterminismAudit/bin/`은 비소스 빌드 산출물이며 커밋 대상이
아니다. 자동 정리 명령은 실행 환경 승인 한도 때문에 거부되었으므로 커밋 스테이징에서
제외해야 한다.
