# 24개 불변식과 실제 실행 검사의 연결

<!-- actual-map-metadata:begin -->
매핑 소스: `af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a`
테스트 소스 지문: `9fc6ac9beff3d3c598a6449ad86d9fd4f306f1bc1bf288391390f734aa32c117`
<!-- actual-map-metadata:end -->

현재 실제 등록 검사는 **314개**(feature276 + Program38), **24개 불변식·증인152개 연결**이다.
[기계 맵](invariant-execution-map.json)은 실제 이름·파일/행·관련 INV·경계·구체 주장·앵커를 기록하며 실행 상태는 계속 `not_asserted`다.
이름의 INV 태그만으로 수용을 선언하지 않고 제안 카탈로그의 `test_status=not_implemented`·원장·Notion을 변경하지 않는다.

## af91 전체 기능 원문과 소스 검토

정확한 위 소스, 제품 지문 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`의
루트 **전체314/314·실패0·exit0/clean**을 [원본 JSON](invariant-final-af91fb0/functional.json),
[로그](invariant-final-af91fb0/functional.log), [감독 실행 기록](invariant-final-af91fb0/root-run.original.json)으로 보존한다.
JSON SHA256은 `6bb3de5ccb787eeee7a71f86295ce17c9eaa2a630cbef206f95e04beaf312e87`이다.
실제 별도 worktree `/workspace/seonro-final-af91fb0`, CPU0, 2026-10-30 18:45:19.695632~18:50:43.734641 UTC다.
외부 CPU1~3 작업과 공유한 기능 실행이며 합산 시간은 성능 표본이 아니다. [provenance](invariant-final-af91fb0/provenance.json)에
실제 명령·프로세스 기록·네 DLL/정보버전·PE 메타데이터/실행폴더 사본·원문 SHA와 한계를 남겼다.
[기존 검증기 대조](invariant-final-af91fb0/execution-verification.json)는 실제314개 이름과152개 명명 증인을 확인한다.

[소스 검토](invariant-execution-source-review-af91fb0.json)는 c6 이후 새 운송 검사1파일만 변경됐음을 읽고 실제 경계를 연결했다.
새 검사는 **host + public_world**이며 같은 작은 세계의 공개 조작/탐색/운송 후 열린 계약을 JSON2와 작은 내부 임계값의 이진3 실제 파일로 각각 저장/복원한다.
명령 재전송·단일 정산(INV-005), 원본/복원본의 같은 due와 hash/RNG(INV-006), 소유·실물/노동 예약(INV-017), 공통 저장(INV-004)을 구분해 연결했다.
봉쇄 주입용 Restore 뒤 다시 실제 파일 저장을 거치며, 부분 모듈 왕복만을 공통 저장으로 세지 않는다. selfcarrier/fee0 작은 fixture의 범위다.
기존 helper·fixture·제품/HTML 바이트는 c6와 같으며, 모든 기존 보조 앵커를 변경 없는 refresh helper로 다시 확인했다.

## 저장 수용 권고와 남은 최종 판정

[D02/T01~T03 감사](acceptance-functional-af91fb0.md)는 자체 기능 조항과 원장의 선행 마감을 분리한다.
[c6 실제 네 규모 기준선](final-performance/README.md)은 2·100·1,000·10,000명의 12개월 진행과 저장/읽기·상태 hash 확인을 완료했다.
10,000명은 기본 codec에서 398,228,447B를 저장/읽었고, 새 소스와 runtime 제품 바이트는 동일하다.
이 이전 소스 실측과 새 운송 중 파일 기능 근거를 함께 제시하되 **af91 최종 지문의 인구 실측은 진행 중이며 이 맵에서 완료로 세지 않는다**.
따라서 모든24INV 조합·정본111개 전체·최종 T04/T05/장기 안정성·성장/행동·플랫폼·릴리스 완료는 별도다.
작은 내부 임계값 검사는 실제 대량 저장 자체가 아니며, 대량 보고서의 상태 hash/저장 크기를 실제 저장 배열의 SHA/동일성으로 바꾸지 않는다.

## 과거 원본과 거절 경계

이전 [c6의313 원본/provenance](invariant-final-c6e4704/README.md)·[313 JSON 맵 사본](invariant-final-c6e4704/map.snapshot.json)·
[Markdown 사본](invariant-final-c6e4704/map.snapshot.md)·[당시 SourceReview](invariant-execution-source-review-c6e4704.json)·[당시 감사](acceptance-functional-c6e4704.md)는 수정하지 않았다.
사본은 이전 map hash와 byte 동일하며 Markdown 원래 상대 링크는 [당시 경로](https://github.com/hddy774/project-hora/blob/c527c2a47079abc6ba0cc3415af9658371d17efb/docs/evidence/0.2.1/invariant-execution-map.md)를 따른다.
더 이전 [9704](invariant-final-9704e95/README.md)·[926](invariant-final-9263756/README.md)·[305/308 실패](inventory-oracle-final-integration/whole-functional-068ecef.original.json)와
[926 대용량 저장 실패](baseline-9263756-save-failure/README.md)도 당시 source/실패로 보존한다.
[거절 확인](invariant-final-af91fb0/negative-input-checks.json)은 이전313·선택1·합성 미등록315·실패305/308을 현재 전체 근거로 거절한다.
`--executed-source`는 호출자 선언이므로 원문 SHA·실제 run 기록·DLL/제품 지문을 함께 확인해야 한다.

## 읽기 전용 검증과 다음 갱신

```sh
python3 scripts/validate-v021-invariant-execution.py \
  --report docs/evidence/0.2.1/invariant-final-af91fb0/functional.json \
  --executed-source af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a \
  --output /workspace/scratch/invariant-final-recheck.json
