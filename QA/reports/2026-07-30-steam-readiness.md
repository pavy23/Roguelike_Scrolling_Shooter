# Steam 출시 적합성 냉정 심사 보고서 (Steam Readiness Audit Report)

**심사관**: 퍼블리싱 사업본부 수석 제품 품질 심사관 (Product Quality Inspector / 30년 경력)  
**심사 대상 프로젝트**: `Roguelike_Scrolling_Shooter` (RSS)  
**검증 실행 파일**: `Builds/Dev/RSS.exe` (667,648 bytes)  
**검증 참조 문서**: `QA/reports/2026-07-28-checklist.md`, `QA/reports/2026-07-29-balance-crosscheck-01.md`, `QA/reports/2026-07-29-performance-pass-03.md`, `QA/reports/2026-07-29-final-sweep-01.md`  
**심사 일자**: 2026년 7월 30일  
**최종 심사 판정**: **[출시 거절 / REJECT - REVISION REQUIRED] (현 상태 출시 불가)**

---

## 1. 종합 심사 총평 (Executive Verdict)

본 심사관은 30여 년간 스팀(Steam) 및 글로벌 상용 마켓플레이스에 수백여 종의 타이틀을 입점·검수해 온 퍼블리셔 품질 관리 책임자의 관점에서, 본 프로젝트 `Roguelike_Scrolling Shooter`(Builds/Dev/RSS.exe)의 스팀 출시 적합성을 냉정하게 평가하였다.

**결론부터 명시하자면, 현재 빌드는 Steam에 절대 출시할 수 없는 "개발 도중의 프로토타입/알파 상태"에 머물러 있다.**

엔진 내부의 결정론적 시뮬레이션(`Shmup.Core.Standalone`), 60fps 프레임 렌더링 유지, SpriteAtlas 164장 패킹을 통한 드로우콜 최적화(57 Batches) 등 **백엔드 코어 구조와 기본적인 기술적 기틀은 훌륭히 다져졌으나**, Commercial Commercial Commercial Game(상용 상업용 게임)으로서 게이머의 지갑을 열게 만들 프레젠테이션 품질, 오디오 완성도, UI/UX 규격, 언어 정책, Steam Platform SDK 연동, 그리고 가장 치명적인 **"환불 정책(Refund Policy) 방어 전략"이 전무한 상태**이다.

개발팀의 감정이나 내부적인 기술적 달성도에 대한 옹호는 일절 배제한다. 본 보고서는 오직 Steam 스토어에 출시되었을 때 유저 리뷰 "음성적(Negative)" 수렴과 무더기 환불 사태를 막기 위한 냉정한 결격 사유 및 필살 조치 과제만을 기술한다.

---

## 2. 프로젝트 실측 데이터 현황 (Project Asset & Spec Matrix)

| 구분 | 실측 현황 (Project Actual Status) | 상용 Steam 게임 기준 대비 평가 |
|---|---|---|
| **스프라이트 (Sprites)** | 총 253장 (GameSprites.spriteatlas 164장 패킹) | **[미달]** 적 30종, 플레이어 1종, 보스 5종 구성에 최소 분량만 겨우 채움. 연출 애니메이션 컷 부족 |
| **BGM 오디오 (Music)** | 7곡 (전량 절차적 칩튠 생성, 사람 청취 검수 미완) | **[격하/위험]** 칩튠 노이즈 피로도 미검수, 기승전결 구조 결여. 귀를 자극하는 음역대 미조율 |
| **SFX 사운드 (Effects)** | 총 10개 (타격, 파괴, 발사 등 최저한 요소만 존재) | **[부실]** 타격감/피격감/경고음/UI 음향 믹싱이 거의 비어있음. 타격 피드백 쾌감 부재 |
| **UI/UX 시스템** | UGUI 기반 픽셀 폰트 전환 완료 (`UiKit.cs`, `OptionsScreen.cs`) | **[보통]** 레이아웃 구조는 잡혔으나 언어 혼재, 마우스 제어 미흡, 정보 시각화 수준 낮음 |
| **입력 및 디바이스** | 키보드 + 게임패드 (Input System Rebinding 지원) | **[양호]** 기본 게임패드 수신 및 키 바인딩 지원. 단, 인게임 패드 버튼 아이콘 텍스트 미표시 |
| **접근성 (Accessibility)** | 화면 흔들림(Shake) / 플래시 감소(Reduce Flash) 2종 | **[부실]** 색약 모드, 글자 크기 조정, 사운드 자막, 사운드 채널별 볼륨 조절 전무 |
| **언어 (Localization)** | 한국어 및 영어 혼재, i18n 로컬라이제이션 시스템 미구현 | **[치명적 결함]** 타이틀/온보딩/옵션 간 언어 파편화. 해외 유저 진입 및 스토어 심사 불통 사유 |
| **Steam platform integration** | 도전과제(Achievements), Cloud Save, Leaderboard 미구현 | **[미완]** Steamworks SDK 미연동. 플레이 동기 부여 및 데이터 보존 장치 전무 |
| **1회 런 플레이 타임** | 1회 성공적 런당 약 5분 소요 | **[최악의 상업 리스크]** 2시간 환불 창 내 전체 콘텐츠(5스테이지) 15~20회 완파 및 환불 폭탄 |

