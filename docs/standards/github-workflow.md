# GitHub 사용 규칙

> 적용 대상: 이 저장소에 기여하는 **모든 사람과 AI 에이전트**.
> 목적: 누가 언제 무엇을 왜 바꿨는지 추적 가능하게 하고, `main`을 항상 빌드·실행 가능한 상태로 유지한다.

---

## 1. 브랜치 전략 (GitHub Flow 변형)

```
main ──●────────●──────────●──────────●───  (항상 배포 가능, 직접 push 금지)
        \      /  \        /
         feat/...   fix/...
```

| 브랜치 | 용도 | 예시 |
|---|---|---|
| `main` | 배포 가능한 최신 코드. **직접 push 금지**, PR로만 병합 | — |
| `feat/<이슈번호>-<짧은-설명>` | 새 기능 | `feat/42-market-order-matching` |
| `fix/<이슈번호>-<짧은-설명>` | 버그 수정 | `fix/57-negative-inventory` |
| `refactor/<...>` | 동작 변경 없는 구조 개선 | `refactor/63-split-labor-system` |
| `perf/<...>` | 성능 개선 | `perf/70-soa-company-table` |
| `docs/<...>` | 문서만 변경 | `docs/design-milestones` |
| `chore/<...>` | 빌드·CI·설정 | `chore/ci-dotnet-format` |
| `codex/<이슈번호>-<...>` | Codex(구현 담당 AI) 작업 브랜치 (규칙은 다른 브랜치와 동일) | `codex/42-market-order-matching` |
| `claude/<...>` | Claude(기획 담당 AI) 문서 작업 브랜치 | `claude/game-redesign-...` |
| `release/vX.Y` | 릴리스 안정화 (필요할 때만) | `release/v1.0` |

규칙:
- 브랜치 이름은 **영어 소문자 + 하이픈**. 한글·공백 금지.
- 한 브랜치 = 한 가지 목적. 기능 + 리팩터링을 섞지 않는다.
- 병합된 브랜치는 삭제한다. 병합된 브랜치에 새 작업을 이어 올리지 않는다.
- 브랜치 수명은 **최대 1주**를 권장. 길어지면 기능을 쪼갠다.

---

## 2. 커밋 메시지 규칙 (Conventional Commits)

### 형식
```
<타입>(<범위>): <요약>

<본문 — 무엇을, 왜 바꿨는지. 어떻게는 코드가 말한다>

<꼬리말 — Refs/Closes 이슈, BREAKING CHANGE, 공동 작성자>
```

### 타입
| 타입 | 의미 | 버전 영향 |
|---|---|---|
| `feat` | 새 기능 | MINOR |
| `fix` | 버그 수정 | PATCH |
| `perf` | 성능 개선 | PATCH |
| `refactor` | 동작 변경 없는 구조 개선 | — |
| `test` | 테스트 추가·수정 | — |
| `docs` | 문서 | — |
| `data` | 밸런스·정의 데이터(JSON) 변경 | PATCH |
| `art` | 이미지·사운드 리소스 | — |
| `chore` | 빌드·CI·도구·설정 | — |

### 범위 (scope) — 모듈 이름 사용
`domain`, `sim`, `market`, `labor`, `production`, `finance`, `ai`, `ui`, `save`, `data`, `ci`, `docs`

### 규칙
- 요약은 **한국어 또는 영어, 50자 이내**, 마침표 없음, 명령형/현재형 ("추가", "수정", "Add").
- 본문은 72자 줄바꿈 권장. **"왜"** 를 반드시 적는다.
- 하나의 커밋은 하나의 논리적 변경. 빌드가 깨지는 중간 커밋을 `main`에 남기지 않는다(스쿼시 병합으로 보장).
- 호환성을 깨는 변경(저장 파일 형식 등)은 꼬리말에 `BREAKING CHANGE:` 를 쓴다.
- AI가 작성한 커밋은 꼬리말에 `Co-Authored-By:` 로 표시한다.

### 예시
```
feat(market): 도시별 호가 매칭으로 가격 결정

고정 가격 대신 매수·매도 호가를 가격 순으로 체결한다.
미체결 수량을 다음 달 가격 신호로 남겨 품귀/과잉을 표시할 수 있게 한다.

Closes #42
```
```
perf(sim): 월 정산 시 세계 깊은 복사 제거

정산 시간이 기본 세계에서 820ms → 140ms로 감소 (Pixel 7a, 10회 평균).

Refs #70
```

---

## 3. 이슈 규칙

- **모든 작업은 이슈에서 시작**한다 (오탈자 수준의 문서 수정은 예외).
- 템플릿 사용: 버그 / 기능 / 작업 (`.github/ISSUE_TEMPLATE/`).
- 이슈 하나는 **1~3일 안에 끝낼 크기**. 더 크면 상위 이슈(Epic) + 하위 이슈로 나눈다.

### 라벨
| 분류 | 라벨 |
|---|---|
| 종류 | `type:bug` `type:feature` `type:task` `type:perf` `type:docs` |
| 영역 | `area:sim` `area:ui` `area:data` `area:ci` `area:art` |
| 우선순위 | `P0-긴급` `P1-높음` `P2-보통` `P3-낮음` |
| 상태 | `status:needs-design` `status:blocked` `good first issue` |

### 마일스톤
GitHub Milestone = 기획서 §9 로드맵의 M0~M5와 1:1로 대응한다.

---

## 4. Pull Request 규칙

### 4.1 PR 만들기 전 (작성자 체크)
- [ ] 로컬에서 `dotnet format --verify-no-changes` 통과
- [ ] 로컬에서 빌드 + 전체 테스트 통과
- [ ] 성능 민감 코드를 바꿨다면 벤치마크 결과 첨부
- [ ] 새 public 타입·메서드에 XML 문서 주석 작성
- [ ] 관련 문서(기획·ADR·README) 갱신

