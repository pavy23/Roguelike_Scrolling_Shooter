using System;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// Core 시뮬 이벤트(REQ-005)를 효과음으로 번역한다. 순수 표현 — 게임 상태에 영향 없음.
    /// 채택음은 Tools/SfxGen/sfxgen_snes.py 산출물이고, 획득·강화 차임만
    /// Tools/SfxGen/sfxgen_chime.py 후보 a(유리 벨 완전5도)로 교체했다
    /// ("파워업과 봄 아이템 ... 사운드가 너무 거슬려", 2026-08-02).
    /// 같은 틱에 같은 소리는 1회만 재생해 다중 격파 시 볼륨 폭주를 막는다.
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
        [SerializeField] AudioClip _laserBeam;     // laser 계열 발사음
        [SerializeField] AudioClip _spreadShot;    // spread 계열 발사음
        [SerializeField] AudioClip _warning;       // 보스 위험 패턴 예고 (REQ-059)

        /// <summary>선택 함선의 주무기 계열 — 발사음을 계열별로 바꾼다 (REQ-022 후속).</summary>
        public Shmup.Core.WeaponType WeaponFamily { get; set; } = Shmup.Core.WeaponType.Vulcan;

        AudioClip FireClip =>
            WeaponFamily == Shmup.Core.WeaponType.Laser && _laserBeam != null ? _laserBeam :
            WeaponFamily == Shmup.Core.WeaponType.Spread && _spreadShot != null ? _spreadShot :
            _laser;

        [Range(0f, 1f)]
        [SerializeField] float _laserVolume = 0.35f;

        // 이번 스텝에서 이미 재생한 클립 (틱당 1회 제한)
        readonly bool[] _playedThisStep = new bool[15];

        public void PlayEvents(ReadOnlySpan<SimEvent> events)
        {
            if (_source == null) return;
            Array.Clear(_playedThisStep, 0, _playedThisStep.Length);

            // 반복 피로 완화: 틱 단위 ±4% 피치 랜덤 (표현 전용 — 시뮬 Rng와 무관)
            if (events.Length > 0)
                _source.pitch = 1f + (UnityEngine.Random.value - 0.5f) * 0.08f;

            // 전멸 폭탄은 같은 틱에 적 사망 폭발음을 대량으로 몰고 온다. 폭발 채널을
            // 먼저 선점해 큰 볼륨으로 한 번만 울린다 — 그러지 않으면 사망음이 채널을
            // 먹어 폭탄이 작게 들리거나, 별도 채널로 두면 두 폭발음이 겹쳐 클리핑한다.
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type != SimEventType.BombActivated) continue;
                PlayOnce(2, _explosion, 1f);
                break;
            }

            for (int i = 0; i < events.Length; i++)
            {
                switch (events[i].Type)
                {
                    case SimEventType.PlayerFired:
                        // 발사음은 내지 않는다 — 주무기와 미사일 모두
                        // ("주무기랑 미사일 소리 둘다 꺼야 한다", 2026-07-30).
                        //
                        // 오토파이어가 기본 ON이라 발사가 쉬지 않고 일어난다. 거기에
                        // 주무기와 미사일이 각각 이 이벤트를 내므로 발사음이 끊이지 않고
                        // 울려 듣기 괴로웠다. 발사 자체는 탄이 화면에 보이고 명중하면
                        // 타격음이 나므로, 발사음이 없어도 피드백은 충분히 남는다.
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
                        // 오토파이어로 캡슐을 연달아 먹는 구간이 있어 이 소리가 가장 자주
                        // 울린다 — 0.9는 과했다. 차임 자체도 피크 0.6으로 낮게 만들었다.
                        PlayOnce(5, _pickup, 0.5f);
                        break;
                    case SimEventType.PowerUpLevelChanged:
                        PlayOnce(6, _powerup, 0.6f);
                        break;
                    case SimEventType.BossSpawned:
                        // 보스 등장은 획득 차임이 아니라 경보다. 차임을 얌전하게 바꾼 뒤
                        // 강화음으로 보스를 알리면 "좋은 일"처럼 들린다 (2026-08-02).
                        PlayOnce(7, _warning, 0.85f);
                        break;
                    case SimEventType.BossPhaseChanged:
                        PlayOnce(8, _hit, 1f);
                        break;
                    case SimEventType.StageCleared:
                        // 클리어는 BgmPlayer가 5.5초 팡파르(jingle_clear)를 0.9로 울린다.
                        // 여기서는 그 위에 얹는 짧은 확인음 정도로만 남긴다.
                        PlayOnce(9, _powerup, 0.45f);
                        break;
                    case SimEventType.BombAcquired:
                        // 캡슐보다 귀한 획득이라 pickup이 아니라 powerup 계열로 알린다.
                        PlayOnce(10, _powerup, 0.55f);
                        break;
                    case SimEventType.BombActivationRejectedEmpty:
                        // 재고 없이 눌렀다 — 짧고 작게. 버튼이 죽지 않았음을 알리는 정도다.
                        PlayOnce(11, _hit, 0.25f);
                        break;
                    case SimEventType.BossAttackTelegraphed:
                        // 위험 패턴 예고 — 눈과 귀 양쪽으로. 탄막 속에서는 화면 번쩍임을
                        // 놓치기 쉽다.
                        PlayOnce(12, _warning, 0.7f);
                        break;
                    case SimEventType.ObstacleDamaged:
                        // 장애물은 적과 달리 가만히 있는 과녁이라 지속 사격을 받는다.
                        // 적 피격(0.5)과 같은 볼륨이면 벽 하나 부수는 동안 화면 전체가
                        // 타격음으로 덮인다 — 채널도 EnemyHit과 나눠 두어야 같은 틱에
                        // 적과 장애물을 동시에 맞혔을 때 한쪽이 삼켜지지 않는다.
                        PlayOnce(13, _hit, 0.3f);
                        break;
                    case SimEventType.ObstacleDestroyed:
                        // 파괴는 격파와 같은 종류의 성과지만 적 격파(0.8)보다는 작다 —
                        // 장애물은 길을 여는 수단이지 목표가 아니다.
                        PlayOnce(14, _explosion, 0.6f);
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
