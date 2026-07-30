using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 경로 선택 화면 (REQ-028): 스테이지 클리어 보상 후 다음 목적지를 고른다.
    /// 각 후보는 테마 × 조우 타입 — 어느 세계로, 어떤 위험을 안고 갈지가 런을 가른다.
    /// 키보드 1/2/3 즉시 선택, 좌우 커서 + Enter/(A) 확정. 선택만 Core에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteScreen : MonoBehaviour
    {
        const int MaxOptions = 3;

        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;
        [SerializeField] string[] _encounterNames;    // EncounterType 순서와 정렬
        [SerializeField] Sprite[] _encounterIcons;
        [SerializeField] string[] _themeIds;          // 테마 표시명 매핑용
        [SerializeField] string[] _themeNames;

        GameObject _root;
        readonly Image[] _boxBorders = new Image[MaxOptions];
        readonly Image[] _icons = new Image[MaxOptions];
        readonly Text[] _labels = new Text[MaxOptions];
        bool _built;
        int _cursor;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("RouteCanvas", 72);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.62f));
            UiKit.CreateCornerText(canvas.transform, _fontBold, UiText.RouteTitle, 16,
                UiKit.TextAccent, new Vector2(0.5f, 1f), new Vector2(0f, -84f),
                TextAnchor.UpperCenter, "Title");

            const float boxWidth = 150f, boxHeight = 86f, gap = 14f;
            float totalWidth = MaxOptions * boxWidth + (MaxOptions - 1) * gap;
            for (int i = 0; i < MaxOptions; i++)
            {
                var panel = UiKit.CreatePanel(canvas.transform,
                    new Vector2(boxWidth, boxHeight), $"Route{i}");
                panel.anchoredPosition = new Vector2(
                    -totalWidth / 2f + boxWidth / 2f + i * (boxWidth + gap), -6f);
                _boxBorders[i] = panel.GetComponent<Image>();

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(panel, false);
                var icon = iconGo.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, -10f);
                iconRect.sizeDelta = new Vector2(24f, 24f);
                _icons[i] = icon;

                _labels[i] = UiKit.CreateCornerText(panel, _font, "", 10,
                    UiKit.TextMain, new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                    TextAnchor.LowerCenter, "Label");
                _labels[i].rectTransform.sizeDelta = new Vector2(boxWidth - 8f, 44f);
                int index = i;   // 클로저가 루프 변수를 잡지 않도록 복사
                UiKit.MakeTappable(_boxBorders[i], () => Choose(index));
            }
            UiKit.CreateCornerText(canvas.transform, _font,
                UiPlatform.TouchMode ? UiText.ChoiceHintsTouch : UiText.ChoiceHints,
                10, UiKit.TextDim,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), TextAnchor.MiddleCenter, "Hints");

            _root.SetActive(false);
        }

        /// <summary>탭/키 공용 선택. 열려 있지 않거나 범위를 벗어난 탭은 무시한다.</summary>
        void Choose(int index)
        {
            if (_director == null || !_director.AwaitingRoute) return;
            var options = _director.RouteOptions;
            if (options == null || index < 0 || index >= options.Count) return;
            _director.ChooseRoute(index);
        }

        void Update()
        {
            if (_director == null || _root == null) return;
            bool awaiting = _director.AwaitingRoute;
            if (_root.activeSelf != awaiting)
                _root.SetActive(awaiting);
            if (!awaiting)
            {
                _built = false;
                return;
            }

            var options = _director.RouteOptions;
            if (options == null || options.Count == 0) return;
            if (!_built)
            {
                _built = true;
                _cursor = 0;
                for (int i = 0; i < MaxOptions; i++)
                {
                    bool used = i < options.Count;
                    _boxBorders[i].gameObject.SetActive(used);
                    if (!used) continue;
                    var option = options[i];
                    int encounter = (int)option.EncounterType;
                    _labels[i].text =
                        $"[{i + 1}] {ThemeName(option.ThemeId)}\n{EncounterName(encounter)}";
                    var sprite = IconFor(encounter);
                    _icons[i].enabled = sprite != null;
                    if (sprite != null) _icons[i].sprite = sprite;
                }
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) { _director.ChooseRoute(0); return; }
                if (keyboard.digit2Key.wasPressedThisFrame && options.Count > 1) { _director.ChooseRoute(1); return; }
                if (keyboard.digit3Key.wasPressedThisFrame && options.Count > 2) { _director.ChooseRoute(2); return; }
            }

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) move = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame) move = 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame) move = -1;
                if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame) move = 1;
            }
            if (move != 0)
                _cursor = Mathf.Clamp(_cursor + move, 0, options.Count - 1);

            bool confirm = (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                        || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            if (confirm)
            {
                _director.ChooseRoute(_cursor);
                return;
            }

            for (int i = 0; i < MaxOptions; i++)
                if (_boxBorders[i].gameObject.activeSelf)
                    _boxBorders[i].color = i == _cursor ? UiKit.TextAccent : UiKit.PanelBorder;
        }

        Sprite IconFor(int encounter)
        {
            if (_encounterIcons == null) return null;
            return encounter >= 0 && encounter < _encounterIcons.Length
                ? _encounterIcons[encounter] : null;
        }

        string EncounterName(int encounter)
        {
            if (_encounterNames != null && encounter >= 0 && encounter < _encounterNames.Length)
                return _encounterNames[encounter];
            return "?";
        }

        string ThemeName(string themeId)
        {
            if (_themeIds == null || _themeNames == null || themeId == null) return "?";
            int count = Mathf.Min(_themeIds.Length, _themeNames.Length);
            for (int i = 0; i < count; i++)
                if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                    return _themeNames[i];
            return themeId;
        }
    }
}
