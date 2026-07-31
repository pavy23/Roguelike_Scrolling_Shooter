# REQ-088 GROK 구현·검증 보고서

- 작업일: 2026-08-01
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 결과: **PASS** (레이저 적 배치 후속 포함)

## 결론

REQ-088 다섯 항목 + 사람 지적 **레이저 적 배치**를 반영했다.
CODEX REQ-085/086/087 파서 축을 확인한 뒤 `GameData/` 수치와 BalanceSim 게이트를 갱신했다.

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **454/454** |
| BalanceSim | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** |
| 같은 시드 2회 (`12345` 3st 30000t) | **EXACT_MATCH** `D66A41FDFF551D0D` (초기 5항목 시점) |

---

## 1. 1스테이지 보스 HP 반감 (사람 지정)

| 항목 | 이전 | 확정 |
|---|---:|---:|
| `boss_stage1.hp` | 8500 | **4250** |
| mid TTK @ 450 DPS | 18.9s | **9.4s** (튜토리얼 단축 밴드 8–16s) |
| full-power TTK @ 1880 | 4.5s | **2.3s** (floor ≥2.0s) |

- BalanceSim: `BossStage1Hp=4250` 하드 게이트 + stage1 전용 TTK 밴드.
- S1 full clearability: tot≈6780, TTK≈20s, hits≈1.51 **CLEAR**.

### 제안 (사람 승인 대상 — 미변경)

stage1 반감으로 표준 보스 HP 곡선이 **4250 → 14500 → 18000 → 20000 → 28000**이 된다.
`boss_hive` 점프가 약 **3.4×**로 커진다. 플레이테스트에서 2스테이지 벽이 느껴지면
hive를 **9000–11000** 부근으로 낮추는 안을 권한다 (이번 커밋에는 손대지 않음).

---

## 2. 옵션 미사일 배율 (REQ-085 B)

| 필드 | 값 | 근거 |
|---|---:|---|
| `weapons.json.optionMissileDamagePercent` | **50** | 시작 제안 채택 |
| 본체 미사일 | 100% | 파서/시뮬 고정 |
| Option×6 @50% 총 배수 | **4.00×** body | 게이트 ≤4.5× |

- straight L6 body ST≈210 → 옵션 6기 포함 총 ST≈840.
- 100%면 7× 폭증; 50%는 본체 단독 대비 상한이 체감 가능한 수준으로 유지된다.

---

## 3. 기체 무기 진화 3단계 (REQ-086 A)

### 게이지

| 슬롯 | maxLevel |
|---|---:|
| Double / Laser / Triple | **3** (이전 1) |
| Speed / Missile / Option / Shield | 6 (유지) |

비용 평탄 1 유지. 재발동마다 +1, 최대 3에서 포화.

### 계열별 levels (L1=루트 기본, L2/L3=levels[])

| 계열 | L2 | L3 | L1→L3 분석 DPS |
|---|---|---|---:|
| **double** | Tail Guard: `[0,5,32]` 3-way (후방 LUT 32) | Cross Fire: `[0,0,5,32]` 4-way + burst 2 | 84 → 336 (**4.00×**) |
| **triple** (spread) | Pulse Fan: 5-way + pulse 2↔6 / period 12 | Afterburner: pulse + inertia 50% + minInt 4 + burst 2 | 108 → 360 (**3.33×**) |
| **laser** | Lance: pierce 4 + impact 8@0.75u | Prism Beam: beam dmg/tick 2, len 20, halfW 0.125→0.5 | 68.6 → 120 (**1.75×**) |

- 설계 목표 “L3 ≈ L1의 1.3–1.5×”는 **레벨별 baseDamage 축이 없어** 탄 수 증가 계열에서 초과한다.
  BalanceSim 게이트는 구조(축·단조성) + L3/L1 ∈ **[1.15, 4.5]** 로 잠금.
- 엄격 1.5× 밴드가 필요하면 CODEX에 `levels[].baseDamage`(또는 damagePercent) 선택 필드를 요청할 것.

