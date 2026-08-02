# REQ-120 플레이어 Y 가시 경계 회귀 보고서

- 담당: CODEX / SIMULATION
- 범위: `Assets/Scripts/Core/`, `Assets/Tests/EditMode/`
- 데이터 변경: 없음
- 커밋: 요청대로 생성하지 않음
- 결과: **CORE PASS / build29 직접 원인 가설은 반증** — 실데이터 기본 Y 경계는 이미 카메라 안이며, 넓은 외부 설정에서도 스테이지 전투가 가시 범위를 절대 벗어나지 않도록 Core 불변식을 추가했다.

## 1. build29 보고서와 실데이터 재현

`Reviews/from-tester/build29-sorting-fix-2026-08-03.md`를 UTF-8로 정독했다. 테스터의 결정적 증거는 St1/St2 late와 St3 전함에서 하강 입력 후 플레이어가 영구 소실되고 함미 HP도 감소하지 않았다는 것이다. 후보는 `BattleSim.ClampPlayerPosition`, `_playerMinY`, `_playerMaxY`였다.

현재 실데이터 구성값과 카메라 경계는 다음과 같다.

| 값 | subunit | world unit |
|---|---:|---:|
| 카메라/플레이필드 하단 | -2880 | -11.25 |
| 플레이어 hitbox 포함 안전 하단 | -2784 | -10.875 |
| 실데이터 기본 `PlayerMinY` | -2752 | -10.75 |
| 실데이터 기본 `PlayerMaxY` | 2752 | 10.75 |

따라서 현재 `GameDataSet.CreateBattleSimConfig()` 경로는 화면 하단보다 128 subunit(0.5u), hitbox 포함 안전 하단보다도 32 subunit(0.125u) 안쪽이다. seed 2 실데이터 런에서 600틱 하강과 1200틱 상승을 각 구간에 적용한 실제 시계열도 다음처럼 멈췄다.

```text
Opening   down min=-2752 (-10.7500u), up max=2752 (10.7500u)
MidBoss   down min=-2752 (-10.7500u), up max=2752 (10.7500u)
Closing   down min=-2752 (-10.7500u), up max=2752 (10.7500u)
StageBoss down min=-2752 (-10.7500u), up max=2752 (10.7500u)
warship   down min=-2752 (-10.7500u)
```

즉 build29의 실데이터와 같은 Core 구성에서 `PlayerY < -11.25u`는 재현되지 않았다. 실데이터 기본값만 놓고 보면 Y 클램프는 보고된 영구 렌더 소실의 직접 원인이 아니다.

### 화면 밖 하한이 열리던 구조적 조건

`BattleSimConfig`는 공개 가변 설정이고 기존 생성자는 그 `PlayerMinY/MaxY`를 그대로 신뢰했다. 따라서 스테이지 전투에 예를 들어 `±20u`를 주입하면 기존 `AdvanceDigitalPlayerAxis`/`ClampPlayerPosition`은 합법적으로 `PlayerY=-20u`까지 이동시켰다. 신규 실데이터 StagePlan 테스트가 이 외부 설정 취약 조건을 고정한다.

수정 후 같은 테스트의 관측값:

```text
wide-config down min=-2784 (-10.8750u)
wide-config up   max= 2784 ( 10.8750u)
```

## 2. 도입 이력과 관련성

- `58d22a9` (2026-07-28, REQ-005): 640x360 전환 때 기본 Y 경계를 ±6.5u에서 **±10.75u**로 확대했다. 카메라 ±11.25u 안쪽이므로 이 커밋도 화면 밖 하한을 만들지는 않았다.
- `5fb2b1c` (2026-07-30): 아날로그 이동과 현재 `ClampPlayerPosition`을 도입했다. 함수는 받은 min/max를 정확히 지킬 뿐 가시 범위를 별도로 강제하지 않았다.
- `4a128c0` (2026-07-30): corridor/drift를 도입했다. corridor는 `minimumY=Math.Max(_playerMinY, corridorMin+halfHeight)`, `maximumY=Math.Min(_playerMaxY, corridorMax-halfHeight)`로 기존 범위를 **좁히기만** 한다.
- REQ-103a (`9a962de`/`a9e2bac`)는 기존 스키마의 실데이터 재구성이며 Core Y 경계 대입을 추가하지 않았다. `traversableLaneMasks`는 생성 경로 검증/해시 입력이고 플레이어 좌표 clamp에 참여하지 않는다.
- `d5e5384` (REQ-115b)는 흡입력 델타를 추가했지만 최종 `PlayerY`는 같은 min/max로 다시 clamp된다.
- `e7c9a7c` (REQ-119)는 전함 진입 후 `ScrollX` 고정만 바꿨고 PlayerY 또는 경계에는 손대지 않았다.

따라서 late 구간, 통로/잠식, traversable lane mask, REQ-119가 실데이터 Y 하한을 -11.25u 아래로 여는 경로는 없다.

## 3. 수정

