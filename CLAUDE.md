# CLAUDE.md — Roguelike_Scrolling_Shooter

너는 이 프로젝트의 **CLAUDE = RENDERER**다. 공용 규칙은 `AGENTS.md`를 먼저 읽어라 — 특히 §4 결정론 규칙과 §7 사람 결정 사항.

## 프로젝트

2D 횡스크롤 로그라이크 슈팅 (그라디우스 계열, SNES 도트 스타일 고해상도). Unity 6000.5.3f1, Universal 2D (URP). 아키텍처는 Simulation(순수 C#) / Presentation(Unity) / Content(JSON) 3분리.

## 네 소유 영역

- `Assets/` 전체 — **단, `Assets/Scripts/Core/`와 `Assets/Tests/EditMode/`는 제외 (CODEX 소유)**
- 씬, 프리팹, Pixel Perfect Camera, 패럴랙스, 스프라이트, 탄막 오브젝트 풀링, 입력(Input System), 오디오, UI
- `ProjectSettings/`

## 금지 사항

- `Assets/Scripts/Core/` 아래 파일 생성/수정 금지. 탄 위치 계산, 데미지, 드롭 판정 같은 **게임 로직은 전부 Shmup.Core에 있어야 한다** — 필요한 인터페이스가 없으면 직접 만들지 말고 `Reviews/from-claude/requests.md`에 요청을 남겨라.
- `GameData/*.json` 수정 금지 (GROK 소유). 수치가 필요하면 읽기만 한다.
- `MonoBehaviour.Update`에서 게임플레이 결정을 내리지 마라. Presentation은 Core의 상태를 **그리기만** 한다.
- 게임 데이터를 ScriptableObject로 만들지 마라 (AGENTS.md §5).

## 고정된 기술 결정

- Pixel Perfect Camera: Reference Resolution **640×360**, Assets PPU **16**, Filter Mode Retro AA. (2026-07-28 사람 승인으로 384×224에서 상향 — ROADMAP.md M0.) 이 값은 아트 원본 해상도라 이후 변경 금지.
- 에셋 직렬화: Force Text + Visible Meta Files (변경 금지).
- 탄/적은 반드시 오브젝트 풀링. `Instantiate`/`Destroy`를 게임 루프에서 호출하지 않는다.

## 커밋 규칙

**태스크 완료 = 커밋까지다.** 작업 트리에 변경을 남긴 채 태스크를 끝냈다고 보고하지 마라.
태스크가 끝나면 (1) 검증 명령 통과 확인 → (2) `git add` + 의미 있는 메시지로 커밋 → (3) `git log --oneline -3`으로 커밋 확인까지 하고 보고한다. 커밋되지 않은 작업은 병합 시점에 다른 브랜치와 꼬인다.

## 검증 명령

- Core 로직 (Unity 불필요): `cd Tools\CoreStandalone && dotnet test`
- Unity EditMode 테스트: Window → General → Test Runner → EditMode → Run All
- 배치모드: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile test.log`
