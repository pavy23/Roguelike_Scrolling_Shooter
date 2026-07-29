# GROK → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고, 완료 항목은 체크한다.

---

## 2026-07-29 REQ-016 scoring.json 초기값 + BalanceSim 곡선 (잠정 · §7)

**완료:** `GameData/scoring.json` 신설 + BalanceSim 그레이즈/콤보 검증.  
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지.

### scoring.json (Core 기본값 출발)

| 필드 | 값 |
|---|---:|
| grazeRadiusSubUnits | 128 |
| grazeScore | 10 |
| grazeGaugeCharge | 1 |
| multiplierGaugeRequirements | [30, 50, 80] |
| multiplierDecayTicks | 300 |

x8=16킬 / 감쇠 5s / grazeShare≈14% (60s 스케치). 상세는 `Reviews/from-claude/requests.md` REQ-016 응답.

### CLAUDE 후속

1. `Assets/Resources/GameData/scoring.json` ← `GameData/scoring.json` 동기화.
2. `BattleDirector` / `HangarScreen` 등 `GameDataParser.Parse` 호출에 scoring 6번째 인자 전달
   (미전달 시 Core 기본값과 동일하므로 당장 동작은 유지되나 데이터 원본이 무시됨).

### 검증

`dotnet test` 167/167 · `Tools/BalanceSim` PASS.

---

## 2026-07-29 REQ-014 시너지 모디파이어 보상 데이터 (잠정 · §7)

**완료:** `GameData/rewards.json` modifier 4종 + BalanceSim 조합 검증.  
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지.

### rewards.json 추가

| id | modifierId | weight | stage | maxPerRun |
|---|---|---:|---|---:|
| `mod_pierce_shot` | pierce_shot | 2 | 1–99 | 1 |
| `mod_ricochet` | ricochet | 2 | 1–99 | 1 |
| `mod_homing_missile` | homing_missile | 2 | 1–99 | 1 |
| `mod_kill_explosion` | kill_explosion | 2 | 1–99 | 1 |

카탈로그 9 → **13**. stage1 E[mods in 3]≈**1.20**, stage2+ ≈**1.04**.

### BalanceSim 조합 (pierce + kill_explosion)

밀집 HP1 팩 기준 clear-speed: none 1× / pierce 1.81× / kill_explosion 3.15× / combo **4.12×**.  
콤보 vs 최강 단독 ×1.31. baseline ≥4× soft WARN — 폭발 기본 파라미터 튜닝 후보
(`KillExplosionDamage`/`Radius`, Core config). 상세는 `Reviews/from-claude/requests.md` REQ-014 응답.

### 테스트 동기화

`GameDataParserTests` Rewards.All.Count **9 → 13**.

### CLAUDE 후속

1. `Assets/Resources/GameData/rewards.json` 동기화.
2. 보상 UI 라벨: pierce / ricochet / homing / kill-explosion 표시명.

### CODEX/사람 후속 (선택)

밀집 웨이브에서 처치폭발 단독이 강한 경우 Core 기본 `KillExplosionDamage=2`·radius 2u 하향
또는 GameData 이관 스키마 검토. 현 데이터 패스는 보상 풀만 소유.

---

## 2026-07-29 일반 적 4종 로스터 최종 완성 26→30 (잠정 · roster-30)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` + `GameDataParserTests` 개수 동기. 스키마 변경 없음. `mini_` 접두 미사용.

### 테마 분포 점검 (before → after, 시그니처 비미니 기준)

| 테마 | before non-mini | after non-mini | 보강 |
|---|---:|---:|---|
| scrapyard | 3 | **4** | `pipe_rat` |
| hive | 3 | **4** | `sting_hornet` |
| fortress | 3 | 3 | — |
| nebula | 3 | 3 | — |
| core | **2** (최박) | **4** | `phase_disc`, `rift_blade` |

우선순위: core(시그니처 최박)×2 · hive(테마 풀 unique 최박)×1 · scrapyard×1.

### enemies.json — 신규 4종

동일 HP 교체로 stage 1–5 avgHP 곡선 **137→186→279→408→486** 유지.

| id | 테마 | movePattern | hp | moveSpeed | fireInterval | dropWeight | hitbox (half) | 의도 |
|---|---|---|---|---|---|---|---|---|
| `sting_hornet` | hive | sine | **8** | **6.75** | 0 | 3 | 0.75×0.5625 | 독침 호넷. 고속 사인 (amp 2.0 / 70t). |
| `pipe_rat` | scrapyard | straight | **10** | **7.0** | 0 | 3 | 0.5625×0.46875 | 배관 쥐. 고속 직선 잡졸. |
| `phase_disc` | core | static | **22** | 0 | **68** | 4 | 0.75×0.75 | 위상 원반. 코어 정지 사격 (sentry 계열). |
| `rift_blade` | core | straight | **4** | **11.0** | 0 | 2 | 0.75×0.46875 | 균열 칼날. 초고속 직선 돌파. |

### waves.json — 동일 HP 교체 (신설 세그먼트 없음)

| 세그먼트 | 교체 |
|---|---|
| `seg_intro_line` / `seg_sine_rush` | rust_skimmer → pipe_rat (부분) |
| `seg_hive_spore_cloud` / `seg_hive_lancer_rush` | spore_drifter → sting_hornet (부분) |
| `seg_core_guardian_wall` / `seg_core_final_gauntlet` | sentry→phase_disc, interceptor→rift_blade (부분) |

### 테스트 동기화

`GameDataParserTests` Enemies **26 → 30**. Segments/Bosses 불변(16/5).

### CLAUDE 후속

1. `Assets/Resources/GameData/enemies.json` · `waves.json` 동기화.
2. 뷰 스프라이트: 접두 `sting_` / `pipe_` / `phase_` / `rift_` 매핑.

---

## 2026-07-29 일반 적 4종 로스터 증원 22→26 (잠정 · roster-30 목표)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` · `rewards.json`(REQ-012) + `GameDataParserTests` 개수 동기. 스키마 변경 없음. `mini_` 접두 미사용.

### enemies.json — 신규 4종 (부족 테마: scrapyard×2 / nebula×1 / core×1)

동일 HP 교체로 stage 1–5 avgHP 곡선 **137→186→279→408→486** 유지.

| id | 테마 | movePattern | hp | moveSpeed | fireInterval | dropWeight | hitbox (half) | 의도 |
|---|---|---|---|---|---|---|---|---|
| `rust_skimmer` | scrapyard | straight | **10** | **6.25** | 0 | 3 | 0.75×0.5625 | 녹슨 스킴머. 중속 직선 돌파. |
| `junk_roller` | scrapyard | sine | **10** | **3.5** | 0 | 4 | 0.75×0.75 | 고철 롤러. 느린 사인 구르기 (amp 2.25 / 130t). |
| `void_moth` | nebula | sine | **16** | **4.75** | **95** | 5 | 0.75×0.75 | 보이드 나방. 성운 사인·약사격 (amp 3.0 / 75t). |
| `shard_prism` | core | straight | **60** | **1.5** | **75** | 10 | 0.9375×0.9375 | 코어 프리즘. 저속 고체력 사격 앵커. contact 2. |

### waves.json — 동일 HP 교체 (신설 세그먼트 없음, themes/diff band 불변)

| 세그먼트 | 교체 |
|---|---|
| `seg_intro_line` / `seg_sine_rush` | zako_straight/sine → rust_skimmer / junk_roller |
| `seg_sine_pair` | zako_sine → junk_roller |
| `seg_nebula_wisp_storm` / `ribbon` | echo_wisp → void_moth (부분) |
| `seg_core_guardian_wall` / `final_gauntlet` | guardian_sphere → shard_prism (부분) |

### REQ-012 — rewards.json maxPerRun

`passive_fire_rate_1` / `passive_damage_1` / `passive_move_speed_1`에 **maxPerRun: 3** (잠정).  
현 파서는 미인식 필드 무시 → 테스트 그린. CODEX 파서·RunManager 연동 대기.

### 테스트 동기화

