using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 마지막 목숨 경고: 붉은 비네트 맥동 + 주기적 경고음.
    ///
    /// HP가 사라지고 실드 스톡이 유일한 내구도가 된 뒤로(REQ-040), 위험 신호는
    /// **스톡 0**이다 — 그 상태에서 한 번만 더 맞으면 즉사한다. 예전 `PlayerHp == 1`
    /// 조건은 이제 살아 있는 동안 항상 참이라(생존 1 / 사망 0 호환 프로퍼티) 쓸 수 없다.
    ///
    /// 순수 표현 — director의 상태를 읽기만 한다. 접근성(플래시 감소) 설정을 존중한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LowHpWarning : MonoBehaviour
    {
        const float BeepInterval = 1.1f;

        [SerializeField] BattleDirector _director;
        [SerializeField] JuiceDirector _juice;
        [SerializeField] AudioSource _source;
        [SerializeField] AudioClip _warningClip;

        const float EdgeThickness = 10f;   // 640×360 기준 픽셀

        GameObject _root;
        Image[] _edges;
        float _beepTimer;

        /// <summary>화면 한 변에 붙는 경고 띠. 두 앵커를 잇는 방향으로 늘어난다.</summary>
        static Image CreateEdge(
            RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 inward)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            // 가로변이면 높이만, 세로변이면 폭만 준다.
            bool horizontal = Mathf.Approximately(anchorMin.y, anchorMax.y);
            rect.sizeDelta = horizontal
                ? new Vector2(0f, EdgeThickness)
                : new Vector2(EdgeThickness, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = -inward * (EdgeThickness * 0.5f);
            return image;
        }

        void Start()
        {
            if (_source == null)
            {
                // 경고음 전용 소스 (SFX 스로틀·피치 랜덤과 분리)
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }

            var canvas = UiKit.CreateCanvas("LowHpCanvas", 42);
            canvas.transform.SetParent(transform, false);

            // 화면 전체를 덮으면 스톡 0으로 버티는 동안 게임이 보이지 않는다 (실제로
            // 폰 스크린샷이 온통 붉게 나왔다). 위험은 가장자리 띠로만 알린다 — 시야
            // 중앙을 비워 두면서도 주변시로 충분히 읽힌다.
            _root = new GameObject("EdgeWarning");
            _root.transform.SetParent(canvas.transform, false);
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _edges = new Image[4];
            _edges[0] = CreateEdge(rootRect, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.up);
            _edges[1] = CreateEdge(rootRect, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.down);
            _edges[2] = CreateEdge(rootRect, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.left);
            _edges[3] = CreateEdge(rootRect, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.right);
            _root.SetActive(false);
        }

        void Update()
        {
            if (_director == null || _root == null) return;

            bool danger = !_director.IsRunFinished && _director.ShieldRemaining == 0
                          && Time.timeScale > 0f;
            if (_root.activeSelf != danger)
            {
                _root.SetActive(danger);
                _beepTimer = 0f;
            }
            if (!danger) return;

            // 띠는 좁으므로 전체 딤보다 진하게 써도 시야를 막지 않는다.
            float peak = _juice != null && _juice.FlashReduced ? 0.35f : 0.75f;
            float pulse = (Mathf.Sin(Time.unscaledTime * 5.2f) + 1f) * 0.5f;
            var color = new Color(0.95f, 0.08f, 0.12f, 0.25f + pulse * peak);
            for (int i = 0; i < _edges.Length; i++)
                _edges[i].color = color;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer <= 0f)
            {
                _beepTimer = BeepInterval;
                if (_source != null && _warningClip != null)
                    _source.PlayOneShot(_warningClip, 0.55f);
            }
        }
    }
}
