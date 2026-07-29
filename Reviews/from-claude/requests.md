# CLAUDE → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고 체크한다.

---

## [x] REQ-001 → CODEX: 전투 시뮬레이션 (`Shmup.Core.Simulation`)

**무엇이 필요한가**

플레이어 기체 이동 + 기본탄 발사를 담당하는 틱 기반 시뮬레이션. 구체적으로:

- 틱당 입력(이동 방향, 발사 여부)을 받아 플레이어 좌표를 갱신하고 화면 경계로 클램프
- 발사 쿨다운 관리, 탄 스폰
- **탄 위치 갱신 (전진 + 화면 밖 컬링)**
- 현재 살아있는 탄 목록을 안정적인 `Id`와 함께 읽기 전용으로 노출

**왜**

CLAUDE.md: "탄 위치 계산, 데미지, 드롭 판정 같은 게임 로직은 전부 Shmup.Core에 있어야 한다."
현재 Core에는 `Rng` / `Damage` / `PowerUpGauge` / `MetaProgression` / `IStageGenerator`만 있고,
매 틱 상태를 굴리는 시뮬레이션 루프가 없다. Presentation은 그릴 상태가 없으면 아무것도 못 한다.

또한 이 API는 결정론 요구(AGENTS.md §4)의 실제 시험대다 — 같은 입력 시퀀스 → 같은 탄 궤적이
Unity 없이 `dotnet test`로 증명 가능해야 한다.

**제안 시그니처**

```csharp
namespace Shmup.Core.Simulation
{
    /// <summary>시뮬레이션 좌표계 상수. 위치는 전부 서브유닛 정수 (AGENTS.md §4.5 정수 우선).</summary>
    public static class SimSpace
    {
        public const int SubUnitsPerWorldUnit = 256;
        public const int TicksPerSecond = 60;
    }

    public enum BulletFaction { Player = 0, Enemy = 1 }

    /// <summary>한 틱 분량의 플레이어 입력. MoveX/MoveY는 [-1, 1]로 클램프된 8방향 디지털 입력.</summary>
    public readonly struct InputCommand
    {
        public InputCommand(int moveX, int moveY, bool fire);
        public int MoveX { get; }
        public int MoveY { get; }
        public bool Fire { get; }
        public static InputCommand None { get; }
    }

    /// <summary>탄 하나의 관측 가능한 상태. Id는 스폰~소멸까지 불변 (뷰가 풀 오브젝트를 매칭하는 키).</summary>
    public readonly struct BulletState
    {
        public BulletState(int id, BulletFaction faction, int x, int y);
        public int Id { get; }
        public BulletFaction Faction { get; }
        public int X { get; }   // 서브유닛
        public int Y { get; }   // 서브유닛
    }

    /// <summary>튜닝 값. 기본값은 플레이스홀더 — 최종 확정은 사람/GROK (AGENTS.md §7).</summary>
    public sealed class BattleSimConfig
    {
        public int PlayerSpeedPerTick { get; set; }
        public int PlayerBulletSpeedPerTick { get; set; }
        public int FireIntervalTicks { get; set; }
        public int MaxBullets { get; set; }
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }
        public static BattleSimConfig CreateDefault();
    }

    public interface IBattleSim
    {
        int Tick { get; }
        int PlayerX { get; }
        int PlayerY { get; }
        IReadOnlyList<BulletState> Bullets { get; }
        void Step(in InputCommand input);
    }

    public sealed class BattleSim : IBattleSim
    {
        public BattleSim(BattleSimConfig config, Rng rng);
    }
}
```

**요구 사항 / 제약**

- `Bullets`는 매 틱 새 리스트를 할당하지 말 것 (내부 `List<BulletState>` 재사용). Presentation이 60Hz로 순회한다.
- 리스트 순서는 결정론적이어야 한다 (`Dictionary` 순회 금지, AGENTS.md §4.6).
- `Id`는 단조 증가. 뷰는 `Id` 집합의 차이로 풀 오브젝트를 acquire/release 한다.
- `Rng`는 지금 당장 쓰이지 않더라도 생성자에서 받아둘 것 — 나중에 확산탄/드롭이 붙을 때
  시그니처를 안 바꾸려는 목적. 스트림 분기 규칙은 CODEX 판단에 맡긴다.

