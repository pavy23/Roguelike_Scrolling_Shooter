"""적·지형 레이저 예고/발사음 생성기 (사람 요청 2026-08-02).

사람 피드백: "레이저 발사하는 소리도 따로 있어야 할듯".

지금까지 SfxPlayer에는 LaserTelegraphStarted/LaserFired 케이스가 아예 없어서
**화면에서 가장 위험한 공격이 소리로는 존재하지 않았다.** 탄막 속에서 예고선을
놓치면 아무 경고 없이 맞는다.

두 음을 만든다:

  charge — 예고(Telegraph) 시작. 낮게 상승하는 험/차지. 0.45~0.6초로 예고 길이
           (30~42틱 = 0.5~0.7초)보다 조금 짧게 끝나 "다 찼다 → 쏜다"가 된다.
           볼륨은 절제한다(재생 0.35): 예고는 경고지 위협 그 자체가 아니다.
  fire   — 발사(Firing 진입). 짧은 잽 0.15~0.25초. 차지의 상승을 받아
           **아래로** 떨어뜨려 방출로 읽히게 한다.

방향은 기존 채택음과 같다 — "얌전하지만 고급진"(2026-08-02). 하드 스퀘어
지르기 금지: 배음은 트라이앵글·사인·필터 노이즈로만 쌓고, 피크는 0.55~0.7로
낮게 잡아 다이내믹을 남긴다.

사용:
  python sfxgen_laser.py --outdir out/laser            # 후보 3종 + 분석표
  python sfxgen_laser.py --adopt b --outdir out/laser  # 채택안을 Assets에 반영
"""

from __future__ import annotations

import argparse
import os
import wave as wavemod

import numpy as np

import bgmgen_snes as B

SR = B.SR
ASSET_DIR = os.path.join("..", "..", "Assets", "Audio", "Sfx")


# ── 공통 유틸 ────────────────────────────────────────────────────────────────

def blank(dur: float) -> np.ndarray:
    return np.zeros(int(dur * SR))


def put(buf: np.ndarray, sig: np.ndarray, at: float, gain: float = 1.0) -> None:
    i = int(at * SR)
    end = min(len(buf), i + len(sig))
    if end > i:
        buf[i:end] += sig[:end - i] * gain


def sweep(f0: float, f1: float, dur: float, curve: float = 1.0) -> np.ndarray:
    """f0 → f1 지수 스윕의 위상. curve>1이면 뒤에서 몰아친다."""
    n = int(dur * SR)
    t = np.linspace(0.0, 1.0, n) ** curve
    freq = f0 * (f1 / f0) ** t
    return B.phase(freq, n)


def fade(n: int, attack: float, release: float) -> np.ndarray:
    """코사인 페이드 인/아웃 — 시작과 끝의 '틱'을 없앤다."""
    env = np.ones(n)
    a = min(n, max(1, int(attack * SR)))
    r = min(n - a, max(1, int(release * SR)))
    env[:a] = 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, a))
    if r > 0:
        env[n - r:] = 0.5 + 0.5 * np.cos(np.linspace(0.0, np.pi, r))
    return env


def air(dur: float, f0: float, f1: float, seed: int = 20260802) -> np.ndarray:
    """상승하는 필터 노이즈 — 에너지가 모이는 '흡기' 층. 고역은 잘라 둔다."""
    n = int(dur * SR)
    rng = np.random.default_rng(seed)
    noise = rng.uniform(-1.0, 1.0, n)
    # 시간에 따라 대역이 올라가야 하므로 블록 단위로 필터를 갈아 끼운다.
    out = np.zeros(n)
    blocks = 24
    edges = np.linspace(0, n, blocks + 1).astype(int)
    for i in range(blocks):
        s, e = edges[i], edges[i + 1]
        if e <= s:
            continue
        cut = f0 * (f1 / f0) ** (i / max(1, blocks - 1))
        seg = B.lp(B.hp(noise[s:e], cut * 0.6), cut, order=2.0)
        out[s:e] = seg
    return out


def polish(x: np.ndarray, peak: float, cutoff: float = 6500.0,
           delay_ms: float = 55.0, feedback: float = 0.14) -> np.ndarray:
    """공통 마감: 고역 정리 → 짧은 꼬리 → 낮은 피크로 정규화."""
    x = B.lp(x, cutoff)
    x = B.echo(x, delay_ms, feedback, taps=3, damp_hz=3200)
    m = float(np.max(np.abs(x)))
    return x * (peak / m) if m > 1e-9 else x


