"""전함 파츠를 **함체 그림에서 오려내** 만든다.

왜 이렇게 하나:
    엔진·코어를 따로 그려 얹었더니 어디에 놓아도 "배 위에 붙인 판때기"로 보였다.
    밝기를 맞추고 모서리를 깎고 위치를 갑판에 맞춰도 마찬가지였다 — 함체는 픽셀
    단위로 촘촘한 그림인데 파츠는 매끈한 도형이라, 애초에 같은 그림이 아니었다.
    사람 지적 2026-08-03: "좀더 전함 그림과 조화로운 디자인이 필요해."

    그래서 하이브에서 통한 방법을 그대로 쓴다: **새로 그리지 않고 잘라 쓴다.**
    파츠 스프라이트는 함체의 그 자리 픽셀 그대로이고, 그 위에 배기구/코어 발광만
    새긴다. 파츠가 멀쩡할 때는 배와 완전히 같은 재질이고, 파괴되면 그 구획만
    그을린 색으로 물든다.

    자르는 위치는 GameData의 파츠 오프셋에서 읽는다 — 데이터가 움직이면 그림도
    따라 움직인다. 손으로 맞춘 좌표는 반드시 언젠가 어긋난다.

주의: 파츠 사각형이 **실제 선체 위**에 있어야 한다. 하늘을 자르면 투명한 조각이
나와 아무것도 안 보인다. 이 스크립트는 그 경우 경고한다.

실행: python Tools/ArtGen/cut_warship_parts.py
"""
import json
import os

from PIL import Image

HERE = os.path.dirname(__file__)
HULL = os.path.join(HERE, "..", "..", "..", "art-input", "warship_hull.png")
WAVES = os.path.join(HERE, "..", "..", "GameData", "waves.json")
DST = os.path.join(HERE, "..", "..", "..", "art-input")
PPU = 16

# 파츠 id → 출력 파일명. 포탑은 자르지 않는다 — 갑판 위로 솟은 별개 물체라
# 함체에서 오려내면 아무것도 안 나온다(그 자리는 하늘이다).
CUTS = {"engine": "warship_stern", "core": "warship_core"}


def fortress():
    with open(WAVES, encoding="utf-8") as handle:
        data = json.load(handle)
    return next(b for b in data["bosses"] if b["id"] == "boss_fortress")


def vent_rows(part, pw, ph, seed):
    """배기 슬릿을 새긴다.

    한 줄을 끝에서 끝까지 곧게 그으면 스티커처럼 보인다. 장갑판을 따라 **끊어진
    구간**으로 새기고, 위에 어두운 그림자 한 줄을 둬 파인 것으로 읽히게 한다.
    난수는 쓰지 않는다 — 같은 입력이면 같은 그림이어야 한다.
    """
    px = part.load()
    for index, ratio in enumerate((0.30, 0.50, 0.70)):
        row = int(ph * ratio)
        x = int(pw * 0.10)
        segment = 0
        while x < int(pw * 0.90):
            noise = ((x * 7919 + seed * 104729 + index * 15485863) % 101) / 100.0
            length = int(pw * (0.16 + noise * 0.16))
            gap = int(pw * (0.04 + noise * 0.05))
            for step in range(length):
                cx = x + step
                if cx >= pw:
                    break
                if row - 1 >= 0 and px[cx, row - 1][3]:
                    px[cx, row - 1] = (22, 16, 18, 255)          # 파인 그림자
                for dy, color in ((0, (255, 190, 96, 255)),
                                  (1, (214, 96, 28, 255)),
                                  (2, (150, 58, 20, 255))):
                    cy = row + dy
                    if cy < ph and px[cx, cy][3]:
                        px[cx, cy] = color
            x += length + gap
            segment += 1


def core_glow(part, pw, ph):
    """코어는 장갑 안쪽이 타오르는 것으로 — 원형 발광을 선체 픽셀 위에만 새긴다."""
    px = part.load()
    cx, cy = pw // 2, ph // 2
    rx, ry = pw * 0.30, ph * 0.30
    for y in range(ph):
        for x in range(pw):
            if not px[x, y][3]:
                continue
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if d > 1.0:
                continue
            if d > 0.72:
                px[x, y] = (150, 58, 20, 255)
            elif d > 0.35:
                px[x, y] = (214, 96, 28, 255)
            elif d > 0.12:
                px[x, y] = (255, 176, 64, 255)
            else:
                px[x, y] = (255, 236, 190, 255)


def main():
    hull = Image.open(HULL).convert("RGBA")
    w, h = hull.size
    cx, cy = w // 2, h // 2
    boss = fortress()

    for index, part in enumerate(boss["parts"]):
        name = CUTS.get(part["id"])
        if name is None:
            continue
        pw = int(round(part["halfWidth"] * 2 * PPU))
        ph = int(round(part["halfHeight"] * 2 * PPU))
        x0 = cx + int(round(part["offsetX"] * PPU)) - pw // 2
        y0 = cy - int(round(part["offsetY"] * PPU)) - ph // 2
        cut = hull.crop((x0, y0, x0 + pw, y0 + ph)).copy()

        opaque = sum(1 for p in cut.getdata() if p[3] > 0)
        coverage = opaque / float(pw * ph)
        if part["id"] == "core":
            core_glow(cut, pw, ph)
        else:
            vent_rows(cut, pw, ph, index + 1)
        cut.save(os.path.join(DST, f"{name}.png"))
        flag = "" if coverage >= 0.55 else "  <-- 선체를 거의 안 덮는다. 오프셋 확인!"
        print(f"{name}: {pw}x{ph}  선체 덮임 {coverage * 100:.0f}%{flag}")


if __name__ == "__main__":
    main()
