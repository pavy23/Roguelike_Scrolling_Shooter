# Steam 출시 준비도 기술·상품 심사

- 심사일: 2026-07-30
- 대상: `sim` worktree, `HEAD 69fb81a`
- 심사 역할: 게임 퍼블리셔 기술 심사
- 판정: **NO-GO — Steam 1.0 출시 불가**
- 권고 출시 형태: 현재 빌드는 내부 프로토타입/비공개 플레이테스트 수준. 유료 1.0 또는 유료 Early Access에도 부적합.

## 1. 총평

이 프로젝트는 “기본 전투가 작동하고 Core 단위 테스트가 많은 버티컬 슬라이스”다. “돈을 받고 반복 플레이를 약속할 수 있는 상용 로그라이크”는 아니다.

가장 큰 문제는 콘텐츠 양이 아니다. **5스테이지 런의 승리 종료가 코드에 없다.** `RunManager`는 플레이어 사망만 `RunOver`로 처리하며, 보스를 잡으면 보상·경로 선택 후 무조건 다음 스테이지를 만든다. 테마는 5개 뒤 모듈로 반복된다. 따라서 기획상 최종 보스인 5스테이지 보스를 잡아도 엔딩, 승리 결과, 크레딧, 런 완료가 없고 6스테이지로 진행한다. 이것 하나만으로도 출시 차단이다.

그 외에도 저장 교체가 실제로 원자적이지 않고, 이어하기를 검증하기 전에 원본 저장을 삭제하며, 메타 저장 실패가 메인 루프까지 예외로 전파될 수 있다. 리플레이 기록은 4,096개 RLE 구간을 넘으면 의도적으로 예외를 던지는데 Presentation이 이를 처리하지 않는다. 최신 경로 선택 기능을 포함한 현재 `HEAD`에서 저장소의 결정론 감사 스위트는 실패한다. 254개 테스트는 모두 Core 테스트이며 저장 I/O, 씬, 입력, 옵션, 승리 흐름을 전혀 실행하지 않는다.

현재 상태로 Steam에 출시하면 낮은 가격이어도 “5분짜리 데모”, “엔딩 없는 게임”, “설정/세이브가 불안정하다”, “로그라이크 빌드가 형성되기 전에 끝난다”는 평가가 중심이 될 가능성이 높다. 정상적인 1.0 기대치를 적용하면 **Mostly Negative 위험이 높고, 관대하게 보아도 Mixed 상단을 기대하기 어렵다.**

## 2. 심사 범위와 실측

### 2.1 확인한 범위

- Core 20개 C# 파일, Presentation 33개 C# 파일: 총 53개
- Core NUnit 테스트 23개 파일
- 사용자 제공 콘텐츠 실측: 적 30종, 세그먼트 38개, 보상 13종, 함선 3종, BGM 7곡, SFX 10개
- `GameData/*.json` 및 빌드용 `Assets/Resources/GameData/*.json`
- Unity 프로젝트/품질/빌드 설정
- 기존 QA 성능·결정론·시각 회귀 보고서
- 빌드 스크립트와 독립 결정론/밸런스 도구
- 상용 비교작의 공식 Steam 페이지 및 Steamworks 공식 문서

### 2.2 재검증 결과

| 항목 | 결과 | 해석 |
|---|---:|---|
| `dotnet test --no-restore` | **254/254 PASS**, 299 ms | Core의 작은 단위 계약은 양호하나 Unity/파일 I/O/전체 런은 범위 밖 |
| `dotnet run --no-restore --project Tools/DeterminismAudit -- --suite` | **FAIL** | 첫 시나리오가 `completed only 0/4 stages`로 종료 |
| PlayMode 테스트 | **0개** | `Assets/Tests/PlayMode` 자체가 없음 |
| CI 워크플로 | **없음** | `.github/workflows` 없음 |
| 현재 커밋 대상 릴리스 빌드/클린 머신 증거 | **없음** | 기존 QA는 Editor Play Mode 중심이며 현재 HEAD보다 이전 |
| GameData 원본↔Resources 복사본 | SHA-256 일치 | 현재 시점 데이터 복사 드리프트는 없음 |
| 세그먼트 | 38개, 평균 754.74틱 = **12.58초** | 사용자 실측과 일치 |
| 한 스테이지 | 3세그먼트 + 보스 | 순수 웨이브 약 37.7초 |
| 의도된 5스테이지 런 | 약 **5분** | 원 기획 25~35분의 약 1/5 |

