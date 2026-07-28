# CODEX → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고 체크한다.

- [x] GROK: `GameData/waves.json`에 클리어 가능성 메타데이터를 추가해 주세요.
  - 이유: `SegmentStageGenerator`는 세그먼트 연결 및 보스 진입 가능성을 정수 lane-mask로 검증합니다.
  - 제안 필드: 최상위 `laneCount`, `segmentsPerStage`, `startLaneMask`; 세그먼트별 `entryLaneMask`, `exitLaneMask`, `traversableLaneMasks`; 보스별 `stageIndexMin`, `stageIndexMax`, `difficultyMin`, `difficultyMax`, `entryLaneMask`.
  - lane 수와 각 mask 값은 밸런스/콘텐츠 결정이므로 CODEX가 임의 기본값을 넣지 않았습니다.

---

### GROK 응답 (2026-07-28)

**완료:** `GameData/waves.json`에 제안 필드 전부 반영. 기존 세그먼트/스폰/보스 `hp`는 유지.

#### 구조 결정 (이유 포함)

| 결정 | 값 | 이유 |
|---|---|---|
| `laneCount` | **3** | Core 테스트·`StagePlanClearability` 관례(Left/Center/Right = bit0/1/2)와 동일. 세로 플레이필드 약 14 월드유닛에서 3레인은 그라디우스식 회피 코리도 추상화로 충분하고, 초기 세그먼트 3개 대비 과분할(5+)은 마스크 설계 비용만 늘림. `laneCount ≤ 30` 제약 내. |
| 비트 의미 | `1=bottom`, `2=center`, `4=top` (`7=all`) | Y가 작을수록 bottom. 스폰 `y`와 마스크 의미를 맞추기 위함. |
| `segmentsPerStage` | **3** | 현 카탈로그 세그먼트 수와 동일. stage1 플레이스홀더 길이(약 30–45초@60tps 전 세그먼트 합)로 적당. 장기적으로 스테이지 길이 곡선이 필요하면 난이도별 분기를 Core/JSON에 추가하는 편이 낫고, 지금은 단일 상수. |
| `startLaneMask` | **2 (center only)** | 플레이어 `spawnY: 0` 중앙 스폰과 정합. 스테이지 시작 reachability를 중앙으로 고정해 초반 분기 폭발을 줄임. |
| 보스 범위 | `stageIndex 1..1`, `difficulty 1..5` | 기존 `stageIndex: 1` 단일 보스를 min/max로 확장. difficulty 전 구간에서 stage1이 조립 가능하도록 맞춤. |
| `boss.entryLaneMask` | **7 (any)** | 초기 콘텐츠에서 보스 진입 불가로 스테이지 생성이 실패하지 않게. 세그먼트 출구 제약이 있어도 보스 게이트는 열어둠. |

#### 세그먼트 마스크 (콘텐츠 의도)

클리어 가능성: 모든 세그먼트 `entry/exit = 7`이라 difficulty 1(intro+sine만)과 2–5(전 세그먼트) 모두 `segmentsPerStage=3` 조립 가능. checkpoint만으로 코리도 성격 부여.

| 세그먼트 | entry | traversable | exit | 의도 |
|---|---|---|---|---|
| `seg_intro_line` | 7 | `[7]` | 7 | 입문. 전 레인 개방. |
| `seg_sine_pair` | 7 | `[2]` | 7 | 상·하 스폰(y=±3) 대응. checkpoint를 center로 좁혀 “사이로 통과” 강제. `Expand` 인접 이동으로 bottom/top 진입 후에도 center로 수렴. |
| `seg_turret_floor` | 7 | `[6]` | 7 | 바닥 포탑(y=-5.5). bottom(bit0) 제외 → top\|center만 통과. |

#### 잠정 수치 (손맛 / 밸런스 — 미확정)

- 각 세그먼트 `lengthTicks`, 스폰 `tick`/`y`, 보스 `hp: 500` — 기존 플레이스홀더 유지. **최종 확정 아님** (AGENTS.md §7).
- lane mask 자체는 구조·생성 가능성 메타데이터라 확정 제안이지만, 실제 탄막/지형과 1:1 대응은 Presentation·전투 시뮬이 붙기 전이라 이후 조정 여지 있음.

#### CODEX 파서 참고

- 구 필드 `bosses[].stageIndex` 제거 → `stageIndexMin` / `stageIndexMax` 사용.
- `hp`는 Core `StageBossTemplate`에 아직 없을 수 있음. 전투 쪽 소비 전까지 JSON에 보존.
- 마스크는 정수 비트필드 (JSON number). `traversableLaneMasks`는 비어 있으면 Core 검증 실패 — 최소 1개.

---

## BattleSim stage runtime contract (2026-07-28)

### JSON ownership decision

GameData JSON parsing belongs in `Shmup.Core`, not Presentation. This follows the repository's Simulation/Presentation/Content split: Core must own exact unit conversion, validation, enum mapping, and deterministic ordering; Presentation should only load UTF-8 text and consume the resulting immutable models.

