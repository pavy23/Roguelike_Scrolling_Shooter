# REQ-116 GROK 구현·검증 보고서 — 보스 리디자인 데이터 전면 재작성

- 작업일: 2026-08-02
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 선행: REQ-115a (페이즈 게이트·2형태) · REQ-115b (체인·흡입) · `hidden-boss-anchors.md`
- 결과: **PASS**

## 결론

| 보스 | 변경 요지 | HP |
|---|---|---:|
| **boss_stage1** | 이동만 3단 차별화 (호버→돌진 왕복→광폭 사인) · 크기·HP 유지 | 4,250 |
| **boss_hive** | 5×4 · 하단 촉수 2본 · 코어 게이트(촉수) · p1 본체 개방 | 14,500 |
| **boss_fortress** | **불변** (전함 warship) | 19,600 |
| **boss_storm** | 5×4 · p2 룡 1기 체인 · p3 룡 2기+낙뢰 밀도 | 20,000 |
| **boss_core** | form1 28k 유지 + **form2 프리즘 14k** (전환 180t) | 28,000+14,000 |
| **boss_leviathan** | 앵커 10파츠 · 3막 50%/20% · holdX 9 · half 9.8×10 | 62,000 |
| **boss_broodmother** | 앵커 7파츠 · 흡입+재생 · 3막 · holdX 9 · half 9.25×9.3 | 62,000 |

| 검증 | 결과 |
|---|---|
| `dotnet test` (CoreStandalone) | **545/545** |
| BalanceSim | **all green** (`CheckReq116BossRedesign` 신규) |
| DeterminismAudit `--suite` | **AUDIT PASS** (6/6 + cap-boundary 256, seed-7 PerfectClear) |

---

## 1. St1 boss_stage1 — 이동 교육만

| 페이즈 | 이동 | 탄막 (유지) |
|---|---|---|
| p0 | **legacyHover** | aimed 64t · 3-way · 9.0 |
| p1 | **lungeReturn** amp 6 / 120t tel 24 | aimed 44t · 4-way · heavy + scrapThrow |
| p2 | **verticalSine** amp 3.5 / 72t | aimed 24t · 2-way · splitter + scrapThrow |

- half 4×3 · holdX 14 · HP **4250** (REQ-088 사람 잠금) 불변.
- threat mono 유지 (BalanceSim).

## 2. St2 boss_hive — Dobkeratops 촉수

| 항목 | 값 |
|---|---|
| halfExtents | **5×4** |
| parts | tentacle_left/right 2000 each · core 10500 (gate=촉수 2본) |
| p0 | Legacy coreGate — 촉수 파괴 시 본체 노출 (소프트락 방지: partRules 무적 오버라이드 없음) |
| p1 @66.7% | partVulnerability **all** — 촉수 잔존 시에도 본체 개방 |
| p2 @33.3% | 촉수 비활성 · 코어 광폭 radial/splitter |

## 3. St4 boss_storm — 번개룡 체인

| 페이즈 | 체인 | 낙뢰 |
|---|---|---|
| p0 | 없음 | spiral 오프너 |
| p1 @66.7% | **summonCount 1** · 7절 · headHp 1400 | heavy + lightning laser |
| p2 @33.3% | **summonCount 2** · 8절 · headHp 1600 | 밀도 최대 (int 10t) |

- half **5×4**. hitRule `headOnly` (REQ-115b).

## 4. St5 boss_core — 2형태

| 형태 | id | HP | 패턴 |
|---|---|---:|---|
| form1 | boss_core | **28,000** | 기존 3페이즈 prism (임계 명시) |
| form2 | **boss_core_prism** | **14,000** | spiral 프리즘빔 강화 → radial 혼합 · figureEight→sine |
| 전환 | — | — | **transitionTicks 180** (3s) |

- 총 전투 HP 42,000 = form1×1.5. Ghost L1 보너스 점유율 게이트 유지 (≤12% of St5 reach).
- form1 TTK@1050 ≈ 26.7s + form2 ≈ 13.3s + 전환 3s ≈ **43s** (클로징 고스트와 병행 시 보너스 only).

## 5. Leviathan 3막 (앵커 단일 소스)

