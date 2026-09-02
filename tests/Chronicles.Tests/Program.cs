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

if (args.Contains("--verify-local-save", StringComparer.Ordinal))
{
    var load = SlotSaveStore.Load(SlotSaveStore.DefaultRoot, SlotSaveStore.DefaultSlotId);
    if (!load.IsSuccess)
    {
        Console.Error.WriteLine($"LOCAL SAVE ERROR: {load.Status} · {load.Message}");
        return 1;
    }

    var checkpoint = load.Checkpoint!;
    var restored = World.Restore(checkpoint);
    var snapshot = restored.CreateSnapshot(10, 10);
    Console.WriteLine(
        $"LOCAL SAVE OK: world schema {load.WorldSchema} · checkpoint schema {checkpoint.SchemaVersion} · " +
        $"{snapshot.Population:N0}명 · {GameCalendar.GetDate(snapshot.Tick)} · " +
        $"시장 {snapshot.Market.Goods.Count:N0}종");
    return 0;
}

if (args.Contains("--verify-large-local-save", StringComparer.Ordinal))
{
    var source = SlotSaveStore.Load(SlotSaveStore.DefaultRoot, SlotSaveStore.DefaultSlotId);
    if (!source.IsSuccess)
    {
        Console.Error.WriteLine($"LARGE SAVE SOURCE ERROR: {source.Status} · {source.Message}");
        return 1;
    }

    var checkpoint = source.Checkpoint!;
    var advanceRaw = Environment.GetEnvironmentVariable("CMIP_LARGE_SAVE_ADVANCE_YEARS");
    var advanceYears = 0;
    if (!string.IsNullOrWhiteSpace(advanceRaw)
        && (!int.TryParse(advanceRaw, out advanceYears) || advanceYears is < 0 or > 10_000))
    {
        Console.Error.WriteLine("CMIP_LARGE_SAVE_ADVANCE_YEARS는 0~10000 정수여야 합니다.");
        return 2;
    }
    if (advanceYears > 0)
    {
        var expanded = World.Restore(checkpoint);
        expanded.AdvanceTo(new SimulationTick(checked(
            expanded.Tick.Value + GameCalendar.FromYears(advanceYears).Value)));
        checkpoint = expanded.CreateCheckpoint();
    }

    var probeRoot = Path.Combine(Path.GetTempPath(), $"cmip-large-save-{Guid.NewGuid():N}");
    try
    {
        var saveTimer = Stopwatch.StartNew();
        var saved = SlotSaveStore.Save(probeRoot, "probe", checkpoint);
        saveTimer.Stop();
        var loadTimer = Stopwatch.StartNew();
        var loaded = SlotSaveStore.Load(probeRoot, "probe");
        loadTimer.Stop();
        if (!loaded.IsSuccess || loaded.StateHash != saved.StateHash)
        {
            Console.Error.WriteLine(
                $"LARGE SAVE ROUNDTRIP ERROR: {loaded.Status} · {loaded.Message}");
            return 1;
        }

        Console.WriteLine(
            $"LARGE SAVE OK: advance {advanceYears:N0}년 · JSON {saved.JsonBytes:N0} bytes · container " +
            $"{saved.PayloadBytes:N0} bytes · save/load " +
            $"{saveTimer.Elapsed.TotalSeconds:N2}/{loadTimer.Elapsed.TotalSeconds:N2}s · " +
            $"hash {saved.StateHash:X16}");
        return 0;
    }
    finally
    {
        if (Directory.Exists(probeRoot))
        {
            Directory.Delete(probeRoot, recursive: true);
        }
    }
}

var tests = new (string Name, Action Body)[]
{
    ("기본 인구와 목표 순위", DefaultPopulationAndGoal),
    ("공식 Registry·typed command·원자 transaction·causal event", AuthorityFoundationTests.Run),
    ("게임 시간·배속·경지 수명", GameTimeAndRealmRules),
    ("자질 50.00 대경지 1층→9층 대원만 전체 기준", CultivationAuthorityRules),
    ("대경지별 5단계 세부 수행 진척·지연 관찰", CultivationDetailProgressProjection),
    ("승인 Effect 0개·Typed 실패·수행 외부 계수 계약", CultivationAttemptContractTests.Run),
    ("입문 Gate 병목 단련·적용 Work·저장 왕복", EntryGateRecoveryAndAppliedWork),
    ("년월일 달력과 월 1회 행동 제한", CalendarAndMonthlyActionLimit),
    ("Notion 수행 Work·준비·Preflight·안착 상태", CultivationStateMachine),
    ("장기 수행 계획·조건·경량 pulse·저장 왕복", LongCultivationPlanConditionsAndPersistence),
    ("고경지 대형 체력 수행 조건 산술 안전성", HighRealmHealthRatioDoesNotOverflow),
    ("9층 소경지·대원만 분리와 수행 Formula Trace", FloorNineGreatPerfectionAndFormulaTrace),
    ("소경지·대경지 돌파 영구 연대기", CultivationBreakthroughChronicle),
    ("한국식 큰 수 단위 표기", KoreanLargeNumberFormatting),
    ("8단계 품질 경계와 격차 배율", QualityTiersAndBoundaryMultipliers),
    ("6대 자질 60개 Component·대표값·지연 조회·저장 이전", DetailedAptitudesAuthorityAndLazyProjection),
    ("공법·법술 경험·숙련·이해·품질 불변", SkillMasteryUnderstandingAndImmutableQuality),
    ("0세 탄생·6대 자질·일대기·윤회", BirthAptitudesChronicleAndReincarnation),
    ("영혼·육신·현생 신원과 AI 행동 이유 권위", PersonIdentityAndDecisionAuthority),
    ("Goal→Plan→Task→Action 인과·인지·생별 기억·외형 권위", AgentCausalLifeVerticalFlow),
    ("희소 방향 관계 6축·초기 Role·생애 경계", SparseDirectionalRelationsAndRoles),
    ("사제 성립·Teaching 결과·전승 provenance·직접 수행 0%", TeachingVerticalFlow),
    ("세계 중요 로그·개인 100줄 로그·영구 연대기", WorldPersonalLogsAndPermanentChronicle),
    ("개인 최근 로그 고정 링·100행 이후 무할당", AgentHistoryStoreTests.Run),
    ("개인 생별 100년 종합 연대기 투영", PersonalCenturyChronicleProjectionTest),
    ("전투력 구성 요소와 랭킹", CombatPowerAndRanking),
    ("랭킹 범주별 revision cache·무변경 정렬 0회", RankingRevisionCacheAvoidsUnchangedSorts),
    ("부자 TOP 1000·기간별 상위권 변동·세계 연대기", WealthRankingHistoryAndWorldChronicle),
    ("자질 직접 전투력 제거·공법 법술·실전 속성 간접 보정", AptitudeIndirectCombatBalance),
    ("공법·법술·법보 전투 스케일 상한과 품질 격차", SkillCombatScaleRegression),
    ("동일 seed 결정론", DeterministicWorld),
    ("AdvanceTo 호출 분할·runtime pulse·checkpoint 결정론", AdvancePartitionInvarianceTests.Run),
    ("5 seed 장기 수행 직렬·2단계 병렬 결정론", CultivationParallelDeterminismTests.Run),
    ("10,000명 수행 직렬·2단계 병렬 처리량", CultivationParallelThroughputTests.Run),
    ("이벤트 기반 성장", EventDrivenGrowth),
    ("180년 장기 수행·사냥·전투 참여 회귀", LongRunCultivationHuntingAndCombat),
    ("활발한 랭킹 경쟁과 전투 경험", ActiveRankingCompetition),
    ("도시 임무·영석·재료·단약·법보 경제 순환", EconomyMarketTests.EconomyLoop),
    ("재료 부족 상품이 다른 도시 생산을 막지 않음", EconomyMarketTests.ProductionSkipsGoodsWithoutResources),
    ("시장 원자 정산·중복 source·품질 개선 구매", EconomyMarketTests.MarketSettlementAtomicity),
    ("시장 Offer→Reservation→만료·원자 체결", EconomyMarketTests.MarketOfferReservationLifecycle),
    ("시장 소유자 용량 초과 원자 거부", EconomyMarketTests.MarketOwnerCapacityGuard),
    ("최근 12개월 수급 창·관찰 snapshot 재사용", EconomyMarketTests.MarketFlowWindowAndSnapshotCache),
    ("schema 24 시장 수급·원장 codec·source 재생 방지", EconomyMarketTests.MarketSchema24CodecAndSourceReplay),
    ("광맥 0 보존·손상 도시 생산 저장 거부", EconomyMarketTests.MarketCheckpointValidation),
    ("전체 분포·랭킹 변동·승패·사망 통계", WorldStatisticsQuery),
    ("관찰 조회 무영향", ObservationQueriesAreReadOnly),
    ("활성 UI 구역 선택 조회 성능", SelectiveObservationPerformance),
    ("지도 내부 탭 지연 투영 성능", LazyMapAgentProjectionPerformance),
    ("5층천 Atlas·1·2층천·900 영맥·개인 공간 지식", AtlasHierarchySpatialKnowledgeAndVeins),
    ("하천대륙 10×10 주·오천주 1·2층천 10×10 지점", HacheonOcheonGeographyBlueprint),
    ("설정용 AI 수·개발자 밸런스 권위·저장 왕복", DeveloperWorldSettingsAndBalancePersistence),
    ("관찰 UI 상태 분리·관심 목록 지연 조회", ObserverUiContractTests.Run),
    ("조직 Core·창설 Gate·중복 소속·승계·병합·schema 25→26", OrganizationCoreTests.Run),
    ("콘텐츠 Definition·Instance·Provenance·발견·원자 소유권", ContentAuthorityTests.Run),
    ("서적 Content·Edition·실물·접근·오해·교정 권위", KnowledgeAuthorityTests.Run),
    ("법칙 후보·개인 이해·세계 AI·천도 REVIEW 권위", LawAuthorityTests.Run),
    ("절차 생성 아이템·서적·지도·요수·지속 세계 카탈로그", WorldGenerationTests.Run),
    ("저장 codec 결정론 왕복", SaveCodecRoundTrip),
    ("손상 저장 거부", CorruptedSaveIsRejected),
    ("원자 저장 파일 왕복", AtomicSaveFileRoundTrip),
    ("schema 25→26 SQLite 다중 슬롯·후보 원자 복원", SqliteSlotPersistenceTests.Run),
    ("저장 실패 뒤 world·일시정지 보존과 다른 슬롯 재저장", SaveFailureRecoveryTests.Run),
    ("모든 로그 파일 5MiB 상한·기존 part 재검사·UTF-8 분할", BoundedLogFilesNeverExceedFiveMiB),
    ("로그 종류·자동 수동·인물별 출력 경로 계약", LogOutputDirectoryContract),
    ("10,000명 로컬 실시간 성능 예산", DefaultWorldPerformanceBudget),
    ("100배속 runtime pulse SOAK", RuntimeHundredSpeedSoakTests.Run),
};

tests =
[
    .. tests,
    ("교대 전투 속성과 누적 부상 사망", CombatAuthorityAndInjury),
    ("schema 7~9 진행 전투 단일 turn owner 복구", LegacyCombatTurnMigration),
    ("전투 숙련·이해 저장과 schema 12 기본값 이전", CombatProgressionSaveAndMigration),
    ("3×3 지도와 지형별 요수 생태", WorldMapAndBiomeMonsters),
    ("단계형 윤회·계보·6영역 흔적·schema 22 이전", StagedReincarnationLineageTraceAndMigration),
];

var filterIndex = Array.IndexOf(args, "--filter");
if (filterIndex >= 0)
{
    if (filterIndex + 1 >= args.Length
        || string.IsNullOrWhiteSpace(args[filterIndex + 1]))
    {
        Console.Error.WriteLine("TEST FILTER ERROR: --filter 뒤에 테스트 이름 일부를 입력해야 합니다.");
        Console.Error.WriteLine("사용법: Chronicles.Tests --filter \"저장 codec\"");
        return 2;
    }

    var filter = args[filterIndex + 1];
    var registeredTests = tests;
    tests = registeredTests.Where(test => test.Name.Contains(
        filter,
        StringComparison.CurrentCultureIgnoreCase)).ToArray();
    if (tests.Length == 0)
    {
        Console.Error.WriteLine($"TEST FILTER ERROR: 일치하는 테스트가 없습니다: {filter}");
        Console.Error.WriteLine("가까운 테스트 후보:");
        foreach (var suggestion in FindClosestTestNames(filter, registeredTests.Select(test => test.Name)))
        {
            Console.Error.WriteLine($"  - {suggestion}");
        }

        return 2;
    }
}

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Name}\n{exception}");
    }
}

Console.WriteLine($"RESULT: {tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static IEnumerable<string> FindClosestTestNames(
    string filter,
    IEnumerable<string> registeredNames)
{
    var normalizedFilter = filter.Trim();
    return registeredNames
        .Select(name => new
        {
            Name = name,
            Distance = EditDistance(normalizedFilter, name),
        })
        .OrderBy(candidate => candidate.Distance)
        .ThenBy(candidate => candidate.Name, StringComparer.CurrentCulture)
        .Take(8)
        .Select(candidate => candidate.Name);
}

static int EditDistance(string left, string right)
{
    var previous = new int[right.Length + 1];
    var current = new int[right.Length + 1];
    for (var rightIndex = 0; rightIndex <= right.Length; rightIndex++)
    {
        previous[rightIndex] = rightIndex;
    }

    for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
    {
        current[0] = leftIndex;
        for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
        {
            var substitutionCost = char.ToUpperInvariant(left[leftIndex - 1])
                == char.ToUpperInvariant(right[rightIndex - 1])
                    ? 0
                    : 1;
            current[rightIndex] = Math.Min(
                Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                previous[rightIndex - 1] + substitutionCost);
        }

        (previous, current) = (current, previous);
    }

    return previous[right.Length];
}

static void DefaultPopulationAndGoal()
{
    var world = World.CreateDefault(42);
    Equal(10_000, world.Population, "기본 인구");
    var initialSnapshot = world.CreateSnapshot(1, 0);
    True(!initialSnapshot.RealmRankings[0].HasEnteredCultivation, "0세 경지 랭킹은 미입문 표시");
    Equal(10_000, initialSnapshot.Statistics.RealmDistribution[0].Count, "0세 미입문 분포");
    Equal(0, initialSnapshot.Statistics.RealmDistribution[1].Count, "0세 연기경 분포");
    for (var index = 0; index < world.Population; index++)
    {
        Equal(1, world.GetAgentSnapshot(new AgentId(index)).TargetRank, "모든 AI의 목표 순위");
        Equal(0L, world.GetAgentSnapshot(new AgentId(index)).AgeYears, "모든 AI의 시작 나이");
    }
}

static void RankingRevisionCacheAvoidsUnchangedSorts()
{
    var scores = new[]
    {
        12L,
        long.MaxValue,
        12L,
        -4L,
        long.MinValue,
        999L,
        -4L,
    };
    var radixOrder = new int[scores.Length];
    var radixScratch = new int[scores.Length];
    RankingRadixSorter.SortDescending(radixOrder, scores, radixScratch);
    var comparisonOrder = Enumerable.Range(0, scores.Length)
        .OrderByDescending(agent => scores[agent])
        .ThenBy(agent => agent)
        .ToArray();
    EqualSequence(comparisonOrder, radixOrder, "64비트 랭킹 radix 순서·동점 AgentId 계약");

    const int population = 512;
    var world = World.Create(0xCACE_0014UL, population);
    var initial = world.GetRankingCacheDiagnostics();

    Equal(1L, initial.CombatPower.SortCount, "초기 전투력 정렬 횟수");
    Equal(1L, initial.Realm.SortCount, "초기 경지 정렬 횟수");
    Equal(1L, initial.Aptitude.SortCount, "초기 자질 정렬 횟수");
    Equal(1L, initial.Wealth.SortCount, "초기 부자 정렬 횟수");
    True(!initial.CombatPower.IsDirty, "초기 전투력 cache 동기화");
    True(!initial.Realm.IsDirty, "초기 경지 cache 동기화");
    True(!initial.Aptitude.IsDirty, "초기 자질 cache 동기화");
    True(!initial.Wealth.IsDirty, "초기 부자 cache 동기화");

    for (var pulse = 0; pulse < 100; pulse++)
    {
        world.AdvanceTo(world.Tick);
    }

    var afterIdlePulses = world.GetRankingCacheDiagnostics();
    Equal(initial.TotalSortCount, afterIdlePulses.TotalSortCount, "무변경 pulse 정렬 0회");
    Equal(initial.CombatPower.DataRevision, afterIdlePulses.CombatPower.DataRevision, "무변경 전투력 revision");
    Equal(initial.Realm.DataRevision, afterIdlePulses.Realm.DataRevision, "무변경 경지 revision");
    Equal(initial.Aptitude.DataRevision, afterIdlePulses.Aptitude.DataRevision, "무변경 자질 revision");
    Equal(initial.Wealth.DataRevision, afterIdlePulses.Wealth.DataRevision, "무변경 부자 revision");

    world.AdvanceTo(new SimulationTick(10L * GameCalendar.TicksPerYear));
    var afterTenYears = world.GetRankingCacheDiagnostics();
    var elapsedMonthBoundaries = GameCalendar.GetMonthIndex(world.Tick);
    AssertMonthlySortBound(initial.CombatPower, afterTenYears.CombatPower, elapsedMonthBoundaries, "전투력");
    AssertMonthlySortBound(initial.Realm, afterTenYears.Realm, elapsedMonthBoundaries, "경지");
    AssertMonthlySortBound(initial.Aptitude, afterTenYears.Aptitude, elapsedMonthBoundaries, "자질");
    AssertMonthlySortBound(initial.Wealth, afterTenYears.Wealth, elapsedMonthBoundaries, "부자");
    Equal(
        world.TotalReincarnations > 0,
        initial.Aptitude.DataRevision != afterTenYears.Aptitude.DataRevision,
        "자질 revision은 환생으로만 변경");
    // 새 월의 환경·행동은 다음 월 의사결정 스냅샷을 dirty로 만들 수 있다. 관찰은 이를
    // 즉시 정렬하지 않고, 다음 결정론적 월 경계에서 범주별 한 번만 확정해야 한다.

    var hashBeforeObservation = world.ComputeStateHash();
    var snapshot = world.CreateSnapshot(population, 0);
    var decisionRankings = world.CreateCheckpoint().DecisionRankings!;
    AssertDecisionRankingOrder(snapshot.CombatPowerRankings, decisionRankings.CombatRanksByAgent, "전투력");
    AssertDecisionRankingOrder(snapshot.RealmRankings, decisionRankings.RealmRanksByAgent, "경지");
    AssertDecisionRankingOrder(snapshot.AptitudeRankings, decisionRankings.AptitudeRanksByAgent, "자질");
    AssertDecisionRankingOrder(snapshot.WealthRankings, decisionRankings.WealthRanksByAgent, "부자");
    Equal(hashBeforeObservation, world.ComputeStateHash(), "랭킹 관찰은 권위 상태 불변");
    Equal(
        afterTenYears.TotalSortCount,
        world.GetRankingCacheDiagnostics().TotalSortCount,
        "랭킹 관찰 중 정렬 0회");

    var checkpoint = world.CreateCheckpoint();
    Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "현재 인과·생애·조직 권위 schema");
    var restored = World.Restore(checkpoint);
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "랭킹 cache 저장 제외·복원 상태 hash");
    var restoredSnapshot = restored.CreateSnapshot(population, 0);
    EqualSequence(
        snapshot.CombatPowerRankings.Select(entry => entry.AgentId.Value).ToArray(),
        restoredSnapshot.CombatPowerRankings.Select(entry => entry.AgentId.Value).ToArray(),
        "복원 전투력 랭킹");
    EqualSequence(
        snapshot.RealmRankings.Select(entry => entry.AgentId.Value).ToArray(),
        restoredSnapshot.RealmRankings.Select(entry => entry.AgentId.Value).ToArray(),
        "복원 경지 랭킹");
    EqualSequence(
        snapshot.AptitudeRankings.Select(entry => entry.AgentId.Value).ToArray(),
        restoredSnapshot.AptitudeRankings.Select(entry => entry.AgentId.Value).ToArray(),
        "복원 자질 랭킹");
    EqualSequence(
        snapshot.WealthRankings.Select(entry => entry.AgentId.Value).ToArray(),
        restoredSnapshot.WealthRankings.Select(entry => entry.AgentId.Value).ToArray(),
        "복원 부자 랭킹");
    var restoredInitial = restored.GetRankingCacheDiagnostics();
    Equal(4L, restoredInitial.TotalSortCount, "복원 파생 랭킹 범주별 1회 재구축");
    restored.AdvanceTo(restored.Tick);
    Equal(
        restoredInitial.TotalSortCount,
        restored.GetRankingCacheDiagnostics().TotalSortCount,
        "복원 직후 무변경 pulse 정렬 0회");
}

static void AssertMonthlySortBound(
    RankingCategoryCacheDiagnostics before,
    RankingCategoryCacheDiagnostics after,
    long elapsedMonthBoundaries,
    string category)
{
    var revisionDelta = checked(after.DataRevision - before.DataRevision);
    var sortDelta = checked(after.SortCount - before.SortCount);
    True(sortDelta <= elapsedMonthBoundaries, $"{category} 월 경계당 최대 한 번 정렬");
    True((ulong)sortDelta <= revisionDelta, $"{category} dirty revision 없는 정렬 금지");
    Equal(revisionDelta > 0, sortDelta > 0, $"{category} 변경 revision 월 경계 반영");
}

static void AssertDecisionRankingOrder(
    IReadOnlyList<RankingEntry> ranking,
    IReadOnlyList<int> ranksByAgent,
    string category)
{
    var expectedAgents = Enumerable.Range(0, ranksByAgent.Count)
        .OrderBy(agent => ranksByAgent[agent])
        .ToArray();
    EqualSequence(
        expectedAgents,
        ranking.Select(entry => entry.AgentId.Value).ToArray(),
        $"{category} 월 경계 의사결정 랭킹 순서");
    EqualSequence(
        Enumerable.Range(1, ranking.Count).ToArray(),
        ranking.Select(entry => entry.Rank).ToArray(),
        $"{category} 1-based 연속 순위");
}

static void GameTimeAndRealmRules()
{
    var expectedVersion = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "VERSION")).Trim();
    Equal(expectedVersion, ProductInfo.Version, "제품 버전");
    Equal(2_000L, GameCalendar.TicksPerYear, "1게임년 tick 수");
    Equal(10L, GameCalendar.GetYear(new SimulationTick(20_000)), "tick의 게임년 변환");
    Equal(1d, GameCalendar.GetRealSecondsPerGameYear(10), "10배속 게임년 현실 초");
    Equal(
        200,
        SimulationPacing.GetTicksPerPulse(1) * 1_000 / SimulationPacing.PulseMilliseconds,
        "1배속 초당 tick");
    True(
        SimulationPacing.GetTicksPerPulse(100) % GameCalendar.TicksPerYear != 0,
        "100배속 화면 갱신 보폭은 정확히 1년이면 안 됩니다.");
    True(
        Enumerable.Range(1, 10)
            .Select(step => GameCalendar.GetDate(new SimulationTick(
                (long)SimulationPacing.GetTicksPerPulse(100) * step)).Month)
            .Distinct()
            .Count() > 1,
        "100배속 월 표시 변화");

    var expectedLifespans = new long[]
    {
        150, 300, 600, 1_200, 3_000, 9_000, 20_000, 100_000, 100_000, 1_000_000,
    };
    Equal(expectedLifespans.Length, RealmRules.All.Count, "10대경지 수");
    for (var index = 0; index < expectedLifespans.Length; index++)
    {
        Equal(expectedLifespans[index], RealmRules.GetProfile((RealmStage)index).MaximumLifespanYears, "경지별 수명");
        Equal(
            expectedLifespans[index] / 3m,
            RealmRules.GetProfile((RealmStage)index).TargetCultivationYears,
            "기준 수행 기간은 수명의 3분의 1");
    }

    using var runner = new SimulationRunner(World.Create(1, 10));
    foreach (var speed in new[] { 1, 10, 25, 50, 100 })
    {
        runner.SetSpeed(speed);
        Equal(speed, runner.Speed, "지원 배속");
    }

    Throws<ArgumentOutOfRangeException>(() => runner.SetSpeed(4), "미지원 배속");
}

