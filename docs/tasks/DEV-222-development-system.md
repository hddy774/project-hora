# [DEV-222] 공통 개발·리뷰 체계와 SDK 고정 복원

## 목표와 출처

사용자 전달 PR84 피드백에 따라 main 공통 작업체계를 현재 선로만상록에 맞춘다.
[이슈222](https://github.com/hddy774/project-hora/issues/222),
[새 피드백 수신](../plans/reviews/v021-claude-pr84-review-2026-10-31.md),
[리뷰 구조 명세](V021-REVIEW-STRUCTURE.md)를 연결한다.
현재 명세/구현 작성자는 Codex이며 역할 배정만으로 외부 Claude 검토 완료라고 기록하지 않는다.

기준 개발 source는404767ad4dc576ea3669cddd42529a50741cbf6f,
공통 원본 main은cefb2021c249e83d5409a892af97b67e54066a92다.

## 소유 범위와 계약

- AGENTS/CLAUDE, `.editorconfig`/`.gitattributes`/`global.json`, PR/issue 템플릿,
  `docs/standards/`, `docs/redesign/`의 공통 안내와 `docs/tasks/`의 이 명세/템플릿/M0 기반을 작성한다.
- workflow·제품src·검사 소스·진행 생성기·원장·기존 증거 원문/게이트 JSON 경로는 수정하지 않는다.
- root는 새 피드백 수신/리뷰 가이드/리뷰 구조와 release workflow SDK를,
  Android 담당은 product/API24 workflow SDK를 맡는다. 이 명세는 해당 파일을 링크하고 불일치 목록을 전달한다.
- SDK는 설치된10.0.403 정확 버전·rollForward=disable·미리보기 금지다.
  `.editorconfig`는 새/변경 코드의 가독성 기준이며 기존 raw/legacy/생성물 광역 포맷을 하지 않는다.
- 기존 사용자 전체 개발/병합/게시 승인과 클로드 개정5 피드백1/1 원형을 유지한다.
  이번 피드백은 별도 수신 문서에 연결하고 새 대기 장벽을 만들지 않는다.

## 수용 기준

- [ ] main 공통 원본의 채택/맞춤/제외가 source SHA와 실제 목적 경로로 추적된다.
- [ ] 현재 Chronicles 의존·명령·파일 저장·기능 러너로 쓸 수 있는 명세/리뷰/이슈·PR 양식이다.
- [ ] SDK 설치/선택·strict 고정, 서식/JSON/front matter/문서 링크를 확인한다.
- [ ] engine-free Release 빌드·기존 창세1개를CPU0에서만 확인한다.
- [ ] 증거 크기/SHA 색인과 신규 원문의 만료 대비 영속 보관 정책을 제공한다.
- [ ] 소유 범위 밖 변경·기존 raw/legacy/피드백 원형 변경이 없다.

## 검증과 영향

SDK/설정/템플릿·링크의 정적 확인 및 `dotnet build tests/Chronicles.Game.Tests -c Release`를 수행한다.
기존 등록 `INV-001/002/011` 창세1개만 선택한다. 새 기능 검사·성능/성장 실행은 추가하지 않는다.
global.json은 제품 fingerprint 입력이므로 기존FP528 원문을 새 설정의 최종 합격으로 재라벨하지 않는다.
실제 결과는 `docs/standards/development-system-validation.json`에 기록하고 통합 담당이 후속 최종 검증을 수행한다.
