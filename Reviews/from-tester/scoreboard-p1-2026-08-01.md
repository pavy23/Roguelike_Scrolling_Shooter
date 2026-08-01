# 글로벌 스코어보드 P1 실플레이 검증 (배포 게이트)

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html?v=tester1`
- API: `https://rss-scoreboard.coreboard.workers.dev`
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 스크린샷 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\tester-shots\`
- 검증 스크립트: 같은 scratchpad 경로의 `step1_title.js` ~ `step4_submit.js`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 타이틀 RANKING 버튼 노출 | PASS |
| 2 | 랭킹 모달 (DAILY RANKING + PAVY 항목 로드) | PASS |
| 3 | CLOSE로 모달 닫힘 | PASS |
| 4 | 게임오버 SUBMIT SCORE → RANK #n | PASS |
| 5 | 서버 반영 (all 보드에 TESTER 등장) | PASS |
| 6 | 회귀 스모크 (HUD 6게이지 + SHIELD) | PASS |

**판정: 전 항목 PASS — 배포 가능.**

---

## 상세

### 1. 타이틀 RANKING 버튼

타이틀 로드(캔버스 width>300 대기 + 18초) 후 스크린샷 확인. 우측 NEW SEED 카드 바로 아래 RANKING 버튼이 정상 노출됨. 캔버스 로컬 좌표(1300x731 기준) 중심 약 `(1168, 465)`.

스크린샷: `tester-shots/title_canvas.png`, `tester-shots/title_full.png`

### 2. 랭킹 모달

RANKING 클릭 → 3초 대기 → "DAILY RANKING" 타이틀과 `1 PAVY 123,450` 항목이 로드됨 확인. OFFLINE 표시 없음 (CORS 정상 동작).

스크린샷: `tester-shots/ranking_modal.png`

### 3. CLOSE

CLOSE 버튼(모달 로컬 좌표 중심 약 `(650, 565)`) 클릭 후 모달이 사라지고 타이틀 화면으로 정상 복귀 확인.

스크린샷: `tester-shots/after_close.png`

### 4. 게임오버 제출

LAUNCH 클릭 후 방치(자동사격만, 이동 없음), F11(10초 스킵) 치트로 진행 가속. 5회 스킵(약 15초 실경과) 내 GAME OVER 패널 도달, SUBMIT SCORE 버튼 확인.

`window.prompt` 다이얼로그("스코어보드에 표시할 이름 (2~10자)")가 정상 발생했고 `page.on('dialog', ...)`로 "TESTER" 자동 수락됨. 클릭 1.5초 후 SUBMIT SCORE 버튼 라벨이 `RANK #1`로 즉시 전환됨 (SENDING... 단계는 캡처 간격보다 빨리 지나감 — 지연 없이 성공 응답).

- 최종 스코어: 410 (KILLS 4, CAPSULES 2, ACC 15.6%, run 1 stage 1)

스크린샷: `tester-shots/game_over_panel.png` (제출 전), `tester-shots/after_submit_1.5s.png` (RANK #1로 전환 확인), `tester-shots/after_submit_4.5s.png`, `tester-shots/after_submit_9.5s.png` (유지 확인)

### 5. 서버 반영

제출 직후 `GET https://rss-scoreboard.coreboard.workers.dev/v1/board/all` 조회:

```json
{"board":"all","entries":[{"n":"TESTER","s":410,"sd":"193015973","sh":"starter","d":"NORMAL","g":"KIA","h":"","t":1785562283123,"tk":"23a61b9c"}]}
```

TESTER 항목이 all 보드에 정상 반영됨 (데일리 런이 아니므로 daily 보드가 아닌 all 보드로 감 — 기획대로).

참고: 검증 시작 시점에 `/v1/board/daily`에는 기존 스모크 항목 `PAVY 123450`이 그대로 있었음 — 이번 TESTER 제출과 별개로 정상 유지.

### 6. 회귀 스모크

LAUNCH 3초 후 전투 화면 스크린샷. 하단 HUD에 6칸 게이지(SPEED / SHOT / MISSILE / DOUBLE SHOT / OPTION / SHIELD, 각 LV0)가 정상 렌더링. 좌하단 `SHIELD x1` 표기도 정상 노출. 스테이지 안내("STAGE 1/5 ADVANCE > mid-boss"), PAUSE, SELECT(치트/스킬 게이지) 등 상단 HUD 요소 모두 정상.

스크린샷: `tester-shots/combat_hud.png`

## 발견된 문제

없음. 6개 항목 모두 결함 없이 통과.

## 참고 관찰 (문제 아님)

- F11(10초 스킵) 치트가 정지 상태(이동 없음, 자동사격만) 플레이에서 사망까지 시간을 크게 단축시켜줌 — 5회 스킵(약 15초 실경과) 내 사망 도달. 향후 유사 검증 시 재사용 가능.
- SUBMIT SCORE → RANK #n 전환이 1.5초 캡처 간격 내에 이미 완료되어 SENDING... 중간 상태는 스크린샷으로 포착하지 못함 (응답이 빨라서 발생한 것으로, 결함 아님).
