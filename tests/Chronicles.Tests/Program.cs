using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;
using System.Diagnostics;

var tests = new (string Name, Action Body)[]
{
    ("기본 인구와 목표 순위", DefaultPopulationAndGoal),
    ("게임 시간·배속·경지 수명", GameTimeAndRealmRules),
    ("년월일 달력과 월 1회 행동 제한", CalendarAndMonthlyActionLimit),
    ("Notion 수행 Work·준비·Preflight·안착 상태", CultivationStateMachine),
    ("한국식 큰 수 단위 표기", KoreanLargeNumberFormatting),
    ("8단계 품질 경계와 격차 배율", QualityTiersAndBoundaryMultipliers),
    ("0세 탄생·6대 자질·일대기·윤회", BirthAptitudesChronicleAndReincarnation),
    ("전투력 구성 요소와 랭킹", CombatPowerAndRanking),
    ("동일 seed 결정론", DeterministicWorld),
    ("이벤트 기반 성장", EventDrivenGrowth),
    ("활발한 랭킹 경쟁과 전투 경험", ActiveRankingCompetition),
    ("도시 임무·영석·재료·단약·법보 경제 순환", EconomyLoop),
    ("전체 분포·랭킹 변동·승패·사망 통계", WorldStatisticsQuery),
    ("관찰 조회 무영향", ObservationQueriesAreReadOnly),
    ("저장 codec 결정론 왕복", SaveCodecRoundTrip),
    ("손상 저장 거부", CorruptedSaveIsRejected),
    ("원자 저장 파일 왕복", AtomicSaveFileRoundTrip),
    ("10,000명 로컬 실시간 성능 예산", DefaultWorldPerformanceBudget),
};

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
    for (var index = 0; index < world.Population; index++)
    {
        Equal(1, world.GetAgentSnapshot(new AgentId(index)).TargetRank, "모든 AI의 목표 순위");
        Equal(0L, world.GetAgentSnapshot(new AgentId(index)).AgeYears, "모든 AI의 시작 나이");
    }
}

