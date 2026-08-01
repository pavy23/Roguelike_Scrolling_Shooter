# REQ-094 CODEX 구현 보고

## 결과

REQ-094 A/B를 CODEX 소유 영역에 구현했다. 커밋은 요청대로 만들지 않았다.

### A. 자기 제약형 계약 축

- ContractDefinition과 ContractEffectType에 다음 공개 축을 추가했다.
  - GaugeActivationBanned
  - OptionActivationBanned
  - ShieldActivationBanned
- waves.json 계약 DTO/파서가 다음 optional camelCase 필드를 읽는다.
  - gaugeActivationBanned
  - optionActivationBanned
  - shieldActivationBanned
- 기존 waves 스키마 버전은 올리지 않았다. 세 필드는 누락 시 모두 false인 하위 호환 optional 필드이며, 기존 계약 데이터의 의미와 파싱 결과를 바꾸지 않는다.
- RunManager가 계약 선택 및 suspend 복원 때 제약을 PowerUpGauge에 적용한다. 첫 바이옴/런 재시작에서는 해제한다.
- 게이지 전체 잠금이어도 Collect()의 캡슐 적립/커서 순환은 유지한다. 잠긴 발동은 레벨·진행도·커서를 소비하지 않는다.
- Presentation 관측용 공개 결과를 추가했다.
  - PowerUpActivationResult.ContractGaugeActivationBanned
  - PowerUpActivationResult.ContractOptionActivationBanned
  - PowerUpActivationResult.ContractShieldActivationBanned
- CanActivate도 현재 선택 슬롯의 계약 잠금을 반영한다.
- 결정론 해시에 계약 효과, 게이지 잠금 상태, 마지막 발동 결과를 포함했다.

리플레이/suspend 판단:

- 리플레이에는 기존처럼 계약 선택 이력의 계약 ID가 기록되고, 같은 GameData 계약 카탈로그에서 세 제약을 재구성한다. 입력 스키마 변경은 필요 없다.
- suspend에는 기존 activeContractId와 계약 선택 이력이 이미 있으므로 별도 중복 필드는 추가하지 않았다. 복원 시 활성 계약 정의에서 게이지 잠금을 재적용한다.
- 전용 테스트에서 계약 선택 → suspend/export → resume → 입력 녹화/playback 후 전체 관측 상태 해시 일치를 검증했다.

### B. 런 통계

- BattleStatistics.BombsUsed와 RunStatistics.BombsUsed를 공개했다.
- 폭탄 재고가 실제로 소비되고 BombActivated가 발생한 성공 발동만 센다. 재고 0 거부는 세지 않는다.
- 방/배틀 교체 때 RunManager가 포화 덧셈으로 누계한다.
- 기존 RunStatistics.GrazeCount는 이미 현재 배틀 + 완료 배틀을 포화 합산하는 런 누계였다. 회귀 테스트와 결정론 해시 경로를 유지·확인했다.
- 도달 지점은 기존 공개 StageIndex/RoomIndex로 충분하며 추가 필드를 만들지 않았다.
- 최대 배율은 요청대로 Core에 추가하지 않았다.

suspend 스키마 판단:

- BombsUsed는 재고만으로 역산할 수 없다. 획득·보상·상한 변화가 있으므로 정확한 런 누계 복원을 위해 RunSuspendData를 v21 → v22로 올리고 bombsUsed를 추가했다.
- v21은 기존 체크섬 검증 후 v22로 이행하며 BombsUsed = 0으로 초기화한다. v21의 기존 저장 의미를 보존한다.
- v22 체크섬, 음수 검증, export/resume, 결정론 해시에 BombsUsed를 포함했다.

## 테스트 추가/갱신

- 게이지 전체/OPTION/SHIELD 잠금 사유, 비소비 거부, 비대상 슬롯 허용
- 계약 JSON optional 필드 파싱 및 효과 뷰
- 계약 선택 → suspend 복원 → replay 입력 후 상태 해시 일치
- 폭탄 성공 발동/빈 재고 거부 카운트
- 방 경계 런 누계 및 suspend 복원
- v21 → v22 BombsUsed = 0 이행
- 기존 스키마 버전 기대값 v22 갱신
- NUnit 3 호환 유지, Assert.Multiple 미사용

## 검증

1. Tools/CoreStandalone에서 dotnet test --no-restore
   - PASS: 477/477, 실패 0, 스킵 0
2. dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
   - 6/6 전체 런 시나리오 PASS
   - 256-seed cap-boundary 감사 PASS
   - 최종 출력: AUDIT PASS
3. dotnet run --no-restore --project Tools/BalanceSim 동일 구조 2회 실행
   - 양쪽 exit code 0
   - 양쪽 최종 출력: PASS: BalanceSim all checks green.
   - 전체 출력 비교: OUTPUT_MATCH=True
4. git diff --check
   - 오류 없음
5. 변경 Core 금지 API 감사
   - UnityEngine, System.Random, Guid.NewGuid, DateTime.Now, Environment.TickCount 신규 사용 없음

