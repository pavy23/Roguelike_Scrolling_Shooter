"""doc_shots.js가 찍은 스크린샷을 README용 시트로 합친다.

왜 스크립트인가: docs/screenshots/*.png는 손으로 찍어 붙여 온 것이라 게임이 바뀌어도
아무도 다시 만들지 않았다. 캡처(doc_shots.js)와 합성(이 파일)을 코드로 두면
"빌드 → 캡처 → 합성"이 한 번에 돌아간다.

실행: python Tools/QaHarness/compose_docs.py
"""
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(__file__)
SRC = os.path.join(HERE, "out", "docs")
DST = os.path.join(HERE, "..", "..", "docs", "screenshots")

# 브라우저 캔버스에서 게임 화면만 잘라 내는 창 (1300x760 캡처 기준)
VIEW = (170, 55, 1130, 660)

BG = (14, 14, 20)
TITLE_COLOR = (255, 214, 140)
LABEL_COLOR = (226, 232, 240)
SUB_COLOR = (140, 150, 170)


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGB").crop(VIEW)


def strip(panels, title, subtitle, out_name, panel_width=680):
    """가로 스트립 시트. panels = [(이미지, 큰 라벨, 작은 라벨), ...]"""
    ratio = (VIEW[3] - VIEW[1]) / (VIEW[2] - VIEW[0])
    pw = panel_width
    ph = int(pw * ratio)
    gap = 18
    head = 74
    foot = 54
    width = len(panels) * pw + (len(panels) - 1) * gap
    sheet = Image.new("RGB", (width, head + ph + foot), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((4, 14), title, fill=TITLE_COLOR)
    draw.text((4, 34), subtitle, fill=SUB_COLOR)
    draw.line([(0, head - 12), (width, head - 12)], fill=(48, 52, 66))

    for index, (image, label, note) in enumerate(panels):
        x = index * (pw + gap)
        sheet.paste(image.resize((pw, ph), Image.LANCZOS), (x, head))
        draw.rectangle([x, head, x + pw - 1, head + ph - 1], outline=(60, 66, 84))
        draw.text((x + 4, head + ph + 10), label, fill=LABEL_COLOR)
        if note:
            draw.text((x + 4, head + ph + 26), note, fill=SUB_COLOR)
        if index < len(panels) - 1:
            cy = head + ph // 2
            cx = x + pw + gap // 2
            draw.polygon([(cx - 5, cy - 8), (cx + 6, cy), (cx - 5, cy + 8)],
                         fill=(255, 190, 90))

    os.makedirs(DST, exist_ok=True)
    path = os.path.join(DST, out_name)
    sheet.save(path)
    print(f"{out_name}  {sheet.size[0]}x{sheet.size[1]}")


def single(name, out_name):
    os.makedirs(DST, exist_ok=True)
    image = Image.open(os.path.join(SRC, name)).convert("RGB")
    path = os.path.join(DST, out_name)
    image.save(path)
    print(f"{out_name}  {image.size[0]}x{image.size[1]}")


def main():
    single("title.png", "title.png")
    single("battle_early.png", "battle_early.png")
    load("leviathan.png").save(os.path.join(DST, "b39_leviathan.png"))
    print("b39_leviathan.png")

    strip(
        # 라벨은 영문이다 — PIL 기본 폰트에 한글 글리프가 없어 네모로 찍힌다.
        [(load("boss_scrapyard.png"), "1  SCRAPYARD", "salvage yard"),
         (load("boss_hive.png"), "2  BIO HIVE", "sever the legs to break the shield"),
         (load("warship_70s.png"), "3  FORTRESS", "the whole fight is one warship"),
         (load("boss_nebula.png"), "4  NEBULA", "storm front"),
         (load("boss_core.png"), "5  CORE", "your stage-1 ghost fights beside you")],
        "ROGUELIKE SCROLLING SHOOTER - STAGE JOURNEY",
        "5 SECTORS   SCRAPYARD > HIVE > FORTRESS > NEBULA > CORE",
        "stage_flow_overview.png")

    strip(
        # 문구는 화면에 실제로 보이는 것만 적는다. 1막 잠김은 앵커 -9에서 -4로
        # 완화됐다(엔진을 y=0에서 때릴 수 있어야 해서) — "화면 아래로 잠긴다"는
        # 옛 설계 문구를 그대로 쓰면 사진과 어긋난다.
        [(load("warship_10s.png"), "ACT 1  STERN", "hull sits low - break the deck thruster"),
         (load("warship_70s.png"), "ACT 2  HULL", "rises to centre - turret line"),
         (load("warship_150s.png"), "ACT 3  BOW", "core, then a robot climbs out")],
        "STAGE 3 - ONE GIANT WARSHIP",
        "34x17 UNITS ON A 40x22.5 SCREEN",
        "stage3_warship_flow.png")


if __name__ == "__main__":
    main()
