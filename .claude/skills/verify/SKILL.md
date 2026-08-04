---
name: verify
description: 이 프로젝트의 검증 파이프라인을 순서대로 돌린다 — 씬 재생성 → EditMode → WebGL 빌드 → 헤드리스 플레이 확인. 변경을 배포하기 전이나 "빌드해줘"/"확인해줘"를 들었을 때 쓴다.
disable-model-invocation: false
---

# 검증 파이프라인

Unity 변경은 **네 단계를 순서대로** 통과해야 신뢰할 수 있다. 하나라도 건너뛰면
"테스트는 통과했는데 화면에서는 안 보이는" 상태로 배포된다 — 이 프로젝트에서
실제로 여러 번 일어났다(라벨이 빈 칸으로 나온 개발자 패널, 판때기로 보이던 피격
표시, 함체 크기를 키우기 전 스크린샷).

## 순서

```bash
# 0. 값싼 그물부터 (Unity 없이 20초)
python -X utf8 Tools/CsCheck/cs_syntax_check.py --changed
python -X utf8 Tools/CsCheck/subunit_grid_check.py --changed
python -X utf8 Tools/CsCheck/sprite_import_check.py     # PPU 16 · Point 인가
python -X utf8 Tools/CsCheck/art_source_check.py        # art-input 원본과 같은가
cd Tools/CoreStandalone && dotnet test && cd ../..

# 1. 씬 재생성 — 아트·프리팹·직렬화 참조가 바뀌었으면 필수
unity run . -- -executeMethod Shmup.EditorTools.BattleSceneBuilder.Build \
  -logFile scene.log --non-interactive --no-banner

# 2. EditMode
unity test --mode EditMode --output test-results.xml --non-interactive --no-banner

# 3. WebGL
unity build --target WebGL \
  --execute-method Shmup.EditorTools.MobileBuilder.BuildWebGl \
  --log-file build.log --no-tail --non-interactive --no-banner

# 4. 헤드리스 플레이 확인 (화면을 실제로 본다)
#
# **서버는 이미 떠 있으면 다시 띄우지 마라.** 검증할 때마다 새로 띄우면 같은
# 포트라 첫 번째만 듣고 나머지는 좀비로 쌓인다 — 2026-08-04에 11개까지 쌓였고
# 사람이 "지금 실행중인건 뭐야?"라고 묻고서야 알았다. 공유 PC다 (AGENTS.md §9).
curl -s -o /dev/null http://127.0.0.1:8099/index.html   || (cd Builds/Web && python -m http.server 8099 --bind 127.0.0.1 &)
node Tools/QaHarness/rss-verify.js --stage 3 --warp boss --seconds 60 --out ./out/check
```

`unity` CLI를 쓴다. **`Unity.exe -batchmode` 직접 호출 금지** (사람 지시).

## 끝나고 치울 것

헤드리스 확인이 끝나면 남긴 프로세스를 확인해라. 하네스(puppeteer)는 스스로
브라우저를 닫지만 **로컬 서버는 안 닫힌다.**

```bash
# 지금 떠 있는 내 서버 보기
powershell -Command "Get-CimInstance Win32_Process -Filter \"Name like 'python%'\" |
  Where-Object { \$_.CommandLine -match 'http.server 8099' } |
  Select-Object ProcessId"
```

세션을 마칠 때는 정리한다. 하나만 남기고 재사용하는 편이 낫다.

## 실패를 읽는 법

**exit 6은 두 가지 뜻이다.** 반드시 갈라 보라:

```bash
grep -c "error CS" scene.log          # 0이 아니면 컴파일 오류
python -X utf8 -c "import xml.etree.ElementTree as ET;\
t=ET.parse('test-results.xml').getroot();\
print(t.get('passed'),'/',t.get('total'),'failed',t.get('failed'))"
```

- `error CS`가 있으면 → 컴파일 오류. 0단계를 건너뛴 것이다.
- 테스트 결과가 전부 통과인데 exit 6이면 → Unity가 결과를 쓴 뒤 종료 중
  크래시(0xC0000005)한 것이다. **재실행하면 된다.**
- 진짜 테스트 실패면 실패한 테스트 이름을 뽑아 읽어라.

## 헤드리스 결과를 믿기 전에

하네스의 `FAIL`이 항상 회귀는 아니다. **기체를 y=0에 고정**하고 돌리므로,
그 높이에 판정이 없는 보스에서는 데미지가 0으로 나온다. 실제로 이것이
보스 판정 구멍(2026-08-04)을 찾아낸 단서였지만, 반대로 멀쩡한 빌드에서도
FAIL이 뜬다. 판단이 서지 않으면 **프레임을 직접 열어 보라**:

```bash
python -X utf8 -c "from PIL import Image; \
Image.open('Tools/QaHarness/out/check/060_t.png').crop((170,55,1130,660)).save('frame.png')"
```

그리고 Read 도구로 `frame.png`를 본다. 이 프로젝트에서 반복된 교훈이다 —
**수치가 통과해도 화면은 거짓말할 수 있다.**

## UI를 바꿨으면

타이틀·격납고·HUD를 건드렸으면 캡처해서 눈으로 확인한다. 라벨이 빈 칸으로
나오거나 글자가 겹치는 것은 테스트가 잡지 못한다.

```bash
node Tools/QaHarness/doc_shots.js title
```

## 배포

검증이 끝난 뒤에만.

```bash
powershell -ExecutionPolicy Bypass -File Tools/deploy_web.ps1 -Message "buildNN: 무엇을 고쳤는지"
```

스크립트가 `Build/`만 갱신하고 index.html의 캐시 스탬프만 바꾼다.
직접 복사하지 마라 — 커스텀 index.html을 덮어써 레이아웃이 깨진 적이 있고,
스탬프를 안 바꿔 배포가 플레이어에게 **다섯 번 연속 도달하지 않은** 적도 있다.