static void CalendarAndMonthlyActionLimit()
{
    Equal(new GameDate(0, 1, 1), GameCalendar.GetDate(new SimulationTick(0)), "세계 시작일");
    Equal(new GameDate(0, 1, 30), GameCalendar.GetDate(new SimulationTick(166)), "첫 달 마지막 날");
    Equal(new GameDate(0, 2, 1), GameCalendar.GetDate(new SimulationTick(167)), "둘째 달 첫날");
    Equal(new GameDate(0, 12, 30), GameCalendar.GetDate(new SimulationTick(1_999)), "첫해 마지막 날");
    Equal(new GameDate(1, 1, 1), GameCalendar.GetDate(new SimulationTick(2_000)), "다음해 첫날");
    Equal(167L, GameCalendar.GetMonthStart(1).Value, "둘째 달 시작 tick");
    Equal(334L, GameCalendar.GetMonthStart(2).Value, "셋째 달 시작 tick");
    Equal(2_000L, GameCalendar.GetMonthStart(12).Value, "다음해 시작 tick");

    var world = World.Create(99001, 500);
    var previousActions = new int[world.Population];
    for (var month = 0; month < 24; month++)
    {
        var endOfMonth = GameCalendar.GetMonthStart(month + 1).Value - 1;
        world.AdvanceTo(new SimulationTick(endOfMonth));
        for (var index = 0; index < world.Population; index++)
        {
            var agent = world.GetAgentSnapshot(new AgentId(index));
            var delta = agent.ActionsTaken - previousActions[index];
            True(delta is >= 0 and <= 1, $"Agent {index}의 월 행동 횟수는 최대 1회여야 합니다.");
            previousActions[index] = agent.ActionsTaken;
        }
    }

    True(previousActions.All(count => count > 0), "모든 AI가 월별 행동 일정에 참여해야 합니다.");
    var checkpoint = world.CreateCheckpoint();
    var restored = World.Restore(checkpoint);
    for (var index = 0; index < world.Population; index++)
    {
        var originalAgent = world.GetAgentSnapshot(new AgentId(index));
        var restoredAgent = restored.GetAgentSnapshot(new AgentId(index));
        Equal(originalAgent.ActionsTaken, restoredAgent.ActionsTaken, "저장 후 월 행동 누계");
        True(
            Nullable.Equals(originalAgent.LastActionDate, restoredAgent.LastActionDate),
            "저장 후 마지막 행동일");
    }
}

static void CultivationAuthorityRules()
{
    Equal(9, RealmRules.MinorStageCount, "모든 대경지는 1~9층");
    Equal(1_000, CultivationRules.GetMonthlyRealmWorkGain(5_000), "자질 50.00 월 Work");
    Equal(500, CultivationRules.GetMonthlyRealmWorkGain(2_500), "자질 25.00 월 Work");
    Equal(2_000, CultivationRules.GetMonthlyRealmWorkGain(10_000), "자질 100.00 월 Work");
    foreach (var profile in RealmRules.All)
    {
        var expected = checked(profile.MaximumLifespanYears * 4L * CultivationRules.BaseMonthlyWork);
        var totalRealmWork = RealmRules.GetTotalRealmRequiredWork(profile.Stage);
        var baselineMonths = totalRealmWork / (decimal)CultivationRules.BaseMonthlyWork;
        Equal(expected, totalRealmWork, "대경지 1층→9층 Work 합계");
        Equal(
            profile.MaximumLifespanYears * 4m,
            baselineMonths,
            "자질 50.00에서 대경지 1층→9층 대원만 전체 기준 개월");
        Equal(
            profile.TargetCultivationYears,
            baselineMonths / GameCalendar.MonthsPerYear,
            "대경지 전체 기준 수행 기간은 수명의 3분의 1");
        True(
            RealmRules.GetMinorStageRequiredWork(profile.Stage, 1) < totalRealmWork,
            "층 하나에 대경지 전체 기준 기간을 적용하면 안 됩니다.");
    }

    var gate = CultivationRules.EvaluateEntryGate(1, 5_500, 5_000);
    Equal(6_250, gate.GateScoreHundredths, "입문 GateScore");
    True(gate.Passed, "Core 40.00 및 GateScore 60.00 통과");
    True(CultivationRules.ProvisionalBalances.All(item => item.RequiresReview), "임시 수치는 재검토 표시");
    Equal(8, CultivationFormulaRegistry.All.Count, "F-CULT 공식 레지스트리 수");
    Equal(
        8,
        CultivationFormulaRegistry.All
            .Select(item => item.StableId)
            .Distinct(StringComparer.Ordinal)
            .Count(),
        "F-CULT 안정 ID 중복 금지");
    True(
        CultivationRules.GetAttemptClass(9, greatPerfection: false)
            == BreakthroughClass.FloorNineCompletion,
        "9층 Work 완료 뒤 별도 소경지 돌파 분류");
    True(
        CultivationRules.GetAttemptClass(9, greatPerfection: true)
            == BreakthroughClass.MajorRealm,
        "대원만 뒤 대경지 돌파 분류");
    Throws<ArgumentOutOfRangeException>(
        () => CultivationFormulaRegistry.Get(CultivationFormulaKind.None),
        "빈 수행 공식 ID 거부");
}

static void CultivationDetailProgressProjection()
{
    Equal(1, CultivationDetailProgressRules.CurrentRevision, "세부 수행 정의 revision");
    True(
        CultivationDetailProgressRules.ProvisionalBalance.RequiresReview,
        "미확정 단계와 가중치는 PC_TEMP_REVIEW");

    var allStableIds = new HashSet<string>(StringComparer.Ordinal);
    var entryDefinitions = CultivationDetailProgressRules.GetDefinitions(
        CultivationLifecyclePhase.EntryTraining,
        RealmStage.QiRefining);
    Equal(CultivationDetailProgressRules.StageCount, entryDefinitions.Count, "범인 입문 세부 단계 수");
    foreach (var definition in entryDefinitions)
    {
        True(allStableIds.Add(definition.StableId), "범인 입문 세부 단계 안정 ID 고유");
    }

    foreach (var realm in RealmRules.All.Select(profile => profile.Stage))
    {
        var definitions = CultivationDetailProgressRules.GetDefinitions(
            CultivationLifecyclePhase.RealmCultivation,
            realm);
        Equal(CultivationDetailProgressRules.StageCount, definitions.Count, $"{realm} 세부 단계 수");
        Equal(
            CultivationDetailProgressRules.BasisPointMaximum,
            definitions.Sum(definition => definition.WeightBasisPoints),
            $"{realm} 세부 단계 가중치 합");
        True(definitions.All(definition => definition.RequiresReview), $"{realm} 임시 검토 표식");
        foreach (var definition in definitions)
        {
            True(allStableIds.Add(definition.StableId), $"{realm} 세부 단계 안정 ID 고유");
        }
    }

    Equal(
        55,
        allStableIds.Count,
        "범인 입문 5개와 10대경지 50개 안정 ID");
    var qiDefinitions = CultivationDetailProgressRules.GetDefinitions(
        CultivationLifecyclePhase.RealmCultivation,
        RealmStage.QiRefining);
    Equal("영기 감지", qiDefinitions[0].KoreanName, "연기경 1단계 영기 감지");
    Equal("천지 감응", qiDefinitions[1].KoreanName, "연기경 2단계 감응");
    Equal("원리 이해·깨달음", qiDefinitions[2].KoreanName, "연기경 3단계 깨달음");
    Equal("영기 인도", qiDefinitions[3].KoreanName, "연기경 4단계 영기 인도");
    Equal("단전 축적", qiDefinitions[4].KoreanName, "연기경 5단계 단전 축적");

    var totalWork = RealmRules.GetTotalRealmRequiredWork(RealmStage.QiRefining);
    var checkpoints = new[] { 0, 1_000, 2_500, 4_500, 7_000, 10_000 };
    var previousOverall = -1;
    for (var index = 0; index < checkpoints.Length; index++)
    {
        var target = totalWork * checkpoints[index] / 10_000;
        var snapshot = CreateQiDetailAtAccumulatedWork(target, totalWork);
        Equal(checkpoints[index], snapshot.OverallProgressBasisPoints, "연기경 전체 세부 진척 경계");
        True(snapshot.OverallProgressBasisPoints >= previousOverall, "세부 수행 진척 단조 증가");
        previousOverall = snapshot.OverallProgressBasisPoints;
        if (index < checkpoints.Length - 1)
        {
            Equal(index, snapshot.CurrentStageIndex, "가중치 경계의 현재 세부 단계");
            True(
                snapshot.Stages.Take(index).All(stage =>
                    stage.State == CultivationDetailStageState.Completed),
                "이전 세부 단계 완료");
            True(
                snapshot.Stages[index].State == CultivationDetailStageState.Active,
                "현재 세부 단계 활성");
        }
        else
        {
            True(
                snapshot.Stages.All(stage =>
                    stage.State == CultivationDetailStageState.Completed),
                "연기경 전체 Work 완료 시 모든 세부 단계 완료");
        }
    }

    var world = World.Create(741105, 10);
    var beforeHash = world.ComputeStateHash();
    var beforeAgent = world.GetAgentSnapshot(new AgentId(0));
    using (var runner = new SimulationRunner(world))
    {
        var hidden = runner.GetObservation(
            new AgentId(0),
            sections: ObservationSections.None);
        True(hidden.SelectedAgent.Cultivation.DetailProgress is null, "접힌 세부 수행 UI는 DTO 미생성");
        Equal(0, hidden.SelectedAgent.Cultivation.EntryGates.Count, "수행 탭이 닫히면 입문 Gate DTO 0개");
        Equal(0, hidden.SelectedAgent.Cultivation.Components.Count, "수행 탭이 닫히면 경지 구성요소 DTO 0개");
        True(hidden.SelectedAgent.Cultivation.ActionPlan is null, "수행 탭이 닫히면 계획 DTO 미생성");
        True(
            hidden.SelectedAgent.Cultivation.FoundationFormation is null,
            "수행 탭이 닫히면 축기 형성 DTO 미생성");
        var cultivationVisible = runner.GetObservation(
            new AgentId(0),
            sections: ObservationSections.Cultivation);
        Equal(3, cultivationVisible.SelectedAgent.Cultivation.EntryGates.Count, "수행 탭이 열리면 입문 Gate DTO 생성");
        True(
            cultivationVisible.SelectedAgent.Cultivation.DetailProgress is null,
            "세부 토글이 닫히면 수행 단계 DTO는 계속 미생성");
        var visible = runner.GetObservation(
            new AgentId(0),
            sections: ObservationSections.Cultivation | ObservationSections.CultivationDetail);
        Equal(
            CultivationDetailProgressRules.StageCount,
            visible.SelectedAgent.Cultivation.DetailProgress!.Stages.Count,
            "펼친 세부 수행 UI만 5개 DTO 생성");
        Equal(
            "범인 입문 준비",
            visible.SelectedAgent.Cultivation.DetailProgress.ScopeKoreanName,
            "미입문자는 연기경으로 위장하지 않음");

        for (var warmup = 0; warmup < 10; warmup++)
        {
            _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.None);
            _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.Cultivation);
        }

        var hiddenStart = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < 500; sample++)
        {
            _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.None);
        }
        var hiddenAllocated = GC.GetAllocatedBytesForCurrentThread() - hiddenStart;
        var visibleStart = GC.GetAllocatedBytesForCurrentThread();
        for (var sample = 0; sample < 500; sample++)
        {
            _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.Cultivation);
        }
        var visibleAllocated = GC.GetAllocatedBytesForCurrentThread() - visibleStart;
        True(hiddenAllocated < visibleAllocated, "닫힌 수행 탭은 열린 탭보다 적게 할당");
        Console.WriteLine(
            $"CULTIVATION DETAIL UI PERF: 닫힘 {hiddenAllocated:N0} B / " +
            $"수행 탭 {visibleAllocated:N0} B · " +
            $"할당 감소 {(1m - (hiddenAllocated / (decimal)visibleAllocated)) * 100m:0.0}% (500회)");
    }

    var afterAgent = world.GetAgentSnapshot(new AgentId(0));
    Equal(beforeHash, world.ComputeStateHash(), "세부 수행 관찰은 권위 상태·RNG 무영향");
    Equal(beforeAgent.CultivationProgress, afterAgent.CultivationProgress, "세부 수행 관찰은 Work 불변");
    Equal(beforeAgent.TechniqueQuality, afterAgent.TechniqueQuality, "깨달음 표시는 공법 품질 불변");
    Equal(beforeAgent.SpellQuality, afterAgent.SpellQuality, "깨달음 표시는 법술 품질 불변");

    static CultivationDetailProgressSnapshot CreateQiDetailAtAccumulatedWork(
        long accumulatedWork,
        long totalWork)
    {
        if (accumulatedWork >= totalWork)
        {
            return CultivationDetailProgressRules.Create(
                CultivationLifecyclePhase.RealmCultivation,
                RealmStage.QiRefining,
                RealmRules.MinorStageCount,
                RealmRules.GetMinorStageRequiredWork(
                    RealmStage.QiRefining,
                    RealmRules.MinorStageCount),
                floorNineCompleted: true,
                greatPerfectionAchieved: false);
        }

        var remaining = accumulatedWork;
        for (var floor = 1; floor <= RealmRules.MinorStageCount; floor++)
        {
            var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, floor);
            if (remaining <= requirement)
            {
                return CultivationDetailProgressRules.Create(
                    CultivationLifecyclePhase.RealmCultivation,
                    RealmStage.QiRefining,
                    floor,
                    checked((int)remaining),
                    floorNineCompleted: false,
                    greatPerfectionAchieved: false);
            }

            remaining -= requirement;
        }

        throw new InvalidOperationException("연기경 누적 Work를 층에 매핑할 수 없습니다.");
    }
}

static void EntryGateRecoveryAndAppliedWork()
{
    var blockedWorld = World.Create(551120, 1);
    var blockedCheckpoint = blockedWorld.CreateCheckpoint();
    var blockedAgent = WithSynchronizedAptitudeDetails(blockedCheckpoint.Agents[0] with
    {
        SpiritualRoot = 0,
        Bone = 0,
        Constitution = 0,
        Comprehension = 0,
        Mind = 0,
        Soul = 0,
        CultivationProgress = CultivationRules.RequiredEntryWork,
        CultivationState = CultivationState.EntryTraining,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
    });
    blockedWorld = World.Restore(blockedCheckpoint with { Agents = [blockedAgent] });
    blockedWorld.AdvanceTo(new SimulationTick(1));
    var blocked = blockedWorld.GetAgentSnapshot(new AgentId(0));
    True(blocked.CultivationState == CultivationState.Bottleneck, "Gate 실패는 실제 병목 상태");
    True(blocked.BottleneckCause == CultivationBottleneckCause.EntryGate, "입문 Gate 병목 원인");
    Equal(0, blocked.Cultivation.LatestTrace.WorkGain, "이미 가득 찬 입문 Work의 실제 적용 증가량");

    blockedWorld.AdvanceTo(GameCalendar.GetMonthStart(1));
    var tempered = blockedWorld.GetAgentSnapshot(new AgentId(0));
    True(
        tempered.Cultivation.EntryGates.Any(gate => gate.TemperingBonusHundredths > 0),
        "실패 Gate에 단련 보정이 누적되어야 합니다.");
    var restored = World.Restore(blockedWorld.CreateCheckpoint());
    Equal(blockedWorld.ComputeStateHash(), restored.ComputeStateHash(), "입문 Gate 단련 저장 왕복");

    restored.AdvanceTo(GameCalendar.FromYears(10));
    var entered = restored.GetAgentSnapshot(new AgentId(0));
    True(
        entered.CultivationState != CultivationState.EntryTraining
            && !(entered.CultivationState == CultivationState.Bottleneck
                && entered.BottleneckCause == CultivationBottleneckCause.EntryGate),
        "고정 선천자질이 낮아도 Gate 단련으로 입문 정체를 해소해야 합니다.");
    True(
        restored.GetAgentChronicle(new AgentId(0))
            .Any(entry => entry.Kind == ChronicleEventKind.CultivationEntered),
        "Gate 단련 뒤 입문 연대기");

    var cappedWorld = World.Create(551121, 1);
    var cappedCheckpoint = cappedWorld.CreateCheckpoint();
    var cappedAgent = WithSynchronizedAptitudeDetails(cappedCheckpoint.Agents[0] with
    {
        SpiritualRoot = 10_000,
        Bone = 10_000,
        Constitution = 10_000,
        Comprehension = 10_000,
        Mind = 10_000,
        Soul = 10_000,
        CultivationProgress = CultivationRules.RequiredEntryWork - 1_000,
        CultivationState = CultivationState.EntryTraining,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
    });
    cappedWorld = World.Restore(cappedCheckpoint with { Agents = [cappedAgent] });
    cappedWorld.AdvanceTo(new SimulationTick(1));
    Equal(
        1_000,
        cappedWorld.GetAgentSnapshot(new AgentId(0)).Cultivation.LatestTrace.WorkGain,
        "입문 Work 표시는 원시 gain이 아니라 cap 적용분이어야 합니다.");
}

static void CultivationStateMachine()
{
    var baseline = World.Create(551122, 1);
    var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 1);
    var checkpoint = baseline.CreateCheckpoint();
    var preparedAgent = checkpoint.Agents[0] with
    {
        CultivationProgress = requirement,
        CultivationState = CultivationState.RealmCultivation,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
    };
    var world = World.Restore(checkpoint with { Agents = [preparedAgent] });
    var randomBeforePreparation = world.CreateCheckpoint().RandomState;

    world.AdvanceTo(new SimulationTick(1));
    var preparing = world.GetAgentSnapshot(new AgentId(0));
    True(
        preparing.CultivationState == CultivationState.PreparingBreakthrough,
        "Work 충족 뒤에는 즉시 확률 판정하지 않고 돌파 준비에 들어가야 합니다.");
    Equal(randomBeforePreparation, world.CreateCheckpoint().RandomState, "돌파 준비는 RNG를 소비하지 않음");

    world.AdvanceTo(GameCalendar.GetMonthStart(1));
    var preflight = world.GetAgentSnapshot(new AgentId(0));
    True(preflight.CultivationState == CultivationState.PreflightReady, "준비 뒤 Preflight 대기 상태");
    Equal(randomBeforePreparation, world.CreateCheckpoint().RandomState, "Preflight 준비는 RNG를 소비하지 않음");

    world.AdvanceTo(GameCalendar.GetMonthStart(2));
    var attempted = world.GetAgentSnapshot(new AgentId(0));
    True(
        attempted.CultivationState is CultivationState.Stable or CultivationState.PreparingBreakthrough,
        "돌파 시도는 성공 안정 또는 실제 실패 후 재준비로 끝나야 합니다.");

    var restored = World.Restore(world.CreateCheckpoint());
    var restoredAgent = restored.GetAgentSnapshot(new AgentId(0));
    True(attempted.CultivationState == restoredAgent.CultivationState, "수행 상태 저장 왕복");
    True(attempted.BottleneckCause == restoredAgent.BottleneckCause, "병목 원인 저장 왕복");

    foreach (var profile in RealmRules.All)
    {
        True(!string.IsNullOrWhiteSpace(profile.StableId), "Realm Profile 안정 ID");
        Equal(RealmProfile.CurrentRevision, profile.Revision, "Realm Profile revision");
        Equal(
            profile.Stage != RealmStage.QiRefining,
            profile.RequiresStabilization,
            "대경지 진입 뒤에만 안착 상태");
    }
}

static void LongCultivationPlanConditionsAndPersistence()
{
    Equal(1, CultivationPlanRules.MinimumPlanMonths, "최소 행동 계획 1개월");
    Equal(120_000, CultivationPlanRules.MaximumPlanMonths, "최대 행동 계획 1만년");

    var world = World.Create(551129, 256);
    var initial = world.GetAgentSnapshot(new AgentId(0));
    var decisionRevision = initial.Decision.Latest.AuthorityRevision;
    world.AdvanceTo(initial.NextDecisionTick);
    var committed = world.GetAgentSnapshot(new AgentId(0));
    var plan = committed.Cultivation.ActionPlan
        ?? throw new InvalidOperationException("수행 행동 경계에서 장기 계획이 생성되어야 합니다.");

    True(plan.IsActive, "현재 수행 계획 활성 상태");
    True(
        plan.PlannedMonths is >= CultivationPlanRules.MinimumPlanMonths
            and <= CultivationPlanRules.MaximumPlanMonths,
        "수행 계획 기간 범위");
    True(plan.PlannedEndTick.Value > plan.StartedTick.Value, "수행 시작·종료 시점 순서");
    Equal(committed.LocationId, plan.LocationId, "수행 장소 고정");
    True(Enum.IsDefined(plan.Intensity), "수행 강도 typed 조건");
    True(Enum.IsDefined(plan.ResourcePolicy), "소비 자원 typed 조건");
    True(Enum.IsDefined(plan.FormationSupport), "수행 진법 typed 조건");
    True(Enum.IsDefined(plan.TeachingRole), "가르침 역할 typed 조건");
    True(plan.ConditionFactorBasisPoints is >= 5_000 and <= 20_000, "조건 종합 계수 범위");
    True(plan.TeachingBonusBasisPoints is >= 10_000 and <= 13_000, "가르침 흡수 계수 범위");
    True(plan.RequiresReview, "임의 수행 조건 PC_TEMP_REVIEW 표식");
    Equal(
        decisionRevision,
        committed.Decision.Latest.AuthorityRevision,
        "장기 계획 pulse는 목표·계획을 매번 다시 계산하지 않음");

    var checkpoint = world.CreateCheckpoint();
    var persistedPlan = checkpoint.Agents[0].Cultivation!.Value.ActionPlan
        ?? throw new InvalidOperationException("수행 계획 권위 저장 누락");
    Equal(plan.AuthorityRevision, persistedPlan.AuthorityRevision, "수행 계획 revision 저장");
    Equal(plan.PlannedMonths, persistedPlan.PlannedMonths, "수행 계획 기간 저장");
    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "수행 계획 codec·hash 왕복");

    restored.AdvanceTo(plan.PlannedEndTick);
    var boundary = restored.GetAgentSnapshot(new AgentId(0));
    True(
        boundary.Decision.Latest.AuthorityRevision > decisionRevision,
        "수행 계획 종료 경계에서만 다음 행동을 판단");
}

