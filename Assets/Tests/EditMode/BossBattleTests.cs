using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>REQ-007: 적탄, 보스 페이즈 상태기계, 보상 3택 흐름 검증.</summary>
    [TestFixture]
    public class BossBattleTests
    {
        [Test]
        public void BossSpawnsAfterSegmentsApproachesAndHolds()
        {
            var sim = CreateBossSim(
                bossMaxHp: 100,
                holdX: 400,
                phases: Phase(interval: 1, ways: 1, speed: 64),
                weaponDamage: 100,
                bulletSpeed: 256);
            InputCommand none = InputCommand.None;
            var fire = new InputCommand(0, 0, true);

            for (int tick = 0; tick < 200 && !sim.BossActive; tick++)
                sim.Step(in none);
            Assert.IsTrue(sim.BossActive);
            Assert.IsTrue(sim.BossEntering);

            Assert.IsTrue(sim.BossActive);
            Assert.AreEqual(1, sim.EventsThisTick.Length);
            Assert.AreEqual(SimEventType.BossSpawned, sim.EventsThisTick[0].Type);
            int entryX = sim.Boss.X;
            Assert.Greater(
                entryX - 256,
                SimSpace.PlayfieldHalfWidthSubUnits);

            sim.Step(in fire);
            Assert.Less(sim.Boss.X, entryX);

            while (sim.BossEntering)
            {
                Assert.AreEqual(100, sim.Boss.Hp);
                for (int i = 0; i < sim.Bullets.Count; i++)
                {
                    Assert.AreNotEqual(
                        BulletFaction.Enemy,
                        sim.Bullets[i].Faction);
                }
                sim.Step(in fire);
            }
            Assert.AreEqual(400, sim.Boss.X);           // holdX 정지
            Assert.IsFalse(sim.BossEntering);
        }

        [Test]
        public void BossWaitsForNaturalEnemyExitAndPostClearDelay()
        {
            EnemyDefinition linger = new EnemyDefinition(
                "linger", 10, 0, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 64);
            BattleContent content = Content(linger);
            StagePlan plan = new StagePlan(
                new[]
                {
                    Segment(
                        "closing",
                        200,
                        new SpawnEvent(1, linger.Id, 1000, 500),
                        new SpawnEvent(150, linger.Id, 1000, 500))
                },
                "boss", 1, 1, 1,
                100, 256, 256, 300,
                Phase(interval: 999, ways: 1));
            BattleSim sim = new BattleSim(
                CreateConfig(),
                new Rng(0x82UL),
                plan,
                content,
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            int previousEnemyX = int.MaxValue;
            int fieldClearTick = -1;
            for (int tick = 0; tick < 250 && fieldClearTick < 0; tick++)
            {
                sim.Step(in none);
                Assert.IsFalse(sim.BossActive);
                Assert.LessOrEqual(sim.Enemies.Count, 1,
                    "the spawn inside the 90-tick cleanup lead must be suppressed");
                if (sim.Enemies.Count == 0)
                {
                    fieldClearTick = sim.Tick;
                    break;
                }
                if (sim.Tick >= 110)
                {
                    Assert.Less(sim.Enemies[0].X, previousEnemyX,
                        "the survivor must move left until it crosses the despawn boundary");
                }
                previousEnemyX = sim.Enemies[0].X;
            }

            Assert.GreaterOrEqual(fieldClearTick, 0);
            for (int delay = 1;
                delay < BattleSim.BossPostClearDelayTicks;
                delay++)
            {
                sim.Step(in none);
                Assert.IsFalse(sim.BossActive);
            }
            sim.Step(in none);
            Assert.IsTrue(sim.BossActive,
                $"boss inactive at tick {sim.Tick}; field cleared at {fieldClearTick}");
            Assert.IsTrue(HasEvent(
                sim.EventsThisTick,
                SimEventType.BossSpawned));
        }

        [Test]
        public void BossFiresAimedSpreadThatCanHitPlayer()
        {
            var sim = CreateBossSim(
                bossMaxHp: 100, holdX: 300, phases: Phase(interval: 4, ways: 3, speed: 64));
            InputCommand none = InputCommand.None;

            bool sawEnemyBullet = false;
            bool playerWasHit = false;
            for (int i = 0; i < 400 && !playerWasHit; i++)
            {
                sim.Step(in none);
                for (int b = 0; b < sim.Bullets.Count; b++)
                    if (sim.Bullets[b].Faction == BulletFaction.Enemy)
                        sawEnemyBullet = true;
                ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
                for (int e = 0; e < events.Length; e++)
                    if (events[e].Type == SimEventType.PlayerHit)
                        playerWasHit = true;
            }

            Assert.IsTrue(sawEnemyBullet, "적탄이 스폰되어야 한다");
            Assert.IsTrue(playerWasHit, "조준탄이 정지한 플레이어를 맞혀야 한다");
        }

        [Test]
        public void BossPhaseChangesAtHpSplitAndDeathClearsStage()
        {
            var sim = CreateBossSim(
                bossMaxHp: 20,
                holdX: 300,
                phases: new[]
                {
                    new BossPhase(999, 1, 64, 1),
                    new BossPhase(999, 5, 64, 1)
                },
                weaponDamage: 10,
                bulletSpeed: 50);
            var fire = new InputCommand(0, 0, true);

            var seen = new List<SimEventType>();
            for (int i = 0; i < 300; i++)
            {
                sim.Step(in fire);
                ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
                for (int e = 0; e < events.Length; e++)
                    seen.Add(events[e].Type);
                if (!sim.BossActive && seen.Contains(SimEventType.StageCleared))
                    break;
            }

            Assert.Contains(SimEventType.BossSpawned, seen);
            Assert.Contains(SimEventType.EnemyHit, seen);
            Assert.Contains(SimEventType.BossPhaseChanged, seen);   // 20 → 10 경계
            Assert.Contains(SimEventType.EnemyKilled, seen);
            Assert.Contains(SimEventType.StageCleared, seen);
            Assert.IsFalse(sim.BossActive);
            Assert.IsTrue(sim.BossDefeated);
            Assert.AreEqual(40L, sim.Score);
            Assert.GreaterOrEqual(sim.Statistics.ShotsFired, 2L);
            Assert.AreEqual(2L, sim.Statistics.ShotsHit);
            Assert.AreEqual(1L, sim.Statistics.Kills);
        }

        [Test]
        public void ThreeBossPhasesChangeAtExactSixtySixAndThirtyThreePercentBoundaries()
        {
            var phases = new[]
            {
                new BossPhase(
                    999,
                    1,
                    32,
                    1,
                    BossMovementPattern.Stationary,
                    0,
                    1,
                    1,
                    BossPartVulnerability.CoreOnly),
                new BossPhase(
                    999,
                    2,
                    48,
                    1,
                    BossMovementPattern.VerticalSine,
                    128,
                    1,
                    8,
                    BossPartVulnerability.All),
                new BossPhase(
                    999,
                    4,
                    96,
                    1,
                    BossMovementPattern.VerticalSine,
                    256,
                    1,
                    4,
                    BossPartVulnerability.All)
            };
            BattleSim sim = CreateBossSim(
                bossMaxHp: 3,
                holdX: 300,
                phases: phases,
                weaponDamage: 1,
                bulletSpeed: 50);
            InputCommand none = InputCommand.None;
            for (int tick = 0;
                tick < 300
                    && (!sim.BossActive || sim.Boss.X != 300);
                tick++)
                sim.Step(in none);

            int firstTransition = FireOneShotUntilPhaseChange(sim);
            AssertAll(() =>
            {
                Assert.AreEqual(1, firstTransition);
                Assert.AreEqual(2, sim.Boss.Hp);
                Assert.AreEqual(1, sim.Boss.Phase);
                Assert.AreEqual(
                    BossMovementPattern.VerticalSine,
                    sim.Boss.MovementPattern);
                Assert.AreEqual(
                    BossPartVulnerability.All,
                    sim.Boss.PartVulnerability);
            });

            int secondTransition = FireOneShotUntilPhaseChange(sim);
            AssertAll(() =>
            {
                Assert.AreEqual(2, secondTransition);
                Assert.AreEqual(1, sim.Boss.Hp);
                Assert.AreEqual(2, sim.Boss.Phase);
                Assert.AreEqual(
                    BossMovementPattern.VerticalSine,
                    sim.Boss.MovementPattern);
                Assert.AreEqual(
                    BossPartVulnerability.All,
                    sim.Boss.PartVulnerability);
            });
        }

        [Test]
        public void TimedMovementPhaseTransitionPreservesPositionDelta()
        {
            var phases = new[]
            {
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.VerticalSine,
                    256, 1, 64,
                    BossPartVulnerability.All,
                    durationTicks: 12),
                new BossPhase(
                    999, 1, 0, 1,
                    BossMovementPattern.VerticalSine,
                    384, 1, 80,
                    BossPartVulnerability.All,
                    durationTicks: 12)
            };
            BattleSim sim = CreateBossSim(
                bossMaxHp: 100,
                holdX: 300,
                phases: phases);
            InputCommand none = InputCommand.None;
            for (int tick = 0;
                tick < 300
                    && (!sim.BossActive || sim.Boss.X != 300);
                tick++)
                sim.Step(in none);

            int previousY = sim.Boss.Y;
            sim.Step(in none);
            int previousDelta = sim.Boss.Y - previousY;
            bool transitioned = false;
            for (int tick = 0; tick < 24 && !transitioned; tick++)
            {
                previousY = sim.Boss.Y;
                sim.Step(in none);
                int currentDelta = sim.Boss.Y - previousY;
                if (HasEvent(
                        sim.EventsThisTick,
                        SimEventType.BossPhaseChanged))
                {
                    transitioned = true;
                    Assert.AreEqual(previousDelta, currentDelta);
                    Assert.LessOrEqual(Math.Abs(currentDelta), 32);
                    int transitionY = sim.Boss.Y;
                    sim.Step(in none);
                    int nextDelta = sim.Boss.Y - transitionY;
                    Assert.LessOrEqual(
                        Math.Abs(nextDelta - currentDelta),
                        32,
                        "the new sine phase must preserve velocity near the transition");
                }
                previousDelta = currentDelta;
            }
            Assert.IsTrue(transitioned);
        }

        [Test]
        public void TurretDefinitionsFireAtConfiguredInterval()
        {
            EnemyDefinition turret = new EnemyDefinition(
                "turret", "Turret", 30, 1, 0, EnemyMovePattern.Static,
                0, 1, 10, 128, 128, 0, 0, 1, 64);
            BattleContent content = Content(turret);
            StagePlan plan = new StagePlan(
                new[] { Segment("t", 600, new SpawnEvent(0, turret.Id, 500, 200)) },
                "boss", 1, 1, 1);
            var sim = new BattleSim(
                CreateConfig(), new Rng(7UL), plan, content, PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            int enemyBullets = 0;
            for (int i = 0; i < 25; i++)
            {
                sim.Step(in none);
                enemyBullets = 0;
                for (int b = 0; b < sim.Bullets.Count; b++)
                    if (sim.Bullets[b].Faction == BulletFaction.Enemy)
                        enemyBullets++;
            }

            Assert.GreaterOrEqual(enemyBullets, 1, "터렛은 fireIntervalTicks마다 사격해야 한다");
        }

        [Test]
        public void ExtremeAimedVectorDoesNotOverflow()
        {
            EnemyDefinition turret = new EnemyDefinition(
                "extreme", "Extreme", 30, 0, 0, EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0, 0, 1, 64);
            BattleContent content = Content(turret);
            StagePlan plan = new StagePlan(
                new[]
                {
                    Segment(
                        "extreme",
                        10,
                        new SpawnEvent(0, turret.Id, int.MinValue, int.MinValue))
                },
                "boss", 1, 1, 1);
            BattleSimConfig config = CreateConfig();
            config.PlayerMinX = int.MaxValue;
            config.PlayerMaxX = int.MaxValue;
            config.PlayerMinY = int.MaxValue;
            config.PlayerMaxY = int.MaxValue;
            config.PlayerSpawnX = int.MaxValue;
            config.PlayerSpawnY = int.MaxValue;
            config.BulletDespawnX = int.MaxValue;
            config.EnemyDespawnX = int.MinValue;
            var sim = new BattleSim(
                config, new Rng(17UL), plan, content, PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            sim.Step(in none);

            Assert.AreEqual(1, CountBullets(sim, BulletFaction.Enemy));
        }

        [Test]
        public void EnemyBulletBudgetDoesNotConsumePlayerBulletBudget()
        {
            EnemyDefinition turret = new EnemyDefinition(
                "turret", "Turret", 30, 0, 0, EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0, 0, 1, 64);
            BattleContent content = Content(turret);
            StagePlan plan = new StagePlan(
                new[] { Segment("t", 10, new SpawnEvent(0, turret.Id, 500, 200)) },
                "boss", 1, 1, 1);
            BattleSimConfig config = CreateConfig();
            config.MaxBullets = 1;
            config.MaxEnemyBullets = 1;
            var sim = new BattleSim(
                config, new Rng(19UL), plan, content, PowerUpGauge.CreateDefault());
            var fire = new InputCommand(0, 0, true);

            sim.Step(in fire);

            Assert.AreEqual(1, CountBullets(sim, BulletFaction.Enemy));
            Assert.AreEqual(1, CountBullets(sim, BulletFaction.Player));
        }

        [Test]
        public void EnemyFireAtBudgetSkipsAdditionalShot()
        {
            EnemyDefinition turret = new EnemyDefinition(
                "turret", "Turret", 30, 0, 0, EnemyMovePattern.Static,
                0, 1, 1, 0, 0, 0, 0, 1, 64);
            BattleContent content = Content(turret);
            StagePlan plan = new StagePlan(
                new[] { Segment("t", 10, new SpawnEvent(0, turret.Id, 500, 200)) },
                "boss", 1, 1, 1);
            BattleSimConfig config = CreateConfig();
            config.MaxEnemyBullets = 1;
            var sim = new BattleSim(
                config, new Rng(20UL), plan, content, PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            sim.Step(in none);
            int firstBulletId = sim.Bullets[0].Id;
            sim.Step(in none);

            Assert.AreEqual(1, CountBullets(sim, BulletFaction.Enemy));
            Assert.AreEqual(firstBulletId, sim.Bullets[0].Id);
        }

        [Test]
        public void EvenWayBossSpreadIsCenteredOnAimAxis()
        {
            BattleSimConfig config = CreateConfig();
            config.BulletDespawnX = 10000;
            config.MaxEnemyBullets = 2;
            int holdX = config.BulletDespawnX + 2 * SimSpace.SubUnitsPerWorldUnit;
            EnemyDefinition dummy = Dummy();
            StagePlan plan = new StagePlan(
                new[] { Segment("intro", 1) },
                "even_boss", 1, 1, 1,
                100, 256, 256, holdX, Phase(interval: 1, ways: 2, speed: 1024));
            var sim = new BattleSim(
                config,
                new Rng(23UL),
                plan,
                Content(dummy),
                PowerUpGauge.CreateDefault());
            InputCommand none = InputCommand.None;

            for (int tick = 0; tick < 200 && !sim.BossActive; tick++)
                sim.Step(in none);
            sim.Step(in none); // y=0에서 첫 2-way 발사
            sim.Step(in none); // 첫 볼리 1틱 전진

            var enemyBulletYs = new List<int>();
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction == BulletFaction.Enemy)
                    enemyBulletYs.Add(sim.Bullets[i].Y);

            Assert.AreEqual(2, enemyBulletYs.Count);
            Assert.AreNotEqual(0, enemyBulletYs[0]);
            Assert.AreEqual(-enemyBulletYs[0], enemyBulletYs[1]);
        }

        [Test]
        public void RunManagerAwaitsRewardAfterBossAndResumesOnChoice()
        {
            RunManager run = CreateBossRun(seed: 42UL);
            CompleteBoss(run);

            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);
            Assert.AreEqual(
                RewardSelectionKind.Main,
                run.RewardSelectionKind);
            Assert.AreEqual(1, run.StageIndex);
            Assert.AreEqual(1, run.Statistics.StagesCleared);
            Assert.GreaterOrEqual(run.Statistics.ShotsHit, 2L);
            Assert.GreaterOrEqual(run.Statistics.Kills, 2L);

            run.ChooseReward(0);
            Assert.AreEqual(
                RunState.AwaitingContract,
                run.State);
            Assert.IsTrue(run.ChooseContract(0));
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(2, run.StageIndex);
            Assert.AreEqual(0, run.RewardOptions.Count);
            Assert.AreEqual(1, run.Statistics.StagesCleared);
            Assert.GreaterOrEqual(run.Statistics.ShotsHit, 2L);
            Assert.GreaterOrEqual(run.Statistics.Kills, 2L);
        }

        [Test]
        public void RewardOptionsAreDeterministicPerSeedAndStage()
        {
            RunManager first = CreateBossRun(seed: 99UL);
            RunManager second = CreateBossRun(seed: 99UL);
            CompleteBoss(first);
            CompleteBoss(second);

            Assert.AreEqual(RunState.AwaitingReward, first.State);
            Assert.AreEqual(RunState.AwaitingReward, second.State);
            for (int i = 0; i < first.RewardOptions.Count; i++)
            {
                Assert.AreEqual(first.RewardOptions[i].Id, second.RewardOptions[i].Id);
                Assert.AreEqual(first.RewardOptions[i].Type, second.RewardOptions[i].Type);
                Assert.AreEqual(first.RewardOptions[i].Slot, second.RewardOptions[i].Slot);
                Assert.AreEqual(first.RewardOptions[i].Amount, second.RewardOptions[i].Amount);
            }
        }

        [Test]
        public void InjectedRewardsFilterByInclusiveStageRange()
        {
            RewardCatalog rewards = Catalog(
                Reward("early_a", 1, 1),
                Reward("early_b", 1, 1),
                Reward("early_c", 1, 1),
                Reward("late_a", 2, 9),
                Reward("late_b", 2, 9),
                Reward("late_c", 2, 9));
            RunManager run = CreateBossRun(seed: 42UL, rewards: rewards);

            CompleteBoss(run);

            Assert.AreEqual(3, run.RewardOptions.Count);
            for (int i = 0; i < run.RewardOptions.Count; i++)
                StringAssert.StartsWith("early_", run.RewardOptions[i].Id);
        }

        [Test]
        public void InjectedRewardsUseWeightsAndNeverRepeatAnEntry()
        {
            RewardCatalog rewards = Catalog(
                Reward("common_a", 1, 9),
                Reward("common_b", 1, 9),
                Reward("common_c", 1, 9),
                Reward("heavy", 1, 9, weight: 100));
            int heavySelections = 0;

            for (ulong seed = 0; seed < 64; seed++)
            {
                RunManager run = CreateBossRun(seed, rewards: rewards);
                CompleteBoss(run);

                bool foundHeavy = false;
                for (int i = 0; i < run.RewardOptions.Count; i++)
                {
                    if (run.RewardOptions[i].Id == "heavy")
                        foundHeavy = true;
                    for (int j = i + 1; j < run.RewardOptions.Count; j++)
                        Assert.AreNotEqual(
                            run.RewardOptions[i].Id,
                            run.RewardOptions[j].Id,
                            "가중 선택은 비복원이어야 한다");
                }
                if (foundHeavy)
                    heavySelections++;
            }

            Assert.GreaterOrEqual(
                heavySelections,
                60,
                "큰 weight의 보상이 균등 선택보다 뚜렷하게 자주 포함되어야 한다");
        }

        [Test]
        public void InjectedRewardsFillMissingEligibleEntriesWithFallbacks()
        {
            RewardCatalog rewards = Catalog(
                Reward("only_a", 1, 1),
                Reward("only_b", 1, 1),
                Reward("late", 2, 9));
            RunManager run = CreateBossRun(seed: 42UL, rewards: rewards);

            CompleteBoss(run);

            Assert.AreEqual(
                RunManager.MainRewardOptionCount,
                run.RewardOptions.Count);
            int fallbackCount = 0;
            for (int i = 0; i < run.RewardOptions.Count; i++)
            {
                if (run.RewardOptions[i].Id.StartsWith(
                        "fallback_",
                        StringComparison.Ordinal))
                    fallbackCount++;
            }
            Assert.AreEqual(1, fallbackCount);
        }

        [Test]
        public void RewardOptionsAreExposedThroughAnImmutableView()
        {
            RunManager run = CreateBossRun(seed: 42UL);
            CompleteBoss(run);

            Assert.IsFalse(run.RewardOptions is RewardOption[]);
            var options = (IList<RewardOption>)run.RewardOptions;
            Assert.IsTrue(options.IsReadOnly);
            Assert.Throws<NotSupportedException>(
                () => options[0] = new RewardOption(
                    RewardType.Capsules, PowerUpSlot.MainShot, 999));
        }

        [Test]
        public void LegacyRepairRewardRestoresShieldStockAndExpiresOnRestart()
        {
            RunManager run = null;
            int repairIndex = -1;
            for (ulong seed = 0; seed < 128 && repairIndex < 0; seed++)
            {
                RunManager candidate = CreateRepairRun(seed);
                CompleteBoss(candidate);
                for (int i = 0; i < candidate.RewardOptions.Count; i++)
                {
                    if (candidate.RewardOptions[i].Type != RewardType.RepairHp)
                        continue;
                    run = candidate;
                    repairIndex = i;
                    break;
                }
            }

            Assert.IsNotNull(run, "테스트 시드 범위에서 RepairHp 보상을 찾지 못했다");
            run.ChooseReward(repairIndex);
            Assert.AreEqual(1, run.Battle.ShieldStock);
            Assert.IsTrue(run.ChooseContract(0));

            InputCommand none = InputCommand.None;
            for (int i = 0;
                i < 20 && run.State != RunState.RunOver;
                i++)
                run.Step(in none);
            Assert.AreEqual(RunState.RunOver, run.State);

            run.Restart(999UL);
            Assert.AreEqual(0, run.Battle.ShieldStock);
        }

        [Test]
        public void LegacyPlansWithoutBossStillAdvanceByTicks()
        {
            RunManager run = CreateBossRun(seed: 5UL, bossMaxHp: 0);
            InputCommand none = InputCommand.None;

            for (int i = 0; i < 5; i++) run.Step(in none);
            Assert.AreEqual(1, run.BiomeIndex);
            Assert.AreEqual(2, run.RoomIndex);
            Assert.AreEqual(RunState.Playing, run.State);
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────────────

        static BossPhase[] Phase(int interval, int ways, int speed = 32)
        {
            return new[] { new BossPhase(interval, ways, speed, 1) };
        }

        static BattleSim CreateBossSim(
            int bossMaxHp,
            int holdX,
            IReadOnlyList<BossPhase> phases,
            int weaponDamage = 10,
            int bulletSpeed = 2)
        {
            EnemyDefinition dummy = Dummy();
            BattleContent content = Content(
                new WeaponDefinition("shot", weaponDamage, 1, bulletSpeed, 1, 8, 8), dummy);
            StagePlan plan = new StagePlan(
                new[] { Segment("intro", 5) },
                "test_boss", 1, 1, 1,
                bossMaxHp, 256, 256, holdX, phases);
            return new BattleSim(
                CreateConfig(), new Rng(11UL), plan, content, PowerUpGauge.CreateDefault());
        }

        static EnemyDefinition Dummy()
        {
            return new EnemyDefinition(
                "dummy", 1, 0, EnemyMovePattern.Static, 0, 1, 0, 0, 0, 0, 64);
        }

        static RunManager CreateBossRun(
            ulong seed,
            int bossMaxHp = 10,
            RewardCatalog rewards = null)
        {
            EnemyDefinition dummy = Dummy();
            BattleContent content = Content(
                new WeaponDefinition("shot", 10, 1, 50, 1, 8, 8), dummy);
            StagePlan plan = new StagePlan(
                new[] { Segment("intro", 5) },
                "test_boss", 1, 1, 1,
                bossMaxHp, 256, 256, 300, Phase(interval: 999, ways: 1));
            return new RunManager(
                seed,
                new FixedPlanGenerator(plan),
                CreateConfig(),
                content,
                PowerUpGauge.CreateDefault(),
                rewards);
        }

        static RewardCatalog Catalog(params RewardDefinition[] rewards)
        {
            return new RewardCatalog(RunManager.RewardOptionCount, rewards);
        }

        static RewardDefinition Reward(
            string id,
            int stageIndexMin,
            int stageIndexMax,
            int weight = 1)
        {
            return new RewardDefinition(
                id,
                RewardType.Capsules,
                PowerUpSlot.MainShot,
                1,
                weight,
                stageIndexMin,
                stageIndexMax);
        }

        static RunManager CreateRepairRun(ulong seed)
        {
            EnemyDefinition lethal = new EnemyDefinition(
                "lethal", 1, 100, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 64);
            BattleContent content = Content(
                new WeaponDefinition("shot", 10, 1, 50, 1, 8, 8), lethal);
            BattleSimConfig config = CreateConfig();
            config.StartingShieldStock = 0;
            config.PlayerHitInvulnerabilityTicks = 0;
            return new RunManager(
                seed,
                new RewardThenLethalGenerator(lethal.Id),
                config,
                content,
                PowerUpGauge.CreateDefault());
        }

        static void CompleteBoss(RunManager run)
        {
            var fire = new InputCommand(0, 0, true);
            for (int i = 0; i < 2000; i++)
            {
                if (run.State == RunState.AwaitingReward)
                {
                    if (run.RewardSelectionKind
                        == RewardSelectionKind.Main)
                        break;
                    Assert.AreEqual(
                        RewardSelectionKind.MidStage,
                        run.RewardSelectionKind);
                    run.ChooseReward(0);
                    continue;
                }
                run.Step(in fire);
            }
            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(
                RewardSelectionKind.Main,
                run.RewardSelectionKind);
        }

        static int CountBullets(BattleSim sim, BulletFaction faction)
        {
            int count = 0;
            for (int i = 0; i < sim.Bullets.Count; i++)
                if (sim.Bullets[i].Faction == faction)
                    count++;
            return count;
        }

        sealed class FixedPlanGenerator : IStageGenerator
        {
            readonly StagePlan _plan;
            public FixedPlanGenerator(StagePlan plan) { _plan = plan; }
            public StagePlan Generate(ulong seed, int stageIndex, int difficulty) => _plan;
        }

        sealed class RewardThenLethalGenerator : IStageGenerator
        {
            readonly string _lethalEnemyId;

            public RewardThenLethalGenerator(string lethalEnemyId)
            {
                _lethalEnemyId = lethalEnemyId;
            }

            public StagePlan Generate(ulong seed, int stageIndex, int difficulty)
            {
                if (stageIndex == 1)
                {
                    return new StagePlan(
                        new[] { Segment("intro", 1) },
                        "reward_boss", 1, 1, 1,
                        10, 256, 256, 300, Phase(interval: 999, ways: 1));
                }

                return new StagePlan(
                    new[]
                    {
                        Segment(
                            "lethal",
                            5,
                            new SpawnEvent(1, _lethalEnemyId, 0, 0),
                            new SpawnEvent(2, _lethalEnemyId, 0, 0))
                    },
                    "legacy", 1, 1, 1);
            }
        }

        static BattleContent Content(WeaponDefinition weapon, params EnemyDefinition[] enemies)
        {
            return new BattleContent(enemies, new[] { weapon }, weapon.Id);
        }

        static BattleContent Content(params EnemyDefinition[] enemies)
        {
            return Content(new WeaponDefinition("shot", 1, 1, 0, 1, 0, 0), enemies);
        }

        static StageSegment Segment(string id, int lengthTicks, params SpawnEvent[] spawns)
        {
            return new StageSegment(id, lengthTicks, spawns, 1, 1, new[] { 1 });
        }

        static int FireOneShotUntilPhaseChange(BattleSim sim)
        {
            int startingPhase = sim.Boss.Phase;
            var fire = new InputCommand(0, 0, true);
            InputCommand none = InputCommand.None;
            for (int tick = 0; tick < 100; tick++)
            {
                if (tick == 0)
                    sim.Step(in fire);
                else
                    sim.Step(in none);
                ReadOnlySpan<SimEvent> events = sim.EventsThisTick;
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].Type == SimEventType.BossPhaseChanged)
                        return events[i].Arg;
                }
            }
            Assert.Fail(
                $"Boss phase {startingPhase} did not change after one shot.");
            return -1;
        }

        static void AssertAll(Action assert) => assert();

        static bool HasEvent(
            ReadOnlySpan<SimEvent> events,
            SimEventType type)
        {
            for (int i = 0; i < events.Length; i++)
                if (events[i].Type == type)
                    return true;
            return false;
        }

        static BattleSimConfig CreateConfig()
        {
            return new BattleSimConfig
            {
                PlayerSpeedPerTick = 2,
                PlayerBulletSpeedPerTick = 1,
                FireIntervalTicks = 1,
                MaxBullets = 64,
                PlayerMinX = -1000,
                PlayerMaxX = 1000,
                PlayerMinY = -1000,
                PlayerMaxY = 1000,
                BulletDespawnX = 2000,
                EnemyDespawnX = -2000,
                PlayerSpawnX = 0,
                PlayerSpawnY = 0,
                PlayerMaxHp = 50,
                PlayerHalfWidth = 64,
                PlayerHalfHeight = 64,
                CapsuleHalfWidth = 0,
                CapsuleHalfHeight = 0,
                CapsuleNoDropWeight = 0,
                ScrollSpeedNumerator = 0,
                ScrollSpeedDenominator = 1,
                EnemyBulletSpeedNumerator = 64,
                EnemyBulletSpeedDenominator = 1,
                EnemyBulletHalfWidth = 32,
                EnemyBulletHalfHeight = 32,
                EnemyBulletDamage = 1,
                MaxEnemyBullets = 32
            };
        }
    }
}
