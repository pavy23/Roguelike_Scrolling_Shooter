# 전함 등장 연출 — originX 오프스크린 + scrollSpeed 가속

**날짜:** 2026-08-05  
**담당:** GROK (content only)  
**원인 보고:** 사람 — "전함 등장할때 갑자기 툭 등장해서 많이 어색하네"

## 원인

| 항목 | 값 |
|---|---|
| 함체 스프라이트 | `warship_hull.png` 544px / PPU 16 → **폭 34 u** (중심 ±17) |
| 구 `originX` | **18.0** |
| 등장 순간 함체 점유 | x ∈ **[1, 35]** — 화면(−20~+20) 안쪽이 이미 절반 넘게 들어와 "툭" |

## 변경 (`GameData/waves.json` → `boss_fortress.warship`)

| 필드 | 구 | 신 | 비고 |
|---|---|---|---|
| `originX` | 18.0 | **38.0** | 왼쪽 끝 ≥ 화면 우측 밖 |
| `scrollSpeedPerSecond` | 1.5 | **3.0** | 정박 시간 8~11s 유지 |
| `warningTicks` | 180 | 180 | 불변 |
| `groups[1].anchorTravelTicks` | 240 | 240 | 불변 |
| boss `holdX` | 12.0 | 12.0 | 불변 (warship HoldX 상속) |

## 계산 (holdX = **12.0**, halfWidth = **17.0**)

사람 초안은 holdX≈9로 잡았으나 실데이터 `boss_fortress.holdX` 는 **12.0**.

### (a) 등장 순간 함체 왼쪽 끝 x

```
originX − halfWidth = 38.0 − 17.0 = 21.0
```

화면 우측 경계 = **20** → **1 u 바깥**에서 시작. (`originX ≥ 37` 조건 충족)

### (b) 정박(holdX)까지 걸리는 초

```
(originX − holdX) / scrollSpeedPerSecond
= (38.0 − 12.0) / 3.0
= 26 / 3
≈ 8.67 s
```

목표 밴드 **8~11 s** 안. (경고 3 s 동안에도 스크롤은 진행 — Core `AdvanceScrollToHold`는 `_activeGroupIndex < 0` 이어도 동작)

### 게이트 활성화 시점 (tick = 180 = 3 s)

```
WorldX ≈ 38 − 3.0×3 = 29.0
turret_a (ox +4) ≈ 33  → 아직 플레이필드(+20) 밖 (의도)
```

정박 시각 ≈ **520틱 (8.67 s)**.

## 2·3막 정지 위치

스크롤 속도만 올렸고 holdX·파츠 offset·MaximumVisibleAttritionScrollOffset 조건은 그대로 → **서는 자리는 좌표 기준 불변**, 도달 시간만 단축.

## 검증

```
cd Tools\CoreStandalone && dotnet test
```

| 결과 | 수 |
|---|---|
| 통과 | **584** |
| 실패 | **2** |
| 전체 | 586 |

| 테스트 | 결과 |
|---|---|
| Req117WarshipIntegrationTests | **PASS** |
| Req176WarshipMovingFireTests | **PASS** |
| Req177WarshipPhase2BurstTests | **PASS** |
| Req118 …PlayfieldForEntireEncounter | **FAIL** — tick&lt;240 후 HoldX 고정 가정 (4 s 정박) |
| Req119 …DamagesSternSoonAfterGateActivation | **FAIL** — 게이트 후 240틱 내 피격 강제 |

Req118/119는 **구 4초 정박(origin 18·speed 1.5 또는 origin 24·speed 3)** 에 묶인 하드코딩. 데이터 쪽이 사람 의도(오프스크린 8~11 s 등장)에 맞고, 테스트 갱신은 **CODEX** (`Reviews/from-grok/requests.md` 2026-08-05 절).

## 커밋

지시대로 **미커밋**.