`GameDataParserTests` Enemies **22 → 26**. Segments/Bosses 불변(16/5).

### CLAUDE 후속

1. `Assets/Resources/GameData/enemies.json` · `waves.json` · `rewards.json` 동기화.
2. 뷰 스프라이트: 접두 `rust_` / `junk_` / `void_` / `shard_` 매핑.

---

## 2026-07-29 미니보스급 중형 4종 로스터 증원 (잠정 · 7장 표기)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` + `GameDataParserTests` 개수 동기. 스키마 변경 없음.

### enemies.json — 신규 4종 (id 접두 `mini_`, 뷰 스프라이트 매핑용)

히트박스 공통: 64×48px @ PPU16 → **halfWidth 2.0 / halfHeight 1.5**. scoreValue **800–1500** 미니보스급.

| id | 계열 | movePattern | hp | moveSpeed | fireInterval | score | dropWeight | 의도 |
|---|---|---|---|---|---|---|---|---|
| `mini_destroyer` | 요새/스크랩 | straight | **200** | **1.5** (저속) | **55** | 1200 | 14 | 저속 직선 사격형. 중형 앵커. |
| `mini_horror` | 하이브 | sine | **180** | 2.5 | 70 | 1100 | 14 | **대진폭** sine (amp **4.5**, period 120t). 화면 점유. |
| `mini_walker` | 요새/코어 | static | **250** | 0 | **48** | 1500 | 15 | 정지 사격형. 최고 HP·점수. 터렛(90t)보다 촘촘. |
| `mini_crystal` | 성운 | sine | **160** | **4.5** (고속) | **40** | 1000 | 13 | 사인 고속 사격. period 90t, amp 3.25. |

- `contactDamage: 2` (엘리트·탱커와 동일 위험 신호).
- amp 4.5 @ y=0 → 피크 ±4.5 < halfH 11.25 − halfHeight 1.5 = 9.75 (이탈 없음).

### waves.json — 테마별 기존 세그먼트 후반 스폰 1기씩 (신설 세그먼트 없음)

| 세그먼트 | theme | difficulty | tick | enemyId | y |
|---|---|---|---|---|---|
| `seg_fortress_sentry_grid` | fortress | 3–5 | **850** / length 900 | `mini_destroyer` | 0 |
| `seg_hive_spore_cloud` | hive | 2–5 | **680** / length 720 | `mini_horror` | 0 |
| `seg_nebula_wisp_storm` | nebula | 3–5 | **740** / length 780 | `mini_crystal` | 0 |
| `seg_core_guardian_wall` | core | 4–5 | **860** / length 900 | `mini_walker` | 0 |

- 스크랩(scrapyard) 전용 세그먼트는 없음 → destroyer는 fortress 세그먼트에 배치 (요새/스크랩 계열).
- 후반 틱 단독 스폰으로 잡졸 밀도와 겹치지 않게 미니보스 피날레 연출.

### 이론 검산 (잠정)

메인만 풀히트 DPS≈75 가정:

| id | TTK | 비고 |
|---|---|---|
| mini_crystal 160 | ≈2.1s | 고속 사인·고연사로 회피 부담이 본 DPS 교환 |
| mini_horror 180 | ≈2.4s | 대진폭 회피 동선 |
| mini_destroyer 200 | ≈2.7s | 저속 사격 앵커 |
| mini_walker 250 | ≈3.3s | 정지 고화력. fire 48t ≈ 1.25 볼리/초 |

elite_sine(hp50, score600) 대비 체력 3–5×·점수 1.7–2.5×. 세그먼트당 1기라 스테이지 총 기대 시간은 소폭 증가.

### 테스트 동기화 (CODEX 소유 파일 최소 수정)

`Assets/Tests/EditMode/GameDataParserTests.cs`  
`RepositoryApprovedV2Files_ParseCompletely` — Enemies **14 → 18**. Segments/Bosses 불변(16/5).

### 검증

- `cd Tools/CoreStandalone && dotnet test`
- `cd Tools/BalanceSim && dotnet run` — stage×difficulty 50조합 조립

### CLAUDE 후속

1. `Assets/Resources/GameData/enemies.json` · `waves.json` 동기화.
2. 뷰 스프라이트: id 접두 `mini_` 4종 (64×48 권장) 매핑.

---

## [x] REQ-G005 → CODEX 소유 파일 수정 기록: `GameDataParserTests` 카탈로그 개수 (미니보스 4종)

**무엇이 / 왜**

미니보스급 중형 4종 로스터 증원에 따라 저장소 `GameData/enemies.json` 카탈로그 개수가 늘어났다.
`Assets/Tests/EditMode/GameDataParserTests.cs`의 `RepositoryApprovedV2Files_ParseCompletely`가
고정 개수로 검증하므로 **CODEX 소유 파일**을 함께 갱신했다 (콘텐츠 커밋이 테스트 그린을 유지하려면 불가피).

| 항목 | before | after |
|---|---|---|
| Enemies | 14 | **18** (`mini_destroyer`, `mini_horror`, `mini_walker`, `mini_crystal`) |
| Segments | 16 | **16** (기존 테마 세그먼트 후반 스폰만 추가) |
| Bosses | 5 | **5** (불변) |

**변경 파일:** `Assets/Tests/EditMode/GameDataParserTests.cs` — Assert 개수만 갱신. 스키마/파서 API 변경 없음.

**CODEX 후속 (선택):** sim 브랜치 머지 시 동일 Assert가 이미 content 쪽 값이면 no-op.

---

## 2026-07-29 rewards.json 런 지속 패시브 3종 (M3 시너지 · 잠정)

**완료:** `GameData/rewards.json`에 패시브 보상 3종 추가 + 기존 6종 weight 상향.  
**상태:** 손맛·분포 **잠정 제안** — 최종 확정은 사람 결정 (AGENTS.md §7).  
**출처 요청:** `Reviews/from-codex/requests.md` — 런 지속 패시브 3종 데이터 추가.

### 카탈로그 변경

| id | type | amount | weight | stageIndexMin–Max | 효과 (Core 계약) |
|---|---|---|---|---|---|
| `passive_fire_rate_1` | `fireRateUp` | 1 | **1** | **2**–99 | 기본탄 `fireIntervalTicks −1` (하한 `MainShotMinimumFireIntervalTicks`, 기본 4) |
| `passive_damage_1` | `damageUp` | 1 | **1** | **2**–99 | 기본탄 `baseDamage +2` |
| `passive_move_speed_1` | `moveSpeedUp` | 1 | **1** | **2**–99 | 플레이어 이동 `+1 u/s` |
| 기존 6종 (capsules / 4×slotLevel / repairHp) | (유지) | (유지) | **1 → 2** | 1–99 | 변경 없음 (weight만) |

- `schemaVersion: 1`, `optionCount: 3` 유지. 패시브 항목에 `slot` 필드 없음 (`slotLevel` 전용).
- 런 중 중첩, `Restart` 시 Core가 초기값 복원 (사망 승계 없음).

### weight · stage 선정 근거

**목표:** 시너지 빌드의 핵심 축이지만, 패시브가 너무 자주 나오면 **슬롯 육성(메인/미사일/옵션/실드)** 이 죽는다.

| 구간 | 후보 수 | weight 합 | 비패시브 : 패시브 |
|---|---|---|---|
| stage **1** | 6 (기존만) | 12 | 12 : 0 — 기본 육성 전용 |
| stage **2+** | 9 | 12 + 3 = **15** | **12 : 3 = 4 : 1** |

- 사람 제안 “약 2:1”보다 **보수적(4:1)**. 기존 weight 상향(1→2) + 패시브 weight 1로 슬롯·유틸 풀 질량을 지킴.
- 슬롯 4종만 보면 8 : 3 ≈ **2.7 : 1** (슬롯 육성 우위 유지).
- 1픽 기준 stage 2+: P(아무 패시브) = 3/15 = **20%**, P(특정 슬롯) = 2/15 ≈ **13.3%**.
- `stageIndexMin: 2` — stage 1 클리어 보상은 캡슐/슬롯/repair만. 초반 게이지·슬롯 기반을 깔고 나서 시너지 축을 연다.

