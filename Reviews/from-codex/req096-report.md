# REQ-096 CODEX 구현 보고서

작성일: 2026-08-01  
담당: CODEX / SIMULATION  
커밋: 하지 않음 (오케스트레이터가 수행)

## 결과

QA/PLAYTESTER 전용 stage 선택과 플레이어 무적 게이트를 Core에 추가했다.
모든 신규 심벌은 `Shmup.Core.Simulation`의 public API다.

- `RunConfig.StartStageIndex`
  - 기본값 `1`
  - `new RunConfig(3)`을 전달하면 biome/stage 3, room 1에서 기본 파워 상태로 시작한다.
  - 난이도는 실제 진행 위치인 stage 3 기준으로 계산한다.
  - 앞 stage의 클리어 수, 방 보상, 계약, 통계는 소급 지급하지 않는다.
  - 사망 후 `Restart`도 구성된 시작 stage로 돌아간다.
  - 구성된 `RunProgressionConfig.BiomeCount` 범위를 벗어나면 생성 시 거부한다.
- `BattleSimConfig.PlayerInvulnerable`
  - 기본값 `false`
  - `true`이면 적/탄/레이저/장애물 충돌 피해가 shield를 소모하거나 플레이어를 사망시키지 않는다.
  - time-limit 만료 이벤트는 유지하되 shield 소모와 사망은 무시한다.
  - 그레이즈, 점수, 사격/킬/수집 통계와 전투 tick은 기존대로 진행한다.
- `RunManager.DevFlagsActive`
  - `StartStageIndex != 1` 또는 `PlayerInvulnerable == true`이면 `true`다.
  - Presentation이 이 값을 읽어 `MarkCheatUsed` 및 제출 차단에 연결할 수 있다.

Presentation의 현재 일반 런 생성 형태를 유지하면서 마지막 인자로
`RunConfig`을 받을 수 있도록 다음 계열의 오버로드를 제공했다.

- 기본 `RunManager(..., PowerUpGauge, RunConfig)`
- 데이터 카탈로그 `RunManager(..., RewardCatalog, ContractCatalog, RunConfig)`
- 현재 BattleDirector 형태 `RunManager(..., RewardCatalog, ShipDefinition, difficultyNumerator, difficultyDenominator, RunConfig)`
- 전체 진행 구성 `RunManager(..., RunProgressionConfig, RunConfig)`

## 서스펜드 판단

`RunSuspendData`, 저장 무결성 버전, 마이그레이션 코드는 수정하지 않았다.

dev 런은 제출 불가 QA 세션이고 재개 요구가 없으므로,
`RunManager.ExportSuspendData()`는 `DevFlagsActive == true`일 때
`InvalidOperationException`으로 명시적으로 거부한다. 이로써 start-stage 정보가 없는
기존 스키마로 dev 런이 정상 런처럼 복원되는 경로를 만들지 않았다.
기본 플래그 런의 export/resume 계약은 그대로 유지된다.

## 결정론 및 하위 호환

- 두 플래그의 기본값은 각각 stage `1`, invulnerable `false`다.
- 기본 생성자와 명시적 `RunConfig.CreateDefault()` 생성자의 초기 전체 상태 해시가
  동일함을 테스트했다.
- `DeterminismAuditHasher`는 `DevFlagsActive == true`인 경우에만 dev 표식을 추가로
  fold한다. 기본 런은 추가 바이트를 전혀 fold하지 않으므로 기존 해시 형식을
  byte-for-byte 유지한다.
- 난수 스트림, stage permutation, 입력 기록, replay payload, suspend DTO는 변경하지 않았다.

## 테스트 추가

- 요청 stage 3 시작, stage 기준 난이도, room 1, 기본 파워, 0 클리어 통계
- invulnerable 피격 시 shield/life/state 유지 및 `DevFlagsActive`
- invulnerable 상태에서도 그레이즈 점수/콤보/통계/이벤트 정상 동작
- 기본 생성자와 명시적 기본 `RunConfig` 상태 해시 일치 및 suspend export 허용
- dev run suspend export 거부
- 시작 stage 하한/진행 범위 검증
- dev run 사망 후 restart가 구성된 stage로 복귀
- 실제 `GameData` + 기본 파워 레벨 0 + stage 3 + invulnerable 통합 스모크

실데이터 스모크 관측값:

- seed: `0x960096`
- 시작: stage 3 / room 1 / 모든 power-up level 0
- stage 3 biome boss 진입: `5,128` ticks
- 진입 시 상태: `Playing`, player alive, room 3 boss battle
- 누적 score: `21,235`
- 누적 graze: `61`

## 검증 결과

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS: 485 / 485
FAIL: 0
SKIP: 0
```

### DeterminismAudit 전체 suite

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
6 / 6 scenarios PASS
cap-boundary PASS (256 seeds)
AUDIT PASS
```

### 같은 시드 2회 (dev flags off)

```text
dotnet run --no-restore --no-build --project Tools/DeterminismAudit -- 12345 3 30000
run 1: hash=09CFB98E4172D2C7 ticks=19929 completedStages=3/3
run 2: hash=09CFB98E4172D2C7 ticks=19929 completedStages=3/3
MATCH
```

## 변경 파일

- `Assets/Scripts/Core/Simulation/RunManager.cs`
- `Assets/Scripts/Core/Simulation/BattleSim.cs`
- `Assets/Scripts/Core/Simulation/DeterminismAuditHasher.cs`
- `Assets/Tests/EditMode/RunManagerTests.cs`
- `Assets/Tests/EditMode/BattleScoringTests.cs`
- `Assets/Tests/EditMode/Req096DevRunTests.cs`
- `Assets/Tests/EditMode/Req096DevRunTests.cs.meta`
- `Reviews/from-codex/req096-report.md`
