using Chronicles.Core;
using Chronicles.Simulation;

internal static class ContentAuthorityTests
{
    public static void Run()
    {
        DefinitionsAndInitialSecretRealmsAreStable();
        ItemLifecycleIsAtomicAndIndexed();
        DiscoveryAndCheckpointRoundTripPreserveAuthority();
        InvalidCheckpointGraphsAreRejected();
        WorldPersistenceAndSelectiveObservationAreConnected();
        AiExplorationDiscoversAndChroniclesSecretRealms();
    }

    private static void DefinitionsAndInitialSecretRealmsAreStable()
    {
        TestAssert.Equal(11, ContentDefinitionRegistry.All.Count, "PC 간략화 Definition 수");
        TestAssert.Equal(
            ContentDefinitionRegistry.All.Count,
            ContentDefinitionRegistry.All.Select(definition => definition.Id).Distinct().Count(),
            "Definition 안정 ID 유일성");
        TestAssert.Equal(
            ContentDefinitionRegistry.All.Count,
            ContentDefinitionRegistry.All.Select(definition => definition.StableRef)
                .Distinct(StringComparer.Ordinal).Count(),
            "Definition stable ref 유일성");

        var first = ContentAuthorityStore.CreateInitial(0x2100UL);
        var sameSeed = ContentAuthorityStore.CreateInitial(0x2100UL);
        var differentSeed = ContentAuthorityStore.CreateInitial(0x2101UL);
        var publicView = first.CreateSnapshot(ContentDiscovererRef.Observer, includeHidden: false);
        var diagnostic = first.CreateSnapshot(ContentDiscovererRef.Observer, includeHidden: true);

        TestAssert.Equal(ContentPopulationRules.InitialAuthorityInstanceCount, first.InstanceCount,
            "초기 비경·재료·아이템·독립 전승 Instance 수");
        TestAssert.Equal(ContentPopulationRules.InitialNaturalSecretRealmCount,
            diagnostic.NaturalSecretRealms, "자연 비경 수");
        TestAssert.Equal(ContentPopulationRules.InitialArtificialSecretRealmCount,
            diagnostic.ArtificialSecretRealms, "인공 비경 수");
        TestAssert.Equal(ContentPopulationRules.ApprovedMaterialStackCount,
            diagnostic.MaterialStackCount, "승인 재료 stack 수");
        TestAssert.Equal(
            ContentPopulationRules.ApprovedItemInstanceCount,
            diagnostic.ItemInstanceCount,
            "승인 아이템 실물 수");
        TestAssert.Equal(ContentPopulationRules.ApprovedIndependentInheritanceCount,
            diagnostic.CreatedWorkCount, "승인 독립 전승 실물 수");
        TestAssert.Equal(0, diagnostic.ItemsAndMaterials.Count,
            "아이템 내부 탭이 닫히면 행 materialization 0건");
        var itemPage = first.CreateSnapshot(
            ContentDiscovererRef.Observer,
            includeHidden: false,
            secretRealmLimit: 0,
            itemLimit: 100);
        TestAssert.Equal(100, itemPage.ItemsAndMaterials.Count,
            "아이템 내부 탭에서만 첫 page 투영");
        TestAssert.Equal(
            ContentPopulationRules.ApprovedMaterialStackCount
                + ContentPopulationRules.ApprovedItemInstanceCount
                + ContentPopulationRules.ApprovedIndependentInheritanceCount,
            itemPage.ItemResultTotal,
            "재료·아이템·독립 전승 page 전체 수");
        TestAssert.Equal(0, publicView.SecretRealms.Count, "미발견 비경 관찰자 비공개");
        TestAssert.Equal(ContentPopulationRules.InitialSecretRealmCount,
            diagnostic.SecretRealmResultTotal, "개발 진단에서만 숨은 비경 투영");
        TestAssert.Equal(first.ComputeStateHash(), sameSeed.ComputeStateHash(),
            "동일 seed 콘텐츠 결정론");
        TestAssert.True(first.ComputeStateHash() != differentSeed.ComputeStateHash(),
            "다른 seed 비경 배치 구분");
    }