python3 scripts/validate-v021-invariant-execution.py --self-test
python3 scripts/refresh-v021-invariant-execution.py --self-test
```

이번 문서 작업은 원본 읽기·기존 Python 검증만 수행했고 C# 빌드/기능 runner·측정기 실행은0이다.
검증기/refresh helper·제품·원장·readiness·Notion·제안 상태는 바꾸지 않았다. Python 합성 검증은 제품 C# 통과 수에 더하지 않는다.
다음 소스에서 실제 이름/fixture/helper/제품이 바뀌면 기존 helper로 변경 계획을 만들고 관련 경계·증인·앵커를 수동 검토한 뒤 같은 출처의 전체 원문으로 대조한다.

## 실제 검사 증인

경계는 `public_world`(GameWorld/실제 명령·월 정산·조회), `module`(순수/부분 도메인·byte codec), `module_and_world`(별도 모듈 비교와 실제 세계),
`mechanics`(수식), `host`(파일/세션/별도 CLI)다. `public_world` 자체가 모든 fixture 구성을 공개 명령으로 만들었다는 뜻은 아니다.
각 증인의 실제 주장과 한계를 따른다. 전체314개 이름의 관련 INV와 경계는 JSON에 있다.

### INV-001 · 도메인별 단일 권위와 안정 신원

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-001 슬롯 재사용·낡은 세대/사망 신원 거부](../../../tests/Chronicles.Game.Tests/Program.cs#L300) | `public_world` / 거절/부정, 공개 명령 | 사망 뒤 슬롯 재사용 시 이전 세대 조회는0이며 죽은 신원의 빙의 명령을 새 사람에게 실행하지 않는다. |
| [INV-004/017/021 H10 action owner: unbacked typed work and forged markers are rejected before completion](../../../tests/Chronicles.Game.Tests/Features/ActionAuthorityScenarios.cs#L48) | `public_world` / 거절/부정, 공개 명령 | 원장 소유가 없는 작업·위조 marker·숫자/enum 별칭을 거절한다. |
| [INV-001/004/006/021/022 H04 dynamic hot columns: original person order, slot generations and lease relocation lifetime](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L22) | `module` / 정상 결과, 거절/부정, 조회 순수성 | 모듈 hot columns의 원래 사람 순서·죽음/이동 원본 참조·lease 중 구조변경 거절·dispose/재배치 뒤 옛 참조 거절·슬롯/가계 보존을 검사한다. 저장 왕복은 이 본문에서 실행하지 않는다. |
| [INV-001/004/006/021/022 H04 saved life boundary: real lifespan death and explicit new arrival reuse no old token](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L195) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 수명 사망과 공개 create 이후에도 원본/새 경로의 월별·저장 결과가 같고 옛 슬롯 세대는 새 인물로 해석되지 않는다. |
| [inventory owned lifetime: 단일 인계·불변 결과·기존 참조 격리와 소비 후 재사용 거부](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedTransactionScenarios.cs#L33) | `module` / 정상 결과, 거절/부정, 조회 순수성 | 부분 모듈의 한 번 인계는 실제 Person 참조를 유지하며 옛 items/원장/재료/능력/장비 참조의 외부 변조가 새 권위를 쓰지 못하고 토큰은 재사용되지 않는다. |
| [inventory sync: 중복 ID·교차 namespace·잘못된 프로필·메타데이터의 기존 초기 거부 우선순위 보존](../../../tests/Chronicles.Game.Tests/Features/InventorySynchronizationScenarios.cs#L71) | `module` / 거절/부정, 재현성 | 부분 모듈 원본의 교차 ID/namespace·프로필·메타데이터 오류에서 공개/소비형 동기화가 같은 초기 거부 순서를 유지하고 원본을 고치지 않는다. |
| [inventory typed copy fields: 전 원장 sentinel·현재 JSON Person defaults·모든 중첩 참조 격리](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedSnapshotScenarios.cs#L164) | `module` / 정상 결과, 거절/부정 | 전 원장 필드 sentinel/nullable/ordinal comparer와 JSON Person defaults를 typed copy와 대조하고 중첩 참조 변조를 격리한다. 합성 모듈 입력이며 공통 codec 저장은 아니다. |
| [inventory validation boundary: 빈 ID·양수·교차 중복·원래 수집 순서·최대 allocator 경계를 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryValidationBoundaryScenarios.cs#L68) | `module` / 정상 결과, 거절/부정 | 부분 원장의 빈/0/교차중복/순서·최대 allocator 경계와 선행 clock 오류를 고정 문자열/원본 불변으로 검사한다. 실제 생사/슬롯 재사용 검사는 아니다. |

범위: 등록한 신원/몸/슬롯·명령 검사 범위다. 모든 미래 참조 유형이나 별도 SpiritFragment 시스템의 전 수명 조합을 증명하지 않는다.

### INV-002 · 100지점·명시 통로·단일 실제 위치

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [비경 공개 명령: 실제 현지 탐색만 입구를 발견하고 일반 자연 cache·100지점과 권위가 분리된다](../../../tests/Chronicles.Game.Tests/Features/ExplorationIntegrationScenarios.cs#L57) | `public_world` / 정상 결과, 공개 명령, 조회 순수성 | 전략 노드는 100개이며 유료 현지 탐색 전 숨은 입구를 보이지 않고 발견을 타인에게 복제하지 않는다. |
| [inventory public: completed route delivers physical cargo and blocked routes keep it at actual origin](../../../tests/Chronicles.Game.Tests/Features/InventoryIntegrationScenarios.cs#L154) | `public_world` / 정상 결과, 거절/부정, 공개 명령 | 실제 완료한 운송만 도착지에 화물을 두고 차단 경로는 원래 위치를 유지한다. |
| [비경 공개 명령: 내부 생활은 실제 휴대한 식량만 쓰고 바깥 바닥 재고나 보호자 식량을 관통하지 않는다](../../../tests/Chronicles.Game.Tests/Features/ExplorationMaintenanceScenario.cs#L3) | `public_world` / 거절/부정, 공개 명령 | 비경 안의 생활·바깥 자녀 부양은 바깥 바닥 물품과 실제 공간을 관통하지 않는다. |

범위: 현재 100지점·명시 이동/화물·비경 하위 공간의 실제 계약이다. 별도 신규 지도/대규모 월드는 이 이름으로 주장하지 않는다.

### INV-003 · 형식화된 법칙과 시행 경계 보존

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-003/005 법칙 일괄 적용·검증 실패 원자성·중복 재전송·플레이 권한](../../../tests/Chronicles.Game.Tests/Features/LawPresetScenarios.cs#L5) | `public_world` / 거절/부정, 공개 명령 | 일괄 법칙은 전체 검증 후 적용하며 실패·재전송·플레이 권한 거절은 상태를 바꾸지 않는다. |
| [INV-003 기존 임신 법칙 revision 보존](../../../tests/Chronicles.Game.Tests/Program.cs#L82) | `public_world` / 정상 결과, 공개 명령 | 잉태 뒤 임신 기간·출산 비활성 법칙을 바꿔도 기존 임신 revision과 출생 결과를 보존한다. |
| [비경 공개 명령: 실제 도구·식량과 채택 법칙·한 번의 위험을 저장하고 월별 진척으로 유한 보상을 선점한다](../../../tests/Chronicles.Game.Tests/Features/ExplorationIntegrationScenarios.cs#L76) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 입장 뒤 바뀐 기간·위험·도구 마모를 현재 입장에 소급하지 않는다. |
| [ecology failure law: 실제 예약 거부 기억의 법칙별 재확인 기간·관측 수명 상한·저장 revision](../../../tests/Chronicles.Game.Tests/Features/EcologyRecoveryIntegrationScenarios.cs#L140) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 중앙 잠정 기간 law의 단위/범위/기본, 실제1/5개월과 관측 수명2개월 상한을 검사한다. 공개 변경/revision/조회/범위 거절/저장 후 실제 예약 실패 기억과 재시도 시점은 법칙을 따르며 무료출력은 없다. |

범위: 양친 금단·혈연 허용·잠정값 개발 보정은 사용자 확정 정책이다. 각 법칙의 모든 수학 조합이나 후속 보정을 통과로 확장하지 않는다.

### INV-004 · 공통 저장 왕복과 중단 복구

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/005 host recovery: committed pending saves, authenticated sequence and immutable old originals](../../../tests/Chronicles.Game.Tests/Features/HostRecoveryScenarios.cs#L6) | `host` / 거절/부정, 저장/재개, 공개 명령 | 정상 pending/primary/backup 우선순위·중단 파일·체크섬 순서와 이전 원본 보존을 실제 호스트 파일로 검사한다. |
| [INV-004/017/018/021 H10 action owner: real consumed production resumes and cancellation never recreates inputs](../../../tests/Chronicles.Game.Tests/Features/ActionAuthorityScenarios.cs#L89) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 소비된 공정의 저장과 취소가 실제 원료를 복원하지 않고 고아 작업·변조 완료시각을 거절한다. |
| [INV-004/005/006/022 EX05 준비 기능: 동일 GameCodec 바이트·선택 압축 원자파일·중단 복구·원본 보존](../../../tests/Chronicles.Game.Tests/Features/ExecutionCandidateCorrectnessScenarios.cs#L11) | `host` / 정상 결과, 거절/부정, 저장/재개 | 두 실제 시조/공개 명령의 같은 GameCodec envelope를 JSON/압축 원자파일 후보로 저장하고 sequence·pending/backup·손상/이전 원본을 보존한다. 실제 손상 Brotli primary의 decoder 오류도 InvalidDataException으로 거절해 backup복구·원문 bytes 보존을 검사한다. sampler/앱 압축 채택 증거는 아니다. |
| [INV-004/006 입문 준비 조회: 토납과 감응을 분리하고 실제 준비·개시 공급과 저장을 유지한다](../../../tests/Chronicles.Game.Tests/Features/EntryPreparationQueryScenarios.cs#L37) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 entry 토납은 Mortal로 남고 별도 준비가 entry 출처를 동결한다. 개시는 실제 영기/공급을 소비하며 준비·개시의 전체 codec 저장과 개인 선택을 보존하고 Qi 공법/숙련을 지급하지 않는다. |
| [inventory projection: Create·Restore·Decode 기본 투영과 strict reference의 실제 작업·저장 재개 동등성](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L38) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | Create/Restore/Decode의 기본 JSON15 factory에서 내부 counter/mode는 저장하지 않는다. 실제 채집을 strict/기본 후보로 완료하여 전체 해시/저장 bytes와 재전송 무소비를 대조한다. |
| [inventory typed integration: instance 선택·Execute 전파·실제 counter와 Restore 기본 JSON 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedIntegrationScenarios.cs#L15) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 typed opt-in/JSON 기본의 작업/해시/저장 bytes를 비교한다. Execute는 instance 선택을 전달하되 Restore/Decode는 JSON15 기본·counter0으로 돌아가고 재전송은 소비를 반복하지 않는다. |
| [INV-004 대용량 저장: 기존 schema2 바이트·고정 원본·구버전 정책 보존](../../../tests/Chronicles.Game.Tests/Features/LargeSaveEnvelopeScenarios.cs#L10) | `public_world` / 정상 결과, 거절/부정, 저장/재개 | 실제 작은 세계에서 수정 전9704 DLL의 고정2개 원본 hash를 독립 기존 encoder로 재현하고, 기존/새 schema2의 실제 bytes·읽기·순번을 대조한다. JSON 봉투 schema1/3과 DomainSchema1은 계속 거절한다. 내부 순번 API를 사용하며 실제10,000명 저장은 아니다. |
| [INV-004 대용량 저장: UTF8 양형식·경계·순번·명령 재전송·RNG 저장 속행](../../../tests/Chronicles.Game.Tests/Features/LargeSaveEnvelopeScenarios.cs#L38) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 작은 실제 세계의 내부 임계값 seam으로 양형식·정확한 길이 경계·UTF8 본문/논리 체크섬·순번7/상한·음수 거부를 대조한다. 공개 possess 재전송과2개월 속행의 hash/RNG·최종 작은 저장 bytes가 같으며 실제128MiB 초과/대규모 성공 근거는 아니다. |
| [INV-004 대용량 저장: prefix·버전·길이·순번·체크섬·잘림·후행 자료 엄밀 거부](../../../tests/Chronicles.Game.Tests/Features/LargeSaveEnvelopeScenarios.cs#L72) | `module` / 거절/부정 | 작은 실제 세계에서 만든 봉투의 byte/codec 부정 검사다. prefix·버전·길이·순번·체크섬·본문·잘림·후행 자료와 올바른 체크섬의 잘못된 JSON을 거절한다. 세계의 실제 명령/시간 진행이나 대규모 저장은 실행하지 않는다. |
| [INV-004 대용량 저장: 실제 원자파일 혼합형식·pending 우선순번·backup·손상 원문 보존](../../../tests/Chronicles.Game.Tests/Features/LargeSaveEnvelopeScenarios.cs#L101) | `host` / 정상 결과, 거절/부정, 저장/재개 | 실제 임시파일 host에서 내부 임계값으로 schema2→이진3→schema2·순번·완전 pending 우선·작은 backup 복구·손상 원문 보존을 검사한다. 다음 순번 overflow 실패가 유효 primary/pending/backup bytes를 덮지 않으며 OS 강제종료/전원상실·실제10,000명 재실측은 아니다. |
| [INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패](../../../tests/Chronicles.Game.Tests/Features/InventoryTransportSaveScenarios.cs#L7) | `host` + `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 공개 운송 중 열린 계약과 봉쇄 복귀 계약을 JSON2/public SaveAtomic 및 작은 임계값 binary3의 실제 파일로 각각 저장/Load한다. owner·운반자 원위치·예약·due·순번/RNG/ID/fullhash와 저장후 배송/실패를 비교한다. selfcarrier/fee0 작은fixture이며 실제대량/제삼자수수료/전원상실 실험은 아니다. |

범위: 열거한 도메인/호스트 저장·손상·중단 복구를 검사한다. 실제 OS 강제 종료·Android 재실행 증거는 별도 플랫폼 검증이다.

