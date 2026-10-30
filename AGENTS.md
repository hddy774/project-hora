# 개발 규칙

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

- `VERSION`이 애플리케이션 버전의 유일한 원본이다.
- 이번 통합 목표 VERSION은 사용자 지정 0.2.1이다. 서브 PR마다 patch를 올리지 않는다.
- 기능 단위로 테스트한 뒤 작은 커밋을 만들고 원격 개발 브랜치에 푸시한다.
- 생성물과 `bin/obj/artifacts`는 커밋하지 않는다.
- 릴리스 전에는 Release 빌드, 전체 테스트, 10,000명 성능 검증과 패키지 실행 확인을 수행한다.