    private static void ItemLifecycleIsAtomicAndIndexed()
    {
        var store = ContentAuthorityStore.CreateInitial(7);
        var material = ContentDefinitionRegistry.Get("material.artifact");
        var owner = new ContentOwnerRef(ContentOwnerKind.Agent, 0);
        var created = store.TryCreate(new CreateContentInstanceCommand(
            material.Id,
            owner,
            new ContentHolderRef(ContentHolderKind.PersonalInventory, 0),
            LocationId: 0,
            Quantity: 10,
            ContentQualityAvailability.Specified,
            QualityHundredths: 7_999,
            ConditionHundredths: 10_000,
            Source(1),
            Tick: 1));
        TestAssert.True(created.Applied, "법보 재료 stack 생성");
        var id = created.InstanceId!.Value;
        TestAssert.Equal(1, store.GetByOwner(owner).Count, "소유자 index 생성");
        TestAssert.Equal(1, store.GetAtLocation(0).Count(value => value == id), "위치 index 생성");

        var beforeFailure = store.ComputeStateHash();
        var rejected = store.TryTransfer(new TransferContentCommand(
            id,
            new ContentOwnerRef(ContentOwnerKind.Agent, 1),
            new ContentOwnerRef(ContentOwnerKind.City, 0),
            new ContentHolderRef(ContentHolderKind.CityWarehouse, 0),
            NewLocationId: 1,
            Source(2),
            Tick: 2));
        TestAssert.Equal(ContentCommandStatus.OwnerMismatch, rejected.Status, "잘못된 소유자 이전 거부");
        TestAssert.Equal(beforeFailure, store.ComputeStateHash(), "실패한 이전은 원자 불변");

        TestAssert.True(store.TryReserve(new ChangeContentStatusCommand(id, owner, Source(3), 3)).Applied,
            "재료 예약");
        TestAssert.Equal(ContentCommandStatus.InvalidTransition,
            store.TryConsume(new ConsumeContentCommand(id, owner, 1, Source(4), 4)).Status,
            "예약 중 소비 거부");
        TestAssert.True(store.TryReleaseReservation(
            new ChangeContentStatusCommand(id, owner, Source(5), 5)).Applied,
            "예약 해제");
        var consumed = store.TryConsume(new ConsumeContentCommand(id, owner, 3, Source(6), 6));
        TestAssert.True(consumed.Applied, "부분 소비");
        TestAssert.Equal(7L, store.GetInstance(id).Quantity, "부분 소비 잔량");
        TestAssert.Equal(ContentCommandStatus.DuplicateSource,
            store.TryConsume(new ConsumeContentCommand(id, owner, 3, Source(6), 6)).Status,
            "동일 source replay 거부");

        var city = new ContentOwnerRef(ContentOwnerKind.City, 0);
        TestAssert.True(store.TryTransfer(new TransferContentCommand(
            id,
            owner,
            city,
            new ContentHolderRef(ContentHolderKind.CityWarehouse, 0),
            NewLocationId: 1,
            Source(7),
            Tick: 7)).Applied,
            "도시 창고 원자 이전");
        TestAssert.Equal(0, store.GetByOwner(owner).Count, "이전 소유자 index 제거");
        TestAssert.Equal(1, store.GetByOwner(city).Count, "새 소유자 index 연결");

        TestAssert.True(store.TryConsume(new ConsumeContentCommand(id, city, 7, Source(8), 8)).Applied,
            "전량 소비");
        TestAssert.Equal(ContentInstanceStatus.Consumed, store.GetInstance(id).Status,
            "전량 소비 terminal 상태");
        TestAssert.Equal(0, store.GetByOwner(city).Count, "terminal 소유 index 제외");
        TestAssert.Equal(0, store.GetAtLocation(1).Count(value => value == id),
            "terminal 위치 index 제외");
    }

