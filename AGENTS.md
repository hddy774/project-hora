# 선로만상록 개발·AI 협업 규칙

작업 전 현재 사용자 지시, 이 파일, 할당된 `docs/tasks/` 명세와 관련 정본을 읽는다.
공통 기준은 [개발 절차](docs/standards/development-process.md),
[코드 규칙](docs/standards/coding-conventions.md),
[GitHub 규칙](docs/standards/github-workflow.md)이며 Claude의 역할은 [CLAUDE.md](CLAUDE.md)에 있다.
명세·코드·검증 출처가 다르면 실제 소스와 결과를 대조해 기록한다.

## 역할과 이미 승인된 실행 범위

- Claude는 기획·자기완결 작업 명세·리뷰를 맡고 Codex는 구현·테스트·PR·리뷰 반영·릴리스를 맡는다.
  실제 작성자와 리뷰 수행 여부를 남기며 역할 배정만으로 Claude가 검토했다고 표시하지 않는다.
- 사용자의 기존 0.2.1 전체 개발·병합·최신 릴리스/APK 게시 승인은 유지된다.
  새 Claude 응답이나 `status:ready` 라벨을 기다리는 절차를 추가하지 않는다.
- 일상적인 구현 선택은 근거를 적고 진행한다. 실제 정책 충돌은 기존 사용자 결정과 정본에서 해결하고,
  독립적으로 진행할 수 있는 작업은 계속한다. 잠정값 보정은 사용자가 위임한 범위를 따른다.
- 병렬 작업은 별도 worktree와 파일 소유 범위로 나눈다. 공유 파일·통합·실측 일정은 통합 담당자와 조율한다.
  사용자 승인과 내부 작업 배정은 구분한다.
- [작업 템플릿](docs/tasks/_template.md)의 범위·권위/정보 경계·수용 기준·검증 명령을 채워 작은 PR로 완결한다.
  [리뷰 체크리스트](docs/standards/review-checklist.md)로 정확성·보존·저장/재전송·가독성을 확인한다.
- 이번 검토의 [PR84 피드백 수신](docs/plans/reviews/v021-claude-pr84-review-2026-10-31.md),
  [버전별 리뷰 안내](docs/reviews/v0.2.1-review-guide.md),
  [리뷰 구조 명세](docs/tasks/V021-REVIEW-STRUCTURE.md)를 연결한다.
  기존 개정5 검토의1/1 수신 원문/횟수는 그대로 유지한다.

## 제품 경계

- 0.2.1 정본은 Notion 408acc9e6175817ab59bd91bb9dc011d와 docs/plans/v021-tasks.json이다.
- 관찰과 플레이는 같은 세계를 사용한다. 플레이는 개인 지식·권한에 제한되며 AI와 같은 명령을 쓴다.
- 기본 창세는 정확히 두 반선 시조·종문 0개다. 숨은 주민이나 시장 공급자를 만들지 않는다.
- 혈연 후손 간 혼인/출산 허용, 양친 모두 금단 이상, 잠정값 개발 보정 위임은 2026-10-30 사용자 확정이다.
- AI는 생존·양육·수행·생업·관계·정치 등 여러 목표를 가지며 전지적 상태를 읽지 않는다.
- 게임의 핵심 권위 시스템은 수행과 전투이며, 랭킹·도시 경제는 두 시스템의 성장과
  경쟁을 연결한다.
- 가문·종문·왕조·경제·윤회 13경로 등 0.2.1 전체 범위는 사용자 승인 개발 대상이다.

## 저장소·개발 순서

- 정본 저장소는 `hddy774/project-hora`다. 다른 프로젝트 자료를 추가하지 않는다.
- 소스·관련 기획·증거를 이 저장소에 저장한다. main 직접 push 대신 0.2.1 메인 PR과 여러 서브 PR을 사용한다.
- GitHub 제목·본문·진행 설명과 커밋 설명은 한글을 기본으로 한다. 필요한 영어는 한글 옆에 병기하고 코드 식별자·경로·검증 수치는 보존한다.
- 전체 진행률은 정본111개 중 수용 근거와 모든 선행 작업이 확인된 done 작업 수로 계산한다. 자체 검증 통과·선행 대기는 validated로 분리하며 임의 부분 점수를 주지 않는다. 원장 변경 후 `scripts/update-v021-progress.py`로 문서를 갱신하고 자동화가 메인 PR·총괄/분야 이슈를 갱신하도록 한다.
- 사용자는 0.2.1 전체 개발·PR 병합·최신 릴리스와 APK 게시까지 승인했다.
- 기능별 정확성 검사는 개발 중 수행한다. 성능·프로파일링·대량 인구·성장/장기 실행 시험은 모든 기능 개발이 끝난 뒤 마무리 단계에서만 수행한다.
- 기존 Android 앱 ID `org.hora.xianxiaobserver`와 배포 인증서를 유지한다. 키스토어·비밀 값은 로컬로 가져오거나 출력하지 않는다.

