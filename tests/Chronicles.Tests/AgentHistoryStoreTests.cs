using Chronicles.Core;
using Chronicles.Simulation;

internal static class AgentHistoryStoreTests
{
    public static void Run()
    {
        var store = new PersonalLogRingStore(population: 2);
        var log = store[0];
        for (var sequence = 1; sequence <= 150; sequence++)
        {
            log.Add(CreateEntry(sequence));
        }

        TestAssert.Equal(PersonalLogRingStore.Capacity, log.Count,
            "개인 최근 로그는 정확히 100행만 유지");
        TestAssert.Equal(51L, log[0].Sequence,
            "용량 초과 시 가장 오래된 항목부터 덮어씀");
        TestAssert.Equal(150L, log[^1].Sequence,
            "최신 항목 순서를 유지");

        log[^1] = CreateEntry(151);
        TestAssert.Equal(151L, log[^1].Sequence,
            "장기 행동 로그의 마지막 행 갱신을 지원");

        // 링과 해당 segment는 이미 할당되어 있으므로 이후 덮어쓰기는 managed
        // 저장소를 확장하지 않는다. JIT 이후 같은 스레드의 직접 할당량으로 보증한다.
        log.Add(CreateEntry(152));
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var sequence = 153; sequence <= 10_152; sequence++)
        {
            log.Add(CreateEntry(sequence));
        }
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        TestAssert.Equal(0L, allocatedAfter - allocatedBefore,
            "100행 이후 링 덮어쓰기는 managed 메모리를 추가 할당하지 않음");
        TestAssert.Equal(PersonalLogRingStore.Capacity, log.Count,
            "장기 덮어쓰기 뒤에도 보관 행 수 고정");
        TestAssert.Equal(10_152L, log[^1].Sequence,
            "장기 덮어쓰기 뒤 최신 sequence 유지");
    }

    private static PersonalLogEntry CreateEntry(long sequence) => new(
        sequence,
        new SimulationTick(sequence),
        LifeNumber: 1,
        PersonalLogKind.ActionCompleted,
        AgentActivity.Cultivating,
        WorldEventKind: null,
        ChronicleEventKind: null,
        IsPrimaryActor: true,
        RelatedAgent: null,
        Value: sequence);
}
