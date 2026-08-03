"""C# 파일의 **리터럴/괄호 균형**만 빠르게 본다. 컴파일러가 아니다.

왜 있나:
    2026-08-04 세션에서 스크립트로 문자열을 치환하다 C#의 `\\n`을 진짜 줄바꿈으로
    써 넣어 리터럴이 여러 번 깨졌다. 그때마다 발견 경로가
    "씬 재생성 → EditMode → WebGL 빌드"(3~5분)여서, 20초면 알 수 있는 오류를
    다섯 번 왕복하며 20분 넘게 썼다.

    Tools/CoreStandalone은 Core와 테스트만 링크하므로 Presentation 파일의 오류를
    잡지 못한다. 그래서 Unity도 dotnet도 없이 도는 검사가 따로 필요하다.

무엇을 잡나 (이 세 가지가 실제로 겪은 전부다):
    - 한 줄을 넘어가 버린 문자열/문자 리터럴
    - 닫히지 않은 블록 주석
    - 파일 끝에서 맞지 않는 중괄호/괄호

무엇을 못 잡나: 타입 오류, 이름 오류, 시그니처 불일치. 그건 dotnet build와
Unity의 몫이다. 이 검사는 **가장 값싼 그물**이지 안전망 전체가 아니다.

사용:
    python Tools/CsCheck/cs_syntax_check.py <파일...>
    python Tools/CsCheck/cs_syntax_check.py --changed     # git 변경분만
종료 코드: 문제가 있으면 1.
"""
import io
import os
import subprocess
import sys

# 축약 표기를 허용하는 리터럴 접두사 조합 ($@" 와 @$" 둘 다 유효하다)
VERBATIM_PREFIXES = ('@$"', '$@"', '@"')


def check_source(text):
    """(줄번호, 메시지) 목록을 돌려준다. 빈 목록이면 통과."""
    problems = []
    depth = {'{': 0, '(': 0, '[': 0}
    closer = {'}': '{', ')': '(', ']': '['}
    line = 1
    i = 0
    n = len(text)

    while i < n:
        c = text[i]

        if c == '\n':
            line += 1
            i += 1
            continue

        # 주석
        if text.startswith('//', i):
            end = text.find('\n', i)
            i = n if end < 0 else end
            continue
        if text.startswith('/*', i):
            end = text.find('*/', i + 2)
            if end < 0:
                problems.append((line, '닫히지 않은 블록 주석'))
                break
            line += text.count('\n', i, end)
            i = end + 2
            continue

        # 축자 문자열 @"..." — 줄바꿈을 품는 것이 정상이고, "" 가 이스케이프다
        prefix = next((p for p in VERBATIM_PREFIXES if text.startswith(p, i)), None)
        if prefix:
            start_line = line
            i += len(prefix)
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                if text[i] == '\n':
                    line += 1
                i += 1
            else:
                problems.append((start_line, '닫히지 않은 축자 문자열(@")'))
            continue

        # 일반 문자열 / 문자 리터럴 — **줄을 넘어가면 오류다**
        if c == '"' or c == "'":
            quote = c
            start_line = line
            i += 1
            closed = False
            while i < n:
                ch = text[i]
                if ch == '\\':
                    i += 2
                    continue
                if ch == '\n':
                    break          # 닫히기 전에 줄이 끝났다
                if ch == quote:
                    closed = True
                    i += 1
                    break
                i += 1
            if not closed:
                kind = '문자열' if quote == '"' else '문자'
                problems.append(
                    (start_line,
                     f'{kind} 리터럴이 줄 끝에서 닫히지 않았다 '
                     f'— 스크립트로 편집하며 \\n을 진짜 줄바꿈으로 쓴 경우가 대부분이다'))
            continue

        if c in depth:
            depth[c] += 1
        elif c in closer:
            depth[closer[c]] -= 1
            if depth[closer[c]] < 0:
                problems.append((line, f"'{c}'가 짝 없이 닫혔다"))
                depth[closer[c]] = 0
        i += 1

    for opener, count in depth.items():
        if count > 0:
            problems.append((0, f"'{opener}' {count}개가 닫히지 않았다"))
    return problems


def changed_files():
    try:
        out = subprocess.run(
            ['git', 'diff', '--name-only', 'HEAD'],
            capture_output=True, text=True, check=False).stdout
    except OSError:
        return []
    return [p for p in out.split('\n') if p.strip().endswith('.cs')]


def main(argv):
    targets = argv[1:]
    if targets == ['--changed'] or not targets:
        targets = changed_files()
    targets = [t for t in targets if t.endswith('.cs') and os.path.isfile(t)]
    if not targets:
        return 0

    failed = 0
    for path in targets:
        with io.open(path, encoding='utf-8', errors='replace') as handle:
            problems = check_source(handle.read())
        for line, message in problems:
            where = f'{path}({line})' if line else path
            print(f'{where}: {message}')
            failed = 1
    return failed


if __name__ == '__main__':
    sys.exit(main(sys.argv))
