# REQ-087 보스 탄막 어휘 확장 보고서

- 작업일: 2026-07-31
- 담당: CODEX / SIMULATION
- 기준 커밋: `7d7c4a8`
- 결과: PASS
- 커밋: 요청대로 생성하지 않음

## 구현

### 공용 탄종 4종

- 보스 페이즈 선택 축 `BossProjectileKind`를 추가했다. JSON 선택 필드
  `projectileKind`가 없으면 기존 `normal` 콩알탄을 유지한다.
- Presentation 관측용 `BulletKind`를 `Heavy`, `Splitter`, `Mine`,
  `BossLaser`까지 확장했다.
- heavy
  - 공용 적탄 히트박스의 250%를 정수 계산해 사용한다.
  - 속도는 기존 페이즈 `bulletSpeed`를 그대로 소비하므로 느린 수치의 확정은 GROK 데이터
    소유로 남겼다.
- splitter
  - `splitAfterTicks`만큼 비행한 뒤 부모를 제거하고 LUT 기준 좌/중/우 3갈래
    `EnemyShot`으로 분열한다.
  - 분열 자탄도 `MaxEnemyBullets` 잔여 예산 안에서만 생성하며 부족하면
    `EnemyBulletCapacityExceeded`를 낸다.
- mine
  - `mineTravelTicks` 비행 → `mineTelegraphTicks` 정지 → 그 시점의 플레이어 좌표를
    정수 벡터로 고정 조준 → `mineAcceleration`으로 매 틱 가속한다.
  - 가속도 JSON 단위는 world-unit/s²이며 정수 유리수 subunit/tick²로 변환한다.
- boss laser
  - 기존 `LaserState`/`LaserAttackDefinition` 4단계(예고/발사/지속/소멸)를 재사용한다.
  - `LaserSourceKind.Boss`를 추가해 보스 위치에 앵커하고 기존 `MaxLasers` 상한 경로를
    그대로 통과시켰다.

### 보스 시그니처 5종

- `BossSignaturePattern`과 JSON 선택 필드 `signaturePatternId`를 추가했다.
  지원 id는 `scrapThrow`, `brood`, `laserGrid`, `lightning`, `prismCore`다.
- `scrapThrow`: `Breakable` 장애물을 보스 위치에서 투척하고, 페이즈 탄속과
  `signatureGravity`를 이용해 순수 정수 포물선 운동을 한다. `MaxObstacles`도 적용한다.
- `brood`: 페이즈 탄을 `signatureHomingTurnLutSlotsPerTick`만큼 약유도하고,
  `signatureSpawnEnemyId`(예: `hive_tentacle`)를 기존 적 소환 경로로 생성한다.
  알 수 없는 적 id는 파서와 `BattleSim` 생성 시 모두 거부한다.
- `laserGrid`: `bossLaser` 프로필을 상/하 대칭으로 동시에 시작한다. 각 빔을 독립적으로
  레이저 상한 검사하므로 두 번째 빔도 조용히 누락되지 않는다.
- `lightning`: 발사 시 플레이어 X 레인을 고정한 세로 boss laser를 시작한다.
- `prismCore`: 볼리 인덱스 기반 LUT 회전 빔 2기를 반대 방향으로 만들고, 페이즈의
  `radial` 발사 패턴을 함께 사용하면 코어 개방 링탄이 생성된다.
- 콘텐츠 파서는 1페이즈의 시그니처를 거부해 2~3페이즈 등장 문법을 강제한다.

### 텔레그래프·결정론 계약

- `BossAttackTelegraphed`의 기존 `Arg=phaseIndex`를 보존하면서 `SimEvent`에
  `BulletKind`, `SignaturePattern`, `TelegraphKind`를 추가했다.
- `TelegraphKind.Barrage`와 `TelegraphKind.Laser`를 분리해 Presentation이 앰버/적색을
  상태 추측 없이 선택할 수 있다.
- 페이즈의 모든 신규 필드, 런타임 탄종/시그니처/히트박스 배율, 텔레그래프 인자를
  `DeterminismAuditHasher`에 포함했다.