`SimSpace`에 공개 정수 API를 추가했다.

- `GetVisiblePlayerCenterMinY(int playerHalfHeight)`
- `GetVisiblePlayerCenterMaxY(int playerHalfHeight)`

스테이지가 활성화된 `BattleSim` 생성 시 구성 Y 경계를 위 안전 범위로 각각 clamp한다. 구성 범위가 화면과 전혀 겹치지 않아도 가장 가까운 가시 가장자리의 한 점으로 축약한다. 이어받은 continuity와 최초 spawn Y도 첫 틱 전부터 같은 유효 경계로 clamp한다.

통로 제약은 이후 매 틱 이 유효 범위와 다시 교집합을 취하므로 벽/침식 제약이 약화되지 않는다. 저수준 stage-disabled 테스트 배틀은 임의 좌표 공간을 계속 사용할 수 있어 수학/오버플로 단위 테스트 계약도 유지된다.

## 4. 신규 회귀 테스트

`Req120PlayerVisibleBoundsTests` 3건을 추가했다. NUnit 3 API만 사용하며 `Assert.Multiple`은 없다.

1. `RepositoryStageWithWideInjectedBoundsCannotLeaveVisiblePlayfield`
   - 실데이터 StagePlan + 의도적으로 넓힌 ±20u 구성.
   - 600틱 하강, 1200틱 상승의 모든 PlayerY 샘플이 hitbox 포함 가시 경계 안인지 확인.
2. `RepositoryNormalStageAllSectionsKeepSustainedMovementVisible`
   - 실데이터 seed 2 일반 스테이지의 Opening/MidBoss/Closing/StageBoss.
   - 각 구간에서 지속 하강/상승 모든 틱의 PlayerY 경계를 확인.
3. `RepositoryFortressDownwardRegressionRecoversAndDamagesStern`
   - 실데이터 seed 2, stage 3 fortress warship.
   - 600틱 하강 후 함미 Y로 복귀하면서 실제 주무기 입력을 유지.
   - ghost shot/직접 HP 변경 없이 `stern HP 2200 -> 2190`, 활성 후 120틱에 첫 명중.

기존 `StageGimmickTests`, `Req109GhostReplayTests`, `RunSuspendTests`, 전함 테스트를 포함한 전체 suite도 통과했다.

## 5. 검증

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS: 실패 0, 통과 554, 전체 554
```

### DeterminismAudit 전체

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
PASS 6 scenarios
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

대표 해시:

- `seed-0-first`: `2228B0BC8FF74827`
- `seed-12345-rotating`: `7A9ABDED2093ACE7`
- `seed-7-hidden`: `635BC252A796E942`

### 같은 시드 독립 2회

```text
seed=12345 stages=3 tickBudget=30000
RUN_1 hash=9EF16B716F696EEF ticks=21439 rooms=9/9
RUN_2 hash=9EF16B716F696EEF ticks=21439 rooms=9/9
EXACT_MATCH True
```

### 정적 검사

- 신규 `Assert.Multiple`: 없음
- 신규 `System.Random`, `UnityEngine.Random`, `Guid.NewGuid()`, 벽시계 API: 없음
- Core 신규 심벌: 모두 public
- `GameData/`, Presentation, 씬 변경: 없음

## 6. 리플레이/서스펜드 영향

- 실데이터 기본 경계 ±10.75u는 신규 안전 경계 ±10.875u보다 이미 좁으므로 정상 런, 기존 리플레이, 정상 suspend의 좌표/해시는 바뀌지 않는다.
- 저장 스키마 변경은 없다.
- 비정상적으로 화면 밖 Y를 가진 외부 continuity/suspend가 들어오면 생성 시 가시 경계로 복구된다.
- ghost recording에 저장되는 기존 구성 경계 필드는 그대로다. 정상 실데이터 값이 바뀌지 않으므로 재생 계약도 유지된다.

## 7. build29 최종 판단과 라이브 게이트

이번 수정으로 Core가 어떤 외부 Y 설정을 받아도 스테이지 플레이어 중심/히트박스가 카메라 세로 범위를 벗어나는 경로는 닫혔다. 그러나 build29와 동일한 실데이터 경로는 수정 전부터 이미 ±10.75u에 clamp됐으므로, 이 코드만으로 테스터가 본 영구 렌더 소실이 해소됐다고 단정할 수 없다.

다음 빌드에서는 dev overlay에 `PlayerX/PlayerY`와 최종 소비 `InputCommand`를 함께 표시해 하강 소실 프레임을 재캡처해야 한다.

- 소실 중 Core `PlayerY`가 -2752 부근이면 원인은 Presentation renderer/transform/입력 관측 경로다.
- Core `PlayerY`가 신규 안전 하단 -2784보다 작다면 이 테스트와 다른 빌드 소스가 사용된 것이다.
- 재중앙화 후 실제 함미 HP가 감소하면 Core 주무기 충돌 경로는 이번 테스트와 일치한다.

