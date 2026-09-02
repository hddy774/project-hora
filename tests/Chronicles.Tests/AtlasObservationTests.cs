using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Queries;
using Chronicles.Simulation;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using static TestAssert;

internal static class AtlasObservationTests
{
    public static void WorldMapAndBiomeMonsters()
    {
        Equal(18, WorldMapRules.AllLocations.Count, "1·2층천 3×3 호환 구역 위치 수");
        Equal(9, WorldMapRules.AllMonsters.Count, "지형별 요수 수");
        Equal(2, WorldMapRules.AllLocations.Count(item => item.Biome == BiomeType.City), "층천별 중앙 도시 수");
        foreach (var location in WorldMapRules.AllLocations.Where(item => item.Biome != BiomeType.City))
        {
            var monsters = WorldMapRules.GetMonsters(location.Biome);
            Equal(3, monsters.Count, $"{location.KoreanName} 요수 종 수");
            True(monsters.All(monster => monster.Biome == location.Biome), "요수와 서식 지형 일치");
        }

        var newbornWorld = World.Create(9008, 100);
        var newbornMap = newbornWorld.CreateSnapshot(
            1,
            0,
            ObservationSections.Map,
            WorldMapRules.CityLocationId).Map;
        var newbornCity = newbornMap.Locations[WorldMapRules.CityLocationId];
        True(!newbornCity.HasCultivator, "0세 지도 최고 경지는 미입문");
        True(
            newbornCity.Agents.All(agent => !agent.HasEnteredCultivation),
            "0세 지도 인물은 연기경으로 오표시하지 않음");

        var world = World.Create(9009, 1_000);
        world.AdvanceTo(new SimulationTick(20_000));
        var occupiedLocation = world.GetAgentSnapshot(world.LeaderId).LocationId;
        var snapshot = world.CreateSnapshot(25, 25, ObservationSections.All, occupiedLocation);
        Equal(world.Population, snapshot.Map.Locations.Sum(location => location.Population), "지도 인구 보존");
        True(snapshot.Map.Locations.Count(location => location.Population > 0) > 1, "AI 지도 이동");
        True(world.TotalMonsterHunts > 0, "요수 사냥 누계");
        True(world.TotalMonsterVictories > 0, "요수 사냥 승리 누계");
        True(world.TotalMonsterVictories <= world.TotalMonsterHunts, "요수 사냥 집계 보존");
        var detailedLocation = snapshot.Map.Locations[occupiedLocation];
        True(detailedLocation.Agents.Count > 0, "선택 지도 칸 수선자 상세 조회");
        True(
            detailedLocation.Agents.Count <= WorldMapRules.DetailedAgentPageSize,
            "선택 지역 수선자 페이지 상한");
        Equal(detailedLocation.Population, detailedLocation.Activities.Total, "지역 행동 분포 인구 보존");
        True(detailedLocation.StrongestAgent is not null, "지역 최강 수선자 요약");
        True(
            detailedLocation.AverageCombatPower <= detailedLocation.MaximumCombatPower,
            "지역 평균 전투력은 최고 전투력을 넘을 수 없음");
        if (detailedLocation.Population > WorldMapRules.DetailedAgentPageSize)
        {
            var nextPage = world.CreateSnapshot(
                25,
                25,
                ObservationSections.Map,
                occupiedLocation,
                WorldMapRules.DetailedAgentPageSize);
            var nextLocation = nextPage.Map.Locations[occupiedLocation];
            Equal(
                WorldMapRules.DetailedAgentPageSize,
                nextLocation.AgentPageOffset,
                "지역 수선자 다음 페이지 offset");
            True(
                detailedLocation.Agents.Select(item => item.AgentId)
                    .Intersect(nextLocation.Agents.Select(item => item.AgentId))
                    .Any() == false,
                "지역 수선자 페이지 중복 방지");
        }
        True(
            snapshot.Map.Locations
                .Where(location => location.Biome != BiomeType.City)
                .SelectMany(location => location.Monsters)
                .All(monster => monster.Population is >= 0 and <= WorldMapRules.MaximumMonsterPopulation),
            "요수 개체 수 범위");
        True(
            Enumerable.Range(0, world.Population)
                .Select(index => world.GetAgentSnapshot(new AgentId(index)))
                .Any(agent => agent.MonsterVictories > 0 && agent.InventoryItemCount > 0),
            "지형 요수 전리품 획득");
    }

