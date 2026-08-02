# build31 시각 검증 — 전함 이중 요새 제거 + 코어 무적 테두리 (커밋 1c0b9e4)

- 담당: CLAUDE (RENDERER), 2026-08-03
- 대상: `Builds/Web` (2026-08-02 23:34 빌드, 커밋 1c0b9e4 포함)
- 방법: headless Chrome(puppeteer-core, swiftshader) + `python -m http.server 8099`.
  공유 PC 제약으로 **화면에 창을 띄우지 않는 경로만** 사용했다 — 스크린샷은 파일로만.
- 스크립트: 세션 스크래치패드 `b31_boss4.js` (F11 스킵 + 중간보스 퀵픽 카드 자동 탭 +
  타이틀 시드 입력. 키 연타는 프레임당 1회만 인식되므로 80ms 간격 필수 — 다음 테스터 참고).

## 결과: 두 수정 모두 PASS (렌더러 스모크 기준)

| # | 항목 | 결과 | 근거 |
|---|---|---|---|
| 1 | 전함 "요새 두 개" 해소 | **PASS** | St3 fortress `sect Boss/live`(시드 4177372, 44스킵)에서 함체 실루엣 + 하드포인트(포탑 4문·함미·코어)만 보임. 이전에 함체 중앙에 겹쳐 서던 풀사이즈 `boss_fortress` 본체 렌더러 없음. `warship gate 1/3 turret 4/4` 오버레이 정상, 관찰 45초간 보스 HP바 연속 감소(함미 데미지 경로 산 채로 유지). 증거: `docs/screenshots/b31_warship_single_hull.png` |
| 2 | 코어 무적 청록 네모 → 1px 테두리 | **PASS** | St2 hive `sect Boss/live`(시드 2 정타 입력 확인)에서 코어 무적 표시가 **테두리만** 그려지고 내부로 보스 아트가 그대로 보임. 채워진 네모 재현 없음. t0~t42s 관찰 내내 동일. 증거: `docs/screenshots/b31_hive_core_border.png` |
| 3 | 콘솔 에러 | **0건** | 세 런(St3 nebula 오발 포함) 전부 `[error]`/`[pageerror]` 없음 |

## 추가 검증 (같은 날 2차 런): 전함 완주 — STAGE CLEAR 도달

build25~30 내내 미도달이던 전함 완주를 처음 끝까지 갔다. 시드 2 정타 입력(St3
fortress), god + F9/F10 파워업(OPTION LV4·SHOT LV2·MISSILE LV1·DOUBLE MK1),
보스전은 F11 가속 + 기체 상하 스윕(y ≈ ±1~2u). 결과:

- tick ≈ 11.7k 보스룸 진입 → tick ≈ 63.6k **STAGE CLEAR - CHOOSE REWARD** 정상 표시.
  증거: `docs/screenshots/b31_warship_stage_clear.png`
- 전투 내내 단일 함체 렌더링 유지, 보스 HP바 단조 감소, 격파 후 전함 뷰 잔상 없이
  정리(리워드 화면에 하드포인트·실루엣 안 남음). 콘솔 에러 0건.
- **포탑은 4/4인 채 함미 경로만으로 격파됐다** — 스윕 폭이 포탑 밴드에 충분히 못
  닿은 탓도 있지만, 애초에 Core 규칙이 아직 2막(REQ-112: 포탑 분기 미배선)이라
  포탑 파괴가 격파 조건에 관여하지 않는다. REQ-112가 구현되면 이 런은 재검증 대상이다.
- `MidBossDefeated` 1회 발화 여부는 스크린샷으로 관측 불가 — REQ-112의 결정론
  테스트 제안에 이미 포함돼 있다.

## 한계 (다음 검증자 참고)
- URL `?seed=N`은 무시된다 — `DevArgs.OverrideSeed`는 커맨드라인 인자만 읽는다.
  WebGL에서 시드를 고정하려면 타이틀에서 Backspace(간격 두고)로 지우고 숫자를 쳐라.
- 무적 테두리의 1px 유지(대형 코어에서 늘어나지 않는지)는 hive 코어(7×5u)에서 확인됐고,
  다른 보스 코어는 미확인이나 같은 9-슬라이스 경로라 구조상 동일하다.

## 관련

- REQ-122 (보스전 콤보 배율 붕괴, CODEX): `requests.md`에 발행돼 있음. 이번 검증 중에도
  St3 보스전 배율이 ×1로 주저앉는 것이 오버레이에서 재관측됐다 — Core 수정 대기.
