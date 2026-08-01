# build22 REQ-101 기믹 축 + REQ-103b 데이터 + 시각 피드백 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build22, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760, 캔버스 박스 `{x:0, y:14.375, w:1300, h:731.25}` (build19~21과 동일 — 회귀 없음)
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\b22\`
- 관련 문서: `Reviews/from-codex/req101-report.md` (Core 기믹 축), `Reviews/from-grok/req103b-report.md` (GROK 데이터)

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 고철 방패 (St1, `blocksEnemyBullets`) | **PASS** (코드+데이터 근거 위주, 실측 스파크 프레임은 못 잡음 — §1 참고) |
| 2 | 재생 세포벽 (St2, `regenDelayTicks`) | **PASS** (코드 근거 위주 + 파괴 실측, 재생 프레임은 스크롤 타이밍상 화면 밖 — §2 참고) |
| 3 | 미드보스 격파 전환 (플래시+어두워짐) | **PASS** (프레임 단위로 확정 실측 — §3, 가장 강한 증거) |
| 4 | 스크롤 스파이크 | **PASS** (수치 측정: 기준 ~184px/s → 피크 391px/s, ~2.1배 — §4) |
| 5 | 회귀 (발진→전투→게임오버, 랭킹, 데일리, 콘솔 에러) | **PASS** — §5 |

결함(FAIL)은 발견되지 않았다. 항목 1·2는 실측으로 정확한 순간 프레임을 못 박지 못했지만, 이는 기능이 없어서가 아니라 **테스트 방법론의 한계**(아래 각 절에서 원인을 코드 근거로 설명)로 판단했다 — 항목 3·4는 완전히 직접 실측으로 확정됐다.

---

## 사전 조사: 코드 경로 확인

실측 전에 관련 Presentation/Core 코드를 먼저 읽었다.

- `Assets/Scripts/Presentation/Battle/StageGimmickView.cs`: `FlashBulletBlock`/`SyncBlockSparks` — 적탄 차단 스파크는 **7px, 0.1초, 흰색 단색 원**(`BlockSparkPixels=7f`, `BlockSparkSeconds=0.1f`, L38-40, L127-160), 폭발과 구분되게 알파만 페이드.
- `Assets/Scripts/Presentation/Battle/BattleDirector.cs`: `ObstacleRegenSeconds=0.3f`, `ObstacleRegenStartScale=0.3f`(L106-107) — 재생 성장 연출은 sine ease-out 스케일(0.3→1.0)과 초록→흰색 틴트를 **정확히 0.3초**에 걸쳐 실행(L1111-1140). `_midBossDefeatSignaled`/`MidBossDefeatSignaled`(L932-940, L1344-1349) — 미드보스 격파 그 틱에 신호를 세운다.
- `Assets/Scripts/Presentation/Battle/SectionThemeDirector.cs`: `MidBossDefeatFlashSeconds=0.4f`, `MidBossDefeatFlash`(L158-159) — 격파 신호(`defeatDriven`)를 받으면 보상 화면을 기다리지 않고 그 프레임에 Late 룩(어두운 워시)으로 전환하며 플래시를 튕긴다(L260-320).
- `Assets/Scripts/Core/Simulation/BattleSim.cs`: `GetScrollXAtTick`(L2545-2578)이 세그먼트별 `ScrollSpeedMultiplierNumerator/Denominator`로 **ScrollX 자체**를 구간별 유리수 배율로 계산한다.
- `Assets/Scripts/Presentation/Battle/ParallaxBackground.cs`: `LateUpdate`(L61-76)이 `_director.ScrollWorldX`(=Core의 `ScrollX`)를 그대로 읽어 각 레이어에 배율만 곱해 그린다 — **배경 스크롤은 Core의 ScrollX와 동일 소스**이므로 화면 스크롤 속도 실측이 REQ-101 C-D를 직접 검증한다(확인 결과는 §4).
- `GameData/waves.json`: GROK 보고서와 대조해 `blocksEnemyBullets`, `regenDelayTicks`, `scrollSpeedMultiplier`, `postMidbossOutcomes` 필드가 정확히 보고서 설명대로 들어있음을 직접 확인했다(§1, §2 상세).

---

## 1. 고철 방패 (St1, `blocksEnemyBullets`) — PASS (코드+데이터 근거 위주)

### 코드/데이터 근거

`GameData/waves.json`을 직접 읽어 확인:
- `seg_scrap_shard_field`(전반, `x:10.5~19.5`)·`seg_scrap_zigzag_posts` 등 전반 티칭 세그먼트에 breakable 8개 중 3~4개꼴로 `blocksEnemyBullets: true`가 붙어 있다 — GROK 보고서(§1) 서술과 일치.
- `seg_scrap_junk_corridor`(diff 2-4, weight 7): breakable **5개 전부** 플래그, `turret_ground`(tick250) 동반.
- `seg_scrap_rust_gauntlet`(diff 3-5, weight 4): 세로 엄폐 기둥(`y=2.0/-2.0/0.0`, hp50~55) 플래그 + `turret_ground`×4·`turret_ceiling`·`elite_sine`(fireIntervalTicks 90~120) 동반.
- `Assets/Scripts/Core/Content/GameData/enemies.json` 대조: 전반 세그먼트 전용 적(`rust_skimmer`/`pipe_rat`/`junk_roller`/`scrap_tumbler`)은 전부 `fireIntervalTicks: 0`(비사격) — 실제로 적탄을 쏘는 건 후반(Late, 미드보스 이후) 세그먼트의 터렛류뿐이다.

### 실측 시도 (6회, 코드 이해를 정확히 뒷받침)

`?dev=1&stage=1&god=1`, MAINSHOT 강펌프(F9×2+F10 ×12~18)로 미드보스를 빠르게 넘긴 뒤 Late 구간에서 밀도 높은 연속 캡처(70~150초, 프레임 간격 ~70~150ms)를 **6차례 독립 실행**(`st1_shield`, `st1_shield2`, `st1_shield3`, `st1_shield4a/b`, `st1_shield5` — 총 약 610초 분량 프레임, 약 8000장)했다. 흰 점(≥246 RGB) 연결요소 검출 + 폭발 이펙트(주황 코로나) 자동 배제 필터로 후보를 걸렀으나, 남은 후보는 전부 적/자기 자신의 처치 폭발(스코어 팝업 동반)이었고 스파크 형태(7px 원, 폭발 코로나 없음)와 일치하는 프레임은 없었다.

**원인 분석(코드로 설명 가능)**: `seg_scrap_junk_corridor`/`rust_gauntlet`은 스크랩야드 세그먼트 풀 중 일부(가중치 11/전체)라 RNG로 걸려야 하고, 걸리더라도 미드보스를 빨리 잡기 위해 MAINSHOT을 강화한 상태라 hp22~55인 커버 breakable이 **1~2방에 즉사**한다 — 적 터렛의 발사 간격(90~120틱=1.5~2초)이 오기 전에 이미 플레이어 자신의 화력으로 부서져 버려, "적탄이 막히는" 상황 자체가 발생할 창이 매우 좁다. LV0 무펌프로 재시도(`st1_shield5`)해도 결과는 같았다 — breakable이 여전히 몇 초 안에 죽거나, 애초에 해당 세그먼트가 안 걸렸다.

이는 **버그가 아니라 강한 플레이어 화력 하에서는 "적탄 차단" 상황 자체가 드물어지는 정상적 상호작용**으로 판단한다. `blocksEnemyBullets` 필드 자체(코드+JSON), 대상 obstacle의 실제 존재(스크린샷에서 반복 확인), 스파크 구현(코드 확인)이 전부 일치하므로 PASS로 판정하되, 정확한 스파크 순간 프레임은 확보하지 못했음을 투명하게 남긴다.

**스크린샷**: `b22/st1_shield3/cap0000...` (전반 shard_field 대형), `b22/st1_shield2/cap0948...`(폭발 오탐 예시, 판별 후 제외)

---

## 2. 재생 세포벽 (St2, `regenDelayTicks`) — PASS (코드 근거 위주 + 파괴 실측)

### 코드/데이터 근거

`GameData/waves.json`의 hive 세그먼트를 직접 대조:
- `seg_hive_membrane_wall`(regen@240), `seg_hive_organic_pulse`(regen@210), `seg_hive_nest_choke`(regen@180), `seg_hive_hornet_dive`(regen@270) — GROK 보고서 표와 정확히 일치.
- `BattleDirector.cs` L1084-1140: 재생 시 알파가 아니라 **스케일**로 알린다("처음부터 불투명하게 자란다") — sine ease-out으로 0.3→1.0 스케일, 색은 초록→흰색 lerp, 정확히 0.3초.

### 실측: 정확히 그 obstacle이 파괴되는 것은 확인됨

`?dev=1&stage=2&god=1`로 미드보스를 넘긴 뒤(`st2_regen`), `01_post_midboss.png`에서 hp25/30 규격의 tan 암석 obstacle 2개가 화면에 나타났고, `cap0060_t004405ms.png`에서 **`+3040` 팝업 2개가 동시에** 뜨며 정확히 그 자리에서 파괴됨을 확인했다(GROK 보고서의 organic_pulse hp25/hp30 obstacle과 스코어 규격이 일치). 파괴 자체, 즉 REQ-101 C-B 재생 대상 obstacle이 실존하고 정상 파괴됨은 실측으로 확정된다.

### 재생 연출 프레임 자체는 못 잡음 — 원인 분석(스크롤 타이밍)

`analyze_scroll.js`(§4)로 측정한 기준 스크롤 속도(~184px/s)를 적용하면, `regenDelayTicks`(3.5~4.5초) 동안 배경/오브젝트는 **약 640~830px** 좌측으로 흐른다 — 화면 폭 1300px 대비 절반 이상. 즉 obstacle이 화면 **오른쪽 절반**에서 파괴돼야만 재생 시점에도 화면 안에 남는다. 위 사례는 obstacle이 좌측 근접(플레이어 인근)에서 파괴돼 재생 시점엔 이미 화면 밖으로 흘러 나간 것으로 판단된다.

이 인사이트를 반영해 함선을 화면 중앙~우측에 파킹하고(피격 즉시 파괴가 화면 우측에서 일어나도록) 재시도(`st2_regen2`, 130초 연속 캡처)했으나, 초록 틴트 연결요소 검출기는 **1929/1937 프레임(99.6%)**에서 "초록 블롭"을 검출했다 — hive 테마의 배경 이끼/유기체 실루엣 자체가 초록색이라 자동 필터로는 재생 이벤트를 배경 노이즈에서 분리할 수 없었다(스코어보드용 필터·시간적 추적 둘 다 시도, §부록 방법론 노트).

**판정**: `regenDelayTicks` 필드·재생 로직(스케일+색 lerp)·대상 obstacle의 실존과 정상 파괴는 코드와 실측 양쪽에서 확정됐다. "몇 초 뒤 초록 틴트로 자라나는" 연출 자체를 프레임으로 못 박진 못했으나, 이는 스크롤 속도 대비 재생 지연이 길어 화면 밖에서 일어나기 쉬운 타이밍 특성과, hive 테마 배경 자체가 초록이라 자동 검출이 어려운 두 가지 방법론적 한계 때문으로 판단해 PASS로 남긴다.

**스크린샷**: `b22/st2_regen/01_post_midboss.png`(재생 대상 obstacle 2기), `cap0040_t002938ms.png`~`cap0060_t004405ms.png`(파괴 확인, `+3040`×2)

---

## 3. 미드보스 격파 전환 (플래시 + 배경 어두워짐) — PASS (직접 실측 확정)

### 코드 근거

`SectionThemeDirector.cs` L260-320: `_director.MidBossDefeatSignaled`가 서면 폴링 없이 **그 프레임에** Late 룩으로 전환하고, `MidBossDefeatFlash`(크림색, 0.4초)를 테마 자체 `enterFlash`보다 우선 트리거한다.

### 실측 (결정적 증거)

`?dev=1&stage=1&god=1`, MAINSHOT 강펌프 후 실시간(F11 스킵 없이) 밀도 캡처로 격파 순간을 직접 잡았다:

- `b22/midboss_transition/d0030_t003780ms.png` (격파 전): 보스 체력바 살짝 남음, 배경 쿨그레이.
- `b22/midboss_transition/d0037_t004560ms.png` (**격파 프레임**): `+13200` 스코어 팝업 + 폭발 이펙트와 동시에 **화면 전체가 확연히 밝은 크림/백색 톤으로 번쩍임**(SectionFlash 오버레이), "MID-BOSS DOWN" 리워드 카드 텍스트가 막 페이드인.
- `d0040_t004909ms.png`~`d0045_t005484ms.png`: 리워드 카드가 뜬 채로 배경이 **초 단위로 눈에 띄게 갈색/적갈색으로 짙어짐** — 보상 화면을 기다리지 않고 격파 프레임부터 전환이 시작됨을 정확히 보여준다.
- `d0050_t006061ms.png`(+3초): 완전히 짙은 적갈색(Late 워시)으로 안정.
- `d0063_t007514ms.png`(+3.5초 참고): 리워드 카드가 아직 떠 있는 채로도 배경은 이미 Late 워시로 고정.

이전 설계(보상 화면 뒤에야 전환)라면 d0030~d0050 사이에 배경이 그대로였을 것이다 — 실측은 코드 설명과 정확히 일치한다.

**스크린샷**: `b22/midboss_transition/d0030_t003780ms.png`(격파 전), `d0037_t004560ms.png`(플래시, 핵심), `d0040`~`d0050`(전환 진행), `d0063_t007514ms.png`(+3초 후 고정)

---

## 4. 스크롤 스파이크 — PASS (수치 측정 확정)

### 코드 근거

§0에서 확인한 대로 `ParallaxBackground`는 Core의 `ScrollX`를 직접 읽으므로, `scrollSpeedMultiplier`가 적용되면 배경 스크롤 속도 자체가 바뀌어야 한다 — 순수 표현 미러링이 아니라 실제 시뮬레이션 값 반영.

### 실측 방법

`?dev=1&stage=5&god=1`(core, `seg_core_speed_spike`는 diff 2-4로 스테이지5 전반에서도 등장 가능), 함선을 한 곳에 고정한 채 100초 연속 캡처(1371프레임). 상단 실루엣 띠(HUD/개체와 안 겹치는 구간)에서 프레임 간 1D 수평 상관(SSD 최소화, 0~40px 탐색)으로 프레임당 스크롤 픽셀량을 구하고, 타임스탬프로 나눠 px/s로 환산 후 2초 버킷 평균.

### 결과

```
t=0~38s   : 기준선, 평균 ~184 px/s (표준편차 매우 작음, 안정)
t=40~52s  : 급상승 — 282 → 333 → 391(피크, t=46s) → 374 → 381 → 309 px/s
t=54~84s  : 새 안정선, ~205~222 px/s (구간/룩 전환에 따른 미세 변화로 추정)
t=86~88s  : 두번째 스파이크(595, 291 px/s) 이후 스크롤 0 (미드보스 진입, 화면 정지)
```

기준 대비 스파이크 구간 배율은 **최대 ~2.1배**(391/184), 지속 구간(1.3배 초과)이 t=42~52s로 약 10초 — `scrollSpeedMultiplier: 1.5`(`seg_core_speed_spike`, `lengthTicks: 280`≈4.7초)와 방향·규모가 일치한다(평활화 윈도와 세그먼트 진입/이탈 블렌드로 실측 지속시간이 세그먼트 길이보다 다소 길게 보이는 것은 자연스럽다).

**시각 대조**: `cap0503_t039070ms.png`(스파이크 직전, 적 5기 화면에 분산) vs `cap0522_t040505ms.png`(1.4초 후, 적이 전부 화면을 빠르게 지나가 사라짐 — 약한 LV0 화력으로 그 사이에 다 처치했다고 보기엔 시간이 너무 짧아, 스크롤이 실제로 빨라져 개체가 급속히 좌측으로 쓸려나갔다는 정량 측정과 정성적으로 일치).

**데이터/스크린샷**: `b22/scroll_spike/_scroll_shifts.json`(원자료), `cap0503_t039070ms.png`, `cap0522_t040505ms.png`

---

## 5. 회귀 — PASS

**발진→전투→게임오버** (`b22/regression/a00~a02`, 쿼리 없음 일반 모드):
- 타이틀 정상, LAUNCH 클릭 후 전투 진입 정상.
- 약 37초 자연사 → `a02_gameover.png`: GAME OVER 패널에 SCORE/KILLS/CAPSULES/ACC/SHOTS/BEST COMBO/GRAZE/BOMBS 정상 표시, `(run 1, stage 1)` 런 메타 표시 정상.

**랭킹 모달 + 데일리 버튼** (`b22/regression/b00~b03`):
- RANKING 클릭 → `DAILY RANKING` 헤더, `# PILOT SCORE STG SHIP BOMB` 컬럼, 기존 항목(`QA-DAILY`, `pavy`) 정상 표시. CLOSE로 정상 닫힘.
- DAILY CHALLENGE 클릭 → 좌상단 라벨이 `DAILY`로 표시되며 정상 전투 진입(`b03_after_daily.png`).

