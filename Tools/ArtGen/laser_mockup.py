#!/usr/bin/env python3
"""레이저 연출 후보 비교 시트.

사람 지시 2026-08-04: "레이저 연출이 너무 허접해. 좀더 상용 게임 수준의 레이저
연출로 바꿔주고, 레이저가 중간에 끊기지 않게끔 해줘. 기존안 / 후보 몇개 그려서
비교해보자"

왜 Unity가 아니라 여기서 그리나: 후보 하나를 보려고 씬 재생성 + 빌드 + 캡처를
돌리면 4분이다. 넷을 비교하려면 16분. 레이저는 결국 **겹겹이 쌓은 알파 사각형**이라
같은 규칙으로 여기서 그릴 수 있고, 고른 뒤 그것만 Unity에 옮기면 된다.

실행: python Tools/ArtGen/laser_mockup.py
"""
import math
import pathlib

import numpy as np
from PIL import Image, ImageDraw, ImageFont

W, H = 620, 150
OUT = pathlib.Path(__file__).resolve().parent / "out" / "laser"

# 게임 화면과 같은 어두운 배경 위에서 판단해야 한다 — 흰 종이 위에서는
# 어떤 안이든 잘 보인다.
BG = (14, 16, 26)

BEAM_Y = H // 2
BEAM_X0, BEAM_X1 = 40, W - 40


def canvas():
    image = Image.new("RGB", (W, H), BG)
    # 배경 잡음(별·구조물)을 조금 깔아 대비를 실제와 비슷하게 만든다.
    rng = np.random.default_rng(7)
    pixels = np.asarray(image).copy()
    for _ in range(90):
        x, y = rng.integers(0, W), rng.integers(0, H)
        v = int(rng.integers(40, 90))
        pixels[y, x] = (v, v, v + 18)
    return Image.fromarray(pixels, "RGB")


def add_layer(accumulator, y0, y1, color, alpha):
    """[y0,y1) 구간에 색을 알파 합성. accumulator는 float RGB."""
    lo, hi = max(0, int(y0)), min(H, int(math.ceil(y1)))
    if hi <= lo:
        return
    band = accumulator[lo:hi, BEAM_X0:BEAM_X1]
    accumulator[lo:hi, BEAM_X0:BEAM_X1] = (
        band * (1.0 - alpha) + np.array(color, dtype=float) * alpha)


def soft_beam(accumulator, half, color_core, color_edge, alpha_scale=1.0):
    """가장자리로 갈수록 옅어지는 빔. 상용 게임 레이저의 핵심은 이 감쇠다."""
    for offset in range(int(half) + 1):
        # 가우시안 감쇠 — 선형보다 코어가 단단하고 바깥이 부드럽다.
        t = offset / max(1.0, half)
        falloff = math.exp(-(t ** 2) * 3.2)
        color = tuple(
            color_core[i] * (1 - t) + color_edge[i] * t for i in range(3))
        alpha = min(1.0, falloff * alpha_scale)
        add_layer(accumulator, BEAM_Y - offset - 1, BEAM_Y - offset, color, alpha)
        add_layer(accumulator, BEAM_Y + offset, BEAM_Y + offset + 1, color, alpha)


def flow(accumulator, half, phase=0.0):
    """길이 방향 에너지 흐름 — 밝기 파동이 총구에서 선단으로 흐른다."""
    xs = np.arange(BEAM_X0, BEAM_X1)
    wave = 0.5 + 0.5 * np.sin(xs * 0.12 + phase)
    lo, hi = BEAM_Y - int(half * 0.45), BEAM_Y + int(half * 0.45) + 1
    region = accumulator[lo:hi, BEAM_X0:BEAM_X1]
    accumulator[lo:hi, BEAM_X0:BEAM_X1] = np.clip(
        region + wave[None, :, None] * 55.0, 0, 255)


