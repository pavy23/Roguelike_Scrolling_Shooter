#!/usr/bin/env python3
"""SfxGen — sfxr 스타일 절차 SFX 생성기 (ROADMAP M1, 무료·결정론).

외부 서비스 없이 numpy만으로 16비트 슈팅 게임 효과음을 합성한다.
타입별 프리셋 + 시드로 후보를 대량 생산하고, 채택안의 (타입, 시드)를 커밋해 재현한다.

사용:
  python sfxgen.py pilot --out out            # 전 타입 × 시드 3개 = 후보 15개
  python sfxgen.py one --type laser --seed 3 --out out/sfx_laser_3.wav
타입: laser, hit, explosion, pickup, powerup
"""
import argparse
import math
import wave
from pathlib import Path

import numpy as np

SAMPLE_RATE = 44100


def _envelope(n, attack, sustain, decay, punch=0.0):
    """attack/sustain/decay 샘플 수 기반 진폭 엔벨로프. punch는 서스테인 초입 강조."""
    env = np.zeros(n)
    a, s, d = int(attack), int(sustain), int(decay)
    a = max(a, 1); s = max(s, 1); d = max(d, 1)
    total = a + s + d
    if total > n:
        scale = n / total
        a, s, d = max(1, int(a * scale)), max(1, int(s * scale)), max(1, int(d * scale))
    env[:a] = np.linspace(0.0, 1.0, a)
    sustain_curve = 1.0 + punch * np.linspace(1.0, 0.0, s)
    env[a:a + s] = sustain_curve
    env[a + s:a + s + d] = np.linspace(1.0, 0.0, d)
    return env[:n]


def _osc(freqs, duty, kind, rng):
    """주파수 배열(샘플당)을 따라가는 오실레이터. kind: square|saw|sine|noise"""
    phase = np.cumsum(freqs / SAMPLE_RATE) % 1.0
    if kind == "square":
        return np.where(phase < duty, 1.0, -1.0)
    if kind == "saw":
        return 2.0 * phase - 1.0
    if kind == "sine":
        return np.sin(2 * np.pi * phase)
    if kind == "noise":
        # 주파수에 맞춰 홀드되는 화이트 노이즈 (sfxr 방식)
        n = len(freqs)
        noise = rng.uniform(-1, 1, n)
        hold = np.maximum(1, (SAMPLE_RATE / np.maximum(freqs, 1) / 8).astype(int))
        idx = np.zeros(n, dtype=int)
        pos = 0
        i = 0
        while i < n:
            idx[i:i + hold[i]] = pos
            i += hold[i]
            pos += 1
        return noise[np.minimum(idx, n - 1)]
    raise ValueError(kind)


def _lowpass(signal, cutoff):
    if cutoff >= SAMPLE_RATE / 2:
        return signal
    rc = 1.0 / (2 * math.pi * cutoff)
    alpha = (1.0 / SAMPLE_RATE) / (rc + 1.0 / SAMPLE_RATE)
    out = np.empty_like(signal)
    acc = 0.0
    for i, x in enumerate(signal):
        acc += alpha * (x - acc)
        out[i] = acc
    return out


def synth(kind: str, seed: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    sr = SAMPLE_RATE

    if kind == "laser":
        dur = 0.18 + rng.uniform(0, 0.08)
        n = int(dur * sr)
        f0 = rng.uniform(880, 1500)
        slide = rng.uniform(-2400, -1200)          # 급강하
        freqs = np.maximum(80, f0 + slide * np.linspace(0, dur, n))
        sig = _osc(freqs, rng.uniform(0.2, 0.4), "square", rng)
        env = _envelope(n, 0.002 * sr, 0.05 * sr, (dur - 0.06) * sr, punch=0.4)

    elif kind == "hit":
        dur = 0.15 + rng.uniform(0, 0.05)
        n = int(dur * sr)
        f0 = rng.uniform(300, 500)
        freqs = np.maximum(60, f0 * np.exp(-np.linspace(0, 6, n)))
        tone = _osc(freqs, 0.5, "saw", rng)
        noise = _osc(np.full(n, 2500.0), 0.5, "noise", rng)
        sig = 0.5 * tone + 0.6 * noise
        env = _envelope(n, 0.001 * sr, 0.02 * sr, (dur - 0.03) * sr, punch=0.6)

    elif kind == "explosion":
        dur = 0.6 + rng.uniform(0, 0.25)
        n = int(dur * sr)
        base = rng.uniform(700, 1100)
        freqs = base * np.exp(-np.linspace(0, 2.2, n)) + 60
        sig = _osc(freqs, 0.5, "noise", rng)
        sig = _lowpass(sig, rng.uniform(1800, 3200))
        rumble = _osc(np.maximum(40, freqs * 0.12), 0.5, "sine", rng)
        sig = 0.8 * sig + 0.45 * rumble
        env = _envelope(n, 0.001 * sr, 0.12 * sr, (dur - 0.14) * sr, punch=0.8)

    elif kind == "pickup":
        dur = 0.22
        n = int(dur * sr)
        f0 = rng.uniform(700, 900)
        # 2음 상승 아르페지오 (그라디우스 캡슐 오마주)
        freqs = np.where(np.arange(n) < n // 2, f0, f0 * rng.choice([1.335, 1.5]))
        sig = _osc(freqs, 0.5, "square", rng)
        env = _envelope(n, 0.002 * sr, 0.10 * sr, 0.10 * sr, punch=0.2)

    elif kind == "powerup":
        dur = 0.5
        n = int(dur * sr)
        f0 = rng.uniform(400, 520)
        steps = np.floor(np.linspace(0, 5.999, n))          # 6단 상승
        freqs = f0 * (2 ** (steps * rng.uniform(2.5, 3.5) / 12))
        vib = 1.0 + 0.006 * np.sin(2 * np.pi * 40 * np.linspace(0, dur, n))
        sig = _osc(freqs * vib, 0.35, "square", rng)
        env = _envelope(n, 0.003 * sr, 0.32 * sr, 0.15 * sr, punch=0.15)

    else:
        raise SystemExit(f"모르는 타입: {kind}")

    out = sig * env
    peak = np.max(np.abs(out))
    if peak > 0:
        out = out / peak * 0.85
    return out


def write_wav(path: Path, signal: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    data = (signal * 32767).astype(np.int16)
    with wave.open(str(path), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SAMPLE_RATE)
        f.writeframes(data.tobytes())
    print(f"saved: {path} ({len(signal) / SAMPLE_RATE:.2f}s)")


TYPES = ["laser", "hit", "explosion", "pickup", "powerup"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("one", help="타입+시드로 1개 생성")
    p.add_argument("--type", required=True, choices=TYPES)
    p.add_argument("--seed", type=int, required=True)
    p.add_argument("--out", required=True)

    p = sub.add_parser("pilot", help="전 타입 × 시드 0..2 후보 생성")
    p.add_argument("--out", required=True)
    p.add_argument("--seeds", type=int, default=3)

    args = parser.parse_args()
    if args.cmd == "one":
        write_wav(Path(args.out), synth(args.type, args.seed))
    else:
        for kind in TYPES:
            for seed in range(args.seeds):
                write_wav(Path(args.out) / f"sfx_{kind}_{seed}.wav", synth(kind, seed))


if __name__ == "__main__":
    main()
