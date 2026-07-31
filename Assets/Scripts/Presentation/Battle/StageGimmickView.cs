using Shmup.Core.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 스테이지 기믹의 시각화 (REQ-055): 통로 벽, 시야 제한 구름, 드리프트 흐름,
    /// 제한 시간 카운트다운.
    ///
    /// 두 가지는 **없으면 불공정하다**:
    /// - 통로 벽이 보이지 않으면 안 보이는 벽에 부딪힌다.
    /// - 제한 시간을 알리지 않으면 갑자기 죽는다 (초과는 방어막·무적을 무시하는 즉사다).
    ///
    /// 순수 표현이다. 특히 드리프트는 Core가 이미 기체 위치에 합성하므로
    /// **여기서 기체를 추가로 움직이면 이중 적용이 된다** — 배경 흐름으로만 보여 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageGimmickView : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] Sprite _pixelSprite;
        [SerializeField] Font _font;
        [SerializeField] Transform _root;

        [Tooltip("제한 시간이 이 초 이하로 남으면 임박 경고를 켠다.")]
        [SerializeField] float _warnSeconds = 10f;

        SpriteRenderer _wallTop, _wallBottom;
        Image _visionOverlay;
        Text _timerText;
        float _wallFlash;

        static readonly Color WallColor = new Color(0.42f, 0.26f, 0.30f, 0.95f);
        static readonly Color WallFlashColor = new Color(1f, 0.5f, 0.45f, 1f);

        void Start()
        {
            var parent = _root != null ? _root : transform;

            _wallTop = CreateWall(parent, "CorridorWallTop");
            _wallBottom = CreateWall(parent, "CorridorWallBottom");

            var canvas = UiKit.CreateCanvas("GimmickCanvas", 44);
            canvas.transform.SetParent(transform, false);

            // 시야 제한: 화면 전체를 덮되 옅게. 완전히 가리면 플레이가 불가능해지고,
            // 너무 옅으면 기믹이 없는 것과 같다.
            _visionOverlay = UiKit.CreateDim(
                canvas.transform, new Color(0.16f, 0.13f, 0.28f, 0f), "VisionCloud");
            _visionOverlay.gameObject.SetActive(false);

            _timerText = UiKit.CreateCornerText(canvas.transform, _font, "", 12,
                UiKit.TextAccent, new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                TextAnchor.UpperCenter, "TimeLimit");
            UiKit.AddShadow(_timerText);
            _timerText.gameObject.SetActive(false);
        }

        SpriteRenderer CreateWall(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _pixelSprite;
            renderer.color = WallColor;
            renderer.sortingOrder = 14;   // 적보다 앞, HUD보다 뒤
            renderer.enabled = false;
            return renderer;
        }

        void LateUpdate()
        {
            if (_director == null) return;
            SyncCorridor();
            SyncVision();
            SyncTimeLimit();
        }

        void SyncCorridor()
        {
            if (_wallTop == null) return;
            var env = _director.Environment;
            bool active = env.HasCorridor;
            if (_wallTop.enabled != active)
            {
                _wallTop.enabled = active;
                _wallBottom.enabled = active;
            }
            if (!active) return;

            // Core는 통로의 통과 가능 구간을 서브유닛으로 준다. 그 밖을 벽으로 채운다.
            float minY = env.CorridorMinY / (float)SimSpace.SubUnitsPerWorldUnit;
            float maxY = env.CorridorMaxY / (float)SimSpace.SubUnitsPerWorldUnit;
            float fieldHalf =
                SimSpace.PlayfieldHalfHeightSubUnits / (float)SimSpace.SubUnitsPerWorldUnit;
            float width =
                2f * SimSpace.PlayfieldHalfWidthSubUnits / SimSpace.SubUnitsPerWorldUnit;

            PlaceWall(_wallTop, maxY, fieldHalf, width);
            PlaceWall(_wallBottom, -fieldHalf, minY, width);

            // 벽에 닿으면 번쩍여서 "여기가 벽이다"를 알린다.
            if (_wallFlash > 0f)
            {
                _wallFlash -= Time.deltaTime;
                var color = Color.Lerp(WallColor, WallFlashColor, Mathf.Clamp01(_wallFlash * 4f));
                _wallTop.color = color;
                _wallBottom.color = color;
            }
            else if (_wallTop.color != WallColor)
            {
                _wallTop.color = WallColor;
                _wallBottom.color = WallColor;
            }
        }

        static void PlaceWall(SpriteRenderer renderer, float fromY, float toY, float width)
        {
            float height = Mathf.Max(0f, toY - fromY);
            renderer.transform.localPosition = new Vector3(0f, (fromY + toY) * 0.5f, 0f);
            renderer.transform.localScale = new Vector3(width, height, 1f);
        }

        /// <summary>벽 접촉 이벤트를 받아 번쩍임을 예약한다 (BattleDirector가 호출).</summary>
        public void FlashCorridorContact() => _wallFlash = 0.25f;

        void SyncVision()
        {
            if (_visionOverlay == null) return;
            bool obscured = _director.VisionObscured && !_director.IsRunFinished;
            if (_visionOverlay.gameObject.activeSelf != obscured)
                _visionOverlay.gameObject.SetActive(obscured);
            if (!obscured) return;

            // 옅게 흐르는 구름 — 알파를 천천히 흔들어 정적인 딤이 아니라 흐름으로 읽히게.
            float drift = (Mathf.Sin(Time.time * 0.6f) + 1f) * 0.5f;
            var color = _visionOverlay.color;
            color.a = 0.34f + drift * 0.14f;
            _visionOverlay.color = color;
        }

        void SyncTimeLimit()
        {
            if (_timerText == null) return;
            int remaining = _director.RemainingTimeTicks;
            bool active = remaining > 0 && !_director.IsRunFinished;
            if (_timerText.gameObject.activeSelf != active)
                _timerText.gameObject.SetActive(active);
            if (!active) return;

            float seconds = remaining / (float)SimSpace.TicksPerSecond;
            bool urgent = seconds <= _warnSeconds;
            _timerText.text = urgent
                ? $"!! {seconds:0.0} !!"
                : $"TIME  {Mathf.CeilToInt(seconds)}";
            // 임박하면 붉게 맥동한다 — 초과는 즉사이므로 강하게 알려야 한다.
            _timerText.color = urgent
                ? Color.Lerp(UiKit.TextDanger, Color.white,
                    (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f)
                : UiKit.TextAccent;
        }
    }
}
