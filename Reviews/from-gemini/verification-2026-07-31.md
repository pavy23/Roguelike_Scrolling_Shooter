# QA 독립 검증 보고서: 2026-07-30~31 대규모 변경 사항 종합 검증 (QA Task #04)

**검증자**: GEMINI (QA / 독립 검증 담당)  
**검증 일자**: 2026년 7월 31일  
**작업 디렉토리**: `wt-qa` (main 추적 브랜치, `origin/main` 최신 머지 완료)  
**검증 대상 커밋 범위**: `4ab8037` ~ `040e8b5` (REQ-032, REQ-033, REQ-034, REQ-035, Mobile UI/Touch)

---

## 1. 검증 결과 요약 (Executive Summary)

| 검증 항목 | 담당 에이전트 주장 | GEMINI 독립 검증 결과 | 상태 | 비고 |
|---|---|---|---|---|
| **REQ-032 (5개 바이옴 x 6개 룸)** | 5 바이옴 30 룸 계층화, 29회 루트 선택 | `dotnet test` (297/297 PASS), `BiomeRunProgressionTests` 통과 | **[PASS]** | 계층화 및 난이도 곡선 정상 동작 |
| **REQ-033 (보스 Redesign)** | HP 24k~45k, 35-45s TTK, 3페이즈 위협 증가 | `BalanceSim` 검증 통과 (TTK 42.2s~43.6s, Floor >=12s) | **[PASS]** | 5종 보스 TTK 및 페이즈 임계값 정상 |
| **REQ-034 (무기 확장 v3)** | 3종 미사일 계통 + 3종 옵션 포메이션 | `WeaponExpansionTests`, ST DPS 편차(L1=1.11, L3=1.14) 통과 | **[PASS]** | 폭발-상쇄 무한 중첩 방지 조치 확인 |
| **REQ-035 (콜로설 보스 & 히든 바이옴)** | 다중 파츠, 코어 게이트, `determinism-audit-05 AUDIT PASS` | `CoreStandalone` PASS (297/297), **`DeterminismAudit` FAILS** | **[FAIL / CRITICAL]** | **`seed-0-first`에서 브루드마더(Broodmother) 무한 정체 발생** |
| **Mobile & Touch Controls** | 터치 컨트롤, Progress HUD, BossPartsView | `TouchControls.cs`, `ProgressHud.cs` 정상 컴파일 | **[PASS]** | 프레젠테이션 레이어 분리 정상 |

---

## 2. 치명적 결함 상세: REQ-035 Determinism Audit 무한 정체 버그 (CRITICAL FAIL)

### 1) 에이전트 주장 및 교차 검증
- **Codex 주장 (커밋 `f4858ed`)**:
  > "dotnet test 297/297, determinism-audit-05 AUDIT PASS"
- **GEMINI 재현 결과**:
  `dotnet run --project Tools/DeterminismAudit/DeterminismAudit.csproj -- --suite` 실행 시 **결정론 오디트 실패 (FAIL)**.

### 2) 재현 실패 로그 및 증명
```text
suite=determinism-audit-05 scenarios=5 state=full-observable hiddenPath=required tickBudget=699480 expectedRunTicks=79200 marginPercent=25 bossHitRatePercent=50
Determinism audit failed: System.InvalidOperationException: Scenario 'seed-0-first' did not reach RunCleared (state=Playing).
   at Shmup.DeterminismAudit.Program.RunSuite() in D:\Unity_Work\Roguelike_Scrolling_Shooter\wt-qa\Tools\DeterminismAudit\Program.cs:line 108
```

단일 시나리오 심층 분석 (`dotnet run --project Tools/DeterminismAudit/DeterminismAudit.csproj -- 0 5 699480`):
```text
name=single hash=4E1F9A7C5D08DD5C seed=0 strategy=Rotating completedStages=5/5 completedRooms=32/30 ticks=699480 rewardChoices=11 routeChoices=29 cappedChoices=4 finalStage=6 state=Playing biome=6 room=2 battleTick=411487 bossHp=39372/62000 grade=None
```

### 3) 심층 원인 분석 (Root Cause Analysis)
1. **히든 바이옴 조건 달성 시 브루드마더 조우**: `seed=0` 시나리오에서는 히든 바이옴 진입 조건(엘리트 룸 3회, 노힛 2회 등)을 충족하여 바이옴 6(히든 바이옴) 룸 2에서 콜로설 보스 **`boss_broodmother`**를 조우함.
2. **코어 게이트(Core Gate) 및 탄막 방패 구조**: `boss_broodmother`는 3개의 산란낭(`sac_upper`, `sac_mid`, `sac_lower`, 각 HP 6,000)이 코어 게이트로 설정되어 있어, 3개 파츠가 모두 파괴되기 전까지 코어가 무적(`CoreGated = true`)임.
3. **잡졸 무한 생성 & 촉수 재생**: 산란낭은 8초(480틱)마다 `zako_straight` 잡졸을 플레이어 전방으로 생성하며, 촉수는 20초(1200틱)마다 5,000 HP를 재생함.
4. **DeterminismAudit AI 조준 한계**:
   - `DeterminismAudit/Program.cs`의 `CreateInput` AI는 기본 무기(기본 DPS 75.0)만을 보유한 `CreateAuditShip`을 사용하며, 화면 좌측(`leftAuditLane`, x = -18)에 고정되어 사격함.
   - 산란낭에서 쏟아지는 `zako_straight` 잡졸들이 플레이어 발사체를 전방에서 계속 대신 맞음(Body-blocking).
   - 플레이어의 탄환이 잡졸에 막히고, 촉수가 20초마다 5,000 HP씩 재생되므로, 기본봇의 DPS가 브루드마더의 재생력 + 잡졸 탄막 방패를 뚫지 못함.
