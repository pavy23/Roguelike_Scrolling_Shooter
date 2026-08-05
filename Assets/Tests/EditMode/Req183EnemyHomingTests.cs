using NUnit.Framework;
using Shmup.Core.Simulation;

namespace Shmup.Core.Tests
{
    /// <summary>
    /// REQ-183: 적 유도탄은 **영원히 따라오지 않는다.**
    ///
    /// 사람 보고 2026-08-05: "하이브 보스 마지막 패턴 유도 미사일이 끝까지
    /// 따라오는거 아직 안고쳐졌네."
    ///
    /// 유도가 탄의 수명 내내 이어지고 있었다. 한 번 나가면 화면 어디로 도망쳐도
    /// 붙고, 그 패턴을 쓰는 페이즈가 끝난 뒤에도 남은 탄은 계속 꺾었다 — 페이즈
    /// 데이터에서 유도를 0으로 꺼도 이미 날아간 탄에는 소용이 없다. **피할 방법이
    /// 없는 탄은 패턴이 아니라 처형이다.**
    ///
    /// 여기서 보는 것은 설정의 존재가 아니라 **의도**다: 유도 시간이 유한하고,
    /// 사람이 한 번 크게 유인해 흘려보낼 만큼은 되며, 화면을 가로지르는 내내
    /// 붙어 있을 만큼 길지는 않다.
    /// </summary>
    [TestFixture]
    public sealed class Req183EnemyHomingTests
    {
        [Test]
        public void EnemyHomingStopsSteeringAfterAWhile()
        {
            var config = new BattleSimConfig();

            Assert.Greater(
                config.EnemyHomingDurationTicks,
                0,
                "적 유도에 시간 제한이 없다 — 한 번 나가면 영원히 따라온다.");

            // 너무 짧으면 유도라고 부를 수 없다. 0.5초는 화면에서 한 번 꺾이는 것이
            // 보이는 최소치다.
            Assert.GreaterOrEqual(
                config.EnemyHomingDurationTicks,
                SimSpace.TicksPerSecond / 2,
                "유도 시간이 너무 짧아 유도로 읽히지 않는다.");

            // 화면 폭(40유닛)을 가로지르는 시간보다 길면 "끝까지 따라온다"와
            // 사실상 같다. 느린 탄(6유닛/초) 기준 횡단이 약 6.7초이므로 그 절반을
            // 상한으로 둔다.
            int crossingTicks = (int)(
                SimSpace.TicksPerSecond
                * (2.0 * SimSpace.PlayfieldHalfWidthSubUnits
                    / SimSpace.SubUnitsPerWorldUnit) / 6.0);
            Assert.Less(
                config.EnemyHomingDurationTicks,
                crossingTicks / 2,
                $"유도가 {config.EnemyHomingDurationTicks}틱이나 이어진다 — "
                + "화면을 가로지르는 내내 붙어 있는 것과 다르지 않다.");
        }
    }
}
