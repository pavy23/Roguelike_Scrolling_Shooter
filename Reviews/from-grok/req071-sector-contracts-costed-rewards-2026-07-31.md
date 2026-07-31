# REQ-071: 섹터 계약 + 대가 있는 보상 데이터

**상태:** GameData 반영 완료 · 수치 잠정 (AGENTS.md §7)  
**검증:** `dotnet test` 383/383 · BalanceSim 전부 그린 (REQ-060 + REQ-071 포함)

## 설계 원칙

런을 빚는 결정은 **수치 버프가 아니라 거래**여야 한다.
- 계약: 표준 항로는 항상 옵션 0. 저위험에도 대가(카드 수·점수·드롭)를 붙여 표준이 죽지 않게.
- 보상: mid = 전술 정비, main = 빌드 결정. 대가 카드는 무료 동급보다 **확실히 세다**.
- 모디: maxStacks/maxPerRun 2, maxCombinedModifierCost 5 → 몰빵(더블 스택) 가능, 4종 풀스택 불가.

## A. 계약 목록 (`waves.json` contracts)

| id | riskTier | 거래 한 문장 | dens | cap | bomb | gim | Δcards | score | w |
|---|---|---|---:|---:|---|---:|---:|---:|---:|
| `standard_route` | **safe** (표준/무채) | 무보정 표준 항로 | ×1 | ×1 | — | ×1 | 0 | ×1 | 1 |
| `risk_lane` | high | 더 세고 더 줍는다 | ×1.4 | ×1.5 | — | ×1 | 0 | ×1.3 | 4 |
| `lockdown_zone` | high | 기믹 지옥 대신 폭탄 보장 | ×1 | ×1 | ×1.5 + 보장 | ×1.5 | 0 | ×1.15 | 3 |
| `supply_line` | low | 캡슐은 풍부, 보상 카드 −1 | ×1 | ×1.3 | — | ×1 | −1 | ×1 | 4 |
| `drydock_route` | low | 안전(적 −25%) 대신 성장·점수 손해 | ×0.75 | ×1 | — | ×1 | −1 | ×0.8 | 3 |
| `high_stakes` | high | 보상 카드 +1, 밀도 +25% | ×1.25 | ×1 | — | ×1 | +1 | ×1 | 4 |
| `scrap_bounty` | high | 점수 몰빵, 캡슐은 말라 | ×1.2 | ×0.7 | — | ×1 | 0 | ×1.5 | 3 |
| `soft_landing` | low | 폭탄 여유(×2), 점수·밀도 소폭 손해 | ×0.9 | ×1 | ×2 | ×1 | 0 | ×0.85 | 3 |
| `escort_run` | high | 최악 밀도 호송, 폭탄 보장 + 점수 | ×1.5 | ×1.25 | 보장 | ×1 | 0 | ×1.4 | 2 |

- 옵션 수: 2..3 (항상 표준 + 1~2 특수)
- **riskTier 표기:** Core 파서는 `safe`/`low`/`high`/`extreme`만 허용. 표준 항로는 **반드시 `safe` + 완전 중립**. Presentation 3색 매핑 제안: Safe=무채(표준) / Low=파랑 / High=빨강. (`extreme` 미사용)

## B. 보상 풀 (`rewards.json` schema v4)

| pool | 개수 | 배치 |
|---|---:|---|
| **mid** | 5 | `capsules_5`, `repair_hp_1`, `bomb_stock_1`, `passive_move_speed_1`, `slot_shield_1` |
| **main** | 20 | 슬롯 3(Main/Missile/Option), 패시브 연사·데미지, mod 4, 미사일 계열 3, 옵션 포메이션 3, 대가 5 |
| **both** | 0 | 최소(공용 없음 — mid/main 분리 선명) |

### 신설 대가 보상 (5종, 전부 main)

| id | 이득 | 대가 | 비고 |
|---|---|---|---|
| `overclock_core` | damageUp **2** (+4 base dmg) | shieldMaxDown 1 | 무료 damageUp 1의 2배 |
| `light_frame` | moveSpeedUp **6** | bombMaxDown 1 | 무료 move 3의 2배 |
| `ammo_mod` | fireRateUp **2** | capsuleDropWeightDown 2 | 연사 2틱, 드롭 가중 −2 |
| `glass_cannon` | damageUp **3** | shieldMaxDown 2 | 스테이지 3+ 고위험 빌드 |
| `bomber_payload` | bombStock **2** | moveSpeedDown 1 | 폭탄 몰빵, 기동 대가 |

### 모디파이어 스택 (4종)

| 필드 | 값 |
|---|---|
| maxPerRun / maxStacks | **2** (1→2 개방) |
| stackable | true |
| stackStrength | 1 |
| interactionCost | 1 |
| maxCombinedModifierCost | **5** (더블×2 + 싱글 1, 또는 더블+싱글×3) |

## C. 최악 조합 검증 (BalanceSim REQ-071)

| 지표 | 수치 |
|---|---|
| 최밀 계약 | `escort_run` dens×**1.50** |
| S1 baseline totHP / TTK@reachEff | 11030 / **41s** |
| S1 worst dens (OC×1.5) totHP / TTK | 11650 / **44s** |
| 게이트 (≤140s×1.35 = 189s) | **PASS** |
| REQ-060 stage1 CLEAR | **유지** (TTK 41s, hits≈3.21) |

밀도는 open+close 스폰만 스케일(미드/보스는 계약 밀도 미적용) — Core 적용 방식과 동일 가정.

## 변경 파일

- `GameData/waves.json` — `contracts` 카탈로그
- `GameData/rewards.json` — schema v4, pool/costs/스택
- `Tools/BalanceSim/Program.cs` — 모디 maxPerRun 2..3, REQ-071 게이트
- `Assets/Tests/EditMode/GameDataParserTests.cs` — 보상 20→25, contracts 존재 검산

## 후속

1. [ ] CLAUDE: `Assets/Resources/GameData/waves.json` · `rewards.json` 동기화
2. [ ] CLAUDE: 계약 카드 UI — riskTier 3색, 거래 한 줄 카피
3. [ ] CLAUDE: 대가 보상 카드 — Gains/Costs 표시
4. [ ] 사람: 계약 weight·배수 손맛 확정 (AGENTS §7)
