# 2026-08-05 장애물 half 0.75 데이터 기입 — GROK 보고

사람 지시: 스테이지 2~히든 공통 solid / breakable / laserEmitter 1.5× (half 0.75).  
**커밋 없음** (지시).

## 적용

| theme | solid | breakable | laserEmitter | 합계 |
|---|---:|---:|---:|---:|
| hive | 29 | 28 | 0 | **57** |
| fortress | 99 | 29 | 20 | **148** |
| nebula | 38 | 29 | 0 | **67** |
| core | 38 | 36 | 6 | **80** |
| abyss | 17 | 17 | 0 | **34** |
| brood | 10 | 17 | 0 | **27** |
| **합계** | **231** | **156** | **26** | **413** |

각 항목에:

```json
"halfWidth": 0.75,
"halfHeight": 0.75
```

(기본 0.5의 1.5배. 필드 생략 시 전역 기본 0.5 유지.)

## scrapyard 미터치 확인

| 항목 | 값 |
|---|---|
| scrapyard 장애물 총수 | 77 (solid 21 + breakable 56) |
| scrapyard 중 halfWidth/halfHeight 기입 | **0** |
| 샘플 `seg_turret_floor_scrapyard` solid/breakable | type/x/y/hp 만 (half 없음) |

## 그대로 둔 것

- `hive_tentacle` halfWidth=1.171875 / halfHeight=2.34375 (이미 1.5×)
- scrapyard 전 세그먼트
- laser 빔 굵기 (`thinHalfWidth` / `fullHalfWidth`) — 포탑 본체 크기만 변경

## corridor 결과

BalanceSim corridor 검사가 **전역 halfH=0.5만** 보고 있어, per-obstacle `HalfHeight`를 쓰도록
`Tools/BalanceSim/Program.cs`를 수정한 뒤 재실행 (GROK 소유: 밸런스 시뮬 스크립트).

```
default halfH=128su (0.50u) (per-obstacle HalfHeight overrides when >0)
minCorridorGap=256su (1.0u)
corridor=ok : 102 / 102
FAIL obstacles: 0
PASS: obstacle layout / corridor / stage-1 empty checks.
```

- **y 배치 조정 세그먼트: 없음** (크기를 되돌리지 않았고, gap 부족 칼럼도 없었음)
- 사전 계산의 fortress tight 후보는 같은 x 칼럼이 아니거나 여유가 있어 실측 최소 gap ≫ 1.0u
- 기존 FAIL 8건 유지 (이번 범위 외: shuffle WALL, 103b hive dig path, scoring 등)

## 검증

```
cd Tools\CoreStandalone && dotnet test
→ 통과! 실패 0, 통과 586, 전체 586

cd Tools\BalanceSim && dotnet run -c Release
→ PASS: obstacle layout / corridor / stage-1 empty checks.
→ corridor 전수 ok (102)
→ FAIL: 8 check failure(s)  ← 기존과 동일 계열
```

## 변경 파일 (미커밋)

1. `GameData/waves.json` — 비-scrapyard 6테마 장애물 413개 half 기입
2. `Tools/BalanceSim/Program.cs` — corridor 검사 per-obstacle HalfHeight 반영

## 후속

- CLAUDE: Resources 동기화 + 연출 스케일 (스키마 연동 시 그림 자동 배율 — 확인)
- GEMINI: DeterminismAudit 베이스라인 (데이터 변경 후)
- 커밋은 사람/오케스트레이터 지시 대기
