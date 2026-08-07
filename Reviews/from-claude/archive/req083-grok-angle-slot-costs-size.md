# REQ-083 (GROK): 더블 각도·MainShot 슬롯·비용 평탄화·졸개 크기 (6차 피드백)

너는 이 프로젝트의 GROK = CONTENT 담당이다. 소유 영역 `GameData/*.json`. 작업 디렉토리 wt-content, 시작 전 `git merge main` — **CODEX REQ-082(게이지 6칸 Core 확장)가 이미 main에 있다.** 스키마는 파서/DTO에서 직접 확인해라.

## 1. 더블 샷 각도 (사람: "너무 가파르다, 직선과 전방 30도 정도로")

`weapons.json` double의 `shotAngleLutSlots: [0, 8]`(45°) → 상향탄을 **30°에 가장 가까운
LUT 슬롯**으로. 64슬롯 LUT 기준 30° = 슬롯 5.33이므로 5(28.1°) 채택을 권장하되,
실제 LUT 해상도를 파서에서 확인하고 근거를 보고서에 남겨라.

## 2. MainShot 게이지 슬롯 (사람: "스피드 다음 기본 샷 강화 추가")

- `weapons.json` powerUpGauge.slots에 MainShot 슬롯 추가 (nameKey "Shot", maxLevel은
  main_shot 무기 maxLevel과 정합 — 현재 5).
- `ships.json` 전 기체 `powerUpGaugeSlots`를 6칸으로:
  `["Speed","MainShot","Missile","Weapon(기체 무기 자리)","Option","Shield"]` — Speed **바로 다음**이 사람 지정.
- Core는 6칸을 받도록 확장돼 있다(REQ-082 D). 파서가 거부하면 CODEX 보고서를 확인하고 불일치를 보고해라.

## 3. 레벨업 비용 평탄화 (사람: "레벨업이 너무 더디다. 한번 아이템 쓸 때마다 1씩")

모든 게이지 슬롯 costCurve를 **평탄 1** (baseCost 1, linearGrowth 0, quadraticGrowth 0)로.
캡슐 1개 = 활성화 1회 = 레벨 +1이 되게 하라. Option/Shield의 기존 고비용(2+…)도 1로.
**밸런스 재검증 필수**: 성장 속도가 크게 빨라지므로 BalanceSim 클리어 게이트·난이도
게이트가 깨지면 시작 레벨/비용이 아니라 **다른 손잡이**(캡슐 드롭률, 적 HP 스케일)로
보정해라 — 평탄 1은 사람 지정이라 불가침이다. Shield 소프트캡(§7 잠정 3) 유지.

## 4. 졸개 크기 1.25배 (사람: "졸개들이 크기가 너무 작네")

`enemies.json` 일반 졸개(보스/중간보스/장애물 제외)의 `halfWidth`/`halfHeight`(있다면) 및
충돌 치수를 **×1.25**. 이 값은 Presentation 스프라이트 스케일도 겸하므로 시각·히트박스가
함께 커진다. 판정이 쉬워지는 만큼 밸런스 영향(명중률 상승, 플레이어 충돌 증가)을
BalanceSim으로 확인하고 수치를 보고서에 남겨라. 레이저 센트리·프리즘 비머 포함, 미니보스는 네 판단.

## 검증

- `dotnet test` 전부 통과, BalanceSim all green, DeterminismAudit AUDIT PASS
- 같은 시드 2회 해시 일치
- 보고서 `Reviews/from-grok/req083-report.md`: 각도 슬롯 근거, 6칸 슬롯 표, 평탄화 후 캡슐 EV vs 총 레벨 비용, 크기 변경 목록

끝나면 커밋해라.

---

## GROK 완료 (2026-07-31)

- [x] 1. 더블 샷 각도 → `shotAngleLutSlots: [0, 5]` (≈28.1°)
- [x] 2. MainShot 6칸 게이지 (ships + BalanceSim; 7슬롯 카탈로그는 Core가 MainShot 거부)
- [x] 3. 비용 평탄 1 (powerUpCostCurve + 전 슬롯 costCurve)
- [x] 4. 졸개 halfWidth/halfHeight ×1.25 (midBoss 제외 29종)

보고서: `Reviews/from-grok/req083-report.md`
