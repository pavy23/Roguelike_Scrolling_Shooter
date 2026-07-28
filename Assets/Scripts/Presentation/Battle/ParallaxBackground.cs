using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 다층 패럴랙스 스타필드. 각 레이어는 같은 타일 2장을 이어 붙인 루트이며,
    /// 스크롤 오프셋만큼 왼쪽으로 밀고 타일 폭에서 래핑한다.
    ///
    /// 스크롤 원천은 Core의 ScrollX (틱의 순수 함수). Presentation은 스크롤 위치를
    /// 읽어 그릴 뿐 진행 속도를 결정하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Transform[] _layers;
        [SerializeField] float[] _factors;
        [SerializeField] float _tileWidth = 24f;

        void LateUpdate()
        {
            if (_director == null || _layers == null || _factors == null) return;

            float scroll = _director.ScrollWorldX;   // Core ScrollX(서브유닛)의 월드 변환값
            for (int i = 0; i < _layers.Length && i < _factors.Length; i++)
            {
                if (_layers[i] == null) continue;
                float offset = Mathf.Repeat(scroll * _factors[i], _tileWidth);
                _layers[i].localPosition = new Vector3(-offset, 0f, 0f);
            }
        }
    }
}
