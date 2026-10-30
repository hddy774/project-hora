# Android 제품 CI: 렌더 관측과 전경 검사 실패 보존

기존 [제품 CI run41136418480](https://github.com/hddy774/project-hora/actions/runs/41136418480)의 [Android job124339878666](https://github.com/hddy774/project-hora/actions/runs/41136418480/job/124339878666)은 **실패**했다. 빌드와 설치는 성공했고 앱은 렌더 성공 표식을 남겼지만, 이후 전경 UI 검사에서는 시스템의 Quickstep 응답 없음 대화상자만 접근성 트리에 나타났다. 이 자료는 실패를 성공으로 바꾸거나 Game/렌더 실패로 원인을 단정하지 않는다.

[PR219](https://github.com/hddy774/project-hora/pull/219)의 head는 `c61b05d21140772a366624324c1aea160997b6b5`다. 실제 job checkout은 `3d11273bfa3daa90d34cf31e1422b9e5f21c0bfa`와 이 head를 합친 PR merge `fc4c2a954e19286cda316465ebd220c8e69809cf`다. API의 head_sha와 실제 빌드 checkout을 구분했으며 [전체 job 로그](raw/job.log)의 fetch·checkout·git log 원문에 근거한다. run/attempt는 `41136418480/1`, 이미지 설정은 API35·default·x86_64·pixel_6이다.

| 원문 관찰 | 결과 |
|---|---|
| 빌드 step / 22:14:22.388Z 설치 로그 | 빌드 success, `adb install -r`의 `Success` |
| 기기 logcat `10-30 22:14:25.674`, runner 수집 22:14:42.511Z | `rendered:true`, heading `만상의 흐름`, title `선로만상록`, version `0.2.1`, error `null`, connection `연결됨` |
| 22:14:44.495Z 출력된 UI 덤프 | 모든 node의 package가 `android`. `Quickstep isn't responding`, `Close app`, `Wait`만 텍스트로 나타나며 앱 WebView node 없음 |
| 원본 PNG 직접 확인 | 앱 화면 위에 같은 Quickstep 대화상자가 덮인 모습 |
| 22:14:45.421Z 검사 | `verify-android-ui.py:7`의 `Foreground app WebView missing` assertion, 실행 step exit1 |
| 후속 업로드 | APK 업로드는 skipped, runtime evidence 업로드는 success |

[render 원문](raw/android-render-log.txt), [UI XML 원문](raw/android-ui.xml), [API metadata](metadata/), 실제 checkout에서 읽은 [제품 workflow](source/v021-product.yml)와 [검사기](source/verify-android-ui.py)를 변경 없이 저장했다. 검사기는 전경 WebView assertion을 먼저 수행하므로 뒤의 렌더 판정까지 통과한 것으로 기록하지 않는다. 전체 job 로그는 연결 도구가 디코딩한 content를 추가 편집 없이 UTF-8로 저장했으며, 그 바이트의 SHA를 남겼다. GitHub 마스킹이 이미 반영된 로그다.

원본 artifact13015628987 ZIP은 **141,030B**, SHA256 `d6ef5c831ce3ae395e943f6f46fe66e4c536150ce5157cf53f5d7c6e5db3c97e`이며 서버 digest와 일치했다. ZIP의 파일은 render 204B, XML 4,146B, PNG 152,005B 세 개다. [manifest.json](manifest.json)에 각 파일·저장 로그·메타데이터·source SHA, PNG 크기/해시/시각 확인, ZIP 출처를 기록했다. ZIP과 PNG 원형은 `/workspace/scratch/android-pr219-job124339878666/`에 보존했고 이 PR에는 바이너리를 넣지 않았다. GitHub artifact의 기록된 만료일은 2026-11-02이며 만료 후 원형 접근은 보존본에 의존한다.

Quickstep ANR의 원인과 발생 경위는 이 자료로 확정할 수 없다. 새 플랫폼 실험·수동 workflow 실행·재시도·앱/World 실행은 0회이며 제품·workflow·게이트·timeout을 바꾸지 않았다. 이 일반 제품 CI 자료는 공식 retained 서명 APK의 동일 바이트 API24/35·0.2.0 덮어설치 검증을 대신하지 않는다. 공식 릴리스 실행도 0회다.
