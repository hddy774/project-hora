# 동결 소스9263756의 전체 기능 원본과 실제 불변식 매핑

루트의 별도 isolated 실행은 `9263756b1d00b8696fddecacb077f55d95141f26`에서
전체 C# 기능 **308/308**, 실패0, exit0으로 끝났다.
[기능 JSON](functional.json)과 [stdout 로그](functional.log)는 scratch 원본 bytes를 그대로 복사했다.
JSON SHA256은 `453d4b2f567f77d14115b9294fdd78187e76808c4ed37f7b4af056dcf5ad2708`이다.

[출처 기록](provenance.json)에 실행 소스/제품·테스트 지문, 실제 네 DLL의 정보 버전·해시와
실행 디렉터리 복사본 일치, clean 재확인, 원본 경로/파일 해시를 담았다.
제품 지문은 `7c61d0bb5cccdb079108d705bc042fa0080932c39667cf1d912e77ed1afcb634`다.

| 파일 | 확인한 범위 |
| --- | --- |
| [매핑 대조](execution-verification.json) | 실제308개 이름·24INV의 명명된143개 증인 연결이 모두 원본 통과 행에 대응 |
| [검증기 자체 시험](validator-self-test.json) | 합성 정상1·잘못된 보고서9/맵8·중복JSON 거절 |
| [갱신 도구 자체 시험](refresh-self-test.json) | 미검토 갱신9개 거절·링크 외 의미 보존 |
| [입력 거절/출처 한계](negative-input-checks.json) | 이전258개 전체·실패305/308·선택32개 거절, 옛56bb 동일 이름만으로 최신 소스를 인증하지 못하는 한계 |

검증기 `--executed-source`는 호출자 선언이다. 테스트본문이 같은 제품-only 변경은 테스트 지문만으로 구분하지 못하므로,
이번 원본 해시·루트의 실제 빌드/실행 출처·제품 지문과 DLL 정보도 별도로 확인했다.
기존 실패·선택·옛 전체 원문을 수정하거나 새 실행으로 승격하지 않았다.

실제 이름·경계·주장·앵커는 [기계 매핑](../invariant-execution-map.json)과 [설명](../invariant-execution-map.md),
변경46개 C# 경로와 C# 지문 외 HTML2의 검토 범위는 [SourceReview](../invariant-execution-source-review-9263756.json)에 있다.
맵 자체의 `execution_status`는 `not_asserted`이며 제안 카탈로그의 `not_implemented` 상태·원장·Notion은 그대로다.

이 근거는 전체308개 C# 기능과 명명된 증인 연결의 통과다. 모든 불변식 조합·111작업 전체·성능/성장·장기 안정성·native·
Windows/Android/API24/35·전체 T05·APK 릴리스 완료를 뜻하지 않는다. 별도 UI 검수는 C# 검사 수에 더하지 않는다.
매핑 작업에서 기능 runner·계측기·프로파일을 새로 실행하지 않았으며 기능 소요 시간을 성능 표본으로 사용하지 않았다.
