# REQ-084 (CODEX): 옵션 최대 6기 지원 (사람 지정, 2026-07-31)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main`
(main에는 네 REQ-082와 GROK REQ-083이 병합돼 있다).

사람 지정: **"옵션도 6개까지 늘리자"** — Option 게이지 maxLevel 4 → 6.

## 범위

1. Core의 옵션 편성(formation) 오프셋/추종 이력이 4기까지만 정의돼 있다면 **6기까지 확장**하라.
   - trail(딜레이 추종): `N * OptionFollowDelayTicks` 이력 링버퍼 용량이 6기 기준으로 충분한지 확인.
   - 다른 편성(무기 계열별 오프셋 등)이 있으면 5·6기 오프셋을 결정론 규칙대로 정의.
2. 옵션 수 상한을 가정한 검증/상수(`maxLevel 4` 하드코드, 테스트 픽스처)를 6 호환으로.
3. 미러 발사 볼리 용량: 옵션 6기 + 주무기 볼리가 MaxBullets 예산에서 결정론적으로
   잘리는 기존 규칙이 6기에서도 성립하는지 테스트로 확인.
4. 데이터(`weapons.json` option maxLevel, 게이지 슬롯 maxLevel)는 GROK 몫 — 너는 Core가
   6을 받아들이게만 하라. 단 Core에 기본값/상한 검증이 있으면 6으로.

## 검증

- dotnet test 전부 통과, DeterminismAudit AUDIT PASS, 같은 시드 2회 해시 일치
- 보고서 `Reviews/from-codex/req084-report.md`. 커밋은 오케스트레이터가 대신한다.
