# 정치 노동 가용성 문맥의 성숙도 맵 생성 생략

기준 소스는 [`674329f455fd366640d29ca495ab6b9b5d185eaf`](https://github.com/hddy774/project-hora/commit/674329f455fd366640d29ca495ab6b9b5d185eaf),
제품 변경 소스는 [`e15cf1cc79d023ba4f02cac04b4a6d9500167ee8`](https://github.com/hddy774/project-hora/commit/e15cf1cc79d023ba4f02cac04b4a6d9500167ee8)다.

변경은 [`PoliticsLaborAvailable`의 한 호출](https://github.com/hddy774/project-hora/blob/e15cf1cc79d023ba4f02cac04b4a6d9500167ee8/src/Chronicles.Game/World.PoliticsBridge.cs#L213)이다.
기존 `PoliticsContext(includeMaturity: false)` 옵션을 사용해 이 조회에서 불필요한 전인구 성숙도 맵 생성을 생략한다.
정치 성년/명령/전체 검증의 `PoliticsContext()` 기본값 true와 이전 수행 사용권의 생략 호출은 유지한다.

같은 [`PoliticsEngine.IsLaborAvailable`](https://github.com/hddy774/project-hora/blob/e15cf1cc79d023ba4f02cac04b4a6d9500167ee8/src/Chronicles.Game/Politics/PoliticsEngine.cs#L42)를 그대로 호출한다.
기준 소스와 본문이 동일하며 SHA256은 `5d4893ae99e97b0f34439d4942d646fff7d8e4b623f757bc9b112c80997d00fd`다.
이 함수는 from/until·현재 Month, 실제 생존 인물의 Idle 행동, 실제 Labor 예약의 기간 겹침만 읽고 성숙도·Rules는 읽지 않는다.
새 캐시·정책·월말 지연·검증 삭제·기능·저장 필드를 추가하지 않았다.

선행 원본 `9263756`과 기준 소스의 C# 차이는0이다. 기존 10,000명 예열 profile의
`warmup_advance`에서 `PoliticsLaborAvailable` inclusive8.291135441315543%가 기록되어 이 한 곳을 선정했다.
이는 기존 측정 원본을 읽은 근거이며 이번 leaf에서 개선 수치나 최종 수용을 주장하지 않는다.
원본 profile 파일과 hash는 `source-provenance.json`에 기록했다. 기존 측정·성장/AI 원본은 수정하지 않았다.

기능 검사는 기존 공개 정치 성년·노동/예약·AI 수행·개인 조회·고정 세계 oracle 두 개를 선택해 실행했다.
구체 결과와 원시 기능 보고서 SHA256은 `functional-results.json`에 기록한다.
새 검사·새 벤치·프로파일·성장·대량 인구 실행은0이다. 실제 비용 대조와 후속population/stability 판정은 별도 단계다.

| 기존 검사 선택 | 결과 |
| --- | --- |
| `public politics` · 비인간 성년·공개 권리·정책 노동·저장 | 9/9 |
| `organizations share actual labor` · 중복 노동 예약 거부 | 1/1 |
| `actual worker withdrawal` · 철회·원자적 실패 | 1/1 |
| `AI 수행 연결` · 입문/상위 준비·재학습·난수/정보 비간섭 | 5/5 |
| `inventory owned reference` · 기존 고정 세계 oracle 두 개 | 2/2 |
| `own combat social and AI` · 개인 미관측 정보 필터 | 1/1 |

합계19/19이며 Release 빌드 경고0·오류0이다. oracle 상수와 실제 상태 전사·난수·실물·저장 재개/명령 재전송 검사는 기존 값을 유지한다.
