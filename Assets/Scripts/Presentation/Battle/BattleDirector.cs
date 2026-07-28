using System.Collections.Generic;
using Shmup.Core;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// Core 시뮬레이션과 씬을 잇는 유일한 지점.
    ///
    /// 하는 일: (1) 고정 60Hz로 IBattleSim.Step 호출, (2) 시뮬 상태를 트랜스폼에 복사,
    /// (3) 탄 뷰를 풀에서 빌리고 돌려주기. 그 외에는 아무 결정도 하지 않는다 —
    /// 이동량, 발사 쿨다운, 탄 위치, 화면 밖 컬링은 전부 Shmup.Core 소관이다.
    ///
    /// 틱은 FixedUpdate에서만 돈다. Fixed Timestep은 SimSpace.TicksPerSecond(60)와
    /// 맞춰야 한다 (ProjectSettings/TimeManager.asset = 0.0166666667).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class BattleDirector : MonoBehaviour
    {
        [Header("Scene wiring")]
        [SerializeField] PlayerInputReader _input;
        [SerializeField] Transform _playerTransform;
        [SerializeField] GameObject _bulletPrefab;
        [SerializeField] Transform _bulletRoot;
        [SerializeField] GameObject _enemyPrefab;
        [SerializeField] Transform _enemyRoot;
        [SerializeField] GameObject _capsulePrefab;
        [SerializeField] Transform _capsuleRoot;
        [SerializeField] GameObject _explosionPrefab;
        [SerializeField] Transform _fxRoot;
        [SerializeField] SpriteRenderer _damageFlash;
        [SerializeField] Sprite _missileSprite;
        [SerializeField] GameObject _optionPrefab;
        [SerializeField] Transform _optionRoot;
        [SerializeField] SpriteRenderer _shieldView;
        [SerializeField] SfxPlayer _sfx;
        [Tooltip("폭발 애니메이션 프레임 (M2). 비어 있으면 단일 스프라이트 확대+페이드 폴백.")]
        [SerializeField] Sprite[] _explosionFrames;
        [SerializeField] Sprite _enemyShotSprite;
        [SerializeField] SpriteRenderer _bossRenderer;
        [SerializeField] GameObject _bossHpRoot;
        [SerializeField] Transform _bossHpFill;
        [Tooltip("definitionId 접두어 → 전용 스프라이트 (M2 적 비주얼 다양화). 매칭 실패 시 기본+틴트 폴백.")]
        [SerializeField] string[] _enemySpritePrefixes;
        [SerializeField] Sprite[] _enemySprites;
        [SerializeField] BossIntro _bossIntro;
        [Tooltip("스테이지 테마 배경 루트들. StagePlan.ThemeId 매칭 우선, 없으면 (StageIndex-1)%개수 로테이션 (M3).")]
        [SerializeField] GameObject[] _themeBackgrounds;
        [SerializeField] string[] _themeIds;
        [Tooltip("StagePlan.BossId 접두어 → 보스 스프라이트.")]
        [SerializeField] string[] _bossSpritePrefixes;
        [SerializeField] Sprite[] _bossSprites;

        // 아이들 애니메이션 (M4): 접두어별 프레임 시퀀스를 평탄화해 직렬화 (빌더가 채움).
        [SerializeField] string[] _animPrefixes;
        [SerializeField] int[] _animFrameCounts;
        [SerializeField] Sprite[] _animFrames;
        [SerializeField] float _animFramesPerSecond = 8f;

        /// <summary>HP 바 폭 (월드유닛). px_white(2px) 스프라이트 기준 스케일 환산에 쓴다.</summary>
        const float BossHpBarWidthUnits = 16f;
        const float WhiteSpriteUnits = 2f / 16f;

        [Header("Run")]
        [Tooltip("로그라이크 시드. 같은 시드 + 같은 입력 = 같은 결과 (AGENTS.md §4).")]
        [SerializeField] long _seed = 1;


        RunManager _run;
        MetaState _meta;              // 함선 해금 메타 (저장 채널: MetaSave)
        int _lastCreditedRunNumber;   // 런당 1회만 점수 적립
        IBattleSim _sim;   // 매 스텝 _run.Battle로 갱신 — 스테이지 전환/재시작 시 인스턴스가 교체된다
        SpritePool _bulletPool;
        SpritePool _enemyPool;
        SpritePool _capsulePool;

        // Id → 뷰 인스턴스. Core가 주는 Id는 스폰~소멸까지 불변이라 매칭 키로 쓸 수 있다.
        readonly Dictionary<int, Transform> _bulletViews = new Dictionary<int, Transform>(64);
        readonly Dictionary<int, Transform> _enemyViews = new Dictionary<int, Transform>(32);
        readonly Dictionary<int, Transform> _capsuleViews = new Dictionary<int, Transform>(16);
        readonly Dictionary<int, Transform> _optionViews = new Dictionary<int, Transform>(4);
        readonly Dictionary<int, SpriteRenderer> _enemyRenderers = new Dictionary<int, SpriteRenderer>(32);
        SpritePool _optionPool;
        Sprite _mainShotSprite;   // Awake에서 탄 프리팹 원본 스프라이트 캡처
        readonly HashSet<int> _aliveIds = new HashSet<int>();
        readonly List<int> _retiredIds = new List<int>(16);

        // 시각 이펙트 (순수 표현 — 게임 상태에 영향 없음)
        SpritePool _fxPool;
        readonly List<Transform> _activeFx = new List<Transform>(16);
        readonly List<SpriteRenderer> _activeFxRenderers = new List<SpriteRenderer>(16);
        readonly List<float> _activeFxAges = new List<float>(16);
        const float ExplosionDuration = 0.28f;
        int _lastHp = -1;
        float _damageFlashAge = float.MaxValue;
        const float DamageFlashDuration = 0.3f;

        // 적 종류 구분용 틴트 (플레이스홀더 아트 한정 — 실제 스프라이트가 생기면 제거)
        static readonly Color32[] EnemyTints =
        {
            new Color32(0xE8, 0x6A, 0x6A, 0xFF),   // zako_straight
            new Color32(0xB4, 0x7A, 0xE8, 0xFF),   // zako_sine
            new Color32(0x8C, 0x8C, 0x9C, 0xFF)    // turret_ground
        };

        public int Tick => _sim?.Tick ?? 0;
        public int ActiveBulletViews => _bulletViews.Count;

        /// <summary>현재 런의 시드. --seed=N 커맨드라인 → 타이틀 입력 → 인스펙터 순으로 결정되고, 재시작 시 갱신된다.</summary>
        public long Seed { get; private set; }

        /// <summary>파워업 게이지 (Core/RunManager 소유). HUD가 읽어서 그린다. 재시작 시 승계 적용된 새 인스턴스로 바뀐다.</summary>
        public PowerUpGauge Gauge => _run?.PowerUpGauge;

        public long TotalScore => _run?.TotalScore ?? 0;
        /// <summary>런 전체 통계 (완료 스테이지 + 현재 전투 합산 — Core 권위 값).</summary>
        public RunStatistics RunStats => _run != null ? _run.Statistics : default;
        /// <summary>현재 스테이지 테마 (BgmPlayer 등 표현 계층 참조용). null 가능.</summary>
        public string CurrentThemeId => _run != null && _run.StagePlan != null ? _run.StagePlan.ThemeId : null;
        public int RunNumber => _run?.RunNumber ?? 0;
        public int StageIndex => _run?.StageIndex ?? 0;
        public int Difficulty => _run?.Difficulty ?? 0;
        public bool IsRunOver => _run != null && _run.State == RunState.RunOver;

        /// <summary>런 오버 상태에서만 유효. 새 시드로 재출격 — 파워업 레벨은 MetaProgression 승계를 따른다.</summary>
        public void RestartRun()
        {
            if (_run == null || _run.State != RunState.RunOver) return;
            ulong newSeed = (uint)System.Environment.TickCount ^ ((ulong)(uint)_run.RunNumber << 32);
            _run.Restart(newSeed);
            Seed = (long)newSeed;
            RefreshBattle();
            SyncViews();
        }

        /// <summary>서브유닛 ScrollX의 월드 단위 값. 배경 패럴랙스가 읽는다.</summary>
        public float ScrollWorldX => _sim != null ? _sim.ScrollX * SimView.WorldUnitsPerSubUnit : 0f;

        public int PlayerHp => _sim?.PlayerHp ?? 0;

        void Awake()
        {
            if (!ValidateWiring()) return;

            Seed = DevArgs.OverrideSeed ?? DevArgs.RuntimeSeed ?? _seed;

            // GameData JSON이 유일한 원본 (AGENTS.md §5). 씬 재생성 시 Resources로 복사되고,
            // 파싱·단위 변환은 전부 Core(GameDataParser) 소관이다.
            var data = GameDataParser.Parse(
                LoadGameDataText("enemies"),
                LoadGameDataText("weapons"),
                LoadGameDataText("waves"),
                TryLoadGameDataText("rewards"),    // 없으면 Core 내장 풀 폴백 (REQ-G001)
                TryLoadGameDataText("ships"));     // 없으면 기본 함선 1척

            // 격납고 선택 함선 (저장이 채널 — Title 씬 HangarScreen이 기록)
            _meta = MetaSave.Load(data);
            var selectedShip = data.FindShip(_meta.SelectedShipId) ?? data.DefaultShip;

            var config = data.CreateBattleSimConfig();
            // 스키마에 아직 없는 잠정값 (스키마 v3 후보 — GameData로 옮기면 이 블록 제거)
            // EnemyDespawnX는 REQ-005 이후 Core 기본값(-22u, SimSpace 상수 파생)을 그대로 쓴다.
            // 캡슐 히트박스는 새 16×14px 스프라이트 기준 ×1.5.
            config.CapsuleHalfWidth = SimSpace.SubUnitsPerWorldUnit * 15 / 32;
            config.CapsuleHalfHeight = SimSpace.SubUnitsPerWorldUnit * 3 / 8;
            config.PlayerMaxHp = 3;

            // 런 수명은 Core(RunManager) 소관: 스테이지 전환, 난이도 곡선, 사망 감지,
            // 재시작 시 파워업 승계까지. Presentation은 Step을 돌리고 Battle을 그릴 뿐이다.
            _run = new RunManager(
                (ulong)Seed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                data.CreatePowerUpGauge(),
                data.Rewards,
                selectedShip);
            _sim = _run.Battle;

            // 풀 용량은 Core가 허용하는 최대 개수와 맞춘다 — 런타임에 풀이 부족해질 수 없다.
            _bulletPool = new SpritePool(_bulletPrefab, _bulletRoot, config.MaxBullets, "Bullet");
            _enemyPool = new SpritePool(_enemyPrefab, _enemyRoot, 32, "Enemy");
            _capsulePool = new SpritePool(_capsulePrefab, _capsuleRoot, 16, "Capsule");
            _fxPool = new SpritePool(_explosionPrefab, _fxRoot, 16, "Explosion");
            _optionPool = new SpritePool(_optionPrefab, _optionRoot, 4, "Option");

            var bulletPrefabRenderer = _bulletPrefab.GetComponent<SpriteRenderer>();
            _mainShotSprite = bulletPrefabRenderer != null ? bulletPrefabRenderer.sprite : null;

            if (_damageFlash != null)
                _damageFlash.color = new Color(1f, 0.2f, 0.2f, 0f);
            if (_shieldView != null)
                _shieldView.enabled = false;

            ApplyStageTheme();
            ApplyBossSprite();
            SyncViews();
        }

        void Update()
        {
            AnimateExplosions();
            AnimateDamageFlash();
        }

        void FixedUpdate()
        {
            if (_run == null) return;
            _run.Step(_input.ConsumeCommand());

            // 런 종료 시 점수를 메타 재화로 1회 적립 (함선 해금 재원)
            if (_run.State == RunState.RunOver
                && _meta != null
                && _run.RunNumber != _lastCreditedRunNumber)
            {
                _lastCreditedRunNumber = _run.RunNumber;
                _meta.CreditScore(_run.TotalScore);
                MetaSave.Save(_meta);
            }

            // 이벤트는 스텝 직후 같은 호출 안에서 소비한다 — 다음 Step에서 클리어되기 때문.
            var events = _run.Battle.EventsThisTick;
            if (_sfx != null)
                _sfx.PlayEvents(events);
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (e.Type == SimEventType.EnemyKilled || e.Type == SimEventType.PlayerKilled)
                    SpawnExplosion(SimView.ToWorld(e.X, e.Y));
                else if (e.Type == SimEventType.BossSpawned && _bossIntro != null)
                    _bossIntro.Trigger();
            }
            RefreshBattle();
            SyncViews();
        }

        /// <summary>
        /// Core는 스테이지 전환/재시작 때 IBattleSim 인스턴스를 교체한다. Id 공간이
        /// 새로 시작되므로 이전 배틀의 뷰를 전부 반납하고 새 인스턴스로 갈아탄다.
        /// </summary>
        void RefreshBattle()
        {
            var battle = _run.Battle;
            if (ReferenceEquals(battle, _sim)) return;

            ReleaseAll(_bulletViews, _bulletPool);
            ReleaseAll(_enemyViews, _enemyPool);
            ReleaseAll(_capsuleViews, _capsulePool);
            ReleaseAll(_optionViews, _optionPool);
            _enemyRenderers.Clear();
            _lastHp = -1;   // 배틀 교체 직후 HP 차이를 피격 플래시로 오인하지 않게

            _sim = battle;
            ApplyStageTheme();
            ApplyBossSprite();
        }

        /// <summary>
        /// Core가 정한 StagePlan.ThemeId로 배경을 고른다 (테마-보스 바인딩과 일치 보장).
        /// ThemeId가 없거나 미등록이면 스테이지 번호 로테이션 폴백.
        /// </summary>
        void ApplyStageTheme()
        {
            if (_themeBackgrounds == null || _themeBackgrounds.Length == 0 || _run == null) return;

            int index = -1;
            string themeId = _run.StagePlan != null ? _run.StagePlan.ThemeId : null;
            if (!string.IsNullOrEmpty(themeId) && _themeIds != null)
            {
                int count = Mathf.Min(_themeIds.Length, _themeBackgrounds.Length);
                for (int i = 0; i < count; i++)
                    if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
            }
            if (index < 0)
            {
                index = (_run.StageIndex - 1) % _themeBackgrounds.Length;
                if (index < 0) index += _themeBackgrounds.Length;
            }

            for (int i = 0; i < _themeBackgrounds.Length; i++)
                if (_themeBackgrounds[i] != null && _themeBackgrounds[i].activeSelf != (i == index))
                    _themeBackgrounds[i].SetActive(i == index);
        }

        void ApplyBossSprite()
        {
            if (_bossRenderer == null || _run == null || _run.StagePlan == null) return;
            if (_bossSpritePrefixes == null || _bossSprites == null) return;
            string bossId = _run.StagePlan.BossId;
            int count = Mathf.Min(_bossSpritePrefixes.Length, _bossSprites.Length);
            Sprite best = null;
            int bestLength = -1;
            for (int i = 0; i < count; i++)
            {
                string prefix = _bossSpritePrefixes[i];
                if (string.IsNullOrEmpty(prefix) || _bossSprites[i] == null) continue;
                if (bossId.StartsWith(prefix, System.StringComparison.Ordinal)
                    && prefix.Length > bestLength)
                {
                    best = _bossSprites[i];
                    bestLength = prefix.Length;
                }
            }
            if (best != null)
                _bossRenderer.sprite = best;
        }

        static void ReleaseAll(Dictionary<int, Transform> views, SpritePool pool)
        {
            foreach (var pair in views) pool.Release(pair.Value);
            views.Clear();
        }

        /// <summary>시뮬 상태를 트랜스폼에 그대로 복사한다. 보간 없음 — 픽셀 퍼펙트라 스냅이 맞다.</summary>
        void SyncViews()
        {
            _playerTransform.localPosition = SimView.ToWorld(_sim.PlayerX, _sim.PlayerY);

            SyncBullets();
            SyncOptions();
            SyncEnemies();
            SyncCapsules();
            SyncShield();
            SyncBoss();
        }

        Sprite SpriteForBulletKind(BulletKind kind)
        {
            if (kind == BulletKind.Missile && _missileSprite != null) return _missileSprite;
            if (kind == BulletKind.EnemyShot && _enemyShotSprite != null) return _enemyShotSprite;
            return _mainShotSprite;
        }

        void SyncBoss()
        {
            if (_bossRenderer == null) return;
            bool active = _sim.BossActive;
            if (_bossRenderer.enabled != active)
                _bossRenderer.enabled = active;
            if (_bossHpRoot != null && _bossHpRoot.activeSelf != active)
                _bossHpRoot.SetActive(active);
            if (!active) return;

            _bossRenderer.transform.localPosition = SimView.ToWorld(_sim.Boss.X, _sim.Boss.Y);
            if (_run != null && _run.StagePlan != null)
                ApplyIdleAnimation(_bossRenderer, _run.StagePlan.BossId, 0);

            if (_bossHpFill != null && _sim.Boss.MaxHp > 0)
            {
                float fraction = Mathf.Clamp01((float)_sim.Boss.Hp / _sim.Boss.MaxHp);
                var scale = _bossHpFill.localScale;
                scale.x = BossHpBarWidthUnits / WhiteSpriteUnits * fraction;
                _bossHpFill.localScale = scale;
                var position = _bossHpFill.localPosition;
                position.x = -BossHpBarWidthUnits * (1f - fraction) / 2f;
                _bossHpFill.localPosition = position;
            }
        }

        // ── 보상 선택 (RunManager AwaitingReward — RewardScreen이 소비) ─────────

        public bool AwaitingReward => _run != null && _run.State == RunState.AwaitingReward;
        public System.Collections.Generic.IReadOnlyList<RewardOption> RewardOptions
            => _run?.RewardOptions;

        public void ChooseReward(int index)
        {
            if (!AwaitingReward) return;
            _run.ChooseReward(index);
            RefreshBattle();
            SyncViews();
        }

        /// <summary>
        /// 개발용 빨리감기 (DevCheats F11): 무입력 틱을 일괄 진행한다. 이벤트 FX/SFX는
        /// 스킵 구간에서 생략한다 — 순수 dev 편의, 게임플레이 판정은 전부 Core 그대로.
        /// </summary>
        public void DevFastForward(int ticks)
        {
            if (_run == null) return;
            var none = InputCommand.None;
            for (int i = 0; i < ticks && _run.State == RunState.Playing; i++)
                _run.Step(in none);
            RefreshBattle();
            SyncViews();
        }

        /// <summary>DevCheats 오버레이용.</summary>
        public int ShieldRemaining => _sim?.ShieldRemaining ?? 0;

        /// <summary>선택적 GameData — 없으면 null (Core가 폴백 처리).</summary>
        static string TryLoadGameDataText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            return asset != null ? asset.text : null;
        }

        static string LoadGameDataText(string name)
        {
            var asset = Resources.Load<TextAsset>("GameData/" + name);
            if (asset == null)
                throw new System.InvalidOperationException(
                    $"Resources/GameData/{name} 를 찾을 수 없다. Tools → Shmup → Rebuild Battle Scene 으로 " +
                    "GameData JSON 복사를 다시 실행해라.");
            return asset.text;
        }

        void SyncBullets()
        {
            var bullets = _sim.Bullets;
            _aliveIds.Clear();

            for (int i = 0; i < bullets.Count; i++)
            {
                var bullet = bullets[i];
                _aliveIds.Add(bullet.Id);

                if (!_bulletViews.TryGetValue(bullet.Id, out var view))
                {
                    view = _bulletPool.Acquire();
                    if (view == null) continue;   // 풀 소진 — 경고는 풀이 이미 냈다
                    _bulletViews.Add(bullet.Id, view);

                    // Kind는 Id 수명 동안 불변 — 획득 시 한 번만 스프라이트를 고른다.
                    var renderer = view.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                        renderer.sprite = SpriteForBulletKind(bullet.Kind);
                }

                view.localPosition = SimView.ToWorld(bullet.X, bullet.Y);
            }

            ReleaseDeadViews(_bulletViews, _bulletPool);
        }

        void SyncOptions()
        {
            var options = _sim.Options;
            _aliveIds.Clear();

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                _aliveIds.Add(option.Index);

                if (!_optionViews.TryGetValue(option.Index, out var view))
                {
                    view = _optionPool.Acquire();
                    if (view == null) continue;
                    _optionViews.Add(option.Index, view);
                }

                view.localPosition = SimView.ToWorld(option.X, option.Y);
            }

            ReleaseDeadViews(_optionViews, _optionPool);
        }

        void SyncShield()
        {
            if (_shieldView == null) return;
            int remaining = _sim.ShieldRemaining;
            _shieldView.enabled = remaining > 0;
            if (remaining > 0)
            {
                var c = _shieldView.color;
                c.a = 0.25f + 0.15f * Mathf.Min(remaining, 3);
                _shieldView.color = c;
            }
        }

        void SyncEnemies()
        {
            var enemies = _sim.Enemies;
            _aliveIds.Clear();

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                _aliveIds.Add(enemy.Id);

                if (!_enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = _enemyPool.Acquire();
                    if (view == null) continue;
                    _enemyViews.Add(enemy.Id, view);

                    var renderer = view.GetComponent<SpriteRenderer>();
                    _enemyRenderers[enemy.Id] = renderer;
                    if (renderer != null)
                    {
                        Sprite typed = SpriteForEnemy(enemy.DefinitionId);
                        if (typed != null)
                        {
                            renderer.sprite = typed;
                            renderer.color = Color.white;
                        }
                        else
                        {
                            renderer.color = TintFor(enemy.DefinitionId);
                        }
                    }
                }

                view.localPosition = SimView.ToWorld(enemy.X, enemy.Y);
                if (_enemyRenderers.TryGetValue(enemy.Id, out var animRenderer))
                    ApplyIdleAnimation(animRenderer, enemy.DefinitionId, enemy.Id);
            }

            ReleaseDeadEnemies();
        }

        /// <summary>
        /// 적 뷰 반납. 폭발은 이제 Core의 EnemyKilled 이벤트가 정본이다 (REQ-005) —
        /// 화면 밖 despawn과 처치를 위치로 추측하던 휴리스틱은 제거했다.
        /// </summary>
        void ReleaseDeadEnemies()
        {
            _retiredIds.Clear();
            foreach (var pair in _enemyViews)
                if (!_aliveIds.Contains(pair.Key))
                    _retiredIds.Add(pair.Key);

            for (int i = 0; i < _retiredIds.Count; i++)
            {
                int id = _retiredIds[i];
                _enemyPool.Release(_enemyViews[id]);
                _enemyViews.Remove(id);
                _enemyRenderers.Remove(id);
            }
        }

        bool HasExplosionFrames => _explosionFrames != null && _explosionFrames.Length > 0;

        /// <summary>프레임 애니메이션 기준 30fps — 9프레임이면 0.3초.</summary>
        float ExplosionLifetime => HasExplosionFrames
            ? _explosionFrames.Length / 30f
            : ExplosionDuration;

        void SpawnExplosion(Vector3 position)
        {
            var fx = _fxPool.Acquire();
            if (fx == null) return;
            fx.localPosition = position;
            var renderer = fx.GetComponent<SpriteRenderer>();
            if (HasExplosionFrames)
            {
                fx.localScale = Vector3.one;
                if (renderer != null)
                {
                    renderer.sprite = _explosionFrames[0];
                    renderer.color = Color.white;
                }
            }
            else
            {
                fx.localScale = Vector3.one * 0.6f;
            }
            _activeFx.Add(fx);
            _activeFxRenderers.Add(renderer);
            _activeFxAges.Add(0f);
        }

        void AnimateExplosions()
        {
            float lifetime = ExplosionLifetime;
            for (int i = _activeFx.Count - 1; i >= 0; i--)
            {
                float age = _activeFxAges[i] + Time.deltaTime;
                if (age >= lifetime)
                {
                    _fxPool.Release(_activeFx[i]);
                    _activeFx.RemoveAt(i);
                    _activeFxRenderers.RemoveAt(i);
                    _activeFxAges.RemoveAt(i);
                    continue;
                }

                _activeFxAges[i] = age;
                float t = age / lifetime;
                var renderer = _activeFxRenderers[i];
                if (HasExplosionFrames)
                {
                    if (renderer != null)
                    {
                        int frame = Mathf.Min(
                            (int)(t * _explosionFrames.Length), _explosionFrames.Length - 1);
                        renderer.sprite = _explosionFrames[frame];
                    }
                    continue;
                }

                _activeFx[i].localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, t);
                if (renderer != null)
                {
                    var c = renderer.color;
                    c.a = 1f - t;
                    renderer.color = c;
                }
            }
        }

        void AnimateDamageFlash()
        {
            int hp = PlayerHp;
            if (_lastHp >= 0 && hp < _lastHp)
                _damageFlashAge = 0f;
            _lastHp = hp;

            if (_damageFlash == null || _damageFlashAge >= DamageFlashDuration) return;

            _damageFlashAge += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - _damageFlashAge / DamageFlashDuration) * 0.35f;
            _damageFlash.color = new Color(1f, 0.2f, 0.2f, alpha);
        }

        void SyncCapsules()
        {
            var capsules = _sim.Capsules;
            _aliveIds.Clear();

            for (int i = 0; i < capsules.Count; i++)
            {
                var capsule = capsules[i];
                _aliveIds.Add(capsule.Id);

                if (!_capsuleViews.TryGetValue(capsule.Id, out var view))
                {
                    view = _capsulePool.Acquire();
                    if (view == null) continue;
                    _capsuleViews.Add(capsule.Id, view);
                }

                view.localPosition = SimView.ToWorld(capsule.X, capsule.Y);
            }

            ReleaseDeadViews(_capsuleViews, _capsulePool);
        }

        /// <summary>
        /// 아이들 애니 프레임 조회. 반환: 평탄 배열의 (시작, 개수). 없으면 count 0.
        /// 순수 표현 — 시간 기반 프레임 순환이라 시뮬 결정론과 무관하다.
        /// </summary>
        void GetAnimRange(string id, out int start, out int count)
        {
            start = 0;
            count = 0;
            if (_animPrefixes == null || _animFrameCounts == null || _animFrames == null) return;
            int bestLength = -1;
            int offset = 0;
            int total = Mathf.Min(_animPrefixes.Length, _animFrameCounts.Length);
            for (int i = 0; i < total; i++)
            {
                if (!string.IsNullOrEmpty(_animPrefixes[i])
                    && id.StartsWith(_animPrefixes[i], System.StringComparison.Ordinal)
                    && _animPrefixes[i].Length > bestLength)
                {
                    bestLength = _animPrefixes[i].Length;
                    start = offset;
                    count = _animFrameCounts[i];
                }
                offset += _animFrameCounts[i];
            }
            if (start + count > _animFrames.Length) count = 0;
        }

        void ApplyIdleAnimation(SpriteRenderer renderer, string id, int desyncSalt)
        {
            if (renderer == null) return;
            GetAnimRange(id, out int start, out int count);
            if (count <= 0) return;
            int frame = ((int)(Time.time * _animFramesPerSecond) + desyncSalt) % count;
            renderer.sprite = _animFrames[start + frame];
        }

        /// <summary>가장 긴 접두어 매칭 — zako_sine_slow가 zako_sine보다 구체적 매칭을 이기게.</summary>
        Sprite SpriteForEnemy(string definitionId)
        {
            if (_enemySpritePrefixes == null || _enemySprites == null) return null;
            Sprite best = null;
            int bestLength = -1;
            int count = Mathf.Min(_enemySpritePrefixes.Length, _enemySprites.Length);
            for (int i = 0; i < count; i++)
            {
                string prefix = _enemySpritePrefixes[i];
                if (string.IsNullOrEmpty(prefix) || _enemySprites[i] == null) continue;
                if (definitionId.StartsWith(prefix, System.StringComparison.Ordinal)
                    && prefix.Length > bestLength)
                {
                    best = _enemySprites[i];
                    bestLength = prefix.Length;
                }
            }
            return best;
        }

        static Color32 TintFor(string definitionId)
        {
            // 결정론적 해시 → 팔레트. 임시 아트 한정: 종류가 눈에만 구분되면 된다.
            int hash = 0;
            for (int i = 0; i < definitionId.Length; i++)
                hash = hash * 31 + definitionId[i];
            return EnemyTints[(hash & 0x7FFFFFFF) % EnemyTints.Length];
        }

        void ReleaseDeadViews(Dictionary<int, Transform> views, SpritePool pool)
        {
            _retiredIds.Clear();
            foreach (var pair in views)
                if (!_aliveIds.Contains(pair.Key))
                    _retiredIds.Add(pair.Key);

            for (int i = 0; i < _retiredIds.Count; i++)
            {
                int id = _retiredIds[i];
                pool.Release(views[id]);
                views.Remove(id);
            }
        }

        bool ValidateWiring()
        {
            string missing = null;
            if (_input == null) missing = nameof(_input);
            else if (_playerTransform == null) missing = nameof(_playerTransform);
            else if (_bulletPrefab == null) missing = nameof(_bulletPrefab);
            else if (_bulletRoot == null) missing = nameof(_bulletRoot);
            else if (_enemyPrefab == null) missing = nameof(_enemyPrefab);
            else if (_enemyRoot == null) missing = nameof(_enemyRoot);
            else if (_capsulePrefab == null) missing = nameof(_capsulePrefab);
            else if (_capsuleRoot == null) missing = nameof(_capsuleRoot);
            else if (_optionPrefab == null) missing = nameof(_optionPrefab);
            else if (_optionRoot == null) missing = nameof(_optionRoot);

            if (missing == null) return true;

            Debug.LogError($"[{nameof(BattleDirector)}] '{missing}' 참조가 비어 있다. " +
                           "Tools → Shmup → Rebuild Battle Scene 으로 씬을 다시 생성해라.", this);
            enabled = false;
            return false;
        }
    }
}