---

## 3. 심사 관점 1: 상용 게임 대비 프레젠테이션 품질 (Presentation Quality)

### 1) 아트 일관성 및 시각적 폴리싱 (Art Consistency & Visual Polish)
- **스프라이트 분량 및 연출의 한계 (253장)**:  
  적 30종(`ruster_skimmer`, `void_moth`, `rift_blade` 등)과 보스 5종의 외형을 갖췄으나, 대부분 단일 프레임 또는 2~3프레임 렌더링에 의존하고 있다. 피격 시 Flash 연출 외에 기체가 부분 파괴되거나 비행 궤적에 따른 다이내믹 프레임 변형 애니메이션이 부재하여 시각적 생동감이 떨어진다.
- **배경 Parallax 및 도트 감성**:  
  `ParallaxBackground.cs`를 통한 다중 레이어 스크롤 및 384x224 정수배 픽셀 렌더링 체계는 픽셀 아쿠아 스타일의 도트 룩을 잘 유지한다. 그러나 스테이지별 테마 전환(Scrapyard → Hive → Fortress → Nebula → Core) 시 차별화되는 배경 파티클 특수효과나 공간감이 상용 타격 슈팅 게임(예: *ZeroRanger*, *Crimson Clover*) 대비 단순 평면 그림에 가깝다.

### 2) 오디오 품질 및 사운드 디자이닝 (Audio Quality & Variety)
- **절차 생성 칩튠 7곡의 위험성 (Human Listening Review Incomplete)**:  
  사람의 귀로 검수되지 않은 절차적 생성(Procedural Chiptune) BGM 7곡은 특정 주파수 대역(특히 2kHz~4kHz 고음역대)에서 주파수 픽이 발생하거나 반복 루프 시 플레이어에게 심각한 청각적 피로감을 유발할 위험이 매우 높다. 상용 슈팅 게임에서 BGM은 박진감과 몰입감을 결정짓는 핵심 요소인데, 현재 오디오는 "임시 개발용 사운드(Placeholder)" 수준을 벗어나지 못했다.
- **SFX 10개의 턱없는 부족**:  
  사운드 효과음이 10개에 불과하다. 적으로부터 쏟아지는 탄막 소리, 보스 부위 파괴음, 게이지 차오르는 소리, Low HP 경고음, 파워업 획득음, UI 호버/클릭음 등 상용 슈팅 게임이 기본적으로 갖춰야 할 40~50개 이상의 사운드 레이어에 비해 턱없이 부족하여, 게임을 플레이하는 내내 "소리가 텅 비어있다"는 느낌을 지울 수 없다.

### 3) UI/UX 완성도 (UI/UX Polish)
- **언어 혼재의 조잡함**:  
  타이틀 화면(`TitleScreen.cs`)에서는 `ROGUELIKE SCROLLING SHOOTER`, `PRESS SPACE / (A) TO LAUNCH` 같은 영문과 `(숫자 입력/백스페이스로 수정)` 같은 한국어가 한 화면에 섞여 있다.  
  온보딩 힌트(`OnboardingHints.cs`)는 100% 한국어 문장("적을 잡으면 캡슐이 떨어진다...")인 반면, 옵션 메뉴(`OptionsScreen.cs`)는 `RESOLUTION`, `REBIND FIRE`, `SCREEN SHAKE` 등 100% 영문으로 구성되어 있다. 이는 글로벌 마켓플레이스 상용 타이틀로서 심각한 미완성 인상을 준다.
