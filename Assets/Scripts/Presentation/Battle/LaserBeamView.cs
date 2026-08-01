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
    /// **발사는 원점에서 앞으로 뻗어 나간다.** 예전에는 Firing 진입 프레임에 전장이
    /// 통째로 켜져서 "갑자기 나타난다"로 읽혔다(사람 지적 2026-08-01). 선단이 원점에서
    /// 끝점까지 뻗는 아주 짧은 연출을 넣되, Core 히트박스는 진입 즉시 전장이므로
    /// 길게 끌지 않는다 — 늦으면 "안 보이는 곳에서 맞았다"가 된다.
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

        /// <summary>
        /// 발사 순간 선단이 원점에서 끝점까지 뻗는 데 걸리는 시간. Core는 Firing 첫 틱부터
        /// 전장으로 판정하므로 이 값은 "눈이 방향을 읽을 수 있는 최소치"여야 한다.
        /// </summary>
        const float GrowSeconds = 0.18f;

        /// <summary>그로우 첫 프레임에도 선단이 보이게 하는 최소 길이 비율.</summary>
        const float MinGrowFraction = 0.06f;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(8);

        // 이번 프레임에 살아 있는 레이저 id — 사라진 빔의 그로우 기록을 걷어낸다.
        readonly HashSet<int> _seen = new HashSet<int>();

        // 그로우 경과 시간 (id → 나이). 동시 8줄이 상한이라 선형 탐색이 사전보다 싸고,
        // 매 프레임 할당이 없다.
        readonly List<int> _growIds = new List<int>(8);
        readonly List<float> _growAges = new List<float>(8);

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
                for (int i = 0; i < lasers.Count; i++)
                {
                    var laser = lasers[i];
                    // 풀이 모자라 못 그린 빔도 살아 있는 것은 맞다 — 여기서 빠뜨리면
                    // 그로우 기록이 지워졌다가 다음 프레임에 처음부터 다시 뻗는다.
                    _seen.Add(laser.Id);
                    if (slot >= _pool.Count) continue;
                    Draw(_pool[slot++], laser);
                }
            }

            // 남은 슬롯은 끈다 (풀이므로 파괴하지 않는다).
            for (; slot < _pool.Count; slot++)
                if (_pool[slot].enabled) _pool[slot].enabled = false;

            ForgetDeadBeams();
        }

        /// <summary>사라진 빔의 그로우 기록 정리 — 같은 슬롯을 다음 빔이 물려받아도 새로 뻗는다.</summary>
        void ForgetDeadBeams()
        {
            for (int i = _growIds.Count - 1; i >= 0; i--)
            {
                if (_seen.Contains(_growIds[i])) continue;
                _growIds.RemoveAt(i);
                _growAges.RemoveAt(i);
            }
        }

        /// <summary>
        /// 지금 그려야 할 길이 비율 (0~1). 예고는 전장으로 보여 줘야 어디를 피할지 알 수
        /// 있고, 발사 이후 단계(Sustaining/Dissipating)는 이미 다 뻗은 뒤다 —
        /// 뻗는 연출은 Firing 구간만의 것이다.
        /// </summary>
        float GrowFraction(in LaserState laser)
        {
            if (laser.Phase != LaserPhase.Firing) return 1f;

            int index = _growIds.IndexOf(laser.Id);
            if (index < 0)
            {
                index = _growIds.Count;
                _growIds.Add(laser.Id);
                _growAges.Add(0f);
            }

            float age = _growAges[index] + Time.deltaTime;
            _growAges[index] = age;
            if (age >= GrowSeconds) return 1f;

            // 감속 곡선: 선단이 초반에 확 튀어나가고 끝에서 붙는다. 등속으로 늘리면
            // "천천히 자란다"로 읽혀 히트박스(이미 전장)와의 어긋남이 더 눈에 띈다.
            float t = age / GrowSeconds;
            return Mathf.Max(1f - (1f - t) * (1f - t), MinGrowFraction);
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

            // 원점(Start)에 뿌리를 박고 선단만 앞으로 뻗는다 — 중심이 아니라 시작점이
            // 고정돼야 "쏘아 나간다"로 읽힌다.
            float grow = GrowFraction(laser);

            var t = renderer.transform;
            t.localPosition = start + delta * (0.5f * grow);
            t.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(length * grow, thickness, 1f);

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
