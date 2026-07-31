# REQ-054 후속 — 보스 페이즈 체감 · 중간보스 · 후반/보상 정책 (2026-07-30)

**담당:** GROK = CONTENT  
**상태:** GameData 반영 완료 · 전부 잠정 (AGENTS.md §7)  
**범위:** `GameData/waves.json` · `GameData/enemies.json` · 정책 문서  
**비범위:** Core 파서/Presentation (sim 브랜치 REQ-054 구현 소비 전제)

---

## 1. 보스 페이즈 표 (표준 5종)

Core 페이즈 전환: `(maxHp - hp) * 3 / maxHp` 균등 3분할  
→ p1 진입 잔여 HP ≈ 66.7%, p2 진입 잔여 HP ≈ 33.3%.

구조 고정: **p0 기본(정지·코어만)** / **p1 이동 시작 + 파츠 all** / **p2 광폭화(밀도↑)**.

예상 체류 시간 = 해당 페이즈 HP 몫(≈ total/3) ÷ 바이옴 도달 DPS (이론, 회피 손실 미포함).

### boss_stage1 (scrapyard) — 튜토리얼 · 명확한 3단 교육

| 페이즈 | HP 구간 | 탄막 | 이동 | 파츠 | threat | dens | 체류@500DPS |
|---|---|---|---|---|---:|---:|---:|
| p0 aimed | 100–67% (3000hp) | 55t · 3-way · 9.0 u/s | **stationary** | **coreOnly** | 0.491 | 0.055 | **6.0s** |
| p1 spread | 67–33% (3000hp) | 48t · 6-way · 8.0 u/s | **verticalSine** amp **1.75** / **150t** | **all** | 1.000 | 0.125 | **6.0s** |
| p2 rapid | 33–0% (3000hp) | 20t · 3-way · 14.5 u/s | verticalSine amp **2.0** / **100t** | all | 2.175 | 0.150 | **6.0s** |

- **총 HP 9000** · mid TTK **18.0s** (목표 유지) · full@1880 **4.8s**
- 성격: 가장 완만한 광폭화. p1에서 처음으로 움직임을 가르친다.

### boss_hive — 이동 특화 (대진폭 위빙)

| 페이즈 | HP 구간 | 탄막 | 이동 | 파츠 | threat | dens | 체류@600DPS |
|---|---|---|---|---|---:|---:|---:|
| p0 aimed | 100–67% | 50t · 3 · 9.5 | stationary | coreOnly | 0.570 | 0.060 | 8.1s |
| p1 spread | 67–33% | 42t · 7 · 8.5 | **verticalSine amp 3.25 / 96t** | all | 1.417 | 0.167 | 8.1s |
| p2 rapid | 33–0% | 16t · 3 · 14.5 | **amp 3.5 / 72t** (계속 요동) | all | 2.719 | 0.188 | 8.1s |

- **HP 14500** (15000→패턴세 보정) · mid TTK **24.2s** · full **7.7s**
- 성격: p1부터 화면을 크게 흔든다. 광폭화는 밀도+주기 단축.

### boss_fortress — 요새 · 저진폭 중 밀도 압박

| 페이즈 | HP 구간 | 탄막 | 이동 | 파츠 | threat | dens | 체류@720DPS |
|---|---|---|---|---|---:|---:|---:|
| p0 aimed | 100–67% | 46t · 4 · 10.0 | stationary | coreOnly | 0.870 | 0.087 | 8.3s |
| p1 spread | 67–33% | 40t · 8 · 9.0 | **verticalSine amp 0.875 / 210t** (느린 횡요) | all | 1.800 | 0.200 | 8.3s |
| p2 rapid | 33–0% | 14t · 3 · 15.5 | amp 1.25 / 150t | all | 3.321 | 0.214 | 8.3s |

- **HP 18000** · mid TTK **25.0s** · full **9.6s**
- 성격: 거의 안 움직이는 요새. 위협은 스프레드 밀도 → 광폭 연사.

### boss_storm — 성운 폭풍 · 광폭화 극단

| 페이즈 | HP 구간 | 탄막 | 이동 | 파츠 | threat | dens | 체류@880DPS |
|---|---|---|---|---|---:|---:|---:|
| p0 aimed | 100–67% | 42t · 4 · 10.5 | stationary | coreOnly | 1.000 | 0.095 | 8.5s |
| p1 spread | 67–33% | 36t · 8 · 9.5 | **verticalSine amp 2.75 / 84t** | all | 2.111 | 0.222 | 8.5s |
| p2 rapid | 33–0% | **12t** · 3 · 16.5 | **amp 3.25 / 60t** | all | 4.125 | 0.250 | 8.5s |

- **HP 22500** · mid TTK **25.6s** · full **12.0s**
- 성격: p2 간격 12t · dens 0.25 — 5종 중 가장 거친 광폭화.

### boss_core — 피날레 하이브리드