- **마우스 제어 지원 미흡**:  
  UGUI 기반으로 전환되었으나 대부분의 메뉴 조작이 방향키와 Enter/Space, 키보드 단축키 위주로 짜여 있어 현대 PC 게이머들의 마우스 클릭 내비게이션 요구를 만족하지 못한다.

---

## 4. 심사 관점 2: Steam 출시 필수 요구사항 체크리스트 (Steam Launch Mandatory Checklist)

Steam 상점에 정식 입점하고 유저 환불 사태를 방지하기 위해 필수적으로 갖춰야 할 항목들의 점검 결과이다.

```
[ Steam Launch Readiness Checklist ]
[X] 1. Store Page Marketing Assets (Capsule Images, Header, Screenshots, Trailer) -- [NOT CREATED]
[X] 2. Minimum/Recommended System Requirements Definition                         -- [NOT DEFINED]
[!] 3. Executable Runtime Stability (Standalone Release Build Validation)         -- [PARTIAL / WARNING]
[X] 4. Clear Language Indicators & Localization Framework (i18n)                  -- [FAILED - MIXED TEXT]
[X] 5. Steamworks SDK Integration (Achievements, Cloud Save, Leaderboards)        -- [NOT IMPLEMENTED]
[X] 6. Commercial Refund Policy Risk Mitigation (Playtime & Content Loop)         -- [FATAL RISK - 5 MIN RUN]
```

### 1) 스토어 에셋 및 마케팅 자산 (Store Page Assets)
- Steam Direct 등록을 위한 메인 캡슐(Main Capsule: 616x353), 헤더 캡슐(460x215), 히어로 캡슐(374x448), 스몰 캡슐(231x87), 라이브러리 에셋 및 최소 5장 이상의 HD 스크린샷과 트레일러 영상이 전혀 준비되어 있지 않다.

### 2) 시스템 요구사항 및 실행 안정성 (System Requirements & Stability)
- **최소/권장 사 명시 부재**: 스팀 상점 페이지 명시용 OS, CPU, RAM, GPU, DirectX 요구사항 타겟팅이 정의되지 않았다.
- **실행 파일 검증 한계 (`Builds/Dev/RSS.exe`)**:  
  `QA/reports/2026-07-28-checklist.md` 리포트에 따르면, 이전 AI QA 평가 시 CLI 실행(Exit Code 0)만 확인하였을 뿐, 헤드리스 환경 제약으로 인해 화면 렌더링, 프레임 드랍, 실제 키보드/패드 입력 반응에 대한 실측 검증이 수행되지 못했다. 에디터 플레이 모드(`Performance Pass #03`)에서 Mono Heap 1,344MB 동결 및 57 Batches 최적화가 확인되었으나, 독립 릴리즈 스탠드얼론 빌드에서의 프레임 유지력 및 무결성 검증이 추가 필요하다.

### 3) 언어 표기 및 로컬라이제이션 리스크 (Language & L10n Risk)
- Steam 상점 스토어 페이지에서 "한국어 지원: 예 / 영어 지원: 예"로 표기할 경우, 게임 진입 직후 텍스트 언어가 혼재되어 있고 언어 변경 옵션(Language Selector)이 존재하지 않아 **유저들의 "지원 언어 허위 표기" 신고 및 부정적 평가(Negative Review)의 직격탄**을 맞게 된다.

### 4) 치명적 상업 리스크: Steam 환불 정책 (Steam Refund Policy Risk)
- **5분 런과 2시간 환불 창의 충돌**:  
  Steam 환불 정책은 **"플레이 시간 2시간 이내, 구매 후 14일 이내 사유 불문 100% 환불"**을 보장한다.
- 현재 RSS 프로젝트는 1회 런 플레이 타임이 **약 5분**에 불과하다. 플레이어가 게임을 시작하여 1.5시간(90분) 동안 플레이할 경우, 무려 **18회의 런을 진행**하게 되며 스테이지 1~5, 적 30종, 보스 5종, 미사일/옵션/메인샷 풀업 엔딩까지 **게임의 모든 콘텐츠를 100% 완파하고 질리게 된다.**
- 플레이어는 모든 콘텐츠를 소진한 후, 스팀 환불 가능 시간인 2시간이 채 되기 전에 "환불 요청"을 눌러 100% 돈을 돌려받을 수 있다. **현재 상태로 출시하는 것은 개발진이 공들여 만든 게임을 무료로 풀고 환불 폭탄을 맞겠다는 자살 행위와 같다.**

