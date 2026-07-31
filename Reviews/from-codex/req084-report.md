# REQ-084 Core 옵션 최대 6기 지원 보고서

- 작업일: 2026-07-31
- 담당: CODEX / SIMULATION
- 기준 main: `d134e04`
- 결과: PASS

## 구현

- `PowerUpGauge.MaximumOptionCount = 6`을 추가하고 Core 기본 게이지의 Option 최대 레벨을 4에서 6으로 확장했다.
- 구버전 weapons 스키마의 Option effect soft cap 기본값을 4에서 6으로 확장했다.
- fixed 편성 기본 오프셋을 6기까지 정의했다.
  - X: `192, 192, 192, 192, 192, 192` subunits
  - Y: `384, -384, 704, -704, 1024, -1024` subunits
- `weapons.json` fixed 편성 파서는 기존 4개 오프셋과 신규 6개 오프셋을 모두 수용한다. 기존 콘텐츠 호환성을 유지하면서 GROK의 6기 데이터가 병합되면 그대로 소비할 수 있다.
- trail 이력 링버퍼는 기존부터 `maxOptionLevel * OptionFollowDelayTicks + 1`로 계산되고 있었다. 6기 테스트로 가장 오래된 `6 * delay` 표본까지 보존됨을 확인했다.
- orbit 편성은 옵션 수를 이용한 정수 LUT 균등 배치이므로 별도 하드코딩 변경 없이 6기를 지원한다. 6기 상태를 같은 입력으로 130틱 비교했다.
- 미러 발사는 기존의 플레이어 본체 우선, Option index 오름차순 규칙을 유지한다. `MaxBullets = 4`에서 본체 + Option 1~3만 생성되고 Option 4~6은 결정론적으로 잘리는 테스트를 추가했다.

`GameData/`는 GROK 소유이므로 수정하지 않았다. 현재 저장소 데이터의 Option maxLevel 4도 계속 동작한다.

## 추가/갱신 테스트

- 기본 게이지 Option 최대값 6
- 6기 trail 이력 용량과 N-tick 지연 좌표
- 6기 fixed 5·6번 오프셋 좌표
- 6기 orbit 결정론 좌표
- 6기 + 본체 미러 볼리의 `MaxBullets` 절단 순서와 동일 시드 bullet id
- Option maxLevel 6 및 fixed 오프셋 6개 weapons JSON 파싱

## 검증 증거

### 전체 Core 테스트

명령:

```powershell
cd Tools/CoreStandalone
dotnet test --no-restore
```

결과:

```text
통과!  - 실패: 0, 통과: 415, 건너뜀: 0, 전체: 415, 기간: 915 ms
```

### DeterminismAudit 전체 suite

명령:

```powershell
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

결과 요약:

```text
PASS seed-0-first              hash=0E3683771EB9A5BA
PASS seed-1-last               hash=BF0DF454B2C09E15
PASS seed-12345-rotating       hash=21EAE9EC77EE4B8E
PASS seed-deadbeef-rotating    hash=702B6704D2B0C930
PASS seed-max-prefer-capped    hash=FDF3FD05E3EC959F
PASS seed-7-hidden             hash=C395351F2B32BBFA
PASS cap-boundary seedsScanned=256 qualifyingSeeds=256
AUDIT PASS
```

### 같은 시드 2회

명령을 seed `12345`, stageCount `3`, tickCount `30000`으로 두 번 실행했다.

```text
RUN_1 hash=4432CEB27060C4D4 completedStages=3/3 completedRooms=9/9 ticks=17803
RUN_2 hash=4432CEB27060C4D4 completedStages=3/3 completedRooms=9/9 ticks=17803
EXACT_MATCH True
```

### 정적 검사

```text
git diff --check: PASS
Core의 legacySoftCap=4 / exactly four offsets 잔여: 없음
```

커밋은 요청대로 생성하지 않았다.
