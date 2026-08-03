"""하이브 보스를 **한 장의 완성 렌더에서 잘라** 파츠로 만든다.

왜 이렇게 하나:
    이전 방식은 몸통(boss_hive_body)과 다리(hive_leg)를 **각각 따로 생성**해서
    이어 붙였다. 두 그림은 광원도, 골격 두께도, 색조도 달랐고 골반 높이도
    맞지 않아 관절이 "뚝 끊긴" 것처럼 보였다 — 사람 보고 2026-08-03.

    그림을 새로 뽑아 맞추려 해도 생성기는 매번 다른 그림을 준다. 그래서 방향을
    뒤집었다: **이미 완성되어 있는 전신 렌더 하나(boss_hive_tall.png)를 잘라
    쓴다.** 자른 조각을 같은 캔버스 위에 그대로 두므로, 다리가 붙어 있는 동안은
    원본과 **픽셀 단위로 동일**하다. 관절이 어긋날 여지 자체가 없다.

출력(전부 128x256 동일 캔버스 — 유니티에서 전부 같은 위치에 겹쳐 그리면 된다):
    boss_hive_torso.png    몸통 + 허벅지 그루터기 + 꼬리 (다리 제외)
    boss_hive_leg_l.png    왼 다리 (절단선 아래)
    boss_hive_leg_r.png    오른 다리 (절단선 아래, 꼬리는 몸통에 남긴다)
    boss_hive_wound_l.png  왼 다리 절단면 (다리가 파괴된 동안만 켠다)
    boss_hive_wound_r.png  오른 다리 절단면
    boss_hive_shield.png   머리 실드 (원본에 구워져 있던 청록 오라를 떼어낸 것)

실행: python Tools/ArtGen/cut_hive_parts.py
"""
import colorsys
import os
from collections import deque

from PIL import Image

SRC = os.path.join(os.path.dirname(__file__), "out", "final", "boss_hive_tall.png")
# 씬 빌더가 art-input을 원본으로 삼아 Assets/Art/Sprites로 복사해 간다.
# Assets 쪽에 바로 쓰면 다음 씬 재생성에서 art-input의 옛 그림에 덮인다 - 실제로 겪었다.
DST = os.path.join(os.path.dirname(__file__), "..", "..", "..", "art-input")

# 절단선. 무릎 바로 아래로 잡는다.
#
# 처음엔 사타구니 바로 아래(156)에서 잘랐더니 다리가 통째로 사라져 "다리가
# 잘렸다"가 아니라 "다리가 없어졌다"로 보였다. 무릎 아래에서 자르면 허벅지와
# 무릎이 남아 **잘린 것**으로 읽힌다. 좌우 다리가 확실히 분리된 행이어야 한다.
SEAM_Y = 178

# 꼬리와 오른 다리는 한 덩어리로 잡힌다. 꼬리가 종아리 뒤를 지나며 여러 번
# 스치기 때문에, 플러드필로 떼려 하면 어디선가 반드시 새어 나간다(실제로 두 번
# 샜다 - 한 번은 꼬리가 다리를 삼켰고, 한 번은 다리가 발을 잃었다).
#
# 그래서 흐름 대신 **경계선**을 직접 긋는다. 종아리 위쪽에서는 꼬리가 멀리
# 오른쪽에 있고, 아래쪽에서는 꼬리가 안쪽으로 감겨 들어온다. 두 구간의 경계값만
# 다르게 주면 충분하다.
TAIL_SPLIT_Y = 215
TAIL_SPLIT_X_ABOVE = 106
TAIL_SPLIT_X_BELOW = 90


def load():
    im = Image.open(SRC).convert("RGBA")
    return im, im.load(), im.size


def is_aura(r, g, b):
    """구워져 있는 청록 실드 오라. 몸 색(올리브/자주)과 색상환에서 확실히 떨어져 있다."""
    h, s, _ = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    return 0.40 <= h <= 0.62 and s > 0.12


