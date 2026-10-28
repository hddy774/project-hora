# [M0-04] 월 정산 파이프라인 골격

## 배경 (왜)
현행 게임은 월 정산 시 세계 전체를 깊은 복사하고 모든 모듈이 서로를 직접 호출해 느리고 테스트하기 어려웠다(피드백 T7, P1, P2). 고정 순서의 시스템 파이프라인으로 교체할 뼈대를 만든다. 실제 경제 로직은 M1에서 각 시스템에 채운다.
관련: [기획서 §3, §7.2](../redesign/02-game-design.md), [코드 규칙 §9](../standards/coding-conventions.md)

## 범위
- 포함: `ISimulationSystem`, `WorldState`(빈 골격), `MonthContext`, `TurnPhase`, `MonthPipeline`, `ResolveResult`, 시스템별 실행 시간 측정, 테스트
- 제외: 실제 경제 시스템 구현, 명령(Command) 체계(M1), 저장/불러오기

## 인터페이스 (계약)
```csharp
namespace Hora.Simulation
{
    public enum TurnPhase { Planning, Resolving, Report }

    public interface ISimulationSystem
    {
        string Name { get; }
        void Execute(WorldState world, MonthContext context);
    }

    public sealed class WorldState
    {
        public int MonthIndex { get; }          // 0부터. 게임 시작 월 = 0
        public TurnPhase Phase { get; }
        public ulong Seed { get; }
        public WorldState(ulong seed);
        // M1에서 테이블(CompanyTable 등)이 추가된다.
        public ulong ComputeStateHash();        // 결정론 테스트용. 현재는 MonthIndex·Phase·Seed 기반 FNV-1a 64
    }

    public sealed class MonthContext
    {
        public int MonthIndex { get; }
        public ref DeterministicRandom RandomFor(int systemIndex); // 시스템별 독립 스트림
    }

    public sealed class MonthPipeline
    {
        public MonthPipeline(IReadOnlyList<ISimulationSystem> systems);  // 순서 = 실행 순서
        public ResolveResult Resolve(WorldState world, IProgress<PipelineProgress>? progress = null);
    }

    public readonly struct PipelineProgress { public readonly int Completed; public readonly int Total; public readonly string SystemName; }

    public sealed class ResolveResult
    {
        public bool IsSuccess { get; }
        public IReadOnlyList<SystemTiming> Timings { get; }   // 시스템별 경과 시간(마이크로초)
    }
}
```

## 동작 규칙
1. `Resolve` 는 `world.Phase == Planning` 일 때만 실행. 아니면 `IsSuccess=false` 반환(예외 아님).
2. 실행 순서: Phase=Resolving → 시스템을 생성자 순서대로 1회씩 `Execute` → MonthIndex+1 → Phase=Report.
   Report → Planning 전환은 `world.BeginPlanning()` 메서드로 (UI가 보고서를 닫을 때 호출).
3. `WorldState` 의 setter는 `internal` 로 두고 `InternalsVisibleTo("Hora.Simulation.Tests")` 허용.
4. 시스템 `i` 의 난수 스트림 = `new DeterministicRandom(world.Seed, (ulong)(monthIndex * 1000 + i))` — 다른 시스템을 추가해도 기존 시스템의 난수가 바뀌지 않도록 `i` 는 시스템 등록 인덱스.
5. 시간 측정은 `Stopwatch.GetTimestamp()` 사용. 측정값은 결과에만 담고 **상태 해시에 포함하지 않는다**(결정론).
6. 시스템이 예외를 던지면 잡지 않는다(버그). 단, 파이프라인은 world를 Resolving 상태로 남겨두며 이 사실을 XML 주석에 명시한다.
7. 세계 상태 복사(clone, 직렬화 왕복) 금지.
8. `progress` 는 각 시스템 완료 후 1회 보고 (R24 진행바용). null이면 보고하지 않는다.

## 수용 기준
- [ ] 위 타입 구현, 모든 타입·public 멤버 XML 주석
- [ ] 빈 시스템 10개 파이프라인으로 120개월 실행 테스트 통과
- [ ] 같은 시드로 두 번 120개월 실행 시 `ComputeStateHash` 동일
- [ ] `tools/Hora.Tools` 에 `bench-pipeline` 명령: 빈 시스템 10개 × 120개월, 월당 평균 시간·할당 출력. 월당 할당 0B (progress=null)
- [ ] 코드 규칙·format·전체 테스트 통과

## 테스트 케이스
- `Resolve_WhenNotPlanning_ReturnsFailureWithoutRunning`
- `Resolve_RunsSystemsInRegistrationOrder`
- `Resolve_IncrementsMonthAndEntersReport`
- `BeginPlanning_FromReport_EntersPlanning`
- `Resolve_ReportsProgressOncePerSystem`
- `RandomFor_SameMonthAndSystem_IsDeterministic`
- `Resolve_120Months_SameSeed_SameHash`

## 참고
- 선행: M0-02, M0-03
