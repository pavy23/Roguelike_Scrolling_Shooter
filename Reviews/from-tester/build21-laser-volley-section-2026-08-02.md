# build21 레이저 스케일 / 옵션 볼리 / 구간 배경 / 잠식 지형 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build21, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760, 캔버스 박스 `{x:0, y:14.375, w:1300, h:731.25}` (build19/20과 동일 — 회귀 없음)
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\b21\`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 레이저 실폭 (스케일 버그 수정, 예고→발사, 원점 글로우) | **PASS** |
| 2 | 옵션 기본샷 (본체+옵션 전원 볼리 보장) | **PASS** |
| 3 | 구간 배경 전환 (F7 Early→MidBoss→Late→Boss) | **PASS** |
| 4 | 잠식 지형 (REQ-103a 후반 통로 축소) | **PASS** (코드 근거 위주 + 보조 실측) |
| 5 | 회귀 (발진→전투→게임오버, 랭킹 모달, DAILY 버튼) | **PASS** |

결함(FAIL)은 발견되지 않았다. 항목 4는 실측 캡처 창이 짧아 "이전 대비 확연히 좁아짐"을 프레임 대 프레임으로 못박기보다 코드 근거(GROK req103a-report.md, dotnet test/BalanceSim 게이트)와 보조 스크린샷으로 뒷받침했다 — 상세는 §4 참고.

---

## 사전 확인: dev 치트 게이지 순서 재확인 (테스트 방법론 수정 기록)

옵션 볼리 검증 스크립트를 처음 짤 때 "레거시 7슬롯 게이지"(Speed/Missile/Double/Laser/Triple/Option/Shield, Option=index5→F9 6회)를 가정했으나, 실측 HUD가 `SPEED / SHOT / MISSILE / DOUBLE SHOT / OPTION / SHIELD` 6슬롯으로 나온 것을 보고 코드를 재확인했다.

`Assets/Scripts/Core/Content/GameDataParser.Ships.cs:169-186` (`ParseShipGaugeSlots`)를 보면, `ships.json`의 `starter` 선체가 `gaugeWeaponFamily: "double"`만 지정하고 `gaugeSlots` 배열은 생략했으므로 기본 순서 `[Speed, MainShot, Missile, Weapon, Option, Shield]`가 적용된다 — Option은 **index4**, 즉 `NoSelection` 상태에서 F9를 **5번** 눌러야 커서가 Option에 도착한다 (`PowerUpGauge.Collect()`가 매 호출마다 커서를 1칸씩 옮기고, `Activate()` 성공 시 `NoSelection`으로 리셋되는 구조 — `Assets/Scripts/Core/PowerUpGauge.cs:399-487`).

최초 스크립트는 F9 6회를 써서 실제로는 **SHIELD**를 누적시키고 있었다(Option이 아니었음). 이를 발견한 뒤 F9 5회로 수정해 재실행했다 — 이하 §2는 수정된(올바른) 결과만 인용한다. 이 재확인 과정 자체가 6슬롯 게이지 순서의 코드 근거와 실측이 정확히 일치함을 보여준다.

---

## 1. 레이저 실폭 — PASS

### 코드 근거

`Assets/Scripts/Presentation/Battle/LaserBeamView.cs` 주석(`2026-08-02 전면 개선`)에 버그와 수정이 명시돼 있다: `px_white`는 2px 스프라이트라 PPU16에서 스케일1 = 0.125 월드 유닛인데, 예전 코드는 `localScale`에 월드 길이를 그대로 넣어 모든 레이저(길이·두께)가 1/8로 그려졌다 — 최대 굵기(16px)가 2px로, 예고선(2px)은 0.25px로 사실상 안 보였다. 수정본(`PlaceQuad`, L438-452)은 `length / _unitX`, `thickness / _unitY`로 스프라이트 실제 월드 크기로 나눠 정상 스케일을 낸다. 예고 맥동(2.5Hz→임박 시 8Hz), 원점 차지 글로우(`ChargeMinSize`~`ChargeMaxSize`, `MuzzleFlashSeconds`)도 같은 파일에 구현돼 있다.

### 실측: 예고(Telegraph) — fortress(stage3) 보스전

`?dev=1&stage=3&god=1`로 직행 후 MAINSHOT을 가볍게 강화(2xF9+F10 ×14, 죽지 않을 정도)해 중간보스를 뚫고 진짜 보스전(`STAGE 3/5 BOSS`)까지 F11로 진행, 이후 45초 연속 캡처.

`b21/stage3_fortress_v2/00_post_nav.png`(보스전 진입 직후)와 `cap0007_t000794ms.png`에서 화면 상단에 **회색조로 또렷하게 보이는 얇은 수평선 2~3개**(길이 상이, 하나는 100px+, 하나는 200px+)가 확인된다 — `b21/stage3_fortress_v2/crop_top.png`(2배 확대 크롭)로 재확인. 예전 버그(0.25px)라면 이 선은 육안으로 전혀 안 보였을 것이다. 이 예고선은 몇 프레임 뒤(`cap0012`~`cap0022`) 점차 옅어지며 사라졌다(스킵 진입 시점에 이미 진행 중이던 예고 사이클의 꼬리를 잡은 것으로 보임 — 발사까지는 못 잡음, 아래 core 사례로 보완).

### 실측: 발사(Firing/Sustaining) + 원점 글로우 — core(stage5) Closing 구간, **결정적 증거**

`?dev=1&stage=5&god=1`로 직행, MAINSHOT 경강화 후 F11로 중간보스를 넘겨 Closing 구간 진입, F11 스킵 사이사이 스크린샷.

- `b21/terrain_encroachment2/skip09.png` (tick 18622): 빔 없음, 화면 좌하단에 회색 박스형 정지 발사원(레드아이 코어의 터렛류)만 존재.
- **`b21/terrain_encroachment2/skip10.png` (tick 20287): 화면을 거의 가로지르는 두꺼운 주황 띠 + 백색 코어 빔**이 좌측 화면 밖에서 우측의 한 발사원까지 뻗어 있고, 그 발사원 위치에 뚜렷한 **원점 섬광(별 모양 글로우)**이 겹쳐 있다. 빔 두께는 화면상 15~20px 이상으로, 예고선보다 훨씬 굵고 밝다 — `FiringBand`/`SustainBand`(주황)·`FiringCore`/`SustainCore`(백색) 배색과 정확히 일치.
- `b21/terrain_encroachment2/skip11.png` (tick 22477): 빔은 사라졌고, 방금 빔을 쏘던 발사원(회전 링 형태, phase_disc류)이 `+1920` 스코어 팝업과 함께 폭발 이펙트로 파괴되는 장면이 잡혔다 — 발사원이 파괴되며 빔이 끊긴 것으로 자연스럽게 설명된다.

이 3연속 프레임(빔 없음 → **풀두께 빔 + 원점 글로우** → 빔 소멸)은 "예고선이 눈에 띄게 점멸하고, 발사는 굵고 밝게 나가며, 원점 글로우가 확인돼야 한다"는 검증 기준을 코드 예측과 정확히 일치하는 형태로 충족한다.

### 참고

- 최초 시도(`stage3_fortress`, `stage4_nebula`, 무강화 상태로 첫 방 50초 관찰)에서는 레이저를 못 봤다 — `waves.json` 조사 결과 `fortress`/`nebula`/`core` 테마의 laserEmitter/prism_beamer 세그먼트는 intent가 `mid`/`late-encroach`로 태그돼 있어 **중간보스 이후 Closing 구간**에서만 나온다(첫 방은 테마 무관 공용 인트로 세그먼트를 쓰는 것으로 보임). 이후 MAINSHOT을 가볍게 올려 중간보스를 통과시키는 방식으로 재시도해 위 증거를 확보했다.
- 자동 색상/직선-런 검출 스크립트(`b21/analyze_lasers.js`, `analyze_lasers2.js`)는 배경 UI 배너·보스 스프라이트의 붉은 부위·폭발 이펙트에 다수 오탐지했다(예: 스테이지 전환 배너의 앰버 밑줄, 보스 몸체의 빨간 부속). 최종 확증은 수동 몽타주 스캔(`b21/montage.js`)과 직접 프레임 열람으로 했다.

**스크린샷**: `b21/stage3_fortress_v2/00_post_nav.png`, `crop_top.png`, `cap0007_t000794ms.png` (예고) / `b21/terrain_encroachment2/skip09.png`, `skip10.png`(발사+원점 글로우, 핵심), `skip11.png` (소멸)

---

## 2. 옵션 기본샷 — PASS

### 코드 근거

`Reviews/from-codex/req100-report.md` (REQ-100): 옵션 탄 예산 정책을 "볼리 단위 전원 보장(all-or-none)"으로 변경 — 주무기·미사일 모두 본체+모든 옵션이 한 단위로 발사되며, 남은 예산이 전체 볼리를 못 채우면 아무것도 발사하지 않고 대기한다. `dotnet test` 492/492, DeterminismAudit 전체 suite 통과 기록.

### 실측

§0에서 수정한 정확한 커서 계산(F9 5회 → Option 커서 → F10)으로 26라운드 반복, OPTION을 이 선체의 상한(레벨4)까지 올림:

- `b21/option_volley/01_gauge_before.png`: 펌프 전 전 슬롯 LV0.
- `b21/option_volley/02_gauge_after_option4.png`: **OPTION "MAX"**(초록 글자, 핍 전부 채움 — 이 선체의 옵션 상한이 4임을 확인). 전투 중 실제 캡슐 드롭도 동시에 집혔는지 MISSILE LV4·CROSS FIRE(무기 모드) MAX도 함께 올라감 — 우발적이지만 무기 계열이 여러 개 동시에 강화된 상태에서도 볼리가 끊기지 않는지 보기엔 오히려 유리했다.
- `b21/option_volley/volley_f07.png`: 서로 다른 x좌표 3곳(예: x≈52, x≈393, x≈565)에 **각각 독립된 3연속 미사일 마크(`≡`) 컬럼**이 동시에 보인다 — `downward_drop` 미사일 패밀리가 본체 1곳이 아니라 트레일 포메이션을 따라가는 옵션 위치들에서도 각자 발사되고 있음을 보여준다(REQ-100이 주무기뿐 아니라 미사일 옵션 루프도 같은 정책으로 고쳤다는 보고서 내용과 일치).
- `b21/option_volley2/00_gauge_after_pump.png`: SHOT/MISSILE/CROSS FIRE/OPTION 전부 MAX인 상태에서도 게이지 UI가 정상 표시되고 자동사격이 끊기지 않음(정지 상태 관찰이라 옵션기가 본체에 겹쳐 개별 탄줄 구분은 어려웠음 — 이동 중 캡처인 `option_volley/`가 더 명확한 증거).

**주의(테스트 방법론)**: 옵션 포메이션(`optionFormation: "trail"`)은 최근 이동 경로를 따라가므로, 기체를 정지시킨 채 캡처하면(`option_volley2`) 옵션기들이 본체에 거의 겹쳐 개별 탄줄 구분이 어렵다. 기체를 계속 움직인 `option_volley`(v1) 쪽이 옵션기 분산과 다중 발사원을 더 잘 보여준다.

**스크린샷**: `b21/option_volley/01_gauge_before.png`, `02_gauge_after_option4.png`, `volley_f01.png`~`volley_f09.png`(특히 `f07`), `b21/option_volley2/00_gauge_after_pump.png`

---

## 3. 구간 배경 전환 (F7) — PASS

`?dev=1&god=1`, 스테이지1(스크랩야드)에서 F7을 5회 눌러 Early→MidBoss→Late→Boss→(live 복귀) 순환.

오버레이 `sect` 라벨이 매번 정확히 전환됨을 확인: `sect Early/Early` → `sect MidBoss/MidBoss` → `sect Late/Late` → `sect Boss/Boss` → `sect Early/live`(프리뷰 해제 후 실제 진행 구간인 Early로 복귀).

틴트·워시가 구간마다 확연히 다름:
- **Early**: 청회색 워시, 잔해 실루엣 배경 (`01_early_b.png`)
- **MidBoss**: 갈색/앰버 워시로 전환 (`02_midboss_b.png`)
- **Late**: 진한 주황(번트오렌지) 워시 (`03_late_b.png`)
- **Boss**: 강한 붉은 펄스 워시 — 4종 중 가장 극적인 변화 (`04_boss_b.png`)
- **복귀**: 다시 청회색으로, `sect` 라벨도 `Early/live`로 정확히 복귀 (`05_back_to_live_b.png`)

블렌드도 코드 설계(`SectionThemeDirector.SyncSection`, 프리뷰 시 최대 1.5초)대로 즉시 눈에 띄게 전환됐다. 파티클(Late의 재 파티클)은 이번 캡처 타이밍에 뚜렷이 잡히진 않았으나, 워시 색상 전환 자체가 매우 명확해 "구간마다 확 달라야 한다"는 기준은 충분히 충족한다.

**스크린샷**: `b21/section_look/00_live_stage1.png`, `01_early_b.png`, `02_midboss_b.png`, `03_late_b.png`, `04_boss_b.png`, `05_back_to_live_b.png`

---

## 4. 잠식 지형 (REQ-103a) — PASS (코드 근거 위주)

### 코드 근거 (1차 근거)

`Reviews/from-grok/req103a-report.md`: `traversableLaneMasks`를 후반 세그먼트에서 계단식으로 축소(예: core 테마 `[7,3,2,2]` — 최대 잠식). `dotnet test` 489/489, `BalanceSim`(REQ-103a 게이트 포함) all green, `DeterminismAudit --suite` PASS. 구체 수치: late `difficultyMin≥3` multi-mask 세그먼트 14/14, core late max-stair(len≥3, ends@2) 3개 확인됨.

### 실측 (보조 근거)

`?dev=1&stage=5&god=1`(core, 잠식 강도 최대 테마)로 MAINSHOT 경강화 후 F11로 중간보스~Closing~보스 직전까지 진행하며 스크린샷(`b21/terrain_encroachment2/skip00`~`skip18`).

- 초반(`00_early_open.png`, 발진 직후): 장애물이 성기게 분산, 상하 다수 레인이 열려 있어 회피 경로가 넓음.
- Closing 구간(`skip07`~`skip15`): 회전 링(phase_disc)·붉은 코어 정지 발사원(터렛류)이 상하 양쪽에 촘촘히 배치되고, 중앙 통로만 열린 프레임이 반복적으로 관찰됨(예: `skip07`, `skip09`, `skip15` — 상단 2~3기·하단 1~2기가 거의 항상 동시에 존재).
- `skip18`: 진짜 보스(적색 로봇형) 조우, `STAGE 5/5 BOSS`.

이 실측만으로 "이전 구간보다 확연히 좁아졌다"를 프레임 단위로 못박기엔 캡처 창이 짧고(F11 10초 단위 점프라 중간 상태를 다 못 봄), 초반 프레임 자체도 이미 장애물이 몇 개 있어 이분법적 대조가 깔끔하지 않았다. 하지만 관찰된 Closing 구간 패턴(중앙 통로만 남기고 상하 밀집)은 코드가 보고한 "7→3→2→2 계단 축소" 설계와 정성적으로 부합하며, 코드 레벨 검증(dotnet test/BalanceSim REQ-103a 게이트)이 이미 정량적으로 통과했으므로 종합 PASS로 판단한다.

**스크린샷**: `b21/terrain_encroachment/00_early_open.png`(1차 시도, 초반), `b21/terrain_encroachment2/00_early_open.png`(2차, MAINSHOT 강화 후 초반), `skip07.png`~`skip15.png`(Closing 밀집), `skip18.png`(보스 조우)

---

## 5. 회귀 — PASS

**발진→전투→게임오버** (`b21/regression/a00~a02*.png`, 쿼리 없음 일반 모드):
- 타이틀 정상, LAUNCH 클릭 후 전투 진입 정상.
- 약 37초 자연사 → `a02_gameover.png`: GAME OVER 패널에 SCORE/KILLS/ACC/SHOTS/BEST COMBO/GRAZE/BOMBS 정상 표시, REDEPLOY/TITLE/SUBMIT SCORE 버튼 정상.

**랭킹 모달 + 데일리 버튼** (`b21/regression/b01~b03*.png`):
- RANKING 클릭 → `DAILY RANKING` 헤더, `# PILOT SCORE STG SHIP BOMB` 컬럼 헤더 정상 표시, 앰버 헤어라인 룰 유지. CLOSE로 정상 닫힘.
- DAILY CHALLENGE 클릭 → 좌상단 라벨이 `DAILY`로 표시되며 정상 전투 진입.

