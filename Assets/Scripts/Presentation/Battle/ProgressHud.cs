using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 바이옴/룸 진행도 HUD + 바이옴 진입 배너 (REQ-032).
    /// 22분 런에서 "지금 어디쯤인가"를 알려 주지 않으면 여정 감각이 생기지 않는다.
    /// 좌상단에 BIOME n/5 · ROOM m/6 을 점 표시로, 바이옴이 바뀌면 중앙에 테마 배너를 띄운다.
    /// 순수 표현 — director 상태를 읽기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressHud : MonoBehaviour
    {
        const float BannerSeconds = 2.2f;

        [SerializeField] BattleDirector _director;
        [SerializeField] Font _font;
        [SerializeField] Font _fontBold;
        [SerializeField] string[] _themeIds;
        [SerializeField] string[] _themeNames;

        Text _progressText;
        Text _bannerText;
        GameObject _bannerRoot;
        int _shownBiome = -1, _shownRoom = -1;
        int _bannerBiome = -1;
        float _bannerAge = float.MaxValue;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("ProgressCanvas", 43);
            canvas.transform.SetParent(transform, false);

            _progressText = UiKit.CreateCornerText(canvas.transform, _font, "", 11,
                UiKit.TextDim, new Vector2(0f, 1f), new Vector2(8f, -30f),
                TextAnchor.UpperLeft, "Progress");
            UiKit.AddShadow(_progressText);

            // 바이옴 진입 배너 (중앙, 짧게)
            var band = new GameObject("BiomeBanner");
            band.transform.SetParent(canvas.transform, false);
            var image = band.AddComponent<Image>();
            image.color = new Color(0.03f, 0.05f, 0.12f, 0.72f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 44f);
            _bannerText = UiKit.CreateTextStretch(rect, _fontBold, "", 20,
                UiKit.TextAccent, TextAnchor.MiddleCenter, 0f, "BannerText");
            _bannerRoot = band;
            _bannerRoot.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _progressText == null) return;

            int biome = _director.BiomeIndex;
            int room = _director.RoomIndex;
            if (biome != _shownBiome || room != _shownRoom)
            {
                _shownBiome = biome;
                _shownRoom = room;
                _progressText.text = BuildProgress(biome, room);
            }

            // 바이옴이 바뀐 순간 배너
            if (biome != _bannerBiome && biome > 0 && !_director.IsRunFinished)
            {
                _bannerBiome = biome;
                _bannerAge = 0f;
                _bannerText.text = $"BIOME {biome}  -  {ThemeName(_director.CurrentThemeId)}";
            }

            if (_bannerAge < BannerSeconds)
            {
                _bannerAge += Time.deltaTime;
                if (!_bannerRoot.activeSelf) _bannerRoot.SetActive(true);
                // 끝에서 부드럽게 사라짐
                float fade = Mathf.Clamp01((BannerSeconds - _bannerAge) / 0.5f);
                var color = UiKit.TextAccent;
                color.a = fade;
                _bannerText.color = color;
            }
            else if (_bannerRoot.activeSelf)
            {
                _bannerRoot.SetActive(false);
            }
        }

        string BuildProgress(int biome, int room)
        {
            int biomeCount = Mathf.Max(1, _director.BiomeCount);
            int roomsPerBiome = Mathf.Max(1, _director.RoomsPerBiome);
            var sb = new System.Text.StringBuilder(48);
            sb.Append("BIOME ").Append(biome).Append('/').Append(biomeCount);
            sb.Append("   ROOM ");
            // 점 표시로 룸 진행을 한눈에: ●●●○○○
            for (int i = 1; i <= roomsPerBiome; i++)
                sb.Append(i <= room ? '#' : '.');
            sb.Append(' ').Append(Mathf.Min(room, roomsPerBiome)).Append('/').Append(roomsPerBiome);
            return sb.ToString();
        }

        string ThemeName(string themeId)
        {
            if (_themeIds == null || _themeNames == null || themeId == null) return "";
            int count = Mathf.Min(_themeIds.Length, _themeNames.Length);
            for (int i = 0; i < count; i++)
                if (string.Equals(_themeIds[i], themeId, System.StringComparison.Ordinal))
                    return _themeNames[i];
            return themeId;
        }
    }
}
