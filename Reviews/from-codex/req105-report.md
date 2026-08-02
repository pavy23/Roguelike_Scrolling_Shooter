# REQ-105 구현 보고 — 32배 콤보·런 클리어 실드 보너스·HitsTaken

날짜: 2026-08-02  
담당: CODEX / SIMULATION  
결론: **PASS**

## 구현 요약

### 1. 콤보 6레벨 일반화

- `BattleSimConfig.ComboMultipliers`: `[1, 2, 4, 8, 16, 32]`
- `BattleSimConfig.ComboGaugeRequirements`: `[30, 50, 80, 130, 200]`
- 기존 `ComboMultiplierLevel1~4`, `ComboGaugeRequiredForLevel2~4` 개별 프로퍼티를 제거하고 배열 계약으로 교체했다.
- 전투 생성 시 배열을 복제하므로 외부에서 config 배열을 바꿔 이미 시작한 전투의 결과를 흔들 수 없다.
- 길이는 배율 6개/요구치 5개로 고정 검증하며 각 값은 양수여야 한다.
- 연속성/서스펜드의 허용 multiplier level도 `0..5`로 확장했다.

### 2. scoring.json 하위 호환 판단

`scoring.json` 스키마 1은 유지했다.

- 신규 정규 형식은 `multiplierGaugeRequirements` 5개다.
- 현재 GROK 데이터처럼 3개인 스키마 1 payload는 호환 입력으로 허용하고 Core가 뒤에 잠정값 `130, 200`을 붙인다.
- 3개/5개 이외 길이는 거부한다.
- `shieldBonusScorePerStock`은 스키마 1의 optional 필드로 추가했다. 누락 시 잠정 기본값 `5000`, 음수는 거부한다.

이 판단은 기존 `GameData/scoring.json`을 즉시 깨뜨리지 않으면서 GROK이 5개 배열과 실드 보너스 값을 명시적으로 이관할 시간을 주기 위한 것이다. GROK 데이터 반영 요청은 `Reviews/from-codex/requests.md`에 추가했다.

### 3. 잔여 실드 점수화 시점

**스테이지 클리어가 아니라 최종 런 클리어에서 한 번만 지급**하도록 결정했다.

스테이지마다 지급하면 동일한 생존 실드를 여러 스테이지에서 반복 환산하여 캠페인 길이만큼 중복 보상을 받는다. 또한 최종 보스 뒤 숨은 바이옴 진입 시 실제 런 종료 전 실드를 미리 환산하게 된다. 따라서 `RunState.RunCleared` 확정 시점이 게임 규칙상 일관된다.

- 공식: `remainingShieldStock * ShieldBonusScorePerStock`
- 콤보/인카운터/계약 배율은 적용하지 않는다.
- `BattleSim.AwardRunClearShieldBonus()`는 단회성/idempotent다.
- 실제 지급액은 `Battle.Score`와 `RunManager.TotalScore`에 포함된다.
- `RunManager.RunClearShieldBonus`로 최종 보너스를 조회할 수 있다.
- `SimEventType.ShieldBonusAwarded`를 발행한다.
  - `EntityId`: 잔여 실드 수
  - `Arg`: 실제 지급 총액(`int.MaxValue` 포화)
  - `X/Y`: 플레이어 위치

현재 Presentation은 같은 battle tick의 이벤트를 한 번만 소비하므로 계약 선택 뒤 추가된 이벤트를 자동 소비하지 않는다. Core 이벤트는 관측 가능하지만 화면의 `SHIELD BONUS +N` 표시는 CLAUDE가 성공한 `ChooseContract` 직후 이벤트를 다시 소비하거나 `RunClearShieldBonus`를 읽어야 한다. 소유권 규칙에 따라 요청을 남겼다.

### 4. HitsTaken

- `BattleStatistics.HitsTaken`: 한 전투의 허용된 피격 누계
- `RunStatistics.HitsTaken`: 완료 전투 + 현재 전투의 포화 합산
- 실드가 막은 피격과 사망 피격을 모두 센다.
- 무적/피격 무적 시간/이미 사망 상태 때문에 거부된 접촉은 세지 않는다.
- 전투 교체, 컨티뉴 통계 승계, 런 재시작 초기화, 서스펜드 경계를 모두 반영했다.
- `DeterminismAuditHasher`의 전투/런 전체 관측 상태에 포함했다.

## 저장·리플레이 스키마 판단

규칙과 최종 점수, 공개 통계가 바뀌므로 구버전 재생/서스펜드를 묵시적으로 마이그레이션하지 않았다.

- `RunSuspendData`: **25 → 26**
  - `hitsTaken` 필드와 체크섬 포함
  - v25 명시적 거부
- `InputRecordingData`: **23 → 24**
  - 6레벨 콤보/실드 보너스 전 규칙으로 녹화된 v23 명시적 거부

현재 버전 payload는 체크섬 검증을 거치며 음수 `hitsTaken`은 거부한다.

## 테스트

신규/갱신 검증:

- 1/2/4/8/16/32 전 단계 진입, 점수 배율, 이벤트 검증
- 기본 5개 요구치와 legacy 3개 → 5개 확장 검증
- 명시적 5개 요구치와 실드 보너스 파싱, 음수 거부
- 런 클리어 잔여 실드 단회 보너스/점수/이벤트 검증
- 실드 피격·사망 피격·사망 후 거부의 `HitsTaken` 검증
- 전투 교체 합산과 서스펜드 `hitsTaken`/체크섬 검증
- v23 replay / v25 suspend 거부 검증

실행 결과:

```text
dotnet test --no-restore
PASS 510/510

DeterminismAuditSmoke_ConsumesRewardsAndRoutesToVictory
same-seed hash=D52CC9547B206E48, ticks=1144

dotnet run --no-restore --project Tools/DeterminismAudit/DeterminismAudit.csproj -- --suite
PASS 6/6 + cap-boundary 256/256
AUDIT PASS
```

`Assert.Multiple`은 사용하지 않았고, Core 신규 공개 계약은 모두 `public`이다.
