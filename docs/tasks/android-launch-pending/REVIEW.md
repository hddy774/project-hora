# 리뷰 안내

## 먼저 볼 변경

1. `scripts/android_readiness.py`의 `check_launch`: adb0·무오류·유일한 정확 Activity와
   유일한 `ok`/`timeout`만 인정한다. 반환값은 시작 대기 상태이며 합격값이 아니다.
2. `await_ready`의 `launch` 기록: raw status·adb 종료/외부 timeout·시작 대기 상태를
   `readiness.json`의 최종 `passed`와 분리한다. 기존 Reader와 반복·합격 조건을 대조한다.
3. `scripts/test-android-readiness.py`: 원본15개 Name을 유지하고 새9개 검사로 승인 경계를 확인한다.
   원래 verifier를 실제 subprocess로 호출하지만 adb·시계·후속 화면은 모의다.

[scope-comparison.json](checks/scope-comparison.json)에 f362 원본과의 AST 대조가 있다.
`Reader`, `inspect_dialog`, `foreground_app`, `main` 등7개 정의와 기존 준비 반복 전체가 동일하다.
기존15개 검사 중14개 본문은 동일하다. 나머지1개는 timeout 즉시 거부 하위 사례를 unknown 상태 거부로
바꾼 것뿐이며, 승인 정책을 바꿨다는 사실을 숨기지 않는다. 새9개가 지연 준비·90초 실패·기한 공유·
시작 오류·외부 timeout·최종 PID/PNG/전경/DOM/ANR 거부를 검사한다.

## 실행과 출처

- [원본15개 로그](checks/baseline-tests.log)·[소스/Name/SHA](checks/baseline.json)는 수정 전에 보존했다.
- 최종 Python24개 결과와 정적/진행도 검사 원문은 `checks/`에 연결한다.
- 최종 소스·제품 지문·파일 SHA와 원격/로컬 tree 대조는 `verification.json`을 읽는다.
- 실제 실패 fixture와 작은 원문은 [실패 증거](../../evidence/0.2.1/android-launch-pending/README.md)에 있다.
  기존 run은 실패이며 실제 앱이 이후 준비됐다는 주장이나 새로운 native 성공 증거는 없다.

## 범위·한계

작성·자체 검토는 Codex UI 담당이다. 루트의 독립 승인·통합은 별도다.
두 Python 파일 외 실행 로직·Game/Core/Hosting·workflow·원래 DOM verifier·공식 서명/업그레이드 판정은
수정하지 않았다. 테스트 파일은 기존15개를 그대로 추적하기 위해 같은 파일에 새 검사를 추가했다.

work-status의 단계·다음 행동·새 지문 대기 설명과 생성 진행도2파일만 추가 승인 범위로 갱신했다.
원장111개·완료52/111·validated56·진행 중 Z04/T04/T06·기존 정책·이전 원문은 그대로다.
새 지문의 전체323·32개 성능 지표·120개월·실제 API24/35는 루트 통합 후 검증 대상이다.
이 작업에서 Game/native/성능·성장·장기·수동 Actions 재실행은 수행하지 않았다.
