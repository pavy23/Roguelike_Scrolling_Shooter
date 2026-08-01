# REQ-099 GROK 구현·검증 보고서

- 작업일: 2026-08-01
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 배경: 사람 피드백 — 스테이지1 장애물 배치가 판마다 비슷함 (풀 6종·장애물 3종)
- 결과: **PASS**

## 결론

세그먼트 풀 **38 → 48** (+10). 난이도 1 후보 **6 → 10**, 장애물 포함 **3 → 7**.  
난이도 2–7 각 대역에서 장애물 세그먼트 ≥2종 (d6/d7 신규 3종).  
테마 어휘 유지 (hive 촉수·막 / fortress 차폐·포탑·레이저 / nebula 부유 격자 / core 위상 기둥).

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **485/485** |
| BalanceSim | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** |

---

## 1. Before / After

| 대역 | 이전 segs / 장애물 | 이후 segs / 장애물 |
|---|---:|---:|
| Diff 1 | 6 / **3** | **10 / 7** |
| Diff 2 | 28 / 25 | 34 / 31 |
| Diff 3 | 35 / 33 | 40 / 38 |
| Diff 4 | 31 / 31 | 37 / 37 |
| Diff 5 | 26 / 26 | 31 / 31 |
| Diff 6 | 0 / 0 | **3 / 3** |
| Diff 7 | 0 / 0 | **3 / 3** |

기본 `StageDifficultyCurve` 상한은 여전히 5. d6–7 세그먼트는 고난이도 확장·시드 지터(REQ-098) 대비 대표 배치.

---

## 2. 신규 세그먼트 (+10)

### 스테이지1 scrapyard (breakable only — solid/laser 금지)

| id | d | 배치 성격 | obs | mask |
|---|---|---|---:|---:|
| `seg_scrap_zigzag_posts` | 1–2 | **상하 지그재그 기둥** (x 진행 따라 ±y 교대) | 6b | 7 |
| `seg_scrap_center_breach` | 1–3 | **중앙 파괴벽 뚫기** (x=14.5 세로 벽 5 + 후속 2) | 7b | 7 |
| `seg_scrap_shard_field` | 1–2 | **흩어진 파편밭** (넓은 y 스팬 8점) | 8b | 7 |
| `seg_scrap_rail_split` | 1–2 | **상하 레일 분리** (중앙 통로 강요) | 6b | 2 |

기존 3종(debris_line / pipe_dash / skimmer_weave)과 배치 축이 겹치지 않도록 설계.  
HP 밴드 22–35 (관례 15–50). 스폰에 `scrap_tumbler`×2를 넣어 stage1 풀 가중 평균 HP를 유지 (S1→S2 jump ≈ **3.24×** ≤ 4.0).

### 중후반 테마 변형

| id | theme | d | 배치 성격 | obs 구성 |
|---|---|---|---|---|
| `seg_hive_tentacle_posts` | hive | 2–4 | 촉수 **지그재그 기둥** (edge solid 교대) | 3s+2b (hive max≤5) |
| `seg_hive_membrane_wall` | hive | 3–5 | 유기 **막 벽** 관통 + 상하 solid | 2s+3b |
| `seg_fortress_shield_bastion` | fortress | 2–5 | **차폐 패널** + 포탑/레이저 | 2s+3b+1L |
| `seg_fortress_crossfire_alley` | fortress | 4–7 | 교차사격 골목 (차폐+레이저) | 4s+3b+1L |
| `seg_nebula_drift_lattice` | nebula | 3–7 | **대각 부유 격자** (리본/스톰과 차별) | 4s+3b |
| `seg_core_phase_columns` | core | 4–7 | 위상 기둥 조밀 통로 + 레이저 | 3s+3b+1L |

breakable HP 신규: 22–50. solid HP=0. laserEmitter HP=0.  
solid 동일 x 열 통로 gap ≥1u 자체 검산 통과.  
레이저 템플릿 피크(적 laser_sentry + emitter) ≤4 유지 (`crossfire_alley` peak=3).

---

## 3. 골든 / 게이트 갱신

| 파일 | 변경 |
|---|---|
| `GameData/waves.json` | segments 38→**48** |
| `Tools/BalanceSim/Program.cs` | `ExpectedSegmentCount` 38→**48** · `MinStage1CandidateSegments` 6→**10** |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | Segments.Count 38→**48** (카탈로그 골든) |

---

## 4. 자체 검산 메모

- **stage-1 solid 금지:** 신규 d_min=1 4종 전부 breakable only.
- **shared intro 무장애물:** `seg_intro_line` / `sine_*` 유지.
- **hive density:** theme max obstacles = 5 (게이트 ≤5).
- **core ≥ hive density:** core max 8+ 유지.
- **traversableLaneMasks:** edge solid 교대/스태거는 mask 7; 상하 동시 압박 골목은 mask 2.

---

## 5. 타 에이전트 요청

`Reviews/from-grok/requests.md` REQ-099 절 참고.

### CLAUDE
1. Resources `GameData/waves.json` 동기화 (세그먼트 48)
2. (선택) 신규 장애물 배치 시각 확인 — zigzag / center wall / rail / tentacle pillars

### CODEX
- 골든 `Segments.Count=48` 은 content가 테스트 파일에 반영함 (카탈로그 계약). 추가 Core 변경 없음.
- REQ-098 시드 지터와 병행: 좌표는 대표 배치로 두었음. 지터 진폭이 크면 center_breach 벽 정렬이 풀릴 수 있음 — 관측 후 진폭 상한 조율 권장.

---

## 6. 커밋 대상

- `GameData/waves.json`
- `Tools/BalanceSim/Program.cs`
- `Assets/Tests/EditMode/GameDataParserTests.cs`
- `Reviews/from-grok/req099-report.md`
- `Reviews/from-grok/requests.md`
