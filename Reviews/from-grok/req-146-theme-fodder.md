# REQ-146 — 테마별 고유 졸개 최소 5종

## 요약
테마 성격 졸개가 4종이던 **scrapyard·nebula**에 고유 졸개 1종씩 추가하고 세그먼트에 배치.
hive / fortress / core는 기존 5종 유지(공유 포탑·범용 zako/elite는 고유 종에서 제외).

## 테마별 고유 졸개 (최종)

| theme | 고유 졸개 (5+) | 비고 |
|---|---|---|
| scrapyard | rust_skimmer, pipe_rat, scrap_tumbler, junk_roller, **magnet_claw** | +1 |
| hive | brood_spitter, hive_tentacle, spore_drifter, lancer_dart, sting_hornet | 유지 |
| fortress | interceptor_rush, sentry_drone, mortar_drone, laser_sentry (+ turret_* 성격) | 유지 |
| nebula | void_moth, echo_wisp, wisp_spark, phase_disc, **mist_specter** | +1 |
| core | rift_blade, phase_disc, shard_prism, guardian_sphere, prism_beamer | 유지 |

공유 제외: `turret_ground`/`turret_ceiling`, `zako_*`, `elite_sine`.

## 신규 적 스펙

### `magnet_claw` (scrapyard)
| 항목 | 값 | 근거 |
|---|---|---|
| HP | 72 | scrap_tumbler(80) 대역 중형 |
| contact | 1 | 테마 졸개 공통 |
| fireIntervalTicks | 140 | tumbler(150)보다 살짝 빠른 조준 |
| dropWeight | 5 | tumbler/echo 계열 |
| halfW/H | 1.171875 / 0.9375 | 300/256 · 240/256 |
| movement | **sine** speed 3.25 amp 2.75 period 110 | scrap 고유는 dash/zigzag뿐 → sine으로 차별 |
| score | 210 | tumbler 220 근처 |

역할: 자석 집게가 사인 궤도로 떠 다니며 간헐 사격. 스킴/파이프 대시·텀블러 지그재그와 겹치지 않음.

### `mist_specter` (nebula)
| 항목 | 값 | 근거 |
|---|---|---|
| HP | 11 | wisp_spark(8)~저중형 사이 저HP 졸개 |
| contact | 1 | |
| fireIntervalTicks | 0 | 접촉/돌진형 순수 졸개 |
| dropWeight | 3 | pipe_rat/sting 대역 |
| halfW/H | 1.40625 / 1.171875 | 360/256 · 300/256 |
| movement | **dive** speed 7.25 delay 16 duration 26 | nebula 고유는 sine/zigzag/static → dive로 차별 |
| score | 95 | sting_hornet 90 근처 |

역할: 안개 망령이 잠깐 머문 뒤 화면을 가로질러 돌진. 위습 부유·에코/모스 지그재그와 다른 압박.

## 세그먼트 배치 (기존 스폰 id 치환 — 틱·Y·난이도 유지)

### magnet_claw — 22 스폰 / 9 세그먼트
| segment | 치환 수 | 원본 역할 |
|---|---|---|
| seg_sine_pair_scrapyard | 4 | junk_roller |
| seg_scrap_skimmer_weave | 3 | junk_roller |
| seg_sine_rush_scrapyard | 4 | scrap_tumbler + junk_roller |
| seg_mixed_mid_scrapyard | 2 | scrap_tumbler + junk_roller |
| seg_scrap_junk_corridor | 2 | scrap_tumbler |
| seg_scrap_tumbler_pack | 2 | scrap_tumbler |
| seg_scrap_center_breach | 1 | scrap_tumbler |
| seg_scrap_shard_field | 2 | junk_roller + scrap_tumbler |
| seg_scrap_clean_kill_junk | 2 | junk_roller |

### mist_specter — 24 스폰 / 9 세그먼트
| segment | 치환 수 | 원본 역할 |
|---|---|---|
| seg_intro_line_nebula | 3 | wisp_spark |
| seg_swarm_fast_nebula | 6 | wisp_spark |
| seg_mixed_mid_nebula | 3 | wisp_spark |
| seg_sine_rush_nebula | 3 | wisp_spark + echo_wisp |
| seg_nebula_void_moth_swarm | 2 | wisp_spark |
| seg_nebula_echo_ribbon | 2 | wisp_spark |
| seg_nebula_wisp_storm | 1 | wisp_spark |
| seg_sine_pair_nebula | 2 | wisp_spark |
| seg_turret_floor_nebula | 2 | wisp_spark |

## 아트 요청 (사람 — 스프라이트 미제작 시 기존 폴백)

### `magnet_claw`
- **실루엣**: 앞쪽에 벌린 집게(C자/집게발 2갈래), 뒤쪽에 원형 자석 코일 또는 전자석 디스크. 전체는 가로로 긴 기계 잡몹.
- **색**: 녹슨 주황-갈(scrapyard) + 자석부 청회색/암자. 하이라이트에 녹 얼룩.
- **크기**: half≈1.17×0.94 → 스프라이트 대략 **38×30 px** 전후 (PPU 16 기준 월드 half 대응). scrap_tumbler보다 약간 납작하고 집게가 앞으로 돌출.
- **포즈**: 집게가 열린 채 떠 있는 idle 1프레임 우선. 가능하면 집게 개폐 2프레임.

### `mist_specter`
- **실루엣**: 흐릿한 망토/안개 덩어리 중앙에 희미한 눈 또는 균열. 하방은 흩어지는 연기 꼬리. 외곽 알파 페더.
- **색**: 청보라 성운(nebula) 반투명 + 가장자리 흰 빛. wisp_spark보다 크고 덜 선명.
- **크기**: half≈1.41×1.17 → 대략 **45×38 px** 전후. 돌진 시 흐려 보이도록 가로로 약간 늘린 형태 가능.
- **포즈**: 부유 idle 1프레임. 돌진감은 이동 패턴(dive)이 담당.

## 검증
- `cd Tools\CoreStandalone && dotnet test` → **571/571 PASS**
- 격자: half/speed/amplitude 전부 1/256 exact
- JSON: UTF-8 no BOM, indent 2

## 동반 테스트 (소유 경계 예외 — REQ-145와 동일 사유)
`GameDataParserTests.RepositoryApprovedV2Files_ParseCompletely` 적 카탈로그 개수 단언에 `+2`(magnet_claw, mist_specter) 및 `FindEnemy` 존재 확인 추가.
데이터 변경에 묶인 기대값이라 content 커밋에 포함. 세그먼트 개수 불변식 개편(사람 지시)과 같은 이유로, 이후 졸개 추가 시 이 수식 단언은 다시 깨질 수 있음 — 세그먼트처럼 불변식으로 바꾸는 것을 권장.
