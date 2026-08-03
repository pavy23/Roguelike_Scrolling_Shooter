# REQ-141 GROK — 잡몹 1.5배 + boss_hive 최종 페이즈 양팔 대량 발사

작성일: 2026-08-03  
브랜치/worktree: `content` / `wt-content`  
검증: `dotnet test` **568/568** (CoreStandalone)

---

## 1. 잡몹 halfWidth/halfHeight ×1.5

### 대상 선정

사람: "zako 계열, 소형 비행체" / "보스·중형 이상은 건드리지 마라".

| 포함 (13) | 근거 |
|---|---|
| `zako_straight`, `zako_sine`, `zako_fast`, `zako_sine_slow` | zako 계열 소형 |
| `spore_drifter`, `lancer_dart`, `interceptor_rush`, `wisp_spark` | 테마 소형 비행 |
| `rust_skimmer`, `sting_hornet`, `pipe_rat`, `rift_blade`, `junk_roller` | 저HP 소형 비행 |

| 제외 | 근거 |
|---|---|
| `zako_tank` | REQ-067 중형/엘리트 티어 (bombDropWeight 25) |
| turret / sentry / brood / mortar / laser / phase_disc 등 | 중형 사수 |
| elite / guardian / shard_prism | 엘리트 |
| `mini_*` | 중간보스 |
| 보스 본체·파츠 | 명시 제외 |

### 수치 (1/256 서브유닛 격자)

현재값이 이미 1/256 격자이므로 ×1.5 = 서브유닛 `×3/2` (짝수 서브유닛 보장). 전 항목 exact.

| id | before (W×H) | after (W×H) | sub (before→after) |
|---|---:|---:|---|
| zako_straight / sine | 1.0546875×0.8203125 | **1.58203125×1.23046875** | 270×210 → 405×315 |
| zako_fast | 0.9375×0.703125 | **1.40625×1.0546875** | 240×180 → 360×270 |
| zako_sine_slow | 1.171875×0.9375 | **1.7578125×1.40625** | 300×240 → 450×360 |
| spore_drifter | 1.171875×1.0546875 | **1.7578125×1.58203125** | 300×270 → 450×405 |
| lancer_dart | 0.8203125×0.5859375 | **1.23046875×0.87890625** | 210×150 → 315×225 |
| interceptor_rush | 0.9375×0.703125 | **1.40625×1.0546875** | 240×180 → 360×270 |
| wisp_spark | 0.9375×0.8203125 | **1.40625×1.23046875** | 240×210 → 360×315 |
| rust_skimmer / sting_hornet | 1.0546875×0.8203125 | **1.58203125×1.23046875** | 270×210 → 405×315 |
| pipe_rat | 0.9375×0.703125 | **1.40625×1.0546875** | 240×180 → 360×270 |
| rift_blade | 0.9375×0.5859375 | **1.40625×0.87890625** | 240×150 → 360×225 |
| junk_roller | 1.171875×0.9375 | **1.7578125×1.40625** | 300×240 → 450×360 |

뷰는 판정 반크기에 맞춰 그리므로 **데이터만 키우면 화면에서도 커진다** (HiveBossView·적 뷰 공통 규약).

### HP / 배치 보정 판단

- **HP 유지.** 대상 잡몹은 대부분 HP 6–14 (1–2발 클래스). 면적 ×2.25로 플레이어 탄 명중은 쉬워지지만 TTK 변화가 체감될 만큼의 왕복 교전이 아니다.
- **접촉 리스크**는 dive/dash (`zako_fast`, `interceptor_rush`, `lancer_dart`, `rift_blade` 등)에서 소폭 상승. contactDamage는 이미 1이라 더 못 낮추고, HP를 깎아도 접촉 판정은 그대로다. 속도·배치 변경은 §7 밸런스 사안에 가깝고 사람 원문이 크기만 지목했으므로 **이번 패스에서는 크기만**.
- 플레이테스트에서 dive 계열이 불공정하면 후속: `durationTicks`−2 또는 `speed` 소폭 하향 후보.

---

## 2. boss_hive 최종 페이즈 — 양팔 대량 발사

### 이전 (p2 @ hpThreshold 0.333)

| 축 | 값 |
|---|---|
| pattern | radial 3-way |
| fireInterval | 14t |
| bulletSpeed | 14.5 |
| projectileKind | splitter (splitAfter 14) |
| threat | 3×14.5/14 ≈ **3.11** |
| bps | ≈ **12.9**/s |
| partRules | 촉수 비활성 · 코어만 |

