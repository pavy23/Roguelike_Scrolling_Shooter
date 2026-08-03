# GROK → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고, 완료 항목은 체크한다.

---

## 2026-08-03 REQ-139 — 거대 전함 스케일·미사일/레이저 패턴 (1차)

**완료 (content, 데이터 가능분):**
- `boss_fortress` half **17.0×8.5** (화면 걸침 연출용 본체 박스)
- 파츠: 포탑 4문 갑판 일렬(y≈6.5–7), 코어 함수 안쪽(ox=-11), 함미 engine 상향 배치
- 1페이즈 engine = 저속 `aimedSpread` (미사일 체감) / 2페이즈 turret = `laser` 스태거
- HP **19600 잠금** (크기↑≠시간↑)
- 표: `Reviews/from-grok/req139-report.md`

### 2차 완료 (content `05a5f82`, Core 스키마 af27d38 반영)
- groups 앵커: stern **Y=-9 / travel=0**, hull **Y=0 / travel=90**, bow **Y=0 / travel=0**
- engine.offsetY **1.5→7.0** (갑판 밴드 — 잠김 후에도 Y≈0 직사 피격)
- 표: `Reviews/from-grok/req139-report-phase2.md`
- 검증: `dotnet test` **568/568**

### CODEX
1. [x] 그룹별 `anchorOffsetY`/`anchorTravelTicks` (af27d38, CLAUDE 대행) — 데이터 쪽 값은 GROK 2차에서 채움
2. [ ] 로봇 폼 (`_bossForm2` 경로를 그룹 전멸에도)
3. [ ] `advanceOnGroupCleared` (필요 시)
4. [ ] `GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely`: `RerollCost` expect **5→4** (REQ-137 실데이터)

### CLAUDE
1. [x] Resources `GameData/waves.json` 동기화 (2차 커밋에 포함)
2. [ ] 거대 함체 아트 (본체 half 17×8.5에 맞춤) + 하드포인트 정렬 (engine y=7.0 반영)
3. [ ] 정박 상승 연출 뷰 + 로봇 연출

### 사람
1. [ ] 잠김 깊이(−9)·상승 1.5초 체감 확정 (로봇은 3차)

---

## 2026-08-03 REQ-137 / REQ-138 — 캡슐 ×0.7 + 하이브 보스 재설계

**완료 (content):**
- REQ-137: `noDropWeight` 13→**21** (~0.7× EV), `rerollCost` 5→**4**, BalanceSim 밴드 [7,14]
- REQ-138: `boss_hive` half **4.0×7.25**, 다리 2(tentacle_*)+머리 core 게이트, HP 2500+2500+9500
- 부수: `seg_hive_brood_wave` y 1/256 양자화 (파서 차단 해제)
- 표: `Reviews/from-grok/req137-138-report.md`
- **결정론 해시 변동** (드롭 + 보스 파츠). schemaVersion **불변**.

### CLAUDE
1. [ ] Resources `GameData/enemies.json` · `waves.json` · `rewards.json` 동기화
2. [ ] boss_hive Presentation: torso 앵커(y≈+2.25), 다리 at tentacle parts, 실드 돔 at core, 다리 절단 연출
3. [ ] (선택) 캡슐 희소·하이브 실드 게이트 체감 캡처

### GEMINI
1. [ ] DeterminismAudit 베이스라인 갱신 (REQ-137/138)
2. [ ] 캡슐 EV 교차 · hive coreGate(다리 파괴→머리 노출) 검증

### 사람
1. [ ] REQ-137 0.7× 체감 (early Main L5 근방 성장) 확정
2. [ ] REQ-138 아트–파츠 좌표 정합 확인

---

## 2026-08-03 REQ-135 / REQ-136 — 소형 적 1.5배 + scrap 고철 감축

**완료 (content):**
- REQ-135: 소형 비행 잡졸 12종 `halfWidth`/`halfHeight` ×1.5 (`enemies.json`)
- REQ-136: scrapyard breakable **77→50** (35.1%), REQ-129 분산 유지, 의도 벽 유지
- 표: `Reviews/from-grok/req135-136-report.md`
- **결정론 해시 변동** (히트박스 + 장애물 개수/좌표)

### CLAUDE
1. [ ] Resources `GameData/enemies.json` · `waves.json` 동기화
2. [ ] (선택) stage1 잡졸 가독성·고철 밀도 체감 캡처
3. [ ] 장애물 스프라이트가 Core half 상수에 묶여 있으면 크기 절반 연동 (아래 CODEX 후)

### CODEX
1. [ ] **REQ-136 크기 절반**: `ObstacleHalfWidth`/`ObstacleHalfHeight` 전역 0.5→0.25  
   또는 scrapyard breakable 전용 per-obstacle half 필드 (스키마 확장).  
   GameData obstacle DTO에 크기 필드 없음 — GROK 단독 불가.

### GEMINI
1. [ ] DeterminismAudit 베이스라인 갱신 (REQ-135/136)
2. [ ] stage1 접촉 난이도·고철 밀도 교차 확인

### 사람
1. [ ] REQ-135 ×1.5 접촉 난이도 과한지 (대안 ×1.25 보고에 기술)
2. [ ] 장애물 크기 절반 전역 vs scrap-only 정책

---

## 2026-08-03 REQ-132 — stage1 잡졸 저빈도 사격 (안 A 적용)

**완료 (content):**
- `enemies.json`: `junk_roller` 180 · `scrap_tumbler` 150 (안 A)
- `waves.json`: REQ-131 편대 탄벽 방지 — `debris_line` 편대→skimmer, `center_breach` 편대→pipe_rat
- 표: `Reviews/from-grok/req132-report.md`
- 결정론 해시 **변동** (fire + spawn 기종). schemaVersion 불변.

### CLAUDE
1. [ ] Resources `GameData/enemies.json` · `waves.json` 동기화
2. [ ] (선택) stage1 early 그레이즈·cover 체감 캡처

### GEMINI
1. [ ] DeterminismAudit 베이스라인 갱신 (REQ-132)
2. [ ] early scrap peak 탄밀도 교차 (이론 peak1s≤2)

---

## 2026-08-03 REQ-129 / REQ-130 — scrap breakable 분산 + 잡졸 무발사 조사

