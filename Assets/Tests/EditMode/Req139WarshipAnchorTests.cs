using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-139: 거대 전함이 막마다 **세로로 자리를 옮긴다**.
    ///
    /// 사람 요구는 "처음엔 화면 아래 오른쪽에 윗부분만 보이다가, 윗부분을 부수면
    /// 가운데로 정렬"이다. 그래서 검증할 성질은 셋이다:
    ///   ① 정박 위치가 막마다 바뀌고 파츠가 통째로 따라간다
    ///   ② 이동은 정수 보간이라 같은 입력이면 같은 좌표에 온다 (§4)
    ///   ③ 이동 도중에 저장/복원해도 같은 자리에서 이어진다
    ///
    /// 좌표 상수를 박지 않는다 — 밸런스 수치가 바뀔 때마다 깨지는 테스트를 오늘만
    /// 세 번 고쳤다. 여기서는 픽스처를 직접 만들어 **관계**만 검증한다.
    /// </summary>
    public sealed class Req139WarshipAnchorTests
    {
        const int Unit = SimSpace.SubUnitsPerWorldUnit;
        const int OriginY = 0;
        const int TravelTicks = 60;
        const int LoweredY = -6 * Unit;

        static BossPartDefinition Part(string id, int offsetY, int hp, bool isCore = false)
        {
            return new BossPartDefinition(
                id,
                0,
                offsetY,
                Unit,
                Unit,
                hp,
                isCore,
                null,
                null,
                0);
        }

        static IReadOnlyList<BossPartDefinition> Parts()
        {
            return new[]
            {
                Part("gate", 2 * Unit, 10),
                Part("line", 0, 10),
                Part("core", -2 * Unit, 10, true)
            };
        }

        /// <summary>1막은 아래에 잠겨 있고, 2막에서 원위치로 올라온다.</summary>
        static WarshipEncounterDefinition Definition()
        {
            var groups = new[]
            {
                new WarshipPartGroupDefinition(
                    "gate",
                    WarshipGroupRole.MidbossGate,
                    new[] { "gate" },
                    0,
                    LoweredY,
                    0),
                new WarshipPartGroupDefinition(
                    "line",
                    WarshipGroupRole.AttritionLine,
                    new[] { "line" },
                    600,
                    0,
                    TravelTicks),
                new WarshipPartGroupDefinition(
                    "core",
                    WarshipGroupRole.FinalCore,
                    new[] { "core" },
                    0,
                    0,
                    0)
            };
            return new WarshipEncounterDefinition(
                "anchor_test",
                901,
                4,
                20 * Unit,
                OriginY,
                12 * Unit,
                Unit,
                1,
                9,
                2,
                3,
                groups,
                Parts());
        }

        static WarshipEncounter Fresh()
        {
            var definition = Definition();
            var encounter = new WarshipEncounter(definition, Parts());
            while (encounter.ActiveGroupIndex < 0)
                encounter.Step(Array.Empty<WarshipDamageCommand>());
            return encounter;
        }

        static WarshipPartState Find(WarshipEncounter encounter, string partId)
        {
            for (int i = 0; i < encounter.Parts.Count; i++)
                if (encounter.Parts[i].PartId == partId)
                    return encounter.Parts[i];
            throw new InvalidOperationException(partId);
        }

        /// <summary>
        /// REQ-139 3막: 함체를 다 부수면 **거기서 전투가 끝난다.**
        ///
        /// 예전에는 함체 안에서 로봇이 튀어나오는 네 번째 페이즈가 있었고, 이
        /// 테스트도 그것을 요구했다. 사람이 2026-08-05에 없애기로 정했다 —
        /// "전함보스는 전함 부수면 그냥 끝 (마지막 로봇 단독 페이즈 없음)".
        /// 설계 결정이 뒤집혔으므로 기대값도 뒤집는다.
        /// </summary>
        [Test]
        public void ClearingTheFortressHullEndsTheBattle()
        {
            GameDataSet data = ParseRepositoryGameData();
            StageBossTemplate fortress = null;
            for (int i = 0; i < data.StageGeneration.Bosses.Count; i++)
                if (data.StageGeneration.Bosses[i].BossId == "boss_fortress")
                    fortress = data.StageGeneration.Bosses[i];

            Assert.NotNull(fortress, "요새 보스가 데이터에 없다.");
            Assert.NotNull(
                fortress.WarshipEncounter,
                "요새는 전함 조우로 굴러간다.");
            Assert.IsNull(
                fortress.Form2,
                "전함은 두 번째 폼 없이 끝난다 — 함체를 부수면 그것으로 끝이다.");
        }

        static GameDataSet ParseRepositoryGameData()
        {
            string gameData = Path.Combine(FindRepositoryRoot(), "GameData");
            return GameDataParser.Parse(
                File.ReadAllText(Path.Combine(gameData, "enemies.json")),
                File.ReadAllText(Path.Combine(gameData, "weapons.json")),
                File.ReadAllText(Path.Combine(gameData, "waves.json")),
                File.ReadAllText(Path.Combine(gameData, "rewards.json")),
                File.ReadAllText(Path.Combine(gameData, "ships.json")),
                File.ReadAllText(Path.Combine(gameData, "scoring.json")));
        }

        static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }


        /// <summary>
        /// 함체를 전부 부수면 **로봇이 실제로 나오는가**.
        ///
        /// 데이터와 전환 훅은 각각 들어갔지만 둘을 이어 붙인 경로를 끝까지 돌려 본 적이
        /// 없었고, 사람이 실플레이에서 "전함 파괴뒤 나오는 로봇도 반영 안됨"이라고
        /// 보고했다. 실제 데이터로 파츠를 전부 깨뜨려 폼 전환까지 확인한다.
        /// </summary>
        [Test]
        public void DestroyingEveryWarshipPartEndsTheBattle()
        {
            GameDataSet data = ParseRepositoryGameData();
            var generator = new SegmentStageGenerator(data.StageGeneration);
            int difficulty = StageDifficultyCurve.CreateDefault().GetDifficulty(3);
            StagePlan plan = generator.GenerateRoute(
                20240803UL, 3, difficulty, "fortress", EncounterType.Normal);
            Assert.AreEqual("boss_fortress", plan.BossId);
            Assert.NotNull(plan.WarshipEncounter);
            Assert.IsNull(plan.Form2, "전함에 두 번째 폼이 남아 있다.");

            BattleSimConfig config = data.CreateBattleSimConfig();
            config.PlayerInvulnerable = true;
            var battle = new BattleSim(
                config, new Rng(20240803UL), plan, data.BattleContent,
                data.CreatePowerUpGauge());

            var input = InputCommand.None;
            for (int tick = 0; tick < 40000 && !battle.BossActive; tick++)
                battle.Step(in input);
            Assert.IsTrue(battle.BossActive, "보스가 등장하지 않았다.");

            // 살아 있는 파츠를 계속 때린다. 그룹 게이트 때문에 한 번에 하나씩만
            // 열리므로, 매 틱 "지금 때릴 수 있는 것"을 찾아 때리는 방식이어야 한다.
            for (int tick = 0; tick < 60000 && !battle.BossDefeated; tick++)
            {
                var parts = battle.BossParts;
                for (int i = 0; i < parts.Count; i++)
                {
                    BossPartState part = parts[i];
                    if (part.Destroyed || part.Invulnerable) continue;
                    battle.TrySpawnGhostMainShot(part.X, part.Y, 400);
                }
                battle.Step(in input);
                if (battle.BossDefeated) break;
            }

            Assert.IsTrue(
                battle.BossDefeated,
                "함체를 다 부쉈는데 전투가 끝나지 않았다.");
            Assert.AreEqual(
                0, battle.BossFormIndex,
                "두 번째 폼으로 넘어갔다 — 전함은 함체가 끝이다.");
        }

        [Test]
        public void OpeningActStagesTheHullAtItsOwnAnchor()
        {
            WarshipEncounter encounter = Fresh();

            Assert.AreEqual(0, encounter.ActiveGroupIndex);
            Assert.AreEqual(LoweredY, encounter.AnchorOffsetY);
            // 파츠는 함체를 타고 간다 — 정박 오프셋이 전부에 그대로 실린다.
            Assert.AreEqual(
                OriginY + LoweredY + 2 * Unit,
                Find(encounter, "gate").Y);
            Assert.AreEqual(
                OriginY + LoweredY - 2 * Unit,
                Find(encounter, "core").Y);
        }

        [Test]
        public void ClearingTheOpeningActRaisesTheHullOverItsTravelTicks()
        {
            WarshipEncounter encounter = Fresh();
            encounter.Step(new[] { new WarshipDamageCommand("gate", 10) });
            Assert.AreEqual(1, encounter.ActiveGroupIndex);

            int previous = encounter.AnchorOffsetY;
            Assert.AreEqual(LoweredY, previous, "이동은 있던 자리에서 시작해야 한다.");

            for (int tick = 0; tick < TravelTicks; tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                Assert.GreaterOrEqual(
                    encounter.AnchorOffsetY,
                    previous,
                    "정박 이동은 되돌아가지 않는다.");
                previous = encounter.AnchorOffsetY;
            }

            Assert.AreEqual(0, encounter.AnchorOffsetY, "정확히 목표에 안착해야 한다.");
            Assert.AreEqual(1000, encounter.AnchorTravelPermille);
            Assert.AreEqual(OriginY, Find(encounter, "line").Y);
        }

        [Test]
        public void TheSameTicksProduceTheSameAnchorEveryRun()
        {
            WarshipEncounter first = Fresh();
            WarshipEncounter second = Fresh();
            first.Step(new[] { new WarshipDamageCommand("gate", 10) });
            second.Step(new[] { new WarshipDamageCommand("gate", 10) });

            for (int tick = 0; tick < TravelTicks + 5; tick++)
            {
                first.Step(Array.Empty<WarshipDamageCommand>());
                second.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(first.AnchorOffsetY, second.AnchorOffsetY);
                Assert.AreEqual(
                    first.AnchorTravelPermille,
                    second.AnchorTravelPermille);
            }
        }

        [Test]
        public void SuspendingMidTravelResumesAtTheSameHeight()
        {
            WarshipEncounter encounter = Fresh();
            encounter.Step(new[] { new WarshipDamageCommand("gate", 10) });
            for (int tick = 0; tick < TravelTicks / 3; tick++)
                encounter.Step(Array.Empty<WarshipDamageCommand>());

            int height = encounter.AnchorOffsetY;
            Assert.AreNotEqual(LoweredY, height, "이동 중이어야 의미가 있는 검증이다.");
            Assert.AreNotEqual(0, height);

            WarshipEncounter restored = WarshipEncounter.Restore(
                Definition(),
                Parts(),
                encounter.CaptureSuspendData());
            Assert.AreEqual(height, restored.AnchorOffsetY);

            for (int tick = 0; tick < TravelTicks; tick++)
            {
                encounter.Step(Array.Empty<WarshipDamageCommand>());
                restored.Step(Array.Empty<WarshipDamageCommand>());
                Assert.AreEqual(encounter.AnchorOffsetY, restored.AnchorOffsetY);
            }
        }
    }
}