static void HighRealmHealthRatioDoesNotOverflow()
{
    Equal(
        10_000,
        CultivationPlanRules.GetBodyConditionBasisPoints(int.MaxValue, int.MaxValue),
        "int 상한 최대 체력 비율");
    Equal(
        5_000,
        CultivationPlanRules.GetBodyConditionBasisPoints(1_000_000_000, 2_000_000_000),
        "대형 체력 50% 비율");
    Equal(
        10_000,
        CultivationPlanRules.GetBodyConditionBasisPoints(int.MaxValue, 1),
        "현재 체력이 최대값을 넘는 손상 입력 상한");
    Throws<ArgumentOutOfRangeException>(
        () => CultivationPlanRules.GetBodyConditionBasisPoints(1, 0),
        "최대 체력 0 거부");
}

static void FloorNineGreatPerfectionAndFormulaTrace()
{
    var seedWorld = World.Create(551123, 1);
    var checkpoint = seedWorld.CreateCheckpoint();
    var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 9);
    var cultivation = checkpoint.Agents[0].Cultivation!.Value with
    {
        PendingCarryoverWork = 0,
        LastWorkBefore = requirement - 1,
        LastWorkGain = 0,
        FloorNineCompleted = false,
        GreatPerfectionAchieved = false,
        LatestTrace = CultivationTraceState.Empty,
        AuthorityRevision = 0,
    };
    var floorNineAgent = checkpoint.Agents[0] with
    {
        RealmStage = RealmStage.QiRefining,
        RealmLayer = 9,
        CultivationProgress = requirement - 1,
        CultivationState = CultivationState.RealmCultivation,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
        LastActionMonth = -1,
        LastActionTick = 0,
        ActionsTaken = 0,
        Cultivation = cultivation,
    };
    var world = World.Restore(checkpoint with { Agents = [floorNineAgent] });

    world.AdvanceTo(new SimulationTick(1));
    var preparing = world.GetAgentSnapshot(new AgentId(0));
    True(!preparing.Cultivation.FloorNineCompleted, "Work 충족만으로 9층을 완성하면 안 됨");
    True(preparing.CultivationState == CultivationState.PreparingBreakthrough, "9층 돌파 준비 상태");
    True(
        preparing.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.ProgressAccumulation,
        "Work Formula Trace");
    Equal(1L, preparing.Cultivation.AuthorityRevision, "첫 수행 권위 revision");

    var nextMonth = 1L;
    world.AdvanceTo(GameCalendar.GetMonthStart(nextMonth++));
    var preflight = world.GetAgentSnapshot(new AgentId(0));
    True(preflight.CultivationState == CultivationState.PreflightReady, "9층 Preflight 준비");
    True(
        preflight.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.BreakthroughReadiness,
        "Preflight Formula Trace");

    AgentSnapshot completed = preflight;
    for (var attempt = 0; attempt < 24 && !completed.Cultivation.FloorNineCompleted; attempt++)
    {
        world.AdvanceTo(GameCalendar.GetMonthStart(nextMonth++));
        completed = world.GetAgentSnapshot(new AgentId(0));
    }

    True(completed.Cultivation.FloorNineCompleted, "9층 소경지 돌파가 유한 시도 안에 완료되어야 함");
    True(!completed.Cultivation.GreatPerfectionAchieved, "9층 완성과 대원만은 같은 commit이면 안 됨");
    Equal(9, completed.RealmLayer, "9층 완성 뒤 층 유지");
    True(
        completed.Cultivation.LatestTrace.AttemptClass == BreakthroughClass.FloorNineCompletion,
        "9층 Typed Result");
    True(
        completed.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.BreakthroughResult,
        "돌파 결과 Formula Trace");
    Equal(
        completed.Cultivation.AuthorityRevision,
        completed.Cultivation.LatestTrace.AuthorityRevisionAfter,
        "Trace와 수행 권위 revision 일치");
    True(
        world.GetAgentChronicle(new AgentId(0))
            .Any(entry => entry.Kind == ChronicleEventKind.FloorNineCompletion),
        "9층 소경지 완성 영구 연대기");

    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(world.CreateCheckpoint())));
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "수행 Formula Trace 저장·codec 왕복");
    var restoredCompleted = restored.GetAgentSnapshot(new AgentId(0));
    Equal(completed.Cultivation.AuthorityRevision, restoredCompleted.Cultivation.AuthorityRevision, "수행 권위 revision 복원");

    restored.AdvanceTo(GameCalendar.GetMonthStart(nextMonth));
    var greatPerfection = restored.GetAgentSnapshot(new AgentId(0));
    True(greatPerfection.Cultivation.GreatPerfectionAchieved, "9층 완성 다음 별도 행동에서 대원만 완성");
    True(
        greatPerfection.Cultivation.LatestTrace.AttemptClass
            == BreakthroughClass.GreatPerfectionCompletion,
        "대원만 Typed Result");
    True(
        restored.GetAgentChronicle(new AgentId(0))
            .Any(entry => entry.Kind == ChronicleEventKind.GreatPerfectionAchieved),
        "대원만 영구 연대기");

    var rankingSeed = World.Create(551124, 3);
    var rankingCheckpoint = rankingSeed.CreateCheckpoint();
    var rankedAgents = rankingCheckpoint.Agents
        .Select((agent, index) => agent with
        {
            RealmStage = RealmStage.QiRefining,
            RealmLayer = 9,
            CultivationProgress = requirement,
            CultivationState = CultivationState.Stable,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Cultivation = agent.Cultivation!.Value with
            {
                PendingCarryoverWork = 0,
                FloorNineCompleted = index <= 1,
                GreatPerfectionAchieved = index == 0,
                LatestTrace = CultivationTraceState.Empty,
                AuthorityRevision = 0,
            },
        })
        .ToArray();
    var rankingWorld = World.Restore(rankingCheckpoint with { Agents = rankedAgents });
    var realmRanking = rankingWorld.CreateSnapshot(
        3,
        0,
        ObservationSections.RealmRanking).RealmRankings;
    Equal(new AgentId(0), realmRanking[0].AgentId, "대원만이 같은 경지·층 1위");
    Equal(new AgentId(1), realmRanking[1].AgentId, "9층 완성이 Work 충족보다 우선");
    Equal(new AgentId(2), realmRanking[2].AgentId, "Work 충족 미돌파는 같은 층 최하위");
    True(realmRanking[0].Score > realmRanking[1].Score, "대원만 경지 점수 분리");
    True(realmRanking[1].Score > realmRanking[2].Score, "9층 완성 경지 점수 분리");
}

static void CultivationBreakthroughChronicle()
{
    var minorWorld = World.Create(444001, 1);
    var checkpoint = minorWorld.CreateCheckpoint();
    var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 1);
    var minorAgent = WithSynchronizedAptitudeDetails(checkpoint.Agents[0] with
    {
        SpiritualRoot = 10_000,
        Bone = 10_000,
        Constitution = 10_000,
        Comprehension = 10_000,
        Mind = 10_000,
        CultivationProgress = requirement,
        CultivationState = CultivationState.PreflightReady,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        Health = checkpoint.Agents[0].MaximumHealth,
        Qi = checkpoint.Agents[0].MaximumQi,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
    });
    minorWorld = World.Restore(checkpoint with { Agents = [minorAgent] });
    for (var month = 1; month <= 12
        && minorWorld.GetAgentSnapshot(new AgentId(0)).RealmLayer == 1; month++)
    {
        minorWorld.AdvanceTo(GameCalendar.GetMonthStart(month));
    }

    Equal(2, minorWorld.GetAgentSnapshot(new AgentId(0)).RealmLayer, "1→2층 돌파");
    True(
        minorWorld.GetAgentChronicle(new AgentId(0))
            .Any(item => item.Kind == ChronicleEventKind.MinorStageBreakthrough),
        "소경지 돌파 영구 연대기");

    var majorWorld = World.Create(444002, 1);
    var majorCheckpoint = majorWorld.CreateCheckpoint();
    var majorCultivation = majorCheckpoint.Agents[0].Cultivation!.Value with
    {
        FloorNineCompleted = true,
        GreatPerfectionAchieved = true,
    };
    var majorAgent = WithSynchronizedAptitudeDetails(majorCheckpoint.Agents[0] with
    {
        SpiritualRoot = 10_000,
        Bone = 10_000,
        Constitution = 10_000,
        Comprehension = 10_000,
        Mind = 10_000,
        RealmLayer = 9,
        CultivationProgress = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 9),
        CultivationState = CultivationState.PreflightReady,
        CultivationBottleneckCause = CultivationBottleneckCause.None,
        TechniqueMastery = 5_000,
        SkillProgress = majorCheckpoint.Agents[0].SkillProgress!.Value with
        {
            TechniqueUnderstanding = 5_000,
        },
        Health = majorCheckpoint.Agents[0].MaximumHealth,
        Qi = majorCheckpoint.Agents[0].MaximumQi,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
        Cultivation = majorCultivation,
    });
    majorWorld = World.Restore(majorCheckpoint with { Agents = [majorAgent] });
    for (var month = 1; month <= 36
        && majorWorld.GetAgentSnapshot(new AgentId(0)).RealmStage == RealmStage.QiRefining; month++)
    {
        majorWorld.AdvanceTo(GameCalendar.GetMonthStart(month));
    }

    var foundationAgent = majorWorld.GetAgentSnapshot(new AgentId(0));
    True(
        foundationAgent.RealmStage == RealmStage.FoundationEstablishment,
        "연기→축기 대경지 돌파");
    var formation = foundationAgent.Cultivation.FoundationFormation
        ?? throw new InvalidOperationException("축기 형성 결과가 없습니다.");
    Equal(FoundationMethodRules.StableId, formation.MethodStableId, "삼도축기요결 Method Ref");
    True(formation.CompletedSteps == FoundationProcedureStep.Complete,
        "삼도축기요결 5단계 완료");
    Equal(10, formation.QualityComponentsHundredths.Count, "축기 형성 품질 10요소");
    True(formation.Source.Route != FoundationRoute.None, "축기 FormationRoute 권위");
    True(
        formation.Source.Availability is CultivationValueAvailability.Actual
            or CultivationValueAvailability.ActualZero,
        "축기 Source Snapshot 가용성");
    var foundationCheckpoint = majorWorld.CreateCheckpoint();
    True(
        foundationCheckpoint.Agents[0].Cultivation!.Value.FoundationFormation is not null,
        "축기 형성 결과 저장 연결");
    var restoredFoundation = World.Restore(
        WorldSaveCodec.Decode(WorldSaveCodec.Encode(foundationCheckpoint)));
    Equal(majorWorld.ComputeStateHash(), restoredFoundation.ComputeStateHash(),
        "축기 형성 결과 결정론 저장 왕복");
    var corruptedCultivation = foundationCheckpoint.Agents[0].Cultivation!.Value;
    var corruptedFormation = corruptedCultivation.FoundationFormation!.Value with
    {
        MethodRevision = FoundationMethodRules.CurrentRevision + 1,
    };
    Throws<InvalidDataException>(
        () => World.Restore(foundationCheckpoint with
        {
            Agents =
            [
                foundationCheckpoint.Agents[0] with
                {
                    Cultivation = corruptedCultivation with
                    {
                        FoundationFormation = corruptedFormation,
                    },
                },
            ],
        }),
        "축기 형성 Method revision 손상 저장 거부");
    True(
        majorWorld.GetAgentChronicle(new AgentId(0))
            .Any(item => item.Kind == ChronicleEventKind.MajorRealmBreakthrough),
        "대경지 돌파 영구 연대기");
}

static void BirthAptitudesChronicleAndReincarnation()
{
    var initial = World.Create(8801, 1);
    var initialAgent = initial.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
    var initialSoul = initialAgent.Aptitudes.Soul;
    var initialSoulId = initialAgent.Identity.SoulId;
    var initialBodyId = initialAgent.Identity.BodyId;
    var initialPersonId = initialAgent.Identity.PersonId;
    Equal(1, initialAgent.LifeNumber, "첫 생 번호");
    Equal(0L, initialAgent.AgeYears, "탄생 나이");
    Equal(6, new[]
    {
        initialAgent.Aptitudes.SpiritualRoot,
        initialAgent.Aptitudes.Bone,
        initialAgent.Aptitudes.Constitution,
        initialAgent.Aptitudes.Comprehension,
        initialAgent.Aptitudes.Mind,
        initialAgent.Aptitudes.Soul,
    }.Length, "6대 수행자질");
    var initialChronicle = initial.GetAgentChronicle(new AgentId(0));
    Equal(1, initialChronicle.Count, "첫 일대기 기록 수");
    True(initialChronicle[0].Kind == ChronicleEventKind.Birth, "탄생 기록");

    var deathTick = GameCalendar.FromYears(150).Value;
    var birthCheckpoint = initial.CreateCheckpoint();
    initial.AdvanceTo(new SimulationTick(deathTick - 1));
    var checkpoint = initial.CreateCheckpoint();
    var preparedAgent = birthCheckpoint.Agents[0] with
    {
        BirthTick = 0,
        RealmStage = RealmStage.QiRefining,
        RealmLayer = (byte)MinorStage.Floor1,
        CultivationProgress = 0,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = deathTick,
        Opponent = -1,
    };
    var prepared = checkpoint with
    {
        SchemaVersion = 24,
        Agents = [preparedAgent],
        Causal = null,
    };
    var world = World.Restore(prepared);
    world.AdvanceTo(new SimulationTick(deathTick));
    var dead = world.GetAgentSnapshot(new AgentId(0));
    Equal(150L, dead.AgeYears, "연기경 최대 수명 도달");
    True(dead.Activity == AgentActivity.Reincarnating, "윤회의 강 상태");
    True(dead.Identity.LifeState == PersonLifeState.ReincarnationRiver, "윤회의 강 신원 상태");
    Equal(initialSoulId, dead.Identity.SoulId, "사망 후 영혼 ID 유지");
    Equal(initialBodyId, dead.Identity.BodyId, "윤회의 강에서 직전 육신 ID 유지");
    Equal(initialPersonId, dead.Identity.PersonId, "윤회의 강에서 직전 현생 ID 유지");
    Equal(1, world.TotalDeaths, "사망 집계");

    world.AdvanceTo(new SimulationTick(
        deathTick + ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining)));
    var reborn = world.GetAgentSnapshot(
        new AgentId(0),
        includeAptitudeDetails: true,
        includeCausal: true);
    Equal(2, reborn.LifeNumber, "환생 후 생 번호");
    Equal(0L, reborn.AgeYears, "환생 나이");
    Equal(initialSoul, reborn.Aptitudes.Soul, "영혼 자질 윤회 연속성");
    Equal(initialSoulId, reborn.Identity.SoulId, "환생 후 영혼 ID 연속성");
    True(reborn.Identity.BodyId != initialBodyId, "환생 후 새 육신 ID");
    True(reborn.Identity.PersonId != initialPersonId, "환생 후 새 현생 ID");
    True(reborn.Identity.LifeState == PersonLifeState.Living, "환생 후 생존 신원 상태");
    True(
        reborn.Causal!.CurrentLifeMemories.All(memory => memory.LifeNumber == 2),
        "전생 기억을 새 생 기억으로 자동 복사하지 않음");
    True(
        reborn.Causal.CurrentLifeMemories.Any(memory => memory.Kind == AgentMemoryKind.Rebirth),
        "새 생 환생 경계 기억");
    True(
        reborn.Aptitudes.SpiritualRoot != initialAgent.Aptitudes.SpiritualRoot
        || reborn.Aptitudes.Bone != initialAgent.Aptitudes.Bone
        || reborn.Aptitudes.Constitution != initialAgent.Aptitudes.Constitution
        || reborn.Aptitudes.Comprehension != initialAgent.Aptitudes.Comprehension
        || reborn.Aptitudes.Mind != initialAgent.Aptitudes.Mind,
        "새 육신의 자질은 다시 형성되어야 합니다.");
    var initialSoulComponents = initialAgent.AptitudeDetails!.Groups[(int)AptitudeDomain.Soul]
        .Components.Select(component => component.InnateValue).ToArray();
    var rebornSoulComponents = reborn.AptitudeDetails!.Groups[(int)AptitudeDomain.Soul]
        .Components.Select(component => component.InnateValue).ToArray();
    True(initialSoulComponents.SequenceEqual(rebornSoulComponents), "영혼 세부 자질 윤회 연속성");
    True(
        !initialAgent.AptitudeDetails.Groups[(int)AptitudeDomain.Bone].Components
            .Select(component => component.InnateValue)
            .SequenceEqual(reborn.AptitudeDetails.Groups[(int)AptitudeDomain.Bone].Components
                .Select(component => component.InnateValue)),
        "새 육신의 근골 세부 자질은 다시 형성되어야 합니다.");
    Equal(1, world.TotalReincarnations, "환생 집계");

    var chronicle = world.GetAgentChronicle(new AgentId(0));
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Death), "사망 일대기");
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.ReincarnationEntered), "윤회의 강 일대기");
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Rebirth), "환생 일대기");
}

