# REQ-089 (CODEX, 핫픽스): 기체 무기(더블/트리플/레이저) 발사 불능 회귀

너는 CODEX = SIMULATION 담당이다. 작업 디렉토리 wt-sim, 시작 전 `git merge main`.
**라이브 회귀다 — 최우선.**

## 증상 (사람 실플레이, 7차 배포 직후)

- 게이지에서 더블/트리플/레이저를 발동해도 **특수탄이 나가지 않는다**
- **진화(2·3단계 추가 파워업)도 나타나지 않는다**
- REQ-086(무기 모드 슬롯 maxLevel 3 + 진화 축) 이전 빌드에서는 정상이었다

## 오케스트레이터 선행 조사 (그대로 믿지 말고 재확인해라)

- 데이터는 정상으로 보인다: powerUpGauge.slots의 Double/Laser/Triple maxLevel 3,
  families에 levels[] (2·3단계) 존재. 단 **double의 weaponType이 'spread'**로 되어 있다
  (REQ-088에서 바뀌었는지 확인 필요).
- Core 체인: PowerUpGauge.ActivateWeaponMode → ActiveWeaponMode → BattleSim.ApplyGaugeWeaponMode
  → FindPrimaryWeaponFamily → ApplyPrimaryWeaponProfile + ApplyPrimaryWeaponLevel(GetLevel(lv)).
  코드상 명백한 단절은 못 찾았다 — **454개 테스트가 전부 픽스처 데이터라 실데이터 경로를
  안 탄다**는 것이 유일하게 확실한 갭이다.
- 의심 지점: (a) definition.GetLevel(1) — levels[]에 1이 없을 때의 폴백,
  (b) double(weaponType spread) L1의 spreadWays/shotAngleLutSlots 소비 경로,
  (c) 함선 게이지 'Weapon' 자리 → Double/Laser/Triple 슬롯 매핑이 REQ-086 이후에도
  ActivateWeaponMode로 이어지는지, (d) RunManager 스테이지 전환 시 ApplyGaugeWeaponMode
  재적용 여부.

## 요구

1. **실데이터 재현 먼저**: 리포지토리 GameData JSON을 GameDataParser로 파싱해
   RunManager+BattleSim을 구동하고, 캡슐 적립→Weapon 슬롯 발동→틱 진행 후
   (a) EquippedPrimaryWeaponFamily 전환 (b) 기대 각도/갈래의 플레이어 탄 생성
   (c) 재발동 시 진화 레벨 2·3 반영을 검증하는 **통합 테스트**를 작성해라.
   세 기체(double/triple/laser) 전부. 이 테스트가 먼저 **빨갛게 재현**되어야 한다.
2. 근본 원인을 고쳐라. 원인이 데이터(GameData)라면 수정하지 말고 정확한 진단을
   보고서에 남겨라 — GROK에 넘긴다.
3. 회귀 테스트는 실데이터 경로로 남긴다 (다시는 픽스처만으로 통과 못 하게).

## 검증

- 신규 통합 테스트 포함 dotnet test 전부, DeterminismAudit AUDIT PASS, 같은 시드 2회
- 보고서 `Reviews/from-codex/req089-report.md`: 근본 원인 한 문단 필수. 커밋은 오케스트레이터가 대신한다.
