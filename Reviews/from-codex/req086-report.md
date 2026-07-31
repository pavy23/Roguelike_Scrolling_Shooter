# REQ-086 기체 무기 진화 3단계 + 계약 목적지 바이옴 보고서

- 작업일: 2026-07-31
- 담당: CODEX / SIMULATION
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## A. 기체 무기 진화 3단계

### 게이지와 관측 API

- 무기 모드 슬롯의 `maxLevel == 1` 제한을 제거했다.
- 같은 무기 모드를 다시 활성화하면 1씩 올라가며 최대 레벨에서 포화한다. 다른 모드를 선택하면 기존 모드는 0, 새 모드는 1부터 시작한다.
- 비용은 기존처럼 활성화 1회당 캡슐 1개로 유지했다.
- `Grant` 경로도 지정한 양만큼 무기 레벨을 올리되 최대 레벨에서 포화한다.
- 4슬롯 레거시/기본 정의의 Double/Laser/Triple 기본 상한을 3으로 변경했다.
- `GetGaugeSlotView`의 기존 `Level`/`MaxLevel`/`IsActiveWeaponMode` 관측을 그대로 사용하며 1~3단계를 테스트로 확인했다.

### 레벨 데이터 구조

- `PrimaryWeaponFamilyDefinition.Levels`와 `GetLevel(level)`을 추가했다.
- 레벨은 1부터 연속되어야 하며 최대 3단계다. 레벨 1은 기존 계열 기본 동작과 일치해야 한다.
- `weapons.json` 계열에 선택 필드 `levels`를 추가했다. 호환 별칭 `evolutionLevels`도 읽지만 두 필드를 동시에 쓰면 거부한다.
- 레벨별 선택 축:
  - `shotAngleLutSlots`, `spreadWays`, `spreadStepLutSlots`
  - `burstCount`, `burstIntervalTicks`, `pierceEnemyCount`
  - `pulseMinStepLutSlots`, `pulseMaxStepLutSlots`, `pulsePeriodTicks`
  - `inertiaVelocityPercent`
  - `impactExplosionDamage`, `impactExplosionRadius`
  - `minimumFireIntervalTicks`
  - `beamDamagePerTick`, `beamLength`, `beamStartHalfWidth`, `beamGrowthPerTick`, `beamMaxHalfWidth`
- 공간 수치는 파서에서 정수 sub-unit으로 변환하고 전투 중 계산은 정수/정수 유리수만 사용한다. 선택 필드가 없으면 기존 레벨 1 동작이다.

### 전투 동작

- Double:
  - L2는 데이터 각도 배열로 후방탄을 포함할 수 있다. 승인 수치의 LUT 32(180도)를 그대로 표현한다.
  - L3는 같은 전방 각도를 포함한 교차 각도 배열과 정수 틱 시차 버스트를 지원한다.
- Triple:
  - L2는 정수 틱 삼각파로 `pulseMinStepLutSlots`와 `pulseMaxStepLutSlots` 사이를 왕복하는 5-way 팬을 지원한다.
  - L3는 플레이어의 실제 틱 이동량에 `inertiaVelocityPercent`를 적용해 탄 초기 속도에 정수 가산하고, 레벨별 최소 연사 간격을 적용한다.
- Laser:
  - L2는 관통 수를 레벨별로 덮어쓰며, 관통 소진/최종 명중에서 `KillExplosionTriggered` 이벤트 경로를 재사용해 정수 반경 광역 피해를 준다.
  - L3는 기존 `LaserState`에 `LaserSourceKind.Player`를 추가해 텔레그래프 없이 발사 입력 틱에 즉시 생성한다. 유지 중 플레이어를 따라가고 반폭이 정수 틱마다 상한까지 증가한다.
  - 플레이어 빔은 일반 적과 레거시 보스 및 보스 파츠를 매 틱 판정하며, 발사 입력을 놓으면 제거된다. 적 레이저의 플레이어 충돌 경로에서는 제외된다.
- 진화 레벨, 미완료 버스트 수/쿨다운과 계약 목적지를 `DeterminismAuditHasher`에 포함했다.

## B. 계약 목적지 바이옴 결합

- `ContractOption`을 추가해 카탈로그의 `ContractDefinition`과 실행별 `DestinationThemeId`/`DestinationThemeStageIndex`를 분리했다.
- `RunManager.ContractOptions[i].DestinationThemeId`로 다음 바이옴을 직접 관측할 수 있다.
- 기존 Presentation 소비 코드와의 호환을 위해 계약 정의의 공개 속성을 전달하고 `ContractDefinition` 암시적 변환을 제공했다.
- 스테이지 2 진입 후보는 남은 셔플 풀의 2~4번 테마, 스테이지 3 진입은 남은 2개, 스테이지 4 진입은 남은 1개에서 결정론적으로 결합한다.
- 후보 수보다 남은 테마 수가 많거나 같을 때 목적지는 서로 다르다. 남은 테마가 하나면 승인 규칙의 "가능한 한"에 따라 중복한다.
- 선택 시 선택 테마를 다음 위치로 교환하고 밀려난 테마는 남은 풀에 유지한다. 따라서 이후 계약 후보와 실제 생성 순서가 같은 셔플 풀을 공유한다.
- `standard_route`도 같은 방식으로 목적지가 있으며 계약 보정만 중립이다.
- 스테이지 5 진입은 기존 고정 계획을 사용하고, `endRun`/`uncharted` 계약은 목적지 테마를 추가하지 않아 기존 규칙을 유지한다.
- 일반 `IStageGenerator`와 `IRouteStageGenerator` 양쪽에서 선택 목적지가 실제 `StagePlan.ThemeId`가 되도록 처리했다.

