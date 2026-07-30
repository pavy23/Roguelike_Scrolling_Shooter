# REQ-060 첫 스테이지 난이도 (GROK / 2026-07-30)

**상태:** content 수치 반영 완료 · 전부 잠정(§7)  
**검증:** BalanceSim (sim Core + content GameData) REQ-060 **PASS** · 조립/드롭/보스 TTK 전부 그린  
(콜로설 normal-gen 제외 1건은 sim Core가 content의 `IsHiddenOnlyColossalBoss` 스킵을 아직 안 가진 기존 괴리)

## 1. 원인 진단 (수치)

누적 변경(REQ-052 성장·4티어 / REQ-053 중간보스 / REQ-055 잔해 / 실드 단일 자원) 후 **스테이지 1 기준**으로 재측정.

| 원인 | 증거 | 심각도 |
|---|---|---|
| **중간보스 HP + 전역 균등 선택** | mini_* HP 2400–4500, 스테이지 무시 랜덤. 스타터 DPS 75 기준 TTK **32–60s** | **치명** |
| **초반 화력 바닥** | Main L0=L1=75 DPS, L2에 순수 캡슐 4개(1+L+L²). 첫 레벨업이 데미지에 무의미 | **높음** |
| **스테이지1 고HP 세그** | `seg_sine_rush` avg pool 내 hp 1052 (elite_sine 620) vs 다른 세그 116–190 | **중간** |
| **보스+저화력** | boss 9000 @75 = 120s 연속 풀히트 | **중간** (성장 후 해소 전제) |
| 실드 스톡 3 | 장시간 중간보스·보스에서 소진 | **2차** (싸움을 짧게 하면 완화) |
| 잔해 기믹 | stage1 파괴 가능 위주, 막힘보다 파밍 | **낮음** (유지) |

**하지 않은 것:** 성장 곡선 1+L+L² 되돌림, 실드 상한 3 변경, 후반 보스/세그 HP 인하.

## 2. 조치 (초반 관대 · 후반 유지)

| 파일 | 변경 |
|---|---|
| `ships.json` | starter `startingPowerUpLevels` **[2,0,0,0]** → Main2 시작 (DPS **128.6**) |
| `enemies.json` midboss | horror **800** / destroyer **1100** / crystal **1400** / walker **1600** |
| `enemies.json` drops | `noDropWeight` 15→**13**, 스크랩 잡몹 dropW 소폭 상향 |
| `waves.json` | `seg_sine_rush` elite_sine→zako_sine_slow (seg HP 1052→**532**) |
| `waves.json` | `boss_stage1` 9000→**8500** (full@1880 = 4.5s 플로어 유지) |
| `Tools/BalanceSim` | `CheckStageClearability` 게이트 + 캡슐 EV 밴드 상한 20 |

### 실드 상한 제안 (바꾸지 않음)

스톡 3 + starter maxHp 3 유지. 중간보스 TTK를 18s 이하로 줄인 뒤 예상 피격 ≈3.2 (리페어 보상 1회 여유).  
**더 늘리려면** 근거: mid-skill 클리어율이 50% 미만으로 남을 때 §7 재논의.

### CODEX 요청 (필수 후속)

1. **중간보스 스테이지/테마 가중 선택** — 지금 전역 균등이라 stage1에 walker가 온다. 홈 테마 soft prefer + stage 인덱스 스케일.
2. **sim GenerateCore에 `IsHiddenOnlyColossalBoss` 복원** — content Core엔 있음, sim에 없어 stage5 normal gen이 콜로설을 뽑음.
3. content ← sim 병합 후 laser/gimmick 파서로 content 워크트리 `dotnet test`/BalanceSim 자체 통과.

## 3. 스테이지별 난이도 표 (조정 후)

모델: Opening 3seg + MidBoss + Closing 3seg + StageBoss.  
스타터 Main2 DPS 128.6 · mid-skill 적중 70% → **eff 90**.  
중간보스는 전역 풀 평균/최악. reach DPS는 보스 앵커.

| Stage | Theme | 적 수≈ (OC) | pool avgHP/seg | OC HP | mid avg | boss HP | **총 HP** | need DPS@120s | reach DPS | full TTK@eff | hits≈ | 클리어 |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | scrapyard | 75 | 207 | 1240 | 1225 | 8500 | **10965** | 91 | 382 | **41s** | 3.16 | **CLEAR** |
| 2 | hive | 89 | 790 | 4739 | 1225 | 14500 | 20464 | 171 | 600 | 49s | 3.30 | report |
| 3 | fortress | 93 | 1434 | 8604 | 1225 | 18000 | 27829 | 232 | 720 | 55s | 3.36 | report |
| 4 | nebula | 86 | 1956 | 11734 | 1225 | 22500 | 35459 | 295 | 880 | 58s | 3.40 | report |
| 5 | core | 87 | 2744 | 16464 | 1225 | 28000 | 45689 | 381 | 1050 | 62s | 3.48 | report |

### 중간보스 TTK @ starter eff 90

| id | HP | TTK |
|---|---:|---:|
| mini_horror | 800 | 8.9s |
| mini_destroyer | 1100 | 12.2s |
| mini_crystal | 1400 | 15.6s |
| mini_walker | 1600 | **17.8s** (게이트 ≤18s) |

### 스테이지1 성장 여유

- 캡슐 EV (3seg room) ≈ 8.6 · open+close ≈ **17** (+ mid drop + 2/3-choice 보상)
- Main2→Main3 순수 비용 **7** (1+L+L²) → 보스 전 Main3 또는 Opt1 현실적
- 보스 8500 @ reach 450 → mid TTK **18.9s** (튜토리얼 밴드)

### 조정 전 (참고)

| 항목 | 전 | 후 |
|---|---:|---:|
| starter Main DPS | 75 | **128.6** |
| mid worst HP / TTK@75 | 4500 / 60s | **1600 / 17.8s@90** |
| stage1 총 HP (avg mid) | ~14095 | **~10965** |
| stage1 pool avgHP | 299 (sine_rush 1052) | **207** |
| boss_stage1 | 9000 | **8500** |

## 4. 검증 명령

```text
# GameData + BalanceSim Program (sim Core 필요: laser 파서)
cd Tools\BalanceSim
dotnet run --project _req060_simcore.csproj
# → REQ-060 CLEAR / 조립·드롭·보스 PASS
# → colossal normal-gen FAIL 1건 = sim에 IsHiddenOnlyColossalBoss 부재

cd Tools\CoreStandalone
dotnet test
# content Core: laser 파서 미포함 시 waves 파싱 실패 가능 (병합 대기)
# 알려진 예외: RepositoryApprovedV2Files 30→31, 리듬 런 2건 (CODEX REQ-057)
```

결정론: content Core 단독 러너는 기믹 파서 병합 후 재감사. 수치-only 변경이라 시드 해시는 적 HP/드롭 경로만 변함.
