# T04 case 수명 프레임 분리

기준은 `b15992b6150afb8e4f3348501c51051a7f7733d0`, 관련 이슈는 #233이다.
[진단 PR235](https://github.com/hddy774/project-hora/pull/235)의 순차 개시 heap에서
추가 World/State와 `Program.Sequence` stack root 경로를 관측했다.
원래 지표가 현재 세계·공유/runtime 캐시·작은 이전 결과를 포함한다는 계약에 맞춰,
이전 case의 world/final snapshot/Checkpoint closure를 다음 case 프레임까지 넘기지 않는다.

## 소유와 변경

생산 변경은 없고 `tests/Chronicles.Game.Tests/PerformanceStabilityScenarios.cs` 한 파일만 수정한다.
case 본문을 `MeasureStabilityCase`로 옮기고 `MethodImplOptions.NoInlining`을 지정한다.
반환하는 private record는 기존 작은 report, passed bool, 완료 로그 문자열뿐이다.
report는 원래처럼 수치·해시·작은 Laws 사전·checkpoint/workload 집계이며 world나 final snapshot을 담지 않는다.

호출자는 이전 순서대로 aggregate 갱신→reports.Add→finishedCases/runningPopulation→SaveProgress→Console를 수행한다.
완료 문자열은 이미 멈춘 Stopwatch와 바뀌지 않는 최종 수치로 helper 안에서 계산하고 원래 출력 위치로 넘긴다.
case 시간의 시작·정지 위치, total 시간, 실패 catch/기록/재throw, 실제 I/O와 진행 순서는 유지한다.
fixture factory를 별도로 나누지 않는다.

## 그대로인 계약

- process 범위 retained와 원래 두 번의 full GC 및 KeepAlive.
- 0개월을 포함한 모든 checkpoint,120개월,30개월 저장·6개월 불변식과 원래 시간/retained/save 상한.
- 실제 조회·observe·원자 저장/재개·hash·RNG·인구와 실물 흐름.
- JSON report의 필드/순서, partial/실패 의미, source·원문·예산·제품 codec.
- 공유 풀과 runtime 캐시는 계속 지표에 포함한다. 배열의 풀 귀속이나 전체 스파이크 원인은 확정하지 않는다.

이 변경으로 T04 상한을 통과한다고 주장하지 않는다. 기존 실패 원문은 보존하고 통합 후 새 지문의
전체 정확성과 정식 실측은 통합 담당이 수행한다.

## 실제 검증

- SDK10.0.403, CPU2, `-m:1 -p:UseSharedCompilation=false` Release: 경고0·오류0.
- 기존 기능 `T04 sensitivity contract` 선택5/5. 이 짧은 호출에는 별도 affinity를 지정하지 않았다.
- `python3 scripts/verify-v021-release.py self-test`: 통과. 실제 서명/게시/플랫폼/성능 실행이 아니다.
- [Roslyn 대조](source-comparison.json): 기존 case12개 statement의 서명/본문 토큰·주석,
  report 생성식/필드 순서, 완료 문자열, 시작/종료 progress 순서, outer failure 처리와 미변경 메서드 동일.
- [컴파일 결과](compiled-metadata.json): 실제 NoInlining flag와 반환 필드 object/bool/string 확인.
- checkpoint 최소 수를3→2로 낮추거나 반환 필드를 GameWorld로 바꾼 scratch 부정 입력2개는 모두 거부했다.
- 새 전체·장기·성능·heap 비교 실행은0이다.

첫 추출 스크립트가 내부 로그를 선택해 낸 중간 구문 오류와 self-test의 잘못된 CLI flag는
각 원문 log로 보존했다. 이를 수정한 최종 소스로 위 검증을 수행했다.
[검증 요약](validation.json), [선택 원문](functional.json), [Release 원문](release-build.log)을 확인할 수 있다.

## 정적 대조 재현

`verify-source.cs`와 `verify-project.csproj.txt`를 checkout 밖 scratch에 복사한다.
SDK Roslyn을 직접 참조하며 제품 프로젝트 의존성을 추가하지 않는다.

```bash
repo_root="$(pwd)"
task_dir="$repo_root/docs/tasks/T04-STABILITY-CASE-LIFETIME"
proof_dir="$(mktemp -d)"
cp "$task_dir/verify-project.csproj.txt" "$proof_dir/Proof.csproj"
git show b15992b6150afb8e4f3348501c51051a7f7733d0:tests/Chronicles.Game.Tests/PerformanceStabilityScenarios.cs > "$proof_dir/before.cs"
taskset -c 2 dotnet build "$proof_dir/Proof.csproj" -c Release -m:1 \
  -p:UseSharedCompilation=false -p:VerificationSource="$task_dir/verify-source.cs"
taskset -c 2 dotnet "$proof_dir/bin/Release/net10.0/Proof.dll" "$proof_dir/before.cs" \
  "$repo_root/tests/Chronicles.Game.Tests/PerformanceStabilityScenarios.cs" "$proof_dir/comparison.json" \
  "$repo_root/tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll"
```

검사기는 메서드나 World를 실행하지 않고 구문/토큰과 실제 컴파일 메타데이터만 읽는다.
