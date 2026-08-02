# REQ-104 구현 보고서 — 크레딧 경제 P1 / 컨티뉴 시스템

- 담당: CODEX / SIMULATION
- 일자: 2026-08-02
- 상태: 구현 및 standalone 검증 완료
- 커밋: 오케스트레이터 수행 예정

## 1. 구현 결과

### 메타 재고 및 구매

- `MetaState.ContinueStock` 추가, 저장 범위 `0..8` 강제.
- `ContinueEconomyConfig`로 아래 값을 주입 가능하게 분리.
  - 최대 재고: 8
  - 첫 구매 가격: 2,000
  - 현재 재고 한 칸당 다음 가격 증가: 1,000
  - 최종전 환산 실드 상한: 5
  - 초과 컨티뉴 1개당 점수: 1,000
- `MetaState.TryPurchaseContinue()`는 성공 여부, 실제 가격, 거부 사유(`StockFull`, `InsufficientCurrency`)를 반환한다.
- Core 기본 config만 제공하며 `GameData/`는 수정하지 않았다. GROK이 이후 JSON 원본을 추가하면 파싱한 값으로 `ContinueEconomyConfig`를 생성할 수 있다.

### 런 오버 컨티뉴

- `RunManager.ContinueAvailability`로 가능 여부, 남은 재고, 거부 사유를 관측한다.
- `TryUseContinue()` 성공 시:
  1. 런 재고와 연결된 `MetaState` 재고를 각각 1 감소
  2. 런 점수를 0으로 리셋
  3. 동일 바이옴/방/보스 구간의 시작점에서 재개
  4. 바이옴, 방, 계약, 난이도, 런 시드, 클리어 통계는 유지
  5. 파워업 게이지와 부분 진행, 무기 계열, 실드/폭탄, 패시브 보상 수치, modifier를 함선 기본 상태로 초기화
  6. `RunStatistics.ContinuesUsed` 누계 및 `ContinueDecisionHistory` 기록
- 사망한 배틀의 사격/명중/킬/캡슐/graze/폭탄 통계는 누계하되, 그 배틀의 점수는 폐기한다.

### 최종전 판돈

- 최종 바이옴의 보스방 진입 경계에서 자동으로 1회 정산한다.
- 보유 컨티뉴 전량을 MetaState와 런 재고에서 회수한다.
- 현재 실드 스톡부터 config 상한 5까지 `1 continue = +1 shield`로 충전한다.
- 상한을 넘는 수량은 소멸하지 않고 `1 continue = 1,000 score`로 환산한다.
- `FinalWagerCommitted`, `FinalWagerShieldGranted`, `FinalWagerOverflowConverted`, `FinalWagerScoreBonus`를 공개 관측한다.
- 정산 뒤 해당 런은 `FinalWagerCommitted` 사유로 컨티뉴가 금지된다.

수치 근거:

- 1,000점은 최저 구매가 2,000의 50%라서 초과분을 완전 소멸시키지 않으면서 구매보다 높은 점수 환급을 만들지 않는다.
- 8개를 기본 가격 사다리로 모두 구매하는 총비용은 44,000이다. 실드가 이미 가득 차 8개가 전부 점수화되는 최악 조건에서도 환급은 8,000(약 18.2%)이다.
- 실제 컨티뉴 사용은 기존 런 점수를 0으로 만들므로 컨티뉴 구매/사용을 통한 스코어 파밍 경로도 차단한다.
- 이 값은 P1 제안값이며 `ContinueEconomyConfig` 주입으로 GROK 밸런스 데이터가 교체할 수 있다.

### 공정성 / 데일리

- `ContinuesUsed > 0`이면 스코어보드가 컨티뉴 사용 런을 표시할 수 있다. 제출 허용/차단 판단은 Presentation 정책으로 남겼다.
- 데일리 런은 `RunConfig.IsDailyRun`으로 선언한다.
- 데일리 런은 유효 초기 재고가 항상 0이며 MetaState 재고를 소비하거나 최종전 이득으로 바꾸지 않는다.
- 런 오버 시 거부 사유 `ContinueRejectionReason.DailyRun`을 노출한다.

