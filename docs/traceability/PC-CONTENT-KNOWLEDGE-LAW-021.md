# PC 0.0.21 콘텐츠·지식·법칙 이식 계약

## 적용 원칙

- P11/M11~M13의 C++ 구현과 승인된 Notion 명세를 이식 후보 근거로 사용하되, PC의
  `Core → Simulation → Queries → Runtime`, `Simulation → Persistence` 경계를 지킨다.
- 승인된 수량은 보존한다. 배치·발견 확률·초기 분배처럼 PC에서 임시로 정한 값은 안정
  review ID와 revision을 갖고 `PC_TEMP_REVIEW`로 남긴다.
- 법칙 후보는 승인된 Stable Law가 아니다. Adapter·Result·Metric·State I/O·SQLite
  round-trip·save replay 증거가 모두 없으므로 후보 ref로만 저장하고 Stable LawId를
  발급하지 않는다.

## Content authority

- Definition과 Instance를 분리하고 Instance에는 Owner, Holder, 위치, 수량, 품질
  가용성, 상태와 최신 provenance를 둔다.
- 생성·이전·예약·소비·파괴·붕괴·발견은 typed command/result로 처리한다. 실패한
  전이는 상태와 hash를 바꾸지 않으며 동일 source result 재사용을 거부한다.
- 인스턴스는 위치·소유자·보유 위치·정의별 index를 유지한다. 발견되지 않았거나
  비활성인 비경을 AI가 매 tick 전수 검사하지 않고 이동·사냥 결과에서 결정론적으로
  발견 후보만 평가한다.
- 승인 초기 모집단은 자연 비경 240, 인공 비경 60, 재료 stack 9,000, 아이템 2,400,
  물리 서적 600이다. PC 지점 배치와 발견 확률은 아래 review 대상으로 남긴다.

## Knowledge authority

- 내용 정체성, 판본, 물리 기록 binding, 개인·조직 접근/Claim, provenance를 서로 다른
  안정 참조와 저장 열로 분리한다.
- 원본·복사·개정·요약·번역·변조·복원·재구성 판본 관계를 보존한다. 다른 내용의 판본을
  parent로 연결하거나 하나의 물리 기록을 여러 판본에 중복 binding하지 않는다.
- 개인의 접근, 이해도, 신뢰도와 오해·교정 상태는 판본 자체를 변경하지 않는다.
  불변 내용과 판본은 공유하고 인물마다 전체 텍스트를 복제하지 않는다.
- 조직 전승, Teaching과 법칙 연구의 전체 인과 연결, 실제 복원 공식과 장기 전승 UI는
  아직 `Gap/Deferred`다.

## Law review authority

- C++ 이식 후보 목록의 기초 10개·파생 44개를 이름 없는 `LawCandidateRef`로 보존한다.
- 객관 후보 상태, 개인 Knowledge Claim·이해·체득·운용·대도 방향, 세계 AI 판단,
  천도 판단을 별도 owner와 checkpoint 열로 저장한다.
- 개인의 오해·교정은 객관 후보 readiness를 바꾸지 않는다. 세계 AI와 천도의 판단은
  서로 다른 actor이며 REVIEW 상태에서는 application/effect를 만들지 않는다.
- Scope·Relation은 `DataUnavailable`, 일곱 readiness evidence는 0, application은 0으로
  시작한다. 현재 revision은 외부 입력으로 이를 READY로 위조한 checkpoint를 거부한다.
- 실제 Law adapter, 상태 입력·출력, metric, 세계 사건, Stable LawId와 법칙 실행은
  승인 증거가 생길 때까지 `Gap/Deferred`다.

## 저장·조회·UI

- checkpoint schema 27은 Content, 28은 Knowledge, 29는 Law review authority를
  추가한다. 구 schema의 hash 의미를 보존하고 누락 상태는 각 migration 계약대로만
  초기화한다.
- Content·Knowledge·Law는 state hash와 SQLite payload에 포함한다. query cache와
  화면 행은 저장하지 않는다.
- 콘텐츠 작업공간이 닫히면 관련 query와 DTO가 0개다. 열린 작업공간에서도 비경,
  판본, 법칙 내부 탭 중 실제 선택 탭의 행만 page 단위로 materialize한다.
- 일반 세계 설정 모드에는 이해 가능한 검토 상태만 표시한다. candidate ref, Scope,
  Adapter·Metric 등 내부 증거는 세계 설정 모드에서만 표시한다.

## PC_TEMP_REVIEW revision 1

- `PC-TEMP-CONTENT-PLACEMENT-021`: 초기 비경·재료·아이템의 PC Atlas 배치 규칙.
- `PC-TEMP-CONTENT-PC-DEFINITIONS-021`: 간략 PC 아이템 정의와 승인 수량의 매핑.
- `PC-TEMP-CONTENT-DISCOVERY-CHANCE-021`: 이동 5%, 사냥 10% 발견 후보 확률.
- `PC-TEMP-KNOWLEDGE-INITIAL-DISTRIBUTION-021`: 60 내용·90 판본·600 실물 초기 분배.
- `PC-TEMP-KNOWLEDGE-RESTORATION-FORMULA-021`: 실제 복원·재구성 공식은 미승인 상태.
- `PC-TEMP-LAW-CANDIDATE-CATALOG-021`: 기초 10·파생 44 ordinal 후보 catalog.
- `PC-TEMP-LAW-READINESS-GATE-021`: 일곱 증거가 모두 없으면 REVIEW를 유지하는 gate.

## 자동 검증

- `ContentAuthorityTests`: 원자 lifecycle, index, 발견, checkpoint graph, 지연 관찰.
- `KnowledgeAuthorityTests`: 판본 계보, 오해·교정, 실물 binding, schema migration.
- `LawAuthorityTests`: 후보-only gate, owner 분리, 무효과 preflight, schema 29 왕복.
- 전체 자동 검사는 `64/64`를 통과했으며 관찰 전후 state hash와 RNG 소비가 같다.

## 구현 커밋

- Content: `357fe3c`, `128fd4f`, `9cc6a71`
- Knowledge: `326ec67`, `8d57f32`
- Law: `2345c93`, `7e1e8ce`, `b753773`, `2aaaf94`, `8d97947`
