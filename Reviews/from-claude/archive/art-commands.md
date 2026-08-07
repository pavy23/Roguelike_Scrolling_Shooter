# 아트 생성 명령 모음

산출물과 함께 프롬프트를 커밋한다 (ART-DIRECTION.md §재현성). 여기 없는 그림은
어떻게 나왔는지 아무도 모른다는 뜻이므로, 새로 만들 때마다 추가해라.

전제: `OPENAI_API_KEY`, `PIXELLAB_API_KEY` 환경변수.

## 배경 — 원경 (`<prefix>_far.png`)

**꽉 찬 장면이라 `--opaque`가 필수다.** 이걸 빼면 모델이 하늘·바닥을 비우고,
가로 타일링 크로스페이드가 그 빈 곳을 좌우 대칭 유령으로 만든다 (실제로 겪었다).

```bash
python Tools/ArtGen/artgen.py openai --opaque --model gpt-image-1.5 \
  --size 1536x1024 --quality high --out Tools/ArtGen/out/raw/<prefix>_far.png \
  --prompt "Pixel art scrolling background, full-bleed opaque scene filling the
   entire frame, <장면>, hi-bit 16-bit style like SNES-era Gradius and R-Type with
   modern shading, <소재>, 3-4 tone ramps per material, hard pixel edges, no
   anti-aliasing, wide horizontal composition that repeats left to right,
   limited 48 color palette"

python Tools/ArtGen/artgen.py post --src Tools/ArtGen/out/raw/<prefix>_far.png \
  --target 640x360 --cover --no-trim --colors 48 \
  --out Tools/ArtGen/out/section/<prefix>_far.png
```

황혼/야간 변형(`_far_dusk` / `_far_dark`)은 **절차 리매핑이 맞다** — 알파와
실루엣을 원본에서 그대로 물려받아 구간 전환에서 형태가 튀지 않는다:

```bash
python Tools/ArtGen/gen_section_bg.py --only <prefix> \
  --src Tools/ArtGen/out/section --out Tools/ArtGen/out/section
```

## 배경 — 전경 실루엣 (`<prefix>_fg.png`)

위아래 띠 + 가운데 투명. **띠가 두껍고 꽉 차야 한다** — 얇게 나오면 전경으로
읽히지 않는다 (abyss 1차가 그래서 다시 뽑았다).

```bash
python Tools/ArtGen/artgen.py openai --model gpt-image-1.5 \
  --size 1536x1024 --quality high --out Tools/ArtGen/out/raw/<prefix>_fg.png \
  --prompt "Pixel art parallax FOREGROUND silhouette layer on a fully transparent
   background, hi-bit 16-bit style like SNES-era Gradius and R-Type with modern
   shading, a THICK SOLID dark terrain mass filling the bottom quarter of the frame
   edge to edge and a THICK SOLID mass hanging from the top edge, wide open
   transparent gap through the middle where the player flies, hard pixel edges,
   no anti-aliasing, seamless when tiled left to right, limited 24 color palette,
   dark values so gameplay sprites stay readable on top. Subject: <장면>"

python Tools/ArtGen/artgen.py post --src Tools/ArtGen/out/raw/<prefix>_fg.png \
  --target 640x360 --no-trim --colors 24 \
  --out Tools/ArtGen/out/section/<prefix>_fg.png
```

`--cover`를 쓰지 마라. 전경은 위아래 띠 위치가 의미라서, 잘라 맞추면 띠가 화면
밖으로 나간다.

## 배경 — 중경 (`<prefix>_mid.png`)

투명 배경 위 **낱개 오브젝트**다. 붙어 있으면 원경과 구별되지 않는다.

```bash
--prompt "Pixel art parallax midground layer on fully transparent background,
 scattered isolated <소재> floating with large empty transparent gaps between them,
 ... objects spread across a wide horizontal strip, limited 32 color palette"
--target 640x360 --cover --no-trim --colors 32
```

## 배경 — 랜드마크 (`<prefix>_landmark.png`)

지나가는 대형 단일 구조물. 반복하지 않으므로 타일링 제약이 없다.

```bash
python Tools/ArtGen/artgen.py openai --model gpt-image-1.5 \
  --size 1024x1024 --quality high --out Tools/ArtGen/out/raw/<prefix>_lm.png \
  --prompt "Pixel art LANDMARK object on a fully transparent background, a single
   large non-repeating structure the player flies past, hi-bit 16-bit style like
   SNES-era Gradius and R-Type with modern shading, 3-4 tone ramps per material,
   hard pixel edges, no anti-aliasing, no ground plane, no background scenery,
   isolated object only, limited 32 color palette. Subject: <대상>"

python Tools/ArtGen/artgen.py post --src Tools/ArtGen/out/raw/<prefix>_lm.png \
  --target 360x240 --colors 32 --out Tools/ArtGen/out/section/<prefix>_landmark.png
```

"no ground plane, no background scenery, isolated object only"를 빼지 마라 —
빼면 모델이 밑에 땅을 깔아 주고, 그 땅이 화면 한가운데를 떠다닌다.

## 보스 아이들 애니메이션 (`anim_<bossPrefix>_00..04.png`)

