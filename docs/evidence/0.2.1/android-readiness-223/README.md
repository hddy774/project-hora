# Android 준비 확인223: 짧은 기능·정적 검증

기준 `404767ad4dc576ea3669cddd42529a50741cbf6f`, [이슈223](https://github.com/hddy774/project-hora/issues/223), [작업 계약](../../../tasks/223-android-ci-readiness.md). 제품 CI·공식 release의 실제 화면 수집·API24 probe가 공통 `scripts/android_readiness.py`를 사용한다. 기존 `scripts/verify-android-ui.py`는 바이트 동일하며 실제 subprocess로 호출한다.

[checks.json](checks.json)에 CPU3에서 실행한 Python12/12, actionlint1.7.12, 기존 release guard/기획/제품 경계 self-test, 원문 로그 SHA를 기록했다. 테스트 입력의 Quickstep XML은 기존 artifact13015628987의 원문을 복사했다. fake ADB를 통한 parser/flow 검사이며 실제 Android·APK·전경·기본 WebView 버전 합격을 새로 증명하지 않는다. fixture의 checker 호출은 원래 UI 검사기를 실제 subprocess로 실행한다.

검사 경계는 정상 am start -W/요청 Activity, 외부 Quickstep Wait 전후 원문과 기존 verifier 합격, 본앱/unknown/다른 owner ANR 거부, 본앱 crash, launch stderr 오류, 다른 전경·깨진/다른 package XML, 잘못된 DOM/stale marker, Wait/ADB timeout 뒤 deadline 비재설정이다. 원래 renderer/DOM predicate는 낮추지 않았다. unknown/본앱 대화상자에는 tap이 없고 성공 proof도 생기지 않는다.

[protected-wiring.json](protected-wiring.json)은 공식 `adb/package_state/launch/old_world_proof/retained_upgrade/main` 6함수, `actual_render`의 APK 해시 가드, verify/publish/UI checker/release workflow 4파일이 기준과 바이트 동일함을 기록한다. API24 source·APK·KVM 생산자 run block도 그대로이며, 생성 probe의 AST는 SDK/comment 외 동일하다. SDK는 소유 product3/API24한 곳을 10.0.403으로 고정했다. global.json과 release workflow SDK는 루트 소유다.

공통 reader는 90초 deadline 하나를 install(제품 CLI)·launch·모든 poll·Wait·최종 진단에 공유한다. Wait 후 시간을 재설정하거나 앱을 재실행하지 않는다. 각 관측은 logcat/UI/foreground/window/PID/screenshot을 남기며 Wait 번호·UTC 시작/종료·전후 경로·capture 완결 여부를 기록한다. deadline이나 ADB 장애 시 원문이 부분적일 수 있으며 명령 returncode/timeout과 마지막 deadline 안의 수집본을 보존한다. 불완전 수집이나 deadline 이후에는 합격하지 않는다.

새 source의 실제 플랫폼 검증은 일반 자동 제품 CI/API24에서 별도로 확인할 대상이다. 이 작업의 로컬 Android/ADB·서명/공식 릴리스·성능/성장/장기 실행은 0회다. 새 scripts 입력으로 제품 지문이 달라지므로 기존 FP528 증거를 현재 source 합격으로 재라벨하지 않는다. 최종 same signed APK API24/35·기존 인증서·0.2.0 원본 보존·게시 검증은 그대로 필요하다.
