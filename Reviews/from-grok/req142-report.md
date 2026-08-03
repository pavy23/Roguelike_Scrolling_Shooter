# REQ-142 GROK 구현 보고 — 전함 파츠를 함체 갑판선에 얹기

- 작업일: 2026-08-03
- 담당: GROK / CONTENT
- 브랜치/worktree: `content` / `wt-content`
- 기반: main `64ed212` (커밋 3904577 이후 포함)

## 문제

`warship_hull.png`(544×272 = 34×17u) 실측 갑판선은 x에 따라 크게 다르다.
함교(x 0~+5)만 +5 근처, 나머지는 +1.4~+2.6. 기존 파츠는 전부
`offsetY` +6.5~+7이라 포탑 c·d 등이 갑판 위에 3u 이상 떠 있었다.

## 규칙

`offsetY = 갑판선(x) + halfHeight` → 파츠 **바닥**이 갑판에 닿음.
코어(ox=−11)는 함수 안쪽이라 `offsetY = 0` 유지.
모든 값은 1/256 격자 exact (픽셀 실측 그대로).

## 확정 수치

| 파츠 | offsetX | 갑판선 | halfH | offsetY (전→후) | 바닥 |
|---|---:|---:|---:|---|---:|
| engine | +5.0 | +5.0625 | 2.5 | 7.0 → **7.5625** | 5.0625 |
| turret_a | +4.0 | +4.9375 | 1.25 | 6.5 → **6.1875** | 4.9375 |
| turret_b | +0.0 | +5.4375 | 1.25 | 7.0 → **6.6875** | 5.4375 |
| turret_c | −4.0 | +2.625 | 1.25 | 7.0 → **3.875** | 2.625 |
| turret_d | −8.0 | +1.75 | 1.25 | 6.5 → **3.0** | 1.75 |
| core | −11.0 | (함수 안) | 2.5 | 0.0 유지 | — |

| 그룹 | anchorOffsetY (전→후) | 의도 |
|---|---|---|
| stern | −9.0 → **−8.0** | 아래 잠김 유지, 엔진 y=0 직사 여유 |
| hull / bow | 0.0 | 변경 없음 |

### 1막 피격 (A=−8, engine oy=7.5625)

- 엔진 월드 중심 = −0.4375, 히트박스 **[−2.9375, +2.0625]** → y=0 직사 교차
- 함체 상단 = +0.5, 화면 하단(−11.25) 아래 잠김 **5.25u** (이전 6.25u)
- "아래에 잠겨 윗부분만" 연출 유지, Req119 통과

엔진 offsetY는 갑판에 얹히면서 **소폭 상승**(7→7.5625)이다.
떠 있던 주범은 함교 밖 포탑(c·d)의 일괄 +6.5~7 배치였다.

## 변경 파일

| 파일 | 내용 |
|---|---|
| `GameData/waves.json` | 파츠 offsetY + stern anchorOffsetY |
| `Assets/Resources/GameData/waves.json` | 동일 동기화 |
| `Tools/_req139_fortress_scale.py` | 재현 스크립트 REQ-142 반영 |
| `Reviews/from-grok/req142-report.md` | 본 보고 |

`Assets/Scripts`·`Assets/Tests` 미수정 (소유 밖).

## 검증

```text
cd Tools\CoreStandalone && dotnet test
→ Req118 / Req119 / Req139: 9/9 PASS
→ 전체: 571 / 571 PASS
```

## 하지 않은 것

- 엔진 스프라이트 밝기/아트 (사람이 따로 처리)
- halfWidth/halfHeight·HP·offsetX 변경
- Core / Presentation 코드
