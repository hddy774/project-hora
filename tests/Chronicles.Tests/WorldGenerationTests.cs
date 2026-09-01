using Chronicles.Core;
using Chronicles.Simulation;

internal static class WorldGenerationTests
{
    public static void Run()
    {
        SameSeedAndConfigurationAreReproducible();
        DomainSeedsAndFixedUniqueContentAreSeparated();
        DisabledMapRandomizationPreservesBaseline();
        ProductionRegionsAreEmbeddedInOcheonPoints();
        ConfiguredPopulationAndVisibleProfilesAreConnected();
        OngoingGenerationAndCheckpointRoundTripAreDeterministic();
    }

    private static void DisabledMapRandomizationPreservesBaseline()
    {
        var configuration = CreateCompactConfiguration() with { RandomizeMap = false };
        var first = World.Create(8101, 16, WorldBalanceConfiguration.Default, configuration);
        var second = World.Create(8201, 16, WorldBalanceConfiguration.Default, configuration);
        var firstLocations = first.CreateSnapshot(
            1,
            0,
            ObservationSections.Map).Map.Locations;
        var secondLocations = second.CreateSnapshot(
            1,
            0,
            ObservationSections.Map).Map.Locations;
        TestAssert.True(
            firstLocations.Select(value => (value.Id, value.Name, value.Biome))
                .SequenceEqual(secondLocations.Select(value => (value.Id, value.Name, value.Biome))),
            "지도 무작위 해제 시 seed와 무관한 기준 지도");
        TestAssert.True(
            firstLocations.Select(value => (value.Id, value.Name, value.Biome))
                .SequenceEqual(WorldMapRules.AllLocations.Select(value =>
                    (value.Id, value.KoreanName, value.Biome))),
            "지도 무작위 해제 시 기존 18개 Production 지점 보존");
        TestAssert.Equal(
            WorldGeographyRules.Blueprint,
            first.CreateSnapshot(1, 0, ObservationSections.Map).Map.Geography!,
            "지도 무작위 해제 시 하천대륙 기준 청사진 보존");
    }

    private static void ProductionRegionsAreEmbeddedInOcheonPoints()
    {
        var world = World.Create(
            8251,
            16,
            WorldBalanceConfiguration.Default,
            CreateCompactConfiguration());
        var map = world.CreateSnapshot(1, 0, ObservationSections.Map).Map;
        var mappedPointIds = new HashSet<(byte Layer, ushort PointId)>();

        foreach (var region in map.Locations)
        {
            var pointId = WorldGeographyRules.GetProductionPointId(region.X, region.Y);
            TestAssert.True(
                WorldGeographyRules.TryGetProductionRegionCoordinates(
                    pointId,
                    out var mappedX,
                    out var mappedY),
                "기존 Production 지역은 오천주 중앙 지점에 대응");
            TestAssert.Equal(region.X, mappedX, "오천주 지점에서 지역 X 복원");
            TestAssert.Equal(region.Y, mappedY, "오천주 지점에서 지역 Y 복원");
            TestAssert.True(
                mappedPointIds.Add((region.Atlas.HeavenlyLayer, pointId)),
                "층천별 Production 지역의 지점 대응은 중복되지 않음");

            var points = region.Atlas.HeavenlyLayer == 1
                ? map.Geography!.FirstHeavenPoints
                : map.Geography!.SecondHeavenPoints;
            var point = points[pointId];
            TestAssert.Equal(pointId, point.PhysicalPointId, "지점 stable 물리 ID 유지");
            TestAssert.Equal(region.Atlas.HeavenlyLayer, point.HeavenlyLayer,
                "지역 활동과 지점 층천 일치");
        }

        TestAssert.Equal(WorldMapRules.LocationCount, mappedPointIds.Count,
            "18개 Production 지역 활동을 대륙·주·지점 계층에 통합");
        TestAssert.True(
            !WorldGeographyRules.TryGetProductionRegionCoordinates(0, out _, out _),
            "중앙 3×3 밖 지점은 활동 권위를 임의 연결하지 않음");
    }

