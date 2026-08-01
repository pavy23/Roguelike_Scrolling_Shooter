# build20 장애물 배치 랜덤화 / 시드 UI 숨김 / 장애물 파괴 연출 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build20, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\b20\`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 장애물 배치 랜덤화 (시드별 배치, 동일 시드 재현) | PASS |
| 2 | 시드 UI 숨김 (일반 모드 숨김 / dev 모드 노출, RANKING 위치) | PASS |
| 3 | 장애물 사운드 | SKIP (헤드리스 무음 환경, §3 참고) — 시각 회귀(앰버 플래시·파괴 폭발)는 PASS |
| 4 | 회귀 (발진→전투→게임오버, 데일리 버튼, 랭킹 모달) | PASS |

---

## 사전 확인: `?seed=N` URL 파라미터는 존재하지 않음 (방법론 조정)

과제 지침은 `&seed=101/202/303` 같은 URL 쿼리로 시드를 고정하는 것을 전제했으나, 코드를 확인한 결과
`Assets/Scripts/Presentation/Battle/DevArgs.cs`의 `OverrideSeed`는 `Environment.GetCommandLineArgs()`
(즉 `--seed=N` 커맨드라인 인자)만 읽고 URL 쿼리는 전혀 파싱하지 않는다 (`TryReadArg`를 쓰지 않음).
WebGL 빌드에는 커맨드라인 인자가 없으므로 `?seed=101`은 **아무 효과가 없다** — 버그는 아니고 애초에
지원되지 않는 경로다 (F3 오버레이 안내 문구도 `--seed=N pins the seed`로 커맨드라인만 언급).

대신 build19에서 이미 검증된 **타이틀 화면 수동 시드 입력**(숫자 키 직접 타이핑, `TitleScreen.EditSeed`)
경로를 사용했다: 백스페이스로 기존 랜덤 시드를 완전히 지운 뒤(`_seedText` 최대 12자리, 14회 입력으로
안전하게 클리어) 목표 숫자를 입력. `?dev=1`에서만 동작하며, 타이틀 SEED 텍스트에 정확한 값이 반영됨을
스크린샷으로 매번 확인했다 (`b20/seed101a/01_title_seed_set.png` 등 4장 — SEED 101/202/303/101 정확히 표시).
이 경로도 "시드로 런을 고정한다"는 기능적 요구를 동일하게 충족하므로 항목 1 검증에는 문제가 없다.

## 1. 장애물 배치 랜덤화 — PASS

시드 101(1차) / 202 / 303 / 101(2차, 재현성 확인용) 4개 런을 `?dev=1&god=1`로 진행, 각 런 발진 후
t=15/30/45/60초 지점에서 스크린샷 캡처 (`b20/seed101a/`, `b20/seed202/`, `b20/seed303/`, `b20/seed101b/`).

**동일 시드 재현성 (101a vs 101b):** t=15s, 30s, 45s 세 시점 모두 tick·score·장애물(대각선 줄무늬 파괴가능
블록) 위치·적 종류/위치·(t45에서는 미드보스 종류·위치까지) **픽셀 단위로 완전히 동일**했다.
- t15: 둘 다 `tick 900 score 220`, 동일한 6개 장애물 배치 (`b20/seed101a/t15.png` vs `b20/seed101b/t15.png`)
- t30: 둘 다 `tick 1800 score 2810`, 동일 위치의 미사일 폭발 이펙트까지 일치
- t45: 둘 다 `tick 810→` 화면에서 동일한 미드보스(로봇형) 동일 위치 조우

**시드 간 랜덤화 (101 vs 202 vs 303):** 같은 t=15s 시점(`tick 900`) 비교에서:
- seed101: 장애물(대각선 파괴가능 블록) 6개, 넓게 분산 배치, score 220
- seed303: 장애물 4개, 좁게 뭉친 배치(위치가 101과 명백히 다름), 적 종류(황금 벌형)도 다름, score 975
- seed202: 이 시점 프레임에는 장애물이 보이지 않음(적은 붉은 거미형 전용) — t45에서는 전차형 미드보스가 등장해 101/303의 미드보스와도 종류가 다름을 확인. "장애물 0개"도 배치 랜덤화의 정상적인 변주 범위로 판단(장애물 자체는 101·303 두 시드에서 명확히 재현됐으므로 지침의 "장애물이 안 나오면 다른 시드 추가" 조건에 걸리지 않음).
- t30/t45 비교에서도 세 시드 각각 스코어·틱 진행·적 구성·미드보스 종류가 서로 확연히 다름을 재확인.

코드 근거: `Assets/Scripts/Core/Generation/SegmentStageGenerator.cs`의 `JitterObstacles(Rng rng)` /
`obstacleJitterRng = stageRng.Fork(ObstacleJitterStream)`가 런 시드에서 파생된 스트림으로 장애물 위치를
지터링하므로, 시드가 같으면 완전히 결정론적으로 재현되고 시드가 다르면 다른 배치가 나오는 것이 설계된 동작이다 — 관측 결과와 정확히 일치.

스크린샷: `b20/seed101a/`, `b20/seed101b/`, `b20/seed202/`, `b20/seed303/` 각 폴더의 `01_title_seed_set.png`(시드 확정 크롭), `t15.png`/`t30.png`/`t45.png`/`t60.png`

## 2. 시드 UI 숨김 — PASS

`b20/recon_nodev_title.png` (쿼리 없음) vs `b20/recon_dev_title.png` (`?dev=1`) 비교:

