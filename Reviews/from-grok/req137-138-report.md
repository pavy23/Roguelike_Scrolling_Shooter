# REQ-137 / REQ-138 보고서 (GROK / CONTENT)

- 작업일: 2026-08-03
- 브랜치/worktree: `content` / `wt-content`
- 결과: **REQ-137 PASS** · **REQ-138 PASS** · BalanceSim **all green**

---

## REQ-137 — 파워업 캡슐 스폰율 ×0.7

### 1. 변경

| 항목 | 수정 전 | 수정 후 | 비고 |
|---|---:|---:|---|
| `enemies.json` `dropTable.noDropWeight` | **13** | **21** | 단일 노브로 전 적 p≈0.7× 근사 |
| `rewards.json` `rerollCost` | **5** | **4** | 캡슐 희소에 맞춘 리롤 경제 동조 (허용 4..6) |
| BalanceSim open-stage EV 밴드 | [10, 20] | **[7, 14]** | 의도적 0.7× 스케일 |

공식: `p = dropWeight / (noDropWeight + dropWeight)`  
개별 `dropWeight`는 유지. noDrop만 올리면 잡졸(저 w)이 중형/미니보스보다 조금 더 깎인다 (w=2 → 0.65×, w=26 → 0.83×). **가중 스테이지 open+close EV 합 비율은 정확히 0.700**.

### 2. 기대 캡슐 수 (테마 풀 · open 3 + close 5 = 8 segs)

| S | theme | open EV | open+close EV | vs 이전 |
|---:|---|---:|---:|---:|
| 1 | scrapyard | 6.27 | **16.71** | 0.678 |
| 2 | hive | 7.74 | **20.65** | 0.689 |
| 3 | fortress | 8.79 | **23.43** | 0.695 |
| 4 | nebula | 8.95 | **23.87** | 0.709 |
| 5 | core | 9.68 | **25.81** | 0.721 |
| **5스테이지 합** | | | **110.5** (was 157.8) | **0.700** |

BalanceSim 전역 가중: `E_caps/seg=2.90` · `E_stage(open 3)=8.71` (밴드 [7,14] 내).

### 3. 5스테이지 완주 가능성

| 지표 | 값 | 판단 |
|---|---|---|
| flat-1 exclusive full (Speed/Main/Missile/Option/Shield L6 + weapon mode L3) | ≈33 캡슐 | 런 EV 110 ≫ 33 |
| exclusive full에 필요한 스테이지 수 | ≈1.5 스테이지 | 여유 |
| S1 open EV → Main@mid | ≈L5 (BalanceSim) | mid TTK 게이트 PASS |
| S1 open+close → Main@boss | ≈L5 | stage1 clear PASS |
| 리롤/스테이지 (전부 리롤 시) | ≈1.8 (cost 4) | 밴드 [1.5, 3.5] PASS |

**결론: 5스테이지 완주 가능 — 위험 낮음.**  
캡슐이 파워업 유일 공급원이지만 flat-1 게이지 + open+close 8세그 구조상 런 EV가 여전히 exclusive full의 ~3.3배.  
후반 “풀파워 수집 욕구”는 줄고, **조기 올인 타이밍 압박**이 조금 늘 수 있다 (의도된 희소).

**소프트 우려 (적용 유지, 사람 판단 대기):**
1. 잡졸 p가 중형보다 더 깎여 early trickle이 얇아짐 — stage1 초반 Main 성장이 L6 직전이 아닌 L5 부근에 머물 수 있음.
2. 실플레이에서 미스/미회수가 늘면 후반 실드·옵션 부족 체감 가능 — PLAYTESTER 확인 권장.
3. 대안(미적용): noDrop 18(~0.79×) 완화, 또는 잡졸 dropWeight만 보정.

### 4. 변경 파일

- `GameData/enemies.json` — noDropWeight
- `GameData/rewards.json` — rerollCost
- `Tools/BalanceSim/Program.cs` — capsule EV 밴드
- `Tools/BalanceSim/_req137_138_verify.py`

---

## REQ-138 — 하이브 보스 재설계 (길쭉한 체형 + 다리 게이트)

### 1. 본체 판정

| | 수정 전 | 수정 후 | 아트 |
|---|---:|---:|---|
| halfWidth | 5.0 | **4.0** | torso 8u wide |
| halfHeight | 4.0 | **7.25** | torso 10u + legs 아래 연장 → full **8 × 14.5** |
| hp (총) | 14500 | **14500** | 불변 |
| holdX | 14.0 | 14.0 | 불변 |

### 2. 파츠 표