### 재설계

| 축 | 값 | 이유 |
|---|---|---|
| **pattern** | **spiral** | 회전 다팔 스트림 = "양팔이 움직이며 쏨" 시각 문법. radial 3-way보다 팔 수가 분명 |
| **ways** | **5** | p1=7 미만 제약(BalanceSim). 72° 간격 → 안전 지대 확보 (아래 계산) |
| **fireIntervalTicks** | **10** | 구 14t보다 촘촘. 대량 발사 체감 |
| **bulletSpeed** | **12.0** | p1=8.5 초과 제약. 구 14.5보다 약간 느려 회피 여유 |
| **projectileKind** | **mine** | **미사일 계열** — 비행→정지→예고→플레이어 가속 |
| mineTravel / telegraph / accel | **12 / 16 / 2800** | 구 p1 mine(20/24/2400)보다 빠른 사이클. telegraph 16t≈0.27s 회피창 |
| **signature** | **brood** + `hive_tentacle` | 하이브 정체성 유지. 소환 촉수가 **에너지 조준탄**(EnemyShot) 발사 → 화면 위 **미사일(mine)+에너지** 혼합 |
| signatureHomingTurn | **2** | mine에 약유도 가세 (LUT 슬롯/틱). 구 p1·p2의 1보다 강함 |
| movement | verticalSine **amp 4.25 / period 54t** | 다리 절단 후 몸통 격렬 위빙 ("팔 흔들림") |
| partRules | 촉수 off · 코어 only | 구 계약 유지 (다리 파괴 후 코어 집중) |

### 밀도·위협 비교

| | ways | int | spd | bps | threat |
|---|---:|---:|---:|---:|---:|
| p0 | 3 | 50 | 9.5 | 3.6 | 0.57 |
| p1 | 7 | 34 | 8.5 | 12.4 | 1.75 |
| **p2 구** | 3 | 14 | 14.5 | **12.9** | **3.11** |
| **p2 신** | 5 | 10 | 12.0 | **30.0** | **6.00** |

- bps **+132%**, threat **+93%** — "대량 발사"로 읽힘.
- BalanceSim mono: 0.57 < 1.75 < 6.00, p2 ways(5)<p1(7), p2 spd(12)>p1(8.5) ✓

### 회피 가능성 (안전 지대)

1. **나선 팔 간격**: 5-way → 72°. 보스 반경 r≈8 wu에서 원주 간격 ≈ `2π·8/5 ≈ 10.1 wu` ≫ 기체 hitbox(~0.75–1.5). 팔 사이 레인에 상주 가능.
2. **링 간격**: v=12 wu/s × (10/60)s = **2.0 wu** 링 간격. 세로 기동으로 링 사이 통과 가능.
3. **기뢰 예고**: mineTelegraphTicks=16 (0.27s) 정지 후 조준 가속 전 회피 창. 가속 2800 wu/s² — 즉시 히트 아님.
4. **나선 회전**: SpiralStepLutSlots=2 → 11.25°/volley, 10t 주기 → 풀 회전 ≈ 5.3s. 레인이 천천히 돌아가 따라가기 가능.

### 미사일 × 에너지 혼합 근거

스키마상 **한 페이즈 = projectileKind 1개**. 두 종류를 동시에 쓰려면 이중 발사 원(본체 + 시그니처/파츠)이 필요하다.

| 소스 | 탄 | 역할 |
|---|---|---|
| 본체 spiral 5-way | **mine** | 미사일 계열 대량 |
| brood → hive_tentacle | **normal 조준탄** | 에너지 계열 보조 |

파츠 양팔 직사( aimedSpread )는 다리가 Destroyed면 발사가 스킵되고, invulnerable 파츠도 발사가 스킵된다 (`UpdateActiveBossPartAttacks`). 다리 절단 연출을 유지하려면 코어 본체+brood 이중 원이 최선이다.

---

## 검증

```
cd Tools\CoreStandalone && dotnet test
→ 통과!  실패:0  통과:568  전체:568
```

---

## 변경 파일

- `GameData/enemies.json` — 잡몹 13종 halfExtents ×1.5
- `GameData/waves.json` — `boss_hive` phases[2] 재설계
