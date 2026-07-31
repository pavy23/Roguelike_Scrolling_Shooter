# REQ-061 — 중간보스 행동 패턴 데이터 (2026-07-30)

## 범위

- **원본:** `GameData/enemies.json` 의 `mini_*` 항목 `midBoss` 필드
- **스키마 위치 정정:** 사용자 브리프의 `waves.json`이 아니라 **`enemies.json` EnemyDto.midBoss**  
  (`MidBossProfileDto` / `ParseMidBossProfile` — CODEX REQ-058)
- HP·contactDamage·드롭 가중은 REQ-060 값 유지 (스테이지1 클리어 게이트 보존)

## 테마별 중간보스 표

| id | 홈 테마 | HP | 성격 | 순환 (duration / telegraph) | 요구 플레이 |
|---|---|---:|---|---|---|
| **mini_destroyer** | scrapyard | 1100 | **돌진형** — 정지 조준 → 예고 후 고속 연사 돌진 탄막 → 소폭 횡이동 회복 | P0 3.5s / 0s · P1 **4.0s / 0.60s** · P2 3.0s / 0s | P1 예고(0.6s) 보고 **레인 이탈**. 탄속 13.5·3way·20t — 직진 회피보다 **상하 자리 이동** |
| **mini_horror** | hive | 800 | **산탄 살포형** — 대진폭 드리프트 3way → 정지 7way 샷건 → 광폭 sine 1way | P0 4.0s / 0s · P1 **3.5s / 0.50s** · P2 3.0s / 0s | P1 정지 샷건 전 **화면 가장자리로 도피**. 탄속은 7.5로 느려 밀도만 위험 |
| **mini_walker** | fortress | 1600 | **정지 집중 사격형** — 락온 1way → 예고 후 3way 포격 → 재장전 1way | P0 3.5s / 0s · P1 **4.0s / 0.70s** · P2 3.0s / 0s | **전 구간 Stationary.** P1 0.7s 예고 후 16t/12u 포격 — 수직 슬라럼으로 조준축 이탈 |
| **mini_crystal** | nebula | 1400 | **위치 전환형** — 정지 홀드 → 대진폭·단주기 sine(워프감) → 프리즘 5way | P0 3.0s / 0s · P1 **4.5s / 0.50s** · P2 **3.5s / 0.60s** | P1 amp 4.5 / period 48t 으로 **Y 점프에 맞춰 추적**. P2 5way는 예고 후 사이 틈 통과 |

### 페이즈 수치 상세

| id | P | durationTicks | telegraphTicks | ways | fireInterval | bulletSpeed | movement |
|---|---:|---:|---:|---:|---:|---:|---|
| destroyer | 0 | 210 | 0 | 1 | 72 | 7.5 | stationary |
| destroyer | 1 | 240 | **36** | 3 | 20 | 13.5 | verticalSine 1.25 / 54t |
| destroyer | 2 | 180 | 0 | 1 | 48 | 8.5 | verticalSine 1.75 / 120t |
| horror | 0 | 240 | 0 | 3 | 58 | 8.0 | verticalSine 3.5 / 120t |
| horror | 1 | 210 | **30** | 7 | 38 | 7.5 | stationary |
| horror | 2 | 180 | 0 | 1 | 64 | 8.0 | verticalSine 4.0 / 96t |
| walker | 0 | 210 | 0 | 1 | 52 | 9.0 | stationary |
| walker | 1 | 240 | **42** | 3 | 16 | 12.0 | stationary |
| walker | 2 | 180 | 0 | 1 | 70 | 8.0 | stationary |
| crystal | 0 | 180 | 0 | 1 | 60 | 8.0 | stationary |
| crystal | 1 | 270 | **30** | 3 | 40 | 9.5 | verticalSine 4.5 / 48t |
| crystal | 2 | 210 | **36** | 5 | 28 | 10.0 | verticalSine 2.0 / 72t |

- 지속 3.0–4.5s (권장 3–6s 밴드)
- 위험 페이즈 telegraph ≥ 0.5s (30t+)
- Boss 이동축은 Y만 (`stationary` / `verticalSine`) — “횡돌진”은 **예고 + 고속 조준탄**으로 연출 (Core 계약 한계)

## core 테마

전용 `mini_core`는 **적 카탈로그 수 31 고정 테스트**(`RepositoryApprovedV2Files_ParseCompletely`)에 걸려 이번 커밋에 넣지 않음.  
core 바이옴은 홈 매칭 3×가 없어 4종 균등(+타 테마 3× 없음)으로 순환 — 네 성격이 모두 등장 가능.

**후속 요청 (CODEX):** `mini_core` 추가 시 `RepositoryApprovedV2Files` enemy count 31→32.  
제안 스펙: HP 1550, `themeId: core`, `stageIndexMin: 3`, phases = hive산탄 → fortress집중(tel 36) → scrapyard돌진(tel 42).

## 선택·난이도

| 항목 | 값 |
|---|---|
| weight | 전원 1 (홈 테마 시 Core가 ×3) |
| stageIndexMin/Max | 1 .. Max (전원 전 구간) |
| contactDamage | 2 유지 (돌진 접촉 데미지 상향 없음 — REQ-060) |
| 스테이지1 mid worst TTK | walker 1600 @ starterEff 90 → **17.8s ≤ 18s** |

## 검증

| 항목 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **360/360 PASS** |
| BalanceSim (로컬 Core) | **all green** · REQ-060 stage1 **CLEAR** hits≈3.16 |
| 결정론 | 시드 0–31 × 2회 해시 일치 |
| mid avgHP | 1225 (REQ-060과 동일) |

```text
cd Tools\CoreStandalone && dotnet test
# BalanceSim: GameData root + 병합 후 Core
```

## 설계 메모

- `movementPattern` / `durationTicks` / `telegraphTicks` 만으로 성격 분리. `partVulnerability` 미사용 (파츠 없는 미니).
- 데이터 없으면 Core `CreateDefaultMidBossPattern` 폴백 — 지금은 **전 mini_* 프로필 채움**.
- `Assets/Resources/GameData` 동기화는 CLAUDE 소유 (이번 커밋 미포함).
