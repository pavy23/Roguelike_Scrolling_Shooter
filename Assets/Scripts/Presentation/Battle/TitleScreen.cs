using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 플레이스홀더 타이틀 화면. 스타필드가 천천히 흐르고, 시드를 확인/수정한 뒤
    /// Space/Enter로 출격한다. 시각은 IMGUI 임시 — HD 도트 아트가 오면 교체.
    ///
    /// 시드는 방문할 때마다 새로 뽑는다. 이건 "이번 런을 무엇으로 할지"의 선택일 뿐이고
    /// (Presentation 소관), 같은 시드를 넣으면 같은 런이 나오는 것은 Core가 보장한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] Transform[] _layers;
        [SerializeField] float[] _factors;
        [SerializeField] float _tileWidth = 24f;
        [SerializeField] float _driftSpeed = 1.2f;

        string _seedText;
        GUIStyle _titleStyle, _promptStyle, _labelStyle;

        void Start()
        {
            _seedText = ((uint)System.Environment.TickCount).ToString();
        }

        void Update()
        {
            if (_layers != null && _factors != null)
            {
                float scroll = Time.time * _driftSpeed;
                for (int i = 0; i < _layers.Length && i < _factors.Length; i++)
                {
                    if (_layers[i] == null) continue;
                    float offset = Mathf.Repeat(scroll * _factors[i], _tileWidth);
                    _layers[i].localPosition = new Vector3(-offset, 0f, 0f);
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                StartRun();
        }

        void StartRun()
        {
            DevArgs.RuntimeSeed = long.TryParse(_seedText, out long seed)
                ? seed
                : (uint)System.Environment.TickCount;
            SceneManager.LoadScene("Battle");
        }

        void OnGUI()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(28, Screen.height / 7),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.62f, 0.83f, 1f, 1f) }
                };
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(14, Screen.height / 24),
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.88f, 0.55f, 1f) }
                };
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(12, Screen.height / 36),
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.7f, 0.85f, 1f, 0.9f) }
                };
            }

            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, h * 0.18f, w, h * 0.25f), "ROGUELIKE\nSCROLLING SHOOTER", _titleStyle);

            // 깜빡이는 출격 안내
            if (Mathf.Repeat(Time.time, 1f) < 0.7f)
                GUI.Label(new Rect(0, h * 0.62f, w, h * 0.1f), "PRESS SPACE TO LAUNCH", _promptStyle);

            GUI.Label(new Rect(w * 0.5f - 220, h * 0.78f, 150, 32), "SEED", _labelStyle);
            _seedText = GUI.TextField(new Rect(w * 0.5f - 60, h * 0.78f, 200, 32), _seedText, 12);
        }
    }
}
