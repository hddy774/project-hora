# 223: Android CI 실제 준비 확인

추적: [이슈223](https://github.com/hddy774/project-hora/issues/223). 기준 소스 `404767ad4dc576ea3669cddd42529a50741cbf6f`. 기존 실패 원문은 PR221의 `android-quickstep-41136418480`에 보존돼 있다.

제품 CI는 설치 후 sleep20과 단일 UI 덤프를 사용했다. 실제 앱 렌더 표식이 있어도 외부 Quickstep ANR 창이 전경을 덮으면 검사에 실패했다. 정상 launch와 실제 앱 준비를 관측하고 명확한 외부 런처의 Wait만 처리한다. 실패를 합격으로 바꾸거나 원래 UI 기준을 낮추지 않는다.

수용 계약:

- `am start -W` exit/Status/실제 요청 Activity를 확인하고 한 번만 launch한다.
- 공통 reader의 단일 90초 deadline에 설치(제품 CLI)·launch·poll·Wait·진단을 포함한다. 반복이나 Wait 뒤에 시간을 재설정하지 않고 deadline 뒤에는 합격하지 않는다.
- 앱 PID·실제 resumed Activity/current focus와 기존 `verify-android-ui.py`의 WebView/DOM/version/native GameSession 판정을 함께 통과해야 성공한다.
- Android 시스템 ANR 대화상자의 명시된 외부 런처 제목(Quickstep/Launcher3/Pixel Launcher), 실제 Wait resource/버튼/bounds만 허용한다. ANR window에 process가 명시됐다면 같은 런처여야 한다. 본앱 ANR·크래시·unknown 대화상자는 거부하며 Close app이나 임의 버튼은 누르지 않는다.
- Wait마다 번호·시각·횟수·선택 resource/bounds와 전후 raw 경로·수집 완결 여부를 기록한다. 실패 시에도 수집된 logcat/UI/foreground/window/screenshot 및 명령 결과를 보존한다. deadline을 넘으면 그 안에서 마지막으로 수집한 원문을 보존한다.
- 제품 CI·release.actual_render·API24 probe는 같은 reader를 사용한다. 공식 owner/source/sign/publish와 retained APK/hash·0.2.0 원본 보존 함수는 변경하지 않는다.
- 소유 product 3곳/API24 1곳 SDK는 10.0.403. global.json과 release workflow SDK 3곳은 루트 소유다.

검증은 외부 Quickstep Wait→기존 실제 verifier 통과, 본앱/unknown/owner-mismatch 거부, launch 실패, deadline·명령 timeout, 기존 verifier의 잘못된 DOM 거부를 짧은 Python parser/flow fixture로 확인한다. 정적 actionlint·AST·보호 함수/파일 동일성도 기록한다. fixture는 실제 Android 설치·전경 검증을 대신하지 않는다. 새 source의 자동 제품 CI/API24 결과는 별도 기록하며 기존 FP528 증거를 새 source 합격으로 재라벨하지 않는다.

수행/전투/Game/법칙·계수·성능/성장/장기 실행 변경은 없다. 공식 릴리스는 루트의 최종 통합 검증 뒤 실행한다.

KVM 후속 승인: API24의 기존 host/문자장치/r+w 조건은 그대로 두고 reload→trigger→10초 상한 settle 순서를 확인하며 단계별 exit/id/stat을 남긴다. 실패 code는 그대로 전파하고 chmod 우회는 없다. 이전451 KVM 실패의 정확한 원인은 미확정으로 보존한다.

실제 query 경계: API35의 `dumpsys window windows` 원문은 mCurrentFocus를 포함하지 않았다. 전경 조건은 유지하고 전체 `dumpsys window`로 조회해 current focus를 수집한다. 이전451의 실패/원문과 기존 verifier 오프라인 재입력 성공은 별개로 보존하며 새 source 자동 플랫폼 합격으로 재라벨하지 않는다.

캡처 순서: current focus는 UI/screenshot 뒤에 수집해 늦게 나타난 dialog가 이전focus로 합격하지 않도록 한다. Wait는 아직현재focus가같은launcher일때만허용한다. Wait후dialog없는ready관측을즉시같은verifier로검증할수있지만dialog관측을재사용해tap하지않는다. a0e8의jobsuccess와ANR포함PNG는둘다원문보존하고clean프레임합격으로합성하지않는다.
