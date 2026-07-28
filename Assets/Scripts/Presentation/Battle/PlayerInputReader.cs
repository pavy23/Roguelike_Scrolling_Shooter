using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// Input System에서 입력을 **샘플링만** 한다. 이동량·발사 가능 여부 같은 판정은
    /// 전부 Shmup.Core가 하고, 여기서는 한 틱 분량의 InputCommand로 포장해 넘긴다.
    ///
    /// 샘플링은 Update(가변 프레임), 소비는 BattleDirector.FixedUpdate(고정 60Hz)에서 일어난다.
    /// 한 프레임 안에 FixedUpdate가 0번 도는 경우에도 눌림이 유실되지 않도록
    /// "이번 프레임에 눌렸다"를 래치해 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset _actions;
        [SerializeField] string _actionMapName = "Player";
        [SerializeField] string _moveActionName = "Move";
        [SerializeField] string _fireActionName = "Attack";

        [Tooltip("아날로그 스틱을 디지털 8방향으로 바꿀 때의 임계값.")]
        [SerializeField, Range(0.05f, 0.95f)] float _deadZone = 0.4f;

        /// <summary>옵션 화면(리바인딩)용 읽기 접근자.</summary>
        public InputActionAsset Actions => _actions;
        public string FireActionName => _fireActionName;

        InputAction _moveAction;
        InputAction _fireAction;

        Vector2 _move;
        bool _fireHeld;
        bool _firePressedThisFrame;

        void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] InputActionAsset이 비어 있다. " +
                               "인스펙터에서 Assets/Settings/InputSystem_Actions를 지정해라.");
                enabled = false;
                return;
            }

            var map = _actions.FindActionMap(_actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] 액션 맵 '{_actionMapName}'을 찾을 수 없다.");
                enabled = false;
                return;
            }

            _moveAction = map.FindAction(_moveActionName, throwIfNotFound: false);
            _fireAction = map.FindAction(_fireActionName, throwIfNotFound: false);
            if (_moveAction == null || _fireAction == null)
            {
                Debug.LogError($"[{nameof(PlayerInputReader)}] '{_moveActionName}' 또는 " +
                               $"'{_fireActionName}' 액션이 없다.");
                enabled = false;
            }
        }

        void OnEnable()
        {
            _moveAction?.Enable();
            _fireAction?.Enable();
        }

        void OnDisable()
        {
            _moveAction?.Disable();
            _fireAction?.Disable();
            _move = Vector2.zero;
            _fireHeld = false;
            _firePressedThisFrame = false;
        }

        void Update()
        {
            _move = _moveAction.ReadValue<Vector2>();
            _fireHeld = _fireAction.IsPressed();
            if (_fireAction.WasPressedThisFrame()) _firePressedThisFrame = true;
        }

        /// <summary>한 틱 분량의 입력을 만들어 반환하고 눌림 래치를 소모한다.</summary>
        public InputCommand ConsumeCommand()
        {
            if (!enabled) return InputCommand.None;

            var command = new InputCommand(Digital(_move.x), Digital(_move.y),
                                           _fireHeld || _firePressedThisFrame);
            _firePressedThisFrame = false;
            return command;
        }

        int Digital(float axis)
        {
            if (axis <= -_deadZone) return -1;
            if (axis >= _deadZone) return 1;
            return 0;
        }
    }
}
