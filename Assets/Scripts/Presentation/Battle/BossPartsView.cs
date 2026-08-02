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
        const float FlashDuration = 0.09f;

        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] Sprite _markSprite;        // 1px 흰색 사각 (틴트로 재사용)

        /// <summary>
        /// St3 거대 전함(REQ-110/111)이 화면에 있는 동안은 이 범용 오버레이가 비켜난다.
        /// 전함은 파츠마다 실제 스프라이트를 얹으므로 그 위에 회색 사각까지 겹치면
        /// 포탑이 뭉개지고, 파괴/무적 표현이 두 컴포넌트에서 이중으로 나온다.
        /// </summary>
        [SerializeField] WarshipView _warshipView;

        /// <summary>
        /// 코어 무적 표시용 **테두리** 스프라이트. 9-슬라이스라 어떤 크기로 늘려도
        /// 테두리는 1px로 남는다.
        /// </summary>
        Sprite _ringSprite;

        void Awake()
        {
            EnsureRingSprite();
            if (_markSprite != null) return;
            // 흰색 1px 스프라이트를 런타임 생성 (틴트만 바꿔 쓰므로 아트 파일이 불필요)
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _markSprite = Sprite.Create(
                texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 16f);
        }

        /// <summary>
        /// 코어 무적("아직 못 깎는다")을 **채워진 네모**로 그리면 실드가 아니라 UI 오류로
        /// 읽힌다 — 사람 플레이 스크린샷(2026-08-03, St3 hive)에서 보스 위에 반투명
        /// 청록 사각형이 얹혀 "이게 뭐냐"고 지적된 것이 이것이다. 게다가 파츠 판정 크기로
        /// 맞추면서 더 커졌다(hive 코어 7×5u). 테두리로 바꾸면 같은 자리에서 같은 정보를
        /// 주면서 보스 아트를 가리지 않는다.
        ///
        /// 8×8 텍스처의 1px 흰 테두리 + border 2px. 9-슬라이스는 모서리(2px)를 그대로
        /// 두고 가운데만 늘리므로, 크기를 키워도 선 굵기가 안 변한다.
        /// </summary>
        void EnsureRingSprite()
        {
            if (_ringSprite != null) return;
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    texture.SetPixel(x, y, edge ? Color.white : clear);
                }
            texture.Apply();
            _ringSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(2f, 2f, 2f, 2f));
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
                && (_warshipView == null || !_warshipView.Active);
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
                overlay.transform.localPosition = SimView.ToWorld(part.X, part.Y);
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
                    // 파괴: 그을린 반투명 검정
                    color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
                }
                else if (age < FlashDuration)
                {
                    _flashAge[part.PartId] = age + Time.deltaTime;
                    color = new Color(1f, 1f, 1f, 0.55f);
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