    private static void DiscoveryAndCheckpointRoundTripPreserveAuthority()
    {
        var store = ContentAuthorityStore.CreateInitial(9);
        var realm = new ContentInstanceId(1);
        var agent = new ContentDiscovererRef(ContentDiscovererKind.Agent, 0);
        TestAssert.True(store.TryDiscover(new DiscoverContentCommand(
            realm, agent, ContentDiscoveryState.Located, Source(100), Tick: 10)).Applied,
            "개인 비경 위치 발견");
        TestAssert.Equal(1,
            store.CreateSnapshot(agent, includeHidden: false).ViewerKnownSecretRealms,
            "개인 지식 투영");
        TestAssert.Equal(0,
            store.CreateSnapshot(ContentDiscovererRef.Observer, includeHidden: false)
                .PubliclyLocatedSecretRealms,
            "개인 지식과 공개 지식 분리");
        TestAssert.True(store.TryDiscover(new DiscoverContentCommand(
            realm,
            ContentDiscovererRef.Observer,
            ContentDiscoveryState.Located,
            Source(101),
            Tick: 11)).Applied,
            "관찰자 공개 발견");
        TestAssert.Equal(1,
            store.CreateSnapshot(ContentDiscovererRef.Observer, includeHidden: false)
                .SecretRealmResultTotal,
            "공개 비경 목록");

        var checkpoint = store.CreateCheckpoint();
        var restored = ContentAuthorityStore.Restore(checkpoint);
        TestAssert.Equal(store.ComputeStateHash(), restored.ComputeStateHash(),
            "콘텐츠 checkpoint hash 왕복");
        TestAssert.Equal(ContentCommandStatus.DuplicateSource,
            restored.TryDiscover(new DiscoverContentCommand(
                new ContentInstanceId(2),
                agent,
                ContentDiscoveryState.Located,
                Source(101),
                Tick: 12)).Status,
            "복원 후 source replay 거부");

        TestAssert.True(restored.TryCollapse(new ChangeContentStatusCommand(
            realm,
            ContentOwnerRef.World,
            Source(102),
            Tick: 13)).Applied,
            "비경 붕괴 전이");
        TestAssert.Equal(ContentInstanceStatus.Collapsed, restored.GetInstance(realm).Status,
            "비경 붕괴 terminal 상태");
        TestAssert.Equal(0, restored.GetAtLocation(restored.GetInstance(realm).LocationId)
            .Count(value => value == realm), "붕괴 비경 위치 index 제외");
    }

    private static void InvalidCheckpointGraphsAreRejected()
    {
        var checkpoint = ContentAuthorityStore.CreateInitial(11).CreateCheckpoint();
        var duplicateInstance = checkpoint with
        {
            Instances = [.. checkpoint.Instances, checkpoint.Instances[0]],
        };
        TestAssert.Throws<InvalidDataException>(
            () => ContentAuthorityStore.Restore(duplicateInstance),
            "중복 Instance checkpoint 거부");

        var child = checkpoint.Provenance[1] with
        {
            ParentProvenanceId = checkpoint.Provenance[0].ProvenanceId,
        };
        var crossInstanceParent = checkpoint with
        {
            Provenance = [checkpoint.Provenance[0], child, .. checkpoint.Provenance.Skip(2)],
        };
        TestAssert.Throws<InvalidDataException>(
            () => ContentAuthorityStore.Restore(crossInstanceParent),
            "다른 Instance provenance parent 거부");
    }

