# ART-DIRECTION.md — HD 도트(hi-bit) 파일럿 기준

사람 + RENDERER가 관리한다. 에이전트는 이 규격을 벗어나는 아트를 임포트하지 않는다.

## 방향

- **hi-bit 픽셀아트**: SNES 감성의 저해상도 캔버스(384×224, PPU 16)를 유지하되, 팔레트·셰이딩·디테일은 현대 수준. 8비트(아타리/NES) 회귀 금지.
- **레퍼런스**: 그라디우스 V, R-Type (특히 생체+기계 융합 적 디자인, 거대 보스 실루엣), 현대 hi-bit 계열(Blasphemous, Huntdown)의 셰이딩 밀도.
- 톤: 어두운 우주 배경 위에 채도 높은 기체/탄/이펙트가 읽히는 대비. 탄과 아이템은 항상 최고 가시성.

## 에셋 규격 (파일럿 대상)

| 에셋 | 크기(px) | 파일 | 비고 |
|---|---|---|---|
| 플레이어 기체 | 32×20 | player_ship.png | 우측 향. 엔진 글로우 포함 |
| 잡졸 (zako 계열) | 16×16 | enemy_<id>.png | 종류별 실루엣 차별화 |
| 엘리트/탱커 | 24×24 | enemy_<id>.png | |
| 파워업 캡슐 | 12×10 | capsule.png | 주황 발광, 최고 가시성 |
| HUD 슬롯 아이콘 | 16×8 | hud_icon_<slot>.png | S/M/O/SH 심볼 |

- 투명 배경(알파), **팔레트 32색 이내**(에셋당), 안티앨리어싱 없는 하드 픽셀 경계.
- Unity 임포트: Point 필터 아님 — 프로젝트 표준(Retro AA)을 따르므로 임포터 설정은 BattleSceneBuilder/임포트 스크립트가 일괄 처리.

## AI 생성 파일럿 절차

**생성 모델 (검토 결과, 2026-07)**: Claude는 이미지 생성 불가 → 1순위 **OpenAI gpt-image-1** (지시 이행 정밀, 투명 배경 네이티브, 인페인팅 반복 수정), 2순위 **Gemini/Imagen** (시리즈 에셋 스타일 일관성 — 같은 프롬프트 교차 생성해 비교), 보조 xAI. 범용 모델이 부족하면 픽셀아트 특화 도구(Retro Diffusion 등) 검토.

1. 아래 프롬프트 템플릿으로 이미지 모델에서 **4배 크기(예: 기체 128×80)**로 생성 (직접 16px급 생성은 뭉개짐)
2. 후처리: 정수 다운스케일(최근접) → 팔레트 양자화(32색) → 배경 제거 → 규격 크기 확인
3. `Assets/Art/Sprites/`에 교체 배치 → 씬 재생성 → 빌드에서 픽셀 스케일 확인
4. 파일럿 평가: 플레이어 기체 + 잡졸 1종만 먼저. 품질 합격 시 전 에셋 확장.

### 프롬프트 템플릿 (이미지 모델용)

```
Pixel art sprite of a sleek sci-fi space fighter ship, side view facing right,
hi-bit 16-bit style like SNES-era Gradius and R-Type, modern pixel art shading
with 3-4 tone ramps per material, dark blue and steel hull with orange engine
glow, hard pixel edges, no anti-aliasing, transparent background, 128x80 canvas,
sprite sheet style, limited 32 color palette
```

적 유닛은 위 템플릿에서 대상만 교체:
```
...biomechanical alien drone enemy, R-Type inspired organic-mechanical fusion,
menacing silhouette readable at 16x16...
```

## 비고

- 좌표/히트박스는 GameData JSON이 원본 — 아트 크기를 바꾸면 halfWidth/halfHeight도 GROK을 통해 갱신할 것.
- 코드 생성 플레이스홀더(BattleSceneBuilder의 픽셀 배열)는 해당 에셋이 실파일로 교체되는 시점에 제거.