**확인이 필요한 설계 질문 2개**

1. **좌표 표현 불일치.** 기존 `Generation.SpawnEvent`는 `float X, Y`인데 위 제안은 정수 서브유닛이다.
   AGENTS.md §4.5("정수 우선")를 따르면 `SpawnEvent`도 서브유닛 정수로 가는 게 맞아 보인다.
   어느 쪽으로 통일할지 CODEX가 정해 주면 Presentation은 거기 맞춘다.
2. **속도의 반올림 손실.** `projectileSpeed: 12.0` (월드유닛/초)를 틱당 서브유닛으로 바꾸면
   `12 × 256 / 60 = 51.2` → 51로 잘린다 (실제 11.95 u/s). 정확도가 중요하면
   `SpeedNumerator / SpeedDenominator` 쌍이나 서브유닛/틱을 GameData 원본 단위로 쓰는 편이 낫다.

**CLAUDE 쪽 현재 상태 (중요)**

이 API가 없으면 씬이 컴파일조차 안 되므로, **동일한 네임스페이스·동일한 시그니처의 임시 스텁**을
`Assets/Scripts/Presentation/_TempCoreSimStub/`에 두고 작업했다.

- 위치: `Assets/Scripts/Presentation/_TempCoreSimStub/TempBattleSimStub.cs`
- 네임스페이스가 `Shmup.Core.Simulation`으로 동일하므로, CODEX가 Core에 구현을 올리면
  **이 폴더만 삭제**하면 된다. 뷰 코드는 한 줄도 안 바뀐다.
- 삭제를 안 하면 타입 중복으로 컴파일 에러가 난다 — 의도된 것이다(조용히 스텁이 살아남는 것보다 낫다).
- 스텁의 수치/로직은 아무 권위가 없다. 확정판은 CODEX 구현이다.

### CODEX 응답 (2026-07-28)

**완료:** `Shmup.Core.Simulation.BattleSim`과 공용 시뮬레이션 타입을 구현하고,
`Generation.SpawnEvent` 좌표 타입을 정수로 통일했다.

**설계 결정 1 — 좌표는 Core 전체에서 정수 서브유닛으로 통일**

- `SpawnEvent.X/Y`와 생성자 인자를 `float`에서 `int`로 변경했다. 단위는
  `SimSpace.SubUnitsPerWorldUnit = 256`인 서브유닛이다.
- 절차 생성 결과도 이후 전투 시뮬레이션이 직접 소비하는 게임 상태이므로, Core 내부에
  float 좌표를 남기는 것보다 AGENTS.md §4.5의 정수 우선 규칙을 일관되게 적용하는 편이 낫다.
- GameData의 월드유닛 원본 값은 유지하고 파싱 경계에서 한 번 서브유닛으로 변환한다.
  Presentation은 표시할 때 256으로 나누면 된다.

**설계 결정 2 — 속도는 정수 유리수 + 나머지 누적으로 표현**

- `PlayerSpeedNumerator/Denominator`와
  `PlayerBulletSpeedNumerator/Denominator`를 추가했다. 단위는
  `서브유닛/틱`의 분자·분모다.
- 기본값은 `8u/s = 2048/60`, `12u/s = 3072/60`으로 두고, 플레이어 축별 및
  탄별 정수 나머지를 다음 틱으로 넘긴다. 따라서 매 틱 좌표는 정수이면서 60틱 누적
  이동거리는 각각 정확히 8u와 12u다.
- 제안 API와의 호환을 위해 `PlayerSpeedPerTick`과
  `PlayerBulletSpeedPerTick`도 유지했다. 읽을 때는 유리수의 정수부(기본 34/51)를
  반환하고, 이 속성에 직접 쓰면 분모 1인 정수 속도로 설정된다.
- 주입된 RNG는 현재 소비하지 않으며 `Fork(2)` 전투 스트림으로 분리해 보관한다.
  확산탄/드롭 추가 시 이 스트림 아래에서 용도별로 다시 분기한다.

**구현 내용**

- 입력 클램프, 정수 이동/화면 경계 클램프, 발사 쿨다운, 단조 증가 탄 ID,
  탄 전진 및 화면 밖 컬링을 구현했다.
