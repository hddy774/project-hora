# 검토 안내·정확한 실행 범위

## 제품 변경

`GameCodec.Encode`의 JSON 분기만 `EncodeJsonEnvelope`로 연결했다. 같은 canonical UTF8 payload를
16KiB byte segment로 써서 schema2 Payload 문자열의 전체 UTF16 사본을 피한다. 순서는
Format→Schema→Hash→Payload→Sequence이며 기존 Encoder/indent/MaxDepth64·정수 순번·논리 checksum은 같다.
do-loop는 빈 입력도 마지막 segment=true로 닫는다. 출력은 호출 소유 ArrayBufferWriter→ToArray이므로
전체 출력 byte 배열과 최종 복사는 남는다. 저장 길이·escape 팽창 자체를 줄이는 변경은 아니다.

[범위 대조](checks/scope-comparison.json)는 JSON return/private helper/using 추가를 되돌리면
GameCodec 전체가 `5f789c817eb9519b696acd72410643169ff11ef2`와 동일함을 확인한다. Hash/Clone/Decode/
PayloadHash/16MiB limit·binary, Hosting, 원래 plan/harness/twoGC 함수도 바이트 동일하다.

## 실제 기능·바이트 검증

- 실행 source: 깨끗한 로컬 `13b6d1bfd0fea1048158a62755b589aaba47fcb5`, tree
  `b9cd876f8f78098230444ef5c72af3d5b1fc948c`, FP
  `c91c3d7b7cf0441f207825d0490d38501072b63b0364234fbd7cf973b1f620f1`.
- CPU3·SDK10.0.403/runtime10.0.14·TieredPGO=false. 실제4DLL의 Release 정보/source/hash는
  [실행 전 동결 원문](diagnostic/source-freeze.json)에 있다. 이후 문서 추가는 제품을 바꾸지 않는다.
- Release 경고0·오류0. 새2개+기존7개인 **선택9/9** 통과다. 전체 기능 통과로 쓰지 않는다.
- 실제 원래5f Release DLL 네 개를 별도 AssemblyLoadContext로 읽고 원래 Legacy fixture·새 DLL의
  결과와 **12개 전체 bytes**를 독립 대조했다. sequence0/7/상한·en-US/tr-TR·원형/경계 Unicode 모두 같다.
  [비교 원문](original-comparison/result.json)에 원래/새 DLL의 실제 출처도 함께 고정했다.

| filter | 실제 결과 | 범위 |
| --- | ---: | --- |
| `schema2 UTF8 segments:` | 2/2 | 실제 세계의 Unicode/문화권/순번·왕복, 순수 formatter의 빈/16KiB±1·분할 UTF8/escape·property 순서 |
| `대용량 저장:` | 4/4 | 기존 고정 schema2 bytes, 양형식·checksum·순번·재전송/RNG, 손상 거부, 실제 원자 파일 복구 |
| `저장 봉투 기본 경계:` | 1/1 | 실제16MiB 양쪽·기존 큰 JSON Decode·기본 파일 binary |
| `host recovery:` | 1/1 | pending/backup·원문 보존·실제 호스트 재실행 |
| `저장 왕복·호스트 문화권 독립` | 1/1 | 기존 문화권/명령 식별자 저장 호환 |

정확한 명령과 결과는 [verification.json](verification.json), [selected-run.json](checks/selected-run.json)에 있다.
제품 경계·정본·릴리스 보호 self-test·diff whitespace도 통과했다. 새 Name2개의 INV-004/006 맵 후속은
verification.json에 보고했으며 공유 맵/원장/validator는 수정하지 않았다.

최초 빌드의 CA1861은 테스트의 반복 배열을 정적 readonly로 고쳐 해결했다. 독립 비교 probe의 최초
GameEvent constructor 매개변수 수 오타(9→8)도 scratch에서 바로잡았다. 두 실패 원문은 각각
checks/release-build.initial-failed.log와 original-comparison/run.initial-failed.log에 보존했다.
그 후 진단 source/4DLL을 동결했고 실제 성능 진단은 아래1회뿐이다.

## 승인된 N2 진단1회

원래 plan SHA `ec853c715eafc4341fa47802e4de445f0caf165833470d15beb69550905b6c46`를 그대로 사용했다.
N2/120개월·167초·5개 파일 I/O 경계·6개월 불변식·동일 two-GC·동일 process scope다. 같은 CLI 앞선
선택 `INV-001/002/011` 뒤 실행하고, 첫 N2 보고가 쓰이면 외부중단했다. 외부 cap은 승인된200초다.

후보 실제 실행은02:47:29.747670~02:48:10.265522UTC, exit-15, N2 case39.4222047초다.
전체 원문은 `complete:false/status:running/finishedCases:[2]`인 partial로 그대로 보존했다. 뒤 case 보고는0개다.
raw의 case passed는 horizon/시간/기능 flags이며 최종 전체T04 판정이 아니다.

| 월 | 원래5f retained B | 후보 retained B | Hash/saveBytes/flags 등 나머지 checkpoint 필드 |
| ---: | ---: | ---: | --- |
| 0 | 14,550,960 | 14,510,120 | 전부 동일 |
| 30 | 46,122,224 | 20,956,440 | 전부 동일 |
| 60 | 96,717,696 | 29,608,864 | 전부 동일 |
| 90 | 164,093,608 | 46,653,072 | 전부 동일 |
| 120 | 147,548,184 | 46,893,224 | 전부 동일 |

원래 retained 상한47,725,032B를 이번 N2 관측점 모두 충족했고 최대점의 여유는831,808B다.
관측 여유가 크다고 해석하거나 최종 전체T04 통과로 승격하지 않는다. 원래 case39.909593초와 후보39.4222047초는
공유 호스트·고정 순서·beforeCPU0/afterCPU3의 단일 진단이므로 독립 인과 시간 개선율로 쓰지 않는다.

usedLaws·fixture·workload·finalHash도 같고 실제 primary5,027,868B 및 backup4,663,494B가 전후 **바이트 동일**하다.
원래5f의164MB 원형과 새 원문을 [전후 대조](diagnostic/before-after-comparison.json),
[이전 원문](diagnostic/before.original.json), [새 원문](diagnostic/n2-diagnostic.json)에 각각 구분했다.
재실행·예산/GC/plan/fixture 보정·report 후가공은0이다. Decode의 Payload string·반복 검증 비용은 여전히 남는다.
이번 관측만으로 heap root/ArrayPool 원인을 확정하지 않는다.

## 후속 검증과 리뷰

Codex가 구현·바이트 대조·실행 결과를 자체 검토했다. 외부 Claude 검토를 새로 수신했다고 표시하지 않는다.
net10 segment API를 쓰므로 실제 애플리케이션 target 빌드/API24 native CI와 최종 통합 source의 전체
기능·성능 검증은 별도로 필요하다. 이 PR에서 새 원격 수동 실행·성장·전체 기능 실행은 하지 않았다.
기존111개 수용 상태·원래 게이트/예산은 변경하지 않는다.
