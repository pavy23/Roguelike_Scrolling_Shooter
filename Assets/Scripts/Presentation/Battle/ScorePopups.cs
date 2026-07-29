using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 격파 점수 플로팅 팝업 (+N). 배율이 붙은 실제 부여 점수(REQ-024 이벤트 Arg)를
    /// 격파 지점에 띄워 콤보 체감을 준다. 순수 표현 — 풀링으로 프레임 루프 무할당.
    /// 캔버스는 ConstantPixelSize + 정수 배율이라 스크린 좌표를 배율로 나눠 배치한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScorePopups : MonoBehaviour
    {
        const int Capacity = 16;
        const float Lifetime = 0.75f;
        const float RiseWorld = 26f;   // 캔버스 픽셀 기준 상승량

        [SerializeField] Font _font;

        readonly List<Text> _pool = new List<Text>(Capacity);
        readonly List<float> _ages = new List<float>(Capacity);
        readonly List<Vector2> _origins = new List<Vector2>(Capacity);
        readonly List<int> _active = new List<int>(Capacity);
        Canvas _canvas;
        CanvasScaler _scaler;
        Camera _camera;

        void Start()
        {
            _canvas = UiKit.CreateCanvas("ScorePopupCanvas", 44);
            _canvas.transform.SetParent(transform, false);
            _scaler = _canvas.GetComponent<CanvasScaler>();
            _camera = Camera.main;

            for (int i = 0; i < Capacity; i++)
            {
                var text = UiKit.CreateCornerText(_canvas.transform, _font, "", 11,
                    UiKit.TextAccent, new Vector2(0f, 0f), Vector2.zero,
                    TextAnchor.MiddleCenter, "Popup");
                // 좌하단 앵커 + 중앙 피벗이라야 anchoredPosition이 곧 화면 좌표가 된다
                text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(80f, 16f);
                UiKit.AddShadow(text);
                text.enabled = false;
                _pool.Add(text);
                _ages.Add(0f);
                _origins.Add(Vector2.zero);
            }
        }

        /// <summary>월드 좌표에 +score 팝업을 띄운다. 여유 슬롯이 없으면 무시.</summary>
        public void Spawn(Vector3 worldPosition, int score)
        {
            if (score <= 0 || _canvas == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            int slot = -1;
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].enabled) { slot = i; break; }
            if (slot < 0) return;

            float scale = _scaler != null && _scaler.scaleFactor > 0f ? _scaler.scaleFactor : 1f;
            Vector3 screen = _camera.WorldToScreenPoint(worldPosition);
            var origin = new Vector2(screen.x / scale, screen.y / scale);

            var text = _pool[slot];
            text.text = "+" + score;
            text.color = UiKit.TextAccent;
            text.enabled = true;
            text.rectTransform.anchoredPosition = origin;
            _origins[slot] = origin;
            _ages[slot] = 0f;
            if (!_active.Contains(slot)) _active.Add(slot);
        }

        void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                int slot = _active[i];
                float age = _ages[slot] + Time.deltaTime;
                if (age >= Lifetime)
                {
                    _pool[slot].enabled = false;
                    _active.RemoveAt(i);
                    continue;
                }
                _ages[slot] = age;
                float t = age / Lifetime;
                _pool[slot].rectTransform.anchoredPosition =
                    _origins[slot] + new Vector2(0f, RiseWorld * t);
                var color = UiKit.TextAccent;
                color.a = 1f - t * t;   // 후반에 빠르게 사라짐
                _pool[slot].color = color;
            }
        }
    }
}
