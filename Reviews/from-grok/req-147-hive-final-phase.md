# REQ-147 — 하이브 최종 페이즈 회피 가능 재조정

## 어느 탄이 문제인가 (Core 경로)

최종 페이즈(`hpThreshold: 0.333`)는 매 사격 틱에 **두 경로**가 동시에 돈다.

| 경로 | Core | 체감 |
|---|---|---|
| **기뢰** (`projectileKind: mine`) | `FireRadialBossVolley` → travel → telegraph → `UpdateMineProjectile` 가속 + brood 시 `TurnEnemyProjectileTowardPlayer` | 예고 후 가속·추적. **“너무 빨라 못 피한다”의 주범** |
| **브루드 스폰** (`signaturePatternId: brood` → `hive_tentacle`) | `FireBossSignature` → `SpawnBossEnemy` 후 졸개 `fireIntervalTicks:100` 조준탄 | 기본 적탄 속도 **8 wu/s** (고정). 느린 조준탄이지만 스폰 밀도가 높으면 화면이 찬다 |

결론: **속도를 손댈 대상은 기뢰 쪽**. 텐타클 조준탄 자체는 8 wu/s로 이미 느리다. 다만 사격 간격이 짧으면 텐타클이 과다 누적되므로 간격 완화로 같이 잡는다.

## 목표

사람 원문 “지금보다 4배 이상 느려야” → REQ-144 도달 0.82s × 4 ≈ **3.28s** → **≥3.2s** (예고 종료 후, 잔여거리 12 wu 기준).

`a_sub = mineAcceleration * 256 / 60²`, 이동거리 = `a_sub * k(k+1)/2`.

## 변경 (최종 페이즈만)

| 필드 | REQ-144 후 | REQ-147 | 이유 |
|---|---|---|---|
| `mineAcceleration` | 36 | **2** | d=12 도달 **3.47s** (≥3.2s). exact `32/225` 서브/틱² |
| `signatureHomingTurnLutSlotsPerTick` | 2 | **1** | 가속 중 추적을 중간 페이즈 수준으로. 느린 가속+강한 호밍이면 여전히 락온 |
| `fireIntervalTicks` | 10 | **24** | 수명 길어지면 fi=10 시 동시 기뢰 ~174 > cap 128. 동시 밀도 ~구(62) 유지 |
| `ways` | 7 | **9** | 간격 완화 분 밀도·패턴 보완 |
| `mineTelegraphTicks` | 24 | **28** | 예고 가독성 +0.067s |

중간 페이즈(`mineAcceleration: 2400`)는 손대지 않음.

## 도달 시간표 (예고 종료 후)

| accel | d=12 | d=14 | d=16 | 최고속(d=12) |
|---|---|---|---|---|
| 36 (이전) | 0.82s | 0.88s | 0.95s | ~29 wu/s |
| **2 (이번)** | **3.47s** | **3.73s** | **4.00s** | ~6.9 wu/s |

동시 기뢰 추정: lifetime≈16+28+208=252t, fi=24, ways=9 → ≈**95**발 (<128 cap).

텐타클 스폰: 6/s → **2.5/s** (fi 24). 조준탄 속도는 그대로 8 wu/s.

## 검증
`cd Tools\CoreStandalone && dotnet test`
