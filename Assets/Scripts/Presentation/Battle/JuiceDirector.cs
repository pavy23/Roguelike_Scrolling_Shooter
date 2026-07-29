using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 게임 필(주스) 연출 허브: 히트스톱·슬로모·화면 흔들림. 순수 표현 —
    /// Time.timeScale을 잠깐 낮출 뿐이라 FixedUpdate 기반 시뮬의 틱 내용에는 영향 없다
    /// (실시간만 지연). PauseScreen의 timeScale=0과 충돌하지 않도록 0은 절대 쓰지 않고,
    /// 일시정지 중에는 관여하지 않는다.
    /// 접근성: rss.shake(기본 1) / rss.flashreduce(기본 0) PlayerPrefs 토글.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JuiceDirector : MonoBehaviour
    {
        public const string ShakePrefKey = "rss.shake";
        public const string FlashReducePrefKey = "rss.flashreduce";

        const float MinTimeScale = 0.05f;   // 0이면 PauseScreen 상태와 구분 불가

        [SerializeField] Transform _cameraTransform;

        bool _shakeEnabled = true;
        bool _flashReduced;

        float _hitstopRemaining;            // unscaled 초
        float _slowmoRemaining;
        float _slowmoScale = 1f;
        float _baseTimeScale = 1f;          // 효과 시작 시점의 배속 (dev x4 등 보존)
        bool _timeEffectActive;
        float _shakeAmplitude;              // 월드 유닛
        Vector3 _cameraBasePosition;
        bool _cameraBaseCaptured;

        /// <summary>흔들림 토글 상태 (BattleDirector가 플래시 연출 게이트에도 사용).</summary>
        public bool ShakeEnabled => _shakeEnabled;
        public bool FlashReduced => _flashReduced;

        void Awake()
        {
            ReloadPrefs();
        }

        public void ReloadPrefs()
        {
            _shakeEnabled = PlayerPrefs.GetInt(ShakePrefKey, 1) == 1;
            _flashReduced = PlayerPrefs.GetInt(FlashReducePrefKey, 0) == 1;
        }

        /// <summary>짧은 정지감. 더 강한(긴) 요청이 우선한다.</summary>
        public void Hitstop(float seconds)
        {
            if (_flashReduced) seconds *= 0.5f;   // 감소 모드에선 절반
            _hitstopRemaining = Mathf.Max(_hitstopRemaining, Mathf.Clamp(seconds, 0f, 0.25f));
        }

        /// <summary>보스 격파 등 긴 슬로모. scale은 [0.1, 1).</summary>
        public void Slowmo(float scale, float seconds)
        {
            _slowmoScale = Mathf.Clamp(scale, 0.1f, 0.95f);
            _slowmoRemaining = Mathf.Max(_slowmoRemaining, Mathf.Clamp(seconds, 0f, 2f));
        }

        /// <summary>진폭(월드 유닛)으로 흔들림 요청. 큰 요청이 우선, 지수 감쇠.</summary>
        public void Shake(float amplitude)
        {
            if (!_shakeEnabled) return;
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, Mathf.Clamp(amplitude, 0f, 1.5f));
        }

        void Update()
        {
            bool paused = Time.timeScale == 0f;

            // 시간 연출 (일시정지 중엔 관여 금지 — PauseScreen이 timeScale 소유).
            // 효과가 없을 땐 timeScale에 손대지 않는다 — 개발용 배속(x4)·성능 계측을 깨지 않기 위함.
            if (!paused)
            {
                bool wantsEffect = _slowmoRemaining > 0f || _hitstopRemaining > 0f;
                if (wantsEffect)
                {
                    if (!_timeEffectActive)
                    {
                        _baseTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                        _timeEffectActive = true;
                    }
                    float target = _baseTimeScale;
                    if (_slowmoRemaining > 0f)
                    {
                        _slowmoRemaining -= Time.unscaledDeltaTime;
                        target = _baseTimeScale * _slowmoScale;
                    }
                    if (_hitstopRemaining > 0f)
                    {
                        _hitstopRemaining -= Time.unscaledDeltaTime;
                        target = MinTimeScale;
                    }
                    if (!Mathf.Approximately(Time.timeScale, target))
                        Time.timeScale = target;
                }
                else if (_timeEffectActive)
                {
                    _timeEffectActive = false;
                    Time.timeScale = _baseTimeScale;
                }
            }

            // 화면 흔들림 (unscaled — 히트스톱 중에도 흔들리는 쪽이 타격감이 산다)
            if (_cameraTransform != null)
            {
                if (!_cameraBaseCaptured)
                {
                    _cameraBasePosition = _cameraTransform.localPosition;
                    _cameraBaseCaptured = true;
                }
                if (_shakeAmplitude > 0.001f)
                {
                    _shakeAmplitude *= Mathf.Exp(-8f * Time.unscaledDeltaTime);
                    var offset = new Vector3(
                        (Mathf.PerlinNoise(Time.unscaledTime * 31f, 0.3f) - 0.5f) * 2f,
                        (Mathf.PerlinNoise(0.7f, Time.unscaledTime * 29f) - 0.5f) * 2f,
                        0f) * _shakeAmplitude;
                    _cameraTransform.localPosition = _cameraBasePosition + offset;
                }
                else if (_shakeAmplitude != 0f)
                {
                    _shakeAmplitude = 0f;
                    _cameraTransform.localPosition = _cameraBasePosition;
                }
            }
        }

        void OnDestroy()
        {
            // 씬 전환 시 잔류 방지 (일시정지 소유권은 건드리지 않는다)
            if (Time.timeScale != 0f && Time.timeScale != 1f)
                Time.timeScale = 1f;
        }
    }
}
