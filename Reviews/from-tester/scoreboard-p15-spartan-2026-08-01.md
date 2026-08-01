# build17/18 SPARTAN 계약 카드 + 스코어보드 상세 통계 실플레이 검증

- 담당: PLAYTESTER (Claude Sonnet 5)
- 대상: `http://localhost:8099/index.html` (검증 도중 build17 → build18로 갱신됨. build18은 `?dev=1&stage=N&god=1` 스테이지 직행+무적 인자 추가)
- API: `https://rss-scoreboard.coreboard.workers.dev`
- 도구: puppeteer-core (headless Chrome, `C:/Program Files/Google/Chrome/Application/chrome.exe`), viewport 1300x760
- 스크린샷/스크립트 저장 위치: `C:\Users\pavy2\AppData\Local\Temp\claude\D--Unity-Work-Roguelike-Scrolling-Shooter\8daea698-fdad-44ba-b245-873604c89633\scratchpad\p15\`

## 결과 요약

| # | 항목 | 결과 |
|---|---|---|
| 1 | 회귀 (타이틀→LAUNCH→전투→게임오버) | PASS |
| 2 | SPARTAN 계약 카드 실물 확인 | SKIP (미발견 — 근거는 §2 참고) |
| 3 | CONTRACT LOCK 피드백 | SKIP (2번 미발견에 종속) |
| 4 | 점수 제출 상세 통계 (st/rm/op/lv/bb/gz/mx) | PASS |
| 5 | 랭킹 모달 문맥 줄 (기체약칭·도달·NB) | PASS |
| 6 | 게임오버 요약 BOMBS 줄 | PASS |

---

## 1. 회귀 — PASS

`?v=submit1` (dev 치트 없는 클린 URL)로 타이틀 로드 → LAUNCH(중앙 클릭) → 전투 진입.

- "TOUCH AND DRAG - YOUR SHIP FOLLOWS YOUR FINGER" / "AUTO FIRE IS ON" 안내 정상 노출 (드래그 조작 + 오토파이어 확인).
- 하단 HUD 6게이지(SPEED/SHOT/MISSILE/DOUBLE SHOT/OPTION/SHIELD) 정상 렌더링.
- 실시간(치트 없이) 플레이 진행 후 GAME OVER 패널까지 정상 도달, SCORE/KILLS/ACC/BEST COMBO/GRAZE/BOMBS 요약 정상 표시.

스크린샷: `p15/submit/00_combat.png`, `p15/submit/01_gameover.png`

## 2. SPARTAN 계약 카드 — SKIP (미발견)

### 절차
자동 플레이봇(puppeteer)으로 `?dev=1&seed=N`을 시드별로 순회하며 실시간(치트로 SHIELD/SHOT 등을 강화하되 F11 스킵은 배제 — 이유는 "발견된 문제" 참고) 전투를 진행, 게임오버 시 REDEPLOY로 재시도(시드당 최대 6~8회 재시도)하면서 "MID-BOSS DOWN - QUICK PICK"(2카드) / "STAGE CLEAR - CHOOSE REWARD"(3카드+리롤) 보상 화면이 뜰 때마다 스크린샷 후 카드를 선택해 진행. 카드 선택 직후 다른 카드 화면이 바로 이어지는 체이닝(예: 보상 픽 → 계약 픽)이 있는지도 별도로 확인(`chaintest` 런).

- **시드 12개 시도**: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14
- **카드 화면 캡처 총 62장** (2슬롯/3슬롯 레이아웃 혼합, `p15/huntA`, `p15/huntB`, `p15/huntC`, `p15/chaintest` 하위 `seed*/card_*.png`)
- 전부 목검토(스크린샷 직접 확인) 완료. 등장한 카드 종류: Speed+1/+2, Shield+1(2종: 스택 복구/용량 증가), Capsule x5, Bomb+1, Missile+1, Missile Swap, Homing Missile, Formation Swap, Ricochet, Pierce Shot(대가: BOMB CAP -1), Kill Explosion, Shot+1, Option+1 등.
- **SPARTAN PROTOCOL / NO OPTION RUN / BARE HULL 중 어느 것도 발견하지 못함.**
- 보상 픽 화면 직후 다른 카드 화면이 체이닝되는 사례도 없었음 (계약 픽이 별도 화면으로 끼어드는 패턴은 관찰 안 됨).
- build18의 `?dev=1&stage=2&god=1` 스테이지 직행 치트로 stage 2 시작 지점을 직접 확인했으나(무적 상태) 곧바로 "STAGE 2/5 ADVANCE > mid-boss" 전투로 진입 — 별도의 계약 선택 화면 없이 바로 전투가 시작됨 (스테이지 직행 경로에서는 계약 카드가 노출되지 않거나, 기본값이 자동 적용되는 것으로 보임).

과제 안내상 "1슬롯 3.2%/시리즈 16.1%"로 등장률이 낮다고 했고, 12개 시드 × 카드 화면 62회 노출(슬롯 기준으로는 더 많음)에도 못 만난 것은 확률적으로 있을 수 있는 일 — **버그로 단정하기 어려워 SKIP 처리**. (기대값 계산상 세션당 16.1% 확률로 62번의 "시리즈" 표본이 아니라 정확히는 12번의 "시드=시리즈" 표본에 해당하므로, 12회 시행에서 미발견 확률은 약 13% 정도로 못 만날 수도 있는 범위.)

### 발견된 문제 (테스트 자동화 측, 회귀와 무관)
- 이 빌드는 **마우스를 누른 채 드래그**해야 기체가 움직임 — `mouse.move`만으로는 기체가 그 자리에 고정된 채 죽는다. 초반 봇이 이걸 놓쳐서 스폰 직후 반복 즉사했음.
- **F11(+10초 스킵)을 실전투 중 사용하면 그 사이 경과가 순간 처리되며 사실상 무조건 즉사**한다(회피할 시간이 없는 채로 데미지만 누적되는 것으로 추정). 회귀에 영향 없는 자동화 이슈이나, 향후 유사 자동화 스크립트를 짤 때 F11은 전투 중이 아니라 안전 구간에서만 써야 함을 기록해 둔다.
- 계약/보상 카드 화면 중 일부("MID-BOSS DOWN - QUICK PICK")는 하단 장비 HUD 패널이 그대로 보이는 상태라 "HUD 사라짐"만으로는 카드 화면을 못 잡는다 — 프레임 체크섬이 거의 안 변하는(일시정지) 상태를 추가 신호로 써서 해결.

## 3. CONTRACT LOCK 피드백 — SKIP

2번에서 SPARTAN류 계약 카드를 실제로 수락하지 못했으므로 검증 불가. 지침대로 SKIP 처리.

## 4. 점수 제출 상세 통계 — PASS

치트 없는 클린런 2회 진행 (일반 런 + DAILY CHALLENGE 런), 사망 후 SUBMIT SCORE 클릭 → `window.prompt` 이름 입력 다이얼로그를 `page.on('dialog')`로 자동 수락("QA-BOT", "QA-DAILY") → 버튼이 "RANK #n"으로 전환되는 것 확인.

curl로 서버 조회 결과, 두 항목 모두 `st`(stage), `rm`(reach), `op`, `lv`, `bb`, `gz`, `mx` 필드가 전부 실려 있음:

```json
{"n":"QA-BOT","s":490,"sd":"1051022028","sh":"starter","d":"NORMAL","g":"KIA","h":"","t":1785569422063,"tk":"100d23d0","st":1,"rm":2,"op":0,"lv":0,"bb":0,"gz":1,"mx":2}
```

```json
{"n":"QA-DAILY","s":2230,"sd":"3401227383","sh":"starter","d":"NORMAL","g":"KIA","h":"","t":1785569794238,"tk":"47fb538e","st":1,"rm":2,"op":0,"lv":0,"bb":0,"gz":4,"mx":2}
```
(daily board: `daily:20260801`에 반영됨)

스크린샷: `p15/submit/01_gameover.png`, `p15/submit/02_after_submit.png`(RANK #1 전환), `p15/ranking/13_gameover.png`, `p15/ranking/14_after_submit.png`

## 5. 랭킹 모달 문맥 줄 — PASS

타이틀 → RANKING 클릭 → DAILY RANKING 모달에서 항목 렌더링 확인.

- **신규(문맥 있는) 항목**: `2  QA-DAILY  2,230  ST 1-2  NB` — 기체 약칭(`ST` = starter), 도달(`1-2`, st/rm 필드 기반), 앰버 `NB`(New Best로 추정) 배지 모두 정상 노출.
- **구 항목(문맥 없는) 항목**: `1  PAVY  123,450` — 컨텍스트 필드가 없는 레거시 데이터인데도 깨지지 않고 이름+점수만 정상 렌더링. 하위 호환 확인됨.

스크린샷: `p15/ranking/16_ranking_with_daily_entry.png`

## 6. 게임오버 요약 BOMBS 줄 — PASS

모든 게임오버 스크린샷에서 `BEST COMBO xN  GRAZE N  BOMBS N` 형식의 줄이 항상 표시됨. 예: `BEST COMBO x2  GRAZE 1  BOMBS 0` (`p15/submit/01_gameover.png`), `BEST COMBO x2  GRAZE 4  BOMBS 0` (`p15/quicktest/seed1/c02_OVER.png` 계열 다수).

---

## 종합

- 배포에 영향을 주는 **결함(FAIL)은 발견되지 않음.**
- 항목 2/3(SPARTAN 계약 카드)은 낮은 등장률로 인해 지침에 따라 12개 시드 시도 후 SKIP 처리. 버그 의심 신호는 없었으나, 등장 자체를 실물로 확인하지 못했으므로 카드 텍스트/색상/CONTRACT LOCK 피드백의 실제 렌더링은 **미검증 상태로 남음** — 필요하면 결정론적 시드 탐색(같은 시드에서 계약이 뜨는 정확한 룸을 알아내는 방식)이나 디버그 강제 노출 스위치가 있으면 재검증이 훨씬 빨라질 것으로 보임.
