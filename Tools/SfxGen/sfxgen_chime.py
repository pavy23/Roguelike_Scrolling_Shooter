"""파워업·획득 차임 후보 생성기 (사람 요청 2026-08-02).

사람 피드백: "파워업과 봄 아이템 먹고 적용했을 때 사운드가 너무 거슬려.
좀 더 얌전하지만 고급진 파워업 사운드로".

기존 sfxgen_snes.sfx_pickup/sfx_powerup는 FM 벨(모듈레이션 지수 2.4)과
상행 아르페지오 4음 + 6kHz 하이패스 반짝임이라 어택이 금속적이고 고역이 날카롭다.
오토파이어로 캡슐을 연달아 먹으면 그 어택이 계속 찔러 "거슬린다"가 된다.

여기서는 같은 SNES 톤 팔레트를 쓰되 **어택을 부드럽게, 고역을 눌러, 2음으로 짧게**
간다. 후보 4개를 만들어 파형 특성(길이·피크·RMS·스펙트럼 중심/롤오프)을 찍고,
채택안만 Assets/Audio/Sfx에 덮어쓴다 (파일명이 같아 Unity 배선 변경이 없다).

사용:
  python sfxgen_chime.py --outdir out/chime            # 후보 4종 + 분석표
  python sfxgen_chime.py --adopt c --outdir out/chime  # 채택안을 Assets에 반영
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


def soft_env(n: int, attack: float, decay: float) -> np.ndarray:
    """부드러운 어택 + 지수 감쇠. 하드 스퀘어의 클릭 어택을 없앤다."""
    a = max(1, int(attack * SR))
    a = min(a, n)
    t = np.arange(n) / SR
    env = np.exp(-t * decay)
    # 어택은 코사인 반주기 — 선형보다 시작이 더 완만해 '틱' 소리가 안 난다.
    env[:a] *= 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, a))
    return env


def glass(f: float, dur: float, partials=(1.0, 2.0, 3.0), gains=(1.0, 0.28, 0.12),
          attack: float = 0.010, decay: float = 9.0) -> np.ndarray:
    """가산합성 유리 벨: 사인 배음만 쌓는다 — FM처럼 불협 사이드밴드가 없다."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for p, g in zip(partials, gains):
        x += g * np.sin(2.0 * np.pi * f * p * t) * np.exp(-t * decay * p ** 0.6)
    return x * soft_env(n, attack, decay * 0.35)


def warm(f: float, dur: float, attack: float = 0.022, decay: float = 7.0) -> np.ndarray:
    """트라이앵글 + 사인 서브. 배음이 홀수뿐이라 스퀘어보다 훨씬 얌전하다."""
    n = int(dur * SR)
    ph = B.phase(f, n)
    x = 0.75 * B.tri(ph) + 0.35 * np.sin(ph) + 0.12 * np.sin(ph * 0.5)
    return B.lp(x, 3200) * soft_env(n, attack, decay)


