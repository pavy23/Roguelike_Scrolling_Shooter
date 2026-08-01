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

        // 적탄 차단 스파크 (REQ-101 C-A). blocksEnemyBullets 장애물이 적탄을 먹는 일은
        // **매우 잦다** — 여기서 폭발이나 큰 섬광을 쓰면 화면이 지저분해지고, 정작
        // 봐야 할 남은 탄이 묻힌다. "탄이 사라진 이유"만 알리는 최소 신호다:
        // 7px 흰 점 0.1초. 위치는 Core가 준 탄 소거 좌표(= 장애물 표면)를 그대로 쓴다.
        const int BlockSparkCount = 4;      // 동시 표시 상한 — 넘치면 오래된 것부터 재사용
        const float BlockSparkSeconds = 0.1f;
        const float BlockSparkPixels = 7f;  // 6~8px 사이
        const float AssetsPixelsPerUnit = 16f;
        SpriteRenderer[] _blockSparks;
        float[] _blockSparkAges;
        int _blockSparkCursor;

        static readonly Color BlockSparkColor = new Color(1f, 1f, 1f, 1f);

        // 스프라이트 스케일 1이 만드는 월드 크기. px_white는 2px/PPU16 = 0.125u라
        // localScale에 월드 길이를 그대로 넣으면 1/8 크기로 그려진다 (2026-08-02 수정).
        float _unitX = 1f, _unitY = 1f;

        static readonly Color WallColor = new Color(0.42f, 0.26f, 0.30f, 0.95f);
        static readonly Color WallFlashColor = new Color(1f, 0.5f, 0.45f, 1f);

        void Start()
        {
            var parent = _root != null ? _root : transform;

            if (_pixelSprite != null)
            {
                Vector3 size = _pixelSprite.bounds.size;
                if (size.x > 0.0001f) _unitX = size.x;
                if (size.y > 0.0001f) _unitY = size.y;
            }

            _wallTop = CreateWall(parent, "CorridorWallTop");
            _wallBottom = CreateWall(parent, "CorridorWallBottom");
            CreateBlockSparks(parent);

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

        /// <summary>
        /// 차단 스파크는 풀을 미리 깔아 둔다 — 게임 루프에서 Instantiate 금지(CLAUDE.md).
        /// 4개면 충분하다: 같은 틱 차단은 BattleDirector가 1회로 접어 보내므로
        /// 최대 동시 표시는 0.1초 안에 들어온 프레임 수(60fps 기준 6프레임)보다 훨씬 적다.
        /// </summary>
        void CreateBlockSparks(Transform parent)
        {
            _blockSparks = new SpriteRenderer[BlockSparkCount];
            _blockSparkAges = new float[BlockSparkCount];
            float world = BlockSparkPixels / AssetsPixelsPerUnit;
            for (int i = 0; i < BlockSparkCount; i++)
            {
                var go = new GameObject($"BulletBlockSpark_{i}");
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(world / _unitX, world / _unitY, 1f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _pixelSprite;
                renderer.color = BlockSparkColor;
                renderer.sortingOrder = 12;   // 장애물(9) 위, 통로 벽(14) 아래
                renderer.enabled = false;
                _blockSparks[i] = renderer;
                _blockSparkAges[i] = BlockSparkSeconds;
            }
        }

        /// <summary>
        /// 적탄이 장애물에 먹힌 지점에 스파크를 찍는다 (BattleDirector가 호출).
        /// 좌표는 Core의 EnemyBulletBlocked 이벤트 X/Y — 뷰가 위치를 추정하지 않는다.
        /// </summary>
        public void FlashBulletBlock(Vector3 worldPosition)
        {
            if (_blockSparks == null || _blockSparks.Length == 0) return;
            int index = _blockSparkCursor;
            _blockSparkCursor = (_blockSparkCursor + 1) % _blockSparks.Length;
            var renderer = _blockSparks[index];
            if (renderer == null) return;
            renderer.transform.localPosition = worldPosition;
            renderer.color = BlockSparkColor;
            renderer.enabled = true;
            _blockSparkAges[index] = 0f;
        }

        void SyncBlockSparks()
        {
            if (_blockSparks == null) return;
            for (int i = 0; i < _blockSparks.Length; i++)
            {
                var renderer = _blockSparks[i];
                if (renderer == null || !renderer.enabled) continue;

                float age = _blockSparkAges[i] + Time.deltaTime;
                _blockSparkAges[i] = age;
                if (age >= BlockSparkSeconds)
                {
                    renderer.enabled = false;
                    continue;
                }
                // 알파만 떨어뜨린다 — 크기가 커지면 폭발로 읽혀 "차단"의 무게가 어긋난다.
                var color = BlockSparkColor;
                color.a = 1f - age / BlockSparkSeconds;
                renderer.color = color;
            }
        }

        void LateUpdate()
        {
            if (_director == null) return;
            SyncCorridor();
            SyncVision();
            SyncTimeLimit();
            SyncBlockSparks();
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

            PlaceWall(_wallTop, maxY, fieldHalf, width, _unitX, _unitY);
            PlaceWall(_wallBottom, -fieldHalf, minY, width, _unitX, _unitY);

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

        static void PlaceWall(
            SpriteRenderer renderer, float fromY, float toY, float width, float unitX, float unitY)
        {
            float height = Mathf.Max(0f, toY - fromY);
            renderer.transform.localPosition = new Vector3(0f, (fromY + toY) * 0.5f, 0f);
            renderer.transform.localScale = new Vector3(width / unitX, height / unitY, 1f);
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
