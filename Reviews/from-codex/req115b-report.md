# REQ-115b 구현 보고서 — 세그먼트 체인 미니언 + 흡입 역장

- 담당: CODEX / SIMULATION
- 상태: PASS
- 커밋: 하지 않음 (오케스트레이터 커밋 대기)
- 정독 문서:
  - `Reviews/from-claude/boss-redesign-2026-08-03.md` §필요한 Core 축 B
  - `Reviews/from-claude/hidden-boss-anchors.md`

## 1. 세그먼트 체인 미니언

### 피격 판단

Gradius 화염룡 문법을 따라 **머리만 피격 가능, 머리 파괴 시 체인 전체 소멸**로 확정했다.

- `SegmentChainDamageRule.HeadOnly`만 허용한다.
- JSON의 `hitRule`도 현재 `"headOnly"`만 받는다.
- 몸통 절은 충돌 접촉 판정은 있지만 플레이어 탄/레이저/폭탄 피해를 받지 않는다.
- 머리 HP가 0이 되면 모든 절 상태를 같은 틱에 제거하고 `SegmentChainDestroyed`를 발행한다.
- 체인은 별도 점수/드롭 수치를 임의로 만들지 않았다. 필요하면 GROK 밸런스 축으로 후속 데이터 필드를 추가한다.

### 이동과 소환

- `segmentCount`: 6~8만 허용한다.
- 머리는 플레이어 방향으로 매 틱 최대 `turnLutSlotsPerTick`만큼 완만히 선회한다.
  - 64칸 정수 LUT를 재사용하므로 1칸은 5.625도다.
  - 이동 속도와 잔여분은 정수/정확 유리수로 누적한다.
- 몸통은 머리 위치 원형 히스토리에서 `segmentIndex * followDelayTicks` 전 좌표를 읽는다.
  별도 부동소수점 관절 보간은 없다.
- 각 페이즈의 `segmentChain.summonCount`와 `summonIntervalTicks`가 실제 틱 스케줄을 제어한다.
  페이즈 진입 시 스케줄이 초기화되며 첫 체인은 보스 진입 완료 직후 소환된다.
- 보스 형태 전환/최종 격파 시 남은 체인은 함께 제거된다.

### 공개 관측 계약

- `IBattleSim.SegmentChains` / `SegmentChainState`
  - `ChainId`, `SegmentIndex`, `X`, `Y`
  - `IsHead`, `Damageable`
  - `HeadHp`, `HeadMaxHp`
- `SimEventType.SegmentChainSpawned`
  - `EntityId`: 체인 ID
  - `X/Y`: 머리 스폰점
  - `Arg`: 절 수
- `SimEventType.SegmentChainDestroyed`
  - `EntityId`: 체인 ID
  - `X/Y`: 파괴 시 머리 좌표
  - `Arg`: 제거된 절 수

### `waves.json` 예시

실제 HP/속도/간격은 GROK 소관이다.

```json
{
  "pattern": "lightning",
  "fireIntervalTicks": 45,
  "ways": 6,
  "bulletSpeed": 5,
  "hpThreshold": 0.5,
  "segmentChain": {
    "segmentCount": 8,
    "summonCount": 2,
    "summonIntervalTicks": 180,
    "headHp": 1200,
    "halfWidth": 0.75,
    "halfHeight": 0.5,
    "moveSpeed": 6,
    "turnLutSlotsPerTick": 1,
    "followDelayTicks": 4,
    "contactDamage": 1,
    "spawnOffsetX": -2,
    "spawnOffsetY": 0,
    "hitRule": "headOnly"
  }
}
```

## 2. 흡입 역장

기존 파츠 공격 `suction`을 REQ-115b 계약으로 확장했다.

