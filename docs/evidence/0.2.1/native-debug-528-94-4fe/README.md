# 제품 지문 528의 네이티브 CI와 이전 실패 원형

이 자료는 성장 소스 7ce/제품 지문 4e228의 자료와 별개다. 이전 head는
`94ccb5d16f0c0d7bba0fd14aa8b0451dfe15bdd5`, 최신 head는
`4fe1fe4b95dcdef6cf8cb9e315fd99684f2bc135`이고 두 API24 source 기록의 제품 지문은
`528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`로 같다.

이전 제품 실행 41117917658의 Windows 작업 124276843674는 빌드 오류 0 뒤
기존 45초 Desktop watchdog에서 시간 초과했다. 원로그에는 종료 전 앱 진단이 없어
정확한 원인을 확정하지 않는다. 실제 checkout은 PR merge `cbf09633cb2022a4125e1001978c579b54d09a64`다.
이전 API24 실행 41117917684/작업 124276843196은 APK 빌드 오류 0 뒤 KVM 준비 단계에서
실패했다. 에뮬레이터·앱 실행 전이며 source/APK 메타데이터만 있다. udev trigger 직후
권한 검사 경합은 후보지만 당시 device stat/settle 기록이 없어 단정하지 않는다.

[최신 제품 실행 41120849698](https://github.com/hddy774/project-hora/actions/runs/41120849698)은
세 작업 모두 성공했다. `4fe-product/product-tests.json`에 실제 314/314가 있다.
Windows 124286751202는 실제 WPF WebView2 JSON `passed:true/actualRenderAndBridge:true`를 출력했다.
Android35 124286750791은 실제 API35와 렌더·오류 없음·GameSession 연결을 확인했다.
제품 CI의 실제 checkout은 PR merge `4ab393cfca5667b5763c7ad226656df1b77ff30c`다.
head4fe와의 차이는 문서와 EX 전용 workflow뿐이며 전체 tree가 같다고 주장하지 않는다.

[최신 API24 실행 41120849667](https://github.com/hddy774/project-hora/actions/runs/41120849667)의
작업 124286750681도 성공했다. 실제 checkout은 head4fe, stock WebView는
52.0.2743.100이며 변경되지 않았다. 새 설치·실제 DOM·브리지 연결을 확인했고
proof가 참조한 DOM/PNG/log 지문을 내려받은 원본과 직접 대조했다.
`runtime/fresh/dom-proof.json`의 접근성 텍스트 export=false라는 기존 한계도 보존했다.

`diagnostic-summary.json`과 `final-run-results.json`은 실제 독립 판독 결과의 바이트 동일 사본이다.
선택된 실패/Windows/provider 로그는 `.excerpt.txt`이고, 원로그의 선택 행을 변경하지 않았다.
원로그의 전체 SHA·원래 경로·행 번호와 전체 복사가 아니라는 범위는 `excerpt-provenance.json`에 있다.
ZIP·APK·PNG·인증서·전체 시스템/작업 로그는 복사하지 않았다. 원형 ZIP ID/지문과 로컬 원문 경로,
각 파일 지문은 artifact sidecar, 기존 진단 manifest 및 복사 manifest에서 구분한다.

이전 실패는 성공으로 재라벨하지 않았다. 이 진단에서 수동 재실행·제품/워크플로 수정·
검사 완화·timeout 변경·새 네이티브 실행은 0이다. 디버그 서명 새 설치의 조기 CI이며
공식 retained 인증서의 동일 APK API24/35·0.2.0 업그레이드·게시·장치 성능 검증을 대신하지 않는다.
