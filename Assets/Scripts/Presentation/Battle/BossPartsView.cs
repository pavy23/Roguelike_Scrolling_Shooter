using System.Collections.Generic;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 초대형 보스 파츠 뷰 (REQ-035). 본체 스프라이트 한 장 위에 파츠별 상태를 겹쳐 그린다.
    /// - 파츠 피격: 흰색 플래시
    /// - 파괴: 검게 그을린 오버레이 + 폭발 (본체 스프라이트는 그대로 두고 상태만 표현)
    /// - 코어 무적: 청록 실드 링을 코어 위에 표시
    /// 순수 표현 — Core의 BossParts 목록을 읽기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPartsView : MonoBehaviour
    {
        const float FlashDuration = 0.12f;

        /// <summary>피격 플래시 최대 알파. 이 값 그대로 나오는 것은 작은 파츠뿐이다.</summary>
        const float MaxFlashAlpha = 0.62f;

        /// <summary>
        /// 피격 채움 색. **빨강이다.**
        ///
        /// 흰색이었을 때는 밝은 보스 아트 위에서 그냥 "하얀 박스"로 읽혔다 (사람 보고
        /// 2026-08-04). 맞고 있다는 신호는 배경·보스 아트와 갈라져야 하고, 무적을
        /// 뜻하는 청록 테두리와도 갈라져야 한다 — 색이 곧 문법이다.
        /// </summary>
        static readonly Color HitTint = new Color(1f, 0.28f, 0.24f, 1f);

        /// <summary>
        /// 이 면적(월드 유닛²)까지는 플래시를 최대 알파로 얹는다. 그보다 큰 파츠는
        /// 면적에 반비례해 옅어진다 — 4×4유닛(=16) 언저리가 "번쩍였다"로 읽히는 상한이다.
        /// </summary>
        const float FlashAreaReference = 16f;

        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] Sprite _markSprite;        // 1px 흰색 사각 (틴트로 재사용)

        /// <summary>
        /// St3 거대 전함(REQ-110/111)이 화면에 있는 동안은 이 범용 오버레이가 비켜난다.
        /// 전함은 파츠마다 실제 스프라이트를 얹으므로 그 위에 회색 사각까지 겹치면
        /// 포탑이 뭉개지고, 파괴/무적 표현이 두 컴포넌트에서 이중으로 나온다.
        /// </summary>
        [SerializeField] WarshipView _warshipView;
        [Tooltip("하이브 조립 뷰가 화면을 소유하면 범용 오버레이는 비켜난다.")]
        [SerializeField] HiveBossView _hiveView;

        /// <summary>
        /// 코어 무적 표시용 **테두리** 스프라이트. 9-슬라이스라 어떤 크기로 늘려도
        /// 테두리는 1px로 남는다.
        /// </summary>
        Sprite _ringSprite;

        void Awake()
        {
            EnsureRingSprite();
            if (_markSprite != null) return;
            // 가장자리가 부드러운 **원형 감쇠** 스프라이트를 런타임 생성한다.
            //
            // 예전에는 흰색 1px이었다. 그걸 파츠 크기로 늘리면 가장자리가 칼같이
            // 잘린 직사각형이 되어, 색을 무엇으로 하든 "판때기"로 읽혔다 (흰색일 때
            // 지적받고 빨강으로 바꿨더니 이번엔 붉은 판이 됐다). 가장자리가 흐려지면
            // 같은 알파라도 **빛**으로 읽힌다.
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 정규화 반경 → 안쪽은 꽉 차고 바깥 30%에서만 사그라진다.
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.InverseLerp(0.55f, 1f, r));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            texture.Apply();
            _markSprite = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f);
        }

        /// <summary>
        /// 코어 무적("아직 못 깎는다")을 **채워진 네모**로 그리면 실드가 아니라 UI 오류로
        /// 읽힌다 — 사람 플레이 스크린샷(2026-08-03, St3 hive)에서 보스 위에 반투명
        /// 청록 사각형이 얹혀 "이게 뭐냐"고 지적된 것이 이것이다. 게다가 파츠 판정 크기로
        /// 맞추면서 더 커졌다(hive 코어 7×5u). 테두리로 바꾸면 같은 자리에서 같은 정보를
        /// 주면서 보스 아트를 가리지 않는다.
        ///
        /// **네모 테두리는 쓰지 않는다** — 사람이 "네모 상자가 보이는데 피격 박스가
        /// 보이는 거냐"고 물었다(2026-08-03, 에일리언형 보스). 닫힌 사각형은 이 바닥에서
        /// 디버그 히트박스의 관용 표현이라, 정보가 아니라 개발 잔재로 읽힌다.
        ///
        /// 대신 **네 모서리 브래킷**을 그린다. 조준·잠금의 관용 표현이라 "여기가 목표인데
        /// 지금은 잠겨 있다"로 읽히고, 변이 없어 히트박스로 오해되지 않는다.
        ///
        /// 16×16 텍스처에 모서리 L자만 그리고 border 5px로 9-슬라이스한다. 9-슬라이스는
        /// 모서리를 원본 크기로 두고 변·가운데만 늘리는데, 그 변·가운데가 전부 투명이라
        /// 파츠가 아무리 커도 브래킷 크기가 변하지 않는다.
        /// </summary>
        void EnsureRingSprite()
        {
            if (_ringSprite != null) return;
            const int size = 16;
            const int arm = 5;    // 모서리에서 뻗는 팔 길이(px) — border와 같아야 안 늘어난다
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool nearLeft = x < arm, nearRight = x >= size - arm;
                    bool nearBottom = y < arm, nearTop = y >= size - arm;
                    bool inCorner = (nearLeft || nearRight) && (nearBottom || nearTop);
                    // 모서리 칸 안에서도 L자만 남긴다: 가장자리 1px 두 줄.
                    bool onEdgeLine =
                        x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, inCorner && onEdgeLine ? Color.white : clear);
                }
            texture.Apply();
            _ringSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(arm, arm, arm, arm));
        }

        readonly Dictionary<string, SpriteRenderer> _overlays =
            new Dictionary<string, SpriteRenderer>(12);
        readonly Dictionary<string, int> _lastHp = new Dictionary<string, int>(12);
        readonly Dictionary<string, float> _flashAge = new Dictionary<string, float>(12);
        readonly List<string> _keys = new List<string>(12);

        void Update()
        {
            if (_director == null || _root == null || _markSprite == null) return;
            var parts = _director.BossParts;
            bool active = _director.BossActive && parts != null && parts.Count > 0
                && (_warshipView == null || !_warshipView.Active)
                && (_hiveView == null || !_hiveView.Active);
            if (!active)
            {
                if (_overlays.Count > 0) HideAll();
                return;
            }

            var bossWorld = _director.BossWorldPosition;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var overlay = GetOverlay(part.PartId);
                // 본체 그림이 숨쉬며 흔들리므로(BattleDirector.BossIdleOffsetY) 파츠
                // 표시도 같은 오프셋을 탄다 — 둘이 따로 놀면 피격 표시가 몸에서
                // 떨어져 나온 것처럼 보인다.
                overlay.transform.localPosition = SimView.ToWorld(part.X, part.Y)
                    + new Vector3(0f, _director != null ? _director.BossIdleOffsetY : 0f, 0f);
                // 오버레이 크기 = 파츠 **판정** 크기. 예전에는 전 파츠 공통 3.5×3.5 고정이라
                // 피격 플래시·파괴 그을림이 실제로 맞는 범위와 어긋났다 — 작은 포탑에는
                // 과하게 크고, 큰 파츠에는 모자랐다. 방마다 정의가 바뀌므로 매 프레임 맞춘다.
                var partDefinition = FindPartDefinition(part.PartId);
                if (partDefinition != null)
                    overlay.size = new Vector2(
                        2f * partDefinition.HalfWidth * SimView.WorldUnitsPerSubUnit,
                        2f * partDefinition.HalfHeight * SimView.WorldUnitsPerSubUnit);

                // 피격 감지 → 플래시
                if (_lastHp.TryGetValue(part.PartId, out int previous)
                    && part.Hp < previous && !part.Destroyed)
                    _flashAge[part.PartId] = 0f;
                _lastHp[part.PartId] = part.Hp;

                float age = _flashAge.TryGetValue(part.PartId, out float a) ? a : float.MaxValue;
                Color color;
                // 채움(그을림·피격 플래시)과 테두리(무적 실드)를 스프라이트로 가른다.
                // 무적은 오래 켜져 있는 상태라 채우면 보스 아트를 통째로 덮는다.
                bool ring = false;
                if (part.Destroyed)
                {
                    // 파괴 자국. 예전에는 0.72 알파의 회흑색 한 겹이었는데, 크고
                    // 복잡한 히든 보스 그림 위에서는 **그림자로 읽혔다** — 사람이
                    // "부서진 부위는 지금보다 더 부서진 느낌이 나게. 지금은 티가
                    // 잘 안나네"라고 했다(2026-08-04).
                    //
                    // 두 가지를 바꾼다.
                    //   1. 훨씬 짙게(0.92) 덮어 아래 그림을 확실히 지운다. 반투명은
                    //      "어두워졌다"이지 "없어졌다"가 아니다.
                    //   2. 잔불이 느리게 명멸한다. 완전히 죽은 검정은 배경 구멍처럼
                    //      보이지만, 숨 쉬듯 붉은 기가 오가면 **타고 있는 잔해**로
                    //      읽힌다. 주기를 파츠마다 어긋나게 해 한꺼번에 깜빡이지
                    //      않게 한다(같은 박자로 뛰면 화면 전체가 맥동한다).
                    float ember = (Mathf.Sin(
                        Time.time * 1.7f + part.PartId.GetHashCode() * 0.37f)
                        + 1f) * 0.5f;
                    color = new Color(
                        0.06f + 0.16f * ember,
                        0.04f + 0.05f * ember,
                        0.05f,
                        0.92f);
                }
                else if (age < FlashDuration)
                {
                    _flashAge[part.PartId] = age + Time.deltaTime;
                    // 색은 **빨강**이다 — 흰색은 배경·보스 아트와 섞여 그냥 밝은 판으로
                    // 읽혔다 (사람 보고 2026-08-04: "피격받을때 하얀색 박스만 보여서
                    // 이상해. 파츠 붉은 빛 나게 해줘"). 무적의 청록과도 갈라진다.
                    //
                    // 크기별로 **채움/테두리를 갈랐던 규칙은 되돌렸다.** 그 규칙은 마크가
                    // 각진 1px 사각형이던 시절의 대응이었는데, 지금 마크는 가장자리가
                    // 사그라지는 원형이라 큰 파츠를 채워도 판때기가 되지 않는다.
                    //
                    // 되돌린 이유는 실제 증상이다: 레비아탄은 파츠 10개 중 7개가 커서
                    // 전부 테두리로 갔고, 테두리는 0.12초 동안 1px 선이라 사실상 안
                    // 보였다 (사람 보고 2026-08-04: "레비아탄은 어딜 때리고 있는지
                    // 전혀 피격 이팩트가 없어서 모르겠어").
                    //
                    // 대신 면적에 반비례해 옅게 한다 — 작은 포탑은 또렷한 스파크,
                    // 큰 파츠는 넓게 번지는 홍조. 다만 **바닥을 둔다**: 아무리 커도
                    // 최대의 40%까지만 옅어진다. 안 보이는 피드백은 없는 것과 같다.
                    float area = overlay.size.x * overlay.size.y;
                    float sizeScale = Mathf.Clamp(
                        FlashAreaReference / Mathf.Max(area, 0.01f), 0.4f, 1f);
                    float decay = 1f - Mathf.Clamp01(age / FlashDuration);
                    color = new Color(
                        HitTint.r, HitTint.g, HitTint.b,
                        MaxFlashAlpha * sizeScale * decay);
                }
                else if (part.IsCore && part.CoreGated)
                {
                    // 코어 무적: 청록 맥동 **테두리**. 선이라 알파를 올려도 안 가린다.
                    float pulse = (Mathf.Sin(Time.time * 4.5f) + 1f) * 0.5f;
                    color = new Color(0.35f, 0.85f, 1f, 0.55f + pulse * 0.35f);
                    ring = true;
                }
                else
                {
                    color = Color.clear;   // 정상 상태에서는 본체 아트만 보인다
                }
                var wanted = ring && _ringSprite != null ? _ringSprite : _markSprite;
                if (overlay.sprite != wanted) overlay.sprite = wanted;
                overlay.color = color;
                overlay.enabled = color.a > 0.01f;
            }
        }

        BossPartDefinition FindPartDefinition(string partId)
        {
            var definitions = _director != null ? _director.BossPartDefinitions : null;
            if (definitions == null) return null;
            for (int i = 0; i < definitions.Count; i++)
                if (string.Equals(definitions[i].PartId, partId, System.StringComparison.Ordinal))
                    return definitions[i];
            return null;
        }

        SpriteRenderer GetOverlay(string partId)
        {
            if (_overlays.TryGetValue(partId, out var existing)) return existing;
            var go = new GameObject($"Part_{partId}");
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _markSprite;
            renderer.sortingOrder = 16;   // 보스(15) 위
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(3.5f, 3.5f);
            renderer.enabled = false;
            _overlays.Add(partId, renderer);
            _keys.Add(partId);
            return renderer;
        }

        void HideAll()
        {
            for (int i = 0; i < _keys.Count; i++)
                if (_overlays.TryGetValue(_keys[i], out var overlay))
                    overlay.enabled = false;
            _lastHp.Clear();
            _flashAge.Clear();
        }
    }
}
