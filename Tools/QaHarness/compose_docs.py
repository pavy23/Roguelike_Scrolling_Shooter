"""doc_shots.js가 찍은 스크린샷을 README용 시트로 합친다.

왜 스크립트인가: docs/screenshots/*.png는 손으로 찍어 붙여 온 것이라 게임이 바뀌어도
아무도 다시 만들지 않았다. 캡처(doc_shots.js)와 합성(이 파일)을 코드로 두면
"빌드 → 캡처 → 합성"이 한 번에 돌아간다.

실행: python Tools/QaHarness/compose_docs.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(__file__)
SRC = os.path.join(HERE, "out", "docs")
DST = os.path.join(HERE, "..", "..", "docs", "screenshots")

# 브라우저 캔버스에서 게임 화면만 잘라 내는 창 (1300x760 캡처 기준)
VIEW = (170, 55, 1130, 660)

BG = (14, 14, 20)
TITLE_COLOR = (255, 214, 140)
LABEL_COLOR = (226, 232, 240)
SUB_COLOR = (140, 150, 170)



def _big_font(size):
    """굵고 큰 글리프. PIL 기본 폰트는 확대가 안 돼서 물음표가 점처럼 찍힌다."""
    for name in ("arialbd.ttf", "arial.ttf", "seguisb.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def redact_boss(image, question_scale=0.42):
    """히든 보스를 **검은 실루엣 + ?** 로 가린다.

    사람 지시 2026-08-04: "히든 보스는 실루엣을 검정 +? 표 처리해줘 ㅋㅋ
    스포일러잖아". README는 이 게임을 처음 보는 사람이 읽는 문서다 — 두 거대
    보스의 정체는 조건 두 개를 채워야 열리는 것이라, 사진 한 장으로 미리
    보여 주면 그 보상이 사라진다.

    가리는 대상은 좌표가 아니라 **연결 성분**으로 찾는다. 보스는 배경과 확실히
    다른 색의 커다란 덩어리 하나다 — 가장 밝은 점에서 시작해 배경이 아닌
    이웃으로 번져 나가면 촉수 끝까지 따라간다. 좌표를 박아 두면 시드나 연출이
    바뀔 때 엉뚱한 곳을 칠하고, 그것을 알아차릴 방법이 없다.
    """
    rgb = np.asarray(image.convert("RGB")).astype(np.int16)
    h, w = rgb.shape[:2]

    # 배경색 = 화면에서 가장 흔한 색. 그것과 충분히 다른 픽셀만 후보다.
    flat = (rgb[:, :, 0] // 8 * 1024 + rgb[:, :, 1] // 8 * 32 + rgb[:, :, 2] // 8)
    background = np.bincount(flat.ravel()).argmax()
    base = np.array([(background // 1024) * 8 + 4,
                     (background // 32 % 32) * 8 + 4,
                     (background % 32) * 8 + 4], dtype=np.int16)
    foreground = np.abs(rgb - base).sum(axis=2) > 48

    # 덩어리 안의 어두운 외곽선이 연결을 끊는다 — 머리만 칠하고 몸통을 놓친
    # 적이 있다. 번지기 전에 한 번 닫아(팽창→수축) 그 틈을 메운다.
    def _dilate(mask, times):
        for _ in range(times):
            grown = mask.copy()
            grown[1:, :] |= mask[:-1, :]
            grown[:-1, :] |= mask[1:, :]
            grown[:, 1:] |= mask[:, :-1]
            grown[:, :-1] |= mask[:, 1:]
            mask = grown
        return mask

    foreground = ~_dilate(~_dilate(foreground, 3), 3)

    # HUD(위/아래 띠)는 보스가 아니다 — 성분이 거기까지 번지지 않게 잘라 둔다.
    margin = int(h * 0.16)
    foreground[:margin, :] = False
    foreground[h - margin:, :] = False

    if not foreground.any():
        return image

    # **가장 큰 연결 성분**을 고른다. 한때는 "가장 두꺼운 곳"에서 번지게 했는데,
    # 그 기준은 틈을 메우고 나면 플레이어 기체를 골랐다 — 실제로 기체가 검게
    # 칠해진 사진이 나왔다. 크기는 그런 식으로 흔들리지 않는다.
    #
    # 라벨링은 최대값 전파로 한다: 전경 픽셀마다 고유 번호를 주고 이웃 중
    # 최대값으로 계속 덮어쓰면, 한 성분 안의 픽셀이 전부 같은 번호로 수렴한다.
    labels = np.where(foreground,
                      np.arange(h * w, dtype=np.int32).reshape(h, w),
                      -1)
    while True:
        spread = labels.copy()
        spread[1:, :] = np.maximum(spread[1:, :], labels[:-1, :])
        spread[:-1, :] = np.maximum(spread[:-1, :], labels[1:, :])
        spread[:, 1:] = np.maximum(spread[:, 1:], labels[:, :-1])
        spread[:, :-1] = np.maximum(spread[:, :-1], labels[:, 1:])
        spread[~foreground] = -1
        if np.array_equal(spread, labels):
            break
        labels = spread

    sizes = np.bincount(labels[foreground].ravel())
    grown = labels == int(np.argmax(sizes))

    # 실루엣을 조금 부풀려 가장자리 잔여 픽셀까지 덮는다.
    mask = _dilate(grown, 4)

    out = np.asarray(image.convert("RGB")).copy()
    out[mask] = (6, 6, 10)
    result = Image.fromarray(out, "RGB")

    ys, xs = np.nonzero(mask)
    cx, cy = int(xs.mean()), int(ys.mean())
    span = min(xs.max() - xs.min(), ys.max() - ys.min())
    draw = ImageDraw.Draw(result)
    font = _big_font(max(24, int(span * question_scale)))
    draw.text((cx, cy), "?", font=font, fill=(236, 240, 250), anchor="mm")
    return result


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGB").crop(VIEW)


def strip(panels, title, subtitle, out_name, panel_width=680, arrow=True):
    """가로 스트립 시트. panels = [(이미지, 큰 라벨, 작은 라벨), ...]

    arrow=False는 **순서가 아니라 선택지**일 때 쓴다 — 화살표는 "다음"을 뜻하므로
    둘 중 하나로 갈리는 관계에 붙이면 거짓말이 된다."""
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
        if arrow and index < len(panels) - 1:
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
    # 히든 보스는 정체를 가린다 (사람 지시 2026-08-04 — 스포일러).
    redact_boss(load("hidden_boss.png")).save(
        os.path.join(DST, "b39_leviathan.png"))
    print("b39_leviathan.png (redacted)")

    strip(
        # 어느 거대 보스가 나오느냐로 **갈리는** 것이지 이어지는 것이 아니다.
        # 그리고 보스 이름은 적지 않는다 — 사람 지시대로 정체는 가린다.
        [(load("hidden_abyss.png"), "ABYSS", "a sunken trench, far below the map"),
         (load("hidden_brood.png"), "BROOD", "inside something alive")],
        "UNCHARTED - A PLACE, NOT A REMATCH",
        "WHAT WAITS THERE DECIDES WHERE YOU GO   NO MID-BOSS",
        "hidden_biomes.png", arrow=False)

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
