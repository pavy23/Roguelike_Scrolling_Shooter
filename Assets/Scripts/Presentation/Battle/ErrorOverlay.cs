using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// C# 예외와 오류 로그를 화면에 띄운다.
    ///
    /// 왜 필요한가: 원격 플레이(폰 WebGL)에서 문제가 나면 콘솔을 볼 수 없다. index.html이
    /// JS 오류는 화면에 표시하지만 **Unity의 C# 예외는 JS로 새어 나오지 않아** 아무 단서 없이
    /// "게임이 멈춘다"만 남는다. 실제로 "중간보스 직후 멈춘다"는 보고를 받고도 원인을
    /// 추측만 할 수 있었다.
    ///
    /// Update에서 예외가 던져지면 그 컴포넌트의 Update가 매 프레임 실패하므로 화면이
    /// 굳은 것처럼 보인다 — 정지의 정체가 예외인 경우가 많다. 그것을 눈에 보이게 만든다.
    ///
    /// 배포판에도 남긴다. 원격에서 진단할 다른 수단이 없고, 오류가 없으면 아무것도
    /// 그리지 않으므로 정상 플레이에는 영향이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ErrorOverlay : MonoBehaviour
    {
        [SerializeField] Font _font;

        [Tooltip("같은 메시지를 이 횟수까지만 표시한다 — 매 프레임 반복되는 예외로 화면이 덮이지 않게.")]
        [SerializeField] int _maxRepeats = 3;

        Text _text;
        Image _panel;
        readonly List<string> _lines = new List<string>(8);
        readonly Dictionary<string, int> _seen = new Dictionary<string, int>(8);

        const int MaxLines = 6;

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void Start()
        {
            var canvas = UiKit.CreateCanvas("ErrorCanvas", 200);   // 무엇보다 위에
            canvas.transform.SetParent(transform, false);

            _panel = UiKit.CreateDim(canvas.transform, new Color(0.35f, 0.02f, 0.05f, 0.88f), "ErrorPanel");
            var rect = _panel.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 96f);
            rect.anchoredPosition = Vector2.zero;

            _text = UiKit.CreateTextStretch(_panel.rectTransform, _font, "", 9,
                new Color(1f, 0.86f, 0.86f, 1f), TextAnchor.LowerLeft, 5f, "ErrorText");

            _panel.gameObject.SetActive(false);
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            // 반복 억제: 예외는 매 프레임 같은 줄을 쏟아내므로 몇 번만 남긴다.
            if (!_seen.TryGetValue(message, out int count)) count = 0;
            _seen[message] = count + 1;
            if (count >= _maxRepeats) return;

            // 스택의 첫 줄만 붙인다 — 폰 화면에 전체 스택은 들어가지 않고,
            // 어느 메서드에서 났는지가 대부분의 단서다.
            string where = "";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int nl = stackTrace.IndexOf('\n');
                where = "  @ " + (nl > 0 ? stackTrace.Substring(0, nl) : stackTrace);
                if (where.Length > 110) where = where.Substring(0, 110);
            }

            _lines.Add(message.Length > 150 ? message.Substring(0, 150) : message);
            if (where.Length > 0) _lines.Add(where);
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);

            if (_text != null) _text.text = string.Join("\n", _lines);
            if (_panel != null && !_panel.gameObject.activeSelf)
                _panel.gameObject.SetActive(true);
        }
    }
}
