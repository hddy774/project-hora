# API24 시작 대기 실패와 보완 범위

[실패 실행41165712717](https://github.com/hddy774/project-hora/actions/runs/41165712717)의
job124432709700 단계12 원형은 실패로 유지한다. 실제 소스 f3627aa·제품 지문 c91은
[원래 source.json](failure/source.original.json)에 있다.

adb 종료0·subprocess timeout 없음이지만 Android는 `Status: timeout`, 정확한 Activity,
`WaitTime: 10094`를 보고했다. [원래 준비 결과](failure/readiness.original.json)는
`passed:false`, 관측 반복0, launcher Wait0이다. [호출 원문](failure/commands.original.json)과
[실패 traceback 발췌](failure/failure-traceback.excerpt.txt)를 함께 보존한다.
이 발췌는 수집한 로그에서 필요한 줄만 뽑은 자료이며 전체 로그 파일이라고 주장하지 않는다.

정확한 launch stdout은 [회귀 fixture](../../../../scripts/fixtures/android-launch-timeout-api24-41165712717.txt)로
바이트 그대로 보존했다. 원형 회색 PNG는 scratch/아티팩트에 있으며 이 PR에 이진 파일을 추가하지 않는다.
PID2759·앱 resumed 기록은 있으나 currentFocus는 null, DOM/bridge 기록은 없었다.
이 원문에서 Quickstep/앱 ANR·확정 충돌은 관측하지 못했고 지연의 내부 원인은 미확정이다.

루트는 [#253](https://github.com/hddy774/project-hora/issues/253)에서 정확한 adb/Activity/무오류 조건의
Android timeout만 **시작 대기 미완료**로 구분하도록 승인했다. 이후 합격은 여전히 단일90초 안에서
PID·전경·WebView·원래 DOM/bridge·완전한 캡처 검사로만 판정한다. 기존 실패를 새 성공으로 바꾸지 않는다.
새 Python 검사에서 fixture 이후의 화면 응답·시계는 모의이며 실제 API24가 나중에 준비됐다는 증거가 아니다.

원본 ZIP artifact13028898344는115,518바이트,
SHA256 `9c56be71848c58a0399d93daab9a86a1d10beffcf4a1cf4b234c9635e5758adf`이며
서버 digest와 대조했다. Actions 만료 시각은2026-11-14T03:48:07Z다.
전체 ZIP은 `/workspace/scratch/native-f3627aa-41165712717/native-evidence.zip`에 보존했고
이 Git 문서는 필수 소형 원문만 담는다. 전체 ZIP의 영속 외부 보관 완료를 주장하지 않는다.

[failure-index.json](failure-index.json)은 파일별 출처·바이트·SHA와 Git/scratch 보관 범위를 구분한다.
새 구현·검사 결과는 [작업 명세](../../../tasks/android-launch-pending/TASK.md)와 별도 source로 기록한다.
공식 retained 서명·동일 APK·API24/35·0.2.0 업그레이드 및 최종 안정성 수용과는 별도다.
