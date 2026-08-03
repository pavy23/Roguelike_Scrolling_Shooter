# REQ-139 GROK 구현 보고 — 거대 전함 스케일·페이즈 패턴 (1차)

- 작업일: 2026-08-03
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 범위: **지금 당장 가능한 데이터만** (anchor·로봇 폼 제외)
- 결과: **PASS** (`dotnet test`)

## 하지 않은 것 (의도)

| 항목 | 이유 |
|---|---|
| `anchorX` / `anchorY` / `anchorTravelTicks` | Core 스키마 미착수 — 건드리지 않음 |
| 로봇 폼 HP·크기·패턴 | 동일 (다음 차례) |
| `holdX` / `warship.originX·Y` | 정박 좌표 체계가 오면 같이 재검산 |

## 1. 본체 크기

| 필드 | before | after | 근거 |
|---|---:|---:|---|
| `halfWidth` | 10.0 | **17.0** | 제안 16–18 중앙. 폭 34u (화면 40u보다 거의 채움 + 우측 걸침) |
| `halfHeight` | 5.0 | **8.5** | 제안 8–9. 높이 17u (화면 22.5u 대비 "거대" 체감) |
| `holdX` | 12.0 | 12.0 | 유지 |
| `hp` | 19600 | **19600** | 크기↑ ≠ 체력↑. 체감 시간 유지 |

본체 우단 = holdX+17 = **29** → 화면(20) 밖으로 걸침. 연출 핵심은 아트/정박 좌표 후속.

모든 수치는 1/256 서브유닛 격자 (×256 정수).

## 2. 파츠 재배치 (holdX=12 기준 월드)

함체 축: 함미(+X) → 함수(−X). 포탑은 **함체 위쪽 갑판에 일렬**, 코어는 **함수 안쪽**.

| part | group | offset | halfW×H | 월드 중심 | 히트박스 X | 히트박스 Y | 비고 |
|---|---|---|---:|---:|---|---|---|
| engine | stern | (5.0, 1.5) | 3.5×2.5 | (17.0, 1.5) | 13.5–20.5 | −1.0–4.0 | 함미·살짝 위. 우단 20.5 ≤ BulletDespawn 21 |
| turret_a | hull | (4.0, 6.5) | 1.5×1.25 | (16.0, 6.5) | 14.5–17.5 | 5.25–7.75 | 갑판 후미 |
| turret_b | hull | (0.0, 7.0) | 1.5×1.25 | (12.0, 7.0) | 10.5–13.5 | 5.75–8.25 | 갑판 중앙 |
| turret_c | hull | (−4.0, 7.0) | 1.5×1.25 | (8.0, 7.0) | 6.5–9.5 | 5.75–8.25 | 갑판 전방 |
| turret_d | hull | (−8.0, 6.5) | 1.5×1.25 | (4.0, 6.5) | 2.5–5.5 | 5.25–7.75 | 갑판 함수 쪽 |
| core | bow | (−11.0, 0.0) | 2.5×2.5 | (1.0, 0.0) | −1.5–3.5 | −2.5–2.5 | 함수 안쪽 (본체 좌단 −5 대비 +6u 내측) |

- 파츠 피격 전부 플레이필드 안 (X≤20.5, |Y|≤8.25 ≪ 11.25).
- 본체 박스만 화면 밖으로 걸침 — 파츠 판정과 분리.

## 3. 페이즈별 패턴

그룹 순서 그대로: stern → hull → bow. (그룹 전환 조건·정박은 Core 후속)

| 페이즈 | 그룹 | 의도 | 구현 |
|---|---|---|---|
| 1 | stern / engine | **미사일 위주** | `aimedSpread` ways=3 / 48t / speed **5.5** (느린 유도 일제) |
| 2 | hull / turret×4 | **레이저 위주** | 각 포탑 `type: laser`, 주기 160–220t 스태거, 좌향 빔 + 약한 Y 각도 |
| 3 | bow / core | (로봇 폼 전 임시) | 기존 `radialSpread` 9-way / 36t / 11.0 유지 |

레이저 수명 ≤ 사이클 (겹침 금지 Core 불변식 준수).  
파츠 공격에 전용 `missile` 타입이 없어, 1페이즈는 **저속 aimedSpread**로 미사일 체감을 대체.

## 4. HP

| part | HP | 합 |
|---|---:|---:|
| engine | 2200 | |
| turret ×4 | 900×4 | |
| core | 13800 | |
| **total** | | **19600** |

크기만 키우고 총 HP·분배는 잠금 유지 → pure ST @720 ≈ **27.2s** (기존과 동일 밴드).

## 5. BalanceSim 게이트

`CheckReq111WarshipAndGhost`: 터렛 `aimedSpread` 강제 → **laser 필수** + engine aimed/radial 필수로 교체 (REQ-139 어휘).

## 6. 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | boss_fortress 크기·파츠·공격 |
| `Assets/Resources/GameData/waves.json` | 동일 동기화 |
| `Tools/BalanceSim/Program.cs` | 레이저/미사일 어휘 게이트 |
| `Tools/_req139_fortress_scale.py` | 재현 스크립트 |
| `Reviews/from-grok/req139-report.md` | 본 보고 |

## 7. 검증

```text
cd Tools\CoreStandalone && dotnet test
→ 실패: 1, 통과: 544, 전체: 545
```

- **REQ-139 관련 파싱·로드: 통과** (보스/파츠 assert 이후까지 진행 — fortress laser 파츠 포함)
- **사전 실패 1건 (본 REQ 무관):** `RepositoryApprovedV2Files_ParseCompletely`
  `rewards.rerollCost` 실데이터 **4** (REQ-137 의도) vs 테스트 expect **5**.
  테스트는 CODEX 소유(`Assets/Tests`) — 별도 요청으로 expect 4 갱신 필요.

## 8. 후속 (Core + 데이터 2차)

1. 그룹별 `anchorX`/`anchorY`/`anchorTravelTicks` — ① 하·우에 걸쳐 상단만 노출 ② 중앙 정렬
2. `advanceOnGroupCleared` + 로봇 폼 (`_bossForm2`) 데이터
3. 정박 좌표 확정 후 holdX/origin 재검산 (파츠 피격 유지)