**완료 (content):**
- REQ-129: scrapyard breakable x 분산 (`waves.json`) — 5-stack 벽 제거, 의도 벽 일부 유지
- REQ-130: 조사만 — early scrap 사격 0은 소프트 의도, 그레이즈 갭 제안 수치 미적용 (§7)
- 표: `Reviews/from-grok/req129-130-report.md`
- **사람 확정 (2026-08-03):** 안 A 채택 → REQ-132에서 적용 완료

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (scrap breakable 좌표)
2. [ ] (선택) stage1 고철 분산 체감 캡처

### GEMINI
1. [ ] DeterminismAudit 해시 변동 — 베이스라인 갱신 (REQ-129 좌표)
2. [ ] stage1 breakable “한 덩어리” 체감 해소 교차 확인

### 사람
1. [x] REQ-130 안 A (`junk_roller` 180 / `scrap_tumbler` 150) 채택 → REQ-132
2. [ ] 그레이즈·배율 가뭄을 난이도로 볼지 점수 표현으로 볼지

---

## 2026-08-02 REQ-116 — 보스 리디자인 데이터 전면 (content 완료)

**완료 (content):**  
- `waves.json` bosses[] 재작성 (St3 fortress **불변**)  
  - St1: legacyHover → lungeReturn → verticalSine (HP 4250 유지)  
  - St2 hive: 5×4 + 촉수 2 + coreGate · p1 본체 개방  
  - St4 storm: 5×4 + segmentChain p2=1기 / p3=2기  
  - St5 core: form2 `boss_core_prism` HP **14000** · transition 180t  
  - Leviathan/Broodmother: 앵커 파츠 · holdX **9.0** · 3막 50%/20% · 총 62k  
- BalanceSim `CheckReq116BossRedesign` + colossal TTK 2–2.5× 게이트  
**표:** `Reviews/from-grok/req116-report.md`  
**검증:** 545/545 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (전 보스 테이블)
2. [ ] form2 전환 연출 (`BossFormTransitionStarted` / `BossFormChanged`) · 프리즘 아바타 스프라이트
3. [ ] St2 촉수 스윕 · St4 세그먼트 체인 · Broodmother 흡입 왜곡 · Leviathan 참수빔 (fullHalfW≈1.4)
4. [ ] 히든 2종 스프라이트 임포트 + `_bossSpritePrefixes` 등록 (`hidden-boss-anchors.md` 후속)

### CODEX
1. [ ] (선택) 멀티파트 `active:false`가 피격 스킵임을 감사/문서에 명시 — content는 코어 차폐에 의존
2. [ ] (선택) form2 MaxHp를 클리어어빌리티/감사 예산에 form1+form2 합산 반영 여부 검토

### GEMINI
1. [ ] St5 form1+form2+고스트 체감 밀도 교차 검산
2. [ ] 히든 3막 TTK (이론 62s@1000 DPS · 2.17× 일반) vs 실플레이
3. [ ] DeterminismAudit 해시 변동 — 베이스라인 갱신

### 사람
1. [ ] 막별 HP 분배 (core 12400 / maw 15600 / sacs 31k) · 흡입 3/5 · 레일 fullHalfW 1.4 손맛 확정 (§7 잠정)
2. [ ] St2 촉수 HP 2000×2 · St4 체인 headHp 1400/1600 확정 또는 재조율

---

## 2026-08-02 REQ-111 — 대형 기믹 데이터 St3 전함 + St5 고스트 (content 완료)

**완료 (content):**  
- `waves.json` `boss_fortress`: parts(engine+turret×4+core) + `warship` fortress_warship · HP **19600**  
- fortress late fodder thin (drone_lattice/armored_gate/crossfire) · 보스 룸 스폰은 Core 빈 룸  
- St5 closing 밀도 **유지** · GhostReplayConfig L1/8t **기본값 유지** (DPS 75 = St5 reach 7.1%)  
- BalanceSim `CheckReq111WarshipAndGhost`  
**표:** `Reviews/from-grok/req111-report.md`  
**검증:** 529/529 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CODEX
1. [ ] **긴급 인수 리뷰** — `RunManager.ApplyProgressionBossDifficulty` multipart/warship early-return (GROK가 테마 셔플 크래시 차단용으로 content 작업 중 최소 수정). 소유 영역이므로 리뷰·테스트·원하면 리팩터 인수.
   - 원인: progression MaxHp 치환 + source.BossParts 유지 → sum≠MaxHp
   - 제안: warship/parts 있으면 `return source` (현재 적용됨)
2. [ ] (선택) WarshipEncounter를 BattleSim 틱 루프에 본배선 — content 데이터는 plan에 실림. Presentation 연동 전 시뮬 구동 확인
3. [ ] GhostReplayConfig 기본값(L1/8t) 유지 합의 — content 밸런스 권고와 동일. 변경 시 BalanceSim Ghost 게이트 상수 동시 갱신

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (boss_fortress warship+parts · fortress late thin)
2. [ ] 전함 파츠 스프라이트/프리팹 (engine · turret×4 · core) + WARNING/그룹 활성/코어 개막 연출 — `WarshipWarningStarted` / `WarshipGroupActivated` / `WarshipCoreBattleStarted` / `MidBossDefeated`(함미)
3. [ ] (선택) 고스트 잔상은 REQ-109 배선 완료분 유지 · closing 밀도 변경 없음

### GEMINI
1. [ ] St3 fortress 전함 TTK·클리어 교차 검산 (BalanceSim wall≈37s vs 실플레이)
2. [ ] St5 closing 고스트 동시 화력 체감 (L1 보너스 only)
3. [ ] DeterminismAudit 해시 변동 — 베이스라인 갱신 여부

### 사람
1. [ ] 전함 HP 19600 / attrition 720t / ways 9→3 — 손맛 확정 또는 재조율 지시
2. [ ] 고스트 고정 L1 유지 vs 상향 (현재 권고: 유지)

---

## 2026-08-02 REQ-108 — 기체 해금 가격 상향 (content 완료)

