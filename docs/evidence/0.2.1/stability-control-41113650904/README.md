# 개편 전 장기 안정성 실패 원문 보존

이 기록은 구 control 실행41113650904의 **실패**다. 측정 소스8433dd26b6a8380d32f9b5d7093f88f1e8eb43a6 / 제품 지문4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304이며, 새528 소스의 합격이나 구조 개편 후 최종T04 근거로 승격하지 않는다. 사용자 피드백에 따른 global.json·Android CI·Game 파일 무동작 분할 뒤에는 새 소스의 정확성과 성능을 별도로 확인한다. PR84 병합·원장·readiness·게시 상태를 바꾸지 않았다.

실행: https://github.com/hddy774/project-hora/actions/runs/41113650904 · job124262168261 · 시도1. 실제 CLI는2026-10-30 19:15:29.431424 UTC에 시작하여2026-10-31 00:20:16.281704 UTC에 종료0, 외부330분 강제중단false다. 작업은00:20:24에failure로 종료했다. 단계9의CLI success와 단계10의실제 결과 검증 failure를 구분한다. SDK10.0.403 / .NET10.0.14 / Ubuntu24.04.5 / 실제4CPU다. 열·전원과 개별PGO 환경변수는 미수집이다. 구 runtimeconfig에는 TieredPGO 항목이 없으므로 효과값을 단정하지 않는다.

| 초기 인구 | 실제/목표 개월 | 실제/상한 초 | 하네스 case flag | 보유 관리메모리 최대/상한 B | 저장 최대/상한 B |
| ---: | ---: | ---: | :---: | ---: | ---: |
| 2 |120/120|22.836/167|true|166029656/47725032 **초과**|5027868/8748435|
|100|112/120|333.640/309 **초과**|false|202108824/262419240|63783092/45658311 **초과**|
|1000|56/120|1695.154/1575 **초과**|false|1234119392/995045448 **초과**|188361600/241598442|
|10000|110/120|16220.450/15098 **초과**|false|2744902600/2192400000 **초과**|710527774/1194685341|

원문은complete=false/passed=false/status=failed, executionFailure=null이다. finishedCases=[2,100,1000,10000]는 네 사례의 시도가 끝났다는 뜻이며 네 사례가120개월을 마쳤다는 뜻이 아니다. 하네스 case flag는 기간·시간·예외·최소checkpoint를 판단하며 별도 메모리/저장 예산까지 통과했다는 뜻이 아니다. 단계10의 첫 실제 실패는 당시Python14행의 `report['complete'] is True and report['passed'] is True and report['status']=='completed'`이며 실제값은false/false/failed다.

관측된18개 경계의불변식·실제GameSaveStore.SaveAtomic/Load hash일치·관찰자View 무변경·공개observe 승인 플래그는 모두true다. 각월은[0,30,60,90,120], [0,30,60,90,112], [0,30,56], [0,30,60,90,110]이다. 이를4×120개월·각5회완료 또는 다른 생산/운송 명령의 장기검사로 확대하지 않는다. 보유 관리메모리는 같은 프로세스의강제GC 뒤표본이며 연속peak RAM이나 현재세계만의힙이 아니다.

원래 계획ec853c715eafc4341fa47802e4de445f0caf165833470d15beb69550905b6c46, 기준선1842a4dbffb66fa9c8489ad31343d94fed933f022c8d63be430b774c37998484, 예산7365b80e3de602dfe331a5348f4008808f6620744577582d07bc7f33f11dc272를 유지했다. 수치 상향·조건 변경·새 실측·새 프로파일·제품 소스 수정은 없다. 확인된 추세와 미확인 원인 후보는 [읽기 분석](trend-review.md)에 분리했다.

원본ZIP artifact13021868705는1308159B, SHA256 f3f7891885faf568452b474a0677f07209c45c96024006a20f147188db988233이며 서버digest와다운로드가일치했다. artifact-manifest28개 파일을 완전한 로컬ZIP 추출본과대조하고 네DLL의SHA·Release/소스PE기록·내장source문자열을 확인했다. 완전한ZIP/네DLL은 `/workspace/scratch/stability-control-41113650904/`와 원격artifact에 보존한다. Git에는원문텍스트·runtimeconfig·입력·PE/SHA기록을 저장하고 생성DLL바이너리를 넣지 않는다. 원격artifact 만료예정은2026-11-14이며 영구저장 링크로 주장하지 않는다.

raw/result.json SHA256은25545738ad7daf632e08963440d3deba349aa8a17f5ce2722ff2cef152f8f393다. raw 파일은 원래ZIP의바이트와같다. job.original.log는GitHub 연결도구가반환한전체UTF-8디코딩본문과같으며, API JSON은그디코딩구조를보존한표현이다. slot-manifest에는실제8개최종파일의SHA/크기가있지만 수GB저장본문은업로드제외였다. 따라서 외부에서그8개본문을다운로드해독립재생했다고하지않는다. 실제18회파일왕복은원래하네스결과/로그로확인한다.

assessment-budget.original.json은별도보존한채택예산의바이트원문이며 원래실행ZIP에포함됐던입력이라고하지않는다.

원문job.original.log의타임스탬프뒤공백9곳은원문바이트보존때문에유지했다. 기본diff공백검사는이불변원문한파일을제외한모든추가파일에적용했고, 원문은별도바이트동등성과SHA로검증했다.
