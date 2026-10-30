# D02·T01~T03 기능 수용 감사 초안

정본은 revision6의 D02/T01/T02/T03 표와 `v021-tasks.json`의 수용 조항이다. 실행 근거는 정확한
`c6e470458ecefe055d98f89120f08c40c0c0660d`(제품 지문 `dcc95b5cb52a0d3b8e6b66975b736a685f4a82250508a2434d29563bd7b9cfc0`)의
[전체313/313 원문](invariant-final-c6e4704/functional.json)이다. 기존 [9704의309/309](invariant-final-9704e95/functional.json)는 당시 원문으로 보존했다.
실제 compiled 이름·파일/행·경계·조항별 증인과 한계는 [기계 감사](acceptance-functional-c6e4704.json)에 있다.
이 문서는 수용 권고이며 원장/status/Notion/readiness를 변경하지 않는다.

| 작업·수용 조항 | 실제 근거와 경계 | 남은 증거·권고 |
| --- | --- | --- |
| D02: 기능별 T07/INV·중간 상태 왕복 | 실제 공개 세계의 잉태·공동수업·제작/경매예치·수행준비·정책준비·비경진척·전투점유·윤회대기에서 GameCodec/후속 진행. `KnowledgeWorldTeachingScenario`, `InventoryOwnedWorldReferenceScenario`, `EntryPreparationSaveScenario`, `PoliticsPublicPolicyIntegrationScenario`, `ExplorationPaidProgressScenario` 등 | 여러 작은 세계의 명명된 범위이며 모든 가능한 동시 중간 상태 조합을 열거한 것은 아니다. |
| D02: 최종 형식·복구·기존 파일 보존 | 새 `LegacySaveEnvelopeCompatibilityScenario`/`LargeSaveEnvelopeRoundTripScenario`는 실제 작은 세계+내부 순번/임계값, `LargeSaveEnvelopeCorruptionScenario`는 byte/codec module, `LargeSaveAtomicRecoveryScenario`와 기존 `HostRecoveryScenarios`는 실제 임시파일 host | **D02 자체 수용 권고 보류**: 알려진 #191 반례의 수정후 실제10,000명 기본 public 저장→읽기/hash·저장크기/형식 확인이 필요하다. 현재4규모 실측 완료를 가정하지 않는다. |
| D02: 운송 중 상태의 정확한 경계 | `InventoryTransportScenario`의 부분 원장 JSON 왕복(module), `InventoryPublicTransportScenario`의 실제 도착/봉쇄 실패(public_world)를 구분 | 해당 공개 운송 검사 자체에는 GameCodec 호출이 없다. 부분 원장 왕복을 전체 세계 저장으로 표기하지 않는다. |
| T01: 인구/자산/부모/유전/권리/예약/source 통합 감사 | 공통 `Invariants.Validate`/`ValidateDomainState`, 실제 창세·출생·잘못된 부모/중복신원 거부, `PeoplePublicMutationPolicyScenario`, 실제 제작/예치/source-free화폐 복원 거부·사용권·정책·교육·유한 보상 증인 | **등록된 기능 범위의 자체 감사 수용 권고 가능**. 313이름/24INV/148명명 증인의 대조이며 모든 불변식 조합·정본111개 전체 완료가 아니다. D02 등 선행은 그대로 남는다. |
| T02: 두 시조→가계/수행/세력/참여 | 정확한 두 반선·종문0 창세, `PeoplePublicFamilyFlowScenario`의 실제 출생/유료양육·전수/동의입양/성년분가/상속, 실제 수행/감응, `PoliticsPublicSectLifecycleScenario`, 빙의/AI 인계 기존 행동 보존 | **작은 실제 세계들의 연결 기능 범위에서 자체 수용 권고 가능**. 유효 law/typed 초기조건을 사용한 여러 시나리오다. 하나의 기본500년 자연 성장 성공·AI 목표 완수로 확대하지 않는다. |
| T02: 조회 hash/RNG 불변 | 240개월 분할/Checkpoint 비교, `RichQueryPermissionScenario`의 실제 View hash·RandomState 불변, `PeoplePublicDetachedDetailScenario`의 중첩 배열 변조 격리, 비경 개인 관측 | C# 조회 검사이며 UI 클릭/native 검수와 별도다. |
| T03: 같은 seed·명령 tick/명령열 | `ExecutionWorldReferenceScenario`+실제 comparator의 고정 tick 명령/출생·취소·무인·저장 경계마다 hash/RNG/이벤트/슬롯 일치, `AgentPlanningReplayScenario`의24개월 저장재개 | **정수 월/명령 경계 자체 수용 권고 가능**. comparator는8명·24개월 기능 전용이다. |
| T03: advance 분할/배속/checkpoint | seed997의240개월 단일/1개월 분할·13개월마다 저장, 문화권·실제 준비/개시·두 봉투2개월 속행. Shell 배속은정수 Months→GameSession→같은 AdvanceTo인 source 구성 | UI timer/장치의 벽시계 속도 실측이 아니다. T02 등 선행과 T04/T05·플랫폼 판정은 별개다. |

새 봉투의3은 **이진 봉투 버전**이다. 상태Schema1/DomainSchema2와 기존 schema2 JSON 바이트/읽기·JSON schema1/3 및 개발 상태 거부 정책은 유지된다.
74/74 저장 선택·독립 세 복사 경로1/1은 [PR192 선택 근거](large-save-envelope-c6e4704/README.md)로 분리했고, 이번313/313 원문으로 최종 이름을 다시 대조했다.
내부 작은 임계값 검사는 실제128MiB 초과·N10,000 성공을 의미하지 않는다. byte[] API의 전체 스냅샷/본문/반환 봉투 메모리와 배열/개별 JSON 값의 상한도 남는다.

926의 실제10,000명 12개월후398,228,383문자 저장 예외/exit134는 [실패 원본](baseline-9263756-save-failure/README.md)으로 보존한다.
성장9/AI108 중단·재시작/복구 이력은 해당926 자료로 판단하며 c6의313개 기능 통과로 고쳐 쓰지 않는다.
초기 감독 래퍼의 지문 JSON 파싱 실패는 게임 빌드/성공 run 시작 전에 발생했고 [별도 원문](invariant-final-c6e4704/supervisor-launch-failure.original.log)으로 분리했다.
매핑·감사 작업에서 C#/scripts/제안카탈로그·원장·Notion 변경과 새 runtime/collector 실행은0이다.
