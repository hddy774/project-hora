# 리팩터링·시뮬레이션 성능 개선 로드맵

> 감사 기준: PC `0.0.21`, GitHub/local `main`
> `7eb4d540b328d4664778b3e494148db0cd36bee2`.
> 재검증 바이너리는 코드가 같은 `c559063d35ebd5e8f0c6763ad009d9b74d8fb40d`,
> 2026-09-02 KST. 이 문서는 개선 후보를 정리하며 게임 규칙·수치·저장 형식을
> 변경하지 않는다.

## 원칙

- 정확성, 결정론, 저장 호환 증거를 성능보다 먼저 확보한다.
- 파일 줄 수가 아니라 owner·생명주기·함께 검색하고 수정하는 작업집합으로 분리한다.
- 구조 이동과 게임 규칙·RNG·저장 wire 변경을 같은 변경에 섞지 않는다.
- 캐시 크기, TTL, 페이지 크기, 메모리 상한과 차단 임계값은 실측과 사용자 승인 전에는
  확정하지 않는다.
- UI와 query는 권위 상태나 RNG를 바꾸지 않으며 `Simulation` 배열을 직접 읽지 않는다.

## P0 - 정확성과 증거

### P0-1. 시간 분할 불변성

**근거**: `World.AdvanceTo`는 호출 종료 경계에서 dirty 랭킹을 최대 네 번 전체 정렬하고,
AI 판단은 그 랭킹을 읽는다. 현재 자동 검사는 한 번에 20,000 tick을 진행하지만 실제
runtime은 50ms 권위 계산 pulse와 `1/10/25/50/100x` 배속으로 여러 번 나누어 호출하고,
관찰 무효화는 100ms로 합친다.

**목표**: 동일 seed·설정·최종 tick이면 다음 진행 방식의 결과가 같아야 한다.

- 단일 `AdvanceTo(finalTick)`.
- 고정 크기와 임의 크기의 여러 chunk.
- 50ms production pulse와 지원 배속별 tick 분할, 100ms 관찰 신호 병합.
- checkpoint 저장·복원을 중간에 한 경우.

**비교 항목**: state hash, RNG stream 상태, event heap, 현재·기간 랭킹, AI 판단 revision,
checkpoint bytes의 의미와 복원 후 hash.

**완료 증거**: property test가 여러 seed와 경계 tick에서 통과하고, 실패 시 최초 차이
domain과 tick을 출력한다. 실패가 확인되면 다른 성능 최적화를 먼저 병합하지 않는다.

### P0-2. 랭킹의 권위 시간 경계

랭킹 갱신을 API 호출 종료가 아니라 simulation의 명시적 시간·revision 경계에 연결한다.
AI가 읽는 랭킹과 UI용 기간 랭킹을 구분하고, 한 경계에서 각 dirty domain을 최대 한 번만
계산한다. TOP-N 또는 증분 인덱스 도입은 분할 불변성 통과 뒤 동일 결과 증거와 함께
검토한다.

### P0-3. 고정 SHA 성능·릴리스 증거

0.0.21의 저장소 증거는 65/65 검사와 독립 프로세스 5회 성능을 기록하지만, 직접 화면
동작은 확인하지 않았다. 다음 릴리스 판정은 다음을 같은 SHA에 묶는다.

- Release 빌드, 전체 자동 검사, architecture·port-ledger 계약.
- 독립 프로세스 5회 성능, SOAK, 결정론·관찰 무영향·저장 복원.
- 실행 파일 checksum과 파일/제품 버전.
- 사용자 화면 직접 확인 또는 명시적 `PENDING`.
- GitHub Release 여부와 로컬 패키징 여부의 분리 표기.

## P1 - 조회·runtime·저장 hot path

### P1-1. revision 기반 observation invalidation

`SimulationRunner`는 일시정지 또는 권위 상태가 바뀌지 않은 pulse에서 observation을
무효화하지 않는다. world 전체 revision 하나 대신 통계·지도·랭킹·선택 인물·시장·기록
등 section별 revision과 dirty scope를 제공한다. Desktop은 현재 보이는 section의
revision이 바뀐 경우에만 query와 UI 갱신을 수행한다.

### P1-2. 통계·지도의 반복 10,000명 순회 제거

통계와 지도는 변경 없는 조회마다 새 10,000명 배열을 만들지 않는다. simulation commit이
남긴 변경 인물·위치·경지·전투력·재산 domain을 바탕으로 aggregate와 index를 갱신하고,
Queries는 불변 snapshot을 짧게 capture한 뒤 world lock 밖에서 DTO를 투영한다. 닫힌
화면과 접힌 detail은 DTO·행·문자열을 만들지 않는다.

### P1-3. 랭킹 계산 비용 분리

