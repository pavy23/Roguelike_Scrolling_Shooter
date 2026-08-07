using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    [TestFixture]
    public sealed class Req118WarshipPositionTests
    {
        /// <summary>등장에 허용하는 최대 시간. 경고 + 화면 진입을 넉넉히 덮는다.</summary>
        const int EntranceTickBudget = 1_800;

        const string FortressThemeId = "fortress";
        const string FortressBossId = "boss_fortress";
        const string SternPartId = "engine";
        const string ForwardTurretPartId = "turret_c";
        const string CorePartId = "core";
        const ulong FortressSeed = 2UL;
        const int FortressStageIndex = 3;
        const int LongGateObservationTick = 3_000;
        const int BossReachTickBudget = 60_000;

        [Test]
        public void RepositoryFortressWarshipKeepsDamageablePartsInPlayfieldForEntireEncounter()
        {
            StageBossTemplate boss = RepositoryFortressBoss();
            var encounter = new WarshipEncounter(
                boss.WarshipEncounter,
                boss.Parts);

            // **경고 틱 수로 세지 않는다.** 1막은 경고가 끝나고 **함체가 화면
            // 안에 들어온 뒤** 열린다 — 등장 거리를 늘리면(2026-08-05) 그 시점이
            // 뒤로 밀린다. 시점을 박아 두면 연출을 손볼 때마다 테스트가 깨지고,
            // 정작 지켜야 할 것("때릴 수 있는 파츠는 화면 안에 있다")은 그대로다.
            for (int tick = 1;
                tick <= EntranceTickBudget
                    && encounter.ActiveGroupIndex < 0;
                tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            Assert.AreEqual(
                0,
                encounter.ActiveGroupIndex,
                $"{EntranceTickBudget}틱 안에 1막이 열리지 않았다.");
            // 경고 구간 동안 함체는 오른쪽에서 들어오는 중이다 - 아직 정박점을
            // 지나치지 않았어야 한다.
            Assert.GreaterOrEqual(
                encounter.WorldX,
                boss.WarshipEncounter.HoldX);
            AssertPartsRideHull(encounter, boss);

            // **정박 시점을 틱으로 박지 않는다.** 240틱은 등장 거리와 스크롤
            // 속도가 예전 값(origin 18 / 1.5)일 때의 숫자였다. 지켜야 할 것은
            // "언젠가 정박점에 서고 그 뒤로는 더 흘러가지 않는다"이지 "4초에
            // 선다"가 아니다.
            while (encounter.Tick < EntranceTickBudget
                && encounter.WorldX > boss.WarshipEncounter.HoldX)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            Assert.AreEqual(
                boss.WarshipEncounter.HoldX,
                encounter.WorldX,
                "정박점에 서지 않았다.");
            AssertPartsRideHull(encounter, boss);

            while (encounter.Tick < LongGateObservationTick)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            Assert.AreEqual(
                boss.WarshipEncounter.HoldX,
                encounter.WorldX);
            AssertPartsRideHull(encounter, boss);

            // 1막을 여는 조건은 "engine 하나"가 아니라 **midbossGate 그룹 전체**다.
            // 구성은 데이터가 정하므로 데이터에서 읽는다 — 여기에 파츠 id를
            // 적어두면 밸런스 변경 때마다 무관하게 깨진다 (REQ-157에서 실제로 깨졌다).
            encounter.Step(GateClearingDamage(encounter, boss));
            Assert.AreEqual(1, encounter.ActiveGroupIndex);
            AssertDamageablePartsInsidePlayfield(encounter);

            // 소모전은 타이머로 넘어간다. 그 길이는 데이터가 정한다 — 여기에 숫자를
            // 적어두면 밸런스가 바뀔 때마다 무관하게 깨진다.
            int attritionTicks =
                boss.WarshipEncounter.Groups[1].AdvanceAfterTicks;
            for (int elapsed = 1; elapsed < attritionTicks; elapsed++)
            {
                int beforeX = encounter.WorldX;
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(1, encounter.ActiveGroupIndex);
                AssertDamageablePartsInsidePlayfield(encounter);
                // 매 틱 확인한다 — 순간이동은 어느 한 틱에서 일어나므로
                // 후반부만 보면 놓친다.
                AssertAttritionLineWalks(encounter, beforeX);
            }

            encounter.Step(Array.Empty<WarshipDamageCommand>());
            Assert.AreEqual(2, encounter.ActiveGroupIndex);
            // 마지막 막은 정박점으로 **되돌아간다** — 그 자리에 있는 것이 아니다.
            // 예전에는 활성화 순간 좌표를 바꿔 버려서(SetAtHoldX) 함체가 한 프레임에
            // 순간이동했고, 사람이 "갑자기 워프를 해버려"라고 보고했다(2026-08-04).
            // 이제는 소모전이 끝난 자리에서 스크롤 속도로 걸어 돌아온다.
            Assert.Less(
                encounter.WorldX,
                boss.WarshipEncounter.HoldX,
                "마지막 막은 소모전이 끝난 왼쪽 자리에서 시작해야 한다.");
            AssertPartsRideHull(encounter, boss);
            AssertDamageablePartsInsidePlayfield(encounter);

            for (int tick = 0; tick < LongGateObservationTick; tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(2, encounter.ActiveGroupIndex);
                AssertPartsRideHull(encounter, boss);
                AssertDamageablePartsInsidePlayfield(encounter);
            }

            // 되돌아오는 이동은 **끝나야** 한다. 걸어 돌아오다 멈추거나 지나쳐 버리면
            // 함체가 화면 밖으로 나가거나 코어를 때릴 수 없는 자리에 선다.
            Assert.AreEqual(
                boss.WarshipEncounter.HoldX,
                encounter.WorldX,
                "마지막 막에서 정박점으로 되돌아오지 못했다.");

            encounter.Step(new[]
            {
                new WarshipDamageCommand(
                    CorePartId,
                    FindPart(encounter, CorePartId).Hp)
            });
            Assert.IsTrue(encounter.Completed);
        }

        [Test]
        public void RepositoryFortressBattleSimProjectileDamagesHeldSternAfterLegacyEscapeTick()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            int difficulty = StageDifficultyCurve.CreateDefault()
                .GetDifficulty(FortressStageIndex);
            StagePlan plan = generator.GenerateRoute(
                FortressSeed,
                FortressStageIndex,
                difficulty,
                FortressThemeId,
                EncounterType.Normal);
            Assert.AreEqual(FortressBossId, plan.BossId);
            Assert.NotNull(plan.WarshipEncounter);

            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var battle = new BattleSim(
                config,
                new Rng(FortressSeed),
                plan,
                data.BattleContent,
                data.CreatePowerUpGauge());

            int ticks = 0;
            while (ticks < BossReachTickBudget
                && !(battle.BossActive
                    && battle.WarshipActiveGroupIndex == 0))
            {
                Step(battle);
                Assert.IsTrue(battle.IsPlayerAlive);
                ticks++;
            }

            Assert.Less(ticks, BossReachTickBudget);
            while (battle.WarshipEncounterTick
                < LongGateObservationTick)
                Step(battle);

            Assert.AreEqual(0, battle.WarshipActiveGroupIndex);
            int sternIndex = FindBattlePartIndex(battle, SternPartId);
            BossPartState stern = battle.BossParts[sternIndex];
            Assert.GreaterOrEqual(
                stern.X,
                -SimSpace.PlayfieldHalfWidthSubUnits);
            Assert.LessOrEqual(
                stern.X,
                SimSpace.PlayfieldHalfWidthSubUnits);
            Assert.AreEqual(
                plan.WarshipEncounter.HoldX
                    + FindPartOffsetX(RepositoryFortressBoss(), SternPartId),
                stern.X);

            int hpBefore = stern.Hp;
            Assert.IsTrue(battle.TrySpawnGhostMainShot(
                stern.X,
                stern.Y,
                1));
            Step(battle);

            Assert.Less(battle.BossParts[sternIndex].Hp, hpBefore);
            Assert.AreEqual(hpBefore - 1, battle.BossParts[sternIndex].Hp);
        }

        static void AssertDamageablePartsInsidePlayfield(
            WarshipEncounter encounter)
        {
            for (int i = 0; i < encounter.Parts.Count; i++)
            {
                WarshipPartState part = encounter.Parts[i];
                if (!part.Active || part.Invulnerable)
                    continue;
                Assert.GreaterOrEqual(
                    part.X,
                    -SimSpace.PlayfieldHalfWidthSubUnits,
                    $"tick={encounter.Tick} part={part.PartId}");
                Assert.LessOrEqual(
                    part.X,
                    SimSpace.PlayfieldHalfWidthSubUnits,
                    $"tick={encounter.Tick} part={part.PartId}");
            }
        }

        /// <summary>
        /// 파츠가 **함체를 타고 간다**는 것을 검증한다.
        ///
        /// 예전에는 파츠마다 절대 월드 좌표(19·15·9…)를 박아 뒀는데, 함체 크기나
        /// 파츠 배치를 조정할 때마다 이 테스트가 깨졌다 - 검증하려던 성질(파츠가
        /// 함체에 붙어 함께 움직인다)과는 무관한 실패다. 관계식으로 바꾼다.
        /// </summary>
        static void AssertPartsRideHull(
            WarshipEncounter encounter,
            StageBossTemplate boss)
        {
            for (int i = 0; i < boss.Parts.Count; i++)
            {
                BossPartDefinition definition = boss.Parts[i];
                Assert.AreEqual(
                    encounter.WorldX + definition.OffsetX,
                    FindPart(encounter, definition.PartId).X,
                    $"tick={encounter.Tick} part={definition.PartId}");
            }
        }

        static int FindPartOffsetX(
            StageBossTemplate boss,
            string partId)
        {
            for (int i = 0; i < boss.Parts.Count; i++)
                if (boss.Parts[i].PartId == partId)
                    return boss.Parts[i].OffsetX;
            throw new InvalidOperationException(
                $"Part '{partId}' is missing from the fortress template.");
        }

        /// <summary>
        /// 소모전 구간은 **어느 시점엔가 멈춘다.** 계속 흘러가면 함체가 화면 밖으로
        /// 사라진다.
        ///
        /// 예전에는 "살아 있는 가장 왼쪽 파츠가 화면 끝에 정확히 닿는다"로 확인했는데,
        /// 이동 한계가 **앞으로 상대할 파츠까지 보도록** 바뀌면서(마지막 막이 열릴 때
        /// 코어가 화면 밖에 서 있던 문제) 더 이른 자리에서 멈추게 됐다. 정확한
        /// 정지 좌표는 파츠 배치가 정하는 값이라 박아 둘 것이 아니다 — 멈췄다는
        /// 사실과 화면 안이라는 사실만 지킨다.
        /// </summary>
        /// <summary>
        /// 소모전 동안 함체가 **걸어서** 움직인다. 한때는 "후반에는 멈춰 있다"로
        /// 적어 두었는데, 사람이 "천천히 이동해야 하는데 갑자기 워프를 해버려"라고
        /// 한 뒤 이동 속도를 늦추자 그 단언이 깨졌다 — 멈추는 것은 원래 요구가
        /// 아니었다. 지킬 것은 **한 틱에 튀지 않는다**는 것이다.
        /// </summary>
        static void AssertAttritionLineWalks(
            WarshipEncounter encounter, int previousWorldX)
        {
            int step = Math.Abs(encounter.WorldX - previousWorldX);
            Assert.LessOrEqual(
                step,
                MaximumWalkStepSubUnits,
                $"tick={encounter.Tick} — 함체가 한 틱에 {step} 서브유닛 움직였다. "
                + "이건 걷는 게 아니라 순간이동이다.");
            AssertDamageablePartsInsidePlayfield(encounter);
        }

        /// <summary>
        /// 한 틱에 허용되는 최대 이동. 스크롤 속도(월드 유닛/초)를 틱으로 환산한
        /// 값보다 클 이유가 없다 — 넉넉히 1 월드유닛/틱으로 잡아 두면 정상 이동은
        /// 전부 통과하고 순간이동만 걸린다.
        /// </summary>
        const int MaximumWalkStepSubUnits = SimSpace.SubUnitsPerWorldUnit;

        static void AssertPartWorldX(
            WarshipEncounter encounter,
            string partId,
            int expectedWorldUnits)
        {
            Assert.AreEqual(
                expectedWorldUnits * SimSpace.SubUnitsPerWorldUnit,
                FindPart(encounter, partId).X,
                $"tick={encounter.Tick} part={partId}");
        }

        /// <summary>
        /// midbossGate 그룹의 모든 파츠를 한 번에 없애는 피해 명령. 그룹 구성이
        /// 바뀌어도 따라간다.
        /// </summary>
        static WarshipDamageCommand[] GateClearingDamage(
            WarshipEncounter encounter,
            StageBossTemplate boss)
        {
            IReadOnlyList<string> gateParts =
                boss.WarshipEncounter.Groups[0].PartIds;
            var commands = new WarshipDamageCommand[gateParts.Count];
            for (int i = 0; i < commands.Length; i++)
                commands[i] = new WarshipDamageCommand(
                    gateParts[i],
                    FindPart(encounter, gateParts[i]).MaxHp);
            return commands;
        }

        static WarshipPartState FindPart(
            WarshipEncounter encounter,
            string partId)
        {
            for (int i = 0; i < encounter.Parts.Count; i++)
                if (string.Equals(
                        encounter.Parts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return encounter.Parts[i];
            Assert.Fail($"Missing warship part '{partId}'.");
            return default;
        }

        static int FindBattlePartIndex(BattleSim battle, string partId)
        {
            for (int i = 0; i < battle.BossParts.Count; i++)
                if (string.Equals(
                        battle.BossParts[i].PartId,
                        partId,
                        StringComparison.Ordinal))
                    return i;
            Assert.Fail($"Missing battle boss part '{partId}'.");
            return -1;
        }

        static void Step(BattleSim battle)
        {
            InputCommand input = InputCommand.None;
            battle.Step(in input);
        }

        static StageBossTemplate RepositoryFortressBoss()
        {
            StageGenerationCatalog catalog = ParseRepositoryGameData()
                .StageGeneration;
            for (int i = 0; i < catalog.Bosses.Count; i++)
                if (string.Equals(
                        catalog.Bosses[i].BossId,
                        FortressBossId,
                        StringComparison.Ordinal))
                    return catalog.Bosses[i];
            Assert.Fail($"Missing boss '{FortressBossId}'.");
            return null;
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
