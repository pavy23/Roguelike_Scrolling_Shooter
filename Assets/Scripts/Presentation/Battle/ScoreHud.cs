using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>우상단 점수 표시 (UGUI + 픽셀 폰트). Core RunManager.TotalScore를 읽기만 한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreHud : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Font _fontBold;

        Text _text, _multiplierText;
        long _lastScore = long.MinValue;
        int _lastMultiplier = -1;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("ScoreCanvas", 40);
            canvas.transform.SetParent(transform, false);
            _text = UiKit.CreateCornerText(canvas.transform, _fontBold, "00000000", 16,
                UiKit.TextAccent, new Vector2(1f, 1f), new Vector2(-8f, -4f),
                TextAnchor.UpperRight, "Score");
            // 우상단 정렬 + 우측 피벗이라 "x32"처럼 자릿수가 늘어도 왼쪽으로만 자란다 —
            // 점수 줄(위)과 겹치지 않고 화면 밖으로도 밀리지 않는다 (REQ-105 32배 대응).
            _multiplierText = UiKit.CreateCornerText(canvas.transform, _fontBold, "x1", 12,
                UiKit.TextDim, new Vector2(1f, 1f), new Vector2(-8f, -24f),
                TextAnchor.UpperRight, "Multiplier");
        }

        void Update()
        {
            if (_director == null || _text == null) return;
            long score = _director.TotalScore;
            if (score != _lastScore)   // 값이 바뀐 프레임에만 문자열 생성 (REQ-009)
            {
                _lastScore = score;
                _text.text = score.ToString("D8");
            }
            int multiplier = _director.ScoreMultiplier;
            if (multiplier != _lastMultiplier && _multiplierText != null)
            {
                _lastMultiplier = multiplier;
                _multiplierText.text = MultiplierLabel(multiplier);
                _multiplierText.color =
                    multiplier >= 8 ? UiKit.TextDanger :
                    multiplier >= 4 ? UiKit.TextAccent :
                    multiplier >= 2 ? UiKit.TextMain : UiKit.TextDim;
            }
        }

        static string MultiplierLabel(int multiplier)
        {
            switch (multiplier)   // 고정 문자열 — 프레임 루프 무할당
            {
                case 1: return "x1";
                case 2: return "x2";
                case 4: return "x4";
                case 8: return "x8";
                case 16: return "x16";   // REQ-105: 콤보 6단계 (1·2·4·8·16·32)
                case 32: return "x32";
                default: return "x" + multiplier;
            }
        }
    }
}