static void StagedReincarnationLineageTraceAndMigration()
{
    var realms = new[]
    {
        RealmStage.QiRefining,
        RealmStage.FoundationEstablishment,
        RealmStage.GoldenCore,
        RealmStage.NascentSoul,
        RealmStage.SpiritTransformation,
        RealmStage.VoidRefinement,
        RealmStage.BodyIntegration,
        RealmStage.TribulationCrossing,
        RealmStage.Mahayana,
        RealmStage.HalfImmortal,
    };
    var expectedDurationYears = new[] { 3, 5, 8, 13, 21, 34, 55, 89, 144, 233 };
    for (var index = 0; index < realms.Length; index++)
    {
        Equal(
            expectedDurationYears[index],
            ReincarnationRules.GetTotalDurationYears(realms[index]),
            $"{realms[index]} 윤회의 강 총 기간");
        Equal(
            checked((long)expectedDurationYears[index] * GameCalendar.TicksPerYear),
            ReincarnationRules.GetTotalDurationTicks(realms[index]),
            $"{realms[index]} 윤회의 강 tick 기간");
    }

    Equal(
        834L,
        ReincarnationRules.GetStageBoundaryTick(
            startedTick: 0,
            RealmStage.FoundationEstablishment,
            ReincarnationStage.Entering),
        "12로 나누어떨어지지 않는 축기경 첫 경계 올림");
    True(
        ReincarnationRules.GetPreservationLevel(2_999) == TracePreservationLevel.Lost
        && ReincarnationRules.GetPreservationLevel(3_000) == TracePreservationLevel.Fragment
        && ReincarnationRules.GetPreservationLevel(5_500) == TracePreservationLevel.Partial
        && ReincarnationRules.GetPreservationLevel(8_000) == TracePreservationLevel.Intact,
        "전생 흔적 보존 등급 경계");
    True(
        ReincarnationRules.GetAccessLevel(2_999) == TraceAccessLevel.Sealed
        && ReincarnationRules.GetAccessLevel(3_000) == TraceAccessLevel.Latent
        && ReincarnationRules.GetAccessLevel(6_000) == TraceAccessLevel.Partial
        && ReincarnationRules.GetAccessLevel(8_500) == TraceAccessLevel.Immediate,
        "전생 흔적 접근 등급 경계");

    var stageContractStore = new ReincarnationStore(1);
    stageContractStore.InitializeFirstLife(
        agentIndex: 0,
        PersonCausalRules.CreateSoulId(0),
        PersonCausalRules.CreateBodyId(0, 1),
        PersonCausalRules.CreatePersonId(0, 1),
        personName: "단계계약",
        birthTick: 0,
        RealmStage.QiRefining,
        realmLayer: (byte)MinorStage.Floor1,
        combatPower: 100);
    var stageCandidates = ReincarnationRules.AllTraceDomains
        .Select(domain => new ReincarnationTraceCandidate(domain, 5_500, 3_000))
        .ToArray();
    var stageContractJourney = stageContractStore.BeginJourney(
        agentIndex: 0,
        deathTick: 0,
        LifeEndKind.Lifespan,
        RealmStage.QiRefining,
        stageCandidates);
    Throws<InvalidOperationException>(
        () => stageContractStore.AdvanceStage(
            agentIndex: 0,
            tick: checked(stageContractJourney.NextTransitionTick + 1)),
        "윤회 단계의 지연 전이는 유효하지 않은 저장 상태를 만들 수 없어야 함");

    var birthContextStore = new ReincarnationStore(1);
    birthContextStore.InitializeFirstLife(
        0,
        PersonCausalRules.CreateSoulId(0),
        PersonCausalRules.CreateBodyId(0, 1),
        PersonCausalRules.CreatePersonId(0, 1),
        "출생후보검증",
        birthTick: 0,
        RealmStage.QiRefining,
        realmLayer: 1,
        combatPower: 100);
    var birthJourney = birthContextStore.BeginJourney(
        0,
        deathTick: GameCalendar.TicksPerYear,
        LifeEndKind.Combat,
        RealmStage.QiRefining,
        stageCandidates,
        SoulDamageLevel.Severe,
        new SimulationEventRecordId(0xC001));
    while (birthJourney.Stage != ReincarnationStage.PreparingRebirth)
    {
        birthContextStore.AdvanceStage(
            0,
            birthJourney.NextTransitionTick,
            new SimulationEventRecordId(0xC002));
        birthJourney = birthContextStore.GetCurrentJourney(0)!;
    }
    var noCandidate = birthContextStore.PrepareBirthCandidate(
        0,
        birthJourney.RebirthReadyTick,
        parentOnePersonId: null,
        parentTwoPersonId: null,
        allowIndependentManifestation: false);
    True(noCandidate == BirthCandidateStatus.UnavailableLongWait, "출생 후보 부재 명시");
    var deferredTick = birthContextStore.DeferForBirthCandidate(
        0,
        birthJourney.RebirthReadyTick);
    var deferred = birthContextStore.GetCurrentJourney(0)!;
    Equal(1, deferred.BirthCandidateWaitYears, "출생 후보 부재 1년 장기 대기");
    True(deferred.BirthCandidateStatus == BirthCandidateStatus.Searching, "출생 후보 재탐색 상태");
    Equal(deferredTick, deferred.RebirthReadyTick, "연기된 환생 준비 경계");
    True(
        ReincarnationStore.IsValidCheckpoint(
            birthContextStore.CreateCheckpoint(),
            maximumTick: deferredTick - 1),
        "장기 출생 대기 저장 검증");
    True(birthContextStore.AdvanceStage(0, deferredTick).ReadyForRebirth, "연기 경계 재도달");
    var parentOne = new PersonIdentityId(0xAA01);
    var parentTwo = new PersonIdentityId(0xAA02);
    True(
        birthContextStore.PrepareBirthCandidate(
            0,
            deferredTick,
            parentOne,
            parentTwo,
            allowIndependentManifestation: false) == BirthCandidateStatus.Located,
        "출생 부모 후보 확정");
    birthContextStore.CompleteRebirth(
        0,
        deferredTick,
        PersonCausalRules.CreateSoulId(0),
        PersonCausalRules.CreateBodyId(0, 2),
        PersonCausalRules.CreatePersonId(0, 2),
        "출생후보검증2",
        RealmStage.QiRefining,
        initialRealmLayer: 1,
        initialCombatPower: 100,
        rebirthCausalRecordId: new SimulationEventRecordId(0xC003));
    var birthContext = birthContextStore.GetSnapshot(0, deferredTick);
    True(birthContext.CurrentJourney is null, "부모 연결 뒤 환생 완료");
    Equal(parentOne, birthContext.Lineage[^1].ParentOnePersonId!.Value, "첫 부모 현생 ID");
    Equal(parentTwo, birthContext.Lineage[^1].ParentTwoPersonId!.Value, "둘째 부모 현생 ID");
    True(birthContext.Lineage[^1].FamilyId.Value != 0, "부모 조합의 안정 가계 ID");
    True(
        birthContext.Lineage[^1].BirthStatus == BirthCandidateStatus.Located,
        "새 생 출생 후보 상태");
    True(
        birthContext.Traces.All(trace =>
            trace.EvidenceStatus == PreservationEvidenceStatus.Available),
        "새 윤회 흔적 provenance 근거 상태");
    var completedBirthJourney = birthContextStore.CreateCheckpoint().Journeys.Single();
    Equal(new SimulationEventRecordId(0xC001), completedBirthJourney.RootCausalRecordId, "윤회 root causal record");
    Equal(new SimulationEventRecordId(0xC002), completedBirthJourney.CrossingCausalRecordId, "도하 완료 causal record");
    Equal(new SimulationEventRecordId(0xC003), completedBirthJourney.RebirthCausalRecordId, "환생 causal record");

    var initial = World.Create(8810, 1);
    var firstLife = initial.GetAgentSnapshot(new AgentId(0));
    var birthCheckpoint = initial.CreateCheckpoint();
    var deathTick = GameCalendar.FromYears(150).Value;
    initial.AdvanceTo(new SimulationTick(deathTick - 1));
    var nearDeathCheckpoint = initial.CreateCheckpoint();
    var preparedAgent = birthCheckpoint.Agents[0] with
    {
        BirthTick = 0,
        RealmStage = RealmStage.QiRefining,
        RealmLayer = (byte)MinorStage.Floor1,
        CultivationProgress = 0,
        CultivationState = CultivationState.PreCultivation,
        TechniqueQuality = QualityValue.MaximumHundredths,
        TechniqueMastery = SkillProgressionRules.MaximumHundredths,
        SpellQuality = QualityValue.MaximumHundredths,
        SpellMastery = SkillProgressionRules.MaximumHundredths,
        SkillProgress = new SkillProgressCheckpoint(
            SkillProgressionRules.GetRequiredExperience(SkillProgressionRules.MaximumHundredths),
            SkillProgressionRules.MaximumHundredths,
            SkillProgressionRules.GetRequiredExperience(SkillProgressionRules.MaximumHundredths),
            SkillProgressionRules.MaximumHundredths),
        CombatExperience = 1_000_000,
        CombatProgress = new CombatProgressCheckpoint(QualityValue.MaximumHundredths),
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = deathTick,
        Opponent = -1,
    };
    var world = World.Restore(nearDeathCheckpoint with
    {
        SchemaVersion = 24,
        Agents = [preparedAgent],
        Causal = null,
    });
    var actionsAtDeath = preparedAgent.ActionsTaken;
    world.AdvanceTo(new SimulationTick(deathTick));

    var riverAgent = world.GetAgentSnapshot(new AgentId(0));
    var river = world.GetAgentReincarnationSnapshot(new AgentId(0));
    Equal(150L, riverAgent.AgeYears, "사망 시점 나이");
    Equal(actionsAtDeath, riverAgent.ActionsTaken, "사망 전이에서 행동 횟수 불변");
    True(river.CurrentJourney!.Stage == ReincarnationStage.Entering, "윤회의 강 진입 단계");
    Equal(1, river.Lineage.Count, "첫 사망 시 계보 수");
    Equal(6, river.Traces.Count, "전생 흔적 6영역");
    Equal(
        ReincarnationRules.TraceDomainCount,
        river.Traces.Select(trace => trace.Domain).Distinct().Count(),
        "전생 흔적 영역 중복 없음");
    True(river.Traces.All(trace => trace.HasEvidence), "새 schema 사망은 흔적 근거를 보유");
    True(
        river.Traces.All(trace => trace.AccessibleHundredths == 0
            && trace.IntegratedHundredths == 0
            && trace.PendingAccessibleHundredths <= trace.PreservedHundredths),
        "환생 전 흔적은 접근·통합되지 않음");

    var durationTicks = ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining);
    var boundaryNumerators = new[] { 1, 8, 10, 11, 12 };
    var stageBeforeBoundary = new[]
    {
        ReincarnationStage.Entering,
        ReincarnationStage.Crossing,
        ReincarnationStage.LateCrossing,
        ReincarnationStage.CrossingComplete,
        ReincarnationStage.PreparingRebirth,
    };
    var stageAfterBoundary = new[]
    {
        ReincarnationStage.Crossing,
        ReincarnationStage.LateCrossing,
        ReincarnationStage.CrossingComplete,
        ReincarnationStage.PreparingRebirth,
    };

    for (var boundaryIndex = 0; boundaryIndex < boundaryNumerators.Length; boundaryIndex++)
    {
        var expectedBoundary = checked(
            deathTick + ((durationTicks * boundaryNumerators[boundaryIndex]) + 11) / 12);
        var beforeBoundary = world.GetAgentReincarnationSnapshot(new AgentId(0));
        True(
            beforeBoundary.CurrentJourney!.Stage == stageBeforeBoundary[boundaryIndex],
            $"윤회 {boundaryIndex + 1}차 경계 이전 단계");
        Equal(
            expectedBoundary,
            beforeBoundary.CurrentJourney.NextTransitionTick.Value,
            $"윤회 {boundaryIndex + 1}차 경계 tick");

        Equal(
            expectedBoundary,
            world.GetAgentSnapshot(new AgentId(0)).NextDecisionTick.Value,
            $"윤회 {boundaryIndex + 1}차 Agent schedule 결합");
        world.AdvanceTo(new SimulationTick(expectedBoundary - 1));
        var immediatelyBeforeBoundary = world.GetAgentSnapshot(new AgentId(0));
        True(
            world.GetAgentReincarnationSnapshot(new AgentId(0)).CurrentJourney!.Stage
                == stageBeforeBoundary[boundaryIndex],
            $"윤회 {boundaryIndex + 1}차 경계 직전 단계 불변");
        Equal(actionsAtDeath, immediatelyBeforeBoundary.ActionsTaken, "윤회 경계 직전 행동 불변");
        Equal(150L, immediatelyBeforeBoundary.AgeYears, "윤회 경계 직전 나이 고정");
        if (boundaryIndex == 2)
        {
            Equal(
                0,
                world.GetAgentChronicle(new AgentId(0)).Count(entry =>
                    entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted),
                "도하 완료 경계 직전 Chronicle 없음");
        }

        world.AdvanceTo(new SimulationTick(expectedBoundary));
        var atBoundaryAgent = world.GetAgentSnapshot(new AgentId(0));
        Equal(actionsAtDeath, atBoundaryAgent.ActionsTaken, "윤회 중 행동 횟수 불변");
        Equal(
            boundaryIndex < stageAfterBoundary.Length ? 150L : 0L,
            atBoundaryAgent.AgeYears,
            boundaryIndex < stageAfterBoundary.Length
                ? "윤회 중 나이는 사망 시점에 고정"
                : "환생 완료 시 새 생은 0세");

        if (boundaryIndex < stageAfterBoundary.Length)
        {
            True(
                world.GetAgentReincarnationSnapshot(new AgentId(0)).CurrentJourney!.Stage
                    == stageAfterBoundary[boundaryIndex],
                $"윤회 {boundaryIndex + 1}차 경계 이후 단계");
        }
        if (boundaryIndex == 2)
        {
            var crossingEntry = world.GetAgentChronicle(new AgentId(0)).Single(entry =>
                entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted);
            Equal(expectedBoundary, crossingEntry.Tick.Value, "도하 완료 Chronicle tick");
            Equal(1, crossingEntry.LifeNumber, "도하 완료 Chronicle 원천 생");
        }

        if (boundaryIndex == 0)
        {
            var stateHashBeforeObservation = world.ComputeStateHash();
            using (var runner = new SimulationRunner(world))
            {
                var withoutReincarnation = runner.GetObservation(
                    new AgentId(0),
                    rankingCount: 1,
                    eventCount: 0,
                    sections: ObservationSections.None);
                True(
                    withoutReincarnation.SelectedReincarnation is null,
                    "윤회 탭 비활성 시 투영 생략");
                var withReincarnation = runner.GetObservation(
                    new AgentId(0),
                    rankingCount: 1,
                    eventCount: 0,
                    sections: ObservationSections.Reincarnation);
                True(withReincarnation.SelectedReincarnation is not null, "윤회 탭 활성 시 지연 투영");
            }
            Equal(
                stateHashBeforeObservation,
                world.ComputeStateHash(),
                "윤회 관찰 플래그는 권위 상태·RNG 무영향");

            var middleCheckpoint = world.CreateCheckpoint();
            var restored = World.Restore(WorldSaveCodec.Decode(
                WorldSaveCodec.Encode(middleCheckpoint)));
            Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "윤회 중간 단계 codec·hash 왕복");

            var schema22Checkpoint = WorldSaveCodec.Decode(WorldSaveCodec.Encode(
                middleCheckpoint with
                {
                    SchemaVersion = 22,
                    Reincarnation = null,
                }));
            var migratedSchema22 = World.Restore(schema22Checkpoint);
            var migratedReincarnation = migratedSchema22.GetAgentReincarnationSnapshot(
                new AgentId(0));
            True(migratedReincarnation.IsCompleteLineage, "첫 생 schema 22 이전은 완전 계보");
            Equal(1, migratedReincarnation.Lineage.Count, "schema 22 현재 생 계보 이전");
            Equal(6, migratedReincarnation.Traces.Count, "schema 22 윤회 중 6영역 흔적 생성");
            True(
                migratedReincarnation.Traces.All(trace => !trace.HasEvidence
                    && trace.PreservedHundredths == 0
                    && trace.PendingAccessibleHundredths == 0
                    && trace.AccessibleHundredths == 0
                    && trace.IntegratedHundredths == 0),
                "schema 22에는 없던 전생 근거를 조작하지 않음");
            var migratedCheckpoint = migratedSchema22.CreateCheckpoint();
            Equal(WorldCheckpoint.CurrentSchemaVersion, migratedCheckpoint.SchemaVersion, "schema 22는 재저장 시 현재 schema");
            True(migratedCheckpoint.Reincarnation is not null, "이전 후 윤회 권위 저장 생성");

            world = restored;
        }
    }

    var reborn = world.GetAgentSnapshot(new AgentId(0));
    var continuity = world.GetAgentReincarnationSnapshot(new AgentId(0));
    Equal(2, reborn.LifeNumber, "단계 완료 뒤 새 생 번호");
    Equal(firstLife.Identity.SoulId, reborn.Identity.SoulId, "환생 영혼 ID 연속성");
    True(firstLife.Identity.BodyId != reborn.Identity.BodyId, "환생 육신 ID 교체");
    True(firstLife.Identity.PersonId != reborn.Identity.PersonId, "환생 현생 ID 교체");
    Equal(2, continuity.Lineage.Count, "환생 뒤 두 생 계보");
    True(
        continuity.Lineage[1].PredecessorId == continuity.Lineage[0].Id,
        "새 생 predecessor는 직전 생을 가리킴");
    True(continuity.CurrentJourney is null, "완료된 윤회는 진행 여정에서 제거");
    True(
        continuity.Traces.All(trace => trace.HasEvidence
            && trace.IntegratedHundredths <= trace.AccessibleHundredths
            && trace.AccessibleHundredths <= trace.PreservedHundredths
            && trace.IntegratedHundredths == 0),
        "환생 후 보존·접근·통합 분리 invariant");
    Equal(6, continuity.Traces.Count, "완료 여정의 6영역 흔적 수");
    var cachedContinuity = world.GetAgentReincarnationSnapshot(new AgentId(0));
    True(
        ReferenceEquals(continuity.Lineage, cachedContinuity.Lineage)
        && ReferenceEquals(continuity.Traces, cachedContinuity.Traces),
        "권위 revision 불변 시 계보·흔적 projection 재사용");
    True(
        continuity.Lineage is not LifeLineageSnapshot[]
        && continuity.Traces is not ReincarnationTraceSnapshot[],
        "projection 캐시의 내부 배열 비노출");
    True(reborn.RealmStage == RealmStage.QiRefining, "전생 흔적으로 경지 자동 복구 없음");
    Equal(1, reborn.RealmLayer, "전생 흔적으로 소경지 자동 복구 없음");
    Equal(0, reborn.CultivationProgress, "전생 흔적으로 수행 진척 자동 복구 없음");
    Equal(0, reborn.CombatExperience, "전생 흔적으로 전투 경험 자동 복구 없음");
    Equal(0, reborn.CombatUnderstanding, "전생 흔적으로 전투 이해 자동 복구 없음");
    True(
        reborn.TechniqueMastery < SkillProgressionRules.MaximumHundredths
        && reborn.SpellMastery < SkillProgressionRules.MaximumHundredths,
        "전생 공법·법술 숙련 자동 복구 없음");
    Equal(0, reborn.TechniqueUnderstanding, "전생 공법 이해 자동 복구 없음");
    Equal(0, reborn.SpellUnderstanding, "전생 법술 이해 자동 복구 없음");
    Equal(
        1,
        world.GetAgentChronicle(new AgentId(0)).Count(entry =>
            entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted),
        "윤회의 강 도하 완료 Chronicle 정확히 1회");

    var validCheckpoint = world.CreateCheckpoint();
    var validReincarnation = validCheckpoint.Reincarnation!;
    Equal(1, validReincarnation.Journeys.Length, "첫 윤회 완료 여정 수");
    True(validReincarnation.Journeys[0].CompletedTick is not null, "첫 윤회 완료 tick 보존");
    var validRoundTrip = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(validCheckpoint)));
    Equal(world.ComputeStateHash(), validRoundTrip.ComputeStateHash(), "최종 윤회 checkpoint codec·hash 왕복");

    var legacySecondLife = World.Restore(validCheckpoint with
    {
        SchemaVersion = 22,
        Reincarnation = null,
    }).GetAgentReincarnationSnapshot(new AgentId(0));
    True(!legacySecondLife.IsCompleteLineage, "schema 22 제2생 이전은 불완전 계보");
    Equal(2, legacySecondLife.FirstTrackedLifeNumber, "schema 22 첫 추적 생 번호");
    Equal(1, legacySecondLife.Lineage.Count, "schema 22에 없는 전생 계보를 만들지 않음");
    Equal(0, legacySecondLife.Traces.Count, "schema 22 생존 상태에 없는 전생 흔적을 만들지 않음");

    var invalidLineage = validReincarnation.Lineage.ToArray();
    var secondLifeIndex = Array.FindIndex(invalidLineage, entry => entry.LifeNumber == 2);
    invalidLineage[secondLifeIndex] = invalidLineage[secondLifeIndex] with
    {
        PredecessorId = new LifeRecordId(ulong.MaxValue),
    };
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            Reincarnation = validReincarnation with { Lineage = invalidLineage },
        }),
        "잘못된 윤회 predecessor 저장");

    var invalidTraces = validReincarnation.Traces.ToArray();
    invalidTraces[0] = invalidTraces[0] with
    {
        AccessibleHundredths = 0,
        IntegratedHundredths = 1,
    };
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            Reincarnation = validReincarnation with { Traces = invalidTraces },
        }),
        "통합치가 접근치를 초과한 윤회 흔적 저장");
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            Reincarnation = validReincarnation with { Journeys = [] },
        }),
        "사망 생에 필요한 윤회 여정 누락 저장");
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            Reincarnation = validReincarnation with
            {
                Journeys =
                [
                    validReincarnation.Journeys[0],
                    validReincarnation.Journeys[0],
                ],
            },
        }),
        "중복 윤회 여정 ID 저장");
    var invalidEndKindLineage = validReincarnation.Lineage.ToArray();
    invalidEndKindLineage[0] = invalidEndKindLineage[0] with
    {
        EndKind = (LifeEndKind)byte.MaxValue,
    };
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            Reincarnation = validReincarnation with { Lineage = invalidEndKindLineage },
        }),
        "알 수 없는 생 종료 원인 저장");
    Throws<InvalidDataException>(
        () => World.Restore(validCheckpoint with
        {
            TotalDeaths = checked(validCheckpoint.TotalDeaths + 1),
        }),
        "사망·환생·윤회의 강 집계 불일치 저장");

    var secondDeathTick = checked(
        validCheckpoint.Tick + GameCalendar.FromYears(150).Value);
    var delayedSecondLife = validCheckpoint.Agents[0] with
    {
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = secondDeathTick,
        Opponent = -1,
    };
    var threeLifeWorld = World.Restore(validCheckpoint with
    {
        Agents = [delayedSecondLife],
    });
    threeLifeWorld.AdvanceTo(new SimulationTick(secondDeathTick));
    var secondRiverCheckpoint = threeLifeWorld.CreateCheckpoint();
    var reorderedSecondRiver = secondRiverCheckpoint.Reincarnation! with
    {
        Traces = secondRiverCheckpoint.Reincarnation!.Traces.Reverse().ToArray(),
    };
    threeLifeWorld = World.Restore(secondRiverCheckpoint with
    {
        Reincarnation = reorderedSecondRiver,
    });
    threeLifeWorld.AdvanceTo(new SimulationTick(checked(
        secondDeathTick + ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining))));
    var threeLifeContinuity = threeLifeWorld.GetAgentReincarnationSnapshot(new AgentId(0));
    Equal(3, threeLifeContinuity.Lineage.Count, "두 번 환생 뒤 세 생 계보");
    Equal(12, threeLifeContinuity.Traces.Count, "완료된 두 여정의 6영역 흔적 보존");
    True(
        threeLifeContinuity.Lineage[1].PredecessorId == threeLifeContinuity.Lineage[0].Id
        && threeLifeContinuity.Lineage[2].PredecessorId == threeLifeContinuity.Lineage[1].Id,
        "세 생 predecessor 연쇄");
    Equal(
        2,
        threeLifeContinuity.Traces.Select(trace => trace.JourneyId).Distinct().Count(),
        "두 윤회 여정의 흔적 분리");
}

static void PersonIdentityAndDecisionAuthority()
{
    var world = World.Create(8802, 8);
    var initial = world.GetAgentSnapshot(new AgentId(0));
    Equal(PersonCausalRules.CreateSoulId(0), initial.Identity.SoulId, "안정 영혼 ID");
    Equal(PersonCausalRules.CreateBodyId(0, 1), initial.Identity.BodyId, "첫 육신 ID");
    Equal(PersonCausalRules.CreatePersonId(0, 1), initial.Identity.PersonId, "첫 현생 ID");
    Equal(PersonCausalRules.DecisionContractId, initial.Decision.ContractId, "AI 결정 계약 ID");
    Equal(PersonCausalRules.DecisionContractRevision, initial.Decision.ContractRevision, "AI 결정 계약 revision");
    True(initial.Decision.RequiresReview, "미확정 Utility 다음 버전 재검토 표식");
    True(initial.Decision.Latest.PrimaryGoal == AgentGoalKind.ReachRankOne, "모든 AI 최종 목표는 1위");
    True(initial.Decision.Latest.SupportingGoal == AgentGoalKind.EnterCultivation, "첫 행동의 현재 목표");
    True(initial.Decision.Latest.SelectedActivity == AgentActivity.Cultivating, "첫 행동 선택");

    var beforeRevision = initial.Decision.Latest.AuthorityRevision;
    world.AdvanceTo(initial.NextDecisionTick);
    var committed = world.GetAgentSnapshot(new AgentId(0));
    Equal(
        beforeRevision,
        committed.Decision.Latest.AuthorityRevision,
        "장기 행동 계획 진행 중 decision revision 유지");
    var plan = committed.Cultivation.ActionPlan
        ?? throw new InvalidOperationException("장기 수행 계획 누락");
    world.AdvanceTo(plan.PlannedEndTick);
    var decided = world.GetAgentSnapshot(new AgentId(0));
    True(decided.Decision.Latest.AuthorityRevision > beforeRevision, "계획 경계에서 decision revision 증가");
    True(decided.Decision.Latest.Tick <= world.Tick.Value, "결정 시점은 현재 세계 시간 이하");

    var beforeObservation = world.ComputeStateHash();
    _ = world.GetAgentSnapshot(new AgentId(0));
    Equal(beforeObservation, world.ComputeStateHash(), "행동 이유 관찰은 권위 상태 무영향");

    var checkpoint = world.CreateCheckpoint();
    Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "윤회 계보·Teaching·인과·조직 저장 schema");
    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "인물 신원·결정 codec·hash 왕복");

    var legacy = checkpoint with
    {
        SchemaVersion = 19,
        Agents = checkpoint.Agents.Select(agent => agent with
        {
            Identity = null,
            Decision = null,
        }).ToArray(),
    };
    var migrated = World.Restore(legacy).GetAgentSnapshot(new AgentId(0));
    Equal(PersonCausalRules.CreateSoulId(0), migrated.Identity.SoulId, "schema 19 영혼 ID 결정론 이전");
    Equal(PersonCausalRules.CreateBodyId(0, migrated.LifeNumber), migrated.Identity.BodyId, "schema 19 육신 ID 이전");
    True(migrated.Decision.Latest.PrimaryGoal == AgentGoalKind.ReachRankOne, "schema 19 최종 목표 이전");
    True(migrated.Decision.Latest.AuthorityRevision >= 1, "schema 19 결정 revision 이전");
}

