# PC 0.0.26 100배속 runtime·관찰 경합 수직 계약

## 사용자 결함과 원인

- 10,000명 세계의 100배속 설정 화면에서 pulse·query·backlog가 함께 과부하로
  표시됐다.
- 관찰 stopwatch가 권위 world lock 대기 전부터 시작되어 실제 DTO query가 아닌 worker
  대기까지 query 시간으로 기록했다.
- 설정·저장 화면도 공통 관찰 갱신 경로를 지나 상세 세계 snapshot을 요청했고, 성능
  카드가 100ms UI timer마다 같은 1초 표본을 다시 바인딩했다.
- 시뮬레이션에서는 월 index·수명 나눗셈, 반복 개인 로그 재투영, 수행 관계 전체 상태
  투영과 8-pass ranking 정렬이 hot path에 남아 있었다.

## runtime 계약

- `RuntimeShellSnapshot`은 tick·인구·전투·도전·돌파 누계만 가진 불변 참조형이며 worker
  pulse가 끝난 안전 경계에서 `Volatile.Write`로 게시한다.
- 셸만 필요한 화면은 world lock이나 observation query를 사용하지 않는다.
- 자동 UI 갱신은 `TryGetObservation`으로 권위 lock을 즉시 얻을 수 있을 때만 DTO를 만든다.
  실패하면 이전 불변 DTO를 유지하며 RNG·권위 상태·revision을 변경하지 않는다.
- 저장, 명시적 사용자 선택처럼 반드시 최신 권위 상태가 필요한 작업은 기존 blocking
  경로를 계속 사용한다.
- observation 계측은 lock 획득 후 실제 query·DTO materialization 구간만 기록한다.
- 100배속은 3 pulse마다 관찰 무효화를 알린다. 50ms pulse 기준 1.5 게임년 간격이므로
  월 위상을 고정하지 않는다.

## simulation hot path 계약

- 같은 due tick의 `actionMonth`는 한 번 계산하고 모든 수행 전이에 전달한다.
- 나이는 기존 달력 의미를 바꾸지 않고 `birthTick + lifespanTicks <= currentTick` 정수
  비교로 판정한다.
- 반복 수행 로그는 `PersonalLogRingStore.TryExtendLatestAction`에서 최신 compact 행만
  갱신한다. 최근 100행 상한과 표시 DTO 순서는 유지한다.
- `SparseRelationStore`의 스승·제자 개수 index는 관계 역할에서 파생된다. checkpoint에는
  중복 저장하지 않고 restore 때 관계 배열에서 재구성한다.
- 수행 관계 탐색은 작은 `RelationRoleAdjacencyState`를 사용한다. 가르침 bonus와
  동거·생존 조건, AgentId 순서는 변경하지 않는다.
- ranking radix pass 변경은 부호 변환된 64-bit key와 AgentId tie-break 결과가 기존
  비교 정렬과 완전히 같아야 한다.

## 의미 보존 검증

- 5 seed 직렬·2단계 병렬 수행 결과와 월·100년·1,000년 checkpoint hash가 일치한다.
- `AdvanceTo` 분할, 관찰 유무, busy-frame 생략은 동일 상태 hash와 RNG 순서를 만든다.
- ranking revision이 바뀌지 않은 idle pulse에서는 정렬 0회를 유지한다.
- 닫힌 설정·저장·비인물 화면은 상세 인물 projection 0회여야 한다.

## 성능 완료 기준

- 60초 100배속 warm-up 이후 pulse p95 80ms 이하, p99 100ms 이하, 정상 표본 95%
  이상, 목표 tick 95% 이상, 최종 backlog 1초 이하.
- 활성 요약 query p95 16ms 이하, 1,000행 materialization p95 50ms 이하.
- 10,000명·20,000 tick은 독립 Release 프로세스 5회 중앙값을 기록한다.
- 저장 성능은 별도 계약이다. SQLite 1초 권고 미달을 runtime 성공으로 숨기지 않는다.
