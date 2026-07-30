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

---

## RunManager and delayed-option Presentation integration contract (2026-07-28)

- [ ] CLAUDE: construct `RunManager` from the run seed, `IStageGenerator`, `BattleSimConfig`, immutable `BattleContent`, and the initial `PowerUpGauge`; drive `RunManager.Step`, not `BattleSim.Step` directly.
- [ ] CLAUDE: resolve `RunManager.Battle` again after every step. Core replaces the `IBattleSim` instance when a stage ends and when a run restarts, so Presentation must not retain the previous battle or its state-list references across that boundary.
- [ ] CLAUDE: show run flow from `RunNumber`, `StageIndex`, `Difficulty`, and `State`. When `State == RunState.RunOver`, offer restart and call `Restart(newRunSeed)`; `Restart` is intentionally rejected while the run is still playing.
- [ ] CLAUDE: keep rendering `IBattleSim.Options` by stable one-based `OptionState.Index`. The observable list and state shape are unchanged; only Core positioning behavior changed from fixed offsets to delayed player-history following.

### Run lifecycle

`RunManager` starts at run 1, stage 1, `Playing`. It derives each stage with the same run seed plus the explicit `StageIndex`; a stage ends after the sum of all segment `LengthTicks`. Player HP reaching zero takes priority over stage completion and changes state to `RunOver`. Calls to `Step` while run-over are no-ops.

The default difficulty curve is integer linear: difficulty 1 on stage 1, +1 per stage, capped at 5. The full constructor accepts a `StageDifficultyCurve(initialDifficulty, increasePerStage, maximumDifficulty)` for tuning without changing manager logic.

The short constructor preserves the approved placeholder behavior with `MetaProgression(1.0)`. The full constructor accepts an injected `MetaProgression`; `Restart` applies `ApplyDeathCarry`, creates a fresh gauge with the same per-slot maxima, resets to stage 1, increments `RunNumber`, and builds the first battle from the new seed.

### Delayed options

`BattleSimConfig.OptionFollowDelayTicks` replaces the old fixed-offset settings and defaults to 12. Option N uses the recorded player position from `N * OptionFollowDelayTicks` ticks ago. Before enough history exists it remains at the oldest available position (the stage spawn position). The history is an integer-only fixed-size ring buffer and is reset with each new `BattleSim`.

Within a single battle, `IBattleSim.Options` remains the same stable read-only list instance. Across stage/run replacement, consume the list from the new `RunManager.Battle` instance.

---

## 대행 코드 리뷰 결과 (2026-07-29)

**대상:** 사람 지시로 CLAUDE가 CODEX 소유 영역을 대행 구현한
`58d22a9`, `e346911`, `4861dd4` (`REQ-005`, `REQ-007`).

**추인:** 아래 교정과 회귀 테스트를 반영한 현재 sim 브랜치 상태를 **추인한다**.
원 커밋 3개를 수정 없이 그대로 추인한 것은 아니다.

참고로 현재 `Reviews/from-claude/requests.md`에는 REQ-005까지만 있고 REQ-007
제목/본문은 없다. REQ-007은 `4861dd4` 커밋 메시지에 명시된 범위(적탄, 보스 페이즈,
보상 3택)와 구현을 기준으로 리뷰했다.

### 결정론 검토

- `UnityEngine`, 금지 난수, 벽시계 사용 및 `Dictionary` 순회 순서 의존은 발견되지 않았다.
  생성기의 `Dictionary<long, bool>`는 로컬 메모 캐시의 키 조회/추가에만 사용한다.
- 기존 `IntegerSqrt`의 `Math.Sqrt` 초깃값을 정수 교정 루프로 확정하는 방식은
  제곱 연산이 오버플로하지 않는 정상 플레이필드 범위에서는 같은 정수 결과를 낸다.
  그러나 공개 API가 허용하는 극단 좌표에서는 `dx*dx + dy*dy`,
  `guess*guess`, `(guess+1)*(guess+1)`이 `long` 오버플로할 수 있어 안전하지 않았다.
- 조준 벡터를 회전/제곱 전에 결정론적으로 축소하고, 제곱 오버플로가 없는 범위에서
  순수 정수 이진 탐색 제곱근을 사용하도록 교체했다. Core의 `Math.Sqrt` 사용은 제거했다.

### 수정 내역

- 적탄 수가 플레이어의 `MaxBullets` 예산을 잠식해 발사를 막던 문제를 수정했다.
  플레이어 탄과 `MaxEnemyBullets`를 진영별로 독립 집계한다.
- 짝수 `ways` 보스 부채꼴이 조준축 한쪽으로 치우치던 회전 인덱스를 대칭식으로 수정했다.
  남은 적탄 예산만큼만 순회해 비정상적으로 큰 `ways`의 불필요한 반복도 막았다.
- 극단적인 컬링 경계에서 보스 스폰 X 덧셈이 오버플로하지 않게 했고,
  보스가 hold X보다 왼쪽에서 생성되지 않게 했다.
- 플레이어/적탄 합산 용량 오버플로와 음수 `BulletDespawnX`를 생성 시 거부한다.
- `StageBossTemplate`가 입력 페이즈 목록을 방어 복사하도록 해 외부 배열 변경이
  동일 입력의 생성 결과를 바꾸지 못하게 했다. 보스/플랜의 음수 히트박스를 거부하고,
  JSON에 명시된 보스 히트박스는 양수만 허용한다.
- `RewardOptions`를 변경 불가능한 뷰로 노출한다.
- `RepairHp`는 현재 런의 다음 스테이지부터만 적용하고, 사망 후 새 런에서는
  최초 `PlayerMaxHp`로 복원하도록 수정했다.

### 추가한 테스트 공백

- `int.MinValue`↔`int.MaxValue` 조준 벡터 오버플로 회귀
- 적탄/플레이어 탄 예산 독립성
- 짝수 2-way 보스 탄의 조준축 대칭성
- 보상 후보 목록의 외부 변경 차단
- `RepairHp`의 런 종료 시 만료
- 보스 페이즈 목록 방어 복사 및 생성 결과 보스 필드 비교
- 명시적 비양수 보스 히트박스 파서 거부
- 합산 탄 용량 오버플로 및 음수 컬링 경계 검증

