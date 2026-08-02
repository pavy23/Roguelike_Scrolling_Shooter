# REQ-113 구현 보고서 — WarshipEncounter BattleSim 통합

- 작업일: 2026-08-02
- 담당: CODEX / SIMULATION
- 상태: **PASS**
- 커밋: 하지 않음 (오케스트레이터 커밋 대상)

## 결론

`StagePlan.WarshipEncounter`가 있는 전투에서 `BattleSim`이 REQ-110 상태 기계를
실제로 생성하고 매 틱 구동한다. 파츠 HP, 피격 가능 여부, 파츠 공격, 이벤트,
함수 개막 탄막이 하나의 encounter 상태를 공유하므로 기존의 “정의와 단위 테스트만
존재하고 실제 전투는 legacy multipart로 동작”하던 이중 경로를 제거했다.

실제 `GameData` 결정론 감사에서 최초 통합 후 함수 코어가 함체 스크롤을 따라 화면
밖으로 계속 이동하는 추가 결함도 발견했다. `finalCore` 활성 시 함수 코어룸을 기존
보스 `holdX`에 고정하도록 수정했으며, 이후 6개 장거리 시나리오가 모두 완주했다.

## 구현

### BattleSim 본배선

- 보스 스폰 시 스케일된 runtime 파츠 HP로 `WarshipEncounter`를 생성한다.
- 한 `BattleSim.Step`을 `BeginTick → 모든 실제 충돌/폭탄/레이저 피해 → CompleteTick`
  순서로 연결했다. 따라서 attrition 제한 틱의 피해까지 먼저 처리한 뒤 함수로
  전환하며, 같은 틱의 ordered damage 의미가 보존된다.
- WARNING 및 현재 그룹 밖 파츠는 `BossPartState.Invulnerable = true`이며 공격도
  중단된다. 기존 core gate/phase 취약성은 전함이 아닌 보스에만 적용된다.
- 함미 그룹 전멸 시 `MidBossDefeated`를 한 번 발행하고 함체 그룹을 즉시 연다.
- 함체 그룹은 `advanceAfterTicks`가 끝나면 남은 포탑을 비활성화하고 함수 그룹을
  연다.
- 함수 그룹 전환 시 전함을 `BossHoldX`에 고정해 코어룸을 화면 안에 유지한다.

### 이벤트와 관측

`BattleSim.EventsThisTick`에 다음 상태기계 이벤트를 전달한다.

- `WarshipWarningStarted` (41)
- `WarshipGroupActivated` (42)
- `WarshipCoreBattleStarted` (43, `Arg = CoreOpeningWays`)
- 함미 완료의 기존 `MidBossDefeated`

`IBattleSim`에는 다음 public 관측을 추가했다.

- `WarshipActiveGroupIndex`
- `WarshipDestroyedAttritionParts`
- `WarshipCoreOpeningWays`

결정론 감사에는 encounter tick, 그룹 elapsed, scroll remainder, 개막 탄막 소비 여부도
포함한다.

### 함수 개막 탄막

함수 core 파츠의 첫 projectile volley가 실제
`WarshipEncounter.ConsumeCoreOpeningWays()`를 사용한다.

- 포탑 0문 파괴: 9-way
- 포탑 2문 파괴: 5-way
- 포탑 4문 파괴: 3-way

첫 volley 이후에는 파츠 정의의 원래 ways를 사용한다. 이 소비 상태를 suspend에
저장하므로 복원 뒤 보상 개막 탄막이 다시 발사되지 않는다.

### 리플레이 / 서스펜드

- `WarshipEncounterSuspendData`를 schema v2로 올리고
  `coreOpeningConsumed`를 추가했다.
- `BattleSim.CaptureWarshipEncounterSuspendData()`와
  `RestoreWarshipEncounterSuspendData(...)`를 추가했다.
- restore는 일반 전투 리플레이가 동일 encounter tick까지 진행된 상태에서만 허용하고,
  파츠 HP/파괴/무적/공격 쿨다운/보스 합산 HP를 payload로 재동기화한다.

## 통합 테스트

기존 REQ-110 단독 상태기계 테스트를 유지하면서 다음 실제 `BattleSim` 경로 4개를
추가했다. `Assert.Multiple`은 사용하지 않았다.

1. `BattleSimTicksGateGroupsAndPublishWarshipLifecycleEvents`
   - 실제 ghost projectile 충돌로 비활성 포탑 무피해, 함미 격파, 그룹 전환,
     `MidBossDefeated` 단일 발행 검증
2. `BattleSimAttritionTimerAndDestroyedTurretsChangeOpeningVolley`
   - 실제 틱으로 시간 게이트 진행, 0문/4문 파괴의 9/3 이벤트 및 실제 적탄 수,
     함수 코어룸 hold 위치 검증
3. `RecordedBombInputsReplayWholeWarshipBattleExactly`
   - 폭탄 입력만 기록/재생해 함미→함체→함수→클리어 전체 BattleSim 해시 매 틱 일치
4. `BattleSimWarshipSuspendRestoresMidAttritionAndOpeningConsumption`
   - 함체 중간 JSON 왕복 복원 후 전체 battle hash 일치, 개막 소비 후 복원 시
     3/5/7-way 보상이 중복되지 않고 원래 9-way로 복귀하는 것 검증

## 검증 결과

### 전체 CoreStandalone

```text
dotnet test --no-restore --verbosity quiet
PASS 536/536, 실패 0, 건너뜀 0
```

### 실제 GameData 동일 시드 2회 결정론 감사

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
PASS 6/6 scenarios
PASS cap-boundary 256/256
AUDIT PASS
```

주요 전함 포함 경로:

```text
seed-0-first  hash=9658313643950081  completedStages=5/5
seed-1-last   hash=3467C7153AA045FB  completedStages=5/5
seed-7-hidden hash=171CD9DAB3B752BB  grade=PerfectClear
```

UnityEngine, `System.Random`, 벽시계, 비결정 순회는 추가하지 않았다.