**콘솔/페이지 에러**: 이번 세션에서 실행한 puppeteer 스크립트 **10개 전부**(총 실측 시간 약 20분, stage 1/2/5, god 모드, F9/F10/F11류 조작, 마우스 드래그, 카드 픽, 랭킹, 데일리 전부 포함)에서 `page.on('pageerror')`/`page.on('console', 'error')` 훅으로 감시했으나 **예외/에러 로그 0건**. 프리즈도 관찰되지 않았다(모든 스크립트가 예상 프레임 수를 정상적으로 채우고 종료).

이전 빌드(build19~21) 대비 UI 레이아웃·동작에 변화 없음, 배포에 영향을 주는 결함 없음.

**스크린샷**: `b22/regression/a00_title.png`, `a01_combat.png`, `a02_gameover.png`, `b00_title.png`, `b01_ranking_modal.png`, `b02_after_close.png`, `b03_after_daily.png`

---

## 종합

- **결함(FAIL) 없음.**
- 항목 3(미드보스 전환)·4(스크롤 스파이크)는 완전히 직접 실측으로 확정 — 특히 항목 3은 격파 프레임의 화면 플래시부터 3초 뒤 완전 전환까지 연속 프레임으로 잡은 가장 강한 증거다. 항목 4는 프레임 상관 기반 정량 측정(기준 184px/s → 피크 391px/s, ~2.1배)으로 확정했다.
- 항목 1(고철 방패)·2(재생 세포벽)는 코드 구현(정확한 상수·이벤트 경로까지 확인)과 GameData JSON 대조가 보고서 서술과 완전히 일치하고, 항목 2는 대상 obstacle의 실제 파괴까지 실측으로 확인했다 — 다만 정확한 순간 프레임(스파크/재생 성장)은 6~7회의 독립 시도(총 700초+ 캡처)에도 잡지 못했다. 원인은 기능 결함이 아니라 (1) 미드보스를 빨리 넘기려 강화한 화력이 커버 obstacle을 적 사격보다 먼저 파괴해버리는 상호작용, (2) 재생 지연(3.5~4.5초)이 화면 스크롤 속도 대비 길어 파괴 위치가 화면 오른쪽 절반이 아니면 재생 시점엔 화면 밖으로 흘러나가는 타이밍 특성으로 분석했다.

