using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// ESC 일시정지 (M4 선행). Time.timeScale=0으로 FixedUpdate(시뮬 틱)를 멈출 뿐이라
    /// 결정론에 영향 없다. 볼륨은 AudioListener.volume 전역 값 + PlayerPrefs 저장.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseScreen : MonoBehaviour
    {
        const string VolumePrefKey = "rss.volume";

        bool _paused;
        GUIStyle _titleStyle, _bodyStyle;

        void Start()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!_paused);
            if (!_paused) return;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                AdjustVolume(0.1f);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                AdjustVolume(-0.1f);
            if (keyboard.qKey.wasPressedThisFrame)
            {
                SetPaused(false);
                SceneManager.LoadScene("Title");
            }
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        static void AdjustVolume(float delta)
        {
            AudioListener.volume = Mathf.Clamp01(AudioListener.volume + delta);
            PlayerPrefs.SetFloat(VolumePrefKey, AudioListener.volume);
        }

        void OnDestroy()
        {
            // 씬 전환 시 잔류 방지
            if (_paused)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        void OnGUI()
        {
            if (!_paused) return;
            EnsureStyles();
            float width = Screen.width, height = Screen.height;

            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, height * 0.32f, width, 44f), "PAUSED", _titleStyle);
            GUI.Label(
                new Rect(0, height * 0.45f, width, 90f),
                $"VOLUME  {(int)(AudioListener.volume * 100f)}%   (←/→)\n\n" +
                "ESC  RESUME        Q  QUIT TO TITLE",
                _bodyStyle);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.8f, 0.95f) }
            };
        }
    }
}
