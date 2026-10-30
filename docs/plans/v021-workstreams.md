# 0.2.1 메인·서브 PR 개발

정본 저장소: https://github.com/hddy774/project-hora

메인 통합 브랜치는 `codex/v0.2.1`이며 최종 PR은 `main`을 대상으로 합니다. 각 서브 PR은 통합 브랜치를 대상으로 독립 코드와 기능 시나리오를 제출합니다. 검증된 서브 PR을 먼저 합친 뒤 공통 명령·시간·저장·조회에 연결합니다. 모듈의 단위 시나리오 통과를 전체 기능 완료로 계산하지 않습니다.

| 분야 | 이슈 | 브랜치 | 소유 경로 |
| --- | --- | --- | --- |
| 수행·전투·잠재 | #76 | codex/v021-cultivation | Game/Cultivation |
| 생애·유전·인지·육신 | #75 | codex/v021-people | Game/People |
| 지식·판본·학습 | #79 | codex/v021-knowledge | Game/Knowledge |
| 물품·제작·운송 | #79, #80 | codex/v021-inventory | Game/Inventory |
| 정치·정책·권한·재정 | #81 | codex/v021-politics | Game/Politics |
| 공용 화면·사용 흐름 | #82 | codex/v021-ui | Shell/index.html, UI verifier |
| 공통 권위·월 진행·저장·플랫폼·릴리스 | #74, #77, #78, #83 | codex/v0.2.1 | shared integration |

통합은 단일 세계 권위를 유지하며 각 모듈의 영구 상태를 저장·복원합니다. 실물·화폐·노동·지식·소유권을 중복 발급하지 않습니다. 관찰 정보와 개인의 믿음·지식을 분리하고, 플레이와 AI는 동일한 명령 검증을 사용합니다.

사용자 요청에 따라 성능 측정·프로파일링·성장/장기 실행 시험은 모든 기능 개발과 통합이 끝난 후 실시합니다. 기존 실패 측정은 실패 기록으로 보존하며 새 구현의 통과로 표시하지 않습니다.
