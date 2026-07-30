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
        /// <summary>MissileFamily 순서(Straight/SpreadBomb/PiercingLance)와 정렬.</summary>
        [SerializeField] Sprite[] _missileFamilySprites;
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
        readonly Dictionary<int, Color> _enemyDeathTints = new Dictionary<int, Color>(32);   // 테마별 폭발 틴트
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

        // 함선별 스프라이트 (밸런스/스피드/탱커) — 선택 함선에 맞는 기체 비주얼 적용
        [SerializeField] string[] _shipSpriteIds;
        [SerializeField] Sprite[] _shipSprites;
        // 장애물 (REQ-023): 테마×계열 스프라이트, _themeIds와 인덱스 정렬
        [SerializeField] Transform _obstacleRoot;
        [SerializeField] GameObject _obstaclePrefab;
        [SerializeField] Sprite[] _obstacleSolidSprites;
        [SerializeField] Sprite[] _obstacleBreakableSprites;
        SpritePool _obstaclePool;
        readonly Dictionary<int, Transform> _obstacleViews = new Dictionary<int, Transform>(32);

        // 무기 계열별 주무기 탄 스프라이트 (REQ-022): laser/spread가 없으면 vulcan 폴백
        [SerializeField] Sprite _laserShotSprite;
        [SerializeField] Sprite _spreadShotSprite;

        void ApplyWeaponBulletSprite(Shmup.Core.WeaponType weaponType)
        {
            if (weaponType == Shmup.Core.WeaponType.Laser && _laserShotSprite != null)
                _mainShotSprite = _laserShotSprite;
            else if (weaponType == Shmup.Core.WeaponType.Spread && _spreadShotSprite != null)
                _mainShotSprite = _spreadShotSprite;
            // Vulcan은 프리팹 원본(_mainShotSprite 초기값) 유지
        }

        void ApplyShipSprite(string shipId)
        {
            if (_playerTransform == null || _shipSpriteIds == null || _shipSprites == null) return;
            var renderer = _playerTransform.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            int count = Mathf.Min(_shipSpriteIds.Length, _shipSprites.Length);
            for (int i = 0; i < count; i++)
            {
                if (!string.Equals(_shipSpriteIds[i], shipId, System.StringComparison.Ordinal)) continue;
                if (_shipSprites[i] == null) return;
                renderer.sprite = _shipSprites[i];
                // 엔진 프레임 애니는 starter 전용 아트 — 다른 함선은 정지 스프라이트 유지
                var animator = _playerTransform.GetComponent<PlayerShipAnimator>();
                if (animator != null) animator.enabled = i == 0;
                return;
            }
        }

        // 주스 연출 (REQ 없음 — 순수 표현, 시뮬 비관여)
        [SerializeField] JuiceDirector _juice;
        [SerializeField] SpriteRenderer _muzzleFlash;
        readonly List<Transform> _punchTargets = new List<Transform>(16);
        readonly List<SpriteRenderer> _punchRenderers = new List<SpriteRenderer>(16);
        readonly List<Vector3> _punchBaseScales = new List<Vector3>(16);
        readonly List<Color> _punchBaseColors = new List<Color>(16);
        readonly List<float> _punchAges = new List<float>(16);
        const float PunchDuration = 0.09f;
        static readonly Color PunchTint = new Color(1f, 0.45f, 0.45f);
        readonly List<Vector3> _pendingBoomPositions = new List<Vector3>(8);
        readonly List<float> _pendingBoomDelays = new List<float>(8);
        float _muzzleAge = float.MaxValue;
        const float MuzzleDuration = 0.06f;

        /// <summary>현재 콤보 배율 (MultiplierChanged 이벤트 추적, HUD 표시용).</summary>
        public int ScoreMultiplier { get; private set; } = 1;

        /// <summary>이번 런에서 도달한 최고 배율 (게임오버 요약용).</summary>
        public int BestMultiplier { get; private set; } = 1;

        /// <summary>런 지속 모디파이어 (게임오버 요약용).</summary>
        public BattleModifier ActiveModifiers => _run != null ? _run.ActiveModifiers : BattleModifier.None;

        [SerializeField] ScorePopups _scorePopups;
        [SerializeField] SpawnTelegraph _spawnTelegraph;
        int _lastBossHp = -1;
        float _bossFlashAge = float.MaxValue;
        const float BossFlashDuration = 0.09f;

        // 경로 선택 (REQ-028): RouteScreen이 읽고 고르는 얇은 어댑터
        public bool AwaitingRoute => _run != null && _run.State == RunState.AwaitingRoute;
        public IReadOnlyList<RouteOption> RouteOptions => _run != null ? _run.RouteOptions : null;

        public void ChooseRoute(int index)
        {
            if (!AwaitingRoute) return;
            if (_replayMode) return;              // 리플레이는 기록된 경로를 자동 재현
            if (_recordingActive) _recordedRoutes.Add(index);
            _run.ChooseRoute(index);
            RefreshBattle();
            SyncViews();
        }

        readonly List<int> _recordedRoutes = new List<int>(8);
        int _replayRouteCursor;

        /// <summary>보스전 진행 중 여부 (BgmPlayer 보스 트랙 전환용).</summary>
        public bool BossActive => _sim != null && _sim.BossActive;

        /// <summary>런 완주(최종 보스 격파) 여부 — 결과 화면이 승리/패배를 가른다 (REQ-031).</summary>
        public bool IsRunCleared => _run != null && _run.State == RunState.RunCleared;

        /// <summary>초대형 보스 파츠 상태 (REQ-035) — BossPartsView가 읽는다.</summary>
        public IReadOnlyList<BossPartState> BossParts =>
            _sim != null ? _sim.BossParts : null;

        /// <summary>플레이어 기체의 월드 좌표 (터치 드래그 조작이 목표 방향을 계산할 때 쓴다).</summary>
        public Vector2 PlayerWorldPosition =>
            _sim != null ? (Vector2)SimView.ToWorld(_sim.PlayerX, _sim.PlayerY) : Vector2.zero;

        /// <summary>보스 본체의 월드 좌표 (파츠 오버레이 기준점).</summary>
        public Vector3 BossWorldPosition =>
            _sim != null ? SimView.ToWorld(_sim.Boss.X, _sim.Boss.Y) : Vector3.zero;

        // 바이옴/룸 진행도 (REQ-032) — 22분 런에서 현재 위치를 알려 준다
        public int BiomeIndex => _run?.BiomeIndex ?? 0;
        public int RoomIndex => _run?.RoomIndex ?? 0;
        public int BiomeCount => _run?.BiomeCount ?? 0;
        public int RoomsPerBiome => _run?.RoomsPerBiome ?? 0;

        /// <summary>런이 끝났는가 (사망 또는 완주).</summary>
        public bool IsRunFinished => _run != null && _run.IsFinished;

        /// <summary>타이틀 CONTINUE가 채우는 이어하기 데이터 — Awake에서 1회 소비.</summary>
        public static Shmup.Core.Simulation.RunSuspendData PendingResume;

        /// <summary>타이틀 REPLAY가 채우는 재생 데이터 — Awake에서 1회 소비.</summary>
        public static ReplayFileData PendingReplay;

        // 리플레이/녹화 (REQ-018/019). 기록은 Playing 틱의 명령만 담는다 — 보상 대기 길이가
        // 달라도 스트림이 어긋나지 않는다. 보상 선택은 별도 목록(rewardChoices)으로 재현.
        InputRecorder _recorder;
        bool _recordingActive;
        readonly List<int> _recordedChoices = new List<int>(8);
        bool _replayMode;
        InputPlayback.Enumerator _playback;
        int _replayChoiceCursor;
        bool _replayStreamEnded;
        float _replayEndTimer = 3f;
        string _recordShipId;

        public bool ReplayMode => _replayMode;

        /// <summary>현재 런을 스테이지 경계 스냅샷으로 저장 (Playing 상태에서만 유효).</summary>
        public void SaveRunToDisk()
        {
            if (_run == null || _run.State != RunState.Playing) return;
            RunSave.Save(_run.ExportSuspendData());
        }

        void OnApplicationQuit()
        {
            if (_run != null && !IsRunOver && !_replayMode)
                SaveRunToDisk();
        }

        // Step이 RunOver/AwaitingReward에서 no-op이면 EventsThisTick이 클리어되지 않는다 —
        // 같은 이벤트를 매 FixedUpdate 재소비하지 않도록 (배틀 인스턴스, 틱)으로 신선도 판정.
        IBattleSim _lastEventSim;
        int _lastEventTick = -1;

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
        /// <summary>재출격 시 요약 지표 초기화 (RestartRun 경유).</summary>
        void ResetRunSummary()
        {
            BestMultiplier = 1;
            ScoreMultiplier = 1;
        }

        public void RestartRun()
        {
            // 완주(RunCleared) 후에도 새 런을 시작할 수 있어야 한다 (REQ-031)
            if (_run == null || !_run.IsFinished) return;
            ulong newSeed = (uint)System.Environment.TickCount ^ ((ulong)(uint)_run.RunNumber << 32);
            _run.Restart(newSeed);
            Seed = (long)newSeed;
            ResetRunSummary();
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
                TryLoadGameDataText("ships"),      // 없으면 기본 함선 1척
                TryLoadGameDataText("scoring"));   // 없으면 Core 기본 그레이즈/콤보 수치 (REQ-016)

            // 격납고 선택 함선 (저장이 채널 — Title 씬 HangarScreen이 기록)
            _meta = MetaSave.Load(data);
            var selectedShip = data.FindShip(_meta.SelectedShipId) ?? data.DefaultShip;

            // 리플레이 모드 (REQ-018/019): 기록 당시의 함선·시드로 재현. 이어하기보다 우선.
            var pendingReplay = PendingReplay;
            PendingReplay = null;
            if (pendingReplay != null)
            {
                _replayMode = true;
                selectedShip = data.FindShip(pendingReplay.shipId) ?? selectedShip;
                _playback = new InputPlayback(pendingReplay.recording).GetEnumerator();
                _recordedChoices.Clear();
                if (pendingReplay.rewardChoices != null)
                    _recordedChoices.AddRange(pendingReplay.rewardChoices);
                _recordedRoutes.Clear();
                if (pendingReplay.routeChoices != null)
                    _recordedRoutes.AddRange(pendingReplay.routeChoices);
            }

            var config = data.CreateBattleSimConfig();
            // 스키마에 아직 없는 잠정값 (스키마 v3 후보 — GameData로 옮기면 이 블록 제거)
            // EnemyDespawnX는 REQ-005 이후 Core 기본값(-22u, SimSpace 상수 파생)을 그대로 쓴다.
            // 캡슐 히트박스는 새 16×14px 스프라이트 기준 ×1.5.
            config.CapsuleHalfWidth = SimSpace.SubUnitsPerWorldUnit * 15 / 32;
            config.CapsuleHalfHeight = SimSpace.SubUnitsPerWorldUnit * 3 / 8;
            config.PlayerMaxHp = 3;

            // 런 수명은 Core(RunManager) 소관: 스테이지 전환, 난이도 곡선, 사망 감지,
            // 재시작 시 파워업 승계까지. Presentation은 Step을 돌리고 Battle을 그릴 뿐이다.
            // 이어하기(REQ-017): 타이틀이 PendingResume을 채우면 새 런 대신 리줌한다.
            var pending = PendingResume;
            PendingResume = null;
            if (_replayMode) pending = null;   // 리플레이는 항상 새 런 재현
            if (pending != null)
            {
                try
                {
                    var resumeShip = data.FindShip(pending.shipId) ?? selectedShip;
                    _run = RunManager.ResumeFromSuspendData(
                        pending,
                        new SegmentStageGenerator(data.StageGeneration),
                        config,
                        data.BattleContent,
                        data.CreatePowerUpGauge(),
                        data.Rewards,
                        resumeShip);
                }
                catch (System.Exception e)
                {
                    // 저장 파일은 남겨 둔다 — 다음 실행에서 다시 시도할 수 있게
                    Debug.LogWarning($"[BattleDirector] 이어하기 실패({e.GetType().Name}) — 새 런으로 시작. {e.Message}");
                    _run = null;
                }
                if (_run != null)
                    RunSave.Delete();   // 복원 성공 후에만 소비 (심사 지적 반영)
            }
            if (_run == null)
            {
                // 난이도 배율 (REQ-020): 새 런은 타이틀 선택, 리플레이는 기록 당시 값
                int diffNum, diffDen;
                if (_replayMode && pendingReplay != null)
                {
                    diffNum = Mathf.Max(1, pendingReplay.difficultyNumerator);
                    diffDen = Mathf.Max(1, pendingReplay.difficultyDenominator);
                }
                else
                {
                    DifficultySelect.GetMultiplier(out diffNum, out diffDen);
                }
                _run = new RunManager(
                    (ulong)Seed,
                    new SegmentStageGenerator(data.StageGeneration),
                    config,
                    data.BattleContent,
                    data.CreatePowerUpGauge(),
                    data.Rewards,
                    selectedShip,
                    diffNum,
                    diffDen);
            }
            _sim = _run.Battle;

            ApplyShipSprite(selectedShip != null ? selectedShip.Id : null);
            if (_sfx != null && selectedShip != null)
                _sfx.WeaponFamily = selectedShip.WeaponType;   // 계열별 발사음

            // 라이브 신규 런만 녹화한다 (리플레이/이어하기 런은 제외 — 첫 목숨 기준)
            if (!_replayMode && pending == null)
            {
                _recorder = new InputRecorder();
                _recordingActive = true;
                _recordedChoices.Clear();
                _recordedRoutes.Clear();
                _recordShipId = selectedShip != null ? selectedShip.Id : null;
            }

            // 풀 용량은 Core가 허용하는 최대 개수와 맞춘다 — 런타임에 풀이 부족해질 수 없다.
            // 시뮬 Bullets는 플레이어탄+적탄 합산 리스트 — 풀도 합산 용량이어야 한다 (GROK 스트레스 검증 후속)
            _bulletPool = new SpritePool(
                _bulletPrefab, _bulletRoot, config.MaxBullets + config.MaxEnemyBullets, "Bullet");
            _enemyPool = new SpritePool(_enemyPrefab, _enemyRoot, 32, "Enemy");
            _capsulePool = new SpritePool(_capsulePrefab, _capsuleRoot, 16, "Capsule");
            _fxPool = new SpritePool(_explosionPrefab, _fxRoot, 16, "Explosion");
            _optionPool = new SpritePool(_optionPrefab, _optionRoot, 4, "Option");
            if (_obstaclePrefab != null && _obstacleRoot != null)
                _obstaclePool = new SpritePool(_obstaclePrefab, _obstacleRoot, config.MaxObstacles, "Obstacle");

            var bulletPrefabRenderer = _bulletPrefab.GetComponent<SpriteRenderer>();
            _mainShotSprite = bulletPrefabRenderer != null ? bulletPrefabRenderer.sprite : null;
            if (selectedShip != null)
                ApplyWeaponBulletSprite(selectedShip.WeaponType);

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
            AnimatePunches();
            AnimateMuzzleFlash();
            TickPendingBooms();

            // 리플레이 종료 → 잠시 후 타이틀 복귀
            if (_replayMode && (IsRunOver || _replayStreamEnded))
            {
                _replayEndTimer -= Time.unscaledDeltaTime;
                if (_replayEndTimer <= 0f)
                {
                    _replayMode = false;
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
                }
            }
        }

        void FixedUpdate()
        {
            if (_run == null) return;

            bool playingBefore = _run.State == RunState.Playing;
            InputCommand command;
            if (_replayMode)
            {
                if (playingBefore)
                {
                    if (_playback.MoveNext()) command = _playback.Current;
                    else { command = InputCommand.None; _replayStreamEnded = true; }
                }
                else
                {
                    command = InputCommand.None;
                    // 보상 대기: 기록된 선택을 즉시 재현 (기록도 Playing 틱만 담아 정렬 유지)
                    if (_run.State == RunState.AwaitingReward)
                    {
                        int choice = _replayChoiceCursor < _recordedChoices.Count
                            ? _recordedChoices[_replayChoiceCursor++] : 0;
                        _run.ChooseReward(choice);
                    }
                    else if (_run.State == RunState.AwaitingRoute)
                    {
                        int route = _replayRouteCursor < _recordedRoutes.Count
                            ? _recordedRoutes[_replayRouteCursor++] : 0;
                        _run.ChooseRoute(route);
                    }
                }
            }
            else
            {
                command = _input.ConsumeCommand();
            }
            if (_recordingActive && playingBefore)
                _recorder.Record(in command);
            _run.Step(command);

            // 런 종료(사망 또는 완주) 시 점수를 메타 재화로 1회 적립. 리플레이는 비적립.
            if (_run.IsFinished
                && !_replayMode
                && _meta != null
                && _run.RunNumber != _lastCreditedRunNumber)
            {
                _lastCreditedRunNumber = _run.RunNumber;
                _meta.CreditScore(_run.TotalScore);
                MetaSave.Save(_meta);
                RunSave.Delete();   // 런 종료 — 이어하기 무효화

                // 첫 목숨 녹화 종료 → 마지막 런 리플레이 저장
                if (_recordingActive)
                {
                    _recordingActive = false;
                    ReplaySave.Save(new ReplayFileData
                    {
                        seed = Seed,
                        shipId = _recordShipId,
                        finalScore = _run.TotalScore,
                        difficultyNumerator = _run.DifficultyMultiplierNumerator,
                        difficultyDenominator = _run.DifficultyMultiplierDenominator,
                        rewardChoices = _recordedChoices.ToArray(),
                        routeChoices = _recordedRoutes.ToArray(),
                        recording = _recorder.Export()
                    });
                }
            }

            // 이벤트는 스텝 직후 같은 호출 안에서 소비한다 — 다음 Step에서 클리어되기 때문.
            var battle = _run.Battle;
            bool freshEvents = !ReferenceEquals(battle, _lastEventSim) || battle.Tick != _lastEventTick;
            _lastEventSim = battle;
            _lastEventTick = battle.Tick;
            var events = freshEvents ? battle.EventsThisTick : System.ReadOnlySpan<SimEvent>.Empty;
            if (_sfx != null)
                _sfx.PlayEvents(events);
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case SimEventType.EnemyHit:
                        PunchEnemy(e.EntityId);
                        break;
                    case SimEventType.EnemyKilled:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y), 1f,
                            _enemyDeathTints.TryGetValue(e.EntityId, out var deathTint)
                                ? deathTint : Color.white);
                        if (_scorePopups != null)
                            _scorePopups.Spawn(SimView.ToWorld(e.X, e.Y), e.Arg);   // Arg = 부여 점수 (REQ-024)
                        break;
                    case SimEventType.PlayerKilled:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y));
                        if (_juice != null)
                        {
                            _juice.Shake(0.45f);
                            _juice.Slowmo(0.35f, 0.6f);
                        }
                        break;
                    case SimEventType.PlayerHit:
                        // 런 종료 후에도 피격 이벤트가 이어질 수 있다 — 영구 히트스톱 방지
                        if (_juice != null && !IsRunOver)
                        {
                            _juice.Shake(0.28f);
                            _juice.Hitstop(0.06f);
                        }
                        break;
                    case SimEventType.BossSpawned:
                        if (_bossIntro != null) _bossIntro.Trigger();
                        if (_juice != null) _juice.Shake(0.3f);
                        break;
                    case SimEventType.BossPhaseChanged:
                        if (_juice != null)
                        {
                            _juice.Shake(0.22f);
                            _juice.Hitstop(0.07f);
                        }
                        break;
                    case SimEventType.StageCleared:
                        TriggerBossDeathSequence();
                        break;
                    case SimEventType.PlayerFired:
                        _muzzleAge = 0f;
                        break;
                    case SimEventType.BulletRicocheted:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y), 0.45f);   // 도탄 스파크 (소형)
                        break;
                    case SimEventType.GrazeScored:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y), 0.3f);    // 그레이즈 스파크
                        break;
                    case SimEventType.MultiplierChanged:
                        ScoreMultiplier = e.Arg;
                        if (e.Arg > BestMultiplier) BestMultiplier = e.Arg;
                        break;
                    case SimEventType.KillExplosionTriggered:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y), 1.4f);    // 광역 폭발 (대형)
                        if (_juice != null) _juice.Shake(0.1f);
                        break;
                    case SimEventType.ObstacleDestroyed:
                        SpawnExplosion(SimView.ToWorld(e.X, e.Y), 0.9f);
                        if (_scorePopups != null)
                            _scorePopups.Spawn(SimView.ToWorld(e.X, e.Y), e.Arg);
                        break;
                }
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
            if (_obstaclePool != null) ReleaseAll(_obstacleViews, _obstaclePool);
            _enemyRenderers.Clear();
            _enemyDeathTints.Clear();
            _lastHp = -1;   // 배틀 교체 직후 HP 차이를 피격 플래시로 오인하지 않게

            _sim = battle;
            ScoreMultiplier = 1;   // 새 배틀 인스턴스 — 배율 표시 초기화
            _lastBossHp = -1;      // 보스 피격 플래시 오인 방지
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
            SyncObstacles();
            SyncShield();
            SyncBoss();
        }

        /// <summary>장애물 뷰 동기화 (REQ-023). 테마×계열로 스프라이트를 고른다.</summary>
        void SyncObstacles()
        {
            if (_obstaclePool == null) return;
            var obstacles = _sim.Obstacles;
            _aliveIds.Clear();
            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                _aliveIds.Add(obstacle.Id);
                if (!_obstacleViews.TryGetValue(obstacle.Id, out var view))
                {
                    view = _obstaclePool.Acquire();
                    if (view == null) continue;
                    _obstacleViews.Add(obstacle.Id, view);
                    var renderer = view.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        var sprite = SpriteForObstacle(obstacle.Type);
                        if (sprite != null) renderer.sprite = sprite;
                        renderer.color = Color.white;
                    }
                }
                view.localPosition = SimView.ToWorld(obstacle.X, obstacle.Y);
            }

            _retiredIds.Clear();
            foreach (var pair in _obstacleViews)
                if (!_aliveIds.Contains(pair.Key))
                    _retiredIds.Add(pair.Key);
            for (int i = 0; i < _retiredIds.Count; i++)
            {
                int id = _retiredIds[i];
                _obstaclePool.Release(_obstacleViews[id]);
                _obstacleViews.Remove(id);
            }
        }

        Sprite SpriteForObstacle(ObstacleType type)
        {
            bool solid = type == ObstacleType.Solid;
            string themeId = CurrentThemeId;
            if (!string.IsNullOrEmpty(themeId) && _themeIds != null)
            {
                int count = Mathf.Min(_themeIds.Length,
                    Mathf.Min(_obstacleSolidSprites?.Length ?? 0, _obstacleBreakableSprites?.Length ?? 0));
                for (int i = 0; i < count; i++)
                    if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                        return solid ? _obstacleSolidSprites[i] : _obstacleBreakableSprites[i];
            }
            return solid
                ? (_obstacleSolidSprites != null && _obstacleSolidSprites.Length > 0 ? _obstacleSolidSprites[0] : null)
                : (_obstacleBreakableSprites != null && _obstacleBreakableSprites.Length > 0 ? _obstacleBreakableSprites[0] : null);
        }

        Sprite SpriteForBulletKind(BulletKind kind)
        {
            // 미사일은 계열별 스프라이트 (REQ-034): 직진 추진체 / 투하 폭탄 / 관통 창
            if (kind == BulletKind.Missile)
            {
                var family = _run != null ? _run.CurrentMissileFamily : MissileFamily.Straight;
                var typed = SpriteForMissileFamily(family);
                if (typed != null) return typed;
                if (_missileSprite != null) return _missileSprite;
            }
            if (kind == BulletKind.EnemyShot && _enemyShotSprite != null) return _enemyShotSprite;
            return _mainShotSprite;
        }

        Sprite SpriteForMissileFamily(MissileFamily family)
        {
            if (_missileFamilySprites == null) return null;
            int index = (int)family;
            return index >= 0 && index < _missileFamilySprites.Length
                ? _missileFamilySprites[index] : null;
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

            // 보스 피격 플래시 + 빈사 맥동 (HP 바 외에 시각 피드백이 없던 문제)
            int bossHp = _sim.Boss.Hp;
            if (_lastBossHp >= 0 && bossHp < _lastBossHp)
                _bossFlashAge = 0f;
            _lastBossHp = bossHp;

            var bossColor = Color.white;
            if (_bossFlashAge < BossFlashDuration)
            {
                _bossFlashAge += Time.deltaTime;
                float t = Mathf.Clamp01(_bossFlashAge / BossFlashDuration);
                bossColor = Color.Lerp(new Color(1f, 0.55f, 0.55f), Color.white, t);
            }
            else if (_sim.Boss.MaxHp > 0 && bossHp * 4 <= _sim.Boss.MaxHp)
            {
                // 빈사(25% 이하): 붉은 맥동 — 접근성 플래시 감소 시 완화
                float amplitude = _juice != null && _juice.FlashReduced ? 0.12f : 0.3f;
                float pulse = (Mathf.Sin(Time.time * 7f) + 1f) * 0.5f * amplitude;
                bossColor = new Color(1f, 1f - pulse, 1f - pulse);
            }
            if (_bossRenderer.color != bossColor)
                _bossRenderer.color = bossColor;

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
            if (_replayMode) return;   // 리플레이 중 수동 선택 금지 (자동 재현)
            if (_recordingActive) _recordedChoices.Add(index);
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

        /// <summary>
        /// 스톡 단계별 실드 색. 쌓일수록 차가운 청색에서 뜨거운 쪽으로 옮겨가서,
        /// 숫자를 읽지 않아도 남은 양이 보인다.
        /// </summary>
        static readonly Color[] ShieldTierColors =
        {
            new Color(0.35f, 0.70f, 1.00f, 0.55f),   // 1: 청색
            new Color(0.35f, 1.00f, 0.85f, 0.62f),   // 2: 청록
            new Color(0.70f, 1.00f, 0.55f, 0.70f),   // 3: 연녹
            new Color(1.00f, 0.90f, 0.45f, 0.78f),   // 4: 금색
            new Color(1.00f, 0.55f, 0.95f, 0.86f)    // 5+: 자홍 (현재 상한)
        };

        void SyncShield()
        {
            if (_shieldView == null) return;
            int remaining = _sim.ShieldRemaining;
            _shieldView.enabled = remaining > 0;
            if (remaining <= 0) return;

            // 실드는 기체 모양이어야 와닿는다 — 원형 링은 가로로 긴 함선과 겹치지 않아
            // "내가 감싸여 있다"는 느낌을 주지 못했다. 기체 스프라이트를 그대로 빌려
            // 기체 뒤에 조금 크게 깔면 외곽이 테두리처럼 보인다. 스프라이트를 매 프레임
            // 따라가므로 함선 교체와 애니메이션 프레임에 자동으로 맞는다.
            var ship = _playerTransform != null
                ? _playerTransform.GetComponent<SpriteRenderer>() : null;
            if (ship != null)
            {
                if (!ReferenceEquals(_shieldView.sprite, ship.sprite))
                    _shieldView.sprite = ship.sprite;
                _shieldView.sortingOrder = ship.sortingOrder - 1;
                _shieldView.flipX = ship.flipX;
            }

            int tier = Mathf.Clamp(remaining, 1, ShieldTierColors.Length);
            // 스톡이 쌓이면 테두리가 두꺼워진다 (기체와의 크기 차이가 곧 두께다).
            // 상한(5)에서도 기체 실루엣을 알아볼 수 있는 범위로 계수를 잡았다.
            float thickness = 1f + 0.08f * tier;
            _shieldView.transform.localScale = new Vector3(thickness, thickness, 1f);
            _shieldView.color = ShieldTierColors[tier - 1];
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
                    _enemyDeathTints[enemy.Id] = DeathTintFor(enemy.DefinitionId);
                    // 화면 밖에서 스폰된 적은 등장 예고 마커를 띄운다
                    if (_spawnTelegraph != null
                        && enemy.X > SimSpace.PlayfieldHalfWidthSubUnits)
                        _spawnTelegraph.Warn(SimView.ToWorld(enemy.X, enemy.Y).y);
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
                _enemyDeathTints.Remove(id);
            }
        }

        bool HasExplosionFrames => _explosionFrames != null && _explosionFrames.Length > 0;

        /// <summary>프레임 애니메이션 기준 30fps — 9프레임이면 0.3초.</summary>
        float ExplosionLifetime => HasExplosionFrames
            ? _explosionFrames.Length / 30f
            : ExplosionDuration;

        // 테마 계열별 폭발 틴트 — 기계는 흰색(원색), 유기체는 녹황, 스크랩은 주황, 에너지는 청보라
        static Color DeathTintFor(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) return Color.white;
            if (definitionId.StartsWith("spore", System.StringComparison.Ordinal)
                || definitionId.StartsWith("brood_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("sting_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("lancer", System.StringComparison.Ordinal)
                || definitionId.StartsWith("mini_horror", System.StringComparison.Ordinal))
                return new Color(0.75f, 1f, 0.6f);
            if (definitionId.StartsWith("scrap_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("rust_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("junk_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("pipe_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("zako_tank", System.StringComparison.Ordinal))
                return new Color(1f, 0.8f, 0.55f);
            if (definitionId.StartsWith("wisp", System.StringComparison.Ordinal)
                || definitionId.StartsWith("echo_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("void_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("shard_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("phase_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("rift_", System.StringComparison.Ordinal)
                || definitionId.StartsWith("mini_crystal", System.StringComparison.Ordinal))
                return new Color(0.72f, 0.85f, 1f);
            return Color.white;
        }

        void SpawnExplosion(Vector3 position, float scale = 1f)
        {
            SpawnExplosion(position, scale, Color.white);
        }

        void SpawnExplosion(Vector3 position, float scale, Color tint)
        {
            var fx = _fxPool.Acquire();
            if (fx == null) return;
            fx.localPosition = position;
            var renderer = fx.GetComponent<SpriteRenderer>();
            if (HasExplosionFrames)
            {
                fx.localScale = Vector3.one * scale;
                if (renderer != null)
                {
                    renderer.sprite = _explosionFrames[0];
                    renderer.color = tint;
                }
            }
            else
            {
                fx.localScale = Vector3.one * (0.6f * scale);
            }
            _activeFx.Add(fx);
            _activeFxRenderers.Add(renderer);
            _activeFxAges.Add(0f);
        }

        /// <summary>적 피격 펀치: 짧은 스케일 팝 + 붉은 틴트. 원 스케일은 시작 시점 값을 복원한다.</summary>
        void PunchEnemy(int enemyId)
        {
            if (!_enemyViews.TryGetValue(enemyId, out var view) || view == null) return;

            // 이미 펀치 중이면 age만 리셋 (베이스 스케일 중복 캡처 방지)
            for (int i = 0; i < _punchTargets.Count; i++)
                if (ReferenceEquals(_punchTargets[i], view))
                {
                    _punchAges[i] = 0f;
                    return;
                }

            _enemyRenderers.TryGetValue(enemyId, out var renderer);
            _punchTargets.Add(view);
            _punchRenderers.Add(renderer);
            _punchBaseScales.Add(view.localScale);
            _punchBaseColors.Add(renderer != null ? renderer.color : Color.white);
            _punchAges.Add(0f);
        }

        void AnimatePunches()
        {
            bool tint = _juice == null || !_juice.FlashReduced;
            for (int i = _punchTargets.Count - 1; i >= 0; i--)
            {
                var view = _punchTargets[i];
                float age = _punchAges[i] + Time.deltaTime;
                bool alive = view != null && view.gameObject.activeSelf;
                if (age >= PunchDuration || !alive)
                {
                    if (alive)
                    {
                        view.localScale = _punchBaseScales[i];
                        if (_punchRenderers[i] != null) _punchRenderers[i].color = _punchBaseColors[i];
                    }
                    _punchTargets.RemoveAt(i);
                    _punchRenderers.RemoveAt(i);
                    _punchBaseScales.RemoveAt(i);
                    _punchBaseColors.RemoveAt(i);
                    _punchAges.RemoveAt(i);
                    continue;
                }
                _punchAges[i] = age;
                float t = age / PunchDuration;
                float scale = 1f + 0.14f * Mathf.Sin(t * Mathf.PI);   // 팝 후 복원
                view.localScale = _punchBaseScales[i] * scale;
                if (tint && _punchRenderers[i] != null)
                    _punchRenderers[i].color = Color.Lerp(PunchTint, _punchBaseColors[i], t);
            }
        }

        void AnimateMuzzleFlash()
        {
            if (_muzzleFlash == null) return;
            if (_muzzleAge >= MuzzleDuration)
            {
                if (_muzzleFlash.enabled) _muzzleFlash.enabled = false;
                return;
            }
            _muzzleAge += Time.deltaTime;
            _muzzleFlash.enabled = true;
        }

        /// <summary>
        /// 보스 격파 시퀀스: 보스 위치 주변 다단 폭발 + 슬로모 + 흔들림.
        /// StageCleared는 보스 격파 직후 발생 — 보스 뷰가 켜져 있을 때만 연출한다.
        /// </summary>
        void TriggerBossDeathSequence()
        {
            if (_bossRenderer == null || !_bossRenderer.enabled) return;
            var center = _bossRenderer.transform.localPosition;
            if (_juice != null)
            {
                _juice.Shake(0.5f);
                _juice.Slowmo(0.35f, 0.7f);
            }
            SpawnExplosion(center);
            for (int i = 0; i < 6; i++)
            {
                var offset = new Vector3(
                    Mathf.Cos(i * 2.4f) * (0.6f + 0.25f * i),
                    Mathf.Sin(i * 1.9f) * (0.5f + 0.2f * i),
                    0f);
                _pendingBoomPositions.Add(center + offset);
                _pendingBoomDelays.Add(0.12f * (i + 1));
            }
        }

        void TickPendingBooms()
        {
            for (int i = _pendingBoomPositions.Count - 1; i >= 0; i--)
            {
                float delay = _pendingBoomDelays[i] - Time.deltaTime;
                if (delay <= 0f)
                {
                    SpawnExplosion(_pendingBoomPositions[i]);
                    _pendingBoomPositions.RemoveAt(i);
                    _pendingBoomDelays.RemoveAt(i);
                }
                else
                {
                    _pendingBoomDelays[i] = delay;
                }
            }
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
            float intensity = _juice != null && _juice.FlashReduced ? 0.15f : 0.35f;
            float alpha = Mathf.Clamp01(1f - _damageFlashAge / DamageFlashDuration) * intensity;
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
