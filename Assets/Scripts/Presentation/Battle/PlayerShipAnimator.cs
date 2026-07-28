using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 플레이어 기체 프레임 순환 (엔진 화염 등, M2). 순수 표현 — 시뮬 상태와 무관한
    /// 시간 기반 루프라 결정론에 영향 없다. 프레임이 없으면 아무것도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShipAnimator : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Sprite[] _frames;
        [SerializeField] float _framesPerSecond = 10f;

        void Update()
        {
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            int index = (int)(Time.time * _framesPerSecond) % _frames.Length;
            _renderer.sprite = _frames[index];
        }
    }
}
