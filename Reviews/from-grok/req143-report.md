# REQ-143 GROK 구현 보고 — 전함 엔진 파츠를 함체 상부 구조물에 맞추기

- 작업일: 2026-08-03
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 기반: main `d4046a2` (merge main into content 후 작업)

## 문제

엔진 파츠가 계속 "배 위에 얹힌 판때기"로 보였다. 좌표를 갑판에 맞춰도
마찬가지였고, 진짜 원인은 **엔진 그림이 함체와 다른 그림**이라는 점이다.
그래서 엔진/코어 스프라이트는 함체 픽셀을 오려 만든다
(`Tools/ArtGen/cut_warship_parts.py`). 이 방식의 전제: **파츠 사각형이
실제 선체 위**에 있어야 한다. 기존 엔진 사각형은 선체를 **7%**만 덮음
(= 거의 전부 하늘). 코어는 81%로 정상이라 유지.

## 확정 수치 (사람 지시)

| 항목 | 전 | 후 | 1/256 |
|---|---:|---:|---|
| offsetX | 5.0 | **2.25** | 576 |
| offsetY | 7.5625 | **3.75** | 960 |
| halfWidth | 3.5 | **3.0** | 768 |
| halfHeight | 2.5 | **2.0** | 512 |

이 사각형은 함체 상부 구조물을 **90%** 덮는다 (사람 측정: 0.25u 격자
스캔 중 "90% 이상 덮으면서 가장 높은 자리").

### stern `anchorOffsetY`

엔진이 ~4u 내려가므로 1막 y=0 직사 피격을 위해 앵커를 올린다.

| | 전 | 후 |
|---|---:|---:|
| anchorOffsetY | −8.0 | **−4.0** |

**선정 근거**

- 엔진 월드 y = `anchorOffsetY + 3.75 ± 2.0`
- `A = −4.0` → 중심 **−0.25**, 범위 **[−2.25, +1.75]** → y=0 직사 교차
- 구 배치(A=−8, oy=7.5625) 중심 −0.4375 / 범위 [−2.94, +2.06]과
  비슷한 "아래 잠김 + 윗부분 노출" 비율
- 사람 제안 −3.75 근처이되, 중심을 약간 아래로 두어 잠김 연출을 유지
- −4.0 = −1024/256 exact

코어 파츠·hull/bow 앵커·HP·공격 패턴은 변경 없음.

## cut_warship_parts.py 검증

```text
warship_stern: 96x64  선체 덮임 90%
warship_core:  80x80  선체 덮임 81%
```

경고(55% 미만) 없음. 엔진 7% → 90%로 복구.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | engine 사각형 + stern anchorOffsetY |
| `Assets/Resources/GameData/waves.json` | 동일 동기화 (런타임 로드용) |
| `Reviews/from-grok/req143-report.md` | 본 보고 |

`Assets/Scripts`·`Assets/Tests` 미수정 (소유 밖). 코어 파츠 미수정.

## 검증

```text
cd Tools\CoreStandalone && dotnet test
→ 통과! 실패 0, 통과 571, 전체 571 (Req118 / Req119 포함)
```

## 하지 않은 것

- 아트 임포트(`Assets/Art/Sprites/`) — CLAUDE/렌더러 영역. cut 출력은
  `art-input/warship_stern.png`에 재생성됐으나 스프라이트 확정 임포트는
  Presentation 쪽 후속.
- `cut_warship_parts.py` 자체 커밋 — main 작업 트리 untracked 원본이
  CLAUDE/사람 측. 검증용으로만 실행.
- 밸런스 기본값(HP 등) §7 범위 밖 확정 변경 없음