**완료 (content):**  
- `ships.json` unlockCost **Interceptor 25,000 → 50,000** / **Bulwark 50,000 → 100,000** (REQ-106 안 A, 사람 승인)  
- 컨티뉴 사다리(2000+1000×stock, max stock 8 → 단가 2k–9k, 풀 누적 44k) vs 50k/100k: **컨티뉴 &lt; 기체 유지**  
**표:** `Reviews/from-grok/req108-report.md`  
**검증:** 510/510 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/ships.json` 동기화 (unlockCost interceptor **50000** · bulwark **100000**)
2. [ ] (선택) 격납고 해금 가격 표시가 JSON `unlockCost`를 그대로 반영하는지 확인

### CODEX
- 없음 (UnlockCost 소비 계약 변경 없음).

### 사람
1. [x] 해금 가격 안 A `50000/100000` 승인 → REQ-108 적용

### GEMINI
1. [ ] 해금 창 체감 (초반 사망 1–2회 / 평균 클리어 1회)
2. [ ] 컨티뉴 구매 vs 기체 해금 우선순위 UX

---

## 2026-08-02 REQ-106 — 11차 밸런스 실드/배율/실드 보너스 (content 완료)

**완료 (content):**  
- `ships.json` startingShieldStock **Starter 2 / Interceptor 1 / Bulwark 3** (정체성 유지·생존 1단 상향)  
- `scoring.json` `multiplierGaugeRequirements` **[30,50,80,130,200]** (5개 정규) + `shieldBonusScorePerStock` **8000**  
- BalanceSim: 6레벨 배열 API · x8/x16/x32 킬 밴드 · 실드 보너스 2–5% 게이트 · ship identity 2/1/3  
- 해금 가격·보드 상한: **제안만** (25k/50k→50k/100k 권장, 보드 10B 유지)  
**표:** `Reviews/from-grok/req106-report.md`  
**검증:** 510/510 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/ships.json` · `scoring.json` 동기화 (실드 2/1/3 · 배율 5요구치 · shieldBonus 8000)
2. [ ] (REQ-105) 런 클리어 `ShieldBonusAwarded` / `RunClearShieldBonus` → `SHIELD BONUS +N` 표시
3. [ ] (선택) HUD 콤보 배율 x16/x32 표시 확인

### CODEX
- 없음 (REQ-105 계약 소비). MaxShieldStock 기본 3 유지 — Bulwark 시작 3=풀스톡.

### 사람
1. [x] 해금 가격 조정: 권장 A `50000/100000` 승인 → **REQ-108 적용**
2. [ ] 보드 상한 9,999,999,999 — 유지 권고 (변경 불필요)

### GEMINI
1. [ ] 실드 2/1/3 체감 · x32 도달 빈도 (노피격 vs 평균)
2. [ ] 클리어 점수 분포 vs 해금 25k/50k
3. [ ] DeterminismAudit 해시 변동 — 베이스라인 갱신 여부

---

## 2026-08-02 REQ-103b — 대개편 2차 기믹 축 데이터 (content 완료)

**완료 (content):**  
- scrapyard `blocksEnemyBullets` cover (전반 티칭 + 후반 엄폐 라인)  
- hive `regenDelayTicks` 180–270 (membrane/organic/nest/hornet) · dig path 유지  
- cleanKill 후반 분기 세그 **10** (테마×2) · Default untagged 유지  
- scroll spike `1.5` ×2 (`seg_scrap_speed_spike` / `seg_core_speed_spike`)  
- fortress 함체 포탑·nebula phase_disc mild 밀도  
- core `timeLimitTicks` 9000→**12000** (기믹 1.5 하 closing 7세그 여유)  
- 세그먼트 **48→60** · BalanceSim `CheckReq103bGimmickAxes`  
**표:** `Reviews/from-grok/req103b-report.md`  
**검증:** 499/499 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (60 segs · blocksEnemyBullets / regenDelayTicks / postMidbossOutcomes / scrollSpeedMultiplier / core TL)
2. [ ] (선택) `EnemyBulletBlocked` · `ObstacleRegenerated` · `MidBossDefeated` 연출 구독
3. [ ] (선택) scrap cover · hive dig · speed-spike 구간 시각 확인

### CODEX
- 없음 (REQ-101 C-A..D 스키마 완료, content optional 필드만 사용). C-E 섹션 마커는 Presentation 연동 시 재검토.

### GEMINI
1. [ ] cleanKill 분기 후반 체감·clearability 교차 검산
2. [ ] DeterminismAudit 해시 변동은 content 의도 — 베이스라인 갱신 여부
3. [ ] core timeLimit 12000 · 기믹 intensity 1.5 계약 경로 소프트락 재검

---

## 2026-08-02 REQ-103a — 스테이지 대개편 1차 (기존 스키마, content 완료)

**완료 (content):**  
- `waves.json` 후반 잠식 multi-mask 7→3→2 (테마 차등, St5=`[7,3,2,2]`) · 마스크 변경 32  
- 전 세그먼트 보스 밸리 gap≥120 (패딩 37)  
- 후반 static 포대 성격 분리 (scrap/hive/nebula/core) · laser peak≤4 유지  
- 스크롤 스파이크 필드 **없음** → Core C-D → **REQ-103b에서 content 채움**  
- BalanceSim `CheckReq103aStageOverhaul` 게이트 추가  
**표:** `Reviews/from-grok/req103a-report.md`  
**Core 요구서:** `Reviews/from-grok/req103-core-requests.md`  
**검증:** 489/489 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CODEX
1. [x] **C-A** `Obstacle.blocksEnemyBullets` — REQ-101 PASS · content REQ-103b 채움
2. [x] **C-B** `Obstacle.regenDelayTicks` — REQ-101 PASS · content REQ-103b 채움
3. [x] **C-C** `midbossOutcome` 분기 — REQ-101 PASS · content REQ-103b cleanKill 풀
4. [x] **C-D** `Segment.scrollSpeedMultiplier` — REQ-101 PASS · content REQ-103b spike
5. [ ] **C-E** `StageSectionEvent` 마커 (Intro/MidbossDefeat/LateHalf/BossWarn) — MidBossDefeated 이벤트는 발행됨 · 전 구간 마커 확장 여부는 Presentation 요청
6. [x] (참고) BalanceSim `CreateSegment(Rng)` REQ-098 호환은 content가 수정함

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (잠식 multi-mask + 밸리 패딩 → **103b 전체 동기화로 통합**)
2. [ ] (C-E 후) SectionTheme lerp — MidbossDefeat / LateHalf 구독
3. [ ] (선택) 잠식 구간 레인 벽/배경 연출 정합

### GEMINI
1. [ ] 잠식 multi-mask 스테이지 clearability·시각 회귀
2. [ ] DeterminismAudit 해시 변동은 content 의도 변경 — 베이스라인 갱신 여부 판단

---