| 페이즈 | HP 구간 | 탄막 | 이동 | 파츠 | threat | dens | 체류@1050DPS |
|---|---|---|---|---|---:|---:|---:|
| p0 aimed | 100–67% | 40t · 4 · 11.0 | stationary | coreOnly | 1.100 | 0.100 | 8.9s |
| p1 spread | 67–33% | 34t · 7 · 10.5 | verticalSine amp 2.25 / 100t | all | 2.162 | 0.206 | 8.9s |
| p2 rapid | 33–0% | **12t** · 3 · **17.0** | amp 2.75 / 66t | all | 4.250 | 0.250 | 8.9s |

- **HP 28000** · mid TTK **26.7s** · full **14.9s**
- 성격: 이동은 중상 · 탄속·밀도로 피날레 압박 (amp는 storm보다 낮음).

### 5종 성격 요약

| 보스 | 차별 축 | HP | mid TTK |
|---|---|---:|---:|
| stage1 | 교육용 완만 전환 | 9000 | 18.0s |
| hive | **최대 진폭 이동** | 14500 | 24.2s |
| fortress | **최저 진폭 · 밀도 요새** | 18000 | 25.0s |
| storm | **광폭화 극단** (12t / dens 0.25) | 22500 | 25.6s |
| core | 이동+밀도 하이브리드 피날레 | 28000 | 26.7s |

패턴 압박으로 실전 TTK가 이론보다 늘 수 있어 hive~core HP를 소폭 인하(패턴세 보정). stage1은 사람 피드백 18s 목표 유지.

---

## 2. 중간보스 배치 · 선택 정책

### Core 계약 (REQ-054)

- `mini_` 접두 전 항목 → ordinal 정렬 → `Rng.Fork(6).Fork(biomeIndex)` 균등 선택
- **JSON 배열 순서는 결과에 영향 없음**
- 세그먼트 스폰에 `mini_*`를 넣으면 MidBoss와 이중 출현 → **세그먼트 피날레 16건을 중형 앵커로 교체** (MidBoss 전용)

### HP 확정 (잠정 §7) — 직전 사이클 값 유지

| id | HP | 성격 | 홈 테마 (의도, Core 미강제) |
|---|---:|---|---|
| mini_horror | **2400** | 대진폭 sine · 저연사 | hive |
| mini_destroyer | **3000** | 저속 직선 앵커 | fortress / scrapyard |
| mini_crystal | **3600** | 고속 sine · 고연사 | nebula |
| mini_walker | **4500** | 정지 고화력 | core |

### 5 스테이지 / 4 종 반복 정책

1. **전역 풀 4종 유지** — Core가 테마 가중을 아직 안 하므로 전 바이옴 동일 풀.
2. **반복은 허용·의도** — 5 바이옴 × 균등 4풀이면 기대 중복 1회. “엘리트 재출현”으로 취급.
3. **스케일은 HP가 아니라 도달 화력** — 초반엔 같은 미니가 길게, 후반엔 짧게 (아래 TTK).
4. **테마 정합은 soft** — 홈 테마 선호 가중은 CODEX 요청 (아래 §5).

### 중간보스 TTK (도달 시점 화력)

MidBoss는 Opening 직후 → 바이옴 보스 도달 DPS의 **≈70%** 를 앵커로 사용.

| 바이옴 순서* | 가정 mid DPS | horror 2400 | destroyer 3000 | crystal 3600 | walker 4500 | 스테이지 보스 mid TTK |
|---|---:|---:|---:|---:|---:|---:|
| scrapyard (stage1) | 350 | **6.9s** | 8.6s | 10.3s | 12.9s | stage1 18.0s |
| hive | 420 | 5.7s | **7.1s** | 8.6s | 10.7s | hive 24.2s |
| fortress | 500 | 4.8s | 6.0s | **7.2s** | 9.0s | fortress 25.0s |
| nebula | 620 | 3.9s | 4.8s | 5.8s | **7.3s** | storm 25.6s |
| core | 740 | 3.2s | 4.1s | 4.9s | **6.1s** | core 26.7s |

\*테마 순열은 시드 의존. 위는 ordinal 난이도 앵커.

- 전 셀 **≤ 13s** → 상한 30–40s 여유 큼.
- 전 셀 **스테이지 보스 mid TTK보다 짧음** (구간 목표 / 보스 예고).
- 의도: 전용 MidBoss 구간의 **짧은 클라이맥스** (4–13s). 더 길게 하려면 HP 상향을 사람 지시 후.

### 스테이지 보스 TTK 요약

| 보스 | HP | 도달 DPS | mid TTK | full@1880 |
|---|---:|---:|---:|---:|
| boss_stage1 | 9000 | 500 | **18.0s** | 4.8s |
| boss_hive | 14500 | 600 | 24.2s | 7.7s |
| boss_fortress | 18000 | 720 | 25.0s | 9.6s |
| boss_storm | 22500 | 880 | 25.6s | 12.0s |
| boss_core | 28000 | 1050 | 26.7s | 14.9s |

---

## 3. Closing 구간 · EncounterType 웨이브 정책

Core: Closing은 전용 RNG 스트림으로 `Normal / Elite / Supply / Hazard / Rare` 중 하나.  
세그먼트 카탈로그는 Opening과 공유 — **EncounterType 변환**으로 성격을 가른다.

