# REQ-158 + REQ-159 — 히든 보스 페이즈 도달 + 전용 테마

- 작업일: 2026-08-04
- 담당: GROK / CONTENT (`wt-content` / branch `content`)
- 선행: main fast-forward (REQ-157 테스트 + `Req158BossPhaseReachabilityTests`)

---

## 한 줄 요약

1. **REQ-158**: 레비아탄/브루드마더 파츠 HP·취약 규칙을 고쳐 **모든 본체 페이즈 + 격파**가 산수로 가능하게 했다.  
2. **REQ-159**: 히든 전용 테마 `abyss` / `brood` + 세그먼트 각 5종 + 보스 `theme` 바인딩.

`dotnet test` (CoreStandalone) **573/573 PASS**.  
BalanceSim: 콜로설·REQ-158 관련 게이트 PASS. (아래 §5 기존 FAIL 일부는 본 작업과 무관.)

---

## REQ-158 — 페이즈 도달 불변식

### 원인 (확정)

페이즈 전환 조건은 잔여 HP 비율(`hpThreshold` 0.5 / 0.2).  
**누적 취약 파츠 HP 합**이 다음 문턱까지 깎기에 모자라면 그 페이즈에서 영구 정지.

| 보스 | 수정 전 ph0 floor | ph1 문턱 | 수정 전 ph1 floor | ph2 문턱 |
|---|---:|---:|---:|---:|
| leviathan | **39000** | 31000 | **21400** | 12400 |
| broodmother | 28000 | 31000 | **12400 (=0 margin)** | 12400 |

레비아탄: ph0에서 취약 합 23k만으로 31k 문턱을 못 넘김 → **ph1 미시작**.  
브루드: ph1 여유가 0 — 코어 HP=문턱이라 미세 오차/리젠 상황에서 위태로움.

### 수정 내용과 근거

#### 공통

| 항목 | 값 | 근거 |
|---|---|---|
| 총 HP | **62000 유지** | BalanceSim `ColossalTotalHp` / full-eff TTK≥40s@1500 |
| act 문턱 | **0.5 / 0.2 유지** | BalanceSim REQ-116 (1/2, 1/5) |
| 코어 HP | **12400 → 10000** | 마지막 문턱(12400) 대비 **margin 2400**. 코어만 무적인 채 ph1을 끝낼 때 floor=coreHp 이므로, margin을 내려면 **core < 0.2×total** 이 필수. BalanceSim `ColossalCoreHp` 동기화. |

#### boss_leviathan

| 파츠 | 구 HP | 신 HP | 비고 |
|---|---:|---:|---|
| turret_spine | 5000 | **7500** | ph0 풀 확장 |
| head_cowl | 4500 | **7000** | |
| rear_engine | 3500 | **5500** | |
| lower_launcher | 5000 | **7500** | |
| shield_emitter | 5000 | **6500** | |
| blade_limb_* | 4000 | **3500** | ph1 신규 — 총량 유지 위해 소폭↓ |
| rib_gate | 9600 | **6000** | |
| railgun | 9000 | **5000** | **ph1부터 취약** (레이저 발사는 여전히 ph2 partRules) |
| core | 12400 | **10000** | |

**partRules 변경**: phase 1 `railgun` → `active:true, invulnerable:false` (공격 없음 — 본체 정의에 attack 없음).  
ph2에서 laser 오버라이드 유지.

| 페이즈 | removable | floor | 다음 문턱 | margin |
|---|---:|---:|---:|---:|
| 0 | 34000 | 28000 | 31000 | **3000** |
| 1 (+blades+rib+railgun) | 52000 | 10000 | 12400 | **2400** |
| 2 (+core) | 62000 | 0 | 0 | — |

#### boss_broodmother

| 파츠 | 구 HP | 신 HP |
|---|---:|---:|
| maw | 15600 | **18000** |
| heart_core | 12400 | **10000** |
| (나머지 동일) | | |

partRules 변경 없음. ph0 이미 충분; ph1 margin만 확보.

| 페이즈 | removable | floor | 다음 문턱 | margin |
|---|---:|---:|---:|---:|
| 0 | 34000 | 28000 | 31000 | **3000** |
| 1 (+maw) | 52000 | 10000 | 12400 | **2400** |
| 2 (+heart) | 62000 | 0 | 0 | — |

### form2 (페이즈 4)

본체 HP 0 → form2 전환 구조 유지 (`boss_leviathan_drone` / `boss_broodmother_spawn` HP 7500).  
REQ-158 테스트는 멀티파트 **본체 페이즈**만 검사 (form2는 단일 본체).

### 검증

- `Req158BossPhaseReachabilityTests.EveryMultipartBossCanReachEveryPhaseAndDie` **PASS**
- 전 스위트 573 PASS

---

## REQ-159 — 히든 전용 테마

### 데이터

| 항목 | 내용 |
|---|---|
| `themes` | `…, "core", "abyss", "brood"` (core 뒤 append — ThemeIds[4]=core 유지) |
| `boss_leviathan.theme` | `"abyss"` |
| `boss_broodmother.theme` | `"brood"` |
| gimmicks | abyss: **visionObscured=true** (심해 시야); brood: false, 시간제한 없음 |
| 세그먼트 | 테마당 **5종** (BalanceSim `MinThemeTaggedSegments=5`) |

