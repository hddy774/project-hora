using Chronicles.Core;
using Chronicles.Simulation;

internal static class AgentHistoryStoreTests
{
    public static void Run()
    {
        VerifySparseChunkAllocation();
        VerifyCompactNullableRoundTrip();

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

        var chronological = log.ToArray();
        TestAssert.Equal(10_053L, chronological[0].Sequence,
            "ToArray는 링 wrap 이후에도 오래된 항목부터 복사");
        TestAssert.Equal(10_152L, chronological[^1].Sequence,
            "ToArray는 링 wrap 이후 최신 항목을 마지막에 복사");
    }

    private static void VerifySparseChunkAllocation()
    {
        var store = new PersonalLogRingStore(population: 65);
        TestAssert.Equal(0, store.AllocatedChunkCount,
            "로그가 없을 때 entry 청크를 할당하지 않음");
        TestAssert.Equal(0, store.AllocatedEntrySlotCount,
            "로그가 없을 때 entry 슬롯을 할당하지 않음");

        store[0].Add(CreateEntry(1));
        TestAssert.Equal(3, store.AllocatedChunkCount,
            "첫 로그에 자주 사용하는 30행만 선할당");
        TestAssert.Equal(64 * 30, store.AllocatedEntrySlotCount,
            "64명 segment의 첫 30행을 초기화 경계에서 할당");

        store[63].Add(CreateEntry(2));
        TestAssert.Equal(3, store.AllocatedChunkCount,
            "같은 segment의 첫 30행 범위는 청크를 공유");

        store[64].Add(CreateEntry(3));
        TestAssert.Equal(6, store.AllocatedChunkCount,
            "마지막 1명 segment는 별도 30행을 선할당");
        TestAssert.Equal((64 * 30) + 30, store.AllocatedEntrySlotCount,
            "마지막 segment는 실제 인원수만큼만 슬롯 할당");

        for (var sequence = 4; sequence <= 13; sequence++)
        {
            store[0].Add(CreateEntry(sequence));
        }
        TestAssert.Equal(6, store.AllocatedChunkCount,
            "30행 이하에서는 시뮬레이션 중 청크를 추가하지 않음");

        for (var sequence = 14; sequence <= 32; sequence++)
        {
            store[0].Add(CreateEntry(sequence));
        }
        TestAssert.Equal(6, store.AllocatedChunkCount,
            "30번째 항목까지 선할당 범위를 사용");

        store[0].Add(CreateEntry(33));
        TestAssert.Equal(7, store.AllocatedChunkCount,
            "31번째 항목에서 네 번째 10행 청크를 지연 할당");
        TestAssert.Equal((64 * 40) + 30, store.AllocatedEntrySlotCount,
            "31행 이상의 로그 범위는 단계적으로 확장");
    }

    private static void VerifyCompactNullableRoundTrip()
    {
        TestAssert.Equal(36, PersonalLogRingStore.StoredEntrySizeBytes,
            "개인 로그 내부 entry는 nullable padding 없이 36바이트로 저장");

        var store = new PersonalLogRingStore(population: 2);
        var log = store[0];
        var expected = new PersonalLogEntry[32];
        for (var mask = 0; mask < expected.Length; mask++)
        {
            var activity = (mask & 0b00001) != 0
                ? AgentActivity.Teaching
                : (AgentActivity?)null;
            var worldEvent = (mask & 0b00010) != 0
                ? WorldEventKind.ReincarnationCrossingCompleted
                : (WorldEventKind?)null;
            var chronicleEvent = (mask & 0b00100) != 0
                ? ChronicleEventKind.ReincarnationCrossingCompleted
                : (ChronicleEventKind?)null;
            var relatedAgent = (mask & 0b01000) != 0
                ? new AgentId(1)
                : (AgentId?)null;
            var isPrimaryActor = (mask & 0b10000) != 0;
            expected[mask] = new PersonalLogEntry(
                Sequence: 1_000 + mask,
                Tick: new SimulationTick(2_000 + mask),
                LifeNumber: 3_000 + mask,
                Kind: (PersonalLogKind)(mask % 3),
                Activity: activity,
                WorldEventKind: worldEvent,
                ChronicleEventKind: chronicleEvent,
                IsPrimaryActor: isPrimaryActor,
                RelatedAgent: relatedAgent,
                Value: -4_000 - mask);
            log.Add(expected[mask]);
        }

        for (var index = 0; index < expected.Length; index++)
        {
            TestAssert.Equal(expected[index], log[index],
                $"nullable 조합 {index}의 인덱스 조회 왕복");
        }

        var copied = log.ToArray();
        TestAssert.Equal(expected.Length, copied.Length,
            "압축 로그 ToArray 행 수 유지");
        for (var index = 0; index < expected.Length; index++)
        {
            TestAssert.Equal(expected[index], copied[index],
                $"nullable 조합 {index}의 ToArray 왕복");
        }

        var replacement = expected[^1] with
        {
            Activity = null,
            WorldEventKind = null,
            ChronicleEventKind = null,
            IsPrimaryActor = false,
            RelatedAgent = null,
        };
        log[^1] = replacement;
        TestAssert.Equal(replacement, log[^1],
            "압축 링의 논리 인덱스 수정 왕복");
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
