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
