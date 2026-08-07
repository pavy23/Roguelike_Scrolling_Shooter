# REQ-088 (GROK): 7차 피드백 데이터 일괄 (사람 지정·승인)

너는 GROK = CONTENT 담당이다. 작업 디렉토리 wt-content, 시작 전 `git merge main` —
CODEX REQ-085/086/087이 전부 병합돼 있다. 각 보고서(Reviews/from-codex/req085~087-report.md)와
파서에서 새 데이터 축을 확인하고 값을 채워라.

## 1. 1스테이지 보스 HP 반감 (사람 지정, 불가침)

- `waves.json` boss_stage1 `hp: 8500` → **4250**.
- 다른 보스는 유지하되, stage1 반감으로 보스 HP 곡선(4250→14500)이 급해진다 —
  boss_hive를 손볼 필요가 있으면 **제안만** 보고서에 남겨라 (변경은 사람 승인 대상).

## 2. 옵션 미사일 배율 (REQ-085 B)

- `weapons.json`에 `optionMissileDamagePercent` 확정. 옵션 6기 만렙 기준 총 미사일
  DPS가 본체 단독 대비 폭증하지 않게 — 시작 제안 50, BalanceSim로 검증 후 확정.

## 3. 기체 무기 진화 3단계 수치 (REQ-086 A, 사람 승인 설계)

- 기체 무기 게이지 슬롯 maxLevel 3, 계열별 L2/L3 거동 필드 채움:
  - double L2 테일 가드(후방탄 LUT 32), L3 크로스(전방 2연 버스트+상향+후방)
  - triple L2 펄스 팬(5-way 맥동 min/max/period), L3 애프터버너(관성 % + 최소 간격 -1)
  - laser L2 랜스(관통 4 + 명중 폭발 데미지/반경), L3 프리즘 빔(지속 틱딜·두께 성장)
- 진화 단계가 강해질수록 탄당 데미지를 조여 총 DPS가 완만히 오르게 (L3 ≈ L1의 1.3~1.5×).

## 4. 보스 탄막 어휘 배치 (REQ-087, 사람 승인)

- 공용 탄종: heavy/splitter/mine/boss laser 수치(크기 배율·분열 틱/각·기뢰 예고/가속·레이저 예고/지속).
- 5보스 페이즈 2~3에 시그니처 배치: 고철 투척/산란/레이저 그리드/낙뢰/회전 프리즘+링탄.
- 페이즈 1은 기존 학습 구간 유지 — 시그니처 금지(파서가 강제).
- 난이도 곡선: 스테이지가 뒤로 갈수록 탄종 혼합이 짙어지게.

## 5. 계약 목적지 (REQ-086 B)

- Core가 후보에 destinationTheme을 배정한다 — 데이터 쪽 추가 필드가 필요하면 파서 확인
  후 반영, 아니면 기존 계약 데이터 그대로 두고 정합만 확인.

## 검증 (전부 필수)

- dotnet test 전부(현재 454), BalanceSim all green (**stage1 보스 4250 게이트, 옵션 미사일
  포함 DPS, 진화 단계 DPS 곡선 검사 추가**), DeterminismAudit AUDIT PASS, 같은 시드 2회
- 보고서 `Reviews/from-grok/req088-report.md`: 항목별 값 표 + 근거. 끝나면 커밋해라.
