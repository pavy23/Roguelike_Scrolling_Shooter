using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 데모 영상용 프레임 시퀀스 캡처 (dev 도구). Time.captureFramerate로 게임 시간을
    /// 캡처 페이스에 고정해 프레임 드랍 없는 오프라인 캡처를 만든다.
    /// 인코딩(ffmpeg)은 외부에서 수행. 게임플레이에는 영향 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoFrameCapture : MonoBehaviour
    {
        public static bool Finished { get; private set; }

        string _dir;
        int _frame;
        int _total;

        public static void Begin(string dir, int fps, float seconds)
        {
            Finished = false;
            System.IO.Directory.CreateDirectory(dir);
            var go = new GameObject("DemoFrameCapture");
            DontDestroyOnLoad(go);
            var capture = go.AddComponent<DemoFrameCapture>();
            capture._dir = dir;
            capture._total = (int)(fps * seconds);
            Time.captureFramerate = fps;
        }

        void Update()
        {
            if (_frame < _total)
            {
                ScreenCapture.CaptureScreenshot(
                    System.IO.Path.Combine(_dir, $"f_{_frame:D5}.png"));
                _frame++;
            }
            else
            {
                Time.captureFramerate = 0;
                PlayerInputReader.AutopilotEnabled = false;
                Finished = true;
                Destroy(gameObject);
            }
        }
    }
}
