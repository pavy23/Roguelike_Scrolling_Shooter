#!/usr/bin/env python3
"""BgmGen — 결정론 절차 칩튠 BGM 생성기 (ROADMAP M2, 무료·라이선스 청정).

sfxgen과 같은 철학: (시드)가 원본이고 채택 시드를 커밋한다. 외부 서비스 없음.
구성: 8마디 코드 진행 루프 × 섹션 — 트라이앵글 베이스, 스퀘어 아르페지오,
시드 기반 펜타토닉 스퀘어 리드, 노이즈 햇. 끝을 페이드 없이 잘라 심리스 루프.

사용:
  python bgmgen.py --seed 0 --bars 32 --bpm 132 --out out/bgm_scrapyard_0.wav
"""
import argparse
import wave
from pathlib import Path

import numpy as np

SAMPLE_RATE = 44100


def note_hz(semitone_from_a4: float) -> float:
    return 440.0 * (2.0 ** (semitone_from_a4 / 12.0))


def square(phase, duty=0.5):
    return np.where((phase % 1.0) < duty, 1.0, -1.0)


def triangle(phase):
    p = phase % 1.0
    return 4.0 * np.abs(p - 0.5) - 1.0


def render_note(freq, length, wave_kind, duty=0.5, volume=1.0, decay=4.0):
    n = int(length * SAMPLE_RATE)
    t = np.arange(n) / SAMPLE_RATE
    phase = freq * t
    tone = square(phase, duty) if wave_kind == "square" else triangle(phase)
    env = np.exp(-decay * t / length)
    return tone * env * volume


def render_hat(length, rng, volume=0.2):
    n = int(length * SAMPLE_RATE)
    env = np.exp(-np.linspace(0, 9, n))
    return rng.uniform(-1, 1, n) * env * volume


# 테마별 프리셋 (M4): 진행(루트 반음계, A4=0), bpm 기본값, 리드 스케일, 아르페지오 밀도.
THEME_PRESETS = {
    "scrapyard": {
        "bpm": 132,
        "progression": [-24, -28, -33, -26, -24, -28, -29, -29],  # Am F C G / Am F E E
        "scale": [0, 3, 5, 7, 10, 12],        # 마이너 펜타토닉
        "arp": [12, 19, 24, 19, 12, 19, 24, 31],
    },
    "hive": {
        "bpm": 112,
        "progression": [-26, -26, -30, -25, -26, -26, -31, -25],  # G 프리지안 느낌 (반음 압박)
        "scale": [0, 1, 3, 5, 7, 8, 12],      # 프리지안
        "arp": [12, 18, 24, 18, 12, 18, 24, 30],
    },
    "fortress": {
        "bpm": 148,
        "progression": [-24, -24, -27, -29, -24, -24, -22, -29],  # 드라이빙 파워 진행
        "scale": [0, 3, 5, 6, 7, 10, 12],     # 블루스 마이너 (긴장)
        "arp": [12, 19, 24, 19, 12, 19, 24, 19],
    },
    "nebula": {
        "bpm": 100,
        "progression": [-21, -26, -19, -24, -21, -26, -14, -24],  # 부유하는 메이저7 느낌
        "scale": [0, 2, 4, 7, 9, 12],         # 메이저 펜타토닉 (몽환)
        "arp": [12, 16, 19, 24, 19, 16, 12, 16],
    },
    "core": {
        "bpm": 160,
        "progression": [-24, -23, -24, -22, -24, -23, -19, -17],  # 반음 상승 압박 (최종)
        "scale": [0, 1, 4, 5, 8, 11, 12],     # 더블 하모닉 (위압)
        "arp": [12, 13, 19, 24, 19, 13, 12, 13],
    },
}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--theme", choices=sorted(THEME_PRESETS), default="scrapyard")
    parser.add_argument("--bpm", type=int, default=None, help="미지정 시 테마 프리셋 값")
    parser.add_argument("--bars", type=int, default=32, help="4/4 마디 수 (8의 배수 권장)")
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    preset = THEME_PRESETS[args.theme]
    bpm = args.bpm if args.bpm else preset["bpm"]

    rng = np.random.default_rng(args.seed)
    beat = 60.0 / bpm
    bar = beat * 4
    total = int(args.bars * bar * SAMPLE_RATE)
    mix = np.zeros(total)

    progression = preset["progression"]
    minor_penta = preset["scale"]

    def place(buffer, start_sample, signal):
        end = min(start_sample + len(signal), total)
        if end > start_sample:
            buffer[start_sample:end] += signal[: end - start_sample]

    for bar_index in range(args.bars):
        root = progression[bar_index % len(progression)]
        bar_start = int(bar_index * bar * SAMPLE_RATE)

        # 베이스: 트라이앵글, 1·3박 루트 / 2·4박 5도
        for beat_index in range(4):
            semitone = root if beat_index % 2 == 0 else root + 7
            note = render_note(note_hz(semitone), beat * 0.95, "triangle", volume=0.5, decay=1.5)
            place(mix, bar_start + int(beat_index * beat * SAMPLE_RATE), note)

        # 아르페지오: 스퀘어 8분음표 (테마 프리셋 패턴)
        arp_pattern = preset["arp"]
        for eighth in range(8):
            semitone = root + arp_pattern[eighth]
            note = render_note(
                note_hz(semitone), beat * 0.45, "square", duty=0.25, volume=0.16, decay=5.0)
            place(mix, bar_start + int(eighth * beat / 2 * SAMPLE_RATE), note)

        # 리드: 시드 기반 펜타토닉 (2/4/8분 혼합), 마지막 2마디마다 쉼표로 호흡
        if bar_index % 8 not in (7,):
            position = 0.0
            while position < 4.0:
                duration = float(rng.choice([0.5, 0.5, 1.0, 1.0, 2.0]))
                if position + duration > 4.0:
                    duration = 4.0 - position
                if rng.random() < 0.8:   # 20% 쉼표
                    semitone = root + 24 + int(rng.choice(minor_penta))
                    note = render_note(
                        note_hz(semitone), beat * duration * 0.9,
                        "square", duty=0.5, volume=0.22, decay=2.5)
                    place(mix, bar_start + int(position * beat * SAMPLE_RATE), note)
                position += duration

        # 햇: 8분음표, 강박 크게
        for eighth in range(8):
            volume = 0.14 if eighth % 2 == 0 else 0.07
            place(
                mix,
                bar_start + int(eighth * beat / 2 * SAMPLE_RATE),
                render_hat(beat * 0.12, rng, volume))

    peak = np.max(np.abs(mix))
    if peak > 0:
        mix = mix / peak * 0.8

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    data = (mix * 32767).astype(np.int16)
    with wave.open(str(out), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SAMPLE_RATE)
        f.writeframes(data.tobytes())
    print(f"saved: {out} ({len(mix) / SAMPLE_RATE:.1f}s, theme {args.theme}, seed {args.seed}, {bpm}bpm)")


if __name__ == "__main__":
    main()
