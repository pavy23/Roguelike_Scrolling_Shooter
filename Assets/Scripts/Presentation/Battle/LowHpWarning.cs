using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 저체력 경고 (HP 1): 붉은 비네트 맥동 + 주기적 경고음.
    /// 순수 표현 — director의 HP를 읽기만 한다. 접근성(플래시 감소) 설정을 존중한다.
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

            bool danger = !_director.IsRunOver && _director.PlayerHp == 1 && Time.timeScale > 0f;
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
