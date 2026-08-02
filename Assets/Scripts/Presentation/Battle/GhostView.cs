using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// St5 타임루프 고스트 뷰 (REQ-109 관측 소비).
    ///
    /// Core는 최종 바이옴 Closing에서 St1에 기록해 둔 내 입력을 그대로 재생해
    /// <see cref="Shmup.Core.Simulation.GhostState"/>를 굴린다. 그 상태는 판정에
    /// 참여하지만(고스트 탄은 실제 데미지를 준다) **본체는 피격·충돌이 없다**.
    /// 그래서 화면에서 반드시 "지금 이 기체는 내가 아니다"로 읽혀야 한다 —
    /// 살아 있는 아군처럼 보이면 플레이어가 저 몸을 방패로 믿고 죽는다.
    ///
    /// 세 가지로 가른다:
    ///   1. 반투명 본체 (알파 0.45) — 실체가 아니라는 첫 신호
    ///   2. 시안 틴트 — 화면의 어떤 아군/적 색과도 겹치지 않는 유일한 색
    ///   3. 0.2초 간격 위치 잔상 3개 — 지금이 아니라 **지나간 시간**에서 온 궤적
    ///
    /// 잔상은 보간하지 않는다. 픽셀 퍼펙트 원칙대로 0.2초마다 위치를 통째로
    /// 물려받아 계단처럼 남는다 — 부드럽게 이으면 잔상이 아니라 모션 블러가 되고,
    /// "시간이 어긋나 있다"는 인상이 사라진다.
    ///
    /// 순수 표현이다. 위치·발사·수명은 전부 Core가 정하고 여기서는 그리기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GhostView : MonoBehaviour
    {
        /// <summary>
        /// 고스트 본체 틴트. 시안은 이 게임에서 아무도 쓰지 않는 색이다 — 아군 기체는
        /// 무채색+앰버, 적탄은 흰색/주황/핑크, 실드만 청색 계열이라 채도로 갈린다.
        /// </summary>
        public static readonly Color Tint = new Color(0.42f, 0.92f, 1f, 1f);

        /// <summary>
        /// 고스트 주무기 탄 색 (BattleDirector가 <c>BulletKind.GhostMainShot</c>에 쓴다).
        /// 본체와 같은 시안이되 **알파는 1**이다: 탄은 실제로 적을 깎으므로 반투명하게
        /// 그리면 "안 맞는 탄"으로 오독된다. 출처만 같은 색으로 말하고 위력은 숨기지 않는다.
        /// </summary>
        public static readonly Color BulletTint = new Color(0.55f, 0.97f, 1f, 1f);

        /// <summary>본체 알파. 뒤 배경이 비쳐야 "저건 실체가 아니다"가 즉시 읽힌다.</summary>
        public const float BodyAlpha = 0.45f;

        const int TrailCount = 3;

        /// <summary>잔상 샘플 간격(초). 짧으면 뭉쳐 잔상이 아니라 굵은 몸이 된다.</summary>
        const float TrailIntervalSeconds = 0.2f;

        /// <summary>잔상 알파 — 본체 대비 감쇠. 마지막 한 조각은 거의 사라진다.</summary>
        static readonly float[] TrailAlphaScale = { 0.58f, 0.34f, 0.19f };

        /// <summary>등장은 빠르게(합류가 늦게 읽히면 안 된다), 퇴장은 여운을 남긴다.</summary>
        const float FadeInSeconds = 0.3f;
        const float FadeOutSeconds = 0.6f;

        /// <summary>플레이어(정렬 10)보다 뒤. 겹쳤을 때 내 기체가 가려지면 안 된다.</summary>
        const int BodySortingOffset = -2;
        const int TrailSortingOffset = -3;

        SpriteRenderer _playerRenderer;
        SpriteRenderer _body;
        readonly SpriteRenderer[] _trail = new SpriteRenderer[TrailCount];
        readonly Vector3[] _trailPositions = new Vector3[TrailCount];

        Vector3 _position;
        bool _active;
        float _visibility;
        float _sampleAge;
        bool _snapTrail = true;
        bool _shown = true;   // SetRenderersEnabled(false)가 첫 호출에서 실제로 끄도록
        Sprite _shownSprite;

        /// <summary>고스트가 지금 화면에 있는가 (페이드 아웃 중에도 참).</summary>
        public bool Visible => _visibility > 0.001f;

        /// <summary>
        /// 플레이어 기체를 원본으로 삼아 고스트 렌더러 묶음을 만든다. 씬에 프리팹을
        /// 두지 않는 이유는 하나다: **고스트는 기체 스프라이트를 그대로 물려받아야**
        /// 하는데, 그 스프라이트는 선택 함선에 따라 런타임에 정해진다. 프리팹으로
        /// 굳히면 어떤 함선으로 출격해도 같은 실루엣이 나와 "과거의 나"가 깨진다.
        /// </summary>
        public static GhostView Create(Transform playerTransform)
        {
            if (playerTransform == null) return null;
            var playerRenderer = playerTransform.GetComponent<SpriteRenderer>();
            if (playerRenderer == null) return null;

            var root = new GameObject("Ghost");
            root.transform.SetParent(playerTransform.parent, false);
            var view = root.AddComponent<GhostView>();
            view._playerRenderer = playerRenderer;

            // 잔상이 본체보다 먼저 생성돼야 같은 정렬 순서 안에서 뒤에 깔린다.
            for (int i = 0; i < TrailCount; i++)
                view._trail[i] = view.CreateRenderer(
                    "GhostTrail" + i, playerRenderer, TrailSortingOffset);
            view._body = view.CreateRenderer("GhostBody", playerRenderer, BodySortingOffset);

            // 루트는 계속 활성이다. 꺼 버리면 LateUpdate가 돌지 않아 합류 프레임에
            // 스스로 다시 켜질 수 없다 — 보이지 않는 상태는 렌더러 enabled로만 만든다.
            view.SetRenderersEnabled(false);
            return view;
        }

        SpriteRenderer CreateRenderer(string name, SpriteRenderer template, int sortingOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = template.sprite;
            renderer.sortingLayerID = template.sortingLayerID;
            renderer.sortingOrder = template.sortingOrder + sortingOffset;
            renderer.color = new Color(Tint.r, Tint.g, Tint.b, 0f);
            return renderer;
        }

        void SetRenderersEnabled(bool on)
        {
            if (_shown == on) return;
            _shown = on;
            if (_body != null) _body.enabled = on;
            for (int i = 0; i < TrailCount; i++)
                if (_trail[i] != null) _trail[i].enabled = on;
        }

        /// <summary>
        /// 매 스텝 Core의 고스트 상태를 그대로 받는다. 좌표 변환 외에 아무 판정도 하지 않는다.
        /// </summary>
        public void Sync(bool active, int subX, int subY)
        {
            _active = active;
            if (active) _position = SimView.ToWorld(subX, subY);
        }

        /// <summary>
        /// <c>GhostSpawned</c> — 합류 프레임. 잔상을 등장 지점으로 모아 두지 않으면
        /// 원점(0,0)에서 날아온 것처럼 긴 꼬리가 한 번 그려진다.
        /// </summary>
        public void OnSpawned(int subX, int subY)
        {
            _position = SimView.ToWorld(subX, subY);
            _active = true;
            _snapTrail = true;
            _sampleAge = 0f;
        }

        /// <summary>
        /// <c>GhostEnded</c> — 기록이 끝났거나 최종 보스가 먼저 끝났다. 위치는 마지막
        /// 지점에 그대로 두고 알파만 빼서 "그 자리에서 흩어진다"로 읽히게 한다.
        /// </summary>
        public void OnEnded()
        {
            _active = false;
        }

        /// <summary>
        /// 배틀 인스턴스 교체(방 전환). 고스트는 Closing→최종 보스 경계를 넘어 살아
        /// 있으므로 숨기지 않는다 — 다만 좌표가 한 프레임에 크게 튈 수 있어 잔상만 접는다.
        /// </summary>
        public void ResetTrail()
        {
            _snapTrail = true;
            _sampleAge = 0f;
        }

        /// <summary>재출격/타이틀 복귀 — 다음 런에 잔상이 새지 않게 통째로 지운다.</summary>
        public void Hide()
        {
            _active = false;
            _visibility = 0f;
            _snapTrail = true;
            SetRenderersEnabled(false);
        }

        /// <summary>
        /// 위치는 FixedUpdate(시뮬 틱)에서 오고 페이드/잔상 타이밍은 실시간이라
        /// 여기서 합친다. LateUpdate라 같은 프레임의 위치 갱신을 놓치지 않는다.
        /// </summary>
        void LateUpdate()
        {
            if (_body == null) return;

            float dt = Time.deltaTime;
            float target = _active ? 1f : 0f;
            if (_visibility < target)
                _visibility = Mathf.Min(target, _visibility + dt / FadeInSeconds);
            else if (_visibility > target)
                _visibility = Mathf.Max(target, _visibility - dt / FadeOutSeconds);

            bool show = _visibility > 0.001f;
            SetRenderersEnabled(show);
            if (!show) return;

            if (_snapTrail)
            {
                _snapTrail = false;
                for (int i = 0; i < TrailCount; i++) _trailPositions[i] = _position;
            }
            else if (_active)
            {
                _sampleAge += dt;
                if (_sampleAge >= TrailIntervalSeconds)
                {
                    // 프레임이 크게 튀어도 잔상 간격은 한 칸만 민다 — 남은 시간을
                    // 이월하면 렉 한 번에 잔상 전체가 같은 자리로 무너진다.
                    _sampleAge = 0f;
                    for (int i = TrailCount - 1; i > 0; i--)
                        _trailPositions[i] = _trailPositions[i - 1];
                    _trailPositions[0] = _position;
                }
            }

            // 함선 스프라이트는 런타임에 정해지고 엔진 프레임 애니로 계속 바뀐다 —
            // 참조가 실제로 달라진 프레임에만 네 렌더러에 옮긴다.
            var sprite = _playerRenderer != null ? _playerRenderer.sprite : null;
            if (sprite != null && !ReferenceEquals(sprite, _shownSprite))
            {
                _shownSprite = sprite;
                _body.sprite = sprite;
                for (int i = 0; i < TrailCount; i++)
                    if (_trail[i] != null) _trail[i].sprite = sprite;
            }

            _body.transform.localPosition = _position;
            var color = Tint;
            color.a = BodyAlpha * _visibility;
            _body.color = color;

            for (int i = 0; i < TrailCount; i++)
            {
                var renderer = _trail[i];
                if (renderer == null) continue;
                renderer.transform.localPosition = _trailPositions[i];
                color.a = BodyAlpha * TrailAlphaScale[i] * _visibility;
                renderer.color = color;
            }
        }
    }
}
