# 실제 운송 중 양형식 파일 저장·복원

기존 `InventoryPublicTransportScenario`는 공개 명령의 배송 완료·봉쇄 실패를 확인하지만 운송 중 common codec 저장을 거치지 않았다. 모듈의 `InventoryFixture.RoundTrip`이나 봉쇄 주입용 `GameWorld.Restore`만으로 공통 저장 경계를 통과했다고 기록하지 않고, 새 등록 시나리오 **1개**를 추가했다.

검사 소스는 `af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a`, 테스트를 포함한 제품 소스 지문은 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`다. 기준 `bfd250fa2c62fe4a14a7eb1700bcbd27e663fb95`와 비교해 runtime 제품 소스는 그대로이고 새 테스트 파일만 추가했다. 이전 313개 기능/기준선의 출처를 이 새 소스로 이름 바꾸지 않는다.

Release 빌드 경고·오류 0개, `--filter 'INV-004/017 운송 저장:'` 선택 **1/1 통과**, 종료 코드 0이다. 실제 등록 이름은 `INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패`다. [기능 보고서](functional.json), [실행 로그](functional.log), [빌드 로그](release-build.log), [명령·소스·DLL provenance](provenance.json)를 그대로 보존한다.

| 형식 | 실제 파일 저장·복원 경로 | 실행한 중간 상태 |
| --- | --- | --- |
| JSON 봉투 schema2 | public `GameSaveStore.SaveAtomic` → `Load` | 공개 `possess`→`explore`→`transport`의 열린 운송, 봉쇄된 복귀 운송 |
| 이진 봉투 version3 | 내부 임계값 0의 같은 `GameSaveStore.SaveAtomic` → `Load` | 같은 공개 입력의 독립 작은 세계에서 같은 두 운송 상태 |

각 저장은 실제 파일의 형식과 순번(출발 운송 1, 복귀 운송 2)을 검사하며 `GameCodec.DecodeEnvelope`의 결과와 `GameSaveStore.Load`를 모두 원본과 대조한다. 운송 중 소유권, 원위치 운반자와 실물 위치 표기, 단일 실물/노동 예약, 계약 due, RNG·NextId·전체 상태 hash를 확인한다. 같은 transport 명령은 저장 직후와 정산 뒤 원본/복원본 모두 재전송해 hash 무변화를 확인한다.

원본과 복원본을 같은 due까지 진행하면 실제 배송 완료와 단일 정산·예약 해제가 같다. 이후 공개 복귀 transport가 수락된 뒤 `BlockedUntil = due + 1`만 명시 fixture로 설정한다. 이때의 `GameWorld.Restore` 다음에 **다시 운송 중 실제 파일 저장·복원**을 하고, due의 실패가 실물을 원위치에 남기며 예약을 한 번만 해제하는지 확인한다. 두 형식의 독립 실행도 같은 최종 세계 hash를 만든다. 임시 파일 디렉터리는 `finally`에서 삭제한다.

이는 작은 고정 세계의 기능 검사다. 발송자·운반자·수령인이 같은 실제 시조이고 수수료는 0이며, 제삼자 위탁 동의/수수료 경계를 새로 검사한 것으로 확대하지 않는다. 이 작업에서 추가 전체 기능·성능·성장·대량 인구 실행은 하지 않았다. 진행 중이던 c6 기준선이 끝나 CPU0 사용 허가를 받은 뒤 위 빌드·선택 검사만 순차 실행했다.

상위 PR: #84. 검증 PR: #196. 관련 PR: #192, #194. 관련 이슈: #191.
