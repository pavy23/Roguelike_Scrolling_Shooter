# REQ-114 GROK 구현·검증 보고서 — 전함 함미 화면 밖 (좌표 수정)

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-111 (fortress warship 데이터) · RENDERER 발견 결함
- 결과: **PASS**

## 결론

| 축 | 처리 |
|---|---|
| 원인 | `boss_fortress` holdX **14** + engine.offsetX **8** → 함미 중심 **x=22** (플레이필드 우측 20 / BulletDespawnX 21 밖) |
| 수정 | holdX **14 → 12**, engine.offsetX **8 → 4** (멀티파트 보스 관례: leviathan/broodmother holdX 12, 최대 오프셋 ≤5) |
| 좌표 | 전 파츠(포탑 라인 포함) 히트박스 전체가 플레이필드 안 + 탄 컬링선 안 (아래 표) |
| HP·공격 | 변경 없음 (좌표만) |

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **532/532** |
| BalanceSim | **all green** (`CheckReq111WarshipAndGhost` 포함) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary 256) |

---

## 1. 결함 (before)

Core 멀티파트 경로: 파츠 월드 X = `bossX(holdX) + offsetX` (`BattleSim.RefreshBossPartPositions`).

| 상수 | 값 (월드유닛) |
|---|---:|
| `PlayfieldHalfWidth` | **20** (가시 우측 끝) |
| `BulletDespawnX` | **21** (플레이어 탄 컬링) |
| holdX (구) | 14 |
| engine.offsetX (구) | 8 |
| engine.halfWidth | 2.5 |

| part | 중심 X | 좌단 | 우단 | 가시 내 폭 | 탄 히트 가능 폭 |
|---|---:|---:|---:|---:|---:|
| **engine** | **22.0** | 19.5 | 24.5 | **0.5u** (19.5–20) | **1.5u** (19.5–21) |

히트박스 총 폭 5.0 중 왼쪽 **1.5u만** 탄에 맞고, 스프라이트 대부분은 화면 밖. RENDERER 관측과 일치.

---

## 2. 수정

파일: `GameData/waves.json` → `bosses[]` id=`boss_fortress`

| 필드 | before | after | 근거 |
|---|---:|---:|---|
| `holdX` | 14.0 | **12.0** | 멀티파트 관례 (`boss_leviathan` / `boss_broodmother`) |
| `parts[engine].offsetX` | 8.0 | **4.0** | 관례 최대 오프셋 ≤5, 요청 `engine.offsetX ≤ 4` |

기타 파츠 offset·half·HP·attack·warship 그룹 계약 **불변**.

함미 중심 after: **12 + 4 = 16**. 우단 18.5 ≤ 20 (가시) ≤ 21 (탄 컬링).

---

## 3. 좌표 검산 (hold 정지 후)

가정:

- 보스 본체 중심 = `holdX` (Y=0, 이동 패턴 오프셋 0)
- 파츠 중심 = `(holdX + offsetX, offsetY)`
- 히트박스 AABB = 중심 ± halfW/H
- 플레이필드 X ∈ [−20, 20], Y ∈ [−11.25, 11.25]
- 플레이어 탄 유효 X ≤ `BulletDespawnX` = 21  
  → 파츠 전체가 피격 가능 조건: **우단 ≤ 21** (이상적으로 우단 ≤ 20으로 스프라이트도 가시)

### 3.1 After (holdX=12)

