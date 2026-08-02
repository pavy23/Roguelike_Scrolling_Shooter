# REQ-110 Core 구현 보고서 — St3 거대 전함

- 일자: 2026-08-02
- 담당: CODEX / SIMULATION
- 상태: **PASS**
- 커밋: 하지 않음 (오케스트레이터 커밋 대상)

## 1. 구현 요약

기존 `BossPartDefinition`의 위치·HP·히트박스·`BossPartAttackProfile`을 그대로
사용하고, 그 위에 순서와 장기 상태만 담당하는 public
`WarshipEncounterDefinition` / `WarshipEncounter` 계층을 추가했다.

상태 전이는 다음과 같다.

1. `WARNING`: `warningTicks` 동안 모든 파츠 비활성
2. `midbossGate`: 함미 그룹 전 파츠 파괴 시 완료
3. `attritionLine`: 함체 포탑 노출 시간 `advanceAfterTicks` 동안 진행
4. `finalCore`: 함수 그룹 전 파츠 파괴 시 스테이지 완료

함체 그룹을 전 파츠 파괴 조건으로 만들면 포탑 파괴 수가 항상 최대가 되어 분기가
사라진다. 따라서 함미/함수는 파괴 게이트, 함체는 시간 게이트로 구분했다. 지나친
포탑은 HP를 유지하지만 비활성화되며, 실제 파괴된 포탑만 함수 개막 밀도 감소에
반영된다.

## 2. public Core 계약

- `WarshipGroupRole`: `MidbossGate`, `AttritionLine`, `FinalCore`
- `WarshipPartGroupDefinition`: 그룹 ID, ordered part IDs, 함체 노출 틱
- `WarshipEncounterDefinition`: WARNING, 전함 원점, 정확 스크롤 유리수,
  함수 개막 밀도 규칙, ordered 3그룹
- `WarshipEncounter`: 틱 진행, ordered damage 입력, 그룹 활성, 포탑 카운트,
  함수 개막 밀도, 파츠 world 좌표, 이벤트, 완료 상태
- `WarshipPartState`: 전함 로컬 오프셋 + 누적 스크롤로 산출한 world 좌표,
  HP/활성/무적 관측
- `WarshipEncounterSuspendData`: 전함 중간 상태 저장 스키마 v1
- `DeterminismAuditHasher.FoldWarshipEncounterState(...)`: 전함 상태 전용 감사 해시

`CoreOpeningWays` 계산은 다음 정수식이다.

```text
max(minimumCoreOpeningWays,
    baseCoreOpeningWays - destroyedAttritionParts * waysReductionPerTurret)
```

## 3. 기존 이벤트·outcome 접속 판단

- 함미 완료 틱에 기존 `SimEventType.MidBossDefeated`를 정확히 한 번 발행한다.
  `Arg`는 전함 encounter tick, `PartId`는 함미 그룹 ID다. 기존 구간 전환과
  미드스테이지 보상 연출이 같은 이벤트를 구독할 수 있다.
- 신규 표현 이벤트는 `WarshipWarningStarted`, `WarshipGroupActivated`,
  `WarshipCoreBattleStarted`다. 함수 개막 이벤트 `Arg`가 확정된 ways다.
- `midbossOutcome` 재사용은 하지 않았다. 이 값은 함미 격파 직후 후반 route를
  선택할 때 확정되지만, 포탑 수는 그 뒤 함체 구간에서 누적된다. 동일 필드를
  재사용하면 과거의 route 입력을 미래 정보로 다시 쓰게 된다. 후반 route 선택은
  기존 REQ-103b 파이프라인을 유지하고, 함수 탄막 보상은 `CoreOpeningWays`로
  독립시켰다.

## 4. waves.json 스키마

`bosses[]` 항목에 optional `warship`을 추가했다. GROK 소유 데이터는 수정하지
않았다. 예상 형태는 다음과 같다.

