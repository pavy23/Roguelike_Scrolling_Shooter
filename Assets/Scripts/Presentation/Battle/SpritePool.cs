using System.Collections.Generic;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 고정 용량 오브젝트 풀. 생성 시점에 전부 Instantiate 해 두고, 그 뒤로는
    /// SetActive 토글만 한다 — 게임 루프에서 Instantiate/Destroy 금지 (CLAUDE.md).
    ///
    /// 용량이 바닥나면 새로 만들지 않고 null을 돌려주고 한 번만 경고한다.
    /// 조용히 늘어나는 풀은 프레임 스파이크의 원인이라, 차라리 눈에 띄게 실패시킨다.
    /// </summary>
    public sealed class SpritePool
    {
        readonly Stack<Transform> _free;
        readonly Transform _root;
        readonly string _label;
        bool _exhaustionLogged;

        public SpritePool(GameObject prefab, Transform root, int capacity, string label = null)
        {
            _root = root;
            _label = label ?? (prefab != null ? prefab.name : "SpritePool");
            _free = new Stack<Transform>(Mathf.Max(0, capacity));
            Capacity = Mathf.Max(0, capacity);

            for (int i = 0; i < Capacity; i++)
            {
                var instance = Object.Instantiate(prefab, root);
                instance.name = $"{_label}_{i:D3}";
                instance.SetActive(false);
                _free.Push(instance.transform);
            }
        }

        public int Capacity { get; }
        public int FreeCount => _free.Count;
        public int ActiveCount => Capacity - _free.Count;

        /// <summary>비활성 인스턴스를 하나 켜서 반환. 용량 초과면 null.</summary>
        public Transform Acquire()
        {
            if (_free.Count == 0)
            {
                if (!_exhaustionLogged)
                {
                    _exhaustionLogged = true;
                    Debug.LogWarning($"[SpritePool:{_label}] 용량 {Capacity} 소진. " +
                                     "Core의 MaxBullets와 풀 용량이 어긋났는지 확인해라.");
                }
                return null;
            }

            var t = _free.Pop();
            t.gameObject.SetActive(true);
            return t;
        }

        public void Release(Transform instance)
        {
            if (instance == null) return;
            instance.gameObject.SetActive(false);
            if (instance.parent != _root) instance.SetParent(_root, false);
            _free.Push(instance);
        }
    }
}
