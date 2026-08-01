using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 데모 영상용 자동 진행기 (dev 도구). 타이틀을 잠시 보여준 뒤 전투로 진입하고,
    /// 오토파일럿으로 플레이하면서 보상·경로 화면을 잠깐 노출한 후 자동 선택한다.
    /// 게임플레이 코드에는 관여하지 않는다 — 에디터에서 수동 생성해 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoRunner : MonoBehaviour
    {
        [SerializeField] float _titleSeconds = 9f;
        [SerializeField] float _decisionDelay = 2.5f;   // 선택 화면 노출 시간

        float _age;
        bool _launched;
        float _decisionTimer;

        public static DemoRunner Begin(float titleSeconds, float decisionDelay)
        {
            var go = new GameObject("DemoRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<DemoRunner>();
            runner._titleSeconds = titleSeconds;
            runner._decisionDelay = decisionDelay;
            PlayerInputReader.AutopilotEnabled = true;
            return runner;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;

            if (!_launched)
            {
                if (_age < _titleSeconds) return;
                _launched = true;
                DevArgs.RuntimeSeed = 20260729;
                // 영상용 고정 시드 = 지정 시드 런이다. 오토파일럿 주행이 보드에
                // 올라갈 일은 없지만, 낙인을 세워 두는 쪽이 규칙에 일관된다.
                DevArgs.RuntimeDaily = false;
                DevArgs.RuntimeSeeded = true;
                SceneManager.LoadScene("Battle");
                return;
            }

            var director = FindAnyObjectByType<BattleDirector>();
            if (director == null) return;

            // 보상 화면은 잠깐 보여준 뒤 자동 선택 (영상에 UI가 담기도록).
            // 경로 선택은 폐지됐다 (REQ-054).
            if (director.AwaitingReward)
            {
                _decisionTimer += Time.unscaledDeltaTime;
                if (_decisionTimer < _decisionDelay) return;
                _decisionTimer = 0f;
                director.ChooseReward(0);
                return;
            }
            _decisionTimer = 0f;

            // 런이 끝나면(사망 또는 완주) 즉시 재출격해 영상이 끊기지 않게
            if (director.IsRunFinished)
                director.RestartRun();
        }
    }
}
