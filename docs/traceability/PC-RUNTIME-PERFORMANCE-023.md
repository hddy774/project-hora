# PC 0.0.23 저장·메모리·병렬 수행 수직 계약

## 범위와 비변경 계약

- 장기 세계가 기존 512MiB JSON 상한을 넘어도 기존 슬롯을 훼손하지 않고 저장·복원한다.
- 테스트 도구의 잘못된 필터와 저장 실패를 게임 권위 예외와 분리한다.
- 최근 개인 로그와 장기 기록의 표시·시간순 조회·schema 31 checkpoint 의미를 유지하며
  보관 형태만 압축한다.
- 수행 수치 계산 중 공유 상태를 읽거나 RNG를 소비하지 않는 구간만 병렬화한다.
- 외부 `WorldSchema 25`, 내부 checkpoint schema 31, 상태 hash 의미와 기존 저장 wire는
  변경하지 않는다. `SlotSaveResult.JsonBytes`는 관찰용 계측값이며 저장 정본이 아니다.

## 대용량 저장과 오류 경계

- `WorldSaveCodec`은 압축 전 JSON 최대 1.5GiB와 압축 container 최대 256MiB를 서로
  구분한다. 초과·메모리·파일 시스템·권위 검증·취소·예상 밖 실패를 typed 단계와 실제
  byte 수로 반환한다.
- encode 경로는 `Utf8JsonWriter → BrotliStream → scratch container`로 이어지고,
  `SlotSaveStore`는 SQLite zeroblob을 먼저 만든 뒤 incremental BLOB handle에 256KiB씩
  쓴다. 같은 payload의 대형 `byte[]`를 중복 보유하지 않는다.
- 후보 검증은 SQLite BLOB 길이·SHA-256을 스트리밍 확인한다. load는
  primary/temp/backup metadata를 먼저 정렬하고 선택 후보 하나만 scratch 파일로 해제해
  Candidate World를 복원한다.
- scratch 파일과 새 임시 DB만 작업 소유 자원이다. 모든 종료 경로에서 이를 정리하며
  기존 primary/backup과 구 `%LocalAppData%/.../world.cmip`은 수정·이동·삭제하지 않는다.
- Application은 안전 경계 checkpoint를 작업 Task에만 넘기고 완료 뒤 참조를 해제한다.
  Desktop은 작업 중 입력을 잠그고 성공·실패·취소 뒤 이전 일시정지와 버튼 상태를
  `finally` 경계에서 복구한다.

## 최근 로그와 장기 기록 메모리

- `PersonalLogRingStore`는 인물마다 정확히 최근 100행만 보관한다. 내부 entry는 nullable
  wrapper와 padding을 제거한 36바이트 값이며 공개 `PersonalLogEntry`는 query 경계에서
  손실 없이 복원한다.
- 64명 segment 안에서 10행 slab을 공유한다. 첫 기록 시 30행을 확보하고 31~100행은
  실제 도달할 때만 확장한다. 100행 이후 순환 덮어쓰기는 새 배열이나 이동을 만들지 않는다.
- 연대기는 반복 `PersonName` 대신 안정 `PersonIdentityId`와 Life 번호를 저장한다.
  query와 checkpoint 생성 시 현재 이름을 결합해 기존 시간순 DTO와 schema 31 wire를
  그대로 제공한다.
- 생별 연대기와 윤회 흔적의 비어 있는 collection은 lazy segmented 저장소로 유지한다.

## 결정론적 CPU 병렬 수행

- due 인물은 `(tick, AgentId)` 안정 순서로 한 번 수집한다. 고정 일정은 4진 indexed
  min-heap이 같은 tick의 AgentId tie-break를 보존한다.
- RNG·시장·관계·로그·권위 상태를 건드리지 않는 수행 입력만 immutable batch로 만들고
  최대 `min(논리 코어 - 1, 4)` worker에서 순수 계산한다.
- Result commit, 이벤트, RNG와 다음 일정은 원래 AgentId 순서로 직렬 적용한다.
- commit 직전 life/activity/plan/cultivation/health/location revision fingerprint가
  달라지면 그 인물만 원래 직렬 수행 경로에서 다시 계산한다.
- 직렬·병렬 5개 seed의 월·100년과 별도 1,000년 checkpoint hash, RNG, 사건, 연대기,
  `AdvanceTo` 분할 결과가 같아야 한다.

## 관찰·성능 계약

- 성능 진단은 pulse/query 시간 외에 할당량/초, GC0·1·2, heap, fragmentation,
  committed bytes, p95·p99, 정상 표본과 backlog를 제공한다. 강제 GC live bytes는
  benchmark 경로에서만 측정한다.
- 권위 계산은 50ms마다 `10 × speed` tick을 처리하고 관찰 무효화는 두 pulse를 합쳐
  100ms마다 보낸다. 1배속 10초/년과 모든 지원 배속의 tick 비율은 유지한다.
- 세계 로그 자동 출력이 꺼져 있으면 runtime event/역사서 feed를 구독·보관·복사하지
  않는다. 성능 UI의 percentile·그래프·로그 행은 새 1초 표본이나 탭/크기 변경 때만
  다시 만든다.
- 닫힌 화면은 query·DTO·행 materialization을 실행하지 않는다. observer preference는
  UI 선택만 보관하고 simulation 권위나 RNG를 바꾸지 않는다.
- 10,000명·20,000 tick 독립 Release 5회 중앙값은 900ms 이하여야 한다.
- 100배속 10분 SOAK는 warm-up 뒤 pulse p95 80ms, p99 100ms, 정상 표본 95%, 최종
  backlog 1초 예산을 모두 만족해야 한다.
- 개인 로그는 100행 이후 retained memory와 append allocation이 증가하지 않아야 한다.

## 추적 상태

- 이 수직 계약은 저장·runtime·메모리·수행 병렬화에 한정한다.
- 238개 이식 원장의 기존 M14 UI/접근성 `Gap / Deferred` 63건을 이 최적화로 완료 처리하지
  않는다. 원장은 `Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 / Gap·Deferred 63`을
  그대로 유지한다.
- 미승인 게임 수치·공식·콘텐츠를 추가하지 않았다.
