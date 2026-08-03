"""GameData의 좌표·크기가 **1/256 서브유닛 격자**에 떨어지는지 본다.

왜 있나:
    AGENTS.md §4 결정론 규칙이다. 격자를 벗어난 값 하나가 들어오면 EditMode
    테스트가 무더기로 터진다 — 2026-08-03에 편대 y 간격 하나 때문에 26개가
    한꺼번에 실패했다. 그때 발견 경로는 Unity 테스트 러너(3분)였는데,
    이 검사는 1초 안에 같은 답을 준다.

무엇을 보나: 월드 유닛으로 해석되는 실수 필드. 256을 곱했을 때 정수가 아니면
문제다 (예: 0.1 → 25.6). 틱·HP·개수처럼 격자와 무관한 필드는 건너뛴다.

사용:
    python Tools/CsCheck/subunit_grid_check.py <파일...>
    python Tools/CsCheck/subunit_grid_check.py --changed
종료 코드: 문제가 있으면 1.
"""
import io
import json
import os
import subprocess
import sys

SUBUNITS = 256

# **허용 목록**이다. 처음에는 "격자와 무관한 키를 제외"하는 방식으로 짰는데,
# 배율(scoreMultiplier 1.3)·표류 속도(0.35)·HP 임계값(0.667)까지 무더기로 잡아
# 실제 데이터에서 46건이 떴다. 전부 **비율**이라 격자 규칙 대상이 아니다.
# 매번 우는 늑대가 되느니, 규칙이 실제로 지배하는 **월드 좌표·크기**만 본다.
GRID_KEYS = frozenset({
    'x', 'y',
    'offsetx', 'offsety',
    'halfwidth', 'halfheight',
    'holdx', 'originx', 'originy',
    'anchoroffsety',
    'amplitude', 'movementamplitude',
    'spawnx',
})


def is_grid_key(key):
    return key.lower() in GRID_KEYS


def walk(node, path, problems):
    if isinstance(node, dict):
        for key, value in node.items():
            walk(value, f'{path}.{key}' if path else key, problems)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            walk(value, f'{path}[{index}]', problems)
    elif isinstance(node, float):
        key = path.rsplit('.', 1)[-1].split('[')[0]
        if not is_grid_key(key):
            return
        scaled = node * SUBUNITS
        if abs(scaled - round(scaled)) > 1e-9:
            problems.append(
                f'{path} = {node} — 256을 곱하면 {scaled:.4f}로 격자를 벗어난다')


def changed_files():
    try:
        out = subprocess.run(
            ['git', 'diff', '--name-only', 'HEAD'],
            capture_output=True, text=True, check=False).stdout
    except OSError:
        return []
    return [p for p in out.split('\n')
            if p.strip().endswith('.json') and 'GameData' in p]


def main(argv):
    targets = argv[1:]
    if targets == ['--changed'] or not targets:
        targets = changed_files()
    targets = [t for t in targets
               if t.endswith('.json') and 'GameData' in t and os.path.isfile(t)]
    if not targets:
        return 0

    failed = 0
    for path in targets:
        try:
            with io.open(path, encoding='utf-8') as handle:
                data = json.load(handle)
        except (OSError, ValueError) as error:
            print(f'{path}: 읽을 수 없다 — {error}')
            failed = 1
            continue
        problems = []
        walk(data, '', problems)
        for problem in problems:
            print(f'{path}: {problem}')
            failed = 1
    return failed


if __name__ == '__main__':
    sys.exit(main(sys.argv))