패키지 복원을 포함한 `dotnet test`는 샌드박스가 사용자 전역 `NuGet.Config` 읽기를 거부해 실행되지 않았고, 기존 복원 산출물을 사용한 `--no-restore` 테스트는 모두 통과했다. 이는 코드 결함이 아니라 심사 환경 제약이다.

## 3. 출시 차단 결함

심각도 정의:

- **S0**: 제품 정의 또는 런 완료가 성립하지 않음. 반드시 수정 전 출시 금지.
- **S1**: 저장 손상, 진행 손실, 크래시, 재현성 붕괴 가능성이 높음.
- **S2**: 특정 환경에서 기능 상실·성능 회귀·지원 비용 급증 가능성.

### S0-1. 5스테이지 승리 상태와 엔딩이 없다

근거:

- `RunState`에는 `Playing`, `RunOver`, `AwaitingReward`, `AwaitingRoute`만 있고 `Victory`/`Completed`가 없다: `Assets/Scripts/Core/Simulation/RunManager.cs:7-16`.
- `State = RunState.RunOver`는 플레이어 HP가 0 이하일 때만 실행된다: `RunManager.cs:956-972`.
- 보스 격파 시 `BeginRewardSelection()`으로 간다: `RunManager.cs:975-983`.
- 보상·경로 선택 후 항상 `AdvanceStage()`를 호출한다: `RunManager.cs:1017-1071`.
- `AdvanceStage()`는 `StageIndex++` 후 다음 스테이지를 만든다: `RunManager.cs:1380-1403`.
- 테마 선택은 `(stageIndex - 1) % runOrder.Length`이므로 5테마 뒤 반복된다: `Assets/Scripts/Core/Generation/SegmentStageGenerator.cs:1032-1059`.
- 보스 데이터는 `stageIndexMax: 99`라 5 이후에도 계속 조립된다: `GameData/waves.json`.
- `GameOverScreen`은 패배 화면뿐이며 승리 화면/크레딧/런 완료 화면은 없다.

영향:

- “5스테이지+최종 보스”라는 제품 약속이 코드와 일치하지 않는다.
- 최종 보스 격파 성취가 사라지고, 런 통계와 메타 보상도 승리 의미를 구분하지 못한다.
- 5분이 짧은 문제가 아니라 **5분 후 끝나야 할 게임이 끝나지 않는 문제**다.

출시 전 완료 기준:

1. `Victory` 또는 `Completed` 상태, 최종 스테이지 수를 데이터/런 규칙으로 명시한다.
2. 최종 보스 격파 → 승리 결과 → 메타 반영 → 저장 정리 → 타이틀/다음 난이도 흐름을 만든다.
3. 패배와 승리의 통계·리플레이·메타 보상을 분리한다.
4. 1~5스테이지 전체를 실제 GameData로 통과하는 Core/PlayMode E2E 테스트를 추가한다.

### S1-1. 저장 파일 교체가 원자적이지 않고 백업이 없다

근거:

- `MetaSave.Save`: 임시 파일 작성 후 기존 파일을 삭제하고 임시 파일을 이동한다: `Assets/Scripts/Presentation/Battle/MetaSave.cs:68-80`.
- `RunSave.Save`: 같은 `Write temp → Delete original → Move temp` 순서다: `RunSave.cs:17-29`.
- `ReplaySave.Save`: 동일하다: `ReplaySave.cs:29-41`.
- 기존 파일 삭제와 새 파일 이동 사이에 프로세스 종료, 전원 손실, 백신/동기화 잠금, 디스크 오류가 발생하면 정상 파일이 사라진다.
- `.tmp` 복구, `.bak` 세대 백업, 체크섬, 저널, `File.Replace` 계열의 교체 전략이 없다.

추가 위험:

- `MetaSave.Save`에는 예외 처리가 없다. 게임오버 틱에서 `MetaSave.Save`를 직접 호출하므로 쓰기 실패가 `FixedUpdate` 예외로 전파된다: `BattleDirector.cs:479-488`.
- Hangar에서 잠금 해제/함선 선택 때도 `MetaSave.Save`를 직접 호출한다: `HangarScreen.cs:91-104`.
- 메타 저장에는 명시적 `schemaVersion`이 없고 마이그레이션/무결성 표식도 없다: `MetaSave.cs:15-21`.
- 메타 로드 실패는 백업 복구가 아니라 신규 상태로 초기화한다: `MetaSave.cs:26-53`. 손상 한 번으로 해금/재화 전체가 사라질 수 있다.
- 프로젝트 `companyName`이 아직 `DefaultCompany`다: `ProjectSettings/ProjectSettings.asset:15`. 출시 전에 회사명을 바꾸면 `Application.persistentDataPath`가 달라져 기존 저장이 사라진 것처럼 보일 수 있으므로 명시적 마이그레이션이 필요하다.

### S1-2. 이어하기가 복구 검증 전에 원본 저장을 삭제한다

근거:

- 타이틀에서 Continue 입력 즉시 `PendingResume`에 메모리 참조를 넣고 `RunSave.Delete()`를 호출한 뒤 Battle 씬을 로드한다: `TitleScreen.cs:132-140`.
- 실제 구조 검증과 복원은 Battle 씬 `Awake()`의 `ResumeFromSuspendData`에서 나중에 수행한다: `BattleDirector.cs:330-352`.
- 복원이 실패하면 경고 후 새 런을 시작하지만, 원래 `run.json`은 이미 삭제됐다.

영향:

- 업데이트 후 스키마/콘텐츠 불일치, 손상, 함선 ID 변경, 일시적 로드 예외가 발생하면 플레이어는 Continue를 누르는 순간 복구 가능한 원본까지 잃는다.
- 성공적으로 Battle 씬이 떠서 첫 안전 저장이 완료되기 전에 크래시해도 원본은 없다.

출시 전 완료 기준:

- 복원 성공 및 새 체크포인트 커밋 뒤에만 이전 저장을 소비한다.
- 유효성 검사 실패 시 원본과 백업을 보존하고 사용자에게 복구/새 게임 선택을 준다.
- 스테이지 경계 자동 저장, `OnApplicationPause`/`OnApplicationFocus` 대응, 강제 종료·전원 손실 테스트를 추가한다.

### S1-3. 런 저장은 정상 종료 콜백과 수동 키보드 흐름에 과도하게 의존한다

근거:

- 자동 저장 진입점은 사실상 `OnApplicationQuit()`이다: `BattleDirector.cs:215-226`.
- 일시정지 화면에서 타이틀 저장은 키보드 `Q`에만 연결돼 있고 게임패드에는 동등한 기능이 없다: `PauseScreen.cs:59-79`.
- `ProjectSettings`의 `runInBackground: 1` 때문에 Alt-Tab 중에도 게임이 계속 진행될 수 있다: `ProjectSettings/ProjectSettings.asset:90`.
- OS 강제 종료, 크래시, Steam Deck 절전/프로세스 정리, 작업 관리자 종료에서는 `OnApplicationQuit` 보장이 없다.
- 저장되는 내용도 현재 틱이 아니라 현재 스테이지 시작 체크포인트다: `RunManager.cs:723-807`.

판정:

- 스테이지 처음부터 재개하는 정책 자체는 허용 가능하나, 현재는 그 정책조차 안정적으로 디스크에 남지 않는다.
- Steam Deck 지원을 표기하려면 절전/재개와 컨트롤러만으로 안전 종료를 반드시 검증해야 한다.

### S1-4. 리플레이 입력 버퍼 소진이 플레이 중 무처리 예외가 된다

근거:

- `InputRecorder` 기본 RLE 구간 용량은 4,096이다: `Assets/Scripts/Core/Simulation/InputRecording.cs:64-78`.
- 서로 다른 명령이 4,096개를 넘으면 `InvalidOperationException`을 던진다: `InputRecording.cs:129-147`.
- 이 동작은 테스트로 의도적으로 고정되어 있다: `Assets/Tests/EditMode/InputRecordingTests.cs:322-333`.
- 실제 전투에서는 매 Playing 틱마다 `_recorder.Record()`를 예외 처리 없이 호출한다: `BattleDirector.cs:440-477`.
- 5분 런은 약 18,000틱이다. 방향/발사/활성화 상태가 평균 4틱마다 바뀌면 4,500개 RLE 구간으로 한도를 넘는다. 아날로그 스틱 임계값 주변 노이즈나 빠른 탭 입력으로 현실적으로 도달 가능하다.