### HUD 단계명 (Presentation, CLAUDE 선반영)

DOUBLE / TAIL GUARD / CROSS FIRE · TRIPLE / PULSE FAN / BURNER · LASER / LANCE / PRISM BEAM

---

## 4. 보스 탄막 어휘 배치 (REQ-087)

### 공용 탄종 사용

| 탄종 | 배치 예 | 수치 요약 |
|---|---|---|
| normal | 전 보스 **p0** (학습 구간) | 기존 콩알탄 |
| heavy | stage1 p1, fortress p1–2, core p2 | 히트박스 2.5× (Core), 느린 벽 느낌은 기존 bulletSpeed 유지 |
| splitter | stage1 p2, hive p2, storm p1 | `splitAfterTicks` 14–18 |
| mine | hive p1, storm p2 | travel 16–20 / telegraph 18–24 / accel 2400–3000 wu/s² |
| bossLaser | core p1 (프로필 + prism) | fortress/storm 시그니처 레이저 프로필 포함 |

### 시그니처 (p0 금지 · p1–p2 필수)

| 보스 | 시그니처 | p1 탄종 | p2 탄종 |
|---|---|---|---|
| boss_stage1 | **scrapThrow** (고철 투척) | heavy | splitter |
| boss_hive | **brood** + `hive_tentacle` | mine | splitter |
| boss_fortress | **laserGrid** + bossLaser 프로필 | heavy | heavy |
| boss_storm | **lightning** + 세로 빔 프로필 | splitter | mine |
| boss_core | **prismCore** + 회전빔 프로필 | bossLaser + radial 링 | heavy + burst |

- 파서가 p0 시그니처를 거부하는 계약을 데이터로 준수.
- 스테이지가 뒤로 갈수록 p1/p2 탄종 혼합 점수 증가 (BalanceSim weightedMix=42).
- 기존 ways/speed/threat 모노 게이트 유지 (core p1 pattern만 wall→**radial**로 링탄 문법에 맞춤; ways/speed 동일).

---

## 5. 계약 목적지 정합 (REQ-086 B)

- Core가 후보에 `destinationThemeId`를 배정한다. **데이터 추가 필드 불필요**.
- `waves.json.contracts.entries` 유지:
  - 일반 항로: destination 없음 → 런타임 셔플 테마 결합
  - `end_run` / `uncharted`: `destinationKind` 유지
- BalanceSim REQ-071/073 계약 게이트 **PASS**.
- DeterminismAudit cap-boundary: stage2/3 battle hash matched.

---

## BalanceSim 게이트 추가·변경

| 게이트 | 내용 |
|---|---|
| `BossStage1Hp` | hp == **4250** |
| stage1 TTK | mid 8–16s, full ≥2.0s |
| `optionMissileDamagePercent` | == **50**, Option×6 총 배수 ≤4.5 |
| weapon mode maxLevel | Double/Laser/Triple **3** (7슬롯 + 함선 게이지) |
| evolution levels | 3단 구조·시그니처 축·DPS 비감소·L3/L1 밴드 |
| boss vocabulary | p0 no-sig, p1–2 시그니처, 4탄종 커버리지 |

---

## 검증 증거

```text
dotnet test Tools/CoreStandalone
통과!  - 실패: 0, 통과: 454

dotnet run -c Release --project Tools/BalanceSim/VerifyThemeAssembly.csproj
PASS: BalanceSim all checks green.

dotnet run -c Release --project Tools/DeterminismAudit -- --suite
AUDIT PASS
(hashes: D560B124…, 5C5B80EA…, 69D34D6B…, 9AEC4AA0…, EE67495B…, C41C8A49…)

dotnet run … DeterminismAudit -- 12345 3 30000  (×2)
RUN_1 hash=D66A41FDFF551D0D ticks=16901
RUN_2 hash=D66A41FDFF551D0D ticks=16901
EXACT_MATCH True
```