    public static void HacheonOcheonGeographyBlueprint()
    {
        var blueprint = WorldGeographyRules.Blueprint;
        Equal("하천대륙", blueprint.Continent.KoreanName, "현재 대륙 이름");
        Equal(10, blueprint.Continent.WidthInProvinceTiles, "대륙 주 가로 타일");
        Equal(10, blueprint.Continent.HeightInProvinceTiles, "대륙 주 세로 타일");
        Equal(100, blueprint.Provinces.Count, "대륙 아래 10×10 주");
        var ocheon = blueprint.Provinces[WorldGeographyRules.OcheonProvinceId];
        Equal("오천주", ocheon.KoreanName, "현재 상세 구현 주");
        True(ocheon.HasPointDetail, "오천주 지점 상세 구현");
        Equal(1, blueprint.Provinces.Count(province => province.HasPointDetail), "상세 구현 주 한 곳");
        Equal(100, blueprint.FirstHeavenPoints.Count, "1층천 오천주 10×10 지점");
        Equal(100, blueprint.SecondHeavenPoints.Count, "2층천 오천주 10×10 지점");

        for (ushort pointId = 0; pointId < WorldGeographyRules.PointsPerProvince; pointId++)
        {
            var first = blueprint.FirstHeavenPoints[pointId];
            var second = blueprint.SecondHeavenPoints[pointId];
            Equal(first.PhysicalPointId, second.PhysicalPointId, "두 층천 물리 좌표 대응");
            Equal(first.X, second.X, "두 층천 X 좌표 대응");
            Equal(first.Y, second.Y, "두 층천 Y 좌표 대응");
            True(first.NodeId != second.NodeId, "층천별 안정 위치 ID 분리");
            True(first.Description != second.Description, "층천별 묘사 분리");
            True(first.Settlements.LargeCityCount <= 1, "대도시 최대 하나");
            True(first.Settlements.SmallCityCount <= 2, "소도시 최대 둘");
            True(first.Settlements.VillageCount <= 4, "촌 최대 넷");
            Equal(0, first.Settlements.SectCount, "1층천 종문 없음");
            Equal(0, first.Settlements.ClanCount, "1층천 수선 가문 없음");
            True(second.Settlements.Society != PointSocietyKind.Mortal, "2층천 범인 정착지 없음");
        }

        var firstSettlements = blueprint.FirstHeavenPoints.Count(point =>
            point.Settlements.HasSettlement);
        var secondSettlements = blueprint.SecondHeavenPoints.Count(point =>
            point.Settlements.HasSettlement);
        True(firstSettlements > secondSettlements, "상위 층천 정착지 밀도 감소");
        True(blueprint.FirstHeavenPoints.Any(point => !point.Settlements.HasSettlement), "자연 전용 지점 존재");
        True(blueprint.FirstHeavenPoints.Any(point =>
            point.Settlements.HasSettlement && point.Resources.EstimatedBeastPopulation > 0), "도시 주변 영수 존재");
        True(blueprint.FirstHeavenPoints.Any(point => point.Resources.HasSpiritVein), "자연 영맥 존재");
        True(blueprint.FirstHeavenPoints.Any(point =>
            point.Resources.Mine != MineResourceKind.None), "광산 존재");
        AssertNoAdjacentStrongholds(blueprint.FirstHeavenPoints);
        AssertNoAdjacentStrongholds(blueprint.SecondHeavenPoints);
    }