    private static void SameSeedAndConfigurationAreReproducible()
    {
        var configuration = CreateCompactConfiguration();
        var first = World.Create(4401, 16, WorldBalanceConfiguration.Default, configuration);
        var second = World.Create(4401, 16, WorldBalanceConfiguration.Default, configuration);
        var firstCheckpoint = first.CreateCheckpoint();
        var secondCheckpoint = second.CreateCheckpoint();

        TestAssert.Equal(first.ComputeStateHash(), second.ComputeStateHash(),
            "같은 seed·설정 canonical hash");
        TestAssert.True(
            firstCheckpoint.Generation!.Items.SequenceEqual(
                secondCheckpoint.Generation!.Items),
            "같은 seed 아이템 카탈로그");
        TestAssert.True(
            firstCheckpoint.Generation.Books.SequenceEqual(
                secondCheckpoint.Generation.Books),
            "같은 seed 서적 카탈로그");
        TestAssert.True(
            firstCheckpoint.Generation.Monsters.SequenceEqual(
                secondCheckpoint.Generation.Monsters),
            "같은 seed 요수 카탈로그");

        var randomBefore = firstCheckpoint.RandomState;
        _ = first.CreateSnapshot(
            10,
            0,
            ObservationSections.Map | ObservationSections.Content,
            contentItemOffset: ContentPopulationRules.ApprovedMaterialStackCount,
            contentItemLimit: 20,
            knowledgeEditionLimit: 20);
        TestAssert.Equal(randomBefore, first.CreateCheckpoint().RandomState,
            "절차 생성 관찰은 게임플레이 RNG 무소비");
    }

    private static void DomainSeedsAndFixedUniqueContentAreSeparated()
    {
        var configuration = CreateCompactConfiguration();
        var first = World.Create(5511, 16, WorldBalanceConfiguration.Default, configuration)
            .CreateCheckpoint();
        var second = World.Create(6611, 16, WorldBalanceConfiguration.Default, configuration)
            .CreateCheckpoint();
        var firstGeneration = first.Generation!;
        var secondGeneration = second.Generation!;

        var firstFixed = firstGeneration.Items.Where(value => value.IsFixedUnique).ToArray();
        var secondFixed = secondGeneration.Items.Where(value => value.IsFixedUnique).ToArray();
        TestAssert.Equal(4, firstFixed.Length, "고정 유니크 4종");
        TestAssert.True(
            firstFixed.Select(value => value.GeneratedId).SequenceEqual(
                secondFixed.Select(value => value.GeneratedId)),
            "고정 유니크 ID는 seed와 무관");
        TestAssert.True(
            !firstGeneration.Items.Where(value => !value.IsFixedUnique)
                .Select(value => value.GeneratedId)
                .SequenceEqual(secondGeneration.Items.Where(value => !value.IsFixedUnique)
                    .Select(value => value.GeneratedId)),
            "무작위 아이템 ID는 세계 seed에 따라 변화");
        TestAssert.True(
            !firstGeneration.Books.Select(value => value.Name)
                .SequenceEqual(secondGeneration.Books.Select(value => value.Name)),
            "서적 이름 profile은 세계 seed에 따라 변화");
        TestAssert.True(
            !firstGeneration.Monsters.Select(value => value.Name)
                .SequenceEqual(secondGeneration.Monsters.Select(value => value.Name)),
            "요수 이름 profile은 세계 seed에 따라 변화");

        var changedItemCount = configuration with { InitialRandomItemDefinitionCount = 12 };
        var baselineWorld = World.Create(7711, 16, WorldBalanceConfiguration.Default, configuration);
        var changedWorld = World.Create(7711, 16, WorldBalanceConfiguration.Default, changedItemCount);
        var baselineAgent = baselineWorld.GetAgentSnapshot(new AgentId(0));
        var changedAgent = changedWorld.GetAgentSnapshot(new AgentId(0));
        TestAssert.Equal(
            baselineAgent.Aptitudes.SpiritualRoot,
            changedAgent.Aptitudes.SpiritualRoot,
            "아이템 수량 변경은 인물 RNG 순서에 무영향");
        TestAssert.Equal(
            baselineAgent.Aptitudes.Comprehension,
            changedAgent.Aptitudes.Comprehension,
            "도메인 seed 분리 오성 재현");
        TestAssert.True(
            baselineWorld.CreateCheckpoint().Generation!.Books.SequenceEqual(
                changedWorld.CreateCheckpoint().Generation!.Books),
            "아이템 종류 변경은 서적 도메인에 무영향");
    }