def cap(image, x, size, color, spikes=True):
    """총구/착탄 플레어. 빔이 '어디서 나와 어디에 닿는지'를 말해 준다."""
    draw = ImageDraw.Draw(image, "RGBA")
    for r in range(int(size), 0, -1):
        a = int(200 * (1 - r / size) ** 2)
        draw.ellipse([x - r, BEAM_Y - r, x + r, BEAM_Y + r], fill=color + (a,))
    if not spikes:
        return
    for k in range(8):
        angle = k * math.pi / 4
        length = size * (1.9 if k % 2 == 0 else 1.15)
        draw.line(
            [x, BEAM_Y,
             x + math.cos(angle) * length, BEAM_Y + math.sin(angle) * length],
            fill=color + (150,), width=1)


def render(kind):
    image = canvas()
    accumulator = np.asarray(image).astype(float)

    if kind == "A":
        # 기존안: 단색 띠 + 단색 코어. 경계가 딱 떨어져 '색칠한 막대'로 보인다.
        add_layer(accumulator, BEAM_Y - 9, BEAM_Y + 9, (232, 74, 42), 0.55)
        add_layer(accumulator, BEAM_Y - 3, BEAM_Y + 3, (255, 210, 170), 1.0)

    elif kind == "B":
        # 다층 감쇠: 넓은 외곽 글로우 → 본체 → 흰 코어.
        soft_beam(accumulator, 26, (255, 120, 60), (120, 30, 20), 0.30)
        soft_beam(accumulator, 11, (255, 190, 120), (230, 90, 40), 0.85)
        add_layer(accumulator, BEAM_Y - 2, BEAM_Y + 2, (255, 252, 246), 1.0)

    elif kind == "C":
        # B + 양 끝 캡 (총구 플레어 · 착탄 스플래시)
        soft_beam(accumulator, 26, (255, 120, 60), (120, 30, 20), 0.30)
        soft_beam(accumulator, 11, (255, 190, 120), (230, 90, 40), 0.85)
        add_layer(accumulator, BEAM_Y - 2, BEAM_Y + 2, (255, 252, 246), 1.0)

    elif kind == "D":
        # C + 길이 방향 에너지 흐름
        soft_beam(accumulator, 28, (255, 120, 60), (120, 30, 20), 0.30)
        soft_beam(accumulator, 12, (255, 190, 120), (230, 90, 40), 0.85)
        flow(accumulator, 12, phase=0.0)
        add_layer(accumulator, BEAM_Y - 2, BEAM_Y + 2, (255, 252, 246), 1.0)

    image = Image.fromarray(np.clip(accumulator, 0, 255).astype("uint8"), "RGB")

    if kind in ("C", "D"):
        cap(image, BEAM_X0, 17, (255, 226, 190))
        cap(image, BEAM_X1, 13, (255, 190, 140), spikes=False)

    return image


LABELS = {
    "A": "A  기존안 — 단색 띠 + 단색 코어",
    "B": "B  다층 감쇠 (외곽 글로우 → 본체 → 흰 코어)",
    "C": "C  B + 총구 플레어 · 착탄 스플래시",
    "D": "D  C + 길이 방향 에너지 흐름",
}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    # 한글 라벨이라 한글 글리프가 있는 폰트를 써야 한다 — arial은 네모로 찍힌다.
    font = None
    for name in ("malgunbd.ttf", "malgun.ttf", "NanumGothicBold.ttf", "gulim.ttc"):
        try:
            font = ImageFont.truetype(name, 15)
            break
        except OSError:
            continue
    if font is None:
        font = ImageFont.load_default()

    kinds = ["A", "B", "C", "D"]
    sheet = Image.new("RGB", (W + 20, (H + 34) * len(kinds) + 10), (10, 10, 14))
    draw = ImageDraw.Draw(sheet)
    for index, kind in enumerate(kinds):
        panel = render(kind)
        panel.save(OUT / f"laser_{kind}.png")
        y = 10 + index * (H + 34)
        draw.text((10, y), LABELS[kind], fill=(232, 238, 248), font=font)
        sheet.paste(panel, (10, y + 20))
    path = OUT / "laser_compare.png"
    sheet.save(path)
    print(f"saved {path}  {sheet.size[0]}x{sheet.size[1]}")


if __name__ == "__main__":
    main()
