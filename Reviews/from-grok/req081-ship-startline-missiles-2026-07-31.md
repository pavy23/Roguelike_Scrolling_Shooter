# REQ-081 — 기체 출발선 통일 + 미사일 데이터 (2026-07-31)

## A. 출발선 (사람 지정 · 불가침)

| 기체 | weaponType | Main 시작 | Speed | Missile | Option | Shield stock | move | 기본 미사일 | 게이지 정체성 |
|---|---|---|---|---|---|---|---|---|---|
| starter | **vulcan** | **L0** | 0 | 0 | 0 | **1** | 1/1 | downward_drop | Double |
| interceptor | **vulcan** | **L0** | 0 | 0 | 0 | **0** | 5/4 | straight | Triple |
| bulwark | **vulcan** | **L0** | 0 | 0 | 0 | **2** | 4/5 | homing | Laser |

정체성 무기는 게이지 Weapon 슬롯 활성화로만 획득.

## B. 미사일 5계열 (weapons.json schemaVersion **7**)

| 계열 | baseDmg | interval | minInt | red/Lv | speed | fallY | pierce | boom | growth%/Lv | dropDelay | turn/t | L1 ST | L3 ST | L6 ST |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| straight | 20 | 42 | 20 | 5 | 10 | 1.5 | 0 | — | **50** | 0 | 1 | 28.6 | 75 | 210 |
| spread_bomb | 12 (+16) | 54 | 36 | 5 | 6 | 9 | 0 | 16@1.75 | **45** | 0 | 1 | 31.1 | 71 | ~180 |
| piercing_lance | 40 | 70 | 44 | 6 | 16 | 0 | 2 | — | **40** | 0 | 1 | 34.3 | 74 | ~165 |
| downward_drop | 18 | 40 | 24 | 4 | 9 | 9 | 0 | — | **40** | **4** | 1 | 27.0 | 60 | specialty |
| homing | 14 | 48 | 28 | 4 | 8 | 0 | 0 | — | **30** | 0 | **1** | 17.5 | 33 | convenience tax |

- 유도 L1 ST < 직선 (17.5 < 28.6) — 편의 페널티.
- 하강: 전진 4t 후 낙하, 지상·하단 커버 특화 (ST 밴드는 combat trio만 hard gate).
- 레벨 growth: L1→L6 곡선이 체감되도록 계열별 차등. 미사일 L6 ST는 주무기 vulcan L6(≈525) 미만.

### 전환 보상 (main 풀, 낮은 가중)

- `missile_family_downward_drop` weight **1** stage 1+
- `missile_family_homing` weight **1** stage 1+

## C. 더블 각도

- `shotAngleLutSlots: [0, 8]` — 64-slot LUT에서 8 = **45°** 상향 (그라디우스 문법).
- forward `0` + upward diagonal `8`.

## 스테이지 1 L0 재검증

| 항목 | 수치 |
|---|---|
| starter Main L0 DPS | 75.0 (eff@70% = 52.5) |
| open capsule EV (3seg) | ≈8.55 → Main@mid **L2** (cost 1+3=4) |
| open+close EV | ≈17.1 → Main@boss **L3** |
| mid worst (mini_walker 1600) TTK@midEff | ≈17.8s ≤ 18s |
| S1 full TTK@reach | ≈41s ≤ 140s, hits≈3.21 ≤ 3.25 **CLEAR** |

시작 레벨은 건드리지 않음. 현재 초반 캡슐 EV로 mid 전 Main L2 도달이 가능하므로 **밀도/드롭 추가 보정 불필요**.

## 검증

- `dotnet test` **408/408**
- BalanceSim **all green** (스테이지1 L0 게이트 포함)
- DeterminismAudit `--suite` **AUDIT PASS** (시나리오별 2회 해시 일치)

## 스키마

- `ships.json` schemaVersion **3** (`missileFamily` 필수)
- `weapons.json` schemaVersion **7**
