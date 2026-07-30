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

        /// <summary>
        /// 적립 중인 다음 레벨의 핍 색. 레벨업 비용이 레벨에 따라 늘어난 뒤로
        /// (REQ-053) 활성화 한 번이 레벨을 올리지 못하는 경우가 정상이 됐다.
        /// 적립 상황을 보여 주지 않으면 "눌렀는데 아무 일도 없다"로 읽힌다.
        /// </summary>
        static readonly Color32 PipBanking = new Color32(0x4E, 0x7A, 0xB8, 0xFF);
        static readonly Color32 PipBankingNear = new Color32(0x86, 0xB6, 0xF0, 0xFF);

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

                // 다음 레벨을 향한 적립 비율. GetProgress는 적립된 캡슐 수이고
                // 필요량은 레벨에 따라 달라지므로 여기서 비율로 환산한다.
                int required = gauge.GetRequiredCapsules((PowerUpSlot)slot);
                float banked = required > 0
                    ? gauge.GetProgress((PowerUpSlot)slot) / (float)required
                    : 0f;

                for (int pip = 0; pip < MaxPipsPerSlot; pip++)
                {
                    int index = slot * MaxPipsPerSlot + pip;
                    if (index >= _pips.Length) break;

                    bool withinMax = pip < maxLevel;
                    _pips[index].enabled = withinMax;
                    if (!withinMax) continue;

                    if (pip < level)
                        _pips[index].color = PipFilled;
                    else if (pip == level && banked > 0.001f)
                        _pips[index].color = banked >= 0.5f ? PipBankingNear : PipBanking;
                    else
                        _pips[index].color = PipEmpty;
                }
            }
        }
    }
}
