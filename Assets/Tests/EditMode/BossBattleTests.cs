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
                bossMaxHp: 100, holdX: 400, phases: Phase(interval: 999, ways: 1));
            InputCommand none = InputCommand.None;

            for (int i = 0; i < 4; i++) sim.Step(in none);
            Assert.IsFalse(sim.BossActive);

            sim.Step(in none);   // tick 5 = 세그먼트 소진 → 스폰
            Assert.IsTrue(sim.BossActive);
            Assert.AreEqual(1, sim.EventsThisTick.Length);
            Assert.AreEqual(SimEventType.BossSpawned, sim.EventsThisTick[0].Type);
            int entryX = sim.Boss.X;

            sim.Step(in none);
            Assert.AreEqual(entryX - 16, sim.Boss.X);   // 진입 속도 16 서브유닛/틱

            for (int i = 0; i < 200; i++) sim.Step(in none);
            Assert.AreEqual(400, sim.Boss.X);           // holdX 정지
            Assert.AreEqual(100, sim.Boss.Hp);
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

            sim.Step(in none); // 보스 스폰
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
            var fire = new InputCommand(0, 0, true);

            for (int i = 0; i < 500 && run.State == RunState.Playing; i++)
                run.Step(in fire);

            Assert.AreEqual(RunState.AwaitingReward, run.State);
            Assert.AreEqual(3, run.RewardOptions.Count);
            Assert.AreEqual(1, run.StageIndex);

            run.ChooseReward(0);
            Assert.AreEqual(RunState.Playing, run.State);
            Assert.AreEqual(2, run.StageIndex);
            Assert.AreEqual(0, run.RewardOptions.Count);
        }

        [Test]
        public void RewardOptionsAreDeterministicPerSeedAndStage()
        {
            RunManager first = CreateBossRun(seed: 99UL);
            RunManager second = CreateBossRun(seed: 99UL);
            var fire = new InputCommand(0, 0, true);

            for (int i = 0; i < 500 && first.State == RunState.Playing; i++)
                first.Step(in fire);
            for (int i = 0; i < 500 && second.State == RunState.Playing; i++)
                second.Step(in fire);

            Assert.AreEqual(RunState.AwaitingReward, first.State);
            Assert.AreEqual(RunState.AwaitingReward, second.State);
            for (int i = 0; i < first.RewardOptions.Count; i++)
            {
                Assert.AreEqual(first.RewardOptions[i].Type, second.RewardOptions[i].Type);
                Assert.AreEqual(first.RewardOptions[i].Slot, second.RewardOptions[i].Slot);
                Assert.AreEqual(first.RewardOptions[i].Amount, second.RewardOptions[i].Amount);
            }
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
        public void RepairRewardExpiresWhenTheRunEnds()
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
            Assert.AreEqual(51, run.Battle.PlayerHp);

            InputCommand none = InputCommand.None;
            run.Step(in none);
            Assert.AreEqual(RunState.RunOver, run.State);

            run.Restart(999UL);
            Assert.AreEqual(50, run.Battle.PlayerHp);
        }

        [Test]
        public void LegacyPlansWithoutBossStillAdvanceByTicks()
        {
            RunManager run = CreateBossRun(seed: 5UL, bossMaxHp: 0);
            InputCommand none = InputCommand.None;

            for (int i = 0; i < 5; i++) run.Step(in none);
            Assert.AreEqual(2, run.StageIndex);   // 틱 소진으로 전환 (보상 없음)
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

        static RunManager CreateBossRun(ulong seed, int bossMaxHp = 10)
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
                PowerUpGauge.CreateDefault());
        }

        static RunManager CreateRepairRun(ulong seed)
        {
            EnemyDefinition lethal = new EnemyDefinition(
                "lethal", 1, 100, EnemyMovePattern.Static,
                0, 1, 0, 0, 0, 0, 64);
            BattleContent content = Content(
                new WeaponDefinition("shot", 10, 1, 50, 1, 8, 8), lethal);
            BattleSimConfig config = CreateConfig();
            config.PlayerMaxHp = 50;
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
            for (int i = 0; i < 500 && run.State == RunState.Playing; i++)
                run.Step(in fire);
            Assert.AreEqual(RunState.AwaitingReward, run.State);
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
                            new SpawnEvent(1, _lethalEnemyId, 0, 0))
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
