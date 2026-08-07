using System;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-184: 레이저 3단(프리즘 빔)에서도 옵션은 계속 쏜다.
    ///
    /// 빔은 본체에서만 나간다. 예전에는 빔이 메인샷 발리를 통째로 대체해
    /// 옵션의 주무기 기여가 0이 됐다 (사람 보고 2026-08-07: "옵션에서
    /// 레이저가 하나도 안나가는 것 같아"). 결정: 본체는 빔, 옵션은 2단
    /// (랜스) 볼트를 유지한다.
    /// </summary>
    public sealed class Req184BeamOptionVolleyTests
    {
        [Test]
        public void BeamAndOptionBoltCoexistAndHonorTheSharedCooldown()
        {
            PowerUpGauge gauge = CreateGauge();
            Activate(gauge, PowerUpSlot.Option);
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            Activate(gauge, PowerUpSlot.Laser);
            BattleSim sim = CreateBattle(gauge);
            InputCommand fire = new InputCommand(0, 0, true);

            sim.Step(in fire);

            Assert.AreEqual(1, sim.Options.Count);
            Assert.AreEqual(1, sim.Lasers.Count);
            Assert.AreEqual(
                LaserSourceKind.Player, sim.Lasers[0].SourceKind);
            Assert.AreEqual(
                1,
                sim.Bullets.Count,
                "빔이 켜져 있어도 옵션 한 기가 볼트 한 발을 쏴야 한다.");

            // 발사 간격이 도는 동안 다음 틱에 볼트가 더 늘면 안 된다 —
            // 옵션 발리도 본체와 같은 쿨다운 규칙을 쓴다.
            sim.Step(in fire);
            Assert.AreEqual(1, sim.Bullets.Count);
            Assert.AreEqual(1, sim.Lasers.Count);
        }

        [Test]
        public void RepositoryLaserLevelThreeKeepsTheLanceImpactExplosion()
        {
            // 옵션 볼트가 "2단 랜스 그대로"라는 결정은 데이터가 지켜야 한다:
            // 3단 프로필이 착탄 폭발을 물려받지 않으면 옵션 볼트만 조용히
            // 약해진다. 수치를 복사하지 않고 2단과의 관계로 못 박는다.
            string root = TestKit.FindRepositoryRoot();
            string gameData = Path.Combine(root, "GameData");
            GameDataSet data = GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")));
            PrimaryWeaponFamilyDefinition laser =
                data.BattleContent.FindPrimaryWeaponFamily(
                    PrimaryWeaponFamily.Laser);
            PrimaryWeaponLevelDefinition lance = laser.GetLevel(2);
            PrimaryWeaponLevelDefinition prism = laser.GetLevel(3);

            Assert.Greater(lance.ImpactExplosionDamage, 0);
            Assert.AreEqual(
                lance.ImpactExplosionDamage,
                prism.ImpactExplosionDamage);
            Assert.AreEqual(
                lance.ImpactExplosionRadius,
                prism.ImpactExplosionRadius);
            Assert.AreEqual(
                lance.PierceEnemyCount,
                prism.PierceEnemyCount);
        }

        static BattleSim CreateBattle(PowerUpGauge gauge)
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerSpeedPerTick = 10;
            config.PlayerBulletSpeedPerTick = 20;
            config.PlayerMinX = -1000;
            config.PlayerMaxX = 1000;
            config.PlayerMinY = -1000;
            config.PlayerMaxY = 1000;
            config.PlayerSpawnX = 0;
            config.PlayerSpawnY = 0;
            config.BulletDespawnX = 10000;
            config.EnemyDespawnX = -10000;
            config.MaxBullets = 64;
            config.MaxEnemyBullets = 0;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.CapsuleHalfWidth = 0;
            config.CapsuleHalfHeight = 0;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            var weapon = new WeaponDefinition(
                "shot", 1, 10, 20, 1, 0, 0);
            var content = new BattleContent(
                Array.Empty<EnemyDefinition>(),
                new[] { weapon },
                weapon.Id,
                CreateFamilies());
            var segment = new StageSegment(
                "req184",
                100,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment }, "boss", 1, 1, 1);
            return new BattleSim(
                config,
                new Rng(0x8604UL),
                plan,
                content,
                gauge,
                BattleModifier.None);
        }

        static PrimaryWeaponFamilyDefinition[] CreateFamilies()
        {
            // BattleContent가 Double+Laser 두 패밀리를 최소로 요구한다.
            int[] doubleAngles = { 0, 5 };
            var doubleLevels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 5, 0, 2, 5, doubleAngles)
            };
            int[] laserAngles = { 0 };
            var laserLevels = new[]
            {
                new PrimaryWeaponLevelDefinition(
                    1, 8, 2, 1, 0, laserAngles),
                new PrimaryWeaponLevelDefinition(
                    2, 8, 4, 1, 0, laserAngles,
                    impactExplosionDamage: 2,
                    impactExplosionRadius: 64),
                new PrimaryWeaponLevelDefinition(
                    3, 8, 4, 1, 0, laserAngles,
                    impactExplosionDamage: 2,
                    impactExplosionRadius: 64,
                    beamDamagePerTick: 2,
                    beamLength: 500,
                    beamStartHalfWidth: 1,
                    beamGrowthPerTick: 2,
                    beamMaxHalfWidth: 5)
            };
            return new[]
            {
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Double,
                    "Double",
                    "Double evolution.",
                    WeaponType.Spread,
                    1, 10, 5, 3, 1,
                    20, 1, 0, 0, 0, 2, 5,
                    doubleAngles,
                    doubleLevels),
                new PrimaryWeaponFamilyDefinition(
                    PrimaryWeaponFamily.Laser,
                    "Laser",
                    "Laser evolution.",
                    WeaponType.Laser,
                    1, 10, 8, 2, 1,
                    20, 1, 0, 0, 2, 1, 0,
                    laserAngles,
                    laserLevels)
            };
        }

        static PowerUpGauge CreateGauge()
        {
            return new PowerUpGauge(new[]
            {
                5, 3, 6, 3, 6, 3, 3, 3
            });
        }

        static void Activate(PowerUpGauge gauge, PowerUpSlot slot)
        {
            int gaugeIndex = -1;
            for (int i = 0; i < gauge.GaugeSlots.Count; i++)
                if (gauge.GaugeSlots[i].Slot == slot)
                {
                    gaugeIndex = i;
                    break;
                }
            for (int i = 0; i <= gaugeIndex; i++)
                gauge.Collect();
            Assert.AreEqual(
                PowerUpActivationResult.LevelIncreased,
                gauge.ActivateDetailed());
        }
    }
}