static void AgentCausalLifeVerticalFlow()
{
    var world = World.Create(0xCA05_A116UL, 256);
    var hidden = world.GetAgentSnapshot(new AgentId(0));
    True(hidden.Causal is null, "접힌 인과 UI는 상세 DTO를 만들지 않음");
    True(hidden.LifeAuthority.Gender != GenderIdentity.Undefined, "생별 성별 권위");
    True(hidden.LifeAuthority.SoulState == SoulEmbodimentState.Embodied, "영혼은 육신에 깃듦");
    True(hidden.LifeAuthority.BodyState == BodyLifeState.Living, "현재 육신 생존");
    True(hidden.LifeAuthority.LifePhase == CurrentLifePhase.Living, "현재 생 상태");
    True(hidden.LifeAuthority.Appearance.DefinitionId.Value != 0, "외형 안정 정의 ID");
    True(hidden.LifeAuthority.Appearance.DerivedBeautyHundredths is >= 0 and <= 10_000, "외형 파생값 범위");

    var beforeHash = world.ComputeStateHash();
    var visible = world.GetAgentSnapshot(new AgentId(0), includeCausal: true);
    Equal(beforeHash, world.ComputeStateHash(), "인과·외형 관찰은 권위 상태 불변");
    var causal = visible.Causal!;
    Equal(AgentCausalRules.ContractId, causal.ContractId, "인과 계약 ID");
    Equal(AgentCausalRules.PerceivedFactCapacity, causal.PerceivedFacts.Count, "인지 가능한 사실만 고정 투영");
    Equal(1, causal.CurrentLifeMemories.Count, "탄생 중요 기억");
    True(causal.CurrentLifeMemories[0].Kind == AgentMemoryKind.Birth, "탄생 기억 종류");
    True(causal.CurrentPath.GoalId.Value != 0, "Goal 안정 ID");
    True(causal.CurrentPath.PlanId.Value != 0, "Plan 안정 ID");
    True(causal.CurrentPath.TaskId.Value != 0, "Task 안정 ID");
    True(causal.CurrentPath.ActionId.Value != 0, "Action 안정 ID");
    True(causal.CurrentPath.EventDefinitionId.Value != 0, "Event Definition 안정 ID");
    True(causal.CurrentPath.EventInstanceId.Value != 0, "Event Instance 안정 ID");
    True(causal.CurrentPath.ResultId.Value != 0, "Result 안정 ID");
    True(causal.CurrentPath.EventRecordId.Value != 0, "Event Record 안정 ID");
    Equal(causal.CurrentPath.EventRecordId, causal.CurrentPath.RootRecordId, "첫 판정은 인과 root");
    True(causal.CurrentPath.ParentRecordId is null, "첫 판정 parent 없음");

    world.AdvanceTo(visible.NextDecisionTick);
    var committedSnapshot = world.GetAgentSnapshot(new AgentId(0), includeCausal: true);
    var committedCausal = committedSnapshot.Causal!;
    Equal(
        causal.AuthorityRevision,
        committedCausal.AuthorityRevision,
        "같은 장기 행동 pulse에서 인과 revision 유지");
    var plan = committedSnapshot.Cultivation.ActionPlan
        ?? throw new InvalidOperationException("인과 경로의 수행 계획 누락");
    world.AdvanceTo(plan.PlannedEndTick);
    var after = world.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
    True(after.AuthorityRevision > causal.AuthorityRevision, "계획 경계에서 인과 revision 증가");
    Equal(causal.CurrentPath.RootRecordId, after.CurrentPath.RootRecordId, "같은 생 causal root 유지");
    True(after.CurrentPath.ParentRecordId == causal.CurrentPath.EventRecordId, "직전 record가 parent");
    True(after.CurrentPath.EventRecordId != causal.CurrentPath.EventRecordId, "판정별 record ID 분리");

    world.AdvanceTo(new SimulationTick(80L * GameCalendar.TicksPerYear));
    var bounded = world.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
    True(
        bounded.CurrentLifeMemories.Count <= AgentCausalRules.MemoryCapacityPerAgent,
        "생별 기억 고정 상한");
    True(
        bounded.CurrentLifeMemories.All(memory => memory.ReinforcementCount > 0),
        "반복 경험은 새 로그 대신 기억 강화");

    var requiredResolutions = new[]
    {
        CausalResolutionCode.Committed,
        CausalResolutionCode.Reject,
        CausalResolutionCode.Revalidate,
        CausalResolutionCode.Retry,
        CausalResolutionCode.Defer,
        CausalResolutionCode.AlternativeResult,
        CausalResolutionCode.Compensate,
        CausalResolutionCode.Blocked,
    };
    Equal(8, requiredResolutions.Distinct().Count(), "공통 실패·회복 결과 8종");
    Equal(
        4,
        new[]
        {
            CausalFailureScope.Action,
            CausalFailureScope.Task,
            CausalFailureScope.Plan,
            CausalFailureScope.Goal,
        }.Distinct().Count(),
        "행동·Task·Plan·Goal 실패 범위 분리");
    True(
        world.BrowseAgents(0, 256)
            .Select(agent => agent.LifeAuthority.Gender)
            .All(gender => gender != GenderIdentity.Undefined),
        "모든 인물 성별 명시");

    var checkpoint = world.CreateCheckpoint();
    Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "생애·인과·조직 권위 schema");
    True(checkpoint.Causal is not null, "인과 store 권위 저장");
    True(checkpoint.Agents.All(agent => agent.LifeAuthority is not null), "생애·외형 권위 저장");
    var restored = World.Restore(checkpoint);
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "생애·인과 hash 왕복");
    var restoredCausal = restored.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
    Equal(bounded.CurrentPath, restoredCausal.CurrentPath, "현재 인과 경로 왕복");
    EqualSequence(
        bounded.PerceivedFacts.ToArray(),
        restoredCausal.PerceivedFacts.ToArray(),
        "인지 사실 왕복");
    EqualSequence(
        bounded.CurrentLifeMemories.ToArray(),
        restoredCausal.CurrentLifeMemories.ToArray(),
        "생별 기억 왕복");

    var forgedPaths = checkpoint.Causal!.CurrentPaths.ToArray();
    forgedPaths[0] = forgedPaths[0] with { ActionId = new ActionId(forgedPaths[0].ActionId.Value ^ 1UL) };
    Throws<InvalidDataException>(
        () => World.Restore(checkpoint with
        {
            Causal = checkpoint.Causal with { CurrentPaths = forgedPaths },
        }),
        "위조된 Action 안정 ID 저장 거부");
}

static void SparseDirectionalRelationsAndRoles()
{
    Equal(8, RelationRules.MaximumRelationsPerAgent, "인물당 희소 관계 슬롯 임시 상한");
    Equal(0, RelationRules.DirectTeachingCultivationSpeedBonusBasisPoints, "일반 Teaching 직접 수행속도 0%");
    var initialRoles = new[]
    {
        RelationRole.MasterDisciple,
        RelationRole.Friendship,
        RelationRole.Romance,
        RelationRole.Companion,
        RelationRole.PersistentRivalry,
    };
    Equal(5, initialRoles.Distinct().Count(), "초기 Relation Role 5종");

    var stopwatch = Stopwatch.StartNew();
    var world = World.Create(8803, 500);
    world.AdvanceTo(GameCalendar.FromYears(160));
    stopwatch.Stop();
    var checkpoint = world.CreateCheckpoint();
    var relationStore = checkpoint.Relations;
    True(relationStore is not null, "schema 21 희소 관계 저장 존재");
    True(relationStore!.Relations.Length > 0, "실제 공존·전투에서 관계 생성");
    True(
        relationStore.Relations.Length <= 500 * RelationRules.MaximumRelationsPerAgent / 2,
        "희소 관계 총량 상한");
    var degree = new int[world.Population];
    foreach (var relation in relationStore.Relations)
    {
        degree[relation.LeftAgent]++;
        degree[relation.RightAgent]++;
        Equal(RelationRules.CreateId(relation.LeftAgent, relation.RightAgent), relation.Id, "관계 안정 ID");
        True(RelationRules.IsValidMetrics(relation.LeftTowardRight), "정방향 6축 범위");
        True(RelationRules.IsValidMetrics(relation.RightTowardLeft), "역방향 6축 범위");
    }
    True(degree.All(count => count <= RelationRules.MaximumRelationsPerAgent), "모든 인물 관계 adjacency 상한");
    True(
        relationStore.Relations.Any(relation => relation.Roles != RelationRole.None),
        "실제 반복 상호작용에서 Role 성립");
    True(
        relationStore.Relations.Any(relation => relation.LeftTowardRight != relation.RightTowardLeft),
        "승자·패자 관점의 방향별 6축 분리");
    True(
        relationStore.Relations.SelectMany(relation => relation.History)
            .Any(history => history.Kind == RelationHistoryKind.RoleFormed),
        "중요 Role 성립 역사");
    True(
        relationStore.Relations.SelectMany(relation => relation.History)
            .Any(history => history.Kind == RelationHistoryKind.RoleEndedAtLifeBoundary),
        "사망·윤회 생애 경계 Role 종료 역사");
    True(relationStore.ResultSequence > 0, "관계 typed result 전역 sequence");
    True(
        relationStore.Relations.Any(relation =>
            relation.LatestInteractionResult.InteractionKind == RelationInteractionKind.Dialogue),
        "일반 대화 상호작용 typed result");
    True(
        relationStore.Relations.Any(relation =>
            relation.LatestInteractionResult.Id.Value != 0
            && relation.LatestInteractionResult.Id == RelationRules.CreateResultId(
                relation.Id,
                relation.LatestInteractionResult.Sequence)),
        "관계 결과 안정 ID");
    True(
        relationStore.Relations.Any(relation =>
            relation.LatestInteractionResult.SourceEventRecordId.Value != 0),
        "관계 결과가 AI causal event record를 참조");

    var beforeObservation = world.ComputeStateHash();
    var hidden = world.GetAgentSnapshot(new AgentId(0));
    True(hidden.Relations is null, "접힌 관계 UI DTO 미생성");
    var visible = Enumerable.Range(0, world.Population)
        .Select(index => world.GetAgentSnapshot(new AgentId(index), includeRelations: true))
        .First(agent => agent.Relations is { Count: > 0 });
    True(visible.Relations!.Count <= RelationRules.MaximumRelationsPerAgent, "펼친 관계 UI 희소 DTO");
    True(
        visible.Relations.All(relation =>
            relation.ExternalContext.OrganizationContext == RelationContextAvailability.Available
            && relation.ExternalContext.ReputationContext == RelationContextAvailability.DataUnavailable),
        "조직 맥락은 사용 가능하고 미구현 평판은 DataUnavailable");
    True(
        visible.Relations.All(relation =>
            relation.ExternalContext.FamilyContext is RelationContextAvailability.Available
                or RelationContextAvailability.DataUnavailable),
        "가계 맥락 availability 명시");
    Equal(beforeObservation, world.ComputeStateHash(), "관계 관찰은 권위 상태 무영향");

    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "희소 관계 codec·hash 왕복");
    var legacy = checkpoint with { SchemaVersion = 20, Relations = null };
    Equal(0, World.Restore(legacy).TotalRelations, "schema 20은 빈 희소 관계로 이전");
    Console.WriteLine(
        $"RELATION PERF: 500명·160년 {stopwatch.Elapsed.TotalMilliseconds:N1} ms · " +
        $"관계 {relationStore.Relations.Length:N0}개 · 최대 degree {degree.Max():N0}");
}

static void TeachingVerticalFlow()
{
    Equal(0, RelationRules.DirectTeachingCultivationSpeedBonusBasisPoints, "일반 Teaching 직접 수행속도 0%");
    Equal(
        8,
        Enum.GetValues<TeachingResultKind>().Distinct().Count(),
        "Teaching Typed Result 8종");
    var deterministicRoll = TeachingRules.GetDedicatedRollBasisPoints(
        8804,
        new TeachingProvenanceId(123456789),
        55_000,
        3);
    Equal(
        deterministicRoll,
        TeachingRules.GetDedicatedRollBasisPoints(
            8804,
            new TeachingProvenanceId(123456789),
            55_000,
            3),
        "Teaching 전용 roll 결정론");

    var schema21Source = World.Create(88040, 8).CreateCheckpoint();
    var schema21Agents = schema21Source.Agents
        .Select(agent => agent with { Teaching = null })
        .ToArray();
    var schema21Relation = new RelationStateCheckpoint(
        RelationRules.CreateId(0, 1),
        0,
        1,
        schema21Agents[0].Identity!.Value.PersonId,
        schema21Agents[1].Identity!.Value.PersonId,
        default,
        default,
        RelationRole.None,
        MasterAgent: -1,
        CooperativeInteractions: 0,
        JourneyInteractions: 0,
        CombatInteractions: 0,
        History:
        [
            new RelationHistoryEntry(
                1,
                0,
                RelationHistoryKind.RelationCreated,
                RelationRole.None,
                schema21Agents[0].Identity!.Value.PersonId,
                schema21Agents[1].Identity!.Value.PersonId),
        ]);
    var schema21Checkpoint = schema21Source with
    {
        SchemaVersion = 21,
        Agents = schema21Agents,
        Relations = new RelationStoreCheckpoint(1, [schema21Relation]),
    };
    var schema21Container = RemoveSchema22TeachingProperties(
        WorldSaveCodec.Encode(schema21Checkpoint));
    var schema21Restored = World.Restore(WorldSaveCodec.Decode(schema21Container));
    True(
        schema21Restored.CreateCheckpoint().Agents.All(agent =>
            agent.Teaching is
            {
                AuthorityRevision: 0,
                LearningAuthorityRevision: 0,
                Latest.AuthorityRevision: 0,
                LatestLearning.AuthorityRevision: 0,
            }),
        "schema 21은 빈 Teaching 권위로 이전");
    Equal(1, schema21Restored.TotalRelations, "schema 21 관계 continuity 누락 필드 이전");

    var world = World.Create(8804, 500);
    WorldCheckpoint? checkpoint = null;
    var activeTeacher = -1;
    for (var year = 20; year <= 160 && activeTeacher < 0; year += 10)
    {
        world.AdvanceTo(GameCalendar.FromYears(year));
        checkpoint = world.CreateCheckpoint();
        activeTeacher = Array.FindIndex(
            checkpoint.Agents,
            agent => agent.Activity == AgentActivity.Teaching
                && agent.Teaching is { Latest.IsActive: true });
    }

    True(checkpoint is not null && activeTeacher >= 0, "실제 AI Teaching 행동 예약");
    var source = checkpoint!;
    var teacher = source.Agents[activeTeacher];
    var teaching = teacher.Teaching!.Value;
    var trace = teaching.Latest;
    var learnerIndex = trace.LearnerAgent;
    var learner = source.Agents[learnerIndex];
    True(
        learner.Teaching is { } learning
        && learning.LatestLearning.IsActive
        && learning.LatestLearning.ProvenanceId == trace.ProvenanceId,
        "교사·학습자 Teaching context provenance 연결");
    True(trace.TargetKnowledgeRef.Value != 0 && trace.ProvenanceId.Value != 0, "지식 Ref·provenance 안정 ID");
    True(
        source.Relations!.Relations.Any(relation =>
            relation.Id == trace.RelationId
            && (relation.Roles & RelationRole.MasterDisciple) != 0
            && relation.MasterAgent == activeTeacher),
        "사제 Role 성립");

    var missingMirrorAgents = source.Agents.ToArray();
    var missingMirrorLearnerTeaching = learner.Teaching!.Value;
    missingMirrorAgents[learnerIndex] = learner with
    {
        Teaching = missingMirrorLearnerTeaching with
        {
            LearningAuthorityRevision = 0,
            LatestLearning = TeachingTraceState.Empty,
        },
    };
    Throws<InvalidDataException>(
        () => World.Restore(source with { Agents = missingMirrorAgents }),
        "활성 Teaching 한쪽 mirror 누락 저장 거부");

    var forgedProvenance = new TeachingProvenanceId(trace.ProvenanceId.Value ^ 1UL);
    var forgedAgents = source.Agents.ToArray();
    forgedAgents[activeTeacher] = teacher with
    {
        Teaching = teaching with
        {
            Latest = teaching.Latest with { ProvenanceId = forgedProvenance },
        },
    };
    forgedAgents[learnerIndex] = learner with
    {
        Teaching = missingMirrorLearnerTeaching with
        {
            LatestLearning = missingMirrorLearnerTeaching.LatestLearning with
            {
                ProvenanceId = forgedProvenance,
            },
        },
    };
    Throws<InvalidDataException>(
        () => World.Restore(source with { Agents = forgedAgents }),
        "Teaching provenance 파생값 위조 저장 거부");

    var agents = source.Agents.ToArray();
    var teacherDueTick = teacher.NextDecisionTick;
    var postponedTick = checked(teacherDueTick + GameCalendar.TicksPerYear);
    for (var index = 0; index < agents.Length; index++)
    {
        if (index != activeTeacher
            && agents[index].Activity != AgentActivity.Reincarnating
            && agents[index].NextDecisionTick != long.MaxValue)
        {
            agents[index] = agents[index] with
            {
                NextDecisionTick = Math.Max(agents[index].NextDecisionTick, postponedTick),
            };
        }
    }
    var isolated = World.Restore(source with { Agents = agents });
    True(
        isolated.GetAgentSnapshot(new AgentId(activeTeacher)).Teaching is null,
        "접힌 Teaching UI는 상세 DTO를 생성하지 않음");
    var teacherBefore = isolated.GetAgentSnapshot(
        new AgentId(activeTeacher),
        includeTeaching: true);
    var learnerBefore = isolated.GetAgentSnapshot(
        new AgentId(learnerIndex),
        includeTeaching: true);
    isolated.AdvanceTo(new SimulationTick(teacherDueTick));
    var teacherAfter = isolated.GetAgentSnapshot(
        new AgentId(activeTeacher),
        includeTeaching: true);
    var learnerAfter = isolated.GetAgentSnapshot(
        new AgentId(learnerIndex),
        includeTeaching: true,
        includeCausal: true);
    True(!teacherAfter.Teaching!.Latest.IsActive, "Teaching 결과 정산 완료");
    True(
        teacherAfter.Teaching!.Latest.LastResult is not TeachingResultKind.None,
        "Teaching Typed Result 발생");
    True(
        !string.IsNullOrWhiteSpace(
            teacherAfter.Teaching.LatestParticipants?.TeacherName)
        && !string.IsNullOrWhiteSpace(
            teacherAfter.Teaching.LatestParticipants?.LearnerName),
        "Teaching 상세 DTO 교사·학습자 이름 투영");
    Equal(teacherBefore.CultivationProgress, teacherAfter.CultivationProgress, "교사의 직접 수행 Work 0");
    Equal(learnerBefore.CultivationProgress, learnerAfter.CultivationProgress, "학습자의 직접 수행 Work 0");
    True(
        learnerAfter.TechniqueTrainingExperience != learnerBefore.TechniqueTrainingExperience
        || learnerAfter.SpellTrainingExperience != learnerBefore.SpellTrainingExperience
        || learnerAfter.TechniqueUnderstanding != learnerBefore.TechniqueUnderstanding
        || learnerAfter.SpellUnderstanding != learnerBefore.SpellUnderstanding
        || learnerAfter.Teaching!.TechniqueMisunderstandingPoints
            != learnerBefore.Teaching!.TechniqueMisunderstandingPoints
        || learnerAfter.Teaching!.SpellMisunderstandingPoints
            != learnerBefore.Teaching!.SpellMisunderstandingPoints,
        "Teaching은 숙련 경험·이해 또는 오해 상태를 변경");
    True(
        learnerAfter.Causal!.LatestKnowledgeClaim is { } claim
        && claim.TeachingProvenanceId == trace.ProvenanceId
        && claim.RecipientPersonId == learnerAfter.Identity.PersonId,
        "학습 결과 KnowledgeClaim·provenance·현생 연결");
    True(
        learnerAfter.Causal.CurrentLifeMemories.Any(memory =>
            memory.Kind == AgentMemoryKind.Teaching
            && memory.RelationId == trace.RelationId),
        "Teaching 결과가 관계 provenance를 가진 생별 기억으로 승격");

    var completedCheckpoint = isolated.CreateCheckpoint();
    var completedTrace = teacherAfter.Teaching.Latest;
    var completedRelation = completedCheckpoint.Relations!.Relations.Single(
        relation => relation.Id == completedTrace.RelationId);
    var persistedTeachingSessions = completedTrace.TargetKind == TeachingTargetKind.Technique
        ? completedRelation.TechniqueTeachingSessions
        : completedRelation.SpellTeachingSessions;
    Equal(
        completedTrace.SessionsCompleted,
        persistedTeachingSessions,
        "relation+knowledgeRef 전승 진전 회차 연속성");
    var transmissionHistory = completedCheckpoint.Relations!.Relations
        .SelectMany(relation => relation.History)
        .FirstOrDefault(history => history.Kind is
            RelationHistoryKind.TeachingTransmissionEstablished
            or RelationHistoryKind.TeachingTransmissionUpdated);
    True(
        transmissionHistory.Sequence > 0
        && transmissionHistory.TeachingTarget is not null
        && transmissionHistory.KnowledgeRef.Value != 0
        && transmissionHistory.ProvenanceId.Value != 0,
        "중요 전승 관계 역사는 대상 Ref·provenance를 영구 보존");

    var jointCultivationTick = GameCalendar.GetNextMonthStart(
        new SimulationTick(completedCheckpoint.Tick)).Value;
    var jointAgents = completedCheckpoint.Agents.ToArray();
    for (var index = 0; index < jointAgents.Length; index++)
    {
        if (index == activeTeacher || index == learnerIndex)
        {
            jointAgents[index] = jointAgents[index] with
            {
                Activity = AgentActivity.Cultivating,
                NextDecisionTick = jointCultivationTick,
                Opponent = -1,
                Cultivation = jointAgents[index].Cultivation!.Value with
                {
                    ActionPlan = CultivationActionPlanState.Empty,
                },
            };
        }
        else if (jointAgents[index].Activity != AgentActivity.Reincarnating
            && jointAgents[index].NextDecisionTick != long.MaxValue)
        {
            // 월 경계 랭킹 계약으로 동일 seed에서 선택되는 교사가 자신의 스승도 가진
            // 경우가 있다. 이 검증은 지정한 사제 두 명의 공동 수행만 보는 테스트이므로
            // 다른 수선자의 대기 중 수행 상태를 함께 격리한다.
            jointAgents[index] = jointAgents[index] with
            {
                Activity = jointAgents[index].Activity == AgentActivity.Cultivating
                    ? AgentActivity.TrainingTechnique
                    : jointAgents[index].Activity,
                Cultivation = jointAgents[index].Activity == AgentActivity.Cultivating
                    ? jointAgents[index].Cultivation!.Value with
                    {
                        ActionPlan = CultivationActionPlanState.Empty,
                    }
                    : jointAgents[index].Cultivation,
                NextDecisionTick = Math.Max(
                    jointAgents[index].NextDecisionTick,
                    checked(jointCultivationTick + GameCalendar.TicksPerYear)),
            };
        }
    }

    var jointWorld = World.Restore(completedCheckpoint with { Agents = jointAgents });
    var instructorExperienceBefore = jointWorld.GetAgentSnapshot(
        new AgentId(activeTeacher)).Cultivation.CultivationExperience;
    jointWorld.AdvanceTo(new SimulationTick(jointCultivationTick));
    var instructorCultivation = jointWorld.GetAgentSnapshot(
        new AgentId(activeTeacher)).Cultivation;
    var learnerCultivation = jointWorld.GetAgentSnapshot(
        new AgentId(learnerIndex)).Cultivation;
    var instructorPlan = instructorCultivation.ActionPlan
        ?? throw new InvalidOperationException("사제 공동 수행의 교사 계획 누락");
    var learnerPlan = learnerCultivation.ActionPlan
        ?? throw new InvalidOperationException("사제 공동 수행의 학습자 계획 누락");
    True(
        instructorPlan.TeachingRole == CultivationTeachingRole.Instructor,
        $"공동 수행 교사 역할 · 실제 {instructorPlan.TeachingRole} · 상대 {instructorPlan.TeachingPartner}");
    True(
        instructorPlan.TeachingPartner == new AgentId(learnerIndex),
        "공동 수행 교사 대상");
    True(
        learnerPlan.TeachingRole == CultivationTeachingRole.Learner,
        "공동 수행 학습자 역할");
    True(
        learnerPlan.TeachingPartner == new AgentId(activeTeacher),
        "공동 수행 학습자 스승");
    True(
        learnerPlan.TeachingBonusBasisPoints > CultivationPlanRules.NeutralFactorBasisPoints,
        "스승 지식·경험과 학습자 자질에 따른 수행 흡수 보너스");
    True(
        instructorCultivation.CultivationExperience > instructorExperienceBefore,
        "가르치며 함께 수행한 경험은 교사에게 귀속");

    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(completedCheckpoint)));
    Equal(isolated.ComputeStateHash(), restored.ComputeStateHash(), "Teaching codec·hash 왕복");
    var beforeObservation = isolated.ComputeStateHash();
    _ = isolated.GetAgentSnapshot(
        new AgentId(activeTeacher),
        includeRelations: true,
        includeTeaching: true);
    Equal(beforeObservation, isolated.ComputeStateHash(), "Teaching·관계 관찰 무영향");
}

