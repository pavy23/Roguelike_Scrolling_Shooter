# REQ-148 — 맷집 지나친 졸개 HP 절반

## 후보 검증

| 적 | 변경 | 근거 |
|---|---|---|
| **elite_sine** | 620 → **310** | sine 이동, half 1.64×1.33(소형 중 상위), fire 120. 세그먼트 전반 중형 앵커. “빨간색 큰 비행기”와 일치 |
| **zako_sine_slow** | 100 → **50** | sine 이동, half 1.76×1.41(잡졸 중 최대). 동 패턴 `zako_sine` HP 14 대비 7× — 확실히 튐 |

### 스테이지 1 배치 메모

- 테마 분할(REQ-145/146) 이후 **difficultyMin=1** 사인 세그먼트는 테마 고유 졸개:
  - scrapyard: `magnet_claw` HP 72
  - hive: `spore_drifter` HP 14
  - nebula: `wisp_spark` HP 8
- `zako_sine_slow`는 현재 **nebula mid** (`seg_nebula_wisp_storm`/`ribbon`, dMin=2) 위주.
- 그래도 “위아래 사인 + 잡졸 대비 과한 맷집” 아키타입은 그대로라 후보 유지.
  (scrapyard 초반 sine이 체감 대상이었다면 차기 `magnet_claw` 조율 후보.)

## 검증
`cd Tools\CoreStandalone && dotnet test`
