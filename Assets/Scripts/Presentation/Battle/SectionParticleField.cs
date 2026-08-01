using UnityEngine;

namespace Shmup.Presentation.Battle
{
    /// <summary>
    /// 구간 날씨 파티클. **코드 생성 · 단색 사각(px_white) 재활용 · 아트 0장.**
    ///
    /// Unity ParticleSystem 대신 SpriteRenderer 풀을 직접 돌린다 — 정렬 순서를
    /// 게임플레이 아래(order 7)로 못 박아 탄 가시성을 절대 해치지 않기 위해서다.
    /// 이 프로젝트의 다른 뷰(SpritePool)와 같은 문법이기도 하다.
    ///
    /// 순수 표현이라 결정론과 무관하다 (표현용 UnityEngine.Random 사용). 다만 시간은
    /// **게임 틱 기준 dt**를 받아 돈다 — 일시정지(timeScale 0)에서 눈이 계속 내리면 안 된다.
    /// </summary>
    public sealed class SectionParticleField
    {
        public const int MaxParticles = 72;

        struct P
        {
            public float x, y;
            public float vx, vy;
            public float swayPhase, swayHz, swayAmp;
            public float life, maxLife;
            public float alpha;
            public float twinkleHz;
            public Color color;
            public bool live;
        }

        readonly Transform _root;
        readonly SpriteRenderer[] _renderers = new SpriteRenderer[MaxParticles];
        readonly P[] _particles = new P[MaxParticles];

        readonly float _halfW, _halfH;
        readonly float _unitX, _unitY;   // 스프라이트 스케일 1이 만드는 월드 크기

        SectionParticle _preset = SectionParticle.None;
        float _density;        // 현재 밀도 (0~1) — 목표로 부드럽게 수렴
        float _targetDensity;
        float _time;

        public SectionParticleField(
            Transform parent, Sprite pixelSprite, int sortingOrder, float halfWidth, float halfHeight)
        {
            _halfW = halfWidth;
            _halfH = halfHeight;

            _unitX = 1f;
            _unitY = 1f;
            if (pixelSprite != null)
            {
                Vector3 size = pixelSprite.bounds.size;
                if (size.x > 0.0001f) _unitX = size.x;
                if (size.y > 0.0001f) _unitY = size.y;
            }

            _root = new GameObject("SectionParticles").transform;
            _root.SetParent(parent, false);

            for (int i = 0; i < MaxParticles; i++)
            {
                var go = new GameObject("P");
                go.transform.SetParent(_root, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = pixelSprite;
                renderer.sortingOrder = sortingOrder;
                renderer.enabled = false;
                _renderers[i] = renderer;
            }
        }

        /// <summary>프리셋/밀도 목표 갱신. 프리셋이 바뀌어도 기존 입자는 수명을 마치고
        /// 새 프리셋으로 다시 태어난다 — 그것만으로 자연스러운 크로스페이드가 된다.</summary>
        public void SetPreset(SectionParticle preset, float density)
        {
            _preset = preset;
            _targetDensity = Mathf.Clamp01(density);
        }

        /// <summary>게임 틱 기준 dt(초)로 전진.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _time += dt;

            // 밀도는 3초에 걸쳐 수렴한다 — 구간이 바뀌자마자 눈이 뚝 끊기면 티가 난다.
            _density = Mathf.MoveTowards(_density, _targetDensity, dt / 3f);

            int want = _preset == SectionParticle.None
                ? 0
                : Mathf.RoundToInt(_density * MaxParticles);

            int live = 0;
            for (int i = 0; i < MaxParticles; i++)
            {
                ref P p = ref _particles[i];
                if (!p.live)
                {
                    // 정원이 남으면 새로 태어난다 (화면 안 임의 위치 — 첫 진입이 자연스럽게)
                    if (live < want && _preset != SectionParticle.None)
                    {
                        Spawn(ref p, i, true);
                        live++;
                    }
                    continue;
                }

                p.life -= dt;
                bool over = live >= want;
                if (p.life <= 0f || over)
                {
                    if (over)
                    {
                        // 정원 초과분은 조용히 사라진다
                        p.live = false;
                        _renderers[i].enabled = false;
                        continue;
                    }
                    Spawn(ref p, i, false);
                }

                p.x += p.vx * dt;
                p.y += p.vy * dt;
                if (p.swayAmp > 0f)
                    p.x += Mathf.Sin((_time + p.swayPhase) * p.swayHz * Mathf.PI * 2f)
                           * p.swayAmp * dt;

                // 화면 밖으로 나가면 반대편에서 다시 들어온다
                if (p.x < -_halfW - 6f || p.x > _halfW + 6f
                    || p.y < -_halfH - 3f || p.y > _halfH + 3f)
                    Spawn(ref p, i, false);

                // 수명 양끝(각 15%)을 페이드해 팝인/팝아웃을 없앤다.
                // t는 남은 수명 비율 — 태어날 때 1, 죽을 때 0이다.
                float t = Mathf.Clamp01(p.life / Mathf.Max(0.01f, p.maxLife));
                float fade = Mathf.Min(1f, Mathf.Min((1f - t) * 6.5f, t * 6.5f));
                float a = p.alpha * fade;
                if (p.twinkleHz > 0f)
                    a *= 0.55f + 0.45f * Mathf.Sin((_time + p.swayPhase) * p.twinkleHz * Mathf.PI * 2f);

                var renderer = _renderers[i];
                renderer.transform.localPosition = new Vector3(p.x, p.y, 0f);
                var c = p.color;
                c.a = a;
                renderer.color = c;
                if (!renderer.enabled) renderer.enabled = true;
                live++;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                _particles[i].live = false;
                if (_renderers[i] != null) _renderers[i].enabled = false;
            }
            _density = 0f;
        }

