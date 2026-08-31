using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;
using System.Diagnostics;

if (args.Contains("--verify-local-save", StringComparer.Ordinal))
{
    var path = WorldSaveStore.DefaultPath;
    var checkpoint = WorldSaveStore.Load(path);
    var restored = World.Restore(checkpoint);
    var snapshot = restored.CreateSnapshot(10, 10);
    Console.WriteLine(
        $"LOCAL SAVE OK: schema {checkpoint.SchemaVersion} -> {WorldCheckpoint.CurrentSchemaVersion} · " +
        $"{snapshot.Population:N0}명 · {GameCalendar.GetDate(snapshot.Tick)} · " +
        $"시장 {snapshot.Market.Goods.Count:N0}종");
    return 0;
}

var tests = new (string Name, Action Body)[]
{
    ("기본 인구와 목표 순위", DefaultPopulationAndGoal),
    ("게임 시간·배속·경지 수명", GameTimeAndRealmRules),
    ("자질 50.00 대경지 1층→9층 대원만 전체 기준", CultivationAuthorityRules),
    ("입문 Gate 병목 단련·적용 Work·저장 왕복", EntryGateRecoveryAndAppliedWork),
    ("년월일 달력과 월 1회 행동 제한", CalendarAndMonthlyActionLimit),
    ("Notion 수행 Work·준비·Preflight·안착 상태", CultivationStateMachine),
    ("소경지·대경지 돌파 영구 연대기", CultivationBreakthroughChronicle),
    ("한국식 큰 수 단위 표기", KoreanLargeNumberFormatting),
    ("8단계 품질 경계와 격차 배율", QualityTiersAndBoundaryMultipliers),
    ("공법·법술 경험·숙련·이해·품질 불변", SkillMasteryUnderstandingAndImmutableQuality),
    ("0세 탄생·6대 자질·일대기·윤회", BirthAptitudesChronicleAndReincarnation),
    ("세계 중요 로그·개인 100줄 로그·영구 연대기", WorldPersonalLogsAndPermanentChronicle),
    ("전투력 구성 요소와 랭킹", CombatPowerAndRanking),
    ("부자 TOP 1000·기간별 상위권 변동·세계 연대기", WealthRankingHistoryAndWorldChronicle),
    ("자질 직접 전투력 제거·공법 법술·실전 속성 간접 보정", AptitudeIndirectCombatBalance),
    ("공법·법술·법보 전투 스케일 상한과 품질 격차", SkillCombatScaleRegression),
    ("동일 seed 결정론", DeterministicWorld),
    ("이벤트 기반 성장", EventDrivenGrowth),
    ("180년 장기 수행·사냥·전투 참여 회귀", LongRunCultivationHuntingAndCombat),
    ("활발한 랭킹 경쟁과 전투 경험", ActiveRankingCompetition),
    ("도시 임무·영석·재료·단약·법보 경제 순환", EconomyLoop),
    ("재료 부족 상품이 다른 도시 생산을 막지 않음", ProductionSkipsGoodsWithoutResources),
    ("전체 분포·랭킹 변동·승패·사망 통계", WorldStatisticsQuery),
    ("관찰 조회 무영향", ObservationQueriesAreReadOnly),
    ("활성 UI 구역 선택 조회 성능", SelectiveObservationPerformance),
    ("지도 내부 탭 지연 투영 성능", LazyMapAgentProjectionPerformance),
    ("저장 codec 결정론 왕복", SaveCodecRoundTrip),
    ("손상 저장 거부", CorruptedSaveIsRejected),
    ("원자 저장 파일 왕복", AtomicSaveFileRoundTrip),
    ("10,000명 로컬 실시간 성능 예산", DefaultWorldPerformanceBudget),
};

