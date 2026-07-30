using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 모바일 터치 조작 (원격 플레이). 가상 스틱이 아니라 **기체를 손가락으로 직접 끌고 다니는**
    /// 방식 — 모바일 슈팅의 표준이고 스틱보다 정확하다.
    ///
    /// - 손가락을 짚은 지점으로 기체가 온다. 오프셋을 두지 않아서 "지금 어디를 잡고 있는지"가
    ///   손가락 위치와 일치한다 (오프셋을 유지하면 기체가 손에서 떨어져 있어 지연처럼 느껴진다).
    /// - 발사는 오토파이어(모바일 기본 ON)가 처리한다. 끄면 드래그 중 자동 발사.
    /// - 우상단 버튼: 게이지 활성화. 그 아래 작은 버튼: 오토파이어 토글.
    ///
    /// 시뮬은 8방향 디지털 InputCommand만 받으므로, 목표 지점 방향을 매 프레임 디지털로
    /// 변환해 넘긴다 — Core 변경이 없고 결정론에도 영향이 없다. 따라오는 속도는 시뮬의
    /// 이동 속도가 상한이라, 체감 지연을 더 줄이려면 그쪽 수치를 올려야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchControls : MonoBehaviour
    {
        // 정지 판정 반경. 넓으면 손가락을 따라오다 멈춰서 지연처럼 느껴지므로 좁게 잡는다.
        const float StopRadiusWorld = 0.05f;

        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] bool _forceShow;

        GameObject _root;
        Text _autoFireLabel;
        RectTransform _activateButton, _autoFireButton;
        Camera _camera;

        int _dragFinger = -1;
        Vector2 _targetWorld;
        bool _dragging;
        bool _activatePressed;
        bool _enabledForDevice;
        readonly System.Collections.Generic.List<RectTransform> _reserved =
            new System.Collections.Generic.List<RectTransform>(4);

        public static TouchControls Instance { get; private set; }

        public bool Active => _root != null && _root.activeSelf;

        /// <summary>목표 지점 방향의 8방향 디지털 이동 (-1/0/1).</summary>
        public Vector2 Move { get; private set; }

        /// <summary>드래그 중이면 발사 (오토파이어가 꺼져 있을 때의 보조 발사).</summary>
        public bool Fire => _dragging;

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

            _activateButton = CreateButton(canvas.transform, "ACTIVATE", "X",
                new Vector2(1f, 1f), new Vector2(-52f, -52f), 64f);
            _autoFireButton = CreateButton(canvas.transform, "AutoFire", "AUTO",
                new Vector2(1f, 1f), new Vector2(-52f, -126f), 56f);
            _autoFireLabel = _autoFireButton.GetComponentInChildren<Text>();

            // 조작 안내는 OnboardingHints가 기기에 맞는 문면으로 띄운다 — 여기서 또 적지 않는다.
            _enabledForDevice = touchDevice;
            _root.SetActive(touchDevice);
            if (touchDevice) EnhancedTouchSupport.Enable();
            RefreshAutoFireLabel();
        }

        /// <summary>
        /// 메뉴(일시정지·보상·경로·게임오버)가 떠 있는 동안은 조작 오버레이를 걷는다.
        /// 그러지 않으면 메뉴 버튼을 누르려는 터치가 기체 드래그로 해석되고,
        /// X/AUTO 버튼이 메뉴 위에 겹쳐 눌린다.
        /// </summary>
        bool GameplayActive =>
            Time.timeScale > 0f
            && !OptionsScreen.IsOpen
            && (_director == null
                || (!_director.AwaitingReward && !_director.AwaitingRoute
                    && !_director.IsRunFinished));

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
                    Move = Vector2.zero;
                    _dragging = false;
                    _dragFinger = -1;
                    _activatePressed = false;
                }
            }
            if (!shouldShow) return;
            if (_camera == null) _camera = Camera.main;

            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            bool dragActive = false;

            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                Vector2 screen = touch.screenPosition;

                // 버튼 영역 처리 (탭)
                if (touch.began)
                {
                    if (HitButton(_activateButton, screen)) { _activatePressed = true; continue; }
                    if (HitButton(_autoFireButton, screen))
                    {
                        PlayerInputReader.SetAutoFire(!PlayerInputReader.AutoFire);
                        RefreshAutoFireLabel();
                        continue;
                    }
                }
                else if (HitButton(_activateButton, screen) || HitButton(_autoFireButton, screen))
                {
                    continue;   // 버튼 위에서 끌어도 기체가 끌려가지 않게
                }

                // 다른 캔버스의 버튼(일시정지 등)은 UGUI가 처리한다 — 드래그로 겹쳐 읽지 않는다.
                if (HitReserved(screen)) continue;

                // 드래그 이동 — 짚은 지점이 그대로 목표다 (오프셋 없음)
                if (_dragFinger == -1 || _dragFinger == touch.finger.index)
                {
                    if (_dragFinger == -1) _dragFinger = touch.finger.index;
                    _targetWorld = ScreenToWorld(screen);
                    dragActive = true;
                }
            }

            _dragging = dragActive;
            if (!dragActive)
            {
                _dragFinger = -1;
                Move = Vector2.zero;
                return;
            }

            // 목표 방향 → 8방향 디지털
            Vector2 delta = _targetWorld - PlayerWorld();
            Move = new Vector2(
                Mathf.Abs(delta.x) < StopRadiusWorld ? 0f : Mathf.Sign(delta.x),
                Mathf.Abs(delta.y) < StopRadiusWorld ? 0f : Mathf.Sign(delta.y));
        }

        Vector2 PlayerWorld()
        {
            if (_director == null) return Vector2.zero;
            return _director.PlayerWorldPosition;
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

        void RefreshAutoFireLabel()
        {
            if (_autoFireLabel == null) return;
            _autoFireLabel.text = PlayerInputReader.AutoFire ? "AUTO\nON" : "AUTO\nOFF";
            _autoFireLabel.color = PlayerInputReader.AutoFire
                ? UiKit.TextAccent : new Color(0.6f, 0.7f, 0.85f, 0.8f);
        }

        RectTransform CreateButton(
            Transform parent, string name, string label,
            Vector2 anchor, Vector2 offset, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.35f, 0.55f, 0.95f, 0.22f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(size, size);

            var text = UiKit.CreateText(rect, _font, label, 11,
                UiKit.TextMain, TextAnchor.MiddleCenter, "Label");
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