# ── 후보 ─────────────────────────────────────────────────────────────────────
# 세 후보 모두 charge는 상승, fire는 하강이다. 두 음이 한 동작으로 이어져야
# "충전했다가 쏜다"가 들린다 — 서로 관계없는 소리 두 개면 예고 기능을 못 한다.
#
#   a) 험 위주        — 삼각파 2단 옥타브 + 옅은 노이즈. 가장 얌전하다.
#   b) 험 + 흡기      — a에 상승 노이즈를 실어 "빨려 들어간다"를 더한다.
#   c) 배음 코러스    — 완전5도를 겹쳐 넓게. 가장 화려하지만 탄막에서 뜬다.

def charge_a() -> np.ndarray:
    dur = 0.50
    n = int(dur * SR)
    ph = sweep(96.0, 300.0, dur, curve=1.35)
    x = 0.70 * B.tri(ph) + 0.30 * np.sin(ph) + 0.22 * np.sin(ph * 0.5)
    x *= fade(n, 0.055, 0.06)
    # 뒤로 갈수록 밝아진다 — 컷오프도 같이 올려 "충전이 찬다"를 음색으로 만든다.
    x = 0.65 * B.lp(x, 1400.0) + 0.35 * B.lp(x, 3200.0) * np.linspace(0.0, 1.0, n) ** 2
    x += air(dur, 700.0, 2600.0) * 0.05 * np.linspace(0.0, 1.0, n) ** 2
    return polish(x, 0.55, cutoff=5200.0, delay_ms=70.0, feedback=0.16)


def charge_b() -> np.ndarray:
    dur = 0.55
    n = int(dur * SR)
    ph = sweep(88.0, 330.0, dur, curve=1.5)
    x = 0.62 * B.tri(ph) + 0.34 * np.sin(ph) + 0.26 * np.sin(ph * 0.5)
    x += 0.16 * np.sin(sweep(88.0 * 3.0, 330.0 * 3.0, dur, curve=1.5))   # 옅은 3배음
    x *= fade(n, 0.06, 0.05)
    x = B.lp(x, 2600.0)
    # 흡기층 — 이 후보의 정체성. 노이즈 대역이 험을 따라 올라가면서 밝아진다.
    # 험만으로는 스펙트럼이 300Hz대에 뭉쳐(중심 420Hz) BGM 아래로 묻힌다.
    # 0.35까지 올려 중심을 780Hz로 끌어올렸다 — 그 위는 쉭쉭거리기 시작한다.
    x += air(dur, 900.0, 6500.0) * 0.35 * np.linspace(0.0, 1.0, n) ** 1.6
    return polish(x, 0.56, cutoff=6800.0, delay_ms=75.0, feedback=0.18)


def charge_c() -> np.ndarray:
    dur = 0.58
    n = int(dur * SR)
    x = np.zeros(n)
    for mult, gain in ((1.0, 0.55), (1.5, 0.30), (2.0, 0.18)):   # 근음 + 완전5도 + 옥타브
        ph = sweep(92.0 * mult, 300.0 * mult, dur, curve=1.4)
        x += gain * (0.7 * B.tri(ph) + 0.3 * np.sin(ph))
    x *= fade(n, 0.07, 0.06)
    x = B.lp(x, 3000.0)
    x += air(dur, 800.0, 3000.0) * 0.06 * np.linspace(0.0, 1.0, n) ** 2
    return polish(x, 0.58, cutoff=6000.0, delay_ms=90.0, feedback=0.20)


def fire_a() -> np.ndarray:
    dur = 0.20
    n = int(dur * SR)
    ph = sweep(1300.0, 280.0, dur, curve=1.2)
    x = 0.62 * B.tri(ph) + 0.38 * np.sin(ph) + 0.16 * np.sin(ph * 0.5)
    x += 0.14 * np.sin(sweep(1300.0 * 2.0, 280.0 * 2.0, dur, curve=1.2))
    x *= np.exp(-np.arange(n) / SR * 14.0) * fade(n, 0.003, 0.02)
    # 방출 순간의 공기 — 아주 짧게만. 길면 폭발음이 된다.
    burst = int(0.03 * SR)
    x[:burst] += (air(0.03, 4200.0, 1600.0, seed=7)[:burst]
                  * np.exp(-np.arange(burst) / SR * 60.0) * 0.24)
    return polish(x, 0.62, cutoff=8000.0, delay_ms=42.0, feedback=0.12)