| id | offsetX | offsetY | halfW | halfH | hp | 무적 규칙 |
|---|---:|---:|---:|---:|---:|---|
| `tentacle_left` (다리 L) | **-2.25** | **-3.75** | **1.5** | **3.5** | **2500** | 항상 피격 가능 (p0 Legacy) |
| `tentacle_right` (다리 R) | **+2.25** | **-3.75** | **1.5** | **3.5** | **2500** | 동일 |
| `core` (머리) | **0.0** | **+5.5** | **2.5** | **1.75** | **9500** | **`coreGatePartIds`: 양 다리 파괴 전 무적** |

- 파츠 합 = 2500+2500+9500 = **14500** = boss hp.
- id는 `tentacle_*` 유지 (BalanceSim REQ-116 게이트 · 기존 phase partRules 호환).
- 다리 공격: 기존 `verticalMovement` 유지 (좌 180t / 우 200t).
- 페이즈: p0 `partVulnerability: legacy` (코어 게이트) 유지 · p2 partRules로 다리 비활성+무적 / 코어 노출 유지.

### 3. 아트 좌표 정합성 (보스 원점 = 실루엣 중심)

| 레이어 | 월드 y 구간 | 비고 |
|---|---|---|
| 본체 판정 | [-7.25, +7.25] | halfHeight 7.25 |
| torso 스프라이트 (8×10, 머리 위 정렬) | [-2.75, +7.25] | 상단 = 몸 상단 |
| 다리 스프라이트 (3×7, part offset) | **[-7.25, -0.25]** | center y=-3.75, hh=3.5 |
| 다리–몸 겹침 (엉덩이) | 2.5u | 자연 부착 |
| 머리 코어 판정 | **[+3.75, +7.25]** | center y=5.5, hh=1.75 |
| 실드 돔 6×6 (Presentation) | 머리 중심 부근 | 코어보다 약간 크게 덮음 |

다리 ox=±2.25 → 가로 span [0.75, 3.75] / [-3.75, -0.75], 몸 halfW=4.0 안쪽(바깥 가장자리와 0.25u 여유).

**Presentation 주의:**
- `boss_hive_torso.png`는 다리 없음 — 원점을 실루엣 중심에 두면 torso 중심은 y≈**+2.25** (몸 중심에서 위로). 스프라이트 앵커를 데이터 halfExtents 중심에 두면 다리가 몸에서 떨어져 보임 → **torso를 y=+2.25에 오프셋**하거나 아트 피벗을 실루엣 중심에 맞출 것.
- `boss_hive_leg.png`는 part 좌표(±2.25, -3.75)에 그리면 판정과 일치. 좌우는 flipX.
- `fx_shield_dome.png`는 core (0, 5.5) 또는 머리 중심에 오버레이. 게이트 해제 시 페이드아웃.

### 4. 스키마 / 결정론

| 항목 | 결과 |
|---|---|
| schemaVersion | **상향 불필요** (기존 parts / isCore / coreGatePartIds / halfExtents) |
| 결정론 해시 | **변동** — noDropWeight(드롭 스트림) + boss_hive 파츠 기하/HP |
| 신규 Core API | 불필요 (게이트·무적 구조 재사용) |

### 5. 부수 수정

- `seg_hive_brood_wave` spawn y 4개 (`±0.6`, `±1.8`)를 1/256 서브유닛 양자화 — **기존 파서 실패 원인** 수정 (BalanceSim 로드 차단 해제). REQ-131 편대 잔재.

### 6. 변경 파일

- `GameData/waves.json` — boss_hive + brood_wave y
- `Tools/BalanceSim/Program.cs` — HiveHalfW/H = 4.0 / 7.25
- `Tools/BalanceSim/_req137_138_verify.py`, `_fix_hive_brood_y.py`

---

## 검증

```
dotnet run --project Tools/BalanceSim/VerifyThemeAssembly.csproj -c Release
→ PASS: BalanceSim all checks green.
```

---

## 타 에이전트 요청 요약

### CLAUDE (Presentation)
1. Resources `GameData/*.json` 동기화
2. boss_hive 파츠 뷰: 다리 스프라이트 at tentacle offsets, torso 앵커, 실드 돔 at core, 다리 파괴 연출
3. (선택) 캡슐 드롭 희소 체감 캡처

### GEMINI
1. DeterminismAudit 베이스라인 갱신 (noDrop + hive parts)
2. 캡슐 EV 교차 검산 · hive 파츠 게이트 동작 확인

### 사람
1. REQ-137 0.7× + reroll 4 체감 확정
2. REQ-138 다리/실드 연출 아트 매핑 확인