def fm_soft(f: float, dur: float, index: float = 0.9, ratio: float = 2.0,
            attack: float = 0.006, decay: float = 12.0) -> np.ndarray:
    """저지수 FM 마림바. index를 1 아래로 두면 금속감 대신 나무 느낌이 남는다."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    mod = np.sin(2.0 * np.pi * f * ratio * t) * np.exp(-t * 22.0) * index
    x = np.sin(2.0 * np.pi * f * t + mod)
    return x * soft_env(n, attack, decay)


def air(n: int, level: float = 0.05) -> np.ndarray:
    """아주 옅은 공기 층. 8kHz 위만 남겨 '반짝임'이 아니라 '질감'으로 깔린다."""
    rng = np.random.default_rng(20260802)
    t = np.arange(n) / SR
    return B.hp(rng.uniform(-1, 1, n), 8000) * np.exp(-t * 6.0) * level


def polish(x: np.ndarray, peak: float, cutoff: float = 7000.0,
           delay_ms: float = 65.0, feedback: float = 0.18) -> np.ndarray:
    """공통 마감: 고역 정리 → 짧은 에코 꼬리 → 낮은 피크로 정규화.

    피크를 0.9가 아니라 0.55~0.7로 잡는 것이 '얌전함'의 절반이다 — 재생 볼륨을
    낮추는 것과 달리 클립 자체의 다이내믹이 남는다.
    """
    x = B.lp(x, cutoff)
    x = B.echo(x, delay_ms, feedback, taps=3, damp_hz=3500)
    m = float(np.max(np.abs(x)))
    return x * (peak / m) if m > 1e-9 else x


# ── 후보 ─────────────────────────────────────────────────────────────────────
# 공통 골격: 짧은 2음 상행. 음정만 후보별로 다르다.
#   a) 유리 벨 · 완전5도 (0 → 7)
#   b) 따뜻한 트라이앵글 · 장6도 (0 → 9)
#   c) 유리 벨 + 공기층 · 옥타브 (0 → 12), 감쇠를 길게
#   d) 저지수 FM 마림바 · 완전4도 (0 → 5), 가장 짧고 건조

def pickup_a() -> np.ndarray:
    x = blank(0.34)
    put(x, glass(B.note_hz(12), 0.16), 0.000, 0.85)   # A5
    put(x, glass(B.note_hz(19), 0.26), 0.075, 1.00)   # E6
    return polish(x, 0.60)


def pickup_b() -> np.ndarray:
    x = blank(0.34)
    put(x, warm(B.note_hz(12), 0.17), 0.000, 0.90)
    put(x, warm(B.note_hz(21), 0.24), 0.080, 1.00)
    return polish(x, 0.58, cutoff=5200)


def pickup_c() -> np.ndarray:
    x = blank(0.42)
    put(x, glass(B.note_hz(12), 0.22, decay=6.0), 0.000, 0.80)
    put(x, glass(B.note_hz(24), 0.32, decay=5.0), 0.085, 1.00)
    x += air(len(x), 0.035)
    return polish(x, 0.62, delay_ms=95.0, feedback=0.24)


def pickup_d() -> np.ndarray:
    x = blank(0.26)
    put(x, fm_soft(B.note_hz(12), 0.13), 0.000, 0.90)
    put(x, fm_soft(B.note_hz(17), 0.19), 0.065, 1.00)
    return polish(x, 0.56, cutoff=6000, delay_ms=48.0, feedback=0.12)


# 파워업/봄: 같은 음색이되 한 음 더 얹고 아래에 5도를 옅게 깔아 '승격'을 만든다.
# 여전히 2음 상행이 주선율 — 4음 아르페지오는 유치해진다.

def powerup_a() -> np.ndarray:
    x = blank(0.52)
    put(x, glass(B.note_hz(0), 0.34, decay=5.5), 0.000, 0.35)    # 받침 A4
    put(x, glass(B.note_hz(12), 0.20), 0.000, 0.80)
    put(x, glass(B.note_hz(19), 0.36, decay=6.5), 0.090, 1.00)
    return polish(x, 0.62, delay_ms=80.0, feedback=0.22)


def powerup_b() -> np.ndarray:
    x = blank(0.52)
    put(x, warm(B.note_hz(0), 0.36, decay=4.5), 0.000, 0.32)
    put(x, warm(B.note_hz(12), 0.20), 0.000, 0.85)
    put(x, warm(B.note_hz(21), 0.34, decay=5.0), 0.095, 1.00)
    return polish(x, 0.60, cutoff=5200, delay_ms=80.0, feedback=0.20)


def powerup_c() -> np.ndarray:
    x = blank(0.62)
    put(x, glass(B.note_hz(0), 0.44, decay=4.0), 0.000, 0.34)
    put(x, glass(B.note_hz(7), 0.40, decay=4.5), 0.030, 0.30)
    put(x, glass(B.note_hz(12), 0.26, decay=6.0), 0.000, 0.78)
    put(x, glass(B.note_hz(24), 0.44, decay=4.5), 0.100, 1.00)
    x += air(len(x), 0.04)
    return polish(x, 0.64, delay_ms=110.0, feedback=0.26)


def powerup_d() -> np.ndarray:
    x = blank(0.40)
    put(x, fm_soft(B.note_hz(0), 0.26, decay=8.0), 0.000, 0.32)
    put(x, fm_soft(B.note_hz(12), 0.16), 0.000, 0.88)
    put(x, fm_soft(B.note_hz(17), 0.26, decay=9.0), 0.075, 1.00)
    return polish(x, 0.58, cutoff=6000, delay_ms=55.0, feedback=0.14)


CANDIDATES = {
    "a": ("유리벨 완전5도", pickup_a, powerup_a),
    "b": ("트라이앵글 장6도", pickup_b, powerup_b),
    "c": ("유리벨+공기 옥타브", pickup_c, powerup_c),
    "d": ("저지수FM 완전4도", pickup_d, powerup_d),
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
    # 어택 시간: 피크의 90%에 도달하기까지
    env = np.abs(x)
    peak_i = int(np.argmax(env))
    thresh = 0.9 * env[peak_i]
    attack_i = int(np.argmax(env >= thresh))
    return {
        "dur": n / SR,
        "peak": float(np.max(np.abs(x))),
        "rms": float(np.sqrt(np.mean(x ** 2))),
        "centroid": centroid,
        "rolloff85": roll,
        "attack_ms": attack_i / SR * 1000.0,
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
    ap.add_argument("--outdir", default="out/chime")
    ap.add_argument("--adopt", choices=sorted(CANDIDATES),
                    help="이 후보를 Assets/Audio/Sfx의 sfx_pickup/sfx_powerup로 덮어쓴다")
    args = ap.parse_args()

    rows = []
    for key, (label, pick, power) in sorted(CANDIDATES.items()):
        for kind, fn in (("pickup", pick), ("powerup", power)):
            x = fn()
            path = os.path.join(args.outdir, f"sfx_{kind}_{key}.wav")
            write_wav(path, x)
            m = analyze(x)
            rows.append((key, label, kind, m, path))

    header = f"{'cand':<5}{'kind':<9}{'dur(s)':>8}{'peak':>7}{'rms':>7}" \
             f"{'centroid':>10}{'roll85':>9}{'atk(ms)':>9}  설명"
    print(header)
    print("-" * len(header))
    for key, label, kind, m, _ in rows:
        print(f"{key:<5}{kind:<9}{m['dur']:>8.2f}{m['peak']:>7.2f}{m['rms']:>7.3f}"
              f"{m['centroid']:>10.0f}{m['rolloff85']:>9.0f}{m['attack_ms']:>9.1f}  {label}")

    if args.adopt:
        label, pick, power = CANDIDATES[args.adopt]
        for kind, fn in (("pickup", pick), ("powerup", power)):
            dest = os.path.join(ASSET_DIR, f"sfx_{kind}.wav")
            write_wav(dest, fn())
            print(f"채택({args.adopt}, {label}) → {os.path.abspath(dest)}")


if __name__ == "__main__":
    main()