tests =
[
    .. tests,
    ("교대 전투 속성과 누적 부상 사망", CombatAuthorityAndInjury),
    ("schema 7~9 진행 전투 단일 turn owner 복구", LegacyCombatTurnMigration),
    ("전투 숙련·이해 저장과 schema 12 기본값 이전", CombatProgressionSaveAndMigration),
    ("3×3 지도와 지형별 요수 생태", WorldMapAndBiomeMonsters),
];

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
}

static void EntryGateRecoveryAndAppliedWork()
{
    var blockedWorld = World.Create(551120, 1);
    var blockedCheckpoint = blockedWorld.CreateCheckpoint();
    var blockedAgent = blockedCheckpoint.Agents[0] with
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
    };
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
    var cappedAgent = cappedCheckpoint.Agents[0] with
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
    };
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

static void CultivationBreakthroughChronicle()
{
    var minorWorld = World.Create(444001, 1);
    var checkpoint = minorWorld.CreateCheckpoint();
    var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 1);
    var minorAgent = checkpoint.Agents[0] with
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
    };
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
    var majorAgent = majorCheckpoint.Agents[0] with
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
        Health = majorCheckpoint.Agents[0].MaximumHealth,
        Qi = majorCheckpoint.Agents[0].MaximumQi,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = 1,
        Cultivation = majorCultivation,
    };
    majorWorld = World.Restore(majorCheckpoint with { Agents = [majorAgent] });
    for (var month = 1; month <= 36
        && majorWorld.GetAgentSnapshot(new AgentId(0)).RealmStage == RealmStage.QiRefining; month++)
    {
        majorWorld.AdvanceTo(GameCalendar.GetMonthStart(month));
    }

    True(
        majorWorld.GetAgentSnapshot(new AgentId(0)).RealmStage == RealmStage.FoundationEstablishment,
        "연기→축기 대경지 돌파");
    True(
        majorWorld.GetAgentChronicle(new AgentId(0))
            .Any(item => item.Kind == ChronicleEventKind.MajorRealmBreakthrough),
        "대경지 돌파 영구 연대기");
}

