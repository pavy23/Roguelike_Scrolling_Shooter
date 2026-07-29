using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 옵션 화면 v1 (M4): 해상도 프리셋·전체화면·발사 키 리바인딩.
    /// 일시정지 중 O 키로 연다. 설정은 PlayerPrefs에 저장하고 시작 시 복원한다.
    /// 리바인딩은 Input System 바인딩 오버라이드(JSON)로 저장 — 시뮬에는 영향 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OptionsScreen : MonoBehaviour
    {
        const string ResolutionPrefKey = "rss.resolution";
        const string FullscreenPrefKey = "rss.fullscreen";
        const string BindingsPrefKey = "rss.bindings";

        static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1152, 672),
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440)
        };

        [SerializeField] PlayerInputReader _input;

        bool _open;
        int _resolutionIndex;
        InputActionRebindingExtensions.RebindingOperation _rebind;
        GUIStyle _titleStyle, _bodyStyle;
        string _panelText;
        int _panelKey = -1;

        void Start()
        {
            _resolutionIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(ResolutionPrefKey, 0), 0, Resolutions.Length - 1);
            bool fullscreen = PlayerPrefs.GetInt(FullscreenPrefKey, 0) == 1;
            // 에디터에서는 해상도 강제 변경을 건너뛴다 (게임 뷰가 관리)
            if (!Application.isEditor)
                Apply(fullscreen);
            LoadBindings();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 리바인딩 대기 중에는 다른 입력 처리를 멈춘다
            if (_rebind != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) CancelRebind();
                return;
            }

            if (keyboard.oKey.wasPressedThisFrame && Time.timeScale == 0f)
                _open = !_open;
            if (!_open) return;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _resolutionIndex = (_resolutionIndex + 1) % Resolutions.Length;
                Apply(Screen.fullScreen);
            }
            if (keyboard.fKey.wasPressedThisFrame)
                Apply(!Screen.fullScreen);
            if (keyboard.bKey.wasPressedThisFrame)
                StartRebindFire();
            if (keyboard.digit1Key.wasPressedThisFrame) StartRebindMovePart("up");
            if (keyboard.digit2Key.wasPressedThisFrame) StartRebindMovePart("down");
            if (keyboard.digit3Key.wasPressedThisFrame) StartRebindMovePart("left");
            if (keyboard.digit4Key.wasPressedThisFrame) StartRebindMovePart("right");
            if (keyboard.xKey.wasPressedThisFrame)
                ResetBindings();
        }

        void Apply(bool fullscreen)
        {
            var resolution = Resolutions[_resolutionIndex];
            if (!Application.isEditor)
                Screen.SetResolution(resolution.x, resolution.y, fullscreen);
            PlayerPrefs.SetInt(ResolutionPrefKey, _resolutionIndex);
            PlayerPrefs.SetInt(FullscreenPrefKey, fullscreen ? 1 : 0);
        }

        InputAction FindFireAction()
        {
            if (_input == null || _input.Actions == null) return null;
            return _input.Actions.FindAction(_input.FireActionName, throwIfNotFound: false);
        }

        void StartRebindFire()
        {
            var fire = FindFireAction();
            if (fire == null) return;
            StartRebind(fire, -1);
        }

        /// <summary>Move 2D 컴포지트의 키보드 파트(up/down/left/right)를 리바인딩한다.</summary>
        void StartRebindMovePart(string partName)
        {
            if (_input == null || _input.Actions == null) return;
            var move = _input.Actions.FindAction("Move", throwIfNotFound: false);
            if (move == null) return;
            for (int i = 0; i < move.bindings.Count; i++)
            {
                var binding = move.bindings[i];
                if (binding.isPartOfComposite
                    && string.Equals(binding.name, partName, System.StringComparison.OrdinalIgnoreCase)
                    && binding.path.StartsWith("<Keyboard>", System.StringComparison.Ordinal))
                {
                    StartRebind(move, i);
                    return;
                }
            }
        }

        void StartRebind(InputAction action, int bindingIndex)
        {
            action.Disable();
            var operation = bindingIndex >= 0
                ? action.PerformInteractiveRebinding(bindingIndex)
                : action.PerformInteractiveRebinding();
            _rebind = operation
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(_ => FinishRebind(action))
                .OnCancel(_ => FinishRebind(action))
                .Start();
        }

        void FinishRebind(InputAction fire)
        {
            _rebind?.Dispose();
            _rebind = null;
            fire.Enable();
            if (_input != null && _input.Actions != null)
                PlayerPrefs.SetString(BindingsPrefKey, _input.Actions.SaveBindingOverridesAsJson());
            _panelText = null;   // 바인딩 표시 갱신
        }

        void CancelRebind()
        {
            _rebind?.Cancel();
        }

        void ResetBindings()
        {
            if (_input == null || _input.Actions == null) return;
            _input.Actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingsPrefKey);
            _panelText = null;   // 바인딩 표시 갱신
        }

        void LoadBindings()
        {
            if (_input == null || _input.Actions == null) return;
            string json = PlayerPrefs.GetString(BindingsPrefKey, null);
            if (!string.IsNullOrEmpty(json))
                _input.Actions.LoadBindingOverridesFromJson(json);
        }

        void OnGUI()
        {
            if (_rebind != null)
            {
                DrawPanel("PRESS ANY KEY FOR FIRE   (ESC cancel)");
                return;
            }
            if (!_open || Time.timeScale != 0f)
            {
                _open = _open && Time.timeScale == 0f;
                return;
            }

            // REQ-009: 열려 있는 동안 매 프레임 문자열을 만들지 않도록 상태 키 기준으로 캐시
            int panelKey = (_resolutionIndex << 1) | (Screen.fullScreen ? 1 : 0);
            if (_panelText == null || panelKey != _panelKey)
            {
                _panelKey = panelKey;
                var resolution = Resolutions[_resolutionIndex];
                var fire = FindFireAction();
                string fireBinding = fire != null
                    ? InputControlPath.ToHumanReadableString(
                        fire.bindings[0].effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice)
                    : "?";
                _panelText =
                    $"OPTIONS\n\n" +
                    $"[R] RESOLUTION   {resolution.x} x {resolution.y}\n" +
                    $"[F] FULLSCREEN   {(Screen.fullScreen ? "ON" : "OFF")}\n" +
                    $"[B] REBIND FIRE  (now: {fireBinding})\n" +
                    "[1]~[4] REBIND MOVE  (up/down/left/right)\n" +
                    $"[X] RESET BINDINGS\n\n" +
                    "[O] CLOSE";
            }
            DrawPanel(_panelText);
        }

        void DrawPanel(string text)
        {
            EnsureStyles();
            float width = Screen.width, height = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(width * 0.25f, height * 0.2f, width * 0.5f, height * 0.55f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(width * 0.25f, height * 0.22f, width * 0.5f, height * 0.5f), text, _bodyStyle);
        }

        void EnsureStyles()
        {
            if (_bodyStyle != null) return;
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
            };
        }
    }
}
