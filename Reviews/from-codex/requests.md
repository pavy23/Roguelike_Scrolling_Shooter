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

- [x] GROK: add or approve GameData fields for enemy `halfWidth`, `halfHeight`, sine `amplitude`/`periodTicks`; weapon projectile `halfWidth`/`halfHeight`; stage `scrollSpeed`; drop-table `noDropWeight`; and wave spawn `x` (or a documented global spawn X).
- [x] GROK: confirm `dropWeight` semantics. The implemented contract is `dropWeight / (noDropWeight + dropWeight)` for one capsule type.

### GROK 응답 (2026-07-28) — BattleSim stage runtime schema

**완료:** `GameData/enemies.json` · `weapons.json` · `waves.json` 스키마 확장 (`schemaVersion` 1 → **2**).  
`TempStageContent.cs` 잠정값을 참고하되 콘텐츠 관점에서 전 적/무기 카탈로그로 일반화. **모든 신규 수치는 잠정(플레이스홀더)** — 손맛·밸런스 최종 확정은 사람 결정 (AGENTS.md §7).

#### 1) 필드 배치

| 위치 | 필드 | 단위 | 비고 |
|---|---|---|---|
| `enemies.json` → 각 enemy | `halfWidth`, `halfHeight` | 월드유닛 (half-extent AABB) | PPU 16 기준 스프라이트 대략 절반. 파서는 서브유닛 정수 변환. |
| `enemies.json` → 각 enemy | `amplitude`, `periodTicks` | 월드유닛 / 틱 | **전 적에 필수.** non-sine은 `amplitude: 0`, `periodTicks: 1` (Core `EnemyDefinition` 제약: period ≥ 1). sine만 의미 있는 값. |
| `enemies.json` → 최상위 | `dropTable.noDropWeight` | 정수 가중치 | 전역 단일 캡슐 타입용. `BattleSimConfig.CapsuleNoDropWeight` 대응. |
| `weapons.json` → 각 weapon | `projectileHalfWidth`, `projectileHalfHeight` | 월드유닛 | flat 키 (기존 `projectileSpeed`와 동일 스타일). option/shield는 0. |
| `waves.json` → 최상위 | `scrollSpeed` | 월드유닛/초 (u/s) | 파서: `ScrollSpeedNumerator = scrollSpeed * U`, `Denominator = Tps`. |
| `waves.json` → 최상위 | `spawnX` | 월드유닛 | **전역 스폰 X** (아래 정책). 세그먼트 스폰 엔트리에는 `x` 없음. |

스폰 엔트리 스키마는 기존 유지: `{ "tick", "enemyId", "y" }` only.

#### 2) 웨이브 스폰 X 정책 — **전역 `spawnX` 채택**

**결정:** 세그먼트/스폰별 `x`가 아니라 `waves.json` 최상위 `spawnX: 13.0` 단일 값.

**근거:**
1. 그라디우스형 횡스크롤에서 적은 카메라 우측 밖 고정 라인으로 진입하는 것이 기본. 대형은 이미 `y`·`tick`·적 타입으로 표현한다.
2. 스폰별 `x`를 두면 모든 spawn 행이 비대해지고, 화면 폭(24u @ 384×16 PPU) 변경 시 일괄 수정이 어렵다. 전역이면 뷰포트 튜닝 한 곳.
3. Core `SpawnEvent`는 `(tick, enemyId, x, y)`를 받지만, 카탈로그 → 플랜 변환 시 `x = catalog.spawnX`를 주입하면 됨. 세그먼트 상대 깊이 오프셋이 나중에 필요하면 optional per-spawn `x` 오버라이드를 schema v3로 추가 가능 — 지금은 YAGNI.
4. `TempStageContent`도 `const int spawnX = 13 * U` 전역 고정이었음 → 동일 정책 승인.

`13.0` ≈ 우측 플레이필드 밖 1u (뷰 반폭 ~12). **잠정.**

#### 3) 제안 수치 (전부 잠정)

**dropTable**