## 2026-08-01 REQ-099 — 세그먼트 풀 장애물 배치 다양화 (content 완료)

**완료 (content):**  
- `waves.json` segments **38→48** (+10 장애물 변형)  
- Diff1: segs 6→**10**, 장애물 3→**7** (zigzag / center breach / shard field / rail split)  
- Diff6–7: 0→**3** (fortress crossfire · nebula lattice · core columns)  
- 테마 어휘: hive 촉수·막 / fortress 차폐·포탑 / nebula 부유 격자 / core 위상 기둥  
- BalanceSim: ExpectedSegmentCount 48 · MinStage1Candidate ≥10  
**표:** `Reviews/from-grok/req099-report.md`  
**검증:** 485/485 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (segments 48)
2. [ ] (선택) 신규 장애물 배치 시각 확인 — zigzag posts / center wall / rail / tentacle pillars

### CODEX
1. [ ] (참고) `GameDataParserTests` Segments.Count **38→48** 은 content가 골든 갱신. 추가 Core 불필요.
2. [ ] REQ-098 시드 지터 진폭이 크면 `seg_scrap_center_breach` 세로 벽 정렬이 흐트러질 수 있음 — 관측 후 진폭 상한 조율.

---

## 2026-08-01 REQ-097 — MainShot(SHOT) maxLevel 5→6 (content 완료)

**완료 (content):**  
- `weapons.json` `main_shot.maxLevel` / `effectSoftCapLevel` **5→6**  
- 평탄 1 비용 유지 → SHOT costToMax=6 (함선 게이지 MainShot 슬롯)  
- BalanceSim: MainShot max=6 · L6/L5∈[1.10,1.40] · 기체 ST ratio 게이트 유지  
**표:** `Reviews/from-grok/req097-report.md`  
**검증:** 485/485 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/weapons.json` 동기화 (`main_shot` max/softCap **6**)
2. [ ] HUD SHOT 슬롯 포화 표시가 max 6과 정합하는지 확인

### CODEX
1. [ ] (선택·기획 확인 후) 주무기 진화 `levels[]` 상한 3→6 완화  
   - `PrimaryWeaponLevelDefinition`: level 허용 1..6  
   - `CopyAndValidateLevels`: entries ≤6  
   - `ParsePrimaryWeaponLevels`: consecutive level 2..N, N≤6  
   - 이유: content가 L4–L6 패턴 오버라이드를 넣을 수 없음 (현재 파서/모델 하드캡 3).  
   - MainShot 파워 축 6은 **이미 content만으로 동작** (하드캡 없음). 진화 6단은 별도 의도일 때만.

---

## 2026-08-01 REQ-095 — SPARTAN 자기 제약 계약 (content 완료)

**완료 (content):**  
- `waves.json` contracts: `spartan_protocol` (ban gauge, ×1.6, extreme, w=1) / `no_option_run` (ban option, ×1.3, high, w=2) / `bare_hull` (ban shield, ×1.4, high, w=2)  
- BalanceSim REQ-095 EV·희소성 게이트 (specialty 상한 6..12)  
- 골든 카탈로그 11→14 + ban 축 검산  
**표:** `Reviews/from-grok/req095-report.md`  
**검증:** 477/477 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData/waves.json` 동기화 (SPARTAN 3계약)
2. [ ] 계약 카드 UI 한 줄 카피 (NO GAUGE / NO OPTION / NO SHIELD + score mult)
3. [ ] 발동 거부 `Contract*Banned` → “CONTRACT LOCK” 피드백 (REQ-094 관측)

### CODEX
- 없음 (ActivationBanned 축은 REQ-094 완료).

---

## 2026-07-31 REQ-081 — 출발선 통일 + 미사일 5계열 (content 완료)

**완료 (content):**  
- `ships.json` **v3**: 전 기체 vulcan L0 + 기본 미사일 (starter=downward_drop / interceptor=straight / bulwark=homing)  
- `weapons.json` **v7**: 더블 `[0,8]`=45°, 미사일 5계열 + damageGrowth / dropDelay / homingTurn  
- `rewards.json`: `missile_family_downward_drop` / `homing` (main, weight 1)  
- BalanceSim: 5-family + L0 open-growth stage1 gate  
**표:** `Reviews/from-grok/req081-ship-startline-missiles-2026-07-31.md`  
**검증:** 408/408 · BalanceSim all green · DeterminismAudit AUDIT PASS

### CLAUDE
1. [ ] Resources `GameData` 동기화 (`ships` v3, `weapons` v7, `rewards` 미사일 2종)

### CODEX
1. [x] `GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely` — content가 스키마 7/출발선 기대값을 반영함 (content 브랜치에서 계약 테스트 갱신; 리뷰 환영).
2. [ ] `GameDataSet.ApplyMissileFamily`에 `DamageGrowthPercentPerLevel` / `DropDelayTicks` / `HomingTurnLutSlotsPerTick` 미적용 — RunManager 경로는 OK, CreateBattleSimConfig 기본 적용 시 누락. 제안: RunManager와 동일 필드 복사.

---

## 2026-07-31 REQ-077 — mid `slot_speed_1` 교체 (content 완료)

**완료 (content):**  
- `rewards.json`: `passive_move_speed_1` (`moveSpeedUp`) → **`slot_speed_1`** (`slotLevel` Speed×1, weight **4**, mid)  
- 이중 성장 경제 제거; mid 풀 가중 유지  
- BalanceSim: free mid Speed 필수, residual `moveSpeedUp` FAIL  
**표:** `Reviews/from-grok/req077-slot-speed-mid-replacement-2026-07-31.md`  
**선행:** REQ-076 (하니스 조준 수정 + `slot_speed_1` 회귀 테스트)

### CLAUDE
1. [ ] Resources `rewards.json` 동기화 (`slot_speed_1`)

---

## 2026-07-31 REQ-075 — 7슬롯 게이지 + 레이저 적 (content 완료 · 후속 요청)

**완료 (content):**  
- `weapons.json` **v6**: `powerUpGauge` 7슬롯 + `primaryWeaponFamilies` 4종 + 슬롯별 비용  
- `enemies.json`: `laser_sentry` / `prism_beamer` (+2 → 카탈로그 34)  
- `waves.json`: scrap/nebula/fortress에 레이저 적 희소 배치  
- `rewards.json`: `light_frame` → SlotLevel Speed×2; mid `moveSpeedUp` 잔류 → **REQ-077에서 `slot_speed_1`로 교체 완료**  
**표:** `Reviews/from-grok/req075-seven-slot-gauge-laser-enemies-2026-07-31.md`  
**검증:** 394/394 · BalanceSim all green · 시드 해시 일치

