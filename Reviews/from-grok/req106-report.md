# REQ-106 GROK 구현·검증 보고서 — 11차 밸런스 (실드/배율/실드 보너스)

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-105 (Core 콤보 6레벨·ShieldBonus·HitsTaken, CODEX)
- 결과: **PASS**

## 결론

사람 지시 반영: 기체 기본 실드 한 단계 상향, 배율 곡선 6레벨 정규화(상한 x32), 런 클리어 잔여 실드 보너스 수치 확정 제안.  
보드 상한·해금 가격은 **제안만** (적용은 사람 승인 후).

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **510/510** |
| BalanceSim | **all green** (REQ-106 x8/x16/x32·실드 보너스 게이트 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary 256) |

---

## 1. 기본 실드 (`ships.json`)

| id | 이전 | 이후 | 정체성 |
|---|---:|---:|---|
| `starter` | 1 | **2** | 균형 (기준선) |
| `interceptor` | 0 | **1** | 유리몸·고속 (가장 얇음 유지) |
| `bulwark` | 2 | **3** | 탱커 (상한 `DefaultMaxShieldStock=3`과 동일 → 시작 풀스톡) |

- 해금 가격(25000/50000)·이동 배율·게이지 패밀리·미사일·옵션 포메이션·시작 파워업(전부 0) **변경 없음**.
- Core 런타임 상한은 기본 3 / 하드 5 (`MaximumShieldStock`). Bulwark 시작 3은 “시작부터 풀 스톡” 탱커 표현. 피격 후 실드 게이지 회복은 상한 내에서 유효.

---

## 2. 배율 곡선 (`scoring.json`)

### 확정 데이터

```json
"multiplierGaugeRequirements": [30, 50, 80, 130, 200],
"multiplierDecayTicks": 300,
"grazeGaugeCharge": 3
```

배율 배열은 Core 고정 `[1, 2, 4, 8, 16, 32]` (content 미소유).  
CODEX 잠정 `[30,50,80,130,200]`을 BalanceSim EV로 검산 후 **그대로 채택**.

### 킬 전용 클라이밍 (killGaugeGain=10)

| 목표 | 누적 킬 | 해석 |
|---|---:|---|
| x2 | 3 | 즉시 반응 |
| x4 | 8 | 초반 구간 |
| x8 | 16 | **평균 런 체류 하한** — 중반 도달 가능 |
| x16 | 29 | **평균 상단** — 무피격 연속 전투 필요 |
| x32 | 49 | **최상위** — 장시간 노피격 유지 |

x32/x8 킬 비 = **3.06** (≥ 게이트 2.5). 감쇠 300틱(5s)·**PlayerHit → 콤보 리셋**(Core)이 x32를 노피격급 표현으로 고정.

### 그레이즈 상호작용 (`grazeGaugeCharge=3`)

| 경로 | x8 | x16 | x32 |
|---|---:|---:|---:|
| 순수 그레이즈 | 54 | 98 | 165 |
| 킬 전용 | 16 | 29 | 49 |
| 혼합 (1킬+3그레이즈, 게이지/킬=19) | — | — | **≈26킬** |

- 그레이즈 점수는 배율 미적용(고정 10). 60s 스케치 grazeShare **5.9%** ≪ 40% 상한.
- 순수 그레이즈 클라이밍은 킬 대비 **3.4×** 느림 (소프트 게이트 ≥3×).
- 그레이즈는 감쇠를 리셋하지 않음 — **킬로 유지 + 그레이즈로 가속**이 스킬 표현.

### 60s 노피격 스케치 (1킬/2s + 3그레이즈/s)

- peak/end **x32**, killScore 주도.
- 평균 플레이(피격 리셋)는 구간마다 x1로 떨어져 **x8~x16 재상승**이 현실적 상단.

---

## 3. 실드 보너스 (`shieldBonusScorePerStock`)

| 필드 | 값 |
|---|---:|
| `shieldBonusScorePerStock` | **8000** |

- 지급 시점: **런 클리어 1회** (REQ-105 Core). 콤보/계약 배율 미적용.
- Clear EV 스케치 (BalanceSim):  
  `240 kills × catalog avg≈630 × mult≈4.0 ≈ 604,800`