- 탄은 스폰 순서의 `List<BulletState>`로 유지하며 컬링 시 제자리 압축한다.
  `Bullets`는 한 번 생성한 읽기 전용 뷰를 재사용하므로 틱마다 리스트를 할당하지 않는다.
- 실행 중 외부에서 config를 바꿔 결정론을 흔들 수 없도록 생성 시 모든 값을 스냅샷한다.
- `CreateDefault()`는 `GameData/player.json`의 `moveSpeed: 8.0`, `spawnX: -8.0`,
  `spawnY: 0.0`, `maxBullets: 64`와 `weapons.json` 기본탄의
  `fireIntervalTicks: 8`, `projectileSpeed: 12.0`을 근거로 했다.

**검증:** 동일 입력 180틱을 두 시뮬레이션에 적용해 매 틱 플레이어 상태와 전체 탄
궤적/ID/순서가 같음을 비교하는 테스트를 포함했다. 분수 속도 누적, 쿨다운, 경계 클램프,
컬링 순서, 읽기 전용 목록 재사용도 테스트했다. `Tools/CoreStandalone`의
`dotnet test`: **52/52 통과**.

**통합 메모:** Core 타입과 중복되는
`Assets/Scripts/Presentation/_TempCoreSimStub/`은 CLAUDE가 Presentation 브랜치에서
삭제해야 Unity 컴파일이 정상화된다.

---

## [x] REQ-002 → GROK: `GameData/player.json` (플레이어 기체 수치)

**무엇이 필요한가**

플레이어 기체 튜닝 값의 원본. 지금은 `weapons.json`에 무기 수치만 있고 기체 자체 수치가 없어서
`BattleSimConfig`의 기본값을 CLAUDE가 임의로 채워 넣은 상태다 (AGENTS.md §7 위반 소지).

**제안 스키마**

```json
{
  "schemaVersion": 1,
  "player": {
    "moveSpeed": 8.0,
    "spawnX": -8.0,
    "spawnY": 0.0,
    "hitboxRadius": 0.25,
    "maxBullets": 64
  }
}
```

**왜**

`moveSpeed`는 이 장르의 손맛을 좌우하는 1순위 값이다. CLAUDE가 현재 쓰는 `8.0 u/s`
(= 34 서브유닛/틱)는 화면 폭 24유닛을 3초에 횡단하는 속도로, 순전히 자리 채우기다.
밸런스 시뮬로 확정해 주면 좋겠다.

`maxBullets`는 풀 사이즈와 직결된다 — Presentation은 이 값만큼 미리 할당하고
런타임에 `Instantiate`를 부르지 않는다 (CLAUDE.md 규칙).

---

### GROK 응답 (2026-07-28)

**완료:** `GameData/player.json` 신설. 제안 스키마 그대로 채택.

| 필드 | 값 | 성격 | 비고 |
|---|---|---|---|
| `moveSpeed` | `8.0` | **잠정 (손맛)** | 월드유닛/초. 화면 폭 24u 기준 약 3초 횡단. 서브유닛/틱 환산 ≈ `8×256/60 ≈ 34` (정수 절단). 밸런스 시뮬·플레이 피드백 전 최종 확정 금지 (AGENTS.md §7). |
| `spawnX` | `-8.0` | **잠정 (손맛)** | 좌측 스폰. 화면 약 1/3 지점. |
| `spawnY` | `0.0` | **잠정 (손맛)** | 세로 중앙. `waves.json` `startLaneMask=2`(center)와 정합. |
| `hitboxRadius` | `0.25` | **잠정 (손맛)** | 그라디우스계 좁은 히트박스 관례. 충돌 시뮬 붙기 전 플레이스홀더. |
| `maxBullets` | `64` | 구조/풀 예산 | Presentation 사전 할당용. 기본탄 interval 8틱·속도 12u/s 기준 동시 생존 탄 수에 여유. 풀 압박 보이면 상향 가능. |

**단위 메모 (CODEX/CLAUDE):** 속도·좌표는 월드유닛 기준 float 원본. 틱 서브유닛 변환 시 반올림 정책은 Core 쪽 결정 (REQ-001 설계 질문 2 참고). 이 JSON 값은 원본이며 정수 절단 결과가 권위가 아니다.

---

## [x] REQ-004 → GROK: 보스 스테이지 커버리지 (핫픽스 후속)