### 서스펜드/리플레이

- 계약 선택 기록에 `destinationThemeId`와 `destinationThemeStageIndex`를 저장하고 체크섬/복제/유효성 검사에 포함했다.
- 서스펜드 복원 시 계약 선택 순서대로 테마 교환을 재적용한 후 현재 스테이지를 생성한다.
- 리플레이는 목적지 결정을 기록·재생하고 현재 스키마에서 누락된 nextStage 목적지를 손상 데이터로 거부한다.
- 신규 테스트는 같은 시드의 후보 목적지/순서 일치, 가능한 후보의 상호 구별, 선택 목적지의 실제 다음 `StagePlan`, 서스펜드 복원 해시/테마 순서, 리플레이 선택 기록을 검증한다.

## 스키마 영향

- `weapons.json`: **schemaVersion 7 유지**. 모든 진화 축이 선택 필드이고 누락 시 기존 동작이므로 버전을 올리지 않았다.
- `InputRecordingData`: **18 → 19**. 목적지 결정이 없는 v18은 동일 항로를 재생할 수 없어 명시적으로 거부한다.
- `RunSuspendData`: **19 → 20**. 목적지 결정이 없는 v19는 동일 항로 상태를 복원할 수 없어 명시적으로 거부한다.

## GameData / BalanceSim 경계

- `GameData/`는 GROK 소유이므로 수정하지 않았다.
- 현재 `GameData/weapons.json`의 Double/Laser/Triple 게이지 `maxLevel`은 모두 1이며 계열별 `levels` 데이터도 아직 없다. 따라서 현재 데이터로 실행하면 기존 L1 동작을 보존한다.
- 현 데이터의 weapons schemaVersion 7 JSON에 선택 레벨 축을 삽입해 파싱하고 `BattleContent`까지 전달하는 구조 호환 테스트는 통과했다.
- GROK가 승인 수치와 각 슬롯 `maxLevel: 3`을 넣은 뒤에야 실제 BalanceSim 수치 검증이 가능하다. 이번 검증은 요청대로 데이터 도착 전 구조 호환 범위다.

## 신규/갱신 테스트

- `Req086WeaponEvolutionTests`
  - 무기 재활성화 1~3단계 및 HUD view
  - Double L3 교차 각도와 시차 버스트
  - Triple L2 정수 맥동 및 L3 정수 관성
  - Laser L2 관통 소진 폭발
  - Laser L3 즉시 생성/두께 성장/일반 적 피해/보스 피해/해제
  - 현 weapons schema 7에서 선택 레벨 축 파싱
- `RoguelikeCompletionTests`
  - 계약 목적지의 상호 구별, 같은 시드 결정론, 실제 다음 테마, 서스펜드와 리플레이 재현
- `WeaponExpansionTests`
  - 리플레이 19/서스펜드 20 버전 및 직전 버전 거부

## 검증 증거

### CoreStandalone 전체 테스트

명령:

```powershell
cd Tools/CoreStandalone
dotnet test --no-restore --logger "console;verbosity=minimal"
```

결과:

```text
통과! - 실패: 0, 통과: 426, 건너뜀: 0, 전체: 426, 기간: 815 ms
```

REQ-086 필터 결과도 별도로 8/8 통과했다.

### DeterminismAudit 전체 suite

명령:

```powershell
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

최종 결과:

```text
PASS seed-0-first           hash=871C1CF5B4077BB2 stages=5/5 rooms=15/15 ticks=25471
PASS seed-1-last            hash=443839C3A7B79B83 stages=5/5 rooms=15/15 ticks=27754
PASS seed-12345-rotating    hash=A637A367F381BC98 stages=5/5 rooms=15/15 ticks=28339
PASS seed-deadbeef-rotating hash=4B7724AA025AB26D stages=5/5 rooms=15/15 ticks=31834
PASS seed-max-prefer-capped hash=B1797F8CF1C13F92 stages=5/5 rooms=15/15 ticks=25995
PASS seed-7-hidden          hash=49527D347AAB143B stages=6/5 rooms=17/15 ticks=32462 grade=PerfectClear
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256 stage2BattleHash=matched stage3BattleHash=matched stage3RewardOptions=matched
AUDIT PASS
```

### 같은 시드 2회

두 실행에 사용한 명령:

```powershell
dotnet run --no-restore --no-build --project Tools/DeterminismAudit -- 12345 3 30000
```

결과:

```text
RUN_1 hash=E647FD5B1EF327AD completedStages=3/3 completedRooms=9/9 ticks=17238 finalStage=4
RUN_2 hash=E647FD5B1EF327AD completedStages=3/3 completedRooms=9/9 ticks=17238 finalStage=4
EXACT_MATCH True
```

### 정적 점검

```text
git diff --check: PASS
변경 Core의 System.Random / UnityEngine / Guid.NewGuid / DateTime.Now·UtcNow / Environment.TickCount 신규 사용: 없음
GameData 및 타 에이전트 소유 코드 변경: 없음
감사 실행으로 생성된 untracked Tools/DeterminismAudit/bin 출력: 제거 완료
```
