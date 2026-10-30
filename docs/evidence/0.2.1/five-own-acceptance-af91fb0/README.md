# 다섯 작업 자체 수용 확인·완료율 유지

root가 승인한 D02·T01·T02·T03·T05 **다섯 작업만** `in_progress`에서 `validated`로 바꾼다. `done` 승격은0이다. [기계 감사](acceptance.json)에 실제 수용 조항·원문·화면/네이티브 출처·선행과 변경 전후 집계를 기록한다.

| 구분 | 이전 | 반영 후 |
| --- | ---: | ---: |
| 선행 포함 완료(done) | 49/111 · 44.1% | **49/111 · 44.1%** |
| 자체 수용 근거(done+validated) | 97/111 · 87.4% | **102/111 · 91.9%** |
| validated | 48 | 53 |
| in_progress | 14 | 9 |

자체 수용과 모든 선행의 마감은 다르다. D02의 직접 미완료 선행에 L04·L05가 있고 여러 `validated` 선행도 남는다. 이 다섯 작업의 전이적 최상위 진행 중 원인은 **L04·L05·Z04**다. T01→D02, T02→T01 및 U03/U04/U05/U07, T03→T02, T05→T02 및 X04가 아직 `done`이 아니므로 연쇄 완료도0이다. 나머지 진행 중9개는 **T04·T06·H06·H09·L03·L04·L05·L07·Z04**로 유지한다.

| 작업 | 확인한 자체 수용 근거 | 분리한 범위 |
| --- | --- | --- |
| D02 최종 통합 저장 감사 | [af91 전체314/314](../invariant-final-af91fb0/README.md), [조항별 감사](../acceptance-functional-af91fb0.md), 실제 운송 중 JSON2/이진3 파일·재전송·배송/봉쇄, 실제 대량 codec 저장·읽기 | 고정 성능 합격·선행 마감·릴리스를 뜻하지 않음 |
| T01 최종 불변식 감사 | 실제314이름·24INV·152개 명명 연결, 소스/부정·저장/helper 앵커·원문 출처 검토 | 맵은 `not_asserted`; 모든 불변식 조합·정본111개 전체 완료가 아님 |
| T02 전 범위 시나리오·조회 무영향 | 두 시조→가계/수행/세력/참여의 실제 연결, 개인 조회 hash/RNG·반환 격리 | 작은 명시 조건의 기능 사례이며 장기 자연 성장·AI 분포는 별도 |
| T03 시간·명령·결정론 회귀 | 고정 seed·명령/월 경계·advance 분할·문화권·checkpoint·운송 같은 due/hash/RNG | 벽시계 배속·장치 성능 수용을 대신하지 않음 |
| T05 UI·모드/입력 사용성 | 아래 926 실화면 + 보관된 자산 동일 감사 + root가 확인한 현행 소스 실제 네이티브 CI | 공식 유지 인증서 동일 APK·업그레이드는 **T06** |