---

## 5. 심사 관점 3: 접근성과 사용성 평가 (Accessibility & Usability)

### 1) 색약 대응 및 시각적 직관성 (Colorblindness Support) - **[미구현]**
- 빨간색/초록색 적 탄막과 아군 탄막, 경고 테두리, HUD 게이지 핍의 색상이 고정 색상으로 렌더링된다. 적성/적변색약 유저를 위한 탄막 쉐이더 아웃라인, 무늬/패턴 덧씌우기 옵션이 전무하여 시각 장애 유저 접근성이 떨어진다.

### 2) 키 안내 및 게임패드 연동 (Key Guides & Rebinding) - **[부문적 구현 / 개선 필요]**
- `OptionsScreen.cs`를 통해 키보드 및 패드 리바인딩 기능이 작성되어 있으며 PlayerPrefs 저장 오버라이딩을 지원하는 점은 우수하다.
- 그러나 실제 인게임 HUD나 온보딩 힌트에서는 `(A)`, `(X)`, `(Y)` 등의 단순 텍스트 표기만 제공되며, 연결된 컨트롤러 종류(Xbox, DualSense, Switch Pro)에 맞는 전문 UI 버튼 글리프(Icon Sprite) 렌더링 체계가 없다.

### 3) 튜토리얼 및 온보딩 (Tutorial & Onboarding) - **[부실]**
- `OnboardingHints.cs`에서 런 시작 후 하단 텍스트 3줄이 6초 간격으로 지나가는 방식이 전부다.
- 캡슐을 먹었을 때 게이지 커서가 어떻게 움직이는지, X/(Y) 버튼을 어느 타이밍에 눌러 파워업을 확정해야 하는지에 대한 **상호작용형 튜토리얼(Interactive Tutorial Stage)**이 없어, 슈팅 입문자는 첫 런에서 파워업 메커니즘을 이해하지 못하고 사망할 가능성이 매우 높다.

### 4) 옵션 완성도 (Options Completeness) - **[심각한 사운드 옵션 누락]**
- `OptionsScreen.cs` 코드 검수 결과:
  - 지원 옵션: 해상도(4종), 전체화면, 키 리바인딩, 화면 흔들림(Shake), 플래시 감소(Reduce Flash)
  - **결여된 핵심 옵션**: **마스터 볼륨, BGM 볼륨, SFX 볼륨 조절 슬라이더가 단 하나도 존재하지 않는다!**
  - 사운드 크기를 게임 내에서 조절할 수 없는 PC 게임은 2026년 상용 게임 기준에서 심각한 사격 감점 대상이다.

---

## 6. 심사 관점 4: 첫 15분 경험 평가 (First 15-Minute Player Experience)

신규 유저가 스팀에서 게임을 구매하고 결제한 직후 첫 15분간 겪게 될 솔직한 심리 상태 추적이다.

### [0 ~ 5분: 첫 번째 런 (First Impressions & Confusion)]
- **느낌**: 조작감은 경쾌하고 프레임(60fps)은 부드러우나, 시작하자마자 하단에 뜨는 한국어 온보딩 텍스트와 영문 UI의 불일치로 어색함을 느낌.
- **문제점**: 캡슐을 먹어도 게이지 커서가 이동하는 원리를 직관적으로 깨닫기 전 적 탄막에 피격됨. BGM 칩튠의 고음역 멜로디가 피로하게 느껴지며, 적 파괴 시 sound 효과음(SFX)이 빈약하여 "내가 타격하고 있는지" 피드백이 약함. 5분 만에 스테이지 2~3에서 첫 사망.

### [5 ~ 10분: 두 번째 런 & 밸런스 붕괴 체감 (Power Spike & Trivialization)]
- **느낌**: 캡슐 메커니즘을 이해하고 게이지를 활성화하여 MainShot + Option + Missile 파워업을 중첩하기 시작함.
- **문제점**: `QA/reports/2026-07-29-balance-crosscheck-01.md`에서 증명되었듯, 무기 풀업 시 DPS가 기본 75.0에서 **1,880.0(25.1배)**으로 폭증함. 
  이로 인해 난이도가 급격히 무너지며, 화면 내의 적과 보스(`boss_stage1`, `boss_fortress`)가 등장하자마자 1~2초 만에 녹아내림. 슈팅 게임 특유의 탄막 피하기 긴장감과 탄막 회피의 재미가 순식간에 휘발됨.

