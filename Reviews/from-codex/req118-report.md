# REQ-118 전함 위치/스크롤 동기 보고서

- 담당: CODEX / SIMULATION
- 범위: `Assets/Scripts/Core/`, `Assets/Tests/EditMode/`
- 데이터 변경: 없음 (`GameData/`는 GROK 소유)
- 결론: **PASS** — 무조건 스크롤 결함을 재현했고, 함미/함체/코어별 이동 규칙과 전체 수명 위치 불변식을 Core에 반영했다.

## 1. 실데이터 재현과 원인

`boss_fortress` 실데이터는 `originX=24`, `holdX=12`, `scrollSpeedPerSecond=3`, 60 tick/s, PPU와 무관한 Core 좌표 단위 `256 subunits/world unit`를 사용한다.

수정 전 `WarshipEncounter.BeginTick()`은 경고, 함미, 함체, 코어 구분 없이 매 틱 `AdvanceScroll()`을 호출했다. 따라서 전함 기준 X는 `24 - 0.05 × tick`으로 계속 감소했다. `BattleSim`은 함미/함체에서 이 좌표를 그대로 파츠 충돌 좌표에 사용했고, 코어에서만 별도로 `BossHoldX`를 적용해 `WarshipEncounter.Parts`와 `BattleSim.BossParts` 사이에도 좌표 원본이 둘로 갈렸다.

수정 전 월드 X 시계열은 다음과 같다. 경계 `±20`은 파츠 중심 기준이며, 괄호 안 tick은 처음으로 왼쪽 경계를 벗어나는 tick이다.

| encounter tick | 전함 기준 X | 함미 engine / turret_a,b | turret_c,d | core |
|---:|---:|---:|---:|---:|
| 0 | 24 | 28 | 24 | 18 |
| 180 (함미 활성) | 15 | 19 | 15 | 9 |
| 540 | -3 | 1 | -3 | -9 |
| 760 | -14 | -10 | -14 | -20 |
| 880 | -20 | -16 | -20 | -26 |
| 960 | -24 | -20 | -24 | -30 |
| 961 | -24.046875 | **-20.046875 (이탈)** | -24.046875 | -30.046875 |
| 3000 (50초) | -126 | -122 | -126 | -132 |

- core 원시 좌표 최초 이탈: tick **761**
- turret_c/d 최초 이탈: tick **881**
- 활성 함미 engine 및 turret_a/b 최초 이탈: tick **961**
- 테스터의 약 50초 관측값: 전함 기준 X **-126**, 함미 X **-122**

따라서 build26의 `warship stern 1/3` 고착과 HP 무변화는 함미가 피격 가능 상태인 채 플레이필드 밖으로 계속 이동한 것이 직접 원인이다.

## 2. 수정 사항

### 단일 위치 원본

- `WarshipEncounterDefinition.HoldX`를 public 계약으로 추가했다.
- `GameDataParser`가 소유 보스의 `holdX`를 중첩 전함 정의에 전달한다.
- `WarshipEncounter.WorldX`를 public으로 노출했다.
- `BattleSim`은 별도 코어 좌표 분기 없이 `WarshipEncounter.WorldX`만 사용한다.
- 결정론 해시에 `HoldX`를 포함했다.

### 3막 이동 규칙

| 상태 | 이동 규칙 |
|---|---|
| warning / stern (`MidbossGate`) | `originX`에서 `holdX`까지만 접근하고 정지 |
| hull (`AttritionLine`) | 이 구간에서만 좌측 스크롤. 살아 있고 피격 가능한 활성 파츠 중심이 X=-20을 넘기 전 정지 |
| core (`FinalCore`) | 그룹 활성 시 `holdX`로 고정하고 이후 정지 |

`advanceAfterTicks`는 기존처럼 함체 구간의 강제 전환 상한으로만 사용한다. 함미 또는 코어 대기 시간에는 스크롤이나 자동 전환을 일으키지 않는다.

수정 후 fortress 시계열:

- tick 180: stern X=19, turret_c X=15, core X=9
- tick 240: `holdX=12` 도달, stern X=16, turret_c X=12, core X=6
- tick 3000: 함미를 파괴하지 않아도 위 좌표 그대로 유지
- 함체 elapsed 640: 살아 있는 전방 포탑 `turret_c` X=-20에서 정지
- 함체 elapsed 720: 코어 그룹으로 강제 전환, 전함 X=12 / core X=6에서 정지

## 3. 테스트

신규 `Req118WarshipPositionTests` 2건:

1. `RepositoryFortressWarshipKeepsDamageablePartsInPlayfieldForEntireEncounter`
   - 저장소 실데이터를 파싱한다.
   - warning → 함미 장기 대기 → 함체 720 tick → 코어 장기 대기 → 완료까지 전 수명을 진행한다.
   - 매 tick 모든 `Active && !Invulnerable` 파츠 중심 X가 `[-20,+20]`인지 개별 assert한다.
   - 함미/포탑/코어의 위 시계열과 함체 경계 정지를 함께 assert한다.
2. `RepositoryFortressBattleSimProjectileDamagesHeldSternAfterLegacyEscapeTick`
   - seed 2의 실데이터 fortress StagePlan을 실제 `BattleSim`으로 진행한다.
   - 과거 이탈 tick 961을 훨씬 지난 encounter tick 3000까지 함미를 대기시킨다.
   - 함미 X=16과 피격 가능 상태를 확인한다.
   - `GhostMainShot`을 실제 발사체 목록에 생성하고 다음 `BattleSim.Step`의 발사체/보스 파츠 충돌 경로로 HP가 정확히 1 감소함을 확인한다. `WarshipDamageCommand` 직접 호출 테스트가 아니다.

기존 파서/전함/BattleSim 테스트도 새 public `HoldX` 계약과 단일 좌표 원본에 맞춰 갱신했다. `Assert.Multiple`은 사용하지 않았다.

## 4. GROK 데이터 적합성

현재 `originX=24`, `holdX=12`, `scrollSpeedPerSecond=3.0`, `advanceAfterTicks=720`은 새 규칙과 호환된다.

- 전함은 4초에 holdX에 도달한다. warning 180 tick(3초) 뒤 함미가 X=19에서 활성화되고, 1초 더 접근해 X=16에서 정지한다.
- 함체는 최대 12초 동안만 전진한다. `turret_c/d`가 살아 있으면 elapsed 640에 X=-20에서 정지하고 남은 80 tick을 유지한다.
- 해당 포탑이 파괴됐으면 남은 피격 가능 포탑의 offset에 따라 조금 더 전진할 수 있으나 그 파츠도 X=-20을 넘지 않는다.
- 코어 전환 시 기존 설계대로 holdX에 재고정된다.

따라서 **GROK 수치 재조정 요청은 없다.**

## 5. 검증 결과

- `dotnet test --no-restore --nologo`: **549/549 PASS**
- 금지 심벌 검사: 신규 `Assert.Multiple`, `System.Random`, `UnityEngine.Random`, `Guid.NewGuid`, 벽시계 사용 없음
- `dotnet run --no-restore --project Tools/DeterminismAudit -- --suite`: **AUDIT PASS**
  - 6/6 장기 시나리오 PASS
  - cap-boundary 256 seeds PASS
  - `seed-7-hidden`: `E2D9EFC1C74A3705`, PerfectClear, bossHp 0/62000
- 동일 seed 2 단일 감사 2회:
  - run 1: `691D9313E77FE6EC`
  - run 2: `691D9313E77FE6EC`
  - 결과: **일치**

커밋은 요청대로 생성하지 않았다. 오케스트레이터가 검수 후 커밋한다.