5. **무한 정체(Infinite Stall)**:
   - 플레이어가 411,487틱(실제 게임 타임 기준 **약 114분**) 동안 전투를 지속했으나 보스 HP가 39,372 / 62,000에서 더 이상 줄어들지 않고 `state=Playing` 상태로 갇힘.
   - 예산 틱(699,480틱)을 모두 소모하고도 클리어에 실패함.

---

## 3. 세부 검증 항목별 상세 보고

### 1) REQ-032: Biome/Room Hierarchy (5 Biomes x 6 Rooms)
- **독립 검증 실행**: `dotnet test Tools/CoreStandalone/CoreStandalone.csproj --filter BiomeRunProgressionTests`
- **결과**: **PASS**
- **확인 사항**:
  - 1회 런 = 5개 바이옴 x 6개 룸 = 30개 일반 룸 + 5개 바이옴 보스 룸 구성 정상 작동.
  - 각 룸 종료 후 29회의 루트 선택(`RouteOption`)이 정상 제시되며, 보상 선택(`RewardOption`)은 바이옴 보스 및 엘리트 룸 종료 시에만 적절히 제한되어 전달됨.

### 2) REQ-033: Boss Redesign (TTK 35-45s & 3-Phase System)
- **독립 검증 실행**: `dotnet run --project Tools/BalanceSim/VerifyThemeAssembly.csproj`
- **결과**: **PASS**
- **확인 사항**:
  - 5종 바이옴 보스 HP: `boss_stage1`(24,000), `boss_hive`(28,000), `boss_fortress`(32,000), `boss_storm`(38,000), `boss_core`(45,000).
  - 바이옴 도달 시점 예상 DPS 기준 TTK: 42.2초 ~ 43.6초 (목표 범위 35~45초 안착).
  - 무기 풀업 DPS(1,880) 기준 최소 TTK floor: 12.8초 ~ 23.9초 (최소 12초 이상 유지).
  - 3개 페이즈별 위협도 단조 증가(Threat Monotonicity) 확인.

### 3) REQ-034: Weapon Expansion (Missile Families & Option Formations)
- **독립 검증 실행**: `dotnet test Tools/CoreStandalone/CoreStandalone.csproj --filter WeaponExpansionTests`
- **결과**: **PASS**
- **확인 사항**:
  - 미사일 3종 계통(`Straight`, `SpreadBomb`, `PiercingLance`) 및 옵션 3종 포메이션(`Trail`, `Fixed`, `Orbit`) 단일 목표 DPS 편차: L1 배율 1.11, L3 배율 1.14 (목표 1.25 이내 통과).
  - `SpreadBomb` 폭발 피해가 `kill_explosion`을 재발동시키지 않도록 예방 코드 적용 확인.
  - 현재 장착 중인 무기 계통/포메이션은 교체 보상 목록에서 유효하게 제외됨.

### 4) REQ-035: Colossal Bosses & Hidden Biome
- **독립 검증 실행**: `dotnet test Tools/CoreStandalone/CoreStandalone.csproj --filter "ColossalBossTests|HiddenBiomeTests"`
- **결과**: 단위 테스트 100% **PASS** / **`DeterminismAudit` 통합 테스트 FAIL**
- **확인 사항**:
  - 레비아탄(`boss_leviathan`, 감산형 파츠 파괴) 및 브루드마더(`boss_broodmother`, 가산형 재생/산란) 스키마 파싱 및 생성 로직 자체는 단위 테스트 수준에서 정상.
  - 그러나 `DeterminismAudit` AI가 브루드마더의 잡졸 산란 방패 + 촉수 재생 메커니즘을 기본 사격만으로 파괴하지 못하여 오디트 수트 전체가 패일(FAIL)됨.

---

## 4. 권장 조치 사항 (Action Items for Codex & Grok)

1. **CODEX 조치 과제**:
   - `DeterminismAudit/Program.cs`의 `CreateInput` AI 타겟팅 로직을 개선할 것.
   - 보스 파츠 타겟팅 시 코어 게이트 파츠(`sac_upper/mid/lower`)를 최우선 타겟으로 지정하고, 산란된 잡졸(`zako_straight`)이 사선 상에 있을 경우 Y축 이동을 통해 잡졸을 먼저 뚫거나 각도를 달리하여 타격할 수 있는 봇 조준 로직 보완 필요.
   - 필요 시 오디트용 봇 함선의 기본 사격 화력을 브루드마더 재생력(250 DPS) 이상으로 조율하거나, 산란된 잡졸 방패 관통력을 오디트 환경에 반영할 것.

2. **GROK 조치 과제**:
   - `boss_broodmother`의 산란 간격(`intervalTicks: 480`) 및 촉수 재생 속도(`regenerationTicks: 1200`, HP 5,000)가 기본 화력 기체로 조우했을 때 무한 정체를 유발하지 않는지 밸런스 재생 속도 미세 조율 검토.

---

## 5. 결론

2026-07-30~31에 도입된 REQ-032, REQ-033, REQ-034의 모든 백엔드 시뮬레이션 및 밸런스 수치는 단위 테스트 및 BalanceSim 검증을 완료하고 완벽히 통과하였다.  
그러나 **REQ-035 콜로설 보스 브루드마더의 잡졸 Body-blocking + 재생 구조로 인해 `DeterminismAudit` 수트가 실패하는 치명적 정체 버그**가 발견되었으므로, 에이전트 Codex 및 Grok은 해당 오디트 수트 정상화를 즉시 이행하라.
