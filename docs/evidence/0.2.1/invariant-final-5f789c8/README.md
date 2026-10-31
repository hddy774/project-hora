# 5f789c8 전체 321개 기능과 불변식 연결

실제 실행 소스는 `5f789c817eb9519b696acd72410643169ff11ef2`, 제품 지문은
`a8634094870fd1219dacba2494392e2f8efb5709fbc41c13c24c69202fdbac12`다. 동결 Release 실행의 실제 결과는
**321/321·실패0·종료0**이다. 이 문서 작업의 새 게임·빌드·성능·성장 실행은0이다.

## 실제 원문과 실행 출처

| 자료 | 범위 |
| --- | --- |
| [기능 원문](functional.json)·[로그](functional.log) | 전체321개 실제 결과. SHA256 `f2c354e118a8a403a47d4989967abf001e6d177ddf947238eba665797841be92` |
| [원래 run](root-run.original.json)·[빌드 로그](release-build.log) | 정확 명령·시각·CPU0·SDK·실제 런타임 map·원래 DLL SHA·Release 결과 |
| [runtimeconfig](runtimeconfig.original.json)·[출처](provenance.json) | TieredPGO=false/환경 override4개 null·4DLL source/Release·생성정보와 PE/CLI 정적 확인 |
| [검사기 결과](execution-verification.json) | 기존 검사기의321개 실제 이름·24INV·167개 증인과 전체 보고서 대조 |
| [새 연결](map-additions.json)·[보존 대조](preservation-checks.json) | 새7개·15연결과 기존314개 행·152개 연결의 불변 |

기능 실행은 **2026-10-31T02:25:09.935382+00:00~2026-10-31T02:33:48.851809+00:00**, CPU0에서 이루어졌다.
SDK10.0.403이고 원래 실행 PID의 runtime map은 .NET10.0.14 파일을 기록한다.
runtimeconfig의10.0.0은 프레임워크 대상값이다. 문서 리뷰가 과거 프로세스를 독립 계측했다고
표시하지 않으며, root가 기록한 실제 map 경로와 현재 동결 실행 진입점의 SHA/정보 버전을 대조했다.
게시 전 b159 DLL의 PR236 선택 증거와 달리 이 원문은 동결5f 소스의 전체 실행이다.
각 기능 소요 시간은 성능 수용 표본이 아니다.

## 새 7개 검사의 실제 범위

| 등록 검사 | 경계 | 증명 범위와 한계 |
| --- | --- | --- |
| canonical Hash3 | module | 독립 기존 buffered oracle·고정 df N100 입력·Unicode/문화권·null/열거 실패·부분 출력 격리·상태/RNG/ID. 파일 저장/재개나 N100 월별 성능 실측은 아니다. |
| 기본 저장 경계1 | host | 사건 텍스트만 늘린 본문16MiB/16MiB+1에서 실제 기본 JSON2/이진3·작은 기존 bytes·이전 큰 JSON 읽기·순번/RNG·임시 원자파일 왕복/정리를 확인한다. 실제100명120개월 예산을 대신하지 않는다. |
| 소유 지식 격리1 | module | 내부 attach의 즉시 전이·busy/잘못된 schema 거부·공개 Checkpoint/Restore/조회 배열의 깊은 격리다. 실제 GameWorld 파일 저장은 아니다. |
| 소유 지식 캐시1 | public_world | 실제 possess/study·1/2개월 완료·공통 codec/외부 snapshot·후보 교체 후 두 완료 기록/전체 Hash 동등이다. |
| 소유 지식 원자성1 | public_world | 실제 거부/충돌/재전송·타 영혼 결속 재료의 예약 실패·커밋 revision 상한 예외·저장 재개 뒤 다음 학습을 검사한다. Name의 INV-007만으로 View/관찰 대조를 추정하지 않아 INV-004/005에 연결했다. |

각 정확한 Name·INV·claim·앵커는 [새 연결 JSON](map-additions.json)과
[전체 증인표](../invariant-execution-map.md)에 있다. 기존314개의 source/경계/INV 행과
24개 제목/한계·152개 claim/kinds/anchors는 그대로다. 새 등록은 feature283+Program38이고
증인 연결은 총167개다. 맵 실행 상태는 계속 `not_asserted`다.

## 소스 감사와 과거 보존

[SourceReview](../invariant-execution-source-review-5f789c8.json)는 df→5f의 제품8경로를 검토한다.
Hash의 호출 단위 비파일 stream, 기존 binary3를 더 일찍 선택하는16MiB 경계, 이미 분리한
세계 소유 KnowledgeState의 내부 attach와 객체 교체 시 캐시 재연결, NoInlining 장기 사례 경계를 구분한다.
NoInlining은 등록 기능을 추가하지 않는다. 원래 계획/상한·시간·검증/IO·보고서 경계를 바꿔 합격시킨 근거가 아니다.
새 실제 장기 결과는 별도로 필요하다.

기존 refresh helper는 일반적으로 scripts/WF를 생략하므로 실제 제품 입력 분류와 별도 대조했다.
이번 변경8개는 helper 범위와 같고 추가 경로0개다. 새 Name7개를 먼저 수동 연결한 전/후 검토 계획을
함께 남겼으며 helper·검사기·정본 제안 검사 상태·원장·readiness·예산은 바꾸지 않았다.

[이전 df 전체314](../invariant-final-df72405/README.md)와
[당시 SourceReview](../invariant-execution-source-review-df72405.json)는 당시 출처로 유지한다.
이전 표의 고정 commit URL과 SHA는 보존 대조에 기록했다. 과거 성공·장기 실패/부분·native·
성장/민감도 자료를 새 제품 지문의 통과로 재라벨하지 않는다.

```text
python3 -B scripts/validate-v021-invariant-execution.py --report docs/evidence/0.2.1/invariant-final-5f789c8/functional.json --executed-source 5f789c817eb9519b696acd72410643169ff11ef2 --output docs/evidence/0.2.1/invariant-final-5f789c8/execution-verification.json
```

검사기는 실제 등록 이름·Git 테스트 소스·보고서를 대조하며 과거 프로세스를 인증하지 않는다.
새 전체 기능 원문과 run·DLL·소스 감사를 함께 읽어야 한다. 이 자료만으로 네 규모 성능·120개월 장기·
민감도 기본값·native·유지 서명/업그레이드·111개/릴리스 완료를 판정하지 않는다.
