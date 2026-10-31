# 지식 정산의 독립 물리 사본과 선택 기능 근거

제품 실행 소스는 `de69ab07e53a395a6bdd2040b4759a6974e4038d`, 기반은 문서 통합 소스
`674329f455fd366640d29ca495ab6b9b5d185eaf`다. 제품 소스 지문은
`979f583103e200e10457497739cdf7f3fa9d2df5c70bf73cb45de1c53a501a09`다.

10,000명 원본926의 실제 측정월 전 예열 진단에서, 매체·접근권만 읽는 `SettleKnowledge`가 전체 지식 이력을 복사하는 경로가 확인됐다.
이번 변경은 제품 두 파일의 **7줄 추가·2줄 삭제**다.

- 내부 `PhysicalCheckpoint(bool includeAccess = true)`는 기존 전체 복사의 매체·접근권 selector와 같은 record/중첩 배열을 독립 복사한다.
- 첫 caller는 `includeAccess:false`의 매체만 읽고, 물리 동기화 뒤 두 번째 caller는 매체와 접근권을 읽는다. 기존 삽입 순서를 유지하며 필터·정렬하지 않는다.
- 공개 `Restore`·`Checkpoint`·전체 `Clone`, 즉시 `SaveKnowledge` 반영, 규칙·출처·RNG·권한은 유지한다. mutable 권위 alias·소유권 이전·월말 지연·큰 이력 cache는 없다.

[원본 소스 대조](source-reference.json)는 공개 전체 복사 구현 파일이 `9263756b1d00b8696fddecacb077f55d95141f26`과
byte 단위로 같고, 실제 기존 두 selector를 그대로 reference로 사용함을 기록한다.

| 실제 선택 | 결과 | 원본 보고서 |
| --- | ---: | --- |
| `knowledge` | 33/33 | [지식·새 사본 격리](knowledge-functional.json) |
| `AI 수행 연결:` | 5/5 | [실제 수행 연결](ai-functional.json) |
| `감응 출처 선택:` | 5/5 | [실제 개인 판본 선택](evidence-functional.json) |
| `입문 준비 조회:` | 3/3 | [개인 조회·명령·저장](query-functional.json) |
| `inventory source observation:` | 3/3 | [실제 유료 관측·재전송](observations-functional.json) |
| `inventory owned reference:` | 2/2 | [기존 고정 세계 전사](oracles-functional.json) |

서로 다른 선택 기능 **51/51**이 통과했다. 기존 고정 상수·검사 본문은 바꾸지 않았다.
새 검사1개는 부분 지식 모듈에서 전체 사본의 두 필드와 모든 값/순서를 동기화 전후 비교한다.
destroyed 매체·만료/철회 접근권·출처/revision을 보존하고, record·중첩 결손 장 배열과 공개 전체 사본을 변조해도
권위·다른 사본·다음 조회·실제 접근권이 유지됨을 검사한다. 이 도메인 JSON 복원은 공통 GameCodec 전체 세계 저장 증거와 구분한다.
실제 공개 세계·권한·저장·재전송은 기존 선택들의 각 본문 범위로 확인한다.

[Release 빌드 원문](build.log)은 경고0·오류0이다. 정확한 네 DLL의 정보 버전/해시와 테스트 소스 지문,
변조하지 않은 보고서 원본 경로/해시·선택 이름 대조는 [출처 기록](provenance.json)에 있다. CPU4에서 기능만 확인했고 기능 ms는 비용 비교 표본으로 쓰지 않는다.

이 작업의 비용·성장·AI 계측·프로파일 실행은 **0회**다. 기존186 맵/143개 증인·원본926의 전체308/308 및 진행 성장9·AI108은
그 실행 소스의 근거로 보존하며 이 새 source의 실측으로 바꾸어 표시하지 않는다.
새 정적 등록은309개이므로 후속 최종 맵에서 새 case와 테스트 지문을 연결해야 한다. 현재 소스의 전체309개·규모 비용·안정성·native 검증은 루트가 별도로 수행한다.

전체 진행률은 저장된 원장 기준 **44.1%(49/111)**이며, 원장·Notion 상태를 변경하지 않았다.
