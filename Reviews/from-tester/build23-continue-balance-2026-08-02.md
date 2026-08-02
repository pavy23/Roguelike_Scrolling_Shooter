# build23 컨티뉴 경제 + 실드 2/1/3 + x32 배율 + HIT 컬럼 + 포탑 비주얼 + 실드0 무음화 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build23, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\b23shots\`, `...\b23turret3\`
- 코드 열람: `Assets/Scripts/Core/Simulation/BattleSim.cs`, `RunManager.cs`, `ContinueEconomy.cs`, `Assets/Scripts/Presentation/Battle/{HangarScreen,GameOverScreen,TitleScreen,ScoreHud,PowerUpHudView,LowHpWarning,BattleDirector,UiText}.cs`, `Assets/Resources/GameData/{ships.json,waves.json}`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 기본 실드 x2 (Starter, 이전 x1) | **PASS** |
| 2 | 레이저 포탑 비주얼 (팔각 본체+포신, 앰버 예열) | **PASS** |
| 3a | 격납고 컨티뉴 가격/재고 UI + NEED 표시 | **PASS** |
| 3b | 컨티뉴 구매 → 게임오버 CONTINUE → 점수 0 재개 | **PASS** |
| 4 | 랭킹 HIT 컬럼 (헤더+실값 정렬) + 게임오버 HITS 줄 | **PASS** |
| 5 | x32 HUD 실측 | **SKIP** (코드 확인으로 대체 — 사유 §5) |
| 6 | 실드 0 무음화 (AudioSource 없음 + 적색 깜빡임 유지) | **PASS** |
| 7 | 회귀 (일반 런 + 콘솔 에러 0) | **PASS** |

중요한 방법론적 발견 하나: **WebGL의 `Application.persistentDataPath`(IDBFS)는 Emscripten 런타임이 살아있는 동안에만 쓰기가 보장되고, `syncfs` 없이 브라우저/프로세스를 재시작하면 저장한 크레딧·컨티뉴 재고가 사라진다.** 처음 두 차례 스크립트를 별도 프로세스로 나눠 실행했다가 크레딧이 매번 0으로 리셋되는 현상을 겪었고, 원인을 `MetaSave.cs`(파일 기반 저장, PlayerPrefs 아님)에서 확인한 뒤 **단일 브라우저 세션 안에서 두 번 죽어 크레딧을 적립 → 컨티뉴 구매 → 세 번째 런에서 사용**까지 한 스크립트로 이어 붙여 우회했다. 이 자체는 build23의 버그가 아니라 헤드리스 자동화의 제약이지만, 혹시 실제 플레이어 브라우저에서도 예기치 않은 크리시/강제 종료 시 최근 저장이 유실될 수 있다는 점은 참고용으로 남긴다(§부록).

---

## 1. 기본 실드 x2 — PASS

`Assets/Resources/GameData/ships.json`에서 `starter` 함선 `"startingShieldStock": 2"` 확인 (interceptor는 1). `BattleSim.cs:7743` `ApplyPlayerHit`은 `ShieldStock>0`이면 감소만 시키고, `ShieldStock==0`일 때 맞으면 `_playerAlive=false`로 즉사시킨다 — 즉 시작값 2는 "2번 막고 3번째에 죽는다"는 뜻과 정확히 대응한다.

실측: 새 런 발사 직후(F3 dev 오버레이 병기) HUD가 `SHIELD x2`, 오버레이도 `shield 2`로 동시에 표시됨.

- `b23shots/22_launch_seq_t0ms.png`

세 번의 독립된 데스 런(전부 god/치트 없이 자연사) 모두 게임오버 요약에서 `HITS 3`이 나왔다 — 실드 2번(무피해)+3번째(사망)로 정확히 일치.

- `b23shots/56_final_state.png` (SCORE 1650, HITS 3)
- `b23shots/205_run1_gameover.png`, `209_run2_gameover.png` (같은 세션의 2·3번째 사망, 각각 HITS 3)

## 2. 레이저 포탑 비주얼 — PASS

