# REQ-154 — 히든 보스 4페이즈 재구성

- 작업일: 2026-08-04
- 담당: GROK / CONTENT
- 브랜치: `content` / `wt-content` (main fast-forward 후 작업)
- 선행: REQ-153 (피니셔 클리어 가능 수준), REQ-139 (form2 경로)

## 한 줄 요약

`boss_leviathan` / `boss_broodmother` 본체를 **성격 다른 3페이즈**로 나누고, **페이즈 4는 기존 `form2` 스키마**로 소형 비행체 최종전을 붙였다. Core 신규 개념 없음.

---

## 1. Core 기존 수단 확인

| 필요 능력 | 이미 있는가 | 사용처 |
|---|---|---|
| 물리 타격 (낫/촉수) | `BossPartAttackType.MeleeCharge` | 레비아탄 blade_limb, 브루드 촉수 P2 |
| 대형 빔 (한 줄기) | part `type: laser` + `LaserAttackDto` (레비아탄 railgun 기존) | 레비아탄 P3 |
| 여러 줄기 빔 | **스키마로 가능** — (a) 파츠마다 `type: laser` 독립 발사, (b) `signaturePatternId: laserGrid` = 본체 기준 상·하 대칭 2줄기, (c) `prismCore` = 회전 2줄기 | 브루드 P3 = (a) 3파츠 레이저 |
| 4페이즈 = 소형 비행체 | `form2` + `DefeatBoss`의 `_bossFormIndex == 0 && _bossForm2 != null` | 본체 HP 0 → transition → form2 스폰 |

### 스키마로 안 되는 것 / Core 요청 (현재 없음)

- **N≥3 줄기를 단일 `bossLaser` 프로필로 한 번에** 쏘는 전용 패턴은 없다.  
  → 파츠 레이저 N개로 대체 가능하므로 **Core 변경 불필요**.
- `laserGrid`는 항상 **정확히 2줄기(수직 미러)** 뿐. 각도가 다른 3줄기가 필요하면 파츠 레이저가 정답.
- 빔 “화면 절반 두께”는 데이터 `fullHalfWidth`로 조절 가능 (플레이필드 halfH ≈ 11.25 → 절반 두께면 fullHalfW ≈ 5.6). 회피 우선으로 **2.5(두께 5.0u)** 를 채택. 더 키우려면 사람 지시 후 상향.

---

## 2. 페이즈 구성

공통: 본체 페이즈 3 (hpThreshold **0.5 / 0.2** 유지 — BalanceSim REQ-116 게이트) + form2 페이즈 4.

| 페이즈 | 성격 (사람 지시) | 레비아탄 | 브루드마더 |
|---|---|---|---|
| **1** | 단순 탄, 1~5 보스보다 까다롭게 (밀도·각도) | aimed **5-way / 56t / 7.0** + 포탑 4~5way. 낫 비활성 | radial **5-way / 56t / 7.0** + 촉수 aimed. 밀리/흡입 비활성 |
| **2** | 탄 + 물리 타격 | radial 4-way/48t/7.0 + **blade meleeCharge** 상·하 | aimed 4-way/48t/7.0 + **촉수 melee** + maw **흡입 2.0/2.5**(REQ-153 유지) |
| **3** | 대형 빔 | **railgun 한 줄기** fullHalfW **2.5**, tel 150t, sus 72t, 화면 관통 endX−27.4 | 촉수L/R + maw **레이저 3줄기** (각도 분기, 긴 텔) |
| **4 form2** | 소형 비행체 | `boss_leviathan_drone` half **2.5×2.5**, HP 7500 | `boss_broodmother_spawn` half **2.25×2.25**, HP 7500 |

액트1 fireInterval(56) > 액트2(48) — entrance valley 게이트 유지.

---

## 3. 탄 도달 시간·밀도 계산

가정: holdX≈9 → 플레이어 쪽 유효 거리 **≈15u**, 60 tick/s.

| 소스 | interval | ways | speed | 밀도 (b/s) | 15u 도달 | 비고 |
|---|---:|---:|---:|---:|---:|---|
| stage1 P1 (비교) | 64 | 3 | **9.0** | 2.81 | **1.67s** | 기준 |
| hive P1 (비교) | 50 | 3 | **9.5** | 3.60 | **1.58s** | 기준 |
| **히든 P1 (둘)** | 56 | 5 | **7.0** | **5.36** | **2.14s** | 밀도↑·탄속↓ (하이브 탄속 실패 교훈) |
| 히든 P2 본체 | 48 | 4 | 7.0 | 5.00 | 2.14s | + 밀리 |
| 히든 P3 본체 | 72 | 3 | 7.0 | 2.50 | 2.14s | 빔 주력, 탄은 보조 |
| form2 levi P1 | 24 | 3 | 8.0 | 7.50 | 1.88s | 소형 기동 전함 로봇 대역 |
| form2 levi P2 | 14 | 4 | 8.5 | 17.1* | 1.76s | *burst+텔 — 실효는 낮음 |
| form2 brood P1 | 28 | 4 | 8.0 | 8.57 | 1.88s | figureEight |
| form2 brood P2 | 18 | 3 | 8.5 | 10.0 | 1.76s | lungeReturn |

