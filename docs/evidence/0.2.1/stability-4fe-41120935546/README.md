# 개편 전 4fe 장기 안정성 실패 원문

실행41120935546은 **failure**로 종료했다. 실제 소스4fe1fe4b95dcdef6cf8cb9e315fd99684f2bc135 / 지문528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e의 개편 전 역사 자료다. global.json403 복원·Android CI·Game 파일 무동작 분할 뒤의 최종T04 합격으로 승격하지 않는다. PR84 병합·원장·readiness·게시 상태 변경은 없다.

실행: https://github.com/hddy774/project-hora/actions/runs/41120935546 · job124286966029 · 시도1. 실제 CLI는2026-10-30 20:09:23.462014 UTC 시작,2026-10-31 01:10:02.150245 UTC 종료0이며 외부330분 강제중단false다. job은01:10:08 UTC에failure로 종료했다. 단계9 success와 실제완료 검증 단계10 failure를 구분한다. SDK10.0.403 / .NET10.0.14 / Ubuntu24.04.5 /4CPU이며 실제 Tests.runtimeconfig의TieredPGO=false를 확인했다. 개별PGO 환경변수·호스트 열·전원은 미수집이다.

| 초기 인구 | 실제/목표 개월 | 실제/상한 초 | 하네스 case flag | 보유 관리메모리 최대/상한 B | 저장 최대/상한 B |
| ---: | ---: | ---: | :---: | ---: | ---: |
|2|120/120|40.835/167|true|164513968/47725032 **초과**|5027868/8748435|
|100|115/120|329.392/309 **초과**|false|201584576/262419240|65047350/45658311 **초과**|
|1000|59/120|1650.952/1575 **초과**|false|1235174752/995045448 **초과**|193378160/241598442|
|10000|120/120|16005.401/15098 **초과**|false|2747000768/2192400000 **초과**|730975582/1194685341|

10,000명은120개월에 도달했지만 시간 상한을 넘겼다.100/1000명은 기간도 미완료다. 2명 case flag는true지만 별도 보유 관리메모리 예산을 넘었으므로T04 통과가 아니다. 원문complete=false/passed=false/status=failed, executionFailure=null이다. finishedCases4는 시도4개가 끝났다는 뜻이다. 첫 실제 검증 실패는 당시Python14행의 `report['complete'] is True and report['passed'] is True and report['status']=='completed'`이며 실제false/false/failed다.

관측된18개 경계의불변식·실제GameSaveStore 파일 저장/재개 hash일치·관찰자View 무변경·공개observe 승인 플래그는모두true다. 실제월은[0,30,60,90,120], [0,30,60,90,115], [0,30,59], [0,30,60,90,120]이다. 이를4사례 전부의120개월/시간/메모리/저장 통과나 다른 생산·운송 명령의 장기검사로 확대하지 않는다. 메모리는강제GC 뒤의동일프로세스표본이며 현재세계만의힙이나peak RAM이아니다. 전체프로세스 최대RSS14,044,976KiB는별도원문지표다.

구8433 실행과공통15경계의StateHash·workload·저장크기는모두같다. [대조 원문](historical-comparison.json)은관측된공통월만비교한다. 최종월·runner·미측정열/전원/개별PGO환경값차이를무시한시간개선율이나인과관계는주장하지않는다. [읽기 분석](trend-review.md)은확인된경로와미확인원인을분리한다.

원래계획ec853c715eafc4341fa47802e4de445f0caf165833470d15beb69550905b6c46·기준선1842a4dbffb66fa9c8489ad31343d94fed933f022c8d63be430b774c37998484·예산7365b80e3de602dfe331a5348f4008808f6620744577582d07bc7f33f11dc272는바꾸지않았다. assessment-budget.original.json은별도대조용채택예산이며원래ZIP에들었던입력이라고하지않는다. 이번job은창세1개만실행했고기존whole314afaa049…/source94 원문을사전대조했다. 새전체기능실행을했다는뜻이아니다.

artifact13024151431은1,308,262B/SHA88a22d118d6824815b5836241a2097d76dea95fe07b82442731e307f966c9b80로서버digest와다운로드가일치했다. manifest28개와실제4DLL SHA·Release/소스PE기록·내장source문자열을확인했다. raw/result.json SHA는99c684a45c3134bf7527a911e0f277209077cdd758ac937307eeb8db27721f01이다.

Git에는ZIP의텍스트·runtimeconfig·입력·PE/SHA기록을바이트동일하게보존한다. 생성DLL/완전ZIP은 `/workspace/scratch/stability-4fe-41120935546/`와원격artifact에있고Git에는넣지않았다. 원격artifact만료예정은2026-11-14다. job.original.log는연결도구의전체UTF-8디코딩본문과같고API JSON은디코딩구조의표현이다. slot-manifest는실제최종8파일SHA/크기지만큰저장본문은원래업로드제외였다. 외부에서그본문을독립재생했다고하지않는다.