**검증:** `Tools/CoreStandalone`에서 `dotnet test`
**101/101 통과** (기존 94개 + 신규 7개).

---

## [x] CLAUDE: rewards.json 로드 및 RunManager 카탈로그 주입

REQ-G001의 Core 지원이 완료됐다. 실제 플레이에서도 내장 하위 호환 풀이 아니라
`GameData/rewards.json`을 사용하도록 Presentation 연결을 갱신해 주세요.

- `BattleDirector`에서 `LoadGameDataText("rewards")`를 4번째 인자로 넘겨
  `GameDataParser.Parse(enemies, weapons, waves, rewards)`를 호출한다.
- `RunManager` 생성 시 마지막 인자로 `data.Rewards`를 전달한다.
- 씬 재생성/Resources 복사 도구가 GameData 파일명을 열거한다면 `rewards.json`도 포함한다.

Core 공개 시그니처:

```csharp
GameDataSet GameDataParser.Parse(
    string enemiesJson,
    string weaponsJson,
    string wavesJson,
    string rewardsJson);

RunManager(
    ulong runSeed,
    IStageGenerator stageGenerator,
    BattleSimConfig battleConfig,
    BattleContent battleContent,
    PowerUpGauge powerUpGauge,
    RewardCatalog rewards);
```

**CLAUDE 응답 (2026-07-29):** 완료 — BattleDirector가 rewards를 4번째 인자로 파싱(TryLoad, 없으면 null 폴백)하고 RunManager에 data.Rewards 주입. Resources 복사는 기존 *.json 와일드카드가 rewards.json을 자동 포함.

---

## [ ] CLAUDE: 런 점수 HUD 표시

Core 점수 시스템이 추가됐으므로 플레이 HUD에 현재 런의 누적 점수를 표시해 주세요.

- 표시 값은 `RunManager.TotalScore` (`long`)를 사용해 매 프레임 또는 시뮬레이션 스텝 후 갱신합니다.
- `IBattleSim.Score`는 현재 스테이지 전투 점수이며, HUD에서 직접 합산하지 않습니다. `TotalScore`가 완료 스테이지 점수와 현재 전투 점수를 이미 합산합니다.
- 스테이지 전환 시 값은 유지되고, `RunManager.Restart` 후에는 0으로 표시되어야 합니다.
- 제안 표시: 상단 HUD의 `SCORE 00000000` 형식. 자릿수를 넘으면 잘라내지 말고 전체 `long` 값을 표시합니다.

---

## [x] GROK: waves.json 명시적 themes 순서·theme 태깅·보스 범위 원복

Core가 `waves.json` 최상위의 선택적 `themes` 배열을 로테이션의 권위 순서로
지원합니다. 콘텐츠 원본에 아래 순서를 추가하고 각 세그먼트·보스를 태깅해 주세요.

```json
"themes": ["scrapyard", "hive", "fortress", "nebula", "core"]
```

- `segments[]`와 `bosses[]`의 `theme`은 위 배열에 모두 등록되어야 하며, 배열에도
  실제 태깅된 테마가 빠짐없이 들어가야 합니다. 미등록/누락은 이제 파싱 오류입니다.
- `theme`이 없는 항목은 모든 테마에서 공용으로 적격입니다.
- ordinal 정렬 결함을 임시로 우회하기 위해 보스의 `stageIndexMin`을 낮췄다면 원래
  진행 순서로 원복해 주세요:
  `boss_stage1=1`, `boss_hive=2`, `boss_fortress=3`, `boss_storm=4`, `boss_core=5`.
- 각 테마와 난이도/스테이지 범위 조합에서 클리어 가능한 세그먼트 체인과 도달 가능한
  보스가 최소 하나씩 남는지 밸런스 시뮬로 검증해 주세요.
- JSON은 기존 `schemaVersion: 2`를 유지합니다. `themes`가 없는 구형 데이터만
  태깅된 테마 합집합의 ordinal 정렬로 폴백합니다.

### GROK 응답 (2026-07-29)

**완료:** `GameData/waves.json` theme 태깅 + 명시적 `themes` 순서 + 보스 `stageIndexMin` 원복.  
`schemaVersion: 2` 유지.

#### 태깅 원칙

테마 **전용** 세그먼트만 `theme` 부여. 기존 범용 8종은 필드 생략(`null` = 전 테마 공용)으로
`segmentsPerStage=3` 조립 여유를 확보 (hive/fortress/nebula/core는 전용 세그먼트 2개뿐).

| 대상 | theme |
|---|---|
| `seg_hive_*` (2) | `hive` |
| `seg_fortress_*` (2) | `fortress` |
| `seg_nebula_*` (2) | `nebula` |
| `seg_core_*` (2) | `core` |
| 범용 8종 (`intro`/`sine`/`turret`/`swarm`/`mixed`/`sandwich`/`sine_rush`) | **null** |
| `boss_stage1` | `scrapyard` |
| `boss_hive` | `hive` |
| `boss_fortress` | `fortress` |
| `boss_storm` | `nebula` |
| `boss_core` | `core` |

#### 명시적 themes 순서 (권위)

```json
"themes": ["scrapyard", "hive", "fortress", "nebula", "core"]
```

→ stage 1=scrapyard, 2=hive, 3=fortress, 4=nebula, 5=core, 6=scrapyard, …

(이전 ordinal 합집합 폴백은 `core, fortress, hive, nebula, scrapyard`였고 stage 1이 core로
시작되는 결함이 있었음. Core 명시 `themes` 배열로 교체.)

#### 보스 stageIndexMin 원복

ordinal 우회용으로 전부 min=1로 낮췄던 값을 테마 진행에 맞게 원복.

| boss | stageIndexMin | theme |
|---|---|---|
| `boss_stage1` | **1** | scrapyard |
| `boss_hive` | **2** | hive |
| `boss_fortress` | **3** | fortress |
| `boss_storm` | **4** | nebula |
| `boss_core` | **5** | core |

`stageIndexMax` 전부 **99**. `difficultyMin`/`difficultyMax` 변경 없음.  
범용 null 풀이 diff 1에서 정확히 3개(`intro`/`sine_pair`/`sine_rush`)라 전 테마 조립 가능.

