# PC 0.0.21 Runtime·SQLite 저장 수직 계약

## 범위와 판정

- 요구사항: `A110-M02-V01`~`A110-M02-V17`
- V01~V12, V14~V16: `Port`
- V17: Android 실기기 검증을 Windows x64 직접 실행·결정론·복구 시험으로 대체하는 `PC Adapt`
- V13: 단일 SQLite 주 DB와 transaction은 구현됐지만 승인 정본의 53-table 논리 도메인 물리 분리는 아직 단일 `authority_snapshot` canonical payload이므로 `Gap / Deferred`

## 수직 계약

- Runtime worker는 `_worldGate` 안전 경계에서 권위 checkpoint와 비권위 표시 요약을 함께 캡처한다.
- 저장 캡처·인코딩·파일 I/O는 UI 호출 스레드가 아니라 백그라운드 작업에서 수행한다.
- 저장은 `Safe Boundary → Encode → temporary SQLite → 전체 후보 검증 → backup 보존 → primary 원자 교체 → manifest` 순서를 따른다.
- 불러오기는 primary/temp/backup metadata와 payload hash를 검사하고 최신 generation부터 Candidate World를 복원한다.
- Candidate World의 checkpoint metadata와 실제 복원 state hash가 일치한 후에만 Runtime의 권위 World를 교체한다.
- 복원 실패는 기존 권위 World를 바꾸지 않으며 typed status, 후보 종류, 실패 원인을 반환한다.
- query cache와 UI 표시 DTO는 저장 정본이 아니며 복원 뒤 새 `WorldObservationQueries`로 재생성한다.
- generation sequence, world/checkpoint schema, candidate kind, 실패 목록, state hash가 복구·migration 근거로 남는다.

### checkpoint schema 31 월 경계 결정론 이전

- 외부 SQLite `WorldSchema 25`는 유지하고 내부 checkpoint만 30에서 31로 올린다.
- schema 31은 `DecisionRankingCheckpoint`에 마지막 처리 월, 전투력·경지·자질·부자
  inverse rank, 범주별 dirty 상태를 저장한다. 이는 표시 cache가 아니라 다음 AI 행동의
  입력이 되는 월간 의사결정 스냅샷이다.
- schema 30 이하는 당시 계약대로 현재 수치에서 랭킹을 한 번 재구성하고 저장 시
  schema 31로 승격한다. 구 schema hash 검증에는 schema 31 필드를 포함하지 않는다.
- schema 31 복원은 rank 배열의 1-based 완전 순열, 처리 월과 세계 시각의 일치,
  dirty 상태를 검사한다. query와 checkpoint 생성은 정렬이나 RNG를 발생시키지 않는다.

## 진행률·UI

- Application은 Persistence 단계 enum을 Windows 표시 DTO로 변환한다.
- 저장 카드 하단에는 실제 단계 기반 0~100% 진행률과 현재 단계가 표시된다.
- 불러오기 진행은 후보 검사, 선택, 해제, World hash 검증, Candidate World 원자 교체를 구분한다.
- 작업 중 카드 입력을 비활성화하고 완료·실패 시 진행 표시를 닫는다. 지연 도착한 표본은 작업 session 종료 후 무시한다.

## 성능·검증

- 10,000명 SQLite save/load 5회 중앙값 1초 budget을 테스트한다.
- 9개 슬롯 catalog는 manifest와 고정폭 header만 읽고 payload를 해제하지 않으며 p95 50ms budget을 테스트한다.
- 저장·불러오기 진행률은 단조 증가, save 100%, 후보 검증 90% 뒤 Application 원자 교체 100%를 검증한다.
- primary 손상 시 backup, crash-left temp 후보 복구와 state hash 일치를 검증한다.
- 구 `.cmip` 원본은 열기·수정·이동·삭제 없이 typed 비호환으로 거부한다.
- 관찰·저장 여부가 RNG와 권위 hash를 바꾸지 않는 회귀 시험을 유지한다.

## 남은 검토

- `A110-M02-V13`: 53-table 물리 schema, Stable ID 8-byte BLOB column, 핵심 index, 2-generation verified backup의 정본 일치 이식.
- 현재 단일 canonical payload는 기능 안전성과 복구 근거를 제공하지만 이 조항의 완료 증거로 승격하지 않는다.
