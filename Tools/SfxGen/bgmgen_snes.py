"""SNES급 BGM 생성기.

기존 bgmgen.py는 구형파/삼각파 + 지수감쇠뿐이라 음색이 NES(8비트)에 머물렀다
("음악은 너무 8비트인데", 2026-07-30). SNES가 8비트와 다른 지점은 파형이 아니라
**편성**이다:

  1. 샘플 기반 악기 — 스트링·브라스·벨처럼 배음이 살아 있는 음색
  2. 하드웨어 에코(DSP) — SNES 음악의 공간감은 거의 전부 여기서 온다
  3. 8채널 폴리포니 — 화성이 두껍고 대선율이 들어간다
  4. 32kHz / 벨로시티 표현 — 기계적이지 않은 연주

여기서는 실제 샘플이 없으므로 감산·FM 합성으로 그 음색을 근사하고, 에코와
스테레오 배치로 공간을 만든다. 화성 설계(테마별 진행·스케일)는 기존 bgmgen.py의
의도를 그대로 물려받았다 — 그쪽이 잘 되어 있었고 문제는 음색이었다.

용량: 스테레오로 가면 파일이 2배가 되므로 SNES 네이티브인 32kHz를 쓰고 루프를
짧게 잡는다. 기존(모노 44.1kHz 32마디, 합계 35MB)보다 작으면서 음질은 올라간다.

사용법:
    python bgmgen_snes.py --theme scrapyard --out bgm_scrapyard.wav
    python bgmgen_snes.py --all --outdir ../../Assets/Audio/Bgm
"""

from __future__ import annotations

import argparse
import os
import struct
import wave

import numpy as np

SR = 32000          # SNES 네이티브 샘플레이트
MASTER = 0.92       # 최종 헤드룸
TARGET_RMS = 0.20   # 곡 간 체감 볼륨을 맞추는 기준


# ── 기본 유틸 ────────────────────────────────────────────────────────────────

def note_hz(semi: float) -> float:
    """A4=0 기준 반음 → Hz."""
    return 440.0 * (2.0 ** (semi / 12.0))


def env_adsr(n: int, attack: float, decay: float, sustain: float,
             release: float) -> np.ndarray:
    """초 단위 ADSR. 노트 길이가 짧으면 구간을 비례 축소한다."""
    a = max(1, int(attack * SR))
    d = max(1, int(decay * SR))
    r = max(1, int(release * SR))
    if a + d + r > n:                      # 짧은 노트는 전체를 눌러 담는다
        scale = n / float(a + d + r)
        a, d, r = max(1, int(a * scale)), max(1, int(d * scale)), max(1, int(r * scale))
    s = max(0, n - a - d - r)
    return np.concatenate([
        np.linspace(0.0, 1.0, a, endpoint=False) ** 1.5,   # 완만한 어택 곡선
        np.linspace(1.0, sustain, d, endpoint=False),
        np.full(s, sustain),
        np.linspace(sustain, 0.0, r) ** 1.5,
    ])[:n]


def lp(x: np.ndarray, cutoff_hz: float, order: float = 1.0) -> np.ndarray:
    """주파수 영역 저역 필터 (-6dB/oct × order).

    scipy가 없어 IIR을 벡터화할 수 없다. 처음엔 박스 필터를 썼는데 차단이
    완만해서 지정한 cutoff보다 훨씬 아래까지 죽었다 — 곡 전체가 답답해졌다
    (스펙트럼 중심 1426Hz, 기존 NES판은 5070Hz). FFT는 O(n log n)이면서
    차단 주파수가 정확해 음색을 의도한 대로 잡을 수 있다.
    """
    n = len(x)
    if cutoff_hz >= SR / 2 or n < 16:
        return x
    f = np.fft.rfftfreq(n, 1.0 / SR)
    h = 1.0 / np.sqrt(1.0 + (f / cutoff_hz) ** 2) ** order
    return np.fft.irfft(np.fft.rfft(x) * h, n)


def hp(x: np.ndarray, cutoff_hz: float) -> np.ndarray:
    """고역 통과 — 하이햇·스네어의 공기감을 남기는 데 쓴다."""
    n = len(x)
    if cutoff_hz <= 20.0 or n < 16:
        return x
    f = np.fft.rfftfreq(n, 1.0 / SR)
    r = f / cutoff_hz
    h = r / np.sqrt(1.0 + r ** 2)
    return np.fft.irfft(np.fft.rfft(x) * h, n)