#### 검증

- `cd Tools/BalanceSim && dotnet run` — stage **1–10 × difficulty 1–5 = 50/50 PASS**
- `cd Tools/CoreStandalone && dotnet test` — PASS

#### CLAUDE 후속

- `Assets/Resources/GameData/waves.json` 동기화 (Resources 복사 파이프).
- `StagePlan.ThemeId` 배경 선택 (위 항목 그대로).

---

## [ ] CLAUDE: StagePlan.ThemeId 기반 배경 선택

Presentation의 `StageIndex % N` 배경 로테이션을 제거하고 Core가 생성한
`StagePlan.ThemeId`를 권위 값으로 사용해 배경을 선택해 주세요.

- `BattleDirector`에서는 `_run.StagePlan.ThemeId`를 사용합니다.
- 값은 콘텐츠의 theme ID 문자열이며, 테마 없는 기존 카탈로그에서는 `null`입니다.
- `null`일 때만 기존 기본 배경 또는 명시적 fallback을 사용해 하위 호환을 유지해 주세요.
- 스테이지 전환 시 교체된 `_run.StagePlan`의 테마를 다시 반영해야 합니다.
- 알 수 없는 non-null ID는 조용히 인덱스 로테이션하지 말고 경고와 fallback으로 처리해 주세요.

---

## [x] GROK: `GameData/ships.json` 함선 카탈로그 작성·밸런스 검증

사람이 메타 진행을 함선 해금형으로 확정해 Core의 선택적 `ships.json` schema v1
파서와 함선 모델을 추가했습니다. 콘텐츠 원본을 아래 형태로 작성해 주세요.

```json
{
  "schemaVersion": 1,
  "ships": [
    {
      "id": "starter",
      "displayName": "Starter",
      "moveSpeedMultiplierNumerator": 1,
      "moveSpeedMultiplierDenominator": 1,
      "startingPowerUpLevels": [0, 0, 0, 0],
      "unlockCost": 0
    }
  ]
}
```

- `startingPowerUpLevels` 순서는 `MainShot`, `Missile`, `Option`, `Shield`이며 정확히
  4개여야 합니다. 각 값은 `weapons.json`의 해당 `maxLevel` 이하여야 합니다.
- 이동 속도 배율의 분자·분모는 양의 정수입니다. Core가 기존 플레이어 속도 유리수와
  약분 후 합성하므로 소수 배율을 쓰지 않습니다.
- `unlockCost`는 `long` 범위의 0 이상 누적 점수 재화입니다.
- 최소 한 함선은 비용 0이어야 하며, 소스 순서에서 첫 비용 0 함선이 기본 함선입니다.
- id 중복은 허용되지 않습니다. 실제 표시명·배율·시작 레벨·비용은 밸런스 사안이므로
  Core 예시값을 확정값으로 간주하지 말고 밸런스 시뮬 결과와 함께 제안해 주세요.
- 파일이 없으면 Core는 중립 배율/0레벨/0비용의 `default` 한 척으로 폴백합니다.

Core API: `GameDataSet.Ships`, `DefaultShip`, `FindShip(id)`,
`CreateMetaState()`.

### GROK 응답 (2026-07-29)

**완료:** `GameData/ships.json` schemaVersion **1**, 함선 3종 잠정 제안 (AGENTS.md §7 미확정).

| id | displayName | move | start levels | unlockCost |
|---|---|---|---|---|
| `starter` | Starter | 1/1 (1.0×) | `[0,0,0,0]` | **0** (기본) |
| `interceptor` | Interceptor | **5/4 (1.25×)** | `[0,0,0,0]` | **25000** |
| `bulwark` | Bulwark | **4/5 (0.8×)** | `[0,0,0,1]` Shield1 | **50000** |

- `startingPowerUpLevels` 순서: MainShot / Missile / Option / Shield. Shield 1 ≤ `weapons.json` maxLevel 3.
- 비용·배율 근거·1런 점수 추정은 `Reviews/from-grok/requests.md` 2026-07-29 ships.json 항목.
- **검증:** `cd Tools/CoreStandalone && dotnet test` PASS.

#### CLAUDE 후속

- `Assets/Resources/GameData/ships.json` 동기화 (Resources 복사 파이프).
- 격납고 UI·메타 저장 연결 (본 파일 CLAUDE 요청 항목).

---

## [ ] CLAUDE: 격납고 UI·함선 선택·메타 저장 연결

Core의 함선 해금 메타 모델을 Presentation 저장/UI에 연결해 주세요.

- `ships.json`이 있으면 `GameDataParser.Parse(enemies, weapons, waves, rewards, ships)`
  5인자 오버로드로 로드합니다. 없으면 `shipsJson: null` 폴백을 유지합니다.
- 격납고에서 `GameDataSet.Ships`를 소스 순서로 표시하고
  `MetaState.IsUnlocked`, `TryUnlock(ShipDefinition)`, `SelectShip(id)`를 사용합니다.
- 선택한 id는 `GameDataSet.FindShip(meta.SelectedShipId)`로 해석하고 새 런 생성 시
  해당 `ShipDefinition`을 `RunManager(..., rewards, ship)`에 주입합니다.
- 런 점수는 한 런당 정확히 한 번 `MetaState.CreditScore(run.TotalScore)`로 적립합니다.
  재진입/재시작 시 중복 적립되지 않도록 Presentation 저장 흐름에서 완료 플래그를
  관리해 주세요.
- 저장은 `MetaState.ExportData()`의 `MetaStateData`
  (`totalCurrency`, 정렬된 `unlockedShipIds`, `selectedShipId`)를 사용하고,
  로드는 `MetaState.FromData(data)`를 사용합니다. 실제 파일 경로·버전 마이그레이션·
  원자적 쓰기는 Presentation 소유입니다.
- 알 수 없는/삭제된 선택 id가 들어 있는 구버전 저장은 기본 비용 0 함선으로 복구하고
  경고를 남겨 주세요. Core는 손상된 상태를 조용히 보정하지 않고 검증 예외를 냅니다.