    private static void WorldPersistenceAndSelectiveObservationAreConnected()
    {
        var world = World.Create(0xC021UL, 32);
        TestAssert.Equal(
            ContentPopulationRules.InitialAuthorityInstanceCount
                + KnowledgePopulationRules.InitialPhysicalRecordCount,
            world.TotalContentInstances, "세계 콘텐츠 store 연결");
        var hashBeforeObservation = world.ComputeStateHash();
        var closed = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.None,
            worldChronicleCount: 0);
        TestAssert.Equal(0, closed.Content!.TotalInstances, "닫힌 콘텐츠 query 0건");
        var opened = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0);
        TestAssert.Equal(
            ContentPopulationRules.InitialAuthorityInstanceCount
                + KnowledgePopulationRules.InitialPhysicalRecordCount,
            opened.Content!.TotalInstances, "열린 콘텐츠 요약 투영");
        TestAssert.Equal(0, opened.Content.SecretRealms.Count,
            "일반 관찰에서 미발견 비경 행 비공개");
        TestAssert.Equal(0, opened.Content.ItemsAndMaterials.Count,
            "아이템 내부 탭이 닫히면 세계 DTO 행 0건");
        var itemsOpened = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0,
            contentItemLimit: ContentWorkspacePageSize);
        TestAssert.Equal(ContentWorkspacePageSize,
            itemsOpened.Content!.ItemsAndMaterials.Count,
            "아이템 내부 탭에서만 100행 투영");
        TestAssert.Equal(hashBeforeObservation, world.ComputeStateHash(),
            "콘텐츠 관찰 무영향");

        var checkpoint = world.CreateCheckpoint();
        TestAssert.Equal(29, checkpoint.SchemaVersion, "콘텐츠·지식·법칙 checkpoint schema 29");
        TestAssert.True(checkpoint.Content is not null, "콘텐츠 checkpoint 연결");
        var restored = World.Restore(checkpoint);
        TestAssert.Equal(world.ComputeStateHash(), restored.ComputeStateHash(),
            "세계 콘텐츠 저장 hash 왕복");

        var schema26 = checkpoint with
        {
            SchemaVersion = 26,
            Content = null,
            Knowledge = null,
        };
        var migrated = World.Restore(schema26);
        TestAssert.Equal(
            world.ComputeStateHashForCheckpointSchema(26),
            migrated.ComputeStateHashForCheckpointSchema(26),
            "schema 26 저장 hash 호환");
        TestAssert.Equal(
            ContentPopulationRules.InitialAuthorityInstanceCount
                + KnowledgePopulationRules.InitialPhysicalRecordCount,
            migrated.CreateCheckpoint().Content!.Instances.Length,
            "schema 26은 seed 기반 콘텐츠·지식 실물로 이식");

        var developerWorld = World.Create(
            0xC022UL,
            16,
            WorldBalanceConfiguration.Default with { DeveloperModeEnabled = true });
        var diagnostics = developerWorld.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: ContentPopulationRules.InitialSecretRealmCount);
        TestAssert.Equal(ContentPopulationRules.InitialSecretRealmCount,
            diagnostics.Content!.SecretRealms.Count,
            "세계 설정 모드에서만 숨은 비경 진단");
        var summaryOnly = developerWorld.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0);
        TestAssert.Equal(0, summaryOnly.Content!.SecretRealms.Count,
            "비경 내부 탭이 닫히면 행 materialization 0건");
        TestAssert.Equal(ContentPopulationRules.InitialSecretRealmCount,
            summaryOnly.Content.SecretRealmResultTotal,
            "행 생략 중에도 콘텐츠 요약 유지");

        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(checkpoint with { Content = null }),
            "schema 29 콘텐츠 누락 거부");
    }

    private const int ContentWorkspacePageSize = 100;

    private static void AiExplorationDiscoversAndChroniclesSecretRealms()
    {
        var world = World.Create(0xD021UL, 64);
        world.AdvanceTo(GameCalendar.FromYears(101));

        var checkpoint = world.CreateCheckpoint();
        var discoveries = checkpoint.Content!.Discoveries;
        var publicDiscoveries = discoveries.Count(value =>
            value.DiscovererKind == ContentDiscovererKind.Observer
            && value.State >= ContentDiscoveryState.Located);
        var agentDiscoveries = discoveries.Count(value =>
            value.DiscovererKind == ContentDiscovererKind.Agent
            && value.State >= ContentDiscoveryState.Located);
        TestAssert.True(publicDiscoveries > 0, "AI 탐색 결과가 공개 비경 지식으로 연결");
        TestAssert.Equal(publicDiscoveries, agentDiscoveries,
            "각 공개 발견에는 발견자 개인 지식 provenance가 선행");

        var century = checkpoint.CenturyChronicle!.Entries[0];
        TestAssert.Equal(publicDiscoveries, century.SecretRealmsDiscovered,
            "첫 100년 비경 발견이 세계 역사서에 압축");
        TestAssert.True(
            century.UniqueSecretRealmDiscoverers is > 0 and <= 64,
            "세계 역사서에 고유 비경 발견자 수 보존");
        TestAssert.True(checkpoint.Knowledge!.Access.Length > 0,
            "AI의 실제 서적 구매가 지식 접근 권위로 연결");

        var restored = World.Restore(checkpoint);
        TestAssert.Equal(world.ComputeStateHash(), restored.ComputeStateHash(),
            "AI 비경 발견·100년 역사서 저장 hash 왕복");
    }

    private static ContentSourceResultId Source(ulong sequence) =>
        new(0xC021_0000_0000_0000UL + sequence);
}