`GameData/waves.json`을 직접 분석: `laserEmitter` 장애물은 **fortress·core 테마 세그먼트에만** 존재한다(`seg_fortress_turret_cross` 등). `SegmentStageGenerator.cs:1593` `BuildThemeOrder`를 읽어보면 스테이지1=`scrapyard` 고정, 스테이지5=`core` 고정이고 **스테이지 2~4만** `hive/fortress/nebula`를 시드별로 셔플한다 — 즉 "스테이지 1~2"에서 포탑을 보려면 사실상 스테이지2가 fortress로 뽑힌 시드만 가능하다(약 1/3 확률). `DevArgs.OverrideSeed`는 URL 쿼리를 읽지 않고 커맨드라인 `--seed=`만 읽으므로(`DevArgs.cs:90-103`) `?seed=N`으로 고정은 불가능해, `?dev=1&stage=2&god=1`을 반복 재시도(5회)해 fortress 테마를 맞췄다.

fortress 테마에서 포탑 장애물이 **팔각/원통형 회색 본체 + 붉게 빛나는 포신 끝**(스크린샷 참고용 `turret_preview.png` 디자인과 일치하는 실루엣)으로 렌더링되고, 발사 중(Firing/Sustaining) 빔이 **적색**으로 표시됨을 확인:

- `b23turret3/try2/f18.png` (tick600, 상하 두 포탑이 동시에 적색 빔 발사, 본체 형태 뚜렷)
- `b23turret3/try2/f21.png` (tick660, 근접 샷 — 회색 원통 본체 + 붉은 포구)
- `b23seedscan/seed3.png`, `seed4.png` (다른 시드에서도 동일 실루엣 재확인)

예고(Telegraph) 중 앰버로 달아오르는 것은 350ms 간격 샘플링으로는 0.6초(36틱)짜리 창을 정확히 포착하지 못했지만(§2 실측 한계), 코드로 명확히 확인했다: `BattleDirector.cs:173` `EmitterHeatColor = new Color(1f, 0.55f, 0.32f, 1f)`(앰버)이고, `TrackLaserEmitters()`(L1391-1411)가 `LaserPhase.Telegraph` 동안 `PhaseTicksRemaining`에 비례해 `heat`를 0.15→0.75로 올리며(L1399-1401) `Color.Lerp(c, EmitterHeatColor, heat)`(L1336)로 스프라이트를 앰버로 서서히 물들인다 — "예고 중 앰버로 달아오름"은 코드상 확정.

`f19.png`(tick630)와 `f20.png`(tick660 직전)에서 빔이 순간적으로 적색→백색-주황으로 밝아지는 프레임도 잡혔는데, 이는 `dissipateTicks`(소산) 구간의 잔열 표현(`heat=0.4` 폴백, L1409)으로 판단된다 — 발사 중 시각 피드백이 단조롭지 않고 단계별로 갈린다는 추가 근거.

## 3a. 격납고 컨티뉴 가격/재고 UI + NEED — PASS

새 브라우저 프로필(크레딧 0) 상태의 타이틀 화면 하단좌측에 `CONTINUE 0/8`과 `BUY CONTINUE 2,000 cr` 버튼이 항상 보인다(WebGL은 `UiPlatform.TouchMode`가 항상 true라 버튼형 UI로 렌더링됨, `UiKit.cs:17-21`).

- `b23shots/201_title_hangar_initial.png`

크레딧 0 상태에서 BUY를 누르면 `HangarScreen.TryBuyContinue()`가 `ContinuePurchaseRejectionReason.InsufficientCurrency`를 받아 `NEED {price} cr`을 2초간 띄운다 — 실측으로 `CONTINUE 0/8   NEED 2,000 cr`이 정확히 표시됨을 확인:

- `b23shots/202_hangar_need_credits.png`

## 3b. 컨티뉴 구매 → 사용 흐름 — PASS

단일 세션에서 죽음 2회(HITS 3, 각 스코어 4240/4690 등) 후 크레딧이 `CreditScore`(`BattleDirector.cs:930`, 런 스코어를 1:1로 적립)로 누적돼 `CREDIT 5,080`이 됨:

- `b23shots/210_after_run2_title_credit.png`

BUY 클릭 → 구매 성공, `CONTINUE 1/8`(재고>0이라 앰버 강조색)로 바뀌고 크레딧이 `5,080 → 3,080`(정확히 2,000 차감), 다음 가격도 사다리 공식대로 `3,000 cr`로 갱신됨(`ContinueEconomyConfig.GetPurchasePrice`: `2000 + 1000×stock`과 일치):

- `b23shots/212_buy_continue_after_credit.png`

재고 1을 들고 세 번째 런을 시작 → 사망 → 게임오버 화면에 **REDEPLOY / CONTINUE (1 LEFT) / TITLE** 세 버튼과 경고문 `CONTINUE RESTARTS THIS SECTOR - SCORE RESETS TO 0`이 함께 등장(재고 0일 때는 이 버튼 자체가 없었음, §1의 다른 게임오버 스크린과 대조):

- `b23shots/214_run3_gameover_continue_visible.png` (SCORE 1060, HITS 3, CONTINUE (1 LEFT) 버튼 확인)

CONTINUE 클릭 → 스코어가 `00000000`으로 리셋되고 SHIELD가 `x2`로 완전 회복, 같은 스테이지(STAGE 1/5, MID-BOSS)가 이어짐 — `RunManager.TryUseContinue()`(L2594-2619)의 `_completedStageScore=0`, `ResetToBasicPowerState()`, `BuildCurrentStage()` 동작과 정확히 일치:

- `b23shots/215_run3_after_continue_click_score_reset.png`

## 4. 랭킹 HIT 컬럼 + 게임오버 HITS 줄 — PASS

게임오버 요약 줄에 `BEST COMBO xN   GRAZE n   BOMBS n   HITS n` 형태로 HITS가 항상 표시됨(위 §1·§3b 스크린샷 전부에서 확인).

랭킹 모달(타이틀 우측 RANKING 버튼) 헤더: `#  PILOT  SCORE  STG  SHIP  BOMB  HIT` — 신규 HIT 컬럼이 BOMB 뒤에 정렬돼 있음:

- `b23shots/203_ranking_initial.png` (엔트리 없는 초기 상태, 헤더만)

타이틀의 랭킹 모달은 **데일리 보드만** 보여준다(`TitleScreen.RequestRanking()` → `ScoreboardClient.FetchBoard(true,...)` → `/v1/board/daily`, `ScoreboardClient.cs:280-283`). 일반 런 제출은 `/v1/scores`로 올라가 전역(all) 보드 랭크(`RANK #3` 확인, `b23shots/206_run1_after_submit.png`)는 받지만 이 데일리 전용 모달에는 안 뜬다 — 그래서 데일리 챌린지로 한 판을 더 돌려 제출했더니 실제 값이 정렬돼 나왔다:

- `b23shots/305_daily_ranking_with_entry.png`: `1  TESTERDAIL   860  1-2  ST   0   3` — HIT 컬럼에 실값 `3`이 SCORE/STG/SHIP/BOMB과 같은 줄에 우측정렬로 표시됨 (BOMB `0`은 강조 뱃지 색으로도 확인).

구 항목(HIT 없는 레거시 기록)의 `-` 폴백은 서버에 그런 레코드가 없어 실측하지 못했지만, `TitleScreen.cs:312-328` `AppendHitCell`을 코드로 직접 확인: `if (!entry.HasHits) { sb.Append("-".PadLeft(ColHit)); return; }` — `ScoreboardClient`가 응답에 `ht` 키가 없는 기록에 `HasHits=false`(내부적으로 -1 심음)를 세우는 구조이므로 로직 자체는 확정적으로 맞다.

## 5. x32 HUD — SKIP (코드 확인으로 대체)