영향:

- 리플레이는 부가 기능인데, 리플레이 기록 실패가 본 게임 진행을 중단시키는 구조다.

출시 전 완료 기준:

- 성장 가능한 버퍼 또는 명시적인 최대 메모리 정책을 사용한다.
- 한도 도달 시 리플레이만 비활성화하고 플레이는 계속돼야 한다.
- 최장 목표 런의 최악 입력 패턴, 저용량 디스크, 손상 리플레이를 테스트한다.

### S1-5. 손상 리플레이가 Battle 씬 초기화를 깨뜨릴 수 있다

근거:

- `ReplaySave.TryLoad()`는 외부 DTO가 있고 `recording != null`인지만 확인한다: `ReplaySave.cs:45-56`.
- 실질적인 스키마·RLE 검증은 `new InputPlayback()`에서 예외를 던지는 방식이다: `InputRecording.cs:302-348`, `374-468`.
- `BattleDirector.Awake()`는 이 생성자를 복원용 try/catch 바깥에서 호출한다: `BattleDirector.cs:304-318`.

영향:

- 부분 기록, 구버전, 클라우드 충돌, 사용자 파일 손상 시 Battle 초기화가 중단될 수 있다.
- 손상 리플레이는 삭제/격리하고 타이틀로 안전 복귀해야지 전투 씬을 망가뜨리면 안 된다.

### S1-6. 최신 결정론 감사가 실패하며 기존 QA PASS는 현재 HEAD 증거가 아니다

재현 명령:

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

결과:

```text
suite=determinism-audit-02 scenarios=5 state=full-observable
Determinism audit failed: InvalidOperationException:
Scenario 'seed-0-first' completed only 0/4 stages.
```

코드 원인 정황:

- 감사 루프는 `AwaitingReward`만 처리하고 최신 `AwaitingRoute`를 처리하지 않는다: `Tools/DeterminismAudit/Program.cs:135-177`.
- 최신 경로 선택 통합 커밋 `ae4e67a`는 2026-07-29 22:00이고, 마지막 성능 PASS 대상 `276b03a`는 같은 날 12:52다.
- 기존 결정론 리포트는 130개 테스트, 최종 스윕은 145개 테스트를 기록한다. 현재는 254개다. 기존 보고서가 현재 HEAD에 대한 릴리스 증명일 수 없다.

판정:

- Core 자체의 결정론 위반을 이번 실패만으로 단정할 수는 없다. 감사 하네스가 최신 상태를 따라오지 못한 것이 직접 원인이다.
- 그러나 **릴리스 게이트 도구가 깨진 상태**이므로 “결정론 검증 완료” 주장도 철회해야 한다.

### S1-7. 시작 데이터 예외에 대한 사용자 안전 경로가 없다

근거:

- 필수 Resources가 없으면 `LoadGameDataText`가 예외를 던진다: `BattleDirector.cs:832-846`.
- `GameDataParser.Parse`와 필수 리소스 로드는 `Awake()` 최상위에서 보호되지 않는다: `BattleDirector.cs:284-299`.
- Hangar도 같은 방식으로 필수 JSON을 파싱한다: `HangarScreen.cs:31-36`, `145-156`.

영향:

- 빌드 파이프라인 실수, 손상된 배포, 잘못된 Addressables/Resources 정리, 데이터 스키마 회귀가 있으면 “안전한 오류 화면”이 아니라 플레이 불능이 된다.
- 필수 콘텐츠 오류는 RC 빌드에서 절대 없어야 하지만, 방어 경로와 빌드 전 검증 둘 다 필요하다.

## 4. 성능·플랫폼·회귀 위험

### S2-1. 성능 보고서는 릴리스 성능 증거로 부족하다

기존 QA에서 확인되는 긍정적 수치:

- Editor Play Mode에서 CPU 2.30 ms 이하, GPU 1.90 ms 이하를 기록한 패스가 있다.
- SpriteAtlas 적용 뒤 약 57 draw calls / 9 SetPass를 기록했다.
- Core 정상 상태의 관리 메모리 0-byte 할당 테스트가 존재한다.

그러나 출시 판정에는 부족하다:

- 모두 단일 개발 PC의 Editor 측정이며 하드웨어, 해상도, GPU 드라이버, 전원 모드가 보고서에 없다.
- 평균/순간 스냅샷뿐이고 p95/p99/p99.9 프레임 타임, GC.Alloc/frame, 1% low, 장시간 soak가 없다.
- 마지막 성능 패스 대상은 `276b03a`; 현재 `HEAD 69fb81a`에는 이후 경로 선택, 38세그먼트, 희귀 인카운터, 자석 드롭, 텔레그래프 변경이 포함된다.
- 최신 HEAD의 Windows Release 플레이어, 클린 머신, 저사양 내장 GPU, 4K, 다중 모니터, 게임패드 hot-plug 측정이 없다.
- `BattleSim`은 탄 제거 때 9개의 병렬 `List`에서 각각 `RemoveAt`을 수행한다: `BattleSim.cs:2818-2827`. 현재 384탄 상한에서는 대체로 감당 가능하겠지만 고밀도 제거 프레임의 최악값을 Release 빌드에서 측정해야 한다.
- Presentation 적 풀은 고정 32개다: `BattleDirector.cs:394-403`. 실제 데이터의 한 세그먼트는 최대 39회 스폰(`seg_core_final_gauntlet`)하므로, 생존 시간/세그먼트 겹침을 포함한 풀 소진 스트레스 증명이 필요하다. 소진 시 시뮬 적은 존재하지만 화면에서 사라진다: `SpritePool.cs:40-52`, `BattleDirector.cs:923-951`.

### S2-2. Windows 개발 빌드 외 플랫폼 전략이 없다

근거:

- `build.ps1`은 고정 경로의 Unity 6000.5.3f1과 `-buildWindows64Player`만 호출한다.
- 테스트, 데이터 검증, Release/Development 플래그 확인, 버전 주입, 심볼 보관, Steam depot 업로드를 빌드 단계에 연결하지 않는다.
- 패키지 목록에 실험 버전 `com.unity.pipeline: 0.4.0-exp.1`이 포함돼 있다: `Packages/manifest.json:16`. 출시 후보에서는 사용 필요성, 플레이어 빌드 영향, 업데이트 고정 및 제거 가능성을 검증해야 한다.
- `companyName: DefaultCompany`, `bundleVersion: 1.0`, 명시적 application identifier 없음: `ProjectSettings/ProjectSettings.asset:15-16`, `151`, `171-179`.
- 크래시 보고 API는 꺼져 있고(`enableCrashReportAPI: 0`), 자체 예외/로그 수집도 없다.
- Steamworks SDK/패키지/호출, App ID, 업적, Cloud, Rich Presence 코드는 검색되지 않는다.
- macOS/Linux 빌드 파이프라인과 테스트가 없다. Windows 전용 출시는 가능하지만 스토어에서 그 사실을 명확히 해야 한다.

Steam Deck:

- Proton에서 Windows 빌드가 실행된다는 증거가 없다.
- 타이틀/전투 일부는 게임패드를 지원하지만, 일시정지에서 저장 후 타이틀 복귀가 키보드 Q 전용이다.
- 화면 모드가 4개 고정 해상도와 단순 fullscreen bool뿐이다: `OptionsScreen.cs:20-26`, `250-257`.
- 따라서 지금 “Full Controller Support”나 “Steam Deck 호환”을 표기하면 안 된다.

### S2-3. 테스트 수 254개가 주는 허위 안전감

현재 테스트가 잘 보는 것:

- RNG, 파서, 정수 시뮬, 전투 충돌/점수, 보스, 보상, 경로, 중단 데이터 검증.

보지 않는 것:

- `MetaSave`, `RunSave`, `ReplaySave` 실제 파일 교체와 전원 손실.
- Title → Battle → Reward → Route → Final Boss → Victory 전체 흐름.
- Unity 씬 참조 누락, Resources 패킹, 입력 리바인딩, PlayerPrefs 손상.
- 해상도/전체화면/다중 모니터, 오디오 장치, 게임패드 hot-plug.
- Release 플레이어에서의 IL2CPP/Mono 차이, 클린 머신, Steam Overlay/Cloud.
- 25~35분 장기 런, 최악 탄막, 풀 소진, 반복 재시작.

결론:

- 254개는 Core 라이브러리 품질 지표이지 게임 출시 품질 지표가 아니다.
- PlayMode E2E, 저장 fault injection, 실제 플레이어 빌드 자동화가 없으므로 회귀 가능성은 높다.