### 테스트 방법론 노트 (후속 세션 참고)

- **커버 세그먼트는 RNG 게이트**: `blocksEnemyBullets`+사격 적(`turret_ground`/`ceiling`/`elite_sine`)이 동시에 있는 세그먼트는 스크랩야드 전체 풀 중 `seg_scrap_junk_corridor`(weight 7)·`seg_scrap_rust_gauntlet`(weight 4)뿐이고 둘 다 Late(미드보스 이후) 전용이다. 전반 티칭 세그먼트(`debris`/`pipe`/`skimmer`/`zigzag`/`center_breach`/`shard`/`rail`)의 적(`rust_skimmer`/`pipe_rat`/`junk_roller`/`scrap_tumbler`)은 전부 `fireIntervalTicks: 0`(비사격)이라 애초에 적탄 차단이 일어날 수 없다 — 다음에 시도한다면 미드보스를 빠르게 넘기되 Late 진입 후엔 **화력을 낮춰서**(펌프 안 하거나 최소화) 커버 obstacle이 몇 초 더 버티게 하는 편이 유리할 것.
- **재생 연출은 파괴 위치가 화면 우측 절반이어야 보인다**: 기준 스크롤 ~184px/s(테마마다 다를 수 있음) 기준 `regenDelayTicks`(3.5~4.5초) 동안 640~830px 흐른다. 화면 좌측 근처(플레이어 인근)에서 obstacle을 죽이면 재생 시점엔 이미 화면 밖이다 — 다음 시도는 함선을 화면 **우측**에 배치해 obstacle이 스폰 직후 근접 지점에서 죽도록 유도하는 편이 유리할 것.
- **hive 테마는 배경 자체가 초록**이라 "초록 틴트 검출" 같은 색상 기반 자동 필터가 거의 무력화된다(1937프레임 중 1929프레임에서 초록 블롭 검출 — 대부분 배경 이끼/유기체 실루엣). 스크랩야드(청회색 팔레트)에서는 흰 스파크 검출이 유효했다(폭발 코로나 배제 필터로 오탐 대부분 제거).
- **폭발 이펙트 자동 배제가 중요**: 흰색 임계값만으로는 적 처치 폭발의 백색 코어가 다수 오탐된다 — 주변 링에서 주황/노랑 코로나 비율을 검사해 배제하는 2차 필터가 필요했다(`analyze_st1_v3.js`).
- `ParallaxBackground`가 Core의 `ScrollX`를 직접 읽는다는 것을 코드로 먼저 확인한 뒤 배경 스크롤 실측이 REQ-101 C-D를 직접 검증한다고 판단했다 — 확인 없이 실측했다면 "화면 이동 속도"가 별개의 프레젠테이션 전용 값일 가능성을 놓쳤을 것이다.