- 기존 JSON은 신규 필드가 전부 선택적이므로 schemaVersion을 올리지 않았고,
  `GameData/`는 GROK 소유라 수정하지 않았다.

## 추가 검증 테스트

`Req087BossVocabularyTests`와 `GameDataParserTests`에 다음 경로를 추가했다.

- normal/heavy/splitter/mine의 `BulletState.Kind` 관측과 heavy 정수 히트박스
- splitter N틱 후 3분열 및 자탄 예산 절단
- mine 비행/정지 예고/현재 플레이어 방향 가속
- boss laser의 `LaserSourceKind.Boss` 및 `MaxLasers=0` 거부
- 고철 포물선 장애물, 산란 약유도+촉수, 상하 레이저 그리드, 플레이어 레인 낙뢰,
  회전 프리즘 2기+4-way 링탄
- 5개 탄종 각각 동일 시드 결정론
- 5개 시그니처 각각 동일 시드 결정론
- 신규 JSON 필드 전체 파싱, 가속도/중력 exact fraction, 누락 시 legacy 기본값
- 텔레그래프의 탄종/시그니처/레이저 색 분류

## 검증 증거

### 전체 CoreStandalone 테스트

명령:

```powershell
cd Tools\CoreStandalone
dotnet test --no-restore
```

결과:

```text
통과!  - 실패: 0, 통과: 454, 건너뜀: 0, 전체: 454, 기간: 895 ms
```

REQ-087 전용 fixture 결과는 27/27 PASS이며, 별도 JSON 파서 테스트도 PASS다.

### DeterminismAudit 전체 suite

명령:

```powershell
dotnet run --no-restore --project Tools\DeterminismAudit -- --suite
```

결과:

```text
PASS seed-0-first              hash=43AD4456771CC78B
PASS seed-1-last               hash=3B7686086EBDBBC2
PASS seed-12345-rotating       hash=F73F2DB9BEAF090A
PASS seed-deadbeef-rotating    hash=201EC919DD2A19F5
PASS seed-max-prefer-capped    hash=52A4B535418F2A20
PASS seed-7-hidden             hash=7677FDD0FCD1D96B
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

### 같은 시드 2회

명령을 seed `12345`, stageCount `3`, tickCount `30000`으로 연속 실행했다.

```text
RUN_1 hash=E4F7A89904F9E8CE completedStages=3/3 completedRooms=9/9 ticks=17238
RUN_2 hash=E4F7A89904F9E8CE completedStages=3/3 completedRooms=9/9 ticks=17238
EXACT_MATCH True
```

### 적탄 예산·레이저 상한 경로

- heavy/splitter/mine 최초 볼리: 기존 `GetBossVolleyShotCount`가
  `MaxEnemyBullets - CountEnemyBullets()`로 절단하고 초과 이벤트를 낸다.
- splitter 자탄: 부모 제거 후 잔여 예산을 다시 계산해 최대 3개만 생성한다.
- 테스트 `EveryPointProjectileRespectsZeroEnemyBulletBudget`에서 heavy/splitter/mine
  각각 `MaxEnemyBullets=0`, 탄 0개, 초과 이벤트를 확인했다.
- 테스트 `SplitChildrenRespectEnemyBulletBudgetAndEmitCapacityEvent`에서 상한 2에 자탄이
  정확히 2개만 남고 초과 이벤트가 발생함을 확인했다.
- boss laser/laserGrid/lightning/prismCore는 전부 기존 `TryStartLaser`의
  `_lasers.Count >= _maxLasers` 검사로 진입한다.
- 테스트 `BossLaserUsesBossSourceAndHardLaserCap`에서 `MaxLasers=0`, 레이저 0개와
  `LaserCapacityExceeded`를 확인했다.
- 테스트 `LaserGridSynchronizesTwoBossLasersAndReportsTruncation`에서 상한 1일 때 첫 빔만
  존재하고 두 번째 빔 거부 이벤트가 발생함을 확인했다.

### 정적 검사

```text
git diff --check: PASS
신규 System.Random / UnityEngine.Random / Guid.NewGuid / 벽시계 API: 없음
GameData 변경: 없음
감사 실행 빌드 산출물 Tools/DeterminismAudit/bin: 제거 완료
```
