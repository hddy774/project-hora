# N1000 저장 파일 증가: SIGKILL 원문과 단발30월 재개 관측

새 프로세스의30월 저장 재개가31..120월을 진행해 **종료0·세 경계 확인·원본 보존**으로 끝났다.
최종 primary는 **199,347,513B**, SHA256은
`4cfcd688a1955e30e03582d570d23eaffd696ac44455e4b1f9b8573011697445`다.
이 진단은 파일 크기·정수 논리 관측이며 **정식 시간·관리힙·원래 전체120개월·네 사례 전체/T04 수용 근거가 아니다.**

## 첫 시도와 중단 원형

[원래 결과](original-interrupted/result.json)·[실행 기록](original-interrupted/diagnostic.json)·
[로그](original-interrupted/measurement.log)·[선행 선택검사](original-interrupted/preceding-functional.json)·
[복구 관측](original-interrupted/recovery-observation.json)을 바이트 동일하게 보존했다.
원래 raw는 `finishedCases=[2,100]`, N1000 진행 중, `status=running/complete=false/passed=false` 그대로다.
PID170032의 wait status9/SIGKILL·부모 소멸/exec session미확인과 cgroup 관측을 기록했지만 원인은 미확정이다.
oom/oom_kill0과16GiB peak만으로 원인을 단정하거나 raw 상태를 완료/합격으로 고쳐 쓰지 않는다.

## 단 한 번의 별도 재개

[재개 원문](resumed/resume-result.json)·[로그](resumed/resume.log)·[supervisor](resumed/supervisor.json)는
각자 새 session의 supervisor170429/child170430과 실제 runtime10.0.14를 기록한다.
제어 시작04:15:26.438758~종료04:29:09.234494 UTC, exit0·외부상한 미도달·최대시도1이다.
제어 소요 시간은 정식 성능 수치가 아니다.

원래 primary94,252,646B/SHA13963d…·backup23,035,565B/SHAff49e5…를 새 출력으로 복사하고
정확Month30 Load를 요구했다. 이미 기록한30월 observe/저장을 반복하지 않고31..120월을 진행했다.
36..120월의6개월INV15개와60/90/120의 INV→View무영향→observe(`stability:1000:month`)→
hash→SaveAtomic→Load→hash→INV 순서를 유지했다. 법칙·seed·인구·게임 규칙·기간을 보정하지 않았다.

| 경계 월 | primary B | backup B | 저장 후 상태 Hash |
| --- | ---: | ---: | --- |
| 60 | 129,339,785 | 94,252,646 | `f875eebaffbb39ec285674623b2636bb30c9dd629108c528437dbfdc9758ba7d` |
| 90 | 163,392,036 | 129,339,785 | `4256528a7409e1bb85ccbb249f7d103172e1c60180d2ffc412bb2bb29d339544` |
| 120 | 199,347,513 | 163,392,036 | `70ab212ec499b71f4a6a32dd8fe8b3f89a74dc8de39b915368735ffbd1fde235` |

세 경계의 조회 무영향·실제 명령·저장재개 Hash·INV는 모두 true다.120월에는 RNG
`11972361760660350311`, NextId11698·Revision351·people/living1000·출생/사망0,
실제 완료 지식47603·물리 작업21·채취39·생산5를 기록했다. 이 값은 이 진단의 관측이다.
최종 파일 한 값은 기존 N1000 저장 상한241,598,442B 아래지만 정식 사례 전체 수용으로 계산하지 않는다.

[네 파일 digest](slot-digests.json)는 종료 뒤 한 번 독립 계산한 실제 파일 SHA/크기와
원본이 복구 관측과 동일함을 확인한다. 큰 slot 본문은 로컬 경로만 남겼고 Git에는 넣지 않았다.
로컬 파일이나 별도 자산이 없으면 checksum만으로 정확한 입력을 복원할 수 없다는 한계가 있다.

## 소스·빌드·문서 범위

[실제 helper C#](helper/Program.cs)·[직접 DLL 참조 프로젝트](helper/ResumeProbe.csproj)·
[supervisor 원형](helper/supervise.py)·[준비 당시 기록](helper/prepared-manifest.json)·
[당시 안내](helper/README.original.md)·[실제 runtimeconfig](helper/runtimeconfig.original.json)를 보존했다.
준비 문서의 미컴파일/미실행 표기는 당시 원형이며 현재 결과로 고치지 않았다. root가 helper만
경고/오류0으로 빌드해 한 번 실행했다. 문서 작성자는 새 빌드나 게임을 실행하지 않았다.

사용한 제품은 frozen7901 소스 `7901f344462ebc1f98d7105efd79ab0aa058b8fe`/FP `c91c3d7b7cf0441f207825d0490d38501072b63b0364234fbd7cf973b1f620f1`의 기존 Game/Core/Hosting DLL3개다.
문서 base `f3627aa4db4282a8aebb572bffe2048261e0756a`와 이 세 runtime source 경로의 변경은0이다.
후속 Python 소스2464337ec577b2894eb87468c2c72916b1d6b8bf/FP0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2는 Android 준비 흐름·그 검사·launch-timeout fixture3경로가 바뀐 별도 출처다. root의 새 전체 검사는 실행 중이며 이7901/C91 진단으로 합격을 인증하지 않는다. 다른 제품FP·1cf 중단 실행도 이번 범위가 아니다.
시간/관리힙·정식120개월·4사례/T04PASS·native/릴리스·원장/readiness/진행도 승격을 하지 않는다.

[출처](provenance.json)·[전체 SHA색인](manifest.json)을 함께 읽는다. 기존 원문·slot·제품/예산/계획을
수정하지 않았고 큰 slot/DLL/중복ZIP은 추가하지 않았다. 실제1cf 중단 원문은 별도 보존 범위다.
