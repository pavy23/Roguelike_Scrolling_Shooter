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

## 한계 (다음 검증자 참고)

- 렌더러 관점 스모크다. 전함 **완주**(포탑 분기→함체→코어→MidBossDefeated 1회)는
  build30 테스터 보고서의 남은 항목 그대로다.
- URL `?seed=N`은 무시된다 — `DevArgs.OverrideSeed`는 커맨드라인 인자만 읽는다.
  WebGL에서 시드를 고정하려면 타이틀에서 Backspace(간격 두고)로 지우고 숫자를 쳐라.
- 무적 테두리의 1px 유지(대형 코어에서 늘어나지 않는지)는 hive 코어(7×5u)에서 확인됐고,
  다른 보스 코어는 미확인이나 같은 9-슬라이스 경로라 구조상 동일하다.

## 관련

- REQ-122 (보스전 콤보 배율 붕괴, CODEX): `requests.md`에 발행돼 있음. 이번 검증 중에도
  St3 보스전 배율이 ×1로 주저앉는 것이 오버레이에서 재관측됐다 — Core 수정 대기.
