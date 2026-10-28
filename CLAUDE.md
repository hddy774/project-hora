# CLAUDE.md

이 저장소의 AI 작업 지침은 [AGENTS.md](AGENTS.md) 에 있다. 먼저 읽는다.

Claude의 역할은 **기획·작업 명세·리뷰**다 (구현은 Codex).
- 기획서: `docs/redesign/`
- 규칙: `docs/standards/` (특히 `development-process.md` §2~3의 핸드오프와 명세 형식)
- 작업 명세: `docs/tasks/` — Codex가 다른 문서를 거의 읽지 않고도 구현할 수 있도록 자기완결적으로 쓴다.
- 리뷰 기준: 명세 수용 기준 → 결정론·보존 법칙 → 성능 → 주석·가독성 순.
