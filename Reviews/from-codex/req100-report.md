# REQ-100 보고서 — 옵션 주무기 발사 기아 수정 + 포메이션 전환 점검

작성일: 2026-08-02  
담당: CODEX / SIMULATION

## 결론

- 옵션 탄 예산 정책을 **볼리 단위 전원 보장(all-or-none)** 으로 변경했다.
- 주무기와 미사일 모두 본체+모든 옵션이 한 단위로 발사되며, 남은 플레이어 탄 예산이 전체 볼리를 수용하지 못하면 어느 발사체도 만들지 않고 대기한다.
- `MaxBullets`는 하드 상한으로 유지된다. 임의의 상한 상향으로 문제를 늦추는 방식은 사용하지 않았다.
- 옵션 포메이션 보상 적용 후 주무기/미사일 발사, 위치 재배열, 룸 전환 유지, 바이옴(스테이지) 전환 유지를 실제 `RunManager` 전환 테스트로 확인했다. 포메이션 전환 경로의 Core 버그는 발견되지 않았다.

## A. 옵션 기본샷 기아 수정

### 원인

기존 `SpawnMainShotVolley`는 본체를 먼저 발사한 뒤 옵션을 인덱스 순서로 순회하면서 매 옵션마다 `CountPlayerBullets() < _maxBullets`를 재검사했다. 예산 경계에서는 본체와 낮은 인덱스 옵션만 발사되고 뒤쪽 옵션은 반복해서 잘렸다. Spread는 한 발사원 내부에서도 남은 예산만큼만 잘라 발사했다.

미사일 옵션 루프도 같은 인덱스 편향을 갖고 있어 동일 정책으로 함께 수정했다.

### 선택한 정책과 계산

상한을 192 등 특정 값으로 올리는 방식은 현재 조합만 늦출 뿐, 다방향 진화 무기와 미사일이 같은 플레이어 탄 예산을 공유할 때 다시 부분 볼리가 생길 수 있다. 따라서 다음 필요량을 `long`으로 먼저 계산하고 전체가 들어갈 때만 발사한다.

- 단발 주무기: `(본체 1 + 옵션 6) × 1 = 7발`
- 현재 최대 5-way 주무기: `(본체 1 + 옵션 6) × 5 = 35발`
- 미사일: `(본체 1 + 옵션 6) × 1 = 7발`
- 64발 예산의 최고연사 단발 회귀 케이스: 완전한 7발 볼리 9회 = 63발, 다음 볼리는 공간이 생길 때까지 전원 대기

이 정책은 옵션 인덱스와 무관하며 난수나 순회 순서에 의존하지 않는다.

### 상한·오버플로·성능

- 플레이어 탄 수는 계속 `MaxBullets` 이하이므로 기존 리스트/히트 기록 사전할당 계약을 깨지 않는다.
- 기존 `(long)config.MaxBullets + config.MaxEnemyBullets > int.MaxValue` 합산 오버플로 가드를 그대로 유지했다.
- 새 볼리 필요량 계산도 `long` 곱셈/뺄셈으로 수행해 정수 오버플로를 피한다.
- 플레이어 탄-적/장애물/보스 충돌 루프의 최대 플레이어 탄 수는 변하지 않으므로 최악 작업량은 증가하지 않는다.
- 포화 시 남는 슬롯보다 볼리가 크면 일부 슬롯을 비워 두므로 실제 충돌 후보 수는 기존과 같거나 더 적다.
- 옵션 루프 안에서 반복하던 `CountPlayerBullets()` 전수 스캔을 제거해 발사 판정 측의 중복 O(B) 스캔은 감소했다.

## B. 포메이션 전환 점검

신규 통합 테스트는 보상 카드로 `Trail -> Fixed`를 적용한 뒤 새 `BattleSim`을 실제로 생성한다.

1. `OptionFormationRewardRepositionsAndKeepsWeaponsAcrossRoomTransition`
   - 바이옴 1의 룸 2 미드 보상에서 Fixed 선택
   - 룸 3 진입 후 6기 모두 지정 오프셋으로 재배열 확인
   - 본체+옵션 6기의 주무기 7발 및 미사일 7발 실생성 확인
2. `OptionFormationRewardPersistsAndKeepsWeaponsAcrossBiomeTransition`
   - 바이옴 1 메인 보상에서 Fixed 선택 후 계약 선택
   - 바이옴 2 / 룸 1 진입 뒤 포메이션 유지와 위치 재배열 확인
   - 본체+옵션 6기의 주무기 7발 및 미사일 7발 실생성 확인

기존 근거 테스트도 계속 통과한다.

- `OptionFormationsProduceDeterministicPositions`
- `ShipOptionFormationSeedsRunSuspendAndReplay`
- `SuspendAndRecordingPreserveLoadoutAndLegacyDefaults`

## 신규·갱신 회귀 테스트

- `SixOptionMainVolleyWaitsRatherThanTruncatingAtBulletBudget`
- `OptionMissileVolleyWaitsRatherThanTruncatingAtBulletBudget`
- `SixOptionsAtHighestFireRateSpawnOnlyCompleteMainShotVolleys`
- `OptionFormationRewardRepositionsAndKeepsWeaponsAcrossRoomTransition`
- `OptionFormationRewardPersistsAndKeepsWeaponsAcrossBiomeTransition`

`Assert.Multiple`은 사용하지 않았다.

## 검증 결과

### CoreStandalone 전체

```text
dotnet test --no-restore
통과: 492, 실패: 0, 건너뜀: 0
```

### DeterminismAudit 전체 suite

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
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
hash=E72DF1B5E3690F0F
completedStages=3/3
completedRooms=9/9
ticks=20753
```

판정: **SAME-SEED MATCH**.

## 변경 파일

- `Assets/Scripts/Core/Simulation/BattleSim.cs`
- `Assets/Tests/EditMode/BattlePowerUpSimTests.cs`
- `Assets/Tests/EditMode/WeaponExpansionTests.cs`
- `Reviews/from-codex/req100-report.md`

커밋은 요청대로 수행하지 않았다.
