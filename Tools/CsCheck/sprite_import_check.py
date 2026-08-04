#!/usr/bin/env python3
"""Assets/Art/Sprites/*.png.meta 의 임포트 설정을 검사한다.

왜 있는가: 2026-08-04에 사람이 "히든 스테이지 배경이 뒤를 꽉채우는게 아니라
가운데만 조그맣게 들어가면 안됨"이라고 보고했다. 원인은 아트가 아니라 **임포트
설정**이었다 — Assets에 직접 복사해 넣은 PNG는 Unity 기본값(PPU 100 · Bilinear)로
들어오는데, 이 프로젝트의 규격은 PPU 16 · Point다. 640×360 배경이 6.4×3.6 유닛짜리
흐릿한 조각이 되어 화면 가운데에 박혔다.

이 종류는 조용하다. 컴파일도 되고 테스트도 통과하고 크래시도 없다. 화면을 봐야
알 수 있고, 그마저도 "아트가 이상한가?"로 오해하기 쉽다. 그래서 수치로 못 박는다.

사용:
    python sprite_import_check.py            # 전체
    python sprite_import_check.py --changed  # 스테이지된 변경분만 (훅용)
"""
import argparse
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
SPRITE_DIR = ROOT / "Assets" / "Art" / "Sprites"

# CLAUDE.md 고정 기술 결정: Assets PPU 16, Filter Mode Retro AA(=Point, 0).
EXPECTED_PPU = 16
EXPECTED_FILTER = 0

PPU_RE = re.compile(r"^\s*spritePixelsToUnits:\s*(\d+)", re.M)
FILTER_RE = re.compile(r"^\s*filterMode:\s*(-?\d+)", re.M)


def changed_meta_files():
    """스테이지된 png/meta에 대응하는 meta 경로."""
    try:
        out = subprocess.run(
            ["git", "diff", "--cached", "--name-only", "--diff-filter=ACM"],
            cwd=ROOT, capture_output=True, text=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return []
    seen = []
    for line in out.splitlines():
        path = ROOT / line.strip()
        if path.suffix == ".png":
            path = path.with_suffix(".png.meta")
        if path.name.endswith(".png.meta") and path.exists():
            seen.append(path)
    return seen


def check(paths):
    problems = []
    for meta in paths:
        text = meta.read_text(encoding="utf-8", errors="replace")
        # 스프라이트가 아닌 텍스처(폰트 아틀라스 등)는 PPU 항목이 없다.
        ppu = PPU_RE.search(text)
        filt = FILTER_RE.search(text)
        if ppu and int(ppu.group(1)) != EXPECTED_PPU:
            problems.append(
                f"{meta.relative_to(ROOT)}: spritePixelsToUnits "
                f"{ppu.group(1)} (기대 {EXPECTED_PPU}) — 화면에서 크기가 어긋난다")
        if filt and int(filt.group(1)) != EXPECTED_FILTER:
            problems.append(
                f"{meta.relative_to(ROOT)}: filterMode {filt.group(1)} "
                f"(기대 {EXPECTED_FILTER}=Point) — 픽셀 경계가 뭉개진다")
    return problems


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--changed", action="store_true")
    args = parser.parse_args()

    paths = (changed_meta_files() if args.changed
             else sorted(SPRITE_DIR.glob("*.png.meta")))
    if not paths:
        return 0

    problems = check(paths)
    if not problems:
        return 0

    print("스프라이트 임포트 설정이 규격과 다르다:", file=sys.stderr)
    for line in problems:
        print(f"  {line}", file=sys.stderr)
    print(
        "\n씬을 재생성하면 BattleSceneBuilder가 바로잡는다:\n"
        "  unity run . -- -executeMethod "
        "Shmup.EditorTools.BattleSceneBuilder.Build -logFile scene.log",
        file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
