#!/usr/bin/env python3
"""에너지 실드 돔 이펙트 (하이브 보스 머리 실드, 2026-08-03 사람 지시).

왜 절차 생성인가: 실드는 **안이 비쳐야** 한다. PixelLab로 두 번 뽑았더니 꽉 찬
원반이 나와 머리를 통째로 가렸다. 반투명·중공(中空)은 알파를 직접 다뤄야 하는
문제라 생성 모델보다 코드가 정확하다. ART-DIRECTION의 "조형급 아트 절차 생성 금지"는
캐릭터·배경 같은 조형에 대한 것이고, 이펙트 오버레이는 그 대상이 아니다
(BossPartsView의 무적 브래킷도 같은 이유로 코드 생성이다).

구성:
  - 바깥 테두리: 밝은 청록 1~2px, 알파 높음 → "막이 여기 있다"
  - 내부: 육각 셀 격자를 아주 옅게 → 힘의 장 질감, 뒤가 비친다
  - 중심: 거의 투명 → 보스 머리가 그대로 읽힌다

사용: python gen_shield_dome.py --out ../../../art-input/fx_shield_dome.png
"""
from __future__ import annotations

import argparse
import math

from PIL import Image

RIM = (150, 245, 255)      # 테두리 — 청록 백열
FILL = (90, 210, 235)      # 내부 셀 선
SIZE = 96


def build(size: int = SIZE) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = (size - 1) / 2.0
    radius = c - 0.5

    for y in range(size):
        for x in range(size):
            dx, dy = x - c, y - c
            d = math.hypot(dx, dy) / radius
            if d > 1.0:
                continue

            # 테두리: 가장자리 8%에 몰아준다. 안쪽으로 갈수록 급격히 옅어진다.
            edge = max(0.0, (d - 0.92) / 0.08)
            alpha = int(235 * edge ** 0.6)
            color = RIM

            if alpha < 30:
                # 육각 셀: 두 방향 격자를 겹쳐 근사한다. 선 위에서만 옅게 칠한다.
                cell = 9.0
                u = (x + y * 0.5) % cell
                v = (y * 0.866) % cell
                on_line = min(u, cell - u) < 0.9 or min(v, cell - v) < 0.9
                # 중심으로 갈수록 더 투명 — 뒤가 보여야 한다.
                depth = 0.25 + 0.75 * d
                alpha = int((46 if on_line else 16) * depth)
                color = FILL

            if alpha <= 0:
                continue
            px[x, y] = (color[0], color[1], color[2], min(255, alpha))
    return img


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--size", type=int, default=SIZE)
    args = parser.parse_args()
    img = build(args.size)
    img.save(args.out)
    opaque = sum(1 for p in img.getdata() if p[3] > 200)
    print(f"saved: {args.out} ({args.size}x{args.size}, 불투명 픽셀 {opaque})")


if __name__ == "__main__":
    main()