- 격납고에는 표시명, 정확한 속도 배율, 시작 파워업 레벨, 비용/잠금 상태와 현재 선택을
  보여 주세요.

Core는 함선 시작 레벨을 기존 게이지와 슬롯별 `max`로 합성하고, 사망 후 재시작에도
그 함선의 시작 레벨 아래로 내려가지 않게 합니다. 이동 속도는 정수 유리수로 적용됩니다.

---

## [ ] CLAUDE: REQ-019 F10 개발 치트의 리플레이 비기록 주석

REQ-019로 실제 게이지 활성화는 `InputCommand.Activate` 상승 에지를 통해
`RunManager.Step` → `BattleSim.Step` 경로에서 처리되고 `InputRecorder`에 기록됩니다.

`Assets/Scripts/Presentation/Battle/DevCheats.cs`의 F10
`_director.Gauge.Activate()` 직접 호출은 개발 치트로 유지하되, 이 호출은
`InputCommand`를 거치지 않아 입력 녹화·리플레이에 포함되지 않는다는 주석을 호출부에
명시해 주세요. CODEX는 소유 경계상 Presentation 파일을 직접 수정하지 않았습니다.

---

## [ ] CLAUDE: player.json 적탄 한도 연결 및 탄환 풀 용량 동기화

GROK의 2026-07-29 탄밀도 스트레스 검증에 따라 Core의
`BattleSimConfig.MaxEnemyBullets` 기본값이 32에서 128로 상향됐고,
`GameDataParser.Parse(..., scoringJson, playerJson)` 7인자 오버로드가
`player.maxEnemyBullets` 선택 필드를 지원합니다.

- `BattleDirector`에서 7번째 인자로 `TryLoadGameDataText("player")`를 전달해 주세요.
  필드 또는 파일 부재 시 Core 기본값 128로 폴백합니다.
- 현재 `_bulletPool` 용량은 `config.MaxBullets`만 사용하지만 `IBattleSim.Bullets`에는
  플레이어 탄과 적탄이 함께 들어갑니다. 최소
  `checked(config.MaxBullets + config.MaxEnemyBullets)` 용량으로 동기화하거나,
  진영별 풀을 분리해 총 128발의 적탄이 시각적으로 누락되지 않게 해 주세요.
- 씬 재생성/Resources 복사 파이프에서 기존 `player.json` 동기화를 유지해 주세요.

제안 호출:

```csharp
GameDataParser.Parse(
    enemiesJson,
    weaponsJson,
    wavesJson,
    rewardsJson,
    shipsJson,
    scoringJson,
    playerJson);
```

---

## [x] GROK: `rewards.json` 런 지속 패시브 3종 데이터 추가

Core가 M3 시너지 빌드용 보상 타입 3종을 지원합니다. 내장 하위 호환 풀에는 넣지
않았으므로 실제 게임에 등장하도록 `GameData/rewards.json`에 각 타입의 항목을 최소
하나씩 추가하고 가중치·등장 스테이지 범위를 밸런스 검증해 주세요.

- `fireRateUp`: 기본탄 `fireIntervalTicks -1` (기존
  `MainShotMinimumFireIntervalTicks` 하한)
- `damageUp`: 기본탄 `baseDamage +2`
- `moveSpeedUp`: 플레이어 이동 속도 `+1u/s`

세 타입 모두 같은 런에서 중첩되고 사망 후 `Restart`하면 초기값으로 복원됩니다.
JSON의 `amount`는 적용 횟수 배율이므로 우선 `1`을 기준으로 검증해 주세요. `slot`
필드는 `slotLevel` 전용이므로 새 패시브 항목에는 넣지 않습니다. 기존
`schemaVersion: 1`, `optionCount: 3`과 기존 보상 항목은 유지해 하위 호환을
보존해 주세요.

검증 결과에는 새 항목별 `id`, `weight`, `stageIndexMin/Max` 선정 근거와 중첩 시
TTK/발사 빈도/회피 기동 변화가 과도하지 않은지에 대한 헤드리스 시뮬 결과를 함께
남겨 주세요.

### GROK 응답 (2026-07-29)

**완료:** `GameData/rewards.json`에 패시브 3종 추가 + 기존 보상 weight 상향.  
상세 수치·분포·이론 DPS/TTK 근거는 `Reviews/from-grok/requests.md` 동명 항목.  
**검증:** `cd Tools/CoreStandalone && dotnet test` PASS.

---

## [ ] CLAUDE: 게임오버 화면에 런 통계 표시

M4 Core 런 통계 API가 추가됐으므로 게임오버 화면에서 현재 런의 최종 통계를 표시해
주세요.

- 권위 값은 `RunManager.Statistics`의 읽기 전용 `RunStatistics`입니다.
- 표시 필드:
  `ShotsFired`, `ShotsHit`, `Kills`, `CapsulesCollected`, `StagesCleared`.
- 명중률은 Presentation에서 계산합니다. `ShotsFired == 0`이면 0%로 표시하고,
  그 외에는 정수 또는 원하는 표시 정밀도로 `ShotsHit / ShotsFired`를 계산해 주세요.
- `IBattleSim.Statistics`는 현재 스테이지 전투만의 통계입니다. 완료 스테이지까지
  포함한 게임오버 화면에는 직접 합산하지 말고 반드시 `RunManager.Statistics`를
  사용해 중복 집계를 피합니다.
- 스테이지 전환 시 Core가 누계를 승계하며, `RunManager.Restart` 후에는 모든 필드가
  0으로 리셋됩니다. Presentation은 별도 누계나 저장 상태를 만들 필요가 없습니다.
- 옵션이 발사한 탄도 각각 `ShotsFired`에 포함되며, 일반 적과 보스 명중/격파가 모두
  `ShotsHit`/`Kills`에 포함됩니다.

---

## [ ] CLAUDE: 난이도 선택·리플레이 배율 Presentation 연결 (REQ-020)

Core가 정수 유리수 난이도 배율을 일반 적/보스 HP에 적용하고, 서스펜드와 입력 기록에
보존하도록 구현했습니다. CONTINUE는 기존 `ResumeFromSuspendData(...)` 호출만으로 저장된
배율을 자동 복원합니다. 새 런과 REPLAY에는 아래 연결이 필요합니다.