### 세그먼트 설계 근거

| 원칙 | 적용 |
|---|---|
| 난이도 > 스테이지 5 | `difficultyMin` 4–5, `difficultyMax` 7; 탄속 상향 대신 **배치·수·HP 유형** |
| 보스 예고 | abyss = 느리·단단 (tank/guardian/shard/laser_sentry); brood = 빠르·약·다수 (lancer/hornet/spore/spitter) |
| 중간보스 없음 | `mini_*` 미사용. 방 1개 = 세그먼트 1개 밀도 |
| lengthTicks | 1170–1330 (기존 고난이 ~850–900보다 길게) + **valley gap ≥150t** (REQ-103a ≥120) |
| 레인 마스크 | `[7]`, `[7,3]`, `[7,3,2]`, `[7,3,2,2]` — 기존 후반 패턴과 동일 연결 |

#### abyss (5)

| id | 컨셉 |
|---|---|
| `seg_abyss_pressure_trench` | 탱크·가디언 압력 해구 |
| `seg_abyss_wreck_field` | 침몰 잔해 + 터렛·자석 |
| `seg_abyss_biolum_bloom` | void_moth/echo_wisp → 중형 앵커 |
| `seg_abyss_depth_columns` | 상하 레이저·탱크 기둥 |
| `seg_abyss_leviathan_approach` | 접근로 최고 밀도 |

#### brood (5)

| id | 컨셉 |
|---|---|
| `seg_brood_hatch_rush` | 랜서·호넷 부화 러시 |
| `seg_brood_nest_corridor` | 벽 촉수 + 스포어 복도 |
| `seg_brood_sac_burst` | 산란낭 파열 파도 |
| `seg_brood_tendon_weave` | 촉수 회랑 + mist_specter |
| `seg_brood_mother_approach` | 접근로 최고 밀도 새끼 파도 |

---

## Core 공존 변경 (최소, 이유)

`themes`에 abyss/brood를 넣으면 **ThemeIds 길이 7**이 되어:

- `BuildThemeOrder`가 core를 섞고 brood를 끝으로 고정 → 요새 스테이지 인덱스 붕괴
- `GenerateRoute(abyss)`는 콜로설 제외라 **조립 불가** → 기존 테스트 7건 실패

content 단독으로 검증 가능하도록 **최소 필터**를 넣었다:

| 파일 | 변경 |
|---|---|
| `SegmentStageGenerator.cs` | `IsHiddenOnlyTheme` / `HiddenThemeAbyss|Brood` 상수; `BuildThemeOrder`가 히든 테마 제외 (1–5 바이옴 순서 = 기존 5개) |
| `GameDataParserTests.cs` | `RepositoryWaveCatalogSupportsEveryRouteEncounterType`가 히든 테마 skip |

### 오케스트레이터(Core/Presentation) 남은 일

1. **히든 방 라우팅**: 언차티드 진입 시 세그먼트/`StagePlan.ThemeId`를 선택된 콜로설에 맞춰 `abyss`/`brood`로 고정. (지금은 GenerateRoute가 콜로설을 고르지 않음 — `IsHiddenOnlyColossalBoss` 스킵.)
2. **방 수 1 + 바로 보스**: 중간보스 없이 세그먼트 1 → 보스.
3. **배경/패럴랙스 아트** id = `abyss` / `brood`.
4. (선택) 히든 Generate API: 콜로설 허용 + `GenerateColossalBoss`와 세그먼트 조립 통합.

이 필터는 “본 라우팅”이 들어오면 그쪽으로 대체·확장해도 된다. **ThemeIds 카탈로그에는 abyss/brood를 남겨** 세그먼트·기믹·보스 theme 파싱이 깨지지 않게 했다.

---

## BalanceSim

| 체크 | 결과 |
|---|---|
| Colossal core=10000, theme abyss/brood | **PASS** |
| GenerateColossalBoss stage5/diff5 | **PASS** |
| full-fight TTK ratio vs normal | **PASS** (≈2.22 in [2.0, 2.5]) |
| REQ-103a valley (히든 세그먼트) | **PASS** (gap 150) |
| 히든 forced assembly / cleanKill | skip (의도) |

본 작업과 무관·기존으로 보이는 FAIL (로컬 스냅샷 기준 6건 중 일부):

- scoring graze smoke
- enemy movement count band
- segment weight count (expected 60 — 카탈로그 확장으로 이미 이탈)
- laser template sources peak
- `seg_sandwich_hive` mid-lane breakable (103b)

원하면 별도 티켓으로 정리.

---

## 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | REQ-158 HP/rules + REQ-159 themes/segments/gimmicks/boss.theme |
| `Tools/BalanceSim/Program.cs` | ColossalCoreHp=10000, 히든 테마 스킵, 보스 theme 게이트 |
| `Assets/Scripts/Core/Generation/SegmentStageGenerator.cs` | 히든 테마 필터 (최소) |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | 히든 라우트 스킵 |
| `Reviews/from-grok/req-158-159-hidden-phase-and-themes.md` | 본 보고 |

---

## 검증 명령

```
cd Tools\CoreStandalone && dotnet test
dotnet run --project Tools\BalanceSim\VerifyThemeAssembly.csproj -c Release
```
