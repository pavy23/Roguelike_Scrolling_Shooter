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

## 임포트

`Assets/Art/Sprites/`에 넣은 뒤 **씬을 재생성해야** 배선이 붙는다:

```bash
unity run . -- -executeMethod Shmup.EditorTools.BattleSceneBuilder.Build \
  -logFile scene.log --non-interactive --no-banner
```