| 항목 | 값 |
|---|---|
| holdX | **9.0** |
| halfWidth / halfHeight | **9.8 / 10.0** |
| 파츠 | turret_spine · head_cowl · blade×2 · rear_engine(+8.4 비대칭) · lower_launcher · shield_emitter · railgun · rib_gate · core |

### 막별 HP 분배 (총 62,000)

| 막 | 임계 | 영구 경로 HP | 파츠 |
|---|---|---:|---|
| 1 외갑 | 100%→50% | **31,000** | spine/cowl/blade×2/engine/launcher/shield |
| 2 참수빔 | 50%→20% | **18,600** | railgun 9k + rib_gate 9.6k |
| 3 코어 폭주 | 20%→0 | **12,400** | core |

### 참수 레일건 (act2 part attack)

| 필드 | 값 | 근거 |
|---|---|---|
| startOffset (part-rel) | −3.9 / 0 | 포구(−6.5,+1.4) − railgun(−2.6,+1.4) |
| endOffsetX | **−27.4** | 보스 기준 −30 (화면 관통) |
| fullHalfWidth | **1.3984375** (≈1.4) | 1.2–1.6 밴드 |
| cycle | 260t (tel90+fire10+sus120+diss20) | 비중첩 |

코어/레일 비활성 파츠는 **active:false** (무적만으로는 히트박스가 후방 파츠 탄을 가로챔).

## 6. Broodmother 3막

| 항목 | 값 |
|---|---|
| holdX | **9.0** |
| half | **9.25 × 9.3** |
| 흡입 | maw act2 · effectSpeed 3 · **effectMaxSpeed 5** · offset (−3.296875, −0.3984375) → 중심 ≈(−3.4,+6.5) |
| 재생 | tentacle_left/right regenDelay **900t** |

### 막별 HP

| 막 | 영구 경로 | HP |
|---|---|---:|
| 1 산란 방벽 | sac×3 (regen 촉수 제외) | **31,000** |
| 2 아가리 흡입 | maw (+ 잔여 촉수 칩) | **15,600** |
| 3 심장 | heart_core | **12,400** |
| (옵션) | tentacle regen | 1,500×2 |

심장/아가리는 막 전에는 **active:false** (소프트락 방지).

## 7. BalanceSim 신규·갱신 게이트

- `CheckReq116BossRedesign`: St1 이동 사다리 · hive 5×4+촉수2 · storm 체인 1→2 · form2 14k · levi 앵커 파츠·빔 · brood 흡입/재생 · fortress 잠금 · generate smoke.
- Colossal: core HP **12,400** · endgame DPS **1000** · TTK **50–70s** · **3-act/normal ratio 2.0–2.5** (실측 2.17) · holdX/half 앵커 · act 임계 1/2·1/5 · act1 fireInterval > act2 (진입 밸리).
- Standard boss 최소 개수 4→**3** (hive multipart 편입).

## 8. 검증 로그

```text
cd Tools\CoreStandalone && dotnet test
→ 통과!  실패: 0, 통과: 545, 전체: 545

cd Tools\BalanceSim && dotnet run --project VerifyThemeAssembly.csproj
→ PASS: BalanceSim all checks green.
→ PASS: REQ-116 boss redesign tables.
→ PASS: colossal boss catalog / TTK / spawn / generate.
   3-act vs normal ratio=2.17 ∈ [2.0, 2.5]

dotnet run --project Tools/DeterminismAudit -- --suite
→ AUDIT PASS (6/6 + cap-boundary 256)
→ seed-7-hidden PerfectClear bossHp=0/62000
```

재현 스크립트: `Tools/_req116_boss_redesign.py` (waves.json bosses[] 재생성; fortress 원본 보존).

## 9. 후속 요청 (requests.md)

- **CLAUDE**: Resources 동기화 · form2/체인/흡입/촉수 연출 · 히든 스프라이트 임포트.
- **CODEX**: (선택) 감사 파일럿이 비활성 코어 뒤 파츠를 맞출 때 active:false 의존 — 문서화만.
- **GEMINI**: form2+고스트 체감 · 히든 3막 TTK 교차 검산 · 해시 베이스라인.
- **사람**: 막별 HP·흡입 세기·레일 fullHalfWidth 1.4 손맛 확정 (§7 잠정).