god 모드 + fortress 스테이지 관측 세션들에서 관측된 최고 배율은 `x2`(`ScoreHud`의 멀티플라이어 표시, 예: `b23turret3/try2/f19.png` 상단 `x1`, 그 외 세션에서도 최고 `x4` 1회)에 그쳤다 — 배율을 32까지 올리려면 킬을 끊김 없이 상당수 이어야 하는데(콤보 게이지가 킬 없이 시간이 지나면 감소, `BattleSim.cs:7635-7648`), 자동화 스크립트는 회피/화력보다 관측 위치 유지에 집중해 짧은 시간 안에 그만큼의 연속 킬을 만들지 못했다.

코드로는 배율 상한이 실제로 32까지 있음을 확인: `ScoreHud.cs:60-61` `case 16: return "x16";`, `case 32: return "x32";`이 고정 문자열로 존재하고, `BattleSim.cs:920` `ComboMultiplierLevelCount = 6`(1·2·4·8·16·32의 6단계)이 `RunManager.cs:4500` 검증과 함께 일치한다. HUD 표시 로직 자체(자릿수 늘어도 우측 피벗이라 안 밀림, `ScoreHud.cs:24-28`)도 코드로 확정.

## 6. 실드 0 무음화 — PASS

`LowHpWarning.cs` 전체를 읽었다 — `AudioSource`/`PlayOneShot`/`AudioClip` 필드나 호출이 **전혀 없다**. 클래스 주석에 `**소리는 내지 않는다.**`, `사람 지시 2026-08-02: "빨갛게 깜빡거리기만 하고 소리는 끄자"`가 명시돼 있고, `Update()`가 하는 일은 `_director.ShieldRemaining==0`일 때 화면 4변 가장자리 띠를 `Mathf.Sin(Time.unscaledTime*5.2f)`로 맥동시키는 것뿐(L76-92).

실측: SHIELD x0 상태에서 화면 4변에 붉은 띠가 계속 맥동하는 것을 여러 프레임에서 확인(오디오는 헤드리스라 직접 청취 불가 — 코드 부재로 대체 확인):

- `b23shots/13_run2_gameover_continue_check.png`, `14_run2_after_continue_click.png` (SHIELD x0 + 붉은 테두리 활성, 게임플레이 진행 중)

## 7. 회귀 — PASS

- 일반 런(비-dev, `?dev=1` 없는 순정 URL): 타이틀 → LAUNCH → 전투 진입까지 정상, dev 오버레이/치트 텍스트가 전혀 노출되지 않음(cheat 게이팅 정상) — `b23shots/402_nodev_battle_no_overlay.png`
- 콘솔 에러/페이지 예외 0건: `page.on('console'|'pageerror')` 리스너를 걸고 로드~전투 진입~24초 플레이까지 관측, `TOTAL CONSOLE/PAGE ERRORS: 0`
- 이 세션 전체(약 15분, 런 6회 이상: 자연사 3회, 컨티뉴 1회, 데일리 1회, god 모드 관측 다수)에서 스크립트/브라우저 크래시나 UI 깨짐 없음

---

## 부록: WebGL 저장 지속성 메모 (버그 아님, 방법론 기록)

`MetaSave.cs`는 `Application.persistentDataPath/meta.json`에 파일로 저장한다(PlayerPrefs 아님). WebGL 빌드에서 `persistentDataPath`는 IDBFS(가상 파일시스템)이고, 실측 결과 **브라우저 프로세스를 완전히 새로 띄우면(headless Chrome을 매번 재시작) 직전 세션에서 저장한 크레딧이 사라졌다**(`Default/IndexedDB/http_localhost_8099.indexeddb.leveldb`에 로그가 쌓이긴 하지만 읽어보면 갱신되지 않음) — 즉 명시적 `syncfs` 없이는 저장이 실제 IndexedDB로 flush되는 시점이 보장되지 않는 것으로 보인다. 같은 세션(같은 페이지, 씬 전환만) 안에서는 즉시 반영됐다. 실제 플레이어는 탭을 끄지 않는 한 문제되지 않겠지만, 브라우저 크래시/강제 종료 시나리오에서 최근 구매/해금이 유실될 가능성은 CODEX/렌더러 몫으로 참고 공유한다(정식 REQ는 별도 판단 필요 — 본 리포트 범위 밖).
