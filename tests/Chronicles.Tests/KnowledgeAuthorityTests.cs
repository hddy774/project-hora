using Chronicles.Core;
using Chronicles.Simulation;

internal static class KnowledgeAuthorityTests
{
    public static void Run()
    {
        ApprovedInitialPopulationIsSeparatedAndDeterministic();
        EditionLineageAndKnowledgeClaimsAreTypedAndAtomic();
        PhysicalBindingsAndCheckpointGraphsAreValidated();
        WorldSchemaAndLazyObservationAreConnected();
    }

    private static void ApprovedInitialPopulationIsSeparatedAndDeterministic()
    {
        var content = ContentAuthorityStore.CreateInitial(0x1205UL);
        var store = KnowledgeAuthorityStore.CreateInitial(0x1205UL, 32, content);
        var sameContent = ContentAuthorityStore.CreateInitial(0x1205UL);
        var same = KnowledgeAuthorityStore.CreateInitial(0x1205UL, 32, sameContent);
        var snapshot = store.CreateSnapshot();

        TestAssert.Equal(KnowledgePopulationRules.InitialContentCount,
            snapshot.ContentCount, "승인 지식 Content 수");
        TestAssert.Equal(KnowledgePopulationRules.InitialEditionCount,
            snapshot.EditionCount, "승인 Edition 수");
        TestAssert.Equal(KnowledgePopulationRules.InitialPhysicalRecordCount,
            snapshot.PhysicalRecordCount, "승인 물리 기록물 수");
        TestAssert.Equal(0, snapshot.AccessCount, "실물 소유와 자동 이해 분리");
        TestAssert.Equal(
            ContentPopulationRules.InitialAuthorityInstanceCount
                + KnowledgePopulationRules.InitialPhysicalRecordCount,
            content.InstanceCount,
            "비경과 물리 서적 Instance가 같은 Content 권위에 중복 없이 존재");
        TestAssert.Equal(store.ComputeStateHash(), same.ComputeStateHash(),
            "동일 seed 지식 모집단 결정론");
        TestAssert.Equal(content.ComputeStateHash(), sameContent.ComputeStateHash(),
            "동일 seed 물리 기록물 배치 결정론");
        TestAssert.True(snapshot.Editions.All(value =>
            value.ConfidenceAvailability == KnowledgeValueAvailability.DataUnavailable),
            "미확정 초기 판본 신뢰도를 임의 수치로 위장하지 않음");
    }

    private static void EditionLineageAndKnowledgeClaimsAreTypedAndAtomic()
    {
        var content = ContentAuthorityStore.CreateInitial(0x1206UL);
        var store = KnowledgeAuthorityStore.CreateInitial(0x1206UL, 8, content);
        var initial = store.CreateCheckpoint();
        var firstContent = new KnowledgeContentId(initial.Contents[0].ContentId);
        var firstEdition = new KnowledgeEditionId(initial.Editions[0].EditionId);
        var otherEdition = new KnowledgeEditionId(initial.Editions[1].EditionId);

        var beforeInvalid = store.ComputeStateHash();
        var invalidLineage = store.TryCreateEdition(new CreateKnowledgeEditionCommand(
            firstContent,
            otherEdition,
            KnowledgeEditionRelation.Copy,
            ContributorAgent: 0,
            KnowledgeValueAvailability.Specified,
            ConfidenceHundredths: 6_000,
            Source(1),
            Tick: 10));
        TestAssert.Equal(KnowledgeCommandStatus.InvalidRelation, invalidLineage.Status,
            "다른 Content의 판본을 parent로 연결 거부");
        TestAssert.Equal(beforeInvalid, store.ComputeStateHash(),
            "실패한 판본 전이는 권위 상태 불변");

        var tampered = store.TryCreateEdition(new CreateKnowledgeEditionCommand(
            firstContent,
            firstEdition,
            KnowledgeEditionRelation.Tampered,
            ContributorAgent: 0,
            KnowledgeValueAvailability.Specified,
            ConfidenceHundredths: 6_000,
            Source(2),
            Tick: 10));
        TestAssert.True(tampered.Applied, "변조 판본은 원본과 별도 Edition으로 생성");

        var misunderstood = store.TryGrantAccess(new GrantKnowledgeAccessCommand(
            new KnowledgeAccessorRef(KnowledgeAccessorKind.Agent, 0),
            tampered.EditionId!.Value,
            KnowledgeAccessState.Studied,
            KnowledgeBeliefState.Misunderstood,
            KnowledgeValueAvailability.Specified,
            UnderstandingHundredths: 1_500,
            KnowledgeValueAvailability.Specified,
            ConfidenceHundredths: 5_000,
            Source(3),
            Tick: 11));
        TestAssert.True(misunderstood.Applied, "판본 접근과 오해 상태를 분리 기록");
        TestAssert.Equal(1, store.CreateSnapshot().MisunderstoodAccessCount,
            "오해 접근 집계");

        var corrected = store.TryGrantAccess(new GrantKnowledgeAccessCommand(
            new KnowledgeAccessorRef(KnowledgeAccessorKind.Agent, 0),
            firstEdition,
            KnowledgeAccessState.Understood,
            KnowledgeBeliefState.Corrected,
            KnowledgeValueAvailability.Specified,
            UnderstandingHundredths: 4_000,
            KnowledgeValueAvailability.Specified,
            ConfidenceHundredths: 7_000,
            Source(4),
            Tick: 12));
        TestAssert.True(corrected.Applied, "오해 교정 provenance 전이");
        TestAssert.Equal(0, store.CreateSnapshot().MisunderstoodAccessCount,
            "교정 뒤 오해 집계 제거");

        var beforeDowngrade = store.ComputeStateHash();
        var downgrade = store.TryGrantAccess(new GrantKnowledgeAccessCommand(
            new KnowledgeAccessorRef(KnowledgeAccessorKind.Agent, 0),
            firstEdition,
            KnowledgeAccessState.CanRead,
            KnowledgeBeliefState.Misunderstood,
            KnowledgeValueAvailability.Specified,
            UnderstandingHundredths: 500,
            KnowledgeValueAvailability.Specified,
            ConfidenceHundredths: 2_000,
            Source(5),
            Tick: 13));
        TestAssert.Equal(KnowledgeCommandStatus.InvalidTransition, downgrade.Status,
            "접근·교정 상태 역행 거부");
        TestAssert.Equal(beforeDowngrade, store.ComputeStateHash(),
            "실패한 접근 전이는 상태 불변");
        TestAssert.Equal(KnowledgeCommandStatus.DuplicateSource,
            store.TryCreateEdition(new CreateKnowledgeEditionCommand(
                firstContent,
                firstEdition,
                KnowledgeEditionRelation.Copy,
                ContributorAgent: 0,
                KnowledgeValueAvailability.DataUnavailable,
                ConfidenceHundredths: 0,
                Source(4),
                Tick: 14)).Status,
            "다른 전이에서도 동일 source_result 재생 거부");
    }

