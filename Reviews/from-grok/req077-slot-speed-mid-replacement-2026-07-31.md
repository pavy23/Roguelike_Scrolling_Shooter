# REQ-077 — mid `slot_speed_1` 교체 (content)

**작성:** GROK · 2026-07-31  
**상태:** GameData 반영 · 검증 통과 · 커밋  
**선행:** REQ-076 (리듬 하니스 보스 추적 수정, 395/395 + AUDIT PASS)

## 검증

| 항목 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **395/395** |
| BalanceSim (`VerifyThemeAssembly`) | **all green** (REQ-077 speed economy PASS) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary) |
| 동일 시드 2회 해시 | suite 시나리오별 2회 일치 강제 |

---

## 변경

| id | before | after |
|---|---|---|
| mid weight-4 카드 | `passive_move_speed_1` (`moveSpeedUp` amount 3, maxPerRun 4) | **`slot_speed_1`** (`slotLevel` **Speed** amount **1**, weight **4**, pool mid) |

- `light_frame` (main, costed Speed×2 + bombMaxDown) 유지.
- 카탈로그에 `moveSpeedUp` 카드 없음 → 이중 성장 경제 제거.
- 보상 카드 수 25 유지 (id 교체만).

## 이유

REQ-075에서 mid weight-4 게이지 변이가 1-tick 리듬 하니스 hang을 유발해 `moveSpeedUp`을 잔류시켰음.  
REQ-076이 hang 원인이 하니스 조준(보스 Y 미추적)임을 확인하고 `slot_speed_1` 카탈로그 회귀 테스트까지 추가함 → content 교체 안전.

## BalanceSim

- free mid SlotLevel Speed 필수
- residual `moveSpeedUp` → **FAIL** (WARN 해제)

## CLAUDE 요청

- [ ] `Assets/Resources/GameData/rewards.json` 동기화 (`slot_speed_1` 반영)
- [ ] 보상 UI: Speed 슬롯 레벨 카드 라벨 (기존 SlotLevel 경로면 무작업)

## CODEX

- 추가 요청 없음. `EnsureMidSpeedSlotReward`는 카탈로그에 `slot_speed_1`이 있으면 그대로 사용.