| Type | 세그 수 | Core 변환 | 제안 웨이브 조합 | Opening 대비 밀도 |
|---|---|---|---|---|
| **Normal** | 3 | 그대로 | 테마 후반 세그 가중 (weight↑ 구간: rust_gauntlet / nest_choke / armored_gate / prism_haze / void_mix) | Opening보다 **중형·강화형 비중↑**, 잡몹 러시 비중↓ |
| **Elite** | 1 | HP×1.5 | 고 weight 중형 세그 1개 (mixed_mid, sandwich, theme elite anchors) | 짧은 **고밀도 노드** — Opening 장판 리듬과 반대 |
| **Supply** | 1 | 보스 없음 · drop×4 · 최저 전투 세그 | intro / sine_pair / scrap_debris 계 | **휴식** — 전투 거의 없음, 캡슐 흡수 |
| **Hazard** | 3 | 장애물 미러 주입 | sandwich / turret_cross / phase_discs 등 장애 다수 세그 | 동일 스폰 HP + **회피 코리도 압박** (Opening보다 지형 위험) |
| **Rare** | 3 | spawn HP×2 | Normal과 동일 풀, 적 맷집 2배 | Opening 대비 **TTK 체감 급상승** 보상 노드 |

**Opening vs Closing 분리 원칙**

| | Opening | Closing |
|---|---|---|
| 목표 | 워밍업 · 게이지 축적 | Mid 보상 후 보스 직전 압박 |
| 적 구성 | 잡몹 라인 · 사인 입문 | 중형 앵커 · 포탑 교차 · 엘리트 |
| 밀도 | 여유 회피 코리도 | 레인수축 + 장애 (Hazard) 또는 HP 배율 (Rare/Elite) |
| 보상 | (없음) | Mid 2택 직후 → 보스 → Main 3택 |

현 카탈로그 weight는 Opening/Closing 공유. **섹션 태그(`opening`/`closing`) 가중**이 생기면 Closing 전용 고밀도 세그 가중을 올리는 것이 다음 스텝.

---

## 4. 보상 풀 정책 (중간 2택 / 주 3택)

Core 현황: **동일 `rewards.json` 풀**에서 개수만 2 / 3. 종류 필터 없음.

### 제안 정책 (잠정)

| 종류 | 카드 수 | 후보 풀 (의도) | 제외 |
|---|---:|---|---|
| **MidStage** | 2 | `capsules_*`, `slot_*_1`, `repair_hp_1` / ShieldStock, `passive_move_speed_1` | modifier, missileFamily, optionFormation, fireRate/damage 대형 패시브 |
| **Main** | 3 | 전 풀 — 특히 `modifier`, `missileFamily`, `optionFormation`, `passive_fire_rate/damage`, 슬롯 레벨 | 순수 소량 캡슐만 단독 3장 되는 것은 가중으로 억제 |

**근거:** Mid는 Opening 직후 즉시 화력/생존 보정. Main은 보스 클리어 후 빌드 분기.

### 현 풀 가중 soft 가이드 (스키마 변경 전)

- Mid 체감: `capsules_5` weight 2→**3**, `repair_hp_1` 2→**3** (동일 풀이라 Main에도 영향 — 풀 분리 전까지 소폭만)
- Main 빌드: modifier·family·formation weight 유지 (이미 슬롯과 동급 2)

**CODEX 요청:** `RewardDefinition.selectionKinds` (`mid` | `main` | `both`) 또는 분리 풀. 구현 전엔 문서 정책만.

---

## 5. 다른 에이전트 요청

### CODEX

1. [ ] MidBoss 테마 가중: `mini_*` 홈 테마 매칭 시 weight×2 등 (균등 폴백 유지, 결정론 Fork 유지)
2. [ ] 보상 `selectionKinds` 필터 (Mid 2 / Main 3 풀 분리)
3. [ ] (선택) 세그먼트 `sectionTags: ["opening","closing"]` 가중
4. content 브랜치 Core에 REQ-054 phase movement 파서 병합 확인 — JSON은 이미 채움

### CLAUDE

1. [ ] `Assets/Resources/GameData/` 동기화 (waves / enemies)
2. [ ] `BossPhaseChanged` + 이동/파츠 취약 전환 VFX (REQ-054 CLAUDE 항목)
3. [ ] MidBoss HP UI (`StageSection == MidBoss`)

---

## 6. 검증

| 검사 | 결과 |
|---|---|
| `dotnet test` CoreStandalone | **297/297** |
| `Tools/BalanceSim` | **PASS** (캡슐 EV 10.32, boss TTK mono, threat mono) |
| DeterminismAudit `seed=12345 stages=3 ticks=30000` ×2 | 해시 일치 **`BEB6933375E2C17D`** |

---

## 7. 변경 파일

- `GameData/waves.json` — 5 보스 페이즈 축 채움 · HP 소폭 인하 · mini_* 세그 스폰 제거
- `GameData/enemies.json` — `noDropWeight` 16→15 (mini 제거 후 EV 복구)
- `Tools/BalanceSim/Program.cs` — REQ-054 movement 메모
- `Tools/BalanceSim/_apply_req054_boss_phases.py` — 재현 스크립트
