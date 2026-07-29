using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// BGM 레이어 (M4 완성): 테마별 루프 + 보스 트랙 전환 + 덕킹 + 징글.
    /// director 상태를 폴링만 한다 (순수 표현). 트랙은 Tools/SfxGen/bgmgen.py
    /// 프리셋 산출물 (시드 커밋으로 재현).
    /// - 보스전 진입/이탈: bgm_boss로 크로스 전환 (덕킹으로 이음새 완화)
    /// - 스테이지 클리어(AwaitingReward): 루프 덕킹 + 클리어 징글 1회
    /// - 게임오버(IsRunOver): 루프 정지 + 게임오버 스팅어 1회
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BgmPlayer : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] AudioSource _source;
        [SerializeField] string[] _themeIds;
        [SerializeField] AudioClip[] _clips;
        [SerializeField] AudioClip _bossClip;
        [SerializeField] AudioClip _clearJingle;
        [SerializeField] AudioClip _gameOverJingle;

        const float DuckLerpSpeed = 6f;

        float _baseVolume = 1f;   // 빌더가 지정한 믹스 레벨을 기준으로 덕킹/복원
        string _activeThemeId;
        bool _bossTrackActive;
        bool _wasAwaitingReward;
        int _jingledRunNumber = int.MinValue;
        float _duckUntilRealtime;

        void Awake()
        {
            if (_source != null)
                _baseVolume = _source.volume;
        }

        void Update()
        {
            if (_director == null || _source == null) return;

            // 게임오버: 루프 멈추고 스팅어 1회 (런당 1번)
            if (_director.IsRunOver)
            {
                if (_jingledRunNumber != _director.RunNumber)
                {
                    _jingledRunNumber = _director.RunNumber;
                    _source.Stop();
                    if (_gameOverJingle != null)
                        _source.PlayOneShot(_gameOverJingle, 0.9f);
                }
                return;
            }

            // 재출격 직후 루프 복구
            if (!_source.isPlaying && _source.clip != null && Time.timeScale > 0f)
                _source.Play();

            // 스테이지 클리어: 덕킹 + 클리어 징글 (AwaitingReward 상승 에지)
            bool awaiting = _director.AwaitingReward;
            if (awaiting && !_wasAwaitingReward)
            {
                Duck(2.2f);
                if (_clearJingle != null)
                    _source.PlayOneShot(_clearJingle, 0.9f);
            }
            _wasAwaitingReward = awaiting;

            // 보스 트랙 전환 (진입: 덕킹 후 교체, 이탈: 테마 트랙 복귀)
            bool bossActive = _director.BossActive;
            if (bossActive != _bossTrackActive)
            {
                _bossTrackActive = bossActive;
                Duck(0.5f);
                AudioClip target = bossActive && _bossClip != null
                    ? _bossClip
                    : FindClip(_director.CurrentThemeId);
                SwapClip(target);
            }
            else if (!bossActive)
            {
                // 일반 구간: 테마 변화 폴링
                string themeId = _director.CurrentThemeId;
                if (!string.IsNullOrEmpty(themeId) && themeId != _activeThemeId)
                {
                    _activeThemeId = themeId;
                    SwapClip(FindClip(themeId));
                }
            }

            // 덕킹 볼륨 복원
            float targetVolume = Time.realtimeSinceStartup < _duckUntilRealtime
                ? _baseVolume * 0.4f
                : _baseVolume;
            _source.volume = Mathf.MoveTowards(
                _source.volume, targetVolume, DuckLerpSpeed * Time.unscaledDeltaTime);
        }

        void Duck(float seconds)
        {
            _duckUntilRealtime = Mathf.Max(
                _duckUntilRealtime, Time.realtimeSinceStartup + seconds);
        }

        void SwapClip(AudioClip clip)
        {
            if (clip == null || _source.clip == clip) return;
            _source.clip = clip;
            _source.Play();
        }

        AudioClip FindClip(string themeId)
        {
            if (_themeIds == null || _clips == null) return null;
            int count = Mathf.Min(_themeIds.Length, _clips.Length);
            for (int i = 0; i < count; i++)
                if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                    return _clips[i];
            return _clips.Length > 0 ? _clips[0] : null;
        }
    }
}