static void BirthAptitudesChronicleAndReincarnation()
{
    var initial = World.Create(8801, 1);
    var initialAgent = initial.GetAgentSnapshot(new AgentId(0));
    var initialSoul = initialAgent.Aptitudes.Soul;
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
        Agents = [preparedAgent],
    };
    var world = World.Restore(prepared);
    world.AdvanceTo(new SimulationTick(deathTick));
    var dead = world.GetAgentSnapshot(new AgentId(0));
    Equal(150L, dead.AgeYears, "연기경 최대 수명 도달");
    True(dead.Activity == AgentActivity.Reincarnating, "윤회의 강 상태");
    Equal(1, world.TotalDeaths, "사망 집계");

    world.AdvanceTo(new SimulationTick(deathTick + GameCalendar.TicksPerYear));
    var reborn = world.GetAgentSnapshot(new AgentId(0));
    Equal(2, reborn.LifeNumber, "환생 후 생 번호");
    Equal(0L, reborn.AgeYears, "환생 나이");
    Equal(initialSoul, reborn.Aptitudes.Soul, "영혼 자질 윤회 연속성");
    True(
        reborn.Aptitudes.SpiritualRoot != initialAgent.Aptitudes.SpiritualRoot
        || reborn.Aptitudes.Bone != initialAgent.Aptitudes.Bone
        || reborn.Aptitudes.Constitution != initialAgent.Aptitudes.Constitution
        || reborn.Aptitudes.Comprehension != initialAgent.Aptitudes.Comprehension
        || reborn.Aptitudes.Mind != initialAgent.Aptitudes.Mind,
        "새 육신의 자질은 다시 형성되어야 합니다.");
    Equal(1, world.TotalReincarnations, "환생 집계");

    var chronicle = world.GetAgentChronicle(new AgentId(0));
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Death), "사망 일대기");
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.ReincarnationEntered), "윤회의 강 일대기");
    True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Rebirth), "환생 일대기");
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
    var prepared = checkpoint.Agents.Select(agent => agent with
    {
        Comprehension = 10_000,
        TechniqueMastery = 0,
        Activity = AgentActivity.TrainingTechnique,
        NextDecisionTick = 1,
        SkillProgress = new SkillProgressCheckpoint(0, 0, 0, 0),
    }).ToArray();
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
    True(checkpoint.SchemaVersion == 17, "100년 세계 역사서 저장 schema 17");
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
    Equal(9, WorldMapRules.AllLocations.Count, "3×3 지도 위치 수");
    Equal(9, WorldMapRules.AllMonsters.Count, "지형별 요수 수");
    Equal(1, WorldMapRules.AllLocations.Count(item => item.Biome == BiomeType.City), "중앙 도시 수");
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
    var rankingOnly = world.CreateSnapshot(25, 25, ObservationSections.None);
    var statisticsOnly = world.CreateSnapshot(25, 25, ObservationSections.Statistics);
    var summaryOnly = world.CreateSnapshot(25, 25, ObservationSections.StatisticsSummary);
    var realmDistributionOnly = world.CreateSnapshot(25, 25, ObservationSections.RealmDistribution);
    var cityOnly = world.CreateSnapshot(25, 25, ObservationSections.MarketSummary);

    Equal(1, exactSearch.Count, "정확한 Agent ID 검색 결과");
    Equal(leader.Id, exactSearch[0].Id, "정확한 Agent ID 검색 대상");
    True(nameSearch.Count > 0, "이름 검색 결과가 있어야 합니다.");
    Equal(new AgentId(0), firstPage[0].Id, "전체 탐색 첫 ID");
    Equal(new AgentId(9_999), finalPage[^1].Id, "전체 탐색 마지막 ID");
    Equal(150, snapshot.CombatPowerRankings.Count, "전투력 랭킹 조회 크기");
    Equal(150, snapshot.RealmRankings.Count, "경지 랭킹 조회 크기");
    Equal(150, snapshot.AptitudeRankings.Count, "자질 랭킹 조회 크기");
    Equal(0, rankingOnly.Statistics.RealmDistribution.Count, "비활성 통계 조회 생략");
    Equal(0, rankingOnly.Market.Goods.Count, "비활성 시장 조회 생략");
    Equal(0, rankingOnly.Map.Locations.Count, "비활성 지도 조회 생략");
    Equal(0, rankingOnly.RecentEvents.Count, "비활성 세계 로그 조회 생략");
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

    var selective = Stopwatch.StartNew();
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        // 현재 통계 탭: 단일 snapshot에서 화면에 펼친 경지 분포만 계산한다.
        sink = world.CreateSnapshot(
            1_000,
            150,
            ObservationSections.StatisticsSummary
                | ObservationSections.RealmDistribution
                | ObservationSections.CombatPowerRanking);
    }
    selective.Stop();
    GC.KeepAlive(sink);

    var reduction = legacy.ElapsedTicks == 0
        ? 0m
        : (legacy.ElapsedTicks - selective.ElapsedTicks) * 100m / legacy.ElapsedTicks;
    Console.WriteLine(
        $"UI PERF: 이전 이중 전체 조회 {legacy.Elapsed.TotalMilliseconds:N1} ms · 선택 조회 {selective.Elapsed.TotalMilliseconds:N1} ms · 감소 {reduction:0.0}% ({iterations:N0}회)");
    True(selective.ElapsedTicks < legacy.ElapsedTicks, "활성 UI 선택 조회는 이전 이중 전체 조회보다 빨라야 합니다.");
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

