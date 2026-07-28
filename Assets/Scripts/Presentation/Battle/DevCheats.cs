using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 개발용 치트 + 오버레이. 빌드/에디터 공통으로 동작한다.
    ///
    /// - F9: 캡슐 획득(게이지 커서 전진), F10: 활성화 — 캡슐 드롭이 시뮬레이션에
    ///   연결되기 전(REQ-001 이후 교체)까지 HUD를 손으로 시험하기 위한 임시 입력이다.
    ///   이것은 dev 스캐폴딩이지 게임플레이 경로가 아니다.
    /// - 좌상단 오버레이: 현재 시드 / 틱 / 조작 안내. "시드 12345에서 2분 지점" 식의
    ///   재현 가능한 플레이 테스트 리포트를 위해 시드를 항상 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevCheats : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;

        GUIStyle _style;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _director == null || _director.Gauge == null) return;

            if (keyboard.f9Key.wasPressedThisFrame) _director.Gauge.Collect();
            if (keyboard.f10Key.wasPressedThisFrame) _director.Gauge.Activate();
        }

        void OnGUI()
        {
            if (_director == null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, Screen.height / 40),
                    normal = { textColor = new Color(0.7f, 0.85f, 1f, 0.9f) }
                };

            GUI.Label(new Rect(8, 4, Screen.width - 16, _style.fontSize * 3),
                $"seed {_director.Seed}   tick {_director.Tick}\n[F9] capsule   [F10] activate   (--seed=N 으로 시드 고정)",
                _style);
        }
    }
}