### 4.2 PR 크기
- **변경 400줄 이하 권장**, 800줄 초과 시 분할 필수 (자동 생성 파일·데이터 JSON 제외).
- 리팩터링과 기능 변경은 다른 PR로 분리한다.

### 4.3 PR 제목·본문
- 제목은 커밋 규칙과 같은 형식: `feat(market): 도시별 호가 매칭으로 가격 결정`
- 본문은 `.github/pull_request_template.md` 의 항목을 채운다.
- UI 변경은 **스크린샷 또는 짧은 영상** 필수.

### 4.4 리뷰
- 최소 **승인 1명** 필요 (1인 개발 단계에서는 AI 리뷰 + 본인 셀프 리뷰 체크리스트로 대체 가능).
- 리뷰어는 24시간 이내 1차 응답을 목표로 한다.
- 리뷰 코멘트 접두어:
  - `[필수]` 병합 전 반드시 수정
  - `[제안]` 개선 권장, 작성자 판단
  - `[질문]` 의도 확인
  - `[칭찬]` 좋은 코드 공유
- 리뷰 관점 우선순위: **정확성 > 결정론·보존 법칙 > 성능 > 가독성 > 스타일** (스타일은 도구가 잡는다).

### 4.5 병합
- **Squash and merge** 만 사용 → `main` 이력이 PR 단위로 깔끔하게 남는다.
- 스쿼시 커밋 메시지는 PR 제목 규칙을 따른다.
- 병합 조건: CI 전부 통과 + 승인 + 충돌 없음 + 모든 `[필수]` 코멘트 해결.

---

## 5. `main` 브랜치 보호 설정 (저장소 관리자)

Settings → Branches → `main`:
- [x] Require a pull request before merging (승인 1)
- [x] Require status checks to pass: `format`, `build`, `test`, `perf-guard`
- [x] Require branches to be up to date before merging
- [x] Require conversation resolution before merging
- [x] Do not allow force pushes / deletions

---

## 6. 버전과 릴리스

- **SemVer**: `MAJOR.MINOR.PATCH` (예: `v1.2.3`). 1.0 이전에는 MINOR에서도 호환성이 깨질 수 있다.
- 태그 형식: `v1.0.0` (재설계 이전 Godot 버전은 `legacy/godot-v0.2.1` 태그로 보존).
- 릴리스 첨부물: **Android APK 1개** (사용자 확정 R23). 체크섬·서명 증거는 CI 아티팩트에 보존.
- 릴리스 노트: 커밋 타입별 자동 생성 후 사람이 "플레이어가 체감하는 변화" 위주로 편집.
- 저장 파일 형식이 바뀌면 릴리스 노트에 **마이그레이션 여부**를 반드시 적는다.

---

## 7. 저장소 구조 (재설계 이후 목표)

```
project-hora/
├── .github/            # CI 워크플로, PR·이슈 템플릿
├── docs/
│   ├── redesign/       # 피드백·기획서
│   ├── standards/      # 이 문서 등 규칙
│   └── decisions/      # ADR (설계 결정 기록)
├── src/
│   ├── Hora.Domain/        # 순수 C#: ID, Money, 상태 구조체, 정의
│   ├── Hora.Simulation/    # 순수 C#: 월 정산 시스템, 명령, AI
│   └── Hora.Client/        # 엔진 프로젝트 (Unity/Godot)
├── tests/
│   ├── Hora.Domain.Tests/
│   └── Hora.Simulation.Tests/
├── tools/
│   └── Hora.Tools/         # 밸런스 시뮬레이터 CLI, 벤치마크
├── data/               # 정의 JSON + JSON Schema
├── games/monthly-economy/  # (구버전, 이관 완료 후 legacy 태그만 남기고 제거 검토)
├── .editorconfig
├── AGENTS.md           # AI 에이전트(Codex) 작업 지침 — 모든 AI가 먼저 읽는다
└── README.md
```

---

## 8. 대용량·민감 파일

- 이미지·사운드·폰트 등 이진 리소스는 **Git LFS** 로 관리한다 (`*.png`, `*.psd`, `*.wav`, `*.ogg`, `*.otf`, `*.ttf`, `*.fbx`). 신규 프로젝트 생성 시 `.gitattributes` 를 먼저 설정한다.
- **절대 커밋 금지**: 서명 키(`*.keystore`, `*.jks`, `*.p12`), API 키, 비밀번호, 개인정보. 서명 키는 GitHub Secrets로만 사용한다.
- 실수로 커밋했다면 즉시 키를 폐기·교체하고 관리자에게 알린다 (이력 삭제만으로는 부족하다).
- 빌드 산출물(`bin/`, `obj/`, `Library/`, `Builds/`, `*.apk`)은 `.gitignore` 로 제외한다.

---

## 9. AI 에이전트 추가 규칙

역할 분담: **Claude = 기획·명세·리뷰**, **Codex = 구현**, **사람(프로젝트 오너) = 결정·승인·병합**.

- AI는 지정된 작업 브랜치(`codex/*`, `claude/*`)에서만 작업하고, `main` 에 직접 push 하지 않는다.
- Codex는 **작업 명세(이슈)에 적힌 범위만** 구현한다. 명세가 모호하면 추측하지 말고 PR/이슈에 질문을 남긴다.
- 사용자가 요청하지 않은 병합·릴리스 게시를 하지 않는다.
- PR 본문에 "AI가 작성함"과 사람이 확인해야 할 지점(가정·미검증 항목)을 명시한다.
- 자세한 작업 지침은 저장소 루트 `AGENTS.md` 를 따른다.