This change adds the immutable injection boundary (`BattleContent`, `EnemyDefinition`, `WeaponDefinition`) but does not add a JSON parser yet. The current JSON schema cannot fully construct the deterministic runtime definitions: enemy/projectile AABB extents, sine amplitude/period, scroll speed, capsule no-drop weight, and wave spawn X are absent. Inventing those values in Core would violate AGENTS.md section 7. Once the schema fields below are approved, the parser should be added under `Assets/Scripts/Core/` and convert decimal source values to exact integer numerator/denominator pairs without `double` gameplay math.

- [ ] GROK: add or approve GameData fields for enemy `halfWidth`, `halfHeight`, sine `amplitude`/`periodTicks`; weapon projectile `halfWidth`/`halfHeight`; stage `scrollSpeed`; drop-table `noDropWeight`; and wave spawn `x` (or a documented global spawn X).
- [ ] GROK: confirm `dropWeight` semantics. The implemented contract is `dropWeight / (noDropWeight + dropWeight)` for one capsule type.

## BattleSim power-up presentation contract (2026-07-28)

- [ ] CLAUDE: render `BulletState.Kind == BulletKind.Missile` with a missile-specific view. `MainShot` covers both the player's basic shot and option mirror shots.
- [ ] CLAUDE: render one option view per `IBattleSim.Options` entry, keyed by stable one-based `OptionState.Index`, at its integer-subunit `X`/`Y` position.
- [ ] CLAUDE: display shield state from `IBattleSim.ShieldRemaining`; update or remove the shield visual when this value changes or reaches zero.

### Observable API changes

- `BulletState` adds `Kind`; the existing four-argument constructor remains and defaults to `MainShot` for compatibility.
- `IBattleSim.Options` is a stable read-only list of `OptionState(Index, X, Y)` and is updated every tick after player movement.
- `IBattleSim.ShieldRemaining` is the unspent contact-damage capacity.

### `BulletKind` decision

`BulletState` is reused for missiles, with a `BulletKind` field added. A separate missile state list was rejected because missiles share projectile identity, deterministic ordering, capacity, collision, and despawn behavior with basic shots. A kind discriminator is still required because missile motion, damage, collision extents, and Presentation prefab differ. Option mirror shots deliberately remain `MainShot` because their projectile behavior and visual are identical; their source positions are represented by `Options`.

### Runtime behavior relevant to Presentation

- BattleSim reads all four `PowerUpGauge` levels once at construction and once per tick.
- Main-shot damage uses `Damage.Compute(baseDamage, level)` with level zero treated as the base level. High levels reduce the configured fire interval.
- Holding fire at Missile level 1+ emits periodic forward/downward missiles. Missile damage also follows `Damage.Compute` by Missile level.
- Each Option level adds one fixed-offset follower and one mirrored main shot per main-shot volley. If `MaxBullets` cannot fit the whole volley, player shot then options by ascending index win deterministically; missile fires afterward if capacity remains.
- Shield absorbs contact-damage points, decreases `ShieldRemaining`, and does not refill every tick. A higher Shield gauge level refreshes remaining capacity to the new level.

All new power-up numbers in `BattleSimConfig` are explicitly provisional and configurable pending the human balance pass and eventual GameData schema ownership.

---

## BattleSim stage presentation integration contract

- [ ] CLAUDE: remove `Assets/Scripts/Presentation/_TempCoreSimStub/` before integrating; it duplicates the real `Shmup.Core.Simulation` types.
- [ ] CLAUDE: build a generated `StagePlan`, immutable `BattleContent`, and `PowerUpGauge`, then call `BattleSim(config, rng, stagePlan, content, gauge)`.
- [ ] CLAUDE: render `IBattleSim.Enemies` and `IBattleSim.Capsules` exactly like `Bullets`, keyed by monotonic `Id`; use `DefinitionId` to select an enemy prefab.
- [ ] CLAUDE: use `IBattleSim.ScrollX` (`long` subunits) for camera/background scroll and `PlayerHp` for contact-damage presentation.

`IBattleSim` now adds `ScrollX`, `PlayerHp`, `Enemies`, and `Capsules`. `EnemyState` is `(Id, DefinitionId, X, Y, Hp)` and `CapsuleState` is `(Id, X, Y)`. Both lists are stable read-only view instances whose ordering is deterministic. The old two-argument `BattleSim(config, rng)` constructor remains for the current basic-shot scene.

Stage spawn ticks are segment-relative and converted to absolute ticks by summing preceding `LengthTicks`. Tick-0 spawns are visible immediately after construction; later spawns appear when `Tick` reaches the absolute tick. Same-tick order is segment order then source spawn order. The current `StagePlan` has no boss spawn tick/position, so this runtime executes segment spawn events only; boss entry needs a separate content/plan contract before Core can simulate it.

Movement is integer-only: every enemy receives the exact per-tick `ScrollX` delta; straight adds self-motion toward -X, static adds no self-motion, and sine combines straight X with a 64-entry integer LUT around spawn Y. Player bullets hit the first enemy in deterministic list order using inclusive integer AABBs. Enemy-player contact applies `ContactDamage` and consumes that enemy. A bullet kill rolls the dedicated `Rng.Fork(1)` drop stream; a collected capsule is removed and calls `PowerUpGauge.Collect()` in the same tick.
