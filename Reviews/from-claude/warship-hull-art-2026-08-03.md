# 전함 함체 아트 제작 + 보스 스프라이트 미등록 2종 (build32)

- 담당: CLAUDE (RENDERER), 2026-08-03
- 발단: 사람이 build31 스크린샷을 보고 "전함에 일반 보스 3개가 겹쳐 있는데 잘못된 거
  아니냐"고 지적. **잘못이 아니라 미완성이었다** — `WarshipView`가 함체 아트 부재를
  기존 스프라이트 조립(함미=boss_fortress, 코어=boss_core, 포탑=obstacle_laser_turret)
  으로 대신하고 있었고, 그게 "보스 세 마리"로 읽혔다. 사람 지시로 함체 아트를 제작했다.

## 1. warship_hull.png 제작 (ART-DIRECTION.md 320×160 슬롯)

- 도구: `Tools/ArtGen/artgen.py openai --model gpt-image-1.5 --size 1536x1024 --n 2`
  → `post --target 320x160 --colors 48`. ART-DIRECTION §생성 파이프라인의 "대형
  키프레임은 gpt-image-1.5" 규칙 그대로다(gpt-image-2는 투명 배경 미지원).
- 프롬프트 요구사항을 슬롯 규격에 맞췄다: 측면도, 뱃머리 왼쪽·함미 오른쪽,
  갑판 상/하단은 포탑이 얹히므로 **평평한 장착면으로 비움**, 포탑은 그리지 않음.
- 2안 생성 후 A안 채택(41색). 원본·B안·최종본은 `Tools/ArtGen/out/`에 보존 —
  재현·교체 가능(같은 파일명으로 덮고 씬 재생성).
- 배선은 이미 있었다(`WarshipView._hullSprite`): 파일이 들어오자 실루엣 판 3장이
  자동으로 한 장에 자리를 내줬다. 코드 수정 0.

## 2. 곁가지로 드러난 진짜 버그 — 보스 스프라이트 2종 미등록

"다른 보스도 미완성인 게 있나" 확인 중 발견. `art-input/boss_broodmother.png`와
`boss_leviathan.png`는 **REQ-035 때 이미 제작돼 들어와 있었는데**
`BattleSceneBuilder`의 보스ID→스프라이트 매핑에 등록이 빠져 있었다.

`BattleDirector.ApplyBossSprite`는 매칭 실패 시 `best=null`로 **이전 스프라이트를
그대로 유지**한다 — 즉 미지의 구역 거대 보스 2종이 직전 보스(초기값 boss_stage1)
그림으로 나오고 있었다. 조용히 틀리는 종류의 버그다. 등록을 추가했다.

`boss_core_prism`(최종 보스 2형태)은 **의도적으로 미등록** — prefix 매칭이 길이
우선이라 `boss_core`를 물려받는 게 맞다.

전수 조사 결과 나머지 art-input 자산은 전부 배선돼 있다(배경 테마·폭발·기체 애니·
노드 아이콘은 문자열 조립 루프로 로드돼 정적 grep에 안 잡힐 뿐이다).

## 3. 검증

| 항목 | 결과 |
|---|---|
| EditMode (`unity test --mode EditMode`) | **547/547 통과** |
| Core (`dotnet test`) | **554/554 통과** |
| 씬 재생성 (scene31.log) | 에러 0, `외부 아트 적용: warship_hull.png` 확인 |
| WebGL build32 | `Build Finished, Result: Success` |
| 인게임 headless (St3 fortress, 시드 2) | 함체 1장이 실루엣 판을 대체, 함미·포탑 4문·코어가 갑판 위에 정렬. 콘솔 에러 0건 |

## 4. 남은 것

- 함체 아트는 **사람 큐레이션 대상**(ART-DIRECTION §절차 4). B안이 마음에 들면
  `Tools/ArtGen/out/final/warship_hull_B.png`로 교체 후 씬 재생성만 하면 된다.
- 미지의 구역 보스 2종은 스프라이트 등록만 고쳤고 **인게임 확인은 아직**이다 —
  히든 조건 2개를 채워야 열리는 항로라 headless 도달 비용이 크다. 다음 테스터가
  그 경로를 탈 때 함께 봐 주면 좋겠다.
