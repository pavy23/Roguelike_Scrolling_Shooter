"""SNES급 효과음 생성기 (BGM에 이어 SFX도 — 사람 요청 2026-07-31).

기존 sfxgen.py는 단일 발진기 + 지수 감쇠라 음색이 NES에 머물렀다. BGM과 같은
원리로 올린다: **레이어링**(몸통+공기+반짝임), 정확한 FFT 필터, 새추레이션,
피치 엔벨로프, 폭발에는 짧은 에코 꼬리. 악기·필터는 bgmgen_snes를 재사용한다.

같은 파일명으로 덮어쓰므로 Unity 배선 변경이 없다. 모노 32kHz — 효과음은
위치가 게임플레이 정보가 아니라서 스테레오 폭보다 펀치가 우선이다.

사용법: python sfxgen_snes.py --outdir ../../Assets/Audio/Sfx
"""

from __future__ import annotations

import argparse
import os
import wave as wavemod

import numpy as np

import bgmgen_snes as B
import sfxgen_chime as C

SR = B.SR


def t_axis(dur: float) -> np.ndarray:
    return np.arange(int(dur * SR)) / SR


def pitch_sweep(f0: float, f1: float, dur: float, curve: float = 1.0) -> np.ndarray:
    """f0→f1 피치 스윕의 위상. curve>1 이면 초반에 빠르게 떨어진다."""
    n = int(dur * SR)
    shape = np.linspace(0.0, 1.0, n) ** curve
    freq = f0 + (f1 - f0) * shape
    return np.cumsum(2.0 * np.pi * freq / SR), freq


def env_exp(dur: float, rate: float) -> np.ndarray:
    return np.exp(-t_axis(dur) * rate)


def norm(x: np.ndarray, peak: float = 0.9) -> np.ndarray:
    m = float(np.max(np.abs(x))) if len(x) else 0.0
    return x * (peak / m) if m > 1e-9 else x


def rng_for(name: str) -> np.random.Generator:
    return np.random.default_rng(abs(hash(name)) % (2**31))


# ── 개별 효과음 ──────────────────────────────────────────────────────────────

def sfx_hit() -> np.ndarray:
    """피격/타격: 금속 몸통 + 노이즈 스냅 + 살짝 내려앉는 무게."""
    rng = rng_for("hit")
    dur = 0.16
    ph, _ = pitch_sweep(320, 180, dur, curve=1.6)
    body = np.sin(ph) * 0.7 + B.sq(ph * 0.5, 0.35) * 0.25
    snap = B.hp(rng.uniform(-1, 1, int(dur * SR)), 3200) * env_exp(dur, 55)
    x = body * env_exp(dur, 26) + snap * 0.8
    return norm(B.saturate(x, 2.2))


def sfx_explosion() -> np.ndarray:
    """격파 폭발: 깊은 붐(피치 드롭) + 파편 크래클 + 에코 꼬리."""
    rng = rng_for("explosion")
    dur = 0.85
    ph, _ = pitch_sweep(140, 28, dur, curve=2.2)
    boom = np.sin(ph) * env_exp(dur, 6.5)
    rumble = B.lp(rng.uniform(-1, 1, int(dur * SR)), 240) * env_exp(dur, 5.0) * 1.6
    crackle = B.hp(rng.uniform(-1, 1, int(dur * SR)), 1800) \
        * env_exp(dur, 14) * (rng.uniform(0, 1, int(dur * SR)) > 0.72)
    x = boom * 1.0 + rumble * 0.8 + crackle * 0.5
    x = B.saturate(x, 2.6)
    x = B.echo(x, 90, 0.3, taps=3, damp_hz=1400)
    return norm(x)