| 필드 | 값 | 의도 |
|---|---|---|
| `noDropWeight` | **8** | Temp 동일. zako_straight `dropWeight:4` → P(drop)=4/12≈33%. 잡졸 상대 가중치 4–5 기준으로 캡슐이 과다/과소하지 않은 중반 경제. |

**scrollSpeed:** `3.0` u/s (Temp 동일). 스테이지 체감 스크롤. **잠정.**

**적 hitbox / sine** (Temp 3종 정렬 + 확장 5종 역할 스케일)

| id | halfW×halfH | amplitude | periodTicks | 비고 |
|---|---|---|---|---|
| `zako_straight` | 0.5 × 0.375 | 0 | 1 | Temp: U/2 × U×3/8. ~16×12px |
| `zako_sine` | 0.5 × 0.375 | **1.5** | **120** | Temp 동일. 2초/주기 @60tps |
| `turret_ground` / `turret_ceiling` | 0.5 × 0.5 | 0 | 1 | Temp 터렛. ~16×16px |
| `zako_fast` | 0.375 × 0.3125 | 0 | 1 | 소형 스웜 |
| `zako_tank` | 0.625 × 0.5 | 0 | 1 | 대형 탱커 |
| `zako_sine_slow` | 0.5 × 0.4375 | **2.0** | **180** | 넓은·느린 파형 (화면 점유) |
| `elite_sine` | 0.625 × 0.5 | **1.8** | **90** | 큰 히트박스 + 빠른 위빙 |

**무기 projectile hitbox**

| id | halfW × halfH | 비고 |
|---|---|---|
| `main_shot` | 0.25 × 0.09375 | Temp: U/4 × U×3/32. 8×3px 스프라이트 절반 |
| `missile` | 0.3125 × 0.1875 | 메인보다 두꺼운 미사일 잠정 |
| `option` / `shield` | 0 × 0 | 발사체 없음 플레이스홀더 |

#### 4) dropWeight 의미론 — **동의**

구현 계약 `P(drop) = dropWeight / (noDropWeight + dropWeight)` (단일 캡슐 타입)에 **동의**.

- 적별 `dropWeight`는 상대 보상 곡선 (잡졸 4–5 / 스웜 3 / 포탑 2 / 탱커·슬로 6–7 / 엘리트 12).
- `noDropWeight`는 전역 드롭 밀도 노브. 일괄 스케일 조정에 적합.
- 현재 경제 목표: 잡졸 킬 약 1/3 드롭 → `noDropWeight=8` + `dropWeight=4` 정합.
- **대안을 채택하지 않음.** 다중 아이템 테이블·스테이지 보정 계수는 캡슐 타입이 늘 때 schema v3+로 검토.
- `dropWeight: 0` → 드롭 없음 (Core early-return) — 유지.

#### CODEX 파서 참고

- 월드유닛 → 서브유닛: `half * SubUnitsPerWorldUnit` (256). `0.09375 * 256 = 24` exact.
- 속도: `scrollSpeed` u/s → num=`scrollSpeed*U`, den=`TicksPerSecond` (기존 moveSpeed/projectileSpeed와 동일 패턴).
- `amplitude`/`periodTicks`는 non-sine도 파싱·전달. sine 패턴이 아니면 런타임이 무시해도 됨.
- `spawnX`는 카탈로그 전역; `StageSegmentTemplate`/`SpawnEvent` 생성 시 모든 스폰에 동일 X 주입.
- `dropTable.noDropWeight` → `BattleSimConfig.CapsuleNoDropWeight` (콘텐츠 쪽 권위; Config 기본값 0 덮어쓰기).
- 구 `schemaVersion: 1` 파일은 본 필드 없음 — 로더는 v2 필수 또는 명시적 기본값 정책을 택할 것 (권장: v2 필수, 누락 시 검증 실패로 Core가 값을 발명하지 않게).

#### 잠정값 표시 (확정 금지)

- hitbox half extents 전 적/무기
- sine amplitude / periodTicks
- `scrollSpeed`, `spawnX`, `noDropWeight`
- 기존 HP·speed·dropWeight·세그먼트 밀도 등 (이전 응답과 동일)

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
