using System;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req087BossVocabularyTests
    {
        [TestCase(BossProjectileKind.Normal, BulletKind.EnemyShot)]
        [TestCase(BossProjectileKind.Heavy, BulletKind.Heavy)]
        [TestCase(BossProjectileKind.Splitter, BulletKind.Splitter)]
        [TestCase(BossProjectileKind.Mine, BulletKind.Mine)]
        public void PhaseProjectileKindIsObservable(
            BossProjectileKind projectileKind,
            BulletKind expected)
        {
            BossPhase phase = Phase(projectileKind);
            BattleSim sim = CreateSim(phase, 16, 8, 8);

            StepUntil(sim, battle => CountEnemyBullets(battle) > 0);

            Assert.AreEqual(expected, FirstEnemyBullet(sim).Kind);
            Assert.AreEqual(
                expected == BulletKind.Heavy ? 250 : 100,
                FirstEnemyBullet(sim).CollisionScalePercent);
        }

        [Test]
        public void SplitterFliesForIntegerTicksThenSplitsIntoThreeLutBranches()
        {
            BattleSim sim = CreateSim(
                Phase(BossProjectileKind.Splitter, splitAfterTicks: 3),
                16,
                8,
                8);
            StepUntil(sim, battle => FirstEnemyBullet(battle).Kind
                == BulletKind.Splitter);
            BulletState parent = FirstEnemyBullet(sim);

            Step(sim, 2);
            Assert.AreEqual(BulletKind.Splitter, FirstEnemyBullet(sim).Kind);
            Step(sim, 2);

            Assert.AreEqual(3, CountEnemyBullets(sim));
            Assert.AreEqual(BulletKind.EnemyShot, FirstEnemyBullet(sim).Kind);
            Assert.AreNotEqual(
                EnemyBullet(sim, 0).Y,
                EnemyBullet(sim, 2).Y);
            Assert.AreNotEqual(parent.Id, FirstEnemyBullet(sim).Id);
        }

        [Test]
        public void MineStopsTelegraphsThenAcceleratesTowardCurrentPlayer()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Mine,
                mineTravelTicks: 2,
                mineTelegraphTicks: 2,
                mineAccelerationNumerator: 4);
            BattleSim sim = CreateSim(phase, 16, 8, 8);
            StepUntil(sim, battle => FirstEnemyBullet(battle).Kind
                == BulletKind.Mine);

            Step(sim, 2);
            BulletState stopped = FirstEnemyBullet(sim);
            Step(sim, 1);
            Assert.AreEqual(stopped.X, FirstEnemyBullet(sim).X);
            Assert.AreEqual(stopped.Y, FirstEnemyBullet(sim).Y);
            Step(sim, 2);

            Assert.Less(FirstEnemyBullet(sim).X, stopped.X);
        }

        [Test]
        public void SplitChildrenRespectEnemyBulletBudgetAndEmitCapacityEvent()
        {
            BattleSim sim = CreateSim(
                Phase(BossProjectileKind.Splitter, splitAfterTicks: 1),
                2,
                8,
                8);
            bool capacity = false;
            StepUntil(sim, battle =>
            {
                capacity |= HasEvent(
                    battle.EventsThisTick,
                    SimEventType.EnemyBulletCapacityExceeded);
                return capacity;
            });

            Assert.AreEqual(2, CountEnemyBullets(sim));
            Assert.IsTrue(capacity);
        }

        [TestCase(BossProjectileKind.Heavy)]
        [TestCase(BossProjectileKind.Splitter)]
        [TestCase(BossProjectileKind.Mine)]
        public void EveryPointProjectileRespectsZeroEnemyBulletBudget(
            BossProjectileKind kind)
        {
            BattleSim sim = CreateSim(Phase(kind), 0, 8, 8);
            StepUntil(
                sim,
                battle => HasEvent(
                    battle.EventsThisTick,
                    SimEventType.EnemyBulletCapacityExceeded));

            Assert.AreEqual(0, CountEnemyBullets(sim));
        }

        [Test]
        public void BossLaserUsesBossSourceAndHardLaserCap()
        {
            LaserAttackDefinition laser = Laser(-10, 0, -100, 0);
            BossPhase phase = Phase(
                BossProjectileKind.BossLaser,
                laser: laser);
            BattleSim accepted = CreateSim(phase, 8, 1, 8);
            StepUntil(accepted, battle => battle.Lasers.Count > 0);
            Assert.AreEqual(LaserSourceKind.Boss, accepted.Lasers[0].SourceKind);
            Assert.AreEqual(accepted.Boss.Id, accepted.Lasers[0].SourceEntityId);

            BattleSim rejected = CreateSim(phase, 8, 0, 8);
            StepUntil(
                rejected,
                battle => HasEvent(
                    battle.EventsThisTick,
                    SimEventType.LaserCapacityExceeded));
            Assert.AreEqual(0, rejected.Lasers.Count);
        }

        [Test]
        public void ScrapThrowCreatesBreakableParabolicObstacle()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Heavy,
                signature: BossSignaturePattern.ScrapThrow,
                signatureObstacleHp: 7,
                signatureGravityNumerator: 2);
            BattleSim sim = CreateSim(phase, 16, 8, 1);
            StepUntil(sim, battle => battle.Obstacles.Count > 0);
            ObstacleState spawned = sim.Obstacles[0];
            Assert.AreEqual(ObstacleType.Breakable, spawned.Type);
            Assert.AreEqual(7, spawned.Hp);
            Step(sim, 2);
            Assert.Less(sim.Obstacles[0].X, spawned.X);
            Assert.AreNotEqual(spawned.Y, sim.Obstacles[0].Y);
        }

        [Test]
        public void BroodCreatesWeakHomingLarvaeAndTentacle()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Normal,
                signature: BossSignaturePattern.Brood,
                signatureSpawnEnemyId: "hive_tentacle",
                signatureHomingTurn: 1);
            BattleSim sim = CreateSim(phase, 16, 8, 8);
            StepUntil(
                sim,
                battle => CountEnemyBullets(battle) > 0
                    && ContainsEnemy(battle, "hive_tentacle"));

            Assert.AreEqual(
                BossSignaturePattern.Brood,
                FirstEnemyBullet(sim).SignaturePattern);
            Assert.IsTrue(ContainsEnemy(sim, "hive_tentacle"));
        }

        [Test]
        public void LaserGridSynchronizesTwoBossLasersAndReportsTruncation()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Heavy,
                signature: BossSignaturePattern.LaserGrid,
                laser: Laser(-10, 20, -100, 20));
            BattleSim full = CreateSim(phase, 16, 2, 8);
            StepUntil(full, battle => battle.Lasers.Count == 2);
            Assert.AreEqual(-full.Lasers[0].StartY, full.Lasers[1].StartY);

            BattleSim capped = CreateSim(phase, 16, 1, 8);
            StepUntil(
                capped,
                battle => HasEvent(
                    battle.EventsThisTick,
                    SimEventType.LaserCapacityExceeded));
            Assert.AreEqual(1, capped.Lasers.Count);
        }

        [Test]
        public void LightningLocksVerticalBossLaserToPlayerLane()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Normal,
                signature: BossSignaturePattern.Lightning,
                laser: Laser(0, -100, 0, 100));
            BattleSim sim = CreateSim(phase, 16, 8, 8);
            StepUntil(sim, battle => battle.Lasers.Count > 0);

            Assert.AreEqual(sim.PlayerX, sim.Lasers[0].StartX);
            Assert.AreEqual(sim.Lasers[0].StartX, sim.Lasers[0].EndX);
            Assert.AreEqual(LaserSourceKind.Boss, sim.Lasers[0].SourceKind);
        }

        [Test]
        public void PrismCoreCreatesTwoRotatingBeamsPlusRingVolley()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Normal,
                BossFirePattern.Radial,
                ways: 4,
                signature: BossSignaturePattern.PrismCore,
                laser: Laser(0, 0, -100, 0));
            BattleSim sim = CreateSim(phase, 16, 2, 8);
            StepUntil(
                sim,
                battle => battle.Lasers.Count == 2
                    && CountEnemyBullets(battle) == 4);

            Assert.AreEqual(2, sim.Lasers.Count);
            Assert.AreEqual(4, CountEnemyBullets(sim));
            // 두 빔이 **서로 반대 방향**으로 나간다. 예전에는 끝점이 보스를 중심으로
            // 정확히 대칭인지를 봤는데, 이제 빔이 화면 끝까지 뻗으므로(2026-08-04)
            // 보스가 중앙에서 벗어나면 좌우 거리가 달라 대칭이 깨진다. 지켜야 할
            // 것은 좌표가 아니라 "양쪽으로 쏜다"는 성질이다.
            long left = (long)sim.Lasers[0].EndX - sim.Lasers[0].StartX;
            long right = (long)sim.Lasers[1].EndX - sim.Lasers[1].StartX;
            Assert.AreNotEqual(
                Math.Sign(left),
                Math.Sign(right),
                "두 빔이 같은 쪽으로 나간다 — 좌우 한 쌍이어야 한다.");
            Assert.GreaterOrEqual(
                Math.Abs(left),
                SimSpace.PlayfieldHalfWidthSubUnits - Math.Abs(sim.Boss.X),
                "왼쪽 빔이 화면 끝까지 닿지 않는다.");
            Assert.GreaterOrEqual(
                Math.Abs(right),
                SimSpace.PlayfieldHalfWidthSubUnits - Math.Abs(sim.Boss.X),
                "오른쪽 빔이 화면 끝까지 닿지 않는다.");
        }

        [Test]
        public void TelegraphCarriesProjectileSignatureAndColorClass()
        {
            BossPhase phase = Phase(
                BossProjectileKind.Heavy,
                BossFirePattern.Burst,
                signature: BossSignaturePattern.LaserGrid,
                telegraphTicks: 2,
                laser: Laser(-10, 20, -100, 20));
            BattleSim sim = CreateSim(phase, 16, 2, 8);
            SimEvent telegraph = default;
            StepUntil(sim, battle => TryFindEvent(
                battle.EventsThisTick,
                SimEventType.BossAttackTelegraphed,
                out telegraph));

            Assert.AreEqual(BulletKind.Heavy, telegraph.BulletKind);
            Assert.AreEqual(
                BossSignaturePattern.LaserGrid,
                telegraph.SignaturePattern);
            Assert.AreEqual(BossTelegraphKind.Laser, telegraph.TelegraphKind);
        }

        [TestCase(BossSignaturePattern.ScrapThrow)]
        [TestCase(BossSignaturePattern.Brood)]
        [TestCase(BossSignaturePattern.LaserGrid)]
        [TestCase(BossSignaturePattern.Lightning)]
        [TestCase(BossSignaturePattern.PrismCore)]
        public void EverySignatureIsDeterministicForSameSeed(
            BossSignaturePattern signature)
        {
            BossPhase phase = SignaturePhase(signature);
            BattleSim first = CreateSim(phase, 24, 4, 8, 0x8700UL);
            BattleSim second = CreateSim(phase, 24, 4, 8, 0x8700UL);
            var firstHash = new DeterminismAuditHasher();
            var secondHash = new DeterminismAuditHasher();

            for (int tick = 0; tick < 180; tick++)
            {
                InputCommand input = InputCommand.None;
                first.Step(in input);
                second.Step(in input);
                firstHash.FoldBattleState(first);
                secondHash.FoldBattleState(second);
            }

            Assert.AreEqual(firstHash.Hash, secondHash.Hash);
        }

        [TestCase(BossProjectileKind.Normal)]
        [TestCase(BossProjectileKind.Heavy)]
        [TestCase(BossProjectileKind.Splitter)]
        [TestCase(BossProjectileKind.Mine)]
        [TestCase(BossProjectileKind.BossLaser)]
        public void EveryProjectileKindIsDeterministicForSameSeed(
            BossProjectileKind kind)
        {
            BossPhase phase = kind == BossProjectileKind.BossLaser
                ? Phase(kind, laser: Laser(-10, 0, -100, 0))
                : Phase(kind);
            BattleSim first = CreateSim(phase, 24, 4, 8, 0x8710UL);
            BattleSim second = CreateSim(phase, 24, 4, 8, 0x8710UL);
            var firstHash = new DeterminismAuditHasher();
            var secondHash = new DeterminismAuditHasher();

            for (int tick = 0; tick < 180; tick++)
            {
                InputCommand input = InputCommand.None;
                first.Step(in input);
                second.Step(in input);
                firstHash.FoldBattleState(first);
                secondHash.FoldBattleState(second);
            }

            Assert.AreEqual(firstHash.Hash, secondHash.Hash);
        }

        static BossPhase SignaturePhase(BossSignaturePattern signature)
        {
            switch (signature)
            {
                case BossSignaturePattern.ScrapThrow:
                    return Phase(
                        BossProjectileKind.Heavy,
                        signature: signature,
                        signatureObstacleHp: 5,
                        signatureGravityNumerator: 1);
                case BossSignaturePattern.Brood:
                    return Phase(
                        BossProjectileKind.Normal,
                        signature: signature,
                        signatureSpawnEnemyId: "hive_tentacle",
                        signatureHomingTurn: 1);
                case BossSignaturePattern.LaserGrid:
                    return Phase(
                        BossProjectileKind.Heavy,
                        signature: signature,
                        laser: Laser(-10, 20, -100, 20));
                case BossSignaturePattern.Lightning:
                    return Phase(
                        BossProjectileKind.Normal,
                        signature: signature,
                        laser: Laser(0, -100, 0, 100));
                case BossSignaturePattern.PrismCore:
                    return Phase(
                        BossProjectileKind.Normal,
                        BossFirePattern.Radial,
                        4,
                        signature: signature,
                        laser: Laser(0, 0, -100, 0));
                default:
                    throw new ArgumentOutOfRangeException(nameof(signature));
            }
        }

        static BossPhase Phase(
            BossProjectileKind projectileKind,
            BossFirePattern firePattern = BossFirePattern.Aimed,
            int ways = 1,
            int splitAfterTicks = 0,
            int mineTravelTicks = 0,
            int mineTelegraphTicks = 0,
            int mineAccelerationNumerator = 0,
            BossSignaturePattern signature = BossSignaturePattern.None,
            string signatureSpawnEnemyId = null,
            int signatureObstacleHp = 0,
            int signatureGravityNumerator = 0,
            int signatureHomingTurn = 0,
            int telegraphTicks = 0,
            LaserAttackDefinition laser = null)
        {
            if (projectileKind == BossProjectileKind.Splitter
                && splitAfterTicks == 0)
                splitAfterTicks = 20;
            if (projectileKind == BossProjectileKind.Mine)
            {
                if (mineTravelTicks == 0) mineTravelTicks = 10;
                if (mineTelegraphTicks == 0) mineTelegraphTicks = 10;
                if (mineAccelerationNumerator == 0)
                    mineAccelerationNumerator = 2;
            }
            return new BossPhase(
                20,
                ways,
                8,
                1,
                BossMovementPattern.Stationary,
                0,
                1,
                1,
                BossPartVulnerability.Legacy,
                0,
                telegraphTicks,
                firePattern,
                projectileKind,
                splitAfterTicks,
                mineTravelTicks,
                mineTelegraphTicks,
                mineAccelerationNumerator,
                1,
                signature,
                signatureSpawnEnemyId,
                signatureObstacleHp,
                signatureGravityNumerator,
                1,
                signatureHomingTurn,
                laser);
        }

        static LaserAttackDefinition Laser(
            int startX,
            int startY,
            int endX,
            int endY)
        {
            return new LaserAttackDefinition(
                20,
                2,
                2,
                2,
                2,
                startX,
                startY,
                endX,
                endY,
                1,
                3,
                1);
        }

        static BattleSim CreateSim(
            BossPhase phase,
            int maxEnemyBullets,
            int maxLasers,
            int maxObstacles,
            ulong seed = 0x8701UL)
        {
            BattleSimConfig config = BattleSimConfig.CreateDefault();
            config.PlayerSpeedPerTick = 0;
            config.PlayerBulletSpeedPerTick = 0;
            config.MaxBullets = 0;
            config.MaxEnemyBullets = maxEnemyBullets;
            config.MaxLasers = maxLasers;
            config.MaxObstacles = maxObstacles;
            config.MaxEnemies = 16;
            config.PlayerMinX = -100;
            config.PlayerMaxX = 100;
            config.PlayerMinY = -100;
            config.PlayerMaxY = 100;
            config.PlayerSpawnX = -70;
            config.PlayerSpawnY = 0;
            config.BulletDespawnX = 10_000;
            config.EnemyDespawnX = -10_000;
            config.ScrollSpeedNumerator = 0;
            config.ScrollSpeedDenominator = 1;
            config.EnemyBulletDamage = 0;
            config.PlayerHalfWidth = 0;
            config.PlayerHalfHeight = 0;
            config.EnemyBulletHalfWidth = 0;
            config.EnemyBulletHalfHeight = 0;
            var segment = new StageSegment(
                "entry",
                1,
                Array.Empty<SpawnEvent>(),
                1,
                1,
                new[] { 1 });
            var plan = new StagePlan(
                new[] { segment },
                "boss",
                1,
                1,
                1,
                9999,
                1,
                1,
                50,
                new[] { phase });
            return new BattleSim(
                config,
                new Rng(seed),
                plan,
                Content(),
                PowerUpGauge.CreateDefault());
        }

        static BattleContent Content()
        {
            var tentacle = new EnemyDefinition(
                "hive_tentacle",
                5,
                0,
                EnemyMovePattern.Static,
                0,
                1,
                0,
                0,
                0,
                0,
                1);
            var weapon = new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0);
            return new BattleContent(
                new[] { tentacle },
                new[] { weapon },
                weapon.Id);
        }

        static void StepUntil(BattleSim sim, Func<BattleSim, bool> predicate)
        {
            for (int tick = 0; tick < 512; tick++)
            {
                InputCommand input = InputCommand.None;
                sim.Step(in input);
                if (predicate(sim))
                    return;
            }
            Assert.Fail("Condition was not reached within the tick budget.");
        }

        static void Step(BattleSim sim, int count)
        {
            for (int i = 0; i < count; i++)
            {
                InputCommand input = InputCommand.None;
                sim.Step(in input);
            }
        }

        static int CountEnemyBullets(BattleSim sim)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction == BulletFaction.Enemy)
                    count++;
            return count;
        }

        static BulletState FirstEnemyBullet(BattleSim sim)
        {
            return EnemyBullet(sim, 0);
        }

        static BulletState EnemyBullet(BattleSim sim, int requested)
        {
            int found = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
            {
                if (sim.Bullets[i].Faction != BulletFaction.Enemy)
                    continue;
                if (found++ == requested)
                    return sim.Bullets[i];
            }
            return default;
        }

        static bool ContainsEnemy(BattleSim sim, string id)
        {
            for (int i = 0; i < sim.Enemies.Count; i++)
                if (string.Equals(
                    sim.Enemies[i].DefinitionId,
                    id,
                    StringComparison.Ordinal))
                    return true;
            return false;
        }

        static bool HasEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            return TryFindEvent(events, type, out _);
        }

        static bool TryFindEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type,
            out SimEvent found)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                {
                    found = events[i];
                    return true;
                }
            found = default;
            return false;
        }
    }
}
