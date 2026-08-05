# 2026-08-05 스테이지2+ 장애물·레이저포·하이브 촉수 1.5× — GROK 보고

사람 지시: 스테이지 2~히든 공통 크기 1.5×. **커밋 없음** (지시).

## 범위 판정

| 테마 | 스테이지 대역 | 이번 대상 |
|---|---|---|
| scrapyard | St1 입문 | **제외** (지시) |
| hive | St2 | 포함 |
| fortress | St3 | 포함 |
| nebula | St4 | 포함 |
| core | St5 | 포함 |
| abyss / brood | 히든 | 포함 |

세그먼트 판별: `theme` ∈ 위 포함 목록 (difficultyMin 2+ 세그먼트와 정합). scrapyard solid 21·breakable 56은 손대지 않음.

## id / 에셋 확인

| 사람 표현 | 실제 id / 에셋 | 크기 필드 위치 |
|---|---|---|
| 부딪히는 장애물 | `obstacles[].type=solid` | **없음** — Core `ObstacleHalf*=0.5u` 전역 |
| 레이저포 | `obstacles[].type=laserEmitter` + `obstacle_laser_turret.png` | 본체 크기 **없음** (레이저 빔 `thinHalfWidth`/`fullHalfWidth`만 있음 — 빔 굵기, 포탑 아님) |
| 보라 촉수 | `enemies.json` **`hive_tentacle`** (avgRGB 마젠타, `enemy_hive_tentacle.png`) | `halfWidth`/`halfHeight` ✅ |
| 초록 포대 | hive **breakable** + `obstacle_spore_pillar.png` (avgRGB 초록) — 별도 enemy id 없음 | 장애물 크기 필드 **없음** |

대상 테마 장애물 개수 (변경 예정, Core 대기):

| theme | solid | breakable | laserEmitter |
|---|---:|---:|---:|
| hive | 29 | 28 (초록 포대) | 0 |
| fortress | 99 | 29 | 20 |
| nebula | 38 | 29 | 0 |
| core | 38 | 36 | 6 |
| abyss | 17 | 17 | 0 |
| brood | 10 | 17 | 0 |

## GROK 적용분

### 1. `hive_tentacle` ×1.5 (완료)

| 필드 | before | after |
|---|---:|---:|
| halfWidth | 0.78125 | **1.171875** |
| halfHeight | 1.5625 | **2.34375** |

- 1/256 양자화 정확 (300/256, 600/256).
- 사용처: hive 세그먼트 48스폰 + brood 24스폰 (히든 포함 — 지시 범위).
- scrapyard/turret_ground·ceiling은 **공유 적**이라 스테이지1에도 나와 크기 변경 안 함.

### 2. solid / laserEmitter / hive breakable (차단)

```
BattleSimConfig.ObstacleHalfWidth  = SubUnitsPerWorldUnit / 2  // 0.5u
BattleSimConfig.ObstacleHalfHeight = SubUnitsPerWorldUnit / 2  // 0.5u
ObstacleDto: type, x, y, hp, blocksEnemyBullets, regenDelayTicks, laser 만
```

전역 0.5→0.75로 올리면 scrapyard stage1 breakable까지 커져 **지시 위반**.  
per-obstacle half 스키마 없이는 테마 한정 1.5× 불가.

→ `Reviews/from-grok/requests.md` 2026-08-05 절에 CODEX/CLAUDE 요청 기록.  
CODEX MCP usage limit (재시도 ~2026-08-08) — §9-1 CLAUDE 대행 가능.

Core 도착 후 데이터 채울 값:

| 대상 | halfWidth / halfHeight |
|---|---|
| solid (비-scrapyard 6테마) | 0.75 / 0.75 |
| laserEmitter (fortress/core) | 0.75 / 0.75 |
| hive breakable (초록 포대) | 0.75 / 0.75 |
| scrapyard 전부 | 미기입 (기본 0.5) |

### 3. corridor 사전 계산 (halfH 0.75 가정)

Playfield ±11.25u, min gap 1.0u. 대상 테마 multi-solid 칼럼을 halfH=0.75로 재계산 → **FAIL 0**.  
가장 빡센 예: fortress `[-6.5,-4.0]` 칼럼 → inter-gap 정확히 1.0u (경계 ok).  
Core 반영 후에도 배치 벌림이 필요 없을 가능성 큼 — BalanceSim으로 재확인 필수.

## 스테이지1 제외 확인

- scrapyard 세그먼트 obstacles: **수정 0건**
- shared(theme null) intro 풀: **수정 0건**
- `turret_ground` / `turret_ceiling` (scrapyard 포탑): **수정 0건**
- 변경 파일: `GameData/enemies.json` 의 `hive_tentacle` 한 줄 쌍만

## 검증

```
cd Tools\CoreStandalone && dotnet test
→ 통과! 실패 0, 통과 586, 전체 586

cd Tools\BalanceSim && dotnet run -c Release
→ PASS: obstacle layout / corridor / stage-1 empty checks.
→ 전 세그먼트 corridor=ok (FAIL 0)
→ 기존 FAIL 유지 (이번 범위 외): shuffle WALL, 103b hive dig path, scoring 등 — 총 8 check failure(s)
```

## 후속

1. CODEX(또는 CLAUDE 대행): per-obstacle half 스키마 + corridor per-solid half
2. GROK: solid/laser/hive-breakable 0.75 기입 + corridor FAIL 시 y 벌림
3. CLAUDE: Resources 동기화 + ObstacleViewScale/인스턴스 스케일 정합
4. GEMINI: DeterminismAudit 베이스라인