기능 원문 소스는 `af91fb0600a7c3603eac4fd7429ec2dd54bc1e3a`, 제품 지문은 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`다. 전체 원문 SHA는 `6bb3de5ccb787eeee7a71f86295ce17c9eaa2a630cbef206f95e04beaf312e87`다. 기존313/309/308·305/308 실패·제안 `test_status=not_implemented`를 변경하지 않았다.

## T05의 화면·소스·네이티브 연결

[926 실제 화면 검수](../final-ui-9263756/README.md)는 실제 GameSession에 연결한 1280/390px·일반/200% 글자에서 메뉴·수행·장착/호위·정책/관계/비경·정보 통제·법칙·저장/불러오기 피드백을 확인했다. 입문 대화창에서는 Tab·Enter·실제 터치 탭을 사용하고 문서/대화창 가로 넘침과 콘솔 오류를 검사했다. 당시의 네이티브 실행 제외 한계는 그대로 유지한다.

[926 소스 명세](../final-ui-9263756/source.json)와 [9704 검토](../invariant-execution-source-review-9704e95.json)·[c6 검토](../invariant-execution-source-review-c6e4704.json)·[af91 검토](../invariant-execution-source-review-af91fb0.json)의 보관된 동일 자산/변경 경로 감사로 이 화면 범위를 연결한다. 이 갱신에서 새 UI 실행이나 자산 재해시를 한 것은 아니다.

PR196의 이벤트 head는 `f3efb36c2b1755dca710dcf110f41baf633145a1`이다. product run41109698838의 세 작업은 실제 병합 checkout `b49129f22ac1c5dc3f9d16bef9af9309c1ab63da`에서 실행됐다. UI 담당자의 새 원문 감사에서 f3→b491 차이는 문서19파일뿐이고 제품 입력/지문은 같지만 전체 트리는 다름을 확인했다. API24 run41109698732는 exact f3에서 실행됐다. 이벤트 head·실제 checkout·제품 입력 동등성을 구분하며 실제 runtime/test 입력은 af91과 같다. 다음 표의 출처는 **root가 직접 확인한 GitHub run/jobs/단계 API와 UI 담당자의 [새 원문 출처 확인](native-ci-ui-provenance.original.json)**이다. 이 갱신이 새 CI 실행이나 로그 다운로드를 수행했다고 기록하지 않는다.

| run/job | 실제 완료 범위 | root 확인 |
| --- | --- | --- |
| [41109698838 / 124248620691](https://github.com/hddy774/project-hora/actions/runs/41109698838/job/124248620691) | Windows/WebView2, 실제 화면·core 단계7 success | 2026-10-30 19:34 UTC, 분 단위 |
| [41109698838 / 124248620831](https://github.com/hddy774/project-hora/actions/runs/41109698838/job/124248620831) | portable 기능 단계9 success | 같은 확인 |
| [41109698838 / 124248620999](https://github.com/hddy774/project-hora/actions/runs/41109698838/job/124248620999) | Android API35 실제 설치·실행 단계11 success | 같은 확인 |
| [41109698732 / 124248619303](https://github.com/hddy774/project-hora/actions/runs/41109698732/job/124248619303) | Android API24 run/job success | root의 앞선 직접 확인과 UI 담당자의 19:49:34 UTC 새 원문 확인 |

product CI의 성능 단계는 의도적으로 skipped이며 성능 합격으로 세지 않는다. PR190의 옛 API24 run41105101780을 새 run으로 재라벨하지 않았다. 실제 네이티브 렌더/브리지·사용성 근거는 공식 서명 동일 APK의 API24/35 설치·기존 릴리스 업그레이드 증거와 구분한다.

## 성능 실패와 이후 소스 경계

[첫 af91 고정 성능 대조](../final-performance/first-final-population-af91fb0/assessment.json)는 네 규모 수집과 codec 왕복을 마쳤지만 **100명 월p95(2240.2229ms > 1294ms), 10,000명 조회(1769.4717ms > 1660ms)**로 불합격이다. 원문·고정 예산·사전등록 quiet1·후속 원인분리 자료를 그대로 보존하며 이 다섯 자체 검증으로 성능 실패를 숨기거나 상한을 바꾸지 않는다. T04와 나머지9개는 계속 진행 중이다.

root가 검토 중인 런타임 PGO 설정 변경은 별도 소스/지문과 재검증 대상이다. 현재 af91/f3의 기능·화면·네이티브 근거를 이후 소스의 전체·규모·native·릴리스 통과로 확대하지 않는다. 공식 서명/업그레이드·성장/행동·R&D·장기는 각 작업의 별도 실제 근거로 판단한다.

이번 변경은 quiet 종료 통지를 받은 뒤 원장5개의 status/evidence, 진행도와 이 근거 문서만 수정한다. src/tests/scripts/workflow/VERSION·수용 조항/의존 그래프·Notion·readiness·기존 실패 원본을 바꾸지 않고 새 게임 빌드·기능/성능/성장/native 실행도 하지 않는다.
