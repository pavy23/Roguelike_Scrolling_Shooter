using Shmup.Core;
using Shmup.Core.Content;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 타이틀 화면 격납고 (함선 해금형 메타, 2026-07-29 사람 확정).
    /// ←/→ 함선 순환, U 해금, 선택은 즉시 저장 — 전투 씬이 저장을 읽어 함선을 적용한다.
    /// 해금/선택 규칙은 전부 Core MetaState 소관, 여기는 입출력만.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HangarScreen : MonoBehaviour
    {
        GameDataSet _data;
        MetaState _meta;
        int _cursor;
        GUIStyle _headerStyle, _bodyStyle;

        void Start()
        {
            _data = GameDataParser.Parse(
                LoadText("enemies"), LoadText("weapons"), LoadText("waves"),
                TryLoadText("rewards"), TryLoadText("ships"));
            _meta = MetaSave.Load(_data);
            for (int i = 0; i < _data.Ships.Count; i++)
                if (_data.Ships[i].Id == _meta.SelectedShipId)
                    _cursor = i;
        }

        void Update()
        {
            if (_data == null || _meta == null || _data.Ships.Count == 0) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                _cursor = (_cursor - 1 + _data.Ships.Count) % _data.Ships.Count;
            if (keyboard.rightArrowKey.wasPressedThisFrame)
                _cursor = (_cursor + 1) % _data.Ships.Count;

            var ship = _data.Ships[_cursor];
            if (keyboard.uKey.wasPressedThisFrame && !_meta.IsUnlocked(ship.Id))
            {
                if (_meta.TryUnlock(ship))
                    MetaSave.Save(_meta);
            }
            if (_meta.IsUnlocked(ship.Id) && _meta.SelectedShipId != ship.Id)
            {
                _meta.SelectShip(ship.Id);
                MetaSave.Save(_meta);
            }
        }

        void OnGUI()
        {
            if (_data == null || _meta == null || _data.Ships.Count == 0) return;
            EnsureStyles();

            float width = Screen.width, height = Screen.height;
            var ship = _data.Ships[_cursor];
            bool unlocked = _meta.IsUnlocked(ship.Id);

            GUI.Label(
                new Rect(0, height * 0.855f, width, 26f),
                $"HANGAR  ◄ {_cursor + 1}/{_data.Ships.Count} ►      CREDIT {_meta.TotalCurrency:N0}",
                _headerStyle);

            string status = unlocked
                ? (_meta.SelectedShipId == ship.Id ? "[SELECTED]" : "[OWNED]")
                : $"[LOCKED — {ship.UnlockCost:N0} cr, U to unlock]";
            var levels = ship.StartingPowerUpLevels;
            GUI.Label(
                new Rect(0, height * 0.90f, width, 60f),
                $"{ship.DisplayName}  {status}\n" +
                $"speed x{(float)ship.MoveSpeedMultiplierNumerator / ship.MoveSpeedMultiplierDenominator:0.##}   " +
                $"start S{levels[0]} M{levels[1]} O{levels[2]} B{levels[3]}",
                _bodyStyle);
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.9f, 0.6f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.85f, 1f) }
            };
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