## 2. 리플레이 / 서스펜드 스키마 판단

- `InputRecordingData`: schema `22 -> 23`
  - 초기 컨티뉴 재고, 데일리 여부, 경제 config, 성공한 컨티뉴 결정의 누적 시뮬레이션 틱을 기록한다.
  - `InputPlayback.ContinueDecisions`와 `CreateRunConfig()`로 재현 입력을 제공한다.
  - REQ-104 입력이 없는 schema 22는 명시적으로 거부한다.
- `RunSuspendData`: schema `24 -> 25`
  - 남은/초기 재고, 사용 누계, 결정 이력, 누적 시뮬 틱, 데일리, 판돈 정산 결과, 경제 config를 체크섬에 포함한다.
  - schema 24는 명시적으로 거부한다.
- `MetaStateData`: schema `2 -> 3`
  - `continueStock`을 체크섬에 포함한다.
  - 메타 schema 2는 런 재현 데이터가 아니므로 재고 0으로 안전 마이그레이션한다.

컨티뉴는 일반 틱 입력과 달리 `RunOver` 상태에서 발생하는 명시적 결정이다. 따라서 RLE 입력 비트를 바꾸지 않고, `SimulationTicksElapsed` 경계의 별도 결정 스트림으로 기록했다. 재생기는 해당 틱에 런이 `RunOver`인지 확인한 뒤 `TryUseContinue()`를 호출한다. 결정 틱은 엄격한 오름차순이며 초기 재고보다 많은 사용 기록은 거부한다.

## 3. 결정론 / 무결성

- `DeterminismAuditHasher`에 다음을 포함했다.
  - 데일리 여부
  - 현재 재고
  - 사용 누계와 결정 이력
  - 누적 시뮬레이션 틱
  - 최종전 판돈 정산 상태와 결과
- 리플레이/서스펜드 체크섬에도 경제 config와 모든 컨티뉴 상태를 canonical order로 포함했다.
- 벽시계, `System.Random`, `Guid.NewGuid`, UnityEngine 참조를 추가하지 않았다.
- `Assert.Multiple`을 사용하지 않았다.

## 4. 테스트

추가 테스트: `Req104ContinueEconomyTests` 7개

- 구매 가격 사다리와 MetaState 저장 왕복
- 컨티뉴 사용: 재고 감소, 점수 0, 기본 파워, 통계/결정 기록
- 데일리 거부와 MetaState 재고 불변
- 최종 보스 진입 판돈: 실드 4개 + 초과 4개 = 4,000점
- 리플레이 결정 스트림 재현 및 서스펜드 왕복 해시
- 같은 시드/입력/컨티뉴 결정 2회 해시 일치
- 구 replay 22 / suspend 24 거부

최종 검증:

```text
dotnet test --no-restore
PASS 506 / FAIL 0 / SKIP 0

dotnet run --project Tools/DeterminismAudit -- --suite
6 scenarios PASS
cap-boundary PASS
AUDIT PASS
```

감사 suite는 각 시나리오를 동일 조건으로 두 번 실행해 전체 관측 상태 해시를 비교한다. REQ-104 전용 동일 시드 테스트도 별도로 통과했다.

## 5. 연동 메모

- Presentation은 일반 런 생성 시 구매가 끝난 `MetaState`를 `RunManager`에 주입한다.
- 데일리는 `new RunConfig(isDailyRun: true)`를 사용한다. MetaState에 재고가 있어도 데일리 런에는 들어오지 않는다.
- 리플레이는 `InputPlayback.CreateRunConfig()`으로 런을 만들고, `ContinueDecisions`의 틱에 `TryUseContinue()`를 적용한다.
- 스코어보드는 `run.Statistics.ContinuesUsed`와 `run.DevFlagsActive`를 독립 표기로 사용할 수 있다.

참고: 요청에 명시된 `Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md`는 현재 `wt-sim`에 존재하지 않았다. 승인된 REQ-104 본문을 권위 요구사항으로 삼아 구현했다.
