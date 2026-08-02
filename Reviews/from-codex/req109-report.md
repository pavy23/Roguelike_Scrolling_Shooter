# REQ-109 보고서 — St5 타임루프 고스트

- 담당: CODEX / SIMULATION
- 일자: 2026-08-02
- 상태: Core 구현 및 standalone 검증 완료
- 커밋: 오케스트레이터 수행 예정

## 결론

- 이번 런의 St1 입력을 `RunManager`가 기존 `InputRecorder`의 RLE 버퍼로 직접 보관한다.
- 최종 바이옴 `Closing` 진입 시 기록이 있으면 무적·무충돌 `GhostState`를 활성화하고, 정수 전용 입력 재생으로 과거 궤적을 만든다.
- 고스트는 옵션·미사일·봄·게이지·현재 무기 진화를 사용하지 않는다. `GhostMainShot` 직선탄만 고정 저레벨로 발사하며 실제 적/장애물/보스 충돌 및 데미지 파이프라인에 참여한다.
- 고스트 상태, 입력 기록, 이동 나머지, 발사 쿨다운, 탄의 고정 데미지, lifecycle 이벤트를 결정론 해시에 포함했다.
- St1을 거치지 않은 St3 직행 dev 런은 기록도 고스트도 만들지 않는다.
- suspend는 기록과 최종 구간 경계의 재생 상태를 보존해야 하므로 v26 유지가 불가능하다고 판단해 **v27**로 올렸다. v26은 명확히 거부한다.

## 기록 및 컨티뉴 정책

1. St1 첫 전투 경계에서 플레이어 위치, 이동 속도 유리수, 이동 한계를 시작 상태로 캡처한다.
2. `RunManager.Step`에 들어온 원본 `InputCommand`를 St1 동안 틱 순서대로 RLE 기록한다.
3. 정상 진행은 St1 보스 격파 틱에 기록을 확정한다.
4. St1에서 사망하면 그 첫 시도의 사망 틱까지 기록하고 즉시 확정한다.
5. 같은 런에서 컨티뉴해 해당 구간을 다시 플레이해도 확정된 첫 기록을 덮어쓰거나 이어 붙이지 않는다.
6. 컨티뉴가 아닌 `Restart`는 새 런이므로 기록을 비우고 새 St1 기록을 시작한다.
7. 예약한 RLE run 상한에 도달하면 현재까지의 기록을 안전하게 확정한다. 기본 상한은 최악의 경우 매 틱 명령이 달라지는 10분(`36,000` runs)이며 `GhostReplayConfig`에서 조절할 수 있다.

`InputRecorder`에는 무할당 `TryRecord`, RLE run 조회, 경계 prefix export/restore를 추가했다. 전체 기록을 매 감사 틱 다시 순회하면 장시간 감사가 이차 시간이 되므로, 명령 시퀀스를 틱 순서대로 접는 증분 FNV-1a 해시도 유지한다.

## 재생과 화력

- 활성 조건: `BiomeIndex == FinalStageIndex && StageSection == Closing && HasStageOneGhostRecording`
- 기본 고스트 entity id: `RunManager.StageOneGhostEntityId == 1`
- 기본 고정 무기 레벨: 1
- 기본 발사 간격: 8틱
- 고정 탄 데미지: `Damage.Compute(런 시작 main-shot base damage, FixedWeaponLevel)`
- 탄 종류: `BulletKind.GhostMainShot`
- 탄은 플레이어 faction으로 실제 충돌하지만 옵션, 미사일, burst/beam, 관통/도탄 modifier를 상속하지 않는다.
- 고스트 본체는 `BattleSim` entity/collider로 등록하지 않으므로 피격·접촉·지형 충돌이 없다.
- St1 전체 기록이 Closing 한 방보다 길 수 있으므로 고스트는 Closing에서 시작해 최종 보스 경계까지 상태와 재생 커서를 유지한다. 기록이 끝나거나 최종 보스가 먼저 끝나면 `GhostEnded`로 종료한다.

입력→궤적 변환은 `GhostReplayMotion.Advance`의 정수 순수 함수다. 디지털 대각선 정규화와 아날로그 속도 clamp는 `BattleSim`의 정수 규칙을 그대로 사용한다. St5 환경 hazard는 고스트 본체에 적용하지 않는다.

## 관측 API

Presentation이 사용할 public 표면은 다음과 같다.

- `RunManager.Ghost : GhostState`
  - `Active`, `EntityId`, `X`, `Y`, `IsFiring`, `PlaybackTick`
  - 발사 쿨다운과 이동 remainder도 감사/디버그용 public 상태로 제공
- `RunManager.HasStageOneGhostRecording`
- `RunManager.StageOneGhostRecordedTicks`
- `RunManager.StageOneGhostRecordedRunCount`
- `RunManager.StageOneGhostRecordingStart`
- `SimEventType.GhostSpawned`
  - `EntityId=ghost id`, `X/Y=spawn`, `Arg=fixed weapon level`
- `SimEventType.GhostEnded`
  - `EntityId=ghost id`, `X/Y=final point`, `Arg=replayed ticks`
- `BulletKind.GhostMainShot`
  - `BulletState.FixedDamage`에 현재 플레이어 빌드와 무관한 정확한 데미지 표시

