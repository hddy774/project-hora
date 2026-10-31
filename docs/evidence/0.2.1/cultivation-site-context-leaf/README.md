# 수행 사용권 문맥의 성숙도 맵 생성 생략

기준 소스는 [`2af63d897eaa3050dd738412121c8d466957d409`](https://github.com/hddy774/project-hora/commit/2af63d897eaa3050dd738412121c8d466957d409),
제품 변경 소스는 [`39d38da3cb78841eb8fdeacc98307d28671380f4`](https://github.com/hddy774/project-hora/commit/39d38da3cb78841eb8fdeacc98307d28671380f4)다.

변경은 두 코드 구간이다.

- [`PoliticsContext(bool includeMaturity = true)`](https://github.com/hddy774/project-hora/blob/39d38da3cb78841eb8fdeacc98307d28671380f4/src/Chronicles.Game/World.PoliticsBridge.cs#L40): 기본 정치 문맥은 기존 전인구 성숙도 맵을 생성한다.
- [`CultivationContextFor`의 한 호출](https://github.com/hddy774/project-hora/blob/39d38da3cb78841eb8fdeacc98307d28671380f4/src/Chronicles.Game/World.CultivationBridge.cs#L391): 수행 사용권 검사에 전달할 때만 `includeMaturity: false`를 지정한다.

호출하는 [`CanUseCultivationSite`](https://github.com/hddy774/project-hora/blob/39d38da3cb78841eb8fdeacc98307d28671380f4/src/Chronicles.Game/Politics/PoliticsEngine.cs#L88)는 기준 소스와 본문이 동일하다.
이 함수는 실제 생존 인물·현재 장소, 소유자/운영자, 해산하지 않은 집단의 현재 구성원 여부만 읽고 성숙도·정책을 읽지 않는다.
새 권한 우회 규칙이나 월 단위 캐시는 추가하지 않았다. 성년 판정·정치 명령·정치 검증은 기본값 true를 유지한다.

기존 기능 검사18/18을 사용했다. 새 구현을 반복하는 검사나 새로운 성능 검사는 추가하지 않았다.

| 기존 검사 선택 | 통과 | 확인 범위 |
| --- | --- | --- |
| `public cultivation rights` | 1/1 | 사유지 거부 원자성·실물 서적 소유와 개인 지식·공유 노동 예약 |
| `public politics` | 9/9 | 실제 권리 이전·정책 노동/취소·저장·영호/영송의 고유 성숙도·가계/왕조 |
| `AI 수행 연결` | 5/5 | 실제 입문/상위 준비·재학습·실패 회복·개인 정보/난수 비간섭 |
| `inventory owned reference` | 2/2 | 기존 고정 세계 전사 두 개의 hash·난수·실물·저장 재개·명령 재전송 |
| `own combat social and AI` | 1/1 | 미관측 환경/관계 변화의 개인 조회 비간섭 |

`functional-results.json`은 실제 기능 보고서의 결과와 원본 SHA256을 기록한다.
`source-provenance.json`은 변경 소스, 미변경 권한 함수 본문 SHA256, 기본값과 유일한 생략 호출을 기록한다.
Release 빌드 경고0·오류0도 확인했다. 새 비용·프로파일·성장·대량 인구 실행은0이며, 이 소스 변경의 성능 개선 수치는 아직 주장하지 않는다.
