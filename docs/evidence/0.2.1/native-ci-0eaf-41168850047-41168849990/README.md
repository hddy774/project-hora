# 새 제품 지문의 네이티브 자동 실행 근거

제품 지문 `0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2`에서
API24와 단발 재실행의 API35가 원래 화면·연결 검사를 통과했다. API35 첫 설치 실패는 그대로 보존한다.
이 문서는 기존 CI를 읽어 수집한 근거이며 새 앱 실행·빌드·검사·재시도는 수행하지 않았다.

| 실행·범위 | 실제 job | 결과와 보존 원문 |
| --- | --- | --- |
| [API24 41168850047](https://github.com/hddy774/project-hora/actions/runs/41168850047), attempt1 | 124442258491 | 기본 WebView52 유지, 시작1회·관측8회·32.345초/90초, [원래 판정](api24/readiness.json)·[DOM/브리지](api24/dom-proof.json) |
| [제품 41168849990](https://github.com/hddy774/project-hora/actions/runs/41168849990), API35 attempt1 | 124442288537 | 설치 return1·`adb: device offline`, [stderr](api35-attempt1/install.txt.stderr.txt)·[launch:null/관측0](api35-attempt1/readiness.json)·[시작 호출0](api35-attempt1/commands.json) |
| 같은 제품 실행, API35 attempt2 | 124444644193 | 소유자의 실제 재실행1회, 설치 성공·시작1회·관측2회·21.212초/같은90초, [원래 판정](api35-attempt2/readiness.json)·[DOM/브리지](api35-attempt2/dom-proof.json) |
| 같은 제품 실행, portable 원래 attempt1 | 124442288556 | [원래 전체 보고서](portable/product-tests.original.json) `selection:all`, 323개·실패0, [소스/RESULT 로그](portable/source-and-result.excerpt.txt) |
| 같은 제품 실행, Windows 원래 attempt1 | 124442288464 | WPF WebView2의 `actualRenderAndBridge:true`, [소스·원래 JSON](windows/source-and-native.excerpt.txt) |

API35 첫 실패는 앱 시작이나 새로운 timeout 파서에 도달하기 전의 adb 전송 실패다.
내부 단절 원인은 미확정이다. 실패 원형을 새 성공으로 고치지 않았고, 전체 첫 실행이 종료된 뒤
루트가 요청한 단발 attempt2를 별도 결과로 기록했다. 최신 전체 workflow는 success다.
재실행 전의 요청 거절은 실제 실행으로 세지 않는다.

## 소스와 판정 경계

API24 실제 checkout은 `9e8df4b2813522343c01c523826597fcf7fee948`,
제품 CI 실제 checkout은 `0c8a64cc577009667e9ba5beedaa4e38ec0001c3`이다.
각 job의 실제 git-log와 GitHub Git commit API를 대조하여 전체 tree가
`eb3ecccfa61825a3875aa7811071b7c1d20ae776`로 같음을 확인했다.

문서 기준 개발 소스 `96ff2a858a50fb2b753943527a144c80080cd32c`는 CI와 문서 tree가 다르지만,
기존 fingerprint 함수로 같은 제품 지문을 확인했고 변경된 제품 입력은0개다.
[source-equivalence.json](source-equivalence.json)에 실제 source/tree·현재 소스·동등성 범위를 구분했다.
PR254 구현 검사 소스246·초기1cf 기록과 이전 f362의 실패 run41165712717도 그 원래 출처로 유지한다.

두 실제 성공의 Android 시작 상태는 모두 **`Status: ok`**다. 이번 native 결과로 실제 OS의
`Status: timeout` 이후 복구까지 확인했다고 주장하지 않는다. 그 분기의 Python24개 flow 검사는
PR254의 별도 모의 검사 근거다.

단일 시작·같은90초·살아 있는 PID·앱 resumed/최종 focus·완전한 캡처와 원래 DOM/브리지 검사를 유지했다.
API24는 원래 checker가7회 거부한 뒤1회 통과했고, API35는1회 거부 후1회 통과했다.
두 실행 모두 launcher Wait0이다. API24의 기존 wrapper 설치는 readiness 시계 이전이며,
API35 helper의90초에는 설치·시작·관측이 모두 포함된다.

## 최종 원형 화면

| 화면 | 바이트·관찰 |
| --- | --- |
| [API24 최종 PNG](screenshots/api24-final.png) | 150,046바이트. 앱 전경·주요 명령·본문이 보이고 외부 대화상자 없음 |
| [API35 attempt2 최종 PNG](screenshots/api35-attempt2-final.png) | 157,832바이트. 앱 전경·주요 명령·본문이 보이고 외부 대화상자 없음. 기존 [이슈251](https://github.com/hddy774/project-hora/issues/251)의 부제/statusbar 겹침은 계속 관측 |

합계307,878바이트로5MiB 미만이다. 두 파일은 원본과 바이트가 같으며 재촬영·편집·리사이즈·합성하지 않았다.
첫 실패의 큰 배경 PNG·중복 snapshot·APK·DLL·ZIP은 Git에 추가하지 않는다.

## 보관과 한계

[provenance.json](provenance.json)은 run/attempt/job·실제 상태·artifact ID/digest/만료와 범위를,
[retention-manifest.json](retention-manifest.json)은 원본/보존 파일 SHA와 완전 사본/선택 행을 구분한다.
전경·focus·render는 필요한 원형 행만 발췌했고, 전체 로그 SHA와 scratch 출처를 함께 남겼다.
발췌를 전체 로그나 전체 ZIP의 Git 보관이라고 주장하지 않는다.

수집한 원형 ZIP4개는 scratch에 유지한다. API24 artifact 만료는2026-11-14,
제품 runtime·portable artifact 만료는2026-11-03이다. 이 폴더는 만료 후에도 필요한 작은 원문·최종 PNG를
보존하며, 빠진 전체 원형을 SHA만으로 복원할 수 있다는 뜻은 아니다.

API24와 API35는 **서로 다른 CI의 디버그 서명 APK**다. 기록한 APK SHA는 CI 생산자 원문이며 수집자가
큰 APK를 내려받아 독립 계산하지 않았다. 유지 인증서의 동일 APK로 API24/35·0.2.0 업그레이드·공개 게시를
검증하는 공식 게이트는 별도로 남는다. 전체 사용성·장치 성능·111개 수용 완료로 확대하지 않는다.

수집·자체 검토: Codex UI 담당. 루트도 두 최종 PNG를 직접 확인했다는 보고와 수집자의 판독은 구분한다.
원장·release readiness·진행도3파일·제품·기존 증거는 변경하지 않았다.
