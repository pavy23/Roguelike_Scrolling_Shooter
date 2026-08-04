using System;
using System.Collections.Generic;
using Shmup.Core.Simulation;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// Core 시뮬 이벤트(REQ-005)를 효과음으로 번역한다. 순수 표현 — 게임 상태에 영향 없음.
    /// 채택음은 Tools/SfxGen/sfxgen_snes.py 산출물이고, 획득·강화 차임만
    /// Tools/SfxGen/sfxgen_chime.py 후보 a(유리 벨 완전5도)로 교체했다
    /// ("파워업과 봄 아이템 ... 사운드가 너무 거슬려", 2026-08-02).
    /// 레이저 예고·발사음은 Tools/SfxGen/sfxgen_laser.py 후보 b(험 + 흡기)다
    /// ("레이저 발사하는 소리도 따로 있어야 할듯", 2026-08-02).
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
        [SerializeField] AudioClip _laserCharge;   // 적·지형 레이저 예고 차지 (REQ-042)
        [SerializeField] AudioClip _laserFire;     // 적·지형 레이저 발사

        /// <summary>
        /// 초대형 빔 발사음. 반폭이 <see cref="HeavyBeamHalfWidthSubUnits"/>를
        /// 넘는 빔에만 쓴다 (사람 지시 2026-08-05: "3페이즈 대형 레이저는 소리가
        /// 너무 썰렁해서 대형 레이저를 쏘는 박력있는 레이저음으로").
        ///
        /// 기존 발사음은 잡몹 빔(반폭 0.25유닛)에 맞춰 짧고 얇다. 화면을 관통하는
        /// 반폭 5유닛 빔에 그것을 붙이면 크기와 소리가 어긋난다.
        /// </summary>
        [SerializeField] AudioClip _laserFireHeavy;

        /// <summary>
        /// 이 반폭(서브유닛)을 넘으면 초대형 빔 소리를 쓴다. 2유닛 = 512 —
        /// 잡몹 빔(0.25)과 갑판 포탑(0.75~0.875)은 아래, 코어 빔(5.0)과 레비아탄
        /// 레일건(5.625)은 위다.
        /// </summary>
        const int HeavyBeamHalfWidthSubUnits = 512;

        /// <summary>선택 함선의 주무기 계열 — 발사음을 계열별로 바꾼다 (REQ-022 후속).</summary>
        public Shmup.Core.WeaponType WeaponFamily { get; set; } = Shmup.Core.WeaponType.Vulcan;

        AudioClip FireClip =>
            WeaponFamily == Shmup.Core.WeaponType.Laser && _laserBeam != null ? _laserBeam :
            WeaponFamily == Shmup.Core.WeaponType.Spread && _spreadShot != null ? _spreadShot :
            _laser;

        [Range(0f, 1f)]
        [SerializeField] float _laserVolume = 0.35f;

        // 이번 스텝에서 이미 재생한 클립 (틱당 1회 제한)
        readonly bool[] _playedThisStep = new bool[17];

        // ── 레이저 소리 (2026-08-02 사람 요청: "레이저 발사하는 소리도 따로 있어야 할듯") ──
        //
        // **소스 구분이 필요하다.** LaserFired의 Arg는 소스 종류가 아니라 빔 반폭이라
        // 이벤트만으로는 적 레이저와 플레이어 PRISM BEAM을 가를 수 없다. 소스가 실리는
        // 곳은 LaserTelegraphStarted의 Arg뿐인데, 플레이어 빔은 예고 단계가 없어 그
        // 이벤트를 아예 내지 않는다 — 그래서 **예고를 낸 id만 적대 레이저로 기억**하고,
        // LaserFired가 그 목록에 없으면 플레이어 빔으로 판정한다.
        //
        // 플레이어 빔을 굳이 낮추는 이유: 지속빔이라 오토파이어로 계속 재점화되면
        // 발사음이 끊이지 않는다. 완전히 지우면 "빔이 켜졌다"는 확인이 사라지므로
        // 들릴락 말락 한 볼륨만 남긴다.
        readonly HashSet<int> _hostileLasers = new HashSet<int>();

        /// <summary>추적 id 상한. 런이 길어지면 LaserEnded를 못 본 id가 쌓일 수 있다.</summary>
        const int MaxTrackedLasers = 64;

        const float HostileLaserFireVolume = 0.5f;

        /// <summary>초대형 빔은 더 크게 — 화면을 관통하는 것이 조용하면 안 된다.</summary>
        const float HeavyBeamFireVolume = 0.72f;
        const float PlayerBeamFireVolume = 0.15f;

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

            // 같은 이유로 적대 레이저 발사가 채널을 먼저 잡는다. 플레이어 빔 점화가
            // 같은 틱에 끼면 (거의 안 들리는 0.15로) 적 레이저를 삼켜 버린다 —
            // 나를 노리는 빔이 내가 켠 빔에 묻히는 것이 가장 나쁜 경우다.
            // **첫 빔이 아니라 가장 굵은 빔으로 고른다.** 처음에는 첫 이벤트에서
            // 곧장 break했는데, 코어의 초대형 빔은 포탑 빔들과 같은 틱에 점화되는
            // 일이 흔하다. 얇은 쪽이 먼저 오면 채널을 먹고 끝나서, 화면에는 반폭
            // 5유닛 빔이 나가는데 귀에는 잡몹 발사음만 들렸다.
            int widestHalfWidth = -1;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Type != SimEventType.LaserFired
                    || !_hostileLasers.Contains(events[i].EntityId))
                    continue;
                // LaserFired의 Arg는 그 빔의 반폭이다 — 굵기로 소리를 고른다.
                if (events[i].Arg > widestHalfWidth)
                    widestHalfWidth = events[i].Arg;
            }
            if (widestHalfWidth >= 0)
            {
                bool heavy = widestHalfWidth >= HeavyBeamHalfWidthSubUnits
                    && _laserFireHeavy != null;
                PlayOnce(
                    16,
                    heavy ? _laserFireHeavy : _laserFire,
                    heavy ? HeavyBeamFireVolume : HostileLaserFireVolume);
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
                    case SimEventType.LaserTelegraphStarted:
                        // 예고는 경고지 위협 그 자체가 아니다 — 절제된 볼륨으로 깐다.
                        // 탄막 속에서 예고선을 놓쳐도 귀로 "곧 온다"가 남아야 한다.
                        // 플레이어 빔은 이 이벤트를 내지 않으므로 전부 적대 레이저다.
                        if (_hostileLasers.Count >= MaxTrackedLasers)
                            _hostileLasers.Clear();
                        _hostileLasers.Add(events[i].EntityId);
                        PlayOnce(15, _laserCharge, 0.35f);
                        break;
                    case SimEventType.LaserFired:
                        // 적대 발사는 위 선점 패스가 이미 큰 볼륨으로 울렸다. 여기 남는
                        // 것은 플레이어 빔 점화뿐이고, 채널이 먹혔으면 조용히 넘어간다.
                        PlayOnce(16, _laserFire, PlayerBeamFireVolume);
                        break;
                    case SimEventType.LaserEnded:
                        _hostileLasers.Remove(events[i].EntityId);
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