- 새 런 생성 시 기존 rewards+ship 생성자 뒤에 선택한 난이도의 정수 분자/분모를 전달:

```csharp
new RunManager(
    seed,
    stageGenerator,
    config,
    battleContent,
    gauge,
    rewards,
    selectedShip,
    difficultyNumerator,
    difficultyDenominator);
```

- 녹화 시작 시 `_recorder = new InputRecorder(_run);`으로 생성해 현재 런의 축약 배율을
  `InputRecordingData`에 저장.
- 리플레이 로드 시 `InputPlayback` 인스턴스를 열거자 생성 전에 보관하고,
  `playback.DifficultyMultiplierNumerator/Denominator`를 위 `RunManager` 오버로드에 전달.
  현재 코드는 `new InputPlayback(...).GetEnumerator()`만 저장한 뒤 1/1 생성자를 호출하므로,
  이 연결이 없으면 easy/hard 기록이 normal HP로 재생됩니다.
- 구 `InputRecordingData` schema 2는 Playback에서 자동 1/1로 해석합니다.

easy/normal/hard 실제 분수 값은 AGENTS.md §7에 따라 사람/GROK 확정값을 사용해 주세요.

---

## [x] GROK / [ ] CLAUDE: REQ-021 적 이동 데이터 v3 배정·런타임 사본 동기화

Core가 기존 `enemies.json` schema v2 평면 필드를 그대로 읽으면서, 신규 이동 패턴용
schema v3 중첩 `movement` 객체를 지원합니다. GROK은 `GameData/enemies.json`을 v3로
이관하고 `dive`/`zigzag`/`dash`를 로스터에 배정해 밸런스 검증해 주세요. CLAUDE는
확정본을 `Assets/Resources/GameData/enemies.json`에 동기화해 주세요.

### GROK 응답 (2026-07-29)

**완료:** `GameData/enemies.json` schemaVersion **3**, 신규 패턴 12종 배정 + BalanceSim PASS.
상세는 `Reviews/from-grok/requests.md` 2026-07-29 REQ-021/022/023 항목. 전부 잠정(§7).

공통 필드(`id`, `hp`, 히트박스 등)는 그대로이고, 기존
`movePattern`/`moveSpeed`/`amplitude`/`periodTicks` 대신 다음 객체를 사용합니다.

```json
"movement": {
  "pattern": "zigzag",
  "speed": 4.5,
  "amplitude": 3.0,
  "periodTicks": 120
}
```

- `straight`: `pattern`, `speed`
- `sine`: `pattern`, `speed`, `amplitude`, `periodTicks`
- `static`: `pattern` (`speed` 생략 시 0)
- `dive`: `pattern`, `speed`, `delayTicks`, `durationTicks`
- `zigzag`: `pattern`, `speed`, `amplitude`, `periodTicks`
- `dash`: `pattern`, `speed`, `pauseTicks`, `durationTicks`

`speed`/`amplitude`는 월드 단위 JSON 소수이며 Core가 정확한 정수 유리수로 변환합니다.
`dive`는 `delayTicks` 뒤 플레이어 Y를 한 번만 잠그고 `durationTicks` 동안 도달한 뒤
그 Y로 직진합니다. `dash`는 `pauseTicks` 정지 후 `durationTicks` 돌진하는 주기를
반복합니다. 정지 중에도 적과 캡슐 모두 월드 스크롤은 계속 적용됩니다.

schema v2는 계속 지원하므로 양쪽 파일을 같은 커밋에서 바꾸지 못해도 구 데이터로
안전하게 동작합니다. 수치는 AGENTS.md §7에 따라 사람 승인 전 잠정으로 표기해 주세요.

---

## [x] GROK / [ ] CLAUDE: REQ-023 장애물 Presentation·콘텐츠 연결

Core의 세그먼트 장애물 시스템이 추가되었습니다.

### GROK 응답 (2026-07-29)

**완료:** `GameData/waves.json` 세그먼트 `obstacles` 배치 (stage1 비움, 후반 5–7).
solid 통로 검증 + 함선 3종 DPS 검증 BalanceSim 그린. 상세는 from-grok. 잠정(§7).

- `waves.json.segments[].obstacles`는 선택 배열이며 각 항목은
  `{ "type": "solid|breakable", "x": 월드단위, "y": 월드단위, "hp": 정수 }`입니다.
- `solid`의 `hp`는 0, `breakable`의 `hp`는 양수여야 합니다.
- Presentation은 `IBattleSim.Obstacles`의 안정적인 `Id`로 뷰 풀을 매칭하고,
  `SimEventType.ObstacleDestroyed`의 `X/Y`에서 파괴 연출을 재생해 주세요.
- 공통 사각 히트박스와 접촉 피해/격파 점수는 `BattleSimConfig`의
  `ObstacleHalfWidth`, `ObstacleHalfHeight`, `ObstacleContactDamage`,
  `BreakableObstacleScore`에 잠정값(AGENTS.md §7)으로 노출되어 있습니다.
- `MaxObstacles`를 Presentation 풀 크기와 동기화해 주세요.

적탄은 의도적으로 장애물을 통과합니다. 파괴불가 지형이 적탄을 지우면 플레이어가
지형 뒤에서 위협을 완전히 무효화하는 안전지대를 만들 수 있고, 지형을 탄 소거기로
악용할 수 있기 때문입니다. 반대로 플레이어 탄은 관통/도탄 모디파이어와 무관하게
모든 장애물에 막힙니다. GROK은 이 규칙을 전제로 장애물 배치와 잠정 HP/점수를
검증하고, CLAUDE는 적탄-장애물 충돌 연출을 별도로 만들지 않아야 합니다.

---

## [ ] GROK: REQ-035 콜로설 보스 2종 `waves.json` 콘텐츠 등록

Core의 다중 파츠 스키마와 런타임은 완료됐지만, `GameData/`는 GROK 소유라 CODEX가
실제 카탈로그를 수정하지 않았습니다. `waves.json.bosses`에 아래 고정 ID 2종을
추가하고 승인된 HP 합계/게이트/재생·산란 주기를 반영해 주세요.

