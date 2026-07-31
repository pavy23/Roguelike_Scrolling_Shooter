# REQ-086 (CODEX): 기체 무기 진화 3단계 + 계약 목적지 바이옴 (7차 피드백, 사람 승인)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main`
(REQ-085가 선행 병합돼 있어야 한다).

## A. 기체 무기 진화 3단계 (사람 승인 설계)

기체 무기 게이지 슬롯 maxLevel 1 → **3**. 재발동마다 진화한다 (비용 평탄 1 유지).
무기 계열 데이터에 레벨별 거동 축을 추가하라 — 수치는 GROK, 구조는 너.

| 계열 | L2 | L3 |
|---|---|---|
| double | **테일 가드**: 후방탄 추가 (LUT 슬롯 32 = 180°) | **크로스 배라지**: 전방 2연장(같은 각도 2발 시차 버스트) + 상향 + 후방 |
| triple | **펄스 팬**: 5-way + 부채꼴 폭이 틱 기반으로 맥동 (spreadStep이 min↔max를 정수 주기로 순환) | **애프터버너 볼리**: 맥동 5-way + 플레이어 이동 속도의 일부를 탄 초기 속도에 정수 가산 + 최소 연사 간격 -1 |
| laser | **차지드 랜스**: 관통 4 + 관통 소진/최종 명중 지점에 소형 폭발(광역 데미지, KillExplosion 경로 재사용 가능) | **프리즘 빔**: 지속 빔 — 기존 LaserState 시스템을 플레이어 소스로 재사용, 유지 중 두께 성장 |

필요한 새 데이터 축(파서에 선택 필드로, 누락 시 기존 거동):
- 레벨별 `shotAngleLutSlots` / `spreadWays` / 버스트 수 / 관통 수 오버라이드
- 맥동: `pulseMinStepLutSlots`, `pulseMaxStepLutSlots`, `pulsePeriodTicks`
- 관성탄: `inertiaVelocityPercent` (플레이어 속도의 %를 탄속에 가산, 정수)
- 명중 폭발: `impactExplosionDamage`, `impactExplosionRadius`
- 플레이어 지속 빔: 텔레그래프 없이 즉시, 유지 틱당 데미지, 두께 성장 곡선(정수)

HUD 관측: `GetGaugeSlotView`가 무기 모드 슬롯의 Level(1~3)을 이미 노출하는지 확인하고,
아니면 노출하라 (Presentation이 단계명을 표시한다).

## B. 계약(항로)에 목적지 바이옴 결합 (사람 승인 — 다라이어스 분기)

- 스테이지 2~4 셔플을 계약 선택과 결합: 계약 후보를 생성할 때 각 후보에
  **destinationThemeId**(다음 스테이지의 바이옴)를 부여하라.
- 후보가 2~3개면 가능한 한 **서로 다른 테마**를 제시한다 (남은 셔플 풀에서 결정론적 선발).
  선택한 항로의 테마가 실제 다음 스테이지가 된다.
- 표준 항로(standard_route)도 목적지는 갖는다 — 무보정일 뿐.
- 마지막 스테이지 직전(5 진입)과 미지의 구역/귀환은 기존 규칙 유지.
- 관측 API: `ContractOptions[i].DestinationThemeId` 노출. 리플레이/서스펜드 재현 검증.
- 스키마 버전 영향 판단 명시.

## 검증

- dotnet test 전부(레벨별 거동·목적지 결정론 신규 테스트), DeterminismAudit AUDIT PASS,
  같은 시드 2회, BalanceSim은 데이터 도착 전이면 구조 호환만 확인
- 보고서 `Reviews/from-codex/req086-report.md`. 커밋은 오케스트레이터가 대신한다.
