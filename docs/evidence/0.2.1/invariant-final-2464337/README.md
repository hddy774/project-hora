# 2464337 전체323개 기능과 Python 입력 소스 감사

실제 실행 소스는 `2464337ec577b2894eb87468c2c72916b1d6b8bf`, 제품 지문은
`0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2`다.
동결 Release 실행의 실제 결과는 **323/323·실패0·종료0**이다.

## 실제 원문과 출처

| 자료 | 보존 범위 |
| --- | --- |
| [기능 원문](functional.json)·[로그](functional.log) | 전체323개 실제 결과. SHA256 `053535d3f2f4269a4404dbc21b44297dbf71664972a41fe9f2297733fe0793e2` |
| [원래 실행 기록](root-run.original.json)·[빌드 로그](release-build.log) | 명령·CPU0·시각·SDK·실제 런타임 map·원래 DLL SHA·Release 결과 |
| [runtimeconfig](runtimeconfig.original.json)·[출처](provenance.json) | PGO=false/환경 override4개 null·전후clean·4DLL source/Release와 생성 정보·PE/CLI 정적 확인 |
| [기존 검사기 결과](execution-verification.json) | 실제323개 이름·24INV·171개 연결과 전체 원문 대조 |
| [보존 대조](preservation-checks.json)·[SHA 색인](manifest.json) | 기존 C#/등록323/171개의 의미·앵커·테스트 지문 불변과 원문/파생물 구분 |

root 실행은2026-10-31 04:32:35.067843~04:41:22.603520 UTC이며 정확 종료 시각은 원래 run을 따른다.
SDK10.0.403과 실제 PID의 .NET10.0.14 map을 기록했다. runtimeconfig의10.0.0은 프레임워크 대상값이다.
문서 리뷰가 과거 프로세스를 독립 계측했다는 뜻은 아니며, root가 기록한 map과 현재 동결 진입점의
SHA/정보 버전을 대조했다. 원문 사본은 scratch와 바이트 동일하고 기능 소요 시간은 성능 수용 표본이 아니다.

## 바뀐 제품 입력3개와 helper의 범위

[SourceReview](../invariant-execution-source-review-2464337.json)는7901 이후의 실제 제품 입력3개를 다룬다.

- `scripts/android_readiness.py`: adb return0·단 하나의 알려진 Status와 정확한 Activity를 요구한다.
  Android `Status: timeout`은 시작 대기 미완료로 기록하고, 같은90초 deadline 안에서 기존 PID·전경·
  WebView·완전한 screenshot·DOM checker를 통과해야 준비 성공이다. subprocess timeout·비zero·오류와
  중복/잘못된 Status·Activity 행은 거부하고 원문/stderr/관측 상태를 보존한다.
- `scripts/test-android-readiness.py`: 실제 launch stdout을 가상 clock/device와 조합하는 host 회귀다.
  지연 준비·원래90초·설치/로그/launch 공유 한도·중복/오류/미상/누락된 증거를 다룬다.
  실제 기기 재생이나 Game 전체검사에 포함된 새 사례로 부르지 않는다.
- `scripts/fixtures/android-launch-timeout-api24-41165712717.txt`: 실제 API24 launch stdout5행이다.
  이 fixture 자체는 준비 DOM이나 실제 기기 성공 근거가 아니다.

기존 refresh helper의 C#/SDK 범위에서는 변경0개다. 이 한계를 그대로 두고 `manual_additional_paths`에
세 경로를 빠짐없이 기록했으며 실제 제품 입력 분류와 따로 대조했다. helper·검사기는 수정하지 않았다.

## 같은323개와171개 증인

[현재 JSON](../invariant-execution-map.json)·[읽기용 표](../invariant-execution-map.md)의
등록323개(feature285+Program38)·24INV·171증인은 이전7901과 같다. 전체 C#·검사 본문과
Name/source/boundary/INV·24개 title/limits·171개 claim/kinds/anchors를 정확히 보존했다.
Python 흐름이나 host 회귀를 새 Game INV 증인으로 세지 않았고 맵은 계속 `not_asserted`다.

[이전7901 원문](../invariant-final-7901f34/README.md)과
[당시 SourceReview](../invariant-execution-source-review-7901f34.json)는 당시 source/지문으로 유지한다.
이전 map은 고정 commit URL/SHA로 보존했다. 새 C# 전체 결과는 Python host 검사·실제 native를
대신하지 않는다. 이 문서에서는 Python 검사·adb·기기·Game·빌드·성능·성장을 새로 실행하지 않았다.

```text
python3 -B scripts/validate-v021-invariant-execution.py --report docs/evidence/0.2.1/invariant-final-2464337/functional.json --executed-source 2464337ec577b2894eb87468c2c72916b1d6b8bf --output docs/evidence/0.2.1/invariant-final-2464337/execution-verification.json
```

기존 검사기의 실제 등록/Git 테스트 소스/보고서 대조와 과거 실행 인증은 구분한다.
새 단기 측정과 원래4×120개월 장기 검증의 진행·수용은 별도이며 이 문서에는 합격을 기록하지 않는다.
이전 단기32·SIGKILL/부분 원문·30월 재개 파일 진단·성장/민감도를 새FP의 수용으로 재라벨하지 않는다.
원장/readiness/진행3파일·예산·제품·릴리스 게이트는 변경하지 않았다.
