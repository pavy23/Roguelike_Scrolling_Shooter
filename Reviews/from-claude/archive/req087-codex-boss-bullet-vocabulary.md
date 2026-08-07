# REQ-087 (CODEX): 보스 탄막 어휘 확장 — 공용 4탄종 + 고유 시그니처 5종 (사람 승인)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, REQ-086 병합 후 진행.

## A. 공용 탄종 4종 (전 보스 페이즈 데이터에서 조합 가능)

| 탄종 | 거동 |
|---|---|
| **heavy** (대구경탄) | 크고(히트박스 2~3배) 느림. 콩알탄 사이의 '벽' |
| **splitter** (파편탄) | 정수 틱 N 비행 후 3갈래로 분열 (분열 각도 LUT) |
| **mine** (기뢰탄) | 도착점에 정지 → 예고 틱 → 플레이어 방향 조준 가속 |
| **boss laser** | 기존 LaserState 재사용, 소스=보스. 예고선 → 빔 |

- `BulletKind` 확장 + 페이즈 발사 데이터에 탄종 선택 축(선택 필드, 기본 기존 콩알탄).
- Presentation 구분용으로 SimEvent 또는 BulletState.Kind로 탄종 관측 가능해야 한다.

## B. 보스별 고유 시그니처 (테마 결합, 사람 승인)

| 보스 | 시그니처 | 구현 힌트 |
|---|---|---|
| boss_stage1 | **고철 투척** — 파괴 가능한 잔해를 포물선 투척 | 기존 breakable obstacle 스폰 재사용, 포물선=정수 중력 가속 |
| boss_hive | **산란** — 약유도 유충탄 + 촉수 소환 | 유도는 homing 미사일 로직 약화판, 촉수=hive_tentacle 스폰 |
| boss_fortress | **레이저 그리드** — 상하 벽 동기 레이저 + heavy 포탄 | 기존 laserEmitter 게이트 로직 재사용 |
| boss_storm | **낙뢰** — 세로 예고선 후 번개 기둥 | 세로 방향 boss laser 변형 |
| boss_core | **회전 프리즘 빔 2기** + 코어 개방 시 전방위 링탄 | 회전 빔=각도 LUT 순환 레이저, 링탄=원형 볼리 |

- 페이즈 데이터에 시그니처 패턴 id 축 추가. 수치(간격·탄속·예고 틱)는 GROK 몫.
- 시그니처는 페이즈 2~3에서 등장하는 걸 기본 문법으로 (페이즈 1은 기존 학습 구간).
- 텔레그래프 이벤트에 탄종/시그니처 구분 인자(Presentation이 색을 나눈다: 앰버=탄막, 적=레이저).

## 검증

- dotnet test 전부(탄종별·시그니처별 결정론 테스트), DeterminismAudit AUDIT PASS, 같은 시드 2회
- 적탄 예산(MaxEnemyBullets)·레이저 상한 초과 경로가 새 탄종에도 조용히 새지 않는지 확인
- 보고서 `Reviews/from-codex/req087-report.md`. 커밋은 오케스트레이터가 대신한다.
