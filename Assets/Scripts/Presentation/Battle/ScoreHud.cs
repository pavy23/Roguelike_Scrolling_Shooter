using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>우상단 점수 표시 (Core RunManager.TotalScore를 읽기만 한다).</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreHud : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;

        GUIStyle _style;

        void OnGUI()
        {
            if (_director == null) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(16, Screen.height / 30),
                    alignment = TextAnchor.UpperRight,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.95f, 0.9f, 0.6f) }
                };
            GUI.Label(
                new Rect(0, 6, Screen.width - 12, _style.fontSize * 1.5f),
                _director.TotalScore.ToString("D8"),
                _style);
        }
    }
}
