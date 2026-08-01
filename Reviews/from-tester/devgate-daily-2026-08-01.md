# 빌드16 배포 게이트 — dev 게이트 + DAILY 모드 표기 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (`Builds/Web`, 빌드 시각 2026-08-01 14:54, `BuildOptions.None` — Development Build 아님 확인 → `?dev=1` 유무가 곧 게이트 판정 기준이 됨)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760, 캔버스 ref 변환식 `toX(rx)=box.x+((rx+320)/640)*box.width`, `toY(ry)=box.y+(ry/360)*box.height` (기존 검증과 동일)
- 스크린샷 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\devgate-shots\`
- 검증 스크립트: 같은 scratchpad 경로의 `gate1_release.js`, `gate2_dev.js`, `gate3_daily.js`, `gate3b_banner.js`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 치트 차단 (릴리즈 경로, `?v=gate1`, dev 없음) — F9×5 후 게이지 커서 무변화 | PASS |
| 2 | dev 모드 치트 허용 (`?v=gate2&dev=1`) — F9×3 후 게이지 커서 이동 | PASS |
| 3 | 치트 런 제출 차단 — GAME OVER에서 "DEV RUN - NO SUBMIT" 비활성 | PASS |
| 4 | DAILY 표기 — 타이틀 2줄 라벨 + 전투 중 DAILY 뱃지·배너 윗줄 | PASS |
| 5 | 회귀 — HUD 6칸 게이지 + SHIELD 표기 (DAILY 런) | PASS |

**판정: 전 항목 PASS — 배포 가능.**

---

## 상세

### 0. 사전 확인 — 이 빌드가 릴리즈 경로인지

`Assets/Editor/MobileBuilder.cs`의 `BuildWebGl()`이 `BuildPipeline.BuildPlayer`에 `options = BuildOptions.None`(Development 플래그 없음)을 넘긴다. `DevArgs.ResolveDevMode()`는 `Debug.isDebugBuild`가 true면 무조건 dev 모드가 되므로, 이 빌드가 실제로 Development Build가 **아님**을 먼저 확인해야 1번 항목이 의미가 있다. 빌드 파일(`Builds/Web/Build/*.unityweb`)이 오늘(2026-08-01 14:54) 새로 생성된 것도 확인 — 이번 빌드16 산출물이 맞음.

참고: 이전 스코어보드 검증(`?v=tester1`, dev 파라미터 없음)에서는 F11 스킵 치트가 그대로 동작했었는데, 이는 그 빌드가 이번 dev-게이트 기능 추가 이전 것이었기 때문 (태스크 설명의 "이전 빌드에선 F9가 캡슐을 줬다"와 일치). 이번 빌드16이 게이트를 처음 적용한 빌드.

### 1. 치트 차단 (릴리즈 경로)

`?v=gate1` (dev 파라미터 없음) 로드 → LAUNCH → 전투 진입 직후 스크린샷(F9 이전) → F9 5회(각 150ms 간격) → 스크린샷(F9 이후).

게이지 6칸(SPEED/SHOT/MISSILE/DOUBLE SHOT/OPTION/SHIELD) 모두 F9 전후 동일하게 회색(FrameNormal) 유지 — 앰버 커서가 전혀 나타나지 않음. 좌상단 dev 오버레이(F3 안내 문구)도 없음 → dev 모드 자체가 비활성 확인.

스크린샷: `devgate-shots/gate1_01_combat_entry.png` (F9 전), `devgate-shots/gate1_02_after_f9x5.png` (F9 후) — 육안 대조 결과 게이지 열 완전 동일.

### 2. dev 모드 치트 허용

`?v=gate2&dev=1` 로드 → LAUNCH → 전투 진입 직후 스크린샷 → F9 3회(200ms 간격) → 스크린샷.

좌상단에 dev 오버레이(`run 1 stage 1 diff 1 seed ... [F9] capsule [F10] activate ...`)가 즉시 노출되어 dev 모드 활성 확인. F9 3회 후 MISSILE 슬롯(3번째 칸) 프레임이 앰버(FrameCursor)로 전환됨 — 게이지 커서가 실제로 이동함.

스크린샷: `devgate-shots/gate2_01_combat_entry.png` (F9 전, 전 칸 회색), `devgate-shots/gate2_02_after_f9x3.png` (F9 후, MISSILE 칸 앰버)

### 3. 치트 런 제출 차단

2번 세션 계속 — 방치(터치 이동 없음, 자동사격만, F11 스킵 미사용) → 약 33초 경과 시점 GAME OVER 도달(dev 오버레이 `tick 308 shield 0 dead` 확인, 30~90초 범위 내).

GAME OVER 패널에서 REDEPLOY(앰버, 활성)·TITLE(활성) 옆에 세 번째 버튼이 `DEV RUN - NO SUBMIT` 라벨로 무채색(비활성 스타일)으로 표시됨. 해당 버튼 좌표를 클릭했으나 클릭 전후 스크린샷이 픽셀 단위로 완전히 동일 — 다이얼로그도, 라벨 변화도, 상태 변화도 전혀 없음 → 클릭이 실제로 무시됨(정상 비활성) 확인.

- 최종 스코어: 1270 (KILLS 8, CAPSULES 2, ACC 16.9%, run 1 stage 1) — F9 치트가 사용된 런이므로 위 라벨대로 제출 자체가 닫혀 있음.

스크린샷: `devgate-shots/gate2_03_game_over.png` (GAME OVER 도달 직후), `devgate-shots/gate2_04_game_over_settled.png` (버튼 클릭 전), `devgate-shots/gate2_05_after_submit_click.png` (클릭 후 — 04와 동일)

### 4. DAILY 표기

`?v=gate3` 로드 → 타이틀 스크린샷 확인: 좌측 버튼 열 2번째 자리에 `DAILY CHALLENGE` / `08-01 · GLOBAL SEED` 2줄 라벨 정상 노출.

해당 버튼(좌측 2번째, 캔버스 로컬 ref 좌표 중심 약 `rx=-245, ry=205`) 클릭 → 전투 진입. 진입 직후 200ms 간격 연속 캡처로 배너 노출 구간(BannerSeconds=2.2초)을 포착:

- 좌상단 STAGE 표시 바로 위에 앰버 `DAILY` 뱃지 상시 노출 확인.
- 첫 바이옴 배너("BIOME 1 - SCRAPYARD") 상단에 `DAILY CHALLENGE` 윗줄이 함께 표시됨 (첫 배너에만 붙는 사양대로).

스크린샷: `devgate-shots/gate3_00_title.png` (타이틀 2줄 라벨), `devgate-shots/gate3b_03_t1000ms.png` (DAILY 뱃지 + 배너 윗줄 동시 확인)

### 5. 회귀 스모크 (DAILY 런)

같은 4번 세션에서 배너가 사라진 뒤(진입 약 6초 후) 스크린샷: 하단 HUD 6칸 게이지(SPEED/SHOT/MISSILE/DOUBLE SHOT/OPTION/SHIELD, 각 LV0) 정상 렌더링, 좌하단 `SHIELD x1` 표기 정상, 좌상단 `DAILY` 뱃지도 계속 유지됨. STAGE 1/5, ADVANCE > mid-boss 등 상단 HUD 요소 모두 정상.

스크린샷: `devgate-shots/gate3_05_hud_regression.png`

## 발견된 문제

없음. 5개 항목 모두 결함 없이 통과.

## 참고 관찰 (문제 아님)

- 게이지 슬롯은 "헤어라인 셀"(테두리만 있는 스프라이트)이라 앰버 커서 색이 슬롯 안쪽 채움이 아니라 **테두리(특히 위쪽 변)**에만 뚜렷하게 나타난다. 자동 픽셀 프로브를 만들 때 이 점을 몰라 처음엔 오탐(0으로 판정)이 있었으나, 스크린샷 육안 대조로 실제 커서 이동을 확실히 확인했다.
- 첫 바이옴 배너의 `DAILY CHALLENGE` 윗줄은 `BannerSeconds=2.2초` 동안만 보인다. 전투 진입 후 2.5초 이상 지나 첫 스크린샷을 찍으면 이미 사라진 뒤라 놓친다 — 진입 직후 200~300ms 간격의 연속 캡처가 필요했다.
- CAPSULES 스탯이 2로 집계된 것은 F9 치트(3회) + 실제 드롭 캡슐 일부 획득이 섞인 결과로 보이며, 제출 차단 판정과는 무관.
