using System.Collections.Generic;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 보스가 기체를 빨아들일 때의 연출 (브루드마더 최종 패턴).
    ///
    /// Core는 <see cref="BattleDirector.SuctionActive"/>로 이 상태를 계속
    /// 내보내고 있었는데 **읽는 쪽이 없었다.** 기체가 끌려가는데 화면은 아무
    /// 말도 하지 않아서, 사람이 "빨아들이는건 아무런 이팩트가 없어서 모르겠어"라고
    /// 보고했다 (2026-08-04).
    ///
    /// 그리는 것은 둘이다:
    ///   1. **보스로 오므라드는 고리** — 바깥에서 생겨 보스 중심으로 줄어든다.
    ///      방향(어디로 끌려가는가)과 세기(고리가 촘촘할수록 강하다)를 같이 말한다.
    ///   2. **기체에서 보스로 흐르는 줄기** — 지금 끌리고 있는 것이 나라는 표시.
    ///
    /// 고리는 풀이고 꺼져 있을 때 비용이 없다. 판정에는 관여하지 않는다 —
    /// 흡입의 세기와 범위는 전부 Core가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuctionView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [Tooltip("고리·줄기에 쓰는 스프라이트. 가장자리가 부드러운 것이 좋다.")]
        [SerializeField] Sprite _ringSprite;
        [SerializeField] Sprite _softSprite;

        /// <summary>동시에 떠 있는 고리 수. 많을수록 촘촘하게 빨려 들어간다.</summary>
        const int RingCount = 5;
        /// <summary>고리 하나가 바깥에서 보스까지 오므라드는 데 걸리는 시간(초).</summary>
        const float RingSeconds = 1.1f;
        /// <summary>고리가 생겨나는 반경(월드 유닛).</summary>
        const float StartRadius = 13f;
        /// <summary>보스 뒤·게임플레이 아래. 탄과 기체를 가리면 안 된다.</summary>
        const int RingOrder = 6;

        static readonly Color RingTint = new Color(0.95f, 0.45f, 0.85f, 1f);
        static readonly Color PullTint = new Color(1f, 0.72f, 0.95f, 1f);

        readonly List<SpriteRenderer> _rings = new List<SpriteRenderer>(RingCount);
        SpriteRenderer _pull;
        float _ringUnit = 1f;
        float _softUnitX = 1f;
        float _softUnitY = 1f;
        float _age;

        void Start()
        {
            Transform parent = _root != null ? _root : transform;
            if (_ringSprite != null)
            {
                float size = _ringSprite.bounds.size.x;
                if (size > 0.0001f) _ringUnit = size;
            }
            if (_softSprite != null)
            {
                Vector3 size = _softSprite.bounds.size;
                if (size.x > 0.0001f) _softUnitX = size.x;
                if (size.y > 0.0001f) _softUnitY = size.y;
            }
            for (int i = 0; i < RingCount; i++)
                _rings.Add(Create(parent, $"SuctionRing_{i:D2}", _ringSprite));
            _pull = Create(parent, "SuctionPull", _softSprite);
        }

        static SpriteRenderer Create(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = RingOrder;
            renderer.enabled = false;
            return renderer;
        }

        void LateUpdate()
        {
            if (_director == null || _rings.Count == 0) return;
            bool active = _director.SuctionActive && _director.BossActive;
            if (!active)
            {
                Hide();
                return;
            }

            _age += Time.deltaTime;
            Vector3 boss = _director.BossWorldPosition;
            Vector3 ship = (Vector3)_director.PlayerWorldPosition;

            for (int i = 0; i < _rings.Count; i++)
            {
                // 고리마다 위상을 어긋나게 해 끊임없이 들어오는 것처럼 보이게 한다.
                float phase = (_age / RingSeconds + i / (float)_rings.Count) % 1f;
                // 1 → 0 으로 줄어든다 (바깥에서 보스로).
                float radius = StartRadius * (1f - phase);
                float scale = Mathf.Max(0.05f, radius * 2f) / _ringUnit;
                var renderer = _rings[i];
                var t = renderer.transform;
                t.localPosition = boss;
                t.localRotation = Quaternion.identity;
                t.localScale = new Vector3(scale, scale, 1f);
                Color color = RingTint;
                // 들어올수록 진해진다 — 가까울수록 위험하다는 것과 같은 방향이다.
                color.a = Mathf.Lerp(0.05f, 0.5f, phase);
                renderer.color = color;
                if (!renderer.enabled) renderer.enabled = true;
            }

            // 기체 → 보스 줄기. "지금 끌리는 것은 나"를 말한다.
            if (_pull != null && _softSprite != null)
            {
                Vector3 delta = boss - ship;
                float length = delta.magnitude;
                if (length > 0.2f)
                {
                    var t = _pull.transform;
                    t.localPosition = ship + delta * 0.5f;
                    t.localRotation = Quaternion.Euler(
                        0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    t.localScale = new Vector3(
                        length / _softUnitX, 0.5f / _softUnitY, 1f);
                    Color color = PullTint;
                    color.a = 0.20f + 0.14f * Mathf.Sin(_age * 9f);
                    _pull.color = color;
                    if (!_pull.enabled) _pull.enabled = true;
                }
                else if (_pull.enabled)
                {
                    _pull.enabled = false;
                }
            }
        }

        void Hide()
        {
            _age = 0f;
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].enabled) _rings[i].enabled = false;
            if (_pull != null && _pull.enabled) _pull.enabled = false;
        }
    }
}
