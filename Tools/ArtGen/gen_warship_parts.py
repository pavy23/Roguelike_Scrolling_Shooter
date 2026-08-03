"""전함 하드포인트(엔진·포탑·코어)를 **함체 팔레트로 직접 그린다**.

왜 생성이 아니라 직접 그리기인가:
    이 자리에는 원래 다른 보스의 스프라이트를 빌려 얹고 있었다(함미=boss_fortress,
    함수=boss_core). 각자 다른 조형·다른 광원이라 배 위에 이물질이 붙은 것처럼
    보였고, 사람이 스크린샷을 보고 "연결부위가 너무너무 어색해"라고 했다.

    새로 생성해도 같은 위험이 남는다 — 생성기는 매번 다른 화풍을 준다. 파츠는
    작고 형태가 단순하니(포탑·배기구·코어) **함체에서 색을 뽑아 직접 그리는 편이**
    확실하다. 하이브에서 실드 돔을 그렇게 해결했던 것과 같은 판단이다.

크기는 GameData의 파츠 판정에서 온다 — 그림이 판정보다 크면 "때렸는데 안 맞는다"가
되고, 작으면 맞을 자리를 놓친다. 값을 여기 박지 않고 waves.json에서 읽는다.

실행: python Tools/ArtGen/gen_warship_parts.py
"""
import json
import os

from PIL import Image

HERE = os.path.dirname(__file__)
HULL = os.path.join(HERE, "..", "..", "..", "art-input", "warship_hull.png")
WAVES = os.path.join(HERE, "..", "..", "GameData", "waves.json")
DST = os.path.join(HERE, "..", "..", "..", "art-input")
PPU = 16


def hull_palette():
    """함체에서 어두운 순으로 색을 뽑는다. 파츠가 배와 같은 재질로 보이게."""
    image = Image.open(HULL).convert("RGBA")
    counts = {}
    for r, g, b, a in image.getdata():
        if a == 0:
            continue
        counts[(r, g, b)] = counts.get((r, g, b), 0) + 1
    ranked = sorted(counts, key=lambda c: -counts[c])[:24]
    ranked.sort(key=lambda c: c[0] + c[1] + c[2])
    return ranked


def part_sizes():
    with open(WAVES, encoding="utf-8") as handle:
        data = json.load(handle)
    boss = next(b for b in data["bosses"] if b["id"] == "boss_fortress")
    sizes = {}
    for part in boss["parts"]:
        sizes[part["id"]] = (
            int(round(part["halfWidth"] * 2 * PPU)),
            int(round(part["halfHeight"] * 2 * PPU)))
    return sizes


def new(size):
    return Image.new("RGBA", size, (0, 0, 0, 0))


def box(px, x0, y0, x1, y1, fill, outline=None):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = x in (x0, x1) or y in (y0, y1)
            px[x, y] = outline if (edge and outline) else fill


def ellipse(px, cx, cy, rx, ry, fill, outline=None):
    for y in range(cy - ry, cy + ry + 1):
        for x in range(cx - rx, cx + rx + 1):
            dx = (x - cx) / max(1, rx)
            dy = (y - cy) / max(1, ry)
            d = dx * dx + dy * dy
            if d <= 1.0:
                px[x, y] = outline if (outline and d > 0.62) else fill


def draw_turret(w, h, pal):
    """갑판 포탑. 진행 방향(왼쪽)을 향한 2연장 포신."""
    dark, mid, light = pal[1], pal[len(pal) // 2], pal[-2]
    outline = pal[0]
    image = new((w, h))
    px = image.load()
    base_top = int(h * 0.55)
    box(px, 2, base_top, w - 3, h - 2, dark, outline)            # 받침
    box(px, 4, int(h * 0.25), w - 6, base_top, mid, outline)     # 포탑 하우징
    box(px, 6, int(h * 0.30), w - 10, int(h * 0.40), light)      # 상단 하이라이트
    barrel = int(h * 0.36)
    for row in (barrel, barrel + int(h * 0.18)):                 # 2연장 포신
        box(px, 0, row, 5, row + 2, mid, outline)
    return image


def draw_engine(w, h, pal):
    """함미 배기구. 주황 슬릿이 뚫려 있어 약점으로 읽힌다."""
    dark, mid = pal[1], pal[len(pal) // 2]
    outline = pal[0]
    image = new((w, h))
    px = image.load()
    # 장갑이 대부분을 차지하고, 빛은 좁은 틈으로만 샌다. 처음엔 안쪽을 통째로
    # 주황으로 채웠더니 배 위에 형광 간판을 붙인 꼴이었다.
    box(px, 1, 1, w - 2, h - 2, dark, outline)
    box(px, 3, 3, w - 4, h - 4, mid)
    box(px, 5, 5, w - 6, h - 6, dark)
    slits = 3
    span = max(4, (h - 16) // slits)
    for index in range(slits):
        top = 8 + index * span
        bottom = top + 2
        if bottom >= h - 8:
            break
        box(px, 9, top - 1, w - 10, bottom + 1, (24, 16, 18, 255))
        box(px, 10, top, w - 11, bottom, (206, 92, 28, 255))
        box(px, 12, top + 1, w - 13, top + 1, (255, 176, 72, 255))
    for x in range(3, w - 3, 6):                                  # 리벳
        px[x, 3] = mid
        px[x, h - 4] = mid
    return image


def draw_core(w, h, pal):
    """함수 코어. 장갑 소켓 안에서 주황 구체가 타오른다."""
    dark, mid = pal[1], pal[len(pal) // 2]
    outline = pal[0]
    image = new((w, h))
    px = image.load()
    cx, cy = w // 2, h // 2
    # 장갑 소켓이 두껍고 빛나는 알맹이는 작다 — 코어는 "약점"이지 조명이 아니다.
    ellipse(px, cx, cy, w // 2 - 1, h // 2 - 1, dark, outline)     # 소켓 장갑
    ellipse(px, cx, cy, w // 2 - 4, h // 2 - 4, mid)
    ellipse(px, cx, cy, int(w * 0.30), int(h * 0.30), (26, 18, 20, 255))
    ellipse(px, cx, cy, int(w * 0.24), int(h * 0.24), (206, 92, 28, 255))
    ellipse(px, cx, cy, int(w * 0.15), int(h * 0.15), (255, 176, 64, 255))
    ellipse(px, cx - 1, cy - 1, max(1, w // 16), max(1, h // 16), (255, 236, 190, 255))
    for angle in range(4):                                         # 장갑 리브
        x = cx + (w // 2 - 3) * (1 if angle % 2 else -1)
        y = cy + (h // 2 - 3) * (1 if angle < 2 else -1)
        px[max(0, min(w - 1, x)), max(0, min(h - 1, y))] = mid
    return image


def main():
    pal = hull_palette()
    sizes = part_sizes()
    jobs = [
        ("warship_turret", draw_turret, sizes["turret_a"]),
        ("warship_stern", draw_engine, sizes["engine"]),
        ("warship_core", draw_core, sizes["core"]),
    ]
    for name, draw, size in jobs:
        image = draw(size[0], size[1], pal)
        image.save(os.path.join(DST, f"{name}.png"))
        print(f"{name}: {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