### CODEX
1. [x] DeterminismAudit `--suite` seed-0-first 7슬롯 하 완주 — REQ-076 감사 자동 플레이(공격 기여 슬롯 투자) + suite AUDIT PASS.
2. [x] 리듬 하니스 hang — Core 아님, 보스 조준 문제. REQ-076 수정 + `slot_speed_1` 카탈로그 회귀 테스트. content는 REQ-077에서 mid 교체 완료.

### CLAUDE
1. [ ] Resources `GameData` 동기화 (weapons v6, enemies 34, waves, rewards — REQ-077 `slot_speed_1` 포함)
2. [ ] 게이지 HUD nameKey 풀네임 (Speed / Missile / Double Shot / Laser / Triple Shot / Option / Shield)

---

## 2026-07-31 REQ-071 — 섹터 계약 + 대가 있는 보상 (완료 · content)

**완료 (content):**  
- `waves.json`: `contracts` 카탈로그 — 표준 1 + 특수 8, options 2..3  
- `rewards.json` schema **v4**: mid/main 풀 분리, 대가 보상 5종, mod maxStacks/maxPerRun **2**  
- BalanceSim REQ-071 게이트 (최밀 escort_run ×1.5 TTK 44s PASS)  
**표:** `Reviews/from-grok/req071-sector-contracts-costed-rewards-2026-07-31.md`  
**검증:** `dotnet test` 383/383 · BalanceSim all green · REQ-060 CLEAR 유지

### CLAUDE

1. [ ] Resources `waves.json` / `rewards.json` 동기화
2. [ ] 계약 카드 UI: riskTier 3색 (Safe=무채 / Low=파랑 / High=빨강), 거래 한 줄 카피
3. [ ] 대가 보상 카드: Gains + Costs 표시 (shieldMaxDown / moveSpeedDown / capsuleDropWeightDown / bombMaxDown)

### CODEX

1. [x] 골든 카탈로그 Rewards 20→**25** + Contracts 존재 검산 — content가 `GameDataParserTests` 동반 수정.
2. [ ] (정보) riskTier JSON 키는 Core 파서 기준 `safe`/`low`/`high`/`extreme`. 기획 "standard" = `safe`. Presentation 매핑만 맞추면 됨.

---

## 2026-07-31 REQ-067/068 — 폭탄 드롭 + 스테이지1 보스 탄수 (완료 · content)

**완료 (content):**  
- `enemies.json`: `bombNoDropWeight=100`, 중형·중간보스 `bombDropWeight` (스테이지당 EV≈1.2~2.3, 후반 raw>3→cap 3)  
- `rewards.json`: `bomb_stock_1` type=bombStock weight=2 maxPerRun=3  
- `waves.json`: `boss_stage1` 탄수 완화 (peak 9.0→4.3 b/s)  
**표:** `Reviews/from-grok/req067-068-bomb-drop-boss1-2026-07-31.md`  
**검증:** `dotnet test` 360/360 · BalanceSim all green · REQ-060 CLEAR · 시드 해시 일치

### CODEX

1. [x] 보상 카탈로그 골든 `Rewards.All.Count` 19→**20** — content가 테스트 1줄 동반 수정 (bomb_stock_1). 리뷰 환영.
2. [ ] (정보) 후반 bomb EV raw>3 — 스톡 cap 3으로 실효 상한. 드롭 스트림 Fork(2) 계약 유지.

### CLAUDE

1. [ ] Resources `enemies.json` / `rewards.json` / `waves.json` 동기화
2. [ ] 폰 플레이: 중간보스 처치 후 폭탄 픽업 확인 · 스테이지1 보스 탄막 체감

---

## 2026-07-30 REQ-063 — 코어 전용 중간보스 `mini_core` (완료 · content)

**완료 (content):** `GameData/enemies.json`에 `mini_core` 추가 (카탈로그 31→**32**).  
**표·TTK:** `Reviews/from-grok/req063-mini-core-2026-07-30.md`  
**검증:** `dotnet test` 360/360 · BalanceSim all green · 시드 해시 2회 일치.

| 항목 | 값 |
|---|---|
| HP | 1550 |
| themeId / stageIndexMin | core / **3** |
| phases | 산탄 → 집중(tel36) → 돌진(tel42) |
| TTK @ core reach ~1050 DPS | **≈1.5s** (패턴 읽기형) |
| mid avgHP / worst | 1290 / walker 1600 @17.8s (게이트 유지) |

### CODEX

1. [x] **`mini_core` 불변식 (REQ-062)** — main 병합으로 수신. content가 mini_core 등록 완료.

### CLAUDE

1. [ ] Resources `enemies.json` 동기화 (`mini_core` + REQ-061 midBoss 5종)
2. [ ] `mini_core` 스프라이트 (미등록 시 폴백+틴트)

---

## 2026-07-30 REQ-061 — 중간보스 행동 패턴 (완료 · content)

**완료 (content):** `mini_*` 전 항목에 `midBoss.phases` (2~3 순환 + telegraph) 채움.  
**표:** `Reviews/from-grok/req061-midboss-patterns-2026-07-30.md`  
**검증:** `dotnet test` 360/360 · BalanceSim all green · REQ-060 CLEAR · 시드 해시 2회 일치.

### CODEX

1. [x] **`mini_core` 추가 시 enemy count 테스트 31→32** — REQ-062 main 병합 + REQ-063 content 등록 완료.

### CLAUDE

1. [ ] Resources `enemies.json` 동기화 (midBoss 프로필 — 현재 5종)
2. [ ] 중간보스 `BossAttackTelegraphed` / `BossPhaseChanged` 시청각 (이미 main 병합분 있으면 확인만)

---

## 2026-07-30 REQ-060 — 첫 스테이지 난이도 (완료 · 잠정 §7)

**완료 (content):** 스테이지1 클리어 가능하도록 초반 화력·중간보스 HP·세그/드롭/첫 보스 조정. 후반 보스·세그 HP 유지.  
**상세 표:** `Reviews/from-grok/req060-stage1-difficulty-2026-07-30.md`

### 핵심 수치