    public static void AtlasHierarchySpatialKnowledgeAndVeins()
    {
        Equal(5, WorldAtlasRules.AllHeavenlyLayers.Count, "5층천 Definition");
        Equal(
            2,
            WorldAtlasRules.AllHeavenlyLayers.Count(item =>
                item.ImplementationState == AtlasImplementationState.Production),
            "Production 1·2층천");
        Equal(18, WorldAtlasRules.ProductionLocationCount, "실구현 지점 수");
        Equal(900, WorldAtlasRules.AllSpiritVeins.Count, "영맥 Source 900개");
        Equal(
            50,
            WorldAtlasRules.AllSpiritVeins.Count(item => item.LocationId == 0),
            "지점별 영맥 Source 분배");

        foreach (var node in WorldAtlasRules.AllNodes)
        {
            if (node.ParentId is { } parent)
            {
                True(
                    WorldAtlasRules.AllNodes.Any(candidate => candidate.Id == parent),
                    $"Atlas 부모 존재: {node.Id}");
            }
        }

        var spatial = new SpatialAuthorityStore(2);
        var qiDenied = spatial.PlanRoute(
            0,
            WorldMapRules.CityLocationId,
            WorldMapRules.SecondHeavenCityLocationId,
            RealmStage.QiRefining,
            startedTick: 0,
            dueTick: GameCalendar.TicksPerYear);
        True(qiDenied == MovementResolutionCode.AccessDenied, "층천 접근 조건");
        var foundationPlan = spatial.PlanRoute(
            0,
            WorldMapRules.CityLocationId,
            WorldMapRules.SecondHeavenCityLocationId,
            RealmStage.FoundationEstablishment,
            startedTick: 0,
            dueTick: GameCalendar.TicksPerYear);
        True(foundationPlan == MovementResolutionCode.Planned, "축기경 2층천 경로 계획");
        var arrival = spatial.CompleteRoute(
            0,
            WorldMapRules.CityLocationId,
            RealmStage.FoundationEstablishment,
            GameCalendar.TicksPerYear,
            travelerAlive: true,
            out var destination);
        True(arrival == MovementResolutionCode.Arrived, "도착 Typed Result");
        Equal(WorldMapRules.SecondHeavenCityLocationId, destination, "도착 지점");
        spatial.RecordArrival(0, WorldMapRules.CityLocationId, destination, currentMonth: 12);
        True(spatial.IsKnown(0, destination), "도착 발견 지식");
        True(
            spatial.GetKnowledgeSnapshot(0, destination).KnownLocationCount > 1,
            "개인별 공간 지식 범위");

        var beforeSupply = spatial.GetRemainingSpiritStoneSupply(destination);
        True(spatial.TryExtractSpiritStones(destination, 123, out var extracted), "영맥 채굴");
        Equal(123L, extracted, "영맥 추출량");
        Equal(
            beforeSupply - extracted,
            spatial.GetRemainingSpiritStoneSupply(destination),
            "유한 영맥 공급 보존");

        var checkpoint = spatial.CreateCheckpoint();
        var locations = new ushort[] { destination, WorldMapRules.CityLocationId };
        var restored = SpatialAuthorityStore.Restore(checkpoint, locations);
        Equal(
            spatial.GetRemainingSpiritStoneSupply(destination),
            restored.GetRemainingSpiritStoneSupply(destination),
            "영맥 공급 저장 복원");
        Equal(
            spatial.GetRoute(0),
            restored.GetRoute(0),
            "이동 상태 저장 복원");

        var world = World.Create(8_808, 300);
        var initial = world.CreateSnapshot(
            10,
            10,
            ObservationSections.Map,
            WorldMapRules.CityLocationId).Map;
        Equal(5, initial.Atlas!.Layers.Count, "지도 query 5층천");
        Equal(900, initial.Atlas.SpiritVeinSourceCount, "지도 query 영맥 수");
        Equal(300, initial.Atlas.Layers[0].Population, "기존 3×3 호환 구역 시작 인구");
        Equal(0, initial.Atlas.Layers[1].Population, "2층천 초기 미진입");

        var hashBeforeObservation = world.ComputeStateHash();
        _ = world.CreateSnapshot(10, 10, ObservationSections.Map);
        Equal(hashBeforeObservation, world.ComputeStateHash(), "Atlas 관찰 무영향");

        world.AdvanceTo(new SimulationTick(200L * GameCalendar.TicksPerYear));
        var spread = world.CreateSnapshot(
            10,
            10,
            ObservationSections.Map,
            WorldMapRules.SecondHeavenCityLocationId).Map;
        True(
            spread.Atlas!.Layers[1].Population > 0,
            "축기 이상 AI가 개인 공간 지식과 접근 조건으로 2층천에 확산");

        var worldCheckpoint = world.CreateCheckpoint();
        True(worldCheckpoint.Atlas is not null, "Atlas 권위 저장");
        var restoredWorld = World.Restore(worldCheckpoint);
        Equal(world.ComputeStateHash(), restoredWorld.ComputeStateHash(), "Atlas 세계 결정론 복원");
    }

