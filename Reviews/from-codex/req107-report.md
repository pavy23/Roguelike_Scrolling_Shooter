# REQ-107 보고서 — 이어하기 컨티뉴 재고 복제 구멍 봉인

- 담당: CODEX / SIMULATION
- 일자: 2026-08-02
- 상태: Core 구현 및 standalone 검증 완료
- 커밋: 오케스트레이터 수행 예정

## 결론

- `RunManager.ResumeFromSuspendData`에 live `MetaState`를 받는 public 오버로드를 추가했다.
- 메타 연결 리줌 이후 `TryUseContinue()`와 최종전 판돈은 신규 런과 동일한 기존 공통 경로를 타므로 런 재고와 메타 재고를 함께 차감한다.
- 메타 없는 기존 리줌 오버로드는 그대로 유지했다. 리플레이·결정론 검증은 종전처럼 외부 메타를 변경하지 않고 고립된 런을 복원한다.
- suspend 스키마는 v26을 유지했다. v26에 필요한 회계 상태가 이미 모두 존재하고 canonical checksum에 포함되므로 새 필드가 필요하지 않다.

## v26 확인과 정합 규칙

v26 `RunSuspendData`에는 다음 값이 이미 있다.

- `continueStock`: 현재 런의 남은 컨티뉴 재고
- `initialContinueStock`: 런 시작 시 재고
- `continuesUsed`: 이 런에서 사용한 수량
- `continueDecisions`: 사용 결정의 누적 시뮬레이션 틱
- `finalWagerCommitted`, `finalWagerShieldGranted`, `finalWagerOverflowConverted`: 최종전 판돈 회계

기존 검증식은 다음 보존 법칙을 강제한다.

```text
initialContinueStock
  = continueStock + continuesUsed + finalWagerSpent
```

위 값들은 모두 suspend checksum에 포함되어 있다. 따라서 REQ-107에서는 스키마를 올리지 않고 아래 규칙을 추가했다.

1. 일반 런을 live `MetaState`와 리줌할 때 `data.continueStock == metaState.ContinueStock`이어야 한다.
2. 일치하면 과거 `continuesUsed`나 판돈을 메타에서 다시 차감하지 않는다. 이미 저장에 반영된 회계로 취급한다.
3. 리줌 이후 새 사용/판돈만 런과 메타에서 동시에 차감한다.
4. 불일치는 오래된 suspend/meta 조합 또는 기존 복제 구멍으로 생성된 상태이므로 메타를 변경하지 않고 `ArgumentException`으로 거부한다. 자동 차감은 정상 메타를 이중 차감할 수 있고 자동 증가는 복제를 허용하므로 수행하지 않는다.
5. 데일리 런은 예외다. 런 재고는 항상 0이고 live 메타 재고는 사용할 수 없는 외부 재고로 그대로 남는다. 따라서 데일리 리줌은 두 값의 불일치를 허용하며 메타를 소비하지 않는다.
6. 메타 없는 기존 오버로드에는 일치 검사를 적용하지 않는다.

## 공개 API

간단한 기본 콘텐츠 경로:

```csharp
RunManager.ResumeFromSuspendData(
    data,
    stageGenerator,
    battleConfig,
    battleContent,
    powerUpGauge,
    metaState);
```

현재 Presentation 호출과 같은 콘텐츠 경로는 기존 인자 끝에 메타만 추가하면 된다.

```csharp
RunManager.ResumeFromSuspendData(
    data,
    stageGenerator,
    battleConfig,
    battleContent,
    powerUpGauge,
    rewards,
    ship,
    metaState);
```

커스텀 progression/content 경로에도 `MetaState`를 명시적으로 받는 public 오버로드를 추가했다. 세 live-meta 오버로드는 null 메타를 거부한다.

Presentation의 실제 이어하기 경로는 기존 메타 없는 호출 대신 위 live-meta 오버로드에 현재 저장 슬롯의 `MetaState`를 전달해야 한다. `Assets/Scripts/Presentation/`은 CLAUDE/RENDERER 소유이므로 CODEX는 수정하지 않았다.

## 회귀 테스트

`Req104ContinueEconomyTests`에 다음 4개를 추가했다.

- `MetaResumeAcrossTwoSuspendsChargesExactlyOncePerContinue`
  - suspend → meta 리줌 → 사망 → 컨티뉴 사용 → 재 suspend → 재리줌
  - 첫 사용에서 런/메타가 2→1로 정확히 한 번 감소
  - 두 번째 리줌 자체는 과거 사용분을 재차감하지 않음
  - 재리줌 뒤 다음 사용도 런/메타가 1→0으로 함께 감소
- `MetaResumeRejectsMismatchedContinueInventoryWithoutMutation`
  - suspend 재고 2 / meta 재고 1 조합을 거부하고 meta 1을 유지
- `DailyMetaResumeKeepsRunStockZeroAndLeavesMetaStockUntouched`
  - 데일리 런 재고 0과 meta 재고 2를 정상 복원하고 사용을 거부
- `MetaResumeFinalWagerConsumesRunAndMetaStockTogether`
  - 리줌 후 최종전 진입 판돈이 런/메타 재고 2를 모두 0으로 정산

기존 `ReplayAndSuspendRoundTripContinueDecisionDeterministically`도 계속 통과해 메타 없는 리줌의 종전 동작과 경계 해시 일치를 확인했다. `Assert.Multiple`은 사용하지 않았다.

## 검증 결과

### 집중 회귀

```text
dotnet test --filter FullyQualifiedName~Req104ContinueEconomyTests
PASS 11 / FAIL 0 / SKIP 0
```

### CoreStandalone 전체

```text
dotnet test
PASS 514 / FAIL 0 / SKIP 0
```

### DeterminismAudit 전체 suite

```text
dotnet run --project Tools/DeterminismAudit -- --suite
6/6 scenarios PASS
cap-boundary: 256/256 qualifying seeds PASS
AUDIT PASS
```

### 같은 시드 독립 2회

```text
dotnet run --no-restore --no-build --project Tools/DeterminismAudit -- 12345 3 30000
```

두 실행 모두:

```text
hash=1A1D23D06DA4DA3A
completedStages=3/3
completedRooms=9/9
ticks=20447
```

판정: **SAME-SEED MATCH**.

## 변경 파일

- `Assets/Scripts/Core/Simulation/RunManager.cs`
- `Assets/Tests/EditMode/Req104ContinueEconomyTests.cs`
- `Reviews/from-codex/req107-report.md`

요청대로 커밋하지 않았다.
