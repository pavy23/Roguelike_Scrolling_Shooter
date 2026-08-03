using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 하이브 보스 뷰 (사람 지시 2026-08-03).
    ///
    /// 요구는 넷이었다: ① 위아래로 길쭉한 체형, ② 머리에 처음엔 총알이 안 먹는 실드,
    /// ③ 다리를 부수면 실드가 사라지고 다리가 **잘리는** 연출, ④ 잘린 자리가 어색하게
    /// 뚝 끊기지 않게 손상 부위를 그릴 것.
    ///
    /// 첫 시도는 몸통 그림과 다리 그림을 **따로 생성해서 파츠 좌표에 얹는** 방식이었다.
    /// 두 그림은 광원도 골격도 달랐고 골반 높이도 안 맞아서, 사람이 스크린샷을 보고
    /// "다리가 뚝뚝 끊어진다"고 했다. 좌표를 아무리 맞춰도 **다른 그림 두 장**이라
    /// 관절이 맞을 수가 없었다.
    ///
    /// 그래서 방향을 뒤집었다. 완성된 전신 렌더 한 장을 잘라 파츠를 만들고
    /// (Tools/ArtGen/cut_hive_parts.py), 그 조각들을 **전부 같은 캔버스·같은 좌표**에
    /// 겹쳐 그린다. 다리가 붙어 있는 동안은 원본과 픽셀 단위로 같다 — 어긋날 여지가
    /// 구조적으로 없다. 파츠마다 위치를 계산하던 코드가 통째로 사라진 이유다.
    ///
    /// 판정은 전부 Core가 정한다. 어떤 다리가 부서졌는지도, 실드가 언제 풀리는지도
    /// Core가 준 파츠 상태(CoreGated)가 말한다. 여기서는 그리기만 한다.
    /// (CLAUDE.md: Presentation은 게임플레이 판정을 하지 않는다.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HiveBossView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] JuiceDirector _juice;

        [Tooltip("몸통 + 허벅지 + 꼬리 (다리 제외) — art-input/boss_hive_torso.png")]
        [SerializeField] Sprite _torsoSprite;
        [Tooltip("왼 다리 (무릎 아래) — art-input/boss_hive_leg_l.png")]
        [SerializeField] Sprite _legLeftSprite;
        [Tooltip("오른 다리 (무릎 아래) — art-input/boss_hive_leg_r.png")]
        [SerializeField] Sprite _legRightSprite;
        [Tooltip("왼 다리 절단면 — art-input/boss_hive_wound_l.png")]
        [SerializeField] Sprite _woundLeftSprite;
        [Tooltip("오른 다리 절단면 — art-input/boss_hive_wound_r.png")]
        [SerializeField] Sprite _woundRightSprite;
        [Tooltip("머리 실드 — art-input/boss_hive_shield.png")]
        [SerializeField] Sprite _shieldSprite;
        [Tooltip("단색 1px 스프라이트 (양손 에너지 발광에 쓴다)")]
        [SerializeField] Sprite _glowSprite;

        /// <summary>이 보스에서만 조립을 켠다. 다른 보스는 기존 본체 렌더러 그대로다.</summary>
        const string HiveBossIdPrefix = "boss_hive";

        // 정렬: 조각들이 픽셀 단위로 겹치지 않으므로 순서는 사실상 자유지만,
        // 잘린 다리가 떨어질 때 몸통 뒤로 지나가야 자연스럽다.
        const int LegOrder = 14;
        const int TorsoOrder = 15;
        const int WoundOrder = 16;
        const int ShieldOrder = 17;

        /// <summary>실드가 깨지는 데 걸리는 시간(초). 짧게 — 해방의 순간이지 연출 대기가 아니다.</summary>
        const float ShieldBreakSeconds = 0.45f;

        /// <summary>잘린 다리가 떨어져 사라지기까지의 시간(초).</summary>
        const float LegFallSeconds = 0.9f;

        /// <summary>
        /// 양다리를 다 부순 뒤의 발광 위치 (아트 캔버스 128x256 기준 손 좌표를
        /// 캔버스 중심에서의 유닛 오프셋으로 환산한 값).
        ///
        /// 사람 지시: "두 다리 다 파괴하고 나면 양팔이 움직이면서 미사일과 에너지탄을
        /// 대량 발사하는 패턴." 탄막 자체는 Core/데이터가 쏘고(REQ-141), 여기서는
        /// **그게 팔에서 나온다는 것**을 보이게 한다.
        ///
        /// 팔 그림을 잘라 실제로 휘두르는 것도 검토했지만, 오른팔이 몸통 앞으로
        /// 겹쳐 지나가 깨끗한 분리선이 없다 — 잘못 자르면 다리에서 겪은 "관절이
        /// 어색하다"가 재발한다. 그래서 자르지 않고 손끝에서 에너지가 터지게 한다.
        /// </summary>
        static readonly Vector2 LeftHandOffset = new Vector2(-2.75f, -3.25f);
        static readonly Vector2 RightHandOffset = new Vector2(1.75f, -2.5f);

        /// <summary>발광 한 번의 주기(초). 대량 발사라 짧게 몰아친다.</summary>
        const float BarragePulseSeconds = 0.24f;

        SpriteRenderer _torso;
        SpriteRenderer _shield;
        readonly SpriteRenderer[] _handGlows = new SpriteRenderer[2];
        readonly SpriteRenderer[] _legs = new SpriteRenderer[2];
        readonly SpriteRenderer[] _wounds = new SpriteRenderer[2];
        readonly bool[] _legDestroyed = new bool[2];
        readonly float[] _legFallAge = { float.MaxValue, float.MaxValue };

        // 피격 플래시. 하이브는 전용 뷰라 범용 파츠 오버레이(BossPartsView)가 비켜나
        // 있고, 그 오버레이가 하던 피격 반응까지 함께 사라져 있었다 — 사람 보고
        // 2026-08-03: "다리 부술 때도 피격하는 효과가 있으면 좋겠어."
        // 맞고 있다는 신호가 없으면 플레이어는 약점을 때리고 있는지 알 수 없다.
        readonly int[] _lastLegHp = { -1, -1 };
        readonly float[] _legHitFlash = { float.MaxValue, float.MaxValue };
        int _lastCoreHp = -1;
        float _coreHitFlash = float.MaxValue;

        /// <summary>피격 플래시 지속(초). 짧게 — 길면 몸 색이 통째로 물든다.</summary>
        const float HitFlashSeconds = 0.12f;

        bool _visible;
        bool _shieldWasUp;
        float _shieldBreakAge = float.MaxValue;

        /// <summary>전함 뷰처럼, 이 뷰가 화면을 소유하면 범용 파츠 오버레이는 비켜난다.</summary>
        public bool Active => _visible;

        void Update()
        {
            var parts = _director != null ? _director.BossParts : null;
            string bossId = _director != null ? _director.BossStageId : null;
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

            Vector3 anchor = _director.BossWorldPosition;
            float scale = ArtScale();

            _torso = Ensure(_torso, "HiveTorso", _torsoSprite, TorsoOrder);
            Place(_torso, anchor, scale);
            SyncCoreFlash(parts);

            SyncLegs(parts, anchor, scale);
            SyncShield(parts, anchor, scale);
            SyncBarrage(anchor, scale);
            _visible = true;
        }

        /// <summary>
        /// 그림을 **판정 크기에 맞춘다**.
        ///
        /// 이 프로젝트에서 같은 사고가 세 번 났다 — 보이는 크기와 맞는 크기가 달라서
        /// "때렸는데 안 맞는다"·"무데미지다"라는 보고가 올라왔다(커밋 c3df07c 참조).
        /// 그래서 배율을 아트에 박아 두지 않고 Core가 정한 반높이에서 **계산한다**.
        /// 데이터가 바뀌면 그림이 따라간다.
        /// </summary>
        float ArtScale()
        {
            float halfHeight = _director.BossHalfHeightWorld;
            if (halfHeight <= 0f || _torsoSprite == null) return 1f;
            float artHeight = _torsoSprite.rect.height / _torsoSprite.pixelsPerUnit;
            if (artHeight <= 0f) return 1f;
            return halfHeight * 2f / artHeight;
        }

        void SyncLegs(IReadOnlyList<BossPartState> parts, Vector3 anchor, float scale)
        {
            // Core의 파츠 목록에서 좌/우 다리를 고른다. 그림의 좌우와 판정의 좌우가
            // 엇갈리면 멀쩡한 다리가 사라지므로, x 좌표로 직접 맞춘다.
            var seen = new bool[2];
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.IsCore) continue;
                int side = part.X < _director.BossPositionSubUnitsX ? 0 : 1;
                if (seen[side]) continue;
                seen[side] = true;

                var legSprite = side == 0 ? _legLeftSprite : _legRightSprite;
                var woundSprite = side == 0 ? _woundLeftSprite : _woundRightSprite;
                _legs[side] = Ensure(_legs[side], side == 0 ? "HiveLegL" : "HiveLegR", legSprite, LegOrder);
                _wounds[side] = Ensure(_wounds[side], side == 0 ? "HiveWoundL" : "HiveWoundR", woundSprite, WoundOrder);

                if (_lastLegHp[side] > part.Hp && !part.Destroyed)
                    _legHitFlash[side] = 0f;
                _lastLegHp[side] = part.Hp;

                if (part.Destroyed && !_legDestroyed[side])
                {
                    _legFallAge[side] = 0f;
                    TriggerLegSever(anchor);
                }
                _legDestroyed[side] = part.Destroyed;

                DrawLeg(side, anchor, scale);
                Place(_wounds[side], anchor, scale);
                SetEnabled(_wounds[side], part.Destroyed);
            }

            for (int side = 0; side < 2; side++)
                if (!seen[side])
                {
                    SetEnabled(_legs[side], false);
                    SetEnabled(_wounds[side], false);
                }
        }

        /// <summary>코어(=몸통)가 맞으면 몸통 전체가 번쩍인다.</summary>
        void SyncCoreFlash(IReadOnlyList<BossPartState> parts)
        {
            if (_torso == null) return;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsCore) continue;
                int hp = parts[i].Hp;
                if (_lastCoreHp > hp) _coreHitFlash = 0f;
                _lastCoreHp = hp;
                break;
            }
            _torso.color = Flash(ref _coreHitFlash);
        }

        /// <summary>
        /// 피격 순간의 색. 흰색이 아니라 **빨강**이다.
        ///
        /// 처음엔 흰 섬광으로 냈는데, 하이브는 몸이 밝은 올리브라 흰색이 잘 안 읽혔다
        /// (사람 지시 2026-08-03: "다리부분 피격당할때 빨간색 등으로 피격당하는지
        /// 알게 해줘"). 맞고 있다는 신호는 배경·몸 색과 확실히 갈라져야 한다.
        /// </summary>
        static readonly Color HitTint = new Color(2.6f, 0.45f, 0.35f, 1f);

        /// <summary>피격색에서 원래 색으로 돌아오는 감쇠. 남은 시간을 직접 굴린다.</summary>
        Color Flash(ref float age)
        {
            if (age >= HitFlashSeconds) return Color.white;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / HitFlashSeconds);
            // 빨강 → 원색. 알파는 건드리지 않는다(떨어지는 다리의 페이드와 겹친다).
            return Color.Lerp(HitTint, Color.white, t);
        }

        /// <summary>
        /// 멀쩡한 다리는 제자리에, 방금 잘린 다리는 **떨어뜨린다**.
        /// 그냥 사라지면 "없어졌다"로 읽힌다 — 사람이 원한 건 "잘렸다"였다.
        /// </summary>
        void DrawLeg(int side, Vector3 anchor, float scale)
        {
            var leg = _legs[side];
            if (leg == null) return;

            if (!_legDestroyed[side])
            {
                Place(leg, anchor, scale);
                leg.color = Flash(ref _legHitFlash[side]);
                leg.transform.localRotation = Quaternion.identity;
                leg.enabled = true;
                return;
            }

            float age = _legFallAge[side];
            if (age >= LegFallSeconds)
            {
                leg.enabled = false;
                return;
            }
            _legFallAge[side] = age + Time.deltaTime;
            float t = Mathf.Clamp01(age / LegFallSeconds);
            float drift = (side == 0 ? -1f : 1f) * t * 0.6f;
            Place(leg, anchor + new Vector3(drift, -t * t * 7f, 0f), scale);
            leg.transform.localRotation =
                Quaternion.Euler(0f, 0f, (side == 0 ? 1f : -1f) * t * 55f);
            leg.color = new Color(1f, 1f, 1f, 1f - t * t);
            leg.enabled = true;
        }

        /// <summary>절단 연출: 관절에서 터지고 화면이 한 번 흔들린다.</summary>
        void TriggerLegSever(Vector3 at)
        {
            if (_director == null) return;
            _director.SpawnSeverBurst(at);
            if (_juice != null) _juice.Shake(0.3f);
        }

        void SyncShield(IReadOnlyList<BossPartState> parts, Vector3 anchor, float scale)
        {
            bool shieldUp = false;
            for (int i = 0; i < parts.Count; i++)
            {
                if (!parts[i].IsCore) continue;
                shieldUp = parts[i].CoreGated && !parts[i].Destroyed;
                break;
            }

            if (_shieldWasUp && !shieldUp) _shieldBreakAge = 0f;   // 지금 풀렸다
            _shieldWasUp = shieldUp;

            _shield = Ensure(_shield, "HiveShield", _shieldSprite, ShieldOrder);
            if (_shield == null) return;
            Place(_shield, anchor, scale);

            if (shieldUp)
            {
                // 잠긴 동안엔 은은히 맥동한다 — 살아 있는 막으로 읽혀야 한다.
                float pulse = (Mathf.Sin(Time.time * 3.2f) + 1f) * 0.5f;
                _shield.color = new Color(1f, 1f, 1f, 0.7f + pulse * 0.3f);
                _shield.enabled = true;
                return;
            }

            if (_shieldBreakAge < ShieldBreakSeconds)
            {
                // 깨짐: 밝게 터지며 사라진다. 다리를 다 부순 보상이 눈에 보여야 한다.
                // 실드는 몸통과 같은 캔버스라 확대하면 머리에서 떨어져 나가므로
                // 크기는 건드리지 않고 밝기와 투명도로만 표현한다.
                _shieldBreakAge += Time.deltaTime;
                float t = Mathf.Clamp01(_shieldBreakAge / ShieldBreakSeconds);
                float flash = 1f + (1f - t) * 1.6f;
                _shield.color = new Color(flash, flash, flash, 1f - t);
                _shield.enabled = true;
                return;
            }
            _shield.enabled = false;
        }

        /// <summary>
        /// 양다리를 다 부순 뒤의 난사 연출 — 양손이 타오르고 몸통이 반동으로 흔들린다.
        /// </summary>
        void SyncBarrage(Vector3 anchor, float scale)
        {
            bool barrage = _legDestroyed[0] && _legDestroyed[1];
            for (int i = 0; i < 2; i++)
            {
                _handGlows[i] = Ensure(
                    _handGlows[i], i == 0 ? "HiveHandGlowL" : "HiveHandGlowR",
                    _glowSprite, ShieldOrder);
                if (_handGlows[i] == null) continue;
                if (!barrage)
                {
                    SetEnabled(_handGlows[i], false);
                    continue;
                }

                // 좌우가 엇갈려 터진다 — 동시에 터지면 팔이 아니라 조명으로 읽힌다.
                float phase = Time.time / BarragePulseSeconds + (i == 0 ? 0f : 0.5f);
                float pulse = 1f - Mathf.Repeat(phase, 1f);
                Vector2 offset = i == 0 ? LeftHandOffset : RightHandOffset;
                var glow = _handGlows[i];
                glow.transform.localPosition =
                    anchor + new Vector3(offset.x * scale, offset.y * scale, 0f);
                float size = (0.6f + pulse * 1.5f) * scale;
                glow.transform.localScale = new Vector3(size, size, 1f);
                // 미사일의 주황과 에너지탄의 청록을 오가며 두 탄종을 예고한다.
                glow.color = Color.Lerp(
                    new Color(1f, 0.62f, 0.18f, pulse * 0.85f),
                    new Color(0.5f, 0.95f, 1f, pulse * 0.85f),
                    Mathf.PingPong(Time.time * 0.9f + i * 0.5f, 1f));
                glow.enabled = true;
            }

            if (_torso == null) return;
            if (!barrage)
            {
                _torso.transform.localRotation = Quaternion.identity;
                return;
            }
            // 난사의 반동. 크게 흔들면 판정과 그림이 어긋나 보이므로 아주 얕게.
            float sway = Mathf.Sin(Time.time * 11f);
            _torso.transform.localPosition =
                anchor + new Vector3(sway * 0.12f * scale, 0f, 0f);
            _torso.transform.localRotation = Quaternion.Euler(0f, 0f, sway * 2.2f);
        }

        void Place(SpriteRenderer renderer, Vector3 anchor, float scale)
        {
            if (renderer == null) return;
            renderer.transform.localPosition = anchor;
            renderer.transform.localScale = Vector3.one * scale;
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
            _lastCoreHp = -1;
            _coreHitFlash = float.MaxValue;
            SetEnabled(_torso, false);
            SetEnabled(_shield, false);
            for (int i = 0; i < 2; i++) SetEnabled(_handGlows[i], false);
            if (_torso != null) _torso.transform.localRotation = Quaternion.identity;
            for (int i = 0; i < 2; i++)
            {
                SetEnabled(_legs[i], false);
                SetEnabled(_wounds[i], false);
                _legDestroyed[i] = false;
                _legFallAge[i] = float.MaxValue;
                _lastLegHp[i] = -1;
                _legHitFlash[i] = float.MaxValue;
            }
        }

        static void SetEnabled(SpriteRenderer renderer, bool on)
        {
            if (renderer != null && renderer.enabled != on) renderer.enabled = on;
        }
    }
}