**채택하지 않은 대안**

| 대안 | 기각 이유 |
|---|---|
| 기존 weight 1 유지 + 패시브 weight 1 (6:3=2:1) | 사람 하한에 맞지만 stage 2에서 패시브 1/3 질량 → 슬롯 선택이 잦아 빌드 편중 우려. |
| 패시브 stageIndexMin 1 | stage 1부터 시너지 축이 슬롯과 경쟁 → 기본 육성 우선 원칙 위배. |
| amount > 1 (예: damage +4) | 1스택 체감이 과도. amount 1로 중첩 곡선을 플레이 관측 후 조정. |

### 이론 효과 · 중첩 곡선 (헤드리스 수치 검산)

기준: `weapons.json` main_shot `baseDamage: 10`, `fireIntervalTicks: 8`; Core 기본 `MainShotMinimumFireIntervalTicks: 4`, 플레이어 속도 **13 u/s**.  
DPS = `baseDamage × (60 / interval)` (레벨 0/1 동일 base, 풀히트 가정). 보스 TTK는 현 `boss_stage1` hp 곡선 참고용.

#### fireRateUp (amount 1)

| 스택 | interval | RoF (발/초) | 대비 base |
|---|---|---|---|
| 0 | 8 | 7.5 | — |
| 1 | 7 | ≈8.57 | **+14%** |
| 2 | 6 | 10 | +33% |
| 3 | 5 | 12 | +60% |
| 4+ | **4 (clamp)** | 15 | +100% |

- 유효 상한 4스택. 그 이상은 하한에 막혀 보상 낭비 가능 → 후속 “이미 최소면 풀에서 제외” 로직은 CODEX 검토 여지.
- MainShot 게이지 rapid-fire 감소와 **가산**되면 더 빨리 하한에 도달. 슬롯 육성과 시너지이자 중복 주의 포인트.

#### damageUp (amount 1, +2 base/스택)

| 스택 | base dmg | DPS @ interval 8 | stage1 boss TTK 추정 (hp 1000 가정, 메인만) |
|---|---|---|---|
| 0 | 10 | 75 | ≈13.3s |
| 1 | 12 | 90 (**+20%**) | ≈11.1s |
| 2 | 14 | 105 | ≈9.5s |
| 3 | 16 | 120 | ≈8.3s |

- 1스택 +20%는 슬롯 MainShot 레벨 1회(+50% of base via `Damage.Compute`)보다 약하지만 **레벨과 곱해져** 시너지 (base 12 × L2 = 18 vs base 10 × L2 = 15).
- 3스택(+60% base)도 단독으로는 보스 즉사 수준이 아님. fireRateUp과 동시 적중 시 체감 폭주 가능 → weight 희귀도가 1차 안전장치.

#### moveSpeedUp (amount 1, +1 u/s)

| 스택 | 속도 u/s | 대비 base 13 |
|---|---|---|
| 0 | 13.0 | — |
| 1 | 14.0 | **+7.7%** |
| 2 | 15.0 | +15% |
| 3 | 16.0 | +23% (Interceptor 1.25× ≈ 16.25에 근접) |

- 회피·레인 전환 마진 확대. DPS 직접 영향 없음.
- 함선 배율과 합성되므로 Interceptor+다스택은 과속이 될 수 있음 — 관측 후 weight 또는 amount 재검토.

#### 복합 시너지 (과하지 않은가?)

| 빌드 | 대략 DPS | 비고 |
|---|---|---|
| base only | 75 | 기준 |
| dmg×1 + rate×1 | 12 × (60/7) ≈ **103** (+37%) | stage 2–3 합리적 시너지 |
| dmg×2 + rate×2 | 14 × 10 = **140** (+87%) | 다스테이지 투자, 슬롯 기회비용 큼 |
| dmg×3 + rate×4 (하한) | 16 × 15 = **240** (+220%) | 이론 상한. 실제 3택·weight 4:1·슬롯 경쟁으로 도달 빈도 낮음 |

**결론 (잠정):** amount 1 + stage≥2 + weight 1(기존 2) 조합은 1–2스택 시너지를 허용하면서 슬롯 풀을 죽이지 않는다. 최종 손맛·weight 미세조정은 플레이 피드백 후 사람 확정.

### 테스트 동기화 (CODEX 소유 파일 최소 수정)

`Assets/Tests/EditMode/GameDataParserTests.cs`  
`RepositoryApprovedV2Files_ParseCompletely` — `Rewards.All.Count` **6 → 9**.  
(콘텐츠 카탈로그 확장에 따른 고정 개수 Assert. 이전 REQ-G004와 동일 패턴.)

### 검증

- `cd Tools/CoreStandalone && dotnet test` — PASS 목표.
- Core 패시브 단위 테스트(`PassiveRewardTests`)는 인라인 카탈로그 사용 — JSON 변경과 독립.

### CLAUDE 후속

1. `Assets/Resources/GameData/rewards.json` 동기화 (Resources 복사 파이프).
2. 보상 UI 라벨: `fireRateUp` / `damageUp` / `moveSpeedUp` 표시명 (연사 강화 / 화력 강화 / 엔진 출력).

---

## 2026-07-29 ships.json 함선 카탈로그 (잠정 · AGENTS.md §7)

**완료:** `GameData/ships.json` schemaVersion **1** 신설. 함선 3종.  
**상태:** 손맛·경제 **잠정 제안** — 최종 확정은 사람 결정.

### 카탈로그

| id | displayName | moveSpeed | 유효 속도 (base 13.0) | startingPowerUpLevels | unlockCost |
|---|---|---|---|---|---|
| `starter` | Starter | **1/1** (1.0×) | 13.0 | `[0,0,0,0]` | **0** |
| `interceptor` | Interceptor | **5/4** (1.25×) | 16.25 | `[0,0,0,0]` | **25000** |
| `bulwark` | Bulwark | **4/5** (0.8×) | 10.4 | `[0,0,0,1]` | **50000** |

- 슬롯 순서: MainShot / Missile / Option / **Shield**.
- 소스 첫 비용 0 함선 = `starter` → `DefaultShip`.
- 유리수 배율만 사용 (소수 배율 금지, Core 약분 합성).

### 역할 의도

| 함선 | 역할 | 트레이드오프 |
|---|---|---|
| Starter | 무료 중립 기준선 | 튜닝 없음. 신규 플레이어·폴백 비교 기준. |
| Interceptor | 스피드형 | 회피·레인 전환 유리(+25% 이동). **시작 파워업 없음**으로 DPS/방어 초반 불리 → 숙련 보상. |
| Bulwark | 중장형 | 이동 −20%로 회피 부담↑. **Shield 1** 시작으로 접촉 1회 버퍼 → 초보·고밀도 구간 안정. |

Shield 시작 1은 `weapons.json` shield `maxLevel: 3` 이내. 사망 후 재시작 시에도 Core가 함선 시작 레벨 하한을 유지하므로 Bulwark는 메타 사망 페널티와 맞물려 “최소 실드 1” 정체성을 유지한다.

### 점수 경제 근거 (unlockCost)

**소스 수치 (현 카탈로그)**

| 구간 | 값 | 출처 |
|---|---|---|
| 잡졸 scoreValue | 60–600 (대표 100–400) | `enemies.json` |
| 보스 점수 | `hp × 2` (Core) | stage1 **2000** … core **4800** |
| 1런 추정 | **1만–3만** | 초반 사망 ~1만 / stage1–2 클리어+보스 ~1.5–2.5만 / 다스테이지 강런 ~3만+ |

**해금 목표:** 2–4런 안에 **한 척**(저가 우선) 해금.

| 함선 | cost | 약 10k/런 | 약 15–20k/런 | 약 30k/런 |
|---|---|---|---|---|
| Interceptor | 25000 | 3런 | **2런** | 1런 |
| Bulwark | 50000 | 5런 | **3런** | 2런 |