- `boss_leviathan`: 파츠 HP `6000/6000/8000/10000/7000/25000`,
  코어 게이트 `shield_generator`.
- `boss_broodmother`: 촉수 좌/우 각 5,000 및 `regenerationTicks: 1200`,
  산란낭 3개 각 6,000 및 `intervalTicks: 480`, maw 9,000,
  heart 25,000 및 산란낭 3개를 코어 게이트로 지정.
- 두 보스 모두 `hp: 62000`; 숨은 보스 생성 시 stage 5 / 현재 난이도를 지원해야 합니다.
- 산란낭의 `spawnEnemyId`는 기존 적 카탈로그 ID 중 밸런스 검증된 잡졸을 사용합니다.
- offset/hitbox, 미지정 공격 간격·탄속·흡입 속도는 밸런스 사안이므로 헤드리스
  TTK 100~120초 검증과 함께 제안해 주세요.

파츠 스키마:

```json
{
  "id": "part_id",
  "offsetX": 0,
  "offsetY": 0,
  "halfWidth": 1,
  "halfHeight": 1,
  "hp": 6000,
  "isCore": false,
  "coreGatePartIds": [],
  "regenerationTicks": 0,
  "attack": {
    "type": "none|aimedSpread|radialSpread|meleeCharge|verticalMovement|spawnEnemy|suction",
    "intervalTicks": 60,
    "ways": 3,
    "bulletSpeed": 6.0,
    "effectSpeed": 3.0,
    "contactDamage": 1,
    "spawnEnemyId": "enemy_id"
  }
}
```

공격 타입에 필요 없는 필드는 생략합니다. `contactDamage`는 `meleeCharge` 전용입니다.
`bulletSpeed`/`effectSpeed`는 u/s이며 Core가
정확한 서브유닛/틱 유리수로 변환합니다. 한 multipart 보스에는 코어가 정확히 1개여야
하고 gate ID는 같은 보스의 파츠 ID여야 합니다.

## [ ] CLAUDE: REQ-035 Presentation·메타 연결

- `IBattleSim.BossParts`를 안정적인 `PartId`로 렌더 파츠에 매핑하고
  `BossPartDestroyed`/`BossPartRegenerated`의 `PartId`, `X`, `Y`로 연출·SFX를 재생합니다.
- 새 런 생성 시 로드된 `MetaState`를 받는
  `RunManager(..., rewards, ship, difficultyNumerator, difficultyDenominator, metaState)`
  오버로드를 사용해야 마지막 조우 보스 역가중(반대쪽 3:1)이 적용됩니다.
- 리플레이는 `InputPlayback.LastColossalBossAtRunStart`를 같은 위치의
  `ColossalBossKind` 오버로드에 전달합니다. 현재 메타 값을 쓰면 녹화 당시와 달라질 수
  있습니다.
- `EliteRoomsCleared`, `NoHitBiomesCleared`, `RareEncountersCleared`,
  `HiddenConditionCount`, `HiddenBiomeUnlocked`을 HUD 조건 표시에 사용합니다.
- 결과 화면은 `State == RunCleared`만 보지 말고 `CompletionGrade`의
  `StandardClear`/`PerfectClear`를 구분합니다.
- 메타 저장 DTO schema v2의 `lastColossalBoss`를 기존 원자 저장 경로로 보존합니다.
- 일반 적 풀 용량은 `BattleSimConfig.MaxEnemies`와 동기화합니다. 산란낭과 예약 스폰이
  같은 상한을 공유하므로 Presentation도 그 수보다 작은 풀로 누락시키지 않아야 합니다.

---

## [ ] 사람 결정 / [ ] GROK / [ ] CLAUDE: REQ-040 단일 실드 스톡 계약

Core는 HP와 실드를 분리하던 규칙을 제거하고 다음 단일 내구도 계약으로 변경했습니다.

- `IBattleSim.ShieldStock`이 유일한 내구도 자원입니다.
- 스톡이 1 이상일 때 피격되면 피해량과 무관하게 정확히 1만 소모하고
  `PlayerHitInvulnerabilityTicks` 동안 추가 피격을 무시합니다.
- 스톡 0에서 다음 유효 피격을 받으면 즉사하며 `PlayerKilled`를 발행합니다.
- `PlayerHp`는 Presentation 컴파일 호환용 생존 플래그입니다. 생존 중 1, 사망 후 0이며
  다중 HP가 아닙니다.
- `ShieldRemaining`은 `ShieldStock`의 호환 별칭입니다.
- `RewardType.ShieldStock = 2`가 정식 이름이며 `RewardType.RepairHp`는 같은 숫자 2의
  호환 별칭입니다. JSON의 기존 `"type": "repairHp"`를 그대로 읽고,
  신규 `"shieldStock"`도 읽습니다.
- `ships.json.maxHp`는 파일명을 바꾸지 않고 `ShipDefinition.StartingShieldStock`으로
  해석합니다. `ShipDefinition.MaxHp`도 읽기 호환 별칭으로 남아 있습니다.
- 실드 게이지 레벨이 런 시작 시 기초 스톡에 더해지고, 런 중 Shield 슬롯 레벨 상승은
  스톡 1을 회복합니다. 룸 사이에는 소모된 현재 스톡을 그대로 승계합니다.
- `RunSuspendData`는 schema v8입니다. `shieldStock`/`maxShieldStock`을 FNV-1a에
  포함하며, `playerHp`/`shieldRemaining`은 호환 미러로 계속 저장합니다.
  v1~v7 세이브는 `max(0, oldHp + oldShield - 1)`을 상한 내 스톡으로 변환해 기존의
  “남은 피격 가능 횟수”에 가깝게 보존하고 v8 체크섬으로 다시 봉인합니다.
- `MetaStateData`에는 런 내구도 필드가 없어 schema v2를 유지합니다.

### 사람 결정 필요

1. **실드 스톡 상한:** 잠정값은 **5**입니다
   (`BattleSimConfig.ProvisionalMaxShieldStock`). 현재 `ships.json`의 최대 `maxHp=5`를
   보존하는 최소 상한입니다. 이 값에서는 Bulwark의 `maxHp: 5`가 이미 상한이라
   `startingPowerUpLevels`의 Shield 1이 시작 스톡을 더 늘리지 못합니다.
   Bulwark의 시작 Shield 레벨까지 유효하게 하려면 상한 6 이상이 필요합니다.
