# REQ-082 CODEX 구현·검증 보고서

작성일: 2026-07-31  
브랜치/worktree: `sim` / `wt-sim`  
커밋: 수행하지 않음(오케스트레이터 담당)

## 결론

REQ-082의 Core 범위 A~D를 모두 구현했다. 새 로직은 정수 틱/정수 좌표만 사용하고,
새 난수·벽시계·비결정 순회 의존성을 추가하지 않았다.

## A. 보스 등장 시퀀스

- 보스가 있는 모든 스테이지에서 종료 예정 90틱 전부터 예약 일반 적 스폰을 억제한다.
- 그 시점에 남은 일반 적은 즉시 삭제하지 않고 화면 왼쪽으로 결정론적으로 퇴장한다.
  퇴장 중 플레이어 충돌 판정만 중지하며, 실제 엔티티 제거는 기존 화면 밖 despawn 경로를
  사용한다.
- 일반 적이 0이 된 틱을 기록하고 60틱의 빈 전장을 유지한 뒤 보스를 스폰한다.
- 스폰 후 기존 우측 화면 밖 위치에서 hold X까지 활공하는 진입은 유지한다.
- `BattleSim` 공통 보스 경로에 적용했으므로 일반/거대 보스를 포함한 모든 보스 타입에
  동일하게 적용된다.
- 시간 제한이 있는 보스 방은 새 빈 전장 60틱 때문에 기존 전투 시간이 줄지 않도록
  제한을 60틱 연장한다.

관련 상수:

- `BattleSim.BossSpawnSuppressionLeadTicks = 90`
- `BattleSim.BossPostClearDelayTicks = 60`

## B. 보스 페이즈 전환 움찔 수정

- HP 기반 페이즈에서도 `_bossPhaseAge`가 매 틱 증가하도록 수정했다. 기존에는 HP 기반
  페이즈의 이동 위상이 첫 위치에 사실상 고정될 수 있었다.
- 페이즈 전환 시 새 파형의 위상을 이전 Y 속도와 가장 가까운 정수 위상으로 선택한다.
- 전환 직전 Y와 속도를 anchor에 반영해 첫 프레임 위치와 다음 프레임 `dy`가 연속되게 했다.
- 기존의 절대 월드 Y와 파형 offset을 비교하던 잘못된 차원 비교를 제거했다.

## C. `ObstacleDamaged` 이벤트

- `SimEventType.ObstacleDamaged = 33` 추가.
- 비치명타 계약: `EntityId=obstacleId`, `X/Y=투사체 충돌점`, `Arg=남은 HP`.
- 플레이어 기본탄, 미사일 및 폭탄의 비치명 breakable obstacle 피해에 적용했다.
- 치명타 틱에는 `ObstacleDamaged`를 내지 않고 기존 `ObstacleDestroyed`만 발생한다.
- 장애물 피해 처리를 `ApplyDamageToObstacleAt` 한 경로로 통합해 이벤트 의미를 동일하게
  유지한다.

## D. 함선 게이지 6칸

- 함선 전용 게이지를 5칸에서 6칸으로 확장했다.
- 기본 순서: `Speed, MainShot, Missile, 지정 무기, Option, Shield`.
- `MainShot` 활성화는 공유 주무기 레벨을 올리므로 모든 주무기 패밀리의 기존 전투 계산에
  즉시 반영된다.
- `ships.json` 파서는 명시적 `MainShot`을 인식하고 새 데이터는 정확히 6칸이어야 한다.
- 코드에서 직접 만든 기존 5칸 `ShipDefinition`은 `Speed` 바로 뒤에 `MainShot`을 삽입해
  호환 이행한다. JSON 원본에는 이 묵시적 이행을 허용하지 않아 잘못된 새 데이터를 조기에
  거부한다.

### 서스펜드/리플레이 스키마 판단

직렬화 필드 모양은 변하지 않지만 동일한 캡슐 입력에서 커서가 가리키는 슬롯의 의미가
달라진다. 구 데이터를 그대로 재생하면 의미상 동일 입력→동일 결과 계약이 깨지므로 버전을
올리고 직전 버전을 명시적으로 거부한다.