```json
"warship": {
  "id": "fortress_warship",
  "eventEntityId": 110,
  "warningTicks": 180,
  "originX": 24,
  "originY": 0,
  "scrollSpeedPerSecond": 3.0,
  "baseCoreOpeningWays": 9,
  "waysReductionPerTurret": 2,
  "minimumCoreOpeningWays": 3,
  "groups": [
    { "id": "stern", "role": "midbossGate", "partIds": ["engine"] },
    { "id": "hull", "role": "attritionLine",
      "partIds": ["turret_a", "turret_b"], "advanceAfterTicks": 600 },
    { "id": "bow", "role": "finalCore", "partIds": ["core"] }
  ]
}
```

파츠 배치·HP·무장은 기존 sibling `parts[]`에서 정의한다. 모든 파츠는 정확히 한
그룹에 속해야 하고, 기존 multipart 규칙대로 정확히 한 core가 있어야 하며,
core는 final 그룹에만 올 수 있다. 파서와 public 생성자가 잘못된 역할 순서,
중복/누락/미등록 파츠, 잘못된 시간·밀도 범위를 거부한다.

`StageBossTemplate.WarshipEncounter`에서 파싱 결과를 관측할 수 있고,
`SegmentStageGenerator`가 `StagePlan.WarshipEncounter`까지 보존한다. Supply
encounter는 보스를 제거하므로 전함 정의도 함께 제거한다.

## 5. 결정론·리플레이·서스펜드

- 전함 상태 머신은 난수를 사용하지 않는다. 입력은 호출 순서가 보존된
  `WarshipDamageCommand` 목록이며 같은 정의·초기 상태·명령열이면 같은 결과다.
- 스크롤은 정수 numerator/denominator와 remainder만 사용한다. 파츠 world X는
  `originX + localOffsetX - scrollOffset`이다.
- 배열 선언 순서만 순회하며 `Dictionary`/`HashSet` 순서에 의존하지 않는다.
- 리플레이는 기존 입력에서 동일한 ordered damage 명령열을 재생하면 된다.
  전용 same-seed 테스트가 매 틱 전체 상태/이벤트 해시 일치를 검증한다.
- 중간 저장은 `WarshipEncounterSuspendData` v1에 tick, scroll offset/remainder,
  active group/elapsed ticks, 포탑 파괴 수, WARNING/함미/완료 플래그, ordered part
  HP를 저장한다. 복원 시 schema, encounter ID, 범위, part 수, HP와 포탑 카운트
  일관성을 검증한다.
- 전함 payload는 독립 버전 계약이므로 기존 “현재 room 시작점으로 되감기” 방식의
  `RunSuspendData` 버전은 올리지 않았다. Presentation 저장 컨테이너가 전함 도중
  종료를 지원할 때 이 payload를 함께 직렬화하면 정확한 중간 상태로 돌아온다.
  JSON 직렬화 왕복 후 동일 명령을 계속 적용해 audit hash가 일치하는 테스트가 있다.

## 6. 검증 결과

### 전용 테스트

- WARNING 중 선행 데미지 무시
- 함미 → 함체 → 함수 순차 활성
- 기존 `MidBossDefeated` 발행
- 포탑 0개/2개 파괴에 따른 함수 ways 7/5 분기
- 전함 로컬 좌표 + 3/2 서브유닛 스크롤 remainder
- 중간 서스펜드 JSON 왕복 및 계속 실행 해시 일치
- 동일 seed 두 실행의 매 틱 전함 audit hash 일치
- 함수 파괴 후 `StageCleared`
- warship JSON 파싱/StagePlan 전달 및 중복 그룹 파츠 거부

### 전체 테스트

```text
dotnet test --no-restore
통과 529 / 실패 0 / 건너뜀 0
```

### 결정론 감사

```text
dotnet run --project Tools/DeterminismAudit -- --suite
6 scenarios PASS (각 동일 seed 2회)
cap-boundary 256 seeds PASS
AUDIT PASS
```

감사 대표 해시:

- seed 0: `D98528A4A50034AB`
- seed 12345: `B878363D5CF8980E`
- seed 7 hidden: `E1FF866E9D3EB83B`

## 7. 변경하지 않은 영역

- `GameData/waves.json`: GROK 소유이므로 실제 St3 수치/파츠는 추가하지 않음
- Presentation 장면·프리팹·연출: CLAUDE/RENDERER 소유
- 커밋: 오케스트레이터가 수행