### [10 ~ 15분: 세 번째 런 & 콘텐츠 고갈 / 환불 고민 (Boredom & Refund Decision)]
- **느낌**: 3번째 런만에 최종 스테이지 5 보스(`boss_core`)까지 도달하거나 클리어함.
- **문제점**: 15분 만에 게임의 핵심 루트와 보스 패턴을 전부 파악함. 도전과제도 없고, 온라인 리더보드 경쟁 요소도 없으며, 메타 프로그래밍 해금 요소도 얇아 더 이상 플레이할 동기를 잃음.  
  유저는 생각한다: **"15분 만에 끝났네? 재밌긴 한데 분량이 이게 다야? 2시간 지나기 전에 빨리 스팀 환불 신청해야겠다."**

---

## 7. 출시 필수(P0 Mandatory) vs 선택(P1 Optional) 조치 과제

Steam 출시 승인(Greenlight / Sign-Off)을 얻기 위해 개발팀이 이행해야 할 과제를 우선순위별로 분리한다.

```
+-----------------------------------------------------------------------------------+
|                        STEAM RELEASE ACTION ITEM MATRIX                           |
+-----------------------------------------------------------------------------------+
|  [P0: MANDATORY BEFORE LAUNCH] - 이 과제들이 완료되지 않으면 입점 거부              |
|  1. Content Loop & Replayability Expansion (런 길이 10~15분 확장, 메타 해금 강화)   |
|  2. Audio Overhaul (BGM 사람 청취 검수/튜닝, SFX 10개 -> 40개+ 확충)                |
|  3. Options Screen Audio Controls (Master/BGM/SFX Volume Sliders 추가)             |
|  4. Full Localization System (한국어/영어 분리 및 i18n 시스템 구축, 언어 혼재 제거) |
|  5. Steamworks Integration (Steam Achievements 최소 20종, Cloud Save 연동)          |
|  6. Balance Curve Normalization (무기 풀업 DPS 폭증 억제 및 보스 TTK 정상화)       |
|  7. Steam Store Page Marketing Assets (캡슐 이미지, 스크린샷, 트레일러 제작)       |
+-----------------------------------------------------------------------------------+
|  [P1: OPTIONAL / POST-LAUNCH] - 출시 후 업데이트 가능 항목                           |
|  1. Colorblind Mode & High-Contrast Bullet Shaders (색약 지원 쉐이더)             |
|  2. Dynamic Controller Glyph Display (Xbox/PS/Switch 버튼 아이콘 UI)              |
|  3. Online Global Leaderboards (스팀 글로벌 점수 리더보드)                         |
|  4. Interactive Tutorial Stage (대화형 튜토리얼 씬)                               |
|  5. Additional Player Ships & Branching Secret Routes (추가 기체 및 비경 루트)     |
+-----------------------------------------------------------------------------------+
```

### P0: 필수 이행 과제 (Must-Fix Before Launch)
1. **환불 방지형 콘텐츠 루프 설계 (Content Loop & Run Time Expansion)**:
   - 1회 런 타임을 최소 10~12분으로 확장하거나, 클리어 후 해금되는 회차 요소(New Game+, 엔드리스 모드, 난이도 오버라이드, 시드 일간 도전에 따른 보상)를 강화하여 최소 **총 플레이 타임 5~10시간 이상**을 확보할 것.
2. **사운드 오버홀 (Audio Overhaul & SFX 40+ Expansion)**:
   - 절차 생성 BGM 7곡에 대한 음향 전문 믹싱/마스터링 및 인체 청취 피로도 검수 완료.
   - SFX 사운드 레이어를 10개에서 최소 40개 이상으로 확충하여 타격/피격/파괴/UI/경고 피드백 완성.
3. **사운드 볼륨 옵션 구현 (`OptionsScreen.cs` 수정)**:
   - Master / Music / SFX Volume 조절 슬라이더 및 오디오 믹서(AudioMixer) 연동 필수 구현.
4. **완벽한 로컬라이제이션 구축 (Localization & Text Clean-up)**:
   - 텍스트 언어 혼재를 100% 제거하고, Smart Localization 또는 JSON 기반 i18n 시스템을 도입하여 영어/한국어 완벽 전환 옵션 제공.