Presentation은 `GhostState`를 반투명 잔상 기체로 렌더링하고 lifecycle 이벤트를 fade-in/fade-out 및 SFX의 권위 신호로 사용하면 된다. `GhostMainShot`은 일반 플레이어 탄과 구분되는 팔레트만 적용하면 된다. Presentation 소유 파일은 수정하지 않았다.

## suspend v27

`RunSuspendData.ghostRecording`에 다음을 canonical checksum과 함께 보관한다.

- St1 RLE input prefix와 `totalTicks`
- 시작 위치, 속도 유리수, 이동 한계
- 기록 시작/확정 상태
- 고정 무기 레벨, 발사 간격, recorder 상한
- 최종 Closing/보스 경계의 활성 여부, 위치, 발사 프레임, playback tick, 쿨다운, 이동 remainder

`ExportSuspendData`는 현재 전투 중간값이 아니라 기존 계약대로 현재 전투의 tick-zero 경계를 내보낸다. 따라서 St1 진행 중에는 해당 방 시작 시점의 input prefix만 내보내고, 최종 구간에서는 해당 방 시작 시점의 ghost playback 상태를 내보낸다.

v26에는 이 기록이 없어 리줌 후 St5 화력과 해시가 달라진다. 자동으로 “기록 없음” 처리하는 것은 정상 런의 최종전 결과를 바꾸므로 v26을 거부했다. v27 checksum 변조와 RLE canonical 형식, 시작 상태, config, playback 위치를 모두 검증한다.

최종 보스 경계 suspend 왕복 테스트 중 기존 보상 기록 복원기가 보상 시퀀스를 전역 오름차순으로 잘못 가정한 문제도 수정했다. 실제 번호 체계는 바이옴 보스 뒤 다음 바이옴에서 작아질 수 있다. RNG/번호 생성은 변경하지 않고, 복원 검증을 “끝난 시퀀스 그룹이 나중에 재등장하지 않음”으로 교정했다.

## 결정론

- 신규 RNG 없음
- 벽시계/부동소수점/UnityEngine 없음
- 입력 기록과 시작 상태의 순수 정수 함수
- Ghost 상태 및 미래에 영향을 주는 movement remainder/쿨다운까지 `DeterminismAuditHasher`에 포함
- `BulletState.FixedDamage`와 `GhostMainShot`도 battle hash에 포함
- suspend payload 전체가 canonical checksum 대상

## 테스트

`Req109GhostReplayTests` 7개:

- 같은 St1 기록 + 같은 시드 → 매 ghost tick 궤적/발사/전체 hash 일치
- 고정 레벨 직선탄 생성 및 현재 옵션과 분리
- `GhostMainShot`이 적 HP에 정확한 실제 데미지 적용
- St1 첫 사망 기록을 컨티뉴 재시도가 덮어쓰지 않음
- St3 직행 dev 런은 기록/고스트/event 없음
- v27 suspend가 기록을 왕복하고 ghost field checksum 변조를 감지하며 v26 거부
- 최종 보스 tick-zero 경계에서 활성 ghost 상태와 다음 틱 hash까지 정확히 복원

`Assert.Multiple`은 사용하지 않았다.

## 검증 결과

### REQ-109 집중 감사

```text
REQ-109 AUDIT PASS seed=0x109A ghostTicks=4 hash=A7B0D1A50F62231B
PASS 7 / FAIL 0 / SKIP 0
```

### CoreStandalone 전체

```text
dotnet test --no-restore
PASS 521 / FAIL 0 / SKIP 0
duration 37s
```

### DeterminismAudit 전체 suite

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
6/6 scenarios PASS
cap-boundary: 256/256 qualifying seeds PASS
AUDIT PASS
```

이 suite는 5-stage 실제 GameData 경로를 두 번씩 독립 실행하므로 St1 기록과 St5 Closing 고스트 활성 경로를 포함한다.

### 같은 시드 독립 2회

```text
dotnet run --no-restore --no-build --project Tools/DeterminismAudit -- 12345 5 704760
```

두 실행 모두:

```text
hash=E8137233B7837508
completedStages=5/5
completedRooms=15/15
ticks=34503
state=RunCleared
```

판정: **SAME-SEED MATCH (ghost-active path 포함)**.

### 정적/형식 검사

```text
Assert.Multiple / UnityEngine / System.Random / Guid.NewGuid /
DateTime.Now / Environment.TickCount: changed files 0건
git diff --check: PASS
```

## 변경 파일

- `Assets/Scripts/Core/Simulation/GhostReplay.cs` + `.meta`
- `Assets/Scripts/Core/Simulation/InputRecording.cs`
- `Assets/Scripts/Core/Simulation/RunManager.cs`
- `Assets/Scripts/Core/Simulation/RunSuspendData.cs`
- `Assets/Scripts/Core/Simulation/BattleSim.cs`
- `Assets/Scripts/Core/Simulation/DeterminismAuditHasher.cs`
- `Assets/Scripts/Core/SaveDataIntegrity.cs`
- `Assets/Tests/EditMode/Req109GhostReplayTests.cs` + `.meta`
- `Assets/Tests/EditMode/WeaponExpansionTests.cs`
- `Reviews/from-codex/req109-report.md`

요청대로 커밋하지 않았다.
