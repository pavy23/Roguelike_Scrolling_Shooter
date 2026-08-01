# build19 랭킹 모달 재편 / SHOT 6레벨 / 레이저 빔 그로우 / 수동 시드 제출 차단 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build19)
- API: `https://rss-scoreboard.coreboard.workers.dev`
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\b19\`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 랭킹 모달 재편 (헤더/컬럼 정렬/앰버 BOMB 0) | PASS (관찰사항 1건, §1 참고) |
| 2 | SHOT 6레벨 (LV5→LV6/MAX, 핍 6개) | PASS |
| 3 | 레이저 빔 그로우 (예고선→원점에서 확장) | SKIP (미재현 — §3 참고) |
| 4 | 수동 시드 제출 차단 + 리롤 복구 | PASS |
| 5 | 회귀 (발진→전투→게임오버, 무기 발사) | PASS |

---

## 1. 랭킹 모달 재편 — PASS

타이틀 → RANKING 클릭 → "DAILY RANKING" 모달 확인.

- 헤더 줄 `# PILOT SCORE STG SHIP BOMB` + 앰버 헤어라인 룰이 정상 노출.
- 본문 3개 항목 모두 고정폭 컬럼으로 헤더와 정확히 정렬됨 (컬럼 어긋남 없음).
- BOMB 0은 앰버색으로 렌더링됨 (`QA-DAILY`, `pavy` 두 항목 모두 `0`이 amber).
- 구 항목(`1 PAVY 123,450`)은 STG·BOMB 칸이 `-`로 정상 폴백.

**관찰사항 (FAIL 아님, 참고용):** 위 `PAVY` 항목은 STG=`-`, BOMB=`-`인데 SHIP 칸만 `-`가 아니라 `ST`(starter)로 표시됨. 세 칸이 모두 `-`일 것으로 기대했으나(과제 지침 문구 기준) 실제로는 SHIP만 채워져 있음. `/v1/board/all` API 조회 결과 이 정확한 레코드(123,450점)는 조회되지 않아 원본 데이터 구조를 직접 확인하지 못했다 — 순수 무-컨텍스트 레거시(예: 필드 자체가 없음, `all` 보드의 `TESTER` 항목처럼)인지, 아니면 `sh` 필드는 있고 `st`/`rm`만 없는 부분-마이그레이션 데이터인지 구분이 안 됨. 후자라면 SHIP=`ST` 표시가 정확한 동작이므로 버그가 아닐 수 있음. 컬럼 정렬 자체는 완벽하므로 PASS 처리하되, 후속 확인이 필요하면 완전 무-컨텍스트 레코드(`sh` 필드 자체가 없는 것)로 재현 요망.

스크린샷: `b19/04_ranking_modal.png` (전체), `b19/04_crop.png` (3배 확대 크롭 — 헤더/컬럼/앰버 0 확인용), `b19/r2_crop.png` (재확인)

## 2. SHOT 6레벨 — PASS

`?dev=1&stage=1&god=1`로 스테이지 1 직행+무적 진입. 최초 F9/F10 조작으로 게이지 커서 동작을 먼저 리버스엔지니어링:

- **F9**: 미선택 상태에서 눌러 SPEED→SHOT→MISSILE→DOUBLE SHOT→OPTION→SHIELD 순으로 커서가 1칸씩 순환 이동 (하이라이트 앰버 테두리로 확인).
- **F10**: 현재 커서가 있는 슬롯에 캡슐 1개를 적용(레벨+1). 캡슐이 없는 상태에서 연속 F10을 눌러도 무동작(no-op)이며, 백그라운드 실전투(오토파이어)로 인한 실제 캡슐 드롭도 커서를 밀어낼 수 있어 매 시도 전 커서 위치를 스크린샷 픽셀 검사로 재확인 후 진행.

SHOT 슬롯에 F9(커서 재조준)+F10(적용)을 6회 반복한 결과:

- LV1→LV5까지는 흰색 텍스트 + 채워진 핍 개수만큼 파란 정사각형 표시 (`SHOT LV5`, 핍 5/6).
- **LV6에서 라벨이 `SHOT` → 초록색 `MAX`로 전환**, 6개 핍 전부 점등, 슬롯 테두리도 초록으로 강조됨. HUD가 LV5 다음 LV6(핍 6개)까지 정상 상승하고 MAX 표기가 정확히 6레벨에서 뜨는 것을 확인.