    private static void ConfiguredPopulationAndVisibleProfilesAreConnected()
    {
        var configuration = CreateCompactConfiguration();
        var world = World.Create(8801, 16, WorldBalanceConfiguration.Default, configuration);
        var checkpoint = world.CreateCheckpoint();
        var generation = checkpoint.Generation!;
        TestAssert.Equal(12, generation.Items.Length, "고정 4 + 무작위 아이템 8종");
        TestAssert.Equal(6, generation.Books.Length, "설정 서적 내용 6종");
        TestAssert.Equal(6, generation.Monsters.Length, "3지형 × 요수 2종");
        TestAssert.True(
            generation.Books.All(value => value.Kind switch
            {
                KnowledgeContentKind.Technique => value.PrimaryEffect is
                    GeneratedBookEffectKind.TechniqueTraining
                    or GeneratedBookEffectKind.TechniqueUnderstanding
                    or GeneratedBookEffectKind.CultivationInsight,
                KnowledgeContentKind.Spell => value.PrimaryEffect is
                    GeneratedBookEffectKind.SpellTraining
                    or GeneratedBookEffectKind.SpellUnderstanding
                    or GeneratedBookEffectKind.CombatInsight,
                _ => false,
            }),
            "공법·법술 서적의 주 효과 영역 일치");

        var snapshot = world.CreateSnapshot(
            10,
            0,
            ObservationSections.Map | ObservationSections.Content,
            contentItemOffset: ContentPopulationRules.ApprovedMaterialStackCount,
            contentItemLimit: configuration.InitialItemInstanceCount,
            knowledgeEditionLimit: configuration.InitialBookEditionCount);
        var content = snapshot.Content
            ?? throw new InvalidOperationException("Content 관찰 결과가 필요합니다.");
        TestAssert.Equal(configuration.InitialItemInstanceCount,
            content.ItemInstanceCount,
            "설정 초기 아이템 실물 수");
        TestAssert.Equal(configuration.InitialBookContentCount,
            content.Knowledge!.ContentCount,
            "설정 서적 내용 수");
        TestAssert.Equal(configuration.InitialBookEditionCount,
            content.Knowledge.EditionCount,
            "설정 서적 판본 수");
        TestAssert.Equal(configuration.InitialPhysicalBookCount,
            content.Knowledge.PhysicalRecordCount,
            "설정 실물 서적 수");
        TestAssert.True(content.ItemsAndMaterials
            .Where(value => value.Kind == ContentInstanceKind.Item)
            .All(value => value.GeneratedDefinition is not null),
            "아이템 실물은 생성 이름·효과 profile을 노출");
        TestAssert.True(content.Knowledge.Editions
            .All(value => value.GeneratedDefinition is not null),
            "서적 판본은 생성 이름·효과 profile을 노출");
        TestAssert.True(snapshot.Map.Locations
            .Where(value => value.Biome != BiomeType.City)
            .All(value => value.Monsters.Count == configuration.MonsterSpeciesPerBiome),
            "지도 서식지는 설정된 요수 종족 수를 노출");
        TestAssert.Equal(WorldGeographyRules.PointsPerProvince,
            snapshot.Map.Geography!.FirstHeavenPoints.Count,
            "무작위 제1층천 10×10 지점 유지");
        TestAssert.Equal(WorldGeographyRules.PointsPerProvince,
            snapshot.Map.Geography.SecondHeavenPoints.Count,
            "무작위 제2층천 10×10 지점 유지");
    }

