# REQ-029 세그먼트 weight · 조우 5종 · 캡슐 자석 드롭 검산

**작성:** GROK (content) · 2026-07-29  
**상태:** 전부 잠정 (AGENTS.md §7) — 사람 플레이 피드백 전 최종 확정 금지.  
**검증:** `dotnet test` 254/254 · `Tools/BalanceSim` PASS

---

## 1. 세그먼트 weight (`GameData/waves.json`)

DefaultWeight = **10**. 평범한 편성은 근처(10–12), 특색·밀집·미로는 낮춤.

| 대역 | weight | 역할 | 예 |
|---:|---:|---|---|
| workhorse | 11–12 | 인트로·기본 라인 | `seg_intro_line`, `seg_scrap_debris_line` |
| baseline | 9–10 | 표준 테마 웨이브 | `seg_hive_brood_wave`, `seg_sine_pair` |
| solid special | 6–8 | 중간 밀도·팩 | `seg_scrap_tumbler_pack`, `seg_sandwich` |
| spectacle | 4–5 | 밀집·미로·쇼케이스 | `seg_swarm_fast`, `seg_hive_nest_choke` |
| peak rare | 2–3 | 최밀집/게이트 | `seg_core_final_gauntlet`(2), `seg_fortress_interceptor_assault`(3) |

**분포:** n=38 · min=2 max=12 mean≈6.82 · low(w≤5)=15 · high(w≥10)=8  
**BalanceSim:** `CheckSegmentWeights` PASS.

CLAUDE: `Assets/Resources/GameData/waves.json` ← `GameData/waves.json` 동기화 필요.

---

## 2. 조우 5종 균형 (BalanceSim 샘플 n=400/타입)

Core 잠정 배수 (데이터 이관 전 · **권고만**):

| 타입 | 세그 | 보스 | HP | 드롭 | 점수 | 보상 |
|---|---:|---|---|---|---|---|
| Normal | 3 | O | ×1 | ×1 | ×1 | 1 pick |
| Elite | 1 | O | ×1.5 | ×1 | ×1 | 1 pick + 모디파이어 가중 |
| Supply | 1 (최저 스폰) | X | ×1 | ×4 | ×1 | 1 pick |
| Hazard | 3 + 장애물 주입 | O | ×1 | ×1 | ×1.5 | 1 pick |
| Rare | 3 | O | ×2 | ×1 | ×1 | **2 picks** · 라우트 등장 12% |

### 측정 요약 (스테이지 1–5 × 5테마 × 16시드)

| 타입 | spawnHP | bossHP | E_caps | obs |
|---|---:|---:|---:|---:|
| Normal | 799 | 2480 | 11.59 | 7.6 |
| Elite | 410 | 2480 | 3.87 | 2.6 |
| Supply | 137 | 0 | 5.52 | 0.8 |
| Hazard | 799 | 2480 | 11.59 | 11.9 |
| Rare | 1599 | 2480 | 11.59 | 7.6 |

### 판정 · Core 권고 (데이터로 못 바꿈)

1. **Elite 위험 대비 보상**  
   - combat-load(elite/normal) ≈ **0.88** — 보스가 총량의 ~75%를 차지해 1세그여도 총 부하가 높다.  
   - 보상은 모디파이어 가중 1픽뿐. **권고:** (a) Elite 클리어 시 보상 2픽, 또는 (b) Elite 전용 점수×1.25–1.5, 또는 (c) HP 배수를 3/2 → 5/4로 완화.  
   - 사람 플레이에서 “짧게 아프고 이득 적음”이면 (a) 우선.

2. **Supply 최적해 위험**  
   - 전투 부하 normal 대비 **~4%**, 보스 없음, 드롭×4 → E_caps≈5.5/노드(자석 후).  
   - 라우트에서 Normal/Elite/Supply/Hazard가 **균등**이면 Supply 올픽이 파워 스노우볼.  
   - **권고:** Supply 라우트 가중 하향(예: 등장 슬롯 확률 ½), 또는 drop×4 → ×2–3, 또는 Supply 보상에서 슬롯 직가 제외·캡슐/수리만.  
   - 배수는 `StagePlan.CapsuleDropMultiplier*` (Core) — GameData 이관 후보.

3. **Rare 12%**  
   - offer≈12% · 제시 시 선택률 45% 가정 → 플레이 비율 ≈**5.4%** 스테이지 전이.  
   - HP×2 + 보상 2픽은 리스크/리워드 대칭에 가깝다.  
   - **권고:** 현 12% 유지. Rare가 “안 보인다”는 피드백이면 15–18%로 상향; 남발이면 8–10%.

4. **Hazard 점수 1.5배**  
   - 장애물 밀도 ≈×1.57 vs score×1.50 → 대략 정합.  
   - **권고:** 미로가 과도하게 아프면 score 3/2 → 7/4 또는 장애물 주입량 축소; 시시하면 현행 유지.

---

## 3. 캡슐 자석 후 드롭률

| 항목 | 변경 전 | 변경 후 (잠정) |
|---|---:|---:|
| `dropTable.noDropWeight` | 8 | **12** |
| weight-biased E_caps/seg | ~6.1 | **4.67** |
| E_stage (3 seg) | ~18.4 | **14.0** |
| Supply 1노드 E_caps | ~13+ | **~10.3** |

자석(반경 3u, Core)으로 회수≈전량 가정 시 기존 noDrop=8은 스테이지당 ~18캡슐·게이지 과활성 우려.  
**noDrop 8→12**로 스테이지 EV를 밴드 [10,16] 안으로 맞춤. 개별 `dropWeight`는 유지.

CLAUDE: `Assets/Resources/GameData/enemies.json` 동기화.

---

## 4. 파일 변경

| 경로 | 내용 |
|---|---|
| `GameData/waves.json` | 38세그먼트 `weight` 부여 |
| `GameData/enemies.json` | `noDropWeight` 8→12 |
| `Tools/BalanceSim/Program.cs` | weight / encounter / magnet-drop 검산 추가 |

---

## 5. CODEX / CLAUDE 후속

- **CLAUDE:** Resources GameData 동기화 (`waves.json`, `enemies.json`).  
- **CODEX (선택, 사람 승인 후):** Elite 보상 픽 수 · Supply 라우트 가중 · 조우 배수를 GameData 스키마로 이관.  
- **사람:** §7 최종 확정 — weight 분포, noDrop=12, Rare 12%, Hazard 1.5×.