- **일반 모드**: 우측 열에 `NEW SEED` 버튼도 `SEED : ...` 텍스트도 전혀 없음. `RANKING` 버튼이 그 빈자리를 채우며 위로 올라와(우측 열 첫 슬롯 위치) 렌더링됨 — 구멍 없이 자연스럽게 배치됨.
- **dev 모드**: `NEW SEED` 버튼 + `SEED : <난수>` 텍스트가 우측 열 상단에 정상 노출되고, `RANKING` 버튼은 그 아래(원래 두 번째 슬롯 위치)로 밀려 내려가 있음.

코드 근거(`TitleScreen.BuildTouchButtons`)와도 일치: 시드 블록은 `if (_seedUi)` 안에서만 생성되고
`rightY` 커서를 소비하는 방식이라, 릴리스에서 시드 블록이 빠지면 그만큼 `RANKING`이 자동으로 위로
당겨진다 — 지침이 우려한 "빈 구멍"이 생기지 않음. `_seedUi = DevArgs.DevMode`이고 `DevMode`는
WebGL에서 URL에 `dev=1`이 있을 때만 참이므로 게이팅 로직도 코드상 올바르다.

스크린샷: `b20/recon_nodev_title.png` (일반 모드, 시드 UI 없음), `b20/recon_dev_title.png` (dev 모드, 시드 UI 노출)

## 3. 장애물 사운드 — SKIP / 시각 회귀 PASS

**사운드 자체**: headless Chrome은 오디오 출력이 없어 실청취 검증이 불가능한 환경이므로 지침대로 SKIP 처리한다 (실청취는 사람 몫).

**시각 회귀(앰버 플래시·파괴 폭발)**: seed303 런에서 자동사격(오토파이어)이 배치가 밀집한 파괴가능
장애물 클러스터를 통과하도록 기체를 세로로 드래그하며 0.7초 간격 연속 캡처(`b20/item3/f00~f18.png`).

- `b20/item3/f10.png`: 파괴가능 장애물 클러스터 중 하나가 인접한 다른 블록들과 확연히 다른 **주황/앰버색으로
  틴트**된 것을 확인 — 코드의 `ObstacleHitFlashColor = (1, 0.72, 0.25)` (`BattleDirector.cs:98`)가 실제
  피격 프레임에 정확히 반영됨.
- `b20/item3/f02.png`, `f03.png`, `f07.png`, `f11.png`: 장애물 클러스터 자리에 `+80`/`+110`/`+160`/`+50`
  스코어 팝업이 연속 등장 — `ObstacleDestroyed` 이벤트의 `_scorePopups.Spawn(...)` 호출(`BattleDirector.cs:899-902`)이
  파괴가능 장애물이 줄어드는 시점과 정확히 일치해 발생. 프레임 간 장애물 개수가 6→5→4→3개로 줄어드는 것도 육안 확인됨.
- `b20/item3/f08.png`: 뚜렷한 주황색 폭발 스타버스트 이펙트(`+200` 팝업 동반) — `SpawnExplosion` 호출 경로가
  정상 작동함을 보여줌 (이 특정 프레임은 스케일상 적 처치 폭발일 가능성도 있으나, 장애물 파괴 경로도 동일한
  `SpawnExplosion` 함수를 0.9 스케일로 호출하므로 파괴 이펙트 자체의 존재는 코드·인접 프레임 증거로 충분히 확인됨).

코드와 스크린샷 두 경로 모두에서 앰버 히트플래시 + 파괴 폭발 + 스코어 팝업이 일관되게 확인되어, 시각적
회귀는 없다고 판단한다.

## 4. 회귀 — PASS

**발진→전투→게임오버** (`b20/item4/a00~a02*.png`, 쿼리 없음 일반 모드):
- 타이틀 정상, LAUNCH 클릭 후 전투 진입 정상 (HUD 6게이지, "TOUCH AND DRAG" / "AUTO FIRE IS ON" 안내 정상, 이 화면에서도 장애물이 자연스럽게 렌더링됨).
- 약 37초 자연사 → GAME OVER 패널에 SCORE/KILLS/ACC/SHOTS/BEST COMBO/GRAZE/BOMBS 정상 표시, REDEPLOY/TITLE/SUBMIT SCORE 버튼 정상.

**데일리 버튼** (`b20/item4/b03_after_daily.png`):
- 타이틀에서 `DAILY CHALLENGE` 버튼 클릭 → 좌상단 라벨이 `run N` 대신 `DAILY`로 표시되며 정상 전투 진입 확인.

**랭킹 모달** (`b20/item4/b01_ranking_modal.png`, `b02_after_close.png`):
- `DAILY RANKING` 헤더, `# PILOT SCORE STG SHIP BOMB` 컬럼 헤더 + 앰버 헤어라인 룰, BOMB 열 앰버 색상(`0`) — build19에서 재편된 헤더 형식이 그대로 유지됨을 확인.
- CLOSE로 정상 닫힘 → 일반 모드 타이틀로 복귀(시드 UI 없음, RANKING 정상 위치) 확인.

---

## 종합

- 배포에 영향을 주는 **결함(FAIL)은 발견되지 않음.**
- 항목 1(장애물 배치 랜덤화)·항목 2(시드 UI 숨김) 모두 코드 설계와 실측이 정확히 일치하며 PASS.
- 항목 3은 사운드 실청취만 환경 제약으로 SKIP, 시각 회귀(앰버 플래시·파괴 폭발·스코어 팝업)는 코드·스크린샷 양쪽에서 확인되어 PASS로 판단.
- 항목 4 회귀 전부 정상.
- 참고: `?seed=N` URL 쿼리는 WebGL 빌드에서 실제로 아무 효과가 없다(커맨드라인 전용 경로) — 향후 QA 지침 작성 시 타이틀 수동 시드 입력(`?dev=1` + 숫자 키 타이핑) 경로를 기준으로 삼는 것을 권장한다.
