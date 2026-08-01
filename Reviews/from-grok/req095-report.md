# REQ-095 GROK 구현·검증 보고서

- 작업일: 2026-08-01
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: `main` REQ-094 Core 머지 (`gauge/option/shield ActivationBanned` 계약 축)
- 결과: **PASS**

## 결론

사람 승인 SPARTAN 자기 제약형 계약 3종을 `waves.json` contracts에 추가했다.  
배율·가중치는 시작 제안 값을 **유지**하되, BalanceSim risk-adjusted EV로 기존 고위험 계약 대비 균형을 검산했다 (아래 §3).

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **477/477** |
| BalanceSim | **all green** (REQ-095 게이트 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + 256-seed cap-boundary) |

---

## 1. 추가 계약 (`GameData/waves.json`)

| id | riskTier | 제약 | score× | dens | weight | 한 줄 |
|---|---|---|---:|---:|---:|---|
| `spartan_protocol` | **extreme** | `gaugeActivationBanned` | **1.6** | ×1 | **1** | 스테이지 중 게이지 발동 전면 금지 (캡슐 적립·커서 순환은 유지) |
| `no_option_run` | **high** | `optionActivationBanned` | **1.3** | ×1 | **2** | OPTION 슬롯만 발동 금지 |
| `bare_hull` | **high** | `shieldActivationBanned` | **1.4** | ×1 | **2** | SHIELD 슬롯만 발동 금지 |

- 밀도·드롭·기믹 배수는 **전부 중립** — 리스크는 자기 제약만. 기존 density 최악 게이트(`escort_run` ×1.5)는 그대로.
- destination: 전부 `nextStage` (기본). terminal(`end_run`/`uncharted`)과 분리.
- 카탈로그: **1 standard + 11 nextStage specialty + end_run + uncharted = 14 entries** (이전 11).

Core 파서 optional 필드 (REQ-094):

```json
"gaugeActivationBanned": true   // 또는 option / shield
```

---

## 2. 가중치 설계 — 극한 카드 희소성

specialty 풀 가중 합 = **31** (기존 26 + SPARTAN 5).

| 지표 | 수치 | 해석 |
|---|---:|---|
| `spartan_protocol` 1-slot pick P | **3.2%** | extreme 단독 후보 빈도 |
| 대략 offer P (옵션 2..3 = specialty 1~2장) | **≈4.7%** | 표준 항로 대비 극한 카드가 보드에 자주 안 뜸 |
| `no_option` / `bare_hull` 1-slot P | **6.5%** each | escort_run(w=2)과 동급 희소 |
| SPARTAN 시리즈 합 1-slot share | **16.1%** | 25% 게이트 여유 |
| extreme specialty 총 weight | **1** | 비-extreme 최다 weight(4) 미만 강제 |

BalanceSim 게이트:

- extreme nextStage weight 합 ≤2, spartan w≤1, 비-extreme 최다 weight 미만
- offer P(spartan) ≤ 8%
- series 1-slot share ≤ 25%

---

## 3. EV 균형 (시작 제안 유지 근거)

모델 (provisional §7, BalanceSim REQ-095):

```
severity = dens압력 + ban(0.75 full / 0.40 option / 0.25 shield) + 캡슐 가뭄 보정
adjEV  = score × (1 − 0.35 × severity)
```

| 계약 | score× | severity | adjEV | 비고 |
|---|---:|---:|---:|---|
| `scrap_bounty` | 1.50 | 0.30 | **1.340** | 기존 최고 점수 추격 |
| `bare_hull` * | 1.40 | 0.25 | **1.277** | 숙련 플레이어 점수 표현 (실드 없이 버티기) |
| `spartan_protocol` * | 1.60 | 0.75 | **1.180** | 최고 배율 × 성장 잠금 |
| `escort_run` | 1.40 | 0.50 | 1.155 | 밀도 최악 |
| `risk_lane` | 1.30 | 0.40 | 1.118 | 밀도·캡슐 트레이드 |
| `no_option_run` * | 1.30 | 0.40 | **1.118** | risk_lane과 동 EV, 다른 리스크 축 |

### 왜 시작 제안을 그대로 뒀는가

1. **spartan ×1.6 / w=1** — adjEV(1.18)가 scrap(1.34)보다 **12% 낮음**. 배율을 더 올리면 scrap 추월 위험; 내리면 extreme 보상이 약해짐. 희소 weight로 출현을 억제하는 쪽이 맞다.
2. **no_option ×1.3 / w=2** — risk_lane과 adjEV 일치. 옵션 부재 vs 밀도 증가는 플레이 스타일 선택의 동치 교환.
3. **bare_hull ×1.4 / w=2** — adjEV가 scrap 바로 아래. 실드 밴은 숙련자 부담이 작아 보이지만 후반 밀도·보스 구간에서 칩 데미지 사망 리스크가 실제로는 더 큼(모델 severity 0.25는 보수적). 배율을 1.35로 깎으면 “실드 없이 점수” 매력이 과도하게 줄 수 있어 **1.4 유지**.

피어 밴드 게이트: lo=1.006 .. hi=1.501 — 3종 전부 통과.  
spartan이 scrap을 8% 초과 지배하지 않음.

**수치 잠정** (AGENTS.md §7) — 실플레이 후 사람이 확정.

---

## 4. BalanceSim / 테스트 변경

| 파일 | 내용 |
|---|---|
| `Tools/BalanceSim/Program.cs` | REQ-095 `CheckSpartanContracts` + specialty 상한 **6..10 → 6..12** + 카탈로그 ban 플래그 덤프 |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | 골든 카탈로그 11→**14** + SPARTAN 3축 ban 존재 검산 |

(과거 content 작업과 동일: 골든 카운트·BalanceSim은 데이터 변경과 동반 수정.)

---

## 5. 변경 파일

- `GameData/waves.json` — SPARTAN 3계약
- `Tools/BalanceSim/Program.cs` — REQ-095 게이트
- `Assets/Tests/EditMode/GameDataParserTests.cs` — 카탈로그 카운트·ban 검산
- `Reviews/from-grok/req095-report.md` — 본 보고서
- `Reviews/from-grok/requests.md` — CLAUDE Resources 동기화 요청

---

## 6. 후속 요청

### CLAUDE
1. [ ] `Assets/Resources/GameData/waves.json` 동기화 (SPARTAN 3계약)
2. [ ] 계약 카드 UI: extreme tier 색(이미 Extreme 색 있음) + SPARTAN 한 줄 카피
   - spartan: “NO GAUGE ACTIVATE · SCORE ×1.6”
   - no_option: “NO OPTION · SCORE ×1.3”
   - bare_hull: “NO SHIELD · SCORE ×1.4”
3. [ ] 발동 거부 시 `PowerUpActivationResult.Contract*Banned` → “CONTRACT LOCK” 피드백 (REQ-094 관측 축)

### CODEX
- 없음 (ActivationBanned 축은 REQ-094에서 완료).

### 사람 (§7)
- [ ] SPARTAN score× / weight 손맛 확정 (현재 잠정 1.6/1.3/1.4 · w=1/2/2)
- [ ] bare_hull이 스코어보드에서 “거의 무료 ×1.4”로 느껴지면 ×1.35 또는 severity 재보정 후보