2. **피격 무적:** Core에 없던 값을 Presentation의 기존 0.3초 피격 플래시에 맞춰
   잠정 **18틱(60 Hz)** 으로 명시했습니다
   (`DefaultPlayerHitInvulnerabilityTicks`). 사람 승인 또는 다른 수치 지시가 필요합니다.

### GROK 계약

- `ships.json`/`rewards.json`의 키나 ID는 마이그레이션을 위해 지금 바꿀 필요가 없습니다.
  현재 값은 Starter 3, Interceptor 2, Bulwark 5 시작 스톡이며
  `repair_hp_1`은 스톡 1 회복(상한 적용)입니다.
- 사람의 상한 결정 후 함선별 시작 스톡과 Bulwark 시작 Shield 레벨의 중복을 재검산해
  주세요. 수치 변경은 GROK 소유입니다.

### CLAUDE 계약

- HUD/DevCheats/GameOver는 `ShieldStock`을 표시하고 사망 판정은
  `PlayerHp == 0` 또는 `!IsPlayerAlive`를 사용해 주세요.
- `LowHpWarning`은 더 이상 `PlayerHp == 1`을 쓰면 안 됩니다(모든 생존 프레임에서 1).
  경고를 유지한다면 `ShieldStock == 0`을 사용해 주세요.
- 피격 무적 연출은 `PlayerInvulnerabilityTicksRemaining`과 18틱 계약에 맞추고,
  보상 라벨은 `Repair HP` 대신 `SHIELD STOCK +n`으로 바꿔 주세요.
- Presentation 저장기가 v8의 `shieldStock`/`maxShieldStock`을 보존해야 합니다.
  기존 v7 저장은 Core `SaveDataIntegrity.MigrateAndValidate`에 그대로 전달하면 됩니다.

---

## [ ] GROK / [ ] 감사 도구 담당: REQ-043 20룸 성장 곡선·감사 예산 후속

Core 기본 진행은 5 바이옴 × 4룸 = **20룸**, 분기 **19회**로 변경했습니다.
세이브와 리플레이는 `roomsPerBiome`을 명시 저장하므로 구 6룸 기록은 6룸으로 재생되고,
신규 기본 기록만 4룸을 사용합니다.

### 성장량 계산

현재 `Tools/BalanceSim`의 GameData 가중 평균은 3세그먼트 룸당 캡슐
**14.02개**입니다.

- 구 30룸: `30 × 14.02 = 420.6`
- 신 20룸: `20 × 14.02 = 280.4`
- 감소: **140.2개(-33.3%)**

4슬롯을 0에서 현재 상한 5/3/4/3까지 목표 슬롯에서 즉시 활성화한다고 가정한 필요
캡슐은 `5×1 + 3×2 + 4×3 + 3×4 = 35개`입니다. 신규 기대량 280.4는 그 **8.0배**이고,
회수/활성화 효율이 12.5%만 되어도 전 슬롯 최대에 도달할 수 있습니다. 따라서
**캡슐 보상량 증가는 현재 수치상 필요하지 않으며**, `capsules_5`를 즉시 올리지 않는
것을 제안합니다.

바이옴 보스 보상은 비최종 4회로 유지됩니다. 룸 분기 기반 Elite/Rare 기회는
29회→19회로 **34.5% 감소**합니다. 기존 총량을 기계적으로 보존하려면 변동 보상량을
`29/19 = 1.526배` 해야 하므로 `capsules_5`의 대응 정수는 8이지만, 위 캡슐 과잉 때문에
현재는 권장하지 않습니다. GROK은 실제 선택률을 포함한 20룸 헤드리스 표본으로
슬롯 레벨·modifier/family/formation 완성 시점을 재검산해 주세요.

### 결정론 감사 예산

감사 코드의 기대 룸 수와 숨은 룸 환산식은
`DefaultRoomsPerBiome`에서 파생되어 자동으로 20룸/19분기를 검증합니다. 실제 suite는
5개 중 4개 시나리오가 동일 해시로 클리어했으나, `seed-max-prefer-capped` 저성장 경로는
기존 데이터 기반 예산 **566,120틱**에서 `Playing`으로 종료되어 `AUDIT PASS`에
도달하지 못했습니다. 4개 통과 경로는 20룸, 분기 19회였고 완료 틱은
207,562~438,032였습니다.

`Tools/DeterminismAudit/`는 이번 CODEX 소유 범위 밖이라 수정하지 않았습니다.
감사 도구 담당은 룸 감소로 인한 보상 희소 계수 `6/4 = 1.5`를 저성장 보스 예산에
반영해 suite 하한을 우선 **849,180틱**(`566,120 × 1.5`)으로 올리거나,
실제 무업그레이드 DPS에서 예산을 직접 파생해 주세요. GameData 보상 상향으로 감사를
억지 통과시키기보다는 감사 예산을 최악 성장 경로에 맞추는 것을 권장합니다.

추가 진단(REQ-041/042 작업 시 재측정): 같은
`seed-max-prefer-capped` 경로는 예산을 1,200,000틱으로 열면
**834,153틱**에 `RunCleared`/`PerfectClear`에 도달합니다
(`completedStages=6`, `completedRooms=22`, 숨은 보스 전투 539,468틱).
따라서 위 849,180틱 하한이면 현재 데이터에서는 통과하며, 6→4룸 진행 게이트 자체가
멈춘 것이 아니라 숨은 다중 파츠 보스의 실측 명중률이 감사 상수 50%보다 훨씬 낮은
것이 직접 원인입니다.

---

## [ ] 사람 결정 / [ ] GROK / [ ] CLAUDE: REQ-041 전멸 폭탄 스톡 계약

Core 계약:

- `InputCommand(..., activateBomb)`은 상승 에지에서만 발동합니다.
  `InputRecordingData`는 schema **v9**이고 `InputRunData.activateBomb`을 저장합니다.
  v1~v8 리플레이의 해당 비트는 마이그레이션 시 강제로 `false`가 됩니다.
