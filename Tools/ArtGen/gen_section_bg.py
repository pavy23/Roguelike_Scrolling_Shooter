#!/usr/bin/env python3
"""gen_section_bg.py — 구간(SectionTheme) 배경 아트 슬롯 20종 생성.

대개편 Phase C 2단계. BattleSceneBuilder.CreateSectionArtSlots가 읽는 키 규칙:
  <prefix>_far_dusk / <prefix>_far_dark / <prefix>_fg / <prefix>_landmark
  (prefix = scrap · hive · fort · nebula · core)

생성 방식은 두 갈래다 (AI 재생성 안 씀 — 정합과 결정론이 우선):

  1) far_dusk / far_dark = **기존 <prefix>_far.png의 램프 리매핑**.
     알파와 실루엣을 그대로 물려받으므로 구간 전환에서 형태가 튀지 않고
     색만 바뀐다. 팔레트는 base 16 + accent 6 = 22색 고정.

  2) fg / landmark = **절차 생성**. 주기 함수(period=640)만 써서
     가로 타일링 이음매가 수학적으로 보장된다. 팔레트는 인덱스 버퍼로
     그려서 정확히 고정한다(fg 10색, landmark 18~20색).

전부 결정론이다 — 시드가 소스이고 재실행하면 같은 파일이 나온다.

사용: python gen_section_bg.py [--only scrap,hive] [--out <dir>]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

W, H = 640, 360
DEFAULT_OUT = Path(__file__).resolve().parents[3] / "art-input"

PREFIXES = ["scrap", "hive", "fort", "nebula", "core", "abyss", "brood"]


# ── 공통 ──────────────────────────────────────────────────────────────────────

def ramp(anchors, n):
    """앵커 색을 n단계로 보간한 램프. 반환 shape (n,3) uint8."""
    a = np.array(anchors, dtype=np.float64)
    t = np.linspace(0.0, len(a) - 1.0, n)
    i0 = np.clip(np.floor(t).astype(int), 0, len(a) - 1)
    i1 = np.clip(i0 + 1, 0, len(a) - 1)
    f = (t - i0)[:, None]
    return np.round(a[i0] * (1 - f) + a[i1] * f).astype(np.uint8)


def _smooth(t):
    return t * t * (3 - 2 * t)


def noise2(rng, h, w, gx, gy):
    """x축으로 주기적인 2D 값 노이즈 (0~1). gx가 정수라 x=0/x=w가 연속이다."""
    lat = rng.random((gy + 1, gx + 1))
    lat[:, -1] = lat[:, 0]                      # x 래핑
    xs = np.linspace(0, gx, w, endpoint=False)
    ys = np.linspace(0, gy, h, endpoint=False)
    x0 = np.floor(xs).astype(int)
    y0 = np.floor(ys).astype(int)
    fx = _smooth(xs - x0)[None, :]
    fy = _smooth(ys - y0)[:, None]
    x1 = np.minimum(x0 + 1, gx)
    y1 = np.minimum(y0 + 1, gy)
    a = lat[np.ix_(y0, x0)]
    b = lat[np.ix_(y0, x1)]
    c = lat[np.ix_(y1, x0)]
    d = lat[np.ix_(y1, x1)]
    top = a + (b - a) * fx
    bot = c + (d - c) * fx
    return top + (bot - top) * fy


def fbm2(rng, h, w, base=4, octaves=4):
    out = np.zeros((h, w))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        g = base * (2 ** o)
        out += amp * noise2(rng, h, w, g, max(2, g * h // w))
        total += amp
        amp *= 0.5
    return out / total


def ridge1d(rng, harmonics, width=W):
    """주기 width의 1D 능선. harmonics = [(정수 주파수, 진폭), …]."""
    x = np.arange(width)
    out = np.zeros(width)
    for k, amp in harmonics:
        out += amp * np.sin(2 * np.pi * k * x / width + rng.uniform(0, 2 * np.pi))
    return out


def save_indexed(idx, palette, path):
    """인덱스 버퍼(0=투명) + 팔레트 → RGBA PNG. 색 수가 정확히 고정된다."""
    pal = np.zeros((len(palette) + 1, 4), dtype=np.uint8)
    for i, c in enumerate(palette):
        pal[i + 1] = (c[0], c[1], c[2], 255)
    out = pal[idx]
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(out, "RGBA").save(path)
    used = len(set(map(tuple, out[out[:, :, 3] > 0][:, :3].tolist())))
    print(f"  saved {Path(path).name}  {out.shape[1]}x{out.shape[0]}  colors={used}")


def disc(idx, cx, cy, r, value, wrap=True, h=None, w=None):
    """원 채우기. wrap이면 x가 폭 기준으로 감긴다(타일 이음매 연속)."""
    h = idx.shape[0] if h is None else h
    w = idx.shape[1] if w is None else w
    r = max(0.5, r)
    y0, y1 = int(max(0, cy - r - 1)), int(min(h, cy + r + 2))
    if y0 >= y1:
        return
    yy = np.arange(y0, y1)[:, None]
    xx = np.arange(int(cx - r - 1), int(cx + r + 2))[None, :]
    m = (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r
    xs = xx % w if wrap else xx
    ok = m & ((xs >= 0) & (xs < w))
    ys = np.repeat(yy, xx.shape[1], axis=1)
    idx[ys[ok], np.broadcast_to(xs, m.shape)[ok]] = value


def rect(idx, x, y, w_, h_, value, wrap=True):
    h, w = idx.shape
    ys = np.arange(int(y), int(y + h_))
    xs = np.arange(int(x), int(x + w_))
    ys = ys[(ys >= 0) & (ys < h)]
    if len(ys) == 0 or len(xs) == 0:
        return
    xs = xs % w if wrap else xs[(xs >= 0) & (xs < w)]
    idx[np.ix_(ys, xs)] = value


def thick_line(idx, x0, y0, x1, y1, width, value, wrap=True):
    n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 2
    for t in np.linspace(0, 1, n):
        disc(idx, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, width * 0.5, value, wrap)


# ── 1) far_dusk / far_dark — 기존 원경 램프 리매핑 ─────────────────────────────
# base 16 + accent 6 = 22색. dusk는 "적당히" 옮긴다(중간보스 구간은 20초 롱블렌드라
# 스프라이트 교체가 사건 없이 일어난다 — 색이 확 튀면 팝이 보인다).
# dark는 중간보스 격파 섬광+셰이크가 덮어 주므로 과감하게 옮긴다.

FAR_RAMPS = {
    # theme: (dusk base anchors, dusk accent, dark base anchors, dark accent)
    "scrap": (
        [(26, 20, 30), (58, 38, 40), (96, 60, 50), (138, 88, 62), (186, 130, 84), (238, 192, 138)],
        [(120, 52, 28), (176, 84, 34), (226, 128, 48), (255, 178, 92)],
        [(10, 11, 20), (22, 25, 40), (38, 42, 60), (60, 62, 80), (88, 86, 100), (132, 128, 142)],
        [(70, 26, 22), (112, 40, 28), (158, 62, 34), (206, 104, 52)],
    ),
    "hive": (
        [(26, 16, 28), (58, 26, 46), (98, 42, 62), (146, 70, 82), (192, 118, 104), (232, 186, 148)],
        [(112, 44, 96), (162, 66, 122), (206, 106, 152), (240, 168, 190)],
        [(7, 13, 13), (16, 30, 27), (28, 52, 43), (44, 78, 62), (66, 110, 84), (104, 158, 118)],
        [(24, 96, 84), (40, 142, 122), (78, 190, 160), (150, 232, 200)],
    ),
    "fort": (
        [(22, 20, 26), (46, 42, 50), (80, 72, 74), (118, 102, 94), (164, 138, 110), (216, 186, 140)],
        [(118, 60, 30), (166, 92, 38), (214, 132, 52), (250, 184, 98)],
        [(8, 10, 15), (19, 22, 31), (34, 40, 53), (54, 62, 80), (82, 92, 114), (126, 140, 168)],
        [(92, 22, 26), (140, 34, 36), (188, 56, 50), (232, 104, 88)],
    ),
    "nebula": (
        [(20, 14, 32), (42, 28, 60), (72, 48, 94), (110, 76, 130), (154, 116, 168), (206, 178, 216)],
        [(96, 52, 132), (140, 80, 176), (184, 122, 214), (226, 180, 244)],
        [(6, 7, 16), (16, 18, 36), (30, 34, 62), (50, 58, 96), (78, 92, 136), (124, 146, 190)],
        [(60, 78, 132), (100, 128, 184), (156, 186, 226), (216, 232, 252)],
    ),
    "core": (
        [(14, 16, 30), (30, 42, 60), (52, 74, 88), (88, 112, 106), (140, 152, 116), (214, 202, 152)],
        [(120, 96, 32), (172, 140, 44), (218, 184, 70), (252, 226, 140)],
        [(5, 8, 18), (13, 22, 46), (24, 44, 80), (38, 74, 122), (62, 116, 172), (112, 178, 224)],
        [(16, 96, 128), (28, 140, 180), (60, 188, 224), (140, 236, 250)],
    ),
    # 히든 심해 — 청록 단색에 가깝다. 다섯 공개 테마 중 어느 것과도 색이 겹치지
    # 않아야 "여긴 다른 곳"으로 읽힌다. 액센트는 생물발광(차가운 청록/백색).
    "abyss": (
        [(8, 14, 20), (16, 30, 40), (26, 50, 62), (40, 74, 86), (62, 104, 112), (110, 152, 152)],
        [(16, 92, 104), (28, 138, 148), (66, 190, 194), (158, 240, 236)],
        [(3, 6, 10), (8, 16, 24), (14, 28, 40), (22, 44, 60), (34, 66, 86), (62, 108, 128)],
        [(10, 68, 84), (18, 108, 128), (40, 158, 176), (110, 216, 226)],
    ),
    # 히든 둥지 — 자홍/보라 살덩이. abyss와 정반대 색상환에 두어 두 히든 스테이지가
    # 서로도 확실히 달라 보이게 한다. 액센트는 알집의 병든 호박색.
    "brood": (
        [(22, 10, 22), (48, 20, 42), (80, 34, 62), (116, 54, 80), (158, 86, 102), (208, 148, 152)],
        [(148, 84, 22), (198, 124, 32), (238, 172, 60), (255, 220, 140)],
        [(8, 4, 12), (20, 9, 24), (36, 16, 40), (56, 26, 58), (82, 44, 80), (128, 82, 118)],
        [(112, 56, 14), (162, 92, 24), (212, 138, 44), (250, 200, 110)],
    ),
}

BASE_STOPS = 16
ACCENT_STOPS = 6
# 불투명 픽셀 평균 휘도 목표. 기존 원경(평균 9~35)보다는 밝게 올려 구간 변화를 읽히게
# 하되, 탄·아이템(≈255)과 두 단계 이상 벌어지도록 눌러 둔다.
DUSK_LUM = 55.0
DARK_LUM = 36.0


def make_far_variant(src_path, out_path, base_anchors, accent_anchors, gamma, tone,
                     target_lum):
    src = np.asarray(Image.open(src_path).convert("RGBA")).astype(np.float32)
    rgb, alpha = src[:, :, :3], src[:, :, 3]
    mask = alpha > 8
    if not mask.any():
        raise SystemExit(f"{src_path}: 불투명 픽셀이 없다")

    lum = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
    mx = rgb.max(axis=2)
    mn = rgb.min(axis=2)
    sat = np.where(mx > 1, (mx - mn) / np.maximum(mx, 1e-3), 0.0)

    lo, hi = np.percentile(lum[mask], [2, 98])
    t = np.clip((lum - lo) / max(1e-3, hi - lo), 0, 1) ** gamma

    # 발광부(용암·생체발광·네온)만 액센트 램프로 뽑는다 — 채도×밝기 상위 8%.
    score = sat * (lum / 255.0)
    thr = np.percentile(score[mask], 92)
    accent = mask & (score >= max(thr, 0.06))

    base_pal = ramp(base_anchors, BASE_STOPS)
    acc_pal = ramp(accent_anchors, ACCENT_STOPS)
    out = np.zeros((H, W, 3), dtype=np.uint8)
    bi = np.round(t * (BASE_STOPS - 1)).astype(int)
    out[:] = base_pal[bi]
    ai = np.round(np.clip((t - 0.35) / 0.65, 0, 1) * (ACCENT_STOPS - 1)).astype(int)
    out[accent] = acc_pal[ai[accent]]

    # 밝기 정규화: 원경은 탄(255)보다 두 단계 아래여야 한다 (ART-DIRECTION 가시성 규칙).
    # 테마마다 원본 노출이 제각각이라 절대 목표 평균 휘도로 맞춰 20장의 톤을 통일한다.
    cur = (0.299 * out[:, :, 0] + 0.587 * out[:, :, 1]
           + 0.114 * out[:, :, 2])[mask].mean()
    if cur > 1:
        out = np.clip(np.round(out.astype(np.float32) * (target_lum / cur)), 0, 255).astype(np.uint8)

    # 밤 변형은 알파도 살짝 조여 실루엣을 무겁게 (형태는 그대로 유지).
    a = alpha if tone == "dusk" else np.clip(alpha * 1.08, 0, 255)
    rgba = np.dstack([out, a.astype(np.uint8)])
    rgba[:, :, :3][alpha <= 0] = 0
    Image.fromarray(rgba, "RGBA").save(out_path)
    used = len(set(map(tuple, rgba[rgba[:, :, 3] > 8][:, :3].tolist())))
    print(f"  saved {Path(out_path).name}  {W}x{H}  colors={used}")


# ── 2-A) 전경 실루엣 fg (640×360, 가로 타일링) ────────────────────────────────
# 팔레트 인덱스 규약: 1=심부, 2=본체, 3=림/디테일, 4=액센트, 5=액센트 하이라이트.

FG_PALETTES = {
    # 10색: 실루엣 6단 + 액센트 4단. Near 틴트(0.2~0.5 곱)를 통과해도 형태가 읽히게
    # 원본을 중간 밝기로 잡는다.
    "scrap": [(18, 14, 18), (34, 26, 28), (52, 40, 40), (74, 56, 52), (100, 76, 66),
              (132, 100, 82), (150, 66, 32), (196, 100, 40), (236, 150, 62), (252, 206, 130)],
    "hive": [(14, 18, 16), (26, 34, 30), (40, 52, 44), (58, 74, 60), (80, 100, 78),
             (108, 130, 98), (28, 118, 104), (48, 168, 140), (96, 216, 176), (176, 246, 214)],
    "fort": [(14, 15, 19), (26, 29, 36), (40, 45, 55), (58, 65, 78), (82, 92, 108),
             (114, 126, 146), (112, 26, 28), (168, 40, 38), (222, 68, 56), (252, 150, 120)],
    "nebula": [(16, 14, 26), (28, 24, 44), (44, 38, 66), (64, 56, 92), (90, 80, 124),
               (122, 112, 160), (72, 60, 140), (112, 96, 190), (162, 152, 232), (222, 216, 252)],
    "core": [(10, 14, 22), (20, 28, 42), (32, 44, 64), (48, 64, 90), (68, 90, 122),
             (96, 124, 158), (18, 96, 130), (30, 150, 194), (70, 204, 238), (162, 244, 252)],
    "abyss": [(6, 12, 16), (12, 22, 30), (20, 36, 46), (30, 54, 66), (44, 76, 88),
              (64, 104, 114), (14, 86, 98), (26, 134, 146), (62, 188, 194), (150, 238, 234)],
    "brood": [(16, 8, 16), (30, 14, 28), (48, 24, 42), (70, 36, 58), (98, 54, 76),
              (132, 80, 100), (128, 66, 18), (180, 106, 28), (226, 158, 54), (250, 212, 128)],
}


# 인덱스 규약: 1~6 = 실루엣 램프(어두움→밝음), 7~10 = 액센트 램프(어두움→발광).
DEEP, DARK, MID, BODY, EDGE, RIM = 1, 2, 3, 4, 5, 6
ACC0, ACC1, ACC2, ACC3 = 7, 8, 9, 10

# 깊이(표면에서 아래로 픽셀) → 실루엣 인덱스. 위 두 줄만 밝고 급격히 어두워진다 —
# 전경은 sortingOrder 55(게임플레이 위)라 덩어리는 확실히 어두워야 한다.
_DEPTH_BANDS = [(1, RIM), (3, EDGE), (8, BODY), (18, MID), (44, DARK)]


def _fill_below(idx, height):
    """능선 height(x) 아래를 채운다 (아래에서 올라오는 지형)."""
    yy = np.arange(H)[:, None]
    d = yy - (H - height)[None, :]
    idx[d >= 0] = DEEP
    for limit, value in reversed(_DEPTH_BANDS):
        idx[(d >= 0) & (d < limit)] = value


def _fill_above(idx, height):
    """천장 프린지 — 위에서 매달린 띠."""
    yy = np.arange(H)[:, None]
    d = height[None, :] - yy
    idx[d >= 0] = DEEP
    for limit, value in reversed(_DEPTH_BANDS):
        idx[(d >= 0) & (d < limit)] = value


def _peaks(rng, count, lo, hi, sig_lo, sig_hi):
    """x 래핑 가우시안 봉우리 합 — 능선에 큰 실루엣 변화를 준다."""
    x = np.arange(W)
    out = np.zeros(W)
    tops = []
    for _ in range(count):
        cx = int(rng.integers(0, W))
        amp = rng.uniform(lo, hi)
        sig = rng.uniform(sig_lo, sig_hi)
        d = np.minimum(np.abs(x - cx), W - np.abs(x - cx))
        out += amp * np.exp(-(d ** 2) / (2 * sig ** 2))
        tops.append(cx)
    return out, tops


def fg_scrap(rng):
    """고철 더미 능선 — 톱니 능선 + 박힌 철판 + 안테나 마스트."""
    idx = np.zeros((H, W), np.uint8)
    r = ridge1d(rng, [(1, 9), (2, 7), (3, 5), (5, 4), (8, 3), (13, 2), (21, 1.4), (34, 0.9)])
    r = np.abs(r) * 0.75 + r * 0.25                     # 능선을 뾰족하게
    piles, tops = _peaks(rng, 5, 30, 62, 26, 58)        # 고철 더미
    height = np.clip(44 + r + piles, 20, 128)
    _fill_below(idx, height)

    # 표면 얼룩 — 고철 질감
    n = fbm2(rng, H, W, base=20, octaves=3)
    yy = np.arange(H)[:, None]
    inside = yy >= (H - height)[None, :]
    idx[inside & (n > 0.63)] = EDGE
    idx[inside & (n < 0.33)] = DEEP

    # 박힌 철판 (능선에 걸치게)
    for _ in range(30):
        x = int(rng.integers(0, W))
        top = H - height[x] + rng.integers(-4, 18)
        pw, ph = int(rng.integers(12, 42)), int(rng.integers(5, 14))
        rect(idx, x, top, pw, ph, MID)
        rect(idx, x, top, pw, 1, EDGE)
        rect(idx, x, top + ph - 1, pw, 1, DEEP)
        if rng.random() < 0.35:                          # 녹슨 절단면
            rect(idx, x + pw - 2, top + 1, 2, ph - 2, ACC0)
    # 안테나 마스트 + 크로스바
    for cx in tops:
        for _ in range(2):
            x = int((cx + rng.integers(-30, 31)) % W)
            base = H - height[x] + 2
            top = base - int(rng.integers(26, 66))
            rect(idx, x, top, 2, base - top, EDGE)
            rect(idx, x - 5, top + 6, 12, 1, BODY)
            rect(idx, x - 3, top + 14, 8, 1, BODY)
            rect(idx, x, top, 2, 2, ACC2)                # 항공 표시등
    # 잔불
    for _ in range(30):
        x = int(rng.integers(0, W))
        y = int(H - height[x] * rng.uniform(0.05, 0.6))
        disc(idx, x, y, rng.uniform(1.0, 2.6), ACC1)
        disc(idx, x, y, 0.8, ACC3)
    # 천장 프린지
    ct = ridge1d(rng, [(1, 6), (3, 5), (6, 4), (11, 2.5), (19, 1.5)])
    _fill_above(idx, np.clip(22 + np.abs(ct), 6, 58))
    return idx


def fg_hive(rng):
    """촉수 융기 — 부풀어 오른 둔덕 + 위로 굽는 촉수 + 발광 포자낭."""
    idx = np.zeros((H, W), np.uint8)
    bumps, mounds = _peaks(rng, 8, 16, 44, 26, 70)
    height = np.clip(26 + bumps, 18, 102)
    _fill_below(idx, height)

    n = fbm2(rng, H, W, base=12, octaves=3)
    yy = np.arange(H)[:, None]
    inside = yy >= (H - height)[None, :]
    idx[inside & (n > 0.60)] = EDGE
    idx[inside & (n < 0.36)] = DEEP

    def tentacle(x0, y0, length, bend, phase, up=True, w0=6.5):
        sign = -1 if up else 1
        for t in np.linspace(0, 1, 96):
            px = x0 + bend * (t ** 1.6) + 6 * np.sin(t * 5 + phase)
            py = y0 + sign * length * t
            wdt = w0 * (1 - t) ** 0.8 + 0.9
            disc(idx, px, py, wdt, MID)
            disc(idx, px - wdt * 0.5, py, max(0.6, wdt * 0.32), EDGE)
        tipx = x0 + bend + 6 * np.sin(5 + phase)
        tipy = y0 + sign * length
        disc(idx, tipx, tipy, 2.4, ACC1)
        disc(idx, tipx, tipy, 1.1, ACC3)

    for cx in mounds:
        for _ in range(3):
            x0 = int(cx + rng.integers(-42, 43))
            y0 = H - height[x0 % W] + 5
            tentacle(x0, y0, rng.uniform(36, 92), rng.uniform(-44, 44), rng.uniform(0, 1.4))
    # 발광 포자낭
    for _ in range(26):
        px = int(rng.integers(0, W))
        py = H - height[px] * rng.uniform(0.12, 0.8)
        rr = rng.uniform(2.4, 6.0)
        disc(idx, px, py, rr, ACC0)
        disc(idx, px, py, rr * 0.6, ACC1)
        disc(idx, px, py, rr * 0.25, ACC3)
    # 천장 육벽 + 매달린 촉수
    ct = ridge1d(rng, [(1, 7), (2, 6), (5, 4), (9, 2.5)])
    _fill_above(idx, np.clip(24 + np.abs(ct), 8, 62))
    for _ in range(18):
        x0 = int(rng.integers(0, W))
        tentacle(x0, 26, rng.uniform(22, 64), rng.uniform(-22, 22),
                 rng.uniform(0, 1.4), up=False, w0=4.5)
    return idx


def fg_fort(rng):
    """포탑 실루엣 라인 — 블록 플랫폼 + 포탑 + 상단 거더 트러스."""
    idx = np.zeros((H, W), np.uint8)
    cell = 16
    cells = W // cell
    levels = np.array([rng.choice([30, 38, 46, 46, 54, 62, 74]) for _ in range(cells)], float)
    levels = (levels + np.roll(levels, 1)) / 2.0                # 이웃 평활(래핑)
    height = np.repeat(levels, cell)
    _fill_below(idx, height)

    # 패널 라인 — 기계적 격자
    inside = np.arange(H)[:, None] >= (H - height)[None, :]
    grid = np.zeros((H, W), bool)
    grid[::11, :] = True
    grid[:, ::cell] = True
    idx[inside & grid] = DEEP
    row = np.arange(W)
    for yl in range(H - 70, H, 22):
        if 0 <= yl < H:
            idx[yl, inside[yl] & ((row // cell) % 3 == 0)] = EDGE
    # 격납 해치 불빛
    for _ in range(24):
        x = int(rng.integers(0, W))
        y = int(H - height[x] * rng.uniform(0.15, 0.8))
        rect(idx, x, y, 3, 2, ACC0)

    # 포탑 (2단 받침 + 앙각 포신)
    for i in range(7):
        cx = int((i + 0.5) * W / 7 + rng.integers(-20, 20)) % W
        base = int(H - height[cx])
        bw = int(rng.integers(24, 36))
        bh = int(rng.integers(14, 20))
        rect(idx, cx - bw // 2, base - bh, bw, bh, MID)
        rect(idx, cx - bw // 2, base - bh, bw, 2, EDGE)
        rect(idx, cx - bw // 2, base - 3, bw, 3, DARK)
        rect(idx, cx - bw // 2 - 4, base - bh - 6, bw + 8, 7, BODY)
        rect(idx, cx - bw // 2 - 4, base - bh - 6, bw + 8, 1, RIM)
        ang = rng.uniform(-1.2, -0.4)
        ln = rng.uniform(28, 46)
        bx, by = cx, base - bh - 5
        ex, ey = bx + ln * np.cos(ang), by + ln * np.sin(ang)
        thick_line(idx, bx, by, ex, ey, 6, MID)
        thick_line(idx, bx, by, ex, ey, 2, EDGE)
        disc(idx, ex, ey, 3.0, DARK)
        disc(idx, ex, ey, 1.4, ACC1)
        rect(idx, cx - 3, base - bh + 4, 6, 3, ACC1)            # 비상등
        rect(idx, cx - 1, base - bh + 5, 2, 1, ACC3)

    # 상단 거더 + 트러스
    top_h = 26
    rect(idx, 0, 0, W, top_h, DARK, wrap=False)
    rect(idx, 0, 0, W, 3, MID, wrap=False)
    rect(idx, 0, top_h - 3, W, 3, DEEP, wrap=False)
    for i in range(0, W, 40):
        thick_line(idx, i, top_h, i + 20, top_h + 18, 4, DARK)
        thick_line(idx, i + 40, top_h, i + 20, top_h + 18, 4, DARK)
        rect(idx, i + 16, top_h + 14, 9, 8, MID)
        rect(idx, i + 16, top_h + 14, 9, 1, EDGE)
        rect(idx, i + 19, top_h + 18, 3, 3, ACC1)
        rect(idx, i + 36, top_h, 8, 6, MID)
    return idx


def fg_nebula(rng):
    """구름 소용돌이 — 로그 나선을 따라 배치한 메타볼 밀도장을 3단 포스터라이즈."""
    idx = np.zeros((H, W), np.uint8)
    field = np.zeros((H, W))
    yy, xx = np.mgrid[0:H, 0:W]

    def add_blob(cx, cy, r):
        # 유한 지지 커널이어야 한다 — 1/d² 꼬리를 쓰면 원거리 합이 임계값을 넘어
        # 캔버스가 통째로 채워진다(전경이 플레이필드를 가림).
        rad = r * 2.4
        dx = np.abs(xx - cx)
        dx = np.minimum(dx, W - dx)                     # x 래핑 → 이음매 연속
        q = 1.0 - (dx ** 2 + (yy - cy) ** 2) / (rad * rad)
        np.add(field, np.clip(q, 0, 1) ** 3, out=field)

    # 아래 구름 띠 + 그 안의 소용돌이 2개, 위쪽에 얇은 띠. 전경은 게임플레이 위라
    # 화면 중앙(플레이필드)은 반드시 비워 둔다.
    base = ridge1d(rng, [(1, 9), (2, 7), (4, 5), (7, 3)])
    for x0 in range(0, W, 12):
        add_blob(x0, H - 24 + base[x0] * 0.6, rng.uniform(22, 32))
    swirls = [(158, H - 74, 1.9), (466, H - 66, -1.7)]
    for cx0, cy0, turns in swirls:
        for k in range(52):
            t = k / 51.0
            a = turns * 2 * np.pi * t
            rad = 8 + 58 * t
            add_blob(cx0 + rad * np.cos(a), cy0 + rad * np.sin(a) * 0.40,
                     8 + 11 * (1 - t))
    for x0 in range(0, W, 16):
        add_blob(x0, 6 + base[x0] * 0.5, rng.uniform(13, 21))

    n = fbm2(rng, H, W, base=8, octaves=4)
    f = field * (0.72 + 0.56 * n)                       # 가장자리를 노이즈로 뜯는다
    idx[f > 0.34] = EDGE
    idx[f > 0.52] = BODY
    idx[f > 0.80] = MID
    idx[f > 1.20] = DARK
    idx[f > 1.85] = DEEP
    idx[(idx > 0) & (n > 0.72)] = RIM                   # 볕 든 구름 마루

    # 소용돌이 눈의 방전
    for cx0, cy0, _t in swirls:
        px, py = cx0, cy0
        for _ in range(8):
            nx = px + rng.uniform(-24, 24)
            ny = py + rng.uniform(4, 20)
            thick_line(idx, px, py, nx, ny, 3, ACC1)
            thick_line(idx, px, py, nx, ny, 1, ACC3)
            px, py = nx, ny
        disc(idx, cx0, cy0, 5, ACC2)
        disc(idx, cx0, cy0, 2.2, ACC3)
    return idx


def fg_core(rng):
    """회로 첨탑 — 각진 첨탑 + 회로 트레이스 + 발광 노드."""
    idx = np.zeros((H, W), np.uint8)
    base_h = 30
    rect(idx, 0, H - base_h, W, base_h, DARK, wrap=False)
    rect(idx, 0, H - base_h, W, 3, MID, wrap=False)
    rect(idx, 0, H - base_h, W, 1, EDGE, wrap=False)

    spires = []
    for i in range(16):
        cx = int((i + 0.5) * W / 16 + rng.integers(-9, 9))
        hgt = int(rng.integers(30, 140))
        bw = int(rng.integers(16, 28))
        tw = max(6, bw - int(rng.integers(6, 13)))
        top = H - base_h - hgt
        for y in range(top, H - base_h + 2):
            f = (y - top) / max(1, (H - base_h - top))
            half = (tw + (bw - tw) * f) * 0.5
            rect(idx, cx - half, y, half * 2, 1, DARK)
            rect(idx, cx - half, y, 2, 1, BODY)          # 좌측 림
            rect(idx, cx + half - 1, y, 1, 1, DEEP)      # 우측 그림자
        rect(idx, cx - tw // 2 - 3, top - 5, tw + 6, 6, MID)
        rect(idx, cx - tw // 2 - 3, top - 5, tw + 6, 1, RIM)
        disc(idx, cx, top - 7, 2.8, ACC1)
        disc(idx, cx, top - 7, 1.3, ACC3)
        # 회로 트레이스
        for y in range(top + 9, H - base_h, 13):
            half = int((tw + (bw - tw) * (y - top) / max(1, H - base_h - top)) * 0.5)
            rect(idx, cx - half + 2, y, max(2, half * 2 - 4), 1, ACC1)
            if rng.random() < 0.4:
                rect(idx, cx - 1, y - 1, 3, 3, ACC2)
        spires.append((cx, top))

    # 첨탑 사이 케이블
    spires.sort()
    for (x0, y0), (x1, y1) in zip(spires, spires[1:] + [(spires[0][0] + W, spires[0][1])]):
        thick_line(idx, x0, y0 + 5, x1, y1 + 5, 1, MID)
        mx, my = (x0 + x1) // 2, (y0 + y1) // 2 + 5
        rect(idx, mx - 1, my - 1, 3, 3, ACC0)

    # 천장 역첨탑
    ct = ridge1d(rng, [(1, 5), (3, 4), (7, 3), (13, 2)])
    _fill_above(idx, np.clip(16 + np.abs(ct), 5, 44))
    for i in range(11):
        cx = int((i + 0.5) * W / 11 + rng.integers(-12, 12))
        hgt = int(rng.integers(16, 58))
        for y in range(0, hgt):
            half = max(2.0, 9 * (1 - y / hgt))
            rect(idx, cx - half, y, half * 2, 1, DARK)
            rect(idx, cx - half, y, 1, 1, MID)
        disc(idx, cx, hgt, 2.2, ACC1)
    return idx


def fg_abyss(rng):
    """해구 바닥 — 무너진 침전 능선 + 굴뚝 열수공 + 위에서 내려오는 종유석.

    심해의 압박감은 "위아래가 좁다"로 온다. 바닥과 천장을 둘 다 두껍게 물려서
    플레이 통로를 가운데로 눌러 둔다 — 히든 구간이 공개 스테이지보다 답답해야 한다.
    """
    idx = np.zeros((H, W), np.uint8)
    r = ridge1d(rng, [(1, 8), (2, 6), (4, 4), (7, 3), (12, 2), (20, 1.2)])
    mounds, tops = _peaks(rng, 4, 26, 54, 34, 70)        # 침전 둔덕
    height = np.clip(52 + r + mounds, 26, 132)
    _fill_below(idx, height)

    # 침전층 — 가로로 길게 눌린 무늬. 물속에 쌓인 것이라 결이 수평이다.
    n = fbm2(rng, H, W, base=26, octaves=3)
    yy = np.arange(H)[:, None]
    inside = yy >= (H - height)[None, :]
    band = (yy // 3) % 2 == 0
    idx[inside & band & (n > 0.58)] = MID
    idx[inside & (n < 0.30)] = DEEP

    # 열수공 굴뚝 — 가늘고 높게 솟아 검은 연기를 뿜는다.
    for cx in tops:
        for _ in range(2):
            x = int((cx + rng.integers(-40, 41)) % W)
            base = H - height[x] + 3
            hgt = int(rng.integers(30, 78))
            for y in range(hgt):
                half = max(1.5, 5.5 * (1 - y / hgt) + 1.0)
                rect(idx, x - half, base - y, half * 2, 1, DARK)
                rect(idx, x - half, base - y, 1, 1, MID)
            disc(idx, x, base - hgt, 2.4, ACC0)          # 분출구
            for k in range(5):                            # 흩어지는 연기 기둥
                disc(idx, x + rng.integers(-6, 7), base - hgt - 6 - k * 7,
                     rng.uniform(2.0, 4.4), DEEP)

    # 생물발광 — 어둠 속에 떠 있는 점광. 이 테마의 유일한 밝은 것이다.
    for _ in range(70):
        x = int(rng.integers(0, W))
        y = int(rng.integers(30, H - 20))
        disc(idx, x, y, rng.uniform(0.8, 1.8), ACC1)
        if rng.random() < 0.3:
            disc(idx, x, y, 0.7, ACC3)

    # 천장 종유석 — 바닥보다 길게 내려서 통로를 좁힌다.
    ct = ridge1d(rng, [(1, 7), (3, 5), (6, 4), (11, 2.5), (18, 1.5)])
    _fill_above(idx, np.clip(30 + np.abs(ct), 10, 66))
    for i in range(14):
        cx = int((i + 0.5) * W / 14 + rng.integers(-10, 10))
        hgt = int(rng.integers(24, 78))
        for y in range(hgt):
            half = max(1.5, 7 * (1 - y / hgt))
            rect(idx, cx - half, y, half * 2, 1, DARK)
            rect(idx, cx - half, y, 1, 1, MID)
        disc(idx, cx, hgt, 1.8, ACC2)                    # 끝의 발광점
    return idx


def fg_brood(rng):
    """둥지 내벽 — 살덩이 둔덕 + 알집 무더기 + 위에서 늘어진 힘줄.

    abyss가 "단단하고 차갑다"면 여기는 "물렁하고 축축하다". 실루엣을 둥글게
    가고 수직선을 늘어지게 해서 같은 좁은 통로라도 체감이 다르게 만든다.
    """
    idx = np.zeros((H, W), np.uint8)
    r = ridge1d(rng, [(1, 7), (2, 6), (3, 5), (5, 3.5), (9, 2)])
    r = r * 0.4 + np.abs(r) * 0.2                        # 둥글게 — 뾰족함을 죽인다
    lumps, tops = _peaks(rng, 6, 24, 52, 40, 84)
    height = np.clip(46 + r + lumps, 22, 126)
    _fill_below(idx, height)

    # 살결 — 굵고 불규칙한 얼룩
    n = fbm2(rng, H, W, base=12, octaves=4)
    yy = np.arange(H)[:, None]
    inside = yy >= (H - height)[None, :]
    idx[inside & (n > 0.60)] = BODY
    idx[inside & (n < 0.34)] = DEEP

    # 알집 무더기 — 둔덕 위에 뭉쳐 붙는다. 안쪽이 병든 호박색으로 빛난다.
    for cx in tops:
        for _ in range(7):
            x = int((cx + rng.integers(-46, 47)) % W)
            y = int(H - height[x] * rng.uniform(0.15, 0.85))
            rad = rng.uniform(4.0, 11.0)
            disc(idx, x, y, rad, EDGE)
            disc(idx, x, y, rad * 0.72, ACC0)
            disc(idx, x, y, rad * 0.40, ACC2)
            disc(idx, x - rad * 0.2, y - rad * 0.2, rad * 0.16, ACC3)

    # 늘어진 힘줄 — 천장에서 통로 안으로 내려온다.
    for _ in range(26):
        x = int(rng.integers(0, W))
        drop = int(rng.integers(40, 150))
        sway = rng.integers(-18, 19)
        thick_line(idx, x, 0, x + sway, drop, rng.uniform(1.5, 3.5), MID)
        disc(idx, x + sway, drop, rng.uniform(1.6, 3.2), ACC1)

    # 천장 막 — 두껍게. 위쪽이 무겁게 덮여 있어야 "안에 들어와 있다"로 읽힌다.
    ct = ridge1d(rng, [(1, 8), (2, 6), (5, 4), (9, 2.5)])
    _fill_above(idx, np.clip(34 + np.abs(ct), 12, 72))
    return idx


FG_BUILDERS = {"scrap": fg_scrap, "hive": fg_hive, "fort": fg_fort,
               "nebula": fg_nebula, "core": fg_core,
               "abyss": fg_abyss, "brood": fg_brood}
FG_SEEDS = {"scrap": 4101, "hive": 4202, "fort": 4303, "nebula": 4404, "core": 4505,
            "abyss": 4606, "brood": 4707}


# ── 2-B) 랜드마크 (비반복 대형 오브젝트, 화면 높이의 1/2~2/3) ────────────────
# 팔레트 = 본체 램프 14단 + 액센트 4단 = 18색. 인덱스 1..14 = 램프, 15..18 = 액센트.

LM_SPECS = {
    #            (w,   h,   본체 앵커,                                              액센트 앵커)
    "scrap": (384, 200,
              [(26, 20, 22), (56, 44, 42), (92, 74, 64), (132, 106, 88), (176, 146, 116), (222, 198, 160)],
              [(122, 44, 22), (180, 78, 30), (232, 128, 46), (254, 196, 120)]),
    "hive": (248, 232,
             [(22, 16, 26), (52, 32, 52), (88, 54, 78), (128, 84, 104), (172, 126, 134), (218, 186, 178)],
             [(36, 128, 96), (66, 182, 132), (118, 226, 168), (196, 250, 214)]),
    "fort": (192, 240,
             [(20, 22, 28), (44, 48, 58), (74, 80, 92), (108, 116, 130), (148, 158, 174), (204, 214, 230)],
             [(120, 32, 28), (178, 52, 40), (232, 96, 62), (255, 190, 120)]),
    "nebula": (352, 236,
               [(20, 16, 34), (44, 34, 68), (74, 58, 104), (110, 90, 144), (154, 134, 186), (212, 200, 236)],
               [(96, 110, 190), (150, 170, 230), (206, 224, 250), (255, 255, 255)]),
    "core": (232, 232,
             [(10, 16, 30), (22, 38, 66), (36, 66, 104), (56, 100, 146), (92, 148, 190), (156, 210, 236)],
             [(126, 90, 22), (188, 146, 40), (238, 202, 88), (255, 246, 190)]),
    "abyss": (400, 216,
              [(6, 12, 18), (14, 28, 38), (24, 48, 60), (38, 74, 86), (60, 106, 114), (108, 156, 156)],
              [(14, 88, 100), (26, 136, 146), (64, 190, 194), (156, 240, 236)]),
    "brood": (296, 244,
              [(18, 8, 18), (40, 18, 36), (68, 32, 56), (100, 50, 74), (140, 80, 96), (194, 138, 142)],
              [(140, 78, 20), (192, 118, 30), (236, 168, 58), (255, 218, 138)]),
}
LM_RAMP_STOPS = 14


def _shade(idx, mask, level, stops=LM_RAMP_STOPS):
    """level(0~1) → 램프 인덱스 1..stops."""
    v = 1 + np.clip(np.round(level * (stops - 1)), 0, stops - 1).astype(np.uint8)
    idx[mask] = v[mask]


def lm_scrap(rng, w, h):
    """난파 모함 — 두 동강 난 대형 함선. 함미가 꺾여 내려앉고 파단면이 타고 있다."""
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    u = xx / (w - 1.0)
    break_u = 0.60

    # 함수는 수평, 함미(오른쪽)는 꺾여 내려앉는다.
    tilt = np.where(u < break_u, 0.0, (u - break_u) * 150.0)
    cy = 88.0 + tilt
    # 비행갑판: 위는 평평(약간의 시어), 아래는 선저 곡선. 뱃머리는 왼쪽.
    bow = np.clip((u - 0.02) / 0.16, 0, 1)
    stern = np.clip((1.0 - u) / 0.035, 0, 1)                    # 함미는 뭉툭하게 잘린 채
    taper = np.minimum(bow, stern)
    top_h = 20.0 * taper
    bot_h = (12.0 + 26.0 * np.sqrt(np.clip(1 - ((u - 0.45) / 0.62) ** 2, 0, 1))) * taper
    hull = (yy >= cy - top_h) & (yy <= cy + bot_h) & (taper > 0.02)
    gap = np.abs(u - break_u) < (0.012 + 0.012 * np.sin(yy * 0.4))
    hull &= ~gap

    # 셰이딩: 갑판 쪽이 밝고 선저로 갈수록 어둡다 + 장갑판 줄무늬
    depth = (yy - (cy - top_h)) / np.maximum(top_h + bot_h, 1)
    lvl = np.clip(0.92 - depth * 1.05, 0, 1) * 0.86
    lvl += 0.14 * ((xx % 23) < 11)
    _shade(idx, hull, np.clip(lvl, 0, 1))

    # 비행갑판 라인 · 함현 현창
    idx[hull & (np.abs(yy - (cy - top_h)) < 2.0)] = LM_RAMP_STOPS
    idx[hull & (np.abs(yy - (cy - top_h + 5)) < 1.0)] = LM_RAMP_STOPS - 8
    idx[hull & ((xx % 19) < 3) & (np.abs(yy - (cy + 4)) < 3)] = LM_RAMP_STOPS + 2
    idx[hull & (yy > cy + bot_h - 3)] = 1                        # 선저 그림자

    # 함교(아일랜드) · 마스트 · 레이더
    bx = int(0.26 * w)
    by = int(88 - 20 * np.clip((bx / (w - 1) - 0.02) / 0.16, 0, 1))
    rect(idx, bx, by - 30, 42, 32, LM_RAMP_STOPS - 4, wrap=False)
    rect(idx, bx, by - 30, 42, 2, LM_RAMP_STOPS - 1, wrap=False)
    rect(idx, bx, by - 30, 3, 32, LM_RAMP_STOPS - 2, wrap=False)
    for i in range(bx + 5, bx + 38, 8):
        rect(idx, i, by - 24, 4, 5, LM_RAMP_STOPS + 2, wrap=False)
    rect(idx, bx + 17, by - 62, 4, 32, LM_RAMP_STOPS - 6, wrap=False)
    rect(idx, bx + 6, by - 52, 26, 2, LM_RAMP_STOPS - 3, wrap=False)
    rect(idx, bx + 10, by - 44, 18, 2, LM_RAMP_STOPS - 3, wrap=False)
    disc(idx, bx + 19, by - 64, 2.6, LM_RAMP_STOPS + 2, wrap=False)

    # 갑판 위 잔해 (부서진 캐터펄트 · 격납 구조물)
    for _ in range(9):
        px = int(rng.uniform(0.08, 0.55) * w)
        pw, ph = int(rng.integers(10, 26)), int(rng.integers(5, 13))
        py = int(88 - 20 * np.clip((px / (w - 1) - 0.02) / 0.16, 0, 1)) - ph
        rect(idx, px, py, pw, ph, LM_RAMP_STOPS - 7, wrap=False)
        rect(idx, px, py, pw, 1, LM_RAMP_STOPS - 3, wrap=False)

    # 파단면 골조 (드러난 늑골)
    xb = int(break_u * w)
    for k in range(-3, 4):
        rect(idx, xb - 22 + k * 6, 78 + abs(k) * 3, 3, 34 - abs(k) * 4,
             LM_RAMP_STOPS - 5, wrap=False)
        rect(idx, xb + 6 + k * 6, 86 + abs(k) * 3, 3, 30 - abs(k) * 4,
             LM_RAMP_STOPS - 5, wrap=False)

    # 불타는 파단부 + 흩어지는 잔해
    for _ in range(90):
        px = rng.normal(break_u * w, 16)
        py = rng.normal(96, 24)
        if 0 <= px < w and 0 <= py < h:
            disc(idx, px, py, rng.uniform(1.0, 3.2),
                 LM_RAMP_STOPS + int(rng.integers(1, 5)), wrap=False)
    for _ in range(50):
        px = rng.normal(break_u * w + 10, 34)
        py = rng.normal(70, 40)
        if 0 <= px < w and 0 <= py < h and idx[int(py), int(px)] == 0:
            disc(idx, px, py, rng.uniform(0.7, 1.9), LM_RAMP_STOPS - 6, wrap=False)
    return idx


def lm_hive(rng, w, h):
    """거대 알주머니 — 반투명 막 안의 배아 알 + 늘어진 촉수."""
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    cx, cy = w * 0.5, h * 0.42
    rx, ry = w * 0.40, h * 0.36
    nx = (xx - cx) / rx
    ny = (yy - cy) / ry
    d2 = nx ** 2 + ny ** 2
    sac = d2 <= 1.0
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam = np.clip(nx * -0.52 + ny * -0.60 + nz * 0.61, 0, 1)
    _shade(idx, sac, lam ** 0.8 * 0.9 + 0.05)
    idx[sac & (d2 > 0.90)] = LM_RAMP_STOPS - 2                   # 막 테두리
    idx[sac & (d2 > 0.965)] = LM_RAMP_STOPS

    # 배아 알
    for _ in range(9):
        a = rng.uniform(0, 2 * np.pi)
        rr = rng.uniform(0, 0.66)
        ex, ey = cx + np.cos(a) * rr * rx, cy + np.sin(a) * rr * ry
        er = rng.uniform(10, 22)
        disc(idx, ex, ey, er, LM_RAMP_STOPS + 1, wrap=False)
        disc(idx, ex, ey, er * 0.62, LM_RAMP_STOPS + 2, wrap=False)
        disc(idx, ex - er * 0.2, ey - er * 0.22, er * 0.26, LM_RAMP_STOPS + 3, wrap=False)
    idx[~sac & (idx > 0) & (yy < cy)] = 0

    # 늘어진 촉수 — 가늘고 많게
    for _ in range(20):
        x0 = cx + rng.uniform(-0.92, 0.92) * rx
        y0 = cy + np.sqrt(max(0.0, 1 - ((x0 - cx) / rx) ** 2)) * ry - 3
        length = rng.uniform(20, max(24.0, h - y0 - 2))
        bend = rng.uniform(-18, 18)
        phase = rng.uniform(0, 6)
        for t in np.linspace(0, 1, 80):
            px = x0 + bend * t ** 1.7 + 4 * np.sin(t * 7 + phase)
            py = y0 + length * t
            if 0 <= px < w and 0 <= py < h:
                disc(idx, px, py, 3.0 * (1 - t) ** 0.6 + 0.7, LM_RAMP_STOPS - 7, wrap=False)
                disc(idx, px - 1.0, py, max(0.5, 1.1 * (1 - t)), LM_RAMP_STOPS - 4, wrap=False)
    # 상단 줄기
    rect(idx, cx - 9, 0, 18, int(cy - ry * 0.9) + 4, LM_RAMP_STOPS - 5, wrap=False)
    rect(idx, cx - 9, 0, 3, int(cy - ry * 0.9) + 4, LM_RAMP_STOPS - 2, wrap=False)
    return idx


def lm_fort(rng, w, h):
    """관제탑 — 저부 트러스 · 축 · 관제실 · 안테나 어레이."""
    idx = np.zeros((h, w), np.uint8)
    cx = w // 2

    def shaded_box(x, y, bw, bh, lo=0.15, hi=0.95):
        for i in range(int(bw)):
            f = 1 - abs((i / max(1, bw - 1)) - 0.30) * 1.7
            rect(idx, x + i, y, 1, bh,
                 1 + int(np.clip(lo + (hi - lo) * np.clip(f, 0, 1), 0, 1) * (LM_RAMP_STOPS - 1)),
                 wrap=False)

    # 기단
    shaded_box(cx - 62, h - 44, 124, 44)
    rect(idx, cx - 62, h - 44, 124, 2, LM_RAMP_STOPS, wrap=False)
    for i in range(cx - 58, cx + 58, 14):
        rect(idx, i, h - 38, 3, 30, 2, wrap=False)
    # 축 + 트러스
    shaded_box(cx - 22, 96, 44, h - 140)
    for y in range(100, h - 44, 16):
        thick_line(idx, cx - 22, y, cx + 22, y + 16, 2, LM_RAMP_STOPS - 4, wrap=False)
        thick_line(idx, cx + 22, y, cx - 22, y + 16, 2, LM_RAMP_STOPS - 4, wrap=False)
        rect(idx, cx - 24, y, 48, 2, LM_RAMP_STOPS - 1, wrap=False)
    # 관제실 (넓은 머리)
    shaded_box(cx - 58, 56, 116, 42)
    rect(idx, cx - 58, 56, 116, 2, LM_RAMP_STOPS, wrap=False)
    rect(idx, cx - 66, 92, 132, 8, LM_RAMP_STOPS - 5, wrap=False)
    rect(idx, cx - 66, 92, 132, 2, LM_RAMP_STOPS - 1, wrap=False)
    for i in range(cx - 52, cx + 50, 12):
        rect(idx, i, 66, 7, 14, LM_RAMP_STOPS + 3, wrap=False)   # 창
        rect(idx, i, 66, 7, 2, LM_RAMP_STOPS + 2, wrap=False)
    # 상부 링 + 안테나
    shaded_box(cx - 30, 34, 60, 22)
    rect(idx, cx - 34, 34, 68, 3, LM_RAMP_STOPS, wrap=False)
    rect(idx, cx - 3, 2, 6, 34, LM_RAMP_STOPS - 4, wrap=False)
    rect(idx, cx - 3, 2, 2, 34, LM_RAMP_STOPS - 1, wrap=False)
    for y, ln in [(8, 26), (14, 20), (20, 14)]:
        rect(idx, cx - ln, y, ln * 2, 2, LM_RAMP_STOPS - 3, wrap=False)
    disc(idx, cx, 2, 3.4, LM_RAMP_STOPS + 2, wrap=False)
    disc(idx, cx, 2, 1.6, LM_RAMP_STOPS + 3, wrap=False)
    # 접시 안테나
    for sx in (cx - 46, cx + 42):
        disc(idx, sx, 46, 9, LM_RAMP_STOPS - 3, wrap=False)
        disc(idx, sx + 2, 46, 6.5, LM_RAMP_STOPS - 7, wrap=False)
    # 경고등
    for i, y in enumerate(range(110, h - 50, 34)):
        rect(idx, cx - 26, y, 4, 4, LM_RAMP_STOPS + 2, wrap=False)
        rect(idx, cx + 22, y, 4, 4, LM_RAMP_STOPS + 1, wrap=False)
    return idx


def lm_nebula(rng, w, h):
    """번개 치는 거목 구름 — 적란운 덩어리 안에서 가지치는 뇌격."""
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    field = np.zeros((h, w))

    def blob(bx, by, r):
        rad = r * 2.4                                            # 유한 지지 커널
        q = 1.0 - ((xx - bx) ** 2 + (yy - by) ** 2) / (rad * rad)
        np.add(field, np.clip(q, 0, 1) ** 3, out=field)

    # 거목(모루) 실루엣: 위가 넓게 퍼지고 아래로 좁아지는 줄기.
    for _ in range(90):
        t = rng.random() ** 0.8
        by = 30 + t * (h - 62)
        spread = (w * 0.34) * (1 - t * 0.72)                     # 아래로 갈수록 좁게
        bx = w * 0.5 + rng.normal(0, max(6.0, spread * 0.55))
        blob(bx, by, rng.uniform(13, 26) * (1 - t * 0.42))
    for _ in range(46):                                          # 위로 퍼지는 수관
        a = rng.uniform(0, np.pi)
        rad = rng.uniform(0, w * 0.44)
        blob(w * 0.5 + np.cos(a) * rad, 46 - np.sin(a) * 32 + rng.normal(0, 8),
             rng.uniform(15, 30))

    thr = 0.30
    cloud = field > thr
    lvl = np.clip((field - thr) / max(1e-3, np.percentile(field, 99.5) - thr), 0, 1)
    light = np.clip(0.26 + 0.55 * (1 - yy / h) + 0.26 * (1 - np.abs(xx - w * 0.36) / w), 0, 1)
    _shade(idx, cloud, np.clip(lvl * 0.40 + light * 0.72, 0, 1))
    idx[cloud & (lvl < 0.06)] = 2                                # 가장자리를 어둡게 뜯는다

    # 뇌격 나무
    def bolt(x0, y0, x1, y1, depth):
        seg = 5
        pts = [(x0, y0)]
        for i in range(1, seg + 1):
            t = i / seg
            pts.append((x0 + (x1 - x0) * t + rng.normal(0, 9 * (1 - t)),
                        y0 + (y1 - y0) * t))
        for (ax, ay), (bx, by) in zip(pts, pts[1:]):
            thick_line(idx, ax, ay, bx, by, 4 - depth, LM_RAMP_STOPS + 1, wrap=False)
            thick_line(idx, ax, ay, bx, by, max(1, 2 - depth), LM_RAMP_STOPS + 3, wrap=False)
        if depth < 2:
            for _ in range(2):
                mx, my = pts[rng.integers(1, seg)]
                bolt(mx, my, mx + rng.uniform(-70, 70), my + rng.uniform(30, 70), depth + 1)

    bolt(w * 0.5, 40, w * 0.5 + rng.uniform(-40, 40), h - 20, 0)
    # 방전 주변 발광
    glow = idx >= LM_RAMP_STOPS + 1
    for dy, dx in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
        s = np.roll(np.roll(glow, dy, axis=0), dx, axis=1)
        idx[(idx > 0) & s & ~glow] = LM_RAMP_STOPS + 2
    return idx


def lm_core(rng, w, h):
    """거대 코어 구체 — 구면 셰이딩 + 위도 회로선 + 적도 링 + 중심 발광."""
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    cx, cy = w * 0.5, h * 0.5
    r = w * 0.36
    nx = (xx - cx) / r
    ny = (yy - cy) / r
    d2 = nx ** 2 + ny ** 2
    ball = d2 <= 1.0
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    lam = np.clip(nx * -0.48 + ny * -0.58 + nz * 0.66, 0, 1)
    rim = np.clip((d2 - 0.72) / 0.28, 0, 1) * 0.55
    _shade(idx, ball, np.clip(lam ** 0.85 * 0.85 + rim, 0, 1))

    # 위도 회로선 (구면 투영)
    lat = np.arcsin(np.clip(ny, -1, 1))
    band = (np.abs(np.sin(lat * 7.0)) < 0.10) & ball
    idx[band] = LM_RAMP_STOPS
    lon = np.arctan2(nx, np.maximum(nz, 1e-3))
    mer = (np.abs(np.sin(lon * 5.0)) < 0.06) & ball & (nz > 0.18)
    idx[mer] = LM_RAMP_STOPS - 2

    # 중심 발광 코어
    glow = d2 <= 0.16
    idx[glow] = LM_RAMP_STOPS + 1
    idx[d2 <= 0.075] = LM_RAMP_STOPS + 2
    idx[d2 <= 0.026] = LM_RAMP_STOPS + 3
    for k in range(12):                                          # 방사 스포크
        a = k * np.pi / 6 + 0.13
        thick_line(idx, cx, cy, cx + np.cos(a) * r * 0.94, cy + np.sin(a) * r * 0.94,
                   2, LM_RAMP_STOPS + 1, wrap=False)
    idx[~ball & (idx > 0)] = 0

    # 적도 링 (구체 앞뒤로 걸치는 타원 고리)
    ex, ey = (xx - cx) / (r * 1.30), (yy - cy) / (r * 0.30)
    ring = (np.abs(ex ** 2 + ey ** 2 - 1.0) < 0.30) & (np.abs(ey) < 3.5)
    idx[ring] = LM_RAMP_STOPS - 4
    idx[ring & (yy > cy)] = LM_RAMP_STOPS - 1                    # 앞쪽 고리를 밝게
    for k in range(16):                                          # 고리 노드
        a = k * np.pi / 8
        disc(idx, cx + np.cos(a) * r * 1.30, cy + np.sin(a) * r * 0.30, 2.6,
             LM_RAMP_STOPS + 2, wrap=False)
    return idx


def lm_abyss(rng, w, h):
    """가라앉은 거대 갈비뼈 — 레비아탄보다 먼저 죽은 무언가의 골격.

    다가올 보스를 말로 하지 않고 크기로 말한다. 플레이어 기체 옆을 지나가는
    이 뼈대가 레비아탄만 하다면, 레비아탄이 어느 정도인지 이미 안 셈이다.
    """
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    u = xx / (w - 1.0)

    # 척추 — 완만하게 휘어 침전물에 반쯤 파묻힌다.
    spine = 0.46 * h + 0.20 * h * np.sin(u * np.pi * 0.85 + 0.4)
    core_band = np.abs(yy - spine) < (7 - 3 * u)
    _shade(idx, core_band, np.clip(0.42 + 0.30 * (1 - u), 0, 1))

    # 늑골 — 척추에서 아래로 벌어져 내려간다. 뒤쪽일수록 짧고 어둡다.
    ribs = 17
    for i in range(ribs):
        t = (i + 0.5) / ribs
        sx = t * w * 0.92
        sy = 0.46 * h + 0.20 * h * np.sin(t * np.pi * 0.85 + 0.4)
        span = (0.40 - 0.24 * t) * h
        bow = (0.16 - 0.09 * t) * w
        level = 0.52 - 0.30 * t
        steps = int(span)
        for k in range(steps):
            f = k / max(1, steps - 1.0)
            px = sx + bow * np.sin(f * np.pi * 0.62)
            py = sy + span * f
            if 0 <= px < w and 0 <= py < h:
                disc(idx, px, py, max(1.0, 3.4 - 2.0 * t), 0, wrap=False)
                m = np.zeros((h, w), bool)
                y0, y1 = int(max(0, py - 4)), int(min(h, py + 5))
                x0, x1 = int(max(0, px - 4)), int(min(w, px + 5))
                sub = ((xx[y0:y1, x0:x1] - px) ** 2
                       + (yy[y0:y1, x0:x1] - py) ** 2) <= max(1.0, 3.4 - 2.0 * t) ** 2
                m[y0:y1, x0:x1] = sub
                _shade(idx, m, np.full((h, w), np.clip(level, 0, 1)))

    # 두개골 — 앞쪽(왼쪽)의 큰 덩어리. 눈구멍만 비워 둔다.
    scx, scy, sr = w * 0.10, 0.44 * h, 0.17 * h
    skull = ((xx - scx) / (sr * 1.5)) ** 2 + ((yy - scy) / sr) ** 2 <= 1.0
    _shade(idx, skull, np.clip(0.60 - 0.25 * ((yy - scy) / max(1.0, sr)), 0, 1))
    socket = ((xx - scx + sr * 0.5) ** 2 + (yy - scy + sr * 0.2) ** 2) <= (sr * 0.34) ** 2
    idx[socket] = 0
    # 눈구멍 안쪽의 생물발광 — 죽은 것이 아직 빛난다.
    disc(idx, scx - sr * 0.5, scy - sr * 0.2, sr * 0.16, LM_RAMP_STOPS + 3, wrap=False)

    # 침전물에 묻힌 아래쪽을 깎아 낸다 — 바닥선 아래는 보이지 않는다.
    silt = 0.86 * h + 0.05 * h * np.sin(u * np.pi * 2.2)
    idx[yy > silt] = 0

    # 주변에 떠 있는 발광 미립자
    for _ in range(90):
        px, py = rng.uniform(0, w), rng.uniform(0, h * 0.88)
        if idx[int(py), int(px)] == 0:
            disc(idx, px, py, rng.uniform(0.7, 1.7),
                 LM_RAMP_STOPS + int(rng.integers(1, 4)), wrap=False)
    return idx


def lm_brood(rng, w, h):
    """산란관 — 천장에서 내려온 거대 기관. 알집이 매달려 부풀고 있다."""
    idx = np.zeros((h, w), np.uint8)
    yy, xx = np.mgrid[0:h, 0:w]
    v = yy / (h - 1.0)

    # 본체 — 위가 굵고 아래로 좁아지는 관. 마디마다 부풀어 있다.
    cx = w * 0.5 + w * 0.06 * np.sin(v * np.pi * 1.4)
    radius = (0.30 - 0.16 * v) * w * (1.0 + 0.14 * np.sin(v * np.pi * 6.0))
    body = np.abs(xx - cx) <= radius
    # 왼쪽에서 오는 빛 — 관 왼쪽이 밝고 오른쪽이 어둡다.
    lit = np.clip(0.78 - 0.62 * (xx - cx + radius) / np.maximum(1.0, radius * 2), 0, 1)
    _shade(idx, body, lit)

    # 마디 주름 — 가로로 감긴 띠
    for k in range(11):
        t = (k + 0.5) / 11
        band = (np.abs(v - t) < 0.012) & body
        _shade(idx, band, np.full((h, w), 0.22))

    # 매달린 알집 — 관 아래쪽에 무겁게 뭉친다.
    for _ in range(22):
        t = rng.uniform(0.30, 0.98)
        py = t * h
        rad = rng.uniform(7.0, 20.0) * (0.5 + t)
        px = w * 0.5 + w * 0.06 * np.sin(t * np.pi * 1.4) \
            + rng.choice([-1, 1]) * ((0.30 - 0.16 * t) * w + rad * 0.55)
        if not (0 <= px < w and 0 <= py < h):
            continue
        disc(idx, px, py, rad, LM_RAMP_STOPS - 4, wrap=False)
        disc(idx, px, py, rad * 0.74, LM_RAMP_STOPS + 1, wrap=False)
        disc(idx, px, py, rad * 0.42, LM_RAMP_STOPS + 3, wrap=False)
        thick_line(idx, px, py - rad, w * 0.5, py - rad * 1.8, 2.0,
                   LM_RAMP_STOPS - 6, wrap=False)

    # 끝에서 늘어진 힘줄
    for _ in range(9):
        sx = w * 0.5 + rng.integers(-int(w * 0.12), int(w * 0.12))
        thick_line(idx, sx, h * 0.92, sx + rng.integers(-24, 25), h - 1,
                   rng.uniform(1.5, 3.0), LM_RAMP_STOPS - 8, wrap=False)
    return idx


LM_BUILDERS = {"scrap": lm_scrap, "hive": lm_hive, "fort": lm_fort,
               "nebula": lm_nebula, "core": lm_core,
               "abyss": lm_abyss, "brood": lm_brood}
LM_SEEDS = {"scrap": 5101, "hive": 5202, "fort": 5303, "nebula": 5404, "core": 5505,
            "abyss": 5606, "brood": 5707}


# ── 실행 ──────────────────────────────────────────────────────────────────────

def build(prefix, out_dir, src_dir):
    print(f"[{prefix}]")
    dusk_base, dusk_acc, dark_base, dark_acc = FAR_RAMPS[prefix]
    src = src_dir / f"{prefix}_far.png"
    if not src.exists():
        raise SystemExit(f"{src} 가 없다 — 기존 원경이 있어야 변형을 만든다.")
    make_far_variant(src, out_dir / f"{prefix}_far_dusk.png", dusk_base, dusk_acc,
                     gamma=0.92, tone="dusk", target_lum=DUSK_LUM)
    make_far_variant(src, out_dir / f"{prefix}_far_dark.png", dark_base, dark_acc,
                     gamma=1.22, tone="dark", target_lum=DARK_LUM)

    rng = np.random.default_rng(FG_SEEDS[prefix])
    save_indexed(FG_BUILDERS[prefix](rng), FG_PALETTES[prefix],
                 out_dir / f"{prefix}_fg.png")

    w, h, body, acc = LM_SPECS[prefix]
    rng = np.random.default_rng(LM_SEEDS[prefix])
    idx = LM_BUILDERS[prefix](rng, w, h)
    pal = [tuple(c) for c in ramp(body, LM_RAMP_STOPS)] + [tuple(c) for c in ramp(acc, 4)]
    save_indexed(idx, pal, out_dir / f"{prefix}_landmark.png")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--out", default=str(DEFAULT_OUT))
    ap.add_argument("--src", default=str(DEFAULT_OUT), help="기존 <prefix>_far.png가 있는 곳")
    args = ap.parse_args()
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    targets = [p.strip() for p in args.only.split(",") if p.strip()] or PREFIXES
    for p in targets:
        build(p, out_dir, Path(args.src))


if __name__ == "__main__":
    main()