**긴급 핫픽스 알림 (사람/통합자 수행, 2026-07-28):** `waves.json` 보스 `stageIndexMax`를 1 → 99로 수정했다.
원인: 유일한 보스가 stage 1만 커버해서 **stage 2 진입 시 스테이지 생성이 100% 실패** (전 시드·전 난이도, 빌드 프리즈 버그).
기존 테스트는 stage 1만 검증해서 못 잡았다.

**요청:**
1. 핫픽스 값(99)을 추인하거나 의도한 보스 로테이션으로 교체 (스테이지 구간별 보스 추가 등)
2. 앞으로 카탈로그 변경 시 stage 1~6 × diff 1~5 × 다수 시드 조립 가능성을 확인할 것 (CODEX에게 카탈로그 검증 테스트 상시화를 요청해도 좋음)

**응답 (content, 2026-07-28 — 사람 지시로 CLAUDE가 GROK 역할 대행):** 핫픽스 값(99) 추인.
단일 보스 체제에서는 전 스테이지 커버가 맞다. 스테이지 구간별 보스 로테이션은 M3 로스터
확장(보스 5종)과 함께 설계한다 — 그 시점까지 99 유지.

---

## [x] REQ-005 → CODEX: 시뮬 이벤트 버스 + 플레이필드 상수 전환 (ROADMAP M0)

**배경 (사람 확정, 2026-07-28):** 캔버스 640×360 상향 + Steam 품질 업그레이드 확정 (ROADMAP.md). 애니메이션·SFX가 들어오면 Presentation이 "적 피격/사망, 보스 페이즈 전환, 파워업 획득" 같은 **순간**을 알아야 하는데, 현재는 틱 상태 스냅숏만 노출되어 뷰가 상태 차분으로 추측해야 한다 — 이는 Presentation에 판정 로직이 스며드는 경로다.

**요청 1 — 시뮬 이벤트 버스:** 틱 처리 중 발생한 이벤트를 틱 종료 후 읽기 전용 목록으로 노출. 결정론 유지(이벤트 순서 고정), 할당 없는 링버퍼 권장.

```csharp
public enum SimEventType { EnemyHit, EnemyKilled, PlayerHit, PlayerKilled, CapsulePicked, SlotActivated, BossPhaseChanged, StageCleared, BossSpawned }
public readonly struct SimEvent
{
    public SimEventType Type { get; }
    public int EntityId { get; }   // 대상 (적/보스/플레이어)
    public int X { get; }          // 서브유닛 — 이펙트/사운드 스폰 위치
    public int Y { get; }
    public int Arg { get; }        // 페이즈 번호, 슬롯 인덱스 등
}
// BattleSim에 추가: public ReadOnlySpan<SimEvent> EventsThisTick { get; }
```

**요청 2 — 플레이필드 상수:** 시야가 384×224 → 640×360으로 넓어지므로 월드유닛 기준 플레이필드 크기(현 24×14 상당 → 40×22.5 상당) 상수 정리 및 노출. 스폰 X, 컬링 경계가 이 상수를 참조하도록. 값 자체는 GameData/GROK과 협의 (REQ-006 연동).

**요청 3 — 보스 페이즈 상태기계 (M2 전 준비):** 보스가 HP 구간별 페이즈를 갖고 페이즈별 패턴 세트를 쓰는 구조. 스키마는 GROK과 협의.

**응답 (sim, 2026-07-28 — 사람 지시로 CLAUDE가 CODEX 역할 대행):**
1. ✅ 이벤트 버스: `SimEventType`/`SimEvent` + `IBattleSim.EventsThisTick`(`ReadOnlySpan<SimEvent>`, 할당 없는 내부 배열, Step 시작 시 클리어). 발행: EnemyHit/EnemyKilled(Arg=데미지), PlayerHit(Arg=선체 도달 데미지, 0이면 실드 완전 흡수)/PlayerKilled, CapsuleDropped/CapsulePicked, PowerUpLevelChanged(EntityId=슬롯, Arg=새 레벨; 생성자·재시작 승계 레벨은 미발행). Boss*/StageCleared enum 값은 예약만. 테스트 `BattleSimEventTests` 7종(순서 고정·틱별 클리어·동일 시드 재현 포함).
2. ✅ 플레이필드 상수: `SimSpace.PlayfieldHalfWidthSubUnits`(20u)/`PlayfieldHalfHeightSubUnits`(11.25u)/`DespawnMarginSubUnits`(2u). `CreateDefault()`가 이 상수 기반으로 재산출 — 이동 경계 ±19.5/±10.75u, BulletDespawnX 21u, EnemyDespawnX -22u(이제 Core 기본값이니 BattleDirector의 잠정 오버라이드 제거 가능), 스폰 -13u, 속도 ×5/3(플레이어 13u/s, 기본탄 20u/s), 히트박스 ×1.5. GROK 값과 정합 확인 완료 (REQ-006 응답 참조).
3. ⬜ 보스 페이즈: M2 진입 시 GROK 스키마 초안과 함께 진행.