static void WorldPersonalLogsAndPermanentChronicle()
{
    var world = World.Create(772211, 1);
    var endTick = GameCalendar.GetMonthStart(180).Value - 1;
    world.AdvanceTo(new SimulationTick(endTick));
    var personalLog = world.GetAgentPersonalLog(new AgentId(0));
    True(personalLog.Count <= World.PersonalLogCapacity, "개인 로그 최대 100줄");
    True(
        personalLog.Zip(personalLog.Skip(1), (left, right) => left.Sequence < right.Sequence).All(value => value),
        "개인 로그는 발생 순서대로 유지되어야 합니다.");
    True(personalLog.Any(entry => entry.Kind == PersonalLogKind.ActionCompleted), "개인 행동 로그");
    True(
        personalLog.Any(entry =>
            entry.Kind == PersonalLogKind.ActionCompleted && entry.Value > 1),
        "같은 장기 행동은 로그 행 추가 대신 기간 수치를 갱신");

    var restoredLogWorld = World.Restore(world.CreateCheckpoint());
    True(
        personalLog.SequenceEqual(restoredLogWorld.GetAgentPersonalLog(new AgentId(0))),
        "개인 로그 저장 왕복");

    var busyWorld = World.Create(772212, 2_000);
    busyWorld.AdvanceTo(new SimulationTick(8_000));
    var worldLog = busyWorld.CreateSnapshot(10, 500).RecentEvents;
    True(worldLog.Count <= World.WorldLogCapacity, "세계 로그 최대 100줄");
    True(
        worldLog.All(entry => entry.Kind is not (
            WorldEventKind.CombatStarted
            or WorldEventKind.CombatPhysicalAttack
            or WorldEventKind.CombatSpellCast
            or WorldEventKind.CombatGuarded
            or WorldEventKind.CombatRetreated
            or WorldEventKind.MinorStageBreakthrough
            or WorldEventKind.BreakthroughFailed
            or WorldEventKind.MissionCompleted
            or WorldEventKind.MaterialSold
            or WorldEventKind.MaterialPurchased
            or WorldEventKind.PillPurchased)),
        "전체 로그에는 반복적인 개인 사건이 노출되면 안 됩니다.");

    var chronicleWorld = World.Create(772213, 1);
    var checkpoint = chronicleWorld.CreateCheckpoint();
    var agent = checkpoint.Agents[0];
    var permanentEntries = Enumerable.Range(1, 150)
        .Select(sequence => new LifeChronicleEntry(
            sequence,
            new SimulationTick(0),
            1,
            0,
            agent.Name,
            ChronicleEventKind.FortuitousEncounter,
            RealmStage.QiRefining,
            MinorStage.Floor1,
            sequence))
        .ToArray();
    var permanent = World.Restore(checkpoint with
    {
        ChronicleSequence = permanentEntries.Length,
        Agents = [agent with { Chronicle = permanentEntries }],
    });
    Equal(150, permanent.GetAgentChronicle(new AgentId(0)).Count, "연대기는 100줄로 잘리지 않음");
    var encoded = WorldSaveCodec.Encode(permanent.CreateCheckpoint());
    var loaded = World.Restore(WorldSaveCodec.Decode(encoded));
    Equal(150, loaded.GetAgentChronicle(new AgentId(0)).Count, "영구 연대기 저장 왕복");
}

static void PersonalCenturyChronicleProjectionTest()
{
    var world = World.Create(772214, 1);
    var current = world.GetAgentSnapshot(new AgentId(0)) with { AgeYears = 250 };
    var detailed = new[]
    {
        new LifeChronicleEntry(
            1, new SimulationTick(0), 1, 0, current.Name,
            ChronicleEventKind.Birth, RealmStage.QiRefining, MinorStage.Floor1, 0),
        new LifeChronicleEntry(
            2, new SimulationTick(600), 1, 50, current.Name,
            ChronicleEventKind.MinorStageBreakthrough,
            RealmStage.QiRefining, MinorStage.Floor2, 0),
        new LifeChronicleEntry(
            3, new SimulationTick(1_200), 1, 100, current.Name,
            ChronicleEventKind.MajorRealmBreakthrough,
            RealmStage.FoundationEstablishment, MinorStage.Floor1, 0),
        new LifeChronicleEntry(
            4, new SimulationTick(2_388), 1, 199, current.Name,
            ChronicleEventKind.BreakthroughFailure,
            RealmStage.FoundationEstablishment, MinorStage.Floor5, 1),
        new LifeChronicleEntry(
            5, new SimulationTick(3_000), 1, 250, current.Name,
            ChronicleEventKind.TechniqueAcquired,
            RealmStage.FoundationEstablishment, MinorStage.Floor6, 7_999),
    };

    var volumes = PersonalCenturyChronicleProjection.Create(
        detailed,
        current,
        new SimulationTick(3_000));
    Equal(3, volumes.Count, "0~99·100~199·200~299세 3권");
    True(volumes[0].IsComplete, "첫 100년 종합 연대기 완료");
    Equal(1, volumes[0].GrowthEventCount, "첫 100년 성장 사건 집계");
    True(volumes[1].IsComplete, "둘째 100년 종합 연대기 완료");
    Equal(1, volumes[1].MajorRealmBreakthroughCount, "대경지 돌파 집계");
    Equal(1, volumes[1].BreakthroughFailureCount, "돌파 실패 집계");
    True(!volumes[2].IsComplete, "현재 100년 구간 작성 중");
    Equal(1, volumes[2].AcquisitionEventCount, "획득 사건 집계");
    Equal(5, detailed.Length, "세부 연대기 원본 보존");

    var sparseCurrent = current with { AgeYears = 350 };
    var sparse = PersonalCenturyChronicleProjection.Create(
        [detailed[0], detailed[^1] with { AgeYears = 350 }],
        sparseCurrent,
        new SimulationTick(4_000));
    Equal(4, sparse.Count, "사건이 없는 100년 구간도 역사서 권으로 유지");
    Equal(0, sparse[1].EventCount, "100~199세 빈 권");
    Equal(0, sparse[2].EventCount, "200~299세 빈 권");
    Equal(199L, sparse[1].ObservedThroughAgeYears, "완료된 빈 권 관찰 종료 나이");
    True(sparse[1].IsComplete && sparse[2].IsComplete, "생존한 빈 권 완결");
}

static void KoreanLargeNumberFormatting()
{
    Equal("9,999", KoreanNumberFormatter.Format(9_999), "만 미만 표기");
    Equal("2만", KoreanNumberFormatter.Format(20_000), "만 단위 표기");
    Equal("10만", KoreanNumberFormatter.Format(100_000), "십만 단위 표기");
    Equal("1억", KoreanNumberFormatter.Format(100_000_000), "억 단위 표기");
    Equal("1.23억", KoreanNumberFormatter.Format(123_456_789), "억 소수 표기");
    Equal("1조", KoreanNumberFormatter.Format(1_000_000_000_000), "조 단위 표기");
}

static void QualityTiersAndBoundaryMultipliers()
{
    Equal(8, QualityRules.All.Count, "품질 단계 수");
    True(new QualityValue(999).Tier == QualityTier.Scrap, "9.99 미만 폐품");
    True(new QualityValue(1_000).Tier == QualityTier.ExtremeLow, "10 이상 극하품");
    True(new QualityValue(2_999).Tier == QualityTier.ExtremeLow, "29.99 극하품");
    True(new QualityValue(3_000).Tier == QualityTier.Low, "30 이상 하품");
    True(new QualityValue(3_999).Tier == QualityTier.Low, "39.99 하품");
    True(new QualityValue(4_000).Tier == QualityTier.Medium, "40 이상 중품");
    True(new QualityValue(5_999).Tier == QualityTier.Medium, "59.99 중품");
    True(new QualityValue(6_000).Tier == QualityTier.High, "60 이상 상품");
    True(new QualityValue(7_999).Tier == QualityTier.High, "79.99 상품");
    True(new QualityValue(8_000).Tier == QualityTier.ExtremeHigh, "80 이상 극상품");
    True(new QualityValue(8_999).Tier == QualityTier.ExtremeHigh, "89.99 극상품");
    True(new QualityValue(9_000).Tier == QualityTier.Heaven, "90 이상 천품");
    True(new QualityValue(9_999).Tier == QualityTier.Heaven, "99.99 천품");
    True(new QualityValue(10_000).Tier == QualityTier.Flawless, "100 무결품");

    BoundaryRatio(999, 1_000, 5m, "폐품→극하품");
    BoundaryRatio(2_999, 3_000, 2m, "극하품→하품");
    BoundaryRatio(3_999, 4_000, 1.5m, "하품→중품");
    BoundaryRatio(5_999, 6_000, 1.5m, "중품→상품");
    BoundaryRatio(7_999, 8_000, 2m, "상품→극상품");
    BoundaryRatio(8_999, 9_000, 4m, "극상품→천품");
    BoundaryRatio(9_999, 10_000, 10m, "천품→무결품");
}

static void DetailedAptitudesAuthorityAndLazyProjection()
{
    Equal(6, AptitudeRules.All.Count, "6대 자질 정의 수");
    Equal(60, AptitudeRules.All.Sum(group => group.Components.Count), "세부 자질 총 60개");
    Throws<ArgumentOutOfRangeException>(
        () => AptitudeRules.GetComponentOffset((AptitudeDomain)byte.MaxValue),
        "잘못된 자질 영역 거부");
    Equal(
        60,
        AptitudeRules.All.SelectMany(group => group.Components)
            .Select(component => component.StableId)
            .Distinct(StringComparer.Ordinal)
            .Count(),
        "세부 자질 안정 ID 중복 금지");
    True(
        AptitudeRules.ProvisionalBalances.All(balance => balance.RequiresReview),
        "미확정 세부 자질 분포는 재검토 표식");

    var stopwatch = Stopwatch.StartNew();
    var world = World.CreateDefault(741103);
    var checkpoint = world.CreateCheckpoint();
    stopwatch.Stop();
    Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "윤회 계보·Teaching·인과·조직 저장 schema");

    long summaryTotal = 0;
    var summaryMinimum = int.MaxValue;
    var summaryMaximum = int.MinValue;
    foreach (var agent in checkpoint.Agents)
    {
        var details = agent.AptitudeDetails;
        True(details is not null, "신규 저장 세부 자질 존재");
        Equal(AptitudeRules.FormulaRevision, details!.FormulaRevision, "세부 자질 공식 revision");
        True(details.RequiresReview, "분포 임시값 재검토 상태 저장");
        Equal(AptitudeRules.TotalComponentCount, details.InnateComponents.Length, "Agent 세부 자질 길이");
        for (var domainIndex = 0; domainIndex < AptitudeRules.DomainCount; domainIndex++)
        {
            var domain = (AptitudeDomain)domainIndex;
            var summary = AptitudeRules.CalculateSummaryHundredths(
                details.InnateComponents,
                domain);
            Equal(GetCheckpointAptitudeSummary(agent, domain), summary, "Component 평균과 Summary 일치");
            summaryTotal += summary;
            summaryMinimum = Math.Min(summaryMinimum, summary);
            summaryMaximum = Math.Max(summaryMaximum, summary);
        }
    }

    var summaryMean = summaryTotal / (decimal)(checkpoint.Agents.Length * AptitudeRules.DomainCount);
    True(summaryMean is >= 4_800m and <= 5_200m, "10,000명 세부 자질 평균 중심 구간");
    True(summaryMinimum < 2_000 && summaryMaximum > 8_000, "10,000명 자질 분포 범위");

    var stateHash = world.ComputeStateHash();
    using (var runner = new SimulationRunner(world))
    {
        var hidden = runner.GetObservation(
            new AgentId(0),
            sections: ObservationSections.None);
        True(hidden.SelectedAgent.AptitudeDetails is null, "접힌 세부 자질 UI는 DTO 미생성");
        var visible = runner.GetObservation(
            new AgentId(0),
            sections: ObservationSections.AptitudeDetails);
        Equal(6, visible.SelectedAgent.AptitudeDetails!.Groups.Count, "펼친 세부 자질 UI 6개 내부 탭 DTO");
        True(
            visible.SelectedAgent.AptitudeDetails.Groups.All(group =>
                group.DerivedAbilities.Count == 2
                && group.DerivedAbilities.All(ability =>
                    ability.FormulaText.Contains("30%", StringComparison.Ordinal)
                    && ability.FormulaText.Contains("20%", StringComparison.Ordinal))),
            "각 자질 내부 탭의 수행/전투 파생 능력");
    }
    Equal(stateHash, world.ComputeStateHash(), "세부 자질 관찰은 권위 상태 무영향");

    var encoded = WorldSaveCodec.Encode(checkpoint);
    True(encoded.Length < 64 * 1024 * 1024, "10,000명 세부 자질 저장은 압축 payload 예산 이내");
    var restored = World.Restore(WorldSaveCodec.Decode(encoded));
    Equal(stateHash, restored.ComputeStateHash(), "세부 자질 저장 codec·hash 왕복");

    var legacySource = World.Create(741104, 10).CreateCheckpoint();
    var legacy = legacySource with
    {
        SchemaVersion = 17,
        Agents = legacySource.Agents
            .Select(agent => agent with { AptitudeDetails = null })
            .ToArray(),
    };
    var migrated = World.Restore(legacy);
    var migratedAgent = migrated.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
    foreach (var group in migratedAgent.AptitudeDetails!.Groups)
    {
        True(
            group.Components.All(component => component.InnateValue == group.Summary),
            "schema 17 대표값은 손실 없이 균일 Component로 이전");
    }

    var componentAuthoritySource = World.Create(741105, 1).CreateCheckpoint();
    var highCoreComponents = AptitudeRules.CreateUniformComponents(new Aptitudes(
        QualityValue.FromWhole(50),
        QualityValue.FromWhole(50),
        QualityValue.FromWhole(50),
        QualityValue.FromWhole(50),
        QualityValue.FromWhole(50),
        QualityValue.FromWhole(50)));
    var lowCoreComponents = highCoreComponents.ToArray();
    var boneStart = AptitudeRules.GetComponentOffset(AptitudeDomain.Bone);
    highCoreComponents[boneStart + 1] = 10_000;
    highCoreComponents[boneStart + 6] = 10_000;
    highCoreComponents[boneStart + 2] = 0;
    highCoreComponents[boneStart + 3] = 0;
    lowCoreComponents[boneStart + 1] = 0;
    lowCoreComponents[boneStart + 6] = 0;
    lowCoreComponents[boneStart + 2] = 10_000;
    lowCoreComponents[boneStart + 3] = 10_000;
    var uniformSummaryAgent = WithSynchronizedAptitudeDetails(
        componentAuthoritySource.Agents[0] with
        {
            SpiritualRoot = 5_000,
            Bone = 5_000,
            Constitution = 5_000,
            Comprehension = 5_000,
            Mind = 5_000,
            Soul = 5_000,
        });
    var highCoreAgent = uniformSummaryAgent with
    {
        AptitudeDetails = new AptitudeDetailCheckpoint(
            AptitudeRules.FormulaRevision,
            RequiresReview: true,
            highCoreComponents),
    };
    var lowCoreAgent = uniformSummaryAgent with
    {
        AptitudeDetails = new AptitudeDetailCheckpoint(
            AptitudeRules.FormulaRevision,
            RequiresReview: true,
            lowCoreComponents),
    };
    var highCoreWorld = World.Restore(componentAuthoritySource with { Agents = [highCoreAgent] });
    var lowCoreWorld = World.Restore(componentAuthoritySource with { Agents = [lowCoreAgent] });
    var highCore = highCoreWorld.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
    var lowCore = lowCoreWorld.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
    Equal(highCore.Aptitudes.Bone, lowCore.Aptitudes.Bone, "같은 근골 Summary");
    Console.WriteLine(
        $"APTITUDE COMPONENT AUTHORITY: 공법 역할 " +
        $"{highCore.AptitudeDetails!.RoleScores.TechniqueEffectHundredths}/" +
        $"{lowCore.AptitudeDetails!.RoleScores.TechniqueEffectHundredths} · 보정 " +
        $"{highCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints}/" +
        $"{lowCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints}");
    True(
        highCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints
            > lowCore.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints,
        "실제 공법 보정은 Summary가 아니라 관련 Component 파생 능력을 사용");

    Console.WriteLine(
        $"APTITUDE PERF: 10,000명·60 Component 초기화+checkpoint {stopwatch.Elapsed.TotalMilliseconds:N1} ms · " +
        $"압축 저장 {encoded.Length / 1024d / 1024d:N2} MiB · 평균 {summaryMean / 100m:0.00}");
}

static void SkillMasteryUnderstandingAndImmutableQuality()
{
    Equal(100L, SkillProgressionRules.GetRequiredExperience(10), "숙련 0.10 요구 경험");
    Equal(10_000L, SkillProgressionRules.GetRequiredExperience(100), "숙련 1.00 요구 경험");
    Equal(100_000_000L, SkillProgressionRules.GetRequiredExperience(10_000), "숙련 100.00 요구 경험");
    Equal((ushort)100, SkillProgressionRules.GetMasteryHundredths(10_000), "경험→숙련 제곱근 곡선");
    True(
        SkillProgressionRules.GetRequiredExperience(9_001) - SkillProgressionRules.GetRequiredExperience(9_000)
            > SkillProgressionRules.GetRequiredExperience(1_001) - SkillProgressionRules.GetRequiredExperience(1_000),
        "고숙련 구간의 다음 0.01 요구 경험이 더 커야 합니다.");

    var original = World.Create(991122, 500);
    var checkpoint = original.CreateCheckpoint();
    var originalQualities = checkpoint.Agents.Select(agent => agent.TechniqueQuality).ToArray();
    var prepared = checkpoint.Agents.Select(agent =>
        WithSynchronizedAptitudeDetails(agent with
        {
            Comprehension = 10_000,
            TechniqueMastery = 0,
            Activity = AgentActivity.TrainingTechnique,
            NextDecisionTick = 1,
            SkillProgress = new SkillProgressCheckpoint(0, 0, 0, 0),
        })).ToArray();
    var world = World.Restore(checkpoint with { Agents = prepared });
    world.AdvanceTo(new SimulationTick(1));
    var enlightenmentCount = 0;
    for (var index = 0; index < world.Population; index++)
    {
        var agent = world.GetAgentSnapshot(new AgentId(index));
        Equal(originalQualities[index], agent.TechniqueQuality.Hundredths, "수련·깨달음은 공법 품질 불변");
        True(agent.TechniqueTrainingExperience > 0, "공법 수련 경험 누적");
        True(agent.TechniqueMastery > 0, "누적 경험으로 숙련도 상승");
        True(
            world.GetAgentChronicle(new AgentId(index)).All(item =>
                item.Kind is not ChronicleEventKind.TechniqueEnlightenment
                    and not ChronicleEventKind.SpellEnlightenment),
            "깨달음은 영구 연대기에 기록하지 않아야 합니다.");
        if (world.GetAgentPersonalLog(new AgentId(index))
            .Any(item => item.WorldEventKind == WorldEventKind.TechniqueEnlightenment))
        {
            enlightenmentCount++;
            True(agent.TechniqueUnderstanding > 0, "깨달음은 이해도만 상승");
        }
    }

    True(enlightenmentCount > 0, "다수 수련자 중 공법 깨달음이 발생해야 합니다.");
}

static void BoundaryRatio(int lowerHundredths, int upperHundredths, decimal expected, string message)
{
    var lower = QualityRules.GetEffectiveHundredths(new QualityValue(checked((ushort)lowerHundredths)));
    var upper = QualityRules.GetEffectiveHundredths(new QualityValue(checked((ushort)upperHundredths)));
    var ratio = (decimal)upper / lower;
    True(Math.Abs(ratio - expected) < 0.01m, $"{message} 경계 배율: expected≈{expected}, actual={ratio}");
}

static void CombatPowerAndRanking()
{
    var world = World.CreateDefault(77);
    var snapshot = world.CreateSnapshot(1_000, 0);
    Equal(1_000, snapshot.CombatPowerRankings.Count, "전투력 TOP 1000");
    Equal(1_000, snapshot.RealmRankings.Count, "경지 TOP 1000");
    Equal(1_000, snapshot.AptitudeRankings.Count, "자질 TOP 1000");
    Equal(1_000, snapshot.WealthRankings.Count, "부자 TOP 1000");

    var previousPower = long.MaxValue;
    foreach (var entry in snapshot.CombatPowerRankings)
    {
        True(entry.Category == RankingCategory.CombatPower, "전투력 랭킹 범주");
        Equal(0L, entry.CombatPower.Aptitude, "자질은 직접 전투력에 합산하지 않아야 합니다.");
        Equal(entry.CombatPower.Total, checked(
            entry.CombatPower.Realm
            + entry.CombatPower.Aptitude
            + entry.CombatPower.Technique
            + entry.CombatPower.Spell
            + entry.CombatPower.Experience
            + entry.CombatPower.Treasure), "전투력 합계");
        True(entry.CombatPower.Total <= previousPower, "랭킹은 전투력 내림차순이어야 합니다.");
        previousPower = entry.CombatPower.Total;
    }

    var previousRealmScore = long.MaxValue;
    foreach (var entry in snapshot.RealmRankings)
    {
        True(entry.Category == RankingCategory.Realm, "경지 랭킹 범주");
        True(entry.Score <= previousRealmScore, "경지 랭킹은 대경지·소경지·진척 내림차순이어야 합니다.");
        previousRealmScore = entry.Score;
    }

    var previousAptitude = int.MaxValue;
    foreach (var entry in snapshot.AptitudeRankings)
    {
        True(entry.Category == RankingCategory.Aptitude, "자질 랭킹 범주");
        True(entry.AptitudeScore <= previousAptitude, "자질 랭킹은 6대 자질 합계 내림차순이어야 합니다.");
        previousAptitude = entry.AptitudeScore;
    }

    var previousWealth = long.MaxValue;
    foreach (var entry in snapshot.WealthRankings)
    {
        True(entry.Category == RankingCategory.Wealth, "부자 랭킹 범주");
        True(entry.Score <= previousWealth, "부자 랭킹은 보유 영석 내림차순이어야 합니다.");
        Equal(entry.Rank, world.GetAgentSnapshot(entry.AgentId).WealthRank, "부자 공식 순위");
        previousWealth = entry.Score;
    }
}

