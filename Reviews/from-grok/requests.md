# GROK → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 제안 시그니처. 처리되면 담당 에이전트가 응답을 덧붙이고 체크한다.

(아직 요청 없음)

---

## 콘텐츠 확장 기록 (2026-07-28) — 스테이지 썰렁 피드백

플레이 피드백: 스테이지가 썰렁하다. `enemies.json` / `waves.json` 카탈로그 확장. **스키마 형식 변경 없음.** 아래 수치는 전부 **잠정값**이며 손맛·밸런스 최종 확정은 사람 결정 (AGENTS.md §7).

### enemies.json — 추가 5종 + dropWeight 정비

| id | movePattern | hp | moveSpeed | dropWeight | 의도 |
|---|---|---|---|---|---|
| `zako_straight` (기존) | straight | 10 | 3.0 | **4** (was 3) | 기본 잡졸. 드롭 체감 소폭 상향. |
| `zako_sine` (기존) | sine | 10 | 2.5 | **5** (was 3) | 사인 잡졸. 회피 부담 대비 드롭 우대. |
| `turret_ground` (기존) | static | 30 | 0 | **2** (was 1) | 지상 포탑. 저드롭 유지하되 0에 가깝지 않게. |
| `zako_fast` **NEW** | straight | 6 | 5.0 | 3 | 고속 저체력 스웜. 밀도 담당, 개체당 드롭은 낮음. |
| `zako_tank` **NEW** | straight | 40 | 1.5 | 7 | 저속 고기동 탱커. 킬 보상형 드롭. |
| `zako_sine_slow` **NEW** | sine | 18 | 1.8 | 6 | 느린 사인. 화면 점유·압박. |
| `turret_ceiling` **NEW** | static | 30 | 0 | 2 | 천장 포탑. ground 대칭. |
| `elite_sine` **NEW** | sine | 50 | 2.0 | 12 | 엘리트. 고 dropWeight로 캡슐 하이라이트. fireInterval 120 잠정. |

**dropWeight 설계 메모 (잠정):** 상대 가중치만 의미 있음. 잡졸 4–5 / 스웜 3 / 포탑 2 / 탱커·슬로사인 6–7 / 엘리트 12. 절대 드롭 확률 공식은 Core 드롭 구현에 따름 — 체감 과다/과소 시 스케일 일괄 조정 권장.

`contactDamage` / `scoreValue` / `fireIntervalTicks` 도 잠정. 엘리트·탱커 contactDamage=2는 위험 신호용 플레이스홀더.

### waves.json — 세그먼트 3 → 8종, 밀도 상향

`laneCount=3`, `segmentsPerStage=3`, `startLaneMask=2`, 보스 메타 유지. **모든 세그먼트 `entryLaneMask=7`, `exitLaneMask=7`** → difficulty 1–5에서 `segmentsPerStage=3` 조립·보스 진입 가능 (기존 클리어 가능성 전략 유지).

| 세그먼트 | diff | lengthTicks | traversable | 밀도 성격 | 의도 |
|---|---|---|---|---|---|
| `seg_intro_line` | 1–3 | 600 | `[7]` | 중 (10 spawns) | 입문 직선. y 분산으로 전 레인 사용감. |
| `seg_sine_pair` | 1–5 | 600 | `[2]` | 중–고 (10) | 상하 사인 + slow. center 코리도. |
| `seg_turret_floor` | 2–5 | 900 | `[6]` | 중 (11) | 바닥 포탑 + 상부 잡졸/탱커. top\|center. |
| `seg_swarm_fast` **NEW** | 2–5 | 600 | `[7]` | **고** (18) | 고속 스웜 폭주. 전 레인 개방. |
| `seg_mixed_mid` **NEW** | 2–5 | 720 | `[7]` | 중–고 (14) | straight/sine/fast/tank 혼합 샘플. |
| `seg_turret_ceiling` **NEW** | 2–5 | 900 | `[3]` | 중 (11) | 천장 포탑. bottom\|center. floor 대칭. |
| `seg_sandwich` **NEW** | 3–5 | 840 | `[2]` | 고 (17) | 상하 포탑 + 중앙 압박 + elite 피날레. |
| `seg_sine_rush` **NEW** | 1–4 | 660 | `[6]` | 중–고 (14) | 사인 연속. floor 회피 메타(bottom 제외). |

**difficulty 1 풀:** intro / sine_pair / sine_rush 만 → 3세그먼트 조립 가능.  
**difficulty 2:** sandwich 제외 대부분.  
**difficulty 3–5:** sandwich 포함 풀 카탈로그.

### 잠정값 일람 (확정 금지 — 사람 지시 전 유지)

- 신규 적 HP / speed / dropWeight / contactDamage / score / fireInterval
- 기존 적 dropWeight 변경 (3→4, 3→5, 1→2)
- 전 세그먼트 spawn tick·y·lengthTicks·밀도
- 보스 `hp: 500` 미변경 (기존 플레이스홀더)

### 후속 관찰 포인트 (밸런스 시뮬 / 플레이)

1. 스웜 세그먼트에서 드롭이 과다해지면 `zako_fast.dropWeight` 또는 스폰 수를 먼저 깎을 것.
2. sandwich + elite가 difficulty 3+에서 과도하면 `difficultyMin` 4로 올리거나 elite HP 하향.
3. `segmentsPerStage`는 3 유지 — 카탈로그 다양성으로 반복 체감만 완화. 스테이지 절대 길이가 짧으면 상수 상향은 별도 결정.
4. Core/Presentation이 `movePattern` 문자열을 아직 전부 소비하지 않을 수 있음 — 데이터는 스키마 그대로 준비. 미구현 패턴 시 CLAUDE/CODEX 연동 필요.

### 다른 에이전트 요청

(현재 없음 — 스키마 확장 없음. 신규 enemyId는 기존 `enemyId` 문자열 참조 방식과 동일.)
