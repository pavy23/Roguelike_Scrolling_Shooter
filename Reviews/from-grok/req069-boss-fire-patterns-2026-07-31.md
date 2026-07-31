# REQ-069 — 보스별 발사 패턴 조합 (2026-07-31)

**상태:** content 수치 반영 완료 · 전부 잠정(§7)  
**검증:** `dotnet test` **378/378** · BalanceSim **all green** · REQ-060 **CLEAR** · 시드 42 해시 2회 일치

## 배경

CODEX REQ-066이 `BossFirePattern` 어휘를 연결했다. 기존 `spread`/`rapid` 문자열은
`aimed`로 하위호환 매핑되므로, 데이터가 안 바뀌면 모든 보스가 조준탄이었다.

## 보스 한 문장 성격

| id | 테마 | 성격 |
|---|---|---|
| **boss_stage1** | scrapyard | 조준탄만으로 페이즈를 읽는 **튜토리얼 보스** (REQ-068 완화 유지) |
| **boss_hive** | hive | 링 → 소용돌이 → 고속 링 — **생체 촉수 전방위 압박** |
| **boss_fortress** | fortress | 봉쇄 벽 → 예고 일제사 → 고속 벽 — **요새 봉쇄·포격** |
| **boss_storm** | nebula | 소용돌이 밀도 상승 후 추적 조준 — **네뷸라 소용돌이** |
| **boss_core** | core | 링 → 벽 → 예고 일제사 — **최종 종합 시험** |

## 페이즈 × 패턴 × 초당 탄수

초당 탄수 = `ways × 60 / fireIntervalTicks` (요청 발수 기준; wall은 실제 발사 ways−1,  
burst 실효 주기는 `telegraphTicks + fireIntervalTicks`로 더 낮음).

| Boss | P | pattern | ways | int | spd | bps | threat | tel |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| stage1 | 0 | **aimed** | 3 | 64 | 9.0 | **2.81** | 0.422 | — |
| stage1 | 1 | **aimed** | 4 | 56 | 8.0 | **4.29** | 0.571 | — |
| stage1 | 2 | **aimed** | 2 | 30 | 13.5 | **4.00** | 0.900 | — |
| hive | 0 | **radial** | 3 | 50 | 9.5 | **3.60** | 0.570 | — |
| hive | 1 | **spiral** | 7 | 42 | 8.5 | **10.00** | 1.417 | — |
| hive | 2 | **radial** | 3 | 16 | 14.5 | **11.25** | 2.719 | — |
| fortress | 0 | **wall** | 4 | 46 | 10.0 | **5.22** (실효 3) | 0.870 | — |
| fortress | 1 | **burst** | 8 | 40 | 9.0 | **12.00** (실효 주기 64t) | 1.800 | 24 |
| fortress | 2 | **wall** | 5 | 14 | 15.5 | **21.43** (실효 4) | 5.536 | — |
| storm | 0 | **spiral** | 4 | 42 | 10.5 | **5.71** | 1.000 | — |
| storm | 1 | **spiral** | 8 | 36 | 9.5 | **13.33** | 2.111 | — |
| storm | 2 | **aimed** | 3 | 12 | 16.5 | **15.00** | 4.125 | — |
| core | 0 | **radial** | 4 | 40 | 11.0 | **6.00** | 1.100 | — |
| core | 1 | **wall** | 7 | 34 | 10.5 | **12.35** (실효 6) | 2.162 | — |
| core | 2 | **burst** | 3 | 12 | 17.0 | **15.00** (실효 주기 30t) | 4.250 | 18 |

### 설계 메모

1. **스테이지1** — 전 페이즈 `aimed`. 수치·threat는 REQ-068 그대로 (학습 구간).
2. **wall은 스테이지3(fortress)부터** — hive에는 wall 없음.
3. **페이즈 전환 = 패턴 전환**  
   - hive: radial → spiral → radial  
   - fortress: wall → burst → wall  
   - core: radial → wall → burst  
   - storm: spiral 밀도 상승 후 aimed 추격 (동일 spiral도 ways 4→8로 시각 변화).
4. **위협 모노톤 / ways·speed 사다리** — BalanceSim 게이트 유지 (HP·TTK 미변경).
5. **탄 예산** — densest(core p1 wall) boss-only sim peak≈36, gen peak≈51 ≪ MaxEnemyBullets=128.  
   capacity overflow는 일상 밀도에서 발생하지 않음.

## 중간보스 (선택 갱신)

REQ-061 성격 유지. 파서(`ParseBossPhase`)가 midBoss.phases.pattern을 읽음.

| id | 변경 | 유지 성격 |
|---|---|---|
| mini_horror p1 | **radial** 7-way | 산탄 살포 → 전방위 링 |
| mini_crystal p2 | **spiral** 5-way | 위치 전환 후 프리즘 소용돌이 |
| mini_core p0/p1 | **radial** → **burst** | 종합: 링 개막 → 예고 집중포 → 돌진 조준 |
| mini_destroyer / mini_walker | 미변경 (aimed) | 돌진 / 정지 집중 유지 |

## 변경 파일

- `GameData/waves.json` — 표준 보스 5종 phases.pattern (+ burst telegraphTicks)
- `GameData/enemies.json` — mini_horror / mini_crystal / mini_core 일부 pattern
- `Tools/BalanceSim/Program.cs` — 보스 로그에 실제 FirePattern·bps 출력

## CLAUDE 요청

1. [ ] `Assets/Resources/GameData/waves.json` · `enemies.json` 동기화

## 검증 명령

```text
cd Tools\CoreStandalone && dotnet test
# 378/378

cd Tools\BalanceSim && dotnet run --project _req060_simcore.csproj -c Release
# PASS: BalanceSim all checks green · REQ-060 CLEAR

dotnet run --project Tools/DeterminismAudit -- 42 1 3000
# hash=22BC49D9D8F52FFD ×2 MATCH
```
