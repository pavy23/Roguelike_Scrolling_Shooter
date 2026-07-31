using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 코드 생성 9-slice UI 스킨 — CODEX 시안 "비행 계기판" 언어 (사람 선택, 2026-07-31).
    ///
    /// 4-에이전트 디자인 경쟁에서 CODEX 안이 채택됐다: 베벨·그라데이션을 전부 걷어내고
    /// **무채색 헤어라인 셀 + 앰버 단일 액센트 + 챔퍼 블록 CTA**로 계기판 위계를 만든다.
    /// 장식이 아니라 서열이 고급스러움을 만든다 — 채운 면은 화면당 주 동작 하나뿐이다.
    ///
    /// 규약:
    /// - <see cref="Button"/>(셀)·<see cref="Cta"/>·<see cref="Frame"/>은 **그레이스케일**,
    ///   최종 색은 <c>Image.color</c> 틴트가 정한다. 셀은 보더 1.0/채움 0.16으로 그려
    ///   한 틴트로 "헤어라인 테두리 + 짙은 속"이 동시에 나온다.
    /// - PPU 100 = CanvasScaler referencePixelsPerUnit 기본값이라 보더 픽셀 수가
    ///   그대로 640×360 캔버스 픽셀이 된다 (정수 배율은 PixelUiScaler가 보장).
    /// </summary>
    public static class UiSkin
    {
        const int N = 18;   // 텍스처 한 변
        const int B = 6;    // 9-slice 보더

        static Sprite _button, _cta, _frame, _fill, _rule;

        /// <summary>계기판 셀: 1px 헤어라인 테두리 + 짙은 평면 채움 (틴트 = 테두리 색).</summary>
        public static Sprite Button => _button != null ? _button : _button = BuildCell();

        /// <summary>주 동작 블록: 꽉 채운 면 + 우상단 챔퍼(모따기). 화면당 하나만.</summary>
        public static Sprite Cta => _cta != null ? _cta : _cta = BuildCta();

        /// <summary>패널 테두리 링 (중앙 투명 — 틴트 재색칠은 프레임만 물든다).</summary>
        public static Sprite Frame => _frame != null ? _frame : _frame = BuildFrame();

        /// <summary>패널 속 채움 (거의 불투명한 잉크색 평면, 색 고정).</summary>
        public static Sprite Fill => _fill != null ? _fill : _fill = BuildFill();

        /// <summary>양끝이 사그라드는 장식 가로선 (섹션 구분·배너).</summary>
        public static Sprite Rule => _rule != null ? _rule : _rule = BuildRule();

        static Sprite Make(Texture2D tex, Vector4 border)
        {
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static int EdgeDepth(int x, int y)
        {
            return Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - 1 - x, N - 1 - y));
        }

        /// <summary>1px 헤어라인 + 평면 속. 라운드·베벨 없음 — 계기판은 직각이다.</summary>
        static Sprite BuildCell()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float v = EdgeDepth(x, y) == 0 ? 1.0f : 0.16f;
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        /// <summary>
        /// 챔퍼 블록: 우상단 모서리를 5px 대각으로 따낸 꽉 찬 면.
        /// 슬라이스로 늘려도 챔퍼가 우상단 보더 안에 있어 형태가 유지된다.
        /// </summary>
        static Sprite BuildCta()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            const int Cut = 5;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    // 우상단 대각 모따기: 잘린 삼각형은 투명
                    int over = (x - (N - 1 - Cut)) + (y - (N - 1 - Cut));
                    Color c;
                    if (over > Cut) c = Color.clear;
                    else if (over == Cut) c = new Color(0.55f, 0.55f, 0.55f, 1f); // 모따기 단면
                    else if (y <= 1) c = new Color(0.62f, 0.62f, 0.62f, 1f);      // 하단 접지 2px
                    else c = Color.white;                                          // 평면 채움
                    tex.SetPixel(x, y, c);
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        /// <summary>헤어라인 프레임: 1px 선 + 1px 안쪽 어둠. 중앙은 Fill 몫.</summary>
        static Sprite BuildFrame()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int d = EdgeDepth(x, y);
                    Color c = d == 0 ? new Color(1f, 1f, 1f, 1f)
                        : d == 1 ? new Color(0.07f, 0.07f, 0.07f, 1f)
                        : Color.clear;
                    tex.SetPixel(x, y, c);
                }
            }
            return Make(tex, new Vector4(B, B, B, B));
        }

        static Sprite BuildFill()
        {
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            // 잉크색 평면 — 계기판은 유리 대신 무광. 상단 1px만 아주 옅게 밝혀
            // 셀과 패널이 같은 재질로 읽히게 한다.
            var ink = new Color(0.055f, 0.070f, 0.090f, 0.97f);
            var lip = new Color(0.095f, 0.115f, 0.140f, 0.97f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    tex.SetPixel(x, y, y >= N - 2 ? lip : ink);
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