    public static void DeveloperWorldSettingsAndBalancePersistence()
    {
        var configured = new WorldBalanceConfiguration(
            DeveloperModeEnabled: true,
            CultivationWorkPercent: 200,
            CombatAggressionPercent: 125,
            EconomyActivityPercent: 75,
            MonsterDangerPercent: 150);
        WorldBalanceRules.Validate(configured);
        Throws<ArgumentOutOfRangeException>(
            () => WorldBalanceRules.Validate(configured with { MonsterDangerPercent = 401 }),
            "개발자 배율 상한 검증");

        var world = World.Create(20260901, 321, configured);
        Equal(321, world.Population, "설정 AI 수");
        Equal(configured, world.Balance, "세계 개발자 밸런스 권위");
        world.AdvanceTo(new SimulationTick(8_000));
        var checkpoint = world.CreateCheckpoint();
        True(checkpoint.Balance == configured, "checkpoint 개발자 밸런스");
        var restored = World.Restore(checkpoint);
        Equal(configured, restored.Balance, "복원 개발자 밸런스");
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "개발자 세계 저장 hash");

        var defaultWorld = World.Create(20260902, 1);
        var fasterWorld = World.Create(
            20260902,
            1,
            new WorldBalanceConfiguration(
                DeveloperModeEnabled: true,
                CultivationWorkPercent: 200));
        var firstCultivationMonth = GameCalendar.GetMonthStart(
            (CultivationRules.EntryAgeYears * GameCalendar.MonthsPerYear) + 1);
        defaultWorld.AdvanceTo(firstCultivationMonth);
        fasterWorld.AdvanceTo(firstCultivationMonth);
        var baseline = defaultWorld.GetAgentSnapshot(new AgentId(0));
        var faster = fasterWorld.GetAgentSnapshot(new AgentId(0));
        True(baseline.Cultivation.LastGain > 0, "기준 수행 Work 발생");
        Equal(
            WorldBalanceRules.ApplyPercent(baseline.Cultivation.LastGain, 200),
            faster.Cultivation.LastGain,
            "수행 Work 개발자 배율 실제 적용");
    }

    public static void ObservationQueriesAreReadOnly()
    {
        var world = World.CreateDefault(991);
        world.AdvanceTo(new SimulationTick(300));
        var before = world.ComputeStateHash();
        var leader = world.GetAgentSnapshot(world.LeaderId);
        var exactSearch = world.SearchAgents(leader.Id.ToString());
        var nameSearch = world.SearchAgents(leader.Name[..1], 25);
        var firstPage = world.BrowseAgents(0, 500);
        var finalPage = world.BrowseAgents(9_500, 500);
        var personalLog = world.GetAgentPersonalLog(leader.Id);
        var chronicle = world.GetAgentChronicle(leader.Id);
        var snapshot = world.CreateSnapshot(150, 150);
        var closedWorkspace = world.CreateSnapshot(25, 25, ObservationSections.None);
        var statisticsOnly = world.CreateSnapshot(25, 25, ObservationSections.Statistics);
        var summaryOnly = world.CreateSnapshot(25, 25, ObservationSections.StatisticsSummary);
        var realmDistributionOnly = world.CreateSnapshot(25, 25, ObservationSections.RealmDistribution);
        var cityOnly = world.CreateSnapshot(25, 25, ObservationSections.MarketSummary);
        var organizationOnly = world.CreateSnapshot(25, 25, ObservationSections.Organization);

        Equal(1, exactSearch.Count, "정확한 Agent ID 검색 결과");
        Equal(leader.Id, exactSearch[0].Id, "정확한 Agent ID 검색 대상");
        True(nameSearch.Count > 0, "이름 검색 결과가 있어야 합니다.");
        Equal(new AgentId(0), firstPage[0].Id, "전체 탐색 첫 ID");
        Equal(new AgentId(9_999), finalPage[^1].Id, "전체 탐색 마지막 ID");
        Equal(150, snapshot.CombatPowerRankings.Count, "전투력 랭킹 조회 크기");
        Equal(150, snapshot.RealmRankings.Count, "경지 랭킹 조회 크기");
        Equal(150, snapshot.AptitudeRankings.Count, "자질 랭킹 조회 크기");
        Equal(0, closedWorkspace.CombatPowerRankings.Count, "닫힌 화면 전투력 랭킹 DTO 생략");
        Equal(0, closedWorkspace.RealmRankings.Count, "닫힌 화면 경지 랭킹 DTO 생략");
        Equal(0, closedWorkspace.AptitudeRankings.Count, "닫힌 화면 자질 랭킹 DTO 생략");
        Equal(0, closedWorkspace.WealthRankings.Count, "닫힌 화면 부자 랭킹 DTO 생략");
        Equal(0, closedWorkspace.Statistics.RealmDistribution.Count, "비활성 통계 조회 생략");
        Equal(0, closedWorkspace.Market.Goods.Count, "비활성 시장 조회 생략");
        Equal(0, closedWorkspace.Map.Locations.Count, "비활성 지도 조회 생략");
        Equal(0, closedWorkspace.RecentEvents.Count, "비활성 세계 로그 조회 생략");
        Equal(0, closedWorkspace.PermanentWorldChronicle.Count, "비활성 세계 연대기 조회 생략");
        True(
            ReferenceEquals(WorldOrganizationSnapshot.Empty, closedWorkspace.Organizations),
            "닫힌 조직 화면 DTO 생성 생략");
        True(
            !ReferenceEquals(WorldOrganizationSnapshot.Empty, organizationOnly.Organizations),
            "열린 조직 화면만 조직 DTO 투영");
        True(statisticsOnly.Statistics.RealmDistribution.Count > 0, "활성 통계만 계산");
        Equal(0, statisticsOnly.Market.Goods.Count, "통계 관찰 중 시장 계산 생략");
        Equal(0, summaryOnly.Statistics.RealmDistribution.Count, "접힌 경지 분포 계산 생략");
        Equal(0, summaryOnly.Statistics.CombatPowerDistribution.Count, "접힌 전투력 분포 계산 생략");
        True(realmDistributionOnly.Statistics.RealmDistribution.Count > 0, "펼친 경지 분포만 계산");
        Equal(0, realmDistributionOnly.Statistics.CombatPowerDistribution.Count, "접힌 전투력 분포는 미계산");
        Equal(0, cityOnly.Market.Goods.Count, "도시 내부 탭에서 시장 상품 목록 계산 생략");
        True(cityOnly.Market.TotalStock > 0, "도시 요약 재고는 상품 DTO 없이 집계");
        Equal(
            cityOnly.Market.TotalStock,
            cityOnly.Market.MaterialStock + cityOnly.Market.ProcessedStock,
            "도시 요약 재고 분류 보존");
        Equal(
            EconomyRules.MaximumConcurrentProduction,
            cityOnly.Market.ProductionCapacity,
            "도시 생산 동시 처리 용량 계약");
        True(personalLog.Count <= World.PersonalLogCapacity, "개인 로그 조회 크기");
        True(chronicle.Count > 0, "영구 연대기 조회");
        Equal(before, world.ComputeStateHash(), "관찰 조회는 세계 상태를 바꾸면 안 됩니다.");

        using var runner = new SimulationRunner(world);
        var personalOnly = runner.GetObservation(
            leader.Id,
            rankingCount: 25,
            eventCount: 25,
            sections: ObservationSections.PersonalLog);
        True(personalOnly.SelectedPersonalLog.Count > 0, "활성 개인 로그 조회");
        Equal(0, personalOnly.SelectedChronicle.Count, "비활성 연대기 조회 생략");
    }

    public static void SelectiveObservationPerformance()
    {
        const int iterations = 80;
        const double summaryQueryP95BudgetMilliseconds = 16.0;
        const double thousandRowMaterializationP95BudgetMilliseconds = 50.0;
        var world = World.CreateDefault(992);
        world.AdvanceTo(new SimulationTick(20_000));
        _ = world.CreateSnapshot(1_000, 150, ObservationSections.All);
        _ = world.CreateSnapshot(
            1_000,
            150,
            ObservationSections.StatisticsSummary
                | ObservationSections.RealmDistribution
                | ObservationSections.CombatPowerRanking);

        WorldSnapshot? sink = null;
        var legacy = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            // 이전 UI 경로: worker의 전체 snapshot 뒤 UI가 전체 observation을 다시 요청했다.
            sink = world.CreateSnapshot(1_000, 150, ObservationSections.All);
            sink = world.CreateSnapshot(1_000, 150, ObservationSections.All);
        }
        legacy.Stop();

        var selectiveSamples = new double[iterations];
        var selective = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            // 현재 통계 탭: 단일 snapshot에서 화면에 펼친 경지 분포만 계산한다.
            var startedAt = Stopwatch.GetTimestamp();
            sink = world.CreateSnapshot(
                1_000,
                150,
                ObservationSections.StatisticsSummary
                    | ObservationSections.RealmDistribution
                    | ObservationSections.CombatPowerRanking);
            selectiveSamples[iteration] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        }
        selective.Stop();

        var listSamples = new double[iterations];
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var startedAt = Stopwatch.GetTimestamp();
            sink = world.CreateSnapshot(
                1_000,
                0,
                ObservationSections.CombatPowerRanking);
            listSamples[iteration] = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            Equal(1_000, sink.CombatPowerRankings.Count, "1,000행 랭킹 materialization 크기");
        }
        GC.KeepAlive(sink);

        var reduction = legacy.ElapsedTicks == 0
            ? 0m
            : (legacy.ElapsedTicks - selective.ElapsedTicks) * 100m / legacy.ElapsedTicks;
        var summaryP95 = GetP95(selectiveSamples);
        var listP95 = GetP95(listSamples);
        Console.WriteLine(
            $"UI PERF: 이전 이중 전체 조회 {legacy.Elapsed.TotalMilliseconds:N1} ms · 선택 조회 {selective.Elapsed.TotalMilliseconds:N1} ms · 감소 {reduction:0.0}% ({iterations:N0}회) · " +
            $"요약 query p95 {summaryP95:0.000} ms · 1,000행 materialization p95 {listP95:0.000} ms");
        True(selective.ElapsedTicks < legacy.ElapsedTicks, "활성 UI 선택 조회는 이전 이중 전체 조회보다 빨라야 합니다.");
        True(summaryP95 <= summaryQueryP95BudgetMilliseconds, "요약 화면 query p95 16 ms 예산");
        True(listP95 <= thousandRowMaterializationP95BudgetMilliseconds, "1,000행 materialization p95 50 ms 예산");
    }

    public static void NonPersonWorkspaceSkipsAgentProjection()
    {
        const int iterations = 200;
        var world = World.CreateDefault(994);
        var queries = new WorldObservationQueries(world);
        var request = new ObservationRequest(
            RankingCount: 1,
            EventCount: 0,
            Sections: ObservationSections.StatisticsSummary,
            WorldChronicleCount: 0,
            ContentSecretRealmLimit: 0,
            KnowledgeEditionLimit: 0,
            LawCandidateLimit: 0);
        _ = queries.GetSnapshot(request);
        _ = queries.GetObservation(request with { SelectedAgent = new AgentId(0) });

        var hashBefore = world.ComputeStateHash();
        var worldAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        WorldSnapshot? worldSink = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            worldSink = queries.GetSnapshot(request);
        }
        var worldAllocated = GC.GetAllocatedBytesForCurrentThread() - worldAllocatedBefore;

        var personAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        WorldObservation? personSink = null;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            personSink = queries.GetObservation(request with { SelectedAgent = new AgentId(0) });
        }
        var personAllocated = GC.GetAllocatedBytesForCurrentThread() - personAllocatedBefore;
        GC.KeepAlive(worldSink);
        GC.KeepAlive(personSink);

        var reduction = personAllocated == 0
            ? 0m
            : (personAllocated - worldAllocated) * 100m / personAllocated;
        Console.WriteLine(
            $"WORLD-ONLY UI PERF: world {worldAllocated:N0} B · person {personAllocated:N0} B · " +
            $"숨은 인물 투영 제거 {reduction:0.0}% ({iterations:N0}회)");
        True(worldAllocated < personAllocated, "비인물 화면은 선택 인물 DTO보다 적게 할당해야 합니다.");
        Equal(hashBefore, world.ComputeStateHash(), "world-only 관찰은 권위 상태를 바꾸지 않음");
    }

    public static void LazyMapAgentProjectionPerformance()
    {
        const int iterations = 200;
        var world = World.CreateDefault(993);
        _ = world.CreateSnapshot(25, 0, ObservationSections.Map);
        _ = world.CreateSnapshot(
            25,
            0,
            ObservationSections.Map,
            WorldMapRules.CityLocationId);

        WorldSnapshot? sink = null;
        var overviewAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var overview = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            sink = world.CreateSnapshot(25, 0, ObservationSections.Map);
        }
        overview.Stop();
        var overviewAllocated = GC.GetAllocatedBytesForCurrentThread() - overviewAllocatedBefore;
        var overviewRows = sink!.Map.Locations.Sum(location => location.Agents.Count);

        var agentsAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var agents = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            sink = world.CreateSnapshot(
                25,
                0,
                ObservationSections.Map,
                WorldMapRules.CityLocationId);
        }
        agents.Stop();
        var agentsAllocated = GC.GetAllocatedBytesForCurrentThread() - agentsAllocatedBefore;
        var agentRows = sink!.Map.Locations.Sum(location => location.Agents.Count);
        GC.KeepAlive(sink);

        var allocationReduction = agentsAllocated == 0
            ? 0m
            : (agentsAllocated - overviewAllocated) * 100m / agentsAllocated;
        Console.WriteLine(
            $"MAP UI PERF: 개요 {overview.Elapsed.TotalMilliseconds:N1} ms / {overviewAllocated:N0} B · " +
            $"수선자 탭 {agents.Elapsed.TotalMilliseconds:N1} ms / {agentsAllocated:N0} B · " +
            $"닫힌 탭 할당 감소 {allocationReduction:0.0}% ({iterations:N0}회)");
        Equal(0, overviewRows, "지도 개요에서는 수선자 행을 투영하지 않음");
        Equal(WorldMapRules.DetailedAgentPageSize, agentRows, "수선자 탭은 한 페이지만 투영");
        True(overviewAllocated < agentsAllocated, "닫힌 수선자 탭은 열린 탭보다 적게 할당해야 합니다.");
    }

    static void AssertNoAdjacentStrongholds(
        IReadOnlyList<PointHeavenViewDefinition> points)
    {
        var strongholds = points.Where(point => point.Settlements.HasMajorStronghold).ToArray();
        for (var left = 0; left < strongholds.Length; left++)
        {
            for (var right = left + 1; right < strongholds.Length; right++)
            {
                True(
                    !WorldGeographyRules.AreAdjacentIncludingDiagonal(
                        strongholds[left],
                        strongholds[right]),
                    "도시·종문·가문 주변 한 칸 이격");
            }
        }
    }

    static double GetP95(double[] samples)
    {
        Array.Sort(samples);
        var index = Math.Min(samples.Length - 1, (int)Math.Ceiling(samples.Length * 0.95) - 1);
        return samples[index];
    }
}
