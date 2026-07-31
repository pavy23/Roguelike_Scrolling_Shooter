# REQ-075 — 7슬롯 게이지 데이터 + 레이저 적 (content)

**작성:** GROK · 2026-07-31  
**상태:** GameData 반영 · 394/394 · BalanceSim green · 시드 해시 일치  
**DeterminismAudit suite:** seed-0-first가 3/5 스테이지에서 틱 예산 소진 (보스 잔여 HP 40) — **CODEX 요청**

---

## A. 게이지 슬롯 (`weapons.json` schema **v6**)

표시 순서 (그라디우스 문법, 앞 싸게 / 뒤 비싸게):

| # | 슬롯 | nameKey | maxLevel | 비용 곡선 | L0→max 순수 캡슐 | 비고 |
|---:|---|---|---:|---|---:|---|
| 0 | Speed | Speed | 5 | flat **1** | 5 | +1.0 u/s/레벨 |
| 1 | Missile | Missile | 3 | 1+L+L² | 11 | 기존 성장 |
| 2 | Double | Double Shot | **1** | flat **2** | 2 | 무기 모드 단발 |
| 3 | Laser | Laser | **1** | flat **3** | 3 | 무기 모드 단발 |
| 4 | Triple | Triple Shot | **1** | flat **3** | 3 | 무기 모드 단발 |
| 5 | Option | Option | 4 | 2+L+L² | 28 | 후방 고가 |
| 6 | Shield | Shield | 3 | 2+2L+L² | 17 | 최후방 고가 |

- MainShot은 히든 공유 파워 축 (게이지 비표시). `powerUpCostCurve` 1+L+L² 유지.
- 무기 모드 maxLevel=1 (스키마 강제). 상호 배타 전환.
- exclusive full-power (모드 1개) ≈ **64 캡슐** → EV 9.7 기준 ~**6.6 스테이지** (라우팅 낭비 미포함).

### SPEED 곡선

| 레벨 | 가산 (u/s) | 누적 보너스 | 체감 (base 9.5) |
|---:|---:|---:|---:|
| 0 | — | 0 | 9.5 (느림) |
| 1 | +1.0 | 1.0 | 10.5 |
| 2 | +1.0 | 2.0 | 11.5 |
| 3 | +1.0 | 3.0 | 12.5 |
| 4 | +1.0 | 4.0 | 13.5 |
| 5 | +1.0 | 5.0 | 14.5 |

“처음 느리다가 점점 빨라지는” = 낮은 base + 레벨 누적 (스키마는 레벨당 고정 보너스).

### DOUBLE 계열 (primaryWeaponFamilies)

| family | dmg | interval | ways | step (lut) | ST DPS | volley DPS |
|---|---:|---:|---:|---:|---:|---:|
| vulcan | 10 | 8 | 1 | 0 | **75.0** | 75.0 |
| double | 7 | 10 | 2 | **16** (±45°) | **42.0** | 84.0 |
| laser | 16 | 14 | 1 | 0 | **68.6** | 68.6 |
| spread (Triple) | 6 | 10 | 3 | 4 | **36.0** | 108.0 |

- ST max/min = **2.08** (게이트 ≤2.25) PASS  
- Double: Core Spread는 대칭 발사 → 그라디우스 “전방+45° 상방”은 ±45° V로 근사 (비대칭은 Core 요청 필요).

---

## B. 레이저 적

| id | 역할 | cycle | life | 배치 |
|---|---|---:|---:|---|
| `laser_sentry` | 포트리스 중형 정지 레이저 | 240t | 90t | `seg_fortress_sentry_grid` 1기 (Y=4.5) |
| `prism_beamer` | 네뷸라/스크랩 중형 정지 레이저 | 210t | 80t | scrap tumbler + nebula ribbon 각 1기 |

- 지형 게이트 없는 scrapyard/nebula에 배치 → 초중반 “레이저 쏘는 적” 노출.
- MaxLasers=8. 세그먼트 피크 소스(적+이미터) ≤4.
- 카탈로그 32→**34**.

---

## C. 보상 (`rewards.json`)

| id | 변경 | 비고 |
|---|---|---|
| `light_frame` | moveSpeedUp×6 → **SlotLevel Speed ×2** + bombMaxDown | 이중 성장 제거 (대가 경로) |
| `passive_move_speed_1` | **moveSpeedUp 잔류** weight 4 mid | 아래 리스크 참고 |

### moveSpeedUp 잔류 이유 (잠정)

1-tick `RhythmRunGenerator` 하니스가 **mid weight-4 카드가 게이지를 건드리면**(Collect/GrantLevels) rooms=3에서 정지한다.  
동일 weight로 SlotLevel/Capsules/Repair로 바꾸면 재현. moveSpeedUp(컨피그 속도만)은 통과.  
→ **CODEX 요청:** mid 보상 후 게이지 변이 + 초단 세그먼트 진행 버그 조사. 수정 후 mid `passive_move_speed_1` → `slot_speed_1` 교체.

---

## 검증

| 항목 | 결과 |
|---|---|
| `dotnet test` | **394/394** |
| BalanceSim | **all green** (4계열 DPS 표 + 게이지 비용 곡선) |
| 시드 해시 2회 | `0x48DA7A` 2스테이지 `BC2E6E4B54819BAD` 일치 |
| DeterminismAudit `--suite` | seed-0-first 3/5 (보스 잔여 ~40 HP) — CODEX |

---

## 요청

### CODEX
1. [ ] DeterminismAudit suite가 7슬롯 게이지 + 신규 비용 곡선에서 5/5 완주하도록 자동 플레이/틱 예산 조정 또는 원인 수정.
2. [ ] `RunCompletionTests` RhythmRunGenerator: mid 보상 `GrantLevels`/`Collect` 후 진행 정지 원인 (rooms=3, 50k tick hang).

### CLAUDE
1. [ ] Resources `GameData/*.json` 동기화 (weapons v6, enemies, waves, rewards).
2. [ ] HUD nameKey: `Speed` / `Missile` / `Double Shot` / `Laser` / `Triple Shot` / `Option` / `Shield` 풀네임 표시.
