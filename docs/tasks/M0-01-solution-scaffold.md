# [M0-01] .NET 솔루션·프로젝트 구조 생성

## 배경 (왜)
C# 재설계의 첫 작업. 엔진과 독립된 시뮬레이션 코어를 Unity·Godot 어느 쪽에서도 쓸 수 있도록 .NET Standard 2.1 / C# 9 라이브러리로 만든다.
관련: [기획서 §7.1, §8](../redesign/02-game-design.md), [코드 규칙 §1](../standards/coding-conventions.md)

## 범위
- 포함: 솔루션 파일, 프로젝트 5개, 공통 빌드 설정, 각 프로젝트의 빈 placeholder 타입 1개와 테스트 1개, 루트 `.gitignore` 보강
- 제외: 엔진 프로젝트(`Hora.Client`), CI 워크플로(M0-05), 도메인 타입 구현(M0-02~04)
- 구버전 `games/monthly-economy/` 는 건드리지 않는다.

## 만들 구조
```
Hora.sln
Directory.Build.props
src/Hora.Domain/Hora.Domain.csproj           netstandard2.1, LangVersion 9.0
src/Hora.Simulation/Hora.Simulation.csproj   netstandard2.1, LangVersion 9.0, → Hora.Domain 참조
tests/Hora.Domain.Tests/                     net8.0, NUnit, → Hora.Domain
tests/Hora.Simulation.Tests/                 net8.0, NUnit, → Hora.Simulation
tools/Hora.Tools/                            net8.0 콘솔 앱, → Hora.Simulation (BenchmarkDotNet 패키지 추가)
```

## 동작 규칙
1. `Directory.Build.props` (루트) 공통 설정:
   - `Nullable=enable`, `ImplicitUsings=disable`, `GenerateDocumentationFile=true`
   - `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`
2. `src/` 하위 프로젝트에만 `TreatWarningsAsErrors=true`, `LangVersion=9.0`, `TargetFramework=netstandard2.1`.
   (조건: `$(MSBuildProjectDirectory)` 가 `src` 하위일 때 — `src/Directory.Build.props` 를 별도로 두고 루트 것을 Import 해도 된다)
3. C# 9 `record` 사용을 위해 `src/Hora.Domain/Internal/IsExternalInit.cs` 추가 (`namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }`, 주석 포함).
4. `Hora.Simulation` 이 `Hora.Domain` 외 다른 프로젝트를 참조하면 안 된다. `Hora.Domain` 은 어떤 프로젝트도 참조하지 않는다.
5. NuGet 패키지 버전은 `Directory.Packages.props` (Central Package Management)로 한 곳에서 관리한다.
6. 각 src 프로젝트에 `AssemblyMarker` 같은 placeholder 타입 1개를 두되 코드 규칙의 주석 형식(파일 헤더 + `/// <summary>`)을 지킨다.
7. 루트 `.gitignore` 에 `bin/`, `obj/`, `*.user`, `.vs/`, `BenchmarkDotNet.Artifacts/`, `TestResults/` 추가 (기존 항목 유지).

## 수용 기준
- [ ] 저장소 루트에서 `dotnet build -warnaserror` 성공 (경고 0)
- [ ] `dotnet test` 성공 (placeholder 테스트 2개 통과)
- [ ] `dotnet format --verify-no-changes` 성공 (루트 `.editorconfig` 적용)
- [ ] `src/` 프로젝트가 netstandard2.1 / C# 9 로 빌드됨 (`file-scoped namespace` 를 쓰면 빌드 실패하는지 직접 확인 후 되돌림)
- [ ] public 멤버 XML 주석을 지우면 빌드가 실패함 (CS1591 오류) 확인
- [ ] `games/monthly-economy/` 변경 없음

## 테스트 케이스
- `AssemblyMarker_Exists_IsLoadable` (Domain, Simulation 각 1개)

## 참고
- 루트 `.editorconfig`, `AGENTS.md`