- 입력 이동, 스테이지 drift, 흡입 외력을 같은 플레이어 이동 틱에 합산한다.
- 흡입 방향은 파츠의 확정 효과 원점을 향한 정규화 정수 벡터다.
- `effectSpeed`는 정확 유리수 외력 세기, `effectMaxSpeed`는 틱당 외력 상한이다.
- 기존 `effectMaxSpeed` 없는 데이터는 하위 호환을 위해 상한 없음으로 처리한다.
- 활성 종료 시 외력 잔여분을 초기화하므로 흡입 관성이 다음 페이즈에 새지 않는다.
- 탄/그레이즈 상태와 공유하는 값이나 RNG가 없으며, 흡입은 플레이어 이동에만 작용한다.
- REQ-115a 연결은 `BossPhase.partRules[].active/invulnerable/attack`을 그대로 사용한다.
  파츠가 비활성, 파괴, 무적 또는 보스 진입/형태 전환 상태이면 역장은 꺼진다.

### 확정 Broodmother 앵커

확정 아가리 파츠 중심 `(-0.1,+6.9)`과 흡입 중심 `(-3.4,+6.5)`이 다르므로
`effectOffsetX/Y`를 파츠 상대 좌표로 추가했다. Core 좌표는 1/256 단위여야 하므로 GROK 데이터에서는
다음 근사값을 사용한다.

```json
{
  "type": "suction",
  "effectSpeed": 3,
  "effectMaxSpeed": 5,
  "effectOffsetX": -3.296875,
  "effectOffsetY": -0.3984375
}
```

Broodmother 파츠 좌표도 같은 정수 서브유닛 규칙으로 양자화하면 최종 흡입 중심은
약 `(-3.3984375,+6.5)`가 된다.

### 공개 관측 계약

- `IBattleSim.SuctionActive`
- `SimEventType.SuctionStarted` / `SuctionEnded`
  - `EntityId`: 보스 ID
  - `X/Y`: `effectOffset`까지 합산된 실제 역장 중심
  - `PartId`: 소스 파츠 ID (`maw`)

Presentation은 이 이벤트 좌표를 왜곡 중심으로 사용하면 된다. 상태 차분으로 활성 전환을 추측할 필요가 없다.

## 3. 결정론과 해시

- 신규 이동/외력/히스토리는 정수와 정확 유리수만 사용한다.
- 신규 RNG 소비는 없다.
- 페이즈 체인 정의, 흡입 상한/오프셋, 체인 절 상태, `SuctionActive`를
  `DeterminismAuditHasher`에 포함했다.
- 같은 시드와 동일 입력으로 두 `BattleSim`을 180틱 진행해 누적 상태 해시가 같은 테스트를 추가했다.

## 4. 실전 틱 통합 테스트

신규 테스트는 public 데미지 훅을 사용하지 않고 실제 `BattleSim.Step` 발사체/이동/충돌을 통과한다.

- `BattleTicksSpawnTrackAndDestroyHeadOnlySegmentChain`
  - 보스 진입 → 6절 소환 → 머리 히스토리 추적 → 플레이어 실탄으로 머리 파괴 → 전체 제거
- `BattleTicksRespectPhaseChainSummonCountAndInterval`
  - 페이즈의 2기/3틱 간격 스케줄 검증
- `BattleTicksSuctionResistsInputAndPhaseGateEndsField`
  - 반대 이동 입력 100에 상한 20 외력을 합산해 실제 이동량 80 검증
  - 플레이어 실탄으로 HP 게이트 전환 → `SuctionEnded` → 다음 틱 입력 이동량 100 복구
- `SameSeedAndInputsReplayChainAndSuctionStateExactly`
  - 같은 시드 2회, 동일 180틱 입력, 전체 누적 해시 일치
- 파서 테스트
  - 8절/2기/간격/선회/히스토리/머리 피격 규칙의 정확 모델 변환
  - 흡입 세기/상한/확정 효과 오프셋의 정확 서브유닛 변환

## 5. 검증 결과

```text
cd Tools/CoreStandalone
dotnet test --no-restore --verbosity quiet
```

- CoreStandalone: **PASS 545/545**, 실패 0, 건너뜀 0

```text
dotnet run --no-restore --project Tools/DeterminismAudit -- --suite
```

- DeterminismAudit: **6/6 + cap-boundary 256/256, AUDIT PASS**
- 같은 시드 2회: 신규 180틱 체인+흡입 누적 해시 일치
- `git diff --check`: PASS
- 신규 `UnityEngine`, `System.Random`, `Guid.NewGuid()`, 벽시계, `Assert.Multiple`: 없음