        void Spawn(ref P p, int index, bool anywhere)
        {
            float sizeX, sizeY;
            switch (_preset)
            {
                case SectionParticle.Ash:      // 재 — 느리게 흩날리며 가라앉는다
                    p.color = new Color(0.86f, 0.58f, 0.40f);
                    p.vx = -Random.Range(2.5f, 5.0f);
                    p.vy = -Random.Range(0.4f, 1.3f);
                    p.swayHz = Random.Range(0.15f, 0.4f);
                    p.swayAmp = Random.Range(0.6f, 1.6f);
                    p.maxLife = Random.Range(6f, 11f);
                    p.alpha = Random.Range(0.35f, 0.7f);
                    sizeX = sizeY = Random.Range(0.07f, 0.16f);
                    p.twinkleHz = 0f;
                    break;
                case SectionParticle.Spore:    // 포자 — 크게 흔들리며 떠오른다
                    p.color = new Color(0.78f, 1.00f, 0.66f);
                    p.vx = -Random.Range(1.2f, 2.8f);
                    p.vy = Random.Range(0.2f, 0.9f);
                    p.swayHz = Random.Range(0.1f, 0.3f);
                    p.swayAmp = Random.Range(1.2f, 2.8f);
                    p.maxLife = Random.Range(7f, 13f);
                    p.alpha = Random.Range(0.3f, 0.6f);
                    sizeX = sizeY = Random.Range(0.09f, 0.20f);
                    p.twinkleHz = 0f;
                    break;
                case SectionParticle.Bubble:   // 기포 — 빠르게 상승
                    p.color = new Color(0.72f, 0.95f, 1.00f);
                    p.vx = -Random.Range(1.0f, 2.2f);
                    p.vy = Random.Range(1.2f, 2.6f);
                    p.swayHz = Random.Range(0.5f, 1.1f);
                    p.swayAmp = Random.Range(0.4f, 1.0f);
                    p.maxLife = Random.Range(4f, 8f);
                    p.alpha = Random.Range(0.25f, 0.5f);
                    sizeX = sizeY = Random.Range(0.06f, 0.14f);
                    p.twinkleHz = 0f;
                    break;
                case SectionParticle.Ember:    // 불씨 — 빠르고 밝게 깜빡인다
                    p.color = new Color(1.00f, 0.58f, 0.18f);
                    p.vx = -Random.Range(3f, 6.5f);
                    p.vy = Random.Range(0.5f, 1.8f);
                    p.swayHz = Random.Range(0.4f, 0.9f);
                    p.swayAmp = Random.Range(0.5f, 1.4f);
                    p.maxLife = Random.Range(3f, 6f);
                    p.alpha = Random.Range(0.5f, 0.9f);
                    sizeX = sizeY = Random.Range(0.05f, 0.11f);
                    p.twinkleHz = Random.Range(2f, 5f);
                    break;
                case SectionParticle.Fog:      // 안개 스크림 — 가로로 긴 띠
                    p.color = new Color(0.78f, 0.80f, 0.96f);
                    p.vx = -Random.Range(6f, 12f);
                    p.vy = Random.Range(-0.2f, 0.2f);
                    p.swayHz = 0f;
                    p.swayAmp = 0f;
                    p.maxLife = Random.Range(3f, 6f);
                    p.alpha = Random.Range(0.08f, 0.20f);
                    sizeX = Random.Range(4f, 11f);
                    sizeY = Random.Range(0.06f, 0.14f);
                    p.twinkleHz = 0f;
                    break;
                case SectionParticle.Mote:     // 네온 티끌 — 반짝이는 점
                    p.color = new Color(0.62f, 0.92f, 1.00f);
                    p.vx = -Random.Range(4f, 8f);
                    p.vy = Random.Range(-0.5f, 0.5f);
                    p.swayHz = Random.Range(0.3f, 0.8f);
                    p.swayAmp = Random.Range(0.2f, 0.8f);
                    p.maxLife = Random.Range(3f, 7f);
                    p.alpha = Random.Range(0.4f, 0.8f);
                    sizeX = sizeY = Random.Range(0.05f, 0.10f);
                    p.twinkleHz = Random.Range(1.5f, 4f);
                    break;
                default:
                    p.live = false;
                    _renderers[index].enabled = false;
                    return;
            }

            p.life = p.maxLife;
            p.swayPhase = Random.Range(0f, 10f);
            p.live = true;

            // 왼쪽으로 흐르므로 기본 스폰은 오른쪽 밖. 첫 진입만 화면 전체에 뿌린다.
            p.x = anywhere
                ? Random.Range(-_halfW, _halfW)
                : _halfW + Random.Range(0.5f, 5f);
            p.y = Random.Range(-_halfH, _halfH);

            // px_white는 2px(=0.125u)다. 월드 크기를 그대로 localScale에 넣으면 1/8이 된다
            // (StageGimmickView·LaserBeamView가 같은 함정에 걸렸던 곳이다).
            _renderers[index].transform.localScale =
                new Vector3(sizeX / _unitX, sizeY / _unitY, 1f);
        }
    }
}
