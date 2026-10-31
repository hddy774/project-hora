# 개인 지식의 기존 권위 인덱스 연결과 선택 기능 검사

실행 소스는 `04908ecc3f3dd30e2b24ba85f84af9dc9d854bf9`, 기반 소스는
`2af63d897eaa3050dd738412121c8d466957d409`다.
제품 소스 지문은 `4be2f9fc0c0092ba9b082d922febeac7cb71ad9d5bdca19e5038077a8c7b67cf`다.

월별 수행 중 개인 지식 조회가 전역 Memories/Mastery를 반복 검색하던 두 helper를
기존 `KnowledgeRuntime().Adopt`와 `FindKnowledge`에 연결했다.
변경은 [World.KnowledgeBridge.cs](../../../../src/Chronicles.Game/World.KnowledgeBridge.cs#L424)의
4줄 추가·9줄 삭제이며, 공개 권위 API·저장 형식·법칙·검사 본문·고정 전사 상수는 바꾸지 않았다.

기존 권위는 복원 시 이력 순서대로 개인 key 인덱스를 만들고, 실제 학습·정정·실습 완료 때 같은 인덱스를 갱신한다.
따라서 두 helper는 같은 인물·work·1장의 마지막 개인 revision, 현재 숙련과 출처를 조회한다.
`SaveKnowledge`의 즉시 반영 시점은 유지하며 월말 지연이나 전역 mutable alias 공유를 도입하지 않는다.

| 실제 선택 | 결과 | 원본 보고서 |
| --- | ---: | --- |
| `knowledge` | 32/32 | [지식 기능](knowledge-functional.json) |
| `AI 수행 연결:` | 5/5 | [실제 수행 AI](ai-functional.json) |
| `감응 출처 선택:` | 5/5 | [혼합 개인 출처](evidence-functional.json) |
| `입문 준비 조회:` | 3/3 | [개인 조회·준비·저장](query-functional.json) |
| `inventory owned reference:` | 2/2 | [기존 고정 세계 전사](oracles-functional.json) |

서로 다른 기존 검사 **47/47**이 통과했다. 보고서 bytes는 scratch 원본에서 그대로 복사했고,
각 선택의 실제 결과 이름 집합을 해당 소스의 등록 이름과 대조했다.
실제 전수·오독/정정·역사 보존·숙련·개인정보 비간섭·저장 재개와 두 고정 세계 전사를 포함한다.
선택의 일부 순수 모듈 검사와 실제 공개 세계 검사의 경계는 각 본문을 따른다.

[Release 빌드 원문](build.log)은 경고 0·오류 0이다. 네 assembly의 정보 버전이 실행 소스와 같으며
실제 DLL 해시·원본 경로/해시·선택·명령·테스트 소스 지문은 [출처 기록](provenance.json)에 있다.
실행은 CPU1에 고정했다. 기능 runner의 소요 시간은 비용 비교 표본으로 사용하지 않는다.

이 패치에서 성능·성장·AI 계측·프로파일 실행은 **0회**다. 이전 `56bbfb2`의 전체308/308과
프로파일 진단은 별도 소스 근거이며 이 패치의 비용 개선이나 최신 전체 통과로 바꾸지 않는다.
전체308개 최종 실행·같은 조건의 비용 비교·플랫폼/릴리스 검증은 루트가 별도로 수행한다.

저장된 진행 문서 기준 전체 진행률은 **44.1%(49/111)**다. 이 변경으로 원장·Notion 상태나
제안 불변식 카탈로그의 `test_status=not_implemented`를 수정하지 않았다.