def sfx_pickup() -> np.ndarray:
    """캡슐 획득: 얌전한 2음 상승 차임 (유리 벨, 완전5도).

    첫 버전은 FM 벨(모듈레이션 지수 2.4) 2음이라 어택이 금속적이었고, 파워업은
    4음 아르페지오 + 6kHz 반짝임이라 더 날카로웠다. 사람 피드백으로 교체했다 —
    "파워업과 봄 아이템 먹고 적용했을 때 사운드가 너무 거슬려. 좀 더 얌전하지만
    고급진 파워업 사운드로" (2026-08-02). 후보 비교와 파라미터는 sfxgen_chime.py.
    """
    return C.pickup_a()


def sfx_powerup() -> np.ndarray:
    """레벨업/봄 획득: 같은 유리 벨 2음에 5도 받침만 얹는다 — 승격이되 조용하게."""
    return C.powerup_a()


def sfx_warning() -> np.ndarray:
    """경고: 디튠 스퀘어 2음 교대 — 급박하지만 귀를 찢지 않게."""
    dur = 0.22
    n = int(dur * SR)
    t = t_axis(dur)
    f = np.where((t * 9) % 2 < 1, 620.0, 780.0)
    ph = np.cumsum(2.0 * np.pi * f / SR)
    x = (B.sq(ph, 0.5) + B.sq(ph * 1.01, 0.5)) * 0.5
    x = B.lp(x, 2600) * B.env_adsr(n, 0.01, 0.03, 0.8, 0.06)
    return norm(x, 0.8)


def _zap(f0: float, f1: float, dur: float, sparkle_hz: float, name: str) -> np.ndarray:
    """레이저 계열 공통: 빠른 피치 스윕 + 고역 스파클."""
    ph, freq = pitch_sweep(f0, f1, dur, curve=1.3)
    core = B.saw(ph) * 0.6 + np.sin(ph * 2.0) * 0.4
    core = B.lp(core, 5200) * env_exp(dur, 22)
    sparkle = B.hp(rng_for(name).uniform(-1, 1, int(dur * SR)), sparkle_hz) \
        * env_exp(dur, 40) * 0.3
    return norm(B.saturate(core + sparkle, 1.8), 0.8)


def sfx_laser() -> np.ndarray:
    return _zap(1400, 480, 0.16, 6500, "laser")


def sfx_laser_beam() -> np.ndarray:
    return _zap(900, 340, 0.22, 5200, "beam")


def sfx_laser_spread() -> np.ndarray:
    return _zap(1700, 620, 0.15, 7000, "spread")


