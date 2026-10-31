# N1000 개시·순차 case 수명 heap 진단

이 자료는 `df7240556a260202a248e57b1f9868e372b9d995`의 기존 4DLL을 그대로 사용한
scratch 진단이다. 제품 지문은 `0cb2f90fe60e571032450eff0b172b1ee7bc44b51724bcff01fdc94dc2bbfb8e`다.
별도 전체 기능314/314의 출처와 DLL SHA는 [manifest](manifest.json)에 연결했다.
제품·공식 측정 하네스·예산 수정은 0이며 새 T04 합격 근거로 사용하지 않는다.

## 실제 범위

SDK10.0.403/runtime10.0.14, Release/PGOoff, CPU2 한 개 affinity에서 실행했다.
root CPU0·knowledge CPU1 진단과 같은 호스트를 공유했다. 열·전원·동시 부하를 통제한 비용 비교가 아니다.
진단 child에는 명시한 소수 환경 변수만 넘겨 서명·게시 자격 증명을 제외했다.

- fresh: 실제 N1000 fixture의 첫 checkpoint.
- sequential: N2·N100의 첫 checkpoint(조회·observe·실제 저장·재개 포함) 뒤 N1000 첫 checkpoint.
- follow-up fresh: 같은 작업 뒤 case 메서드 프레임이 반환된 위치의 heap을 추가 수집.

세 child는 모두 exit0이며, World 진행은0개월이다. 이전 case120개월은 실행하지 않았다.
따라서 원래120개월 작업 뒤1.234GB 스파이크를 재현했다고 주장하지 않는다.
각 개시 측정은 공식 순서처럼 두 번 full GC 직후·현재 snapshot/조회/observe/I/O 전에 읽고
명시 pause에서 dump했다. 프레임 반환 표본도 같은 두 번 GC를 사용했다.

## 관측값

| 실행 | 개시 retained(byte) | 프레임 반환 뒤 retained(byte) |
| --- | ---: | ---: |
| fresh | 61,877,768 | 545,406,464 |
| 순차 | 118,719,360 | 595,768,200 |
| 추가 fresh | 61,869,592 | 545,406,488 |

fresh/순차의 N1000 최종 실제 hash는 모두
`8ffff98da4011819fd4478b238d478db0f47abc9d9bc4a3655dd2dce83b91e8a`,
저장 bytes는23,035,565다. 어떤 상한·작업량·거부 조건도 바꿔 통과시킨 결과가 아니다.

[heap-types.json](heap-types.json)의 전체 `dumpheap -stat` 첫 표 기준:

| heap | GameWorld | GameState | Person | byte[] 합계 | char[] 합계 |
| --- | ---: | ---: | ---: | ---: | ---: |
| fresh 개시 | 1 | 1 | 1000 | 34,536,025 | 10,594 |
| 순차 개시 | 2 | 3 | 1200 | 34,543,645 | 50,342,290 |
| 프레임 반환 뒤 | 0 | 0 | 0 | 269,425,062 | 268,446,074 |

순차의 추가 World는 `Program.Sequence`의 stack slot으로 연결된다.
현재 World는 별도의 Checkpoint frame root도 갖는다.
관련 원문은 [world roots](sequential/roots-world-before-analyzer-crash.txt)에 있다.
이 관측은 다음 case까지 이전 graph가 남을 수 있는 하네스 수명 문제의 근거이며
게임의 장기 누수나 기존 전체 스파이크를 단독으로 설명하는 증거는 아니다.

프레임 반환 뒤에는 추적한 weak reference가 모두 false이고 heap의 World/State/Person 수도0이다.
그러나 큰 배열의 `gcroot`는0개로 나왔다. 마지막 GC 뒤 trim/finalizer 시점, 비root 배열과
수집/도구 한계를 추가로 구분해야 하므로 **이 결과로 ArrayPool 귀속을 확정하지 않는다.**
할당량·heap에 존재하는 배열·GC root·`GetTotalMemory` 시점은 서로 다른 관측이다.

## 실패와 다음 검토

SOS의 `gcroot -all` 인수 조회는 exit1, `dumpobj`는 두 gcroot 결과를 출력한 뒤 exit139로 실패했다.
[실패 인수 원문](post-return/roots-failed-all-option.txt)과 native 실패 전 원문을 그대로 남겼다.
지원되는 기본 `gcroot` 조회와 타입 집계를 사용했으며 실패를 성공으로 바꾸지 않았다.

확인된 범위의 최소 수정 후보는 정식 Stability의 case를 별도 NoInlining 수명 프레임으로
나누어 이전 world/final snapshot을 다음 case로 넘기지 않는 것이다.
현재 세계·공유/runtime 캐시를 포함한 원래 process 범위, 두 번 GC,0개월 포함 모든 checkpoint,
120개월·실제 I/O·상한은 유지해야 한다. 이 진단만으로 제품 Clone/Encode의 풀 정책을 바꾸지 않는다.

## 출처·재현

- [실제 probe](probe-Program.cs), [프로젝트](probe-project.csproj.txt), [감독기](supervise.py), [추가 수집](followup.py).
- 원래 빌드한4DLL은 `manifest.json`의 source 경로와 SHA를 사용한다. 프로젝트의 절대 HintPath는 해당 검증 환경을 기록한 것이다.
- 두 marker 사이에서만 감독기가 수집하고 명시적으로 resume한다. 새 실행은 기존 출력 폴더를 재사용하지 않는다.
- `markers.json`/`post-markers.json`은 원시 측정·GC index·환경, 각 result/child.log는 실제 작업·hash를 보존한다.
- 큰 dump3개는 `manifest.json`의 실제 로컬 경로·bytes·SHA256으로 보존했다. Git에 대형 dump나 저장 슬롯을 넣지 않았으며 원격 영속 자산 업로드를 완료했다고 표시하지 않는다.
- 이전 실측·실패 원문은 그대로이며 새 최종 수용은 통합 담당이 별도로 판정한다.
