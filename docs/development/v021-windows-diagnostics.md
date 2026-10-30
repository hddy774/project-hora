# 0.2.1 윈도우 실패 진단 실행

이 경로는 [PR215 실패 원문](../evidence/0.2.1/windows-pr215-41132328410/README.md)의 정보 공백을 확인하는 소형 진단입니다. 기존 제품 CI와 공식 릴리스 판정은 그대로 유지합니다. 추적 이슈는 [#216](https://github.com/hddy774/project-hora/issues/216)입니다.

루트가 새 workflow PR의 정확한 HEAD를 검토·동결한 뒤 소유자 `hddy774`가 같은 저장소 PR에 **`0.2.1 윈도우 진단 실행`** 라벨을 한 번 붙입니다. `pull_request:labeled`만 받으며 synchronize 자동 재실행, 직접 dispatch, 자동 재시도는 없습니다. 작성자는 라벨을 붙이거나 실행하지 않았습니다.

workflow 정의의 PR/event SHA와 실제 빌드 소스를 구분합니다. 실제 checkout은 `35f33db919f910c9d55c42b61b76459a08688d12`로 고정하고 기존 `verify-v021-release.py:fingerprint()`를 바꾸지 않고 호출해 제품 지문 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e` 및 clean 상태를 검사합니다. SDK10.0.403을 사용하고 실제 런타임 목록을 남깁니다.

`windows-latest`의 기존 Release Desktop 빌드와 `--smoke-test`를 한 번 실행합니다. 기존 제품 smoke의 `WaitForExit(45000)`, exit code, 실제 report 읽기 판정을 유지합니다. 이미지 pin, WebView 자동 설치/업데이트, 기능 값/저장/테스트 변경은 없습니다.

성공·실패 모두 14일 artifact에 요청/소스/도구 버전과 다음 진단을 보존합니다.

- stdout/stderr 원문, wrapper 시작·종료·timeout·exit 관측, 앱/살아 있는 WebView 프로세스 상태와 실행파일 버전
- 허용한 `ImageOS`/`ImageVersion`/`RUNNER_OS`, OS 설명, WebView2의 공개 `pv` 레지스트리 및 설치 실행파일 버전
- 실제로 존재하는 `seonro-smoke-result.json`과 이번 실행 이후 갱신된 앱 startup `debug-auto-*.log`만 복사한 원문/해시

현재 고정 앱은 단계별 네이티브 초기화 trace를 쓰지 않습니다. 새 `wrapper-observation.json`은 wrapper 관찰이고 앱 내부 trace인 것처럼 표시하지 않습니다. 버전/파일 부재와 접근 실패는 미상으로 남기고 결과를 합성하지 않습니다. Edge 목록 버전을 WebView2 버전으로 대체하지 않습니다. 게임 저장 슬롯·환경 전체·자격 증명·키스토어는 복사하지 않습니다.

이 실행은 원인 진단이며 전체 기능/성능, 공식 서명 APK의 API24/35 동시 검증, 기존 인증서 유지, 0.2.0 덮어쓰기 업데이트, 게시를 대체하지 않습니다. 공식 릴리스 Windows 60000ms 단계의 실패 진단 패치는 별도 scratch 검토안이며 이 PR은 그 workflow를 수정하지 않습니다.