5. **Steamworks SDK 연동**:
   - `Steamworks.NET` 연동을 통한 **도전과제(Achievements 20종 이상)** 및 **클라우드 세이브(Cloud Save)** 필수 탑재.
6. **DPS 밸런스 및 보스 TTK 재정립**:
   - `QA/reports/2026-07-29-balance-crosscheck-01.md` 피드백을 반영하여 Option 중첩 데미지 감쇄 비율 적용 및 미사일 쿨다운 버그 수정, 보스 TTK를 15~30초 대의 긴장감 넘치는 전투로 재설계.
7. **Steam 상점 마케팅 에셋 제작**:
   - 스팀 스토어용 캡슐 아트 5종, 스크린샷 5장 이상, 트레일러 영상 제작.

### P1: 선택/후속 개선 과제 (Optional / Post-Launch)
1. **색약 모드 및 탄막 가시성 토글 추가**: 색약 유저용 탄막 아웃라인 쉐이더 적용.
2. **게임패드 전문 버튼 글리프 연동**: XInput/DirectInput에 따른 버튼 아이콘 스프라이트 교체.
3. **대화형 튜토리얼 씬 구현**: 조작 및 게이지 메커니즘을 튜토리얼 존에서 직접 실습.
4. **추가 기체 2종 및 숨겨진 보스 루트 확장**: 메타 프로그래밍 재화를 통한 기체 해금 요소 추가.

---

## 8. 실측 및 QA 데이터 기반 근거 종합 (Empirical Evidence Matrix)

본 심사 보고서의 모든 지적 사항은 프로젝트 내 실측 코드 및 이전 QA 리포트 데이터에 직접적으로 근거한다.

1. **`OptionsScreen.cs` (L16-33, L209-241)**:
   - 해상도, 전체화면, 키 바인딩, 쉐이크, 플래시 조절만 존재하며 **볼륨 조절 코드가 0줄임 확인**.
2. **`TitleScreen.cs` (L47-57, L180)** & **`OnboardingHints.cs` (L20-25)**:
   - `ROGUELIKE SCROLLING SHOOTER`, `PRESS SPACE...` 영문 타이틀과 `(숫자 입력/백스페이스로 수정)`, `적을 잡으면 캡슐이 떨어진다...` 한국어 문장이 하드코딩으로 혼재됨 확인.
3. **`QA/reports/2026-07-29-balance-crosscheck-01.md` (L93-121)**:
   - 기본 DPS 75.0 대비 풀업 DPS 1,880.0(25.1배 폭증)으로 인해 Stage 5 보스 TTK가 1.27초로 붕괴되고 미사일 30t 쿨다운 캡핑 버그 증명.
4. **`QA/reports/2026-07-28-checklist.md` (L6-33)**:
   - 헤드리스 환경으로 인해 실행 파일(`Builds/Dev/RSS.exe`)의 실제 화면 출력, 픽셀 퍼펙트, HUD 관찰, 프레임 체감 검증이 미실행("Not Tested") 상태였음 증명.
5. **`QA/reports/2026-07-29-performance-pass-03.md` (L75-108)**:
   - SpriteAtlas 164장 패킹으로 57 Batches 최적화 및 Mono Heap 1,344MB 동결 등 백엔드 엔진 성능은 합격점이나, 프레젠테이션 레이어(오디오/UI/콘텐츠)가 미달됨을 증명.

---

## 9. 최종 심사 의견 (Final Auditor Summary)

프로젝트 RSS는 **"엔진과 백엔드 뼈대는 훌륭하나, 살(오디오/UI/콘텐츠/마케팅)이 전혀 붙지 않은 뼈대뿐인 거신"**이다.

이 상태로 Steam에 출시를 강행한다면, 환불율 70% 이상, 유저 평가 "복합적(Mixed)" 또는 "대체로 부정적(Mostly Negative)" 판정을 받아 개발팀과 퍼블리셔 모두에게 심각한 브랜드 타격을 입힐 것이 명약관관하다.

개발팀은 즉시 P0 필수 과제(오디오 확충, 사운드 옵션 구현, 언어 혼재 해결 및 L10n, 환불 방지형 콘텐츠 루프 확장, Steamworks 연동) 작업에 착수하라. 본 심사관은 해당 과제들이 100% 이행되고 재검수가 완료될 때까지 **Steam 출시 승인(Sign-Off)을 전면 거부(REJECT)**한다.

---
