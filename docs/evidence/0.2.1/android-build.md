# Android 공용 화면 검증

0.2.1 개발 APK: .NET SDK 10.0.403 / Android workload 36.1.118 / JDK 21 /
Android SDK 36, build-tools 36.0.0. arm64-v8a·x86_64를 같은 APK에 포함한다.

Release 빌드 경고0·오류0. 현재 생성물은 검증용 기본 서명이며 최종 릴리스 서명과 구분한다.
`GameSession`만 로컬 WebView 브리지에 노출하고 플레이 조회는 개인 인지를 거친다.
앱 중단은 세계 일시정지와 저장이며 수행 공정 취소 명령이 아니다.

공용 셸의 Chromium 검증: 1280px·390px에서 9개 메뉴, 인물 선택/빙의,
플레이 법칙 메뉴 숨김, 채집·시간 진행·저장, JavaScript 오류0·가로 넘침0.
재현: `scripts/verify-v021-ui.py`.

Android 실제 설치/실행과 실기기 성능은 별도 근거를 요구한다.
브라우저 검증으로 Android 실행을 대체하지 않는다. 전체 111개 작업의 완료 증거가 아니다.
