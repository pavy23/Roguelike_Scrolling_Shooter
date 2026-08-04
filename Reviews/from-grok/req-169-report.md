# REQ-169 — 히든 보스 HP 38,000 → 50,000 (사람 확정)

**브랜치:** `content` (main `eb34dc8` 반영 후)  
**소유:** GROK · `GameData/waves.json` + BalanceSim 게이트 정합

## 배경

REQ-167에서 "480초 때려도 마지막 페이즈 미도달"을 근거로 62k→38k 하향을 넣었으나
**근거가 틀렸다.**

| 가정 | 실측/수정 |
|---|---|
| 헤드리스 DPS ≈ 141 HP/s | 하네스가 **조준 없이 훑기만** 함. BalanceSim 도달 DPS 가정은 **1000** (약 7×) |
| HP가 원인 | `RunManager.CreateBossOnlyPlan`이 **form2를 통째로 빠뜨림** (사람/CODEX 수정, 9da2656). HP를 다 깎아도 마지막 페이즈가 존재하지 않았음 |

그리고 38k 상태는 곡선이 뒤집혀 있었다:

```
하이브   14,500
성운     20,000
코어     28,000   ← 공개 최종 보스
전함     44,000   ← REQ-168 유지
히든     38,000   ← 전함·코어보다 가벼움 (이상)
```

사람 2번안: **히든 둘 50,000** — 전함보다 확실히 무겁고 구 62k보다 가볍다.

## 변경

### `GameData/waves.json`

`boss_leviathan` / `boss_broodmother` 본체 **hp 38000 → 50000**.  
파츠는 38k 배분 비율로 ×(50/38) 스케일 후 합이 정확히 50000이 되도록 코어에서 1 조정.  
**form2 HP 7500 유지.**

#### boss_leviathan (합 50,000)

| 파츠 | 38k | 50k | 최초 취약 |
|---|---:|---:|---|
| turret_spine | 4600 | **6053** | 0 |
| head_cowl | 4300 | **5658** | 0 |
| rear_engine | 3400 | **4474** | 0 |
| lower_launcher | 4600 | **6053** | 0 |
| shield_emitter | 4000 | **5263** | 0 |
| blade_limb_upper | 2200 | **2895** | 1 |
| blade_limb_lower | 2200 | **2895** | 1 |
| railgun | 3000 | **3947** | 1 |
| rib_gate | 3700 | **4868** | 1 |
| core | 6000 | **7894** | 2 |

| 페이즈 | removable | floor | 다음 문턱 | margin |
|---|---:|---:|---:|---:|
| 0 | 27501 | 22499 | 25000 (0.5) | **2501** |
| 1 | 42106 | 7894 | 10000 (0.2) | **2106** |
| 2 | 50000 | 0 | 0 | — |

코어 7894 ≤ 0.2×50000=10000 → 마지막 페이즈 문턱 아래.

#### boss_broodmother (합 50,000)

| 파츠 | 38k | 50k | 최초 취약 |
|---|---:|---:|---|
| tentacle_left/right | 900×2 | **1184×2** | 0 (재생 없음 유지) |
| sac_left/right | 6200×2 | **8158×2** | 0 |
| sac_lower | 6700 | **8816** | 0 |
| maw | 11100 | **14605** | 1 |
| heart_core | 6000 | **7895** | 2 |

| 페이즈 | removable | floor | 다음 문턱 | margin |
|---|---:|---:|---:|---:|
| 0 | 27500 | 22500 | 25000 | **2500** |
| 1 | 42105 | 7895 | 10000 | **2105** |
| 2 | 50000 | 0 | 0 | — |

### BalanceSim 게이트 정합 (`Tools/BalanceSim/Program.cs`)

데이터 50k 적용 직후 colossal이 세 곳에서 깨졌다. **데이터 잘못이 아니라 게이트 기준 불일치.**

1. **`HeaviestPublicBossHp`가 전함(44k)을 포함**  
   → 50000/44000=**1.14** < 1.15.  
   사람 설계 문안은 "공개 최종 보스(**28,000**)" 대비 1.15~1.80.  
   **수정:** `WarshipEncounter != null` 제외 → 기준 = core 28000 → **50k/28k=1.79** (상한 근처, 통과).

2. **절대 TTK 밴드가 구 62k용**  
   - full-eff ≥40s @1500 → HP ≥60k 필요 (50k면 33.3s)  
   - vs-normal ratio [2.0, 2.5] → fight HP ≈80k 필요 (50k+form2면 1.45)  
   **수정 (REQ-169 의도):** mid TTK [45,60]s · full-eff ≥30s · vs-normal [1.20, 1.80].

상한(1.80)은 넘기지 않았다 — 45k~50k 하향 조정 불필요.

## TTK 스케치 (가정 DPS)

| DPS | 본체 50k | +form2 7.5k |
|---:|---:|---:|
| 1000 (reach) | 50.0s | 57.5s |
| 1500 (full-eff) | 33.3s | 38.3s |

## 검증

| 항목 | 결과 |
|---|---|
| `cd Tools\CoreStandalone && dotnet test` | **578/578 PASS** (Req158 페이즈 도달 포함) |
| BalanceSim colossal 게이트 | **PASS** (`PASS: colossal boss catalog / TTK / spawn / generate.`) |
| form2 HP | 7500 유지 |
| 코어 ≤ 총 HP 20% | 7894/7895 ≤ 10000 |

### BalanceSim 잔여 FAIL (REQ-169 범위 밖 · 사전 존재)

전체 러너는 아직 18 failure — 전함 절대 수치 잔존 게이트(REQ-111/116/157), brood tentacle regen 기대(REQ-166에서 재생 끔), shuffle clearability, 103b hive dig path 등.  
**colossal 블록은 통과**했고, 위 잔여는 이번 HP 되돌리기와 무관.

## 커밋 대상

- `GameData/waves.json` — 히든 둘 본체·파츠 HP
- `Tools/BalanceSim/Program.cs` — 공개 최종 기준에서 전함 제외 + TTK 밴드 정합
- 이 보고서