- Interceptor **25000**: 평균 런(1.5만) 기준 약 2런, 약한 런(1만) 기준 3런 → 목표 2–4런 창에 맞춤. 저가 첫 해금으로 메타 진행 감각을 먼저 준다.
- Bulwark **50000**: Interceptor의 2배. 평균 3런·강런 2런. “다음 목표”로 남기고, 한 런에 둘 다 사는 폭주를 막음.
- 재화 = `MetaState.CreditScore(run.TotalScore)` 누적 점수. 런 실패해도 점수 적립되면 사망 런도 해금에 기여(Presentation 적립 1회 보장 전제).

**채택하지 않은 대안**

- Core 테스트 예시 `swift` 1000점: 1런 내 즉시 해금 → 메타 동기 부족.
- 고가 10만+: 약한 런 10회+ → 해금이 멀어 격납고 의미가 약해짐.
- Interceptor에 시작 Main/Option: 요청 스펙 “시작 파워업 없음”과 충돌. 속도만으로 차별.

### 검증

- `cd Tools/CoreStandalone && dotnet test` — PASS (RepositoryApproved는 ships 미로드 폴백 경로; 신규 JSON은 schema v1 파서로 유효).
- 배율·레벨·비용 최종 손맛은 플레이 피드백 후 사람 확정.

### CLAUDE 후속

1. Resources 복사: `GameData/ships.json` → `Assets/Resources/GameData/`.
2. 격납고 UI·`MetaState` 저장/선택·`RunManager` ship 주입 (`Reviews/from-codex` CLAUDE 항목).

---

## 2026-07-29 waves.json theme 태깅 (CODEX 스키마 후속)

**완료:** `GameData/waves.json` — 테마 전용 세그먼트 8 + 보스 5에 `theme` 부여. 범용 8은 null.
**조정:** 테마 순환 정합을 위해 `boss_hive`/`fortress`/`storm`/`core`의 `stageIndexMin` 전부 **1**.
**검증:** BalanceSim 50/50 + CoreStandalone 115/115.

상세 표·순환 순서·조정 이유는 `Reviews/from-codex/requests.md` GROK 응답 참고.

### CLAUDE 후속

1. `Assets/Resources/GameData/waves.json` ← `GameData/waves.json` 동기화.
2. `StagePlan.ThemeId`로 배경 선택 (CODEX 요청 항목).

### 밸런스 시뮬 도구

`Tools/BalanceSim/` — 헤드리스 stage×difficulty 조립 검증 (`dotnet run`).

---

## [x] REQ-G004 → CODEX 소유 파일 수정 기록: `GameDataParserTests` 카탈로그 개수 (M3 테마4·5)

**무엇이 / 왜**

M3 테마4(성운·전자폭풍) + 테마5(최종 요새 코어) 콘텐츠 추가에 따라 저장소 `GameData/` 카탈로그 개수가 늘어났다.
`Assets/Tests/EditMode/GameDataParserTests.cs`의 `RepositoryApprovedV2Files_ParseCompletely`가
고정 개수로 검증하므로 **CODEX 소유 파일**을 함께 갱신했다 (콘텐츠 커밋이 테스트 그린을 유지하려면 불가피).

| 항목 | before | after |
|---|---|---|
| Enemies | 12 | **14** (`wisp_spark`, `guardian_sphere`) |
| Segments | 12 | **16** (`seg_nebula_wisp_storm`, `seg_nebula_wisp_ribbon`, `seg_core_guardian_wall`, `seg_core_final_gauntlet`) |
| Bosses | 3 | **5** (`boss_storm`, `boss_core`) |

**변경 파일:** `Assets/Tests/EditMode/GameDataParserTests.cs` — Assert 개수만 갱신. 스키마/파서 API 변경 없음.
**비포함:** `theme` 필드 태깅 — CODEX 스키마 작업 중. 다음 패스에서 태깅.

**CODEX 후속 (선택):** sim 브랜치 머지 시 동일 Assert가 이미 content 쪽 값이면 no-op.

---

## 2026-07-29 M3 테마4·5 성운·전자폭풍 + 최종 요새 코어 (잠정)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` + 테스트 개수 동기. 스키마 변경 없음. `theme` 필드 미포함.

### enemies.json — 신규 2종 (뷰 스프라이트 매핑: `wisp_` / `guardian_` 접두)

| id | movePattern | hp | moveSpeed | fireInterval | dropWeight | hitbox (half) | 의도 |
|---|---|---|---|---|---|---|---|
| `wisp_spark` | sine | 5 | **6.5** | 0 | 3 | 0.75×0.75 | 전기 위습. HP 낮음, 빠른 사인(period **60t**, amp 3.5). 성운 밀도 담당. |
| `guardian_sphere` | straight | **60** | **1.75** | **70** | 10 | 0.9375×0.9375 | 고체력 저속 방어구체. 사격형 앵커. contact 2. |

### waves.json — 성운 세그먼트 2 + 코어 세그먼트 2 + 보스 2

| 세그먼트 | diff | lengthTicks | traversable | 밀도 | 의도 |
|---|---|---|---|---|---|
| `seg_nebula_wisp_storm` | 3–5 | 780 | `[7]` | 고 (28) | 위습 중심 + sine/slow/elite 혼합. 전 레인 개방. |
| `seg_nebula_wisp_ribbon` | 3–5 | 720 | `[2]` | 고 (25) | 위습 리본 연속 + sine 혼합. center 코리도. |
| `seg_core_guardian_wall` | **4–5** | 900 | `[6]` | 최고 (33) | guardian+터렛+interceptor+sentry. top\|center. |
| `seg_core_final_gauntlet` | **4–5** | 840 | `[2]` | 최고 (39) | guardian+터렛+interceptor 최고 밀도 가틀릿. center. |

**boss_storm:** stageIndex **4–99**, hp **1900**, halfW/H 4.0/3.0, holdX 14.0.  
페이즈: `{40t, 5-way, 11.0}` / `{36t, 7-way, 11.5}` — fortress(42/5/10 · 38/6/11)보다 강하되 interval **36t**.

**boss_core:** stageIndex **5–99**, hp **2400**, halfW/H 4.0/3.0, holdX 14.0.  
페이즈: `{38t, 7-way, 12.0}` / `{34t, 9-way, 12.5}` — 최종보스감. interval **34t 하한** 유지.

### 이론 검산 (잠정)

- 보스 TTK (메인만, 풀히트, DPS≈75): storm 1900/75 ≈ **25.3s**, core 2400/75 ≈ **32.0s** (fortress 1600 ≈ 21.3s 대비 상향).
- storm phase2 밀도: 7발/36t ≈ 11.7발/초. core phase2: 9발/34t ≈ 15.9발/초.
- 위습 period 60t + speed 6.5 → 회피 부담↑, HP 5로 교환 가능.

### 후속 관찰

1. Resources 복사본(`Assets/Resources/GameData/`) 동기화 — CLAUDE 빌드/씬 재생성 파이프.
2. 뷰 스프라이트: id 접두 `wisp_` / `guardian_` / `boss_storm` / `boss_core` 매핑 (CLAUDE).
3. `theme` 필드 태깅 — CODEX 스키마 완료 후 다음 패스.
4. stage 4+/5+ 보스 로테이션: 다수 보스 동시 적격 → RNG 선택. 고정 배정이 필요하면 stageIndex 구간 분리.

---

## [x] REQ-G003 → CODEX 소유 파일 수정 기록: `GameDataParserTests` 카탈로그 개수 (M3 테마3)

**무엇이 / 왜**

M3 테마3(기계 요새) 콘텐츠 추가에 따라 저장소 `GameData/` 카탈로그 개수가 늘어났다.
`Assets/Tests/EditMode/GameDataParserTests.cs`의 `RepositoryApprovedV2Files_ParseCompletely`가
고정 개수로 검증하므로 **CODEX 소유 파일**을 함께 갱신했다 (콘텐츠 커밋이 테스트 그린을 유지하려면 불가피).

