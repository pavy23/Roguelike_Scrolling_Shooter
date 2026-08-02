// IDBFS 플러시 브리지 (RENDERER, WebGL 저장 유실 수정 — build23 테스터 발견).
//
// WebGL의 Application.persistentDataPath는 IndexedDB를 백엔드로 쓰는 IDBFS다.
// C#에서 File.WriteAllText로 쓴 내용은 **Emscripten 메모리 FS에만** 반영되고,
// FS.syncfs(false, cb)를 명시로 부르기 전까지 IndexedDB로 내려가지 않는다.
// 그래서 같은 세션에서는 저장이 멀쩡히 보이다가 브라우저를 껐다 켜면 마지막
// 저장(크레딧·컨티뉴 재고·해금)이 통째로 사라진다.
//
// 규약: RssSyncFileSystem()은 즉시 반환하고 실제 동기화는 비동기로 끝난다.
// 실패해도 게임을 세우지 않는다 — 저장은 어차피 best-effort고, 예외가 C#으로
// 넘어가면 저장 경로 전체가 죽는다.
mergeInto(LibraryManager.library, {
  RssSyncFileSystem: function () {
    try {
      // FS는 Unity WebGL 런타임이 항상 링크한다(persistentDataPath가 IDBFS라서).
      // 그래도 없는 빌드에서 조용히 빠져나가도록 가드를 둔다.
      if (typeof FS === 'undefined' || typeof FS.syncfs !== 'function') return;

      var s = window.__rssFileSync;
      if (!s) {
        s = window.__rssFileSync = {
          running: false,
          again: false,
          // syncfs를 겹쳐 부르면 IDB 트랜잭션이 서로를 덮어쓸 수 있다.
          // 진행 중이면 예약만 해 두고, 끝난 뒤 딱 한 번 더 돌린다.
          run: function () {
            if (s.running) { s.again = true; return; }
            s.running = true;
            try {
              FS.syncfs(false, function (err) {
                s.running = false;
                if (err) console.warn('[RssFileSync] syncfs 실패:', err);
                if (s.again) { s.again = false; s.run(); }
              });
            } catch (e) {
              s.running = false;
              console.warn('[RssFileSync] syncfs 예외:', e);
            }
          }
        };

        // 안전망: 탭을 닫거나 백그라운드로 보낼 때 한 번 더 내린다. 폰 브라우저는
        // beforeunload를 못 믿으므로 pagehide + visibilitychange를 함께 건다.
        // (C# 디바운스 타이머가 아직 안 터졌더라도 여기서 잡힌다.)
        var flush = function () { s.run(); };
        window.addEventListener('pagehide', flush);
        document.addEventListener('visibilitychange', function () {
          if (document.visibilityState === 'hidden') flush();
        });
      }

      s.run();
    } catch (e) {
      console.warn('[RssFileSync] 브리지 오류:', e);
    }
  }
});