전투력·경지·자질·부자 네 랭킹의 dirty 원인을 각각 추적한다. 전체 순서가 필요한 AI와
TOP 1,000만 필요한 UI의 소비 계약을 분리하고, full sort·partial selection·증분 index의
비용과 결과 동일성을 같은 harness에서 비교한다.

### P1-4. 저장 copy와 SQLite 지연

authority snapshot capture, encode, compress, container checksum, SQLite transaction을
각각 계측한다. `WorldSaveCodec`의 compressed payload/container `ToArray`와
container→payload→decompressed JSON 중복 메모리를 streaming 또는 pooled buffer로
줄인다. primary/backup/temp 원자성, checksum, schema 7~30 migration과 state hash는
그대로 유지한다.

보존된 0.0.21 package evidence의 SQLite 중앙값은 저장 973ms, 복원 869ms다. 감사 중
코드 동등 Release 바이너리를 독립 프로세스 5회 다시 실행한 교차 프로세스 중앙값은
852/851ms였으나, 5개 중 1개가 1,108/1,027ms로 권고 1초를 넘었다. 현재 테스트는 이
초과를 hard failure로 처리하지 않는다. 다음 기준선에서는 꼬리 지연과 gate 누락을 별도
P0 증거로 기록하되, 사용자 승인 없이 새 차단값을 만들지 않는다.

### P1-5. 측정 규약

- 고정 SHA와 Release 구성, CPU·메모리·.NET·OS·시각을 기록한다.
- 10,000명·20,000 tick을 새 프로세스 5회 실행해 median·min·max를 기록한다.
- production pulse는 충분한 표본에서 p50·p95·p99, backlog, allocation과 GC를 기록한다.
- query는 summary, 선택 detail, 1,000행, 닫힌 section을 나눠 측정한다.
- checkpoint·복원과 SQLite 저장·복원을 별도로 측정한다.
- 과거 버전과는 같은 harness·환경·입력의 결과만 비교한다.

## P2 - owner와 작업집합 정리

### P2-1. Query projection 경계

Simulation에 남은 read-only 통계·지도·랭킹 projection을 Queries owner로 이동한다.
Simulation은 권위 상태와 최소 불변 snapshot만 제공하고, 문자열·WPF 타입·화면 상태를
소유하지 않는다.

### P2-2. Persistence 경계

World의 checkpoint state capture와 Persistence의 container·codec·checksum·migration·
SQLite 저장을 명확히 나눈다. 저장 wire 변경이 없는 구조 이동을 먼저 하고, wire 변경이
필요하면 별도 version·fixture·migration 작업으로 분리한다.

### P2-3. 테스트 작업집합

단일 대형 테스트 프로그램을 기능별 suite와 공통 registry로 나누되 전체 검사의 한 진입점,
검사 이름, `RESULT: N/N passed` 형식은 유지한다. 각 suite는 정상·경계·잘못된 ID·결정론·
저장·관찰 무영향을 함께 소유한다.

### P2-4. WPF 화면 작업집합

`MainWindow`의 선택 인물, 설정, 지도, 시장, 기록 표현을 view model 또는 화면별
작업집합으로 나눈다. Application 명령과 Presentation DTO 계약은 유지하고, Desktop에서
World나 authority store를 직접 읽는 우회 경로를 허용하지 않는다.

### P2-5. Hot state와 cold history

현재 행동·현재 랭킹·짧은 ring buffer는 hot state로 유지하고, 무제한 생애·세계 기록은
cold history로 분리한다. summary/detail DTO와 keyset cursor를 사용해 선택된 범위만
지연 로드한다. cache·page 수치는 실측과 사용자 승인 후 정한다.

## 모바일 참고 backlog

모바일 release `v1.1.2`는 PC와 별도 제품선이다. 현재 로컬 모바일 checkout은 release
source SHA가 아니므로 다음 항목은 구현 요청이 아니라 release SHA 재감사 후보로만 남긴다.

- `AppState → alpha110::LiveRuntimeFacade110` 활성 경로와 문서상 legacy DAG의 차이.
- `alpha110` 내부 simulation·formula·runtime·codec·query 책임 재분류.
- advance·render·전체 JSON observation이 같은 mutex를 사용하는 구간 측정.
- summary/detail DTO, visible section 요청, revision snapshot으로 전체 JSON 빈도 축소.

## Backlog 작성 규격

각 후속 항목은 반드시 `증상과 근거`, `목표 owner`, `비목표`, `결정론·RNG 영향`,
`저장·migration 영향`, `측정 전후`, `테스트와 완료 증거`를 기록한다. 정적 코드 존재만으로
완료 처리하지 않고 고정 SHA의 테스트·artifact·직접 검증 상태를 구분한다.
