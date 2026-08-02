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
| **GEMINI** | QA / VERIFIER | `QA/` (테스트 플랜, 캡처, 베이스라인, 리포트) — **코드·에셋·데이터 소유 없음** | 리포트 자체가 산출물. 빌드 실행·관찰 + Unity CLI로 시각 QA(`capture_game_view` 스크린샷 비교, `get_performance_stats` 성능 추적) |

공유 파일(`AGENTS.md`, `CLAUDE.md`, `GEMINI.md`, `.gitignore`)은 사람만 수정한다.

**CLAUDE 3분화 개편 (2026-08-01 사람 승인):** 기존 CLAUDE 역할을 모델 3개로 나눈다.

| 담당 | 모델 | 임무 | 비고 |
|---|---|---|---|
| **ORCHESTRATOR** | Claude Fable 5 | 전체 오케스트레이션 — REQ 발행, 디스패치, 병합, 빌드·배포, 최종 검수. 공유 규칙 준수 감시 | 커밋 주체. 코드 구현은 위임이 기본 |
| **RENDERER** | Claude Opus 5 (서브에이전트) | Presentation 구현 — 기존 CLAUDE 소유 영역 그대로 (`Assets/` 중 Core·Tests 제외, `ProjectSettings/`, `Tools/ArtGen/`) | 오케스트레이터 세션 안에서 구동. 커밋은 오케스트레이터가 검수 후 대신 (Co-Authored-By 표기). 렌더러 가동 중 오케스트레이터는 같은 파일을 만지지 않는다 |
| **PLAYTESTER** | Claude Sonnet 5 (서브에이전트) | **실플레이 체감 검증** — 배포 전 브라우저 실주행(치트 F9/F10/F11 활용), 스크린샷 근거 리포트 `Reviews/from-tester/` | 코드·에셋 소유 없음. GEMINI와 경계: GEMINI = 코드·결정론·데이터의 **재현 검증**, PLAYTESTER = 화면에서 실제로 **보이고 느껴지는가**. 배포 게이트에 PLAYTESTER PASS 추가 (5중 게이트) |

**아트/사운드 생성물 규칙** (2026-07-28): AI 생성 에셋은 후보 다량 생산 → **사람(아트 디렉터) 큐레이션 합격작만** `Assets/Art/`·`Assets/Audio/`에 확정한다. 생성 프롬프트·파라미터는 산출물과 함께 커밋해 재현 가능하게 유지한다. 시뮬 상태 변화를 애니메이션·사운드로 표현해야 하면 Core의 시뮬 이벤트(REQ-005)를 구독한다 — Presentation에서 게임플레이 판정을 내리지 않는 원칙은 그대로다.

**GEMINI 확장 임무 (2026-07-29 사람 승인):**
1. **시각 리그레션 감시** — 기준 스크린샷을 `QA/baselines/`에 관리하고, 씬 재생성/대규모 병합 후 `capture_game_view` 캡처와 픽셀 diff로 배경·HUD 파손을 감지해 리포트한다.
2. **결정론 감사** — 같은 시드 2회 실행의 상태 해시 비교를 정례화한다 (CODEX가 제공하는 CoreStandalone 콘솔 러너 사용).
3. **밸런스 교차 검산** — GROK의 이론치(TTK 등)를 실제 시뮬 구동으로 재검산해 독립 검증한다.
4. **마일스톤 병합 게이트** — 마일스톤 단위(M0~M5) 병합은 GEMINI 검증 리포트 통과 후 push 한다.

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

## 9. 호출 규약 — 누구를 어떤 채널로 부르는가 (2026-08-03 사람 지시로 추가)

§2가 **누가 무엇을 소유하는지**를 정했다면, 이 절은 **실제로 어떻게 부르는지**를 정한다.
채널이 문서에 없어 그동안 오케스트레이터가 타 역할을 직접 겸하는 일이 반복됐다.

| 역할 | 호출 채널 | 작업 디렉토리 | 세션 이어가기 |
|---|---|---|---|
| **CODEX** (SIMULATION) | MCP `mcp__codex__codex` | `cwd`를 `..\wt-sim`으로 **반드시 지정** | `mcp__codex__codex-reply` |
| **GROK** (CONTENT) | MCP `mcp__grok__grok_run` | `cwd`를 `..\wt-content`로 **반드시 지정** | `session_id` 부여 → `resume_id` |
| **GEMINI** (QA) | 전용 MCP 없음 — 사람이 Gemini CLI로 직접 구동 | `..\wt-qa` | 해당 없음 |
| **RENDERER / PLAYTESTER** | Claude Agent 서브에이전트 (**사람이 명시 요청할 때만** 스폰) | `main` | `SendMessage` |
| **긴급 구조/2차 진단** | 스킬 `codex:rescue` | 현재 세션 | 스킬이 관리 |

**cwd 지정이 규칙인 이유**: `grok_run`과 `codex`는 **파일 수정 권한을 가진 에이전트**를 띄운다.
cwd를 안 주면 `main`에서 남의 소유 영역을 고칠 수 있다 — §2 위반이 사고로 일어난다.

**호출 프롬프트에 반드시 포함할 것** (없으면 상대가 규칙을 모른 채 작업한다):
1. 소유 경계 — "너는 `Assets/Scripts/Core/`만 만진다" 같은 한 줄
2. 검증 명령 — CODEX는 `dotnet test`, GROK은 JSON 스키마 검증
3. 커밋 규칙 — 자기 브랜치에만 커밋, 태스크 완료 = 커밋까지
4. REQ 번호와 `Reviews/from-claude/requests.md`의 해당 절 (맥락을 통째로 붙여넣지 말고 참조로)

**샌드박스/승인 정책**: `mcp__codex__codex`는 `sandbox: workspace-write`, `approval-policy: never`가
기본 조합이다. `danger-full-access`는 쓰지 않는다.

**Unity 조작 채널**: `unity` CLI(1.0.0-beta.3, 최신)가 표준이다 —
빌드 `unity build --target WebGL --execute-method ...`, 테스트 `unity test --mode EditMode`,
에디터 명령 `unity run --command ...`. **`Unity.exe -batchmode` 직접 호출은 금지**
(파이프라인 우회 + `ProjectSettings.asset`의 `runInBackground`를 뒤집는 부작용).
`unity list`/`unity command`와 unity-mcp 계열(`capture_game_view` 등)은 **에디터가 떠 있어야** 붙는다.

**공유 PC 제약** (사람이 해제할 때까지): 화면에 창을 띄우는 도구를 쓰지 않는다 —
검증은 headless 브라우저 + 파일 스크린샷, 빌드는 배치, 데스크톱 빌드 실행·브라우저 자동화 금지.

**결과 수령 후**: 산출물을 `Reviews/from-<호출된-에이전트>/`에 기록하고, 커밋은 §2대로
오케스트레이터가 검수 후 수행한다(Co-Authored-By 표기). 오케스트레이터 세션이 없을 때는
호출자가 대행하되 커밋 메시지에 그 사실을 남긴다.