- `InputRecordingData`: 16 → **17**, v16 거부
- `RunSuspendData`: 17 → **18**, v17 거부

## 테스트 추가/갱신

- 보스: 늦은 예약 스폰 억제, 생존 적의 자연 퇴장, 정확한 60틱 빈 전장, 활공 진입,
  페이즈 Y/`dy` 연속성.
- 장애물: 기본탄/미사일/폭탄 비치명 이벤트의 좌표와 잔여 HP, 치명타의 damaged 미발생.
- 게이지: 6칸 파싱/순서, `MainShot` 활성화 후 레벨 상승, 지정 무기 인덱스 이동,
  구 5칸 코드 생성 호환.
- 스키마: 새 현재 버전과 직전 리플레이/서스펜드 거부.

## 검증 증거

### CoreStandalone

명령: `cd Tools/CoreStandalone && dotnet test --no-restore`

결과: **PASS 412 / FAIL 0 / SKIP 0** (`net10.0`, 884 ms)

### DeterminismAudit

명령: `cd Tools/DeterminismAudit && dotnet run --no-restore --project . -- --suite`

결과: **AUDIT PASS**

| 시나리오 | 해시 |
|---|---|
| seed-0-first | `7A01E2920BE323A2` |
| seed-1-last | `D509F128B1DDB160` |
| seed-12345-rotating | `8C31AAB84C051EE0` |
| seed-deadbeef-rotating | `B50843F0A86589F4` |
| seed-max-prefer-capped | `4AFD3FB0CC100D97` |
| seed-7-hidden | `AE920B606902A0E9` |

추가 cap-boundary 감사: seeds 256/256 qualifying, stage2/stage3 battle hash와 stage3
reward options 모두 matched.

### 같은 시드 2회

명령(2회):
`dotnet run --no-restore --project . -- 12345 3 30000`

- 1회: `hash=702122A5D1072EB1`, ticks=18612, stages=3/3, rooms=9/9
- 2회: `hash=702122A5D1072EB1`, ticks=18612, stages=3/3, rooms=9/9
- 결과: **SAME-SEED HASH MATCH**

### BalanceSim

현재 worktree의 `GameData/`에는 작업 시작 전부터 GROK 소유의 미커밋 REQ-079~081
데이터가 있었고, `sim`의 구 BalanceSim 실행기는 그 최신 데이터(적 32종/보스 7종 등)를
모르는 상태라 직접 실행은 REQ-082와 무관한 카탈로그 검사에서 실패했다. 이 파일들은
수정하거나 되돌리지 않았다.

검증을 막지 않기 위해 임시 사본에서 다음 통합 상태를 구성했다.

1. 로컬 `main`의 최신 `GameData/`와 `Tools/BalanceSim/`
2. 이 작업의 REQ-082 게임플레이 `Assets/Scripts/Core/`
3. REQ-082 후속 데이터로 세 함선의 `Speed` 뒤에 `MainShot` 추가
4. BalanceSim의 함선 계약 기대값/무기 인덱스를 6칸 기준으로만 임시 조정

명령: `dotnet run --no-restore --project VerifyThemeAssembly.csproj`

결과: 종료 코드 **0**, **`PASS: BalanceSim all checks green.`**

임시 사본 변경은 검증 전용이며 실제 GROK 소유 파일에는 반영하지 않았다. 필요한 정식
후속은 `Reviews/from-codex/requests.md`에 남겼다.

## 작업 경계

- 수정: `Assets/Scripts/Core/`, `Assets/Tests/EditMode/`, 이 보고서/요청서만.
- 작업 시작 전 존재한 `GameData/*.json` 미커밋 변경은 보존했다.
- `Tools/BalanceSim` 실제 파일과 Presentation 영역은 수정하지 않았다.
- 금지 API 검색 및 `git diff --check`: 이상 없음.
