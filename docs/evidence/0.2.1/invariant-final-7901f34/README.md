# 7901f34 전체 323개 기능과 불변식 연결

실제 실행 소스는 `7901f344462ebc1f98d7105efd79ab0aa058b8fe`, 제품 지문은
`c91c3d7b7cf0441f207825d0490d38501072b63b0364234fbd7cf973b1f620f1`다. root의 동결 Release 실행 결과는
**323/323·실패0·종료0**이다. 이 문서 작업의 새 Game·빌드·성능·성장 실행은0이다.

## 원문과 실행 출처

| 자료 | 범위 |
| --- | --- |
| [기능 원문](functional.json)·[로그](functional.log) | 실제 전체323개 결과. SHA256 `1cca9aea9ffdef4149282cf8586fbbaf653288a62c31b5beccbe9b1bcd2706ba` |
| [원래 실행 기록](root-run.original.json)·[빌드 로그](release-build.log) | 명령·시각·CPU0·SDK·실제 런타임 map·원래 DLL SHA·Release 결과 |
| [runtimeconfig](runtimeconfig.original.json)·[출처](provenance.json) | PGO=false/환경 override4개 null·전후 clean·4DLL source/Release·생성 정보와 PE/CLI 정적 대조 |
| [검사기 결과](execution-verification.json) | 기존 검사기의323개 실제 이름·24INV·171개 증인 및 실제 전체 원문 대조 |
| [새 연결](map-additions.json)·[보존 대조](preservation-checks.json) | 새2개/4연결과 기존321행/167연결의 불변 |

기능 실행은 **2026-10-31T03:04:58.093304+00:00~2026-10-31T03:13:09.096909+00:00**, CPU0에서 수행했다.
SDK10.0.403이며 원래 실행 PID의 runtime map은 .NET10.0.14 경로를 기록한다.
runtimeconfig의10.0.0은 프레임워크 대상값이다. 문서 리뷰가 과거 프로세스를 독립 계측했다고
표시하지 않으며 root가 기록한 map과 현재 동결 실행 진입점의 SHA/정보 버전을 대조했다.
원문은 scratch 파일과 바이트 동일하고, 기능 소요 시간은 성능 수용 표본이 아니다.

## 새 2개 검사의 실제 범위

| 등록 검사 | 경계 | 실제 범위와 한계 |
| --- | --- | --- |
| schema2 실제 세계 호환1 | public_world | 사건 텍스트만 바꾼 GameWorld fixture·두 문화권·Unicode/lone surrogate·빈값/긴 문자열·순번0/7/상한에서 독립 기존 encoder와 전체 bytes를 대조한다. 공통 Decode의 순번/Hash/RNG와 원본 불변도 검사한다. 공개 행동 명령·월 진행·파일/성능은 실행하지 않는다. |
| schema2 조각 경계1 | module | private writer를 reflection delegate로 호출하여 빈 payload·16KiB 전후와 경계 UTF8 문자/escape·property 순서를 독립 이전 serializer bytes와 대조한다. 임의 UTF8 본문이며 실제 세계·공통 Decode/파일·월 진행은 실행하지 않는다. |

두 정확한 Name은 INV-004/006에 각각 연결했다. [전체 증인표](../invariant-execution-map.md)는
등록323개(feature285+Program38)·24INV·171개 연결이다. 기존321개의 source/경계/INV 행과
24개 제목/한계·167개 claim/kinds/anchors는 보존하고 새4연결을 추가했다.
맵의 실행 상태는 계속 `not_asserted`다.

## 소스 감사와 과거 자료

[SourceReview](../invariant-execution-source-review-7901f34.json)는 5f→7901의 실제2제품경로를 검토한다.
schema2 봉투를 같은 Format/Schema/Hash/Payload/Sequence 순서와 같은 writer 설정으로 쓰고,
Payload만16KiB UTF8 조각으로 나눈다. canonical 본문·논리checksum·순번·기본16MiB 선택·binary3·
Decode/Hash/Clone은 바꾸지 않는다. 테스트 기대값도 segment writer로 만들지 않는다.

helper의 일반적인 scripts/WF 생략 한계는 실제 product_path 분류와 별도 대조했고
이번2경로는 helper 범위와 같으며 추가 제품입력0개다. 새 Name2개를 먼저 수동 연결한 전/후 검토
계획을 보존하며 helper·검사기·제품·정본 제안 검사 상태·원장·readiness·예산은 수정하지 않았다.

[이전5f 전체321](../invariant-final-5f789c8/README.md)와
[당시 SourceReview](../invariant-execution-source-review-5f789c8.json)는 당시 source/지문으로 유지한다.
이전 표의 고정 commit URL/SHA는 보존 대조에 남겼다. 과거 성공·장기 실패/부분·native·성장/민감도
원문을 새 제품 지문의 합격으로 재라벨하지 않는다.

```text
python3 -B scripts/validate-v021-invariant-execution.py --report docs/evidence/0.2.1/invariant-final-7901f34/functional.json --executed-source 7901f344462ebc1f98d7105efd79ab0aa058b8fe --output docs/evidence/0.2.1/invariant-final-7901f34/execution-verification.json
```

검사기는 실제 이름·Git 테스트 소스·원문을 대조하며 과거 프로세스를 인증하지 않는다.
전체 기능의 정확성과 새 네 규모/120개월 성능·민감도 기본값·native·유지 서명/업그레이드·
111개/릴리스 수용은 구분한다. 단일 N2 진단을 전체 네 규모 수용으로 확대하지 않는다.
