# REQ-082 (CODEX): 보스 등장 시퀀스·움찔 수정 + ObstacleDamaged + 게이지 6칸 (6차 피드백)

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main` (main = ffad859).
사람 피드백 4건이 전부 Core 소관이다. 결정론 규칙(AGENTS.md §4) 준수.

## A. 보스 등장 시 졸개가 갑자기 사라짐 (사람: "졸개들이 가고 잠시 뒤에 등장하는 걸로")

증상: 보스(중간보스 포함) 등장 시점에 화면의 졸개들이 즉시 사라진다.
원인을 먼저 찾아라 (스테이지 틱 소진 시 일괄 제거로 추정 — `_bossEntryStartTick` 주변).

요구 시퀀스:
1. 보스 진입 전 여유 구간(제안 ~90틱)부터 신규 스폰 중단.
2. 살아 있는 졸개는 **즉시 삭제 금지** — 자기 이동으로 화면 밖으로 나가 자연 퇴장하게 하라
   (필요하면 퇴장 가속/강제 좌측 이동 플래그. 화면 안에서 pop-out은 안 된다).
3. 필드가 비고 **잠시 뒤**(제안 ~60틱 정적) 보스 활강 진입 시작 — 기존 활강 연출 유지.
4. 미지의 구역(HiddenBoss)·중간보스 모두 동일 규칙.

주의: 스테이지 총 틱과 보스 진입 틱 산정이 바뀌면 BalanceSim 클리어 게이트 재검증.

## B. 보스 움찔거림 (사람: "중간중간 움찔하는 듯한 액션이 너무 이상해")

패턴 전환 배너 문제(기수정)와 별개로 **이동 자체가 순간적으로 튀는** 증상이다.
의심 지점: 페이즈 전환 시 사인 호버 앵커(`_bossMovementAnchorY`)/위상(`_bossMovementPhaseOffsetTicks`) 재설정
불연속, 진폭·주기가 다른 페이즈로 넘어갈 때 y 점프, multipart 보스 파츠 위치 갱신 순서.
원인을 특정하고 **위치·속도가 연속**이 되게 수정하라 (전환 틱에서 y와 dy가 이어져야 한다).
수정 전후를 같은 시드로 비교해 전환 틱 근방 y 시퀀스가 매끄러움을 테스트로 증명하라.

## C. ObstacleDamaged 이벤트 추가 (사람: "장애물에 데미지 주는 표시가 안 남")

`SimEventType.ObstacleDamaged = 33`: EntityId = obstacle id, X/Y = 피격점, Arg = 남은 HP.
장애물이 플레이어 탄/미사일/폭탄에 맞을 때마다 발행 (파괴 틱은 기존 ObstacleDestroyed만).
Presentation(CLAUDE)이 피격 플래시에 쓴다. 이벤트 추가는 리플레이 스키마와 무관하지만 확인해라.

## D. 파워업 게이지 5→6칸 (사람: "스피드 다음 기본 샷 강화 추가")

- `PowerUpGauge.ShipGaugeSlotCount` 5 고정을 **6으로 확장** (파서·ShipMeta·RunManager 정합 포함).
- 기체 게이지 슬롯에 **MainShot 종류**가 오면 활성화 시 주무기(main) 레벨 +1이 되는지 보장
  (main 레벨 축은 이미 존재 — REQ-060에서 시작 레벨을 만졌던 그 축).
- 데이터는 GROK이 이어서 넣는다(["Speed","MainShot","Missile","Weapon","Option","Shield"]).
  네가 Core 기본값/테스트 픽스처에서 5칸을 가정한 곳을 전부 6칸 호환으로 바꿔라.
- 서스펜드/리플레이에 게이지 커서·적립이 실리므로 **스키마 버전 인상 여부를 판단**하고,
  올리면 구버전 명시 거부 테스트 갱신.

## 검증 (전부 필수)

- `Tools/CoreStandalone dotnet test` 전부 통과 (현재 408 + 신규)
- `Tools/DeterminismAudit` AUDIT PASS, 같은 시드 2회 해시 일치
- BalanceSim all green (A의 틱 변화 반영)
- Unity 호환: EditMode가 참조할 Core 심벌 public, `Assert.Multiple` 금지

완료 보고는 `Reviews/from-codex/req082-report.md`. 커밋은 샌드박스 제약으로 오케스트레이터가 대신 한다 — 작업 트리에 결과만 남겨라.
