# REQ-115a 구현 보고서 — 보스 리디자인 Core 축 1차

- 담당: CODEX / SIMULATION
- 상태: PASS
- 커밋: 하지 않음 (오케스트레이터 커밋 대기)
- 승인 스펙: `Reviews/from-claude/boss-redesign-2026-08-03.md` 정독
- St3 전함: 기존 `WarshipEncounter` 상태기와 데이터 불변

## 1. 구현 결과

### 멀티파트 HP 페이즈 게이트

- `BossPhase.hpThreshold`를 정확한 정수 분수로 파싱한다. 런타임 비교는
  `remainingHp * thresholdDenominator <= maxHp * thresholdNumerator` 교차곱만 사용한다.
- 명시 임계를 쓰는 경우 2페이즈 이후의 모든 임계가 있어야 하며 엄격히 감소해야 한다.
  기존 데이터처럼 임계가 전혀 없으면 종전의 균등 HP 분할을 그대로 사용한다.
- `BossPhasePartRule`로 페이즈별 파츠 `active` / `invulnerable` / `attack`을 오버라이드한다.
  규칙이 없는 파츠는 기존 상시 활성 및 `partVulnerability`/core gate 규칙을 유지한다.
- 비활성 파츠는 `BossPartState.Active == false`이며 표시, 충돌, 피격, 공격이 모두 중단된다.
  다음 페이즈에서 활성화하면 보존된 HP로 노출된다.
- 파츠 공격 오버라이드는 `none`, 탄막, 이동, 소환, 흡입 및 신규 `laser`를 모두 사용할 수 있다.

### 레일건 / 극태빔 파츠

- `BossPartAttackType.Laser`와 `BossPartAttackProfile.LaserAttack`을 추가했다.
- 파츠 빔은 기존 `LaserAttackDefinition` / `LaserState`의 4단계(예고, 발사, 유지, 소멸)를 그대로 사용한다.
- `thinHalfWidth` / `fullHalfWidth`에 별도 상한을 두지 않아 화면 관통 특대 폭을 데이터로 지정할 수 있다.
- `LaserSourceKind.BossPart`를 추가했다. 이때 `LaserState.SourceEntityId`는 현재 형태의 zero-based
  `BossParts` 인덱스다. 파츠가 숨거나 파괴되면 해당 빔 소스가 사라져 기존 레이저 종료 경로로 정리된다.
- 공격 `intervalTicks`와 레이저 `cycleIntervalTicks`는 같아야 해 동일 소스의 빔 중첩을 데이터 단계에서 차단한다.

### St5 2형태 전환

- `BossFormDefinition` / `StagePlan.Form2` / `StageBossTemplate.Form2`와 `bosses[].form2` 파서를 추가했다.
- 형태 1 격파 시:
  1. 형태 1 점수(`scaled form maxHp * 2`)와 `EnemyKilled`를 즉시 지급/발행한다.
  2. `BossFormTransitionStarted`를 발행하고 `transitionTicks` 동안 본체 없는 무적 전환 상태가 된다.
  3. 카운트다운 종료 시 새 엔티티 ID, 별도 HP/크기/holdX/패턴/파트 배열로 형태 2를 생성한다.
  4. `BossFormChanged`와 호환용 `BossSpawned`를 함께 발행한다.
- `BossDefeated`는 전환 중 false이며 형태 2 최종 격파 후에만 true다.
- `StageCleared`는 최종 형태 격파 틱에만 발행한다.
- 형태별 `EnemyKilled`와 점수 지급이 분리되어 Presentation이 외갑 파괴와 최종 격파를 별도 연출할 수 있다.
- 전함과 form2의 동시 정의는 생성자/카탈로그 검증에서 거부한다. St3 전용 경로와 새 상태기의 결합을 금지했다.

## 2. 관측 계약

신규 public 관측:

- `IBattleSim.BossFormIndex` / `BossState.FormIndex`: 0=원형, 1=form2
- `IBattleSim.BossTransitioning`
- `IBattleSim.BossTransitionTicksRemaining`
- `BossPartState.Active`
- `SimEventType.BossFormTransitionStarted`
  - `EntityId`: 격파된 형태의 엔티티 ID
  - `Arg`: 전환 연출 틱
  - `PartId`: form2 콘텐츠 ID
- `SimEventType.BossFormChanged`
  - `EntityId`: 새 형태의 엔티티 ID
  - `Arg`: zero-based 형태 인덱스
  - `PartId`: 새 형태 콘텐츠 ID

기존 `BossPhaseChanged.Arg`는 계속 zero-based 페이즈 인덱스다. 이벤트가 발행될 때
`BossParts`에는 이미 새 페이즈의 활성/무적 구성이 반영되어 있다.

## 3. 서스펜드 / 리플레이 판단

- `RunSuspendData`는 기존 계약대로 **방/보스전 시작 경계로 되감아 재개**한다. 전투 도중 객체 그래프를
  직렬화하지 않으므로 별도 form/phase suspend 필드는 추가하지 않았다.
