# PC 0.0.25 분할 권위 저장·조직 역사 보존 수직 계약

## 사용자 결함과 완료 조건

- 하나의 거대한 JSON이 1.5GiB 상한에 도달해 저장이 실패하던 경로를 제거한다.
- 세이브 슬롯은 계속 한 개의 원자적 SQLite `world.db`로 유지하되, 직렬화 단위를
  적절한 크기의 독립 section으로 분리한다.
- 불러오기는 모든 대형 payload를 동시에 만들지 않고 section을 순서대로 검증·해제한다.
- 조직 역사는 조직별 최근 100건만 보존한다.
- 기존 SQLite 슬롯과 primary/temp/backup 후보 복구, 외부 WorldSchema 25, 내부
  checkpoint schema 32를 유지한다.

## 저장 wire와 원자성

- `authority_snapshot`에는 인물·세계 이벤트·세계 연대기 배열을 비운 작은 권위 header를
  저장한다.
- `authority_layout`은 format version, 전체 항목 수, section 수와 합계 크기를 저장한다.
- `authority_section`은 종류·순번·원본 offset·항목 수·JSON 크기·container 크기·
  SHA-256·BLOB을 저장한다.
- 인물은 512명, 세계 이벤트와 세계 연대기는 4,096건 단위다. 각 section의 압축 전 JSON
  상한은 512MiB, 압축 container 상한은 256MiB다.
- header와 모든 section은 같은 SQLite transaction에 기록한다. 전체 후보 검증이 끝난
  뒤에만 기존 primary를 backup으로 보존하고 temporary를 primary로 원자 교체한다.
- 기존 단일 `authority_snapshot` DB에는 layout table이 없으므로 이전 codec 경로로
  읽는다. 기존 파일을 관찰만으로 다시 쓰거나 이동·삭제하지 않는다.

## 순차 복원·메모리

- 후보 목록은 고정 metadata와 layout만 읽으며 section payload를 해제하지 않는다.
- 선택 후보는 header를 한 번 읽고, section metadata의 순서·offset·coverage를 검증한 뒤
  BLOB을 하나씩 해제해 최종 배열에 복사한다.
- SQLite 외부 SHA-256과 CMIP 내부 Brotli payload checksum은 같은 순차 읽기에서 함께
  검사한다. 복원용 scratch 파일은 성공·실패 모두 정리한다.
- canonical state hash 검증으로 만든 현재 schema `World`를 Application이 재사용해 같은
  checkpoint를 두 번 복원하지 않는다. 구 schema는 기존 migration 경로를 사용한다.

## 조직 역사 보존

- `OrganizationRules.HistoryRetentionLimit`의 사용자 확정값은 100이다.
- 신규 조직 이벤트는 전역 sequence를 먼저 증가시킨 뒤 해당 조직의 최근 99건과 새
  기록만 보존한다. 이벤트 종류별 우회 추가 경로를 허용하지 않는다.
- 이전 세이브의 100건 초과 배열은 원본 state hash를 검증한 다음 runtime 게시 직전에
  최신 100건으로 줄인다. 이 정리는 gameplay RNG와 새 이벤트 sequence를 변경하지 않는다.
- 조직 UI와 checkpoint는 같은 bounded 권위 배열만 읽으므로 닫힌 화면 여부와 무관하게
  장기 메모리와 저장 크기가 조직 수 × 100건 범위로 제한된다.

## 검증

- 10,000명 저장이 header 포함 21개 이상의 순차 데이터 조각으로 생성되는지 검사한다.
- 분할 후보의 저장·복원 state hash, 9개 슬롯 격리, 단일 payload 호환, primary/temp/backup
  복구, scratch 정리를 검사한다.
- 조직 이벤트 150건 입력 후 최신 100건·연속 sequence를 검사하고, 120건 legacy 배열의
  21~120 sequence 이관과 장기 세계의 조직별 상한을 검사한다.
- 실제 기본 슬롯은 읽기 전용 source로만 사용하고 GUID 임시 root에서 분할 저장·순차
  복원한다.

## 남은 경계

- 이 변경은 대형 권위 graph를 안전한 section으로 나누는 저장 결함 수정이다. M02 V13의
  승인된 53개 논리 table 전체 이식을 완료로 주장하지 않으며 해당 행은 `Gap/Deferred`로
  유지한다.
