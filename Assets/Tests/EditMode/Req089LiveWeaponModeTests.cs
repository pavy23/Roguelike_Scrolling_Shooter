using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    public sealed class Req089LiveWeaponModeTests
    {
        [TestCase(
            "starter",
            PowerUpSlot.Double,
            PrimaryWeaponFamily.Double,
            2,
            3,
            5)]
        [TestCase(
            "interceptor",
            PowerUpSlot.Triple,
            PrimaryWeaponFamily.Spread,
            3,
            5,
            5)]
        [TestCase(
            "bulwark",
            PowerUpSlot.Laser,
            PrimaryWeaponFamily.Laser,
            1,
            1,
            0)]
        public void RepositoryGameDataWeaponSlotFiresAndEvolves(
            string shipId,
            PowerUpSlot weaponSlot,
            PrimaryWeaponFamily expectedFamily,
            int levelOneWays,
            int levelTwoWays,
            int levelThreeWays)
        {
            GameDataSet data = ParseRepositoryGameData();
            ShipDefinition ship = data.FindShip(shipId);
            PowerUpGauge gauge = data.CreatePowerUpGauge(ship);
            BattleSimConfig config = data.CreateBattleSimConfig();
            var run = new RunManager(
                0x890089UL,
                new SegmentStageGenerator(data.StageGeneration),
                config,
                data.BattleContent,
                gauge,
                data.Rewards,
                ship,
                1,
                1);

            Assert.AreEqual(weaponSlot, ship.GaugeSlots[3]);
            Assert.AreEqual(3, gauge.GetMaxLevel(weaponSlot));

            AssertWeaponLevel(
                run,
                weaponSlot,
                expectedFamily,
                1,
                levelOneWays);
            AssertWeaponLevel(
                run,
                weaponSlot,
                expectedFamily,
                2,
                levelTwoWays);
            AssertWeaponLevel(
                run,
                weaponSlot,
                expectedFamily,
                3,
                levelThreeWays);
        }

        static void AssertWeaponLevel(
            RunManager run,
            PowerUpSlot weaponSlot,
            PrimaryWeaponFamily expectedFamily,
            int expectedLevel,
            int expectedProjectileWays)
        {
            PowerUpGauge gauge = run.PowerUpGauge;
            while (!gauge.HasSelection
                || gauge.SelectedSlot != weaponSlot)
                gauge.Collect();

            BattleSim battle = (BattleSim)run.Battle;
            int previousBulletId = MaximumPlayerBulletId(battle.Bullets);
            int previousLaserId = MaximumPlayerLaserId(battle.Lasers);
            run.Step(new InputCommand(0, 0, true, true));
            battle = (BattleSim)run.Battle;
            // Unity 내장 NUnit에는 Assert.Multiple이 없다 (dotnet-vs-unity 컴파일 함정)
            Assert.AreEqual(expectedLevel, gauge.GetLevel(weaponSlot));
            Assert.AreEqual(
                PowerUpWeaponModeFor(weaponSlot),
                gauge.ActiveWeaponMode);
            Assert.AreEqual(
                expectedFamily,
                battle.EquippedPrimaryWeaponFamily);
            Assert.AreEqual(
                expectedLevel,
                battle.PrimaryWeaponEvolutionLevel);

            int originX = battle.PlayerX;
            int originY = battle.PlayerY;
            List<int> projectileIds = PlayerBulletIdsAfter(
                battle.Bullets,
                previousBulletId);
            List<int> laserIds = PlayerLaserIdsAfter(
                battle.Lasers,
                previousLaserId);
            for (int tick = 0;
                projectileIds.Count == 0
                    && laserIds.Count == 0
                    && tick < 20;
                tick++)
            {
                run.Step(new InputCommand(0, 0, true));
                projectileIds = PlayerBulletIdsAfter(
                    battle.Bullets,
                    previousBulletId);
                laserIds = PlayerLaserIdsAfter(
                    battle.Lasers,
                    previousLaserId);
            }
            Assert.AreEqual(
                expectedProjectileWays,
                projectileIds.Count,
                $"{expectedFamily} L{expectedLevel} projectile ways");

            if (expectedFamily == PrimaryWeaponFamily.Laser
                && expectedLevel == 3)
            {
                Assert.AreEqual(1, laserIds.Count);
                Assert.AreEqual(
                    LaserSourceKind.Player,
                    FindLaser(battle.Lasers, laserIds[0]).SourceKind);
            }
            else
            {
                Assert.AreEqual(0, laserIds.Count);
                run.Step(new InputCommand(0, 0, true));
                AssertExpectedDirections(
                    battle,
                    projectileIds,
                    expectedFamily,
                    expectedLevel,
                    originX,
                    originY);
            }

            for (int i = 0; i < 20; i++)
                run.Step(new InputCommand(0, 0, true));
            Assert.AreEqual(RunState.Playing, run.State);
        }

        static void AssertExpectedDirections(
            BattleSim battle,
            IReadOnlyList<int> projectileIds,
            PrimaryWeaponFamily family,
            int level,
            int originX,
            int originY)
        {
            bool forward = false;
            bool upward = false;
            bool downward = false;
            bool rearward = false;
            for (int i = 0; i < projectileIds.Count; i++)
            {
                BulletState bullet = FindBullet(
                    battle.Bullets,
                    projectileIds[i]);
                forward |= bullet.X > originX && bullet.Y == originY;
                upward |= bullet.Y > originY;
                downward |= bullet.Y < originY;
                rearward |= bullet.X < originX;
            }

            if (family == PrimaryWeaponFamily.Double)
            {
                Assert.IsTrue(forward);
                Assert.IsTrue(upward);
                Assert.AreEqual(level >= 2, rearward);
                return;
            }
            if (family == PrimaryWeaponFamily.Spread)
            {
                Assert.IsTrue(forward);
                Assert.IsTrue(upward);
                Assert.IsTrue(downward);
                return;
            }
            Assert.IsTrue(forward);
            Assert.IsFalse(upward);
            Assert.IsFalse(downward);
        }

        static int MaximumPlayerBulletId(IReadOnlyList<BulletState> bullets)
        {
            int maximum = -1;
            for (int i = 0; i < bullets.Count; i++)
                if (bullets[i].Faction == BulletFaction.Player)
                    maximum = Math.Max(maximum, bullets[i].Id);
            return maximum;
        }

        static int MaximumPlayerLaserId(IReadOnlyList<LaserState> lasers)
        {
            int maximum = -1;
            for (int i = 0; i < lasers.Count; i++)
                if (lasers[i].SourceKind == LaserSourceKind.Player)
                    maximum = Math.Max(maximum, lasers[i].Id);
            return maximum;
        }

        static List<int> PlayerBulletIdsAfter(
            IReadOnlyList<BulletState> bullets,
            int previousId)
        {
            var result = new List<int>();
            for (int i = 0; i < bullets.Count; i++)
                if (bullets[i].Faction == BulletFaction.Player
                    && bullets[i].Id > previousId)
                    result.Add(bullets[i].Id);
            return result;
        }

        static List<int> PlayerLaserIdsAfter(
            IReadOnlyList<LaserState> lasers,
            int previousId)
        {
            var result = new List<int>();
            for (int i = 0; i < lasers.Count; i++)
                if (lasers[i].SourceKind == LaserSourceKind.Player
                    && lasers[i].Id > previousId)
                    result.Add(lasers[i].Id);
            return result;
        }

        static BulletState FindBullet(
            IReadOnlyList<BulletState> bullets,
            int id)
        {
            for (int i = 0; i < bullets.Count; i++)
                if (bullets[i].Id == id)
                    return bullets[i];
            Assert.Fail($"Player bullet {id} disappeared before direction check.");
            return default;
        }

        static LaserState FindLaser(
            IReadOnlyList<LaserState> lasers,
            int id)
        {
            for (int i = 0; i < lasers.Count; i++)
                if (lasers[i].Id == id)
                    return lasers[i];
            Assert.Fail($"Player laser {id} was not found.");
            return default;
        }

        static PowerUpWeaponMode PowerUpWeaponModeFor(PowerUpSlot slot)
        {
            switch (slot)
            {
                case PowerUpSlot.Double: return PowerUpWeaponMode.Double;
                case PowerUpSlot.Laser: return PowerUpWeaponMode.Laser;
                case PowerUpSlot.Triple: return PowerUpWeaponMode.Triple;
                default: throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(
                TestKit.FindRepositoryRoot(),
                "GameData");
            return GameDataParser.Parse(
                Read(gameData, "enemies.json"),
                Read(gameData, "weapons.json"),
                Read(gameData, "waves.json"),
                Read(gameData, "rewards.json"),
                Read(gameData, "ships.json"),
                Read(gameData, "scoring.json"));
        }

        static string Read(string directory, string fileName)
        {
            return File.ReadAllText(Path.Combine(directory, fileName));
        }
    }
}
