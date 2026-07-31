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
        [SerializeField] GameObject _bombPickupPrefab;
        [SerializeField] Transform _bombPickupRoot;
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
        SpritePool _bombPickupPool;

        // Id → 뷰 인스턴스. Core가 주는 Id는 스폰~소멸까지 불변이라 매칭 키로 쓸 수 있다.
        readonly Dictionary<int, Transform> _bulletViews = new Dictionary<int, Transform>(64);
        readonly Dictionary<int, Transform> _enemyViews = new Dictionary<int, Transform>(32);
        readonly Dictionary<int, Transform> _capsuleViews = new Dictionary<int, Transform>(16);
        readonly Dictionary<int, Transform> _bombPickupViews = new Dictionary<int, Transform>(16);
        readonly Dictionary<int, float> _obstacleFadeAges = new Dictionary<int, float>(32);
        const float ObstacleFadeSeconds = 0.35f;

        // 피격 플래시 (REQ-082 C): "데미지를 주고 있다는 표시가 안 난다" — 비치명
        // ObstacleDamaged마다 앰버로 번쩍여 탄이 먹히고 있음을 보여 준다.
        readonly Dictionary<int, float> _obstacleHitFlashes = new Dictionary<int, float>(32);
        const float ObstacleHitFlashSeconds = 0.12f;
        static readonly Color ObstacleHitFlashColor = new Color(1f, 0.72f, 0.25f, 1f);
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

        /// <summary>스테이지 기믹 시각화 (REQ-055) — 통로 벽 접촉 번쩍임에 필요하다.</summary>
        [SerializeField] StageGimmickView _gimmickView;
        int _lastBossHp = -1;
        float _bossFlashAge = float.MaxValue;

        // 페이즈 전환 발광 (REQ-054). 남은 시간과 최대 세기를 따로 둔다 —
        // 33% 광폭화는 66%보다 강하게 번쩍여야 한다.
        float _bossPhaseFlash;
        float _bossPhaseFlashPeak;
        float _bombFlashAge = float.MaxValue;
        const float BombFlashDuration = 0.5f;
        [SerializeField] BombButton _bombButton;
        const float BossFlashDuration = 0.09f;

        /// <summary>
        /// 경로 선택은 폐지됐다 (REQ-054) — 새 런은 이 상태에 들어가지 않고
        /// `RunManager.ChooseRoute`는 `NotSupportedException`을 던진다. 다른 화면이
        /// "메뉴가 떠 있는지" 판단할 때 쓰던 자리라 상수 false로 남겨 둔다.
        /// </summary>
        public bool AwaitingRoute => false;


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
        readonly List<int> _recordedContractChoices = new List<int>(8);
        int _replayContractCursor;
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

            // 적 티어별 히트박스 크기를 캐시해 스프라이트 스케일에 쓴다.
            CacheEnemyExtents(data);

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
                _recordedContractChoices.Clear();
                if (pendingReplay.contractChoices != null)
                    _recordedContractChoices.AddRange(pendingReplay.contractChoices);
                _replayContractCursor = 0;
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
                        // 기체 인식 게이지 (REQ-078) — 인자 없는 구버전은 7칸을
                        // 만들어 5칸 기체 검증에서 예외가 난다 (실측 재현).
                        data.CreatePowerUpGauge(resumeShip),
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
                    data.CreatePowerUpGauge(selectedShip),
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
                _recordedContractChoices.Clear();
                _recordShipId = selectedShip != null ? selectedShip.Id : null;
            }

            // 풀 용량은 Core가 허용하는 최대 개수와 맞춘다 — 런타임에 풀이 부족해질 수 없다.
            // 시뮬 Bullets는 플레이어탄+적탄 합산 리스트 — 풀도 합산 용량이어야 한다 (GROK 스트레스 검증 후속)
            _bulletPool = new SpritePool(
                _bulletPrefab, _bulletRoot, config.MaxBullets + config.MaxEnemyBullets, "Bullet");
            _enemyPool = new SpritePool(_enemyPrefab, _enemyRoot, 32, "Enemy");
            _capsulePool = new SpritePool(_capsulePrefab, _capsuleRoot, 16, "Capsule");
            if (_bombPickupPrefab != null && _bombPickupRoot != null)
            {
                _bombPickupPool = new SpritePool(
                    _bombPickupPrefab, _bombPickupRoot, config.MaxBombPickups, "BombPickup");
            }
            _fxPool = new SpritePool(_explosionPrefab, _fxRoot, 16, "Explosion");
            _optionPool = new SpritePool(_optionPrefab, _optionRoot, 6, "Option");   // 옵션 6기 (REQ-084)
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
                        // -1 = 리롤 (REQ-072). 리롤은 대기 상태를 유지하므로 다음
                        // 틱에 이어지는 기록(다음 리롤 또는 실제 선택)을 소비한다.
                        if (choice == RerollChoiceSentinel)
                            _run.RerollRewardOptions();
                        else
                            _run.ChooseReward(choice);
                    }
                    // 계약 대기: 기록된 계약 선택을 재현. 기록이 모자라면 0(표준 항로) —
                    // Core가 표준 항로를 항상 0번에 두므로 안전한 기본값이다.
                    else if (_run.State == RunState.AwaitingContract)
                    {
                        int choice = _replayContractCursor < _recordedContractChoices.Count
                            ? _recordedContractChoices[_replayContractCursor++] : 0;
                        _run.ChooseContract(choice);
                    }
                    // 경로 선택 재현은 없어졌다 (REQ-054). 구버전 리플레이의 route
                    // payload는 열리기만 하고, 재생은 현 빌드 규칙을 따른다.
                }
            }
            else
            {
                command = _input.ConsumeCommand();
            }
            if (_recordingActive && playingBefore)
            {
                // 용량 방어 (스테이지 1 크래시, 2026-07-30 폰 스크린샷). 녹화는 같은
                // 입력이 이어지면 한 칸으로 압축하는데, 터치 아날로그 입력은 매 틱
                // 델타가 달라 압축이 전혀 안 되고 4096칸이 ~68초 만에 찬다. 가득 찬
                // 뒤의 Record는 예외를 던져 Update가 매 프레임 죽는다.
                //
                // 잘린 리플레이는 재생 시 어차피 어긋나므로, 차기 직전에 녹화를 접고
                // 리플레이 저장을 포기한다 — 게임은 계속된다. 근본(용량/아날로그 압축)은
                // Core 몫이라 REQ로 넘겼다.
                if (_recorder.RunCount >= _recorder.Capacity - 1)
                {
                    _recordingActive = false;
                    Debug.LogWarning(
                        "[replay] 입력 녹화 용량 도달 — 이번 런은 리플레이 저장 없이 진행한다.");
                }
                else
                {
                    _recorder.Record(in command);
                }
            }
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
                        contractChoices = _recordedContractChoices.ToArray(),
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
                        // WARNING 배너는 스테이지 최종 보스(와 숨은 보스)에게만 띄운다
                        // ("중간보스 나올때 Warning 뜨는것도 이상함", 2026-07-30).
                        // 중간보스는 스테이지마다 나오는 통과 의례라 매번 배너가 뜨면
                        // 경고의 무게가 사라진다 — 흔들림만 남긴다.
                        if (_bossIntro != null
                            && StageSection != RunStageSection.MidBoss)
                            _bossIntro.Trigger();
                        if (_juice != null) _juice.Shake(0.3f);
                        break;
                    case SimEventType.BossPhaseChanged:
                        // 페이즈가 넘어간 것이 명백해야 한다 — 예전에는 탄종만 바뀌어
                        // 플레이어가 눈치채지 못했다 (REQ-054). 33% 광폭화는 66%보다
                        // 확실히 크게 알린다. Arg는 0-based 페이즈 index(1 또는 2).
                        bool enraged = e.Arg >= 2;
                        if (_juice != null)
                        {
                            _juice.Shake(enraged ? 0.55f : 0.3f);
                            _juice.Hitstop(enraged ? 0.14f : 0.08f);
                        }
                        _bossPhaseFlash = enraged ? 0.75f : 0.45f;
                        _bossPhaseFlashPeak = enraged ? 1f : 0.6f;
                        // WARNING 배너를 여기서 다시 틀지 않는다 ("보스 HP가 내려갈때마다
                        // Warning이 또 뜨는건 이상해", 2026-07-30). 배너는 "보스가 왔다"는
                        // 등장 신호인데 페이즈마다 반복되면 의미가 섞인다 — 페이즈 전환은
                        // 흔들림·히트스톱·보스 글로우만으로 알린다.
                        break;
                    case SimEventType.CorridorContact:
                        // 벽에 닿았다 — 어디가 벽인지 번쩍여 알린다 (실제 피해는 PlayerHit).
                        if (_gimmickView != null) _gimmickView.FlashCorridorContact();
                        if (_juice != null) _juice.Shake(0.12f);
                        break;
                    case SimEventType.TimeLimitExpired:
                        // 제한 시간 초과는 방어막·무적을 무시하는 즉사다. 사망 연출과
                        // 구분되게 크게 알린다.
                        if (_juice != null)
                        {
                            _juice.Shake(0.7f);
                            _juice.Hitstop(0.2f);
                        }
                        break;
                    case SimEventType.BossAttackTelegraphed:
                        // 위험 패턴 예고 (REQ-059). 이벤트만 있고 그리지 않으면 예고가
                        // 없는 것과 같다 — 회피가 실력이 되려면 눈에 보여야 한다.
                        // 보스 본체를 잠깐 밝히는 기존 페이즈 플래시를 약하게 재사용한다.
                        _bossPhaseFlash = Mathf.Max(_bossPhaseFlash, 0.3f);
                        _bossPhaseFlashPeak = Mathf.Max(_bossPhaseFlashPeak, 0.5f);
                        break;
                    case SimEventType.BombActivated:
                        // 화면을 지우는 사건이므로 가장 크게 알린다. 무적 시간이 함께
                        // 붙으므로(45틱) 플레이어가 "지금 안전하다"를 읽을 수 있어야 한다.
                        _bombFlashAge = 0f;
                        if (_juice != null)
                        {
                            _juice.Shake(0.6f);
                            _juice.Hitstop(0.12f);
                        }
                        break;
                    case SimEventType.BombActivationRejectedEmpty:
                        // 눌렀는데 아무 일도 없으면 버튼이 고장난 것처럼 느껴진다.
                        // 재고가 없다는 것을 짧게 알린다.
                        if (_bombButton != null) _bombButton.FlashEmpty();
                        break;
                    case SimEventType.LaserCapacityExceeded:
                    case SimEventType.EnemyCapacityExceeded:
                    case SimEventType.ObstacleCapacityExceeded:
                        // 상한 초과를 조용히 넘기지 않는다 — 과거에 적 탄이 상한에 걸려
                        // 조용히 발사되지 않던 버그를 놓친 전례가 있다.
                        if (Debug.isDebugBuild || Application.isEditor)
                            Debug.LogWarning(
                                $"[capacity] {e.Type} cap={e.Arg} at ({e.X},{e.Y})");
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
                    case SimEventType.ObstacleDamaged:
                        _obstacleHitFlashes[e.EntityId] = ObstacleHitFlashSeconds;
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
            if (_bombPickupPool != null) ReleaseAll(_bombPickupViews, _bombPickupPool);
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
            SyncBombPickups();
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
                        // 장애물은 예약 틱에 자기 좌표(화면 안 포함)에서 즉시 생겨난다 —
                        // 그대로 그리면 끊기듯 나타난다 ("장애물이 끊기듯 등장", 2026-07-31).
                        // 시뮬 판정은 스폰 즉시 유효하지만, 페이드는 짧아(0.35s) 판정과
                        // 표시의 어긋남이 문제되기 전에 끝난다.
                        renderer.color = new Color(1f, 1f, 1f, 0f);
                    }
                    _obstacleFadeAges[obstacle.Id] = 0f;
                }
                view.localPosition = SimView.ToWorld(obstacle.X, obstacle.Y);

                bool fading = _obstacleFadeAges.TryGetValue(obstacle.Id, out float age)
                    && age < ObstacleFadeSeconds;
                bool flashing = _obstacleHitFlashes.TryGetValue(obstacle.Id, out float flash)
                    && flash > 0f;
                if (fading || flashing)
                {
                    var stateRenderer = view.GetComponent<SpriteRenderer>();
                    if (fading)
                    {
                        age += Time.deltaTime;
                        _obstacleFadeAges[obstacle.Id] = age;
                    }
                    if (flashing)
                    {
                        flash -= Time.deltaTime;
                        // 0에 닿는 프레임에 흰색으로 복원되고 키가 빠진다 — 잔틴트 방지
                        if (flash <= 0f) _obstacleHitFlashes.Remove(obstacle.Id);
                        else _obstacleHitFlashes[obstacle.Id] = flash;
                    }
                    if (stateRenderer != null)
                    {
                        // 피격 플래시(앰버)와 스폰 페이드(알파)는 독립 축 — 동시여도 겹친다
                        float flashT = flashing
                            ? Mathf.Clamp01(flash / ObstacleHitFlashSeconds) : 0f;
                        var c = Color.Lerp(Color.white, ObstacleHitFlashColor, flashT);
                        c.a = fading ? Mathf.Clamp01(age / ObstacleFadeSeconds) : 1f;
                        stateRenderer.color = c;
                    }
                }
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
                _obstacleFadeAges.Remove(id);
                _obstacleHitFlashes.Remove(id);
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

        /// <summary>
        /// 신규 보스 탄종(REQ-087)의 색 — 전용 스프라이트가 나오기 전까지 색·크기로
        /// 구분한다. 탄종 구분은 게임플레이 정보라 반드시 한눈에 갈라져야 한다.
        /// </summary>
        static Color ColorForBulletKind(BulletKind kind)
        {
            switch (kind)
            {
                case BulletKind.Heavy: return new Color(1f, 0.55f, 0.25f, 1f);     // 묵직한 주황
                case BulletKind.Splitter: return new Color(1f, 0.5f, 0.8f, 1f);    // 분열 예고 핑크
                case BulletKind.Mine: return new Color(0.55f, 0.85f, 1f, 1f);      // 정지 기뢰 청백
                default: return Color.white;
            }
        }

        static float ScaleForBulletKind(BulletKind kind)
        {
            switch (kind)
            {
                case BulletKind.Heavy: return 2.2f;
                case BulletKind.Splitter: return 1.35f;
                case BulletKind.Mine: return 1.6f;
                default: return 1f;
            }
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
            // 페이즈 전환 발광은 피격 플래시보다 우선한다 — 전환은 드물고 중요한 사건이라
            // 피격에 묻히면 안 된다. 흰빛으로 크게 번쩍이며 감쇠한다.
            if (_bossPhaseFlash > 0f)
            {
                _bossPhaseFlash -= Time.deltaTime;
                float glow = Mathf.Clamp01(_bossPhaseFlash) * _bossPhaseFlashPeak;
                if (_juice != null && _juice.FlashReduced) glow *= 0.4f;
                bossColor = Color.Lerp(bossColor, Color.white, glow);
                // 발광 중에는 살짝 부풀려 존재감을 준다.
                float swell = 1f + glow * 0.12f;
                _bossRenderer.transform.localScale = new Vector3(swell, swell, 1f);
            }
            else if (_bossRenderer.transform.localScale != Vector3.one)
            {
                _bossRenderer.transform.localScale = Vector3.one;
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

        // ── 섹터 계약 (REQ-070 — ContractScreen이 소비) ─────────────────────────

        public bool AwaitingContract =>
            _run != null && _run.State == RunState.AwaitingContract;

        public System.Collections.Generic.IReadOnlyList<ContractDefinition> ContractOptions
            => _run?.ContractOptions;

        /// <summary>현재 스테이지에 적용 중인 계약. 스테이지 1과 런 종료 후에는 null.</summary>
        public ContractDefinition ActiveContract => _run?.ActiveContract;

        public void ChooseContract(int index)
        {
            if (!AwaitingContract) return;
            if (_replayMode) return;   // 리플레이 중 수동 선택 금지 (자동 재현)
            if (!_run.ChooseContract(index)) return;   // 잘못된 인덱스는 Core가 안전 거부
            // 기록은 성공 후에만 — 거부된 선택이 기록되면 리플레이가 어긋난다.
            if (_recordingActive) _recordedContractChoices.Add(index);
            RefreshBattle();
            SyncViews();
        }

        /// <summary>
        /// 지금 고르는 보상이 중간보스 직후의 짧은 2택인지, 스테이지 보스 후의 주 3택인지.
        /// 화면 제목과 배치를 나누는 데 쓴다 (REQ-054).
        /// </summary>
        public RewardSelectionKind RewardKind =>
            _run != null ? _run.RewardSelectionKind : RewardSelectionKind.None;

        /// <summary>스테이지 내부 진행 구간 — 전반/중간보스/후반/보스 연출을 가른다.</summary>
        public RunStageSection StageSection =>
            _run != null ? _run.StageSection : RunStageSection.Opening;

        /// <summary>지속 레이저 상태 (REQ-042) — LaserBeamView가 선분으로 그린다.</summary>
        public IReadOnlyList<LaserState> Lasers => _sim?.Lasers;

        // ── 스테이지 기믹 관측값 (REQ-055) — StageGimmickView가 그린다 ─────────
        public StageEnvironmentState Environment =>
            _sim != null ? _sim.Environment : default;

        /// <summary>네뷸라 시야 제한. 표현 전용 플래그로, 판정에는 영향이 없다.</summary>
        public bool VisionObscured => _sim != null && _sim.VisionObscured;

        /// <summary>코어 스테이지 제한 시간. 0이면 제한이 없다. 초과는 즉사다.</summary>
        public int RemainingTimeTicks => _sim != null ? _sim.RemainingTimeTicks : 0;
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

        // ── 보상 리롤 (REQ-072 — 캡슐 화폐) ─────────────────────────────────────

        public int CapsuleBalance => _run?.CapsuleBalance ?? 0;
        public int RewardRerollCost => _run?.RewardRerollCost ?? 0;
        public bool CanRerollRewards => _run != null && _run.CanRerollRewardOptions;

        /// <summary>리롤 성공 여부. 리플레이 기록에는 선택 -1이 리롤을 뜻한다.</summary>
        public bool RerollRewards()
        {
            if (!AwaitingReward || _replayMode) return false;
            if (!_run.RerollRewardOptions()) return false;
            if (_recordingActive) _recordedChoices.Add(RerollChoiceSentinel);
            return true;
        }

        /// <summary>
        /// 리플레이 선택 기록에서 리롤을 뜻하는 센티널. 실제 카드 인덱스는 0 이상이므로
        /// 충돌하지 않고, 구버전 리플레이는 스키마 버전(v13)에서 이미 거부된다.
        /// </summary>
        public const int RerollChoiceSentinel = -1;

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

        /// <summary>보유한 전멸 폭탄 수 — HUD와 폭탄 버튼의 활성 여부를 정한다.</summary>
        public int BombStock => _sim?.BombStock ?? 0;

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

                    // Kind는 Id 수명 동안 불변 — 획득 시 한 번만 외형을 고른다.
                    // 풀 재사용 대비: 색·스케일도 매 획득마다 반드시 재설정한다.
                    var renderer = view.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.sprite = SpriteForBulletKind(bullet.Kind);
                        renderer.color = ColorForBulletKind(bullet.Kind);
                    }
                    view.localScale = Vector3.one * ScaleForBulletKind(bullet.Kind);
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
                    // 옵션 8×8은 너무 작아 눈에 안 띈다 ("크기를 두배정도", 2026-07-31).
                    // 순수 표현 스케일 — 옵션은 피격 판정이 없어 크기가 커져도 안전하다.
                    view.localScale = Vector3.one * 2f;
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
                        ApplyEnemyScale(view, renderer, enemy.DefinitionId);
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

            if (_damageFlash == null) return;

            bool flashReduced = _juice != null && _juice.FlashReduced;

            // 폭탄이 피격보다 우선한다 — 화면을 지우는 사건이 더 크고, 폭탄 직후 피격이
            // 겹칠 때(무적 만료 직전) 약한 쪽이 이기면 연출이 뒤바뀐다.
            if (_bombFlashAge < BombFlashDuration)
            {
                _bombFlashAge += Time.deltaTime;
                float t = Mathf.Clamp01(1f - _bombFlashAge / BombFlashDuration);
                // 폭탄 아이콘과 같은 자홍 계열 — 무엇이 터졌는지 색으로 연결된다.
                _damageFlash.color = new Color(1f, 0.55f, 1f, t * (flashReduced ? 0.3f : 0.7f));
                return;
            }

            if (_damageFlashAge >= DamageFlashDuration) return;

            _damageFlashAge += Time.deltaTime;
            float intensity = flashReduced ? 0.15f : 0.35f;
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

                // 캡슐만 맥동시켜 "먹어야 하는 것"임을 알린다. 적·옵션은 맥동하지 않으므로
                // 움직임 자체가 구분 신호가 된다. 표현 전용이라 시뮬에는 영향이 없다.
                var pulseRenderer = view.GetComponent<SpriteRenderer>();
                if (pulseRenderer != null)
                {
                    // id를 위상에 섞어 여러 캡슐이 한꺼번에 깜빡이지 않게 한다.
                    float phase = Time.time * 6f + capsule.Id * 0.7f;
                    float t = (Mathf.Sin(phase) + 1f) * 0.5f;
                    pulseRenderer.color = Color.Lerp(
                        new Color(0.72f, 0.92f, 1f, 1f), Color.white, t);
                }
            }

            ReleaseDeadViews(_capsuleViews, _capsulePool);
        }

        /// <summary>
        /// 전멸 폭탄 픽업. 캡슐과 달리 회전으로 알린다 — 둘이 같이 떨어졌을 때
        /// 맥동만으로는 구분이 안 되므로 움직임의 종류 자체를 다르게 둔다.
        /// </summary>
        void SyncBombPickups()
        {
            if (_bombPickupPool == null) return;

            var pickups = _sim.BombPickups;
            _aliveIds.Clear();

            for (int i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                _aliveIds.Add(pickup.Id);

                if (!_bombPickupViews.TryGetValue(pickup.Id, out var view))
                {
                    view = _bombPickupPool.Acquire();
                    if (view == null) continue;
                    _bombPickupViews.Add(pickup.Id, view);
                }

                view.localPosition = SimView.ToWorld(pickup.X, pickup.Y);
                // 표현 전용 회전 — 시뮬 판정은 축 정렬 박스라 영향이 없다.
                view.localRotation = Quaternion.Euler(
                    0f, 0f, (Time.time * 90f + pickup.Id * 37f) % 360f);
            }

            ReleaseDeadViews(_bombPickupViews, _bombPickupPool);
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
        /// <summary>
        /// 적 정의의 히트박스 반폭 (서브유닛). 적 티어가 생기면서 판정 크기가 5.7배까지
        /// 벌어졌는데, 스프라이트를 그대로 두면 "안 맞았는데 맞는" 판정이 생겨 크기가
        /// 알려주는 정보가 거짓이 된다.
        /// </summary>
        readonly Dictionary<string, int> _enemyHalfWidths = new Dictionary<string, int>(64);

        void CacheEnemyExtents(GameDataSet data)
        {
            _enemyHalfWidths.Clear();
            var definitions = data.BattleContent.Enemies;
            for (int i = 0; i < definitions.Count; i++)
                _enemyHalfWidths[definitions[i].Id] = definitions[i].HalfWidth;
        }

        /// <summary>
        /// 스프라이트를 히트박스 폭에 맞춘다. 균일 스케일이라 원본 종횡비는 유지된다 —
        /// 축별로 늘리면 픽셀아트가 찌그러진다. 티어별 전용 스프라이트가 준비되면
        /// 스케일 배율이 1에 가까워지므로 이 코드는 그대로 두면 된다.
        /// </summary>
        void ApplyEnemyScale(Transform view, SpriteRenderer renderer, string definitionId)
        {
            if (view == null) return;
            if (renderer == null || renderer.sprite == null
                || !_enemyHalfWidths.TryGetValue(definitionId, out int halfWidthSubUnits))
            {
                view.localScale = Vector3.one;
                return;
            }

            var sprite = renderer.sprite;
            float spriteWorldWidth = sprite.rect.width / sprite.pixelsPerUnit;
            if (spriteWorldWidth <= 0.0001f)
            {
                view.localScale = Vector3.one;
                return;
            }

            float targetWorldWidth =
                2f * halfWidthSubUnits / SimSpace.SubUnitsPerWorldUnit;
            float scale = targetWorldWidth / spriteWorldWidth;
            view.localScale = new Vector3(scale, scale, 1f);
        }

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
