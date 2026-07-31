# REQ-073: 로그라이크 마무리 데이터 — 미지의 구역 계약 + 리롤 + 셔플

**상태:** GameData 반영 완료 · 수치 잠정 (AGENTS.md §7)  
**검증:** `dotnet test` **386/386** · BalanceSim 전부 그린 (REQ-060/071/073) · DeterminismAudit **AUDIT PASS**

## A. 최종 계약 카드 2종 (`waves.json` contracts)

| id | destinationKind | riskTier | eligibility | 효과 | 비고 |
|---|---|---|---|---|---|
| `end_run` | **endRun** | **safe** | **always** | 무보정 중립 | 최종 화면 항상 노출 · 런 안전 종료 |
| `uncharted` | **uncharted** | **extreme** | **hiddenBiomeUnlocked** | score×**1.25** | 2-of-3 히든 조건 충족 시에만 · 콜로서스 진입 |

- 기존 nextStage 8종 id 유지, 신규 id 비충돌.
- Core 파서 허용값: `destinationKind` = `nextStage`/`endRun`/`uncharted`, `eligibility` = `always`/`hiddenBiomeUnlocked`.
- mid-run 옵션 풀은 Core가 `DestinationKind == NextStage`만 추첨하므로 최종 2종은 중간 계약 화면에 섞이지 않음.
- 미지의 구역은 진입 자체가 보상 → 효과는 점수 배율만 부여 (1.25, 과하지 않게).

## B. 리롤 비용 (`rewards.json` schema v5)

| 항목 | 값 |
|---|---|
| `schemaVersion` | **5** (`rerollCost` 필수) |
| `rerollCost` | **5** (Core 기본값과 동일, §7 잠정 4~6 중점) |

### 교환비 계산

| 지표 | 수치 |
|---|---:|
| 스테이지 캡슐 EV S1 / S2 / 평균 | 8.55 / 10.91 / **9.73** |
| 게이지 비용 1+L+L² | L0→1=**1**, L1→2=**3**, L2→3=**7** |
| 1 리롤 비용 | **5** 캡슐 |
| vs Main2→3 | 5/7 ≈ **71%** (레벨업 대부분을 카드 재추첨에 씀) |
| vs 초반 누적 L0→2 | 5/4 = **125%** (초반 2단 성장보다 비쌈) |
| 스테이지당 최대 리롤 예산 (전부 리롤 투입) | 9.73/5 ≈ **1.9회/스테이지** |

**판단:** 4는 EV 대비 너무 싸서 mid+main 양쪽 리롤이 상시화되기 쉽고, 6은 Main2→3의 ~86%라 게이지 성장과 강하게 충돌한다. **5**는 “스테이지당 1~2회 의미 있는 재추첨, 무한 스캠 불가” 교환비.

## C. 스테이지 2~4 셔플 검증

고정: S1=`scrapyard`, S5=`core`. 중간의 hive/fortress/nebula 6순열 전부 평가.

| S2 / S3 / S4 | S2 poolHP | S2 TTK | hits≈ | 판정 |
|---|---:|---:|---:|---|
| hive / fortress / nebula | 790 | 49s | 2.55 | **CLEAR** |
| hive / nebula / fortress | 790 | 49s | 2.55 | **CLEAR** |
| fortress / hive / nebula | 1069 | 61s | 2.96 | **CLEAR** |
| fortress / nebula / hive | 1069 | 61s | 2.96 | **CLEAR** |
| **nebula** / fortress / hive | 1131 | 76s | 3.68 | **CLEAR nebula@S2** |
| **nebula** / hive / fortress | 1131 | 76s | 3.68 | **CLEAR nebula@S2** |

- 전 순서 S2 pool < S4 pool (난이도 순서 역전 없음). 최악 비율: nebula@S2 vs hive@S4 = 0.87×.
- 네뷸라 2번째: 시야 제한(uptime×0.88) + 드리프트 가산 히트 반영 후에도 CLEAR.

### 수치 조정 (nebula@S2 대응)

최초 게이트 실패 원인: `boss_storm` HP 22500 + 이른 드리프트 → hits≈4.01 / TTK 83s.

| 조정 | 전 → 후 | 이유 |
|---|---|---|
| `boss_storm` HP | 22500 → **20000** | 셔플 시 S2 도달 화력으로 학습 가능 유지. 정위치 S4 중밴드 TTK 25.6s→22.7s 유지 |
| early nebula drift (wisp/echo/void 4세그) | 합성 ~0.49–0.60 → **~0.38–0.47** | 이른 시야+드리프트 압박 완화. late d3–5 (crystal/prism)는 유지 |

## 변경 파일

- `GameData/waves.json` — `end_run`/`uncharted` 계약, boss_storm HP, early nebula drift
- `GameData/rewards.json` — schema v5, `rerollCost: 5`
- `Tools/BalanceSim/Program.cs` — REQ-073 terminal/reroll/shuffle 게이트
- `Assets/Tests/EditMode/GameDataParserTests.cs` — 카탈로그 개수·reroll·terminal 단언

## 후속

1. [ ] CLAUDE: `Assets/Resources/GameData/waves.json` · `rewards.json` 동기화
2. [ ] CLAUDE: 최종 계약 화면 — endRun 안전 카드 / uncharted extreme 카드 카피·색
3. [ ] 사람: `rerollCost=5`, uncharted score×1.25, boss_storm 20000 최종 확정 (§7)
