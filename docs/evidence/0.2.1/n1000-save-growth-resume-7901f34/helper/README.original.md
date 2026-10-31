# N1000 30월 저장에서의 파일 증가 재개 진단 준비

현재는 **준비만 완료**했다. helper 빌드·게임 실행·원본 큰 파일 재해시를 하지 않았다.
원래 부분 진단의 SIGKILL/부모 소멸 원인은 미확정이다. cgroup oom/oom_kill=0이나16GiB peak만으로
앱 오류·OOM·도구 종료를 단정하지 않는다. 원본과 원격41163711116은 변경하지 않는다.

`ResumeProbe.csproj`는 기존7901의 Game/Core/Hosting DLL만 직접 참조한다. ProjectReference·Tests DLL·
NuGet 추가·제품 빌드는 없다. `Program.cs`는 public API만 사용하며 아직 컴파일 검증하지 않았다.

- 원래 N1000 primary94,252,646B/SHA13963d…·backup23,035,565B/SHAff49e5…를 새 폴더로
  복사하고 양쪽 크기/SHA를 확인한다. 값은 원래 recovery-observation.json에서 인계했다.
- 복사본의 public GameSaveStore.Load 결과 Month==30을 요구한다.30월의 관찰/저장을 반복하지 않는다.
- 실제31..120월,6개월마다INV,60/90/120월의 INV→View무영향→observe
  (`stability:1000:{month}`)→hash→SaveAtomic→Load→hash→INV 순서를 유지한다.
- 파일 bytes·Hash/RNG/ID/Revision·실제 정수 흐름만 기록한다. 원본은 끝에도 크기/SHA를 재확인한다.
  출력 schema는 진단 전용이고 FormalT04Eligible=false다. 시간·관리힙·원래 전체120개월 합격으로 쓰지 않는다.

## root 검토·실행 승인 뒤 사용할 명령 — 지금 실행하지 않음

helper만 빌드한다. 참조 대상7901 제품 프로젝트/소스는 빌드하거나 고치지 않는다.

```bash
taskset -c 0 /workspace/.dotnet/dotnet build /workspace/scratch/save-growth-resume-7901f34/ResumeProbe.csproj -c Release --disable-build-servers -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false
```

단 한 번, 새 출력 경로로만 시작한다. 이미 출력이 있으면 재사용/재시도하지 않는다.

```bash
nohup setsid --fork /usr/bin/python3 /workspace/scratch/save-growth-resume-7901f34/supervise.py /workspace/scratch/v021-n1000-resume-7901f34-once1 > /workspace/scratch/v021-n1000-resume-7901f34-once1-launch.log 2>&1 < /dev/null
```

supervisor/게임 자식은 각자 새 session이고, 원래 N1000 상한1575초를 외부 중단 한도로만 사용한다.
상한이면 자기 private 자식 process group만 TERM→5초→KILL→최대5초 종료확인하며 무한 재시도하지 않는다.
제어 시각/초는 supervisor.json에만 남기며 정식 시간 판정으로 쓰지 않는다. setsid는 이미 관측한
SIGKILL 원인 규명이나 재발 방지를 보장하지 않는다.

예상 출력은 `resume-world/resume-result.json`, 새 primary/backup, `resume.log`, `supervisor.json`이다.
60/90/120 파일 bytes는 진단 관측값이며, 원격 최종120개월의 네 규모·모든 상한 통과와 별개다.
