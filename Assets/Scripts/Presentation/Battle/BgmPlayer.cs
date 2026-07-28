using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 테마별 BGM 루프 (M4). director의 현재 테마를 폴링해 트랙을 교체한다.
    /// 트랙은 Tools/SfxGen/bgmgen.py 테마 프리셋 산출물 (시드 커밋으로 재현).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BgmPlayer : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] AudioSource _source;
        [SerializeField] string[] _themeIds;
        [SerializeField] AudioClip[] _clips;

        string _activeThemeId;

        void Update()
        {
            if (_director == null || _source == null) return;
            string themeId = _director.CurrentThemeId;
            if (string.IsNullOrEmpty(themeId) || themeId == _activeThemeId) return;

            AudioClip clip = FindClip(themeId);
            _activeThemeId = themeId;
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
