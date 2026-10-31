# 첫 영속 보관 실행의 HTTP415 실패

실제 [run41161342049/attempt1](https://github.com/hddy774/project-hora/actions/runs/41161342049)은 소유자 라벨1회로 요청되어 `a8709c0de824c3071d97ecb6cd9a86ab73641b13`을 checkout했다. actor/triggering_actor는 hddy774, 실제 이벤트는 pull_request이며 자체 검사23/23 뒤 원형 다운로드 단계에서 실패했다. 재실행·재라벨·source 이동 없이 이 원문을 보존했다.

- [상태 원문](status.json)은 `state=실패`, `phase=original-download`, HTTP415다. 상태 artifact13026938746의 원형 ZIP은420B, SHA256 `ab8b13764abb1788b28fd7b714185e80cd816dd08cbf4f4fa8afe3383656a818`과 일치했다. 내용은 status.json 한 개이며 최종 index는 없었다.
- [작업 원문 UTF8](job-124419348718.log.json), [run 원문](run.json), [jobs](jobs.json), [artifact metadata](artifacts.json)를 보존한다. 작업 로그는 `originalUtf8`을 UTF8로 복원하면 BOM·공백·마지막 개행을 포함해 `originalBytes/originalSha256`과 일치하는 JSON 래퍼로 보관한다. GH_TOKEN은 Actions가 마스킹한 값만 보인다.
- 코드의 원형6개 순서상 첫 진입은 artifact13024674339/zip이다. 원래 실행은 요청별 URL을 출력하지 않으므로 첫 ID는 고정 호출 순서로 대조했다. 같은 첫 URL의 [무인증 읽기 전용 probe](anonymous-actions-zip-accept-probe.json)는 `Accept: application/octet-stream`에 HTTP415와 `Must accept 'application/json'` 본문을 반환했다. 이 probe는 인증된 다운로드 성공의 증거가 아니다.
- 수정은 Actions ZIP 진입의 Accept를 고정 `application/json`으로 분리한다. 원형 redirect 뒤 bytes/SHA 검사와 교차 호스트 Authorization 제거를 유지하며 draft release asset·공개 asset의 octet 미디어는 바꾸지 않는다. [수정 전 mock415](../actions-zip-415-before.log)와 [수정 후24개 검사](../actions-zip-media-self-test.log)를 분리 보존한다.
- [관측 release 목록](releases-after.json)에는 기존0.2.0/0.1.0만 있다. [latest 원문](latest-after.json)은0.2.0 ID428209007, APK689402166·43,505,222B·서버 SHA256 `b381a400514ea311117bb2b28d15c68baffc00d4ea71a689c9140a69867bf2df`로 기존 값과 일치한다. 이 점검에서 APK를 다시 내려받은 검증으로 확대하지 않는다.

고정 source의 실패 위치는 draft 생성 전이다. 이번 보관 실행은 공개 archive·최종 index를 만들지 못했으며 보관 상태는 pending이다. 원래 민감도 run41131479924의 전체failure·4완주/2partial·complete=false와 새 분리 실행의 결과를 변경하지 않는다. 공식 v0.2.1 서명·출시는 실행하지 않았다.
