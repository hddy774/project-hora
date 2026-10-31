# c6 기준 313개 기능 원본과 24INV 매핑

정확한 소스 `c6e470458ecefe055d98f89120f08c40c0c0660d`, 제품 지문
`dcc95b5cb52a0d3b8e6b66975b736a685f4a82250508a2434d29563bd7b9cfc0`의 루트 전체 C# 기능313/313·실패0·exit0/clean을 보존한다.
실행 worktree는 `/workspace/seonro-final-c6e4704`, CPU0,18:18:45.327637~18:24:17.486670 UTC다. 외부 CPU1~3 실행이 있었고 기능 합산296.633초는 성능 표본이 아니다.
매핑 작업에서 기능 runner/collector를 실행하지 않았다.

- [functional.json](functional.json)·[functional.log](functional.log)·[root-run.original.json](root-run.original.json)은 scratch 원문과 byte 동일하다. JSON SHA256: `5704c7e50745960585b11b77afb19f844a97991eeda7bfc3be25dece5bd07abe`.
- [provenance.json](provenance.json)은 실제4DLL 생성/내장 정보 버전·hash·실행 폴더 복사본·runtimeconfig/deps·원문/맵/SourceReview 출처를 묶는다. 독립 프로세스 attestation은 아니다.
- [execution-verification.json](execution-verification.json)은313개 이름·24INV·148개 명명 증인을 대조한다. [기계 맵](../invariant-execution-map.json) 자체는 `not_asserted`다.
- [negative-input-checks.json](negative-input-checks.json)은 이전9704의309개 전체·실패305/308·선택74개 원문을 현재 전체 근거로 거절한다. 과거/선택 원문은 수정하지 않았다.
- [validator-self-test.json](validator-self-test.json)·[refresh-self-test.json](refresh-self-test.json)은 Python 합성/정적 자체 시험이며 제품 C# 통과 수에 더하지 않는다.
- [supervisor-launch-failure.original.log](supervisor-launch-failure.original.log)는 fingerprint 문자열을 JSON으로 파싱한 초기 래퍼 오류다. 게임 빌드/성공 run 시작 전에 발생했고 실제 전체 성공과 분리 보존한다.

[소스 검토](../invariant-execution-source-review-c6e4704.json)는 제품 C#2+Hosting csproj1+검사 C#1의4경로와 변경 없는 HTML2개를 구분한다.
새4검사는 작은 실제 세계의 legacy/양형식 저장2개, byte/codec 부정module1개, 실제 임시파일host1개다. 내부 임계값을 써 같은 생산 분기를 실행한다.
이진봉투3은 상태Schema1/DomainSchema2·작은JSON봉투2·이전 상태 거부 정책을 바꾸지 않는다. 실제 대규모 저장 성공과 메모리/배열/JSON 개별 값 상한은 별도다.

이전 [9704 원본/출처](../invariant-final-9704e95/README.md)·[309/144 JSON 맵 사본](../invariant-final-9704e95/map.snapshot.json)·
[Markdown 사본](../invariant-final-9704e95/map.snapshot.md)·[SourceReview](../invariant-execution-source-review-9704e95.json), 그 앞926 기록은 보존한다.
두 맵 사본은 갱신 전 bytes 그대로이며 이전 provenance의 map hash와 대조한다. Markdown 원래 상대 링크는
[당시 경로](https://github.com/hddy774/project-hora/blob/d795f7027ac078ba729ca1881f101a7e96e3e5e1/docs/evidence/0.2.1/invariant-execution-map.md)를 따른다.

[D02/T01~T03 감사 초안](../acceptance-functional-c6e4704.md)은 명명된 조항의 자체수용 권고와 선행 마감을 구분한다.
특히 D02는 #191의 수정후 실제10,000명 기본 저장/읽기 성공 전까지 권고 보류다. 이전926 저장 실패/exit134와 성장/AI 중단·복구 자료를313 기능 통과로 덮어 쓰지 않는다.
전체24INV의 모든 조합·정본111개 전체·성능/성장/장기 안정성·native/플랫폼·전체T05·릴리스 완료는 주장하지 않는다.
제안 카탈로그의 `test_status=not_implemented`·원장·Notion·readiness·제품/CLI/scripts/C# 변경과 새 runtime/계측 실행은0이다.
