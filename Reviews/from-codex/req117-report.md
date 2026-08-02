# REQ-117 진단·회귀 보고서 — 실제 GameData WarshipEncounter 생성 경로

- 담당: CODEX / SIMULATION
- 상태: **CORE PASS / build25 Presentation 재검증 요청**
- 커밋: 하지 않음 (오케스트레이터 커밋 대기)
- 정독: `main/Reviews/from-tester/build25-warship-ghost-art-2026-08-03.md`

## 결론

현재 main 병합 HEAD에서는 보고된 “실제 RunManager 생성 경로에서
`StagePlan.WarshipEncounter`가 null”을 재현하지 못했다. 실제 저장소
`GameData/*.json`을 전부 파싱한 뒤 `RunManager`로 fortress 보스룸까지 진행하면
다음 연결이 모두 유지된다.

```text
waves.json warship
  → GameDataParser
  → StageGenerationCatalog / StageBossTemplate
  → SegmentStageGenerator
  → RunManager fortress 보스룸 StagePlan
  → BattleSim WarshipEncounter
```

seed 2의 stage 3 fortress와, seed 1에서 fortress가 stage 2로 셔플된 경우 모두
`fortress_warship`이 부착된다. 워닝 종료 후 함미 `engine`은 무적이 해제되어 실제
projectile collision으로 HP가 감소하고, 격파 틱에 기존 `MidBossDefeated`가 발행된다.

build25 테스터 보고서의 직접 관측은 dev 오버레이 미표시와 HP바 무변화다.
보고서 자체도 이를 “테스트 방법론 한계 또는 실제 통합 갭”으로 분류했으며,
`StagePlan.WarshipEncounter == null`을 메모리에서 직접 관측한 증거는 없다. 현재
`GameData/waves.json`과 런타임 복제본 `Assets/Resources/GameData/waves.json`의
SHA-256은 동일하다. 따라서 남은 build25 증상은 최신 Resources/scene을 포함하지
않은 빌드 또는 직렬화된 `WarshipView` 참조 문제일 가능성이 높다는 추론이다.

Core에서 재현되지 않는 조건에 임의의 로직 변경을 넣지는 않았다. 대신 수제
`StagePlan`으로만 검증했던 REQ-110/113의 사각을 막는 실제 데이터 E2E 테스트를
추가하고, Presentation 소유자에게 빌드 배선 재검증을 요청했다.

## 경로별 진단

### 1. 파서

`GameDataParser.Waves.cs`의 boss 파서는 실제 `source.warship`을
`ParseWarshipEncounter`로 변환하고 `StageBossTemplate` 생성자에 전달한다.

- `GameDataParser.Waves.cs:649`: `WarshipEncounterDefinition warship`
- `GameDataParser.Waves.cs:662`: `new StageBossTemplate(..., parts, warship, form2)`

실데이터 카탈로그의 `boss_fortress.WarshipEncounter.EncounterId`는
`fortress_warship`이다.

### 2. 카탈로그 → 생성기

`SegmentStageGenerator.GenerateCore`는 선택된 fortress boss의
`selectedBoss.WarshipEncounter`를 `StagePlan`에 전달한다
(`SegmentStageGenerator.cs:1277`). 실데이터로 직접 `GenerateRoute(...,
"fortress", Normal)`한 계획도 null이 아니다.

### 3. 생성기 → RunManager 보스룸

`RunManager`의 선택 경로에서 warship 전달은 현재 모두 보존된다.

- 셔플 진행도 보정: `RunManager.cs:5745`
- 계약 적용 계획 복제: `RunManager.cs:5884`
- 보스룸 전용 계획 생성: `RunManager.cs:6305`

실제 RunManager의 일반룸 계획은 의도적으로 보스를 제거하므로 warship이 null이다.
보스룸으로 전환되면 generator를 다시 호출한 뒤 `CreateBossOnlyPlan`이 encounter를
보존한다. REQ-117 테스트는 바로 이 실제 전환을 진행해 검증한다.

## 추가한 실데이터 E2E 테스트

파일: `Assets/Tests/EditMode/Req117WarshipIntegrationTests.cs`

### `RepositoryGameDataRunManagerFortressBossActivatesDamageableStern`

- 저장소의 `enemies/weapons/waves/rewards/ships/scoring.json`을 실제 로드한다.
- seed 2의 fortress 위치가 stage 3인지 확인한다.
- 파서 카탈로그와 `SegmentStageGenerator` 계획 양쪽에서 warship 부착을 먼저
  어서트한다.
- `RunConfig(3)`의 실제 RunManager를 일반룸부터 fortress 보스룸까지 진행한다.
- `StagePlan.WarshipEncounter == fortress_warship`을 어서트한다.
- 보스룸 틱을 워닝 종료까지 진행해 group 0 활성과 함미 무적 해제를 어서트한다.
- `TrySpawnGhostMainShot`을 사용해 실제 projectile collision으로 함미 HP 감소를
  먼저 확인하고, 다음 피격으로 격파한다.
- group 1 전환과 `MidBossDefeated` 발행을 어서트한다.

결과:

```text
seed=2 fortressStage=3 hash=D6DB1ACE1F090538
```

### `RepositoryGameDataShuffledFortressWarshipIsAttachedDeterministically`

- 0~63 시드 범위에서 fortress가 기본 stage 3이 아닌 순번에 배치되는 실제 시드를
  찾는다.
- 첫 표본은 seed 1, fortress stage 2다.
- 같은 파서→카탈로그→생성기→RunManager→BattleSim→함미 격파 경로를 두 번
  독립 실행한다.
- 최종 full-observable run hash와 함미 HP 전이를 모두 비교한다.

결과:

```text
shuffled seed=1 fortressStage=2
hash=59ADEB748EFEE7B8 repeat=59ADEB748EFEE7B8
```

`Assert.Multiple`은 사용하지 않았고, 테스트가 접근하는 Core 심벌은 모두 public이다.

## 전체 검증

### CoreStandalone

```text
dotnet test --no-restore --verbosity minimal
PASS 547/547, 실패 0, 건너뜀 0
```

### 실제 GameData 결정론 감사

```text
dotnet run --no-restore -- --suite
PASS 6/6 scenarios
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

주요 결과:

```text
seed-0-first  C878433F9A0CD73A  completedStages=5/5
seed-1-last   643F51AE3D36478E  completedStages=5/5
seed-7-hidden 7053520CD75F036D  grade=PerfectClear
```

금지 API 감사에서 실행 코드의 `UnityEngine.Random`, `System.Random`,
`Guid.NewGuid()`, `DateTime.Now`, `Environment.TickCount` 사용은 발견되지 않았다.
`Rng.cs`의 금지 규칙 설명 주석에 문자열 `System.Random`만 존재한다.

## 후속 요청

`Reviews/from-codex/requests.md`에 `REQ-117 → CLAUDE/RENDERER` 항목을 추가했다.

- 보스룸 진입 시 plan encounter id / WarshipView 직렬화 참조 / active 상태 동시 로그
- 최신 Resources 포함 scene 및 WebGL 재빌드
- `warship stern 1/3` 오버레이와 함미 HP 감소 라이브 재캡처

이 후속은 Presentation 소유 영역이므로 CODEX가 해당 파일을 수정하지 않았다.
