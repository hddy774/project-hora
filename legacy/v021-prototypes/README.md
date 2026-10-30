# 0.2.1 교체된 초기 경로의 출처

단일 도메인 어댑터로 교체한 초기 Game completion/Start 경로의 이동 전 파일을 보존한다.
`source-provenance.json`은 원본 commit과 각 파일의 SHA-256을 기록한다. 이 소스는 현재
프로젝트나 별도 실행 솔루션이 아니며 컴파일하지 않는다. 전체 당시 저장소는 기록된 Git
commit에서 복원할 수 있다. 현재 World 규칙을 이 파일에 수정하거나 재사용하지 않는다.

현재 실행 경계는 루트 `docs/architecture/v021-product-boundary.md`와 typed action owner
검증을 따른다. 과거 코드에 있는 raw Work·구식 제작/학습 정산은 현재 권위가 아니다.
