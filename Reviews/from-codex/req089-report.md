# REQ-089 라이브 무기 모드 회귀 조사 보고서

- 작업일: 2026-08-01
- 담당: CODEX / SIMULATION
- 판정: **Core 발사 체인 정상, Presentation 후속 필요**
- 커밋: 오케스트레이터 수행

## 근본 원인

**근본 원인:** 리포지토리 `GameData` 6종을 실제 라이브와 같은 `GameDataParser.Parse` 오버로드로 읽고 같은 `RunManager` 생성자와 자동발사 입력을 사용한 통합 재현에서 starter/Double, interceptor/Triple, bulwark/Laser 모두 게이지 활성화 즉시 `EquippedPrimaryWeaponFamily`가 전환되고 L1→L2→L3의 2/3/4-way, 3/5/5-way, 1/1/Player beam이 정확히 생성되었으므로 Core나 `GameData`에는 보고된 “발사 불능” 단절이 없다. 반면 Presentation의 `BattleDirector`는 모든 실데이터 함선의 시작 `weaponType`인 `vulcan`으로 `_mainShotSprite`와 SFX 계열을 Awake에서 한 번만 설정하고, Core가 이미 공개하는 `IBattleSim.PlayerWeaponType` 변화는 추적하지 않으며, `PowerUpHudView.EvolutionName`은 소문자 `double/triple/laser`를 기대하지만 실데이터 `nameKey`는 `Double Shot/Triple Shot/Laser`라 단계명이 폴백된다. 따라서 확인 가능한 회귀 원인은 Core 상태가 아니라 무기 전환 후 Presentation이 시각·음향 정체성과 진화명을 갱신하지 않는 소비자 측 캐시/키 불일치이며, “탄 자체가 전혀 안 보임” 주장까지 확정하려면 CLAUDE 소유 Unity 런타임에서 Core bullet/laser count와 풀 뷰 count를 같은 프레임에 캡처해야 한다.

## 실데이터 통합 회귀 테스트

`Assets/Tests/EditMode/Req089LiveWeaponModeTests.cs`를 추가했다.

- `GameData/enemies.json`, `weapons.json`, `waves.json`, `rewards.json`,
  `ships.json`, `scoring.json`을 직접 읽는다.
- 라이브 `BattleDirector`와 동일하게 `SegmentStageGenerator`,
  `CreateBattleSimConfig()`, 함선별 `CreatePowerUpGauge(ship)`, 난이도 `1/1`
  `RunManager` 생성 경로를 사용한다.
- 캡슐 `Collect()` 4회 순환으로 실제 함선의 네 번째 `Weapon` 슬롯을 선택하고,
  자동발사 상태에서 활성화한다.
- 세 기체 모두 장착 계열, 활성 모드, 진화 레벨, 새 플레이어 탄 ID와 갈래,
  전/상/하/후방 방향, Laser L3 `LaserSourceKind.Player`를 검증한다.

이 신규 테스트는 수정 전 Core에서도 3/3 통과했다. 요청된 증상을 빨갛게 만들 수
없었으므로 증거 없이 Core 제품 코드를 변경하지 않았다.

## 실제로 빨갛게 발견·수정한 테스트 결함

전체 테스트 첫 실행은 `OptionalLevelAxesParseWithoutWeaponsSchemaBump`에서 실패했다.
이 테스트가 과거 `weapons.json`에 `spread.levels`가 없던 시절의 문자열 치환으로
가짜 levels를 삽입했는데, REQ-088 실데이터에는 이미 levels가 있어 JSON에 동일 멤버가
두 번 생겼다. 테스트를 `RepositoryLevelAxesParseWithoutWeaponsSchemaBump`로 바꾸고
리포지토리 JSON을 변형하지 않은 채 직접 파싱하도록 수정했다. 이는 발사 로직 결함이
아니라 실데이터 도착 후 남은 픽스처성 테스트 결함이다.

## 의심 지점 재확인

- `GetLevel(1)`: 파서가 family base profile을 L1로 앞에 붙여 3단 연속 배열을 만든다.
- Double `weaponType: spread`: `PrimaryWeaponFamilyDefinition`이 Double에 대해 이를
  필수로 검증하며, 실데이터 L1은 전방+상향 2-way로 발사된다.
- 함선 `Weapon` 자리: ships 파서가 `gaugeWeaponFamily`에 따라
  Double/Triple/Laser 슬롯으로 바꾸고 `CreatePowerUpGauge(ship)`가 즉시 활성화
  슬롯으로 복제한다.
- 스테이지 전환: `RunManager.Step`이 현재 Battle의 장착 계열을
  `CurrentPrimaryWeaponFamily` 및 `_battleConfig`에 먼저 동기화하며, 새 Battle의
  생성자 `ReadPowerUpLevels()`가 활성 모드와 진화 레벨을 다시 적용한다.

## 검증

```text
dotnet test --filter FullyQualifiedName~Req089LiveWeaponModeTests --no-restore
PASS 3/3

dotnet test --no-restore
PASS 457/457

dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
AUDIT PASS (6 scenarios + cap-boundary)

same seed 12345, stageCount 3, tickBudget 30000, two independent runs
RUN_1 hash=478CACA5AC713FDB ticks=17920
RUN_2 hash=478CACA5AC713FDB ticks=17920
EXACT_MATCH true
```

## 변경 범위

- Core 제품 코드: 변경 없음
- 테스트: 실데이터 통합 회귀 3케이스 추가, 구 실데이터 문자열 변조 테스트 제거
- `GameData/`, Presentation, 공유 파일: 변경 없음
