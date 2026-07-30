using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 전멸 폭탄 발동 버튼 + 재고 표시.
    ///
    /// Core는 REQ-046부터 폭탄을 완전히 지원했지만(<c>InputCommand.ActivateBomb</c>,
    /// 재고, 무적, 데미지) Presentation이 없어서 **발동할 방법이 아예 없었다.**
    /// 사람이 요청한 "전멸폭탄 스톡모았다가 쓰기"의 마지막 조각이다.
    ///
    /// 재고가 0일 때도 탭을 받는다 — 눌렀는데 아무 반응이 없으면 버튼이 고장난 것처럼
    /// 느껴지므로, Core가 <c>BombActivationRejectedEmpty</c>를 내고 여기서 붉게 알린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombButton : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;

        public static BombButton Instance { get; private set; }

        Image _background;
        Text _label;
        bool _pressed;
        float _emptyFlash;
        int _shownStock = -1;

        static readonly Color Ready = new Color(0.62f, 0.16f, 0.55f, 0.85f);
        static readonly Color Empty = new Color(0.24f, 0.20f, 0.28f, 0.55f);
        static readonly Color EmptyFlash = new Color(1f, 0.28f, 0.24f, 0.95f);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            UiKit.EnsureEventSystem();

            var canvas = UiKit.CreateCanvas("BombCanvas", 46);
            canvas.transform.SetParent(transform, false);

            // 우하단 — 가로 모드에서 오른손 엄지가 닿는 자리다. 기체 드래그는 화면
            // 어디서나 시작되므로 버튼 영역은 아래에서 드래그 대상에서 뺀다.
            var button = UiKit.CreateTouchButton(
                canvas.transform, _font, "BOMB", 10,
                new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(52f, 44f),
                OnTap, "BombButton", accent: true);

            _background = button.targetGraphic as Image;
            _label = button.GetComponentInChildren<Text>();

            // 폭탄 버튼을 누르려다 기체가 끌려가면 최악이다 — 이 사각형은 드래그에서 뺀다.
            var touch = TouchControls.Instance;
            if (touch != null) touch.ReserveRect(button.GetComponent<RectTransform>());
        }

        void OnTap() => _pressed = true;

        /// <summary>
        /// 이번 프레임의 탭을 소비한다. PlayerInputReader가 매 프레임 한 번만 호출해야
        /// 한다 — 두 번 호출하면 두 번째는 항상 false가 된다.
        /// </summary>
        public bool ConsumePress()
        {
            bool pressed = _pressed;
            _pressed = false;
            return pressed;
        }

        /// <summary>재고 없이 눌렀음을 알린다 (BattleDirector가 Core 이벤트로 호출).</summary>
        public void FlashEmpty() => _emptyFlash = 0.35f;

        void Update()
        {
            if (_director == null || _background == null) return;

            // 메뉴·보상 화면에서는 숨긴다. TouchControls와 같은 기준을 쓴다.
            var touch = TouchControls.Instance;
            bool visible = touch == null || touch.GameplayActive;
            if (_background.transform.parent.gameObject.activeSelf != visible)
                _background.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;

            int stock = _director.BombStock;
            if (stock != _shownStock)
            {
                _shownStock = stock;
                if (_label != null) _label.text = stock > 0 ? $"BOMB {stock}" : "BOMB 0";
            }

            if (_emptyFlash > 0f)
            {
                _emptyFlash -= Time.unscaledDeltaTime;
                _background.color = Color.Lerp(
                    Empty, EmptyFlash, Mathf.Clamp01(_emptyFlash * 3f));
                return;
            }

            // 재고가 있으면 자홍으로 켜 두고, 없으면 죽여서 "지금 쓸 수 있는가"가
            // 눈으로 바로 읽히게 한다.
            _background.color = stock > 0 ? Ready : Empty;
            if (_label != null)
                _label.color = stock > 0 ? Color.white : UiKit.TextDim;
        }
    }
}
