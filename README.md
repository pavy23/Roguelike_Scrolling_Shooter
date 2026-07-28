# Roguelike Scrolling Shooter

2D 횡스크롤 로그라이크 슈팅 (그라디우스 계열, SNES 감성 hi-bit 픽셀아트 지향).
Claude(RENDERER) / Codex(SIMULATION) / Grok(CONTENT) / Gemini(QA) 4-에이전트 + 사람 오케스트레이션으로 개발한다.
에이전트 규칙은 [AGENTS.md](AGENTS.md), 아트 방향은 [ART-DIRECTION.md](ART-DIRECTION.md) 참고.

## 요구 사항

| 항목 | 버전 |
|---|---|
| Unity | **6000.5.3f1** (Universal 2D/URP 템플릿 기반) |
| .NET SDK | 8.0 이상 (Core 테스트용 — net10.0 타깃) |
| git | 2.40+ |
| (선택) 에이전트 CLI | claude / codex / grok / agy — 각자 로그인 필요 |

## 새 컴퓨터에서 받기

```powershell
mkdir D:\Unity_Work\Roguelike_Scrolling_Shooter
cd D:\Unity_Work\Roguelike_Scrolling_Shooter
git clone https://github.com/pavy23/Roguelike_Scrolling_Shooter.git main
cd main
git worktree add ..\wt-sim sim          # CODEX 작업 폴더
git worktree add ..\wt-content content  # GROK 작업 폴더
git worktree add ..\wt-qa qa            # GEMINI 작업 폴더
```

> 경로는 자유지만, 문서·스크립트가 `D:\Unity_Work\Roguelike_Scrolling_Shooter` 기준으로 쓰여 있어 같은 경로를 권장.

### 검증 (Unity 안 열고)

```powershell
cd main\Tools\CoreStandalone
dotnet test        # 전부 초록이어야 정상
```

### Unity 열기 / 빌드

- 에디터: Unity Hub → `main` 폴더를 6000.5.3f1로 열기 (첫 임포트 수 분)
- 씬 재생성(스프라이트·씬·GameData 복사 일괄): 에디터 메뉴 `Tools → Shmup → Rebuild Battle Scene`
  또는 헤드리스: `Unity.exe -batchmode -projectPath . -executeMethod Shmup.EditorTools.BattleSceneBuilder.Build -logFile scene.log` (내부에서 스스로 종료)
- 플레이어 빌드: `main`에서 `powershell -ExecutionPolicy Bypass -File .\build.ps1` → `Builds\Dev\RSS.exe` (창모드 1152×672)
- 시드 고정 실행: `RSS.exe --seed=12345`

## git에 들어가지 않는 로컬 설정 (이전 PC에서 복사)

| 파일 | 용도 |
|---|---|
| `D:\Unity_Work\.claude\settings.json` | 오케스트레이터가 에이전트 CLI를 헤드리스로 띄우는 권한 규칙 |
| `main\.claude\settings.local.json` | RENDERER(Claude) 모델 지정 (`{"model": "claude-opus-5", "effortLevel": "high"}`) |
| `%USERPROFILE%\.gemini\antigravity-cli\settings.json` | agy(QA) 헤드리스 도구 권한 (`command(*)`, `read_file(*)`, `write_file(*)`) |

## 저장소 구조

```
main\                     Unity 프로젝트 + git 저장소 루트 (CLAUDE=RENDERER 작업 위치)
  Assets\Scripts\Core\    Shmup.Core — 순수 C# 결정론 시뮬레이션 (CODEX 소유)
  Assets\Scripts\Presentation\  Unity 표현 계층 (CLAUDE 소유)
  Assets\Tests\EditMode\  NUnit 테스트 — Unity Test Runner와 dotnet test가 같은 파일 공유
  Tools\CoreStandalone\   Unity 없이 dotnet test 하는 링크 프로젝트
  GameData\               게임 데이터 JSON 원본 (GROK 소유) — 빌드 시 Resources로 복사됨
  QA\                     테스트 플랜/캡처/리포트 (GEMINI 소유)
  Reviews\from-*\         에이전트 간 요청/응답 프로토콜
wt-sim\  wt-content\  wt-qa\   에이전트별 git worktree (위 명령으로 재생성)
```

## 작업 흐름 요약

1. 각 에이전트는 자기 worktree/브랜치에서만 작업하고 커밋한다 (소유 영역: AGENTS.md §2)
2. 병합은 사람(오케스트레이터)이 main에서 수행 → `dotnet test` + Unity EditMode 테스트 통과 확인
3. `build.ps1`로 빌드 → `RSS.exe` 플레이 테스트 → 피드백을 다음 사이클 태스크로
4. 세션 종료 시 `git push origin --all`