| 항목 | 전 → 후 |
|---|---|
| starter Main | L0 (75 DPS) → **L2 (128.6 DPS)** |
| mini_* HP | 2400–4500 → **800–1600** |
| boss_stage1 | 9000 → **8500** |
| noDropWeight | 15 → **13** |
| seg_sine_rush | elite 앵커 제거 (HP 1052→532) |

### CODEX

1. [x] **중간보스 스테이지/테마 가중 선택** — main 병합으로 수신 (themeId 3× weight + stageIndexMin/Max).
2. [x] **sim `GenerateCore`에 `IsHiddenOnlyColossalBoss` 복원** — main 병합으로 수신.
3. [x] content ← main 병합 후 laser/gimmick 파서 → content 워크트리 `dotnet test` / `BalanceSim` 자체 통과.
4. [x] (기존) `RepositoryApprovedV2Files` 적 수 · 리듬 런 (main 병합 후 360/360).

### CLAUDE

1. [ ] Resources GameData 동기화: `ships.json` · `enemies.json` · `waves.json` (+ REQ-061 midBoss)
2. [ ] 스타터 Main2 시작이 HUD 게이지/툴팁과 맞는지 확인

### 검증

- BalanceSim (merged Core + content data): REQ-060 **CLEAR**, 조립 50/50, 드롭/보스 TTK **PASS**
- 성장 곡선·실드 상한 3: **변경 없음** (제안만 문서)

---

## 2026-07-30 REQ-055 — 스테이지 기믹 데이터 완료

**완료 (content):** `GameData/waves.json` gimmicks/environment/breakable·laser 배치, `enemies.json` `hive_tentacle`, BalanceSim stage-1 잔해 정책.  
**상세 표·여유 계산:** `Reviews/from-grok/req055-stage-gimmicks-2026-07-30.md`

### CODEX

1. [ ] `GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely` 적 카탈로그 기대값 **30 → 31** (`hive_tentacle` 추가)
2. [ ] (선택) `CurrentMiniBossContent` 리듬 런: content GameData는 sim Core에서 기믹 이전에도 RunOver — 고정 봇(y=0) vs 실데이터 괴리 조사. 기믹 단독 원인은 아님.
3. [ ] content ← sim 병합 후 laserEmitter/gimmick 파서가 content Core에 들어와야 content `dotnet test` / BalanceSim이 통과한다.

### CLAUDE

1. [ ] REQ-055 Presentation 계약 (corridor 벽 / drift 시각화 / VisionObscured 구름 / 타임 카운트다운) — `Reviews/from-codex/requests.md` REQ-055 섹션
2. [ ] Resources GameData 동기화 (waves / enemies) after merge
3. [ ] `hive_tentacle` 스프라이트 매핑 (벽 촉수 — 세로로 긴 Static 적)

### 검증 (sim Core + 본 데이터)

- StageGimmickTests + DeterminismAuditSmoke: **PASS**
- 전체 `dotnet test`: **351/353** (위 CODEX 2건)
- BalanceSim: 조립·장애물·보스 TTK **PASS** (무기 v3/colossal 실패는 기존 괴리)

---

## 2026-07-30 REQ-054 후속 — 보스 페이즈 체감 · 중간보스 · 후반/보상 (잠정 §7)

**완료 (content):** `GameData/waves.json` 5보스 phases에 `movementPattern` / `movementAmplitude` / `movementPeriodTicks` / `partVulnerability` 채움 + HP 소폭 인하 + 세그먼트 `mini_*` 스폰 제거(중형 앵커 교체). `enemies.json` noDropWeight 16→15. `rewards.json` capsules/repair weight 2→3.  
**상세 표:** `Reviews/from-grok/req054-boss-phases-2026-07-30.md`

### 핵심 수치

| 보스 | HP | mid TTK | p1 이동 | p2 dens |
|---|---:|---:|---|---:|
| boss_stage1 | 9000 | 18.0s | sine amp1.75/150t | 0.150 |
| boss_hive | 14500 | 24.2s | **amp3.25/96t** | 0.188 |
| boss_fortress | 18000 | 25.0s | amp0.875/210t | 0.214 |
| boss_storm | 22500 | 25.6s | amp2.75/84t | **0.250** |
| boss_core | 28000 | 26.7s | amp2.25/100t | 0.250 |

중간보스 HP 2400–4500 확정. TTK mid 3–13s (상한 30–40s·스테이지 보스보다 짧음). 4종/5스테이지: 전역 풀 균등 + 기대 중복 1회.

### CLAUDE

1. [ ] Resources GameData 동기화 (waves / enemies / rewards)
2. [ ] BossPhaseChanged 이동·파츠 VFX (REQ-054)
3. [ ] MidBoss 전용 HP UI

### CODEX

1. [ ] mini_* 테마 가중 선택 (홈 테마 soft prefer)
2. [ ] reward `selectionKinds` (mid 2택 / main 3택 풀 분리)
3. [ ] (선택) segment `sectionTags` opening/closing

### 검증

- `dotnet test` **297/297**
- BalanceSim **PASS** (EV 10.32)
- DeterminismAudit seed=12345 ×2 hash **`BEB6933375E2C17D`**

---

## 2026-07-30 적 4티어 재배치 (크기=맷집 · 잠정 §7)

**완료 (content):** `GameData/enemies.json` HP·히트박스·점수·드롭 재배치 + `GameData/waves.json` 38세그 티어 리듬·`intent` 한 줄.
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지.
**화력 전제:** CODEX 성장 곡선 너프 예정 → mid DPS 앵커를 기존 analyze 대비 ~15% 낮게 가정 (early~100 / mid~550 / full~1500, 현 god-run 1880 아님).

### 티어 설계 (기존 30종 배치, 신규 적 없음)

| 티어 | 수 | HP | halfW×H (대략) | score | dropW | 체감 TTK |
|---|---:|---|---|---|---|---|
| 잡몹 | 12 | 6–14 | 0.44–0.63 × 0.31–0.56 | 50–120 | 2–4 | early 0.06–0.14s · mid flash |
| 강화형 | 10 | 80–140 | 0.88–0.94 × 0.69–0.88 | 220–380 | 4–6 | early 0.8–1.4s · mid 0.15–0.25s |
| 중형 | 4 | 500–850 | 1.25–1.38 × 1.0–1.25 | 800–1300 | 13–15 | early 5–8.5s · mid 0.9–1.5s · full 0.3–0.6s |
| 중간보스 | 4 | 2400–4500 | 2.25–2.5 × 1.75–1.88 | 3000–5000 | 22–26 | stage mid ~4–8s · full 1.6–3.0s |

