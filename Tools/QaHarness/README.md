# QaHarness — WebGL 빌드 headless 검증

세션마다 새로 쓰던 puppeteer 스크립트를 여기로 옮겼다. 스크린샷을 눈으로 읽는 대신
**픽셀 수치 어서션**으로 PASS/FAIL을 낸다 (REQ-124).

## 준비 (1회)

```
cd Tools/QaHarness
npm i                     # puppeteer-core + pngjs
```

Chrome 경로가 기본값과 다르면 `RSS_CHROME` 환경변수로 준다.

## 실행

```
# 1) 빌드 (batchmode 직접 호출 금지 — AGENTS.md §9)
unity build --target WebGL --execute-method Shmup.EditorTools.MobileBuilder.BuildWebGl --log-file build.log

# 2) 서버 (별도 셸, 백그라운드)
cd Builds/Web && python -m http.server 8099 --bind 127.0.0.1

# 3) 검증
cd Tools/QaHarness
node rss-verify.js --stage 3 --warp boss --seed 2 --seconds 40 --out ./out/st3
```

종료 코드 0 = PASS. `out/<이름>/report.json`에 HP 시계열이 남고, 10초마다 스크린샷이
떨어진다(회귀 시 눈으로 볼 근거).

## 옵션

| 옵션 | 뜻 |
|---|---|
| `--stage N` | N스테이지에서 시작 (`?stage=N`) |
| `--warp early\|midboss\|late\|boss` | 해당 구간까지 즉시 워프 (REQ-124) |
| `--seed N` | 시드 고정. **URL이 아니라 타이틀 키 입력으로 넣는다** — `DevArgs.OverrideSeed`는 커맨드라인 인자만 읽어서 `?seed=`는 무시된다 |
| `--seconds N` | 도착 후 관찰 시간 (1초 간격 샘플링) |
| `--god 0` | 무적 끄기 (기본 켜짐) |

## 함정 (다시 밟지 마라)

- **키 연타는 프레임당 1회만 인식된다.** `Backspace`로 시드를 지울 때 간격(80ms+)이
  없으면 한 글자만 지워진다. 시드가 안 바뀌어 "왜 매번 다른 판이지?"가 된다.
- **워프는 런 시작 1회만 돈다.** 5바이옴 완주가 필요한 미지의 구역에는 안 통한다
  (REQ-123 대기).
- **좌표를 박지 마라.** 캔버스 배율이 바뀌면 조용히 틀린다 — `measureBossHpBar`처럼
  색으로 찾아라.
- **hive 계열 보스는 기체를 세로로 움직여야 진행된다.** 기본 y=0에 서 있으면 무적
  코어만 때리고 진행이 0인데 탄은 사라져서 맞고 있는 것처럼 보인다 (REQ-125).

## 공유 PC 제약

AGENTS.md §9: 사람이 해제할 때까지 **항상 headless**다. `headless: false`로 바꾸지 마라 —
작업자 화면에 창이 튀어나온다.
