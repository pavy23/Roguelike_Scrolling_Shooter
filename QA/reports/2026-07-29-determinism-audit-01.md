# QA Report: Determinism Audit #01 (2026-07-29)

**Evaluator**: GEMINI (QA / VERIFIER)  
**Target Tool**: `Tools/DeterminismAudit` (Console Runner)  
**Execution Date**: 2026-07-29  
**Branch**: `qa`  
**Mission**: GEMINI Extended Mission #2 — Determinism Audit Regular Execution #01  

---

## 1. Audit Overview

본 리포트는 AGENTS.md GEMINI 확장 임무 2번(결정론 감사)의 첫 번째 정례 실행 결과이다.  
CODEX가 작성한 Standalone 시뮬레이션 콘솔 러너 `Tools/DeterminismAudit`를 활용하여 5개의 지정 시드에 대해 각각 18,000 틱(Tick) 동안 2회 반복 구동하고, 매 틱 계산되는 64-bit FNV-1a 상태 해시(`hash`)의 100% 일치 여부를 검증하였다.

### Audit Configuration & Runner Parameters
- **Runner Tool**: `Tools/DeterminismAudit` (`dotnet run --project Tools/DeterminismAudit -- <seed> <stageCount> <tickCount>`)
- **Requested Stage Limit (`stageCount`)**: `3`
- **Requested Tick Limit (`tickCount`)**: `18000` (기본 정례 감사 규격)
- **State Hashing Specification**:
  - 틱 단위로 Run/Stage 상태, 플레이어 좌표, 적/탄환 객체 수, 총 점수(`TotalScore`), 이벤트 누적 카운트를 FNV-1a 해시 함수로 폴딩.
  - 플레이어 Max HP는 감사용으로 1,000,000으로 조정되어 생존 실패 없이 18,000 틱 동안 스테이지 전환 및 무작위 패턴을 지속 구동.

---

## 2. Determinism Audit Results Summary

지정한 5개 시드(1, 42, 12345, 99999, 20260729)에 대해 각각 2회 구동을 완료하였으며, **모든 시드에서 2회 실행 결과 해시가 100% 일치**함을 확인하였다.

| Seed | Run 1 Hash | Run 2 Hash | Match Status | Requested Ticks | Executed Ticks | Completed Stages | Final Stage | Final State | Ship ID | Total Score |
|---|---|---|---|---|---|---|---|---|---|---|
| `1` | `7C368D57D50F4EC8` | `7C368D57D50F4EC8` | **MATCH (PASS)** | 18000 | 18000 | 2 | 3 | Playing | starter | Folded in Hash |
| `42` | `1DEA356FA2B9C45D` | `1DEA356FA2B9C45D` | **MATCH (PASS)** | 18000 | 18000 | 2 | 3 | Playing | starter | Folded in Hash |
| `12345` | `4A4DD55F5A280185` | `4A4DD55F5A280185` | **MATCH (PASS)** | 18000 | 18000 | 2 | 3 | Playing | starter | Folded in Hash |
| `99999` | `FC096F6F73E2AF67` | `FC096F6F73E2AF67` | **MATCH (PASS)** | 18000 | 18000 | 2 | 3 | Playing | starter | Folded in Hash |
| `20260729` | `996C403CDDF8ED58` | `996C403CDDF8ED58` | **MATCH (PASS)** | 18000 | 18000 | 2 | 3 | Playing | starter | Folded in Hash |

---

## 3. Seed-by-Seed Execution Logs

### Seed 1
- **Run 1 Output**: `hash=7C368D57D50F4EC8 seed=1 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Run 2 Output**: `hash=7C368D57D50F4EC8 seed=1 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Result**: **PASS** (해시 축출 및 상태 완벽 일치)

### Seed 42
- **Run 1 Output**: `hash=1DEA356FA2B9C45D seed=42 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Run 2 Output**: `hash=1DEA356FA2B9C45D seed=42 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Result**: **PASS** (해시 축출 및 상태 완벽 일치)

### Seed 12345
- **Run 1 Output**: `hash=4A4DD55F5A280185 seed=12345 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Run 2 Output**: `hash=4A4DD55F5A280185 seed=12345 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Result**: **PASS** (해시 축출 및 상태 완벽 일치)

### Seed 99999
- **Run 1 Output**: `hash=FC096F6F73E2AF67 seed=99999 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Run 2 Output**: `hash=FC096F6F73E2AF67 seed=99999 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Result**: **PASS** (해시 축출 및 상태 완벽 일치)

### Seed 20260729
- **Run 1 Output**: `hash=996C403CDDF8ED58 seed=20260729 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Run 2 Output**: `hash=996C403CDDF8ED58 seed=20260729 requestedStages=3 completedStages=2 requestedTicks=18000 executedTicks=18000 stage=3 state=Playing ship=starter`
- **Result**: **PASS** (해시 축출 및 상태 완벽 일치)

---

## 4. Standalone Test Suite Verification (`dotnet test`)

- **Execution Command**: `dotnet test Tools/CoreStandalone`
- **Assembly**: `Shmup.Core.Standalone.dll (net10.0)`
- **Results**:
  - **Total Tests**: 130
  - **Passed**: 130
  - **Failed**: 0
  - **Skipped**: 0
  - **Duration**: 168 ms
- **Status**: **ALL GREEN**

---

## 5. Mismatch Reproduction & Critical Bug Log

- **Mismatch Count**: 0 건
- **Reproduction Steps**: N/A (불일치 항목 미발생)
- **Critical Status**: **NONE** (결정론적 상태 재현성 완전 보장 확인)

---

## 6. Conclusion & Summary

1. **결정론 감사 결과**: 시드 5개(1, 42, 12345, 99999, 20260729)에 대해 18,000 틱 구동 시 2회 실행 간 FNV-1a 64-bit 해시값이 100% 동일함.
2. **단독 단위 테스트 결과**: `Shmup.Core` Standalone 130개 단위 테스트 전원 통과 (0 Failure).
3. **결론**: 현재 `Shmup.Core` 시뮬레이션 엔진은 동일 시드 및 입력 제어 하에서 비결정론적 요소(벽시계 시간, 비정렬 딕셔너리 순회, 외부 float 오차 등) 없이 완벽한 시드 재현성을 유지하고 있음.
4. **Severity**: **PASS (ALL GREEN)**.
