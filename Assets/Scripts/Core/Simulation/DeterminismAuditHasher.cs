using System;
using System.Collections.Generic;
using Shmup.Core.Generation;

namespace Shmup.Core.Simulation
{
    /// <summary>
    /// Stable FNV-1a fold for determinism audits. The run overload includes every
    /// publicly observable mutable field, ordered collection item, reward option,
    /// generated stage value, and event. Values are folded byte-by-byte in an
    /// explicit little-endian order.
    /// </summary>
    public sealed class DeterminismAuditHasher
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        ulong _hash = OffsetBasis;

        public ulong Hash => _hash;
        public string HexHash => _hash.ToString("X16");

        public void FoldRunState(RunManager run)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));

            FoldInt32(run.RunNumber);
            FoldInt32(run.StageIndex);
            FoldInt32((int)run.State);
            FoldUInt64(run.RunSeed);
            FoldInt32(run.Difficulty);
            FoldInt64(run.TotalScore);

            RunStatistics statistics = run.Statistics;
            FoldInt64(statistics.ShotsFired);
            FoldInt64(statistics.ShotsHit);
            FoldInt64(statistics.Kills);
            FoldInt64(statistics.CapsulesCollected);
            FoldInt64(statistics.GrazeCount);
            FoldInt32(statistics.StagesCleared);

            FoldShip(run.Ship);
            FoldPowerUpGauge(run.PowerUpGauge);
            FoldStagePlan(run.StagePlan);
            FoldRewards(run.RewardOptions);
            FoldBattle(run.Battle);
        }

        public void FoldBattleState(IBattleSim battle)
        {
            if (battle == null)
                throw new ArgumentNullException(nameof(battle));
            FoldBattle(battle);
        }

        public void FoldRewardChoice(
            int stageIndex,
            int optionIndex,
            in RewardOption option)
        {
            FoldInt32(stageIndex);
            FoldInt32(optionIndex);
            FoldString(option.Id);
            FoldInt32((int)option.Type);
            FoldInt32((int)option.Slot);
            FoldInt32(option.Amount);
        }

        public void FoldTick(
            int runNumber,
            int stageIndex,
            RunState runState,
            IBattleSim steppedBattle,
            long totalScore)
        {
            if (steppedBattle == null)
                throw new ArgumentNullException(nameof(steppedBattle));
            FoldTick(
                runNumber,
                stageIndex,
                (int)runState,
                steppedBattle.Tick,
                steppedBattle.PlayerX,
                steppedBattle.PlayerY,
                steppedBattle.Bullets.Count,
                steppedBattle.Enemies.Count,
                totalScore,
                steppedBattle.EventsThisTick.Length);
        }

        public void FoldTick(
            int runNumber,
            int stageIndex,
            int runState,
            int battleTick,
            int playerX,
            int playerY,
            int bulletCount,
            int enemyCount,
            long totalScore,
            int eventCount)
        {
            FoldInt32(runNumber);
            FoldInt32(stageIndex);
            FoldInt32(runState);
            FoldInt32(battleTick);
            FoldInt32(playerX);
            FoldInt32(playerY);
            FoldInt32(bulletCount);
            FoldInt32(enemyCount);
            FoldInt64(totalScore);
            FoldInt32(eventCount);
        }

        void FoldPowerUpGauge(PowerUpGauge gauge)
        {
            FoldInt32(gauge.Cursor);
            for (int i = 0; i < PowerUpGauge.SlotCount; i++)
            {
                var slot = (PowerUpSlot)i;
                FoldInt32(gauge.GetLevel(slot));
                FoldInt32(gauge.GetMaxLevel(slot));
            }
        }

        void FoldShip(ShipDefinition ship)
        {
            FoldString(ship.Id);
            FoldString(ship.DisplayName);
            FoldInt32(ship.MoveSpeedMultiplierNumerator);
            FoldInt32(ship.MoveSpeedMultiplierDenominator);
            FoldInt64(ship.UnlockCost);
            FoldInt32(ship.StartingPowerUpLevels.Count);
            for (int i = 0; i < ship.StartingPowerUpLevels.Count; i++)
                FoldInt32(ship.StartingPowerUpLevels[i]);
        }

        void FoldStagePlan(StagePlan plan)
        {
            FoldString(plan.BossId);
            FoldString(plan.ThemeId);
            FoldInt32(plan.LaneCount);
            FoldInt32(plan.StartLaneMask);
            FoldInt32(plan.BossEntryLaneMask);
            FoldInt32(plan.BossMaxHp);
            FoldInt32(plan.BossHalfWidth);
            FoldInt32(plan.BossHalfHeight);
            FoldInt32(plan.BossHoldX);

            FoldInt32(plan.BossPhases.Count);
            for (int i = 0; i < plan.BossPhases.Count; i++)
            {
                BossPhase phase = plan.BossPhases[i];
                FoldInt32(phase.FireIntervalTicks);
                FoldInt32(phase.Ways);
                FoldInt32(phase.BulletSpeedNumerator);
                FoldInt32(phase.BulletSpeedDenominator);
            }

            FoldInt32(plan.Segments.Count);
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                StageSegment segment = plan.Segments[i];
                FoldString(segment.SegmentId);
                FoldInt32(segment.LengthTicks);
                FoldInt32(segment.EntryLaneMask);
                FoldInt32(segment.ExitLaneMask);
                FoldInt32(segment.TraversableLaneMasks.Count);
                for (int j = 0; j < segment.TraversableLaneMasks.Count; j++)
                    FoldInt32(segment.TraversableLaneMasks[j]);

                FoldInt32(segment.Spawns.Count);
                for (int j = 0; j < segment.Spawns.Count; j++)
                {
                    SpawnEvent spawn = segment.Spawns[j];
                    FoldInt32(spawn.Tick);
                    FoldString(spawn.EnemyId);
                    FoldInt32(spawn.X);
                    FoldInt32(spawn.Y);
                }
            }
        }

        void FoldRewards(IReadOnlyList<RewardOption> rewards)
        {
            FoldInt32(rewards.Count);
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardOption reward = rewards[i];
                FoldString(reward.Id);
                FoldInt32((int)reward.Type);
                FoldInt32((int)reward.Slot);
                FoldInt32(reward.Amount);
            }
        }

        void FoldBattle(IBattleSim battle)
        {
            FoldInt32(battle.Tick);
            FoldInt64(battle.Score);
            BattleStatistics statistics = battle.Statistics;
            FoldInt64(statistics.ShotsFired);
            FoldInt64(statistics.ShotsHit);
            FoldInt64(statistics.Kills);
            FoldInt64(statistics.CapsulesCollected);
            FoldInt64(statistics.GrazeCount);
            FoldInt32(battle.MultiplierLevel);
            FoldInt32(battle.ScoreMultiplier);
            FoldInt32(battle.ComboGauge);
            FoldInt64(battle.ScrollX);
            FoldInt32(battle.PlayerX);
            FoldInt32(battle.PlayerY);
            FoldInt32(battle.PlayerHp);
            FoldInt32(battle.ShieldRemaining);

            FoldInt32(battle.Bullets.Count);
            for (int i = 0; i < battle.Bullets.Count; i++)
            {
                BulletState bullet = battle.Bullets[i];
                FoldInt32(bullet.Id);
                FoldInt32((int)bullet.Faction);
                FoldInt32((int)bullet.Kind);
                FoldInt32(bullet.X);
                FoldInt32(bullet.Y);
            }

            FoldInt32(battle.Options.Count);
            for (int i = 0; i < battle.Options.Count; i++)
            {
                OptionState option = battle.Options[i];
                FoldInt32(option.Index);
                FoldInt32(option.X);
                FoldInt32(option.Y);
            }

            FoldInt32(battle.Enemies.Count);
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                EnemyState enemy = battle.Enemies[i];
                FoldInt32(enemy.Id);
                FoldString(enemy.DefinitionId);
                FoldInt32(enemy.X);
                FoldInt32(enemy.Y);
                FoldInt32(enemy.Hp);
            }

            FoldInt32(battle.Capsules.Count);
            for (int i = 0; i < battle.Capsules.Count; i++)
            {
                CapsuleState capsule = battle.Capsules[i];
                FoldInt32(capsule.Id);
                FoldInt32(capsule.X);
                FoldInt32(capsule.Y);
            }

            ReadOnlySpan<SimEvent> events = battle.EventsThisTick;
            FoldInt32(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                SimEvent simEvent = events[i];
                FoldInt32((int)simEvent.Type);
                FoldInt32(simEvent.EntityId);
                FoldInt32(simEvent.X);
                FoldInt32(simEvent.Y);
                FoldInt32(simEvent.Arg);
            }

            FoldBool(battle.BossActive);
            BossState boss = battle.Boss;
            FoldInt32(boss.Id);
            FoldInt32(boss.X);
            FoldInt32(boss.Y);
            FoldInt32(boss.Hp);
            FoldInt32(boss.MaxHp);
            FoldInt32(boss.Phase);
        }

        void FoldBool(bool value)
        {
            FoldByte(value ? (byte)1 : (byte)0);
        }

        void FoldString(string value)
        {
            if (value == null)
            {
                FoldInt32(-1);
                return;
            }

            FoldInt32(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                FoldByte((byte)character);
                FoldByte((byte)(character >> 8));
            }
        }

        void FoldInt32(int value)
        {
            unchecked
            {
                uint bits = (uint)value;
                for (int shift = 0; shift < 32; shift += 8)
                    FoldByte((byte)(bits >> shift));
            }
        }

        void FoldInt64(long value)
        {
            unchecked
            {
                ulong bits = (ulong)value;
                for (int shift = 0; shift < 64; shift += 8)
                    FoldByte((byte)(bits >> shift));
            }
        }

        void FoldUInt64(ulong value)
        {
            unchecked
            {
                for (int shift = 0; shift < 64; shift += 8)
                    FoldByte((byte)(value >> shift));
            }
        }

        void FoldByte(byte value)
        {
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }
    }
}
