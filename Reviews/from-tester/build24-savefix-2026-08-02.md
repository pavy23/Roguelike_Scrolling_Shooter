# build24 WebGL 저장 syncfs + 컨티뉴 복제 봉인 + 해금 가격 50k/100k 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build24, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 방법론: `userDataDir`를 고정 프로필(`b24profile`)로 지정하고, 각 세션마다 `browser.close()` 호출 후 **Node에서 `process.kill(pid, 0)`으로 브라우저 프로세스가 완전히 종료됐음을 폴링 확인**한 다음, 완전히 새로운 Node 프로세스(별도 스크립트 실행)에서 같은 프로필로 재기동해 IndexedDB 지속성을 검증했다. 총 5회의 독립된 브라우저 프로세스 수명(phase1 → phase2 → explore/phase3 → suspend_a → suspend_b → suspend_c-session1 → suspend_c-session2)에 걸쳐 진행.
- 스크립트/스크린샷: `C:\Users\pavy2\AppData\Local\Temp\claude\...\scratchpad\b24_phase1.js`, `b24_phase2.js`, `b24_explore_pause.js`, `b24_suspend_a.js`, `b24_suspend_b.js`, `b24_suspend_c.js` / 스크린샷·콘솔로그는 `...\scratchpad\b24shots\`
- 코드 열람: `Assets/Scripts/Presentation/Battle/{SafeFile,SaveFlush,RunSave,HangarScreen,GameOverScreen,TitleScreen,PauseScreen}.cs`, `Assets/Plugins/WebGL/FileSync.jslib`, `Assets/Scripts/Core/{SaveDataIntegrity.cs,Simulation/RunSuspendData.cs}`, `Assets/Scripts/Presentation/Battle/BattleDirector.cs`, `Assets/Resources/GameData/ships.json`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | WebGL 저장 지속성 (크레딧, 브라우저 프로세스 완전 종료 후 재기동) | **PASS** |
| 2 | 컨티뉴 재고 지속성 (구매 후 재기동 유지 + 소비 후 재기동해도 되살아나지 않음) | **PASS** |
| 3 | 해금 가격 (Interceptor 50,000 / Bulwark 100,000) | **PASS** |
| 4 | 이어하기(서스펜드 저장) 회귀 — pause→타이틀→CONTINUE로 재개 | **PASS** (+ 손상/불일치 거부 시 새 런 폴백은 코드로 확인) |
| 5 | 회귀 (일반 런 + 콘솔 에러 0) | **PASS** |

핵심 발견: build23에서 지적됐던 "브라우저를 완전히 새로 띄우면 직전 세션의 저장이 사라진다" 문제가 **5번의 독립된 브라우저 프로세스 재기동 전 구간에서 재현되지 않았다**. 크레딧(4,130 → 재기동 → 4,130 유지 → 컨티뉴 구매 2,130 → 재기동 → 2,130 유지 → 컨티뉴 소비 후 최종 4,595 → 재기동 → 4,595 유지)과 컨티뉴 재고(0/8 → 구매 1/8 → 재기동 → 1/8 유지 → 소비 0/8 → 재기동 → 0/8 유지, **되살아나지 않음**) 모두 매 재기동마다 직전 값을 정확히 유지했다. 5개 세션의 콘솔 로그 전체(`[RssFileSync]` 경고 검색 포함)에서 syncfs 실패/경고, 그리고 실제 `[error]`/`pageerror`가 **단 한 건도 없었다**(`[log] [UnityCache] Error when initializing cache...`는 로그 레벨이 `log`인 Unity 자체의 브라우저 CacheStorage 폴백 메시지로, IDBFS 저장 경로와 무관하고 매 세션 동일하게 나타나 헤드리스 크롬 환경 특성으로 판단된다).

---

## 1. WebGL 저장 지속성 (크레딧) — PASS

**세션 A (phase1, 새 프로필)**: 격납고 초기 상태는 `CREDIT 0`(`b24shots/001_title_hangar_fresh.png`). 자연사 1회(SCORE 4130, HITS 3)로 런 종료 시 `BattleDirector.cs:956` `_meta.CreditScore(_run.TotalScore)` → `MetaSave.Save(_meta)`가 즉시 실행되어 타이틀 복귀 시 `CREDIT 4,130`이 표시됨(`b24shots/021_after_run1_title_credit.png`). 콘솔에 `[RssFileSync]` 경고 0건, syncfs 디바운스(0.5s) + 여유 4초 대기 후 `browser.close()` → `process.kill(pid,0)`로 프로세스 완전 종료를 폴링 확인(`PROCESS FULLY EXITED: true`).

**세션 B (phase2, 같은 프로필로 완전 재기동)**: 새 Node 프로세스에서 새 헤드리스 Chrome을 같은 `userDataDir`로 기동 → 로드 직후 격납고에 **`CREDIT 4,130`이 그대로 유지**됨을 확인(`b24shots/101_reboot_title_hangar_credit_check.png`) — build23까지는 여기서 0으로 리셋됐던 지점. 컨티뉴 1개 구매(2,000cr) 후 `CREDIT 2,130`, `CONTINUE 1/8`, 다음 가격 `3,000cr`로 정확히 갱신(`b24shots/102_after_buy_continue.png`).

**세션 C, D (explore_pause / suspend_a~c)**: 이후 3차례 더 재기동하며 매번 직전 크레딧 값이 유지됨을 재확인했고(`b24shots/200_reboot2_hangar_stock_check.png`: `CREDIT 2,130` 유지, `b24shots/018_reboot_final_stock_check.png`: 컨티뉴 소비 뒤 `CREDIT 4,595` 유지), 5개 세션의 콘솔 로그(`b24shots/phase1_console.log`, `phase2_console.log`, `suspend_a_console.log`, `suspend_b_console.log`, `suspend_c_console.log`) 전체에서 `RssFileSync` 관련 경고나 `[error]`/`pageerror`가 전혀 나오지 않았다.

`SaveFlush.cs`/`FileSync.jslib` 코드 확인: `SafeFile.Write`/`Delete`가 매 저장 후 `SaveFlush.Request()`를 호출하고, 0.5초 디바운스 뒤 `RssSyncFileSystem()`(jslib)이 `FS.syncfs(false, cb)`로 IDBFS를 실제 IndexedDB로 내려보낸다. 포커스 상실/일시정지/종료 시점은 `FlushNow()`로 즉시 내리고, jslib 쪽에서도 `pagehide`/`visibilitychange`를 별도로 걸어 이중 안전망을 둔 구조 — 실측 결과와 정확히 일치한다.

## 2. 컨티뉴 재고 지속성 — PASS

§1에서 구매한 컨티뉴 1개가 재기동 뒤에도 `CONTINUE 1/8`로 유지됨을 확인(`b24shots/200_reboot2_hangar_stock_check.png`, `b24shots/202_paused.png` 상단 오버레이에서도 간접 확인).

이어서 재고를 실제로 소비하는 흐름까지 검증했다(`b24_suspend_c.js`): 자연사 → 게임오버에 `CONTINUE (1 LEFT)` 버튼 노출(`b24shots/008_gameover_with_continue_1left.png`, SCORE 1605) → 클릭 → 스코어 `00000000`으로 리셋되고 전투 재개(`b24shots/009_after_continue_click_score_should_reset.png`) → 두 번째 사망에서는 재고가 0이라 `CONTINUE` 버튼 없이 `REDEPLOY/TITLE`만 노출되고 `CONTINUED x1` 보너스 태그가 요약에 표시됨(`b24shots/016_gameover2_no_continue_left.png`) → 타이틀에서 `CONTINUE 0/8`, 다음 가격이 사다리 초기값 `2,000cr`로 리셋(`b24shots/017_title_after_stock_consumed.png`).

**중요**: 이 상태에서 브라우저를 완전히 새로 재기동해도 소비된 재고가 **되살아나지 않고 `0/8`로 유지**됨을 확인(`b24shots/018_reboot_final_stock_check.png`) — REQ-107 주석(`BattleDirector.cs:650-654`)이 설명하는 "메타 없는 오버로드로 리줌하면 이어한 런의 컨티뉴가 메타에 되살아나는 복제 구멍"이 실측으로 재현되지 않았다.

**컨티뉴 복제 봉인 코드 확인**: `BattleDirector.cs:640-685`에서 이어하기(`PendingResume`)가 있을 때 살아 있는 `_meta`가 있으면 `RunManager.ResumeFromSuspendData(..., _meta)` 오버로드로 런 재고와 메타 재고를 함께 물린다. Core는 비-데일리 런에서 저장 재고와 메타 재고가 어긋나면 `ArgumentException`으로 거부하고, `catch` 블록이 `_run = null`로 두어 **새 런으로 안전하게 폴백**한다(저장 파일은 지우지 않고 남겨 재시도 가능하게 함) — "깨진 조합 거부 시 새 런 폴백"이 코드로 확정된다(실측은 정상 흐름만 수행, 손상 파일 직접 주입은 범위 밖).

## 3. 해금 가격 — PASS

`Assets/Resources/GameData/ships.json`에서 `interceptor.unlockCost=50000`, `bulwark.unlockCost=100000` 확인. 격납고에서 ► 커서로 순환하며 실측:

- Interceptor: `[LOCKED — 50,000 cr]` + `UNLOCK\n50,000 cr` 버튼 (`b24shots/002_hangar_interceptor_price.png`)
- Bulwark: `[LOCKED — 100,000 cr]` + `UNLOCK\n100,000 cr` 버튼 (`b24shots/003_hangar_bulwark_price.png`)

## 4. 이어하기(서스펜드 저장) 회귀 — PASS

`b24_suspend_a.js` → `b24_suspend_b.js` (완전히 별도의 브라우저 프로세스 두 개로 나눠 진행 — 서스펜드 저장도 파일 기반이라 재기동 내구성까지 겸해 검증됨):

- 런 시작 → tick 510까지 진행(SCORE 0, STAGE 1, seed 3636943659) → `ESC` 일시정지(`b24shots/211_suspend_paused.png`) → `QUIT` 클릭 → `PauseScreen.QuitToTitle()`이 `_director.SaveRunToDisk()` 호출 후 `Title` 씬 로드. 타이틀에 `CONTINUE\nstage 1` 앰버 버튼이 나타남(`b24shots/212_suspend_title_after_quit.png`).
- **완전히 새로운 브라우저 프로세스**에서 같은 프로필로 재기동 → 타이틀에 여전히 `CONTINUE stage 1` 버튼이 남아 있음(`b24shots/001_title_with_suspended_continue.png`, suspend_b) → 클릭 → 전투가 재개됨: `run 1`, `stage 1`, **같은 seed(3636943659)** 유지, tick은 120부터(`b24shots/002_resumed_battle_tick_check.png`) — `RunSuspendData` 클래스 주석대로 "룸/바이옴 보스 시작 지점" 체크포인트로 복원되는 설계이므로 정지 시점 tick(510)과 다른 것은 정상(코드: `RunSuspendData.cs:96` "Exporting during a stage deliberately returns the state captured before tick zero"). 새 런이 아니라 **저장된 런이 이어졌다**는 근거는 (a) run 번호가 새로 시작하지 않고 `run 1` 그대로, (b) seed가 저장 시점과 동일, (c) 재개 직후 게임오버 화면에도 `(run 1, stage 1)`로 표기된 점.
- 재개된 런은 그대로 플레이해 자연사까지 진행했고(`b24shots/013_regress_final.png`, SCORE 830), 이 구간 콘솔 에러 0건(§5와 통합 확인).

## 5. 회귀 — PASS

- 총 5개의 독립 브라우저 세션(합계 런 5회 이상: 자연사 4회, 컨티뉴 사용 1회, 서스펜드/재개 1회, 일반 진행 다수)에서 **`[error]`/`pageerror` 콘솔 메시지 0건** (`b24shots/*_console.log` 전체 grep 결과, `[UnityCache] Error...`는 `[log]` 레벨의 Unity 자체 캐시 폴백 메시지로 IDBFS 저장과 무관 — 매 세션 재현되는 헤드리스 환경 특성).
- 일반 런 진행(스테이지 1 미드보스 구간, 파워업 게이지, SHIELD x2→x0 데스 시퀀스, HITS 카운트) 전부 build23 대비 회귀 없이 정상 동작.
- 프로세스 레벨에서도 매 `browser.close()` 후 `process.kill(pid,0)` 폴링이 전부 `PROCESS FULLY EXITED: true`로 종료돼, 크래시/행 없이 깨끗하게 종료됨을 확인.
