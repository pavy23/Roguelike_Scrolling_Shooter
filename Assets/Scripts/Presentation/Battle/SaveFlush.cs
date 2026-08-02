using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 저장을 실제 영구 저장소로 밀어 넣는다 (WebGL 전용, 그 외 플랫폼은 완전한 no-op).
    ///
    /// 배경: WebGL의 <c>Application.persistentDataPath</c>는 IDBFS라 파일 쓰기가
    /// 메모리 FS에만 남고, <c>FS.syncfs(false)</c>를 부르기 전에는 IndexedDB로
    /// 내려가지 않는다. 같은 세션에서는 저장이 멀쩡히 보이지만 브라우저를 껐다 켜면
    /// 마지막 저장이 통째로 사라진다 (build23 테스터 리포트 §부록).
    /// 데스크톱/에디터는 OS 파일시스템이 알아서 하므로 부를 것이 없다.
    ///
    /// 전략: <see cref="Request"/>는 디바운스(마지막 요청 후 0.5초)로 모아서 한 번만
    /// 동기화한다 — 격납고 커서 이동처럼 연속으로 저장이 나가는 지점이 있기 때문이다.
    /// 종료·포커스 상실 같은 "지금 아니면 못 내린다" 시점은 <see cref="FlushNow"/>로
    /// 즉시 내린다. 브라우저 탭 종료(pagehide)는 jslib 쪽 리스너가 한 번 더 잡는다.
    /// </summary>
    public static class SaveFlush
    {
        /// <summary>마지막 저장 요청 후 이만큼 조용하면 동기화한다.</summary>
        public const float DebounceSeconds = 0.5f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void RssSyncFileSystem();

        static Pump _pump;
#endif

        /// <summary>저장 직후 호출. 디바운스되어 곧(0.5초 내) 실제 동기화가 일어난다.</summary>
        public static void Request()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_pump == null)
            {
                // 펌프가 아직/이미 없는 예외적 시점(부팅 직전, 종료 중)은 바로 내린다.
                FlushNative();
                return;
            }
            _pump.Schedule();
#endif
        }

        /// <summary>대기 중인 요청을 접고 지금 동기화한다 (종료·포커스 상실 시점).</summary>
        public static void FlushNow()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_pump != null) _pump.Cancel();
            FlushNative();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        static void FlushNative()
        {
            // PlayerPrefs도 같은 IDBFS 마운트에 얹혀 있다. Save()로 파일까지 내려보낸 뒤
            // 한 번의 syncfs로 세이브 파일과 함께 내보낸다 (옵션·난이도·표시 이름).
            PlayerPrefs.Save();
            RssSyncFileSystem();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_pump != null) return;

            var go = new GameObject("~RssSaveFlush");
            // 씬 전환 중에도 디바운스 타이머가 살아 있어야 한다
            UnityEngine.Object.DontDestroyOnLoad(go);
            _pump = go.AddComponent<Pump>();

            // 부팅 시 1회: jslib가 pagehide/visibilitychange 훅을 이 호출에서 심는다.
            // (저장이 한 번도 없던 세션에서도 설정 변경분이 탭 종료에 딸려 나가도록)
            FlushNative();
        }

        /// <summary>디바운스 타이머 + 생명주기 훅. 게임 로직은 전혀 건드리지 않는다.</summary>
        sealed class Pump : MonoBehaviour
        {
            float _timer = -1f;

            public void Schedule() => _timer = DebounceSeconds;
            public void Cancel() => _timer = -1f;

            void OnEnable() => Application.focusChanged += OnFocusChanged;
            void OnDisable() => Application.focusChanged -= OnFocusChanged;

            void OnFocusChanged(bool focused)
            {
                if (!focused) FlushNow();
            }

            void OnApplicationPause(bool paused)
            {
                if (paused) FlushNow();
            }

            void OnApplicationQuit() => FlushNow();

            void Update()
            {
                if (_timer < 0f) return;
                // 일시정지(timeScale 0) 중 저장도 제때 나가야 한다 — unscaled로 센다.
                _timer -= Time.unscaledDeltaTime;
                if (_timer <= 0f)
                {
                    _timer = -1f;
                    FlushNative();
                }
            }
        }
#endif
    }
}
