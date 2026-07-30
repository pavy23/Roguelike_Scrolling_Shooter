using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 마지막 목숨 경고: 붉은 비네트 맥동 + 주기적 경고음.
    ///
    /// HP가 사라지고 실드 스톡이 유일한 내구도가 된 뒤로(REQ-040), 위험 신호는
    /// **스톡 0**이다 — 그 상태에서 한 번만 더 맞으면 즉사한다. 예전 `PlayerHp == 1`
    /// 조건은 이제 살아 있는 동안 항상 참이라(생존 1 / 사망 0 호환 프로퍼티) 쓸 수 없다.
    ///
    /// 순수 표현 — director의 상태를 읽기만 한다. 접근성(플래시 감소) 설정을 존중한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LowHpWarning : MonoBehaviour
    {
        const float BeepInterval = 1.1f;

        [SerializeField] BattleDirector _director;
        [SerializeField] JuiceDirector _juice;
        [SerializeField] AudioSource _source;
        [SerializeField] AudioClip _warningClip;

        Image _vignette;
        float _beepTimer;

        void Start()
        {
            if (_source == null)
            {
                // 경고음 전용 소스 (SFX 스로틀·피치 랜덤과 분리)
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }

            var canvas = UiKit.CreateCanvas("LowHpCanvas", 42);
            canvas.transform.SetParent(transform, false);
            _vignette = UiKit.CreateDim(canvas.transform, new Color(0.9f, 0.05f, 0.08f, 0f), "Vignette");
            _vignette.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _vignette == null) return;

            bool danger = !_director.IsRunFinished && _director.ShieldRemaining == 0
                          && Time.timeScale > 0f;
            if (_vignette.gameObject.activeSelf != danger)
            {
                _vignette.gameObject.SetActive(danger);
                _beepTimer = 0f;
            }
            if (!danger) return;

            float peak = _juice != null && _juice.FlashReduced ? 0.10f : 0.22f;
            float pulse = (Mathf.Sin(Time.unscaledTime * 5.2f) + 1f) * 0.5f;
            var color = _vignette.color;
            color.a = pulse * peak;
            _vignette.color = color;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer <= 0f)
            {
                _beepTimer = BeepInterval;
                if (_source != null && _warningClip != null)
                    _source.PlayOneShot(_warningClip, 0.55f);
            }
        }
    }
}
