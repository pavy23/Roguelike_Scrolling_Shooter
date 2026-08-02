using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// St4 번개룡 — 세그먼트 체인 미니언 뷰 (REQ-115b 관측 소비).
    ///
    /// **왜 새 뷰가 필요했나.** Core는 이 미니언을 <c>IBattleSim.Enemies</c>가 아니라
    /// <c>IBattleSim.SegmentChains</c>라는 **별도 관측**으로 노출한다. BattleDirector의
    /// 적 동기화(SyncEnemies)는 Enemies만 읽으므로 체인은 어떤 배치에서도 한 번도
    /// 그려진 적이 없었다 — 접촉 데미지는 주는데 화면에 없는 상태였다
    /// ("체인 미니언 스프라이트를 시각적으로 못 찾았다", build26/27 테스터 2회 연속).
    ///
    /// **그리는 것.** 절(segment)마다 스프라이트 한 장. 위치는 Core가 준 좌표를 그대로
    /// 쓴다 — 관절 보간을 여기서 하면 판정과 어긋난다(Core는 머리 위치 히스토리에서
    /// segmentIndex * followDelayTicks 전 좌표를 읽어 준다).
    ///
    /// **피격 가능 여부를 색으로 가른다** (<c>SegmentChainState.Damageable</c>).
    /// Gradius 화염룡 문법대로 **머리만** 피격 가능하고 몸통 절은 무적이다.
    ///   - 머리   : 백열(거의 흰색) + 맥동 + 방전 코어 글로우. "여기를 쏴라".
    ///   - 몸통   : 시안 → 짙은 청보라로 점점 어두워지고 크기도 점감한다.
    ///              어두운 절 = 쏴도 안 통하는 절이라는 신호다.
    /// 절 사이는 낙뢰 아크로 잇는다. 색은 nebula 폭풍 테마의 섬광색군
    /// (SectionTheme nebula flashColor 0.92/0.95/1.00)과 같아서 스테이지 어휘로 읽힌다.
    ///
    /// 정렬은 일반 적(8)과 같은 층이다. 아크는 그 바로 뒤(7), 머리 글로우는 앞(9).
    ///
    /// 오브젝트 풀: 렌더러를 Start에서 전부 만들어 두고 그 뒤로는 enabled 토글만 한다
    /// (CLAUDE.md — 게임 루프에서 Instantiate/Destroy 금지).
    ///
    /// 순수 표현 — HP도 위치도 판정도 전부 Core가 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SegmentChainView : MonoBehaviour
    {
        // ── 정렬 ──────────────────────────────────────────────────────────────
        // 적 프리팹이 8이다 (BattleSceneBuilder.WriteSpritePrefab). 체인도 적이므로
        // 같은 층에 둔다 — 탄(5)보다 앞, 장애물(9~12)·보스(15)보다 뒤.
        const int ArcOrder = 7;
        const int SegmentOrder = 8;
        const int GlowOrder = 9;

        // ── 크기 ──────────────────────────────────────────────────────────────

        /// <summary>절 크기 점감: 머리 1.0 → 꼬리 이 값. 굵기가 곧 "머리가 어디냐"다.</summary>
        const float TailScale = 0.55f;

        /// <summary>
        /// Core가 체인 절 크기를 안 줄 때의 폴백 반폭(서브유닛). waves.json의
        /// halfWidth 0.75 월드유닛과 같다 (0.75 * 256).
        /// </summary>
        const int FallbackHalfWidthSubUnits = 192;

        /// <summary>머리 방전 글로우 지름(월드 유닛) — 머리 실폭보다 조금 크게.</summary>
        const float GlowSizeFactor = 1.9f;

        // ── 색 ────────────────────────────────────────────────────────────────

        /// <summary>머리 기본색. 백열에 가까운 청백 — 유일하게 피격 가능한 절이다.</summary>
        static readonly Color HeadTint = new Color(0.88f, 0.96f, 1.00f, 1f);

        /// <summary>머리 맥동 최고점. 완전 백열.</summary>
        static readonly Color HeadHot = new Color(1.00f, 1.00f, 1.00f, 1f);

        /// <summary>머리 바로 뒤 절 — 밝은 시안.</summary>
        static readonly Color BodyNear = new Color(0.46f, 0.86f, 1.00f, 1f);

        /// <summary>꼬리 절 — 짙은 청보라. 무적 절일수록 어둡다.</summary>
        static readonly Color BodyFar = new Color(0.20f, 0.34f, 0.66f, 1f);

        /// <summary>절 사이 낙뢰 아크 — nebula 섬광과 같은 색군.</summary>
        static readonly Color ArcTint = new Color(0.92f, 0.95f, 1.00f, 1f);

        /// <summary>머리 글로우 — 시안 방전.</summary>
        static readonly Color GlowTint = new Color(0.55f, 0.92f, 1.00f, 1f);

        // ── 맥동 / 점멸 ───────────────────────────────────────────────────────

        const float HeadPulseHz = 5f;
        /// <summary>머리 HP가 이 비율 아래면 맥동이 빨라진다 — "곧 죽는다".</summary>
        const float HeadLowHpFraction = 0.3f;
        const float HeadLowHpPulseHz = 11f;

        const float ArcFlickerHz = 13f;
        const float ArcAlphaMin = 0.30f;
        const float ArcAlphaMax = 0.85f;
        const float ArcThicknessMin = 0.07f;
        const float ArcThicknessMax = 0.16f;

        /// <summary>머리 피격 백색 플래시 지속. 보스 피격 플래시와 같은 값.</summary>
        const float HitFlashSeconds = 0.09f;

        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] JuiceDirector _juice;

        [Tooltip("머리 스프라이트 — 전기/구체 계열(enemy_echo_wisp). 비면 몸통 것을 쓴다.")]
        [SerializeField] Sprite _headSprite;

        [Tooltip("몸통 절 스프라이트. 비면 머리 것을 쓴다.")]
        [SerializeField] Sprite _bodySprite;

        [Tooltip("px_white — 절 사이 낙뢰 아크. 비면 아크를 그리지 않는다.")]
        [SerializeField] Sprite _pixelSprite;

        [Tooltip("머리 방전 코어 글로우(fx_muzzle_00). 비면 글로우를 그리지 않는다.")]
        [SerializeField] Sprite _glowSprite;

        [Tooltip("동시에 그릴 수 있는 절 수. 체인 3기 × 8절 = 24가 현재 데이터 상한이다.")]
        [SerializeField] int _capacity = 48;

        readonly List<SpriteRenderer> _segments = new List<SpriteRenderer>(48);
        readonly List<SpriteRenderer> _arcs = new List<SpriteRenderer>(48);
        readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>(8);

        /// <summary>체인별 절 수 (관측에는 없다 — 이번 프레임 상태에서 센다).</summary>
        readonly Dictionary<int, int> _chainLengths = new Dictionary<int, int>(8);

        /// <summary>체인별 마지막 머리 HP — 감소하면 피격 플래시.</summary>
        readonly Dictionary<int, int> _lastHeadHp = new Dictionary<int, int>(8);
        readonly Dictionary<int, float> _hitFlashAge = new Dictionary<int, float>(8);
        readonly List<int> _staleChains = new List<int>(8);

        /// <summary>px_white 한 장의 실제 월드 크기 — 아크 스케일 계산의 분모.</summary>
        float _pixelUnitX = 1f;
        float _pixelUnitY = 1f;
        float _headSpriteWidth = 1f;
        float _bodySpriteWidth = 1f;
        float _glowSpriteWidth = 1f;

        int _activeSegments;
        int _activeArcs;
        int _activeGlows;

        /// <summary>개발 오버레이 관측값 — 지금 화면에 그려진 절 수.</summary>
        public int VisibleSegmentCount => _activeSegments;

        /// <summary>지금 살아 있는 체인 수.</summary>
        public int ActiveChainCount => _chainLengths.Count;

        void Start()
        {
            var parent = _root != null ? _root : transform;

            Sprite head = _headSprite != null ? _headSprite : _bodySprite;
            Sprite body = _bodySprite != null ? _bodySprite : _headSprite;
            _headSpriteWidth = SpriteWidth(head, 1f);
            _bodySpriteWidth = SpriteWidth(body, 1f);
            _glowSpriteWidth = SpriteWidth(_glowSprite, 1f);
            if (_pixelSprite != null)
            {
                Vector3 size = _pixelSprite.bounds.size;
                if (size.x > 0.0001f) _pixelUnitX = size.x;
                if (size.y > 0.0001f) _pixelUnitY = size.y;
            }

            int capacity = Mathf.Max(0, _capacity);
            for (int i = 0; i < capacity; i++)
            {
                // 머리는 0번 절뿐이지만 어느 슬롯이 머리가 될지는 프레임마다 다르다 —
                // 슬롯마다 스프라이트를 그때 바꿔 끼운다 (렌더러 생성은 하지 않는다).
                _segments.Add(CreateRenderer(parent, $"ChainSeg_{i:D2}", body, SegmentOrder));
                if (_pixelSprite != null)
                    _arcs.Add(CreateRenderer(parent, $"ChainArc_{i:D2}", _pixelSprite, ArcOrder));
            }
            if (_glowSprite != null)
            {
                // 글로우는 머리에만 붙으므로 체인 수만큼이면 된다.
                for (int i = 0; i < 8; i++)
                    _glows.Add(CreateRenderer(parent, $"ChainGlow_{i:D2}", _glowSprite, GlowOrder));
            }
        }

        static float SpriteWidth(Sprite sprite, float fallback)
        {
            if (sprite == null) return fallback;
            float width = sprite.bounds.size.x;
            return width > 0.0001f ? width : fallback;
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        void LateUpdate()
        {
            if (_director == null || _segments.Count == 0) return;

            var chains = _director.SegmentChains;
            if (chains == null || chains.Count == 0)
            {
                if (_activeSegments > 0 || _activeArcs > 0 || _activeGlows > 0) HideAll();
                if (_chainLengths.Count > 0)
                {
                    _chainLengths.Clear();
                    _lastHeadHp.Clear();
                    _hitFlashAge.Clear();
                }
                return;
            }

            MeasureChains(chains);
            Draw(chains);
        }

        /// <summary>
        /// 절 수는 관측에 없다. 체인마다 최대 SegmentIndex를 세어 점감 곡선의 분모를 만든다.
        /// 같은 패스에서 머리 HP 차분(피격 플래시)도 잡는다.
        /// </summary>
        void MeasureChains(IReadOnlyList<SegmentChainState> chains)
        {
            _chainLengths.Clear();
            for (int i = 0; i < chains.Count; i++)
            {
                var s = chains[i];
                if (_chainLengths.TryGetValue(s.ChainId, out int known))
                {
                    if (s.SegmentIndex + 1 > known)
                        _chainLengths[s.ChainId] = s.SegmentIndex + 1;
                }
                else
                {
                    _chainLengths.Add(s.ChainId, s.SegmentIndex + 1);
                }

                if (!s.IsHead) continue;
                if (_lastHeadHp.TryGetValue(s.ChainId, out int previous) && s.HeadHp < previous)
                    _hitFlashAge[s.ChainId] = 0f;
                _lastHeadHp[s.ChainId] = s.HeadHp;
            }

            // 사라진 체인의 HP/플래시 기록은 버린다 — ChainId가 재사용되면
            // 남은 값이 새 체인의 첫 프레임을 피격으로 오인하게 만든다.
            _staleChains.Clear();
            foreach (var pair in _lastHeadHp)
                if (!_chainLengths.ContainsKey(pair.Key))
                    _staleChains.Add(pair.Key);
            for (int i = 0; i < _staleChains.Count; i++)
            {
                _lastHeadHp.Remove(_staleChains[i]);
                _hitFlashAge.Remove(_staleChains[i]);
            }
        }

        void Draw(IReadOnlyList<SegmentChainState> chains)
        {
            bool flashReduced = _juice != null && _juice.FlashReduced;

            int halfWidthSubUnits = _director.SegmentChainHalfWidthSubUnits;
            if (halfWidthSubUnits <= 0) halfWidthSubUnits = FallbackHalfWidthSubUnits;
            float headWorldWidth = 2f * halfWidthSubUnits / SimSpace.SubUnitsPerWorldUnit;

            Sprite head = _headSprite != null ? _headSprite : _bodySprite;
            Sprite body = _bodySprite != null ? _bodySprite : _headSprite;

            int segmentSlot = 0;
            int arcSlot = 0;
            int glowSlot = 0;
            float time = Time.time;

            for (int i = 0; i < chains.Count && segmentSlot < _segments.Count; i++)
            {
                var state = chains[i];
                int length = _chainLengths.TryGetValue(state.ChainId, out int n) ? n : 1;
                float t = length > 1 ? (float)state.SegmentIndex / (length - 1) : 0f;

                Vector3 world = SimView.ToWorld(state.X, state.Y);
                var renderer = _segments[segmentSlot++];

                // ── 색: 피격 가능 여부가 밝기다 ─────────────────────────────
                Color color;
                float scale;
                if (state.Damageable)
                {
                    float flashAge = _hitFlashAge.TryGetValue(state.ChainId, out float a)
                        ? a : float.MaxValue;
                    if (flashAge < HitFlashSeconds)
                    {
                        _hitFlashAge[state.ChainId] = flashAge + Time.deltaTime;
                        color = Color.white;
                    }
                    else
                    {
                        // 남은 HP가 적을수록 빠르게 맥동한다 — Core가 준 HP만 읽는다.
                        float hpFraction = state.HeadMaxHp > 0
                            ? Mathf.Clamp01((float)state.HeadHp / state.HeadMaxHp)
                            : 1f;
                        float hz = hpFraction <= HeadLowHpFraction ? HeadLowHpPulseHz : HeadPulseHz;
                        float pulse = (Mathf.Sin(time * hz * Mathf.PI * 2f) + 1f) * 0.5f;
                        if (flashReduced) pulse *= 0.35f;
                        color = Color.Lerp(HeadTint, HeadHot, pulse);
                    }
                    scale = 1f;
                    if (renderer.sprite != head) renderer.sprite = head;
                }
                else
                {
                    // 무적 절: 뒤로 갈수록 어둡고 작다. 어둠 자체가 "안 통한다"는 신호다.
                    color = Color.Lerp(BodyNear, BodyFar, t);
                    scale = Mathf.Lerp(1f, TailScale, t);
                    if (renderer.sprite != body) renderer.sprite = body;
                }

                float spriteWidth = state.Damageable ? _headSpriteWidth : _bodySpriteWidth;
                float fit = spriteWidth > 0.0001f ? headWorldWidth / spriteWidth : 1f;
                var transformRef = renderer.transform;
                transformRef.localPosition = world;
                transformRef.localScale = new Vector3(fit * scale, fit * scale, 1f);
                renderer.color = color;
                if (!renderer.enabled) renderer.enabled = true;

                // ── 머리 방전 글로우 ────────────────────────────────────────
                if (state.Damageable && glowSlot < _glows.Count)
                {
                    var glow = _glows[glowSlot++];
                    float glowPulse = (Mathf.Sin(time * HeadPulseHz * Mathf.PI * 2f + 1.2f) + 1f) * 0.5f;
                    float alpha = Mathf.Lerp(0.28f, 0.62f, glowPulse);
                    if (flashReduced) alpha *= 0.5f;
                    float size = headWorldWidth * GlowSizeFactor
                        * Mathf.Lerp(0.92f, 1.08f, glowPulse);
                    float glowFit = _glowSpriteWidth > 0.0001f ? size / _glowSpriteWidth : 1f;
                    var glowTransform = glow.transform;
                    glowTransform.localPosition = world;
                    glowTransform.localScale = new Vector3(glowFit, glowFit, 1f);
                    glow.color = new Color(GlowTint.r, GlowTint.g, GlowTint.b, alpha);
                    if (!glow.enabled) glow.enabled = true;
                }

                // ── 절 사이 낙뢰 아크 ───────────────────────────────────────
                // 다음 상태가 같은 체인의 다음 절일 때만 잇는다. Core는 체인 단위로
                // 연속 배치해 주므로 인접 비교로 충분하다.
                if (arcSlot >= _arcs.Count || i + 1 >= chains.Count) continue;
                var next = chains[i + 1];
                if (next.ChainId != state.ChainId) continue;

                Vector3 nextWorld = SimView.ToWorld(next.X, next.Y);
                Vector3 delta = nextWorld - world;
                float distance = delta.magnitude;
                if (distance <= 0.02f) continue;

                var arc = _arcs[arcSlot++];
                // 절마다 위상을 어긋내 한 줄로 동시에 껌뻑이지 않게 한다.
                float phase = state.ChainId * 1.7f + state.SegmentIndex * 0.9f;
                float flicker = (Mathf.Sin(time * ArcFlickerHz * Mathf.PI * 2f + phase) + 1f) * 0.5f;
                float arcAlpha = Mathf.Lerp(ArcAlphaMin, ArcAlphaMax, flicker);
                float thickness = Mathf.Lerp(ArcThicknessMin, ArcThicknessMax, flicker);
                if (flashReduced)
                {
                    arcAlpha = (ArcAlphaMin + ArcAlphaMax) * 0.5f;
                    thickness = (ArcThicknessMin + ArcThicknessMax) * 0.5f;
                }
                // 꼬리로 갈수록 아크도 가늘어진다 — 굵기 정보가 절 크기와 어긋나면 안 된다.
                thickness *= Mathf.Lerp(1f, TailScale, t);

                var arcTransform = arc.transform;
                arcTransform.localPosition = world + delta * 0.5f;
                arcTransform.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                // px_white는 2px 스프라이트라 월드 길이를 그대로 스케일에 넣으면 1/8로
                // 그려진다 (LaserBeamView가 2026-08-02에 고친 것과 같은 함정).
                arcTransform.localScale = new Vector3(
                    distance / _pixelUnitX, thickness / _pixelUnitY, 1f);
                arc.color = new Color(ArcTint.r, ArcTint.g, ArcTint.b, arcAlpha);
                if (!arc.enabled) arc.enabled = true;
            }

            DisableFrom(_segments, segmentSlot, _activeSegments);
            DisableFrom(_arcs, arcSlot, _activeArcs);
            DisableFrom(_glows, glowSlot, _activeGlows);
            _activeSegments = segmentSlot;
            _activeArcs = arcSlot;
            _activeGlows = glowSlot;
        }

        /// <summary>지난 프레임에 켰던 슬롯만 끈다 — 매 프레임 전체 순회를 피한다.</summary>
        static void DisableFrom(List<SpriteRenderer> renderers, int from, int previousActive)
        {
            int end = Mathf.Min(previousActive, renderers.Count);
            for (int i = from; i < end; i++) renderers[i].enabled = false;
        }

        void HideAll()
        {
            DisableFrom(_segments, 0, _activeSegments);
            DisableFrom(_arcs, 0, _activeArcs);
            DisableFrom(_glows, 0, _activeGlows);
            _activeSegments = 0;
            _activeArcs = 0;
            _activeGlows = 0;
        }
    }
}