스크린샷: `b19/shot6final3/level_5_reached.png` / `crop_lvl5.png` (LV5, 핍 5/6), `b19/shot6final3/level_6_reached.png` / `crop_lvl6.png` (LV6 MAX, 핍 6/6, 초록 강조)

## 3. 레이저 빔 그로우 — SKIP (미재현)

### 시도 내역

1. **stage=2 (일반 레이저 적 구간)**: `?dev=1&stage=2&god=1`로 진입, F3로 dev 오버레이 숨김 후 0.05~0.1s 간격 연속 캡처.
   - 실시간(F11 스킵 없음) 14초 구간 캡처(150프레임) — mid-boss 진입 전 corridor, 라인 형태 공격 없음.
   - 실시간 45초 구간 재캡처(510프레임, `montage_stage2long.png`) — corridor를 자연 스크롤로 통과, mid-boss(백색/시안 스파크형 적) 조우까지 포함. 다양한 벌레형 적·탄막 확인되나 레이저 빔은 관찰되지 않음.
   - F11(god 모드라 안전) 기반 헌팅으로 mid-boss "MID-BOSS DOWN - QUICK PICK" 카드 픽까지 2회 도달 — 카드 픽 화면 진입 시 상단에 정적인 빨간 HP 바(고정 길이, 프레임 간 불변)를 레이저로 오인한 예비 탐지가 있었으나 재검토 결과 보스 HP 바임을 확인(레이저 아님).
2. **stage=3 (보스 LaserGrid, 요새)**: `?dev=1&stage=3&god=1`로 진입.
   - corridor 22초 캡처(294프레임) — 로봇형 적(fortress 테마), 레이저 없음.
   - F11 헌팅으로 mid-boss(스파크형 적, 동일 계열) 조우 후 격파, 이어서 "STAGE 3/5 BOSS"(곤충형 적) 조우 성공.
   - 진짜 보스 조우 후 20초 연속 캡처(226프레임) + 42초 연속 캡처(491프레임, `montage_boss4.png`) 총 2회, 화력을 의도적으로 약하게 유지(보스가 오래 생존하도록)하며 관찰 — **보스가 관찰 구간 내내 사실상 무공격 상태**(HP 바가 거의 줄지 않고, 플레이어 탄 외에 화면에 적탄이 전혀 없음)였음. 레이저는커녕 일반 탄막도 발사하지 않음.

### 판정 근거

`?dev=1&stage=N&god=1` 스테이지 직행 경로로는 stage 2의 "일반 레이저 적"도, stage 3 보스의 LaserGrid 공격도 관찰 창(총 실측 약 2분 이상) 내에 한 번도 재현되지 않았다. 특히 stage 3 보스가 장시간 완전히 비공격 상태로 있었던 점이 눈에 띄는데, 이는 (a) 레이저 등 공격 패턴이 특정 트리거(플레이어 근접, 페이즈 HP 임계값, 스크립트 타이머 등)에 의존하고 이 조건이 dev 스테이지 직행 경로에서 충족되지 않았거나, (b) god/dev 플래그가 보스 AI의 공격 개시 자체를 억제하고 있을 가능성을 시사한다. 어느 쪽이든 이 경로로는 레이저 그로우 연출 자체를 관찰할 기회가 없었으므로 버그로 단정하지 않고 **SKIP** 처리한다. 재검증하려면 (1) god=1 없이 체력을 감수하며 정공법으로 보스까지 도달하거나, (2) 레이저 공격을 강제로 트리거하는 별도 dev 커맨드가 있으면 훨씬 빠를 것으로 보인다.

스크린샷/캡처 폴더: `b19/laser_stage2/`(corridor 14s), `b19/laser_stage2_long/`(corridor 45s, `montage_stage2long.png`), `b19/laser_hunt/`, `b19/laser_hunt2/`(mid-boss 카드픽, 정적 HP바 오탐 사례 포함), `b19/laser_stage3/`(corridor 22s), `b19/laser_stage3_boss3/boss_encounter_2.png`(보스 조우 확인샷), `b19/laser_stage3_boss4/`(보스 42s 관찰, `montage_boss4.png`)