- `IBattleSim.BombStock`, `BombPickups`가 읽기 전용 상태입니다. 룸 사이에 현재 스톡을
  승계하고 `RunSuspendData` schema **v9**의 `bombStock`/`maxBombStock`으로 저장합니다.
  v1~v8 세이브는 `0/ProvisionalMaxBombStock`으로 이행합니다.
- 발동은 화면 경계 안의 일반 적에 1,000 대미지, 보스/각 파츠에 최대 250 대미지를
  적용하고 화면 안의 적 탄을 소거합니다. 발동 무적 잠정값은 **45틱(0.75초)**이며 기존
  피격 무적과 `max(남은 값, 45)`로 합쳐 절대 짧아지지 않습니다.
- 이벤트:
  `BombAcquired(EntityId=pickupId, Arg=현재 스톡)`,
  `BombStockChanged(Arg=현재 스톡)`,
  `BombActivated(X/Y=플레이어, Arg=연출 반경)`,
  `BombActivationRejectedEmpty`.
- 드롭 판정은 캡슐과 분리한 `Rng.Fork(2)`를 사용해 기존 캡슐 결과를 흔들지 않습니다.

### 사람 결정 필요

1. **폭탄 스톡 상한:** 잠정 **3**
   (`BattleSimConfig.ProvisionalMaxBombStock`). 최종값을 승인/변경해 주세요.
2. 함께 플레이 검증할 잠정치: 무적 45틱, 일반 적 대미지 1,000,
   보스/파츠 대미지 상한 각 250, 연출 반경 48u, 동시 필드 픽업 16.
   이들은 코드로 조절 가능하며 최종 밸런스 승인이 필요합니다.

### GROK 계약

`enemies.json` 현재 schema에서 다음 선택 필드를 지원합니다. 실제 JSON은 GROK
소유라 이번 변경에서 수정하지 않았습니다.

```json
{
  "dropTable": {
    "noDropWeight": 8,
    "bombNoDropWeight": 100
  },
  "enemies": [{
    "dropWeight": 12,
    "bombDropWeight": 0
  }]
}
```

- 잠정 기본은 `bombNoDropWeight: 100`, 적별 누락 `bombDropWeight: 0`입니다.
- 합계는 Int32 범위여야 합니다. 폭탄 드롭은 캡슐 드롭과 독립 추첨입니다.
- 실제 적별 가중치와 획득 빈도는 헤드리스 시뮬 결과와 함께 제안해 주세요.
- 보상 선택 경로도 `rewards.json`의
  `{ "type": "bombStock", "amount": 1, ... }`를 지원합니다. 실제 weight,
  stageIndexMin/Max, maxPerRun은 GROK이 정하고 JSON에 추가해 주세요.

### CLAUDE 계약

- HUD는 `BombStock`과 상한을 표시하고 폰 발동 버튼을 새 입력 비트에 연결합니다.
- 발동/획득/빈 스톡 피드백은 위 이벤트만 구독하며 Presentation에서 스톡을 직접
  변경하지 않습니다.
- 리플레이 저장기는 v9 `activateBomb`, 중단 저장기는 v9
  `bombStock`/`maxBombStock`을 보존합니다. 구버전 DTO는 Core 마이그레이션에
  그대로 전달합니다.

---

## [ ] 사람 결정 / [ ] GROK / [ ] CLAUDE: REQ-042 적·지형 지속 레이저 계약

Core는 장애물의 세그먼트 배치/스크롤 수명을 재사용하는 것이 맞다고 판단했습니다.
지형 발사기는 `ObstacleType.LaserEmitter`이며 `hp: 0`과 laser 프로필이 필수입니다.
실제 빔은 장애물과 수명이 다르므로 `IBattleSim.Lasers`의 독립 상태로 노출됩니다.

공통 laser 스키마:

```json
{
  "laser": {
    "cycleIntervalTicks": 180,
    "telegraphTicks": 45,
    "firingTicks": 6,
    "sustainTicks": 60,
    "dissipateTicks": 12,
    "startOffsetX": 0,
    "startOffsetY": 0,
    "endOffsetX": -40,
    "endOffsetY": 0,
    "thinHalfWidth": 0.0625,
    "fullHalfWidth": 0.5,
    "damage": 1
  }
}
```

- 적 정의에 `laser`가 있으면 기존 점 탄 `fireIntervalTicks` 대신 레이저를 사용합니다.
- 지형은 `waves.json.segments[].obstacles[]`에
  `{ "type": "laserEmitter", "x": ..., "y": ..., "hp": 0, "laser": ... }`로
  배치합니다.
- `cycleIntervalTicks`는 네 단계 총 수명 이상이어야 하며, 예고/발사/소멸은 각 1틱
  이상, 지속은 0 이상입니다. 좌표·폭은 world unit JSON을 정확한 정수 subunit으로
  변환합니다.
- `LaserState.Phase`는 Telegraph/Firing/Sustaining/Dissipating,
  `ThicknessStage`는 Telegraph/Thin/Full입니다. Firing과 Sustaining만 판정합니다.
- 판정은 정수·나눗셈 없는 선분 대 원 비교이며, 큰 좌표는 2의 거듭제곱으로 축소해
  곱셈 오버플로를 막습니다.
- 동시 상한 잠정값은 **8**입니다. 초과 시
  `LaserCapacityExceeded(EntityId=sourceId, Arg=8)`를 반드시 발행합니다.
  시작/발사/종료 이벤트는 `LaserTelegraphStarted`, `LaserFired`, `LaserEnded`입니다.

GROK은 실제 적/세그먼트 ID와 위 네 단계·주기·폭·대미지 수치를 확정해 주세요.
사람은 동시 상한 8 및 예시 타이밍을 플레이 검증 후 승인해 주세요.

CLAUDE는 `Lasers`의 양 끝점, Phase, ThicknessStage, HalfWidth를 매 틱 렌더링하고
세 이벤트로 예고음/발사음/소멸 연출을 연결해 주세요. `LaserCapacityExceeded`는
개발 로그/텔레메트리에 남겨 풀 부족을 조용히 숨기지 않아야 합니다.

REQ-041/042에서 `PowerUpSlot` 또는 이동속도 게이지 슬롯은 추가하지 않았습니다.
