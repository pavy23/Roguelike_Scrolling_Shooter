using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 적·지형이 쏘는 지속 레이저를 그린다 (REQ-042).
    ///
    /// Core는 선분(시작·끝점)과 4단계 진행(Telegraph → Firing → Sustaining →
    /// Dissipating), 굵기 단계를 상태로 노출한다. 여기서는 그 상태를 1px 흰 스프라이트
    /// 하나를 늘려 표현한다 — 선분 렌더러를 쓰면 픽셀아트 화면에서 뜬다.
    ///
    /// **예고 단계를 확실히 다르게 보여야 한다.** 예고 없는 레이저는 불공정하고,
    /// 예고가 발사와 비슷해 보이면 예고가 있으나 마나다. 그래서 예고는 가늘고 반투명하게
    /// 깜빡이고, 발사는 굵고 밝게 나간다.
    ///
    /// 순수 표현 — 판정은 전부 Core의 선분 대 원 정수 연산이 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaserBeamView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Sprite _pixelSprite;
        [SerializeField] Transform _root;

        [Tooltip("동시에 그릴 수 있는 레이저 수. Core의 MaxLasers와 맞춘다.")]
        [SerializeField] int _capacity = 8;

        static readonly Color TelegraphColor = new Color(1f, 0.35f, 0.45f, 0.30f);
        static readonly Color FiringColor = new Color(1f, 0.92f, 0.72f, 0.95f);
        static readonly Color SustainColor = new Color(1f, 0.62f, 0.42f, 0.88f);
        static readonly Color DissipateColor = new Color(0.85f, 0.45f, 0.55f, 0.45f);

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(8);
        readonly HashSet<int> _seen = new HashSet<int>();

        void Start()
        {
            var parent = _root != null ? _root : transform;
            for (int i = 0; i < _capacity; i++)
            {
                var go = new GameObject($"Laser_{i:D2}");
                go.transform.SetParent(parent, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _pixelSprite;
                // 탄보다 뒤, 배경보다 앞 — 화면을 가리지 않으면서 위협은 읽히게.
                renderer.sortingOrder = 12;
                renderer.enabled = false;
                _pool.Add(renderer);
            }
        }

        void LateUpdate()
        {
            if (_director == null || _pool.Count == 0) return;
            var lasers = _director.Lasers;
            _seen.Clear();

            int slot = 0;
            if (lasers != null)
            {
                for (int i = 0; i < lasers.Count && slot < _pool.Count; i++)
                {
                    Draw(_pool[slot++], lasers[i]);
                }
            }

            // 남은 슬롯은 끈다 (풀이므로 파괴하지 않는다).
            for (; slot < _pool.Count; slot++)
                if (_pool[slot].enabled) _pool[slot].enabled = false;
        }

        void Draw(SpriteRenderer renderer, in LaserState laser)
        {
            Vector3 start = SimView.ToWorld(laser.StartX, laser.StartY);
            Vector3 end = SimView.ToWorld(laser.EndX, laser.EndY);
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.0001f)
            {
                renderer.enabled = false;
                return;
            }

            // Core가 준 반폭을 그대로 쓴다 — 예고 단계는 이 값이 얇게 들어온다.
            float thickness = 2f * laser.HalfWidth / SimSpace.SubUnitsPerWorldUnit;
            if (thickness <= 0.0001f) thickness = 0.0625f;

            var t = renderer.transform;
            t.localPosition = (start + end) * 0.5f;
            t.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length, thickness, 1f);

            Color color = ColorFor(laser.Phase);
            // 예고 중에는 깜빡여서 "아직 안 쏜다"를 알린다.
            if (laser.Phase == LaserPhase.Telegraph)
            {
                float blink = Mathf.Repeat(Time.time * 9f, 1f) < 0.5f ? 1f : 0.45f;
                color.a *= blink;
            }
            renderer.color = color;
            if (!renderer.enabled) renderer.enabled = true;
        }

        static Color ColorFor(LaserPhase phase)
        {
            switch (phase)
            {
                case LaserPhase.Telegraph: return TelegraphColor;
                case LaserPhase.Firing: return FiringColor;
                case LaserPhase.Sustaining: return SustainColor;
                default: return DissipateColor;
            }
        }
    }
}