## 4. 수동 시드 제출 차단 — PASS

타이틀에서 마우스 클릭 없이 숫자 키(`7 1 3 5 9`)를 눌러 시드 입력 시도.

- 시드 줄이 즉시 앰버로 바뀌며 `SEED : <숫자> [MANUAL SEED - NO SUBMIT]`로 전환됨 (`b19/seed_submit/crop_01seed.png`).
  - 참고: 숫자를 입력하면 기존 랜덤 시드 뒤에 자릿수가 **이어붙는** 방식이며, 총 12자리에서 더 이상 늘어나지 않도록 캡이 걸려 있음(추가 입력 무시). 백스페이스는 정상적으로 마지막 한 자리를 지움. 요청 범위 밖의 동작이라 FAIL 처리하지 않으나 참고용으로 기록.
- 치트 없이(non-dev URL) 발진 → 무입력 상태로 방치해 자연사(약 37초) → GAME OVER 패널에서 SUBMIT SCORE 버튼 자리에 **"SEEDED RUN - NO SUBMIT"** 문구가 비활성 스타일(다른 버튼과 달리 앰버 채움/챔퍼 없음)로 표시됨 (`03_gameover_seeded.png`).
- 해당 버튼을 클릭해도 아무 변화 없음 — `window.prompt` 다이얼로그도 뜨지 않고 패널 상태 동일 (`04_after_submit_click_seeded.png`, 클릭 전후 픽셀 동일).
- TITLE로 복귀 → **NEW SEED(리롤)** 클릭 → 시드가 다시 회색 비-앰버 표기로 복구, `[MANUAL SEED]` 문구 사라짐 (`06_after_new_seed.png`). 이때 좌측 하단에 "REPLAY" 버튼이 새로 생긴 것도 확인(직전 시드 재플레이용으로 추정, 회귀 이슈 아님).
- 리롤된 시드로 재발진 → 자연사(약 33초) → SUBMIT SCORE 클릭 → `window.prompt` 다이얼로그 정상 발생("QA-SEED" 자동 입력) → 버튼이 **"RANK #3"**으로 정상 전환 (`09_after_submit_rerolled.png`).
- 서버 측 확인: `GET /v1/board/all`에 `{"n":"QA-SEED","s":1250,"sd":"563993738", ...}` 항목이 정확히 1건 반영됨 — 시드 값(`563993738`)도 리롤 후 타이틀에 표시된 시드와 일치. 시드런 제출은 QA-SEED로 정확히 1회만 발생.

## 5. 회귀 — PASS

수동 시드 테스트 두 런 모두 non-dev URL(`?v=seedsub1`, 치트 없음)로 진행되어 자연스러운 회귀 검증을 겸함.

- "TOUCH AND DRAG - YOUR SHIP FOLLOWS YOUR FINGER" / "AUTO FIRE IS ON" 안내 정상 노출 (`02_combat_seeded.png`).
- 하단 6게이지(SPEED/SHOT/MISSILE/DOUBLE SHOT/OPTION/SHIELD) HUD 정상 렌더링, dev 오버레이 없음(정상 — non-dev URL이므로).
- 실시간 자연사 → GAME OVER 패널에 SCORE/KILLS/ACC/SHOTS/BEST COMBO/GRAZE/BOMBS 요약 정상 표시 (두 런 모두).
- 오토파이어(기본 무기 발사)가 두 런 모두 정상 동작하여 KILLS 8건씩 기록됨.

---

## 종합

- 배포에 영향을 주는 **결함(FAIL)은 발견되지 않음.**
- 항목 3(레이저 빔 그로우)은 dev 스테이지 직행 경로에서 stage 2/3 모두 총 2분 이상 관찰했음에도 레이저 공격 자체가 한 번도 트리거되지 않아 SKIP 처리. 특히 stage 3 보스가 장시간 완전 비공격 상태였던 점은 dev/god 경로의 특성일 가능성이 높으나, 필요시 정공법(비-god) 경로 재검증을 권장.
- 항목 1의 SHIP 칸 관찰사항은 버그 의심 신호가 약해 PASS로 유지했으나, 완전 무-컨텍스트 레코드로 재현 가능하면 한 번 더 확인할 가치가 있음.
