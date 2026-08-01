# REQ-098 구현 보고서 — 시드 기반 장애물 배치 지터

## 결과

- `SegmentStageGenerator.GenerateCore`에서 스테이지 RNG로부터 독립적인 장애물 지터 스트림을 분기했다.
- 일반 스테이지, 경로 조우, 섹션 경로 조우는 모두 `GenerateCore`를 통과하므로 세그먼트가 있는 전 스테이지에 적용된다. 세그먼트가 없는 거대 보스 전용 플랜은 적용 대상 장애물이 없다.
- 같은 `(seed, stageIndex, difficulty)`는 같은 장애물 좌표를 생성한다.
- 다른 시드는 실제 장애물 y 좌표를 바꾼다.
- 기존 세그먼트 및 보스 추첨 스트림은 변경하지 않았다.

## 구현 상세

- 공개 스트림 상수: `SegmentStageGenerator.ObstacleJitterStream = 3`
- 기존 스트림 유지:
  - segment selection = 0
  - boss selection = 1
  - theme permutation = 2
- 장애물 지터 스트림은 스테이지 RNG에서 별도로 `Fork`한다. 이후 세그먼트 위치와 장애물 인덱스별로 다시 `Fork`해 한 장애물의 추가/삭제가 다른 장애물의 지터를 연쇄 이동시키지 않게 했다.
- 정수 서브유닛 범위:
  - solid: y축 `[-128, +128]` = `±0.5u`
  - breakable: y축 `[-384, +384]` = `±1.5u`
  - laserEmitter: 요청에 지터 크기가 없고 빔 통로 위험이 있어 고정
- x축은 유지한다. `traversableLaneMasks`는 추상 차선 연결성만 갖고 실제 장애물 충돌 박스와의 공간 검증 정보를 제공하지 않으므로, 요청의 보수 대안에 따라 y축만 지터했다.
- 좌표 합산은 `long`에서 수행하고 `int` 범위로 포화해 오버플로를 방지한다.

## 세그먼트/보스 추첨 보존

장애물이 없는 카탈로그와 같은 세그먼트/가중치/보스를 가지되 장애물만 추가한 카탈로그를 시드 0~127에서 비교했다. 모든 시드에서 두 세그먼트 ID와 보스 ID가 일치한다. 지터는 별도 스트림이므로 기존 `SegmentSelectionStream`과 `BossSelectionStream` 소비 상태를 바꾸지 않는다.

## 리플레이/서스펜드 호환성

장애물 좌표는 입력에 기록되지 않고 시드에서 재생성된다. 따라서 기존 리플레이 v20 또는 서스펜드로 새 Core에서 재개하면 과거와 다른 충돌 배치가 만들어질 수 있어 호환으로 간주할 수 없다.

- `InputRecordingData.CurrentSchemaVersion`: 20 → 21
- `RunSuspendData.CurrentSchemaVersion`: 22 → 23
- 리플레이 v20은 명시적으로 거부한다.
- 지터 도입 전 서스펜드 v21/v22는 명시적으로 거부한다.
- 그 밖의 과거 스키마에 대한 기존 마이그레이션/거부 정책은 유지한다.

## 검증

### Standalone 전체 테스트

명령:

```text
cd Tools/CoreStandalone
dotnet test
```

결과: **PASS — 489 passed, 0 failed, 0 skipped**

추가/갱신 검증:

- 같은 시드 2회 생성 시 전체 플랜과 장애물 좌표 일치
- stageIndex 1~7 각각에서 다른 시드가 장애물 좌표를 실제로 변경
- x축 고정 및 solid/breakable y 지터 상한 검증
- 장애물 유무가 기존 세그먼트/보스 추첨을 바꾸지 않음을 시드 128개로 검증
- 리플레이 v20 및 서스펜드 v21/v22 거부 검증
- `Assert.Multiple` 미사용

### DeterminismAudit

명령:

```text
dotnet run --project Tools/DeterminismAudit -- --suite
```

결과: **AUDIT PASS**

- 6개 전체 관측 상태 시나리오 PASS
- 각 시나리오는 같은 시드를 독립적으로 2회 실행해 해시 일치를 확인
- cap-boundary 256 seeds PASS

주요 해시:

- seed 0: `89112C854824E007`
- seed 1: `A419968BBCDA958D`
- seed 12345: `9FBA70581DCF4A95`
- seed DEADBEEF: `2B4D1FE78B7B446D`
- seed max: `13F8509434FA9B3F`
- seed 7 hidden: `5441F22D442C0A6D`

## 인계

- 커밋하지 않았다. 오케스트레이터가 변경분을 검수 후 커밋한다.
