#!/usr/bin/env python3
"""빌드에 들어가는 GameData 사본이 원본과 같은지 확인한다.

왜 있나 (2026-08-05): 런타임은 `GameData/*.json`이 아니라 `Assets/Resources/
GameData/*.json` 사본을 읽는다. 사본 갱신은 씬 재생성에만 붙어 있었는데, 아트가
안 바뀐 날에는 씬 재생성을 건너뛰는 것이 자연스러워서 **데이터만 바뀐 변경이
통째로 빌드에서 빠졌다.** 전함 2막 부무장 탄막이 그렇게 사라졌다.

무서운 점은 조용하다는 것이다. Core 테스트는 원본을 읽으므로 582개가 전부
통과하고, 빌드도 성공하고, 콘솔 에러도 0이다. 화면에서만 기능이 없다. 그
간극을 사람이 스크린샷으로 찾아내야 했다.

빌드가 사본을 직접 맞추도록 고쳤지만(MobileBuilder.BuildWebGl), 이 검사는
그 경로를 타지 않는 실행(에디터 재생, 부분 빌드)에서도 어긋남을 잡는다.

실행: python Tools/CsCheck/gamedata_sync_check.py
"""
import hashlib
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "GameData"
COPY = ROOT / "Assets" / "Resources" / "GameData"


def digest(path):
    # 줄바꿈 차이는 무시한다 — 복사는 바이트 단위지만 도구에 따라 CRLF가 섞인다.
    data = path.read_bytes().replace(b"\r\n", b"\n")
    return hashlib.sha256(data).hexdigest()


def main():
    if not SOURCE.is_dir():
        print(f"FAIL: 원본 폴더가 없다: {SOURCE}")
        return 1

    problems = []
    for source in sorted(SOURCE.glob("*.json")):
        target = COPY / source.name
        if not target.exists():
            problems.append(f"{source.name}: 사본이 없다")
            continue
        if digest(source) != digest(target):
            problems.append(
                f"{source.name}: 사본이 원본과 다르다 "
                f"(원본 {source.stat().st_size}B / 사본 {target.stat().st_size}B)")

    for target in sorted(COPY.glob("*.json")) if COPY.is_dir() else []:
        if not (SOURCE / target.name).exists():
            problems.append(f"{target.name}: 원본에 없는 사본이 남아 있다")

    if problems:
        print("FAIL: 빌드용 GameData 사본이 원본과 어긋난다.")
        for line in problems:
            print("  - " + line)
        print("\n고치는 법: WebGL 빌드를 돌리면 자동으로 맞춰진다. 빌드 없이 맞추려면")
        print("  unity run . -- -executeMethod "
              "Shmup.EditorTools.BattleSceneBuilder.SyncGameDataToResources "
              "--non-interactive --no-banner")
        return 1

    count = len(list(SOURCE.glob("*.json")))
    print(f"OK: GameData {count}개 파일이 빌드 사본과 일치한다.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
