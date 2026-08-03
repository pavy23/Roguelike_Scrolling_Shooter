# REQ-144 — 하이브 최종 페이즈 기뢰 도달 시간 계산 근거

## Core 적용 방식 (BattleSim.UpdateMineProjectile)

1. `mineTravelTicks` 동안 `bulletSpeed`로 이동한 뒤 정지.
2. `mineTelegraphTicks` 동안 정지(예고).
3. 예고 종료 틱에 플레이어 방향으로 **한 번만** 가속 벡터를 고정 (`SetBulletAcceleration`).
4. 이후 매 틱 `v += a` (분모 공유). 위치는 `AdvanceBullets`에서 `pos += v/denom`.
5. 정지 상태에서 가속 시작이므로 **k틱 후 이동거리** = `a_sub * k*(k+1)/2`.

단위 변환 (`GameDataParser.ToPerTickAcceleration`):
- `a_sub` (서브유닛/틱²) = `mineAcceleration * 256 / 60²`
- `SubUnitsPerWorldUnit=256`, `TicksPerSecond=60`

## 기존 최종 페이즈 (accel=2800, tele=16)

| 잔여거리 | 비행 틱 | 초 | 최고속(wu/s) |
|---|---|---|---|
| 14 | 6 | **0.100s** | 280 |
| 16 | 6 | **0.100s** | 280 |

예고 후 0.1초 만에 도달 → 반응 불가. 중간 페이즈(2400)도 비행은 ~0.1s이나 예고 24틱으로 회피 윈도우가 조금 더 있음.

## 변경 후 (accel=36, tele=24, travel=16, speed=10, ways=7)

| 잔여거리 | 비행 틱 | 초 | 최고속(wu/s) |
|---|---|---|---|
| 12 | 49 | **0.817s** | 29.4 |
| 14 | 53 | **0.883s** | 31.8 |
| 16 | 57 | **0.950s** | 34.2 |

- 일반 교전 거리(보홀드 X=14, 플레이어 X≈0~-8, travel 후 잔여 ~12–20)에서 **예고 종료 후 ≥0.8초**.
- 최고속 ~30–38 wu/s ≪ 중간 페이즈 피크(~240 wu/s).
- 밀도 보완: ways 5→7 (발사건격 10 유지) → "양팔 대량 발사" 인상 유지.
- mineAcceleration 36 → per-tick 64/25 서브유닛/틱² (1/256 격자 exact).

## 검증
`cd Tools\CoreStandalone && dotnet test` → 571/571 PASS
