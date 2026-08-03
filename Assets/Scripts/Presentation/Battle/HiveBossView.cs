using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 하이브 보스 조립 뷰 (사람 지시 2026-08-03).
    ///
    /// 요구는 셋이었다: ① 위아래로 길쭉한 체형, ② 머리에 처음엔 총알이 안 먹는 실드,
    /// ③ **다리를 부수면 실드가 사라지고 다리가 잘리는 연출**.
    ///
    /// ③이 구조를 결정했다 — 다리가 본체 그림에 그려져 있으면 잘라낼 수 없다.
    /// 그래서 전함(WarshipView)과 같은 방식으로 **조립**한다:
    ///   - 몸통 = boss_hive_torso (다리 없이 그린 그림)
    ///   - 다리 = boss_hive_leg 를 파츠 좌표에 좌우로 (오른쪽은 뒤집는다)
    ///   - 실드 = fx_shield_dome 을 머리 위에 (코어가 잠긴 동안만)
    ///
    /// 판정은 전부 Core가 정한다. 여기서는 Core가 준 파츠 상태를 **그리기만** 한다 —
    /// 어떤 다리가 부서졌는지도, 실드가 언제 풀리는지도 Core의 CoreGated가 말한다.
    /// (CLAUDE.md: Presentation은 게임플레이 판정을 하지 않는다.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HiveBossView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] JuiceDirector _juice;

        [Tooltip("다리 없는 몸통 — art-input/boss_hive_torso.png")]
        [SerializeField] Sprite _torsoSprite;
        [Tooltip("다리 1개 — art-input/boss_hive_leg.png (오른쪽은 flipX)")]
        [SerializeField] Sprite _legSprite;
        [Tooltip("머리 실드 돔 — art-input/fx_shield_dome.png")]
        [SerializeField] Sprite _shieldSprite;

        /// <summary>이 보스에서만 조립을 켠다. 다른 보스는 기존 본체 렌더러 그대로다.</summary>
        const string HiveBossIdPrefix = "boss_hive";

        // 정렬: 몸통(15)은 보스 본체와 같은 층, 다리는 그 뒤(14), 실드는 앞(17).
        // 실드가 머리 앞에 있어야 "덮여 있다"로 읽힌다.
        const int TorsoOrder = 15;
        const int LegOrder = 14;
        const int ShieldOrder = 17;

        /// <summary>실드가 깨지는 데 걸리는 시간(초). 짧게 — 해방의 순간이지 연출 대기가 아니다.</summary>
        const float ShieldBreakSeconds = 0.45f;

        /// <summary>
        /// 몸통 스프라이트 중심의 y 오프셋(월드 유닛).
        ///
        /// 판정 원점은 실루엣 **전체**의 중심(±7.25u)인데 몸통 그림은 위쪽 10유닛만
        /// 차지한다(다리를 따로 그리므로). 그래서 몸통을 원점에 놓으면 아래로 처져
        /// 다리와 2.5유닛 겹쳐야 할 자리가 벌어진다 — GROK의 좌표 보고(REQ-138)에
        /// 명시된 값이다. 데이터가 바뀌면 이 값도 같이 바뀐다.
        /// </summary>
        const float TorsoCenterOffsetY = 2.25f;

        SpriteRenderer _torso;
        SpriteRenderer _shield;
        readonly List<SpriteRenderer> _legs = new List<SpriteRenderer>(4);
        readonly List<bool> _legDestroyed = new List<bool>(4);

        bool _visible;
        bool _shieldWasUp;
        float _shieldBreakAge = float.MaxValue;

        /// <summary>전함 뷰처럼, 이 뷰가 화면을 소유하면 범용 파츠 오버레이는 비켜난다.</summary>
        public bool Active => _visible;

        void Update()
        {
            var parts = _director != null ? _director.BossParts : null;
            string bossId = _director != null && _director.BossStageId != null
                ? _director.BossStageId : null;
            bool active = _director != null
                && _director.BossActive
                && parts != null && parts.Count > 0
                && _torsoSprite != null
                && bossId != null
                && bossId.StartsWith(HiveBossIdPrefix, System.StringComparison.Ordinal);

            if (!active)
            {
                if (_visible) Hide();
                return;
            }

            Vector3 bossWorld = _director.BossWorldPosition;
            SyncTorso(bossWorld);
            SyncLegs(parts);
            SyncShield(parts, bossWorld);
            _visible = true;
        }

        void SyncTorso(Vector3 bossWorld)
        {
            _torso = Ensure(_torso, "HiveTorso", _torsoSprite, TorsoOrder);
            if (_torso == null) return;
            _torso.transform.localPosition =
                bossWorld + new Vector3(0f, TorsoCenterOffsetY, 0f);
            _torso.enabled = true;
        }

        void SyncLegs(IReadOnlyList<BossPartState> parts)
        {
            int legIndex = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.IsCore) continue;   // 코어는 몸통 그림 안에 있다

                while (_legs.Count <= legIndex)
                {
                    _legs.Add(null);
                    _legDestroyed.Add(false);
                }
                var leg = _legs[legIndex];
                if (leg == null)
                {
                    leg = Ensure(null, $"HiveLeg_{legIndex}", _legSprite, LegOrder);
                    _legs[legIndex] = leg;
                }

                if (leg != null)
                {
                    // 좌우 다리는 같은 그림을 뒤집어 쓴다 — 왼쪽 다리는 왼쪽을 향한다.
                    leg.flipX = part.X > _director.BossPositionSubUnitsX;
                    leg.transform.localPosition = SimView.ToWorld(part.X, part.Y);
                    leg.enabled = !part.Destroyed;
                }

                // 파괴되는 프레임에 **잘려 나가는** 연출을 한 번 낸다.
                if (part.Destroyed && !_legDestroyed[legIndex])
                {
                    TriggerLegSever(SimView.ToWorld(part.X, part.Y));
                }
                _legDestroyed[legIndex] = part.Destroyed;
                legIndex++;
            }

            for (int i = legIndex; i < _legs.Count; i++)
                SetEnabled(_legs[i], false);
        }

        /// <summary>
        /// 다리 절단: 관절에서 터지고, 잘린 다리가 아래로 떨어지듯 폭발이 이어진다.
        /// 파괴를 "사라짐"이 아니라 "잘림"으로 읽히게 하는 것이 목적이다.
        /// </summary>
        void TriggerLegSever(Vector3 at)
        {
            if (_director == null) return;
            _director.SpawnSeverBurst(at);
            if (_juice != null) _juice.Shake(0.3f);
        }

        void SyncShield(IReadOnlyList<BossPartState> parts, Vector3 bossWorld)
        {
            bool shieldUp = false;
            Vector3 headWorld = bossWorld;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsCore) continue;
                shieldUp = parts[i].CoreGated && !parts[i].Destroyed;
                headWorld = SimView.ToWorld(parts[i].X, parts[i].Y);
                break;
            }

            if (_shieldWasUp && !shieldUp) _shieldBreakAge = 0f;   // 지금 풀렸다
            _shieldWasUp = shieldUp;

            _shield = Ensure(_shield, "HiveShield", _shieldSprite, ShieldOrder);
            if (_shield == null) return;
            _shield.transform.localPosition = headWorld;

            if (shieldUp)
            {
                // 잠긴 동안엔 은은히 맥동한다 — 살아 있는 막으로 읽혀야 한다.
                float pulse = (Mathf.Sin(Time.time * 3.2f) + 1f) * 0.5f;
                _shield.color = new Color(1f, 1f, 1f, 0.65f + pulse * 0.25f);
                _shield.transform.localScale = Vector3.one * (1f + pulse * 0.03f);
                _shield.enabled = true;
                return;
            }

            if (_shieldBreakAge < ShieldBreakSeconds)
            {
                // 깨짐: 부풀며 사라진다. 다리를 다 부순 보상이 눈에 보여야 한다.
                _shieldBreakAge += Time.deltaTime;
                float t = Mathf.Clamp01(_shieldBreakAge / ShieldBreakSeconds);
                _shield.color = new Color(1f, 1f, 1f, 1f - t);
                _shield.transform.localScale = Vector3.one * (1f + t * 0.6f);
                _shield.enabled = true;
                return;
            }
            _shield.enabled = false;
        }

        SpriteRenderer Ensure(SpriteRenderer existing, string name, Sprite sprite, int order)
        {
            if (existing != null) return existing;
            if (sprite == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(_root != null ? _root : transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        void Hide()
        {
            _visible = false;
            _shieldWasUp = false;
            _shieldBreakAge = float.MaxValue;
            SetEnabled(_torso, false);
            SetEnabled(_shield, false);
            for (int i = 0; i < _legs.Count; i++) SetEnabled(_legs[i], false);
            for (int i = 0; i < _legDestroyed.Count; i++) _legDestroyed[i] = false;
        }

        static void SetEnabled(SpriteRenderer renderer, bool on)
        {
            if (renderer != null && renderer.enabled != on) renderer.enabled = on;
        }
    }
}