def split_aura(px, w, h):
    """오라를 떼어내고, 몸 안쪽에 뚫린 구멍은 주변 몸 색으로 메운다.

    오라는 머리 위를 **덮으며** 그려져 있어서 그냥 지우면 머리에 구멍이 난다.
    바깥(투명 배경과 이어진 곳)은 지우고, 몸에 둘러싸인 구멍만 메운다.
    """
    aura = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a and is_aura(r, g, b):
                aura[y][x] = True

    # 지운 자리 + 원래 투명한 곳 중 테두리에서 닿을 수 있는 곳 = 진짜 바깥
    hole = [[aura[y][x] or px[x, y][3] == 0 for x in range(w)] for y in range(h)]
    outside = [[False] * w for _ in range(h)]
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if hole[y][x]:
                outside[y][x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if hole[y][x] and not outside[y][x]:
                outside[y][x] = True
                q.append((y, x))
    while q:
        cy, cx = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w and hole[ny][nx] and not outside[ny][nx]:
                outside[ny][nx] = True
                q.append((ny, nx))
    return aura, outside


def nearest_body(px, aura, w, h, x, y):
    """가장 가까운 '몸' 픽셀 색. 오라가 덮어쓴 자리를 메우는 데 쓴다."""
    for radius in range(1, 8):
        for dy in range(-radius, radius + 1):
            for dx in range(-radius, radius + 1):
                nx, ny = x + dx, y + dy
                if not (0 <= nx < w and 0 <= ny < h):
                    continue
                r, g, b, a = px[nx, ny]
                if a and not aura[ny][nx]:
                    return (r, g, b, 255)
    return (0, 0, 0, 0)


def components_below(alpha, w, h, seam):
    lab = [[0] * w for _ in range(h)]
    comps = []
    cur = 0
    for y in range(seam, h):
        for x in range(w):
            if alpha[y][x] and lab[y][x] == 0:
                cur += 1
                q = deque([(y, x)])
                lab[y][x] = cur
                cells = []
                while q:
                    cy, cx = q.popleft()
                    cells.append((cy, cx))
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            ny, nx = cy + dy, cx + dx
                            if seam <= ny < h and 0 <= nx < w and alpha[ny][nx] and lab[ny][nx] == 0:
                                lab[ny][nx] = cur
                                q.append((ny, nx))
                comps.append(cells)
    return comps


def carve_tail(cells, alpha, w, h):
    """오른 다리 덩어리에서 꼬리를 떼어낸다 (경계선 방식 - 위 상수 설명 참조)."""
    tail = set()
    for cy, cx in cells:
        limit = TAIL_SPLIT_X_ABOVE if cy < TAIL_SPLIT_Y else TAIL_SPLIT_X_BELOW
        if cx > limit:
            tail.add((cy, cx))
    return tail


def blank(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def build_wound(torso, cells, w, h, seed):
    """다리가 잘린 자리에 덧그릴 **찢긴 그루터기**를 만든다.

    처음엔 몸통 밑동을 직접 깎으려 했는데, 몸통은 다리가 멀쩡할 때도 그려지는
    그림이라 깎으면 멀쩡한 상태에서 관절이 벌어진다. 그래서 몸통은 손대지 않고,
    **절단선 아래로 이어지는 조각을 따로** 만들어 다리가 파괴됐을 때만 덧그린다.
    몸통의 평평한 밑동은 이 조각에 덮여 실루엣 안쪽으로 들어가므로 보이지 않는다.

    조각 구성 — 실제로 뭘 자른 것처럼 읽히도록:
        위쪽은 허벅지 살결을 그대로 이어받아 아래로 갈수록 어두워지고 좁아진다.
        끝은 그을린 검정 테두리, 그 안은 찢긴 살, 한가운데는 코어와 같은 주황 체액.
        몇 가닥은 아래로 흘러내린다.

    난수는 쓰지 않는다 — 같은 입력이면 같은 그림이 나와야 한다(AGENTS.md §4의
    정신은 아트 파이프라인에도 그대로 적용된다). 자리마다 고정된 해시를 쓴다.
    """
    tp = torso.load()
    img = blank(w, h)
    out = img.load()

    xs = sorted({cx for cy, cx in cells if cy < SEAM_Y + 3})
    if not xs:
        return img
    x0, x1 = min(xs), max(xs)
    span = max(1, x1 - x0)

    def noise(x, salt):
        return ((x * 7919 + seed * 104729 + salt * 15485863) % 1009) / 1008.0

    def thigh_color(x):
        """몸통에서 이 열의 마지막 살 색을 가져온다 — 이어붙인 티가 안 나게."""
        for y in range(SEAM_Y - 1, SEAM_Y - 12, -1):
            if 0 <= y < h and tp[x, y][3]:
                return tp[x, y]
        return None

    for x in range(x0, x1 + 1):
        base = thigh_color(x)
        if base is None:
            continue
        t = (x - x0) / span
        bulge = 1.0 - abs(t * 2.0 - 1.0)          # 가운데가 길게 남고 가장자리가 짧다
        length = int(5 + noise(x, 1) * 4 + bulge * 9)

        for k in range(length):
            y = SEAM_Y + k
            if not (0 <= y < h):
                break
            # 아래로 갈수록 좁아진다 — 직사각형 토막이 아니라 찢긴 모양이 되게.
            inset = int(k * 0.5 + noise(x, 4) * k * 0.5)
            if x - x0 < inset or x1 - x < inset:
                break
            tail_rows = 2 if length <= 6 else 3
            if k >= length - tail_rows:
                d = length - 1 - k
                if d == 0:
                    col = (24, 14, 18, 255)                    # 그을린 테두리
                elif bulge > 0.62 and d == 1:
                    col = (255, 168, 56, 255)                  # 코어와 같은 주황 체액
                else:
                    col = (96, 34, 28, 255)                    # 찢긴 살
            else:
                shade = 1.0 - min(0.55, k * 0.075)
                col = (int(base[0] * shade), int(base[1] * shade), int(base[2] * shade), 255)
            out[x, y] = col

        if noise(x, 2) > 0.8:
            drip = 2 + int(noise(x, 3) * 5)
            for k in range(drip):
                y = SEAM_Y + length + k
                if not (0 <= y < h):
                    break
                fade = 255 - int(k / max(1, drip) * 140)
                out[x, y] = (150, 52, 30, fade) if k % 2 else (96, 34, 26, fade)
    return img


def keep_main_body(img, w, h):
    """(쓰지 않는다) 몸에서 떨어져 나온 파편을 지운다.

    한 번 넣었다가 뺐다: 꼬리는 원본에서도 **다리 뒤를 지나느라** 몸통과 픽셀로
    이어져 있지 않다. 그래서 이 청소를 돌리면 꼬리가 통째로 사라진다.
    남는 자잘한 조각(발가락 등)은 실제로는 꼬리 끝이었고, 지울 이유가 없었다.
    """
    px = img.load()
    seen = [[False] * w for _ in range(h)]
    best = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0 or seen[y][x]:
                continue
            q = deque([(y, x)])
            seen[y][x] = True
            cells = []
            while q:
                cy, cx = q.popleft()
                cells.append((cy, cx))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < h and 0 <= nx < w and not seen[ny][nx] and px[nx, ny][3]:
                            seen[ny][nx] = True
                            q.append((ny, nx))
            if len(cells) > len(best):
                best = cells
    keep = set(best)
    dropped = 0
    for y in range(h):
        for x in range(w):
            if px[x, y][3] and (y, x) not in keep:
                px[x, y] = (0, 0, 0, 0)
                dropped += 1
    return dropped


def main():
    im, px, (w, h) = load()
    aura, outside = split_aura(px, w, h)

    shield = blank(w, h)
    sp = shield.load()
    body = blank(w, h)
    bp = body.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if not a:
                continue
            if aura[y][x]:
                sp[x, y] = (r, g, b, a)
                bp[x, y] = (0, 0, 0, 0) if outside[y][x] else nearest_body(px, aura, w, h, x, y)
            else:
                bp[x, y] = (r, g, b, a)

    alpha = [[body.getpixel((x, y))[3] > 0 for x in range(w)] for y in range(h)]
    comps = sorted(components_below(alpha, w, h, SEAM_Y), key=len, reverse=True)
    if len(comps) < 2:
        raise SystemExit("절단선 아래에서 다리 덩어리를 못 찾았다 — SEAM_Y를 확인하라.")

    big = comps[:2]
    tail = carve_tail(big[0], alpha, w, h)
    legs = [set(big[0]) - tail, set(big[1])]
    legs.sort(key=lambda cells: sum(cx for _, cx in cells) / len(cells))   # 왼쪽 먼저
    if not tail:
        raise SystemExit("꼬리를 분리하지 못했다. TAIL_SEED가 절단선 아래 꼬리 위에 있는지 확인하라.")

    torso = body.copy()
    tp = torso.load()
    for cells in legs:
        for cy, cx in cells:
            tp[cx, cy] = (0, 0, 0, 0)

    names = ("l", "r")
    for i, cells in enumerate(legs):
        leg = blank(w, h)
        lp = leg.load()
        for cy, cx in cells:
            lp[cx, cy] = body.getpixel((cx, cy))
        leg.save(os.path.join(DST, f"boss_hive_leg_{names[i]}.png"))
        build_wound(torso, cells, w, h, i + 1).save(
            os.path.join(DST, f"boss_hive_wound_{names[i]}.png"))

    torso.save(os.path.join(DST, "boss_hive_torso.png"))
    shield.save(os.path.join(DST, "boss_hive_shield.png"))
    print(f"잘랐다: 절단선 y={SEAM_Y}, 다리 {[len(c) for c in legs]}px, 꼬리 {len(tail)}px")


if __name__ == "__main__":
    main()
