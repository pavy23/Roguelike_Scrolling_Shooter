# REQ-090 룸 경계 연속성 + 중간보스 침하 수정 보고

- 담당: CODEX / SIMULATION
- 작업일: 2026-08-01
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## 1. 오케스트레이터 진단 검증

진단은 두 항목 모두 맞았다.

- 룸 경계: `RunManager`는 각 룸마다 새 `BattleSim`을 만들었고, 기존
  `BattleContinuityState`는 플레이어 위치와 콤보만 전달했다. 따라서 직전 sim에
  남은 적/탄/픽업은 새 sim에 존재하지 않았고 `ScrollX`도 항상 0에서 다시 시작했다.
- 중간보스: `ConfigureBossMovementPhase`가 페이즈 전환 때마다 현재 Y와 직전 속도를
  새 `_bossMovementAnchorY`에 다시 합산했다. Stationary/VerticalSine 전환이 반복되면
  이 앵커가 한 방향으로 누적 이동하고, 마지막에는 플레이 영역 클램프에 붙었다.

수정 전 실패 관측:

| 관측 | 수정 전 값 |
|---|---:|
| 룸1 경계에서 폐기된 적 | 2 |
| 룸1 최종 ScrollX | 3 |
| 룸2 tick 0 ScrollX | 0 |
| 중간보스 450틱 창 평균 Y | 105, 499, 893, 1287, 1681, 2075, 2469, 2863, 3257, 3651 |
| 중간보스 장시간 Y 범위 | -256..3999 |

## 2. A — 룸 경계 연속성

### 선행 룸 정리

- 중간보스 직전 room 1, 스테이지 보스 직전 마지막 regular room, 히든 보스 직전
  마지막 hidden room을 `ShouldPrepareBossRoomBoundary()`로 결정한다.
- 이 룸들은 종료 60틱 전부터 예약 적/장애물 신규 스폰을 억제한다.
- 남은 적은 기존 보스 진입 정리와 같은 왼쪽 자연 퇴장 플래그를 사용하며, 퇴장 중
  추가 사격/레이저 시작을 막는다.
- 남은 적탄, 캡슐, 폭탄 픽업, 장애물도 같은 방향으로 화면 밖까지 이동한다.
- 정리 구간에서 죽은 적은 캡슐/폭탄을 새로 드롭하지 않는다.
- 종료 tick에 잔여 적/적탄/장애물/레이저/픽업이 하나라도 있으면 `RunManager`가
  경계를 넘지 않고 결정론적으로 몇 tick 더 기다린다. 회귀 테스트에서는 정지 적탄이
  남은 tick 80에 room 1을 유지하고, 전부 퇴장한 뒤에만 room 2로 전환됨을 확인했다.
  플레이어 탄은 계속 발사할 수 있으므로 경계 대기 조건에서 제외했다.
- 기존 보스 sim 내부의 40틱 스폰 억제/자연 퇴장 동작은 유지했다.

### ScrollX 승계

- `BattleContinuityState.ScrollX`를 추가하고 `CaptureContinuityState()`가 현재 절대
  ScrollX를 캡처한다.
- 새 `BattleSim`은 이를 `_scrollBaseOffset`으로 사용하며
  `GetScrollXAtTick(tick) = baseOffset + localScroll(tick)`을 반환한다.
- 같은 바이옴 룸뿐 아니라 보스 종료 후 다음 바이옴 첫 룸에도 최종 ScrollX를 전달한다.
  바이옴 경계에서는 플레이어 위치/콤보는 기존 정책대로 리셋된다.

수정 후 경계 관측표 (`speed=3/2`, 80틱 선행 룸):

| 시점 | sim tick | ScrollX | 적 수 | 관측 |
|---|---:|---:|---:|---|
| 정리 진입 직전 | 19 | 28 | 1 | 초기 적 존재 |
| 정리 구간 | 40 | 60 | 0 | 초기 적 자연 퇴장, tick 40 예약 스폰 억제 |
| 경계 직전 | 79 | 118 | 0 | 필드 정리 완료 |
| 이전 sim 최종 | 80 | 120 | 0 | 강제 증발 없음 |
| 다음 sim 시작 | 0 | 120 | 0 | ScrollX 연속, 보스 룸 빈 필드 시작 |

