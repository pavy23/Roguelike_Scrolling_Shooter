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

        [Header("Run")]
        [Tooltip("로그라이크 시드. 같은 시드 + 같은 입력 = 같은 결과 (AGENTS.md §4).")]
        [SerializeField] long _seed = 1;


        RunManager _run;
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
                LoadGameDataText("waves"));

            var config = data.CreateBattleSimConfig();
            // 스키마에 아직 없는 잠정값 (스키마 v3 후보 — GameData로 옮기면 이 블록 제거)
            config.EnemyDespawnX = -14 * SimSpace.SubUnitsPerWorldUnit;
            config.CapsuleHalfWidth = SimSpace.SubUnitsPerWorldUnit * 5 / 16;
            config.CapsuleHalfHeight = SimSpace.SubUnitsPerWorldUnit / 4;
            config.PlayerMaxHp = 3;

            // 런 수명은 Core(RunManager) 소관: 스테이지 전환, 난이도 곡선, 사망 감지,
            // 재시작 시 파워업 승계까지. Presentation은 Step을 돌리고 Battle을 그릴 뿐이다.
            _run = new RunManager(
                (ulong)Seed,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                data.CreatePowerUpGauge());
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
        }

        /// <summary>DevCheats 오버레이용.</summary>
        public int ShieldRemaining => _sim?.ShieldRemaining ?? 0;

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
                        renderer.sprite = bullet.Kind == BulletKind.Missile && _missileSprite != null
                            ? _missileSprite : _mainShotSprite;
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
                        renderer.color = TintFor(enemy.DefinitionId);
                }

                view.localPosition = SimView.ToWorld(enemy.X, enemy.Y);
            }

            ReleaseDeadEnemies();
        }

        /// <summary>
        /// 적 뷰 반납 + 폭발 이펙트. 마지막 위치가 화면 안이면 처치로 보고 터뜨린다
        /// (왼쪽 밖 despawn과 구분하는 표현 계층 휴리스틱 — 게임 판정이 아니다).
        /// </summary>
        void ReleaseDeadEnemies()
        {
            const float despawnEdgeX = -12.5f;

            _retiredIds.Clear();
            foreach (var pair in _enemyViews)
                if (!_aliveIds.Contains(pair.Key))
                    _retiredIds.Add(pair.Key);

            for (int i = 0; i < _retiredIds.Count; i++)
            {
                int id = _retiredIds[i];
                var view = _enemyViews[id];
                if (view.localPosition.x > despawnEdgeX)
                    SpawnExplosion(view.localPosition);
                _enemyPool.Release(view);
                _enemyViews.Remove(id);
                _enemyRenderers.Remove(id);
            }
        }

        void SpawnExplosion(Vector3 position)
        {
            var fx = _fxPool.Acquire();
            if (fx == null) return;
            fx.localPosition = position;
            fx.localScale = Vector3.one * 0.6f;
            _activeFx.Add(fx);
            _activeFxRenderers.Add(fx.GetComponent<SpriteRenderer>());
            _activeFxAges.Add(0f);
        }

        void AnimateExplosions()
        {
            for (int i = _activeFx.Count - 1; i >= 0; i--)
            {
                float age = _activeFxAges[i] + Time.deltaTime;
                if (age >= ExplosionDuration)
                {
                    _fxPool.Release(_activeFx[i]);
                    _activeFx.RemoveAt(i);
                    _activeFxRenderers.RemoveAt(i);
                    _activeFxAges.RemoveAt(i);
                    continue;
                }

                _activeFxAges[i] = age;
                float t = age / ExplosionDuration;
                _activeFx[i].localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, t);
                var renderer = _activeFxRenderers[i];
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