static void EconomyLoop()
{
    Equal(2, EconomyRules.AllMaterials.Count, "간략화 재료 수");
    Equal(1, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Treasure), "법보 재료 수");
    Equal(1, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Pill), "단약 재료 수");
    Equal(3, EconomyRules.TreasureSlotCount, "본명·방어·공격 법보 슬롯 수");
    Equal(44, EconomyRules.MarketGoodCount, "대경지별 완제품 포함 시장 상품 수");
    True(
        EconomyRules.GetPillGoodId(PillKind.Growth, RealmStage.QiRefining)
            != EconomyRules.GetPillGoodId(PillKind.Growth, RealmStage.FoundationEstablishment),
        "성장 단약은 대경지별 별도 상품");
    var high7999 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(7_999));
    var extreme8000 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(8_000));
    var extreme8100 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(8_100));
    Equal(high7999 * 2, extreme8000, "79.99→80.00 가격 격차는 품질 효과보다 2배 확대");
    Equal(extreme8000 * 2, extreme8100, "80 이상 품질 1.00마다 가격 2배");

    var world = World.Create(707, 2_000);
    var initialMarket = world.CreateSnapshot(1, 0).Market;
    Equal(EconomyRules.MarketGoodCount, initialMarket.Goods.Count, "시장 상품 수");
    True(
        initialMarket.Goods.Where(item => item.AverageQuality is null)
            .All(item => item.CurrentPrice == item.CityBasePrice),
        "품질 없는 초기 상품 가격은 도시 기준가");
    world.AdvanceTo(new SimulationTick(24_000));
    True(world.TotalMissionsCompleted > 0, "도시 임무가 완료되어야 합니다.");
    True(world.TotalTreasuresCrafted > 0, "법보 제작이 발생해야 합니다.");
    True(world.TotalPillsCrafted > 0, "단약 제작이 발생해야 합니다.");
    True(world.TotalMaterialsSold > 0, "재료 판매가 발생해야 합니다.");
    var market = world.CreateSnapshot(1, 0).Market;
    True(market.TreasurySpiritStones >= 0, "도시 시장 영석은 음수가 될 수 없습니다.");
    True(market.Goods.All(item => item.Stock >= 0), "시장 재고는 음수가 될 수 없습니다.");
    True(market.Goods.Any(item => item.Supply > 0), "임무 납품·판매·도시 생산이 공급으로 기록되어야 합니다.");
    True(market.Goods.Any(item => item.Demand > 0), "구매·도시 생산 소비가 수요로 기록되어야 합니다.");
    True(
        market.Goods.Any(item => item.CurrentPrice != item.CityBasePrice),
        "공급과 수요에 따라 시장 가격이 변동해야 합니다.");
    True(
        market.Goods.Single(item => item.Kind == MarketGoodKind.TechniqueBook).Supply > 0,
        "도시는 영석으로 공법서를 생산해야 합니다.");
    True(
        market.Goods.Single(item => item.Kind == MarketGoodKind.SpellBook).Supply > 0,
        "도시는 영석으로 법술서를 생산해야 합니다.");
    True(market.PurchaseTransactions > 0, "도시 시장 구매 거래 누계");
    True(market.SaleTransactions > 0, "도시 시장 판매 거래 누계");
    Equal(
        (long)world.TotalMissionsCompleted,
        market.MissionDeliveries + market.MiningMissionCount,
        "재료 납품·채굴 임무 누계");
    True(market.MaterialsProcessed > 0, "도시 가공 재료 누계");
    True(market.PillsProduced > 0, "도시 단약 생산 누계");
    True(market.TreasuresProduced > 0, "도시 법보 생산 누계");
    True(market.TechniqueBooksProduced > 0, "도시 공법서 생산 누계");
    True(market.SpellBooksProduced > 0, "도시 법술서 생산 누계");
    True(market.SpiritStonesMined > 0, "도시 유한 광맥 채굴 누계");

    var agentsWithEquipment = 0;
    var agentsWithPills = 0;
    var agentsWithMissionHistory = 0;
    for (var index = 0; index < world.Population; index++)
    {
        var agent = world.GetAgentSnapshot(new AgentId(index));
        Equal(3, agent.EquippedTreasures.Count, "개별 법보 슬롯 수");
        True(agent.TechniqueQuality.Hundredths <= 7_999, "시작 공법 품질 상한 79.99");
        True(agent.SpellQuality.Hundredths <= 7_999, "시작 법술 품질 상한 79.99");
        Equal(agent.InventoryItemCount, agent.Materials.Sum(item => item.Count), "개별 재료 상세 합계");
        if (agent.EquippedTreasures.Any(item => item.Quality is not null))
        {
            agentsWithEquipment++;
            True(agent.CombatPower.Treasure > 0, "장착 법보는 전투력에 반영되어야 합니다.");
        }

        if (agent.GrowthPills > 0 || agent.BreakthroughPills > 0)
        {
            agentsWithPills++;
        }

        if (agent.MissionsCompleted > 0)
        {
            agentsWithMissionHistory++;
        }
    }

    True(agentsWithEquipment > 0, "법보를 장착한 AI가 있어야 합니다.");
    True(agentsWithPills > 0, "단약을 보유한 AI가 있어야 합니다.");
    True(agentsWithMissionHistory > 0, "임무 이력이 있는 AI가 있어야 합니다.");
}