def fire_b() -> np.ndarray:
    dur = 0.22
    n = int(dur * SR)
    # 스윕 곡선이 1을 넘으면 **처음 절반을 높은 음역에 머문 뒤** 떨어진다.
    # curve<1로 곧장 떨어뜨렸더니 스펙트럼 중심이 790Hz까지 내려가 "레이저"가
    # 아니라 둔탁한 쿵으로 들렸다. 1.8이면 중심 1810Hz — powerup(1500)과
    # explosion(2700) 사이에 앉는다.
    ph = sweep(1600.0, 300.0, dur, curve=1.8)
    x = 0.55 * B.tri(ph) + 0.42 * np.sin(ph) + 0.20 * np.sin(ph * 0.5)
    x += 0.18 * np.sin(sweep(1600.0 * 2.0, 300.0 * 2.0, dur, curve=1.8))   # 옥타브 반짝임
    x *= np.exp(-np.arange(n) / SR * 18.0) * fade(n, 0.0025, 0.025)
    # 방출 트랜지언트: 35ms 필터 노이즈. 어택에 '실체'를 준다.
    burst = int(0.035 * SR)
    x[:burst] += (air(0.035, 5000.0, 1400.0, seed=11)[:burst]
                  * np.exp(-np.arange(burst) / SR * 55.0) * 0.34)
    x = B.saturate(x, 1.3)
    return polish(x, 0.64, cutoff=7600.0, delay_ms=48.0, feedback=0.14)


def fire_c() -> np.ndarray:
    dur = 0.25
    n = int(dur * SR)
    x = np.zeros(n)
    for mult, gain in ((1.0, 0.58), (1.5, 0.26), (2.0, 0.16)):
        ph = sweep(1800.0 * mult, 340.0 * mult, dur, curve=2.2)
        x += gain * (0.62 * B.tri(ph) + 0.38 * np.sin(ph))
    x *= np.exp(-np.arange(n) / SR * 20.0) * fade(n, 0.004, 0.03)
    burst = int(0.04 * SR)
    x[:burst] += (air(0.04, 4000.0, 1300.0, seed=23)[:burst]
                  * np.exp(-np.arange(burst) / SR * 45.0) * 0.28)
    return polish(x, 0.66, cutoff=7200.0, delay_ms=60.0, feedback=0.16)


CANDIDATES = {
    "a": ("험 위주 (가장 얌전)", charge_a, fire_a),
    "b": ("험 + 흡기 (충전감)", charge_b, fire_b),
    "c": ("5도 코러스 (넓음)", charge_c, fire_c),
}


# ── 분석 / 출력 ──────────────────────────────────────────────────────────────

def analyze(x: np.ndarray) -> dict:
    n = len(x)
    win = np.abs(np.fft.rfft(x * np.hanning(n)))
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    total = win.sum()
    centroid = float((freqs * win).sum() / total) if total > 0 else 0.0
    cum = np.cumsum(win)
    roll = float(freqs[int(np.searchsorted(cum, 0.85 * total))]) if total > 0 else 0.0
    env = np.abs(x)
    peak_i = int(np.argmax(env))
    return {
        "dur": n / SR,
        "peak": float(np.max(np.abs(x))),
        "rms": float(np.sqrt(np.mean(x ** 2))),
        "centroid": centroid,
        "rolloff85": roll,
        "peak_ms": peak_i / SR * 1000.0,
    }


def write_wav(path: str, x: np.ndarray) -> None:
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with wavemod.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--outdir", default="out/laser")
    ap.add_argument("--adopt", choices=sorted(CANDIDATES),
                    help="이 후보를 Assets/Audio/Sfx의 sfx_laser_charge/sfx_laser_fire로 쓴다")
    args = ap.parse_args()

    rows = []
    for key, (label, charge, fire) in sorted(CANDIDATES.items()):
        for kind, fn in (("charge", charge), ("fire", fire)):
            x = fn()
            write_wav(os.path.join(args.outdir, f"sfx_laser_{kind}_{key}.wav"), x)
            rows.append((key, label, kind, analyze(x)))

    header = (f"{'cand':<5}{'kind':<8}{'dur(s)':>8}{'peak':>7}{'rms':>7}"
              f"{'centroid':>10}{'roll85':>9}{'peak(ms)':>10}  설명")
    print(header)
    print("-" * len(header))
    for key, label, kind, m in rows:
        print(f"{key:<5}{kind:<8}{m['dur']:>8.2f}{m['peak']:>7.2f}{m['rms']:>7.3f}"
              f"{m['centroid']:>10.0f}{m['rolloff85']:>9.0f}{m['peak_ms']:>10.1f}  {label}")

    if args.adopt:
        label, charge, fire = CANDIDATES[args.adopt]
        for kind, fn in (("charge", charge), ("fire", fire)):
            dest = os.path.join(ASSET_DIR, f"sfx_laser_{kind}.wav")
            write_wav(dest, fn())
            print(f"채택({args.adopt}, {label}) → {os.path.abspath(dest)}")


if __name__ == "__main__":
    main()
