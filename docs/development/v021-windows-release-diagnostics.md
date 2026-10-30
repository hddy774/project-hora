# 공식 Windows 릴리스 실패 진단 보존

공식 `v021-release.yml`의 Windows smoke는 기존60000ms·앱1회·exit/report 조건·`verify-v021-release.py windows` 호출을 유지합니다. 이 변경은 timeout을 늘리거나 결과를 합성하지 않고 stdout/stderr와 wrapper 시작/종료·timeout·프로세스 상태·허용한 이미지/OS/WebView2 버전만 기록합니다.

`finally`는 존재하는 temp smoke JSON 및 이번 실행 이후 갱신된 기존 앱 startup `debug-auto-*.log`만 `artifacts/windows/diagnostics/`로 복사합니다. 환경 전체·비밀·키스토어·게임 저장 슬롯을 복사하지 않고 screenshot 기능도 추가하지 않습니다. 기존 always artifact 업로드 단계와14일 보존은 그대로입니다.

정상 경로의 `native-smoke.json`/`windows-proof.json`과 diagnostics의 raw를 구분합니다. timeout 뒤 뒤늦게 true JSON이 남아도 실패는 그대로이며 proof를 새로 만들지 않습니다. [실제3VM 근거](../evidence/0.2.1/windows-batch-41136183920/README.md)의 sample3이 이 구분의 구체 사례입니다. 빈stdout/stderr·미상버전·진단파일 부재도 성공 근거가 아닙니다.

변경 기준은 env2행 leaf가 합쳐진3d11273이며 다른 Windows조건, Android단계/인증/업데이트/게시 경로와 제품 지문 선택은 수정하지 않았습니다. 공식 릴리스 실행·추가 진단배치·재실행은 하지 않았습니다.
