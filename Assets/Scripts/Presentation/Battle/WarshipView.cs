using System.Collections.Generic;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// St3 거대 전함 뷰 (REQ-110/111 관측 소비).
    ///
    /// Core는 이 전함을 **멀티파트 보스 + 3단 파츠 그룹**(함미 추진기 / 함체 포탑 라인 /
    /// 함수 코어)으로 노출한다. 파츠 위치·HP·무적은 <see cref="BattleDirector.BossParts"/>가,
    /// 그룹 순서와 역할은 <see cref="BattleDirector.WarshipEncounter"/>(waves.json 계약)가 준다.
    /// 여기서는 **그리기만** 한다 — 어떤 그룹이 열리는지는 Core가 정한다.
    ///
    /// 아트 (2026-08-03 사람 지시로 전용 아트 확정 — 그전까지는 임시 조립이었다):
    ///   1. 함체 — art-input/warship_hull.png 한 장. 없으면 px_white 판 3장(척추 + 상/하
    ///      갑판 레일)으로 어두운 윤곽만 그리는 예전 조립으로 되돌아간다.
    ///   2. 하드포인트 — 함미=warship_stern, 함수=warship_core, 포탑=obstacle_laser_turret.
    ///      함미·함수는 예전에 boss_fortress/boss_core를 빌려 썼는데, 각자 다른 보스의
    ///      조형이라 사람이 "전함·코어·로봇 세 보스가 하나로 보인다"고 지적했다.
    ///      지금은 함체와 같은 팔레트의 전용 파츠다 (파일이 없으면 옛 스프라이트로 폴백).
    ///   3. 장착 기둥 — 갑판 포탑은 판정상 ±3.5유닛까지 벌어져 함체 그림 밖에 선다.
    ///      판정은 Core 소관이라 옮기지 않고, 함체에서 포탑까지 기둥을 그려 잇는다.
    ///
    /// 그룹 피드백은 **Core가 말한 것만** 말한다:
    ///   - 파괴된 파츠      → 그을린 잔해 (함체는 계속 남는다)
    ///   - 무적인 파츠      → 깊은 암전 + 청록 맥동 ("지금은 못 깎는다")
    ///   - 다음 그룹 파츠   → 옅은 암전 ("아직 이 차례가 아니다" — 무적과 구분되는 세기)
    ///   - 그룹이 넘어간 순간 → 짧은 백색 플래시 + 흔들림
    /// 함미 그룹이 전멸하는 프레임은 중간보스 격파와 같은 무게로 친다(흔들림 0.6) —
    /// Core의 MidBossDefeated 연출 상수와 같은 값이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarshipView : MonoBehaviour
    {
        // 정렬: 함체 실루엣은 게임플레이 **아래**(탄 5 아래)다. 예전에는 13이라
        // 기체(10)·주무기탄(5)이 함체 판 뒤로 사라졌다 — 함미를 근접 사격하려면
        // 반드시 함체 위로 들어가야 하는데, 그 순간 자기 기체와 자기 탄이 안 보였다.
        // (전경 실루엣 55와 같은 부류의 가림 버그. SectionThemeDirector.NearSortingOrder 참고.)
        // 하드포인트는 보스 본체(15) 위에 얹는다 — 파츠가 묻히면 조준할 곳이 사라진다.
        //
        // 4는 전경 실루엣 크로스페이드 고스트(3+1)와 같은 순서지만, 고스트는 구간 전환
        // 2~3초 동안만 살아 있고 실루엣 띠(화면 위·아래 끝)와 함체(화면 중앙 띠)는
        // 겹치는 면적이 거의 없다 — 순서가 갈리지 않아도 눈에 잡히지 않는다.
        const int HullOrder = 4;
        const int HardpointOrder = 16;
        /// <summary>장착 기둥은 함체 위·포탑 아래에 깐다.</summary>
        const int PylonOrder = 10;
        /// <summary>
        /// 함체 그림의 반높이(유닛). warship_hull.png의 불투명 영역이 ±3.09유닛이라
        /// 여기서부터가 "배 밖"이다. 기둥은 이 선부터 포탑까지만 그린다 — 중심선에서
        /// 시작하면 기둥이 함체를 가로질러 지나가 배를 두 동강 낸 것처럼 보인다.
        /// </summary>
        const float HullEdgeY = 3.0f;
        /// <summary>기둥 두께(유닛). 포탑 폭(2.5u)보다 확실히 얇아야 받침으로 읽힌다.</summary>
        const float PylonWidth = 0.5f;
        /// <summary>포탑 밑동에 물리는 여유 — 이음매가 뜨면 다시 "떠 있는" 것으로 읽힌다.</summary>
        const float PylonOverlap = 0.4f;

        const float HitFlashSeconds = 0.09f;
        const float ActivateFlashSeconds = 0.32f;

        /// <summary>함체 판 여백(월드 유닛). 파츠가 실루엣 가장자리에 걸리면 배가 짧아 보인다.</summary>
        const float HullPadX = 2f;
        const float SpineHalfHeight = 2.6f;
        const float DeckThickness = 1.6f;

        static readonly Color HullTint = new Color(0.10f, 0.11f, 0.14f, 0.96f);
        static readonly Color DeckTint = new Color(0.16f, 0.17f, 0.21f, 0.96f);
        /// <summary>장착 기둥 색 — 갑판보다 어둡게 깔아 포탑이 앞으로 서게 한다.</summary>
        static readonly Color PylonTint = new Color(0.13f, 0.14f, 0.17f, 1f);
        static readonly Color Scorched = new Color(0.13f, 0.12f, 0.14f, 1f);
        static readonly Color DeepDim = new Color(0.30f, 0.34f, 0.42f, 1f);
        static readonly Color MildDim = new Color(0.62f, 0.64f, 0.68f, 1f);
        static readonly Color GatePulse = new Color(0.35f, 0.85f, 1f, 1f);

        [SerializeField] BattleDirector _director;
        [SerializeField] Transform _root;
        [SerializeField] JuiceDirector _juice;

        [Tooltip("px_white — 함체 실루엣 판. 없으면 실루엣 없이 하드포인트만 그린다.")]
        [SerializeField] Sprite _pixelSprite;
        [Tooltip("아트 슬롯: art-input/warship_hull.png. 있으면 실루엣 판을 대체한다.")]
        [SerializeField] Sprite _hullSprite;
        [Tooltip("함미 추진기 블록 — art-input/warship_stern.png (없으면 boss_fortress 폴백).")]
        [SerializeField] Sprite _sternSprite;
        [Tooltip("함체 포탑 — 기존 obstacle_laser_turret 재사용.")]
        [SerializeField] Sprite _turretSprite;
        [Tooltip("함수 코어 모듈 — art-input/warship_core.png (없으면 boss_core 폴백).")]
        [SerializeField] Sprite _coreSprite;

        SpriteRenderer _hullArt;
        SpriteRenderer _spine;
        SpriteRenderer _deckTop;
        SpriteRenderer _deckBottom;
        readonly List<SpriteRenderer> _hardpoints = new List<SpriteRenderer>(8);

        /// <summary>
        /// 포탑 장착 파일런 (사람 지적 2026-08-03: "포탑이 배 밖에 떠 있다").
        /// 갑판 하드포인트는 로컬 ±2.0·±3.5유닛에 서는데 함체 그림은 ±3.1유닛까지라
        /// 바깥쪽 두 문이 허공에 뜬다. 판정 위치는 Core가 정하므로 옮길 수 없고,
        /// 옮겨서도 안 된다 — 대신 함체에서 포탑까지 짧은 기둥을 그려 "여기 달려 있다"를
        /// 만든다. 순수 장식이라 판정과 무관하다.
        /// </summary>
        readonly List<SpriteRenderer> _pylons = new List<SpriteRenderer>(8);
        readonly List<int> _partGroup = new List<int>(8);
        readonly List<int> _lastHp = new List<int>(8);
        readonly List<float> _hitFlash = new List<float>(8);
        readonly List<float> _activateFlash = new List<float>(8);
        readonly List<bool> _wasInvulnerable = new List<bool>(8);

        WarshipEncounterDefinition _definition;
        bool _visible;
        int _focusGroup;
        int _lastFocusGroup = -1;

        float _unitX = 1f;
        float _unitY = 1f;

        // ── 개발 오버레이 관측값 ──────────────────────────────────────────────

        /// <summary>전함 뷰가 지금 화면을 소유하고 있는가 (BossPartsView가 이 값으로 비켜난다).</summary>
        public bool Active => _visible;

        /// <summary>현재 열려 있는 파츠 그룹 index (0=함미, 1=함체, 2=함수).</summary>
        public int FocusGroupIndex => _focusGroup;

        public int GroupCount => _definition != null ? _definition.Groups.Count : 0;

        public string FocusGroupId =>
            _definition != null && _focusGroup >= 0 && _focusGroup < _definition.Groups.Count
                ? _definition.Groups[_focusGroup].GroupId
                : null;

        /// <summary>남은 함체 포탑 수 (전투 중 파괴하면 함수 개막 밀도가 줄어든다).</summary>
        public int AttritionAlive { get; private set; }

        public int AttritionTotal { get; private set; }

        void Update()
        {
            var definition = _director != null ? _director.WarshipEncounter : null;
            var parts = _director != null ? _director.BossParts : null;
            bool active = definition != null
                && _director.BossActive
                && parts != null && parts.Count > 0;

            if (!active)
            {
                if (_visible) Hide();
                return;
            }

            if (!ReferenceEquals(definition, _definition) || _partGroup.Count != parts.Count)
                Rebuild(definition, parts);

            Vector3 bossWorld = _director.BossWorldPosition;
            SyncHull(parts, bossWorld);
            SyncHardpoints(parts);
            _visible = true;
        }

        // ── 조립 ──────────────────────────────────────────────────────────────

        void Rebuild(WarshipEncounterDefinition definition, IReadOnlyList<BossPartState> parts)
        {
            _definition = definition;
            _lastFocusGroup = -1;
            _focusGroup = 0;

            // 하드포인트 렌더러는 **버리지 않는다** — 방마다 GameObject를 새로 만들면
            // 런 하나에 수십 개가 쌓인다 (게임 루프 Instantiate 금지, CLAUDE.md).
            for (int i = 0; i < _hardpoints.Count; i++)
                SetEnabled(_hardpoints[i], false);
            for (int i = 0; i < _pylons.Count; i++)
                SetEnabled(_pylons[i], false);
            _partGroup.Clear();
            _lastHp.Clear();
            _hitFlash.Clear();
            _activateFlash.Clear();
            _wasInvulnerable.Clear();

            if (_pixelSprite != null)
            {
                Vector3 unit = _pixelSprite.bounds.size;
                // localScale에 월드 길이를 그대로 넣으면 스프라이트 자체 크기만큼 곱해진다
                // (LaserBeamView와 같은 함정 — px_white는 2px/PPU16 = 0.125 유닛이다).
                _unitX = Mathf.Max(0.0001f, unit.x);
                _unitY = Mathf.Max(0.0001f, unit.y);
            }

            AttritionTotal = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                int group = FindGroup(definition, parts[i].PartId);
                _partGroup.Add(group);
                _lastHp.Add(parts[i].Hp);
                _hitFlash.Add(float.MaxValue);
                _activateFlash.Add(float.MaxValue);
                // 등장 중에는 Core가 모든 파츠를 무적으로 잠근다. 첫 관측을 무적으로
                // 잡아 두면 잠금이 풀리는 프레임이 그대로 "열렸다" 플래시가 된다.
                _wasInvulnerable.Add(true);
                BindHardpoint(definition, parts[i], group, i);
                if (group == 1) AttritionTotal++;
            }
            AttritionAlive = AttritionTotal;
        }

        static int FindGroup(WarshipEncounterDefinition definition, string partId)
        {
            var groups = definition.Groups;
            for (int g = 0; g < groups.Count; g++)
                for (int m = 0; m < groups[g].PartIds.Count; m++)
                    if (string.Equals(groups[g].PartIds[m], partId, System.StringComparison.Ordinal))
                        return g;
            return groups.Count - 1;
        }

        /// <summary>
        /// 파츠 히트박스(서브유닛 반크기)를 찾는다. 못 찾으면 null — 그때는 스프라이트를
        /// native 크기로 둔다(기존 동작).
        /// </summary>
        BossPartDefinition FindPartDefinition(string partId)
        {
            var definitions = _director != null ? _director.BossPartDefinitions : null;
            if (definitions == null) return null;
            for (int i = 0; i < definitions.Count; i++)
                if (string.Equals(definitions[i].PartId, partId, System.StringComparison.Ordinal))
                    return definitions[i];
            return null;
        }

        /// <summary>
        /// 하드포인트 스프라이트를 **판정 크기에 맞춘다**.
        ///
        /// 재사용 스프라이트는 원래 이 파츠 크기로 그려진 게 아니다 — 함미로 쓰는
        /// boss_fortress는 128×96px(PPU16 = 8×6u)인데 engine 판정은 5×4u다. native로
        /// 얹으면 보이는 함미가 판정보다 세로로 1.5배 커서, 실루엣 아래쪽 가장자리를
        /// 조준하면 명중 피드백이 아예 없다. build25~30 테스터가 5빌드 연속 "함미
        /// 무데미지"로 오판한 것이 이 거짓말 위에서 벌어졌다(build30 보고서 §2).
        ///
        /// 그림 ≤ 판정이 원칙이다. 스프라이트 종횡비는 유지하지 않는다 — 판정이 축마다
        /// 독립인 AABB(<c>FindBossPartHit</c>)라 종횡비를 지키면 한 축이 다시 어긋난다.
        /// </summary>
        void FitHardpointToHitbox(SpriteRenderer renderer, BossPartDefinition partDefinition)
        {
            if (renderer == null) return;
            if (partDefinition == null || renderer.sprite == null)
            {
                renderer.transform.localScale = Vector3.one;
                return;
            }
            Vector3 native = renderer.sprite.bounds.size;
            if (native.x <= 0.0001f || native.y <= 0.0001f)
            {
                renderer.transform.localScale = Vector3.one;
                return;
            }
            float width = 2f * partDefinition.HalfWidth * SimView.WorldUnitsPerSubUnit;
            float height = 2f * partDefinition.HalfHeight * SimView.WorldUnitsPerSubUnit;
            renderer.transform.localScale =
                new Vector3(width / native.x, height / native.y, 1f);
        }

        void BindHardpoint(
            WarshipEncounterDefinition definition,
            BossPartState part,
            int group,
            int index)
        {
            Sprite sprite = _turretSprite;
            // 함미는 좌우를 뒤집어 건다. 같은 boss_fortress 실루엣이 함체 중앙(보스
            // 본체 렌더러)과 함미에 두 번 서면 "요새가 두 개"로 읽힌다 — 뒤집으면
            // 진행 방향 반대편을 보는 후미부로 읽힌다.
            bool flip = false;
            if (group >= 0 && group < definition.Groups.Count)
            {
                switch (definition.Groups[group].Role)
                {
                    case WarshipGroupRole.MidbossGate:
                        sprite = _sternSprite;
                        flip = true;
                        break;
                    case WarshipGroupRole.FinalCore: sprite = _coreSprite; break;
                    default: sprite = _turretSprite; break;
                }
            }
            if (sprite == null) { sprite = _turretSprite; flip = false; }

            while (_hardpoints.Count <= index)
            {
                var go = new GameObject($"WarshipHardpoint_{_hardpoints.Count:D2}");
                go.transform.SetParent(_root != null ? _root : transform, false);
                var created = go.AddComponent<SpriteRenderer>();
                created.sortingOrder = HardpointOrder;
                created.enabled = false;
                _hardpoints.Add(created);
            }

            var renderer = _hardpoints[index];
            if (renderer == null) return;
            renderer.name = $"WarshipHardpoint_{index:D2}_{part.PartId}";
            renderer.sprite = sprite;
            renderer.flipX = flip;
            renderer.color = Color.white;
            renderer.enabled = sprite != null;
            FitHardpointToHitbox(renderer, FindPartDefinition(part.PartId));
        }

        /// <summary>
        /// 갑판 포탑을 함체에 잇는 장착 기둥. 보스 중심선(y=0)에서 포탑까지 세로로
        /// 깔고, 포탑 스프라이트 뒤(HullOrder와 HardpointOrder 사이)에 둔다.
        /// 파괴된 파츠에는 그리지 않는다 — 떨어져 나간 자리에 기둥만 남으면 이상하다.
        /// </summary>
        void SyncPylon(int index, Vector3 partLocal, bool destroyed)
        {
            float offsetY = partLocal.y - (_root != null ? _root.localPosition.y : 0f);
            // 함체 안에 있는 파츠는 기둥이 필요 없다 — 함미·코어와 안쪽 갑판 포탑이 여기다.
            float outward = Mathf.Abs(offsetY) - HullEdgeY;
            bool needed = !destroyed && outward > 0f;

            while (_pylons.Count <= index) _pylons.Add(null);
            if (!needed)
            {
                SetEnabled(_pylons[index], false);
                return;
            }
            if (_pylons[index] == null)
            {
                _pylons[index] = EnsurePlate(
                    null, $"WarshipPylon_{index:D2}", _pixelSprite, PylonOrder);
                if (_pylons[index] == null) return;
            }

            // 함체 가장자리에서 포탑까지. 양 끝을 조금씩 물려 이음매가 뜨지 않게 한다.
            float sign = Mathf.Sign(offsetY);
            float span = outward + PylonOverlap * 2f;
            float edgeY = (_root != null ? _root.localPosition.y : 0f) + sign * (HullEdgeY - PylonOverlap);
            var pylon = _pylons[index];
            pylon.transform.localPosition = new Vector3(
                partLocal.x, edgeY + sign * span * 0.5f, partLocal.z);
            pylon.transform.localScale = new Vector3(
                PylonWidth / _unitX, span / _unitY, 1f);
            if (pylon.color != PylonTint) pylon.color = PylonTint;
            if (!pylon.enabled) pylon.enabled = true;
        }

        SpriteRenderer EnsurePlate(SpriteRenderer existing, string name, Sprite sprite, int order)
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

        // ── 함체 ──────────────────────────────────────────────────────────────

        void SyncHull(IReadOnlyList<BossPartState> parts, Vector3 bossWorld)
        {
            // 함체 크기는 파츠 배치에서 뽑는다 — waves.json이 파츠를 옮기면 실루엣도 따라간다.
            float minX = float.MaxValue, maxX = float.MinValue;
            float maxY = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 local = SimView.ToWorld(parts[i].X, parts[i].Y) - bossWorld;
                if (local.x < minX) minX = local.x;
                if (local.x > maxX) maxX = local.x;
                float absY = Mathf.Abs(local.y);
                if (absY > maxY) maxY = absY;
            }
            if (minX > maxX) return;

            float left = minX - HullPadX;
            float right = maxX + HullPadX;
            float centerX = (left + right) * 0.5f;
            float width = right - left;

            if (_hullSprite != null)
            {
                // 아트 슬롯이 채워졌다 — 한 장이 실루엣 전체를 대신한다. 픽셀 아트라
                // 늘리지 않고 원본 크기 그대로 보스 중심에 건다.
                _hullArt = EnsurePlate(_hullArt, "WarshipHull", _hullSprite, HullOrder);
                if (_hullArt != null)
                {
                    _hullArt.transform.localPosition = bossWorld;
                    _hullArt.color = Color.white;
                    _hullArt.enabled = true;
                }
                SetEnabled(_spine, false);
                SetEnabled(_deckTop, false);
                SetEnabled(_deckBottom, false);
                return;
            }

            if (_pixelSprite == null) return;
            _spine = EnsurePlate(_spine, "WarshipSpine", _pixelSprite, HullOrder);
            _deckTop = EnsurePlate(_deckTop, "WarshipDeckTop", _pixelSprite, HullOrder);
            _deckBottom = EnsurePlate(_deckBottom, "WarshipDeckBottom", _pixelSprite, HullOrder);

            PlacePlate(_spine, bossWorld + new Vector3(centerX, 0f, 0f),
                width, SpineHalfHeight * 2f, HullTint);
            // 갑판 레일은 척추보다 짧게 — 함수/함미가 뾰족해 보이는 최소한의 실루엣 변화다.
            float deckWidth = Mathf.Max(1f, width - HullPadX * 2f);
            float deckY = Mathf.Max(SpineHalfHeight + DeckThickness * 0.5f, maxY);
            PlacePlate(_deckTop, bossWorld + new Vector3(centerX, deckY, 0f),
                deckWidth, DeckThickness, DeckTint);
            PlacePlate(_deckBottom, bossWorld + new Vector3(centerX, -deckY, 0f),
                deckWidth, DeckThickness, DeckTint);
        }

        void PlacePlate(SpriteRenderer renderer, Vector3 position, float width, float height, Color tint)
        {
            if (renderer == null) return;
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(width / _unitX, height / _unitY, 1f);
            if (renderer.color != tint) renderer.color = tint;
            if (!renderer.enabled) renderer.enabled = true;
        }

        // ── 파츠 그룹 ─────────────────────────────────────────────────────────

        void SyncHardpoints(IReadOnlyList<BossPartState> parts)
        {
            int groupCount = _definition.Groups.Count;

            // 열려 있는 그룹 = 아직 살아 있는 파츠를 가진 **가장 앞선** 그룹.
            // Core의 그룹 게이트(함미 전멸 → 함체 → 함수)를 그대로 따라간다.
            int focus = groupCount - 1;
            for (int g = 0; g < groupCount; g++)
            {
                bool alive = false;
                for (int i = 0; i < parts.Count; i++)
                    if (_partGroup[i] == g && !parts[i].Destroyed) { alive = true; break; }
                if (alive) { focus = g; break; }
            }
            _focusGroup = focus;

            if (_lastFocusGroup >= 0 && focus > _lastFocusGroup)
            {
                // 그룹이 넘어갔다. 새로 열린 그룹만 번쩍인다 — 화면 전체를 씻으면
                // "무엇이 열렸는지"가 사라진다.
                for (int i = 0; i < parts.Count; i++)
                    if (_partGroup[i] == focus) _activateFlash[i] = 0f;
                if (_juice != null)
                {
                    // 함미(중간보스 게이트) 전멸은 중간보스 격파와 같은 무게다.
                    bool sternFell = _lastFocusGroup == 0;
                    _juice.Shake(sternFell ? 0.6f : 0.35f);
                    if (sternFell) _juice.Hitstop(0.08f);
                }
            }
            _lastFocusGroup = focus;

            int attritionAlive = 0;
            float dt = Time.deltaTime;

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var renderer = _hardpoints[i];
                if (_partGroup[i] == 1 && !part.Destroyed) attritionAlive++;
                if (renderer == null) continue;

                renderer.transform.localPosition = SimView.ToWorld(part.X, part.Y);
                SyncPylon(i, renderer.transform.localPosition, part.Destroyed);

                if (_lastHp[i] > part.Hp && !part.Destroyed) _hitFlash[i] = 0f;
                _lastHp[i] = part.Hp;

                // 무적 해제 = 이 파츠가 지금 열렸다. 그룹 전환 플래시와 같은 연출로
                // 묶는다 — 등장 종료(전 파츠)와 코어 게이트 개방(함수)이 둘 다 여기다.
                if (_wasInvulnerable[i] && !part.Invulnerable && !part.Destroyed)
                    _activateFlash[i] = 0f;
                _wasInvulnerable[i] = part.Invulnerable;

                Color tint;
                if (part.Destroyed)
                    tint = Scorched;
                else if (part.Invulnerable)
                {
                    // 무적은 "아직 열리지 않았다"의 가장 강한 상태 — 암전에 청록 맥동을
                    // 얹어 다음 그룹의 옅은 암전과 확실히 갈라 놓는다.
                    float pulse = (Mathf.Sin(Time.time * 4.5f) + 1f) * 0.5f;
                    tint = Color.Lerp(DeepDim, GatePulse, 0.18f + pulse * 0.14f);
                }
                else if (_partGroup[i] > focus)
                    tint = MildDim;
                else
                    tint = Color.white;

                float activate = _activateFlash[i];
                if (activate < ActivateFlashSeconds)
                {
                    _activateFlash[i] = activate + dt;
                    float t = 1f - Mathf.Clamp01(activate / ActivateFlashSeconds);
                    if (_juice != null && _juice.FlashReduced) t *= 0.4f;
                    tint = Color.Lerp(tint, Color.white, t);
                }

                float hit = _hitFlash[i];
                if (hit < HitFlashSeconds)
                {
                    _hitFlash[i] = hit + dt;
                    tint = Color.Lerp(Color.white, tint, Mathf.Clamp01(hit / HitFlashSeconds));
                }

                if (renderer.color != tint) renderer.color = tint;
                if (!renderer.enabled && renderer.sprite != null) renderer.enabled = true;
            }

            AttritionAlive = attritionAlive;
        }

        // ── 정리 ──────────────────────────────────────────────────────────────

        void Hide()
        {
            _visible = false;
            _definition = null;
            _lastFocusGroup = -1;
            SetEnabled(_hullArt, false);
            SetEnabled(_spine, false);
            SetEnabled(_deckTop, false);
            SetEnabled(_deckBottom, false);
            for (int i = 0; i < _hardpoints.Count; i++)
                SetEnabled(_hardpoints[i], false);
            for (int i = 0; i < _pylons.Count; i++)
                SetEnabled(_pylons[i], false);
            _partGroup.Clear();
            _lastHp.Clear();
            _hitFlash.Clear();
            _activateFlash.Clear();
            _wasInvulnerable.Clear();
            AttritionAlive = 0;
            AttritionTotal = 0;
        }

        static void SetEnabled(SpriteRenderer renderer, bool on)
        {
            if (renderer != null && renderer.enabled != on) renderer.enabled = on;
        }
    }
}
