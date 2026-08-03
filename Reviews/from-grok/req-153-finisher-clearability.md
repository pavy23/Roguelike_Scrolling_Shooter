# REQ-153 — 히든 보스 마지막 기술 클리어 가능 수준 재검토

- 작업일: 2026-08-04
- 담당: GROK / CONTENT
- 브랜치: `content` / `wt-content`
- 선행: REQ-152 (멀티파트 판정 구멍 메움), REQ-116 (흡입·레일건 수치)
- 사람 원문: 브루드마더 흡입 역장이 너무 강력한지 재검토 + 레비아탄 참수 레일건 동일 기준

## 결론

| 보스 | 마지막 기술 | 순수 수치 판정 | 실제 위험 요인 | 조치 |
|---|---|---|---|---|
| **broodmother** | maw 흡입 (act2 @50%) | **저항 가능** (처형 아님) | 흡입 + 본체 5-way + 촉수 동시 | 흡입·본체 탄막 완화 |
| **leviathan** | railgun 참수빔 (act2 @50%) | **회피 가능** (텔 1.5s 충분) | 빔 두께 + 2s 지속 + 주변 탄 | 텔↑·지속↓·폭↓·주변 탄↓ |

판정 구멍(REQ-152)으로 act2가 길어지던 체감이 섞여 있었을 가능성이 크다. 구멍을 메운 지금은 화력이 이미 올라가 있으므로 **과너프 없이** 동시 압박만 줄였다.

---

## 1. Core 흡입 모델 (BattleSim.AdvanceSuctionForce)

- `effectSpeed` / `effectMaxSpeed`는 JSON 초당 월드유닛 → `ToPerTickSpeed`로 **틱당 서브유닛** 변환.
- 매 틱 흡입원 방향 단위벡터 × `effectSpeed`만큼 **변위**를 합산 (잔여 remainder로 유리수 exact).
- `effectMaxSpeed`는 틱 변위 벡터 길이 상한. **effectSpeed < max이면 상한은 사실상 미발동**.
- 입력 이동 + drift + 흡입을 같은 틱에 합산. 흡입은 탄/RNG와 무관, 이동에만 작용.
- 구 데이터 상한 없음은 `int.MaxValue` (CODEX REQ-115b 호환).

**구 수치** `effectSpeed:3, effectMaxSpeed:5` → 실효 당김 = **3.0 u/s** (max 미사용).

---

## 2. 기체 이동속도 (실전 기준)

`player.json`의 `moveSpeed:9.5`는 DTO에 연결되어 있지 않다. 실전은
`BattleSimConfig.CreateDefault()`의 **13 u/s** + SPEED 게이지 보너스 **1.5 u/s/레벨**
+ 기체 배수(starter 1.0 / interceptor 1.25 / bulwark 0.8).

| 기체 | SPEED L0 | L1 | L2 | L6 |
|---|---:|---:|---:|---:|
| starter | **13.00** | 14.50 | 16.00 | 22.00 |
| interceptor | 16.25 | 17.75 | 19.25 | 25.25 |
| bulwark (최저) | **10.40** | 11.60 | 12.80 | 18.80 |

대각선 디지털 입력 성분: `46340/65536 ≈ 0.707` (축당 약 70.7%).

---

## 3. 흡입 vs 이속 — 저항 가능 여부

### 구 (effectSpeed 3.0, 실효 3.0 u/s)

| 기체·레벨 | 이속 | 정면 반대 net | 잔존율 | 대각 1축 성분 net\* |
|---|---:|---:|---:|---:|
| starter L0 | 13.00 | **+10.00** | 77% | +6.19 |
| starter L2 | 16.00 | +13.00 | 81% | +8.31 |
| bulwark L0 | 10.40 | **+7.40** | 71% | +4.35 |
| bulwark L0 대각 성분만 | 7.35 | **+4.35** | — | — |

\*흡입이 한 축에 전부 실리고, 기체가 대각으로 반대 성분을 내는 경우.

**판정**: 최대 속도로 반대로 가도 빨려 들어가는 “처형”은 **아니다**.  
bulwark L0도 정면 저항 시 7.4 u/s로 탈출 가능 (10u 이탈 ≈ 1.35s).

### 신 (effectSpeed 2.0, max 2.5 → 실효 2.0 u/s)

