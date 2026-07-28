# QA Baselines — Visual Regression Monitoring System

본 디렉토리는 Roguelike_Scrolling_Shooter 프로젝트의 **시각 리그레션 감시 체계**(`AGENTS.md` GEMINI 확장 임무 1번)를 위한 기준 스크린샷(Baseline Screenshots)과 비교 지침을 관리합니다.

---

## 1. 베이스라인 캡처 목록 (총 6종)

| 파일명 | 대상 씬 / 테마 | 렌더링 설명 | 비고 |
| :--- | :--- | :--- | :--- |
| `baseline_title.png` | `Title.unity` | 타이틀 메인 화면 | 타이틀 씬 기본 캡처 |
| `baseline_scrapyard.png` | `Battle.unity` (`Background_Scrapyard`) | 스크랩야드 5겹 패럴랙스 배경 | 테마 1 베이스라인 |
| `baseline_nebula.png` | `Battle.unity` (`Background_Nebula`) | 네뷸라 배경 | 테마 2 베이스라인 |
| `baseline_hive.png` | `Battle.unity` (`Background_Hive`) | 바이오 하이브 3겹 배경 | 테마 3 베이스라인 |
| `baseline_fortress.png` | `Battle.unity` (`Background_Fortress`) | 기계 요새 3겹 배경 | 테마 4 베이스라인 |
| `baseline_core.png` | `Battle.unity` (`Background_Core`) | 코어 배경 | 테마 5 베이스라인 |

- **캡처 규격**: 1280×720 RGB PNG
- **저장 위치**: `QA/baselines/`

---

## 2. 재촬영 절차 (Recapture Procedure)

아트·HUD 레벨의 의도된 개편/리팩토링 후 기준 스크린샷을 갱신해야 하는 경우, 아래 절차에 따라 재촬영합니다.

### 2.1 사전 조건
1. Unity 에디터가 `main` 프로젝트(`D:\Unity_Work\Roguelike_Scrolling_Shooter\main`)로 실행 중이어야 합니다.
2. Unity CLI 환경변수/경로 확인: `%LOCALAPPDATA%\Unity\bin\unity.exe`

### 2.2 자동 재촬영 스크립트 실행 (권장)
```bash
python QA/tools/capture_baselines.py
```
> **참고 (CLI 버그 대응)**: Unity CLI의 `capture_game_view` 명령은 `save_path` 인자가 무시되는 알려진 버그가 있습니다. 따라서 `--format json` 옵션으로 수신한 JSON 객체 내 `data.result.base64` 필드를 파이썬에서 base64 디코딩하여 PNG 파일로 저장합니다.

### 2.3 수동 CLI 재촬영 단계별 명령

#### (1) 타이틀 씬 캡처
```bash
unity command open_scene Assets/Scenes/Title.unity
unity command editor_play
unity command capture_game_view --format json
# (JSON base64 디코딩 후 QA/baselines/baseline_title.png 로 저장)
unity command editor_stop
```

#### (2) 배틀 씬 테마별 배경 캡처
```bash
unity command open_scene Assets/Scenes/Battle.unity
unity command editor_play

# 테마 토글 (eval 명령으로 GameObject SetActive 설정)
# 예: Background_Scrapyard 활성화
unity command eval "foreach(var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) { if(r.name.StartsWith(\"Background_\")) r.SetActive(r.name == \"Background_Scrapyard\"); }"

unity command capture_game_view --format json
# (JSON base64 디코딩 후 QA/baselines/baseline_scrapyard.png 저장)

# 다른 테마도 동일하게 Background_Nebula, Background_Hive, Background_Fortress, Background_Core 토글 후 캡처
unity command editor_stop
```

---

## 3. Visual Diff 기준 및 평가 지침

### 3.1 픽셀 불일치율 임계값 (Threshold)
- **제안/기본 임계값**: **`5.0%`** (픽셀 불일치율)
- **산출 공식**:
  $$\text{Mismatch Ratio (\%)} = \left( \frac{\text{Number of Mismatched Pixels}}{\text{Total Pixels (1280} \times \text{720)}} \right) \times 100$$

### 3.2 판정 기준 (Decision Criteria)
1. **PASSED ($\le 5.0\%$)**: 
   - 스타필드 패럴랙스 미세 오프셋 차이, 렌더링 프레임 애니메이션 타이밍 차이 등 허용 범위 내 시각적 변화.
2. **FAILED ($> 5.0\%$)**:
   - 씬/배경 레이어 깨짐, HUD 미표시, 카메라/해상도 파손, 블랙 스크린, 에셋 미로드 등 시각적 리그레션 감지.

### 3.3 Visual Diff 스크립트 실행 방법

```bash
# 베이스라인과 검증 캡처 비교 (기본 threshold 5.0%)
python QA/tools/visual_diff.py QA/baselines/baseline_scrapyard.png QA/reports/current_scrapyard.png

# Diff 히트맵 이미지 출력 옵션 지정
python QA/tools/visual_diff.py QA/baselines/baseline_scrapyard.png QA/reports/current_scrapyard.png --diff-out QA/reports/diff_scrapyard.png
```

- **Exit Code**:
  - `0`: PASSED (불일치율 $\le 5.0\%$)
  - `1`: FAILED (불일치율 $> 5.0\%$)
  - `2`: 실행 에러 (파일 미존재 등)