static void ProductionSkipsGoodsWithoutResources()
{
    var world = World.Create(708, 1);
    var checkpoint = world.CreateCheckpoint();
    var stock = (int[])checkpoint.Market.Stock.Clone();
    var demand = (long[])checkpoint.Market.Demand.Clone();
    var supply = (long[])checkpoint.Market.Supply.Clone();
    Array.Clear(stock);
    Array.Clear(demand);
    Array.Clear(supply);

    var blockedTreasure = EconomyRules.GetTreasureGoodId(
        TreasureSlot.AttackTreasure,
        RealmStage.QiRefining);
    demand[blockedTreasure] = 1_000;
    demand[EconomyRules.TechniqueBookGoodId] = 10;

    var market = checkpoint.Market with
    {
        TreasurySpiritStones = 10_000,
        Stock = stock,
        Demand = demand,
        Supply = supply,
        ProductionQueue = [],
        LastIndustryMonth = 0,
    };
    world = World.Restore(checkpoint with { Market = market });
    world.AdvanceTo(GameCalendar.GetMonthStart(1));

    var production = world.CreateSnapshot(1, 0).Market.ProductionQueue ?? [];
    True(production.Count > 0, "재료가 필요 없는 생산은 계속 시작되어야 합니다.");
    True(
        production.Any(item => item.Kind == MarketGoodKind.TechniqueBook),
        "재료 부족 법보 수요가 공법서 생산을 굶기면 안 됩니다.");
    True(
        production.All(item => item.GoodId != blockedTreasure),
        "재료가 없는 상품은 생산 대기열에 들어가면 안 됩니다.");
}

static void SaveCodecRoundTrip()
{
    var original = World.CreateDefault(314159);
    original.AdvanceTo(new SimulationTick(2_500));
    var checkpoint = original.CreateCheckpoint();
    var encoded = WorldSaveCodec.Encode(checkpoint);
    var decoded = WorldSaveCodec.Decode(encoded);
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

static void DefaultWorldPerformanceBudget()
{
    const long targetTick = 20_000;
    var stopwatch = Stopwatch.StartNew();
    var world = World.CreateDefault(20260830);
    world.AdvanceTo(new SimulationTick(targetTick));
    var snapshot = world.CreateSnapshot(150, 150);
    stopwatch.Stop();

    Console.WriteLine(
        $"PERF: {world.Population:N0}명 · {targetTick:N0} tick · {stopwatch.Elapsed.TotalMilliseconds:N0} ms");
    Equal(targetTick, snapshot.Tick.Value, "성능 검증 도달 tick");
    Equal(10_000, snapshot.Population, "성능 검증 인구");
    True(snapshot.TotalCombatsCompleted > 0, "성능 검증 중 전투가 완료되어야 합니다.");
    True(
        stopwatch.Elapsed < TimeSpan.FromSeconds(10),
        $"10,000명 시뮬레이션 성능 예산 10초를 초과했습니다: {stopwatch.Elapsed.TotalSeconds:N2}초");
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
