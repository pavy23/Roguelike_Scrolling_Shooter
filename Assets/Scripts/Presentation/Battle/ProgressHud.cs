using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 스테이지 진행도 HUD + 스테이지 진입 배너 (REQ-032, REQ-054에서 구간 표시로 전환).
    /// 런에서 "지금 어디쯤인가"를 알려 주지 않으면 여정 감각이 생기지 않는다.
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
        Text _contractText;
        string _shownContractId;
        Text _bannerText;
        GameObject _bannerRoot;
        int _shownBiome = -1, _shownRoom = -1;
        RunStageSection _shownSection = (RunStageSection)(-1);
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

            // 활성 계약 (REQ-070). 계약이 스테이지 전체에 걸리는데 표시가 없으면
            // "왜 적이 많지?"가 버그로 읽힌다 — 내가 고른 조건임을 계속 보여 준다.
            _contractText = UiKit.CreateCornerText(canvas.transform, _font, "", 9,
                UiKit.TextDim, new Vector2(0f, 1f), new Vector2(8f, -44f),
                TextAnchor.UpperLeft, "Contract");
            UiKit.AddShadow(_contractText);

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
            var section = _director.StageSection;
            if (biome != _shownBiome || room != _shownRoom || section != _shownSection)
            {
                _shownBiome = biome;
                _shownRoom = room;
                _shownSection = section;
                _progressText.text = BuildProgress(biome, section);
            }

            var contract = _director.ActiveContract;
            string contractId = contract != null ? contract.Id : null;
            if (!string.Equals(contractId, _shownContractId, System.StringComparison.Ordinal))
            {
                _shownContractId = contractId;
                if (contract == null
                    || contract.RiskTier == Shmup.Core.Simulation.ContractRiskTier.Safe)
                {
                    // 표준 항로는 표시하지 않는다 — 무보정 상태가 기본값이다.
                    _contractText.text = "";
                }
                else
                {
                    string name = contract.Id.StartsWith("contract_")
                        ? contract.Id.Substring("contract_".Length)
                        : contract.Id;
                    _contractText.text =
                        $"CONTRACT: {name.Replace('_', ' ').ToUpperInvariant()}";
                    _contractText.color =
                        contract.RiskTier == Shmup.Core.Simulation.ContractRiskTier.Low
                            ? new UnityEngine.Color(0.35f, 0.65f, 1f, 1f)
                            : contract.RiskTier == Shmup.Core.Simulation.ContractRiskTier.Extreme
                                ? new UnityEngine.Color(1f, 0.32f, 0.28f, 1f)
                                : new UnityEngine.Color(1f, 0.62f, 0.25f, 1f);
                }
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

        /// <summary>
        /// 룸 카운터 대신 **지금 어느 구간인지**를 보여 준다. 분기가 사라지고 스테이지
        /// 안이 전반 → 중간보스 → 후반 → 보스로 흐르게 됐으므로(REQ-054), 남은 룸 수보다
        /// "다음에 무엇이 오는지"가 플레이어에게 쓸모 있는 정보다.
        /// </summary>
        string BuildProgress(int biome, RunStageSection section)
        {
            int biomeCount = Mathf.Max(1, _director.BiomeCount);
            var sb = new System.Text.StringBuilder(48);
            sb.Append("STAGE ").Append(biome).Append('/').Append(biomeCount);
            sb.Append("   ").Append(SectionLabel(section));
            return sb.ToString();
        }

        static string SectionLabel(RunStageSection section)
        {
            switch (section)
            {
                case RunStageSection.Opening: return "ADVANCE  >  mid-boss";
                case RunStageSection.MidBoss: return "MID-BOSS";
                case RunStageSection.Closing: return "ADVANCE  >  boss";
                case RunStageSection.StageBoss: return "BOSS";
                case RunStageSection.HiddenOpening: return "!! UNCHARTED !!";
                case RunStageSection.HiddenBoss: return "!! COLOSSUS !!";
                default: return "";
            }
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
