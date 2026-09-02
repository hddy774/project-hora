# PC 0.0.27 성능 기반 리팩터링 수직 계약

## 사용자 계약

- 지원 배속은 1x, 10x, 25x, 50x, 100x이며 최대 100x를 유지한다.
- 100x에서 시뮬레이션이 원활하게 진행되고 UI 관찰이 권위 worker를 막지 않아야 한다.
- 전체 코드는 owner·생명주기·검색 작업집합으로 검토하고, 너무 작은 파일이나 범용
  helper를 남발하지 않는다.
- 최적화는 게임 규칙, RNG 순서, 월 경계, 표시·실전 전투 공식, 저장 wire를 바꾸지 않는다.

## 구현 계약

- `World.Scheduling.cs`: 안정 AgentId 순서의 due 수집과 월 경계를 조정한다.
- `RankingRevisionCache.cs`·`World.Rankings.cs`: 범주별 dirty revision과 할당 없는
  adaptive radix 정렬을 소유한다.
- `World.CultivationParallel.cs`: 순수 수행 입력 계산만 병렬 실행하고 fingerprint 검증 뒤
  직렬 commit한다.
- `AgentDecisionSchedule.cs`: 인물당 일정 하나를 보유하는 고정 용량 4진 indexed heap을
  유지한다.
- `CompactChronicleStore.cs`·`AgentHistoryStores.cs`: cold 연대기와 최근 100행 로그를
  분리한다.
- `SimulationRunner.cs`: lock-free shell, busy 관찰 frame 생략, 100x 3-pulse 관찰
  무효화를 유지한다.

## 완료 판정

- 전체 75/75, architecture/XAML, 238행 port ledger가 통과해야 한다.
- 10,000명·20,000 tick 중앙값은 900ms 이하여야 한다.
- 100x 60초 SOAK의 p95/p99/정상률/진행률/backlog 예산을 모두 통과해야 한다.
- 성능이 나빠진 실험은 코드에 남기지 않고 감사 문서에 폐기 근거만 남긴다.

세부 구조·연구·A/B 판정은 `docs/architecture/PC_PERFORMANCE_FOUNDATION_0.0.27.md`,
실행 증거는 `docs/evidence/0.0.27/verification.md`를 정본으로 한다.