### INV-005 · 원자 전이·멱등 처리·단일 종결

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/005/017/018/024 knowledge public world: a paid reserved paper item becomes exactly one copied book](../../../tests/Chronicles.Game.Tests/Features/KnowledgeIntegrationScenarios.cs#L160) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 같은 실제 필사 명령·완료를 재전송해도 단일 책과 실제 소비만 남긴다. |
| [combat public: command replay paid cooldown and saved continuation preserve actual receipts and query purity](../../../tests/Chronicles.Game.Tests/Features/CombatIntegrationScenarios.cs#L172) | `public_world` / 거절/부정, 저장/재개, 공개 명령, 조회 순수성 | 공격 재전송·같은 시각 취소·저장 재개가 이미 지급한 체력·시간과 전투 영수증을 복제하지 않는다. |
| [비경 공개 명령: 동시 실제 입장과 같은 시점 완료는 유한 원천을 한 번만 선점하고 다시 입장해도 복제하지 않는다](../../../tests/Chronicles.Game.Tests/Features/ExplorationIntegrationScenarios.cs#L129) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 동시 완료·재입장과 예전 소비 영수증 재사용으로 유한 선점을 복제하지 않는다. |
| [inventory commitment reference: 앞 원천 오류·뒤 입력 오류·long overflow의 기존 거부 순서를 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryCommitmentValidationScenarios.cs#L117) | `module` / 거절/부정, 조회 순수성, 재현성 | 원천·입력·노동·overflow·null 입력이 겹치는 부분 모듈의 기존 최초 오류와 입력 불변을 고정 참조와 비교한다. 새로운 오류 형식을 통과로 추정하지 않는다. |
| [inventory owned failure: 두 번째 처리·판매 실패와 예외가 전체 후보·ID·재료를 폐기](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedTransactionScenarios.cs#L57) | `module` / 거절/부정 | 부분 모듈의 두 번째 공정/판매 실패·재료 부족·예외/잘못된 연결은 전체 미인계 후보와 ID/자원을 폐기하고 닫힌 토큰 재사용도 거절한다. |
| [inventory owned parity: 공개 복사 경로와 예약·처리·전용 연결·재전송 결과 및 직렬화 일치](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedTransactionScenarios.cs#L127) | `module` / 정상 결과, 거절/부정, 재현성 | 공개 복사 경로와 소비형 후보의 실제 명령/재전송·예약/소비·작업 연결·최종 직렬화 결과가 같고 기존 참조를 분리한다. 일반 GameCodec 저장 재개로 부르지 않는다. |
| [inventory owned rejection: ID 충돌·빈 ID·오버플로와 기존 오류 우선순위 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedTransactionScenarios.cs#L163) | `module` / 거절/부정, 재현성 | 부분 모듈의 빈/충돌 명령ID·overflow·잘못된 수량에서 공개/소비형 경로의 최초 거부·응답·입력 불변을 비교한다. |
| [inventory owned stale: 월·할당자·개정·실물·사람·재료 지문과 원래 세계 인계 경계](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedTransactionScenarios.cs#L103) | `module` / 거절/부정, 재현성 | 월/할당자/revision/실물/돈/재료가 바뀌면 오래된 후보는 최신 권위를 덮지 못하며 내용이 같은 다른 세계에도 인계하지 못한다. |
| [inventory sync: 공개 custom comparer와 내부 Ordinal 영수증의 대소문자 구분·재전송·충돌 보존](../../../tests/Chronicles.Game.Tests/Features/InventorySynchronizationScenarios.cs#L96) | `module` / 정상 결과, 거절/부정, 재현성 | 부분 모듈의 외부 custom comparer에서도 실제 영수증은 Ordinal ID 구분·같은 요청 재전송·같은 ID 다른 내용 거부를 보존한다. |
| [inventory source observation: 빈 Data는 기존 첫 미관측 선택을 유지하고 명시 정의는 fallback하지 않음](../../../tests/Chronicles.Game.Tests/Features/InventorySourceObservationScenarios.cs#L48) | `public_world` / 정상 결과, 거절/부정, 공개 명령 | 명시 빈 원천 순서 fixture에서 빈 Data는 첫 미관측 선택을 유지한다. 명시 food의 불일치/원격/없는 정의/ID는 fallback하지 않고 실제 명령이 원자 거절된다. |
| [inventory source observation: 현지 정의 부재·실물 비용 부족은 원천·돈·시간·ID·관측 무변화 거부](../../../tests/Chronicles.Game.Tests/Features/InventorySourceObservationScenarios.cs#L68) | `public_world` / 거절/부정, 공개 명령 | 현지 원천 부재·실물 관측 비용 부재 경계의 공개 명령은 전체 상태 불변으로 거절된다. 부족 식량은 명시 Loss fixture로 구성하며 원천/돈/시간/ID/RNG/관측을 무료 변경하지 않는다. |
| [inventory projection: 15개 입력별 stale와 전체 Person strict 경계·미사용 변화 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L91) | `module` / 정상 결과, 거절/부정 | 부분 재고 fixture의 각15 입력/외부 권위 변화는 stale 거절하고 원본을 유지한다. 미사용 변화는 full/strict가 거절하지만 투영 인계가 원본 개인 변화를 덮어쓰지 않는다. Clone은 공통 저장 증거가 아니다. |
| [inventory projection: mutable 참조·재사용·예외·두 번째 처리 및 소비 후 실패의 원자성](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L182) | `module` / 정상 결과, 거절/부정 | 부분 후보 소비/예약 후 인계가 Person 동일성과 중첩 참조 격리를 유지한다. 두번째 처리·소비 후 실패·예외·다른세계 인계/재사용이 원본·실물·ID·원장을 유출하지 않는다. |
| [inventory typed copy reference: 실제 후보 JSON 형태·ordinal·검증·원본 격리 일치](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedSnapshotScenarios.cs#L193) | `module` / 정상 결과, 거절/부정 | 부분 typed 후보가 JSON 형태/지문/ordinal과 같고 원본 참조를 노출하지 않는다. 수량0/allocator stale/음수 돈의 기존 검증 오류도 같으며 인계 전 원본은 불변이다. |
| [inventory typed integration: opaque 전이·영수증·재인계·실패와 동일15필드 stale 계약](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedIntegrationScenarios.cs#L40) | `module` / 정상 결과, 거절/부정 | 부분 JSON/typed 후보의 동일15 stale/opaque 전이·영수증·예약/생산/소비/한번인계를 비교한다. 두번째 처리 실패는 allocator/실물/원장을 유출하지 않는다. Clone을 공통 저장으로 세지 않는다. |
| [INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패](../../../tests/Chronicles.Game.Tests/Features/InventoryTransportSaveScenarios.cs#L7) | `host` + `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 원본/복원본 모두 같은 실제 운송 명령을 운송중과종결후 재전송해 hash불변과같은응답을 확인한다. 배송/봉쇄실패 정산event가한번이고 escrow0·예약해제가 같다. 모든동시명령/위탁당사자조합을 전수탐색한것은 아니다. |

범위: 등록한 명령/원인 키·취소·동시 선점의 실제 순서다. 가능한 모든 같은 tick 조합을 전수 탐색했다는 의미는 아니다.

### INV-006 · 엔진 독립 실행과 재현 가능한 진행

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-006/007/004 시간 분할·조회/저장 무영향](../../../tests/Chronicles.Game.Tests/Program.cs#L111) | `public_world` / 정상 결과, 저장/재개, 재현성, 조회 순수성 | 같은 seed의 일괄/월별 advance·주기적 GameCodec 재개·중간 조회가 같은 권위 해시를 만든다. |
| [INV-006 저장 왕복·호스트 문화권 독립](../../../tests/Chronicles.Game.Tests/Program.cs#L286) | `public_world` / 정상 결과, 저장/재개, 재현성 | 호스트 문화권 변경 뒤에도 명령 식별자와 저장 해시를 보존한다. |
| [INV-004/006 agent planning: identical real goal commands and save/resume preserve choices receipts and world hash](../../../tests/Chronicles.Game.Tests/Features/AgentPlanningIntegrationScenarios.cs#L170) | `public_world` / 정상 결과, 저장/재개, 재현성, 공개 명령 | 같은 실제 목표 명령과 저장/분할 진행이 선택·실물 영수증·해시를 보존한다. |
| [INV-001/004/006/021/022 H04 world reference: actual commands, birth, cancellation, martial and save boundaries match every month](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L151) | `public_world` / 정상 결과, 저장/재개, 공개 명령, 재현성 | 같은 실제 잉태/탐색/여행/취소/무도 명령을 원본과 hot columns/기한 힙 경로에 보내 매월 hash·RNG·사건·생사·슬롯과 저장 재개를 비교한다. |
| [INV-004/006/022 EX04 functional input: actual scalar vector partial backend selection preserves full aptitude and world hash](../../../tests/Chronicles.Game.Tests/Features/ExecutionLifecycleFixtureScenario.cs#L24) | `public_world` / 정상 결과, 거절/부정, 조회 순수성, 재현성 | 실제 PeopleAptitude 경로의 scalar/vector/부분 실패 backend와 cache hit가 전체 재능·인지·권위hash를 보존한다. 계측이나 바이트 저장 왕복으로 세지 않는다. |
| [inventory owned reference: 기존 공개 제작·보관·거래·저장·명령 재전송 결과 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedReferenceScenarios.cs#L5) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 초기 실물을 둔 실제 세계의 제작·장비/결속·피격·수리·가방/거래·경매·거부/재전송·저장·월 경계를 현행 소스에서 선보존한 독립 strict 고정 전사와 비교한다. 예전751/169 버그 전사 해시 보존은 주장하지 않는다. |
| [INV-004/006/008 감응 출처 선택: 실제 전수 Qi 우선·조회 무변화·동결 저장·단일 해소 난수](../../../tests/Chronicles.Game.Tests/Features/CultivationPreparationEvidenceScenarios.cs#L131) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 실제 전수/토납 후 더 나은 개인 Qi 판본을 동결한다. 자기 조회/준비는 권위·RNG 불변이고 실제 해소만 정확히 한 샘플을 소비하며 저장·형성에 work/edition/revision/품질을 보존한다. |
| [inventory projection: 실제 공개 명령·생계 AI가 기본 factory를 거쳐 기존 두 전체 transcript와 저장 속행 일치](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L28) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 수동/명시 정책잉여 두 실제 세계 전사를 JSON15 factory로 실행해 현행 소스의 선보존 strict 고정값·명령/월/저장 결과를 검사한다. 예전751/169 버그 전사와 같은 해시라는 주장이 아니다. |
| [inventory typed copy transcript: 현재 JSON 후보와 실제 예약·생산·소비·장비·거부·재전송 연결](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedSnapshotScenarios.cs#L223) | `module_and_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 부분 JSON/typed 후보의 예약·소비·생산·장비·거절/재전송 응답과 상태를 비교하고 원본에는 인계하지 않는다. 별도 실제 공개 수동 전사/저장을 호출하며 그 호출은 typed 기본 채택 증거가 아니다. |
| [inventory typed integration: 실제 typed factory를 통과한 두 공개 전체 transcript·저장 속행 일치](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedIntegrationScenarios.cs#L5) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 opt-in typed15로 두 독립 실제 세계 전사를 실행해 같은 소스 strict 고정 기준과 명령·실물·저장 결과를 검사한다. 일반 앱의 typed 채택/성능 통과를 뜻하지 않는다. |
| [inventory copy oracle: 현행 규칙의 strict·JSON15·typed15 독립 전체 전사·실제 저장 bytes 동등](../../../tests/Chronicles.Game.Tests/Features/InventoryCopyPolicyEquivalenceScenario.cs#L3) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 두 실제 세계를 strict/JSON15/typed15로 각각 독립 생성해 선보존 같은 소스 고정값·실제 다른 factory counter·전체 Rows·실제 저장 bytes를 대조한다. 옛751 해시 보존/typed 기본 채택은 주장하지 않는다. |
| [INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패](../../../tests/Chronicles.Game.Tests/Features/InventoryTransportSaveScenarios.cs#L7) | `host` + `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 같은 공개 입력의 원본/복원본을 같은 운송 due까지 진행해 전체hash/RNG를 비교하고 JSON2/이진3 독립실행 최종hash도 대조한다. 실제 장치/대량 성능이 아닌 작은세계 정수월 경계다. |

범위: 현재 C# 원본/실제 hot columns·기한 큐·scalar/Vector 경계의 작은 명령·월별·저장·문화권 재현이다. 실제 등록의 기능 비교와 최종 규모별 시간/할당 실측을 구분하며 병렬 worker·Unity·네이티브 실행으로 확대하지 않는다.

### INV-007 · 관찰 순수성과 관찰 여부 독립

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-007/010 rich query: action equipment policy history mapping and detached snapshots](../../../tests/Chronicles.Game.Tests/Features/QueryIntegrationScenarios.cs#L67) | `public_world` / 정상 결과, 조회 순수성 | 조회와 분리된 반환값을 변경해도 권위 상태가 변하지 않으며 장비·정책·이력이 실제 원천에 대응한다. |
| [비경 공개 명령: 실제 현지 탐색만 입구를 발견하고 일반 자연 cache·100지점과 권위가 분리된다](../../../tests/Chronicles.Game.Tests/Features/ExplorationIntegrationScenarios.cs#L57) | `public_world` / 정상 결과, 조회 순수성 | 입구와 인물 조회는 세계/RNG를 바꾸거나 타인의 발견을 생성하지 않는다. |
| [INV-004/009 people queries public: mutating nested observer aptitude, traits, soul, evaluations and claim arrays cannot alter authority](../../../tests/Chronicles.Game.Tests/Features/PeopleReceiptIntegrationScenarios.cs#L68) | `public_world` / 거절/부정, 조회 순수성 | 중첩 재능·특성·혼흔·평가·주장 배열을 변경해도 권위를 쓸 수 없다. |
| [INV-007/024 knowledge physical snapshot: 전체 사본의 매체·접근권과 순서·동기화 시점·중첩 배열 격리가 같다](../../../tests/Chronicles.Game.Tests/Features/KnowledgePhysicalSnapshotScenarios.cs#L6) | `module` / 정상 결과, 거절/부정, 조회 순수성, 저장/재개 | 부분 지식 모듈에서 공개 전체 사본의 두 필드와 독립 투영의 모든 값/순서·동기화 전후·destroyed/만료/철회 기록을 비교한다. record/중첩 배열·공개 전체 사본 변조가 권위/다른 사본/권한 조회로 유출되지 않고 도메인 JSON 복원도 같다. 공통 GameCodec 전체 세계 저장·공개 GameWorld 명령 증거는 아니다. |

범위: 실제 공개/모듈 조회와 분리된 반환값의 순수성이다. 모든 UI 그래프·플랫폼 화면 동작은 C# 보고서만으로 대체하지 않는다.

### INV-008 · 개인 인지 경계와 숨은 변화 비간섭

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-006/008 agent planning: unchanged personal information produces the same choice despite hidden NPC inventory knowledge and wealth](../../../tests/Chronicles.Game.Tests/Features/AgentPlanningIntegrationScenarios.cs#L123) | `public_world` / 거절/부정, 재현성 | 개인 입력이 같은 세계의 숨은 NPC 재고·지식·부 차이는 새 관측 전 선택과 예정시각을 바꾸지 않는다. |
| [INV-008/024 agent planning: an incorrect source belief is rejected by actual execution then replaced through observation](../../../tests/Chronicles.Game.Tests/Features/AgentPlanningIntegrationScenarios.cs#L100) | `public_world` / 거절/부정, 공개 명령 | 잘못된 원천 믿음은 실제 실행에서 거절되고 새 실제 탐색 관측으로 교체된다. |
| [INV-008/009/015 people receipts public: remote rejection and hidden authority cannot change an undelivered request belief](../../../tests/Chronicles.Game.Tests/Features/PeopleReceiptIntegrationScenarios.cs#L42) | `public_world` / 거절/부정, 공개 명령 | 전달되지 않은 원격 거절·권위 변화로 개인 요청 믿음이 갱신되지 않는다. |
| [INV-004/008/021 agent planning: actual search and source cognition budgets are bounded and a deferred goal resumes after save](../../../tests/Chronicles.Game.Tests/Features/AgentPlanningIntegrationScenarios.cs#L148) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 실제 탐색 예산과 원천 인지 예산을 지키고 저장한 지연 목표를 다음 결정에서 재개한다. |
| [inventory owned reference: 명시 거래 성향과 비축3 경계의 실제 분할·판매·저장 결과 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryOwnedReferenceScenarios.cs#L65) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 거래 성향·식량8/월소비1/비축3 경계에서 실제 잉여를 분할/출품하고 실제 구매/저장을 선보존 현행 strict 상수와 비교한다. 개선 전 버그 전사 보존으로 주장하지 않는다. |
| [INV-006/008/021 AI 수행 연결: 숨은 타인 재고·지식 변경은 자기 입문 선택·시간·난수를 바꾸지 않는다](../../../tests/Chronicles.Game.Tests/Features/AgentCultivationIntegrationScenarios.cs#L227) | `public_world` / 거절/부정, 공개 명령, 재현성 | 자기 입문 정보가 같은 두 실제 세계에서 숨은 교사의 추가 재고/지식만 달라도 행동·기한·결정 기록·RNG와 채택 개인 entry 출처는 같다. |
| [ecology recovery: 믿음 예산1의 실제 채집 예약 거부와 즉시 퇴출은 무예외·권위·저장 결정론 유지](../../../tests/Chronicles.Game.Tests/Features/EcologyRecoveryIntegrationScenarios.cs#L107) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 믿음 예산1의 실제 예약 거부는 상태/RNG 불변이며 즉시 퇴출한 실패를 되살리지 않는다. 타인 예약·유한 원천·노동·저장 재개·개인 인지량1·실제 경로 탐색을 검사한다. |
| [ecology recovery: 식량 관측 전 숨은 잔량은 선택에 영향 없고 비용 없는 조사·총자원 추정 금지](../../../tests/Chronicles.Game.Tests/Features/EcologyRecoveryIntegrationScenarios.cs#L195) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 같은 총량/개인정보에서 숨은 식량 구성이 달라도 유료 관측 전 조회/선택/기한/RNG는 같다. 실제 비용/한달 뒤 관측만 차이를 확인하며 비용/농사 출처 없는 인물에게 무료 정보/출력/지식을 주지 않는다. |
| [inventory projection: 모든 미사용 필드의 실제 명령 비간섭과 개인 원천 관측·물리 ledger 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L152) | `module` / 정상 결과, 거절/부정 | 부분 입력의 모든 미사용 개인 필드·일지·계획을 바꾸어도 실제 분할 응답/상태와 원본 개인값·참조가 같다. 자기 관측을 유지하고 타인 관측을 합치지 않는다. 실제 GameWorld/전체 codec 저장은 아니다. |
| [INV-004/008/024 people teaching public: AI missing-method requests use the same actual knowledge adapter and source as player requests](../../../tests/Chronicles.Game.Tests/Features/PeopleTeachingIntegrationScenarios.cs#L3) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 미학습 AI의 요청 work가 같은 공개 requestTeaching 어댑터와 일치한다. source는 실제 plan/결정/영수증과 같고 기다림 한달·바쁜 교사의 독립 행동·저장/RNG를 보존하며 요청만으로 지식·완료 학습을 지급하지 않는다. |

범위: 명시한 숨은 상태 비간섭·출처·실패 수정·논리 예산의 기능 검사다. 인구 단계별 메모리/저장 측정과 장기 행동 분포는 최종 별도 단계다.

### INV-009 · 권한 우선 조회와 표시 비간섭

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-009/024 rich query: knowledge provenance is personal and hidden revision independent](../../../tests/Chronicles.Game.Tests/Features/QueryIntegrationScenarios.cs#L132) | `public_world` / 거절/부정, 조회 순수성 | 숨은 NPC 지식 진척·전역 revision이 자기 개인 지식 투영을 바꾸지 않는다. |
| [INV-009/024 knowledge: 허가한 실제 매체를 전체수·정렬·페이지 전에 필터하여 바깥 도서를 인지하지 않는다](../../../tests/Chronicles.Game.Tests/Features/KnowledgeScenarios.cs#L7) | `module` / 거절/부정, 조회 순수성 | 허가 매체 집합을 전체수·정렬·페이지 전에 적용하여 바깥 미허가 도서가 분모와 첫 페이지에 영향을 주지 않는다. |
| [INV-008/009/010 관찰→플레이·숨은 상태 비간섭·ID 우회](../../../tests/Chronicles.Game.Tests/Program.cs#L243) | `public_world` / 거절/부정, 공개 명령, 조회 순수성 | 미발견 ID·플레이 법칙 명령을 거절하고 같은 인지에서는 숨은 값 차이가 표시를 바꾸지 않는다. |
| [INV-007/008/009/010/016/024 EX06 준비 기능: 권한 선필터 scan/index/cache·숨은 변화 비간섭·실제 장비 무효화](../../../tests/Chronicles.Game.Tests/Features/ExecutionCandidateCorrectnessScenarios.cs#L17) | `public_world` / 정상 결과, 거절/부정, 공개 명령, 조회 순수성 | 실제 개인 World.View의 선권한 DTO를 scan/index/cache로 검색하여 행·전체수·page를 맞춘다. 타인 split은 같은 입력을 유지하고 실제 자기 장비는 캐시를 무효화하며 닫힌 요청은 조회/행을 만들지 않는다. |
| [INV-009/010 입문 준비 조회: 실제 entry 전수만 감응 선택을 열고 Qi 공법은 만들지 않는다](../../../tests/Chronicles.Game.Tests/Features/EntryPreparationQueryScenarios.cs#L5) | `public_world` / 정상 결과, 거절/부정, 공개 명령, 조회 순수성 | 미학습자 선택0, 실제 entry 전수자의 감응 operation만 표시하고 미학습 Qi 정의/지식·현장 공급/권리/품질은 숨긴다. 불충분 준비 명령은 상태/RNG 불변 거절이다. browser/native 클릭 증거와 구분한다. |
| [INV-009 입문 준비 조회: 미관측 현재 공급·사용권 변화는 개인 감응 선택을 바꾸지 않는다](../../../tests/Chronicles.Game.Tests/Features/EntryPreparationQueryScenarios.cs#L74) | `public_world` / 거절/부정, 조회 순수성 | 같은 개인 entry 정보에서 미관측 현장 영기·소유/운영·봉쇄만 바꾸어도 개인 Potential은 같고 조회는 권위/RNG를 바꾸지 않는다. 미관측 site preflight 통과를 뜻하지 않는다. |

범위: 등록한 실제 개인 DTO·미발견 ID·허가 매체·토큰 경계다. 순수 모듈 조회와 실제 전체 GameWorld 응답을 구분한다.

### INV-010 · 조회 세대·시점·표시 의미 보존

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-008/009/010 관찰→플레이·숨은 상태 비간섭·ID 우회](../../../tests/Chronicles.Game.Tests/Program.cs#L243) | `public_world` / 정상 결과, 거절/부정, 공개 명령 | 관찰/빙의 전환은 조회 세대와 권한 상세를 실제로 갱신한다. |
| [INV-009/N03 rich query: real private NPC split preserves actor perception token and valid command context](../../../tests/Chronicles.Game.Tests/Features/QueryIntegrationScenarios.cs#L244) | `public_world` / 거절/부정, 공개 명령, 조회 순수성 | 숨은 NPC의 실제 분할 명령은 자기 표시 토큰을 바꾸지 않고 유효 명령 문맥을 유지한다. |
| [INV-010/021 빙의·AI 인계에서 기존 행동 보존](../../../tests/Chronicles.Game.Tests/Program.cs#L227) | `public_world` / 정상 결과, 공개 명령 | 모드 전환·AI 인계는 접수한 현재 행동과 완료 시각을 취소하지 않는다. |

범위: C# 명령/조회 세대·개인 표시 토큰과 행동 보존을 검사한다. 늦은 웹 응답·실제 Android 전환·캐시/내보내기 UI는 호스트/브라우저의 별도 실행 근거가 필요하다.

### INV-011 · 실제 창세·생식·생애·인구 원장

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-001/002/011 정확한 창세·안정 신원·100지점](../../../tests/Chronicles.Game.Tests/Program.cs#L49) | `public_world` / 정상 결과, 메타 계약 | 기본 창세는 정확히 두 반선·종문0·100지점이며 초기 자산 출처를 기록한다. |
| [INV-004/006/011/014 people public: frozen conception, actual birth soul and production-save continuation](../../../tests/Chronicles.Game.Tests/Features/PeopleIntegrationScenarios.cs#L71) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 실제 잉태·출생에서 새 몸·단일 혼을 배정하고 동결한 초기 유전을 저장 재개한다. |
| [INV-011/012 people public: both-parent Golden Core boundary and explicit martial-law override](../../../tests/Chronicles.Game.Tests/Features/PeopleIntegrationScenarios.cs#L114) | `public_world` / 거절/부정, 공개 명령 | 양친 모두 금단이라는 기본 경계와 명시적 무도 법칙 예외만 실제 임신에 적용한다. |
| [INV-001/004/006/011/022 EX02 functional input: actual frozen conception, father death, reused slot and birth without measurement](../../../tests/Chronicles.Game.Tests/Features/ExecutionLifecycleFixtureScenario.cs#L6) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 작은 명시 EX02 경계에서 실제 잉태·아버지 수명 사망·새 명시 편입의 슬롯 재사용·단일 출생/생물학 연결을 원본/새 실행의 저장 경계로 비교한다. 계측 runner는 호출하지 않는다. |

범위: 정확한 창세와 실제 공개 출산·종족/성숙 경계를 검사한다. 큰 인구의 장기 원장/성장 측정은 실행하지 않은 별도 단계다.

### INV-012 · 유전·가계·교육·상속의 계승 경계

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-001/004/006/012/017/022 people family public: paid care, real teaching, consented adoption, adult separation and estate versus office succession](../../../tests/Chronicles.Game.Tests/Features/PeopleFamilyIntegrationScenarios.cs#L8) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령 | 실제 양육·양자 동의·생물학 DAG·교사/입양자·성년 분가와 재산/채무 상속·직책 승계를 분리한다. |
| [INV-004/011/012 people public genetics: configured zero mutation and frozen model/stream after law changes](../../../tests/Chronicles.Game.Tests/Features/PeopleSocialIntegrationScenarios.cs#L224) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령 | 변이 확률0/크기0과 잉태 뒤 법칙 변경은 동결 유전 모델·선택·몸 원형을 소급 재추첨하지 않는다. |

범위: 동결 유전·생물학 DAG·유료 교육/양육·상속/직책의 실제 공개 흐름과 모듈 경계를 구분한다. 예외 희귀 원형의 자연 발생률을 증명하지 않는다.

### INV-013 · 윤회 자격 증거와 단일 여정

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-013 혼멸·역사 최고 경지로 자격 우회 없음](../../../tests/Chronicles.Game.Tests/Program.cs#L271) | `public_world` / 거절/부정, 공개 명령 | 혼멸·현재 범인 상태는 과거 반선 이력으로 윤회 자격을 우회하지 못한다. |
| [INV-013/014 화신 대원만 경계·실제 사망·대기·출생](../../../tests/Chronicles.Game.Tests/Program.cs#L196) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령 | 화신 대원만 바로 아래/같음 경계와 실제 사망 후 혼의 대기/종료를 검사한다. 실제 출생은 별도 공개 출생 검사가 맡는다. |
| [RC-13 실제 원인 생산자·자동 Gate·여정 왕복](../../../tests/Chronicles.Game.Tests/ReincarnationScenarios.cs#L9) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 13번째 실제 비승 원인 생산자는 새 분혼의 단일 여정·대기 상태와 저장 왕복을 검사한다. |
| [INV-004/005/006/013 윤회 공개 동시 원인: 실제 12·7의 단일 우선 선택과 활성·완성 경계·대체 설명·안정 키를 저장하고 재처리한다](../../../tests/Chronicles.Game.Tests/Features/ReincarnationCauseIntegrationScenario.cs#L3) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 공개 law→실제 월 경계의 동시12/7 중 활성·적격 하나를 선택하고 실제 이탈/사망과 분리한다. 비활성·미완성·복구 도구·건강한 혼약 경계, 대체 설명·안정 body/tick 키·단일 사건·저장/재처리·RNG를 검사한다. |

범위: 13개 실제 원인 생산자와 동시 world-condition12/7 및 활성/완성·복구도구·혼약 경계를 검사한다. 대체 실제 원인·안정 키·한 여정은 새 공개 사례로 보강했다. 발생하지 않은 미래 파단/전투를 잠재 원인으로 만들지 않으며 모든 가능한 환경 조합을 전수 검사했다고 주장하지 않는다.

### INV-014 · 분혼 정체성·출생 배정·흔적 단계

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-014/024 people public: coherent ready ascension, source capture and costed independent split](../../../tests/Chronicles.Game.Tests/Features/PeopleIntegrationScenarios.cs#L190) | `public_world` / 정상 결과, 거절/부정, 공개 명령 | 준비된 실제 비승·비용 있는 독립 분혼은 근원과 새 SoulId를 분리하며 수행 분령을 출생 후보로 복제하지 않는다. |
| [INV-004/014/024 people public: eligible natural death, waiting-soul birth, real awakening and taught integration](../../../tests/Chronicles.Game.Tests/Features/PeopleIntegrationScenarios.cs#L227) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 실제 죽음→대기 혼 출생→각성·유료 전수로 흔적의 접근/통합을 구분한다. |
| [INV-004/014/024 people: six trace domains, awakening, integration, crossing loss and conserved split branches](../../../tests/Chronicles.Game.Tests/Features/PeopleScenarios.cs#L164) | `module` / 정상 결과, 거절/부정, 저장/재개 | 여섯 흔적의 보존·각성·통합·손실과 분혼 분기 보존을 모듈 단위로 검사한다. |

범위: 실제 출생 배정·비승 분혼·여섯 흔적의 단계와 비용을 검사한다. 태내 미배정 영혼 효과를 추정하거나 수행 분령을 독립 혼으로 간주하지 않는다.

### INV-015 · 효과 원천 단회 반영과 형성·현재·잠재 분리

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-006/015 people: six foundations, 24 talents, canonical ten cognition mappings and single cause effects](../../../tests/Chronicles.Game.Tests/Features/PeopleScenarios.cs#L43) | `module` / 정상 결과, 거절/부정, 저장/재개 | 유전·혼흔·특성이 같은 원인이면 단회 반영하고 현재/선천/후천·24재능을 분리한다. |
| [combat mechanics: each canonical axis changes practical hit damage escape coordination or finite exertion](../../../tests/Chronicles.Game.Tests/Features/CombatIntegrationScenarios.cs#L40) | `mechanics` / 정상 결과, 재현성 | 표시값뿐 아니라 실제 명중·피해·회피·협조·유한 소모에서 8축의 의미를 검사한다. |
| [INV-004/015/023 regression·real recovery·costed route conversion·retreat boundaries](../../../tests/Chronicles.Game.Tests/Features/CultivationScenarios.cs#L288) | `module` / 정상 결과, 거절/부정 | 퇴행·회복·유료 경로 변환이 실제 기여를 중복 전달하거나 과거 완성 보상을 재발행하지 않는다. |
| [INV-004/006/022 H09 real talent kernel: scalar/vector boundary, whole output, partial failure, stale and duplicate apply](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L87) | `module` / 정상 결과, 거절/부정, 조회 순수성, 재현성 | 정본 수치 커널은 빈/소량/벡터 경계·부분/미지원/잘못된 출력에서 전체 정답으로 fallback하며 낡은 입력은 게시하지 않고 같은 결과도 두 번 적용하지 않는다. 선천 캐시는 학습/건강/현재 발현을 캐시하지 않는다. |

범위: 공통 원천 단회·상태 표식 피드백 방지·현재/조건부 잠재·실제 전투의 대표 축을 검사한다. 모든 가능한 효과 조합이나 수치 균형 완료는 아니다.

### INV-016 · 실물·소유·보관·접근·수납 분리

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [inventory: INV-016 recursive container capacity, cycle rejection and broken-container overflow preserve stock](../../../tests/Chronicles.Game.Tests/Features/InventoryScenarios.cs#L92) | `module` / 정상 결과, 거절/부정 | 수납 용량·자기/후손 순환·파손 초과를 검사하고 실제 물품을 보존한다. |
| [inventory: INV-016 ownership, custody, use, equipment and remote access remain independent](../../../tests/Chronicles.Game.Tests/Features/InventoryScenarios.cs#L68) | `module` / 거절/부정 | 소유·보관·사용·장비·원격 접근은 별도 권한이다. |
| [inventory: INV-005/015/016 equipment replacement is atomic across occupied slots and return capacity](../../../tests/Chronicles.Game.Tests/Features/InventoryScenarios.cs#L112) | `module` / 거절/부정 | 실패한 장비 교체는 점유 슬롯과 수납 물품을 원자적으로 유지한다. |
| [비경 공개 명령: 내부 생활은 실제 휴대한 식량만 쓰고 바깥 바닥 재고나 보호자 식량을 관통하지 않는다](../../../tests/Chronicles.Game.Tests/Features/ExplorationMaintenanceScenario.cs#L3) | `public_world` / 거절/부정, 공개 명령 | 소유권만으로 현재 하위 공간 밖의 재고를 소비하거나 표시하지 않는다. |
| [inventory validation scope: 이전 실제 명령·해시·거부 결과와 호출 사이 변경을 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryValidationScopeScenarios.cs#L7) | `module_and_world` / 정상 결과, 거절/부정, 저장/재개, 조회 순수성, 재현성 | 실제 창세 snapshot의 후보 지문/낡은 인계와 모듈의 수납·권한·보관/이동·예약·장비·파손·반복 읽기/직렬화를 기존 지문·오류 transcript와 비교한다. |

범위: 모듈의 실물/수납/권리 부정 검사와 세계의 실제 화물·비경 접근을 구분한다. 각 가능한 컨테이너 깊이를 전수 검사했다고 하지 않는다.

### INV-017 · 공통 예약·예치·시간 용량과 공정 소비

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/017/024 knowledge public world: real request and teaching share both participants' labor and cancel together](../../../tests/Chronicles.Game.Tests/Features/KnowledgeIntegrationScenarios.cs#L195) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 실제 교사·학생의 공통 행동 시간을 동시에 점유하고 한쪽 취소가 양쪽 교육을 함께 종료한다. |
| [INV-004/012/017 실제 양육 시간·식량·사회 관계·감정·판단 이력](../../../tests/Chronicles.Game.Tests/Features/SocietyScenarios.cs#L5) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 돌봄 시작은 완료가 아니고 돌봄/수행 중복은 거절되며 복원 후 실제 월·식량을 한 번 소비한다. |
| [INV-004/017/018/021 H10 action owner: real consumed production resumes and cancellation never recreates inputs](../../../tests/Chronicles.Game.Tests/Features/ActionAuthorityScenarios.cs#L89) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 이미 소비한 공정의 취소는 미사용 예약 해제와 구분해 입력을 환급하지 않는다. |
| [INV-004/006/017/022 H04 shared work: earlier teacher clears later pupil's due token in the actual month](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L171) | `public_world` / 정상 결과, 저장/재개, 공개 명령, 재현성 | 먼저 완료하는 실제 교사가 나중 학생의 행동/기한 토큰을 해제하는 월 경계에서도 교육은 한 번 완료하고 원본/새 실행·저장 결과를 보존한다. |
| [inventory commitment reference: 소비되어 없는 입력의 예약 연결·동시 작업·위조·취소를 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryCommitmentValidationScenarios.cs#L84) | `module` / 정상 결과, 거절/부정, 저장/재개, 조회 순수성 | 부분 모듈의 실제 소비 뒤 입력 실물이 없어도 공정/노동/예약 연결을 보존하며 중복·공유 예약 위조와 취소 환급을 거절한다. |
| [inventory sync: 실제 제작·감정·운송과 마지막 노동 행·사람 순서·반복 및 취소 후 새 범위 보존](../../../tests/Chronicles.Game.Tests/Features/InventorySynchronizationScenarios.cs#L5) | `module` / 정상 결과, 거절/부정, 재현성 | 합성 부분 모듈의 제작/감정/운송 약정·같은 Due·마지막 노동 행·원래 사람 순서와 취소 후 새 범위를 보존한다. 현재 전체 해시 상수는113법칙 strict 기준이며 TTL키 제외 시 옛 기준이 재현된 원본은 별도 보존했다. 실제 앱 생사 fixture와 구분한다. |
| [INV-004/008/017/018 AI 수행 연결: 입문 후 정책 이해도까지 실제 재전수하고 다음 경지 학습·돌파를 이어간다](../../../tests/Chronicles.Game.Tests/Features/AgentCultivationIntegrationScenarios.cs#L111) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 첫 실제1% 전수부터 정책10%까지 실제 요청/응답·유료 전수를 반복하고 배운 Qi 수행·다음 경지 준비를 잇는다. 저장/예산을 검사하며 단축 fixture를 기본 성장 성공으로 세지 않는다. |
| [ecology recovery: 실제 전량 예약 실패 뒤 유료 식량 재관측·알려진 이동·저장·제어 경계](../../../tests/Chronicles.Game.Tests/Features/EcologyRecoveryIntegrationScenarios.cs#L58) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 타인의 실제 작업이 식량 전량을 예약한 경계에서 AI는 거절을 짧게 기억하고 한 달/식량 비용의 재관측 후 알려진 인접지로 움직인다. 제어 중 인물에는 회복AI를 적용하지 않고 무료 식량/영구 고갈을 만들지 않는다. |
| [inventory source observation: 공개 food 선택은 정확한 현지 원천을 실제 비용·한 달·저장 재개로 관측](../../../tests/Chronicles.Game.Tests/Features/InventorySourceObservationScenarios.cs#L18) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 정확한 현지 food 원천을 식량1/한달을 지불해 관측하며 완료 전 개인 정보는 없다. 저장 재개·실제 잔량·중복 무소비와 알려진 원천의 유료 재관측을 검사한다. |
| [inventory validation boundary: 단일 노동·인접·겹침·시설 용량과 선행 invalid 오류를 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryValidationBoundaryScenarios.cs#L24) | `module` / 정상 결과, 거절/부정 | 부분 노동 원장의 단일/인접/겹침·시설 capacity/불일치·long 끝 경계에서 기존 거절 우선순위와 원본 불변을 검사한다. 모든 interval 오류가 group 용량 오류보다 먼저다. |
| [INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패](../../../tests/Chronicles.Game.Tests/Features/InventoryTransportSaveScenarios.cs#L7) | `host` + `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 운송중 실제 asset/labor예약ID·active·운반자/custodian·due와같은실물소유를 파일왕복전후 대조한다. 실제배송완료와봉쇄실패가원위치/예약해제를보존하며부분원장만의왕복으로확대하지 않는다. |

범위: 실제 교사/학생·돌봄/수행·제작·정책·전투의 공통 행동/예약과 취소 경계다. UI 계획 초안은 실제 수락/예약으로 세지 않는다.

### INV-018 · 자원·화폐·보상·경제 집계의 보존

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [currency public: exact two founder opening origins never grant money again on save or repeat initialization](../../../tests/Chronicles.Game.Tests/Features/CurrencyIntegrationScenarios.cs#L31) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 두 시조의 명시 초기 화폐 원천만 발행하고 저장/중복 초기화는 새 잔액을 만들지 않는다. |
| [currency public: source-free balances repeated burns and disabled origin validation are rejected on restore](../../../tests/Chronicles.Game.Tests/Features/CurrencyIntegrationScenarios.cs#L155) | `public_world` / 거절/부정, 저장/재개 | 원천 없는 잔액·중복 소각·발행 원천 검증 비활성 위조 저장을 거절한다. |
| [V021-O01 ecology kernel: actual extraction, finite depletion and renewable recovery reconcile to physical source receipts](../../../tests/Chronicles.Game.Tests/Features/EcologyIntegrationScenarios.cs#L79) | `module_and_world` / 정상 결과, 공개 명령 | 실제 추출·재생·손실의 자연 원천 잔액과 물리 생산 영수증을 대조한다. |
| [비경 공개 명령: 동시 실제 입장과 같은 시점 완료는 유한 원천을 한 번만 선점하고 다시 입장해도 복제하지 않는다](../../../tests/Chronicles.Game.Tests/Features/ExplorationIntegrationScenarios.cs#L129) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 경쟁 입장·재입장·중복 영수증은 유한 보상을 복제하지 않는다. |
| [INV-005/008/009/017/018 EX03 준비 기능: 실제 공개 광고·사용권의 scan/index 선택·누락·철회·정산 정합](../../../tests/Chronicles.Game.Tests/Features/ExecutionCandidateCorrectnessScenarios.cs#L5) | `module_and_world` / 정상 결과, 거절/부정, 공개 명령, 재현성 | 권한 필터된 같은 실제 현지 광고/사용권의 scan/ID index가 선택·누락·철회·숨은 변화에서 같고 공개 sale 정산/재전송은 돈·실물을 복제하지 않는다. 전체 AI 분포 실측은 아니다. |
| [inventory commitment reference: 같은 원천의 동시 예약·진행·취소 소비와 호출 사이 변경을 보존](../../../tests/Chronicles.Game.Tests/Features/InventoryCommitmentValidationScenarios.cs#L48) | `module` / 정상 결과, 거절/부정, 저장/재개, 조회 순수성 | 부분 모듈 fixture의 같은 유한 원천을 두 사람이 예약/시작/취소/완료할 때 잔량·실물이 보존되고 과예약/없는 원천/잘못된 수량의 기존 거부와 순서를 유지한다. |
| [INV-008/017/018 agent planning: 실제 정책 비축을 넘는 식량을 일반 거래 목표에서 출품하고 보유 실물만 인도한다](../../../tests/Chronicles.Game.Tests/Features/AgentEcologyTradeIntegrationScenario.cs#L3) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 거래 성향·유한 식량8/월소비1 fixture에서 비축 법칙만큼 남긴 실제 잉여 출품을 선택한다. 실제 구매/저장/재전송이 같은 실물과 대금을 복제하지 않는다. |
| [ecology recovery: 실제 배운 농사·관측한 물과 종자·유료 노동으로 식량 생산하고 저장된 비축법칙 적용](../../../tests/Chronicles.Game.Tests/Features/EcologyRecoveryIntegrationScenarios.cs#L259) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성, 조회 순수성 | 기존 실제 농사 지식이 있는 시조 fixture가 유료 관측·물/종자 채집·법칙 비축12를 거쳐 생산한다. 시작에 실물/노동을 소비하고 완료 전 무료출력 없이 실제 식량12·생태 생산·지식 실습 영수증/저장을 검사한다. |

범위: 물리 원천/생산/소비·손실·유한 보상·화폐 발행/이전의 실제 검사다. 가격/경기·장기 경제 균형이나 대량 인구 측정을 통과로 취급하지 않는다.

### INV-019 · 주권·위임·소유·영토 주장의 분리

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [V021-G01/G02 public politics: one province, persistent territoryless dynasty, annual zero revenue and restoration](../../../tests/Chronicles.Game.Tests/Features/PoliticsIntegrationScenarios.cs#L116) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 한 주의 실제 주권으로 왕조를 세우고 무영토 상태에서도 역사/재산을 보존하되 조세는0이다. |
| [INV-018/019 politics: vassal DAG, one resident tax and one direct-tax tribute per layer](../../../tests/Chronicles.Game.Tests/Features/PoliticsScenarios.cs#L137) | `module` / 거절/부정 | 봉신 DAG·주민 조세1회·층별 직접 조세 공납을 검사하여 중복 주권/집계를 막는다. |
| [V021-M03 public politics: real operation/property transfers respect personal knowledge without granting sovereignty](../../../tests/Chronicles.Game.Tests/Features/PoliticsIntegrationScenarios.cs#L209) | `public_world` / 거절/부정, 공개 명령 | 실제 운영/소유권 이전은 개인 인지를 따르고 주권을 지급하지 않는다. |

범위: 현재 주권/재산/운영·봉신·무영토 왕조의 공개 흐름과 모듈 DAG 검사다. 외부 영역/권리 유형을 추가했다는 의미는 아니다.

### INV-020 · 정책 시행·관할·승계·채권의 계속 효력

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [V021-G03 public politics: real dispatch, preparation escrow, busy action rejection, save and one-time monthly implementation](../../../tests/Chronicles.Game.Tests/Features/PoliticsIntegrationScenarios.cs#L57) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 채택/준비 예치/공통 노동/실제 월별 시행과 재전송의 단일 완료를 검사한다. |
| [V021-S02/S03 public politics: paid education and service cause succession, current charter revocation applies, rebirth restores no office](../../../tests/Chronicles.Game.Tests/Features/PoliticsIntegrationScenarios.cs#L312) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 재원이 있는 실제 교육·봉사·승계, 현행 헌장 철회와 재출생의 직책 비복원을 검사한다. |
| [INV-018/020 politics: all ten detailed policy categories use actual input and effect semantics](../../../tests/Chronicles.Game.Tests/Features/PoliticsScenarios.cs#L269) | `module` / 정상 결과, 거절/부정 | 10대 정책 분류의 실제 입력/효과 의미를 모듈로 검사하며 공개 명령 전 분류 완주로 바꾸어 말하지 않는다. |

범위: 실제 정책 준비/시행·승계·헌장/채권 경계와 10분류 모듈 검사를 구분한다. 모든 정책 분류를 공개 UI 명령으로 완주했다고 주장하지 않는다.

### INV-021 · 기존 행동 스케줄과 계획 경계의 정합성

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/017/021 H10 action owner: unbacked typed work and forged markers are rejected before completion](../../../tests/Chronicles.Game.Tests/Features/ActionAuthorityScenarios.cs#L48) | `public_world` / 거절/부정, 공개 명령 | 원장·몸·예정시각과 일치하지 않는 단일 행동 소유 및 위조 typed marker를 저장/명령에서 거절한다. |
| [INV-004/008/021 agent planning: actual search and source cognition budgets are bounded and a deferred goal resumes after save](../../../tests/Chronicles.Game.Tests/Features/AgentPlanningIntegrationScenarios.cs#L148) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 실제 예산 소진 뒤 목표를 보존하고 다음 결정/복원 시 재개한다. |
| [INV-010/021 ordinary AI cultivation completion returns to the actual goal decision boundary](../../../tests/Chronicles.Game.Tests/Features/CultivationIntegrationScenarios.cs#L252) | `public_world` / 정상 결과, 공개 명령 | 통상 월별 수행 완료는 Idle로 돌아가 건강·식량·목표를 다시 판단한다. |
| [INV-001/004/006/022 H04 actual due queue contracts: same-month changes, cancellation, generation and stable rebuild](../../../tests/Chronicles.Game.Tests/Features/ExecutionInfrastructureScenarios.cs#L63) | `module` / 정상 결과, 거절/부정, 재현성 | 모듈의 실제 기한 큐에서 같은 월 갱신·취소·옛 몸/세대·반복 재예약·ready 순서·안정 rebuild를 검사한다. versioned token의 무효 항목을 버리는 힙이며 제자리 indexed-heap 시험으로 부르지 않는다. |
| [INV-004/006/017/018/021 AI 거래 예산: 마지막 방문 뒤 생계 출품을 유예하고 충분한 예산에서만 실제 거래·환전을 선택한다](../../../tests/Chronicles.Game.Tests/Features/AgentTradeBudgetScenarios.cs#L5) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 예산1/2×원석 유무 네 실제 경계에서 마지막 방문 후 대기는 출품·예약·환전·추가RNG가 없다. 충분한 방문에서만 실제 원석 소비/화폐 원천 또는 보존된 식량 예약 출품을 실행하며 저장 결과가 같다. |

범위: 현재 StableEventQueue는 Person별 active/ready 사전과 versioned-token PriorityQueue로 기한을 판정하고 원래 사람 순서로 실행한다. 취소/재예약의 무효 토큰과 rebuild를 검사하며 옛 제안의 제자리 indexed-heap 위치 배열을 사용/검증했다고 주장하지 않는다. 작은 비교는 대량 성능 결과가 아니다.

### INV-022 · 미정 정책·차단 판정·검증 근거의 비우회

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [V021-M02/O04 ecology kernel: time and population alone do not unlock markets, workshops, settlements or capacity](../../../tests/Chronicles.Game.Tests/Features/EcologyIntegrationScenarios.cs#L99) | `module_and_world` / 거절/부정, 공개 명령 | 시간/인구만으로 공급자·시설·재산·전략 노드·수납 용량을 만들어 정체를 보정하지 않는다. |
| [INV-006/022 people: invariant absolute aptitude and scoped genius/candidate vacancies, ties and dated reevaluation](../../../tests/Chronicles.Game.Tests/Features/PeopleScenarios.cs#L198) | `module` / 거절/부정 | 범위·동점·날짜를 가진 실제 평가에서 공석을 허용하고 숨은 천재를 정답으로 생성하지 않는다. |
| [currency planning: 다른 인물의 돈·관측과 원격·예약·결손 영석은 목표를 만들지 않고 비활성·충분 잔액도 보존](../../../tests/Chronicles.Game.Tests/Features/CurrencyPlanningScenarios.cs#L98) | `public_world` / 거절/부정, 공개 명령 | 숨은 타인 자산·원격/예약/결손 영석이나 비활성·충분 잔액은 무료 채굴/환전 목표를 만들지 않는다. |
| [inventory projection: 70개 schema·실제 IL reader와 컴파일러 람다의 금지 읽기 자기검출](../../../tests/Chronicles.Game.Tests/Features/InventoryProjectionScenarios.cs#L8) | `module` / 거절/부정, 메타 계약 | 컴파일된 재고 메서드/람다 IL의 Person 접근을 명시15/미사용55 분류와 비교하고 금지 Journal 읽기·Work 쓰기를 자기 검출한다. 성능·장기 성장·모든 미래 reader 검증으로 확대하지 않는다. |
| [inventory typed copy schema: 도달한 모든 필드와 명시 Person15 footprint 누락 거부](../../../tests/Chronicles.Game.Tests/Features/InventoryTypedSnapshotScenarios.cs#L144) | `module` / 거절/부정, 메타 계약 | 도달 가능한 타입/전체 필드와 명시 Person15 footprint를 고정 allowlist로 대조해 새 필드/default-copy 결정 누락을 거절한다. 합성 schema guard이며 실제 저장/성능 채택 증거는 아니다. |
| [T04 sensitivity contract: fixed policies, law bounds, source/build identity and strict plan JSON; no measurement](../../../tests/Chronicles.Game.Tests/Features/GrowthSensitivityContractScenarios.cs#L78) | `module` / 정상 결과, 거절/부정, 메타 계약 | 합성 계획/source/빌드 메타데이터 입력으로 고정정책·범위·두 값·세 seed·기간·지문/dirty/build identity·엄격 JSON 계약을 검사한다. 실제 clean 빌드 캡처나 측정 실행 증거는 아니다. |
| [T04 sensitivity contract: completed nine-cohort baseline, physical conditions, hashes and aggregate originals; no growth](../../../tests/Chronicles.Game.Tests/Features/GrowthSensitivityContractScenarios.cs#L104) | `module` / 정상 결과, 거절/부정, 메타 계약 | 합성9-cohort JSON의 완료/6000 관찰·법칙/시조/외부인·환경/PC·digest/원본 행 대조와 변조 거절 계약을 검사한다. 실제9개 성장 표본을 생산하지 않는다. |
| [T04 sensitivity contract: saved People policy values take precedence and unobserved species/models are not used claims](../../../tests/Chronicles.Game.Tests/Features/GrowthSensitivityContractScenarios.cs#L185) | `module` / 정상 결과, 메타 계약 | PeopleState.Empty의 명시 저장 정책값 우선과 관측 없는 종족/수태모델을 used로 표시하지 않음을 검사한다. 선언 catalog 존재를 실제 전종족 실행으로 세지 않는다. |
| [T04 sensitivity contract: two-case partitions remain partial, only six complete, duplicate/CLI/output overwrite rejected](../../../tests/Chronicles.Game.Tests/Features/GrowthSensitivityContractScenarios.cs#L140) | `host` / 정상 결과, 거절/부정, 메타 계약 | 합성 완료 계약에서 선택2개는 전체 미완료/6개만 complete다. 실제 임시 report 고유 경로·lock/pending·덮어쓰기 거절과 CLI 구문을 검사하며 GameWorld/성장 collector는 호출하지 않는다. |
| [T04 sensitivity contract: actual CLI rejects report alias before functional writes and preserves plan/baseline bytes](../../../tests/Chronicles.Game.Tests/Features/GrowthSensitivityContractScenarios.cs#L203) | `host` / 거절/부정, 메타 계약 | 실제 별도 CLI 두 회에서 sensitivity+report plan/base alias를 쓰기 전에 거절하고 원문 bytes·output/pending/lock0·collector0을 검사한다. 민감도/성장 실행은 아니다. |

범위: 제안/실제 이름·작은 기능·실측·플랫폼/릴리스는 서로 다른 근거다. H04/H09·EX03/05/06·민감도 계약은 실제 기능 본문의 범위만 연결하며 sampler 실행/기본 채택/측정 완료로 세지 않는다. 전체 결과는 별도 원본·실행 출처로 대조하며 과거 다른 소스의 보고서를 승격하지 않는다.

### INV-023 · 경지 필수 전이·돌파/중단의 경계

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/006/013/023 public scheduled fracture·restore·same life death authority](../../../tests/Chronicles.Game.Tests/Features/CultivationIntegrationScenarios.cs#L75) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 실제 원영 파단 뒤 실패/취소는 같은 현생 사망으로 한 번 정산하고 앱 저장 복원은 포기가 아니다. |
| [INV-004/013/023 every minor-stage tribulation including entry/ninth/completion](../../../tests/Chronicles.Game.Tests/Features/CultivationScenarios.cs#L319) | `module` / 정상 결과, 거절/부정, 저장/재개 | 경지 진입·9소경지·완료의 실제 겁난과 비승 전 반선 경계를 모듈에서 검사한다. |
| [INV-003/004/006/023 legal cultivation law edits preserve adopted work completion and active attempts](../../../tests/Chronicles.Game.Tests/Features/CultivationIntegrationScenarios.cs#L186) | `public_world` / 정상 결과, 저장/재개, 공개 명령 | 실행 중 시도는 채택 법칙을 유지하고 실제 이후 수행만 새 법칙을 따른다. |
| [INV-004/006/018 AI 수행 연결: 실제 entry 전수·토납 뒤 감응하고 Qi 공법·숙련은 자동 지급하지 않는다](../../../tests/Chronicles.Game.Tests/Features/AgentCultivationIntegrationScenarios.cs#L76) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제10개월 entry 전수·토납 후 AI 별도 준비와 entry revision 형성, 저장 재개를 검사한다. 보통 player 수행은 Mortal/Idle이고 Qi 공법·숙련·근 성장을 지급하지 않는다. 짧은 기간/유한 식량은 명시 fixture다. |
| [INV-004/006/018/024 AI 수행 연결: 실제 감응 실패·회복 후 새 시도를 하며 과거 실패와 출처를 보존한다](../../../tests/Chronicles.Game.Tests/Features/AgentCultivationIntegrationScenarios.cs#L202) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 감응 실패는 Mortal/Recovering 사건으로 남고 위험 법칙 변경 뒤 회복/새 시도가 다른 ID·새 revision·같은 실제 entry 출처를 사용한다. 과거 실패나 경지를 임의로 고치지 않는다. |
| [INV-004/006/023 감응 출처 선택: 1% 실습·전환·상위 경지 계약과 동결 출처 유지](../../../tests/Chronicles.Game.Tests/Features/CultivationPreparationEvidenceScenarios.cs#L65) | `module` / 정상 결과, 거절/부정, 저장/재개 | 부분 수행 모듈에서1% 실습과 준비10%를 분리하고 모듈 JSON 왕복한 시도의 열람 출처를 새 지식으로 대체하지 않는다. 상위/전환은 해당 공법·실습 gate를 유지하며 공통 세계 codec 저장 증거가 아니다. |
| [INV-004/006/018 감응 출처 선택: entry 없는 실제 Qi 학습·토납 뒤 두 AI caller를 지나 준비한다](../../../tests/Chronicles.Game.Tests/Features/CultivationPreparationEvidenceScenarios.cs#L176) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 교본 인계/식량 fixture에서 실제10개월 Qi 독서와 토납 후 entry 없는 AI도 두 caller를 지나 자기 Qi 출처로 준비한다. 보통 player 수행은 Idle이며 entry 지식이나 불필요한 요청을 만들지 않는다. |

범위: 공개 원영 파단·채택 법칙과 모듈의 경지/겁난 전이를 구분한다. 짧은 기능 사례는 최종 수행 비용/위험 분포·성장 측정이 아니다.

### INV-024 · 저작·판본·개인 지식과 실행의 분리

| 정확한 실행 이름·소스 | 경계/검사 종류 | 실제 검사 범위 |
| --- | --- | --- |
| [INV-004/005/024 knowledge public world: study resumes against its actual body book and clock without skill gain](../../../tests/Chronicles.Game.Tests/Features/KnowledgeIntegrationScenarios.cs#L132) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 공부는 실제 몸·책·시간을 쓰며 책 소유/해독으로 실전 숙련을 지급하지 않는다. |
| [INV-004/018/024 knowledge public world: actual misreading paid experiment and rereading preserve adopted history](../../../tests/Chronicles.Game.Tests/Features/KnowledgeIntegrationScenarios.cs#L229) | `public_world` / 거절/부정, 저장/재개, 공개 명령 | 실제 오독→유료 실험→재독은 채택 당시의 해석/행동 이력을 소급 정답화하지 않는다. |
| [INV-004/024 knowledge: read copy and transmission permissions expire independently and teachers cannot exceed their understanding](../../../tests/Chronicles.Game.Tests/Features/KnowledgeScenarios.cs#L446) | `module` / 거절/부정, 저장/재개 | 열람·필사·전수 권리의 독립 만료와 스승 자신의 이해 상한을 검사한다. |
| [INV-004/006/018/024 AI 수행 연결: 명시 Qi 대원만 경계에서 실제 오독·준비 거절 뒤 재독해하고 과거 해석을 보존한다](../../../tests/Chronicles.Game.Tests/Features/AgentCultivationIntegrationScenarios.cs#L150) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 명시 Qi 대원만/교본 인계 경계에서 실제 오독·거절 뒤11회 이상 독해와 저장 재개 후 새 개인 revision으로 준비하고 옛 해석을 보존한다. fixture 경지를 획득 성장으로 표시하지 않는다. |
| [INV-004/006/021 감응 출처 선택: entry 추가는 유효 Qi 자격·정수 품질을 낮추지 않는다](../../../tests/Chronicles.Game.Tests/Features/CultivationPreparationEvidenceScenarios.cs#L9) | `module` / 정상 결과, 거절/부정, 재현성, 조회 순수성 | 순수 수행 fixture의 entry/Qi 자격·정확한 정수 형성 품질을 비교하여 동률은 Qi를 보존한다. 샘플/입력은 불변이고 제공된-context 조회와 준비 출처/거절이 같다. 이 본문은 공통 GameCodec 저장을 실행하지 않는다. |
| [INV-004/006/018 감응 출처 선택: 실제 Qi 오독 거부·재학습 대상도 같은 개인 판본이다](../../../tests/Chronicles.Game.Tests/Features/CultivationPreparationEvidenceScenarios.cs#L197) | `public_world` / 정상 결과, 거절/부정, 저장/재개, 공개 명령, 재현성 | 실제 Qi 오독의 거절·재학습이 같은 개인 work/edition/revision을 가리킨다. 실제 추가 독서·저장 후 새 Qi revision으로 준비하며 옛 오독을 보존하고 entry를 제조하지 않는다. |

범위: 공개 지식/실물/시간·오독/정정과 모듈 권한·판본·숙련을 구분한다. 소유/전승 접근만으로 미독 장절이나 실전 숙련을 지급하지 않는다.
