using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    /// <summary>Integer-only tuning. Fractional speeds use numerator/denominator pairs.</summary>
    public sealed class BattleSimConfig
    {
        public const int ComboMultiplierLevelCount = 6;
        public const int ProvisionalShieldBonusScorePerStock = 5000;
        internal const int DefaultMaxEnemyBullets = 128;
        /// <summary>
        /// Human-approved REQ-049 default and upgrade ceiling.
        /// </summary>
        public const int DefaultMaxShieldStock = 3;
        public const int MaximumShieldStock = 5;
        public const int ProvisionalMaxShieldStock = DefaultMaxShieldStock;
        /// <summary>
        /// Provisional REQ-041 cap pending explicit human balance approval.
        /// </summary>
        public const int ProvisionalMaxBombStock = 3;
        /// <summary>
        /// 피격 후 무적 시간. 사람 지시 2026-08-03: "피격 당했을 때 깜빡이면서
        /// 2~3초 정도 무적시간이 있어야 할듯."
        ///
        /// 예전 값은 0.3초였다(Presentation의 피격 플래시 길이에 맞춘 값). 그 정도로는
        /// 탄막 한가운데에서 실드를 깨고 나온 순간 곧바로 다음 탄에 다시 맞는다 —
        /// 실드를 하나 쓴 대가가 "다음 탄까지 0.3초"인 셈이라 회복할 기회가 없다.
        /// 처음엔 2.5초로 잡았다가 사람이 1.5초로 정했다 — 빠져나올 시간은 되면서
        /// 무르게 느껴지지 않는 선. 뷰가 그동안 기체를 깜빡여 무적임을 알린다.
        /// </summary>
        public const int DefaultPlayerHitInvulnerabilityTicks =
            3 * SimSpace.TicksPerSecond / 2;
        public const int DefaultBombInvulnerabilityTicks =
            3 * SimSpace.TicksPerSecond / 4;

        int _playerSpeedNumerator, _bulletSpeedNumerator;
        int _playerSpeedDenominator = 1, _bulletSpeedDenominator = 1;
        int _startingShieldStock = 1;

        /// <summary>Whole subunits/tick shorthand. Setting it resets the denominator to 1.</summary>
        public int PlayerSpeedPerTick
        {
            get => _playerSpeedNumerator / _playerSpeedDenominator;
            set { _playerSpeedNumerator = value; _playerSpeedDenominator = 1; }
        }

        public int PlayerSpeedNumerator { get => _playerSpeedNumerator; set => _playerSpeedNumerator = value; }
        public int PlayerSpeedDenominator { get => _playerSpeedDenominator; set => _playerSpeedDenominator = value; }

        /// <summary>Legacy whole subunits/tick shorthand for the stage-less constructor.</summary>
        public int PlayerBulletSpeedPerTick
        {
            get => _bulletSpeedNumerator / _bulletSpeedDenominator;
            set { _bulletSpeedNumerator = value; _bulletSpeedDenominator = 1; }
        }

        public int PlayerBulletSpeedNumerator { get => _bulletSpeedNumerator; set => _bulletSpeedNumerator = value; }
        public int PlayerBulletSpeedDenominator { get => _bulletSpeedDenominator; set => _bulletSpeedDenominator = value; }
        public WeaponType PlayerWeaponType { get; set; } = WeaponType.Vulcan;
        /// <summary>
        /// Optional exact family identity. This distinguishes Double from
        /// Triple/Spread even though both use WeaponType.Spread.
        /// </summary>
        public PrimaryWeaponFamily? PlayerWeaponFamily { get; set; }
        public int MainShotBaseDamage { get; set; }
        public int FireIntervalTicks { get; set; }
        public int MainShotHalfWidth { get; set; }
        public int MainShotHalfHeight { get; set; }
        public bool UseConfiguredMainShotStats { get; set; }
        public int MaxBullets { get; set; }
        /// <summary>
        /// Shared regular-enemy population cap. Scheduled and boss-spawned enemies
        /// both use this budget.
        /// </summary>
        public int MaxEnemies { get; set; } = 128;
        public int PlayerMinX { get; set; }
        public int PlayerMaxX { get; set; }
        public int PlayerMinY { get; set; }
        public int PlayerMaxY { get; set; }
        public int BulletDespawnX { get; set; }
        public int EnemyDespawnX { get; set; } = int.MinValue;
        public int PlayerSpawnX { get; set; }
        public int PlayerSpawnY { get; set; }
        /// <summary>Shield stocks available at battle tick zero.</summary>
        public int StartingShieldStock
        {
            get => _startingShieldStock;
            set => _startingShieldStock = value;
        }
        /// <summary>
        /// Compatibility alias for callers that still populate ship HP. REQ-040
        /// interprets the old value as starting shield stock; it is not hull HP.
        /// </summary>
        public int PlayerMaxHp
        {
            get => _startingShieldStock;
            set => _startingShieldStock = value;
        }
        /// <summary>
        /// Provisional cap pending the human balance decision requested by REQ-040.
        /// </summary>
        public int MaxShieldStock { get; set; } =
            ProvisionalMaxShieldStock;
        public int PlayerHitInvulnerabilityTicks { get; set; } =
            DefaultPlayerHitInvulnerabilityTicks;
        /// <summary>
        /// QA-only gate. Incoming hits and time-limit expiration cannot consume
        /// shield stock or kill the player while enabled.
        /// </summary>
        public bool PlayerInvulnerable { get; set; }
        public int StartingBombStock { get; set; }
        public int MaxBombStock { get; set; } = ProvisionalMaxBombStock;
        public int BombInvulnerabilityTicks { get; set; } =
            DefaultBombInvulnerabilityTicks;
        public int BombEffectRadiusSubUnits { get; set; } =
            48 * SimSpace.SubUnitsPerWorldUnit;
        public int BombRegularEnemyDamage { get; set; } = 1_000;
        public int BombBossDamageCap { get; set; } = 250;
        public int BombBossPartDamageCap { get; set; } = 250;
        public int BombNoDropWeight { get; set; } = 100;
        public int MaxBombPickups { get; set; } = 16;
        /// <summary>
        /// 동시에 살아 있을 수 있는 레이저 수. **넘치면 그 빔은 발사되지 않는다**
        /// (TryStartLaser가 LaserCapacityExceeded를 내고 그냥 돌아간다).
        ///
        /// 8은 전함 포탑이 4문이던 시절 값이다. 지금은 포탑만 6문이고 여기에
        /// 레이저 잡몹(laser_sentry · prism_beamer)과 플레이어 레이저가 겹친다 —
        /// 사람이 "레이저가 중간에 끊긴다"고 본 것의 절반이 이것이다(나머지 절반은
        /// 뷰의 슬롯 부족이었고 그쪽은 24로 올렸다).
        /// </summary>
        public int MaxLasers { get; set; } = 24;
        public int PlayerHalfWidth { get; set; }
        public int PlayerHalfHeight { get; set; }
        public int CapsuleHalfWidth { get; set; }
        public int CapsuleHalfHeight { get; set; }
        public int CapsuleNoDropWeight { get; set; }
        /// <summary>
        /// Persistent reward cost subtracted from each enemy capsule weight.
        /// </summary>
        public int CapsuleDropWeightReduction { get; set; }
        public int ContractBombDropMultiplierNumerator { get; set; } = 1;
        public int ContractBombDropMultiplierDenominator { get; set; } = 1;
        public bool ContractGuaranteesBombDrop { get; set; }
        public int ContractCapsuleDropMultiplierNumerator { get; set; } = 1;
        public int ContractCapsuleDropMultiplierDenominator { get; set; } = 1;
        public int ContractScoreMultiplierNumerator { get; set; } = 1;
        public int ContractScoreMultiplierDenominator { get; set; } = 1;
        public int ScrollSpeedNumerator { get; set; }
        public int ScrollSpeedDenominator { get; set; } = 1;
        /// <summary>
        /// Attraction radius in simulation subunits. Zero disables capsule magnetism.
        /// </summary>
        public int CapsuleMagnetRadiusSubUnits { get; set; }
        /// <summary>Capsule attraction speed numerator in subunits per tick.</summary>
        public int CapsuleMagnetSpeedNumerator { get; set; }
        public int CapsuleMagnetSpeedDenominator { get; set; } = 1;

        // Provisional route tuning (REQ-029, AGENTS.md section 7).
        public int RareEncounterChanceNumerator { get; set; } = 12;
        public int RareEncounterChanceDenominator { get; set; } = 100;
        /// <summary>Number of reward choices earned after clearing a Rare node.</summary>
        public int RareRewardSelectionCount { get; set; } = 2;

        // Provisional obstacle tuning (REQ-023, AGENTS.md section 7).
        // Shape and rewards remain configurable until the human balance pass.
        public int MaxObstacles { get; set; } = 32;
        public int ObstacleHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int ObstacleHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int ObstacleContactDamage { get; set; } = 1;
        public int BreakableObstacleScore { get; set; } = 25;
        /// <summary>
        /// Provisional run difficulty tuning (REQ-020, AGENTS.md section 7).
        /// Applied with deterministic ceiling to regular-enemy and boss HP only.
        /// </summary>
        public int EnemyHpMultiplierNumerator { get; set; } = 1;
        public int EnemyHpMultiplierDenominator { get; set; } = 1;

        // Provisional power-up tuning. These are deliberately configurable until
        // the human balance pass replaces them with approved GameData values.
        public int MainShotRapidFireStartLevel { get; set; } = 3;
        public int MainShotFireIntervalReductionPerLevel { get; set; } = 1;
        public int MainShotMinimumFireIntervalTicks { get; set; } = 4;

        // Provisional primary-family profiles (REQ-022, AGENTS.md section 7).
        // RunManager copies the selected profile into the resolved main-shot
        // fields above, so passive rewards and suspend checkpoints keep using
        // one stable set of integers.
        public int LaserBaseDamage { get; set; } = 20;
        public int LaserFireIntervalTicks { get; set; } = 16;
        public int LaserRapidFireStartLevel { get; set; } = 2;
        public int LaserFireIntervalReductionPerLevel { get; set; } = 2;
        public int LaserMinimumFireIntervalTicks { get; set; } = 10;
        public int LaserSpeedNumerator { get; set; } =
            32 * SimSpace.SubUnitsPerWorldUnit;
        public int LaserSpeedDenominator { get; set; } =
            SimSpace.TicksPerSecond;
        public int LaserHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int LaserHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 16;
        /// <summary>Enemies passed after the first laser hit.</summary>
        public int LaserPierceEnemyCount { get; set; } = 2;

        public int SpreadBaseDamage { get; set; } = 6;
        public int SpreadFireIntervalTicks { get; set; } = 10;
        public int SpreadRapidFireStartLevel { get; set; } = 3;
        public int SpreadFireIntervalReductionPerLevel { get; set; } = 1;
        public int SpreadMinimumFireIntervalTicks { get; set; } = 6;
        public int SpreadSpeedNumerator { get; set; } =
            18 * SimSpace.SubUnitsPerWorldUnit;
        public int SpreadSpeedDenominator { get; set; } =
            SimSpace.TicksPerSecond;
        public int SpreadHalfWidth { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 4;
        public int SpreadHalfHeight { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 8;
        public int SpreadWays { get; set; } = 3;
        /// <summary>Angular spacing in 1/64-turn SineLut slots.</summary>
        public int SpreadStepLutSlots { get; set; } = 2;
        public int[] MainShotAngleLutSlots { get; set; } =
            Array.Empty<int>();
        public int MissileBaseDamage { get; set; } = 2;
        public int MissileDamageGrowthPercentPerLevel { get; set; } = 50;
        public int OptionMissileDamagePercent { get; set; } = 100;
        public int MissileFireIntervalTicks { get; set; } = 45;
        public int MissileRapidFireStartLevel { get; set; } = 2;
        public int MissileFireIntervalReductionPerLevel { get; set; } = 5;
        public int MissileMinimumFireIntervalTicks { get; set; } = 30;
        public int MissileSpeedXNumerator { get; set; } = 13 * SimSpace.SubUnitsPerWorldUnit;
        public int MissileSpeedXDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int MissileFallSpeedYNumerator { get; set; } = 5 * SimSpace.SubUnitsPerWorldUnit;
        public int MissileFallSpeedYDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int MissileHalfWidth { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 8;
        public int MissileHalfHeight { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        public MissileFamily MissileFamily { get; set; } =
            MissileFamily.Straight;
        public int MissilePierceEnemyCount { get; set; }
        public int MissileExplosionDamage { get; set; }
        public int MissileExplosionRadiusSubUnits { get; set; }
        public int MissileExplosionMaxTargets { get; set; }
        public int MissileDropDelayTicks { get; set; }
        /// <summary>
        /// Player-position history distance between consecutive options.
        /// Option N follows the position from N * OptionFollowDelayTicks ago.
        /// </summary>
        public int OptionFollowDelayTicks { get; set; } = 12;
        public OptionFormation OptionFormation { get; set; } =
            OptionFormation.Trail;
        public int[] OptionFixedOffsetXs { get; set; } =
            new[] { 192, 192, 192, 192, 192, 192 };
        public int[] OptionFixedOffsetYs { get; set; } =
            new[] { 384, -384, 704, -704, 1024, -1024 };
        public int OptionOrbitRadiusSubUnits { get; set; } =
            7 * SimSpace.SubUnitsPerWorldUnit / 4;
        public int OptionOrbitAngularLutSlotsNumerator { get; set; } = 1;
        public int OptionOrbitAngularLutSlotsDenominator { get; set; } = 2;

        // 적탄 잠정값 (REQ-007) — GameData 이관 전까지 여기서 조절.
        public int EnemyBulletSpeedNumerator { get; set; } = 8 * SimSpace.SubUnitsPerWorldUnit;
        public int EnemyBulletSpeedDenominator { get; set; } = SimSpace.TicksPerSecond;
        public int EnemyBulletHalfWidth { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        public int EnemyBulletHalfHeight { get; set; } = 3 * SimSpace.SubUnitsPerWorldUnit / 16;
        public int EnemyBulletDamage { get; set; } = 1;
        /// <summary>적탄 전용 예산 — 플레이어 탄 풀(MaxBullets)을 잠식하지 않는다.</summary>
        public int MaxEnemyBullets { get; set; } = DefaultMaxEnemyBullets;

        // Provisional synergy tuning (REQ-013, AGENTS.md §7). These stay
        // configurable until the human/GROK balance pass approves authoritative
        // GameData values.
        public int PierceShotEnemyCount { get; set; } = 1;
        public int RicochetRangeSubUnits { get; set; } =
            8 * SimSpace.SubUnitsPerWorldUnit;
        /// <summary>Maximum homing turn per tick in 1/64-turn SineLut slots.</summary>
        public int HomingMissileTurnLutSlotsPerTick { get; set; } = 1;

        /// <summary>
        /// **적** 유도탄이 방향을 꺾는 시간(틱). 이 시간이 지나면 그 탄은 마지막
        /// 방향으로 직진한다.
        ///
        /// 없을 때는 유도가 탄의 수명 내내 이어졌다 — 한 번 발사되면 화면 어디로
        /// 도망쳐도 끝까지 따라붙고, 페이즈가 넘어가 그 패턴이 끝난 뒤에도 남은
        /// 탄은 계속 꺾었다 (사람 보고 2026-08-05: "하이브 보스 마지막 패턴 유도
        /// 미사일이 끝까지 따라오는거"). 피할 방법이 없는 탄은 패턴이 아니라
        /// 처형이다. 2초면 한 번 크게 유인해 흘려보낼 수 있다.
        /// </summary>
        public int EnemyHomingDurationTicks { get; set; } =
            2 * SimSpace.TicksPerSecond;
        public int KillExplosionRadiusSubUnits { get; set; } =
            3 * SimSpace.SubUnitsPerWorldUnit / 2;
        public int KillExplosionDamage { get; set; } = 1;
        public int KillExplosionMaxTargets { get; set; } = 4;

        // Fallback graze/combo scoring tuning (REQ-015/016, AGENTS.md §7).
        // Optional scoring.json values replace these through GameDataSet.
        public int GrazeExtraRadiusSubUnits { get; set; } =
            SimSpace.SubUnitsPerWorldUnit / 2;
        public int GrazeScore { get; set; } = 10;
        public int GrazeComboGaugeGain { get; set; } = 1;
        public int KillComboGaugeGain { get; set; } = 10;
        public int[] ComboGaugeRequirements { get; set; } =
            new[] { 30, 50, 80, 130, 200 };
        public int ComboDecayTicks { get; set; } = 300;
        public int[] ComboMultipliers { get; set; } =
            new[] { 1, 2, 4, 8, 16, 32 };
        /// <summary>
        /// Provisional run-clear award per remaining shield stock. GROK owns the
        /// eventual GameData balance value.
        /// </summary>
        public int ShieldBonusScorePerStock { get; set; } =
            ProvisionalShieldBonusScorePerStock;

        /// <summary>
        /// REQ-133: score awarded per 100 damage dealt to a boss or boss part.
        ///
        /// Boss rooms are fodder-thin by design and their barrages are sparse, so a
        /// player who is landing every shot can still have zero combo actions and
        /// watch the multiplier decay one step every five seconds. Damage itself now
        /// counts, which is what the player is actually doing during a boss fight.
        ///
        /// The default is deliberately small next to the defeat award, which is
        /// <c>maxHp * 2</c>: at 10 per 100 damage, grinding a boss from full to zero
        /// yields <c>maxHp * 0.1</c>, i.e. 5% of the defeat bonus. Killing still
        /// dominates; chip damage only keeps the combo clock alive. GROK owns the
        /// eventual GameData value.
        /// </summary>
        public int BossDamageScorePerHundred { get; set; } = 10;

        /// <summary>
        /// Defaults sourced from player.json, main_shot, and the 40 by 22.5 unit view
        /// (640×360, ROADMAP M0). Spatial values scale the 24×14 originals by ×5/3
        /// (hitboxes ×1.5 to follow the sprite upsize). Power-up values remain
        /// provisional pending the human balance pass.
        /// </summary>
        public static BattleSimConfig CreateDefault()
        {
            const int u = SimSpace.SubUnitsPerWorldUnit;
            return new BattleSimConfig
            {
                PlayerSpeedNumerator = 13 * u,
                PlayerSpeedDenominator = SimSpace.TicksPerSecond,
                PlayerBulletSpeedNumerator = 20 * u,
                PlayerBulletSpeedDenominator = SimSpace.TicksPerSecond,
                MainShotBaseDamage = 10,
                FireIntervalTicks = 8,
                MainShotHalfWidth = 3 * u / 8,
                MainShotHalfHeight = 9 * u / 64,
                MaxBullets = 64,
                PlayerMinX = -39 * u / 2,
                PlayerMaxX = 39 * u / 2,
                PlayerMinY = -43 * u / 4,
                PlayerMaxY = 43 * u / 4,
                BulletDespawnX = SimSpace.PlayfieldHalfWidthSubUnits + u,
                EnemyDespawnX = -(SimSpace.PlayfieldHalfWidthSubUnits + SimSpace.DespawnMarginSubUnits),
                PlayerSpawnX = -13 * u,
                PlayerSpawnY = 0,
                StartingShieldStock = 1,
                MaxShieldStock = ProvisionalMaxShieldStock,
                PlayerHitInvulnerabilityTicks =
                    DefaultPlayerHitInvulnerabilityTicks,
                StartingBombStock = 0,
                MaxBombStock = ProvisionalMaxBombStock,
                BombInvulnerabilityTicks =
                    DefaultBombInvulnerabilityTicks,
                PlayerHalfWidth = 3 * u / 8,
                PlayerHalfHeight = 3 * u / 8,
                CapsuleMagnetRadiusSubUnits = 3 * u,
                CapsuleMagnetSpeedNumerator = 8 * u,
                CapsuleMagnetSpeedDenominator = SimSpace.TicksPerSecond
            };
        }

        internal BattleSimConfig Copy()
        {
            var copy = (BattleSimConfig)MemberwiseClone();
            copy.OptionFixedOffsetXs = OptionFixedOffsetXs == null
                ? null
                : (int[])OptionFixedOffsetXs.Clone();
            copy.OptionFixedOffsetYs = OptionFixedOffsetYs == null
                ? null
                : (int[])OptionFixedOffsetYs.Clone();
            copy.MainShotAngleLutSlots =
                MainShotAngleLutSlots == null
                    ? null
                    : (int[])MainShotAngleLutSlots.Clone();
            copy.ComboGaugeRequirements =
                ComboGaugeRequirements == null
                    ? null
                    : (int[])ComboGaugeRequirements.Clone();
            copy.ComboMultipliers =
                ComboMultipliers == null
                    ? null
                    : (int[])ComboMultipliers.Clone();
            return copy;
        }
    }
}
