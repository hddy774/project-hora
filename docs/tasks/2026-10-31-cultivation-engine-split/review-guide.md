# 검토 안내

이 변경은 같은 `Chronicles.Game.Cultivation.CultivationEngine`의 메서드를 옮긴다.
명령과 결과의 타입, 공개 API, 호출 경로와 저장 상태는 그대로다.
다른 클래스로 위임하거나 실행 방식을 고르는 분기를 추가하지 않는다.

## 파일별 읽기 순서

| 파일 | 메서드 수 | 검토할 내용 |
| --- | ---: | --- |
| `CultivationEngine.cs` | 7 | `Execute`의 명령 분기, 입력·법칙 검증, 결과·거부·사망 델타 |
| `CultivationEngine.Preparation.cs` | 9 | `Preflight`, 실제 지식 출처 선택, 장소 권리·경로 조건, 형성 품질과 부모 근거 |
| `CultivationEngine.Practice.cs` | 6 | 오행 흡수→정련→순환→체화, 보조 공정, 실제 공급 차감과 병목 |
| `CultivationEngine.Formation.cs` | 5 | 준비→개시→금단 파단→형성 판정, 취소 경계 |
| `CultivationEngine.Recovery.cs` | 3 | 안착, 실제 회복 비용, 구조 손상·퇴행 |
| `CultivationEngine.Martial.cs` | 2 | 독립 무예 단련·돌파 |
| `CultivationEngine.Progression.cs` | 11 | 소경지 겁난, 완성·층·성장 목표, 고유 구조 성장·축소 |

새 partial의 다중 문장 줄을 공백·개행으로 정돈했다. 기존 매개변수·호출문 줄 나눔과 주석은 유지했다.
원래 파일의 582줄은 7개 파일 총 772줄로 나뉘며 가장 큰 파일은 187줄이다.
필드·정적 생성자·인스턴스 생성자는 전후 모두 0개다. 초기화 코드를 다른 파일로 옮기지 않았다.

## 멤버 동일성 확인

[member-comparison.json](member-comparison.json)은 기준 소스와 분할 파일들을 SDK 내 Roslyn으로
파싱한 실제 결과다. 43개 메서드 각각에 대해 접근 한정자, 이름, 원래·이동 위치와
서명·본문·전체 멤버의 토큰 SHA256을 기록한다. 토큰 종류와 정확한 문자열을 대조하므로
정수·문자열 리터럴, 연산자, 괄호, 호출 순서의 변경도 거부한다. 공백·개행은 비교에서 제외하고
메서드 주석은 별도로 동일성을 검사한다.

namespace·클래스 구조와 한정자도 검사하며, 클래스 한정자에 `partial`만 추가할 수 있다.
멤버 추가·누락·중복, 새 필드·생성자, 파일별 using·조건부 지시문·클래스 속성 추가는 거부한다.
검증기의 두 부정 입력은 별도 scratch 복사본에서 실제로 검사했다.
`MartialTrain`의 강도 상한 `20000→20001` 변경과 정적 필드 추가가 모두 거부됐다.

재현 예시는 저장소 루트에서 실행한다. SDK `10.0.403`의 Roslyn을 직접 참조하므로
검증 도구에 새 NuGet 패키지나 제품 프로젝트 참조를 추가하지 않는다.

```bash
repo_root="$(pwd)"
proof_dir="$(mktemp -d)"
task_dir="$repo_root/docs/tasks/2026-10-31-cultivation-engine-split"
git show 404767ad4dc576ea3669cddd42529a50741cbf6f:src/Chronicles.Game/Cultivation/CultivationEngine.cs > "$proof_dir/before.cs"
cp "$task_dir/verify-project.csproj.txt" "$proof_dir/verify.csproj"
taskset -c 1 dotnet build "$proof_dir/verify.csproj" -c Release \
  -p:VerificationSource="$task_dir/verify-members.cs" -p:UseSharedCompilation=false -m:1
taskset -c 1 dotnet "$proof_dir/bin/Release/net10.0/verify.dll" \
  "$proof_dir/before.cs" "$repo_root/src/Chronicles.Game/Cultivation" "$proof_dir/comparison.json"
```

## 기능 검사 범위

Release 빌드는 `Core→Game→Hosting→Game.Tests`를 포함하며 CPU1,
`UseSharedCompilation=false`, `-m:1`로 수행했다. 기존 기능 검사는 빌드한 테스트 DLL의
`--filter`와 `--report`만 사용했다. 선택 문자열과 원래 결과는
[functional-results.json](functional-results.json)에 보존한다.

```bash
taskset -c 1 dotnet build tests/Chronicles.Game.Tests/Chronicles.Game.Tests.csproj \
  -c Release -p:UseSharedCompilation=false -m:1
taskset -c 1 dotnet tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll \
  --filter '감응 출처 선택' --report /tmp/cultivation-evidence.json
taskset -c 1 python3 scripts/validate-v021-product-boundary.py --self-test
git diff --check
```

검사 범위는 10경지·30경로, 원소 공정 보존, 준비·예약·단일 판정·파단 사망,
동결된 지식 출처, 회복·전환·법칙 변경, 모든 소경지 겁난, 독립 무예 10단계,
8축 조회, 실제 공개 명령·중간 저장 복원과 AI 수행 연결이다.
새 제품 테스트는 추가하지 않았다. 공통 validator의 단일 파일 가정을 변경할 필요가 없었으며
정적 제품 경계 검사도 통과했다.

이 근거는 멤버 동일성과 선택 기능 검사를 보여준다. 통합 이후 전체 정확성과 새 제품 지문의
성능·성장·장기 검증은 별도다. 이전 `528` 지문의 합격이나 기존 실측 결과를 이번 파일 분할의
실측 결과로 간주하지 않는다.
