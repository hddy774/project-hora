# df72405 전체 314개 기능 검사와 불변식 연결

실제 실행 소스는 `df7240556a260202a248e57b1f9868e372b9d995`, 제품 지문은
`0cb2f90fe60e571032450eff0b172b1ee7bc44b51724bcff01fdc94dc2bbfb8e`다.
통합 담당자의 동결 worktree에서 전체 **314/314, 실패 0, 종료 코드 0**을 확인했다.
이 문서 작업은 게임·빌드·성능·성장 검사를 추가 실행하지 않았다.

## 실행 원문과 출처

| 자료 | 보존 내용 |
| --- | --- |
| [기능 결과](functional.json) | 실제 전체 314개 결과. SHA256 `6f334c51dd49f8059a0199dc86533f218d1f583017b7943368739413ee688f66` |
| [실행 로그](functional.log) | root가 인계한 stdout/stderr 원문 |
| [원래 실행 기록](root-run.original.json) | 실제 명령·시각·SDK·환경·4개 DLL과 deps/runtimeconfig의 SHA |
| [실제 runtimeconfig](runtimeconfig.original.json) | 실행 진입점의 설정 사본. `System.Runtime.TieredPGO=false` |
| [출처 대조](provenance.json) | 원문 SHA·Release 생성 정보·현재 4개 DLL SHA·PE/CLI 메타데이터의 source 직렬화 값 |
| [검사기 결과](execution-verification.json) | 기존 검사기의 314개 이름·24개 불변식·152개 증인 연결 및 실제 보고서 대조 |
| [보존 대조](preservation-checks.json) | 검사 이름·경계·주장·앵커 불변, 원저자 분리 증거와 현재 14개 C# 파일 SHA 대조 |

기능 실행은 2026-10-31 **01:25:30.872438~01:34:20.747566 UTC**, CPU 0에서 수행했다.
SDK는 `10.0.403`이고 통합 담당자가 인계한 실제 런타임은 `.NET 10.0.14`다.
runtimeconfig의 `10.0.0`은 프레임워크 대상값이므로 실제 설치 런타임 버전으로 바꿔 읽지 않는다.
`DOTNET_TieredPGO`, `COMPlus_TieredPGO`, `DOTNET_TieredCompilation`,
`COMPlus_TieredCompilation`은 원래 실행 기록에서 모두 null이다.

사본은 `/workspace/scratch/v021-integration-df72405/` 원문과 바이트가 같다.
기록된 DLL SHA는 현재 동결 실행 진입점 파일과 대조했고, 생성 AssemblyInfo의 Release/source 값과
PE/CLI의 직렬화된 정보 버전도 정적으로 확인했다. 과거 프로세스의 실제 DLL load를 독립 계측했다는
주장은 하지 않는다. 현재 실행 worktree의 HEAD/clean 확인과 root의 동결 실행 인계도 구분한다.

## 소스 감사와 같은 증인표

[SourceReview](../invariant-execution-source-review-df72405.json)는 이전 실제 전체 실행 소스
`94ccb5d16f0c0d7bba0fd14aa8b0451dfe15bdd5` 이후 달라진 제품 입력 **21개 경로**를 다룬다.

- 수행 엔진·인물 사회 bridge의 14개 C# 파일은 원저자의 멤버/토큰/필드·초기화 순서 보존 기록과
  현재 파일 SHA를 연결했다. 원저자 비교 기준의 두 원래 파일이 94 소스와 같은 Git blob인지도 확인했다.
- `global.json`의 SDK 403 정확 선택과 Android 준비 관측·회귀 fixture·진행 자동화 변경은 각 목적을 따로 기록했다.
- 기존 refresh helper의 기본 검토 범위는 C#/SDK **15개**다. 릴리스 검사기의 제품 입력 분류에 포함되는
  workflow/scripts/fixture **6개**를 추가 수동 검토한 계획·확인서를 함께 남겼다. helper나 검사기를 고치지 않았다.

[현재 JSON](../invariant-execution-map.json)과 [읽기용 증인표](../invariant-execution-map.md)는
등록 **314개(feature 276 + Program 38)**, **24개 불변식**, **152개 증인 연결**을 유지한다.
각 Name·boundary·claim·anchor와 테스트 소스 지문은 이전 표와 같다. 실행 상태는 계속
`not_asserted`다. 실제 전체 결과는 별도 원문에 연결하며 제안 검사 상태·원장·readiness를 바꾸지 않는다.

이전 [94 원문](../invariant-final-94ccb5d/README.md)과
[SourceReview](../invariant-execution-source-review-94ccb5d.json)는 그대로 보존했다.
당시 active map의 [JSON](https://github.com/hddy774/project-hora/blob/df7240556a260202a248e57b1f9868e372b9d995/docs/evidence/0.2.1/invariant-execution-map.json)·
[Markdown](https://github.com/hddy774/project-hora/blob/df7240556a260202a248e57b1f9868e372b9d995/docs/evidence/0.2.1/invariant-execution-map.md)은
고정 커밋에 남아 있고 해당 SHA를 보존 대조에 기록했다. 원문을 중복 대형 snapshot으로 다시 저장하지 않았다.

## 검증 범위

```text
python3 -B scripts/validate-v021-invariant-execution.py --report docs/evidence/0.2.1/invariant-final-df72405/functional.json --executed-source df7240556a260202a248e57b1f9868e372b9d995 --output docs/evidence/0.2.1/invariant-final-df72405/execution-verification.json
```

검사기는 실제 등록 이름과 Git 테스트 소스를 대조한다. 테스트 본문이 같으면 이전 94 보고서도
구조검사를 통과할 수 있으므로 이를 새 제품 지문의 실행으로 간주하지 않는다. 새 보고서의 실제 출처는
원래 run·원문 SHA·4개 DLL·소스 감사와 함께 확인한다. `--executed-source`는 호출자의 출처 선언이며
독립적인 과거 실행 인증이 아니다.

이 결과는 새 소스의 전체 기능 결과다. 기능 검사 소요 시간을 성능 표본으로 쓰지 않으며,
새 네 규모 성능·120개월 장기·성장/민감도·Windows/Android native·유지 서명/업그레이드·릴리스 합격은
각각 별도 근거가 필요하다. 과거 제품 지문 `528e62…`/`4e228…`의 성공·실패·부분 원문은 당시 출처로 유지한다.
