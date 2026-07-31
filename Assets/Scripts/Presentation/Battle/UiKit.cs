using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 터치 조작으로 동작해야 하는 환경인지 한 곳에서 판정한다.
    /// WebGL은 <c>isMobilePlatform</c>이 false이고 Touchscreen도 첫 터치 전에는 없어서,
    /// 폰 브라우저에서 UI가 안 보이면 조작이 아예 불가능해진다 — 그래서 WebGL은 항상 참으로 본다.
    /// </summary>
    public static class UiPlatform
    {
        public static bool TouchMode =>
            ForceTouch
            || Application.isMobilePlatform
            || Application.platform == RuntimePlatform.WebGLPlayer
            || (Touchscreen.current != null && !Application.isEditor);

        /// <summary>에디터에서 터치 UI를 확인하기 위한 강제 플래그.</summary>
        public static bool ForceTouch { get; set; }
    }
    /// <summary>
    /// 코드 주도 UGUI 생성 헬퍼 (M4+ UI 픽셀 아트 전환).
    /// 프리팹 대신 각 화면이 Start에서 자기 패널을 조립한다 — 씬 빌더를 얇게 유지하고
    /// 리비전 관리를 코드로 일원화하기 위함. 생성은 로드 시 1회, 프레임 루프 무할당.
    /// 캔버스는 640×360 기준 정수 배율(PixelUiScaler)로만 스케일 — 도트가 뭉개지지 않는다.
    /// </summary>
    public static class UiKit
    {
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;

        // SNES풍 팔레트 (ART-DIRECTION 톤과 정합)
        public static readonly Color PanelBg = new Color(0.045f, 0.06f, 0.12f, 0.92f);
        public static readonly Color PanelBorder = new Color(0.42f, 0.55f, 0.85f, 1f);
        public static readonly Color TextMain = new Color(0.85f, 0.92f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.55f, 0.65f, 0.85f, 1f);
        public static readonly Color TextAccent = new Color(1f, 0.85f, 0.4f, 1f);
        public static readonly Color TextDanger = new Color(1f, 0.3f, 0.25f, 1f);

        /// <summary>터치 버튼 최소 변; 640×360 좌표 기준이라 실기기에서는 배율만큼 커진다.</summary>
        public const float MinTouchSize = 40f;

        public static readonly Color ButtonBg = new Color(0.16f, 0.26f, 0.48f, 0.92f);
        public static readonly Color ButtonBgAccent = new Color(0.26f, 0.42f, 0.72f, 0.95f);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<PixelUiScaler>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// UGUI 버튼은 EventSystem 없이는 클릭이 전달되지 않는다. 이 프로젝트는 씬을 코드로
        /// 조립하고 EventSystem을 두지 않았으므로, 캔버스를 만들 때 없으면 여기서 만든다.
        /// (씬 빌더에 의존하지 않아 Title/Battle 양쪽 모두 자동으로 갖춘다.)
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // 프로젝트가 Input System을 쓰므로 레거시 StandaloneInputModule이 아니라 이쪽이다.
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// 탭 가능한 버튼 (배경 + 라벨). 라벨은 <c>GetComponentInChildren&lt;Text&gt;()</c>로 갱신한다.
        /// </summary>
        public static Button CreateTouchButton(
            Transform parent, Font font, string label, int fontSize,
            Vector2 anchor, Vector2 offset, Vector2 size,
            UnityAction onClick, string name = "Button", bool accent = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = accent ? ButtonBgAccent : ButtonBg;
            image.raycastTarget = true;

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(
                Mathf.Max(size.x, MinTouchSize), Mathf.Max(size.y, MinTouchSize));

            var text = CreateText(rect, font, label, fontSize,
                accent ? TextAccent : TextMain, TextAnchor.MiddleCenter, "Label");
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2f, 2f);
            textRect.offsetMax = new Vector2(-2f, -2f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.72f, 0.78f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// 이미 만들어진 패널/이미지를 그대로 탭 대상으로 만든다 (보상·경로 선택 박스).
        /// 색 전환은 화면 쪽이 커서 표시로 직접 관리하므로 Transition은 끈다.
        /// </summary>
        public static Button MakeTappable(Image target, UnityAction onClick)
        {
            if (target == null) return null;
            target.raycastTarget = true;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = target;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>부모 전체를 덮는 스트레치 이미지 (딤/배경).</summary>
        public static Image CreateDim(Transform parent, Color color, string name = "Dim")
        {
            var image = CreateImage(parent, name, color);
            Stretch(image.rectTransform, Vector2.zero, Vector2.zero);
            return image;
        }

        /// <summary>중앙 앵커 패널: 단색 배경 + 1px 보더 프레임.</summary>
        public static RectTransform CreatePanel(Transform parent, Vector2 size, string name = "Panel")
        {
            var border = CreateImage(parent, name, PanelBorder);
            var rect = border.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var inner = CreateImage(rect, "Bg", PanelBg);
            Stretch(inner.rectTransform, Vector2.one, -Vector2.one);   // 1px 보더
            return rect;
        }

        public static Text CreateText(
            Transform parent, Font font, string content, int size,
            Color color, TextAnchor alignment, string name = "Text")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>스트레치 텍스트 (부모에 꽉 참, padding 픽셀).</summary>
        public static Text CreateTextStretch(
            Transform parent, Font font, string content, int size,
            Color color, TextAnchor alignment, float padding = 6f, string name = "Text")
        {
            var text = CreateText(parent, font, content, size, color, alignment, name);
            Stretch(text.rectTransform, new Vector2(padding, padding), new Vector2(-padding, -padding));
            return text;
        }

        /// <summary>코너 오프셋 배치 텍스트 (HUD용). anchor = (0,1)=좌상단 등.</summary>
        public static Text CreateCornerText(
            Transform parent, Font font, string content, int size,
            Color color, Vector2 anchor, Vector2 offset, TextAnchor alignment, string name = "Text")
        {
            var text = CreateText(parent, font, content, size, color, alignment, name);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(300f, 24f);
            return text;
        }

        /// <summary>픽셀풍 드롭 섀도 (키아트 위 텍스트 가독성용).</summary>
        public static void AddShadow(Graphic graphic, float distance = 2f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    /// <summary>
    /// CanvasScaler를 640×360 기준 정수 배율로 유지한다.
    /// ScaleWithScreenSize의 비정수 배율은 픽셀 폰트를 뭉갠다 — floor(min(w/640, h/360)).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelUiScaler : MonoBehaviour
    {
        CanvasScaler _scaler;
        int _lastWidth, _lastHeight;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
                Apply();
        }

        void Apply()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_scaler == null) return;
            int scale = Mathf.Max(1, Mathf.Min(
                _lastWidth / UiKit.ReferenceWidth, _lastHeight / UiKit.ReferenceHeight));
            _scaler.scaleFactor = scale;
        }
    }
}
