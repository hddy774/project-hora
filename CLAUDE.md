# Claude 기획·명세·리뷰 지침

먼저 [AGENTS.md](AGENTS.md)를 읽는다. 이 저장소의 현재 제품은 **선로만상록**이며,
정본은 `docs/plans/v021-tasks.json`과 연결된 개정6·사용자 결정이다.

Claude는 기획·명세·리뷰를, Codex는 구현·테스트·PR·릴리스를 담당한다.
사용자가 이미 승인한 0.2.1 전체 작업은 추가 Claude 답변이나 별도 승인 라벨 없이 진행한다.
이 파일은 역할 계약이며 외부 Claude 호출을 지시하거나 실제 리뷰 완료를 증명하지 않는다.

- 명세는 [작업 템플릿](docs/tasks/_template.md)으로 작성한다. 목표·소유 파일·입출력/단위·경계값·
  거부 원자성·개인 정보 범위·저장/재전송·수용 기준·정확한 검사 명령을 한 문서에 담는다.
- 리뷰 순서는 명세 일치 → 권위/보존·결정론 → 저장/실패/정보 공정성 → 실제 검증 → 가독성이다.
  `[필수]`에는 재현 입력과 위반 계약, `[제안]`에는 이득과 비용, `[질문]`에는 필요한 사실을 적는다.
- 측정 없는 속도 주장을 승인하지 않는다. 성능·성장 실행은 전체 기능 완료 뒤 배정된 최종 단계에서만 한다.
- 원래 [클로드 피드백 원문](docs/plans/reviews/v021-claude-review-2026-10-30.md)의 **1/1 수신**과
  [기존 판정](docs/plans/v021-review-resolution.md)은 유지한다.
  [이번 PR84 피드백](docs/plans/reviews/v021-claude-pr84-review-2026-10-31.md)은 별도 수신 기록이다.
- 저장 형식·판정 정책·모듈 권위를 바꿀 때는 현재 구현과 정본의 차이, 대안, 검증 경계를 명세에 적는다.
  원문을 고쳐 이전 리뷰의 뜻이나 검증 범위를 바꾸지 않는다.

[개발 절차](docs/standards/development-process.md) · [리뷰 체크리스트](docs/standards/review-checklist.md) ·
[0.2.1 리뷰 안내](docs/reviews/v0.2.1-review-guide.md) · [리뷰 구조 명세](docs/tasks/V021-REVIEW-STRUCTURE.md) ·
[명세 목록](docs/tasks/README.md) · [공통 원본 이관표](docs/standards/restoration-source-map.json)
