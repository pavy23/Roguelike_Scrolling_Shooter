using Shmup.Core;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 그라디우스식 파워업 게이지 HUD. 화면 하단에 슬롯 4개(기본탄/미사일/옵션/실드)와
    /// 슬롯별 레벨 핍을 그린다. Core의 PowerUpGauge 상태를 읽어 색만 바꾼다 —
    /// 게이지 조작은 어디서도 하지 않는다 (Presentation은 그리기만, CLAUDE.md).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerUpHudView : MonoBehaviour
    {
        public const int SlotCount = PowerUpGauge.SlotCount;
        public const int MaxPipsPerSlot = 5;

        [SerializeField] BattleDirector _director;
        [SerializeField] SpriteRenderer[] _slotFrames;   // SlotCount개
        [SerializeField] SpriteRenderer[] _pips;         // SlotCount * MaxPipsPerSlot, 슬롯 우선 평탄화

        static readonly Color32 FrameNormal = new Color32(0x4A, 0x5A, 0x7A, 0xFF);
        static readonly Color32 FrameHighlight = new Color32(0xFF, 0xE0, 0x8C, 0xFF);
        static readonly Color32 FrameMaxed = new Color32(0x7A, 0xE0, 0x9C, 0xFF);
        static readonly Color32 PipFilled = new Color32(0x9C, 0xD4, 0xFF, 0xFF);
        static readonly Color32 PipEmpty = new Color32(0x22, 0x2C, 0x44, 0xFF);

        void LateUpdate()
        {
            var gauge = _director != null ? _director.Gauge : null;
            if (gauge == null || _slotFrames == null || _pips == null) return;

            for (int slot = 0; slot < SlotCount && slot < _slotFrames.Length; slot++)
            {
                int level = gauge.GetLevel((PowerUpSlot)slot);
                int maxLevel = gauge.GetMaxLevel((PowerUpSlot)slot);

                _slotFrames[slot].color =
                    gauge.Cursor == slot ? FrameHighlight :
                    level >= maxLevel ? FrameMaxed : FrameNormal;

                for (int pip = 0; pip < MaxPipsPerSlot; pip++)
                {
                    int index = slot * MaxPipsPerSlot + pip;
                    if (index >= _pips.Length) break;

                    bool withinMax = pip < maxLevel;
                    _pips[index].enabled = withinMax;
                    if (withinMax)
                        _pips[index].color = pip < level ? PipFilled : PipEmpty;
                }
            }
        }
    }
}
