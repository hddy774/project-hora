# 선협 관찰 작업 지침

루트 AGENTS.md를 따르고, 이 게임의 실제 프로젝트(net10.0·Avalonia)와 Hora 경제 게임을 구분한다.
할당된 XO 명세와 [개발 순서](docs/14-development-plan.md), UI 작업은 [고서 목판 계획](docs/15-ui-ux-improvement.md)을 읽는다.
사용자가 지정한 테마와 구현 승인 여부를 구분한다. 승인된 작업 또는 현재 사용자가 명시적으로 지시한 범위에서 구현한다.

## 진행 상태 갱신

사용자가 요청한 진행 보드는 PR #29의 `xianxia-progress` 표식 영역, 원본은 루트 `docs/tasks/xianxia-progress.json`이다.
실제 구현 착수 직전 `in_progress`, 구현 후 검사 시작 때 `verifying`, 수용 기준·검사 통과 후 근거와 함께 `done`으로 갱신한다.
차단되면 이유와 재개 조건을 기록한다. 사용자 지시로 이미 승인받은 사항은 명세·원본에 기록하고 재승인을 요구하지 않는다.
루트에서 `python3 tools/xianxia-progress.py set <ID> --status <상태> --note "현재 작업" --publish`로 즉시 반영한다.
GitHub 연결 도구를 사용하는 환경에서는 같은 도구의 로컬 출력을 최신 PR 본문의 표식 안에 반영해도 된다.
기존 본문은 보존하고 상태 파일 변경도 구현 커밋에 포함한다. 상세 사용법은 [진행 관리](docs/16-progress-tracking.md)를 따른다.
기획·기반 완료를 게임 구현 완료로 세지 않는다. Android CI 완료와 ARM64 실기기·출시 승인을 각각 확인한다.
