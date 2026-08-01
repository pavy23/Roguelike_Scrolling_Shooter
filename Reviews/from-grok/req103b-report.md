# REQ-103b GROK 구현·검증 보고서

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-101 (Core 기믹 축, CODEX) · REQ-103a (기존 스키마 대개편 1차)
- 결과: **PASS**

## 결론

REQ-101 optional 필드로 스테이지 시그니처 기믹 데이터를 채웠다.

| 축 | 처리 |
|---|---|
| St1 고철 방패 | scrapyard breakable 일부 `blocksEnemyBullets: true` + 후반 cover-line 재배치 |
| St2 재생 세포벽 | hive 후반 breakable `regenDelayTicks` 180–270 + dig-center 통로 유지 |
| midbossOutcome 분기 | 테마별 cleanKill 세그 2종(총 10) · Default 풀 유지 |
| 스크롤 스파이크 | scrap 후반·core 전반 `scrollSpeedMultiplier: 1.5` 단세그 각 1 |
| St3/St4 밀도 | fortress 함체 포탑 +1쌍 · nebula phase_disc +1 (mild) |
| St5 시간압 여유 | core `timeLimitTicks` 9000→**12000** (기믹 1.5배 하 closing 7세그 소프트락 방지) |

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **499/499** |
| BalanceSim | **all green** (`CheckReq103bGimmickAxes` 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** |

---

## 1. St1 고철 방패 (`blocksEnemyBullets`)

- **전반 티칭**: debris/pipe/zigzag/shard/center_breach/skimmer/rail — breakable 약 절반에 플래그 (쏘면 엄폐 소멸 학습).
- **후반 엄폐 라인**: junk_corridor / tumbler_pack / rust_gauntlet — mid-lane (`|y|≤4`) breakable에 플래그.
- **gauntlet 재배치**: rust_gauntlet breakable를 세로 엄폐 기둥(y≈±2, 0) 열로 정리 — 가장자리 터렛(±5.5)에 대해 뒤에 숨을 각도 확보.
- 비-cover 잔해는 dig/farm 보상용으로 일부 유지.

Audit: scrap cover segs≥6 · cover obs≥20 · mid-lane posts≥8.

---

## 2. St2 재생 세포벽 (`regenDelayTicks`)

| 세그먼트 | delay | 비고 |
|---|---:|---|
| `seg_hive_membrane_wall` | 240 | 시그니처 굴착 막 — 중앙 breakable 열 |
| `seg_hive_organic_pulse` | 210 | 후반 |
| `seg_hive_nest_choke` | 180 | 후반 |
| `seg_hive_hornet_dive` | 270 | 중후반 티칭 |

- hive **MaxObstacles/seg ≤5** 게이트 유지 — 기존 breakable에 필드만 부여 (장애물 수 증가 없음).
- membrane 중앙 solid 봉쇄 없음 · 중앙 breakable dig path 유지 (재생 후 다시 파고 통과 가능 → 소프트락 방지).
- clearability = 레인 마스크 + 솔리드 복도 갭 (기존) + 재생 벽은 breakable이라 파괴 구간 통과.

---

## 3. midbossOutcome 분기 (`postMidbossOutcomes`)

| outcome | 데이터 |
|---|---|
| **Default / Attrition** | 기존 untagged 세그먼트 (폴백 포함) |
| **CleanKill** | 테마당 2개 변형 세그 (총 10) |

CleanKill 정책 (과보상 금지):
- 스폰 약 18% 감소 (후반 fodder 우선 삭제)
- 잔여 스폰 일부 고 dropWeight 적으로 승격 (캡슐 밀도 소폭↑, 총 EV는 default 대비 과보상 게이트)
- `difficultyMin/Max = 3..5` (stage-1 solid 금지 · early HP 풀 오염 방지)
- 저난이도에서는 Core가 Default 풀로 폴백 (정상)

BalanceSim:
- cleanKill 테마별 ≥1 · closing 조립 tagged 100% @diff 3/5
- EV: avgSpawns cleanKill < default · total capsule EV ≤ default×1.05

---

## 4. 스크롤 스파이크 (`scrollSpeedMultiplier` 3/2)

| id | theme | d | length | mult |
|---|---|---|---:|---:|
| `seg_scrap_speed_spike` | scrapyard | 3–5 | 280 | 1.5 |
| `seg_core_speed_spike` | core | 2–4 | 280 | 1.5 |

짧게 유지 (≤400틱 게이트). scrap 스파이크 장애물에도 cover 플래그.

---

## 5. St3 함체 포탑 · St4 낙뢰 (mild)

| 테마 | 조정 |
|---|---|
| fortress | turret_cross / drone_lattice / armored_gate / crossfire_alley에 **터렛 쌍 +1** |
| nebula | crystal / prism / drift / void_moth에 **phase_disc +1** |

1차 과도 밀도는 DeterminismAudit Rotating 시드에서 방 길이·계약 선택(틱 기반)을 흔들어 core 시간제한 소프트락을 유발해 **mild로 축소**.

---

## 6. core timeLimit 여유 (감사 필수)

| 항목 | 값 |
|---|---:|
| 이전 | 9000 |
| 이후 | **12000** |
| ×1.5 기믹 스케일 (×2/3) | 8000 |
| worst closing 추정 (maxLen×7) | ≤6790 |

근거: `lockdown_zone` 등 `gimmickIntensityMultiplier: 1.5` 계약이 timeLimit을 2/3로 줄인다. closing 7세그 합이 6000을 넘으면 보스 전 구간에서 `TimeLimitExpired` 즉사. 시간압 기믹은 유지하되 여유만 확보.

---

## 7. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | 기믹 필드 · cleanKill 10 · spike 2 · fort/neb mild · core TL |
| `Tools/BalanceSim/Program.cs` | `ExpectedSegmentCount` 48→**60** · `CheckReq103bGimmickAxes` |
| `Assets/Tests/EditMode/GameDataParserTests.cs` | Segments.Count **60** 골든 |
| `Tools/_req103b_transform.py` | 재현 변환 스크립트 |
| `Reviews/from-grok/req103b-report.md` | 본 보고서 |
| `Reviews/from-grok/requests.md` | 요청 갱신 |

세그먼트 수 **48→60** (+10 cleanKill +2 spike).

---

## 8. BalanceSim REQ-103b 게이트 요약

- scrap cover / hive regen / mid-lane cover geometry
- hive late dig path (center breakable)
- scroll 3/2 short spikes (scrap late + core early)
- cleanKill per-theme + tagged closing assemble @d3/5
- cleanKill vs default spawn/EV 게이트
- fortress turret · nebula phase mild 하한
- core timeLimit ≥12000 · scaled ≥ worst closing

---

## 9. 타 에이전트 요청

### CLAUDE
1. Resources `GameData/waves.json` 동기화 (60 segs · 신규 필드)
2. (선택) `EnemyBulletBlocked` / `ObstacleRegenerated` / `MidBossDefeated` 연출 구독
3. (선택) scrap cover · hive regen · speed-spike 구간 시각 확인

### CODEX
- 없음 (REQ-101 스키마 완료). content는 optional 필드만 사용.

### GEMINI
1. cleanKill 분기 후반 체감·clearability 교차 검산
2. DeterminismAudit 해시 변동은 content 의도 변경 — 베이스라인 갱신 여부 판단
3. core timeLimit 12000 체감 (기믹 1.5 계약 경로)
