# 보충 민감도 원형의 공개 보관 검증

[보관 실행 41177374470](https://github.com/hddy774/project-hora/actions/runs/41177374470)의 attempt1은 성공했다. [근거 사전 릴리스](https://github.com/hddy774/project-hora/releases/tag/evidence/v0.2.1-sensitivity-41159864831)(release429767706)에 원형 ZIP6개와 색인1개를 보관했으며, 2026-10-31 06:40 UTC부터 실제 공개 다운로드를 별도로 확인했다. 제품 0.2.1의 공식 릴리스 합격을 뜻하지 않는다.

## 실제 공개 바이트와 출처

[public-byte-proof.json](public-byte-proof.json)은 Authorization·Cookie 없이 실제 HTTPS 요청14건으로 받은 공개 파일7개 전체의 길이·SHA-256·최종 HTTP200을 기록한다. ZIP6개 합계는 **9,952,241바이트**이며 [고정 원형 명세](manifest.original.json)의 각 파일과 일치한다. [공개 색인 원문](public-index.original.json)은 **24,058바이트**, asset694601640, SHA-256 `20168d0596fa0f9d17f933d37a732e0af3725513c3aeb897ec769efc29c7d9cd`다. 임시 CDN 주소의 서명 query는 기록하지 않았다.

[status.original.json](status.original.json)은 별도의 Actions 상태 artifact13033617071에서 가져온 원문이다. [대조 결과](status-index-comparison.json)는 이 artifact 안의 색인과 공개 다운로드 색인이 바이트까지 같고, 명세·원래 실행 metadata도 고정본과 일치함을 보인다. 이 인증된 상태 artifact 조회를 무인증 공개 다운로드 건수에 포함하지 않는다. 원형 ZIP과 상태 artifact ZIP은 Git에 중복 추가하지 않는다.

| 역할 | 실제 출처 |
| --- | --- |
| 보관 실행 | PR263 HEAD `46c484e3ffb4bdac69725ff789c959ef3721c475`, run41177374470/attempt1, 실제 pull_request/labeled, owner hddy774 |
| 보관 대상 실행 | run41159864831/attempt1, metadata HEAD `19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6`, workflow source `836ff97e930e82af36a6f5fae6dae575c109f7d9` |
| 실제 성장 측정 | source `7ce66943b539420c9dc1e3fa8a611daae58020ff`, 제품 지문 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304` |
| 이 문서의 기준 | 개발 소스 `ae8353401d694cb36e76b1f76677d09add82b39d`, 제품 지문 `0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2` |

보관 run·release의 선택 metadata는 [투영](publication-metadata.projection.json)에 저장했다. 실제 측정 소스를 보관 도구나 현재 제품 소스로 바꾸지 않는다.

## 기존 자산 보호

[보관 전](protected-before.projection.json)과 [보관 후](protected-after.projection.json)는 루트가 수집한 원형 API 응답에서 명시된 보호필드만 뽑은 **투영**이다. 각 원형 응답과 전체 입력 파일의 바이트 수·SHA-256을 남겼으며 전체 API 본문을 복제한 자료는 아니다. 본문은 정확한 UTF8 SHA-256으로 표현하고 다운로드 횟수 등 가변 필드는 제외했다.

[독립 재대조](protected-comparison.json)의 세 결과는 모두 true다: 기존 0.2.0 latest/release428209007 및 APK689402166의 보호 metadata, 기존 근거 release429687967의 보호 metadata, 기존 제품 태그가 동일하다. 저장한 전후 투영을 직접 비교할 수 있다. 이 작업에서 기존 APK를 다시 다운로드하거나 바이트를 새로 검증한 것으로 확대하지 않는다.

## 원래 실패와 부분 실행의 위치

원래 run41131479924와 보충 run41159864831은 **각각 4개 조건 완주·2개 조건 시간 상한 부분 종료, 전체 실패**다. 보충의 seed98은 기존 두 조건의 중복이며, 기존4조건과 보충1500/42·2026의2조건을 두 계획에 걸쳐 세면 고유6조건의 유한 관측이 있다. 이를 한 계획의 정상 종료·aggregate PASS로 합성하지 않는다. 원형 report의 complete=false와 seed별 requested 값, collector 실패를 유지한다.

[보충 결과·계획·부분 실행](../growth-sensitivity-results-41159864831/README.md)의 고정 작성 소스는 [PR264 bab085ff](https://github.com/hddy774/project-hora/tree/bab085ff724077c3c06d250abf256b805f1777d5/docs/evidence/0.2.1/growth-sensitivity-results-41159864831)다. [collector 참조](collector-reference.json)가 구분하듯 collection ZIP90개 항목 중89개는 seed ZIP과 동일한 중복이며 고유 항목은 provenance/request.json뿐이다. 실제 오류 로그18953바이트는 별도 Git 원문 [collector/job-124465144337.log](../growth-sensitivity-results-41159864831/collector/job-124465144337.log)에 보존한다. 이 로그가 ZIP의 고유 항목이라고 설명하지 않는다.

이 보관 검증으로 Z04의 기존 유한 보고 판정이나 T04·T06을 변경하지 않는다. 문서 갱신 시각의 최종 안정성 run41170302565는 실제 2db5a6d/제품 지문0eaf의 원래4규모×120개월 측정 중(마지막 상태 기준06:45 UTC)이며, 과거 c91 대조 실행과 별개다. 원장109/111·87기능 완료, T04/T06 진행 중, readiness false, 메인 PR84 DRAFT를 유지한다. 새 게임 실행·측정·릴리스 쓰기는 이 문서 작업에서 하지 않았다.

[검증 요약](verification.json)과 [파일 해시 색인](file-manifest.json)은 이 폴더의 작은 원문·투영·파생 자료를 구분한다.