static void WealthRankingHistoryAndWorldChronicle()
{
    var world = World.Create(7901, 1_000);
    for (var year = 1; year <= 210; year++)
    {
        world.AdvanceTo(GameCalendar.FromYears(year));
    }

    var snapshot = world.CreateSnapshot(
        1_000,
        100,
        ObservationSections.StatisticsSummary
            | ObservationSections.WealthRanking
            | ObservationSections.WorldChronicle);
    Equal(1_000, snapshot.WealthRankings.Count, "부자 랭킹 조회 크기");
    var windows = snapshot.Statistics.RankingMovementWindows
        ?? Array.Empty<RankingMovementWindowSnapshot>();
    Equal(6 * 7, windows.Count, "6개 기간 × 7개 상위권 구간");
    int[] expectedTiers = [1, 3, 5, 10, 100, 500, 1_000];
    foreach (var period in Enum.GetValues<RankingMovementPeriod>())
    {
        var periodRows = windows.Where(item => item.Period == period).ToArray();
        Equal(expectedTiers.Length, periodRows.Length, $"{period} 구간 수");
        True(periodRows.Select(item => item.TopCount).SequenceEqual(expectedTiers), $"{period} 구간 순서");
        foreach (var row in periodRows)
        {
            True(row.FromTick.Value <= row.ToTick.Value, "랭킹 기간 순서");
            True(row.ComparedCount is >= 1 and <= 1_000, "랭킹 비교 인원 범위");
            True(row.ObservationIntervalCount >= 1, "랭킹 연간 관측 횟수");
            var maximumPositionChanges = checked((long)row.ComparedCount * row.ObservationIntervalCount);
            True(row.CombatPowerChangedCount is >= 0 && row.CombatPowerChangedCount <= maximumPositionChanges, "전투력 누적 자리 변동 범위");
            True(row.RealmChangedCount is >= 0 && row.RealmChangedCount <= maximumPositionChanges, "경지 누적 자리 변동 범위");
            True(row.WealthChangedCount is >= 0 && row.WealthChangedCount <= maximumPositionChanges, "부자 누적 자리 변동 범위");
            True(row.CombatPowerChangeBasisPoints is >= 0 and <= 10_000, "전투력 변동률 범위");
            True(row.RealmChangeBasisPoints is >= 0 and <= 10_000, "경지 변동률 범위");
            True(row.WealthChangeBasisPoints is >= 0 and <= 10_000, "부자 변동률 범위");
        }
    }

    True(
        windows.Any(row => row.ObservationIntervalCount > 1
            && (row.CombatPowerChangedCount > row.ComparedCount
                || row.RealmChangedCount > row.ComparedCount
                || row.WealthChangedCount > row.ComparedCount)),
        "다년 기간 자리 변동은 현재와 기준점의 차이가 아니라 연간 실제 변경 횟수를 누적");

    Equal(2, snapshot.PermanentWorldChronicle.Count, "완료된 100년마다 세계 역사서 한 권");
    var firstCentury = snapshot.PermanentWorldChronicle[0];
    Equal(0L, firstCentury.StartYear, "첫 세계 역사서 시작 연도");
    Equal(99L, firstCentury.EndYear, "첫 세계 역사서 종료 연도");
    True(
        firstCentury.RealmBreakthroughCounts.Sum() > 0,
        "세계 역사서는 대경지별 돌파 횟수를 압축");
    True(
        firstCentury.RealmBreakthroughUniqueCounts
            .Zip(firstCentury.RealmBreakthroughCounts)
            .All(pair => pair.First <= pair.Second),
        "대경지별 고유 돌파 인원은 돌파 횟수를 넘지 않음");
    True(
        firstCentury.CombatPowerPositionChanges.Any(value => value > 0)
            || firstCentury.RealmPositionChanges.Any(value => value > 0)
            || firstCentury.AptitudePositionChanges.Any(value => value > 0)
            || firstCentury.WealthPositionChanges.Any(value => value > 0),
        "세계 역사서는 TOP 1·3·5·10 실제 자리 변경을 압축");
    True(
        !string.IsNullOrWhiteSpace(firstCentury.CombatPowerLeader.Name)
            && !string.IsNullOrWhiteSpace(firstCentury.RealmLeader.Name)
            && !string.IsNullOrWhiteSpace(firstCentury.AptitudeLeader.Name)
            && !string.IsNullOrWhiteSpace(firstCentury.WealthLeader.Name),
        "세계 역사서는 세기 말 각 랭킹 1위를 보존");
    True(
        firstCentury.DeathCount >= firstCentury.UniqueDeathCount
            && firstCentury.ReincarnationEntryCount >= firstCentury.UniqueReincarnationEntryCount
            && firstCentury.RebirthCount >= firstCentury.UniqueRebirthCount,
        "세계 역사서는 사망·윤회·환생 횟수와 고유 인원을 함께 보존");
    var chronicleTail = world.CreateSnapshot(
        rankingCount: 1,
        eventCount: 0,
        sections: ObservationSections.WorldChronicle,
        worldChronicleCount: 5);
    Equal(snapshot.PermanentWorldChronicle.Count, chronicleTail.PermanentWorldChronicleCount, "연대기 전체 저장 수");
    Equal(Math.Min(5, snapshot.PermanentWorldChronicle.Count), chronicleTail.PermanentWorldChronicle.Count, "연대기 UI 꼬리 조회 크기");
    if (chronicleTail.PermanentWorldChronicle.Count > 0)
    {
        Equal(
            snapshot.PermanentWorldChronicle[^1].Sequence,
            chronicleTail.PermanentWorldChronicle[^1].Sequence,
            "연대기 UI 꼬리 조회 최신 사건");
    }
    var checkpoint = world.CreateCheckpoint();
    True(checkpoint.RankingHistory?.Frames is { Length: > 1 }, "기간별 랭킹 이력 저장");
    Equal(0, checkpoint.WorldChronicle?.Length ?? -1, "schema 17은 개인 사건형 구 세계 연대기를 비움");
    True(
        checkpoint.CenturyChronicle?.Entries.Length == snapshot.PermanentWorldChronicle.Count,
        "100년 세계 역사서 저장 수");
    var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "랭킹 이력·세계 연대기 저장 상태 hash");
}

static void AptitudeIndirectCombatBalance()
{
    static Aptitudes UniformAptitudes(int hundredths)
    {
        var value = new QualityValue(checked((ushort)hundredths));
        return new Aptitudes(value, value, value, value, value, value);
    }

    var lowAptitudes = UniformAptitudes(0);
    var neutralAptitudes = UniformAptitudes(5_000);
    var highAptitudes = UniformAptitudes(10_000);
    var lowBonuses = AptitudeCombatBonusFormula.Calculate(lowAptitudes);
    var neutralBonuses = AptitudeCombatBonusFormula.Calculate(neutralAptitudes);
    var highBonuses = AptitudeCombatBonusFormula.Calculate(highAptitudes);
    Equal(5_000, lowBonuses.TechniqueEffectBasisPoints, "최저 자질 공법 효과 하한");
    Equal(5_000, lowBonuses.SpellEffectBasisPoints, "최저 자질 법술 효과 하한");
    Equal(5_000, lowBonuses.CombatMasteryEffectBasisPoints, "최저 자질 전투 숙련 효과 하한");
    Equal(5_000, lowBonuses.CombatUnderstandingEffectBasisPoints, "최저 자질 전투 이해 효과 하한");
    Equal(5_000, lowBonuses.CombatExperienceGainBasisPoints, "최저 자질 전투 경험 획득 하한");
    Equal(10_000, neutralBonuses.TechniqueEffectBasisPoints, "자질 50.00 공법 효과 기준");
    Equal(10_000, neutralBonuses.SpellEffectBasisPoints, "자질 50.00 법술 효과 기준");
    Equal(10_000, neutralBonuses.CombatMasteryEffectBasisPoints, "자질 50.00 전투 숙련 효과 기준");
    Equal(10_000, neutralBonuses.CombatUnderstandingEffectBasisPoints, "자질 50.00 전투 이해 효과 기준");
    Equal(10_000, neutralBonuses.CombatExperienceGainBasisPoints, "자질 50.00 전투 경험 획득 기준");
    Equal(20_000, highBonuses.TechniqueEffectBasisPoints, "최고 자질 공법 효과 상한");
    Equal(20_000, highBonuses.SpellEffectBasisPoints, "최고 자질 법술 효과 상한");
    Equal(20_000, highBonuses.CombatMasteryEffectBasisPoints, "최고 자질 전투 숙련 효과 상한");
    Equal(20_000, highBonuses.CombatUnderstandingEffectBasisPoints, "최고 자질 전투 이해 효과 상한");
    Equal(20_000, highBonuses.CombatExperienceGainBasisPoints, "최고 자질 전투 경험 획득 상한");

    static CombatPowerBreakdown Power(Aptitudes aptitudes) => CombatPowerFormula.Calculate(
        RealmStage.QiRefining,
        1,
        aptitudes,
        QualityValue.FromWhole(50),
        5_000,
        5_000,
        QualityValue.FromWhole(50),
        5_000,
        5_000,
        0,
        0);

    var lowPower = Power(lowAptitudes);
    var neutralPower = Power(neutralAptitudes);
    var highPower = Power(highAptitudes);
    Equal(0L, lowPower.Aptitude, "최저 자질 직접 전투력");
    Equal(0L, neutralPower.Aptitude, "중립 자질 직접 전투력");
    Equal(0L, highPower.Aptitude, "최고 자질 직접 전투력");
    True(highPower.Technique <= (neutralPower.Technique * 2) + 1, "최고 자질 공법 전투력 보정 200% 상한");
    True(highPower.Spell <= (neutralPower.Spell * 2) + 1, "최고 자질 법술 전투력 보정 200% 상한");
    True(lowPower.Technique >= neutralPower.Technique / 2, "최저 자질 공법 전투력 보정 50% 하한");
    True(lowPower.Spell >= neutralPower.Spell / 2, "최저 자질 법술 전투력 보정 50% 하한");

    Equal(1_000, CombatProgressionRules.GetMasteryHundredths(10_000), "전투 경험의 숙련도 변환");
    Equal(50, CombatProgressionRules.ApplyExperienceGain(0, 100, 5_000), "최저 자질 전투 경험 50% 획득");
    Equal(100, CombatProgressionRules.ApplyExperienceGain(0, 100, 10_000), "기준 자질 전투 경험 100% 획득");
    Equal(200, CombatProgressionRules.ApplyExperienceGain(0, 100, 20_000), "최고 자질 전투 경험 200% 획득");
    Equal((ushort)50, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 5_000), "최저 자질 전투 이해 50% 획득");
    Equal((ushort)100, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 10_000), "기준 자질 전투 이해 100% 획득");
    Equal((ushort)200, CombatProgressionRules.ApplyUnderstandingGain(0, 100, 20_000), "최고 자질 전투 이해 200% 획득");
    Equal(50, SkillProgressionRules.ApplyExperienceBonus(100, 5_000), "최저 자질 공법·법술 경험 50% 획득");
    Equal(100, SkillProgressionRules.ApplyExperienceBonus(100, 10_000), "기준 자질 공법·법술 경험 100% 획득");
    Equal(200, SkillProgressionRules.ApplyExperienceBonus(100, 20_000), "최고 자질 공법·법술 경험 200% 획득");
    Equal((ushort)75, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 5_000), "최저 자질 공법·법술 이해 50% 성장");
    Equal((ushort)150, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 10_000), "기준 자질 공법·법술 이해 100% 성장");
    Equal((ushort)300, SkillProgressionRules.ApplyUnderstandingGain(0, 10_000, 20_000), "최고 자질 공법·법술 이해 200% 성장");

    static CombatAttributes Attributes(Aptitudes aptitudes) => CombatAttributeFormula.Calculate(
        RealmStage.QiRefining,
        1,
        aptitudes,
        QualityValue.FromWhole(50),
        5_000,
        5_000,
        QualityValue.FromWhole(50),
        5_000,
        5_000,
        1_000,
        1_000,
        0,
        0);

    var neutralAttributes = Attributes(neutralAptitudes);
    var highAttributes = Attributes(highAptitudes);
    True(highAttributes.Attack > neutralAttributes.Attack, "영근·오성 자질은 공격/법술 효과 보너스");
    True(highAttributes.Defense > neutralAttributes.Defense, "근골·체질 자질은 방어/공법 효과 보너스");
    True(highAttributes.MaximumHealth > neutralAttributes.MaximumHealth, "근골·체질 자질은 최대 생명력 보너스");
    True(highAttributes.MaximumQi > neutralAttributes.MaximumQi, "영근·오성 자질은 최대 영력 보너스");
    True(highAttributes.Judgment > neutralAttributes.Judgment, "오성·정신 자질은 판단력 보너스");
    True(highAttributes.Focus > neutralAttributes.Focus, "오성·정신 자질은 집중력 보너스");
    True(
        highAttributes.CultivationEfficiencyBasisPoints > neutralAttributes.CultivationEfficiencyBasisPoints,
        "영근·오성·정신 자질은 수행 효율 보너스");
}

static void SkillCombatScaleRegression()
{
    var aptitudes = new Aptitudes(
        new QualityValue(5_000),
        new QualityValue(5_000),
        new QualityValue(5_000),
        new QualityValue(5_000),
        new QualityValue(5_000),
        new QualityValue(5_000));
    var flawless = CombatPowerFormula.Calculate(
        RealmStage.QiRefining,
        1,
        aptitudes,
        new QualityValue(10_000),
        1_901,
        0,
        new QualityValue(0),
        0,
        0,
        0,
        0);
    Equal(100L, flawless.Realm, "연기경 1층 전투력 기준값");
    var heaven = CombatPowerFormula.Calculate(
        RealmStage.QiRefining,
        1,
        aptitudes,
        new QualityValue(9_999),
        1_901,
        0,
        new QualityValue(0),
        0,
        0,
        0,
        0);
    True(flawless.Technique < 50_000, "무결품 공법 하나가 연기경에서 억 단위 전투력을 만들면 안 됩니다.");
    True(
        flawless.Technique >= heaven.Technique * 9
            && flawless.Technique <= heaven.Technique * 11,
        "천품→무결품의 약 10배 품질 격차는 축소된 전투 기여에서도 유지되어야 합니다.");

    var attributes = CombatAttributeFormula.Calculate(
        RealmStage.QiRefining,
        1,
        aptitudes,
        new QualityValue(10_000),
        10_000,
        10_000,
        new QualityValue(10_000),
        10_000,
        10_000,
        0,
        0);
    True(attributes.MaximumHealth < 50_000, "무결품 공법의 연기경 최대 체력 폭증 방지");
    True(attributes.Attack < 50_000, "무결품 법술의 연기경 실전 공격력 폭증 방지");
}

static void DeterministicWorld()
{
    var left = World.CreateDefault(20260830);
    var right = World.CreateDefault(20260830);
    Equal(left.ComputeStateHash(), right.ComputeStateHash(), "초기 상태 해시");
    left.AdvanceTo(new SimulationTick(500));
    right.AdvanceTo(new SimulationTick(500));
    Equal(left.ComputeStateHash(), right.ComputeStateHash(), "진행 상태 해시");

    var differentSeed = World.CreateDefault(20260831);
    True(left.ComputeStateHash() != differentSeed.ComputeStateHash(), "다른 seed는 다른 세계를 만들어야 합니다.");
}

static void EventDrivenGrowth()
{
    var world = World.Create(11, 1_000);
    var before = world.ComputeStateHash();
    world.AdvanceTo(new SimulationTick(220_000));
    True(before != world.ComputeStateHash(), "시간 진행 후 상태가 바뀌어야 합니다.");
    True(world.TotalBreakthroughs > 0, "장기 수행에서 돌파가 발생해야 합니다.");
    Equal(220_000L, world.Tick.Value, "도달 tick");
}

static void LongRunCultivationHuntingAndCombat()
{
    var world = World.Create(110011, 1_000);
    world.AdvanceTo(GameCalendar.FromYears(180));
    var entryBlocked = 0;
    var higherRealm = 0;
    var hunters = 0;
    var combatants = 0;
    var combatLearners = 0;
    var overLifespan = 0;
    for (var index = 0; index < world.Population; index++)
    {
        var agent = world.GetAgentSnapshot(new AgentId(index));
        if (agent.CultivationState is CultivationState.PreCultivation or CultivationState.EntryTraining
            || (agent.CultivationState == CultivationState.Bottleneck
                && agent.BottleneckCause == CultivationBottleneckCause.EntryGate))
        {
            entryBlocked++;
        }

        if (agent.RealmStage > RealmStage.QiRefining)
        {
            higherRealm++;
        }

        if (agent.MonsterHunts > 0)
        {
            hunters++;
        }

        if (agent.CombatExperience > 0)
        {
            combatants++;
        }

        if (agent.CombatMastery > 0 && agent.CombatUnderstanding > 0)
        {
            combatLearners++;
        }

        if (agent.AgeYears > agent.MaximumLifespanYears)
        {
            overLifespan++;
        }
    }

    Console.WriteLine(
        $"SOAK: 입문 정체 {entryBlocked:N0} · 축기 이상 {higherRealm:N0} · 사냥 참여 {hunters:N0} · 전투 경험 {combatants:N0} · 사냥 {world.TotalMonsterHunts:N0} · 전투 {world.TotalCombatsCompleted:N0} · 사망 {world.TotalDeaths:N0}");
    True(entryBlocked < world.Population / 2, "장기 세계의 과반이 입문 Gate에 영구 정체되면 안 됩니다.");
    True(higherRealm > 0, "장기 수행에서 축기경 이상 수선자가 나와야 합니다.");
    True(hunters >= world.Population / 5, "요수 사냥 경험이 전체 인구에 의미 있게 퍼져야 합니다.");
    True(combatants >= world.Population / 2, "수선자 간 전투 경험이 전체 인구에 퍼져야 합니다.");
    True(combatLearners >= world.Population / 2, "전투 숙련도와 이해도가 전체 인구에 퍼져야 합니다.");
    True(world.TotalMonsterHunts > 0, "장기 세계에서 요수 사냥이 발생해야 합니다.");
    True(world.TotalCombatsCompleted > 0, "장기 세계에서 수선자 전투가 완료되어야 합니다.");
    Equal(0, overLifespan, "현재 수명을 넘긴 생존자가 관찰되면 안 됩니다.");
}

static void ActiveRankingCompetition()
{
    var topDuel = World.Create(773, 2);
    topDuel.AdvanceTo(GameCalendar.FromYears(30));
    True(topDuel.TotalChallenges > 0, "2위도 1위에게 도전할 수 있어야 합니다.");
    True(topDuel.TotalCombatsStarted > 0, "1·2위만 있는 세계에서도 실제 전투가 시작되어야 합니다.");

    var world = World.CreateDefault(774);
    world.AdvanceTo(new SimulationTick(20_000));
    True(world.TotalChallenges >= 1_000, "충분한 상위권 도전이 발생해야 합니다.");
    True(world.TotalCombatsStarted >= 500, "실제 전투가 지속해서 시작되어야 합니다.");
    True(world.TotalCombatsCompleted >= 400, "실제 전투 결과가 누적되어야 합니다.");
    Equal(world.TotalCombatsStarted, world.TotalCombatsCompleted + world.ActiveCombats, "전투 수 보존");
    True(world.ActiveCombats <= world.Population / 2, "한 AI는 한 전투에만 참여해야 합니다.");

    var totalWins = 0;
    var totalLosses = 0;
    var experiencedAgents = 0;
    for (var index = 0; index < world.Population; index++)
    {
        var agent = world.GetAgentSnapshot(new AgentId(index));
        totalWins += agent.Wins;
        totalLosses += agent.Losses;
        if (agent.CombatExperience > 0)
        {
            experiencedAgents++;
            True(agent.CombatPower.Experience > 0, "전투 경험은 전투력에 반영되어야 합니다.");
        }
    }

    Equal(world.TotalCombatsCompleted, totalWins, "완료 전투와 승리 합계");
    Equal(world.TotalCombatsCompleted, totalLosses, "완료 전투와 패배 합계");
    True(experiencedAgents >= 500, "전투 경험을 얻은 AI가 전 인구에 퍼져야 합니다.");

    var events = world.CreateSnapshot(20, 512).RecentEvents;
    True(events.Any(item => item.Kind == WorldEventKind.CombatEnded), "관찰 사건에 전투 결과가 있어야 합니다.");
}

static void CombatAuthorityAndInjury()
{
    var neutral = QualityValue.FromWhole(50);
    var neutralAptitudes = new Aptitudes(neutral, neutral, neutral, neutral, neutral, neutral);
    var neutralStats = CombatAttributeFormula.Calculate(
        RealmStage.QiRefining,
        1,
        neutralAptitudes,
        neutral,
        5_000,
        0,
        neutral,
        5_000,
        0,
        0,
        0);
    Equal(10_000, neutralStats.CultivationEfficiencyBasisPoints, "선천 수행자질 50.00의 수행 효율 기준");
    True(neutralStats.Attack > 0 && neutralStats.Defense > 0, "실전 공격·방어 속성");
    True(CombatInjuryRules.GetSeverity(0) == InjurySeverity.None, "무부상 경계");
    True(CombatInjuryRules.GetSeverity(5_000) == InjurySeverity.Severe, "중상 경계");
    True(CombatInjuryRules.GetSeverity(10_000) == InjurySeverity.Fatal, "치명상 경계");
    Equal(10_000, CombatInjuryRules.GetFatalChanceBasisPoints(InjurySeverity.Fatal), "치명상 사망 확률");

    var world = World.Create(8128, 1_000);
    world.AdvanceTo(new SimulationTick(20_000));
    var checkpoint = world.CreateCheckpoint();
    var activePairs = checkpoint.Agents
        .Select((agent, index) => (Agent: agent, Index: index))
        .Where(item => item.Agent.Opponent > item.Index)
        .ToArray();
    True(activePairs.Length > 0, "장기간 진행 중인 교대 전투");
    foreach (var pair in activePairs)
    {
        var opponent = checkpoint.Agents[pair.Agent.Opponent];
        Equal(
            pair.Agent.CombatAuthority!.Value.TurnOwner,
            opponent.CombatAuthority!.Value.TurnOwner,
            "양측의 단일 턴 소유자");
        var owner = pair.Agent.CombatAuthority.Value.TurnOwner;
        True(owner == pair.Index || owner == pair.Agent.Opponent, "턴 소유자는 전투 당사자여야 합니다.");
    }

    True(world.TotalDeaths > 0, "부상에 의한 전투 사망 누계");
    True(
        Enumerable.Range(0, world.Population)
            .Any(index => world.GetAgentChronicle(new AgentId(index))
                .Any(entry => entry.Kind == ChronicleEventKind.CombatDeath)),
        "전투 사망 영구 연대기");
}

static void LegacyCombatTurnMigration()
{
    var world = World.Create(8129, 1_000);
    world.AdvanceTo(new SimulationTick(20_000));
    var current = world.CreateCheckpoint();
    True(current.ActiveCombats > 0, "migration fixture 진행 전투");
    var legacyAgents = current.Agents
        .Select(agent => agent with { CombatAuthority = null })
        .ToArray();
    var restored = World.Restore(current with
    {
        SchemaVersion = 9,
        Agents = legacyAgents,
    });
    var migrated = restored.CreateCheckpoint();
    var migratedPairs = migrated.Agents
        .Select((agent, index) => (Agent: agent, Index: index))
        .Where(item => item.Agent.Opponent > item.Index)
        .ToArray();
    True(migratedPairs.Length > 0, "구 저장 진행 전투 쌍 유지");
    foreach (var pair in migratedPairs)
    {
        var opponent = migrated.Agents[pair.Agent.Opponent];
        var owner = pair.Agent.CombatAuthority!.Value.TurnOwner;
        Equal(owner, opponent.CombatAuthority!.Value.TurnOwner, "구 저장 쌍의 공통 turn owner");
        True(owner == pair.Index || owner == pair.Agent.Opponent, "turn owner는 전투 당사자");
        var scheduledCount = (pair.Agent.NextDecisionTick == long.MaxValue ? 0 : 1)
            + (opponent.NextDecisionTick == long.MaxValue ? 0 : 1);
        Equal(1, scheduledCount, "구 저장 전투는 turn owner 하나만 예약");
        True(migrated.Agents[owner].NextDecisionTick != long.MaxValue, "turn owner 예약 유지");
    }

    var nextCombatTick = migratedPairs
        .SelectMany(pair => new[]
        {
            pair.Agent.NextDecisionTick,
            migrated.Agents[pair.Agent.Opponent].NextDecisionTick,
        })
        .Where(tick => tick != long.MaxValue)
        .Min();
    restored.AdvanceTo(new SimulationTick(nextCombatTick));
    Equal(
        restored.TotalCombatsStarted,
        restored.TotalCombatsCompleted + restored.ActiveCombats,
        "구 저장 전투 복구 후 수 보존");
}

