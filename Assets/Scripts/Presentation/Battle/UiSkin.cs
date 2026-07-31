using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 코드 생성 9-slice UI 스킨 ("사각 배경에 글자만 있어서 썰렁해" — 2026-07-31).
    ///
    /// 픽셀 아트 베벨/프레임을 에셋 없이 런타임에 한 번 그려 캐시한다. UI는 어차피
    /// 코드로 조립하므로 텍스처도 코드가 그리는 쪽이 임포트 파이프라인(보더 메타,
    /// 필터 설정)을 타지 않아 리비전 관리가 깔끔하다.
    ///
    /// 규약:
    /// - <see cref="Button"/>·<see cref="Frame"/>은 **그레이스케일**로 그린다 —
    ///   최종 색은 <c>Image.color</c> 틴트가 정한다. 덕분에 기존 화면들의 재색칠
    ///   계약(보상 커서 금색, 계약 위험도 색, 폭탄 자홍/회색)이 그대로 동작한다.
    /// - PPU 100 = CanvasScaler referencePixelsPerUnit 기본값이라 보더 픽셀 수가
    ///   그대로 640×360 캔버스 픽셀이 된다 (정수 배율은 PixelUiScaler가 보장).
    /// </summary>
    public static class UiSkin
    {
        const int N = 18;   // 텍스처 한 변
        const int B = 6;    // 9-slice 보더

        static Sprite _button, _frame, _fill, _rule;

        /// <summary>베벨 버튼 면 (상단 하이라이트 + 하단 그늘 + 라운드 코너).</summary>
        public static Sprite Button => _button != null ? _button : _button = BuildButton();

        /// <summary>패널 테두리 링 (중앙 투명 — 틴트 재색칠은 프레임만 물든다).</summary>
        public static Sprite Frame => _frame != null ? _frame : _frame = BuildFrame();

        /// <summary>패널 속 채움 (세로 그라데이션 + 상단 인너 글로우, 색 고정).</summary>
        public static Sprite Fill => _fill != null ? _fill : _fill = BuildFill();

        /// <summary>양끝이 사그라드는 장식 가로선 (섹션 구분·타이틀 밑줄).</summary>
        public static Sprite Rule => _rule != null ? _rule : _rule = BuildRule();

        static Sprite Make(Texture2D tex, Vector4 border)
        {
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>네 코너까지의 맨해튼 거리 최솟값 — 2 미만이면 라운드 코너로 깎인다.</summary>
        static int CornerMetric(int x, int y)
        {
            return Mathf.Min(
                Mathf.Min(x + y, x + (N - 1 - y)),
                Mathf.Min((N - 1 - x) + y, (N - 1 - x) + (N - 1 - y)));
        }

        static int EdgeDepth(int x, int y)
        {
            return Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
        }

        static readonly Color Outline = new Color(0.03f, 0.04f, 0.08f, 1f);

        static Sprite BuildButton()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int m = CornerMetric(x, y);
                    int d = EdgeDepth(x, y);
                    Color c;
                    if (m < 2) c = Color.clear;                    // 라운드 코너
                    else if (m == 2 || d == 0) c = Outline;
                    else
                    {
                        int r = Mathf.Min(d, m - 2) - 1;           // 아웃라인 안쪽 깊이 (0부터)
                        // 위가 밝은 면 — 슬라이스 중앙은 중간값으로 늘어난다.
                        // 값 범위를 높게 잡아야 틴트 색이 탁해지지 않는다 (로컬 검증에서
                        // 금색 CTA가 겨자색으로 가라앉아 상향, 2026-07-31).
                        float v = Mathf.Lerp(0.60f, 0.88f, (float)y / (N - 1));
                        if (r == 0)
                        {
                            if (y >= N - 4) v = 1.0f;              // 상단 하이라이트
                            else if (y <= 3) v = 0.32f;            // 하단 그늘
                            else if (x <= 3) v = 0.92f;            // 좌측 보조광
                            else v = 0.46f;                        // 우측 그늘
                        }
                        c = new Color(v, v, v, 1f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        static Sprite BuildFrame()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int m = CornerMetric(x, y);
                    int d = EdgeDepth(x, y);
                    Color c;
                    if (m < 2) c = Color.clear;
                    else if (m == 2 || d == 0) c = Outline;
                    else
                    {
                        int r = Mathf.Min(d, m - 2) - 1;
                        if (r == 0) c = new Color(0.95f, 0.95f, 0.95f, 1f);       // 주 릿지
                        else if (r == 1) c = new Color(0.62f, 0.62f, 0.62f, 1f);  // 릿지 그늘
                        else if (r == 2) c = new Color(0.10f, 0.10f, 0.10f, 1f);  // 인너 라인
                        else c = Color.clear;                                      // 중앙은 Fill 몫
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        static Sprite BuildFill()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            // 뒤 화면 글자가 비치면 패널 위 텍스트와 겹쳐 지저분하다 — 거의 불투명하게
            var bottom = new Color(0.028f, 0.038f, 0.082f, 0.97f);
            var top = new Color(0.075f, 0.105f, 0.19f, 0.97f);
            var glow = new Color(0.14f, 0.19f, 0.32f, 0.97f);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    Color c = CornerMetric(x, y) < 2
                        ? Color.clear
                        : y >= N - 3
                            ? glow                                  // 상단 인너 글로우 2px
                            : Color.Lerp(bottom, top, (float)y / (N - 1));
                    tex.SetPixel(x, y, c);
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        static Sprite BuildRule()
        {
            const int W = 24, H = 3, Fade = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float edge = Mathf.Min(x + 1, W - x);
                    float a = Mathf.Clamp01(edge / Fade);
                    float v = y == 1 ? 1f : 0.35f;                  // 중앙 행이 심, 위아래는 옅게
                    tex.SetPixel(x, y, new Color(v, v, v, a * (y == 1 ? 0.9f : 0.45f)));
                }
            }
            return Make(tex, new Vector4(Fade, 0f, Fade, 0f));
        }
    }
}
