using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 마지막 목숨 경고: 붉은 가장자리 띠 맥동. **소리는 내지 않는다.**
    ///
    /// HP가 사라지고 실드 스톡이 유일한 내구도가 된 뒤로(REQ-040), 위험 신호는
    /// **스톡 0**이다 — 그 상태에서 한 번만 더 맞으면 즉사한다. 예전 `PlayerHp == 1`
    /// 조건은 이제 살아 있는 동안 항상 참이라(생존 1 / 사망 0 호환 프로퍼티) 쓸 수 없다.
    ///
    /// 스톡 0은 잠깐이 아니라 **런 후반 내내 이어지는 상태**다. 1.1초마다 울리던
    /// 경고음은 그동안 쉬지 않고 반복돼 소음이 됐다(사람 지시 2026-08-02: "빨갛게
    /// 깜빡거리기만 하고 소리는 끄자"). 위험 신호는 시각 채널만 쓴다 — 주변시로
    /// 읽히는 가장자리 맥동은 사격음 위에 얹히지 않으면서도 계속 보인다.
    ///
    /// 순수 표현 — director의 상태를 읽기만 한다. 접근성(플래시 감소) 설정을 존중한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LowHpWarning : MonoBehaviour
    {
        [SerializeField] BattleDirector _director;
        [SerializeField] JuiceDirector _juice;

        const float EdgeThickness = 10f;   // 640×360 기준 픽셀

        GameObject _root;
        Image[] _edges;

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
                _root.SetActive(danger);
            if (!danger) return;

            // 띠는 좁으므로 전체 딤보다 진하게 써도 시야를 막지 않는다.
            float peak = _juice != null && _juice.FlashReduced ? 0.35f : 0.75f;
            float pulse = (Mathf.Sin(Time.unscaledTime * 5.2f) + 1f) * 0.5f;
            var color = new Color(0.95f, 0.08f, 0.12f, 0.25f + pulse * peak);
            for (int i = 0; i < _edges.Length; i++)
                _edges[i].color = color;
        }
    }
}
