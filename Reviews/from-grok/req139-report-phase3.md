# REQ-139 GROK 구현 보고 — 전함 로봇 form2 (3차)

- 작업일: 2026-08-03
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행 Core: `ba556aa` (BeginWarshipFormTransition) + `a8ee9eb` (warship+form2 카탈로그 허용)
- 결과: **PASS** (`dotnet test` 568/568)

## 1. 무엇을 넣었나

`boss_fortress`에 기존 `form2` 스키마로 로봇 페이즈 추가. 함체 3막(엔진 미사일 → 포탑 레이저 → 코어) 전멸 후, Core가 함체를 끊고 form2로 전환한다.

| 필드 | 값 | 근거 |
|---|---:|---|
| `id` | `boss_fortress_robot` | 함체 id와 분리된 로봇 본체 식별 |
| `transitionTicks` | **300** (5.0s) | `boss_core` form2는 180t(3s). 사람 피드백 "폭발이 너무 짧아 클리어 감흥이 없다" — 거대 함체 붕괴 연출 여유 |
| `hp` | **8000** | 함체 19600 이후 **추가** 전투. fortress reach DPS 720 기준 pure ST ≈ **11.1s**. core form2 14000(≈19s)보다 짧아 3막 뒤 과연장 방지 |
| `halfWidth` × `halfHeight` | **2.5 × 2.0** | 함체 17×8.5 대비 ≈1/7 폭. "안에서 나온 기민 파일럿" 스케일. 파츠 없음(body only, core form2와 동일) |
| `holdX` | **10.0** | 함체 holdX 12보다 앞. 돌진 대기 위치를 플레이어 쪽에 둬 근접 위협 체감 |

모든 좌표·크기는 1/256 서브유닛 격자 (×256 정수).

## 2. 패턴 (함체와 대비)

| 막 | 주체 | 어휘 | 이동 |
|---|---|---|---|
| 1 | engine | aimedSpread 저속 (미사일 체감) | 함체 정박 |
| 2 | turret×4 | laser | 함체 정박 |
| 3 | core | radialSpread | 함체 정박 |
| **form2 p1** | robot | **aimed** heavy 3-way / 18t / 12.5 | **lungeReturn** amp 8 / 90t / tel 18 |
| **form2 p2** | robot (HP≤50%) | **burst** heavy 5-way / 12t / 14.0 tel 12 | **lungeReturn** amp 10 / 72t / tel 12 |

- 레이저·radial 봉쇄를 쓰지 않음 → 앞 막과 어휘 분리.
- `lungeReturn` = 예고 후 돌진·복귀 (근접·돌진 계열 권고 그대로).
- p2는 더 깊은 돌진 + 짧은 주기 + burst 예고 산탄으로 클라이맥스만 압박.

## 3. TTK 스케치 (provisional §7, reach 720)

| 구간 | HP | pure ST |
|---|---:|---:|
| 함체 합 | 19600 | ≈27.2s |
| 전환 | — | 5.0s |
| 로봇 | 8000 | ≈11.1s |
| **총 (함체+로봇)** | **27600** | **≈43s** (+전환) |

core form1+form2(42000, ≈43s fight TTK @1050)과 비슷한 총 길이지만, fortress는 부위 파괴·정박이 시간을 나눠 먹어 "한 덩어리 보스"보다 덜 늘어진다.

## 4. Core 블로커 대행

`ba556aa` 시뮬 경로는 준비됐으나 `StagePlan`/`StageBossTemplate`이 아직  
`Warship encounters cannot define a second boss form` 으로 거부.

- CODEX MCP usage limit → **GROK이 최소 가드 삭제 대행** (`a8ee9eb` on `sim`)
- 변경: `IStageGenerator.cs`, `SegmentStageGenerator.cs` 각 1블록 제거 + 주석
- CODEX 복귀 시 리뷰 대상 (`Reviews/from-grok/requests.md`에 한 줄)

## 5. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | `boss_fortress.form2` 로봇 |
| `Assets/Resources/GameData/waves.json` | 동일 동기화 |
| `Tools/BalanceSim/Program.cs` | REQ-116 fortress form2 게이트 (hp/transition/크기/lunge) |
| `Reviews/from-grok/req139-report-phase3.md` | 본 보고 |
| *(sim)* Core 2파일 | warship+form2 허용 |

## 6. 검증

```text
cd Tools\CoreStandalone && dotnet test
→ 통과: 568 / 전체: 568
```

BalanceSim REQ-111 / REQ-116 fortress form2 라인 PASS.  
(사전 실패 2건: scoring graze gauge, colossal TTK band — 본 REQ 무관)

## 7. 후속 (다른 에이전트)

1. **CLAUDE**: `boss_fortress_robot` 스프라이트 + 함체 붕괴→로봇 사출 연출 (`BossFormTransitionStarted` / `BossFormChanged`)
2. **CODEX**: 가드 삭제 커밋 리뷰
3. **PLAYTESTER**: form2 전환 순간 1–2초 샘플링으로 로봇 캡처
