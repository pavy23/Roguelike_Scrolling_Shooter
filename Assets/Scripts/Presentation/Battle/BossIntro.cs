using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 보스 등장 연출 (M2): BossSpawned 이벤트에 맞춰 director가 Trigger()를 호출하면
    /// 2.4초 동안 깜빡이는 WARNING 배너를 그린다. 순수 표현.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossIntro : MonoBehaviour
    {
        const float Duration = 2.4f;
        const float BlinkHz = 3f;

        float _age = float.MaxValue;
        GUIStyle _style;

        public void Trigger()
        {
            _age = 0f;
        }

        void Update()
        {
            if (_age < Duration)
                _age += Time.deltaTime;
        }

        void OnGUI()
        {
            if (_age >= Duration) return;
            if (Mathf.Repeat(_age * BlinkHz, 1f) > 0.55f) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 40,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.25f, 0.2f) }
                };
            }

            float width = Screen.width, height = Screen.height;
            GUI.color = new Color(0.6f, 0.05f, 0.05f, 0.25f);
            GUI.DrawTexture(new Rect(0, height * 0.38f, width, height * 0.16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, height * 0.38f, width, height * 0.16f), "!! WARNING !!", _style);
        }
    }
}
