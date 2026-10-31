# 수행 전이 권위 파일 분할

이슈 [#224](https://github.com/hddy774/project-hora/issues/224). 기준 커밋은
`404767ad4dc576ea3669cddd42529a50741cbf6f`다. 기존 582줄의
`src/Chronicles.Game/Cultivation/CultivationEngine.cs`를 같은 정적 클래스의 기능별 partial로 나눈다.

공개 API, namespace, 타입, 모든 메서드의 서명·본문 토큰, 정수 산술 순서와 난수 경계를 유지한다.
읽기 편하도록 공백·개행만 정돈하며 새 추상 계층·기능·최적화는 추가하지 않는다.
필드·생성자는 원래 파일과 순서를 유지해야 한다. 이번 기준에는 필드·생성자가 없다.

소유 범위는 해당 원본 파일, 새 `CultivationEngine.*.cs`, 이 문서 디렉터리다.
World 연결, 쿼리 권한, 저장 형식, 게임 값, 공통 validator와 설정은 수정하지 않는다.
새 성능·성장·장기 실행을 하지 않으며 이전 제품 지문의 비용 근거를 이 변경의 통과로 간주하지 않는다.

완료 조건은 Roslyn 멤버 대조, Release 빌드, 기존 핵심 기능 검사와 원격 바이트·변경 경로 확인이다.
검사 명령과 결과 및 파일별 읽기 순서는 [검토 안내](review-guide.md)에 기록한다.