## 의존 방향

화살표는 사용하는 프로젝트에서 참조하는 프로젝트로 향한다.

```text
Desktop / Android / Shell -> Hosting -> Game -> Core
Game.Tests -> Game + Hosting
```

- `Core`: 제품 버전·표제와 명시적 상태의 결정론적 RNG. 엔진·OS·파일 의존이 없다.
- `Game`: 단일 세계 권위, 도메인 전이·명령·정보가 제한된 DTO와 메모리 byte/hash/clone codec. 파일 경로·저장 장치·호스트를 참조하지 않는다.
- `Hosting`: 직렬화된 `GameSession`, `GameSaveStore` 파일 슬롯·복구·원본 보존과 Desktop facade. 이 형식의 기존 CLR namespace는 유지하지만 assembly owner는 Hosting이다.
- `Desktop`: WPF `App -> EcologyWindow`와 WebView2. `Android`/`Shell`도 같은 세션·명령·DTO를 사용한다.
- 이전 Windows `MainWindow/Simulation/Runtime/Queries/Persistence/Application/Presentation` 및 이전 테스트는 `legacy/windows-pre-v021`에 출처와 함께 보관하며 현재 제품 참조에 넣지 않는다.
- UI는 가변 세계를 직접 소유·수정하지 않는다. 관찰·검색·정렬은 세계와 RNG를 바꾸지 않는다.

현재 파일 owner·허용 참조는 `docs/architecture/repository_manifest.json`, 실제 진입점·저장·Unity 한계는
[0.2.1 제품 경계](docs/architecture/v021-product-boundary.md)를 따른다.
`python3 scripts/validate-v021-product-boundary.py --self-test`는 정적 경계만 검사하며 네이티브 실행이나 Unity 호환을 증명하지 않는다.

## 시뮬레이션 규칙

- 세계 시간은 정수 tick만 사용하고 벽시계 시간을 권위 상태에 저장하지 않는다.
- AI는 매 렌더 프레임이 아니라 행동 완료·중단 시점에 다음 결정을 내린다.
- 난수는 명시적 seed와 안정된 stream으로만 소비한다.
- 전투력은 경지·자질·공법·법술·전투 경험·법보의 기여도를 각각 계산하고 조회에 공개한다.
- 동일 seed와 동일 명령은 동일 상태 해시와 동일 랭킹을 만들어야 한다.

## 변경과 검증

- SDK는 루트 `global.json`의 **10.0.403**, `rollForward=disable`을 사용한다.
  `dotnet --version`이 다르면 정확한 SDK 설치를 확인하며 파일을 낮은 버전으로 바꿔 우회하지 않는다.
- `VERSION`이 애플리케이션 버전의 유일한 원본이다.
- 이번 통합 목표 VERSION은 사용자 지정 0.2.1이다. 서브 PR마다 patch를 올리지 않는다.
- 기능 단위로 테스트한 뒤 작은 커밋을 만들고 원격 개발 브랜치에 푸시한다.
- 생성물과 `bin/obj/artifacts`는 커밋하지 않는다.
- 릴리스 전에는 Release 빌드, 전체 테스트, 10,000명 성능 검증과 패키지 실행 확인을 수행한다.
- 제품 기능 러너는 `dotnet run --project tests/Chronicles.Game.Tests -c Release`다.
  `-- --filter '<실제 등록 Name의 부분 문자열>'`는 선택 검사이며 전체 합격으로 쓰지 않는다.
- `.editorconfig`는 읽기 쉬운 새 코드·변경 코드의 서식 기준이다. raw·생성물·legacy를 일괄 포맷하지 않는다.
  분석기·경고를 끄거나 실패 검사·원문을 지워 통과시키지 않는다.
- 기존 증거의 본문·SHA·이력과 게이트 JSON 경로를 유지한다. 새 큰 원문은
  [증거 보관 규칙](docs/standards/evidence-retention.md)에 따라 내구성 있는 자산과 작은 색인으로 보관한다.
  Actions의 14일 아티팩트만 영구 출처로 남기지 않는다.
