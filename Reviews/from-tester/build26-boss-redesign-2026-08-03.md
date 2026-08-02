# build26 보스 리디자인(REQ-115/116) + 전함 씬 배선 재검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (build26, `Builds/Web`)
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760, 키보드는 `page.keyboard.down/up`(홀드) + `press`(탭). HP바 실측은 스크린샷 육안이 아니라 **pngjs로 픽셀을 직접 카운트**해 정량화했다(build25 대비 신규 방법론).
- 스크립트/스크린샷: `...\scratchpad\b26_*.js` 일체, 스크린샷은 `b26smoke\`, `b26hive\`, `b26hivebos\`, `b26warshipA\`, `b26storm\`, `b26core\`, `b26regress\`. 픽셀 측정 스크립트는 `measure_hpbar.js`.
- 시간 예산: 약 2시간, 항목별 20~30분 상한을 지켰다.

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 전함 3막 (fortress 보스룸) | **PARTIAL/FAIL** — dev 오버레이 `warship stern 1/3 turret 4/4`가 이번엔 실제로 뜬다(build25 대비 개선 확정). 그러나 **함미(stern) HP가 173초간 정확히 0 변화**(픽셀 단위로 확인)이고, 50초 이후로는 보스·플레이어 스프라이트 자체가 화면에서 사라진다. 포탑/코어 way 비교는 함미를 못 뚫어 시도조차 못함 |
| 2 | hive 아트 실측 | **PASS** — `theme hive` 오버레이로 확정, 초록 팔레트 + 촉수 실루엣(fg) + 알주머니 랜드마크가 nebula(청보라 구름)와 뚜렷이 구분됨. build25의 "hive=nebula" 오진은 신규 `theme` 태그로 해소됨 |
| 3 | 히든 보스 3막 (leviathan/broodmother) | **PASS (코드)** / SKIP(실측) — waves.json 파츠 HP 합·페이즈 게이트·레일건·흡입 좌표 전부 GROK 스펙과 일치 확인. CODEX 자동화 테스트(545/545) + DeterminismAudit seed-7-hidden PerfectClear(bossHp 0/62000)로 시뮬레이션 완주 교차검증. 실측 진입(엘리트3/무피해2/희귀1 중 2)은 dev cheat 부재로 시간 예산 내 시도 안 함 |
| 4 | St2 촉수/St4 번개룡/St5 2형태 | **PARTIAL 3건** — St2는 보스룸 진입 직후 시간 종료(촉수 파츠 미확정), St4는 낙뢰빔(bossLaser) 연출은 확인했으나 체인 미니언 자체는 스샷에서 특정 못함, St5는 form1 전투는 정상(HP 25~30%까지 실제로 깎임)이나 42,000 총 HP를 시간 내 다 못 깎아 form2 전환 미관측 |
| 5 | 회귀 | **PASS** — 일반 런 자연사→GAME OVER 정상, 콘솔 에러 6개 세션 합계 0건 |

**최우선 발견**: 전함 함미(stern) 피격이 **REQ-113/117 통합 이후에도 라이브에서는 여전히 0 데미지**다. 이번엔 오버레이가 정상 표시되므로 "테스트 방법론 한계"로 치부하기 어렵고, 보스·플레이어 스프라이트가 교전 50초 후 화면에서 완전히 사라지는 현상까지 겹쳐 **포지션/스크롤 동기화 버그**를 강하게 시사한다. 아래 §1에 근거를 픽셀 단위로 첨부한다.

---

## 1. 전함 3막 — PARTIAL/FAIL (최우선)

### 1-A. 개선 확인: dev 오버레이가 이번엔 뜬다

`?dev=1&stage=3&god=1` + 타이틀 시드 `2` 입력 → 실전투(F11 미사용, 상하 스윕)로 Boss 섹션 도달 후,
`b26warshipA/010_stern_t2.png`에서 오버레이 3번째 줄에 다음이 정상 출력됨을 확인:

```
warship stern 1/3   turret 4/4
```

이는 build25에서 "다수의 독립 세션·수십 분에 걸쳐 한 번도 관측되지 않음"이라 보고했던 바로 그 세그먼트다. `DevCheats.cs:129` `warshipOn = _warship != null && _warship.Active`가 이번엔 참으로 확인된다 — REQ-113(BattleSim 본배선)·REQ-117(실데이터 E2E 테스트)이 실제로 씬에 반영됐다는 첫 실측 증거다.

### 1-B. 회귀 발견: 함미 HP가 173초간 정확히 0 변화

`measure_hpbar.js`로 HP바 영역(y=66~68, x=390~930)의 채워진 픽셀 수를 스크린샷마다 직접 카운트했다(육안 판독이 아니라 자동 픽셀 비교):

| 스크린샷 | 인카운터 tick | 채움 픽셀 |
|---|---:|---:|
| `010_stern_t2.png` | 540 | **362/540** |
| `012_stern_t4.png` | ~1450 | 362/540 |
| `014_stern_t6.png` | ~2820 | 362/540 |
| `016_stern_t8.png` | 4110 | 362/540 |
| `017_stern_result.png` | 4110 | 362/540 |
| `018_hullA_wait_t3.png` | 4350 | 362/540 |
| `021_hullA_gate_end.png` | 4920 | 362/540 |
| `037_core_still_t8.png` (최종) | **10920** | **362/540** |

tick 540→10920(173초, God모드 + SPEED/SHOT/MISSILE/OPTION/SHIELD/CROSS FIRE 전부 MAX 상태로 정중앙 정지 사격) 동안 **단 1픽셀도 변하지 않았다.** 오버레이도 시종일관 `warship stern 1/3 turret 4/4`로 그룹 전환이 전혀 없었다.

### 1-C. 부가 발견: 보스·플레이어 스프라이트가 화면에서 사라짐

`018_hullA_wait_t3.png`(tick4350)부터 `037_core_still_t8.png`(tick10920)까지, 화면은 붉은 틴트 배경(성벽 실루엣)만 남고 **전함 스프라이트도 플레이어 함선 스프라이트도 전혀 보이지 않는다** — 적 탄 궤적(가는 대각선)만 간헐적으로 보임. 반면 초기 `010_stern_t2.png`에서는 전함 본체(적안 회전축 + 장갑 블록 메카 디자인)가 화면 중앙에 뚜렷이 렌더링돼 있었다. 즉 교전 시작 후 약 50초 시점부터 두 개체 모두 카메라 가시 영역을 벗어난 것으로 보인다.

콘솔 로그(`console_A.log`)에는 `[error]`/`[pageerror]` **0건** — 크래시가 아니라 조용한 로직/포지셔닝 이상이다.

### 1-D. 해석과 후속 제안

REQ-113 보고서는 "함수(bow/core) 그룹 전환 시에만" 전함을 `BossHoldX`에 명시적으로 고정한다고 밝혔다 — 함미(stern) 구간 자체의 포지션 고정 로직은 별도로 언급되지 않는다. `boss_fortress`는 최상위 `holdX: 12.0`을 갖고 있어 일반 보스 메커니즘으로 고정될 것으로 기대되지만, 실측 결과(스프라이트 실종 + 완전 무변화)는 그 가정과 어긋난다.

두 가설이 남는다:
- (a) 함미 구간에서 전함이 표준 holdX 고정을 따르지 않고 스테이지 스크롤을 따라 이동해, 실전투 시작 시점 이후 사거리 밖으로 벗어난다.
- (b) `WarshipDamageCommand` 경로가 여전히 어떤 조건에서 데미지를 흡수하지 못한다(단, REQ-117의 실데이터 E2E 테스트 `RepositoryGameDataRunManagerFortressBossActivatesDamageableStern`는 CODEX 환경에서 통과했다고 보고됨 — 즉 순수 시뮬레이션 레벨에서는 데미지가 들어간다. 실제 빌드와의 괴리 가능성).

**포탑 파괴 수 → 코어 개막 way 비교(요청 c)는 함미 게이트를 못 뚫어 시도하지 못했다.** 다음 빌드 테스터는 (1) 오버레이 `warship` 세그먼트가 뜨는 즉시 스프라이트 위치를 추적 스크린샷으로 남기고, (2) 스프라이트가 사라지는 시점 전후로 HP 픽셀이 변하는지를 `measure_hpbar.js`류 도구로 재확인할 것을 권한다.

**스샷**: `b26warshipA/010_stern_t2.png`(오버레이 정상 확인, 전함 스프라이트 온전), `b26warshipA/018_hullA_wait_t3.png`·`037_core_still_t8.png`(스프라이트 실종 + HP 무변화), `b26warshipA/console_A.log`(에러 0건).

---

## 2. hive 아트 — PASS

`?dev=1&stage=2&god=1` + 시드 2 → `theme hive` 오버레이 확인(`b26smoke/03_ingame.png`, `b26hive/hive_3_Late.png`).

- **배경**: 짙은 초록 팔레트, 중앙에 거대한 반투명 초록 원형 랜드마크(알/코어 형상) — nebula의 청보라 성운 구름과 확연히 다름.
- **전경 실루엣**: 화면 하단 전역에 걸쳐 검은 촉수(나선형) 실루엣이 촘촘히 배치 — `hive_fg.png` 스펙과 일치.
- **St4(nebula) 대조**: 같은 세션에서 St4를 별도로 확인(`b26storm/016_fight_t26.png` 등)한 결과 짙은 남색/보라 성운+별빛 팔레트로 hive와 명확히 구분됨 — build25가 "hive에 nebula가 뜬다"고 오진했던 문제가 신규 `theme {id}` 오버레이 덕에 이번엔 애초에 혼동할 여지가 없었다.

**스샷**: `b26smoke/03_ingame.png`, `b26hive/hive_3_Late.png`, `b26hive/hive_4_Boss.png`.

---

## 3. 히든 보스 3막 (leviathan/broodmother) — PASS(코드) / SKIP(실측)

### 코드 확인 (전량 확정)

`GameData/waves.json:9225`(`boss_leviathan`), `:9579`(`boss_broodmother`)를 직접 읽어 GROK REQ-116 보고서와 대조:

- **leviathan**: 파츠 10개(turret_spine 5000 / head_cowl 4500 / blade_limb_upper·lower 4000×2 / rear_engine 3500 / lower_launcher 5000 / shield_emitter 5000 / railgun 9000 / rib_gate 9600 / core 12400) 합 = **62,000** = `hp` 필드와 정확히 일치.
  - phase0(외갑): railgun/rib_gate/core 모두 `active:false, invulnerable:true`.
  - phase1(`hpThreshold:0.5`, 참수빔): railgun `active:true`에 `attack.type:"laser"`, `endOffsetX:-27.3984375`(화면 관통), `fullHalfWidth:1.3984375` — GROK 보고서의 "≈1.4" 수치와 일치. rib_gate도 함께 개방.
  - phase2(`hpThreshold:0.2`, 코어 폭주): core만 `active:true, invulnerable:false`, radial 12-way.
- **broodmother**: 파츠 7개(tentacle_left/right 1500×2 / sac_left/right 10000×2 / sac_lower 11000 / maw 15600 / heart_core 12400) 합 = **62,000** 일치.
  - phase1(산란): 촉수+낭 활성, maw/heart_core 무적.
  - phase2(`hpThreshold:0.5`, 흡입): maw `attack.type:"suction"`, `effectSpeed:3, effectMaxSpeed:5, effectOffsetX:-3.296875, effectOffsetY:-0.3984375` — REQ-115b 보고서의 확정 앵커와 정확히 일치.
  - phase3(`hpThreshold:0.2`, 심장): heart_core만 개방, radial 12-way.

### 자동화 테스트 교차검증

- CODEX `dotnet test`: **545/545 PASS** (req115a/115b/116 각 보고서 공통 인용).
- `DeterminismAudit --suite`: seed-7-hidden 시나리오가 **PerfectClear, bossHp=0/62000**로 완주 — 즉 히든 보스 3막 전체가 시뮬레이션 레벨에서는 끝까지 격파 가능함을 자동화 테스트가 실제로 증명했다.

### 실측 진입 — SKIP

`RunManager.cs:2961` `TryBeginHiddenBiome`은 `CountHiddenBiomeConditions(eliteRoomsCleared≥3, noHitBiomesCleared≥2, rareEncountersCleared≥1)` 중 **2개 이상 충족**을 요구한다. `BattleDirector.cs:742`의 `DevArgs.OverrideStartStage`는 `Clamp(value, 1, lastStage)`로 막혀 있어 **dev 치트로 히든 바이옴에 직접 진입할 방법이 없다** — 정상 진행으로 스테이지 1부터 조건을 채워야 한다. 스크립트화된 정밀 회피/루트 선택이 필요해 시간 예산(항목당 20~30분) 내로는 무리라 판단, 코드+자동화 테스트 근거로 SKIP 처리했다(지시문이 명시적으로 허용한 처리).

---

## 4. St2 촉수 / St4 번개룡 / St5 2형태 — PARTIAL 3건

### St2 boss_hive 촉수 — PARTIAL

`?dev=1&stage=2&god=1` 시드 2로 30사이클 실전투 진행 후 보스룸(`sect Boss/live`) 진입까지는 도달했으나, **HP바가 나타난 시점이 스크립트 종료 직전(~6초)**이라 촉수 파츠(tentacle_left/right, 각 2000HP)의 파괴 여부를 확정하지 못했다. 화면 우측 상단에 마젠타색 촉수형 부속을 가진 보스 실루엣은 포착함(`b26hivebos/017_fight_final.png`)이나 결정적이지 않다.

### St4 boss_storm 번개룡 — PARTIAL

`waves.json:8971` 확인 결과 phase1(`hpThreshold:0.667`)에 `segmentChain{segmentCount:7, summonCount:1}` + `bossLaser`(낙뢰), phase2(`hpThreshold:0.333`)에 `segmentChain{segmentCount:8, summonCount:2}` + 낙뢰 고밀도가 정의돼 있다. 라이브에서 **거대한 적백 세로줄 레이저 빔**(`b26storm/020_fight_t38.png`, `021_fight_final.png`)이 HP 30~65% 구간에서 실제로 발사되는 것을 확인해 `bossLaser`(낙뢰) 발동 자체는 **PASS**. 그러나 체인 미니언(꼬리를 무는 절 6~8개 오브젝트)은 캡처된 프레임에서 명확히 식별하지 못했다 — 화면 밖이거나 보스 본체에 가려졌을 가능성.

### St5 boss_core 2형태 — PARTIAL

form1(28,000 HP) 교전은 정상 진행돼 HP바가 실제로 25~30%까지 감소했다(`b26core/020_fight_t64.png`, `026_fight_final.png`, tick 3660→5790 구간). 다만 총 42,000 HP(form1+form2)를 시간 예산 내 스크립트 화력으로 다 깎지 못해 **form2(prism) 전환 자체는 라이브로 관측하지 못했다.** 코드 근거: REQ-115a의 `BattleSimTicksFormTransitionScoresEachBodyAndClearsOnlyFinalForm` 테스트가 실제 `BattleSim.Step` 발사체 충돌로 전환을 검증했다고 보고됨(PASS).

---

## 5. 회귀 — PASS

- **일반 런**: dev 플래그 없이(`?v=...`만) 타이틀 → LAUNCH → 자연사 → GAME OVER까지 정상 진행(`b26regress/010_run_final.png`) — `SCORE 00003560 (run 1, stage 1)`, `KILLS 17 CAPSULES 1 ACC 31.4% SHOTS 156`, REDEPLOY/TITLE/SUBMIT SCORE 버튼 정상.
- **콘솔 에러**: 이번 세션 전체(전함 A + storm + core + hiveboss + regress, 총 5개 puppeteer 세션) 콘솔 로그에서 `[error]`/`[pageerror]` **0건**.
- **타이틀 화면**: `DAILY CHALLENGE 08-02 · GLOBAL SEED` 표기 정상, RANKING 버튼 노출 확인(`b26regress/001_title.png`). 모달 자체는 이번 세션에서 클릭 좌표 미스로 열지 못했다(스크립트 한계, 게임 버그 아님).

---

## 참고: 사용한 스크립트/자료

- `b26_smoke.js` — 최초 부팅 + 시드 2 진입 확인, `theme hive` 최초 발견
- `b26_hive.js` — F7 프리뷰로 hive 5구간 테마 순회
- `b26_warship.js A` — 전함 3막 실전투(정중앙 정지 사격, stern 관측 90초+hull 대기+core 관측)
- `measure_hpbar.js` — pngjs 기반 HP바 채움 픽셀 자동 측정(신규, build25 대비 방법론 개선)
- `b26_storm.js` — St4 boss_storm 낙뢰/체인 실전투
- `b26_core.js` — St5 boss_core form1 실전투
- `b26_hiveboss.js` — St2 boss_hive 촉수 실전투
- `b26_regress.js` — 일반 런 + 콘솔 에러 스캔
