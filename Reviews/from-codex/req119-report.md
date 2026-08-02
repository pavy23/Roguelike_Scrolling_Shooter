# REQ-119 함미 게이트 플레이어 좌표/스크롤 연속성 보고서

- 담당: CODEX / SIMULATION
- 범위: `Assets/Scripts/Core/`, `Assets/Tests/EditMode/`
- 데이터 변경: 없음
- 커밋: 요청대로 생성하지 않음
- 결과: **CORE PASS** — 실데이터 seed 2 전함 룸 경계와 함미 게이트의 플레이어 좌표/스크롤 연속성 및 실제 오토파이어 명중을 고정했다.

## 1. build27 단서와 실데이터 재현

`Reviews/from-tester/build27-warship-hold-2026-08-03.md`를 UTF-8로 정독했다. 테스터가 확인한 핵심은 WARNING 전 마지막 샘플에는 플레이어가 있었으나, WARNING 이후 106초 동안 플레이어 시안 픽셀이 0이고 함미 HP/스코어가 고정된 현상이다.

저장소 `GameData`와 동일한 조건으로 다음 경로를 실행했다.

- seed: `2`
- 시작 스테이지: `3`
- 테마/보스/전함: `fortress` / `boss_fortress` / `fortress_warship`
- `RunManager`로 일반 룸을 실제 입력·보상·계약 선택과 함께 진행
- 마지막 일반 룸 최종 틱 → 전함 보스룸 tick 0 → WARNING → 함미 활성까지 매 틱 `PlayerX`, `PlayerY`, `ScrollX` 확인

수정 전 결과:

| 지점 | PlayerX | PlayerY | ScrollX |
|---|---:|---:|---:|
| 마지막 일반 룸 최종 상태 | -3328 | 1424 | 247914 |
| 전함 보스룸 tick 0 | -3328 | 1424 | 247914 |
| WARNING 첫 틱 | -3328 | 1424 | **247935** |

`PlayerX/Y` 자체는 룸 경계에서 정확히 승계됐고 warship 활성화 코드에도 플레이어 좌표를 쓰는 대입은 없었다. 오염 지점은 `BattleSim.GetScrollXAtTick()`이었다. `RunManager`가 만드는 전함 보스 전용 룸은 길이 1틱의 진입 세그먼트를 사용하지만, 기존 함수는 그 세그먼트가 끝난 뒤에도 일반 스테이지의 기본 스크롤 속도를 계속 적용했다. 반면 REQ-118 전함은 별도 `WarshipEncounter.WorldX`에서 `holdX`에 정지한다. 따라서 WARNING 시작부터 플레이어/카메라 기준 스크롤과 전함 기준점이 서로 다른 규칙으로 움직였다.

수정 전 신규 연속성 테스트는 첫 WARNING 틱에서 다음처럼 실패했다.

```text
scroll reference discontinuity at warship tick 1
Expected: 247914
But was:  247935
```

## 2. 수정

`BattleSim.GetScrollXAtTick()`에서 전함 정의가 있는 스테이지는 보스 발동 경계의 마지막 이전 프레임에 스크롤 틱을 고정한다.

- 전함 이전 일반 세그먼트의 기존 구간별 스크롤 계산은 유지한다.
- 보스 발동 시점부터 `ScrollX`는 더 진행하지 않는다.
- 전함의 접근/함미 hold/함체 전진/코어 고정은 계속 `WarshipEncounter.WorldX`가 단독 소유한다.
- `PlayerX/Y`, 입력값, 플레이어 clamp 범위는 전환 코드에서 변경하지 않는다.
- 일반 보스 및 일반 룸의 스크롤 규칙은 변경하지 않는다.

수정 후 seed 2 시계열:

```text
roomBoundary player=(-3328,1424) scroll=247914;
gate player=(-3328,1424) scroll=247914 warshipTick=180
```

룸 경계 tick 0부터 함미 활성 tick 180까지 모든 틱에서 위 플레이어 좌표와 스크롤 기준점이 동일하다.

## 3. 회귀 테스트

신규 `Req119WarshipPlayerContinuityTests` 2건을 추가했다. NUnit 3 API만 사용하며 `Assert.Multiple`은 없다.

### 3.1 `RepositoryFortressWarshipRoomAndGateKeepPlayerFrameContinuous`

- 실데이터 seed 2를 `RunManager`로 fortress 보스룸까지 진행한다.
- 마지막 일반 룸의 최종 `PlayerX/Y/ScrollX`와 새 보스룸 tick 0을 각각 assert한다.
- WARNING 시작부터 함미 활성까지 180틱 동안 매 틱 `PlayerX`, `PlayerY`, `ScrollX` 불변을 개별 assert한다.
- 함미 활성, 플레이어 생존도 확인한다.

### 3.2 `RepositoryFortressAutoFireDamagesSternSoonAfterGateActivation`

- 같은 실데이터 런에서 함미 Y를 향해 플레이어 `Fire` 입력만 유지한다.
- `TrySpawnGhostMainShot`, `WarshipDamageCommand`, 직접 HP 변경은 사용하지 않는다.
- 실제 주무기 생성 → 발사체 이동 → 보스 파츠 충돌 경로를 통과한다.
- 관측 결과: 보스룸에서 `23`발 발사, `3`회 명중, 함미 활성 뒤 `4`틱 내 HP `2180 → 2170`.
- 제한 `240`틱 안에 함미 HP 감소, ShotsFired/ShotsHit 증가를 각각 assert한다.

이 결과는 순수 Core에서 플레이어 자동사격과 함미 충돌 경로가 실제로 동작함을 증명한다. build27의 장시간 HP 고정이 수정 후 라이브에서도 해소됐는지는 새 빌드 실주행으로 다시 확인해야 한다.

## 4. 검증

### CoreStandalone 전체

```text
dotnet test --no-restore --nologo
PASS: 실패 0, 통과 551, 전체 551
```

### 결정론 감사

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
PASS 6 scenarios
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

대표 해시:

- `seed-0-first`: `2228B0BC8FF74827`
- `seed-12345-rotating`: `7A9ABDED2093ACE7`
- `seed-7-hidden`: `635BC252A796E942` (`PerfectClear`)

### 같은 seed 2 두 번

```text
RUN_1 hash=06695FD7CC9A323B ticks=25714 rooms=9/9
RUN_2 hash=06695FD7CC9A323B ticks=25714 rooms=9/9
EXACT_MATCH True
```

### 정적 검사

- `git diff --check`: PASS
- 신규 `Assert.Multiple`: 없음
- 신규 `System.Random`, `UnityEngine.Random`, `Guid.NewGuid()`, 벽시계 API: 없음
- 신규 Core API는 추가하지 않았고, 변경된 공개 `GetScrollXAtTick()` 계약은 결정론적 정수 계산만 사용한다.

## 5. 후속 라이브 검증 포인트

다음 build에서는 WARNING 직전/직후를 1틱 간격으로 캡처해 아래를 함께 확인해야 한다.

1. 플레이어 스프라이트가 계속 보이는가.
2. dev overlay의 `alive`가 유지되는가.
3. 함미 활성 후 첫 240틱 안에 HP 픽셀이 감소하는가.
4. 배경 스크롤이 WARNING과 함께 정지하고 전함만 자체 접근/hold 규칙으로 움직이는가.

Core 실데이터 테스트상 플레이어 좌표 승계와 오토파이어 명중은 모두 PASS이므로, 새 빌드에서도 플레이어 렌더만 소실되거나 입력이 멈춘다면 Presentation 입력/뷰 동기 경로를 별도로 조사해야 한다.
