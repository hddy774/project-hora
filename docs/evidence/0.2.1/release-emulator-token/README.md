# 에뮬레이터 owner 검증의 토큰 환경 전달

기준 소스는 `b5526184aebc8152a87abc31a6d66888ed830bea`다. 출시 경로의 읽기 전용 사전점검에서 API24·35 실행 단계가 시작 전에 중단될 환경 연결 누락을 확인했고, root 승인으로 그 단계의 env 두 줄만 보완했다.

`v021-release-emulator.py:141`은 설치 전에 `verify.owner_context()`를 호출한다. 이 함수의 `github_read()`는 현재 Actions `GH_TOKEN`을 요구한다. 기존 워크플로의 토큰은 이전 APK 다운로드 step에만 있었으므로, 다음 `android-emulator-runner` action의 `with.script`로 실행되는 Python에는 전달되지 않았다. 이제 해당 action step에 `GH_TOKEN: ${{ github.token }}`을 직접 매핑한다. 원래 권한·가드·인증서·비밀 값·게이트는 바꾸지 않는다.

[token-map.json](token-map.json)에 모든 실제 API owner 검사 caller 6단계와 별도 이전 APK 다운로드 1단계의 step/job env·필요 읽기 권한을 대조했다. source/features/signing은 기존 step env, publish 검증과 세 게시 명령은 기존 job env, 에뮬레이터는 이번 step env를 사용한다. `windows`/`apk` 검증 CLI는 원천 일치만 확인하고 `owner_context()`를 호출하지 않는 분기이므로 토큰이 필요한 caller로 세지 않았다.

[checks.json](checks.json): 수정 전 누락1 → 수정 후0, actionlint1.7.12 exit0, 검증기·게시자·에뮬레이터 스크립트는 기준과 바이트 동일. 기능·성능 게이트 본문이나 cert/secret 값은 조회하지 않았다. 변경 파일은 제품 지문 입력이 아닌 release 워크플로와 문서뿐이며 선택 범위도 불변이므로 root의 FP528 입력은 유지된다. 전체 제품 지문 재계산·새 기능/플랫폼 검사·배포/World 실행은 하지 않았다.

이는 정적 연결 확인이다. 실제 명령 댓글·릴리스 워크플로·서명·APK 설치·공식 게시를 실행한 자료가 아니다. 실제 실행 시 `android_runtime`의 최초 owner 검증이 통과한 뒤, 같은 signed APK·API24/35·v0.2.0 저장 보존·초안/readback/latest 결과를 원래 절차에서 관찰해야 한다.