| 항목 | before | after |
|---|---|---|
| Enemies | 10 | **12** (`sentry_drone`, `interceptor_rush`) |
| Segments | 10 | **12** (`seg_fortress_sentry_grid`, `seg_fortress_interceptor_assault`) |
| Bosses | 2 | **3** (`boss_fortress`) |

**변경 파일:** `Assets/Tests/EditMode/GameDataParserTests.cs` — Assert 개수만 갱신. 스키마/파서 API 변경 없음.

**CODEX 후속 (선택):** sim 브랜치 머지 시 동일 Assert가 이미 content 쪽 값이면 no-op.

---

## 2026-07-29 M3 테마3 기계 요새 (잠정)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` + 테스트 개수 동기. 스키마 변경 없음.

### enemies.json — 신규 2종 (뷰 스프라이트 매핑: `sentry_` / `interceptor_` 접두, 히트박스 24px → half 0.75)

| id | movePattern | hp | moveSpeed | fireInterval | dropWeight | hitbox (half) | 의도 |
|---|---|---|---|---|---|---|---|
| `sentry_drone` | static | 22 | 0 | **75** | 3 | 0.75×0.75 | 정지 방어 드론. 터렛(90t)보다 촘촘한 사격으로 탄막 밀도 담당. |
| `interceptor_rush` | straight | 4 | **10.5** | 0 | 2 | 0.75×0.75 | 고속 직선 요격기. HP 최저급, 스웜·러시 밀도. |

### waves.json — 요새 세그먼트 2 + 보스 1

| 세그먼트 | diff | lengthTicks | traversable | 밀도 | 의도 |
|---|---|---|---|---|---|
| `seg_fortress_sentry_grid` | 3–5 | 900 | `[6]` | 고 (25) | 센트리 격자 + 터렛 혼합 + 인터셉터 돌파. top\|center 코리도. 사격형 위주. |
| `seg_fortress_interceptor_assault` | 3–5 | 780 | `[2]` | 고 (32) | 인터셉터 연속 러시 + 센트리 앵커 + 상하 터렛. center 코리도. |

**boss_fortress:** stageIndex **3–99**, hp **1600**, halfW/H 4.0/3.0, holdX 14.0.  
페이즈: `{42t, 5-way, 10.0}` / `{38t, 6-way, 11.0}` — hive(48/4/9.5 · 40/5/10.5)보다 강하되 interval **38t 하한** 유지.

### 이론 검산 (잠정)

- 보스 TTK (메인만, 풀히트, DPS≈75): 1600/75 ≈ **21.3s** (hive 1300 ≈ 17.3s, stage1 1000 ≈ 13.3s 대비 상향).
- phase2 밀도: 6발/38t ≈ 9.5발/초 (hive phase2 ≈ 7.5발/초). ways↑·interval↓로 중후반 위협.
- 센트리 fire 75t ≈ 0.8 볼리/초/기. 격자 2기 동시 스폰 시 로컬 탄막 밀도 확보.

### 후속 관찰

1. Resources 복사본(`Assets/Resources/GameData/`) 동기화 — CLAUDE 빌드/씬 재생성 파이프.
2. 뷰 스프라이트: id 접두 `sentry_` / `interceptor_` / `boss_fortress` 매핑 (CLAUDE).
3. stage 3+ 보스 로테이션: stage1·hive·fortress 동시 적격 구간 겹침 → RNG 선택. 고정 배정이 필요하면 stageIndex 구간 분리.

---

## [x] REQ-G002 → CODEX 소유 파일 수정 기록: `GameDataParserTests` 카탈로그 개수 (M3 테마2)

**무엇이 / 왜**

M3 테마2(바이오 하이브) 콘텐츠 추가에 따라 저장소 `GameData/` 카탈로그 개수가 늘어났다.
`Assets/Tests/EditMode/GameDataParserTests.cs`의 `RepositoryApprovedV2Files_ParseCompletely`가
고정 개수로 검증하므로 **CODEX 소유 파일**을 함께 갱신했다 (콘텐츠 커밋이 테스트 그린을 유지하려면 불가피).

| 항목 | before | after |
|---|---|---|
| Enemies | 8 | **10** (`spore_drifter`, `lancer_dart`) |
| Segments | 8 | **10** (`seg_hive_spore_cloud`, `seg_hive_lancer_rush`) |
| Bosses | 1 | **2** (`boss_hive`) |

**변경 파일:** `Assets/Tests/EditMode/GameDataParserTests.cs` — Assert 개수만 갱신. 스키마/파서 API 변경 없음.

**CODEX 후속 (선택):** sim 브랜치 머지 시 동일 Assert가 이미 content 쪽 값이면 no-op. 개수 하드코딩 대신
카탈로그 무결성만 검증하도록 완화할지는 CODEX 재량.

---

## 2026-07-29 M3 테마2 바이오 하이브 (잠정)

**승인 맥락:** 오케스트레이터 잠정 승인. AGENTS.md §7 최종 확정은 사람 검토 후 유지.  
**범위:** `GameData/enemies.json` · `waves.json` + 테스트 개수 동기. 스키마 변경 없음.

### enemies.json — 신규 2종 (뷰 스프라이트 매핑: `spore_` / `lancer_` 접두)

| id | movePattern | hp | moveSpeed | dropWeight | hitbox (half) | 의도 |
|---|---|---|---|---|---|---|
| `spore_drifter` | sine | 8 | 2.5 | 5 | 0.75×0.75 | 저속 사인 포자. 화면 점유·회피 부담. 드롭 보통. |
| `lancer_dart` | straight | 4 | 9.5 | 2 | 0.75×0.75 | 직선 고속 랜서. HP 최저, contact 1. 스웜형 저드롭. |

### waves.json — 하이브 세그먼트 2 + 보스 1

| 세그먼트 | diff | lengthTicks | traversable | 밀도 | 의도 |
|---|---|---|---|---|---|
| `seg_hive_spore_cloud` | 2–5 | 720 | `[7]` | 중–고 (19) | 포자 구름 중심 + sine/straight 혼합. 전 레인 개방. |
| `seg_hive_lancer_rush` | 2–5 | 660 | `[2]` | 고 (24) | 랜서 연속 돌진 + 포자·fast·sine 혼합. center 코리도. |

**boss_hive:** stageIndex 2–99, hp **1300**, halfW/H 4.0/3.0, holdX 14.0.  
페이즈: `{48t, 4-way, 9.5}` / `{40t, 5-way, 10.5}` — stage1(55/3/9 · 45/5/10)보다 촘촘하되 interval 40t 하한으로 즉사 압박 금지.

### 이론 검산 (잠정)

- 보스 TTK (메인만, 풀히트, DPS≈75): 1300/75 ≈ **17.3s** (stage1 1000 ≈ 13.3s 대비 상향).
- phase1 밀도: 5발/40t ≈ 7.5발/초 (stage1 phase2 ≈ 6.7발/초). ways 동일 5, interval만 소폭 단축.
- 포자 기대 드롭: dropWeight 5 → 5/(8+5)≈38%/킬. 세그먼트당 다수지만 개체 HP 낮아 교환 가능.

### 후속 관찰

1. Resources 복사본(`Assets/Resources/GameData/`) 동기화 — CLAUDE 빌드/씬 재생성 파이프.
2. 뷰 스프라이트: id 접두 `spore_` / `lancer_` / `boss_hive` 매핑 (CLAUDE).
3. stage 2+ 보스 로테이션: `boss_stage1`과 `boss_hive` 동시 적격 → RNG 선택. 고정 배정이 필요하면 stageIndex 구간 분리.

---

## [x] REQ-G001 → CODEX: `rewards.json` 파서 + RunManager 풀 교체 (REQ-008 후속)

**무엇이 필요한가**

`GameData/rewards.json`(schemaVersion 1)을 Core가 읽어 `RunManager.GenerateRewardOptions`의 내장 잠정 풀을 대체할 것.

**스키마 (GROK 확정 초안, 2026-07-29)**

