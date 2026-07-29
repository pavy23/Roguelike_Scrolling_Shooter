using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 보스 등장 연출 (UGUI): BossSpawned 이벤트에 맞춰 director가 Trigger()를 호출하면
    /// 2.4초 동안 깜빡이는 WARNING 배너를 그린다. 순수 표현.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossIntro : MonoBehaviour
    {
        const float Duration = 2.4f;
        const float BlinkHz = 3f;

        [SerializeField] Font _fontBold;

        float _age = float.MaxValue;
        GameObject _banner;

        public void Trigger()
        {
            _age = 0f;
        }

        void Start()
        {
            var canvas = UiKit.CreateCanvas("BossIntroCanvas", 60);
            canvas.transform.SetParent(transform, false);

            var band = new GameObject("Band");
            band.transform.SetParent(canvas.transform, false);
            var bandImage = band.AddComponent<Image>();
            bandImage.color = new Color(0.6f, 0.05f, 0.05f, 0.3f);
            bandImage.raycastTarget = false;
            var rect = bandImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 56f);

            UiKit.CreateTextStretch(rect, _fontBold, "!! WARNING !!", 26,
                UiKit.TextDanger, TextAnchor.MiddleCenter, 0f, "Warning");

            _banner = band;
            _banner.SetActive(false);
        }

        void Update()
        {
            if (_banner == null) return;
            if (_age >= Duration)
            {
                if (_banner.activeSelf) _banner.SetActive(false);
                return;
            }
            _age += Time.deltaTime;
            bool visible = Mathf.Repeat(_age * BlinkHz, 1f) <= 0.55f;
            if (_banner.activeSelf != visible)
                _banner.SetActive(visible);
        }
    }
}