| part | group | offsetX | offsetY | halfW | halfH | 중심X | 중심Y | 좌단 | 우단 | 하단 | 상단 | PF 안 | 탄 피격 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|
| engine | stern | 4.0 | 0.0 | 2.5 | 2.0 | **16.0** | 0.0 | 13.5 | **18.5** | −2.0 | 2.0 | ✅ | ✅ |
| turret_a | hull | 4.0 | 3.5 | 1.25 | 1.09375 | **16.0** | 3.5 | 14.75 | **17.25** | 2.406 | 4.594 | ✅ | ✅ |
| turret_b | hull | 4.0 | −3.5 | 1.25 | 1.09375 | **16.0** | −3.5 | 14.75 | **17.25** | −4.594 | −2.406 | ✅ | ✅ |
| turret_c | hull | 0.0 | 2.0 | 1.25 | 1.09375 | **12.0** | 2.0 | 10.75 | **13.25** | 0.906 | 3.094 | ✅ | ✅ |
| turret_d | hull | 0.0 | −2.0 | 1.25 | 1.09375 | **12.0** | −2.0 | 10.75 | **13.25** | −3.094 | −0.906 | ✅ | ✅ |
| core | bow | −6.0 | 0.0 | 2.0 | 2.0 | **6.0** | 0.0 | 4.0 | **8.0** | −2.0 | 2.0 | ✅ | ✅ |

- 최우측 우단: engine **18.5** (PF 20 대비 headroom **1.5u**, BulletDespawn 21 대비 **2.5u**)
- 최좌측 좌단: core **4.0** (플레이어 스폰 −13 대비 사거리 여유)
- Y 전체 |상/하| ≤ 4.594 ≪ 11.25
- **포탑 라인 4문 전부** 히트박스 전체가 플레이필드·탄 유효 구간 안

### 3.2 Before 대비 (engine만)

| | 중심X | 우단 | PF 내 폭 | 탄 피격 폭 / 전체 |
|---|---:|---:|---:|---|
| before | 22.0 | 24.5 | 0.5 / 5.0 | **1.5 / 5.0** |
| after | 16.0 | 18.5 | 5.0 / 5.0 | **5.0 / 5.0** |

### 3.3 레이아웃 메모

- holdX=12 + engine/turret_a·b offsetX=4 → 함미와 날개 포탑이 같은 X, Y만 분리 (함미 클러스터). 의도된 실루엣.
- 함체 축: core(6) → mid turrets(12) → stern cluster(16). 가로 스팬 중심 기준 10u.
- 본체 `halfWidth: 10` 우단 = holdX+10 = **22** (본체 박스 관측용; 피격은 파츠 단위). 파츠 피격 게이트와 무관. 필요 시 후속 폴리시.

### 3.4 WarshipEncounter 경로 메모

`WarshipEncounter` 월드 X = `originX + offsetX − scrollOffset` (originX=24 유지). REQ-112 전 BattleSim은 멀티파트 hold 경로가 실제 전투 좌표. 본 REQ는 hold 경로 결함만 수정. origin/scroll 정렬이 필요하면 CODEX 연동 후 별도 검산.

---

## 4. 검증

```text
cd Tools\CoreStandalone && dotnet test
→ 통과!  실패: 0, 통과: 532, 전체: 532

cd Tools\BalanceSim && dotnet run --project VerifyThemeAssembly.csproj
→ PASS: BalanceSim all checks green.
→ PASS: REQ-111 warship TTK + ghost review. (parts=6 sum=19600 유지)

cd Tools\DeterminismAudit && dotnet run --project . -- --suite
→ AUDIT PASS (6/6 scenarios + cap-boundary 256)
```

밸런스 수치(HP·TTK·ways) 불변 → REQ-111 게이트 수치 동일 (pureST 27.2s / wall 37.2s).

---

## 5. CLAUDE / 후속

- Presentation은 Core `BossParts` 좌표를 그리기만 함 → 데이터 수정만으로 함미가 화면 안으로 들어온다.
- `Assets/Resources/GameData/waves.json` 동기화는 CLAUDE 몫 (원본은 `GameData/`).
- WarshipEncounter scroll·origin 정합(REQ-112 연동 후)은 필요 시 재검산.

전부 잠정 좌표(AGENTS.md §7). 사람 플레이 피드백으로 hold/오프셋 미세 조정 가능.
