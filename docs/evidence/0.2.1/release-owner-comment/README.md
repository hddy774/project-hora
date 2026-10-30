# owner 명령 댓글 릴리스 진입점 가드

기준은 `ffd83f96ae1bdd60ccac60df7e0116abcde6f827`이다. 변경 범위는 릴리스 전용 워크플로와 검증기/게시자 3파일, 운영 문서와 순수 가드 근거뿐이다. **실제 명령 댓글·릴리스 워크플로·서명·릴리스 게시·앱 실행은 0건**이다. 이 자료는 서명·플랫폼·성능 수용 근거가 아니다.

[운영 절차](../../../development/v021-release-owner-command.md)는 main으로 실제 병합된 PR84에서 owner의 정확한 명령을 사용하는 방법과 기존 수동 실행을 구분한다. `workflow_dispatch` 진단 입력/정상 owner 재실행은 유지한다. 댓글 모드만 게시·retained-signature 업그레이드를 true로 고정한다.

## 확인한 보호 조건

- 실제 저장소·owner actor/triggering_actor·main ref·워크플로 경로/SHA와 실제 event payload를 검증한다.
- 댓글은 created/PR84/정확한 본문/owner/미수정 원댓글이어야 한다. API의 PR84는 이 저장소 main으로 실제 병합돼야 하며, 병합 시각 이후 댓글이어야 한다. merge commit의 checkout 조상 여부도 확인한다.
- API의 현재 main SHA와 checkout/source가 달라지면 거부한다. source 기록 중 checkout이 바뀐 경우도 별도로 거부한다.
- source.json은 실제 `issue_comment` 이벤트와 불변 요청을 기록한다. 게시자는 실제 run.event·actor·triggering_actor·소스·워크플로·run/attempt 및 현재 필수 job을 다시 검증한다. 이벤트를 수동 실행으로 덮어쓰지 않는다.
- 댓글 모드의 업그레이드 플래그를 빼거나 API24/35 중 다른 APK를 쓰는 검사를 거부한다. 기존 서명/플랫폼/성능/readiness/draft/readback/latest 검증은 유지한다.

## 실제 실행한 순수 검사

| 검사 | 결과 |
| --- | --- |
| 기존 release guard / 성능 계약 self-test | passed. 검증기 fixture이며 실제 플랫폼·성능 주장이 아님. |
| 새 요청 가드 | 정상8 / 거부50. owner/이벤트/PR/정확명령/원댓글/업그레이드·source/attempt 경계. |
| source 기록 연결 | 정상1 / checkout 변경 거부1. |
| 실제 publisher.actual_source_run 연결 | 정상4 / 거부5. manual/comment·owner attempt2 정상, 실제 event/다른 rerun actor/attempt 불일치/필수 job 누락/main 변경 거부. |
| actionlint 1.7.12 | exit0, 출력 없음. |
| Python AST / 기존 보호 함수 대조 | passed. 제품 지문 경계·서명/기능/성능/플랫폼/게시 20개 함수의 AST가 기준과 동일. |

[self-test.json](self-test.json), [wiring.json](wiring.json), [guard-report.json](guard-report.json), [actionlint.log](actionlint.log)에 실제 출력·종료·대조 범위를 보존했다. [check-guards.py](check-guards.py)는 같은 연결 검사와 AST 대조를 재현하며 API·서명·게시·앱 호출을 금지한 모형만 사용한다.

`RELEASE_ONLY`, `product_path`, `fingerprint`는 기준과 동일하다. 기능/검사/법칙/314/예산/성능 게이트/readiness를 수정하지 않았다. 원문의 실제 최종 수용이 끝나기 전 이 진입점으로 게시를 우회할 수 없다. 현재 PR84가 아직 미병합인 상태에서는 명령을 실제 작성해 시험하지 않았다.
