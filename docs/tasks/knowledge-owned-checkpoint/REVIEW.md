# 소유권·전이 경계 대조

실제 자체 검토: Codex. 기준 제품 소스는 `db0feae5a5111c6ffc97b180cc37b5f9a232b3e2`이며
원격 구현 `b7b96eb1f17787f92130ddd10fa1ce7faa05d935`와 tree가 같다.
별도 Claude 검토 수신으로 계산하지 않는다. 통합 담당자의 전체 검사와 최종 성능 수용은 별도다.
독립 cultivation 담당 Codex도 같은 제품 두 파일을 읽어 구체 blocker를 찾지 못했다.
해당 읽기 원문은 `checks/independent-source-review.json`에 보존한다. 작성자의 실행을 독립 재실행한 검토는 아니다.

## 상태의 유일 소유와 외부 사본

`GameWorld.Restore`는 입력을 `GameCodec.Clone`한 뒤 검증한다. 공개 `Execute`도 독립 후보 세계에서
Apply/지식 정산/커밋 불변식을 통과한 경우에만 상태를 채택한다. private KnowledgeRuntime만 새 내부
factory를 호출한다. 다른 세계·UI·호스트에 authority나 가변 내부 상태를 반환하지 않는다.

공개 KnowledgeAuthority.Restore의 검증→깊은 복사, Checkpoint의 전체 typed clone, FindActivity의
중첩 배열 사본, PhysicalCheckpoint의 독립 Media/Access 선택은 바꾸지 않았다. 조회 DTO와
GameWorld.Checkpoint/common codec도 그대로다. 상태 schema와 직렬화 필드는 추가하지 않았다.

내부 캐시는 기존 `ReferenceEquals(knowledgeSnapshot, state.Knowledge)` 경계로 묶인다.
명령 채택으로 객체가 교체되면 다음 사용에서 새 상태를 검증하고 인덱스를 다시 만든다.
일반 월 전이의 같은 소유 상태에서는 기존 인덱스를 갱신한다. GameSession의 직렬 명령 실행을 전제로 하며
세계의 동시 가변 접근 API를 새로 만들지 않는다.

## 기존 SaveKnowledge 호출 전후

| 경로 | 대조한 중간 관측과 순서 |
| --- | --- |
| 창세 | 초기화가 독점 상태를 새로 만들고 캐시를 비운다. 작품/판본/명시 개인 지식/실물을 모두 연결한 뒤 Mirror를 호출한다. |
| study | module 시작→실제 행동/Due 설정→기존 Save 경계. 설정 중 조회는 authority.FindActivity의 분리 사본이다. |
| teaching | 양측 행동/Due와 요청 제거→Save. 중간에 다른 세계 지식 조회나 다음 AI 선택을 호출하지 않는다. |
| transcription 시작 | 실제 예약/소비 후 같은 source로 module 시작→행동 설정→Save. 공개 명령 실패는 후보 전체를 버린다. |
| experiment 시작 | BeginKnowledgePractice의 기존 Save 뒤 물리 예약을 한다. 예약 거부 시 실습 취소 후 공개 후보가 폐기된다. |
| 실제 완료 | 실험의 실제 소비/난수/결과는 module 완료 전이다. 완료→필사의 예약 해제/실물 전환/사건→Save→Mirror/사람 학습/기억→행동 해제 순서를 유지한다. |
| 취소 | module 취소→실물 예약 해제→참여자 행동 해제→Save 순서를 유지한다. |
| 물리 정산 | 규칙 변경→첫 물리 사본→실물 동기화→둘째 물리 사본→접근권 동기화→Save→현생/신체 무효 행동 취소. 두 사본의 독립성과 afterSync 순서를 유지한다. |
| 외부 제작/수행 실습 | Begin/Finish/CancelKnowledgePractice의 기존 Save 경계를 유지한다. 기존 기록/source/정책 snapshot과 성공한 실제 공정만 숙련에 반영한다. |

필사 완료의 예약 해제에서 Inventory의 world/projected fingerprint가 읽는 것은
Month/NextId/Revision/Items/People/Materials이며 Knowledge를 포함하지 않는다.
제작 Event의 사회 콜백도 지식 목록을 읽지 않는다. 완료 수업의 사회 정산과 사람의 학습 기록은 기존 Save 뒤다.
`KnowledgeActionSource`의 활동 수는 시작 전 또는 이미 저장 경계를 지난 뒤 읽으며, 행동 설정 중 새 ID를 만들지 않는다.
따라서 앞당겨 보이는 내부 지식 목록이 이 콜백의 영수증·RNG·다음 선택 입력을 바꾸지 않는다.

## 실제 확인

- 선택49개는 pure module·공개 world·고정 전사를 포함한다. 정확한 등록 이름은 verification.json에 있다.
- 새 세 검사는 public snapshot/Restore 입력·조회 배열 격리, 실제 후보 채택 후 월별 재결합,
  busy/동일 ID/충돌 payload, 커밋 직전 revision 예외를 확인한다.
- 결속된 실제 재료의 예약은 지식 실습 시작 뒤 거부된다. 거부 전후 hash가 같고 다음 실제 학습은
  별도 저장복원 대조 세계와 같다. 원래 cache에 실패 후보의 인덱스가 남지 않았다.
- 변경 전 df 네 DLL과 후보 DLL의 같은 seed42/73 공개 명령은 49개 경계와 실제 저장20개가 같다.
  두 실행 모두 매 경계 common-codec 저장재개를 독립 연속 세계와 대조했다.
- 닫힌 진단 파일의 N2 month120 및 N100 month30도 실제 bytes가 같다.
  새 표본의 월60 파일 IO 포함 때문에 전체 실행시간·전체 할당의 인과적 개선율은 판정하지 않는다.

기존 장기 실패와 진단1 부분 원문은 보존했다. 최종 장기/성능/네이티브·릴리스 합격이나 원장 변경의
근거로 이 선택 기능·30초 표본을 확대하지 않는다.
