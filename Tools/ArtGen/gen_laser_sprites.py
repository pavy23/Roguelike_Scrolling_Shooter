#!/usr/bin/env python3
"""레이저 빔·캡 스프라이트 생성 (사람 확정: 후보 C).

왜 스프라이트인가: 부드러운 감쇠를 사각형 여러 겹으로 흉내 내면 겹마다 드로우콜이
붙고 경계가 계단으로 남는다. 감쇠를 **알파에 구워** 두면 한 겹으로 끝나고, 가로로
아무리 늘려도 세로 단면이 그대로 유지된다.

  laser_soft.png  8×64  세로 가우시안 감쇠, 가로 균일 (빔 본체·외곽 공용)
  laser_cap.png   48×48 방사 플레어 + 십자 스파이크 (총구·착탄 공용)

둘 다 흰색이다 — 색은 런타임 틴트로 준다(플레이어 청록 / 적 주황).

실행: python Tools/ArtGen/gen_laser_sprites.py
"""
import math
import pathlib

import numpy as np
from PIL import Image

# art-input은 저장소 **바깥**이다 (main/ 옆). parents[2]는 main/ 이라 한 단계 더 간다 —
# 처음에 이걸 틀려서 main/art-input에 썼고, 씬 빌더는 그쪽을 보지 않는다.
OUT = pathlib.Path(__file__).resolve().parents[3] / "art-input"


def beam_strip(width=8, height=64):
    ys = np.arange(height) + 0.5
    t = np.abs(ys - height / 2.0) / (height / 2.0)
    # 가우시안. 코어는 단단하고 바깥으로 갈수록 급히 옅어진다 — 선형 감쇠는
    # 가운데가 흐려 '빛나는 선'이 아니라 '뿌연 띠'가 된다.
    alpha = np.exp(-(t ** 2) * 5.5)
    alpha[alpha < 0.004] = 0.0
    rgba = np.zeros((height, width, 4), dtype=np.uint8)
    rgba[:, :, :3] = 255
    rgba[:, :, 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)[:, None]
    return Image.fromarray(rgba, "RGBA")


def cap(size=48):
    center = size / 2.0
    yy, xx = np.mgrid[0:size, 0:size]
    dx, dy = xx + 0.5 - center, yy + 0.5 - center
    r = np.sqrt(dx * dx + dy * dy) / center
    core = np.exp(-(r ** 2) * 7.0)

    # 십자 스파이크 — 렌즈 플레어의 그 선이다. 점광원에 '터졌다'는 인상을 준다.
    angle = np.arctan2(dy, dx)
    spike = (np.abs(np.cos(2.0 * angle)) ** 22) * np.exp(-r * 3.6)
    alpha = np.clip(core + spike * 0.85, 0.0, 1.0)
    alpha[r > 1.0] = 0.0
    alpha[alpha < 0.004] = 0.0

    rgba = np.zeros((size, size, 4), dtype=np.uint8)
    rgba[:, :, :3] = 255
    rgba[:, :, 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for name, image in (("laser_soft", beam_strip()), ("laser_cap", cap())):
        path = OUT / f"{name}.png"
        image.save(path)
        print(f"saved {path}  {image.size[0]}x{image.size[1]}")


if __name__ == "__main__":
    main()
