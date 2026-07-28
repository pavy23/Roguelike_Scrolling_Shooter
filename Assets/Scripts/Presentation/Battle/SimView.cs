using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 시뮬레이션 서브유닛 정수 좌표 → Unity 월드 좌표 변환. 순수 단위 변환뿐이고
    /// 어떤 판정도 하지 않는다 (CLAUDE.md: Presentation은 Core 상태를 그리기만 한다).
    /// </summary>
    public static class SimView
    {
        public const float WorldUnitsPerSubUnit = 1f / SimSpace.SubUnitsPerWorldUnit;

        public static Vector3 ToWorld(int subX, int subY)
            => new Vector3(subX * WorldUnitsPerSubUnit, subY * WorldUnitsPerSubUnit, 0f);
    }
}
