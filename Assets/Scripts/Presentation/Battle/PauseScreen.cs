using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// ESC/패드 Start 일시정지 (UGUI + 픽셀 폰트). Time.timeScale=0으로 시뮬 틱을 멈출 뿐이라
    /// 결정론에 영향 없다. 볼륨은 AudioListener.volume 전역 값 + PlayerPrefs 저장.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseScreen : MonoBehaviour
    {
        const string VolumePrefKey = "rss.volume";

        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;

        bool _paused;
        GameObject _root;
        Text _volumeText;
        int _lastVolumePercent = -1;

        void Start()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

            var canvas = UiKit.CreateCanvas("PauseCanvas", 80);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;

            UiKit.CreateDim(canvas.transform, new Color(0f, 0.01f, 0.05f, 0.62f));
            var panel = UiKit.CreatePanel(canvas.transform, new Vector2(300f, 110f));
            UiKit.CreateCornerText(panel, _fontBold, "PAUSED", 20, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -12f), TextAnchor.UpperCenter, "Title");
            _volumeText = UiKit.CreateCornerText(panel, _font, "", 11, UiKit.TextMain,
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), TextAnchor.UpperCenter, "Volume");
            UiKit.CreateCornerText(panel, _font,
                "ESC/(Start) 계속    O 옵션    Q 타이틀", 11, UiKit.TextDim,
                new Vector2(0.5f, 0f), new Vector2(0f, 14f), TextAnchor.LowerCenter, "Hints");

            _root.SetActive(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            bool toggle = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                       || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
            if (toggle)
                SetPaused(!_paused);
            if (!_paused) return;

            if (keyboard != null)
            {
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
            if (gamepad != null)
            {
                if (gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame)
                    AdjustVolume(0.1f);
                if (gamepad.dpad.down.wasPressedThisFrame || gamepad.dpad.left.wasPressedThisFrame)
                    AdjustVolume(-0.1f);
            }

            int volumePercent = (int)(AudioListener.volume * 100f);
            if (volumePercent != _lastVolumePercent && _volumeText != null)
            {
                _lastVolumePercent = volumePercent;
                _volumeText.text = $"VOLUME  {volumePercent}%   (←/→)";
            }
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (_root != null) _root.SetActive(paused);
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
    }
}
