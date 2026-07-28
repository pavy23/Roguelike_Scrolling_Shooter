using System;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// Core 시뮬 이벤트(REQ-005)를 효과음으로 번역한다. 순수 표현 — 게임 상태에 영향 없음.
    /// 채택음은 Tools/SfxGen (타입, 시드 0) 산출물. 같은 틱에 같은 소리는 1회만 재생해
    /// 다중 격파 시 볼륨 폭주를 막는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] AudioSource _source;
        [SerializeField] AudioClip _laser;
        [SerializeField] AudioClip _hit;
        [SerializeField] AudioClip _explosion;
        [SerializeField] AudioClip _pickup;
        [SerializeField] AudioClip _powerup;

        [Range(0f, 1f)]
        [SerializeField] float _laserVolume = 0.35f;

        // 이번 스텝에서 이미 재생한 클립 (틱당 1회 제한)
        readonly bool[] _playedThisStep = new bool[8];

        public void PlayEvents(ReadOnlySpan<SimEvent> events)
        {
            if (_source == null) return;
            Array.Clear(_playedThisStep, 0, _playedThisStep.Length);

            for (int i = 0; i < events.Length; i++)
            {
                switch (events[i].Type)
                {
                    case SimEventType.PlayerFired:
                        PlayOnce(0, _laser, _laserVolume);
                        break;
                    case SimEventType.EnemyHit:
                        PlayOnce(1, _hit, 0.5f);
                        break;
                    case SimEventType.EnemyKilled:
                        PlayOnce(2, _explosion, 0.8f);
                        break;
                    case SimEventType.PlayerHit:
                        PlayOnce(3, _hit, 1f);
                        break;
                    case SimEventType.PlayerKilled:
                        PlayOnce(4, _explosion, 1f);
                        break;
                    case SimEventType.CapsulePicked:
                        PlayOnce(5, _pickup, 0.9f);
                        break;
                    case SimEventType.PowerUpLevelChanged:
                        PlayOnce(6, _powerup, 1f);
                        break;
                }
            }
        }

        void PlayOnce(int channel, AudioClip clip, float volume)
        {
            if (clip == null || _playedThisStep[channel]) return;
            _playedThisStep[channel] = true;
            _source.PlayOneShot(clip, volume);
        }
    }
}
