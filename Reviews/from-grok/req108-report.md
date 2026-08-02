# REQ-108 GROK 구현·검증 보고서 — 기체 해금 가격 상향

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-106 제안 A (사람 승인)
- 결과: **PASS**

## 결론

REQ-106에서 권장한 **안 A**를 적용했다.  
x32 배율 시대 크레딧 수입(점수 1:1 적립)에 맞춰 Interceptor/Bulwark 해금가를 약 2× 상향.  
컨티뉴 사다리 대비 **컨티뉴가 기체보다 싸다**는 상대 균형 유지.

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **510/510** |
| BalanceSim (`VerifyThemeAssembly`) | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary 256) |

---

## 1. 변경 (`ships.json`)

| id | 이전 | 이후 | 배율 |
|---|---:|---:|---:|
| `starter` | 0 | **0** | — |
| `interceptor` | 25,000 | **50,000** | ×2 |
| `bulwark` | 50,000 | **100,000** | ×2 |

실드·이동·게이지 패밀리·미사일·옵션·시작 파워업 등 해금가 외 필드는 변경 없음.

### 의도 (REQ-106 §4 재인용)

| 런 성격 | 대략 점수(크레딧) | 25k/50k 체감 | **50k/100k 체감** |
|---|---|---|---|
| 초반 사망 | 2만–8만 | 1런 이내 해금 (과소) | 조기 사망 **1–2회** / 약한 클리어 창 |
| 평균 클리어 | ~60만 EV | 1런에 둘 다 가능 | 1 클리어로 Interceptor 충분, Bulwark도 1회 내 |
| 고수 클리어 | 100만+ | 즉시 해금 | 여전히 1런 내 — 메타 봉쇄는 아님 |

보드 상한 9,999,999,999는 유지 (REQ-106 권고, 본 REQ 범위 외).

---

## 2. 컨티뉴 사다리 vs 기체 가격 (검산)

Core 기본 경제 (`ContinueEconomyConfig`):

- `FirstPurchasePrice = 2000`
- `PurchasePriceIncrease = 1000`
- `MaximumStock = 8`
- 공식: `price(stock) = 2000 + 1000 × stock` (stock 0..7), stock 8 = 구매 불가(0)

| stock | 단가 |
|---:|---:|
| 0 | 2,000 |
| 1 | 3,000 |
| 2 | 4,000 |
| 3 | 5,000 |
| 4 | 6,000 |
| 5 | 7,000 |
| 6 | 8,000 |
| 7 | 9,000 |
| **풀 8장 누적** | **44,000** |

| 비교 | 값 | 판정 |
|---|---:|---|
| 최단가 컨티뉴 | 2,000 | ≪ Interceptor 50k / Bulwark 100k |
| 최고단가 컨티뉴 | 9,000 | ≪ 양쪽 기체 |
| 풀스톡 8장 누적 | 44,000 | **&lt; Interceptor 50k** · ≪ Bulwark 100k |
| Interceptor 해금 | 50,000 | 컨티뉴 풀 스택보다 비쌈 (해금이 더 큰 메타 투자) |
| Bulwark 해금 | 100,000 | 컨티뉴 풀×2 이상 |

**한 줄 검산:** 컨티뉴 사다리 최고 단가(9k)·풀 8장 누적(44k) 모두 새 해금가(50k/100k) 미만 → **「컨티뉴가 기체보다 싸다」유지**. 크레딧 1:1(`CreditScore`)이므로 점수 스케일 논의와 동일 단위.

---

## 3. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/ships.json` | interceptor 50000 · bulwark 100000 |
| `Reviews/from-grok/req108-report.md` | 본 보고서 |
| `Reviews/from-grok/requests.md` | REQ-108 절 + CLAUDE 동기화 요청 |

---

## 4. 타 에이전트 요청

### CLAUDE
1. Resources `GameData/ships.json` 동기화 (unlockCost 50k / 100k)
2. (선택) 격납고 BUY UI 가격 표시가 JSON 값을 그대로 쓰는지 확인

### CODEX
- 없음. `UnlockCost` 소비 API 변경 없음. 라이브 골든 테스트는 unlockCost를 단정하지 않음.

### GEMINI
1. 해금 창(런 수) 체감: 초반 사망 vs 평균 클리어
2. 컨티뉴 구매 vs 기체 해금 우선순위 UX 관찰