| 시나리오 | 보너스 | 총점 대비 |
|---|---:|---:|
| 잔여 2 (Starter 풀 / conserve) | 16,000 | **2.65%** |
| 잔여 3 (Bulwark 풀) | 24,000 | **3.97%** |

목표 밴드 **2–5%** 안쪽. ‘실드 아껴 클리어’가 보이되 총점을 지배하지 않음.  
(약한 조기 사망 런에서는 상대 비중이 커질 수 있으나, 보너스는 클리어 전용이라 사망 점수에는 영향 없음.)

CODEX 잠정 5000은 동일 EV에서 ~1.7%로 하한(2%) 미달 → **8000으로 상향**.

---

## 4. 보드 상한·해금 가격 (제안만 · 미적용)

### 스코어보드 상한 `9,999,999,999`

- 중간 클리어 EV ~0.6M, 노피격 x32 장기 유지 시에도 수 M~수십 M 추정.
- 이론 상한(전 구간 연속 x32·엘리트 풀)도 10B 대비 여유.
- **조정 불필요** (서버 `worker.js` 유지).

### 기체 해금 `interceptor=25000` / `bulwark=50000`

x8 시대 가정(런 ~1–3만)에서는 2–4런 창이었다. x32 스케일 이후:

| 런 성격 | 대략 점수 | 25k/50k 체감 |
|---|---|---|
| 초반 사망 | 2만–8만 | **1런 이내** 해금 (너무 쌈) |
| 평균 클리어 | ~60만 EV | 1런에 둘 다 가능 |
| 고수 클리어 | 100만+ | 즉시 해금 |

**제안 (사람 승인 후 적용):**

| 안 | Interceptor | Bulwark | 의도 |
|---|---:|---:|---|
| A (권장) | **50,000** | **100,000** | 약 2× — 조기 사망 1–2회 / 약한 클리어 1회 창 복원 |
| B (보수) | **75,000** | **150,000** | ~3× — 메타를 더 길게 |

현 데이터는 **25k/50k 유지**. 변경은 승인 후.

---

## 5. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/ships.json` | startingShieldStock 2/1/3 |
| `GameData/scoring.json` | requirements 5개 + shieldBonus 8000 |
| `Tools/BalanceSim/Program.cs` | 6레벨 배열 API · x16/x32·실드 보너스 게이트 · 실드 identity 2/1/3 |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | GameData 골든 실드 2/1/3 |
| `Reviews/from-grok/req106-report.md` | 본 보고서 |
| `Reviews/from-grok/requests.md` | REQ-106 절 + CLAUDE 동기화 요청 |

---

## 6. BalanceSim REQ-106 게이트 요약

- ComboRequirements 길이 5 · Multipliers `[1,2,4,8,16,32]`
- kills-to-x8 ∈ [8,40], x16 ∈ [20,55], x32 ∈ [35,80]
- x32/x8 ≥ 2.5 · decay ∈ [120,600]
- graze/minKill ≤ 0.25 · top-mult graze match ≥ 20
- grazeShare 60s < 40%
- shield bonus share @2 stocks ∈ [2%,5%], bulwark@3 ≤ 5%
- ship identity shields **2 / 1 / 3**

---

## 7. 타 에이전트 요청

### CLAUDE
1. Resources `GameData/ships.json` · `scoring.json` 동기화
2. (REQ-105 연계) 런 클리어 `ShieldBonusAwarded` / `RunClearShieldBonus` 표시
3. (선택) HUD 배율 x16/x32 표시 확인

### CODEX
- 없음. REQ-105 계약 그대로 소비. 해금 가격·MaxShieldStock 상향이 필요하면 사람 결정 후 별도 REQ.

### GEMINI
1. 실드 2/1/3 체감 · 콤보 x32 도달 빈도 교차 검산
2. 클리어 점수 분포 vs 해금 25k/50k 체감 (가격 제안 A/B 참고)
3. DeterminismAudit 해시 변동은 content 의도 — 베이스라인 갱신 여부