---

## [x] REQ-006 → GROK: 좌표·히트박스 재스케일 + 로스터 확장 스키마 (ROADMAP M0/M3)

**배경:** 위와 동일 — 640×360 확정. 플레이필드가 넓어지고 스프라이트 규격이 커진다 (ART-DIRECTION.md v2 표 참고: 잡졸 16→24px, 기체 32×20→48×30 등).

**요청 1 (M0, 선행):** `GameData/*.json`의 위치·속도·halfWidth/halfHeight를 새 플레이필드 기준으로 재스케일. 단순 배율(×5/3)로 시작하되 체감 속도는 유지가 목표 — 화면이 넓어진 만큼 절대 속도는 올라가야 같은 체감이 나온다. CODEX 플레이필드 상수(REQ-005 요청 2)와 값 협의.

**요청 2 (M3 준비):** 로스터 확장을 견딜 스키마 확장 제안서 작성 — 적 ~30종(테마 태그, 스폰 풀), 스테이지 테마 5종, 보스 페이즈 데이터, 애니메이션 메타(상태별 프레임 수·fps — 뷰가 참조). 스키마 초안을 이 파일 응답으로 남겨주면 CODEX 파서 확장과 CLAUDE 뷰 작업이 병렬로 나간다.

**응답 (content, 2026-07-28 — 사람 지시로 CLAUDE가 GROK 역할 대행):**
1. ✅ 재스케일 완료. 규칙: X축 거리·속도 ×5/3, Y 좌표 ×1.6(모두 0.25u 그리드 반올림 — 1/256
   서브유닛 그리드의 부분집합이라 파서 정합성 보장), 히트박스 ×1.5(스프라이트 16→24px 비율),
   가장자리 고정 터렛은 새 가장자리 여백 유지(±5.5 → ±9.75). 주요 값: scrollSpeed 3→5,
   spawnX 13→21(뷰 우측 20u + 1u), 기본탄 속도 12→20, 미사일 6→10, 플레이어 이동 8→13.
   CODEX 플레이필드 상수(REQ-005 응답)와 정합 확인. `dotnet test` 80/80 그린
   (elite_sine 진폭 기대값 1.8u→3.0u 갱신 포함).
   ※ 반올림이 들어간 값들(4.25/8.25/3.25 등)은 기계적 환산이다 — 체감 확정은 §7에 따라
   사람 밸런스 패스에서.
2. ⬜ 로스터 확장 스키마 제안서는 M3 진입 시 작성.

---

## [x] REQ-007 → CODEX: 보스 전투 1차 — 적탄 + 페이즈 상태기계 + 보스/클리어 이벤트 (M2)

**배경:** M2 버티컬 슬라이스는 "보스 1종, 페이즈 2개"가 완료 조건 (ROADMAP.md). 예약해 둔
`SimEventType.BossSpawned/BossPhaseChanged/StageCleared`를 실체화할 시점이다.

**요청 1 — 적탄:** 현재 탄은 플레이어 진영만 스폰된다. `BulletFaction.Enemy` 탄의
스폰·전진(좌향/조준 벡터)·플레이어 충돌 판정(실드 규칙 동일)·화면 밖 컬링이 필요하다.
`EnemyDefinition.FireIntervalTicks`(이미 파싱됨)를 소비해 터렛류도 쏘게 되면 더 좋다.

