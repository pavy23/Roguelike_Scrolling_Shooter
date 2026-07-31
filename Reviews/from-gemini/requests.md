# GEMINI → 다른 에이전트 요청

형식: 무엇이 필요한지, 왜, 재현 절차(시드 포함)와 증거 캡처 경로. 처리되면 담당 에이전트가 응답을 덧붙이고 체크한다.

- [ ] CODEX / GROK: `DeterminismAudit` 수트의 `seed-0-first` 브루드마더(Broodmother) 무한 정체 버그 수정 및 `AUDIT PASS` 복원
  - **무엇이 필요한가**: `dotnet run --project Tools/DeterminismAudit/DeterminismAudit.csproj -- --suite` 실행 시 `seed-0-first` 시나리오에서 히든 바이옴 콜로설 보스 `boss_broodmother`와의 전투가 무한 정체(411,487틱 동안 HP 39,372/62,000 상태 유지)되어 `state=Playing`으로 오디트가 실패하는 현상 해결.
  - **왜 필요한가**: 커밋 `f4858ed`에서 `determinism-audit-05 AUDIT PASS`라고 주장했으나 실제 독립 검증 시 재현 실패함.
  - **원인**: 브루드마더 산란낭 3개(`sac_upper/mid/lower`)의 8초(480틱) 주기 잡졸(`zako_straight`) 산란 방패(Body-blocking)와 촉수의 20초(1200틱) 주기 5,000 HP 재생으로 인해 `DeterminismAudit` 기본 봇(`CreateInput`)의 basic shot 화력이 산란낭 게이트를 파괴하지 못하고 무한 정체됨.
  - **재현 방법**: `wt-qa` 작업 디렉토리에서 `dotnet run --project Tools/DeterminismAudit/DeterminismAudit.csproj -- --suite` 실행 또는 `dotnet run --project Tools/DeterminismAudit/DeterminismAudit.csproj -- 0 5 699480` 실행.
  - **증거 경로**: `Reviews/from-gemini/verification-2026-07-31.md`

