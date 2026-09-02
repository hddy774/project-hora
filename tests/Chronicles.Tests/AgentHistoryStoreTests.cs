using Chronicles.Core;
using Chronicles.Simulation;

internal static class AgentHistoryStoreTests
{
    public static void Run()
    {
        VerifySparseChunkAllocation();
        VerifyCompactNullableRoundTrip();
        VerifyCompactChronicleRoundTrip();
        VerifyChronicleRetentionAndCenturyArchive();

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

    private static void VerifyCompactChronicleRoundTrip()
    {
        TestAssert.Equal(31, CompactChronicleStore.StoredEntrySizeBytes,
            "영구 연대기 내부 entry는 중복 Person ID와 padding 없이 31바이트로 저장");
        var store = new CompactChronicleStore(population: 2);
        var chronicle = store[0];
        var expected = Enumerable.Range(0, 11)
            .Select(index => new LifeChronicleEntry(
                Sequence: 10_000 + index,
                Tick: new SimulationTick(20_000 + index),
                LifeNumber: index < 6 ? 1 : 2,
                AgeYears: 100 + index,
                PersonName: index < 6 ? "첫생" : "둘째생",
                Kind: (ChronicleEventKind)(index % 4),
                RealmStage: (RealmStage)(index % 3),
                MinorStage: (MinorStage)(index % 4),
                Value: -30_000 - index))
            .ToArray();
        chronicle.AddRange(expected);

        TestAssert.Equal(3, store.AllocatedBlockCount,
            "11개 연대기는 4행 논리 블록 3개만 사용");
        TestAssert.Equal(4_096, store.AllocatedEntrySlotCount,
            "연대기 segment는 4,096행 단위로 연속 할당");
        TestAssert.Equal(expected.Length, chronicle.Count,
            "압축 연대기 행 수 유지");
        for (var index = 0; index < expected.Length; index++)
        {
            TestAssert.Equal(expected[index], chronicle[index],
                $"압축 연대기 인덱스 {index} 왕복");
        }

        var enumerated = chronicle.ToArray();
        TestAssert.Equal(expected.Length, enumerated.Length,
            "순방향 블록 열거 행 수 유지");
        for (var index = 0; index < expected.Length; index++)
        {
            TestAssert.Equal(expected[index], enumerated[index],
                $"순방향 블록 열거 {index} 왕복");
        }
    }

    private static void VerifyChronicleRetentionAndCenturyArchive()
    {
        var details = new CompactChronicleStore(population: 1);
        var archive = new PersonalChronicleArchiveStore(population: 1, details);
        var chronicle = details[0];
        for (var sequence = 1; sequence <= 150; sequence++)
        {
            var entry = CreateChronicleEntry(sequence, ageYears: sequence - 1);
            chronicle.Add(entry);
            archive.Record(0, entry);
        }

        TestAssert.Equal(50, chronicle.Count,
            "완결 종합본과 중복 제거 후 미완결 구간 50건만 유지");
        TestAssert.Equal(101L, chronicle[0].Sequence,
            "100년 종합본에 포함된 상세 기록 제거");
        var volumes = archive.Project(
            0,
            currentLifeNumber: 1,
            currentAgeYears: 149,
            currentTick: new SimulationTick(149 * GameCalendar.TicksPerYear),
            currentName: "첫생",
            currentRealm: RealmStage.FoundationEstablishment,
            currentMinor: MinorStage.Floor2,
            currentLifeEnded: false);
        TestAssert.Equal(2, volumes.Count, "0~99·100~199세 종합 연대기");
        TestAssert.Equal(100, volumes[0].EventCount,
            "삭제된 상세 사건도 완결 종합본 집계에 보존");
        TestAssert.Equal(50, volumes[1].EventCount,
            "현재 작성 중 종합본 집계");

        archive.SetProtected(0, isProtected: true);
        for (var sequence = 151; sequence <= 250; sequence++)
        {
            var entry = CreateChronicleEntry(sequence, ageYears: sequence - 1);
            chronicle.Add(entry);
            archive.Record(0, entry);
        }
        TestAssert.Equal(150, chronicle.Count,
            "관심등록 뒤에는 기존 50건과 신규 상세 기록을 모두 보존");

        archive.SetProtected(0, isProtected: false);
        TestAssert.True(chronicle.Count <= PersonalChronicleRules.DetailedRetentionLimit,
            "관심 해제 시 세부 연대기 100건 상한 복원");
        TestAssert.True(chronicle.All(entry => entry.AgeYears >= 200),
            "완결 종합본과 중복되는 상세 연대기 제거");

        var cappedDetails = new CompactChronicleStore(population: 1);
        var cappedArchive = new PersonalChronicleArchiveStore(population: 1, cappedDetails);
        for (var century = 0; century <= 102; century++)
        {
            var age = century * PersonalChronicleRules.YearsPerVolume;
            var entry = CreateChronicleEntry(century + 1, age);
            cappedDetails[0].Add(entry);
            cappedArchive.Record(0, entry);
        }
        var capped = cappedArchive.Project(
            0,
            1,
            10_200,
            new SimulationTick(10_200L * GameCalendar.TicksPerYear),
            "첫생",
            RealmStage.HalfImmortal,
            MinorStage.Floor9,
            currentLifeEnded: false);
        TestAssert.Equal(PersonalChronicleRules.VolumeRetentionLimit, capped.Count,
            "100년 종합 연대기는 최근 100권만 유지");
        TestAssert.Equal(3L, capped[0].CenturyIndex,
            "100권을 넘으면 가장 오래된 종합본부터 제거");
    }

    private static LifeChronicleEntry CreateChronicleEntry(int sequence, int ageYears) => new(
        sequence,
        new SimulationTick((long)ageYears * GameCalendar.TicksPerYear),
        LifeNumber: 1,
        AgeYears: ageYears,
        PersonName: "첫생",
        ChronicleEventKind.FortuitousEncounter,
        RealmStage.FoundationEstablishment,
        MinorStage.Floor2,
        Value: sequence);
}