## 5. 상용 로그라이크 대비 구조적 부족분

비교는 장르가 완전히 같은가가 아니라 “반복 런을 판매하는 제품이 어떤 깊이와 기술 서비스를 제공하는가”를 기준으로 했다.

| 비교작 | 상용 구조 | 본 프로젝트와의 격차 |
|---|---|---|
| Slay the Spire | 350+ 카드, 200+ 아이템, 50+ 전투, 50+ 이벤트, 4캐릭터, Daily/Custom, Cloud/업적/리더보드 | 보상 13종 중 규칙 변경 modifier는 4개. 이벤트/상점/리스크 비용/커스텀/온라인 비교 없음 |
| Hades | 수십 Boon과 수천 빌드, 영구 성장, 보스/대사 반응, 수천 스토리 이벤트 | 메타는 재화·함선 해금 중심. 런 간 세계 반응/서사/시스템 해금 계층 없음 |
| Dead Cells | 분기 경로, 무기·변이·능력·지역 영구 해금, 최종 보스까지 명확한 런, Cloud/업적 | 경로는 생겼지만 선택 결과의 장기적 차별성이 작고 승리 종료가 없음 |
| Vampire Survivors | 짧고 단순한 조작을 수백 적, 대량 해금, 진화 조합, 비밀, 243 업적, Cloud/협동으로 보완 | 짧은 런을 지탱할 해금/진화/비밀/도전 밀도가 없음 |
| Nova Drift | “빠른 런”을 200+ 모듈 업그레이드, 수십 Super Mod, 기체/무기/실드 조합, 리더보드로 보완 | 가장 직접적인 비교. 현재 13보상/4 modifier로는 빌드 실험 공간이 두 자릿수 시간도 버티기 어려움 |
| Everspace | 절차 레벨, 전리품, 제작, 자원 관리, 영구 성장, 스토리, 3함선, 하드코어 모드 | 함선 수만 3으로 같을 뿐, 전리품/제작/자원/서사/난이도 모드의 구조가 없음 |

공식 근거:

- [Slay the Spire Steam 페이지](https://store.steampowered.com/app/646570/Slay_the_Spire/?l=english)
- [Hades Steam 페이지](https://store.steampowered.com/app/1145360/Hades/)
- [Dead Cells Steam 페이지](https://store.steampowered.com/app/588650/Dead_Cells/)
- [Vampire Survivors Steam 페이지](https://store.steampowered.com/app/1794680/Vampire_Survivors/)
- [Nova Drift Steam 페이지](https://store.steampowered.com/app/858210/Nova_Drift/?l=english)
- [EVERSPACE Steam 페이지](https://store.steampowered.com/app/396750/EVERSPACE/?l=english)

### 5.1 5분 런의 상품성 판정

5분 런 자체가 실패 조건은 아니다. 빠른 런을 파는 게임은 다음 중 하나가 필요하다.

1. 매우 높은 빌드 조합 밀도와 빠른 재시도,
2. 점수 경쟁/리더보드/데일리 변형,
3. 강한 메타 해금과 비밀,
4. 짧지만 완결된 아케이드 캠페인과 다수 난이도/캐릭터,
5. 반복할수록 바뀌는 사건·서사.

현재 프로젝트는 어느 축도 충분하지 않다.

- 5스테이지를 정상 종료한다고 가정해도 최종 전투 전까지 받는 스테이지 보상은 몇 회에 불과하다.
- 13보상 중 다수는 단순 슬롯 +1, 회복, 발사속도/공격/이동 증가다.
- 규칙을 바꾸는 조합은 관통, 도탄, 유도 미사일, 처치 폭발 4개뿐이다.
- 3함선의 주무기 차이는 출발점 차이는 만들지만, 한 런 안에서 빌드가 “완성됐다”는 감각을 만들 선택 수가 부족하다.
- 세그먼트 38개는 좋은 재료지만 한 런 15세그먼트이고, 같은 5테마·5보스 구조를 빠르게 소진한다.

따라서 선택지는 둘 중 하나다.

- 원 기획대로 25~35분 런으로 복구하고 선택·인카운터·빌드 계층을 확장한다.
- 5~10분 마이크로 런으로 재정의하되 40~60개 이상의 의미 있는 업그레이드, 진화/태그 시너지, 데일리/점수 경쟁, 다수 난이도와 해금을 추가한다.

현재처럼 “5분인데 깊이도 얕고 종료도 없는” 중간 상태는 판매하면 안 된다.

## 6. 출시 전 우선순위

### P0 — 해결 전 출시 금지

1. **5스테이지 승리 종료 구현**
   - 데이터 기반 `FinalStageIndex`, Victory 상태, 결과/크레딧, 승리 메타 반영, 최종 저장 정리.
2. **세이브 시스템 재설계**
   - schema version, 체크섬, `.bak` 2세대, 원자 교체, 복원 성공 후 원본 소비, 자동 저장, pause/focus/quit 대응.
   - `DefaultCompany`를 출시 ID로 확정하고 기존 경로 마이그레이션.
3. **리플레이 실패 격리**
   - 4,096 구간 한도 제거 또는 안전 중단, 손상 파일 검증/격리, 플레이 본체에 예외 전파 금지.
4. **최신 결정론 감사 복구**
   - `AwaitingRoute` 처리, 현재 GameData로 5스테이지 완주 2회 해시 일치, CI 필수 게이트.
5. **Unity PlayMode E2E 추가**
   - 새 게임/Continue/패배/재출격/승리/타이틀, 키보드와 게임패드, 손상 세이브를 자동 검증.
6. **현재 HEAD Release Candidate 생성**
   - Windows x64 비개발 빌드, 버전/커밋 표시, 심볼 보관, 클린 머신 설치/실행/삭제, Steam depot smoke.
7. **상용 런 구조 확정**
   - 25~35분으로 복구하거나 5~10분 마이크로 런으로 재정의. 어느 쪽이든 현재보다 훨씬 높은 선택·시너지 밀도가 필요.
8. **Steam Cloud와 저장 충돌 시험**
   - 메타/중단/설정을 구분하고, Cloud는 기기 종속 그래픽 설정을 제외한다. Steam 공식 문서도 기기별 설정을 Cloud에 넣지 말라고 권고한다.
9. **플랫폼·입력 출시 표기 정합**
   - 게임패드만으로 모든 메뉴/저장/종료 가능, hot-plug, Xbox/PlayStation 계열 패드, Steam Input 검증.
10. **최소 2회의 외부 플레이테스트와 RC 버그 번다운**
    - 개발자/에이전트 자동주행이 아닌 신규 플레이어 20명 이상, 크래시/진행 차단 0건, 세이브 손실 0건.

### P1 — 1.0 상품성을 위해 강하게 권고

1. 40~60개 이상 의미 있는 런 업그레이드와 시너지 태그/진화 조합.
2. 엘리트·보급·희귀 경로의 위험/보상 차별화, 상점/이벤트/저주 등 비전투 선택.
3. 업적, 데일리 리더보드, 커스텀 모드, 난이도 승천/Heat 계열.
4. BGM/SFX 분리 볼륨, AudioMixer, 오디오 장치 변경, 반복 SFX 제한.
5. 실제 디스플레이 목록 기반 해상도, 창/무테/전체화면, 다중 모니터, 16:10/울트라와이드 검증.
6. 한/영 최소 2개 언어의 문자열 테이블화. 현재 하드코딩 문자열 상태로는 로컬라이즈 출시가 불가능.
7. 크래시 로그 수집, 개인정보 고지, 버전/시드/스택/하드웨어를 포함한 사용자 제출 패키지.
8. p95/p99 프레임 타임, 1% low, GC.Alloc, 풀 최대 사용량을 기록하는 30~60분 Release soak.
9. 스코어/메타 재화 경제 재검증. 1.0 밸런스는 자동 시뮬뿐 아니라 실제 플레이 분포로 확정.
10. 아트·오디오 사람 큐레이션과 라이선스/생성 파라미터/Steam AI disclosure 정리.

### P2 — 있으면 좋은 항목

1. Rich Presence, Trading Cards, Workshop/공유 시드, 고스트 리플레이.
2. 색각 모드, 탄 가시성 옵션, 진동 강도, 추가 플래시/화면 흔들림 세분화.
3. macOS/Linux 네이티브 빌드. 단, 지원을 약속할 때만 테스트 비용을 감수한다.
4. 리플레이 버전 간 마이그레이션 또는 버전 고정 보관.
5. 세그먼트/보스 모딩 스키마와 콘텐츠 검증 툴.

## 7. Steam 출시 운영 관점

Steam의 빌드 리뷰 통과는 품질 인증이 아니다. Valve는 스토어와 빌드가 기본 체크리스트를 충족하는지 검토하며 보통 3~5영업일, 여유 있게 최소 7영업일 전 제출을 권고한다. 현재 문제는 그 행정 기간 이전 단계다.

- [Steamworks Review Process](https://partner.steamgames.com/doc/store/review_process)
- [Steam Cloud](https://partner.steamgames.com/doc/features/cloud?language=english)
- [Steam User Reviews](https://partner.steamgames.com/doc/store/reviews)

Steam 공식 문서는 사용자가 게임을 실행한 뒤 기대 충족 여부를 리뷰하며, 리뷰 점수가 40% 미만인 Mostly Negative로 내려가면 상점 노출 가능성이 낮아진다고 설명한다. 엔딩 부재와 저장 손실은 첫 2시간 안에 발견될 수 있어 초기 리뷰에 특히 치명적이다.

## 8. 예상 평가와 필요한 추가 기간

### 지금 출시할 경우

가장 가능성 높은 초기 평가:

- **Mostly Negative 위험 높음(대략 20~40% 긍정 예상)**.
- 매우 낮은 가격, 정직한 “짧은 아케이드 프로토타입” 포지셔닝, 소수 우호적 고객만 확보하면 Mixed에 근접할 수 있으나 1.0 로그라이크로 팔면 어렵다.
- 주요 부정 리뷰 예상 문구:
  - “최종 보스를 잡아도 끝나지 않는다.”
  - “5분 만에 본 콘텐츠가 반복된다.”
  - “업그레이드가 숫자 증가뿐이다.”
  - “이어하기/저장이 믿을 수 없다.”
  - “게임패드 완전 지원이 아니다.”
  - “업적·Cloud·언어·설정이 없는 데모 같다.”

### 추가 기간

현 인력 구조와 생성형 에이전트의 속도를 고려해도 사람 플레이·밸런스·RC 안정화 시간은 압축할 수 없다.

- **비공개/무료 공개 데모 가능 상태:** 최소 8~12주
  - 승리 흐름, 세이브, 리플레이 격리, 최신 감사/PlayMode, Windows RC, 게임패드 전체 흐름.
- **판매 가능한 Early Access 후보:** 최소 4~6개월
  - 빌드 다양성, 20분 이상 반복 구조 또는 마이크로 런 재설계, Cloud, 외부 테스트, 운영 도구.
- **신뢰 가능한 Steam 1.0:** **최소 6개월, 현실적으로 9~12개월**
  - 원 기획 25~35분, 상용 로그라이크 수준의 선택 밀도, 로컬라이제이션, Steam 기능, RC 안정화 포함.

기간보다 중요한 것은 게이트다. 아래를 만족하지 못하면 일정이 되어도 출시하지 말아야 한다.

1. 5스테이지 승리 E2E 100회 자동 완주, 진행 차단 0.
2. 강제 종료/손상/구버전/Cloud 충돌 저장 시험에서 영구 손실 0.
3. 현재 HEAD 결정론 suite 전부 PASS.
4. Windows Release 60분 soak, p99 16.67 ms 이내 목표, 풀 소진 0, 미처리 예외 0.
5. 신규 플레이어 20명 이상의 첫 2시간 테스트에서 종료 이해·입력·세이브 차단 0.
6. 콘텐츠 설문에서 “한 런 안에 빌드가 달라졌다”와 “다시 할 이유가 있다”가 명확히 확인될 것.

## 9. 최종 판정

**Steam 1.0: 출시 불가.**  
**유료 Early Access: 출시 불가.**  
**무료 데모: P0 기술 결함 해결 후 가능.**

Core의 정수 결정론 설계, 데이터 파서, 254개 단위 테스트는 좋은 기반이지만, 그것은 엔진 부품의 품질이다. 현재 게임 제품은 승리 종료, 안전한 저장, 최신 E2E 검증, Steam 서비스, 반복 플레이 깊이가 빠져 있다. 특히 기존 QA의 “SHIP READY”는 현재 HEAD와 테스트 범위를 반영하지 못하므로 출시 의사결정 근거에서 제외해야 한다.

퍼블리셔 게이트는 **NO-GO**다.
