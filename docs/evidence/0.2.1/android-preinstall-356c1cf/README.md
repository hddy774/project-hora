# 설치 전 offline·외부 Quickstep ANR 원형

기준 head356c1cf/기존 제품 지문0eaf의 [run41178765537](https://github.com/hddy774/project-hora/actions/runs/41178765537),
attempt1/job124471716168/단계11은 실패다. 실제 checkout은c5f6220이며 요청 head와 전체 tree가 같다.
[source-comparison.json](source-comparison.json)은 실제40자 SHA/tree와 지문 보고·읽기 대조 범위를 구분한다.

- 설치 stdout은 비어 있고 stderr는 정확히 `adb: device offline`, return1·subprocess timeout 없음이다.
  결과 기록은 Reader 시작 후63.361ms이며 최종 진단 포함3.950초/원래90초다.
- [명령 원형](commands.original.json)에 AM0·DOM checker0, [판정 원형](readiness.original.json)에
  passed:false·launch:null·관측0·Wait0이 남아 있다.
- 최종 XML·PNG·focus는 Quickstep ANR, Android log도 com.android.launcher3의 입력 배달 timeout을 기록했다.
  앱 PID·패키지 로그·SeonroRender·FATAL 증거는 캡처 창에서 없었다. 앱 runtime 원인은 입증되지 않았다.
  offline과 외부 ANR의 인과·전송 단절의 내부 원인은 미확정이다.

실제 stderr20바이트·XML4,146바이트·현재 focus 원형 행·ANR 원형 행은 scripts/fixtures에 둔다.
[보존 manifest](retention-manifest.json)에 원본 경로·바이트·SHA·선택 행 범위를 적었다.
새 모의 flow는 이 원형 뒤에 가상 기기 상태/clock을 조합하므로 실제 native 회복 성공을 뜻하지 않는다.

artifact13034136890은1,766,876바이트이며 SHA256은
`a8ee56905470f05459e55e2d7e1baf0c584270d16480f71ee1904cdb82f6b98a`다.
한 번 내려받아 서버 digest와 대조했고 원래 ZIP/PNG/전체 로그는
`/workspace/scratch/native-356c1cf-41178765537-attempt1/`에 유지한다.
만료 시각은 [artifact metadata](artifact-metadata.json)의2026-11-03T07:02:38Z다.
큰 ZIP·PNG·APK·DLL은 Git에 추가하지 않았으며 전체 원형의 외부 영속 보관 완료로 표시하지 않는다.

[이슈266](https://github.com/hddy774/project-hora/issues/266)의 새 설치 전 gate는 별도 구현 소스다.
[명세](../../../tasks/android-preinstall/TASK.md)·[리뷰 안내](../../../tasks/android-preinstall/REVIEW.md)에서
같은90초/설치1회/AM1회·기존 최종 기준과 호출자 범위를 확인한다.
수집과 작성 중 새 Game/native/성능·장기·수동 CI 실행·원형 재라벨은0이다.
