# REQ-149 — magnet_claw HP 절반

## 변경

| 적 | 변경 | 근거 |
|---|---|---|
| **magnet_claw** | 72 → **36** | scrapyard 초반 sine 졸개. 같은 대역 `spore_drifter` 14 / `wisp_spark` 8 대비 72는 과함. 사람 지시로 절반 |

## 대역 메모

- 사격(`fireIntervalTicks` 140) + `bombDropWeight` 5 이라 순수 접촉 졸개(8~14)보다 높은 HP는 유지.
- 36은 spore 대비 ~2.5× (이전 5×). 체감이 여전히 두꺼우면 추가 하향 가능.

## 검증

`cd Tools\CoreStandalone && dotnet test`
