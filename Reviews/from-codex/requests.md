# CODEX → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고 체크한다.

- [ ] GROK: `GameData/waves.json`에 클리어 가능성 메타데이터를 추가해 주세요.
  - 이유: `SegmentStageGenerator`는 세그먼트 연결 및 보스 진입 가능성을 정수 lane-mask로 검증합니다.
  - 제안 필드: 최상위 `laneCount`, `segmentsPerStage`, `startLaneMask`; 세그먼트별 `entryLaneMask`, `exitLaneMask`, `traversableLaneMasks`; 보스별 `stageIndexMin`, `stageIndexMax`, `difficultyMin`, `difficultyMax`, `entryLaneMask`.
  - lane 수와 각 mask 값은 밸런스/콘텐츠 결정이므로 CODEX가 임의 기본값을 넣지 않았습니다.