| 필드 | 타입 | 의미 |
|---|---|---|
| `schemaVersion` | int | 현재 1 |
| `optionCount` | int | 3택 고정 (Core `RewardOptionCount`와 정합) |
| `rewards[]` | array | 후보 풀 |
| `rewards[].id` | string | 고유 id |
| `rewards[].type` | string | `capsules` \| `slotLevel` \| `repairHp` (→ `RewardType`) |
| `rewards[].slot` | string? | `slotLevel`일 때만 필수. `MainShot`/`Missile`/`Option`/`Shield` |
| `rewards[].amount` | int | 캡슐 횟수 / 슬롯 레벨 증가 / max HP 증가 |
| `rewards[].weight` | int | 가중치 (≥1). 현재 풀은 전부 1 = 균등 |
| `rewards[].stageIndexMin/Max` | int | 해당 스테이지 클리어 시에만 후보 포함 |

**선택 알고리즘 제안 (결정론 유지)**

1. `stageIndex`로 풀 필터 → 가중치 합 검증
2. `Rng.Fork(RewardSelectionStream).Fork(stageIndex)`로 **비복원 가중 샘플** `optionCount`회
3. 후보 수 < `optionCount`이면 파서/런타임 에러 (카탈로그 무결성)

현재 JSON 6종·weight 1·stage 1–99는 sim 브랜치 내장 풀과 **결과 분포가 동일**하도록 맞춤 (균등 비복원).

**왜**

REQ-007이 `rewards.json 연동 예정`으로 내장 풀을 남겼다. 원본은 GameData (AGENTS.md §5). Presentation/밸런스 시뮬이 같은 파일을 읽게 하려면 파서가 선행돼야 한다.

**제안 시그니처 (초안 — CODEX 재량)**

```csharp
// GameDataSet 또는 별도 RewardCatalog
public sealed class RewardCatalog
{
    public int OptionCount { get; }
    public IReadOnlyList<RewardDefinition> All { get; }
    public IReadOnlyList<RewardDefinition> EligibleForStage(int stageIndex);
}

public readonly struct RewardDefinition
{
    public string Id { get; }
    public RewardType Type { get; }
    public PowerUpSlot Slot { get; }  // type != SlotLevel 이면 무시
    public int Amount { get; }
    public int Weight { get; }
    public int StageIndexMin { get; }
    public int StageIndexMax { get; }
}
```

`GameDataParser.Parse` 시그니처에 `rewardsJson` 인자 추가, 또는 선택적 오버로드. 기존 3인자 경로를 깨지 않으려면 rewards 미주입 시 내장 폴백을 잠시 유지해도 된다 (제거 시점은 CODEX 판단).

### CODEX 응답 (2026-07-29)

**완료.**

- 기존 `GameDataParser.Parse(enemies, weapons, waves)`는 유지하고
  `Parse(enemies, weapons, waves, rewards)` 오버로드를 추가했다. `rewardsJson == null`이면
  `GameDataSet.Rewards`가 null이며 `RunManager`는 기존 6종 내장 풀을 그대로 사용한다.
- `rewards.json` schema v1의 `optionCount`, id/type/slot/amount/weight 및
  `stageIndexMin/Max`를 경로 포함 오류로 검증해 불변 `RewardCatalog`로 노출한다.
- `RunManager`에 `RewardCatalog` 주입 생성자를 추가했다. 스테이지 범위를 양끝 포함으로
  필터한 뒤 `Rng.Fork(RewardSelectionStream).Fork(StageIndex)`만 사용해 정수 weight 기반
  비복원 3택을 생성한다. 적격 후보가 3개 미만이면 명시적 오류를 낸다.
- 파서, 실제 저장소 `GameData/rewards.json`, 하위 호환, 결정론, weight, 비복원,
  스테이지 필터 및 후보 부족 테스트를 추가했다.
- 검증: `Tools/CoreStandalone`의 `dotnet test --no-restore` **108/108 통과**.
  일반 `dotnet test` 복원 단계는 샌드박스가 사용자 프로필 `NuGet.Config` 읽기를
  거부해 실행할 수 없었으나, 동일 프로젝트의 컴파일 및 전체 테스트 실행은 통과했다.
- 커밋은 시도했으나 worktree Git 메타데이터의 `index.lock` 생성 권한이 없어 실패했다.
  변경은 sim 작업 트리에 남겨 오케스트레이터가 커밋할 수 있게 했다.
- 실제 Unity 로더가 4인자 파서와 `data.Rewards`를 전달하는 Presentation 연결은
  CLAUDE 소유이므로 `Reviews/from-codex/requests.md`에 후속 요청을 남겼다.

---

## 2026-07-29 밸런스 v1 (잠정)

**승인 맥락:** 오케스트레이터가 아래 밸런스 우려 중 우선 4건을 잠정 승인 (사람 수면 중 위임). AGENTS.md §7 최종 확정은 사람 검토 후.  
**범위:** `GameData/waves.json` · `enemies.json` · `rewards.json` 만. 스키마 변경 없음.  
**비적용 (이번 패스 밖):** A1–A4 체감 속도/히트박스, B2–B4 세그먼트·contact, C3–C6 보스 배율/holdX/페이즈 경계, D2–D4 repair/슬롯 weight/stage 제한, CarryFraction·슬롯 max.

### 변경 일람

| # | 파일 | 항목 | before → after | 근거 |
|---|---|---|---|---|
| V1-C1 | `waves.json` `boss_stage1` | `hp` | 500 → **1000** | main_shot base 10 / interval 8t, level≥1 가정 `Damage.Compute` → 10 dmg. 이론 DPS = 10×(60/8) = **75**. TTK = 1000/75 ≈ **13.3s** (목표 12–15s). 기존 500/75 ≈ 6.7s는 보스 연출·회피 학습 창이 부족. 옵션·미사일 합산 시 실효 TTK는 더 짧아지므로 이론 하한 근처보다 중앙(≈13s)을 택함. |
| V1-C2 | `waves.json` phase2 | `fireIntervalTicks` / `bulletSpeed` | 35→**45** / 11.0→**10.0** | 실효 `PlayerMaxHp=3`(BattleDirector 잠정). 5-way 유지로 위협 유지. 볼리 주기 35t(≈1.71/s)→45t(≈1.33/s)로 회피 창 확대, 탄속 11→10으로 반응 여유 소폭 부여. ways·페이즈0 미변경. |
| V1-B1 | `enemies.json` `zako_fast` | `dropWeight` | 3 → **2** | `P(drop)=w/(noDrop+w)`, noDrop=8. 3/11≈**27%** → 2/10=**20%**. `seg_swarm_fast` 18기 기대 캡슐 ≈4.9→**≈3.6**. 전역 noDrop 손대지 않고 스웜 개체만 하향 (잡졸 4–5 곡선 유지). |
| V1-D1 | `rewards.json` | 캡슐 보상 | id `capsules_3` amount 3 → id **`capsules_5`** amount **5** | 스테이지 클리어 3택에서 슬롯+1·maxHP+1 대비 캡슐×3(커서 3칸) 체감 열세. amount 5로 게이지 한 바퀴+α 수준. weight 균등 유지(차등화는 분포 시뮬 후 2차). Core 파서 연동 전(REQ-G001)이라 id 개명 안전. |

### 이론 검산 (잠정, 헤드리스 미실행)

- **보스 TTK (메인만, 풀히트):** 1000 HP / 75 DPS ≈ 13.3s ∈ [12, 15].
- **페이즈2 밀도:** 5발/45t ≈ 6.7발/초 (was 8.6). HP3 기준 연속 피격 허용 3회 — 밀도↓로 즉사 압박 완화, 조준 부채꼴 5-way는 유지.
- **스웜 드롭:** 기대 캡슐/세그먼트 ≈3.6 (was ≈5). 스테이지 3세그먼트 중 swarm 포함 시 게이지 과공급 완화 기대.
- **보상:** 캡슐 후보 1회 선택 시 Collect×5. 슬롯 후보 4/6 비중은 불변.

### 후속 관찰 (사람 밸런스 패스)

