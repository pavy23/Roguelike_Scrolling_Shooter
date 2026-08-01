# REQ-092 (CODEX): 미니보스 이동 어휘 + 기체별 기본 옵션 편성 (9차 피드백)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main`.

## A. 미니보스 이동 차별화 (사람: "어떤 중간보스는 가운데 가만히 있기만")

미니보스전이 페이즈 이동 축(legacyHover/stationary/verticalSine)을 소비할 수 있는지
확인하고, 어휘를 2종 이상 추가하라 (전부 정수 결정론):
- **lungeReturn**: 홀드 위치에서 플레이어 X쪽으로 짧은 돌진 후 복귀 (예고 틱 포함)
- **figureEight** 또는 **sweepColumn**: 세로 8자/기둥 스윕 — LUT 기반
- 미니보스별 배정은 GROK 몫 — 너는 축과 파서 훅만. 가만히 있는 패턴(stationary)은
  진폭 0 사인일 가능성 — 원인 확인해 보고서에 명시.

## B. 기체별 기본 옵션 편성 (사람 지정)

ships.json에 `optionFormation` 선택 필드(trail/fixed/orbit) 파서 추가, 런 시작 시
해당 편성으로 시작. 기존 optionFormation 보상을 먹으면 교체되는 규칙 유지.
- 기체1 trail / 기체2 fixed / 기체3 orbit (데이터는 GROK).
- 서스펜드/리플레이 재현 검증, 스키마 판단 명시.

## C. 중간보스→보스 구간 길이 (사람: "너무 짧다")

현 구조에서 midboss 격파 후 StageBoss 진입 전 전투 구간(Closing)이 실제로 몇 세그·
몇 틱인지 관측해 보고서에 쓰고, 데이터(waves.json)만으로 연장 가능한지 아니면 Core
손잡이가 필요한지 판정하라. Core 변경이 필요하면 반영하라.

## 제약/검증
- Assert.Multiple 금지, Core 심벌 public
- dotnet test 전부, AUDIT PASS, 같은 시드 2회
- 보고서 `Reviews/from-codex/req092-report.md`. 커밋은 오케스트레이터가 대신한다.
