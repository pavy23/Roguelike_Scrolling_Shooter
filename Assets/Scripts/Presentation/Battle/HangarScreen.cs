using Shmup.Core;
using Shmup.Core.Content;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 격납고 (UGUI, 함선 해금형 메타 — 2026-07-29 사람 확정).
    /// ←/→ 또는 패드 dpad 함선 순환, U/(Y) 해금, 선택은 즉시 저장 —
    /// 전투 씬이 저장을 읽어 함선을 적용한다. 해금/선택 규칙은 전부 Core MetaState 소관.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HangarScreen : MonoBehaviour
    {
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        GameDataSet _data;
        MetaState _meta;
        int _cursor;
        Text _headerText, _bodyText;
        int _shownCursor = -1;
        long _shownCurrency = -1;
        string _shownSelected;

        void Start()
        {
            _data = GameDataParser.Parse(
                LoadText("enemies"), LoadText("weapons"), LoadText("waves"),
                TryLoadText("rewards"), TryLoadText("ships"), TryLoadText("scoring"));
            _meta = MetaSave.Load(_data);
            for (int i = 0; i < _data.Ships.Count; i++)
                if (_data.Ships[i].Id == _meta.SelectedShipId)
                    _cursor = i;

            var canvas = UiKit.CreateCanvas("HangarCanvas", 55);
            canvas.transform.SetParent(transform, false);
            _headerText = UiKit.CreateCornerText(canvas.transform, _fontBold, "", 12,
                UiKit.TextAccent, new Vector2(0.5f, 0f), new Vector2(0f, 46f),
                TextAnchor.LowerCenter, "Header");
            _bodyText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextMain, new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                TextAnchor.LowerCenter, "Body");
        }

        void Update()
        {
            if (_data == null || _meta == null || _data.Ships.Count == 0) return;
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            int move = 0;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) move = -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame) move = 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame) move = -1;
                if (gamepad.dpad.right.wasPressedThisFrame) move = 1;
            }
            _cursor = (_cursor + move + _data.Ships.Count) % _data.Ships.Count;

            var ship = _data.Ships[_cursor];
            bool unlockPressed = (keyboard != null && keyboard.uKey.wasPressedThisFrame)
                              || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame);
            if (unlockPressed && !_meta.IsUnlocked(ship.Id))
            {
                if (_meta.TryUnlock(ship))
                    MetaSave.Save(_meta);
                _shownCursor = -1;   // 표시 갱신
            }
            if (_meta.IsUnlocked(ship.Id) && _meta.SelectedShipId != ship.Id)
            {
                _meta.SelectShip(ship.Id);
                MetaSave.Save(_meta);
            }

            RefreshTexts(ship);
        }

        void RefreshTexts(Core.ShipDefinition ship)
        {
            if (_headerText == null || _bodyText == null) return;
            if (_shownCursor == _cursor
                && _shownCurrency == _meta.TotalCurrency
                && _shownSelected == _meta.SelectedShipId) return;
            _shownCursor = _cursor;
            _shownCurrency = _meta.TotalCurrency;
            _shownSelected = _meta.SelectedShipId;

            _headerText.text =
                $"HANGAR  ◄ {_cursor + 1}/{_data.Ships.Count} ►      CREDIT {_meta.TotalCurrency:N0}";
            bool unlocked = _meta.IsUnlocked(ship.Id);
            string status = unlocked
                ? (_meta.SelectedShipId == ship.Id ? "[SELECTED]" : "[OWNED]")
                : $"[LOCKED — {ship.UnlockCost:N0} cr, U/(Y) to unlock]";
            var levels = ship.StartingPowerUpLevels;
            _bodyText.text =
                $"{ship.DisplayName}  {status}\n" +
                $"speed x{(float)ship.MoveSpeedMultiplierNumerator / ship.MoveSpeedMultiplierDenominator:0.##}   " +
                $"start S{levels[0]} M{levels[1]} O{levels[2]} B{levels[3]}";
        }

        static string LoadText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            if (asset == null)
                throw new System.InvalidOperationException($"Resources/GameData/{name} 없음 — 씬 재생성 필요.");
            return asset.text;
        }

        static string TryLoadText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            return asset != null ? asset.text : null;
        }
    }
}
