# REQ-157 GROK 구현 보고 — 전함 3페이즈 재배치 (갑판→함저→코어)

- 작업일: 2026-08-04
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 기반: main `c8aa255` merge (fast-forward)
- Core 선행(사람): 워프 제거(`SetAtHoldX` → `ScrollTowardHold`) + 소모전 스크롤 한계를 미래 파츠까지 확장
  - **주의**: 이 Core 수정은 main 작업 트리에 미커밋 상태로 남아 있었음. content에는 아직 없음.
- 결과: **데이터 PASS** · BalanceSim REQ-111 **PASS** · `dotnet test` **569/572** (아래 단언 3건 → CODEX)

## 하지 않은 것

| 항목 | 이유 |
|---|---|
| `Assets/Scripts`, `Assets/Tests` | 소유 밖. 깨진 단언은 §4에 보고 |
| 함저 포탑 새 스프라이트 | 사람: 기존 포탑 위아래 반전(뷰 처리) |
| Core "마지막 그룹 = 코어" 규칙 변경 | 사람 결정 — 건드리지 않음. P2/P3 역할 스왑으로 대응 |

## 1. 페이즈 맵 (사람 표 반영)

| 페이즈 | 그룹 | role | 공략 대상 | `anchorOffsetY` | `anchorTravelTicks` | 함체 연출 |
|---|---|---|---|---:|---:|---|
| 1 | `stern` | midbossGate | 엔진 + 갑판 포탑 a/b | **−4.5** | **0** | 아래 잠김, 갑판만 노출 |
| 2 | `hull` | attritionLine | 함저 강화 포탑 c/d | **+5.0** | **120** | 위로 상승, 함저가 눈높이 |
| 3 | `bow` | finalCore | 코어 | **0.0** | **120** | 중앙 정박으로 하강 |

이동 경로: **아래(−4.5) → 위(+5.0) → 중앙(0)**  
각 막의 공략 부위가 y≈0 기체 높이에 오도록 함체를 옮긴다. travel 120틱(2.0s @60fps) ∈ 사람 요구 90–150.

`advanceAfterTicks` (소모전 타이머): **720 → 600** (1막 필수 격파량 증가 상쇄).

## 2. 엔진 배치 근거

**1막(midbossGate)에 엔진 + 갑판 포탑을 함께 둠.**

1. 엔진 사각형은 REQ-143 기준 갑판 상부 구조물(offsetY=3.75) — 함저 막으로 옮기면 시각·판정 모두 어긋남.
2. 1막 연출 목표("아래에서 갑판만")와 같은 고도 밴드.
3. 고전 슈팅의 "함미/추진 파괴 = 오프닝 게이트" 문법 유지.
4. 대신 HP를 2200→1000으로 줄여 갑판 포탑 2문과 합쳐도 1막 총량이 폭증하지 않게 함 (아래 §4).

## 3. 좌표 근거 (1/256 격자 exact)

### 좌표계

- `originY = 0`, 파츠 월드 Y = `AnchorOffsetY + offsetY`
- 화면 세로 `±11.25` (`PlayfieldHalfHeight = 45/4`)
- 함체 `halfHeight = 8.5`

### 갑판 포탑 (유지, REQ-142)

| 파츠 | offsetX | offsetY | halfH | 비고 |
|---|---:|---:|---:|---|
| turret_a | +4.0 | **6.1875** | 1.25 | 갑판선@+4.9375 + halfH |
| turret_b | 0.0 | **6.6875** | 1.8125 | 갑판선@+5.4375 근처 |

### 함저 포탑 (신규 배치)

사람 실측 용골선(스냅 1/256):

| x | 용골 Y (실측→격자) |
|---:|---:|
| −4 | −6.62 → **−6.625** (−1696/256) |
| −8 | −5.81 → **−5.8125** (−1488/256) |

배치 규칙: **파츠 바닥이 용골선에 닿음** (`offsetY = keelY + halfHeight`)  
→ 함체 안쪽 하부 포문. (완전히 용골 아래로 달면 P1 앵커에서 화면 밖으로 떨어짐)

| 파츠 | offsetX | keel | halfH | offsetY | halfW |
|---|---:|---:|---:|---:|---:|
| turret_c | −4.0 | −6.625 | 1.5 | **−5.125** | 1.75 |
| turret_d | −8.0 | −5.8125 | 1.5 | **−4.3125** | 1.75 |

### 막별 피격 가능 검증