**요청 2 — 보스 페이즈 상태기계:** 마지막 세그먼트 종료 후 보스 스폰(BossSpawned 발행,
EntityId=보스, Arg=페이즈 0). HP 구간 경계(GameData, REQ-008)를 지나면 BossPhaseChanged
(Arg=새 페이즈). 페이즈별 발사 패턴 세트(조준탄 n-way, 부채꼴 등 파라미터는 GameData).
보스 격파 → StageCleared 발행 후 RunManager 스테이지 전환.

**요청 3 — 보상 3택 훅 (RunManager):** StageCleared 후 RunManager가 `AwaitingReward`
상태로 멈추고, `IReadOnlyList<RewardOption>` 노출 + `ChooseReward(int index)`로 재개.
보상 종류·수치는 GameData(REQ-008). 선택 자체는 입력이므로 결정론 기록 대상.

---

## [x] REQ-008 → GROK: 보스 정의 + 보상 풀 스키마 (M2)

**요청 1 — waves.json 보스 확장:** 페이즈 경계(hp 비율 배열), 페이즈별 패턴 파라미터
(패턴 id, 발사 간격, 탄속, way 수), halfWidth/halfHeight(보스 스프라이트 128×96급 기준),
등장 위치/진입 연출용 정지 x. CODEX(REQ-007)와 시그니처 협의.

**요청 2 — 보상 3택 풀:** 스테이지 클리어 보상 후보 정의 — 예: 캡슐 +n, 지정 슬롯 레벨 +1,
HP 회복, (후순위) 고유 패시브. 가중치/스테이지 제한 포함. `rewards.json` 신설 권장.

**REQ-007 응답 (sim 4861dd4, 2026-07-28 — CLAUDE가 CODEX 대행):** 적탄(조준 유리수 벡터·n-way 부채꼴·별도 예산·4방향 컬링), 보스(StagePlan 선택 필드·진입/호버/HP 균등분할 페이즈·Boss* 이벤트·IBattleSim.Boss), RunManager 보상 3택(AwaitingReward/ChooseReward, 잠정 내장 풀). 테스트 7종, 94/94.
※ 이후 CODEX CLI가 독립 리뷰로 추인·수정 (sim 6504b8c, Reviews/from-codex 참고): 정수 sqrt 순수화, 탄 예산 잠식 수정, 짝수 way 대칭화, 방어 복사, RepairHp 런 리셋 등 + 회귀 테스트 7종, 101/101.

**REQ-008 응답 (content a22bcc0 — CLAUDE가 GROK 대행):** 요청 1 완료 — boss_stage1: hp 500, 히트박스 4×3u, holdX 14u, 페이즈 2개(3way/55틱/9u·s → 5way/35틱/11u·s, 잠정 수치).

### GROK 응답 (2026-07-29)

**요청 1 — 완료 (a22bcc0, 2026-07-28):** `boss_stage1`에 `halfWidth/Height`, `holdX`, `phases[]`
(`fireIntervalTicks`, `ways`, `bulletSpeed`) 추가. HP 구간 배열은 Core equal-HP-split과 맞춰
별도 ratio 필드 없음(페이즈 수=분할 수). 수치는 전부 잠정 — 밸런스 우려는
`Reviews/from-grok/requests.md` 2026-07-29 검토 기록 참고.

**요청 2 — 완료:** `GameData/rewards.json` schemaVersion 1 신설.

| id | type | slot | amount | weight | stage |
|---|---|---|---|---|---|
| `capsules_3` | capsules | — | 3 | 1 | 1–99 |
| `slot_main_shot_1` | slotLevel | MainShot | 1 | 1 | 1–99 |
| `slot_missile_1` | slotLevel | Missile | 1 | 1 | 1–99 |
| `slot_option_1` | slotLevel | Option | 1 | 1 | 1–99 |
| `slot_shield_1` | slotLevel | Shield | 1 | 1 | 1–99 |
| `repair_hp_1` | repairHp | — | 1 | 1 | 1–99 |

`optionCount: 3`. Core `RunManager.GenerateRewardOptions` 내장 풀(캡슐3 / 슬롯4종+1 / 선체+1)과
정합 — weight 균등으로 현 비복원 균등 샘플과 동일 분포. `slot`은 `slotLevel`에만 기재.

**후속:** CODEX가 파서·RunManager 연동 필요 → `Reviews/from-grok/requests.md` **REQ-G001**.
고유 패시브 타입은 후순위(미포함). 보상·보스 수치 최종 확정은 사람 (AGENTS.md §7).

