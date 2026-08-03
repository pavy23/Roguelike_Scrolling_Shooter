using System;
using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>배경 패럴랙스 레이어의 의미. 인덱스가 아니라 역할로 지목해야
    /// 테마마다 레이어 수가 달라도(아트가 없으면 레이어가 생략된다) 룩 테이블이 안 깨진다.</summary>
    public enum BgLayerRole
    {
        SkyFar = 0,    // 별 원경 = 하늘. 실내/체내 구간에서 숨기는 대상.
        Far = 1,       // 테마 원경 (교체 슬롯 대상)
        SkyNear = 2,   // 별 근경 = 하늘
        Mid = 3,       // 테마 중경
        // 테마 전경 실루엣. 가장 빠르게(1.15) 흐르는 최근경이지만 정렬은 게임플레이
        // **아래**다 (SectionThemeDirector.NearSortingOrder = 3, 탄 5 아래).
        // 위에 두면 <theme>_fg.png의 불투명 띠가 기체·탄을 삼킨다.
        Near = 4
    }

    // 워시 알파 상한 0.10 (사람 지적 2026-08-03). 처음엔 0.34~0.55였고 0.20으로
    // 낮췄는데 사람이 "지금도 색칠한 것 같다"고 해서 한 번 더 내렸다. 워시는
    // **분위기를 얹는 얇은 막**이지 배경을 덮는 페인트가 아니다 — 배경 아트가
    // 주인공이고 워시는 그 위 10%다.
    //
    // 여기를 다시 올리려면 반드시 실제 배경 위에서 확인해라. 값만 보고 정하면
    // 또 잠긴다 (두 번 겪었다).

    // 중간보스에서는 **원경 그림을 바꾸지 않는다** (사람 지적 2026-08-03, 사진 2장:
    // "중간보스 진입 시 배경 그림이 갑자기 바뀌어 엄청 어색해").
    //
    // 예전에는 far 레이어를 `*_far_dusk` 그림으로 갈아치웠다. 색조는 20초에 걸쳐
    // 섞이지만 **그림 교체는 결국 다른 그림으로 바뀌는 것**이라, 디졸브를 아무리
    // 늘려도 "장면이 갈렸다"로 읽힌다. 측정해 보니 색 자체는 완만히 변하고 있었는데도
    // 사람이 급변으로 느낀 이유가 이것이다.
    //
    // 황혼은 **틴트로만** 만든다 — 같은 그림 위에 색이 스미면 해가 지는 것으로 읽힌다.
    // Late/Boss의 `*_far_dark` 교체는 남겨 둔다: 그 지점은 사건성 전환이라 갈리는 것이
    // 오히려 맞고, 이미 짧은 전환(3.5~4초)으로 그렇게 설계돼 있다.

    // 중간보스 전환 길이(20/18/16/14초)는 **일부러 길다**. 한 번 6초로 줄였다가
    // 되돌렸다 — 사람이 지적한 것은 프레임 끌림이 아니라 "갑자기 바뀌어 어색하다"였고,
    // 줄이는 것은 정확히 반대 방향이었다. 짧게 만들수록 급작스러워진다.
    // 대신 스프라이트 디졸브(FadeSecondsLong)를 늘려 형태가 천천히 스미게 했다.

    /// <summary>스테이지 내 4구간. Core의 RunStageSection을 표현용으로 압축한 것.</summary>
    public enum SectionKind
    {
        Early = 0,     // 전반 (RunStageSection.Opening)
        MidBoss = 1,   // 중간보스 (MidBoss)
        Late = 2,      // 후반 (Closing / HiddenOpening)
        Boss = 3       // 보스 (StageBoss / HiddenBoss)
    }

    /// <summary>코드 생성 파티클 프리셋. 순수 표현이라 결정론과 무관하다.</summary>
    public enum SectionParticle
    {
        None = 0,
        Ash = 1,     // 재 — 느리게 흩날리며 가라앉는다
        Spore = 2,   // 포자 — 크게 흔들리며 떠오른다
        Bubble = 3,  // 기포 — 빠르게 상승
        Ember = 4,   // 불씨 — 빠르고 밝게 깜빡인다
        Fog = 5,     // 안개 스크림 — 가로로 긴 띠가 빠르게 지나간다
        Mote = 6     // 네온 티끌 — 반짝이는 점
    }

    /// <summary>레이어 하나의 구간별 오버라이드.</summary>
    [Serializable]
    public sealed class SectionLayerLook
    {
        public BgLayerRole role;
        /// <summary>곱 틴트. a는 표시 강도 — 0이면 렌더러를 끈다(하늘 제거 등).</summary>
        public Color tint = Color.white;
        /// <summary>패럴랙스 팩터 배율. Core의 scrollSpeed는 건드리지 않는다 — 체감만 바꾼다.</summary>
        public float scrollMul = 1f;
        /// <summary>원경 교체 슬롯(art-input 파일 키). null이면 원본 유지. — 아트 2단계용.</summary>
        public string spriteSlot;
    }

    /// <summary>
    /// 테마 × 구간 하나의 룩. **Presentation 전용 plain class**다 (ScriptableObject 금지, AGENTS.md §5).
    /// 지금은 <see cref="SectionThemeTable"/>에 코드로 하드코딩하지만, 그대로 JSON 역직렬화
    /// 대상이 될 수 있게 필드만으로 구성했다.
    /// </summary>
    [Serializable]
    public sealed class SectionTheme
    {
        public string themeId;
        public SectionKind section;

        /// <summary>이 구간으로 블렌드되는 시간(게임 초). 중간보스 격파 전환은 짧게(3~5),
        /// 전반→중간보스의 자연 전환은 길게(14~20).</summary>
        public float enterSeconds = 8f;

        /// <summary>전역 틴트. 배경 위·게임플레이 아래에 깔리는 대기(매질) 워시.
        /// a가 강도다. 기체/탄은 덮지 않는다 — 가시성이 먼저다.</summary>
        public Color wash = new Color(0f, 0f, 0f, 0f);
        /// <summary>워시 알파의 사인 펄스 진폭(맥동·비상등). 0이면 정적.</summary>
        public float washPulseAmplitude;
        public float washPulseHz = 0.5f;

        public SectionLayerLook[] layers = Array.Empty<SectionLayerLook>();

        public SectionParticle particle = SectionParticle.None;
        /// <summary>0~1. 최대 입자 수에 대한 비율.</summary>
        public float particleDensity;

        /// <summary>구간 진입 순간의 충격. 중간보스 격파·요새 진입처럼 경계를 사건으로 만든다.</summary>
        public float enterShake;
        /// <summary>진입 스냅 플래시 색(a=피크 알파). a=0이면 없음.</summary>
        public Color enterFlash = new Color(0f, 0f, 0f, 0f);
        public float enterFlashSeconds = 0.35f;

        /// <summary>반복 섬광(낙뢰) 간격 범위(게임 초). max&lt;=0이면 끔. 표현용 Unity Random 사용.</summary>
        public float flashIntervalMin;
        public float flashIntervalMax;
        public Color flashColor = new Color(1f, 1f, 1f, 0f);
        public float flashSeconds = 0.2f;

        /// <summary>비반복 랜드마크 슬롯(거대 코어·난파선). 지금은 전부 null — 아트 2단계 슬롯.</summary>
        public string landmarkSlot;
        public float landmarkScaleStart = 1f;
        public float landmarkScaleEnd = 1f;
        /// <summary>랜드마크가 start→end로 자라는 시간(게임 초). 접근감 연출.</summary>
        public float landmarkGrowSeconds = 45f;
    }

    /// <summary>
    /// 1차 룩 테이블 (설계안 Reviews/from-claude/stage-overhaul-proposal-2026-08-02.md §1·§2).
    ///
    /// 1차 골격은 **아트 0장**이었다 — 3종 세트(파티클 + 틴트 + 스크롤 체감)와
    /// 레이어 on/off만으로 20룩을 만든다. 아트 2단계(Phase C)에서 그 위에 슬롯 20장을
    /// 얹었다: 구간별 원경 교체(dusk/dark) · 전경 실루엣(fg) · 랜드마크(landmark).
    ///
    /// 슬롯 배분 규칙:
    ///   MidBoss  → Far = 교체 없음 (황혼은 틴트로만 — 그림을 갈면 장면이 갈린다)
    ///   Late·Boss→ Far = &lt;p&gt;_far_dark, Near = &lt;p&gt;_fg   (야간/실내 + 전경 실루엣)
    ///   랜드마크 → 테마마다 한 구간에만. 요새만 MidBoss(진입 스냅에 관제탑이 선다),
    ///              나머지는 Late. 코어는 Late→Boss로 이어지며 스케일이 점증한다.
    /// 스프라이트 교체는 구간 진입 프레임에 즉시 일어난다(틴트만 블렌드된다).
    /// 그래서 dusk 램프는 원본과 명도가 크게 벌어지지 않게 잡아 두었다 — MidBoss는
    /// 14~20초 롱블렌드라 교체를 덮어 줄 사건이 없다.
    ///
    /// 구간 배분 원칙 (설계 원칙 2): **극적인 전환은 MidBoss→Late 경계**에 몰아 둔다.
    /// 중간보스 격파 순간이 그 경계이므로, Late의 enterSeconds가 3~4초로 짧고
    /// enterShake가 붙는다. MidBoss 구간은 그 전조(빌드업)라 20초 가까이 길게 스민다.
    /// </summary>
    public static class SectionThemeTable
    {
        public const int SectionCount = 4;

        /// <summary>
        /// 레이어 5겹의 틴트·스크롤 배율. <paramref name="farSlot"/>·<paramref name="nearSlot"/>은
        /// 아트 교체 키다 (art-input/&lt;키&gt;.png — 씬 빌더 CreateSectionArtSlots가 배선한다).
        /// null이면 테마 원본 레이어를 그대로 쓴다.
        /// </summary>
        static SectionLayerLook[] L(
            Color sky, float skyMul,
            Color far, float farMul,
            Color mid, float midMul,
            Color near, float nearMul,
            string farSlot = null, string nearSlot = null)
        {
            return new[]
            {
                new SectionLayerLook { role = BgLayerRole.SkyFar,  tint = sky,  scrollMul = skyMul },
                new SectionLayerLook { role = BgLayerRole.Far,     tint = far,  scrollMul = farMul,
                                       spriteSlot = farSlot },
                new SectionLayerLook { role = BgLayerRole.SkyNear, tint = sky,  scrollMul = skyMul },
                new SectionLayerLook { role = BgLayerRole.Mid,     tint = mid,  scrollMul = midMul },
                new SectionLayerLook { role = BgLayerRole.Near,    tint = near, scrollMul = nearMul,
                                       spriteSlot = nearSlot }
            };
        }

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        static readonly SectionTheme Neutral = new SectionTheme
        {
            themeId = null,
            enterSeconds = 2f,
            layers = L(Color.white, 1f, Color.white, 1f, Color.white, 1f, Color.white, 1f)
        };

        static SectionTheme[] _all;

        public static SectionTheme[] All => _all ?? (_all = Build());

        /// <summary>테마 id + 구간 → 룩. 미등록 테마/구간이면 중립(원본 그대로)을 준다.</summary>
        public static SectionTheme Resolve(string themeId, SectionKind section)
        {
            var all = All;
            if (!string.IsNullOrEmpty(themeId))
            {
                for (int i = 0; i < all.Length; i++)
                    if (all[i].section == section
                        && string.Equals(all[i].themeId, themeId, StringComparison.Ordinal))
                        return all[i];
            }
            return Neutral;
        }

        static SectionTheme[] Build()
        {
            return new[]
            {
                // ── St1 고철 폐선장 ──────────────────────────────────────────────
                // 낮 → (중간보스 격파) dusk 주황 + 재 → 협곡(어둑 + 스크롤 체감 상승) → 용광로
                new SectionTheme
                {
                    themeId = "scrapyard", section = SectionKind.Early, enterSeconds = 1.5f,
                    wash = C(0.58f, 0.72f, 0.92f, 0.10f),
                    layers = L(C(0.78f, 0.86f, 1.00f, 0.55f), 1.00f,
                               C(1.00f, 0.98f, 0.94f), 1.00f,
                               C(1.00f, 0.99f, 0.96f), 1.00f,
                               C(0.98f, 0.96f, 0.94f), 1.00f),
                    particle = SectionParticle.None
                },
                new SectionTheme
                {
                    themeId = "scrapyard", section = SectionKind.MidBoss, enterSeconds = 20f,
                    wash = C(0.95f, 0.62f, 0.34f, 0.10f),
                    layers = L(C(0.85f, 0.78f, 0.82f, 0.70f), 1.00f,
                               C(1.00f, 0.86f, 0.70f), 1.00f,
                               C(0.98f, 0.82f, 0.66f), 1.05f,
                               C(0.85f, 0.68f, 0.56f), 1.10f),
                    particle = SectionParticle.Ash, particleDensity = 0.35f
                },
                new SectionTheme
                {
                    themeId = "scrapyard", section = SectionKind.Late, enterSeconds = 3.5f,
                    enterShake = 0.35f,
                    wash = C(1.00f, 0.42f, 0.14f, 0.10f),
                    layers = L(C(0.62f, 0.42f, 0.44f, 0.90f), 1.10f,
                               C(0.95f, 0.52f, 0.30f), 1.15f,
                               C(0.80f, 0.42f, 0.26f), 1.30f,
                               C(0.75f, 0.38f, 0.29f), 1.45f,
                               farSlot: "scrap_far_dark", nearSlot: "scrap_fg"),
                    particle = SectionParticle.Ash, particleDensity = 1.0f,
                    // 폐함대 난파 모함 — 협곡으로 접근할수록 커진다.
                    landmarkSlot = "scrap_landmark",
                    landmarkScaleStart = 0.85f, landmarkScaleEnd = 1.3f,
                    landmarkGrowSeconds = 50f
                },
                new SectionTheme
                {
                    themeId = "scrapyard", section = SectionKind.Boss, enterSeconds = 4f,
                    wash = C(0.85f, 0.12f, 0.06f, 0.10f),
                    washPulseAmplitude = 0.07f, washPulseHz = 0.45f,
                    layers = L(C(0.30f, 0.16f, 0.18f, 0.80f), 0.90f,
                               C(0.75f, 0.30f, 0.22f), 0.90f,
                               C(0.75f, 0.29f, 0.24f), 0.95f,
                               C(0.75f, 0.30f, 0.28f), 1.00f,
                               farSlot: "scrap_far_dark", nearSlot: "scrap_fg"),
                    particle = SectionParticle.Ember, particleDensity = 0.8f
                },

                // ── St2 생체 군체 ───────────────────────────────────────────────
                // 밝은 유기 → 체내 접근 → (격파) 체내/굴착: 하늘 제거 + 녹색 + 안개 → 심장부 맥동
                new SectionTheme
                {
                    themeId = "hive", section = SectionKind.Early, enterSeconds = 1.5f,
                    wash = C(0.62f, 0.95f, 0.72f, 0.10f),
                    layers = L(C(0.86f, 1.00f, 0.90f, 0.80f), 1.00f,
                               C(1.00f, 1.00f, 0.98f), 1.00f,
                               C(0.98f, 1.00f, 0.96f), 1.00f,
                               C(0.95f, 1.00f, 0.94f), 1.00f),
                    particle = SectionParticle.Spore, particleDensity = 0.6f
                },
                new SectionTheme
                {
                    themeId = "hive", section = SectionKind.MidBoss, enterSeconds = 18f,
                    wash = C(0.36f, 0.85f, 0.52f, 0.10f),
                    layers = L(C(0.70f, 0.92f, 0.78f, 0.45f), 1.00f,
                               C(0.80f, 0.98f, 0.84f), 1.00f,
                               C(0.74f, 0.94f, 0.78f), 1.05f,
                               C(0.62f, 0.86f, 0.68f), 1.10f),
                    particle = SectionParticle.Spore, particleDensity = 0.9f
                },
                new SectionTheme
                {
                    themeId = "hive", section = SectionKind.Late, enterSeconds = 4f,
                    enterShake = 0.25f,
                    wash = C(0.10f, 0.55f, 0.28f, 0.10f),
                    // 하늘 알파 0 = 체내 진입 (설계안 §1 St2 "하늘 레이어 제거")
                    layers = L(C(0.70f, 0.92f, 0.78f, 0.00f), 1.00f,
                               C(0.41f, 0.75f, 0.46f), 0.85f,
                               C(0.39f, 0.75f, 0.46f), 1.15f,
                               C(0.38f, 0.75f, 0.49f), 1.25f,
                               farSlot: "hive_far_dark", nearSlot: "hive_fg"),
                    particle = SectionParticle.Fog, particleDensity = 0.8f,
                    // 거대 알주머니 — 체내를 파고들수록 눈앞을 채운다.
                    landmarkSlot = "hive_landmark",
                    landmarkScaleStart = 0.7f, landmarkScaleEnd = 1.3f,
                    landmarkGrowSeconds = 45f
                },
                new SectionTheme
                {
                    themeId = "hive", section = SectionKind.Boss, enterSeconds = 3f,
                    wash = C(0.06f, 0.30f, 0.16f, 0.10f),
                    washPulseAmplitude = 0.12f, washPulseHz = 0.85f,   // 심장 맥동
                    layers = L(C(0.70f, 0.92f, 0.78f, 0.00f), 1.00f,
                               C(0.44f, 0.75f, 0.53f), 0.80f,
                               C(0.43f, 0.75f, 0.55f), 0.85f,
                               C(0.41f, 0.75f, 0.58f), 0.90f,
                               farSlot: "hive_far_dark", nearSlot: "hive_fg"),
                    particle = SectionParticle.Mote, particleDensity = 0.7f   // 생체발광
                },

                // ── St3 기계 요새 ───────────────────────────────────────────────
                // 외곽 → WARNING 적색 스냅 → (격파) 함내: 하늘 제거 + 금속 회색 → 코어룸 비상등
                new SectionTheme
                {
                    themeId = "fortress", section = SectionKind.Early, enterSeconds = 1.5f,
                    wash = C(0.55f, 0.62f, 0.75f, 0.10f),
                    layers = L(C(0.80f, 0.86f, 1.00f, 1.00f), 1.00f,
                               C(0.95f, 0.97f, 1.00f), 1.00f,
                               C(0.93f, 0.95f, 1.00f), 1.00f,
                               C(0.88f, 0.90f, 0.96f), 1.00f),
                    particle = SectionParticle.None
                },
                new SectionTheme
                {
                    // 요새 진입 = 스냅(1.2초)이다. Darius 요새 문법 — 경계가 사건이어야 한다.
                    themeId = "fortress", section = SectionKind.MidBoss, enterSeconds = 1.2f,
                    enterShake = 0.55f,
                    enterFlash = C(1.00f, 0.15f, 0.12f, 0.55f), enterFlashSeconds = 0.45f,
                    wash = C(0.80f, 0.12f, 0.10f, 0.10f),
                    layers = L(C(0.60f, 0.42f, 0.48f, 0.85f), 1.10f,
                               C(0.85f, 0.55f, 0.52f), 1.15f,
                               C(0.80f, 0.50f, 0.48f), 1.20f,
                               C(0.75f, 0.43f, 0.43f), 1.25f),
                    particle = SectionParticle.Ember, particleDensity = 0.3f,
                    // 관제탑 — 요새 진입 스냅(1.2초 + 섬광)에 맞춰 눈앞에 선다.
                    // 스프라이트 교체 팝이 그 섬광에 묻히는 유일한 MidBoss 랜드마크다.
                    landmarkSlot = "fort_landmark",
                    landmarkScaleStart = 0.6f, landmarkScaleEnd = 1.15f,
                    landmarkGrowSeconds = 40f
                },
                new SectionTheme
                {
                    themeId = "fortress", section = SectionKind.Late, enterSeconds = 4f,
                    enterShake = 0.3f,
                    wash = C(0.42f, 0.46f, 0.52f, 0.10f),
                    layers = L(C(0.60f, 0.42f, 0.48f, 0.00f), 1.00f,   // 함내 — 하늘 없음
                               C(0.67f, 0.70f, 0.75f), 0.80f,
                               C(0.65f, 0.69f, 0.75f), 1.10f,
                               C(0.63f, 0.67f, 0.75f), 1.20f,
                               farSlot: "fort_far_dark", nearSlot: "fort_fg"),
                    particle = SectionParticle.Ember, particleDensity = 0.5f
                },
                new SectionTheme
                {
                    themeId = "fortress", section = SectionKind.Boss, enterSeconds = 3f,
                    wash = C(0.70f, 0.10f, 0.10f, 0.10f),
                    washPulseAmplitude = 0.16f, washPulseHz = 0.65f,   // 비상등 점멸
                    layers = L(C(0.60f, 0.42f, 0.48f, 0.00f), 1.00f,
                               C(0.62f, 0.65f, 0.75f), 0.70f,
                               C(0.61f, 0.64f, 0.75f), 0.80f,
                               C(0.58f, 0.61f, 0.75f), 0.85f,
                               farSlot: "fort_far_dark", nearSlot: "fort_fg"),
                    particle = SectionParticle.Ember, particleDensity = 0.4f
                },

                // ── St4 성운 폭풍 ───────────────────────────────────────────────
                // 보라 + 안개 → 뇌운 접근(암전 시작) → (격파) 뇌운 본격 0.35 암전 + 섬광 → 폭풍의 눈
                new SectionTheme
                {
                    themeId = "nebula", section = SectionKind.Early, enterSeconds = 1.5f,
                    wash = C(0.42f, 0.24f, 0.70f, 0.10f),
                    layers = L(C(0.80f, 0.76f, 1.00f, 1.00f), 1.00f,
                               C(0.82f, 0.70f, 1.00f), 1.00f,
                               C(0.78f, 0.66f, 0.98f), 1.00f,
                               C(0.68f, 0.58f, 0.90f), 1.00f),
                    particle = SectionParticle.Fog, particleDensity = 0.6f
                },
                new SectionTheme
                {
                    themeId = "nebula", section = SectionKind.MidBoss, enterSeconds = 16f,
                    wash = C(0.10f, 0.08f, 0.22f, 0.10f),
                    layers = L(C(0.55f, 0.52f, 0.75f, 0.70f), 1.05f,
                               C(0.57f, 0.50f, 0.75f), 1.10f,
                               C(0.55f, 0.49f, 0.75f), 1.15f,
                               C(0.52f, 0.47f, 0.75f), 1.20f),
                    particle = SectionParticle.Fog, particleDensity = 0.9f,
                    flashIntervalMin = 14f, flashIntervalMax = 22f,
                    flashColor = C(0.85f, 0.88f, 1.00f, 0.50f), flashSeconds = 0.2f
                },
                new SectionTheme
                {
                    themeId = "nebula", section = SectionKind.Late, enterSeconds = 3f,
                    enterShake = 0.4f,
                    wash = C(0.03f, 0.03f, 0.10f, 0.10f),   // 설계안은 0.35였다 — 아래 주석 참고
                    layers = L(C(0.30f, 0.30f, 0.45f, 0.60f), 1.10f,
                               C(0.53f, 0.47f, 0.75f), 1.20f,
                               C(0.51f, 0.46f, 0.75f), 1.30f,
                               C(0.49f, 0.44f, 0.75f), 1.40f,
                               farSlot: "nebula_far_dark", nearSlot: "nebula_fg"),
                    particle = SectionParticle.Fog, particleDensity = 1.0f,
                    flashIntervalMin = 6f, flashIntervalMax = 12f,   // 섬광 빈도 상승
                    flashColor = C(0.92f, 0.95f, 1.00f, 0.70f), flashSeconds = 0.2f,
                    // 번개 치는 거목 구름 — 뇌운의 중심으로 끌려 들어간다.
                    landmarkSlot = "nebula_landmark",
                    landmarkScaleStart = 0.9f, landmarkScaleEnd = 1.35f,
                    landmarkGrowSeconds = 40f
                },
                new SectionTheme
                {
                    // 폭풍의 눈 — 급격히 맑아지는 역설적 정적. 스크롤 체감까지 떨어뜨린다.
                    themeId = "nebula", section = SectionKind.Boss, enterSeconds = 2.5f,
                    wash = C(0.30f, 0.34f, 0.62f, 0.10f),
                    // 전경 구름은 남긴다 — 느리게(0.70) 흐르는 창백한 벽이 곧 "눈"의 테두리다.
                    layers = L(C(0.95f, 0.95f, 1.00f, 1.00f), 0.55f,
                               C(0.85f, 0.85f, 1.00f), 0.60f,
                               C(0.82f, 0.82f, 0.98f), 0.65f,
                               C(0.75f, 0.76f, 0.94f), 0.70f,
                               farSlot: "nebula_far_dark", nearSlot: "nebula_fg"),
                    particle = SectionParticle.None
                },

                // ── St5 적의 코어 ───────────────────────────────────────────────
                // 네온 → 게이트(글로우 강화) → (격파) 최심부(코어 접근 — 랜드마크 스케일 점증) → 최종전 금색
                new SectionTheme
                {
                    themeId = "core", section = SectionKind.Early, enterSeconds = 1.5f,
                    wash = C(0.20f, 0.55f, 0.95f, 0.10f),
                    layers = L(C(0.85f, 0.92f, 1.00f, 1.00f), 1.00f,
                               C(0.80f, 0.95f, 1.00f), 1.00f,
                               C(0.78f, 0.92f, 1.00f), 1.00f,
                               C(0.70f, 0.85f, 1.00f), 1.00f),
                    particle = SectionParticle.Mote, particleDensity = 0.5f
                },
                new SectionTheme
                {
                    themeId = "core", section = SectionKind.MidBoss, enterSeconds = 14f,
                    wash = C(0.25f, 0.70f, 1.00f, 0.10f),
                    layers = L(C(0.90f, 0.95f, 1.00f, 1.00f), 1.05f,
                               C(0.88f, 1.00f, 1.00f), 1.10f,
                               C(0.85f, 0.98f, 1.00f), 1.15f,
                               C(0.78f, 0.92f, 1.00f), 1.20f),
                    particle = SectionParticle.Mote, particleDensity = 0.8f
                },
                new SectionTheme
                {
                    themeId = "core", section = SectionKind.Late, enterSeconds = 3.5f,
                    enterShake = 0.3f,
                    wash = C(0.10f, 0.35f, 0.85f, 0.10f),
                    layers = L(C(0.55f, 0.70f, 1.00f, 0.80f), 1.15f,
                               C(0.60f, 0.80f, 1.00f), 1.25f,
                               C(0.55f, 0.75f, 1.00f), 1.35f,
                               C(0.45f, 0.62f, 0.95f), 1.45f,
                               farSlot: "core_far_dark", nearSlot: "core_fg"),
                    particle = SectionParticle.Mote, particleDensity = 1.0f,
                    // 거대 코어. 접근감(0.6→1.6)이 여기서 시작해 보스전 크기로 이어진다.
                    landmarkSlot = "core_landmark",
                    landmarkScaleStart = 0.6f, landmarkScaleEnd = 1.6f,
                    landmarkGrowSeconds = 45f
                },
                new SectionTheme
                {
                    themeId = "core", section = SectionKind.Boss, enterSeconds = 3f,
                    wash = C(0.55f, 0.42f, 0.10f, 0.10f),
                    washPulseAmplitude = 0.06f, washPulseHz = 0.35f,
                    layers = L(C(0.35f, 0.32f, 0.28f, 0.90f), 0.70f,
                               C(0.75f, 0.63f, 0.43f), 0.75f,
                               C(0.75f, 0.64f, 0.45f), 0.80f,
                               C(0.75f, 0.64f, 0.48f), 0.85f,
                               farSlot: "core_far_dark", nearSlot: "core_fg"),
                    particle = SectionParticle.Ember, particleDensity = 0.6f,
                    landmarkSlot = "core_landmark",
                    landmarkScaleStart = 1.6f, landmarkScaleEnd = 1.6f
                }
            };
        }
    }
}