    private static void OngoingGenerationAndCheckpointRoundTripAreDeterministic()
    {
        var configuration = CreateCompactConfiguration();
        var world = World.Create(9901, 16, WorldBalanceConfiguration.Default, configuration);
        var before = world.CreateCheckpoint();
        world.AdvanceTo(new SimulationTick(checked(2L * GameCalendar.TicksPerYear)));
        var after = world.CreateCheckpoint();
        TestAssert.Equal(
            before.Generation!.Items.Length + (2 * configuration.ItemDefinitionsPerInterval),
            after.Generation!.Items.Length,
            "2년간 지속 생성 아이템 Definition");
        TestAssert.Equal(
            before.Generation.Books.Length + (2 * configuration.BookContentsPerInterval),
            after.Generation.Books.Length,
            "2년간 지속 생성 서적 Content");
        TestAssert.Equal(
            configuration.InitialItemInstanceCount
                + (2 * configuration.ItemDefinitionsPerInterval),
            after.Content!.Instances.Count(value => value.Kind == ContentInstanceKind.Item),
            "새 아이템 Definition마다 실물 생성");
        TestAssert.Equal(
            configuration.InitialBookContentCount
                + (2 * configuration.BookContentsPerInterval),
            after.Knowledge!.Contents.Length,
            "새 서적 내용 권위 등록");
        TestAssert.Equal(24L, after.Generation.LastContentGenerationMonth,
            "지속 생성 cursor 월");

        var restored = World.Restore(after);
        TestAssert.Equal(world.ComputeStateHash(), restored.ComputeStateHash(),
            "schema 30 생성 카탈로그 저장 왕복 hash");
        TestAssert.True(
            after.Generation.Items.SequenceEqual(
                restored.CreateCheckpoint().Generation!.Items),
            "지속 생성 아이템 카탈로그 저장 왕복");
        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(after with
            {
                Generation = after.Generation with { NextItemCatalogOrdinal = 1 },
            }),
            "중복 생성 ID를 만들 수 있는 item cursor 손상 거부");
        TestAssert.Throws<InvalidDataException>(
            () => World.Restore(after with
            {
                Generation = after.Generation with
                {
                    LastContentGenerationMonth = 25,
                },
            }),
            "현재 세계 시간보다 앞선 지속 생성 cursor 거부");

        // schema 29에는 생성 설정이 없었으므로 당시 고정 3종/지형 지도 배열과
        // 동일한 기본 설정으로 만든 체크포인트를 이전 입력으로 사용한다.
        var legacySource = World.Create(
            9902,
            16,
            WorldBalanceConfiguration.Default,
            WorldGenerationConfiguration.Default).CreateCheckpoint();
        var legacyContent = legacySource.Content! with
        {
            Instances = legacySource.Content.Instances
                .Select(value => value with { GeneratedElementId = 0 })
                .ToArray(),
        };
        var legacy29 = legacySource with
        {
            SchemaVersion = 29,
            Generation = null,
            Content = legacyContent,
        };
        var migrated = World.Restore(legacy29);
        TestAssert.Equal(legacySource.Seed, migrated.Seed,
            "schema 29 생성 기본값 이전 seed");
        TestAssert.Equal(WorldGenerationConfiguration.Default,
            migrated.Generation,
            "schema 29는 기본 생성 설정으로 이전");
        var migratedContent = migrated.CreateSnapshot(
            1,
            0,
            ObservationSections.Content,
            contentItemOffset: ContentPopulationRules.ApprovedMaterialStackCount,
            contentItemLimit: 20)
            .Content!;
        TestAssert.True(
            migratedContent.ItemsAndMaterials
                .Where(value => value.Kind == ContentInstanceKind.Item)
                .All(value => value.GeneratedDefinition is { IsFixedUnique: false }),
            "구 저장의 ID 없는 일반 실물이 고정 유니크로 중복 표시되지 않음");
    }

    private static WorldGenerationConfiguration CreateCompactConfiguration() => new(
        RandomizeMap: true,
        RandomizeMonsters: true,
        RandomizeItems: true,
        RandomizeBooks: true,
        IncludeFixedUniqueItems: true,
        InitialRandomItemDefinitionCount: 8,
        InitialItemInstanceCount: 24,
        InitialBookContentCount: 6,
        InitialBookEditionCount: 8,
        InitialPhysicalBookCount: 12,
        MonsterSpeciesPerBiome: 2,
        InitialMonsterPopulationPerHabitat: 77,
        EnableOngoingContentGeneration: true,
        ContentGenerationIntervalYears: 1,
        ItemDefinitionsPerInterval: 2,
        BookContentsPerInterval: 1);
}
