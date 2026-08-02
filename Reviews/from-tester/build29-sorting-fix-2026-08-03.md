# build29 검증 — 정렬 순서 수정(전경 실루엣 55→3, 전함 함체 13→4) + Interceptor B안

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build29, `Builds/Web`)
- 도구: puppeteer-core(headless Chrome, 1300×760) + pngjs. HP바/플레이어 가시성은 픽셀 카운트로 정량화하고, 핵심 판정 프레임은 전부 Read로 육안 재확인했다. dev 오버레이(시드/틱/warship/chain 텍스트)는 IMGUI라 DOM에서 못 읽으므로 스크린샷을 직접 판독한다.
- 시드: 2 (정순 테마 고정, 타이틀에서 수동 입력).
- 스크립트: `...\scratchpad\b29_*.js`. 산출물: `b29warship\`(St3 전함, 104장), `b29st1late\`/`b29st2late\`(St1/St2 후반 회귀, 각 23장), `b29hangar\`(격납고 4장), `b29regress\`(일반 런 10장).
- **방법론 메모(신규)**: `DevCheats.cs`의 F11(+10초 Core 시뮬레이션, 입력 없음, god 유지)을 오프닝/중간보스 구간 통과와 "바닥에 붙인 채 장시간 관측"에 적극 사용했다 — 실시간 대기 없이 동일한 틱 진행을 얻을 수 있어 build28 대비 관측 시간을 4배 이상(전함 단일 런 125초 동안 정지 없이 관측) 늘렸다. F11 프레스 자체가 화면을 깨거나 멈추지 않음을 확인했다(오버레이의 상단 "PAUSE" 박스는 실제 일시정지가 아니라 터치 모드의 상시 표시 버튼이다 — 처음엔 일시정지로 오인해 재확인함).

## 결과 요약

| # | 항목 | 결과 | 핵심 근거 |
|---|---|---|---|
| 1 | 전경 사각지대 해소 (St3 fortress) | **FAIL (재현)** | 보스룸 진입 직후(~t=2.6s) 플레이어가 화면에서 완전히 사라지고, 이후 122초(tick 90→7410) 동안 단 한 프레임도 재검출되지 않음. 육안 확인: 스크린샷 5장(009/010/023/024/104) 전부 플레이어 없음 |
| 2 | 전함 완주 (함미→포탑→코어) | **FAIL (재현)** | Core 오버레이 자체가 `warship stern 1/3 turret 4/4`로 122초 내내 완전 고정 — 픽셀 HP 추정치(776/2700)도 동일 구간 동안 노이즈(±16) 외 변화 없음. 함미조차 못 뚫어 포탑/함체/코어는 이번에도 관측 불가 |
| 3 | St1/St2 후반 사각지대 회귀 | **FAIL (동일 증상)** | St1(scrapyard mid-boss)·St2(hive mid-boss) 둘 다 "바닥 붙이기" 진입 직후 플레이어 0검출로 전환된 뒤 각각 27초·30초 관측 종료까지 재검출 0회. 단, **전경 실루엣 자체(지형/수풀 실루엣 띠)는 두 스테이지 모두 화면에 계속 그려짐 — 삭제되지 않고 배경으로 내려간 상태 확인** |
| 4 | Interceptor B안 | **PASS** | 소스 스프라이트(`Assets/Art/Sprites/ship_interceptor.png`, 2026-08-02 수정)를 8배 업스케일해 직접 확인 — 옥스블러드(적갈)색 동체 + 전진익(앞으로 꺾인 날개) 신규 디자인 확정. 격납고 인게임에서도 함선 #2(Interceptor, 미해금)로 슬롯이 정상 전환됨을 확인(크레딧 0이라 실루엣 틴트로 표시되는 것은 `HangarScreen.cs:280`의 의도된 동작이지 버그 아님) |
| 5 | 회귀 | **PASS** | 일반 런 자연사→GAME OVER 정상(`SCORE 00006530`, KILLS 19 등 통계 정상). 이번 세션 5개 puppeteer 실행(전함/St1/St2/격납고/회귀) 전부 콘솔 `[error]`/`[pageerror]` **0건** |

**최우선 결론**: build29의 정렬 순서 수정(`SectionThemeDirector.NearSortingOrder` 55→3, `WarshipView.HullOrder` 13→4)은 코드에 정확히 반영돼 있고 St1/St2/St3 전 구간에서 전경 실루엣 자체는 여전히 화면에 그려진다(항목3 확인). 하지만 **build28에서 보고된 핵심 증상 — "화면 하단으로 붙이면 기체가 영구적으로 사라지고, 그 뒤로 데미지가 전혀 들어가지 않는다" — 은 build29에서도 St1·St2·St3 세 곳 모두 100% 동일하게 재현된다.** 정렬 순서 수정만으로는 이 문제가 해결되지 않았다는 뜻이며, 근본 원인이 렌더 order가 아니라 **플레이어 위치 클램프(`BattleSim.ClampPlayerPosition`/`_playerMinY`/`_playerMaxY`)가 카메라 가시 영역보다 넓게 설정돼 있어, 기체가 화면 밖(비가시) 좌표까지 밀려날 수 있다는 쪽**일 가능성이 높다. §1에서 코드 근거를 남긴다.

---

## 1. 전경 사각지대 — FAIL, St1/St2/St3 전부 재현 (최우선)

### 1-A. 코드 확인: 정렬 순서 수정 자체는 반영됨

- `Assets/Scripts/Presentation/Battle/SectionThemeDirector.cs:63` — `public const int NearSortingOrder = 3;` (기존 55에서 하향 확인).
- `Assets/Scripts/Presentation/Battle/WarshipView.cs:45` — `const int HullOrder = 4;` (기존 13에서 하향 확인). 주석도 "예전에는 13이라 기체(10)·주무기탄(5)이 함체 판 뒤로 사라졌다"고 명시.

두 수정 모두 커밋대로 반영돼 있다. 문제는 이 수정이 실제 증상을 없애지 못했다는 데 있다.

### 1-B. St3 fortress — 보스룸 진입 직후 영구 소실, 122초 관측

`b29_warship.js`: 클린 키보드 내비게이션(build28의 `b28_warship_clean.js`와 동일 시퀀스, Digit1 모달 안전망)으로 보스 게이트까지 이동 후, 화면 최하단으로 밀착시키는 하강 편향 입력으로 덴스 캡처했다.

| 시점 | 경과 | tick | playerHits(픽셀) | 육안 확인 |
|---|---:|---:|---:|---|
| `001_geared_up.png` | 시작 직후 | 1440 | 148 (정상 크기) | 청록/파랑 제트기 뚜렷이 존재 |
| `007_recentered_boss_entry.png` | 보스 재진입 직전 | 4560 | 266 → 이후 소실 시작 | 소실 이미 시작 (화면 어디에도 없음, 육안 확인) |
| `009_dense_t01_2.6s.png` | 2.6s | 4740 | 0 | 없음 (육안 확인 — 적/기뢰만 존재) |
| `023_dense_t15_21.3s.png` | 21.3s (보스룸 진입, `sect Boss/live`) | tick1 | 0 | 없음 |
| `104_boss_final.png` | 124.9s (관측 종료) | 7410 | 0 | 없음 |

WARSHIP RUN DONE 로그: `elapsed=124.9s, hpMoved=true(노이즈 오탐), consoleErrors=0`. `hpMoved` 플래그는 픽셀 노이즈(788 vs 776, 임계값 3 초과)로 잘못 트리거된 것으로, 실제로는 776±16 범위를 벗어난 적이 없다(§2 참고).

**중요**: 이번 런은 build28의 클린 런보다 하강 편향을 더 강하게 줬다(400ms 하강/150ms 상승 비대칭). 소실 자체는 build28(WARNING 배너 부근 20~25초)보다 오히려 더 일찍(보스룸 진입 전, 나비게이션 단계에서 이미) 발생했다 — 정도가 나아지지 않고 오히려 같거나 더 나쁘다.

### 1-C. St1(scrapyard)·St2(hive) 후반 구간 — 동일 패턴

`b29_stage_late.js`(F11로 각각 tick 5000~8000대까지 진행 후 화면 최하단 밀착):

- **St1** (`b29st1late/`): `004_ff_t11_3.8s.png`(tick 미상, hits 73, 육안 확인 — 청록 제트기 존재)까지는 정상. 바닥 밀착 진입 직후인 `005_hug_bottom_t00_8.4s.png`부터 `023_final.png`(35.2초 경과, 총 19프레임)까지 **playerHits 0 연속** — 육안 확인(`005` 스크린샷)으로도 화면 어디에도 없음.
- **St2** (`b29st2late/`): 동일 패턴. `004_ff_t11_3.8s.png`(hits 78)까지 정상 → `005_hug_bottom_t00_8.5s.png`부터 `023_final.png`(35.7초 경과, 총 19프레임)까지 **playerHits 0 연속**.

두 스테이지 모두 이 문서 §0에서 서술한 build29 코드 변경(전경 실루엣 order 3, 전함 함체 order 4)이 적용된 채로 실행됐음에도 build28과 동일하게 재현된다 — 즉 **이 소실 버그는 St3 전함 전용이 아니라 일반 스테이지에도 있는, 정렬 순서와 무관한 별도 원인일 가능성이 큼**.

### 1-D. 그러나 전경 실루엣 자체는 삭제되지 않았다 (부분 PASS)

St1(`009_hug_bottom_t04_14.0s.png`)·St2(`009_hug_bottom_t04_14.0s.png`) 스크린샷 모두에서 화면 상/하단의 지형·수풀 실루엣 띠는 계속 렌더링되고 있다 — "배경으로 내려갔을 뿐 사라지지 않아야 한다"는 요구사항은 육안으로 충족을 확인했다.

### 1-E. 근본 원인 가설 (코드 근거)

`Assets/Scripts/Core/Simulation/BattleSim.cs:4294`의 `ClampPlayerPosition(position, delta, min, max)`가 `_playerMinY`/`_playerMaxY`(둘 다 `BattleConfig`에서 주입, 이번 조사에서 실제 값까지는 추적 못함)로 플레이어 Y를 제한한다. build28 보고서가 명시한 "y<-2.13에서 소실"이라는 구체적 월드 좌표와, 이번에 St1/St2/St3 전부에서 하강 입력을 오래 유지하면 예외 없이 영구 소실이 재현된다는 점을 종합하면, **이 클램프 범위가 카메라 가시 프러스텀보다 넓어서 기체가 "허용된 이동 범위" 안에 있으면서도 "렌더되는 화면 밖"으로 나갈 수 있는 구조적 여지가 있는 것으로 보인다.** 이번 빌드가 수정한 두 SpriteRenderer.sortingOrder 값은 같은 계열의 가림 버그(전경이 기체를 덮는 것)를 고치는 것이지, 애초에 기체가 화면 밖으로 나가는 것 자체는 막지 못한다 — 원인이 다른 레이어(Presentation 렌더 순서가 아니라 Core의 이동 클램프, 혹은 카메라 뷰포트 매핑)에 있을 가능성을 다음 조사자에게 넘긴다.

---

## 2. 전함 완주 — FAIL, 함미조차 못 뚫음

`b29warship/023_dense_t15_21.3s.png`(보스룸 진입, tick1)부터 `104_boss_final.png`(tick7410, 124.9초 경과)까지 오버레이 텍스트 자체가 **`warship stern 1/3   turret 4/4`로 완전히 고정**돼 있다(스크린샷 두 장 육안 대조 — `024_dense_t16_22.6s.png`와 `104_boss_final.png`가 이 필드에서 문자 그대로 동일). 이 문자열은 픽셀 추정이 아니라 `DevCheats.cs`가 `_director`(Core)에서 직접 읽는 값이라 오독 여지가 없다 — **122초 동안 포탑이 단 한 기도 격파되지 않았다.**

픽셀 기반 HP 추정치(hpSum, y=60~68 5행 합산, 최대 2700)도 동일 구간에서 776 ±16(노이즈) 범위를 벗어나지 않았다. `HP MOVED` 자동 감지가 한 번 오탐(788→776, 임계값 3 초과)했으나 이는 노이즈이지 실제 변화가 아니다.

§1-B/§1-E와 같은 원인(플레이어가 화면 밖으로 밀려나 자동사격이 함미에 닿지 않음)으로 추정된다. 함미 게이트를 뚫지 못해 포탑 분기(`way`)·함체 전진·코어전은 이번에도(build25~28에 이어) 전혀 관측하지 못했다.

---

## 3. Interceptor B안 — PASS

### 3-A. 소스 에셋 직접 확인 (가장 신뢰도 높은 근거)

`Assets/Art/Sprites/ship_interceptor.png`(2026-08-02 수정, build29 배치 당일)를 8배 업스케일해 직접 열람했다. **오래된 청회색 계열이 아니라 옥스블러드(짙은 적갈)색 동체, 앞으로 꺾인(전진익) 은회색 날개, 좌측 후방 주황 엔진 화염** — 지시된 "B안" 스펙과 정확히 일치.

### 3-B. 인게임 격납고에서 슬롯 전환 확인

`b29_hangar.js`: 새 프로필(로컬스토리지 비어있음) → 커서 기본 0(Starter, 해금됨) → `ArrowRight` 1회 → 커서 1(Interceptor). `02_ship1_interceptor.png`에서 하단 텍스트가 `Interceptor [LOCKED - 50,000 cr]  speed x1.25  start S0 M0 O0 B0`로 정상 전환됨을 확인했다.

미리보기 스프라이트 자체는 크레딧 0(미해금)이라 `HangarScreen.cs:280`의 의도된 동작(`_meta.IsUnlocked(ship.Id) ? Color.white : new Color(0.1f,0.12f,0.2f,0.9f)`)에 따라 거의 검은 실루엣으로 틴트돼 화면상 색상 확인은 불가능했다(크롭 `b29hangar/crop_ship1.png` 참고) — 이는 버그가 아니라 "미해금 함선은 실루엣으로" 표시하는 설계다. 진짜 색상 확인은 §3-A의 소스 에셋 직접 열람으로 대체했다.

**스샷**: `ship_interceptor_x8.png`(소스 에셋 8배 확대), `b29hangar/02_ship1_interceptor.png`(격납고 슬롯 전환).

---

## 4. 회귀 — PASS

- **일반 런**(`b29_regress.js`, dev 플래그 없음): 타이틀 → LAUNCH → 자연사 → GAME OVER 정상. `SCORE 00006530 (run 1, stage 1)`, `KILLS 19  CAPSULES 2  ACC 15.6%  SHOTS 282`, `BEST COMBO x8  GRAZE 3  BOMBS 0  HITS 3` 전부 정상 표시. REDEPLOY/TITLE/SUBMIT SCORE 버튼 정상(`b29regress/010_run_final.png`).
- **콘솔 에러**: 이번 세션 5개 puppeteer 실행(전함/St1/St2/격납고/일반 회귀) 전부 `[error]`/`[pageerror]` **0건**.

---

## 참고: 사용한 스크립트/자료

- `b29_measure.js` — build28의 `measureHp`/`b28_findship2.js` 판독 로직을 통합한 공용 모듈(`hpSum`, `findPlayer2`, `playerBounds` 추가)
- `b29_warship.js` — St3 전함 3막 검증. F11 롱홀 구간 포함, HP 변화 감지 시 실시간 관찰로 자동 전환하는 분기 내장
- `b29_stage_late.js` — St1/St2 후반 사각지대 회귀 (인자로 스테이지 번호 받음, F11로 고속 진행 후 바닥 밀착)
- `b29_hangar.js` — 격납고 함선 슬롯 전환(Starter→Interceptor→Bulwark→Interceptor) 스크린샷
- `b29_regress.js` — 일반 런 회귀 (build28의 `b28_regress.js`에서 OUT 경로만 변경)
- `crop_hangar.js` / `upscale_sprite.js` — 격납고 프리뷰 크롭, 소스 스프라이트 업스케일 유틸(신규)

## 다음 테스터/조사자에게

1. **정렬 순서 수정(order 55→3, 13→4)은 코드상 정확히 반영돼 있지만, build28이 보고한 "화면 하단에서 기체 영구 소실 + 데미지 무변화" 증상을 고치지 못했다.** St1(scrapyard)·St2(hive)·St3(fortress) 세 곳 모두 하강 입력을 일정 시간 이상 유지하면 100% 재현된다. 렌더 순서가 아니라 다른 레이어의 문제일 가능성이 높다.
2. 유력 후보로 `Assets/Scripts/Core/Simulation/BattleSim.cs:4294`의 `ClampPlayerPosition`과 그 인자인 `_playerMinY`/`_playerMaxY`(주입원은 `BattleConfig`, 이번 조사에서 실제 수치까지는 못 봄)를 지목한다. 이 클램프 범위가 카메라 가시 영역보다 넓다면, "허용된 이동 범위 안에 있지만 렌더는 안 되는" 사각지대가 구조적으로 생긴다. 다음 조사자는 (a) 이 클램프 값이 실제로 카메라 프러스텀보다 넓은지, (b) 넓다면 왜 그렇게 설계됐는지(예: 전함 함미처럼 화면 밖 파츠를 조준하기 위한 의도적 여유인지)부터 확인하면 좋겠다.
3. 전함 함미 게이트를 이번에도(build25~29 연속 5개 빌드) 뚫지 못해 포탑 분기·함체 전진·코어전은 여전히 전혀 관측하지 못했다. #1/#2가 고쳐지기 전까지는 이 구간을 볼 방법이 없다.
4. Interceptor B안(옥스블러드 전진익)은 에셋 레벨에서 확정 확인했다. 인게임에서 실제 색상까지 보려면 크레딧을 채워주는 dev 치트(현재는 없음)가 있으면 좋겠다 — 미해금 실루엣 틴트 때문에 다음에도 같은 우회(소스 에셋 직접 열람)가 필요할 것이다.
5. **방법론 팁**: `DevCheats.F11`(+10초 Core 시뮬레이션, 입력 없음)은 god 모드와 함께 쓰면 오프닝/중간보스 구간을 실시간 대기 없이 통과시키고, 정지된 위치에서 장시간(수십~백여 초) 관측이 필요할 때도 반복 사용해 관측 시간을 크게 늘릴 수 있다. 부작용 없음을 확인했다(콘솔 에러 0, 화면 정지/깨짐 없음). 오버레이 상단의 "PAUSE" 박스는 실제 일시정지가 아니라 터치 모드 상시 버튼이니 혼동하지 말 것.
