using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 (UGUI + 픽셀 폰트). 스타필드가 천천히 흐르고, 시드를 확인/수정한 뒤
    /// Space/Enter/(A)로 출격한다. 시드 편집은 숫자 키 + 백스페이스 직접 처리
    /// (InputField/EventSystem 의존 없이 패드와 공존).
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
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        string _seedText;
        Text _promptText, _seedValueText;
        string _shownSeed;

        void Start()
        {
            _seedText = ((uint)System.Environment.TickCount).ToString();

            var canvas = UiKit.CreateCanvas("TitleCanvas", 50);
            canvas.transform.SetParent(transform, false);

            UiKit.CreateCornerText(canvas.transform, _fontBold, "ROGUELIKE", 40,
                new Color(0.62f, 0.83f, 1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                TextAnchor.UpperCenter, "Title1");
            UiKit.CreateCornerText(canvas.transform, _fontBold, "SCROLLING SHOOTER", 40,
                new Color(0.62f, 0.83f, 1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f),
                TextAnchor.UpperCenter, "Title2");
            _promptText = UiKit.CreateCornerText(canvas.transform, _font,
                "PRESS SPACE / (A) TO LAUNCH", 14, UiKit.TextAccent,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), TextAnchor.MiddleCenter, "Prompt");
            _seedValueText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextDim, new Vector2(0.5f, 0f), new Vector2(0f, 66f),
                TextAnchor.LowerCenter, "Seed");
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
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                EditSeed(keyboard);
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    StartRun();
                    return;
                }
            }
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                StartRun();
                return;
            }

            // 깜빡이는 출격 안내
            bool promptVisible = Mathf.Repeat(Time.time, 1f) < 0.7f;
            if (_promptText != null && _promptText.enabled != promptVisible)
                _promptText.enabled = promptVisible;

            if (_seedValueText != null && !ReferenceEquals(_shownSeed, _seedText))
            {
                _shownSeed = _seedText;
                _seedValueText.text = $"SEED  {_seedText}_   (숫자 입력/백스페이스로 수정)";
            }
        }

        void EditSeed(Keyboard keyboard)
        {
            if (keyboard.backspaceKey.wasPressedThisFrame && _seedText.Length > 0)
                _seedText = _seedText.Substring(0, _seedText.Length - 1);
            for (Key key = Key.Digit1; key <= Key.Digit0; key++)
            {
                if (!keyboard[key].wasPressedThisFrame || _seedText.Length >= 12) continue;
                int digit = key == Key.Digit0 ? 0 : key - Key.Digit1 + 1;
                _seedText += (char)('0' + digit);
            }
        }

        void StartRun()
        {
            DevArgs.RuntimeSeed = long.TryParse(_seedText, out long seed)
                ? seed
                : (uint)System.Environment.TickCount;
            SceneManager.LoadScene("Battle");
        }
    }
}