P1이 “1~5보다 어렵게”의 의미: **탄속 경쟁이 아니라 탄 수·각도**.  
stage1 대비 밀도 약 **1.9×**, 도달 시간은 **더 김**(2.14s vs 1.67s) → 읽을 시간은 있고 피하기 패턴은 까다로움.

### 빔 회피 (bulwark L0 이속 10.4 u/s, REQ-153 기준)

| 빔 | telegraph | 회피 이동량 | 두께 (2×fullHalfW) | sustain |
|---|---:|---:|---:|---:|
| levi railgun | **2.50s** (150t) | **26.0u** | **5.0u** | 1.20s |
| brood maw | 2.17s | 22.5u | 4.0u | 0.80s |
| brood tent L/R | 2.33–2.50s | 24–26u | 3.0u | 0.90s |

저속 기체도 텔 구간에 빔 대역을 벗어날 여유 있음. 동시 본체 탄은 P3에서 희박(3-way/72t).

---

## 4. form2 (소형 비행체) 근거

| 항목 | 레비아탄 | 브루드마더 | 근거 |
|---|---|---|---|
| id | `boss_leviathan_drone` | `boss_broodmother_spawn` | 신규 콘텐츠 id |
| half | **2.5 × 2.5** | **2.25 × 2.25** | 본체 half ≈9.3–10의 **≈1/4** (전함 로봇 2.5×2.0 참고) |
| HP | **7500** | **7500** | @1000 DPS ≈ **7.5s** — 본체 62s 대비 짧은 피날레 |
| transition | 180t (3s) | 180t | core prism과 동일 대역 (요새 로봇 300t보다 짧게) |
| holdX | 10.0 | 10.0 | 전함 로봇과 동일 |
| 패턴 | lungeReturn 3-way → burst 4-way | figureEight radial → lunge aimed | 거대 함체 이후 **기동 도트 대결** |

### 전투 시간 / HP 배분

- 본체 MaxHp **62000 유지** (BalanceSim `ColossalTotalHp`·core 12400·full-eff floor 게이트).
- form2 **+7500** → 이론 TTK 62s → **69.5s (+12%)**.
- 페이즈 성격 재배치로 “늘어난 시간”은 거의 form2 피날레뿐. 본체 안에서는 P1 50% / P2 30% / P3 20% 임계 유지.
- 더 줄이려면 본체 MaxHp 하향이 필요한데 full-eff TTK≥40s@1500DPS 때문에 **≥60000**이 하한 — 사람 지시 시 조정.

---

## 5. 필요 스프라이트 (아트 = CLAUDE/사람)

| id | 생김새 힌트 |
|---|---|
| `boss_leviathan_drone` | 레비아탄 함체가 부서진 뒤 튀어나온 **낫 잔해형 코어 드론**. 본체 팔레트(청회·뼈대) 유지, 전면 소형 레일 노즐 1개. 크기 ≈ 전함 로봇. 사이드 프로파일 우선. |
| `boss_broodmother_spawn` | 브루드 심장이 탈출한 **둥근 유기 비행 유충**. 연보라/점액 하이라이트, 짧은 촉수 2~3개, 중앙 단일 눈/마우. 드론보다 약간 더 둥글게(2.25 half). |

Presentation: form2 id 스프라이트 맵핑 + 전환 연출(이미 `BossFormTransitionStarted` / `BossFormChanged` 이벤트 존재).  
기존 거대 본체 스프라이트(`boss_leviathan`, `boss_broodmother`)는 유지.

---

## 6. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | 두 히든 보스 페이즈 재구성 + form2 |
| `Tools/BalanceSim/Program.cs` | railgun fullHalfW 허용 [1.2, **3.0**]; full-fight TTK에 form2 HP 합산 |
| `Reviews/from-grok/req-154-hidden-boss-4phase.md` | 본 보고 |

---

## 7. 검증

```
python Tools/CsCheck/subunit_grid_check.py GameData/waves.json  → EXIT 0
cd Tools\CoreStandalone && dotnet test  → 통과 572/572
BalanceSim REQ-116: levi railLaser=OK, brood suction/regen/sacs=OK, generate smoke OK
```

BalanceSim 전체 7 fail은 기존 이슈(scoring graze, segment count 92, laser peak, REQ-103b hive softlock 등) — 이번 히든 보스 변경과 무관. 콜로설 full-fight ratio는 form2 합산 후 게이트 내로 복구.

---

## 8. 잔여 제안 (사람 결정)

1. 레비아탄 P3 빔을 기하학적 화면 절반(fullHalfW≈5.6)까지 키울지 — 현재 2.5는 “대형”이지만 절반 미만.
2. form2 HP 7500이 짧게 느껴지면 9000–10000, 반대로 본체 시간을 깎으려면 MaxHp 60000 하한 검토.
3. form2 전용 파츠(약식 낫/촉수)를 붙일지는 연출 여유 보고 추가 가능 (스키마 지원).
