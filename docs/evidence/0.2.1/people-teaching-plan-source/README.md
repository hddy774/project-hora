# 실제 전수 요청의 계획 출처 회귀

정확한 소스 `068ecefd3226d9d0530aa351fcb28c4002b9edf5`의 기존 검사는 0/1로 실패했다. 원본 검사와 독립 공개 명령 진단은 같은 창세 법칙·편입·현재 노동·기존 유한 식량 소유 조건을 사용한다. 제품과 fixture는 바꾸지 않았다. 원본 실패 보고서와 로그는 그대로 보존한다.

실제 요청은 `entry`, 요청 월 1, 학습자 `Wait`·완료 월 2, 스승 `Guard`였다. 이벤트 출처 `plan:2074:1:0:1:requestTeaching`은 학습자가 저장한 `plan:request:entry` 기억의 출처와 같고, 실제 몸·월·결정 순번·탐색 수에서 구성한 명령 ID와도 일치한다. 현재 `Actions.Decide → RequestPlanningKnowledge → ExecutePlanningStep → TryKnowledgeCommand(requestTeaching)` 경로가 공통 어댑터를 사용한다. 예전 `ai-knowledge-request:` 접두사 하나를 기대한 검사가 현재 계획 경로와 맞지 않았다.

기존 실제 요청·대기·스승 노동·무료 지식/학습 방지·저장 검사는 유지한다. 새 기대는 접두사를 넓게 허용하지 않고 실제 결정 ID와 독립 요청 기억의 출처를 함께 비교하며, RNG 불소비와 남아 있는 대상별 `awaitKnowledge` 단계도 확인한다. 수정 후 같은 선택 검사는 **1/1 통과**했다. 전체 통합 성공이나 성장·비용 측정 결과로 확대하지 않는다.

`provenance.json`은 소스·실제 Game DLL 버전/SHA256·실행 범위·원본 파일 해시를 담는다. `public-probe-068.cs.txt`와 `.csproj.txt`는 기존 준비 조건을 그대로 재현한 scratch 진단 원본이며 제품 프로젝트에서 참조하지 않는다. 실험용 자산·지식·AI 성공을 주입하지 않았다. `updated-functional.original.json`은 테스트 기대를 수정한 작업 트리의 선택 실행이고, 정확한 부모 소스는 provenance에 별도로 기록한다.
