# AGENTS.md — Roguelike_Scrolling_Shooter 에이전트 작업 계약서

이 문서는 이 저장소에서 작업하는 모든 AI 에이전트(CLAUDE, CODEX, GROK)와 사람이 지키는 규칙이다.
자신의 소유 영역 밖의 파일은 **수정하지 않는다**. 필요하면 `Reviews/` 폴더로 요청한다.

## 1. 게임 개요

- 가칭 **Roguelike_Scrolling_Shooter** — 2D 횡스크롤 슈팅 (그라디우스 계열)
- **Steam 판매 품질이 목표** (2026-07-28 확정) — 마일스톤은 ROADMAP.md, 아트 규격은 ART-DIRECTION.md
- hi-bit HD 도트, 정수배 업스케일 (Pixel Perfect Camera, Reference Resolution **640×360**, PPU 16 — 2026-07-28 384×224에서 상향)
- 파워업 게이지: 캡슐 수집 → 커서 순환 → 원하는 슬롯에서 활성화 (기본탄 / 미사일 / 옵션 / 실드)
- 로그라이크: 스테이지는 시드 기반 랜덤 생성, 사망 시 처음부터. 단 파워업 레벨은 승계 (§7 참고)
- 아키텍처: **Simulation(순수 C#) / Presentation(Unity) / Content(JSON)** 3분리

## 2. 소유 영역

| 에이전트 | 역할 | 소유 | 검증 방법 |
|---|---|---|---|
| **CLAUDE** | RENDERER + ART/AUDIO 파이프라인 | `Assets/` 전체 중 `Assets/Scripts/Core/` 제외 — 씬, 프리팹, 카메라, 풀링, 입력, 오디오, `Assets/Scripts/Presentation/`, `ProjectSettings/` + `Tools/ArtGen/` (AI 아트·SFX 생성→후처리→임포트 스크립트) | Unity Test Runner + Unity CLI `capture_game_view` 캡처 |
| **CODEX** | SIMULATION | `Assets/Scripts/Core/` (Shmup.Core) + `Assets/Tests/EditMode/` + `Tools/CoreStandalone/` | `dotnet test` (Unity 안 열음) |
| **GROK** | CONTENT | `GameData/` (JSON 데이터, 밸런스 수치) + 밸런스 시뮬 스크립트 | JSON 스키마 검증 + 헤드리스 시뮬 실행 |
| **GEMINI** | QA / VERIFIER | `QA/` (테스트 플랜, 캡처, 리포트) — **코드·에셋·데이터 소유 없음** | 리포트 자체가 산출물. 빌드 실행·관찰 + Unity CLI로 시각 QA(`capture_game_view` 스크린샷 비교, `get_performance_stats` 성능 추적) |

공유 파일(`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.gitignore`)은 사람만 수정한다.

**아트/사운드 생성물 규칙** (2026-07-28): AI 생성 에셋은 후보 다량 생산 → **사람(아트 디렉터) 큐레이션 합격작만** `Assets/Art/`·`Assets/Audio/`에 확정한다. 생성 프롬프트·파라미터는 산출물과 함께 커밋해 재현 가능하게 유지한다. 시뮬 상태 변화를 애니메이션·사운드로 표현해야 하면 Core의 시뮬 이벤트(REQ-005)를 구독한다 — Presentation에서 게임플레이 판정을 내리지 않는 원칙은 그대로다.

## 3. 브랜치 / worktree

- `main` — 통합 브랜치. CLAUDE가 `D:\Unity_Work\Roguelike_Scrolling_Shooter\main`에서 작업
- `sim` — CODEX 브랜치. worktree `..\wt-sim`
- `content` — GROK 브랜치. worktree `..\wt-content`
- `qa` — GEMINI 브랜치. worktree `..\wt-qa` (QA/와 Reviews/from-gemini/만 커밋)
- 병합은 사람이 수행한다. 에이전트는 자기 브랜치에만 커밋한다.

## 4. 결정론 규칙 (타협 불가)

로그라이크의 시드 재현성은 이 게임의 핵심 요구사항이다. `Shmup.Core`(및 그 테스트)에서:

1. **`UnityEngine` 참조 금지.** asmdef가 `noEngineReferences: true`로 강제한다. 이 설정을 바꾸지 마라.
2. **`System.Random`, `UnityEngine.Random`, `Guid.NewGuid()` 금지.** 모든 난수는 주입받은 `Shmup.Core.Rng`에서만 뽑는다.
3. **`DateTime.Now`, `Environment.TickCount` 등 벽시계 금지.** 시뮬레이션 시간은 정수 틱으로만 흐른다.
4. **스트림 분기.** 용도가 다른 난수는 `Rng.Fork(streamId)`로 분리한다 (예: 스테이지 생성 = 0, 드롭 판정 = 1). 한쪽 로직 수정이 다른 쪽 결과를 흔들면 안 된다.
5. **게임플레이 수치 계산은 정수 우선.** 부동소수점 누적으로 플랫폼 간 결과가 갈리는 코드를 만들지 마라.
6. **`Dictionary`/`HashSet` 순회 순서에 의존하는 로직 금지.** 순서가 필요하면 명시적으로 정렬한다.
7. 같은 입력 → 같은 출력. 절차생성 등 모든 공개 API는 순수 함수여야 하고, 그 사실을 테스트로 증명한다.

## 5. 게임 데이터 규칙

- 게임 데이터(적, 무기, 웨이브, 밸런스)는 **`GameData/*.json`** 이 유일한 원본이다.
- **ScriptableObject를 게임 데이터 저장소로 쓰지 않는다.** (.asset은 GUID 박힌 YAML이라 병렬 작업 시 머지 지옥이고, Core가 Unity 없이 읽을 수 없다.)
- JSON은 UTF-8 (BOM 없음), 들여쓰기 2칸. 키는 camelCase.
- CODEX는 JSON을 읽는 순수 파서/모델을 Core에 두고, CLAUDE는 그 모델을 소비만 한다.

## 6. 테스트 규칙

- `Assets/Tests/EditMode/`의 테스트는 **NUnit 3 API만** 사용한다 — 같은 파일이 Unity Test Runner와 `Tools/CoreStandalone`의 `dotnet test` 양쪽에서 그대로 컴파일된다.
- Core 공개 API를 추가/변경하면 반드시 테스트를 함께 추가/갱신한다.
- 커밋 전 체크: `cd Tools\CoreStandalone && dotnet test` 통과가 최소 조건.

## 7. 사람이 결정할 사항 — 에이전트가 임의로 정하지 말 것

다음 값은 게임의 손맛을 결정하는 밸런스 사안이다. 코드는 조절 가능하게 만들되, **기본값 변경은 사람의 명시적 지시가 있을 때만** 한다.

- `MetaProgression.CarryFraction` — 사망 시 파워업 레벨 승계 비율. 기획 원문은 1.0(전부 승계)이지만, 난이도 붕괴 우려가 있어 GROK의 밸런스 시뮬 결과를 보고 사람이 확정한다.
- `PowerUpGauge` 슬롯별 최대 레벨 (현재 플레이스홀더: 5/3/4/3)
- 적 HP, 데미지 곡선 계수, 웨이브 밀도 등 `GameData/` 수치 전반의 최종 확정

제안은 자유다: 시뮬 결과와 함께 `Reviews/from-<agent>/`에 남겨라.

## 8. Reviews 폴더 프로토콜

- 다른 에이전트 소유 영역에 변경이 필요하면 `Reviews/from-<자기이름>/requests.md`에 요청을 적는다 (무엇이, 왜, 제안 시그니처).
- 요청을 받은 에이전트는 구현 후 같은 파일에 응답을 덧붙이고, 완료 항목은 체크한다.
- 코드 리뷰 코멘트도 같은 폴더 구조를 쓴다.