**중간보스 스테이지 앵커 (mid DPS 가정):**

| id | HP | 목표 mid DPS | TTK mid | TTK full@1500 |
|---|---:|---:|---:|---:|
| mini_horror | 2400 | ~500 (hive) | 4.8s | 1.6s |
| mini_destroyer | 3000 | ~600 (fortress) | 5.0s | 2.0s |
| mini_crystal | 3600 | ~720 (nebula) | 5.0s | 2.4s |
| mini_walker | 4500 | ~880 (core) | 5.1s | 3.0s |

크기 필드는 이미 `halfWidth`/`halfHeight`로 데이터 조정 가능 (1/256 서브유닛 정합). 스키마 변경 없음. `noDropWeight=16` 유지. 스테이지 캡슐 EV **10.01** (밴드 10–16 하한 근처 — 잡몹 thrift + 대형 보상).

### 웨이브

- 38세그 전부 `intent` 한 줄 (파서가 무시하는 문서 필드).
- 성격 분리: 잡몹-only 러시 / 강화형 조준 / 중형 앵커 / 중간보스 피날레.
- stage avgHP mono: 299 → 880 → 2503 → 3688 → 5135.

### CLAUDE 후속

1. [ ] `Assets/Resources/GameData/enemies.json` · `waves.json` 동기화.
2. [ ] **스프라이트·프리팹 스케일이 새 half extents와 맞는지 확인.** 히트박스는 티어별로 크게 갈라졌는데, Presentation이 고정 스프라이트를 쓰면 시각 크기≠맷집이 된다. `halfWidth`/`halfHeight`에 맞춰 뷰 스케일하거나 티어별 아트 크기를 맞출 것.
3. [ ] `mini_*` 히트박스 상향 (2.0×1.5 → 2.25–2.5 × 1.75–1.88) — 뷰 실측 동기화.

### CODEX 메모

- 크기 데이터 경로 확인 완료: `enemies.json` half extents → Core 파서 → 충돌 AABB. 추가 스키마 불필요.
- 성장 곡선(레벨업 비용) 너프가 들어오면 mid DPS 앵커를 재측정하고 중간보스 HP를 한 번 더 맞출 수 있음. 요청서에 예상 화력 표가 아직 없으면 완료 후 공유 바람.

### 검증

- `dotnet test` CoreStandalone **297/297**
- `Tools/BalanceSim` **PASS** (캡슐 EV 10.01, stage HP mono, 조립 50/50)
- DeterminismAudit `seed=12345 stages=3 ticks=30000` 2회 해시 일치 `535B2CBBCA27CEB7`

재생성 스크립트: `Tools/BalanceSim/_apply_enemy_tiers.py`

---

## 2026-07-30 플레이테스트 수치 3건 (스키마 변경 없음)

**완료 (content):** 기존 필드 값만 조정.

| # | 파일 | 필드 | 전 → 후 |
|---|---|---|---|
| 1 | `waves.json` | `boss_stage1.hp` | 12000 → **9000** |
| 2 | `enemies.json` | `dropTable.noDropWeight` | 12 → **16** |
| 3 | `weapons.json` | missile / families `fireIntervalTicks` (+min) | straight 30/15→**42/20**, bomb 42/28→**54/36**, lance 54/36→**70/44** |

BalanceSim 게이트도 튜토리얼 첫 보스 TTK·미사일 ST 밴드에 맞게 갱신. 뒤쪽 보스 HP는 유지(의도적 튜토리얼→실전 점프).

### CLAUDE 후속 (Resources 동기화)

- [ ] `Assets/Resources/GameData/waves.json` ← `GameData/waves.json`
- [ ] `Assets/Resources/GameData/enemies.json` ← `GameData/enemies.json`
- [ ] `Assets/Resources/GameData/weapons.json` ← `GameData/weapons.json`

### 결정론 감사 메모

같은 시드 2회 해시 일치 확인 (`seed=12345` stages=3, hash `0668DB7675A90266`).  
`--suite` 전체는 히든 콜로설(broodmother 재생) 구간에서 예산 내 `RunCleared` 미도달 — 기존 `seed-max-prefer-capped` 예산 계열 이슈와 동일 계열(해시 불일치 아님). CODEX/DeterminismAudit 예산·타격 모델 쪽 후속.

---

## 2026-07-30 REQ-035 콜로설 보스 content 등록 완료 (잠정 · §7)

**완료 (content):** `GameData/waves.json`에 `boss_leviathan` / `boss_broodmother`
(총 HP 62000, CLAUDE 실측 파츠 좌표 → 1/256 양자화, stage 5–99, theme null).
BalanceSim `CheckColossalBosses` 추가. 전부 잠정 §7.

### 밸런스 검산 요약

| 항목 | Leviathan | Broodmother |
|---|---|---|
| 총 HP / 코어 | 62000 / 25000 | 62000 / 25000 |
| 게이트 경로 HP | shield 10000+core = 35000 | sacs 18000+heart = 43000 |
| TTK @560 DPS (total) | 110.7s ∈ [100,120] | 동일 |
| TTK full-eff @1500 | 41.3s ≥40 | 동일 |
| raw full @1880 | 33.0s (info — 멀티파츠 리타겟 세율로 1500 채택) | 동일 |
| min-path ratio | — | 1.23 ≤1.35 soft |
| 산란 peak@120s | — | 45 ≤ MaxEnemies 128 |

**체감:** 동일 ST 총량/코어. 브루드마더는 게이트 더 두껍고 촉수 재생(20s)+산란 압박으로
스톨 시 불리 → 의도된 증가형. HP 추가 조정 없음.

### Core 최소 변경 (content 브랜치에 포함 — CODEX 인수 요청)

파서/스테이지 범위만으로는 히든 전용을 강제할 수 없다 (`ThemeId=null`은 전 테마 매칭,
전용 theme는 ThemeIds를 오염). 그래서 `SegmentStageGenerator`의 일반 보스 풀에서
`LeviathanBossId`/`BroodmotherBossId`를 제외했다 (`IsHiddenOnlyColossalBoss`).
`GenerateColossalBoss` 경로는 그대로 ID 조회. CODEX가 sim 브랜치에서 소유·회귀 테스트
보강해도 좋다.