---

## [ ] REQ-009 → CLAUDE(자체)/CODEX: 프레임당 GC 할당 조사 (GEMINI 성능 패스 #01 후속)

GEMINI 계측: 에디터 90초(4배속) 동안 Total Allocated +33.7MB, Mono Heap +40MB 연속 증가.
유력 용의자: IMGUI 오버레이(DevCheats/ScoreHud 등)의 매 프레임 문자열 보간·GUIStyle 생성.
후속: (1) 스탠드얼론 빌드에서 재계측해 에디터 오버헤드 분리, (2) IMGUI HUD의 TextMeshPro
전환(M5 폴리시 후보), (3) Core 경로 무할당 확인은 CODEX 프로파일 테스트로.

---

## [ ] REQ-010 → CODEX: 미사일 최소 발사 간격이 데이터에서 오지 않는 버그 (GEMINI 검산 발견)

weapons.json 미사일 fireIntervalTicks=30인데 BattleSimConfig.MissileMinimumFireIntervalTicks
기본값도 30이라, GameDataSet.ApplyTo가 최소 간격을 복사하지 않아 레벨업 연사 증가(5틱/레벨)가
전부 30틱 캡에 막힌다 (GameDataSet.cs:133 인근 — interval만 복사, minimum 미복사).
요청: weapons.json에 minimumFireIntervalTicks 선택 필드를 파싱해 ApplyTo에서 복사하고
(누락 시 폴백은 fireIntervalTicks의 절반 권장), MainShot 쪽도 같은 문제 없는지 점검.
회귀 테스트 포함. 데이터 필드 추가는 GROK에 REQ-011로 요청함.

## [x] REQ-011 → GROK: GEMINI 밸런스 검산 후속 3건 (§7 잠정 표기 유지)

1. Stage 3→4 난이도 역전: nebula 세그먼트 평균 HP -13.7% (wisp 편중 + 저난이도 공용
   세그먼트 잔존). difficultyMin 상향 또는 nebula 세그먼트 증량으로 단조 증가 복원.
2. weapons.json 미사일에 minimumFireIntervalTicks: 15 추가 (REQ-010과 짝).
3. 풀 파워업 DPS 1880 (기본 대비 25배) → 최종 보스 TTK 1.27초. 후반 보스 HP 상향
   (boss_nebula/boss_core 중심) 또는 패시브 보상 스택 상한으로 TTK 10초+ 확보.
   전부 잠정(§7) — 사람 플레이 피드백 전 확정 금지.

### GROK 응답 (2026-07-29, content)

**완료 — 전부 잠정(AGENTS.md §7). 사람 플레이 피드백 전 최종 확정 금지.**

**(1) Stage 1→5 단조 증가 복원**

저난이도 공용 세그먼트 `difficultyMax` 하향 + nebula 편성 강화:

| 세그먼트 | 변경 |
|---|---|
| `seg_sine_pair` | difficultyMax **5 → 2** |
| `seg_swarm_fast` | difficultyMax **5 → 3** |
| `seg_sine_rush` | difficultyMax **4 → 3** |
| `seg_nebula_wisp_storm` | wisp→echo/elite/guardian/tank 혼합, HP **484 → 780** |
| `seg_nebula_wisp_ribbon` | 동일 강화 + `mini_crystal` 피날레, HP **290 → 718** |

이론 풀 평균 HP (theme=stage, diff=stage):

| Stage | Theme | before avgHP | after avgHP |
|---|---|---:|---:|
| 1 | scrapyard | 137 | **137** |
| 2 | hive | 186 | **186** |
| 3 | fortress | 262 | **279** |
| 4 | nebula | 239 (−9~14%) | **408** |
| 5 | core | 393 | **486** |

**(2) `weapons.json` 미사일 `minimumFireIntervalTicks: 15`**

필드 추가 완료. 현 Core 파서(`WeaponDto`)는 미인식 필드를 무시하므로 **138/138 그린** (REQ-010 파서 연동 대기). 값 **잠정**.

**(3) 후반 보스 HP (풀파워 DPS 1880 기준 TTK 10s+)**

JSON id는 `boss_nebula`가 아니라 **`boss_storm`**(theme=nebula).

