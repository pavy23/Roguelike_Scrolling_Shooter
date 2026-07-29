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

        Text _text;
        long _lastScore = long.MinValue;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("ScoreCanvas", 40);
            canvas.transform.SetParent(transform, false);
            _text = UiKit.CreateCornerText(canvas.transform, _fontBold, "00000000", 16,
                UiKit.TextAccent, new Vector2(1f, 1f), new Vector2(-8f, -4f),
                TextAnchor.UpperRight, "Score");
        }

        void Update()
        {
            if (_director == null || _text == null) return;
            long score = _director.TotalScore;
            if (score == _lastScore) return;   // 값이 바뀐 프레임에만 문자열 생성 (REQ-009)
            _lastScore = score;
            _text.text = score.ToString("D8");
        }
    }
}
