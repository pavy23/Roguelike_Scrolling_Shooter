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
    # ── 스테이지 5곡 (arr 키가 있으면 arrange_staged가 맡는다) ────────────────
    # 사람 지시: 1=경쾌, 2=잔잔, 3·4=긴장(단 서로 달라야), 5=웅장.

    "scrapyard": dict(
        # 1면 — 경쾌. 믹솔리디안(장음계에 b7)이라 밝은데 팝하지 않고 모험적이다.
        # I-bVII-IV가 계속 돌아 "출격" 느낌을 만든다. 셔플 햇 + 옥타브 베이스가
        # 통통 튀는 추진력을 준다.
        # 2026-08-03 사람 지시: "1스테이지 BGM이 너무 처져서 훨씬 리드미컬하고 신나는
        # SF풍으로". 140bpm + 셔플 0.28은 통통 튀지만 뒤로 끌린다 — 셔플을 거의 빼고
        # (0.06) 168bpm으로 올려 앞으로 미는 그루브로 바꿨다. 화성(믹솔리디안 I-bVII-IV)은
        # "출격" 느낌이 이미 맞아서 그대로 둔다. 바뀐 것은 리듬과 밀도다.
        bpm=168, bars=16, motif_seed=11,
        scale=[0, 2, 4, 5, 7, 9, 10, 12],                       # A 믹솔리디안
        chords_a=[(-24, "maj"), (-24, "maj"), (-26, "maj"), (-26, "maj"),
                  (-31, "maj"), (-31, "maj"), (-26, "maj"), (-24, "maj")],
        # B절: 상대단조(F#m)로 한 번 그늘을 지웠다가 다시 밝게 나온다
        chords_b=[(-27, "min"), (-27, "min"), (-31, "maj"), (-31, "maj"),
                  (-26, "maj"), (-26, "maj"), (-29, "min"), (-24, "maj")],
        pad="strings", echo=(150, 0.34),
        arr=dict(lead_voice="square25", lead_mode="motif", bass="octave_bounce",
                 arp="dense16", drums="drive", swing=0.06, hat=1.0,
                 lead_gain=0.40),
    ),
    "hive": dict(
        # 2면 — 잔잔. 도리안은 단조인데 6도가 장이라 어둡지 않고 '낯설게 아름답다'.
        # 아르페지오를 아예 빼고 온음표 베이스 + 삼각 리드의 긴 프레이즈만 남긴다.
        # 여백이 이 곡의 내용이다.
        bpm=88, bars=16, motif_seed=23,
        scale=[0, 2, 3, 5, 7, 9, 10, 12],                       # A 도리안
        chords_a=[(-24, "min7"), (-24, "min7"), (-22, "min7"), (-22, "min7"),
                  (-19, "maj"), (-19, "maj"), (-26, "maj7"), (-26, "maj7")],
        # B절: 도리안 특유의 장4도(D)를 앞세워 살짝 떠오른다
        chords_b=[(-28, "maj7"), (-28, "maj7"), (-19, "maj"), (-19, "maj"),
                  (-24, "min7"), (-24, "min7"), (-26, "sus4"), (-26, "maj")],
        pad="pad", echo=(260, 0.46),
        arr=dict(lead_voice="triangle", lead_mode="sustain", bass="whole",
                 arp="droplet", drums="ambient", swing=0.0, hat=0.0,
                 lead_gain=0.30, pad_gain=0.34),
    ),
    "fortress": dict(
        # 3면 — 긴장 A(쫓기는 긴장). 프리지안 도미넌트(하모닉 마이너 5음 시작):
        # 장3도 위에 b2가 얹혀 '기계적 위압'이 난다. bII-I 왕복이 심장박동.
        # 16분 아르페지오가 쉬지 않고 돌고 그 위에 듀티 0.5 리드가 길게 버틴다.
        bpm=150, bars=16, motif_seed=37,
        scale=[0, 1, 4, 5, 7, 8, 10, 12],                       # E 프리지안 도미넌트
        chords_a=[(-29, "maj"), (-29, "maj"), (-28, "maj"), (-28, "maj"),
                  (-29, "maj"), (-24, "min"), (-28, "maj"), (-29, "maj")],
        # B절: 4도(Am)로 올라가 압박을 한 단계 조인 뒤 되돌아온다
        chords_b=[(-24, "min"), (-24, "min"), (-26, "maj"), (-26, "maj"),
                  (-28, "maj"), (-28, "maj"), (-29, "maj"), (-29, "maj")],
        pad="brass", echo=(115, 0.26),
        arr=dict(lead_voice="square50", lead_mode="hold", bass="driving8",
                 arp="dense16", drums="drive", swing=0.0, hat=1.0,
                 lead_gain=0.30, pad_gain=0.22),
    ),
    "nebula": dict(
        # 4면 — 긴장 B(숨죽이는 긴장). 3면과 정반대의 방법으로 조인다: 밀도가 아니라
        # 불안정한 화성으로. 증3화음이 반음씩 내려가면 '어디에도 안 착지'하고,
        # 홀톤 스케일은 이끔음이 없어 해결을 기대할 수 없다. 햇 대신 노이즈 스웰.
        bpm=124, bars=16, motif_seed=53,
        scale=[0, 2, 4, 6, 8, 10, 12],                          # 홀톤
        chords_a=[(-24, "aug"), (-24, "aug"), (-25, "aug"), (-25, "aug"),
                  (-26, "aug"), (-26, "aug"), (-27, "aug"), (-27, "aug")],
        # B절: 같은 하강을 한 옥타브 위 폭으로 — 조여드는 느낌만 남기고 착지는 없다
        chords_b=[(-22, "aug"), (-22, "aug"), (-23, "aug"), (-23, "aug"),
                  (-24, "aug"), (-24, "aug"), (-25, "aug"), (-25, "aug")],
        pad="pad", echo=(300, 0.52),
        arr=dict(lead_voice="square25", lead_mode="tremolo", bass="chromatic_fall",
                 arp="swell", drums="pulse", swing=0.0, hat=0.0,
                 lead_gain=0.26, pad_gain=0.30),
    ),
    "core": dict(
        # 5면 — 웅장. 하모닉 마이너로 비장하게 가되 마지막 마디에서 동명 장조로
        # 끝낸다(픽카르디 종지) — 절망이 아니라 결의로 읽힌다. 빠르게 가지 않는다:
        # 웅장함은 속도가 아니라 무게에서 나오므로 128bpm 하프타임 드럼에
        # 옥타브 유니즌 브라스와 5도 스트링을 겹친다.
        bpm=128, bars=16, motif_seed=71,
        scale=[0, 2, 3, 5, 7, 8, 11, 12],                       # A 하모닉 마이너
        chords_a=[(-24, "min"), (-24, "min"), (-28, "maj"), (-28, "maj"),
                  (-26, "maj"), (-26, "maj"), (-29, "maj"), (-29, "maj")],
        # B절 마지막 두 마디가 픽카르디: A단조 곡이 A장3화음으로 선다
        chords_b=[(-24, "min"), (-28, "maj"), (-26, "maj"), (-29, "maj"),
                  (-24, "min"), (-28, "maj"), (-24, "maj"), (-24, "maj")],
        pad="strings", echo=(145, 0.36),
        arr=dict(lead_voice="octave", lead_mode="anthem", bass="whole_octave",
                 arp="fifth_pad", drums="epic", swing=0.0, hat=1.0,
                 lead_gain=0.32, pad_gain=0.34),
    ),

    # ── 유지 대상 (arr 키 없음 → arrange_classic, 시드 0 결과 불변) ───────────
    "boss": dict(
        # 2026-08-03 사람 지시로 현대 편성(arr)으로 옮겼다. 예전에는 arr 키가 없어
        # arrange_classic을 탔는데, 브라스 리드 + rock 드럼만으로는 "웅장하지만 느린"
        # 쪽이었다. 16분 아르페지오와 드라이브 킥을 넣어 앞으로 미는 전투곡으로 바꾼다.
        # 하모닉 마이너(b6+장7도)는 SF 전투의 긴장 그대로라 유지한다.
        bpm=178, bars=16,
        progression=[-24, -24, -18, -19, -24, -24, -17, -12],   # 5도 도약 전투
        chords_a=[(-24, "min"), (-24, "min"), (-18, "maj"), (-19, "maj"),
                  (-24, "min"), (-24, "min"), (-17, "aug"), (-12, "min")],
        chords_b=[(-22, "min"), (-22, "min"), (-19, "maj"), (-19, "maj"),
                  (-24, "min"), (-24, "min"), (-17, "aug"), (-24, "min")],
        scale=[0, 2, 3, 5, 7, 8, 11, 12],                       # 하모닉 마이너
        lead="brass", pad="strings", echo=(110, 0.26),
        drums="rock", motif_seed=89,
        arr=dict(lead_voice="square25", lead_mode="motif", bass="octave_bounce",
                 arp="dense16", drums="drive", swing=0.0, hat=1.0,
                 lead_gain=0.42),
    ),
    "boss_stage": dict(
        # 스테이지 보스 — 웅장하고 위압적으로, **느리게**.
        #
        # 사람 지시 2026-08-03: "각 스테이지 중간보스랑 최종보스 BGM이 같으니까 흥미가
        # 떨어져. 좀더 느린 템포에 웅장하고 위압적으로." 그래서 전투곡(boss, 178bpm)과
        # 정반대로 간다 — 178을 반으로 접은 92bpm에 하프타임 드럼을 얹으면 같은 박자를
        # 세면서도 발걸음이 무거워진다. 속도로 미는 곡이 아니라 **무게로 누르는** 곡이다.
        #
        # 프리지안 도미넌트(장3도 위의 b2)는 해결을 허락하지 않는 화성이라 위압에 맞고,
        # bII → I 왕복은 다가오는 발소리처럼 읽힌다. 리드는 옥타브 유니즌 브라스,
        # 베이스는 온음표로 깔아 저역을 비우지 않는다.
        # 2026-08-04 수정: "일반 1~5 스테이지 보스 노래 약간 더 빠르게 편곡해줘.
        # 너무 히든 보스랑 비슷해." 92와 히든의 76은 16bpm 차이뿐인 데다 편성까지
        # 거의 같아서(옥타브 리드 + epic 드럼 + 온음표 베이스) 두 곡이 한 곡으로
        # 들렸다. 112로 올려 히든과 36bpm을 벌리고, 하이햇을 열어 앞으로 미는
        # 맥을 준다 — 히든은 하이햇이 없어 그것만으로도 질감이 갈린다.
        # 178bpm 전투곡보다는 여전히 훨씬 느리므로 "웅장하고 위압적으로"는 지킨다.
        # 2026-08-05: 사람이 "보스 BGM은 (히든 말고) 지금보다 좀 더 경쾌하게"라고
        # 했다. 112에서 132로 올리고 리듬을 앞으로 미는 편성으로 바꾼다.
        #
        # 화성(프리지안 도미넌트)은 그대로 둔다 — 위압은 거기서 나오고, 사람이
        # 처음 요구한 "웅장하고 위압적으로"를 버리는 것이 아니라 그 위에 추진력을
        # 얹는 것이다. 히든(76bpm)과는 56bpm 벌어져 더 확실히 갈린다.
        bpm=132, bars=16, motif_seed=131,
        scale=[0, 1, 4, 5, 7, 8, 10, 12],                       # 프리지안 도미넌트
        chords_a=[(-24, "maj"), (-24, "maj"), (-23, "maj"), (-23, "maj"),
                  (-24, "maj"), (-24, "maj"), (-29, "min"), (-29, "min")],
        # B절: 단6도로 한 계단 올라섰다가 제자리로 — 올라가도 벗어나지는 못한다
        chords_b=[(-28, "min"), (-28, "min"), (-23, "maj"), (-23, "maj"),
                  (-26, "maj"), (-26, "maj"), (-24, "maj"), (-24, "maj")],
        # 에코도 170→140으로 조인다. 템포가 빨라지면 같은 지연이 다음 박에 겹쳐
        # 뭉개진다 — 느린 곡에서 공간을 주던 값이 빠른 곡에서는 흙탕물이 된다.
        # 편성도 앞으로 민다: 온음표 베이스 → 옥타브 바운스, 지속형 5도 패드 →
        # 16분 아르페지오, epic → drive 드럼. 리드는 옥타브 유니즌을 유지해
        # 무게를 남기되 anthem(길게 끄는) 대신 motif(움직이는)로 바꾼다.
        pad="brass", echo=(120, 0.26),
        arr=dict(lead_voice="octave", lead_mode="motif", bass="octave_bounce",
                 arp="dense16", drums="drive", swing=0.0, hat=1.0,
                 lead_gain=0.40, pad_gain=0.30),
    ),
    "boss_hidden": dict(
        # 히든 보스 — 스테이지 보스보다 한 단계 더 무겁게.
        #
        # 같은 "느리고 웅장"이라도 스테이지 보스와 성격이 달라야 만난 보람이 있다.
        # 저쪽이 "다가오는 위압"이라면 이쪽은 **"눌러앉은 것"**이다: 76bpm으로 더
        # 늦추고, 증3화음이 반음씩 내려가 어디에도 착지하지 않는다(4면 성운에서 쓴
        # 수법을 저역·느린 템포로 옮긴 것). 이끔음이 없는 홀톤 스케일이라 "곧 끝난다"는
        # 기대 자체가 생기지 않는다. 패드는 스트링으로 깔아 공간을 넓게 잡는다.
        bpm=76, bars=16, motif_seed=137,
        scale=[0, 2, 4, 6, 8, 10, 12],                          # 홀톤
        chords_a=[(-26, "aug"), (-26, "aug"), (-27, "aug"), (-27, "aug"),
                  (-28, "aug"), (-28, "aug"), (-29, "aug"), (-29, "aug")],
        # B절: 같은 하강을 더 낮은 자리에서 — 내려갈수록 벗어나기 어려워진다
        chords_b=[(-29, "aug"), (-29, "aug"), (-30, "aug"), (-30, "aug"),
                  (-31, "aug"), (-31, "aug"), (-26, "aug"), (-26, "aug")],
        pad="strings", echo=(320, 0.44),
        arr=dict(lead_voice="octave", lead_mode="anthem", bass="whole_octave",
                 arp="swell", drums="epic", swing=0.0, hat=0.0,
                 lead_gain=0.34, pad_gain=0.40),
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
    """한 곡을 스테레오로 편곡한다.

    편곡 엔진이 둘이다:
      - ``arrange_staged``  : 스테이지 5곡. 편곡 축(리드 음색·베이스 패턴·햇 밀도·
        멜로디 리듬·A/B 구성)을 테마마다 다르게 준다.
      - ``arrange_classic`` : boss·title. 사람이 "유지"로 못박은 두 곡이라
        기존 골격을 **한 줄도 건드리지 않고** 그대로 쓴다 (시드 0 재현 보장).
    """
    if "arr" in THEMES[theme_name]:
        return arrange_staged(theme_name, seed)
    return arrange_classic(theme_name, seed)


def arrange_classic(theme_name: str, seed: int) -> tuple[np.ndarray, np.ndarray]:
    """단일 편곡 골격 (boss·title 전용).

    모든 트랙이 이 골격 하나를 공유해서 "스테이지 음악이 다 비슷비슷"이 됐다
    (사람 지적 2026-08-02) — 스케일과 악기만 바뀌고 베이스 8분 펄스·벨 핑퐁
    아르페지오·마디당 모티프가 전부 같았다. 스테이지 5곡은 arrange_staged로
    옮겼고, 여기는 유지 대상인 두 곡을 위해 남는다.
    """
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


# ── 스테이지 편곡 엔진 ───────────────────────────────────────────────────────
#
# "스테이지 음악이 다 비슷비슷. 1=경쾌, 2=잔잔, 3·4=긴장, 5=웅장" (사람, 2026-08-02).
#
# 진단: 프리셋(진행·스케일·악기)만 바꾸고 **편곡은 하나**였다. 어떤 곡이든
# 베이스는 8분 펄스, 아르페지오는 벨 핑퐁 8분, 멜로디는 모티프 변주, 드럼은
# 8스텝 — 스케일이 달라도 골격이 같으면 같은 곡으로 들린다. 그래서 프리셋이
# 아니라 **편곡 축**을 늘린다:
#
#   lead_voice  리드 파형/듀티      square25 · triangle · square50 · tremolo · octave
#   lead_mode   멜로디 리듬          motif · sustain · hold · tremolo · anthem
#   bass        베이스 패턴          octave_bounce · whole · driving8 · chromatic_fall
#                                    · whole_octave
#   arp         화성 움직임          pingpong8 · none · dense16 · swell · fifth_pad
#   drums/hat/swing  리듬 밀도와 그루브
#   chords_a/b  A절·B절 코드 (2부 구성)
#
# 코드는 (근음, 성질) 쌍으로 준다 — 예전의 "루트 음정으로 3도를 추정" 휴리스틱은
# 픽카르디 종지(core)나 증3화음(nebula) 같은 의도적 화성을 표현할 수 없었다.

CHORD_TONES = {
    "maj": [0, 4, 7],
    "min": [0, 3, 7],
    "min7": [0, 3, 7, 10],
    "maj7": [0, 4, 7, 11],
    "dom7": [0, 4, 7, 10],
    "aug": [0, 4, 8],
    "sus4": [0, 5, 7],
}


# ── 리드 음색 ────────────────────────────────────────────────────────────────

def voice_square(f: float, dur: float, vel: float = 1.0, duty: float = 0.25) -> np.ndarray:
    """칩 스퀘어 리드. duty 0.25는 밝고 코를 찌르는 소리, 0.5는 두껍고 공격적."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 8.0, 5.4, delay_s=0.09)
    x = sq(phase(fv, n), duty) + 0.35 * np.sin(phase(fv, n))
    x = lp(x, 5200)
    x = saturate(x * 0.8, 1.4)
    return x * env_adsr(n, 0.012, 0.07, 0.82, 0.10) * vel


def voice_triangle(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """부드러운 삼각 리드. 느린 어택 + 긴 릴리스라 서스테인 프레이즈에 맞다."""
    n = int(dur * SR)
    if n < 8:
        return np.zeros(0)
    fv = vibrato(f, n, 6.0, 3.8, delay_s=0.22)
    x = tri(phase(fv, n)) + 0.22 * np.sin(phase(fv * 2.0, n))
    x = lp(x, 3200)
    return x * env_adsr(n, 0.075, 0.16, 0.86, 0.30) * vel


def voice_octave(f: float, dur: float, vel: float = 1.0) -> np.ndarray:
    """옥타브 유니즌 브라스. 같은 선율을 두 옥타브로 겹치면 무게가 생긴다."""
    a = inst_brass(f, dur, vel)
    b = inst_brass(f * 2.0, dur, vel * 0.55)
    n = min(len(a), len(b))
    if n == 0:
        return np.zeros(0)
    return a[:n] + b[:n]


LEAD_VOICES = {
    "square25": lambda f, d, v: voice_square(f, d, v, 0.25),
    "square50": lambda f, d, v: voice_square(f, d, v, 0.5),
    "triangle": voice_triangle,
    "octave": voice_octave,
    "bell": inst_bell,
}


def noise_swell(dur: float, vel: float, rng: np.random.Generator,
                cutoff: float = 2600.0) -> np.ndarray:
    """롱 노이즈 스웰. 햇 대신 깔면 박자가 아니라 '기압'이 흐른다 (nebula)."""
    n = int(dur * SR)
    if n < 32:
        return np.zeros(0)
    x = hp(rng.uniform(-1, 1, n), cutoff)
    env = np.linspace(0.0, 1.0, n) ** 2.6
    tail = max(1, int(n * 0.12))
    env[-tail:] *= np.linspace(1.0, 0.0, tail)
    return x * env * vel


# ── 리듬 (16스텝 그리드) ─────────────────────────────────────────────────────

def drum16(style: str, step: int, bar: int, rng: np.random.Generator
           ) -> list[tuple[str, float]]:
    """16분 스텝별 드럼. 스테이지 곡은 8스텝으로는 밀도 차를 못 만든다."""
    hits: list[tuple[str, float]] = []
    fill = (bar % 8 == 7)

    if style == "shuffle":                       # scrapyard — 경쾌한 셔플
        if step in (0, 10):
            hits.append(("kick", 1.0))
        if step == 6 and bar % 4 == 3:
            hits.append(("kick", 0.8))
        if step in (4, 12):
            hits.append(("snare", 0.85))
        if step % 2 == 0:                        # 8분 햇 (배치 때 스윙이 걸린다)
            hits.append(("hat", 0.42 if step % 4 == 0 else 0.24))
    elif style == "ambient":                     # hive — 거의 없음
        if step == 0 and bar % 2 == 0:
            hits.append(("kick", 0.55))
        if step == 8 and bar % 4 == 2:
            hits.append(("tom", 0.32))
    elif style == "drive":                       # fortress — 촘촘
        if step in (0, 6, 8, 14):
            hits.append(("kick", 1.0 if step in (0, 8) else 0.75))
        if step in (4, 12):
            hits.append(("snare", 0.9))
        hits.append(("hat", 0.34 if step % 4 == 0 else 0.17))
    elif style == "pulse":                       # nebula — 심장박동만
        if step == 0:
            hits.append(("kick", 0.7))
        if step == 3:
            hits.append(("kick", 0.4))           # 두 번째 박동 (숨죽인 긴장)
        if step == 8 and bar % 4 == 3:
            hits.append(("snare", 0.4))
    elif style == "epic":                        # core — 하프타임, 무겁게
        if step in (0, 11):
            hits.append(("kick", 1.0 if step == 0 else 0.7))
        if step == 8:
            hits.append(("snare", 0.95))
        if step == 0 and bar % 4 == 0:
            hits.append(("hat", 0.5))            # 마디 머리 오픈햇 = 심벌 대용
        if fill and step >= 12:
            hits.append(("tom", 0.6 + 0.12 * (step - 12)))
    return hits


# ── 레이어 ───────────────────────────────────────────────────────────────────

def bass_line(kind: str, root: int, beat: float, bar: int) -> list[tuple[int, float, float, float]]:
    """(반음, 시작(박), 길이(박), 벨로시티) 목록을 돌려준다."""
    lo = root - 12
    if kind == "octave_bounce":                  # 통통 튀는 옥타브 (scrapyard)
        out = []
        for k in range(8):
            p = lo if k % 2 == 0 else lo + 12
            out.append((p, k * 0.5, 0.46, 0.95 if k % 2 == 0 else 0.6))
        if bar % 4 == 3:                         # 마디 끝 5도 픽업
            out.append((lo + 7, 3.75, 0.22, 0.7))
        return out
    if kind == "whole":                          # 온음표 (hive)
        return [(lo, 0.0, 3.9, 0.8)]
    if kind == "driving8":                       # 드라이빙 8분 (fortress)
        out = [(lo, k * 0.5, 0.46, 1.0 if k % 2 == 0 else 0.72) for k in range(8)]
        out[7] = (lo + 7, 3.5, 0.46, 0.85)
        return out
    if kind == "chromatic_fall":                 # 반음 하강 (nebula)
        return [(lo - i, i * 1.0, 0.95, 0.85 - 0.05 * i) for i in range(4)]
    if kind == "whole_octave":                   # 온음표 + 옥타브 강타 (core)
        return [(lo, 0.0, 3.9, 0.85),
                (lo + 12, 0.0, 0.9, 0.7),
                (lo + 12, 2.0, 0.9, 0.6)]
    return [(lo, 0.0, 3.9, 0.8)]


def melody_phrase(mode: str, motif, bar: int, section: str, scale: list[int],
                  rng: np.random.Generator) -> list[tuple[int, float, float, float]]:
    """(반음, 시작(박), 길이(박), 벨로시티). 리듬이 곡의 성격을 절반은 정한다."""
    if mode == "motif":                          # 경쾌한 모티프 변주 (scrapyard)
        phrase = vary_motif(motif, bar, rng)
        out, at = [], 0.0
        for pitch, dur in phrase:
            if at >= 4.0:
                break
            out.append((pitch, at, min(dur, 4.0 - at) * 0.92, 0.85 if dur >= 1.0 else 0.74))
            at += dur
        return out

    if mode == "sustain":                        # 긴 서스테인 프레이즈 (hive)
        if bar % 4 == 3:                         # 네 마디마다 통째로 쉰다 — 여백이 잔잔함이다
            return []
        a = motif[0][0]
        b = motif[min(2, len(motif) - 1)][0]
        if section == "B":
            a, b = a + 12, b + 5
        return [(a, 0.0, 1.9, 0.62), (b, 2.0, 1.85, 0.55)]

    if mode == "hold":                           # 아르페지오 위에 얹는 긴 강음 (fortress)
        if bar % 2 == 0:
            return [(motif[0][0], 0.0, 1.4, 0.9), (motif[1][0], 1.5, 2.4, 0.85)]
        return [(motif[min(2, len(motif) - 1)][0], 0.5, 3.2, 0.88)]

    if mode == "tremolo":                        # 같은 음 16분 반복 (nebula)
        base = motif[bar % len(motif)][0]
        nxt = base + (2 if section == "A" else -2)
        out = []
        for k in range(8):                       # 앞 2박: 트레몰로
            out.append((base, k * 0.25, 0.22, 0.42 + 0.05 * (k % 2)))
        for k in range(8):                       # 뒤 2박: 반음/온음 이동
            out.append((nxt, 2.0 + k * 0.25, 0.22, 0.40 + 0.05 * (k % 2)))
        return out

    if mode == "anthem":                         # 옥타브 유니즌 장음 (core)
        if bar % 4 == 3:
            return [(motif[0][0] - 5, 0.0, 1.9, 0.9), (motif[0][0], 2.0, 1.9, 0.95)]
        pitch = motif[bar % len(motif)][0]
        return [(pitch, 0.0, 3.6, 0.92)]

    return []


def master(left: np.ndarray, right: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """arrange_classic과 동일한 마스터 체인 (피크 → 새추 → RMS 정렬)."""
    peak = max(float(np.max(np.abs(left))), float(np.max(np.abs(right))), 1e-9)
    left, right = left / peak, right / peak
    left, right = saturate(left * 0.9, 1.25), saturate(right * 0.9, 1.25)
    rms = float(np.sqrt(np.mean(np.concatenate([left, right]) ** 2)))
    gain = TARGET_RMS / max(rms, 1e-9)
    peak = max(float(np.max(np.abs(left))), float(np.max(np.abs(right))), 1e-9)
    gain = min(gain, 0.97 / peak)
    return left * gain * MASTER, right * gain * MASTER


def arrange_staged(theme_name: str, seed: int) -> tuple[np.ndarray, np.ndarray]:
    """스테이지 곡 편곡. 테마마다 편곡 축이 달라 골격 자체가 다른 곡이 된다."""
    cfg = THEMES[theme_name]
    arr = cfg["arr"]
    rng = np.random.default_rng(seed + cfg["motif_seed"])

    bpm, bars = cfg["bpm"], cfg["bars"]
    beat = 60.0 / bpm
    bar_len = beat * 4
    total = int(bar_len * bars * SR) + SR

    left, right = np.zeros(total), np.zeros(total)

    def place(buf: np.ndarray, sig: np.ndarray, at: float, gain: float) -> None:
        i = int(at * SR)
        if i < 0 or i >= len(buf) or len(sig) == 0:
            return
        end = min(len(buf), i + len(sig))
        buf[i:end] += sig[:end - i] * gain

    def stereo_place(sig: np.ndarray, at: float, gain: float, pan: float) -> None:
        ang = (pan * 0.5 + 0.5) * (np.pi / 2)
        place(left, sig, at, gain * float(np.cos(ang)))
        place(right, sig, at, gain * float(np.sin(ang)))

    swing = arr.get("swing", 0.0)

    def swung(beats: float) -> float:
        """8분 뒷박을 뒤로 밀어 셔플을 만든다 (0.0이면 스트레이트)."""
        if swing <= 0.0:
            return beats * beat
        # 8분 그리드에서 홀수 번째(뒷박)만 민다
        idx = beats / 0.5
        if abs(idx - round(idx)) < 1e-6 and int(round(idx)) % 2 == 1:
            return (beats + swing * 0.5) * beat
        return beats * beat

    scale = cfg["scale"]
    lead_voice = LEAD_VOICES[arr["lead_voice"]]
    pad_inst = INSTRUMENTS[cfg["pad"]]
    motif = make_motif(scale, rng)

    for bar in range(bars):
        t0 = bar * bar_len
        section = "A" if bar < bars // 2 else "B"
        chords = cfg["chords_a"] if section == "A" else cfg["chords_b"]
        root, quality = chords[bar % len(chords)]
        tones = CHORD_TONES[quality]
        intro = (bar < 2)                        # 도입 2마디는 얇게

        # --- 베이스 ---
        for p, at, dur, vel in bass_line(arr["bass"], root, beat, bar):
            stereo_place(inst_bass(note_hz(p), beat * dur, vel),
                         t0 + at * beat, 0.44, 0.0)

        # --- 패드/화성 ---
        chord = [root + i for i in tones]
        if not intro:
            chord.append(root + 12)
        for ci, p in enumerate(chord):
            pan = -0.55 + 1.1 * (ci / max(1, len(chord) - 1))
            stereo_place(pad_inst(note_hz(p), bar_len * 0.98, 0.32 if intro else 0.46),
                         t0, arr.get("pad_gain", 0.3), pan)

        # --- 화성 움직임 (아르페지오 / 스웰 / 5도 겹) ---
        kind = arr["arp"]
        if kind == "pingpong8" and not intro:
            notes = [root + 12 + t for t in tones] + [root + 24]
            for k in range(8):
                stereo_place(inst_bell(note_hz(notes[k % len(notes)]), beat * 0.5, 0.3),
                             t0 + swung(k * 0.5), 0.22, -0.7 if k % 2 == 0 else 0.7)
        elif kind == "dense16":
            notes = [root + 12 + t for t in tones] + [root + 24] \
                + [root + 12 + t for t in reversed(tones)]
            for k in range(16):
                stereo_place(
                    voice_square(note_hz(notes[k % len(notes)]), beat * 0.24,
                                 0.34 if k % 4 == 0 else 0.24, 0.125),
                    t0 + k * beat * 0.25, 0.20, -0.6 if k % 2 == 0 else 0.6)
        elif kind == "swell":
            stereo_place(noise_swell(bar_len * 0.98, 0.62 if section == "B" else 0.46, rng),
                         t0, 0.33, -0.25)
            stereo_place(noise_swell(bar_len * 0.6, 0.34, rng, cutoff=1200.0),
                         t0 + bar_len * 0.35, 0.24, 0.45)
        elif kind == "droplet" and bar % 2 == 1:
            # 두 마디에 한 방울. 밀도는 최소로 두되 완전한 무(無)는 답답하다 —
            # 긴 에코가 이 한 음을 공간으로 펼친다 (hive).
            stereo_place(inst_bell(note_hz(root + 24 + tones[bar // 2 % len(tones)]),
                                   beat * 1.6, 0.52),
                         t0 + beat * (1.0 if section == "A" else 2.5),
                         0.28, -0.5 if bar % 4 == 1 else 0.5)
        elif kind == "fifth_pad" and not intro:
            for p, pan in ((root + 7, -0.6), (root + 19, 0.6)):
                stereo_place(inst_strings(note_hz(p), bar_len * 0.98, 0.5), t0, 0.26, pan)

        # --- 리드 ---
        if not intro:
            for pitch, at, dur, vel in melody_phrase(
                    arr["lead_mode"], motif, bar, section, scale, rng):
                length = min(beat * dur, bar_len - at * beat)
                if length <= 0:
                    continue
                stereo_place(lead_voice(note_hz(root + 24 + pitch), length, vel),
                             t0 + swung(at), arr.get("lead_gain", 0.34), 0.12)

        # --- 드럼 ---
        for k in range(16):
            for name, vel in drum16(arr["drums"], k, bar, rng):
                if name == "hat":
                    vel *= arr.get("hat", 1.0)
                    if vel <= 0.01:
                        continue
                at = t0 + (swung(k * 0.25) if swing > 0 else k * beat * 0.25)
                if name == "kick":
                    stereo_place(drum_kick(vel), at, 0.5, 0.0)
                elif name == "snare":
                    stereo_place(drum_snare(vel, rng), at, 0.34, -0.08)
                elif name == "hat":
                    stereo_place(drum_hat(vel, k % 16 == 0 and arr["drums"] == "epic", rng),
                                 at, 0.26, 0.35)
                elif name == "tom":
                    stereo_place(drum_tom(150 + 40 * (k % 4), vel), at, 0.3, -0.3)

    d_ms, fb = cfg["echo"]
    left = echo(left, d_ms, fb)
    right = echo(right, d_ms * 1.14, fb)

    # 루프 이음새: 에코 꼬리를 앞으로 접어 넣어 루프가 끊기지 않게 한다.
    loop_n = int(bar_len * bars * SR)
    for buf in (left, right):
        tail = buf[loop_n:].copy()
        if len(tail):
            m = min(len(tail), loop_n)
            buf[:m] += tail[:m] * 0.85
    left, right = left[:loop_n], right[:loop_n]

    # 루프 이음새의 표본 단차를 없앤다. 에코 꼬리를 접어 넣어도 마지막 표본과 첫
    # 표본의 값은 여전히 다르고, 그 단차가 매 루프마다 광대역 '틱'으로 들린다
    # (기존 scrapyard 단차 0.095). 양 끝에 3ms 레이즈드코사인을 걸면 x[0]≈x[-1]≈0이
    # 되어 단차가 사라진다 — 140bpm 곡에서 3ms는 강박 킥에 완전히 묻힌다.
    fade = int(0.003 * SR)
    if fade * 2 < loop_n:
        ramp = 0.5 - 0.5 * np.cos(np.linspace(0.0, np.pi, fade))
        for buf in (left, right):
            buf[:fade] *= ramp
            buf[-fade:] *= ramp[::-1]

    return master(left, right)


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