    private static void PhysicalBindingsAndCheckpointGraphsAreValidated()
    {
        var content = ContentAuthorityStore.CreateInitial(0x1207UL);
        var store = KnowledgeAuthorityStore.CreateInitial(0x1207UL, 16, content);
        var checkpoint = store.CreateCheckpoint();
        var firstBinding = checkpoint.RecordBindings[0];
        var duplicate = store.TryBindPhysicalRecord(new BindKnowledgeRecordCommand(
            new ContentInstanceId(firstBinding.PhysicalRecordInstanceId),
            new KnowledgeEditionId(checkpoint.Editions[1].EditionId),
            PhysicalRecordFormat.Book,
            Source(20),
            Tick: 20),
            content);
        TestAssert.Equal(KnowledgeCommandStatus.DuplicateBinding, duplicate.Status,
            "하나의 물리 기록물을 여러 Edition에 중복 binding하지 않음");

        var restored = KnowledgeAuthorityStore.Restore(checkpoint, 16, content);
        TestAssert.Equal(store.ComputeStateHash(), restored.ComputeStateHash(),
            "지식 권위 checkpoint hash 왕복");

        var invalidPhysical = checkpoint with
        {
            RecordBindings =
            [
                checkpoint.RecordBindings[0] with { PhysicalRecordInstanceId = 1 },
                .. checkpoint.RecordBindings.Skip(1),
            ],
        };
        TestAssert.Throws<InvalidDataException>(
            () => KnowledgeAuthorityStore.Restore(invalidPhysical, 16, content),
            "비경 Instance를 물리 서적으로 위조한 checkpoint 거부");
    }

    private static void WorldSchemaAndLazyObservationAreConnected()
    {
        var world = World.Create(0x1208UL, 24);
        var closed = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.None,
            worldChronicleCount: 0);
        TestAssert.Equal(0, closed.Content!.Knowledge!.EditionCount,
            "닫힌 콘텐츠 작업공간의 지식 query 0건");

        var summaryOnly = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0,
            knowledgeEditionLimit: 0);
        TestAssert.Equal(KnowledgePopulationRules.InitialEditionCount,
            summaryOnly.Content!.Knowledge!.EditionCount,
            "열린 지식 요약 권위 수");
        TestAssert.Equal(0, summaryOnly.Content.Knowledge.Editions.Count,
            "서적 내부 탭이 닫히면 Edition 행 materialization 0건");

        var opened = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0,
            knowledgeEditionLimit: 100);
        TestAssert.Equal(KnowledgePopulationRules.InitialEditionCount,
            opened.Content!.Knowledge!.Editions.Count,
            "서적 내부 탭에서만 Edition 행 투영");
        TestAssert.Equal(KnowledgePopulationRules.InitialPhysicalRecordCount,
            opened.Content.PhysicalRecordCount,
            "물리 서적은 실제 Content Instance 집계");

        var checkpoint = world.CreateCheckpoint();
        TestAssert.Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion,
            "지식·법칙·세계 생성·월 경계 랭킹 현재 checkpoint schema");
        TestAssert.True(checkpoint.Knowledge is not null, "지식 checkpoint 연결");
        var restored = World.Restore(checkpoint);
        TestAssert.Equal(world.ComputeStateHash(), restored.ComputeStateHash(),
            "세계 지식 권위 저장 hash 왕복");

        var schema27 = checkpoint with { SchemaVersion = 27, Knowledge = null };
        var migrated27 = World.Restore(schema27);
        TestAssert.Equal(
            world.ComputeStateHashForCheckpointSchema(27),
            migrated27.ComputeStateHashForCheckpointSchema(27),
            "schema 27 저장 hash 호환");
        TestAssert.Equal(0, migrated27.CreateCheckpoint().Knowledge!.Editions.Length,
            "schema 27은 권위 없는 지식을 임의 복원하지 않음");

        var schema26 = checkpoint with
        {
            SchemaVersion = 26,
            Content = null,
            Knowledge = null,
        };
        var migrated26 = World.Restore(schema26);
        TestAssert.Equal(KnowledgePopulationRules.InitialEditionCount,
            migrated26.CreateCheckpoint().Knowledge!.Editions.Length,
            "정식 0.0.20 schema 26은 승인 초기 지식 모집단으로 이식");

        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(checkpoint with { Knowledge = null }),
            "schema 28 지식 누락 거부");
    }

    private static ContentSourceResultId Source(ulong sequence) =>
        new(0xE012_0000_0000_0000UL + sequence);
}
