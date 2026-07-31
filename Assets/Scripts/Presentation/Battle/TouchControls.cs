using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 모바일 터치 조작 (원격 플레이). 가상 스틱이 아니라 **손가락 이동량이 그대로 기체
    /// 이동량이 되는** 방식 — 도돈파치 iOS 계열이 부드럽게 느껴지는 이유가 이것이다.
    ///
    /// 이전에는 "짚은 지점"을 목표로 두고 8방향 디지털로 추적했다. 시뮬이 항상 최대 속도로만
    /// 움직였기 때문에 느린 드래그에서는 기체가 먼저 도착해 멈췄다가 다시 출발하는
    /// stop-go 왕복이 생겼고, 그게 떨림으로 보였다. 이제는 프레임 간 손가락 이동량을 모아
    /// 정수 서브유닛 델타로 넘긴다 (REQ-045). 손가락이 멈추면 델타가 0이라 기체도 정확히
    /// 멈추고, 부호가 뒤집힐 여지가 없다. 이동 속도는 Core에서 **델타 클램프 상한**으로
    /// 쓰이므로 스피드업은 "손가락을 얼마나 빨리 따라오는가"로 체감된다.
    ///
    /// - 발사는 오토파이어(모바일 기본 ON)가 처리한다. 끄면 드래그 중 자동 발사.
    /// - 우상단 버튼: 게이지 활성화. 그 아래 작은 버튼: 오토파이어 토글.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchControls : MonoBehaviour
    {
        /// <summary>Core의 아날로그 델타 단위 (1 서브유닛 = 1/256 world unit).</summary>
        const float SubUnitsPerWorldUnit = 256f;

        /// <summary>Pointer 폴백이 쓰는 가상 손가락 번호 (실제 손가락 인덱스와 겹치지 않게).</summary>
        const int PointerFingerId = -100;

        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] bool _forceShow;

        [Tooltip("입력 경로 진단 오버레이. 조작 문제를 다시 추적할 때만 켠다.")]
        [SerializeField] bool _showInputDebug;

        Text _debugText;

        GameObject _root;
        RectTransform _activateButton;
        [SerializeField] Sprite _selectIcon;   // 캡슐 스프라이트 — 게이지 활성화의 시각 언어
        Camera _camera;

        int _dragFinger = -1;
        Vector2 _lastFingerWorld;
        Vector2 _pendingDeltaWorld;   // 아직 시뮬에 넘기지 않은 손가락 이동량
        bool _dragging;
        bool _activatePressed;
        bool _enabledForDevice;
        readonly System.Collections.Generic.List<RectTransform> _reserved =
            new System.Collections.Generic.List<RectTransform>(4);

        public static TouchControls Instance { get; private set; }

        public bool Active => _root != null && _root.activeSelf;

        /// <summary>손가락이 화면에 닿아 끌고 있는 중인지. 이때만 아날로그 경로를 쓴다.</summary>
        public bool IsDragging => _dragging;

        /// <summary>드래그 중이면 발사 (오토파이어가 꺼져 있을 때의 보조 발사).</summary>
        public bool Fire => _dragging;

        /// <summary>
        /// 모아 둔 손가락 이동량을 정수 서브유닛 델타로 꺼낸다 (소비형).
        ///
        /// 잔차를 남기는 것이 중요하다 — 느린 드래그는 한 틱 이동량이 1 서브유닛에 못 미치는데,
        /// 그때마다 버리면 조금씩 끄는 조작이 아예 먹지 않는다.
        /// </summary>
        /// <summary>진단용 누적 델타 (한 번이라도 0이 아닌 값이 나갔는지 확인).</summary>
        int _debugSumDx, _debugSumDy;

        public void ConsumeAnalogDelta(out int deltaX, out int deltaY)
        {
            float x = _pendingDeltaWorld.x * SubUnitsPerWorldUnit;
            float y = _pendingDeltaWorld.y * SubUnitsPerWorldUnit;
            deltaX = (int)x;   // 0 방향으로 절단
            deltaY = (int)y;
            _debugSumDx += deltaX;
            _debugSumDy += deltaY;
            _pendingDeltaWorld = new Vector2(
                (x - deltaX) / SubUnitsPerWorldUnit,
                (y - deltaY) / SubUnitsPerWorldUnit);
        }

        public bool ConsumeActivate()
        {
            bool value = _activatePressed;
            _activatePressed = false;
            return value;
        }

        /// <summary>
        /// 다른 캔버스에 있는 UI 버튼 영역을 드래그 판정에서 빼 달라고 등록한다.
        /// 이게 없으면 그 버튼을 누르려는 터치가 기체 드래그로도 해석되고, 반대로
        /// 기체를 그 근처로 옮기려다 버튼을 눌러 버린다.
        /// </summary>
        public void ReserveRect(RectTransform rect)
        {
            if (rect != null && !_reserved.Contains(rect)) _reserved.Add(rect);
        }

        bool HitReserved(Vector2 screen)
        {
            for (int i = 0; i < _reserved.Count; i++)
                if (HitButton(_reserved[i], screen)) return true;
            return false;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (_forceShow) UiPlatform.ForceTouch = true;
            bool touchDevice = UiPlatform.TouchMode;

            var canvas = UiKit.CreateCanvas("TouchCanvas", 95);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;
            _camera = Camera.main;

            // SELECT = 게이지 활성화. "X"라는 정체불명 글자 대신 캡슐 아이콘 + 라벨
            // ("Bomb, Select을 이쁘게 누르기 편한 아이콘으로", 2026-07-31).
            // 오토샷 토글은 없앴다 — 발사는 항상 자동이다 ("늘 쏴야하니까").
            _activateButton = CreateIconButton(canvas.transform, "Select", "SELECT",
                _selectIcon, new Vector2(1f, 1f), new Vector2(-56f, -56f), 72f);

            // 진단 오버레이 (REQ-045 조작 회귀 추적용 — 원인 확정 후 제거한다).
            // 폰에서 무엇이 들어오는지 눈으로 확인할 방법이 없어서 화면에 직접 찍는다.
            if (touchDevice && _showInputDebug)
            {
                _debugText = UiKit.CreateCornerText(canvas.transform, _font, "", 9,
                    new Color(0.6f, 1f, 0.7f, 0.95f), new Vector2(0f, 0f),
                    new Vector2(6f, 6f), TextAnchor.LowerLeft, "InputDebug");
                _debugText.rectTransform.sizeDelta = new Vector2(620f, 60f);
            }

            // 조작 안내는 OnboardingHints가 기기에 맞는 문면으로 띄운다 — 여기서 또 적지 않는다.
            _enabledForDevice = touchDevice;
            _root.SetActive(touchDevice);
            if (touchDevice) EnhancedTouchSupport.Enable();
        }

        /// <summary>
        /// 메뉴(일시정지·보상·경로·게임오버)가 떠 있는 동안은 조작 오버레이를 걷는다.
        /// 그러지 않으면 메뉴 버튼을 누르려는 터치가 기체 드래그로 해석되고,
        /// X/AUTO 버튼이 메뉴 위에 겹쳐 눌린다.
        ///
        /// BombButton도 이 기준을 따른다 — 조작 오버레이는 걷혔는데 폭탄 버튼만 남으면
        /// 메뉴 위에 떠서 오폭한다.
        /// </summary>
        public bool GameplayActive =>
            Time.timeScale > 0f
            && !OptionsScreen.IsOpen
            && (_director == null
                || (!_director.AwaitingReward && !_director.AwaitingRoute
                    && !_director.AwaitingContract && !_director.IsRunFinished));

        void Update()
        {
            if (_root == null || !_enabledForDevice) return;

            bool shouldShow = GameplayActive;
            if (_root.activeSelf != shouldShow)
            {
                _root.SetActive(shouldShow);
                if (!shouldShow)
                {
                    // 메뉴로 넘어가는 순간의 입력이 남아 기체가 계속 움직이지 않도록 정리한다.
                    _dragging = false;
                    _dragFinger = -1;
                    _pendingDeltaWorld = Vector2.zero;
                    _activatePressed = false;
                }
            }
            if (!shouldShow) return;
            if (_camera == null) _camera = Camera.main;

            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            bool dragActive = false;
            bool sawAnyTouch = touches.Count > 0;

            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                Vector2 screen = touch.screenPosition;

                // 버튼 영역 처리 (탭)
                if (touch.began)
                {
                    if (HitButton(_activateButton, screen)) { _activatePressed = true; continue; }
                }
                else if (HitButton(_activateButton, screen))
                {
                    continue;   // 버튼 위에서 끌어도 기체가 끌려가지 않게
                }

                // 다른 캔버스의 버튼(일시정지 등)은 UGUI가 처리한다 — 드래그로 겹쳐 읽지 않는다.
                if (HitReserved(screen)) continue;

                if (AccumulateDrag(screen, touch.finger.index)) dragActive = true;
            }

            // EnhancedTouch가 비었을 때의 폴백.
            //
            // EnhancedTouch는 WebGL에서 Touchscreen 디바이스가 늦게 붙으면 조용히 빈다.
            // 그리고 `Pointer.current`는 "가장 최근에 쓰인" 포인터를 가리키므로, 터치
            // 디바이스가 존재하기만 해도 마우스 대신 그쪽을 가리켜 position이 갱신되지
            // 않는다 (실제로 이 어긋남 때문에 델타가 계속 0이었다). 그래서 어느 디바이스가
            // 눌려 있는지 저수준 API로 직접 확인한다.
            if (!sawAnyTouch)
            {
                Vector2 screen = Vector2.zero;
                bool pressed = false;
                bool justPressed = false;

                var screenDevice = Touchscreen.current;
                if (screenDevice != null)
                {
                    var primary = screenDevice.primaryTouch;
                    if (primary.press.isPressed)
                    {
                        screen = primary.position.ReadValue();
                        pressed = true;
                        justPressed = primary.press.wasPressedThisFrame;
                    }
                }
                if (!pressed)
                {
                    var mouse = Mouse.current;
                    if (mouse != null && mouse.leftButton.isPressed)
                    {
                        screen = mouse.position.ReadValue();
                        pressed = true;
                        justPressed = mouse.leftButton.wasPressedThisFrame;
                    }
                }

                if (pressed)
                {
                    bool onOwnButton = HitButton(_activateButton, screen);
                    if (justPressed && onOwnButton) _activatePressed = true;
                    if (!onOwnButton && !HitReserved(screen)
                        && AccumulateDrag(screen, PointerFingerId))
                        dragActive = true;
                }
            }

            if (_debugText != null)
            {
                var ts = Touchscreen.current;
                var ms = Mouse.current;
                string ptrState =
                    (ts != null && ts.primaryTouch.press.isPressed) ? "TOUCH"
                    : (ms != null && ms.leftButton.isPressed) ? "MOUSE" : "up";
                var ship = _director != null ? _director.PlayerWorldPosition : Vector2.zero;
                Vector2 rawPos = ms != null ? ms.position.ReadValue() : new Vector2(-1f, -1f);
                if (ts != null && ts.primaryTouch.press.isPressed)
                    rawPos = ts.primaryTouch.position.ReadValue();
                Vector2 rawWorld = ScreenToWorld(rawPos);
                _debugText.text =
                    $"ptr={ptrState} drag={dragActive} touches={touches.Count} "
                    + $"raw=({rawPos.x:0},{rawPos.y:0}) w=({rawWorld.x:0.0},{rawWorld.y:0.0}) "
                    + $"last=({_lastFingerWorld.x:0.0},{_lastFingerWorld.y:0.0}) "
                    + $"scr={Screen.width}x{Screen.height}\n"
                    + $"ship=({ship.x:0.0},{ship.y:0.0}) sum=({_debugSumDx},{_debugSumDy}) "
                    + PlayerInputReader.DebugState;
            }

            _dragging = dragActive;
            if (!dragActive)
            {
                _dragFinger = -1;
                // 손을 떼면 남은 잔차도 버린다 — 놓은 뒤에 기체가 조금 더 미끄러지면
                // "놓았는데 움직인다"로 읽힌다.
                _pendingDeltaWorld = Vector2.zero;
            }
        }

        /// <summary>
        /// 한 입력원의 화면 좌표를 받아 이동량을 누적한다. 짚은 위치가 아니라 **움직인 양**이
        /// 시뮬에 전달되므로, 처음 닿은 프레임은 기준점만 잡고 0을 남긴다 (기체가 손가락
        /// 쪽으로 순간이동하지 않게).
        /// </summary>
        bool AccumulateDrag(Vector2 screen, int fingerId)
        {
            if (_dragFinger != -1 && _dragFinger != fingerId) return false;

            Vector2 world = ScreenToWorld(screen);
            if (_dragFinger == -1) _dragFinger = fingerId;
            else _pendingDeltaWorld += world - _lastFingerWorld;
            _lastFingerWorld = world;
            return true;
        }

        Vector2 ScreenToWorld(Vector2 screen)
        {
            if (_camera == null) return Vector2.zero;
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            return new Vector2(world.x, world.y);
        }

        bool HitButton(RectTransform button, Vector2 screen)
        {
            if (button == null) return false;
            // 캔버스가 ScreenSpaceOverlay + 정수 배율이므로 화면 좌표로 직접 판정
            var corners = new Vector3[4];
            button.GetWorldCorners(corners);
            return screen.x >= corners[0].x && screen.x <= corners[2].x
                && screen.y >= corners[0].y && screen.y <= corners[2].y;
        }


        /// <summary>
        /// 아이콘 + 라벨의 원형 느낌 버튼. 글자 한 칸짜리 반투명 사각형보다 "누르는
        /// 것"으로 읽히고, 아이콘이 게임 내 오브젝트(캡슐/폭탄)와 같은 그림이라
        /// 기능 연결이 설명 없이 된다.
        /// </summary>
        RectTransform CreateIconButton(
            Transform parent, string name, string label, Sprite icon,
            Vector2 anchor, Vector2 offset, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            // 공용 계기판 셀 — 플레이 시야를 가리지 않게 반투명 틴트만 다르다
            image.sprite = UiSkin.Button;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.34f, 0.385f, 0.435f, 0.85f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(size, size);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(rect, false);
                var iconImage = iconGo.AddComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                var iconRect = iconImage.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.58f);
                iconRect.sizeDelta = new Vector2(size * 0.42f, size * 0.42f);
            }

            var text = UiKit.CreateText(rect, _font, label, 9,
                UiKit.TextDim, TextAnchor.LowerCenter, "Label");
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0f, 5f);
            textRect.offsetMax = Vector2.zero;
            return rect;
        }

    }
}