static void CombatProgressionSaveAndMigration()
{
    var world = World.Create(8130, 1_000);
    world.AdvanceTo(new SimulationTick(20_000));
    var checkpoint = world.CreateCheckpoint();
    True(checkpoint.SchemaVersion == WorldCheckpoint.CurrentSchemaVersion, "윤회 계보·인과·조직 저장 현재 schema");
    True(
        checkpoint.Agents.Any(agent => agent.CombatProgress is { UnderstandingHundredths: > 0 }),
        "전투 이해도 저장 대상 발생");

    var decoded = WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint));
    True(
        checkpoint.Agents
            .Select(agent => agent.CombatProgress)
            .SequenceEqual(decoded.Agents.Select(agent => agent.CombatProgress)),
        "전투 이해도 codec 왕복");
    var restored = World.Restore(decoded);
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "전투 성장 저장 상태 hash 왕복");

    var legacy = checkpoint with
    {
        SchemaVersion = 12,
        Agents = checkpoint.Agents
            .Select(agent => agent with { CombatProgress = null })
            .ToArray(),
    };
    var migrated = World.Restore(legacy);
    for (var index = 0; index < migrated.Population; index++)
    {
        var agent = migrated.GetAgentSnapshot(new AgentId(index));
        Equal(0, agent.CombatUnderstanding, "schema 12 전투 이해도 기본값");
        Equal(
            (int)CombatProgressionRules.GetMasteryHundredths(agent.CombatExperience),
            agent.CombatMastery,
            "schema 12 기존 경험에서 전투 숙련도 재구성");
    }
}

static void WorldMapAndBiomeMonsters()
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

static void HacheonOcheonGeographyBlueprint()
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

static void AtlasHierarchySpatialKnowledgeAndVeins()
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

static void DeveloperWorldSettingsAndBalancePersistence()
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

static void ObservationQueriesAreReadOnly()
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

static void SelectiveObservationPerformance()
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

static double GetP95(double[] samples)
{
    Array.Sort(samples);
    var index = Math.Min(samples.Length - 1, (int)Math.Ceiling(samples.Length * 0.95) - 1);
    return samples[index];
}

static void LazyMapAgentProjectionPerformance()
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

static void WorldStatisticsQuery()
{
    var world = World.CreateDefault(808);
    world.AdvanceTo(new SimulationTick(2_000));
    var before = world.ComputeStateHash();
    var statistics = world.CreateStatistics();

    Equal(11, statistics.RealmDistribution.Count, "미입문 포함 경지 분포 구간 수");
    Equal(world.Population, statistics.RealmDistribution.Sum(item => item.Count), "경지 분포 전체 인구");
    Equal(world.Population, statistics.CombatPowerDistribution.Sum(item => item.Count), "전투력 분포 전체 인구");
    Equal((long)world.TotalCombatsCompleted, statistics.TotalWins, "통계 승리 수");
    Equal((long)world.TotalCombatsCompleted, statistics.TotalLosses, "통계 패배 수");
    Equal(world.TotalDeaths, statistics.TotalDeaths, "통계 사망 수");
    Equal(
        world.Population,
        statistics.RankingMovement.RisenCount
            + statistics.RankingMovement.FallenCount
            + statistics.RankingMovement.UnchangedCount,
        "랭킹 변동 인구 보존");
    Equal(before, world.ComputeStateHash(), "통계 조회는 세계 상태를 바꾸면 안 됩니다.");
}

static void SaveCodecRoundTrip()
{
    var original = World.CreateDefault(314159);
    original.AdvanceTo(new SimulationTick(2_500));
    var checkpoint = original.CreateCheckpoint();
    var encoded = WorldSaveCodec.Encode(checkpoint);
    var decoded = WorldSaveCodec.Decode(encoded);
    True(
        WorldSaveCodec.StreamingJsonSizeLimitBytes > 512L * 1024 * 1024,
        "장기 세계 슬롯 저장은 기존 512 MiB JSON 상한을 초과해 지원");
    Equal(checkpoint.RandomState, decoded.RandomState, "저장 왕복 RNG 상태");
    Equal(checkpoint.EventSequence, decoded.EventSequence, "저장 왕복 사건 sequence");
    Equal(checkpoint.PersonalLogSequence, decoded.PersonalLogSequence, "저장 왕복 개인 로그 sequence");
    Equal(checkpoint.TotalChallenges, decoded.TotalChallenges, "저장 왕복 도전 합계");
    Equal(checkpoint.TotalMissionsCompleted, decoded.TotalMissionsCompleted, "저장 왕복 임무 합계");
    Equal(checkpoint.LastRankingRefreshTick, decoded.LastRankingRefreshTick, "저장 왕복 랭킹 갱신 시점");
    Equal(checkpoint.RankingRisenCount, decoded.RankingRisenCount, "저장 왕복 순위 상승 집계");
    Equal(checkpoint.Market.TreasurySpiritStones, decoded.Market.TreasurySpiritStones, "저장 왕복 도시 영석");
    Equal(checkpoint.Market.PurchaseTransactions, decoded.Market.PurchaseTransactions, "저장 왕복 도시 구매 거래");
    Equal(checkpoint.Market.MaterialsProcessed, decoded.Market.MaterialsProcessed, "저장 왕복 도시 가공 누계");
    True(checkpoint.Market.Stock.SequenceEqual(decoded.Market.Stock), "저장 왕복 시장 재고");
    True(checkpoint.Market.Prices.SequenceEqual(decoded.Market.Prices), "저장 왕복 시장 가격");
    Equal(checkpoint.Agents[0].Name, decoded.Agents[0].Name, "저장 왕복 첫 Agent 이름");
    Equal(checkpoint.Agents[0].Soul, decoded.Agents[0].Soul, "저장 왕복 첫 Agent 영혼");
    True(
        checkpoint.Agents[0].CombatProgress == decoded.Agents[0].CombatProgress,
        "저장 왕복 첫 Agent 전투 성장");
    True(
        checkpoint.Agents[0].MaterialInventory.SequenceEqual(decoded.Agents[0].MaterialInventory),
        "저장 왕복 첫 Agent 재료");
    True(
        checkpoint.Agents[0].EquippedTreasureQualities.SequenceEqual(decoded.Agents[0].EquippedTreasureQualities),
        "저장 왕복 첫 Agent 법보");
    True(
        checkpoint.Agents[0].Chronicle.SequenceEqual(decoded.Agents[0].Chronicle),
        "저장 왕복 첫 Agent 일대기");
    True(
        checkpoint.Agents[0].PersonalLog.SequenceEqual(decoded.Agents[0].PersonalLog),
        "저장 왕복 첫 Agent 개인 로그");
    Equal(checkpoint.Agents[^1].Name, decoded.Agents[^1].Name, "저장 왕복 마지막 Agent 이름");
    True(
        checkpoint.Agents[^1].Chronicle.SequenceEqual(decoded.Agents[^1].Chronicle),
        "저장 왕복 마지막 Agent 일대기");
    Equal(checkpoint.Events.Length, decoded.Events.Length, "저장 왕복 사건 수");
    Equal(checkpoint.WorldChronicle?.Length ?? 0, decoded.WorldChronicle?.Length ?? 0, "저장 왕복 구 세계 연대기 수");
    Equal(
        checkpoint.CenturyChronicle?.Entries.Length ?? 0,
        decoded.CenturyChronicle?.Entries.Length ?? 0,
        "저장 왕복 100년 세계 역사서 수");
    Equal(
        checkpoint.CenturyChronicle?.Current.CenturyIndex ?? -1,
        decoded.CenturyChronicle?.Current.CenturyIndex ?? -1,
        "저장 왕복 현재 세계 역사서 세기");
    Equal(checkpoint.RankingHistory?.Frames.Length ?? 0, decoded.RankingHistory?.Frames.Length ?? 0, "저장 왕복 랭킹 이력 수");
    if (checkpoint.Events.Length > 0)
    {
        Equal(checkpoint.Events[0], decoded.Events[0], "저장 왕복 첫 사건");
        Equal(checkpoint.Events[^1], decoded.Events[^1], "저장 왕복 마지막 사건");
    }

    var restored = World.Restore(decoded);
    Equal(original.ComputeStateHash(), restored.ComputeStateHash(), "저장 왕복 상태 해시");
    Equal(original.TotalCombatsCompleted, restored.TotalCombatsCompleted, "저장 왕복 완료 전투");
    Equal(original.ActiveCombats, restored.ActiveCombats, "저장 왕복 진행 중 전투");

    original.AdvanceTo(new SimulationTick(3_000));
    restored.AdvanceTo(new SimulationTick(3_000));
    Equal(original.ComputeStateHash(), restored.ComputeStateHash(), "복원 후 계속 진행 상태 해시");
    True(encoded.Length < 8_000_000, "10,000명 저장 파일은 합리적인 크기여야 합니다.");
}

static void CorruptedSaveIsRejected()
{
    var world = World.Create(2718, 250);
    world.AdvanceTo(new SimulationTick(200));
    var encoded = WorldSaveCodec.Encode(world.CreateCheckpoint());
    encoded[^1] ^= 0x5A;
    Throws<InvalidDataException>(() => WorldSaveCodec.Decode(encoded), "checksum 손상 저장");
}

static void AtomicSaveFileRoundTrip()
{
    var root = Path.Combine(Path.GetTempPath(), "ChroniclesTests", Guid.NewGuid().ToString("N"));
    var path = Path.Combine(root, "world.cmip");
    try
    {
        var world = World.Create(1618, 500);
        world.AdvanceTo(new SimulationTick(400));
        WorldSaveStore.Save(path, world.CreateCheckpoint());
        True(File.Exists(path), "원자 저장 결과 파일");
        True(!File.Exists(path + ".tmp"), "임시 저장 파일이 남으면 안 됩니다.");
        var restored = World.Restore(WorldSaveStore.Load(path));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "원자 저장 파일 상태 해시");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

static void BoundedLogFilesNeverExceedFiveMiB()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"cmip-bounded-log-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var lines = Enumerable.Range(0, 2_400)
            .Select(index => $"{index:D4}|{new string('가', 2_200)}")
            .ToArray();
        var export = BoundedLogFileWriter.WriteNewSeries(directory, "manual", lines);
        True(export.FileCount >= 2, "5MiB 초과 수동 로그 분할");
        var exportPaths = Directory.GetFiles(directory, "manual-*.log");
        Equal(export.FileCount, exportPaths.Length, "수동 로그 결과 파일 수");
        True(
            exportPaths.All(path => new FileInfo(path).Length
                <= BoundedLogFileWriter.MaximumFileBytes),
            "수동 로그 파일별 5MiB 상한");
        Equal(
            lines.Length,
            exportPaths.Sum(path => File.ReadLines(path).Count()),
            "수동 로그 행 보존");

        var firstBatch = lines[..1_200];
        var secondBatch = lines[1_200..];
        _ = BoundedLogFileWriter.AppendSeries(directory, "automatic", firstBatch);
        var automatic = BoundedLogFileWriter.AppendSeries(directory, "automatic", secondBatch);
        True(automatic.FileCount >= 1, "자동 로그 append 결과");
        var automaticPaths = Directory.GetFiles(directory, "automatic-*.log");
        True(automaticPaths.Length >= 2, "자동 로그 용량 회전");
        True(
            automaticPaths.All(path => new FileInfo(path).Length
                <= BoundedLogFileWriter.MaximumFileBytes),
            "자동 로그 파일별 5MiB 상한");
        Equal(
            lines.Length,
            automaticPaths.Sum(path => File.ReadLines(path).Count()),
            "자동 로그 행 보존");

        using (var first = File.Create(Path.Combine(directory, "resume-001.log")))
        {
            first.SetLength(BoundedLogFileWriter.MaximumFileBytes - 50);
        }
        using (var second = File.Create(Path.Combine(directory, "resume-002.log")))
        {
            second.SetLength(BoundedLogFileWriter.MaximumFileBytes - 40);
        }

        _ = BoundedLogFileWriter.AppendSeries(
            directory,
            "resume",
            [new string('x', 80)]);
        var resumedPaths = Directory.GetFiles(directory, "resume-*.log");
        True(resumedPaths.Length == 3, "기존의 부분 사용된 다음 part도 잔여 용량을 재검사");
        True(
            resumedPaths.All(path => new FileInfo(path).Length
                <= BoundedLogFileWriter.MaximumFileBytes),
            "재시작 append 뒤에도 파일별 5MiB 상한");

        var oversized = new string('한', checked((int)(
            BoundedLogFileWriter.MaximumFileBytes / 3 + 1_000)));
        var oversizedExport = BoundedLogFileWriter.WriteNewSeries(
            directory,
            "oversized-utf8",
            [oversized]);
        True(oversizedExport.FileCount >= 2, "단일 UTF-8 기록도 rune 경계에서 분할");
        var oversizedPaths = Directory.GetFiles(directory, "oversized-utf8-*.log");
        True(
            oversizedPaths.All(path => new FileInfo(path).Length
                <= BoundedLogFileWriter.MaximumFileBytes),
            "단일 UTF-8 기록 파일별 5MiB 상한");
        True(
            File.ReadLines(oversizedPaths[0]).First().StartsWith("[분할 기록 1] ", StringComparison.Ordinal),
            "분할 기록 연속 표시");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void LogOutputDirectoryContract()
{
    var worldAutomatic = LogOutputDirectories.GetDirectory(new LogOutputTarget(
        LogOutputKind.World,
        LogOutputMode.Automatic));
    True(
        worldAutomatic.EndsWith(
            Path.Combine("logs", "world", "automatic"),
            StringComparison.OrdinalIgnoreCase),
        "세계 자동 로그 폴더");

    var performanceManual = LogOutputDirectories.GetDirectory(new LogOutputTarget(
        LogOutputKind.Performance,
        LogOutputMode.Manual));
    True(
        performanceManual.EndsWith(
            Path.Combine("logs", "performance", "manual"),
            StringComparison.OrdinalIgnoreCase),
        "성능 수동 로그 폴더");

    var agentId = new AgentId(42);
    var personalManual = LogOutputDirectories.GetDirectory(new LogOutputTarget(
        LogOutputKind.Personal,
        LogOutputMode.Manual,
        agentId));
    True(
        personalManual.EndsWith(
            Path.Combine("logs", "personal", agentId.ToString(), "manual"),
            StringComparison.OrdinalIgnoreCase),
        "개인별 수동 로그 폴더");
    Throws<ArgumentException>(
        () => LogOutputDirectories.GetDirectory(new LogOutputTarget(
            LogOutputKind.Personal,
            LogOutputMode.Automatic)),
        "인물 ID 없는 개인 로그 경로 거부");
}

static void DefaultWorldPerformanceBudget()
{
    const long targetTick = 20_000;
    WarmDefaultWorldPerformancePaths(targetTick);
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();

    var totalStopwatch = Stopwatch.StartNew();
    var startupStopwatch = Stopwatch.StartNew();
    var world = World.CreateDefault(20260830);
    startupStopwatch.Stop();
    var allocatedBeforeSimulation = GC.GetTotalAllocatedBytes(precise: true);
    var simulationStopwatch = Stopwatch.StartNew();
    world.AdvanceTo(new SimulationTick(targetTick));
    simulationStopwatch.Stop();
    var simulationAllocatedBytes = checked(
        GC.GetTotalAllocatedBytes(precise: true) - allocatedBeforeSimulation);
    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    GC.WaitForPendingFinalizers();
    var liveHeapBytes = Math.Max(
        0,
        GC.GetGCMemoryInfo().HeapSizeBytes - GC.GetGCMemoryInfo().FragmentedBytes);
    var projectionStopwatch = Stopwatch.StartNew();
    var snapshot = world.CreateSnapshot(150, 150);
    projectionStopwatch.Stop();
    totalStopwatch.Stop();
    var checkpointStopwatch = Stopwatch.StartNew();
    var checkpoint = world.CreateCheckpoint();
    checkpointStopwatch.Stop();
    var restoreStopwatch = Stopwatch.StartNew();
    var restored = World.Restore(checkpoint);
    restoreStopwatch.Stop();
    var cultivationParallel = world.GetCultivationParallelDiagnostics();

    Console.WriteLine(
        $"PERF: {world.Population:N0}명 · 초기화 {startupStopwatch.Elapsed.TotalMilliseconds:N0} ms + " +
        $"{targetTick:N0} tick {simulationStopwatch.Elapsed.TotalMilliseconds:N0} ms + " +
        $"관찰 {projectionStopwatch.Elapsed.TotalMilliseconds:N1} ms = " +
        $"{totalStopwatch.Elapsed.TotalMilliseconds:N0} ms · checkpoint " +
        $"{checkpointStopwatch.Elapsed.TotalMilliseconds:N0} ms · schema {WorldCheckpoint.CurrentSchemaVersion} 복원 " +
        $"{restoreStopwatch.Elapsed.TotalMilliseconds:N0} ms · 수행 후보 " +
        $"{cultivationParallel.EligibleCandidateCount:N0}명 / 병렬 " +
        $"{cultivationParallel.ParallelCandidateCount:N0}명·{cultivationParallel.ParallelBatchCount:N0}묶음 / " +
        $"재검산 {cultivationParallel.FingerprintFallbackCount:N0}명");
    Console.WriteLine(
        $"PERF MEMORY: simulation 할당 {simulationAllocatedBytes / (1024d * 1024d):N2} MiB · " +
        $"tick당 {simulationAllocatedBytes / (double)targetTick:N0} B · " +
        $"강제 GC 후 live heap {liveHeapBytes / (1024d * 1024d):N2} MiB");
    Equal(targetTick, snapshot.Tick.Value, "성능 검증 도달 tick");
    Equal(10_000, snapshot.Population, "성능 검증 인구");
    True(snapshot.TotalCombatsCompleted > 0, "성능 검증 중 전투가 완료되어야 합니다.");
    True(
        totalStopwatch.Elapsed < TimeSpan.FromSeconds(10),
        $"10,000명 시뮬레이션 성능 예산 10초를 초과했습니다: {totalStopwatch.Elapsed.TotalSeconds:N2}초");
    True(
        checkpointStopwatch.Elapsed + restoreStopwatch.Elapsed < TimeSpan.FromSeconds(5),
        $"10,000명 schema {WorldCheckpoint.CurrentSchemaVersion} checkpoint·복원 성능 예산 5초를 초과했습니다.");
    Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "10,000명 현재 schema 복원 hash");
}

static void WarmDefaultWorldPerformancePaths(long targetTick)
{
    // 동일한 10,000명 작업집합을 한 번 통과시켜 tiered JIT/PGO가 정상 상태에
    // 도달하게 한다. 이 시간은 별도 cold-start 비용이며 steady-state budget에
    // 섞지 않는다.
    var warmup = World.CreateDefault(0x23_00_23UL);
    warmup.AdvanceTo(new SimulationTick(targetTick));
    _ = warmup.CreateSnapshot(16, 16);
    var checkpoint = warmup.CreateCheckpoint();
    _ = World.Restore(checkpoint).ComputeStateHash();
}

static byte[] RemoveSchema22TeachingProperties(byte[] container)
{
    using var input = new MemoryStream(container, writable: false);
    using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
    var magic = reader.ReadBytes(4);
    var containerVersion = reader.ReadInt32();
    var payloadLength = reader.ReadInt32();
    _ = reader.ReadBytes(32);
    var payload = reader.ReadBytes(payloadLength);

    using var compressed = new MemoryStream(payload, writable: false);
    using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
    using var jsonBuffer = new MemoryStream();
    decompressor.CopyTo(jsonBuffer);
    var root = JsonNode.Parse(jsonBuffer.ToArray())!.AsObject();
    foreach (var agentNode in root["agents"]!.AsArray())
    {
        agentNode!.AsObject().Remove("teaching");
    }

    foreach (var relationNode in root["relations"]!["relations"]!.AsArray())
    {
        var relation = relationNode!.AsObject();
        relation.Remove("techniqueTeachingSessions");
        relation.Remove("spellTeachingSessions");
        relation.Remove("techniqueTransmissionEstablished");
        relation.Remove("spellTransmissionEstablished");
        relation.Remove("techniqueKnowledgeRef");
        relation.Remove("spellKnowledgeRef");
        foreach (var historyNode in relation["history"]!.AsArray())
        {
            var history = historyNode!.AsObject();
            history.Remove("teachingTarget");
            history.Remove("knowledgeRef");
            history.Remove("provenanceId");
        }
    }

    var migratedJson = Encoding.UTF8.GetBytes(root.ToJsonString());
    using var payloadBuffer = new MemoryStream();
    using (var compressor = new BrotliStream(
        payloadBuffer,
        CompressionLevel.Optimal,
        leaveOpen: true))
    {
        compressor.Write(migratedJson);
    }

    var migratedPayload = payloadBuffer.ToArray();
    var checksum = SHA256.HashData(migratedPayload);
    using var output = new MemoryStream();
    using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
    writer.Write(magic);
    writer.Write(containerVersion);
    writer.Write(migratedPayload.Length);
    writer.Write(checksum);
    writer.Write(migratedPayload);
    writer.Flush();
    return output.ToArray();
}

static AgentCheckpoint WithSynchronizedAptitudeDetails(AgentCheckpoint agent)
{
    var aptitudes = new Aptitudes(
        new QualityValue(agent.SpiritualRoot),
        new QualityValue(agent.Bone),
        new QualityValue(agent.Constitution),
        new QualityValue(agent.Comprehension),
        new QualityValue(agent.Mind),
        new QualityValue(agent.Soul));
    return agent with
    {
        AptitudeDetails = new AptitudeDetailCheckpoint(
            AptitudeRules.FormulaRevision,
            RequiresReview: true,
            AptitudeRules.CreateUniformComponents(aptitudes)),
    };
}

static int GetCheckpointAptitudeSummary(
    AgentCheckpoint agent,
    AptitudeDomain domain) => domain switch
{
    AptitudeDomain.SpiritualRoot => agent.SpiritualRoot,
    AptitudeDomain.Bone => agent.Bone,
    AptitudeDomain.Constitution => agent.Constitution,
    AptitudeDomain.Comprehension => agent.Comprehension,
    AptitudeDomain.Mind => agent.Mind,
    AptitudeDomain.Soul => agent.Soul,
    _ => throw new InvalidOperationException("알 수 없는 선천자질 영역입니다."),
};

static void EqualSequence<T>(T[]? expected, T[]? actual, string message)
{
    if (expected is null || actual is null)
    {
        True(expected is null && actual is null, message);
        return;
    }

    True(expected.SequenceEqual(actual), message);
}

static void Equal<T>(T expected, T actual, string message)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
    }
}

static void True(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Throws<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{message}: {typeof(TException).Name}이 발생해야 합니다.");
}