def saturate(x: np.ndarray, amount: float = 2.0) -> np.ndarray:
    """부드러운 새추레이션 — 배음을 더해 샘플 음색처럼 두껍게."""
    return np.tanh(x * amount) / np.tanh(amount)


def phase(freq: np.ndarray | float, n: int) -> np.ndarray:
    """주파수(스칼라 또는 샘플별 배열)를 누적 위상으로."""
    f = np.full(n, float(freq)) if np.isscalar(freq) else freq[:n]
    return np.cumsum(2.0 * np.pi * f / SR)


def saw(ph: np.ndarray) -> np.ndarray:
    return 2.0 * ((ph / (2.0 * np.pi)) % 1.0) - 1.0


def sq(ph: np.ndarray, duty: float = 0.5) -> np.ndarray:
    return np.where(((ph / (2.0 * np.pi)) % 1.0) < duty, 1.0, -1.0)


def tri(ph: np.ndarray) -> np.ndarray:
    t = (ph / (2.0 * np.pi)) % 1.0
    return 4.0 * np.abs(t - 0.5) - 1.0


def vibrato(freq: float, n: int, depth_cents: float, rate_hz: float,
            delay_s: float = 0.08) -> np.ndarray:
    """피치 변동. 어택 직후부터 서서히 걸려 기계적으로 들리지 않게 한다."""
    t = np.arange(n) / SR
    ramp = np.clip((t - delay_s) / 0.25, 0.0, 1.0)
    cents = np.sin(2.0 * np.pi * rate_hz * t) * depth_cents * ramp
    return freq * (2.0 ** (cents / 1200.0))


# ── 악기 ─────────────────────────────────────────────────────────────────────

def inst_strings(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """디튠 톱니 3겹 + 느린 어택. SNES 스트링 섹션의 핵심은 디튠 두께다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 7.0, 4.6)
    x = (saw(phase(fv, n))
         + 0.8 * saw(phase(fv * 1.0038, n))
         + 0.8 * saw(phase(fv * 0.9963, n))) / 2.6
    x = lp(x, 2400 + 900 * vel)
    return x * env_adsr(n, 0.11, 0.10, 0.82, 0.22) * vel


def inst_brass(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """톱니 + 필터 엔벨로프. 밝게 열렸다 닫히는 것이 브라스의 인상을 만든다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 5.0, 5.2, delay_s=0.12)
    x = (saw(phase(fv, n)) + 0.55 * sq(phase(fv * 1.0021, n), 0.42)) / 1.5
    # 필터 스윕을 두 대역의 크로스페이드로 근사한다 (시변 IIR 없이)
    bright, dark = lp(x, 4200), lp(x, 1500)
    sweep = np.clip(np.linspace(1.0, 0.0, n) * 2.2, 0.0, 1.0)
    x = bright * sweep + dark * (1.0 - sweep)
    x = saturate(x, 1.6)
    return x * env_adsr(n, 0.035, 0.14, 0.72, 0.16) * vel


