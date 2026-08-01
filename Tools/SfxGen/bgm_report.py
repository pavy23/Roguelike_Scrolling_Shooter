"""BGM 트랙 특성 실측 — "다 비슷비슷"을 수치로 판정한다 (2026-08-02).

귀로 "비슷하다"를 판단하면 논쟁만 남는다. 편곡이 실제로 갈라졌는지 다음으로 잰다:

  centroid   스펙트럼 중심(Hz) — 음색의 밝기. 파형·필터·악기 편성이 결정한다.
  act        활동도 — 에너지 상승분 총량. 이어지는 스트림형 편곡까지 잡는다.
  lo/mid/hi  대역 에너지 분포(%) — 250Hz 미만 / 250~2000 / 2000Hz 초과.
  crest      피크/RMS — 편곡 밀도. 촘촘할수록 낮고, 성글수록 높다.
  onsets/s   초당 음 시작 개수 — 리듬 밀도. 같은 골격이면 이 값이 서로 붙는다.
  seam       루프 이음새: 끝-처음 표본 단차와 (꼬리 RMS / 머리 RMS).

RMS는 일부러 재지 않는다 — arrange의 마스터가 TARGET_RMS로 트랙 간 라우드니스를
맞추기 때문이다(스테이지 전환 때 음량이 튀지 않게). 즉 RMS가 비슷한 것은 버그가
아니라 설계다. 편곡 차이는 위 지표로 봐야 한다.

사용: python bgm_report.py [디렉토리]
"""

from __future__ import annotations

import os
import sys
import wave

import numpy as np

ORDER = ["scrapyard", "hive", "fortress", "nebula", "core", "boss", "title"]


def load(path: str) -> tuple[np.ndarray, int]:
    with wave.open(path) as w:
        ch, sr, n = w.getnchannels(), w.getframerate(), w.getnframes()
        x = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float64) / 32768.0
    if ch == 2:
        x = x.reshape(-1, 2).mean(axis=1)
    return x, sr


def measure(x: np.ndarray, sr: int) -> dict:
    n = len(x)
    spec = np.abs(np.fft.rfft(x * np.hanning(n)))
    freqs = np.fft.rfftfreq(n, 1.0 / sr)
    tot = spec.sum()
    centroid = float((freqs * spec).sum() / tot) if tot else 0.0
    energy = spec ** 2
    e_tot = energy.sum() or 1.0
    lo = 100.0 * energy[freqs < 250].sum() / e_tot
    mid = 100.0 * energy[(freqs >= 250) & (freqs < 2000)].sum() / e_tot
    hi = 100.0 * energy[freqs >= 2000].sum() / e_tot

    rms = float(np.sqrt(np.mean(x ** 2)))
    crest = float(np.max(np.abs(x))) / max(rms, 1e-9)

    # 온셋: 짧은 창의 에너지 증가분(스펙트럼 플럭스)에서 피크를 센다
    hop = 256
    win = 1024
    frames = max(1, (n - win) // hop)
    env = np.empty(frames)
    for i in range(frames):
        seg = x[i * hop:i * hop + win]
        env[i] = np.sqrt(np.mean(seg ** 2))
    # 임계값을 flux의 표준편차로 잡으면 안 된다 — 성긴 곡일수록 분포가 좁아
    # 임계가 같이 내려가고, 결과적으로 잔잔한 트랙이 촘촘한 트랙보다 온셋이
    # 많다고 나온다(hive 9.85 > fortress 7.46 같은 역전). 마스터가 트랙 간
    # RMS를 맞춰 두므로 **평균 레벨 대비 절대 상승폭**으로 재는 것이 옳다.
    flux = np.diff(env, prepend=env[0]).clip(min=0) / max(env.mean(), 1e-9)
    peaks = (flux > 0.35) & (flux >= np.roll(flux, 1)) & (flux > np.roll(flux, -1))
    onsets = float(peaks.sum()) / (n / sr)
    # 활동도: 에너지 상승분의 총량. 16분 스트림처럼 끊김 없이 이어지는 편곡은
    # 뚜렷한 온셋 피크가 적게 잡히지만 이 값은 올라간다 — 둘을 같이 봐야 한다.
    activity = float(flux.mean()) * 100.0

    # 루프 이음새
    w_n = int(0.05 * sr)
    head = float(np.sqrt(np.mean(x[:w_n] ** 2)))
    tail = float(np.sqrt(np.mean(x[-w_n:] ** 2)))
    step = abs(float(x[0]) - float(x[-1]))

    return dict(dur=n / sr, centroid=centroid, lo=lo, mid=mid, hi=hi,
                rms=rms, crest=crest, onsets=onsets, activity=activity,
                seam_step=step, seam_ratio=tail / max(head, 1e-9))


def main() -> None:
    d = sys.argv[1] if len(sys.argv) > 1 else os.path.join("..", "..", "Assets", "Audio", "Bgm")
    rows = []
    for name in ORDER:
        p = os.path.join(d, f"bgm_{name}.wav")
        if not os.path.exists(p):
            continue
        rows.append((name, measure(*load(p))))

    head = (f"{'track':<11}{'dur(s)':>7}{'centroid':>9}{'lo%':>6}{'mid%':>6}{'hi%':>6}"
            f"{'crest':>7}{'onset/s':>8}{'act':>7}{'seam':>7}{'t/h':>6}")
    print(head)
    print("-" * len(head))
    for name, m in rows:
        print(f"{name:<11}{m['dur']:>7.1f}{m['centroid']:>9.0f}{m['lo']:>6.1f}"
              f"{m['mid']:>6.1f}{m['hi']:>6.1f}{m['crest']:>7.2f}{m['onsets']:>8.2f}"
              f"{m['activity']:>7.2f}{m['seam_step']:>7.3f}{m['seam_ratio']:>6.2f}")

    stage = [m for n, m in rows if n in ORDER[:5]]
    if len(stage) >= 2:
        print()
        for key, label in (("centroid", "스펙트럼 중심"), ("onsets", "온셋 밀도"),
                           ("activity", "활동도"), ("hi", "고역 비중%")):
            vals = [m[key] for m in stage]
            spread = max(vals) / max(min(vals), 1e-9)
            print(f"스테이지 5곡 {label:<8} 최소 {min(vals):8.2f}  최대 {max(vals):8.2f}"
                  f"  최대/최소 {spread:5.2f}배")


if __name__ == "__main__":
    main()