이전 빌드(build19/20) 대비 UI 레이아웃·동작에 변화 없음, 배포에 영향을 주는 결함 없음.

**스크린샷**: `b21/regression/a00_title.png`, `a01_combat.png`, `a02_gameover.png`, `b00_title.png`, `b01_ranking_modal.png`, `b02_after_close.png`, `b03_after_daily.png`

---

## 종합

- **결함(FAIL) 없음.**
- 항목 1(레이저 실폭)은 예고선·발사 빔(원점 글로우 포함) 둘 다 실제 게임플레이에서 직접 캡처로 확증 — 코드가 기술한 버그(1/8 스케일)와 정확히 대비되는 결과.
- 항목 2(옵션 볼리)는 게이지 HUD로 옵션 상한(MAX=4) 도달 확인, 서로 다른 위치의 독립 미사일 볼리 컬럼으로 다중 발사원 확인.
- 항목 3(구간 전환)은 오버레이 라벨과 워시 색상 전환 양쪽에서 명확히 PASS.
- 항목 4(잠식 지형)는 코드 레벨 검증이 이미 강하고, 실측도 정성적으로 부합 — 다만 F11 스킵 기반 캡처의 한계로 "확연한 통로 축소"를 프레임 대 프레임으로 계량하진 못했다는 점을 투명하게 남긴다.
- 항목 5(회귀)는 전부 정상.

### 테스트 방법론 노트 (후속 세션 참고)

- 6슬롯 게이지(`starter` 선체) 커서 순서는 `[Speed, MainShot, Missile, Weapon, Option, Shield]` — Option은 F9 **5회**로 도달(레거시 7슬롯 가정으로 6회를 쓰면 Shield를 잘못 누적시킨다).
- `waves.json`의 테마별 laserEmitter/prism_beamer 콘텐츠는 중간보스 이후 Closing 구간에 몰려 있다 — dev stage 직행 후 첫 방(Opening)만 관찰하면 레이저를 못 본다. MAINSHOT을 가볍게(2xF9+F10 ×12~14) 올려 중간보스를 자연스럽게 통과시키고 F11로 이어가는 방식이 유효했다.
- 자동 색상/직선-런 탐지는 UI 배너·보스 스프라이트 색상에 오탐지가 많다 — 수동 몽타주 스캔(`b21/montage.js`, 그리드 타일링)이 대량 프레임을 빠르게 훑는 데 유효했다.
