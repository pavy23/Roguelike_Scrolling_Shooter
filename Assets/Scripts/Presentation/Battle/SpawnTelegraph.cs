using System.Collections.Generic;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 적 등장 예고 마커 (플레이 감각 개선): 적이 화면 우측 밖에서 스폰되는 순간
    /// 그 높이의 화면 가장자리에 깜빡이는 화살표를 띄운다. 특히 dive/dash 패턴 적을
    /// 미리 읽을 수 있게 해 준다. 순수 표현 — 풀링으로 프레임 루프 무할당.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpawnTelegraph : MonoBehaviour
    {
        const int Capacity = 12;
        const float Lifetime = 0.55f;
        const float BlinkHz = 8f;

        [SerializeField] Sprite _markerSprite;
        [SerializeField] Transform _root;
        [SerializeField] float _edgeX = 9.4f;   // 화면 우측 안쪽 (월드 유닛)

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(Capacity);
        readonly List<float> _ages = new List<float>(Capacity);
        readonly List<int> _active = new List<int>(Capacity);

        void Start()
        {
            if (_markerSprite == null) return;
            var parent = _root != null ? _root : transform;
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject($"SpawnMarker_{i:D2}");
                go.transform.SetParent(parent, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _markerSprite;
                renderer.sortingOrder = 18;
                renderer.enabled = false;
                _pool.Add(renderer);
                _ages.Add(0f);
            }
        }

        /// <summary>월드 Y 높이에 예고 마커를 띄운다. 여유 슬롯이 없으면 무시.</summary>
        public void Warn(float worldY)
        {
            if (_pool.Count == 0) return;
            int slot = -1;
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].enabled) { slot = i; break; }
            if (slot < 0) return;

            _pool[slot].transform.localPosition = new Vector3(_edgeX, worldY, 0f);
            _pool[slot].enabled = true;
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
                // 깜빡임 + 후반 페이드
                bool visible = Mathf.Repeat(age * BlinkHz, 1f) < 0.6f;
                var color = Color.white;
                color.a = visible ? 1f - age / Lifetime * 0.4f : 0f;
                _pool[slot].color = color;
            }
        }
    }
}
