using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 모바일 터치 조작 (원격 플레이용). 왼쪽 절반 = 가상 스틱(이동),
    /// 오른쪽 = 발사(홀드) + 게이지 활성화 버튼. 상단 우측 = 일시정지.
    ///
    /// 시뮬은 InputCommand만 받으므로 여기서 8방향 디지털로 변환해 PlayerInputReader에
    /// 주입한다 — 결정론에는 영향이 없다(입력원만 다름).
    /// 터치 지원 기기에서만 UI를 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchControls : MonoBehaviour
    {
        const float StickRadiusPixels = 110f;
        const float DeadZone = 0.28f;

        [SerializeField] Font _font;
        [SerializeField] bool _forceShow;   // 에디터 확인용

        GameObject _root;
        Image _stickBase, _stickKnob;
        Vector2 _stickOrigin;
        int _stickFingerId = -1;
        bool _fireHeld;
        bool _activatePressed;
        RectTransform _canvasRect;
        float _scale = 1f;

        public static TouchControls Instance { get; private set; }

        /// <summary>터치 UI가 켜져 있는가 (PlayerInputReader가 입력원 결정에 쓴다).</summary>
        public bool Active => _root != null && _root.activeSelf;

        public Vector2 Move { get; private set; }
        public bool Fire => _fireHeld;

        public bool ConsumeActivate()
        {
            bool value = _activatePressed;
            _activatePressed = false;
            return value;
        }

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            bool touchDevice = _forceShow
                || Application.isMobilePlatform
                || (Touchscreen.current != null && !Application.isEditor);

            var canvas = UiKit.CreateCanvas("TouchCanvas", 95);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;
            _canvasRect = canvas.GetComponent<RectTransform>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) _scale = Mathf.Max(1f, scaler.scaleFactor);

            _stickBase = CreateCircle(canvas.transform, "StickBase", 128f,
                new Color(0.5f, 0.7f, 1f, 0.16f));
            _stickKnob = CreateCircle(canvas.transform, "StickKnob", 56f,
                new Color(0.7f, 0.85f, 1f, 0.4f));
            _stickBase.enabled = false;
            _stickKnob.enabled = false;

            CreateHint(canvas.transform, "MOVE", new Vector2(0f, 0f), new Vector2(96f, 60f));
            CreateHint(canvas.transform, "FIRE", new Vector2(1f, 0f), new Vector2(-120f, 60f));
            CreateHint(canvas.transform, "X", new Vector2(1f, 0f), new Vector2(-44f, 132f));

            _root.SetActive(touchDevice);
            if (touchDevice) EnhancedTouchSupport.Enable();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_root == null || !_root.activeSelf) return;

            Move = Vector2.zero;
            _fireHeld = false;
            bool stickActive = false;

            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                Vector2 position = touch.screenPosition;
                bool leftHalf = position.x < Screen.width * 0.5f;

                if (leftHalf)
                {
                    // 가상 스틱: 처음 닿은 지점이 중심
                    if (_stickFingerId == -1 || _stickFingerId == touch.finger.index)
                    {
                        if (_stickFingerId == -1) _stickOrigin = position;
                        _stickFingerId = touch.finger.index;
                        stickActive = true;
                        Vector2 delta = position - _stickOrigin;
                        float radius = StickRadiusPixels * _scale;
                        Move = Vector2.ClampMagnitude(delta / radius, 1f);
                        UpdateStickVisual(_stickOrigin, position, radius);
                    }
                }
                else
                {
                    // 오른쪽: 상단 1/4은 활성화, 나머지는 발사 홀드
                    if (position.y > Screen.height * 0.72f)
                    {
                        if (touch.began) _activatePressed = true;
                    }
                    else
                    {
                        _fireHeld = true;
                    }
                }
            }

            if (!stickActive)
            {
                _stickFingerId = -1;
                _stickBase.enabled = false;
                _stickKnob.enabled = false;
            }
        }

        void UpdateStickVisual(Vector2 origin, Vector2 current, float radius)
        {
            _stickBase.enabled = true;
            _stickKnob.enabled = true;
            _stickBase.rectTransform.anchoredPosition = origin / _scale;
            Vector2 knob = origin + Vector2.ClampMagnitude(current - origin, radius);
            _stickKnob.rectTransform.anchoredPosition = knob / _scale;
        }

        Image CreateCircle(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = CircleSprite();
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return image;
        }

        void CreateHint(Transform parent, string label, Vector2 anchor, Vector2 offset)
        {
            var text = UiKit.CreateCornerText(parent, _font, label, 11,
                new Color(0.7f, 0.85f, 1f, 0.45f), anchor, offset,
                TextAnchor.MiddleCenter, $"Hint_{label}");
            text.rectTransform.sizeDelta = new Vector2(90f, 20f);
        }

        static Sprite _circle;

        static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01((r - d) / 2f);
                // 링 형태로: 가장자리만 진하게
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 4f)) / 5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(a * 0.25f, ring)));
            }
            texture.Apply();
            _circle = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
            return _circle;
        }
    }
}
