# 함미 히트박스 코드 교차검증 — build25~30 "무데미지" 5연속 오판의 진짜 원인

- 담당: CLAUDE (RENDERER)
- 발단: `Reviews/from-tester/build30-hud-fade-warship-gate-2026-08-03.md` §다음 조사자 #3
  — "함미 히트박스의 실제 월드 y 범위를 Core에서 확인해 브리핑값 [-2,+2]와 맞는지 보라"
- 결론: **브리핑값이 맞다. 그리고 뷰가 그 값보다 크게 그리고 있었다** — 오판은 테스터
  개인의 실수가 아니라 화면이 거짓말을 하고 있었기 때문이다. 이번에 그 거짓말을 닫았다.

## 1. 판정 범위 — 데이터·코드 양쪽에서 확정

`GameData/waves.json` `boss_fortress` / 파츠 `engine`(= warship 그룹 `stern`):

| 항목 | 값 |
|---|---|
| `offsetY` | 0.0 |
| `halfHeight` | 2.0 |
| `halfWidth` | 2.5 |

보스 중심 Y가 0에 고정되는지가 관건인데, 고정된다:

- `BattleSim.cs:5059` — 보스 스폰 시 `_bossY = 0`, `_bossMovementAnchorY = _bossY`.
- `BattleSim.cs:5776-5790` `ApplyBossPhaseMovement`의 `LegacyHover` 분기 —
  `_bossPartDefinitions.Count > 0 && !legacyVerticalMovementActive`이면
  `_bossY = _bossMovementAnchorY + transitionOffsetY`로 **호버 오프셋을 건너뛴다.**
- `legacyVerticalMovementActive`는 `BattleSim.cs:6104`에서 파츠 공격 타입이
  `VerticalMovement`일 때만 true가 된다. fortress_warship의 파츠 공격은
  radialSpread(engine·core) / aimedSpread(turret ×4)뿐 — **하나도 해당 없다.**

→ 함미 파츠는 `RefreshBossPartPositions`(`:6319-6320`)로 `(bossX+4.0, 0.0)`에 고정,
판정은 `FindBossPartHit`(`:6789-6814`)의 AABB라 **월드 y ∈ [-2.0, +2.0]**. 브리핑값과 일치.

플레이어 탄까지 넣으면: 기본 무기는 `SpawnMainShotFrom`(`:9311-9319`)에서 velocityY=0으로
발사되므로 탄은 발사 시점의 y를 그대로 유지한다. 탄 반높이 `MainShotHalfHeight` = 9/64u
≈ 0.14u. 즉 **기체가 y ∈ 약 [-2.14, +2.14]에 있어야 함미에 맞는다.**

**build25~29의 관측 위치는 y ≈ -10.75였다.** 판정대 아래로 8.6유닛 — 테스터 판정이
뒤집힌 게 맞다. build30 §2-B가 y ≈ -4.9~+3.2로 붙잡자 HP가 연속 감소한 것도
이 수치와 정확히 맞아떨어진다(밴드를 드나들었으니 깎이되 느리게).

## 2. 진짜 문제 — 그림이 판정보다 컸다 (이번 수정 대상)

여기서 멈추면 "테스터가 잘못 섰다"로 끝나는데, **왜 5명이 연속으로 잘못 섰는지**가 남는다.
`WarshipView`는 하드포인트 스프라이트를 **native 크기로** 얹고 있었다 (`localScale` 미조정):

| 파츠 | 재사용 스프라이트 | 그려진 크기 (PPU 16) | 판정 크기 | 어긋남 |
|---|---|---|---|---|
| `engine` (함미) | `anim_boss_fortress_00` 128×96 | 8.0 × **6.0**u | 5.0 × **4.0**u | 세로 **1.5배 과대** |
| `core` (함수) | `anim_boss_core_00` 128×96 | 8.0 × **6.0**u | 4.0 × **4.0**u | 세로 **1.5배 과대** |
| `turret_*` | `obstacle_laser_turret` 32×32 | 2.0 × 2.0u | 2.5 × 2.19u | 판정이 약간 큼(안전한 방향) |

함미 실루엣의 **위아래 각 1유닛은 쏴도 안 맞는 그림**이었다. 보이는 아래쪽 가장자리를
조준하면 명중 이펙트가 전혀 없고, 화면은 "여기 함미인데 데미지가 안 들어간다"고 말한다.
테스터가 본 것이 정확히 그것이다. 사람 플레이어도 똑같이 속는다.

`BossPartsView`(전함 외 멀티파트 보스)도 같은 계열의 문제가 있었다 — 피격 플래시·파괴
그을림 오버레이가 파츠와 무관하게 **전부 3.5×3.5 고정**이었다.

## 3. 수정

Core는 건드리지 않았다. 크기 정보는 이미 `StagePlan.BossParts`(`BossPartDefinition`의
`HalfWidth`/`HalfHeight`)에 있고 Presentation에서 읽을 수 있다 — Core 요청 불필요.

- `BattleDirector.BossPartDefinitions` 신설 — `StagePlan.BossParts` 읽기 전용 통과.
  (`BossParts`는 좌표·HP·무적만 주고 크기를 안 준다.)
- `WarshipView.FitHardpointToHitbox` — 하드포인트 `localScale`을 판정 크기에 맞춘다.
  종횡비는 **일부러 유지하지 않는다**: 판정이 축마다 독립인 AABB라 종횡비를 지키면
  한 축이 다시 어긋난다.
- `BossPartsView` — 오버레이 `size`를 파츠 정의에서 매 프레임 산출.

정의를 못 찾으면 둘 다 기존 동작(native 크기)으로 폴백한다.

## 4. 검증

- Unity 배치모드 컴파일: `error CS` **0건** (`compile31.log`)
- `Tools\CoreStandalone` `dotnet test`: **554/554 통과**
- Unity EditMode 배치모드: **547/547 통과** (`results31.xml`)

## 5. 다음 조사자에게

1. 이 수정 이후 빌드에서 함미를 다시 재보라. 그림 = 판정이 된 지금은 **실루엣 어디를
   쏴도 명중 플래시가 떠야 한다.** 안 뜨는 지점이 남아 있으면 그건 진짜 데미지 라우팅
   버그다 — 이번 수정이 그 판별을 가능하게 만든 것이 핵심 성과다.
2. build30 §다음조사자 #1(폐루프 컨트롤러 5분 이상 완주 관측)은 여전히 미해결이다.
   함미 완파 → 포탑 분기(`way`) → 함체 → 코어까지가 6빌드 연속 미도달.
3. 함미가 세로로 작아 보이게 됐다. 전함의 위압감이 줄었다고 판단되면 **판정을 키우는
   방향**(GROK, `waves.json` `engine.halfHeight`)이 맞다 — 그림을 다시 키우면 같은
   거짓말로 돌아간다.
