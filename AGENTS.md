# AGENTS.md — AI 구현 에이전트(Codex) 작업 지침

이 파일은 이 저장소에서 작업하는 **모든 AI 에이전트가 가장 먼저 읽는** 지침이다.
구현은 Codex, 기획·명세·리뷰는 Claude, 결정·병합은 사람(프로젝트 오너)이 맡는다.

## 1. 프로젝트 한 줄 요약
Hora Economic Lab(호라경제연구실) — 1턴=1개월 경제·경영 시뮬레이션. 세로 화면 Android 우선, 오프라인 싱글플레이.
C#으로 재설계 중이며, 기존 Godot/GDScript 구현(`games/monthly-economy/`)은 **참고용 구버전**이다. 수정하지 않는다.

## 2. 작업 전에 반드시 읽을 문서
1. 할당된 작업 명세: `docs/tasks/<ID>-*.md` ← **이것이 작업 범위의 기준**
2. 코드 규칙: `docs/standards/coding-conventions.md`
3. 필요 시: 기획서 `docs/redesign/02-game-design.md`, 프로세스 `docs/standards/development-process.md`, Git 규칙 `docs/standards/github-workflow.md`

## 3. 절대 규칙
- **명세 범위만 구현한다.** 범위 밖 개선이 필요하면 PR 본문 "후속 제안"에 적고 손대지 않는다.
- 명세가 모호하거나 서로 모순되면 **추측해서 구현하지 말고** 질문을 남기고 멈춘다.
- `main` 에 직접 push 금지. 브랜치: `codex/<이슈번호>-<짧은-설명>`.
- 병합·릴리스·태그 생성 금지 (사람만 한다).
- 비밀 정보(키스토어, API 키) 커밋 금지.
- 테스트를 삭제·비활성화(`[Ignore]`)해서 통과시키지 않는다.

## 4. 코드 규칙 핵심 (상세는 coding-conventions.md)
- 우선순위: 정확성 → **성능** → 가독성·유지보수성 → 간결함. 이해하기 힘든 압축 금지.
- **주석 필수**: 모든 파일 상단 헤더, 모든 타입 `/// <summary>`, 모든 public/internal 멤버 XML 주석, 복잡한 로직에 "왜" 주석. 주석은 한국어, 식별자는 영어.
- 크기 제한: 파일 400줄, 메서드 40줄, 중첩 3단계, 한 줄 120자.
- 코어(`src/Hora.Domain`, `src/Hora.Simulation`): .NET Standard 2.1 + **C# 9**, 엔진 API 참조 금지.
  C# 10+ 문법(file-scoped namespace, global using, record struct, required) 금지.
- 돈은 `Money`(센트 `long`), 수량은 `Quantity`, ID는 전용 `readonly struct`. 돈에 `float/double` 금지.
- 결정론: `System.Random`, `DateTime.Now`, `Guid.NewGuid()`, `Dictionary` 순회 순서 의존 금지.
- 핫 패스(월 정산 반복문): LINQ·람다 캡처·루프 내 `new`·문자열 생성·박싱 금지.
- 예상 가능한 실패는 `Result`/`CommandResult` 반환, 예외는 버그·환경 오류에만.

## 5. 작업 완료 전 체크
```bash
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```
- 명세의 수용 기준을 PR 본문에 항목별로 체크한다.
- 성능 수용 기준이 있으면 벤치마크 수치를 PR에 첨부한다.
- 커밋 메시지: Conventional Commits (`feat(market): 도시별 호가 매칭 추가`), 본문에 "왜", 꼬리말에 `Refs #이슈`.

## 6. 저장소 구조
```
src/Hora.Domain/        순수 C# — ID, Money, 상태 구조체, 정의 데이터 타입
src/Hora.Simulation/    순수 C# — 월 정산 시스템, 명령, AI
src/Hora.Client/        엔진 프로젝트 (엔진 결정 후 생성, ADR 참고)
tests/                  NUnit 테스트 (대상 프로젝트와 1:1)
tools/Hora.Tools/       벤치마크·밸런스 시뮬레이터 CLI
data/                   정의 JSON + schema
docs/                   기획(redesign) · 규칙(standards) · 명세(tasks) · 결정(decisions)
games/monthly-economy/  구버전 Godot 구현 (읽기 전용 참고)
```
