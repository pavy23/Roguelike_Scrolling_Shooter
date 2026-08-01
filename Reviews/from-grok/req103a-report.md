# REQ-103a GROK 구현·검증 보고서

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 설계안: `Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md` (main 병합 경로, 사람 승인)
- 결과: **PASS**

## 결론

기존 `waves.json` 스키마만으로 대개편 1차 전면 재설계 완료.

| 축 | 처리 |
|---|---|
| 후반 지형 잠식 | multi-step `traversableLaneMasks` 7→3→2 (테마별 강도 차등, St5 최대) |
| 보스 직전 밸리 | 전 세그먼트 끝 스폰 공백 ≥120틱 (목표 150) — `lengthTicks` 패딩 |
| 4구간 성격 분리 | 전반 오픈 유지 · 후반 static 포대/지형 편향 + intent 스탬프 |
| 스크롤 스파이크 | **스키마 필드 없음** → Core 요구서 C-D |
| Core 신규 필드 | `Reviews/from-grok/req103-core-requests.md` |

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **489/489** |
| BalanceSim | **all green** (REQ-103a 게이트 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** |

---

## 1. 후반 지형 잠식 (7→3→2)

`traversableLaneMasks`는 이미 Core가 순차 체크포인트로 해석한다 (`StagePlanClearability.Advance` + Expand 인접 레인).  
단일 마스크만 쓰이던 후반 세그먼트를 **계단 축소**로 재설계.

| 테마 (스테이지 체감) | 후반 마스크 예 | 강도 |
|---|---|---|
| scrapyard (St1) | `[7,3]` / `[7,3,3]` | 온화 — 센터 단독 락 지양 |
| hive (St2) | `[7,3,2]` | 굴착 통로 |
| fortress (St3) | `[7,3,2]` | 함체 포탑 라인 |
| nebula (St4) | `[7,3,2]` | 낙뢰·회피 초크 |
| core (St5) | `[7,3,2,2]` | **최대 잠식** |

- 마스크 변경: **32** 세그먼트 (수동 보정 포함: `turret_floor`/`ceiling` 티칭 마스크 복원).
- late `difficultyMin≥3` multi-mask: **14/14**.
- core late max-stair (len≥3, ends@2): **3**.
- 조립 clearability: theme assembly 50/50 + REQ-103a seed assemble stage1–5 OK.

의도적 예외 (유지):
- 초반 오픈: `seg_intro_line`, scrap debris/pipe/zigzag/shard/center_breach → `[7]`
- 튜토리얼 초크: `seg_sine_pair`, `seg_scrap_rail_split`, `seg_scrap_skimmer_weave` → 기존 센터 압박 유지
- 바닥/천장 포탑 티칭: `seg_turret_floor` `[6]`, `seg_turret_ceiling` `[3]`

---

## 2. 보스 직전 밸리 (120–180틱)

규칙: `lengthTicks - lastSpawnTick ≥ 120` (부족 시 length를 `last+150`으로 패딩).  
스폰 삭제 없이 길이만 늘려 HP 로드를 보존.

| 지표 | 값 |
|---|---:|
| 밸리 패딩 적용 | 37 segs |
| 잔여 short gap | 0 |
| BalanceSim valley gap≥120 | 48/48 |

마지막 세그먼트가 시드 조립으로 달라져도, **모든 템플릿**이 밸리를 갖도록 해 “보스 직전 공백”을 규칙화.

---

## 3. 4구간 성격 분리 (스폰 테이블)

설계 흐름을 **난이도 대역 + 테마 후반 세그먼트**에 매핑.

| 구간 | 데이터 표현 |
|---|---|
| 전반 오픈 물량/편대 | d_max≤2 · 스웜 유지 · mask open |
| 중간 (연결) | d 2–4 · 온화 multi-mask |
| 후반 지형+고정 포대 | d_min≥3 또는 late-id · static 비율 상향 |
| 보스 직전 | 전 세그 밸리 |

후반 character swap (swarm → theme static), 레이저 피크 게이트 준수:

| theme | static 대체 | 비고 |
|---|---|---|
| scrapyard | `turret_ground` | 방패 Core(C-A) 전 프록시 |
| hive | `hive_tentacle` | 굴착/고정 위협 |
| fortress | (기존 static 충분) | laser 피크 유지 |
| nebula | `phase_disc` | 회피 축 고정점 |
| core | `phase_disc` (일부 `prism_beamer` 상한) | design peak laser sources ≤4 |

character 변경 10 segs → laser 피크 초과분(`seg_core_final_gauntlet`)은 `phase_disc`로 조정 후 **PASS**.

`intent`에 `REQ103a early-open|mid|late-encroach · masks[…] · theme=` 스탬프.

---

## 4. 스크롤 속도 스파이크

| 조사 | 결과 |
|---|---|
| 루트 `scrollSpeed` | 있음 (5.0) |
| 세그먼트/기믹 배율 필드 | **없음** |
| REQ-103a 데이터 적용 | 불가 → Core 요구서 **C-D** |

착수 후 content 계획: St1 scrap 후반·St5 core 전반에 `scrollSpeedMultiplier: 1.5` 짧은 세그먼트 각 1.

---

## 5. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | 잠식 multi-mask · 밸리 패딩 · 후반 static 성격 · intent |
| `Tools/BalanceSim/Program.cs` | `CheckReq103aStageOverhaul` + REQ-098 `CreateSegment(Rng)` 호환 |
| `Tools/_req103a_transform.py` | 재현용 변환 스크립트 |
| `Reviews/from-grok/req103-core-requests.md` | CODEX 신규 필드 요구서 |
| `Reviews/from-grok/req103a-report.md` | 본 보고서 |
| `Reviews/from-grok/requests.md` | 요청 갱신 |

세그먼트 수 **48 유지** → 골든 `Segments.Count=48` / `ExpectedSegmentCount=48` 변경 없음.

---

## 6. BalanceSim REQ-103a 게이트

- 전 세그먼트 boss-valley gap ≥ 120
- late dMin≥3 multi-mask ≥ 2/3
- core late max-stair ≥ 2
- seed `0x103A0E77` stage1–5 조립 clearable + 마지막 세그 밸리

---

## 7. 타 에이전트 요청

상세: `requests.md` REQ-103a 절 · Core 전문: `req103-core-requests.md`

### CODEX (P0–P1)
1. **C-A** `blocksEnemyBullets` — St1 고철 방패
2. **C-B** `regenDelayTicks` — St2 재생 세포벽
3. **C-C** `midbossOutcome` 분기 — 후반 웨이브 테이블
4. **C-D** `scrollSpeedMultiplier` — 세그먼트 스크롤 스파이크
5. **C-E** 섹션 마커 이벤트 — Presentation 배경 전환 (**세그먼트 인덱스만으로는 불충분**)

### CLAUDE
1. Resources `GameData/waves.json` 동기화
2. (Core C-E 후) SectionTheme 전환 구독

### GEMINI
1. 잠식 multi-mask 스테이지 시각·클리어 재검산
2. DeterminismAudit 시드 해시 베이스라인 갱신 여부 확인 (content 변경으로 해시 변동 정상)
