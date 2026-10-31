# 지식 세계 소유 상태의 반복 전체 사본 제거

## 승인·문제·범위

통합 담당자가 이슈 #233의 실제 df72405 CPU1 진단2를 근거로 승인한 최소 성능 수정이다.
AdvanceTo 표본 안 `SaveKnowledge`/`KnowledgeAuthority.Clone`은 같은 중첩 경로의 약71.8%다.
이는 30초 표본의 가중 스택 비중이며 전체 장기 비용·누수·인과적 속도 향상 판정이 아니다.
실제 작성·명세·자체 검토는 Codex가 담당한다. 외부 Claude 검토로 표시하지 않는다.

기준은 `b15992b6150afb8e4f3348501c51051a7f7733d0`이다. 제품 변경은
`Knowledge/KnowledgeAuthority.cs`와 `World.KnowledgeBridge.cs`의 내부 소유 경로에 한정한다.
새 격리 검사와 이 작업 문서만 추가한다. 검색 인덱스·코덱·호스팅·실측 하네스·정책·예산은 바꾸지 않는다.

## 계약·수명

`AttachWorldOwned(KnowledgeState)`는 GameWorld가 이미 독점 소유하는 상태에만 연결하는 internal API다.
연결 중 지식 전이는 해당 authority만 수행한다. 공개 `Restore`는 입력을 검증하고 깊게 복사하며,
공개 `Checkpoint`, GameWorld의 저장/체크포인트와 조회 DTO는 계속 독립 사본을 반환한다.
외부 입력에 이 내부 API를 사용하거나 world 밖에 authority/상태 alias를 반환하지 않는다.

기존 `knowledgeSnapshot` 객체 동일성 검사는 그대로 둔다. 명령 후보 채택·복원으로 객체가 교체되면
다음 사용에서 인덱스를 다시 연결한다. 다른 세계의 캐시를 공유하지 않는다. `SaveKnowledge` 호출 시점은
그대로 두되 이미 반영된 독점 상태를 전체 복사해 교체하지 않는다. Source ID, revision, 월, RNG,
물리 예약·수업 양측 노동·개인 이해/숙련·재전송 의미는 유지한다.

모든 public 명령은 기존 후보 세계에서 실행된다. 거부·실제 커밋 전 예외는 후보를 버리므로 원래 세계의
상태·RNG·ID·원재료·영수증을 바꾸지 않아야 한다. 빈/잘못된 내부 입력도 기존 검증에서 거부한다.

## 수용 기준

1. 내부 전이는 같은 세계 소유 목록에 즉시 반영되고, 외부 snapshot/Restore 입력·조회 배열은 분리된다.
2. 실제 명령 후보 교체 후 재학습·완료와 저장재개가 같은 월별 결과를 낸다.
3. busy 거부·같은 ID 재전송·다른 payload 및 실제 커밋 직전 예외가 원래 상태를 바꾸지 않는다.
4. 변경 전 df의 네 DLL과 변경 후 네 DLL에 같은 seed/명령/월 경계를 적용하여 매 경계 상태 hash,
   RNG·NextId·지식 JSON·실제 저장 byte를 비교한다. 선행 원문을 고치거나 golden을 자동 갱신하지 않는다.
5. 아래 선택 검사와 고정 원본 전사가 통과해야 한다. 전체 기능·원격 장기 합격은 통합 담당자의 후속 실행이다.

## 사전 고정 검증 범위

- CPU1에서 Release 빌드, 분석기/경고 비활성화 없이 오류0·경고0.
- `--filter 'knowledge world-owned state:'` 새 세 검사.
- `--filter 'knowledge'` 기존 지식 전체 등록 증인(공개 world와 pure module 구분).
- `--filter 'AI 수행 연결'`, `--filter '감응 출처 선택'` 실제 학습·수행 caller 선택 검사.
- `--filter 'inventory owned reference:'` 기존 고정 원본 두 검사와 `--filter 'inventory copy oracle:'` 독립 전사 한 검사.
- df4DLL와 후보 DLL의 유한 명령 비교: 공개 학습·필사·전수·수행, 거부·재전송,
  매월 hash와 중간 common-codec 저장재개. 작은 고정 세계이며 대량 인구/성능 합격 검사가 아니다.

기능 검증 후 별도 사전등록한 진단 비교는 CPU1, 준비 포함180초, 기존 N2→N100 순서와 입력,
N100 month30 직후30초 CPU/할당 표본 한 번으로 한정한다. 범위 초과·부분은 그대로 보존하고
성공까지 늘리거나 장기 수용 기준·예산을 바꾸지 않는다. 원시 trace는 내구성 자산과 작은 Git 색인으로
보관한다. 비교 창과 공유 CPU 차이를 명시하며 원격/전체 성능 합격으로 쓰지 않는다.

## 증거·리뷰

실제 source·명령·등록 Name·표본/전체 구분·결과와 미검증 범위는 완료 시 이 폴더의 검증 자료에 기록한다.
기존 df 진단1 부분 원문과 진단2 원문은 그대로 보존한다. 원장·Notion·readiness는 변경하지 않는다.
