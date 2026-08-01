using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 다층 패럴랙스 스타필드. 각 레이어는 같은 타일 2장을 이어 붙인 루트이며,
    /// 스크롤 오프셋만큼 왼쪽으로 밀고 타일 폭에서 래핑한다.
    ///
    /// 스크롤 원천은 Core의 ScrollX (틱의 순수 함수). Presentation은 스크롤 위치를
    /// 읽어 그릴 뿐 진행 속도를 결정하지 않는다.
    ///
    /// <see cref="SectionThemeDirector"/>가 구간별로 팩터 배율을 걸어 "스크롤이 빨라진
    /// 느낌"을 만든다 — Core의 scrollSpeed는 건드리지 않으므로 결정론에 영향이 없다.
    /// 배율은 lerp로 연속 변하고 오프셋은 팩터의 연속 함수라 배율을 바꿔도 튀지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Transform[] _layers;
        [SerializeField] float[] _factors;
        /// <summary>레이어별 <see cref="BgLayerRole"/> (int 캐스팅). 씬 빌더가 채운다.
        /// 비어 있으면 역할 조회는 Mid로 답한다 — 룩 오버라이드가 조용히 무시될 뿐 깨지진 않는다.</summary>
        [SerializeField] int[] _layerRoles;
        [SerializeField] float _tileWidth = 24f;

        float[] _factorMultipliers;

        public int LayerCount => _layers != null ? _layers.Length : 0;

        public Transform GetLayer(int index) =>
            _layers != null && index >= 0 && index < _layers.Length ? _layers[index] : null;

        public BgLayerRole GetRole(int index)
        {
            if (_layerRoles == null || index < 0 || index >= _layerRoles.Length)
                return BgLayerRole.Mid;
            return (BgLayerRole)_layerRoles[index];
        }

        public float GetBaseFactor(int index) =>
            _factors != null && index >= 0 && index < _factors.Length ? _factors[index] : 1f;

        /// <summary>구간 테마의 스크롤 체감 배율. 런타임 전용 — 직렬화하지 않는다.</summary>
        public void SetFactorMultiplier(int index, float multiplier)
        {
            if (_layers == null || index < 0 || index >= _layers.Length) return;
            if (_factorMultipliers == null || _factorMultipliers.Length != _layers.Length)
                ResetFactorMultipliers();
            _factorMultipliers[index] = multiplier;
        }

        public void ResetFactorMultipliers()
        {
            int count = _layers != null ? _layers.Length : 0;
            if (_factorMultipliers == null || _factorMultipliers.Length != count)
                _factorMultipliers = new float[count];
            for (int i = 0; i < count; i++) _factorMultipliers[i] = 1f;
        }

        void LateUpdate()
        {
            if (_director == null || _layers == null || _factors == null) return;

            float scroll = _director.ScrollWorldX;   // Core ScrollX(서브유닛)의 월드 변환값
            for (int i = 0; i < _layers.Length && i < _factors.Length; i++)
            {
                if (_layers[i] == null) continue;
                float factor = _factors[i];
                if (_factorMultipliers != null && i < _factorMultipliers.Length
                    && _factorMultipliers[i] > 0f)
                    factor *= _factorMultipliers[i];
                float offset = Mathf.Repeat(scroll * factor, _tileWidth);
                _layers[i].localPosition = new Vector3(-offset, 0f, 0f);
            }
        }
    }
}
