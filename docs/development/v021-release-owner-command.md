# 0.2.1 owner 릴리스 진입점

이 문서는 실행 경로의 운영 절차다. 이 변경에서 실제 명령 댓글·워크플로 실행·서명·릴리스 게시를 수행하지 않았다. 정본 기능/성능/서명·플랫폼 근거와 readiness가 모두 충족된 뒤 owner가 실행한다.

기존 `.github/workflows/v021-release.yml`의 `workflow_dispatch`는 유지한다. `publish=false` 진단과 `verify_upgrade` 선택도 유지한다. 연결 도구가 dispatch를 제공하지 않을 때는 **owner `hddy774`가 main으로 실제 병합된 메인 PR84에 새 댓글을 작성하는 진입점**을 쓸 수 있다.

```text
/선로만상록 0.2.1 릴리스
```

본문 전체가 위 문자열과 정확히 같아야 한다. 앞뒤 공백·줄바꿈·추가 설명·다른 버전은 거부한다. PR 본문 변경이나 일반 issue·다른 PR의 댓글은 진입점이 아니다. 댓글 모드는 `publish=true`, `verify_upgrade=true`를 source.json에 명시적으로 고정한다. 수동 입력값을 댓글에 붙여 바꿀 수 없다.

## 실행 전에 확인할 내용

1. 릴리스 전용 서브 PR을 통합하고 전체 main 수용을 완료한다. 이 진입점은 false readiness나 실패한 고정 성능 예산을 우회하지 않는다.
2. 메인 PR84가 이 저장소의 main으로 실제 병합된 상태인지 확인한다. 명령 댓글은 병합 시각 이후에 작성한다.
3. 빌드할 main SHA를 고정하고 실행 중 main을 바꾸지 않는다. checkout은 `github.sha`이며 API의 현재 main과 같아야 한다. PR84의 실제 merge commit도 checkout의 조상이어야 한다.
4. owner가 직접 수동 실행하거나 위 정확한 댓글을 작성한다. 다른 actor·triggering_actor는 거부한다. 자동 재시도는 추가하지 않았다.

source 기록에는 실제 `event`(`workflow_dispatch` 또는 `issue_comment`), 논리 요청, 댓글/PR ID, 게시/업그레이드 옵션과 실제 run/attempt가 포함된다. `GITHUB_EVENT_NAME`이나 API `run.event`를 수동 실행으로 바꾸지 않는다. 게시자는 GitHub의 실제 run.event·actor·triggering_actor·main SHA·워크플로 경로·현재 run/attempt를 같은 source와 대조한다.

댓글 작성 후 수정·삭제, 다른 원댓글/요청 아티팩트, PR 미병합/다른 base, main 변경은 다음 보호 단계에서 거부된다. 정상 owner 재실행은 기존처럼 가능하되 원래 요청과 main이 유지되고, **그 실제 attempt의 source·필수 job·아티팩트**가 다시 일치해야 한다. 이전 attempt의 성공이나 파일로 대체할 수 없다.

## 두 진입점이 공유하는 필수 경로

- 전체 기능/브라우저·readiness와 기존 정본 성능/장기 게이트.
- 실제 Windows WebView2와 기존 인증서 서명. 비밀 값은 기존 Actions 서명 단계에서만 사용한다.
- 동일한 서명 APK 한 개의 실제 API24·35 설치/실행/화면 검증. 댓글 모드는 v0.2.0 retained-signature 업그레이드와 저장 원본 보존을 반드시 요구한다.
- APK만 있는 draft 준비 → 실제 attachment 재다운로드/바이트 일치 → stable latest 게시 → 공개 바이트/태그/인증서 재검증.

댓글 진입점은 게시 허가의 새 우회로가 아니다. 기존 판정 함수와 APK/서명/성능/원장/readiness 범위는 유지하며, 실패 시 게시하지 않는다. 운영자가 실행할 때도 새 확인 질문을 추가하지 않으며 이미 승인된 최종 게시 범위에서 보호 조건을 충족해야 한다.

가드 검사와 출처는 [release-owner-comment 근거](../evidence/0.2.1/release-owner-comment/README.md)에 있다. 순수 가드 fixture는 실제 플랫폼/서명/릴리스 합격 근거가 아니다.
