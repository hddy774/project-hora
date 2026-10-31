# 0.2.1 윈도우 환경 고정3회 관측

이 경로는 [PR215 원인 불명 timeout](../evidence/0.2.1/windows-pr215-41132328410/README.md)의 정보 공백을 확인합니다. [첫 실제 실행](../evidence/0.2.1/windows-diagnostics-41134808459/README.md)은 이전 이미지에서 성공했으며 그 원형93b28과 원문을 유지합니다. 추적 이슈는 [#216](https://github.com/hddy774/project-hora/issues/216)입니다.

루트가 첫 결과 뒤 사전 승인한 후속은 **새 VM3개 고정 배치1회(sample1/2/3)**입니다. 루트가 새 HEAD를 검토·동결한 뒤 소유자 `hddy774`가 같은 저장소 PR에 **`0.2.1 윈도우 환경 3회 관측`** 라벨을 한 번 붙입니다. 기존 단일실행 라벨은 더 이상 트리거가 아닙니다. `pull_request:labeled`만 받고 synchronize/dispatch/자동 재실행은 없습니다. fail-fast=false이므로 실패가 나도 다른 샘플은 수집합니다. 어느 이미지가 배정돼도3개 모두 보존하며 이미지 선별·성공까지 반복하지 않습니다.

실제 checkout은 `35f33db919f910c9d55c42b61b76459a08688d12`로 고정합니다. 기존 `verify-v021-release.py:fingerprint()`를 바꾸지 않고 호출해 지문 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`와 clean을 확인합니다. workflow 정의/event SHA와 실제 빌드 소스는 별개로 기록합니다. SDK10.0.403과 실제 런타임 목록을 남깁니다.

각 VM은 기존 Release Desktop 빌드와 `--smoke-test`를 한 번만 실행하고 `WaitForExit(45000)`·exit code·실제 report 읽기 판정을 유지합니다. sample을 요청/checkout 밖 결과 경로/artifact 이름에 넣습니다. 결과는 성공·실패 모두14일 보존하며 첫 실행과 합쳐 성공률이나 성능을 주장하지 않습니다.

- stdout/stderr 원문, wrapper 시작·종료·timeout·exit, 앱/살아 있는 WebView 프로세스 상태·실행파일 버전
- 허용한 ImageOS/ImageVersion/RUNNER_OS, OS 설명, WebView2 공개 pv 레지스트리·설치 실행파일 버전
- 존재하는 smoke JSON 및 이번 실행 이후 갱신된 앱 startup debug-auto 로그만 복사한 원문/해시
- timeout 뒤 kill 전에 **같은 테스트 앱 PID의 현재 visible 창/대화상자만** bounded WM_PRINT(창당1000ms)로 가능한 경우 PNG 관측. 전체 화면 픽셀이나 다른 앱 창은 수집하지 않음

캡처 부재·오류·내용을 읽을 수 없는 이미지가 원인 규명이나 성공을 뜻하지 않습니다. 캡처 오류는 진단 오류로 남고 이미 결정된45초 timeout은 그대로 실패입니다. 앱의 현재 소스는 단계별 초기화 trace를 쓰지 않으므로 wrapper-observation을 앱 내부 trace라고 표시하지 않습니다. Edge 공개 목록을 실제 WebView2 버전으로 대체하지 않습니다.

새 제품 코드·validator·FP 선택·기존 product/release gate·timeout·WebView 자동 설치/업데이트·이미지 pin 변경은 없습니다. 환경 전체/비밀/키스토어/게임 저장 슬롯을 복사하지 않습니다. 보유 인증서·동일 APK API24/35·0.2.0 업데이트·최종 게시 검증은 공식 릴리스 경로에 그대로 남습니다. 공식 Windows60000ms 진단 보존 패치는 이 배치 결과 뒤 별도로 다룹니다.