def inst_bell(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """FM 벨/마림바. 모듈레이터가 빨리 죽어 어택만 금속적이다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    t = np.arange(n) / SR
    mod = np.sin(2.0 * np.pi * f * 3.01 * t) * np.exp(-t * 14.0) * 2.4
    x = np.sin(2.0 * np.pi * f * t + mod)
    x += 0.3 * np.sin(2.0 * np.pi * f * 2.0 * t) * np.exp(-t * 9.0)
    return x * env_adsr(n, 0.004, 0.30, 0.18, 0.30) * vel


def inst_bass(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """사인 + 서브 + 새추레이션. 폰 스피커에서도 들리게 배음을 남긴다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    ph = phase(f, n)
    x = np.sin(ph) + 0.45 * tri(ph) + 0.3 * np.sin(ph * 0.5)
    x = saturate(x * 0.7, 2.4)
    x = lp(x, 1100)
    return x * env_adsr(n, 0.008, 0.06, 0.86, 0.09) * vel


def inst_pad(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """아주 느린 어택의 패드. 화성을 채워 빈 곳을 없앤다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 4.0, 3.1, delay_s=0.3)
    x = (np.sin(phase(fv, n))
         + 0.6 * saw(phase(fv * 1.0047, n))
         + 0.6 * np.sin(phase(fv * 0.9955, n) * 2.0)) / 2.2
    x = lp(x, 1700)
    return x * env_adsr(n, 0.35, 0.2, 0.75, 0.5) * vel


def inst_lead(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """리드. 톱니+사각에 비브라토를 얹어 노래하듯이."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 11.0, 5.6, delay_s=0.1)
    x = (saw(phase(fv, n)) * 0.7 + sq(phase(fv, n), 0.45) * 0.5
         + 0.35 * np.sin(phase(fv * 2.0, n)))
    x = lp(x, 3600)
    x = saturate(x * 0.8, 1.5)
    return x * env_adsr(n, 0.02, 0.09, 0.80, 0.14) * vel


def drum_kick(vel: float = 1.0) -> np.ndarray:
    n = int(0.16 * SR)
    t = np.arange(n) / SR
    f = 118.0 * np.exp(-t * 26.0) + 44.0          # 피치 드롭
    x = np.sin(2.0 * np.pi * np.cumsum(f) / SR)
    x += 0.18 * np.random.default_rng(1).uniform(-1, 1, n) * np.exp(-t * 190.0)
    return saturate(x * np.exp(-t * 12.0), 1.9) * vel


def drum_snare(vel: float = 1.0, rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(2)
    n = int(0.15 * SR)
    t = np.arange(n) / SR
    noise = rng.uniform(-1, 1, n)
    body = 0.55 * np.sin(2.0 * np.pi * 205.0 * t) + 0.35 * np.sin(2.0 * np.pi * 315.0 * t)
    x = lp(noise, 7500, 1.0) * 1.3 + hp(noise, 4000) * 0.5 + body
    return x * np.exp(-t * 22.0) * vel


def drum_hat(vel: float = 1.0, open_: bool = False,
             rng: np.random.Generator | None = None) -> np.ndarray:
    rng = rng or np.random.default_rng(3)
    n = int((0.14 if open_ else 0.045) * SR)
    t = np.arange(n) / SR
    x = rng.uniform(-1, 1, n)
    x = hp(x, 6000)                                # 고역만 남긴다
    return x * np.exp(-t * (14.0 if open_ else 55.0)) * vel


def drum_tom(f: float, vel: float = 1.0) -> np.ndarray:
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    fr = f * (1.0 + 0.5 * np.exp(-t * 20.0))
    x = np.sin(2.0 * np.pi * np.cumsum(fr) / SR)
    return x * np.exp(-t * 11.0) * vel


# ── 이펙트 ───────────────────────────────────────────────────────────────────

def echo(x: np.ndarray, delay_ms: float, feedback: float, taps: int = 6,
         damp_hz: float = 2600.0) -> np.ndarray:
    """SNES DSP 에코 근사. 피드백 루프를 다중 탭으로 펼쳐 벡터화한다.

    탭마다 저역을 더 깎아 꼬리가 어두워지게 한다 — 실제 피드백 루프의
    필터가 반복 적용되는 것과 같은 효과다.
    """
    out = x.copy()
    d = int(SR * delay_ms / 1000.0)
    if d < 1:
        return out
    sig = x
    for k in range(1, taps + 1):
        if d * k >= len(x):
            break
        sig = lp(sig, damp_hz)
        tap = np.zeros_like(x)
        tap[d * k:] = sig[:len(x) - d * k]
        out += tap * (feedback ** k)
    return out


def normalize(x: np.ndarray, peak: float = 1.0) -> np.ndarray:
    m = float(np.max(np.abs(x))) if len(x) else 0.0
    return x * (peak / m) if m > 1e-9 else x


# ── 테마 ─────────────────────────────────────────────────────────────────────
# 진행/스케일은 기존 bgmgen.py의 설계를 물려받았다. instruments/echo/feel만
# SNES 편성으로 새로 정했다.

THEMES = {
    "scrapyard": dict(
        bpm=132, bars=16,
        progression=[-24, -28, -33, -26, -24, -28, -29, -29],   # Am F C G / Am F E E
        scale=[0, 3, 5, 7, 10, 12],                             # 마이너 펜타토닉
        lead="lead", pad="strings", echo=(150, 0.34),
        drums="rock", motif_seed=11,
    ),
    "hive": dict(
        bpm=112, bars=16,
        progression=[-26, -26, -30, -25, -26, -26, -31, -25],   # 프리지안 압박
        scale=[0, 1, 3, 5, 7, 8, 12],
        lead="bell", pad="pad", echo=(210, 0.44),
        drums="tribal", motif_seed=23,
    ),
    "fortress": dict(
        bpm=148, bars=16,
        progression=[-24, -24, -27, -29, -24, -24, -22, -29],   # 드라이빙
        scale=[0, 3, 5, 6, 7, 10, 12],                          # 블루스 마이너
        lead="brass", pad="brass", echo=(120, 0.28),
        drums="march", motif_seed=37,
    ),
    "nebula": dict(
        bpm=100, bars=16,
        progression=[-21, -26, -19, -24, -21, -26, -14, -24],   # 부유하는 메이저7
        scale=[0, 2, 4, 7, 9, 12],
        lead="bell", pad="pad", echo=(280, 0.5),
        drums="sparse", motif_seed=53,
    ),
    "core": dict(
        bpm=160, bars=16,
        progression=[-24, -23, -24, -22, -24, -23, -19, -17],   # 반음 상승 압박
        scale=[0, 1, 4, 5, 8, 11, 12],                          # 더블 하모닉
        lead="brass", pad="strings", echo=(135, 0.32),
        drums="march", motif_seed=71,
    ),
    "boss": dict(
        bpm=172, bars=16,
        progression=[-24, -24, -18, -19, -24, -24, -17, -12],   # 5도 도약 전투
        scale=[0, 2, 3, 5, 7, 8, 11, 12],                       # 하모닉 마이너
        lead="brass", pad="strings", echo=(110, 0.26),
        drums="rock", motif_seed=89,
    ),
    "title": dict(
        bpm=96, bars=16,
        progression=[-24, -19, -16, -12, -24, -19, -14, -12],   # 상승 희망
        scale=[0, 2, 4, 7, 9, 12],
        lead="strings", pad="pad", echo=(240, 0.42),
        drums="sparse", motif_seed=101,
    ),
}

INSTRUMENTS = {
    "strings": inst_strings, "brass": inst_brass, "bell": inst_bell,
    "bass": inst_bass, "pad": inst_pad, "lead": inst_lead,
}


# ── 작곡 ─────────────────────────────────────────────────────────────────────

def make_motif(scale: list[int], rng: np.random.Generator) -> list[tuple[int, float]]:
    """기억되는 모티프를 만든다.

    무작위 음렬은 "게임 음악"으로 들리지 않는다. SNES 곡의 인상은 짧은 모티프가
    반복·변주되는 데서 온다. 그래서 4~5음 모티프를 뽑아 두고 마디마다 변형해 쓴다.
    도약 뒤에는 순차 진행을 넣어 선율이 흐르게 한다.
    """
    length = int(rng.integers(4, 6))
    motif: list[tuple[int, float]] = []
    idx = int(rng.integers(0, len(scale)))
    for i in range(length):
        # 리듬: 8분 위주에 4분을 섞어 숨을 준다
        dur = float(rng.choice([0.5, 0.5, 0.5, 1.0, 1.0, 1.5]))
        motif.append((scale[idx % len(scale)] + 12 * (idx // len(scale)), dur))
        step = int(rng.integers(-2, 4))
        if abs(step) >= 3:              # 도약 뒤에는 순차로 되돌린다
            step = 1 if step > 0 else -1
        idx = max(0, min(len(scale) * 2 - 1, idx + step))
    return motif


def vary_motif(motif: list[tuple[int, float]], bar: int,
               rng: np.random.Generator) -> list[tuple[int, float]]:
    """마디에 따라 모티프를 변주한다 (전위·반복·꼬리 변경)."""
    out = list(motif)
    if bar % 4 == 2:                                    # 3번째 마디: 위로 전위
        out = [(p + 3, d) for p, d in out]
    elif bar % 4 == 3:                                  # 4번째 마디: 꼬리를 바꿔 마무리
        out = out[:-1] + [(out[-1][0] - 2, out[-1][1] + 0.5)]
    if bar % 8 >= 4:                                    # 후반부: 옥타브 위로
        out = [(p + 12, d) for p, d in out]
    if rng.random() < 0.25:                             # 가끔 첫 음을 늦게 (당김음)
        out = [(out[0][0], out[0][1] * 0.5)] + out[1:]
    return out


def drum_pattern(style: str, step: int, bar: int,
                 rng: np.random.Generator) -> list[tuple[str, float]]:
    """16분 스텝별 드럼. 8스텝(=8분음표) 기준."""
    hits: list[tuple[str, float]] = []
    fill = (bar % 8 == 7)                               # 8마디마다 필

    if style == "rock":
        if step in (0, 4) or (step == 6 and bar % 4 == 3):
            hits.append(("kick", 1.0))
        if step in (2, 6):
            hits.append(("snare", 0.85))
        hits.append(("hat", 0.3 if step % 2 else 0.45))
    elif style == "march":
        if step in (0, 3, 4):
            hits.append(("kick", 0.95))
        if step in (2, 6):
            hits.append(("snare", 0.8))
        if step % 2 == 0:
            hits.append(("hat", 0.35))
    elif style == "tribal":
        if step in (0, 5):
            hits.append(("kick", 0.9))
        if step in (3, 6):
            hits.append(("tom", 0.7))
        if step == 7:
            hits.append(("hat", 0.4))
    else:                                               # sparse
        if step == 0:
            hits.append(("kick", 0.8))
        if step == 4 and bar % 2 == 1:
            hits.append(("snare", 0.5))
        if step in (2, 6):
            hits.append(("hat", 0.22))

    if fill and step >= 4:
        hits.append(("tom", 0.55 + 0.1 * (step - 4)))
    return hits


def arrange(theme_name: str, seed: int) -> tuple[np.ndarray, np.ndarray]:
    """한 곡을 스테레오로 편곡한다."""
    cfg = THEMES[theme_name]
    rng = np.random.default_rng(seed + cfg["motif_seed"])

    bpm, bars = cfg["bpm"], cfg["bars"]
    beat = 60.0 / bpm
    bar_len = beat * 4
    total = int(bar_len * bars * SR) + SR              # 에코 꼬리 여유

    left = np.zeros(total)
    right = np.zeros(total)

    def place(buf: np.ndarray, sig: np.ndarray, at: float, gain: float) -> None:
        i = int(at * SR)
        if i < 0 or i >= len(buf) or len(sig) == 0:
            return
        end = min(len(buf), i + len(sig))
        buf[i:end] += sig[:end - i] * gain

    def stereo_place(sig: np.ndarray, at: float, gain: float, pan: float) -> None:
        """pan -1(왼) ~ +1(오). 등파워 팬닝."""
        ang = (pan * 0.5 + 0.5) * (np.pi / 2)
        place(left, sig, at, gain * float(np.cos(ang)))
        place(right, sig, at, gain * float(np.sin(ang)))

    prog = cfg["progression"]
    scale = cfg["scale"]
    lead_inst = INSTRUMENTS[cfg["lead"]]
    pad_inst = INSTRUMENTS[cfg["pad"]]
    motif = make_motif(scale, rng)

    for bar in range(bars):
        t0 = bar * bar_len
        root = prog[bar % len(prog)]
        # 섹션: 앞 4마디는 얇게 시작해 뒤로 갈수록 채운다 (루프가 지루해지지 않게)
        section = bar // 4
        intro = (section == 0)

        # --- 베이스: 루트 + 5도, 8분 펄스로 추진력 ---
        for k in range(8):
            p = root if k % 4 != 3 else root + 7
            dur = beat * 0.5 * 0.92
            vel = 0.95 if k % 2 == 0 else 0.7
            stereo_place(inst_bass(note_hz(p - 12), dur, vel),
                         t0 + k * beat * 0.5, 0.42, 0.0)

        # --- 패드/스트링: 3화음을 길게 깔아 화성을 채운다 ---
        third = 3 if (root % 12) in (0, 2, 3, 5, 7, 8, 10) else 4
        chord = [root, root + third, root + 7]
        if not intro:
            chord.append(root + 12)
        for ci, p in enumerate(chord):
            pan = -0.55 + 1.1 * (ci / max(1, len(chord) - 1))
            stereo_place(pad_inst(note_hz(p), bar_len * 0.98, 0.34 if intro else 0.46),
                         t0, 0.3, pan)

        # --- 아르페지오: 좌우 핑퐁으로 움직임을 만든다 ---
        if not intro:
            arp = [root + 12, root + 12 + third, root + 19, root + 24]
            for k in range(8):
                p = arp[k % len(arp)]
                pan = -0.7 if k % 2 == 0 else 0.7
                stereo_place(inst_bell(note_hz(p), beat * 0.5, 0.3),
                             t0 + k * beat * 0.5, 0.22, pan)

        # --- 리드: 모티프와 그 변주 ---
        if section >= 1:
            phrase = vary_motif(motif, bar, rng)
            at = t0
            for pitch, dur in phrase:
                if at >= t0 + bar_len:
                    break
                length = min(beat * dur, t0 + bar_len - at)
                vel = 0.9 if dur >= 1.0 else 0.78
                stereo_place(lead_inst(note_hz(root + 24 + pitch), length * 0.95, vel),
                             at, 0.34, 0.12)
                at += beat * dur

        # --- 드럼 ---
        for k in range(8):
            for name, vel in drum_pattern(cfg["drums"], k, bar, rng):
                at = t0 + k * beat * 0.5
                if name == "kick":
                    stereo_place(drum_kick(vel), at, 0.5, 0.0)
                elif name == "snare":
                    stereo_place(drum_snare(vel, rng), at, 0.34, -0.08)
                elif name == "hat":
                    stereo_place(drum_hat(vel, k == 7, rng), at, 0.26, 0.35)
                elif name == "tom":
                    stereo_place(drum_tom(150 + 40 * (k % 4), vel), at, 0.3, -0.3)

    # --- 에코: SNES 공간감의 핵심. 좌우 딜레이를 살짝 다르게 해 넓이를 만든다 ---
    d_ms, fb = cfg["echo"]
    left = echo(left, d_ms, fb)
    right = echo(right, d_ms * 1.14, fb)

    # 루프 이음새: 에코 꼬리를 앞으로 접어 넣어 루프가 끊기지 않게 한다
    loop_n = int(bar_len * bars * SR)
    for buf in (left, right):
        tail = buf[loop_n:].copy()
        if len(tail):
            m = min(len(tail), loop_n)
            buf[:m] += tail[:m] * 0.85
    left, right = left[:loop_n], right[:loop_n]

    # 마스터: 피크 정규화 → 새추레이션으로 붙임성 → 라우드니스 정렬.
    #
    # 피크만 맞추면 곡마다 체감 볼륨이 달라진다 (드럼이 촘촘한 boss는 피크가 같아도
    # 조용하게 들린다). 스테이지가 넘어갈 때 음량이 튀지 않도록 RMS를 목표값에
    # 맞추고, 그 결과가 클리핑하지 않을 만큼만 올린다.
    peak = max(float(np.max(np.abs(left))), float(np.max(np.abs(right))), 1e-9)
    left, right = left / peak, right / peak
    left, right = saturate(left * 0.9, 1.25), saturate(right * 0.9, 1.25)

    rms = float(np.sqrt(np.mean(np.concatenate([left, right]) ** 2)))
    gain = TARGET_RMS / max(rms, 1e-9)
    peak = max(float(np.max(np.abs(left))), float(np.max(np.abs(right))), 1e-9)
    gain = min(gain, 0.97 / peak)          # 클리핑 방지가 라우드니스보다 우선
    return left * gain * MASTER, right * gain * MASTER


# ── 출력 ─────────────────────────────────────────────────────────────────────

def write_wav(path: str, left: np.ndarray, right: np.ndarray) -> None:
    inter = np.empty(len(left) * 2, dtype=np.float64)
    inter[0::2], inter[1::2] = left, right
    pcm = np.clip(inter, -1.0, 1.0)
    data = (pcm * 32767.0).astype("<i2").tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--theme", choices=sorted(THEMES))
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--out")
    ap.add_argument("--outdir")
    args = ap.parse_args()

    if args.all:
        outdir = args.outdir or "."
        os.makedirs(outdir, exist_ok=True)
        for name in sorted(THEMES):
            l, r = arrange(name, args.seed)
            p = os.path.join(outdir, f"bgm_{name}.wav")
            write_wav(p, l, r)
            print(f"{p}  {len(l)/SR:.1f}s  {os.path.getsize(p)/1e6:.1f}MB")
    else:
        if not args.theme or not args.out:
            ap.error("--theme 과 --out 을 함께 주거나 --all 을 써라")
        l, r = arrange(args.theme, args.seed)
        write_wav(args.out, l, r)
        print(f"{args.out}  {len(l)/SR:.1f}s  {os.path.getsize(args.out)/1e6:.1f}MB")


if __name__ == "__main__":
    main()