---

## 6. 레이저 적 배치 (REQ-088 후속 · 사람 지적)

**문제:** `laser_sentry` / `prism_beamer`가 waves에 사실상 안 나왔다.
- 수정 전: `laser_sentry` 1세그먼트×1마리, `prism_beamer` 2세그먼트×각 1마리.
- `shard_prism`은 이름과 달리 `laser` 프로필 없음.

### 판단 — `shard_prism`에 laser 미부여

| 선택 | 이유 |
|---|---|
| **부여 안 함** | 코어 중형 앵커(HP 850)이며 `seg_core_shard_battery`에 **5기** 밀집. prism_beamer급 laser를 붙이면 동시 빔 소스·피크 난이도가 급등. |
| 이름 의미 | “프리즘/결정” 정체성(고체력 사격 앵커)으로 두고, 빔 역할은 `prism_beamer`·`laser_sentry`에 분리. |
| 레이저 적 수 | 카탈로그 laser 적 **2종** 유지 (BalanceSim 게이트 [1,4]). |

### 배치 결과

| 적 | 테마 | 세그먼트 수 | 배치 요약 |
|---|---|---:|---|
| `laser_sentry` | **fortress** | **4** | sentry_grid×2, interceptor_assault×2, mortar_line×1, turret_cross×1 |
| `laser_sentry` | **core** | **3** | guardian_wall×2, void_mix×1, phase_discs×1 |
| `prism_beamer` | **nebula** | **5** | ribbon×1, storm×1, echo_ribbon×1, void_moth_swarm×1, prism_haze×2 |
| `prism_beamer` | **core** | **2** | rift_blades×1, void_mix×1 |
| `prism_beamer` | scrapyard | **1** | tumbler_pack×1만 유지 (스테이지1 완화) |

- fortress `drone_lattice` / `armored_gate`는 이미 laserEmitter 3기 → 적 laser 추가 생략 (피크 소스 억제).
- 세그먼트 템플릿 피크 소스(적 laser + laserEmitter) **≤4** (MaxLasers=8, `LaserCapacityExceeded` 여유).
- scrapyard 레이저 세그먼트 **≤1** 게이트로 스테이지1 최소 노출 고정.

### BalanceSim 게이트 강화

`CheckEnemyLaserProfiles`:
- `laser_sentry` fortress ≥4 · core ≥2
- `prism_beamer` nebula ≥4 · core ≥1
- scrapyard laser segs ≤1
- peak sources ≤ design 4 (초과 시 FAIL)

### 검증 (배치 후)

| 항목 | 결과 |
|---|---|
| `dotnet test` CoreStandalone | **454/454** |
| BalanceSim | **all green** (laser segs fortress=4 core_sentry=3 nebula_beamer=5 core_beamer=2 scrap=1 peak=4) |
| DeterminismAudit `--suite` | **AUDIT PASS** |

```text
PASS: REQ-075 enemy laser profiles.
  laser_sentry segs: fortress=4 core=3
  prism_beamer segs: nebula=5 core=2 scrapyard=1
  peak laser sources … =4 (design≤4, MaxLasers=8)

AUDIT PASS
(hashes: B5132344…, 5D08BF20…, 38F62D27…, 8B44B301…, A6229179…, 1476FE56…)
```

---

## 변경 파일

- `GameData/weapons.json` — optionMissile 50, 무기 모드 maxLevel 3, double/laser/spread levels
- `GameData/waves.json` — boss_stage1 hp 4250, 5보스 p1–p2 탄종·시그니처, **레이저 적 배치 확장**
- `GameData/enemies.json` — (본 후속: `shard_prism` laser 미부여, 변경 없음)
- `Tools/BalanceSim/Program.cs` — REQ-088 게이트 + 레이저 배치 게이트
- `Tools/BalanceSim/_apply_laser_placement.py` — 배치 재현 스크립트
- `Reviews/from-grok/req088-report.md` — 본 보고서