def jingle_clear() -> np.ndarray:
    """스테이지 클리어: 웅장한 승리 (~5.5초).

    첫 버전(도도도-미솔도 상행 아르페지오)은 유치하다는 피드백(2026-07-31).
    빠른 상행 대신 **느리고 낮게** 간다 — 영웅적 종지(I → bVI → bVII → I)를
    저음 브라스와 스트링 스웰로 쌓고, 화음마다 팀파니를 박아 무게를 싣는다.
    선율은 길게 끄는 5도→장6도→옥타브 세 음뿐 — 웅장함은 음 수가 아니라
    화성의 폭과 지속에서 나온다.
    """
    n = int(5.5 * SR)
    x = np.zeros(n)

    def put(sig, at, gain):
        i = int(at * SR)
        end = min(n, i + len(sig))
        if end > i:
            x[i:end] += sig[:end - i] * gain

    def timpani(f, at, gain):
        d = 0.5
        ph, _ = pitch_sweep(f * 1.4, f, d, curve=2.0)
        hit = np.sin(ph) * env_exp(d, 7.0)
        hit += B.lp(rng_for(f"timp{at}").uniform(-1, 1, int(d * SR)), 300) \
            * env_exp(d, 18) * 0.5
        put(B.saturate(hit, 2.0), at, gain)

    # 영웅적 종지: I(0) → bVI(-4) → bVII(-2) → I(0). 화음마다 3옥타브로 쌓는다.
    chords = [(0, 0.0, 1.1), (-4, 1.0, 1.1), (-2, 2.0, 1.1), (0, 3.0, 2.4)]
    for root, at, dur in chords:
        for octave, gain in ((-24, 0.5), (-12, 0.42), (0, 0.3)):
            put(B.inst_brass(B.note_hz(root + octave), dur, 0.9), at, gain)
        # 3도·5도는 스트링이 넓게 깔아 화음의 폭을 만든다
        put(B.inst_strings(B.note_hz(root + 4), dur * 1.15, 0.7), at, 0.3)
        put(B.inst_strings(B.note_hz(root + 7), dur * 1.15, 0.7), at, 0.28)
        timpani(B.note_hz(root - 36), at, 0.55)

    # 선율: 길게 끄는 세 음 (5도 → 장6도 → 옥타브) — 마지막 음이 최종 화음과 함께 선다
    put(B.inst_brass(B.note_hz(7), 1.0, 1.0), 1.05, 0.4)
    put(B.inst_brass(B.note_hz(9), 1.0, 1.0), 2.05, 0.42)
    put(B.inst_brass(B.note_hz(12), 2.3, 1.0), 3.05, 0.5)

    # 최종 화음 위 심벌 스웰 (노이즈 상승) — 정점을 알린다
    swell_n = int(1.2 * SR)
    swell = B.hp(rng_for("cym").uniform(-1, 1, swell_n), 5000) \
        * np.linspace(0, 1, swell_n) ** 2 * 0.5
    put(swell, 1.9, 0.5)
    swell_tail = B.hp(rng_for("cym2").uniform(-1, 1, int(1.8 * SR)), 4500) \
        * env_exp(1.8, 3.2)
    put(swell_tail, 3.0, 0.4)

    x = B.echo(x, 230, 0.4, taps=5, damp_hz=2200)
    return norm(x, 0.88)


def jingle_gameover() -> np.ndarray:
    """게임 오버: 하행 단조 프레이즈 + 낮은 여운 (~6초). 벌하지 않고 배웅한다."""
    n = int(6.0 * SR)
    x = np.zeros(n)

    def put(sig, at, gain):
        i = int(at * SR)
        end = min(n, i + len(sig))
        if end > i:
            x[i:end] += sig[:end - i] * gain

    seq = [(0, 0.0), (-2, 0.7), (-5, 1.4), (-9, 2.2)]
    for semi, at in seq:
        put(B.inst_strings(B.note_hz(semi), 1.1, 0.8), at, 0.45)
    # 마지막 저음 화음이 길게 가라앉는다
    put(B.inst_pad(B.note_hz(-21), 3.0, 0.9), 2.6, 0.5)
    put(B.inst_pad(B.note_hz(-14), 3.0, 0.7), 2.6, 0.35)
    put(B.inst_bell(B.note_hz(-9), 1.6, 0.5), 3.1, 0.3)
    x = B.echo(x, 260, 0.4, taps=4)
    return norm(x, 0.8)


GENERATORS = {
    "sfx_hit": sfx_hit,
    "sfx_explosion": sfx_explosion,
    "sfx_pickup": sfx_pickup,
    "sfx_powerup": sfx_powerup,
    "sfx_warning": sfx_warning,
    "sfx_laser": sfx_laser,
    "sfx_laser_beam": sfx_laser_beam,
    "sfx_laser_spread": sfx_laser_spread,
    "jingle_clear": jingle_clear,
    "jingle_gameover": jingle_gameover,
}


def write_wav(path: str, x: np.ndarray) -> None:
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with wavemod.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--outdir", required=True)
    args = ap.parse_args()
    os.makedirs(args.outdir, exist_ok=True)
    for name, fn in GENERATORS.items():
        p = os.path.join(args.outdir, f"{name}.wav")
        write_wav(p, fn())
        print(f"{p}  {os.path.getsize(p)/1024:.0f}KB")


if __name__ == "__main__":
    main()