| 기체·레벨 | net (정면) | 잔존율 | 대각 1축 net |
|---|---:|---:|---:|
| starter L0 | **+11.00** | 85% | +7.19 |
| bulwark L0 | **+8.40** | 81% | +5.35 |

위협 유지: 여전히 이속의 15–19%를 상시 훔쳐 탄막 회피 대역을 줄인다.

---

## 4. 흡입 중 동시 탄막 (구 → 신)

act2 (`hpThreshold:0.5`) 활성 위협:

| 소스 | 구 | 신 | 비고 |
|---|---|---|---|
| **maw 흡입** | 3.0 u/s (max 5 미사용) | **2.0 u/s** (max **2.5**) | 역장 자체 |
| **본체 pattern** | aimed 5-way / 42t / 8.5 | **4-way / 56t / 7.5** | 주 압박 완화 |
| tentacle_left | melee 210t / 8.0 | 유지 | 접촉 압박 |
| tentacle_right | aimed 5-way / 84t / 6.0 | 유지 | 측면 탄 |
| sac×3 / core | 비활성 | 비활성 | — |

**회피 여지**: 구에는 0.7초마다 5-way heavy + 상시 흡입 + 촉수로 “빠져나와도 탄에 묶임”.  
신에는 본체 주기가 0.93s·4-way·탄속 7.5로, 저 SPEED도 흡입 저항 여유분으로 축 이동할 틈이 생긴다.

---

## 5. 레비아탄 참수 레일건 (동일 기준)

### 구

| 항목 | 값 | 저속 기체 판정 |
|---|---|---|
| telegraph | 90t (1.5s) | bulwark L0로 1.5×10.4≈15.6u 이동 가능 — 빔 두께 회피 **가능** |
| sustain | 120t (2.0s) | 긴 지속 |
| fullHalfWidth | 1.3984375 (두께 ≈2.8u) | 안전 띠 좁음 |
| cycle | 260t | tel+fire+sus+diss=240 + 유휴 20 |
| 본체 동시 | radial 6-way / 55t / 7.5 | 빔 회피 중 탄압 |

### 신

| 항목 | 구 | 신 |
|---|---|---|
| telegraphTicks | 90 | **120** (2.0s) |
| sustainTicks | 120 | **90** (1.5s) |
| fullHalfWidth | 1.3984375 | **1.203125** (BalanceSim 1.2–1.6 하한 근처, 308/256) |
| cycleInterval / interval | 260 | **280** (합 240 + 유휴 40) |
| 본체 fireInterval / ways / speed | 55 / 6 / 7.5 | **64 / 5 / 7.0** |

빔은 여전히 화면 관통(`endOffsetX: -27.3984375`)·damage 1. 위협 삭제 없음.

bulwark L0 텔 구간 이동량: 2.0s × 10.4 = **20.8u** — 빔 반폭 1.2 + 히트박스 여유 대비 충분한 회피 거리.

---

## 6. REQ-152 판정 구멍 보정 폭

| 보스 | REQ-152 구멍 | 화력 체감 |
|---|---|---|
| broodmother | 하부 3.10u + 미세 0.20u → sac_lower 확대 | act1·act2 유효 타격면 ↑ → act2 체류 시간 ↓ |
| leviathan | 하단 0.60u만 | 소폭 |

따라서 이번 너프는 **“처형 수치를 깎는” 수준이 아니라**, 구멍이 메워진 뒤에도 남는 **동시 압박**을 저 SPEED 기준으로 다듬는 쪽에 맞췄다.

---

## 7. 변경 파일

`GameData/waves.json` only

### boss_broodmother act2

- suction `effectSpeed` 3.0 → **2.0**, `effectMaxSpeed` 5.0 → **2.5**
- phase pattern: interval 42→**56**, ways 5→**4**, bulletSpeed 8.5→**7.5**

### boss_leviathan act2

- railgun laser: tel 90→**120**, sustain 120→**90**, fullHalfW →**1.203125**, cycle 260→**280**
- phase pattern: interval 55→**64**, ways 6→**5**, bulletSpeed 7.5→**7.0**

전부 1/256 격자 exact.

---

## 8. 검증

```
cd Tools\CoreStandalone && dotnet test
→ 통과! 실패 0, 통과 571, 전체 571 (net10.0)
```

BalanceSim REQ-116 게이트: suction `effectMaxSpeed>0` 유지, railgun fullHalfW∈[1.2,1.6]·endOffset≤−25 유지.
