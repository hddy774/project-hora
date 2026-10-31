# Android 시작 대기와 최종 화면 준비 구분

## 배경과 승인

- 이슈: [#253](https://github.com/hddy774/project-hora/issues/253), 상위 PR84.
- 작성·구현: Codex UI 담당. 루트가 실제 API24 실패를 읽고 명시적으로 승인한 제한된 수정이다.
- 기준 소스: `f3627aa4db4282a8aebb572bffe2048261e0756a`, 기존 제품 지문
  `c91c3d7b7cf0441f207825d0490d38501072b63b0364234fbd7cf973b1f620f1`.
- 실패 [run 41165712717](https://github.com/hddy774/project-hora/actions/runs/41165712717),
  job124432709700 단계12에서 adb는0, Android는 `Status: timeout`과 정확한 Activity를 반환했다.
  기존 helper는 관측 반복 전 거부했다. 앱 PID는 살아 있었지만 회색 화면·전경 focus 미확인·DOM 미확인이었다.
  앱 초기화·런타임·호스트 중 지연 원인은 미확정이며, 원래 실패는 바꾸지 않는다.

## 범위와 계약

수정 대상은 `scripts/android_readiness.py`, `scripts/test-android-readiness.py`, 정확한 launch fixture와
이 작업의 작은 문서·증거다. 루트의 추가 승인에 따라 work-status와 생성 진행도 두 파일도 갱신한다.
Game/Core/Hosting·workflow·원래 verifier·원장111개·readiness 릴리스 선언은 수정하지 않는다.

| 입력·상태 | 결과 |
| --- | --- |
| adb0·Error/Exception 없음·정확한 Activity·유일한 `Status: ok` | 시작 대기 완료를 기록하고 원래 준비 관측 계속 |
| 같은 조건의 유일한 `Status: timeout` | 시작 대기 미완료를 기록하고 원래 준비 관측 계속 |
| 비0·다른/누락/중복 Activity·오류·미확인/누락/중복 status | 즉시 실패, raw 보존 |
| subprocess TimeoutExpired·OS 실행 실패 | 즉시 실패, 부분 stdout/stderr·종료 상태 보존 |
| 최종 화면 준비 | 살아 있는 PID·앱 resumed/현재 focus·완전한 XML/PNG·앱 WebView·원래 DOM/bridge/version 검사 모두 필수 |
| 앱/알 수 없는 ANR·충돌 | 기존 즉시 거부 유지; 정확한 외부 launcher Wait만 기존 규칙 적용 |

`Reader`가 log 수집·선택 설치·log clear·단일 `am start -W` 이전에 시작한 **동일90초**를 유지한다.
시간을 재설정하거나 추가 시작/재시도·제한 연장을 하지 않는다. `readiness.json`의 `launch`는 adb/Android
시작 대기 상태이고, 최종 `passed`는 기존 준비 검사 결과다. 시작 timeout만으로 합격을 쓰지 않는다.
세계 값·난수·저장 형식·공식 동일 서명 APK/API24·35/0.2.0 업그레이드 계약의 변경은 없다.

## 수용 기준과 검사

1. 원본15개 Python 검사 통과를 먼저 보존한다. 같은15개 Name과 다른 거부 조건을 유지한다.
   이전 timeout 즉시 거부 하위 사례만 승인된 계약에 맞춰 unknown 상태 거부로 바꾼 사실을 명시한다.
2. 실제 launch stdout196바이트를 그대로 fixture로 사용한다. 이후 화면은 모의 응답임을 구분한다.
3. 지연 후 원래 `verify-android-ui.py` 통과·준비 없음의 정확90초 실패·설치/시작 경과 포함을 검사한다.
4. 비0·다른 Activity·오류·미확인 상태·subprocess timeout/OS 실패·PID/PNG/전경/DOM/ANR/충돌 거부를 확인한다.
5. 시작 횟수1·기존 관측 반복/최종 합격 분기·Reader 시계가 그대로인지 AST로 대조한다.

```sh
taskset -c 3 python3 scripts/test-android-readiness.py -v
python3 scripts/update-v021-progress.py
python3 scripts/update-v021-progress.py --self-test
python3 scripts/validate-v021-plan.py --self-test
python3 scripts/validate-v021-plan.py
git diff --check
```

Python mock/parser/flow 검사와 정적 대조만 수행한다. 새 native·Game·성능·성장·장기 실행, 수동 CI 재실행은0이다.
기존 scripts가 제품 지문 입력이므로 새 지문이 생긴다. 루트가 통합 후 새 전체323·32지표·120개월과 실제
API24/35를 확인하며, 과거7901/660 성공은 원래 소스의 근거로 보존한다. 완료율52/111과 상태56/3은 유지한다.

## 증거와 리뷰

- [원래 실패·보관 범위](../../evidence/0.2.1/android-launch-pending/README.md)
- [원본15개 결과](checks/baseline-tests.log), [원본 소스·Name·SHA](checks/baseline.json)
- 최종 검증·AST 대조·소스 지문과 수행 한계는 [리뷰 안내](REVIEW.md)에 연결한다.
- 작성자 자체 검토와 루트 독립 검토를 구분한다. 실제 장치 통과나 최종 릴리스 수용으로 확대하지 않는다.
