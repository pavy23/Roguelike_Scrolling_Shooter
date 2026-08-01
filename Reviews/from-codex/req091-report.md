# REQ-091 무한 대기 수정 보고

- 담당: CODEX / SIMULATION
- 작업일: 2026-08-01
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## 재현 시드와 원인

재현 시드는 **0**이다. 실데이터와 레이저 3단계 연속 발사 입력으로 수정 전 REQ-090
종료 조건을 실행하면 `ticks=12000/12000`, `biome=1 room=1`,
`battleTick=12000`, `hash=03F75FE89A139B18`에서 틱 워치독에 걸렸다. 근본 원인은
`BattleSim.IsRoomBoundaryReady`의 주석과 달리 `_lasers.Count == 0`이 모든 레이저를
대기 대상으로 삼아, 발사 버튼을 누르는 동안 매 틱 유지되는 `LaserSourceKind.Player`
빔이 경계를 영원히 닫은 것이다. 같은 조건식에는 정지하거나 컬링 불가능한 적대
엔티티를 끝없이 기다리는 별도 안전장치도 없어서 상태 의존 영구 대기가 가능했다.

## 수정

- 경계 정리 조건은 `CountHostileLasers()`만 검사한다. 플레이어 빔은 플레이어 탄과
  동일하게 룸 전환을 막지 않는다.
- `BattleSim.RoomBoundaryMaximumWaitTicks = 300`을 public 결정론 상한으로 추가했다.
- 예정 룸 종료 뒤 대기 시간은 `RoomBoundaryWaitTicks`로 정수 틱만 사용해 계산한다.
- 적대/픽업 잔여물이 300틱 안에 정리되지 않으면
  `RoomBoundaryWaitLimitReached`가 true가 되어 강제 진행한다.
- 기존 자연 퇴장 경로는 그대로 우선하며, 정상 잔여 적탄은 상한 전에 정리된다.

## 회귀 테스트

- `BoundaryIgnoresContinuouslyFiringPlayerBeam`
- `BoundaryForcesProgressAtDeterministicWaitLimit`
- `ReproductionSeedZeroLaserRunCompletesDeterministically`
- `SeedsZeroThroughTwoHundredFiftyFiveCompleteStageOne`
- 기존 `BoundaryWaitsForResidualHostileBulletsToExit`도 유지 통과

신규 테스트는 NUnit 3 API만 사용하며 `Assert.Multiple`을 사용하지 않았다. 신규 Core
상한/관측 심벌은 모두 public이다.

## 워치독 시드 스캔

실데이터, 레이저 3단계 연속 발사로 room 1 경계를 통과한 뒤 Double 3단계로
스테이지 1을 끝까지 진행했다. 각 시드는 최대 12,000회의 `Step`으로 제한했고 테스트
프로세스에도 외부 실행 시간 워치독을 적용했다.

| 시드 | 통과 | 최소 완주 틱 | 최대 완주 틱 |
|---|---:|---:|---:|
| 0–31 | 32/32 | 3,080 | 5,795 |
| 32–63 | 32/32 | 3,065 | 5,789 |
| 64–95 | 32/32 | 3,092 | 5,768 |
| 96–127 | 32/32 | 3,118 | 5,800 |
| 128–159 | 32/32 | 3,057 | 5,800 |
| 160–191 | 32/32 | 3,053 | 5,744 |
| 192–223 | 32/32 | 3,100 | 5,776 |
| 224–255 | 32/32 | 3,152 | 5,735 |

고정 seed 0 수정 후 결과:

```text
seed=0 completed=True ticks=5735/12000 state=Playing biome=2 room=1
battleTick=0 hash=3828046BDFA6D79F
```

## 최종 검증

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS: 실패 0, 통과 467, 전체 467
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
RUN_1 hash=FA6F4164022CEB13 ticks=16979 rooms=9/9
RUN_2 hash=FA6F4164022CEB13 ticks=16979 rooms=9/9
EXACT_MATCH True
```

### 정적 검사

```text
git diff --check: PASS
Assert.Multiple 신규 사용: 없음
금지 난수/벽시계 API 신규 사용: 없음
```
