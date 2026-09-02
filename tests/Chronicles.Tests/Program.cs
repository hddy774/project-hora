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
        WriteCandidateFailures(load.Failures);
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
        WriteCandidateFailures(source.Failures);
        return 1;
    }

    var expanded = World.Restore(source.Checkpoint!, Array.Empty<AgentId>());
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
        expanded.AdvanceTo(new SimulationTick(checked(
            expanded.Tick.Value + GameCalendar.FromYears(advanceYears).Value)));
    }
    var checkpoint = expanded.CreateCheckpoint();

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
            $"{saved.PayloadBytes:N0} bytes · split v{saved.StorageFormatVersion} {saved.DataPartCount:N0} parts · save/load " +
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

static void WriteCandidateFailures(IReadOnlyList<SlotCandidateFailure>? failures)
{
    if (failures is null)
    {
        return;
    }
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"  {failure.Kind}: {failure.Status} · {failure.Reason}");
    }
}

var tests = new (string Name, Action Body)[]
{
    ("기본 인구와 목표 순위", WorldPersistenceTests.DefaultPopulationAndGoal),
    ("공식 Registry·typed command·원자 transaction·causal event", AuthorityFoundationTests.Run),
    ("게임 시간·배속·경지 수명", CultivationRulesTests.GameTimeAndRealmRules),
    ("자질 50.00 대경지 1층→9층 대원만 전체 기준", CultivationRulesTests.CultivationAuthorityRules),
    ("대경지별 5단계 세부 수행 진척·지연 관찰", CultivationRulesTests.CultivationDetailProgressProjection),
    ("승인 Effect 0개·Typed 실패·수행 외부 계수 계약", CultivationAttemptContractTests.Run),
    ("입문 Gate 병목 단련·적용 Work·저장 왕복", CultivationRulesTests.EntryGateRecoveryAndAppliedWork),
    ("년월일 달력과 월 1회 행동 제한", CultivationRulesTests.CalendarAndMonthlyActionLimit),
    ("Notion 수행 Work·준비·Preflight·안착 상태", CultivationRulesTests.CultivationStateMachine),
    ("장기 수행 계획·조건·경량 pulse·저장 왕복", CultivationRulesTests.LongCultivationPlanConditionsAndPersistence),
    ("고경지 대형 체력 수행 조건 산술 안전성", CultivationRulesTests.HighRealmHealthRatioDoesNotOverflow),
    ("9층 소경지·대원만 분리와 수행 Formula Trace", CultivationRulesTests.FloorNineGreatPerfectionAndFormulaTrace),
    ("소경지·대경지 돌파 영구 연대기", CultivationRulesTests.CultivationBreakthroughChronicle),
    ("한국식 큰 수 단위 표기", CombatProgressionTests.KoreanLargeNumberFormatting),
    ("8단계 품질 경계와 격차 배율", CombatProgressionTests.QualityTiersAndBoundaryMultipliers),
    ("6대 자질 60개 Component·대표값·지연 조회·저장 이전", CombatProgressionTests.DetailedAptitudesAuthorityAndLazyProjection),
    ("공법·법술 경험·숙련·이해·품질 불변", CombatProgressionTests.SkillMasteryUnderstandingAndImmutableQuality),
    ("0세 탄생·6대 자질·일대기·윤회", LifeReincarnationTests.BirthAptitudesChronicleAndReincarnation),
    ("영혼·육신·현생 신원과 AI 행동 이유 권위", LifeReincarnationTests.PersonIdentityAndDecisionAuthority),
    ("Goal→Plan→Task→Action 인과·인지·생별 기억·외형 권위", LifeReincarnationTests.AgentCausalLifeVerticalFlow),
    ("희소 방향 관계 6축·초기 Role·생애 경계", RelationChronicleTests.SparseDirectionalRelationsAndRoles),
    ("사제 성립·Teaching 결과·전승 provenance·직접 수행 0%", RelationChronicleTests.TeachingVerticalFlow),
    ("세계 로그·개인 로그·관심별 영구 연대기 보존", RelationChronicleTests.WorldPersonalLogsAndPermanentChronicle),
    ("개인 로그 링·세부 100건·100년 종합 100권", AgentHistoryStoreTests.Run),
    ("개인 생별 100년 종합 연대기 legacy 투영", RelationChronicleTests.PersonalCenturyChronicleProjectionTest),
    ("전투력 구성 요소와 랭킹", CombatProgressionTests.CombatPowerAndRanking),
    ("랭킹 범주별 revision cache·무변경 정렬 0회", WorldPersistenceTests.RankingRevisionCacheAvoidsUnchangedSorts),
    ("부자 TOP 1000·기간별 상위권 변동·세계 연대기", CombatProgressionTests.WealthRankingHistoryAndWorldChronicle),
    ("자질 직접 전투력 제거·공법 법술·실전 속성 간접 보정", CombatProgressionTests.AptitudeIndirectCombatBalance),
    ("공법·법술·법보 전투 스케일 상한과 품질 격차", CombatProgressionTests.SkillCombatScaleRegression),
    ("동일 seed 결정론", WorldPersistenceTests.DeterministicWorld),
    ("AdvanceTo 호출 분할·runtime pulse·checkpoint 결정론", AdvancePartitionInvarianceTests.Run),
    ("5 seed 장기 수행 직렬·2단계 병렬 결정론", CultivationParallelDeterminismTests.Run),
    ("10,000명 수행 직렬·2단계 병렬 처리량", CultivationParallelThroughputTests.Run),
    ("이벤트 기반 성장", WorldPersistenceTests.EventDrivenGrowth),
    ("180년 장기 수행·사냥·전투 참여 회귀", WorldPersistenceTests.LongRunCultivationHuntingAndCombat),
    ("활발한 랭킹 경쟁과 전투 경험", WorldPersistenceTests.ActiveRankingCompetition),
    ("도시 임무·영석·재료·단약·법보 경제 순환", EconomyMarketTests.EconomyLoop),
    ("재료 부족 상품이 다른 도시 생산을 막지 않음", EconomyMarketTests.ProductionSkipsGoodsWithoutResources),
    ("시장 원자 정산·중복 source·품질 개선 구매", EconomyMarketTests.MarketSettlementAtomicity),
    ("시장 Offer→Reservation→만료·원자 체결", EconomyMarketTests.MarketOfferReservationLifecycle),
    ("시장 소유자 용량 초과 원자 거부", EconomyMarketTests.MarketOwnerCapacityGuard),
    ("최근 12개월 수급 창·관찰 snapshot 재사용", EconomyMarketTests.MarketFlowWindowAndSnapshotCache),
    ("schema 24 시장 수급·원장 codec·source 재생 방지", EconomyMarketTests.MarketSchema24CodecAndSourceReplay),
    ("광맥 0 보존·손상 도시 생산 저장 거부", EconomyMarketTests.MarketCheckpointValidation),
    ("전체 분포·랭킹 변동·승패·사망 통계", WorldPersistenceTests.WorldStatisticsQuery),
    ("관찰 조회 무영향", AtlasObservationTests.ObservationQueriesAreReadOnly),
    ("활성 UI 구역 선택 조회 성능", AtlasObservationTests.SelectiveObservationPerformance),
    ("비인물 화면 선택 인물 투영 0회", AtlasObservationTests.NonPersonWorkspaceSkipsAgentProjection),
    ("지도 내부 탭 지연 투영 성능", AtlasObservationTests.LazyMapAgentProjectionPerformance),
    ("5층천 Atlas·1·2층천·900 영맥·개인 공간 지식", AtlasObservationTests.AtlasHierarchySpatialKnowledgeAndVeins),
    ("하천대륙 10×10 주·오천주 1·2층천 10×10 지점", AtlasObservationTests.HacheonOcheonGeographyBlueprint),
    ("설정용 AI 수·개발자 밸런스 권위·저장 왕복", AtlasObservationTests.DeveloperWorldSettingsAndBalancePersistence),
    ("관찰 UI 상태 분리·관심 목록 지연 조회", ObserverUiContractTests.Run),
    ("조직 Core·창설 Gate·중복 소속·승계·병합·schema 25→26", OrganizationCoreTests.Run),
    ("콘텐츠 Definition·Instance·Provenance·발견·원자 소유권", ContentAuthorityTests.Run),
    ("서적 Content·Edition·실물·접근·오해·교정 권위", KnowledgeAuthorityTests.Run),
    ("법칙 후보·개인 이해·세계 AI·천도 REVIEW 권위", LawAuthorityTests.Run),
    ("절차 생성 아이템·서적·지도·요수·지속 세계 카탈로그", WorldGenerationTests.Run),
    ("저장 codec 결정론 왕복", WorldPersistenceTests.SaveCodecRoundTrip),
    ("손상 저장 거부", WorldPersistenceTests.CorruptedSaveIsRejected),
    ("원자 저장 파일 왕복", WorldPersistenceTests.AtomicSaveFileRoundTrip),
    ("schema 25→26 SQLite 다중 슬롯·후보 원자 복원", SqliteSlotPersistenceTests.Run),
    ("저장 실패 뒤 world·일시정지 보존과 다른 슬롯 재저장", SaveFailureRecoveryTests.Run),
    ("세계 자동 출력이 켜진 동안만 runtime feed 보관", RuntimeWorldFeedCaptureTests.Run),
    ("모든 로그 파일 5MiB 상한·기존 part 재검사·UTF-8 분할", WorldPersistenceTests.BoundedLogFilesNeverExceedFiveMiB),
    ("로그 종류·자동 수동·인물별 출력 경로 계약", WorldPersistenceTests.LogOutputDirectoryContract),
    ("10,000명 로컬 실시간 성능 예산", WorldPersistenceTests.DefaultWorldPerformanceBudget),
    ("100배속 runtime pulse SOAK", RuntimeHundredSpeedSoakTests.Run),
    ("runtime lock-free shell·busy 관찰 프레임 생략", RuntimeObservationSchedulingTests.Run),
};

tests =
[
    .. tests,
    ("교대 전투 속성과 누적 부상 사망", CombatProgressionTests.CombatAuthorityAndInjury),
    ("schema 7~9 진행 전투 단일 turn owner 복구", CombatProgressionTests.LegacyCombatTurnMigration),
    ("전투 숙련·이해 저장과 schema 12 기본값 이전", CombatProgressionTests.CombatProgressionSaveAndMigration),
    ("3×3 지도와 지형별 요수 생태", AtlasObservationTests.WorldMapAndBiomeMonsters),
    ("단계형 윤회·계보·6영역 흔적·schema 22 이전", LifeReincarnationTests.StagedReincarnationLineageTraceAndMigration),
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