- 중간 틱의 판정 상태는 `BossFormIndex`, `Boss.Phase`, `BossTransitionTicksRemaining`,
  `BossPartState.Active/Invulnerable/Hp`로 완전 관측된다.
- 입력 리플레이는 같은 시드와 입력으로 위 상태를 재구성한다. `DeterminismAuditHasher`에
  form2 정의, HP 임계, 파츠 규칙/무장, 현재 형태/전환 카운트다운/파츠 활성 상태를 모두 포함했다.
- 신규 통합 테스트는 형태/페이즈 중간 상태를 거친 180틱 입력을 RLE 기록·재생하고 매 틱 누적한
  전체 BattleSim 해시가 일치함을 검증한다.

## 4. GROK용 `waves.json` 예시

아래는 보스 항목 내부에 들어갈 스키마 예시다. 실제 수치와 `GameData/waves.json` 반영은 GROK 소유다.

```json
{
  "parts": [
    {
      "id": "armor",
      "offsetX": 0,
      "offsetY": 0,
      "halfWidth": 3,
      "halfHeight": 8,
      "hp": 18000
    },
    {
      "id": "railgun",
      "offsetX": -1,
      "offsetY": 0,
      "halfWidth": 1.5,
      "halfHeight": 1,
      "hp": 6000
    },
    {
      "id": "core",
      "offsetX": 1,
      "offsetY": 0,
      "halfWidth": 2,
      "halfHeight": 3,
      "hp": 38000,
      "isCore": true
    }
  ],
  "phases": [
    {
      "pattern": "aimed",
      "fireIntervalTicks": 75,
      "ways": 5,
      "bulletSpeed": 4,
      "partRules": [
        {
          "partId": "railgun",
          "active": false,
          "invulnerable": true
        },
        {
          "partId": "core",
          "active": true,
          "invulnerable": true
        }
      ]
    },
    {
      "pattern": "radial",
      "fireIntervalTicks": 60,
      "ways": 8,
      "bulletSpeed": 4.5,
      "hpThreshold": 0.5,
      "partRules": [
        {
          "partId": "armor",
          "active": false,
          "invulnerable": true
        },
        {
          "partId": "railgun",
          "active": true,
          "invulnerable": false,
          "attack": {
            "type": "laser",
            "intervalTicks": 240,
            "laser": {
              "cycleIntervalTicks": 240,
              "telegraphTicks": 90,
              "firingTicks": 10,
              "sustainTicks": 120,
              "dissipateTicks": 20,
              "startOffsetX": 0,
              "startOffsetY": 0,
              "endOffsetX": -40,
              "endOffsetY": 0,
              "thinHalfWidth": 0.125,
              "fullHalfWidth": 6,
              "damage": 1
            }
          }
        },
        {
          "partId": "core",
          "active": true,
          "invulnerable": true
        }
      ]
    },
    {
      "pattern": "spiral",
      "fireIntervalTicks": 30,
      "ways": 12,
      "bulletSpeed": 5,
      "hpThreshold": 0.2,
      "partRules": [
        {
          "partId": "railgun",
          "active": false,
          "invulnerable": true
        },
        {
          "partId": "core",
          "active": true,
          "invulnerable": false
        }
      ]
    }
  ],
  "form2": {
    "id": "boss_core_prism",
    "transitionTicks": 180,
    "hp": 14000,
    "halfWidth": 5,
    "halfHeight": 4,
    "holdX": 13,
    "phases": [
      {
        "pattern": "spiral",
        "fireIntervalTicks": 30,
        "ways": 8,
        "bulletSpeed": 5
      }
    ]
  }
}
```

주의: multipart의 최상위 `hp`는 모든 `parts[].hp` 합과 정확히 같아야 한다. form2가 multipart라면
form2의 `hp`와 `form2.parts[].hp`에도 같은 규칙이 적용된다.

## 5. 테스트 및 감사

신규 테스트:

- `Parse_BossPhaseGateRailgunAndForm2CarryIntoPlan`
- `BattleSimTicksExplicitPhaseGateAndFiresExposedRailgunPart`
- `BattleSimTicksFormTransitionScoresEachBodyAndClearsOnlyFinalForm`
- `RecordedInputsReplayPhaseAndFormIntermediateStateExactly`

실전 통합 테스트는 public 데미지 훅을 호출하지 않는다. 실제 `BattleSim.Step`의 플레이어 발사체 이동과
충돌로 파츠 HP를 깎아 페이즈를 전환하고, 실제 레이저 수명 틱 및 형태 전환 틱을 진행한다.

최종 검증 결과는 아래 명령으로 재확인한다.

```text
cd Tools/CoreStandalone
dotnet test --no-restore --verbosity quiet

dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

- CoreStandalone: PASS 540/540, 실패 0, 건너뜀 0
- DeterminismAudit: 6/6 + cap-boundary 256/256, `AUDIT PASS`
- 동일 시드 2회: 감사 suite 및 신규 180틱 BattleSim 기록/재생 해시 일치
- 금지 항목: 신규 `UnityEngine`, `System.Random`, 벽시계, `Assert.Multiple` 사용 없음
