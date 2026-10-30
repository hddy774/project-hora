# af91 최종 기능 수용 감사: D02·T01~T03

정본 revision6과 `v021-tasks.json`의 수용 조항을 실제 `af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a`, 제품 지문 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`의 [전체314/314 원문](invariant-final-af91fb0/functional.json)에 대조했다. [조항별 기계 감사](acceptance-functional-af91fb0.json)는 실제 이름·소스·경계·증인과 원장상 선행을 분리한다. 이 문서는 수용 권고이며 원장·readiness·Notion·제안 상태를 바꾸지 않는다.

| 작업·조항 | 실제 근거와 범위 | 자체 권고·남은 판정 |
| --- | --- | --- |
| D02: 기능별 중간 상태와 최종 저장/복구 | 잉태·공동수업·제작/경매예치·수행/정책 준비·비경·전투 점유·윤회 대기의 기존 common codec 증인과 새 운송 중 실제 파일 증인 1개를 전체314에서 확인 | 명명된 기능 저장·복구 조항의 자체 수용을 권고할 근거를 충족한다. 모든 가능한 동시 중간 상태 조합의 전수 실행은 아니다. |
| D02: 운송 중 common 저장 누락 보완 | `InventoryPublicTransportSaveScenario`가 공개 조작/탐색/운송 후 JSON2·작은 임계값 이진3의 실제 SaveAtomic/Load를 각각 거친다. 열린 계약·실물/노동 예약·기한·순번·RNG·NextId·hash, 재전송 무변화, 같은 due의 배송 완료와 봉쇄 실패를 대조 | host + public_world이며 부분 원장 왕복과 구분한다. 발송자/운반자/수령인이 같은 시조, 수수료0인 작은 fixture다. 봉쇄 주입 Restore 뒤 실제 저장을 다시 거친다. |
| D02: 알려진 대규모 저장 반례 | [수정후 c6 네 규모 원문](final-performance/baseline.json)이 2·100·1,000·10,000명 모두 12개월→기본 GameCodec 저장/읽기→상태 hash 확인을 완료했다. 10,000명은 저장 398,228,447B, 최종 상태 hash `68e52eb8543bc480c84c188954eb60c30463b1427e73b323971a97f43f742b80` | c6와 af91의 runtime `src/**`는 바이트 동일하고 새 테스트1개로 제품 지문만 달라졌다. **af91 최종 지문의 population 실측은 진행 중**이며 c6 측정을 af91로 재라벨하지 않는다. 원장 선행/done·최종 T04/T05/릴리스 판정은 별도다. |
| T01: 인구/부모/유전·자산/권리/예약/source | 실제 창세·출생·유전/중복 거부·실물/화폐 원천·예치/교육/정책 비용·유한 보상, 새 운송 실물/노동 예약의 실제 파일 보존/해제를 확인 | 등록된 **314이름·24INV·152개 명명 연결** 범위의 자체 통합 감사 수용을 권고할 근거가 있다. 모든 조합·111작업 전체 완료와 D02 등 선행 마감은 별개다. |
| T02: 두 시조→가계/수행/세력/참여와 조회 순수성 | 실제 출생/양육/교육/입양/성년 분가/상속, 비용·지식을 쓰는 수행/감응, 종문 교육/공헌/승계, 빙의·AI 인계와 권한 조회의 hash/RNG·반환 배열 격리 | 여러 작은 실제 세계의 연결 기능 범위에서 자체 수용 권고 가능하다. 한 기본 세계의 500년 자연 성장 성공·AI 목표 완수·네이티브 UI 조작으로 확대하지 않는다. |
| T03: 같은 seed/명령 tick·advance 분할/배속/checkpoint | 실제 고정 명령/월 경계 comparator, AI24개월 저장 재개, seed997의240개월 단일/분할·주기 저장, 문화권, 두 봉투/운송 파일의 같은 due/hash/RNG를 확인 | 정수 월·명령 경계의 자체 기능 수용 권고 가능하다. 실제 UI timer/장치의 벽시계 배속 실측과 T02 등 선행, 성능·플랫폼 수용은 별도다. |

이진3은 봉투 버전이며 상태Schema1/DomainSchema2·기존 작은 JSON 봉투2의 바이트/읽기·미지원 저장 거부 정책은 그대로다. 작은 임계값 검사는 대량 실행 자체가 아니다. c6 대규모 원문의 `complete:true`와 저장/읽기 통과는 그 소스의 실제 근거이며, 원문이 보존한 상태 hash·저장 크기를 encoded-save 배열의 SHA/바이트 동일성으로 바꾸지 않는다. byte[] API의 전체 스냅샷·본문·봉투 메모리와 배열/개별 JSON 값 상한도 남는다.

이전 [313 감사](acceptance-functional-c6e4704.md)·[313 맵 사본](invariant-final-c6e4704/map.snapshot.json)·[313 원문/provenance](invariant-final-c6e4704/README.md), 309/308 맵, 실제 [305/308 실패](inventory-oracle-final-integration/whole-functional-068ecef.original.json), [926 대량 저장 실패](baseline-9263756-save-failure/README.md)는 수정하지 않았다. 성장/AI 중단·재시작·장기 안정성·플랫폼·릴리스는 각각의 실제 실행 원문으로 판단한다.

이번 감사는 원문·소스·DLL/PE 메타데이터 읽기와 기존 Python 검증만 수행했다. C# 빌드/기능 runner·성능/성장/collector 실행, 제품·테스트·validator·원장·readiness·Notion 변경은0이다. 최종 수용과 선행 마감은 root가 결정한다.
