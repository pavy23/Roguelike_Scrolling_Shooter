using System.IO;
using NUnit.Framework;
using Shmup.Core.Content;
using Shmup.Core.Generation;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-175: 파츠가 **주 공격과 별개로** 주기적인 레이저를 쏠 수 있다.
    ///
    /// 사람 지시 2026-08-04: "페이즈3는 코어에서 미사일이 나가다가 주기적으로
    /// 초대형 레이저 (직경 코어 지름 만한) 나가는 패턴 추가해줘."
    ///
    /// 파츠당 공격이 하나뿐이라 데이터만으로는 표현할 수 없었다. Core에
    /// `secondaryLaser`가 들어갔고(CODEX 한도 소진으로 GROK이 §9-1 대행 구현,
    /// 리뷰 대기), 리뷰가 없는 코드라 동작을 여기서 못 박는다.
    ///
    /// 검사하는 것은 **연결**이다: 데이터에 있고, 파서가 읽고, 주 공격이 그대로
    /// 남아 있는가. 셋 중 하나만 빠져도 화면에서는 "레이저가 안 나온다"로만 보인다.
    /// </summary>
    public sealed class Req175SecondaryLaserTests
    {
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
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "GameData")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException();
        }

        [Test]
        public void WarshipCoreKeepsItsMissilesAndGainsAPeriodicBeam()
        {
            GameDataSet data = ParseRepositoryGameData();
            StageBossTemplate warship = null;
            var bosses = data.StageGeneration.Bosses;
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i].WarshipEncounter != null)
                    warship = bosses[i];
            Assert.NotNull(warship, "전함 보스를 찾지 못했다.");

            BossPartDefinition core = null;
            for (int i = 0; i < warship.Parts.Count; i++)
                if (warship.Parts[i].IsCore)
                    core = warship.Parts[i];
            Assert.NotNull(core, "전함에 코어 파츠가 없다.");

            // 미사일이 그대로 남아 있어야 한다 — 레이저가 그것을 대체하면
            // 사람이 요구한 "미사일이 나가다가"가 사라진다.
            Assert.AreNotEqual(
                BossPartAttackType.None,
                core.Attack.Type,
                "코어의 주 공격이 사라졌다.");
            Assert.AreNotEqual(
                BossPartAttackType.Laser,
                core.Attack.Type,
                "주 공격이 레이저로 바뀌었다 — 미사일이 있어야 한다.");

            Assert.NotNull(
                core.Attack.SecondaryLaser,
                "코어에 주기적 레이저가 없다.");

            // "직경 코어 지름만한" — 코어 판정 반폭(2.5유닛) 언저리여야 한다.
            // 너무 얇으면 초대형이 아니고, 너무 굵으면 피할 곳이 없다.
            double fullHalf = core.Attack.SecondaryLaser.FullHalfWidth
                / (double)SimSpace.SubUnitsPerWorldUnit;
            double coreHalfWidth = core.HalfWidth
                / (double)SimSpace.SubUnitsPerWorldUnit;
            Assert.GreaterOrEqual(
                fullHalf,
                coreHalfWidth * 0.6,
                $"빔이 {fullHalf:F2}유닛으로 코어({coreHalfWidth:F2})보다 너무 얇다.");
            // 상한을 2.2배로 올렸다. 사람이 2026-08-04에 "페이즈3 레이저는 지금보다
            // 2배 두껍게"라고 지시해 코어 반폭의 2배가 됐다 — 데이터가 맞고 이
            // 상한이 낡은 것이다. 그래도 상한을 없애지는 않는다: 화면 높이의
            // 절반(11.25유닛)을 넘으면 피할 곳이 사라져 패턴이 아니라 처형이 된다.
            Assert.LessOrEqual(
                fullHalf,
                coreHalfWidth * 2.2,
                $"빔이 {fullHalf:F2}유닛으로 코어({coreHalfWidth:F2})보다 너무 굵다.");

            // 빔이 꺼져 있는 시간이 켜져 있는 시간보다 길어야 한다 — 늘 켜져
            // 있으면 미사일을 볼 수 없고 피할 틈도 없다.
            var laser = core.Attack.SecondaryLaser;
            Assert.Greater(
                laser.CycleIntervalTicks,
                laser.LifetimeTicks * 2,
                "빔이 꺼져 있는 시간이 켜져 있는 시간보다 짧다.");

            // 굵은 빔은 예고가 길어야 한다. 예고 없이 이 굵기가 나오면 처형이다.
            Assert.GreaterOrEqual(
                laser.TelegraphTicks,
                (int)(SimSpace.TicksPerSecond * 2.0),
                "초대형 빔의 예고가 2초 미만이다.");
        }
    }
}
