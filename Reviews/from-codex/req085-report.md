# REQ-085 Core 보스 연속 등장·옵션 미사일 보고서

- 작업일: 2026-07-31
- 담당: CODEX / SIMULATION
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## A. 보스 등장 연속화

- `BossPostClearDelayTicks`와 필드 클리어 대기 상태를 제거했다.
- 보스 등장 40틱 전(`stageTotalTicks - 40`)부터 신규 예약 스폰을 억제하고, 그 시점에
  살아 있던 적에는 기존 자연 퇴장 플래그를 붙인다.
- 보스는 정확히 `stageTotalTicks`부터 화면 우측 밖에서 활강 진입한다. 잔여 적 수를
  확인하지 않으므로 적이 남아 있어도 공존한 채 등장한다.
- 보스 등장 후에도 이미 퇴장 플래그가 붙은 잔여 적은 계속 왼쪽으로 이동한다. 보스가
  이후 새로 소환한 적에는 이 플래그를 붙이지 않는다.
- 스폰 억제 리드를 90틱에서 40틱으로 변경했다.
- 제거된 60틱 정적 대기에 맞춰 시간제한의 `+60` 보정을 롤백했다. 설정된
  `timeLimitTicks`를 그대로 사용한다.
- 중간/스테이지/히든 보스는 모두 동일한 `StagePlan` → `BattleSim.UpdateBoss` 경로를
  사용하므로 같은 규칙이 적용된다.

회귀 테스트 `BossEntersAtFortyTickLeadWhileSurvivingEnemiesRetreat`에서 다음을 검증했다.

- 억제 경계의 신규 스폰이 생성되지 않음
- 보스가 stage total 이전에는 등장하지 않고 정확히 stage total에 등장함
- 보스 등장 틱에 잔여 적이 존재해도 `BossSpawned`가 발생함
- 보스 등장 전후 잔여 적의 자연 퇴장이 계속됨
- 시간제한 260틱이 320틱으로 연장되지 않고 그대로 260틱임

## B. 옵션 미사일 미러

- 미사일 볼리를 본체 미사일 → Option index 오름차순으로 생성한다.
- 각 미사일을 추가하기 전에 기존 플레이어 탄 `MaxBullets` 예산을 확인한다. 예산이
  부족하면 뒤쪽 옵션부터 결정론적으로 잘린다.
- 옵션 미사일은 해당 옵션의 현재 정수 좌표에서 생성한다.
- `weapons.json` 루트 선택 필드 `optionMissileDamagePercent`를 파싱한다. 누락 시 기본값은
  100이며 음수는 거부한다. 실제 JSON과 밸런스 값은 GROK 소유이므로 수정하지 않았다.
- 본체 미사일은 100%, 옵션 미사일은 설정 비율을 탄 상태에 보존한다. 레벨 배율 적용 후
  정수 퍼센트를 내림 계산하며 `int` 상한에서 포화한다.
- 이 비율은 일반 적, 보스/보스 파츠, 장애물, 확산 폭발 피해에 동일하게 적용된다.
- 유도 미사일은 각 탄이 기존의 거리 우선·동률 시 낮은 enemy id 우선 타깃 선정 경로를
  그대로 사용한다.
- 탄의 피해 비율을 `DeterminismAuditHasher`에 포함했다.

추가 테스트:

- `OptionMissileVolleyMirrorsInIndexOrderAndTruncatesAtBulletBudget`
  - 본체 + 옵션 6기 상태에서 본체/옵션 순서, 좌표, 피해 비율, MaxBullets 절단 검증
- `OptionMissileDamagePercentScalesCollisionDamage`
  - 본체 10 피해, 옵션 50% = 5 피해의 실제 충돌 결과 검증
- `OptionalOptionMissileDamagePercentFlowsIntoBattleConfig`
  - 선택 필드 42가 `BattleSimConfig`까지 전달되고 누락 시 100인 것을 검증

## 스키마 영향 판단

### weapons.json

- `optionMissileDamagePercent`는 선택 필드이며 누락 기본값이 100이므로 지원 중인 weapons
  schemaVersion 자체는 올리지 않았다.
- `GameData/` 원본은 GROK 소유이므로 이번 변경에 포함하지 않았다.

### 리플레이/서스펜드

직렬화 필드 모양은 바뀌지 않지만, 옵션과 미사일을 가진 상태의 동일한 Fire 입력이 이제
추가 미사일을 생성한다. 구 데이터를 새 규칙으로 재생/재개하면 동일 입력의 전투 결과가
달라지므로 기존 프로젝트 정책에 따라 직전 버전을 명시적으로 거부했다.

- `InputRecordingData`: schema 17 → **18**, v17 거부
- `RunSuspendData`: schema 18 → **19**, v18 거부

서스펜드는 전투 중 탄을 저장하지 않고 현재 룸 경계의 tick 0 상태를 저장하지만, 그
경계에서 재개한 뒤의 Fire 의미가 달라지므로 동일하게 버전을 올렸다.

## 검증 증거

### CoreStandalone 전체 테스트

명령:

```powershell
cd Tools/CoreStandalone
dotnet test --no-restore
```

최종 결과:

```text
통과!  - 실패: 0, 통과: 418, 건너뜀: 0, 전체: 418, 기간: 802 ms
```

### DeterminismAudit 전체 suite

명령:

```powershell
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

최종 결과:

```text
PASS seed-0-first           hash=FCE0F3104D07E9B1 stages=5/5 rooms=15/15
PASS seed-1-last            hash=4AABAD84C2DA266B stages=5/5 rooms=15/15
PASS seed-12345-rotating    hash=EC8C0316B9242E4B stages=5/5 rooms=15/15
PASS seed-deadbeef-rotating hash=F10BE91B4C484EB9 stages=5/5 rooms=15/15
PASS seed-max-prefer-capped hash=1CD46DA057AD9329 stages=5/5 rooms=15/15
PASS seed-7-hidden          hash=91B4736EE63D20DA stages=6/5 rooms=17/15
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

### 같은 시드 2회

명령을 seed `12345`, stageCount `3`, tickCount `30000`으로 연속 두 번 실행했다.

```powershell
dotnet run --no-restore --project Tools/DeterminismAudit -- 12345 3 30000
dotnet run --no-restore --project Tools/DeterminismAudit -- 12345 3 30000
```

결과:

```text
RUN_1 hash=9FA70BDA597C9444 completedStages=3/3 completedRooms=9/9 ticks=17238
RUN_2 hash=9FA70BDA597C9444 completedStages=3/3 completedRooms=9/9 ticks=17238
EXACT_MATCH True
```

### 정적 검사

```text
git diff --check: PASS
BossPostClearDelayTicks / _bossFieldClearTick / 90틱 리드 잔여: 없음
금지 난수·벽시계 API 신규 사용: 없음
```
