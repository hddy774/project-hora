# 설치 전 ADB·외부 Android 화면 준비

## 배경·승인·범위

[이슈266](https://github.com/hddy774/project-hora/issues/266)의 루트 승인 구현이다.
기준 소스는 `356c1cf6baf38b87477a63eee6f25c29f5baf2ce`/기존 제품 지문0eaf이며 실제 작성자는 Codex UI 담당이다.
run41178765537의 API35 설치는 return1·`adb: device offline`였고, 최종 캡처에 외부 Quickstep ANR이 있었다.
앱 시작·PID·DOM은 없었다. 두 현상의 인과는 미확정이며 원래 실패를 보존한다.

소유 범위는 `scripts/android_readiness.py`, `scripts/test-android-readiness.py`, 실제 실패 text/XML fixture,
이 작업 명세·증거뿐이다. VERSION·정책·Game/Core/Hosting·C# 검사·원장·release readiness·진행도는 바꾸지 않는다.

## 호출과 단일 시간 계약

`await_ready(..., install_apk=...)`인 product 분기만 같은 Reader의90초 안에서 다음 순서를 수행한다.

1. 이전 log 원형 보존 → ADB transport/boot 준비 → 현재 기기 XML/PNG/최종 focus 확인.
2. 정확한 외부 launcher ANR이면 기존 whitelist의 Wait만 누르고 새 관측을 수집한다.
   다음 회차에서 transport/UI를 다시 확인하며 stale 좌표를 재사용하지 않는다.
3. `adb install -r --no-streaming` 한 번 → log clear → AM 한 번 → 원래 최종 엄격한 준비 반복.

설치 방식은 이미 API24/공식에서 쓰는 `--no-streaming`과 맞춘다. 이 방식만으로 offline 원인이 해결됐다는
주장은 하지 않는다. 설치 재시도·adbd/에뮬레이터 재시작·데이터 삭제·고정 sleep·시간 연장은 하지 않는다.
원래 command timeout은 `min(남은 시간, 한도)`이며 설치/AM45초·probe10초는 같은90초에서 차감한다.

| 상태 | 처리 |
| --- | --- |
| get-state `device`와 boot `1` | transport/framework 준비 관측. 앱 합격이 아님 |
| get-state의 정상 `offline`, 또는 읽기 probe의 return1/255·빈 stdout·정확한 `adb: device offline`/`error: device offline` 한 줄 | 같은 deadline에서 대기 |
| boot의 정상 빈 값/`0` | 부팅 준비 미완료로 대기 |
| 다른 probe 값·stderr·OS 실행 실패·subprocess timeout | 즉시 실패. 설치/AM 없음 |
| 앱/unknown ANR·대화상자·충돌 | 기존 inspect_dialog로 거부, 자동 dismissal 없음 |
| 정확한 외부 Quickstep/Launcher3/Pixel Launcher ANR | 현재 소유자·제목·android Wait resource·활성·bounds가 모두 맞을 때만 Wait |
| stale dialog XML/ANR focus 불일치·아직 비어 있는 기기 화면 | 준비로 보지 않고 새 관측을 기다림. 좌표 재사용 없음 |
| 설치의 offline·다른 오류·timeout | 원문을 보존하고 실패. 두 번째 install 없음 |

설치 전에는 앱 PID를 조회/요구하지 않는다. `snapshot(require_app_pid=False)`만 기기 단계에 쓰며,
기존 기본값True의 앱 PID·전체 완전성 판정과 최종 while/DOM checker는 그대로다.
`preinstall.json`의 ready는 기기 준비이며, `readiness.json.passed`는 원래 앱 검사 뒤에만 true다.
Wait 영수증에는 `phase:preinstall`을 표시해 기기 캡처와 앱 준비를 구분한다.

## 보존하는 호출자

API24 workflow와 `v021-release-emulator.py`는 밖에서 별도 `--no-streaming` 설치 후
install_apk 없이 actual_render→await_ready를 호출한다. 이 호출·설치 순서는 변경하지 않는다.
공식 upgrade의 설치→UID/기존 저장 SHA 확인→현재 앱 시작 순서와 owner/서명/API/동일 APK 가드는 그대로다.

## 수용·회귀

- 수정 전 기존24개 통과 원문을 보존한다. 기존24개 Name과23개 본문을 유지했다.
  예산 fixture1개는 설치 전 launcher/AM 뒤 앱 미준비를 구분했고, 기존 모든 assertion·90초·남은44초는 유지했다.
- 실제 offline stderr20바이트·Quickstep XML·현재 focus·ANR 원형 행을 fixture로 사용한다.
  이후 기기 상태와 clock은 모의이며 실제 native 복구 성공으로 표시하지 않는다.
- 새16개 flow 검사로 transport/boot/UI 분리, 정확한 Wait, own/unknown/crash 거부, stale/late popup,
  같은90초 소진·55초 대기 후 남은35초·설치 오류/timeout의 재시도0·설치/AM 각1회·최종 PID/DOM/PNG를 확인한다.
- 원래 inspect_dialog/check_launch/main과 최종 while AST, command/deadline AST를 대조한다.
  기본 앱 snapshot의 required tuple을 펼치면 원래 PID 포함 완전성 식과 같다.

```sh
taskset -c 3 python3 scripts/test-android-readiness.py -v
git diff --check
```

실제 실행 소스·파일 SHA·원문은 [REVIEW.md](REVIEW.md)와 연결한다.
Game/빌드/native/성능·성장·장기·수동 CI 실행0이다. 첫 새 native는 코드 PR의 자동 CI로 구분한다.
제품 입력이 바뀌므로 루트가 메모리 최소 수정과 통합한 뒤 새 지문에서 전체 기능·32지표·원래120개월을
한 번 동결 검증한다. 여기서 원장·게시 준비나 공식 동일 서명 APK/업그레이드 수용을 열지 않는다.
