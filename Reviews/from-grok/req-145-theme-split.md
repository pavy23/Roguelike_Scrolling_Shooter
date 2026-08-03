# REQ-145 notes + CODEX request for test expectation (already applied as companion)

## 요약
테마 없는 세그먼트 8개(스폰 105)를 테마 5종 × 8 = 40개로 분할.
기존 테마 세그먼트 52 + 40 = **92**. 범용 세그먼트 0.

## 가중치
- 원본 weight W는 모든 테마에서 후보였음 (`ThemeId == null` → SupportsTheme 전부 true).
- 분할 후 각 테마 복제본이 weight W 유지 → **테마별 분할 세트 가중치 합 = 72** (원본 8개 합과 동일).
- 테마 내 상대 뽑기 확률 유지.

## 적 매핑 (배치·틱·난이도 불변, id만 교체)

| 원본 | scrapyard | hive | fortress | nebula | core |
|---|---|---|---|---|---|
| zako_straight | rust_skimmer | spore_drifter | interceptor_rush | wisp_spark | rift_blade |
| zako_sine | junk_roller | spore_drifter | interceptor_rush | wisp_spark | rift_blade |
| zako_fast | pipe_rat | sting_hornet | interceptor_rush | wisp_spark | rift_blade |
| zako_tank | zako_tank | elite_sine | zako_tank | elite_sine | guardian_sphere |
| zako_sine_slow | scrap_tumbler | brood_spitter | mortar_drone | void_moth | shard_prism |
| elite_sine | elite_sine | elite_sine | elite_sine | elite_sine | elite_sine |
| turret_ground | turret_ground | hive_tentacle | turret_ground | phase_disc | laser_sentry |
| turret_ceiling | turret_ceiling | hive_tentacle | turret_ceiling | phase_disc | laser_sentry |
| rust_skimmer/pipe_rat/junk_roller/scrap_tumbler | (유지) | lancer/spore/brood | interceptor/sentry/mortar | wisp/echo/void | rift/phase/shard |

## CODEX 요청 (동반 1줄)
`GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely` 세그먼트 수 단언 60→92.
데이터 변경에 묶인 기대값이라 content 작업과 함께 반영함. 리뷰 부탁.
