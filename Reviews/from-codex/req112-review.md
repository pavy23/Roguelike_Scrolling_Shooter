# REQ-112 CODEX 인수 리뷰 — progression multipart/warship 난이도 보정

- 작업일: 2026-08-02
- 담당: CODEX / SIMULATION
- 대상: GROK REQ-111의 `RunManager.ApplyProgressionBossDifficulty` 긴급 수정
- 최종 판정: **재구현 후 승인 (PASS)**

## 결론

GROK이 보고한 크래시는 실재한다. 테마 셔플로 현재 진행 위치와 생성 테마가 달라지면 기존 로직은 `BossMaxHp`만 진행 위치의 보스 값으로 교체하고 `BossParts`는 생성 테마 값을 유지했다. `StagePlan`은 multipart 보스에 대해 `BossMaxHp == sum(parts.MaxHp)`를 강제하므로 두 값이 다를 때 `Multipart boss HP must equal the sum of its part HP` 예외가 발생한다.

GROK의 early-return은 데이터 로드를 즉시 복구하는 안전한 긴급 차단이었지만 최종 구현으로는 승인하지 않았다. multipart/warship에서 진행 순서 기준 보스 HP·페이즈 사격 수치 보정을 모두 건너뛰어, REQ-072의 “난이도는 진행 순서, 테마 자산은 셔플된 테마” 계약을 깨기 때문이다.

따라서 early-return을 제거하고 multipart 파츠 HP를 진행 위치의 목표 `BossMaxHp`에 맞춰 정수 비율로 재배분하는 방식으로 인수·재구현했다.

## 재현 및 확인

`ProgressionBossDifficultyTests.ShuffledWarshipScalesPartsToProgressionHpWithoutBreakingInvariant`가 실제 RunManager 테마 셔플 경로를 구성한다.

- 진행 위치: biome 2
- 셔플 테마: theme 3 warship
- 테마 원본: 총 HP 100, 파츠 20/30/50
- biome 2 진행도 참조: 총 HP 50
- GROK early-return 상태의 최초 실행: **FAIL**, 실제 총 HP 100 (진행도 보정 누락)
- early-return 이전 로직이라면 총 HP 50 + 파츠 합 100으로 `StagePlan.ValidateParts` 예외
- 재구현 후: **PASS**, 총 HP 50, 파츠 10/15/25, 합 50

REQ-111 실데이터의 `boss_fortress`도 동일한 구조다. 원래 자리인 St3에서는 `ApplyProgressionBossDifficulty`가 호출되지 않아 GROK 원본 HP **19600**과 파츠 구성은 그대로 유지된다. fortress가 St2/St4로 셔플된 경우에만 그 진행 위치의 참조 총 HP로 파츠가 비례 조정된다.

## 재구현 설계

`ScaleProgressionBossParts`는 다음 계약을 지킨다.

1. 파츠가 없으면 기존 일반 보스 경로를 그대로 사용한다.
2. 각 파츠에 최소 HP 1을 먼저 보장한다.
3. 나머지 HP를 `(sourcePartHp - 1)` 가중치로 선언 순서 누적 정수 배분한다.
4. 마지막 파츠까지의 누적 몫으로 반올림 잔여를 흡수해 파츠 합을 목표 `BossMaxHp`와 정확히 일치시킨다.
5. 파츠 ID, 위치, 히트박스, 코어/게이트, 공격 프로필, 재생 틱, warship 그룹 정의는 그대로 보존한다.

부동소수점·난수·컬렉션 순회 순서 의존이 없으며 `long` 중간 곱셈만 사용한다. 목표 HP가 파츠 수보다 작은, 표현 불가능한 입력은 명시적 `InvalidOperationException`으로 거부한다.

warship 파츠 공격 프로필은 테마 전용 패턴이므로 다른 단일 보스 페이즈와 임의 매핑하지 않았다. source에 `BossPhases`가 있는 multipart 보스는 기존과 동일하게 진행 위치 참조의 사격 수치를 적용한다.

## 회귀 검증

| 경로 | 테스트 | 결과 |
|---|---|---|
| warship 셔플 | `ShuffledWarshipScalesPartsToProgressionHpWithoutBreakingInvariant` | PASS — 100 → 50, 파츠 10/15/25 |
| 일반 보스 셔플 | `ShuffledRegularBossStillUsesProgressionCombatValuesAndThemePattern` | PASS — HP/사격은 biome 2, 이동 패턴은 theme 3 유지 |
| 일반 보스 난이도 배율 | `RewardsAndShipConstructor_ReducesMultiplierAndScalesBossHp` | PASS — 11 × 3/2 = 17 |
| 미니보스 난이도 배율 | `DifficultyMultiplierAlsoScalesGeneratedMiniBossHp` | PASS — base 11, global 3/2와 Elite 3/2 적용 후 26 |

## 전체 검증

- `cd Tools/CoreStandalone && dotnet test --no-restore`: **532/532 PASS**
- `dotnet run --no-restore --project Tools/DeterminismAudit/DeterminismAudit.csproj -- --suite`: **AUDIT PASS**
  - 6/6 full-observable scenarios PASS
  - cap-boundary 256/256 PASS
  - 대표 해시: seed-0 `EAE8157691E80783`, seed-12345 `92238A769E5026B6`, seed-7-hidden `5B0DDD764FC79904`
- 금지 API 스캔: 실행 코드 매치 없음 (`Rng.cs` 주석의 금지 문구만 매치)

## 변경 파일

- `Assets/Scripts/Core/Simulation/RunManager.cs`
- `Assets/Tests/EditMode/ProgressionBossDifficultyTests.cs` + `.meta`
- `Assets/Tests/EditMode/DifficultyMultiplierTests.cs`
- `Reviews/from-codex/req112-review.md`

커밋은 요청대로 생성하지 않았다. 오케스트레이터가 검수 후 커밋한다.