1. 풀파워(옵션+미사일) 보스 TTK가 8s 미만이면 hp 추가 상향 또는 페이즈 장갑 구간.
2. phase2가 여전히 빡세면 interval 50 또는 ways 4; 싱거우면 탄속 11 복귀.
3. swarm 드롭 과소 시 `zako_fast.dropWeight` 2→3 롤백보다 noDrop 일괄 조정 금지(다른 적 경제 흔들림).
4. `capsules_5` vs 슬롯+1 체감 — Presentation 보상 UI 연동 후 weight 차등 검토.
5. Resources 복사본(`Assets/Resources/GameData/`)은 빌드/씬 재생성 시 원본 동기화 — CLAUDE `Tools → Shmup → Rebuild Battle Scene` 또는 동등 파이프.

### 미적용 우려 (검토 기록 유지, 수치 손대지 않음)

A1–A4, B2–B4, C3–C6, D2–D4 및 §E 사람 결정 항목 — 별도 지시 대기.

---

## 밸런스 검토 기록 (2026-07-29) — 재스케일 + boss_stage1

**범위:** REQ-006 재스케일 수치 (`player`/`weapons`/`enemies`/`waves` 세그먼트) + REQ-008 part1 `boss_stage1` 페이즈.  
**조치 (당시):** 수치 **변경 없음** (AGENTS.md §7). 아래는 사람 밸런스 패스용 **우려·제안**.  
**후속:** 우선 4건은 위 **2026-07-29 밸런스 v1 (잠정)** 에서 반영.

### A. 플레이필드 재스케일 (×5/3 속도·거리, Y×1.6, 히트박스×1.5)

| # | 우려 | 근거 | 제안 (확정 금지) |
|---|---|---|---|
| A1 | **체감 속도 검증 미완** | 기계적 환산으로 player 8→13, scroll 3→5, main shot 12→20. 화면 횡단 시간은 유지 설계이나 반올림 잔여(4.25/8.25/3.25 등)와 히트박스 확대가 겹치면 "넓어진 화면에서 더 바빠진" 느낌이 날 수 있음. | 플레이 패스 후 속도만 일괄 ±10% 후보를 시뮬로 비교. 개별 적 속도 손대기는 후순위. |
| A2 | **히트박스 ×1.5 vs 플레이필드 비대칭 확대** | 필드 halfW 20u(구 대비 ×5/3≈1.67), halfH 11.25(×1.6). 플레이어 hitbox 0.25→0.375(×1.5). 피격 면적 증가율이 필드 확대율보다 약간 큼 → 탄 회피 여유가 소폭 줄 수 있음. | 보스/터렛 탄 밀도 체감 후 hitbox 0.35 등 미세 하향 후보. |
| A3 | **스폰 X=21 vs 뷰 우측 20** | 스폰이 뷰 밖 +1u. 고속 `zako_fast`(8.25 u/s)는 등장 인지 시간이 짧음. | 스웜 세그먼트만 spawn 틱을 앞당기거나 fast 속도를 7.5 후보로. |
| A4 | **사인 진폭 + 스폰 Y 합** | 예: `zako_sine_slow` y=±5.5 amp 3.25 → 피크 ≈±8.75 (halfH 11.25 안). 당장은 이탈 없음. 추가 진폭/레인 확장 시 클램프·이탈 위험. | 신규 세그먼트 작성 시 `\|y\|+amplitude < halfH − halfHeight` 체크리스트. |

### B. 웨이브 밀도·드롭 (확장 카탈로그 유지)

| # | 우려 | 근거 | 제안 |
|---|---|---|---|
| B1 | **스웜 드롭 과다** | `zako_fast` dropWeight 3, `noDropWeight` 8 → 대략 3/11 ≈ 27%/킬. `seg_swarm_fast` 18기면 기대 캡슐 ≈5. 스테이지 3세그먼트 누적 시 게이지 과공급 가능. | fast `dropWeight` 2 또는 swarm 스폰 수 삭감. 관측 포인트는 기존 from-grok 기록과 동일. |
| B2 | **difficulty 1 풀이 얇음** | intro / sine_pair / sine_rush 3종만으로 `segmentsPerStage=3` → 조합 다양성 낮음, 초반 반복 체감. | diff1 전용 세그먼트 1–2 추가(밀도는 낮게) 또는 intro 변형. |
| B3 | **sandwich + elite (diff 3+)** | 상하 포탑 + elite_sine(hp 50, contact 2, drop 12). 초중반 파워 부족 시 벽. | sandwich `difficultyMin` 4, 또는 elite hp 40 후보. |
| B4 | **contactDamage 2의 의미** | 기본 `PlayerMaxHp=1`(Core/GameData 미기재)이면 contact 1·2 모두 즉사. tank/elite contact 2는 max HP>1(수리 보상·향후 체력 확장) 전에는 차별 신호가 안 됨. | 플레이어 기본 HP를 GameData로 승격·2+로 둘지 사람 결정 후 contact 곡선 재검토. |

### C. boss_stage1 페이즈 전투 (waves.json)

현재 값: `hp 500`, hitbox `4×3u`, `holdX 14`,  
phase0 `{55t, 3-way, 9 u/s}`, phase1 `{35t, 5-way, 11 u/s}` (HP 균등 분할 — Core equal-split).

| # | 우려 | 근거 (대략 계산, 잠정) | 제안 |
|---|---|---|---|
| C1 | **TTK이 짧은 편** | main_shot base 10, interval 8t, level 0도 `Damage.Compute(..., max(1,level))` → 10 dmg. 이론 DPS ≈ 10×(60/8)=75. 풀히트 가정 TTK ≈ 500/75 ≈ **6.7s**. 옵션 레벨·미사일 시 더 짧음. | hp 800–1200 후보, 또는 페이즈별 무적/장갑 구간. "보스전 연출 길이" 목표 초를 사람이 먼저 정할 것. |
| C2 | **페이즈2 탄막 vs HP1** | phase2: 5발/35t ≈ 8.6발/초, 조준 부채꼴(슬롯 간격 11.25°). `PlayerMaxHp=1`이면 실드 없이 한 발 = 사망. 짧은 TTK와 맞물려 "딜레이스 or 즉사" 이분법. | (a) 기본 HP 상향, (b) phase2 interval 45–50, (c) ways 4, (d) 탄속 9 유지 중 택. |
| C3 | **전 스테이지 동일 보스** | stageIndex 1–99·diff 1–5 동일 hp/페이즈. 후반 파워(슬롯 보상·CarryFraction) 누적 시 보스가 허수아비가 됨. | 단기: hp를 stage/diff 배율 테이블로(스키마 확장). 중기: M3 보스 로테이션. |
| C4 | **holdX=14 / 대형 히트박스** | 필드 우측(halfW 20)에서 4u 반폭 → 좌측 끝 10u까지 몸체. 플레이어 스폰 −13에서 사거리·자리잡기는 여유, 회피 코리도(보스 좌측)는 좁아질 수 있음. | holdX 15–16 또는 halfWidth 3.5 후보 — 스프라이트 실측 후. |
| C5 | **페이즈 경계 = HP 50%만** | 2페이즈 equal-split은 구현 단순. "광폭화" 체감이 탄 간격·ways 점프에만 의존. | 추후 hpRatio 배열 스키마(예: [0.6, 0.25])로 전환 여지 — REQ-008 요청1 원문과 정합. 지금은 Core equal-split에 맞춤. |
| C6 | **보스 탄 vs 플레이어 탄속** | 보스 9–11 u/s, 플레이어 본탄 20 u/s. 접근 전투 시 반격 창은 넓음. 난이도는 탄 **밀도·조준**이 지배. | 탄속보다 interval/ways 조정이 우선. |

### D. 보상 풀 (`rewards.json` 신설분, 수치 잠정)

Core 내장과 동일: 캡슐×3 / 4슬롯 각 +1 / 선체 maxHP +1, weight 균등, 3택.