static void GameTimeAndRealmRules()
{
    Equal("0.0.2", ProductInfo.Version, "제품 버전");
    Equal(2_000L, GameCalendar.TicksPerYear, "1게임년 tick 수");
    Equal(10L, GameCalendar.GetYear(new SimulationTick(20_000)), "tick의 게임년 변환");
    Equal(1d, GameCalendar.GetRealSecondsPerGameYear(10), "10배속 게임년 현실 초");

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

static void CultivationStateMachine()
{
    var baseline = World.Create(551122, 1);
    var requirement = baseline.GetAgentSnapshot(new AgentId(0)).CultivationRequirement;
    var checkpoint = baseline.CreateCheckpoint();
    var preparedAgent = checkpoint.Agents[0] with
    {
        CultivationProgress = requirement,
        CultivationState = CultivationState.Progressing,
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
        attempted.CultivationState is CultivationState.Stabilizing or CultivationState.Bottleneck,
        "돌파 시도는 성공 안착 또는 원인이 있는 병목으로 끝나야 합니다.");
    if (attempted.CultivationState == CultivationState.Bottleneck)
    {
        True(
            attempted.BottleneckCause != CultivationBottleneckCause.None,
            "병목은 원인 없이 저장될 수 없습니다.");
    }

    var restored = World.Restore(world.CreateCheckpoint());
    var restoredAgent = restored.GetAgentSnapshot(new AgentId(0));
    True(attempted.CultivationState == restoredAgent.CultivationState, "수행 상태 저장 왕복");
    True(attempted.BottleneckCause == restoredAgent.BottleneckCause, "병목 원인 저장 왕복");

    foreach (var profile in RealmRules.All)
    {
        True(!string.IsNullOrWhiteSpace(profile.StableId), "Realm Profile 안정 ID");
        Equal(RealmProfile.CurrentRevision, profile.Revision, "Realm Profile revision");
        True(profile.RequiresStabilization, "돌파 성공 후 안착 상태");
    }
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
    var checkpoint = initial.CreateCheckpoint();
    var preparedAgent = checkpoint.Agents[0] with
    {
        BirthTick = 0,
        RealmStage = RealmStage.QiRefining,
        RealmLayer = (byte)MinorStage.Early,
        CultivationProgress = 0,
        Activity = AgentActivity.Cultivating,
        NextDecisionTick = deathTick,
        Opponent = -1,
    };
    var prepared = checkpoint with
    {
        Tick = deathTick - 1,
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

    var previousPower = long.MaxValue;
    foreach (var entry in snapshot.CombatPowerRankings)
    {
        True(entry.Category == RankingCategory.CombatPower, "전투력 랭킹 범주");
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

static void ActiveRankingCompetition()
{
    var world = World.CreateDefault(774);
    world.AdvanceTo(new SimulationTick(2_000));
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
    var snapshot = world.CreateSnapshot(150, 150);

    Equal(1, exactSearch.Count, "정확한 Agent ID 검색 결과");
    Equal(leader.Id, exactSearch[0].Id, "정확한 Agent ID 검색 대상");
    True(nameSearch.Count > 0, "이름 검색 결과가 있어야 합니다.");
    Equal(new AgentId(0), firstPage[0].Id, "전체 탐색 첫 ID");
    Equal(new AgentId(9_999), finalPage[^1].Id, "전체 탐색 마지막 ID");
    Equal(150, snapshot.CombatPowerRankings.Count, "전투력 랭킹 조회 크기");
    Equal(150, snapshot.RealmRankings.Count, "경지 랭킹 조회 크기");
    Equal(150, snapshot.AptitudeRankings.Count, "자질 랭킹 조회 크기");
    Equal(before, world.ComputeStateHash(), "관찰 조회는 세계 상태를 바꾸면 안 됩니다.");
}

static void WorldStatisticsQuery()
{
    var world = World.CreateDefault(808);
    world.AdvanceTo(new SimulationTick(2_000));
    var before = world.ComputeStateHash();
    var statistics = world.CreateStatistics();

    Equal(10, statistics.RealmDistribution.Count, "경지 분포 구간 수");
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
    Equal(30, EconomyRules.AllMaterials.Count, "전체 재료 수");
    Equal(10, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Treasure), "법보 재료 수");
    Equal(10, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Pill), "단약 재료 수");
    Equal(10, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Miscellaneous), "기타 재료 수");
    Equal(7, EconomyRules.TreasureSlotCount, "법보 장착 슬롯 수");

    var world = World.Create(707, 2_000);
    var initialMarket = world.CreateSnapshot(1, 0).Market;
    Equal(EconomyRules.MarketGoodCount, initialMarket.Goods.Count, "시장 상품 수");
    True(
        initialMarket.Goods.All(item => item.CurrentPrice == item.CityBasePrice),
        "시장 초기 가격은 도시 기준가여야 합니다.");
    world.AdvanceTo(new SimulationTick(8_000));
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

    var agentsWithEquipment = 0;
    var agentsWithPills = 0;
    var agentsWithMissionHistory = 0;
    for (var index = 0; index < world.Population; index++)
    {
        var agent = world.GetAgentSnapshot(new AgentId(index));
        Equal(7, agent.EquippedTreasures.Count, "개별 법보 슬롯 수");
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

static void SaveCodecRoundTrip()
{
    var original = World.CreateDefault(314159);
    original.AdvanceTo(new SimulationTick(2_500));
    var checkpoint = original.CreateCheckpoint();
    var encoded = WorldSaveCodec.Encode(checkpoint);
    var decoded = WorldSaveCodec.Decode(encoded);
    Equal(checkpoint.RandomState, decoded.RandomState, "저장 왕복 RNG 상태");
    Equal(checkpoint.EventSequence, decoded.EventSequence, "저장 왕복 사건 sequence");
    Equal(checkpoint.TotalChallenges, decoded.TotalChallenges, "저장 왕복 도전 합계");
    Equal(checkpoint.TotalMissionsCompleted, decoded.TotalMissionsCompleted, "저장 왕복 임무 합계");
    Equal(checkpoint.LastRankingRefreshTick, decoded.LastRankingRefreshTick, "저장 왕복 랭킹 갱신 시점");
    Equal(checkpoint.RankingRisenCount, decoded.RankingRisenCount, "저장 왕복 순위 상승 집계");
    Equal(checkpoint.Market.TreasurySpiritStones, decoded.Market.TreasurySpiritStones, "저장 왕복 도시 영석");
    True(checkpoint.Market.Stock.SequenceEqual(decoded.Market.Stock), "저장 왕복 시장 재고");
    True(checkpoint.Market.Prices.SequenceEqual(decoded.Market.Prices), "저장 왕복 시장 가격");
    Equal(checkpoint.Agents[0].Name, decoded.Agents[0].Name, "저장 왕복 첫 Agent 이름");
    Equal(checkpoint.Agents[0].Soul, decoded.Agents[0].Soul, "저장 왕복 첫 Agent 영혼");
    True(
        checkpoint.Agents[0].MaterialInventory.SequenceEqual(decoded.Agents[0].MaterialInventory),
        "저장 왕복 첫 Agent 재료");
    True(
        checkpoint.Agents[0].EquippedTreasureQualities.SequenceEqual(decoded.Agents[0].EquippedTreasureQualities),
        "저장 왕복 첫 Agent 법보");
    True(
        checkpoint.Agents[0].Chronicle.SequenceEqual(decoded.Agents[0].Chronicle),
        "저장 왕복 첫 Agent 일대기");
    Equal(checkpoint.Agents[^1].Name, decoded.Agents[^1].Name, "저장 왕복 마지막 Agent 이름");
    True(
        checkpoint.Agents[^1].Chronicle.SequenceEqual(decoded.Agents[^1].Chronicle),
        "저장 왕복 마지막 Agent 일대기");
    Equal(checkpoint.Events.Length, decoded.Events.Length, "저장 왕복 사건 수");
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
    const long targetTick = 5_000;
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
