# REQ-094 (CODEX): SPARTAN 계약 축 + 런 통계 확장 (사람 승인 2026-08-01)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main`.

## A. 자기 제약형 계약 효과 축 (사람 승인 — "계약 제안 좋다")

스코어보드 경쟁용 "저강화 리스크 = 점수 보상" 기믹을 **계약 카드**로 넣는다.
계약 효과에 다음 제약 축을 추가하라 (전부 스테이지 단위, 기존 계약 파이프라인 재사용):

| 축 | 의미 |
|---|---|
| `gaugeActivationBanned` | 이 스테이지 동안 게이지 발동(SELECT) 불가 — 캡슐 적립은 됨 |
| `optionActivationBanned` | OPTION 슬롯만 발동 불가 |
| `shieldActivationBanned` | SHIELD 슬롯만 발동 불가 |

- 발동 시도는 조용히 거부 + `PowerUpActivationResult` 계열로 사유 관측 가능하게
  (Presentation이 "CONTRACT LOCK" 피드백을 그린다).
- 점수 배율은 기존 contractScoreMultiplier 축 재사용 — 수치·카드 구성은 GROK 몫.
- 리플레이/서스펜드 재현 검증, 스키마 판단 명시.

## B. 스코어보드 상세 통계 (P1.5)

RunStatistics(또는 RunManager 관측)에 다음이 없으면 추가하라:
- `BombsUsed` (런 누계)
- `GrazeCount` 런 누계 관측 (배틀 단위만 있으면 승계 합산)
- 도달 지점은 기존 StageIndex/RoomIndex로 충분 — 확인만.
최대 배율은 Presentation이 이미 추적하므로 Core 불필요.

## 제약/검증
- Assert.Multiple 금지, Core 심벌 public
- dotnet test 전부, AUDIT PASS, 같은 시드 2회, BalanceSim 구조 호환
- 보고서 `Reviews/from-codex/req094-report.md`. 커밋은 오케스트레이터가 대신한다.
