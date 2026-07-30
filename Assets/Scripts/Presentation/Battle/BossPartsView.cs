using System.Collections.Generic;
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

        void Awake()
        {
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

        readonly Dictionary<string, SpriteRenderer> _overlays =
            new Dictionary<string, SpriteRenderer>(12);
        readonly Dictionary<string, int> _lastHp = new Dictionary<string, int>(12);
        readonly Dictionary<string, float> _flashAge = new Dictionary<string, float>(12);
        readonly List<string> _keys = new List<string>(12);

        void Update()
        {
            if (_director == null || _root == null || _markSprite == null) return;
            var parts = _director.BossParts;
            bool active = _director.BossActive && parts != null && parts.Count > 0;
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

                // 피격 감지 → 플래시
                if (_lastHp.TryGetValue(part.PartId, out int previous)
                    && part.Hp < previous && !part.Destroyed)
                    _flashAge[part.PartId] = 0f;
                _lastHp[part.PartId] = part.Hp;

                float age = _flashAge.TryGetValue(part.PartId, out float a) ? a : float.MaxValue;
                Color color;
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
                    // 코어 무적: 청록 맥동
                    float pulse = (Mathf.Sin(Time.time * 4.5f) + 1f) * 0.5f;
                    color = new Color(0.35f, 0.85f, 1f, 0.28f + pulse * 0.22f);
                }
                else
                {
                    color = Color.clear;   // 정상 상태에서는 본체 아트만 보인다
                }
                overlay.color = color;
                overlay.enabled = color.a > 0.01f;
            }
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
