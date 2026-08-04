#!/usr/bin/env python3
"""초대형 빔 발사음 (전함 3막 코어 빔).

사람 지시 2026-08-05: "3페이즈 대형 레이저는 소리가 너무 썰렁해서 대형 레이저를
쏘는 박력있는 레이저음으로 해줘"

기존 sfx_laser_fire는 모든 적 레이저가 함께 쓰는 소리다. 반폭 0.25유닛짜리
잡몹 빔에 맞춰 짧고 얇게 만들어져서, 화면을 관통하는 반폭 5유닛 빔에 붙으면
크기와 소리가 어긋난다.

박력은 볼륨이 아니라 **저역과 길이**에서 온다:
  1. 서브 사인 스윕(하강)  — 몸으로 느끼는 무게. 실제 대구경 발사음의 핵심.
  2. 톱니 배음층          — 중역을 채워 "에너지"로 읽히게.
  3. 노이즈 버스트        — 어택의 파열. 이게 없으면 시작이 물렁하다.
  4. 긴 꼬리              — 빔이 지속되는 동안 남는 저역 울림.

실행: python Tools/SfxGen/sfxgen_heavy_beam.py
"""
import os
import pathlib
import struct
import wave

import numpy as np

SR = 32000
OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets" / "Audio" / "Sfx"


def write_wav(path, x):
    x = np.clip(x, -1.0, 1.0)
    data = (x * 32767.0).astype(np.int16)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"saved {path}  {len(x) / SR:.2f}s")


def heavy_beam(seconds=1.35):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(4242)

    # 1) 서브 스윕 — 92Hz에서 34Hz로 내려간다. 이 하강이 "거대한 것이 나갔다"다.
    sub_f = 92.0 * np.exp(-t * 2.4) + 34.0
    sub = np.sin(2.0 * np.pi * np.cumsum(sub_f) / SR)
    sub *= np.exp(-t * 1.5)

    # 2) 톱니 배음층 — 중역을 채운다. 살짝 디튠해 두께를 만든다.
    body_f = 210.0 * np.exp(-t * 3.2) + 78.0
    ph = 2.0 * np.pi * np.cumsum(body_f) / SR
    saw = 2.0 * ((ph / (2.0 * np.pi)) % 1.0) - 1.0
    saw2 = 2.0 * (((ph * 1.004) / (2.0 * np.pi)) % 1.0) - 1.0
    body = (saw + saw2) * 0.5 * np.exp(-t * 2.2)

    # 3) 어택 파열 — 앞 90ms만. 없으면 시작이 물렁하다.
    burst = rng.uniform(-1.0, 1.0, n) * np.exp(-t * 34.0)

    # 4) 지속 울림 — 빔이 살아 있는 동안 낮게 깔린다.
    hum = np.sin(2.0 * np.pi * 58.0 * t) * 0.35 * np.exp(-t * 1.1)
    hum += np.sin(2.0 * np.pi * 87.0 * t) * 0.18 * np.exp(-t * 1.4)

    x = sub * 1.0 + body * 0.55 + burst * 0.42 + hum
    # 소프트 클립 — 배음을 더해 폰 스피커에서도 저역이 읽히게 한다.
    x = np.tanh(x * 1.9) / np.tanh(1.9)

    # 시작을 아주 짧게 페이드해 클릭을 없앤다 (2ms).
    fade = min(int(0.002 * SR), n)
    x[:fade] *= np.linspace(0.0, 1.0, fade)
    tail = min(int(0.06 * SR), n)
    x[-tail:] *= np.linspace(1.0, 0.0, tail)
    return x * 0.92


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    write_wav(OUT / "sfx_laser_heavy.wav", heavy_beam())


if __name__ == "__main__":
    main()