| 막 | A | 활성 파츠 | 중심 Y | 박스 | 화면 내 | y=0 교차 |
|---|---:|---|---|---|---|---|
| P1 | −4.5 | engine | −0.75 | [−2.75, +1.25] | ✓ | ✓ |
| P1 | −4.5 | turret_a | +1.6875 | [+0.44, +2.94] | ✓ | (근접, 위로 조준) |
| P1 | −4.5 | turret_b | +2.1875 | [+0.38, +4.0] | ✓ | (근접) |
| P2 시작(A1) | −4.5 | turret_c | −9.625 | [−11.13, −8.13] | ✓ | — |
| P2 시작 | −4.5 | turret_d | −8.8125 | [−10.31, −7.31] | ✓ | — |
| P2 정박 | +5.0 | turret_c | −0.125 | [−1.63, +1.38] | ✓ | ✓ |
| P2 정박 | +5.0 | turret_d | +0.6875 | [−0.81, +2.19] | ✓ | ✓ |
| P3 | 0 | core | −3.0 | [−8.5, +2.5] | ✓ | ✓ |

함체 실루엣:

- P1: Y∈[−13.0, +4.0], 화면 아래 잠김 **1.75u** (갑판·상부 위주)
- P2: Y∈[−3.5, +13.5], 화면 위 돌출 **2.25u** (함저 위주)
- P3: Y∈[−8.5, +8.5] 중앙 정박

## 4. HP 배분 (총 19600 고정)

| 파츠 | 전 | 후 | 역할 |
|---|---:|---:|---|
| engine | 2200 | **1000** | P1 게이트 일부 |
| turret_a/b (갑판) | 900×2 | **700×2** | P1 게이트 |
| turret_c/d (함저) | 900×2 | **1200×2** | P2 강화 |
| core | 13800 | **14800** | P3 |
| **합** | 19600 | **19600** | |

- P1 필수 격파: 1000+700+700 = **2400** (구 engine-only 2200 대비 +200)
- 소모전 타이머: 720→**600** (−2.0s)
- 이론 wall-clock @reach720: warn3.0 + gate3.3 + attr10.0 + core20.6 ≈ **36.9s** (구 ≈37s 근방)

### 함저 "더 센" 패턴

| | 갑판 a/b (참고) | 함저 c | 함저 d |
|---|---|---|---|
| HP | 700 | **1200** | **1200** |
| laser cycle | 180–200 | **140** | **148** |
| sustain | ~40–56 | **72** | **80** |
| damage | 1 | **2** | **2** |
| fullHalfWidth | 0.75 | **1.0** | **1.0** |
| half hitbox | 1.5×1.25 | **1.75×1.5** | **1.75×1.5** |

사이클 합 ≤ `cycleIntervalTicks` (파서 non-overlap 규칙 준수).

## 5. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | boss_fortress 파츠·그룹·앵커·HP |
| `Assets/Resources/GameData/waves.json` | 동일 동기화 (런타임 로드) |
| `Tools/BalanceSim/Program.cs` | REQ-157 게이트 (그룹 구성·HP 밴드·앵커·travel) |
| `Reviews/from-grok/req-157-warship-three-phase.md` | 본 보고 |

## 6. 검증

```text
cd Tools\CoreStandalone && dotnet test
→ 통과 569 / 실패 3 / 전체 572
```

| 결과 | 내용 |
|---|---|
| BalanceSim REQ-111 | **PASS** (wall≈36.9s, hp=19600, parts=6) |
| Req119 autofire | **PASS** (engine 여전히 y=0 직사 가능) |
| 파서/스키마 | **PASS** (laser non-overlap 포함) |

### 깨진 단언 3건 (CODEX 요청)

원인: midbossGate가 이제 `['engine','turret_a','turret_b']` — **engine만 파괴해도 막이 안 넘어감**.

| 테스트 | 파일:줄 | 기대 | 실제 |
|---|---|---|---|
| `RepositoryGameDataRunManagerFortressBossActivatesDamageableStern` | Req117…:164 | ActiveGroupIndex=1 | 0 |
| `RepositoryGameDataShuffledFortressWarshipIsAttachedDeterministically` | Req117…:164 | 동상 | 0 |
| `RepositoryFortressWarshipKeepsDamageablePartsInPlayfieldForEntireEncounter` | Req118…:75 | ActiveGroupIndex=1 | 0 |

**제안 수정** (CODEX): midbossGate 전 파츠를 녹인 뒤 그룹 전환을 단언하거나, `SternPartId` 단일 가정 대신 그룹 partIds를 순회 파괴.

```csharp
// 스케치: 활성 그룹의 모든 파츠 MaxHp 샷
foreach (var partId in encounter.Groups[0].PartIds)
    damage full HP;
Assert.AreEqual(1, activeGroupIndex);
```

Req119는 engine 단건 피격만 보므로 **수정 불필요**.

## 7. 후속

1. CODEX: 위 3 테스트 갱신 → 572/572
2. CLAUDE/뷰: 함저 포탑 스프라이트 상하 반전 (사람 인수)
3. PLAYTESTER: 막 전환 이동이 "천천히" 보이는지, 함저 레이저 압박 체감
4. 사람: 워프 수정 Core 커밋·병합 (main 작업 트리 uncommitted)