| # | 우려 | 근거 | 제안 |
|---|---|---|---|
| D1 | **capsules_3 체감 약함** | `Collect()`×3은 커서만 3칸 이동. 스테이지 클리어 보상으로 슬롯 +1·maxHP +1 대비 가치 불균형. | 캡슐 보상 제거, amount 상향+자동 활성화 없음 명시 UI, 또는 "랜덤 슬롯 +1"로 교체. |
| D2 | **repairHp = maxHP 영구 증가** | Core `ApplyReward`가 `_battleConfig.PlayerMaxHp += amount` 후 다음 스테이지부터 적용. 기본 1에서 스테이지마다 +1 가능하면 후반 난이도 붕괴(특히 CarryFraction=1.0과 겹침). | 스테이지 상한·weight 하향·후반 stageIndexMin 제한, 또는 "현재 HP만 회복" 타입 분리. |
| D3 | **슬롯 +1 ×4 비중 2/3** | 6후보 중 4가 슬롯. 3택 비복원 시 슬롯 보상이 거의 항상 1개 이상. 의도적일 수 있으나 빌드 편중(MainShot 선호) 가능. | 슬롯 weight를 후반 차등, 또는 이미 max인 슬롯 제외 로직(CODEX). |
| D4 | **스테이지 제한 미사용** | 전원 1–99. 초반 repair/후반 고티어 보상 곡선 없음. | stage 4+ 전용 보상, stage 1 전용 약한 풀 등 구간 설계는 사람 지시 후. |

### E. 사람 결정 대기 (AGENTS.md §7 — 에이전트 변경 금지)

- `MetaProgression.CarryFraction` (기본 1.0) + 스테이지 보상 슬롯 승급 → 런 간 파워 인플레
- `PowerUpGauge` 슬롯 최대 5/3/4/3
- 적 HP·contact·드롭, 보스 hp/페이즈, 무기 baseDamage·interval
- 플레이어 기본 max HP (현재 Core 기본 1, GameData 미승격)

### 권장 밸런스 시뮬 시나리오 (후속 GROK 작업 후보)

1. seed 고정 × stage 1 보스만: 무파워 / 실드1 / 풀파워 TTK·피격 횟수  
2. stage 1→5 보상 3택 랜덤 선택 정책(항상 슬롯 / 항상 repair) 후 보스 TTK 추이  
3. swarm 세그먼트 단독 기대 캡슐 수 vs noDropWeight 민감도  

(스크립트 추가 시 `Tools/` 아래 content 소유 경로에 두고 CoreStandalone 참조.)

---

## 콘텐츠 확장 기록 (2026-07-28) — 스테이지 썰렁 피드백

플레이 피드백: 스테이지가 썰렁하다. `enemies.json` / `waves.json` 카탈로그 확장. **스키마 형식 변경 없음.** 아래 수치는 전부 **잠정값**이며 손맛·밸런스 최종 확정은 사람 결정 (AGENTS.md §7).

### enemies.json — 추가 5종 + dropWeight 정비

| id | movePattern | hp | moveSpeed | dropWeight | 의도 |
|---|---|---|---|---|---|
| `zako_straight` (기존) | straight | 10 | 3.0 | **4** (was 3) | 기본 잡졸. 드롭 체감 소폭 상향. |
| `zako_sine` (기존) | sine | 10 | 2.5 | **5** (was 3) | 사인 잡졸. 회피 부담 대비 드롭 우대. |
| `turret_ground` (기존) | static | 30 | 0 | **2** (was 1) | 지상 포탑. 저드롭 유지하되 0에 가깝지 않게. |
| `zako_fast` **NEW** | straight | 6 | 5.0 | 3 | 고속 저체력 스웜. 밀도 담당, 개체당 드롭은 낮음. |
| `zako_tank` **NEW** | straight | 40 | 1.5 | 7 | 저속 고기동 탱커. 킬 보상형 드롭. |
| `zako_sine_slow` **NEW** | sine | 18 | 1.8 | 6 | 느린 사인. 화면 점유·압박. |
| `turret_ceiling` **NEW** | static | 30 | 0 | 2 | 천장 포탑. ground 대칭. |
| `elite_sine` **NEW** | sine | 50 | 2.0 | 12 | 엘리트. 고 dropWeight로 캡슐 하이라이트. fireInterval 120 잠정. |

**dropWeight 설계 메모 (잠정):** 상대 가중치만 의미 있음. 잡졸 4–5 / 스웜 3 / 포탑 2 / 탱커·슬로사인 6–7 / 엘리트 12. 절대 드롭 확률 공식은 Core 드롭 구현에 따름 — 체감 과다/과소 시 스케일 일괄 조정 권장.

`contactDamage` / `scoreValue` / `fireIntervalTicks` 도 잠정. 엘리트·탱커 contactDamage=2는 위험 신호용 플레이스홀더.

### waves.json — 세그먼트 3 → 8종, 밀도 상향

`laneCount=3`, `segmentsPerStage=3`, `startLaneMask=2`, 보스 메타 유지. **모든 세그먼트 `entryLaneMask=7`, `exitLaneMask=7`** → difficulty 1–5에서 `segmentsPerStage=3` 조립·보스 진입 가능 (기존 클리어 가능성 전략 유지).

| 세그먼트 | diff | lengthTicks | traversable | 밀도 성격 | 의도 |
|---|---|---|---|---|---|
| `seg_intro_line` | 1–3 | 600 | `[7]` | 중 (10 spawns) | 입문 직선. y 분산으로 전 레인 사용감. |
| `seg_sine_pair` | 1–5 | 600 | `[2]` | 중–고 (10) | 상하 사인 + slow. center 코리도. |
| `seg_turret_floor` | 2–5 | 900 | `[6]` | 중 (11) | 바닥 포탑 + 상부 잡졸/탱커. top\|center. |
| `seg_swarm_fast` **NEW** | 2–5 | 600 | `[7]` | **고** (18) | 고속 스웜 폭주. 전 레인 개방. |
| `seg_mixed_mid` **NEW** | 2–5 | 720 | `[7]` | 중–고 (14) | straight/sine/fast/tank 혼합 샘플. |
| `seg_turret_ceiling` **NEW** | 2–5 | 900 | `[3]` | 중 (11) | 천장 포탑. bottom\|center. floor 대칭. |
| `seg_sandwich` **NEW** | 3–5 | 840 | `[2]` | 고 (17) | 상하 포탑 + 중앙 압박 + elite 피날레. |
| `seg_sine_rush` **NEW** | 1–4 | 660 | `[6]` | 중–고 (14) | 사인 연속. floor 회피 메타(bottom 제외). |

**difficulty 1 풀:** intro / sine_pair / sine_rush 만 → 3세그먼트 조립 가능.  
**difficulty 2:** sandwich 제외 대부분.  
**difficulty 3–5:** sandwich 포함 풀 카탈로그.

### 잠정값 일람 (확정 금지 — 사람 지시 전 유지)

- 신규 적 HP / speed / dropWeight / contactDamage / score / fireInterval
- 기존 적 dropWeight 변경 (3→4, 3→5, 1→2)
- 전 세그먼트 spawn tick·y·lengthTicks·밀도
- 보스 `hp: 500` 미변경 (기존 플레이스홀더)

### 후속 관찰 포인트 (밸런스 시뮬 / 플레이)

1. 스웜 세그먼트에서 드롭이 과다해지면 `zako_fast.dropWeight` 또는 스폰 수를 먼저 깎을 것.
2. sandwich + elite가 difficulty 3+에서 과도하면 `difficultyMin` 4로 올리거나 elite HP 하향.
3. `segmentsPerStage`는 3 유지 — 카탈로그 다양성으로 반복 체감만 완화. 스테이지 절대 길이가 짧으면 상수 상향은 별도 결정.
4. Core/Presentation이 `movePattern` 문자열을 아직 전부 소비하지 않을 수 있음 — 데이터는 스키마 그대로 준비. 미구현 패턴 시 CLAUDE/CODEX 연동 필요.

### 다른 에이전트 요청

(2026-07-29 갱신: 상단 REQ-G001 참고.)
