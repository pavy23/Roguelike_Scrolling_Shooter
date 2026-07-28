# ART-DIRECTION.md — HD 도트(hi-bit) 기준 v2

사람(아트 디렉터) + RENDERER가 관리한다. 에이전트는 이 규격을 벗어나는 아트를 임포트하지 않는다.
v1(384×224 파일럿)은 2026-07-28 캔버스 상향 결정으로 대체됨 — ROADMAP.md 참고.

## 방향

- **hi-bit 픽셀아트**: 캔버스 **640×360 (16:9)**, PPU 16. SNES 감성의 실루엣·가독성에 현대 수준의 팔레트·셰이딩·애니메이션 밀도. 8비트 회귀 금지.
- **레퍼런스**: 그라디우스 V, R-Type(생체+기계 융합, 거대 보스 실루엣), Blasphemous·Huntdown(640×360급 hi-bit 셰이딩 밀도).
- 톤: 어두운 우주 배경 위에 채도 높은 기체/탄/이펙트. 탄과 아이템은 항상 최고 가시성 — 배경보다 2단계 이상 밝게.

## 에셋 규격 (v2)

| 에셋 | 크기(px) | 파일 | 애니메이션 |
|---|---|---|---|
| 플레이어 기체 | 48×30 | player_ship.png / .ase | 아이들, 상/하 뱅킹, 엔진 화염 (4~6프레임) |
| 잡졸 (zako) | 24×24 | enemy_<id> | 아이들 2~4프레임 + 사망 |
| 중형/터렛 | 32×32 ~ 48×48 | enemy_<id> | 공격 예열 + 사망 |
| 미니보스 | 96×96급 | miniboss_<id> | 페이즈별 상태 |
| 스테이지 보스 | 128×128 ~ 256×160 | boss_<id> | 멀티파트, 단계별 파괴 |
| 파워업 캡슐 | 16×14 | capsule.png | 발광 펄스 2~4프레임 |
| HUD 슬롯 아이콘 | 24×12 | hud_icon_<slot> | 정적 |
| 폭발 (소/중/대) | 24 / 48 / 96 | fx_explosion_<size> | 6~10프레임 시트 |

- 투명 배경(알파), **팔레트 에셋당 48색 이내**, 안티앨리어싱 없는 하드 픽셀 경계.
- 배경: 테마당 패럴랙스 3~4겹 (원경 성층 / 중경 구조물 / 근경 실루엣 / 전경 파티클), 겹당 640×360 이상 타일러블.
- 임포트 설정은 BattleSceneBuilder/임포트 스크립트가 일괄 처리 (프로젝트 표준 Retro AA).

## 생성 파이프라인 (이중화, 2026-07-28 확정)

| 용도 | 도구 | 비고 |
|---|---|---|
| 키프레임·대형(보스, 배경, 48px+) | **OpenAI gpt-image-2** | 4배 크기 생성 → 최근접 다운스케일 → 48색 양자화. `background: transparent` |
| 소형(≤32px)·애니 프레임·회전 | **PixelLab API** (api.pixellab.ai/v2) | 네이티브 저해상도 생성. animate/rotations 엔드포인트로 프레임 일관성 확보 |
| SFX | 생성 API (M1에서 확정) | 아트와 동일한 후보 다량 생산 → 큐레이션 |

절차:
1. `Tools/ArtGen/` 스크립트로 생성 (프롬프트·파라미터·시드를 산출물과 함께 커밋)
2. 후처리: 다운스케일(최근접) → 팔레트 양자화 → 규격 검증
3. Unity CLI `import_asset`으로 임포트 → `capture_game_view`로 인게임 확인 캡처
4. 사람 큐레이션: 합격작만 `Assets/Art/`에 확정. 애니메이션은 Aseprite(.ase) 경유로 클립 자동 생성

### 프롬프트 템플릿 (gpt-image-2)

```
Pixel art sprite of a sleek sci-fi space fighter ship, side view facing right,
hi-bit 16-bit style like SNES-era Gradius and R-Type with modern shading,
3-4 tone ramps per material, dark blue and steel hull with orange engine glow,
hard pixel edges, no anti-aliasing, transparent background, 192x120 canvas,
limited 48 color palette
```

적/보스는 대상만 교체: `...biomechanical alien drone enemy, R-Type inspired
organic-mechanical fusion, menacing silhouette readable at 24x24...`

## 비고

- 좌표/히트박스는 GameData JSON이 원본 — 아트 크기 변경 시 halfWidth/halfHeight는 GROK을 통해 갱신 (REQ-006 참고).
- 코드 생성 플레이스홀더(BattleSceneBuilder 픽셀 배열)는 해당 에셋이 실파일로 교체되는 시점에 제거.
