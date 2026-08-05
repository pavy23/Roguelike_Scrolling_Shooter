# 히든 보스 ph3 근접 공격 + 예고 — 데이터 작업

- 작업일: 2026-08-05
- 담당: GROK / CONTENT
- 선행: CLAUDE `meleeTelegraphTicks` 배관 (commit `6bad786`)
- 커밋: **안 함** (사람 지시)

## 한 줄 요약

레비아탄 낫팔·브루드마더 촉수에 **ph3 전용** `meleeCharge` + 1초 예고를 붙였다. HP·본체 탄 밀도 불변. Req181 TTK 밴드 유지.

---

## 수치

| 보스 | 파츠 (ph3 only) | interval | telegraph | effectSpeed | contactDamage |
|---|---|---:|---:|---:|---:|
| **레비아탄** | `blade_limb_upper`, `blade_limb_lower` | **300** | **60** | **10.0** | **1** |
| **브루드마더** | `tentacle_left`, `tentacle_right` | **300** | **60** | **8.0** | **1** |

### 주기 분해 (공통 interval 300)

| 구간 | 틱 | 초 | 비고 |
|---|---:|---:|---|
| 예고 (정지·노란 점멸) | 60 | 1.0 | 사람 반응 가능 권장(≈1s) |
| 돌진 (`interval/4`) | 75 | 1.25 | 본체 전진 |
| 회복 | 165 | 2.75 | 조밀 탄막 구간 호흡 |
| **합** | 300 | 5.0 | |

제약 `telegraph + max(1, interval/4) ≤ interval` → `60 + 75 = 135 ≤ 300` ✓

### 근거

1. **예고 60t** — 지시 "60틱(1초) 내외". 레이저 텔(160–180t)보다 짧아 늘지지 않고, 돌진 전에 피할 시간은 준다.
2. **주기 300t (5초)** — ph3 본체 탄막이 이미 조밀(≈13.1발/초)하다. 감사 샘플(210–240t)보다 **넉넉히** 벌려, 근접이 탄막을 덮어 회피 공간을 지우지 않게 했다. 레이저 주기(400–420t)보다는 짧아 ph3 정체성이 "간헐 근접"으로 남는다.
3. **effectSpeed** — DeterminismAudit 기존 밀리 대역: 낫팔 **10** (크게 휘두름), 촉수 **8** (뻗음). 돌진 1.25초 × 속도 ≈ 레비아탄 12.5u / 브루드 10u 전진.
4. **contactDamage 1** — 감사·기존 보스 밀리·레이저 `damage: 1`과 동일. (데이터에 다른 meleeCharge 원본이 없어 감사 기본값을 채택.)
5. **ph1·ph2 유지** — 레비아탄 낫은 ph1 비활성 / ph2 활성(공격 없음) 그대로. 브루드 촉수는 ph1 aimedSpread / ph2 laser 유지. **ph3 partRules만** meleeCharge로 교체.
6. **HP 미변경** — TTK 밴드 보존.

### 브루드 ph3 촉수 레이저 제거

ph3 촉수는 기존 laser(400t)였는데 사람 지시대로 **meleeCharge로 교체**. sac L/R 레이저·maw 흡입·본체 radial은 그대로라 빔 위협이 완전히 사라진 것은 아니다.

---

## 검증

```
cd Tools\CoreStandalone && dotnet test
→ 통과 584/584 (현재 수)
```

### Req181HiddenBossRealFightTests (detailed)

| 자세 | Broodmother | Leviathan | 목표 |
|---|---:|---:|---|
| **붙어서** | **100초** (6005틱) | **100초** (6015틱) | ~100초 |
| **떨어져서** | **125초** (7481틱) | **149초** (8940틱) | 125–150초 |

격파 성공. TTK 크게 흔들리지 않음 (근접은 플레이어 무적 테스트라 데미지 경로에 영향 없음; 위치 밀림도 격파를 막지 않음).

### Req158BossPhaseReachabilityTests

`EveryMultipartBossCanReachEveryPhaseAndDie` **통과**.

---

## 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | leviathan ph3 blade 2파츠 + broodmother ph3 tentacle 2파츠 meleeCharge |

`Assets/Resources/GameData/waves.json` 동기화는 CLAUDE 몫 (원본은 `GameData/`).

---

## Core 주의 (소유 밖 — 참고만)

`ApplyBossMeleeContact`는 여전히 `cycle < interval/4` 구간에서만 접촉한다. 예고가 켜지면 그 구간이 **정지 예고 창**과 겹치고, 실제 전진 구간 후반에는 접촉이 꺼진다. 예고 배관(이동)과 접촉 창이 어긋날 수 있다. 데이터 작업 범위 밖이라 수정하지 않았다 — 필요하면 CLAUDE/CODEX에 요청.
