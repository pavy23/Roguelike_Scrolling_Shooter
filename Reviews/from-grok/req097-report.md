# REQ-097 GROK 구현·검증 보고서

- 작업일: 2026-08-01
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 사람 피드백: *「메인샷 파워업은 레벨 6까지 되게 해줘, 지금 5까지야.」*
- 결과: **PASS** (MainShot 축 5→6; 무기 진화 levels[] 6단은 Core 하드캡으로 CODEX 요청)

## 결론

함선 게이지 **SHOT(MainShot)** 공유 파워 축을 `maxLevel` **5→6**으로 확장했다.
캡슐 1 = 레벨 +1 평탄 비용은 유지. 기체 간 ST DPS 비율 게이트(≤2.25)와 기존 BalanceSim 전부 green.

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **485/485** |
| BalanceSim | **all green** |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary) |

---

## 1. 변경 요약

### `GameData/weapons.json`

| 필드 | 이전 | 확정 |
|---|---:|---:|
| `weapons[main_shot].maxLevel` | 5 | **6** |
| `weapons[main_shot].effectSoftCapLevel` | 5 | **6** |

- 함선 6칸 게이지의 MainShot 슬롯 maxLevel은 파서가 `main_shot.maxLevel`에서 복사한다
  (`GameDataSet.CreatePowerUpGauge(ship)` — MainShot 전용 정의).
- `powerUpCostCurve` / 슬롯 costCurve: **baseCost 1, linear/quadratic 0** 유지 → costToMax = **6**.
- 7슬롯 카탈로그(`powerUpGauge.slots`)에는 MainShot을 넣지 않음 — Core가 hidden shared axis로 거부 (REQ-083 계약 유지).

### 손대지 않은 것

| 항목 | 이유 |
|---|---|
| Double / Laser / Triple `maxLevel` 3 | 무기 **모드 진화** 축 (REQ-088). 사람 요청은 MainShot 5→6 |
| `primaryWeaponFamilies[].levels[]` L4–L6 | **Core 하드캡 3단** — 아래 CODEX 요청 |
| 적 HP / 캡슐 드롭 | L6 추가만으로 게이트 붕괴 없음 (full-power 소폭 상승, 밴드 내) |

---

## 2. L6 수치 감각 (MainShot 공유 축)

`Damage.Compute`: base × (100 + 50×(level−1)) / 100  
연사: rapidStart=2, reduction=1/level, min interval = main_shot 기본 절반(4t @ vulcan 8t)

| Main L | dmg (base10) | interval | ST DPS |
|---:|---:|---:|---:|
| 1 | 10 | 8 | 75.0 |
| 2 | 15 | 7 | 128.6 |
| 3 | 20 | 6 | 200.0 |
| 4 | 25 | 5 | 300.0 |
| 5 | 30 | 4 (min) | 450.0 |
| **6** | **35** | **4 (min)** | **525.0** |

- **L6/L5 = 1.17×** — 연사는 L5에서 이미 min 포화, **데미지 축**으로 명확한 상승.
- BalanceSim 게이트: L6/L5 ∈ **[1.10, 1.40]** PASS.
- 계열 정체성(더블/레이저/트리플 패턴·진화)은 **모드 슬롯 L1–L3**이 담당; MainShot L6은 전 계열 공통 화력 천장.

### 기체 baseline ST 비율 (L0 vulcan, 기존 게이트)

| 기체 | sim DPS @180t | 비고 |
|---|---:|---|
| starter / interceptor / bulwark | ≈73.3 | 전원 vulcan 출발선 |
| max/min | **1.00** | band ≤ **2.25** PASS |

Family L0 ST max/min = **2.08** (≤2.25) 유지.

---

## 3. 경제 / 성장

| 지표 | 값 |
|---|---:|
| MainShot cost L0→L6 (flat-1) | **6** |
| Stage1 open capsule EV | ≈8.55 |
| Open+close EV | ≈17.1 |
| 추정 Main@mid (open only) | L5 (EV 소진 후 잔여로 L6 미달 가능) |
| 추정 Main@boss (open+close) | L5 근처 — **L6은 의도적 추가 투자** |

L6은 “한 방 더” 천장이지, stage1 기본 성장 경로의 자동 포화는 아니다.

---

## 4. Core 하드캡 조사 (소유 밖 — 수정 안 함)

### MainShot max 5 하드캡: **없음**

- `PowerUpGauge` / 파서는 `weapons[].maxLevel` 수치를 그대로 사용.
- `effectSoftCapLevel` legacy fallback만 MainShot=5 (schema에 softCap 명시 시 무시).
- 이번 데이터 변경만으로 max 6 동작 (테스트 485 통과로 확인).

### 무기 진화 `levels[]` 6단: **Core 하드캡 3**

| 위치 | 제약 |
|---|---|
| `PrimaryWeaponLevelDefinition` ctor | `level < 1 \|\| level > 3` |
| `PrimaryWeaponFamilyDefinition.CopyAndValidateLevels` | entries 1..**3** |
| `GameDataParser.ParsePrimaryWeaponLevels` | `levels[]` length > 2 거부, level must be 2 then 3 |

→ content만으로 진화 단계를 6까지 넣을 수 없다. **CODEX 요청**으로 남김.

---

## 5. BalanceSim 게이트 추가 (content 소유 Tools)

- 함선 MainShot: `maxLevel == 6`, flat-1, `costToMax == 6`
- `main_shot` 무기: `maxLevel == 6`, `effectSoftCapLevel == 6`
- MainShot L1–L6 비감소 + L6/L5 ∈ [1.10, 1.40]

---

## 6. 검증 로그 (요약)

```
dotnet test → 통과! 실패:0 통과:485
BalanceSim → PASS: BalanceSim all checks green.
  MainShot L6/L5=1.17 · ship ST ratio=1.00
DeterminismAudit --suite → AUDIT PASS (6 scenarios + cap-boundary)
```

---

## 7. 후속 요청 (requests.md 동기)

### CLAUDE
1. `Assets/Resources/GameData/weapons.json` 동기화 (`main_shot` maxLevel/softCap 6)
2. HUD SHOT 슬롯이 max 6을 표시·포화하는지 확인

### CODEX (선택 — 진화 6단이 기획 의도일 때만)
1. 주무기 진화 levels 상한을 3→6으로 완화 (위 표 3곳)
2. 완화 후 content가 double/laser/spread L4–L6 패턴을 채울 수 있음

**해석 메모:** 사람 문장 “지금 5까지야”는 MainShot 축(당시 max 5)과 일치. 진화 모드는 이미 max 3이라 “5”와 맞지 않음. 진화 6단이 별도 기획이면 CODEX 선행 후 content 2차 작업.
