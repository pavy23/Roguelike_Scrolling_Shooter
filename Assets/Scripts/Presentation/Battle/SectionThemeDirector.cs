using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 구간(섹션) 배경 전환. 5스테이지 × 4구간 = 20룩을 **아트 0장**으로 만든다
    /// (설계안 Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md §0-1, §2).
    ///
    /// 3종 세트만 쓴다 — 파티클(날씨) · 틴트(팔레트) · 스크롤 체감(패럴랙스 배율).
    /// 여기에 레이어 on/off(하늘 제거 = 실내 진입)를 더한 것이 전부다.
    ///
    /// **순수 표현이다.** Core가 소유하는 scrollSpeed는 손대지 않고 패럴랙스 팩터에만
    /// 배율을 건다. 구간 판정도 Core가 이미 노출하는 <see cref="RunStageSection"/>을
    /// 폴링할 뿐이라 시뮬레이션에 되먹임이 없다.
    ///
    /// 시간 기준은 **게임 틱**이다 (FixedUpdate 누적). timeScale 0(일시정지)에서는
    /// FixedUpdate가 아예 돌지 않으므로 전환도 눈도 멈춘다 — 메뉴를 보는 동안 해가 지면
    /// 안 된다. 슬로모(JuiceDirector) 중에는 함께 느려지는 쪽이 맞다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SectionThemeDirector : MonoBehaviour
    {
        const int RoleCount = 5;   // BgLayerRole 개수

        [SerializeField] BattleDirector _director;
        [SerializeField] JuiceDirector _juice;
        [SerializeField] Sprite _pixelSprite;

        /// <summary>BattleDirector와 같은 테마 배경 루트 배열. 활성 루트가 곧 현재 테마다.</summary>
        [SerializeField] GameObject[] _themeRoots;

        /// <summary>아트 2단계용 교체 슬롯 사전 (art-input에 파일이 들어오면 씬 빌더가 채운다).</summary>
        [SerializeField] string[] _slotKeys;
        [SerializeField] Sprite[] _slotSprites;

        // 정렬 순서는 **탄 가시성이 결정한다**. 씬에서 가장 낮은 게임플레이 요소가
        // 탄(order 5)이므로 워시·파티클은 그보다 아래에 둔다. 배경 타일은 -100~-85이고
        // 전경 실루엣(themeNear)만 55라 워시 위에 남는다 — 그래서 전경은 룩 테이블의
        // Near 틴트로 따로 어둡게 맞춰 통일감을 낸다.
        [Tooltip("전역 워시 — 배경 위(>-85), 탄(5) 아래. 기체/탄은 절대 덮지 않는다.")]
        [SerializeField] int _washSortingOrder = 0;
        [Tooltip("날씨 파티클 — 워시 위, 탄(5) 아래.")]
        [SerializeField] int _particleSortingOrder = 2;
        [Tooltip("섬광 — 전경 실루엣(55) 위, 피격 플래시(90) 아래. 순간 연출이라 전부 덮는다.")]
        [SerializeField] int _flashSortingOrder = 60;
        // -83인 이유: 크로스페이드 고스트가 원본 order+1에 서므로 중경(-85)의 고스트가
        // -84를 쓴다. 랜드마크는 그 한 칸 위에 둬야 중경 교체 중에도 앞에 남는다.
        [Tooltip("랜드마크 슬롯 — 중경(-85)과 그 고스트(-84) 앞, 워시 뒤.")]
        [SerializeField] int _landmarkSortingOrder = -83;

        // 화면(=플레이필드) 크기. 640×360 / PPU 16 = 40 × 22.5u.
        const float HalfWidth = 20f;
        const float HalfHeight = 11.25f;

        sealed class LookState
        {
            public Color wash;
            public float pulseAmp;
            public float pulseHz;
            public readonly Color[] tint = new Color[RoleCount];
            public readonly float[] scrollMul = new float[RoleCount];

            public void SetNeutral()
            {
                wash = new Color(0f, 0f, 0f, 0f);
                pulseAmp = 0f;
                pulseHz = 0.5f;
                for (int i = 0; i < RoleCount; i++)
                {
                    tint[i] = Color.white;
                    scrollMul[i] = 1f;
                }
            }

            public void CopyFrom(LookState other)
            {
                wash = other.wash;
                pulseAmp = other.pulseAmp;
                pulseHz = other.pulseHz;
                for (int i = 0; i < RoleCount; i++)
                {
                    tint[i] = other.tint[i];
                    scrollMul[i] = other.scrollMul[i];
                }
            }

            public void FromTheme(SectionTheme theme)
            {
                SetNeutral();
                if (theme == null) return;
                wash = theme.wash;
                pulseAmp = theme.washPulseAmplitude;
                pulseHz = theme.washPulseHz;
                if (theme.layers == null) return;
                for (int i = 0; i < theme.layers.Length; i++)
                {
                    var look = theme.layers[i];
                    if (look == null) continue;
                    int role = (int)look.role;
                    if (role < 0 || role >= RoleCount) continue;
                    tint[role] = look.tint;
                    scrollMul[role] = look.scrollMul;
                }
            }

            public static void Lerp(LookState a, LookState b, float t, LookState dst)
            {
                dst.wash = Color.Lerp(a.wash, b.wash, t);
                dst.pulseAmp = Mathf.Lerp(a.pulseAmp, b.pulseAmp, t);
                dst.pulseHz = Mathf.Lerp(a.pulseHz, b.pulseHz, t);
                for (int i = 0; i < RoleCount; i++)
                {
                    dst.tint[i] = Color.Lerp(a.tint[i], b.tint[i], t);
                    dst.scrollMul[i] = Mathf.Lerp(a.scrollMul[i], b.scrollMul[i], t);
                }
            }
        }

        readonly LookState _from = new LookState();
        readonly LookState _target = new LookState();
        readonly LookState _current = new LookState();

        SectionTheme _targetTheme;
        SectionParticleField _particles;
        SpriteRenderer _wash;
        SpriteRenderer _flash;

        // 활성 테마 루트 바인딩 캐시
        GameObject _boundRoot;
        ParallaxBackground _boundParallax;
        SpriteRenderer[][] _layerRenderers;      // [layerIndex][tile]
        Sprite[] _layerOriginalSprites;
        SpriteRenderer _landmark;

        readonly Dictionary<string, Sprite> _slots = new Dictionary<string, Sprite>();

        // ── 스프라이트 크로스페이드 ───────────────────────────────────────────────
        // 구간 진입 프레임에 스프라이트를 즉시 갈아 끼우면 "팝"이 보인다. 리페인트된
        // 배경은 원본과 형태가 아예 달라 더 심하다. 그래서 나가는 스프라이트를 타일마다
        // 하나씩 붙인 **고스트 렌더러**에 남겨 알파 아웃하고, 들어오는 쪽은 본체에
        // 즉시 꽂아 둔다. 고스트가 본체 **위**(order+1)에 서기 때문에
        // 합성 결과 = new·t + old·(1-t) 로 정확한 디졸브가 되고 — 둘 다 반투명하게
        // 만드는 방식과 달리 — 중간에 배경이 비쳐 어두워지는 구간이 없다.
        //
        // 고스트는 생성 후 파괴하지 않는다. 타일 렌더러당 1개를 사전(_ghosts)에 캐시해
        // 재사용하고, 쓰지 않을 때는 enabled=false로만 재운다 (게임 루프 Instantiate 금지).
        const float FadeSecondsMin = 2f;
        const float FadeSecondsMax = 3f;
        const float FadeSecondsLong = 5f;    // 롱블렌드(MidBoss 14~20초) 구간
        const float LongBlendThreshold = 10f;
        const int GhostSortingOffset = 1;

        readonly Dictionary<SpriteRenderer, SpriteRenderer> _ghosts =
            new Dictionary<SpriteRenderer, SpriteRenderer>();

        Sprite[] _layerShownSprite;   // 본체에 꽂아 둔 스프라이트 (들어오는 쪽)
        Sprite[] _layerFadeFrom;      // 고스트가 들고 있는 스프라이트 (나가는 쪽)
        float[] _layerFadeT;          // 0=교체 직후, 1=완료
        float _fadeSeconds = FadeSecondsMin;

        Sprite _landmarkShown;
        Sprite _landmarkFadeFrom;
        float _landmarkFadeT = 1f;

        string _currentThemeId;
        SectionKind _currentSection;
        bool _initialized;

        float _pendingDt;          // FixedUpdate에서 모은 게임 시간
        float _clock;              // 누적 게임 시간 (펄스 위상)
        float _blendT;
        float _blendDuration = 1f;
        float _sectionElapsed;

        float _flashLevel;         // 현재 섬광 알파
        float _flashDecay = 1f;    // 초당 감쇠
        Color _flashTint = Color.white;
        float _nextFlashIn;        // 다음 낙뢰까지 (게임 초)

        /// <summary>개발용 구간 미리보기 (F7). null이면 실제 진행을 따른다.</summary>
        SectionKind? _previewSection;

        // 중간보스 격파 = Late 진입 (REQ-101 C-E). 아래 MidBossDefeat* 값은 룩 테이블의
        // enterShake/enterFlash에 **덧대는** 하한이다 — 테마가 더 세게 잡았으면 그쪽을 쓴다.
        const float MidBossDefeatShake = 0.6f;
        const float MidBossDefeatFlashSeconds = 0.4f;
        static readonly Color MidBossDefeatFlash = new Color(1f, 0.97f, 0.88f, 0.6f);

        void Awake()
        {
            _from.SetNeutral();
            _target.SetNeutral();
            _current.SetNeutral();
        }

        void Start()
        {
            if (_slotKeys != null && _slotSprites != null)
            {
                int count = Mathf.Min(_slotKeys.Length, _slotSprites.Length);
                for (int i = 0; i < count; i++)
                    if (!string.IsNullOrEmpty(_slotKeys[i]) && _slotSprites[i] != null)
                        _slots[_slotKeys[i]] = _slotSprites[i];
            }

            _wash = CreateFullscreenQuad("SectionWash", _washSortingOrder);
            _flash = CreateFullscreenQuad("SectionFlash", _flashSortingOrder);
            _particles = new SectionParticleField(
                transform, _pixelSprite, _particleSortingOrder, HalfWidth, HalfHeight);
        }

        SpriteRenderer CreateFullscreenQuad(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _pixelSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = new Color(0f, 0f, 0f, 0f);
            renderer.enabled = false;

            // px_white는 2px(=0.125u)다. 월드 크기를 그대로 넣으면 1/8이 된다.
            float unitX = 1f, unitY = 1f;
            if (_pixelSprite != null)
            {
                Vector3 size = _pixelSprite.bounds.size;
                if (size.x > 0.0001f) unitX = size.x;
                if (size.y > 0.0001f) unitY = size.y;
            }
            // 화면 흔들림에도 가장자리가 드러나지 않게 넉넉히 덮는다.
            go.transform.localScale = new Vector3(
                (HalfWidth * 2f + 4f) / unitX, (HalfHeight * 2f + 3f) / unitY, 1f);
            return renderer;
        }

        void FixedUpdate()
        {
            // timeScale 0이면 Unity가 FixedUpdate를 아예 호출하지 않는다 —
            // 그것이 곧 "일시정지 중에는 전환도 멈춘다"는 요구를 만족시킨다.
            _pendingDt += Time.fixedDeltaTime;
        }

        void LateUpdate()
        {
            if (_director == null) return;

            float dt = _pendingDt;
            _pendingDt = 0f;
            _clock += dt;

            SyncBinding();
            SyncSection(dt);
            AdvanceBlend(dt);
            ApplyLook(dt);
            TickFlash(dt);
            if (_particles != null) _particles.Tick(dt);
        }

        // ── 구간 판정 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Core의 RunStageSection을 4구간으로 압축한다. Core는 RoomIndex/IsBiomeBoss로
        /// 이미 이 상태 기계를 굴리고 있으므로 Presentation이 따로 세지 않는다 —
        /// **중간보스 격파 = MidBoss→Closing 전이**가 그대로 Late 진입 이벤트가 된다.
        /// </summary>
        static SectionKind ToKind(RunStageSection section)
        {
            switch (section)
            {
                case RunStageSection.Opening: return SectionKind.Early;
                case RunStageSection.MidBoss: return SectionKind.MidBoss;
                case RunStageSection.Closing: return SectionKind.Late;
                case RunStageSection.StageBoss: return SectionKind.Boss;
                case RunStageSection.HiddenOpening: return SectionKind.Late;
                case RunStageSection.HiddenBoss: return SectionKind.Boss;
                default: return SectionKind.Early;
            }
        }

        void SyncSection(float dt)
        {
            string themeId = _director.CurrentThemeId;

            // 폴링만으로는 전환이 늦다: 중간보스를 잡아도 Core의 구간 상태는 보상 선택이
            // 끝나고 AdvanceRoom이 돌 때까지 MidBoss다. Core가 격파 프레임에 내는
            // MidBossDefeated(REQ-101 C-E)를 BattleDirector가 신호로 걸어 두면
            // **격파한 그 프레임에** Late 룩이 시작한다 (설계 원칙 2: 경계는 사건이다).
            bool defeatDriven = _director.MidBossDefeatSignaled;
            SectionKind section = _previewSection
                ?? (defeatDriven ? SectionKind.Late : ToKind(_director.StageSection));

            bool themeChanged = !_initialized
                || !string.Equals(themeId, _currentThemeId, System.StringComparison.Ordinal);
            bool sectionChanged = !_initialized || section != _currentSection;
            // 격파로 앞당겨 들어온 Late — 나중에 폴링이 따라잡아도 다시 전환하지 않는다
            // (section 값이 이미 Late라 sectionChanged가 false다).
            bool midBossDefeatEntry =
                sectionChanged && defeatDriven && !_previewSection.HasValue
                && section == SectionKind.Late;

            _sectionElapsed += dt;

            if (!themeChanged && !sectionChanged) return;

            _currentThemeId = themeId;
            _currentSection = section;
            _sectionElapsed = 0f;

            var theme = SectionThemeTable.Resolve(themeId, section);
            _targetTheme = theme;
            _from.CopyFrom(_current);
            _target.FromTheme(theme);

            if (themeChanged)
            {
                // 스테이지가 바뀌는 순간엔 보상/계약 화면이 끼어 있어 화면이 보이지 않는다.
                // 여기서 lerp를 걸면 새 스테이지 첫 20초가 이전 스테이지 색으로 시작한다.
                _blendT = 1f;
                _blendDuration = 1f;
                _current.CopyFrom(_target);
                if (_particles != null) _particles.Clear();
                // 스테이지 교체 = 화면이 보이지 않는 순간. 여기서 디졸브를 걸면 보상 화면이
                // 끝난 뒤 남은 페이드가 재생된다 — 즉시 스냅이 맞다.
                _fadeSeconds = 0f;
                ClearGhosts();
            }
            else
            {
                _blendT = 0f;
                _blendDuration = Mathf.Max(0.05f, theme.enterSeconds);
                // F7 미리보기는 20초를 기다릴 수 없다 — 눈으로 확인하는 용도라 빠르게 넘긴다.
                if (_previewSection.HasValue) _blendDuration = Mathf.Min(_blendDuration, 1.5f);

                // 디졸브 길이는 틴트 블렌드와 별개다. 짧은 사건성 전환(Late 3.5s·Boss 4s)은
                // 2~3초, 롱블렌드(MidBoss 14~20초)는 5초까지 늘려 "형태가 스미는" 시간을 준다.
                _fadeSeconds = _blendDuration >= LongBlendThreshold
                    ? FadeSecondsLong
                    : Mathf.Clamp(_blendDuration, FadeSecondsMin, FadeSecondsMax);
                if (_previewSection.HasValue) _fadeSeconds = Mathf.Min(_fadeSeconds, 1.5f);

                // 경계를 사건으로 만든다 (설계 원칙 2) — 중간보스 격파·요새 진입.
                // 격파 진입은 구간이 넘어갔다는 신호 자체라, 룩 테이블에 섬광이 없는
                // 테마(scrapyard·hive·core)에서도 반드시 번쩍인다. FlashReduced 게이트는
                // TriggerFlash가 피크 알파에 0.35를 곱해 처리한다 — 여기서 또 곱하지 않는다.
                float shake = midBossDefeatEntry
                    ? Mathf.Max(theme.enterShake, MidBossDefeatShake)
                    : theme.enterShake;
                if (_juice != null && shake > 0f)
                    _juice.Shake(shake);
                if (midBossDefeatEntry && theme.enterFlash.a < MidBossDefeatFlash.a)
                    TriggerFlash(MidBossDefeatFlash, MidBossDefeatFlashSeconds);
                else if (theme.enterFlash.a > 0f)
                    TriggerFlash(theme.enterFlash, theme.enterFlashSeconds);
            }

            _initialized = true;
            if (_particles != null)
                _particles.SetPreset(theme.particle, theme.particleDensity);
            ScheduleNextFlash(theme);
        }

        void AdvanceBlend(float dt)
        {
            if (_blendT >= 1f) return;
            _blendT = Mathf.Clamp01(_blendT + dt / _blendDuration);
            float eased = _blendT * _blendT * (3f - 2f * _blendT);   // smoothstep
            LookState.Lerp(_from, _target, eased, _current);
        }

        // ── 테마 루트 바인딩 ──────────────────────────────────────────────────────

        /// <summary>
        /// BattleDirector가 활성화한 테마 루트를 찾아 레이어 렌더러를 캐시한다.
        /// 인덱스가 아니라 "지금 켜져 있는 루트"를 보므로 director의 폴백 규칙과 항상 일치한다.
        /// </summary>
        void SyncBinding()
        {
            if (_themeRoots == null) return;
            if (_boundRoot != null && _boundRoot.activeSelf) return;

            GameObject active = null;
            for (int i = 0; i < _themeRoots.Length; i++)
                if (_themeRoots[i] != null && _themeRoots[i].activeSelf)
                {
                    active = _themeRoots[i];
                    break;
                }
            if (active == _boundRoot) return;

            RestoreBoundRoot();
            _boundRoot = active;
            _boundParallax = null;
            _layerRenderers = null;
            _layerOriginalSprites = null;
            _layerShownSprite = null;
            _layerFadeFrom = null;
            _layerFadeT = null;
            _landmark = null;
            _landmarkShown = null;
            _landmarkFadeFrom = null;
            _landmarkFadeT = 1f;
            if (active == null) return;

            _boundParallax = active.GetComponent<ParallaxBackground>();
            if (_boundParallax == null) return;
            _boundParallax.ResetFactorMultipliers();

            int layers = _boundParallax.LayerCount;
            _layerRenderers = new SpriteRenderer[layers][];
            _layerOriginalSprites = new Sprite[layers];
            _layerShownSprite = new Sprite[layers];
            _layerFadeFrom = new Sprite[layers];
            _layerFadeT = new float[layers];
            for (int i = 0; i < layers; i++)
            {
                _layerFadeT[i] = 1f;
                var layer = _boundParallax.GetLayer(i);
                if (layer == null)
                {
                    _layerRenderers[i] = System.Array.Empty<SpriteRenderer>();
                    continue;
                }
                // 고스트도 SpriteRenderer라 GetComponentsInChildren에 딸려 온다 — 배제한다.
                // (지금은 고스트가 붙기 전에 바인딩되지만, 테마 루트를 껐다 켜면 걸린다.)
                var found = layer.GetComponentsInChildren<SpriteRenderer>(true);
                int keep = 0;
                for (int k = 0; k < found.Length; k++)
                    if (!IsGhost(found[k])) found[keep++] = found[k];
                if (keep != found.Length) System.Array.Resize(ref found, keep);
                _layerRenderers[i] = found;
                if (_layerRenderers[i].Length > 0)
                    _layerOriginalSprites[i] = _layerRenderers[i][0].sprite;
                _layerShownSprite[i] = _layerOriginalSprites[i];
                // 고스트를 여기서 미리 만든다 — 전투 중 첫 전환 프레임에 GameObject를
                // 만들지 않기 위해서다. 바인딩은 스테이지 경계(보상 화면)에서만 일어난다.
                for (int r = 0; r < _layerRenderers[i].Length; r++) GetGhost(_layerRenderers[i][r]);
            }

            var landmark = active.transform.Find("Landmark");
            if (landmark != null)
            {
                _landmark = landmark.GetComponent<SpriteRenderer>();
                if (_landmark != null)
                {
                    _landmark.sortingOrder = _landmarkSortingOrder;
                    GetGhost(_landmark);
                }
            }
        }

        /// <summary>테마를 떠날 때 이전 루트를 원상복구한다 — 다시 켜질 때 지난 구간의
        /// 틴트가 남아 있으면 안 된다.</summary>
        void RestoreBoundRoot()
        {
            ClearGhosts();
            if (_layerRenderers == null) return;
            for (int i = 0; i < _layerRenderers.Length; i++)
            {
                var renderers = _layerRenderers[i];
                if (renderers == null) continue;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null) continue;
                    renderers[r].color = Color.white;
                    renderers[r].enabled = true;
                    if (_layerOriginalSprites != null && _layerOriginalSprites[i] != null)
                        renderers[r].sprite = _layerOriginalSprites[i];
                }
            }
            if (_boundParallax != null) _boundParallax.ResetFactorMultipliers();
            if (_landmark != null) _landmark.enabled = false;
        }

        // ── 크로스페이드 (고스트 렌더러) ──────────────────────────────────────────

        static bool IsGhost(SpriteRenderer renderer) =>
            renderer != null && renderer.gameObject.name == GhostName;

        const string GhostName = "__XFadeGhost";

        /// <summary>타일 렌더러에 딸린 고스트를 가져온다(없으면 한 번만 만든다).
        /// 부모의 자식이라 패럴랙스 래핑·스케일을 그대로 따라간다.</summary>
        SpriteRenderer GetGhost(SpriteRenderer host)
        {
            if (host == null) return null;
            if (_ghosts.TryGetValue(host, out var ghost) && ghost != null) return ghost;

            var go = new GameObject(GhostName);
            go.transform.SetParent(host.transform, false);
            ghost = go.AddComponent<SpriteRenderer>();
            ghost.sortingLayerID = host.sortingLayerID;
            ghost.sortingOrder = host.sortingOrder + GhostSortingOffset;
            ghost.enabled = false;
            _ghosts[host] = ghost;
            return ghost;
        }

        /// <summary>고스트를 한 프레임분 반영한다. 파괴하지 않고 재우기만 한다.</summary>
        void DriveGhost(SpriteRenderer host, Sprite from, Color tint, float alpha, bool wanted)
        {
            if (host == null) return;
            if (!wanted || from == null || alpha <= 0.004f)
            {
                if (_ghosts.TryGetValue(host, out var sleeping) && sleeping != null && sleeping.enabled)
                    sleeping.enabled = false;
                return;
            }

            var ghost = GetGhost(host);
            if (ghost == null) return;
            if (ghost.sprite != from) ghost.sprite = from;
            // 정렬은 본체를 따라간다 — 씬 빌더가 order를 바꿔도 어긋나지 않는다.
            int order = host.sortingOrder + GhostSortingOffset;
            if (ghost.sortingOrder != order) ghost.sortingOrder = order;
            if (ghost.sortingLayerID != host.sortingLayerID) ghost.sortingLayerID = host.sortingLayerID;
            var color = tint;
            color.a = tint.a * alpha;
            ghost.color = color;
            if (!ghost.enabled) ghost.enabled = true;
        }

        void ClearGhosts()
        {
            foreach (var pair in _ghosts)
                if (pair.Value != null) pair.Value.enabled = false;
        }

        static void AdvanceFade(ref float t, float dt, float duration)
        {
            if (t >= 1f) return;
            t = duration <= 0.0001f ? 1f : Mathf.Clamp01(t + dt / duration);
        }

        /// <summary>디졸브는 선형이면 중간이 흐리다 — smoothstep으로 양 끝을 붙인다.</summary>
        static float Ease(float t) => t * t * (3f - 2f * t);

        // ── 적용 ──────────────────────────────────────────────────────────────────

        void ApplyLook(float dt)
        {
            ApplyLayers(dt);
            ApplyWash();
            ApplyLandmark(dt);
        }

        void ApplyLayers(float dt)
        {
            if (_boundParallax == null || _layerRenderers == null) return;

            for (int i = 0; i < _layerRenderers.Length; i++)
            {
                var renderers = _layerRenderers[i];
                if (renderers == null || renderers.Length == 0) continue;

                int role = (int)_boundParallax.GetRole(i);
                if (role < 0 || role >= RoleCount) role = (int)BgLayerRole.Mid;

                Color tint = _current.tint[role];
                bool visible = tint.a > 0.004f;

                BeginLayerFade(i, ResolveLayerSprite(i, (BgLayerRole)role));
                AdvanceFade(ref _layerFadeT[i], dt, _fadeSeconds);

                Sprite shown = _layerShownSprite[i];
                Sprite from = _layerFadeFrom[i];
                float ghostAlpha = 1f - Ease(_layerFadeT[i]);
                bool fading = visible && from != null && ghostAlpha > 0.004f;

                for (int r = 0; r < renderers.Length; r++)
                {
                    var sr = renderers[r];
                    if (sr == null) continue;
                    if (sr.enabled != visible) sr.enabled = visible;
                    if (visible)
                    {
                        // 들어오는 스프라이트는 처음부터 불투명하다. 고스트가 그 **위**에서
                        // 사라지므로 결과는 new·t + old·(1-t) — 중간에 배경이 비치지 않는다.
                        if (shown != null && sr.sprite != shown) sr.sprite = shown;
                        sr.color = tint;
                    }
                    DriveGhost(sr, from, tint, ghostAlpha, fading);
                }

                if (!fading && from != null && _layerFadeT[i] >= 1f) _layerFadeFrom[i] = null;

                _boundParallax.SetFactorMultiplier(i, _current.scrollMul[role]);
            }
        }

        /// <summary>원경/전경 교체 슬롯을 스프라이트로 푼다. 키가 없거나 아트가 빠졌으면
        /// 테마 원본으로 돌아간다.</summary>
        Sprite ResolveLayerSprite(int layerIndex, BgLayerRole role)
        {
            string slot = null;
            if (_targetTheme != null && _targetTheme.layers != null)
                for (int i = 0; i < _targetTheme.layers.Length; i++)
                    if (_targetTheme.layers[i] != null && _targetTheme.layers[i].role == role)
                    {
                        slot = _targetTheme.layers[i].spriteSlot;
                        break;
                    }

            Sprite wanted = null;
            if (!string.IsNullOrEmpty(slot)) _slots.TryGetValue(slot, out wanted);
            if (wanted == null && _layerOriginalSprites != null)
                wanted = _layerOriginalSprites[layerIndex];
            return wanted;
        }

        /// <summary>교체가 필요하면 이전 스프라이트를 고스트로 넘기고 디졸브를 시작한다.</summary>
        void BeginLayerFade(int layerIndex, Sprite wanted)
        {
            if (wanted == null) return;
            Sprite shown = _layerShownSprite[layerIndex];
            if (shown == wanted) return;

            // 디졸브 도중 또 바뀌면 현재 고스트를 버리고 방금 것을 새 고스트로 삼는다
            // (고스트를 겹겹이 쌓지 않는다 — 렌더러 수가 폭발한다).
            _layerFadeFrom[layerIndex] = shown;
            _layerShownSprite[layerIndex] = wanted;
            _layerFadeT[layerIndex] = shown == null || _fadeSeconds <= 0.0001f ? 1f : 0f;
        }

        void ApplyWash()
        {
            if (_wash == null) return;
            var color = _current.wash;
            if (_current.pulseAmp > 0.0001f)
                color.a += _current.pulseAmp
                           * Mathf.Sin(_clock * _current.pulseHz * Mathf.PI * 2f);
            color.a = Mathf.Clamp01(color.a);

            bool visible = color.a > 0.004f;
            if (_wash.enabled != visible) _wash.enabled = visible;
            if (visible) _wash.color = color;
        }

        void ApplyLandmark(float dt)
        {
            if (_landmark == null) return;

            Sprite wanted = null;
            if (_targetTheme != null && !string.IsNullOrEmpty(_targetTheme.landmarkSlot))
                _slots.TryGetValue(_targetTheme.landmarkSlot, out wanted);

            // 랜드마크는 켜짐/꺼짐 자체가 팝이다 (허공에 난파선이 툭 나타난다).
            // 나타남은 알파 인, 사라짐은 고스트 알파 아웃, 교체는 디졸브로 처리한다.
            if (wanted != _landmarkShown)
            {
                _landmarkFadeFrom = _landmarkShown;
                _landmarkShown = wanted;
                _landmarkFadeT = _fadeSeconds <= 0.0001f ? 1f : 0f;
            }
            AdvanceFade(ref _landmarkFadeT, dt, _fadeSeconds);

            float t = Ease(_landmarkFadeT);
            Color tint = _current.tint[(int)BgLayerRole.Far];
            bool visible = _landmarkShown != null;

            if (visible)
            {
                if (_landmark.sprite != _landmarkShown) _landmark.sprite = _landmarkShown;
                var color = tint;
                // 새로 들어오는 랜드마크는 뒤에 받쳐 줄 이전 그림이 없을 수도 있다 —
                // 그 경우(고스트 없음)에는 본체가 직접 알파 인 한다.
                if (_landmarkFadeFrom == null) color.a = tint.a * t;
                _landmark.color = color;

                // 접근감: 구간 경과에 따라 커진다 (St5 최심부의 거대 코어).
                float grow = _targetTheme.landmarkGrowSeconds > 0.01f
                    ? Mathf.Clamp01(_sectionElapsed / _targetTheme.landmarkGrowSeconds)
                    : 1f;
                float scale = Mathf.Lerp(
                    _targetTheme.landmarkScaleStart, _targetTheme.landmarkScaleEnd, grow);
                _landmark.transform.localScale = new Vector3(scale, scale, 1f);
            }

            // 고스트는 본체의 자식 렌더러라 본체가 꺼져도 계속 그려진다 —
            // 랜드마크가 사라지는 전환도 툭 꺼지지 않고 스러진다.
            float ghostAlpha = 1f - t;
            bool ghostWanted = _landmarkFadeFrom != null && ghostAlpha > 0.004f;
            DriveGhost(_landmark, _landmarkFadeFrom, tint, ghostAlpha, ghostWanted);
            if (!ghostWanted) _landmarkFadeFrom = null;

            if (_landmark.enabled != visible) _landmark.enabled = visible;
        }

        // ── 섬광 (낙뢰 · 진입 스냅) ───────────────────────────────────────────────

        void ScheduleNextFlash(SectionTheme theme)
        {
            if (theme == null || theme.flashIntervalMax <= 0f)
            {
                _nextFlashIn = 0f;
                return;
            }
            float min = Mathf.Max(0.5f, theme.flashIntervalMin);
            float max = Mathf.Max(min + 0.5f, theme.flashIntervalMax);
            // 접근성: 번쩍임 감소 모드에서는 간격을 두 배로 (완전히 없애면 뇌운이 죽는다).
            if (_juice != null && _juice.FlashReduced) { min *= 2f; max *= 2f; }
            _nextFlashIn = Random.Range(min, max);
        }

        void TriggerFlash(Color color, float seconds)
        {
            float peak = color.a;
            if (_juice != null && _juice.FlashReduced) peak *= 0.35f;
            _flashTint = color;
            _flashLevel = Mathf.Max(_flashLevel, peak);
            _flashDecay = peak / Mathf.Max(0.05f, seconds);
        }

        void TickFlash(float dt)
        {
            if (_nextFlashIn > 0f)
            {
                _nextFlashIn -= dt;
                if (_nextFlashIn <= 0f && _targetTheme != null)
                {
                    TriggerFlash(_targetTheme.flashColor, _targetTheme.flashSeconds);
                    ScheduleNextFlash(_targetTheme);
                }
            }

            if (_flash == null) return;
            if (_flashLevel > 0f)
            {
                _flashLevel = Mathf.Max(0f, _flashLevel - _flashDecay * dt);
                var color = _flashTint;
                color.a = _flashLevel;
                _flash.color = color;
                if (!_flash.enabled) _flash.enabled = true;
            }
            else if (_flash.enabled)
            {
                _flash.enabled = false;
            }
        }

        // ── 개발용 ────────────────────────────────────────────────────────────────

        /// <summary>F7: 실제 진행과 무관하게 다음 구간 룩을 강제한다. 순수 표현이라
        /// 치트가 아니다 (제출을 막지 않는다) — 20룩을 눈으로 확인하려면 필요하다.</summary>
        public void DevCycleSectionPreview()
        {
            if (!_previewSection.HasValue) _previewSection = SectionKind.Early;
            else if (_previewSection.Value == SectionKind.Boss) _previewSection = null;
            else _previewSection = (SectionKind)((int)_previewSection.Value + 1);
        }

        public string DevPreviewLabel =>
            _previewSection.HasValue ? _previewSection.Value.ToString() : "live";

        public SectionKind CurrentSection => _currentSection;
    }
}