추가로 바이옴 경계 회귀에서 이전 보스 sim 최종 ScrollX 14가 다음 바이옴 room 1
tick 0의 14로 이어지고 플레이어 위치는 스폰점으로 리셋됨을 확인했다.

## 3. B — 중간보스 침하 수정

- `_bossMovementAnchorY`는 스폰 시의 Y로 고정하고 페이즈 전환 때 다시 쓰지 않는다.
- 새 파형은 직전 속도에 가장 가까운 위상을 결정론적으로 선택한다.
- 위치/속도 연속성을 위해 필요한 차이는 별도 transition offset으로 보정하고 30틱 동안
  정수 선형 감쇠해 고정 스폰 앵커로 복귀시킨다.
- 기존 `TimedMovementPhaseTransitionPreservesPositionDelta`도 그대로 통과한다.

같은 시드 장시간 회귀 관측:

| 관측 | 수정 전 | 수정 후 |
|---|---:|---:|
| 450틱 창 평균 Y | 105 → 3651 누적 | 13, 이후 20 고정 |
| 최대 절대 창 평균 | 3651 | 20 |
| Y 범위 | -256..3999 | -256..256 |

따라서 페이즈가 반복되어도 사인 호버 중심이 스폰 앵커에서 누적 이탈하지 않는다.

## 4. 리플레이 / 서스펜드 스키마 판단

- `InputRecordingData`: schema **19 → 20**. 룸 전환 tick 수와 관측 ScrollX/hash가
  달라지므로 v19를 거부한다.
- `RunSuspendData`: schema **20 → 21**. `stageStartScrollX`(DataMember order 59)를
  저장하고 체크섬에 포함한다. v20은 경계 재개 시 ScrollX를 재현할 수 없어 거부한다.
- suspend 재개 회귀에서 `stageStartScrollX=3`과 재개 sim의 ScrollX 일치를 확인했다.
- `GameData/*.json` 스키마 변경은 없다.

## 5. 회귀 테스트

신규/확장 검증:

- `ScrollContinuesAtRoomBoundary`
- `ScrollContinuesAcrossBiomeBoundary`
- `PreBossRoomSuppressesLateSpawnsAndDrainsBeforeBoundary`
- `CleanupWindowSuppressesCapsuleAndBombDrops`
- `BoundaryWaitsForResidualHostileBulletsToExit`
- `TimedMidBossHoverMeanRemainsNearSpawnAnchor`
- `SuspendAtCarriedRoomBoundaryRestoresContinuity` ScrollX 확장
- pre-REQ-090 replay/suspend 스키마 거부
- 정리 구간에 맞춘 기존 graze/무할당 combat 회귀 시점 갱신

Assets/Tests 신규 코드는 Unity NUnit 3 API만 사용하며 `Assert.Multiple`을 사용하지 않았다.
EditMode가 참조하는 신규 Core 타입/멤버는 public이다.

## 6. 최종 검증 증거

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS: 실패 0, 통과 463, 전체 463
```

### DeterminismAudit 전체 suite

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
PASS 6 scenarios
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

대표 해시:

```text
seed-0-first            A6FCD91D71134451
seed-12345-rotating     473099A1B15E975A
seed-deadbeef-rotating  483FD3AA6F9912BE
seed-7-hidden           345CDB0686812FF2
```

### 같은 시드 2회

```text
seed=12345 stages=3 tickBudget=30000
RUN_1 hash=FA6F4164022CEB13 ticks=16979 rooms=9/9
RUN_2 hash=FA6F4164022CEB13 ticks=16979 rooms=9/9
EXACT_MATCH True
```

### BalanceSim

```text
dotnet run --no-restore --project Tools/BalanceSim/VerifyThemeAssembly.csproj
PASS: all 50 stage×difficulty assemblies succeeded.
PASS: BalanceSim all checks green.
```

### 정적 검사

```text
git diff --check: PASS
System.Random / UnityEngine.Random / Guid.NewGuid / 벽시계 신규 사용: 없음
```