PixelLab `animate-with-text-v3`. **제약 두 개를 기억해라**:
- `frame_count`는 짝수만 (4, 6, 8, …). 5를 넣으면 422다.
- 입력 이미지는 **256×256 이하**. 큰 보스는 정수배로 줄였다가 결과를 다시 키운다.

```bash
python - <<'EOF'
from PIL import Image
im = Image.open("Assets/Art/Sprites/<boss>.png").convert("RGBA")
im.resize((im.width//2, im.height//2), Image.NEAREST).save("Tools/ArtGen/out/<boss>_half.png")
EOF

python Tools/ArtGen/artgen.py animate \
  --first Tools/ArtGen/out/<boss>_half.png --frames 4 --seed <고정> \
  --action "slow menacing idle, <무엇이 어떻게 움직이는지>" \
  --out-dir Tools/ArtGen/out/anim_<boss>

# 결과를 원래 크기로 되돌려 Assets/Art/Sprites/anim_<boss>_NN.png 로 저장
```

시드를 반드시 적어 두어라. 안 그러면 프레임 하나를 다시 뽑을 때 나머지와
질감이 어긋난다.

## 임포트 — **art-input이 원본이다**

새 그림은 `art-input/<이름>.png`에 넣어라. `Assets/Art/Sprites/`는 **산출물**이고,
씬 재생성이 art-input을 그 위에 복사한다.

Assets에만 넣으면 다음 재생성에서 **조용히 옛 버전으로 되돌아간다.** 2026-08-04에
새로 그린 11장(전함 코어 + 공개 5테마의 전경·랜드마크)이 실제로 그렇게 사라졌고,
사람이 "코어가 왜 디자인 보여준거랑 다른게 적용됐지?"라고 물어서야 알았다.
컴파일도 테스트도 임포트 검사도 전부 통과한다 — 파일은 멀쩡하고 내용만 옛것이라서.

    python Tools/CsCheck/art_source_check.py   # 둘이 어긋나면 잡아 준다

넣은 뒤 **씬을 재생성해야** 배선이 붙는다:

```bash
unity run . -- -executeMethod Shmup.EditorTools.BattleSceneBuilder.Build \
  -logFile scene.log --non-interactive --no-banner
```

## 모델 섞어 쓰기 (2026-08-04 검증)

각 모델의 강점이 다르다. **한 모델로 끝내지 말고 이어 붙이는 쪽이 나은 경우가 있다.**

| 모델 | 잘하는 것 | 못하는 것 |
|---|---|---|
| PixelLab | 네이티브 픽셀 격자, 애니메이션 | 256px 상한, 복잡한 구도 지정 |
| gpt-image-1.5 | 구도·소재 해석, 투명, 크기 자유 | 축소하면 격자가 무너짐 |
| Grok | 배경 분위기·깊이감 | 투명·크기 불가, 대비가 낮아 변환에 약함 |
| Gemini `--ref` | **기존 그림의 팔레트·질감 계승** | 내용 정확도 |

### 조합 1 — 구도는 gpt-image, 격자는 PixelLab (**검증됨, 효과 큼**)

단순 축소로 뭉개지던 것이 살아난다. `compare_chain.png` 2번 칸.

```bash
python Tools/ArtGen/artgen.py openai --model gpt-image-1.5 --size 1024x1024 \
  --quality high --out out/raw/x.png --prompt "<구도를 자세히>"
python Tools/ArtGen/artgen.py pixelart --src out/raw/x.png --target 64x48 \
  --seed <고정> --out out/raw/x_px.png
python Tools/ArtGen/artgen.py post --src out/raw/x_px.png --target 64x48 \
  --cutout 40 --colors 32 --out Assets/Art/Sprites/x.png
```

`--cutout`을 빼지 마라 — 변환기는 **불투명 배경**으로 돌려준다.

언제 쓰나: 소재가 복잡하거나 배치를 정확히 지시해야 할 때(파츠가 여럿인 기계,
특정 방향으로 무너지는 잔해). 단순한 물체는 PixelLab 직접이 여전히 낫다.

### 조합 2 — 내용은 PixelLab, 팔레트는 Gemini

기존 에셋과 색이 어긋날 때. Gemini는 팔레트 계승이 확실히 강했다.

```bash
python Tools/ArtGen/artgen.py gemini \
  --ref <새로_만든_스프라이트>.png --ref <기존_에셋>.png \
  --out out/raw/recolored.png \
  --prompt "Recolor the FIRST image to use exactly the palette and shading style
   of the SECOND image. Keep the first image's shapes and composition unchanged.
   Transparent background, hard pixel edges, no anti-aliasing."
```

**내용을 반드시 눈으로 검수해라.** Gemini는 색은 맞추지만 형태를 바꿔 놓기도 한다.

### 쓰지 않는 조합

- **Grok → PixelLab 변환**: Grok 출력이 저대비 회화풍이라 변환기가 형태를 못 잡는다
  (`compare_chain.png` 3번 칸 — 검은 덩어리가 됐다).
- **Grok → 전경/중경/랜드마크**: 투명 배경을 지원하지 않는다. 애초에 불가.
