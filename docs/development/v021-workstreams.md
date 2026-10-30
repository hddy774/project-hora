# 0.2.1 다중 코드 작업 계약

사용자 지시: GitHub에 소스를 계속 저장하며 메인 PR과 여러 서브 PR로 개발한다.
성능 테스트·프로파일링·부하/보정 측정은 모든 기능 개발 후 마지막 단계로 이관한다.
기능 정확성·권한·저장 왕복·앱 빌드/실행 검증은 각 서브 PR에서 계속 수행한다.

메인 통합 PR #8 → 현재 공통 기반 PR #23 → 아래 기능별 서브 PR → 공통 연결/저장/통합 검증 → 최종 성능 → APK 서명·릴리스.

| 작업 | 독립 소스 소유 경로 | 기능 검사 | 연관 이슈 |
|---|---|---|---|
| 지식·서적 | src/Chronicles.Game/Knowledge/ | tests/Chronicles.Game.Tests/Features/KnowledgeScenarios.cs | #11·#14 |
| 물품·제작 | src/Chronicles.Game/Inventory/ | tests/Chronicles.Game.Tests/Features/InventoryScenarios.cs | #14·#15 |
| 정치·권리 | src/Chronicles.Game/Politics/ | tests/Chronicles.Game.Tests/Features/PoliticsScenarios.cs | #16·#15 |
| 인물·특성·평가 | src/Chronicles.Game/People/ | tests/Chronicles.Game.Tests/Features/PeopleScenarios.cs | #10·#11 |
| 관찰·플레이 화면 | src/Chronicles.Shell/index.html | scripts/verify-v021-ui.py | #17 |

공통 State.cs·World*.cs·Laws.cs·Catalog.cs·Queries.cs·GameSession.cs·저장 schema·명령 dispatch·원장·CI는 통합 작업 소유다.
기능 모듈은 직렬화 가능한 도메인 상태·명시적 입력/결과·원자 전이·조회 순수성으로 구현하고, 공통 연결 지점을 설명한다.
다른 작업의 경로를 동시에 수정하지 않는다. 신규 기능 검사는 IFeatureScenario를 구현하면 공통 실행기가 자동 발견한다.
기능 소스와 검사를 작은 커밋으로 푸시하고, 별도 서브 PR에 실제 테스트 결과와 연결 대기 범위를 기록한다.
공통 연결 전 모듈 검사 통과를 해당 기획 작업의 전체 완료로 집계하지 않는다.
