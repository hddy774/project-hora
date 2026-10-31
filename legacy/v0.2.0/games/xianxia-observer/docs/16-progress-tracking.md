# 16. PR 진행표 갱신 방법

사용자 요청의 실시간 갱신은 **작업 상태 변경 명령을 실행하면 PR 본문에 즉시 반영**하는 방식이다.
상태 원본은 [xianxia-progress.json](../../../docs/tasks/xianxia-progress.json), 기본 게시 대상은 [대표 PR #32](https://github.com/hddy774/project-hora/pull/32)다.
현재 계획은 [14](14-development-plan.md), UI 명세는 [15](15-ui-ux-improvement.md)를 따른다.

## 1. 상태와 갱신 시점

| 시점 | 상태 | 기록 |
|---|---|---|
| 착수 직전 | `in_progress` | 작업자·현재 목표를 note에 기록 |
| 구현 커밋 후 검사 시작 | `verifying` | 구현 PR/커밋과 검사 범위 |
| 수용 기준·검사 통과 | `done` | evidence에 PR·CI·커밋 근거 |
| 의존·환경·결정 대기 | `blocked` | 이유와 재개 조건을 note에 기록 |
| 차단 해제 | `planned` 또는 `in_progress` | 해소 근거 |
| 완료 후 회귀 발견 | `in_progress` | 기존 근거와 새 문제 설명 |

기반·기획 완료를 신규 게임 개발14건 완료율에 더하지 않는다. 기획 완료와 코드 완료를 구분한다.
명세와 원본의 `approval`을 사용자 승인 근거로 함께 갱신한다. 이미 받은 승인을 다시 요청할 필요는 없다.
승인 대기 작업과 선행이 예정/개발 중/차단인 작업의 착수를 거부한다.
선행 구현이 끝나 검증 중이면 서브 PR 개발·검증을 병렬로 시작할 수 있다. 후행 완료는 선행이 모두 완료여야 한다.
검증을 건너뛴 완료와 근거 없는 완료도 거부한다. 선행 회귀가 발견되면 연결된 후행의 병합을 보류한다.

## 2. 명령

저장소 루트에서 Python3와 기존 인증이 설정된 GitHub CLI(`gh`)를 사용한다.
도구: [xianxia-progress.py](../../../tools/xianxia-progress.py). `show`는 읽기, `set`은 기본적으로 로컬 파일만 변경한다.

```sh
python3 tools/xianxia-progress.py show
python3 tools/xianxia-progress.py set XO-06 --status in_progress --note "관심 A/B 회귀와 로그 난수 분리 구현" --publish
python3 tools/xianxia-progress.py set XO-06 --status verifying --note "구현 후 전체 검사 중" --evidence "<구현 PR URL>" --publish
python3 tools/xianxia-progress.py set XO-06 --status done --evidence "<수용 기준과 CI를 확인할 URL>" --publish
python3 tools/xianxia-progress.py publish
python3 tools/xianxia-progress.py publish --pr 30
```

`--pr`로 지정한 PR에 동일한 블록을 게시할 수 있다. 기본 대상과 코드 예시 번호를 현재 구현 PR에 맞춰 사용한다.
작업 상태 파일의 변경도 해당 구현 커밋에 포함한다. `--publish` 실패 시 `set`은 로컬 상태를 저장하지 않는다.
외부 PATCH가 성공한 뒤 응답 전달만 실패하면 서버 상태가 바뀌었을 수 있으므로 본문을 확인하고 재시도한다.

## 3. PR 본문과 동시 작업

도구는 `<!-- xianxia-progress:start -->`와 `<!-- xianxia-progress:end -->` 사이만 바꾼다.
표식이 없으면 뒤에 추가하고, 불완전하거나 중복됐으면 중단한다. 기존 검토·작성자·수용 기준을 보존한다.
매번 최신 본문을 조회하고 게시 직전 바뀌었으면 충돌로 중단한다. 진행표 갱신은 한 작업자가 직렬로 수행한다.
최종 조회와 PATCH 사이의 원자적 잠금은 없으므로 동시 편집은 피하고 게시 결과를 확인한다.

PR의 완료 체크는 보기를 위한 표현이다. 브라우저에서 체크만 바꾸면 JSON 원본은 바뀌지 않으며 다음 동기화 때 원본으로 맞춰진다.
상태 변경은 원본과 명령을 사용한다. 미래 상태를 스케줄로 추측해 채우지 않고 실제 착수·검증 이벤트마다 갱신한다.

## 4. 검증

```sh
python3 -m unittest discover -s tools/tests -p 'test_xianxia_progress.py' -v
```

본문 보존·표식 오류·승인·선행·검증/근거 게이트·집계·게시 충돌·게시 실패의 로컬 파일 보존을 검사한다.
단위 검사는 GitHub에 쓰지 않고 API를 대체한다. 본문의 표만으로 CI·실기기 증거를 자동 판정하지는 않는다.