### CLAUDE 후속

1. `Assets/Resources/GameData/waves.json` ← `GameData/waves.json` 동기화.
2. 파츠 `PartId` ↔ 스프라이트 슬롯 매핑 (offset는 양자화값 사용).
3. 산란 잡졸 `zako_straight` 풀/연출.

### 검증

`dotnet test` 297/297 · `Tools/BalanceSim` PASS.

---

## 2026-07-30 REQ-033 보스 전면 재설계 완료 + CODEX 권고 (잠정 · §7)

**완료 (content):** `GameData/waves.json` 보스 5종 HP 24000–45000 · 페이즈 3 · aimed/spread/rapid.
BalanceSim `CheckBossRedesign` · `analyze_stage_hp.py` 기대 화력 갱신.
**상세:** `Reviews/from-claude/requests.md` REQ-033 응답.
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지.

### CLAUDE 후속

1. `Assets/Resources/GameData/waves.json` ← `GameData/waves.json` 동기화 (보스 HP·페이즈).
2. 보스 페이즈 전환 VFX/SFX가 `BossPhaseChanged` Arg(0/1/2)를 쓰는지 확인 (3페이즈).

### CODEX 권고 — REQ-G033: 페이즈별 보스 이동 프로파일

**무엇이:** 현재 `BattleSim` 보스는 진입 후 **전 페이즈 단일 사인 호버**만 한다.
REQ-033 데이터는 탄 패턴 성격(조준/확산/고속)만 구분 가능. 40초 전투 체감 분리를 위해
페이즈별 이동 성격이 있으면 좋다.

**제안 (잠정 §7, 구현은 CODEX 재량):**

```csharp
// GameData phase optional fields (ignored today):
// "moveProfile": "hover" | "verticalSweep" | "dash"
public enum BossMoveProfile { Hover = 0, VerticalSweep = 1, Dash = 2 }
// BossPhase or parallel array on StageBossTemplate
```

| 페이즈 | 권고 moveProfile | 의도 |
|---|---|---|
| p0 aimed | hover | 현행 사인 — 조준 읽기 여유 |
| p1 spread | verticalSweep | 세로 왕복 폭 확대 — 탄막 회피 레인 압박 |
| p2 rapid | dash | 짧은 돌진/정지 반복 — 고속탄과 타이밍 겹침 |

결정론·정수 궤적·기존 hover 하위 호환. 필드 부재 시 Hover 폴백.
커스텀 `phaseHpThresholds` 파싱은 **불필요** — 현 equal-N split이 데이터 문서값(2/3, 1/3)과 일치.

### 검증

`dotnet test` 254/254 · `Tools/BalanceSim` PASS.

---

## 2026-07-29 REQ-029 세그먼트 weight · 조우 검산 · 자석 드롭 보정 (잠정 · §7)

**완료:** `GameData/waves.json` 38세그 `weight` · `enemies.json` `noDropWeight` 8→12 · BalanceSim 조우/드롭 검산.  
**상세:** `Reviews/from-grok/encounter-weight-magnet-2026-07-29.md`  
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지.

### CLAUDE 후속

1. `Assets/Resources/GameData/waves.json` ← `GameData/waves.json` 동기화.
2. `Assets/Resources/GameData/enemies.json` ← `GameData/enemies.json` 동기화 (`noDropWeight` 12).

### CODEX / 사람 후속 (권고 · Core config)

- Elite: 보상 2픽 또는 점수× / HP 배수 완화 (보스 비중으로 총 부하 ≈ Normal 0.88×).
- Supply: 라우트 등장 가중 하향 또는 drop×4 → ×2–3 (최적해 위험).
- Rare 12% · Hazard score 3/2: 현행 유지 권고. 상세는 리포트 §2.

### 검증

`dotnet test` 254/254 · `Tools/BalanceSim` PASS.

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
## 2026-07-29 REQ-021/022/023 사람 피드백 데이터 일괄 (잠정 · §7)

**완료:** enemies v3 이동 배정 + ships weaponType/maxHp + waves obstacles + BalanceSim 검증.  
**상태:** 전부 잠정 — 사람 플레이 피드백 전 최종 확정 금지 (AGENTS.md §7).

### (1) enemies.json schemaVersion 2 → **3**

로스터 30종 전원을 nested `movement`로 이관. 신규 패턴 **12종** 배정:

| pattern | count | 배정 |
|---|---:|---|
| dive | 3 | zako_fast, interceptor_rush, sting_hornet |
| dash | 4 | lancer_dart, rift_blade, pipe_rat, rust_skimmer |
| zigzag | 5 | scrap_tumbler, junk_roller, brood_spitter, void_moth, echo_wisp |

- 빠른 소형 → dive/dash, 중형 → zigzag. 터렛/미니보스/탱커는 straight/sine/static 유지.
- 수치는 기존 speed·amplitude를 이식하거나 패턴 파라미터(delay/duration/pause)를 잠정 기입.

### (2) ships.json — 밸런스/스피드/탱커 확정 기조

| id | weaponType | maxHp | move | unlock |
|---|---|---:|---|---:|
| starter | vulcan | **3** | 1/1 | 0 |
| interceptor | laser | **2** | 5/4 (1.25×) | 25000 |
| bulwark | spread | **5** | 4/5 (0.8×) | 50000 (+Shield1 시작 유지) |

### (3) waves.json obstacles

- stage1-capable (`difficultyMin≤1`: intro/sine_pair/sine_rush) = **빈 배열(필드 생략)**
- early mid 2–3 → hive 4 → fortress 5 → nebula 6 → core **7**
- solid = 상·하 통로 기둥, breakable = 파밍(HP 15–50 잠정). 보스 자체 세그먼트 장애물 없음.

### (4) BalanceSim

- movement roster band [8,12] PASS (12/30)
- obstacle corridor + stage1 empty PASS; plan stage1 obstacles=0
- ship single-target DPS: vulcan≈73 / laser≈73 / spread≈108 (ratio 1.47 ≤ 1.75)

### 검증

`Tools/CoreStandalone` `dotnet test` **234/234** · `Tools/BalanceSim` **PASS**.

### CLAUDE 후속

- `Assets/Resources/GameData/{enemies,ships,waves}.json` 동기화.
- 장애물/주무기 계열 뷰 풀은 Core 이벤트·ShipDefinition 소비.

