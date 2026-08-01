// 스코어보드 표시 이름 입력 브리지 (RENDERER, P1 글로벌 스코어보드).
//
// 폰 브라우저에서 Unity WebGL은 소프트 키보드를 띄우지 못한다 — UGUI InputField를
// 눌러도 IME가 붙지 않아 글자를 아예 못 넣는다. 그래서 이름만은 브라우저가 직접
// 그리는 window.prompt를 빌린다. 게임 상태는 건드리지 않는 순수 입력 통로다.
//
// 반환 규약: 취소하면 0(널 포인터), 아니면 _malloc으로 잡은 UTF-8 버퍼 포인터.
// C# 쪽(NamePrompt.ReadUtf8)이 널 종단까지 읽어 문자열로 만든다.
mergeInto(LibraryManager.library, {
  RssPromptName: function (defaultPtr) {
    var d = UTF8ToString(defaultPtr);
    var v = window.prompt('스코어보드에 표시할 이름 (2~10자)', d || '');
    if (v === null) return 0;
    var len = lengthBytesUTF8(v) + 1;
    var buf = _malloc(len);
    stringToUTF8(v, buf, len);
    return buf;
  }
});
