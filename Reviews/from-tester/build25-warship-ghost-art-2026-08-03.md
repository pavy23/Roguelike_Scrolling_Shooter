# build25 전함 실전 통합 + 고스트 + 새 아트 + 컨티뉴 뱃지 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build25, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760, 키보드는 `page.keyboard.down/up`(홀드) + `press`(탭)
- 시간 예산: 코디네이터 중간 점검(1시간 13분 시점) 지시에 따라 전함 항목은 라이브 완주 대신 **막별 부분 검증으로 전환**하고 마감함. 완주 검증은 다음 빌드 테스터에게 인계.
- 스크립트/스크린샷: `...\scratchpad\b25_*.js` 일체, 스크린샷은 `...\scratchpad\b25art\`, `b25regress\`, `b25seedsearch2\`, `b25warshiprt\`, `b25wfA\`, `b25warshipA\`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 전함 3막 (fortress 보스룸) | **PARTIAL** — 코드 레벨 설계 전량 확인(3막 상태머신·함미전용 피격·포탑수→코어 way 공식·dev 오버레이 포맷), 라이브는 Boss 섹션 진입까지는 반복 재현했으나 함미/포탑/코어 교전 자체를 확정 짓지 못함(아래 상세) |
| 2 | 타임루프 고스트 | **SKIP** — 코드 레벨로 트리거 조건·배너 텍스트·비주얼 스펙 전량 확인. 라이브 St1 기록 + St5 Closing 도달은 시간 예산상 시도하지 못함 (원 지시문이 허용한 대체 경로) |
| 3 | 새 배경 아트 (5테마) | **PARTIAL** — scrapyard/fortress/nebula/core 4테마는 신규 아트 정상 로드(**PASS**). **hive 테마가 nebula 배경 아트를 그대로 표시하는 버그 발견(FAIL)** — 근거 첨부 |
| 4 | 크로스페이드 | **PASS** — F7 미리보기 전환 연속 캡처로 실제 디졸브(팝 아님) 확인 |
| 5 | 회귀 (일반 런 + 랭킹 모달 + 콘솔 에러) | **PASS** — 일반 런 정상 종료, 콘솔 에러 0건. 랭킹 모달 헤더/정렬 정상. 컨티뉴 C 마커는 **코드 확인 PASS**이나 라이브 보드에 컨티뉴 사용 기록이 없어 실측 스샷은 확보 못함(캐비아트) |

**최우선 발견 2건**: (a) **HIVE 테마가 NEBULA 배경 아트를 잘못 로드**하는 버그 — 파일은 디스크에 정상 존재(MD5 다름, 육안으로도 확연히 다른 그림)인데 인게임에는 nebula 그림이 뜬다. (b) 전함(warship) 조우를 stage3 보스룸에서 반복 시도했지만, **dev 오버레이의 `warship ...` 진단 세그먼트가 단 한 번도 뜨지 않았고**, 5분 이상 정지 사격에도 보스 체력바가 전혀 줄지 않았다 — 실기 검증 방법론 한계(사거리/포지셔닝)일 수도, 실제 통합 갭일 수도 있어 다음 빌드에서 재확인이 필요하다.

---

## 1. 전함 3막 — PARTIAL

### 1-A. 코드 레벨 확인 (전량 확정)

`Assets/Scripts/Core/Simulation/WarshipEncounter.cs`, `Assets/Scripts/Core/Generation/WarshipEncounterDefinition.cs`, `GameData/waves.json:8785-8922`(`boss_fortress`), `Assets/Scripts/Presentation/Battle/{WarshipView.cs,DevCheats.cs}`:

- **3막 구조**: group0 `stern`(role `MidbossGate`, part `engine`, HP 2200) → group1 `hull`(role `AttritionLine`, `turret_a/b/c/d` 4문, 각 900HP) → group2 `bow`(role `FinalCore`, part `core`, HP 13800). 합계 19600 = waves.json `boss_fortress.hp`와 일치.
- **함미전용 피격**: `WarshipEncounter.cs:411-417` `ApplyDamageCore`가 `_partGroups[partIndex] != _activeGroupIndex`면 데미지를 하드 게이트로 무시. `WarshipPartState.Invulnerable => !Active || Destroyed`(line 57). 워닝 페이즈(`_activeGroupIndex==-1`, 180틱=3초)엔 전 파츠 무적.
- **포탑 라인은 시간 게이트**: 포탑 4문을 다 죽여도 코어가 조기 개방되지 않는다 — `hull` 그룹은 `AdvanceAfterTicks=720`(12초) 타이머로만 다음 그룹 활성화(`WarshipEncounter.cs:281-288`). 포탑 파괴 수는 대신 **코어 개막 탄막 way 수**만 바꾼다.
- **코어 개막 way 공식**(`WarshipEncounter.cs:164-176`, `CoreOpeningWays`): `max(3, 9 - 2×파괴수)` → 0파괴=9way, 1=7, 2=5, 3=3(바닥), 4=3(클램프).
- **dev 오버레이 포맷**(`DevCheats.cs:156-158`): `   warship {그룹id} {그룹idx+1}/{그룹수}   turret {생존}/{총4}` — `_warship.Active`(=`WarshipView.Active`, 씬에 직렬화된 단일 참조)일 때만 나타남.

### 1-B. 라이브 검증 — 반복 재현했지만 확정 못함

**시드 확보**: `?dev=1&stage=3&god=1` + 타이틀 수동 시드 입력으로 탐색, **seed=2**가 stage3=fortress(성벽·감시탑·레이더 접시 실루엣, `fort_far_dark.png`와 일치)임을 F7 프리뷰로 확인(`b25seedsearch2/seed2_late.png`).

**진행 방법론 전환**: 처음엔 F11(+10초 스킵)로 구간을 건너뛰려 했으나, **F11은 `InputCommand.None`만 먹여 발사가 전혀 안 되므로 킬 게이트가 있는 구간(미니보스방, 보스방 전초)을 절대 못 뚫는다** — 100회 이상 스팸해도 tick만 수만 틱 증가할 뿐 구간이 안 바뀜(`b25warshipcalib/013_boss_room_set11.png`, tick 81998). 이후 **F11을 전혀 안 쓰고 실전투(상하 스윕 + F9/F10 게이지 전량 펌프)로만 진행**하는 방식으로 전환하니 Opening→MidBoss→Closing→Boss 섹션까지 3~4분 안에 안정적으로 도달함(`b25wfA/006_pre_boss_check.png`, `sect Late/live` → `STAGE 3/5 ADVANCE > boss`).

**Boss 섹션 진입 후**: 큰 체력바를 가진 적 1기와 조우(`b25warshiprt/025_cycle23.png`) — 스프라이트가 `art-input/boss_fortress.png`(빨간 코어+어깨 캐논 메카 아이콘)와 일치해 이것이 boss_fortress 조우라는 심증은 있음. 그러나:

- **dev 오버레이에 `warship ...` 세그먼트가 단 한 번도 나타나지 않았다.** `DevCheats.cs:129` `warshipOn = _warship != null && _warship.Active` — 코드상 이 조건이 참이어야 세그먼트가 뜨는데, Boss 섹션 진입 후 다수의 독립 세션·수십 분에 걸쳐 한 번도 관측되지 않음.
- 정중앙(오프셋 y=0, 함미/코어 예상 높이)에 배 조작을 완전히 정지시키고 **5분(300초) 동안 정지 사격**했지만, 체력바 픽셀 위치·점수(`01072173`)가 **정확히 동일하게 유지**됨(`b25wfA/038_stern_phase_result.png` tick17520 vs `b25wfA/058_core_still_t13.png` tick26850, 그 사이 183초간 무변화).
- 이 결과를 코드와 대조하면 두 가지 가능성이 남는다: **(a) 테스트 방법론 한계** — 리센터링이 부정확해(상하 스윕의 왕복 캘리브레이션이 일반 스테이지에서 잰 것이라 보스룸 클램프 경계와 다를 수 있음) 배가 유효 사거리 밖(화면 밖 상단 등)에 고정됐을 가능성, 또는 `waves.json`의 `holdX=12, originX=24`(월드 단위)가 실제 가시 영역보다 멀어 함선 본체가 화면 밖에 판정만 존재할 가능성. **(b) 실제 통합 갭** — 이 dev-stage 진입 경로에서 `WarshipView`가 정상적으로 활성화되지 않는 문제.
- 이 둘을 가르는 결정적 데이터(정상 진행 시 `warship ...` 텍스트가 뜨는 순간의 스샷)를 확보하지 못한 채 시간 예산이 종료됨.

**결론**: 함미전용 피격·포탑수→way 공식·격파 전환 연출은 **코드로는 확정**되지만 **라이브로는 확인도 반증도 못함**. 다음 빌드 테스터는 (1) `?dev=1&stage=3&god=1` 진입 후 Boss 섹션에서 F3로 오버레이를 계속 띄운 채 상하 이동 폭을 훨씬 좁게(예: 0.3~0.5초 홀드) 잡고 화면 중앙 근처를 세밀하게 훑거나, (2) 아예 `--stage` 오버라이드 없이 정상 진행으로 stage3에 도달해 같은 현상이 재현되는지 대조하는 것을 권한다.

**스샷**: `b25seedsearch2/seed2_late.png`(시드 확인), `b25wfA/002~007`(진입 경로), `b25warshiprt/025_cycle23.png`(적 스프라이트, HP바), `b25wfA/038,058`(5분 무변화 대조), `art-input/boss_fortress.png`(스프라이트 원본 대조).

---

## 2. 타임루프 고스트 — SKIP (코드 레벨 확인만)

원 지시문이 명시적으로 허용한 대체 경로("도달이 어려우면 dev 오버레이 ghost:rec 표시 확인 + 코드 수준 SKIP 처리 가능")를 이번엔 그 확인 절차조차 실행할 시간 예산이 없어 **코드 레벨로만** 정리한다.

`Assets/Scripts/Core/Simulation/RunManager.cs:6308-6378`, `Assets/Scripts/Presentation/Battle/{GhostView.cs,ProgressHud.cs,UiText.cs}`:

- **기록**: `BiomeIndex==1`(St1) 전체 구간 자동 기록. God모드 여부와 무관하게 기록됨(단, God/시드고정 등 dev 플래그는 별개로 `CheatUsed`를 세워 스코어 제출만 막음).
- **트리거**: `BiomeIndex==FinalStageIndex(=5)`이고 `StageSection==Closing`일 때 `HasStageOneGhostRecording`이면 발동. 배너 텍스트는 정확히 `"PAST SELF JOINS"`(`UiText.GhostJoinBanner`, `UiText.cs:144`).
- **dev 오버레이 태그**: `ghost:rec`(St1 기록 보유, 아직 미발동) / `ghost:live`(St5 Closing에서 실제 재생 중) — `DevCheats.cs:153`.
- **비주얼 스펙**: 시안 틴트 `(0.42,0.92,1,1)`, 바디 알파 `0.45`, 0.2초 간격 3스텝 잔상 트레일, 페이드인 0.3초/아웃 0.6초(`GhostView.cs`).
- **주의**: `?stage=N` 오버라이드로 St1을 건너뛰면 기록 자체가 생기지 않으므로, 이 기능 확인은 **반드시 St1부터 정상 진행**해야 한다 — 다음 빌드에서 최우선 소요 시간을 이쪽에 배정할 것을 권한다.

---

## 3. 새 배경 아트 — PARTIAL (4 PASS / 1 FAIL)

`?dev=1&stage=1..5&god=1`로 5개 런을 순회하며 F7(Early→MidBoss→Late→Boss)로 각 테마 전 구간 프리뷰를 캡처(`b25art/`).

### PASS — scrapyard(St1) / fortress(St3) / nebula(St4) / core(St5)

- **Scrapyard**: 주황 녹슨 팔레트, 기중기·좌초선 실루엣(`b25art/stage1_3_late_settled.png`) — `scrap_far_dark.png` 원본과 일치 확인.
- **Fortress**: 성벽·감시탑·레이더(`b25art/stage3_3_late_settled.png`) — `fort_far_dark.png`와 일치.
- **Core**: 청록 회로 도시 + 거대 에너지 코어(`b25art/stage5_3_late_settled.png`) — `core_far_dark.png`와 일치, 5테마 중 가장 인상적인 랜드마크.
- (Nebula는 아래 버그의 "정답" 참조 이미지로 재사용됨 — 원본과 일치.)

### FAIL — hive(St2)가 nebula 아트를 그대로 표시

`b25art/stage2_4_boss.png`와 `b25art/stage4_4_boss.png`(nebula)를 나란히 보면 **구름 모양·색·별 배치까지 완전히 동일**하다. `art-input/` 원본 파일을 직접 대조:

| 슬롯 | MD5 (다름 확인) | 실제 그림 |
|---|---|---|
| `hive_far_dark.png` | `378CFDFF...` | **초록 포자 첨탑 + 발광 눈알 실루엣** (전혀 다른 그림) |
| `nebula_far_dark.png` | `CA0261B3...` | 청록 성운 구름 (인게임 St2·St4 모두 이 그림이 뜸) |
| `hive_landmark.png` | 존재 | 초록 촉수 알 생명체 랜드마크 — 인게임에서 St2 어디서도 목격 못함 |

디스크의 hive 전용 파일(`hive_far_dusk/dark.png`, `hive_fg.png`, `hive_landmark.png`) 4종은 모두 존재하고 서로 다른 유효한 그림인데, **실제 빌드에서는 로드되지 않고 nebula 슬롯이 대신 나온다.**

**추정 원인**: `Assets/Editor/BattleSceneBuilder.cs:1725-1744` `CreateSectionArtSlots`는 씬 빌드(에디터 타임) 시점에 `LoadExternalSprite($"{prefix}_{suffix}.png", key)`가 null이면 그 키를 통째로 건너뛴다(line 1737 `if (sprite==null) continue;`). **build25의 씬을 굽던 시점에 `hive_*` 4개 파일이 아직 `art-input/`에 없었거나 로드 실패했을 가능성**이 높다 — 이 경우 `hive_*` 슬롯 키 자체가 `SectionThemeTable`에 등록 안 됐고, 런타임에서 키 미스 시 폴백이 다른(인접했거나 마지막에 성공한) 테마의 스프라이트를 재사용했을 것으로 추정된다. **씬 재빌드(BattleSceneBuilder 재실행) 후 재검증을 권한다.**

**스샷**: `b25art/stage2_1_early.png`(St2 Early — 초록기가 전혀 없는 보라 구름), `b25art/stage2_3_late_settled.png`, `b25art/stage2_4_boss.png` vs `b25art/stage4_4_boss.png`(픽셀까지 동일), `art-input/hive_far_dark.png` vs `art-input/nebula_far_dark.png`(원본은 확연히 다름).

---

## 4. 크로스페이드 — PASS

F7 프리뷰 전환 순간 180ms 간격으로 9프레임 연속 캡처(`b25art/stage1_3_late_xfade_f0~f8.png`). f0(디졸브 시작, 여전히 이전 dusk 톤 지배)→f2(`sect` 라벨은 이미 Late로 바뀌었으나 시각은 과도기)→f5(전경 실루엣 크레인/좌초선이 점점 또렷해지며 새 레이어가 강해짐)로 이어지는 **점진적 알파 블렌드**를 확인, 순간적인 "팝(교체)"은 관측되지 않음. 코드 확인(`SectionThemeDirector.cs:139-534`)과 일치 — 유령 렌더러가 구 스프라이트를 한 order 위에서 페이드아웃하는 동안 신규 스프라이트가 아래서 즉시 풀 오파시티로 노출되는 구조.

---

## 5. 회귀 — PASS

- **일반 런**: dev 플래그 없이(`?v=...`만) 순수 타이틀 → LAUNCH → 실전투 → 자연사까지 진행(`b25regress/`). GAME OVER 화면 정상 렌더(`SCORE 00004480 (run 1, stage 1)`, `KILLS 19 CAPSULES 6 ACC 29.8% SHOTS 208`, REDEPLOY/TITLE/SUBMIT SCORE 버튼 정상 — `b25regress/014_run_end_state.png`).
- **콘솔 에러**: 이번 세션 전체(회귀 런 + 전함 라이브 세션들 다수) 콘솔 로그에서 `[error]`/`[pageerror]` **0건**.
- **랭킹 모달**: RANKING 버튼 클릭 → `DAILY RANKING` 모달 정상 오픈, 헤더 `# PILOT SCORE STG SHIP BOMB HIT` 정렬 정상, 실제 서버 데이터 1건(`1 TESTERDAIL 860 1-2 ST 0 3`) 정상 렌더, CLOSE로 정상 닫힘(`b25regress/003_ranking_modal_settled.png`).
- **컨티뉴 C 마커**: 코드 확인(`TitleScreen.cs:340-355` `AppendPilotCell`, `entry.HasContinues`(`cu>0`)일 때만 `" C{n}"`(dim gray) 부착, 새 컬럼이 아니라 PILOT 셀 내 마커) — 로직 자체는 build24 검증 리포트와 동일 계보로 안정적. 다만 **이번 세션에 가져온 라이브 보드 표본(1건)엔 컨티뉴 사용 기록이 없어**, " C{n}"이 실제 화면에 찍히는 걸 육안으로 확인하지는 못함 — 코드 PASS + 실측 캐비아트로 기록.

---

## 참고: 사용한 스크립트/자료

- `b25_seed_search2.js` — F7 프리뷰만으로 테마 확인(빠름, 방 진행 불필요)
- `b25_warship_navigate.js`, `b25_warship_full.js`, `b25_warship_calib.js`, `b25_warship_realtime.js`, `b25_warship_final.js` — 전함 접근 방법론 반복 개선 과정(F11 무력 확인 → 실전투 전환)
- `b25_art_xfade.js` — 5테마 F7 순회 + 크로스페이드 연속 캡처
- `b25_regression.js` — 일반 런 + 랭킹 모달 + 콘솔 에러
- `art-input/*.png` — 신규 배경 아트 원본 대조 (hive/nebula MD5 비교에 사용)