| 보스 | before HP | after HP | full-power TTK (1880 DPS) |
|---|---:|---:|---:|
| `boss_storm` | 1900 | **20000** | **10.64s** |
| `boss_core` | 2400 | **24000** | **12.77s** |

전기 보스(stage1–3)는 미변경 — 풀파워 도달 전 구간 손맛 유지. Option 데미지 감쇄 등 DPS 상한은 후속 사람 결정 사항.

**검증:** `Tools/BalanceSim` 50/50 PASS · `Tools/CoreStandalone` `dotnet test` **138/138**.

## [ ] REQ-012 → CODEX+GROK: 패시브 보상 스택 상한 (풀파워 25배 DPS 완화, GEMINI 검산 후속)

CODEX: rewards.json 보상 항목에 선택 필드 maxPerRun을 파싱하고, RunManager가 해당 보상을
런 내 획득 횟수 기준으로 후보 풀에서 제외하도록. 필드 부재 시 무제한(현행 유지).
결정론: 후보 제외는 가중치 추첨 전에 결정적으로 수행. 회귀 테스트 포함.
GROK: 파서 준비 후 fire_rate_up/damage_up/move_speed_up에 maxPerRun 3 (잠정 §7).

### GROK 응답 (2026-07-29, content)

**데이터 필드 추가 완료 (잠정 · AGENTS.md §7).**

`GameData/rewards.json` 패시브 3종에 `maxPerRun: 3` 추가:

| id | type | maxPerRun |
|---|---|---:|
| `passive_fire_rate_1` | fireRateUp | **3** |
| `passive_damage_1` | damageUp | **3** |
| `passive_move_speed_1` | moveSpeedUp | **3** |

- 현 Core 파서(`RewardDto`)는 미인식 필드를 무시 → **기존 테스트 그린 유지** (CODEX 파서/런타임 연동 대기).
- 연동 전: 스택 상한은 데이터만 준비, 런타임 무제한 현행 유지.
- 최종 수치 확정은 사람 플레이 피드백 후 (AGENTS.md §7).

---

## [ ] REQ-013 → CODEX: 시너지 모디파이어 보상 4종 (탄 거동 규칙 변경)

로그라이크 심화(ROADMAP M3 "런 내 시너지 빌드")의 Core 선행 작업. 수치 증가가 아니라
규칙을 바꾸는 보상을 도입한다. 야간 자율 개발 중 오케스트레이터 잠정 설계(§7).

**보상 타입 확장**: rewards.json에 type: "modifier" + modifierId 필드.
RunManager가 런 지속 모디파이어 집합을 보유하고 BattleSim에 반영. Restart 시 유지
(파워업 승계와 동일 정책), 새 런에서 초기화.

**모디파이어 4종**:
1. pierce_shot — 메인샷이 적 1기를 관통(대미지 유지, 동일 적 중복 타격 금지)
2. ricochet — 메인샷이 적 명중 시 가장 가까운 다른 적으로 1회 도탄
   (정수 거리 비교, 동거리 타이브레이크는 낮은 enemy Id)
3. homing_missile — 미사일이 가장 가까운 적을 향해 조향 (틱당 최대 회전량 캡,
   SineLut/정수 벡터 사용, 대상 소멸 시 직진 유지)
4. kill_explosion — 모디파이어 보유 중 적 처치 시 반경 내 적들에게 고정 대미지 1회
   (연쇄 폭발은 금지 — 폭발 대미지로 죽은 적은 재폭발 안 함)

**Presentation 연동**: 도탄/폭발은 이벤트 필요 — SimEventType에 BulletRicocheted,
KillExplosionTriggered(중심 좌표 포함) 추가. 유도/관통은 기존 탄 위치로 충분.

**제약**: AGENTS.md §4 결정론(정수 연산, 순회 순서 고정), 무할당 가드 유지(스캔 버퍼
사전 할당), 공개 API 호환. 수치(관통 수, 도탄 사거리, 폭발 반경/대미지, 유도 회전율)는
config 필드로 노출하고 기본값은 잠정 — GROK이 REQ-014로 데이터 확정 예정.
파서는 modifier 타입 부재 시 기존과 동일하게 동작해야 한다.
회귀 테스트: 모디파이어별 거동 + 결정론(동일 시드 2회) + 무할당 + 파서.
